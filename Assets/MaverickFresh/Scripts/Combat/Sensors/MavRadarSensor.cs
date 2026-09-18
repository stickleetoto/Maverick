using System.Collections.Generic;
using MaverickFresh.Combat.Targeting;
using UnityEngine;

namespace MaverickFresh.Combat.Sensors
{
    /// <summary>
    /// The first real sensor: it produces <see cref="MavTrackObservation"/> for what it can actually
    /// see, and nothing else.
    ///
    /// It is an <see cref="IMavTargetObservationFeed"/> like any other producer, so
    /// <see cref="MavTargetTrackOwner"/> is unchanged by its arrival - which was the whole point of
    /// splitting observation from track in the previous phase. Registering this feed is the only
    /// integration step.
    ///
    /// WHAT MAKES IT A SENSOR RATHER THAN A SCENE QUERY. The legacy feed reports every marker in the
    /// scene, which is ground truth with no sensor between it and the consumer. This one reports only
    /// what survives a scan envelope, a range limit scaled by target size, and - optionally - a
    /// line-of-sight check. A target behind the aircraft, too close, too far, or behind terrain is
    /// genuinely not known, and that is the first time anything in Maverick can say so.
    ///
    /// SYNTHETIC PARAMETERS, deliberately. Every number is a gameplay tuning value. Nothing here is
    /// sourced from or intended to represent any real radar's performance, and no classified or
    /// unsupported figure is encoded. The one physically-shaped relation - detection range scaling with
    /// the fourth root of target cross-section - is plain textbook radar-range shape applied to a
    /// synthetic base range, so the result is a gameplay number with sensible behavior rather than a
    /// specification.
    ///
    /// Deliberately NOT implemented, because they are later phases: lock semantics, seekers, guidance,
    /// datalink fusion, ECM/ECCM, clutter, PRF, notching, scan patterns and beam dwell. This class
    /// detects and reports; it holds no lock and owns no weapon.
    ///
    /// <see cref="MavF22SensorSuite"/> was read as prior art and deliberately not extended: it
    /// conflates scanning with lock timing, input handling and HUD state, and writes diagnostic state
    /// back onto the targets it observes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MavRadarSensor : MonoBehaviour, IMavTargetObservationFeed
    {
        [Header("Enable")]
        [Tooltip("Master switch. When off the sensor reports nothing and the legacy feed behaves exactly as before.")]
        public bool enableRadar = true;

        [Header("Wiring")]
        [Tooltip("Owner this sensor registers with. Resolved from this GameObject when left empty.")]
        public MavTargetTrackOwner owner;

        [Tooltip("Boresight reference. Defaults to this transform; set to an antenna transform if one exists.")]
        public Transform boresight;

        [Header("Scan envelope (SYNTHETIC gameplay values)")]
        public MavRadarScanVolume scanVolume = MavRadarScanVolume.Default;

        [Header("Cadence")]
        [Tooltip("Seconds between scans. Between scans the sensor reports its last scan's contacts unchanged.")]
        public float scanIntervalSeconds = 0.25f;

        [Tooltip("Rescan the scene for candidate objects at most this often. Independent of scan cadence.")]
        public float candidateRescanInterval = 1.0f;

        [Header("Detection")]
        [Tooltip("Require an unobstructed line of sight to the target.")]
        public bool requireLineOfSight = true;

        [Tooltip("Layers that block line of sight. Targets themselves should not be on these layers.")]
        public LayerMask lineOfSightBlockers = 0;

        [Tooltip("Quality at or above this reports Tracked rather than Coarse, when a velocity is known.")]
        [Range(0f, 1f)] public float trackedQualityThreshold = 0.35f;

        [Tooltip("Skip markers on this sensor's own aircraft. A platform does not observe itself.")]
        public bool excludeOwnAircraft = true;

        [Header("Read-only state")]
        public int debugCandidateCount;
        public int debugContactCount;
        public int debugScanCount;
        public int debugCandidateRescanCount;
        public int debugRejectedRange;
        public int debugRejectedAngle;
        public int debugRejectedLineOfSight;
        public int debugRejectedOwnship;
        public bool debugRegistered;
        public float debugNearestContactRange;
        public string debugNearestContactName = "none";

        /// <summary>
        /// Radar-local keys, assigned by this sensor and meaningful only to it.
        ///
        /// Deliberately NOT instance ids. The track owner correlates on (feed identity, sourceKey), so a
        /// local counter is sufficient - and using a counter proves the point that keys are local: this
        /// sensor's key 1 and the legacy feed's key 1 are different objects, and the owner keeps them as
        /// separate tracks. An instance id would have hidden that by being globally unique for the wrong
        /// reason.
        /// </summary>
        private readonly Dictionary<int, int> localKeys = new Dictionary<int, int>(64);
        private int nextLocalKey = 1;

        private MavRadarSignature[] candidates = new MavRadarSignature[0];
        private readonly List<MavTrackObservation> contacts = new List<MavTrackObservation>(32);
        private float nextScanTime;
        private float nextCandidateRescanTime;
        private bool hasRescannedOnce;

        public bool IsFeedActive
        {
            get { return enableRadar && isActiveAndEnabled; }
        }

        /// <summary>Provenance. Category only - the owner assigns this feed's identity separately.</summary>
        public MavTrackSource FeedSource
        {
            get { return MavTrackSource.Radar; }
        }

        private void OnEnable()
        {
            if (owner == null)
                owner = GetComponent<MavTargetTrackOwner>();
            if (boresight == null)
                boresight = transform;

            if (owner != null)
            {
                owner.RegisterFeed(this);
                debugRegistered = true;
            }

            nextScanTime = 0f;
            nextCandidateRescanTime = 0f;
            hasRescannedOnce = false;
        }

        private void OnDisable()
        {
            if (owner != null)
                owner.UnregisterFeed(this);
            debugRegistered = false;
            contacts.Clear();
            debugContactCount = 0;
        }

        /// <summary>
        /// Reports this scan's contacts.
        ///
        /// Scanning is throttled to <see cref="scanIntervalSeconds"/> independently of how often the
        /// owner asks. Between scans the last scan's contacts are reported unchanged rather than
        /// recomputed, which is what gives the sensor a real revisit rate instead of silently tracking
        /// at the owner's sample rate.
        /// </summary>
        public int CollectObservations(List<MavTrackObservation> into)
        {
            if (into == null || !IsFeedActive)
                return 0;

            if (Time.unscaledTime >= nextScanTime)
            {
                nextScanTime = Time.unscaledTime + Mathf.Max(0.02f, scanIntervalSeconds);
                Scan();
            }

            for (int i = 0; i < contacts.Count; i++)
                into.Add(contacts[i]);

            return contacts.Count;
        }

        private void Scan()
        {
            debugScanCount++;
            contacts.Clear();
            debugRejectedRange = 0;
            debugRejectedAngle = 0;
            debugRejectedLineOfSight = 0;
            debugRejectedOwnship = 0;
            debugNearestContactRange = 0f;
            debugNearestContactName = "none";

            RescanCandidatesIfDue();

            MavRadarScanVolume volume = scanVolume.Sanitized();
            Transform reference = boresight != null ? boresight : transform;
            Vector3 sensorPosition = reference.position;
            Quaternion sensorRotation = reference.rotation;

            Rigidbody ownBody = GetComponentInParent<Rigidbody>();
            Vector3 ownVelocity = ownBody != null ? ownBody.linearVelocity : Vector3.zero;
            bool haveOwnVelocity = ownBody != null;

            float nearest = float.MaxValue;

            for (int i = 0; i < candidates.Length; i++)
            {
                MavRadarSignature candidate = candidates[i];
                if (candidate == null || candidate.isDestroyed || !candidate.isActiveAndEnabled)
                    continue;

                if (IsOwnAircraft(candidate.transform))
                {
                    debugRejectedOwnship++;
                    continue;
                }

                Rigidbody targetBody = candidate.GetComponentInParent<Rigidbody>();
                bool haveVelocities = haveOwnVelocity && targetBody != null;
                Vector3 targetVelocity = targetBody != null ? targetBody.linearVelocity : Vector3.zero;

                MavRadarGeometry geometry = MavRadarScanVolume.Measure(
                    sensorPosition, sensorRotation, candidate.transform.position,
                    haveVelocities, ownVelocity, targetVelocity);

                float effectiveRange = volume.EffectiveMaxRange(SizeFactor(candidate));

                // Range and angle are rejected separately so the debug counters say WHY nothing was
                // seen, which a single "not detected" cannot.
                if (geometry.rangeMeters < volume.minRangeMeters || geometry.rangeMeters > effectiveRange)
                {
                    debugRejectedRange++;
                    continue;
                }

                if (geometry.azimuthDeg > volume.azimuthHalfAngleDeg
                    || Mathf.Abs(geometry.elevationDeg) > volume.elevationHalfAngleDeg)
                {
                    debugRejectedAngle++;
                    continue;
                }

                if (requireLineOfSight && IsLineOfSightBlocked(sensorPosition, candidate.transform.position))
                {
                    debugRejectedLineOfSight++;
                    continue;
                }

                float quality01 = volume.Quality01(geometry, effectiveRange);

                MavTrackObservation o = new MavTrackObservation();
                o.sourceKey = LocalKeyFor(candidate);
                o.source = MavTrackSource.Radar;
                o.quality = ResolveQuality(quality01, targetBody != null);
                o.position = candidate.transform.position;
                o.hasVelocity = targetBody != null;
                o.velocityMps = targetVelocity;
                o.displayName = candidate.displayName;
                o.team = candidate.team;
                o.isAirTarget = candidate.isAirTarget;
                contacts.Add(o);

                if (geometry.rangeMeters < nearest)
                {
                    nearest = geometry.rangeMeters;
                    debugNearestContactRange = geometry.rangeMeters;
                    debugNearestContactName = candidate.displayName;
                }
            }

            debugContactCount = contacts.Count;
        }

        /// <summary>
        /// Quality this sensor is willing to claim.
        ///
        /// <see cref="MavTrackQuality.Locked"/> is never reported: a lock is a maintained commitment
        /// with meaning for weapons, and lock semantics belong to a later phase. Reporting Locked here
        /// would let a future launcher believe something no system has actually established.
        /// </summary>
        private MavTrackQuality ResolveQuality(float quality01, bool haveVelocity)
        {
            if (!haveVelocity)
                return MavTrackQuality.Coarse;
            return quality01 >= trackedQualityThreshold ? MavTrackQuality.Tracked : MavTrackQuality.Coarse;
        }

        /// <summary>
        /// Synthetic target size factor. Reads the existing gameplay marker's abstract cross-section
        /// value, which is a tuning number rather than a measured RCS, and clamps it into a usable
        /// range. A factor of 1 means "reference sized" and returns the configured base range.
        /// </summary>
        private static float SizeFactor(MavRadarSignature candidate)
        {
            if (candidate == null)
                return 1f;
            return Mathf.Clamp(candidate.radarCrossSectionSqm / 5f, 0.01f, 20f);
        }

        /// <summary>
        /// Assigns and remembers this sensor's own local key for an object.
        ///
        /// Stable for as long as the sensor keeps the entry, which is what lets the owner correlate
        /// repeated observations of the same object into one track.
        /// </summary>
        private int LocalKeyFor(MavRadarSignature candidate)
        {
            int instanceId = candidate.GetInstanceID();
            int key;
            if (localKeys.TryGetValue(instanceId, out key))
                return key;

            key = nextLocalKey++;
            localKeys[instanceId] = key;
            return key;
        }

        private bool IsOwnAircraft(Transform candidateTransform)
        {
            if (!excludeOwnAircraft || candidateTransform == null)
                return false;
            return candidateTransform.root == transform.root;
        }

        /// <summary>
        /// Whether something on a blocking layer sits between sensor and target.
        ///
        /// A zero mask means "nothing blocks", which is treated as unobstructed rather than as
        /// everything-blocks: an unconfigured mask must not silently blind the sensor.
        /// </summary>
        private bool IsLineOfSightBlocked(Vector3 from, Vector3 to)
        {
            if (lineOfSightBlockers.value == 0)
                return false;

            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 0.01f)
                return false;

            return Physics.Raycast(from, delta / distance, distance, lineOfSightBlockers,
                                   QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        /// Finds candidate objects, throttled and independent of scan cadence.
        ///
        /// This is a scene query, and it is here for the same reason the legacy feed has one: nothing
        /// in Maverick publishes a registry of radar-visible objects yet. The difference is what happens
        /// to the result - these are CANDIDATES, subject to envelope, range and line-of-sight tests,
        /// not contacts. When an object registry exists this method is the only thing that changes.
        ///
        /// The time gate does not depend on whether the previous scan found anything: "found nothing"
        /// is a cached result like any other.
        /// </summary>
        private void RescanCandidatesIfDue()
        {
            if (hasRescannedOnce && Time.unscaledTime < nextCandidateRescanTime)
                return;

            nextCandidateRescanTime = Time.unscaledTime + Mathf.Max(0.1f, candidateRescanInterval);
            hasRescannedOnce = true;
            debugCandidateRescanCount++;

            candidates = FindObjectsOfType<MavRadarSignature>(false);
            debugCandidateCount = candidates.Length;
        }

        /// <summary>Test seam: forces one scan immediately, ignoring cadence.</summary>
        public void ScanNowForTesting()
        {
            Scan();
        }

        /// <summary>Test seam: this scan's contacts, for validation to inspect.</summary>
        public int ContactCount
        {
            get { return contacts.Count; }
        }

        public MavTrackObservation GetContact(int index)
        {
            if (index < 0 || index >= contacts.Count)
                return new MavTrackObservation();
            return contacts[index];
        }
    }
}
