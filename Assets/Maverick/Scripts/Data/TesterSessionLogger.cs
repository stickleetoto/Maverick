using System;
using System.IO;
using UnityEngine;
using EaglePhysicalAI.AI;
using EaglePhysicalAI.Aircraft;
using EaglePhysicalAI.Battlefield;
using EaglePhysicalAI.CAS;
using EaglePhysicalAI.Mission;

namespace EaglePhysicalAI.Data
{
    /// <summary>
    /// Writes tester gameplay as JSONL. Use this data later for behavior cloning or analysis.
    /// File path: Application.persistentDataPath/EaglePhysicalAILab/sessions/{sessionId}.jsonl
    /// </summary>
    public class TesterSessionLogger : MonoBehaviour
    {
        public string testerId = "anonymous";
        public string missionId = "mission_001";
        public bool loggingEnabled = true;
        public float sampleInterval = 0.1f;

        [Header("References")]
        public AircraftStateSensor sensor;
        public ManualAircraftInput manualInput;
        public RuleCasPilot aiPilot;
        public CasRequestManager requestManager;
        public CasValidator validator;
        public AbstractStrikeSystem strikeSystem;
        public MissionScoreTracker scoreTracker;

        public string sessionId;
        public string currentFilePath;

        private StreamWriter _writer;
        private float _nextSampleTime;

        private void Awake()
        {
            if (sensor == null) sensor = GetComponent<AircraftStateSensor>();
            if (manualInput == null) manualInput = GetComponent<ManualAircraftInput>();
            if (aiPilot == null) aiPilot = GetComponent<RuleCasPilot>();
            if (strikeSystem == null) strikeSystem = GetComponent<AbstractStrikeSystem>();
            if (requestManager == null) requestManager = FindObjectOfType<CasRequestManager>();
            if (validator == null) validator = FindObjectOfType<CasValidator>();
            if (scoreTracker == null) scoreTracker = FindObjectOfType<MissionScoreTracker>();
        }

        private void OnEnable()
        {
            if (loggingEnabled) StartSession();
        }

        private void OnDisable()
        {
            EndSession();
        }

        private void Update()
        {
            if (!loggingEnabled || _writer == null) return;
            if (Time.time < _nextSampleTime) return;
            _nextSampleTime = Time.time + sampleInterval;
            WriteFrame();
        }

        public void StartSession()
        {
            EndSession();
            sessionId = $"{Sanitize(testerId)}_{Sanitize(missionId)}_{DateTime.Now:yyyyMMdd_HHmmss}";
            string dir = Path.Combine(Application.persistentDataPath, "EaglePhysicalAILab", "sessions");
            Directory.CreateDirectory(dir);
            currentFilePath = Path.Combine(dir, sessionId + ".jsonl");
            _writer = new StreamWriter(currentFilePath, false);
            _nextSampleTime = Time.time;
        }

        public void EndSession()
        {
            if (_writer == null) return;
            _writer.Flush();
            _writer.Dispose();
            _writer = null;
        }

        private void WriteFrame()
        {
            if (sensor == null) return;
            TelemetryFrame frame = BuildFrame();
            string json = JsonUtility.ToJson(frame, false);
            _writer.WriteLine(json);
            _writer.Flush();
        }

        private TelemetryFrame BuildFrame()
        {
            var snapshot = sensor.Capture();
            var frame = new TelemetryFrame
            {
                time = Time.time,
                frame = Time.frameCount,
                sessionId = sessionId
            };

            frame.aircraft.px = snapshot.position.x;
            frame.aircraft.py = snapshot.position.y;
            frame.aircraft.pz = snapshot.position.z;
            frame.aircraft.rx = snapshot.rotationEuler.x;
            frame.aircraft.ry = snapshot.rotationEuler.y;
            frame.aircraft.rz = snapshot.rotationEuler.z;
            frame.aircraft.vx = snapshot.velocity.x;
            frame.aircraft.vy = snapshot.velocity.y;
            frame.aircraft.vz = snapshot.velocity.z;
            frame.aircraft.speed = snapshot.speed;
            frame.aircraft.forwardSpeed = snapshot.forwardSpeed;
            frame.aircraft.altitude = snapshot.altitude;
            frame.aircraft.angleOfAttack = snapshot.angleOfAttack;
            frame.aircraft.stallRisk = snapshot.stallRisk;
            frame.aircraft.isStalling = snapshot.isStalling;
            frame.aircraft.isCrashed = snapshot.isCrashed;
            frame.aircraft.pitchInput = snapshot.pitchInput;
            frame.aircraft.rollInput = snapshot.rollInput;
            frame.aircraft.yawInput = snapshot.yawInput;
            frame.aircraft.throttle = snapshot.throttle;

            CasRequest request = requestManager != null ? requestManager.activeRequest : null;
            frame.cas.requestActive = request != null && request.active;
            frame.cas.requestId = request != null ? request.requestId : "";
            frame.cas.targetId = request?.target != null ? request.target.unitId : "";
            frame.cas.targetAlive = request?.target != null && request.target.isAlive;
            frame.cas.targetDistance = request?.target != null ? Vector3.Distance(transform.position, request.target.transform.position) : -1f;

            if (validator != null && request?.target != null)
            {
                var result = validator.ValidateStrike(transform, request.target);
                frame.cas.validationReason = result.reason;
                frame.cas.strikeAllowed = result.allowed;
                frame.cas.friendlyRisk = result.friendlyRisk;
                frame.cas.geometryScore = result.geometryScore;
            }

            if (manualInput != null)
            {
                frame.playerAction.pitchInput = manualInput.lastPitch;
                frame.playerAction.rollInput = manualInput.lastRoll;
                frame.playerAction.yawInput = manualInput.lastYaw;
                frame.playerAction.throttleInput = manualInput.lastThrottle;
                frame.playerAction.strikePressed = manualInput.strikePressed;
                frame.playerAction.confirmPressed = manualInput.confirmPressed;
                frame.playerAction.abortPressed = manualInput.abortPressed;
            }

            frame.playerAction.selectedIntent = aiPilot != null ? aiPilot.currentIntent.ToString() : "Manual";

            frame.outcome.missionScore = scoreTracker != null ? scoreTracker.score : 0f;
            frame.outcome.successfulStrikes = strikeSystem != null ? strikeSystem.successfulStrikes : 0;
            frame.outcome.abortedStrikes = strikeSystem != null ? strikeSystem.abortedStrikes : 0;
            frame.outcome.friendlyFireIncidents = strikeSystem != null ? strikeSystem.friendlyFireIncidents : 0;
            frame.outcome.crashed = snapshot.isCrashed;

            return frame;
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "unknown";
            foreach (char c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
            return value.Replace(' ', '_');
        }
    }
}
