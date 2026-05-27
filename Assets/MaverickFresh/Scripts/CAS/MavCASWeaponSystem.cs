using UnityEngine;

namespace MaverickFresh
{
    public enum MavCASWeapon
    {
        Gun = 0,
        Rockets = 1,
        TrainingBomb = 2,
        PrecisionStrike = 3,
        Missile = 4
    }

    /// <summary>
    /// Simple CAS weapon system for gameplay prototyping.
    /// Not a real weapon simulation yet; it provides immediate CAS loop:
    /// designate -> select weapon -> fire -> damage ground targets.
    /// </summary>
    [DisallowMultipleComponent]
    public class MavCASWeaponSystem : MonoBehaviour
    {
        [Header("References")]
        public MavCASTargetingSystem targeting;
        public MavMouseFlightRig rig;
        public Camera playerCamera;
        public MavCASCCIPPredictor ccip;
        public MavCASOrdnanceAssets ordnanceAssets;

        [Header("Controls")]
        public KeyCode firePrimaryKey = KeyCode.Mouse0;
        public KeyCode fireSecondaryKey = KeyCode.Space;
        public KeyCode nextWeaponKey = KeyCode.Alpha2;
        public KeyCode prevWeaponKey = KeyCode.Alpha1;
        public KeyCode quickSelectMissileKey = KeyCode.Alpha3;
        public KeyCode quickSelectBombKey = KeyCode.Alpha4;

        [Header("Selected")]
        public MavCASWeapon selectedWeapon = MavCASWeapon.Gun;
        public MavCASWeapon selectedSecondaryWeapon = MavCASWeapon.Rockets;

        [Header("Ammo")]
        public int gunAmmo = 950;
        public int rocketAmmo = 28;
        public int bombAmmo = 6;
        public int precisionAmmo = 4;
        public int missileAmmo = 4;

        [Header("Gun")]
        public float gunDamage = 22f;
        public float gunRange = 2400f;
        public float gunCooldown = 0.055f;
        public float gunScreenRadius = 0.055f;
        public bool gunUsesFixedMuzzleDirection = true;
        public bool gunUseConvergence;
        public float gunConvergenceDistance = 800f;

        [Header("Rockets")]
        public float rocketDamage = 85f;
        public float rocketRadius = 18f;
        public float rocketCooldown = 0.35f;
        public float rocketForwardDrop = 650f;

        [Header("Training Bomb")]
        public float bombDamage = 150f;
        public float bombRadius = 40f;
        public float bombCooldown = 1.0f;
        public float bombDropForward = 950f;

        [Header("Precision Strike")]
        public float precisionDamage = 240f;
        public float precisionRadius = 28f;
        public float precisionCooldown = 1.4f;

        [Header("Guided Missile")]
        public float missileDamage = 260f;
        public float missileRadius = 30f;
        public float missileMuzzleSpeed = 430f;
        public float missileLife = 12f;
        public float missileCooldown = 1.2f;
        public float missileGuidanceDelay = 0.20f;
        public float missileGuidanceStrength = 8.0f;
        public float missileMaxTurnRateDeg = 50f;
        public float missileMotorAcceleration = 42f;
        public float missileMaxSpeed = 780f;

        [Header("Ballistic Sim-Lite")]
        public bool useBallisticProjectiles = true;
        public float gunMuzzleSpeed = 960f;
        public float rocketMuzzleSpeed = 520f;
        public float bombReleaseForwardFactor = 1.0f;
        public float projectileGravity = 9.81f;
        public float gunShellLife = 3.0f;
        public float rocketLife = 8.0f;
        public float bombLife = 14.0f;

        [Header("Runtime")]
        public string lastEvent = "ready";
        public Vector3 lastImpactPoint;
        public float lastImpactRadius;
        public int destroyedCount;
        public int hitCount;

        private static readonly MavCASWeapon[] SecondaryWeapons =
        {
            MavCASWeapon.Rockets,
            MavCASWeapon.TrainingBomb,
            MavCASWeapon.Missile,
            MavCASWeapon.PrecisionStrike
        };

        private float nextGunFireTime;
        private float nextRocketFireTime;
        private float nextBombFireTime;
        private float nextPrecisionFireTime;
        private float nextMissileFireTime;

        private void Awake()
        {
            Resolve();
        }

        private void Update()
        {
            Resolve();
            NormalizeSecondarySelection();

            if (MavFreshInput.GetKeyDown(nextWeaponKey))
                CycleWeapon(1);

            if (MavFreshInput.GetKeyDown(prevWeaponKey))
                CycleWeapon(-1);

            if (MavFreshInput.GetKeyDown(quickSelectMissileKey))
                SelectSecondaryWeapon(MavCASWeapon.Missile);

            if (MavFreshInput.GetKeyDown(quickSelectBombKey))
                SelectSecondaryWeapon(MavCASWeapon.TrainingBomb);

            if (MavFreshInput.GetKey(firePrimaryKey))
                TryFirePrimary();

            if (MavFreshInput.GetKeyDown(fireSecondaryKey))
                TryFireSecondary();
        }

        public void Resolve()
        {
            if (targeting == null) targeting = GetComponent<MavCASTargetingSystem>();
            if (rig == null) rig = FindObjectOfType<MavMouseFlightRig>();
            if (playerCamera == null) playerCamera = Camera.main;
            if (ccip == null) ccip = GetComponent<MavCASCCIPPredictor>();
            if (ordnanceAssets == null) ordnanceAssets = GetComponent<MavCASOrdnanceAssets>();
        }

        public void CycleWeapon(int dir)
        {
            NormalizeSecondarySelection();

            int index = GetSecondaryIndex(selectedSecondaryWeapon);
            int next = (index + dir + SecondaryWeapons.Length) % SecondaryWeapons.Length;
            SelectSecondaryWeapon(SecondaryWeapons[next]);
        }

        public bool TryFirePrimary()
        {
            selectedWeapon = MavCASWeapon.Gun;
            return FireGun();
        }

        public bool TryFireSecondary()
        {
            NormalizeSecondarySelection();
            selectedWeapon = selectedSecondaryWeapon;
            return TryFireWeapon(selectedSecondaryWeapon);
        }

        public bool TryFireSelected()
        {
            if (selectedWeapon != MavCASWeapon.Gun && IsSecondaryWeapon(selectedWeapon))
                selectedSecondaryWeapon = selectedWeapon;

            return TryFireWeapon(selectedWeapon);
        }

        private bool TryFireWeapon(MavCASWeapon weapon)
        {
            switch (weapon)
            {
                case MavCASWeapon.Gun:
                    return FireGun();

                case MavCASWeapon.Rockets:
                    return FireRocket();

                case MavCASWeapon.TrainingBomb:
                    return FireBomb();

                case MavCASWeapon.PrecisionStrike:
                    return FirePrecisionStrike();

                case MavCASWeapon.Missile:
                    return FireMissile();
            }

            return false;
        }

        private bool FireGun()
        {
            if (Time.time < nextGunFireTime)
                return false;

            if (gunAmmo <= 0)
            {
                lastEvent = "gun_empty";
                return false;
            }

            gunAmmo--;
            nextGunFireTime = Time.time + gunCooldown;

            if (useBallisticProjectiles)
            {
                SpawnProjectile(MavCASProjectileKind.GunShell, gunDamage, 3.5f, gunMuzzleSpeed, gunShellLife, false, "GunShell");
                lastEvent = "gun_shell_fire";
                return true;
            }

            MavCASTarget best = PickGunTarget();

            if (best != null)
            {
                bool wasAlive = best.IsAlive();
                best.ApplyDamage(gunDamage, "Gun");
                hitCount++;
                lastImpactPoint = best.transform.position;
                lastImpactRadius = 4f;
                lastEvent = "gun_hit_" + best.displayName;

                if (wasAlive && !best.IsAlive())
                    destroyedCount++;

                return true;
            }

            Vector3 point = transform.position + transform.forward * gunRange;
            if (playerCamera != null)
            {
                Vector2 aim = rig != null ? rig.cursorViewport : new Vector2(0.5f, 0.5f);
                Ray ray = playerCamera.ViewportPointToRay(new Vector3(aim.x, aim.y, 0f));
                point = ray.origin + ray.direction * gunRange;
            }

            lastImpactPoint = point;
            lastImpactRadius = 2f;
            lastEvent = "gun_miss";
            return true;
        }

        private MavCASTarget PickGunTarget()
        {
            if (playerCamera == null)
                return null;

            MavCASTarget[] all = FindObjectsOfType<MavCASTarget>();
            Vector2 aim = rig != null ? rig.cursorViewport : new Vector2(0.5f, 0.5f);

            MavCASTarget best = null;
            float bestDist = float.MaxValue;

            foreach (MavCASTarget t in all)
            {
                if (t == null || !t.IsAlive())
                    continue;

                float range = Vector3.Distance(transform.position, t.transform.position);
                if (range > gunRange)
                    continue;

                Vector3 vp = playerCamera.WorldToViewportPoint(t.transform.position + Vector3.up * 2f);
                if (vp.z <= 0f)
                    continue;

                float d = Vector2.Distance(new Vector2(vp.x, vp.y), aim);
                if (d < bestDist && d <= gunScreenRadius)
                {
                    bestDist = d;
                    best = t;
                }
            }

            return best;
        }

        private bool FireRocket()
        {
            if (Time.time < nextRocketFireTime)
                return false;

            if (rocketAmmo <= 0)
            {
                lastEvent = "rocket_empty";
                return false;
            }

            rocketAmmo--;
            nextRocketFireTime = Time.time + rocketCooldown;

            if (useBallisticProjectiles)
            {
                SpawnProjectile(MavCASProjectileKind.Rocket, rocketDamage, rocketRadius, rocketMuzzleSpeed, rocketLife, true, "Rocket");
                lastEvent = "rocket_launch";
                return true;
            }

            Vector3 point = GetUnguidedImpactPoint(rocketForwardDrop);
            ApplyAreaDamage(point, rocketRadius, rocketDamage, "Rocket");

            lastImpactPoint = point;
            lastImpactRadius = rocketRadius;
            lastEvent = "rocket_fire";
            return true;
        }

        private bool FireBomb()
        {
            if (Time.time < nextBombFireTime)
                return false;

            if (bombAmmo <= 0)
            {
                lastEvent = "bomb_empty";
                return false;
            }

            bombAmmo--;
            nextBombFireTime = Time.time + bombCooldown;

            if (useBallisticProjectiles)
            {
                SpawnBombProjectile();
                lastEvent = "bomb_release";
                return true;
            }

            Vector3 point = GetUnguidedImpactPoint(bombDropForward);
            ApplyAreaDamage(point, bombRadius, bombDamage, "TrainingBomb");

            lastImpactPoint = point;
            lastImpactRadius = bombRadius;
            lastEvent = "bomb_drop";
            return true;
        }

        private bool FirePrecisionStrike()
        {
            if (Time.time < nextPrecisionFireTime)
                return false;

            if (precisionAmmo <= 0)
            {
                lastEvent = "precision_empty";
                return false;
            }

            if (targeting == null || (!targeting.hasDesignatedPoint && targeting.GetDesignatedOrCandidateTarget() == null))
            {
                lastEvent = "precision_no_designation";
                return false;
            }

            precisionAmmo--;
            nextPrecisionFireTime = Time.time + precisionCooldown;

            Vector3 point = targeting.GetBestStrikePoint();
            ApplyAreaDamage(point, precisionRadius, precisionDamage, "PrecisionStrike");

            lastImpactPoint = point;
            lastImpactRadius = precisionRadius;
            lastEvent = "precision_strike";
            return true;
        }

        private bool FireMissile()
        {
            if (Time.time < nextMissileFireTime)
                return false;

            if (missileAmmo <= 0)
            {
                lastEvent = "missile_empty";
                return false;
            }

            if (targeting == null || (!targeting.hasDesignatedPoint && targeting.GetDesignatedOrCandidateTarget() == null))
            {
                lastEvent = "missile_no_designation";
                return false;
            }

            missileAmmo--;
            nextMissileFireTime = Time.time + missileCooldown;

            MavCASTarget target = targeting.GetDesignatedOrCandidateTarget();
            Vector3 point = targeting.GetBestStrikePoint();

            MavCASBallisticProjectile p = SpawnProjectile(
                MavCASProjectileKind.Missile,
                missileDamage,
                missileRadius,
                missileMuzzleSpeed,
                missileLife,
                false,
                "GuidedMissile"
            );

            if (p != null)
            {
                p.guidanceDelay = missileGuidanceDelay;
                p.guidanceStrength = missileGuidanceStrength;
                p.maxTurnRateDeg = missileMaxTurnRateDeg;
                p.motorAcceleration = missileMotorAcceleration;
                p.maxSpeed = missileMaxSpeed;
                p.proximityFuseRadius = Mathf.Max(6f, missileRadius * 0.35f);

                if (target != null)
                    p.SetGuidedTarget(target.transform);
                else
                    p.SetGuidedPoint(point);
            }

            lastImpactPoint = point;
            lastImpactRadius = missileRadius;
            lastEvent = "missile_launch";
            return true;
        }

        private void SelectSecondaryWeapon(MavCASWeapon weapon)
        {
            if (!IsSecondaryWeapon(weapon))
                weapon = MavCASWeapon.Rockets;

            selectedSecondaryWeapon = weapon;
            selectedWeapon = weapon;
            lastEvent = "secondary_" + selectedSecondaryWeapon;
        }

        private void NormalizeSecondarySelection()
        {
            if (!IsSecondaryWeapon(selectedSecondaryWeapon))
                selectedSecondaryWeapon = MavCASWeapon.Rockets;

            if (selectedWeapon != MavCASWeapon.Gun && IsSecondaryWeapon(selectedWeapon))
                selectedSecondaryWeapon = selectedWeapon;
        }

        private bool IsSecondaryWeapon(MavCASWeapon weapon)
        {
            for (int i = 0; i < SecondaryWeapons.Length; i++)
                if (SecondaryWeapons[i] == weapon)
                    return true;

            return false;
        }

        private int GetSecondaryIndex(MavCASWeapon weapon)
        {
            for (int i = 0; i < SecondaryWeapons.Length; i++)
                if (SecondaryWeapons[i] == weapon)
                    return i;

            return 0;
        }

        private Vector3 GetUnguidedImpactPoint(float forwardDistance)
        {
            if (targeting != null && targeting.hasAimGroundPoint)
                return targeting.aimGroundPoint;

            Vector3 point = transform.position + transform.forward * forwardDistance;
            if (Physics.Raycast(transform.position, transform.forward + Vector3.down * 0.25f, out RaycastHit hit, forwardDistance * 2f))
                point = hit.point;

            return point;
        }

        private void ApplyAreaDamage(Vector3 point, float radius, float damage, string source)
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
                float finalDamage = damage * Mathf.Lerp(0.35f, 1f, falloff);

                bool wasAlive = t.IsAlive();
                t.ApplyDamage(finalDamage, source);
                hitCount++;

                if (wasAlive && !t.IsAlive())
                    destroyedCount++;
            }
        }

        private MavCASBallisticProjectile SpawnProjectile(MavCASProjectileKind kind, float damage, float radius, float muzzleSpeed, float life, bool gravityOn, string source)
        {
            Transform spawn = ordnanceAssets != null ? ordnanceAssets.GetSpawnFor(kind) : null;

            Vector3 origin = spawn != null
                ? spawn.position
                : GetFallbackSpawnPosition(kind);

            Vector3 aimDir = spawn != null ? spawn.forward : transform.forward;

            if (kind == MavCASProjectileKind.GunShell)
            {
                origin = spawn != null ? spawn.position : GetFallbackGunMuzzlePosition();
                aimDir = GetGunFireDirection(spawn);
            }

            if ((kind == MavCASProjectileKind.Rocket || kind == MavCASProjectileKind.Missile || (kind == MavCASProjectileKind.GunShell && !gunUsesFixedMuzzleDirection)) && playerCamera != null && rig != null)
            {
                Vector2 aim = rig.cursorViewport;
                Ray ray = playerCamera.ViewportPointToRay(new Vector3(aim.x, aim.y, 0f));

                if (spawn == null && kind != MavCASProjectileKind.GunShell)
                    origin = ray.origin + ray.direction * 4f;

                aimDir = ray.direction.normalized;
            }

            if (aimDir.sqrMagnitude < 0.0001f)
                aimDir = transform.forward;

            Rigidbody rb = GetComponent<Rigidbody>();
            Vector3 aircraftVelocity = rb != null ? rb.linearVelocity : Vector3.zero;
            Vector3 initialVelocity = aircraftVelocity + aimDir * muzzleSpeed;

            GameObject go = CreateOrdnanceObject(kind, origin, aimDir);
            MavCASBallisticProjectile p = go.GetComponent<MavCASBallisticProjectile>();
            if (p == null)
                p = go.AddComponent<MavCASBallisticProjectile>();

            p.gravity = projectileGravity;
            p.lifeTime = life;
            p.useGravity = gravityOn;
            p.destroyRoot = go;
            p.Init(origin, initialVelocity, damage, radius, source, kind);

            if (kind == MavCASProjectileKind.Missile)
                p.guided = true;

            lastImpactPoint = ccip != null
                ? (kind == MavCASProjectileKind.Rocket ? ccip.predictedRocketImpact : ccip.predictedGunImpact)
                : origin + aimDir * 500f;
            lastImpactRadius = radius;

            return p;
        }

        private Vector3 GetGunFireDirection(Transform gunMuzzle)
        {
            if (!gunUsesFixedMuzzleDirection)
                return GetCurrentAimDirection();

            if (gunMuzzle != null)
            {
                if (gunUseConvergence)
                {
                    Vector3 convergencePoint = transform.position + transform.forward * Mathf.Max(1f, gunConvergenceDistance);
                    Vector3 toConvergence = convergencePoint - gunMuzzle.position;
                    if (toConvergence.sqrMagnitude > 0.0001f)
                        return toConvergence.normalized;
                }

                if (gunMuzzle.forward.sqrMagnitude > 0.0001f)
                    return gunMuzzle.forward.normalized;
            }

            return transform.forward;
        }

        private Vector3 GetCurrentAimDirection()
        {
            if (playerCamera != null)
            {
                Vector2 aim = rig != null ? rig.cursorViewport : new Vector2(0.5f, 0.5f);
                Ray ray = playerCamera.ViewportPointToRay(new Vector3(aim.x, aim.y, 0f));
                if (ray.direction.sqrMagnitude > 0.0001f)
                    return ray.direction.normalized;
            }

            return transform.forward;
        }

        private Vector3 GetFallbackSpawnPosition(MavCASProjectileKind kind)
        {
            if (kind == MavCASProjectileKind.GunShell)
                return GetFallbackGunMuzzlePosition();

            return transform.position + transform.forward * 11f + -transform.up * 1.2f;
        }

        private Vector3 GetFallbackGunMuzzlePosition()
        {
            return transform.position + transform.forward * 11.5f + transform.right * 0.85f + -transform.up * 1.1f;
        }

        private GameObject CreateOrdnanceObject(MavCASProjectileKind kind, Vector3 origin, Vector3 forward)
        {
            GameObject prefab = ordnanceAssets != null ? ordnanceAssets.GetPrefabFor(kind) : null;
            GameObject go;

            if (prefab != null)
            {
                go = Instantiate(prefab, origin, Quaternion.LookRotation(forward, Vector3.up));
                go.name = "MavCAS_" + kind + "_" + prefab.name;
                return go;
            }

            PrimitiveType primitive = PrimitiveType.Capsule;
            if (kind == MavCASProjectileKind.GunShell)
                primitive = PrimitiveType.Sphere;

            go = GameObject.CreatePrimitive(primitive);
            go.name = "MavCAS_" + kind;

            if (forward.sqrMagnitude > 0.001f)
                go.transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);

            Vector3 scale = ordnanceAssets != null ? ordnanceAssets.GetFallbackScaleFor(kind) : Vector3.one;
            if (ordnanceAssets == null)
            {
                if (kind == MavCASProjectileKind.GunShell) scale = Vector3.one * 0.8f;
                else if (kind == MavCASProjectileKind.Rocket) scale = new Vector3(1.4f, 3.5f, 1.4f);
                else if (kind == MavCASProjectileKind.Bomb) scale = new Vector3(2.0f, 5.0f, 2.0f);
                else if (kind == MavCASProjectileKind.Missile) scale = new Vector3(1.2f, 4.6f, 1.2f);
            }

            go.transform.localScale = scale;

            Collider col = go.GetComponent<Collider>();
            if (col != null)
                col.isTrigger = true;

            return go;
        }

        private MavCASBallisticProjectile SpawnBombProjectile()
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            Vector3 aircraftVelocity = rb != null ? rb.linearVelocity : transform.forward * 250f;

            Transform spawn = ordnanceAssets != null ? ordnanceAssets.GetSpawnFor(MavCASProjectileKind.Bomb) : null;
            Vector3 origin = spawn != null
                ? spawn.position
                : transform.position + -transform.up * 3f + transform.forward * 4f;

            Vector3 initialVelocity = aircraftVelocity * bombReleaseForwardFactor;

            GameObject go = CreateOrdnanceObject(MavCASProjectileKind.Bomb, origin, transform.forward);
            MavCASBallisticProjectile p = go.GetComponent<MavCASBallisticProjectile>();
            if (p == null)
                p = go.AddComponent<MavCASBallisticProjectile>();

            p.gravity = projectileGravity;
            p.lifeTime = bombLife;
            p.useGravity = true;
            p.destroyRoot = go;
            p.Init(origin, initialVelocity, bombDamage, bombRadius, "TrainingBomb", MavCASProjectileKind.Bomb);

            lastImpactPoint = ccip != null ? ccip.predictedBombImpact : origin + aircraftVelocity.normalized * 500f;
            lastImpactRadius = bombRadius;
            return p;
        }

        private void OnDrawGizmos()
        {
            if (lastImpactRadius > 0f)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(lastImpactPoint, lastImpactRadius);
                Gizmos.DrawLine(lastImpactPoint, lastImpactPoint + Vector3.up * 30f);
            }
        }
    }
}
