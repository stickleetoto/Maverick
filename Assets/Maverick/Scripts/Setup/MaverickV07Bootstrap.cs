using UnityEngine;
using EaglePhysicalAI.Aircraft;
using EaglePhysicalAI.Data;
using EaglePhysicalAI.Scenario;
using EaglePhysicalAI.Training;
using EaglePhysicalAI.UI;

namespace EaglePhysicalAI.Setup
{
    /// <summary>
    /// v0.7 integration bootstrap. Add this to the F-15E/F-22 aircraft root or a manager object.
    /// It preserves EagleV06ResearchBootstrap and adds Maverick rig/profile/training utilities.
    /// </summary>
    [DefaultExecutionOrder(150)]
    public class MaverickV07Bootstrap : MonoBehaviour
    {
        [Header("Required")]
        public GameObject aircraftObject;

        [Header("v0.7 Options")]
        public bool ensureV06ResearchBootstrap = true;
        public bool ensureRigHelper = true;
        public bool ensureProfileApplier = true;
        public bool ensureLandingGearController = true;
        public bool ensureDatasetManifestWriter = true;
        public bool ensureEpisodeResetManager = true;
        public bool ensureTrainingSessionRunner = true;
        public bool ensureReadinessHud = true;
        public bool createProceduralTestRange = false;
        public AircraftTrainingProfileKind defaultProfile = AircraftTrainingProfileKind.F15EStyleCAS;

        [Header("Created References")]
        public EagleV06ResearchBootstrap v06;
        public F15EAssetRigHelper rigHelper;
        public AircraftProfileApplier profileApplier;
        public SimpleLandingGearController gearController;
        public MaverickDatasetManifestWriter manifestWriter;
        public EpisodeResetManager resetManager;
        public AutonomousTrainingSessionRunner trainingRunner;
        public MaverickReadinessHud readinessHud;
        public ProceduralTestRangeBuilder testRangeBuilder;

        private void Reset()
        {
            aircraftObject = gameObject;
        }

        private void Awake()
        {
            if (aircraftObject == null) aircraftObject = gameObject;
            SetupV07();
        }

        [ContextMenu("Setup MAVERICK v0.7")]
        public void SetupV07()
        {
            if (aircraftObject == null) aircraftObject = gameObject;

            if (ensureV06ResearchBootstrap)
            {
                v06 = GetOrAdd<EagleV06ResearchBootstrap>(aircraftObject);
                if (v06.aircraftObject == null) v06.aircraftObject = aircraftObject;
                v06.SetupV06();
            }

            if (ensureRigHelper)
            {
                rigHelper = GetOrAdd<F15EAssetRigHelper>(aircraftObject);
                rigHelper.EnsureRig();
            }

            if (ensureProfileApplier)
            {
                profileApplier = GetOrAdd<AircraftProfileApplier>(aircraftObject);
                profileApplier.defaultProfileKind = defaultProfile;
                profileApplier.ApplyDefaultProfile();
            }

            if (ensureLandingGearController)
            {
                gearController = GetOrAdd<SimpleLandingGearController>(aircraftObject);
            }

            if (ensureDatasetManifestWriter)
            {
                manifestWriter = GetOrAdd<MaverickDatasetManifestWriter>(aircraftObject);
            }

            if (ensureEpisodeResetManager)
            {
                resetManager = GetOrAdd<EpisodeResetManager>(aircraftObject);
                var aircraft = aircraftObject.GetComponent<AircraftPhysicsController>();
                resetManager.aircraft = aircraft;
                resetManager.aircraftRigidbody = aircraft != null ? aircraft.rb : aircraftObject.GetComponent<Rigidbody>();
            }

            if (ensureTrainingSessionRunner)
            {
                trainingRunner = GetOrAdd<AutonomousTrainingSessionRunner>(aircraftObject);
                trainingRunner.resetManager = resetManager;
                trainingRunner.manifestWriter = manifestWriter;
            }

            if (ensureReadinessHud)
            {
                readinessHud = GetOrAdd<MaverickReadinessHud>(aircraftObject);
            }

            if (createProceduralTestRange)
            {
                testRangeBuilder = FindObjectOfType<ProceduralTestRangeBuilder>();
                if (testRangeBuilder == null)
                {
                    var go = new GameObject("Maverick_Procedural_Test_Range_Builder");
                    testRangeBuilder = go.AddComponent<ProceduralTestRangeBuilder>();
                }
                testRangeBuilder.BuildRange();

                if (v06 != null && testRangeBuilder != null)
                {
                    v06.ingressWaypoint = testRangeBuilder.ingressWaypoint;
                    v06.orbitWaypoint = testRangeBuilder.orbitWaypoint;
                    v06.returnWaypoint = testRangeBuilder.returnWaypoint;
                    v06.scriptedTarget = testRangeBuilder.scriptedTarget;
                    v06.scriptedRequester = testRangeBuilder.scriptedRequester;
                    v06.SetupV06();
                }
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
