using UnityEngine;
using EaglePhysicalAI.Aircraft;
using EaglePhysicalAI.AI;
using EaglePhysicalAI.Battlefield;
using EaglePhysicalAI.CAS;
using EaglePhysicalAI.Controls;
using EaglePhysicalAI.Data;
using EaglePhysicalAI.Mission;
using EaglePhysicalAI.PhysicalAI;
using EaglePhysicalAI.SensorAI;
using EaglePhysicalAI.Sensors.Fusion;
using EaglePhysicalAI.Sensors.Radar;
using EaglePhysicalAI.Sensors.TargetingPod;
using EaglePhysicalAI.Sensors.UI;
using EaglePhysicalAI.UI;

namespace EaglePhysicalAI.Setup
{
    public class EagleV05IntegratedBootstrap : MonoBehaviour
    {
        [Header("Required")]
        public GameObject aircraftObject;

        [Header("Optional auto setup")]
        public bool addWarThunderControls = true;
        public bool addPhysicalAI = true;
        public bool addSensors = true;
        public bool addSensorAI = true;
        public bool addIntegratedRecorder = true;
        public bool createHud = true;
        public bool createCamera = false;
        public bool autoBindRadarSignatures = true;

        [Header("Created References")]
        public AircraftPhysicsController aircraft;
        public WarThunderMouseAircraftInput warThunderInput;
        public PhysicalAIRuntimeAgent physicalRuntime;
        public SensorAIRuntimeAgent sensorRuntime;
        public F15ERadarSystem radar;
        public TargetingPodSystem targetingPod;
        public SensorFusionManager fusion;
        public CasRequestManager requestManager;
        public AbstractStrikeSystem strikeSystem;

        private void Reset()
        {
            aircraftObject = gameObject;
        }

        private void Awake()
        {
            if (aircraftObject == null) aircraftObject = gameObject;
            SetupAircraftCore();
            SetupBattlefieldCore();
            if (addSensors) SetupSensors();
            if (addPhysicalAI) SetupPhysicalAI();
            if (addSensorAI) SetupSensorAI();
            if (addIntegratedRecorder) SetupIntegratedRecorder();
            if (createHud) SetupHud();
            if (createCamera) SetupCamera();
            if (autoBindRadarSignatures) SetupRadarSignatures();
        }

        private void SetupAircraftCore()
        {
            aircraft = GetOrAdd<AircraftPhysicsController>(aircraftObject);
            if (GetComponent<AircraftPhysicsController>() == null && gameObject != aircraftObject)
            {
                // The aircraft controller intentionally lives on the aircraft object.
            }

            if (addWarThunderControls)
            {
                warThunderInput = GetOrAdd<WarThunderMouseAircraftInput>(aircraftObject);
                warThunderInput.controller = aircraft;
            }

            var stateSensor = GetOrAdd<AircraftStateSensor>(aircraftObject);
            stateSensor.controller = aircraft;
            GetOrAdd<WaypointAutopilot>(aircraftObject).controller = aircraft;
        }

        private void SetupBattlefieldCore()
        {
            requestManager = FindObjectOfType<CasRequestManager>();
            if (requestManager == null)
            {
                var go = new GameObject("CAS_Request_Manager");
                requestManager = go.AddComponent<CasRequestManager>();
            }

            strikeSystem = GetOrAdd<AbstractStrikeSystem>(aircraftObject);
            strikeSystem.requestManager = requestManager;
            var validator = GetOrAdd<CasValidator>(aircraftObject);
            strikeSystem.validator = validator;

            var mission = GetOrAdd<MissionScoreTracker>(aircraftObject);
            mission.aircraft = aircraft;
            mission.strikeSystem = strikeSystem;
        }

        private void SetupSensors()
        {
            radar = GetOrAdd<F15ERadarSystem>(aircraftObject);
            targetingPod = GetOrAdd<TargetingPodSystem>(aircraftObject);
            targetingPod.radar = radar;
            targetingPod.strikeSystem = strikeSystem;
            targetingPod.requestManager = requestManager;
            var podCameraSetup = GetOrAdd<TargetingPodCameraAutoSetup>(aircraftObject);
            podCameraSetup.createOnStart = true;
            podCameraSetup.renderToGameView = false;

            fusion = GetOrAdd<SensorFusionManager>(aircraftObject);
            fusion.radar = radar;
            fusion.targetingPod = targetingPod;
            fusion.strikeSystem = strikeSystem;
            fusion.requestManager = requestManager;

            var sensorValidator = GetOrAdd<SensorAidedCasValidator>(aircraftObject);
            sensorValidator.fusionManager = fusion;
            sensorValidator.baseValidator = strikeSystem != null ? strikeSystem.validator : null;

            var sensorLog = GetOrAdd<SensorLogRecorder>(aircraftObject);
            sensorLog.radar = radar;
            sensorLog.targetingPod = targetingPod;
            sensorLog.fusionManager = fusion;
        }

        private void SetupPhysicalAI()
        {
            var rulePilot = GetOrAdd<RuleCasPilot>(aircraftObject);
            rulePilot.aircraft = aircraft;
            rulePilot.autopilot = GetOrAdd<WaypointAutopilot>(aircraftObject);
            rulePilot.requestManager = requestManager;
            rulePilot.strikeSystem = strikeSystem;

            var obs = GetOrAdd<PhysicalAIObservationBuilder>(aircraftObject);
            obs.aircraft = aircraft;
            obs.requestManager = requestManager;
            obs.strikeSystem = strikeSystem;

            var validator = GetOrAdd<PhysicalActionValidator>(aircraftObject);
            validator.aircraft = aircraft;
            validator.requestManager = requestManager;
            validator.casValidator = strikeSystem != null ? strikeSystem.validator : null;

            var policy = GetOrAdd<BehaviorCloningLinearPolicy>(aircraftObject);
            var reward = GetOrAdd<PhysicalAIRewardTracker>(aircraftObject);
            reward.aircraft = aircraft;
            reward.requestManager = requestManager;
            reward.strikeSystem = strikeSystem;

            physicalRuntime = GetOrAdd<PhysicalAIRuntimeAgent>(aircraftObject);
            physicalRuntime.aircraft = aircraft;
            physicalRuntime.manualInput = aircraftObject.GetComponent<ManualAircraftInput>();
            physicalRuntime.rulePilot = rulePilot;
            physicalRuntime.autopilot = GetOrAdd<WaypointAutopilot>(aircraftObject);
            physicalRuntime.observationBuilder = obs;
            physicalRuntime.linearPolicy = policy;
            physicalRuntime.actionValidator = validator;
            physicalRuntime.strikeSystem = strikeSystem;
            physicalRuntime.requestManager = requestManager;
            physicalRuntime.rewardTracker = reward;

            var trainer = GetOrAdd<OnlineBehaviorCloningTrainer>(aircraftObject);
            trainer.runtimeAgent = physicalRuntime;
            trainer.observationBuilder = obs;
            trainer.policy = policy;

            var demo = GetOrAdd<PhysicalAIDemonstrationRecorder>(aircraftObject);
            demo.runtimeAgent = physicalRuntime;
            demo.observationBuilder = obs;
            demo.manualInput = physicalRuntime.manualInput;
            demo.rewardTracker = reward;
        }

        private void SetupSensorAI()
        {
            var obs = GetOrAdd<SensorAIObservationBuilder>(aircraftObject);
            obs.radar = radar;
            obs.targetingPod = targetingPod;
            obs.fusion = fusion;
            obs.requestManager = requestManager;
            obs.strikeSystem = strikeSystem;

            var validator = GetOrAdd<SensorActionValidator>(aircraftObject);
            validator.radar = radar;
            validator.targetingPod = targetingPod;
            validator.fusion = fusion;

            var rule = GetOrAdd<SensorRuleCasPilot>(aircraftObject);
            rule.radar = radar;
            rule.targetingPod = targetingPod;
            rule.fusion = fusion;
            rule.requestManager = requestManager;

            var policy = GetOrAdd<SensorBehaviorCloningLinearPolicy>(aircraftObject);

            sensorRuntime = GetOrAdd<SensorAIRuntimeAgent>(aircraftObject);
            sensorRuntime.radar = radar;
            sensorRuntime.targetingPod = targetingPod;
            sensorRuntime.fusion = fusion;
            sensorRuntime.observationBuilder = obs;
            sensorRuntime.rulePilot = rule;
            sensorRuntime.linearPolicy = policy;
            sensorRuntime.actionValidator = validator;

            var trainer = GetOrAdd<OnlineSensorBehaviorCloningTrainer>(aircraftObject);
            trainer.runtimeAgent = sensorRuntime;
            trainer.observationBuilder = obs;
            trainer.policy = policy;
        }

        private void SetupIntegratedRecorder()
        {
            var recorder = GetOrAdd<IntegratedAIDemonstrationRecorder>(aircraftObject);
            recorder.physicalRuntime = physicalRuntime;
            recorder.physicalObservationBuilder = aircraftObject.GetComponent<PhysicalAIObservationBuilder>();
            recorder.manualInput = aircraftObject.GetComponent<ManualAircraftInput>();
            recorder.rewardTracker = aircraftObject.GetComponent<PhysicalAIRewardTracker>();
            recorder.sensorRuntime = sensorRuntime;
            recorder.sensorObservationBuilder = aircraftObject.GetComponent<SensorAIObservationBuilder>();
        }

        private void SetupHud()
        {
            GetOrAdd<SimpleFlightHUD>(aircraftObject).aircraft = aircraft;
            GetOrAdd<PhysicalAIHUD>(aircraftObject).runtimeAgent = physicalRuntime;
            GetOrAdd<SensorAIHUD>(aircraftObject).runtimeAgent = sensorRuntime;
            GetOrAdd<RadarHud>(aircraftObject).radar = radar;
            var podHud = GetOrAdd<TargetingPodHud>(aircraftObject);
            podHud.pod = targetingPod;
            podHud.fusion = fusion;
            GetOrAdd<SensorCrosshairOverlay>(aircraftObject).pod = targetingPod;
            GetOrAdd<WarThunderControlHud>(aircraftObject).input = warThunderInput;
        }

        private void SetupCamera()
        {
            if (Camera.main == null)
            {
                var cam = new GameObject("Main Camera");
                cam.tag = "MainCamera";
                cam.AddComponent<Camera>();
                cam.AddComponent<AudioListener>();
            }

            // v0.9: Main Camera is controlled by MaverickStableChaseCamera.
            // Do not auto-add WarThunderChaseCamera here.
        }

        private void SetupRadarSignatures()
        {
            var units = FindObjectsOfType<GroundUnit>();
            foreach (var unit in units)
            {
                if (unit.GetComponent<RadarSignature>() == null) unit.gameObject.AddComponent<RadarSignature>();
            }
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            if (component == null) component = target.AddComponent<T>();
            return component;
        }
    }
}
