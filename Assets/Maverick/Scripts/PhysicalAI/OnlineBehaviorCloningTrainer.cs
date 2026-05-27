using UnityEngine;
using EaglePhysicalAI.Aircraft;

namespace EaglePhysicalAI.PhysicalAI
{
    /// <summary>
    /// Online supervised learner. While the tester manually flies, this script updates the linear policy
    /// to imitate the tester's current action from the current observation.
    /// </summary>
    public class OnlineBehaviorCloningTrainer : MonoBehaviour
    {
        public bool trainingEnabled;
        public bool trainOnlyWhenManual = true;
        public float trainInterval = 0.05f;
        public bool autoSaveEveryNExamples = true;
        public int saveEvery = 1000;

        [Header("References")]
        public PhysicalAIRuntimeAgent runtimeAgent;
        public PhysicalAIObservationBuilder observationBuilder;
        public BehaviorCloningLinearPolicy policy;
        public ManualAircraftInput manualInput;

        [Header("Stats")]
        public int examplesSeen;
        public float lastLoss;
        public float smoothedLoss;

        private float _nextTrainTime;

        private void Awake()
        {
            if (runtimeAgent == null) runtimeAgent = GetComponent<PhysicalAIRuntimeAgent>();
            if (observationBuilder == null) observationBuilder = GetComponent<PhysicalAIObservationBuilder>();
            if (policy == null) policy = GetComponent<BehaviorCloningLinearPolicy>();
            if (manualInput == null) manualInput = GetComponent<ManualAircraftInput>();
        }

        private void Update()
        {
            if (!trainingEnabled) return;
            if (Time.time < _nextTrainTime) return;
            _nextTrainTime = Time.time + trainInterval;

            if (trainOnlyWhenManual && runtimeAgent != null && runtimeAgent.controlMode != PhysicalAIControlMode.Manual)
            {
                return;
            }

            TrainOneStep();
        }

        public void TrainOneStep()
        {
            if (observationBuilder == null || policy == null || manualInput == null) return;
            PhysicalAIObservation obs = observationBuilder.Build();
            PhysicalAIAction target = PhysicalAIAction.FromManualInput(manualInput);
            lastLoss = policy.TrainSingle(obs, target);
            smoothedLoss = examplesSeen == 0 ? lastLoss : Mathf.Lerp(smoothedLoss, lastLoss, 0.03f);
            examplesSeen++;

            if (autoSaveEveryNExamples && saveEvery > 0 && examplesSeen % saveEvery == 0)
            {
                policy.SaveModel();
            }
        }

        [ContextMenu("Toggle Training")]
        public void ToggleTraining()
        {
            trainingEnabled = !trainingEnabled;
        }
    }
}
