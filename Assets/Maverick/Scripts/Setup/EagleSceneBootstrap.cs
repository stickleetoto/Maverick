using UnityEngine;
using EaglePhysicalAI.Aircraft;
using EaglePhysicalAI.Battlefield;
using EaglePhysicalAI.CAS;
using EaglePhysicalAI.Data;
using EaglePhysicalAI.Mission;
using EaglePhysicalAI.UI;
using EaglePhysicalAI.Controls;
using EaglePhysicalAI.AI;

namespace EaglePhysicalAI.Setup
{
    /// <summary>
    /// Optional helper. Put it on an empty GameObject and assign the aircraft.
    /// It adds missing MVP components without touching your asset setup too much.
    /// </summary>
    public class EagleSceneBootstrap : MonoBehaviour
    {
        public GameObject aircraftObject;
        public bool addMissingComponents = true;
        public bool createManagersIfMissing = true;

        private void Start()
        {
            if (!addMissingComponents) return;
            if (aircraftObject == null)
            {
                Debug.LogWarning("EagleSceneBootstrap: aircraftObject is not assigned.");
                return;
            }

            EnsureAircraftComponents();
            if (createManagersIfMissing) EnsureManagerComponents();
        }

        private void EnsureAircraftComponents()
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
        }

        private void EnsureManagerComponents()
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

            if (FindObjectOfType<SimpleFlightHUD>() == null)
            {
                new GameObject("Simple_Flight_HUD").AddComponent<SimpleFlightHUD>();
            }
        }

        private static T Ensure<T>(GameObject go) where T : Component
        {
            T existing = go.GetComponent<T>();
            return existing != null ? existing : go.AddComponent<T>();
        }
    }
}
