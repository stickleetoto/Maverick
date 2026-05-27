using UnityEngine;
using EaglePhysicalAI.Aircraft;
using EaglePhysicalAI.Controls;

namespace EaglePhysicalAI.Aircraft
{
    /// <summary>
    /// War-Thunder-inspired aircraft systems layer.
    /// Handles flaps, airbrake, gear visual state, simplified engine assist, and applies gameplay modifiers
    /// to the abstract AircraftPhysicsController.
    /// </summary>
    [DisallowMultipleComponent]
    public class MaverickWTAircraftSystemsV11 : MonoBehaviour
    {
        [Header("References")]
        public AircraftPhysicsController aircraft;
        public SimpleLandingGearController visualGear;
        public MaverickWTKeybindProfileV11 keys;

        [Header("State")]
        public MaverickFlapState flaps = MaverickFlapState.Retracted;
        public bool airbrakeDeployed;
        public bool gearDown;
        public MaverickEngineAssistMode engineAssist = MaverickEngineAssistMode.Auto;
        public float manualThrottleLimit = 1f;
        public bool wepActive;

        [Header("Flap / Airbrake Modifiers")]
        public float combatFlapLiftBonus = 0.10f;
        public float takeoffFlapLiftBonus = 0.18f;
        public float landingFlapLiftBonus = 0.28f;
        public float combatFlapDragBonus = 0.008f;
        public float takeoffFlapDragBonus = 0.018f;
        public float landingFlapDragBonus = 0.035f;
        public float airbrakeDragBonus = 0.075f;
        public float gearDragBonus = 0.026f;
        public float flapPitchAuthorityBonus = 0.08f;
        public float flapStallAngleBonus = 2.0f;

        [Header("Auto Assist")]
        public bool autoRetractFlapsAtHighSpeed = true;
        public float combatFlapMaxSpeed = 220f;
        public float takeoffFlapMaxSpeed = 160f;
        public float landingFlapMaxSpeed = 115f;

        [Header("Debug")]
        public string lastSystemEvent = "ready";
        public float effectiveLift;
        public float effectiveDrag;
        public float baseLift;
        public float baseDrag;
        public float basePitchTorque;
        public float baseStallAngle;
        private bool initialized;

        private void Awake()
        {
            if (aircraft == null) aircraft = GetComponent<AircraftPhysicsController>();
            if (visualGear == null) visualGear = GetComponent<SimpleLandingGearController>();
            if (keys == null) keys = GetComponent<MaverickWTKeybindProfileV11>();
            if (keys == null) keys = gameObject.AddComponent<MaverickWTKeybindProfileV11>();
            CacheBaseValues();
        }

        private void CacheBaseValues()
        {
            if (aircraft == null) return;
            baseLift = aircraft.liftCoefficient;
            baseDrag = aircraft.dragCoefficient;
            basePitchTorque = aircraft.pitchTorque;
            baseStallAngle = aircraft.stallAngleDegrees;
            initialized = true;
        }

        private void Update()
        {
            if (aircraft == null) return;
            if (!initialized) CacheBaseValues();

            HandleKeys();
            ApplyAutoProtection();
            ApplyModifiers();
        }

        private void HandleKeys()
        {
            if (MaverickInput.GetKeyDown(keys.landingGear))
            {
                gearDown = !gearDown;
                if (visualGear != null) visualGear.SetGearDown(gearDown);
                lastSystemEvent = gearDown ? "gear_down" : "gear_up";
            }

            if (MaverickInput.GetKeyDown(keys.flapsToggle))
            {
                CycleFlaps();
            }

            if (MaverickInput.GetKeyDown(keys.flapsUp))
            {
                StepFlaps(-1);
            }

            if (MaverickInput.GetKeyDown(keys.flapsDown))
            {
                StepFlaps(1);
            }

            airbrakeDeployed = MaverickInput.GetKey(keys.airbrake);
            wepActive = MaverickInput.GetKey(keys.wep);
        }

        public void CycleFlaps()
        {
            if (flaps == MaverickFlapState.Retracted) flaps = MaverickFlapState.Combat;
            else if (flaps == MaverickFlapState.Combat) flaps = MaverickFlapState.Takeoff;
            else if (flaps == MaverickFlapState.Takeoff) flaps = MaverickFlapState.Landing;
            else flaps = MaverickFlapState.Retracted;
            lastSystemEvent = "flaps_" + flaps;
        }

        public void StepFlaps(int step)
        {
            int next = Mathf.Clamp((int)flaps + step, 0, 3);
            flaps = (MaverickFlapState)next;
            lastSystemEvent = "flaps_" + flaps;
        }

        private void ApplyAutoProtection()
        {
            if (!autoRetractFlapsAtHighSpeed) return;
            float speed = aircraft.Speed;

            if (flaps == MaverickFlapState.Landing && speed > landingFlapMaxSpeed)
            {
                flaps = MaverickFlapState.Takeoff;
                lastSystemEvent = "auto_flaps_takeoff_speed";
            }
            if (flaps == MaverickFlapState.Takeoff && speed > takeoffFlapMaxSpeed)
            {
                flaps = MaverickFlapState.Combat;
                lastSystemEvent = "auto_flaps_combat_speed";
            }
            if (flaps == MaverickFlapState.Combat && speed > combatFlapMaxSpeed)
            {
                flaps = MaverickFlapState.Retracted;
                lastSystemEvent = "auto_flaps_retracted_speed";
            }
        }

        private void ApplyModifiers()
        {
            float liftBonus = 0f;
            float dragBonus = 0f;
            float pitchBonus = 0f;
            float stallBonus = 0f;

            if (flaps == MaverickFlapState.Combat)
            {
                liftBonus += combatFlapLiftBonus;
                dragBonus += combatFlapDragBonus;
                pitchBonus += flapPitchAuthorityBonus * 0.5f;
                stallBonus += flapStallAngleBonus * 0.5f;
            }
            else if (flaps == MaverickFlapState.Takeoff)
            {
                liftBonus += takeoffFlapLiftBonus;
                dragBonus += takeoffFlapDragBonus;
                pitchBonus += flapPitchAuthorityBonus;
                stallBonus += flapStallAngleBonus;
            }
            else if (flaps == MaverickFlapState.Landing)
            {
                liftBonus += landingFlapLiftBonus;
                dragBonus += landingFlapDragBonus;
                pitchBonus += flapPitchAuthorityBonus * 1.25f;
                stallBonus += flapStallAngleBonus * 1.35f;
            }

            if (airbrakeDeployed) dragBonus += airbrakeDragBonus;
            if (gearDown) dragBonus += gearDragBonus;

            effectiveLift = baseLift * (1f + liftBonus);
            effectiveDrag = baseDrag + dragBonus;

            aircraft.liftCoefficient = effectiveLift;
            aircraft.dragCoefficient = effectiveDrag;
            aircraft.pitchTorque = basePitchTorque * (1f + pitchBonus);
            aircraft.stallAngleDegrees = baseStallAngle + stallBonus;
        }
    }
}
