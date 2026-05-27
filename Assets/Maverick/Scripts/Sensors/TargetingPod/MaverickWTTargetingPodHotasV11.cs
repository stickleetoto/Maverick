using UnityEngine;
using EaglePhysicalAI.Controls;

namespace EaglePhysicalAI.Sensors.TargetingPod
{
    [DisallowMultipleComponent]
    public class MaverickWTTargetingPodHotasV11 : MonoBehaviour
    {
        public TargetingPodSystem pod;
        public MaverickWTKeybindProfileV11 keys;

        [Header("State")]
        public bool podMasterOn = true;
        public string lastPodEvent = "ready";

        private void Awake()
        {
            if (pod == null) pod = GetComponent<TargetingPodSystem>();
            if (keys == null) keys = GetComponent<MaverickWTKeybindProfileV11>();
            if (keys == null) keys = gameObject.AddComponent<MaverickWTKeybindProfileV11>();
        }

        private void Update()
        {
            if (pod == null) return;
            pod.podOn = podMasterOn;

            if (MaverickInput.GetKeyDown(keys.podCycleMode))
            {
                pod.CycleMode();
                lastPodEvent = "mode_" + pod.mode;
            }

            if (MaverickInput.GetKeyDown(keys.podSlaveToRadar))
            {
                pod.SlaveToRadarTrack();
                lastPodEvent = "slave_to_radar";
            }

            if (MaverickInput.GetKeyDown(keys.podDesignate))
            {
                bool ok = pod.TryDesignateCurrentTarget();
                lastPodEvent = ok ? "designate_ok" : "designate_failed_" + pod.lastDesignateResult;
            }

            if (MaverickInput.GetKeyDown(keys.podClear))
            {
                pod.ClearTrack();
                lastPodEvent = "clear";
            }
        }
    }
}
