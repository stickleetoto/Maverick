using UnityEngine;

namespace MaverickFresh
{
    /// <summary>
    /// Adds the first Physical AI tools to the current Fresh aircraft:
    /// - data recorder
    /// - simple AI pilot
    /// - ghost trail
    /// 
    /// Put this on MaverickFresh_Manager or directly on Mav_Player.
    /// </summary>
    [DisallowMultipleComponent]
    public class MavPhysicalAIStarterBootstrap : MonoBehaviour
    {
        [Header("References")]
        public GameObject aircraftObject;

        [Header("Install")]
        public bool setupOnAwake = true;
        public bool addDataRecorder = true;
        public bool addSimpleAIPilot = true;
        public bool addGhostTrail = true;

        [Header("Created")]
        public MavFlightDataRecorder recorder;
        public MavSimpleAIPilot aiPilot;
        public MavGhostTrailRecorder ghostTrail;
        public MavPhysicalAIScenarioBootstrap scenarioBootstrap;

        private void Awake()
        {
            if (setupOnAwake)
                Setup();
        }

        [ContextMenu("Setup Physical AI Starter")]
        public void Setup()
        {
            if (aircraftObject == null)
                aircraftObject = MavPlayerResolver.FindPlayerObject();

            if (aircraftObject == null)
            {
                Debug.LogWarning("MavPhysicalAIStarterBootstrap: aircraftObject missing.");
                return;
            }

            MavMouseFlightJet jet = aircraftObject.GetComponent<MavMouseFlightJet>();
            MavInstructorController instructor = aircraftObject.GetComponent<MavInstructorController>();
            if (instructor == null && jet != null)
                instructor = aircraftObject.AddComponent<MavInstructorController>();

            if (addDataRecorder)
            {
                recorder = aircraftObject.GetComponent<MavFlightDataRecorder>();
                if (recorder == null) recorder = aircraftObject.AddComponent<MavFlightDataRecorder>();
                recorder.jet = jet;
                recorder.rig = FindObjectOfType<MavMouseFlightRig>();
                recorder.rb = aircraftObject.GetComponent<Rigidbody>();
            }

            if (addSimpleAIPilot)
            {
                aiPilot = aircraftObject.GetComponent<MavSimpleAIPilot>();
                if (aiPilot == null) aiPilot = aircraftObject.AddComponent<MavSimpleAIPilot>();
                aiPilot.jet = jet;
                aiPilot.instructor = instructor;
                aiPilot.rig = FindObjectOfType<MavMouseFlightRig>();
                aiPilot.rb = aircraftObject.GetComponent<Rigidbody>();
            }

            if (addGhostTrail)
            {
                ghostTrail = aircraftObject.GetComponent<MavGhostTrailRecorder>();
                if (ghostTrail == null) ghostTrail = aircraftObject.AddComponent<MavGhostTrailRecorder>();
            }

            scenarioBootstrap = GetComponent<MavPhysicalAIScenarioBootstrap>();
            if (scenarioBootstrap == null)
                scenarioBootstrap = gameObject.AddComponent<MavPhysicalAIScenarioBootstrap>();

            scenarioBootstrap.aircraftObject = aircraftObject;
            scenarioBootstrap.setupOnAwake = false;
            scenarioBootstrap.createAirTarget = true;
            scenarioBootstrap.createCASTargets = false;
            scenarioBootstrap.Setup();
        }
    }
}
