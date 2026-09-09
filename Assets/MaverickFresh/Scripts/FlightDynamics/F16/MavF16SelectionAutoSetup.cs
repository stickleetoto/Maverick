using UnityEngine;

namespace MaverickFresh.FlightDynamics.F16
{
    /// <summary>
    /// Runtime-only bootstrap for the isolated F-16 reference flight-dynamics path.
    /// It prepares the new physics profile + six-DoF stack when F-16C is selected,
    /// without editing scenes/prefabs or enabling incomplete live ownership.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class MavF16SelectionAutoSetup : MonoBehaviour
    {
        private const string BootstrapObjectName = "Mav_F16SelectionAutoSetup";
        private const float ScanIntervalSeconds = 0.25f;

        private float nextScanTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallBootstrap()
        {
            MavF16SelectionAutoSetup existing = FindObjectOfType<MavF16SelectionAutoSetup>();
            if (existing != null)
                return;

            GameObject go = new GameObject(BootstrapObjectName);
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.DontSave;
            go.AddComponent<MavF16SelectionAutoSetup>();
        }

        private void OnEnable()
        {
            ScanAndReconcile();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextScanTime)
                return;

            nextScanTime = Time.unscaledTime + ScanIntervalSeconds;
            ScanAndReconcile();
        }

        private static void ScanAndReconcile()
        {
            MavAircraftProfileApplier[] appliers = FindObjectsOfType<MavAircraftProfileApplier>(true);
            for (int i = 0; i < appliers.Length; i++)
            {
                MavAircraftProfileApplier applier = appliers[i];
                if (applier == null)
                    continue;

                MavF16SelectionBinding binding = applier.GetComponent<MavF16SelectionBinding>();
                if (binding == null)
                    binding = applier.gameObject.AddComponent<MavF16SelectionBinding>();

                binding.profileApplier = applier;
                binding.ReconcileNow();
            }
        }
    }

    /// <summary>
    /// Per-aircraft runtime binding. The legacy aircraft selector remains the identity source,
    /// but F-16 physical data now lives in MavF16FlightDynamicsProfile instead of the legacy
    /// MavAircraftRuntimeProfile tuning blob.
    /// </summary>
    [DefaultExecutionOrder(-9000)]
    [DisallowMultipleComponent]
    public sealed class MavF16SelectionBinding : MonoBehaviour
    {
        [Header("Selection Source")]
        public MavAircraftProfileApplier profileApplier;

        [Header("Auto-wired F-16 Physics Stack")]
        public MavF16FlightDynamicsProfile physicsProfile;
        public MavSixDoFBody sixDoFBody;
        public MavF16AeroModel aeroModel;
        public MavF16ControlActuator controlActuator;
        public MavF16ReferenceConfigurator configurator;

        [Header("Runtime Status")]
        public bool f16Selected;
        public bool referenceStackPrepared;
        public bool liveSixDoFEnabled;
        [TextArea] public string status;

        private MavAircraftKind lastObservedAircraft = (MavAircraftKind)(-1);
        private bool preparedByThisBinding;
        private bool loggedSelected;

        private void Awake()
        {
            ResolveProfileApplier();
            ReconcileNow();
        }

        private void Update()
        {
            ResolveProfileApplier();
            if (profileApplier == null)
                return;

            if (profileApplier.aircraft != lastObservedAircraft)
                ReconcileNow();
        }

        public void ReconcileNow()
        {
            ResolveProfileApplier();
            if (profileApplier == null)
            {
                status = "No MavAircraftProfileApplier found.";
                return;
            }

            lastObservedAircraft = profileApplier.aircraft;
            f16Selected = profileApplier.aircraft == MavAircraftKind.F16C;

            if (!f16Selected)
            {
                if (preparedByThisBinding && sixDoFBody != null)
                    sixDoFBody.simulationEnabled = false;

                if (loggedSelected)
                {
                    Debug.Log("[Maverick/F16/FDM] F-16 deselected; new flight-dynamics ownership OFF.", this);
                    loggedSelected = false;
                }

                liveSixDoFEnabled = sixDoFBody != null && sixDoFBody.simulationEnabled;
                status = "F-16 not selected; reference six-DoF path inactive.";
                return;
            }

            EnsureReferenceStack();
            WireReferenceStack();

            if (configurator != null)
                configurator.ApplyReferenceValues(false);

            referenceStackPrepared =
                physicsProfile != null
                && sixDoFBody != null
                && aeroModel != null
                && controlActuator != null
                && configurator != null
                && sixDoFBody.debugProfileValid;

            // Live ownership remains intentionally OFF until propulsion + player/FBW command
            // ownership are implemented. This avoids double-applying physics with the legacy stack.
            if (sixDoFBody != null)
                sixDoFBody.simulationEnabled = false;

            liveSixDoFEnabled = false;
            status = referenceStackPrepared
                ? "F-16 selected: dedicated physics profile + Morelli six-DoF stack READY. Live ownership held OFF pending propulsion/control bridge."
                : "F-16 selected, but the physics profile/six-DoF stack is not fully ready.";

            if (!loggedSelected)
            {
                Debug.Log(
                    "[Maverick/F16/FDM] F-16 selected | profile="
                    + (physicsProfile != null && physicsProfile.debugBuiltProfile != null
                        ? physicsProfile.debugBuiltProfile.profileId
                        : "missing")
                    + " | stack=" + (referenceStackPrepared ? "READY" : "NOT_READY")
                    + " | liveSixDoF=OFF (safety hold)",
                    this
                );
                loggedSelected = true;
            }
        }

        private void EnsureReferenceStack()
        {
            physicsProfile = GetComponent<MavF16FlightDynamicsProfile>();
            if (physicsProfile == null)
                physicsProfile = gameObject.AddComponent<MavF16FlightDynamicsProfile>();

            sixDoFBody = GetComponent<MavSixDoFBody>();
            if (sixDoFBody == null)
                sixDoFBody = gameObject.AddComponent<MavSixDoFBody>();

            aeroModel = GetComponent<MavF16AeroModel>();
            if (aeroModel == null)
                aeroModel = gameObject.AddComponent<MavF16AeroModel>();

            controlActuator = GetComponent<MavF16ControlActuator>();
            if (controlActuator == null)
                controlActuator = gameObject.AddComponent<MavF16ControlActuator>();

            configurator = GetComponent<MavF16ReferenceConfigurator>();
            if (configurator == null)
                configurator = gameObject.AddComponent<MavF16ReferenceConfigurator>();

            preparedByThisBinding = true;
        }

        private void WireReferenceStack()
        {
            if (sixDoFBody != null)
            {
                sixDoFBody.profileProvider = physicsProfile;
                sixDoFBody.aerodynamicModel = aeroModel;
                sixDoFBody.applyMassPropertiesOnEnable = true;
                sixDoFBody.autoApplyProfileConfiguration = true;
                sixDoFBody.ApplyConfiguredProfile(false);
            }

            if (controlActuator != null)
                controlActuator.sixDoFBody = sixDoFBody;

            if (configurator != null)
            {
                configurator.flightDynamicsProfile = physicsProfile;
                configurator.sixDoFBody = sixDoFBody;
                configurator.aeroModel = aeroModel;
                configurator.applyReferenceValuesOnAwake = false;
                configurator.applyMassToRigidbodyImmediately = false;
            }
        }

        private void ResolveProfileApplier()
        {
            if (profileApplier == null)
                profileApplier = GetComponent<MavAircraftProfileApplier>();
        }
    }
}
