namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// The aircraft-layer half of the research validation owner (<see cref="MavFlightPhysicsOwner.F15AfitResearch"/>).
    ///
    /// The Core ownership authority checks the id, the governed body, the mandatory source
    /// environment and that nothing else is armed. This adds the checks only the F-15 layer can make:
    /// the research runtime authority grants the body, and the WP-4A preparation report has no
    /// blocker and a SOURCE-EQUIVALENT environment - fixed source density AND source gravity. If
    /// either is missing the body is not simulated: there is no fallback to the standard atmosphere,
    /// Unity's project gravity, or another F-15 profile.
    /// </summary>
    public sealed class MavF15ResearchOwnershipGrant : IMavResearchOwnershipGrant
    {
        public static readonly MavF15ResearchOwnershipGrant Instance = new MavF15ResearchOwnershipGrant();

        public string ResearchConfigurationId
        {
            get { return MavF15AfitResearchIdentity.ConfigurationId; }
        }

        public bool TryGrantResearchOwnership(MavSixDoFBody body, out string reason)
        {
            if (!MavF15ResearchRuntimeAuthority.TryGrant(body, out reason))
                return false;

            MavF15ResearchRuntimePreparationReport preparation = MavF15AfitResearchRuntimePreparation.Evaluate(body);
            if (!preparation.prepared)
            {
                reason = "research preparation has blockers: " + preparation.blockers;
                return false;
            }

            if (!preparation.sourceEquivalentEnvironment)
            {
                reason = "fixed source density AND source gravity are mandatory: " + preparation.equivalenceGaps;
                return false;
            }

            reason = "research validation ownership granted: " + MavF15AfitResearchIdentity.ConfigurationId
                + ", prepared, source-equivalent environment";
            return true;
        }
    }
}
