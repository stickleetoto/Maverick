using UnityEngine;
using EaglePhysicalAI.AI;
using EaglePhysicalAI.Aircraft;
using EaglePhysicalAI.Battlefield;
using EaglePhysicalAI.CAS;
using EaglePhysicalAI.Data;
using EaglePhysicalAI.Mission;

namespace EaglePhysicalAI.UI
{
    public class SimpleFlightHUD : MonoBehaviour
    {
        public AircraftPhysicsController aircraft;
        public RuleCasPilot aiPilot;
        public CasRequestManager requestManager;
        public CasValidator validator;
        public AbstractStrikeSystem strikeSystem;
        public TesterSessionLogger logger;
        public MissionScoreTracker scoreTracker;
        public bool showHud = true;

        private GUIStyle _style;

        private void Awake()
        {
            if (aircraft == null) aircraft = FindObjectOfType<AircraftPhysicsController>();
            if (aiPilot == null) aiPilot = FindObjectOfType<RuleCasPilot>();
            if (requestManager == null) requestManager = FindObjectOfType<CasRequestManager>();
            if (validator == null) validator = FindObjectOfType<CasValidator>();
            if (strikeSystem == null) strikeSystem = FindObjectOfType<AbstractStrikeSystem>();
            if (logger == null) logger = FindObjectOfType<TesterSessionLogger>();
            if (scoreTracker == null) scoreTracker = FindObjectOfType<MissionScoreTracker>();
        }

        private void OnGUI()
        {
            if (!showHud) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = 15
                };
                _style.normal.textColor = Color.white;
            }

            string text = BuildHudText();
            GUI.Box(new Rect(12, 12, 460, 330), text, _style);
        }

        private string BuildHudText()
        {
            string text = "Eagle Physical AI Lab v0.2\n";
            if (aircraft != null)
            {
                text += $"Speed: {aircraft.Speed:0.0} m/s | Alt: {aircraft.Altitude:0.0} m | AoA: {aircraft.AngleOfAttack:0.0}\n";
                text += $"Throttle: {aircraft.throttle:0.00} | StallRisk: {aircraft.StallRisk:0.00} | Stalling: {aircraft.IsStalling}\n";
                text += $"Input P/R/Y: {aircraft.pitchInput:0.00} / {aircraft.rollInput:0.00} / {aircraft.yawInput:0.00}\n";
                text += $"Crashed: {aircraft.IsCrashed}\n";
            }

            if (aiPilot != null)
            {
                text += $"AI: {aiPilot.aiEnabled} | Intent: {aiPilot.currentIntent} | Reason: {aiPilot.lastDecisionReason}\n";
            }

            CasRequest request = requestManager != null ? requestManager.activeRequest : null;
            if (request != null)
            {
                text += $"CAS: {request.requestId} | Target: {(request.target != null ? request.target.unitId : "none")} | Active: {request.active}\n";
                if (validator != null && aircraft != null && request.target != null)
                {
                    var result = validator.ValidateStrike(aircraft.transform, request.target);
                    text += $"Validator: {(result.allowed ? "ALLOW" : "DENY")} | {result.reason} | FriendlyRisk: {result.friendlyRisk:0.00}\n";
                }
            }
            else
            {
                text += "CAS: no active request\n";
            }

            if (strikeSystem != null)
            {
                text += $"Strikes OK/Abort/FF: {strikeSystem.successfulStrikes}/{strikeSystem.abortedStrikes}/{strikeSystem.friendlyFireIncidents}\n";
            }

            if (scoreTracker != null)
            {
                text += $"Score: {scoreTracker.score:0.0}\n";
            }

            if (logger != null)
            {
                text += $"Log: {logger.currentFilePath}\n";
            }

            text += "\nControls: WASD/Arrows pitch-roll, Q/E yaw, Shift/Ctrl throttle, Space abstract strike, C debug CAS request";
            return text;
        }
    }
}
