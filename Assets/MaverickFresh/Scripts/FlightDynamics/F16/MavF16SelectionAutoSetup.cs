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

        [Header("Control Law Selection")]
        [Tooltip("When true the Maverick F-16 control law v0.1 (rate/G augmentation) owns the surfaces. When false the C0 direct-surface test law does. Exactly one law is ever left enabled: two laws driving one actuator would make the surface state depend on component order.")]
        public bool useControlLawV01 = true;

        [Header("Auto-wired Control / Propulsion / Telemetry")]
        [Tooltip("C0 direct-surface test law. Not an F-16 FLCS; it exists so the physical path can be flown without legacy torque.")]
        public MavDirectSurfaceControlLaw directSurfaceControlLaw;

        [Tooltip("Maverick F-16 control law v0.1. Maverick tuning throughout; explicitly NOT the real F-16 FLCS.")]
        public MavF16ControlLawV01 controlLawV01;

        [Tooltip("The law actually driving the actuator this frame.")]
        public MavFlightControlLawBase controlLaw;

        [Tooltip("Manual/test pilot-command source. Not an operational input path and not the War-Thunder instructor; it reports itself non-operational by default.")]
        public MavManualPilotCommandSource pilotCommandSource;

        [Tooltip("NASA Garza/Morelli throttle gearing + engine power-state dynamics. Dimensional thrust remains zero until the sourced altitude/Mach thrust deck is frozen.")]
        public MavF16EnginePowerModel enginePowerModel;

        public MavPropulsionModelBase propulsionModel;
        public MavFlightDynamicsTelemetry telemetry;

        [Header("Runtime Status")]
        public bool f16Selected;

        [Tooltip("STRUCTURALLY_PREPARED: every part present and wired. This is not permission to fly.")]
        public bool referenceStackPrepared;

        [Tooltip("OPERATIONALLY_LIVE_READY: the stack would be correct to hand the aircraft to. Expected FALSE on this branch.")]
        public bool operationallyLiveReady;

        public MavFlightDynamicsReadinessLevel readinessLevel;
        public bool liveSixDoFEnabled;
        public bool propulsionPowerDynamicsAuthoritative;
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

            MavFlightDynamicsReadinessReport readiness = sixDoFBody != null
                ? sixDoFBody.EvaluateReadinessReport()
                : new MavFlightDynamicsReadinessReport
                {
                    level = MavFlightDynamicsReadinessLevel.NotPrepared,
                    structuralReason = "no MavSixDoFBody",
                    operationalReason = "no MavSixDoFBody",
                    summary = "NOT_PREPARED: no MavSixDoFBody"
                };

            readinessLevel = readiness.level;
            readinessReason = readiness.summary;
            operationallyLiveReady = readiness.operationallyLiveReady;

            referenceStackPrepared =
                physicsProfile != null
                && sixDoFBody != null
                && aeroModel != null
                && controlActuator != null
                && configurator != null
                && controlLaw != null
                && propulsionModel != null
                && sixDoFBody.debugProfileValid
                && readiness.structurallyPrepared;

            propulsionPowerDynamicsAuthoritative =
                enginePowerModel != null && enginePowerModel.HasAuthoritativePowerDynamics;
            propulsionDataAuthoritative =
                propulsionModel != null && propulsionModel.HasAuthoritativeData;

            // Safety hold remains mandatory: the throttle/power dynamics are now sourced, but the
            // dimensional thrust deck and player command bridge are still incomplete.
            if (sixDoFBody != null)
                sixDoFBody.simulationEnabled = false;

            liveSixDoFEnabled = false;
            status = referenceStackPrepared
                ? "F-16 selected: " + readinessLevel + ". Morelli aero + sourced engine power "
                  + "dynamics + " + (controlLaw != null ? controlLaw.ControlLawName : "no control law")
                  + ". Live ownership remains OFF. " + readiness.operationalReason
                : "F-16 selected, but the new flight-dynamics stack is not structurally prepared: "
                  + readiness.structuralReason;

            if (!loggedSelected)
            {
                Debug.Log(
                    "[Maverick/F16/FDM] F-16 selected | profile="
                    + (physicsProfile != null && physicsProfile.debugBuiltProfile != null
                        ? physicsProfile.debugBuiltProfile.profileId
                        : "missing")
                    + " | stack=" + (referenceStackPrepared ? "STRUCTURALLY_PREPARED" : "NOT_PREPARED")
                    + " | readiness=" + readinessLevel
                    + " | detail=" + readinessReason
                    + " | controlLaw=" + (controlLaw != null ? controlLaw.ControlLawName : "missing")
                    + " | propulsion=" + (propulsionModel != null ? propulsionModel.PropulsionModelName : "missing")
                    + " | powerDynamicsSourced=" + propulsionPowerDynamicsAuthoritative
                    + " | thrustDeckSourced=" + propulsionDataAuthoritative
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

            // Exactly one control law may be enabled. Both derive from MavFlightControlLawBase and
            // both run at execution order -300, so leaving two enabled would let whichever ran last
            // overwrite the actuator command - a surface state that depends on component order.
            if (useControlLawV01)
            {
                controlLawV01 = GetComponent<MavF16ControlLawV01>();
                if (controlLawV01 == null)
                    controlLawV01 = gameObject.AddComponent<MavF16ControlLawV01>();
                controlLawV01.enabled = true;

                directSurfaceControlLaw = GetComponent<MavDirectSurfaceControlLaw>();
                if (directSurfaceControlLaw != null)
                    directSurfaceControlLaw.enabled = false;

                controlLaw = controlLawV01;
            }
            else
            {
                directSurfaceControlLaw = GetComponent<MavDirectSurfaceControlLaw>();
                if (directSurfaceControlLaw == null)
                    directSurfaceControlLaw = gameObject.AddComponent<MavDirectSurfaceControlLaw>();
                directSurfaceControlLaw.enabled = true;

                controlLawV01 = GetComponent<MavF16ControlLawV01>();
                if (controlLawV01 != null)
                    controlLawV01.enabled = false;

                controlLaw = directSurfaceControlLaw;
            }

            pilotCommandSource = GetComponent<MavManualPilotCommandSource>();
            if (pilotCommandSource == null)
                pilotCommandSource = gameObject.AddComponent<MavManualPilotCommandSource>();

            enginePowerModel = GetComponent<MavF16EnginePowerModel>();
            if (enginePowerModel == null)
                enginePowerModel = gameObject.AddComponent<MavF16EnginePowerModel>();
            propulsionModel = enginePowerModel;

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
                sixDoFBody.pilotCommandSource = pilotCommandSource;
                sixDoFBody.telemetry = telemetry;

                // Both safety gates stay at their defaults on this branch, restated here so the
                // auto-setup cannot silently drift into arming the aircraft:
                //   - a propulsion model with no frozen thrust deck is NOT accepted for live flight
                //   - loads may only be applied at OPERATIONALLY_LIVE_READY
                sixDoFBody.acceptNonAuthoritativePropulsion = false;
                sixDoFBody.requireOperationalReadinessForLoadApplication = true;
                sixDoFBody.allowStructuralOnlyLoadApplication = false;
                sixDoFBody.applyMassPropertiesOnEnable = true;
                sixDoFBody.autoApplyProfileConfiguration = true;
                sixDoFBody.ApplyConfiguredProfile(false);
            }

            if (controlActuator != null)
                controlActuator.sixDoFBody = sixDoFBody;

            if (controlLaw != null)
            {
                controlLaw.sixDoFBody = sixDoFBody;
                controlLaw.actuator = controlActuator;
                controlLaw.commandSource = pilotCommandSource;
                controlLaw.driveActuatorInFixedUpdate = true;
            }

            // The disabled law is still wired, so switching useControlLawV01 does not leave a
            // half-connected component behind, but it must not drive the actuator.
            MavFlightControlLawBase idleLaw = ReferenceEquals(controlLaw, controlLawV01)
                ? (MavFlightControlLawBase)directSurfaceControlLaw
                : controlLawV01;
            if (idleLaw != null)
            {
                idleLaw.sixDoFBody = sixDoFBody;
                idleLaw.actuator = controlActuator;
                idleLaw.commandSource = pilotCommandSource;
                idleLaw.driveActuatorInFixedUpdate = false;
            }

            if (pilotCommandSource != null)
            {
                // Explicitly NOT an operational command source: this is a bench input, and
                // operational live-readiness must not be satisfiable by adding a component.
                pilotCommandSource.treatAsOperationalSource = false;
            }

            if (telemetry != null)
            {
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
