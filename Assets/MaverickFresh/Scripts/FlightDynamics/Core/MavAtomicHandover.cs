namespace MaverickFresh.FlightDynamics
{
    /// <summary>
    /// Which mass the aircraft is flying. Phase 5B.6 item 6.
    ///
    /// The two numbers disagree by 5.4% and both are legitimate in their own context:
    ///
    ///   LegacyGameplay  9 800 kg  - MavAircraftCatalog's F-16C profile. Tuned for the legacy stack,
    ///                               and the mass every existing handling number was chosen against.
    ///   Reference       9 298.65 kg - NASA TP-1538 Table I (20 500 lb). The mass the sourced inertia
    ///                               tensor belongs to, and the only mass for which the reference
    ///                               aerodynamics and inertia are mutually consistent.
    ///
    /// There is no correct answer that covers both. What is NOT allowed is discovering the
    /// disagreement mid-flight.
    /// </summary>
    public enum MavF16MassPolicy
    {
        /// <summary>Nobody chose. Refused.</summary>
        Unspecified = 0,

        /// <summary>9 800 kg, the legacy catalog value. Correct for the aircraft flying today.</summary>
        LegacyGameplay = 1,

        /// <summary>9 298.65 kg, TP-1538 Table I. Required for reference-configuration experiments.</summary>
        Reference = 2
    }

    /// <summary>The ordered steps of an atomic ownership handover.</summary>
    public enum MavHandoverStep
    {
        NotStarted = 0,
        CaptureState = 1,
        ValidateReadiness = 2,
        InitializeReplacement = 3,
        ValidateFinite = 4,
        DisableLegacyWriters = 5,
        EstablishGravityProvider = 6,
        EnableReplacementWriter = 7,
        Commit = 8,
        Committed = 9,
        RolledBack = 10
    }

    /// <summary>
    /// What a handover needs to be able to do. An interface so the sequence can be exercised, and
    /// failed at every step, without a scene, a Rigidbody or a play session.
    /// </summary>
    public interface IMavHandoverTarget
    {
        bool TryCaptureState(out string error);
        bool ValidateReadiness(out string error);
        bool TryInitializeReplacement(out string error);
        bool ValidateFinite(out string error);
        bool TryDisableLegacyWriters(out string error);
        bool TryEstablishGravityProvider(out string error);
        bool TryEnableReplacementWriter(out string error);
        bool TryCommit(out string error);

        /// <summary>Restore the captured Rigidbody state and mass properties, and return to Legacy.</summary>
        void Rollback(string reason);

        /// <summary>Mass at capture time, kg. Compared against the live mass to forbid a mid-air change.</summary>
        float CapturedMassKg { get; }

        /// <summary>Mass right now, kg.</summary>
        float CurrentMassKg { get; }

        /// <summary>Is the aircraft airborne? An airborne mass change is refused outright.</summary>
        bool IsAirborne { get; }

        /// <summary>Which mass the caller intends to fly.</summary>
        MavF16MassPolicy MassPolicy { get; }
    }

    /// <summary>Outcome of a handover attempt.</summary>
    public struct MavHandoverResult
    {
        public bool committed;
        public MavHandoverStep reachedStep;
        public MavHandoverStep failedAtStep;
        public bool rolledBack;
        public string error;

        public override string ToString()
        {
            return committed
                ? "COMMITTED after " + reachedStep
                : "REFUSED at " + failedAtStep + (rolledBack ? " (rolled back): " : ": ") + error;
        }
    }

    /// <summary>
    /// Atomic ownership handover, Legacy -> F16Replacement. Phase 5B.6 item 7.
    ///
    /// THE POINT OF "ATOMIC". Ownership must move in one indivisible operation. A partial handover -
    /// legacy writers disabled but the replacement writer not yet enabled - is an aircraft with no
    /// physics at all, which is exactly the class of hole Phase 5B.5 found in gravity. So every step
    /// is ordered, every failure rolls back, and the sequence is a single function rather than a
    /// convention that several components are each supposed to honour.
    ///
    /// ORDER, and why it is this order:
    ///
    ///   1. CaptureState             - before anything changes, so rollback has somewhere to go.
    ///   2. ValidateReadiness        - cheap refusals first, while nothing has been touched yet.
    ///   3. InitializeReplacement    - build the replacement state from the captured state.
    ///   4. ValidateFinite           - immediately, because a NaN here would otherwise be handed the
    ///                                 aircraft two steps later.
    ///   5. DisableLegacyWriters     - only once the replacement is known good.
    ///   6. EstablishGravityProvider - between the two writers, the one moment when nothing else is
    ///                                 applying force, so the gravity owner can change cleanly.
    ///   7. EnableReplacementWriter  - the aircraft is now flown by the replacement stack.
    ///   8. Commit                   - record it. Steps 5-7 are the window where a failure MUST roll
    ///                                 back, and the rollback restores the captured Rigidbody state
    ///                                 and mass properties.
    ///
    /// MASS IS NOT ALLOWED TO CHANGE IN THE AIR. Checked before anything else runs, because the
    /// legacy and reference masses differ by 5.4% and a cutover that silently swapped them would
    /// change the aircraft's weight mid-flight. For the first 5C-R experiments the aircraft should be
    /// INITIALIZED in the reference configuration on the ground rather than converted in flight -
    /// this check is what makes that a rule instead of an intention.
    ///
    /// NOTHING HERE ENABLES ANYTHING. This is the sequence; no runtime component calls it yet.
    /// </summary>
    public static class MavAtomicHandover
    {
        /// <summary>Tolerance on the mass-invariance check, kg.</summary>
        public const float MassInvarianceToleranceKg = 0.5f;

        public static MavHandoverResult TryExecute(IMavHandoverTarget target)
        {
            MavHandoverResult result = new MavHandoverResult();
            result.reachedStep = MavHandoverStep.NotStarted;

            if (target == null)
            {
                result.failedAtStep = MavHandoverStep.NotStarted;
                result.error = "no handover target";
                return result;
            }

            // ---- precondition: a mass policy must have been chosen ----
            if (target.MassPolicy == MavF16MassPolicy.Unspecified)
            {
                result.failedAtStep = MavHandoverStep.NotStarted;
                result.error = "no mass policy chosen; the legacy 9800 kg and reference 9298.65 kg "
                               + "masses differ by 5.4% and the caller must say which it is flying";
                return result;
            }

            string error;

            // ---- 1. capture ----
            result.reachedStep = MavHandoverStep.CaptureState;
            if (!target.TryCaptureState(out error))
            {
                result.failedAtStep = MavHandoverStep.CaptureState;
                result.error = "could not capture the pre-handover state: " + error;
                // Nothing has changed yet, so there is nothing to roll back.
                return result;
            }

            // ---- precondition: no airborne mass change ----
            // After capture, because the captured mass is what we compare against.
            if (target.IsAirborne
                && System.Math.Abs(target.CurrentMassKg - target.CapturedMassKg)
                   > MassInvarianceToleranceKg)
            {
                result.failedAtStep = MavHandoverStep.CaptureState;
                result.error = "refusing an AIRBORNE mass change: captured "
                               + target.CapturedMassKg.ToString("F2") + " kg, now "
                               + target.CurrentMassKg.ToString("F2")
                               + " kg. Initialize in the reference configuration on the ground "
                               + "instead of converting in flight";
                return result;
            }

            // ---- 2. readiness ----
            result.reachedStep = MavHandoverStep.ValidateReadiness;
            if (!target.ValidateReadiness(out error))
            {
                result.failedAtStep = MavHandoverStep.ValidateReadiness;
                result.error = "replacement readiness refused: " + error;
                return result;
            }

            // ---- 3. initialize ----
            result.reachedStep = MavHandoverStep.InitializeReplacement;
            if (!target.TryInitializeReplacement(out error))
            {
                result.failedAtStep = MavHandoverStep.InitializeReplacement;
                result.error = "could not initialize replacement state: " + error;
                return RollBack(target, result);
            }

            // ---- 4. finite ----
            result.reachedStep = MavHandoverStep.ValidateFinite;
            if (!target.ValidateFinite(out error))
            {
                result.failedAtStep = MavHandoverStep.ValidateFinite;
                result.error = "replacement state is not finite: " + error;
                return RollBack(target, result);
            }

            // ---- 5. disable legacy ----
            result.reachedStep = MavHandoverStep.DisableLegacyWriters;
            if (!target.TryDisableLegacyWriters(out error))
            {
                result.failedAtStep = MavHandoverStep.DisableLegacyWriters;
                result.error = "could not disable every legacy physical writer: " + error;
                return RollBack(target, result);
            }

            // ---- 6. gravity ----
            result.reachedStep = MavHandoverStep.EstablishGravityProvider;
            if (!target.TryEstablishGravityProvider(out error))
            {
                result.failedAtStep = MavHandoverStep.EstablishGravityProvider;
                result.error = "could not establish exactly one gravity provider: " + error;
                return RollBack(target, result);
            }

            // ---- 7. enable replacement ----
            result.reachedStep = MavHandoverStep.EnableReplacementWriter;
            if (!target.TryEnableReplacementWriter(out error))
            {
                result.failedAtStep = MavHandoverStep.EnableReplacementWriter;
                result.error = "could not enable the replacement writer: " + error;
                return RollBack(target, result);
            }

            // ---- 8. commit ----
            result.reachedStep = MavHandoverStep.Commit;
            if (!target.TryCommit(out error))
            {
                result.failedAtStep = MavHandoverStep.Commit;
                result.error = "commit refused: " + error;
                return RollBack(target, result);
            }

            // Mass must still be what it was. Checked again AFTER the handover, because
            // InitializeReplacement is exactly where a mass-properties swap would happen.
            if (System.Math.Abs(target.CurrentMassKg - target.CapturedMassKg)
                > MassInvarianceToleranceKg)
            {
                result.failedAtStep = MavHandoverStep.Commit;
                result.error = "the handover changed the aircraft mass from "
                               + target.CapturedMassKg.ToString("F2") + " to "
                               + target.CurrentMassKg.ToString("F2") + " kg";
                return RollBack(target, result);
            }

            result.reachedStep = MavHandoverStep.Committed;
            result.committed = true;
            result.error = "committed";
            return result;
        }

        private static MavHandoverResult RollBack(IMavHandoverTarget target, MavHandoverResult result)
        {
            target.Rollback(result.error);
            result.rolledBack = true;
            result.committed = false;
            return result;
        }

        /// <summary>The declared mass for a policy, kg.</summary>
        public static float MassForPolicy(MavF16MassPolicy policy)
        {
            switch (policy)
            {
                case MavF16MassPolicy.Reference:
                    return F16.MavF16MassReference.MassKg;
                case MavF16MassPolicy.LegacyGameplay:
                    return 9800f;
                default:
                    return 0f;
            }
        }
    }
}
