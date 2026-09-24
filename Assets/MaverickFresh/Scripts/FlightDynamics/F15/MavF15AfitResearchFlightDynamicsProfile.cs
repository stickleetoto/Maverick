using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// Physical profile for the F-15 RESEARCH configuration
    /// <see cref="MavF15AfitResearchIdentity.ConfigurationId"/> - the AFIT / Baumann / Davison model
    /// at Mach 0.6 / 20,000 ft.
    ///
    /// THIS IS NOT NASA F-15B 836, and it does not fill any gap in the exact path. Every number it
    /// carries belongs to the research model:
    ///   - reference geometry: the ARO10 set 608 ft^2 / 42.8 ft / 15.94 ft
    ///     (<see cref="MavF15BaumannMach06Reference"/>);
    ///   - mass and inertia: Davison's driver (<see cref="MavF15AfitResearchMassReference"/>);
    ///   - envelope: the one source condition, and the transcribed alpha/beta span
    ///     (<see cref="MavF15BaumannMach06Domain"/>).
    ///
    /// The exact provider <see cref="MavF15FlightDynamicsProfile"/> is untouched and stays invalid.
    /// The two never share a profile id, a mass state or a reference geometry.
    ///
    /// Control-surface travel is UNAVAILABLE here too. Public F-15 sources give four conflicting
    /// travel sets (see Docs/Reference/F15_DN1180_SOURCE_LINEAGE_V0.1.md), and picking one would be
    /// invention, so the research aircraft is held at zero travel: structurally flyable, surfaces
    /// neutral, uncontrolled. That limitation is reported, not bypassed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MavF15AfitResearchFlightDynamicsProfile : MavFlightDynamicsProfileProvider
    {
        [Header("Unity Asset Mapping")]
        [Tooltip("SIMULATION REFERENCE CHOICE, not source data: the research model's CG coincides with its own moment reference, so the Rigidbody center of mass sits at the aircraft's local origin. See MavF15AfitResearchMassReference.")]
        public Vector3 centerOfMassLocalM = Vector3.zero;

        [Header("Research Condition")]
        [Tooltip("StrictFitCondition (default): Mach 0.6 / 20,000 ft only - the coefficient-fit condition. SourceReproduction: the true airspeeds Baumann's own model ran (218.5-699.7 ft/s) at its fixed 20,000-ft density, with every coefficient extrapolated from the Mach 0.6 fit and reported as such. Research only; never NASA 836. See Docs/Reference/F15_BAUMANN_SOURCE_CONDITION_AUDIT_V1.0.md.")]
        public MavF15ResearchConditionMode conditionMode = MavF15ResearchConditionMode.StrictFitCondition;

        [Header("Debug")]
        public MavFlightDynamicsProfile debugBuiltProfile;
        public string debugProfileStatus = "not built";
        public string debugSourceCondition = MavF15AfitResearchIdentity.SourceConditionLabel;

        public override MavFlightDynamicsProfile BuildProfile()
        {
            MavFlightDynamicsProfile profile = new MavFlightDynamicsProfile();
            profile.profileId = MavF15AfitResearchIdentity.ConfigurationId;
            profile.displayName = MavF15AfitResearchIdentity.DisplayName;
            profile.description =
                "RESEARCH profile at " + MavF15AfitResearchIdentity.SourceConditionLabel
                + " only. Lineage: " + MavF15AfitResearchIdentity.SourceLineage
                + " Reference geometry 608 ft^2 / 42.8 ft / 15.94 ft (ARO10 driver constants); "
                + "mass/inertia from Davison's driver; fixed total thrust 8,300 lbf from the same "
                + "driver. Control-surface travel unavailable (held at zero). Not NASA F-15B 836 "
                + "and not the exact-target aircraft.";

            profile.referenceGeometry = MavF15BaumannMach06Reference.CreateReferenceGeometry();
            profile.massProperties =
                MavF15AfitResearchMassReference.CreateUnityMassProperties(centerOfMassLocalM);
            profile.envelope = CreateResearchEnvelope(conditionMode);
            profile.controlSurfaceLimits = CreateUnavailableResearchControlLimits();

            // The research source models one total-aircraft thrust force, not an engine
            // installation, so no installation identity or engine count is declared.
            profile.propulsionInstallationId = "unspecified";
            profile.declaredEngineCount = 0;

            profile.useGravity = true;
            profile.zeroUnityLinearDamping = true;
            profile.zeroUnityAngularDamping = true;

            string reason;
            bool valid = profile.IsValid(out reason);
            debugProfileStatus = (valid ? "VALID (research only): " : "INVALID: ") + reason
                + " @ " + MavF15AfitResearchIdentity.SourceConditionLabel
                + (conditionMode == MavF15ResearchConditionMode.SourceReproduction
                    ? " | SOURCE REPRODUCTION: source-exercised speeds admitted; coefficients are the "
                      + "Mach 0.6 fit, extrapolated away from it, NOT aerodynamically validated"
                    : " | strict coefficient-fit condition");

            debugBuiltProfile = profile;
            return profile;
        }

        /// <summary>
        /// The research model's own envelope: Mach 0.6 within the source-condition equality
        /// tolerance, and the alpha/beta span the transcribed routine declares. Altitude is not a
        /// field of the generic envelope; it is enforced by the aerodynamic model and the research
        /// thrust, both of which refuse away from 20,000 ft.
        ///
        /// NOT BROADENED, and not narrowed either. Davison's driver also prints continuation bounds
        /// of -8..50 deg alpha and +/-30 deg beta (PDF p.92). Those are recorded in
        /// Docs/Reference/F15_RESEARCH_PROFILE_V1.0.md but not applied: the span below is the one the
        /// research model already declares.
        /// </summary>
        public static MavFlightDynamicsEnvelope CreateResearchEnvelope()
        {
            return CreateResearchEnvelope(MavF15ResearchConditionMode.StrictFitCondition);
        }

        /// <summary>
        /// The envelope for a condition mode. SourceReproduction widens ONLY the Mach bounds, to the
        /// source-exercised true airspeeds at the standard-atmosphere speed of sound of the fixed
        /// 20,000-ft altitude; the alpha/beta span is unchanged. The widened bounds describe what the
        /// source ran, not where its coefficients are valid.
        /// </summary>
        public static MavFlightDynamicsEnvelope CreateResearchEnvelope(MavF15ResearchConditionMode mode)
        {
            if (mode == MavF15ResearchConditionMode.SourceReproduction)
            {
                float a = MavAtmosphereModel.Sample(MavF15CoefficientFitCondition.PressureAltitudeM).speedOfSoundMps;
                MavFlightDynamicsEnvelope wide = CreateStrictResearchEnvelope();
                wide.minMach = MavF15SourceExercisedOperatingDomain.MinTabulatedTrueAirspeedMps / a;
                wide.maxMach = MavF15SourceExercisedOperatingDomain.MaxTabulatedTrueAirspeedMps / a;
                return wide;
            }

            return CreateStrictResearchEnvelope();
        }

        private static MavFlightDynamicsEnvelope CreateStrictResearchEnvelope()
        {
            return new MavFlightDynamicsEnvelope
            {
                alphaMinDeg = MavF15BaumannMach06Domain.SourceAlphaMinDeg,
                alphaMaxDeg = MavF15BaumannMach06Domain.SourceAlphaMaxDeg,
                betaMinDeg = -MavF15BaumannMach06Domain.SourceAbsBetaMaxDeg,
                betaMaxDeg = MavF15BaumannMach06Domain.SourceAbsBetaMaxDeg,
                minMach = MavF15BaumannMach06Reference.SourceMach
                    - MavF15BaumannMach06Reference.NumericalMachTolerance,
                maxMach = MavF15BaumannMach06Reference.SourceMach
                    + MavF15BaumannMach06Reference.NumericalMachTolerance
            };
        }

        /// <summary>Zero travel: no research-source control-surface authority is accepted.</summary>
        public static MavControlSurfaceLimits CreateUnavailableResearchControlLimits()
        {
            return new MavControlSurfaceLimits();
        }

        [ContextMenu("Rebuild F-15 AFIT Research Profile")]
        private void RebuildFromContextMenu()
        {
            BuildProfile();
        }
    }
}
