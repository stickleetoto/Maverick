using System;
using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>
    /// Where a thrust deck's numbers came from. This is the honesty flag for dimensional thrust and
    /// it travels with every result, so no layer can present bench numbers as reference data.
    /// </summary>
    public enum MavThrustDataAuthority
    {
        /// <summary>No dimensional thrust data at all. Thrust is exactly zero and says so.</summary>
        Unavailable = 0,

        /// <summary>
        /// Deliberately invented numbers used to exercise the architecture. NEVER an aircraft
        /// reference, never valid for live flight, and never to be described as one.
        /// </summary>
        SyntheticBench = 1,

        /// <summary>Frozen from an approved source, with the source recorded in the repository.</summary>
        Authoritative = 2
    }

    /// <summary>
    /// What a deck does when asked about a state outside the data it actually has.
    ///
    /// Silent unbounded extrapolation is not an option here. Every policy is explicit, and only
    /// <see cref="ClampToValidatedEnvelope"/> and <see cref="RejectUnsupportedState"/> are
    /// acceptable for live flight.
    /// </summary>
    public enum MavEnvelopeExcursionPolicy
    {
        /// <summary>Clamp the query to the validated envelope edge and report the excursion.</summary>
        ClampToValidatedEnvelope = 0,

        /// <summary>Return no result at all; the caller must handle an unsupported state.</summary>
        RejectUnsupportedState = 1,

        /// <summary>
        /// Extrapolate, and mark the result non-authoritative. Useful for offline study; NOT
        /// acceptable for live flight, because the number is no longer supported by any data.
        /// </summary>
        MarkNonAuthoritativeExtrapolation = 2
    }

    /// <summary>Everything a dimensional thrust lookup depends on.</summary>
    [Serializable]
    public struct MavThrustDeckQuery
    {
        public float altitudeM;
        public float mach;

        [Tooltip("Actual engine power state in percent, 0..100. Spool dynamics are owned elsewhere; a deck is a pure lookup.")]
        public float actualPowerPercent;

        public static MavThrustDeckQuery Create(float altitudeM, float mach, float actualPowerPercent)
        {
            MavThrustDeckQuery query = new MavThrustDeckQuery();
            query.altitudeM = altitudeM;
            query.mach = mach;
            query.actualPowerPercent = actualPowerPercent;
            return query;
        }
    }

    /// <summary>Result of a dimensional thrust lookup, carrying its own provenance.</summary>
    [Serializable]
    public struct MavThrustDeckResult
    {
        [Tooltip("False when the deck could not answer at all. Callers must not treat thrustN as meaningful.")]
        public bool valid;

        public float thrustN;

        [Tooltip("False when the query was outside the deck's validated altitude/Mach envelope.")]
        public bool insideEnvelope;

        [Tooltip("Provenance of this particular number, after any excursion policy has been applied.")]
        public MavThrustDataAuthority authority;

        public string statusReason;

        public static MavThrustDeckResult Unavailable(string reason)
        {
            MavThrustDeckResult result = new MavThrustDeckResult();
            result.valid = false;
            result.thrustN = 0f;
            result.insideEnvelope = false;
            result.authority = MavThrustDataAuthority.Unavailable;
            result.statusReason = reason;
            return result;
        }
    }

    /// <summary>
    /// Aircraft-independent dimensional thrust data source:
    ///
    ///   (altitude, Mach, actual engine power) -> thrust
    ///
    /// A deck is a pure lookup. It owns no spool dynamics, no throttle gearing and no state: those
    /// belong to the propulsion model above it. Keeping them apart is what lets Maverick have
    /// sourced engine power dynamics (which it does) without also claiming sourced thrust (which it
    /// does not).
    ///
    /// A deck must never invent aircraft data. If numbers are unavailable, it reports
    /// <see cref="MavThrustDataAuthority.Unavailable"/> and returns nothing, which the whole stack
    /// then surfaces honestly.
    /// </summary>
    public abstract class MavThrustDeckBase : MonoBehaviour
    {
        public abstract string DeckName { get; }

        /// <summary>Provenance of the deck as a whole. An individual result may be downgraded further.</summary>
        public abstract MavThrustDataAuthority Authority { get; }

        public abstract MavEnvelopeExcursionPolicy ExcursionPolicy { get; }

        /// <summary>Deterministic lookup. Same query, same deck, same answer, always.</summary>
        public abstract MavThrustDeckResult Evaluate(MavThrustDeckQuery query);

        /// <summary>
        /// Whether this deck may be used to fly an aircraft for real.
        ///
        /// Requires authoritative data AND an excursion policy that keeps every returned number
        /// supported by that data. Extrapolation is deliberately excluded: a number outside the
        /// validated envelope is not evidence about the aircraft, however plausible it looks.
        /// </summary>
        public bool IsAcceptableForLiveFlight
        {
            get { return IsPolicyAcceptableForLiveFlight(Authority, ExcursionPolicy); }
        }

        /// <summary>Pure form of the live-flight acceptance rule, so validation can enumerate it.</summary>
        public static bool IsPolicyAcceptableForLiveFlight(
            MavThrustDataAuthority authority,
            MavEnvelopeExcursionPolicy policy)
        {
            if (authority != MavThrustDataAuthority.Authoritative)
                return false;

            return policy == MavEnvelopeExcursionPolicy.ClampToValidatedEnvelope
                || policy == MavEnvelopeExcursionPolicy.RejectUnsupportedState;
        }
    }

    /// <summary>
    /// Deterministic interpolation and blending mathematics for tabulated thrust decks.
    ///
    /// Pure and bounded throughout. Nothing here extrapolates on its own initiative: the caller
    /// decides what an out-of-range query means, and this class only reports that it happened.
    /// </summary>
    public static class MavThrustDeckMath
    {
        /// <summary>True when the axis is non-empty and strictly ascending, which interpolation requires.</summary>
        public static bool IsStrictlyAscending(float[] axis)
        {
            if (axis == null || axis.Length == 0)
                return false;

            for (int i = 1; i < axis.Length; i++)
            {
                if (!(axis[i] > axis[i - 1]))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// How far beyond an axis edge the extrapolation policy may reach, in grid cells. Bounded on
        /// purpose: "mark it non-authoritative" is not a licence to return an arbitrary number, so
        /// beyond this the value is held at the extrapolated edge.
        /// </summary>
        public const float MaxExtrapolationCells = 1f;

        /// <summary>
        /// Locates a value on an ascending axis, returning the lower index and the fraction to the
        /// next point.
        ///
        /// With allowExtrapolation false the fraction is clamped to 0..1, so the caller can never
        /// receive an extrapolating weight. With it true the fraction may leave that range by at
        /// most <see cref="MaxExtrapolationCells"/>, and no further. The inside flag reports whether
        /// the value was within the axis regardless.
        /// </summary>
        public static void LocateOnAxis(
            float[] axis,
            float value,
            bool allowExtrapolation,
            out int lowerIndex,
            out float fraction,
            out bool inside)
        {
            lowerIndex = 0;
            fraction = 0f;
            inside = true;

            if (axis == null || axis.Length == 0)
            {
                inside = false;
                return;
            }

            if (axis.Length == 1)
            {
                inside = Mathf.Approximately(axis[0], value);
                return;
            }

            int last = axis.Length - 1;

            if (value <= axis[0])
            {
                inside = value >= axis[0] - 1e-6f;
                lowerIndex = 0;
                float lowSpan = axis[1] - axis[0];
                fraction = allowExtrapolation && lowSpan > 0f
                    ? Mathf.Max(-MaxExtrapolationCells, (value - axis[0]) / lowSpan)
                    : 0f;
                return;
            }

            if (value >= axis[last])
            {
                inside = value <= axis[last] + 1e-6f;
                lowerIndex = last - 1;
                float highSpan = axis[last] - axis[last - 1];
                fraction = allowExtrapolation && highSpan > 0f
                    ? Mathf.Min(1f + MaxExtrapolationCells, 1f + (value - axis[last]) / highSpan)
                    : 1f;
                return;
            }

            for (int i = 1; i <= last; i++)
            {
                if (value <= axis[i])
                {
                    lowerIndex = i - 1;
                    float span = axis[i] - axis[i - 1];
                    fraction = span > 0f ? (value - axis[i - 1]) / span : 0f;
                    return;
                }
            }

            lowerIndex = last - 1;
            fraction = 1f;
        }

        /// <summary>
        /// Non-clamping linear blend. Mathf.Lerp clamps its parameter, which would silently defeat
        /// extrapolation and make the policy name a lie.
        /// </summary>
        public static float LerpUnclamped(float a, float b, float t)
        {
            return a + (b - a) * t;
        }

        /// <summary>
        /// Bilinear sample of a row-major table indexed [altitudeIndex * machCount + machIndex].
        /// Returns false when the table or axes are malformed, rather than guessing.
        /// </summary>
        public static bool TryBilinearSample(
            float[] altitudeAxisM,
            float[] machAxis,
            float[] table,
            float altitudeM,
            float mach,
            out float value,
            out bool insideEnvelope)
        {
            return TryBilinearSample(
                altitudeAxisM, machAxis, table, altitudeM, mach,
                false, out value, out insideEnvelope);
        }

        /// <summary>
        /// Bilinear sample with an explicit extrapolation choice. Extrapolation is bounded to
        /// <see cref="MaxExtrapolationCells"/> beyond each axis edge.
        /// </summary>
        public static bool TryBilinearSample(
            float[] altitudeAxisM,
            float[] machAxis,
            float[] table,
            float altitudeM,
            float mach,
            bool allowExtrapolation,
            out float value,
            out bool insideEnvelope)
        {
            value = 0f;
            insideEnvelope = false;

            if (!IsStrictlyAscending(altitudeAxisM) || !IsStrictlyAscending(machAxis))
                return false;

            if (table == null || table.Length != altitudeAxisM.Length * machAxis.Length)
                return false;

            int altitudeIndex;
            float altitudeFraction;
            bool altitudeInside;
            LocateOnAxis(altitudeAxisM, altitudeM, allowExtrapolation,
                out altitudeIndex, out altitudeFraction, out altitudeInside);

            int machIndex;
            float machFraction;
            bool machInside;
            LocateOnAxis(machAxis, mach, allowExtrapolation,
                out machIndex, out machFraction, out machInside);

            insideEnvelope = altitudeInside && machInside;

            int machCount = machAxis.Length;
            int altitudeUpper = Mathf.Min(altitudeIndex + 1, altitudeAxisM.Length - 1);
            int machUpper = Mathf.Min(machIndex + 1, machCount - 1);

            float v00 = table[altitudeIndex * machCount + machIndex];
            float v01 = table[altitudeIndex * machCount + machUpper];
            float v10 = table[altitudeUpper * machCount + machIndex];
            float v11 = table[altitudeUpper * machCount + machUpper];

            float low = LerpUnclamped(v00, v01, machFraction);
            float high = LerpUnclamped(v10, v11, machFraction);
            value = LerpUnclamped(low, high, altitudeFraction);
            return true;
        }

        /// <summary>
        /// Blends idle / military / maximum thrust by actual engine power percent.
        ///
        /// This equation IS sourced - it is the Garza/Morelli form already frozen in
        /// F16/F16_PROPULSION_REFERENCE_V0.1.md. Only the numeric decks it consumes are missing,
        /// which is exactly why the two are kept apart.
        /// </summary>
        public static float BlendByPowerPercent(
            float idleThrustN,
            float militaryThrustN,
            float maximumThrustN,
            float actualPowerPercent)
        {
            float power = Mathf.Clamp(actualPowerPercent, 0f, 100f);

            if (power < 50f)
                return idleThrustN + (militaryThrustN - idleThrustN) * (power / 50f);

            return militaryThrustN + (maximumThrustN - militaryThrustN) * ((power - 50f) / 50f);
        }

        /// <summary>
        /// Checks that a sampled sequence is monotonic within tolerance, in either direction.
        ///
        /// Used before inverting thrust for a throttle setting. Endpoints differing is not evidence
        /// of monotonicity: a deck can rise, fall and rise again between them, and a bisection on
        /// that data will happily converge on a throttle that does not produce the requested thrust.
        /// </summary>
        public static bool IsMonotonic(float[] samples, float tolerance)
        {
            if (samples == null || samples.Length < 2)
                return true;

            bool nonDecreasing = true;
            bool nonIncreasing = true;

            for (int i = 1; i < samples.Length; i++)
            {
                float delta = samples[i] - samples[i - 1];
                if (delta < -tolerance)
                    nonDecreasing = false;
                if (delta > tolerance)
                    nonIncreasing = false;
            }

            return nonDecreasing || nonIncreasing;
        }
    }

    /// <summary>
    /// Tabulated altitude/Mach thrust deck with the sourced power blend on top.
    ///
    /// The three tables (idle, military, maximum) are supplied by a concrete subclass or by the
    /// inspector. This class supplies only the deterministic lookup and the excursion policy; it
    /// contains no aircraft numbers of its own, and its authority defaults to
    /// <see cref="MavThrustDataAuthority.Unavailable"/> so an unconfigured deck is honest by
    /// default rather than silently zero-valued.
    /// </summary>
    [DisallowMultipleComponent]
    public class MavTabulatedThrustDeck : MavThrustDeckBase
    {
        [Header("Provenance (must be set deliberately)")]
        [Tooltip("Where these numbers came from. Unavailable until a real source is frozen. NEVER set Authoritative for invented data.")]
        public MavThrustDataAuthority declaredAuthority = MavThrustDataAuthority.Unavailable;

        [Tooltip("Human-readable citation for the data. Required for anything claiming Authoritative.")]
        public string dataSourceCitation = "none";

        [Tooltip("Behaviour outside the tabulated envelope. Extrapolation is never acceptable for live flight.")]
        public MavEnvelopeExcursionPolicy excursionPolicy = MavEnvelopeExcursionPolicy.ClampToValidatedEnvelope;

        [Header("Axes (must be strictly ascending)")]
        public float[] altitudeAxisM = new float[0];
        public float[] machAxis = new float[0];

        [Header("Tables, row-major [altitudeIndex * machCount + machIndex], Newtons")]
        public float[] idleThrustN = new float[0];
        public float[] militaryThrustN = new float[0];
        public float[] maximumThrustN = new float[0];

        [Header("Debug")]
        public MavThrustDeckResult debugLastResult;

        public override string DeckName
        {
            get { return "Tabulated thrust deck (" + dataSourceCitation + ")"; }
        }

        public override MavThrustDataAuthority Authority
        {
            get { return HasUsableTables() ? declaredAuthority : MavThrustDataAuthority.Unavailable; }
        }

        public override MavEnvelopeExcursionPolicy ExcursionPolicy
        {
            get { return excursionPolicy; }
        }

        public bool HasUsableTables()
        {
            int expected = (altitudeAxisM != null ? altitudeAxisM.Length : 0)
                           * (machAxis != null ? machAxis.Length : 0);

            return expected > 0
                && MavThrustDeckMath.IsStrictlyAscending(altitudeAxisM)
                && MavThrustDeckMath.IsStrictlyAscending(machAxis)
                && idleThrustN != null && idleThrustN.Length == expected
                && militaryThrustN != null && militaryThrustN.Length == expected
                && maximumThrustN != null && maximumThrustN.Length == expected;
        }

        public override MavThrustDeckResult Evaluate(MavThrustDeckQuery query)
        {
            debugLastResult = EvaluateTables(
                altitudeAxisM, machAxis,
                idleThrustN, militaryThrustN, maximumThrustN,
                Authority, excursionPolicy, query);

            return debugLastResult;
        }

        /// <summary>
        /// Pure deck evaluation, so the lookup, the blend and the excursion policy can all be
        /// exercised without a GameObject.
        /// </summary>
        public static MavThrustDeckResult EvaluateTables(
            float[] altitudeAxisM,
            float[] machAxis,
            float[] idleThrustN,
            float[] militaryThrustN,
            float[] maximumThrustN,
            MavThrustDataAuthority authority,
            MavEnvelopeExcursionPolicy excursionPolicy,
            MavThrustDeckQuery query)
        {
            if (authority == MavThrustDataAuthority.Unavailable)
                return MavThrustDeckResult.Unavailable("no dimensional thrust data is frozen for this aircraft");

            bool allowExtrapolation =
                excursionPolicy == MavEnvelopeExcursionPolicy.MarkNonAuthoritativeExtrapolation;

            // Each sample is taken on its own statement rather than in a short-circuiting chain, so
            // every out parameter is definitely assigned whatever happens.
            float idle, military, maximum;
            bool idleInside, militaryInside, maximumInside;

            bool idleOk = MavThrustDeckMath.TryBilinearSample(
                altitudeAxisM, machAxis, idleThrustN,
                query.altitudeM, query.mach, allowExtrapolation, out idle, out idleInside);

            bool militaryOk = MavThrustDeckMath.TryBilinearSample(
                altitudeAxisM, machAxis, militaryThrustN,
                query.altitudeM, query.mach, allowExtrapolation, out military, out militaryInside);

            bool maximumOk = MavThrustDeckMath.TryBilinearSample(
                altitudeAxisM, machAxis, maximumThrustN,
                query.altitudeM, query.mach, allowExtrapolation, out maximum, out maximumInside);

            bool ok = idleOk && militaryOk && maximumOk;

            if (!ok)
                return MavThrustDeckResult.Unavailable("thrust deck tables are malformed");

            bool insideEnvelope = idleInside && militaryInside && maximumInside;

            if (!insideEnvelope && excursionPolicy == MavEnvelopeExcursionPolicy.RejectUnsupportedState)
            {
                return MavThrustDeckResult.Unavailable(
                    "state is outside the tabulated thrust envelope and the deck policy is to reject it");
            }

            MavThrustDeckResult result = new MavThrustDeckResult();
            result.valid = true;
            result.insideEnvelope = insideEnvelope;
            result.thrustN = MavThrustDeckMath.BlendByPowerPercent(
                idle, military, maximum, query.actualPowerPercent);

            // The sample is clamped to the envelope edge by construction, so an excursion under the
            // clamp policy is still supported data - it is simply the edge value, and it is
            // reported as an excursion. Under the extrapolation policy the number is no longer
            // backed by data, so its authority is downgraded regardless of the deck's own claim.
            if (insideEnvelope)
            {
                result.authority = authority;
                result.statusReason = "inside validated envelope";
            }
            else if (excursionPolicy == MavEnvelopeExcursionPolicy.ClampToValidatedEnvelope)
            {
                result.authority = authority;
                result.statusReason = "clamped to the validated envelope edge";
            }
            else
            {
                // Genuinely extrapolated, and bounded to one grid cell beyond the edge. The number
                // is no longer backed by data, so its authority is downgraded whatever the deck
                // claims - and IsAcceptableForLiveFlight rejects this policy outright.
                result.authority = MavThrustDataAuthority.SyntheticBench;
                result.statusReason = "outside the validated envelope: extrapolated, not authoritative";
            }

            return result;
        }
    }
}
