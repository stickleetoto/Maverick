using UnityEngine;
using EaglePhysicalAI.Sensors.Fusion;
using EaglePhysicalAI.Sensors.Radar;
using EaglePhysicalAI.Sensors.TargetingPod;

namespace EaglePhysicalAI.SensorAI
{
    public class SensorActionValidator : MonoBehaviour
    {
        public F15ERadarSystem radar;
        public TargetingPodSystem targetingPod;
        public SensorFusionManager fusion;

        [Header("Thresholds")]
        public float lockQualityThreshold = 0.22f;
        public float designateFusionThreshold = 0.48f;
        public float designatePodQualityThreshold = 0.35f;
        public float actionThreshold = 0.55f;

        [Header("Debug")]
        public string lastSafetyNote = "none";

        private void Awake()
        {
            if (radar == null) radar = FindObjectOfType<F15ERadarSystem>();
            if (targetingPod == null) targetingPod = FindObjectOfType<TargetingPodSystem>();
            if (fusion == null) fusion = FindObjectOfType<SensorFusionManager>();
        }

        public SensorAIAction Validate(SensorAIAction action)
        {
            if (action == null) return new SensorAIAction();
            var safe = SensorAIAction.FromArray(action.ToArray());
            lastSafetyNote = "sensor_action_ok";

            if (radar == null)
            {
                safe.radarModeStep = 0f;
                safe.radarNextTrack = 0f;
                safe.radarLock = 0f;
                safe.radarUnlock = 0f;
                lastSafetyNote = "no_radar_reference";
            }
            else
            {
                if (radar.selectedTrack == null || radar.selectedTrack.trackQuality < lockQualityThreshold)
                {
                    safe.radarLock = 0f;
                    if (action.radarLock >= actionThreshold) lastSafetyNote = "blocked_radar_lock_low_quality";
                }
            }

            if (targetingPod == null)
            {
                safe.podModeStep = 0f;
                safe.podSlaveToRadar = 0f;
                safe.podDesignate = 0f;
                safe.podClearTrack = 0f;
                if (lastSafetyNote == "sensor_action_ok") lastSafetyNote = "no_targeting_pod_reference";
            }
            else
            {
                float fusionConfidence = fusion != null ? fusion.fusedConfidence : 0f;
                bool podTrackOk = targetingPod.targetTrackQuality >= designatePodQualityThreshold || targetingPod.trackedTarget != null;
                if (fusionConfidence < designateFusionThreshold && !podTrackOk)
                {
                    safe.podDesignate = 0f;
                    if (action.podDesignate >= actionThreshold) lastSafetyNote = "blocked_designation_low_sensor_confidence";
                }
            }

            return safe.Clamp();
        }
    }
}
