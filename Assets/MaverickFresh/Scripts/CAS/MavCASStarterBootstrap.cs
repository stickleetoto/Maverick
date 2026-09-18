using MaverickFresh.Combat.Legacy;
using MaverickFresh.Combat.Targeting;
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

            InstallTargetTrackCore(aircraftObject);
        }

        /// <summary>
        /// Installs the TargetTrack Core observer stack beside the legacy CAS stack.
        ///
        /// Purely observational, which is what makes it safe to install by default: the track owner
        /// collects observations, the legacy feed reports today's markers, and the engagement probe
        /// reads the three lock authorities without writing to any of them. Nothing here fires,
        /// designates, locks or clears anything, and removing all four components returns the
        /// aircraft to exactly its previous behavior.
        ///
        /// It is wired here, in the existing composition root, rather than from a new bootstrap of
        /// its own, so there is still one place that decides what a Maverick aircraft is made of.
        /// </summary>
        private void InstallTargetTrackCore(GameObject aircraftObject)
        {
            if (aircraftObject == null)
                return;

            MavTargetTrackOwner owner = aircraftObject.GetComponent<MavTargetTrackOwner>();
            if (owner == null)
                owner = aircraftObject.AddComponent<MavTargetTrackOwner>();

            MavEngagementView view = aircraftObject.GetComponent<MavEngagementView>();
            if (view == null)
                view = aircraftObject.AddComponent<MavEngagementView>();

            MavLegacyTargetObservationFeed feed = aircraftObject.GetComponent<MavLegacyTargetObservationFeed>();
            if (feed == null)
                feed = aircraftObject.AddComponent<MavLegacyTargetObservationFeed>();
            feed.owner = owner;
            owner.RegisterFeed(feed);

            MavLegacyEngagementProbe probe = aircraftObject.GetComponent<MavLegacyEngagementProbe>();
            if (probe == null)
                probe = aircraftObject.AddComponent<MavLegacyEngagementProbe>();
            probe.owner = owner;
            probe.view = view;
            probe.casTargeting = aircraftObject.GetComponent<MavCASTargetingSystem>();
            probe.sensorSuite = aircraftObject.GetComponent<MavF22SensorSuite>();
            probe.targetingPod = aircraftObject.GetComponent<MavTargetingPodSystem>();
        }
    }
}
