using UnityEngine;

namespace MaverickFresh
{
    /// <summary>
    /// Installs CAS starter components onto the Fresh aircraft.
    /// Can be placed on MaverickFresh_Manager or the aircraft.
    /// </summary>
    [DisallowMultipleComponent]
    public class MavCASStarterBootstrap : MonoBehaviour
    {
        [Header("References")]
        public GameObject aircraftObject;

        [Header("Install")]
        public bool setupOnAwake = true;
        public bool addTargeting = true;
        public bool addWeapons = true;
        public bool addRangeSpawner = false;
        public bool addTargetingPod = true;
        public bool addTGPStateManager = true;

        [Header("Created")]
        public MavCASTargetingSystem targeting;
        public MavCASWeaponSystem weapons;
        public MavCASCCIPPredictor ccip;
        public MavCASOrdnanceAssets ordnanceAssets;
        public MavCASTestRangeSpawner rangeSpawner;
        public MavTargetingPodSystem targetingPod;
        public MavTGPStateManager tgpStateManager;

        private void Awake()
        {
            if (setupOnAwake)
                Setup();
        }

        [ContextMenu("Setup CAS Starter")]
        public void Setup()
        {
            if (aircraftObject == null)
                aircraftObject = MavPlayerResolver.FindPlayerObject();

            if (aircraftObject == null)
            {
                Debug.LogWarning("MavCASStarterBootstrap: aircraftObject missing.");
                return;
            }

            if (addTargeting)
            {
                targeting = aircraftObject.GetComponent<MavCASTargetingSystem>();
                if (targeting == null)
                    targeting = aircraftObject.AddComponent<MavCASTargetingSystem>();

                targeting.rig = FindObjectOfType<MavMouseFlightRig>();
                targeting.playerCamera = Camera.main;
            }

            if (addWeapons)
            {
                ordnanceAssets = aircraftObject.GetComponent<MavCASOrdnanceAssets>();
                if (ordnanceAssets == null)
                    ordnanceAssets = aircraftObject.AddComponent<MavCASOrdnanceAssets>();

                ccip = aircraftObject.GetComponent<MavCASCCIPPredictor>();
                if (ccip == null)
                    ccip = aircraftObject.AddComponent<MavCASCCIPPredictor>();

                ccip.rig = FindObjectOfType<MavMouseFlightRig>();
                ccip.playerCamera = Camera.main;
                ccip.aircraftRb = aircraftObject.GetComponent<Rigidbody>();

                weapons = aircraftObject.GetComponent<MavCASWeaponSystem>();
                if (weapons == null)
                    weapons = aircraftObject.AddComponent<MavCASWeaponSystem>();

                weapons.targeting = targeting;
                weapons.rig = FindObjectOfType<MavMouseFlightRig>();
                weapons.playerCamera = Camera.main;
                weapons.ccip = ccip;
                weapons.ordnanceAssets = ordnanceAssets;
            }

            if (addTargetingPod)
            {
                targetingPod = aircraftObject.GetComponent<MavTargetingPodSystem>();
                if (targetingPod == null)
                    targetingPod = aircraftObject.AddComponent<MavTargetingPodSystem>();

                targetingPod.casTargeting = targeting;
                targetingPod.casWeapons = weapons;
            }

            if (addTGPStateManager)
            {
                tgpStateManager = aircraftObject.GetComponent<MavTGPStateManager>();
                if (tgpStateManager == null)
                    tgpStateManager = aircraftObject.AddComponent<MavTGPStateManager>();

                tgpStateManager.targetingPod = aircraftObject.GetComponent<MavTargetingPodSystem>();
                tgpStateManager.forceOffOnStart = true;
                tgpStateManager.startHidden = true;
                tgpStateManager.SetOff();
            }

            if (addRangeSpawner)
            {
                rangeSpawner = GetComponent<MavCASTestRangeSpawner>();
                if (rangeSpawner == null)
                    rangeSpawner = gameObject.AddComponent<MavCASTestRangeSpawner>();
            }
        }
    }
}
