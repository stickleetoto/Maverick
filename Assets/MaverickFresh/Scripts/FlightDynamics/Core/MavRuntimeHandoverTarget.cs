using System.Collections.Generic;
using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>
    /// Everything about the Rigidbody that a handover must be able to put back.
    ///
    /// Deliberately exhaustive. A rollback that restores velocity but forgets the inertia tensor leaves
    /// an aircraft that flies differently than it did a millisecond earlier, and that is a worse outcome
    /// than refusing the handover in the first place.
    /// </summary>
    public struct MavCapturedRigidbodyState
    {
        public bool valid;

        public Vector3 position;
        public Quaternion rotation;
        public Vector3 linearVelocity;
        public Vector3 angularVelocity;

        public float mass;
        public Vector3 centerOfMass;
        public Vector3 inertiaTensor;
        public Quaternion inertiaTensorRotation;
        public bool automaticCenterOfMass;
        public bool automaticInertiaTensor;

        public bool useGravity;
        public float linearDamping;
        public float angularDamping;

        public MavFlightPhysicsOwner owner;
        /// <summary>
        /// Whether the replacement body was armed at capture time. Recorded so a handover can be
        /// audited, NOT so a rollback can restore it - see Rollback, which always disarms.
        /// </summary>
        public bool replacementBodyArmed;

        public static MavCapturedRigidbodyState Capture(Rigidbody rb)
        {
            MavCapturedRigidbodyState c = new MavCapturedRigidbodyState();
            if (rb == null)
                return c;

            c.position = rb.position;
            c.rotation = rb.rotation;
            c.linearVelocity = rb.linearVelocity;
            c.angularVelocity = rb.angularVelocity;

            c.mass = rb.mass;
            c.centerOfMass = rb.centerOfMass;
            c.inertiaTensor = rb.inertiaTensor;
            c.inertiaTensorRotation = rb.inertiaTensorRotation;
            c.automaticCenterOfMass = rb.automaticCenterOfMass;
            c.automaticInertiaTensor = rb.automaticInertiaTensor;

            c.useGravity = rb.useGravity;
            c.linearDamping = rb.linearDamping;
            c.angularDamping = rb.angularDamping;

            c.valid = true;
            return c;
        }

        public void RestoreTo(Rigidbody rb)
        {
            if (rb == null || !valid)
                return;

            // Mass properties before the state that depends on them.
            rb.mass = mass;
            rb.automaticCenterOfMass = automaticCenterOfMass;
            rb.automaticInertiaTensor = automaticInertiaTensor;
            rb.centerOfMass = centerOfMass;
            rb.inertiaTensor = inertiaTensor;
            rb.inertiaTensorRotation = inertiaTensorRotation;

            rb.useGravity = useGravity;
            rb.linearDamping = linearDamping;
            rb.angularDamping = angularDamping;

            rb.position = position;
            rb.rotation = rotation;
            rb.linearVelocity = linearVelocity;
            rb.angularVelocity = angularVelocity;
        }

        public bool MatchesRigidbody(Rigidbody rb, float tolerance)
        {
            if (rb == null || !valid)
                return false;

            return Mathf.Abs(rb.mass - mass) <= tolerance
                   && (rb.centerOfMass - centerOfMass).magnitude <= tolerance
                   && (rb.inertiaTensor - inertiaTensor).magnitude <= tolerance
                   && (rb.linearVelocity - linearVelocity).magnitude <= tolerance
                   && (rb.angularVelocity - angularVelocity).magnitude <= tolerance
                   && rb.useGravity == useGravity;
        }
    }

    /// <summary>
    /// The concrete <see cref="IMavHandoverTarget"/>: the pure eight-step sequence wired to the real
    /// runtime components.
    ///
    /// WHY A PLAIN CLASS AND NOT A COMPONENT. It cannot be dropped on a GameObject, has no Awake and no
    /// FixedUpdate, and nothing in the project constructs it. Phase 5B.7 delivers the adapter; enabling
    /// it in gameplay is a later, separate decision. The safety hold is also still the last word: step
    /// 7 goes through <see cref="MavFlightPhysicsOwnership.TryActivateReplacement"/>, which refuses
    /// while <c>allowReplacementActivation</c> is false - so even a caller that wired this up by
    /// mistake would be refused in a normal build.
    ///
    /// FAILURE INJECTION. <see cref="injectFailureAt"/> makes a chosen step fail, so the rollback path
    /// can be exercised against the real Rigidbody rather than only against a mock. That matters: the
    /// interesting question is not whether the state machine branches, it is whether the aircraft's
    /// mass, inertia and velocity actually come back.
    /// </summary>
    public class MavRuntimeHandoverTarget : IMavHandoverTarget
    {
        private readonly Rigidbody rigidbody;
        private readonly MavFlightPhysicsOwnership ownership;
        private readonly IMavArmableFlightBody replacementBody;
        private readonly List<IMavLegacyPhysicsWriter> legacyWriters;
        private readonly MavF16MassPolicy massPolicy;
        private readonly bool airborne;

        private MavCapturedRigidbodyState captured;

        /// <summary>Make this step fail, for rollback testing. NotStarted means never fail.</summary>
        public MavHandoverStep injectFailureAt = MavHandoverStep.NotStarted;

        /// <summary>Readiness the caller asserts. Kept explicit so the adapter never invents it.</summary>
        public MavReplacementReadiness readiness = MavReplacementReadiness.NotReady();

        /// <summary>Set true once the replacement writer has been enabled, for inspection.</summary>
        public bool replacementWriterEnabled;

        /// <summary>Steps that actually ran, in order. Read by tests and reports.</summary>
        public readonly List<string> executed = new List<string>();

        public string lastRollbackReason = "";
        public bool didRollBack;

        public MavRuntimeHandoverTarget(
            Rigidbody rigidbody,
            MavFlightPhysicsOwnership ownership,
            IMavArmableFlightBody replacementBody,
            List<IMavLegacyPhysicsWriter> legacyWriters,
            MavF16MassPolicy massPolicy,
            bool airborne)
        {
            this.rigidbody = rigidbody;
            this.ownership = ownership;
            this.replacementBody = replacementBody;
            this.legacyWriters = legacyWriters ?? new List<IMavLegacyPhysicsWriter>();
            this.massPolicy = massPolicy;
            this.airborne = airborne;
        }

        public MavCapturedRigidbodyState CapturedState { get { return captured; } }

        private bool Injected(MavHandoverStep step, out string error)
        {
            executed.Add(step.ToString());
            if (injectFailureAt == step)
            {
                error = "injected failure at " + step;
                return true;
            }
            error = null;
            return false;
        }

        // ---------------------------------------------------------------- 1
        public bool TryCaptureState(out string error)
        {
            if (Injected(MavHandoverStep.CaptureState, out error))
                return false;

            if (rigidbody == null)
            {
                error = "no Rigidbody to capture";
                return false;
            }

            captured = MavCapturedRigidbodyState.Capture(rigidbody);
            captured.owner = ownership != null ? ownership.owner : MavFlightPhysicsOwner.Legacy;
            captured.replacementBodyArmed =
                replacementBody != null && replacementBody.ArmedForLiveFlight;

            if (!captured.valid)
            {
                error = "capture produced no state";
                return false;
            }

            return true;
        }

        // ---------------------------------------------------------------- 2
        public bool ValidateReadiness(out string error)
        {
            if (Injected(MavHandoverStep.ValidateReadiness, out error))
                return false;

            if (ownership == null)
            {
                error = "no ownership authority";
                return false;
            }

            // The mass the caller says it is flying must be the mass the Rigidbody actually has.
            float expected = MavAtomicHandover.MassForPolicy(massPolicy);
            if (expected > 0f
                && Mathf.Abs(rigidbody.mass - expected) > MavAtomicHandover.MassInvarianceToleranceKg)
            {
                error = massPolicy + " policy expects " + expected.ToString("F2")
                        + " kg but the Rigidbody carries " + rigidbody.mass.ToString("F2")
                        + " kg. Reference mode must be SPAWNED in the reference configuration, not "
                        + "converted";
                return false;
            }

            return readiness.IsReady(out error);
        }

        // ---------------------------------------------------------------- 3
        public bool TryInitializeReplacement(out string error)
        {
            if (Injected(MavHandoverStep.InitializeReplacement, out error))
                return false;

            if (replacementBody == null)
            {
                error = "no armable replacement body";
                return false;
            }

            // Deliberately does NOT touch mass properties. Whatever the aircraft spawned with is what
            // it flies; a handover is an ownership change, not a re-configuration. The mass-invariance
            // check in MavAtomicHandover exists because this is exactly where a well-meaning
            // re-configuration would go.
            return true;
        }

        // ---------------------------------------------------------------- 4
        public bool ValidateFinite(out string error)
        {
            if (Injected(MavHandoverStep.ValidateFinite, out error))
                return false;

            if (!Finite(rigidbody.linearVelocity) || !Finite(rigidbody.angularVelocity)
                || !Finite(rigidbody.position) || !Finite(rigidbody.inertiaTensor)
                || float.IsNaN(rigidbody.mass) || float.IsInfinity(rigidbody.mass))
            {
                error = "a live Rigidbody channel is not finite";
                return false;
            }

            return true;
        }

        // ---------------------------------------------------------------- 5
        public bool TryDisableLegacyWriters(out string error)
        {
            if (Injected(MavHandoverStep.DisableLegacyWriters, out error))
                return false;

            // Legacy writers are gated, not switched off: each one consults the authority. Moving the
            // owner is therefore what disables them, and it disables ALL of them in one assignment
            // rather than one component at a time - which is what makes it atomic.
            ownership.owner = MavFlightPhysicsOwner.F16Replacement;
            ownership.ownerReason = "atomic handover: legacy writers gated off";

            // Then verify, rather than assume. A writer that is still reporting activity has not
            // noticed, and proceeding would put two owners on the aircraft.
            for (int i = 0; i < legacyWriters.Count; i++)
            {
                if (legacyWriters[i] != null && legacyWriters[i].WroteLiveForceLastStep)
                {
                    error = "legacy writer " + legacyWriters[i].LegacyWriterName
                            + " still reports a live write after the owner changed";
                    return false;
                }
            }

            return true;
        }

        // ---------------------------------------------------------------- 6
        public bool TryEstablishGravityProvider(out string error)
        {
            if (Injected(MavHandoverStep.EstablishGravityProvider, out error))
                return false;

            ownership.EnforceGravityOwnership();

            if (!MavFlightPhysicsOwnership.HasExactlyOneGravitySource(ownership.owner))
            {
                error = "owner " + ownership.owner + " does not resolve to exactly one gravity source";
                return false;
            }

            bool shouldUseUnity =
                MavFlightPhysicsOwnership.ShouldRigidbodyUseUnityGravity(ownership.owner);
            if (rigidbody.useGravity != shouldUseUnity)
            {
                error = "Rigidbody.useGravity is " + rigidbody.useGravity + " but the resolved provider "
                        + ownership.debugGravityProvider + " requires " + shouldUseUnity;
                return false;
            }

            return true;
        }

        // ---------------------------------------------------------------- 7
        public bool TryEnableReplacementWriter(out string error)
        {
            if (Injected(MavHandoverStep.EnableReplacementWriter, out error))
                return false;

            // Through the existing arming authority, so the safety hold is still the last word: this
            // refuses while allowReplacementActivation is false, which is its state in a normal build.
            if (!ownership.TryRequestReplacementArming("atomic handover", readiness, out error))
                return false;

            replacementWriterEnabled = ownership.GovernedBodyArmed;
            if (!replacementWriterEnabled)
            {
                error = "arming was granted but the governed body does not report itself armed";
                return false;
            }

            return true;
        }

        // ---------------------------------------------------------------- 8
        public bool TryCommit(out string error)
        {
            if (Injected(MavHandoverStep.Commit, out error))
                return false;

            if (ownership.owner != MavFlightPhysicsOwner.F16Replacement)
            {
                error = "owner is " + ownership.owner + " at commit time";
                return false;
            }

            ownership.ownerReason = "atomic handover committed";
            return true;
        }

        // ---------------------------------------------------------------- rollback
        public void Rollback(string reason)
        {
            didRollBack = true;
            lastRollbackReason = reason;
            executed.Add("Rollback");

            // Ownership first, so the legacy writers are permitted again before their state is needed.
            if (ownership != null)
            {
                ownership.ReturnToLegacy("handover rolled back: " + reason);

                // DISARM, unconditionally - deliberately not a restore of captured.replacementBodyArmed.
                //
                // A rollback ends with Legacy owning the aircraft, and a legacy-owned aircraft must
                // never have an armed replacement body sitting behind it: that is two owners for one
                // physical effect, which is the exact condition this phase exists to prevent. Restoring
                // a captured "true" would honour history at the cost of the invariant, and it would also
                // make this file a second thing in the codebase capable of granting arming. Only the
                // declared authority grants; this only ever takes away.
                if (replacementBody != null)
                    replacementBody.ArmedForLiveFlight = false;
            }

            replacementWriterEnabled = false;

            // Then every captured Rigidbody property, including the mass properties.
            captured.RestoreTo(rigidbody);

            if (ownership != null)
                ownership.EnforceGravityOwnership();
        }

        public float CapturedMassKg { get { return captured.valid ? captured.mass : 0f; } }
        public float CurrentMassKg { get { return rigidbody != null ? rigidbody.mass : 0f; } }
        public bool IsAirborne { get { return airborne; } }
        public MavF16MassPolicy MassPolicy { get { return massPolicy; } }

        private static bool Finite(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z)
                   && !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
        }
    }
}
