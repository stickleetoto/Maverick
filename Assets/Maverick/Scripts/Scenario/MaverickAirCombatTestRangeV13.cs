using UnityEngine;

namespace EaglePhysicalAI.Scenario
{
    public class MaverickAirCombatTestRangeV13 : MonoBehaviour
    {
        public bool buildOnStart = false;
        public int droneCount = 3;
        public float centerAltitude = 850f;
        public Material droneMaterial;

        private void Start()
        {
            if (buildOnStart)
                Build();
        }

        [ContextMenu("Build Air Combat Test Range")]
        public void Build()
        {
            GameObject center = GameObject.Find("AirCombat_Center");
            if (center == null)
            {
                center = new GameObject("AirCombat_Center");
                center.transform.position = new Vector3(0f, centerAltitude, 500f);
            }

            for (int i = 0; i < droneCount; i++)
            {
                GameObject drone = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                drone.name = "AirTarget_Drone_" + (i + 1).ToString("00");
                drone.transform.localScale = new Vector3(4f, 1f, 8f);
                drone.transform.position = center.transform.position + new Vector3(i * 60f, 0f, 300f + i * 100f);

                if (droneMaterial != null)
                    drone.GetComponent<Renderer>().material = droneMaterial;

                var mover = drone.AddComponent<MaverickAirTargetDroneV13>();
                mover.orbitCenter = center.transform;
                mover.orbitRadius = 350f + i * 160f;
                mover.orbitSpeedDeg = 8f + i * 2f;
                mover.altitude = centerAltitude + i * 90f;

                Collider col = drone.GetComponent<Collider>();
                if (col != null) col.isTrigger = true;
            }
        }
    }
}
