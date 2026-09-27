using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// The Maverick pilot-control law of the pilot-controlled F-15 research aircraft (V1).
    ///
    /// NAMING. This is a MAVERICK control law. It is NOT the F-15 CAS, NOT the F-15 FLCS and NOT a
    /// NASA 836 control law: no F-15 gain, schedule, gearing, stop or rate is sourced, and none is
    /// claimed. <see cref="MavF15ControlLaw"/> - the F-15 FCS architecture with every gain unavailable -
    /// is untouched and is not used here.
    ///
    /// WHAT IT DOES. Direct stick-to-surface gearing about the validated trim
    /// (<see cref="MavF15PilotControlMapping"/>): no feedback, no protection, no augmentation. With the
    /// stick centred it requests exactly the trim stabilator and zero lateral surfaces, so the research
    /// model's own dynamics act unaltered. Every gain and envelope is
    /// <see cref="MavF15PilotControlProvenance.MaverickTunedNonAuthoritative"/> and is read from the
    /// <see cref="MavF15PilotControlledFlightDynamicsProfile"/> on this GameObject.
    ///
    /// THROTTLE. Passed through to telemetry only: <see cref="MavF15PilotControlledFixedThrust.ThrottleStatus"/>.
    ///
    /// OWNERSHIP. It computes a REQUEST: nothing here touches the Rigidbody, computes a force or moment,
    /// or sets an actual surface position. <see cref="MavF15ControlActuator"/> owns the surfaces. Runs at
    /// execution order -300 (base class), ahead of the actuator (-200) and the body (-100).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MavF15PilotControlLaw : MavFlightControlLawBase
    {
        public const string LawName =
            "Maverick F-15 pilot-control approximation V1 (direct gearing about the validated trim; "
            + MavF15PilotControlProvenance.MaverickTunedNonAuthoritative + ") - NOT F-15 CAS / FLCS, NOT NASA 836";

        [Header("Configuration")]
        [Tooltip("Where the gearing, envelopes and trim bias come from. Resolved from this GameObject when empty.")]
        public MavF15PilotControlledFlightDynamicsProfile configuration;

        [Header("Debug / Requested Surface State")]
        public MavF15RequestedSurfaceState debugRequested;
        public MavF15SurfaceState debugUnbounded;
        public bool debugEnvelopeLimited;
        public float debugTrimStabilatorBiasDeg;

        [Tooltip("The pilot's throttle, shown and ignored: THROTTLE INACTIVE - FIXED RESEARCH THRUST.")]
        public float debugThrottleInactive01;

        public override string ControlLawName
        {
            get { return LawName; }
        }

        public MavF15RequestedSurfaceState RequestedF15SurfaceState
        {
            get { return debugRequested; }
        }

        protected override void FixedUpdate()
        {
            // The base pipeline resolves the command, evaluates, and publishes the three shared
            // channels; the differential stabilator has no shared field, so the full four-channel
            // request from the SAME evaluation is pushed straight after - nothing is recomputed.
            base.FixedUpdate();
            PushF15Request();
        }

        /// <summary>
        /// One law step with the timestep supplied - the base pipeline plus the four-channel push -
        /// so validation can drive the real pipeline without Unity's clock.
        /// </summary>
        public void StepPilotControlLaw(float deltaTime)
        {
            StepControlLaw(deltaTime);
            PushF15Request();
        }

        private void PushF15Request()
        {
            if (!driveActuatorInFixedUpdate || !debugDroveActuator)
                return;

            MavF15ControlActuator f15Actuator = actuator as MavF15ControlActuator;
            if (f15Actuator != null)
                f15Actuator.SetF15Command(debugRequested, debugLastOutput.throttle01);
        }

        public override MavControlInput Evaluate(
            MavFlightState state,
            MavAtmosphereSample atmosphere,
            MavPilotCommand command,
            MavFlightDynamicsProfile profile,
            float deltaTime)
        {
            ResolveConfiguration();

            MavF15GameplayControlAuthority authority = configuration != null
                ? configuration.gameplayControlAuthority
                : MavF15GameplayControlAuthority.V1();
            float bias = configuration != null
                ? (float)configuration.trimStart.symmetricStabilatorDeg
                : (float)MavF15PilotTrimStart.TableViiPoint36().symmetricStabilatorDeg;

            MavF15PilotControlSolution solution = MavF15PilotControlMapping.Solve(command, authority, bias);

            debugRequested = MavF15RequestedSurfaceState.From(solution.requested);
            debugUnbounded = solution.unbounded;
            debugEnvelopeLimited = solution.AnyLimited;
            debugTrimStabilatorBiasDeg = bias;
            debugThrottleInactive01 = solution.command.throttle01;

            return new MavControlInput
            {
                throttle01 = solution.command.throttle01,
                elevatorDeg = solution.requested.symmetricStabilatorDeg,
                aileronDeg = solution.requested.aileronDeg,
                rudderDeg = solution.requested.rudderDeg,
                leadingEdgeFlapDeg = 0f
            };
        }

        private void ResolveConfiguration()
        {
            if (configuration == null)
                configuration = GetComponent<MavF15PilotControlledFlightDynamicsProfile>();
        }
    }
}
