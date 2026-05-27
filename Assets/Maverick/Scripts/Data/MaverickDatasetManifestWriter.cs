using System;
using System.IO;
using UnityEngine;
using EaglePhysicalAI.Maverick;
using EaglePhysicalAI.Aircraft;
using EaglePhysicalAI.PhysicalAI;
using EaglePhysicalAI.SensorAI;
using EaglePhysicalAI.Mission;

namespace EaglePhysicalAI.Data
{
    /// <summary>
    /// Writes a small manifest next to collected data so later Python tools know what version/profile/mode created it.
    /// </summary>
    public class MaverickDatasetManifestWriter : MonoBehaviour
    {
        public string folderName = "maverick_manifests";
        public bool writeOnStart = true;
        public bool writeOnApplicationQuit = true;

        [Header("References")]
        public AircraftProfileApplier profileApplier;
        public PhysicalAIRuntimeAgent physicalAgent;
        public SensorAIRuntimeAgent sensorAgent;
        public CurriculumMissionManager curriculum;
        public DatasetQualityMonitor datasetQuality;
        public string lastManifestPath = "none";

        private string _sessionId;
        private string _folder;
        private int _writeCount;

        private void Awake()
        {
            ResolveReferences();
            _sessionId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + "_" + UnityEngine.Random.Range(1000, 9999);
            _folder = Path.Combine(Application.persistentDataPath, MaverickProjectInfo.PersistentRootFolder, folderName);
            Directory.CreateDirectory(_folder);
        }

        private void Start()
        {
            if (writeOnStart) WriteManifest("session_start");
        }

        private void OnApplicationQuit()
        {
            if (writeOnApplicationQuit) WriteManifest("session_quit");
        }

        [ContextMenu("Write Dataset Manifest")]
        public void WriteManifestFromContext()
        {
            WriteManifest("manual_context_menu");
        }

        public string WriteManifest(string reason)
        {
            ResolveReferences();
            string path = Path.Combine(_folder, _sessionId + "_manifest.json");
            _writeCount++;
            string json = BuildJson(reason);
            File.WriteAllText(path, json);
            lastManifestPath = path;
            return path;
        }

        private string BuildJson(string reason)
        {
            string profile = profileApplier != null ? profileApplier.lastAppliedProfile : "unknown";
            string physicalMode = physicalAgent != null ? physicalAgent.controlMode.ToString() : "unknown";
            string sensorMode = sensorAgent != null ? sensorAgent.controlMode.ToString() : "unknown";
            string stage = curriculum != null ? curriculum.stage.ToString() : "unknown";
            string quality = datasetQuality != null ? datasetQuality.qualityNote : "unknown";
            return "{\n" +
                   JsonLine("project", MaverickProjectInfo.ProjectName, true) +
                   JsonLine("version", MaverickProjectInfo.Version, true) +
                   JsonLine("schema", MaverickProjectInfo.DatasetSchema, true) +
                   JsonLine("session_id", _sessionId, true) +
                   JsonLine("reason", reason, true) +
                   JsonLine("utc", DateTime.UtcNow.ToString("o"), true) +
                   JsonLine("profile", profile, true) +
                   JsonLine("physical_mode", physicalMode, true) +
                   JsonLine("sensor_mode", sensorMode, true) +
                   JsonLine("curriculum_stage", stage, true) +
                   JsonLine("dataset_quality", quality, true) +
                   "  \"write_count\": " + _writeCount + "\n" +
                   "}\n";
        }

        private static string JsonLine(string key, string value, bool comma)
        {
            return "  \"" + Escape(key) + "\": \"" + Escape(value) + "\"" + (comma ? "," : "") + "\n";
        }

        private static string Escape(string s)
        {
            return (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
        }

        private void ResolveReferences()
        {
            if (profileApplier == null) profileApplier = GetComponent<AircraftProfileApplier>();
            if (physicalAgent == null) physicalAgent = GetComponent<PhysicalAIRuntimeAgent>();
            if (sensorAgent == null) sensorAgent = GetComponent<SensorAIRuntimeAgent>();
            if (curriculum == null) curriculum = GetComponent<CurriculumMissionManager>();
            if (datasetQuality == null) datasetQuality = GetComponent<DatasetQualityMonitor>();
        }
    }
}
