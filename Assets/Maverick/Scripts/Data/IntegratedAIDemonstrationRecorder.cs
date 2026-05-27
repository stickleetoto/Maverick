using System;
using System.IO;
using UnityEngine;
using EaglePhysicalAI.Aircraft;
using EaglePhysicalAI.PhysicalAI;
using EaglePhysicalAI.SensorAI;

namespace EaglePhysicalAI.Data
{
    [Serializable]
    public class IntegratedAISample
    {
        public float time;
        public int frame;
        public string sessionId;
        public string testerId;
        public string missionId;
        public string source;
        public string tag;
        public float[] physicalObservation;
        public float[] physicalAction;
        public float[] sensorObservation;
        public float[] sensorAction;
        public float physicalReward;
        public string flightMode;
        public string sensorMode;
    }

    public class IntegratedAIDemonstrationRecorder : MonoBehaviour
    {
        public bool recordingEnabled = true;
        public bool recordOnlyManualModes = true;
        public float sampleInterval = 0.05f;
        public string testerId = "anonymous";
        public string missionId = "mission_001";
        public string source = "human_integrated";
        public string runTag = "untagged";

        [Header("References")]
        public PhysicalAIRuntimeAgent physicalRuntime;
        public PhysicalAIObservationBuilder physicalObservationBuilder;
        public ManualAircraftInput manualInput;
        public PhysicalAIRewardTracker rewardTracker;
        public SensorAIRuntimeAgent sensorRuntime;
        public SensorAIObservationBuilder sensorObservationBuilder;

        [Header("Debug")]
        public string sessionId;
        public string currentFilePath;
        public int samplesWritten;

        private StreamWriter _writer;
        private float _nextSampleTime;

        private void Awake()
        {
            if (physicalRuntime == null) physicalRuntime = FindObjectOfType<PhysicalAIRuntimeAgent>();
            if (physicalObservationBuilder == null) physicalObservationBuilder = FindObjectOfType<PhysicalAIObservationBuilder>();
            if (manualInput == null && physicalRuntime != null) manualInput = physicalRuntime.manualInput;
            if (manualInput == null) manualInput = FindObjectOfType<ManualAircraftInput>();
            if (rewardTracker == null) rewardTracker = FindObjectOfType<PhysicalAIRewardTracker>();
            if (sensorRuntime == null) sensorRuntime = FindObjectOfType<SensorAIRuntimeAgent>();
            if (sensorObservationBuilder == null) sensorObservationBuilder = FindObjectOfType<SensorAIObservationBuilder>();
        }

        private void OnEnable()
        {
            if (recordingEnabled) StartRecording();
        }

        private void OnDisable()
        {
            StopRecording();
        }

        private void Update()
        {
            if (!recordingEnabled || _writer == null) return;
            if (recordOnlyManualModes)
            {
                if (physicalRuntime != null && physicalRuntime.controlMode != PhysicalAIControlMode.Manual) return;
                if (sensorRuntime != null && sensorRuntime.controlMode != SensorAIControlMode.Manual) return;
            }

            if (Time.time < _nextSampleTime) return;
            _nextSampleTime = Time.time + sampleInterval;
            WriteSample();
        }

        [ContextMenu("Start Integrated Recording")]
        public void StartRecording()
        {
            StopRecording();
            sessionId = $"{Sanitize(testerId)}_{Sanitize(missionId)}_integrated_{DateTime.Now:yyyyMMdd_HHmmss}";
            string dir = Path.Combine(Application.persistentDataPath, "EaglePhysicalAILab", "integrated_demonstrations");
            Directory.CreateDirectory(dir);
            currentFilePath = Path.Combine(dir, sessionId + ".jsonl");
            _writer = new StreamWriter(currentFilePath, false);
            samplesWritten = 0;
            _nextSampleTime = Time.time;
        }

        [ContextMenu("Stop Integrated Recording")]
        public void StopRecording()
        {
            if (_writer == null) return;
            _writer.Flush();
            _writer.Dispose();
            _writer = null;
        }

        private void WriteSample()
        {
            PhysicalAIObservation physicalObs = physicalObservationBuilder != null ? physicalObservationBuilder.Build() : new PhysicalAIObservation();
            PhysicalAIAction physicalAction = manualInput != null ? PhysicalAIAction.FromManualInput(manualInput) : new PhysicalAIAction();
            SensorAIObservation sensorObs = sensorObservationBuilder != null ? sensorObservationBuilder.Build() : new SensorAIObservation();
            SensorAIAction sensorAction = sensorRuntime != null ? sensorRuntime.lastHumanAction : new SensorAIAction();

            var sample = new IntegratedAISample
            {
                time = Time.time,
                frame = Time.frameCount,
                sessionId = sessionId,
                testerId = testerId,
                missionId = missionId,
                source = source,
                tag = runTag,
                physicalObservation = physicalObs.ToArrayCopy(),
                physicalAction = physicalAction.ToArray(),
                sensorObservation = sensorObs.ToArrayCopy(),
                sensorAction = sensorAction.ToArray(),
                physicalReward = rewardTracker != null ? rewardTracker.lastReward : 0f,
                flightMode = physicalRuntime != null ? physicalRuntime.controlMode.ToString() : "unknown",
                sensorMode = sensorRuntime != null ? sensorRuntime.controlMode.ToString() : "unknown"
            };

            _writer.WriteLine(JsonUtility.ToJson(sample, false));
            samplesWritten++;
        }

        public void SetRunTag(string tag)
        {
            runTag = string.IsNullOrWhiteSpace(tag) ? "untagged" : tag;
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "unknown";
            foreach (char c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
            return value.Replace(' ', '_');
        }
    }
}
