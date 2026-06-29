using UnityEngine;

namespace MaverickFresh
{
    /// <summary>
    /// v0.21.1 Lightweight/Layered Behavior Model for the enemy F-15.
    /// This is a game AI tactical layer, not a separate flight physics model.
    /// It reads the same Rigidbody/aero state used by the player and chooses a BFM-style
    /// intent: intercept, pursue, extend, recover, or defensive break. MavEnemyAircraftPilot
    /// then converts that intent into the same SetControlCommand path used by the physics stack.
    /// </summary>
    [DisallowMultipleComponent]
    public class MavEnemyLBMBrain : MonoBehaviour
    {
        public enum LBMState
        {
            Init,
            Intercept,
            LeadPursuit,
            LagPursuit,
            OneCircle,
            TwoCircle,
            HighYoYo,
            LowYoYo,
            Extend,
            Reengage,
            DefensiveBreak,
            EnergyRecovery,
            AltitudeRecovery
        }

        [Header("References")]
        public MavMouseFlightJet jet;
        public MavAeroBody aero;
        public MavAtmosphericEngine engine;
        public Rigidbody rb;
        public Transform target;
        public Rigidbody targetRb;

        [Header("LBM Tuning")]
        public bool useLBM = true;
        public float decisionInterval = 0.16f;
        public float targetUpdateLeadMin = 0.18f;
        public float targetUpdateLeadMax = 1.15f;
        public float preferredFightDistance = 950f;
        public float gunRange = 620f;
        public float mergeRange = 360f;
        public float extendRange = 1700f;
        public float defensiveAngleDeg = 120f;
        public float noseOnAngleDeg = 18f;
        public float lowEnergySpeed = 185f;
        public float goodEnergySpeed = 310f;
        public float lowAltitude = 260f;
        public float safeAltitude = 520f;
        public float verticalManeuverAltitude = 900f;

        [Header("Behavior Weights")]
        [Range(0f, 1f)] public float leadPursuitWeight = 0.72f;
        [Range(0f, 1f)] public float lagPursuitWeight = 0.35f;
        [Range(0f, 1f)] public float verticalManeuverWeight = 0.45f;
        [Range(0f, 1f)] public float extensionDiscipline = 0.58f;
        [Range(0f, 1f)] public float defensiveBreakStrength = 0.82f;
        [Range(0f, 1f)] public float smoothing = 0.38f;

        [Header("Throttle Policy")]
        public float minThrottlePercent = 62f;
        public float cruiseThrottlePercent = 88f;
        public float attackThrottlePercent = 108f;
        public float extendThrottlePercent = 122f;
        public float recoveryThrottlePercent = 118f;

        [Header("Runtime Blackboard")]
        public LBMState state = LBMState.Init;
        public string rationale = "init";
        public bool hasDirective;
        public Vector3 desiredAimDirection = Vector3.forward;
        public Vector3 desiredAimPoint;
        public float desiredThrottlePercent = 88f;
        public float desiredLeadTime = 0.55f;
        public float desiredDistance = 950f;
        public float distance;
        public float aspectToTargetDeg;
        public float targetAspectDeg;
        public float closureRate;
        public float ownSpeed;
        public float targetSpeed;
        public float energyScore;
        public float nextDecisionTime;

        private Vector3 lastAimDirection;

        private void Awake()
        {
            Resolve();
            lastAimDirection = transform.forward;
        }

        private void OnEnable()
        {
            Resolve();
        }

        public void Resolve()
        {
            if (jet == null) jet = GetComponent<MavMouseFlightJet>();
            if (aero == null) aero = GetComponent<MavAeroBody>();
            if (engine == null) engine = GetComponent<MavAtmosphericEngine>();
            if (rb == null) rb = GetComponent<Rigidbody>();
            if (target != null && targetRb == null) targetRb = target.GetComponent<Rigidbody>();
        }

        /// <summary>
        /// Evaluate the current tactical intent. Returns false when the brain is disabled or has no target.
        /// MavEnemyAircraftPilot calls this so the AI state is deterministic and does not depend on Update order.
        /// </summary>
        public bool Evaluate(Transform currentTarget, Rigidbody currentTargetRb)
        {
            if (!useLBM)
                return false;

            Resolve();
            if (currentTarget != null)
            {
                target = currentTarget;
                targetRb = currentTargetRb != null ? currentTargetRb : currentTarget.GetComponent<Rigidbody>();
            }

            if (rb == null || target == null)
            {
                hasDirective = false;
                state = LBMState.Init;
                rationale = "no_target_or_rb";
                return false;
            }

            if (Time.time < nextDecisionTime && hasDirective)
                return true;

            nextDecisionTime = Time.time + Mathf.Max(0.02f, decisionInterval);
            BuildBlackboard();
            ChooseState();
            BuildDirective();
            return hasDirective;
        }

        private void BuildBlackboard()
        {
            Vector3 ownVel = rb != null ? rb.linearVelocity : Vector3.zero;
            Vector3 tgtVel = targetRb != null ? targetRb.linearVelocity : Vector3.zero;
            Vector3 toTarget = target.position - transform.position;
            distance = toTarget.magnitude;
            ownSpeed = ownVel.magnitude;
            targetSpeed = tgtVel.magnitude;

            Vector3 los = toTarget.sqrMagnitude > 1f ? toTarget.normalized : transform.forward;
            aspectToTargetDeg = Vector3.Angle(transform.forward, los);
            targetAspectDeg = Vector3.Angle(target.forward, -los);
            closureRate = Vector3.Dot(tgtVel - ownVel, los);

            float speedScore = Mathf.InverseLerp(lowEnergySpeed, goodEnergySpeed, ownSpeed);
            float altitudeScore = Mathf.InverseLerp(lowAltitude, verticalManeuverAltitude, transform.position.y);
            float stallPenalty = aero != null ? Mathf.Clamp01(aero.debugStallFactor) : 0f;
            energyScore = Mathf.Clamp01(speedScore * 0.72f + altitudeScore * 0.28f - stallPenalty * 0.35f);
        }

        private void ChooseState()
        {
            if (transform.position.y < lowAltitude)
            {
                state = LBMState.AltitudeRecovery;
                rationale = "low_altitude";
                return;
            }

            if (ownSpeed < lowEnergySpeed || (aero != null && aero.debugStallFactor > 0.38f))
            {
                state = LBMState.EnergyRecovery;
                rationale = "low_energy_or_stall";
                return;
            }

            // If the target is behind us and close, treat it as a defensive break problem.
            if (targetAspectDeg < 55f && aspectToTargetDeg > defensiveAngleDeg && distance < preferredFightDistance * 1.35f)
            {
                state = LBMState.DefensiveBreak;
                rationale = "target_on_six";
                return;
            }

            if (distance > extendRange)
            {
                state = LBMState.Intercept;
                rationale = "far_intercept";
                return;
            }

            if (distance < mergeRange)
            {
                state = energyScore > 0.62f ? LBMState.TwoCircle : LBMState.OneCircle;
                rationale = energyScore > 0.62f ? "merge_two_circle" : "merge_one_circle";
                return;
            }

            if (aspectToTargetDeg < noseOnAngleDeg && distance < gunRange)
            {
                state = LBMState.LeadPursuit;
                rationale = "nose_on_attack";
                return;
            }

            if (energyScore > 0.68f && transform.position.y > verticalManeuverAltitude && distance < preferredFightDistance * 1.25f)
            {
                state = closureRate < -80f ? LBMState.HighYoYo : LBMState.LeadPursuit;
                rationale = closureRate < -80f ? "high_yoyo_manage_closure" : "energy_lead";
                return;
            }

            if (distance < preferredFightDistance * 0.65f && closureRate < -120f)
            {
                state = LBMState.Extend;
                rationale = "too_close_high_closure";
                return;
            }

            state = aspectToTargetDeg > 55f ? LBMState.LagPursuit : LBMState.LeadPursuit;
            rationale = aspectToTargetDeg > 55f ? "lag_to_control_overshoot" : "lead_attack";
        }

        private void BuildDirective()
        {
            Vector3 ownVel = rb != null ? rb.linearVelocity : Vector3.zero;
            Vector3 tgtVel = targetRb != null ? targetRb.linearVelocity : Vector3.zero;
            Vector3 toTarget = target.position - transform.position;
            Vector3 los = toTarget.sqrMagnitude > 1f ? toTarget.normalized : transform.forward;

            float leadT = Mathf.Lerp(targetUpdateLeadMin, targetUpdateLeadMax, Mathf.InverseLerp(250f, 1800f, distance));
            Vector3 predicted = target.position + tgtVel * leadT;
            Vector3 aim = predicted;
            float throttle = attackThrottlePercent;
            float desiredDist = preferredFightDistance;

            Vector3 horizontalRight = Vector3.Cross(Vector3.up, los);
            if (horizontalRight.sqrMagnitude < 0.1f)
                horizontalRight = transform.right;
            horizontalRight.Normalize();

            switch (state)
            {
                case LBMState.AltitudeRecovery:
                    aim = transform.position + Vector3.Slerp(transform.forward, Vector3.up, 0.72f) * 1800f;
                    throttle = recoveryThrottlePercent;
                    desiredDist = preferredFightDistance * 1.2f;
                    break;

                case LBMState.EnergyRecovery:
                    aim = transform.position + Vector3.Slerp(Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized, Vector3.up, 0.18f) * 2200f;
                    throttle = recoveryThrottlePercent;
                    desiredDist = preferredFightDistance * 1.4f;
                    break;

                case LBMState.DefensiveBreak:
                    {
                        float side = Mathf.Sign(Vector3.Dot(target.right, transform.position - target.position));
                        if (Mathf.Abs(side) < 0.1f) side = 1f;
                        Vector3 breakDir = (horizontalRight * side * defensiveBreakStrength + Vector3.up * 0.22f + transform.forward * 0.25f).normalized;
                        aim = transform.position + breakDir * 1800f;
                        throttle = extendThrottlePercent;
                        desiredDist = preferredFightDistance * 1.25f;
                    }
                    break;

                case LBMState.Extend:
                    aim = transform.position + (transform.forward * 0.72f + horizontalRight * 0.42f + Vector3.up * 0.12f).normalized * 2300f;
                    throttle = extendThrottlePercent;
                    desiredDist = extendRange;
                    break;

                case LBMState.OneCircle:
                    aim = target.position + horizontalRight * (mergeRange * 0.75f) + Vector3.up * 120f;
                    throttle = Mathf.Lerp(cruiseThrottlePercent, attackThrottlePercent, energyScore);
                    desiredDist = mergeRange;
                    break;

                case LBMState.TwoCircle:
                    aim = target.position + tgtVel.normalized * (mergeRange * 0.85f) + Vector3.up * 90f;
                    throttle = attackThrottlePercent;
                    desiredDist = preferredFightDistance;
                    break;

                case LBMState.HighYoYo:
                    aim = Vector3.Lerp(predicted, target.position + Vector3.up * 520f, verticalManeuverWeight);
                    throttle = Mathf.Lerp(cruiseThrottlePercent, attackThrottlePercent, 0.55f);
                    desiredDist = preferredFightDistance;
                    break;

                case LBMState.LowYoYo:
                    aim = Vector3.Lerp(predicted, target.position - Vector3.up * 240f, 0.35f);
                    throttle = attackThrottlePercent;
                    desiredDist = preferredFightDistance * 0.85f;
                    break;

                case LBMState.LagPursuit:
                    aim = Vector3.Lerp(predicted, target.position - tgtVel.normalized * 280f, lagPursuitWeight);
                    throttle = Mathf.Lerp(cruiseThrottlePercent, attackThrottlePercent, 0.68f);
                    desiredDist = preferredFightDistance * 0.9f;
                    break;

                case LBMState.Intercept:
                    aim = predicted + tgtVel.normalized * Mathf.Clamp(distance * 0.12f, 80f, 420f);
                    throttle = extendThrottlePercent;
                    desiredDist = preferredFightDistance;
                    break;

                case LBMState.LeadPursuit:
                case LBMState.Reengage:
                default:
                    aim = Vector3.Lerp(target.position, predicted, leadPursuitWeight);
                    throttle = attackThrottlePercent;
                    desiredDist = preferredFightDistance;
                    break;
            }

            // Keep the directive within a sane vertical envelope so the F-15 does not command pure vertical flips.
            Vector3 dir = aim - transform.position;
            if (dir.sqrMagnitude < 1f)
                dir = los;
            dir.Normalize();

            float maxVertical = state == LBMState.AltitudeRecovery ? 0.72f : 0.46f;
            dir.y = Mathf.Clamp(dir.y, -0.58f, maxVertical);
            dir.Normalize();

            if (lastAimDirection.sqrMagnitude < 0.1f)
                lastAimDirection = transform.forward;
            desiredAimDirection = Vector3.Slerp(lastAimDirection, dir, 1f - Mathf.Clamp01(smoothing));
            if (desiredAimDirection.sqrMagnitude < 0.1f)
                desiredAimDirection = dir;
            desiredAimDirection.Normalize();
            lastAimDirection = desiredAimDirection;

            desiredAimPoint = transform.position + desiredAimDirection * Mathf.Max(600f, distance);
            desiredThrottlePercent = Mathf.Clamp(throttle, minThrottlePercent, extendThrottlePercent);
            desiredLeadTime = leadT;
            desiredDistance = desiredDist;
            hasDirective = true;
        }
    }
}
