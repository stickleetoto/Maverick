using System;
using UnityEngine;
using EaglePhysicalAI.Sensors.Radar;
using EaglePhysicalAI.Sensors.TargetingPod;

namespace EaglePhysicalAI.SensorAI
{
    [Serializable]
    public class SensorAIAction
    {
        public float radarModeStep;
        public float radarNextTrack;
        public float radarLock;
        public float radarUnlock;
        public float podModeStep;
        public float podSlaveToRadar;
        public float podDesignate;
        public float podClearTrack;

        public const int Count = 8;

        public static readonly string[] Labels =
        {
            "radar_mode_step",
            "radar_next_track",
            "radar_lock",
            "radar_unlock",
            "pod_mode_step",
            "pod_slave_to_radar",
            "pod_designate",
            "pod_clear_track"
        };

        public SensorAIAction Clamp()
        {
            radarModeStep = Mathf.Clamp(radarModeStep, -1f, 1f);
            radarNextTrack = Mathf.Clamp01(radarNextTrack);
            radarLock = Mathf.Clamp01(radarLock);
            radarUnlock = Mathf.Clamp01(radarUnlock);
            podModeStep = Mathf.Clamp(podModeStep, -1f, 1f);
            podSlaveToRadar = Mathf.Clamp01(podSlaveToRadar);
            podDesignate = Mathf.Clamp01(podDesignate);
            podClearTrack = Mathf.Clamp01(podClearTrack);
            return this;
        }

        public float[] ToArray()
        {
            return new[]
            {
                radarModeStep,
                radarNextTrack,
                radarLock,
                radarUnlock,
                podModeStep,
                podSlaveToRadar,
                podDesignate,
                podClearTrack
            };
        }

        public static SensorAIAction FromArray(float[] values)
        {
            var action = new SensorAIAction();
            if (values == null) return action;
            if (values.Length > 0) action.radarModeStep = values[0];
            if (values.Length > 1) action.radarNextTrack = values[1];
            if (values.Length > 2) action.radarLock = values[2];
            if (values.Length > 3) action.radarUnlock = values[3];
            if (values.Length > 4) action.podModeStep = values[4];
            if (values.Length > 5) action.podSlaveToRadar = values[5];
            if (values.Length > 6) action.podDesignate = values[6];
            if (values.Length > 7) action.podClearTrack = values[7];
            return action.Clamp();
        }

        public static SensorAIAction FromKeyboard(F15ERadarSystem radar, TargetingPodSystem pod)
        {
            var action = new SensorAIAction();
            if (radar != null)
            {
                if (MaverickInput.GetKeyDown(radar.cycleModeKey)) action.radarModeStep = 1f;
                if (MaverickInput.GetKeyDown(radar.nextTrackKey)) action.radarNextTrack = 1f;
                if (MaverickInput.GetKeyDown(radar.lockKey)) action.radarLock = 1f;
                if (MaverickInput.GetKeyDown(radar.unlockKey)) action.radarUnlock = 1f;
            }

            if (pod != null)
            {
                if (MaverickInput.GetKeyDown(pod.cycleModeKey)) action.podModeStep = 1f;
                if (MaverickInput.GetKeyDown(pod.slaveToRadarKey)) action.podSlaveToRadar = 1f;
                if (MaverickInput.GetKeyDown(pod.designateKey)) action.podDesignate = 1f;
                if (MaverickInput.GetKeyDown(pod.clearTrackKey)) action.podClearTrack = 1f;
            }

            return action.Clamp();
        }
    }
}
