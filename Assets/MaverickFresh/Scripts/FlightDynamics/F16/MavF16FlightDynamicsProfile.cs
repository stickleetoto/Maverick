using UnityEngine;

namespace MaverickFresh.FlightDynamics.F16
{
    /// <summary>
    /// Physical F-16 reference profile for the new Maverick flight-dynamics engine.
    ///
    /// Aerodynamic geometry / validity / control-deflection limits come from the Morelli
    /// compact nonlinear F-16 reference already frozen in MavF16MorelliReference.
    /// Mass / inertia come from MavF16MassReference.
    ///
    /// This component is deliberately separate from MavAircraftRuntimeProfile. The latter
    /// still owns legacy game tuning, while this profile owns only real flight-dynamics data.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MavF16FlightDynamicsProfile : MavFlightDynamicsProfileProvider
    {
        [Header("Unity Asset Mapping")]
        [Tooltip("Unity-local CG position. Keep zero until the visual/model origin to aerodynamic datum mapping is measured.")]
        public Vector3 centerOfMassLocalM = Vector3.zero;

        [Header("Debug")]
        public MavFlightDynamicsProfile debugBuiltProfile;

        public override MavFlightDynamicsProfile BuildProfile()
        {
            MavFlightDynamicsProfile profile = new MavFlightDynamicsProfile();
            profile.profileId = "f16-morelli-clean-subsonic-v0.1";
            profile.displayName = "F-16 Morelli Clean Subsonic v0.1";
            profile.description =
                "Physics-only F-16 reference profile. Morelli compact nonlinear aero, " +
                "clean configuration, gear up, no stores, subsonic reference envelope.";

            profile.referenceGeometry = MavF16MorelliReference.CreateReferenceGeometry();
            profile.massProperties = MavF16MassReference.CreateUnityMassProperties(centerOfMassLocalM);

            profile.envelope = new MavFlightDynamicsEnvelope
            {
                alphaMinDeg = MavF16MorelliReference.AlphaMinDeg,
                alphaMaxDeg = MavF16MorelliReference.AlphaMaxDeg,
                betaMinDeg = MavF16MorelliReference.BetaMinDeg,
                betaMaxDeg = MavF16MorelliReference.BetaMaxDeg,
                minMach = 0f,
                maxMach = MavF16MorelliReference.MaxReferenceMach
            };

            profile.controlSurfaceLimits = new MavControlSurfaceLimits
            {
                elevatorMinDeg = MavF16MorelliReference.ElevatorMinDeg,
                elevatorMaxDeg = MavF16MorelliReference.ElevatorMaxDeg,
                aileronMinDeg = MavF16MorelliReference.AileronMinDeg,
                aileronMaxDeg = MavF16MorelliReference.AileronMaxDeg,
                rudderMinDeg = MavF16MorelliReference.RudderMinDeg,
                rudderMaxDeg = MavF16MorelliReference.RudderMaxDeg,

                // The compact Morelli polynomial used in this branch does not consume LEF.
                // Keep it physically disabled instead of inventing an unsupported effect.
                leadingEdgeFlapMinDeg = 0f,
                leadingEdgeFlapMaxDeg = 0f
            };

            profile.useGravity = true;
            profile.zeroUnityLinearDamping = true;
            profile.zeroUnityAngularDamping = true;

            debugBuiltProfile = profile;
            return profile;
        }

        [ContextMenu("Rebuild F-16 Physics Profile")]
        private void RebuildFromContextMenu()
        {
            BuildProfile();
        }
    }
}
