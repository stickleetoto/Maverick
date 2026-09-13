using UnityEngine;

namespace MaverickFresh
{
    public class MavFreshHud : MonoBehaviour
    {
        public MavMouseFlightRig mouseFlight;
        public MavMouseFlightJet jet;
        public MavInstructorController instructor;
        public Camera playerCam;
        public MavFlightDataRecorder recorder;
        public MavSimpleAIPilot aiPilot;
        public MavCASTargetingSystem casTargeting;
        public MavCASWeaponSystem casWeapons;
        public MavCASCCIPPredictor casCCIP;
        public MavTargetingPodSystem targetingPod;
        public MavWTFeelPolishController wtPolish;
        public MavPhysicalAIController physicalAI;
        public MavPhysicalAIRewardLogger rewardLogger;
        public MavAircraftProfileApplier aircraftProfile;
        public MavAeroBody aeroBody;
        public MavAtmosphericEngine atmosphericEngine;
        public MavCombatFlapSystem flapSystem;
        public MavLandingGearSystem landingGear;
        public MavThrustVectorControl thrustVectorControl;
        public bool visible = true;
        public bool controlDebugVisible;
        public bool showDeveloperDebugOnHud = false;
        public KeyCode controlDebugToggleKey = KeyCode.F2;

        private GUIStyle panel;
        private GUIStyle marker;

        private void Awake()
        {
            ResolveReferences();
        }

        private void Update()
        {
            if (MavFreshInput.GetKeyDown(controlDebugToggleKey))
                controlDebugVisible = !controlDebugVisible;

            if (MavFreshInput.GetKeyDown(KeyCode.F12))
                visible = !visible;
        }

        private void OnGUI()
        {
            if (!visible && !controlDebugVisible)
                return;

            EnsureStyles();
            ResolveReferences();

            if (visible && mouseFlight != null && playerCam != null)
            {
                DrawWorldMarker(mouseFlight.BoresightPos, "+", 20);
                DrawWorldMarker(mouseFlight.MouseAimPos, "AIM", 34);
                DrawWorldMarker(mouseFlight.VelocityVectorPos, "VV", 24);
                DrawCursorMarker();
            }

            if (visible)
                DrawPanel();

            if (controlDebugVisible)
                DrawControlDebugPanel();
        }

        private void ResolveReferences()
        {
            if (mouseFlight == null)
                mouseFlight = FindObjectOfType<MavMouseFlightRig>();

            if (jet == null)
                jet = FindObjectOfType<MavMouseFlightJet>();

            if (instructor == null)
                instructor = FindObjectOfType<MavInstructorController>();

            if (playerCam == null)
                playerCam = Camera.main;

            if (recorder == null)
                recorder = FindObjectOfType<MavFlightDataRecorder>();

            if (aiPilot == null)
                aiPilot = FindObjectOfType<MavSimpleAIPilot>();

            if (casTargeting == null)
                casTargeting = FindObjectOfType<MavCASTargetingSystem>();

            if (casWeapons == null)
                casWeapons = FindObjectOfType<MavCASWeaponSystem>();

            if (casCCIP == null)
                casCCIP = FindObjectOfType<MavCASCCIPPredictor>();

            if (targetingPod == null)
                targetingPod = FindObjectOfType<MavTargetingPodSystem>();

            if (wtPolish == null)
                wtPolish = FindObjectOfType<MavWTFeelPolishController>();

            if (physicalAI == null)
                physicalAI = FindObjectOfType<MavPhysicalAIController>();

            if (rewardLogger == null)
                rewardLogger = FindObjectOfType<MavPhysicalAIRewardLogger>();

            if (aircraftProfile == null)
                aircraftProfile = FindObjectOfType<MavAircraftProfileApplier>();

            if (aeroBody == null)
                aeroBody = FindObjectOfType<MavAeroBody>();

            if (atmosphericEngine == null)
                atmosphericEngine = FindObjectOfType<MavAtmosphericEngine>();

            if (flapSystem == null)
                flapSystem = FindObjectOfType<MavCombatFlapSystem>();

            if (landingGear == null)
                landingGear = FindObjectOfType<MavLandingGearSystem>();

            if (thrustVectorControl == null)
                thrustVectorControl = FindObjectOfType<MavThrustVectorControl>();
        }

        private void DrawCursorMarker()
        {
            if (mouseFlight == null || mouseFlight.aimInputMode != MavAimInputMode.CursorPosition)
                return;

            Vector2 p = new Vector2(mouseFlight.cursorViewport.x * Screen.width, (1f - mouseFlight.cursorViewport.y) * Screen.height);
            GUI.Box(new Rect(p.x - 7f, p.y - 7f, 14f, 14f), ".", marker);
        }

        private void DrawWorldMarker(Vector3 world, string label, int size)
        {
            Vector3 p = playerCam.WorldToScreenPoint(world);
            if (p.z <= 1f)
                return;

            Rect r = new Rect(p.x - size * 0.5f, Screen.height - p.y - size * 0.5f, size, size);
            GUI.Box(r, label, marker);
        }

        private void DrawPanel()
        {
            if (jet == null)
                return;

            string text;
            float height;

            if (showDeveloperDebugOnHud)
            {
                text =
                    "MAVERICK FRESH v0.20.9 F-22 Sensors" + AircraftNameSuffix() + "\n" +
                    (wtPolish != null ? $"PRESET {wtPolish.preset}  F5/F6/F7/F8  H Help\n" : "") +
                    $"STATE {jet.state}  SPD {jet.speed:0} m/s  M {jet.machEstimate:0.00}  G {jet.gEstimate:0.0}  {jet.speedRegime}\n" +
                    $"{ThrottleStatus(jet)}  EFF {jet.effectiveThrottle01:0.00}\n" +
                    $"AER AoA {jet.aoaEstimateDeg:0.0} AoS {jet.aosEstimateDeg:0.0} Vy {jet.verticalSpeed:0.0} CYAW {jet.coordinatedYawAssistOutput:0.00}\n" +
                    $"P/Y/R {jet.pitch:0.00}/{jet.yaw:0.00}/{jet.roll:0.00}  TURN {jet.turnBandFactor:0.00}\n" +
                    $"RATE {(jet.useRateBasedControl ? "ON" : "OFF")} TGT {FormatVector(jet.debugTargetAngularRateDeg)} ANG {FormatVector(jet.debugCurrentAngularRateDeg)} ERR {FormatVector(jet.debugRateErrorDeg)} RTORQ {FormatVector(jet.debugRateControlTorque)}\n" +
                    $"BANK {jet.signedBankAngle:0}  BANKTGT {jet.targetBankAngle:0}  HOLD {jet.bankHoldRollCommand:0.00}  PITCHANG {jet.signedPitchAngle:0}\n" +
                    $"STAB {(jet.attitudeStabilizer ? "ON" : "OFF")}  INST {jet.instructorState}\n" +
                    $"AUTH P/R {jet.pitchAuthorityFactor:0.00}/{jet.rollAuthorityFactor:0.00}  YAWDAMP {(jet.yawDamper ? "ON" : "OFF")}\n" +
                    $"ENV HSP {jet.highSpeedPitchLimiterFactor:0.00} GUARD {jet.aoaAosGuardFactor:0.00} VTA {jet.velocityTurnAssistCurrentFactor:0.00} DRAG {jet.envelopeDragAmount:0.0} CLAMP {Bool01(jet.debugFinalTorqueClampActive)}\n" +
                    AeroHudLine() +
                    EngineAndFlapHudLine() +
                    GearHudLine() +
                    TvcHudLine() +
                    (mouseFlight != null ? $"RZONE {(mouseFlight.isInRollZone ? "ROLL" : "NO-ROLL")} CMD {mouseFlight.screenRollCommand:0.00}\n" : "") +
                    $"ANGLE {jet.angleOffTarget:0.0}  localFlyTarget {jet.localFlyTarget.x:0.00},{jet.localFlyTarget.y:0.00},{jet.localFlyTarget.z:0.00}\n" +
                    $"ROLL aggressive {jet.aggressiveRoll:0.00}  wings {jet.wingsLevelRoll:0.00}  mix {jet.wingsLevelInfluence:0.00}\n" +
                    (mouseFlight != null ? $"RIG {mouseFlight.rigState} MODE {mouseFlight.aimInputMode}\n" : "") +
                    "AIM MouseAim  + Boresight  VV Velocity  . Cursor\n" +
                    (recorder != null ? $"REC {(recorder.isRecording ? "ON" : "OFF")} SAMPLES {recorder.samplesWritten}  " : "") +
                    (aiPilot != null ? $"AI {aiPilot.mode} {aiPilot.aiState}\n" : "\n") +
                    (casWeapons != null ? $"CAS PRIMARY Gun  SECONDARY {casWeapons.selectedSecondaryWeapon}  GUN {casWeapons.gunAmmo} RKT {casWeapons.rocketAmmo} BOMB {casWeapons.bombAmmo} PGM {casWeapons.precisionAmmo} MSL {casWeapons.missileAmmo} KILL {casWeapons.destroyedCount}\n" : "") +
                    (casTargeting != null ? $"TGT {(casTargeting.designatedTarget != null ? casTargeting.designatedTarget.displayName : casTargeting.candidateTarget != null ? "CAND:" + casTargeting.candidateTarget.displayName : casTargeting.status)}\n" : "") +
                    (targetingPod != null ? $"TGP {targetingPod.displayMode} {(targetingPod.isLocked ? "LOCK" : "SEARCH")} FOV {targetingPod.fov:0.0}\n" : "") +
                    (physicalAI != null ? $"PAI {(physicalAI.aiEnabled ? "ON" : "OFF")} {physicalAI.mode} {physicalAI.aiState}  F11 Toggle F3 Mode\n" : "") +
                    (rewardLogger != null ? $"RWD {(rewardLogger.isRecording ? "REC" : "OFF")} {rewardLogger.totalReward:0.00} F4 Log\n" : "") +
                    "Mouse aim | Mouse0 neon gun | Space missile/secondary | G gear | 1/2 secondary | W/S pitch | A/D roll | Shift/Ctrl throttle";
                height = 294f;
            }
            else
            {
                text =
                    "MAVERICK FRESH v0.20.9" + AircraftNameSuffix() + "\n" +
                    (wtPolish != null ? $"PRESET {wtPolish.preset}  F5/F6/F7\n" : "") +
                    $"SPD {jet.speed:0} m/s  M {jet.machEstimate:0.00}  ALT {jet.transform.position.y:0}m  G {jet.gEstimate:0.0}\n" +
                    $"{ThrottleStatus(jet)}  P/Y/R {jet.pitch:0.00}/{jet.yaw:0.00}/{jet.roll:0.00}  {jet.speedRegime}\n" +
                    ShortAeroHudLine() +
                    ShortEngineAndFlapHudLine() +
                    ShortGearHudLine() +
                    ShortTvcHudLine() +
                    TvcHudLine() +
                    (casWeapons != null ? $"GUN {casWeapons.gunAmmo}  SEC {casWeapons.selectedSecondaryWeapon}  RKT {casWeapons.rocketAmmo}  BOMB {casWeapons.bombAmmo}  MSL {casWeapons.missileAmmo}\n" : "") +
                    (targetingPod != null ? $"TGP {targetingPod.displayMode} {(targetingPod.isLocked ? "LOCK" : "SEARCH")}\n" : "") +
                    "F2 debug | F12 HUD | Mouse0 neon gun | Space missile/secondary | G gear | W/S pitch | A/D roll";
                height = 144f;
            }

            GUI.Box(new Rect(10, 10, 620, height), text, panel);
        }

        private void DrawControlDebugPanel()
        {
            MavInstructorController inst = instructor;
            MavMouseFlightJet activeJet = jet;

            if (inst == null && activeJet != null)
                inst = activeJet.instructor;

            Vector2 mouseOffset = mouseFlight != null ? mouseFlight.cursorOffsetFromCenter : Vector2.zero;
            bool w = MavFreshInput.GetKey(KeyCode.W) || MavFreshInput.GetKey(KeyCode.UpArrow);
            bool s = MavFreshInput.GetKey(KeyCode.S) || MavFreshInput.GetKey(KeyCode.DownArrow);
            bool a = MavFreshInput.GetKey(KeyCode.A) || MavFreshInput.GetKey(KeyCode.LeftArrow);
            bool d = MavFreshInput.GetKey(KeyCode.D) || MavFreshInput.GetKey(KeyCode.RightArrow);
            bool q = MavFreshInput.GetKey(KeyCode.Q);
            bool e = MavFreshInput.GetKey(KeyCode.E);
            bool tgpFocus = targetingPod != null && targetingPod.displayMode == MavTargetingPodDisplayMode.Fullscreen;
            Rigidbody rb = activeJet != null ? activeJet.GetComponent<Rigidbody>() : null;
            Vector3 angularVelocity = rb != null ? rb.angularVelocity : Vector3.zero;

            string inputLine = inst != null
                ? $"INPUT W/S {Bool01(w)}/{Bool01(s)} A/D {Bool01(a)}/{Bool01(d)} Q/E {Bool01(q)}/{Bool01(e)} mouse {mouseOffset.x:0.00},{mouseOffset.y:0.00} TGPFOCUS {Bool01(tgpFocus)}"
                : "INPUT W/S PITCH: instructor missing";

            string instructorLine = inst != null
                ? $"INST P/Y/R {inst.pitch:0.00}/{inst.yaw:0.00}/{inst.roll:0.00} THR {inst.throttlePercent:0} EFF {inst.effectiveThrottle01:0.00} ENG {Bool01(inst.engineOn)} A/B {Bool01(inst.afterburnerActive)} CUT {Bool01(inst.negativeThrottleActive)} BANKTGT {inst.targetBankAngle:0} HOLD {inst.bankHoldRollCommand:0.00} CYAW {inst.coordinatedYawAssistOutput:0.00} {inst.instructorState}"
                : "INST P/Y/R --/--/--";

            string pitchLine = inst != null
                ? $"PITCH key {inst.debugKeyboardPitchInput:0.00} mouseP/Y {inst.debugMousePitchInput:0.00}/{inst.debugMouseYawInput:0.00} raw/final {inst.debugRawPitchCommand:0.00}/{inst.debugFinalPitchCommand:0.00} AUTH P/R {inst.debugPitchAuthorityFactor:0.00}/{inst.debugRollAuthorityFactor:0.00}"
                : "PITCH --";

            string manualLine = inst != null
                ? $"MAN BOOST P/R/Y {Bool01(inst.debugManualPitchBoostActive)}/{Bool01(inst.debugManualRollBoostActive)}/{Bool01(inst.debugManualYawBoostActive)} KEY P/R {inst.debugManualPitchInput:0.00}/{inst.debugManualRollInput:0.00} RAW {inst.debugRawPitchCommand:0.00}/{inst.debugRawYawCommand:0.00}/{inst.debugRawRollCommand:0.00} SM {inst.debugFinalPitchCommand:0.00}/{inst.debugFinalYawCommand:0.00}/{inst.debugFinalRollCommand:0.00}"
                : "MAN BOOST --";

            string jetLine = activeJet != null
                ? $"JET P/Y/R {activeJet.pitch:0.00}/{activeJet.yaw:0.00}/{activeJet.roll:0.00} SPD {activeJet.speed:0} Vy {activeJet.verticalSpeed:0.0} AoA/AoS {activeJet.aoaEstimateDeg:0.0}/{activeJet.aosEstimateDeg:0.0} G {activeJet.gEstimate:0.0}"
                : "JET P/Y/R --/--/--  SPD --  AoA/AoS --/--  G --";

            string physicsLine = activeJet != null
                ? $"LOCALVEL {FormatVector(activeJet.localVelocity)} ANGVEL {FormatVector(angularVelocity)} FWDANG {activeJet.forwardVelocityAngleDeg:0.0} ALIGN {activeJet.forwardVelocityAlignment:0.00}"
                : "LOCALVEL -- ANGVEL -- FWDANG -- ALIGN --";

            string torqueLine = activeJet != null
                ? $"TORQUE {FormatVector(activeJet.debugAppliedTorque)} MODE {activeJet.debugTorqueMode} ASSIST {Bool01(activeJet.debugDirectManualAssistActive)} SEMI-AERO {(activeJet.debugSemiAeroActive ? "ON" : "OFF")} SLIP {activeJet.debugSideSlipAmount:0.0} {ThrottleStatus(activeJet)}"
                : "TORQUE -- MODE -- SEMI-AERO --";

            string envelopeLine = activeJet != null
                ? $"ENV HSP {activeJet.highSpeedPitchLimiterFactor:0.00} GUARD {activeJet.aoaAosGuardFactor:0.00} CLAMP {Bool01(activeJet.debugFinalTorqueClampActive)} VTA {activeJet.velocityTurnAssistCurrentFactor:0.00} DRAG {activeJet.envelopeDragAmount:0.0} AAUTH {activeJet.debugAeroBodyAuthority:0.00} CTORQ {FormatVector(activeJet.debugControlTorqueAfterClamp)}"
                : "ENV HSP -- GUARD -- CLAMP -- VTA -- DRAG --";

            string actuatorLine = activeJet != null
                ? $"PIPE REC {activeJet.debugReceivedPitchCommand:0.00}/{activeJet.debugReceivedYawCommand:0.00}/{activeJet.debugReceivedRollCommand:0.00} ACT {activeJet.debugActuatedPitchCommand:0.00}/{activeJet.debugActuatedYawCommand:0.00}/{activeJet.debugActuatedRollCommand:0.00} FTORQ {FormatVector(activeJet.debugFinalTorque)}"
                : "ACT --";

            string rateLine = activeJet != null
                ? $"RATE {(activeJet.useRateBasedControl ? "ON" : "OFF")} TGT {FormatVector(activeJet.debugTargetAngularRateDeg)} ANG {FormatVector(activeJet.debugCurrentAngularRateDeg)} ERR {FormatVector(activeJet.debugRateErrorDeg)} RTORQ {FormatVector(activeJet.debugRateControlTorque)} HYST {Bool01(activeJet.debugRollHysteresisActive)} CENTER {Bool01(activeJet.debugMouseCenterQuiet)} MAN {Bool01(activeJet.debugManualPitchActive)}/{Bool01(activeJet.debugManualRollActive)}/{Bool01(activeJet.debugManualYawActive)}"
                : "RATE --";

            string text =
                "CONTROL DEBUG F2\n" +
                inputLine + "\n" +
                pitchLine + "\n" +
                manualLine + "\n" +
                instructorLine + "\n" +
                jetLine + "\n" +
                physicsLine + "\n" +
                envelopeLine + "\n" +
                actuatorLine + "\n" +
                rateLine + "\n" +
                AeroDebugLine() + "\n" +
                EngineFlapDebugLine() + "\n" +
                GearDebugLine() + "\n" +
                TvcDebugLine() + "\n" +
                torqueLine;

            float y = visible ? (showDeveloperDebugOnHud ? 282f : 144f) : 10f;
            GUI.Box(new Rect(10, y, 920, 264), text, panel);
        }

        private void EnsureStyles()
        {
            if (panel != null)
                return;

            panel = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 13 };
            marker = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontSize = 14 };
        }

        private string AeroHudLine()
        {
            if (aeroBody == null)
                aeroBody = FindObjectOfType<MavAeroBody>();

            if (atmosphericEngine == null)
                atmosphericEngine = FindObjectOfType<MavAtmosphericEngine>();

            if (flapSystem == null)
                flapSystem = FindObjectOfType<MavCombatFlapSystem>();

            if (landingGear == null)
                landingGear = FindObjectOfType<MavLandingGearSystem>();

            if (thrustVectorControl == null)
                thrustVectorControl = FindObjectOfType<MavThrustVectorControl>();
            if (aeroBody == null)
                return string.Empty;
            return $"AERO {(aeroBody.useAeroBody ? "ON" : "OFF")} BLEND {aeroBody.aeroBlend:0.00} CL {aeroBody.debugCl:0.00} CD {aeroBody.debugCd:0.00} STALL {aeroBody.debugStallFactor:0.00} QAUTH {aeroBody.debugControlAuthority:0.00} LG {aeroBody.debugLiftG:0.0} DG {aeroBody.debugDragG:0.0}\n";
        }


private string EngineAndFlapHudLine()
{
    if (atmosphericEngine == null)
        atmosphericEngine = FindObjectOfType<MavAtmosphericEngine>();
    if (flapSystem == null)
        flapSystem = FindObjectOfType<MavCombatFlapSystem>();

    string engine = atmosphericEngine != null
        ? $"ENG {(atmosphericEngine.useAtmosphericEngine ? "ON" : "OFF")} THR {atmosphericEngine.debugThrustScale:0.00} ALT {atmosphericEngine.debugAltitudeScale:0.00} RAM {atmosphericEngine.debugRamScale:0.00} WDRAG {atmosphericEngine.debugWaveDragAccel:0.0}"
        : "ENG --";
    string flaps = flapSystem != null
        ? $" FLAP {flapSystem.debugState} {(flapSystem.debugAutoRetracted ? "AUTO-UP" : "")}"
        : " FLAP --";
    return engine + flaps + "\n";
}

private string ShortEngineAndFlapHudLine()
{
    if (atmosphericEngine == null)
        atmosphericEngine = FindObjectOfType<MavAtmosphericEngine>();
    if (flapSystem == null)
        flapSystem = FindObjectOfType<MavCombatFlapSystem>();

    string engine = atmosphericEngine != null ? $"ENG {atmosphericEngine.debugThrustScale:0.00}" : "ENG --";
    string flaps = flapSystem != null ? $" FLAP {flapSystem.debugState}" : " FLAP --";
    return engine + flaps + "\n";
}

        private string ShortAeroHudLine()
        {
            if (aeroBody == null)
                aeroBody = FindObjectOfType<MavAeroBody>();

            if (atmosphericEngine == null)
                atmosphericEngine = FindObjectOfType<MavAtmosphericEngine>();

            if (flapSystem == null)
                flapSystem = FindObjectOfType<MavCombatFlapSystem>();

            if (landingGear == null)
                landingGear = FindObjectOfType<MavLandingGearSystem>();

            if (thrustVectorControl == null)
                thrustVectorControl = FindObjectOfType<MavThrustVectorControl>();
            if (aeroBody == null)
                return string.Empty;
            return $"AERO {(aeroBody.useAeroBody ? "ON" : "OFF")} CL {aeroBody.debugCl:0.00} STALL {aeroBody.debugStallFactor:0.00} QAUTH {aeroBody.debugControlAuthority:0.00}\n";
        }

        private string AeroDebugLine()
        {
            if (aeroBody == null)
                aeroBody = FindObjectOfType<MavAeroBody>();

            if (atmosphericEngine == null)
                atmosphericEngine = FindObjectOfType<MavAtmosphericEngine>();

            if (flapSystem == null)
                flapSystem = FindObjectOfType<MavCombatFlapSystem>();

            if (landingGear == null)
                landingGear = FindObjectOfType<MavLandingGearSystem>();

            if (thrustVectorControl == null)
                thrustVectorControl = FindObjectOfType<MavThrustVectorControl>();
            if (aeroBody == null)
                return "AERO --";
            return $"AERO {(aeroBody.useAeroBody ? "ON" : "OFF")} BLEND {aeroBody.aeroBlend:0.00} RHO {aeroBody.debugAirDensity:0.00} Q {aeroBody.debugDynamicPressure:0} AoA/AoS {aeroBody.debugAoADeg:0.0}/{aeroBody.debugAoSDeg:0.0} CL/CD {aeroBody.debugCl:0.00}/{aeroBody.debugCd:0.00} L/DG {aeroBody.debugLiftG:0.0}/{aeroBody.debugDragG:0.0} STALL {aeroBody.debugStallFactor:0.00} QAUTH {aeroBody.debugControlAuthority:0.00}";
        }


private string EngineFlapDebugLine()
{
    if (atmosphericEngine == null)
        atmosphericEngine = FindObjectOfType<MavAtmosphericEngine>();
    if (flapSystem == null)
        flapSystem = FindObjectOfType<MavCombatFlapSystem>();

    string engine = atmosphericEngine != null
        ? $"ENG {(atmosphericEngine.useAtmosphericEngine ? "ON" : "OFF")} M {atmosphericEngine.debugMach:0.00} THR {atmosphericEngine.debugThrustScale:0.00} ALT/RAM {atmosphericEngine.debugAltitudeScale:0.00}/{atmosphericEngine.debugRamScale:0.00} WDRAG {atmosphericEngine.debugWaveDragAccel:0.0}"
        : "ENG --";
    string flap = flapSystem != null
        ? $"FLAP {flapSystem.debugState} SPD {flapSystem.debugSpeed:0} AUTO {Bool01(flapSystem.debugAutoRetracted)} BASE {Bool01(flapSystem.debugHasBaseline)}"
        : "FLAP --";
    return engine + "  " + flap;
}

        private string GearHudLine()
        {
            if (landingGear == null)
                landingGear = FindObjectOfType<MavLandingGearSystem>();
            if (landingGear == null)
                return string.Empty;
            return $"GEAR {landingGear.status} G TOGGLE DRAG {landingGear.debugDragAccel:0.0}\n";
        }

        private string ShortGearHudLine()
        {
            if (landingGear == null)
                landingGear = FindObjectOfType<MavLandingGearSystem>();
            if (landingGear == null)
                return string.Empty;
            return $"GEAR {(landingGear.gearDown ? "DOWN" : "UP")}\n";
        }

        private string GearDebugLine()
        {
            if (landingGear == null)
                landingGear = FindObjectOfType<MavLandingGearSystem>();
            if (landingGear == null)
                return "GEAR --";
            return $"GEAR {landingGear.state} DOWN {Bool01(landingGear.gearDown)} VIS {Bool01(landingGear.hasGearVisuals)} ANIM {landingGear.debugAnim01:0.00} SPD {landingGear.debugSpeed:0} DRAG {landingGear.debugDragAccel:0.0}";
        }

        private string TvcHudLine()
        {
            if (thrustVectorControl == null)
                thrustVectorControl = FindObjectOfType<MavThrustVectorControl>();
            if (thrustVectorControl == null)
                return string.Empty;
            return $"TVC {(thrustVectorControl.useThrustVectorControl ? "ON" : "OFF")} ACT {Bool01(thrustVectorControl.debugActive)} AOA {thrustVectorControl.debugAoAFactor:0.00} SPD {thrustVectorControl.debugSpeedFactor:0.00} TORQ {FormatVector(thrustVectorControl.debugTorque)}\n";
        }

        private string ShortTvcHudLine()
        {
            if (thrustVectorControl == null)
                thrustVectorControl = FindObjectOfType<MavThrustVectorControl>();
            if (thrustVectorControl == null || !thrustVectorControl.useThrustVectorControl)
                return string.Empty;
            return $"TVC {(thrustVectorControl.debugActive ? "ACTIVE" : "ARMED")}\n";
        }

        private string TvcDebugLine()
        {
            if (thrustVectorControl == null)
                thrustVectorControl = FindObjectOfType<MavThrustVectorControl>();
            if (thrustVectorControl == null)
                return "TVC --";
            return $"TVC {(thrustVectorControl.useThrustVectorControl ? "ON" : "OFF")} ACT {Bool01(thrustVectorControl.debugActive)} AOAFADE {thrustVectorControl.debugAoAFactor:0.00} SPEEDFADE {thrustVectorControl.debugSpeedFactor:0.00} THR {thrustVectorControl.debugThrottleFactor:0.00} TORQ {FormatVector(thrustVectorControl.debugTorque)}";
        }

        private string Bool01(bool value)
        {
            return value ? "1" : "0";
        }

        private string AircraftNameSuffix()
        {
            if (aircraftProfile == null)
                aircraftProfile = FindObjectOfType<MavAircraftProfileApplier>();

            if (aeroBody == null)
                aeroBody = FindObjectOfType<MavAeroBody>();

            if (atmosphericEngine == null)
                atmosphericEngine = FindObjectOfType<MavAtmosphericEngine>();

            if (flapSystem == null)
                flapSystem = FindObjectOfType<MavCombatFlapSystem>();

            if (landingGear == null)
                landingGear = FindObjectOfType<MavLandingGearSystem>();

            if (thrustVectorControl == null)
                thrustVectorControl = FindObjectOfType<MavThrustVectorControl>();

            if (aircraftProfile == null)
                return string.Empty;

            // Show the aircraft that has actually been applied. The applier's serialized request
            // field would happily label the HUD "F22A" on an aircraft nothing has configured yet.
            if (!aircraftProfile.HasAuthoritativeAircraft)
                return string.Empty;

            return " | " + aircraftProfile.AppliedAircraft.ToString();
        }

        private string ThrottleStatus(MavMouseFlightJet activeJet)
        {
            if (activeJet == null)
                return "THR --";

            float pct = activeJet.displayedThrottlePercent;
            if (!activeJet.engineOn)
                return "ENGINE OFF";

            if (pct <= activeJet.minThrottlePercent + 0.1f)
                return $"CUT {pct:0}";

            if (pct <= activeJet.idleThrottlePercent + 0.1f)
                return $"IDLE {pct:0}";

            if (activeJet.useAfterburner && pct > activeJet.afterburnerStartPercent)
                return $"A/B {pct:0}";

            return $"THR {pct:0}";
        }

        private string FormatVector(Vector3 v)
        {
            return $"{v.x:0.0},{v.y:0.0},{v.z:0.0}";
        }
    }
}
