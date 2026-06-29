using System.Reflection;
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
        public int gunAmmo = 1200;
        public int rocketAmmo = 28;
        public int bombAmmo = 6;
        public int precisionAmmo = 4;
        public int missileAmmo = 4;

        [Header("Gun")]
        public float gunDamage = 12f;
        public float gunRange = 3200f;
        [Tooltip("Seconds between rounds. 0.018 = about 3300 RPM visual/gameplay rate.")]
        public float gunCooldown = 0.018f;
        public float gunScreenRadius = 0.055f;
        public bool gunUsesFixedMuzzleDirection = true;
        public bool gunUseConvergence;
        public float gunConvergenceDistance = 850f;

        [Header("Gun Neon Tracer Pass")]
        public bool useNeonGunTracers = true;
        public bool useHitscanGunWhenNeon = true;
        public bool damageAircraftWithGun = true;
        public int gunRoundsPerTriggerStep = 1;
        public float gunSpreadDeg = 0.22f;
        public float gunTracerDuration = 0.045f;
        public float gunTracerWidth = 0.055f;
        public Color gunTracerColor = new Color(0.12f, 0.92f, 1.0f, 1.0f);
        public bool spawnTracerOnMiss = true;

        [Header("Detailed Gun Model")]
        public bool useDetailedGunModel = true;
        [Tooltip("Approximate visual/gameplay rounds per second. 100 = 6000 RPM.")]
        public float gunRoundsPerSecond = 82f;
        public int maxGunRoundsPerFrame = 8;
        public bool useGunSpinUp = true;
        public float gunSpinUpSeconds = 0.18f;
        public float gunSpinDownSeconds = 0.34f;
        [Range(0f, 1f)] public float minSpinToFire = 0.32f;
        public bool consumeAmmoPerRound = true;

        [Header("Detailed Gun Spread / Heat")]
        public float gunBaseSpreadDeg = 0.12f;
        public float gunHeatSpreadDeg = 0.42f;
        public float gunSustainedFireSpreadDeg = 0.18f;
        public float gunHighGSpreadDeg = 0.20f;
        public float gunHeatPerRound = 0.012f;
        public float gunHeatCoolingPerSecond = 0.72f;
        public float gunOverheatThreshold = 0.92f;
        public float gunOverheatCooldownThreshold = 0.55f;
        public bool gunCanOverheat = true;

        [Header("Detailed Gun Visual Rhythm")]
        public int tracerEveryNRounds = 2;
        public int sparkEveryNRounds = 1;
        public bool brightTracerOnLastRound = true;
        public float gunTracerJitterMeters = 0.35f;
        public float gunMuzzleFlashScale = 0.34f;
        public float gunMuzzleFlashLife = 0.028f;

        [Header("Detailed Gun Recoil")]
        [Tooltip("Disabled by default. Modern aircraft gun recoil is not applied to flight physics in Maverick.")]
        public bool applyGunRecoil = false;
        public float gunRecoilForce = 0f;
        public float gunRecoilTorque = 0f;

        [Header("Gun Debug")]
        [Range(0f, 1f)] public float debugGunSpin;
        [Range(0f, 1f)] public float debugGunHeat;
        public bool debugGunOverheated;
        public float debugGunRps;
        public float debugGunDispersionDeg;
        public int debugGunRoundsFired;

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

        [Header("Projectile Velocity Inheritance")]
        public bool projectileInheritsAircraftVelocity = true;
        public float projectileVelocityInheritanceFactor = 0.75f;
        public float muzzleSpawnForwardOffset = 1.5f;
        public bool ignoreAircraftCollisionsForProjectiles = true;
        public float projectileSelfCollisionIgnoreTime = 0.35f;

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

        private float gunShotAccumulator;
        private int gunRoundSequence;
        private float gunSustainedFireTimer;
        private MavGunTracerImpactVfx gunVfx;
        private Rigidbody cachedRigidbody;

        private void Awake()
        {
            Resolve();
        }

        private void Update()
        {
            Resolve();
            NormalizeSecondarySelection();

            bool primaryHeld = MavFreshInput.GetKey(firePrimaryKey);
            UpdateDetailedGunRuntime(primaryHeld);

            if (MavFreshInput.GetKeyDown(nextWeaponKey))
                CycleWeapon(1);

            if (MavFreshInput.GetKeyDown(prevWeaponKey))
                CycleWeapon(-1);

            if (MavFreshInput.GetKeyDown(quickSelectMissileKey))
                SelectSecondaryWeapon(MavCASWeapon.Missile);

            if (MavFreshInput.GetKeyDown(quickSelectBombKey))
                SelectSecondaryWeapon(MavCASWeapon.TrainingBomb);

            if (primaryHeld)
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
            if (gunVfx == null) gunVfx = GetComponent<MavGunTracerImpactVfx>();
            if (cachedRigidbody == null) cachedRigidbody = GetComponent<Rigidbody>();
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
            if (gunAmmo <= 0)
            {
                lastEvent = "gun_empty";
                return false;
            }

            if (!useDetailedGunModel)
                return FireGunLegacy();

            if (gunCanOverheat && debugGunOverheated)
            {
                lastEvent = "gun_overheated";
                return false;
            }

            if (useGunSpinUp && debugGunSpin < minSpinToFire)
            {
                lastEvent = "gun_spinup";
                SpawnMuzzleFlash(GetFallbackGunMuzzlePosition(), 0.45f);
                return false;
            }

            float rps = Mathf.Max(1f, gunRoundsPerSecond) * Mathf.Clamp01(useGunSpinUp ? debugGunSpin : 1f);
            debugGunRps = rps;
            gunShotAccumulator += rps * Time.deltaTime;

            int requested = Mathf.FloorToInt(gunShotAccumulator);
            int rounds = Mathf.Clamp(requested, 0, Mathf.Max(1, maxGunRoundsPerFrame));
            if (rounds <= 0)
                return false;

            bool anyHit = false;
            for (int i = 0; i < rounds; i++)
            {
                if (consumeAmmoPerRound)
                {
                    if (gunAmmo <= 0)
                    {
                        lastEvent = "gun_empty";
                        break;
                    }
                    gunAmmo--;
                }

                bool visibleTracer = ShouldSpawnTracerForRound(i, rounds);
                bool visibleSpark = (sparkEveryNRounds <= 1) || (gunRoundSequence % Mathf.Max(1, sparkEveryNRounds) == 0);
                anyHit |= FireNeonHitscanGunRound(visibleTracer, visibleSpark);

                debugGunRoundsFired++;
                gunRoundSequence++;
                debugGunHeat = Mathf.Clamp01(debugGunHeat + Mathf.Max(0f, gunHeatPerRound));
                gunSustainedFireTimer = Mathf.Min(4f, gunSustainedFireTimer + 0.035f);
                // No flight-physics recoil: keep gun effects visual only.
            }

            gunShotAccumulator -= rounds;
            nextGunFireTime = Time.time + Mathf.Max(0.001f, 1f / Mathf.Max(1f, rps));
            if (gunCanOverheat && debugGunHeat >= gunOverheatThreshold)
                debugGunOverheated = true;

            if (!consumeAmmoPerRound)
                gunAmmo--;

            return true;
        }

        private bool FireGunLegacy()
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

            if (useNeonGunTracers && useHitscanGunWhenNeon)
            {
                bool anyHit = false;
                int rounds = Mathf.Max(1, gunRoundsPerTriggerStep);
                for (int i = 0; i < rounds; i++)
                    anyHit |= FireNeonHitscanGunRound(true, true);
                return true;
            }

            if (useBallisticProjectiles)
            {
                MavCASBallisticProjectile shell = SpawnProjectile(MavCASProjectileKind.GunShell, gunDamage, 3.5f, gunMuzzleSpeed, gunShellLife, false, "GunShell");
                if (useNeonGunTracers && shell != null)
                    MavNeonTracer.Spawn(shell.transform.position, shell.transform.position + shell.velocity.normalized * Mathf.Min(gunRange, 180f), gunTracerDuration, gunTracerWidth, gunTracerColor);
                lastEvent = "gun_shell_fire";
                return true;
            }

            return FireNeonHitscanGunRound(true, true);
        }

        private void UpdateDetailedGunRuntime(bool triggerHeld)
        {
            float dt = Mathf.Max(0f, Time.deltaTime);
            if (!useDetailedGunModel)
                return;

            float spinTarget = triggerHeld ? 1f : 0f;
            float spinSeconds = triggerHeld ? gunSpinUpSeconds : gunSpinDownSeconds;
            float spinRate = spinSeconds <= 0.001f ? 999f : 1f / spinSeconds;
            debugGunSpin = Mathf.MoveTowards(debugGunSpin, spinTarget, spinRate * dt);

            if (!triggerHeld)
            {
                gunShotAccumulator = 0f;
                gunSustainedFireTimer = Mathf.MoveTowards(gunSustainedFireTimer, 0f, 1.6f * dt);
            }

            debugGunHeat = Mathf.MoveTowards(debugGunHeat, 0f, Mathf.Max(0f, gunHeatCoolingPerSecond) * dt);
            if (debugGunOverheated && debugGunHeat <= gunOverheatCooldownThreshold)
                debugGunOverheated = false;

            debugGunDispersionDeg = ComputeCurrentGunSpreadDeg();
        }

        private bool ShouldSpawnTracerForRound(int roundIndexInFrame, int frameRounds)
        {
            if (!useNeonGunTracers)
                return false;
            if (tracerEveryNRounds <= 1)
                return true;
            if (brightTracerOnLastRound && roundIndexInFrame == frameRounds - 1)
                return true;
            return (gunRoundSequence % Mathf.Max(1, tracerEveryNRounds)) == 0;
        }

        private bool FireNeonHitscanGunRound(bool visibleTracer = true, bool visibleSpark = true)
        {
            Transform gunMuzzle = ordnanceAssets != null ? ordnanceAssets.GetSpawnFor(MavCASProjectileKind.GunShell) : null;
            Vector3 origin = gunMuzzle != null ? gunMuzzle.position : GetFallbackGunMuzzlePosition();
            Vector3 direction = GetGunFireDirection(gunMuzzle);
            if (direction.sqrMagnitude < 0.0001f)
                direction = transform.forward;

            direction = ApplyGunSpread(direction.normalized);
            origin += direction * Mathf.Max(0f, muzzleSpawnForwardOffset);

            Vector3 visualEnd = origin + direction * gunRange;
            bool hitSomething = false;
            bool aircraftHit = false;
            Vector3 hitNormal = -direction;

            if (Physics.Raycast(origin, direction, out RaycastHit hit, gunRange, ~0, QueryTriggerInteraction.Ignore))
            {
                visualEnd = hit.point;
                hitNormal = hit.normal;
                hitSomething = true;

                MavCASTarget target = hit.collider.GetComponentInParent<MavCASTarget>();
                if (target != null && target.IsAlive())
                {
                    bool wasAlive = target.IsAlive();
                    target.ApplyDamage(gunDamage, "DetailedGun");
                    hitCount++;
                    lastImpactPoint = hit.point;
                    lastImpactRadius = 4f;
                    lastEvent = "gun_hit_" + target.displayName;

                    if (wasAlive && !target.IsAlive())
                        destroyedCount++;
                }
                else if (damageAircraftWithGun && TryDamageAircraft(hit.collider, gunDamage, hit.point, "DetailedGun"))
                {
                    aircraftHit = true;
                    hitCount++;
                    lastImpactPoint = hit.point;
                    lastImpactRadius = 4f;
                    lastEvent = "gun_hit_aircraft";
                }
                else
                {
                    lastImpactPoint = hit.point;
                    lastImpactRadius = 2f;
                    lastEvent = "gun_spark";
                }
            }
            else
            {
                lastImpactPoint = visualEnd;
                lastImpactRadius = 2f;
                lastEvent = "gun_miss";
            }

            if (visibleTracer && useNeonGunTracers && (hitSomething || spawnTracerOnMiss))
            {
                Vector3 jitter = Random.insideUnitSphere * Mathf.Max(0f, gunTracerJitterMeters);
                MavNeonTracer.SpawnAdvanced(origin, visualEnd + jitter, gunTracerDuration, gunTracerWidth, gunTracerColor, debugGunHeat);
            }

            if (gunVfx != null)
                gunVfx.NotifyGunRound(origin, visualEnd, hitSomething && visibleSpark, hitNormal, aircraftHit, debugGunHeat);

            if (visibleTracer)
                SpawnMuzzleFlash(origin, 1f);

            return hitSomething;
        }

        private Vector3 ApplyGunSpread(Vector3 direction)
        {
            float spread = ComputeCurrentGunSpreadDeg();
            if (spread <= 0.001f)
                return direction;

            Vector2 disk = Random.insideUnitCircle;
            Quaternion random = Quaternion.Euler(disk.y * spread, disk.x * spread, Random.Range(-spread, spread) * 0.15f);
            return (random * direction).normalized;
        }

        private float ComputeCurrentGunSpreadDeg()
        {
            float spread = useDetailedGunModel ? gunBaseSpreadDeg : gunSpreadDeg;
            if (useDetailedGunModel)
            {
                spread += debugGunHeat * gunHeatSpreadDeg;
                spread += Mathf.Clamp01(gunSustainedFireTimer / 2.5f) * gunSustainedFireSpreadDeg;
                spread += EstimateCurrentGLoad01() * gunHighGSpreadDeg;
            }
            else
            {
                spread = Mathf.Max(0f, gunSpreadDeg);
            }
            return Mathf.Max(0f, spread);
        }

        private float EstimateCurrentGLoad01()
        {
            MavMouseFlightJet jet = GetComponent<MavMouseFlightJet>();
            if (jet == null)
                return 0f;
            float g = Mathf.Abs(jet.gEstimate);
            return Mathf.Clamp01((g - 1f) / 8f);
        }

        private void ApplyGunRecoil()
        {
            // Intentionally no-op by default.
            // Maverick treats the gun as visual/ballistic feedback only; it must not kick the aircraft physics.
            if (!applyGunRecoil)
                return;
            if (gunRecoilForce <= 0f)
                return;
            if (cachedRigidbody == null)
                cachedRigidbody = GetComponent<Rigidbody>();
            if (cachedRigidbody == null)
                return;

            // Optional debug-only recoil for experiments. Keep zero/default off.
            cachedRigidbody.AddForce(-transform.forward * gunRecoilForce, ForceMode.Force);
        }

        private void SpawnMuzzleFlash(Vector3 origin, float intensity)
        {
            if (gunMuzzleFlashScale <= 0f || gunMuzzleFlashLife <= 0f)
                return;

            GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flash.name = "Mav_Gun_Muzzle_Flash";
            flash.transform.position = origin;
            flash.transform.localScale = Vector3.one * gunMuzzleFlashScale * Mathf.Clamp(intensity, 0.2f, 2.0f);
            Collider col = flash.GetComponent<Collider>();
            if (col != null) col.enabled = false;
            Renderer r = flash.GetComponent<Renderer>();
            if (r != null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                if (shader == null) shader = Shader.Find("Standard");
                Material mat = new Material(shader);
                mat.color = new Color(gunTracerColor.r, gunTracerColor.g, gunTracerColor.b, 0.95f);
                r.material = mat;
            }
            Destroy(flash, Mathf.Max(0.005f, gunMuzzleFlashLife));
        }

        private bool TryDamageAircraft(Collider collider, float amount, Vector3 point, string source)
        {
            if (collider == null)
                return false;

            Component[] components = collider.GetComponentsInParent<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null)
                    continue;
                if (component.GetType().Name != "MavAircraftDamageState")
                    continue;

                if (!ReflectionIsAlive(component))
                    return false;

                bool wasAlive = ReflectionIsAlive(component);
                bool applied = ReflectionApplyDamage(component, amount, source, point);
                if (!applied)
                    return false;
                bool aliveNow = ReflectionIsAlive(component);
                if (wasAlive && !aliveNow)
                    destroyedCount++;
                return true;
            }

            return false;
        }

        private bool ReflectionIsAlive(Component component)
        {
            MethodInfo method = component.GetType().GetMethod("IsAlive", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null)
                return true;
            object result = method.Invoke(component, null);
            return result is bool b ? b : true;
        }

        private bool ReflectionApplyDamage(Component component, float amount, string source, Vector3 point)
        {
            MethodInfo method = component.GetType().GetMethod("ApplyDamage", new[] { typeof(float), typeof(string), typeof(Vector3) });
            if (method != null)
            {
                method.Invoke(component, new object[] { amount, source, point });
                return true;
            }
            method = component.GetType().GetMethod("ApplyDamage", new[] { typeof(float), typeof(string) });
            if (method != null)
            {
                method.Invoke(component, new object[] { amount, source });
                return true;
            }
            method = component.GetType().GetMethod("TakeDamage", new[] { typeof(float) });
            if (method != null)
            {
                method.Invoke(component, new object[] { amount });
                return true;
            }
            return false;
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

            if ((kind == MavCASProjectileKind.Rocket || kind == MavCASProjectileKind.Missile) && playerCamera != null && rig != null)
            {
                Vector2 aim = rig.cursorViewport;
                Ray ray = playerCamera.ViewportPointToRay(new Vector3(aim.x, aim.y, 0f));

                if (spawn == null && kind != MavCASProjectileKind.GunShell)
                    origin = ray.origin + ray.direction * 4f;

                aimDir = ray.direction.normalized;
            }

            if (aimDir.sqrMagnitude < 0.0001f)
                aimDir = transform.forward;

            aimDir = aimDir.normalized;
            origin += aimDir * Mathf.Max(0f, muzzleSpawnForwardOffset);

            Rigidbody rb = GetComponent<Rigidbody>();
            Vector3 aircraftVelocity = rb != null ? rb.linearVelocity : Vector3.zero;
            Vector3 inheritedVelocity = projectileInheritsAircraftVelocity
                ? aircraftVelocity * projectileVelocityInheritanceFactor
                : Vector3.zero;
            Vector3 initialVelocity = aimDir * muzzleSpeed + inheritedVelocity;

            GameObject go = CreateOrdnanceObject(kind, origin, aimDir);
            MavCASBallisticProjectile p = go.GetComponent<MavCASBallisticProjectile>();
            if (p == null)
                p = go.AddComponent<MavCASBallisticProjectile>();

            p.gravity = projectileGravity;
            p.lifeTime = life;
            p.useGravity = gravityOn;
            p.destroyRoot = go;
            p.inheritedVelocity = inheritedVelocity;
            p.Init(origin, initialVelocity, damage, radius, source, kind);
            p.SetIgnoredRoot(transform, projectileSelfCollisionIgnoreTime);
            IgnoreProjectileAircraftCollisions(go);

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

            if (!gunUsesFixedMuzzleDirection)
                return transform.forward;

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
            p.SetIgnoredRoot(transform, projectileSelfCollisionIgnoreTime);
            IgnoreProjectileAircraftCollisions(go);

            lastImpactPoint = ccip != null ? ccip.predictedBombImpact : origin + aircraftVelocity.normalized * 500f;
            lastImpactRadius = bombRadius;
            return p;
        }

        private void IgnoreProjectileAircraftCollisions(GameObject projectileRoot)
        {
            if (!ignoreAircraftCollisionsForProjectiles || projectileRoot == null)
                return;

            Collider[] projectileColliders = projectileRoot.GetComponentsInChildren<Collider>(true);
            Collider[] aircraftColliders = GetComponentsInChildren<Collider>(true);

            foreach (Collider projectileCollider in projectileColliders)
            {
                if (projectileCollider == null)
                    continue;

                foreach (Collider aircraftCollider in aircraftColliders)
                {
                    if (aircraftCollider == null || aircraftCollider == projectileCollider)
                        continue;

                    Physics.IgnoreCollision(projectileCollider, aircraftCollider, true);
                }
            }
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
