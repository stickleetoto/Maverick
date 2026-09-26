using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// Which density the research body's dynamic pressure uses. Opt-in; the default is the shared
    /// standard atmosphere, exactly as every other aircraft.
    /// </summary>
    public enum MavF15ResearchDensityPolicy
    {
        /// <summary>
        /// <see cref="MavAtmosphereModel"/> at the body's altitude. Does NOT reproduce the source:
        /// its q is 6.83e-4 low even at 6,096 m and follows altitude, which the source has no state
        /// for.
        /// </summary>
        StandardAtmosphere = 0,

        /// <summary>
        /// The source's RHO = 0.0012673 slug/ft^3 at every state, as the source program uses it
        /// (QBARS = .5*RHO*VTRFPS^2*SREF, no altitude state).
        /// </summary>
        SourceFixedDensity = 1
    }

    /// <summary>
    /// Which gravitational acceleration the research body feels. Opt-in; the default is Unity's
    /// project gravity, exactly as every other aircraft.
    /// </summary>
    public enum MavF15ResearchGravityPolicy
    {
        /// <summary>Rigidbody.useGravity with the project gravity (9.81 m/s^2 in this project).</summary>
        UnityProjectGravity = 0,

        /// <summary>
        /// The source's G = 32.174 ft/s^2 (9.8066352 m/s^2), applied once by
        /// <see cref="MavSixDoFBody"/> as the load set's gravitational channel, with
        /// Rigidbody.useGravity off. Physics.gravity is never touched.
        /// </summary>
        SourceGravity = 1
    }

    /// <summary>
    /// WP-4A (D11). The research-only environment the F-15 research profile can supply to its
    /// six-DoF body, in place of the shared standard atmosphere and Unity's project gravity.
    ///
    /// WHAT IT CHANGES, and only for a body that flies the research profile and has opted in:
    ///   - density: the <see cref="MavAtmosphereSample"/> the body builds its flight state from
    ///     carries the source's fixed RHO. The sample is the source's fixed air: it is labelled with
    ///     the 20,000-ft altitude the source gives that density ("AIR DENSITY AT 20000 FT
    ///     ALTITUDE"), and its temperature, pressure and speed of sound are the shared standard
    ///     atmosphere's values there, REPORT-ONLY - the source computes no Mach, and the research
    ///     condition gate uses Mach only to tell the coefficient-fit condition from the rest. The
    ///     body's geometric altitude is not an input to the research model (the source has no
    ///     altitude state) and is reported separately.
    ///   - gravity: the source's G, through the load set.
    ///
    /// WHAT IT NEVER DOES: change <see cref="MavAtmosphereModel"/>, change Physics.gravity, reach
    /// any non-research body, or default on. When it is asked for an override it may not grant, it
    /// returns a REFUSED environment, and the body then applies nothing - it never falls back to
    /// the standard atmosphere.
    /// </summary>
    public static class MavF15AfitResearchRuntimeEnvironment
    {
        public const double FootToMeters = 0.3048;

        /// <summary>
        /// 1 lbf in N, exactly (0.45359237 kg x 9.80665 m/s^2), as a DOUBLE: the shared float
        /// constant would carry its own 1.6e-8 rounding into the density.
        /// </summary>
        public const double PoundForceToNewtonExact = 4.4482216152605;

        /// <summary>1 slug/ft^3 in kg/m^3 (515.378818): 1 slug = 1 lbf s^2/ft, so lbf-to-N over ft^4.</summary>
        public const double SlugPerFt3ToKgPerM3 =
            PoundForceToNewtonExact / (FootToMeters * FootToMeters * FootToMeters * FootToMeters);

        /// <summary>The source's RHO in SI, 0.0012673 x 515.378818 = 0.65313958 kg/m^3.</summary>
        public const float SourceDensityKgM3 =
            (float)(MavF15SourceExercisedOperatingDomain.SourceAirDensitySlugPerFt3 * SlugPerFt3ToKgPerM3);

        /// <summary>The source's G in SI, 9.8066352 m/s^2 (32.174 ft/s^2 exactly converted).</summary>
        public const float SourceGravityMps2 =
            (float)(MavF15SourceExercisedOperatingDomain.SourceGravityFtPerSec2 * FootToMeters);

        /// <summary>The altitude the source's density belongs to, by the source's own label.</summary>
        public const float SourceDensityAltitudeLabelM = MavF15CoefficientFitCondition.PressureAltitudeM;

        public const string DefaultStatus =
            "research profile, default policies: standard atmosphere + Unity project gravity - the "
            + "source density and gravity are NOT reproduced (q 6.83e-4 low at 6,096 m; g +3.43e-4)";

        private const string SourceDensityLabel =
            "source fixed density RHO 0.0012673 slug/ft^3 (0.6531396 kg/m^3) at every state; the sample is "
            + "labelled with the source's 20,000-ft density altitude; T, p and a are standard-atmosphere "
            + "values there, report-only";

        private const string StandardDensityLabel =
            "standard atmosphere at the body's altitude (source density NOT reproduced)";

        private const string SourceGravityLabel =
            "source G 32.174 ft/s^2 (9.8066352 m/s^2) through the load set, Rigidbody.useGravity off";

        private const string UnityGravityLabel =
            "Unity project gravity via Rigidbody.useGravity (source G NOT reproduced)";

        /// <summary>The source's fixed air, as one standard-shaped sample. Constant, so built once.</summary>
        public static MavAtmosphereSample SourceAir()
        {
            return SourceAirSample;
        }

        private static readonly MavAtmosphereSample SourceAirSample = BuildSourceAir();

        private static MavAtmosphereSample BuildSourceAir()
        {
            MavAtmosphereSample air = MavAtmosphereModel.Sample(SourceDensityAltitudeLabelM);
            air.densityKgM3 = SourceDensityKgM3;
            return air;
        }

        public static string DensityLabel(MavDensitySource source)
        {
            return source == MavDensitySource.ResearchSourceFixedDensity ? SourceDensityLabel : StandardDensityLabel;
        }

        public static string GravityLabel(MavGravitySource source)
        {
            return source == MavGravitySource.ResearchSourceGravityThroughLoadSet ? SourceGravityLabel : UnityGravityLabel;
        }

        /// <summary>
        /// The environment <paramref name="profile"/> asks for on <paramref name="body"/>.
        ///
        /// Both policies at their defaults: the standard environment, untouched - there is nothing
        /// to grant. Anything else requires that <paramref name="profile"/> is the body's own
        /// provider and that <see cref="MavF15ResearchRuntimeAuthority"/> grants the body; if not,
        /// the result is REFUSED.
        /// </summary>
        public static MavFlightEnvironment Resolve(
            MavF15AfitResearchFlightDynamicsProfile profile,
            MavSixDoFBody body,
            MavF15ResearchDensityPolicy densityPolicy,
            MavF15ResearchGravityPolicy gravityPolicy,
            MavAtmosphereSample standardAtmosphere,
            float geometricAltitudeM)
        {
            if (densityPolicy == MavF15ResearchDensityPolicy.StandardAtmosphere
                && gravityPolicy == MavF15ResearchGravityPolicy.UnityProjectGravity)
            {
                MavFlightEnvironment standard = MavFlightEnvironment.Standard(standardAtmosphere, geometricAltitudeM);
                standard.status = DefaultStatus;
                return standard;
            }

            if (densityPolicy != MavF15ResearchDensityPolicy.StandardAtmosphere
                && densityPolicy != MavF15ResearchDensityPolicy.SourceFixedDensity)
            {
                return MavFlightEnvironment.Refused(standardAtmosphere, geometricAltitudeM, null,
                    "unknown research density policy " + (int)densityPolicy);
            }

            if (gravityPolicy != MavF15ResearchGravityPolicy.UnityProjectGravity
                && gravityPolicy != MavF15ResearchGravityPolicy.SourceGravity)
            {
                return MavFlightEnvironment.Refused(standardAtmosphere, geometricAltitudeM, null,
                    "unknown research gravity policy " + (int)gravityPolicy);
            }

            if (profile == null || body == null || !ReferenceEquals(body.profileProvider, profile))
            {
                return MavFlightEnvironment.Refused(standardAtmosphere, geometricAltitudeM, null,
                    "research environment requested by a profile that is not this body's provider");
            }

            string reason;
            if (!MavF15ResearchRuntimeAuthority.TryGrant(body, out reason))
            {
                return MavFlightEnvironment.Refused(standardAtmosphere, geometricAltitudeM,
                    body.activeProfile != null ? body.activeProfile.profileId : null, reason);
            }

            MavFlightEnvironment environment = new MavFlightEnvironment
            {
                valid = true,
                geometricAltitudeM = geometricAltitudeM,
                configurationId = MavF15AfitResearchIdentity.ConfigurationId
            };

            if (densityPolicy == MavF15ResearchDensityPolicy.SourceFixedDensity)
            {
                environment.atmosphere = SourceAir();
                environment.densitySource = MavDensitySource.ResearchSourceFixedDensity;
            }
            else
            {
                environment.atmosphere = standardAtmosphere;
                environment.densitySource = MavDensitySource.StandardAtmosphere;
            }

            if (gravityPolicy == MavF15ResearchGravityPolicy.SourceGravity)
            {
                environment.gravitySource = MavGravitySource.ResearchSourceGravityThroughLoadSet;
                environment.loadSetGravityMps2 = SourceGravityMps2;
            }
            else
            {
                environment.gravitySource = MavGravitySource.UnityProjectGravity;
                environment.loadSetGravityMps2 = 0f;
            }

            environment.status = Status(environment.densitySource, environment.gravitySource);
            return environment;
        }

        // Built once: Resolve runs every physics step and should not allocate a status string each time.
        private static readonly string[] StatusByPolicy =
        {
            Compose(MavDensitySource.StandardAtmosphere, MavGravitySource.UnityProjectGravity),
            Compose(MavDensitySource.ResearchSourceFixedDensity, MavGravitySource.UnityProjectGravity),
            Compose(MavDensitySource.StandardAtmosphere, MavGravitySource.ResearchSourceGravityThroughLoadSet),
            Compose(MavDensitySource.ResearchSourceFixedDensity, MavGravitySource.ResearchSourceGravityThroughLoadSet)
        };

        private static string Status(MavDensitySource density, MavGravitySource gravity)
        {
            return StatusByPolicy[(density == MavDensitySource.ResearchSourceFixedDensity ? 1 : 0)
                + (gravity == MavGravitySource.ResearchSourceGravityThroughLoadSet ? 2 : 0)];
        }

        private static string Compose(MavDensitySource density, MavGravitySource gravity)
        {
            return "RESEARCH ENVIRONMENT (" + MavF15AfitResearchIdentity.ConfigurationId + "): density = "
                + DensityLabel(density) + "; gravity = " + GravityLabel(gravity);
        }
    }
}
