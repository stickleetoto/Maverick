using System.Collections.Generic;
using UnityEngine;

namespace MaverickFresh
{
    public class MavAircraftCatalog : MonoBehaviour
    {
        public bool useBuiltInProfiles = true;
        public List<MavAircraftRuntimeProfile> customProfiles = new List<MavAircraftRuntimeProfile>();

        public List<MavAircraftRuntimeProfile> GetProfiles()
        {
            List<MavAircraftRuntimeProfile> output = new List<MavAircraftRuntimeProfile>();
            if (useBuiltInProfiles)
                output.AddRange(CreateBuiltInProfiles());

            for (int i = 0; i < customProfiles.Count; i++)
            {
                if (customProfiles[i] != null)
                    output.Add(customProfiles[i]);
            }

            return output;
        }

        public static MavAircraftRuntimeProfile GetBuiltIn(MavAircraftKind kind)
        {
            List<MavAircraftRuntimeProfile> list = CreateBuiltInProfiles();
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].aircraft == kind)
                    return list[i];
            }
            return GetBuiltInFallback(list);
        }

        private static MavAircraftRuntimeProfile GetBuiltInFallback(List<MavAircraftRuntimeProfile> list)
        {
            if (list == null || list.Count == 0)
                return null;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].aircraft == MavAircraftKind.F22A)
                    return list[i];
            }
            return list[0];
        }

        public static List<MavAircraftRuntimeProfile> CreateBuiltInProfiles()
        {
            List<MavAircraftRuntimeProfile> list = new List<MavAircraftRuntimeProfile>();
            list.Add(CreateF15E());
            list.Add(CreateF16C());
            list.Add(CreateFA18E());
            list.Add(CreateF22A());
            list.Add(CreateF35A());
            return list;
        }

        private static MavAircraftRuntimeProfile Base(MavAircraftKind kind, string id, string display, string shortName, string role, string desc)
        {
            MavAircraftRuntimeProfile p = new MavAircraftRuntimeProfile();
            p.aircraft = kind;
            p.aircraftId = id;
            p.displayName = display;
            p.shortName = shortName;
            p.role = role;
            p.description = desc;
            return p;
        }

        private static MavAircraftRuntimeProfile CreateF15E()
        {
            MavAircraftRuntimeProfile p = Base(
                MavAircraftKind.F15E,
                "f15e",
                "F-15EX EAGLE II",
                "F-15EX",
                "Heavy multirole / power fighter",
                "Strong, fast, stable, and forgiving. A heavy secondary multirole option beside the F-22 main aircraft."
            );
            p.statSpeed = 9f; p.statTurn = 6.2f; p.statStability = 8.0f; p.statPayload = 9.0f; p.statDifficulty = 3.5f;
            p.mass = 14500f;
            p.thrust = 152f;
            p.rateControlP = new Vector3(0.42f, 0.28f, 0.38f);
            p.rateControlD = new Vector3(0.10f, 0.12f, 0.09f);
            p.maxRateControlTorque = new Vector3(32f, 14f, 40f);
            p.throttleSpoolUp = 1.8f;
            p.throttleSpoolDown = 2.4f;
            p.engineModelBlend = 0.66f; p.highAltitudeThrustFloor = 0.48f; p.thrustScaleHeight = 11200f; p.ramRecovery = 0.18f; p.maxEngineThrustScale = 1.17f; p.waveDragStrength = 0.36f; p.maxWaveDragAccel = 22f;
            p.combatFlapMaxSpeed = 255f; p.landingFlapMaxSpeed = 165f; p.combatFlapLiftMultiplier = 1.07f; p.combatFlapCd0Add = 0.011f; p.landingFlapLiftMultiplier = 1.16f; p.landingFlapCd0Add = 0.030f;
            p.targetCruiseSpeed = 315f;
            p.minCombatSpeed = 135f;
            p.maxCombatSpeed = 530f;
            p.startSpeed = 270f;
            p.angularDamping = 1.65f;
            p.maxAngularVelocity = 6.0f;
            p.pitchGain = 0.82f;
            p.rollGain = 1.25f;
            p.yawGain = 0.24f;
            p.targetPitchRateDeg = 48f;
            p.targetYawRateDeg = 18f;
            p.targetRollRateDeg = 72f;
            p.turnTorque = new Vector3(20f, 9f, 26f);
            p.maxAppliedTorqueAccelerationMode = new Vector3(42f, 18f, 52f);
            p.forceModeTorque = new Vector3(6200f, 3200f, 8200f);
            p.aoaSoftLimitDeg = 24f;
            p.aoaHardLimitDeg = 34f;
            p.bestTurnSpeed = 236f;
            p.gunAmmo = 950; p.missileAmmo = 4; p.precisionAmmo = 4; p.bombAmmo = 6; p.rocketAmmo = 28;
            p.useAeroBody = true;
            p.aeroBlend = 0.45f; p.liftBlend = 0.58f; p.dragBlend = 0.72f; p.gravityBlend = 0.45f;
            p.wingArea = 56.5f; p.clSlopePerDeg = 0.070f; p.stallAoADeg = 18f; p.fullStallAoADeg = 32f; p.postStallLiftFactor = 0.34f;
            p.cd0 = 0.028f; p.aspectRatio = 3.0f; p.oswaldEfficiency = 0.75f; p.postStallDrag = 0.18f;
            p.maxLiftG = 9.1f; p.maxDragG = 2.8f; p.referenceControlSpeed = 260f; p.minControlAuthority = 0.36f; p.maxControlAuthority = 1.05f; p.velocityAssistFade = 0.45f;
            p.useThrustVectorControl = false;
            p.useSensorSuite = true; p.radarCrossSectionSqm = 12.0f; p.irSignature = 1.10f; p.stealthRating = 0f; p.radarRangeMeters = 9200f; p.radarFovDeg = 58f; p.radarSensitivity = 0.92f; p.irstRangeMeters = 4300f; p.sensorRefreshSeconds = 0.22f;
            p.hangarScale = 1.05f;
            return p;
        }

        private static MavAircraftRuntimeProfile CreateF16C()
        {
            MavAircraftRuntimeProfile p = Base(
                MavAircraftKind.F16C,
                "f16c",
                "F-16C FIGHTING FALCON",
                "F-16C",
                "Light fighter / agile dogfighter",
                "Small, quick, and responsive. Best for close maneuvering and fast mouse aim practice."
            );
            p.statSpeed = 8.0f; p.statTurn = 8.7f; p.statStability = 6.3f; p.statPayload = 5.5f; p.statDifficulty = 5.5f;
            p.mass = 9800f;
            p.thrust = 123f;
            p.rateControlP = new Vector3(0.62f, 0.22f, 0.58f);
            p.rateControlD = new Vector3(0.08f, 0.10f, 0.07f);
            p.maxRateControlTorque = new Vector3(48f, 12f, 62f);
            p.throttleSpoolUp = 2.4f;
            p.throttleSpoolDown = 2.8f;
            p.engineModelBlend = 0.68f; p.highAltitudeThrustFloor = 0.44f; p.thrustScaleHeight = 10300f; p.ramRecovery = 0.16f; p.maxEngineThrustScale = 1.15f; p.waveDragStrength = 0.34f; p.maxWaveDragAccel = 24f;
            p.combatFlapMaxSpeed = 245f; p.landingFlapMaxSpeed = 155f; p.combatFlapLiftMultiplier = 1.09f; p.combatFlapCd0Add = 0.012f; p.landingFlapLiftMultiplier = 1.17f; p.landingFlapCd0Add = 0.032f;
            p.targetCruiseSpeed = 292f;
            p.minCombatSpeed = 120f;
            p.maxCombatSpeed = 500f;
            p.startSpeed = 245f;
            p.linearDamping = 0.019f;
            p.angularDamping = 1.05f;
            p.maxAngularVelocity = 7.4f;
            p.mouseSensitivity = 4.1f;
            p.pitchGain = 0.92f;
            p.rollGain = 1.52f;
            p.yawGain = 0.20f;
            p.targetPitchRateDeg = 56f;
            p.targetYawRateDeg = 16f;
            p.targetRollRateDeg = 105f;
            p.turnTorque = new Vector3(23f, 7f, 35f);
            p.maxAppliedTorqueAccelerationMode = new Vector3(46f, 15f, 66f);
            p.forceModeTorque = new Vector3(6000f, 2600f, 9300f);
            p.bestTurnSpeed = 215f;
            p.lowSpeedPitchAuthority = 0.50f;
            p.bestSpeedPitchAuthority = 1.22f;
            p.highSpeedPitchAuthority = 0.65f;
            p.aoaSoftLimitDeg = 22f;
            p.aoaHardLimitDeg = 30f;
            p.gunAmmo = 510; p.missileAmmo = 4; p.precisionAmmo = 2; p.bombAmmo = 4; p.rocketAmmo = 18;
            p.useAeroBody = true;
            p.aeroBlend = 0.47f; p.liftBlend = 0.60f; p.dragBlend = 0.74f; p.gravityBlend = 0.45f;
            p.wingArea = 27.9f; p.clSlopePerDeg = 0.078f; p.stallAoADeg = 17f; p.fullStallAoADeg = 29f; p.postStallLiftFactor = 0.28f;
            p.cd0 = 0.026f; p.aspectRatio = 3.2f; p.oswaldEfficiency = 0.78f; p.postStallDrag = 0.22f;
            p.maxLiftG = 9.4f; p.maxDragG = 3.0f; p.referenceControlSpeed = 235f; p.minControlAuthority = 0.30f; p.maxControlAuthority = 1.10f; p.velocityAssistFade = 0.50f;
            p.useThrustVectorControl = false;
            p.useSensorSuite = true; p.radarCrossSectionSqm = 5.0f; p.irSignature = 1.00f; p.stealthRating = 0f; p.radarRangeMeters = 8200f; p.radarFovDeg = 56f; p.radarSensitivity = 0.88f; p.irstRangeMeters = 4100f; p.sensorRefreshSeconds = 0.22f;
            p.hangarScale = 0.92f;
            return p;
        }

        private static MavAircraftRuntimeProfile CreateFA18E()
        {
            MavAircraftRuntimeProfile p = Base(
                MavAircraftKind.FA18E,
                "fa18e",
                "F/A-18E SUPER HORNET",
                "F/A-18E",
                "Carrier multirole / low-speed control",
                "Stable and controllable at low speed. A good aircraft for carrier and ground attack modes."
            );
            p.statSpeed = 7.0f; p.statTurn = 7.4f; p.statStability = 8.6f; p.statPayload = 7.5f; p.statDifficulty = 4.0f;
            p.mass = 13200f;
            p.thrust = 129f;
            p.rateControlP = new Vector3(0.48f, 0.34f, 0.44f);
            p.rateControlD = new Vector3(0.12f, 0.14f, 0.11f);
            p.maxRateControlTorque = new Vector3(38f, 18f, 46f);
            p.throttleSpoolUp = 1.6f;
            p.throttleSpoolDown = 2.0f;
            p.engineModelBlend = 0.64f; p.highAltitudeThrustFloor = 0.42f; p.thrustScaleHeight = 9800f; p.ramRecovery = 0.14f; p.maxEngineThrustScale = 1.12f; p.waveDragStrength = 0.42f; p.maxWaveDragAccel = 25f;
            p.combatFlapMaxSpeed = 235f; p.landingFlapMaxSpeed = 145f; p.combatFlapLiftMultiplier = 1.13f; p.combatFlapCd0Add = 0.014f; p.landingFlapLiftMultiplier = 1.22f; p.landingFlapCd0Add = 0.036f;
            p.targetCruiseSpeed = 270f;
            p.minCombatSpeed = 105f;
            p.maxCombatSpeed = 455f;
            p.startSpeed = 230f;
            p.angularDamping = 1.85f;
            p.maxAngularVelocity = 6.4f;
            p.mouseSensitivity = 3.6f;
            p.pitchGain = 0.88f;
            p.rollGain = 1.18f;
            p.yawGain = 0.32f;
            p.targetPitchRateDeg = 52f;
            p.targetYawRateDeg = 23f;
            p.targetRollRateDeg = 78f;
            p.turnTorque = new Vector3(22f, 11f, 25f);
            p.maxAppliedTorqueAccelerationMode = new Vector3(44f, 23f, 50f);
            p.forceModeTorque = new Vector3(6100f, 3600f, 7900f);
            p.bestTurnSpeed = 195f;
            p.lowSpeedPitchAuthority = 0.82f;
            p.bestSpeedPitchAuthority = 1.12f;
            p.highSpeedPitchAuthority = 0.66f;
            p.aoaSoftLimitDeg = 31f;
            p.aoaHardLimitDeg = 42f;
            p.gunAmmo = 578; p.missileAmmo = 4; p.precisionAmmo = 4; p.bombAmmo = 6; p.rocketAmmo = 24;
            p.useAeroBody = true;
            p.aeroBlend = 0.48f; p.liftBlend = 0.64f; p.dragBlend = 0.78f; p.gravityBlend = 0.45f;
            p.wingArea = 46.5f; p.clSlopePerDeg = 0.074f; p.stallAoADeg = 24f; p.fullStallAoADeg = 42f; p.postStallLiftFactor = 0.45f;
            p.cd0 = 0.031f; p.aspectRatio = 4.0f; p.oswaldEfficiency = 0.76f; p.postStallDrag = 0.19f;
            p.maxLiftG = 8.6f; p.maxDragG = 3.1f; p.referenceControlSpeed = 220f; p.minControlAuthority = 0.42f; p.maxControlAuthority = 1.05f; p.velocityAssistFade = 0.48f;
            p.useThrustVectorControl = false;
            p.useSensorSuite = true; p.radarCrossSectionSqm = 7.0f; p.irSignature = 1.05f; p.stealthRating = 0f; p.radarRangeMeters = 8500f; p.radarFovDeg = 60f; p.radarSensitivity = 0.90f; p.irstRangeMeters = 4400f; p.sensorRefreshSeconds = 0.22f;
            p.hangarScale = 1.0f;
            return p;
        }

        private static MavAircraftRuntimeProfile CreateF22A()
        {
            MavAircraftRuntimeProfile p = Base(
                MavAircraftKind.F22A,
                "f22a",
                "F-22A RAPTOR",
                "F-22A",
                "Primary stealth air superiority / TVC",
                "The main Maverick aircraft: high thrust, high-AoA authority, and TVC-assisted pitch control."
            );
            p.statSpeed = 9.2f; p.statTurn = 9.4f; p.statStability = 7.6f; p.statPayload = 4.5f; p.statDifficulty = 5.8f;
            p.mass = 16500f;
            p.thrust = 300f;
            p.rateControlP = new Vector3(0.58f, 0.28f, 0.48f);
            p.rateControlD = new Vector3(0.13f, 0.13f, 0.11f);
            p.maxRateControlTorque = new Vector3(50f, 18f, 60f);
            p.throttleSpoolUp = 2.2f;
            p.throttleSpoolDown = 2.6f;
            p.engineModelBlend = 0.74f; p.highAltitudeThrustFloor = 0.58f; p.thrustScaleHeight = 12800f; p.ramRecovery = 0.26f; p.maxEngineThrustScale = 1.26f; p.waveDragStrength = 0.22f; p.maxWaveDragAccel = 18f;
            p.combatFlapMaxSpeed = 255f; p.landingFlapMaxSpeed = 155f; p.combatFlapLiftMultiplier = 1.03f; p.combatFlapCd0Add = 0.010f; p.landingFlapLiftMultiplier = 1.10f; p.landingFlapCd0Add = 0.030f;
            p.targetCruiseSpeed = 315f;
            p.minCombatSpeed = 120f;
            p.maxCombatSpeed = 540f;
            p.startSpeed = 285f;
            p.afterburnerMultiplier = 1.40f;
            p.linearDamping = 0.020f;
            p.angularDamping = 1.22f;
            p.maxAngularVelocity = 7.8f;
            p.mouseSensitivity = 3.8f;
            p.pitchGain = 0.96f;
            p.rollGain = 1.30f;
            p.yawGain = 0.22f;
            p.targetPitchRateDeg = 66f;
            p.targetYawRateDeg = 20f;
            p.targetRollRateDeg = 92f;
            p.turnTorque = new Vector3(30f, 9f, 31f);
            p.maxAppliedTorqueAccelerationMode = new Vector3(58f, 18f, 62f);
            p.forceModeTorque = new Vector3(7200f, 3000f, 9000f);
            p.bestTurnSpeed = 220f;
            p.lowSpeedPitchAuthority = 0.95f;
            p.bestSpeedPitchAuthority = 1.27f;
            p.highSpeedPitchAuthority = 0.76f;
            p.aoaSoftLimitDeg = 50f;
            p.aoaHardLimitDeg = 65f;
            p.gunAmmo = 480; p.missileAmmo = 6; p.precisionAmmo = 2; p.bombAmmo = 2; p.rocketAmmo = 0;
            p.useAeroBody = true;
            p.aeroBlend = 0.54f; p.liftBlend = 0.68f; p.dragBlend = 0.68f; p.gravityBlend = 0.43f;
            p.wingArea = 78.0f; p.clSlopePerDeg = 0.068f; p.stallAoADeg = 32f; p.fullStallAoADeg = 62f; p.postStallLiftFactor = 0.58f;
            p.cd0 = 0.024f; p.aspectRatio = 2.36f; p.oswaldEfficiency = 0.78f; p.postStallDrag = 0.16f;
            p.maxLiftG = 9.8f; p.maxDragG = 2.4f; p.referenceControlSpeed = 230f; p.minControlAuthority = 0.50f; p.maxControlAuthority = 1.12f; p.velocityAssistFade = 0.58f;
            p.useThrustVectorControl = true; p.tvcPitchAuthority = 5.5f; p.tvcRollAuthority = 1.2f; p.tvcYawAuthority = 0.0f; p.tvcActivationAoADeg = 18f; p.tvcFullAoADeg = 46f; p.tvcLowSpeedFull = 145f; p.tvcLowSpeedFadeOut = 360f; p.tvcMaxTorque = 7.5f;
            p.useSensorSuite = true; p.radarCrossSectionSqm = 0.03f; p.irSignature = 0.72f; p.stealthRating = 9.5f; p.radarRangeMeters = 12500f; p.radarFovDeg = 72f; p.radarSensitivity = 1.35f; p.irstRangeMeters = 6200f; p.sensorRefreshSeconds = 0.16f;
            p.hangarScale = 1.0f;
            return p;
        }

        private static MavAircraftRuntimeProfile CreateF35A()
        {
            MavAircraftRuntimeProfile p = Base(
                MavAircraftKind.F35A,
                "f35a",
                "F-35A LIGHTNING II",
                "F-35A",
                "Stealth multirole / sensor fighter",
                "Stable, modern, and sensor-focused. Not the best dogfighter, but great for precision missions."
            );
            p.statSpeed = 7.2f; p.statTurn = 6.0f; p.statStability = 8.8f; p.statPayload = 6.0f; p.statDifficulty = 5.2f;
            p.mass = 15000f;
            p.thrust = 132f;
            p.rateControlP = new Vector3(0.36f, 0.26f, 0.34f);
            p.rateControlD = new Vector3(0.14f, 0.14f, 0.13f);
            p.maxRateControlTorque = new Vector3(28f, 14f, 36f);
            p.throttleSpoolUp = 1.4f;
            p.throttleSpoolDown = 1.8f;
            p.engineModelBlend = 0.66f; p.highAltitudeThrustFloor = 0.43f; p.thrustScaleHeight = 10100f; p.ramRecovery = 0.15f; p.maxEngineThrustScale = 1.12f; p.waveDragStrength = 0.40f; p.maxWaveDragAccel = 25f;
            p.combatFlapMaxSpeed = 240f; p.landingFlapMaxSpeed = 150f; p.combatFlapLiftMultiplier = 1.07f; p.combatFlapCd0Add = 0.012f; p.landingFlapLiftMultiplier = 1.16f; p.landingFlapCd0Add = 0.033f;
            p.targetCruiseSpeed = 275f;
            p.minCombatSpeed = 118f;
            p.maxCombatSpeed = 455f;
            p.startSpeed = 235f;
            p.linearDamping = 0.024f;
            p.angularDamping = 2.05f;
            p.maxAngularVelocity = 5.4f;
            p.mouseSensitivity = 3.2f;
            p.pitchGain = 0.70f;
            p.rollGain = 0.96f;
            p.yawGain = 0.24f;
            p.targetPitchRateDeg = 42f;
            p.targetYawRateDeg = 18f;
            p.targetRollRateDeg = 64f;
            p.turnTorque = new Vector3(17f, 8f, 22f);
            p.maxAppliedTorqueAccelerationMode = new Vector3(35f, 16f, 42f);
            p.forceModeTorque = new Vector3(5800f, 3000f, 7200f);
            p.bestTurnSpeed = 225f;
            p.lowSpeedPitchAuthority = 0.60f;
            p.bestSpeedPitchAuthority = 0.98f;
            p.highSpeedPitchAuthority = 0.60f;
            p.aoaSoftLimitDeg = 25f;
            p.aoaHardLimitDeg = 36f;
            p.gunAmmo = 180; p.missileAmmo = 4; p.precisionAmmo = 4; p.bombAmmo = 4; p.rocketAmmo = 0;
            p.useAeroBody = true;
            p.aeroBlend = 0.44f; p.liftBlend = 0.54f; p.dragBlend = 0.82f; p.gravityBlend = 0.45f;
            p.wingArea = 42.7f; p.clSlopePerDeg = 0.064f; p.stallAoADeg = 20f; p.fullStallAoADeg = 34f; p.postStallLiftFactor = 0.32f;
            p.cd0 = 0.030f; p.aspectRatio = 2.7f; p.oswaldEfficiency = 0.72f; p.postStallDrag = 0.24f;
            p.maxLiftG = 7.5f; p.maxDragG = 3.2f; p.referenceControlSpeed = 250f; p.minControlAuthority = 0.38f; p.maxControlAuthority = 1.00f; p.velocityAssistFade = 0.42f;
            p.useThrustVectorControl = false;
            p.useSensorSuite = true; p.radarCrossSectionSqm = 0.06f; p.irSignature = 0.80f; p.stealthRating = 8.5f; p.radarRangeMeters = 11200f; p.radarFovDeg = 68f; p.radarSensitivity = 1.18f; p.irstRangeMeters = 5600f; p.sensorRefreshSeconds = 0.18f;
            p.hangarScale = 0.98f;
            return p;
        }
    }
}
