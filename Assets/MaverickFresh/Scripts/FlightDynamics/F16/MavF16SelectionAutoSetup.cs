using UnityEngine;

namespace MaverickFresh.FlightDynamics.F16
{
    /// <summary>
    /// Runtime-only bootstrap for the isolated F-16 reference flight-dynamics path.
    ///
    /// When the existing Maverick aircraft selector/profile applier switches a player to
    /// F-16C, this bootstrap automatically installs and wires the reference-dynamics
    /// components on the same GameObject. It never edits scenes or prefabs.
    ///
    /// Live six-DoF force application intentionally remains disabled until the dedicated
    /// F-16 propulsion and control-command bridge exist. Auto-enabling the incomplete path
    /// today would either remove thrust/control or double-apply forces with the legacy stack.
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
    /// Per-aircraft runtime binding installed by MavF16SelectionAutoSetup.
    /// It follows MavAircraftProfileApplier.aircraft and prepares the F-16 reference stack
    /// whenever F16C is selected.
    /// </summary>
    [DefaultExecutionOrder(-9000)]
    [DisallowMultipleComponent]
    public sealed class MavF16SelectionBinding : MonoBehaviour
    {
        [Header("Selection Source")]
        public MavAircraftProfileApplier profileApplier;

        [Header("Auto-wired F-16 Reference Stack")]
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
                // If this binding prepared the isolated stack, keep its force owner off
                // whenever another aircraft is selected. Never alter the legacy stack.
                if (preparedByThisBinding && sixDoFBody != null)
                    sixDoFBody.simulationEnabled = false;

                liveSixDoFEnabled = sixDoFBody != null && sixDoFBody.simulationEnabled;
                status = "F-16 not selected; reference six-DoF path inactive.";
                return;
            }

            EnsureReferenceStack();
            WireReferenceStack();

            if (configurator != null)
                configurator.ApplyReferenceValues(false);

            referenceStackPrepared =
                sixDoFBody != null
                && aeroModel != null
                && controlActuator != null
                && configurator != null;

            // IMPORTANT: do not enable the new physical force owner yet. The Morelli aero
            // core is validated in isolation, but live propulsion and player/FBW command
            // ownership are not implemented. Enabling it alongside legacy flight would
            // recreate the exact double-force/double-torque failure we are avoiding.
            if (sixDoFBody != null)
                sixDoFBody.simulationEnabled = false;

            liveSixDoFEnabled = false;
            status = referenceStackPrepared
                ? "F-16 selected: reference dynamics auto-configured. Live six-DoF held OFF until propulsion + control bridge are ready."
                : "F-16 selected, but reference stack could not be fully prepared.";
        }

        private void EnsureReferenceStack()
        {
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
                sixDoFBody.aerodynamicModel = aeroModel;
                sixDoFBody.applyMassPropertiesOnEnable = true;
            }

            if (controlActuator != null)
                controlActuator.sixDoFBody = sixDoFBody;

            if (configurator != null)
            {
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
