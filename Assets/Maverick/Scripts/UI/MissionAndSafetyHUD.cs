using UnityEngine;
using EaglePhysicalAI.Data;
using EaglePhysicalAI.Mission;
using EaglePhysicalAI.Safety;
using EaglePhysicalAI.Training;

namespace EaglePhysicalAI.UI
{
    public class MissionAndSafetyHUD : MonoBehaviour
    {
        public bool visible = true;
        public KeyCode toggleKey = KeyCode.M;
        public CurriculumMissionManager curriculum;
        public FlightEnvelopeSafetyGuard safetyGuard;
        public DatasetQualityMonitor datasetQuality;
        public RunMetricsRecorder metricsRecorder;
        public PolicyEvaluationRunner evaluationRunner;

        private GUIStyle _style;

        private void Awake()
        {
            if (curriculum == null) curriculum = FindObjectOfType<CurriculumMissionManager>();
            if (safetyGuard == null) safetyGuard = FindObjectOfType<FlightEnvelopeSafetyGuard>();
            if (datasetQuality == null) datasetQuality = FindObjectOfType<DatasetQualityMonitor>();
            if (metricsRecorder == null) metricsRecorder = FindObjectOfType<RunMetricsRecorder>();
            if (evaluationRunner == null) evaluationRunner = FindObjectOfType<PolicyEvaluationRunner>();
        }

        private void Update()
        {
            if (MaverickInput.GetKeyDown(toggleKey)) visible = !visible;
        }

        private void OnGUI()
        {
            if (!visible) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.box);
                _style.alignment = TextAnchor.UpperLeft;
                _style.fontSize = 13;
                _style.wordWrap = true;
            }

            string text = "Eagle v0.6 Mission/Safety\n";
            if (curriculum != null)
            {
                text += "Stage: " + curriculum.stage + "  progress=" + curriculum.stageProgress.ToString("0.00") + "\n";
                text += "Mission: t=" + curriculum.missionTime.ToString("0.0") + " score=" + curriculum.curriculumScore.ToString("0.0") + " note=" + curriculum.stageNote + "\n";
            }
            if (safetyGuard != null)
            {
                text += "Safety: " + safetyGuard.state + " reason=" + safetyGuard.lastReason + " override=" + safetyGuard.overrideActive + "\n";
            }
            if (datasetQuality != null)
            {
                text += "DataQuality: " + datasetQuality.estimatedQualityScore.ToString("0.00") + " " + datasetQuality.qualityNote + "\n";
                text += "ManualRatio=" + datasetQuality.manualRatio.ToString("0.00") + " SensorInteract=" + datasetQuality.sensorInteractionRatio.ToString("0.00") + "\n";
            }
            if (metricsRecorder != null)
            {
                text += "Metrics: " + metricsRecorder.snapshotsWritten + " -> " + metricsRecorder.currentFilePath + "\n";
            }
            if (evaluationRunner != null)
            {
                text += "Eval: running=" + evaluationRunner.running + " ep=" + evaluationRunner.currentEpisode + "/" + evaluationRunner.maxEpisodes + "\n";
            }

            GUI.Box(new Rect(10, 430, 520, 170), text, _style);
        }
    }
}
