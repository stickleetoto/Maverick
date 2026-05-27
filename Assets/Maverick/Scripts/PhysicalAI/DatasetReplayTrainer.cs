using System;
using System.IO;
using UnityEngine;

namespace EaglePhysicalAI.PhysicalAI
{
    /// <summary>
    /// Offline-in-Unity trainer for JSONL demonstration files written by PhysicalAIDemonstrationRecorder.
    /// Paste a file path into datasetFilePath, then use the context menu "Train From Dataset File".
    /// </summary>
    public class DatasetReplayTrainer : MonoBehaviour
    {
        public BehaviorCloningLinearPolicy policy;
        public string datasetFilePath;
        public int epochs = 3;
        public int maxSamplesPerEpoch = 0;
        public bool saveAfterTraining = true;

        [Header("Stats")]
        public int samplesTrained;
        public float lastLoss;
        public string lastStatus = "idle";

        private void Awake()
        {
            if (policy == null) policy = GetComponent<BehaviorCloningLinearPolicy>();
        }

        [ContextMenu("Train From Dataset File")]
        public void TrainFromDatasetFile()
        {
            if (policy == null)
            {
                lastStatus = "no_policy";
                return;
            }
            if (string.IsNullOrWhiteSpace(datasetFilePath) || !File.Exists(datasetFilePath))
            {
                lastStatus = "dataset_missing";
                Debug.LogWarning("DatasetReplayTrainer: dataset file missing: " + datasetFilePath);
                return;
            }

            samplesTrained = 0;
            try
            {
                for (int e = 0; e < Mathf.Max(1, epochs); e++)
                {
                    int epochSamples = 0;
                    foreach (string line in File.ReadLines(datasetFilePath))
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        var sample = JsonUtility.FromJson<PhysicalAITrainingSample>(line);
                        if (sample == null || sample.observation == null || sample.action == null) continue;

                        var obs = new PhysicalAIObservation { values = NormalizeObservationLength(sample.observation) };
                        var action = PhysicalAIAction.FromArray(sample.action);
                        lastLoss = policy.TrainSingle(obs, action);
                        samplesTrained++;
                        epochSamples++;

                        if (maxSamplesPerEpoch > 0 && epochSamples >= maxSamplesPerEpoch) break;
                    }
                }

                if (saveAfterTraining) policy.SaveModel();
                lastStatus = $"trained_{samplesTrained}_samples_loss_{lastLoss:0.0000}";
                Debug.Log("DatasetReplayTrainer: " + lastStatus);
            }
            catch (Exception ex)
            {
                lastStatus = "error_" + ex.Message;
                Debug.LogWarning("DatasetReplayTrainer: " + ex.Message);
            }
        }

        private static float[] NormalizeObservationLength(float[] source)
        {
            var target = new float[PhysicalAIObservation.Count];
            if (source == null) return target;
            int n = Mathf.Min(source.Length, target.Length);
            Array.Copy(source, target, n);
            return target;
        }
    }
}
