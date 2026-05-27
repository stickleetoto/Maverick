using System;
using System.IO;
using UnityEngine;
using EaglePhysicalAI.Aircraft;

namespace EaglePhysicalAI.PhysicalAI
{
    [Serializable]
    public class PhysicalAITrainingSample
    {
        public float time;
        public int frame;
        public string sessionId;
        public string source;
        public float[] observation;
        public float[] action;
        public float reward;
        public string tag;
    }

    /// <summary>
    /// Records observation/action/reward samples for behavior cloning.
    /// This is cleaner than full telemetry and is meant to become the learning dataset.
    /// </summary>
    public class PhysicalAIDemonstrationRecorder : MonoBehaviour
    {
        public bool recordingEnabled = true;
        public bool recordOnlyManualControl = true;
        public float sampleInterval = 0.05f;
        public string testerId = "anonymous";
        public string missionId = "mission_001";
        public string runTag = "untagged";
        public string source = "human";

        [Header("References")]
        public PhysicalAIRuntimeAgent runtimeAgent;
        public PhysicalAIObservationBuilder observationBuilder;
        public ManualAircraftInput manualInput;
        public PhysicalAIRewardTracker rewardTracker;

        public string sessionId;
        public string currentFilePath;
        public int samplesWritten;

        private StreamWriter _writer;
        private float _nextSampleTime;

        private void Awake()
        {
            if (runtimeAgent == null) runtimeAgent = GetComponent<PhysicalAIRuntimeAgent>();
            if (observationBuilder == null) observationBuilder = GetComponent<PhysicalAIObservationBuilder>();
            if (manualInput == null) manualInput = GetComponent<ManualAircraftInput>();
            if (rewardTracker == null) rewardTracker = GetComponent<PhysicalAIRewardTracker>();
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
            if (recordOnlyManualControl && runtimeAgent != null && runtimeAgent.controlMode != PhysicalAIControlMode.Manual) return;
            if (Time.time < _nextSampleTime) return;
            _nextSampleTime = Time.time + sampleInterval;
            WriteSample();
        }

        [ContextMenu("Start Recording")]
        public void StartRecording()
        {
            StopRecording();
            sessionId = $"{Sanitize(testerId)}_{Sanitize(missionId)}_demo_{DateTime.Now:yyyyMMdd_HHmmss}";
            string dir = Path.Combine(Application.persistentDataPath, "EaglePhysicalAILab", "demonstrations");
            Directory.CreateDirectory(dir);
            currentFilePath = Path.Combine(dir, sessionId + ".jsonl");
            _writer = new StreamWriter(currentFilePath, false);
            samplesWritten = 0;
            _nextSampleTime = Time.time;
        }

        [ContextMenu("Stop Recording")]
        public void StopRecording()
        {
            if (_writer == null) return;
            _writer.Flush();
            _writer.Dispose();
            _writer = null;
        }

        private void WriteSample()
        {
            if (observationBuilder == null || manualInput == null) return;
            PhysicalAIObservation observation = observationBuilder.Build();
            PhysicalAIAction action = PhysicalAIAction.FromManualInput(manualInput);

            var sample = new PhysicalAITrainingSample
            {
                time = Time.time,
                frame = Time.frameCount,
                sessionId = sessionId,
                source = source,
                observation = observation.ToArrayCopy(),
                action = action.ToArray(),
                reward = rewardTracker != null ? rewardTracker.lastReward : 0f,
                tag = runTag
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
