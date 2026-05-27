using UnityEngine;
using EaglePhysicalAI.Aircraft;
using EaglePhysicalAI.AI;
using EaglePhysicalAI.Battlefield;
using EaglePhysicalAI.CAS;
using EaglePhysicalAI.Data;
using EaglePhysicalAI.Mission;
using EaglePhysicalAI.PhysicalAI;
using EaglePhysicalAI.UI;
using EaglePhysicalAI.Controls;
using EaglePhysicalAI.Sensors.Radar;
using EaglePhysicalAI.Sensors.TargetingPod;
using EaglePhysicalAI.Sensors.Fusion;
using EaglePhysicalAI.Sensors.UI;

namespace EaglePhysicalAI.Setup
{
    /// <summary>
    /// v0.3 bootstrap. Assign aircraftObject, press Play, and it adds the physical-AI stack.
    /// This does not place art assets or terrain; it only wires scripts.
    /// </summary>
    public class EaglePhysicalAIBootstrap : MonoBehaviour
    {
        public GameObject aircraftObject;
        public bool addMissingComponents = true;
        public bool createManagersIfMissing = true;
        public bool createPhysicalAIHud = true;
        public bool createGroundBattleDirector = true;
        public bool addSensorSuite = true;
        public bool createSensorHud = true;
        public bool createRadarSignatureAutoBinder = true;
        public bool createWarThunderControlHud = true;
        public bool createWarThunderChaseCamera = true;

        private void Start()
        {
            if (!addMissingComponents) return;
            if (aircraftObject == null)
            {
                Debug.LogWarning("EaglePhysicalAIBootstrap: aircraftObject is not assigned.");
                return;
            }

            EnsureAircraftStack();
            if (createManagersIfMissing) EnsureManagers();
        }

        private void EnsureAircraftStack()
        {
            if (aircraftObject.GetComponent<Rigidbody>() == null)
            {
                var rb = aircraftObject.AddComponent<Rigidbody>();
                rb.mass = 14500f;
                rb.useGravity = true;
            }

            Ensure<AircraftPhysicsController>(aircraftObject);
            Ensure<WarThunderMouseAircraftInput>(aircraftObject);
            Ensure<AircraftStateSensor>(aircraftObject);
            Ensure<WaypointAutopilot>(aircraftObject);
            Ensure<AbstractStrikeSystem>(aircraftObject);
            Ensure<RuleCasPilot>(aircraftObject);
            Ensure<TesterSessionLogger>(aircraftObject);

            Ensure<PhysicalAIObservationBuilder>(aircraftObject);
            Ensure<PhysicalActionValidator>(aircraftObject);
            Ensure<BehaviorCloningLinearPolicy>(aircraftObject);
            Ensure<PhysicalAIRewardTracker>(aircraftObject);
            Ensure<PhysicalAIRuntimeAgent>(aircraftObject);
            Ensure<PhysicalAIDemonstrationRecorder>(aircraftObject);
            Ensure<OnlineBehaviorCloningTrainer>(aircraftObject);
            Ensure<DatasetReplayTrainer>(aircraftObject);
            Ensure<RunTagger>(aircraftObject);

            if (addSensorSuite)
            {
                Ensure<RadarSignature>(aircraftObject).kind = SensorContactKind.Air;
                Ensure<F15ERadarSystem>(aircraftObject);
                Ensure<TargetingPodSystem>(aircraftObject);
                Ensure<TargetingPodCameraAutoSetup>(aircraftObject);
                Ensure<SensorFusionManager>(aircraftObject);
                Ensure<SensorAidedCasValidator>(aircraftObject);
                Ensure<SensorLogRecorder>(aircraftObject);
            }
        }

        private void EnsureManagers()
        {
            if (FindObjectOfType<CasRequestManager>() == null)
            {
                new GameObject("CAS_Request_Manager").AddComponent<CasRequestManager>();
            }

            if (FindObjectOfType<CasValidator>() == null)
            {
                new GameObject("CAS_Validator").AddComponent<CasValidator>();
            }

            if (FindObjectOfType<MissionScoreTracker>() == null)
            {
                new GameObject("Mission_Score_Tracker").AddComponent<MissionScoreTracker>();
            }

            if (createGroundBattleDirector && FindObjectOfType<GroundBattleDirector>() == null)
            {
                new GameObject("Ground_Battle_Director").AddComponent<GroundBattleDirector>();
            }

            if (FindObjectOfType<SimpleFlightHUD>() == null)
            {
                new GameObject("Simple_Flight_HUD").AddComponent<SimpleFlightHUD>();
            }

            if (createPhysicalAIHud && FindObjectOfType<PhysicalAIHUD>() == null)
            {
                new GameObject("Physical_AI_HUD").AddComponent<PhysicalAIHUD>();
            }

            if (createRadarSignatureAutoBinder && FindObjectOfType<RadarSignatureAutoBinder>() == null)
            {
                new GameObject("Radar_Signature_Auto_Binder").AddComponent<RadarSignatureAutoBinder>();
            }

            if (createWarThunderControlHud && FindObjectOfType<WarThunderControlHud>() == null)
            {
                new GameObject("WarThunder_Control_HUD").AddComponent<WarThunderControlHud>();
            }

            if (createWarThunderChaseCamera && FindObjectOfType<WarThunderChaseCamera>() == null)
            {
                Camera cam = Camera.main;
                GameObject camGo;
                if (cam != null) camGo = cam.gameObject;
                else
                {
                    camGo = new GameObject("WarThunder_Chase_Camera");
                    camGo.AddComponent<Camera>();
                    camGo.tag = "MainCamera";
                }
                var chase = camGo.GetComponent<WarThunderChaseCamera>();
                if (chase == null) chase = camGo.AddComponent<WarThunderChaseCamera>();
                chase.target = aircraftObject.transform;
                chase.input = aircraftObject.GetComponent<WarThunderMouseAircraftInput>();
            }

            if (createSensorHud)
            {
                if (FindObjectOfType<RadarHud>() == null)
                {
                    new GameObject("Radar_HUD").AddComponent<RadarHud>();
                }
                if (FindObjectOfType<TargetingPodHud>() == null)
                {
                    new GameObject("Targeting_Pod_HUD").AddComponent<TargetingPodHud>();
                }
                if (FindObjectOfType<SensorCrosshairOverlay>() == null)
                {
                    new GameObject("Sensor_Crosshair_Overlay").AddComponent<SensorCrosshairOverlay>();
                }
            }
        }

        private static T Ensure<T>(GameObject go) where T : Component
        {
            T existing = go.GetComponent<T>();
            return existing != null ? existing : go.AddComponent<T>();
        }
    }
}
