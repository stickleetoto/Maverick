using System.Reflection;
using UnityEngine;

namespace MaverickFresh
{
    /// <summary>
    /// Merge-safe aircraft damage component used by missiles, gun tracers, enemy AI, and HUD kill logic.
    /// This version intentionally avoids hard compile-time dependencies on sensor/signature scripts so it
    /// can be dropped into older local projects without breaking compilation.
    /// </summary>
    [DisallowMultipleComponent]
    public class MavAircraftDamageState : MonoBehaviour
    {
        [Header("Identity")]
        public string displayName = "Aircraft";
        public int team = 1;

        [Header("Health")]
        public float maxHealth = 100f;
        public float health = 100f;
        public bool destroyed;
        public bool disableOnDestroyed = false;
        public float destroyDelay = 4.0f;

        [Header("Effects")]
        public bool spawnDebugExplosion = true;
        public float explosionScale = 7f;
        public Color explosionColor = new Color(1f, 0.45f, 0.08f, 0.85f);

        [Header("Runtime")]
        public string lastHitSource = "none";
        public float lastDamage;
        public float destroyedTime;

        private Component signatureLike;

        private void Awake()
        {
            ResolveIdentityFromSignature();
            if (maxHealth <= 0f)
                maxHealth = 100f;
            if (health <= 0f)
                health = maxHealth;
        }

        private void OnEnable()
        {
            ResolveIdentityFromSignature();
        }

        private void ResolveIdentityFromSignature()
        {
            if (signatureLike == null)
                signatureLike = GetComponent("MavRadarSignature");

            if (signatureLike == null)
                return;

            var type = signatureLike.GetType();

            if (string.IsNullOrWhiteSpace(displayName) || displayName == "Aircraft")
            {
                string sigName = ReadStringFieldOrProperty(type, signatureLike, "displayName");
                displayName = string.IsNullOrWhiteSpace(sigName) ? gameObject.name : sigName;
            }

            if (TryReadIntFieldOrProperty(type, signatureLike, "team", out int sigTeam))
                team = sigTeam;
        }

        private static string ReadStringFieldOrProperty(System.Type type, object target, string name)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null && field.FieldType == typeof(string))
                return field.GetValue(target) as string;

            PropertyInfo prop = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null && prop.PropertyType == typeof(string))
                return prop.GetValue(target, null) as string;

            return null;
        }

        private static bool TryReadIntFieldOrProperty(System.Type type, object target, string name, out int value)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null && field.FieldType == typeof(int))
            {
                value = (int)field.GetValue(target);
                return true;
            }

            PropertyInfo prop = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null && prop.PropertyType == typeof(int))
            {
                value = (int)prop.GetValue(target, null);
                return true;
            }

            value = 0;
            return false;
        }

        public bool IsAlive()
        {
            return !destroyed && health > 0f && gameObject.activeInHierarchy;
        }

        public void ApplyDamage(float amount, string source, Vector3 hitPoint)
        {
            if (destroyed)
                return;

            ResolveIdentityFromSignature();
            lastDamage = Mathf.Max(0f, amount);
            lastHitSource = string.IsNullOrWhiteSpace(source) ? "unknown" : source;
            health = Mathf.Max(0f, health - lastDamage);

            if (health <= 0f)
                Kill(lastHitSource, hitPoint);
        }

        public void Kill(string source, Vector3 point)
        {
            if (destroyed)
                return;

            destroyed = true;
            destroyedTime = Time.time;
            lastHitSource = string.IsNullOrWhiteSpace(source) ? lastHitSource : source;
            health = 0f;

            if (spawnDebugExplosion)
                SpawnDebugExplosion(point);

            SendMessage("OnMavAircraftDestroyed", this, SendMessageOptions.DontRequireReceiver);

            if (disableOnDestroyed)
                Invoke(nameof(DisableObject), Mathf.Max(0.05f, destroyDelay));
        }

        public void ResetDamage()
        {
            destroyed = false;
            health = Mathf.Max(1f, maxHealth);
            lastHitSource = "none";
            lastDamage = 0f;
            destroyedTime = 0f;
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
        }

        private void DisableObject()
        {
            gameObject.SetActive(false);
        }

        private void SpawnDebugExplosion(Vector3 point)
        {
            GameObject fx = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fx.name = "Mav_DebugAircraftExplosion";
            fx.transform.position = point == Vector3.zero ? transform.position : point;
            fx.transform.localScale = Vector3.one * Mathf.Max(0.1f, explosionScale);

            Collider col = fx.GetComponent<Collider>();
            if (col != null)
                Object.Destroy(col);

            Renderer renderer = fx.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material mat = new Material(Shader.Find("Standard"));
                mat.color = explosionColor;
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", explosionColor * 2f);
                renderer.sharedMaterial = mat;
            }

            Object.Destroy(fx, 0.35f);
        }
    }
}
