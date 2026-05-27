using UnityEngine;
using EaglePhysicalAI.Battlefield;
using EaglePhysicalAI.Sensors.Fusion;
using EaglePhysicalAI.Sensors.Radar;
using EaglePhysicalAI.Sensors.TargetingPod;

namespace EaglePhysicalAI.SensorAI
{
    public class SensorRuleCasPilot : MonoBehaviour
    {
        public bool aiEnabled;
        public F15ERadarSystem radar;
        public TargetingPodSystem targetingPod;
        public SensorFusionManager fusion;
        public CasRequestManager requestManager;

        [Header("Behavior")]
        public float minLockQuality = 0.35f;
        public float minDesignateConfidence = 0.58f;
        public float actionCooldown = 0.35f;
        public bool preferCasTargetCueMode = true;
        public bool autoLock = true;
        public bool autoSlavePod = true;
        public bool autoDesignate = true;

        [Header("Debug")]
        public SensorAIAction lastRuleAction = new SensorAIAction();
        public string lastRuleNote = "none";

        private float _nextActionTime;

        private void Awake()
        {
            if (radar == null) radar = FindObjectOfType<F15ERadarSystem>();
            if (targetingPod == null) targetingPod = FindObjectOfType<TargetingPodSystem>();
            if (fusion == null) fusion = FindObjectOfType<SensorFusionManager>();
            if (requestManager == null) requestManager = FindObjectOfType<CasRequestManager>();
        }

        private void Update()
        {
            if (!aiEnabled) return;
            if (Time.time < _nextActionTime) return;
            _nextActionTime = Time.time + actionCooldown;
            SensorAIAction action = DecideAction();
            ApplyRuleAction(action);
        }

        public SensorAIAction DecideAction()
        {
            var action = new SensorAIAction();
            bool hasRequest = requestManager != null && requestManager.activeRequest != null && requestManager.activeRequest.active;

            if (radar != null)
            {
                F15ERadarMode desiredMode = hasRequest && preferCasTargetCueMode
                    ? F15ERadarMode.CasTargetCue
                    : F15ERadarMode.TrackWhileScan;

                if (radar.mode != desiredMode)
                {
                    radar.SetMode(desiredMode);
                    lastRuleNote = "rule_set_radar_mode_" + desiredMode;
                    return action;
                }

                if (radar.selectedTrack == null && radar.tracks.Count > 0)
                {
                    radar.SelectTrack(radar.GetBestTrack());
                    lastRuleNote = "rule_select_best_radar_track";
                    return action;
                }

                if (autoLock && radar.lockedTrack == null && radar.selectedTrack != null && radar.selectedTrack.trackQuality >= minLockQuality)
                {
                    action.radarLock = 1f;
                    lastRuleNote = "rule_request_radar_lock";
                    return action;
                }
            }

            if (targetingPod != null)
            {
                bool hasRadarCue = radar != null && (radar.lockedTrack != null || radar.selectedTrack != null);
                if (autoSlavePod && hasRadarCue && targetingPod.trackedTarget == null)
                {
                    action.podSlaveToRadar = 1f;
                    lastRuleNote = "rule_slave_pod_to_radar";
                    return action;
                }

                if (targetingPod.trackedTarget != null && targetingPod.mode != TargetingPodMode.PointTrack)
                {
                    targetingPod.mode = TargetingPodMode.PointTrack;
                    lastRuleNote = "rule_set_pod_point_track";
                    return action;
                }

                if (autoDesignate && targetingPod.designatedGroundUnit == null)
                {
                    float confidence = fusion != null ? fusion.fusedConfidence : targetingPod.identificationConfidence;
                    if (confidence >= minDesignateConfidence && targetingPod.hasLineOfSight)
                    {
                        action.podDesignate = 1f;
                        lastRuleNote = "rule_designate_confirmed_target";
                        return action;
                    }
                }
            }

            lastRuleNote = "rule_hold_sensor_state";
            return action.Clamp();
        }

        public void ApplyRuleAction(SensorAIAction action)
        {
            lastRuleAction = action != null ? action.Clamp() : new SensorAIAction();
            if (action == null) return;

            if (radar != null)
            {
                if (action.radarNextTrack > 0.5f) radar.SelectNextTrack();
                if (action.radarLock > 0.5f) radar.LockSelectedTrack();
                if (action.radarUnlock > 0.5f) radar.Unlock();
            }

            if (targetingPod != null)
            {
                if (action.podSlaveToRadar > 0.5f) targetingPod.SlaveToRadarTrack();
                if (action.podDesignate > 0.5f) targetingPod.TryDesignateCurrentTarget();
                if (action.podClearTrack > 0.5f) targetingPod.ClearTrack();
            }
        }
    }
}
