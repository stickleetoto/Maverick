using UnityEngine;

namespace MaverickFresh
{
    /// <summary>
    /// Optional standalone F2 diagnostic overlay for the Fresh control chain.
    /// MavFreshHud has an embedded panel too; use this in scenes without that HUD.
    /// </summary>
    [DisallowMultipleComponent]
    public class MavControlDebugOverlay : MonoBehaviour
    {
        [Header("References")]
        public MavMouseFlightRig rig;
        public MavInstructorController instructor;
        public MavMouseFlightJet jet;
        public MavTargetingPodSystem targetingPod;

        [Header("Display")]
        public KeyCode toggleKey = KeyCode.F2;
        public bool visible;
        public Rect panelRect = new Rect(10f, 10f, 860f, 300f);

        private GUIStyle panelStyle;

        private void Update()
        {
            if (MavFreshInput.GetKeyDown(toggleKey))
                visible = !visible;
        }

        private void OnGUI()
        {
            if (!visible)
                return;

            Resolve();
            EnsureStyle();
            GUI.Box(panelRect, BuildText(), panelStyle);
        }

        private void Resolve()
        {
            if (rig == null)
                rig = FindObjectOfType<MavMouseFlightRig>();

            if (jet == null)
                jet = FindObjectOfType<MavMouseFlightJet>();

            if (instructor == null && jet != null)
                instructor = jet.instructor;

            if (instructor == null)
                instructor = FindObjectOfType<MavInstructorController>();

            if (targetingPod == null)
                targetingPod = FindObjectOfType<MavTargetingPodSystem>();
        }

        private string BuildText()
        {
            bool w = MavFreshInput.GetKey(KeyCode.W) || MavFreshInput.GetKey(KeyCode.UpArrow);
            bool s = MavFreshInput.GetKey(KeyCode.S) || MavFreshInput.GetKey(KeyCode.DownArrow);
            bool a = MavFreshInput.GetKey(KeyCode.A) || MavFreshInput.GetKey(KeyCode.LeftArrow);
            bool d = MavFreshInput.GetKey(KeyCode.D) || MavFreshInput.GetKey(KeyCode.RightArrow);
            bool q = MavFreshInput.GetKey(KeyCode.Q);
            bool e = MavFreshInput.GetKey(KeyCode.E);
            bool tgpFocus = targetingPod != null && targetingPod.displayMode == MavTargetingPodDisplayMode.Fullscreen;
            Vector2 mouseOffset = rig != null ? rig.cursorOffsetFromCenter : Vector2.zero;

            string instLine = instructor != null
                ? "INST P/Y/R " + Format3(instructor.pitch, instructor.yaw, instructor.roll)
                  + "  THR " + instructor.throttlePercent.ToString("0")
                  + "  EFF " + instructor.effectiveThrottle01.ToString("0.00")
                  + "  ENG " + Bool01(instructor.engineOn)
                  + "  A/B " + Bool01(instructor.afterburnerActive)
                  + "  CUT " + Bool01(instructor.negativeThrottleActive)
                  + "  BANKTGT " + instructor.targetBankAngle.ToString("0")
                  + "  HOLD " + instructor.bankHoldRollCommand.ToString("0.00")
                  + "  CYAW " + instructor.coordinatedYawAssistOutput.ToString("0.00")
                  + "  " + instructor.instructorState
                : "INST missing";

            string jetLine = jet != null
                ? "JET SPD " + jet.speed.ToString("0")
                  + "  Vy " + jet.verticalSpeed.ToString("0.0")
                  + "  AoA/AoS " + Format2(jet.aoaEstimateDeg, jet.aosEstimateDeg)
                  + "  G " + jet.gEstimate.ToString("0.0")
                  + "  FWDANG " + jet.forwardVelocityAngleDeg.ToString("0.0")
                  + "  ALIGN " + jet.forwardVelocityAlignment.ToString("0.00")
                  + "  " + ThrottleStatus(jet)
                : "JET missing";

            string vectorLine = jet != null
                ? "LOCALVEL " + FormatVector(jet.localVelocity)
                  + "  ANGVEL " + FormatVector(jet.debugAngularVelocity)
                  + "  ANGMAG " + jet.debugAngularResponseMagnitude.ToString("0.00")
                  + "  TORQUE " + FormatVector(jet.debugAppliedTorque)
                  + "  SEMI-AERO " + (jet.debugSemiAeroActive ? "ON" : "OFF")
                : "LOCALVEL --  ANGVEL --  TORQUE --";

            string envelopeLine = jet != null
                ? "ENV HSP " + jet.highSpeedPitchLimiterFactor.ToString("0.00")
                  + "  GUARD " + jet.aoaAosGuardFactor.ToString("0.00")
                  + "  CLAMP " + Bool01(jet.debugFinalTorqueClampActive)
                  + "  VTA " + jet.velocityTurnAssistCurrentFactor.ToString("0.00")
                  + "  DRAG " + jet.envelopeDragAmount.ToString("0.0")
                  + "  CTORQ " + FormatVector(jet.debugControlTorqueAfterClamp)
                : "ENV HSP --  GUARD --  CLAMP --  VTA --  DRAG --";

            string manualLine = instructor != null
                ? "MAN BOOST P/R/Y " + Bool01(instructor.debugManualPitchBoostActive)
                  + "/" + Bool01(instructor.debugManualRollBoostActive)
                  + "/" + Bool01(instructor.debugManualYawBoostActive)
                  + "  KEY P/R " + Format2(instructor.debugManualPitchInput, instructor.debugManualRollInput)
                  + "  RAW " + Format3(instructor.debugFinalPitchBeforeSmoothing, instructor.debugFinalYawBeforeSmoothing, instructor.debugFinalRollBeforeSmoothing)
                  + "  SM " + Format3(instructor.debugFinalPitchAfterSmoothing, instructor.debugFinalYawAfterSmoothing, instructor.debugFinalRollAfterSmoothing)
                : "MAN BOOST --";

            string actuatorLine = jet != null
                ? "PIPE REC " + Format3(jet.debugReceivedPitchCommand, jet.debugReceivedYawCommand, jet.debugReceivedRollCommand)
                  + "  ACT " + Format3(jet.debugActuatedPitchCommand, jet.debugActuatedYawCommand, jet.debugActuatedRollCommand)
                  + "  FTORQ " + FormatVector(jet.debugFinalTorque)
                  + "  MODE " + jet.debugTorqueMode
                  + "  ASSIST " + Bool01(jet.debugDirectManualAssistActive)
                : "ACT --";

            string rateLine = jet != null
                ? "RATE " + (jet.useRateBasedControl ? "ON" : "OFF")
                  + "  TGT " + FormatVector(jet.debugTargetAngularRateDeg)
                  + "  ANG " + FormatVector(jet.debugCurrentAngularRateDeg)
                  + "  ERR " + FormatVector(jet.debugRateErrorDeg)
                  + "  RTORQ " + FormatVector(jet.debugRateControlTorque)
                  + "  HYST " + Bool01(jet.debugRollHysteresisActive)
                  + "  CENTER " + Bool01(jet.debugMouseCenterQuiet)
                  + "  MAN " + Bool01(jet.debugManualPitchActive) + "/" + Bool01(jet.debugManualRollActive) + "/" + Bool01(jet.debugManualYawActive)
                : "RATE --";

            return "CONTROL DEBUG F2\n"
                + "INPUT W/S " + Bool01(w) + "/" + Bool01(s)
                + "  A/D " + Bool01(a) + "/" + Bool01(d)
                + "  Q/E " + Bool01(q) + "/" + Bool01(e)
                + "  mouse " + mouseOffset.x.ToString("0.00") + "," + mouseOffset.y.ToString("0.00")
                + "  TGPFOCUS " + Bool01(tgpFocus) + "\n"
                + (instructor != null
                    ? "PITCH key " + instructor.debugKeyboardPitchInput.ToString("0.00")
                      + "  mouseP/Y " + Format2(instructor.debugMousePitchInput, instructor.debugMouseYawInput)
                      + "  raw/final " + Format2(instructor.debugRawPitchCommand, instructor.debugFinalPitchCommand)
                      + "  AUTH P/R " + Format2(instructor.debugPitchAuthorityFactor, instructor.debugRollAuthorityFactor) + "\n"
                    : "PITCH instructor missing\n")
                + manualLine + "\n"
                + instLine + "\n"
                + jetLine + "\n"
                + envelopeLine + "\n"
                + actuatorLine + "\n"
                + rateLine + "\n"
                + vectorLine;
        }

        private void EnsureStyle()
        {
            if (panelStyle != null)
                return;

            panelStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 13
            };
        }

        private string Bool01(bool value)
        {
            return value ? "1" : "0";
        }

        private string ThrottleStatus(MavMouseFlightJet activeJet)
        {
            if (activeJet == null)
                return "THR --";

            if (!activeJet.engineOn)
                return "ENGINE OFF";

            float pct = activeJet.displayedThrottlePercent;
            if (pct <= activeJet.minThrottlePercent + 0.1f)
                return "CUT " + pct.ToString("0");

            if (pct <= activeJet.idleThrottlePercent + 0.1f)
                return "IDLE " + pct.ToString("0");

            if (activeJet.useAfterburner && pct > activeJet.afterburnerStartPercent)
                return "A/B " + pct.ToString("0");

            return "THR " + pct.ToString("0");
        }

        private string Format2(float x, float y)
        {
            return x.ToString("0.00") + "/" + y.ToString("0.00");
        }

        private string Format3(float x, float y, float z)
        {
            return x.ToString("0.00") + "/" + y.ToString("0.00") + "/" + z.ToString("0.00");
        }

        private string FormatVector(Vector3 v)
        {
            return v.x.ToString("0.0") + "," + v.y.ToString("0.0") + "," + v.z.ToString("0.0");
        }
    }
}
