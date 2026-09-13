using System;
using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>
    /// Aircraft-independent physical profile consumed by the new flight-dynamics core.
    ///
    /// This profile intentionally contains only physics-domain data: geometry, mass/inertia,
    /// published model validity, and physical control-surface limits. It does not contain
    /// mouse sensitivity, arcade torque, weapons, sensors, camera settings, or combat tuning.
    /// </summary>
    [Serializable]
    public sealed class MavFlightDynamicsProfile
    {
        [Header("Identity")]
        public string profileId = "unconfigured";
        public string displayName = "Unconfigured Flight Dynamics Profile";
        [TextArea(2, 5)] public string description;

        [Header("Reference Geometry")]
        public MavAeroReferenceGeometry referenceGeometry;

        [Header("Mass / CG / Inertia")]
        public MavMassProperties massProperties = new MavMassProperties();

        [Header("Published / Intended Model Envelope")]
        public MavFlightDynamicsEnvelope envelope;

        [Header("Physical Control Surface Limits")]
        public MavControlSurfaceLimits controlSurfaceLimits;

        [Header("Propulsion Installation")]
        [Tooltip("Which propulsion installation this aircraft is equipped with. IDENTITY ONLY: the runtime engine states live in MavPropulsionSystem, never in a profile, because two engines sharing a profile must not share a spool state.")]
        public string propulsionInstallationId = "unspecified";

        [Tooltip("How many engines that installation has. Declared here so a profile can be checked against the propulsion system actually attached, catching an F-15 profile wired to a single-engine system.")]
        [Min(0)] public int declaredEngineCount = 0;

        [Header("Core Physics Ownership")]
        public bool useGravity = true;
        public bool zeroUnityLinearDamping = true;
        public bool zeroUnityAngularDamping = true;

        public bool IsValid(out string reason)
        {
            if (string.IsNullOrWhiteSpace(profileId))
            {
                reason = "profileId is empty";
                return false;
            }

            if (referenceGeometry.wingAreaM2 <= 0f
                || referenceGeometry.wingSpanM <= 0f
                || referenceGeometry.meanAerodynamicChordM <= 0f)
            {
                reason = "reference geometry is invalid";
                return false;
            }

            if (massProperties == null || massProperties.massKg <= 0f)
            {
                reason = "mass properties are invalid";
                return false;
            }

            reason = "OK";
            return true;
        }

        /// <summary>
        /// Whether an attached propulsion installation matches what this profile says the aircraft has.
        ///
        /// Deliberately NOT folded into <see cref="IsValid"/>. Existing profiles predate propulsion
        /// identity and leave it unspecified; making that invalid would break aircraft that are
        /// otherwise fine. This is the check a caller runs when it has both objects in hand, and it
        /// exists to catch the specific mistake of a twin-engine profile pointed at a single-engine
        /// system - a mismatch that would otherwise show up as an aircraft mysteriously short of
        /// thrust.
        /// </summary>
        public bool MatchesPropulsionInstallation(
            MavPropulsionInstallationProfile actual, out string reason)
        {
            if (propulsionInstallationId == "unspecified" && declaredEngineCount == 0)
            {
                reason = "profile declares no propulsion installation; nothing to check";
                return true;
            }

            if (actual == null)
            {
                reason = "profile declares installation '" + propulsionInstallationId
                         + "' but no installation is attached";
                return false;
            }

            if (propulsionInstallationId != "unspecified"
                && actual.installationId != propulsionInstallationId)
            {
                reason = "installation id mismatch: profile declares '" + propulsionInstallationId
                         + "', attached is '" + actual.installationId + "'";
                return false;
            }

            if (actual.EngineCount != declaredEngineCount)
            {
                reason = "engine count mismatch: profile declares " + declaredEngineCount
                         + ", attached installation has " + actual.EngineCount;
                return false;
            }

            reason = "OK";
            return true;
        }
    }

    [Serializable]
    public struct MavFlightDynamicsEnvelope
    {
        public float alphaMinDeg;
        public float alphaMaxDeg;
        public float betaMinDeg;
        public float betaMaxDeg;
        public float minMach;
        public float maxMach;

        public bool Contains(MavFlightState state)
        {
            return state.AlphaDeg >= alphaMinDeg
                && state.AlphaDeg <= alphaMaxDeg
                && state.BetaDeg >= betaMinDeg
                && state.BetaDeg <= betaMaxDeg
                && state.mach >= minMach
                && state.mach <= maxMach;
        }
    }

    [Serializable]
    public struct MavControlSurfaceLimits
    {
        public float elevatorMinDeg;
        public float elevatorMaxDeg;
        public float aileronMinDeg;
        public float aileronMaxDeg;
        public float rudderMinDeg;
        public float rudderMaxDeg;
        public float leadingEdgeFlapMinDeg;
        public float leadingEdgeFlapMaxDeg;

        public MavControlInput Clamp(MavControlInput input)
        {
            MavControlInput output = input;
            output.throttle01 = Mathf.Clamp01(input.throttle01);
            output.elevatorDeg = Mathf.Clamp(input.elevatorDeg, elevatorMinDeg, elevatorMaxDeg);
            output.aileronDeg = Mathf.Clamp(input.aileronDeg, aileronMinDeg, aileronMaxDeg);
            output.rudderDeg = Mathf.Clamp(input.rudderDeg, rudderMinDeg, rudderMaxDeg);

            // A profile may deliberately leave LEF at 0..0 when the selected aerodynamic
            // reference does not model it. This keeps unsupported behavior explicit.
            output.leadingEdgeFlapDeg = Mathf.Clamp(
                input.leadingEdgeFlapDeg,
                leadingEdgeFlapMinDeg,
                leadingEdgeFlapMaxDeg
            );
            return output;
        }
    }

    /// <summary>
    /// Runtime provider contract. Aircraft implementations create a fresh physical profile
    /// rather than mutating the legacy game-facing MavAircraftRuntimeProfile.
    /// </summary>
    public abstract class MavFlightDynamicsProfileProvider : MonoBehaviour
    {
        public abstract MavFlightDynamicsProfile BuildProfile();
    }
}
