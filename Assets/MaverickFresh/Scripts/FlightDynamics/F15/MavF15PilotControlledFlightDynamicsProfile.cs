using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// Physical profile for the PILOT-CONTROLLED F-15 research aircraft
    /// (<see cref="MavF15PilotControlledIdentity.ConfigurationId"/>).
    ///
    /// SOURCE PHYSICS, UNCHANGED. Every physical number is the frozen research model's, built by the
    /// same factories the research profile uses: ARO10 reference geometry
    /// (<see cref="MavF15BaumannMach06Reference"/>), Davison mass and inertia
    /// (<see cref="MavF15AfitResearchMassReference"/>), the research envelope in SourceReproduction
    /// mode, the source's fixed density and the source's gravity
    /// (<see cref="MavF15AfitResearchRuntimeEnvironment"/>). Nothing here is tuned.
    ///
    /// SEPARATE IDENTITY. The profile id is the pilot-controlled id, never the research id, so the
    /// frozen research configuration stays a validation-only identity. It is not NASA 836.
    ///
    /// ENVIRONMENT, MANDATORY. The research model has no altitude state; it is defined at the
    /// source's fixed 20,000-ft density. This aircraft therefore always flies the source fixed density
    /// and the source gravity (through the load set, Rigidbody.useGravity off). There is no
    /// standard-atmosphere option, and a refused grant makes the body apply nothing - never a
    /// fallback. The body's geometric altitude is reported, and is not an input to the model.
    ///
    /// CONDITION MODE. Always SourceReproduction (<see cref="MavF15PilotControlledAeroModel"/>): the
    /// source-exercised true airspeeds at the source's fixed density, every coefficient away from Mach
    /// 0.6 reported as extrapolated. The strict fit condition (Mach 0.6 +/- 0.001) cannot be flown.
    ///
    /// GAMEPLAY CONTROL. The Maverick-owned control approximation - gearing, envelopes, trim start -
    /// is carried here as configuration and labelled MAVERICK_TUNED_NON_AUTHORITATIVE; see
    /// <see cref="MavF15GameplayControlAuthority"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MavF15PilotControlledFlightDynamicsProfile : MavFlightDynamicsProfileProvider
    {
        public const string EnvironmentStatus =
            "PILOT-CONTROLLED RESEARCH ENVIRONMENT (" + MavF15PilotControlledIdentity.ConfigurationId
            + "): source fixed density RHO 0.0012673 slug/ft^3 (0.6531396 kg/m^3) at every state, as the "
            + "research model defines it (no altitude state); source G 32.174 ft/s^2 (9.8066352 m/s^2) "
            + "through the load set, Rigidbody.useGravity off";

        [Header("Unity Asset Mapping")]
        [Tooltip("Same simulation reference choice as the research profile: the research model's CG coincides with its moment reference, at the local origin.")]
        public Vector3 centerOfMassLocalM = Vector3.zero;

        [Header("Maverick Control Approximation (MAVERICK_TUNED_NON_AUTHORITATIVE)")]
        [Tooltip("Stick gearing and command envelopes. Maverick tuning, not F-15 data; never a hard stop or an actuator rate.")]
        public MavF15GameplayControlAuthority gameplayControlAuthority = MavF15GameplayControlAuthority.V1();

        [Tooltip("The validated research equilibrium the aircraft starts from; its stabilator is the pilot-neutral trim bias.")]
        public MavF15PilotTrimStart trimStart = MavF15PilotTrimStart.TableViiPoint36();

        [Header("Pilot Physics Revision")]
        [Tooltip("R2 (default): the current pilot physics - the research model's own first-order surface lags (Davison 1992 STATE12: stab 20, aileron 20, differential 20, rudder 28 1/s). R1: the historical pilot physics every V1/V2 record was made on - surfaces move to their bounded command instantly; kept for regression and comparison and selected explicitly. Nothing else differs. Read by the rig when it wires the actuator.")]
        public MavF15PilotPhysicsRevision physicsRevision = MavF15PilotPhysicsRevision.R2SourceActuatorLags;

        [Header("Debug")]
        public MavFlightDynamicsProfile debugBuiltProfile;
        public string debugProfileStatus = "not built";
        public string debugEnvironmentStatus = "not resolved";

        public override MavFlightEnvironment ResolveEnvironment(
            MavSixDoFBody body, MavAtmosphereSample standardAtmosphere, float geometricAltitudeM)
        {
            MavFlightEnvironment environment = Resolve(this, body, standardAtmosphere, geometricAltitudeM);
            debugEnvironmentStatus = environment.status;
            return environment;
        }

        /// <summary>
        /// The pilot-controlled aircraft's environment: the research source environment when the
        /// profile is the body's provider and <see cref="MavF15PilotControlledAuthority"/> grants the
        /// body; REFUSED otherwise.
        /// </summary>
        public static MavFlightEnvironment Resolve(
            MavF15PilotControlledFlightDynamicsProfile profile,
            MavSixDoFBody body,
            MavAtmosphereSample standardAtmosphere,
            float geometricAltitudeM)
        {
            if (profile == null || body == null || !ReferenceEquals(body.profileProvider, profile))
            {
                return MavFlightEnvironment.Refused(standardAtmosphere, geometricAltitudeM, null,
                    "pilot-controlled environment requested by a profile that is not this body's provider");
            }

            string reason;
            if (!MavF15PilotControlledAuthority.TryGrant(body, out reason))
            {
                return MavFlightEnvironment.Refused(standardAtmosphere, geometricAltitudeM,
                    body.activeProfile != null ? body.activeProfile.profileId : null, reason);
            }

            return new MavFlightEnvironment
            {
                valid = true,
                geometricAltitudeM = geometricAltitudeM,
                configurationId = MavF15PilotControlledIdentity.ConfigurationId,
                atmosphere = MavF15AfitResearchRuntimeEnvironment.SourceAir(),
                densitySource = MavDensitySource.ResearchSourceFixedDensity,
                gravitySource = MavGravitySource.ResearchSourceGravityThroughLoadSet,
                loadSetGravityMps2 = MavF15AfitResearchRuntimeEnvironment.SourceGravityMps2,
                status = EnvironmentStatus
            };
        }

        public override MavFlightDynamicsProfile BuildProfile()
        {
            MavFlightDynamicsProfile profile = new MavFlightDynamicsProfile();
            profile.profileId = MavF15PilotControlledIdentity.ConfigurationId;
            profile.displayName = MavF15PilotControlledIdentity.DisplayName;
            profile.description =
                "PILOT-CONTROLLED research aircraft V1. Physics: " + MavF15PilotControlledIdentity.UnderlyingAerodynamics
                + ". Control: " + MavF15PilotControlledIdentity.PilotControlLayer + ". "
                + MavF15PilotControlledIdentity.NotClaimed + ".";

            profile.referenceGeometry = MavF15BaumannMach06Reference.CreateReferenceGeometry();
            profile.massProperties = MavF15AfitResearchMassReference.CreateUnityMassProperties(centerOfMassLocalM);
            profile.envelope = MavF15AfitResearchFlightDynamicsProfile.CreateResearchEnvelope(
                MavF15ResearchConditionMode.SourceReproduction);
            profile.controlSurfaceLimits = SharedProjectionLimits(gameplayControlAuthority);

            // Same as the research profile: one total-aircraft thrust force, no engine installation.
            profile.propulsionInstallationId = "unspecified";
            profile.declaredEngineCount = 0;

            profile.useGravity = true;
            profile.zeroUnityLinearDamping = true;
            profile.zeroUnityAngularDamping = true;

            string reason;
            bool valid = profile.IsValid(out reason);
            debugProfileStatus = (valid ? "VALID (pilot-controlled research aircraft): " : "INVALID: ") + reason
                + " | SOURCE REPRODUCTION at the source's fixed density; coefficients away from Mach 0.6 are "
                + "the Mach 0.6 fit extrapolated, NOT aerodynamically validated";

            debugBuiltProfile = profile;
            return profile;
        }

        /// <summary>
        /// The shared-contract projection of the gameplay envelopes (symmetric stabilator, aileron,
        /// rudder). The F-15 aerodynamics read the actuator's four-channel state, not this; it keeps the
        /// shared view consistent with the gameplay layer. MAVERICK_TUNED_NON_AUTHORITATIVE.
        /// </summary>
        public static MavControlSurfaceLimits SharedProjectionLimits(MavF15GameplayControlAuthority authority)
        {
            return new MavControlSurfaceLimits
            {
                elevatorMinDeg = authority.symmetricStabilator.minDeg,
                elevatorMaxDeg = authority.symmetricStabilator.maxDeg,
                aileronMinDeg = authority.aileron.minDeg,
                aileronMaxDeg = authority.aileron.maxDeg,
                rudderMinDeg = authority.rudder.minDeg,
                rudderMaxDeg = authority.rudder.maxDeg,
                leadingEdgeFlapMinDeg = 0f,
                leadingEdgeFlapMaxDeg = 0f
            };
        }
    }
}
