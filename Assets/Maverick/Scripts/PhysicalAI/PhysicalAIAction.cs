using System;
using UnityEngine;
using EaglePhysicalAI.Aircraft;

namespace EaglePhysicalAI.PhysicalAI
{
    public enum PhysicalAIControlMode
    {
        Manual,
        RulePilot,
        LinearPolicy,
        ShadowPolicy
    }

    [Serializable]
    public class PhysicalAIAction
    {
        public float pitch;
        public float roll;
        public float yaw;
        public float throttle;
        public float strike;
        public float abort;

        public const int Count = 6;

        public static readonly string[] Labels =
        {
            "pitch", "roll", "yaw", "throttle", "strike", "abort"
        };

        public PhysicalAIAction Clamp()
        {
            pitch = Mathf.Clamp(pitch, -1f, 1f);
            roll = Mathf.Clamp(roll, -1f, 1f);
            yaw = Mathf.Clamp(yaw, -1f, 1f);
            throttle = Mathf.Clamp01(throttle);
            strike = Mathf.Clamp01(strike);
            abort = Mathf.Clamp01(abort);
            return this;
        }

        public float[] ToArray()
        {
            return new[] { pitch, roll, yaw, throttle, strike, abort };
        }

        public static PhysicalAIAction FromArray(float[] values)
        {
            var action = new PhysicalAIAction();
            if (values == null) return action;
            if (values.Length > 0) action.pitch = values[0];
            if (values.Length > 1) action.roll = values[1];
            if (values.Length > 2) action.yaw = values[2];
            if (values.Length > 3) action.throttle = values[3];
            if (values.Length > 4) action.strike = values[4];
            if (values.Length > 5) action.abort = values[5];
            return action.Clamp();
        }

        public static PhysicalAIAction FromAircraftController(AircraftPhysicsController controller)
        {
            if (controller == null) return new PhysicalAIAction();
            return new PhysicalAIAction
            {
                pitch = controller.pitchInput,
                roll = controller.rollInput,
                yaw = controller.yawInput,
                throttle = controller.targetThrottle,
                strike = 0f,
                abort = 0f
            }.Clamp();
        }

        public static PhysicalAIAction FromManualInput(ManualAircraftInput manualInput)
        {
            if (manualInput == null) return new PhysicalAIAction();
            return new PhysicalAIAction
            {
                pitch = manualInput.lastPitch,
                roll = manualInput.lastRoll,
                yaw = manualInput.lastYaw,
                throttle = manualInput.lastThrottle,
                strike = manualInput.strikePressed ? 1f : 0f,
                abort = manualInput.abortPressed ? 1f : 0f
            }.Clamp();
        }
    }
}
