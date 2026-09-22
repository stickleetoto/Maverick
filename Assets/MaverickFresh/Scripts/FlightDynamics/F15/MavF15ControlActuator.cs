using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// Physical surface-state owner for the F-15 reference path.
    ///
    /// This is the ONLY owner of actual F-15 surface positions. A control law states what it
    /// wants; this decides what the aircraft actually did. Nothing downstream may reconstruct a
    /// surface position by other means, and nothing here touches the Rigidbody - surfaces reach
    /// the physics only as aerodynamic coefficients through MavSixDoFBody.
    ///
    /// TWO SURFACE SETS, DELIBERATELY
    /// ------------------------------
    /// The generic <see cref="MavControlInput"/> is still published, because MavSixDoFBody and the
    /// shared readiness/telemetry path speak that contract and it carries throttle. Alongside it
    /// the actuator owns a four-channel <see cref="MavF15SurfaceState"/>, which is the real
    /// physical state: the F-15 drives its stabilators symmetrically AND differentially, and the
    /// differential half has no generic field to live in.
    ///
    /// The generic elevator/aileron/rudder fields mirror the F-15 symmetric stabilator, aileron
    /// and rudder. Differential stabilator exists only in the F-15 set - it is not folded into
    /// the generic aileron field, because a consumer reading that field would then be reading two
    /// different physical surfaces added together.
    ///
    /// FAIL-CLOSED
    /// -----------
    /// Exact NASA 836 travel, sign authority and actuator dynamics are not frozen. Every channel
    /// therefore refuses to move until its authority is declared, with the provenance of that
    /// declaration attached. Refusing is not the same as having zero travel: the difference is
    /// visible in <see cref="debugRefusedChannels"/> and in each channel's source note.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class MavF15ControlActuator : MavControlSurfaceActuatorBase
    {
        [Header("Target")]
        public MavSixDoFBody sixDoFBody;

        [Header("Commanded Surface Deflection (F-15 physical channels)")]
        [Tooltip("What the control law is asking for. This is a REQUEST, not a position - the actual state is published separately and is the only one the aerodynamic model may read.")]
        public MavF15SurfaceState requested;

        [Range(0f, 1f)]
        public float requestedThrottle01;

        [Header("Physical Authority")]
        [Tooltip("Travel and rate authority per channel, each carrying its own provenance. Default is fully unavailable, which refuses all surface motion. Populate a channel only from a source you can name, and mark what that source is.")]
        public MavF15SurfaceLimits limits = MavF15SurfaceLimits.UnavailableExactTarget();

        [Header("Debug / Actual Surface State")]
        [Tooltip("What the aircraft's surfaces are actually doing. Owned here, read-only everywhere else.")]
        public MavF15SurfaceState actualF15Surfaces;

        [Tooltip("The same state projected onto the shared contract, plus throttle. Differential stabilator is NOT represented here and must be read from actualF15Surfaces.")]
        public MavControlInput actual;

        public bool debugAnyChannelRefused;
        public int debugAvailableChannelCount;
        public int debugRefusedChannels;
        public bool debugCommandClamped;
        public string debugStatus = "not stepped";

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

        /// <summary>
        /// The four-channel physical state. Consumers that need differential stabilator - which
        /// means any real F-15 aerodynamic model - must read this rather than
        /// <see cref="ActualSurfaceState"/>.
        /// </summary>
        public MavF15SurfaceState ActualF15SurfaceState
        {
            get { return actualF15Surfaces; }
        }

        public override MavSixDoFBody BoundBody
        {
            get { return sixDoFBody; }
        }

        /// <summary>
        /// Shared-contract entry point. Symmetric stabilator, aileron and rudder map across;
        /// differential stabilator has no generic field and is left UNCHANGED rather than being
        /// derived from aileron here.
        ///
        /// Deriving it would bake the AFIT research relation DTALD = 0.3*DAILD into the physical
        /// actuator, where it would look like aircraft behaviour instead of a research-model
        /// bridge. That relation belongs to the research aero adapter, and only until a real
        /// F-15 control law owns the channel.
        /// </summary>
        public override void SetCommand(MavControlInput command)
        {
            requestedThrottle01 = Mathf.Clamp01(command.throttle01);
            requested.symmetricStabilatorDeg = command.elevatorDeg;
            requested.aileronDeg = command.aileronDeg;
            requested.rudderDeg = command.rudderDeg;
        }

        /// <summary>F-15-native entry point: a control law states all four channels explicitly.</summary>
        public void SetF15Command(MavF15SurfaceState command, float throttle01)
        {
            requested = command;
            requestedThrottle01 = Mathf.Clamp01(throttle01);
        }

        public void StepActuator(float deltaTime)
        {
            Resolve();
            Step(deltaTime);
            Publish();
        }

        public void SnapToBoundedCommand()
        {
            bool refused;
            actualF15Surfaces = BoundCommand(out refused);
            Publish();
        }

        public void Step(float deltaTime)
        {
            bool refused;
            MavF15SurfaceState bounded = BoundCommand(out refused);
            actualF15Surfaces = StepChannels(actualF15Surfaces, bounded, limits, deltaTime);
        }

        /// <summary>
        /// Moves each channel from its current position toward an already-bounded target,
        /// respecting whatever actuator rate has been sourced for that channel.
        ///
        /// Pure and static so the rate behaviour can be exercised deterministically without a
        /// GameObject, a Rigidbody or a play-mode session - the same way the rest of the FDM
        /// validation reaches production code.
        ///
        /// A channel moves at a finite rate ONLY when a rate has actually been sourced. Without
        /// one it steps instantly. That is the honest behaviour for an aircraft whose actuator
        /// dynamics are unknown, and <see cref="MavF15SurfaceChannelLimits.HasSourcedRate"/>
        /// makes it inspectable rather than an unexplained teleport.
        /// </summary>
        public static MavF15SurfaceState StepChannels(
            MavF15SurfaceState current,
            MavF15SurfaceState boundedTarget,
            MavF15SurfaceLimits limits,
            float deltaTime)
        {
            float dt = Mathf.Max(0f, deltaTime);
            MavF15SurfaceState next = current;

            for (int i = 0; i < 4; i++)
            {
                MavF15SurfaceChannel channel = (MavF15SurfaceChannel)i;
                MavF15SurfaceChannelLimits channelLimits = limits.Get(channel);

                float target = boundedTarget.Get(channel);
                float from = current.Get(channel);

                // MoveTowards handles dt == 0 correctly by itself - it returns `from`. Guarding
                // the rate branch on dt > 0 instead would fall through to the instant branch on a
                // zero timestep and teleport a rate-limited surface to its target, which is the
                // exact defect the finite rate exists to prevent.
                next.Set(
                    channel,
                    channelLimits.HasSourcedRate
                        ? Mathf.MoveTowards(from, target, channelLimits.rateLimitDegSec * dt)
                        : target
                );
            }

            return next;
        }

        private MavF15SurfaceState BoundCommand(out bool anyRefused)
        {
            MavF15SurfaceState source = requested;
            if (!source.IsFinite())
            {
                // A non-finite request is a fault upstream. Holding the last good position would
                // hide it; commanding neutral is the safe, visible response.
                source = MavF15SurfaceState.Neutral;
                debugStatus = "REFUSED: non-finite surface request";
            }

            MavF15SurfaceState bounded = limits.Clamp(source, out anyRefused);

            debugAnyChannelRefused = anyRefused;
            debugAvailableChannelCount = limits.AvailableChannelCount;
            debugRefusedChannels = 4 - debugAvailableChannelCount;
            debugCommandClamped =
                !Mathf.Approximately(bounded.symmetricStabilatorDeg, source.symmetricStabilatorDeg)
                || !Mathf.Approximately(bounded.differentialStabilatorDeg, source.differentialStabilatorDeg)
                || !Mathf.Approximately(bounded.aileronDeg, source.aileronDeg)
                || !Mathf.Approximately(bounded.rudderDeg, source.rudderDeg);

            if (debugAvailableChannelCount == 0)
            {
                debugStatus =
                    "REFUSED: no F-15 surface channel has declared travel authority; "
                    + "exact NASA 836 control travel is not frozen";
            }
            else if (anyRefused)
            {
                debugStatus =
                    "PARTIAL: " + debugAvailableChannelCount + "/4 channels have declared authority";
            }
            else
            {
                debugStatus = "OK: all 4 channels have declared authority";
            }

            return bounded;
        }

        /// <summary>
        /// Projects the owned F-15 state onto the shared contract and hands it to the body.
        /// Throttle is passed through untouched: the actuator owns surfaces, propulsion owns
        /// thrust, and this is only the channel the shared contract uses to carry the demand.
        /// </summary>
        private void Publish()
        {
            actual = new MavControlInput
            {
                throttle01 = requestedThrottle01,
                elevatorDeg = actualF15Surfaces.symmetricStabilatorDeg,
                aileronDeg = actualF15Surfaces.aileronDeg,
                rudderDeg = actualF15Surfaces.rudderDeg,
                leadingEdgeFlapDeg = 0f
            };

            if (sixDoFBody != null)
                sixDoFBody.SetControlInput(actual);
        }

        private void Resolve()
        {
            if (sixDoFBody == null)
                sixDoFBody = GetComponent<MavSixDoFBody>();
        }
    }
}
