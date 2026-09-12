using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>
    /// Who owns the live aircraft's physics.
    ///
    /// Deliberately only three working modes plus a fault. Every extra mode is another state somebody
    /// has to reason about when deciding whether a force may be applied.
    /// </summary>
    public enum MavFlightPhysicsOwner
    {
        /// <summary>
        /// The legacy Maverick player stack owns the aircraft. Replacement FDM affects nothing.
        /// This is the default, and it is what the aircraft flies as today.
        /// </summary>
        Legacy = 0,

        /// <summary>
        /// Legacy still owns the live aircraft; the replacement FDM computes in parallel for
        /// telemetry only and writes NOTHING to the Rigidbody.
        /// </summary>
        Shadow = 1,

        /// <summary>
        /// The replacement F-16 stack owns live motion and every legacy physical writer is gated off.
        /// Phase 5A implements the refusal logic for entering this mode but never enters it.
        /// </summary>
        F16Replacement = 2,

        /// <summary>
        /// Ownership could not be established or handed back cleanly. Fails closed: legacy is allowed
        /// to fly the aircraft, the replacement stack is held off, and it stays that way until an
        /// operator clears it.
        /// </summary>
        Fault = 3
    }

    /// <summary>What kind of physical effect a legacy writer produces.</summary>
    public enum MavLegacyWriterKind
    {
        Aerodynamic = 0,
        Thrust = 1,
        Drag = 2,
        ControlTorque = 3,
        StabilityTorque = 4,
        VelocityAssist = 5,
        Gravity = 6,
        Other = 7
    }

    /// <summary>
    /// A component that can write force, torque or velocity to the live player Rigidbody.
    ///
    /// Implementing this is how a writer becomes VISIBLE to the ownership gate. The gate cannot
    /// enumerate what it does not know about, and "I assume that component is off" is precisely the
    /// reasoning this interface exists to replace.
    /// </summary>
    public interface IMavLegacyPhysicsWriter
    {
        /// <summary>Stable name for diagnostics.</summary>
        string LegacyWriterName { get; }

        /// <summary>What sort of physical effect this writer produces.</summary>
        MavLegacyWriterKind LegacyWriterKind { get; }

        /// <summary>
        /// Whether this writer actually applied something to the live Rigidbody on its most recent
        /// physics step. This is the OBSERVED fact, not a configuration flag: a component being
        /// enabled is not proof that it wrote, and a component being disabled is not proof that it
        /// did not.
        /// </summary>
        bool WroteLiveForceLastStep { get; }

        /// <summary>Physics step index at which the flag above was last set, for staleness checks.</summary>
        int LegacyWriterLastStepIndex { get; }
    }

    /// <summary>
    /// A flight body whose live-flight arming the ownership authority governs.
    ///
    /// The authority needs exactly one thing from the replacement stack: the ability to arm and disarm
    /// it. Depending on the whole six-DoF body for that would couple the ownership decision to the
    /// entire aerodynamic pipeline, and would mean the arming rule could not be exercised without
    /// standing up a Rigidbody, an aero model and a control law first. Narrowing it to this makes the
    /// arming contract the explicit thing it ought to be.
    /// </summary>
    public interface IMavArmableFlightBody
    {
        /// <summary>Name for diagnostics.</summary>
        string ArmableBodyName { get; }

        /// <summary>
        /// Whether this body is armed to apply loads to the live aircraft. Settable ONLY by the
        /// ownership authority - it is the single arming decision this whole phase is about.
        /// </summary>
        bool ArmedForLiveFlight { get; set; }
    }

    /// <summary>
    /// One entry in an ownership report.
    /// </summary>
    public struct MavWriterReport
    {
        public string name;
        public MavLegacyWriterKind kind;
        public bool wroteLastStep;
        public int lastStepIndex;
    }

    /// <summary>
    /// THE gate. One object decides whether legacy physics may touch the live Rigidbody, and every
    /// legacy writer asks it rather than being switched off from the outside.
    ///
    /// WHY A GATE AND NOT enabled = false. Disabling components to transfer ownership has three
    /// problems this project has already been bitten by. It cannot distinguish a component that a
    /// designer deliberately turned off from one the transition turned off, so restoring it is
    /// guesswork. It disables command generation along with force writing, when the mouse-flight
    /// command path is supposed to survive into replacement mode. And it proves nothing: a component
    /// can be enabled and contribute nothing, or be re-enabled by something else next frame. A gate
    /// consulted at the moment of writing answers the only question that matters - may THIS write
    /// happen, right now - and the writers report back what they actually did.
    ///
    /// PHASE 5A SCOPE. Default mode is Legacy and in Legacy mode the gate allows everything, so
    /// installing this changes no behaviour whatsoever. Shadow adds replacement computation with no
    /// Rigidbody writes. F16Replacement has its entry conditions and refusal logic implemented and
    /// tested, but Phase 5A never enters it.
    ///
    /// THE SINGLE ARMING AUTHORITY. This component, and only this component, decides whether
    /// MavSixDoFBody is armed. It drives simulationEnabled from the ownership mode every step, and
    /// corrects it if anything else moves it - counting the correction, so a second would-be authority
    /// shows up in diagnostics instead of quietly winning every other frame.
    ///
    /// MavPhysicsOwnershipController (Phase 3) used to set simulationEnabled itself, which made it a
    /// second independent authority. It is now a delegate: it keeps its state machine, restoration-debt
    /// tracking and rollback, and calls TryRequestReplacementArming instead of deciding. A request is
    /// not a decision. That controller cannot satisfy this gate on its own, because it has no
    /// visibility into whether the replacement pipeline has ever run and correctly reports the
    /// shadow-readiness fields as false.
    /// </summary>
    /// <summary>
    /// Where the aircraft's gravitational acceleration comes from. Exactly one of these is active at
    /// a time, by construction rather than by convention.
    ///
    /// WHY AN ENUM AND NOT A BOOL. Gravity was previously decided by two independent conditions in
    /// two components: MavAeroBody forced Rigidbody.useGravity = false unconditionally every physics
    /// step, while its own custom gravity force was gated on legacy physics being permitted. Those
    /// two conditions disagree in F16Replacement mode, and the aircraft ends up with NO gravity at
    /// all. Naming the provider makes "who supplies gravity" a single answer that can be asserted,
    /// instead of an emergent property of two unrelated if-statements.
    /// </summary>
    public enum MavGravityProvider
    {
        /// <summary>Nobody. Never a valid steady state for an aircraft in flight.</summary>
        None = 0,

        /// <summary>Unity's own Rigidbody gravity integration, i.e. Rigidbody.useGravity = true.</summary>
        UnityRigidbody = 1,

        /// <summary>MavAeroBody's explicit gravity force, scaled by its gravityBlend.</summary>
        LegacyAeroCustomGravity = 2,

        /// <summary>The replacement stack's summed load set carries gravity itself.</summary>
        ReplacementLoadSet = 3
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-400)]
    public class MavFlightPhysicsOwnership : MonoBehaviour
    {
        [Header("Governed body")]
        [Tooltip("Name of the body whose arming this authority governs, or 'none'. Read-only mirror: the body is attached at runtime by the startup path rather than serialized, so a stale scene reference cannot point the authority at the wrong object.")]
        public string debugGovernedBody = "none";

        /// <summary>
        /// The body this authority arms and disarms. Attached at runtime, not serialized: a serialized
        /// cross-reference to a component on the same object is exactly the kind of thing that goes
        /// stale and then quietly governs nothing.
        /// </summary>
        private IMavArmableFlightBody governedBody;

        /// <summary>
        /// Hands this authority the body it governs. Idempotent.
        ///
        /// Called by the startup path. Passing null detaches, which is the honest state for an aircraft
        /// that has no replacement stack installed - the common case today.
        /// </summary>
        public void AttachGovernedBody(IMavArmableFlightBody body)
        {
            governedBody = body;
            debugGovernedBody = body != null ? body.ArmableBodyName : "none";
            EnforceArmingForCurrentOwner();
        }

        /// <summary>Whether this authority currently governs a body.</summary>
        public bool HasGovernedBody
        {
            get { return governedBody != null; }
        }

        /// <summary>Whether the governed body is armed. False when nothing is governed.</summary>
        public bool GovernedBodyArmed
        {
            get { return governedBody != null && governedBody.ArmedForLiveFlight; }
        }

        [Header("Ownership")]
        [Tooltip("Current owner of live aircraft physics. Legacy is the default and reproduces today's behaviour exactly.")]
        public MavFlightPhysicsOwner owner = MavFlightPhysicsOwner.Legacy;

        [TextArea(2, 5)] public string ownerReason = "default: legacy player stack owns physics";

        [Header("Safety")]
        [Tooltip("OFF by default. Even with every invariant satisfied, the gate will not enter F16Replacement unless this is explicitly enabled. Phase 5A leaves it off.")]
        public bool allowReplacementActivation = false;

        [Tooltip("How many recent physics steps a legacy writer must have been silent for before replacement activation will consider it inactive. One step is not enough: a writer that only acts under some conditions would look inactive at the wrong moment.")]
        public int legacyQuietStepsRequired = 5;

        [Header("Diagnostics")]
        public int debugPhysicsStepIndex;
        public int debugRegisteredWriterCount;
        public int debugActiveLegacyWriterCount;
        [TextArea(3, 10)] public string debugActiveLegacyWriters = "none";
        public int debugRefusedActivations;
        public int debugRolledBackTransitions;
        [Tooltip("Arming requests refused because this authority had not granted replacement ownership.")]
        public int debugRefusedArmingRequests;
        [Tooltip("Times this authority corrected simulationEnabled because something else had set it.")]
        public int debugArmingCorrections;
        [TextArea(4, 16)] public string debugOwnershipReport = "not evaluated";

        private readonly List<IMavLegacyPhysicsWriter> writers = new List<IMavLegacyPhysicsWriter>(16);

        /// <summary>
        /// Registers a legacy physical writer. Idempotent, so a writer may call this from both Awake
        /// and OnEnable without double-counting.
        /// </summary>
        public void RegisterLegacyWriter(IMavLegacyPhysicsWriter writer)
        {
            if (writer == null || writers.Contains(writer))
                return;

            writers.Add(writer);
            debugRegisteredWriterCount = writers.Count;
        }

        public void UnregisterLegacyWriter(IMavLegacyPhysicsWriter writer)
        {
            if (writer == null)
                return;

            writers.Remove(writer);
            debugRegisteredWriterCount = writers.Count;
        }

        // ==================================================================== the gate

        /// <summary>
        /// May legacy physics write to the live Rigidbody right now?
        ///
        /// The one question every legacy writer asks. Kept trivial on purpose: a gate with
        /// conditions scattered through it is a gate nobody can reason about at the call site.
        /// </summary>
        public bool LegacyPhysicsAllowed
        {
            get { return IsLegacyPhysicsAllowed(owner); }
        }

        /// <summary>May the replacement stack apply loads to the live Rigidbody right now?</summary>
        public bool ReplacementPhysicsAllowed
        {
            get { return IsReplacementPhysicsAllowed(owner); }
        }

        /// <summary>Should the replacement stack compute without applying anything?</summary>
        public bool ReplacementShadowComputeRequested
        {
            get { return owner == MavFlightPhysicsOwner.Shadow; }
        }

        /// <summary>
        /// Pure ownership rule: legacy may write in Legacy, Shadow and Fault.
        ///
        /// Fault allows legacy deliberately. A fault means ownership is untrustworthy, and the safe
        /// resting place for an aircraft in flight is the stack that was flying it - not nothing.
        /// </summary>
        public static bool IsLegacyPhysicsAllowed(MavFlightPhysicsOwner owner)
        {
            return owner != MavFlightPhysicsOwner.F16Replacement;
        }

        /// <summary>
        /// Pure ownership rule: the replacement stack may write ONLY in F16Replacement.
        ///
        /// Shadow returning false here is the entire point of shadow mode.
        /// </summary>
        public static bool IsReplacementPhysicsAllowed(MavFlightPhysicsOwner owner)
        {
            return owner == MavFlightPhysicsOwner.F16Replacement;
        }

        /// <summary>
        /// THE INVARIANT. Legacy and replacement may never both be permitted to write.
        /// </summary>
        public static bool ViolatesExclusiveOwnership(MavFlightPhysicsOwner owner)
        {
            return IsLegacyPhysicsAllowed(owner) && IsReplacementPhysicsAllowed(owner);
        }

        // ==================================================================== stepping

        private void FixedUpdate()
        {
            debugPhysicsStepIndex++;

            // Gravity first, and before any writer runs. This component has DefaultExecutionOrder
            // -400, so the flag is settled for the step before MavAeroBody or the jet touch anything.
            EnforceGravityOwnership();

            EnforceArmingForCurrentOwner();
            RefreshWriterDiagnostics();
        }

        /// <summary>
        /// Drives MavSixDoFBody.simulationEnabled from the ownership mode, every step.
        ///
        /// THE SINGLE ARMING AUTHORITY. The replacement body is armed if and only if this authority
        /// says the replacement stack owns physics. Enforced continuously rather than only at
        /// transitions, because "I set it once" is not the same claim as "nothing else can set it" -
        /// and the whole point of having one authority is that the second claim is the one that holds.
        ///
        /// Only governs a body it was explicitly given. A validation rig that constructs its own body
        /// without wiring it here is untouched, which keeps the existing Phase 2/3 suites valid.
        /// </summary>
        // ==================================================================== gravity ownership

        [Header("Gravity Ownership")]
        [Tooltip("Which component is allowed to supply gravity right now. Resolved from the owner every step; not settable by hand.")]
        public MavGravityProvider debugGravityProvider = MavGravityProvider.LegacyAeroCustomGravity;

        [Tooltip("How many times this authority has had to correct Rigidbody.useGravity away from what some other component left it at. A steadily rising number means something is still fighting over the flag.")]
        public int debugGravityFlagCorrections;

        [TextArea(2, 4)] public string debugGravityOwnershipStatus = "not evaluated";

        /// <summary>
        /// Which single component supplies gravity for a given owner.
        ///
        /// Legacy, Shadow and Fault all answer LegacyAeroCustomGravity. Shadow must match Legacy
        /// exactly - that is what makes it shadow - and Fault deliberately leaves the stack that was
        /// already flying the aircraft in charge, because the safe resting place for an aircraft in
        /// the air is the physics that was holding it up a moment ago.
        ///
        /// F16Replacement answers UnityRigidbody, NOT ReplacementLoadSet. That is a statement about
        /// the code as it exists: MavSixDoFBody does not put a gravity term in its load set today, it
        /// sets Rigidbody.useGravity from its profile and lets Unity integrate weight. Returning
        /// ReplacementLoadSet here would describe an intention rather than the implementation, and
        /// the aircraft would fall through the floor of the abstraction. When the replacement stack
        /// grows its own gravity term, this is the one line that changes.
        /// </summary>
        public static MavGravityProvider ResolveGravityProvider(MavFlightPhysicsOwner owner)
        {
            switch (owner)
            {
                case MavFlightPhysicsOwner.F16Replacement:
                    return MavGravityProvider.UnityRigidbody;

                case MavFlightPhysicsOwner.Legacy:
                case MavFlightPhysicsOwner.Shadow:
                case MavFlightPhysicsOwner.Fault:
                default:
                    return MavGravityProvider.LegacyAeroCustomGravity;
            }
        }

        /// <summary>Should Rigidbody.useGravity be on, for a given owner?</summary>
        public static bool ShouldRigidbodyUseUnityGravity(MavFlightPhysicsOwner owner)
        {
            return ResolveGravityProvider(owner) == MavGravityProvider.UnityRigidbody;
        }

        /// <summary>May MavAeroBody add its own gravity force, for a given owner?</summary>
        public static bool IsLegacyCustomGravityAllowed(MavFlightPhysicsOwner owner)
        {
            return ResolveGravityProvider(owner) == MavGravityProvider.LegacyAeroCustomGravity;
        }

        /// <summary>
        /// Exactly one gravity source in every mode - no mode with none, no mode with two.
        ///
        /// This is the invariant the previous arrangement violated, and it is checkable as a pure
        /// function of the enum, so it is checkable without a Rigidbody, a scene, or a play session.
        /// </summary>
        public static bool HasExactlyOneGravitySource(MavFlightPhysicsOwner owner)
        {
            MavGravityProvider provider = ResolveGravityProvider(owner);
            if (provider == MavGravityProvider.None)
                return false;

            bool unity = provider == MavGravityProvider.UnityRigidbody;
            bool legacyCustom = provider == MavGravityProvider.LegacyAeroCustomGravity;
            bool replacement = provider == MavGravityProvider.ReplacementLoadSet;

            int sources = (unity ? 1 : 0) + (legacyCustom ? 1 : 0) + (replacement ? 1 : 0);
            return sources == 1;
        }

        [Tooltip("Scale the legacy gravity provider is actually applying, reported by it each step. "
                 + "The F-16 uses 1.0, but other aircraft profiles use 0.45, and specific force is only "
                 + "correct if it subtracts the gravity that is REALLY acting.")]
        public float debugLegacyGravityScale = 1f;

        /// <summary>
        /// The legacy gravity provider reports how much gravity it is applying.
        ///
        /// Pushed rather than pulled: this authority does not need to know MavAeroBody's type, and the
        /// number is whatever the provider actually used this step rather than what a configuration
        /// field says it should have used.
        /// </summary>
        public void ReportLegacyGravityScale(float scale)
        {
            debugLegacyGravityScale = scale;
        }

        /// <summary>
        /// The gravitational acceleration ACTUALLY acting on the aircraft, world axes, m/s^2.
        ///
        /// WHY THIS IS NOT JUST Physics.gravity. The legacy stack applies gravity as an explicit force
        /// scaled by MavAeroBody.gravityBlend, which is 1.0 for the F-16 but 0.45 for several other
        /// aircraft profiles. Load factor is specific force, i.e. acceleration MINUS gravity, so
        /// subtracting a full g from an aircraft that is only being pulled down by 0.45 g would put a
        /// 0.55 g error straight into the G limiter.
        ///
        /// The gravity owner is the only component that knows the answer, which is why it lives here.
        /// </summary>
        public Vector3 EffectiveGravityAccelerationWorld
        {
            get
            {
                switch (ResolveGravityProvider(owner))
                {
                    case MavGravityProvider.LegacyAeroCustomGravity:
                        return Physics.gravity * debugLegacyGravityScale;

                    case MavGravityProvider.UnityRigidbody:
                    case MavGravityProvider.ReplacementLoadSet:
                        return Physics.gravity;

                    default:
                        return Vector3.zero;
                }
            }
        }

        /// <summary>May MavAeroBody add its own gravity force right now?</summary>
        public bool LegacyCustomGravityAllowed
        {
            get { return IsLegacyCustomGravityAllowed(owner); }
        }

        /// <summary>
        /// This authority owns Rigidbody.useGravity, and asserts it every step.
        ///
        /// Setup-time writers - the bootstrap, the profile applier, the jet's rigidbody setup, and
        /// MavSixDoFBody's own initialisation - all still write the flag when they run. They are
        /// seeds, not owners: whatever they leave behind, the value at the moment physics integrates
        /// is the one this method wrote. That is the difference between "I set it once" and "nothing
        /// else can leave it wrong", and only the second is ownership.
        /// </summary>
        public void EnforceGravityOwnership()
        {
            debugGravityProvider = ResolveGravityProvider(owner);

            if (gravityRigidbody == null)
                gravityRigidbody = GetComponent<Rigidbody>();

            if (gravityRigidbody == null)
            {
                debugGravityOwnershipStatus =
                    "no Rigidbody on this object, so gravity ownership is not enforced here";
                return;
            }

            bool shouldUseUnityGravity = debugGravityProvider == MavGravityProvider.UnityRigidbody;
            if (gravityRigidbody.useGravity != shouldUseUnityGravity)
            {
                gravityRigidbody.useGravity = shouldUseUnityGravity;
                debugGravityFlagCorrections++;
            }

            debugGravityOwnershipStatus =
                "owner=" + owner
                + "; gravityProvider=" + debugGravityProvider
                + "; Rigidbody.useGravity=" + (shouldUseUnityGravity ? "ON" : "OFF")
                + "; legacyCustomGravity="
                + (debugGravityProvider == MavGravityProvider.LegacyAeroCustomGravity
                    ? "PERMITTED" : "blocked")
                + "; corrections=" + debugGravityFlagCorrections;
        }

        private Rigidbody gravityRigidbody;

        public void EnforceArmingForCurrentOwner()
        {
            if (governedBody == null)
                return;

            bool shouldBeArmed = IsReplacementPhysicsAllowed(owner);
            if (governedBody.ArmedForLiveFlight == shouldBeArmed)
                return;

            // Something else moved it. Correct it and count that, so a second would-be authority shows
            // up in the diagnostics instead of quietly winning every other frame.
            governedBody.ArmedForLiveFlight = shouldBeArmed;
            debugArmingCorrections++;
        }

        /// <summary>
        /// The delegation entry point for any component that used to arm the body itself.
        ///
        /// MavPhysicsOwnershipController (Phase 3) called sixDoFBody.simulationEnabled = true
        /// directly, which made it a second independent ownership authority. It now asks here instead.
        /// A request is not a decision: this method grants arming only if the full activation path -
        /// safety hold, readiness, and every legacy writer being quiet - passes.
        ///
        /// Fails closed. A requester with no authority available gets a refusal, not a default yes.
        /// </summary>
        public bool TryRequestReplacementArming(
            string requesterName,
            MavReplacementReadiness readiness,
            out string error)
        {
            if (!TryActivateReplacement(readiness, out error))
            {
                debugRefusedArmingRequests++;
                error = "arming request from " + requesterName + " refused: " + error;
                return false;
            }

            EnforceArmingForCurrentOwner();
            return true;
        }

        /// <summary>The step index writers stamp their reports with.</summary>
        public int CurrentStepIndex
        {
            get { return debugPhysicsStepIndex; }
        }

        private void RefreshWriterDiagnostics()
        {
            int active = 0;
            StringBuilder names = null;

            for (int i = 0; i < writers.Count; i++)
            {
                IMavLegacyPhysicsWriter w = writers[i];
                if (w == null || !w.WroteLiveForceLastStep)
                    continue;

                active++;
                if (names == null)
                    names = new StringBuilder(128);
                else
                    names.Append(", ");

                names.Append(w.LegacyWriterName).Append('[').Append(w.LegacyWriterKind).Append(']');
            }

            debugActiveLegacyWriterCount = active;
            debugActiveLegacyWriters = names == null ? "none" : names.ToString();
        }

        // ==================================================================== activation

        /// <summary>
        /// Whether every legacy writer has been silent long enough to believe it is contributing
        /// nothing.
        ///
        /// Observation, not configuration. The issue this implements is explicit that a component
        /// being enabled is not proof of ownership, and the converse holds too - so the question asked
        /// is "did anything actually write", over several steps rather than one.
        /// </summary>
        public bool AllLegacyWritersQuiet(out string offender)
        {
            for (int i = 0; i < writers.Count; i++)
            {
                IMavLegacyPhysicsWriter w = writers[i];
                if (w == null)
                    continue;

                if (w.WroteLiveForceLastStep)
                {
                    offender = w.LegacyWriterName + " wrote live force on step " + w.LegacyWriterLastStepIndex;
                    return false;
                }

                int quietFor = debugPhysicsStepIndex - w.LegacyWriterLastStepIndex;
                if (w.LegacyWriterLastStepIndex > 0 && quietFor < Mathf.Max(1, legacyQuietStepsRequired))
                {
                    offender = w.LegacyWriterName + " was quiet for only " + quietFor + " steps";
                    return false;
                }
            }

            offender = "none";
            return true;
        }

        /// <summary>
        /// Requests Shadow mode. Cannot affect live motion by construction, so it has no ownership
        /// preconditions beyond not being in a fault.
        /// </summary>
        public bool TryEnterShadow(out string error)
        {
            if (owner == MavFlightPhysicsOwner.Fault)
            {
                error = "ownership is in a fault; clear it before requesting shadow mode";
                return false;
            }

            if (owner == MavFlightPhysicsOwner.F16Replacement)
            {
                error = "already in replacement mode; returning to shadow means handing physics back "
                        + "to legacy, which is a separate request";
                return false;
            }

            SetOwner(MavFlightPhysicsOwner.Shadow,
                "shadow: legacy still owns physics, replacement computes telemetry only");
            error = string.Empty;
            return true;
        }

        /// <summary>Returns to Legacy. Always permitted: it is the safe direction.</summary>
        public void ReturnToLegacy(string reason)
        {
            SetOwner(MavFlightPhysicsOwner.Legacy, "returned to legacy: " + reason);
        }

        /// <summary>
        /// The atomic handover to replacement ownership.
        ///
        /// Verify, switch, confirm - and on any failure roll all the way back to Legacy rather than
        /// leaving ownership half-moved. The sequence completes inside one call, before any physics
        /// owner runs again, so there is no physics step during which both or neither owns the
        /// aircraft.
        ///
        /// Phase 5A never calls this with allowReplacementActivation true. The logic exists so the
        /// refusal conditions are implemented and testable now, rather than being written in a hurry
        /// at the moment they first matter.
        /// </summary>
        public bool TryActivateReplacement(MavReplacementReadiness readiness, out string error)
        {
            if (!allowReplacementActivation)
            {
                error = "replacement activation is disabled by the safety hold "
                        + "(allowReplacementActivation is false)";
                debugRefusedActivations++;
                return false;
            }

            if (owner == MavFlightPhysicsOwner.Fault)
            {
                error = "ownership is in a fault; it must be cleared deliberately first";
                debugRefusedActivations++;
                return false;
            }

            if (!readiness.IsReady(out error))
            {
                debugRefusedActivations++;
                return false;
            }

            string offender;
            if (!AllLegacyWritersQuiet(out offender))
            {
                error = "legacy physical writers are still active: " + offender;
                debugRefusedActivations++;
                return false;
            }

            MavFlightPhysicsOwner previous = owner;
            SetOwner(MavFlightPhysicsOwner.F16Replacement, "replacement FDM owns physics");

            // Confirm rather than assume. If the switch did not produce a state where exactly one
            // side may write, roll back.
            if (ViolatesExclusiveOwnership(owner)
                || !IsReplacementPhysicsAllowed(owner)
                || IsLegacyPhysicsAllowed(owner))
            {
                debugRolledBackTransitions++;
                SetOwner(MavFlightPhysicsOwner.Legacy,
                    "rolled back: ownership did not settle exclusively on the replacement stack");
                error = "handover could not be confirmed; rolled back to legacy from " + previous;
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Enters the fault state. Legacy keeps flying the aircraft; the replacement stack is held
        /// off until an operator clears it.
        /// </summary>
        public void EnterFault(string reason)
        {
            SetOwner(MavFlightPhysicsOwner.Fault, "FAULT: " + reason);
            Debug.LogError("[Maverick/FDM] Ownership fault: " + reason, this);
        }

        public void ClearFault(string justification)
        {
            if (owner != MavFlightPhysicsOwner.Fault)
                return;

            if (string.IsNullOrEmpty(justification))
            {
                Debug.LogError(
                    "[Maverick/FDM] Refused to clear an ownership fault without a justification.", this);
                return;
            }

            SetOwner(MavFlightPhysicsOwner.Legacy, "fault cleared by operator: " + justification);
        }

        private void SetOwner(MavFlightPhysicsOwner next, string reason)
        {
            owner = next;
            ownerReason = reason;
            EnforceArmingForCurrentOwner();
        }

        // ==================================================================== reporting

        /// <summary>Snapshot of every registered writer and what it did.</summary>
        public MavWriterReport[] BuildWriterReports()
        {
            MavWriterReport[] output = new MavWriterReport[writers.Count];

            for (int i = 0; i < writers.Count; i++)
            {
                IMavLegacyPhysicsWriter w = writers[i];
                MavWriterReport r = new MavWriterReport();
                r.name = w != null ? w.LegacyWriterName : "<destroyed>";
                r.kind = w != null ? w.LegacyWriterKind : MavLegacyWriterKind.Other;
                r.wroteLastStep = w != null && w.WroteLiveForceLastStep;
                r.lastStepIndex = w != null ? w.LegacyWriterLastStepIndex : -1;
                output[i] = r;
            }

            return output;
        }

        [ContextMenu("Build Ownership Report")]
        public void BuildOwnershipReport()
        {
            debugOwnershipReport = DescribeOwnership(
                owner, ownerReason, BuildWriterReports(), debugPhysicsStepIndex);
            Debug.Log("[Maverick/FDM]\n" + debugOwnershipReport, this);
        }

        /// <summary>
        /// The transition diagnostic the issue asks for, plus the writer enumeration that backs it.
        /// Pure, so validation can check the text without a scene.
        /// </summary>
        public static string DescribeOwnership(
            MavFlightPhysicsOwner owner,
            string reason,
            MavWriterReport[] reports,
            int stepIndex)
        {
            int active = 0;
            for (int i = 0; i < reports.Length; i++)
            {
                if (reports[i].wroteLastStep)
                    active++;
            }

            StringBuilder text = new StringBuilder(768);
            text.Append("CurrentOwner=").AppendLine(owner.ToString());
            text.Append("Reason=").AppendLine(reason);
            text.Append("LegacyPhysicsAllowed=").AppendLine(
                IsLegacyPhysicsAllowed(owner) ? "true" : "false");
            text.Append("ReplacementPhysicsAllowed=").AppendLine(
                IsReplacementPhysicsAllowed(owner) ? "true" : "false");
            text.Append("ExclusiveOwnershipHeld=").AppendLine(
                ViolatesExclusiveOwnership(owner) ? "NO - INVARIANT VIOLATED" : "true");
            text.Append("PhysicsStep=").AppendLine(stepIndex.ToString());
            text.Append("RegisteredLegacyWriters=").AppendLine(reports.Length.ToString());
            text.Append("LegacyForceWriters=").AppendLine(active.ToString());

            for (int i = 0; i < reports.Length; i++)
            {
                text.Append("  ").Append(reports[i].wroteLastStep ? "ACTIVE  " : "quiet   ")
                    .Append(reports[i].name)
                    .Append(" [").Append(reports[i].kind).Append("]")
                    .Append(" lastStep=").Append(reports[i].lastStepIndex)
                    .AppendLine();
            }

            return text.ToString();
        }
    }
}
