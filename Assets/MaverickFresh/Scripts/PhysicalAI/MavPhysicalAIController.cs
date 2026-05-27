using UnityEngine;

namespace MaverickFresh
{
    public enum MavPhysicalAIMode
    {
        Player = 0,
        AirIntercept = 1,
        CASAttack = 2,
        Orbit = 3,
        Recovery = 4
    }

    /// <summary>
    /// v0.16 Physical AI Core.
    ///
    /// This is not ML-Agents yet. This is the first real physical closed-loop AI:
    /// - senses Rigidbody state
    /// - chooses a desired aim direction / throttle
    /// - drives the same MouseFlight + Instructor system as the player
    /// - can intercept air targets or attack CAS targets
    /// - produces data that can later become imitation/RL samples
    /// </summary>
    [DisallowMultipleComponent]
    public class MavPhysicalAIController : MonoBehaviour
    {
        [Header("References")]
        public MavMouseFlightJet jet;
        public MavInstructorController instructor;
        public MavMouseFlightRig rig;
        public Rigidbody rb;
        public MavCASTargetingSystem casTargeting;
        public MavCASWeaponSystem casWeapons;

        [Header("Mode")]
        public bool aiEnabled = false;
        public MavPhysicalAIMode mode = MavPhysicalAIMode.AirIntercept;
        public KeyCode toggleAIKey = KeyCode.F11;
        public KeyCode cycleModeKey = KeyCode.F3;

        [Header("Air Target")]
        public Transform airTarget;
        public bool autoFindAirTarget = true;
        public string airTargetNameContains = "AirTarget_AI";
        public float desiredAirDistance = 850f;
        public float minimumAirDistance = 280f;
        public float leadStrength = 0.42f;
        public float aimSmooth = 6.5f;

        [Header("CAS Target")]
        public MavCASTarget casTarget;
        public bool autoFindCASTarget = true;
        public float casAttackAltitude = 650f;
        public float casStandOffDistance = 1500f;
        public float casDiveDistance = 1150f;
        public float casPulloutAltitude = 220f;
        public bool allowAutoDesignate = true;
        public bool allowAutoFire = false;
        public MavCASWeapon preferredCASWeapon = MavCASWeapon.Rockets;

        [Header("Energy / Safety")]
        public float minimumAltitude = 120f;
        public float recoveryAltitude = 260f;
        public float desiredSpeed = 260f;
        public float minimumSpeed = 135f;
        public float maximumSpeed = 430f;
        public float throttleUpBelowSpeed = 210f;
        public float throttleDownAboveSpeed = 390f;
        public float maxNoseUpRecoveryBlend = 0.85f;
        public float groundAvoidanceLookahead = 650f;

        [Header("Orbit")]
        public Vector3 orbitCenter = new Vector3(0f, 650f, 1600f);
        public float orbitRadius = 900f;
        public float orbitHeight = 650f;
        public float orbitAngularSpeed = 18f;

        [Header("Runtime Sensor State")]
        public float speed;
        public float altitude;
        public float targetDistance;
        public float targetAngle;
        public float groundDistance;
        public Vector3 desiredAimDirection;
        public Vector3 desiredAimPoint;
        public float desiredThrottle;
        public bool wantsFire;
        public string aiState = "player";
        public string lastDecision = "none";

        private float orbitAngle;
        private float nextTargetSearchTime;
        private float nextFireTime;

        private void Awake()
        {
            Resolve();
        }

        private void Update()
        {
            Resolve();

            if (MavFreshInput.GetKeyDown(toggleAIKey))
                aiEnabled = !aiEnabled;

            if (MavFreshInput.GetKeyDown(cycleModeKey))
                CycleMode();

            Sense();

            if (!aiEnabled || mode == MavPhysicalAIMode.Player)
            {
                aiState = "player";
                return;
            }

            if (NeedsRecovery())
                RunRecovery();
            else if (mode == MavPhysicalAIMode.AirIntercept)
                RunAirIntercept();
            else if (mode == MavPhysicalAIMode.CASAttack)
                RunCASAttack();
            else if (mode == MavPhysicalAIMode.Orbit)
                RunOrbit();
            else if (mode == MavPhysicalAIMode.Recovery)
                RunRecovery();
        }

        private void LateUpdate()
        {
            if (!aiEnabled || mode == MavPhysicalAIMode.Player)
                return;

            ApplyCommand();
        }

        public void Resolve()
        {
            if (jet == null) jet = GetComponent<MavMouseFlightJet>();
            if (instructor == null && jet != null) instructor = jet.instructor;
            if (instructor == null) instructor = GetComponent<MavInstructorController>();
            if (rig == null) rig = FindObjectOfType<MavMouseFlightRig>();
            if (rb == null) rb = GetComponent<Rigidbody>();
            if (casTargeting == null) casTargeting = GetComponent<MavCASTargetingSystem>();
            if (casWeapons == null) casWeapons = GetComponent<MavCASWeaponSystem>();
        }

        public void CycleMode()
        {
            int count = System.Enum.GetValues(typeof(MavPhysicalAIMode)).Length;
            int next = ((int)mode + 1) % count;
            mode = (MavPhysicalAIMode)next;
            lastDecision = "cycle_mode_" + mode;
        }

        private void Sense()
        {
            speed = rb != null ? rb.linearVelocity.magnitude : 0f;
            altitude = transform.position.y;
            groundDistance = EstimateGroundDistance();

            if (Time.time >= nextTargetSearchTime)
            {
                if (autoFindAirTarget && airTarget == null)
                    FindAirTarget();

                if (autoFindCASTarget && (casTarget == null || !casTarget.IsAlive()))
                    FindCASTarget();

                nextTargetSearchTime = Time.time + 0.75f;
            }

            Transform t = GetCurrentTargetTransform();
            if (t != null)
            {
                Vector3 to = t.position - transform.position;
                targetDistance = to.magnitude;
                targetAngle = Vector3.Angle(transform.forward, to);
            }
            else
            {
                targetDistance = 0f;
                targetAngle = 0f;
            }
        }

        private bool NeedsRecovery()
        {
            if (altitude < minimumAltitude)
                return true;

            if (groundDistance > 0f && groundDistance < minimumAltitude * 0.8f)
                return true;

            if (speed < minimumSpeed && altitude < recoveryAltitude * 1.6f)
                return true;

            if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, groundAvoidanceLookahead))
            {
                // If flying straight into terrain/ground, recover.
                if (Vector3.Dot(hit.normal, Vector3.up) > 0.35f)
                    return true;
            }

            return false;
        }

        private void RunRecovery()
        {
            Vector3 safeDir = Vector3.Slerp(transform.forward, Vector3.up, maxNoseUpRecoveryBlend).normalized;
            desiredAimDirection = safeDir;
            desiredAimPoint = transform.position + desiredAimDirection * 1200f;
            desiredThrottle = 1.15f;
            wantsFire = false;
            aiState = "recovery";
            lastDecision = "recover_altitude_or_speed";
        }

        private void RunAirIntercept()
        {
            if (airTarget == null)
            {
                desiredAimDirection = transform.forward;
                desiredAimPoint = transform.position + transform.forward * 1000f;
                desiredThrottle = 0.85f;
                wantsFire = false;
                aiState = "air_no_target";
                lastDecision = "search_air_target";
                return;
            }

            MavAITargetDrone targetDrone = airTarget.GetComponent<MavAITargetDrone>();
            Rigidbody targetRb = airTarget.GetComponent<Rigidbody>();
            Vector3 targetVel = targetDrone != null ? targetDrone.currentVelocity : targetRb != null ? targetRb.linearVelocity : Vector3.zero;

            float leadTime = Mathf.Clamp(targetDistance / Mathf.Max(180f, speed + 120f), 0f, 2.8f) * leadStrength;
            Vector3 predicted = airTarget.position + targetVel * leadTime;

            if (targetDistance < minimumAirDistance)
            {
                // Break slightly upward/sideways if too close.
                Vector3 away = (transform.position - airTarget.position).normalized;
                predicted = transform.position + Vector3.Slerp(transform.forward, away + Vector3.up * 0.4f, 0.6f).normalized * 1000f;
                aiState = "air_break";
            }
            else
            {
                aiState = targetAngle < 12f ? "air_track" : "air_intercept";
            }

            desiredAimPoint = predicted;
            desiredAimDirection = (desiredAimPoint - transform.position).normalized;
            desiredThrottle = ComputeThrottleForSpeed(desiredSpeed);
            wantsFire = targetAngle < 3.5f && targetDistance < desiredAirDistance;
            lastDecision = "lead_intercept";
        }

        private void RunCASAttack()
        {
            if (casTarget == null || !casTarget.IsAlive())
            {
                FindCASTarget();

                if (casTarget == null)
                {
                    RunOrbit();
                    aiState = "cas_no_target_orbit";
                    return;
                }
            }

            Vector3 targetPoint = casTarget.transform.position;
            Vector3 toTarget = targetPoint - transform.position;
            targetDistance = toTarget.magnitude;
            targetAngle = Vector3.Angle(transform.forward, toTarget);

            if (allowAutoDesignate && casTargeting != null)
            {
                casTargeting.designatedTarget = casTarget;
                casTargeting.designatedPoint = casTarget.transform.position;
                casTargeting.hasDesignatedPoint = true;
                casTargeting.status = "physical_ai_designated_" + casTarget.displayName;
            }

            // Approach point is above and behind target until close enough, then dive aim toward target.
            Vector3 approachPoint = targetPoint + Vector3.up * casAttackAltitude - transform.forward * 120f;

            if (targetDistance > casDiveDistance)
            {
                desiredAimPoint = approachPoint;
                desiredAimDirection = (desiredAimPoint - transform.position).normalized;
                desiredThrottle = ComputeThrottleForSpeed(desiredSpeed);
                wantsFire = false;
                aiState = "cas_approach";
                lastDecision = "cas_approach";
            }
            else if (altitude < casPulloutAltitude)
            {
                RunRecovery();
                aiState = "cas_pullout";
            }
            else
            {
                desiredAimPoint = targetPoint;
                desiredAimDirection = (desiredAimPoint - transform.position).normalized;
                desiredThrottle = 0.82f;
                wantsFire = targetAngle < 8f && targetDistance < casStandOffDistance;
                aiState = wantsFire ? "cas_attack_fire_window" : "cas_attack_align";
                lastDecision = "cas_attack";
            }

            if (allowAutoFire && wantsFire && casWeapons != null && Time.time >= nextFireTime)
            {
                casWeapons.selectedWeapon = preferredCASWeapon;
                casWeapons.TryFireSelected();
                nextFireTime = Time.time + 0.9f;
                lastDecision = "cas_fire_" + preferredCASWeapon;
            }
        }

        private void RunOrbit()
        {
            orbitAngle += orbitAngularSpeed * Time.deltaTime;
            float rad = orbitAngle * Mathf.Deg2Rad;

            Vector3 point = orbitCenter + new Vector3(Mathf.Cos(rad) * orbitRadius, 0f, Mathf.Sin(rad) * orbitRadius);
            point.y = orbitHeight;

            desiredAimPoint = point;
            desiredAimDirection = (desiredAimPoint - transform.position).normalized;
            desiredThrottle = ComputeThrottleForSpeed(desiredSpeed * 0.92f);
            wantsFire = false;
            aiState = "orbit";
            lastDecision = "orbit";
        }

        private void ApplyCommand()
        {
            if (desiredAimDirection.sqrMagnitude < 0.001f)
                desiredAimDirection = transform.forward;

            if (rig != null && rig.mouseAim != null)
            {
                float t = 1f - Mathf.Exp(-aimSmooth * Time.deltaTime);
                rig.mouseAim.position = transform.position;
                rig.mouseAim.forward = Vector3.Slerp(rig.mouseAim.forward, desiredAimDirection, t);
            }

            if (jet != null)
            {
                float currentThrottle = instructor != null ? instructor.throttleIntent : jet.throttle;
                float nextThrottle = Mathf.MoveTowards(currentThrottle, desiredThrottle, Time.deltaTime * 0.85f);

                if (instructor != null)
                    instructor.SetThrottleIntent(nextThrottle);

                jet.throttle = nextThrottle;
            }
        }

        private float ComputeThrottleForSpeed(float targetSpeed)
        {
            if (speed < throttleUpBelowSpeed)
                return 1.15f;

            if (speed > throttleDownAboveSpeed)
                return 0.35f;

            float t = Mathf.InverseLerp(minimumSpeed, maximumSpeed, targetSpeed);
            return Mathf.Lerp(0.62f, 1.05f, t);
        }

        private float EstimateGroundDistance()
        {
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 3000f))
                return hit.distance;

            return altitude;
        }

        private Transform GetCurrentTargetTransform()
        {
            if (mode == MavPhysicalAIMode.CASAttack && casTarget != null)
                return casTarget.transform;

            if (airTarget != null)
                return airTarget;

            return null;
        }

        [ContextMenu("Find Air Target")]
        public void FindAirTarget()
        {
            string nameNeedle = string.IsNullOrEmpty(airTargetNameContains)
                ? "airtarget"
                : airTargetNameContains.ToLowerInvariant();

            GameObject[] all = FindObjectsOfType<GameObject>();
            Transform best = null;
            float bestDist = float.MaxValue;

            foreach (GameObject go in all)
            {
                if (go == null) continue;
                if (!go.name.ToLowerInvariant().Contains(nameNeedle)) continue;

                float d = Vector3.Distance(transform.position, go.transform.position);
                if (d < bestDist && go.transform != transform)
                {
                    bestDist = d;
                    best = go.transform;
                }
            }

            airTarget = best;
        }

        [ContextMenu("Find CAS Target")]
        public void FindCASTarget()
        {
            MavCASTarget[] all = FindObjectsOfType<MavCASTarget>();
            MavCASTarget best = null;
            float bestScore = float.MaxValue;

            foreach (MavCASTarget t in all)
            {
                if (t == null || !t.IsAlive())
                    continue;

                float d = Vector3.Distance(transform.position, t.transform.position);
                if (d < bestScore)
                {
                    bestScore = d;
                    best = t;
                }
            }

            casTarget = best;
        }

        private void OnDrawGizmos()
        {
            if (!aiEnabled)
                return;

            Gizmos.color = Color.magenta;
            Gizmos.DrawRay(transform.position, desiredAimDirection * 220f);
            Gizmos.DrawWireSphere(desiredAimPoint, 20f);

            if (casTarget != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, casTarget.transform.position);
            }

            if (airTarget != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, airTarget.position);
            }
        }
    }
}
