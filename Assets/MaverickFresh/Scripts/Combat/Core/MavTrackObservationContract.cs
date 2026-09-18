using System.Collections.Generic;
using UnityEngine;

namespace MaverickFresh.Combat
{
    /// <summary>
    /// One thing a producer saw, this sample.
    ///
    /// An observation is NOT a track. It carries no identity the rest of the system can rely on and
    /// no history: it is what one producer believes right now. The track owner is what turns a
    /// stream of observations into tracks with stable ids, age and lifetime.
    ///
    /// The split matters because it is where a real sensor will differ from today's adapter. A radar
    /// will emit observations only for what it can actually see, at the quality it can actually
    /// support, and the owner's behavior will not have to change at all.
    /// </summary>
    public struct MavTrackObservation
    {
        /// <summary>
        /// The producing feed's own LOCAL key for this object, stable for as long as that feed
        /// considers it the same object. Zero is invalid.
        ///
        /// Local is the operative word: it need only be unique within one feed, because the owner
        /// correlates on (FEED IDENTITY, sourceKey). Two feeds may use the same value for different
        /// objects without colliding. The owner never interprets the value, and a feed must not try to
        /// make it globally unique - that is what the owner's <c>trackId</c> is for.
        /// </summary>
        public int sourceKey;

        /// <summary>
        /// What kind of producer this is. Category only.
        ///
        /// The owner does NOT trust this field: it already knows which registered feed it just
        /// called, and stamps provenance from that feed's <see cref="IMavTargetObservationFeed.FeedSource"/>.
        /// A disagreement is counted as a provenance mismatch and the feed's value wins. Leave it
        /// <see cref="MavTrackSource.Unknown"/> if there is nothing meaningful to say.
        /// </summary>
        public MavTrackSource source;

        /// <summary>How much the producer trusts it.</summary>
        public MavTrackQuality quality;

        /// <summary>Observed world position.</summary>
        public Vector3 position;

        /// <summary>Observed world velocity in m/s. Only meaningful when <see cref="hasVelocity"/> is true.</summary>
        public Vector3 velocityMps;

        /// <summary>Whether the producer could estimate velocity at all.</summary>
        public bool hasVelocity;

        /// <summary>Human-readable label, for HUD and telemetry only. Never used for control flow.</summary>
        public string displayName;

        /// <summary>Team as the producer understands it. Identification is not modelled yet.</summary>
        public int team;

        /// <summary>Whether the producer considers this an airborne object.</summary>
        public bool isAirTarget;
    }

    /// <summary>
    /// A producer of observations: today the legacy adapter, later a radar, an IRST, a pod, a
    /// datalink.
    ///
    /// The list is filled rather than returned so a feed running every physics step allocates
    /// nothing. Implementations append and must not clear the list.
    /// </summary>
    public interface IMavTargetObservationFeed
    {
        /// <summary>Whether this feed should be consulted at all right now.</summary>
        bool IsFeedActive { get; }

        /// <summary>
        /// What kind of producer this is: classification and provenance, such as Legacy, Radar,
        /// InfraRed or Datalink. NOT an identity, and NOT part of the correlation key - two feeds may
        /// legitimately share a value. The owner assigns each registered feed its own identity and
        /// correlates on that.
        /// </summary>
        MavTrackSource FeedSource { get; }

        /// <summary>Appends this sample's observations. Returns how many were appended.</summary>
        int CollectObservations(List<MavTrackObservation> into);
    }
}
