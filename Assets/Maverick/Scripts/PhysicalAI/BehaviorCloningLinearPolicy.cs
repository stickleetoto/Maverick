using System;
using System.IO;
using UnityEngine;

namespace EaglePhysicalAI.PhysicalAI
{
    [Serializable]
    public class LinearPolicyModel
    {
        public int observationCount = PhysicalAIObservation.Count;
        public int actionCount = PhysicalAIAction.Count;
        public float[] weights;
        public float[] bias;
        public string[] observationLabels = PhysicalAIObservation.Labels;
        public string[] actionLabels = PhysicalAIAction.Labels;
        public string note = "Eagle Physical AI linear behavior cloning policy";
    }

    /// <summary>
    /// Tiny trainable policy: observation vector -> action vector.
    /// This is intentionally simple so it can run in Unity without ML-Agents or Barracuda.
    /// Use it as a first behavior-cloning baseline, not as a final flight AI.
    /// </summary>
    public class BehaviorCloningLinearPolicy : MonoBehaviour
    {
        public TextAsset modelJson;
        public string modelFilePath;
        public bool loadOnStart = true;
        public bool autoInitializeIfMissing = true;
        public int randomSeed = 42;

        [Header("Training")]
        public float learningRate = 0.015f;
        public float l2 = 0.0001f;
        public bool allowOnlineTraining = true;

        [Header("Runtime Debug")]
        public LinearPolicyModel model = new LinearPolicyModel();
        public float lastLoss;
        public int trainedSamples;
        public PhysicalAIAction lastAction = new PhysicalAIAction();

        private System.Random _rng;

        private void Awake()
        {
            _rng = new System.Random(randomSeed);
            if (loadOnStart) LoadModel();
            EnsureModelShape();
        }

        public PhysicalAIAction Predict(PhysicalAIObservation observation)
        {
            EnsureModelShape();
            float[] obs = observation != null ? observation.values : new float[PhysicalAIObservation.Count];
            var outputs = new float[PhysicalAIAction.Count];

            for (int a = 0; a < PhysicalAIAction.Count; a++)
            {
                float z = model.bias[a];
                int offset = a * PhysicalAIObservation.Count;
                for (int i = 0; i < PhysicalAIObservation.Count; i++)
                {
                    z += model.weights[offset + i] * obs[i];
                }

                if (a <= 2) outputs[a] = Tanh(z);              // pitch, roll, yaw
                else outputs[a] = Sigmoid(z);                         // throttle, strike, abort
            }

            lastAction = PhysicalAIAction.FromArray(outputs);
            return lastAction;
        }

        public float TrainSingle(PhysicalAIObservation observation, PhysicalAIAction target)
        {
            if (!allowOnlineTraining || observation == null || target == null) return 0f;
            EnsureModelShape();

            float[] obs = observation.values;
            float[] targetValues = target.Clamp().ToArray();
            float totalLoss = 0f;

            for (int a = 0; a < PhysicalAIAction.Count; a++)
            {
                float z = model.bias[a];
                int offset = a * PhysicalAIObservation.Count;
                for (int i = 0; i < PhysicalAIObservation.Count; i++)
                {
                    z += model.weights[offset + i] * obs[i];
                }

                bool tanhHead = a <= 2;
                float y = tanhHead ? Tanh(z) : Sigmoid(z);
                float error = y - targetValues[a];
                totalLoss += error * error;

                float derivative = tanhHead ? (1f - y * y) : y * (1f - y);
                float gradZ = 2f * error * derivative;

                for (int i = 0; i < PhysicalAIObservation.Count; i++)
                {
                    int wi = offset + i;
                    float grad = gradZ * obs[i] + l2 * model.weights[wi];
                    model.weights[wi] -= learningRate * grad;
                }
                model.bias[a] -= learningRate * gradZ;
            }

            trainedSamples++;
            lastLoss = totalLoss / PhysicalAIAction.Count;
            return lastLoss;
        }

        [ContextMenu("Load Model")]
        public void LoadModel()
        {
            try
            {
                string json = null;
                if (modelJson != null) json = modelJson.text;
                else if (!string.IsNullOrWhiteSpace(modelFilePath) && File.Exists(modelFilePath)) json = File.ReadAllText(modelFilePath);

                if (!string.IsNullOrWhiteSpace(json))
                {
                    var loaded = JsonUtility.FromJson<LinearPolicyModel>(json);
                    if (loaded != null)
                    {
                        model = loaded;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("BehaviorCloningLinearPolicy: failed to load model: " + ex.Message);
            }
            EnsureModelShape();
        }

        [ContextMenu("Save Model")]
        public void SaveModel()
        {
            EnsureModelShape();
            string path = modelFilePath;
            if (string.IsNullOrWhiteSpace(path))
            {
                string dir = Path.Combine(Application.persistentDataPath, "EaglePhysicalAILab", "models");
                Directory.CreateDirectory(dir);
                path = Path.Combine(dir, "linear_policy.json");
                modelFilePath = path;
            }

            File.WriteAllText(path, JsonUtility.ToJson(model, true));
            Debug.Log("BehaviorCloningLinearPolicy: model saved to " + path);
        }

        [ContextMenu("Reset Small Random Model")]
        public void ResetSmallRandomModel()
        {
            _rng = new System.Random(randomSeed);
            model = new LinearPolicyModel();
            EnsureModelShape(true);
            trainedSamples = 0;
            lastLoss = 0f;
        }

        private void EnsureModelShape(bool forceRandom = false)
        {
            if (model == null) model = new LinearPolicyModel();
            model.observationCount = PhysicalAIObservation.Count;
            model.actionCount = PhysicalAIAction.Count;
            model.observationLabels = PhysicalAIObservation.Labels;
            model.actionLabels = PhysicalAIAction.Labels;

            int weightCount = PhysicalAIObservation.Count * PhysicalAIAction.Count;
            if (model.weights == null || model.weights.Length != weightCount || forceRandom)
            {
                model.weights = new float[weightCount];
                if (autoInitializeIfMissing || forceRandom)
                {
                    for (int i = 0; i < model.weights.Length; i++)
                    {
                        model.weights[i] = (float)(_rng.NextDouble() * 2.0 - 1.0) * 0.015f;
                    }
                }
            }

            if (model.bias == null || model.bias.Length != PhysicalAIAction.Count || forceRandom)
            {
                model.bias = new float[PhysicalAIAction.Count];
                model.bias[3] = 0.2f; // mild throttle bias
            }
        }

        private static float Tanh(float x)
        {
            x = Mathf.Clamp(x, -20f, 20f);
            float e2x = Mathf.Exp(2f * x);
            return (e2x - 1f) / (e2x + 1f);
        }

        private static float Sigmoid(float x)
        {
            return 1f / (1f + Mathf.Exp(-Mathf.Clamp(x, -40f, 40f)));
        }
    }
}
