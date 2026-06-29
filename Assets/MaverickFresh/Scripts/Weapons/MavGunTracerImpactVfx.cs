using System.Collections.Generic;
using UnityEngine;

namespace MaverickFresh
{
    /// <summary>
    /// Simulator-style gun visual feedback.
    /// No hitmarkers, no killfeed, no damage direction indicators.
    /// v0.22.6 is designed to be driven directly by MavCASWeaponSystem.NotifyGunRound,
    /// but can still fall back to ammo-delta detection if needed.
    /// </summary>
    [DisallowMultipleComponent]
    public class MavGunTracerImpactVfx : MonoBehaviour
    {
        [Header("References")]
        public MavCASWeaponSystem weaponSystem;

        [Header("Fallback Tracer")]
        public bool enableFallbackTracer = false;
        public float tracerLifeSeconds = 0.055f;
        public float tracerWidth = 0.055f;
        public float maxTracerLength = 2600f;
        public Color tracerColor = new Color(0.15f, 1.0f, 0.95f, 0.92f);
        public float muzzleForward = 11.5f;
        public float muzzleRight = 0.85f;
        public float muzzleDown = 1.1f;

        [Header("Impact Sparks")]
        public bool enableImpactSparks = true;
        public int sparkCount = 7;
        public float sparkLifeSeconds = 0.20f;
        public float sparkSpeed = 46f;
        public float sparkScale = 0.045f;
        public Color sparkColor = new Color(0.2f, 1.0f, 0.95f, 0.95f);
        public Color hotSparkColor = new Color(1.0f, 0.55f, 0.12f, 0.95f);

        [Header("Ricochet Flecks")]
        public bool enableRicochetFlecks = true;
        public int fleckCount = 3;
        public float fleckLifeSeconds = 0.12f;
        public float fleckSpeed = 72f;
        public float fleckScale = 0.028f;

        [Header("Fallback Detection")]
        public bool fallbackAmmoDeltaDetection = true;
        public bool spawnSparksOnlyOnGunHit = true;
        public float minimumShotInterval = 0.005f;

        [Header("Debug")]
        public int debugTracerNotifications;
        public int debugSparkBursts;
        public string debugLastVfx = "ready";

        private int lastGunAmmo = -1;
        private float lastSpawnTime = -999f;
        private Material sparkMaterial;
        private Material hotSparkMaterial;
        private readonly List<GameObject> liveObjects = new List<GameObject>();

        private void Awake()
        {
            Resolve();
            if (weaponSystem != null)
                lastGunAmmo = weaponSystem.gunAmmo;
        }

        private void Update()
        {
            if (!fallbackAmmoDeltaDetection)
                return;

            if (weaponSystem == null)
                Resolve();
            if (weaponSystem == null)
                return;

            if (lastGunAmmo < 0)
                lastGunAmmo = weaponSystem.gunAmmo;

            if (weaponSystem.gunAmmo < lastGunAmmo && Time.time >= lastSpawnTime + minimumShotInterval)
            {
                lastSpawnTime = Time.time;
                SpawnFallbackForShot();
            }

            lastGunAmmo = weaponSystem.gunAmmo;
            CleanupNulls();
        }

        private void Resolve()
        {
            if (weaponSystem == null)
                weaponSystem = GetComponent<MavCASWeaponSystem>();
        }

        public void NotifyGunRound(Vector3 origin, Vector3 end, bool hit, Vector3 hitNormal, bool aircraftHit, float heat01)
        {
            debugTracerNotifications++;
            debugLastVfx = hit ? (aircraftHit ? "aircraft_impact" : "impact") : "tracer";

            if (hit && enableImpactSparks)
            {
                Vector3 incoming = (origin - end).sqrMagnitude > 0.01f ? (origin - end).normalized : -transform.forward;
                SpawnSparks(end, hitNormal.sqrMagnitude > 0.01f ? hitNormal.normalized : incoming, incoming, aircraftHit, heat01);
            }
        }

        private void SpawnFallbackForShot()
        {
            Vector3 origin = GetFallbackGunMuzzlePosition();
            Vector3 end = weaponSystem.lastImpactPoint;
            if ((end - origin).sqrMagnitude < 4f)
                end = origin + transform.forward * Mathf.Min(maxTracerLength, weaponSystem.gunRange);

            Vector3 toEnd = end - origin;
            if (toEnd.magnitude > maxTracerLength)
                end = origin + toEnd.normalized * maxTracerLength;

            if (enableFallbackTracer)
                MavNeonTracer.SpawnAdvanced(origin, end, tracerLifeSeconds, tracerWidth, tracerColor, weaponSystem != null ? weaponSystem.debugGunHeat : 0f);

            bool hit = !string.IsNullOrEmpty(weaponSystem.lastEvent) && weaponSystem.lastEvent.StartsWith("gun_hit");
            if (enableImpactSparks && (!spawnSparksOnlyOnGunHit || hit))
                SpawnSparks(end, -toEnd.normalized, -toEnd.normalized, hit, weaponSystem != null ? weaponSystem.debugGunHeat : 0f);
        }

        private Vector3 GetFallbackGunMuzzlePosition()
        {
            return transform.position + transform.forward * muzzleForward + transform.right * muzzleRight - transform.up * muzzleDown;
        }

        private void SpawnSparks(Vector3 point, Vector3 normal, Vector3 incomingReverse, bool aircraftHit, float heat01)
        {
            debugSparkBursts++;
            int count = Mathf.Clamp(sparkCount + (aircraftHit ? 3 : 0), 1, 32);
            Material mat = heat01 > 0.55f ? GetHotSparkMaterial() : GetSparkMaterial();

            for (int i = 0; i < count; i++)
            {
                Vector3 dir = Vector3.Slerp(normal, incomingReverse, Random.Range(0.15f, 0.65f));
                dir = (dir + Random.onUnitSphere * 0.42f).normalized;
                SpawnSparkObject("Mav_Gun_Impact_Spark", point + Random.insideUnitSphere * 0.22f, dir, sparkSpeed * Random.Range(0.45f, 1.05f), sparkScale, sparkLifeSeconds, mat);
            }

            if (enableRicochetFlecks)
            {
                int fCount = Mathf.Clamp(fleckCount + (aircraftHit ? 2 : 0), 0, 16);
                for (int i = 0; i < fCount; i++)
                {
                    Vector3 reflect = Vector3.Reflect(-incomingReverse, normal);
                    Vector3 dir = (reflect + Random.onUnitSphere * 0.36f).normalized;
                    SpawnSparkObject("Mav_Gun_Ricochet_Fleck", point + normal * 0.08f, dir, fleckSpeed * Random.Range(0.45f, 1.0f), fleckScale, fleckLifeSeconds, mat);
                }
            }
        }

        private void SpawnSparkObject(string name, Vector3 pos, Vector3 dir, float speed, float scale, float life, Material mat)
        {
            GameObject spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            spark.name = name;
            spark.transform.position = pos;
            spark.transform.localScale = Vector3.one * Mathf.Max(0.006f, scale);

            Collider col = spark.GetComponent<Collider>();
            if (col != null) col.enabled = false;

            Renderer r = spark.GetComponent<Renderer>();
            if (r != null) r.material = mat;

            Rigidbody rb = spark.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.linearVelocity = dir * speed;
            rb.linearDamping = 8.5f;

            Destroy(spark, Mathf.Max(0.02f, life));
            liveObjects.Add(spark);
        }

        private Material GetSparkMaterial()
        {
            if (sparkMaterial != null)
                return sparkMaterial;
            sparkMaterial = new Material(FindVfxShader());
            sparkMaterial.color = sparkColor;
            return sparkMaterial;
        }

        private Material GetHotSparkMaterial()
        {
            if (hotSparkMaterial != null)
                return hotSparkMaterial;
            hotSparkMaterial = new Material(FindVfxShader());
            hotSparkMaterial.color = hotSparkColor;
            return hotSparkMaterial;
        }

        private Shader FindVfxShader()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Standard");
            return shader;
        }

        private void CleanupNulls()
        {
            for (int i = liveObjects.Count - 1; i >= 0; i--)
            {
                if (liveObjects[i] == null)
                    liveObjects.RemoveAt(i);
            }
        }
    }
}
