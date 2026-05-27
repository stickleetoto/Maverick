using UnityEngine;
using EaglePhysicalAI.Battlefield;

namespace EaglePhysicalAI.Scenario
{
    public class MaverickRunwayAndRangeBuilder : MonoBehaviour
    {
        public bool buildOnStart = false;
        public Material groundMaterial;
        public Material runwayMaterial;
        public Material hostileMaterial;
        public Material friendlyMaterial;

        [ContextMenu("Build Simple Range")]
        public void BuildRange()
        {
            CreatePlane("Ground_TestRange", new Vector3(0, -1, 0), new Vector3(160, 1, 160), groundMaterial);
            CreateCube("Runway_01", new Vector3(0, -0.45f, 0), new Vector3(18, 0.1f, 160), runwayMaterial);
            CreateTarget("Hostile_TestTarget_01", GroundTeam.Hostile, new Vector3(0, 0.5f, 450), hostileMaterial);
            CreateTarget("Friendly_TestUnit_01", GroundTeam.Friendly, new Vector3(80, 0.5f, 380), friendlyMaterial);
        }

        private void Start()
        {
            if (buildOnStart) BuildRange();
        }

        private GameObject CreatePlane(string name, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = scale;
            if (mat != null) go.GetComponent<Renderer>().material = mat;
            return go;
        }

        private GameObject CreateCube(string name, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = scale;
            if (mat != null) go.GetComponent<Renderer>().material = mat;
            return go;
        }

        private GameObject CreateTarget(string name, GroundTeam team, Vector3 pos, Material mat)
        {
            GameObject go = CreateCube(name, pos, new Vector3(8, 1, 8), mat);
            GroundUnit unit = go.GetComponent<GroundUnit>();
            if (unit == null) unit = go.AddComponent<GroundUnit>();
            unit.team = team;
            return go;
        }
    }
}
