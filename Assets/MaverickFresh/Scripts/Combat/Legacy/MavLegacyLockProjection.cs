namespace MaverickFresh.Combat.Legacy
{
    /// <summary>
    /// Projects each legacy lock authority's observable state into the ONE lock vocabulary.
    ///
    /// WHY THIS EXISTS. The migration order is: legacy lock state, project into track vocabulary,
    /// validate equivalence, introduce one authority, migrate consumers, only then retire the legacy
    /// path. This file is the third step made checkable. Until the legacy semantics are written down
    /// somewhere a test can read, "the new authority represents what the old ones did" is an opinion.
    ///
    /// PURE ON PURPOSE. Every method takes primitives, not components. That means the mapping can be
    /// asserted exhaustively without building a scene, without a radar, and without instantiating the
    /// legacy components at all - which matters because two of them read input and search the scene in
    /// their own Update. A mapping that can only be tested by running the thing it describes is not
    /// much of a check.
    ///
    /// NOT A CONVERTER. Nothing here drives, clears or writes to any legacy authority. It answers one
    /// question - "if the legacy system is in this state, what would the new vocabulary call it" - and
    /// the answer is used for diagnostics and for equivalence assertions, never to make a decision.
    /// </summary>
    public static class MavLegacyLockProjection
    {
        /// <summary>
        /// `MavF22SensorSuite`: a selection plus an STT timer.
        ///
        /// Legacy semantics, read from the source: `lockTimer` accumulates while the mode is STT or
        /// TWS and decays at 1.5x otherwise; `debugHasLock` is `lockTimer / sttLockTime >= 1`; losing
        /// the selected target from `contacts` zeroes both immediately.
        ///
        /// This is the only legacy authority with an acquisition phase at all, which is why its
        /// projection is the only one that can produce <see cref="MavLockState.Acquiring"/>. It never
        /// produces <see cref="MavLockState.Coasting"/>: the legacy suite drops a lock on the first
        /// sweep that does not contain the target, so there is no gap-holding behavior to represent.
        /// </summary>
        /// <param name="hasSelection">A selected target that is still among the suite's contacts.</param>
        /// <param name="lockProgress01">The suite's own `debugLockProgress01`.</param>
        /// <param name="hasLock">The suite's own `debugHasLock`.</param>
        public static MavLockState ProjectSensorSuite(bool hasSelection, float lockProgress01, bool hasLock)
        {
            if (!hasSelection)
                return MavLockState.Idle;
            if (hasLock)
                return MavLockState.Locked;
            if (lockProgress01 > 0f)
                return MavLockState.Acquiring;
            return MavLockState.Selected;
        }

        /// <summary>
        /// `MavTargetingPodSystem`: an instantaneous toggle, with two kinds of lock.
        ///
        /// Legacy semantics: `ToggleLock` locks whatever the pod is looking at with no acquisition
        /// time; `lockedToTarget` distinguishes a lock onto a `MavCASTarget` from a lock onto a bare
        /// ground point; when a locked target dies, `UpdateLock` clears `lockedToTarget` but leaves
        /// `isLocked` set, so a target lock DEGRADES INTO A POINT LOCK rather than ending.
        ///
        /// A point lock projects to <see cref="MavLockState.Idle"/>, which is the honest answer rather
        /// than a convenient one: the track owner has no track for a patch of ground, so there is no
        /// track id to express such a lock in, and inventing one would mean this projection could
        /// create tracks. Point locks are real behavior that the track vocabulary deliberately does not
        /// cover, and saying so is better than quietly widening what a track means.
        /// </summary>
        public static MavLockState ProjectTargetingPod(bool isLocked, bool lockedToTarget, bool targetPresent)
        {
            if (isLocked && lockedToTarget && targetPresent)
                return MavLockState.Locked;
            return MavLockState.Idle;
        }

        /// <summary>
        /// `MavCASTargetingSystem`: a designation.
        ///
        /// Legacy semantics: designation is instantaneous, has no timer, and never expires. The field
        /// is not even cleared when the target dies - callers notice by checking `IsAlive()` at read
        /// time - and only `ClearDesignation` or designating something else ends it.
        ///
        /// A designation IS a maintained commitment, so it projects to
        /// <see cref="MavLockState.Locked"/> when it names a live target. A designation of a bare point
        /// projects to <see cref="MavLockState.Idle"/> for the same reason a pod point lock does.
        ///
        /// Note what is missing on the legacy side: there is no state between "nothing designated" and
        /// "designated". <see cref="MavLockState.Selected"/> and <see cref="MavLockState.Acquiring"/>
        /// have no counterpart here, and that asymmetry is not a projection defect - it is the thing
        /// the new vocabulary adds.
        /// </summary>
        public static MavLockState ProjectCasDesignation(bool hasDesignatedTarget, bool targetAlive)
        {
            if (hasDesignatedTarget && targetAlive)
                return MavLockState.Locked;
            return MavLockState.Idle;
        }

        /// <summary>
        /// Whether a projected legacy state is one the legacy authority can actually reach.
        ///
        /// Used by equivalence validation to assert the SHAPE of each legacy authority rather than only
        /// individual cases: the sensor suite can reach four of the five states, the pod and the
        /// designation only two. If a projection ever returned <see cref="MavLockState.Coasting"/> for
        /// a legacy authority, that would mean the mapping had started inventing behavior, because not
        /// one of the three holds a lock through a gap.
        /// </summary>
        public static bool IsReachableByLegacy(MavLegacyLockAuthorityKind kind, MavLockState state)
        {
            switch (kind)
            {
                case MavLegacyLockAuthorityKind.SensorSuiteStt:
                    return state == MavLockState.Idle
                           || state == MavLockState.Selected
                           || state == MavLockState.Acquiring
                           || state == MavLockState.Locked;

                case MavLegacyLockAuthorityKind.TargetingPod:
                case MavLegacyLockAuthorityKind.CasDesignation:
                    return state == MavLockState.Idle || state == MavLockState.Locked;
            }

            return false;
        }
    }

    /// <summary>Which legacy authority a projection is about.</summary>
    public enum MavLegacyLockAuthorityKind
    {
        SensorSuiteStt = 0,
        TargetingPod = 1,
        CasDesignation = 2,
    }
}
