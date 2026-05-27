using UnityEngine;
using EaglePhysicalAI.Sensors.Fusion;
using EaglePhysicalAI.Sensors.Radar;
using EaglePhysicalAI.Sensors.TargetingPod;

namespace EaglePhysicalAI.SensorAI
{
    public class SensorAIRuntimeAgent : MonoBehaviour
    {
        public SensorAIControlMode controlMode = SensorAIControlMode.Manual;

        [Header("References")]
        public F15ERadarSystem radar;
        public TargetingPodSystem targetingPod;
        public SensorFusionManager fusion;
        public SensorAIObservationBuilder observationBuilder;
        public SensorRuleCasPilot rulePilot;
        public SensorBehaviorCloningLinearPolicy linearPolicy;
        public SensorActionValidator actionValidator;

        [Header("Policy Execution")]
        public float actionThreshold = 0.55f;
        public float discreteActionCooldown = 0.28f;
        public KeyCode manualModeKey = KeyCode.F8;
        public KeyCode ruleModeKey = KeyCode.F9;
        public KeyCode linearModeKey = KeyCode.F10;
        public KeyCode shadowModeKey = KeyCode.F11;

        [Header("Debug")]
        public SensorAIObservation lastObservation = new SensorAIObservation();
        public SensorAIAction rawPolicyAction = new SensorAIAction();
        public SensorAIAction appliedAction = new SensorAIAction();
        public SensorAIAction lastHumanAction = new SensorAIAction();
        public string lastControlNote = "none";

        private float _nextDiscreteActionTime;

        private void Awake()
        {
            if (radar == null) radar = FindObjectOfType<F15ERadarSystem>();
            if (targetingPod == null) targetingPod = FindObjectOfType<TargetingPodSystem>();
            if (fusion == null) fusion = FindObjectOfType<SensorFusionManager>();
            if (observationBuilder == null) observationBuilder = GetComponent<SensorAIObservationBuilder>();
            if (rulePilot == null) rulePilot = GetComponent<SensorRuleCasPilot>();
            if (linearPolicy == null) linearPolicy = GetComponent<SensorBehaviorCloningLinearPolicy>();
            if (actionValidator == null) actionValidator = GetComponent<SensorActionValidator>();
        }

        private void Update()
        {
            if (MaverickInput.GetKeyDown(manualModeKey)) SetMode(SensorAIControlMode.Manual);
            if (MaverickInput.GetKeyDown(ruleModeKey)) SetMode(SensorAIControlMode.RuleSensor);
            if (MaverickInput.GetKeyDown(linearModeKey)) SetMode(SensorAIControlMode.LinearPolicy);
            if (MaverickInput.GetKeyDown(shadowModeKey)) SetMode(SensorAIControlMode.ShadowPolicy);

            lastHumanAction = SensorAIAction.FromKeyboard(radar, targetingPod);

            if (observationBuilder != null) lastObservation = observationBuilder.Build();
            ApplyModeFlags();

            if (controlMode == SensorAIControlMode.LinearPolicy)
            {
                StepLinearPolicy(applyToSensors: true);
            }
            else if (controlMode == SensorAIControlMode.ShadowPolicy)
            {
                StepLinearPolicy(applyToSensors: false);
                lastControlNote = "sensor_shadow_policy_prediction_only";
            }
        }

        public void SetMode(SensorAIControlMode mode)
        {
            controlMode = mode;
            ApplyModeFlags();
            lastControlNote = "sensor_mode_" + mode;
        }

        private void ApplyModeFlags()
        {
            if (rulePilot != null) rulePilot.aiEnabled = controlMode == SensorAIControlMode.RuleSensor;
        }

        private void StepLinearPolicy(bool applyToSensors)
        {
            if (linearPolicy == null) return;
            rawPolicyAction = linearPolicy.Predict(lastObservation);
            SensorAIAction safe = actionValidator != null ? actionValidator.Validate(rawPolicyAction) : rawPolicyAction.Clamp();
            appliedAction = safe;

            if (!applyToSensors) return;
            ApplyAction(safe);
        }

        public void ApplyAction(SensorAIAction action)
        {
            if (action == null) return;
            action.Clamp();
            if (Time.time < _nextDiscreteActionTime)
            {
                lastControlNote = "sensor_action_cooldown";
                return;
            }

            bool didSomething = false;

            if (radar != null)
            {
                if (action.radarModeStep > actionThreshold)
                {
                    radar.CycleMode();
                    didSomething = true;
                    lastControlNote = "policy_cycle_radar_mode";
                }
                if (action.radarNextTrack > actionThreshold)
                {
                    radar.SelectNextTrack();
                    didSomething = true;
                    lastControlNote = "policy_next_radar_track";
                }
                if (action.radarLock > actionThreshold)
                {
                    bool ok = radar.LockSelectedTrack();
                    didSomething = true;
                    lastControlNote = ok ? "policy_radar_lock" : "policy_radar_lock_failed";
                }
                if (action.radarUnlock > actionThreshold)
                {
                    radar.Unlock();
                    didSomething = true;
                    lastControlNote = "policy_radar_unlock";
                }
            }

            if (targetingPod != null)
            {
                if (action.podModeStep > actionThreshold)
                {
                    targetingPod.CycleMode();
                    didSomething = true;
                    lastControlNote = "policy_cycle_pod_mode";
                }
                if (action.podSlaveToRadar > actionThreshold)
                {
                    targetingPod.SlaveToRadarTrack();
                    didSomething = true;
                    lastControlNote = "policy_pod_slave_to_radar";
                }
                if (action.podDesignate > actionThreshold)
                {
                    bool ok = targetingPod.TryDesignateCurrentTarget();
                    didSomething = true;
                    lastControlNote = ok ? "policy_pod_designate" : "policy_pod_designate_failed";
                }
                if (action.podClearTrack > actionThreshold)
                {
                    targetingPod.ClearTrack();
                    didSomething = true;
                    lastControlNote = "policy_pod_clear_track";
                }
            }

            if (didSomething) _nextDiscreteActionTime = Time.time + discreteActionCooldown;
            else lastControlNote = actionValidator != null ? actionValidator.lastSafetyNote : "sensor_policy_hold";
        }
    }
}
