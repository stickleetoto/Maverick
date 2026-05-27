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
        public bool visible = true;
        public bool controlDebugVisible;
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

            string text =
                "MAVERICK FRESH v0.18 Telemetry Instructor\n" +
                (wtPolish != null ? $"PRESET {wtPolish.preset}  F5/F6/F7/F8  H Help\n" : "") +
                $"STATE {jet.state}  SPD {jet.speed:0} m/s  M {jet.machEstimate:0.00}  G {jet.gEstimate:0.0}  {jet.speedRegime}\n" +
                $"THR {jet.throttle:0.00} EFF {jet.effectiveThrottle:0.00}\n" +
                $"AER AoA {jet.aoaEstimateDeg:0.0} AoS {jet.aosEstimateDeg:0.0} Vy {jet.verticalSpeed:0.0} CYAW {jet.coordinatedYawAssistOutput:0.00}\n" +
                $"P/Y/R {jet.pitch:0.00}/{jet.yaw:0.00}/{jet.roll:0.00}  TURN {jet.turnBandFactor:0.00}\n" +
                $"BANK {jet.signedBankAngle:0}  BANKTGT {jet.targetBankAngle:0}  HOLD {jet.bankHoldRollCommand:0.00}  PITCHANG {jet.signedPitchAngle:0}\n" +
                $"STAB {(jet.attitudeStabilizer ? "ON" : "OFF")}  INST {jet.instructorState}\n" +
                $"AUTH P/R {jet.pitchAuthorityFactor:0.00}/{jet.rollAuthorityFactor:0.00}  YAWDAMP {(jet.yawDamper ? "ON" : "OFF")}\n" +
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
                "Mouse aim | Mouse0 gun | Space secondary | 1/2 secondary select | W/S pitch | A/D roll | Shift/Ctrl throttle";

            GUI.Box(new Rect(10, 10, 620, 245), text, panel);
        }

        private void DrawControlDebugPanel()
        {
            MavInstructorController inst = instructor;
            MavMouseFlightJet activeJet = jet;

            if (inst == null && activeJet != null)
                inst = activeJet.instructor;

            Vector2 mouseOffset = mouseFlight != null ? mouseFlight.cursorOffsetFromCenter : Vector2.zero;
            string inputLine = inst != null
                ? $"INPUT W/S PITCH: W {(inst.debugWPressed ? "1" : "0")} S {(inst.debugSPressed ? "1" : "0")} key {inst.debugKeyboardPitchInput:0.00} pre {inst.debugFinalPitchBeforeSmoothing:0.00} out {inst.debugFinalPitchAfterSmoothing:0.00}"
                : "INPUT W/S PITCH: instructor missing";

            string instructorLine = inst != null
                ? $"INST P/Y/R {inst.pitch:0.00}/{inst.yaw:0.00}/{inst.roll:0.00}  mouse {mouseOffset.x:0.00},{mouseOffset.y:0.00}"
                : $"INST P/Y/R --/--/--  mouse {mouseOffset.x:0.00},{mouseOffset.y:0.00}";

            string jetLine = activeJet != null
                ? $"JET P/Y/R {activeJet.pitch:0.00}/{activeJet.yaw:0.00}/{activeJet.roll:0.00}  SPD {activeJet.speed:0}  AoA/AoS {activeJet.aoaEstimateDeg:0.0}/{activeJet.aosEstimateDeg:0.0}  G {activeJet.gEstimate:0.0}"
                : "JET P/Y/R --/--/--  SPD --  AoA/AoS --/--  G --";

            string bankLine = activeJet != null
                ? $"BANK target {activeJet.targetBankAngle:0} hold {activeJet.bankHoldRollCommand:0.00}  torque {(activeJet.useAccelerationTorqueMode ? "Acceleration" : "Force")}"
                : "BANK target -- hold --  torque --";

            string text =
                "CONTROL DEBUG F2\n" +
                inputLine + "\n" +
                instructorLine + "\n" +
                jetLine + "\n" +
                bankLine;

            float y = visible ? 262f : 10f;
            GUI.Box(new Rect(10, y, 620, 92), text, panel);
        }

        private void EnsureStyles()
        {
            if (panel != null)
                return;

            panel = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 13 };
            marker = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontSize = 14 };
        }
    }
}
