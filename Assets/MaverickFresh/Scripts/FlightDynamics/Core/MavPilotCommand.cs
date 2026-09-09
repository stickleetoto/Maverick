using System;
using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>
    /// Aircraft-independent normalized pilot intent.
    ///
    /// This type deliberately contains no Unity forces, no torque values, and no
    /// surface deflections. It expresses *what the pilot wants*, not how any
    /// particular airframe achieves it. A flight control law converts this into
    /// physical control-surface demand (<see cref="MavControlInput"/>).
    ///
    /// Sign convention (aircraft-conventional, stick-referenced):
    ///   pitch   +1 = maximum nose-up demand      (stick aft)
    ///   roll    +1 = maximum roll-right demand   (stick right)
    ///   yaw     +1 = maximum nose-right demand   (right pedal)
    ///   throttle 0 = idle, 1 = maximum
    ///
    /// The mapping from these normalized demands to signed surface deflections is
    /// owned by the control law, because deflection sign conventions belong to the
    /// aerodynamic reference of each aircraft.
    /// </summary>
    [Serializable]
    public struct MavPilotCommand
    {
        [Range(-1f, 1f)] public float pitch;
        [Range(-1f, 1f)] public float roll;
        [Range(-1f, 1f)] public float yaw;
        [Range(0f, 1f)] public float throttle01;

        public static MavPilotCommand Neutral
        {
            get { return new MavPilotCommand(); }
        }

        /// <summary>
        /// Returns the command with every channel forced into its legal normalized range.
        /// Control laws must never assume callers pre-clamped their input.
        /// </summary>
        public MavPilotCommand Clamped()
        {
            MavPilotCommand result;
            result.pitch = Mathf.Clamp(pitch, -1f, 1f);
            result.roll = Mathf.Clamp(roll, -1f, 1f);
            result.yaw = Mathf.Clamp(yaw, -1f, 1f);
            result.throttle01 = Mathf.Clamp01(throttle01);
            return result;
        }

        public bool IsNeutral(float epsilon)
        {
            return Mathf.Abs(pitch) <= epsilon
                && Mathf.Abs(roll) <= epsilon
                && Mathf.Abs(yaw) <= epsilon;
        }
    }
}
