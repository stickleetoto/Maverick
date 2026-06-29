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

    public static class MavGameSession
    {
        public const MavAircraftKind DefaultAircraft = MavAircraftKind.F22A;
        public static MavAircraftKind SelectedAircraft = DefaultAircraft;
        public static MavGameMode SelectedMode = MavGameMode.FreeFlight;
        public static bool HasSelection;
        public static string LastSceneError;

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
