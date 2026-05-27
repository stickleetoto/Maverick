using System.IO;
using UnityEngine;
using EaglePhysicalAI.Aircraft;
using EaglePhysicalAI.PhysicalAI;
using EaglePhysicalAI.SensorAI;
using EaglePhysicalAI.Mission;
using EaglePhysicalAI.Data;
using EaglePhysicalAI.Maverick;

namespace EaglePhysicalAI.Training
{
    /// <summary>
    /// Runs repeated short training/evaluation episodes inside Unity. Useful before Codex/ML-Agents integration.
    /// </summary>
    public class AutonomousTrainingSessionRunner : MonoBehaviour
    {
        public bool autoRun;
        public KeyCode toggleRunKey = KeyCode.Insert;
        public float timeScaleWhenRunning = 3f;
        public float episodeDurationSeconds = 90f;
        public int maxEpisodes = 20;
        public PhysicalAIControlMode physicalMode = PhysicalAIControlMode.RulePilot;
        public SensorAIControlMode sensorMode = SensorAIControlMode.RuleSensor;

        [Header("References")]
        public EpisodeResetManager resetManager;
        public AircraftPhysicsController aircraft;
        public PhysicalAIRuntimeAgent physicalAgent;
        public SensorAIRuntimeAgent sensorAgent;
        public CurriculumMissionManager curriculum;
        public RunMetricsRecorder runMetrics;
        public MaverickDatasetManifestWriter manifestWriter;

        [Header("State")]
        public int episodeIndex;
        public float episodeStartTime;
        public bool running;
        public string lastSummaryPath = "none";

        private string _summaryFolder;

        private void Awake()
        {
            ResolveReferences();
            _summaryFolder = Path.Combine(Application.persistentDataPath, MaverickProjectInfo.PersistentRootFolder, "training_summaries");
            Directory.CreateDirectory(_summaryFolder);
        }

        private void Start()
        {
            if (autoRun) StartSession();
        }

        private void Update()
        {
            if (MaverickInput.GetKeyDown(toggleRunKey))
            {
                if (running) StopSession("manual_toggle_stop");
                else StartSession();
            }

            if (!running) return;
            Time.timeScale = timeScaleWhenRunning;

            if (Time.time - episodeStartTime >= episodeDurationSeconds || (aircraft != null && aircraft.IsCrashed))
            {
                EndEpisode(aircraft != null && aircraft.IsCrashed ? "crash" : "duration_complete");
            }
        }

        public void StartSession()
        {
            ResolveReferences();
            running = true;
            episodeIndex = 0;
            StartNextEpisode("session_start");
        }

        public void StopSession(string reason)
        {
            running = false;
            Time.timeScale = 1f;
            if (manifestWriter != null) manifestWriter.WriteManifest(reason);
        }

        private void StartNextEpisode(string reason)
        {
            if (maxEpisodes > 0 && episodeIndex >= maxEpisodes)
            {
                StopSession("max_episodes_complete");
                return;
            }

            if (resetManager != null) resetManager.ResetEpisode(reason);
            if (physicalAgent != null) physicalAgent.SetMode(physicalMode);
            if (sensorAgent != null) sensorAgent.SetMode(sensorMode);
            if (curriculum != null) { curriculum.stage = CurriculumStage.FreeFlight; curriculum.stageTime = 0f; curriculum.missionTime = 0f; curriculum.stageProgress = 0f; curriculum.stageNote = "episode_reset"; curriculum.missionFailed = false; curriculum.missionComplete = false; }
            episodeStartTime = Time.time;
            episodeIndex++;
        }

        private void EndEpisode(string reason)
        {
            WriteEpisodeSummary(reason);
            StartNextEpisode(reason);
        }

        private void WriteEpisodeSummary(string reason)
        {
            string path = Path.Combine(_summaryFolder, "episode_" + System.DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + ".json");
            string stage = curriculum != null ? curriculum.stage.ToString() : "unknown";
            string json = "{\n" +
                          "  \"project\": \"" + MaverickProjectInfo.ProjectName + "\",\n" +
                          "  \"version\": \"" + MaverickProjectInfo.Version + "\",\n" +
                          "  \"episode_index\": " + episodeIndex + ",\n" +
                          "  \"reason\": \"" + Escape(reason) + "\",\n" +
                          "  \"stage\": \"" + Escape(stage) + "\",\n" +
                          "  \"duration\": " + Mathf.Max(0f, Time.time - episodeStartTime).ToString("0.000") + ",\n" +
                          "  \"aircraft_crashed\": " + ((aircraft != null && aircraft.IsCrashed) ? "true" : "false") + "\n" +
                          "}\n";
            File.WriteAllText(path, json);
            lastSummaryPath = path;
        }

        private static string Escape(string s)
        {
            return (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private void ResolveReferences()
        {
            if (resetManager == null) resetManager = GetComponent<EpisodeResetManager>();
            if (aircraft == null) aircraft = GetComponent<AircraftPhysicsController>();
            if (physicalAgent == null) physicalAgent = GetComponent<PhysicalAIRuntimeAgent>();
            if (sensorAgent == null) sensorAgent = GetComponent<SensorAIRuntimeAgent>();
            if (curriculum == null) curriculum = GetComponent<CurriculumMissionManager>();
            if (runMetrics == null) runMetrics = GetComponent<RunMetricsRecorder>();
            if (manifestWriter == null) manifestWriter = GetComponent<MaverickDatasetManifestWriter>();
        }
    }
}
