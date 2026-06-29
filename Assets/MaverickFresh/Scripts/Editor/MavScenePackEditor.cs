#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MaverickFresh.EditorTools
{
    /// <summary>
    /// v0.22.4 safe editor tools.
    /// IMPORTANT: the old scene generator used to overwrite Mav_MainLobby/Mav_Hangar/Mav_InGame.
    /// It is intentionally disabled here. Use the safe current-scene Mav_Player builder instead.
    /// </summary>
    public static class MavScenePackEditor
    {
        private const string PlayerName = "Mav_Player";
        private const string VisualRootName = "AircraftVisuals";
        private const string RigName = "MaverickFresh_MouseFlightRig";

        [MenuItem("Maverick/Create Three-Scene Game Pack")]
        public static void CreateThreeSceneGamePack()
        {
            EditorUtility.DisplayDialog(
                "Maverick Scene Generator Disabled",
                "This legacy tool is disabled because it can overwrite your hand-built scenes.\n\n" +
                "Use this instead:\nMaverick/Safe Tools/Create or Repair Playable Mav_Player\n\n" +
                "If you really need fresh scenes, duplicate/back up your project first.",
                "OK");
        }

        [MenuItem("Maverick/Safe Tools/Create or Repair Playable Mav_Player")]
        public static void CreateOrRepairPlayablePlayer()
        {
            bool claimVisuals = EditorUtility.DisplayDialog(
                "Create/Repair Mav_Player",
                "This will create or repair Mav_Player in the CURRENT open scene only.\n\n" +
                "It will NOT create scenes, overwrite scenes, save scenes, or touch prefabs.\n\n" +
                "Do you want the tool to claim scene aircraft objects named F15EX, F16, F18, F22, F35 and move them under Mav_Player/AircraftVisuals?",
                "Yes, claim visuals",
                "No, make player only");

            BuildOrRepairPlayer(claimVisuals);
        }

        [MenuItem("Maverick/Safe Tools/Create or Repair Mav_Player Only")]
        public static void CreateOrRepairPlayerOnly()
        {
            BuildOrRepairPlayer(false);
        }

        [MenuItem("Maverick/Safe Tools/Claim Aircraft Visuals Into Existing Mav_Player")]
        public static void ClaimAircraftVisualsOnly()
        {
            GameObject player = GameObject.Find(PlayerName);
            if (player == null)
            {
                EditorUtility.DisplayDialog("Mav_Player missing", "Create Mav_Player first.", "OK");
                return;
            }

            Transform visualRoot = EnsureChild(player.transform, VisualRootName);
            Dictionary<string, GameObject> visuals = ClaimAircraftVisuals(visualRoot, true);
            ConfigureVisualSwitcher(player, visualRoot, visuals);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = player;
        }

        private static void BuildOrRepairPlayer(bool claimVisuals)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                EditorUtility.DisplayDialog("No active scene", "Open your Mav_InGame scene first.", "OK");
                return;
            }

            GameObject player = GameObject.Find(PlayerName);
            if (player == null)
            {
                player = new GameObject(PlayerName);
                Undo.RegisterCreatedObjectUndo(player, "Create Mav_Player");
                player.transform.position = new Vector3(0f, 500f, 0f);
                player.transform.rotation = Quaternion.identity;
            }
            else
            {
                Undo.RecordObject(player.transform, "Repair Mav_Player");
            }

            Rigidbody rb = player.GetComponent<Rigidbody>();
            if (rb == null)
                rb = Undo.AddComponent<Rigidbody>(player);
            ConfigureRigidbody(rb);

            Camera cam = EnsureMainCamera(player.transform);
            Component rig = EnsureMouseFlightRig(player, cam);

            Component jet = EnsureComponent(player, "MaverickFresh.MavMouseFlightJet");
            Component instructor = EnsureComponent(player, "MaverickFresh.MavInstructorController");
            Component wt = EnsureComponent(player, "MaverickFresh.MavWTFeelPolishController");
            Component profileApplier = EnsureComponent(player, "MaverickFresh.MavAircraftProfileApplier");
            Component visualSwitcher = EnsureComponent(player, "MaverickFresh.MavAircraftVisualSwitcher");

            // Optional modern systems. These are added only if the class exists in this project revision.
            EnsureComponent(player, "MaverickFresh.MavAeroBody");
            EnsureComponent(player, "MaverickFresh.MavAtmosphericEngine");
            EnsureComponent(player, "MaverickFresh.MavCombatFlapSystem");
            EnsureComponent(player, "MaverickFresh.MavThrustVectorControl");
            EnsureComponent(player, "MaverickFresh.MavRadarSignature");
            EnsureComponent(player, "MaverickFresh.MavF22SensorSuite");
            EnsureComponent(player, "MaverickFresh.MavBVRWeaponSystem");
            EnsureComponent(player, "MaverickFresh.MavEngagementDirector");
            EnsureComponent(player, "MaverickFresh.MavAirToAirLeadSight");
            EnsureComponent(player, "MaverickFresh.MavGunTracerImpactVfx");
            EnsureComponent(player, "MaverickFresh.MavFreshHud");
            EnsureComponent(player, "MaverickFresh.MavWTQuickHelpOverlay");

            WireFlightReferences(rig, jet, instructor, wt, profileApplier);

            Transform visualRoot = EnsureChild(player.transform, VisualRootName);
            Dictionary<string, GameObject> visuals = claimVisuals
                ? ClaimAircraftVisuals(visualRoot, false)
                : ResolveExistingVisuals(visualRoot);
            ConfigureVisualSwitcher(player, visualRoot, visuals);

            ApplyF22DefaultProfile(profileApplier, visualSwitcher);

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = player;

            EditorUtility.DisplayDialog(
                "Mav_Player Ready",
                "Playable Mav_Player was created/repaired in the current scene.\n\n" +
                "No scenes were saved or overwritten.\n\n" +
                "Expected structure:\nMav_Player / AircraftVisuals / F15EX, F16, F18, F22, F35\n\n" +
                "Default active aircraft: F-22A.",
                "OK");
        }

        private static void ConfigureRigidbody(Rigidbody rb)
        {
            Undo.RecordObject(rb, "Configure Mav_Player Rigidbody");
            rb.mass = 14500f;
            rb.useGravity = false;
            rb.linearDamping = 0.006f;
            rb.angularDamping = 1.2f;
            rb.maxAngularVelocity = Mathf.Max(rb.maxAngularVelocity, 8f);
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            EditorUtility.SetDirty(rb);
        }

        private static Camera EnsureMainCamera(Transform player)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject camGo = new GameObject("Main Camera");
                Undo.RegisterCreatedObjectUndo(camGo, "Create Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }

            Undo.RecordObject(cam.transform, "Configure Main Camera");
            if (cam.transform.position == Vector3.zero)
                cam.transform.position = player.position + new Vector3(0f, 5.5f, -18f);
            cam.transform.rotation = Quaternion.LookRotation((player.position + Vector3.forward * 30f) - cam.transform.position, Vector3.up);
            cam.farClipPlane = Mathf.Max(cam.farClipPlane, 50000f);
            return cam;
        }

        private static Component EnsureMouseFlightRig(GameObject player, Camera cam)
        {
            GameObject rigGo = GameObject.Find(RigName);
            if (rigGo == null)
            {
                rigGo = new GameObject(RigName);
                Undo.RegisterCreatedObjectUndo(rigGo, "Create Mouse Flight Rig");
            }

            Component rig = EnsureComponent(rigGo, "MaverickFresh.MavMouseFlightRig");
            SetField(rig, "aircraft", player.transform);
            SetField(rig, "playerCamera", cam);
            SetField(rig, "mouseSensitivity", 3.0f);
            SetField(rig, "aimDistance", 850f);
            SetField(rig, "showCursorInCursorMode", true);
            return rig;
        }

        private static void WireFlightReferences(Component rig, Component jet, Component instructor, Component wt, Component profileApplier)
        {
            SetField(jet, "controller", rig);
            SetField(jet, "instructor", instructor);
            SetField(jet, "gravityOff", true);
            SetField(jet, "thrust", 260f);
            SetField(jet, "targetCruiseSpeed", 330f);
            SetField(jet, "maxCombatSpeed", 560f);

            SetField(instructor, "rig", rig);
            SetField(instructor, "jet", jet);

            SetField(wt, "rig", rig);
            SetField(wt, "jet", jet);
            SetField(wt, "instructor", instructor);
            SetField(wt, "applyOnStart", false);

            SetField(profileApplier, "applyOnStart", false);
            SetField(profileApplier, "renameObject", false);
            SetField(profileApplier, "allowGlobalRigLookup", true);
        }

        private static Transform EnsureChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null)
                return child;

            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            Undo.SetTransformParent(go.transform, parent, "Parent " + name);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        private static Dictionary<string, GameObject> ClaimAircraftVisuals(Transform visualRoot, bool showReport)
        {
            Dictionary<string, GameObject> visuals = ResolveExistingVisuals(visualRoot);
            string[] keys = { "f15", "f16", "f18", "f22", "f35" };

            foreach (string key in keys)
            {
                if (visuals.ContainsKey(key) && visuals[key] != null)
                    continue;

                GameObject found = FindSceneAircraftVisual(key, visualRoot.root.gameObject);
                if (found == null)
                    continue;

                Undo.SetTransformParent(found.transform, visualRoot, "Claim " + found.name);
                Undo.RecordObject(found.transform, "Reset claimed visual transform");
                found.transform.localPosition = Vector3.zero;
                found.transform.localRotation = Quaternion.identity;
                found.transform.localScale = found.transform.localScale == Vector3.zero ? Vector3.one : found.transform.localScale;
                DisablePhysicsOnVisual(found);
                visuals[key] = found;
            }

            foreach (string key in keys)
            {
                if (!visuals.ContainsKey(key) || visuals[key] == null)
                {
                    GameObject placeholder = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    placeholder.name = VisualNameForKey(key) + "_Placeholder";
                    Undo.RegisterCreatedObjectUndo(placeholder, "Create " + placeholder.name);
                    Undo.SetTransformParent(placeholder.transform, visualRoot, "Parent placeholder");
                    placeholder.transform.localPosition = Vector3.zero;
                    placeholder.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    placeholder.transform.localScale = PlaceholderScaleForKey(key);
                    DisablePhysicsOnVisual(placeholder);
                    visuals[key] = placeholder;
                }
            }

            SetOnlyF22Active(visuals);

            if (showReport)
                EditorUtility.DisplayDialog("Visuals claimed", "Aircraft visuals were claimed under Mav_Player/AircraftVisuals.", "OK");

            return visuals;
        }

        private static Dictionary<string, GameObject> ResolveExistingVisuals(Transform visualRoot)
        {
            Dictionary<string, GameObject> visuals = new Dictionary<string, GameObject>();
            for (int i = 0; i < visualRoot.childCount; i++)
            {
                Transform child = visualRoot.GetChild(i);
                string key = KeyFromName(child.name);
                if (!string.IsNullOrEmpty(key) && !visuals.ContainsKey(key))
                    visuals[key] = child.gameObject;
            }
            return visuals;
        }

        private static void ConfigureVisualSwitcher(GameObject player, Transform visualRoot, Dictionary<string, GameObject> visuals)
        {
            Component sw = EnsureComponent(player, "MaverickFresh.MavAircraftVisualSwitcher");
            if (sw == null)
                return;

            SetField(sw, "visualRoot", visualRoot);
            SetField(sw, "autoFindSceneVisuals", false);
            SetField(sw, "claimLooseSceneVisuals", false);
            SetField(sw, "createPlaceholderIfMissing", false);
            SetField(sw, "preclaimAllResolvedVisuals", true);
            SetField(sw, "f15Visual", GetVisual(visuals, "f15"));
            SetField(sw, "f16Visual", GetVisual(visuals, "f16"));
            SetField(sw, "fa18Visual", GetVisual(visuals, "f18"));
            SetField(sw, "f22Visual", GetVisual(visuals, "f22"));
            SetField(sw, "f35Visual", GetVisual(visuals, "f35"));
        }

        private static void ApplyF22DefaultProfile(Component profileApplier, Component visualSwitcher)
        {
            Type kindType = FindType("MaverickFresh.MavAircraftKind");
            if (kindType == null)
                return;

            object f22 = Enum.Parse(kindType, "F22A");
            SetField(profileApplier, "aircraft", f22);
            SetField(visualSwitcher, "activeAircraft", f22);
            Invoke(profileApplier, "ApplyAircraft", f22);
        }

        private static GameObject GetVisual(Dictionary<string, GameObject> visuals, string key)
        {
            GameObject value;
            return visuals != null && visuals.TryGetValue(key, out value) ? value : null;
        }

        private static void SetOnlyF22Active(Dictionary<string, GameObject> visuals)
        {
            foreach (KeyValuePair<string, GameObject> pair in visuals)
            {
                if (pair.Value != null)
                    pair.Value.SetActive(pair.Key == "f22");
            }
        }

        private static GameObject FindSceneAircraftVisual(string key, GameObject excludeRoot)
        {
            Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
            GameObject best = null;
            int bestScore = -1;

            foreach (Transform tr in all)
            {
                if (tr == null || tr.gameObject == null)
                    continue;
                if (!tr.gameObject.scene.IsValid())
                    continue;
                if (excludeRoot != null && tr.IsChildOf(excludeRoot.transform))
                    continue;
                if (tr.hideFlags != HideFlags.None)
                    continue;

                string candidateKey = KeyFromName(tr.name);
                if (candidateKey != key)
                    continue;

                int score = tr.parent == null ? 100 : 50;
                if (tr.name.Equals(VisualNameForKey(key), StringComparison.OrdinalIgnoreCase))
                    score += 25;
                if (score > bestScore)
                {
                    best = tr.gameObject;
                    bestScore = score;
                }
            }

            return best;
        }

        private static string KeyFromName(string name)
        {
            string n = Normalize(name);
            if (n.Contains("F15EX") || n.Contains("F15E") || n == "F15" || n.Contains("F15")) return "f15";
            if (n.Contains("F16C") || n.Contains("F16")) return "f16";
            if (n.Contains("FA18") || n.Contains("F18") || n.Contains("F/A18")) return "f18";
            if (n.Contains("F22A") || n.Contains("F22")) return "f22";
            if (n.Contains("F35A") || n.Contains("F35")) return "f35";
            return null;
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            char[] buffer = new char[value.Length];
            int count = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char c = char.ToUpperInvariant(value[i]);
                if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
                    buffer[count++] = c;
            }
            return new string(buffer, 0, count);
        }

        private static string VisualNameForKey(string key)
        {
            switch (key)
            {
                case "f15": return "F15EX";
                case "f16": return "F16";
                case "f18": return "F18";
                case "f22": return "F22";
                case "f35": return "F35";
                default: return "Aircraft";
            }
        }

        private static Vector3 PlaceholderScaleForKey(string key)
        {
            switch (key)
            {
                case "f16": return new Vector3(2.2f, 6.0f, 1.3f);
                case "f18": return new Vector3(2.5f, 6.4f, 1.4f);
                case "f22": return new Vector3(2.7f, 6.2f, 1.35f);
                case "f35": return new Vector3(2.4f, 5.8f, 1.4f);
                default: return new Vector3(2.7f, 6.8f, 1.45f);
            }
        }

        private static void DisablePhysicsOnVisual(GameObject root)
        {
            if (root == null)
                return;

            Rigidbody[] bodies = root.GetComponentsInChildren<Rigidbody>(true);
            foreach (Rigidbody body in bodies)
                UnityEngine.Object.DestroyImmediate(body);

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            foreach (Collider collider in colliders)
                collider.enabled = false;
        }

        private static Component EnsureComponent(GameObject go, string fullTypeName)
        {
            Type type = FindType(fullTypeName);
            if (type == null || !typeof(Component).IsAssignableFrom(type))
            {
                Debug.LogWarning("Maverick safe tool: component type not found: " + fullTypeName);
                return null;
            }

            Component existing = go.GetComponent(type);
            if (existing != null)
                return existing;

            return Undo.AddComponent(go, type);
        }

        private static Type FindType(string fullTypeName)
        {
            Type type = Type.GetType(fullTypeName);
            if (type != null)
                return type;

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly assembly in assemblies)
            {
                try
                {
                    type = assembly.GetType(fullTypeName);
                    if (type != null)
                        return type;
                }
                catch { }
            }
            return null;
        }

        private static void SetField(Component component, string fieldName, object value)
        {
            if (component == null)
                return;
            FieldInfo field = component.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
                return;
            if (value != null && !field.FieldType.IsAssignableFrom(value.GetType()))
                return;
            Undo.RecordObject(component, "Configure " + component.GetType().Name);
            field.SetValue(component, value);
            EditorUtility.SetDirty(component);
        }

        private static void Invoke(Component component, string methodName, params object[] args)
        {
            if (component == null)
                return;
            MethodInfo method = component.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null)
                return;
            try
            {
                method.Invoke(component, args);
                EditorUtility.SetDirty(component);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Maverick safe tool: failed to invoke " + methodName + " on " + component.GetType().Name + ": " + ex.Message);
            }
        }
    }
}
#endif
