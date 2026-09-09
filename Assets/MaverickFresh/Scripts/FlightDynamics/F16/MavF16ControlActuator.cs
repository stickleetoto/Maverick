using UnityEngine;

namespace MaverickFresh.FlightDynamics.F16
{
    /// <summary>
    /// Physical control-surface state owner for the isolated F-16 flight-dynamics path.
    ///
    /// This component converts commanded deflections into bounded actual surface states,
    /// then publishes those states to MavSixDoFBody. It deliberately does not apply any
    /// Rigidbody torque itself.
    ///
    /// Morelli provides the aerodynamic-model deflection envelope. Actuator rate limits
    /// are optional and disabled by default until a permitted source is selected; a value
    /// <= 0 means instantaneous motion to the bounded command.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public class MavF16ControlActuator : MonoBehaviour
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
            Resolve();
            Step(Time.fixedDeltaTime);
            if (sixDoFBody != null)
                sixDoFBody.SetControlInput(actual);
        }

        public void SetCommand(MavControlInput input)
        {
            command = input;
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
            actual.elevatorDeg = MoveSurface(
                actual.elevatorDeg,
                bounded.elevatorDeg,
                elevatorRateLimitDegSec,
                dt
            );
            actual.aileronDeg = MoveSurface(
                actual.aileronDeg,
                bounded.aileronDeg,
                aileronRateLimitDegSec,
                dt
            );
            actual.rudderDeg = MoveSurface(
                actual.rudderDeg,
                bounded.rudderDeg,
                rudderRateLimitDegSec,
                dt
            );
            actual.leadingEdgeFlapDeg = MoveSurface(
                actual.leadingEdgeFlapDeg,
                bounded.leadingEdgeFlapDeg,
                leadingEdgeFlapRateLimitDegSec,
                dt
            );
        }

        private static MavControlInput BoundCommand(MavControlInput source, out bool wasClamped)
        {
            MavControlInput bounded = source;
            bounded.throttle01 = Mathf.Clamp01(source.throttle01);
            bounded.elevatorDeg = Mathf.Clamp(
                source.elevatorDeg,
                MavF16MorelliReference.ElevatorMinDeg,
                MavF16MorelliReference.ElevatorMaxDeg
            );
            bounded.aileronDeg = Mathf.Clamp(
                source.aileronDeg,
                MavF16MorelliReference.AileronMinDeg,
                MavF16MorelliReference.AileronMaxDeg
            );
            bounded.rudderDeg = Mathf.Clamp(
                source.rudderDeg,
                MavF16MorelliReference.RudderMinDeg,
                MavF16MorelliReference.RudderMaxDeg
            );

            // The compact Morelli model currently ignores leading-edge-flap input.
            // Preserve the command for future model extensions but do not invent a
            // published aerodynamic envelope here.
            bounded.leadingEdgeFlapDeg = source.leadingEdgeFlapDeg;

            wasClamped =
                !Mathf.Approximately(bounded.throttle01, source.throttle01)
                || !Mathf.Approximately(bounded.elevatorDeg, source.elevatorDeg)
                || !Mathf.Approximately(bounded.aileronDeg, source.aileronDeg)
                || !Mathf.Approximately(bounded.rudderDeg, source.rudderDeg);

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
