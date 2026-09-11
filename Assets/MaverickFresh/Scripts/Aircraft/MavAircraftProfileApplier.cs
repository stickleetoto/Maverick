using System.Reflection;
using UnityEngine;

namespace MaverickFresh
{
    [DisallowMultipleComponent]
    public class MavAircraftProfileApplier : MonoBehaviour
    {
        [Tooltip("The aircraft this component will apply when ApplySelected() or applyOnStart is used. This is a REQUEST, and its serialized default is meaningless at runtime - nothing may read it to learn which aircraft the player is actually flying. Read AppliedAircraft for that.")]
        public MavAircraftKind aircraft = MavAircraftKind.F22A;
        public bool applyOnStart = false;
        public bool renameObject = false;
        [Tooltip("Player profile changes may update the global mouse-flight rig. Enemy aircraft should keep this off.")]
        public bool allowGlobalRigLookup = true;
        public string lastApplied = "none";
        [Tooltip("Why the last aircraft application was refused, or empty. A refusal leaves the previous aircraft fully intact.")]
        [TextArea(2, 4)] public string lastApplyError = string.Empty;

        [Tooltip("The aircraft that has actually been applied, or 'none'. Read-only mirror of the authoritative identity, for the inspector.")]
        public string debugAppliedAircraft = "none";

        /// <summary>
        /// The profile that was ACTUALLY committed to this aircraft, or null if none ever was.
        ///
        /// Deliberately a plain private field: Unity does not serialize it, so a scene or prefab
        /// cannot ship a saved value that makes an aircraft look authoritatively applied when
        /// nothing has run. Authority is established at runtime by an application succeeding, and by
        /// nothing else.
        /// </summary>
        private MavAircraftRuntimeProfile appliedProfile;

        private int appliedRevision;

        /// <summary>
        /// Whether an aircraft identity has been authoritatively applied to this object yet.
        ///
        /// This is the flag every aircraft-aware consumer must check before it acts. The serialized
        /// `aircraft` field is NOT a substitute: it holds whatever the inspector was last saved with
        /// - F-22A by default - so a consumer that reads it during bootstrap will confidently
        /// configure the wrong aircraft. That is exactly the bug this property exists to make
        /// impossible to write.
        /// </summary>
        public bool HasAuthoritativeAircraft
        {
            get { return appliedProfile != null; }
        }

        /// <summary>The profile actually in force, or null when nothing has been applied.</summary>
        public MavAircraftRuntimeProfile AppliedProfile
        {
            get { return appliedProfile; }
        }

        /// <summary>
        /// The aircraft actually in force. Only meaningful when HasAuthoritativeAircraft is true;
        /// callers must check that rather than treat this as "probably right".
        /// </summary>
        public MavAircraftKind AppliedAircraft
        {
            get { return appliedProfile != null ? appliedProfile.aircraft : aircraft; }
        }

        /// <summary>
        /// Increments on every successful application. A caller can compare it across a stretch of
        /// setup to find out whether an aircraft was applied in the meantime, without having to
        /// guess from component state.
        /// </summary>
        public int AppliedRevision
        {
            get { return appliedRevision; }
        }

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
            string error;
            TryApplyAircraft(kind, out error);
        }

        /// <summary>
        /// Applies EXACTLY this aircraft kind, or nothing at all.
        ///
        /// Returns false and leaves the aircraft untouched when the identity cannot be resolved and
        /// confirmed. No other aircraft is ever substituted for the one that was asked for.
        /// </summary>
        public bool TryApplyAircraft(MavAircraftKind kind, out string error)
        {
            MavAircraftRuntimeProfile profile;
            if (!MavAircraftCatalog.TryGetBuiltIn(kind, out profile, out error))
            {
                Fail(error);
                return false;
            }

            return TryApplyProfile(profile, out error);
        }

        public void ApplyProfile(MavAircraftRuntimeProfile profile)
        {
            string error;
            TryApplyProfile(profile, out error);
        }

        public bool TryApplyProfile(MavAircraftRuntimeProfile profile, out string error)
        {
            return TryApplyProfile(profile, null, out error);
        }

        /// <summary>
        /// ATOMIC aircraft application: physics and visual identity change together, or neither
        /// changes.
        ///
        /// This used to be a straight run of "configure everything in order", with the visual
        /// switcher near the end. Two consequences followed from that shape:
        ///
        ///   - a null profile was a silent early return, so a caller whose lookup had already
        ///     fallen back to the F-22 - or failed outright - left the aircraft in whatever state
        ///     the PREVIOUS application had put it in, with no error anywhere;
        ///   - the Rigidbody, jet, aero body, engine, flaps, TVC and sensors were all reconfigured
        ///     before the visual was attempted, so a visual that could not be produced left F-16
        ///     physics under an F-22 model, or the reverse.
        ///
        /// So the visual is now PREPARED first - resolved, parented, posed, but not yet shown - and
        /// only once it exists does anything physical change. Both commits after that point are
        /// incapable of failing.
        /// </summary>
        public bool TryApplyProfile(
            MavAircraftRuntimeProfile profile,
            GameObject explicitVisualSource,
            out string error)
        {
            if (profile == null)
            {
                error = "Aircraft profile is null. The aircraft was NOT changed and no substitute "
                        + "was applied; it is still " + aircraft + ".";
                Fail(error);
                return false;
            }

            // The profile carries its own claimed identity, and a custom profile never went through
            // the catalog's checks. Confirm it before anything is configured from it.
            if (!MavAircraftCatalog.IsIdentityConsistent(profile, profile.aircraft, out error))
            {
                Fail(error);
                return false;
            }

            // ---- PHASE 1: prepare, and refuse if anything is missing -------------------------
            MavAircraftVisualSwitcher visualSwitcher = GetComponent<MavAircraftVisualSwitcher>();
            GameObject preparedVisual = null;

            if (visualSwitcher != null
                && !visualSwitcher.TryPrepareVisual(
                        profile, explicitVisualSource, out preparedVisual, out error))
            {
                error = "Refused to apply " + profile.displayName + ": " + error
                        + " Physics was left on " + aircraft + ".";
                Fail(error);
                return false;
            }

            // ---- PHASE 2: commit ------------------------------------------------------------
            ApplyProfileUnchecked(profile);

            if (visualSwitcher != null)
                visualSwitcher.CommitVisual(profile.aircraft, preparedVisual);

            // Authority is established HERE, by an application that actually completed - never by a
            // serialized field, and never by a caller asserting it.
            appliedProfile = profile;
            appliedRevision++;
            debugAppliedAircraft = profile.displayName + " (" + profile.aircraft + ")";

            lastApplyError = string.Empty;
            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Re-applies the profile that is ALREADY in force.
        ///
        /// This is what a consumer wants when it needs the aircraft configuration refreshed - for
        /// instance because it has just created the jet, or because it is about to layer tuning on
        /// top of a clean baseline. It cannot change which aircraft this is, which is the point: the
        /// only thing it can do is restate the identity that was already established.
        ///
        /// Fails when nothing has been applied yet. That is not an error to paper over by picking an
        /// aircraft - it means the caller ran before the selection did.
        /// </summary>
        public bool TryReapplyAppliedProfile(out string error)
        {
            if (appliedProfile == null)
            {
                error = "No aircraft has been authoritatively applied to " + name + " yet, so there "
                        + "is nothing to re-apply. Nothing was changed and no aircraft was chosen "
                        + "as a stand-in.";
                return false;
            }

            return TryApplyProfile(appliedProfile, out error);
        }

        private void Fail(string reason)
        {
            lastApplyError = reason;
            Debug.LogError("[Maverick/Aircraft] MavAircraftProfileApplier on " + name + ": " + reason, this);
        }

        /// <summary>
        /// The physical half of the commit. Private: everything that could refuse the change has
        /// already run, so there is no supported way to reach this with an unverified profile.
        /// </summary>
        private void ApplyProfileUnchecked(MavAircraftRuntimeProfile profile)
        {
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

            // The visual is NOT applied here. It was prepared before any of the above ran, and it
            // is committed by TryApplyProfile immediately after this returns, so that physics and
            // visual identity can never disagree about which aircraft this is.

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

            Set(jet, "usePhase4BTurnDynamics", p.usePhase4BTurnDynamics);
            Set(jet, "thrustBoostSuppressionG", p.thrustBoostSuppressionG);
            Set(jet, "releaseRateNullingScale", p.releaseRateNullingScale);
            Set(jet, "alignmentAssistFloorAtFullAero", p.alignmentAssistFloorAtFullAero);
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
            aero.usePhase4BTurnAuthority = p.usePhase4BTurnDynamics;
            aero.useAeroStaticStability = p.useAeroStaticStability;
            aero.pitchStabilityStrength = p.pitchStabilityStrength;
            aero.yawStabilityStrength = p.yawStabilityStrength;
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
