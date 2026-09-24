using System;
using System.Collections.ObjectModel;
using MaverickFresh.FlightDynamics.F15;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>What kind of evidence one digitized series is.</summary>
    public enum MavF15ValidationDataKind
    {
        /// <summary>
        /// Flight-derived parameter estimates, one symbol per maneuver. Scattered points: there is
        /// nothing between them, so they are never interpolated.
        /// </summary>
        FlightParameterEstimate = 0,

        /// <summary>A trend line the authors drew through their estimates. A drawing, not data.</summary>
        AuthorTrendLine = 1,

        /// <summary>A flight-measured time history.</summary>
        FlightMeasuredTimeHistory = 2,

        /// <summary>A simulation output time history.</summary>
        SimulationTimeHistory = 3
    }

    /// <summary>
    /// One digitized series of NASA F-15B 836 validation data.
    ///
    /// VALIDATION ONLY. These numbers are targets a model is compared against. They are never
    /// coefficients, never inputs to the aerodynamic model, and never read by runtime code: the
    /// F-15 validation suite fails if any file outside the Validation folder names this type.
    ///
    /// Every series carries its report, figure, page, configuration, flight condition as the
    /// source states it, axes and units, what kind of evidence it is, and how it was digitized
    /// with what uncertainty. A value is read only at a digitized sample, or interpolated between
    /// two adjacent samples of a line-type series - never outside the digitized domain, never
    /// across an omitted sample, and never between scattered estimates.
    /// </summary>
    public sealed class MavF15Nasa836ValidationSeries
    {
        public readonly string id;
        public readonly string report;
        public readonly string figure;
        public readonly string pageReference;
        public readonly MavF15ValidationDataKind kind;

        /// <summary>The configuration flown or simulated, in the source's own terms.</summary>
        public readonly string configuration;

        /// <summary>Mach, altitude and angle of attack as the source states them - or that it does not.</summary>
        public readonly string flightCondition;

        public readonly string xQuantity;
        public readonly string xUnits;
        public readonly string yQuantity;
        public readonly string yUnits;

        /// <summary>Method, calibration check, omitted samples and the basis of the uncertainty.</summary>
        public readonly string digitization;

        public readonly double xUncertainty;
        public readonly double yUncertainty;

        /// <summary>
        /// Largest x-gap across which two adjacent samples may be joined. Zero for scattered
        /// estimates; a little over one sample step for time histories, so an omitted sample
        /// breaks the line.
        /// </summary>
        public readonly double maxInterpolationSpan;

        private readonly double[] x;
        private readonly double[] y;

        public MavF15Nasa836ValidationSeries(
            string id, string report, string figure, string pageReference,
            MavF15ValidationDataKind kind, string configuration, string flightCondition,
            string xQuantity, string xUnits, string yQuantity, string yUnits,
            string digitization, double xUncertainty, double yUncertainty,
            double maxInterpolationSpan, double[] x, double[] y)
        {
            if (x == null || y == null || x.Length != y.Length)
                throw new ArgumentException("series " + id + ": x and y must be the same length");

            this.id = id;
            this.report = report;
            this.figure = figure;
            this.pageReference = pageReference;
            this.kind = kind;
            this.configuration = configuration;
            this.flightCondition = flightCondition;
            this.xQuantity = xQuantity;
            this.xUnits = xUnits;
            this.yQuantity = yQuantity;
            this.yUnits = yUnits;
            this.digitization = digitization;
            this.xUncertainty = xUncertainty;
            this.yUncertainty = yUncertainty;
            this.maxInterpolationSpan =
                kind == MavF15ValidationDataKind.FlightParameterEstimate ? 0.0 : maxInterpolationSpan;
            this.x = (double[])x.Clone();
            this.y = (double[])y.Clone();
        }

        /// <summary>Always the original NASA publication of these results.</summary>
        public MavF15SourceLineage Lineage
        {
            get { return MavF15SourceLineage.OriginalPrimary; }
        }

        /// <summary>Always the exact target. Anything else is catalogued as excluded instead.</summary>
        public MavF15ConfigurationScope Scope
        {
            get { return MavF15ConfigurationScope.Exact836; }
        }

        public bool ValidationOnly
        {
            get { return true; }
        }

        public int Count
        {
            get { return x.Length; }
        }

        public double X(int i) { return x[i]; }
        public double Y(int i) { return y[i]; }

        public double DomainMin
        {
            get { return x.Length > 0 ? x[0] : double.NaN; }
        }

        public double DomainMax
        {
            get { return x.Length > 0 ? x[x.Length - 1] : double.NaN; }
        }

        /// <summary>True when x strictly increases, which a line-type series requires.</summary>
        public bool IsStrictlyIncreasing()
        {
            for (int i = 1; i < x.Length; i++)
            {
                if (!(x[i] > x[i - 1]))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// The digitized value at xq, or linear interpolation between the two samples that bracket
        /// it. Refused when:
        /// the series is scattered estimates; xq is outside the digitized domain; or the bracketing
        /// samples are further apart than <see cref="maxInterpolationSpan"/>.
        /// </summary>
        public bool TryInterpolate(double xq, out double yq, out string reason)
        {
            yq = double.NaN;

            if (kind == MavF15ValidationDataKind.FlightParameterEstimate)
            {
                reason = "scattered flight estimates are never interpolated";
                return false;
            }

            if (x.Length < 2 || double.IsNaN(xq) || xq < x[0] || xq > x[x.Length - 1])
            {
                reason = "outside the digitized domain; no extrapolation";
                return false;
            }

            for (int i = 0; i < x.Length; i++)
            {
                // At a digitized sample the answer is that sample, not a blend of its neighbours.
                if (xq == x[i])
                {
                    yq = y[i];
                    reason = "OK";
                    return true;
                }
            }

            for (int i = 1; i < x.Length; i++)
            {
                if (xq > x[i])
                    continue;

                double span = x[i] - x[i - 1];
                if (span > maxInterpolationSpan)
                {
                    reason = "the bracketing samples are " + span.ToString("G4") + " " + xUnits
                             + " apart, wider than " + maxInterpolationSpan.ToString("G4")
                             + ": an omitted sample is not filled";
                    return false;
                }

                double f = span > 0.0 ? (xq - x[i - 1]) / span : 0.0;
                yq = y[i - 1] + f * (y[i] - y[i - 1]);
                reason = "OK";
                return true;
            }

            reason = "outside the digitized domain; no extrapolation";
            return false;
        }
    }

    /// <summary>A held figure deliberately NOT digitized, and why.</summary>
    public sealed class MavF15ExcludedValidationFigure
    {
        public readonly string report;
        public readonly string figure;
        public readonly string reason;

        public MavF15ExcludedValidationFigure(string report, string figure, string reason)
        {
            this.report = report;
            this.figure = figure;
            this.reason = reason;
        }
    }

    /// <summary>
    /// Digitized NASA F-15B 836 validation data - OriginalPrimary x Exact836 x ValidationOnly.
    ///
    /// Sources: NASA/TM-2008-214634 figs. 12-15 (baseline flight series) and NASA/TM-2012-215978
    /// figs. 27-29 (CAS-off baseline simulation at report table 2's three conditions). What was
    /// held and not digitized, and why, is in <see cref="Excluded"/>. Full record:
    /// Docs/Reference/F15_836_VALIDATION_DATA_V1.0.md.
    ///
    /// Nothing in F15/ or Core/ may read this. These are comparison targets, not model data.
    /// </summary>
    public static partial class MavF15Nasa836ValidationData
    {
        public const string Use = "VALIDATION_ONLY";

        private static ReadOnlyCollection<MavF15Nasa836ValidationSeries> series;
        private static ReadOnlyCollection<MavF15ExcludedValidationFigure> excluded;

        public static ReadOnlyCollection<MavF15Nasa836ValidationSeries> All
        {
            get { return series ?? (series = BuildSeries()); }
        }

        public static ReadOnlyCollection<MavF15ExcludedValidationFigure> Excluded
        {
            get { return excluded ?? (excluded = BuildExcluded()); }
        }

        public static MavF15Nasa836ValidationSeries Find(string id)
        {
            ReadOnlyCollection<MavF15Nasa836ValidationSeries> all = All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].id == id)
                    return all[i];
            }

            return null;
        }
    }
}
