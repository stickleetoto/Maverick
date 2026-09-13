using UnityEngine;

namespace MaverickFresh.FlightDynamics.F16
{
    /// <summary>
    /// Frozen NASA TP-1538 Table VI thrust deck for the Maverick reference F-16.
    ///
    /// DATA AUTHORITY:
    ///   Nguyen et al., NASA TP-1538 (1979), Appendix B, Table VI,
    ///   "THRUST VALUES USED IN SIMULATION", NTRS 19800005879.
    ///
    /// RUNTIME SEMANTICS:
    ///   Garza & Morelli, NASA/TM-2003-212145 (2003), F-16 engine model.
    ///   Tidle/Tmil/Tmax are linearly interpolated in altitude and Mach, then actual
    ///   engine power Pa blends Tidle->Tmil for Pa<50 and Tmil->Tmax for Pa>=50.
    ///
    /// NO EXTRAPOLATION. The public sources used for this freeze do not establish a
    /// defensible thrust policy outside the published altitude/Mach table. Queries
    /// outside the table, outside Pa=[0,100], or containing NaN/Infinity are rejected.
    ///
    /// The table values below are the PUBLISHED SI integer values, not values regenerated
    /// from a newer lbf conversion constant. Negative Tidle entries are intentional source
    /// values and are preserved exactly.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MavF16Tp1538ThrustDeck : MavFrozenThrustDeckBase
    {
        // Canonical dataset identity. The runtime uses the PUBLISHED SI rows from this frozen
        // raw source artifact; table_vi_si_converted.csv remains a derived cross-check only.
        public const string CanonicalRawSourceSha256 =
            "9e0d9906daec75797560e37c6ae490ec10630495658ad9a93216ddacdc940972";

        public const string FrozenSourceIdentity =
            "NASA TP-1538 / NTRS 19800005879 / Table VI / rawSourceCsv sha256 "
            + CanonicalRawSourceSha256;
        public const string FrozenSourceVersion = "F16_TP1538_THRUST_DECK_V0.1";

        // MavThrustDeckProvenance FNV-1a hash over FrozenSourceIdentity, FrozenSourceVersion,
        // and the exact runtime float axes/tables below. Because FrozenSourceIdentity embeds the
        // canonical rawSourceCsv SHA-256, this bit-level runtime hash is DERIVED FROM the frozen
        // dataset identity rather than being a parallel, free-floating identity. The SHA-256 remains
        // the canonical artifact identity; this compact hash only detects runtime-array drift.
        public const uint FrozenTableHash = 3637344115u; // 0xD8CD7773

        public const float MinAltitudeM = 0f;
        public const float MaxAltitudeM = 15240f;
        public const float MinMach = 0.2f;
        public const float MaxMach = 1.0f;
        public const float MinPowerPercent = 0f;
        public const float MaxPowerPercent = 100f;

        private const string Citation =
            "Nguyen et al., NASA TP-1538 (1979), Appendix B, Table VI; "
            + "runtime interpolation/power blend: Garza & Morelli NASA/TM-2003-212145";

        private const string StatusInside = "TP-1538 Table VI sourced thrust: inside frozen envelope";
        private const string StatusNonFinite = "TP-1538 thrust query rejected: non-finite input";
        private const string StatusPowerOutside = "TP-1538 thrust query rejected: actual power outside sourced 0..100 state";
        private const string StatusEnvelopeOutside = "TP-1538 thrust query rejected: altitude/Mach outside frozen Table VI envelope";
        private const string StatusProvenanceMismatch = "TP-1538 thrust unavailable: runtime table does not match frozen provenance";
        private const string StatusMalformed = "TP-1538 thrust unavailable: runtime table is malformed";
        private const string StatusProvenanceOk = "frozen TP-1538 runtime table provenance verified";

        // Row-major [altitudeIndex * machCount + machIndex], exactly the layout expected
        // by MavThrustDeckMath.TryBilinearSample. The source CSV is stored Mach-major;
        // this transpose is structural only and is pinned by validation against all 90 cells.
        private static readonly float[] FrozenAltitudeAxisM =
        {
            0f, 3048f, 6096f, 9144f, 12192f, 15240f
        };

        private static readonly float[] FrozenMachAxis =
        {
            0.2f, 0.4f, 0.6f, 0.8f, 1.0f
        };

        private static readonly float[] FrozenIdleThrustN =
        {
             2824f,   267f, -4537f, -12010f, -16013f,
             1890f,   111f, -3158f,  -8451f,  -6227f,
             3069f,  1535f, -1334f,  -5782f,  -2647f,
             4492f,  3358f,  1557f,  -1099f,  -1521f,
             5916f,  5026f,  4048f,   2669f,   -890f,
             7562f,  6783f,  6049f,   4893f,   3114f
        };

        private static readonly float[] FrozenMilitaryThrustN =
        {
            56401f, 56089f, 56223f, 55111f, 51953f,
            40699f, 41420f, 43764f, 45263f, 43804f,
            28080f, 29401f, 31536f, 34472f, 35806f,
            17970f, 19082f, 20728f, 23663f, 27133f,
            10987f, 11565f, 12632f, 14456f, 16902f,
             6227f,  6939f,  7384f,  8585f, 10275f
        };

        private static readonly float[] FrozenMaximumThrustN =
        {
             95276f, 100970f, 107820f, 115959f, 128485f,
             69834f,  74993f,  84112f,  93742f, 103723f,
             49929f,  54488f,  61204f,  71057f,  81398f,
             32573f,  36269f,  41300f,  49440f,  59977f,
             19727f,  22240f,  25354f,  30513f,  38440f,
             11565f,  12610f,  14300f,  17570f,  22494f
        };

        public override string SourceIdentity
        {
            get { return FrozenSourceIdentity; }
        }

        public override string SourceVersion
        {
            get { return FrozenSourceVersion; }
        }

        public override uint ExpectedTableHash
        {
            get { return FrozenTableHash; }
        }

        public override string DeckName
        {
            get { return "F-16 NASA TP-1538 Table VI v0.1"; }
        }

        public override MavEnvelopeExcursionPolicy ExcursionPolicy
        {
            get { return MavEnvelopeExcursionPolicy.RejectUnsupportedState; }
        }

        /// <summary>
        /// Authority is recomputed from the live table bits. No inspector toggle can raise it,
        /// and a one-bit mutation immediately removes the authoritative claim.
        /// </summary>
        public override MavThrustDataAuthority Authority
        {
            get
            {
                if (!HasUsableTables())
                    return MavThrustDataAuthority.Unavailable;

                return RuntimeTableHashMatchesFrozen()
                    ? MavThrustDataAuthority.Authoritative
                    : MavThrustDataAuthority.SyntheticBench;
            }
        }

        private void Awake()
        {
            EnsureFrozenDataInstalled();
            dataSourceCitation = Citation;
            excursionPolicy = MavEnvelopeExcursionPolicy.RejectUnsupportedState;
            debugProvenanceStatus = RuntimeTableHashMatchesFrozen()
                ? StatusProvenanceOk
                : StatusProvenanceMismatch;
        }

        /// <summary>
        /// Runtime-added components start with empty inherited arrays. Populate only that pristine
        /// state. A partially/fully serialized table is NEVER overwritten here: if someone changed
        /// it, the provenance hash must see the change instead of silently repairing it.
        /// </summary>
        internal void EnsureFrozenDataInstalled()
        {
            bool pristine =
                (altitudeAxisM == null || altitudeAxisM.Length == 0)
                && (machAxis == null || machAxis.Length == 0)
                && (idleThrustN == null || idleThrustN.Length == 0)
                && (militaryThrustN == null || militaryThrustN.Length == 0)
                && (maximumThrustN == null || maximumThrustN.Length == 0);

            if (!pristine)
                return;

            altitudeAxisM = (float[])FrozenAltitudeAxisM.Clone();
            machAxis = (float[])FrozenMachAxis.Clone();
            idleThrustN = (float[])FrozenIdleThrustN.Clone();
            militaryThrustN = (float[])FrozenMilitaryThrustN.Clone();
            maximumThrustN = (float[])FrozenMaximumThrustN.Clone();
        }

        private bool RuntimeTableHashMatchesFrozen()
        {
            if (!HasUsableTables())
                return false;

            uint actual = MavThrustDeckProvenance.ComputeTableHash(
                FrozenSourceIdentity,
                FrozenSourceVersion,
                altitudeAxisM,
                machAxis,
                idleThrustN,
                militaryThrustN,
                maximumThrustN);
            return actual == FrozenTableHash;
        }

        public override MavThrustDeckResult Evaluate(MavThrustDeckQuery query)
        {
            MavThrustDeckResult result = EvaluateInternal(
                altitudeAxisM,
                machAxis,
                idleThrustN,
                militaryThrustN,
                maximumThrustN,
                RuntimeTableHashMatchesFrozen(),
                query);

            debugLastResult = result;
            debugProvenanceStatus = RuntimeTableHashMatchesFrozen()
                ? StatusProvenanceOk
                : StatusProvenanceMismatch;
            return result;
        }

        /// <summary>
        /// Pure canonical path for independent Unity validation. It has no GameObject dependency and
        /// exercises the same source semantics as the component path.
        /// </summary>
        public static MavThrustDeckResult EvaluateFrozen(MavThrustDeckQuery query)
        {
            return EvaluateInternal(
                FrozenAltitudeAxisM,
                FrozenMachAxis,
                FrozenIdleThrustN,
                FrozenMilitaryThrustN,
                FrozenMaximumThrustN,
                true,
                query);
        }

        private static MavThrustDeckResult EvaluateInternal(
            float[] altitudeM,
            float[] mach,
            float[] idle,
            float[] military,
            float[] maximum,
            bool provenanceVerified,
            MavThrustDeckQuery query)
        {
            if (!provenanceVerified)
                return Unavailable(StatusProvenanceMismatch);

            if (!MavThrustDeckMath.IsStrictlyAscending(altitudeM)
                || !MavThrustDeckMath.IsStrictlyAscending(mach)
                || idle == null || military == null || maximum == null
                || idle.Length != altitudeM.Length * mach.Length
                || military.Length != idle.Length
                || maximum.Length != idle.Length)
            {
                return Unavailable(StatusMalformed);
            }

            if (!Finite(query.altitudeM)
                || !Finite(query.mach)
                || !Finite(query.actualPowerPercent))
            {
                return Unavailable(StatusNonFinite);
            }

            if (query.actualPowerPercent < MinPowerPercent
                || query.actualPowerPercent > MaxPowerPercent)
            {
                return Unavailable(StatusPowerOutside);
            }

            if (query.altitudeM < MinAltitudeM
                || query.altitudeM > MaxAltitudeM
                || query.mach < MinMach
                || query.mach > MaxMach)
            {
                return Unavailable(StatusEnvelopeOutside);
            }

            MavThrustDeckResult result = MavTabulatedThrustDeck.EvaluateTables(
                altitudeM,
                mach,
                idle,
                military,
                maximum,
                MavThrustDataAuthority.Authoritative,
                MavEnvelopeExcursionPolicy.RejectUnsupportedState,
                query);

            // The explicit bounds above mean any valid result must be an in-envelope source result.
            // If the shared math ever disagrees, fail closed instead of laundering the discrepancy.
            if (!result.valid || !result.insideEnvelope
                || result.authority != MavThrustDataAuthority.Authoritative)
            {
                return Unavailable(StatusMalformed);
            }

            result.statusReason = StatusInside;
            return result;
        }

        private static MavThrustDeckResult Unavailable(string reason)
        {
            return MavThrustDeckResult.Unavailable(reason);
        }

        private static bool Finite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
