using System;
using UnityEngine;

namespace MaverickFresh
{
    [Serializable]
    public class MavAircraftRuntimeProfile
    {
        [Header("Identity")]
        public MavAircraftKind aircraft;
        public string aircraftId;
        public string displayName;
        public string shortName;
        public string role;
        [TextArea(2, 5)] public string description;
        public GameObject modelPrefab;

        [Header("Hangar")]
        public float hangarScale = 1f;
        public Vector3 hangarRotationEuler = new Vector3(0f, 145f, 0f);
        public Vector3 hangarCameraOffset = new Vector3(0f, 3.2f, -11f);


        [Header("Flight Visual Pose")]
        public Vector3 flightVisualLocalPosition = Vector3.zero;
        public Vector3 flightVisualLocalEuler = Vector3.zero;
        public float flightVisualScale = 1f;

        [Header("Stats 0~10")]
        [Range(0f, 10f)] public float statSpeed = 7f;
        [Range(0f, 10f)] public float statTurn = 7f;
        [Range(0f, 10f)] public float statStability = 7f;
        [Range(0f, 10f)] public float statPayload = 5f;
        [Range(0f, 10f)] public float statDifficulty = 5f;

        [Header("Rigidbody")]
        public float mass = 14500f;
        public float linearDamping = 0.006f;
        public float angularDamping = 1.6f;
        public float maxAngularVelocity = 6f;

        [Header("Speed / Engine")]
        public float startAltitude = 1200f;
        public float startSpeed = 260f;
        public float thrust = 220f;
        public float throttlePercent = 95f;
        public float throttleChangeRate = 45f;
        public float throttleSpoolUp = 1.8f;
        public float throttleSpoolDown = 2.4f;
        public float afterburnerMultiplier = 1.35f;
public bool useAtmosphericEngine = true;
[Range(0f, 1f)] public float engineModelBlend = 0.65f;
public float highAltitudeThrustFloor = 0.46f;
public float thrustScaleHeight = 10500f;
public float ramRecovery = 0.18f;
public float maxEngineThrustScale = 1.18f;
public float waveDragStrength = 0.36f;
public float maxWaveDragAccel = 22f;
        public float targetCruiseSpeed = 315f;
        public float minCombatSpeed = 135f;
        public float maxCombatSpeed = 530f;
        public float lowSpeedThrustBoost = 0.55f;
        public float overspeedDrag = 0.055f;
        public float speedAssistStrength = 0.22f;

        [Header("Mouse / Instructor")]
        public float mouseSensitivity = 3.85f;
        public float aimDistance = 600f;
        public float pitchGain = 0.82f;
        public float yawGain = 0.24f;
        public float rollGain = 1.25f;
        public float maxAutoPitch = 0.68f;
        public float noseDownTrim = 0.115f;
        public float inputSmoothing = 5.8f;
        public float maxScreenRollBankAngle = 72f;
        public float targetPitchRateDeg = 48f;
        public float targetYawRateDeg = 18f;
        public float targetRollRateDeg = 72f;
        public Vector3 rateControlP = new Vector3(0.42f, 0.28f, 0.38f);
        public Vector3 rateControlD = new Vector3(0.10f, 0.12f, 0.09f);
        public Vector3 maxRateControlTorque = new Vector3(32f, 14f, 40f);

        [Header("Torque")]
        public Vector3 turnTorque = new Vector3(20f, 9f, 26f);
        public Vector3 forceModeTorque = new Vector3(6200f, 3200f, 8200f);
        public Vector3 maxAppliedTorqueAccelerationMode = new Vector3(42f, 18f, 52f);
        public Vector3 maxAppliedTorqueForceMode = new Vector3(9000f, 4200f, 10500f);
        public float forceMult = 1000f;

        [Header("Envelope")]
        public float bestTurnSpeed = 236f;
        public float turnBandWidth = 95f;
        public float bestTurnPitchBoost = 1.28f;
        public float bestTurnRollBoost = 1.12f;
        public float lowSpeedPitchAuthority = 0.58f;
        public float bestSpeedPitchAuthority = 1.15f;
        public float highSpeedPitchAuthority = 0.72f;
        public float lowSpeedRollAuthority = 0.70f;
        public float bestSpeedRollAuthority = 1.05f;
        public float highSpeedRollAuthority = 0.82f;
        public float softGLimit = 8.8f;
        public float hardGLimit = 11.2f;
        public float aoaSoftLimitDeg = 24f;
        public float aoaHardLimitDeg = 34f;
        public float aoaPitchReduction = 0.45f;

        // Negative envelope protection (Phase 5B.5 defect D5, reclassified 5B.6).
        //
        // PROVENANCE: GAMEPLAY_SAFETY / MAVERICK_TUNING. None of these is an F-16 value. The Morelli
        // alpha domain (-10 .. +45 deg) is a MODEL VALIDITY range enforced separately by
        // MavF16ReferenceEnvelope; it is not a flight-control limit and is not the justification for
        // the numbers here. See the block comment in MavInstructorController.
        //
        // They reach the CONSUMER (MavInstructorController) directly through ApplyToInstructor - there
        // is deliberately no jet mirror, because a second home for a setting is what caused defects D4
        // and the 5B.0b family.
        public bool useNegativeEnvelopeProtection = true;
        public float aoaNegativeSoftLimitDeg = -7f;
        public float aoaNegativeHardLimitDeg = -10f;
        public float negativeGLimit = -3f;

        [Header("v0.20.3~0.20.6 Experimental Aero")]
        public bool useAeroBody = true;
        [Tooltip("Phase 4B: aerodynamics owns turn curvature and weight support, and the legacy velocity/alignment assists are migrated down by the same amount. Per-aircraft, so an aircraft that has not been re-tuned and re-flown keeps Phase 4A behaviour exactly.")]
        public bool usePhase4BTurnDynamics = false;

        [Tooltip("Phase 4B: load factor at which the low-speed thrust boost is fully suppressed, so a sustained hard turn bleeds energy instead of being quietly paid for by thrust.")]
        public float thrustBoostSuppressionG = 3.0f;

        [Tooltip("Phase 4B: fraction of the rate controller's proportional rate-nulling that remains when no rate is commanded. Lower = more rotational inertia on release.")]
        [Range(0.05f, 1f)] public float releaseRateNullingScale = 0.35f;

        [Tooltip("Phase 4B: how much of the nose-onto-velocity alignment assist survives at full aero ownership. It erases angle of attack, so it must be migrated - but not to zero, or the nose wanders at low speed.")]
        [Range(0f, 1f)] public float alignmentAssistFloorAtFullAero = 0.15f;

        [Tooltip("Phase 4B: aerodynamic static stability replaces the legacy forward-alignment assist's job of holding the nose near the velocity vector. Required whenever the alignment assist is migrated down, or angle of attack has nothing restoring it.")]
        public bool useAeroStaticStability = false;
        public float pitchStabilityStrength = 0.055f;
        public float yawStabilityStrength = 0.030f;

        [Range(0f, 1f)] public float aeroBlend = 0.45f;
        [Range(0f, 1f)] public float liftBlend = 0.55f;
        [Range(0f, 1f)] public float dragBlend = 0.75f;
        [Range(0f, 1.25f)] public float gravityBlend = 0.45f;
        public float wingArea = 56.5f;
        public float clSlopePerDeg = 0.070f;
        public float stallAoADeg = 18f;
        public float fullStallAoADeg = 32f;
        [Range(0f, 1f)] public float postStallLiftFactor = 0.35f;
        public float cd0 = 0.028f;
        public float aspectRatio = 3.0f;
        public float oswaldEfficiency = 0.75f;
        public float postStallDrag = 0.18f;
        public float maxLiftG = 9.2f;
        public float maxDragG = 2.8f;
        public float referenceControlSpeed = 260f;
        [Range(0.05f, 1f)] public float minControlAuthority = 0.35f;
        [Range(0.5f, 1.5f)] public float maxControlAuthority = 1.05f;
        public float velocityAssistFade = 0.45f;

        [Header("v0.20.8 F-22 TVC Assist")]
        public bool useThrustVectorControl = false;
        public float tvcPitchAuthority = 0f;
        public float tvcRollAuthority = 0f;
        public float tvcYawAuthority = 0f;
        public float tvcActivationAoADeg = 18f;
        public float tvcFullAoADeg = 45f;
        public float tvcLowSpeedFull = 150f;
        public float tvcLowSpeedFadeOut = 360f;
        public float tvcMaxTorque = 8f;


[Header("v0.20.9 Sensor / Signature")]
public bool useSensorSuite = true;
public float radarCrossSectionSqm = 5.0f;
public float irSignature = 1.0f;
[Range(0f, 10f)] public float stealthRating = 0f;
public float radarRangeMeters = 9000f;
public float radarFovDeg = 60f;
public float radarSensitivity = 1.0f;
public float irstRangeMeters = 4500f;
public float sensorRefreshSeconds = 0.20f;


[Header("v0.20.7 Combat Flaps")]
public bool useFlaps = true;
public float combatFlapMaxSpeed = 260f;
public float landingFlapMaxSpeed = 170f;
public float combatFlapLiftMultiplier = 1.08f;
public float combatFlapCd0Add = 0.010f;
public float landingFlapLiftMultiplier = 1.18f;
public float landingFlapCd0Add = 0.028f;

        [Header("Manual / Keyboard")]
        public float manualPitchBoost = 2.75f;
        public float manualRollBoost = 1.05f;
        public float keyboardYawAuthority = 0.55f;
        public float keyboardElevatorResponse = 34f;
        public float keyboardElevatorReleaseBlend = 8.5f;

        [Header("Weapons / Arcade Loadout")]
        public int gunAmmo = 950;
        public int missileAmmo = 4;
        public int precisionAmmo = 4;
        public int bombAmmo = 6;
        public int rocketAmmo = 28;
        public float gunMuzzleSpeed = 960f;
        public float missileMaxSpeed = 780f;
        public float missileMaxTurnRateDeg = 50f;

        public MavAircraftRuntimeProfile Clone()
        {
            return (MavAircraftRuntimeProfile)MemberwiseClone();
        }
    }
}
