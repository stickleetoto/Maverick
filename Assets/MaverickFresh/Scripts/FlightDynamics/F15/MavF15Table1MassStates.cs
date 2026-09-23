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
        /// Checked by leading principal minors (Sylvester's criterion), which for this sparsity
        /// reduce to Ixx &gt; 0, Ixx*Iyy &gt; 0, and det = Iyy*(Ixx*Izz - Ixz^2) &gt; 0 - that is,
        /// Ixx &gt; 0, Iyy &gt; 0 and Ixx*Izz &gt; Ixz^2. Re-audited alongside the triangle-test
        /// correction and left unchanged: unlike that test, this one was always evaluated on the
        /// complete tensor.
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
        /// The three principal moments of the COMPLETE body-axis tensor, ascending, in
        /// slug-ft^2. Double precision, because the answer is a small difference of large numbers.
        ///
        /// Body Y is decoupled by the tensor's sparsity, so Iyy is itself principal. The X-Z block
        /// [ Ixx, -Ixz ; -Ixz, Izz ] is diagonalized in closed form. The sign of Ixz cannot change
        /// the eigenvalues, only the direction of the principal axes.
        /// </summary>
        public void GetPrincipalMomentsSlugFt2(
            out double smallest, out double middle, out double largest)
        {
            double a = ixxSlugFt2;
            double d = izzSlugFt2;
            double b = ixzSlugFt2;

            double halfTrace = 0.5 * (a + d);
            double halfDifference = 0.5 * (a - d);
            double root = System.Math.Sqrt(halfDifference * halfDifference + b * b);

            double p = halfTrace - root;
            double q = iyySlugFt2;
            double r = halfTrace + root;

            // Sort three values ascending.
            if (p > q) { double t = p; p = q; q = t; }
            if (q > r) { double t = q; q = r; r = t; }
            if (p > q) { double t = p; p = q; q = t; }

            smallest = p;
            middle = q;
            largest = r;
        }

        /// <summary>
        /// The tightest rigid-body triangle margin, I1 + I2 - I3, on the PRINCIPAL moments sorted
        /// ascending. With the moments sorted and positive this is the only one of the three
        /// inequalities that can fail, so it is the whole test.
        /// </summary>
        public double TightestPrincipalTriangleMarginSlugFt2
        {
            get
            {
                double smallest, middle, largest;
                GetPrincipalMomentsSlugFt2(out smallest, out middle, out largest);
                return smallest + middle - largest;
            }
        }

        /// <summary>
        /// Whether the principal moments satisfy the triangle inequalities every physical rigid
        /// body obeys: each principal moment is at most the sum of the other two. Equivalent to
        /// the second-moment matrix (tr(I)/2)*E - I being positive semidefinite, which is the
        /// complete condition for a real mass distribution to exist.
        ///
        /// CORRECTION. Earlier revisions (named SatisfiesTriangleInequalities) applied the three
        /// inequalities directly to the body-axis diagonal Ixx, Iyy, Izz and called that a
        /// principal-moment test. It is one only when Ixz is zero. On the baseline column, with
        /// Ixz = -5,070 slug-ft^2, body X and Z are not principal axes. The body-axis check is a
        /// NECESSARY condition in any frame (Ixx + Iyy - Izz = 2 * integral of z^2 dm) but not a
        /// SUFFICIENT one: a tensor can pass it and still describe no physical body. The suite
        /// pins such a tensor. The baseline also passes the correct test - only with a smaller
        /// margin than the body-axis check reported, 5,551.6 slug-ft^2 rather than 5,818.
        /// </summary>
        public bool SatisfiesPrincipalTriangleInequalities
        {
            get
            {
                if (!IsPositiveDefinite)
                    return false;

                return TightestPrincipalTriangleMarginSlugFt2 >= 0.0;
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
    /// Source: NASA/TM-2012-215978, Moua, McWherter, Cox and Gera, "Flight Test Results on the
    /// Stability and Control of the F-15 Quiet Spike Aircraft", NTRS 20120013435, distribution
    /// PUBLIC. Retrieved and read; table 1 verified against the rendered page image column by
    /// column. (An earlier revision of this comment gave the report a title it does not have;
    /// the title above is the one printed on its cover.)
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
