using System;
using UnityEngine;

namespace EaglePhysicalAI.PhysicalAI
{
    [Serializable]
    public class PhysicalAIObservation
    {
        public const int Count = 32;

        public static readonly string[] Labels =
        {
            "speed_norm",
            "forward_speed_norm",
            "altitude_norm",
            "vertical_speed_norm",
            "throttle",
            "pitch_input",
            "roll_input",
            "yaw_input",
            "stall_risk",
            "is_stalling",
            "is_crashed",
            "pitch_angle_norm",
            "roll_angle_norm",
            "cas_request_active",
            "target_distance_norm",
            "target_bearing_x",
            "target_elevation_y",
            "target_in_front_dot",
            "strike_allowed",
            "friendly_risk",
            "geometry_score",
            "strike_cooldown_ready",
            "successful_strikes_norm",
            "aborted_strikes_norm",
            "friendly_fire_norm",
            "target_alive",
            "altitude_above_target_norm",
            "target_priority_norm",
            "low_altitude_danger",
            "validation_denied",
            "target_left_right_sign",
            "target_up_down_sign"
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
