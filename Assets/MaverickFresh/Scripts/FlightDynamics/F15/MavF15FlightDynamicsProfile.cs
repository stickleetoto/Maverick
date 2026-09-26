using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// F15-R1 source-honest physical profile skeleton for the exact NASA 836 target.
    ///
    /// This deliberately reuses the aircraft-independent profile contract created for the F-16.
    /// It does NOT reuse F-16 aerodynamic coefficients, control-law gains, engine dynamics, or
    /// geometry numbers.
    ///
    /// Current state is intentionally FAIL-CLOSED:
    ///   - exact-target mass/CG/inertia are populated;
    ///   - exact-target span and engine identity/count are populated;
    ///   - exact-target S/cbar, coefficient envelope and physical hard stops remain unavailable;
    ///   - therefore MavFlightDynamicsProfile.IsValid returns false on reference geometry.
    ///
    /// That is the desired R1 behaviour until the remaining NASA 836 reference-definition gaps close.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MavF15FlightDynamicsProfile : MavFlightDynamicsProfileProvider
    {
        [Header("Unity Asset Mapping")]
        [Tooltip("Unity-local CG position. Keep zero until the visual/model origin to the NASA 836 aerodynamic/CG datum is measured.")]
        public Vector3 centerOfMassLocalM = Vector3.zero;

        [Tooltip("True only after the Unity model origin -> physical CG/datum mapping has been measured for the selected asset.")]
        public bool cgMappingMeasuredAndDeclared;

        [TextArea(2, 4)]
        public string cgMappingProvenance = "NOT MEASURED";

        [Header("Debug")]
        public MavFlightDynamicsProfile debugBuiltProfile;
        public bool debugProfileComplete;
        public string debugProfileStatus = "not built";

        public override MavFlightDynamicsProfile BuildProfile()
        {
            MavFlightDynamicsProfile profile = new MavFlightDynamicsProfile();
            profile.profileId = "f15-nasa836-pre-quiet-spike-r1";
            profile.displayName = "NASA F-15B 836 Pre-Quiet-Spike R1";
            profile.description =
                "F15-R1 physics profile skeleton for "
                + MavF15ReferenceData.TargetConfigurationId
                + ". Exact mass/inertia and span are populated. Exact-target S/cbar, "
                + "aerodynamic validity envelope and control hard stops remain unavailable, "
                + "so the profile intentionally fails closed.";

            profile.referenceGeometry = MavF15ReferenceData.CreateExactTargetGeometry();
            profile.massProperties =
                MavF15MassReference.CreateUnityMassProperties(centerOfMassLocalM);
            profile.envelope =
                MavF15ReferenceData.CreateUnavailableAerodynamicEnvelope();
            profile.controlSurfaceLimits =
                MavF15ReferenceData.CreateUnavailableControlLimits();

            profile.propulsionInstallationId = MavF15PropulsionSkeleton.InstallationId;
            profile.declaredEngineCount = MavF15ReferenceData.EngineCount;

            profile.useGravity = true;
            profile.zeroUnityLinearDamping = true;
            profile.zeroUnityAngularDamping = true;

            string reason;
            bool coreValid = profile.IsValid(out reason);
            debugProfileComplete = coreValid && cgMappingMeasuredAndDeclared;
            if (!coreValid)
                debugProfileStatus = "INCOMPLETE: " + reason;
            else if (!cgMappingMeasuredAndDeclared)
                debugProfileStatus = "INCOMPLETE: Unity CG/datum mapping not measured";
            else
                debugProfileStatus = "COMPLETE";

            debugBuiltProfile = profile;
            return profile;
        }

        [ContextMenu("Rebuild F-15 R1 Physics Profile")]
        private void RebuildFromContextMenu()
        {
            BuildProfile();
        }
    }
}
