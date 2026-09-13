using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>
    /// How a propulsion command distributes throttle across installed engines.
    /// </summary>
    public enum MavPropulsionCommandMode
    {
        /// <summary>
        /// Every engine follows one throttle. The default, and the only mode the F-16 needs.
        ///
        /// This is also what a real twin-engine aircraft does almost all of the time: the pilot moves
        /// two levers together, or an autothrottle drives both. Linked is not a simplification, it is
        /// the normal case.
        /// </summary>
        LinkedThrottle = 0,

        /// <summary>
        /// Each engine reads its own channel. Required for differential throttle, asymmetric
        /// shutdown, single-engine operation, and propulsion-controlled-aircraft experiments.
        /// </summary>
        PerEngineThrottle = 1
    }

    /// <summary>
    /// Which layer OWNS per-engine throttle and cutoff this step.
    ///
    /// Exactly one of these is in force at any instant, never a mixture. That exclusivity is the point:
    /// a partially applied per-engine command, with the remaining engines silently taking the scalar,
    /// is indistinguishable at a glance from a fully applied one, and the difference is which engines
    /// the pilot actually commanded.
    /// </summary>
    public enum MavPropulsionCommandAuthority
    {
        /// <summary>
        /// The scalar pipeline throttle is broadcast to every installed engine.
        ///
        /// The default, the only authority the F-16 ever needs, and the fail-safe: reached whenever no
        /// command source is present, a source declines to publish, or a source publishes an
        /// incomplete command.
        /// </summary>
        LinkedScalar = 0,

        /// <summary>
        /// A command source published a command covering EVERY installed engine, and owns all of
        /// them. Partial coverage does not reach this state.
        /// </summary>
        PerEngineSource = 1
    }

    /// <summary>
    /// The propulsion command boundary: what the control layer asks the engines to do.
    ///
    /// This type exists because independent engine STATE and independent engine COMMANDS are
    /// different requirements, and P0 only delivered the first. The whole pilot pipeline -
    /// <see cref="MavPilotCommand"/>, the control law, <see cref="MavControlInput"/>, the actuator,
    /// and <see cref="MavPropulsionModelBase.Evaluate"/> - carries exactly one scalar
    /// <c>throttle01</c>. A twin-engine aircraft could therefore hold two independent power states but
    /// had no way to be COMMANDED into them through production code.
    ///
    /// The fix is a boundary rather than a wider <see cref="MavControlInput"/>. Putting a throttle
    /// array into that struct would have touched every control law, actuator, trim solver and
    /// validation that copies it, made a serialized struct hold a variable-length array, and pushed
    /// engine-count knowledge up into layers that have no business knowing it. Instead the scalar
    /// pipeline stays exactly as it is and means what it always meant - "the throttle" - while
    /// anything that needs per-engine authority publishes into this object through a
    /// <see cref="MavPropulsionCommandSourceBase"/>.
    ///
    /// Allocation: the arrays are sized ONCE when the propulsion system builds, and
    /// <see cref="BeginStep"/> only overwrites values. Nothing here allocates during a physics step.
    ///
    /// Not a struct, deliberately: a command source has to be able to write into it and have the
    /// propulsion system see the result, which a copied value type could not do.
    /// </summary>
    public sealed class MavPropulsionCommand
    {
        private float[] engineThrottle01 = new float[0];
        private bool[] engineCutoff = new bool[0];

        // Which engines a command source explicitly addressed this step. Needed because "the source
        // did not mention this engine" and "the source asked for the same value the scalar would
        // have given" are different intents that produce the same number.
        private bool[] engineAddressed = new bool[0];

        /// <summary>How this command is being interpreted this step.</summary>
        public MavPropulsionCommandMode Mode { get; private set; }

        /// <summary>
        /// Which layer owns per-engine commands this step. Set by
        /// <see cref="MavPropulsionSystem"/> after it has consulted the command source, never by the
        /// source itself - a source does not get to declare its own authority.
        /// </summary>
        public MavPropulsionCommandAuthority Authority { get; private set; }

        /// <summary>How many engines a source explicitly addressed this step.</summary>
        public int AddressedEngineCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < engineAddressed.Length; i++)
                {
                    if (engineAddressed[i])
                        count++;
                }
                return count;
            }
        }

        /// <summary>Whether a source explicitly addressed this engine this step.</summary>
        public bool IsEngineAddressed(int engineIndex)
        {
            if (engineIndex < 0 || engineIndex >= engineAddressed.Length)
                return false;

            return engineAddressed[engineIndex];
        }

        /// <summary>The single throttle every engine follows in linked mode.</summary>
        public float LinkedThrottle01 { get; private set; }

        /// <summary>Number of engine channels this command can address.</summary>
        public int EngineCount
        {
            get { return engineThrottle01.Length; }
        }

        /// <summary>
        /// Sizes the command to an engine count. Called by the propulsion system at build time, so a
        /// step never has to allocate. Existing values are discarded because a re-sized installation
        /// is a different aircraft configuration, not a continuation of the old one.
        /// </summary>
        public void Resize(int engineCount)
        {
            int count = engineCount > 0 ? engineCount : 0;
            if (engineThrottle01.Length != count)
            {
                engineThrottle01 = new float[count];
                engineCutoff = new bool[count];
                engineAddressed = new bool[count];
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    engineThrottle01[i] = 0f;
                    engineCutoff[i] = false;
                    engineAddressed[i] = false;
                }
            }

            Mode = MavPropulsionCommandMode.LinkedThrottle;
            Authority = MavPropulsionCommandAuthority.LinkedScalar;
            LinkedThrottle01 = 0f;
        }

        /// <summary>
        /// Resets the command to "linked, at this throttle" for a new step.
        ///
        /// Called by the propulsion system BEFORE it consults a command source, so the default every
        /// step is the pipeline throttle applied to every engine. A command source that stops
        /// publishing - disabled, removed, or returning false - therefore reverts to linked flight
        /// rather than leaving a stale asymmetric command applied forever. That failure direction
        /// matters: a stuck differential throttle is a control failure, a reverted link is not.
        /// </summary>
        public void BeginStep(float pipelineThrottle01)
        {
            Mode = MavPropulsionCommandMode.LinkedThrottle;
            Authority = MavPropulsionCommandAuthority.LinkedScalar;
            LinkedThrottle01 = Sanitise(pipelineThrottle01);

            for (int i = 0; i < engineThrottle01.Length; i++)
            {
                engineThrottle01[i] = LinkedThrottle01;
                engineCutoff[i] = false;
                engineAddressed[i] = false;
            }
        }

        /// <summary>
        /// Commands one throttle for every engine. Leaves the mode linked.
        /// </summary>
        public void SetLinkedThrottle(float throttle01)
        {
            Mode = MavPropulsionCommandMode.LinkedThrottle;
            LinkedThrottle01 = Sanitise(throttle01);

            for (int i = 0; i < engineThrottle01.Length; i++)
                engineThrottle01[i] = LinkedThrottle01;
        }

        /// <summary>
        /// Commands one engine's throttle by index, switching the command to per-engine mode.
        ///
        /// Out-of-range indices are ignored rather than throwing: a command source that miscounts
        /// engines must not take the physics step down with it, and the ignored write shows up as an
        /// engine that did not respond.
        /// </summary>
        public void SetEngineThrottle(int engineIndex, float throttle01)
        {
            if (engineIndex < 0 || engineIndex >= engineThrottle01.Length)
                return;

            Mode = MavPropulsionCommandMode.PerEngineThrottle;
            engineThrottle01[engineIndex] = Sanitise(throttle01);
            engineAddressed[engineIndex] = true;
        }

        /// <summary>
        /// Commands fuel cutoff for one engine.
        ///
        /// An OPERATIONAL state, not a data-provenance statement: a deliberately shut-down engine with
        /// an authoritative model is still backed by authoritative data, and
        /// <see cref="MavPropulsionSystem"/> keeps those two questions apart.
        ///
        /// Cutoff needs no fake yaw torque. The remaining engine's thrust acts at its own mount
        /// point, so the yawing moment falls out of r x F exactly as it should.
        /// </summary>
        public void SetEngineCutoff(int engineIndex, bool cutoff)
        {
            if (engineIndex < 0 || engineIndex >= engineCutoff.Length)
                return;

            if (cutoff)
                Mode = MavPropulsionCommandMode.PerEngineThrottle;

            engineCutoff[engineIndex] = cutoff;
            engineAddressed[engineIndex] = true;
        }

        /// <summary>
        /// Grants per-engine authority to the source's published command.
        ///
        /// Called by <see cref="MavPropulsionSystem"/> ONLY when a source published a command covering
        /// every installed engine. The decision lives with the system rather than the source because a
        /// source declaring its own authority could claim ownership of engines it never addressed.
        /// </summary>
        public void CommitPerEngineAuthority()
        {
            Mode = MavPropulsionCommandMode.PerEngineThrottle;
            Authority = MavPropulsionCommandAuthority.PerEngineSource;
        }

        /// <summary>
        /// Discards any published per-engine command and returns every engine to the scalar throttle.
        ///
        /// The fail-safe path, used when a source declines to publish OR publishes an incomplete
        /// command. An incomplete command is refused rather than partially honoured: leaving the
        /// unaddressed engines on the scalar would be a blend of two authorities, and a reader could
        /// not tell which engines the source had actually commanded.
        /// </summary>
        public void RevertToLinked(float pipelineThrottle01)
        {
            Mode = MavPropulsionCommandMode.LinkedThrottle;
            Authority = MavPropulsionCommandAuthority.LinkedScalar;
            LinkedThrottle01 = Sanitise(pipelineThrottle01);

            for (int i = 0; i < engineThrottle01.Length; i++)
            {
                engineThrottle01[i] = LinkedThrottle01;
                engineCutoff[i] = false;
                engineAddressed[i] = false;
            }
        }

        /// <summary>
        /// Throttle for one engine.
        ///
        /// In linked mode every engine reads the linked value, so the per-engine array is not
        /// consulted at all - that keeps linked flight immune to a stale per-engine entry. A cut-off
        /// engine commands zero regardless of mode.
        /// </summary>
        public float ThrottleForEngine(int engineIndex)
        {
            if (engineIndex < 0 || engineIndex >= engineThrottle01.Length)
                return 0f;

            // Under LinkedScalar authority the per-engine array is not consulted at all, so a stale
            // or partial entry cannot leak into linked flight.
            if (Authority == MavPropulsionCommandAuthority.LinkedScalar)
                return LinkedThrottle01;

            if (engineCutoff[engineIndex])
                return 0f;

            return engineThrottle01[engineIndex];
        }

        public bool IsCutoff(int engineIndex)
        {
            if (engineIndex < 0 || engineIndex >= engineCutoff.Length)
                return false;

            // Cutoff is a per-engine command, so it exists only under per-engine authority. Honouring
            // it under LinkedScalar would be the blend this design refuses.
            if (Authority == MavPropulsionCommandAuthority.LinkedScalar)
                return false;

            return engineCutoff[engineIndex];
        }

        /// <summary>
        /// Largest difference between any two engines' commanded throttles.
        ///
        /// Zero means symmetric. Reported so telemetry can say whether an asymmetry was COMMANDED,
        /// which separates a differential-throttle input from an engine that is failing to follow one.
        /// </summary>
        public float CommandedThrottleSpread()
        {
            if (engineThrottle01.Length == 0)
                return 0f;

            float min = float.MaxValue;
            float max = float.MinValue;
            for (int i = 0; i < engineThrottle01.Length; i++)
            {
                float value = ThrottleForEngine(i);
                if (value < min) min = value;
                if (value > max) max = value;
            }

            return max - min;
        }

        /// <summary>
        /// Clamps to 0..1 and maps a non-finite command to idle.
        ///
        /// Idle rather than a clamp: a NaN throttle carries no defensible interpretation, and choosing
        /// 1.0 for it would turn a bad command into full power.
        /// </summary>
        private static float Sanitise(float throttle01)
        {
            if (float.IsNaN(throttle01) || float.IsInfinity(throttle01))
                return 0f;

            return Mathf.Clamp01(throttle01);
        }
    }

    /// <summary>
    /// Supplies per-engine propulsion commands, when an aircraft needs them.
    ///
    /// A MonoBehaviour base class rather than an interface because Unity cannot serialize an interface
    /// reference, and the project already uses this shape for every other pluggable piece
    /// (<see cref="MavPropulsionModelBase"/>, <see cref="MavThrustDeckBase"/>,
    /// <see cref="MavControlSurfaceActuatorBase"/>).
    ///
    /// An aircraft with no source attached flies LINKED, which is why the F-16 needs none of this and
    /// why a normal F-15 would not need one either. A source is what you add to get differential
    /// throttle, asymmetric shutdown, or a propulsion-based control law.
    ///
    /// A source must NOT touch Rigidbody, must not compute loads, and must not hold engine state. Its
    /// only job is to write commands into the object it is handed.
    /// </summary>
    public abstract class MavPropulsionCommandSourceBase : MonoBehaviour
    {
        /// <summary>Identity for telemetry and readiness reporting.</summary>
        public abstract string CommandSourceName { get; }

        /// <summary>
        /// Writes this step's commands into <paramref name="command"/>.
        ///
        /// The command arrives already reset to linked at <paramref name="pipelineThrottle01"/>, so an
        /// implementation only has to write what it wants to change. Returning false means "no
        /// per-engine intent this step" and leaves the linked default in place.
        /// </summary>
        public abstract bool TryPublishPropulsionCommand(
            MavPropulsionCommand command,
            float pipelineThrottle01);
    }
}
