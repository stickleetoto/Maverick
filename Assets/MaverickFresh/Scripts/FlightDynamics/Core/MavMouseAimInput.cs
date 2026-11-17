using System;
using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>
    /// The persistent mouse aim: a WORLD direction stored as azimuth / elevation about world up. Mouse deltas move it;
    /// nothing else does (free-look has its own, camera-only offset). It is not the aircraft's orientation and is never
    /// written to the aircraft.
    ///
    /// Azimuth 0 = world +Z, +90 = world +X (clockwise seen from above); elevation +90 = world +Y. Elevation is clamped
    /// to +-<see cref="MavMouseAimInput.DefaultElevationLimitDeg"/> so the yaw axis never degenerates at the zenith; a
    /// target directly overhead is reachable through the guidance (scripted targets), not through the mouse.
    /// </summary>
    [Serializable]
    public struct MavMouseAimAngles
    {
        public float azimuthDeg;
        public float elevationDeg;

        public Vector3 Direction
        {
            get { return MavMouseAimInput.DirectionFromAngles(azimuthDeg, elevationDeg); }
        }
    }

    /// <summary>Pure aim accumulation, so the code that flies is the code the timing tests drive.</summary>
    public static class MavMouseAimInput
    {
        public const float DefaultElevationLimitDeg = 85f;
        public const float FreeLookYawLimitDeg = 160f;
        public const float FreeLookPitchLimitDeg = 80f;

        public static Vector3 DirectionFromAngles(float azimuthDeg, float elevationDeg)
        {
            float az = azimuthDeg * Mathf.Deg2Rad;
            float el = elevationDeg * Mathf.Deg2Rad;
            float c = Mathf.Cos(el);
            return new Vector3(Mathf.Sin(az) * c, Mathf.Sin(el), Mathf.Cos(az) * c);
        }

        /// <summary>Angles of a world direction. False (angles untouched) for a non-finite or negligible vector.</summary>
        public static bool TryAnglesFromDirection(Vector3 direction, float elevationLimitDeg, out MavMouseAimAngles angles)
        {
            angles = new MavMouseAimAngles();
            if (!MavMouseAimGuidance.IsFinite(direction) || direction.sqrMagnitude < 1e-12f)
                return false;

            Vector3 d = direction.normalized;
            float horizontal = Mathf.Sqrt(d.x * d.x + d.z * d.z);
            angles.azimuthDeg = horizontal > 1e-6f ? Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg : 0f;
            float limit = Mathf.Clamp(elevationLimitDeg, 0f, 89.9f);
            angles.elevationDeg = Mathf.Clamp(Mathf.Atan2(d.y, horizontal) * Mathf.Rad2Deg, -limit, limit);
            return true;
        }

        /// <summary>
        /// Adds one sampled mouse delta (already in degrees) to the aim. Azimuth wraps to [-180, 180); elevation clamps.
        /// Additive, so within the clamp the result does not depend on how the motion was split across frames.
        /// Returns false and leaves the aim unchanged for a non-finite delta.
        /// </summary>
        public static bool ApplyDelta(ref MavMouseAimAngles aim, float deltaAzimuthDeg, float deltaElevationDeg, float elevationLimitDeg)
        {
            if (!MavMouseAimGuidance.IsFinite(deltaAzimuthDeg) || !MavMouseAimGuidance.IsFinite(deltaElevationDeg))
                return false;

            float limit = Mathf.Clamp(elevationLimitDeg, 0f, 89.9f);
            float az = aim.azimuthDeg + deltaAzimuthDeg;
            aim.azimuthDeg = az >= 180f || az < -180f ? MavMouseAimGuidance.WrapDeg(az) : az;   // wrap only when needed: no extra rounding
            aim.elevationDeg = Mathf.Clamp(aim.elevationDeg + deltaElevationDeg, -limit, limit);
            return true;
        }

        /// <summary>Free-look offset (camera only): yaw / pitch relative to the aim, clamped.</summary>
        public static bool ApplyFreeLookDelta(ref Vector2 freeLookYawPitchDeg, float deltaYawDeg, float deltaPitchDeg)
        {
            if (!MavMouseAimGuidance.IsFinite(deltaYawDeg) || !MavMouseAimGuidance.IsFinite(deltaPitchDeg))
                return false;

            freeLookYawPitchDeg.x = Mathf.Clamp(freeLookYawPitchDeg.x + deltaYawDeg, -FreeLookYawLimitDeg, FreeLookYawLimitDeg);
            freeLookYawPitchDeg.y = Mathf.Clamp(freeLookYawPitchDeg.y + deltaPitchDeg, -FreeLookPitchLimitDeg, FreeLookPitchLimitDeg);
            return true;
        }

        /// <summary>The camera's view direction: the aim rotated by the free-look offset (yaw about world up, then pitch).</summary>
        public static Vector3 ViewDirection(MavMouseAimAngles aim, Vector2 freeLookYawPitchDeg)
        {
            float el = Mathf.Clamp(aim.elevationDeg + freeLookYawPitchDeg.y, -89f, 89f);
            return DirectionFromAngles(aim.azimuthDeg + freeLookYawPitchDeg.x, el);
        }

        /// <summary>Mouse units -> degrees (sensitivity, Y inversion). Mouse up = aim up unless inverted.</summary>
        public static Vector2 UnitsToDegrees(float mouseX, float mouseY, float degreesPerMouseUnit, bool invertY)
        {
            return new Vector2(mouseX * degreesPerMouseUnit, (invertY ? -mouseY : mouseY) * degreesPerMouseUnit);
        }
    }
}
