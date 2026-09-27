using System;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// The one gate the pilot-controlled aircraft's runtime elements consult before acting on a body:
    /// its source environment, its fixed research thrust, its trim-start initialization and its
    /// ownership grant.
    ///
    /// Grants exactly <see cref="MavF15PilotControlledIdentity.ConfigurationId"/>, ordinally. Refused
    /// with a reason: the exact NASA F-15B 836 target by name, the frozen research VALIDATION
    /// configuration by name (it is never flown by the pilot-control layer), any exact-target token,
    /// look-alikes, empty and null. The frozen <see cref="MavF15ResearchRuntimeAuthority"/> is not
    /// touched and still grants only the research id; neither authority grants the other's id.
    ///
    /// On a body it also requires that the body really flies the pilot-controlled profile: its
    /// provider is that profile on the same GameObject, and the profile the body built is valid and
    /// carries the pilot-controlled id.
    ///
    /// Granting is not arming. Nothing here arms a body.
    /// </summary>
    public static class MavF15PilotControlledAuthority
    {
        public const string RequiredConfigurationId = MavF15PilotControlledIdentity.ConfigurationId;

        public const string GrantedReason = "pilot-controlled authority granted: " + RequiredConfigurationId;

        public static bool TryGrant(string configurationId, out string reason)
        {
            if (configurationId == null)
            {
                reason = "pilot-controlled runtime refused: no configuration id";
                return false;
            }

            if (string.Equals(configurationId, MavF15ReferenceData.TargetConfigurationId, StringComparison.Ordinal))
            {
                reason = "pilot-controlled runtime refused: '" + configurationId
                    + "' is the exact NASA F-15B 836 target, which stays fail-closed";
                return false;
            }

            if (string.Equals(configurationId, MavF15AfitResearchIdentity.ConfigurationId, StringComparison.Ordinal))
            {
                reason = "pilot-controlled runtime refused: '" + configurationId
                    + "' is the frozen research VALIDATION configuration; it is never flown by the pilot-control layer";
                return false;
            }

            if (!MavF15AfitResearchIdentity.CarriesNoExactTargetToken(configurationId))
            {
                reason = "pilot-controlled runtime refused: '" + configurationId + "' carries an exact-target token";
                return false;
            }

            if (!string.Equals(configurationId, RequiredConfigurationId, StringComparison.Ordinal))
            {
                reason = "pilot-controlled runtime refused: '" + configurationId + "' is not "
                    + RequiredConfigurationId + " (exact, ordinal match required)";
                return false;
            }

            reason = GrantedReason;
            return true;
        }

        public static bool TryGrant(MavSixDoFBody body, out string reason)
        {
            if (body == null)
            {
                reason = "pilot-controlled runtime refused: no six-DoF body";
                return false;
            }

            MavF15PilotControlledFlightDynamicsProfile pilot =
                body.profileProvider as MavF15PilotControlledFlightDynamicsProfile;
            if (pilot == null)
            {
                reason = "pilot-controlled runtime refused: the body's profile provider is "
                    + (body.profileProvider == null ? "missing" : body.profileProvider.GetType().Name)
                    + ", not the pilot-controlled profile";
                return false;
            }

            if (pilot.gameObject != body.gameObject)
            {
                reason = "pilot-controlled runtime refused: the pilot-controlled profile is not on the body's GameObject";
                return false;
            }

            if (body.activeProfile == null || !body.debugProfileValid)
            {
                reason = "pilot-controlled runtime refused: the body has no valid built profile";
                return false;
            }

            return TryGrant(body.activeProfile.profileId, out reason);
        }
    }

    /// <summary>
    /// The aircraft-layer half of the pilot-controlled owner
    /// (<see cref="MavFlightPhysicsOwner.F15PilotControlledResearch"/>). The Core authority checks the
    /// id, the governed body, the mandatory source environment and that nothing else is armed; this
    /// adds what only the F-15 layer can check: the rig is exactly the pilot-controlled stack, with the
    /// frozen research physics components and the Maverick control layer, and nothing research-only
    /// or exact-target mixed in.
    /// </summary>
    public sealed class MavF15PilotControlledOwnershipGrant : IMavResearchOwnershipGrant
    {
        public static readonly MavF15PilotControlledOwnershipGrant Instance = new MavF15PilotControlledOwnershipGrant();

        public string ResearchConfigurationId
        {
            get { return MavF15PilotControlledIdentity.ConfigurationId; }
        }

        public bool TryGrantResearchOwnership(MavSixDoFBody body, out string reason)
        {
            if (!MavF15PilotControlledAuthority.TryGrant(body, out reason))
                return false;

            MavF15PilotControlledAeroModel aero = body.aerodynamicModel as MavF15PilotControlledAeroModel;
            if (aero == null || !aero.acknowledgeResearchAerodynamics)
            {
                reason = "the aerodynamic model is not the pilot-controlled six-axis AFIT/Baumann/Davison research model with its explicit opt-in";
                return false;
            }

            if (body.GetComponent<MavF15AeroModel>() != null)
            {
                reason = "a second aerodynamic model (MavF15AeroModel) is on the aircraft; exactly one may exist";
                return false;
            }

            if (!(body.propulsionModel is MavF15PilotControlledFixedThrust))
            {
                reason = "the propulsion model is not the pilot-controlled fixed research thrust";
                return false;
            }

            MavF15ControlActuator actuator = body.controlSurfaceActuator as MavF15ControlActuator;
            if (actuator == null)
            {
                reason = "no F-15 control actuator owns the surfaces";
                return false;
            }

            for (int i = 0; i < 4; i++)
            {
                MavF15SurfaceChannelLimits c = actuator.limits.Get((MavF15SurfaceChannel)i);
                if (c.travelProvenance != MavEngineDataProvenance.MaverickTuning)
                {
                    reason = "actuator travel on " + (MavF15SurfaceChannel)i + " is graded " + c.travelProvenance
                        + "; the pilot-controlled aircraft may only carry MAVERICK_TUNED_NON_AUTHORITATIVE travel";
                    return false;
                }
            }

            if (!(body.controlLaw is MavF15PilotControlLaw))
            {
                reason = "the control law is not the Maverick pilot-control law";
                return false;
            }

            MavF15AfitResearchStaticSurfaceHold hold = body.GetComponent<MavF15AfitResearchStaticSurfaceHold>();
            if (hold != null && hold.enabled)
            {
                reason = "a research static surface hold is present; it would override the pilot's surfaces";
                return false;
            }

            if (body.GetComponent<MavF15AfitResearchFlightDynamicsProfile>() != null
                || body.GetComponent<MavF15AfitResearchFixedThrust>() != null)
            {
                reason = "a frozen research validation component is on the pilot-controlled aircraft";
                return false;
            }

            MavFlightEnvironment environment = body.ResolveEnvironment(body.transform.position.y);
            if (!environment.valid || environment.densitySource != MavDensitySource.ResearchSourceFixedDensity
                || !environment.OwnsGravityThroughLoadSet)
            {
                reason = "fixed source density AND source gravity are mandatory: " + environment.status;
                return false;
            }

            reason = "pilot-controlled ownership granted: " + MavF15PilotControlledIdentity.ConfigurationId
                + ", research physics + Maverick control approximation, source environment";
            return true;
        }
    }
}
