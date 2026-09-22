using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// Physical surface-state owner for the F-15 reference path.
    ///
    /// The shape intentionally mirrors MavF16ControlActuator so both aircraft use the same
    /// law -> actuator -> aero -> SixDoF ownership pipeline. No Rigidbody torque is applied here.
    ///
    /// Unlike the F-16 reference, exact NASA 836 hard-stop and actuator-rate authority is not yet
    /// frozen. Therefore this actuator refuses all surface motion unless a valid active profile
    /// supplies physical limits. The fallback is zero deflection, not a borrowed preproduction value.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class MavF15ControlActuator : MavControlSurfaceActuatorBase
    {
        [Header("Target")]
        public MavSixDoFBody sixDoFBody;

        [Header("Commanded Surface Deflection")]
        public MavControlInput command;

        [Header("Optional Rate Limits (deg/s, <= 0 = unlimited)")]
        [Min(0f)] public float elevatorRateLimitDegSec;
        [Min(0f)] public float aileronRateLimitDegSec;
        [Min(0f)] public float rudderRateLimitDegSec;
        [Min(0f)] public float leadingEdgeFlapRateLimitDegSec;

        [Header("Debug / Actual Surface State")]
        public MavControlInput actual;
        public bool debugCommandClamped;
        public bool debugUsingPhysicalProfileLimits;
        public bool debugRefusedWithoutValidProfile;

        private void Awake()
        {
            Resolve();
            SnapToBoundedCommand();
        }

        private void OnEnable()
        {
            Resolve();
        }

        private void FixedUpdate()
        {
            StepActuator(Time.fixedDeltaTime);
        }

        public override MavControlInput ActualSurfaceState
        {
            get { return actual; }
        }

        public override MavSixDoFBody BoundBody
        {
            get { return sixDoFBody; }
        }

        public override void SetCommand(MavControlInput input)
        {
            command = input;
        }

        public void StepActuator(float deltaTime)
        {
            Resolve();
            Step(deltaTime);

            if (sixDoFBody != null)
                sixDoFBody.SetControlInput(actual);
        }

        public void SnapToBoundedCommand()
        {
            MavControlInput bounded = BoundCommand(command, out debugCommandClamped);
            actual = bounded;

            if (sixDoFBody != null)
                sixDoFBody.SetControlInput(actual);
        }

        public void Step(float deltaTime)
        {
            MavControlInput bounded = BoundCommand(command, out debugCommandClamped);
            float dt = Mathf.Max(0f, deltaTime);

            actual.throttle01 = bounded.throttle01;
            actual.elevatorDeg =
                MoveSurface(actual.elevatorDeg, bounded.elevatorDeg, elevatorRateLimitDegSec, dt);
            actual.aileronDeg =
                MoveSurface(actual.aileronDeg, bounded.aileronDeg, aileronRateLimitDegSec, dt);
            actual.rudderDeg =
                MoveSurface(actual.rudderDeg, bounded.rudderDeg, rudderRateLimitDegSec, dt);
            actual.leadingEdgeFlapDeg =
                MoveSurface(
                    actual.leadingEdgeFlapDeg,
                    bounded.leadingEdgeFlapDeg,
                    leadingEdgeFlapRateLimitDegSec,
                    dt
                );
        }

        private MavControlInput BoundCommand(MavControlInput source, out bool wasClamped)
        {
            debugUsingPhysicalProfileLimits =
                sixDoFBody != null
                && sixDoFBody.activeProfile != null
                && sixDoFBody.debugProfileValid;

            MavControlInput bounded;
            if (debugUsingPhysicalProfileLimits)
            {
                bounded = sixDoFBody.activeProfile.controlSurfaceLimits.Clamp(source);
                debugRefusedWithoutValidProfile = false;
            }
            else
            {
                // Exact-target travel/sign authority is not frozen yet. Preserve throttle plumbing
                // but refuse aerodynamic surface motion rather than borrow F-16 or preproduction data.
                bounded = new MavControlInput();
                bounded.throttle01 = Mathf.Clamp01(source.throttle01);
                debugRefusedWithoutValidProfile = true;
            }

            wasClamped =
                !Mathf.Approximately(bounded.throttle01, source.throttle01)
                || !Mathf.Approximately(bounded.elevatorDeg, source.elevatorDeg)
                || !Mathf.Approximately(bounded.aileronDeg, source.aileronDeg)
                || !Mathf.Approximately(bounded.rudderDeg, source.rudderDeg)
                || !Mathf.Approximately(bounded.leadingEdgeFlapDeg, source.leadingEdgeFlapDeg);

            return bounded;
        }

        private static float MoveSurface(float current, float target, float rateDegSec, float dt)
        {
            if (rateDegSec <= 0f || dt <= 0f)
                return target;

            return Mathf.MoveTowards(current, target, rateDegSec * dt);
        }

        private void Resolve()
        {
            if (sixDoFBody == null)
                sixDoFBody = GetComponent<MavSixDoFBody>();
        }
    }
}
