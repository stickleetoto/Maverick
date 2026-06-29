using UnityEngine;

namespace MaverickFresh
{
    /// <summary>
    /// v0.20.7 atmospheric engine and transonic drag model.
    /// This does not own throttle input. MavMouseFlightJet still computes commanded throttle;
    /// this component only scales available thrust by altitude/Mach and optionally adds wave drag.
    /// Keep it on Mav_Player next to MavMouseFlightJet and MavAeroBody.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class MavAtmosphericEngine : MonoBehaviour
    {
        [Header("v0.20.7 Atmospheric Engine")]
        public bool useAtmosphericEngine = true;
        [Range(0f, 1f)] public float engineModelBlend = 0.65f;
        public float seaLevelThrustScale = 1.0f;
        public float highAltitudeThrustFloor = 0.46f;
        public float thrustScaleHeight = 10500f;
        public float ramRecovery = 0.18f;
        public float ramMachStart = 0.55f;
        public float ramMachFull = 1.60f;
        public float afterburnerRamBonus = 0.06f;
        public float minThrustScale = 0.35f;
        public float maxThrustScale = 1.18f;

        [Header("Transonic / Wave Drag")]
        public bool applyWaveDrag = true;
        public float transonicMachStart = 0.88f;
        public float transonicMachEnd = 1.18f;
        public float waveDragStrength = 0.36f;
        public float supersonicDragStrength = 0.12f;
        public float maxWaveDragAccel = 22f;

        [Header("Debug")]
        public float debugAltitude;
        public float debugSpeed;
        public float debugMach;
        public float debugAltitudeScale = 1f;
        public float debugRamScale = 1f;
        public float debugThrustScale = 1f;
        public float debugWaveDragAccel;
        public bool debugAfterburnerSeen;

        private Rigidbody rb;
        private MavMouseFlightJet jet;

        private void Awake()
        {
            Resolve();
        }

        private void OnEnable()
        {
            Resolve();
        }

        private void FixedUpdate()
        {
            Resolve();
            UpdateDebug(jet != null && jet.afterburnerActive);
            ApplyWaveDragIfNeeded();
        }

        public float GetThrustScale(bool afterburnerActive)
        {
            if (!useAtmosphericEngine)
                return 1f;

            Resolve();
            UpdateDebug(afterburnerActive);
            return debugThrustScale;
        }

        private void Resolve()
        {
            if (rb == null)
                rb = GetComponent<Rigidbody>();
            if (jet == null)
                jet = GetComponent<MavMouseFlightJet>();
        }

        private void UpdateDebug(bool afterburnerActive)
        {
            if (rb == null)
            {
                debugThrustScale = 1f;
                return;
            }

            debugAltitude = transform.position.y;
            debugSpeed = rb.linearVelocity.magnitude;
            debugMach = debugSpeed / 343f;
            debugAfterburnerSeen = afterburnerActive;

            float altitude = Mathf.Max(0f, debugAltitude);
            float altRaw = Mathf.Exp(-altitude / Mathf.Max(500f, thrustScaleHeight));
            debugAltitudeScale = Mathf.Lerp(Mathf.Clamp01(highAltitudeThrustFloor), 1f, altRaw) * Mathf.Max(0.01f, seaLevelThrustScale);

            float ramT = Mathf.InverseLerp(ramMachStart, Mathf.Max(ramMachStart + 0.01f, ramMachFull), debugMach);
            debugRamScale = 1f + ramRecovery * Mathf.Clamp01(ramT);
            if (afterburnerActive)
                debugRamScale += afterburnerRamBonus * Mathf.Clamp01(ramT);

            float modelScale = Mathf.Clamp(debugAltitudeScale * debugRamScale, minThrustScale, maxThrustScale);
            debugThrustScale = Mathf.Lerp(1f, modelScale, Mathf.Clamp01(engineModelBlend));
        }

        private void ApplyWaveDragIfNeeded()
        {
            debugWaveDragAccel = 0f;
            if (!useAtmosphericEngine || !applyWaveDrag || rb == null)
                return;

            Vector3 v = rb.linearVelocity;
            float speed = v.magnitude;
            if (speed < 20f)
                return;

            float mach = speed / 343f;
            float transonicT = Mathf.InverseLerp(transonicMachStart, Mathf.Max(transonicMachStart + 0.01f, transonicMachEnd), mach);
            float wave = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(transonicT)) * waveDragStrength;
            float supersonic = Mathf.Max(0f, mach - 1f) * supersonicDragStrength;
            debugWaveDragAccel = Mathf.Min(maxWaveDragAccel, (wave + supersonic) * Mathf.Max(0f, engineModelBlend));

            if (debugWaveDragAccel > 0.001f)
                rb.AddForce(-v.normalized * debugWaveDragAccel, ForceMode.Acceleration);
        }
    }
}
