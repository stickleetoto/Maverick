using UnityEngine;

namespace MaverickFresh.FlightDynamics.F16
{
    /// <summary>
    /// Safe F-16 reference configurator for the new profile-driven flight-dynamics engine.
    /// It never edits scenes/prefabs and never touches the legacy Maverick flight scripts.
    /// </summary>
    [DisallowMultipleComponent]
    public class MavF16ReferenceConfigurator : MonoBehaviour
    {
        [Header("Targets")]
        public MavSixDoFBody sixDoFBody;
        public MavF16AeroModel aeroModel;
        public MavF16FlightDynamicsProfile flightDynamicsProfile;

        [Header("Unity Asset Mapping")]
        public Vector3 centerOfMassLocalM = Vector3.zero;

        [Header("Safety")]
        public bool applyReferenceValuesOnAwake = false;
        public bool applyMassToRigidbodyImmediately = false;

        [Header("Debug")]
        public bool debugReferenceApplied;
        public string debugProfileId = "none";

        private void Reset()
        {
            Resolve();
            ApplyReferenceValues(false);
        }

        private void Awake()
        {
            Resolve();
            if (applyReferenceValuesOnAwake)
                ApplyReferenceValues(applyMassToRigidbodyImmediately);
        }

        [ContextMenu("Apply F-16 Physics Profile")]
        public void ApplyReferenceValuesFromContextMenu()
        {
            ApplyReferenceValues(applyMassToRigidbodyImmediately);
        }

        public void ApplyReferenceValues(bool applyMassNow)
        {
            Resolve();
            debugReferenceApplied = false;
            debugProfileId = "none";

            if (flightDynamicsProfile == null)
                return;

            flightDynamicsProfile.centerOfMassLocalM = centerOfMassLocalM;
            MavFlightDynamicsProfile profile = flightDynamicsProfile.BuildProfile();
            if (profile == null)
                return;

            if (aeroModel != null)
            {
                aeroModel.referenceGeometry = profile.referenceGeometry;
                aeroModel.xCgCbar = MavF16MorelliReference.DefaultXcgCbar;
                aeroModel.xCgReferenceCbar = MavF16MorelliReference.XcgReferenceCbar;
            }

            if (sixDoFBody != null)
            {
                sixDoFBody.profileProvider = flightDynamicsProfile;
                sixDoFBody.massProperties = profile.massProperties;
                sixDoFBody.aerodynamicModel = aeroModel;
                sixDoFBody.ApplyConfiguredProfile(applyMassNow);
            }

            debugProfileId = profile.profileId;
            debugReferenceApplied = aeroModel != null || sixDoFBody != null;
        }

        private void Resolve()
        {
            if (sixDoFBody == null)
                sixDoFBody = GetComponent<MavSixDoFBody>();
            if (aeroModel == null)
                aeroModel = GetComponent<MavF16AeroModel>();
            if (flightDynamicsProfile == null)
                flightDynamicsProfile = GetComponent<MavF16FlightDynamicsProfile>();
        }
    }
}
