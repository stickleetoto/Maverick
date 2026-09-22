using System;
using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// How roll-damper authority varies with angle of attack between full authority and zero.
    /// </summary>
    public enum MavF15RollDamperWashoutShape
    {
        /// <summary>
        /// No sourced shape. The schedule cannot produce an intermediate value and refuses.
        /// This is the current state and it is the whole reason this enum exists: knowing the
        /// endpoint a schedule reaches is not the same as knowing the curve it takes to get there.
        /// </summary>
        Unavailable = 0,

        /// <summary>Linear fade from full authority to zero. Select only if a source says linear.</summary>
        Linear = 1
    }

    /// <summary>
    /// Angle-of-attack washout schedule for the F-15 roll damper.
    ///
    /// WHY THIS IS ITS OWN TYPE
    /// ------------------------
    /// The washout is the one FCS element for which a specific numeric claim is in circulation -
    /// that roll-damper authority is scheduled out with increasing alpha and reaches zero at about
    /// 20.2 degrees in the AFIT/Davison model. A bare pair of floats on the schedule struct would
    /// let that number sit next to unsourced ones with nothing recording where it came from or how
    /// much of the curve it actually pins down. It pins down one endpoint. It does not pin down
    /// the shape, and it does not pin down where the fade begins.
    ///
    /// So this type requires THREE things before it will produce a number: a full-authority alpha,
    /// a zero-authority alpha, and a declared shape. Supplying only the zero point leaves the
    /// stage unavailable, which is the honest outcome.
    ///
    /// WHAT IS AND IS NOT IN THE REPOSITORY
    /// ------------------------------------
    /// The Davison Appendix C material that was supplied and audited (printed pages 111-140) is
    /// an OPEN-LOOP wing-rock simulation. Its driver takes control-surface deflections as fixed
    /// parameters - `PAR(1)=DELESD, PAR(2)=DRUDD, PAR(3)=DDA` on printed page 115 - and its
    /// subroutines are the driver, the equations of motion (FUNX), a Hamming integrator, and the
    /// aerodynamic coefficient routine (COEFF). There is no control system anywhere in it, and no
    /// roll-damper schedule.
    ///
    /// The 20.2 degree figure therefore cannot be verified against anything in this repository.
    /// It is carried as <see cref="ReportedAfitZeroAuthorityAlphaDeg"/> with that stated plainly,
    /// and it is NOT wired in by default.
    /// </summary>
    [Serializable]
    public struct MavF15RollDamperSchedule
    {
        /// <summary>
        /// Alpha at which roll-damper authority is reported to reach zero in the AFIT/Davison
        /// model: 20.2 degrees.
        ///
        /// PROVENANCE WARNING. This value was supplied as a task statement, not read off a page.
        /// The Davison material in this repository is Appendix C, which contains no control
        /// system, so nothing here corroborates it. Treat it as
        /// <see cref="MavEngineDataProvenance.CrossValidationOnly"/> at best, pending a citation
        /// to the thesis body or another primary document. It is a CONSTANT here, not a default:
        /// nothing reads it unless someone assigns it deliberately.
        /// </summary>
        public const float ReportedAfitZeroAuthorityAlphaDeg = 20.2f;

        [Tooltip("Alpha below which the roll damper has full authority, degrees.")]
        public float fullAuthorityAlphaDeg;

        [Tooltip("Alpha at or above which roll-damper authority is zero, degrees.")]
        public float zeroAuthorityAlphaDeg;

        [Tooltip("How authority varies between the two. Unavailable means the curve is unknown and the schedule refuses, even if both endpoints are set.")]
        public MavF15RollDamperWashoutShape shape;

        [Tooltip("Where these numbers came from. Unavailable disables the schedule regardless of the values.")]
        public MavEngineDataProvenance provenance;

        [TextArea(1, 3)]
        [Tooltip("Document, page and configuration. A provenance grade with no citation is not provenance.")]
        public string sourceNote;

        /// <summary>
        /// True only when the endpoints, the shape and the provenance are ALL declared and the
        /// interval is ordered. Anything less and the washout does not run.
        /// </summary>
        public bool Available
        {
            get
            {
                return provenance != MavEngineDataProvenance.Unavailable
                    && shape != MavF15RollDamperWashoutShape.Unavailable
                    && zeroAuthorityAlphaDeg > fullAuthorityAlphaDeg;
            }
        }

        /// <summary>
        /// Rejects half-filled declarations, so a schedule carrying the 20.2 endpoint and nothing
        /// else cannot be mistaken for a working one.
        /// </summary>
        public bool IsSelfConsistent(out string reason)
        {
            bool endpointsSet =
                !Mathf.Approximately(fullAuthorityAlphaDeg, 0f)
                || !Mathf.Approximately(zeroAuthorityAlphaDeg, 0f);

            if (provenance != MavEngineDataProvenance.Unavailable
                && shape == MavF15RollDamperWashoutShape.Unavailable)
            {
                reason = "a source is declared but the washout SHAPE is still Unavailable; "
                         + "an endpoint does not determine the curve";
                return false;
            }

            if (shape != MavF15RollDamperWashoutShape.Unavailable
                && provenance == MavEngineDataProvenance.Unavailable)
            {
                reason = "a washout shape is declared but its source is Unavailable";
                return false;
            }

            if (endpointsSet && zeroAuthorityAlphaDeg <= fullAuthorityAlphaDeg)
            {
                reason = "the washout interval is empty or inverted ("
                         + fullAuthorityAlphaDeg.ToString("F2") + " -> "
                         + zeroAuthorityAlphaDeg.ToString("F2") + " deg)";
                return false;
            }

            reason = "OK";
            return true;
        }

        /// <summary>
        /// Roll-damper authority at this alpha, 1 = full, 0 = fully washed out.
        ///
        /// Returns 1 when the schedule is unavailable. That is deliberate: an aircraft without a
        /// washout feature has an undiminished damper, which is a real behaviour. Returning some
        /// intermediate fade instead would be inventing the very curve this type refuses to guess.
        /// Never returns a negative value - a washed-out damper contributes nothing, it does not
        /// start driving the roll.
        /// </summary>
        public float AuthorityAt(float alphaDeg, out string reason)
        {
            string consistency;
            if (!IsSelfConsistent(out consistency))
            {
                reason = "schedule rejected: " + consistency + "; damper left at full authority";
                return 1f;
            }

            if (!Available)
            {
                reason = "no sourced washout schedule; damper left at full authority";
                return 1f;
            }

            if (float.IsNaN(alphaDeg))
            {
                reason = "alpha is not finite; damper left at full authority";
                return 1f;
            }

            float span = zeroAuthorityAlphaDeg - fullAuthorityAlphaDeg;
            float authority = 1f - Mathf.Clamp01((alphaDeg - fullAuthorityAlphaDeg) / span);

            reason = "washout " + shape + " over "
                     + fullAuthorityAlphaDeg.ToString("F2") + ".."
                     + zeroAuthorityAlphaDeg.ToString("F2") + " deg; authority "
                     + authority.ToString("F3");
            return authority;
        }

        /// <summary>
        /// The fail-closed default: nothing declared, damper undiminished.
        /// </summary>
        public static MavF15RollDamperSchedule Unavailable()
        {
            return new MavF15RollDamperSchedule
            {
                fullAuthorityAlphaDeg = 0f,
                zeroAuthorityAlphaDeg = 0f,
                shape = MavF15RollDamperWashoutShape.Unavailable,
                provenance = MavEngineDataProvenance.Unavailable,
                sourceNote =
                    "UNAVAILABLE: no F-15 roll-damper washout schedule has been recovered. The "
                    + "Davison Appendix C material in this repository is an open-loop wing-rock "
                    + "simulation with no control system. A zero-authority alpha of "
                    + "20.2 deg is REPORTED for the AFIT model but is uncorroborated here, and "
                    + "one endpoint does not determine the curve."
            };
        }

        /// <summary>
        /// The reported AFIT endpoint plus a caller-supplied full-authority alpha and shape.
        ///
        /// Exists so that the moment someone can cite where the fade BEGINS, wiring the schedule
        /// is one call rather than a re-derivation - and so the 20.2 figure lives in exactly one
        /// place. It refuses to be used as exact-target authority: the provenance is pinned to
        /// CrossValidationOnly and cannot be raised through this path.
        /// </summary>
        public static MavF15RollDamperSchedule AfitResearch(
            float fullAuthorityAlphaDeg,
            MavF15RollDamperWashoutShape shape,
            string citation)
        {
            return new MavF15RollDamperSchedule
            {
                fullAuthorityAlphaDeg = fullAuthorityAlphaDeg,
                zeroAuthorityAlphaDeg = ReportedAfitZeroAuthorityAlphaDeg,
                shape = shape,
                provenance = MavEngineDataProvenance.CrossValidationOnly,
                sourceNote =
                    "AFIT/Davison RESEARCH. Zero-authority alpha 20.2 deg is REPORTED, not read "
                    + "from a page image in this repository. Full-authority alpha and shape "
                    + "supplied by caller: " + citation
            };
        }
    }
}
