using UnityEngine;
using EaglePhysicalAI.Sensors.TargetingPod;
using EaglePhysicalAI.Sensors.Fusion;

namespace EaglePhysicalAI.Sensors.UI
{
    public class TargetingPodHud : MonoBehaviour
    {
        public TargetingPodSystem pod;
        public SensorFusionManager fusion;
        public bool show = true;
        public Vector2 position = new Vector2(450, 260);
        public Vector2 size = new Vector2(430, 250);

        private void Awake()
        {
            if (pod == null) pod = FindObjectOfType<TargetingPodSystem>();
            if (fusion == null) fusion = FindObjectOfType<SensorFusionManager>();
        }

        private void OnGUI()
        {
            if (!show || pod == null) return;
            GUILayout.BeginArea(new Rect(position.x, position.y, size.x, size.y), GUI.skin.box);
            GUILayout.Label("Targeting Pod / Abstract CAS Sensor");
            GUILayout.Label("Mode: " + pod.mode + " | P:Mode Y:Slave Enter:Designate");
            GUILayout.Label("Slew: I/J/K/L | Zoom: +/- | Clear: Backspace");
            GUILayout.Space(4);
            GUILayout.Label("Track: " + (pod.trackedTarget != null ? pod.trackedTarget.name : "none"));
            GUILayout.Label("Quality: " + pod.targetTrackQuality.ToString("0.00") + " | ID: " + pod.identificationConfidence.ToString("0.00") + " | LOS: " + pod.hasLineOfSight);
            GUILayout.Label("Designated: " + (pod.designatedGroundUnit != null ? pod.designatedGroundUnit.unitId : "none"));
            GUILayout.Label("Last: " + pod.lastDesignateResult);
            GUILayout.Space(4);
            if (fusion != null)
            {
                GUILayout.Label("Fused Target: " + (fusion.fusedBestGroundUnit != null ? fusion.fusedBestGroundUnit.unitId : "none"));
                GUILayout.Label("Fusion: " + fusion.fusedConfidence.ToString("0.00") + " | " + fusion.fusedReason);
            }
            GUILayout.EndArea();
        }
    }
}
