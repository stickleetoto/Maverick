namespace MaverickFresh.Combat
{
    /// <summary>
    /// Where an engagement is in the lock lifecycle.
    ///
    /// Ordered so that "more committed" compares greater, which makes a precedence rule expressible
    /// without a lookup table.
    /// </summary>
    public enum MavLockState
    {
        /// <summary>Nothing selected, nothing locked.</summary>
        Idle = 0,

        /// <summary>A track is selected. Selection is intent, NOT a lock.</summary>
        Selected = 1,

        /// <summary>A selected track is being held long enough to establish a lock.</summary>
        Acquiring = 2,

        /// <summary>
        /// A maintained lock: the authority is committed to one track and that track is still being
        /// refreshed.
        /// </summary>
        Locked = 3,

        /// <summary>
        /// The locked track has gone stale but the lock is being held through the gap. A real lock does
        /// not evaporate on one missed update, and modelling that gap explicitly is what stops "locked"
        /// from meaning "was visible this exact frame".
        /// </summary>
        Coasting = 4,
    }

    /// <summary>Why a lock ended. Recorded rather than inferred, so a consumer never has to guess.</summary>
    public enum MavLockLossReason
    {
        None = 0,

        /// <summary>The track owner no longer holds the track at all.</summary>
        TrackDropped = 1,

        /// <summary>The track still exists but has not been observed for longer than the coast allowance.</summary>
        CoastExpired = 2,

        /// <summary>The track's quality fell below what the authority requires to hold a lock.</summary>
        QualityTooLow = 3,

        /// <summary>Something asked for a different track, so the previous lock was abandoned.</summary>
        SelectionChanged = 4,

        /// <summary>A deliberate break: an operator command, or an AI giving up the engagement.</summary>
        Commanded = 5,

        /// <summary>The authority itself was disabled or lost its track source.</summary>
        AuthorityUnavailable = 6,
    }

    /// <summary>
    /// The one thing that may answer "what is this aircraft locked onto".
    ///
    /// WHY A SEPARATE AUTHORITY. Detection and lock are different questions. A sensor answers "what can
    /// I see"; a lock answers "what am I committed to", which involves time, hysteresis, and a decision
    /// that survives a missed observation. Folding the second into the first is what the legacy stack
    /// did - `MavF22SensorSuite` scans, times an STT lock, reads its own keys and writes HUD state in
    /// one component - and it is why there was no single answer to what the aircraft was engaging.
    ///
    /// A LOCK IS NOT A MEASUREMENT. This is the reason <see cref="MavTrackQuality.Locked"/> is asserted
    /// here and never by a producer: a sensor reporting `Locked` would be claiming an engagement
    /// decision it has no part in. Radar Core asserts the opposite of itself in its own validation, and
    /// that must stay true.
    ///
    /// Future launchers depend on this interface rather than on any concrete lock implementation, so
    /// the lock can be replaced without touching them.
    /// </summary>
    public interface IMavTrackLockAuthority
    {
        /// <summary>Whether this authority can be consulted at all right now.</summary>
        bool IsLockAuthorityActive { get; }

        /// <summary>Where the engagement currently is in the lifecycle.</summary>
        MavLockState LockState { get; }

        /// <summary>
        /// True with the locked track when the state is <see cref="MavLockState.Locked"/> or
        /// <see cref="MavLockState.Coasting"/>.
        ///
        /// Coasting counts as locked on purpose: the commitment has not been abandoned, and a consumer
        /// that treated a coasting lock as "no lock" would drop an engagement on the first missed
        /// observation. A consumer that needs the distinction reads <see cref="LockState"/>.
        /// </summary>
        bool TryGetLockedTrack(out MavTargetTrackData track);

        /// <summary>Seconds the current lock has been held. Zero when not locked.</summary>
        float TimeInLockSeconds { get; }

        /// <summary>Why the last lock ended. <see cref="MavLockLossReason.None"/> if none has.</summary>
        MavLockLossReason LastLossReason { get; }
    }
}
