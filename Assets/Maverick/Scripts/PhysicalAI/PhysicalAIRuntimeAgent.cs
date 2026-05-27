using UnityEngine;
using EaglePhysicalAI.Aircraft;
using EaglePhysicalAI.AI;
using EaglePhysicalAI.Battlefield;
using EaglePhysicalAI.CAS;

namespace EaglePhysicalAI.PhysicalAI
{
    /// <summary>
    /// Runtime owner for physical AI control. It switches between manual, old rule pilot,
    /// linear behavior-cloning policy, and shadow mode for safe testing.
    /// </summary>
    [RequireComponent(typeof(AircraftPhysicsController))]
    public class PhysicalAIRuntimeAgent : MonoBehaviour
    {
        public PhysicalAIControlMode controlMode = PhysicalAIControlMode.Manual;

        [Header("References")]
        public AircraftPhysicsController aircraft;
        public ManualAircraftInput manualInput;
        public WaypointAutopilot autopilot;
        public RuleCasPilot rulePilot;
        public PhysicalAIObservationBuilder observationBuilder;
        public BehaviorCloningLinearPolicy linearPolicy;
        public PhysicalActionValidator actionValidator;
        public AbstractStrikeSystem strikeSystem;
        public CasRequestManager requestManager;
        public PhysicalAIRewardTracker rewardTracker;

        [Header("Policy Execution")]
        public float strikeThreshold = 0.75f;
        public float actionSmoothing = 8f;
        public bool allowPolicyStrike = true;
        public KeyCode manualModeKey = KeyCode.F1;
        public KeyCode ruleModeKey = KeyCode.F2;
        public KeyCode linearModeKey = KeyCode.F3;
        public KeyCode shadowModeKey = KeyCode.F4;

        [Header("Debug")]
        public PhysicalAIObservation lastObservation = new PhysicalAIObservation();
        public PhysicalAIAction rawPolicyAction = new PhysicalAIAction();
        public PhysicalAIAction appliedAction = new PhysicalAIAction();
        public string lastControlNote = "none";

        private float _smoothedPitch;
        private float _smoothedRoll;
        private float _smoothedYaw;
        private float _smoothedThrottle = 0.65f;
        private float _lastStrikeTime;

        private void Awake()
        {
            if (aircraft == null) aircraft = GetComponent<AircraftPhysicsController>();
            if (manualInput == null) manualInput = GetComponent<ManualAircraftInput>();
            if (autopilot == null) autopilot = GetComponent<WaypointAutopilot>();
            if (rulePilot == null) rulePilot = GetComponent<RuleCasPilot>();
            if (observationBuilder == null) observationBuilder = GetComponent<PhysicalAIObservationBuilder>();
            if (linearPolicy == null) linearPolicy = GetComponent<BehaviorCloningLinearPolicy>();
            if (actionValidator == null) actionValidator = GetComponent<PhysicalActionValidator>();
            if (strikeSystem == null) strikeSystem = GetComponent<AbstractStrikeSystem>();
            if (requestManager == null) requestManager = FindObjectOfType<CasRequestManager>();
            if (rewardTracker == null) rewardTracker = GetComponent<PhysicalAIRewardTracker>();
            _smoothedThrottle = aircraft != null ? aircraft.targetThrottle : 0.65f;
        }

        private void Update()
        {
            if (MaverickInput.GetKeyDown(manualModeKey)) SetMode(PhysicalAIControlMode.Manual);
            if (MaverickInput.GetKeyDown(ruleModeKey)) SetMode(PhysicalAIControlMode.RulePilot);
            if (MaverickInput.GetKeyDown(linearModeKey)) SetMode(PhysicalAIControlMode.LinearPolicy);
            if (MaverickInput.GetKeyDown(shadowModeKey)) SetMode(PhysicalAIControlMode.ShadowPolicy);
        }

        private void FixedUpdate()
        {
            ApplyModeFlags();
            if (observationBuilder != null) lastObservation = observationBuilder.Build();

            if (controlMode == PhysicalAIControlMode.LinearPolicy)
            {
                StepLinearPolicy(applyToAircraft: true);
            }
            else if (controlMode == PhysicalAIControlMode.ShadowPolicy)
            {
                StepLinearPolicy(applyToAircraft: false);
                lastControlNote = "shadow_policy_prediction_only";
            }
        }

        public void SetMode(PhysicalAIControlMode mode)
        {
            controlMode = mode;
            ApplyModeFlags();
            lastControlNote = "mode_" + mode;
        }

        private void ApplyModeFlags()
        {
            if (manualInput != null) manualInput.inputEnabled = controlMode == PhysicalAIControlMode.Manual || controlMode == PhysicalAIControlMode.ShadowPolicy;
            if (rulePilot != null) rulePilot.aiEnabled = controlMode == PhysicalAIControlMode.RulePilot;
            if (autopilot != null && controlMode == PhysicalAIControlMode.LinearPolicy)
            {
                autopilot.autopilotEnabled = false;
            }
        }

        private void StepLinearPolicy(bool applyToAircraft)
        {
            if (linearPolicy == null || aircraft == null) return;
            rawPolicyAction = linearPolicy.Predict(lastObservation);
            PhysicalAIAction safe = actionValidator != null ? actionValidator.Validate(rawPolicyAction) : rawPolicyAction.Clamp();

            if (!applyToAircraft)
            {
                appliedAction = safe;
                return;
            }

            ApplyAction(safe);
        }

        public void ApplyAction(PhysicalAIAction action)
        {
            if (aircraft == null || action == null) return;
            action.Clamp();

            float alpha = 1f - Mathf.Exp(-actionSmoothing * Time.fixedDeltaTime);
            _smoothedPitch = Mathf.Lerp(_smoothedPitch, action.pitch, alpha);
            _smoothedRoll = Mathf.Lerp(_smoothedRoll, action.roll, alpha);
            _smoothedYaw = Mathf.Lerp(_smoothedYaw, action.yaw, alpha);
            _smoothedThrottle = Mathf.Lerp(_smoothedThrottle, action.throttle, alpha);

            aircraft.SetControlInputs(_smoothedPitch, _smoothedRoll, _smoothedYaw, _smoothedThrottle);
            appliedAction = new PhysicalAIAction
            {
                pitch = _smoothedPitch,
                roll = _smoothedRoll,
                yaw = _smoothedYaw,
                throttle = _smoothedThrottle,
                strike = action.strike,
                abort = action.abort
            }.Clamp();

            if (allowPolicyStrike && action.strike >= strikeThreshold && Time.time - _lastStrikeTime > 0.25f)
            {
                _lastStrikeTime = Time.time;
                CasRequest request = requestManager != null ? requestManager.activeRequest : null;
                GroundUnit target = request != null ? request.target : null;
                if (strikeSystem != null && target != null)
                {
                    bool fired = strikeSystem.TryStrike(target);
                    lastControlNote = fired ? "policy_strike_executed" : "policy_strike_blocked";
                }
            }
            else
            {
                lastControlNote = actionValidator != null ? actionValidator.lastSafetyNote : "policy_action_applied";
            }
        }
    }
}
