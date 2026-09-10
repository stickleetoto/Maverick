#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using MaverickFresh.FlightDynamics;
using MaverickFresh.FlightDynamics.F16;
using MaverickFresh.FlightDynamics.Validation;

namespace MaverickFresh.FlightDynamics.EditorTools
{
    /// <summary>
    /// Scene-free Play Mode validation of the actual Unity FixedUpdate scheduler boundary used by
    /// Phase 3C. It creates a hidden transient rig only after Play Mode has started and destroys it
    /// when the second fixed step has been observed; no scene or prefab is modified.
    /// </summary>
    [InitializeOnLoad]
    public static class MavFlightDynamicsSchedulerValidationEditor
    {
        private const string PendingKey = "Maverick.FDM.SchedulerProbe.Pending";
        private const string AutoExitKey = "Maverick.FDM.SchedulerProbe.AutoExit";

        private static GameObject probeObject;
        private static bool waitingForResult;
        private static bool autoExitWhenDone;

        static MavFlightDynamicsSchedulerValidationEditor()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update -= PollResult;
            EditorApplication.update += PollResult;

            if (EditorApplication.isPlaying && SessionState.GetBool(PendingKey, false))
                StartProbeInPlayMode();
        }

        [MenuItem("Maverick/Flight Dynamics/Run FixedUpdate Scheduler Validation (Play Mode)")]
        public static void RunSchedulerValidation()
        {
            if (waitingForResult)
            {
                Debug.LogWarning("[Maverick/FDM/Scheduler] A scheduler probe is already running.");
                return;
            }

            if (EditorApplication.isPlaying)
            {
                SessionState.SetBool(AutoExitKey, false);
                StartProbeInPlayMode();
                return;
            }

            SessionState.SetBool(PendingKey, true);
            SessionState.SetBool(AutoExitKey, true);
            Debug.Log("[Maverick/FDM/Scheduler] Entering Play Mode for the FixedUpdate scheduler probe.");
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode
                && SessionState.GetBool(PendingKey, false))
            {
                StartProbeInPlayMode();
            }

            if (change == PlayModeStateChange.EnteredEditMode)
            {
                waitingForResult = false;
                probeObject = null;
            }
        }

        private static void StartProbeInPlayMode()
        {
            if (!EditorApplication.isPlaying || waitingForResult)
                return;

            SessionState.SetBool(PendingKey, false);
            autoExitWhenDone = SessionState.GetBool(AutoExitKey, false);
            SessionState.SetBool(AutoExitKey, false);

            MavFlightDynamicsSchedulerProbeState.Reset();

            probeObject = new GameObject("MavFixedUpdateSchedulerProbe");
            probeObject.hideFlags = HideFlags.HideAndDontSave;

            MavSixDoFBody body = probeObject.AddComponent<MavSixDoFBody>();
            Rigidbody rb = probeObject.GetComponent<Rigidbody>();
            rb.useGravity = false;

            MavF16FlightDynamicsProfile profile =
                probeObject.AddComponent<MavF16FlightDynamicsProfile>();
            MavF16AeroModel aeroModel = probeObject.AddComponent<MavF16AeroModel>();
            MavF16ControlActuator actuator = probeObject.AddComponent<MavF16ControlActuator>();
            MavManualPilotCommandSource commandSource =
                probeObject.AddComponent<MavManualPilotCommandSource>();
            MavF16ControlLawV01 controlLaw = probeObject.AddComponent<MavF16ControlLawV01>();
            MavF16EnginePowerModel engine = probeObject.AddComponent<MavF16EnginePowerModel>();
            MavPhysicsOwnershipController ownership =
                probeObject.AddComponent<MavPhysicsOwnershipController>();
            MavSchedulerLegacyOwner legacy = probeObject.AddComponent<MavSchedulerLegacyOwner>();

            body.profileProvider = profile;
            body.aerodynamicModel = aeroModel;
            body.controlSurfaceActuator = actuator;
            body.controlLaw = controlLaw;
            body.pilotCommandSource = commandSource;
            body.propulsionModel = engine;
            body.simulationEnabled = false;
            body.acceptNonAuthoritativePropulsion = true;
            body.conflictingLegacyPhysicsComponents =
                new string[] { MavSchedulerLegacyOwner.TypeName };

            actuator.sixDoFBody = body;
            controlLaw.sixDoFBody = body;
            controlLaw.actuator = actuator;
            controlLaw.commandSource = commandSource;
            controlLaw.driveActuatorInFixedUpdate = true;
            controlLaw.ResetLawState();

            commandSource.treatAsOperationalSource = true;
            commandSource.commandAvailable = true;
            commandSource.command = MavPilotCommand.Neutral;

            ownership.sixDoFBody = body;
            ownership.autoTransitionToNewFdm = false;
            ownership.legacyPhysicsOwners = new string[] { MavSchedulerLegacyOwner.TypeName };
            ownership.state = MavPhysicsOwnershipState.LegacyOwned;

            body.ApplyConfiguredProfile(true);
            body.NotifyOwnershipChanged();

            MavSchedulerBeforeLawProbe beforeLaw =
                probeObject.AddComponent<MavSchedulerBeforeLawProbe>();
            beforeLaw.commandSource = commandSource;
            beforeLaw.ownership = ownership;

            MavSchedulerAfterLawProbe afterLaw =
                probeObject.AddComponent<MavSchedulerAfterLawProbe>();
            afterLaw.controlLaw = controlLaw;

            MavSchedulerAfterOwnershipProbe afterOwnership =
                probeObject.AddComponent<MavSchedulerAfterOwnershipProbe>();
            afterOwnership.ownership = ownership;
            afterOwnership.body = body;
            afterOwnership.legacyOwner = legacy;

            MavSchedulerAfterBodyProbe afterBody =
                probeObject.AddComponent<MavSchedulerAfterBodyProbe>();
            afterBody.body = body;

            MavSchedulerEndProbe end = probeObject.AddComponent<MavSchedulerEndProbe>();
            end.controlLaw = controlLaw;
            end.ownership = ownership;
            end.body = body;
            end.legacyOwner = legacy;

            waitingForResult = true;
            Debug.Log(
                "[Maverick/FDM/Scheduler] Probe armed. Tick 1 primes SourceSignal; tick 2 drops "
                + "the signal before the real -300 law and requests the handover."
            );
        }

        private static void PollResult()
        {
            if (!waitingForResult || !MavFlightDynamicsSchedulerProbeState.completed)
                return;

            waitingForResult = false;
            bool passed = MavFlightDynamicsSchedulerProbeState.passed;
            string summary = MavFlightDynamicsSchedulerProbeState.summary;

            if (probeObject != null)
                Object.Destroy(probeObject);
            probeObject = null;

            if (passed)
                Debug.Log("[Maverick/FDM/Scheduler] PASS | " + summary);
            else
                Debug.LogError("[Maverick/FDM/Scheduler] FAIL | " + summary);

            EditorUtility.DisplayDialog(
                "Maverick FixedUpdate Scheduler Validation",
                (passed ? "PASS\n\n" : "FAIL\n\n") + summary,
                "OK"
            );

            if (autoExitWhenDone && EditorApplication.isPlaying)
            {
                autoExitWhenDone = false;
                EditorApplication.delayCall += EditorApplication.ExitPlaymode;
            }
        }
    }
}
#endif
