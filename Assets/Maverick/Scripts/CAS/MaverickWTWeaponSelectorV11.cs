using UnityEngine;
using EaglePhysicalAI.Controls;
using EaglePhysicalAI.Battlefield;
using EaglePhysicalAI.Sensors.TargetingPod;

namespace EaglePhysicalAI.CAS
{
    [DisallowMultipleComponent]
    public class MaverickWTWeaponSelectorV11 : MonoBehaviour
    {
        public AbstractStrikeSystem strikeSystem;
        public TargetingPodSystem pod;
        public MaverickWTKeybindProfileV11 keys;

        [Header("Selected Weapons")]
        public MaverickWTPrimaryWeapon primary = MaverickWTPrimaryWeapon.Cannon;
        public MaverickWTSecondaryWeapon secondary = MaverickWTSecondaryWeapon.AbstractCAS;

        [Header("Gameplay")]
        public bool requirePodDesignationForCAS = false;
        public float cannonTrainingRange = 1200f;
        public string lastWeaponEvent = "ready";

        private void Awake()
        {
            if (strikeSystem == null) strikeSystem = GetComponent<AbstractStrikeSystem>();
            if (pod == null) pod = GetComponent<TargetingPodSystem>();
            if (keys == null) keys = GetComponent<MaverickWTKeybindProfileV11>();
            if (keys == null) keys = gameObject.AddComponent<MaverickWTKeybindProfileV11>();
        }

        private void Update()
        {
            if (MaverickInput.GetKeyDown(keys.switchPrimary)) CyclePrimary();
            if (MaverickInput.GetKeyDown(keys.switchSecondary)) CycleSecondary();
            if (MaverickInput.GetKeyDown(keys.firePrimary)) FirePrimary();
            if (MaverickInput.GetKeyDown(keys.fireSecondary)) FireSecondary();
            if (MaverickInput.GetKeyDown(keys.abort)) lastWeaponEvent = "abort";
        }

        public void CyclePrimary()
        {
            primary = primary == MaverickWTPrimaryWeapon.Cannon ? MaverickWTPrimaryWeapon.SensorMark : MaverickWTPrimaryWeapon.Cannon;
            lastWeaponEvent = "primary_" + primary;
        }

        public void CycleSecondary()
        {
            if (secondary == MaverickWTSecondaryWeapon.AbstractCAS) secondary = MaverickWTSecondaryWeapon.TrainingBomb;
            else if (secondary == MaverickWTSecondaryWeapon.TrainingBomb) secondary = MaverickWTSecondaryWeapon.DesignatorOnly;
            else secondary = MaverickWTSecondaryWeapon.AbstractCAS;

            lastWeaponEvent = "secondary_" + secondary;
        }

        public void FirePrimary()
        {
            if (primary == MaverickWTPrimaryWeapon.SensorMark && pod != null)
            {
                bool ok = pod.TryDesignateCurrentTarget();
                lastWeaponEvent = ok ? "sensor_mark_ok" : "sensor_mark_failed";
                return;
            }

            lastWeaponEvent = "cannon_training_fire";
        }

        public void FireSecondary()
        {
            if (secondary == MaverickWTSecondaryWeapon.DesignatorOnly)
            {
                if (pod != null)
                {
                    bool ok = pod.TryDesignateCurrentTarget();
                    lastWeaponEvent = ok ? "designator_only_ok" : "designator_only_failed";
                }
                return;
            }

            if (secondary == MaverickWTSecondaryWeapon.AbstractCAS || secondary == MaverickWTSecondaryWeapon.TrainingBomb)
            {
                if (strikeSystem == null)
                {
                    lastWeaponEvent = "no_strike_system";
                    return;
                }

                if (requirePodDesignationForCAS && (pod == null || pod.designatedGroundUnit == null))
                {
                    lastWeaponEvent = "cas_blocked_no_pod_designation";
                    return;
                }

                GroundUnit target = pod != null && pod.designatedGroundUnit != null ? pod.designatedGroundUnit : null;
                bool ok = strikeSystem.TryStrike(target);
                lastWeaponEvent = ok ? "secondary_strike_ok" : "secondary_strike_blocked";
            }
        }
    }
}
