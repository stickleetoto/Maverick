#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MaverickFresh.Gameplay.EditorTools
{
    /// <summary>
    /// Wires Mav_InGame for the R1 flight session. Idempotent; re-running changes nothing further.
    ///
    ///   - adds Mav_FlightSession with <see cref="MavFlightSessionDirector"/>, the scene's one spawn authority;
    ///   - saves the legacy player stack (CAS_TestRange, Mav_Player, MaverickFresh_MouseFlightRig,
    ///     MaverickFresh_Manager) INACTIVE and hands it to the director, which activates it only for an explicit
    ///     development launch.
    ///
    /// Nothing legacy is deleted or edited: every component and serialized value on those objects is unchanged,
    /// only their active flag. The scene keeps MavFreshBootstrap and MavAircraftProfileApplier, so the official
    /// scene-wiring scan still sees an identity owner for the applier.
    ///
    /// Batch: -executeMethod MaverickFresh.Gameplay.EditorTools.MavGameplaySceneSetup.ApplyBatch -quit
    /// </summary>
    public static class MavGameplaySceneSetup
    {
        public const string InGameScenePath = "Assets/MaverickFresh/Scenes/Mav_InGame.unity";
        public const string DirectorObjectName = "Mav_FlightSession";

        /// <summary>The legacy stack, in the order the saved scene lists it and the director activates it.</summary>
        public static readonly string[] LegacyObjectNames =
        {
            "CAS_TestRange", "Mav_Player", "MaverickFresh_MouseFlightRig", "MaverickFresh_Manager"
        };

        [MenuItem("Maverick/Gameplay/Apply R1 Flight Session To Mav_InGame")]
        public static void ApplyFromMenu()
        {
            string status;
            bool ok = Apply(out status);
            if (ok)
                Debug.Log("[Maverick/Gameplay] " + status);
            else
                Debug.LogError("[Maverick/Gameplay] " + status);
        }

        public static void ApplyBatch()
        {
            string status;
            bool ok = Apply(out status);
            Debug.Log("MAV_GAMEPLAY_SCENE_SETUP " + (ok ? "OK " : "FAILED ") + status);
        }

        public static bool Apply(out string status)
        {
            Scene scene = EditorSceneManager.OpenScene(InGameScenePath, OpenSceneMode.Single);
            GameObject[] roots = scene.GetRootGameObjects();

            List<GameObject> legacy = new List<GameObject>();
            for (int i = 0; i < LegacyObjectNames.Length; i++)
            {
                GameObject found = null;
                for (int r = 0; r < roots.Length; r++)
                {
                    if (roots[r].name == LegacyObjectNames[i])
                    {
                        if (found != null)
                        {
                            status = "two root objects are named " + LegacyObjectNames[i] + "; refusing to guess";
                            return false;
                        }

                        found = roots[r];
                    }
                }

                if (found == null)
                {
                    status = "root object " + LegacyObjectNames[i] + " not found in " + InGameScenePath;
                    return false;
                }

                legacy.Add(found);
            }

            GameObject directorObject = null;
            for (int r = 0; r < roots.Length; r++)
            {
                if (roots[r].name == DirectorObjectName)
                    directorObject = roots[r];
            }

            if (directorObject == null)
                directorObject = new GameObject(DirectorObjectName);
            directorObject.transform.SetSiblingIndex(0);

            MavFlightSessionDirector director = directorObject.GetComponent<MavFlightSessionDirector>();
            if (director == null)
                director = directorObject.AddComponent<MavFlightSessionDirector>();
            director.legacyObjects = legacy.ToArray();

            for (int i = 0; i < legacy.Count; i++)
                legacy[i].SetActive(false);

            EditorUtility.SetDirty(director);
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            status = saved
                ? "Mav_InGame wired: " + DirectorObjectName + " + " + legacy.Count + " legacy objects saved inactive"
                : "saving " + InGameScenePath + " failed";
            return saved;
        }
    }
}
#endif
