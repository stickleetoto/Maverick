using System.Collections.Generic;
using UnityEngine;

namespace MaverickFresh
{
    public enum MavLandingGearState
    {
        Down = 0,
        MovingUp = 1,
        Up = 2,
        MovingDown = 3
    }

    /// <summary>
    /// Merge-safe landing gear controller for manually built Mav_Player rigs.
    /// Attach this to Mav_Player. Put wheel/gear objects under AircraftVisuals or assign them manually.
    /// G toggles gear. The script only animates/hides visuals and adds optional drag; it does not require scene/prefab changes.
    /// </summary>
    [DisallowMultipleComponent]
    public class MavLandingGearSystem : MonoBehaviour
    {
        [Header("Controls")]
        public bool allowInput = true;
        public KeyCode toggleGearKey = KeyCode.G;
        public bool startGearDown = true;

        [Header("Visual References")]
        [Tooltip("Optional root to scan. If empty, the script scans this object and AircraftVisuals children.")]
        public Transform visualSearchRoot;
        public List<Transform> gearVisuals = new List<Transform>();
        public bool autoFindGearVisuals = true;
        public bool includeInactiveChildren = true;
        public bool hideWhenRetracted = true;

        [Header("Animation")]
        public float animationSeconds = 0.65f;
        public Vector3 retractLocalOffset = new Vector3(0f, 0.55f, -0.25f);
        public Vector3 retractLocalEuler = new Vector3(-85f, 0f, 0f);
        public bool animateLocalPose = true;

        [Header("Flight Effects")]
        public bool applyGearDrag = true;
        public float gearDragAcceleration = 5.0f;
        public bool autoRetractAtHighSpeed = true;
        public float autoRetractSpeed = 245f;
        public float highSpeedGearWarning = 190f;

        [Header("Runtime")]
        public MavLandingGearState state = MavLandingGearState.Down;
        public bool gearDown = true;
        public bool hasGearVisuals;
        public string status = "gear_down";
        public float debugSpeed;
        public float debugAnim01;
        public float debugDragAccel;

        private struct GearPose
        {
            public Transform transform;
            public Vector3 downPosition;
            public Quaternion downRotation;
            public Vector3 downScale;
        }

        private readonly List<GearPose> poses = new List<GearPose>();
        private Rigidbody rb;
        private bool initialized;
        private float animStartTime;
        private float animDuration;
        private float from01;
        private float to01;
        private float gear01; // 0 = up, 1 = down

        private static readonly string[] GearNameTokens =
        {
            "gear", "wheel", "tire", "tyre", "landing", "nosewheel", "mainwheel", "strut", "bogie"
        };

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            InitializeIfNeeded();
            SetGearImmediate(startGearDown);
        }

        private void OnEnable()
        {
            InitializeIfNeeded();
        }

        private void Update()
        {
            InitializeIfNeeded();

            if (allowInput && MavFreshInput.GetKeyDown(toggleGearKey))
                ToggleGear();

            UpdateAnimation();
            UpdateStatus();
        }

        private void FixedUpdate()
        {
            if (rb == null)
                rb = GetComponent<Rigidbody>();

            debugSpeed = rb != null ? rb.linearVelocity.magnitude : 0f;
            debugDragAccel = 0f;

            if (autoRetractAtHighSpeed && gearDown && state == MavLandingGearState.Down && debugSpeed > autoRetractSpeed)
                SetGear(false);

            if (!applyGearDrag || rb == null || gear01 <= 0.01f)
                return;

            Vector3 velocity = rb.linearVelocity;
            float speed = velocity.magnitude;
            if (speed < 1f)
                return;

            float drag = gearDragAcceleration * Mathf.Clamp01(gear01) * Mathf.InverseLerp(35f, highSpeedGearWarning, speed);
            debugDragAccel = drag;
            rb.AddForce(-velocity.normalized * drag, ForceMode.Acceleration);
        }

        public void ToggleGear()
        {
            SetGear(!gearDown);
        }

        public void SetGear(bool down)
        {
            InitializeIfNeeded();

            gearDown = down;
            state = down ? MavLandingGearState.MovingDown : MavLandingGearState.MovingUp;
            animStartTime = Time.time;
            animDuration = Mathf.Max(0.01f, animationSeconds);
            from01 = gear01;
            to01 = down ? 1f : 0f;

            if (down)
                SetVisualsActive(true);
        }

        public void SetGearImmediate(bool down)
        {
            InitializeIfNeeded();
            gearDown = down;
            gear01 = down ? 1f : 0f;
            debugAnim01 = gear01;
            state = down ? MavLandingGearState.Down : MavLandingGearState.Up;
            ApplyPose(gear01);
            if (hideWhenRetracted)
                SetVisualsActive(down);
            UpdateStatus();
        }

        public void RebuildVisualCache()
        {
            initialized = false;
            InitializeIfNeeded(true);
            ApplyPose(gear01);
        }

        private void InitializeIfNeeded(bool force = false)
        {
            if (initialized && !force)
                return;

            initialized = true;
            if (rb == null)
                rb = GetComponent<Rigidbody>();

            if (visualSearchRoot == null)
            {
                Transform aircraftVisuals = FindChildRecursive(transform, "AircraftVisuals");
                visualSearchRoot = aircraftVisuals != null ? aircraftVisuals : transform;
            }

            if (autoFindGearVisuals)
                AutoFindGearVisuals();

            BuildPoseCache();
            hasGearVisuals = poses.Count > 0;
        }

        private void AutoFindGearVisuals()
        {
            if (visualSearchRoot == null)
                return;

            Transform[] all = visualSearchRoot.GetComponentsInChildren<Transform>(includeInactiveChildren);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null || t == visualSearchRoot)
                    continue;

                string n = t.name.ToLowerInvariant();
                bool match = false;
                for (int j = 0; j < GearNameTokens.Length; j++)
                {
                    if (n.Contains(GearNameTokens[j]))
                    {
                        match = true;
                        break;
                    }
                }

                if (match && !gearVisuals.Contains(t))
                    gearVisuals.Add(t);
            }
        }

        private void BuildPoseCache()
        {
            poses.Clear();
            for (int i = 0; i < gearVisuals.Count; i++)
            {
                Transform t = gearVisuals[i];
                if (t == null)
                    continue;

                GearPose p = new GearPose
                {
                    transform = t,
                    downPosition = t.localPosition,
                    downRotation = t.localRotation,
                    downScale = t.localScale
                };
                poses.Add(p);
            }
        }

        private void UpdateAnimation()
        {
            if (state != MavLandingGearState.MovingUp && state != MavLandingGearState.MovingDown)
                return;

            float t = Mathf.Clamp01((Time.time - animStartTime) / Mathf.Max(0.01f, animDuration));
            t = Mathf.SmoothStep(0f, 1f, t);
            gear01 = Mathf.Lerp(from01, to01, t);
            debugAnim01 = gear01;
            ApplyPose(gear01);

            if (t >= 0.999f)
            {
                gear01 = to01;
                debugAnim01 = gear01;
                state = gearDown ? MavLandingGearState.Down : MavLandingGearState.Up;
                ApplyPose(gear01);
                if (hideWhenRetracted)
                    SetVisualsActive(gearDown);
            }
        }

        private void ApplyPose(float down01)
        {
            float up01 = 1f - Mathf.Clamp01(down01);
            for (int i = 0; i < poses.Count; i++)
            {
                GearPose p = poses[i];
                if (p.transform == null)
                    continue;

                if (animateLocalPose)
                {
                    p.transform.localPosition = p.downPosition + retractLocalOffset * up01;
                    p.transform.localRotation = p.downRotation * Quaternion.Euler(retractLocalEuler * up01);
                }

                p.transform.localScale = p.downScale;
            }
        }

        private void SetVisualsActive(bool active)
        {
            for (int i = 0; i < poses.Count; i++)
            {
                Transform t = poses[i].transform;
                if (t != null)
                    t.gameObject.SetActive(active);
            }
        }

        private void UpdateStatus()
        {
            string high = debugSpeed > highSpeedGearWarning && gear01 > 0.5f ? " OVERSPEED" : string.Empty;
            switch (state)
            {
                case MavLandingGearState.Down:
                    status = "GEAR DOWN" + high;
                    break;
                case MavLandingGearState.Up:
                    status = "GEAR UP";
                    break;
                case MavLandingGearState.MovingDown:
                    status = "GEAR EXTENDING";
                    break;
                case MavLandingGearState.MovingUp:
                    status = "GEAR RETRACTING";
                    break;
            }
        }

        private static Transform FindChildRecursive(Transform root, string name)
        {
            if (root == null)
                return null;

            if (root.name == name)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildRecursive(root.GetChild(i), name);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
