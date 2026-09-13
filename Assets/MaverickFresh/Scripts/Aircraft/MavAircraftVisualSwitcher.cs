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
        [Tooltip("Why the last visual application failed, or empty. A failure leaves activeAircraft and activeVisual exactly as they were.")]
        [TextArea(2, 4)] public string lastVisualError = string.Empty;

        private readonly Dictionary<MavAircraftKind, GameObject> runtimeVisuals = new Dictionary<MavAircraftKind, GameObject>();
        private readonly Dictionary<MavAircraftKind, bool> sceneClaimedVisuals = new Dictionary<MavAircraftKind, bool>();

        private void Awake()
        {
            EnsureVisualRoot();
        }

        public GameObject ApplyAircraft(MavAircraftRuntimeProfile profile)
        {
            return ApplyAircraft(profile, null);
        }

        public GameObject ApplyAircraft(MavAircraftRuntimeProfile profile, GameObject explicitSource)
        {
            GameObject visual;
            string error;
            if (TryApplyAircraft(profile, explicitSource, out visual, out error))
                return visual;

            Debug.LogError("[Maverick/Aircraft] " + error, this);
            return null;
        }

        /// <summary>
        /// Applies the visual for EXACTLY the aircraft this profile declares, or changes nothing.
        ///
        /// Both overloads above used to answer a null profile with the F-22A: one by way of
        /// activeAircraft (which defaults to F22A), the other by naming it outright. Either way a
        /// caller that had lost track of which aircraft it meant was handed an F-22 that looked like
        /// a successful application.
        ///
        /// The other half of that bug was ordering. activeAircraft was assigned from the profile
        /// BEFORE the visual had been resolved, so a resolution that produced nothing still left the
        /// switcher claiming the new aircraft with the old aircraft's model on screen. Now nothing
        /// is committed until the visual actually exists.
        /// </summary>
        public bool TryApplyAircraft(
            MavAircraftRuntimeProfile profile,
            GameObject explicitSource,
            out GameObject visual,
            out string error)
        {
            visual = null;

            if (!TryPrepareVisual(profile, explicitSource, out visual, out error))
                return false;

            CommitVisual(profile.aircraft, visual);
            return true;
        }

        /// <summary>
        /// PHASE 1 of a two-phase application: get the visual for this profile into existence,
        /// parented and posed, without changing which aircraft is active.
        ///
        /// Split out so a caller that also changes physics can find out whether the visual is
        /// available BEFORE it touches the Rigidbody - which is what makes an aircraft change atomic
        /// rather than "physics definitely, visual hopefully".
        /// </summary>
        public bool TryPrepareVisual(
            MavAircraftRuntimeProfile profile,
            GameObject explicitSource,
            out GameObject visual,
            out string error)
        {
            visual = null;

            if (profile == null)
            {
                error = "Visual switcher was given no aircraft profile. The visible aircraft was "
                        + "NOT changed, and no substitute was chosen.";
                lastVisualError = error;
                return false;
            }

            if (!MavAircraftCatalog.IsIdentityConsistent(profile, profile.aircraft, out error))
            {
                error = "Visual switcher refused an inconsistent aircraft identity: " + error;
                lastVisualError = error;
                return false;
            }

            EnsureVisualRoot();

            if (explicitSource != null)
            {
                RemoveRuntimeVisual(profile.aircraft);
                SetSource(profile.aircraft, explicitSource);
            }

            if (autoFindSceneVisuals)
                AutoResolveAllSceneVisuals();

            // PreclaimAllVisuals deactivates EVERY runtime visual, including the one currently on
            // screen, and it runs before resolution can fail. So remember what was showing: a
            // refusal has to put the previous aircraft back, not leave the pilot looking at nothing.
            GameObject previouslyShown = activeVisual;
            bool previouslyShownWasActive = previouslyShown != null && previouslyShown.activeSelf;

            if (preclaimAllResolvedVisuals)
                PreclaimAllVisuals();

            visual = EnsureRuntimeVisual(profile, explicitSource);

            if (visual == null)
            {
                if (previouslyShownWasActive)
                    previouslyShown.SetActive(true);

                error = "No visual could be resolved for " + profile.displayName
                        + " (MavAircraftKind." + profile.aircraft + "). Assign its visual source, or "
                        + "enable createPlaceholderIfMissing to fly an explicitly marked placeholder. "
                        + "The visible aircraft was NOT changed and no other aircraft was substituted.";
                lastVisualError = error;
                return false;
            }

            error = string.Empty;
            lastVisualError = string.Empty;
            return true;
        }

        /// <summary>
        /// Whether an aircraft visual has actually been committed on this switcher.
        ///
        /// activeAircraft alone cannot answer that: it is a serialized field with a default, so it
        /// names an aircraft from the moment the component exists. This is the visual half of the
        /// same distinction the applier draws between a requested and an applied identity.
        /// </summary>
        public bool HasCommittedVisual
        {
            get { return activeVisual != null; }
        }

        /// <summary>
        /// PHASE 2: make the prepared visual the active one. Deliberately cannot fail - everything
        /// that can go wrong went wrong in phase 1.
        /// </summary>
        public void CommitVisual(MavAircraftKind kind, GameObject visual)
        {
            if (visual == null)
                return;

            activeAircraft = kind;
            SetOnlyActive(kind, visual);
            activeVisual = visual;
            activeVisualName = visual.name;
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
            PreclaimOne(MavAircraftKind.F15E);
            PreclaimOne(MavAircraftKind.F16C);
            PreclaimOne(MavAircraftKind.FA18E);
            PreclaimOne(MavAircraftKind.F22A);
            PreclaimOne(MavAircraftKind.F35A);

            foreach (KeyValuePair<MavAircraftKind, GameObject> kv in runtimeVisuals)
            {
                if (kv.Value != null)
                    kv.Value.SetActive(false);
            }
        }

        /// <summary>
        /// Reserves one aircraft's visual. A kind whose profile cannot be resolved is skipped, not
        /// replaced: pre-claiming is an optimisation, and it must not be able to park one
        /// aircraft's model under another aircraft's key.
        /// </summary>
        private void PreclaimOne(MavAircraftKind kind)
        {
            MavAircraftRuntimeProfile profile;
            string error;
            if (!MavAircraftCatalog.TryGetBuiltIn(kind, out profile, out error))
                return;

            EnsureRuntimeVisual(profile, null);
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
            if (profile == null)
                return null;

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
