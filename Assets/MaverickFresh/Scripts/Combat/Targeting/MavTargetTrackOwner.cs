using System.Collections.Generic;
using UnityEngine;

namespace MaverickFresh.Combat.Targeting
{
    /// <summary>
    /// The single authority for "what this aircraft is aware of".
    ///
    /// It collects observations from registered feeds, correlates them into tracks with stable ids,
    /// ages them, and drops them when they go unseen. That is all it does. It does not detect
    /// anything, does not decide what is worth shooting, does not own a lock, and does not search
    /// the scene: feeds register themselves, which is what lets a real radar replace the legacy
    /// adapter later without this class changing.
    ///
    /// OWNERSHIP OF IDENTITY, which is the part that is easy to get wrong:
    ///
    ///   - a feed owns its own LOCAL <c>sourceKey</c>, meaningful only to that feed
    ///   - this owner owns the GLOBAL, stable <c>trackId</c>; no feed ever assigns one
    ///   - <see cref="MavTrackSource"/> is provenance/category - Legacy, Radar, InfraRed, Datalink -
    ///     and is explicitly NOT an identity
    ///   - correlation authority is (FEED IDENTITY, sourceKey), never (source category, sourceKey)
    ///
    /// That last point matters: two radars, or a radar and a datalink both reporting
    /// <see cref="MavTrackSource.Radar"/>, would collide the moment they happened to choose the same
    /// local key, silently merging two different objects into one track. Feed identity is assigned
    /// here on registration so it cannot be spoofed or duplicated by a producer.
    ///
    /// WHY THIS EXISTS AT ALL. Before it, the target set was whatever `FindObjectsOfType` returned at
    /// the moment each consumer happened to ask. The scene graph WAS the track database, so nothing
    /// could express a target that is believed but not visible, visible but not detected, or seen a
    /// moment ago and now stale - the three things a sensor exists to represent. Every consumer also
    /// got ground truth rather than sensor output, so no sensor limitation could ever be modelled.
    ///
    /// Deliberately NOT here, because they are later phases: detection, scan volumes, lock semantics,
    /// sensor fusion between feeds, identification, seekers, guidance. In particular two feeds
    /// observing the same physical object produce TWO tracks in R0. That is not an oversight: merging
    /// them is fusion, fusion needs a correlation model nobody has written yet, and guessing one here
    /// would be worse than leaving the duplication visible.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MavTargetTrackOwner : MonoBehaviour
    {
        [Header("Sampling")]
        [Tooltip("Seconds between observation sweeps. Tracks age continuously; only sampling is throttled.")]
        public float sampleInterval = 0.25f;

        [Tooltip("A track unseen for longer than this is dropped. Must exceed sampleInterval.")]
        public float trackDropSeconds = 3.0f;

        [Header("Read-only state")]
        public int debugTrackCount;
        public int debugFeedCount;
        public int debugObservationsLastSweep;
        public int debugTracksDropped;
        public int debugNextTrackId = 1;
        public int debugNextFeedId = 1;

        /// <summary>
        /// How many observations arrived declaring a different <see cref="MavTrackSource"/> than the
        /// feed that emitted them. The owner uses the registered feed's value regardless; this counter
        /// exists so a producer lying about its own provenance is visible rather than silent.
        /// </summary>
        public int debugProvenanceMismatches;

        /// <summary>One registered feed, with the identity this owner assigned it.</summary>
        private struct FeedRegistration
        {
            public IMavTargetObservationFeed feed;
            public int feedId;
        }

        private readonly List<FeedRegistration> feeds = new List<FeedRegistration>(4);
        private readonly Dictionary<IMavTargetObservationFeed, int> assignedFeedIds =
            new Dictionary<IMavTargetObservationFeed, int>(4);
        private readonly List<MavTrackObservation> observationScratch = new List<MavTrackObservation>(64);
        private readonly List<MavTrackEntry> tracks = new List<MavTrackEntry>(64);
        private readonly Dictionary<long, int> correlation = new Dictionary<long, int>(64);

        private float nextSampleTime;
        private int nextTrackId = 1;
        private int nextFeedId = 1;

        /// <summary>A track plus the bookkeeping the owner needs and consumers must not see.</summary>
        private struct MavTrackEntry
        {
            public MavTargetTrackData data;
            public long correlationKey;
            public int feedId;
            public string displayName;
            public int team;
            public bool isAirTarget;
            public bool hadPreviousSample;
            public Vector3 previousPosition;
            public float previousSampleTime;
        }

        /// <summary>
        /// Registers a feed and assigns it an identity.
        ///
        /// Idempotent, so a caller that cannot easily tell whether it already registered does not
        /// create a duplicate - which would double every observation. A feed that unregisters and
        /// registers again keeps the SAME identity, so its tracks survive an OnDisable/OnEnable cycle
        /// instead of being recreated under new ids.
        /// </summary>
        public void RegisterFeed(IMavTargetObservationFeed feed)
        {
            if (feed == null || IndexOfFeed(feed) >= 0)
                return;

            int feedId;
            if (!assignedFeedIds.TryGetValue(feed, out feedId))
            {
                feedId = nextFeedId++;
                assignedFeedIds[feed] = feedId;
                debugNextFeedId = nextFeedId;
            }

            FeedRegistration registration;
            registration.feed = feed;
            registration.feedId = feedId;
            feeds.Add(registration);
            debugFeedCount = feeds.Count;
        }

        /// <summary>
        /// Unregisters a feed. Its existing tracks are left to age out normally rather than being
        /// deleted here: a feed going quiet and a feed being removed look the same to a consumer, and
        /// the aging rule already covers both. Because correlation keys carry feed identity, removing
        /// one feed cannot disturb another feed's tracks or their resolution.
        /// </summary>
        public void UnregisterFeed(IMavTargetObservationFeed feed)
        {
            if (feed == null)
                return;

            int index = IndexOfFeed(feed);
            if (index >= 0)
                feeds.RemoveAt(index);
            debugFeedCount = feeds.Count;
        }

        private int IndexOfFeed(IMavTargetObservationFeed feed)
        {
            for (int i = 0; i < feeds.Count; i++)
            {
                if (ReferenceEquals(feeds[i].feed, feed))
                    return i;
            }
            return -1;
        }

        /// <summary>The identity this owner assigned a feed, or 0 if it has never registered.</summary>
        public int GetFeedId(IMavTargetObservationFeed feed)
        {
            if (feed == null)
                return 0;
            int feedId;
            return assignedFeedIds.TryGetValue(feed, out feedId) ? feedId : 0;
        }

        /// <summary>How many tracks are currently held.</summary>
        public int TrackCount
        {
            get { return tracks.Count; }
        }

        /// <summary>Track by index, for consumers that want to walk the set without allocating.</summary>
        public MavTargetTrackData GetTrack(int index)
        {
            if (index < 0 || index >= tracks.Count)
                return MavTargetTrackData.Invalid;
            return tracks[index].data;
        }

        /// <summary>Track by stable id. False when no such track is held any more.</summary>
        public bool TryGetTrackById(int trackId, out MavTargetTrackData track)
        {
            track = MavTargetTrackData.Invalid;
            if (trackId == 0)
                return false;

            for (int i = 0; i < tracks.Count; i++)
            {
                if (tracks[i].data.trackId == trackId)
                {
                    track = tracks[i].data;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Display label for a track. Deliberately separate from <see cref="MavTargetTrackData"/>:
        /// a name is presentation, and putting a string in the track struct would make every future
        /// consumer carry it through code that has no business reading it.
        /// </summary>
        public string GetTrackDisplayName(int trackId)
        {
            for (int i = 0; i < tracks.Count; i++)
            {
                if (tracks[i].data.trackId == trackId)
                    return tracks[i].displayName;
            }
            return string.Empty;
        }

        public bool TryGetTrackTeam(int trackId, out int team, out bool isAirTarget)
        {
            for (int i = 0; i < tracks.Count; i++)
            {
                if (tracks[i].data.trackId == trackId)
                {
                    team = tracks[i].team;
                    isAirTarget = tracks[i].isAirTarget;
                    return true;
                }
            }
            team = 0;
            isAirTarget = false;
            return false;
        }

        /// <summary>
        /// Which feed a track came from. Provenance a consumer can act on without the owner exposing
        /// its correlation table.
        /// </summary>
        public bool TryGetTrackFeedId(int trackId, out int feedId)
        {
            for (int i = 0; i < tracks.Count; i++)
            {
                if (tracks[i].data.trackId == trackId)
                {
                    feedId = tracks[i].feedId;
                    return true;
                }
            }
            feedId = 0;
            return false;
        }

        /// <summary>
        /// Resolves one feed's local key to the global track id.
        ///
        /// Takes the FEED, not a source category, because the feed is the identity half of the
        /// correlation key. A caller holding a concrete object can therefore ask "which track is this,
        /// as far as the feed that reported it is concerned" without two feeds of the same category
        /// being able to answer for each other.
        /// </summary>
        public bool TryResolveTrackId(IMavTargetObservationFeed feed, int sourceKey, out int trackId)
        {
            trackId = 0;
            if (feed == null || sourceKey == 0)
                return false;

            int feedId;
            if (!assignedFeedIds.TryGetValue(feed, out feedId))
                return false;

            return TryResolveTrackIdByFeedId(feedId, sourceKey, out trackId);
        }

        /// <summary>Same resolution, for a caller that already knows the feed id.</summary>
        public bool TryResolveTrackIdByFeedId(int feedId, int sourceKey, out int trackId)
        {
            trackId = 0;
            if (feedId == 0 || sourceKey == 0)
                return false;

            int index;
            if (!correlation.TryGetValue(MakeKey(feedId, sourceKey), out index))
                return false;
            if (index < 0 || index >= tracks.Count)
                return false;

            trackId = tracks[index].data.trackId;
            return trackId != 0;
        }

        private void Update()
        {
            if (Time.unscaledTime >= nextSampleTime)
            {
                nextSampleTime = Time.unscaledTime + Mathf.Max(0.02f, sampleInterval);
                Sweep();
            }

            DropStaleTracks();
            debugTrackCount = tracks.Count;
        }

        /// <summary>
        /// One sweep. Feeds are collected ONE AT A TIME, on purpose: the owner has to know which feed
        /// produced each observation in order to correlate on feed identity and to stamp provenance,
        /// and a single shared list collected from every feed at once throws that away.
        /// </summary>
        private void Sweep()
        {
            float now = Time.time;
            int total = 0;

            for (int i = 0; i < feeds.Count; i++)
            {
                FeedRegistration registration = feeds[i];
                if (registration.feed == null || !registration.feed.IsFeedActive)
                    continue;

                observationScratch.Clear();
                registration.feed.CollectObservations(observationScratch);
                total += observationScratch.Count;

                MavTrackSource declared = registration.feed.FeedSource;
                for (int o = 0; o < observationScratch.Count; o++)
                    Integrate(observationScratch[o], registration.feedId, declared, now);
            }

            debugObservationsLastSweep = total;
        }

        /// <summary>
        /// Folds one observation into the track set.
        ///
        /// Provenance comes from the REGISTERED FEED, not from the observation: the owner already
        /// knows who it just called, so a producer's own claim about its category is redundant at best
        /// and wrong at worst. A disagreement is counted in
        /// <see cref="debugProvenanceMismatches"/> and then ignored in favour of the feed's value.
        /// </summary>
        private void Integrate(MavTrackObservation observation, int feedId, MavTrackSource declaredSource, float now)
        {
            if (observation.sourceKey == 0 || feedId == 0)
                return;

            if (observation.source != declaredSource && observation.source != MavTrackSource.Unknown)
                debugProvenanceMismatches++;

            long key = MakeKey(feedId, observation.sourceKey);
            int index;

            if (!correlation.TryGetValue(key, out index))
            {
                MavTrackEntry created = new MavTrackEntry();
                created.correlationKey = key;
                created.feedId = feedId;
                created.data.trackId = nextTrackId++;
                debugNextTrackId = nextTrackId;
                created.data.source = declaredSource;
                ApplySample(ref created, observation, declaredSource, now, false);
                tracks.Add(created);
                correlation[key] = tracks.Count - 1;
                return;
            }

            if (index < 0 || index >= tracks.Count)
                return;

            MavTrackEntry entry = tracks[index];
            ApplySample(ref entry, observation, declaredSource, now, true);
            tracks[index] = entry;
        }

        /// <summary>
        /// Folds one observation into a track.
        ///
        /// Velocity is taken from the observation when the producer supplied it, and otherwise
        /// differenced from the previous sample. The difference is deliberately NOT computed on the
        /// first sighting: one position is not a velocity, and reporting zero there would be a
        /// measurement nobody made. Quality drops to Coarse until a velocity exists.
        /// </summary>
        private void ApplySample(ref MavTrackEntry entry, MavTrackObservation observation,
                                 MavTrackSource declaredSource, float now, bool hadTrack)
        {
            entry.displayName = observation.displayName;
            entry.team = observation.team;
            entry.isAirTarget = observation.isAirTarget;
            entry.data.source = declaredSource;
            entry.data.position = observation.position;
            entry.data.observedAtTime = now;

            bool haveVelocity = false;
            if (observation.hasVelocity)
            {
                entry.data.velocityMps = observation.velocityMps;
                haveVelocity = true;
            }
            else if (hadTrack && entry.hadPreviousSample)
            {
                float dt = now - entry.previousSampleTime;
                if (dt > 1e-4f)
                {
                    entry.data.velocityMps = (observation.position - entry.previousPosition) / dt;
                    haveVelocity = true;
                }
            }

            if (!haveVelocity)
                entry.data.velocityMps = Vector3.zero;

            MavTrackQuality reported = observation.quality;
            if (!haveVelocity && reported > MavTrackQuality.Coarse)
                reported = MavTrackQuality.Coarse;
            entry.data.quality = reported;

            entry.previousPosition = observation.position;
            entry.previousSampleTime = now;
            entry.hadPreviousSample = true;
        }

        private void DropStaleTracks()
        {
            if (tracks.Count == 0)
                return;

            float now = Time.time;
            float limit = Mathf.Max(0.05f, trackDropSeconds);
            bool removedAny = false;

            for (int i = tracks.Count - 1; i >= 0; i--)
            {
                if (tracks[i].data.AgeAt(now) <= limit)
                    continue;

                correlation.Remove(tracks[i].correlationKey);
                tracks.RemoveAt(i);
                debugTracksDropped++;
                removedAny = true;
            }

            // RemoveAt shifts everything after it, so the index table is rebuilt rather than patched.
            // Correctness over cleverness: a stale index would hand a consumer someone else's track.
            if (removedAny)
                RebuildCorrelation();
        }

        private void RebuildCorrelation()
        {
            correlation.Clear();
            for (int i = 0; i < tracks.Count; i++)
                correlation[tracks[i].correlationKey] = i;
        }

        /// <summary>
        /// Correlation key: feed identity in the high word, the feed's local key in the low word.
        ///
        /// Both halves are needed. Feed identity alone cannot distinguish two objects one feed
        /// reports, and a local key alone cannot distinguish two feeds that chose the same number -
        /// which is exactly what a source CATEGORY fails to prevent, since two radars are both
        /// <see cref="MavTrackSource.Radar"/>.
        /// </summary>
        private static long MakeKey(int feedId, int sourceKey)
        {
            return ((long)feedId << 32) | (uint)sourceKey;
        }

        /// <summary>Test seam: clears all tracks and correlation without touching feed registration.</summary>
        public void ClearTracksForTesting()
        {
            tracks.Clear();
            correlation.Clear();
            debugTrackCount = 0;
        }

        /// <summary>Test seam: runs one sweep immediately, ignoring the sample interval.</summary>
        public void SweepNowForTesting()
        {
            Sweep();
            DropStaleTracks();
            debugTrackCount = tracks.Count;
        }
    }
}
