using MaverickFresh.Combat;
using UnityEngine;

namespace MaverickFresh
{
    /// <summary>
    /// Compact OnGUI overlay for v0.20.9 sensors. Kept separate from MavFreshHud so the main HUD stays stable.
    /// </summary>
    public class MavSensorHudOverlay : MonoBehaviour
    {
        public MavF22SensorSuite sensor;
        public Camera playerCamera;
        public bool visible = true;
        public bool drawTargetMarker = true;
        public KeyCode toggleKey = KeyCode.F9;

        private GUIStyle panel;
        private GUIStyle marker;

        private void Update()
        {
            if (MavFreshInput.GetKeyDown(toggleKey))
                visible = !visible;
        }

        private void OnGUI()
        {
            Resolve();
            if (!visible || sensor == null)
                return;

            EnsureStyles();

            // The F-22 suite's STT lock is NOT the aircraft's lock authority - MavTrackLockController is.
            // STT is kept running as the legacy evidence the lock migration is measured against, so this
            // overlay is a SHADOW readout and says so. An unlabelled "LOCK" here is indistinguishable from
            // the authoritative lock, which is the exact confusion the authority was extracted to end.
            string lockText = MavCombatScopePolicy.ShadowLabel + " "
                            + (sensor.debugHasLock ? "STT LOCK" : $"STT ACQ {sensor.debugLockProgress01 * 100f:0}%");
            string text =
                $"SENSOR {sensor.debugModeLabel} {(sensor.useSensorSuite ? "ON" : "OFF")} [{MavCombatScopePolicy.ShadowLabel}]  Z MODE  X TARGET  F9 HIDE\n" +
                $"CONTACTS {sensor.debugContactCount}  TGT {sensor.debugSelectedName}  {sensor.debugSelectedDistance:0}m  ASP {sensor.debugSelectedAspectDeg:0}  Q {sensor.debugSelectedQuality:0.00}  {lockText}";

            GUI.Box(new Rect(Screen.width - 430f, 10f, 420f, 54f), text, panel);

            if (drawTargetMarker && sensor.selectedTarget != null && playerCamera != null)
                DrawMarker(sensor.selectedTarget.transform.position,
                           sensor.debugHasLock ? MavCombatScopePolicy.ShadowLabel + " STT" : "TGT");
        }

        private void DrawMarker(Vector3 world, string label)
        {
            Vector3 p = playerCamera.WorldToScreenPoint(world);
            if (p.z <= 1f)
                return;
            Rect r = new Rect(p.x - 34f, Screen.height - p.y - 17f, 68f, 34f);
            GUI.Box(r, label, marker);
        }

        private void Resolve()
        {
            if (sensor == null)
                sensor = FindObjectOfType<MavF22SensorSuite>();
            if (playerCamera == null)
                playerCamera = Camera.main;
        }

        private void EnsureStyles()
        {
            if (panel != null)
                return;
            panel = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 13 };
            marker = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontSize = 14 };
        }
    }
}
