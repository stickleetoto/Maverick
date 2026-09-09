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
