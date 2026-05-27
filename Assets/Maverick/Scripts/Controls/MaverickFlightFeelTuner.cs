using UnityEngine;
using EaglePhysicalAI.Aircraft;

namespace EaglePhysicalAI.Controls
{
    public class MaverickFlightFeelTuner : MonoBehaviour
    {
        public MaverickFlightFeelPreset preset = MaverickFlightFeelPreset.HeavyFighter;
        public bool applyOnStart = true;

        public AircraftPhysicsController aircraft;
        public MaverickInstructorV10 instructor;

        [Header("Keys")]
        public KeyCode heavyFighterKey = KeyCode.Home;
        public KeyCode arcadeStableKey = KeyCode.PageUp;
        public KeyCode realisticDebugKey = KeyCode.End;

        private void Awake()
        {
            if (aircraft == null) aircraft = GetComponent<AircraftPhysicsController>();
            if (instructor == null) instructor = GetComponent<MaverickInstructorV10>();
        }

        private void Start()
        {
            if (applyOnStart) ApplyPreset(preset);
        }

        private void Update()
        {
            if (MaverickInput.GetKeyDown(heavyFighterKey)) ApplyPreset(MaverickFlightFeelPreset.HeavyFighter);
            if (MaverickInput.GetKeyDown(arcadeStableKey)) ApplyPreset(MaverickFlightFeelPreset.ArcadeStable);
            if (MaverickInput.GetKeyDown(realisticDebugKey)) ApplyPreset(MaverickFlightFeelPreset.RealisticDebug);
        }

        public void ApplyPreset(MaverickFlightFeelPreset p)
        {
            preset = p;
            if (aircraft == null) aircraft = GetComponent<AircraftPhysicsController>();
            if (instructor == null) instructor = GetComponent<MaverickInstructorV10>();

            if (aircraft != null)
            {
                if (p == MaverickFlightFeelPreset.HeavyFighter)
                {
                    aircraft.maxThrust = 125000f;
                    aircraft.liftCoefficient = 1.15f;
                    aircraft.dragCoefficient = 0.028f;
                    aircraft.inducedDragCoefficient = 0.018f;
                    aircraft.pitchTorque = 26000f;
                    aircraft.rollTorque = 42000f;
                    aircraft.yawTorque = 12000f;
                    aircraft.angularDamping = 0.78f;
                    aircraft.stallAngleDegrees = 26f;
                    aircraft.minLiftSpeed = 42f;
                    aircraft.rollTorqueSign = 1f;
                    aircraft.maxAngularVelocity = 3.0f;
                }
                else if (p == MaverickFlightFeelPreset.ArcadeStable)
                {
                    aircraft.maxThrust = 145000f;
                    aircraft.liftCoefficient = 1.35f;
                    aircraft.dragCoefficient = 0.023f;
                    aircraft.inducedDragCoefficient = 0.014f;
                    aircraft.pitchTorque = 34000f;
                    aircraft.rollTorque = 52000f;
                    aircraft.yawTorque = 16000f;
                    aircraft.angularDamping = 0.95f;
                    aircraft.stallAngleDegrees = 31f;
                    aircraft.minLiftSpeed = 35f;
                    aircraft.rollTorqueSign = 1f;
                    aircraft.maxAngularVelocity = 3.8f;
                }
                else
                {
                    aircraft.maxThrust = 112000f;
                    aircraft.liftCoefficient = 0.95f;
                    aircraft.dragCoefficient = 0.034f;
                    aircraft.inducedDragCoefficient = 0.022f;
                    aircraft.pitchTorque = 23000f;
                    aircraft.rollTorque = 32000f;
                    aircraft.yawTorque = 9500f;
                    aircraft.angularDamping = 0.58f;
                    aircraft.stallAngleDegrees = 24f;
                    aircraft.minLiftSpeed = 52f;
                    aircraft.rollTorqueSign = 1f;
                    aircraft.maxAngularVelocity = 2.6f;
                }

                if (aircraft.rb != null)
                    aircraft.rb.maxAngularVelocity = aircraft.maxAngularVelocity;
            }

            if (instructor != null)
            {
                if (p == MaverickFlightFeelPreset.HeavyFighter)
                {
                    instructor.pitchGain = 1.65f;
                    instructor.yawGain = 0.75f;
                    instructor.rollGain = 2.05f;
                    instructor.commandSmoothing = 8.5f;
                    instructor.maxBankDeg = 72f;
                }
                else if (p == MaverickFlightFeelPreset.ArcadeStable)
                {
                    instructor.pitchGain = 2.0f;
                    instructor.yawGain = 1.0f;
                    instructor.rollGain = 2.4f;
                    instructor.commandSmoothing = 12f;
                    instructor.maxBankDeg = 78f;
                }
                else
                {
                    instructor.pitchGain = 1.25f;
                    instructor.yawGain = 0.55f;
                    instructor.rollGain = 1.45f;
                    instructor.commandSmoothing = 6f;
                    instructor.maxBankDeg = 62f;
                }
            }
        }
    }
}
