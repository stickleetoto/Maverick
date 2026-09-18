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
        /// Stable key for this object, as the producing source sees it, for as long as the source
        /// considers it the same object. The owner correlates on (source, sourceKey) and never
        /// interprets the value. Zero is invalid.
        /// </summary>
        public int sourceKey;

        /// <summary>What produced this observation.</summary>
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

        /// <summary>What kind of producer this is. Used for correlation keys and for display.</summary>
        MavTrackSource FeedSource { get; }

        /// <summary>Appends this sample's observations. Returns how many were appended.</summary>
        int CollectObservations(List<MavTrackObservation> into);
    }
}
