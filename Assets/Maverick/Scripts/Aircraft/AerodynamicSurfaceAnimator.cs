using System;
using UnityEngine;

namespace EaglePhysicalAI.Aircraft
{
    [Serializable]
    public class SurfaceBinding
    {
        public string label = "surface";
        public Transform surface;
        public Vector3 localAxis = Vector3.right;
        public float maxDegrees = 18f;
        public SurfaceInputSource inputSource = SurfaceInputSource.Pitch;
        public float sign = 1f;
        public Quaternion neutralRotation = Quaternion.identity;
    }

    public enum SurfaceInputSource
    {
        Pitch,
        Roll,
        Yaw,
        FlapThrottleAssist,
        AirbrakeAbort
    }

    /// <summary>
    /// Visual-only control surface animator. Assign left/right ailerons, elevator, rudder, etc.
    /// This makes the imported aircraft read better during War Thunder style control tests.
    /// </summary>
    public class AerodynamicSurfaceAnimator : MonoBehaviour
    {
        public AircraftPhysicsController aircraft;
        public ManualAircraftInput manualInput;
        public float responseSpeed = 10f;
        public SurfaceBinding[] surfaces = new SurfaceBinding[0];
        public bool captureNeutralOnAwake = true;

        private void Awake()
        {
            if (aircraft == null) aircraft = GetComponent<AircraftPhysicsController>();
            if (manualInput == null) manualInput = GetComponent<ManualAircraftInput>();
            if (captureNeutralOnAwake) CaptureNeutralRotations();
        }

        [ContextMenu("Capture Neutral Rotations")]
        public void CaptureNeutralRotations()
        {
            if (surfaces == null) return;
            foreach (var s in surfaces)
            {
                if (s != null && s.surface != null) s.neutralRotation = s.surface.localRotation;
            }
        }

        private void LateUpdate()
        {
            if (surfaces == null || aircraft == null) return;
            foreach (var s in surfaces)
            {
                if (s == null || s.surface == null) continue;
                float value = GetInputValue(s.inputSource) * s.sign;
                Quaternion target = s.neutralRotation * Quaternion.AngleAxis(value * s.maxDegrees, s.localAxis.normalized);
                float alpha = 1f - Mathf.Exp(-responseSpeed * Time.deltaTime);
                s.surface.localRotation = Quaternion.Slerp(s.surface.localRotation, target, alpha);
            }
        }

        private float GetInputValue(SurfaceInputSource source)
        {
            switch (source)
            {
                case SurfaceInputSource.Pitch: return aircraft.pitchInput;
                case SurfaceInputSource.Roll: return aircraft.rollInput;
                case SurfaceInputSource.Yaw: return aircraft.yawInput;
                case SurfaceInputSource.FlapThrottleAssist: return Mathf.Clamp01(1f - aircraft.targetThrottle) * 0.35f;
                case SurfaceInputSource.AirbrakeAbort: return manualInput != null && manualInput.abortPressed ? 1f : 0f;
                default: return 0f;
            }
        }
    }
}
