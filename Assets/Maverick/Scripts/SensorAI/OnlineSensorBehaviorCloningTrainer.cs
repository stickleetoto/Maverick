using UnityEngine;

namespace EaglePhysicalAI.SensorAI
{
    public class OnlineSensorBehaviorCloningTrainer : MonoBehaviour
    {
        public bool trainingEnabled = true;
        public bool trainOnlyManualMode = true;
        public float trainInterval = 0.1f;

        public SensorAIRuntimeAgent runtimeAgent;
        public SensorAIObservationBuilder observationBuilder;
        public SensorBehaviorCloningLinearPolicy policy;

        [Header("Debug")]
        public float lastLoss;
        public int samplesTrained;

        private float _nextTrainTime;

        private void Awake()
        {
            if (runtimeAgent == null) runtimeAgent = GetComponent<SensorAIRuntimeAgent>();
            if (observationBuilder == null) observationBuilder = GetComponent<SensorAIObservationBuilder>();
            if (policy == null) policy = GetComponent<SensorBehaviorCloningLinearPolicy>();
        }

        private void Update()
        {
            if (!trainingEnabled || policy == null || observationBuilder == null || runtimeAgent == null) return;
            if (trainOnlyManualMode && runtimeAgent.controlMode != SensorAIControlMode.Manual) return;
            if (Time.time < _nextTrainTime) return;
            _nextTrainTime = Time.time + trainInterval;

            SensorAIAction target = runtimeAgent.lastHumanAction;
            if (target == null) return;
            if (IsNoOp(target)) return;

            SensorAIObservation obs = observationBuilder.Build();
            lastLoss = policy.TrainSingle(obs, target);
            samplesTrained++;
        }

        private static bool IsNoOp(SensorAIAction action)
        {
            float[] values = action.ToArray();
            for (int i = 0; i < values.Length; i++)
            {
                if (Mathf.Abs(values[i]) > 0.01f) return false;
            }
            return true;
        }
    }
}
