using System.IO;
using UnityEngine;
using EaglePhysicalAI.Sensors.Radar;
using EaglePhysicalAI.Sensors.TargetingPod;

namespace EaglePhysicalAI.Sensors.Fusion
{
    public class SensorLogRecorder : MonoBehaviour
    {
        public F15ERadarSystem radar;
        public TargetingPodSystem targetingPod;
        public SensorFusionManager fusionManager;
        public bool record = true;
        public float logInterval = 0.25f;
        public string folderName = "EaglePhysicalAILab/sensor_sessions";

        public string CurrentPath => _path;

        private string _path;
        private StreamWriter _writer;
        private float _lastLogTime;

        private void Awake()
        {
            if (radar == null) radar = GetComponent<F15ERadarSystem>();
            if (targetingPod == null) targetingPod = GetComponent<TargetingPodSystem>();
            if (fusionManager == null) fusionManager = GetComponent<SensorFusionManager>();
            Open();
        }

        private void Update()
        {
            if (!record || _writer == null) return;
            if (Time.time - _lastLogTime < logInterval) return;
            _lastLogTime = Time.time;
            WriteFrame();
        }

        private void OnDestroy()
        {
            Close();
        }

        public void Open()
        {
            string dir = Path.Combine(Application.persistentDataPath, folderName);
            Directory.CreateDirectory(dir);
            _path = Path.Combine(dir, "sensor_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".jsonl");
            _writer = new StreamWriter(_path, false);
            _writer.AutoFlush = true;
        }

        public void Close()
        {
            if (_writer != null)
            {
                _writer.Flush();
                _writer.Close();
                _writer = null;
            }
        }

        private void WriteFrame()
        {
            var frame = new SensorTelemetryFrame
            {
                time = Time.time,
                radarMode = radar != null ? radar.mode.ToString() : "none",
                radarTrackCount = radar != null ? radar.tracks.Count : 0,
                selectedTrack = radar != null && radar.selectedTrack != null ? radar.selectedTrack.displayName : "none",
                lockedTrack = radar != null && radar.lockedTrack != null ? radar.lockedTrack.displayName : "none",
                podMode = targetingPod != null ? targetingPod.mode.ToString() : "none",
                podTrackedTarget = targetingPod != null && targetingPod.trackedTarget != null ? targetingPod.trackedTarget.name : "none",
                podTrackQuality = targetingPod != null ? targetingPod.targetTrackQuality : 0f,
                podIdentificationConfidence = targetingPod != null ? targetingPod.identificationConfidence : 0f,
                fusedTarget = fusionManager != null && fusionManager.fusedBestGroundUnit != null ? fusionManager.fusedBestGroundUnit.unitId : "none",
                fusedConfidence = fusionManager != null ? fusionManager.fusedConfidence : 0f,
                fusedReason = fusionManager != null ? fusionManager.fusedReason : "none",
                aircraftPosition = transform.position
            };
            _writer.WriteLine(JsonUtility.ToJson(frame));
        }
    }
}
