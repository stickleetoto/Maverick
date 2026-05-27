using UnityEngine;
using EaglePhysicalAI.Aircraft;
using EaglePhysicalAI.PhysicalAI;

namespace EaglePhysicalAI.Safety
{
    public enum FlightSafetyState
    {
        Nominal,
        Caution,
        Warning,
        Recovery
    }

    /// <summary>
    /// Game-level safety supervisor for physical AI experiments.
    /// It watches stall/overspeed/low-altitude/unusual attitude and can optionally override
    /// AI outputs with a simple recovery command. This is not a real flight-control system.
    /// </summary>
    [RequireComponent(typeof(AircraftPhysicsController))]
    public class FlightEnvelopeSafetyGuard : MonoBehaviour
    {
        [Header("References")]
        public AircraftPhysicsController aircraft;
        public PhysicalAIRuntimeAgent runtimeAgent;

        [Header("Envelope Limits")]
        public float hardDeckAltitude = 70f;
        public float cautionAltitude = 160f;
        public float maxSafeSpeed = 430f;
        public float cautionStallRisk = 0.55f;
        public float recoveryStallRisk = 0.82f;
        public float maxBankAngleForRecovery = 135f;
        public float maxPitchForRecovery = 75f;

        [Header("Recovery")]
        public bool enableAutoRecovery = true;
        public bool onlyOverrideAutonomousModes = true;
        public float recoveryThrottle = 1f;
        public float recoveryPitchDownWhenStalling = -0.22f;
        public float recoveryPitchUpNearGround = 0.55f;
        public float rollLevelGain = 0.65f;
        public float yawDampingGain = 0.15f;

        [Header("Debug")]
        public FlightSafetyState state = FlightSafetyState.Nominal;
        public string lastReason = "nominal";
        public PhysicalAIAction suggestedRecoveryAction = new PhysicalAIAction();
        public bool overrideActive;

        private void Awake()
        {
            if (aircraft == null) aircraft = GetComponent<AircraftPhysicsController>();
            if (runtimeAgent == null) runtimeAgent = GetComponent<PhysicalAIRuntimeAgent>();
        }

        private void FixedUpdate()
        {
            Evaluate();
            BuildRecoveryAction();
            ApplyOverrideIfNeeded();
        }

        public void Evaluate()
        {
            overrideActive = false;
            if (aircraft == null)
            {
                state = FlightSafetyState.Warning;
                lastReason = "no_aircraft";
                return;
            }

            float pitch = Mathf.DeltaAngle(0f, transform.eulerAngles.x);
            float bank = Mathf.DeltaAngle(0f, transform.eulerAngles.z);

            if (aircraft.IsCrashed)
            {
                state = FlightSafetyState.Recovery;
                lastReason = "crashed";
                return;
            }

            if (aircraft.Altitude < hardDeckAltitude)
            {
                state = FlightSafetyState.Recovery;
                lastReason = "hard_deck";
                return;
            }

            if (aircraft.StallRisk >= recoveryStallRisk || aircraft.IsStalling)
            {
                state = FlightSafetyState.Recovery;
                lastReason = "stall_recovery";
                return;
            }

            if (Mathf.Abs(bank) > maxBankAngleForRecovery || Mathf.Abs(pitch) > maxPitchForRecovery)
            {
                state = FlightSafetyState.Recovery;
                lastReason = "unusual_attitude";
                return;
            }

            if (aircraft.Speed > maxSafeSpeed)
            {
                state = FlightSafetyState.Warning;
                lastReason = "overspeed";
                return;
            }

            if (aircraft.Altitude < cautionAltitude || aircraft.StallRisk > cautionStallRisk)
            {
                state = FlightSafetyState.Caution;
                lastReason = aircraft.Altitude < cautionAltitude ? "low_altitude_caution" : "stall_caution";
                return;
            }

            state = FlightSafetyState.Nominal;
            lastReason = "nominal";
        }

        public PhysicalAIAction BuildRecoveryAction()
        {
            if (aircraft == null)
            {
                suggestedRecoveryAction = new PhysicalAIAction();
                return suggestedRecoveryAction;
            }

            float bank = Mathf.DeltaAngle(0f, transform.eulerAngles.z);
            float pitch = Mathf.DeltaAngle(0f, transform.eulerAngles.x);
            float pitchCommand = 0f;

            if (aircraft.Altitude < cautionAltitude)
            {
                pitchCommand = recoveryPitchUpNearGround;
            }
            else if (aircraft.StallRisk >= cautionStallRisk || aircraft.IsStalling)
            {
                pitchCommand = recoveryPitchDownWhenStalling;
            }
            else if (Mathf.Abs(pitch) > maxPitchForRecovery * 0.65f)
            {
                pitchCommand = -Mathf.Sign(pitch) * 0.35f;
            }

            float rollCommand = Mathf.Clamp(bank / 90f * rollLevelGain, -1f, 1f);
            float yawCommand = 0f;
            if (aircraft.rb != null)
            {
                yawCommand = Mathf.Clamp(-Vector3.Dot(aircraft.rb.angularVelocity, transform.up) * yawDampingGain, -0.35f, 0.35f);
            }

            suggestedRecoveryAction = new PhysicalAIAction
            {
                pitch = pitchCommand,
                roll = rollCommand,
                yaw = yawCommand,
                throttle = recoveryThrottle,
                strike = 0f,
                abort = 1f
            }.Clamp();
            return suggestedRecoveryAction;
        }

        private void ApplyOverrideIfNeeded()
        {
            if (!enableAutoRecovery || aircraft == null) return;
            if (state != FlightSafetyState.Recovery) return;

            bool canOverride = true;
            if (onlyOverrideAutonomousModes && runtimeAgent != null)
            {
                canOverride = runtimeAgent.controlMode != PhysicalAIControlMode.Manual;
            }

            if (!canOverride) return;
            aircraft.SetControlInputs(
                suggestedRecoveryAction.pitch,
                suggestedRecoveryAction.roll,
                suggestedRecoveryAction.yaw,
                suggestedRecoveryAction.throttle);
            overrideActive = true;
        }
    }
}
