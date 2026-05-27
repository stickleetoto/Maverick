using UnityEngine;
using EaglePhysicalAI.Aircraft;
using EaglePhysicalAI.Battlefield;
using EaglePhysicalAI.CAS;
using EaglePhysicalAI.Data;
using EaglePhysicalAI.Mission;
using EaglePhysicalAI.PhysicalAI;
using EaglePhysicalAI.Safety;
using EaglePhysicalAI.SensorAI;
using EaglePhysicalAI.Sensors.Fusion;
using EaglePhysicalAI.Training;
using EaglePhysicalAI.UI;

namespace EaglePhysicalAI.Setup
{
    /// <summary>
    /// Add this on the aircraft or a manager object after v0.5. It preserves v0.5 setup and adds:
    /// safety guard, curriculum manager, metrics recorder, dataset quality monitor, evaluation runner, and HUD.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class EagleV06ResearchBootstrap : MonoBehaviour
    {
        [Header("Required")]
        public GameObject aircraftObject;

        [Header("Optional v0.6 modules")]
        public bool ensureV05IntegratedBootstrap = true;
        public bool addSafetyGuard = true;
        public bool addCurriculumMission = true;
        public bool addRunMetrics = true;
        public bool addDatasetQualityMonitor = true;
        public bool addPolicyEvaluationRunner = true;
        public bool addMissionAndSafetyHud = true;

        [Header("Optional Mission References")]
        public MissionWaypoint ingressWaypoint;
        public MissionWaypoint orbitWaypoint;
        public MissionWaypoint returnWaypoint;
        public GroundUnit scriptedTarget;
        public GroundUnit scriptedRequester;

        [Header("Created References")]
        public EagleV05IntegratedBootstrap v05;
        public FlightEnvelopeSafetyGuard safetyGuard;
        public CurriculumMissionManager curriculum;
        public RunMetricsRecorder runMetrics;
        public DatasetQualityMonitor datasetQuality;
        public PolicyEvaluationRunner evaluationRunner;
        public MissionAndSafetyHUD missionHud;

        private void Reset()
        {
            aircraftObject = gameObject;
        }

        private void Awake()
        {
            if (aircraftObject == null) aircraftObject = gameObject;
            if (ensureV05IntegratedBootstrap)
            {
                v05 = GetComponent<EagleV05IntegratedBootstrap>();
                if (v05 == null) v05 = gameObject.AddComponent<EagleV05IntegratedBootstrap>();
                if (v05.aircraftObject == null) v05.aircraftObject = aircraftObject;
                v05.createCamera = false;
            }
        }

        private void Start()
        {
            SetupV06();
        }

        [ContextMenu("Setup v0.6 Research Modules")]
        public void SetupV06()
        {
            if (aircraftObject == null) aircraftObject = gameObject;
            AircraftPhysicsController aircraft = GetOrAdd<AircraftPhysicsController>(aircraftObject);
            PhysicalAIRuntimeAgent physical = GetOrAdd<PhysicalAIRuntimeAgent>(aircraftObject);
            SensorAIRuntimeAgent sensor = GetOrAdd<SensorAIRuntimeAgent>(aircraftObject);
            AbstractStrikeSystem strike = GetOrAdd<AbstractStrikeSystem>(aircraftObject);
            CasValidator validator = GetOrAdd<CasValidator>(aircraftObject);
            CasRequestManager requests = FindObjectOfType<CasRequestManager>();
            if (requests == null)
            {
                var go = new GameObject("CAS_Request_Manager");
                requests = go.AddComponent<CasRequestManager>();
            }
            SensorFusionManager fusion = GetOrAdd<SensorFusionManager>(aircraftObject);
            PhysicalAIRewardTracker reward = GetOrAdd<PhysicalAIRewardTracker>(aircraftObject);

            if (addSafetyGuard)
            {
                safetyGuard = GetOrAdd<FlightEnvelopeSafetyGuard>(aircraftObject);
                safetyGuard.aircraft = aircraft;
                safetyGuard.runtimeAgent = physical;
            }

            if (addCurriculumMission)
            {
                curriculum = GetOrAdd<CurriculumMissionManager>(aircraftObject);
                curriculum.aircraft = aircraft;
                curriculum.requestManager = requests;
                curriculum.strikeSystem = strike;
                curriculum.validator = validator;
                curriculum.fusion = fusion;
                curriculum.safetyGuard = safetyGuard;
                curriculum.ingressWaypoint = ingressWaypoint;
                curriculum.orbitWaypoint = orbitWaypoint;
                curriculum.returnWaypoint = returnWaypoint;
                curriculum.scriptedTarget = scriptedTarget;
                curriculum.scriptedRequester = scriptedRequester;
            }

            if (addRunMetrics)
            {
                runMetrics = GetOrAdd<RunMetricsRecorder>(aircraftObject);
                runMetrics.aircraft = aircraft;
                runMetrics.physicalRuntime = physical;
                runMetrics.sensorRuntime = sensor;
                runMetrics.rewardTracker = reward;
                runMetrics.curriculum = curriculum;
                runMetrics.safetyGuard = safetyGuard;
                runMetrics.fusion = fusion;
                runMetrics.requestManager = requests;
                runMetrics.strikeSystem = strike;
            }

            if (addDatasetQualityMonitor)
            {
                datasetQuality = GetOrAdd<DatasetQualityMonitor>(aircraftObject);
                datasetQuality.recorder = GetOrAdd<IntegratedAIDemonstrationRecorder>(aircraftObject);
                datasetQuality.physicalRuntime = physical;
                datasetQuality.sensorRuntime = sensor;
            }

            if (addPolicyEvaluationRunner)
            {
                evaluationRunner = GetOrAdd<PolicyEvaluationRunner>(aircraftObject);
                evaluationRunner.aircraft = aircraft;
                evaluationRunner.aircraftRigidbody = aircraft.rb;
                evaluationRunner.physicalRuntime = physical;
                evaluationRunner.sensorRuntime = sensor;
                evaluationRunner.rewardTracker = reward;
                evaluationRunner.curriculum = curriculum;
                evaluationRunner.strikeSystem = strike;
            }

            if (addMissionAndSafetyHud)
            {
                missionHud = GetOrAdd<MissionAndSafetyHUD>(aircraftObject);
                missionHud.curriculum = curriculum;
                missionHud.safetyGuard = safetyGuard;
                missionHud.datasetQuality = datasetQuality;
                missionHud.metricsRecorder = runMetrics;
                missionHud.evaluationRunner = evaluationRunner;
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
