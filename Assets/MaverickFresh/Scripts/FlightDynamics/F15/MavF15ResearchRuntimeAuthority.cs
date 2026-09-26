using System;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// WP-4A. The one gate every research RUNTIME element consults before it acts on a body: the
    /// research source environment (fixed density, source gravity), the static surface hold, and
    /// state injection.
    ///
    /// It grants exactly one identity, <see cref="MavF15AfitResearchIdentity.ConfigurationId"/>,
    /// compared ordinally - no trimming, no case folding, no prefix match. Everything else is
    /// refused with a reason: the exact NASA F-15B 836 target by name, any id carrying an
    /// exact-target token, the exact 836 profile, the F-16, look-alikes, empty and null.
    ///
    /// On a body it additionally requires that the body really flies the research profile: its
    /// provider is the research profile component, on the same GameObject, and the profile the
    /// body built from it is valid and carries the research id. A research component dropped onto
    /// any other aircraft therefore fails closed.
    ///
    /// Granting authority is not arming. Nothing here sets simulationEnabled, and nothing here
    /// makes the research configuration live-ready.
    /// </summary>
    public static class MavF15ResearchRuntimeAuthority
    {
        public const string RequiredConfigurationId = MavF15AfitResearchIdentity.ConfigurationId;

        /// <summary>A constant, so a grant consulted every physics step allocates nothing.</summary>
        public const string GrantedReason = "research runtime authority granted: " + RequiredConfigurationId;

        public static bool IsResearchConfigurationId(string configurationId)
        {
            return string.Equals(configurationId, RequiredConfigurationId, StringComparison.Ordinal);
        }

        /// <summary>The identity check alone.</summary>
        public static bool TryGrant(string configurationId, out string reason)
        {
            if (configurationId == null)
            {
                reason = "research runtime refused: no configuration id";
                return false;
            }

            if (string.Equals(configurationId, MavF15ReferenceData.TargetConfigurationId, StringComparison.Ordinal))
            {
                reason = "research runtime refused: '" + configurationId
                    + "' is the exact NASA F-15B 836 target; no research override, hold or injection "
                    + "ever reaches it";
                return false;
            }

            if (!MavF15AfitResearchIdentity.CarriesNoExactTargetToken(configurationId))
            {
                reason = "research runtime refused: '" + configurationId + "' carries an exact-target token";
                return false;
            }

            if (!IsResearchConfigurationId(configurationId))
            {
                reason = "research runtime refused: '" + configurationId + "' is not "
                    + RequiredConfigurationId + " (exact, ordinal match required)";
                return false;
            }

            reason = GrantedReason;
            return true;
        }

        /// <summary>The identity check on the profile a body actually flies.</summary>
        public static bool TryGrant(MavSixDoFBody body, out string reason)
        {
            if (body == null)
            {
                reason = "research runtime refused: no six-DoF body";
                return false;
            }

            MavF15AfitResearchFlightDynamicsProfile research =
                body.profileProvider as MavF15AfitResearchFlightDynamicsProfile;
            if (research == null)
            {
                reason = "research runtime refused: the body's profile provider is "
                    + (body.profileProvider == null ? "missing" : body.profileProvider.GetType().Name)
                    + ", not the research profile";
                return false;
            }

            if (research.gameObject != body.gameObject)
            {
                reason = "research runtime refused: the research profile is not on the body's GameObject";
                return false;
            }

            if (body.activeProfile == null || !body.debugProfileValid)
            {
                reason = "research runtime refused: the body has not built a valid profile ("
                    + body.debugProfileStatus + ")";
                return false;
            }

            return TryGrant(body.activeProfile.profileId, out reason);
        }
    }
}
