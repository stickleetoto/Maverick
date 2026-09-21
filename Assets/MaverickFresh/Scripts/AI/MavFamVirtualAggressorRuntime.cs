using UnityEngine;
using UnityEngine.SceneManagement;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MaverickFresh
{
    /// <summary>
    /// Development-only runtime entry point for the first FAM virtual opponent.
    ///
    /// The actual opponent is still built by MavEnemyF15Spawner, so the spawned aircraft
    /// uses Maverick's existing Rigidbody + MavMouseFlightJet + aero/engine stack.
    /// This component only installs a small runtime host in flight scenes and exposes
    /// explicit spawn/despawn controls. It never auto-spawns an opponent.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MavFamVirtualAggressorRuntime : MonoBehaviour
    {
        public const string RuntimeHostName = "Mav_FAM_VirtualAggressorRuntime";

        [Header("Runtime Controls")]
        public bool hotkeysEnabled = true;
        public bool logActions = true;

        [Header("Opponent")]
        public MavEnemyF15Spawner spawner;

        [Header("Runtime")]
        public string status = "idle";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryInstallRuntimeHost();
        }

        private static void TryInstallRuntimeHost()
        {
            if (GameObject.Find(RuntimeHostName) != null)
                return;

            GameObject player = MavPlayerResolver.FindPlayerObject();
            if (player == null)
                return;

            GameObject host = new GameObject(RuntimeHostName);
            MavFamVirtualAggressorRuntime runtime = host.AddComponent<MavFamVirtualAggressorRuntime>();
            runtime.EnsureSpawner();
            runtime.status = "ready";
        }

        private void Awake()
        {
            EnsureSpawner();
        }

        private void Update()
        {
            if (!hotkeysEnabled)
                return;

            if (SpawnPressedThisFrame())
                SpawnOne();

            if (DespawnPressedThisFrame())
                DespawnCurrent();
        }

        [ContextMenu("Spawn FAM Virtual Aggressor")]
        public GameObject SpawnOne()
        {
            EnsureSpawner();
            if (spawner == null)
            {
                status = "spawner_missing";
                return null;
            }

            GameObject enemy = spawner.SpawnEnemy();
            status = enemy != null ? "aggressor_active" : "spawn_failed";

            if (logActions)
                Debug.Log("[Maverick/FAM] Virtual aggressor spawn: " + status, this);

            return enemy;
        }

        [ContextMenu("Despawn FAM Virtual Aggressor")]
        public void DespawnCurrent()
        {
            EnsureSpawner();
            if (spawner == null)
            {
                status = "spawner_missing";
                return;
            }

            GameObject enemy = spawner.enemyObject;
            if (enemy == null && !string.IsNullOrEmpty(spawner.enemyName))
                enemy = GameObject.Find(spawner.enemyName);

            if (enemy == null)
            {
                status = "no_aggressor";
                return;
            }

            if (Application.isPlaying)
                Destroy(enemy);
            else
                DestroyImmediate(enemy);

            spawner.enemyObject = null;
            spawner.status = "despawned";
            status = "despawned";

            if (logActions)
                Debug.Log("[Maverick/FAM] Virtual aggressor despawned.", this);
        }

        private void EnsureSpawner()
        {
            if (spawner == null)
                spawner = GetComponent<MavEnemyF15Spawner>();

            if (spawner == null)
                spawner = gameObject.AddComponent<MavEnemyF15Spawner>();

            // R0 contract: one explicit hostile F-15 opponent, no automatic spawn.
            spawner.spawnOnStart = false;
            spawner.spawnOnlyIfMissing = true;
            spawner.attachAIPilot = true;
            spawner.attachLBMBrain = true;
            spawner.playerObjectName = MavPlayerResolver.DefaultPlayerName;
        }

        private static bool SpawnPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.f8Key.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.F8);
#else
            return false;
#endif
        }

        private static bool DespawnPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.f9Key.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.F9);
#else
            return false;
#endif
        }
    }
}
