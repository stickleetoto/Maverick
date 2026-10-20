using System;
using System.IO;
using UnityEngine;
using EaglePhysicalAI.Aircraft;

namespace EaglePhysicalAI.FAM
{
    /// <summary>
    /// Passive 4 Hz recorder for player-flown MAVERICK sessions.
    /// It never writes aircraft controls and deliberately excludes weapon/target data.
    /// Output is JSONL suitable for offline FAM dataset conversion and auditing.
    /// </summary>
    public class FamFlightRecorder : MonoBehaviour
    {
        [Header("Recording")]
        public bool recordingEnabled;
        public bool startRecordingOnEnable;
        public bool recordOnlyWhenManualInputEnabled = true;
        public float sampleInterval = 0.25f;
        public KeyCode toggleKey = KeyCode.F8;
        public string pilotId = "anonymous";
        public string scenarioId = "free_flight";

        [Header("References")]
        public AircraftStateSensor playerSensor;
        public ManualAircraftInput manualInput;
        public AircraftStateSensor leaderSensor;
        public FamBehaviorLabeler behaviorLabeler;

        [Header("Debug")]
        public string sessionId;
        public string currentFilePath;
        public int samplesWritten;
        public string lastToken;

        private StreamWriter _writer;
        private float _nextSampleTime;
        private float _lastSampleTime;
        private float _lastThrottle;

        private void Awake()
        {
            if (playerSensor == null) playerSensor = GetComponent<AircraftStateSensor>();
            if (playerSensor == null) playerSensor = FindObjectOfType<AircraftStateSensor>();
            if (manualInput == null && playerSensor != null)
                manualInput = playerSensor.GetComponent<ManualAircraftInput>();
            if (manualInput == null) manualInput = FindObjectOfType<ManualAircraftInput>();
            if (behaviorLabeler == null) behaviorLabeler = GetComponent<FamBehaviorLabeler>();
            if (behaviorLabeler == null) behaviorLabeler = gameObject.AddComponent<FamBehaviorLabeler>();
            _lastThrottle = manualInput != null ? manualInput.lastThrottle : 0f;
        }

        private void OnEnable()
        {
            if (startRecordingOnEnable) StartRecording();
        }

        private void OnDisable()
        {
            StopRecording();
        }

        private void Update()
        {
            if (MaverickInput.GetKeyDown(toggleKey))
            {
                if (_writer == null) StartRecording();
                else StopRecording();
            }

            if (_writer == null || !recordingEnabled) return;
            if (recordOnlyWhenManualInputEnabled && (manualInput == null || !manualInput.inputEnabled)) return;
            if (playerSensor == null || Time.unscaledTime < _nextSampleTime) return;

            WriteSample();
            _nextSampleTime = Time.unscaledTime + Mathf.Max(0.02f, sampleInterval);
        }

        [ContextMenu("Start FAM Flight Recording")]
        public void StartRecording()
        {
            StopRecording();
            recordingEnabled = true;
            sessionId = $"{Sanitize(pilotId)}_{Sanitize(scenarioId)}_fam_{DateTime.Now:yyyyMMdd_HHmmss}";
            string dir = Path.Combine(Application.persistentDataPath, "EaglePhysicalAILab", "fam_flight");
            Directory.CreateDirectory(dir);
            currentFilePath = Path.Combine(dir, sessionId + ".jsonl");
            _writer = new StreamWriter(currentFilePath, false);
            samplesWritten = 0;
            _nextSampleTime = Time.unscaledTime;
            _lastSampleTime = Time.unscaledTime;
            _lastThrottle = manualInput != null ? manualInput.lastThrottle : 0f;
        }

        [ContextMenu("Stop FAM Flight Recording")]
        public void StopRecording()
        {
            recordingEnabled = false;
            if (_writer == null) return;
            _writer.Flush();
            _writer.Dispose();
            _writer = null;
        }

        private void WriteSample()
        {
            AircraftStateSnapshot ego = playerSensor.Capture();
            float now = Time.unscaledTime;
            float dt = samplesWritten == 0 ? sampleInterval : Mathf.Max(0.0001f, now - _lastSampleTime);

            var control = new FamControlInput
            {
                pitch = manualInput != null ? manualInput.lastPitch : ego.pitchInput,
                roll = manualInput != null ? manualInput.lastRoll : ego.rollInput,
                yaw = manualInput != null ? manualInput.lastYaw : ego.yawInput,
                throttle = manualInput != null ? manualInput.lastThrottle : ego.throttle,
            };
            control.throttleDelta = (control.throttle - _lastThrottle) / dt;

            FamLeaderState leader = BuildLeaderState(ego);
            FamBehaviorToken token = behaviorLabeler != null
                ? behaviorLabeler.Label(ego, control, leader)
                : FamBehaviorToken.CONTINUE;

            var frame = new FamFlightFrame
            {
                sessionId = sessionId,
                step = samplesWritten,
                time = now,
                dt = dt,
                behaviorToken = token.ToString(),
                ego = ToFamState(ego),
                control = control,
                leader = leader
            };

            _writer.WriteLine(JsonUtility.ToJson(frame, false));
            samplesWritten++;
            lastToken = frame.behaviorToken;
            _lastSampleTime = now;
            _lastThrottle = control.throttle;
        }

        private FamLeaderState BuildLeaderState(AircraftStateSnapshot ego)
        {
            var result = new FamLeaderState();
            if (leaderSensor == null || leaderSensor == playerSensor) return result;

            AircraftStateSnapshot leader = leaderSensor.Capture();
            Vector3 relWorld = leader.position - ego.position;
            Vector3 relLocal = playerSensor.transform.InverseTransformDirection(relWorld);
            Vector3 relVelocityWorld = leader.velocity - ego.velocity;
            Vector3 relVelocityLocal = playerSensor.transform.InverseTransformDirection(relVelocityWorld);
            float distance = relWorld.magnitude;
            float closingRate = distance > 0.001f
                ? -Vector3.Dot(relVelocityWorld, relWorld / distance)
                : 0f;

            result.available = true;
            result.relativeRight = relLocal.x;
            result.relativeUp = relLocal.y;
            result.relativeForward = relLocal.z;
            result.relativeVx = relVelocityLocal.x;
            result.relativeVy = relVelocityLocal.y;
            result.relativeVz = relVelocityLocal.z;
            result.distance = distance;
            result.closingRate = closingRate;
            result.headingDeltaDeg = Mathf.DeltaAngle(ego.rotationEuler.y, leader.rotationEuler.y);
            return result;
        }

        private static FamAircraftState ToFamState(AircraftStateSnapshot state)
        {
            return new FamAircraftState
            {
                px = state.position.x,
                py = state.position.y,
                pz = state.position.z,
                rx = state.rotationEuler.x,
                ry = state.rotationEuler.y,
                rz = state.rotationEuler.z,
                vx = state.velocity.x,
                vy = state.velocity.y,
                vz = state.velocity.z,
                speed = state.speed,
                forwardSpeed = state.forwardSpeed,
                altitude = state.altitude,
                angleOfAttack = state.angleOfAttack,
                stallRisk = state.stallRisk,
                isStalling = state.isStalling,
                isCrashed = state.isCrashed
            };
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "unknown";
            foreach (char c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
            return value.Replace(' ', '_');
        }
    }
}
