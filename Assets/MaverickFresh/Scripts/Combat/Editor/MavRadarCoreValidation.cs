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
            ValidateRearQuadrant(report, ref passed, ref failed);
            ValidateEnvelope(report, ref passed, ref failed);
            ValidateWideVolume(report, ref passed, ref failed);
            ValidateSensor(report, ref passed, ref failed);
            ValidateScanCadence(report, ref passed, ref failed);
            ValidateLocalKeyLifecycle(report, ref passed, ref failed);
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

        // ---- rear quadrant, the case the first implementation got wrong -------------------------
        //
        // The original Measure clamped local.z to a positive epsilon before Atan2 and then applied a
        // "180 - azimuth" rear correction. Both halves cancelled: every rear target read as 90 degrees.
        // The only rear case tested was directly astern, which happened to still come out at 180, so
        // the entire rear quadrant was wrong and nothing caught it.
        private static void ValidateRearQuadrant(StringBuilder report, ref int passed, ref int failed)
        {
            Quaternion facing = Quaternion.identity;
            Vector3 origin = Vector3.zero;

            Record(Mathf.Abs(Az(origin, facing, new Vector3(1000f, 0f, 1000f)) - 45f) < 0.01f,
                   "R-040", "front-right measures 45 degrees azimuth",
                   report, ref passed, ref failed);

            Record(Mathf.Abs(Az(origin, facing, new Vector3(-1000f, 0f, 1000f)) - 45f) < 0.01f,
                   "R-041", "front-left measures 45 degrees azimuth",
                   report, ref passed, ref failed);

            Record(Mathf.Abs(Az(origin, facing, new Vector3(1000f, 0f, -1000f)) - 135f) < 0.01f,
                   "R-042", "rear-right measures 135 degrees azimuth, not 90",
                   report, ref passed, ref failed);

            Record(Mathf.Abs(Az(origin, facing, new Vector3(-1000f, 0f, -1000f)) - 135f) < 0.01f,
                   "R-043", "rear-left measures 135 degrees azimuth, not 90",
                   report, ref passed, ref failed);

            Record(Mathf.Abs(Az(origin, facing, new Vector3(0f, 0f, -1000f)) - 180f) < 0.01f,
                   "R-044", "directly astern measures 180 degrees azimuth",
                   report, ref passed, ref failed);

            // Near-astern but off axis: atan2(100, -1000) is about 174.29 degrees. The old code reported
            // 90 here, which is the difference between "behind me" and "almost on the nose".
            float nearAstern = Az(origin, facing, new Vector3(100f, 0f, -1000f));
            Record(Mathf.Abs(nearAstern - 174.29f) < 0.05f,
                   "R-045", "near-astern off-axis measures about 174 degrees, not 90",
                   report, ref passed, ref failed);

            Record(Mathf.Abs(Az(origin, facing, new Vector3(-100f, 0f, -1000f)) - nearAstern) < 0.01f,
                   "R-045b", "near-astern is symmetric left and right",
                   report, ref passed, ref failed);

            // Azimuth must be monotonic as a target swings from the nose to astern. A sign or clamp
            // error shows up here as a fold rather than a single wrong value.
            float a0 = Az(origin, facing, new Vector3(0f, 0f, 1000f));
            float a45 = Az(origin, facing, new Vector3(1000f, 0f, 1000f));
            float a90 = Az(origin, facing, new Vector3(1000f, 0f, 0f));
            float a135 = Az(origin, facing, new Vector3(1000f, 0f, -1000f));
            float a180 = Az(origin, facing, new Vector3(0f, 0f, -1000f));
            Record(a0 < a45 && a45 < a90 && a90 < a135 && a135 < a180,
                   "R-046", "azimuth increases monotonically from boresight round to astern",
                   report, ref passed, ref failed);

            // A rotated sensor must measure the rear quadrant in its own frame. Sensor faces world +X,
            // target sits behind-right of the sensor, so it must read 135 regardless of world axes.
            Quaternion facingX = Quaternion.Euler(0f, 90f, 0f);
            // Sensor forward is world +X, sensor right is world -Z. Behind-right is world (-1000, 0, +1000).
            Record(Mathf.Abs(Az(origin, facingX, new Vector3(-1000f, 0f, 1000f)) - 135f) < 0.01f,
                   "R-047", "a rotated sensor measures the rear quadrant in its own frame",
                   report, ref passed, ref failed);

            // Elevation must stay correct for a rear target: astern and above is still +45.
            MavRadarGeometry rearHigh = MavRadarScanVolume.Measure(
                origin, facing, new Vector3(0f, 1000f, -1000f), false, Vector3.zero, Vector3.zero);
            Record(Mathf.Abs(rearHigh.elevationDeg - 45f) < 0.01f && rearHigh.azimuthDeg > 179f,
                   "R-048", "a target astern and above reports 180 azimuth with +45 elevation",
                   report, ref passed, ref failed);
        }

        private static float Az(Vector3 origin, Quaternion rotation, Vector3 target)
        {
            return MavRadarScanVolume.Measure(origin, rotation, target, false, Vector3.zero, Vector3.zero).azimuthDeg;
        }

        // ---- a wide volume must still respect its own limit -------------------------------------
        private static void ValidateWideVolume(StringBuilder report, ref int passed, ref int failed)
        {
            // This is the case the azimuth bug would have let through. With a +/-100 degree volume, a
            // target at a true 135 degrees is outside the limit - but the old code reported it as 90,
            // which the volume would have admitted.
            MavRadarScanVolume wide;
            wide.minRangeMeters = 0f;
            wide.maxRangeMeters = 50000f;
            wide.azimuthHalfAngleDeg = 100f;
            wide.elevationHalfAngleDeg = 60f;
            wide = wide.Sanitized();

            MavRadarGeometry rearRight = MavRadarScanVolume.Measure(
                Vector3.zero, Quaternion.identity, new Vector3(1000f, 0f, -1000f),
                false, Vector3.zero, Vector3.zero);

            Record(rearRight.azimuthDeg > wide.azimuthHalfAngleDeg,
                   "R-050", "a true 135 degree target is outside a 100 degree half-angle volume",
                   report, ref passed, ref failed);

            Record(!wide.Contains(rearRight, wide.EffectiveMaxRange(1f)),
                   "R-051", "a volume wider than 90 degrees still rejects a target outside its limit",
                   report, ref passed, ref failed);

            // And it must admit one genuinely inside the wide limit: 95 degrees is inside 100.
            MavRadarGeometry justInside = MavRadarScanVolume.Measure(
                Vector3.zero, Quaternion.identity,
                new Vector3(Mathf.Sin(95f * Mathf.Deg2Rad) * 1000f, 0f, Mathf.Cos(95f * Mathf.Deg2Rad) * 1000f),
                false, Vector3.zero, Vector3.zero);
            Record(Mathf.Abs(justInside.azimuthDeg - 95f) < 0.05f
                   && wide.Contains(justInside, wide.EffectiveMaxRange(1f)),
                   "R-052", "a wide volume admits a target just inside its limit, measured correctly",
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

        // ---- one scan, one observation batch ----------------------------------------------------
        //
        // The bug this covers: the sensor used to replay its cached contacts on every poll. The owner
        // stamps observedAtTime = now on every observation it receives - correctly - so one 1 Hz
        // measurement was presented as four fresh 4 Hz measurements. Track age never grew and staleness
        // was driven by owner polling rather than by when the sensor actually looked.
        private static void ValidateScanCadence(StringBuilder report, ref int passed, ref int failed)
        {
            GameObject host = new GameObject("MavRadarCadenceHost");
            GameObject target = null;
            try
            {
                MavTargetTrackOwner owner = host.AddComponent<MavTargetTrackOwner>();
                MavRadarSensor radar = host.AddComponent<MavRadarSensor>();
                radar.owner = owner;
                radar.requireLineOfSight = false;
                radar.excludeOwnAircraft = true;

                // Intentionally different cadences: radar every 1.0 s, owner polling four times faster.
                radar.scanIntervalSeconds = 1.0f;
                radar.candidateRescanInterval = 1.0f;
                radar.UseTestClock(0f);
                radar.ResetCadenceForTesting();

                target = MakeMarker("CADENCE", new Vector3(0f, 0f, 4000f));
                owner.RegisterFeed(radar);

                List<MavTrackObservation> sink = new List<MavTrackObservation>();

                // t = 0: a scan is due, so exactly one batch is emitted.
                int first = radar.CollectObservations(sink);
                Record(first == 1 && radar.debugScanCount == 1,
                       "R-060", "the first poll performs one scan and emits one observation batch",
                       report, ref passed, ref failed);

                // t = 0.25, 0.50, 0.75: owner polls, no scan due, nothing emitted.
                int emittedBetween = 0;
                for (int i = 0; i < 3; i++)
                {
                    radar.AdvanceTestClock(0.25f);
                    sink.Clear();
                    emittedBetween += radar.CollectObservations(sink);
                }
                Record(emittedBetween == 0,
                       "R-061", "polls between scans emit zero observations, so nothing is replayed",
                       report, ref passed, ref failed);
                Record(radar.debugScanCount == 1,
                       "R-061b", "polls between scans perform no additional scan",
                       report, ref passed, ref failed);
                Record(radar.debugPollsWithoutScan == 3,
                       "R-061c", "the suppressed polls are counted, so the cadence is observable",
                       report, ref passed, ref failed);

                // t = 1.0: the next scan is due and refreshes.
                radar.AdvanceTestClock(0.25f);
                sink.Clear();
                int second = radar.CollectObservations(sink);
                Record(second == 1 && radar.debugScanCount == 2,
                       "R-062", "the next due scan emits a fresh observation batch",
                       report, ref passed, ref failed);

                // Over four seconds of polling at 4 Hz there must be about four scans, not sixteen.
                for (int i = 0; i < 12; i++)
                {
                    radar.AdvanceTestClock(0.25f);
                    sink.Clear();
                    radar.CollectObservations(sink);
                }
                Record(radar.debugScanCount == 5,
                       "R-063", "four seconds of 4 Hz polling produces five 1 Hz scans, not sixteen",
                       report, ref passed, ref failed);
                Record(radar.debugObservationsEmitted == radar.debugScanCount,
                       "R-063b", "one observation per scan for one target: emissions never multiply with poll rate",
                       report, ref passed, ref failed);

                // The owner keeps the track across quiet polls rather than losing it.
                //
                // Driven through the OWNER here, not by polling the radar directly. One scan produces
                // one batch for whoever consumes it, so a direct CollectObservations would take the
                // scan the owner was going to get - which is correct behavior and was what made an
                // earlier version of this case fail against working code.
                radar.ResetCadenceForTesting();
                owner.ClearTracksForTesting();

                owner.SweepNowForTesting();
                int tracksAfterScan = owner.TrackCount;
                Record(tracksAfterScan == 1 && owner.debugObservationsLastSweep == 1,
                       "R-064", "an owner sweep that lands on a due scan receives one observation and one track",
                       report, ref passed, ref failed);

                radar.AdvanceTestClock(0.25f);
                owner.SweepNowForTesting();
                Record(owner.debugObservationsLastSweep == 0,
                       "R-064b", "an owner sweep between radar scans receives no observation",
                       report, ref passed, ref failed);
                Record(owner.TrackCount == 1,
                       "R-064c", "the track owner keeps the track across polls that carried no observation",
                       report, ref passed, ref failed);

                radar.AdvanceTestClock(1.0f);
                owner.SweepNowForTesting();
                Record(owner.debugObservationsLastSweep == 1 && owner.TrackCount == 1,
                       "R-064d", "the next due radar scan refreshes the same track rather than adding one",
                       report, ref passed, ref failed);

                // A disabled radar emits nothing even when a scan would be due.
                radar.AdvanceTestClock(10f);
                radar.enableRadar = false;
                sink.Clear();
                Record(radar.CollectObservations(sink) == 0,
                       "R-065", "a disabled radar emits nothing even when a scan is due",
                       report, ref passed, ref failed);
                radar.enableRadar = true;
                radar.ClearTestClock();
            }
            finally
            {
                if (target != null) Object.DestroyImmediate(target);
                Object.DestroyImmediate(host);
            }
        }

        // ---- radar-local key lifecycle ----------------------------------------------------------
        private static void ValidateLocalKeyLifecycle(StringBuilder report, ref int passed, ref int failed)
        {
            GameObject host = new GameObject("MavRadarKeyLifecycleHost");
            GameObject a = null;
            GameObject b = null;
            try
            {
                MavTargetTrackOwner owner = host.AddComponent<MavTargetTrackOwner>();
                MavRadarSensor radar = host.AddComponent<MavRadarSensor>();
                radar.owner = owner;
                radar.requireLineOfSight = false;
                radar.scanIntervalSeconds = 1.0f;
                radar.candidateRescanInterval = 1.0f;
                radar.UseTestClock(0f);
                radar.ResetCadenceForTesting();
                owner.RegisterFeed(radar);

                a = MakeMarker("TARGET-A", new Vector3(0f, 0f, 4000f));

                // Driven through the owner. The radar's own last-scan contacts are read for the key,
                // rather than polling the radar separately: one scan yields one batch, so a second
                // consumer would take the observation the owner needed.
                owner.SweepNowForTesting();
                Record(radar.ContactCount == 1, "R-070", "target A is detected",
                       report, ref passed, ref failed);
                int keyA = radar.ContactCount == 1 ? radar.GetContact(0).sourceKey : 0;

                int trackA;
                bool haveTrackA = owner.TryResolveTrackId(radar, keyA, out trackA);
                Record(haveTrackA && trackA != 0,
                       "R-070b", "target A has a track",
                       report, ref passed, ref failed);

                // A is destroyed and a different object takes its place.
                Object.DestroyImmediate(a);
                a = null;
                radar.AdvanceTestClock(1.5f);
                b = MakeMarker("TARGET-B", new Vector3(0f, 0f, 4200f));

                owner.SweepNowForTesting();
                Record(radar.ContactCount == 1, "R-071", "target B is detected after A was destroyed",
                       report, ref passed, ref failed);
                int keyB = radar.ContactCount == 1 ? radar.GetContact(0).sourceKey : 0;

                Record(keyB != 0 && keyB != keyA,
                       "R-072", "target B does not inherit A's radar-local key",
                       report, ref passed, ref failed);

                Record(radar.DebugPrunedLocalKeys >= 1,
                       "R-073", "the destroyed target's local key was pruned rather than retained forever",
                       report, ref passed, ref failed);

                int trackB;
                bool haveTrackB = owner.TryResolveTrackId(radar, keyB, out trackB);
                Record(haveTrackB && trackB != 0 && (!haveTrackA || trackB != trackA),
                       "R-074", "B gets its own track instead of continuing A's",
                       report, ref passed, ref failed);

                // A's key must no longer resolve to anything, so nothing can address the dead target.
                int strayA;
                Record(keyA == 0 || !owner.TryResolveTrackId(radar, keyA, out strayA)
                       || strayA == trackA,
                       "R-074b", "A's pruned key cannot resolve to B's track",
                       report, ref passed, ref failed);

                radar.ClearTestClock();
            }
            finally
            {
                if (a != null) Object.DestroyImmediate(a);
                if (b != null) Object.DestroyImmediate(b);
                Object.DestroyImmediate(host);
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
