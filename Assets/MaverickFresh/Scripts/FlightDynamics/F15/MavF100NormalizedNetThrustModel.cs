using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>Why a normalized-thrust query did or did not produce a number.</summary>
    public enum MavF100ThrustSupport
    {
        /// <summary>Nothing was evaluated. The reason string says what was wrong.</summary>
        NotEvaluated = 0,

        /// <summary>
        /// The flight condition matches no documented operating point, so the source says
        /// nothing about it. Deliberately NOT interpolated - see
        /// <see cref="MavF100NormalizedNetThrustModel"/>.
        /// </summary>
        OutsideSourceSupport = 1,

        /// <summary>
        /// The condition matched a documented point but the power lever angle fell outside the
        /// range tested there, and the excursion policy clamped it to the nearest tested setting.
        /// </summary>
        PowerLeverClampedToTestedRange = 2,

        /// <summary>Fully inside the source data, in both flight condition and power lever angle.</summary>
        Supported = 3
    }

    /// <summary>
    /// Result of a normalized net-thrust lookup. Carries a FRACTION, never a force: converting to
    /// newtons needs <see cref="MavF100SourceData.DesignMaximumNetThrust"/>, which is undeclared.
    /// </summary>
    public struct MavF100NetThrustFractionResult
    {
        public MavF100ThrustSupport support;

        /// <summary>Net thrust as a fraction of design maximum net thrust. Meaningless unless supported.</summary>
        public float netThrustFraction;

        /// <summary>Which figure-17 panel answered, for traceability. Empty when none did.</summary>
        public string panel;

        public MavF100SourceClass sourceClass;

        /// <summary>Always <see cref="MavF100ThrustQuantity.UninstalledNetThrust"/> when supported.</summary>
        public MavF100ThrustQuantity quantity;

        public string reason;

        public bool HasNumber
        {
            get
            {
                return support == MavF100ThrustSupport.Supported
                    || support == MavF100ThrustSupport.PowerLeverClampedToTestedRange;
            }
        }

        public static MavF100NetThrustFractionResult Unsupported(
            MavF100ThrustSupport support, string reason)
        {
            MavF100NetThrustFractionResult r = new MavF100NetThrustFractionResult();
            r.support = support;
            r.netThrustFraction = 0f;
            r.panel = string.Empty;
            r.sourceClass = MavF100SourceClass.Unavailable;
            r.quantity = MavF100ThrustQuantity.Unspecified;
            r.reason = reason;
            return r;
        }
    }

    /// <summary>
    /// LAYER A + LAYER B of the R5 propulsion brief: the source-backed steady thrust
    /// characteristic, and the envelope inside which it is actually supported.
    ///
    /// WHAT THIS MODEL IS
    /// ------------------
    /// The F100-PW-100(3) net thrust characteristic of NASA TP-1034 figure 17, as a fraction of
    /// design maximum net thrust, over power lever angle, at each of seven documented flight
    /// conditions spanning sea level to 17.83 km and Mach 0 to 2.2.
    ///
    /// That is a genuine, complete, source-backed description of how this engine's thrust varies
    /// with throttle, altitude and Mach across the whole F-15 envelope. It is the most the four
    /// R5 documents support, and it is considerably more than nothing.
    ///
    /// WHAT IT IS NOT
    /// --------------
    /// It is not thrust. The figure's normalizer - net thrust at the sea-level static
    /// maximum-augmentation design point - is printed nowhere in the pack, so this model can say
    /// that maximum augmentation at 9.144 km and Mach 0.9 gives 0.519 of the design maximum, and
    /// cannot say what that is in newtons. <see cref="MavF100ThrustDeck"/> is the piece that holds
    /// that gap open rather than papering over it.
    ///
    /// WHY THERE IS NO INTERPOLATION BETWEEN FLIGHT CONDITIONS
    /// ------------------------------------------------------
    /// The seven conditions are not a grid. They are scattered test points, and four of the seven
    /// differ in altitude AND Mach simultaneously. Interpolating between, say, 9.144 km at Mach
    /// 0.9 and 6.096 km at Mach 1.8 would require assuming how thrust splits between the two
    /// variables across the transonic region - which is precisely the physics the data would have
    /// been needed to establish. Any scheme would produce smooth, plausible, entirely invented
    /// numbers, and nothing downstream could tell them from the measured ones.
    ///
    /// So a query off the documented points returns
    /// <see cref="MavF100ThrustSupport.OutsideSourceSupport"/> and no number. Interpolation ALONG
    /// power lever angle is different and is performed: figure 17 draws a continuous curve through
    /// its markers at each condition, so the source itself asserts the intermediate values exist.
    /// </summary>
    public static class MavF100NormalizedNetThrustModel
    {
        /// <summary>
        /// How close a query must be in altitude to count as the documented condition.
        ///
        /// An INTERFACE tolerance, not engine data: it decides whether the caller is asking about
        /// a tested point, never what the answer is. Held tight enough that two documented points
        /// can never both match - the closest pair, 12.19 km and 13.72 km, are 1.53 km apart.
        /// </summary>
        public const float ConditionMatchAltitudeToleranceM = 150f;

        /// <summary>
        /// How close a query must be in Mach to count as the documented condition. As above: the
        /// closest documented pair, Mach 2.15 and 2.20, are 0.05 apart, so this cannot straddle.
        /// </summary>
        public const float ConditionMatchMachTolerance = 0.02f;

        /// <summary>
        /// Evaluates the normalized net thrust characteristic.
        ///
        /// Power lever angle is passed in degrees because that is the abscissa the source actually
        /// uses. It is deliberately NOT a 0..1 throttle: the map from a cockpit throttle to PLA is
        /// an aircraft interface convention, and inventing one here would bury it inside what is
        /// otherwise sourced data. See <see cref="MavF100EngineControlSchedules"/>.
        /// </summary>
        public static MavF100NetThrustFractionResult Evaluate(
            float altitudeM, float mach, float powerLeverAngleDeg)
        {
            if (float.IsNaN(altitudeM) || float.IsNaN(mach) || float.IsNaN(powerLeverAngleDeg)
                || float.IsInfinity(altitudeM) || float.IsInfinity(mach)
                || float.IsInfinity(powerLeverAngleDeg))
            {
                return MavF100NetThrustFractionResult.Unsupported(
                    MavF100ThrustSupport.NotEvaluated,
                    "non-finite query (altitude, Mach or power lever angle)");
            }

            MavF100OperatingPointCurve[] curves = MavF100SourceData.NetThrustCurves;

            int match = -1;
            for (int i = 0; i < curves.Length; i++)
            {
                if (Mathf.Abs(curves[i].altitudeM - altitudeM) <= ConditionMatchAltitudeToleranceM
                    && Mathf.Abs(curves[i].mach - mach) <= ConditionMatchMachTolerance)
                {
                    match = i;
                    break;
                }
            }

            if (match < 0)
            {
                return MavF100NetThrustFractionResult.Unsupported(
                    MavF100ThrustSupport.OutsideSourceSupport,
                    "no documented TP-1034 figure 17 operating point within "
                    + ConditionMatchAltitudeToleranceM + " m and "
                    + ConditionMatchMachTolerance + " Mach of altitude " + altitudeM
                    + " m, Mach " + mach + "; the source says nothing about this condition and "
                    + "no interpolation between scattered test points is performed");
            }

            MavF100OperatingPointCurve curve = curves[match];
            if (curve.Count < 2)
            {
                return MavF100NetThrustFractionResult.Unsupported(
                    MavF100ThrustSupport.NotEvaluated,
                    "operating point " + curve.panel + " has fewer than two breakpoints");
            }

            int last = curve.Count - 1;
            bool clamped = false;
            float pla = powerLeverAngleDeg;

            if (pla < curve.powerLeverAngleDeg[0])
            {
                pla = curve.powerLeverAngleDeg[0];
                clamped = true;
            }
            else if (pla > curve.powerLeverAngleDeg[last])
            {
                pla = curve.powerLeverAngleDeg[last];
                clamped = true;
            }

            MavF100NetThrustFractionResult result = new MavF100NetThrustFractionResult();
            result.netThrustFraction = InterpolateAlongPowerLever(curve, pla);
            result.panel = curve.panel;
            result.quantity = MavF100ThrustQuantity.UninstalledNetThrust;

            // PW-100(3) data standing in for a PW-100 target: compatible support, never exact.
            result.sourceClass = MavF100SourceClass.CompatibleSupport;

            if (clamped)
            {
                result.support = MavF100ThrustSupport.PowerLeverClampedToTestedRange;
                result.reason = "power lever angle " + powerLeverAngleDeg
                    + " deg clamped to the range tested at " + curve.panel + ", "
                    + curve.powerLeverAngleDeg[0] + " to " + curve.powerLeverAngleDeg[last]
                    + " deg";
            }
            else
            {
                result.support = MavF100ThrustSupport.Supported;
                result.reason = "TP-1034 " + curve.panel + ", F100-PW-100(3), "
                    + "fraction of design maximum net thrust";
            }

            return result;
        }

        /// <summary>
        /// Linear interpolation along the power lever axis of one operating point. Never
        /// extrapolates: the caller has already clamped, and the breakpoints are ascending.
        /// </summary>
        private static float InterpolateAlongPowerLever(
            MavF100OperatingPointCurve curve, float powerLeverAngleDeg)
        {
            float[] x = curve.powerLeverAngleDeg;
            float[] y = curve.netThrustFraction;

            for (int i = 1; i < x.Length; i++)
            {
                if (powerLeverAngleDeg <= x[i])
                {
                    float span = x[i] - x[i - 1];
                    if (span <= 0f)
                        return y[i];

                    float t = (powerLeverAngleDeg - x[i - 1]) / span;
                    return y[i - 1] + (y[i] - y[i - 1]) * t;
                }
            }

            return y[y.Length - 1];
        }

        /// <summary>
        /// The documented operating points, for anyone who needs to know where the data IS rather
        /// than ask whether one particular query is inside it.
        /// </summary>
        public static string DescribeSourceEnvelope()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder(512);
            sb.AppendLine("F100-PW-100(3) normalized net thrust, documented operating points:");

            MavF100OperatingPointCurve[] curves = MavF100SourceData.NetThrustCurves;
            for (int i = 0; i < curves.Length; i++)
            {
                MavF100OperatingPointCurve c = curves[i];
                sb.Append("  ").Append(c.panel)
                  .Append("  altitude ").Append(c.altitudeM.ToString("0")).Append(" m")
                  .Append(", Mach ").Append(c.mach.ToString("0.00"))
                  .Append(", PLA ").Append(c.powerLeverAngleDeg[0].ToString("0.0"))
                  .Append(" to ").Append(c.powerLeverAngleDeg[c.Count - 1].ToString("0.0"))
                  .Append(" deg (").Append(c.Count).AppendLine(" points)");
            }

            sb.AppendLine("Between these points the source says nothing, and neither does this model.");
            return sb.ToString();
        }
    }
}
