using MaverickFresh.Combat.Legacy;
using MaverickFresh.Combat.Sensors;
using MaverickFresh.Combat.Targeting;
using MaverickFresh.Combat;
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

            InstallAirToGround();

            // ALWAYS, and deliberately outside the A2G freeze. This composition root installs both stacks,
            // and the A2A one - track owner, engagement view, radar, the lock authority - is the path this
            // phase exists to leave running. Freezing A2G by returning early from Setup would have taken
            // the whole of A2A with it, which is the one mistake this method's shape invites.
            InstallTargetTrackCore(aircraftObject);
        }

        /// <summary>
        /// Installs the legacy air-to-ground stack: CAS targeting, CAS weapons, CCIP, ordnance, the
        /// targeting pod and its state manager.
        ///
        /// DORMANT. Combat development is A2A-first and A2G is frozen, so this installs nothing - which is
        /// the OUTERMOST barrier and the one that matters most. If the components are never added there is
        /// nothing to disable, nothing reading input, and nothing writing a designation. The per-component
        /// guards behind this one exist for aircraft that already carry these components in scene or prefab
        /// data, which this bootstrap never sees.
        ///
        /// <c>setupOnAwake</c>, <c>addTargeting</c> and the rest are left alone on purpose. They are `true`
        /// in the scenes that exist, and editing scene data would hide this decision somewhere it cannot be
        /// reviewed or asserted. A scene with all of them set is now harmless.
        /// </summary>
        private void InstallAirToGround()
        {
            if (MavCombatScopePolicy.AirToGroundFrozen)
                return;

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

            // Radar Core R0. Registers as a second feed beside the legacy one; the owner is unchanged
            // by its arrival. The legacy feed deliberately STAYS registered: it is the architectural
            // coverage the radar has to match before anything is removed, and keeping both is also what
            // proves two feeds with identical local keys stay separate tracks.
            MavRadarSensor radar = aircraftObject.GetComponent<MavRadarSensor>();
            if (radar == null)
                radar = aircraftObject.AddComponent<MavRadarSensor>();
            radar.owner = owner;
            owner.RegisterFeed(radar);

            // Radar track/lock R0. The one authoritative lock authority. It reads tracks and decides
            // commitment; it does not detect, and the radar knows nothing about it.
            MavTrackLockController lockController = aircraftObject.GetComponent<MavTrackLockController>();
            if (lockController == null)
                lockController = aircraftObject.AddComponent<MavTrackLockController>();
            lockController.owner = owner;
            lockController.engagementView = view;

            MavLegacyEngagementProbe probe = aircraftObject.GetComponent<MavLegacyEngagementProbe>();
            if (probe == null)
                probe = aircraftObject.AddComponent<MavLegacyEngagementProbe>();
            probe.owner = owner;
            probe.view = view;
            probe.feed = feed;
            probe.casTargeting = aircraftObject.GetComponent<MavCASTargetingSystem>();
            probe.sensorSuite = aircraftObject.GetComponent<MavF22SensorSuite>();
            probe.targetingPod = aircraftObject.GetComponent<MavTargetingPodSystem>();
        }
    }
}
