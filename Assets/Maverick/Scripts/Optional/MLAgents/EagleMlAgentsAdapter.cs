#if EAGLE_USE_ML_AGENTS
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using EaglePhysicalAI.PhysicalAI;

namespace EaglePhysicalAI.Optional.MLAgents
{
    /// <summary>
    /// Optional ML-Agents bridge. This file is disabled by default.
    /// To enable it: install Unity ML-Agents and add EAGLE_USE_ML_AGENTS to Scripting Define Symbols.
    /// </summary>
    public class EagleMlAgentsAdapter : Agent
    {
        public PhysicalAIObservationBuilder observationBuilder;
        public PhysicalAIRuntimeAgent runtimeAgent;
        public PhysicalAIRewardTracker rewardTracker;
        public PhysicalActionValidator actionValidator;

        private void Awake()
        {
            if (observationBuilder == null) observationBuilder = GetComponent<PhysicalAIObservationBuilder>();
            if (runtimeAgent == null) runtimeAgent = GetComponent<PhysicalAIRuntimeAgent>();
            if (rewardTracker == null) rewardTracker = GetComponent<PhysicalAIRewardTracker>();
            if (actionValidator == null) actionValidator = GetComponent<PhysicalActionValidator>();
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            PhysicalAIObservation obs = observationBuilder != null ? observationBuilder.Build() : new PhysicalAIObservation();
            for (int i = 0; i < PhysicalAIObservation.Count; i++) sensor.AddObservation(obs.values[i]);
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            var continuous = actions.ContinuousActions;
            var action = new PhysicalAIAction
            {
                pitch = continuous[0],
                roll = continuous[1],
                yaw = continuous[2],
                throttle = Mathf.Clamp01((continuous[3] + 1f) * 0.5f),
                strike = Mathf.Clamp01((continuous[4] + 1f) * 0.5f),
                abort = Mathf.Clamp01((continuous[5] + 1f) * 0.5f)
            };

            if (actionValidator != null) action = actionValidator.Validate(action);
            if (runtimeAgent != null) runtimeAgent.ApplyAction(action);
            if (rewardTracker != null) AddReward(rewardTracker.lastReward);
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var continuous = actionsOut.ContinuousActions;
            if (runtimeAgent == null || runtimeAgent.appliedAction == null) return;
            var a = runtimeAgent.appliedAction;
            continuous[0] = a.pitch;
            continuous[1] = a.roll;
            continuous[2] = a.yaw;
            continuous[3] = a.throttle * 2f - 1f;
            continuous[4] = a.strike * 2f - 1f;
            continuous[5] = a.abort * 2f - 1f;
        }
    }
}
#endif
