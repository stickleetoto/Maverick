using MaverickFresh.Combat.Targeting;
using UnityEngine;

namespace MaverickFresh.Combat.Legacy
{
    /// <summary>
    /// Watches the three legacy lock authorities and publishes what each currently points at, as
    /// track ids, into <see cref="MavEngagementView"/>.
    ///
    /// Strictly an observer. It never designates, never locks, never clears, and never writes to the
    /// legacy components - it only reads their public state. If this component is removed, the three
    /// authorities behave exactly as they did before it existed, which is the property that makes it
    /// safe to run alongside live gameplay.
    ///
    /// It publishes two different things. The TRACK IDS say what each authority points at; the
    /// PROJECTED LOCK STATES, via <see cref="MavLegacyLockProjection"/>, say where each one is in its
    /// own lifecycle. The second is what makes the migration's equivalence claim checkable rather than
    /// merely stated.
    ///
    /// It lives in Legacy/ because it is the only thing besides the observation feed that knows the
    /// concrete legacy types. Targeting/ stays clean, so when those authorities are retired this file
    /// is deleted and nothing in Targeting/ changes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MavLegacyEngagementProbe : MonoBehaviour
    {
        [Header("Wiring")]
        public MavTargetTrackOwner owner;
        public MavEngagementView view;

        /// <summary>
        /// The feed whose local keys this probe speaks. Resolution is per FEED, not per source
        /// category: the legacy objects this probe holds are keyed by the legacy feed, and only that
        /// feed's keys can be resolved to its tracks.
        /// </summary>
        public MavLegacyTargetObservationFeed feed;

        [Header("Legacy authorities (read-only)")]
        public MavCASTargetingSystem casTargeting;
        public MavF22SensorSuite sensorSuite;
        public MavTargetingPodSystem targetingPod;

        [Header("Sampling")]
        [Tooltip("Seconds between projections. This is diagnostics, not simulation.")]
        public float sampleInterval = 0.1f;

        [Header("Read-only state")]
        public int debugResolvedCount;
        public int debugUnresolvedCount;

        private float nextSampleTime;

        private void OnEnable()
        {
            Resolve();
            nextSampleTime = 0f;
        }

        private void Resolve()
        {
            if (owner == null)
                owner = GetComponent<MavTargetTrackOwner>();
            if (view == null)
                view = GetComponent<MavEngagementView>();
            if (feed == null)
                feed = GetComponent<MavLegacyTargetObservationFeed>();
            if (casTargeting == null)
                casTargeting = GetComponent<MavCASTargetingSystem>();
            if (sensorSuite == null)
                sensorSuite = GetComponent<MavF22SensorSuite>();
            if (targetingPod == null)
                targetingPod = GetComponent<MavTargetingPodSystem>();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextSampleTime)
                return;
            nextSampleTime = Time.unscaledTime + Mathf.Max(0.02f, sampleInterval);

            Resolve();
            if (view == null)
                return;

            if (owner == null || feed == null)
            {
                view.ClearAll();
                return;
            }

            int resolved = 0;
            int unresolved = 0;

            // CAS designation.
            MavCASTarget designated = casTargeting != null ? casTargeting.designatedTarget : null;
            PublishOne(designated != null ? designated.GetInstanceID() : 0,
                       designated != null ? designated.displayName : null,
                       ref resolved, ref unresolved,
                       PublishKind.Designation);

            // Sensor suite selection. Only counted as a lock when the suite reports one: a selected
            // contact that has not completed STT is not a lock, and flattening the two would make the
            // view claim more certainty than the legacy system has.
            MavRadarSignature sensorTarget = sensorSuite != null && sensorSuite.debugHasLock
                ? sensorSuite.selectedTarget
                : null;
            PublishOne(sensorTarget != null ? sensorTarget.GetInstanceID() : 0,
                       sensorTarget != null ? sensorTarget.displayName : null,
                       ref resolved, ref unresolved,
                       PublishKind.SensorLock);

            // Pod lock. Only a lock onto a TARGET projects to a track; the pod can also lock a bare
            // ground point, which is not something the track owner knows about and must not be
            // invented as one.
            MavCASTarget podTarget = targetingPod != null && targetingPod.isLocked && targetingPod.lockedToTarget
                ? targetingPod.lockedTarget
                : null;
            PublishOne(podTarget != null ? podTarget.GetInstanceID() : 0,
                       podTarget != null ? podTarget.displayName : null,
                       ref resolved, ref unresolved,
                       PublishKind.PodLock);

            // Each authority's own lifecycle state, in the new vocabulary. Read-only, like everything
            // else here: MavLegacyLockProjection is a pure mapping and none of these reads can change
            // what the legacy components do.
            view.PublishLegacyLockStates(
                MavLegacyLockProjection.ProjectCasDesignation(
                    designated != null, designated != null && designated.IsAlive()),
                MavLegacyLockProjection.ProjectSensorSuite(
                    sensorSuite != null && sensorSuite.selectedTarget != null,
                    sensorSuite != null ? sensorSuite.debugLockProgress01 : 0f,
                    sensorSuite != null && sensorSuite.debugHasLock),
                MavLegacyLockProjection.ProjectTargetingPod(
                    targetingPod != null && targetingPod.isLocked,
                    targetingPod != null && targetingPod.lockedToTarget,
                    targetingPod != null && targetingPod.lockedTarget != null));

            view.RefreshDisagreement();
            debugResolvedCount = resolved;
            debugUnresolvedCount = unresolved;
        }

        private enum PublishKind
        {
            Designation,
            SensorLock,
            PodLock,
        }

        /// <summary>
        /// Maps one legacy object to a track id and publishes it.
        ///
        /// An object the owner has no track for publishes id 0 while still carrying its display name.
        /// That is deliberate: it says "this authority claims something the track owner has not seen",
        /// which is exactly the kind of disagreement worth making visible rather than papering over
        /// by minting a track here. Minting one would also give this observer the power to create
        /// tracks, which belongs to the owner alone.
        /// </summary>
        private void PublishOne(int instanceId, string displayName, ref int resolved, ref int unresolved, PublishKind kind)
        {
            int trackId = 0;
            if (instanceId != 0)
            {
                if (owner.TryResolveTrackId(feed, instanceId, out trackId))
                    resolved++;
                else
                    unresolved++;
            }

            switch (kind)
            {
                case PublishKind.Designation:
                    view.PublishDesignation(trackId, displayName);
                    break;
                case PublishKind.SensorLock:
                    view.PublishSensorLock(trackId, displayName);
                    break;
                case PublishKind.PodLock:
                    view.PublishPodLock(trackId, displayName);
                    break;
            }
        }
    }
}
