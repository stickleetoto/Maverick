using UnityEngine;
using EaglePhysicalAI.Sensors.Fusion;
using EaglePhysicalAI.Sensors.Radar;
using EaglePhysicalAI.Sensors.TargetingPod;

namespace EaglePhysicalAI.SensorAI
{
    public class SensorAIHUD : MonoBehaviour
    {
        public SensorAIRuntimeAgent runtimeAgent;
        public SensorAIObservationBuilder observationBuilder;
        public SensorBehaviorCloningLinearPolicy linearPolicy;
        public OnlineSensorBehaviorCloningTrainer onlineTrainer;
        public F15ERadarSystem radar;
        public TargetingPodSystem targetingPod;
        public SensorFusionManager fusion;

        public bool showHud = true;
        public KeyCode toggleKey = KeyCode.F12;
        public Rect windowRect = new Rect(20, 520, 430, 250);

        private void Awake()
        {
            if (runtimeAgent == null) runtimeAgent = FindObjectOfType<SensorAIRuntimeAgent>();
            if (observationBuilder == null) observationBuilder = FindObjectOfType<SensorAIObservationBuilder>();
            if (linearPolicy == null) linearPolicy = FindObjectOfType<SensorBehaviorCloningLinearPolicy>();
            if (onlineTrainer == null) onlineTrainer = FindObjectOfType<OnlineSensorBehaviorCloningTrainer>();
            if (radar == null) radar = FindObjectOfType<F15ERadarSystem>();
            if (targetingPod == null) targetingPod = FindObjectOfType<TargetingPodSystem>();
            if (fusion == null) fusion = FindObjectOfType<SensorFusionManager>();
        }

        private void Update()
        {
            if (MaverickInput.GetKeyDown(toggleKey)) showHud = !showHud;
        }

        private void OnGUI()
        {
            if (!showHud) return;
            windowRect = GUILayout.Window(GetInstanceID(), windowRect, DrawWindow, "Sensor AI v0.5");
        }

        private void DrawWindow(int id)
        {
            GUILayout.Label($"Mode: {(runtimeAgent != null ? runtimeAgent.controlMode.ToString() : "none")}");
            GUILayout.Label($"Note: {(runtimeAgent != null ? runtimeAgent.lastControlNote : "none")}");
            GUILayout.Space(4);

            if (radar != null)
            {
                string selected = radar.selectedTrack != null ? radar.selectedTrack.displayName : "none";
                string locked = radar.lockedTrack != null ? radar.lockedTrack.displayName : "none";
                GUILayout.Label($"Radar: {radar.mode} | tracks {radar.tracks.Count} | selected {selected} | locked {locked}");
            }

            if (targetingPod != null)
            {
                string tracked = targetingPod.trackedTarget != null ? targetingPod.trackedTarget.name : "none";
                string designated = targetingPod.designatedGroundUnit != null ? targetingPod.designatedGroundUnit.name : "none";
                GUILayout.Label($"TPOD: {targetingPod.mode} | trackQ {targetingPod.targetTrackQuality:0.00} | id {targetingPod.identificationConfidence:0.00} | LOS {targetingPod.hasLineOfSight}");
                GUILayout.Label($"TPOD target: {tracked} | designated {designated}");
            }

            if (fusion != null)
            {
                string fused = fusion.fusedBestGroundUnit != null ? fusion.fusedBestGroundUnit.name : "none";
                GUILayout.Label($"Fusion: {fused} | conf {fusion.fusedConfidence:0.00} | {fusion.fusedReason}");
            }

            if (runtimeAgent != null)
            {
                float[] action = runtimeAgent.appliedAction != null ? runtimeAgent.appliedAction.ToArray() : null;
                if (action != null)
                {
                    GUILayout.Label($"Applied sensor action: Rmode {action[0]:0.00} next {action[1]:0.00} lock {action[2]:0.00} | Pmode {action[4]:0.00} slave {action[5]:0.00} desig {action[6]:0.00}");
                }
            }

            if (linearPolicy != null)
            {
                GUILayout.Label($"Sensor policy samples: {linearPolicy.trainedSamples} | loss {linearPolicy.lastLoss:0.0000}");
            }
            if (onlineTrainer != null)
            {
                GUILayout.Label($"Online sensor trainer: {onlineTrainer.samplesTrained} | last {onlineTrainer.lastLoss:0.0000}");
            }

            GUILayout.Label("F8 Manual | F9 RuleSensor | F10 LinearSensor | F11 ShadowSensor | F12 HUD");
            GUI.DragWindow();
        }
    }
}
