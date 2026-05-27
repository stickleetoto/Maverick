using UnityEngine;

namespace MaverickFresh
{
    /// <summary>
    /// Builds a simple ground target range for CAS testing.
    /// Attach to any empty object and run context menu, or set buildOnStart.
    /// </summary>
    public class MavCASTestRangeSpawner : MonoBehaviour
    {
        public bool buildOnStart = false;
        public int targetRows = 2;
        public int targetsPerRow = 5;
        public float spacing = 90f;
        public Vector3 rangeCenter = new Vector3(0f, 0f, 1600f);
        public Material targetMaterial;
        public bool createGroundPlane = true;
        public Vector3 groundSize = new Vector3(1600f, 1f, 2200f);

        private void Start()
        {
            if (buildOnStart)
                BuildRange();
        }

        [ContextMenu("Build CAS Test Range")]
        public void BuildRange()
        {
            if (createGroundPlane && GameObject.Find("MavCAS_GroundPlane") == null)
            {
                GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.name = "MavCAS_GroundPlane";
                ground.transform.position = new Vector3(rangeCenter.x, -1.5f, rangeCenter.z);
                ground.transform.localScale = groundSize;
            }

            GameObject root = GameObject.Find("MavCAS_Targets");
            if (root == null)
                root = new GameObject("MavCAS_Targets");

            for (int r = 0; r < targetRows; r++)
            {
                for (int c = 0; c < targetsPerRow; c++)
                {
                    GameObject target = GameObject.CreatePrimitive(PickPrimitive(r, c));
                    target.name = "AirTarget_Ground_" + r.ToString("00") + "_" + c.ToString("00");
                    target.transform.SetParent(root.transform, true);

                    float x = (c - (targetsPerRow - 1) * 0.5f) * spacing;
                    float z = r * spacing;
                    target.transform.position = rangeCenter + new Vector3(x, 3f, z);
                    target.transform.localScale = PickScale(r, c);

                    if (targetMaterial != null)
                        target.GetComponent<Renderer>().material = targetMaterial;

                    MavCASTarget casTarget = target.AddComponent<MavCASTarget>();
                    casTarget.displayName = target.name;
                    casTarget.targetType = PickType(r, c);
                    casTarget.maxHealth = PickHealth(casTarget.targetType);
                    casTarget.health = casTarget.maxHealth;
                    casTarget.scoreValue = PickScore(casTarget.targetType);
                }
            }
        }

        private PrimitiveType PickPrimitive(int r, int c)
        {
            if ((r + c) % 4 == 0) return PrimitiveType.Cube;
            if ((r + c) % 4 == 1) return PrimitiveType.Capsule;
            if ((r + c) % 4 == 2) return PrimitiveType.Cylinder;
            return PrimitiveType.Cube;
        }

        private Vector3 PickScale(int r, int c)
        {
            MavCASTargetType t = PickType(r, c);
            if (t == MavCASTargetType.Tank) return new Vector3(12f, 4f, 20f);
            if (t == MavCASTargetType.APC) return new Vector3(10f, 3.5f, 16f);
            if (t == MavCASTargetType.Radar) return new Vector3(9f, 12f, 9f);
            if (t == MavCASTargetType.Building) return new Vector3(24f, 20f, 24f);
            return new Vector3(8f, 3f, 14f);
        }

        private MavCASTargetType PickType(int r, int c)
        {
            int v = (r * 7 + c) % 5;
            return (MavCASTargetType)v;
        }

        private float PickHealth(MavCASTargetType t)
        {
            switch (t)
            {
                case MavCASTargetType.Tank: return 220f;
                case MavCASTargetType.APC: return 150f;
                case MavCASTargetType.Radar: return 120f;
                case MavCASTargetType.Building: return 300f;
                default: return 90f;
            }
        }

        private int PickScore(MavCASTargetType t)
        {
            switch (t)
            {
                case MavCASTargetType.Tank: return 300;
                case MavCASTargetType.APC: return 180;
                case MavCASTargetType.Radar: return 250;
                case MavCASTargetType.Building: return 400;
                default: return 100;
            }
        }
    }
}
