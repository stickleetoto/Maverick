using System.Reflection;
using UnityEngine;

namespace MaverickFresh
{
    [DisallowMultipleComponent]
    public class MavAircraftProfileApplier : MonoBehaviour
    {
        public MavAircraftKind aircraft = MavAircraftKind.F22A;
        public bool applyOnStart = false;
        public bool renameObject = false;
        [Tooltip("Player profile changes may update the global mouse-flight rig. Enemy aircraft should keep this off.")]
        public bool allowGlobalRigLookup = true;
        public string lastApplied = "none";

        private void Start()
        {
            if (applyOnStart)
                ApplyAircraft(aircraft);
        }

        [ContextMenu("Apply Selected Aircraft")]
        public void ApplySelected()
        {
            ApplyAircraft(aircraft);
        }

        public void ApplyAircraft(MavAircraftKind kind)
        {
            MavAircraftRuntimeProfile profile = MavAircraftCatalog.GetBuiltIn(kind);
            ApplyProfile(profile);
        }

        public void ApplyProfile(MavAircraftRuntimeProfile profile)
        {
            if (profile == null)
                return;

            aircraft = profile.aircraft;
            if (renameObject)
                gameObject.name = profile.aircraftId.ToUpperInvariant() + "_Player";

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.mass = profile.mass;
                rb.useGravity = false;
                rb.linearDamping = profile.linearDamping;
                rb.angularDamping = profile.angularDamping;
                rb.maxAngularVelocity = Mathf.Max(1f, profile.maxAngularVelocity);
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }

            MavMouseFlightJet jet = GetComponent<MavMouseFlightJet>();
            if (jet != null)
                ApplyToJet(jet, profile);

            MavAeroBody aero = GetComponent<MavAeroBody>();
            if (aero != null)
                ApplyToAeroBody(aero, profile);

            MavAtmosphericEngine engine = GetComponent<MavAtmosphericEngine>();
            if (engine != null)
                ApplyToAtmosphericEngine(engine, profile);

            MavCombatFlapSystem flaps = GetComponent<MavCombatFlapSystem>();
            if (flaps != null)
                ApplyToFlaps(flaps, profile);

            MavThrustVectorControl tvc = GetComponent<MavThrustVectorControl>();
            if (tvc != null)
                ApplyToThrustVectorControl(tvc, profile);

            MavRadarSignature signature = GetComponent<MavRadarSignature>();
            if (signature != null)
                ApplyToRadarSignature(signature, profile);

            MavF22SensorSuite sensors = GetComponent<MavF22SensorSuite>();
            if (sensors != null)
                sensors.ApplyProfile(profile);

            MavMouseFlightRig rig = GetComponent<MavMouseFlightRig>();
            if (rig == null)
                rig = GetComponentInChildren<MavMouseFlightRig>();
            if (rig == null && allowGlobalRigLookup)
                rig = FindObjectOfType<MavMouseFlightRig>();
            if (rig != null)
                ApplyToRig(rig, profile);

            MavInstructorController instructor = GetComponent<MavInstructorController>();
            if (instructor != null)
                ApplyToInstructor(instructor, profile);

            MavWTFeelPolishController wt = GetComponent<MavWTFeelPolishController>();
            if (wt != null)
            {
                wt.applyOnStart = false;
                wt.jet = jet;
                wt.instructor = instructor;
                wt.rig = rig;
                // Keep F5/F6/F7 usable, but aircraft profile is the top-level identity pass.
            }

            MavF15EFlightSpecProfile oldF15 = GetComponent<MavF15EFlightSpecProfile>();
            if (oldF15 != null)
                oldF15.applyOnStart = false;

            MavCASWeaponSystem weapons = GetComponent<MavCASWeaponSystem>();
            if (weapons != null)
                ApplyToWeapons(weapons, profile);

            MavAircraftVisualSwitcher visualSwitcher = GetComponent<MavAircraftVisualSwitcher>();
            if (visualSwitcher != null)
                visualSwitcher.ApplyAircraft(profile);

            lastApplied = profile.displayName + " / " + System.DateTime.Now.ToString("HH:mm:ss");
            Debug.Log("MavAircraftProfileApplier applied " + profile.displayName + " to " + name);
        }

        private void ApplyToJet(MavMouseFlightJet jet, MavAircraftRuntimeProfile p)
        {
            Set(jet, "thrust", p.thrust);
            Set(jet, "turnTorque", p.turnTorque);
            Set(jet, "accelerationModeTorque", p.turnTorque);
            Set(jet, "forceModeTorque", p.forceModeTorque);
            Set(jet, "maxAppliedTorqueAccelerationMode", p.maxAppliedTorqueAccelerationMode);
            Set(jet, "maxAppliedTorqueForceMode", p.maxAppliedTorqueForceMode);
            Set(jet, "maxAppliedTorque", p.maxAppliedTorqueAccelerationMode);
            Set(jet, "forceMult", p.forceMult);
            Set(jet, "linearDamping", p.linearDamping);
            Set(jet, "angularDamping", p.angularDamping);
            Set(jet, "maxAngularVelocity", p.maxAngularVelocity);

            Set(jet, "sensitivity", p.mouseSensitivity);
            Set(jet, "pitchGain", p.pitchGain);
            Set(jet, "yawGain", p.yawGain);
            Set(jet, "rollGain", p.rollGain);
            Set(jet, "maxAutoPitch", p.maxAutoPitch);
            Set(jet, "noseDownTrim", p.noseDownTrim);
            Set(jet, "inputSmoothing", p.inputSmoothing);
            Set(jet, "maxScreenRollBankAngle", p.maxScreenRollBankAngle);

            Set(jet, "useRateBasedControl", true);
            Set(jet, "useAccelerationTorqueMode", true);
            Set(jet, "targetPitchRateDeg", p.targetPitchRateDeg);
            Set(jet, "targetYawRateDeg", p.targetYawRateDeg);
            Set(jet, "targetRollRateDeg", p.targetRollRateDeg);
            Set(jet, "rateControlP", p.rateControlP);
            Set(jet, "rateControlD", p.rateControlD);
            Set(jet, "maxRateControlTorque", p.maxRateControlTorque);

            Set(jet, "autoSpeedAssist", true);
            Set(jet, "targetCruiseSpeed", p.targetCruiseSpeed);
            Set(jet, "minCombatSpeed", p.minCombatSpeed);
            Set(jet, "maxCombatSpeed", p.maxCombatSpeed);
            Set(jet, "lowSpeedThrustBoost", p.lowSpeedThrustBoost);
            Set(jet, "overspeedDrag", p.overspeedDrag);
            Set(jet, "speedAssistStrength", p.speedAssistStrength);

            Set(jet, "bestTurnSpeed", p.bestTurnSpeed);
            Set(jet, "turnBandWidth", p.turnBandWidth);
            Set(jet, "bestTurnPitchBoost", p.bestTurnPitchBoost);
            Set(jet, "bestTurnRollBoost", p.bestTurnRollBoost);
            Set(jet, "lowSpeedPitchAuthority", p.lowSpeedPitchAuthority);
            Set(jet, "bestSpeedPitchAuthority", p.bestSpeedPitchAuthority);
            Set(jet, "highSpeedPitchAuthority", p.highSpeedPitchAuthority);
            Set(jet, "lowSpeedRollAuthority", p.lowSpeedRollAuthority);
            Set(jet, "bestSpeedRollAuthority", p.bestSpeedRollAuthority);
            Set(jet, "highSpeedRollAuthority", p.highSpeedRollAuthority);
            Set(jet, "softGLimit", p.softGLimit);
            Set(jet, "hardGLimit", p.hardGLimit);
            Set(jet, "aoaSoftLimitDeg", p.aoaSoftLimitDeg);
            Set(jet, "aoaHardLimitDeg", p.aoaHardLimitDeg);
            Set(jet, "aoaPitchReduction", p.aoaPitchReduction);

            Set(jet, "manualPitchBoost", p.manualPitchBoost);
            Set(jet, "manualRollBoost", p.manualRollBoost);
            Set(jet, "keyboardYawAuthority", p.keyboardYawAuthority);
            Set(jet, "keyboardElevatorResponse", p.keyboardElevatorResponse);
            Set(jet, "keyboardElevatorReleaseBlend", p.keyboardElevatorReleaseBlend);

            Set(jet, "maxThrottle", 1.40f);
            Set(jet, "afterburnerThrustMultiplier", p.afterburnerMultiplier);
            Set(jet, "throttlePercent", p.throttlePercent);
            Set(jet, "throttleChangeRatePercentPerSecond", p.throttleChangeRate);
            Set(jet, "throttleSpoolUpRate", p.throttleSpoolUp);
            Set(jet, "throttleSpoolDownRate", p.throttleSpoolDown);
        }

        private void ApplyToAeroBody(MavAeroBody aero, MavAircraftRuntimeProfile p)
        {
            aero.useAeroBody = p.useAeroBody;
            aero.aeroBlend = p.aeroBlend;
            aero.liftBlend = p.liftBlend;
            aero.dragBlend = p.dragBlend;
            aero.gravityBlend = p.gravityBlend;
            aero.wingArea = p.wingArea;
            aero.clSlopePerDeg = p.clSlopePerDeg;
            aero.stallAoADeg = p.stallAoADeg;
            aero.fullStallAoADeg = p.fullStallAoADeg;
            aero.postStallLiftFactor = p.postStallLiftFactor;
            aero.cd0 = p.cd0;
            aero.aspectRatio = p.aspectRatio;
            aero.oswaldEfficiency = p.oswaldEfficiency;
            aero.postStallDrag = p.postStallDrag;
            aero.maxLiftG = p.maxLiftG;
            aero.maxDragG = p.maxDragG;
            aero.referenceControlSpeed = p.referenceControlSpeed;
            aero.minControlAuthority = p.minControlAuthority;
            aero.maxControlAuthority = p.maxControlAuthority;
            aero.velocityAssistFade = p.velocityAssistFade;
        }


private void ApplyToAtmosphericEngine(MavAtmosphericEngine engine, MavAircraftRuntimeProfile p)
{
    engine.useAtmosphericEngine = p.useAtmosphericEngine;
    engine.engineModelBlend = p.engineModelBlend;
    engine.highAltitudeThrustFloor = p.highAltitudeThrustFloor;
    engine.thrustScaleHeight = p.thrustScaleHeight;
    engine.ramRecovery = p.ramRecovery;
    engine.maxThrustScale = p.maxEngineThrustScale;
    engine.waveDragStrength = p.waveDragStrength;
    engine.maxWaveDragAccel = p.maxWaveDragAccel;
}

private void ApplyToFlaps(MavCombatFlapSystem flaps, MavAircraftRuntimeProfile p)
{
    flaps.useFlaps = p.useFlaps;
    flaps.combatMaxSpeed = p.combatFlapMaxSpeed;
    flaps.landingMaxSpeed = p.landingFlapMaxSpeed;
    flaps.combatLiftSlopeMultiplier = p.combatFlapLiftMultiplier;
    flaps.combatCd0Add = p.combatFlapCd0Add;
    flaps.landingLiftSlopeMultiplier = p.landingFlapLiftMultiplier;
    flaps.landingCd0Add = p.landingFlapCd0Add;
    flaps.SetState(MavFlapState.Retracted);
}

private void ApplyToThrustVectorControl(MavThrustVectorControl tvc, MavAircraftRuntimeProfile p)
{
    tvc.useThrustVectorControl = p.useThrustVectorControl;
    tvc.pitchAuthority = p.tvcPitchAuthority;
    tvc.rollAuthority = p.tvcRollAuthority;
    tvc.yawAuthority = p.tvcYawAuthority;
    tvc.activationAoADeg = p.tvcActivationAoADeg;
    tvc.fullAoADeg = p.tvcFullAoADeg;
    tvc.lowSpeedFull = p.tvcLowSpeedFull;
    tvc.lowSpeedFadeOut = p.tvcLowSpeedFadeOut;
    tvc.maxTorque = p.tvcMaxTorque;
}


private void ApplyToRadarSignature(MavRadarSignature signature, MavAircraftRuntimeProfile p)
{
    signature.displayName = p.shortName;
    signature.team = 0;
    signature.isAirTarget = true;
    signature.radarCrossSectionSqm = p.radarCrossSectionSqm;
    signature.irSignature = p.irSignature;
    signature.stealthRating = p.stealthRating;
}

        private void ApplyToRig(MavMouseFlightRig rig, MavAircraftRuntimeProfile p)
        {
            Set(rig, "mouseSensitivity", p.mouseSensitivity);
            Set(rig, "aimDistance", p.aimDistance);
            Set(rig, "cameraFov", Mathf.Lerp(64f, 72f, p.statSpeed / 10f));
            Set(rig, "zoomFov", 38f);
            Set(rig, "cameraLocalPosition", new Vector3(0f, Mathf.Lerp(4.8f, 5.8f, p.hangarScale), Mathf.Lerp(-15.8f, -18.2f, p.statPayload / 10f)));
        }

        private void ApplyToInstructor(MavInstructorController instructor, MavAircraftRuntimeProfile p)
        {
            Set(instructor, "targetPitchRateDeg", p.targetPitchRateDeg);
            Set(instructor, "targetYawRateDeg", p.targetYawRateDeg);
            Set(instructor, "targetRollRateDeg", p.targetRollRateDeg);
            Set(instructor, "maxScreenRollBankAngle", p.maxScreenRollBankAngle);
            Set(instructor, "mouseSensitivity", p.mouseSensitivity);
        }

        private void ApplyToWeapons(MavCASWeaponSystem weapons, MavAircraftRuntimeProfile p)
        {
            weapons.gunAmmo = p.gunAmmo;
            weapons.missileAmmo = p.missileAmmo;
            weapons.precisionAmmo = p.precisionAmmo;
            weapons.bombAmmo = p.bombAmmo;
            weapons.rocketAmmo = p.rocketAmmo;
            weapons.gunMuzzleSpeed = p.gunMuzzleSpeed;
            weapons.missileMaxSpeed = p.missileMaxSpeed;
            weapons.missileMaxTurnRateDeg = p.missileMaxTurnRateDeg;
        }

        private static void Set(object target, string fieldName, object value)
        {
            if (target == null || string.IsNullOrEmpty(fieldName))
                return;

            FieldInfo f = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null)
            {
                try
                {
                    if (value is float && f.FieldType == typeof(int))
                        f.SetValue(target, Mathf.RoundToInt((float)value));
                    else
                        f.SetValue(target, value);
                }
                catch { }
            }
        }
    }
}
