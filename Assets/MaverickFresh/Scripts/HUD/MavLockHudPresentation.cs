using MaverickFresh.Combat;
using MaverickFresh.Combat.Targeting;
using UnityEngine;

namespace MaverickFresh
{
    /// <summary>
    /// How the HUD presents the authoritative lock. Pure formatting, no state, no side effects.
    ///
    /// WHY A SEPARATE CLASS. The HUD builds its panels inside OnGUI, which no test can read. Pulling the
    /// presentation rules out into functions that take the authority and return strings is what lets the
    /// lifecycle be asserted exactly - Selected really not displayed as Locked, Coasting distinguishable
    /// from Locked, nothing shown at all when the authority is unavailable. A rule that can only be
    /// checked by looking at the screen is not a rule anyone maintains.
    ///
    /// READ-ONLY BY CONSTRUCTION. Everything here is static and takes its inputs as parameters. There is
    /// no field to cache a lock in, so a stale lock cannot survive the authority going away, and there
    /// is nothing for a presentation path to write back into.
    ///
    /// It reads only <see cref="IMavTrackLockAuthority"/> and <see cref="MavEngagementView"/>. It does
    /// not know the concrete lock controller, any sensor, or any legacy targeting component.
    /// </summary>
    public static class MavLockHudPresentation
    {
        /// <summary>Shown when the authority is available but committed to nothing.</summary>
        public const string NoLockText = "LOCK ---";

        /// <summary>Shown in the debug panel when the authority cannot be consulted at all.</summary>
        public const string OfflineText = "AUTH offline";

        /// <summary>
        /// Whether the authority holds a commitment the HUD should treat as a live lock.
        ///
        /// Coasting counts, because the commitment has not been abandoned - a HUD that dropped the lock
        /// symbol on one missed observation would misreport what the aircraft is doing. The STATE is
        /// still reported separately so the two never blur together.
        /// </summary>
        public static bool ShowsLockCommitment(IMavTrackLockAuthority authority)
        {
            if (authority == null || !authority.IsLockAuthorityActive)
                return false;
            return authority.LockState == MavLockState.Locked
                   || authority.LockState == MavLockState.Coasting;
        }

        /// <summary>Short label per lifecycle state. Locked and Coasting are deliberately different.</summary>
        public static string StateLabel(MavLockState state)
        {
            switch (state)
            {
                case MavLockState.Selected: return "SEL";
                case MavLockState.Acquiring: return "ACQ";
                case MavLockState.Locked: return "LOCKED";
                case MavLockState.Coasting: return "COAST";
            }
            return "IDLE";
        }

        /// <summary>
        /// The compact player-facing line.
        ///
        /// Returns the EMPTY STRING when the authority is unavailable, so the line disappears rather than
        /// freezing on its last value. That is the fail-closed rule in presentation form: no authority
        /// means no lock symbology, not the previous lock symbology.
        ///
        /// Selection is never drawn as a lock. SEL and ACQ are visibly not LOCKED, because showing a lock
        /// the aircraft does not have is the specific failure this phase exists to avoid.
        /// </summary>
        public static string FormatPlayerLine(IMavTrackLockAuthority authority, MavEngagementView view)
        {
            if (authority == null || !authority.IsLockAuthorityActive)
                return string.Empty;

            MavLockState state = authority.LockState;
            if (state == MavLockState.Idle)
                return NoLockText;

            if (state == MavLockState.Acquiring)
            {
                int percent = Mathf.RoundToInt(Mathf.Clamp01(authority.AcquisitionProgress01) * 100f);
                return "LOCK ACQ " + percent + "%";
            }

            if (state == MavLockState.Selected)
                return "LOCK SEL";

            // Locked or Coasting: a commitment, with the track it is committed to.
            return "LOCK " + StateLabel(state) + " " + LockedName(view);
        }

        /// <summary>
        /// The developer line: the full lifecycle, plus what each legacy authority still claims.
        ///
        /// The legacy half is kept on purpose. Pod and CAS designation carry information the track
        /// vocabulary deliberately cannot express - a lock or designation on a bare ground point has no
        /// track to be about - so replacing them with the authoritative lock would delete real state to
        /// make the architecture look tidier.
        /// </summary>
        public static string FormatDebugLine(IMavTrackLockAuthority authority, MavEngagementView view)
        {
            string authPart;

            if (authority == null || !authority.IsLockAuthorityActive)
            {
                authPart = OfflineText;
            }
            else
            {
                MavLockState state = authority.LockState;
                authPart = "AUTH " + StateLabel(state);

                if (state == MavLockState.Acquiring)
                {
                    int percent = Mathf.RoundToInt(Mathf.Clamp01(authority.AcquisitionProgress01) * 100f);
                    authPart += " " + percent + "%";
                }

                MavTargetTrackData locked;
                if (authority.TryGetLockedTrack(out locked))
                {
                    authPart += " trk " + locked.trackId + " " + LockedName(view)
                                + " hold " + authority.TimeInLockSeconds.ToString("0.0") + "s";
                }

                if (authority.LastLossReason != MavLockLossReason.None)
                    authPart += " lastloss " + authority.LastLossReason;
            }

            if (view == null)
                return authPart;

            string legacyPart = " | LEG des " + StateLabel(view.legacyDesignationLockState)
                                + " stt " + StateLabel(view.legacySensorLockState)
                                + " pod " + StateLabel(view.legacyPodLockState);

            if (AuthoritativeDisagreesWithLegacy(authority, view))
                legacyPart += "  DISAGREE";

            return authPart + legacyPart;
        }

        /// <summary>
        /// Whether the authoritative lock points at a different track than some legacy authority does.
        ///
        /// Derived here rather than read from MavEngagementView.legacyDisagreesWithAuthoritative. That
        /// field was once computed only while preferAuthoritativeLock was ON - dead for the whole of the
        /// migration - which is why this was written to compare the published ids directly. The view's
        /// field has since been corrected and the two now answer the same question, asserted in the
        /// migration-prep suite.
        ///
        /// It is still derived here, for a reason that outlives that defect: the view's field is a
        /// snapshot, only as fresh as the last RefreshDisagreement call, while this reads the authority's
        /// live committed lock at the moment the panel is drawn. A presenter that showed DISAGREE one
        /// frame late would be reporting the publisher's cadence, not the engagement. Costs nothing, and
        /// keeps the presenter pure.
        /// </summary>
        public static bool AuthoritativeDisagreesWithLegacy(IMavTrackLockAuthority authority, MavEngagementView view)
        {
            if (view == null || authority == null || !authority.IsLockAuthorityActive)
                return false;

            MavTargetTrackData locked;
            if (!authority.TryGetLockedTrack(out locked) || locked.trackId == 0)
                return false;

            int id = locked.trackId;
            if (view.designatedTrackId != 0 && view.designatedTrackId != id)
                return true;
            if (view.sensorLockTrackId != 0 && view.sensorLockTrackId != id)
                return true;
            if (view.podLockTrackId != 0 && view.podLockTrackId != id)
                return true;
            return false;
        }

        /// <summary>
        /// The locked track name, from the claim the authority already published.
        ///
        /// Only ever consulted once the authority has been found available and committed, so a published
        /// name cannot be read while it is stale.
        /// </summary>
        private static string LockedName(MavEngagementView view)
        {
            if (view == null || string.IsNullOrEmpty(view.debugAuthoritativeLockName))
                return "none";
            return view.debugAuthoritativeLockName;
        }
    }
}
