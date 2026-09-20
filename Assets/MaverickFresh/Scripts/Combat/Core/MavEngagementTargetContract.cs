using UnityEngine;

namespace MaverickFresh.Combat
{
    /// <summary>
    /// What KIND of thing an engagement is pointed at.
    ///
    /// Three cases, because the legacy stack really does have three and collapsing them is what made the
    /// lock authorities incomparable. A radar lock is about a TRACK - something a sensor believes it is
    /// following. A CAS designation or a targeting-pod point lock can be about a BARE PLACE ON THE GROUND
    /// that nothing is tracking and that has no identity beyond its coordinates.
    ///
    /// <see cref="None"/> is zero so that an unset reference cannot look like a valid one.
    /// </summary>
    public enum MavEngagementTargetKind
    {
        /// <summary>Nothing is referenced. The default.</summary>
        None = 0,

        /// <summary>A track, identified by its track id from <c>MavTargetTrackOwner</c>.</summary>
        Track = 1,

        /// <summary>A place in the world, identified by its coordinates and nothing else.</summary>
        WorldPoint = 2,
    }

    /// <summary>
    /// What an engagement is pointed at: a track, a bare world point, or nothing.
    ///
    /// WHY THIS EXISTS. <see cref="IMavTrackLockAuthority"/> is track-only and must stay that way - a lock
    /// is a maintained commitment to something being tracked, and there is no coherent way to "lock" a
    /// patch of dirt that no sensor is following. But `MavTargetingPodSystem` and `MavCASTargetingSystem`
    /// genuinely do designate bare points, and `MavCASWeaponSystem` genuinely does release on one. That
    /// information is real. The two previous phases documented it as the representational blocker
    /// underneath four of the six remaining consumer migrations, and this is the vocabulary that lets it
    /// be expressed without pretending it is a lock.
    ///
    /// THE ALTERNATIVE WAS WORSE. The tempting shortcut is to mint a track for the designated point so
    /// everything can go through one authority. That would put a fabricated entry in the track owner that
    /// no sensor ever observed, which every consumer of <c>MavTargetTrackData</c> would then have to
    /// treat as real - including quality, age and the coast logic, none of which mean anything for a
    /// coordinate. The other shortcut is to widen the lock interface with a `worldPoint`, which makes
    /// every lock consumer handle a case that can never be locked. Both merge two concepts to avoid
    /// writing down that there are two.
    ///
    /// WHAT THIS IS NOT. It is a REFERENCE, not a decision. Holding one says nothing about whether a lock
    /// is established, whether a designation has been made, or whether a weapon may fire - which is why
    /// there is deliberately no `IsLocked`, no `IsDesignated` and no `IsAuthorized` member here, and why
    /// there is no interface in this file. Those are separate authorities' answers, and a value type that
    /// appeared to answer them would be read as if it did.
    ///
    /// NO OBJECT OWNERSHIP. Like <c>MavTargetTrackData</c>, this holds no GameObject, Transform or
    /// Component. A track reference is an id; resolving it is the track owner's job. A point reference is
    /// coordinates; there is nothing to resolve. A consumer that held the object would be reading truth
    /// no authority ever claimed, which is the shortcut the scene-searching legacy code takes.
    ///
    /// IMMUTABLE BY CONSTRUCTION. A readonly struct with private readonly fields and no setters, built
    /// only through the factories below. A reference that could be edited after it was handed over would
    /// let a consumer change what it was told to engage.
    /// </summary>
    public readonly struct MavEngagementTargetRef
    {
        private readonly MavEngagementTargetKind kind;
        private readonly int trackId;
        private readonly Vector3 worldPoint;

        private MavEngagementTargetRef(MavEngagementTargetKind kind, int trackId, Vector3 worldPoint)
        {
            this.kind = kind;
            this.trackId = trackId;
            this.worldPoint = worldPoint;
        }

        /// <summary>A reference to nothing. Also what <c>default</c> gives, so an unset field is valid.</summary>
        public static MavEngagementTargetRef None
        {
            get { return new MavEngagementTargetRef(MavEngagementTargetKind.None, 0, Vector3.zero); }
        }

        /// <summary>
        /// A reference to a track.
        ///
        /// A non-positive id is not a track, so it produces <see cref="None"/> rather than a Track case
        /// carrying nothing. Track id 0 already means "no track" everywhere else in the combat path, and
        /// allowing a Track reference with id 0 would create a second way to say nothing - one that reads
        /// as something.
        /// </summary>
        public static MavEngagementTargetRef FromTrack(int trackId)
        {
            if (trackId <= 0)
                return None;
            return new MavEngagementTargetRef(MavEngagementTargetKind.Track, trackId, Vector3.zero);
        }

        /// <summary>
        /// A reference to a place in the world.
        ///
        /// Every coordinate is accepted, including the origin: a designation at (0,0,0) is a real
        /// designation, and there is no in-band value that could mean "no point". That asymmetry with
        /// <see cref="FromTrack"/> is why <see cref="Kind"/> exists rather than being inferred from the
        /// payload.
        /// </summary>
        public static MavEngagementTargetRef FromWorldPoint(Vector3 point)
        {
            return new MavEngagementTargetRef(MavEngagementTargetKind.WorldPoint, 0, point);
        }

        /// <summary>Which of the three cases this is.</summary>
        public MavEngagementTargetKind Kind { get { return kind; } }

        public bool IsNone { get { return kind == MavEngagementTargetKind.None; } }

        public bool IsTrack { get { return kind == MavEngagementTargetKind.Track; } }

        public bool IsWorldPoint { get { return kind == MavEngagementTargetKind.WorldPoint; } }

        /// <summary>
        /// The track id, or 0 when this is not a track.
        ///
        /// Fails closed for the same reason the lock authority's accessors do: a consumer that read an id
        /// out of a world-point reference would be engaging a track that was never referenced.
        /// </summary>
        public int TrackId
        {
            get { return kind == MavEngagementTargetKind.Track ? trackId : 0; }
        }

        /// <summary>The referenced point, or <see cref="Vector3.zero"/> when this is not a world point.</summary>
        public Vector3 WorldPoint
        {
            get { return kind == MavEngagementTargetKind.WorldPoint ? worldPoint : Vector3.zero; }
        }

        /// <summary>
        /// True with the track id when this references a track.
        ///
        /// The preferred accessor, matching <c>TryGetLockedTrack</c>: it forces the caller to handle the
        /// other two cases instead of reading a zero and carrying on.
        /// </summary>
        public bool TryGetTrackId(out int id)
        {
            if (kind == MavEngagementTargetKind.Track)
            {
                id = trackId;
                return true;
            }
            id = 0;
            return false;
        }

        /// <summary>True with the point when this references a place in the world.</summary>
        public bool TryGetWorldPoint(out Vector3 point)
        {
            if (kind == MavEngagementTargetKind.WorldPoint)
            {
                point = worldPoint;
                return true;
            }
            point = Vector3.zero;
            return false;
        }

        /// <summary>
        /// Value equality, with the kind participating.
        ///
        /// So a track reference is never equal to a point reference, whatever their payloads look like -
        /// the case that would let "track 5" and "the point at (5,0,0)" be confused for each other.
        /// Components are compared exactly rather than through <c>Vector3</c>'s approximate <c>==</c>,
        /// because a contract that said two different designations were the same within a tolerance would
        /// make equality a policy decision.
        /// </summary>
        public bool Equals(MavEngagementTargetRef other)
        {
            if (kind != other.kind)
                return false;

            switch (kind)
            {
                case MavEngagementTargetKind.Track:
                    return trackId == other.trackId;
                case MavEngagementTargetKind.WorldPoint:
                    return worldPoint.x == other.worldPoint.x
                           && worldPoint.y == other.worldPoint.y
                           && worldPoint.z == other.worldPoint.z;
            }
            return true;   // None == None
        }

        public override bool Equals(object obj)
        {
            return obj is MavEngagementTargetRef && Equals((MavEngagementTargetRef)obj);
        }

        public override int GetHashCode()
        {
            int hash = (int)kind * 397;
            switch (kind)
            {
                case MavEngagementTargetKind.Track:
                    return hash ^ trackId;
                case MavEngagementTargetKind.WorldPoint:
                    return hash ^ worldPoint.GetHashCode();
            }
            return hash;
        }

        public static bool operator ==(MavEngagementTargetRef a, MavEngagementTargetRef b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(MavEngagementTargetRef a, MavEngagementTargetRef b)
        {
            return !a.Equals(b);
        }

        /// <summary>Diagnostics only. Never parsed, and never a substitute for reading <see cref="Kind"/>.</summary>
        public override string ToString()
        {
            switch (kind)
            {
                case MavEngagementTargetKind.Track:
                    return "track " + trackId;
                case MavEngagementTargetKind.WorldPoint:
                    return "point " + worldPoint.ToString("F1");
            }
            return "none";
        }
    }
}
