using UnityEngine;

namespace MaverickFresh
{
    public class MavInGameBootstrap : MonoBehaviour
    {
        [Header("Manual aircraft visual slots (optional)")]
        public GameObject f15exVisual;
        public GameObject f16Visual;
        public GameObject f18Visual;
        public GameObject f22Visual;
        public GameObject f35Visual;

        [Header("Legacy slot aliases (kept for old inspector assignments)")]
        public GameObject f15Prefab;
        public GameObject f16Prefab;
        public GameObject fa18Prefab;
        public GameObject f22Prefab;
        public GameObject f35Prefab;

        [Header("Manual Mav_Player Root")]
        public bool useGenericPlayerRoot = true;
        public string playerObjectName = "Mav_Player";
        public bool createPlayerIfMissing = false;
        public bool autoInstallFlightComponentsOnManualPlayer = true;
        public bool applyAirStartToManualPlayer = true;

        [Header("Visual Switching")]
        public bool autoFindAircraftVisuals = true;
        public bool claimLooseSceneVisuals = false;
        public bool createPlaceholderVisualIfMissing = false;
        public bool preclaimAllAircraftVisuals = true;
        public bool enableRuntimeAircraftHotkeys = false; // deprecated: aircraft switching is now hangar-only by default.

        [Header("Startup")]
        public bool setupOnStart = true;
        public bool useSessionSelection = true;
        public MavAircraftKind fallbackAircraft = MavAircraftKind.F22A;
        public MavGameMode fallbackMode = MavGameMode.FreeFlight;
        public bool createEnvironment = true;
        public bool spawnModeTargets = true;
        public bool installCAS = true;
        public bool installPhysicalAI = true;

        [Header("Enemy Aircraft")]
        public bool spawnEnemyF15InDogfight = true;
        public bool spawnEnemyF15InFreeFlight = false;
        public GameObject enemyF15VisualSource;
        public Vector3 enemyF15SpawnOffsetLocal = new Vector3(850f, 130f, 2400f);
        public float enemyF15StartSpeed = 285f;

        [Header("Runtime")]
        public GameObject aircraftObject;
        public MavAircraftRuntimeProfile activeProfile;
        public MavFreshBootstrap freshBootstrap;
        public MavAircraftProfileApplier profileApplier;
        public MavAircraftVisualSwitcher visualSwitcher;
        public MavInGameMenuOverlay menuOverlay;
        public MavEnemyF15Spawner enemyF15Spawner;
        public bool playerWasMissing;
        public string setupStatus;
        private MavGameMode activeMode = MavGameMode.FreeFlight;

        private void Start()
        {
            if (setupOnStart)
                SetupInGame();
        }

        [ContextMenu("Setup In-Game")]
        public void SetupInGame()
        {
            MavAircraftKind aircraft = useSessionSelection && MavGameSession.HasSelection ? MavGameSession.SelectedAircraft : fallbackAircraft;
            MavGameMode mode = useSessionSelection ? MavGameSession.SelectedMode : fallbackMode;
            activeMode = mode;
            activeProfile = MavAircraftCatalog.GetBuiltIn(aircraft);

            if (createEnvironment)
                BuildEnvironment();

            Camera cam = EnsureCamera();
            aircraftObject = ResolvePlayerRoot();
            if (aircraftObject == null)
            {
                playerWasMissing = true;
                setupStatus = "Missing Mav_Player. Create a GameObject named '" + playerObjectName + "' in Mav_InGame and put aircraft visuals under it.";
                MavGameSession.LastSceneError = setupStatus;
                Debug.LogError("MavInGameBootstrap: " + setupStatus);
                return;
            }

            playerWasMissing = false;
            MavGameSession.LastSceneError = string.Empty;

            if (applyAirStartToManualPlayer && activeProfile != null)
            {
                aircraftObject.transform.position = new Vector3(0f, activeProfile.startAltitude, 0f);
                aircraftObject.transform.rotation = Quaternion.identity;
            }

            Rigidbody rb = aircraftObject.GetComponent<Rigidbody>();
            if (rb == null && autoInstallFlightComponentsOnManualPlayer)
                rb = aircraftObject.AddComponent<Rigidbody>();

            if (autoInstallFlightComponentsOnManualPlayer && aircraftObject.GetComponent<MavAeroBody>() == null)
                aircraftObject.AddComponent<MavAeroBody>();

            if (autoInstallFlightComponentsOnManualPlayer && aircraftObject.GetComponent<MavAtmosphericEngine>() == null)
                aircraftObject.AddComponent<MavAtmosphericEngine>();

            if (autoInstallFlightComponentsOnManualPlayer && aircraftObject.GetComponent<MavCombatFlapSystem>() == null)
                aircraftObject.AddComponent<MavCombatFlapSystem>();

            if (autoInstallFlightComponentsOnManualPlayer && aircraftObject.GetComponent<MavThrustVectorControl>() == null)
                aircraftObject.AddComponent<MavThrustVectorControl>();

            if (autoInstallFlightComponentsOnManualPlayer && aircraftObject.GetComponent<MavRadarSignature>() == null)
                aircraftObject.AddComponent<MavRadarSignature>();

            if (autoInstallFlightComponentsOnManualPlayer && aircraftObject.GetComponent<MavF22SensorSuite>() == null)
                aircraftObject.AddComponent<MavF22SensorSuite>();

            ConfigureVisualSwitcherForPlayer();

            freshBootstrap = gameObject.GetComponent<MavFreshBootstrap>();
            if (freshBootstrap == null && autoInstallFlightComponentsOnManualPlayer)
                freshBootstrap = gameObject.AddComponent<MavFreshBootstrap>();

            if (freshBootstrap != null)
            {
                freshBootstrap.setupOnAwake = false;
                freshBootstrap.aircraftObject = aircraftObject;
                freshBootstrap.genericPlayerName = playerObjectName;
                freshBootstrap.createGenericPlayerIfMissing = false;
                freshBootstrap.applyLegacyF15Profile = false;
                freshBootstrap.mainCamera = cam;
                freshBootstrap.installCASStarter = installCAS;
                freshBootstrap.installPhysicalAIStarter = installPhysicalAI && (mode == MavGameMode.Dogfight || mode == MavGameMode.FreeFlight);
                freshBootstrap.applyAirStart = applyAirStartToManualPlayer;
                freshBootstrap.startAltitude = activeProfile.startAltitude;
                freshBootstrap.startSpeed = activeProfile.startSpeed;
                freshBootstrap.startEulerAngles = Vector3.zero;
                freshBootstrap.jetThrust = activeProfile.thrust;
                freshBootstrap.mouseSensitivity = activeProfile.mouseSensitivity;
                freshBootstrap.aimDistance = activeProfile.aimDistance;
                freshBootstrap.SetupFreshMouseFlight();
            }

            if (visualSwitcher != null)
                visualSwitcher.ApplyAircraft(activeProfile, GetVisualSource(aircraft));

            profileApplier = aircraftObject.GetComponent<MavAircraftProfileApplier>();
            if (profileApplier == null && autoInstallFlightComponentsOnManualPlayer)
                profileApplier = aircraftObject.AddComponent<MavAircraftProfileApplier>();

            if (profileApplier != null)
            {
                profileApplier.applyOnStart = false;
                profileApplier.renameObject = false;
                profileApplier.ApplyProfile(activeProfile);
            }

            rb = aircraftObject.GetComponent<Rigidbody>();
            if (rb != null && applyAirStartToManualPlayer)
            {
                rb.linearVelocity = aircraftObject.transform.forward * activeProfile.startSpeed;
                rb.angularVelocity = Vector3.zero;
            }

            if (spawnModeTargets)
                BuildMode(mode);

            if (cam != null)
            {
                menuOverlay = cam.GetComponent<MavInGameMenuOverlay>();
                if (menuOverlay == null)
                    menuOverlay = cam.gameObject.AddComponent<MavInGameMenuOverlay>();
                menuOverlay.profile = activeProfile;
                menuOverlay.mode = mode;

                MavSensorHudOverlay sensorHud = cam.GetComponent<MavSensorHudOverlay>();
                if (sensorHud == null)
                    sensorHud = cam.gameObject.AddComponent<MavSensorHudOverlay>();
                sensorHud.sensor = aircraftObject.GetComponent<MavF22SensorSuite>();
                sensorHud.playerCamera = cam;
            }

            setupStatus = "Ready: " + activeProfile.displayName + " from hangar selection.";
        }

        /// <summary>
        /// Development-only switching. Final flow should use Mav_Hangar -> MavGameSession -> Mav_InGame.
        /// </summary>
        public void DebugSwitchAircraft(MavAircraftKind aircraft)
        {
            if (!enableRuntimeAircraftHotkeys)
                return;
            ApplySelectedAircraftInPlace(aircraft, true);
        }

        public void ApplySelectedAircraftInPlace(MavAircraftKind aircraft, bool saveToSession)
        {
            activeProfile = MavAircraftCatalog.GetBuiltIn(aircraft);
            if (activeProfile == null || aircraftObject == null)
                return;

            if (saveToSession)
                MavGameSession.SelectAircraft(aircraft);

            ConfigureVisualSwitcherForPlayer();
            if (visualSwitcher != null)
                visualSwitcher.ApplyAircraft(activeProfile, GetVisualSource(aircraft));

            if (profileApplier == null)
                profileApplier = aircraftObject.GetComponent<MavAircraftProfileApplier>();
            if (profileApplier == null && autoInstallFlightComponentsOnManualPlayer)
                profileApplier = aircraftObject.AddComponent<MavAircraftProfileApplier>();

            if (profileApplier != null)
            {
                profileApplier.applyOnStart = false;
                profileApplier.renameObject = false;
                profileApplier.ApplyProfile(activeProfile);
            }

            MavFreshHud hud = Camera.main != null ? Camera.main.GetComponent<MavFreshHud>() : null;
            if (hud != null)
                hud.jet = aircraftObject.GetComponent<MavMouseFlightJet>();

            if (menuOverlay != null)
            {
                menuOverlay.profile = activeProfile;
                menuOverlay.mode = activeMode;
            }
        }

        private GameObject ResolvePlayerRoot()
        {
            if (aircraftObject != null)
                return aircraftObject;

            if (useGenericPlayerRoot && !string.IsNullOrEmpty(playerObjectName))
            {
                GameObject existing = GameObject.Find(playerObjectName);
                if (existing != null)
                    return existing;
            }

            MavMouseFlightJet existingJet = FindObjectOfType<MavMouseFlightJet>();
            if (existingJet != null)
                return existingJet.gameObject;

            if (createPlayerIfMissing)
            {
                GameObject player = new GameObject(string.IsNullOrEmpty(playerObjectName) ? "Mav_Player" : playerObjectName);
                return player;
            }

            return null;
        }

        private void ConfigureVisualSwitcherForPlayer()
        {
            if (aircraftObject == null)
                return;

            visualSwitcher = aircraftObject.GetComponent<MavAircraftVisualSwitcher>();
            if (visualSwitcher == null && autoInstallFlightComponentsOnManualPlayer)
                visualSwitcher = aircraftObject.AddComponent<MavAircraftVisualSwitcher>();
            if (visualSwitcher == null)
                return;

            visualSwitcher.f15Visual = FirstNonNull(f15exVisual, f15Prefab);
            visualSwitcher.f16Visual = FirstNonNull(f16Visual, f16Prefab);
            visualSwitcher.fa18Visual = FirstNonNull(f18Visual, fa18Prefab);
            visualSwitcher.f22Visual = FirstNonNull(f22Visual, f22Prefab);
            visualSwitcher.f35Visual = FirstNonNull(f35Visual, f35Prefab);
            visualSwitcher.autoFindSceneVisuals = autoFindAircraftVisuals;
            visualSwitcher.claimLooseSceneVisuals = claimLooseSceneVisuals;
            visualSwitcher.createPlaceholderIfMissing = createPlaceholderVisualIfMissing;
            visualSwitcher.preclaimAllResolvedVisuals = preclaimAllAircraftVisuals;
            visualSwitcher.renameRuntimeVisuals = false;
            visualSwitcher.AutoResolveAllSceneVisuals();
        }

        private static GameObject FirstNonNull(GameObject preferred, GameObject legacy)
        {
            return preferred != null ? preferred : legacy;
        }

        private Camera EnsureCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject camGo = new GameObject("Main Camera");
                cam = camGo.AddComponent<Camera>();
                cam.tag = "MainCamera";
            }
            cam.enabled = true;
            cam.depth = 100;
            return cam;
        }

        private void BuildEnvironment()
        {
            if (GameObject.Find("Mav_InGame_Sun") == null)
            {
                GameObject lightGo = new GameObject("Mav_InGame_Sun");
                Light l = lightGo.AddComponent<Light>();
                l.type = LightType.Directional;
                l.intensity = 1.2f;
                lightGo.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            }

            if (GameObject.Find("Mav_InGame_Ground") == null)
            {
                GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.name = "Mav_InGame_Ground";
                ground.transform.position = new Vector3(0f, -2f, 1600f);
                ground.transform.localScale = new Vector3(6000f, 1f, 6000f);
            }
        }

        private void BuildMode(MavGameMode mode)
        {
            if (mode == MavGameMode.TestRange || mode == MavGameMode.GroundAttack)
            {
                MavCASTestRangeSpawner range = gameObject.GetComponent<MavCASTestRangeSpawner>();
                if (range == null)
                    range = gameObject.AddComponent<MavCASTestRangeSpawner>();
                range.buildOnStart = false;
                range.targetRows = mode == MavGameMode.GroundAttack ? 3 : 2;
                range.targetsPerRow = mode == MavGameMode.GroundAttack ? 6 : 5;
                range.rangeCenter = new Vector3(0f, 0f, 1800f);
                range.BuildRange();
            }

            if (mode == MavGameMode.Dogfight || mode == MavGameMode.FreeFlight)
            {
                MavAirTargetSpawner air = gameObject.GetComponent<MavAirTargetSpawner>();
                if (air == null)
                    air = gameObject.AddComponent<MavAirTargetSpawner>();
                air.buildOnStart = false;
                air.droneCount = mode == MavGameMode.Dogfight ? 3 : 3;
                air.centerAltitude = activeProfile != null ? activeProfile.startAltitude - 250f : 900f;
                air.BuildTargets();
            }

            if ((mode == MavGameMode.Dogfight && spawnEnemyF15InDogfight) || (mode == MavGameMode.FreeFlight && spawnEnemyF15InFreeFlight))
                BuildEnemyF15();

            if (mode == MavGameMode.CarrierTest)
                BuildCarrierPlaceholder();
        }


        private void BuildEnemyF15()
        {
            enemyF15Spawner = gameObject.GetComponent<MavEnemyF15Spawner>();
            if (enemyF15Spawner == null)
                enemyF15Spawner = gameObject.AddComponent<MavEnemyF15Spawner>();

            enemyF15Spawner.spawnOnStart = false;
            enemyF15Spawner.spawnOnlyIfMissing = true;
            enemyF15Spawner.playerObjectName = playerObjectName;
            enemyF15Spawner.f15VisualSource = FirstNonNull(enemyF15VisualSource, FirstNonNull(f15exVisual, f15Prefab));
            enemyF15Spawner.spawnOffsetLocal = enemyF15SpawnOffsetLocal;
            enemyF15Spawner.startSpeed = enemyF15StartSpeed;
            enemyF15Spawner.enemyAircraft = MavAircraftKind.F15E;
            enemyF15Spawner.SpawnEnemy();
        }

        private void BuildCarrierPlaceholder()
        {
            GameObject carrier = GameObject.Find("Mav_Carrier_Prototype");
            if (carrier != null) return;

            carrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
            carrier.name = "Mav_Carrier_Prototype";
            carrier.transform.position = new Vector3(0f, 2f, 2200f);
            carrier.transform.localScale = new Vector3(90f, 8f, 360f);

            GameObject deck = GameObject.CreatePrimitive(PrimitiveType.Cube);
            deck.name = "Mav_Carrier_Deck_Line";
            deck.transform.SetParent(carrier.transform, true);
            deck.transform.position = carrier.transform.position + new Vector3(0f, 5f, 0f);
            deck.transform.localScale = new Vector3(8f, 0.25f, 340f);
        }

        private GameObject GetVisualSource(MavAircraftKind kind)
        {
            if (kind == MavAircraftKind.F15E) return FirstNonNull(f15exVisual, f15Prefab);
            if (kind == MavAircraftKind.F16C) return FirstNonNull(f16Visual, f16Prefab);
            if (kind == MavAircraftKind.FA18E) return FirstNonNull(f18Visual, fa18Prefab);
            if (kind == MavAircraftKind.F22A) return FirstNonNull(f22Visual, f22Prefab);
            if (kind == MavAircraftKind.F35A) return FirstNonNull(f35Visual, f35Prefab);
            return null;
        }
    }
}
