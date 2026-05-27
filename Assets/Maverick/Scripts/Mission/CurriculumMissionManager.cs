using UnityEngine;
using EaglePhysicalAI.Aircraft;
using EaglePhysicalAI.Battlefield;
using EaglePhysicalAI.CAS;
using EaglePhysicalAI.Sensors.Fusion;
using EaglePhysicalAI.Sensors.TargetingPod;
using EaglePhysicalAI.Safety;
using EaglePhysicalAI.Scenario;

namespace EaglePhysicalAI.Mission
{
    /// <summary>
    /// Lightweight curriculum controller for building tester missions and AI training episodes.
    /// It advances through simple stages: fly, approach, orbit, confirm, strike/abort, RTB.
    /// </summary>
    public class CurriculumMissionManager : MonoBehaviour
    {
        [Header("References")]
        public AircraftPhysicsController aircraft;
        public CasRequestManager requestManager;
        public AbstractStrikeSystem strikeSystem;
        public CasValidator validator;
        public SensorFusionManager fusion;
        public TargetingPodSystem targetingPod;
        public FlightEnvelopeSafetyGuard safetyGuard;
        public MissionWaypoint ingressWaypoint;
        public MissionWaypoint orbitWaypoint;
        public MissionWaypoint returnWaypoint;
        public GroundUnit scriptedTarget;
        public GroundUnit scriptedRequester;

        [Header("Stage Rules")]
        public bool autoCreateCasRequest = true;
        public float freeFlightMinSeconds = 8f;
        public float orbitRadius = 900f;
        public float orbitHoldSeconds = 12f;
        public float sensorConfirmConfidence = 0.55f;
        public float maxStageSeconds = 180f;

        [Header("State")]
        public CurriculumStage stage = CurriculumStage.FreeFlight;
        public float stageTime;
        public float missionTime;
        public float stageProgress;
        public string stageNote = "start";
        public bool missionFailed;
        public bool missionComplete;
        public float curriculumScore;

        private int _lastSuccessfulStrikes;
        private int _lastAbortedStrikes;
        private float _orbitTimer;

        private void Awake()
        {
            if (aircraft == null) aircraft = FindObjectOfType<AircraftPhysicsController>();
            if (requestManager == null) requestManager = FindObjectOfType<CasRequestManager>();
            if (strikeSystem == null) strikeSystem = FindObjectOfType<AbstractStrikeSystem>();
            if (validator == null) validator = FindObjectOfType<CasValidator>();
            if (fusion == null) fusion = FindObjectOfType<SensorFusionManager>();
            if (targetingPod == null) targetingPod = FindObjectOfType<TargetingPodSystem>();
            if (safetyGuard == null) safetyGuard = FindObjectOfType<FlightEnvelopeSafetyGuard>();
        }

        private void Start()
        {
            if (strikeSystem != null)
            {
                _lastSuccessfulStrikes = strikeSystem.successfulStrikes;
                _lastAbortedStrikes = strikeSystem.abortedStrikes;
            }
        }

        private void Update()
        {
            if (missionComplete || missionFailed) return;
            missionTime += Time.deltaTime;
            stageTime += Time.deltaTime;

            if (aircraft != null && aircraft.IsCrashed)
            {
                missionFailed = true;
                stageNote = "failed_crash";
                curriculumScore -= 250f;
                return;
            }

            if (safetyGuard != null && safetyGuard.state == FlightSafetyState.Recovery)
            {
                curriculumScore -= 0.02f * Time.deltaTime;
            }

            if (stageTime > maxStageSeconds)
            {
                missionFailed = true;
                stageNote = "failed_stage_timeout";
                curriculumScore -= 60f;
                return;
            }

            StepStage();
        }

        private void StepStage()
        {
            switch (stage)
            {
                case CurriculumStage.FreeFlight:
                    stageProgress = Mathf.Clamp01(stageTime / Mathf.Max(1f, freeFlightMinSeconds));
                    stageNote = "stabilize_aircraft";
                    if (stageTime >= freeFlightMinSeconds) Advance(CurriculumStage.WaypointApproach, 10f);
                    break;

                case CurriculumStage.WaypointApproach:
                    stageNote = "reach_ingress_waypoint";
                    if (aircraft != null && ingressWaypoint != null)
                    {
                        float distance = Vector3.Distance(aircraft.transform.position, ingressWaypoint.transform.position);
                        stageProgress = 1f - Mathf.Clamp01(distance / 5000f);
                        if (ingressWaypoint.IsReachedBy(aircraft.transform.position)) Advance(CurriculumStage.CasOrbit, 25f);
                    }
                    else
                    {
                        Advance(CurriculumStage.CasOrbit, 5f);
                    }
                    break;

                case CurriculumStage.CasOrbit:
                    EnsureCasRequest();
                    stageNote = "hold_cas_orbit";
                    stageProgress = Mathf.Clamp01(_orbitTimer / Mathf.Max(1f, orbitHoldSeconds));
                    if (aircraft != null && orbitWaypoint != null)
                    {
                        float distance = Vector3.Distance(aircraft.transform.position, orbitWaypoint.transform.position);
                        if (Mathf.Abs(distance - orbitRadius) < orbitRadius * 0.45f) _orbitTimer += Time.deltaTime;
                    }
                    else
                    {
                        _orbitTimer += Time.deltaTime;
                    }
                    if (_orbitTimer >= orbitHoldSeconds) Advance(CurriculumStage.SensorConfirm, 30f);
                    break;

                case CurriculumStage.SensorConfirm:
                    EnsureCasRequest();
                    stageNote = "confirm_target_with_sensors";
                    float confidence = fusion != null ? fusion.fusedConfidence : 0f;
                    stageProgress = Mathf.Clamp01(confidence / Mathf.Max(0.01f, sensorConfirmConfidence));
                    if (confidence >= sensorConfirmConfidence || (targetingPod != null && targetingPod.ResolveTrackedGroundUnit() != null))
                    {
                        Advance(CurriculumStage.StrikeOrAbort, 35f);
                    }
                    break;

                case CurriculumStage.StrikeOrAbort:
                    EnsureCasRequest();
                    stageNote = "strike_only_if_validator_allows";
                    stageProgress = GetStrikeWindowProgress();
                    if (strikeSystem != null)
                    {
                        if (strikeSystem.successfulStrikes > _lastSuccessfulStrikes)
                        {
                            Advance(CurriculumStage.ReturnToBase, 90f);
                        }
                        else if (strikeSystem.abortedStrikes > _lastAbortedStrikes)
                        {
                            _lastAbortedStrikes = strikeSystem.abortedStrikes;
                            curriculumScore += 5f;
                            stageNote = "abort_recorded_good_if_unsafe";
                        }
                    }
                    break;

                case CurriculumStage.ReturnToBase:
                    stageNote = "return_to_base_or_exit_area";
                    if (aircraft != null && returnWaypoint != null)
                    {
                        float distance = Vector3.Distance(aircraft.transform.position, returnWaypoint.transform.position);
                        stageProgress = 1f - Mathf.Clamp01(distance / 6500f);
                        if (returnWaypoint.IsReachedBy(aircraft.transform.position)) Advance(CurriculumStage.Complete, 70f);
                    }
                    else
                    {
                        Advance(CurriculumStage.Complete, 30f);
                    }
                    break;

                case CurriculumStage.Complete:
                    missionComplete = true;
                    stageProgress = 1f;
                    stageNote = "mission_complete";
                    break;
            }
        }

        private void Advance(CurriculumStage nextStage, float scoreBonus)
        {
            curriculumScore += scoreBonus;
            stage = nextStage;
            stageTime = 0f;
            stageProgress = 0f;
            stageNote = "advance_" + nextStage;
        }

        private void EnsureCasRequest()
        {
            if (!autoCreateCasRequest || requestManager == null || scriptedTarget == null) return;
            if (requestManager.activeRequest != null && requestManager.activeRequest.active) return;
            requestManager.CreateRequest(scriptedRequester, scriptedTarget, scriptedTarget.transform.position, 1f, "curriculum_auto_request");
        }

        private float GetStrikeWindowProgress()
        {
            if (validator == null || aircraft == null) return 0f;
            GroundUnit target = scriptedTarget;
            if (target == null && requestManager != null && requestManager.activeRequest != null) target = requestManager.activeRequest.target;
            if (target == null) return 0f;
            var result = validator.ValidateStrike(aircraft.transform, target);
            return result.allowed ? 1f : Mathf.Clamp01((result.targetConfidence + result.geometryScore) * 0.5f * (1f - result.friendlyRisk));
        }
    }
}
