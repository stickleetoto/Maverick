using UnityEngine;

namespace MaverickFresh.FlightDynamics.F16
{
    /// <summary>
    /// SYNTHETIC BENCH THRUST DECK - NOT AIRCRAFT DATA.
    ///
    /// Every number in this component was made up to exercise the propulsion architecture. It is
    /// not F-16 data, not F110 or F100 data, not derived from any publication, and not an
    /// approximation of any real engine. It exists so the deck interface, the interpolation, the
    /// envelope policy, the powered-trim path and the throttle inversion can be tested end to end
    /// while the real thrust deck remains unavailable.
    ///
    /// Safety properties, all deliberate:
    ///
    ///   - <see cref="Authority"/> is hard-coded to <see cref="MavThrustDataAuthority.SyntheticBench"/>
    ///     and cannot be raised from the inspector. There is no field to set it to Authoritative.
    ///   - <see cref="MavThrustDeckBase.IsAcceptableForLiveFlight"/> is therefore always false, so a
    ///     stack using this deck can never reach OPERATIONALLY_LIVE_READY on propulsion grounds.
    ///   - The F-16 auto-setup never attaches it. It has to be added by hand, deliberately.
    ///   - The numbers are round and obviously artificial, so nobody can mistake a survey printed
    ///     from them for reference output.
    ///
    /// The shape is generic turbofan-ish only so the architecture is exercised over a realistic
    /// range of magnitudes: thrust falling with altitude via density ratio, a mild Mach term, and
    /// the sourced Garza/Morelli idle/military/maximum power blend on top. None of that shape is a
    /// claim about any aircraft.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MavSyntheticBenchThrustDeck : MavThrustDeckBase
    {
        [Header("SYNTHETIC BENCH DATA - NOT AIRCRAFT DATA, NOT VALID FOR FLIGHT")]
        [Tooltip("Made-up sea-level static idle thrust, Newtons. Round number, chosen for testing.")]
        public float syntheticIdleThrustSeaLevelN = 5000f;

        [Tooltip("Made-up sea-level static military thrust, Newtons. Round number, chosen for testing.")]
        public float syntheticMilitaryThrustSeaLevelN = 60000f;

        [Tooltip("Made-up sea-level static maximum thrust, Newtons. Round number, chosen for testing.")]
        public float syntheticMaximumThrustSeaLevelN = 110000f;

        [Tooltip("Made-up Mach lapse coefficient. Not a measured ram-recovery characteristic.")]
        public float syntheticMachFactor = 0.25f;

        [Header("Envelope")]
        [Tooltip("Upper altitude of the synthetic envelope, metres.")]
        public float envelopeCeilingM = 15000f;

        [Tooltip("Upper Mach of the synthetic envelope.")]
        public float envelopeMaxMach = 1.6f;

        public MavEnvelopeExcursionPolicy policy = MavEnvelopeExcursionPolicy.ClampToValidatedEnvelope;

        [Header("Debug")]
        public MavThrustDeckResult debugLastResult;

        public override string DeckName
        {
            get { return "SYNTHETIC BENCH deck (invented numbers, NOT aircraft data)"; }
        }

        /// <summary>
        /// Hard-coded. There is deliberately no way to declare invented numbers authoritative.
        /// </summary>
        public override MavThrustDataAuthority Authority
        {
            get { return MavThrustDataAuthority.SyntheticBench; }
        }

        public override MavEnvelopeExcursionPolicy ExcursionPolicy
        {
            get { return policy; }
        }

        public override MavThrustDeckResult Evaluate(MavThrustDeckQuery query)
        {
            debugLastResult = EvaluateSynthetic(
                query,
                syntheticIdleThrustSeaLevelN,
                syntheticMilitaryThrustSeaLevelN,
                syntheticMaximumThrustSeaLevelN,
                syntheticMachFactor,
                envelopeCeilingM,
                envelopeMaxMach,
                policy);

            return debugLastResult;
        }

        /// <summary>
        /// Pure synthetic evaluation, so the architecture around it can be validated without a
        /// GameObject. Monotone in power and in throttle by construction, which is what makes it
        /// useful for exercising the trim solver's throttle inversion.
        /// </summary>
        public static MavThrustDeckResult EvaluateSynthetic(
            MavThrustDeckQuery query,
            float idleSeaLevelN,
            float militarySeaLevelN,
            float maximumSeaLevelN,
            float machFactor,
            float ceilingM,
            float maxMach,
            MavEnvelopeExcursionPolicy policy)
        {
            bool insideEnvelope =
                query.altitudeM >= -1e-3f
                && query.altitudeM <= ceilingM + 1e-3f
                && query.mach >= -1e-3f
                && query.mach <= maxMach + 1e-3f;

            if (!insideEnvelope && policy == MavEnvelopeExcursionPolicy.RejectUnsupportedState)
            {
                return MavThrustDeckResult.Unavailable(
                    "state is outside the synthetic bench envelope and the policy is to reject it");
            }

            float altitudeM = query.altitudeM;
            float mach = query.mach;

            if (policy != MavEnvelopeExcursionPolicy.MarkNonAuthoritativeExtrapolation)
            {
                altitudeM = Mathf.Clamp(altitudeM, 0f, ceilingM);
                mach = Mathf.Clamp(mach, 0f, maxMach);
            }
            else
            {
                // Bounded even when extrapolating: an unbacked number must still be finite.
                altitudeM = Mathf.Clamp(altitudeM, -1000f, ceilingM * 1.5f);
                mach = Mathf.Clamp(mach, 0f, maxMach * 1.5f);
            }

            // Density ratio against the same atmosphere the rest of the core uses, so the synthetic
            // deck at least behaves consistently with the simulated air.
            float seaLevelDensity = MavAtmosphereModel.Sample(0f).densityKgM3;
            float density = MavAtmosphereModel.Sample(altitudeM).densityKgM3;
            float densityRatio = seaLevelDensity > 0f ? Mathf.Clamp01(density / seaLevelDensity) : 0f;

            float machTerm = 1f + Mathf.Max(-0.9f, machFactor * Mathf.Max(0f, mach));
            float lapse = densityRatio * machTerm;

            float idle = idleSeaLevelN * lapse;
            float military = militarySeaLevelN * lapse;
            float maximum = maximumSeaLevelN * lapse;

            MavThrustDeckResult result = new MavThrustDeckResult();
            result.valid = true;
            result.insideEnvelope = insideEnvelope;
            result.thrustN = MavThrustDeckMath.BlendByPowerPercent(
                idle, military, maximum, query.actualPowerPercent);

            // Synthetic data can never be upgraded, only reported.
            result.authority = MavThrustDataAuthority.SyntheticBench;
            result.statusReason = insideEnvelope
                ? "SYNTHETIC BENCH data, inside the synthetic envelope"
                : "SYNTHETIC BENCH data, outside the synthetic envelope";

            return result;
        }
    }
}
