using System.Collections.Generic;
using MaverickFresh.Combat.Targeting;
using UnityEngine;

namespace MaverickFresh.Combat.Legacy
{
    /// <summary>
    /// Presents today's live target markers as observations, so consumers can move onto tracks
    /// before any real sensor exists.
    ///
    /// This is THE quarantine boundary. It is the only place in the combat architecture that sweeps
    /// the scene for targets, and it exists so that everything downstream can be written against
    /// <see cref="MavTargetTrackOwner"/> rather than against `FindObjectsOfType`. When Radar Core
    /// lands, it registers its own feed and this one is deleted; nothing downstream changes.
    ///
    /// It reports <see cref="MavTrackQuality.Coarse"/> and <see cref="MavTrackSource.Legacy"/> on
    /// purpose. What it produces is ground truth with no sensor model behind it - no range limit, no
    /// aspect, no field of view, no detection at all - and calling that Tracked or Locked would
    /// dress up a scene query as a measurement. Coarse is the honest label, and it keeps the day a
    /// real sensor arrives an obvious upgrade rather than a silent one.
    ///
    /// It deliberately does not filter by team, range or visibility. Filtering is a sensor's job,
    /// and doing it here would hide the absence of a sensor.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MavLegacyTargetObservationFeed : MonoBehaviour, IMavTargetObservationFeed
    {
        [Header("Wiring")]
        [Tooltip("Owner this feed registers with. Resolved from this GameObject when left empty.")]
        public MavTargetTrackOwner owner;

        [Header("Sources")]
        [Tooltip("Include air markers (MavRadarSignature).")]
        public bool includeAirSignatures = true;

        [Tooltip("Include ground markers (MavCASTarget).")]
        public bool includeGroundTargets = true;

        [Tooltip("Rescan the scene for markers at most this often. Between rescans the cached marker set is reused.")]
        public float markerRescanInterval = 1.0f;

        /// <summary>
        /// Skips markers belonging to this feed's own aircraft.
        ///
        /// The player carries a MavRadarSignature of its own - MavInGameBootstrap installs one on the
        /// aircraft so other sensors can see it - so without this the owner tracks the ownship, and a
        /// gate run confirmed exactly that: one air marker, one track, the aircraft observing itself.
        ///
        /// This is platform self-exclusion, not a sensor filter. Range, aspect and field of view are
        /// deliberately left to a real sensor, but "a platform does not observe itself" is not a
        /// detection model, it is basic sanity, and leaving it out would force every later consumer
        /// to special-case the ownship.
        /// </summary>
        [Tooltip("Skip markers on this feed's own aircraft. A platform does not observe itself.")]
        public bool excludeOwnAircraft = true;

        [Header("Read-only state")]
        public int debugAirMarkers;
        public int debugGroundMarkers;
        public int debugObservationsLastCollect;
        public bool debugRegistered;

        /// <summary>
        /// How many times the scene has actually been swept. Exposed so the rescan THROTTLE can be
        /// validated rather than assumed: the first version of this class only honoured the timer when
        /// a previous scan had found something, so a scene with zero targets re-swept on every
        /// observation sample instead of at markerRescanInterval - the one case where the sweep is
        /// pure waste.
        /// </summary>
        public int debugRescanCount;

        private MavRadarSignature[] airMarkers = new MavRadarSignature[0];
        private MavCASTarget[] groundMarkers = new MavCASTarget[0];
        private float nextRescanTime;
        private bool hasScannedOnce;

        public bool IsFeedActive
        {
            get { return isActiveAndEnabled; }
        }

        public MavTrackSource FeedSource
        {
            get { return MavTrackSource.Legacy; }
        }

        private void OnEnable()
        {
            if (owner == null)
                owner = GetComponent<MavTargetTrackOwner>();

            if (owner != null)
            {
                owner.RegisterFeed(this);
                debugRegistered = true;
            }

            nextRescanTime = 0f;
            hasScannedOnce = false;
        }

        private void OnDisable()
        {
            if (owner != null)
                owner.UnregisterFeed(this);
            debugRegistered = false;
        }

        public int CollectObservations(List<MavTrackObservation> into)
        {
            if (into == null)
                return 0;

            RescanIfDue();

            int appended = 0;

            if (includeAirSignatures)
            {
                for (int i = 0; i < airMarkers.Length; i++)
                {
                    MavRadarSignature marker = airMarkers[i];
                    if (marker == null || marker.isDestroyed || !marker.isActiveAndEnabled)
                        continue;
                    if (IsOwnAircraft(marker.transform))
                        continue;

                    MavTrackObservation o = new MavTrackObservation();
                    o.sourceKey = marker.GetInstanceID();
                    o.source = MavTrackSource.Legacy;
                    o.quality = MavTrackQuality.Coarse;
                    o.position = marker.transform.position;
                    o.hasVelocity = TryReadVelocity(marker.gameObject, out o.velocityMps);
                    o.displayName = marker.displayName;
                    o.team = marker.team;
                    o.isAirTarget = marker.isAirTarget;
                    into.Add(o);
                    appended++;
                }
            }

            if (includeGroundTargets)
            {
                for (int i = 0; i < groundMarkers.Length; i++)
                {
                    MavCASTarget marker = groundMarkers[i];
                    if (marker == null || marker.isDestroyed || !marker.isActiveAndEnabled)
                        continue;
                    if (IsOwnAircraft(marker.transform))
                        continue;

                    MavTrackObservation o = new MavTrackObservation();
                    o.sourceKey = marker.GetInstanceID();
                    o.source = MavTrackSource.Legacy;
                    o.quality = MavTrackQuality.Coarse;
                    o.position = marker.transform.position;
                    o.hasVelocity = TryReadVelocity(marker.gameObject, out o.velocityMps);
                    o.displayName = marker.displayName;
                    o.team = marker.team;
                    o.isAirTarget = false;
                    into.Add(o);
                    appended++;
                }
            }

            debugObservationsLastCollect = appended;
            return appended;
        }

        /// <summary>
        /// Whether a marker belongs to this feed's own aircraft. Compared by transform root so a
        /// marker anywhere in the aircraft's hierarchy is excluded, not just one on the same object.
        /// </summary>
        private bool IsOwnAircraft(Transform markerTransform)
        {
            if (!excludeOwnAircraft || markerTransform == null)
                return false;
            return markerTransform.root == transform.root;
        }

        /// <summary>
        /// Reads velocity from the marker's own Rigidbody when it has one. Read-only: nothing here
        /// writes physics, and a marker without a Rigidbody simply reports no velocity, which the
        /// owner then differences between samples.
        /// </summary>
        private static bool TryReadVelocity(GameObject go, out Vector3 velocity)
        {
            velocity = Vector3.zero;
            if (go == null)
                return false;

            Rigidbody body = go.GetComponentInParent<Rigidbody>();
            if (body == null)
                return false;

            velocity = body.linearVelocity;
            return true;
        }

        /// <summary>
        /// The scene sweep, throttled. Markers are spawned and destroyed rarely, so re-walking every
        /// object every sample is pure waste; the per-marker null and destroyed checks above handle
        /// anything that disappears between rescans.
        /// </summary>
        private void RescanIfDue()
        {
            // The time gate is deliberately independent of what the previous scan FOUND. An earlier
            // version also required a non-empty cached marker set, which meant an empty scene - no
            // targets at all - fell through the gate and re-swept on every observation sample, at the
            // sweep rate rather than the rescan rate. "Found nothing" is a perfectly good scan result
            // and must be cached like any other.
            if (hasScannedOnce && Time.unscaledTime < nextRescanTime)
                return;

            nextRescanTime = Time.unscaledTime + Mathf.Max(0.1f, markerRescanInterval);
            hasScannedOnce = true;
            debugRescanCount++;

            if (includeAirSignatures)
            {
                airMarkers = FindObjectsOfType<MavRadarSignature>(false);
                debugAirMarkers = airMarkers.Length;
            }
            else
            {
                airMarkers = new MavRadarSignature[0];
                debugAirMarkers = 0;
            }

            if (includeGroundTargets)
            {
                groundMarkers = FindObjectsOfType<MavCASTarget>(false);
                debugGroundMarkers = groundMarkers.Length;
            }
            else
            {
                groundMarkers = new MavCASTarget[0];
                debugGroundMarkers = 0;
            }
        }
    }
}
