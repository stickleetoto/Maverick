namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// Identity of the PILOT-CONTROLLED F-15 research aircraft (V1): the frozen AFIT / Baumann /
    /// Davison research model flown by a human through a Maverick-owned control approximation.
    ///
    /// It is a separate configuration from the frozen research configuration
    /// <see cref="MavF15AfitResearchIdentity.ConfigurationId"/>, which stays a validation-only
    /// identity and is never used for gameplay control. The two share the research model's physics
    /// (aerodynamic fit, geometry, mass, inertia, fixed thrust, source density and gravity) and
    /// nothing else: the pilot-controlled identity adds a control layer ABOVE that physics and changes
    /// none of it.
    ///
    /// WHAT THIS AIRCRAFT IS NOT, and never claims to be:
    ///   - not NASA F-15B 836 (<see cref="MavF15ReferenceData.TargetConfigurationId"/>);
    ///   - not the production F-15 flight-control system (no F-15 CAS, FLCS or NASA 836 control law);
    ///   - not source-authoritative actuator behaviour: surface gearing, command envelopes and slew
    ///     rates are MAVERICK_TUNED_NON_AUTHORITATIVE values (<see cref="MavF15GameplayControlAuthority"/>).
    /// </summary>
    public static class MavF15PilotControlledIdentity
    {
        /// <summary>Configuration / profile id. Carries no exact-target token by construction.</summary>
        public const string ConfigurationId = "F15_AFIT_BAUMANN_DAVISON_PILOT_CONTROLLED_V1";

        public const string DisplayName =
            "F-15 pilot-controlled research aircraft V1 (AFIT/Baumann/Davison aero, Maverick control "
            + "approximation) - NOT NASA 836, NOT the production F-15 FCS";

        /// <summary>What the physics is: the frozen research model, unchanged.</summary>
        public const string UnderlyingAerodynamics =
            "AFIT/Baumann/Davison research model (" + MavF15AfitResearchIdentity.ConfigurationId
            + " physics, frozen research baseline V1): Davison App. C coefficient fits, ARO10 reference "
            + "geometry, Davison mass/inertia, fixed 8,300 lbf total thrust, source fixed density and "
            + "source gravity";

        /// <summary>What the control layer is: a Maverick approximation, owned by this project.</summary>
        public const string PilotControlLayer =
            "Maverick-owned pilot-control approximation: direct stick-to-surface gearing about a "
            + "validated trim, with MAVERICK_TUNED_NON_AUTHORITATIVE gains, command envelopes and slew rates";

        public const string NotClaimed =
            "NOT NASA F-15B 836; NOT the production F-15 FCS (no F-15 CAS / FLCS / NASA 836 control law); "
            + "NOT source-authoritative actuator travel, hard stops or rates; NOT a sourced thrust deck "
            + "(throttle inactive)";

        /// <summary>The frozen research configuration this aircraft's physics is taken from.</summary>
        public const string PhysicsConfigurationId = MavF15AfitResearchIdentity.ConfigurationId;
    }
}
