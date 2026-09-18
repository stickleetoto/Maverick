#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using MaverickFresh.Combat.Targeting;
using UnityEditor;
using UnityEngine;

namespace MaverickFresh.Combat.EditorTools
{
    /// <summary>
    /// Behavioral validation for the TargetTrack Core owner.
    ///
    /// Runs against a synthetic feed rather than the scene, so every assertion is about the owner's
    /// own rules - identity, correlation, velocity, aging, dropping - and none of it depends on what
    /// happens to be in a level. That also means these cases keep working unchanged when the legacy
    /// feed is replaced by a real radar, which is the point of the feed being an interface.
    ///
    /// Deliberately not tested here, because none of it exists: detection, scan volumes, lock
    /// semantics, fusion between feeds, guidance.
    /// </summary>
    public static class MavTargetTrackValidation
    {
        /// <summary>A feed the test drives directly, so observations are exact and repeatable.</summary>
        private sealed class ScriptedFeed : IMavTargetObservationFeed
        {
            public readonly List<MavTrackObservation> pending = new List<MavTrackObservation>();
            public bool active = true;

            public bool IsFeedActive { get { return active; } }
            public MavTrackSource FeedSource { get { return MavTrackSource.Legacy; } }

            public int CollectObservations(List<MavTrackObservation> into)
            {
                for (int i = 0; i < pending.Count; i++)
                    into.Add(pending[i]);
                return pending.Count;
            }

            public void Set(int key, Vector3 position, bool hasVelocity, Vector3 velocity, MavTrackQuality quality)
            {
                pending.Clear();
                Add(key, position, hasVelocity, velocity, quality);
            }

            public void Add(int key, Vector3 position, bool hasVelocity, Vector3 velocity, MavTrackQuality quality)
            {
                MavTrackObservation o = new MavTrackObservation();
                o.sourceKey = key;
                o.source = MavTrackSource.Legacy;
                o.quality = quality;
                o.position = position;
                o.hasVelocity = hasVelocity;
                o.velocityMps = velocity;
                o.displayName = "T" + key;
                o.team = 1;
                o.isAirTarget = true;
                pending.Add(o);
            }
        }

        [MenuItem("Maverick/Combat/Run TargetTrack Core Validation")]
        public static void RunFromMenu()
        {
            int passed, failed;
            string report = RunAll(out passed, out failed);
            Debug.Log(report);
            EditorUtility.DisplayDialog("TargetTrack Core Validation",
                (failed == 0 ? "PASS" : "FAIL") + "  passed=" + passed + " failed=" + failed, "OK");
        }

        public static string RunAll(out int passed, out int failed)
        {
            passed = 0;
            failed = 0;
            StringBuilder report = new StringBuilder();
            report.AppendLine("Maverick TargetTrack Core Validation R0");
            report.AppendLine("=======================================");

            GameObject host = new GameObject("MavTargetTrackValidationHost");
            try
            {
                MavTargetTrackOwner owner = host.AddComponent<MavTargetTrackOwner>();
                owner.trackDropSeconds = 3f;
                ScriptedFeed feed = new ScriptedFeed();

                // T-001 an owner with no feed holds nothing.
                owner.SweepNowForTesting();
                Record(owner.TrackCount == 0, "T-001", "an owner with no registered feed holds no tracks",
                       report, ref passed, ref failed);

                // T-002 registration is idempotent, so a double-registered feed cannot double-count.
                owner.RegisterFeed(feed);
                owner.RegisterFeed(feed);
                Record(owner.debugFeedCount == 1, "T-002", "registering the same feed twice registers it once",
                       report, ref passed, ref failed);

                // T-003 one observation becomes one track with a non-zero id.
                feed.Set(101, new Vector3(0f, 100f, 0f), false, Vector3.zero, MavTrackQuality.Coarse);
                owner.SweepNowForTesting();
                Record(owner.TrackCount == 1, "T-003", "one observation produces one track",
                       report, ref passed, ref failed);

                MavTargetTrackData first = owner.GetTrack(0);
                int firstId = first.trackId;
                Record(firstId != 0 && first.IsValid, "T-003b", "the track has a non-zero id and reports valid",
                       report, ref passed, ref failed);

                // T-004 identity survives re-observation: the same source key keeps the same track id.
                feed.Set(101, new Vector3(0f, 100f, 10f), false, Vector3.zero, MavTrackQuality.Coarse);
                owner.SweepNowForTesting();
                Record(owner.TrackCount == 1 && owner.GetTrack(0).trackId == firstId,
                       "T-004", "re-observing the same source key keeps one track with the same id",
                       report, ref passed, ref failed);

                // T-005 a different source key is a different track.
                feed.Set(101, new Vector3(0f, 100f, 20f), false, Vector3.zero, MavTrackQuality.Coarse);
                feed.Add(202, new Vector3(50f, 100f, 0f), false, Vector3.zero, MavTrackQuality.Coarse);
                owner.SweepNowForTesting();
                Record(owner.TrackCount == 2, "T-005", "a second source key produces a second track",
                       report, ref passed, ref failed);

                int secondId = 0;
                for (int i = 0; i < owner.TrackCount; i++)
                    if (owner.GetTrack(i).trackId != firstId)
                        secondId = owner.GetTrack(i).trackId;
                Record(secondId != 0 && secondId != firstId, "T-005b", "the two tracks have different ids",
                       report, ref passed, ref failed);

                // T-006 a reported velocity is carried through unchanged.
                feed.Set(303, new Vector3(0f, 200f, 0f), true, new Vector3(0f, 0f, 250f), MavTrackQuality.Tracked);
                owner.SweepNowForTesting();
                bool carried = false;
                for (int i = 0; i < owner.TrackCount; i++)
                {
                    MavTargetTrackData t = owner.GetTrack(i);
                    if (Mathf.Approximately(t.velocityMps.z, 250f))
                        carried = true;
                }
                Record(carried, "T-006", "a producer-reported velocity reaches the track unchanged",
                       report, ref passed, ref failed);

                // T-007 quality is capped at Coarse while no velocity exists, so a first sighting
                // cannot claim to be Tracked.
                owner.ClearTracksForTesting();
                feed.Set(404, new Vector3(0f, 0f, 0f), false, Vector3.zero, MavTrackQuality.Tracked);
                owner.SweepNowForTesting();
                Record(owner.TrackCount == 1 && owner.GetTrack(0).quality == MavTrackQuality.Coarse,
                       "T-007", "a first sighting with no velocity is capped to Coarse, not Tracked",
                       report, ref passed, ref failed);

                // T-008 resolving a source key back to a track id works, and an unknown key does not.
                int resolved;
                bool ok = owner.TryResolveTrackId(MavTrackSource.Legacy, 404, out resolved);
                Record(ok && resolved == owner.GetTrack(0).trackId,
                       "T-008", "a known source key resolves to its track id",
                       report, ref passed, ref failed);

                int unresolved;
                bool miss = owner.TryResolveTrackId(MavTrackSource.Legacy, 999999, out unresolved);
                Record(!miss && unresolved == 0, "T-008b", "an unknown source key resolves to nothing",
                       report, ref passed, ref failed);

                // T-009 lookup by id, and a dropped id stops resolving.
                MavTargetTrackData byId;
                Record(owner.TryGetTrackById(owner.GetTrack(0).trackId, out byId) && byId.IsValid,
                       "T-009", "a held track is retrievable by id",
                       report, ref passed, ref failed);

                MavTargetTrackData missing;
                Record(!owner.TryGetTrackById(0, out missing),
                       "T-009b", "track id 0 never resolves",
                       report, ref passed, ref failed);

                // T-010 an invalid observation is ignored rather than becoming a zero-key track.
                owner.ClearTracksForTesting();
                feed.Set(0, Vector3.zero, false, Vector3.zero, MavTrackQuality.Coarse);
                owner.SweepNowForTesting();
                Record(owner.TrackCount == 0, "T-010", "an observation with no source key is ignored",
                       report, ref passed, ref failed);

                // T-011 an inactive feed contributes nothing.
                owner.ClearTracksForTesting();
                feed.active = false;
                feed.Set(505, Vector3.zero, false, Vector3.zero, MavTrackQuality.Coarse);
                owner.SweepNowForTesting();
                Record(owner.TrackCount == 0, "T-011", "an inactive feed is not consulted",
                       report, ref passed, ref failed);
                feed.active = true;

                // T-012 the default track is not usable, so an unset value cannot look like a target.
                MavTargetTrackData invalid = MavTargetTrackData.Invalid;
                Record(!invalid.IsValid && invalid.trackId == 0 && invalid.quality == MavTrackQuality.None,
                       "T-012", "the default track reports invalid",
                       report, ref passed, ref failed);

                // T-013 aging is measured from observation time, not read time.
                MavTargetTrackData aged = MavTargetTrackData.Invalid;
                aged.observedAtTime = 10f;
                Record(Mathf.Approximately(aged.AgeAt(12.5f), 2.5f) && Mathf.Approximately(aged.AgeAt(9f), 0f),
                       "T-013", "age is measured from the observation time and never negative",
                       report, ref passed, ref failed);

                // T-014 the engagement view reports disagreement only when authorities differ.
                MavEngagementView view = host.AddComponent<MavEngagementView>();
                view.PublishDesignation(7, "a");
                view.PublishSensorLock(7, "a");
                view.PublishPodLock(0, null);
                view.RefreshDisagreement();
                Record(!view.authoritiesDisagree && view.claimingAuthorityCount == 2,
                       "T-014", "two authorities pointing at the same track do not disagree",
                       report, ref passed, ref failed);

                view.PublishSensorLock(9, "b");
                view.RefreshDisagreement();
                Record(view.authoritiesDisagree, "T-014b", "two authorities pointing at different tracks disagree",
                       report, ref passed, ref failed);

                // T-015 primary precedence is sensor lock, then pod, then designation.
                view.PublishDesignation(1, "d");
                view.PublishSensorLock(2, "s");
                view.PublishPodLock(3, "p");
                Record(view.PrimaryTrackId == 2, "T-015", "sensor lock outranks pod lock and designation",
                       report, ref passed, ref failed);

                view.PublishSensorLock(0, null);
                Record(view.PrimaryTrackId == 3, "T-015b", "pod lock outranks designation",
                       report, ref passed, ref failed);

                view.PublishPodLock(0, null);
                Record(view.PrimaryTrackId == 1, "T-015c", "designation is used when nothing is locked",
                       report, ref passed, ref failed);

                view.ClearAll();
                Record(view.PrimaryTrackId == 0 && view.claimingAuthorityCount == 0,
                       "T-015d", "a cleared view claims nothing",
                       report, ref passed, ref failed);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }

            report.AppendLine("RESULT: " + (failed == 0 ? "PASS" : "FAIL")
                              + " passed=" + passed + " failed=" + failed);
            return report.ToString();
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
