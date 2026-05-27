using UnityEngine;

namespace MaverickFresh
{
    /// <summary>
    /// Assign your imported asset prefabs here.
    ///
    /// Recommended prefab setup:
    /// - Prefab root has MavCASBallisticProjectile or it will be added at runtime.
    /// - Visual mesh is child/root.
    /// - Collider may be trigger or normal; projectile uses raycast travel anyway.
    /// - Forward axis should be prefab +Z if possible.
    /// </summary>
    [DisallowMultipleComponent]
    public class MavCASOrdnanceAssets : MonoBehaviour
    {
        [Header("Projectile Prefabs")]
        public GameObject gunShellPrefab;
        public GameObject rocketPrefab;
        public GameObject bombPrefab;
        public GameObject missilePrefab;

        [Header("Optional Spawn Points")]
        public Transform gunMuzzle;
        public Transform leftRocketRail;
        public Transform rightRocketRail;
        public Transform bombPylon;
        public Transform missileRail;

        [Header("Fallback primitive scale")]
        public Vector3 fallbackGunScale = Vector3.one * 0.8f;
        public Vector3 fallbackRocketScale = new Vector3(1.4f, 3.5f, 1.4f);
        public Vector3 fallbackBombScale = new Vector3(2.0f, 5.0f, 2.0f);
        public Vector3 fallbackMissileScale = new Vector3(1.2f, 4.6f, 1.2f);

        [Header("Runtime")]
        public bool alternateRocketRail = true;
        public bool lastRocketLeft;

        public Transform GetRocketSpawn()
        {
            if (leftRocketRail == null && rightRocketRail == null)
                return null;

            if (!alternateRocketRail)
                return leftRocketRail != null ? leftRocketRail : rightRocketRail;

            lastRocketLeft = !lastRocketLeft;

            if (lastRocketLeft && leftRocketRail != null)
                return leftRocketRail;

            if (!lastRocketLeft && rightRocketRail != null)
                return rightRocketRail;

            return leftRocketRail != null ? leftRocketRail : rightRocketRail;
        }

        public Transform GetSpawnFor(MavCASProjectileKind kind)
        {
            switch (kind)
            {
                case MavCASProjectileKind.GunShell:
                    return gunMuzzle;

                case MavCASProjectileKind.Rocket:
                    return GetRocketSpawn();

                case MavCASProjectileKind.Bomb:
                    return bombPylon;

                case MavCASProjectileKind.Missile:
                    return missileRail;
            }

            return null;
        }

        public GameObject GetPrefabFor(MavCASProjectileKind kind)
        {
            switch (kind)
            {
                case MavCASProjectileKind.GunShell:
                    return gunShellPrefab;

                case MavCASProjectileKind.Rocket:
                    return rocketPrefab;

                case MavCASProjectileKind.Bomb:
                    return bombPrefab;

                case MavCASProjectileKind.Missile:
                    return missilePrefab;
            }

            return null;
        }

        public Vector3 GetFallbackScaleFor(MavCASProjectileKind kind)
        {
            switch (kind)
            {
                case MavCASProjectileKind.GunShell:
                    return fallbackGunScale;

                case MavCASProjectileKind.Rocket:
                    return fallbackRocketScale;

                case MavCASProjectileKind.Bomb:
                    return fallbackBombScale;

                case MavCASProjectileKind.Missile:
                    return fallbackMissileScale;
            }

            return Vector3.one;
        }
    }
}
