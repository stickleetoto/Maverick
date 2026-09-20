using UnityEngine;

namespace MaverickFresh.Combat.Targeting
{
    /// <summary>
    /// Which legacy authority is pointing at which track, in one place.
    ///
    /// Consolidating the three competing lock authorities (issue #16), in the order express, observe,
    /// then move.
    ///
    /// EXPRESS and OBSERVE were TargetTrack Core R0: CAS designation, the sensor suite's STT lock and
    /// the pod's lock are each projected into one vocabulary - track ids from
    /// <see cref="MavTargetTrackOwner"/> - and their disagreement became visible instead of being
    /// buried in three components.
    ///
    /// MOVE has been PREPARED here, not performed. <see cref="authoritativeLockTrackId"/> carries the
    /// claim of the one real lock authority, <see cref="MavTrackLockController"/>, and
    /// <see cref="PrimaryTrackId"/> can be made to prefer it - but
    /// <see cref="preferAuthoritativeLock"/> is OFF by default, so the answer consumers get is still
    /// the legacy one. Consumers migrate first; the default moves after.
    ///
    /// The legacy authorities still run and still own their own state. They are NOT deleted, because
    /// their behavior has to be demonstrably represented by the new path before removing them can be
    /// called safe. <see cref="legacyDisagreesWithAuthoritative"/> is the signal that says when that
    /// is true.
    ///
    /// Read-only for consumers. A publisher fills it; nothing here decides anything.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MavEngagementView : MonoBehaviour
    {
        [Header("Projected legacy authorities (read-only)")]
        [Tooltip("Track the CAS designation currently points at, or 0.")]
        public int designatedTrackId;

        [Tooltip("Track the sensor suite currently holds, or 0.")]
        public int sensorLockTrackId;

        [Tooltip("Track the targeting pod currently holds, or 0.")]
        public int podLockTrackId;

        [Header("Authoritative lock (Radar track/lock R0)")]
        /// <summary>
        /// The track the ONE authoritative lock authority holds, or 0.
        ///
        /// This is the answer that is meant to win. The three legacy projections above are still
        /// published because their systems still run, and they are kept visible precisely so the
        /// migration can be watched rather than assumed - but they are no longer the best answer
        /// available.
        /// </summary>
        [Tooltip("Track held by the authoritative lock authority, or 0.")]
        public int authoritativeLockTrackId;

        [Tooltip("Lifecycle state of the authoritative lock.")]
        public MavLockState authoritativeLockState = MavLockState.Idle;

        [Tooltip("Display name of the authoritatively locked track.")]
        public string debugAuthoritativeLockName = "none";

        /// <summary>
        /// Whether the authoritative lock is allowed to win <see cref="PrimaryTrackId"/>.
        ///
        /// The migration switch, in one place, so moving authority is a decision rather than a side
        /// effect of a class existing.
        ///
        /// DEFAULTS TO LEGACY - false. While it is off, <see cref="PrimaryTrackId"/> answers exactly
        /// what it answered before this phase: sensor STT lock, then pod lock, then designation. The
        /// authoritative lock is still computed, still published and still visible; it simply does not
        /// win yet.
        ///
        /// WHY OFF. The migration order is legacy authority, project and observe, validate equivalence,
        /// introduce the new authority, MIGRATE CONSUMERS, then switch the default, and only then
        /// retire the legacy owners. Consumers have not migrated. Making the new path the default
        /// before they have would move authority ahead of the step that proves the move is safe, and
        /// the fact that no consumer reads this property today is not a reason to get the order wrong -
        /// the first one that does would inherit a default nobody had validated against it.
        ///
        /// Turning it on is the deliberate, reversible opt-in, and it is asserted in both positions.
        /// </summary>
        [Tooltip("Off by default: PrimaryTrackId uses the legacy order. On: the authoritative lock wins.")]
        public bool preferAuthoritativeLock = false;

        [Header("Legacy lock states, projected (diagnostics)")]
        /// <summary>
        /// Each legacy authority's own state, expressed in the new vocabulary by
        /// <c>MavLegacyLockProjection</c>.
        ///
        /// The track ids above say WHAT each authority points at. These say WHERE each one is in the
        /// lifecycle, which is what makes equivalence checkable instead of merely assertable: an STT
        /// lock at 60% progress is Acquiring, and a pod point lock is Idle because it has no track to
        /// be about. Publishing them costs nothing and turns the migration's central claim - that the
        /// new authority can represent what the old ones do - into something observable at runtime.
        /// </summary>
        [Tooltip("The sensor suite's STT progress expressed as a lock state.")]
        public MavLockState legacySensorLockState = MavLockState.Idle;

        [Tooltip("The pod's lock expressed as a lock state. A bare point lock reads Idle.")]
        public MavLockState legacyPodLockState = MavLockState.Idle;

        [Tooltip("The CAS designation expressed as a lock state. A bare point designation reads Idle.")]
        public MavLockState legacyDesignationLockState = MavLockState.Idle;

        /// <summary>
        /// True when a legacy authority claims a different track than the authoritative lock.
        ///
        /// The migration signal, and the evidence for retiring the legacy owners: while this stays false
        /// in practice, the legacy authorities are agreeing with the new one and can be removed without
        /// changing behavior; while it is true, something still disagrees and deleting the legacy path
        /// would change what the aircraft does.
        ///
        /// INDEPENDENT OF <see cref="preferAuthoritativeLock"/>, because precedence and disagreement are
        /// different questions. The switch decides which answer consumers get; this says whether the two
        /// answers differ. It was originally computed only while the switch was ON, which made it
        /// unobservable during the one period it is for - the pre-switch window when the evidence is
        /// gathered - so the signal could only confirm a move that had already been made.
        ///
        /// DIAGNOSTIC ONLY. Reading or raising it selects nothing, locks nothing, creates no track and
        /// does not touch <see cref="PrimaryTrackId"/>. Recomputed by <see cref="RefreshDisagreement"/>,
        /// so it reports the last evaluated state rather than watching the authority continuously.
        /// </summary>
        [Tooltip("A legacy authority claims a different track than the authoritative lock. Diagnostic.")]
        public bool legacyDisagreesWithAuthoritative;

        [Header("Disagreement")]
        [Tooltip("True when two or more authorities point at different non-zero tracks.")]
        public bool authoritiesDisagree;

        [Tooltip("How many authorities currently claim something.")]
        public int claimingAuthorityCount;

        [Header("Diagnostics")]
        public string debugDesignatedName = "none";
        public string debugSensorLockName = "none";
        public string debugPodLockName = "none";

        /// <summary>
        /// The single answer.
        ///
        /// BY DEFAULT this is the LEGACY answer, unchanged from before this phase: sensor STT lock,
        /// then pod lock, then CAS designation. That ordering reflects claim strength as the legacy
        /// systems actually use it - an STT lock is a maintained commitment to one object, a pod lock
        /// is a maintained commitment to a ground point, and a designation is a marker that survives
        /// losing sight of the target.
        ///
        /// The AUTHORITATIVE LOCK outranks all three once <see cref="preferAuthoritativeLock"/> is
        /// turned on. It is the only claim produced by a system whose whole job is the lock lifecycle -
        /// acquisition, maintenance, coast and loss - rather than by a component that also scans, reads
        /// keys and draws a HUD. Which is why it is meant to win eventually, and why it does not win
        /// yet: consumers have to migrate onto it first.
        ///
        /// The legacy path is not kept out of politeness. Removing it, or defaulting past it, would
        /// change behavior in every case the authoritative lock has not taken over, and this phase
        /// moves authority without changing what the aircraft does.
        /// </summary>
        public int PrimaryTrackId
        {
            get
            {
                if (preferAuthoritativeLock && authoritativeLockTrackId != 0)
                    return authoritativeLockTrackId;
                if (sensorLockTrackId != 0)
                    return sensorLockTrackId;
                if (podLockTrackId != 0)
                    return podLockTrackId;
                return designatedTrackId;
            }
        }

        /// <summary>
        /// Which kind of authority the current <see cref="PrimaryTrackId"/> came from. Diagnostic: it
        /// makes the migration's progress readable at a glance.
        /// </summary>
        public string PrimarySourceName
        {
            get
            {
                if (preferAuthoritativeLock && authoritativeLockTrackId != 0) return "authoritative-lock";
                if (sensorLockTrackId != 0) return "legacy-sensor-stt";
                if (podLockTrackId != 0) return "legacy-pod-lock";
                if (designatedTrackId != 0) return "legacy-cas-designation";
                return "none";
            }
        }

        /// <summary>Publishes the authoritative lock. Called by the lock authority itself.</summary>
        public void PublishAuthoritativeLock(int trackId, MavLockState lockState, string displayName)
        {
            authoritativeLockTrackId = trackId;
            authoritativeLockState = lockState;
            debugAuthoritativeLockName = string.IsNullOrEmpty(displayName) ? "none" : displayName;
        }

        /// <summary>
        /// Publishes the three legacy authorities' own lifecycle states. Called by the legacy probe.
        ///
        /// Separate from the track-id publishers on purpose: those have consumers and assertions
        /// already, and widening their signatures to carry a state would have meant editing a contract
        /// that is closed and passing.
        /// </summary>
        public void PublishLegacyLockStates(MavLockState designation, MavLockState sensor, MavLockState pod)
        {
            legacyDesignationLockState = designation;
            legacySensorLockState = sensor;
            legacyPodLockState = pod;
        }

        /// <summary>Publishes the CAS designation projection. Called by the legacy probe.</summary>
        public void PublishDesignation(int trackId, string displayName)
        {
            designatedTrackId = trackId;
            debugDesignatedName = string.IsNullOrEmpty(displayName) ? "none" : displayName;
        }

        public void PublishSensorLock(int trackId, string displayName)
        {
            sensorLockTrackId = trackId;
            debugSensorLockName = string.IsNullOrEmpty(displayName) ? "none" : displayName;
        }

        public void PublishPodLock(int trackId, string displayName)
        {
            podLockTrackId = trackId;
            debugPodLockName = string.IsNullOrEmpty(displayName) ? "none" : displayName;
        }

        /// <summary>
        /// Recomputes the disagreement summary. Separate from the publish calls so a publisher can
        /// set all three and evaluate once, rather than reporting disagreement against a half-updated
        /// view.
        /// </summary>
        public void RefreshDisagreement()
        {
            int claims = 0;
            if (designatedTrackId != 0) claims++;
            if (sensorLockTrackId != 0) claims++;
            if (podLockTrackId != 0) claims++;
            claimingAuthorityCount = claims;

            bool disagree = false;
            if (designatedTrackId != 0 && sensorLockTrackId != 0 && designatedTrackId != sensorLockTrackId)
                disagree = true;
            if (designatedTrackId != 0 && podLockTrackId != 0 && designatedTrackId != podLockTrackId)
                disagree = true;
            if (sensorLockTrackId != 0 && podLockTrackId != 0 && sensorLockTrackId != podLockTrackId)
                disagree = true;

            authoritiesDisagree = disagree;

            // Migration signal: does anything legacy still claim a different track than the authority?
            //
            // Deliberately NOT gated on preferAuthoritativeLock. Precedence and disagreement are two
            // different questions: the switch decides WHICH answer consumers get, this field reports
            // WHETHER the two available answers differ. Gating it on the switch made it computable only
            // once the move it was meant to justify had already happened - dead for exactly the period
            // it exists for, the pre-switch window in which the legacy owners still run and the
            // evidence for retiring them is being collected.
            //
            // A zero authoritative id means no committed lock. The publisher is the lock authority
            // itself, publishing LockedTrackId, which is 0 unless a lock is actually held - so there is
            // nothing for legacy to disagree WITH, and absence is not conflict. A zero legacy id is a
            // claim of nothing, for the same reason: an authority that is not claiming anything is not
            // contradicting one that is.
            bool legacyDisagrees = false;
            if (authoritativeLockTrackId != 0)
            {
                if (designatedTrackId != 0 && designatedTrackId != authoritativeLockTrackId)
                    legacyDisagrees = true;
                if (sensorLockTrackId != 0 && sensorLockTrackId != authoritativeLockTrackId)
                    legacyDisagrees = true;
                if (podLockTrackId != 0 && podLockTrackId != authoritativeLockTrackId)
                    legacyDisagrees = true;
            }
            legacyDisagreesWithAuthoritative = legacyDisagrees;
        }

        /// <summary>Clears every projection. Used when the probe loses its sources.</summary>
        public void ClearAll()
        {
            designatedTrackId = 0;
            sensorLockTrackId = 0;
            podLockTrackId = 0;
            debugDesignatedName = "none";
            debugSensorLockName = "none";
            debugPodLockName = "none";
            authoritiesDisagree = false;
            claimingAuthorityCount = 0;
            legacyDisagreesWithAuthoritative = false;
            legacySensorLockState = MavLockState.Idle;
            legacyPodLockState = MavLockState.Idle;
            legacyDesignationLockState = MavLockState.Idle;
        }
    }
}
