using UnityEngine;

namespace MaverickFresh.FlightDynamics.F16
{
    /// <summary>
    /// Runtime-only opt-in for the first powered replacement-FDM smoke test.
    ///
    /// DEFAULT OFF. When enabled it wires the sourced TP-1538 propulsion installation and the
    /// operational mouse-instructor command bridge, then asks the ownership authority for SHADOW
    /// mode only. Legacy still flies the aircraft; the replacement stack computes the same load set
    /// it would use live but is not allowed to write it to the Rigidbody.
    ///
    /// This component deliberately does NOT enable replacement activation. Moving from a healthy
    /// shadow run to F16Replacement is a separate, atomic handover step.
    /// </summary>
    [DefaultExecutionOrder(-8500)]
    [DisallowMultipleComponent]
    public sealed class MavF16PoweredReferenceShadowSmoke : MonoBehaviour
    {
        [Header("Explicit opt-in")]
        [Tooltip("OFF by default. Enable only in Play Mode for the powered replacement-FDM shadow smoke test.")]
        public bool enablePoweredReferenceShadowSmoke = false;

        [Tooltip("How many finite shadow physics steps are required before the smoke run is reported healthy.")]
        public int requiredFiniteShadowSteps = 25;

        [Header("Auto-wired")]
        public MavF16SelectionBinding binding;
        public global::MaverickFresh.MavInstructorController instructor;
        public MavMouseInstructorPilotCommandSource commandSource;
        public MavF16PropulsionSystem propulsionSystem;
        public MavFlightPhysicsOwnership ownership;

        [Header("Read-only smoke status")]
        public bool configured;
        public bool tp1538PropulsionAcceptable;
        public bool operationalCommandSourceAvailable;
        public bool readyAssumingLegacyOwnershipCleared;
        public bool shadowLoadSetFinite;
        public bool replacementWroteNoLiveLoads = true;
        public int shadowStepsThisRun;
        public bool shadowHealthy;
        [TextArea(3, 8)] public string status = "OFF - legacy flight unchanged";

        private int shadowStepBaseline;
        private int loadApplicationBaseline;
        private bool loggedActive;

        private void Awake()
        {
            ResolveBinding();
        }

        private void FixedUpdate()
        {
            ResolveBinding();

            if (!enablePoweredReferenceShadowSmoke)
            {
                if (configured)
                    RestoreSafeLegacyWiring("powered reference shadow smoke disabled");
                return;
            }

            ApplyPoweredShadowConfiguration();
            UpdateDiagnostics();
        }

        private void Update()
        {
            // Keep Inspector status useful even when physics is paused.
            if (enablePoweredReferenceShadowSmoke && configured)
                UpdateDiagnostics();
        }

        private void OnDisable()
        {
            if (configured)
                RestoreSafeLegacyWiring("powered reference shadow smoke component disabled");
        }

        private void ApplyPoweredShadowConfiguration()
        {
            if (binding == null || !binding.f16Selected)
            {
                status = "REFUSED - authoritative F-16C selection is not active";
                return;
            }

            MavSixDoFBody body = binding.sixDoFBody;
            if (body == null || binding.controlLaw == null || binding.controlActuator == null)
            {
                status = "REFUSED - replacement FDM stack is not structurally wired";
                return;
            }

            instructor = GetComponent<global::MaverickFresh.MavInstructorController>();
            if (instructor == null || !instructor.isActiveAndEnabled)
            {
                status = "REFUSED - no active MavInstructorController to bridge pilot intent from";
                return;
            }

            commandSource = GetComponent<MavMouseInstructorPilotCommandSource>();
            if (commandSource == null)
                commandSource = gameObject.AddComponent<MavMouseInstructorPilotCommandSource>();
            commandSource.instructor = instructor;

            propulsionSystem = GetComponent<MavF16PropulsionSystem>();
            if (propulsionSystem == null)
                propulsionSystem = gameObject.AddComponent<MavF16PropulsionSystem>();

            tp1538PropulsionAcceptable = propulsionSystem.IsAcceptableForLiveFlight;
            if (!tp1538PropulsionAcceptable)
            {
                if (!propulsionSystem.ConfigureTp1538SourcedInstallation())
                {
                    status = "REFUSED - TP-1538 sourced propulsion installation could not be configured";
                    return;
                }

                tp1538PropulsionAcceptable = propulsionSystem.IsAcceptableForLiveFlight;
            }

            if (!tp1538PropulsionAcceptable)
            {
                status = "REFUSED - TP-1538 propulsion is present but not acceptable for live flight";
                return;
            }

            ownership = GetComponent<MavFlightPhysicsOwnership>();
            if (ownership == null)
                ownership = gameObject.AddComponent<MavFlightPhysicsOwnership>();

            // This first smoke stage is SHADOW ONLY. Even a fully healthy run cannot accidentally
            // cross into live replacement ownership from this component.
            ownership.allowReplacementActivation = false;
            ownership.AttachGovernedBody(body);
            body.physicsOwnership = ownership;

            // Re-assert these references every physics step because the older selection binding
            // periodically reconciles its conservative bench wiring. This component executes before
            // the control law/body and therefore leaves one deterministic configuration for the step.
            body.propulsionModel = propulsionSystem;
            body.pilotCommandSource = commandSource;
            body.acceptNonAuthoritativePropulsion = false;
            body.requireOperationalReadinessForLoadApplication = true;
            body.allowStructuralOnlyLoadApplication = false;

            binding.propulsionModel = propulsionSystem;
            binding.controlLaw.commandSource = commandSource;
            binding.controlLaw.sixDoFBody = body;
            binding.controlLaw.actuator = binding.controlActuator;
            binding.controlLaw.driveActuatorInFixedUpdate = true;

            MavFlightControlLawBase idleLaw = ReferenceEquals(binding.controlLaw, binding.controlLawV01)
                ? (MavFlightControlLawBase)binding.directSurfaceControlLaw
                : binding.controlLawV01;
            if (idleLaw != null)
                idleLaw.commandSource = commandSource;

            if (ownership.owner == MavFlightPhysicsOwner.Fault)
            {
                status = "REFUSED - physics ownership is in Fault; clear the fault before a shadow run";
                return;
            }

            if (ownership.owner == MavFlightPhysicsOwner.F16Replacement)
            {
                status = "REFUSED - aircraft is already in live replacement ownership; shadow smoke will not change ownership";
                return;
            }

            if (ownership.owner != MavFlightPhysicsOwner.Shadow)
            {
                string error;
                if (!ownership.TryEnterShadow(out error))
                {
                    status = "REFUSED - could not enter Shadow: " + error;
                    return;
                }
            }

            if (!configured)
            {
                shadowStepBaseline = body.debugShadowComputeSteps;
                loadApplicationBaseline = body.debugLoadApplications;
                configured = true;
            }

            if (!loggedActive)
            {
                Debug.Log(
                    "[Maverick/F16/FDM] POWERED_REFERENCE_SHADOW active | TP-1538 thrust sourced | "
                    + "mouse-instructor command bridge operational | replacement live writes blocked",
                    this);
                loggedActive = true;
            }
        }

        private void UpdateDiagnostics()
        {
            MavSixDoFBody body = binding != null ? binding.sixDoFBody : null;
            if (body == null)
                return;

            tp1538PropulsionAcceptable = propulsionSystem != null
                                        && propulsionSystem.IsAcceptableForLiveFlight;
            operationalCommandSourceAvailable = commandSource != null
                                                && commandSource.IsOperationalCommandSource
                                                && commandSource.HasCommandSignal;

            MavFlightDynamicsReadinessInputs inputs = body.BuildReadinessInputs();
            MavFlightDynamicsReadinessReport withoutLegacy =
                MavFlightDynamicsReadiness.EvaluateAssumingLegacyOwnershipCleared(inputs);
            readyAssumingLegacyOwnershipCleared = withoutLegacy.operationallyLiveReady;

            shadowStepsThisRun = Mathf.Max(0, body.debugShadowComputeSteps - shadowStepBaseline);
            shadowLoadSetFinite = body.debugShadowLoadSetFinite;
            replacementWroteNoLiveLoads = body.debugLoadApplications == loadApplicationBaseline;

            shadowHealthy = configured
                            && ownership != null
                            && ownership.owner == MavFlightPhysicsOwner.Shadow
                            && tp1538PropulsionAcceptable
                            && operationalCommandSourceAvailable
                            && readyAssumingLegacyOwnershipCleared
                            && shadowLoadSetFinite
                            && replacementWroteNoLiveLoads
                            && shadowStepsThisRun >= Mathf.Max(1, requiredFiniteShadowSteps);

            status = shadowHealthy
                ? "HEALTHY POWERED SHADOW - ready for a separate atomic handover experiment"
                : "POWERED SHADOW | owner=" + (ownership != null ? ownership.owner.ToString() : "missing")
                  + " | TP1538=" + tp1538PropulsionAcceptable
                  + " | command=" + operationalCommandSourceAvailable
                  + " | readyExceptLegacy=" + readyAssumingLegacyOwnershipCleared
                  + " | finite=" + shadowLoadSetFinite
                  + " | shadowSteps=" + shadowStepsThisRun
                  + "/" + Mathf.Max(1, requiredFiniteShadowSteps)
                  + " | replacementLiveWrites=" + (replacementWroteNoLiveLoads ? "0" : "DETECTED");
        }

        private void RestoreSafeLegacyWiring(string reason)
        {
            MavSixDoFBody body = binding != null ? binding.sixDoFBody : null;

            if (ownership != null)
            {
                ownership.allowReplacementActivation = false;
                if (ownership.owner == MavFlightPhysicsOwner.Shadow)
                    ownership.ReturnToLegacy(reason);
            }

            if (body != null && binding != null)
            {
                body.propulsionModel = binding.enginePowerModel;
                body.pilotCommandSource = binding.pilotCommandSource;

                if (binding.controlLaw != null)
                    binding.controlLaw.commandSource = binding.pilotCommandSource;

                binding.propulsionModel = binding.enginePowerModel;
            }

            configured = false;
            shadowHealthy = false;
            shadowStepsThisRun = 0;
            shadowLoadSetFinite = false;
            replacementWroteNoLiveLoads = true;
            status = "OFF - legacy flight unchanged";

            if (loggedActive)
                Debug.Log("[Maverick/F16/FDM] POWERED_REFERENCE_SHADOW stopped; returned to Legacy.", this);
            loggedActive = false;
        }

        private void ResolveBinding()
        {
            if (binding == null)
                binding = GetComponent<MavF16SelectionBinding>();
        }
    }

    /// <summary>
    /// Adds the opt-in smoke component beside every runtime F-16 selection binding without changing
    /// scenes or prefabs. The component itself defaults OFF, so merely installing this bootstrap has
    /// zero effect on flight behaviour.
    /// </summary>
    [DefaultExecutionOrder(-8750)]
    public sealed class MavF16PoweredReferenceShadowSmokeBootstrap : MonoBehaviour
    {
        private const string BootstrapObjectName = "Mav_F16PoweredReferenceShadowSmoke";
        private const float ScanIntervalSeconds = 0.25f;
        private float nextScanTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallBootstrap()
        {
            MavF16PoweredReferenceShadowSmokeBootstrap existing =
                FindObjectOfType<MavF16PoweredReferenceShadowSmokeBootstrap>();
            if (existing != null)
                return;

            GameObject go = new GameObject(BootstrapObjectName);
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.DontSave;
            go.AddComponent<MavF16PoweredReferenceShadowSmokeBootstrap>();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextScanTime)
                return;

            nextScanTime = Time.unscaledTime + ScanIntervalSeconds;

            MavF16SelectionBinding[] bindings = FindObjectsOfType<MavF16SelectionBinding>(true);
            for (int i = 0; i < bindings.Length; i++)
            {
                MavF16SelectionBinding binding = bindings[i];
                if (binding == null)
                    continue;

                MavF16PoweredReferenceShadowSmoke smoke =
                    binding.GetComponent<MavF16PoweredReferenceShadowSmoke>();
                if (smoke == null)
                    smoke = binding.gameObject.AddComponent<MavF16PoweredReferenceShadowSmoke>();

                smoke.binding = binding;
            }
        }
    }
}
