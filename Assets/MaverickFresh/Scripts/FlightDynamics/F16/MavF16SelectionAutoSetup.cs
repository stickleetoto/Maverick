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

        [Header("Auto-wired Phase 1 Control / Propulsion / Telemetry")]
        [Tooltip("C0 direct-surface test law. Not an F-16 FLCS; it exists so the physical path can be flown without legacy torque.")]
        public MavDirectSurfaceControlLaw controlLaw;

        [Tooltip("Zero-thrust placeholder. No authoritative F-16 propulsion data is frozen in this branch.")]
        public MavNullPropulsionModel propulsionModel;

        public MavFlightDynamicsTelemetry telemetry;

        [Header("Runtime Status")]
        public bool f16Selected;
        public bool referenceStackPrepared;
        public bool liveSixDoFEnabled;
        public bool propulsionDataAuthoritative;
        [TextArea] public string status;
        [TextArea] public string readinessReason = "not evaluated";

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
                referenceStackPrepared = false;
                readinessReason = "F-16 not selected";
                status = "F-16 not selected; reference six-DoF path inactive.";
                return;
            }

            EnsureReferenceStack();
            WireReferenceStack();

            if (configurator != null)
                configurator.ApplyReferenceValues(false);

            // Readiness is evaluated by the load-application boundary itself, so this binding
            // cannot disagree with the component that would actually take ownership.
            string bodyReadiness = "no MavSixDoFBody";
            bool bodyReady = sixDoFBody != null && sixDoFBody.IsReadyForLiveFdm(out bodyReadiness);
            readinessReason = bodyReadiness;

            referenceStackPrepared =
                physicsProfile != null
                && sixDoFBody != null
                && aeroModel != null
                && controlActuator != null
                && configurator != null
                && controlLaw != null
                && propulsionModel != null
                && sixDoFBody.debugProfileValid
                && bodyReady;

            propulsionDataAuthoritative =
                propulsionModel != null && propulsionModel.HasAuthoritativeData;

            // Live ownership remains intentionally OFF. The stack is complete enough to fly, but
            // no authoritative propulsion data is frozen and no player input is bound to the new
            // path, so the legacy stack keeps physical ownership until a human arms this manually.
            if (sixDoFBody != null)
                sixDoFBody.simulationEnabled = false;

            liveSixDoFEnabled = false;
            status = referenceStackPrepared
                ? "F-16 selected: physics profile + Morelli aero + C0 test law + zero-thrust propulsion + telemetry PREPARED. "
                  + "Live ownership held OFF by default (propulsion data not frozen; no player input bound to the new path)."
                : "F-16 selected, but the new flight-dynamics stack is not fully prepared: " + readinessReason;

            if (!loggedSelected)
            {
                Debug.Log(
                    "[Maverick/F16/FDM] F-16 selected | profile="
                    + (physicsProfile != null && physicsProfile.debugBuiltProfile != null
                        ? physicsProfile.debugBuiltProfile.profileId
                        : "missing")
                    + " | stack=" + (referenceStackPrepared ? "PREPARED" : "NOT_READY")
                    + " | readiness=" + readinessReason
                    + " | controlLaw=" + (controlLaw != null ? controlLaw.ControlLawName : "missing")
                    + " | propulsion=" + (propulsionModel != null ? propulsionModel.PropulsionModelName : "missing")
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

            controlLaw = GetComponent<MavDirectSurfaceControlLaw>();
            if (controlLaw == null)
                controlLaw = gameObject.AddComponent<MavDirectSurfaceControlLaw>();

            // Zero-thrust on purpose: no authoritative F-16 propulsion data is frozen in this
            // branch, so the propulsion owner exists and contributes literally nothing.
            propulsionModel = GetComponent<MavNullPropulsionModel>();
            if (propulsionModel == null)
                propulsionModel = gameObject.AddComponent<MavNullPropulsionModel>();

            telemetry = GetComponent<MavFlightDynamicsTelemetry>();
            if (telemetry == null)
                telemetry = gameObject.AddComponent<MavFlightDynamicsTelemetry>();

            preparedByThisBinding = true;
        }

        private void WireReferenceStack()
        {
            if (sixDoFBody != null)
            {
                sixDoFBody.profileProvider = physicsProfile;
                sixDoFBody.aerodynamicModel = aeroModel;
                sixDoFBody.propulsionModel = propulsionModel;
                sixDoFBody.controlLaw = controlLaw;
                sixDoFBody.controlSurfaceActuator = controlActuator;
                sixDoFBody.telemetry = telemetry;
                sixDoFBody.applyMassPropertiesOnEnable = true;
                sixDoFBody.autoApplyProfileConfiguration = true;
                sixDoFBody.ApplyConfiguredProfile(false);
            }

            if (controlActuator != null)
                controlActuator.sixDoFBody = sixDoFBody;

            if (controlLaw != null)
            {
                // The law owns the surface command from here on. Pilot intent stays neutral because
                // Phase 1 deliberately binds no player input to the new path.
                controlLaw.sixDoFBody = sixDoFBody;
                controlLaw.actuator = controlActuator;
                controlLaw.driveActuatorInFixedUpdate = true;
            }

            if (telemetry != null)
            {
                // Telemetry must never spam the console or touch disk unless a developer asks.
                telemetry.logToConsole = false;
                telemetry.captureCsv = false;
            }

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
