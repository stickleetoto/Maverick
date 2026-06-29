using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Maverick landing gear controller.
/// Attach this to Mav_Player.
///
/// Supports two modes:
/// 1) Preferred for imported aircraft models:
///    Find state groups like "F-22LandingOn" and "F-22LandingOff".
///    Gear DOWN  => LandingOn active,  LandingOff inactive.
///    Gear UP    => LandingOn inactive, LandingOff active.
///
/// 2) Fallback:
///    Find individual wheel/gear/strut objects and hide/show them.
/// </summary>
public class MavLandingGearSystem : MonoBehaviour
{
    [Header("Input")]
    public KeyCode toggleKey = KeyCode.G;
    public bool inputEnabled = true;

    [Header("State")]
    public bool gearDown = true;
    [Range(0f, 1f)] public float gearProgress = 1f;

    [Header("Timing")]
    public float extendSeconds = 0.25f;
    public float retractSeconds = 0.25f;

    [Header("LandingOn / LandingOff State Groups")]
    public bool useLandingOnOffGroups = true;
    public bool autoFindLandingOnOffGroups = true;

    [Tooltip("Objects with these strings in their name are treated as the visible gear-down state. Example: F-22LandingOn")]
    public string[] landingOnKeywords = new string[]
    {
        "landingon", "landing_on", "landing on", "gearondown", "geardown", "gear_down", "wheelson", "wheelsdown"
    };

    [Tooltip("Objects with these strings in their name are treated as the visible gear-up/retracted state. Example: F-22LandingOff")]
    public string[] landingOffKeywords = new string[]
    {
        "landingoff", "landing_off", "landing off", "gearoffup", "gearup", "gear_up", "wheelsoff", "wheelsup"
    };

    public List<GameObject> landingOnObjects = new List<GameObject>();
    public List<GameObject> landingOffObjects = new List<GameObject>();

    [Header("Fallback Auto Find")]
    public bool autoFindGearVisuals = true;
    public bool searchActiveAircraftOnly = true;
    public bool refreshWhenAircraftVisualChanges = true;
    public float refreshIntervalSeconds = 0.5f;

    [Header("Fallback Individual Gear Visuals")]
    public List<Transform> gearVisuals = new List<Transform>();
    public bool hideWhenRetracted = true;

    [Header("Fallback Animation")]
    public bool animateRotation = false;

    [Tooltip("Generic retract rotation. If this rotates the gear wrong, disable Animate Rotation and use hide-only mode.")]
    public Vector3 retractRotationOffsetEuler = new Vector3(-90f, 0f, 0f);

    [Range(0f, 1f)] public float retractedScaleMultiplier = 1f;

    [Header("Debug")]
    public string activeAircraftVisualName = "";
    public int foundLandingOnCount = 0;
    public int foundLandingOffCount = 0;
    public int foundGearCount = 0;
    public bool usingLandingOnOffMode = false;
    public bool debugLogs = false;

    private readonly Dictionary<Transform, PoseData> basePose = new Dictionary<Transform, PoseData>();

    private Transform aircraftVisualsRoot;
    private Transform activeAircraftRoot;
    private float nextRefreshTime;
    private bool warnedNoGear;

    private struct PoseData
    {
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;

        public PoseData(Transform t)
        {
            localPosition = t.localPosition;
            localRotation = t.localRotation;
            localScale = t.localScale;
        }
    }

    private void Awake()
    {
        aircraftVisualsRoot = transform.Find("AircraftVisuals");
        RefreshAll();
        ApplyGearInstant();
    }

    private void Update()
    {
        if (inputEnabled && Input.GetKeyDown(toggleKey))
        {
            ToggleGear();
        }

        if (refreshWhenAircraftVisualChanges && Time.unscaledTime >= nextRefreshTime)
        {
            nextRefreshTime = Time.unscaledTime + Mathf.Max(0.1f, refreshIntervalSeconds);
            CheckAircraftVisualChanged();
        }

        UpdateGearMotion();
    }

    public void ToggleGear()
    {
        SetGearDown(!gearDown);
    }

    public void SetGearDown(bool down)
    {
        if (gearDown == down)
            return;

        gearDown = down;

        if (debugLogs)
            Debug.Log($"[MavLandingGearSystem] Gear {(gearDown ? "DOWN" : "UP")}");

        if (!usingLandingOnOffMode && gearDown)
            SetGearObjectsActive(true);
    }

    public void RefreshAll()
    {
        aircraftVisualsRoot = transform.Find("AircraftVisuals");
        Transform searchRoot = GetSearchRoot();
        activeAircraftRoot = searchRoot;
        activeAircraftVisualName = searchRoot != null ? searchRoot.name : "(none)";

        if (autoFindLandingOnOffGroups)
            RefreshLandingOnOffGroups(searchRoot);

        usingLandingOnOffMode = useLandingOnOffGroups && (landingOnObjects.Count > 0 || landingOffObjects.Count > 0);

        if (autoFindGearVisuals)
            RefreshGearVisuals(searchRoot);

        CacheBasePoses();

        foundLandingOnCount = landingOnObjects.Count;
        foundLandingOffCount = landingOffObjects.Count;
        foundGearCount = gearVisuals.Count;
    }

    public void RefreshLandingOnOffGroups()
    {
        RefreshLandingOnOffGroups(GetSearchRoot());
        usingLandingOnOffMode = useLandingOnOffGroups && (landingOnObjects.Count > 0 || landingOffObjects.Count > 0);
        foundLandingOnCount = landingOnObjects.Count;
        foundLandingOffCount = landingOffObjects.Count;
        ApplyGearInstant();
    }

    private void RefreshLandingOnOffGroups(Transform searchRoot)
    {
        landingOnObjects.Clear();
        landingOffObjects.Clear();

        if (searchRoot == null)
            return;

        Transform[] all = searchRoot.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in all)
        {
            if (t == searchRoot)
                continue;

            string normalized = NormalizeName(t.name);

            if (NameMatches(normalized, landingOnKeywords))
            {
                AddUnique(landingOnObjects, t.gameObject);
            }
            else if (NameMatches(normalized, landingOffKeywords))
            {
                AddUnique(landingOffObjects, t.gameObject);
            }
        }
    }

    public void RefreshGearVisuals()
    {
        RefreshGearVisuals(GetSearchRoot());
        foundGearCount = gearVisuals.Count;
        ApplyGearInstant();
    }

    private void RefreshGearVisuals(Transform searchRoot)
    {
        gearVisuals.Clear();
        basePose.Clear();

        if (searchRoot == null)
            return;

        Transform[] all = searchRoot.GetComponentsInChildren<Transform>(true);

        foreach (Transform t in all)
        {
            if (t == searchRoot)
                continue;

            string n = NormalizeName(t.name);

            // Do not treat whole LandingOn/LandingOff state groups as individual gear parts.
            if (NameMatches(n, landingOnKeywords) || NameMatches(n, landingOffKeywords))
                continue;

            bool looksLikeGear =
                n.Contains("gear") ||
                n.Contains("wheel") ||
                n.Contains("tire") ||
                n.Contains("tyre") ||
                n.Contains("landing") ||
                n.Contains("nosewheel") ||
                n.Contains("mainwheel") ||
                n.Contains("strut") ||
                n.Contains("bogie");

            if (!looksLikeGear)
                continue;

            if (!gearVisuals.Contains(t))
                gearVisuals.Add(t);
        }

        CacheBasePoses();

        if (gearVisuals.Count == 0 && landingOnObjects.Count == 0 && landingOffObjects.Count == 0 && !warnedNoGear)
        {
            warnedNoGear = true;
            Debug.LogWarning("[MavLandingGearSystem] No gear visuals found. Expected groups like F-22LandingOn/F-22LandingOff, or add objects manually.");
        }
    }

    private Transform GetSearchRoot()
    {
        if (!searchActiveAircraftOnly)
            return aircraftVisualsRoot != null ? aircraftVisualsRoot : transform;

        if (aircraftVisualsRoot == null)
            return transform;

        for (int i = 0; i < aircraftVisualsRoot.childCount; i++)
        {
            Transform child = aircraftVisualsRoot.GetChild(i);
            if (child.gameObject.activeSelf)
                return child;
        }

        return aircraftVisualsRoot;
    }

    private void CheckAircraftVisualChanged()
    {
        Transform current = GetSearchRoot();
        if (current != activeAircraftRoot)
        {
            RefreshAll();
            ApplyGearInstant();
        }
    }

    private void CacheBasePoses()
    {
        foreach (Transform t in gearVisuals)
        {
            if (t == null)
                continue;

            if (!basePose.ContainsKey(t))
                basePose.Add(t, new PoseData(t));
        }
    }

    private void UpdateGearMotion()
    {
        float target = gearDown ? 1f : 0f;
        float seconds = gearDown ? extendSeconds : retractSeconds;
        float speed = seconds <= 0.01f ? 999f : 1f / seconds;

        gearProgress = Mathf.MoveTowards(gearProgress, target, speed * Time.deltaTime);

        if (usingLandingOnOffMode)
        {
            ApplyLandingOnOffState();
            return;
        }

        ApplyGearPose();

        if (!gearDown && gearProgress <= 0.001f && hideWhenRetracted)
            SetGearObjectsActive(false);
    }

    private void ApplyGearInstant()
    {
        gearProgress = gearDown ? 1f : 0f;

        if (usingLandingOnOffMode)
        {
            ApplyLandingOnOffState();
            return;
        }

        SetGearObjectsActive(true);
        ApplyGearPose();

        if (!gearDown && hideWhenRetracted)
            SetGearObjectsActive(false);
    }

    private void ApplyLandingOnOffState()
    {
        bool showDown = gearProgress >= 0.5f;
        SetObjectsActive(landingOnObjects, showDown);
        SetObjectsActive(landingOffObjects, !showDown);
    }

    private void ApplyGearPose()
    {
        float extended = Mathf.Clamp01(gearProgress);
        Quaternion retractRot = Quaternion.Euler(retractRotationOffsetEuler);

        foreach (Transform t in gearVisuals)
        {
            if (t == null)
                continue;

            if (!basePose.TryGetValue(t, out PoseData pose))
            {
                pose = new PoseData(t);
                basePose[t] = pose;
            }

            t.localPosition = pose.localPosition;

            if (animateRotation)
                t.localRotation = pose.localRotation * Quaternion.Slerp(retractRot, Quaternion.identity, extended);
            else
                t.localRotation = pose.localRotation;

            if (retractedScaleMultiplier < 0.999f)
            {
                float scaleMul = Mathf.Lerp(retractedScaleMultiplier, 1f, extended);
                t.localScale = pose.localScale * scaleMul;
            }
            else
            {
                t.localScale = pose.localScale;
            }

            if (hideWhenRetracted && gearDown && !t.gameObject.activeSelf)
                t.gameObject.SetActive(true);
        }
    }

    private void SetGearObjectsActive(bool active)
    {
        foreach (Transform t in gearVisuals)
        {
            if (t == null)
                continue;

            t.gameObject.SetActive(active);
        }
    }

    private static void SetObjectsActive(List<GameObject> objects, bool active)
    {
        for (int i = 0; i < objects.Count; i++)
        {
            GameObject go = objects[i];
            if (go == null)
                continue;

            if (go.activeSelf != active)
                go.SetActive(active);
        }
    }

    private static void AddUnique(List<GameObject> list, GameObject go)
    {
        if (go != null && !list.Contains(go))
            list.Add(go);
    }

    private static string NormalizeName(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return string.Empty;

        return raw.ToLowerInvariant()
            .Replace("-", "")
            .Replace("_", "")
            .Replace(" ", "")
            .Replace("/", "")
            .Replace(".", "");
    }

    private static bool NameMatches(string normalizedName, string[] keywords)
    {
        if (string.IsNullOrEmpty(normalizedName) || keywords == null)
            return false;

        for (int i = 0; i < keywords.Length; i++)
        {
            string k = NormalizeName(keywords[i]);
            if (!string.IsNullOrEmpty(k) && normalizedName.Contains(k))
                return true;
        }

        return false;
    }
}
