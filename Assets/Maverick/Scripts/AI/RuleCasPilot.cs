using UnityEngine;
using EaglePhysicalAI.Aircraft;
using EaglePhysicalAI.Battlefield;
using EaglePhysicalAI.CAS;

namespace EaglePhysicalAI.AI
{
    /// <summary>
    /// Rule-based pilot that chooses high-level game intents.
    /// It does not directly model real tactics; it only drives the Unity test aircraft through abstract goals.
    /// </summary>
    [RequireComponent(typeof(AircraftPhysicsController))]
    [RequireComponent(typeof(WaypointAutopilot))]
    public class RuleCasPilot : MonoBehaviour
    {
        public bool aiEnabled;
        public AircraftIntent currentIntent = AircraftIntent.FollowWaypoints;

        [Header("References")]
        public AircraftPhysicsController aircraft;
        public WaypointAutopilot autopilot;
        public CasRequestManager requestManager;
        public CasValidator validator;
        public AbstractStrikeSystem strikeSystem;
        public Transform returnBase;

        [Header("Intent Thresholds")]
        public float dangerStallRisk = 0.7f;
        public float minSafeAltitude = 120f;
        public float approachDistance = 1800f;
        public float orbitDistance = 1100f;
        public float strikeDecisionInterval = 1.0f;

        [Header("Autopilot Presets")]
        public float approachSpeed = 230f;
        public float orbitSpeed = 190f;
        public float breakAwayAltitude = 850f;
        public float orbitRadius = 900f;

        public string lastDecisionReason;
        private float _lastStrikeDecisionTime;

        private void Awake()
        {
            if (aircraft == null) aircraft = GetComponent<AircraftPhysicsController>();
            if (autopilot == null) autopilot = GetComponent<WaypointAutopilot>();
            if (requestManager == null) requestManager = FindObjectOfType<CasRequestManager>();
            if (validator == null) validator = FindObjectOfType<CasValidator>();
            if (strikeSystem == null) strikeSystem = GetComponent<AbstractStrikeSystem>();
        }

        private void FixedUpdate()
        {
            if (!aiEnabled) return;
            ChooseIntent();
            ApplyIntent();
        }

        private void ChooseIntent()
        {
            if (aircraft.IsCrashed)
            {
                currentIntent = AircraftIntent.Manual;
                lastDecisionReason = "aircraft_crashed";
                return;
            }

            if (aircraft.StallRisk >= dangerStallRisk || aircraft.Altitude < minSafeAltitude)
            {
                currentIntent = AircraftIntent.StabilizeAircraft;
                lastDecisionReason = "recover_stability_or_altitude";
                return;
            }

            CasRequest request = requestManager != null ? requestManager.activeRequest : null;
            if (request == null || request.target == null || !request.active)
            {
                currentIntent = returnBase != null ? AircraftIntent.ReturnToBase : AircraftIntent.FollowWaypoints;
                lastDecisionReason = "no_active_cas_request";
                return;
            }

            float distance = Vector3.Distance(transform.position, request.target.transform.position);
            if (distance > approachDistance)
            {
                currentIntent = AircraftIntent.ApproachCasZone;
                lastDecisionReason = "far_from_request";
                return;
            }

            if (distance > orbitDistance)
            {
                currentIntent = AircraftIntent.OrbitCasZone;
                lastDecisionReason = "near_request_building_orbit";
                return;
            }

            CasValidationResult result = validator != null
                ? validator.ValidateStrike(transform, request.target)
                : CasValidationResult.Allow("no_validator", 0.5f, 0f, 0.5f);

            if (result.allowed)
            {
                currentIntent = AircraftIntent.StrikeOrAbort;
                lastDecisionReason = result.reason;
            }
            else
            {
                currentIntent = AircraftIntent.BreakAway;
                lastDecisionReason = result.reason;
            }
        }

        private void ApplyIntent()
        {
            CasRequest request = requestManager != null ? requestManager.activeRequest : null;

            switch (currentIntent)
            {
                case AircraftIntent.StabilizeAircraft:
                    autopilot.autopilotEnabled = true;
                    autopilot.orbitMode = false;
                    autopilot.directTarget = null;
                    autopilot.targetAltitude = Mathf.Max(breakAwayAltitude, aircraft.Altitude + 250f);
                    autopilot.targetSpeed = approachSpeed;
                    aircraft.SetControlInputs(0f, 0f, 0f, 1f);
                    break;

                case AircraftIntent.FollowWaypoints:
                    autopilot.autopilotEnabled = true;
                    autopilot.orbitMode = false;
                    autopilot.directTarget = null;
                    autopilot.targetSpeed = approachSpeed;
                    break;

                case AircraftIntent.ApproachCasZone:
                    if (request?.target != null)
                    {
                        autopilot.targetSpeed = approachSpeed;
                        autopilot.targetAltitude = Mathf.Max(450f, request.target.transform.position.y + 450f);
                        autopilot.SetDirectTarget(request.target.transform, true);
                    }
                    break;

                case AircraftIntent.OrbitCasZone:
                    if (request?.target != null)
                    {
                        autopilot.targetSpeed = orbitSpeed;
                        autopilot.targetAltitude = Mathf.Max(450f, request.target.transform.position.y + 500f);
                        autopilot.SetOrbitTarget(request.target.transform, orbitRadius, 1, true);
                    }
                    break;

                case AircraftIntent.StrikeOrAbort:
                    if (request?.target != null)
                    {
                        autopilot.targetSpeed = approachSpeed;
                        autopilot.SetDirectTarget(request.target.transform, true);
                        if (Time.time - _lastStrikeDecisionTime >= strikeDecisionInterval)
                        {
                            _lastStrikeDecisionTime = Time.time;
                            if (strikeSystem != null) strikeSystem.TryStrike(request.target);
                        }
                    }
                    break;

                case AircraftIntent.BreakAway:
                    autopilot.autopilotEnabled = true;
                    autopilot.orbitMode = false;
                    autopilot.directTarget = returnBase;
                    autopilot.targetAltitude = breakAwayAltitude;
                    autopilot.targetSpeed = approachSpeed;
                    break;

                case AircraftIntent.ReturnToBase:
                    autopilot.autopilotEnabled = true;
                    autopilot.orbitMode = false;
                    autopilot.directTarget = returnBase;
                    autopilot.targetSpeed = approachSpeed;
                    break;
            }
        }
    }
}
