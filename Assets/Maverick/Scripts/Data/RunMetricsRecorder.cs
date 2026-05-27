using System;
using System.IO;
using UnityEngine;
using EaglePhysicalAI.Aircraft;
using EaglePhysicalAI.Battlefield;
using EaglePhysicalAI.CAS;
using EaglePhysicalAI.Mission;
using EaglePhysicalAI.PhysicalAI;
using EaglePhysicalAI.Safety;
using EaglePhysicalAI.SensorAI;
using EaglePhysicalAI.Sensors.Fusion;

namespace EaglePhysicalAI.Data
{
    [Serializable]
    public class RunMetricsSnapshot
    {
        public float time;
        public int frame;
        public string missionId;
        public string stage;
        public string safetyState;
        public string safetyReason;
        public string flightMode;
        public string sensorMode;
        public float altitude;
        public float speed;
        public float stallRisk;
        public float fusedConfidence;
        public int requestsTotal;
        public int successfulStrikes;
        public int abortedStrikes;
        public int friendlyFireIncidents;
        public float rewardReturn;
        public float curriculumScore;
        public bool crashed;
        public bool missionComplete;
        public bool missionFailed;
    }

    /// <summary>
    /// Writes low-frequency run metrics separate from high-frequency demonstrations.
    /// Useful for comparing manual, rule, shadow, and learned policies after playtests.
    /// </summary>
    public class RunMetricsRecorder : MonoBehaviour
    {
        public bool recordingEnabled = true;
        public float sampleInterval = 1.0f;
        public string missionId = "mission_001";

        [Header("References")]
        public AircraftPhysicsController aircraft;
        public PhysicalAIRuntimeAgent physicalRuntime;
        public SensorAIRuntimeAgent sensorRuntime;
        public PhysicalAIRewardTracker rewardTracker;
        public CurriculumMissionManager curriculum;
        public FlightEnvelopeSafetyGuard safetyGuard;
        public SensorFusionManager fusion;
        public CasRequestManager requestManager;
        public AbstractStrikeSystem strikeSystem;

        [Header("Debug")]
        public string currentFilePath;
        public int snapshotsWritten;

        private StreamWriter _writer;
        private float _nextSampleTime;

        private void Awake()
        {
            if (aircraft == null) aircraft = FindObjectOfType<AircraftPhysicsController>();
            if (physicalRuntime == null) physicalRuntime = FindObjectOfType<PhysicalAIRuntimeAgent>();
            if (sensorRuntime == null) sensorRuntime = FindObjectOfType<SensorAIRuntimeAgent>();
            if (rewardTracker == null) rewardTracker = FindObjectOfType<PhysicalAIRewardTracker>();
            if (curriculum == null) curriculum = FindObjectOfType<CurriculumMissionManager>();
            if (safetyGuard == null) safetyGuard = FindObjectOfType<FlightEnvelopeSafetyGuard>();
            if (fusion == null) fusion = FindObjectOfType<SensorFusionManager>();
            if (requestManager == null) requestManager = FindObjectOfType<CasRequestManager>();
            if (strikeSystem == null) strikeSystem = FindObjectOfType<AbstractStrikeSystem>();
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
            if (Time.time < _nextSampleTime) return;
            _nextSampleTime = Time.time + sampleInterval;
            WriteSnapshot();
        }

        public void StartRecording()
        {
            string dir = Path.Combine(Application.persistentDataPath, "EaglePhysicalAILab", "run_metrics");
            Directory.CreateDirectory(dir);
            string file = "run_metrics_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".jsonl";
            currentFilePath = Path.Combine(dir, file);
            _writer = new StreamWriter(currentFilePath, append: false);
            snapshotsWritten = 0;
        }

        public void StopRecording()
        {
            if (_writer == null) return;
            WriteSnapshot();
            _writer.Flush();
            _writer.Dispose();
            _writer = null;
        }

        public void WriteSnapshot()
        {
            if (_writer == null) return;
            var s = new RunMetricsSnapshot
            {
                time = Time.time,
                frame = Time.frameCount,
                missionId = missionId,
                stage = curriculum != null ? curriculum.stage.ToString() : "none",
                safetyState = safetyGuard != null ? safetyGuard.state.ToString() : "none",
                safetyReason = safetyGuard != null ? safetyGuard.lastReason : "none",
                flightMode = physicalRuntime != null ? physicalRuntime.controlMode.ToString() : "none",
                sensorMode = sensorRuntime != null ? sensorRuntime.controlMode.ToString() : "none",
                altitude = aircraft != null ? aircraft.Altitude : 0f,
                speed = aircraft != null ? aircraft.Speed : 0f,
                stallRisk = aircraft != null ? aircraft.StallRisk : 0f,
                fusedConfidence = fusion != null ? fusion.fusedConfidence : 0f,
                requestsTotal = requestManager != null ? requestManager.requests.Count : 0,
                successfulStrikes = strikeSystem != null ? strikeSystem.successfulStrikes : 0,
                abortedStrikes = strikeSystem != null ? strikeSystem.abortedStrikes : 0,
                friendlyFireIncidents = strikeSystem != null ? strikeSystem.friendlyFireIncidents : 0,
                rewardReturn = rewardTracker != null ? rewardTracker.episodeReturn : 0f,
                curriculumScore = curriculum != null ? curriculum.curriculumScore : 0f,
                crashed = aircraft != null && aircraft.IsCrashed,
                missionComplete = curriculum != null && curriculum.missionComplete,
                missionFailed = curriculum != null && curriculum.missionFailed
            };
            _writer.WriteLine(JsonUtility.ToJson(s));
            snapshotsWritten++;
        }
    }
}
