#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using MaverickFresh.Combat.Sensors;
using MaverickFresh.Combat.Targeting;
using UnityEditor;
using UnityEngine;

namespace MaverickFresh.Combat.EditorTools
{
    /// <summary>
    /// Deterministic validation for Radar Core R0.
    ///
    /// The geometry cases run against <see cref="MavRadarScanVolume.Measure"/> directly - positions, a
    /// rotation and velocities in, measurement out - so every one of them is exact and needs no scene,
    /// no physics and no running game. The sensor cases build a small synthetic scene of markers, which
    /// is the only way to exercise candidate discovery and ownship exclusion honestly.
    ///
    /// Deliberately not tested, because none of it exists: lock semantics, seekers, guidance, datalink
    /// fusion, ECM, clutter, PRF, notching.
    /// </summary>
    public static class MavRadarCoreValidation
    {
        [MenuItem("Maverick/Combat/Run Radar Core Validation")]
        public static void RunFromMenu()
        {
            int passed, failed;
            string report = RunAll(out passed, out failed);
            Debug.Log(report);
            EditorUtility.DisplayDialog("Radar Core Validation",
                (failed == 0 ? "PASS" : "FAIL") + "  passed=" + passed + " failed=" + failed, "OK");
        }

        public static string RunAll(out int passed, out int failed)
        {
            passed = 0;
            failed = 0;
            StringBuilder report = new StringBuilder();
            report.AppendLine("Maverick Radar Core Validation R0");
            report.AppendLine("=================================");

            ValidateGeometry(report, ref passed, ref failed);
            ValidateEnvelope(report, ref passed, ref failed);
            ValidateSensor(report, ref passed, ref failed);
            ValidateFeedSeparation(report, ref passed, ref failed);

            report.AppendLine("RESULT: " + (failed == 0 ? "PASS" : "FAIL")
                              + " passed=" + passed + " failed=" + failed);
            return report.ToString();
        }

        // ---- pure geometry ---------------------------------------------------------------------
        private static void ValidateGeometry(StringBuilder report, ref int passed, ref int failed)
        {
            Quaternion facingZ = Quaternion.identity;
            Vector3 origin = Vector3.zero;

            MavRadarGeometry ahead = MavRadarScanVolume.Measure(
                origin, facingZ, new Vector3(0f, 0f, 1000f), false, Vector3.zero, Vector3.zero);
            Record(Mathf.Approximately(ahead.rangeMeters, 1000f)
                   && ahead.azimuthDeg < 0.01f
                   && Mathf.Abs(ahead.elevationDeg) < 0.01f,
                   "R-001", "a target on boresight measures zero azimuth and elevation at true range",
                   report, ref passed, ref failed);

            MavRadarGeometry right = MavRadarScanVolume.Measure(
                origin, facingZ, new Vector3(1000f, 0f, 1000f), false, Vector3.zero, Vector3.zero);
            Record(Mathf.Abs(right.azimuthDeg - 45f) < 0.01f && Mathf.Abs(right.elevationDeg) < 0.01f,
                   "R-002", "a target 45 degrees right measures 45 degrees azimuth, zero elevation",
                   report, ref passed, ref failed);

            MavRadarGeometry left = MavRadarScanVolume.Measure(
                origin, facingZ, new Vector3(-1000f, 0f, 1000f), false, Vector3.zero, Vector3.zero);
            Record(Mathf.Abs(left.azimuthDeg - 45f) < 0.01f,
                   "R-002b", "azimuth is unsigned, so left and right of boresight measure the same",
                   report, ref passed, ref failed);

            MavRadarGeometry high = MavRadarScanVolume.Measure(
                origin, facingZ, new Vector3(0f, 1000f, 1000f), false, Vector3.zero, Vector3.zero);
            Record(Mathf.Abs(high.elevationDeg - 45f) < 0.01f && high.azimuthDeg < 0.01f,
                   "R-003", "a target 45 degrees above measures positive elevation, zero azimuth",
                   report, ref passed, ref failed);

            MavRadarGeometry low = MavRadarScanVolume.Measure(
                origin, facingZ, new Vector3(0f, -1000f, 1000f), false, Vector3.zero, Vector3.zero);
            Record(Mathf.Abs(low.elevationDeg + 45f) < 0.01f,
                   "R-003b", "elevation is signed, so below boresight measures negative",
                   report, ref passed, ref failed);

            MavRadarGeometry behind = MavRadarScanVolume.Measure(
                origin, facingZ, new Vector3(0f, 0f, -1000f), false, Vector3.zero, Vector3.zero);
            Record(behind.azimuthDeg > 179f,
                   "R-004", "a target directly astern measures near 180 degrees azimuth, never boresight",
                   report, ref passed, ref failed);

            // Rotation must be respected: the same world target, sensor turned to face it.
            Quaternion facingX = Quaternion.Euler(0f, 90f, 0f);
            MavRadarGeometry rotated = MavRadarScanVolume.Measure(
                origin, facingX, new Vector3(1000f, 0f, 0f), false, Vector3.zero, Vector3.zero);
            Record(rotated.azimuthDeg < 0.01f,
                   "R-005", "geometry is measured in the sensor frame, so rotating to face a target puts it on boresight",
                   report, ref passed, ref failed);

            // Closure: sensor closing on a stationary target is positive.
            MavRadarGeometry closing = MavRadarScanVolume.Measure(
                origin, facingZ, new Vector3(0f, 0f, 1000f), true,
                new Vector3(0f, 0f, 300f), Vector3.zero);
            Record(closing.hasClosure && Mathf.Abs(closing.closureMps - 300f) < 0.01f,
                   "R-006", "closing on a stationary target reports positive closure",
                   report, ref passed, ref failed);

            MavRadarGeometry opening = MavRadarScanVolume.Measure(
                origin, facingZ, new Vector3(0f, 0f, 1000f), true,
                Vector3.zero, new Vector3(0f, 0f, 300f));
            Record(opening.hasClosure && opening.closureMps < -299f,
                   "R-006b", "a target running away reports negative closure",
                   report, ref passed, ref failed);

            MavRadarGeometry noVel = MavRadarScanVolume.Measure(
                origin, facingZ, new Vector3(0f, 0f, 1000f), false, Vector3.zero, Vector3.zero);
            Record(!noVel.hasClosure && Mathf.Approximately(noVel.closureMps, 0f),
                   "R-006c", "closure is not invented when no velocity was supplied",
                   report, ref passed, ref failed);
        }

        // ---- envelope decisions ----------------------------------------------------------------
        private static void ValidateEnvelope(StringBuilder report, ref int passed, ref int failed)
        {
            MavRadarScanVolume v;
            v.minRangeMeters = 100f;
            v.maxRangeMeters = 10000f;
            v.azimuthHalfAngleDeg = 60f;
            v.elevationHalfAngleDeg = 30f;
            v = v.Sanitized();

            MavRadarGeometry inside = new MavRadarGeometry();
            inside.rangeMeters = 5000f;
            inside.azimuthDeg = 30f;
            inside.elevationDeg = 10f;
            Record(v.Contains(inside, v.EffectiveMaxRange(1f)),
                   "R-010", "a contact inside range and angle is contained",
                   report, ref passed, ref failed);

            MavRadarGeometry tooClose = inside;
            tooClose.rangeMeters = 50f;
            Record(!v.Contains(tooClose, v.EffectiveMaxRange(1f)),
                   "R-011", "a contact inside minimum range is rejected",
                   report, ref passed, ref failed);

            MavRadarGeometry tooFar = inside;
            tooFar.rangeMeters = 20000f;
            Record(!v.Contains(tooFar, v.EffectiveMaxRange(1f)),
                   "R-012", "a contact beyond effective range is rejected",
                   report, ref passed, ref failed);

            MavRadarGeometry wideAz = inside;
            wideAz.azimuthDeg = 61f;
            Record(!v.Contains(wideAz, v.EffectiveMaxRange(1f)),
                   "R-013", "a contact outside the azimuth limit is rejected",
                   report, ref passed, ref failed);

            MavRadarGeometry highEl = inside;
            highEl.elevationDeg = 31f;
            Record(!v.Contains(highEl, v.EffectiveMaxRange(1f)),
                   "R-014", "a contact above the elevation limit is rejected",
                   report, ref passed, ref failed);

            MavRadarGeometry lowEl = inside;
            lowEl.elevationDeg = -31f;
            Record(!v.Contains(lowEl, v.EffectiveMaxRange(1f)),
                   "R-014b", "the elevation limit is symmetric, so below is rejected too",
                   report, ref passed, ref failed);

            // Azimuth and elevation are separate limits, not one cone: 55 degrees azimuth is inside
            // while 31 degrees elevation is not, which a single cone angle could not express.
            MavRadarGeometry wideButLow = inside;
            wideButLow.azimuthDeg = 55f;
            wideButLow.elevationDeg = 5f;
            Record(v.Contains(wideButLow, v.EffectiveMaxRange(1f)),
                   "R-015", "azimuth and elevation are independent limits, not a single cone",
                   report, ref passed, ref failed);

            // Size scaling: bigger sees further, smaller sees less, reference is unchanged.
            Record(Mathf.Approximately(v.EffectiveMaxRange(1f), v.maxRangeMeters),
                   "R-016", "a reference-sized target uses the configured base range unchanged",
                   report, ref passed, ref failed);
            Record(v.EffectiveMaxRange(16f) > v.maxRangeMeters
                   && v.EffectiveMaxRange(0.1f) < v.maxRangeMeters,
                   "R-016b", "effective range grows with target size and shrinks with a smaller one",
                   report, ref passed, ref failed);
            Record(Mathf.Abs(v.EffectiveMaxRange(16f) - v.maxRangeMeters * 2f) < 1f,
                   "R-016c", "size scaling follows the fourth-root shape: sixteen times the size doubles range",
                   report, ref passed, ref failed);

            // Quality falls off with range and with angle, and stays bounded.
            MavRadarGeometry near = inside;
            near.rangeMeters = 500f;
            near.offBoresightDeg = 0f;
            MavRadarGeometry far = inside;
            far.rangeMeters = 9500f;
            far.offBoresightDeg = 0f;
            float qNear = v.Quality01(near, v.EffectiveMaxRange(1f));
            float qFar = v.Quality01(far, v.EffectiveMaxRange(1f));
            Record(qNear > qFar && qNear <= 1f && qFar >= 0f,
                   "R-017", "quality falls off with range and stays within [0,1]",
                   report, ref passed, ref failed);

            MavRadarGeometry offAxis = near;
            offAxis.offBoresightDeg = 55f;
            Record(v.Quality01(offAxis, v.EffectiveMaxRange(1f)) < qNear,
                   "R-017b", "quality falls off away from boresight",
                   report, ref passed, ref failed);

            // A nonsense envelope is clamped rather than producing negative geometry.
            MavRadarScanVolume bad;
            bad.minRangeMeters = -50f;
            bad.maxRangeMeters = -100f;
            bad.azimuthHalfAngleDeg = 0f;
            bad.elevationHalfAngleDeg = 900f;
            MavRadarScanVolume fixedUp = bad.Sanitized();
            Record(fixedUp.minRangeMeters >= 0f
                   && fixedUp.maxRangeMeters > fixedUp.minRangeMeters
                   && fixedUp.azimuthHalfAngleDeg >= 0.5f
                   && fixedUp.elevationHalfAngleDeg <= 90f,
                   "R-018", "a nonsense envelope is clamped into a usable shape",
                   report, ref passed, ref failed);
        }

        // ---- the sensor against synthetic markers -----------------------------------------------
        private static void ValidateSensor(StringBuilder report, ref int passed, ref int failed)
        {
            GameObject sensorHost = new GameObject("MavRadarValidationSensor");
            GameObject ahead = null;
            GameObject astern = null;
            GameObject tooFar = null;

            try
            {
                sensorHost.transform.position = Vector3.zero;
                sensorHost.transform.rotation = Quaternion.identity;

                MavTargetTrackOwner owner = sensorHost.AddComponent<MavTargetTrackOwner>();
                MavRadarSensor radar = sensorHost.AddComponent<MavRadarSensor>();
                radar.owner = owner;
                radar.requireLineOfSight = false;
                radar.scanVolume = MavRadarScanVolume.Default;
                radar.scanIntervalSeconds = 0f;

                // Ownship marker, deliberately on the sensor's own object.
                sensorHost.AddComponent<MavRadarSignature>().displayName = "OWNSHIP";

                ahead = MakeMarker("AHEAD", new Vector3(0f, 0f, 4000f));
                astern = MakeMarker("ASTERN", new Vector3(0f, 0f, -4000f));
                tooFar = MakeMarker("TOOFAR", new Vector3(0f, 0f, 500000f));

                radar.ScanNowForTesting();

                Record(radar.ContactCount == 1,
                       "R-020", "only the target inside the envelope becomes a contact",
                       report, ref passed, ref failed);

                bool aheadSeen = false;
                for (int i = 0; i < radar.ContactCount; i++)
                    if (radar.GetContact(i).displayName == "AHEAD")
                        aheadSeen = true;
                Record(aheadSeen, "R-020b", "the contact is the target ahead, not one of the rejects",
                       report, ref passed, ref failed);

                Record(radar.debugRejectedOwnship >= 1,
                       "R-021", "the ownship marker is rejected, so a platform never tracks itself",
                       report, ref passed, ref failed);

                Record(radar.debugRejectedAngle >= 1,
                       "R-022", "the target astern is rejected on angle",
                       report, ref passed, ref failed);

                Record(radar.debugRejectedRange >= 1,
                       "R-023", "the target beyond effective range is rejected on range",
                       report, ref passed, ref failed);

                MavTrackObservation contact = radar.GetContact(0);
                Record(contact.source == MavTrackSource.Radar,
                       "R-024", "the sensor declares Radar provenance",
                       report, ref passed, ref failed);
                Record(contact.quality != MavTrackQuality.Locked,
                       "R-025", "the sensor never claims a Locked track, because lock semantics are a later phase",
                       report, ref passed, ref failed);
                Record(contact.sourceKey != 0,
                       "R-026", "the contact carries a radar-local source key",
                       report, ref passed, ref failed);

                // Local keys are stable across scans for the same object.
                int keyFirst = contact.sourceKey;
                radar.ScanNowForTesting();
                Record(radar.ContactCount == 1 && radar.GetContact(0).sourceKey == keyFirst,
                       "R-027", "a radar-local key is stable across scans for the same object",
                       report, ref passed, ref failed);

                // Keys are LOCAL: the first one is a small counter value, not a global instance id.
                Record(keyFirst < 1000,
                       "R-027b", "radar keys are a local counter rather than a global instance id",
                       report, ref passed, ref failed);

                // Disabling the radar makes it report nothing and deactivates it as a feed.
                radar.enableRadar = false;
                List<MavTrackObservation> sink = new List<MavTrackObservation>();
                int produced = radar.CollectObservations(sink);
                Record(produced == 0 && sink.Count == 0 && !radar.IsFeedActive,
                       "R-028", "a disabled radar produces nothing and reports itself inactive",
                       report, ref passed, ref failed);

                radar.enableRadar = true;

                // Candidate rescan is throttled, and the gate does not depend on finding anything.
                int rescansBefore = radar.debugCandidateRescanCount;
                radar.ScanNowForTesting();
                radar.ScanNowForTesting();
                radar.ScanNowForTesting();
                Record(radar.debugCandidateRescanCount == rescansBefore,
                       "R-029", "candidate discovery is throttled independently of scan cadence",
                       report, ref passed, ref failed);
            }
            finally
            {
                if (ahead != null) Object.DestroyImmediate(ahead);
                if (astern != null) Object.DestroyImmediate(astern);
                if (tooFar != null) Object.DestroyImmediate(tooFar);
                Object.DestroyImmediate(sensorHost);
            }
        }

        // ---- two feeds, identical local keys, separate tracks -----------------------------------
        private static void ValidateFeedSeparation(StringBuilder report, ref int passed, ref int failed)
        {
            GameObject host = new GameObject("MavRadarFeedSeparationHost");
            try
            {
                MavTargetTrackOwner owner = host.AddComponent<MavTargetTrackOwner>();

                // Two feeds of DIFFERENT categories, both using local key 1 for different objects.
                // The radar feed and the legacy feed must not merge, and the owner must keep the
                // radar's key 1 distinct from the legacy feed's key 1.
                KeyedFeed radarLike = new KeyedFeed(MavTrackSource.Radar, 1, new Vector3(0f, 0f, 1000f), "RADAR-1");
                KeyedFeed legacyLike = new KeyedFeed(MavTrackSource.Legacy, 1, new Vector3(0f, 0f, 9000f), "LEGACY-1");
                owner.RegisterFeed(radarLike);
                owner.RegisterFeed(legacyLike);
                owner.SweepNowForTesting();

                Record(owner.TrackCount == 2,
                       "R-030", "a radar feed and the legacy feed using the same local key produce two separate tracks",
                       report, ref passed, ref failed);

                int radarTrack, legacyTrack;
                bool okR = owner.TryResolveTrackId(radarLike, 1, out radarTrack);
                bool okL = owner.TryResolveTrackId(legacyLike, 1, out legacyTrack);
                Record(okR && okL && radarTrack != legacyTrack && radarTrack != 0 && legacyTrack != 0,
                       "R-030b", "each feed's key 1 resolves to its own track",
                       report, ref passed, ref failed);

                MavTargetTrackData radarData, legacyData;
                owner.TryGetTrackById(radarTrack, out radarData);
                owner.TryGetTrackById(legacyTrack, out legacyData);
                Record(radarData.source == MavTrackSource.Radar && legacyData.source == MavTrackSource.Legacy,
                       "R-031", "provenance distinguishes the two tracks, stamped from their feeds",
                       report, ref passed, ref failed);

                // The legacy feed remains a first-class producer: removing the radar leaves it intact.
                owner.UnregisterFeed(radarLike);
                owner.SweepNowForTesting();
                int legacyAfter;
                Record(owner.TryResolveTrackId(legacyLike, 1, out legacyAfter) && legacyAfter == legacyTrack,
                       "R-032", "unregistering the radar leaves the legacy feed's track untouched",
                       report, ref passed, ref failed);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>A feed that reports exactly one observation under a caller-chosen local key.</summary>
        private sealed class KeyedFeed : IMavTargetObservationFeed
        {
            private readonly MavTrackSource source;
            private readonly int key;
            private readonly Vector3 position;
            private readonly string label;

            public KeyedFeed(MavTrackSource source, int key, Vector3 position, string label)
            {
                this.source = source;
                this.key = key;
                this.position = position;
                this.label = label;
            }

            public bool IsFeedActive { get { return true; } }
            public MavTrackSource FeedSource { get { return source; } }

            public int CollectObservations(List<MavTrackObservation> into)
            {
                MavTrackObservation o = new MavTrackObservation();
                o.sourceKey = key;
                o.source = source;
                o.quality = MavTrackQuality.Coarse;
                o.position = position;
                o.displayName = label;
                o.team = 1;
                o.isAirTarget = true;
                into.Add(o);
                return 1;
            }
        }

        private static GameObject MakeMarker(string name, Vector3 position)
        {
            GameObject go = new GameObject("MavRadarValidationTarget_" + name);
            go.transform.position = position;
            MavRadarSignature sig = go.AddComponent<MavRadarSignature>();
            sig.displayName = name;
            sig.team = 2;
            sig.isAirTarget = true;
            return go;
        }

        private static void Record(bool condition, string id, string description,
                                   StringBuilder report, ref int passed, ref int failed)
        {
            if (condition)
            {
                passed++;
                report.AppendLine("  PASS  " + id + "  " + description);
            }
            else
            {
                failed++;
                report.AppendLine("  FAIL  " + id + "  " + description);
            }
        }
    }
}
#endif
