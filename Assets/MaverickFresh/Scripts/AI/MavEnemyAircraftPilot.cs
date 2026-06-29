using UnityEngine;

namespace MaverickFresh
{
    /// <summary>
    /// Enemy aircraft control adapter.
    /// The physics are still the same MavMouseFlightJet/Aero stack as the player.
    /// v0.21.1 adds an optional LBM tactical brain above this adapter. The brain chooses
    /// intent; this pilot converts intent into pitch/yaw/roll/throttle commands.
    /// </summary>
    [DisallowMultipleComponent]
    public class MavEnemyAircraftPilot : MonoBehaviour
    {
        [Header("References")]
        public MavMouseFlightJet jet;
        public Rigidbody rb;
        public Transform target;
        public Rigidbody targetRb;
        public MavEnemyLBMBrain lbmBrain;

        [Header("LBM Tactical Layer")]
        public bool useLBMBrain = true;
        public bool autoInstallLBMBrain = true;
        public bool lbmOverridesCloseBreak = true;
        public bool showLBMStateInAiState = true;

        [Header("Targeting")]
        public bool autoFindPlayer = true;
        public string playerObjectName = MavPlayerResolver.DefaultPlayerName;
        public float leadTime = 0.55f;
        public float desiredDistance = 950f;
        public float closeDistance = 420f;
        public float disengageDistance = 180f;
        public float verticalLeadBias = 0.12f;

        [Header("Control Gains")]
        public float pitchGain = 1.15f;
        public float yawGain = 0.42f;
        public float rollGain = 1.35f;
        public float bankTowardTarget = 0.85f;
        public float levelWhenCentered = 0.28f;
        public float commandSlew = 2.6f;
        public float maxPitchCommand = 0.82f;
        public float maxYawCommand = 0.32f;
        public float maxRollCommand = 0.95f;

        [Header("Throttle")]
        public float minThrottlePercent = 58f;
        public float cruiseThrottlePercent = 86f;
        public float attackThrottlePercent = 102f;
        public float afterburnerThrottlePercent = 118f;
        public float throttleSlewPercent = 45f;

        [Header("Safety")]
        public float altitudeFloor = 170f;
        public float recoveryAltitude = 360f;
        public float maxNoseHighPitchDeg = 52f;
        public float maxDivePitchDeg = -65f;
        public float overspeed = 520f;
        public bool keepAlive = true;

        [Header("Runtime")]
        public string aiState = "init";
        public string tacticalState = "none";
        public float distanceToTarget;
        public float angleToTarget;
        public Vector3 desiredAimDirection;
        public Vector3 smoothedCommand;
        public float throttlePercent;
        public bool usingLBMDirective;

        private void Awake()
        {
            Resolve();
        }

        private void OnEnable()
        {
            Resolve();
        }

        private void Update()
        {
            Resolve();

            if (target == null && autoFindPlayer)
                FindPlayerTarget();

            if (jet == null || rb == null || target == null)
            {
                aiState = target == null ? "no_target" : "missing_components";
                return;
            }

            RunPilot();
        }

        [ContextMenu("Find Player Target")]
        public void FindPlayerTarget()
        {
            GameObject player = null;
            if (!string.IsNullOrEmpty(playerObjectName))
                player = GameObject.Find(playerObjectName);
            if (player == null)
                player = MavPlayerResolver.FindPlayerObject();

            if (player != null && player.transform != transform)
            {
                target = player.transform;
                targetRb = player.GetComponent<Rigidbody>();
            }
        }

        private void Resolve()
        {
            if (jet == null) jet = GetComponent<MavMouseFlightJet>();
            if (rb == null) rb = GetComponent<Rigidbody>();
            if (target != null && targetRb == null) targetRb = target.GetComponent<Rigidbody>();

            if (lbmBrain == null && useLBMBrain)
                lbmBrain = GetComponent<MavEnemyLBMBrain>();
            if (lbmBrain == null && useLBMBrain && autoInstallLBMBrain)
                lbmBrain = gameObject.AddComponent<MavEnemyLBMBrain>();
            if (lbmBrain != null)
            {
                lbmBrain.jet = jet;
                lbmBrain.rb = rb;
                lbmBrain.target = target;
                lbmBrain.targetRb = targetRb;
            }
        }

        private void RunPilot()
        {
            Vector3 targetVelocity = targetRb != null ? targetRb.linearVelocity : Vector3.zero;
            Vector3 ownVelocity = rb != null ? rb.linearVelocity : Vector3.zero;
            Vector3 predicted = target.position + targetVelocity * leadTime;

            Vector3 toTarget = predicted - transform.position;
            distanceToTarget = toTarget.magnitude;
            angleToTarget = toTarget.sqrMagnitude > 1f ? Vector3.Angle(transform.forward, toTarget.normalized) : 0f;

            if (toTarget.sqrMagnitude < 1f)
                desiredAimDirection = transform.forward;
            else
                desiredAimDirection = toTarget.normalized;

            float targetThrottle = ComputeFallbackThrottle(ownVelocity.magnitude);
            usingLBMDirective = false;

            if (useLBMBrain && lbmBrain != null && lbmBrain.enabled && lbmBrain.Evaluate(target, targetRb))
            {
                desiredAimDirection = lbmBrain.desiredAimDirection.sqrMagnitude > 0.01f
                    ? lbmBrain.desiredAimDirection.normalized
                    : desiredAimDirection;
                targetThrottle = lbmBrain.desiredThrottlePercent;
                desiredDistance = lbmBrain.desiredDistance;
                leadTime = lbmBrain.desiredLeadTime;
                tacticalState = lbmBrain.state.ToString();
                aiState = showLBMStateInAiState ? ("lbm_" + lbmBrain.state + " / " + lbmBrain.rationale) : "lbm";
                usingLBMDirective = true;
            }

            if (!usingLBMDirective || !lbmOverridesCloseBreak)
            {
                ApplyLegacyMergeBreak(ref desiredAimDirection);
                if (!usingLBMDirective)
                    aiState = distanceToTarget < closeDistance ? "merge_break" : (distanceToTarget > desiredDistance * 1.6f ? "intercept" : "pursuit");
            }

            ApplySafetyOverrides(ref desiredAimDirection, ref targetThrottle, ownVelocity.magnitude);
            ApplyControlFromAim(desiredAimDirection, targetThrottle);
        }

        private float ComputeFallbackThrottle(float ownSpeed)
        {
            float distT = Mathf.InverseLerp(closeDistance, desiredDistance * 1.7f, distanceToTarget);
            float targetThrottle = Mathf.Lerp(minThrottlePercent, attackThrottlePercent, distT);
            if (distanceToTarget > desiredDistance * 1.5f)
                targetThrottle = afterburnerThrottlePercent;
            if (ownSpeed > overspeed)
                targetThrottle = Mathf.Min(targetThrottle, cruiseThrottlePercent);
            if (transform.position.y < altitudeFloor)
                targetThrottle = afterburnerThrottlePercent;
            return targetThrottle;
        }

        private void ApplyLegacyMergeBreak(ref Vector3 aimDir)
        {
            if (distanceToTarget >= closeDistance)
                return;

            Vector3 breakDir = Vector3.Cross(Vector3.up, (target.position - transform.position).normalized);
            if (breakDir.sqrMagnitude < 0.1f)
                breakDir = transform.right;
            float breakT = 1f - Mathf.InverseLerp(disengageDistance, closeDistance, distanceToTarget);
            aimDir = Vector3.Slerp(aimDir, breakDir.normalized, Mathf.Clamp01(breakT)).normalized;
        }

        private void ApplySafetyOverrides(ref Vector3 aimDir, ref float targetThrottle, float ownSpeed)
        {
            float pitchDeg = NormalizeAngle(transform.eulerAngles.x);
            if (transform.position.y < altitudeFloor || pitchDeg < maxDivePitchDeg)
            {
                float t = Mathf.InverseLerp(recoveryAltitude, altitudeFloor, transform.position.y);
                aimDir = Vector3.Slerp(aimDir, Vector3.up, Mathf.Clamp01(0.45f + t * 0.45f)).normalized;
                targetThrottle = afterburnerThrottlePercent;
                aiState = "altitude_recovery";
            }
            else if (pitchDeg > maxNoseHighPitchDeg && ownSpeed < 220f)
            {
                Vector3 horizon = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                if (horizon.sqrMagnitude < 0.1f)
                    horizon = transform.forward;
                aimDir = Vector3.Slerp(aimDir, horizon.normalized, 0.38f).normalized;
                aiState = "nose_high_recovery";
            }
        }

        private void ApplyControlFromAim(Vector3 aimDir, float targetThrottle)
        {
            Vector3 local = transform.InverseTransformDirection(aimDir);
            if (local.sqrMagnitude < 0.0001f)
                local = Vector3.forward;
            local.Normalize();

            // Sign convention mirrors MavInstructorController: local up target usually maps to negative pitch command.
            float pitchCmd = -Mathf.Clamp(local.y * pitchGain + verticalLeadBias * Mathf.Sign(local.y), -maxPitchCommand, maxPitchCommand);
            float yawCmd = Mathf.Clamp(local.x * yawGain, -maxYawCommand, maxYawCommand);
            float rollTowardTarget = Mathf.Clamp(local.x * rollGain, -maxRollCommand, maxRollCommand);
            float levelSignal = Mathf.Clamp(transform.right.y * levelWhenCentered, -0.35f, 0.35f);
            float centerT = 1f - Mathf.Clamp01(Mathf.Abs(local.x) / 0.32f);
            float rollCmd = Mathf.Clamp(rollTowardTarget * bankTowardTarget + levelSignal * centerT, -maxRollCommand, maxRollCommand);

            Vector3 targetCmd = new Vector3(pitchCmd, yawCmd, rollCmd);
            smoothedCommand = Vector3.MoveTowards(smoothedCommand, targetCmd, commandSlew * Time.deltaTime);

            throttlePercent = Mathf.MoveTowards(throttlePercent <= 0f ? cruiseThrottlePercent : throttlePercent, targetThrottle, throttleSlewPercent * Time.deltaTime);
            jet.SetThrottlePercent(throttlePercent);
            jet.SetControlCommand(smoothedCommand.x, smoothedCommand.y, smoothedCommand.z, jet.throttle, true, true, true);
        }

        private static float NormalizeAngle(float angle)
        {
            while (angle > 180f) angle -= 360f;
            while (angle < -180f) angle += 360f;
            return angle;
        }
    }
}
