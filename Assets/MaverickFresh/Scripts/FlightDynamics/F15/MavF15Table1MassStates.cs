using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// Which column of NASA/TM-2012-215978 table 1 a mass state came from.
    ///
    /// This enum exists because of a real error. Earlier R1 revisions froze the
    /// <see cref="QuietSpikeExtended"/> column into
    /// <see cref="MavF15MassReference"/> while the surrounding comments and documentation said
    /// "baseline F-15B test airplane". Six anonymous constants carry no column with them, so
    /// nothing in the code or the tests could notice.
    ///
    /// Two of the three columns share a weight (37,152 lb) and all six numbers look equally
    /// plausible, which is exactly why the column has to be part of the data rather than part of a
    /// comment.
    /// </summary>
    public enum MavF15MassStateKind
    {
        /// <summary>
        /// "Baseline F-15B test airplane" - the aircraft WITHOUT the Quiet Spike article fitted.
        /// This is the column the frozen pre-Quiet-Spike target requires.
        /// </summary>
        Baseline = 0,

        /// <summary>"Spike retracted" - Quiet Spike fitted, stowed.</summary>
        QuietSpikeRetracted = 1,

        /// <summary>"Spike extended" - Quiet Spike fitted, deployed.</summary>
        QuietSpikeExtended = 2
    }

    /// <summary>
    /// One column of NASA/TM-2012-215978 table 1, as printed, with the column identity attached.
    ///
    /// Raw source units throughout - pounds, percent MAC, slug-ft^2. SI is derived by
    /// <see cref="MavF15MassReference"/> from these values and is deliberately not stored here,
    /// so there is exactly one place a conversion can be got wrong.
    /// </summary>
    public struct MavF15MassStateReference
    {
        public MavF15MassStateKind kind;

        /// <summary>The column heading as the table prints it.</summary>
        public string columnLabel;

        public float weightLb;
        public float xcgPercentMac;

        public float ixxSlugFt2;
        public float iyySlugFt2;
        public float izzSlugFt2;

        /// <summary>
        /// Product of inertia, source sign preserved exactly. All three columns are negative.
        /// The baseline value is roughly eleven times the two spike values, which is the largest
        /// single consequence of the column correction.
        /// </summary>
        public float ixzSlugFt2;

        public float fuelStateLb;
        public string citation;

        /// <summary>
        /// Whether the body-axis inertia tensor built from these values is positive definite,
        /// using the conventional aircraft tensor
        /// [ Ixx, 0, -Ixz ; 0, Iyy, 0 ; -Ixz, 0, Izz ].
        ///
        /// Checked by leading principal minors, which for this sparsity reduce to Ixx &gt; 0,
        /// Iyy &gt; 0, and Ixx*Izz &gt; Ixz^2.
        /// </summary>
        public bool IsPositiveDefinite
        {
            get
            {
                if (!(ixxSlugFt2 > 0f) || !(iyySlugFt2 > 0f) || !(izzSlugFt2 > 0f))
                    return false;

                return ixxSlugFt2 * izzSlugFt2 > ixzSlugFt2 * ixzSlugFt2;
            }
        }

        /// <summary>
        /// Whether the three principal moments satisfy the triangle inequalities every physical
        /// rigid body obeys. Not required by the tensor algebra, but a genuine check on the
        /// transcription: a mistyped digit usually breaks one of these.
        /// </summary>
        public bool SatisfiesTriangleInequalities
        {
            get
            {
                return ixxSlugFt2 + iyySlugFt2 >= izzSlugFt2
                    && ixxSlugFt2 + izzSlugFt2 >= iyySlugFt2
                    && iyySlugFt2 + izzSlugFt2 >= ixxSlugFt2;
            }
        }

        public bool Matches(
            float weight, float xcg, float ixx, float iyy, float izz, float ixz)
        {
            return Mathf.Approximately(weightLb, weight)
                && Mathf.Approximately(xcgPercentMac, xcg)
                && Mathf.Approximately(ixxSlugFt2, ixx)
                && Mathf.Approximately(iyySlugFt2, iyy)
                && Mathf.Approximately(izzSlugFt2, izz)
                && Mathf.Approximately(ixzSlugFt2, ixz);
        }
    }

    /// <summary>
    /// NASA/TM-2012-215978 table 1 in full - all three columns, each labelled.
    ///
    /// Source: NASA/TM-2012-215978, "Flight-Test Evaluation of the Longitudinal Stability and
    /// Control Characteristics of the F-15B Quiet Spike Aircraft" lineage, NTRS 20120013435,
    /// distribution PUBLIC. Retrieved and read; table 1 verified against the rendered page image
    /// column by column.
    ///
    /// Table caption: "Mass and inertia characteristics of the baseline NASA Dryden Flight
    /// Research Center F-15B test airplane." The body text on the same page states: "The mass and
    /// inertia characteristics are shown in table 1 for a mid-fuel loading of 8,000 lb", which
    /// applies to all three columns.
    ///
    /// NOTE on the report's own symbol list: it defines IXZ as "airplane product of inertia about
    /// the XY axis". That is a typographical slip in the source - the symbol is IXZ, the table row
    /// is IXZ, and the quantity is the X-Z product of inertia. Recorded so the next reader does not
    /// have to wonder.
    ///
    /// All three states are held even though only one is the target, because a column that is
    /// present and named cannot be silently confused with its neighbours. The two spike states are
    /// NOT part of the frozen target and nothing may select them for it.
    /// </summary>
    public static class MavF15Table1MassStates
    {
        public const string Citation =
            "NASA/TM-2012-215978 table 1 (NTRS 20120013435, public), mid-fuel loading 8,000 lb. "
            + "Verified against the rendered page image.";

        public const float TableFuelStateLb = 8000f;

        /// <summary>
        /// "Baseline F-15B test airplane" - no Quiet Spike article fitted. The column the frozen
        /// pre-Quiet-Spike target requires.
        /// </summary>
        public static MavF15MassStateReference Baseline
        {
            get
            {
                return State(
                    MavF15MassStateKind.Baseline, "Baseline F-15B test airplane",
                    37426f, 26.34f, 30345f, 198687f, 223214f, -5070f);
            }
        }

        /// <summary>"Spike retracted" - Quiet Spike fitted and stowed. NOT the target.</summary>
        public static MavF15MassStateReference QuietSpikeRetracted
        {
            get
            {
                return State(
                    MavF15MassStateKind.QuietSpikeRetracted, "Spike retracted",
                    37152f, 26.13f, 27947f, 189456f, 212746f, -466f);
            }
        }

        /// <summary>
        /// "Spike extended" - Quiet Spike fitted and deployed. NOT the target.
        ///
        /// This is the column earlier R1 revisions froze by mistake. It is kept here, correctly
        /// labelled, so the mistake is visible rather than erased.
        /// </summary>
        public static MavF15MassStateReference QuietSpikeExtended
        {
            get
            {
                return State(
                    MavF15MassStateKind.QuietSpikeExtended, "Spike extended",
                    37152f, 26.05f, 27953f, 190777f, 213957f, -460f);
            }
        }

        /// <summary>All three columns, in table order. Fresh array each call.</summary>
        public static MavF15MassStateReference[] All
        {
            get
            {
                return new MavF15MassStateReference[]
                {
                    Baseline, QuietSpikeRetracted, QuietSpikeExtended
                };
            }
        }

        /// <summary>
        /// Selects a column by kind. There is no default case that quietly returns something:
        /// an unrecognised kind returns false, because guessing a mass state is the failure this
        /// whole file exists to prevent.
        /// </summary>
        public static bool TryGet(
            MavF15MassStateKind kind, out MavF15MassStateReference state)
        {
            MavF15MassStateReference[] all = All;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].kind == kind)
                {
                    state = all[i];
                    return true;
                }
            }

            state = new MavF15MassStateReference();
            return false;
        }

        private static MavF15MassStateReference State(
            MavF15MassStateKind kind, string label,
            float weightLb, float xcg, float ixx, float iyy, float izz, float ixz)
        {
            MavF15MassStateReference s = new MavF15MassStateReference();
            s.kind = kind;
            s.columnLabel = label;
            s.weightLb = weightLb;
            s.xcgPercentMac = xcg;
            s.ixxSlugFt2 = ixx;
            s.iyySlugFt2 = iyy;
            s.izzSlugFt2 = izz;
            s.ixzSlugFt2 = ixz;
            s.fuelStateLb = TableFuelStateLb;
            s.citation = Citation;
            return s;
        }
    }
}
