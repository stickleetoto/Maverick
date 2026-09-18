using UnityEngine;

namespace MaverickFresh.Combat.Targeting
{
    /// <summary>
    /// Which legacy authority is pointing at which track, in one place.
    ///
    /// This is the first step of consolidating the three competing lock authorities (issue #16). It
    /// does NOT take the lock away from them: CAS designation, the sensor suite's STT lock and the
    /// pod's lock all still run exactly as they did, still own their own state, and still respond to
    /// their own keys. What changes is that their answers are now expressed in one vocabulary -
    /// track ids from <see cref="MavTargetTrackOwner"/> - and visible side by side, so the
    /// disagreement between them is observable instead of buried in three components.
    ///
    /// Consolidation in the sense of "one component decides" is deliberately NOT done here. Taking
    /// the lock away from three live systems at the same time as introducing tracks would change
    /// gameplay while changing the architecture, and then neither could be trusted. The order is:
    /// express, observe, then move authority.
    ///
    /// Read-only for consumers. A publisher fills it; nothing here decides anything.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MavEngagementView : MonoBehaviour
    {
        [Header("Projected legacy authorities (read-only)")]
        [Tooltip("Track the CAS designation currently points at, or 0.")]
        public int designatedTrackId;

        [Tooltip("Track the sensor suite currently holds, or 0.")]
        public int sensorLockTrackId;

        [Tooltip("Track the targeting pod currently holds, or 0.")]
        public int podLockTrackId;

        [Header("Disagreement")]
        [Tooltip("True when two or more authorities point at different non-zero tracks.")]
        public bool authoritiesDisagree;

        [Tooltip("How many authorities currently claim something.")]
        public int claimingAuthorityCount;

        [Header("Diagnostics")]
        public string debugDesignatedName = "none";
        public string debugSensorLockName = "none";
        public string debugPodLockName = "none";

        /// <summary>
        /// The single answer, once one is needed.
        ///
        /// Precedence is sensor lock, then pod lock, then designation, and it is a PLACEHOLDER: no
        /// consumer reads it today, and it exists so that the question "which one wins" has one
        /// documented home rather than being re-decided at each call site. When Radar Core gives
        /// locks real meaning, this is the method that changes, and only this one.
        ///
        /// The ordering reflects claim strength as the legacy systems actually use it: an STT lock is
        /// a maintained commitment to one object, a pod lock is a maintained commitment to a ground
        /// point, and a CAS designation is a marker that survives losing sight of the target.
        /// </summary>
        public int PrimaryTrackId
        {
            get
            {
                if (sensorLockTrackId != 0)
                    return sensorLockTrackId;
                if (podLockTrackId != 0)
                    return podLockTrackId;
                return designatedTrackId;
            }
        }

        /// <summary>Publishes the CAS designation projection. Called by the legacy probe.</summary>
        public void PublishDesignation(int trackId, string displayName)
        {
            designatedTrackId = trackId;
            debugDesignatedName = string.IsNullOrEmpty(displayName) ? "none" : displayName;
        }

        public void PublishSensorLock(int trackId, string displayName)
        {
            sensorLockTrackId = trackId;
            debugSensorLockName = string.IsNullOrEmpty(displayName) ? "none" : displayName;
        }

        public void PublishPodLock(int trackId, string displayName)
        {
            podLockTrackId = trackId;
            debugPodLockName = string.IsNullOrEmpty(displayName) ? "none" : displayName;
        }

        /// <summary>
        /// Recomputes the disagreement summary. Separate from the publish calls so a publisher can
        /// set all three and evaluate once, rather than reporting disagreement against a half-updated
        /// view.
        /// </summary>
        public void RefreshDisagreement()
        {
            int claims = 0;
            if (designatedTrackId != 0) claims++;
            if (sensorLockTrackId != 0) claims++;
            if (podLockTrackId != 0) claims++;
            claimingAuthorityCount = claims;

            bool disagree = false;
            if (designatedTrackId != 0 && sensorLockTrackId != 0 && designatedTrackId != sensorLockTrackId)
                disagree = true;
            if (designatedTrackId != 0 && podLockTrackId != 0 && designatedTrackId != podLockTrackId)
                disagree = true;
            if (sensorLockTrackId != 0 && podLockTrackId != 0 && sensorLockTrackId != podLockTrackId)
                disagree = true;

            authoritiesDisagree = disagree;
        }

        /// <summary>Clears every projection. Used when the probe loses its sources.</summary>
        public void ClearAll()
        {
            designatedTrackId = 0;
            sensorLockTrackId = 0;
            podLockTrackId = 0;
            debugDesignatedName = "none";
            debugSensorLockName = "none";
            debugPodLockName = "none";
            authoritiesDisagree = false;
            claimingAuthorityCount = 0;
        }
    }
}
