using UnityEngine;
using EaglePhysicalAI.PhysicalAI;

namespace EaglePhysicalAI.UI
{
    public class PhysicalAIHUD : MonoBehaviour
    {
        public PhysicalAIRuntimeAgent runtimeAgent;
        public PhysicalAIObservationBuilder observationBuilder;
        public BehaviorCloningLinearPolicy policy;
        public OnlineBehaviorCloningTrainer onlineTrainer;
        public PhysicalAIDemonstrationRecorder recorder;
        public PhysicalAIRewardTracker rewardTracker;
        public RunTagger runTagger;
        public bool showHud = true;
        public bool showObservationHead = true;
        public int observationHeadCount = 10;

        private GUIStyle _style;

        private void Awake()
        {
            if (runtimeAgent == null) runtimeAgent = FindObjectOfType<PhysicalAIRuntimeAgent>();
            if (observationBuilder == null) observationBuilder = FindObjectOfType<PhysicalAIObservationBuilder>();
            if (policy == null) policy = FindObjectOfType<BehaviorCloningLinearPolicy>();
            if (onlineTrainer == null) onlineTrainer = FindObjectOfType<OnlineBehaviorCloningTrainer>();
            if (recorder == null) recorder = FindObjectOfType<PhysicalAIDemonstrationRecorder>();
            if (rewardTracker == null) rewardTracker = FindObjectOfType<PhysicalAIRewardTracker>();
            if (runTagger == null) runTagger = FindObjectOfType<RunTagger>();
        }

        private void OnGUI()
        {
            if (!showHud) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = 14
                };
                _style.normal.textColor = Color.white;
            }

            GUI.Box(new Rect(12, 350, 560, 350), BuildText(), _style);
        }

        private string BuildText()
        {
            string text = "Physical AI v0.2\n";
            if (runtimeAgent != null)
            {
                text += $"Mode: {runtimeAgent.controlMode} | Note: {runtimeAgent.lastControlNote}\n";
                var raw = runtimeAgent.rawPolicyAction;
                var applied = runtimeAgent.appliedAction;
                text += $"Raw A: P/R/Y/T/S {raw.pitch:0.00}/{raw.roll:0.00}/{raw.yaw:0.00}/{raw.throttle:0.00}/{raw.strike:0.00}\n";
                text += $"App A: P/R/Y/T/S {applied.pitch:0.00}/{applied.roll:0.00}/{applied.yaw:0.00}/{applied.throttle:0.00}/{applied.strike:0.00}\n";
            }

            if (rewardTracker != null)
            {
                text += $"Reward: last {rewardTracker.lastReward:0.000} | return {rewardTracker.episodeReturn:0.00} | steps {rewardTracker.episodeSteps}\n";
                text += $"Reward parts: {rewardTracker.lastRewardBreakdown}\n";
            }

            if (policy != null)
            {
                text += $"LinearPolicy: samples {policy.trainedSamples} | loss {policy.lastLoss:0.0000} | model {policy.modelFilePath}\n";
            }

            if (onlineTrainer != null)
            {
                text += $"OnlineTrainer: {(onlineTrainer.trainingEnabled ? "ON" : "OFF")} | examples {onlineTrainer.examplesSeen} | smooth loss {onlineTrainer.smoothedLoss:0.0000}\n";
            }

            if (recorder != null)
            {
                text += $"DemoRecorder: {(recorder.recordingEnabled ? "ON" : "OFF")} | samples {recorder.samplesWritten} | tag {recorder.runTag}\n";
                text += $"Demo path: {recorder.currentFilePath}\n";
            }

            if (runTagger != null)
            {
                text += $"RunTag: {runTagger.currentTag} | 1 good / 2 bad / 3 crash / 4 ff-risk / 5 interesting\n";
            }

            if (observationBuilder != null)
            {
                text += $"Obs validation: {observationBuilder.lastValidationReason}\n";
                if (showObservationHead && observationBuilder.lastObservation != null && observationBuilder.lastObservation.values != null)
                {
                    int n = Mathf.Min(observationHeadCount, observationBuilder.lastObservation.values.Length);
                    text += "Obs[0..]: ";
                    for (int i = 0; i < n; i++) text += observationBuilder.lastObservation.values[i].ToString("0.00") + " ";
                    text += "\n";
                }
            }

            text += "Keys: F1 Manual | F2 Rule | F3 LinearPolicy | F4 ShadowPolicy\n";
            return text;
        }
    }
}
