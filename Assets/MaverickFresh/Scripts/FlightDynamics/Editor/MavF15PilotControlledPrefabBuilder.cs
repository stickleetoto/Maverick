using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using MaverickFresh.FlightDynamics.F15;
using Object = UnityEngine.Object;

namespace MaverickFresh.FlightDynamics.EditorTools
{
    /// <summary>
    /// Builds the pilot-controlled F-15 research aircraft prefabs, and places one in a scene on request:
    ///   V1 - <see cref="PrefabPath"/>, Direct V1 law only (the regression baseline, unchanged);
    ///   V2 - <see cref="PrefabPathV2"/>, the same rig starting in Assisted V2, carrying both laws so F2 switches
    ///        V1 / V2 in flight (stick centred).
    ///
    /// The prefab is <see cref="MavF15PilotControlledRig"/> with its whole stack serialized (keyboard pilot
    /// source), its HUD, a primitive-shape visual (no collider, no new mesh or material asset) and a chase
    /// camera. It starts itself from the validated trim on its first physics step. It is NOT the frozen research
    /// validation rig, and it touches no existing scene, prefab, model, material or F-16 content.
    ///
    /// To fly it: open an empty scene, Maverick / F-15 / Place Pilot-Controlled Research F-15 In Scene (V1) or
    /// ... V2 (Assisted) In Scene, press Play.
    /// Headless (pass -quit): -executeMethod MaverickFresh.FlightDynamics.EditorTools.MavF15PilotControlledPrefabBuilder.BuildBatch
    /// (V1) or .BuildBatchV2 (V2).
    /// </summary>
    public static class MavF15PilotControlledPrefabBuilder
    {
        public const string PrefabFolder = "Assets/MaverickFresh/Prefabs/F15";
        public const string PrefabPath = PrefabFolder + "/F15_PilotControlledResearch_V1.prefab";
        public const string RootName = "F15_PilotControlledResearch_V1";
        public const string PrefabPathV2 = PrefabFolder + "/F15_PilotControlledResearch_V2.prefab";
        public const string RootNameV2 = "F15_PilotControlledResearch_V2";

        [MenuItem("Maverick/F-15/Build Pilot-Controlled Research F-15 Prefab")]
        public static void BuildFromMenu()
        {
            string status;
            bool ok = Build(out status);
            Debug.Log("[Maverick/F-15 pilot-controlled] " + status);
            if (ok)
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        public static void BuildBatch()
        {
            string status;
            if (!Build(out status))
                throw new InvalidOperationException(status);
            Debug.Log("[Maverick/F-15 pilot-controlled] " + status);
        }

        [MenuItem("Maverick/F-15/Build Pilot-Controlled Research F-15 V2 (Assisted) Prefab")]
        public static void BuildV2FromMenu()
        {
            string status;
            bool ok = BuildV2(out status);
            Debug.Log("[Maverick/F-15 pilot-controlled] " + status);
            if (ok)
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPathV2);
        }

        public static void BuildBatchV2()
        {
            string status;
            if (!BuildV2(out status))
                throw new InvalidOperationException(status);
            Debug.Log("[Maverick/F-15 pilot-controlled] " + status);
        }

        [MenuItem("Maverick/F-15/Place Pilot-Controlled Research F-15 In Scene")]
        public static void PlaceInScene()
        {
            Place(PrefabPath, false);
        }

        [MenuItem("Maverick/F-15/Place Pilot-Controlled Research F-15 V2 (Assisted) In Scene")]
        public static void PlaceV2InScene()
        {
            Place(PrefabPathV2, true);
        }

        private static void Place(string path, bool v2)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                string status;
                if (!(v2 ? BuildV2(out status) : Build(out status)))
                {
                    Debug.LogError("[Maverick/F-15 pilot-controlled] " + status);
                    return;
                }

                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, "Place pilot-controlled research F-15");
            Selection.activeGameObject = instance;
            EditorSceneManager.MarkSceneDirty(instance.scene);
        }

        /// <summary>The V1 prefab: Direct V1 law only.</summary>
        public static bool Build(out string status)
        {
            return BuildPrefab(PrefabPath, RootName, MavF15PilotControlMode.DirectV1, out status);
        }

        /// <summary>The V2 prefab: starts in Assisted V2 and carries the V1 law disabled, for the F2 switch.</summary>
        public static bool BuildV2(out string status)
        {
            return BuildPrefab(PrefabPathV2, RootNameV2, MavF15PilotControlMode.AssistedV2, out status);
        }

        private static bool BuildPrefab(string prefabPath, string rootName, MavF15PilotControlMode mode, out string status)
        {
            EnsureFolder("Assets/MaverickFresh", "Prefabs");
            EnsureFolder("Assets/MaverickFresh/Prefabs", "F15");

            GameObject root = new GameObject(rootName);
            try
            {
                MavF15PilotControlledRig rig = root.AddComponent<MavF15PilotControlledRig>();
                rig.commandSourceKind = MavF15PilotCommandSourceKind.Keyboard;
                rig.startOnFirstPhysicsStep = true;
                rig.releasePilotControlledSafetyHold = true;
                rig.controlMode = mode;

                // Edit mode calls no Awake: build and wire the stack now so the prefab carries it serialized.
                rig.EnsureStack();

                MavF15PilotControlledHud hud = root.AddComponent<MavF15PilotControlledHud>();
                hud.rig = rig;

                BuildVisual(root.transform);
                BuildCamera(root.transform);

                bool saved;
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out saved);
                status = saved
                    ? "built " + prefabPath + " (" + MavF15PilotControlledIdentity.ConfigurationId + ", " + mode + ")"
                    : "could not save " + prefabPath;
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>Unity primitives, colliders removed: a shape to see, not a model, and nothing to collide with.</summary>
        private static void BuildVisual(Transform parent)
        {
            GameObject visual = new GameObject("Visual (primitive shapes, no collider)");
            visual.transform.SetParent(parent, false);
            Part(visual.transform, "Fuselage", new Vector3(0f, 0f, 0f), new Vector3(2f, 2f, 19f));
            Part(visual.transform, "Wing", new Vector3(0f, 0f, -1f), new Vector3(13f, 0.25f, 4.5f));
            Part(visual.transform, "Stabilator", new Vector3(0f, 0f, -8f), new Vector3(8.6f, 0.2f, 2.8f));
            Part(visual.transform, "Fin L", new Vector3(-1.5f, 2f, -8f), new Vector3(0.25f, 3.2f, 3f));
            Part(visual.transform, "Fin R", new Vector3(1.5f, 2f, -8f), new Vector3(0.25f, 3.2f, 3f));
        }

        private static void Part(Transform parent, string name, Vector3 position, Vector3 scale)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            Collider c = part.GetComponent<Collider>();
            if (c != null)
                Object.DestroyImmediate(c);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
        }

        private static void BuildCamera(Transform parent)
        {
            GameObject cam = new GameObject("Chase Camera");
            cam.transform.SetParent(parent, false);
            cam.transform.localPosition = new Vector3(0f, 5f, -30f);
            cam.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
            Camera camera = cam.AddComponent<Camera>();
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 30000f;
            cam.AddComponent<AudioListener>();
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + child))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
