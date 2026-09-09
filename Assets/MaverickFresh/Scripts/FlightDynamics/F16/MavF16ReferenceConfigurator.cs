using UnityEngine;

namespace MaverickFresh.FlightDynamics.F16
{
    /// <summary>
    /// Safe helper that applies the published F-16 reference geometry and mass/inertia
    /// values to the isolated flight-dynamics components without touching legacy Maverick
    /// flight scripts, scenes, or prefabs.
    ///
    /// The Unity-local center of mass remains explicit because prefab/model origin placement
    /// is an asset convention. Default zero means "use the current GameObject origin as CG".
    /// </summary>
    [DisallowMultipleComponent]
    public class MavF16ReferenceConfigurator : MonoBehaviour
    {
        [Header("Targets")]
        public MavSixDoFBody sixDoFBody;
        public MavF16AeroModel aeroModel;

        [Header("Unity Asset Mapping")]
        public Vector3 centerOfMassLocalM = Vector3.zero;

        [Header("Safety")]
        public bool applyReferenceValuesOnAwake = false;
        public bool applyMassToRigidbodyImmediately = false;

        [Header("Debug")]
        public bool debugReferenceApplied;

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

        [ContextMenu("Apply F-16 Reference Values")]
        public void ApplyReferenceValuesFromContextMenu()
        {
            ApplyReferenceValues(applyMassToRigidbodyImmediately);
        }

        public void ApplyReferenceValues(bool applyMassNow)
        {
            Resolve();
            debugReferenceApplied = false;

            if (aeroModel != null)
            {
                aeroModel.referenceGeometry = MavF16MorelliReference.CreateReferenceGeometry();
                aeroModel.xCgCbar = MavF16MorelliReference.DefaultXcgCbar;
                aeroModel.xCgReferenceCbar = MavF16MorelliReference.XcgReferenceCbar;
            }

            if (sixDoFBody != null)
            {
                sixDoFBody.massProperties = MavF16MassReference.CreateUnityMassProperties(centerOfMassLocalM);
                if (applyMassNow)
                    sixDoFBody.ApplyConfiguredMassProperties();
            }

            debugReferenceApplied = aeroModel != null || sixDoFBody != null;
        }

        private void Resolve()
        {
            if (sixDoFBody == null)
                sixDoFBody = GetComponent<MavSixDoFBody>();
            if (aeroModel == null)
                aeroModel = GetComponent<MavF16AeroModel>();
        }
    }
}
