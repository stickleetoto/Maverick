using UnityEngine;
using EaglePhysicalAI.Maverick;
using EaglePhysicalAI.Aircraft;
using EaglePhysicalAI.PhysicalAI;
using EaglePhysicalAI.SensorAI;
using EaglePhysicalAI.Sensors.Radar;
using EaglePhysicalAI.Sensors.TargetingPod;
using EaglePhysicalAI.Sensors.Fusion;
using EaglePhysicalAI.CAS;
using EaglePhysicalAI.Safety;
using EaglePhysicalAI.Data;
using EaglePhysicalAI.Training;

namespace EaglePhysicalAI.UI
{
    /// <summary>
    /// Runtime setup checklist. Keep it on while integrating assets, then disable for cleaner playtests.
    /// </summary>
    public class MaverickReadinessHud : MonoBehaviour
    {
        public bool visible = true;
        public KeyCode toggleKey = KeyCode.F12;
        public Vector2 position = new Vector2(16f, 16f);
        public Vector2 size = new Vector2(460f, 520f);

        [Header("References")]
        public AircraftPhysicsController aircraft;
        public AircraftProfileApplier profileApplier;
        public PhysicalAIRuntimeAgent physicalAgent;
        public SensorAIRuntimeAgent sensorAgent;
        public F15ERadarSystem radar;
        public TargetingPodSystem targetingPod;
        public SensorFusionManager fusion;
        public AbstractStrikeSystem strikeSystem;
        public CasValidator validator;
        public FlightEnvelopeSafetyGuard safetyGuard;
        public DatasetQualityMonitor qualityMonitor;
        public EpisodeResetManager resetManager;
        public AutonomousTrainingSessionRunner trainingRunner;

        private void Awake()
        {
            ResolveReferences();
        }

        private void Update()
        {
            if (MaverickInput.GetKeyDown(toggleKey)) visible = !visible;
        }

        private void OnGUI()
        {
            if (!visible) return;
            ResolveReferences();
            GUILayout.BeginArea(new Rect(position.x, position.y, size.x, size.y), GUI.skin.box);
            GUILayout.Label(MaverickProjectInfo.ProjectName + " v" + MaverickProjectInfo.Version);
            GUILayout.Label(MaverickProjectInfo.Subtitle);
            GUILayout.Space(6f);
            Row("AircraftPhysics", aircraft != null, aircraft != null ? "speed " + aircraft.Speed.ToString("0") + " alt " + aircraft.Altitude.ToString("0") : "missing");
            Row("Profile", profileApplier != null, profileApplier != null ? profileApplier.lastAppliedProfile : "missing");
            Row("Physical AI", physicalAgent != null, physicalAgent != null ? physicalAgent.controlMode + " / " + physicalAgent.lastControlNote : "missing");
            Row("Sensor AI", sensorAgent != null, sensorAgent != null ? sensorAgent.controlMode + " / " + sensorAgent.lastControlNote : "missing");
            Row("Radar", radar != null, radar != null ? radar.mode + " tracks " + radar.tracks.Count : "missing");
            Row("Targeting Pod", targetingPod != null, targetingPod != null ? targetingPod.mode + " id " + targetingPod.identificationConfidence.ToString("0.00") : "missing");
            Row("Sensor Fusion", fusion != null, fusion != null ? "conf " + fusion.fusedConfidence.ToString("0.00") : "missing");
            Row("CAS Strike", strikeSystem != null, strikeSystem != null ? "ok " + strikeSystem.successfulStrikes + " abort " + strikeSystem.abortedStrikes : "missing");
            Row("CAS Validator", validator != null, validator != null ? "ready" : "missing");
            Row("Safety", safetyGuard != null, safetyGuard != null ? safetyGuard.state + " / " + safetyGuard.lastReason : "missing");
            Row("Dataset Quality", qualityMonitor != null, qualityMonitor != null ? qualityMonitor.qualityNote : "missing");
            Row("Episode Reset", resetManager != null, resetManager != null ? "episode " + resetManager.episodeIndex : "missing");
            Row("Training Runner", trainingRunner != null, trainingRunner != null ? (trainingRunner.running ? "running" : "stopped") : "missing");
            GUILayout.Space(6f);
            GUILayout.Label("Hotkeys: F1-F4 flight AI | F8-F11 sensor AI | F12 HUD | Insert train | Home F-15E profile | End F-22 profile | G gear");
            GUILayout.EndArea();
        }

        private void Row(string label, bool ok, string note)
        {
            GUILayout.Label((ok ? "[OK] " : "[!!] ") + label + " — " + note);
        }

        private void ResolveReferences()
        {
            if (aircraft == null) aircraft = GetComponent<AircraftPhysicsController>();
            if (profileApplier == null) profileApplier = GetComponent<AircraftProfileApplier>();
            if (physicalAgent == null) physicalAgent = GetComponent<PhysicalAIRuntimeAgent>();
            if (sensorAgent == null) sensorAgent = GetComponent<SensorAIRuntimeAgent>();
            if (radar == null) radar = GetComponent<F15ERadarSystem>();
            if (targetingPod == null) targetingPod = GetComponent<TargetingPodSystem>();
            if (fusion == null) fusion = GetComponent<SensorFusionManager>();
            if (strikeSystem == null) strikeSystem = GetComponent<AbstractStrikeSystem>();
            if (validator == null) validator = GetComponent<CasValidator>();
            if (safetyGuard == null) safetyGuard = GetComponent<FlightEnvelopeSafetyGuard>();
            if (qualityMonitor == null) qualityMonitor = GetComponent<DatasetQualityMonitor>();
            if (resetManager == null) resetManager = GetComponent<EpisodeResetManager>();
            if (trainingRunner == null) trainingRunner = GetComponent<AutonomousTrainingSessionRunner>();
        }
    }
}
