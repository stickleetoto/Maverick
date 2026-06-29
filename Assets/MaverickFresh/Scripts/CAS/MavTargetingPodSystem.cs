using UnityEngine;
using UnityEngine.UI;

namespace MaverickFresh
{
    public enum MavTargetingPodDisplayMode
    {
        Off = 0,
        PictureInPicture = 1,
        Fullscreen = 2
    }

    /// <summary>
    /// Sim-lite targeting pod for CAS.
    ///
    /// Features:
    /// - Creates/uses a pod camera
    /// - PIP or fullscreen TGP display
    /// - Slew with I/J/K/L
    /// - Zoom with 5/6 or +/- keys
    /// - Lock point with R
    /// - Send designation to MavCASTargetingSystem with B (G is landing gear)
    ///
    /// This is a gameplay TGP, not a real sensor simulation.
    /// </summary>
    [DisallowMultipleComponent]
    public class MavTargetingPodSystem : MonoBehaviour
    {
        [Header("References")]
        public Transform podMount;
        public Camera podCamera;
        public RenderTexture podTexture;
        public MavCASTargetingSystem casTargeting;
        public MavCASWeaponSystem casWeapons;

        [Header("Display")]
        public MavTargetingPodDisplayMode displayMode = MavTargetingPodDisplayMode.Off;
        public bool startHidden = true;
        public bool forceOffOnStart = true;
        public bool showPipByDefault = false;
        public bool allowFullscreenMode;
        public int textureSize = 512;
        public Rect pipRect = new Rect(18f, 240f, 320f, 220f);
        public CanvasGroup pipCanvasGroup;
        public RawImage pipRawImage;
        public Vector2 defaultPipAnchorMin = new Vector2(0.72f, 0.62f);
        public Vector2 defaultPipAnchorMax = new Vector2(0.98f, 0.96f);
        public float defaultPipAlpha = 1f;
        public bool applyDefaultPipAnchorsOnStart = true;
        public bool drawOverlay = true;
        public bool drawDebugText = true;

        [Header("Controls")]
        [Tooltip("Disabled by default so T is reserved for radar/lock controls. Enable manually only when using the legacy CAS targeting pod.")]
        public bool inputEnabled = false;
        public KeyCode toggleDisplayKey = KeyCode.None;
        public KeyCode toggleFocusKey = KeyCode.V;
        public KeyCode exitFocusKey = KeyCode.Escape;
        public KeyCode cycleDisplayKey = KeyCode.O;
        public KeyCode lockPointKey = KeyCode.R;
        public KeyCode pushDesignationKey = KeyCode.B;
        public KeyCode recenterKey = KeyCode.U;
        public KeyCode slewUpKey = KeyCode.I;
        public KeyCode slewDownKey = KeyCode.K;
        public KeyCode slewLeftKey = KeyCode.J;
        public KeyCode slewRightKey = KeyCode.L;
        public KeyCode zoomInKey = KeyCode.Alpha5;
        public KeyCode zoomOutKey = KeyCode.Alpha6;
        public KeyCode zoomInAltKey = KeyCode.Equals;
        public KeyCode zoomOutAltKey = KeyCode.Minus;

        [Header("Optics")]
        public float fov = 24f;
        public float minFov = 5f;
        public float maxFov = 65f;
        public float zoomSpeed = 35f;
        public float slewSpeedDeg = 34f;
        public float fineSlewMultiplier = 0.35f;
        public float maxYaw = 160f;
        public float minPitch = -88f;
        public float maxPitch = 35f;

        [Header("Raycast / Targeting")]
        public float maxRange = 12000f;
        public LayerMask raycastMask = ~0;
        public float targetSnapRadiusScreen = 0.065f;
        public bool autoSnapTargetNearReticle = true;
        public bool lockFollowsMovingTarget = true;

        [Header("Runtime")]
        public bool isPowered = true;
        public bool isLocked;
        public bool lockedToTarget;
        public MavCASTarget lockedTarget;
        public MavCASTarget candidateTarget;
        public bool isPipVisible;
        public bool isFocusActive;
        public Vector3 lockPoint;
        public Vector3 lookPoint;
        public bool hasLookPoint;
        public float yaw;
        public float pitch = -18f;
        public float slantRange;
        public string status = "ready";

        private GUIStyle labelStyle;
        private GUIStyle centerStyle;
        private bool startupStateApplied;
        private bool pipBeforeFocus;
        private bool warnedMainCameraReference;

        private void Awake()
        {
            Resolve();
            EnsurePodMount();
            EnsureCameraAndTexture();
            RecenterToBoresight();
            ClampDisplayMode();
            ConfigureDefaultPipUi();
            ApplyStartupDisplayState();
        }

        private void OnEnable()
        {
            Resolve();
            EnsurePodMount();
            EnsureCameraAndTexture();
            ClampDisplayMode();
            ConfigureDefaultPipUi();

            if (!startupStateApplied)
                ApplyStartupDisplayState();
            else
                ApplyDisplayState();
        }

        private void Start()
        {
            if (forceOffOnStart)
                ApplyStartupDisplayState();
        }

        private void OnDestroy()
        {
            if (podTexture != null)
            {
                podTexture.Release();
                Destroy(podTexture);
                podTexture = null;
            }
        }

        private void Update()
        {
            Resolve();
            EnsurePodMount();
            EnsureCameraAndTexture();
            ClampDisplayMode();
            ApplyDisplayState();

            HandleInput();
            UpdateLookDirection();
            UpdateRaycastAndCandidate();
            UpdateLock();
            UpdateCamera();
        }

        private void Resolve()
        {
            if (casTargeting == null) casTargeting = GetComponent<MavCASTargetingSystem>();
            if (casWeapons == null) casWeapons = GetComponent<MavCASWeaponSystem>();
        }

        private void EnsurePodMount()
        {
            if (podMount != null)
                return;

            Transform existing = transform.Find("MavTGP_Mount");
            if (existing != null)
            {
                podMount = existing;
                return;
            }

            GameObject mount = new GameObject("MavTGP_Mount");
            mount.transform.SetParent(transform, false);
            mount.transform.localPosition = new Vector3(0.7f, -2.0f, 3.5f);
            mount.transform.localRotation = Quaternion.identity;
            podMount = mount.transform;
        }

        private void EnsureCameraAndTexture()
        {
            if (podTexture == null || podTexture.width != textureSize || podTexture.height != textureSize)
            {
                if (podTexture != null)
                {
                    podTexture.Release();
                    Destroy(podTexture);
                }

                podTexture = new RenderTexture(textureSize, textureSize, 16, RenderTextureFormat.ARGB32);
                podTexture.name = "MavTGP_RenderTexture";
                podTexture.Create();
            }

            if (podCamera != null && podCamera == Camera.main)
            {
                if (!warnedMainCameraReference)
                {
                    Debug.LogWarning("MavTargetingPodSystem: Main Camera was assigned as podCamera. Creating a dedicated TGP camera so the main view stays normal.", this);
                    warnedMainCameraReference = true;
                }

                podCamera = null;
            }

            if (podCamera == null)
            {
                GameObject camObj = new GameObject("MavTGP_Camera");
                camObj.transform.SetParent(podMount != null ? podMount : transform, false);
                podCamera = camObj.AddComponent<Camera>();
                podCamera.enabled = true;
                podCamera.depth = -50;
                podCamera.clearFlags = CameraClearFlags.Skybox;
            }

            podCamera.targetTexture = podTexture;
            podCamera.enabled = true;
            podCamera.fieldOfView = fov;
            podCamera.nearClipPlane = 0.05f;
            podCamera.farClipPlane = maxRange;
        }

        private void HandleInput()
        {
            if (!inputEnabled)
                return;

            if (IsKeyDown(toggleDisplayKey))
                SetPip(displayMode != MavTargetingPodDisplayMode.PictureInPicture);

            if (IsKeyDown(toggleFocusKey))
                SetFocus(displayMode != MavTargetingPodDisplayMode.Fullscreen);

            if (IsKeyDown(exitFocusKey) && displayMode == MavTargetingPodDisplayMode.Fullscreen)
                SetFocus(false);

            if (IsKeyDown(cycleDisplayKey))
            {
                if (!allowFullscreenMode)
                {
                    SetPip(displayMode == MavTargetingPodDisplayMode.Off);
                }
                else if (displayMode == MavTargetingPodDisplayMode.Off) SetPip(true);
                else if (displayMode == MavTargetingPodDisplayMode.PictureInPicture) SetFocus(true);
                else SetOff();
            }

            if (IsKeyDown(recenterKey))
                RecenterToBoresight();

            if (IsKeyDown(lockPointKey))
                ToggleLock();

            if (IsKeyDown(pushDesignationKey))
                PushDesignationToCAS();

            float fine = MavFreshInput.GetKey(KeyCode.LeftShift) ? fineSlewMultiplier : 1f;
            float slew = slewSpeedDeg * fine * Time.deltaTime;

            if (!isLocked)
            {
                if (IsKeyHeld(slewLeftKey)) yaw -= slew;
                if (IsKeyHeld(slewRightKey)) yaw += slew;
                if (IsKeyHeld(slewUpKey)) pitch += slew;
                if (IsKeyHeld(slewDownKey)) pitch -= slew;

                yaw = Mathf.Clamp(yaw, -maxYaw, maxYaw);
                pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            }

            bool zoomIn = IsKeyHeld(zoomInKey) || IsKeyHeld(zoomInAltKey);
            bool zoomOut = IsKeyHeld(zoomOutKey) || IsKeyHeld(zoomOutAltKey);

            if (zoomIn) fov -= zoomSpeed * Time.deltaTime;
            if (zoomOut) fov += zoomSpeed * Time.deltaTime;

            fov = Mathf.Clamp(fov, minFov, maxFov);
        }

        private bool IsKeyDown(KeyCode key)
        {
            return key != KeyCode.None && MavFreshInput.GetKeyDown(key);
        }

        private bool IsKeyHeld(KeyCode key)
        {
            return key != KeyCode.None && MavFreshInput.GetKey(key);
        }

        public void SetOff()
        {
            displayMode = MavTargetingPodDisplayMode.Off;
            allowFullscreenMode = false;
            isLocked = false;
            lockedToTarget = false;
            lockedTarget = null;
            status = "off";
            ApplyDisplayState();
        }

        public void SetPip(bool enabled)
        {
            allowFullscreenMode = false;
            displayMode = enabled ? MavTargetingPodDisplayMode.PictureInPicture : MavTargetingPodDisplayMode.Off;
            status = enabled ? "pip" : "off";
            ApplyDisplayState();
        }

        public void SetFocus(bool enabled)
        {
            if (enabled)
            {
                pipBeforeFocus = displayMode == MavTargetingPodDisplayMode.PictureInPicture;
                allowFullscreenMode = true;
                displayMode = MavTargetingPodDisplayMode.Fullscreen;
                status = "focus";
            }
            else
            {
                displayMode = pipBeforeFocus ? MavTargetingPodDisplayMode.PictureInPicture : MavTargetingPodDisplayMode.Off;
                allowFullscreenMode = false;
                status = displayMode == MavTargetingPodDisplayMode.PictureInPicture ? "pip" : "off";
            }

            ApplyDisplayState();
        }

        public void ApplyStartupDisplayState()
        {
            startupStateApplied = true;
            pipBeforeFocus = false;
            allowFullscreenMode = false;

            if (!forceOffOnStart && showPipByDefault && !startHidden)
                SetPip(true);
            else
                SetOff();
        }

        public void RecenterToBoresight()
        {
            yaw = 0f;
            pitch = -15f;
            isLocked = false;
            lockedToTarget = false;
            lockedTarget = null;
            status = "boresight";
        }

        public void ToggleLock()
        {
            if (isLocked)
            {
                isLocked = false;
                lockedToTarget = false;
                lockedTarget = null;
                status = "unlock";
                return;
            }

            if (candidateTarget != null && candidateTarget.IsAlive())
            {
                lockedTarget = candidateTarget;
                lockedToTarget = true;
                isLocked = true;
                lockPoint = lockedTarget.transform.position;
                status = "lock_target_" + lockedTarget.displayName;
                return;
            }

            if (hasLookPoint)
            {
                lockedTarget = null;
                lockedToTarget = false;
                isLocked = true;
                lockPoint = lookPoint;
                status = "lock_point";
                return;
            }

            status = "lock_failed";
        }

        public void PushDesignationToCAS()
        {
            if (casTargeting == null)
            {
                status = "no_cas_targeting";
                return;
            }

            if (lockedToTarget && lockedTarget != null && lockedTarget.IsAlive())
            {
                casTargeting.designatedTarget = lockedTarget;
                casTargeting.designatedPoint = lockedTarget.transform.position;
                casTargeting.hasDesignatedPoint = true;
                casTargeting.status = "tgp_designated_" + lockedTarget.displayName;
                status = "pushed_target";
                return;
            }

            if (isLocked || hasLookPoint)
            {
                casTargeting.designatedTarget = null;
                casTargeting.designatedPoint = isLocked ? lockPoint : lookPoint;
                casTargeting.hasDesignatedPoint = true;
                casTargeting.status = "tgp_designated_point";
                status = "pushed_point";
                return;
            }

            status = "push_failed";
        }

        private void UpdateLookDirection()
        {
            if (isLocked)
            {
                Vector3 targetPoint = lockPoint;

                if (lockedToTarget && lockedTarget != null && lockedTarget.IsAlive() && lockFollowsMovingTarget)
                {
                    targetPoint = lockedTarget.transform.position;
                    lockPoint = targetPoint;
                }

                Vector3 dir = targetPoint - GetCameraOrigin();
                if (dir.sqrMagnitude > 1f)
                {
                    Quaternion localLook = Quaternion.Inverse(transform.rotation) * Quaternion.LookRotation(dir.normalized, Vector3.up);
                    Vector3 euler = NormalizeEuler(localLook.eulerAngles);
                    yaw = Mathf.Clamp(euler.y, -maxYaw, maxYaw);
                    pitch = Mathf.Clamp(-euler.x, minPitch, maxPitch);
                }
            }
        }

        private void UpdateRaycastAndCandidate()
        {
            hasLookPoint = false;
            candidateTarget = null;

            Vector3 origin = GetCameraOrigin();
            Vector3 dir = GetLookDirection();

            if (Physics.Raycast(origin, dir, out RaycastHit hit, maxRange, raycastMask, QueryTriggerInteraction.Ignore))
            {
                lookPoint = hit.point;
                slantRange = hit.distance;
                hasLookPoint = true;

                MavCASTarget direct = hit.collider.GetComponentInParent<MavCASTarget>();
                if (direct != null && direct.IsAlive())
                    candidateTarget = direct;
            }
            else
            {
                lookPoint = origin + dir * maxRange;
                slantRange = maxRange;
            }

            if (candidateTarget == null && autoSnapTargetNearReticle && podCamera != null)
                candidateTarget = FindTargetNearPodCenter();

            if (!isLocked)
            {
                if (candidateTarget != null) status = "candidate_" + candidateTarget.displayName;
                else status = hasLookPoint ? "ground_track" : "search";
            }
        }

        private MavCASTarget FindTargetNearPodCenter()
        {
            MavCASTarget[] all = FindObjectsOfType<MavCASTarget>();
            MavCASTarget best = null;
            float bestDist = float.MaxValue;

            foreach (MavCASTarget t in all)
            {
                if (t == null || !t.IsAlive())
                    continue;

                Vector3 vp = podCamera.WorldToViewportPoint(t.transform.position + Vector3.up * 2f);
                if (vp.z <= 0f)
                    continue;

                float d = Vector2.Distance(new Vector2(vp.x, vp.y), new Vector2(0.5f, 0.5f));
                if (d < bestDist && d <= targetSnapRadiusScreen)
                {
                    bestDist = d;
                    best = t;
                }
            }

            return best;
        }

        private void UpdateLock()
        {
            if (!isLocked)
                return;

            if (lockedToTarget && (lockedTarget == null || !lockedTarget.IsAlive()))
            {
                lockedToTarget = false;
                lockedTarget = null;
                status = "target_lost_point_hold";
            }
        }

        private void UpdateCamera()
        {
            if (podCamera == null)
                return;

            podCamera.transform.position = GetCameraOrigin();
            podCamera.transform.rotation = Quaternion.LookRotation(GetLookDirection(), Vector3.up);
            podCamera.fieldOfView = fov;
            podCamera.farClipPlane = maxRange;
        }

        private Vector3 GetCameraOrigin()
        {
            if (podMount != null)
                return podMount.position;

            return transform.position + transform.forward * 3f + -transform.up * 2f;
        }

        private Vector3 GetLookDirection()
        {
            Quaternion local = Quaternion.Euler(-pitch, yaw, 0f);
            return (transform.rotation * local * Vector3.forward).normalized;
        }

        private void ClampDisplayMode()
        {
            if (!allowFullscreenMode && displayMode == MavTargetingPodDisplayMode.Fullscreen)
                displayMode = MavTargetingPodDisplayMode.Off;
        }

        private void ConfigureDefaultPipUi()
        {
            if (!applyDefaultPipAnchorsOnStart || pipRawImage == null)
                return;

            RectTransform rt = pipRawImage.rectTransform;
            if (rt == null)
                return;

            rt.anchorMin = defaultPipAnchorMin;
            rt.anchorMax = defaultPipAnchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private void ApplyDisplayState()
        {
            ClampDisplayMode();

            isPipVisible = displayMode == MavTargetingPodDisplayMode.PictureInPicture;
            isFocusActive = displayMode == MavTargetingPodDisplayMode.Fullscreen;

            if (pipRawImage != null)
            {
                pipRawImage.texture = podTexture;
                pipRawImage.enabled = isPipVisible;
            }

            if (pipCanvasGroup != null)
            {
                pipCanvasGroup.alpha = isPipVisible ? defaultPipAlpha : 0f;
                pipCanvasGroup.interactable = false;
                pipCanvasGroup.blocksRaycasts = false;
            }
        }

        private Vector3 NormalizeEuler(Vector3 e)
        {
            e.x = NormalizeAngle(e.x);
            e.y = NormalizeAngle(e.y);
            e.z = NormalizeAngle(e.z);
            return e;
        }

        private float NormalizeAngle(float a)
        {
            while (a > 180f) a -= 360f;
            while (a < -180f) a += 360f;
            return a;
        }

        private void OnGUI()
        {
            ClampDisplayMode();

            if (displayMode == MavTargetingPodDisplayMode.Off || podTexture == null)
                return;

            InitStyles();

            Rect r = displayMode == MavTargetingPodDisplayMode.Fullscreen
                ? new Rect(0f, 0f, Screen.width, Screen.height)
                : pipRect;

            GUI.color = Color.white;
            GUI.DrawTexture(r, podTexture, ScaleMode.StretchToFill, false);

            if (drawOverlay)
                DrawTGPOverlay(r);

            if (drawDebugText)
                DrawTGPText(r);
        }

        private void InitStyles()
        {
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label);
                labelStyle.normal.textColor = Color.green;
                labelStyle.fontSize = 13;
                labelStyle.fontStyle = FontStyle.Bold;
            }

            if (centerStyle == null)
            {
                centerStyle = new GUIStyle(GUI.skin.label);
                centerStyle.normal.textColor = Color.green;
                centerStyle.alignment = TextAnchor.MiddleCenter;
                centerStyle.fontSize = 16;
                centerStyle.fontStyle = FontStyle.Bold;
            }
        }

        private void DrawTGPOverlay(Rect r)
        {
            Color old = GUI.color;
            GUI.color = Color.green;

            float cx = r.x + r.width * 0.5f;
            float cy = r.y + r.height * 0.5f;

            GUI.Box(new Rect(cx - 28f, cy, 56f, 1.5f), "");
            GUI.Box(new Rect(cx, cy - 28f, 1.5f, 56f), "");
            GUI.Label(new Rect(cx - 12f, cy - 12f, 24f, 24f), isLocked ? "L" : "+", centerStyle);

            if (candidateTarget != null && podCamera != null)
            {
                Vector3 vp = podCamera.WorldToViewportPoint(candidateTarget.transform.position + Vector3.up * 2f);
                if (vp.z > 0f)
                {
                    float x = r.x + vp.x * r.width;
                    float y = r.y + (1f - vp.y) * r.height;
                    GUI.Label(new Rect(x - 20f, y - 15f, 40f, 30f), "TGT", centerStyle);
                }
            }

            GUI.color = old;
        }

        private void DrawTGPText(Rect r)
        {
            string lockText = isLocked
                ? (lockedToTarget && lockedTarget != null ? "LOCK " + lockedTarget.displayName : "POINT LOCK")
                : "SEARCH";

            string text =
                "TGP " + lockText + "\n" +
                "FOV " + fov.ToString("0.0") + "  RNG " + slantRange.ToString("0") + "m\n" +
                "YAW " + yaw.ToString("0") + "  PIT " + pitch.ToString("0") + "\n" +
                "T Toggle | O Mode | IJKL Slew | 5/6 Zoom | R Lock | G Designate\n" +
                status;

            GUI.Label(new Rect(r.x + 8f, r.y + 8f, r.width - 16f, 90f), text, labelStyle);
        }

        private void OnDrawGizmos()
        {
            Vector3 origin = podMount != null ? podMount.position : transform.position;
            Vector3 dir = Application.isPlaying ? GetLookDirection() : transform.forward;

            Gizmos.color = isLocked ? Color.yellow : Color.green;
            Gizmos.DrawRay(origin, dir * 120f);

            if (isLocked || hasLookPoint)
            {
                Gizmos.DrawWireSphere(isLocked ? lockPoint : lookPoint, 10f);
            }
        }
    }
}
