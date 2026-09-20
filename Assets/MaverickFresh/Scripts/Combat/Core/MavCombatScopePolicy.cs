namespace MaverickFresh.Combat
{
    /// <summary>
    /// Which parts of the combat stack are in the ACTIVE gameplay path, in one place.
    ///
    /// Combat development is air-to-air first. Air-to-ground is NOT deleted - every CAS, targeting-pod and
    /// A2G weapon source file is still here, still compiles, and still carries its own logic. It is
    /// QUARANTINED: removed from the active runtime so that the A2A lock and fire-control design can be
    /// built without CAS, TGP and bare-world-point semantics leaking into it.
    ///
    /// WHY A POLICY AND NOT A SERIALIZED FLAG. Every A2G system already has serialized switches -
    /// <c>setupOnAwake</c>, <c>installCASStarter</c>, <c>allowAutoDesignate</c> - and they are all `true`
    /// in the scenes that exist today. Freezing by editing those values would put the decision in scene
    /// data, where it is invisible in review, easy to flip by accident, and impossible to assert. So the
    /// decision lives here, in code, and each A2G entry point consults it and fails closed. A scene that
    /// still has <c>setupOnAwake = true</c> is now harmless.
    ///
    /// NO RUNTIME SETTER, deliberately. Nothing - not a scene, not a stray script, not a debug menu - can
    /// re-enable A2G while this is what it is. Restoring A2G is a source change on one line, reviewed like
    /// any other, and it is what <c>Docs/Combat/A2A_ONLY_COMBAT_FREEZE_R0.md</c> describes.
    ///
    /// NOT A FEATURE FLAG SYSTEM. One decision, not a framework. If a second scope question appears it can
    /// join it here; inventing a general mechanism for one answer would be the same mistake as inventing a
    /// designation authority before anything designates.
    /// </summary>
    public static class MavCombatScopePolicy
    {
        /// <summary>
        /// Air-to-ground is frozen: dormant, preserved, and barred from the active runtime.
        ///
        /// `static readonly` rather than `const` so that the guards which read it are ordinary runtime
        /// branches. A `const` would let the compiler fold them away and report the far side as
        /// unreachable, which would turn every fail-closed guard into a warning and, worse, make the
        /// barriers invisible to anything that inspects the compiled code.
        /// </summary>
        public static readonly bool AirToGroundFrozen = true;

        /// <summary>
        /// Whether an A2G system may install, enable or act. The form every barrier is written against, so
        /// there is one question being asked rather than a scattering of negations.
        /// </summary>
        public static bool AirToGroundAllowed
        {
            get { return !AirToGroundFrozen; }
        }

        /// <summary>Recorded by A2G weapons and designators when they refuse. Never shown as a state.</summary>
        public const string FrozenEventSuffix = "_a2g_frozen";

        /// <summary>
        /// The label the developer HUD puts in front of dormant A2G readouts.
        ///
        /// Dormant state may still be shown to a developer - it is the legacy evidence the migration is
        /// measured against - but it must never read as live gameplay authority, which is exactly what an
        /// unlabelled `TGP LOCK` line would do.
        /// </summary>
        public const string DormantLabel = "A2G DISABLED";

        /// <summary>
        /// The label for legacy A2A state that is still computed but is NOT the authority - today, the
        /// F-22 sensor suite's STT lock.
        ///
        /// STT is kept running on purpose: its projection into the engagement view is the evidence the lock
        /// migration is judged by. But it is a SHADOW, and a `LOCK` caption that does not say so invites a
        /// reader - or a pilot - to treat it as the aircraft's lock, which is the specific confusion the
        /// authoritative lock exists to end.
        /// </summary>
        public const string ShadowLabel = "SHADOW";
    }
}
