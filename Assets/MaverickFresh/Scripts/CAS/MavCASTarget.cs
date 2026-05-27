using UnityEngine;

namespace MaverickFresh
{
    public enum MavCASTargetType
    {
        Truck = 0,
        APC = 1,
        Tank = 2,
        Radar = 3,
        Building = 4
    }

    /// <summary>
    /// Simple ground target for CAS gameplay.
    /// This is intentionally lightweight: health, damage, destroyed state, gizmo marker.
    /// </summary>
    [DisallowMultipleComponent]
    public class MavCASTarget : MonoBehaviour
    {
        [Header("Target")]
        public MavCASTargetType targetType = MavCASTargetType.Truck;
        public string displayName = "Ground Target";
        public int team = 1;
        public bool isDestroyed;

        [Header("Health")]
        public float maxHealth = 100f;
        public float health = 100f;
        public float armorMultiplier = 1f;

        [Header("Scoring")]
        public int scoreValue = 100;
        public bool countsForMission = true;

        [Header("Visual")]
        public bool hideOnDestroyed = false;
        public Color aliveGizmoColor = Color.red;
        public Color destroyedGizmoColor = Color.gray;
        public float gizmoRadius = 8f;

        [Header("Runtime")]
        public string lastHitBy = "";
        public float lastDamage;

        private Renderer[] renderers;
        private Collider[] colliders;

        private void Awake()
        {
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = gameObject.name;

            health = Mathf.Clamp(health <= 0f ? maxHealth : health, 0f, maxHealth);
            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);
        }

        public float DistanceTo(Vector3 point)
        {
            return Vector3.Distance(transform.position, point);
        }

        public void ApplyDamage(float amount, string source)
        {
            if (isDestroyed)
                return;

            float finalDamage = Mathf.Max(0f, amount / Mathf.Max(0.01f, armorMultiplier));
            health -= finalDamage;
            lastDamage = finalDamage;
            lastHitBy = source;

            if (health <= 0f)
                DestroyTarget(source);
        }

        public void DestroyTarget(string source)
        {
            isDestroyed = true;
            health = 0f;
            lastHitBy = source;

            if (hideOnDestroyed)
            {
                foreach (Renderer r in renderers)
                    if (r != null) r.enabled = false;
            }

            foreach (Collider c in colliders)
            {
                if (c != null)
                    c.enabled = false;
            }
        }

        public bool IsAlive()
        {
            return !isDestroyed && health > 0f && gameObject.activeInHierarchy;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = isDestroyed ? destroyedGizmoColor : aliveGizmoColor;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 3f, gizmoRadius);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 18f);
        }
    }
}
