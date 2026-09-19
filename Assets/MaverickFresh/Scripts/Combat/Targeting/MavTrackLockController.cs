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
    /// THE AUTHORITY BOUNDARY FAILS CLOSED. When <see cref="IsLockAuthorityActive"/> is false, every
    /// lock-facing accessor reports nothing held - Idle, zero, false, and no promotion to
    /// <see cref="MavTrackQuality.Locked"/> - and the claim published to
    /// <see cref="MavEngagementView"/> is withdrawn. That has to hold even when Update will never run
    /// again, which is why <see cref="OnDisable"/> exists and why the enable switch is a property.
    ///
    /// SELECTION OWNERSHIP. Two things can select a track: a command source, and the automatic rule.
    /// The controller records which, because the difference is not recoverable from the state machine
    /// afterwards and the two have different rights - an automatic selection that cannot reach a lock
    /// may be replaced by one that can, while a commanded selection is held until the command is
    /// withdrawn or its track disappears. See <see cref="SelectionIsExplicit"/>.
    ///
    /// Deliberately NOT here, because they are later phases: missile guidance, seekers, autopilots,
    /// datalink fusion, ECM, and any weapon at all. This class knows nothing about what a lock is FOR.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MavTrackLockController : MonoBehaviour, IMavTrackLockAuthority
    {
        [Header("Enable")]
        [SerializeField]
        [Tooltip("Master switch. When off the authority reports Idle and never locks.")]
        private bool enableLockAuthority = true;

        /// <summary>
        /// The master switch, as a property so that turning it OFF withdraws the claim immediately
        /// rather than at some later frame.
        ///
        /// It is a property and not a public field for one concrete reason: a field write has no hook,
        /// so a lock could be left standing internally while the authority reported itself unavailable.
        /// The accessors below fail closed regardless, but withdrawing at the moment of the decision is
        /// what keeps the internal state and the published state honest with each other.
        ///
        /// An Inspector edit writes the serialized field directly and so bypasses this setter; the
        /// falling-edge check in <see cref="Step"/> catches that on the next update.
        /// </summary>
        public bool EnableLockAuthority
        {
            get { return enableLockAuthority; }
            set
            {
                if (enableLockAuthority == value)
                    return;
                enableLockAuthority = value;
                if (!value)
                {
                    WithdrawAuthority(MavLockLossReason.AuthorityUnavailable);
                    PublishDebug();
                }
            }
        }

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
        public bool debugSelectionIsExplicit;
        public int debugLocksEstablished;
        public int debugLocksLost;
        public string debugLockedTrackName = "none";

        private int selectedTrackId;

        /// <summary>
        /// Whether the current selection was ASKED FOR or merely picked.
        ///
        /// The controller has two ways to arrive in <see cref="MavLockState.Selected"/> - a command
        /// source calling <see cref="RequestSelection"/>, and the automatic rule picking something - and
        /// without this flag those two are indistinguishable afterwards. That mattered: automatic
        /// reconsideration would happily replace a commanded selection whose quality was below the lock
        /// threshold, silently overriding a player, an AI or a FAM command. Selection ownership is not
        /// derivable from the state machine, so it is recorded.
        /// </summary>
        private bool selectionIsExplicit;

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

        /// <summary>
        /// Whether a lock is being held BY AN AVAILABLE AUTHORITY.
        ///
        /// Every lock-facing accessor goes through this rather than testing the state directly, because
        /// an unavailable authority must fail closed. Without it the stored state leaked past the
        /// boundary: a disabled component stops receiving Update, so the inactive path in
        /// <see cref="Step"/> never ran, and a consumer could keep seeing Locked forever while
        /// <see cref="IsLockAuthorityActive"/> said false. "Nothing is holding this" is the only honest
        /// answer an unavailable authority can give.
        /// </summary>
        private bool HasCommittedLock
        {
            get
            {
                return IsLockAuthorityActive
                       && (state == MavLockState.Locked || state == MavLockState.Coasting);
            }
        }

        public MavLockState LockState
        {
            get { return IsLockAuthorityActive ? state : MavLockState.Idle; }
        }

        public float TimeInLockSeconds
        {
            get { return HasCommittedLock ? lockElapsed : 0f; }
        }

        public MavLockLossReason LastLossReason
        {
            get { return lastLossReason; }
        }

        /// <summary>The track currently selected, whether or not it is locked. Zero when none.</summary>
        public int SelectedTrackId
        {
            get { return IsLockAuthorityActive ? selectedTrackId : 0; }
        }

        /// <summary>
        /// True when the current selection was commanded rather than picked automatically.
        ///
        /// A commanded selection is an instruction, not a suggestion: the automatic rule may not take it
        /// away. Exposed so a consumer - and validation - can tell the two apart, because the lock state
        /// alone cannot.
        /// </summary>
        public bool SelectionIsExplicit
        {
            get { return IsLockAuthorityActive && selectedTrackId != 0 && selectionIsExplicit; }
        }

        /// <summary>The locked track, including while coasting. Zero when not locked.</summary>
        public int LockedTrackId
        {
            get { return HasCommittedLock ? lockedTrackId : 0; }
        }

        /// <summary>How far through acquisition, 0..1. Zero unless acquiring.</summary>
        public float AcquisitionProgress01
        {
            get
            {
                if (!IsLockAuthorityActive)
                    return 0f;
                if (state != MavLockState.Acquiring)
                    return state == MavLockState.Locked || state == MavLockState.Coasting ? 1f : 0f;
                return Mathf.Clamp01(acquisitionElapsed / Mathf.Max(0.01f, acquisitionSeconds));
            }
        }

        public bool TryGetLockedTrack(out MavTargetTrackData track)
        {
            track = MavTargetTrackData.Invalid;
            if (!HasCommittedLock)
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

            // Only an AVAILABLE authority may promote a track to Locked. An unavailable one reports the
            // producer's measurement unchanged, which is the honest answer: there is no engagement to
            // layer over it. Returning None instead would destroy information the sensor really has.
            if (HasCommittedLock && trackId == lockedTrackId)
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
            {
                // Re-commanding the track already selected does NOT restart acquisition - a command
                // source repeating itself must not reset progress - but it DOES claim ownership. If the
                // automatic rule had picked this track, an operator naming it makes the selection
                // commanded from now on, and therefore no longer something the automatic rule may
                // replace. Returning early without this was the same defect in a second place.
                selectionIsExplicit = true;
                return true;
            }

            if (state == MavLockState.Locked || state == MavLockState.Coasting)
                RecordLoss(MavLockLossReason.SelectionChanged);

            selectedTrackId = trackId;
            selectionIsExplicit = true;   // commanded: the automatic rule may not take this away
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
            WithdrawAuthority(reason);
        }

        /// <summary>
        /// THE one place the authority gives up what it is holding.
        ///
        /// Every way a claim can end routes through here - a commanded break, the selected track
        /// disappearing, the authority becoming unavailable, and component disable - so there is exactly
        /// one definition of "holding nothing" to get right. Several of those paths used to reset the
        /// same six fields separately, and the authority-unavailable case did not reset them at all.
        ///
        /// A loss is RECORDED only if a lock was actually held. There is nothing to lose from
        /// <see cref="MavLockState.Acquiring"/>, and recording one would make
        /// <see cref="LastLossReason"/> describe something that never happened.
        /// </summary>
        private void WithdrawAuthority(MavLockLossReason reason)
        {
            if (state == MavLockState.Locked || state == MavLockState.Coasting)
                RecordLoss(reason);

            selectedTrackId = 0;
            selectionIsExplicit = false;   // the claim is over; automatic selection may resume
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

        /// <summary>
        /// Withdraws the claim when the component is disabled, destroyed, or its GameObject deactivated.
        ///
        /// This is REQUIRED, not tidiness. Unity stops calling Update the moment the component is
        /// disabled, so the inactive path in <see cref="Step"/> never runs again - the internal state and
        /// whatever was last published to <see cref="MavEngagementView"/> would both stay Locked
        /// indefinitely. A consumer would then see a lock held by an authority that reports itself
        /// unavailable and can never change its mind.
        /// </summary>
        private void OnDisable()
        {
            WithdrawAuthority(MavLockLossReason.AuthorityUnavailable);
            PublishDebug();
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
                    WithdrawAuthority(MavLockLossReason.AuthorityUnavailable);
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
                // The track is gone entirely. A lock in progress or held cannot survive that, and a
                // commanded track that no longer exists cannot still be honoured - so this is the same
                // withdrawal as any other, with its own reason.
                WithdrawAuthority(MavLockLossReason.TrackDropped);
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
                selectionIsExplicit = false;   // picked, not asked for
                acquisitionElapsed = 0f;
                state = MavLockState.Selected;
            }
        }

        /// <summary>
        /// Re-picks an AUTOMATIC selection when the current one cannot reach a lock and another can.
        ///
        /// WHY THIS IS NEEDED. Automatic selection used to run only while nothing was selected, so the
        /// FIRST lock-capable-looking track latched forever. With the legacy scene-sweep feed live -
        /// and it always is - that feed reports Coarse and nothing better, while
        /// <see cref="minimumLockQuality"/> is Tracked. So a legacy track could be selected at startup
        /// and the controller would sit in Selected indefinitely, never acquiring, while a radar track
        /// good enough to lock went unlooked-at. Nothing was logged, because nothing had failed: it was
        /// a selection that simply could never progress.
        ///
        /// OWNERSHIP RULE. Automatic selections may yield; commanded ones may not. An earlier version of
        /// this method claimed it "never overrides an explicit request" and reasoned that a commanded
        /// selection stays in Selected only until its track becomes lockable - which is false whenever
        /// the commanded track simply stays Coarse. Nothing recorded WHICH kind of selection was held,
        /// so a player, AI or FAM command naming a Coarse track could be silently replaced. The origin
        /// is now tracked in <see cref="selectionIsExplicit"/> and checked here first.
        ///
        /// A commanded selection is therefore held until one of: its quality improves, its track is
        /// dropped, another selection is commanded, a commanded clear or break, or the authority is
        /// disabled. Waiting on a low-quality track is a legitimate thing to be told to do - the point
        /// of a command is that the machine does not know better.
        ///
        /// Otherwise deliberately narrow: it runs ONLY in <see cref="MavLockState.Selected"/> and ONLY
        /// when the selection is below the lock threshold, so it can never interrupt acquisition, a
        /// lock, or a coast. A rule that could re-pick mid-acquisition would make acquisition timing
        /// unpredictable, which is the opposite of what this phase is for.
        /// </summary>
        private void ReconsiderStalledSelection()
        {
            if (selectionIsExplicit)
                return;   // commanded: not ours to replace

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
                // Already false - this method returns early for a commanded selection - but stated so
                // that every write to selectedTrackId visibly decides the origin alongside it.
                selectionIsExplicit = false;
                acquisitionElapsed = 0f;
            }
        }

        /// <summary>
        /// Mirrors state for the Inspector and publishes the authoritative claim.
        ///
        /// Deliberately reads through the PUBLIC accessors, not the private fields, so that what is
        /// published and what the Inspector shows are exactly what a consumer would observe. An
        /// unavailable authority therefore publishes "nothing held" without needing a second rule here.
        /// </summary>
        private void PublishDebug()
        {
            debugLockState = LockState;
            debugSelectedTrackId = SelectedTrackId;
            debugLockedTrackId = LockedTrackId;
            debugAcquisitionProgress01 = AcquisitionProgress01;
            debugTimeInLock = TimeInLockSeconds;
            debugCoastElapsed = state == MavLockState.Coasting ? coastElapsed : 0f;
            debugLastLossReason = lastLossReason;
            debugSelectionIsExplicit = SelectionIsExplicit;
            debugLockedTrackName = owner != null && LockedTrackId != 0
                ? owner.GetTrackDisplayName(LockedTrackId)
                : "none";

            if (engagementView != null)
                engagementView.PublishAuthoritativeLock(LockedTrackId, LockState, debugLockedTrackName);
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
