using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using MaverickFresh.FlightDynamics.F15;
using Object = UnityEngine.Object;

namespace MaverickFresh.FlightDynamics.EditorTools
{
    /// <summary>
    /// Places an EXPERIMENTAL human playtest rig for the realistic mouse instructor v2 in the OPEN scene: the pilot-controlled
    /// F-15 research rig (Assisted V2 law, unchanged) with the opt-in mouse-aim source, its existing HUD, primitive shapes (no
    /// collider, no new mesh or material asset) and a separate mouse-aim chase camera.
    ///
    /// It writes NO asset: no prefab is built or modified, no scene is saved (save it yourself if you want to keep it; the
    /// intended use is an empty, unsaved scene). Nothing existing selects the mouse instructor; this menu is the opt-in.
    /// </summary>
    public static class MavMouseAimInstructorV2Setup
    {
        public const string RootName = "F15_MouseInstructorV2_TestRig (EXPERIMENTAL)";
        public const string CameraName = "Mouse Instructor V2 Camera";

        [MenuItem("Maverick/F-15/Mouse Instructor V2 (EXPERIMENTAL)/Place Playtest Rig In Open Scene")]
        public static void PlaceInScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Maverick/Mouse Instructor V2] leave Play Mode before placing the playtest rig");
                return;
            }

            GameObject root = new GameObject(RootName);
            MavF15PilotControlledRig rig = root.AddComponent<MavF15PilotControlledRig>();
            rig.commandSourceKind = MavF15PilotCommandSourceKind.MouseAim;
            rig.controlMode = MavF15PilotControlMode.AssistedV2;
            rig.startOnFirstPhysicsStep = true;
            rig.releasePilotControlledSafetyHold = true;

            // Edit mode calls no Awake: build and wire the stack now (idempotent; Awake repeats it in Play Mode).
            rig.EnsureStack();

            MavF15PilotControlledHud hud = root.AddComponent<MavF15PilotControlledHud>();
            hud.rig = rig;
            BuildVisual(root.transform);

            GameObject cam = new GameObject(CameraName);
            cam.transform.SetPositionAndRotation(new Vector3(0f, 5f, -34f), Quaternion.identity);
            Camera camera = cam.AddComponent<Camera>();
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 30000f;
            camera.tag = "MainCamera";
            cam.AddComponent<AudioListener>();
            MavMouseAimCameraRig view = cam.AddComponent<MavMouseAimCameraRig>();
            view.source = (MavMouseAimPilotCommandSource)rig.commandSource;
            view.viewCamera = camera;

            Undo.RegisterCreatedObjectUndo(root, "Place mouse instructor V2 playtest rig");
            Undo.RegisterCreatedObjectUndo(cam, "Place mouse instructor V2 camera");
            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(root.scene);

            int otherListeners = 0;
            foreach (AudioListener l in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            {
                if (l.gameObject != cam)
                    otherListeners++;
            }

            Debug.Log("[Maverick/Mouse Instructor V2] placed " + RootName + " (" + MavF15PilotControlledIdentity.ConfigurationId
                      + ", Assisted V2, mouse aim) and " + CameraName + ". Press Play. Mouse = aim, hold C = free look, X = hands off, Esc = release cursor."
                      + (otherListeners > 0 ? " NOTE: the scene has " + otherListeners + " other AudioListener(s)/camera(s); use an empty scene." : ""));
        }

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

        private static void Part(Transform parent, string name, Vector3 localPosition, Vector3 scale)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            Collider c = part.GetComponent<Collider>();
            if (c != null)
                Object.DestroyImmediate(c);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = scale;
        }
    }
}
