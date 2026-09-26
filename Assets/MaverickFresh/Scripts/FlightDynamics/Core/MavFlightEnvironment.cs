using System;
using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>
    /// Where the air density a <see cref="MavSixDoFBody"/> computes dynamic pressure from comes from.
    /// </summary>
    public enum MavDensitySource
    {
        /// <summary>
        /// <see cref="MavAtmosphereModel"/> at the body's altitude. The default for every aircraft,
        /// and the only source any profile provider gets without explicitly asking for another.
        /// </summary>
        StandardAtmosphere = 0,

        /// <summary>
        /// A research configuration's own fixed source density, supplied by its profile provider.
        /// Research only: the provider must grant it, and a refusal is never turned into the
        /// standard atmosphere behind the caller's back.
        /// </summary>
        ResearchSourceFixedDensity = 1
    }

    /// <summary>
    /// Who applies gravity to the Rigidbody. Exactly one of the two, never both and never neither.
    /// </summary>
    public enum MavGravitySource
    {
        /// <summary>
        /// Unity applies the project gravity through <c>Rigidbody.useGravity</c>, as the profile asks.
        /// The default for every aircraft. Gravity is then not part of the load set.
        /// </summary>
        UnityProjectGravity = 0,

        /// <summary>
        /// <see cref="MavSixDoFBody"/> applies a research configuration's own gravitational
        /// acceleration as the load set's separate gravitational channel, once, with
        /// <c>Rigidbody.useGravity</c> off. <c>Physics.gravity</c> is never changed. Research only.
        /// </summary>
        ResearchSourceGravityThroughLoadSet = 1
    }

    /// <summary>
    /// The environment one physics step of a <see cref="MavSixDoFBody"/> runs in: the atmosphere
    /// sample it builds the flight state from, and who applies gravity.
    ///
    /// Resolved every step by the body's profile provider
    /// (<see cref="MavFlightDynamicsProfileProvider.ResolveEnvironment"/>). The base provider
    /// returns <see cref="Standard"/> - the shared standard atmosphere, unchanged, and Unity's
    /// project gravity - so every aircraft that does not explicitly override it behaves exactly as
    /// before this type existed.
    ///
    /// <see cref="valid"/> false means the provider REFUSED an override it was asked for. The body
    /// then computes and applies nothing that step: an override that cannot be honoured must not
    /// quietly become the standard atmosphere.
    /// </summary>
    [Serializable]
    public struct MavFlightEnvironment
    {
        public const string StandardStatus =
            "standard atmosphere at the body's altitude; Unity project gravity (Rigidbody.useGravity)";

        [Tooltip("False when the profile provider refused the environment it was configured for. The body then applies no loads - never a silent fallback.")]
        public bool valid;

        [Tooltip("The sample the flight state (dynamic pressure, Mach) is built from this step.")]
        public MavAtmosphereSample atmosphere;

        public MavDensitySource densitySource;
        public MavGravitySource gravitySource;

        [Tooltip("Gravitational acceleration the body applies through the load set, m/s^2. Zero unless gravitySource is ResearchSourceGravityThroughLoadSet.")]
        public float loadSetGravityMps2;

        [Tooltip("The body's geometric altitude (world Y), whatever the atmosphere sample is labelled with.")]
        public float geometricAltitudeM;

        [Tooltip("The configuration that asked for a non-standard environment, when one did.")]
        public string configurationId;

        public string status;

        public bool OverridesDensity
        {
            get { return densitySource != MavDensitySource.StandardAtmosphere; }
        }

        public bool OwnsGravityThroughLoadSet
        {
            get { return gravitySource == MavGravitySource.ResearchSourceGravityThroughLoadSet; }
        }

        /// <summary>The default: the standard sample exactly as given, and Unity's project gravity.</summary>
        public static MavFlightEnvironment Standard(MavAtmosphereSample standardAtmosphere, float geometricAltitudeM)
        {
            return new MavFlightEnvironment
            {
                valid = true,
                atmosphere = standardAtmosphere,
                densitySource = MavDensitySource.StandardAtmosphere,
                gravitySource = MavGravitySource.UnityProjectGravity,
                loadSetGravityMps2 = 0f,
                geometricAltitudeM = geometricAltitudeM,
                configurationId = null,
                status = StandardStatus
            };
        }

        /// <summary>
        /// A refused override. The standard sample is carried only so the body's diagnostic state
        /// stays finite; <see cref="valid"/> is false and the body applies nothing.
        /// </summary>
        public static MavFlightEnvironment Refused(
            MavAtmosphereSample standardAtmosphere, float geometricAltitudeM, string configurationId, string reason)
        {
            return new MavFlightEnvironment
            {
                valid = false,
                atmosphere = standardAtmosphere,
                densitySource = MavDensitySource.StandardAtmosphere,
                gravitySource = MavGravitySource.UnityProjectGravity,
                loadSetGravityMps2 = 0f,
                geometricAltitudeM = geometricAltitudeM,
                configurationId = configurationId,
                status = "REFUSED: " + reason
            };
        }
    }
}
