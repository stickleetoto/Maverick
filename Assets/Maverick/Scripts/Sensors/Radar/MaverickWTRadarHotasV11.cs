using UnityEngine;
using EaglePhysicalAI.Controls;

namespace EaglePhysicalAI.Sensors.Radar
{
    /// <summary>
    /// War-Thunder-inspired radar HOTAS wrapper.
    /// This calls the existing abstract F15ERadarSystem and adds stateful key handling.
    /// </summary>
    [DisallowMultipleComponent]
    public class MaverickWTRadarHotasV11 : MonoBehaviour
    {
        public F15ERadarSystem radar;
        public MaverickWTKeybindProfileV11 keys;

        [Header("State")]
        public bool radarMasterOn = true;
        public bool useAutoTrackSelect = true;
        public bool showRadarHud = true;
        public string lastRadarEvent = "ready";

        private void Awake()
        {
            if (radar == null) radar = GetComponent<F15ERadarSystem>();
            if (keys == null) keys = GetComponent<MaverickWTKeybindProfileV11>();
            if (keys == null) keys = gameObject.AddComponent<MaverickWTKeybindProfileV11>();
        }

        private void Update()
        {
            if (radar == null) return;

            if (MaverickInput.GetKeyDown(keys.radarPower))
            {
                radarMasterOn = !radarMasterOn;
                radar.radarOn = radarMasterOn;
                if (!radarMasterOn) radar.Unlock();
                lastRadarEvent = radarMasterOn ? "radar_on" : "radar_off";
            }

            if (!radarMasterOn) return;

            radar.autoSelectBestTrack = useAutoTrackSelect;

            if (MaverickInput.GetKeyDown(keys.radarCycleMode))
            {
                radar.CycleMode();
                lastRadarEvent = "mode_" + radar.mode;
            }

            if (MaverickInput.GetKeyDown(keys.radarNextTrack))
            {
                radar.SelectNextTrack();
                lastRadarEvent = radar.selectedTrack != null ? "selected_" + radar.selectedTrack.displayName : "selected_none";
            }

            if (MaverickInput.GetKeyDown(keys.radarLock))
            {
                bool ok = radar.LockSelectedTrack();
                lastRadarEvent = ok ? "lock_ok" : "lock_failed";
            }

            if (MaverickInput.GetKeyDown(keys.radarUnlock))
            {
                radar.Unlock();
                lastRadarEvent = "unlock";
            }
        }
    }
}
