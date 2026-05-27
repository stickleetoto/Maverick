using System;
using System.IO;
using UnityEngine;

namespace EaglePhysicalAI.SensorAI
{
    [Serializable]
    public class SensorLinearPolicyModel
    {
        public int observationCount = SensorAIObservation.Count;
        public int actionCount = SensorAIAction.Count;
        public float[] weights;
        public float[] bias;
        public string[] observationLabels = SensorAIObservation.Labels;
        public string[] actionLabels = SensorAIAction.Labels;
        public string note = "Eagle Physical AI sensor linear behavior cloning policy";
    }

    public class SensorBehaviorCloningLinearPolicy : MonoBehaviour
    {
        public TextAsset modelJson;
        public string modelFilePath;
        public bool loadOnStart = true;
        public bool autoInitializeIfMissing = true;
        public int randomSeed = 71;
        public float learningRate = 0.02f;
        public float l2 = 0.0001f;
        public bool allowOnlineTraining = true;

        [Header("Runtime Debug")]
        public SensorLinearPolicyModel model = new SensorLinearPolicyModel();
        public float lastLoss;
        public int trainedSamples;
        public SensorAIAction lastAction = new SensorAIAction();

        private System.Random _rng;

        private void Awake()
        {
            _rng = new System.Random(randomSeed);
            if (loadOnStart) LoadModel();
            EnsureModelShape();
        }

        public SensorAIAction Predict(SensorAIObservation observation)
        {
            EnsureModelShape();
            float[] obs = observation != null ? observation.values : new float[SensorAIObservation.Count];
            var outputs = new float[SensorAIAction.Count];

            for (int a = 0; a < SensorAIAction.Count; a++)
            {
                float z = model.bias[a];
                int offset = a * SensorAIObservation.Count;
                for (int i = 0; i < SensorAIObservation.Count; i++)
                {
                    z += model.weights[offset + i] * obs[i];
                }

                outputs[a] = a == 0 || a == 4 ? Tanh(z) : Sigmoid(z);
            }

            lastAction = SensorAIAction.FromArray(outputs);
            return lastAction;
        }

        public float TrainSingle(SensorAIObservation observation, SensorAIAction target)
        {
            if (!allowOnlineTraining || observation == null || target == null) return 0f;
            EnsureModelShape();
            float[] obs = observation.values;
            float[] targetValues = target.Clamp().ToArray();
            float totalLoss = 0f;

            for (int a = 0; a < SensorAIAction.Count; a++)
            {
                float z = model.bias[a];
                int offset = a * SensorAIObservation.Count;
                for (int i = 0; i < SensorAIObservation.Count; i++) z += model.weights[offset + i] * obs[i];

                bool tanhHead = a == 0 || a == 4;
                float y = tanhHead ? Tanh(z) : Sigmoid(z);
                float error = y - targetValues[a];
                totalLoss += error * error;
                float derivative = tanhHead ? (1f - y * y) : y * (1f - y);
                float gradZ = 2f * error * derivative;

                for (int i = 0; i < SensorAIObservation.Count; i++)
                {
                    int wi = offset + i;
                    float grad = gradZ * obs[i] + l2 * model.weights[wi];
                    model.weights[wi] -= learningRate * grad;
                }
                model.bias[a] -= learningRate * gradZ;
            }

            trainedSamples++;
            lastLoss = totalLoss / SensorAIAction.Count;
            return lastLoss;
        }

        [ContextMenu("Load Sensor Model")]
        public void LoadModel()
        {
            try
            {
                string json = null;
                if (modelJson != null) json = modelJson.text;
                else if (!string.IsNullOrWhiteSpace(modelFilePath) && File.Exists(modelFilePath)) json = File.ReadAllText(modelFilePath);

                if (!string.IsNullOrWhiteSpace(json))
                {
                    var loaded = JsonUtility.FromJson<SensorLinearPolicyModel>(json);
                    if (loaded != null) model = loaded;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("SensorBehaviorCloningLinearPolicy: failed to load model: " + ex.Message);
            }
            EnsureModelShape();
        }

        [ContextMenu("Save Sensor Model")]
        public void SaveModel()
        {
            EnsureModelShape();
            string path = modelFilePath;
            if (string.IsNullOrWhiteSpace(path))
            {
                string dir = Path.Combine(Application.persistentDataPath, "EaglePhysicalAILab", "models");
                Directory.CreateDirectory(dir);
                path = Path.Combine(dir, "sensor_linear_policy.json");
                modelFilePath = path;
            }

            File.WriteAllText(path, JsonUtility.ToJson(model, true));
            Debug.Log("SensorBehaviorCloningLinearPolicy: model saved to " + path);
        }

        [ContextMenu("Reset Small Random Sensor Model")]
        public void ResetSmallRandomModel()
        {
            _rng = new System.Random(randomSeed);
            model = new SensorLinearPolicyModel();
            EnsureModelShape(true);
            trainedSamples = 0;
            lastLoss = 0f;
        }

        private void EnsureModelShape(bool forceRandom = false)
        {
            if (model == null) model = new SensorLinearPolicyModel();
            model.observationCount = SensorAIObservation.Count;
            model.actionCount = SensorAIAction.Count;
            model.observationLabels = SensorAIObservation.Labels;
            model.actionLabels = SensorAIAction.Labels;

            int weightCount = SensorAIObservation.Count * SensorAIAction.Count;
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

            if (model.bias == null || model.bias.Length != SensorAIAction.Count || forceRandom)
            {
                model.bias = new float[SensorAIAction.Count];
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
