using UnityEngine;
using EaglePhysicalAI.Battlefield;

namespace EaglePhysicalAI.CAS
{
    public enum MaverickPrimaryWeapon
    {
        TrainingGun = 0,
        SensorMark = 1
    }

    public enum MaverickSecondaryWeapon
    {
        AbstractCasStrike = 0,
        TrainingBomb = 1
    }

    /// <summary>
    /// Lightweight War-Thunder-like weapon selector abstraction.
    /// It does not implement real weapons; it routes keys to existing abstract CAS systems.
    /// </summary>
    public class MaverickWeaponSelector : MonoBehaviour
    {
        public AbstractStrikeSystem strikeSystem;
        public MaverickPrimaryWeapon primary = MaverickPrimaryWeapon.TrainingGun;
        public MaverickSecondaryWeapon secondary = MaverickSecondaryWeapon.AbstractCasStrike;

        [Header("Keys")]
        public KeyCode firePrimaryKey = KeyCode.Mouse0;
        public KeyCode fireSecondaryKey = KeyCode.Space;
        public KeyCode switchPrimaryKey = KeyCode.Alpha1;
        public KeyCode switchSecondaryKey = KeyCode.Alpha2;
        public KeyCode confirmKey = KeyCode.Return;
        public KeyCode abortKey = KeyCode.Backspace;

        [Header("Debug")]
        public string lastWeaponEvent = "none";

        private void Awake()
        {
            if (strikeSystem == null) strikeSystem = GetComponent<AbstractStrikeSystem>();
        }

        private void Update()
        {
            if (MaverickInput.GetKeyDown(switchPrimaryKey)) CyclePrimary();
            if (MaverickInput.GetKeyDown(switchSecondaryKey)) CycleSecondary();

            if (MaverickInput.GetKeyDown(firePrimaryKey)) FirePrimary();
            if (MaverickInput.GetKeyDown(fireSecondaryKey)) FireSecondary();

            if (MaverickInput.GetKeyDown(abortKey)) lastWeaponEvent = "abort_selected_weapon";
        }

        public void CyclePrimary()
        {
            primary = primary == MaverickPrimaryWeapon.TrainingGun ? MaverickPrimaryWeapon.SensorMark : MaverickPrimaryWeapon.TrainingGun;
            lastWeaponEvent = "primary_" + primary;
        }

        public void CycleSecondary()
        {
            secondary = secondary == MaverickSecondaryWeapon.AbstractCasStrike ? MaverickSecondaryWeapon.TrainingBomb : MaverickSecondaryWeapon.AbstractCasStrike;
            lastWeaponEvent = "secondary_" + secondary;
        }

        public void FirePrimary()
        {
            // TrainingGun is intentionally abstract for now.
            lastWeaponEvent = "primary_fire_" + primary;
        }

        public void FireSecondary()
        {
            if (secondary == MaverickSecondaryWeapon.AbstractCasStrike && strikeSystem != null)
            {
                bool ok = strikeSystem.TryStrike();
                lastWeaponEvent = ok ? "secondary_cas_strike_ok" : "secondary_cas_strike_blocked";
            }
            else
            {
                lastWeaponEvent = "secondary_fire_" + secondary;
            }
        }
    }
}
