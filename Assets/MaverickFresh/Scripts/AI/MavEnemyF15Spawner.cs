using UnityEngine;

namespace MaverickFresh
{
    /// <summary>
    /// v0.21.0 enemy F-15 spawner.
    /// Creates an AI F-15EX using the same flight stack as the player: Rigidbody + MavMouseFlightJet
    /// + MavAeroBody + MavAtmosphericEngine + profile data. It is not an orbiting dummy.
    /// </summary>
    public class MavEnemyF15Spawner : MonoBehaviour
    {
        [Header("Spawn")]
        public bool spawnOnStart;
        public bool spawnOnlyIfMissing = true;
        public string enemyName = "Mav_Enemy_F15EX";
        public MavAircraftKind enemyAircraft = MavAircraftKind.F15E;
        public GameObject f15VisualSource;
        public string playerObjectName = MavPlayerResolver.DefaultPlayerName;
        public Vector3 spawnOffsetLocal = new Vector3(850f, 130f, 2400f);
        public float startSpeed = 285f;

        [Header("AI")]
        public bool attachAIPilot = true;
        public bool attachLBMBrain = true;
        public float desiredDistance = 950f;
        public float closeDistance = 420f;
        public float leadTime = 0.55f;

        [Header("LBM Brain")]
        public float lbmDecisionInterval = 0.16f;
        public float lbmPreferredFightDistance = 950f;
        public float lbmGunRange = 620f;
        public float lbmMergeRange = 360f;
        public float lbmLowEnergySpeed = 185f;

        [Header("Fallback Visual")]
        public bool createPlaceholderIfNoVisual = true;
        public Material enemyMaterial;
        public Color placeholderColor = new Color(0.85f, 0.12f, 0.08f, 1f);

        [Header("Runtime")]
        public GameObject enemyObject;
        public string status = "idle";

        private void Start()
        {
            if (spawnOnStart)
                SpawnEnemy();
        }

        [ContextMenu("Spawn Enemy F-15")]
        public GameObject SpawnEnemy()
        {
            if (spawnOnlyIfMissing)
            {
                GameObject existing = GameObject.Find(enemyName);
                if (existing != null)
                {
                    enemyObject = existing;
                    status = "existing";
                    return enemyObject;
                }
            }

            GameObject player = GameObject.Find(playerObjectName);
            if (player == null)
                player = MavPlayerResolver.FindPlayerObject();

            Vector3 spawnPos = new Vector3(0f, 1200f, 2600f);
            Quaternion spawnRot = Quaternion.identity;
            if (player != null)
            {
                Transform pt = player.transform;
                spawnPos = pt.position + pt.right * spawnOffsetLocal.x + Vector3.up * spawnOffsetLocal.y + pt.forward * spawnOffsetLocal.z;
                Vector3 toPlayer = (pt.position - spawnPos);
                Vector3 flat = Vector3.ProjectOnPlane(toPlayer, Vector3.up);
                if (flat.sqrMagnitude > 0.1f)
                    spawnRot = Quaternion.LookRotation(flat.normalized, Vector3.up);
            }

            enemyObject = new GameObject(enemyName);
            enemyObject.transform.position = spawnPos;
            enemyObject.transform.rotation = spawnRot;

            Rigidbody rb = enemyObject.AddComponent<Rigidbody>();
            rb.mass = 14500f;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.maxAngularVelocity = 7f;

            MavMouseFlightJet jet = enemyObject.AddComponent<MavMouseFlightJet>();
            jet.allowGlobalRigLookup = false;
            jet.controller = null;
            jet.instructor = null;
            jet.enableMouseFlightAutopilot = false;
            jet.gravityOff = true;

            enemyObject.AddComponent<MavAeroBody>();
            enemyObject.AddComponent<MavAtmosphericEngine>();
            enemyObject.AddComponent<MavCombatFlapSystem>();

            MavAircraftProfileApplier applier = enemyObject.AddComponent<MavAircraftProfileApplier>();
            applier.allowGlobalRigLookup = false;
            applier.renameObject = false;
            applier.applyOnStart = false;
            applier.ApplyAircraft(enemyAircraft);

            MavRadarSignature sig = enemyObject.GetComponent<MavRadarSignature>();
            if (sig == null)
                sig = enemyObject.AddComponent<MavRadarSignature>();
            sig.displayName = "Enemy F-15EX";
            sig.team = 1;
            sig.isAirTarget = true;
            sig.radarCrossSectionSqm = 12.5f;
            sig.irSignature = 1.25f;
            sig.stealthRating = 0f;

            AttachVisual(enemyObject, player);
            EnsureCollider(enemyObject);

            MavEnemyLBMBrain lbm = null;
            if (attachLBMBrain)
            {
                lbm = enemyObject.AddComponent<MavEnemyLBMBrain>();
                lbm.jet = jet;
                lbm.rb = rb;
                lbm.target = player != null ? player.transform : null;
                lbm.targetRb = player != null ? player.GetComponent<Rigidbody>() : null;
                lbm.decisionInterval = lbmDecisionInterval;
                lbm.preferredFightDistance = lbmPreferredFightDistance;
                lbm.gunRange = lbmGunRange;
                lbm.mergeRange = lbmMergeRange;
                lbm.lowEnergySpeed = lbmLowEnergySpeed;
            }

            if (attachAIPilot)
            {
                MavEnemyAircraftPilot pilot = enemyObject.AddComponent<MavEnemyAircraftPilot>();
                pilot.jet = jet;
                pilot.rb = rb;
                pilot.target = player != null ? player.transform : null;
                pilot.targetRb = player != null ? player.GetComponent<Rigidbody>() : null;
                pilot.desiredDistance = desiredDistance;
                pilot.closeDistance = closeDistance;
                pilot.leadTime = leadTime;
                pilot.useLBMBrain = attachLBMBrain;
                pilot.autoInstallLBMBrain = false;
                pilot.lbmBrain = lbm;
                pilot.FindPlayerTarget();
            }

            rb.linearVelocity = enemyObject.transform.forward * Mathf.Max(80f, startSpeed);
            rb.angularVelocity = Vector3.zero;

            status = "spawned " + enemyName;
            return enemyObject;
        }

        private void AttachVisual(GameObject root, GameObject player)
        {
            GameObject source = f15VisualSource;
            if (source == null && player != null)
                source = FindF15VisualUnderPlayer(player.transform);

            GameObject holder = new GameObject("AircraftVisual");
            holder.transform.SetParent(root.transform, false);
            holder.transform.localPosition = Vector3.zero;
            holder.transform.localRotation = Quaternion.identity;
            holder.transform.localScale = Vector3.one;

            if (source != null)
            {
                GameObject clone = Instantiate(source, holder.transform, false);
                clone.name = "Enemy_F15EX_Visual";
                clone.SetActive(true);
                clone.transform.localPosition = source.transform.localPosition;
                clone.transform.localRotation = source.transform.localRotation;
                clone.transform.localScale = source.transform.localScale;
                SetLayerRecursively(clone, root.layer);
                return;
            }

            if (createPlaceholderIfNoVisual)
                BuildPlaceholderF15(holder.transform);
        }

        private GameObject FindF15VisualUnderPlayer(Transform player)
        {
            if (player == null)
                return null;

            Transform visuals = player.Find("AircraftVisuals");
            if (visuals == null)
                visuals = player;

            string[] names = { "F15EX", "F-15EX", "F15E", "F15", "F-15" };
            foreach (string n in names)
            {
                Transform found = visuals.Find(n);
                if (found != null)
                    return found.gameObject;
            }

            for (int i = 0; i < visuals.childCount; i++)
            {
                Transform child = visuals.GetChild(i);
                string lower = child.name.ToLowerInvariant();
                if (lower.Contains("f15") || lower.Contains("f-15"))
                    return child.gameObject;
            }
            return null;
        }

        private void BuildPlaceholderF15(Transform parent)
        {
            Material mat = enemyMaterial;
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard"));
                mat.color = placeholderColor;
            }

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Enemy_F15EX_Body_Placeholder";
            body.transform.SetParent(parent, false);
            body.transform.localScale = new Vector3(1.8f, 0.55f, 5.5f);
            body.GetComponent<Renderer>().material = mat;

            GameObject wing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wing.name = "Enemy_F15EX_Wing_Placeholder";
            wing.transform.SetParent(parent, false);
            wing.transform.localPosition = new Vector3(0f, -0.03f, -0.45f);
            wing.transform.localScale = new Vector3(6.2f, 0.12f, 1.45f);
            wing.GetComponent<Renderer>().material = mat;

            GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Enemy_F15EX_Nose_Placeholder";
            nose.transform.SetParent(parent, false);
            nose.transform.localPosition = new Vector3(0f, 0.02f, 3.1f);
            nose.transform.localScale = new Vector3(0.85f, 0.38f, 1.3f);
            nose.GetComponent<Renderer>().material = mat;

            GameObject tail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tail.name = "Enemy_F15EX_Tail_Placeholder";
            tail.transform.SetParent(parent, false);
            tail.transform.localPosition = new Vector3(0f, 0.55f, -2.35f);
            tail.transform.localScale = new Vector3(3.1f, 1.05f, 0.28f);
            tail.GetComponent<Renderer>().material = mat;
        }

        private void EnsureCollider(GameObject root)
        {
            Collider c = root.GetComponent<Collider>();
            if (c != null)
                return;

            BoxCollider box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0f, 0f);
            box.size = new Vector3(6.5f, 1.6f, 7.2f);
            box.isTrigger = false;
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            if (go == null) return;
            go.layer = layer;
            for (int i = 0; i < go.transform.childCount; i++)
                SetLayerRecursively(go.transform.GetChild(i).gameObject, layer);
        }
    }
}
