using System.Text;
using UnityEngine;
using MaverickFresh.FlightDynamics;
using MaverickFresh.FlightDynamics.F15;

namespace MaverickFresh.Gameplay
{
    /// <summary>
    /// Developer flight debug readout, behind one key (F1), off by default. Precise terminology lives here and
    /// only here: configuration id, FDM owner and reason, control law, research-domain status, surfaces, qbar,
    /// p/q/r, alpha/beta, the V2 gain schedule, and the F-16 readiness summary. Reads only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MavFlightDebugHud : MonoBehaviour
    {
        public MavFlightSessionDirector director;
        public KeyCode toggleKey = KeyCode.F1;
        public bool show;

        private string cachedCounts = "";
        private float nextCountTime;
        private Vector2 scroll;

        private void Update()
        {
            if (MavFreshInput.GetKeyDown(toggleKey))
                show = !show;
        }

        private void OnGUI()
        {
            if (!show || director == null)
                return;

            MavGameplayUiStyle.Ensure();
            GUI.depth = 5;
            if (Time.unscaledTime >= nextCountTime)
            {
                cachedCounts = MavPlayerSceneCensus.Take().Describe();
                nextCountTime = Time.unscaledTime + 1f;
            }

            Rect r = new Rect(16f, 76f, Mathf.Min(760f, Screen.width - 32f), Mathf.Min(620f, Screen.height - 180f));
            MavGameplayUiStyle.Panel(r, true);
            GUILayout.BeginArea(new Rect(r.x + 10f, r.y + 8f, r.width - 20f, r.height - 16f));
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label(Build(), MavGameplayUiStyle.Mono);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private string Build()
        {
            StringBuilder sb = new StringBuilder(2048);
            sb.AppendLine("FLIGHT SESSION DEBUG (F1)");
            sb.AppendLine("state " + director.State + "   request " + director.request);
            MavPlayableAircraftDefinition d = director.Definition;
            if (d != null)
            {
                sb.AppendLine("aircraft " + d.displayName + " [" + d.aircraft + "] status " + d.status);
                sb.AppendLine("physics config " + d.physicsConfigurationId);
                sb.AppendLine("physics path   " + d.physicsPathDescription);
            }

            if (director.State == MavFlightSessionState.Failed)
                sb.AppendLine("FAILED " + director.failureTitle + ": " + director.failureReason + " | " + director.failureDetail);

            sb.AppendLine("physicsReady " + director.physicsReady + "  playerControl " + director.playerControlActive
                          + "  timeScale " + Time.timeScale);
            sb.AppendLine(cachedCounts);

            MavSpawnedAircraft a = director.Aircraft;
            if (a != null && a.rig != null)
            {
                MavF15PilotControlledRig rig = a.rig;
                sb.AppendLine("rig " + rig.debugStartStatus);
                sb.AppendLine("control mode " + rig.controlMode + "   law " + (rig.ActiveLaw != null ? rig.ActiveLaw.ControlLawName : "none"));
                if (rig.ownership != null)
                    sb.AppendLine("FDM owner " + rig.ownership.owner + ": " + rig.ownership.ownerReason);
                if (a.body != null)
                {
                    MavFlightState s = a.body.debugState;
                    sb.AppendLine("qbar " + s.dynamicPressurePa.ToString("F1") + " Pa   p/q/r " + s.aeroBodyRatesRadSec.x.ToString("F4") + " "
                                  + s.aeroBodyRatesRadSec.y.ToString("F4") + " " + s.aeroBodyRatesRadSec.z.ToString("F4")
                                  + " rad/s   alpha " + s.AlphaDeg.ToString("F2") + "  beta " + s.BetaDeg.ToString("F2"));
                    sb.AppendLine("loads applied " + a.body.debugLoadApplications + "   duplicate refusals " + a.body.debugRejectedDuplicateApplications
                                  + "   armed " + a.body.ArmedForLiveFlight);
                }

                if (rig.lawV2 != null)
                    sb.AppendLine("V2 gain scale " + rig.lawV2.debugLaw.gainScale.ToString("F3") + "   (MAVERICK_TUNED_NON_AUTHORITATIVE)");
                if (a.input != null)
                    sb.AppendLine("command pitch " + a.input.debugCommand.pitch.ToString("F2") + " roll " + a.input.debugCommand.roll.ToString("F2")
                                  + " yaw " + a.input.debugCommand.yaw.ToString("F2") + "   throttle request (UI only) "
                                  + a.input.requestedThrottle01.ToString("F2"));
                sb.AppendLine();
                sb.AppendLine(rig.debugDiagnostics.Format());
            }

            sb.AppendLine();
            sb.AppendLine(MavF16GameplayReadiness.Evaluate().summary);
            return sb.ToString();
        }
    }

    /// <summary>Counts of the things there must be exactly one of in a flight. Global scans - call rarely.</summary>
    public struct MavPlayerSceneCensus
    {
        public int enabledCameras;
        public int enabledAudioListeners;
        public int pilotRigs;
        public int armedBodies;
        public int operationalSourcesWithSignal;
        public int playerInputs;
        public int flightDirectors;
        public int activeLegacyPlayers;

        public static MavPlayerSceneCensus Take()
        {
            MavPlayerSceneCensus c = new MavPlayerSceneCensus();
            foreach (Camera cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (cam.isActiveAndEnabled) c.enabledCameras++;
            foreach (AudioListener l in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (l.isActiveAndEnabled) c.enabledAudioListeners++;
            c.pilotRigs = Object.FindObjectsByType<MavF15PilotControlledRig>(FindObjectsSortMode.None).Length;
            foreach (MavSixDoFBody b in Object.FindObjectsByType<MavSixDoFBody>(FindObjectsSortMode.None))
                if (b.ArmedForLiveFlight) c.armedBodies++;
            foreach (MavPilotCommandSourceBase s in Object.FindObjectsByType<MavPilotCommandSourceBase>(FindObjectsSortMode.None))
                if (s.isActiveAndEnabled && s.IsOperationalCommandSource && s.HasCommandSignal) c.operationalSourcesWithSignal++;
            c.playerInputs = Object.FindObjectsByType<MavPlayerFlightInput>(FindObjectsSortMode.None).Length;
            c.flightDirectors = Object.FindObjectsByType<MavFlightSessionDirector>(FindObjectsSortMode.None).Length;
            foreach (MavMouseFlightJet j in Object.FindObjectsByType<MavMouseFlightJet>(FindObjectsSortMode.None))
                if (j.isActiveAndEnabled) c.activeLegacyPlayers++;
            return c;
        }

        public string Describe()
        {
            return "census: cameras " + enabledCameras + ", audio listeners " + enabledAudioListeners + ", pilot rigs " + pilotRigs
                   + ", armed bodies " + armedBodies + ", operational sources " + operationalSourcesWithSignal + ", player inputs " + playerInputs
                   + ", directors " + flightDirectors + ", active legacy jets " + activeLegacyPlayers;
        }
    }
}
