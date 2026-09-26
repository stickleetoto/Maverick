namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// Identity of the F-15 RESEARCH configuration: the AFIT / Baumann / Davison model at its one
    /// source condition, Mach 0.6 and 20,000 ft.
    ///
    /// THIS IS NOT NASA F-15B 836. It is the McAir ARO10 / 1988 F-15 Aerobase lineage as curve-fitted
    /// by Baumann (AFIT/GAE/ENY/89D-01) and carried forward by Davison (AFIT/GAE/ENY/92M-01, DTIC
    /// ADA256613). It flies on that model's own reference geometry, mass, inertia and thrust, and on
    /// nothing of the exact target's. The exact target is <see cref="MavF15ReferenceData"/>, and the
    /// two identities are kept textually apart so neither can be mistaken for the other in a log.
    /// </summary>
    public static class MavF15AfitResearchIdentity
    {
        /// <summary>Configuration / profile id. Carries no exact-target token by construction.</summary>
        public const string ConfigurationId = "F15_AFIT_BAUMANN_DAVISON_MACH06_20K_RESEARCH";

        /// <summary>The single condition the research model represents.</summary>
        public const string SourceConditionLabel = "M 0.6 / 20,000 ft (6,096 m)";

        public const string DisplayName =
            "F-15 AFIT/Baumann/Davison research - " + SourceConditionLabel + " - NOT NASA 836";

        public const string SourceLineage =
            "McAir ARO10 / 1988 F-15 Aerobase (not public) -> Baumann AFIT/GAE/ENY/89D-01 "
            + "(DTIC ADA217366) SAS curve fits -> Davison AFIT/GAE/ENY/92M-01 (DTIC ADA256613) "
            + "Appendix C, transcribed and audited in Maverick R2.";

        /// <summary>
        /// Tokens that belong to the exact NASA 836 target and must never appear in a research
        /// identity string.
        /// </summary>
        public static readonly string[] ExactTargetTokens =
        {
            "NASA_F15B_836", "74-0141", "PRE_QUIET_SPIKE", "EXACT"
        };

        /// <summary>True when <paramref name="id"/> contains none of <see cref="ExactTargetTokens"/>.</summary>
        public static bool CarriesNoExactTargetToken(string id)
        {
            if (id == null)
                return true;

            string upper = id.ToUpperInvariant();
            for (int i = 0; i < ExactTargetTokens.Length; i++)
            {
                if (upper.Contains(ExactTargetTokens[i]))
                    return false;
            }

            return true;
        }
    }
}
