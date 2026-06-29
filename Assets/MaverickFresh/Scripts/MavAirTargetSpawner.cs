using UnityEngine;

namespace MaverickFresh
{
    public class MavAirTargetSpawner : MonoBehaviour
    {
        public bool buildOnStart = false;
        public int droneCount = 3;
        public float centerAltitude = 850f;
        public Material droneMaterial;

        private void Start()
        {
            if (buildOnStart)
                BuildTargets();
        }

        [ContextMenu("Build Fresh Air Targets")]
        public void BuildTargets()
        {
            GameObject center = GameObject.Find("MavFresh_AirCombatCenter");
            if (center == null)
            {
                center = new GameObject("MavFresh_AirCombatCenter");
                center.transform.position = new Vector3(0f, centerAltitude, 650f);
            }

            for (int i = 0; i < droneCount; i++)
            {
                GameObject drone = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                drone.name = "MavFresh_AirTarget_" + (i + 1).ToString("00");
                drone.transform.localScale = new Vector3(5f, 1.2f, 10f);
                drone.transform.position = center.transform.position + new Vector3(i * 80f, 0f, 280f + i * 160f);

                if (droneMaterial != null)
                    drone.GetComponent<Renderer>().material = droneMaterial;

                Collider c = drone.GetComponent<Collider>();
                if (c != null) c.isTrigger = true;

                MavAirTargetDrone mover = drone.AddComponent<MavAirTargetDrone>();
                mover.orbitCenter = center.transform;
                mover.orbitRadius = 350f + i * 170f;
                mover.orbitSpeedDeg = 10f + i * 2.5f;
                mover.altitude = centerAltitude + i * 80f;

                MavRadarSignature sig = drone.AddComponent<MavRadarSignature>();
                sig.displayName = "Drone " + (i + 1).ToString("00");
                sig.team = 1;
                sig.isAirTarget = true;
                sig.radarCrossSectionSqm = 3.0f + i * 0.8f;
                sig.irSignature = 0.85f + i * 0.10f;
            }
        }
    }
}
