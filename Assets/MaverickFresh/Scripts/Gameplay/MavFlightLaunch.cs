using System;
using UnityEngine;

namespace MaverickFresh.Gameplay
{
    public enum MavFlightLaunchKind
    {
        /// <summary>A normal launch from the player flow. Only a launchable player aircraft.</summary>
        Player = 0,

        /// <summary>An explicit development launch of a legacy aircraft. Never reached by the FLY button.</summary>
        DevelopmentLegacy = 1
    }

    /// <summary>
    /// What the player asked the game to fly. Game/session data only - it carries no simulation state, and the
    /// flight session re-derives everything physical from the aircraft's own definition.
    /// </summary>
    [Serializable]
    public struct MavFlightLaunchRequest
    {
        public MavAircraftKind aircraft;
        public MavGameMode mode;
        public MavFlightLaunchKind kind;

        /// <summary>1 on the first launch, +1 for every restart of the same flight.</summary>
        public int attempt;

        public override string ToString()
        {
            return aircraft + " / " + mode + " / " + kind + " / attempt " + attempt;
        }
    }

    /// <summary>
    /// Turns a selection into a flight, and a flight back into the hangar. The only code that loads the flight
    /// scene for the player flow.
    ///
    /// The Prepare* methods do the deciding and write only session state, so they can be validated without
    /// loading a scene. A refused launch records the player's selection and nothing else: no request is made,
    /// and no other aircraft is ever put in its place.
    /// </summary>
    public static class MavFlightLauncher
    {
        public static bool TryPrepareLaunch(MavAircraftKind aircraft, out MavFlightLaunchRequest request, out string reason)
        {
            MavGameSession.SelectAircraft(aircraft);
            MavGameSession.SelectMode(MavGameMode.FreeFlight);

            MavPlayableAircraftDefinition definition;
            if (!MavPlayableAircraftRegistry.CanLaunchAsPlayer(aircraft, out definition, out reason))
            {
                // Refused: the selection stays what the player chose, and no flight is left pending.
                MavGameSession.ClearLaunchRequest();
                request = default(MavFlightLaunchRequest);
                return false;
            }

            request = new MavFlightLaunchRequest
            {
                aircraft = aircraft,
                mode = MavGameMode.FreeFlight,
                kind = MavFlightLaunchKind.Player,
                attempt = 1
            };
            MavGameSession.SetLaunchRequest(request);
            return true;
        }

        public static bool TryLaunch(MavAircraftKind aircraft, out string reason)
        {
            MavFlightLaunchRequest request;
            if (!TryPrepareLaunch(aircraft, out request, out reason))
            {
                MavGameSession.LastSceneError = reason;
                return false;
            }

            return LoadScene(MavSceneNames.InGame, out reason);
        }

        /// <summary>Development only: a legacy aircraft on the legacy flight model, labelled as such.</summary>
        public static bool TryPrepareDevelopmentLaunch(MavAircraftKind aircraft, out MavFlightLaunchRequest request, out string reason)
        {
            MavPlayableAircraftDefinition definition;
            if (!MavPlayableAircraftRegistry.TryGetDevelopment(aircraft, out definition))
            {
                request = default(MavFlightLaunchRequest);
                reason = aircraft + " has no development launch.";
                return false;
            }

            MavGameSession.SelectAircraft(aircraft);
            MavGameSession.SelectMode(MavGameMode.FreeFlight);
            request = new MavFlightLaunchRequest
            {
                aircraft = aircraft,
                mode = MavGameMode.FreeFlight,
                kind = MavFlightLaunchKind.DevelopmentLegacy,
                attempt = 1
            };
            MavGameSession.SetLaunchRequest(request);
            reason = definition.displayName + " (development launch)";
            return true;
        }

        public static bool TryDevelopmentLaunch(MavAircraftKind aircraft, out string reason)
        {
            MavFlightLaunchRequest request;
            if (!TryPrepareDevelopmentLaunch(aircraft, out request, out reason))
                return false;
            return LoadScene(MavSceneNames.InGame, out reason);
        }

        /// <summary>The same aircraft, mode and launch kind again, from its known start.</summary>
        public static MavFlightLaunchRequest PrepareRestart(MavFlightLaunchRequest current)
        {
            MavFlightLaunchRequest next = current;
            next.attempt = Mathf.Max(1, current.attempt) + 1;
            MavGameSession.SetLaunchRequest(next);
            return next;
        }

        public static bool Restart(MavFlightLaunchRequest current, out string reason)
        {
            PrepareRestart(current);
            return LoadScene(MavSceneNames.InGame, out reason);
        }

        /// <summary>Ends the flight. The selection is kept, so the hangar shows the aircraft just flown.</summary>
        public static void PrepareReturnToHangar()
        {
            MavGameSession.ClearLaunchRequest();
        }

        public static bool ReturnToHangar(out string reason)
        {
            PrepareReturnToHangar();
            return LoadScene(MavSceneNames.Hangar, out reason);
        }

        public static bool ReturnToMainLobby(out string reason)
        {
            MavGameSession.ClearLaunchRequest();
            return LoadScene(MavSceneNames.MainLobby, out reason);
        }

        private static bool LoadScene(string scene, out string reason)
        {
            // A paused flight must never carry a stopped clock into the next scene.
            Time.timeScale = 1f;
            bool ok = MavSceneLoader.LoadSceneSafe(scene);
            reason = ok ? "loading " + scene : MavGameSession.LastSceneError;
            return ok;
        }
    }
}
