using UnityEngine;

namespace MaverickFresh
{
    public enum MavPilotMode
    {
        Player = 0,
        SimpleAI = 1
    }

    /// <summary>
    /// Physical AI step 1.5:
    /// A simple autopilot-like AI pilot that drives the existing MavMouseFlightRig target.
    /// It does not replace the physics model. It only moves the mouse aim direction toward a target.
    /// </summary>
    [DisallowMultipleComponent]
    public class MavSimpleAIPilot : MonoBehaviour
    {
        [Header("References")]
        public MavMouseFlightJet jet;
        public MavInstructorController instructor;
        public MavMouseFlightRig rig;
        public Transform target;
        public Rigidbody rb;

        [Header("Mode")]
        public MavPilotMode mode = MavPilotMode.Player;
        public KeyCode toggleAIKey = KeyCode.F10;

        [Header("Target Seeking")]
        public bool autoFindTarget = true;
        public string targetNameContains = "AirTarget";
        public float desiredDistance = 600f;
        public float attackDistance = 450f;
        public float leadAmount = 0.35f;
        public float targetAimSmooth = 5.5f;

        [Header("AI Behavior")]
        public float minThrottle = 0.55f;
        public float maxThrottle = 1.15f;
        public float closeDistanceThrottle = 0.35f;
        public float farDistanceThrottle = 1.05f;
        public float altitudeFloor = 120f;
        public float altitudeRecoveryPitchBias = 0.35f;

        [Header("Runtime")]
        public Vector3 desiredAimDirection;
        public float distanceToTarget;
        public float angleToTarget;
        public string aiState = "player";

        private void Awake()
        {
            Resolve();
        }

        private void Update()
        {
            if (MavFreshInput.GetKeyDown(toggleAIKey))
            {
                mode = mode == MavPilotMode.Player ? MavPilotMode.SimpleAI : MavPilotMode.Player;
            }

            if (mode == MavPilotMode.Player)
            {
                aiState = "player";
                return;
            }

            Resolve();

            if (target == null && autoFindTarget)
                FindTarget();

            if (jet == null || rig == null || rig.mouseAim == null)
                return;

            RunSimpleAI();
        }

        [ContextMenu("Set Player Mode")]
        public void SetPlayerMode()
        {
            mode = MavPilotMode.Player;
        }

        [ContextMenu("Set Simple AI Mode")]
        public void SetSimpleAIMode()
        {
            mode = MavPilotMode.SimpleAI;
        }

        [ContextMenu("Find Target")]
        public void FindTarget()
        {
            string nameNeedle = string.IsNullOrEmpty(targetNameContains)
                ? "airtarget"
                : targetNameContains.ToLowerInvariant();

            GameObject[] all = FindObjectsOfType<GameObject>();
            Transform best = null;
            float bestDist = float.MaxValue;

            foreach (GameObject go in all)
            {
                if (go == null) continue;
                if (!go.name.ToLowerInvariant().Contains(nameNeedle)) continue;

                float d = Vector3.Distance(transform.position, go.transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = go.transform;
                }
            }

            target = best;
        }

        private void Resolve()
        {
            if (jet == null) jet = GetComponent<MavMouseFlightJet>();
            if (instructor == null && jet != null) instructor = jet.instructor;
            if (instructor == null) instructor = GetComponent<MavInstructorController>();
            if (rig == null) rig = FindObjectOfType<MavMouseFlightRig>();
            if (rb == null) rb = GetComponent<Rigidbody>();
        }

        private void RunSimpleAI()
        {
            if (target == null)
            {
                aiState = "no_target";
                return;
            }

            Vector3 targetVelocity = Vector3.zero;
            MavAITargetDrone targetDrone = target.GetComponent<MavAITargetDrone>();
            Rigidbody trb = target.GetComponent<Rigidbody>();
            if (targetDrone != null) targetVelocity = targetDrone.currentVelocity;
            else if (trb != null) targetVelocity = trb.linearVelocity;

            Vector3 predicted = target.position + targetVelocity * leadAmount;
            Vector3 toTarget = predicted - transform.position;

            distanceToTarget = toTarget.magnitude;
            angleToTarget = Vector3.Angle(transform.forward, toTarget);

            if (toTarget.sqrMagnitude < 1f)
                desiredAimDirection = transform.forward;
            else
                desiredAimDirection = toTarget.normalized;

            if (transform.position.y < altitudeFloor)
            {
                desiredAimDirection = Vector3.Slerp(desiredAimDirection, Vector3.up, altitudeRecoveryPitchBias).normalized;
                aiState = "altitude_recovery";
            }
            else if (distanceToTarget > desiredDistance)
            {
                aiState = "intercept";
            }
            else if (distanceToTarget < attackDistance)
            {
                aiState = "close_manage";
            }
            else
            {
                aiState = "track";
            }

            float t = 1f - Mathf.Exp(-targetAimSmooth * Time.deltaTime);
            rig.mouseAim.position = transform.position;
            rig.mouseAim.forward = Vector3.Slerp(rig.mouseAim.forward, desiredAimDirection, t);

            float distT = Mathf.InverseLerp(attackDistance, desiredDistance * 2.0f, distanceToTarget);
            float targetThrottle = Mathf.Lerp(closeDistanceThrottle, farDistanceThrottle, distT);
            float currentThrottle = instructor != null ? instructor.throttleIntent : jet.throttle;
            float nextThrottle = Mathf.MoveTowards(currentThrottle, Mathf.Clamp(targetThrottle, minThrottle, maxThrottle), Time.deltaTime * 0.75f);

            if (instructor != null)
                instructor.SetThrottleIntent(nextThrottle);

            jet.throttle = nextThrottle;
        }
    }
}
