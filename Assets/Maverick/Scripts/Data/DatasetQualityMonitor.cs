using UnityEngine;
using EaglePhysicalAI.PhysicalAI;
using EaglePhysicalAI.SensorAI;

namespace EaglePhysicalAI.Data
{
    /// <summary>
    /// Runtime estimate of whether demonstrations contain useful signal.
    /// It does not parse files; it watches live observations/actions and exposes simple quality hints.
    /// </summary>
    public class DatasetQualityMonitor : MonoBehaviour
    {
        public IntegratedAIDemonstrationRecorder recorder;
        public PhysicalAIRuntimeAgent physicalRuntime;
        public SensorAIRuntimeAgent sensorRuntime;

        [Header("Rolling Stats")]
        public int windowFrames = 600;
        public int samplesSeen;
        public float averagePhysicalActionMagnitude;
        public float averageSensorActionMagnitude;
        public float manualRatio;
        public float strikeActionRatio;
        public float sensorInteractionRatio;
        public float estimatedQualityScore;
        public string qualityNote = "not_enough_samples";

        private float _physicalMagnitudeSum;
        private float _sensorMagnitudeSum;
        private int _manualFrames;
        private int _strikeFrames;
        private int _sensorInteractionFrames;

        private void Awake()
        {
            if (recorder == null) recorder = FindObjectOfType<IntegratedAIDemonstrationRecorder>();
            if (physicalRuntime == null) physicalRuntime = FindObjectOfType<PhysicalAIRuntimeAgent>();
            if (sensorRuntime == null) sensorRuntime = FindObjectOfType<SensorAIRuntimeAgent>();
        }

        private void Update()
        {
            Step();
        }

        public void Step()
        {
            samplesSeen++;
            if (physicalRuntime != null)
            {
                var a = physicalRuntime.appliedAction ?? physicalRuntime.rawPolicyAction;
                float mag = a != null ? (Mathf.Abs(a.pitch) + Mathf.Abs(a.roll) + Mathf.Abs(a.yaw) + Mathf.Abs(a.throttle - 0.5f)) / 4f : 0f;
                _physicalMagnitudeSum += mag;
                if (physicalRuntime.controlMode == PhysicalAIControlMode.Manual) _manualFrames++;
                if (a != null && a.strike > 0.5f) _strikeFrames++;
            }

            if (sensorRuntime != null)
            {
                var s = sensorRuntime.appliedAction ?? sensorRuntime.rawPolicyAction;
                float mag = 0f;
                if (s != null)
                {
                    mag = (Mathf.Abs(s.radarModeStep) + s.radarNextTrack + s.radarLock + s.radarUnlock + Mathf.Abs(s.podModeStep) + s.podSlaveToRadar + s.podDesignate + s.podClearTrack) / SensorAIAction.Count;
                    if (mag > 0.05f) _sensorInteractionFrames++;
                }
                _sensorMagnitudeSum += mag;
            }

            if (samplesSeen >= windowFrames)
            {
                FlushWindow();
            }
        }

        public void FlushWindow()
        {
            int n = Mathf.Max(1, samplesSeen);
            averagePhysicalActionMagnitude = _physicalMagnitudeSum / n;
            averageSensorActionMagnitude = _sensorMagnitudeSum / n;
            manualRatio = _manualFrames / (float)n;
            strikeActionRatio = _strikeFrames / (float)n;
            sensorInteractionRatio = _sensorInteractionFrames / (float)n;

            estimatedQualityScore = 0f;
            estimatedQualityScore += Mathf.Clamp01(averagePhysicalActionMagnitude * 2.5f) * 0.25f;
            estimatedQualityScore += Mathf.Clamp01(averageSensorActionMagnitude * 4f) * 0.2f;
            estimatedQualityScore += Mathf.Clamp01(manualRatio) * 0.25f;
            estimatedQualityScore += Mathf.Clamp01(sensorInteractionRatio * 4f) * 0.2f;
            estimatedQualityScore += Mathf.Clamp01(strikeActionRatio * 12f) * 0.1f;

            if (manualRatio < 0.5f) qualityNote = "mostly_ai_generated_data";
            else if (sensorInteractionRatio < 0.01f) qualityNote = "low_sensor_action_diversity";
            else if (averagePhysicalActionMagnitude < 0.03f) qualityNote = "low_flight_action_diversity";
            else if (estimatedQualityScore > 0.62f) qualityNote = "good_training_signal";
            else qualityNote = "usable_but_needs_more_variety";

            samplesSeen = 0;
            _physicalMagnitudeSum = 0f;
            _sensorMagnitudeSum = 0f;
            _manualFrames = 0;
            _strikeFrames = 0;
            _sensorInteractionFrames = 0;
        }
    }
}
