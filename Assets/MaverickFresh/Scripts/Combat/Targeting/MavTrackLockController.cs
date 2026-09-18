using UnityEngine;

namespace MaverickFresh.Combat.Targeting
{
    /// <summary>
    /// The authoritative lock: track selection, acquisition, maintenance, coast and loss.
    ///
    /// It reads tracks from <see cref="MavTargetTrackOwner"/> and decides commitment. It does not
    /// detect, does not scan, does not touch a sensor, and does not read input - a command source or an
    /// AI asks it for a selection, and it answers with a lifecycle.
    ///
    /// WHY LOCK IS SEPARATE FROM DETECTION. A sensor answers "what can I see"; a lock answers "what am
    /// I committed to". The second involves time, hysteresis and a decision that deliberately survives
    /// a missed observation. The legacy stack merged them - MavF22SensorSuite scans, times its own STT
    /// lock, reads its own keys and writes HUD state in one component - which is exactly why there was
    /// no single answer to what the aircraft was engaging.
    ///
    /// WHAT MAKES Locked MEAN SOMETHING. <see cref="MavTrackQuality.Locked"/> is asserted HERE, through
    /// <see cref="EffectiveQualityOf"/>, and never by a producer. A sensor that reported Locked would be
    /// claiming an engagement decision it plays no part in; Radar Core asserts the opposite of itself in
    /// its own validation. The track owner's stored quality is left untouched, so no producer's
    /// measurement is rewritten - the lock is expressed as a property of the engagement, layered over
    /// the measurement rather than overwriting it.
    ///
    /// Deliberately NOT here, because they are later phases: missile guidance, seekers, autopilots,
    /// datalink fusion, ECM, and any weapon at all. This class knows nothing about what a lock is FOR.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MavTrackLockController : MonoBehaviour, IMavTrackLockAuthority
    {
        [Header("Enable")]
        [Tooltip("Master switch. When off the authority reports Idle and never locks.")]
        public bool enableLockAuthority = true;

        [Header("Wiring")]
        [Tooltip("Track source. Resolved from this GameObject when left empty.")]
        public MavTargetTrackOwner owner;

        [Tooltip("Optional engagement view to publish the authoritative lock into.")]
        public MavEngagementView engagementView;

        [Header("Selection")]
        [Tooltip("Automatically select the best available track when nothing is selected.")]
        public bool autoSelectBestTrack = true;

        [Tooltip("Minimum quality a track needs before it can be selected at all.")]
        public MavTrackQuality minimumSelectableQuality = MavTrackQuality.Coarse;

        [Header("Lock lifecycle (SYNTHETIC gameplay values)")]
        [Tooltip("Seconds a selected track must be held continuously before the lock is established.")]
        public float acquisitionSeconds = 1.15f;

        [Tooltip("Minimum quality required to establish and hold a lock.")]
        public MavTrackQuality minimumLockQuality = MavTrackQuality.Tracked;

        [Tooltip("A track unobserved for longer than this is stale for lock purposes.")]
        public float staleTrackSeconds = 0.75f;

        [Tooltip("Seconds a lock is held through a stale track before it is lost.")]
        public float coastSeconds = 2.0f;

        [Header("Read-only state")]
        public MavLockState debugLockState = MavLockState.Idle;
        public int debugSelectedTrackId;
        public int debugLockedTrackId;
        public float debugAcquisitionProgress01;
        public float debugTimeInLock;
        public float debugCoastElapsed;
        public MavLockLossReason debugLastLossReason = MavLockLossReason.None;
        public int debugLocksEstablished;
        public int debugLocksLost;
        public string debugLockedTrackName = "none";

        private int selectedTrackId;
        private int lockedTrackId;
        private MavLockState state = MavLockState.Idle;
        private float acquisitionElapsed;
        private float lockElapsed;
        private float coastElapsed;
        private MavLockLossReason lastLossReason = MavLockLossReason.None;

        private bool useTestClock;
        private float testClockSeconds;

        /// <summary>
        /// Clock. Lock semantics are entirely about elapsed time, so validation has to be able to move
        /// time itself - editor time does not advance between calls. Unset in every normal run.
        /// </summary>
        private float CurrentTime
        {
            get { return useTestClock ? testClockSeconds : Time.time; }
        }

        public bool IsLockAuthorityActive
        {
            get { return enableLockAuthority && isActiveAndEnabled && owner != null; }
        }

        public MavLockState LockState
        {
            get { return state; }
        }

        public float TimeInLockSeconds
        {
            get { return state == MavLockState.Locked || state == MavLockState.Coasting ? lockElapsed : 0f; }
        }

        public MavLockLossReason LastLossReason
        {
            get { return lastLossReason; }
        }

        /// <summary>The track currently selected, whether or not it is locked. Zero when none.</summary>
        public int SelectedTrackId
        {
            get { return selectedTrackId; }
        }

        /// <summary>The locked track, including while coasting. Zero when not locked.</summary>
        public int LockedTrackId
        {
            get { return state == MavLockState.Locked || state == MavLockState.Coasting ? lockedTrackId : 0; }
        }

        /// <summary>How far through acquisition, 0..1. Zero unless acquiring.</summary>
        public float AcquisitionProgress01
        {
            get
            {
                if (state != MavLockState.Acquiring)
                    return state == MavLockState.Locked || state == MavLockState.Coasting ? 1f : 0f;
                return Mathf.Clamp01(acquisitionElapsed / Mathf.Max(0.01f, acquisitionSeconds));
            }
        }

        public bool TryGetLockedTrack(out MavTargetTrackData track)
        {
            track = MavTargetTrackData.Invalid;
            if (owner == null)
                return false;
            if (state != MavLockState.Locked && state != MavLockState.Coasting)
                return false;
            return owner.TryGetTrackById(lockedTrackId, out track);
        }

        /// <summary>
        /// The quality of a track as the ENGAGEMENT sees it.
        ///
        /// This is where <see cref="MavTrackQuality.Locked"/> acquires concrete meaning: the locked
        /// track reports Locked, and every other track reports exactly what its producer measured. The
        /// owner's stored data is not modified, so no measurement is ever rewritten - a consumer that
        /// wants the raw sensor answer still gets it from the owner.
        /// </summary>
        public MavTrackQuality EffectiveQualityOf(int trackId)
        {
            MavTargetTrackData track;
            if (owner == null || trackId == 0 || !owner.TryGetTrackById(trackId, out track))
                return MavTrackQuality.None;

            if ((state == MavLockState.Locked || state == MavLockState.Coasting) && trackId == lockedTrackId)
                return MavTrackQuality.Locked;

            return track.quality;
        }

        /// <summary>
        /// Asks for a specific track. Selection is intent, not a lock: acquisition still has to run.
        ///
        /// Selecting a different track while locked abandons the lock, recorded as
        /// <see cref="MavLockLossReason.SelectionChanged"/>. Re-selecting the track already selected is
        /// a no-op rather than a restart, so a command source repeating itself does not reset progress.
        /// </summary>
        public bool RequestSelection(int trackId)
        {
            if (!IsLockAuthorityActive)
                return false;

            if (trackId == 0)
            {
                BreakLock(MavLockLossReason.Commanded);
                return true;
            }

            MavTargetTrackData track;
            if (!owner.TryGetTrackById(trackId, out track) || !track.IsValid)
                return false;
            if (track.quality < minimumSelectableQuality)
                return false;

            if (trackId == selectedTrackId)
                return true;

            if (state == MavLockState.Locked || state == MavLockState.Coasting)
                RecordLoss(MavLockLossReason.SelectionChanged);

            selectedTrackId = trackId;
            lockedTrackId = 0;
            acquisitionElapsed = 0f;
            lockElapsed = 0f;
            coastElapsed = 0f;
            state = MavLockState.Selected;
            return true;
        }

        /// <summary>Deliberately abandons any selection and lock.</summary>
        public void BreakLock(MavLockLossReason reason)
        {
            if (state == MavLockState.Locked || state == MavLockState.Coasting)
                RecordLoss(reason);

            selectedTrackId = 0;
            lockedTrackId = 0;
            acquisitionElapsed = 0f;
            lockElapsed = 0f;
            coastElapsed = 0f;
            state = MavLockState.Idle;
        }

        private void OnEnable()
        {
            if (owner == null)
                owner = GetComponent<MavTargetTrackOwner>();
            if (engagementView == null)
                engagementView = GetComponent<MavEngagementView>();
        }

        private void Update()
        {
            Step(Time.deltaTime);
        }

        /// <summary>
        /// One lifecycle step. Public so validation can drive it with an explicit delta instead of
        /// waiting on frames.
        /// </summary>
        public void Step(float deltaSeconds)
        {
            if (!IsLockAuthorityActive)
            {
                if (state != MavLockState.Idle)
                    BreakLock(MavLockLossReason.AuthorityUnavailable);
                PublishDebug();
                return;
            }

            float now = CurrentTime;

            if (autoSelectBestTrack && selectedTrackId == 0)
                SelectBestTrack();
            else if (autoSelectBestTrack && state == MavLockState.Selected)
                ReconsiderStalledSelection();

            if (selectedTrackId == 0)
            {
                state = MavLockState.Idle;
                PublishDebug();
                return;
            }

            MavTargetTrackData selected;
            if (!owner.TryGetTrackById(selectedTrackId, out selected) || !selected.IsValid)
            {
                // The track is gone entirely. A lock in progress or held cannot survive that.
                bool wasCommitted = state == MavLockState.Locked || state == MavLockState.Coasting;
                selectedTrackId = 0;
                lockedTrackId = 0;
                acquisitionElapsed = 0f;
                if (wasCommitted)
                    RecordLoss(MavLockLossReason.TrackDropped);
                lockElapsed = 0f;
                coastElapsed = 0f;
                state = MavLockState.Idle;
                PublishDebug();
                return;
            }

            float age = selected.AgeAt(now);
            bool stale = age > Mathf.Max(0.01f, staleTrackSeconds);
            bool qualityOkForLock = selected.quality >= minimumLockQuality;

            switch (state)
            {
                case MavLockState.Idle:
                case MavLockState.Selected:
                    state = MavLockState.Selected;
                    acquisitionElapsed = 0f;
                    // Acquisition only begins once the track is both fresh and good enough. Starting the
                    // timer on a stale or poor track would let a lock be built out of nothing.
                    if (!stale && qualityOkForLock)
                        state = MavLockState.Acquiring;
                    break;

                case MavLockState.Acquiring:
                    if (stale || !qualityOkForLock)
                    {
                        // Acquisition is all-or-nothing: an interrupted attempt restarts rather than
                        // resuming, so a target flickering in and out cannot accumulate a lock.
                        acquisitionElapsed = 0f;
                        state = MavLockState.Selected;
                        break;
                    }

                    acquisitionElapsed += Mathf.Max(0f, deltaSeconds);
                    if (acquisitionElapsed >= Mathf.Max(0.01f, acquisitionSeconds))
                    {
                        lockedTrackId = selectedTrackId;
                        lockElapsed = 0f;
                        coastElapsed = 0f;
                        state = MavLockState.Locked;
                        debugLocksEstablished++;
                        lastLossReason = MavLockLossReason.None;
                    }
                    break;

                case MavLockState.Locked:
                    lockElapsed += Mathf.Max(0f, deltaSeconds);

                    if (!qualityOkForLock)
                    {
                        RecordLoss(MavLockLossReason.QualityTooLow);
                        DemoteToSelected();
                        break;
                    }

                    if (stale)
                    {
                        // Hold the commitment through the gap rather than dropping it on one missed
                        // observation. That is what a coast is for.
                        coastElapsed = 0f;
                        state = MavLockState.Coasting;
                    }
                    break;

                case MavLockState.Coasting:
                    lockElapsed += Mathf.Max(0f, deltaSeconds);
                    coastElapsed += Mathf.Max(0f, deltaSeconds);

                    if (!stale)
                    {
                        // A fresh observation arrived: the lock resumes without re-acquiring, because
                        // the commitment was never abandoned.
                        coastElapsed = 0f;
                        state = qualityOkForLock ? MavLockState.Locked : MavLockState.Coasting;
                        if (!qualityOkForLock)
                        {
                            RecordLoss(MavLockLossReason.QualityTooLow);
                            DemoteToSelected();
                        }
                        break;
                    }

                    if (coastElapsed >= Mathf.Max(0.01f, coastSeconds))
                    {
                        RecordLoss(MavLockLossReason.CoastExpired);
                        DemoteToSelected();
                    }
                    break;
            }

            PublishDebug();
        }

        /// <summary>
        /// Drops to Selected after losing a lock, keeping the selection.
        ///
        /// The selection survives on purpose: losing a lock is not the same as losing interest, and an
        /// operator who was engaging something usually wants to re-acquire it rather than start over.
        /// </summary>
        private void DemoteToSelected()
        {
            lockedTrackId = 0;
            acquisitionElapsed = 0f;
            lockElapsed = 0f;
            coastElapsed = 0f;
            state = MavLockState.Selected;
        }

        private void RecordLoss(MavLockLossReason reason)
        {
            lastLossReason = reason;
            debugLocksLost++;
        }

        /// <summary>
        /// Picks the best track available: highest quality, nearest as the tie-break.
        ///
        /// Deliberately simple and documented rather than clever. Threat evaluation is a later concern,
        /// and a rule nobody can predict is worse than a plain one.
        /// </summary>
        private void SelectBestTrack()
        {
            int bestId = 0;
            MavTrackQuality bestQuality = MavTrackQuality.None;
            float bestRange = float.MaxValue;
            Vector3 here = transform.position;

            for (int i = 0; i < owner.TrackCount; i++)
            {
                MavTargetTrackData t = owner.GetTrack(i);
                if (!t.IsValid || t.quality < minimumSelectableQuality)
                    continue;

                float range = Vector3.Distance(here, t.position);
                if (t.quality > bestQuality || (t.quality == bestQuality && range < bestRange))
                {
                    bestId = t.trackId;
                    bestQuality = t.quality;
                    bestRange = range;
                }
            }

            if (bestId != 0)
            {
                selectedTrackId = bestId;
                acquisitionElapsed = 0f;
                state = MavLockState.Selected;
            }
        }

        /// <summary>
        /// Re-picks the automatic selection when the current one cannot reach a lock and another can.
        ///
        /// WHY THIS IS NEEDED. Automatic selection used to run only while nothing was selected, so the
        /// FIRST lock-capable-looking track latched forever. With the legacy scene-sweep feed live -
        /// and it always is - that feed reports Coarse and nothing better, while
        /// <see cref="minimumLockQuality"/> is Tracked. So a legacy track could be selected at startup
        /// and the controller would sit in Selected indefinitely, never acquiring, while a radar track
        /// good enough to lock went unlooked-at. Nothing was logged, because nothing had failed: it was
        /// a selection that simply could never progress.
        ///
        /// Deliberately narrow. It runs ONLY in <see cref="MavLockState.Selected"/> and ONLY when the
        /// current selection is below the lock threshold, so it can never interrupt acquisition, a
        /// lock, or a coast - and it never overrides an explicit request, because an explicit request
        /// leaves the state machine in Selected only until the track it named becomes lockable. A rule
        /// that could re-pick mid-acquisition would make acquisition timing unpredictable, which is the
        /// opposite of what this phase is for.
        /// </summary>
        private void ReconsiderStalledSelection()
        {
            MavTargetTrackData current;
            if (!owner.TryGetTrackById(selectedTrackId, out current))
                return;
            if (current.quality >= minimumLockQuality)
                return;   // it can still get there; leave it alone

            // Only move for a track that is actually lock-capable right now. Swapping one unlockable
            // track for another would just churn the selection.
            int bestId = 0;
            MavTrackQuality bestQuality = MavTrackQuality.None;
            float bestRange = float.MaxValue;
            Vector3 here = transform.position;

            for (int i = 0; i < owner.TrackCount; i++)
            {
                MavTargetTrackData t = owner.GetTrack(i);
                if (!t.IsValid || t.quality < minimumLockQuality)
                    continue;

                float range = Vector3.Distance(here, t.position);
                if (t.quality > bestQuality || (t.quality == bestQuality && range < bestRange))
                {
                    bestId = t.trackId;
                    bestQuality = t.quality;
                    bestRange = range;
                }
            }

            if (bestId != 0 && bestId != selectedTrackId)
            {
                selectedTrackId = bestId;
                acquisitionElapsed = 0f;
            }
        }

        private void PublishDebug()
        {
            debugLockState = state;
            debugSelectedTrackId = selectedTrackId;
            debugLockedTrackId = LockedTrackId;
            debugAcquisitionProgress01 = AcquisitionProgress01;
            debugTimeInLock = TimeInLockSeconds;
            debugCoastElapsed = state == MavLockState.Coasting ? coastElapsed : 0f;
            debugLastLossReason = lastLossReason;
            debugLockedTrackName = owner != null && LockedTrackId != 0
                ? owner.GetTrackDisplayName(LockedTrackId)
                : "none";

            if (engagementView != null)
                engagementView.PublishAuthoritativeLock(LockedTrackId, state, debugLockedTrackName);
        }

        /// <summary>Test seam: drives lifecycle timing from an explicit clock.</summary>
        public void UseTestClock(float seconds)
        {
            useTestClock = true;
            testClockSeconds = seconds;
        }

        /// <summary>Test seam: advances the test clock.</summary>
        public void AdvanceTestClock(float seconds)
        {
            testClockSeconds += seconds;
        }

        /// <summary>Test seam: returns to real time.</summary>
        public void ClearTestClock()
        {
            useTestClock = false;
        }
    }
}
