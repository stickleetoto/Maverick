using System;
using UnityEngine;
using EaglePhysicalAI.Safety;
using EaglePhysicalAI.Sensors.Radar;
using EaglePhysicalAI.Sensors.TargetingPod;

namespace EaglePhysicalAI.Aircraft
{
    public enum AircraftTrainingProfileKind
    {
        GenericFighter,
        F15EStyleCAS,
        F22StyleTestbed,
        HeavyTrainer
    }

    /// <summary>
    /// Gameplay tuning profile for aircraft physics, safety, and sensor ranges.
    /// This is not a real aircraft data sheet; it is a reusable training preset.
    /// </summary>
    [Serializable]
    public class AircraftTrainingProfile
    {
        [Header("Identity")]
        public string profileName = "F-15E Style CAS Testbed";
        public AircraftTrainingProfileKind kind = AircraftTrainingProfileKind.F15EStyleCAS;

        [Header("Physics Tuning")]
        public float mass = 14500f;
        public float maxThrust = 85000f;
        public float liftCoefficient = 0.85f;
        public float dragCoefficient = 0.035f;
        public float inducedDragCoefficient = 0.015f;
        public float stallAngleDegrees = 28f;
        public float minLiftSpeed = 35f;
        public float pitchTorque = 22000f;
        public float rollTorque = 28000f;
        public float yawTorque = 9000f;
        public float angularDamping = 0.55f;
        public float spawnThrottle = 0.72f;

        [Header("Safety Envelope")]
        public float minimumSafeAltitude = 120f;
        public float lowAltitudeRecoverAltitude = 450f;
        public float stallWarningSpeed = 58f;
        public float overspeedWarning = 420f;
        public float maximumSafeBankAngle = 115f;

        [Header("Sensor Tuning")]
        public float radarAirRange = 8500f;
        public float radarGroundRange = 6200f;
        public float radarAzimuthHalfAngle = 60f;
        public float radarElevationHalfAngle = 35f;
        public float podMaxRecognitionRange = 6500f;
        public float podMinFov = 8f;
        public float podMaxFov = 55f;

        public static AircraftTrainingProfile CreateF15EStyle()
        {
            return new AircraftTrainingProfile
            {
                profileName = "F-15E Style CAS Testbed",
                kind = AircraftTrainingProfileKind.F15EStyleCAS,
                mass = 14500f,
                maxThrust = 90000f,
                liftCoefficient = 0.92f,
                dragCoefficient = 0.036f,
                inducedDragCoefficient = 0.016f,
                stallAngleDegrees = 29f,
                minLiftSpeed = 38f,
                pitchTorque = 23000f,
                rollTorque = 27500f,
                yawTorque = 9500f,
                angularDamping = 0.6f,
                spawnThrottle = 0.74f,
                minimumSafeAltitude = 130f,
                lowAltitudeRecoverAltitude = 500f,
                stallWarningSpeed = 62f,
                overspeedWarning = 430f,
                maximumSafeBankAngle = 115f,
                radarAirRange = 9000f,
                radarGroundRange = 7000f,
                radarAzimuthHalfAngle = 65f,
                radarElevationHalfAngle = 38f,
                podMaxRecognitionRange = 7000f,
                podMinFov = 7f,
                podMaxFov = 58f
            };
        }

        public static AircraftTrainingProfile CreateF22Style()
        {
            return new AircraftTrainingProfile
            {
                profileName = "F-22 Style Testbed",
                kind = AircraftTrainingProfileKind.F22StyleTestbed,
                mass = 12500f,
                maxThrust = 98000f,
                liftCoefficient = 0.88f,
                dragCoefficient = 0.029f,
                inducedDragCoefficient = 0.013f,
                stallAngleDegrees = 31f,
                minLiftSpeed = 34f,
                pitchTorque = 26000f,
                rollTorque = 33000f,
                yawTorque = 10500f,
                angularDamping = 0.64f,
                spawnThrottle = 0.7f,
                minimumSafeAltitude = 130f,
                lowAltitudeRecoverAltitude = 500f,
                stallWarningSpeed = 58f,
                overspeedWarning = 455f,
                maximumSafeBankAngle = 125f,
                radarAirRange = 9600f,
                radarGroundRange = 6100f,
                radarAzimuthHalfAngle = 68f,
                radarElevationHalfAngle = 40f,
                podMaxRecognitionRange = 6200f,
                podMinFov = 8f,
                podMaxFov = 55f
            };
        }

        public void Apply(AircraftPhysicsController aircraft, FlightEnvelopeSafetyGuard safety, F15ERadarSystem radar, TargetingPodSystem pod)
        {
            if (aircraft != null)
            {
                if (aircraft.rb != null) aircraft.rb.mass = mass;
                aircraft.maxThrust = maxThrust;
                aircraft.liftCoefficient = liftCoefficient;
                aircraft.dragCoefficient = dragCoefficient;
                aircraft.inducedDragCoefficient = inducedDragCoefficient;
                aircraft.stallAngleDegrees = stallAngleDegrees;
                aircraft.minLiftSpeed = minLiftSpeed;
                aircraft.pitchTorque = pitchTorque;
                aircraft.rollTorque = rollTorque;
                aircraft.yawTorque = yawTorque;
                aircraft.angularDamping = angularDamping;
                aircraft.stallWarningSpeed = stallWarningSpeed;
                aircraft.overspeedWarning = overspeedWarning;
                aircraft.targetThrottle = Mathf.Clamp01(spawnThrottle);
                aircraft.throttle = Mathf.Clamp01(spawnThrottle);
            }

            if (safety != null)
            {
                safety.hardDeckAltitude = minimumSafeAltitude;
                safety.cautionAltitude = lowAltitudeRecoverAltitude;
                safety.maxBankAngleForRecovery = maximumSafeBankAngle;
                safety.maxSafeSpeed = overspeedWarning;
            }

            if (radar != null)
            {
                radar.maxAirSearchRange = radarAirRange;
                radar.maxGroundSearchRange = radarGroundRange;
                radar.azimuthHalfAngle = radarAzimuthHalfAngle;
                radar.elevationHalfAngle = radarElevationHalfAngle;
            }

            if (pod != null)
            {
                pod.maxRecognitionRange = podMaxRecognitionRange;
                pod.minFov = podMinFov;
                pod.maxFov = podMaxFov;
            }
        }
    }
}
