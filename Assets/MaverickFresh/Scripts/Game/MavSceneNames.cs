using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MaverickFresh
{
    public static class MavSceneNames
    {
        public const string MainLobby = "Mav_MainLobby";
        public const string Hangar = "Mav_Hangar";
        public const string InGame = "Mav_InGame";
    }

    public enum MavGameMode
    {
        FreeFlight = 0,
        TestRange = 1,
        Dogfight = 2,
        GroundAttack = 3,
        CarrierTest = 4
    }

    /// <summary>
    /// The game-level session: which aircraft and mode the player selected, and the flight they asked to launch.
    /// Never simulation state - aero, surfaces, ownership and engine state belong to the aircraft.
    /// </summary>
    public static class MavGameSession
    {
        /// <summary>The aircraft used only when nothing has been selected at all (R1: the F-15).</summary>
        public const MavAircraftKind DefaultAircraft = MavAircraftKind.F15E;
        public static MavAircraftKind SelectedAircraft = DefaultAircraft;
        public static MavGameMode SelectedMode = MavGameMode.FreeFlight;
        public static bool HasSelection;
        public static string LastSceneError;

        /// <summary>The flight the player asked for; read by the flight session in Mav_InGame. Not persisted.</summary>
        public static MaverickFresh.Gameplay.MavFlightLaunchRequest CurrentLaunch;
        public static bool HasLaunchRequest;

        public static void SetLaunchRequest(MaverickFresh.Gameplay.MavFlightLaunchRequest request)
        {
            CurrentLaunch = request;
            HasLaunchRequest = true;
        }

        public static void ClearLaunchRequest()
        {
            CurrentLaunch = default(MaverickFresh.Gameplay.MavFlightLaunchRequest);
            HasLaunchRequest = false;
        }

        public static void SelectAircraft(MavAircraftKind aircraft)
        {
            SelectedAircraft = aircraft;
            HasSelection = true;
            PlayerPrefs.SetInt("MavSelectedAircraft", (int)aircraft);
            PlayerPrefs.SetInt("MavHasSelection", 1);
            PlayerPrefs.Save();
        }

        public static void SelectMode(MavGameMode mode)
        {
            SelectedMode = mode;
            PlayerPrefs.SetInt("MavSelectedMode", (int)mode);
            PlayerPrefs.Save();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RestoreSession()
        {
            if (PlayerPrefs.GetInt("MavHasSelection", 0) == 1)
            {
                int aircraftValue = PlayerPrefs.GetInt("MavSelectedAircraft", (int)DefaultAircraft);
                int modeValue = PlayerPrefs.GetInt("MavSelectedMode", (int)MavGameMode.FreeFlight);
                SelectedAircraft = System.Enum.IsDefined(typeof(MavAircraftKind), aircraftValue)
                    ? (MavAircraftKind)aircraftValue
                    : DefaultAircraft;
                SelectedMode = System.Enum.IsDefined(typeof(MavGameMode), modeValue)
                    ? (MavGameMode)modeValue
                    : MavGameMode.FreeFlight;
                HasSelection = true;
            }
        }

        public static void Launch(MavAircraftKind aircraft, MavGameMode mode)
        {
            SelectAircraft(aircraft);
            SelectMode(mode);
            MavSceneLoader.LoadSceneSafe(MavSceneNames.InGame);
        }
    }

    public static class MavSceneLoader
    {
        public static bool LoadSceneSafe(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
                return false;

            try
            {
                SceneManager.LoadScene(sceneName);
                MavGameSession.LastSceneError = string.Empty;
                return true;
            }
            catch (Exception e)
            {
                MavGameSession.LastSceneError = "Scene load failed: " + sceneName + " / " + e.Message;
                Debug.LogError(MavGameSession.LastSceneError);
                return false;
            }
        }
    }
}
