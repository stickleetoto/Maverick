using UnityEngine;

namespace MaverickFresh
{
    /// <summary>
    /// Installs v0.16 Physical AI components and optionally builds an AI target scenario.
    /// Put this on MaverickFresh_Manager or Mav_Player.
    /// </summary>
    [DisallowMultipleComponent]
    public class MavPhysicalAIScenarioBootstrap : MonoBehaviour
    {
        [Header("References")]
        public GameObject aircraftObject;

        [Header("Install")]
        public bool setupOnAwake = true;
        public bool addPhysicalAIController = true;
        public bool addRewardLogger = true;
        public bool addFlightRecorder = true;
        public bool addGhostTrail = true;

        [Header("Scenario")]
        public bool createAirTarget = true;
        public bool createCASTargets = false;
        public Vector3 airTargetCenter = new Vector3(0f, 900f, 2400f);

        [Header("Created")]
        public MavPhysicalAIController physicalAI;
        public MavPhysicalAIRewardLogger rewardLogger;
        public MavFlightDataRecorder flightRecorder;
        public MavGhostTrailRecorder ghostTrail;
        public MavAITargetDrone drone;
        public MavCASTestRangeSpawner casRange;

        private void Awake()
        {
            if (setupOnAwake)
                Setup();
        }

        [ContextMenu("Setup Physical AI Scenario")]
        public void Setup()
        {
            if (aircraftObject == null)
                aircraftObject = MavPlayerResolver.FindPlayerObject();

            if (aircraftObject == null)
            {
                Debug.LogWarning("MavPhysicalAIScenarioBootstrap: aircraftObject missing.");
                return;
            }

            MavMouseFlightJet jet = aircraftObject.GetComponent<MavMouseFlightJet>();
            MavInstructorController instructor = aircraftObject.GetComponent<MavInstructorController>();
            if (instructor == null && jet != null)
                instructor = aircraftObject.AddComponent<MavInstructorController>();

            if (addPhysicalAIController)
            {
                physicalAI = aircraftObject.GetComponent<MavPhysicalAIController>();
                if (physicalAI == null)
                    physicalAI = aircraftObject.AddComponent<MavPhysicalAIController>();

                physicalAI.jet = jet;
                physicalAI.instructor = instructor;
                physicalAI.rig = FindObjectOfType<MavMouseFlightRig>();
                physicalAI.rb = aircraftObject.GetComponent<Rigidbody>();
                physicalAI.casTargeting = aircraftObject.GetComponent<MavCASTargetingSystem>();
                physicalAI.casWeapons = aircraftObject.GetComponent<MavCASWeaponSystem>();
                physicalAI.aiEnabled = false;
            }

            if (addRewardLogger)
            {
                rewardLogger = aircraftObject.GetComponent<MavPhysicalAIRewardLogger>();
                if (rewardLogger == null)
                    rewardLogger = aircraftObject.AddComponent<MavPhysicalAIRewardLogger>();

                rewardLogger.ai = physicalAI;
                rewardLogger.jet = jet;
                rewardLogger.weapons = aircraftObject.GetComponent<MavCASWeaponSystem>();
                rewardLogger.rb = aircraftObject.GetComponent<Rigidbody>();
            }

            if (addFlightRecorder)
            {
                flightRecorder = aircraftObject.GetComponent<MavFlightDataRecorder>();
                if (flightRecorder == null)
                    flightRecorder = aircraftObject.AddComponent<MavFlightDataRecorder>();

                flightRecorder.jet = jet;
                flightRecorder.rig = FindObjectOfType<MavMouseFlightRig>();
                flightRecorder.rb = aircraftObject.GetComponent<Rigidbody>();
            }

            if (addGhostTrail)
            {
                ghostTrail = aircraftObject.GetComponent<MavGhostTrailRecorder>();
                if (ghostTrail == null)
                    ghostTrail = aircraftObject.AddComponent<MavGhostTrailRecorder>();
            }

            if (createAirTarget)
                BuildAirTarget();

            if (createCASTargets)
                BuildCASRange();

            if (physicalAI != null && drone != null)
                physicalAI.airTarget = drone.transform;
        }

        [ContextMenu("Build Air Target")]
        public void BuildAirTarget()
        {
            GameObject existing = GameObject.Find("AirTarget_AI_Drone");
            if (existing != null)
            {
                drone = existing.GetComponent<MavAITargetDrone>();
                if (drone == null) drone = existing.AddComponent<MavAITargetDrone>();
                return;
            }

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "AirTarget_AI_Drone";
            go.transform.position = airTargetCenter + new Vector3(850f, 0f, 0f);
            go.transform.localScale = new Vector3(8f, 3f, 14f);

            drone = go.AddComponent<MavAITargetDrone>();
            drone.center = airTargetCenter;
        }

        [ContextMenu("Build CAS Range")]
        public void BuildCASRange()
        {
            casRange = GetComponent<MavCASTestRangeSpawner>();
            if (casRange == null)
                casRange = gameObject.AddComponent<MavCASTestRangeSpawner>();

            casRange.BuildRange();
        }
    }
}
