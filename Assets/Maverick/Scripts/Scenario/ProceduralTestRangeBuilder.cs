using UnityEngine;
using EaglePhysicalAI.Battlefield;
using EaglePhysicalAI.Mission;

namespace EaglePhysicalAI.Scenario
{
    /// <summary>
    /// Creates a simple placeholder training range when you do not want to place assets by hand yet.
    /// It uses primitive objects only and is safe to delete after your real scene is built.
    /// </summary>
    public class ProceduralTestRangeBuilder : MonoBehaviour
    {
        public bool buildOnStart;
        public bool skipIfGroundUnitsExist = true;
        public Material friendlyMaterial;
        public Material hostileMaterial;
        public Material neutralMaterial;
        public Material markerMaterial;

        [Header("Layout")]
        public int hostileCount = 5;
        public int friendlyCount = 3;
        public float unitSpacing = 90f;
        public Vector3 hostileCenter = new Vector3(900f, 0f, 900f);
        public Vector3 friendlyCenter = new Vector3(650f, 0f, 520f);
        public Vector3 neutralCenter = new Vector3(1040f, 0f, 760f);

        [Header("Created References")]
        public GroundUnit scriptedTarget;
        public GroundUnit scriptedRequester;
        public MissionWaypoint ingressWaypoint;
        public MissionWaypoint orbitWaypoint;
        public MissionWaypoint returnWaypoint;
        public NoStrikeZone noStrikeZone;
        public GroundThreatZone threatZone;

        private void Start()
        {
            if (buildOnStart) BuildRange();
        }

        [ContextMenu("Build Procedural Test Range")]
        public void BuildRange()
        {
            if (skipIfGroundUnitsExist && FindObjectsOfType<GroundUnit>().Length > 0) return;

            var root = new GameObject("Maverick_Procedural_Test_Range").transform;
            root.position = Vector3.zero;

            for (int i = 0; i < hostileCount; i++)
            {
                Vector3 p = hostileCenter + new Vector3((i % 3 - 1) * unitSpacing, 0f, (i / 3) * unitSpacing);
                var unit = CreateUnit(root, "Hostile_" + i, GroundTeam.Hostile, p, hostileMaterial, new Vector3(32f, 18f, 48f));
                if (i == 0) scriptedTarget = unit;
            }

            for (int i = 0; i < friendlyCount; i++)
            {
                Vector3 p = friendlyCenter + new Vector3((i - 1) * unitSpacing, 0f, 0f);
                var unit = CreateUnit(root, "Friendly_" + i, GroundTeam.Friendly, p, friendlyMaterial, new Vector3(32f, 18f, 42f));
                unit.canRequestCas = true;
                if (i == 0) scriptedRequester = unit;
            }

            CreateUnit(root, "Neutral_NoStrike_Object", GroundTeam.Neutral, neutralCenter, neutralMaterial, new Vector3(46f, 28f, 46f));

            ingressWaypoint = CreateWaypoint(root, "Waypoint_Ingress", new Vector3(0f, 750f, -350f), 160f);
            orbitWaypoint = CreateWaypoint(root, "Waypoint_Orbit", hostileCenter + new Vector3(0f, 600f, -300f), 220f);
            returnWaypoint = CreateWaypoint(root, "Waypoint_RTB", new Vector3(-700f, 650f, -900f), 200f);

            var noStrike = new GameObject("NoStrikeZone_Procedural");
            noStrike.transform.SetParent(root, true);
            noStrike.transform.position = neutralCenter;
            noStrikeZone = noStrike.AddComponent<NoStrikeZone>();
            noStrikeZone.radius = 180f;

            var threat = new GameObject("GroundThreatZone_Procedural");
            threat.transform.SetParent(root, true);
            threat.transform.position = hostileCenter + new Vector3(140f, 0f, 120f);
            threatZone = threat.AddComponent<GroundThreatZone>();
            threatZone.radius = 520f;
            threatZone.danger = 0.55f;
        }

        private GroundUnit CreateUnit(Transform root, string name, GroundTeam team, Vector3 position, Material material, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(root, true);
            go.transform.position = position + Vector3.up * (scale.y * 0.5f);
            go.transform.localScale = scale;
            if (material != null)
            {
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null) renderer.sharedMaterial = material;
            }
            var unit = go.AddComponent<GroundUnit>();
            unit.unitId = name;
            unit.team = team;
            unit.health = 100f;
            unit.importance = team == GroundTeam.Hostile ? 1f : 0.5f;
            return unit;
        }

        private MissionWaypoint CreateWaypoint(Transform root, string name, Vector3 position, float radius)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, true);
            go.transform.position = position;
            var wp = go.AddComponent<MissionWaypoint>();
            wp.waypointId = name;
            wp.radius = radius;
            return wp;
        }
    }
}
