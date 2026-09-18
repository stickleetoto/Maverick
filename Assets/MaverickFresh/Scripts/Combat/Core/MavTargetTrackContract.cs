using UnityEngine;

namespace MaverickFresh.Combat
{
    /// <summary>
    /// How much a track is worth trusting. Ordered weakest to strongest so comparisons are meaningful.
    /// </summary>
    public enum MavTrackQuality
    {
        /// <summary>No usable information. The default, so an unset track cannot look valid.</summary>
        None = 0,

        /// <summary>Position only, or a position whose age is unknown. Not enough to shoot from.</summary>
        Coarse = 1,

        /// <summary>Position and velocity from a repeated observation of the same object.</summary>
        Tracked = 2,

        /// <summary>A dedicated, maintained lock on one object.</summary>
        Locked = 3,
    }

    /// <summary>
    /// What produced a track. Consumers use this for display and for deciding whether a track is
    /// good enough for a given job, never to reach back into the producer's implementation.
    /// </summary>
    public enum MavTrackSource
    {
        Unknown = 0,
        Legacy = 1,
        Radar = 2,
        InfraRed = 3,
        TargetingPod = 4,
        Datalink = 5,
    }

    /// <summary>
    /// The neutral shape of "something the aircraft is aware of".
    ///
    /// THIS IS A DATA CONTRACT, NOT A TRACKER. Nothing here detects, scans, correlates, ages or
    /// maintains anything. It exists so that the radar, missile, HUD and AI work that follows can be
    /// written against one shape instead of each re-deriving its own, and so none of them has to
    /// start from `FindObjectsOfType&lt;SomeLegacyMarker&gt;()` the way today's code does.
    ///
    /// Deliberately absent, because they belong to later phases: detection, scan volumes, track
    /// initiation and dropping, correlation between sensors, lock timing, seeker behavior, guidance.
    ///
    /// Deliberately NOT a GameObject reference. A track is what a sensor believes, which is not the
    /// same thing as the object itself: it can be stale, wrong, or about something no longer there.
    /// A consumer that holds the GameObject cannot represent any of that, and ends up reading truth
    /// the sensor never had - the exact shortcut today's scene-searching code takes.
    /// </summary>
    public struct MavTargetTrackData
    {
        /// <summary>
        /// Stable identity for as long as the producer considers this the same object. Zero means no
        /// track. Identity is the producer's to assign and must not be derived from instance ids.
        /// </summary>
        public int trackId;

        /// <summary>Best known world position.</summary>
        public Vector3 position;

        /// <summary>Best known world velocity, m/s. Zero when the producer cannot estimate it.</summary>
        public Vector3 velocity;

        /// <summary>
        /// `Time.time` when this information was last actually observed - NOT when it was read.
        /// Age is the consumer's to compute, so a stale track cannot masquerade as a fresh one.
        /// </summary>
        public float observedAtTime;

        /// <summary>How much the producer trusts this. <see cref="MavTrackQuality.None"/> means unusable.</summary>
        public MavTrackQuality quality;

        /// <summary>What kind of producer this came from.</summary>
        public MavTrackSource source;

        /// <summary>A track is usable only if it has identity and non-zero quality.</summary>
        public bool IsValid
        {
            get { return trackId != 0 && quality != MavTrackQuality.None; }
        }

        /// <summary>Seconds since this information was observed, at the supplied time.</summary>
        public float AgeAt(float now)
        {
            return Mathf.Max(0f, now - observedAtTime);
        }

        public static MavTargetTrackData Invalid
        {
            get
            {
                MavTargetTrackData t;
                t.trackId = 0;
                t.position = Vector3.zero;
                t.velocity = Vector3.zero;
                t.observedAtTime = 0f;
                t.quality = MavTrackQuality.None;
                t.source = MavTrackSource.Unknown;
                return t;
            }
        }
    }

    /// <summary>
    /// Something that can report a current track.
    ///
    /// One method on purpose. A radar returning many tracks, a seeker holding exactly one, and a
    /// legacy adapter wrapping today's designated target all satisfy this, and no consumer needs to
    /// know which it is talking to. Enumeration of multiple tracks is deliberately left out until a
    /// real sensor exists to justify its shape.
    /// </summary>
    public interface IMavTargetTrackSource
    {
        /// <summary>
        /// True with a valid track when this source currently has one. Implementations must return
        /// false rather than a stale or fabricated track.
        /// </summary>
        bool TryGetCurrentTrack(out MavTargetTrackData track);
    }
}
