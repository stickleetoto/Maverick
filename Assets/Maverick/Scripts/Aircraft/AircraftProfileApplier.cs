using UnityEngine;
using EaglePhysicalAI.Safety;
using EaglePhysicalAI.Sensors.Radar;
using EaglePhysicalAI.Sensors.TargetingPod;

namespace EaglePhysicalAI.Aircraft
{
    /// <summary>
    /// Applies a gameplay aircraft training profile to the current aircraft and sensor suite.
    /// </summary>
    public class AircraftProfileApplier : MonoBehaviour
    {
        public AircraftTrainingProfileKind defaultProfileKind = AircraftTrainingProfileKind.F15EStyleCAS;
        public AircraftTrainingProfile customProfile;
        public bool applyOnAwake = true;
        public bool reapplyWithHotkeys = true;
        public KeyCode applyF15EKey = KeyCode.Home;
        public KeyCode applyF22Key = KeyCode.End;

        [Header("Resolved References")]
        public AircraftPhysicsController aircraft;
        public FlightEnvelopeSafetyGuard safetyGuard;
        public F15ERadarSystem radar;
        public TargetingPodSystem targetingPod;
        public string lastAppliedProfile = "none";

        private void Awake()
        {
            ResolveReferences();
            if (applyOnAwake) ApplyDefaultProfile();
        }

        private void Update()
        {
            if (!reapplyWithHotkeys) return;
            if (MaverickInput.GetKeyDown(applyF15EKey)) ApplyProfile(AircraftTrainingProfile.CreateF15EStyle());
            if (MaverickInput.GetKeyDown(applyF22Key)) ApplyProfile(AircraftTrainingProfile.CreateF22Style());
        }

        [ContextMenu("Apply Default Profile")]
        public void ApplyDefaultProfile()
        {
            if (customProfile != null)
            {
                ApplyProfile(customProfile);
                return;
            }

            if (defaultProfileKind == AircraftTrainingProfileKind.F22StyleTestbed)
            {
                ApplyProfile(AircraftTrainingProfile.CreateF22Style());
            }
            else
            {
                ApplyProfile(AircraftTrainingProfile.CreateF15EStyle());
            }
        }

        public void ApplyProfile(AircraftTrainingProfile profile)
        {
            if (profile == null) return;
            ResolveReferences();
            profile.Apply(aircraft, safetyGuard, radar, targetingPod);
            lastAppliedProfile = profile.profileName;
        }

        private void ResolveReferences()
        {
            if (aircraft == null) aircraft = GetComponent<AircraftPhysicsController>();
            if (safetyGuard == null) safetyGuard = GetComponent<FlightEnvelopeSafetyGuard>();
            if (radar == null) radar = GetComponent<F15ERadarSystem>();
            if (targetingPod == null) targetingPod = GetComponent<TargetingPodSystem>();
        }
    }
}
