using System.Collections.Generic;
using UnityEngine;

namespace MaverickFresh
{
    /// <summary>
    /// Keeps the flyable player as one generic physics object and swaps only the visible aircraft model.
    /// This prevents the game from depending on F15E_Player or any specific FBX existing in the scene.
    /// Put this on Mav_Player. Assign prefab assets or loose scene FBX objects, or let it auto-find by name.
    /// </summary>
    [DisallowMultipleComponent]
    public class MavAircraftVisualSwitcher : MonoBehaviour
    {
        [Header("Visual Root")]
        public Transform visualRoot;
        public string visualRootName = "AircraftVisuals";

        [Header("Aircraft visual sources - prefab asset or loose scene object")]
        public GameObject f15Visual;
        public GameObject f16Visual;
        public GameObject fa18Visual;
        public GameObject f22Visual;
        public GameObject f35Visual;

        [Header("Auto Resolve")]
        public bool autoFindSceneVisuals = true;
        public bool claimLooseSceneVisuals = true;
        public bool createPlaceholderIfMissing = true;
        public bool preclaimAllResolvedVisuals = true;
        public bool disableVisualColliders = true;
        public bool disableVisualRigidbodies = true;
        public bool preserveClaimedSceneScale = true;
        public bool applyProfileScaleToPrefabInstances = true;
        public bool preferVisualRootChildren = true;
        public bool renameRuntimeVisuals = false;

        [Header("Runtime")]
        public MavAircraftKind activeAircraft = MavAircraftKind.F22A;
        public GameObject activeVisual;
        public string activeVisualName = "none";
        public bool lastVisualWasPlaceholder;

        private readonly Dictionary<MavAircraftKind, GameObject> runtimeVisuals = new Dictionary<MavAircraftKind, GameObject>();
        private readonly Dictionary<MavAircraftKind, bool> sceneClaimedVisuals = new Dictionary<MavAircraftKind, bool>();

        private void Awake()
        {
            EnsureVisualRoot();
        }

        public GameObject ApplyAircraft(MavAircraftRuntimeProfile profile)
        {
            if (profile == null)
                profile = MavAircraftCatalog.GetBuiltIn(activeAircraft);

            return ApplyAircraft(profile, null);
        }

        public GameObject ApplyAircraft(MavAircraftRuntimeProfile profile, GameObject explicitSource)
        {
            if (profile == null)
                profile = MavAircraftCatalog.GetBuiltIn(MavAircraftKind.F22A);

            EnsureVisualRoot();
            activeAircraft = profile.aircraft;

            if (explicitSource != null)
            {
                RemoveRuntimeVisual(profile.aircraft);
                SetSource(profile.aircraft, explicitSource);
            }

            if (autoFindSceneVisuals)
                AutoResolveAllSceneVisuals();

            if (preclaimAllResolvedVisuals)
                PreclaimAllVisuals();

            GameObject visual = EnsureRuntimeVisual(profile, explicitSource);
            SetOnlyActive(profile.aircraft, visual);
            activeVisual = visual;
            activeVisualName = visual != null ? visual.name : "none";
            return visual;
        }

        public void RemoveRuntimeVisual(MavAircraftKind kind)
        {
            GameObject existing;
            if (runtimeVisuals.TryGetValue(kind, out existing) && existing != null)
            {
                if (IsSceneClaimed(kind))
                    existing.SetActive(false);
                else if (existing.scene.IsValid() && existing.scene.isLoaded)
                    Destroy(existing);
            }
            runtimeVisuals.Remove(kind);
            sceneClaimedVisuals.Remove(kind);
        }

        public void PreclaimAllVisuals()
        {
            EnsureRuntimeVisual(MavAircraftCatalog.GetBuiltIn(MavAircraftKind.F15E), null);
            EnsureRuntimeVisual(MavAircraftCatalog.GetBuiltIn(MavAircraftKind.F16C), null);
            EnsureRuntimeVisual(MavAircraftCatalog.GetBuiltIn(MavAircraftKind.FA18E), null);
            EnsureRuntimeVisual(MavAircraftCatalog.GetBuiltIn(MavAircraftKind.F22A), null);
            EnsureRuntimeVisual(MavAircraftCatalog.GetBuiltIn(MavAircraftKind.F35A), null);

            foreach (KeyValuePair<MavAircraftKind, GameObject> kv in runtimeVisuals)
            {
                if (kv.Value != null)
                    kv.Value.SetActive(false);
            }
        }

        public GameObject GetSource(MavAircraftKind kind)
        {
            switch (kind)
            {
                case MavAircraftKind.F15E: return f15Visual;
                case MavAircraftKind.F16C: return f16Visual;
                case MavAircraftKind.FA18E: return fa18Visual;
                case MavAircraftKind.F22A: return f22Visual;
                case MavAircraftKind.F35A: return f35Visual;
                default: return null;
            }
        }

        public void SetSource(MavAircraftKind kind, GameObject source)
        {
            switch (kind)
            {
                case MavAircraftKind.F15E: f15Visual = source; break;
                case MavAircraftKind.F16C: f16Visual = source; break;
                case MavAircraftKind.FA18E: fa18Visual = source; break;
                case MavAircraftKind.F22A: f22Visual = source; break;
                case MavAircraftKind.F35A: f35Visual = source; break;
            }
        }

        public void AutoResolveAllSceneVisuals()
        {
            if (!autoFindSceneVisuals)
                return;

            TryAutoAssign(MavAircraftKind.F15E);
            TryAutoAssign(MavAircraftKind.F16C);
            TryAutoAssign(MavAircraftKind.FA18E);
            TryAutoAssign(MavAircraftKind.F22A);
            TryAutoAssign(MavAircraftKind.F35A);
        }

        private GameObject EnsureRuntimeVisual(MavAircraftRuntimeProfile profile, GameObject explicitSource)
        {
            GameObject existing;
            if (runtimeVisuals.TryGetValue(profile.aircraft, out existing) && existing != null)
            {
                ApplyVisualPose(existing, profile, IsSceneClaimed(profile.aircraft));
                return existing;
            }

            GameObject source = explicitSource != null ? explicitSource : GetSource(profile.aircraft);
            if (source == null && autoFindSceneVisuals)
                source = TryAutoAssign(profile.aircraft);

            bool sceneClaimed = false;
            lastVisualWasPlaceholder = false;
            GameObject visual = null;

            if (source != null)
            {
                bool sourceIsSceneObject = source.scene.IsValid() && source.scene.isLoaded;
                if (sourceIsSceneObject && claimLooseSceneVisuals && !source.transform.IsChildOf(visualRoot))
                {
                    visual = source;
                    sceneClaimed = true;
                    visual.transform.SetParent(visualRoot, false);
                }
                else if (source.transform.IsChildOf(visualRoot))
                {
                    visual = source;
                    sceneClaimed = true;
                }
                else
                {
                    visual = Instantiate(source, visualRoot);
                    sceneClaimed = false;
                }
            }

            if (visual == null && createPlaceholderIfMissing)
            {
                visual = MavAircraftVisualFactory.CreateDisplayVisual(profile, visualRoot, null, false);
                sceneClaimed = false;
                lastVisualWasPlaceholder = true;
            }

            if (visual == null)
                return null;

            if (renameRuntimeVisuals)
                visual.name = profile.aircraftId + "_visual_active_model";
            PrepareVisual(visual);
            runtimeVisuals[profile.aircraft] = visual;
            sceneClaimedVisuals[profile.aircraft] = sceneClaimed;
            ApplyVisualPose(visual, profile, sceneClaimed);
            return visual;
        }

        private void ApplyVisualPose(GameObject visual, MavAircraftRuntimeProfile profile, bool sceneClaimed)
        {
            if (visual == null)
                return;

            visual.transform.SetParent(visualRoot, false);
            visual.transform.localPosition = profile.flightVisualLocalPosition;
            visual.transform.localRotation = Quaternion.Euler(profile.flightVisualLocalEuler);

            if (!sceneClaimed || !preserveClaimedSceneScale)
            {
                float scale = Mathf.Max(0.001f, profile.flightVisualScale);
                if (applyProfileScaleToPrefabInstances || !sceneClaimed)
                    visual.transform.localScale = Vector3.one * scale;
            }
        }

        private void SetOnlyActive(MavAircraftKind active, GameObject activeObject)
        {
            foreach (KeyValuePair<MavAircraftKind, GameObject> kv in runtimeVisuals)
            {
                if (kv.Value != null)
                    kv.Value.SetActive(kv.Key == active && kv.Value == activeObject);
            }
        }

        private bool IsSceneClaimed(MavAircraftKind kind)
        {
            bool value;
            return sceneClaimedVisuals.TryGetValue(kind, out value) && value;
        }

        private GameObject TryAutoAssign(MavAircraftKind kind)
        {
            GameObject already = GetSource(kind);
            if (already != null)
                return already;

            string[] tokens = TokensFor(kind);

            if (preferVisualRootChildren && visualRoot != null)
            {
                GameObject child = FindBestCandidate(tokens, visualRoot.GetComponentsInChildren<Transform>(true), true);
                if (child != null)
                {
                    SetSource(kind, child);
                    return child;
                }
            }

            GameObject sceneObject = FindBestCandidate(tokens, FindObjectsOfType<Transform>(true), false);
            if (sceneObject != null)
            {
                SetSource(kind, sceneObject);
                return sceneObject;
            }

            return null;
        }

        private GameObject FindBestCandidate(string[] tokens, Transform[] candidates, bool allowChildrenOfThisPlayer)
        {
            Transform best = null;
            int bestScore = -1;

            for (int i = 0; i < candidates.Length; i++)
            {
                Transform t = candidates[i];
                if (t == null || t == transform || t == visualRoot)
                    continue;
                if (!allowChildrenOfThisPlayer && t.IsChildOf(transform))
                    continue;
                if (!IsAcceptableVisualCandidate(t.gameObject, allowChildrenOfThisPlayer))
                    continue;

                string n = t.name.ToLowerInvariant();
                int score = ScoreName(n, tokens);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = t;
                }
            }

            return best != null && bestScore > 0 ? best.gameObject : null;
        }

        private static string[] TokensFor(MavAircraftKind kind)
        {
            switch (kind)
            {
                case MavAircraftKind.F15E: return new[] { "f15ex", "f-15ex", "f15_ex", "f15 ex", "f15", "f-15", "f15e", "f_15" };
                case MavAircraftKind.F16C: return new[] { "f16", "f-16", "f16c", "f_16" };
                case MavAircraftKind.FA18E: return new[] { "f18", "f-18", "fa18", "fa-18", "f/a-18", "f_18" };
                case MavAircraftKind.F22A: return new[] { "f22", "f-22", "f22a", "f_22" };
                case MavAircraftKind.F35A: return new[] { "f35", "f-35", "f35a", "f_35" };
                default: return new string[0];
            }
        }

        private static int ScoreName(string lowerName, string[] tokens)
        {
            if (string.IsNullOrEmpty(lowerName) || tokens == null)
                return -1;

            int score = -1;
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (string.IsNullOrEmpty(token))
                    continue;
                if (lowerName == token)
                    score = Mathf.Max(score, 100 + token.Length);
                else if (lowerName.Contains(token))
                    score = Mathf.Max(score, 10 + token.Length);
            }
            return score;
        }

        private bool IsAcceptableVisualCandidate(GameObject go, bool allowChildrenOfThisPlayer = false)
        {
            if (go == null)
                return false;
            if (go.GetComponent<Camera>() != null || go.GetComponent<Light>() != null)
                return false;
            if (go.GetComponent<MavMouseFlightJet>() != null || go.GetComponent<MavInGameBootstrap>() != null || go.GetComponent<MavFreshBootstrap>() != null)
                return false;
            if (go.GetComponent<MavAircraftVisualSwitcher>() != null)
                return false;
            if (!allowChildrenOfThisPlayer && (go.name.StartsWith("Mav_") || go.name.StartsWith("MaverickFresh_")))
                return false;
            return go.GetComponentInChildren<Renderer>(true) != null;
        }

        private void PrepareVisual(GameObject visual)
        {
            if (visual == null)
                return;

            if (disableVisualColliders)
            {
                Collider[] colliders = visual.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < colliders.Length; i++)
                    colliders[i].enabled = false;
            }

            if (disableVisualRigidbodies)
            {
                Rigidbody[] bodies = visual.GetComponentsInChildren<Rigidbody>(true);
                for (int i = 0; i < bodies.Length; i++)
                {
                    if (bodies[i].gameObject != gameObject)
                        bodies[i].isKinematic = true;
                }
            }
        }

        private void EnsureVisualRoot()
        {
            if (visualRoot != null)
                return;

            Transform existing = transform.Find(visualRootName);
            if (existing != null)
            {
                visualRoot = existing;
                return;
            }

            GameObject root = new GameObject(string.IsNullOrEmpty(visualRootName) ? "AircraftVisuals" : visualRootName);
            root.transform.SetParent(transform, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            visualRoot = root.transform;
        }
    }
}
