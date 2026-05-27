using System;
using UnityEngine;

namespace EaglePhysicalAI.SensorAI
{
    [Serializable]
    public class SensorAIObservation
    {
        public const int Count = 28;

        public static readonly string[] Labels =
        {
            "radar_on",
            "radar_mode_norm",
            "radar_track_count_norm",
            "radar_has_selected",
            "radar_selected_range_norm",
            "radar_selected_bearing_norm",
            "radar_selected_quality",
            "radar_selected_id_confidence",
            "radar_has_lock",
            "radar_locked_range_norm",
            "radar_locked_quality",
            "pod_on",
            "pod_mode_norm",
            "pod_has_track",
            "pod_track_quality",
            "pod_id_confidence",
            "pod_has_los",
            "pod_fov_norm",
            "fusion_has_target",
            "fusion_confidence",
            "fusion_friendly_risk",
            "cas_request_active",
            "active_request_priority",
            "strike_cooldown_ready",
            "target_designated",
            "target_hostile",
            "selected_target_in_front",
            "sensor_ready_to_strike"
        };

        public float[] values = new float[Count];

        public float this[int index]
        {
            get => values[index];
            set => values[index] = Mathf.Clamp(value, -1f, 1f);
        }

        public float[] ToArrayCopy()
        {
            var copy = new float[Count];
            Array.Copy(values, copy, Count);
            return copy;
        }

        public void ClampAll()
        {
            if (values == null || values.Length != Count) values = new float[Count];
            for (int i = 0; i < values.Length; i++) values[i] = Mathf.Clamp(values[i], -1f, 1f);
        }
    }
}
