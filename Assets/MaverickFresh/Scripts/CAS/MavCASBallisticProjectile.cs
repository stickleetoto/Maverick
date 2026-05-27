using UnityEngine;

namespace MaverickFresh
{
    public enum MavCASProjectileKind
    {
        GunShell = 0,
        Rocket = 1,
        Bomb = 2,
        Missile = 3
    }

    /// <summary>
    /// Game/sim-lite projectile for asset-based CAS ordnance.
    ///
    /// v0.13:
    /// - Can be attached to your own bullet/rocket/bomb/missile prefabs.
    /// - If spawned from MavCASWeaponSystem, it receives velocity/damage/radius.
    /// - Optional simple guidance for missiles.
    /// </summary>
    [DisallowMultipleComponent]
    public class MavCASBallisticProjectile : MonoBehaviour
    {
        [Header("Projectile")]
        public MavCASProjectileKind kind = MavCASProjectileKind.Rocket;
        public Vector3 velocity;
        public float gravity = 9.81f;
        public float lifeTime = 12f;
        public bool useGravity = true;
        public bool alignToVelocity = true;

        [Header("Guidance")]
        public bool guided;
        public Transform guidedTarget;
        public bool guideToPoint;
        public Vector3 guidedPoint;
        public float guidanceDelay = 0.15f;
        public float guidanceStrength = 7.5f;
        public float maxTurnRateDeg = 55f;
        public float motorAcceleration = 30f;
        public float maxSpeed = 760f;
        public float proximityFuseRadius = 8f;

        [Header("Damage")]
        public float damage = 100f;
        public float radius = 20f;
        public string sourceName = "CASProjectile";
        public LayerMask hitMask = ~0;

        [Header("Asset/Visual")]
        [Tooltip("Optional visual root inside the prefab. If empty, this object's transform is used.")]
        public Transform visualRoot;
        [Tooltip("Object destroyed on impact. Usually the projectile root.")]
        public GameObject destroyRoot;
        public bool disableColliderAfterImpact = true;

        [Header("Debug")]
        public Vector3 lastPosition;
        public bool hasImpacted;
        public string status = "ready";

        private float spawnTime;
        private Collider[] colliders;

        public void Init(Vector3 startPosition, Vector3 initialVelocity, float dmg, float rad, string source, MavCASProjectileKind projectileKind)
        {
            transform.position = startPosition;
            lastPosition = startPosition;
            velocity = initialVelocity;
            damage = dmg;
            radius = rad;
            sourceName = source;
            kind = projectileKind;
            spawnTime = Time.time;
            status = "launched";

            if (destroyRoot == null)
                destroyRoot = gameObject;

            colliders = GetComponentsInChildren<Collider>(true);
        }

        public void SetGuidedTarget(Transform target)
        {
            guided = target != null;
            guidedTarget = target;
            guideToPoint = false;
        }

        public void SetGuidedPoint(Vector3 point)
        {
            guided = true;
            guideToPoint = true;
            guidedPoint = point;
        }

        private void Start()
        {
            spawnTime = Time.time;
            lastPosition = transform.position;

            if (destroyRoot == null)
                destroyRoot = gameObject;

            colliders = GetComponentsInChildren<Collider>(true);
        }

        private void Update()
        {
            if (hasImpacted)
                return;

            float dt = Time.deltaTime;
            Vector3 old = transform.position;

            UpdateGuidance(dt);

            if (useGravity)
                velocity += Vector3.down * gravity * dt;

            Vector3 next = old + velocity * dt;
            Vector3 delta = next - old;
            float dist = delta.magnitude;

            if (dist > 0.001f && Physics.Raycast(old, delta.normalized, out RaycastHit hit, dist, hitMask, QueryTriggerInteraction.Ignore))
            {
                // Ignore hitting our own trigger-only collider if the prefab has one.
                if (!hit.collider.transform.IsChildOf(transform))
                {
                    Impact(hit.point);
                    return;
                }
            }

            transform.position = next;

            if (guided && proximityFuseRadius > 0f)
            {
                Vector3 targetPoint = GetGuidancePoint();
                if (Vector3.Distance(transform.position, targetPoint) <= proximityFuseRadius)
                {
                    Impact(transform.position);
                    return;
                }
            }

            if (alignToVelocity && velocity.sqrMagnitude > 1f)
            {
                Quaternion rot = Quaternion.LookRotation(velocity.normalized, Vector3.up);
                if (visualRoot != null)
                    visualRoot.rotation = rot;
                else
                    transform.rotation = rot;
            }

            lastPosition = old;

            if (Time.time - spawnTime > lifeTime)
                Impact(transform.position);
        }

        private void UpdateGuidance(float dt)
        {
            if (!guided || Time.time - spawnTime < guidanceDelay)
                return;

            Vector3 targetPoint = GetGuidancePoint();
            Vector3 toTarget = targetPoint - transform.position;

            if (toTarget.sqrMagnitude < 1f)
                return;

            Vector3 desiredDir = toTarget.normalized;
            Vector3 currentDir = velocity.sqrMagnitude > 1f ? velocity.normalized : transform.forward;

            float maxRadians = maxTurnRateDeg * Mathf.Deg2Rad * dt;
            Vector3 newDir = Vector3.RotateTowards(currentDir, desiredDir, maxRadians, 0f).normalized;

            float currentSpeed = velocity.magnitude;
            float desiredSpeed = Mathf.Min(maxSpeed, currentSpeed + motorAcceleration * dt);
            Vector3 desiredVelocity = newDir * desiredSpeed;

            float blend = 1f - Mathf.Exp(-guidanceStrength * dt);
            velocity = Vector3.Lerp(velocity, desiredVelocity, blend);
            status = "guiding";
        }

        private Vector3 GetGuidancePoint()
        {
            if (!guideToPoint && guidedTarget != null)
                return guidedTarget.position;

            return guidedPoint;
        }

        public void Impact(Vector3 point)
        {
            if (hasImpacted)
                return;

            hasImpacted = true;
            status = "impact";
            transform.position = point;

            if (disableColliderAfterImpact && colliders != null)
            {
                foreach (Collider c in colliders)
                    if (c != null) c.enabled = false;
            }

            ApplyAreaDamage(point);

            if (destroyRoot != null)
                Destroy(destroyRoot, 0.08f);
            else
                Destroy(gameObject, 0.08f);
        }

        private void ApplyAreaDamage(Vector3 point)
        {
            MavCASTarget[] all = FindObjectsOfType<MavCASTarget>();

            foreach (MavCASTarget t in all)
            {
                if (t == null || !t.IsAlive())
                    continue;

                float d = Vector3.Distance(point, t.transform.position);
                if (d > radius)
                    continue;

                float falloff = 1f - Mathf.Clamp01(d / Mathf.Max(0.1f, radius));
                float finalDamage = damage * Mathf.Lerp(0.25f, 1f, falloff);
                t.ApplyDamage(finalDamage, sourceName);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = guided ? Color.magenta : Color.yellow;
            Gizmos.DrawLine(lastPosition, transform.position);
            if (radius > 1f)
                Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
