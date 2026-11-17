using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>
    /// Chase camera and on-screen markers for the realistic mouse instructor v2. RENDER ONLY: it moves its own camera
    /// Transform (never the aircraft), reads the mouse-aim source and the six-DoF body, and draws three markers so the
    /// difference between them stays visible:
    ///   [ ]  the mouse AIM direction (the instructor's target - an intent, not a promise)
    ///   -o-  the flight path (velocity vector) - what the guidance steers by default
    ///    +   the nose (body +X)
    /// The camera looks along the aim (plus the free-look offset while the free-look key is held). It registers itself
    /// with the source, which treats a missing / disabled camera as mouse input loss (its declared policy).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class MavMouseAimCameraRig : MonoBehaviour
    {
        public MavMouseAimPilotCommandSource source;
        public Camera viewCamera;

        [Header("Chase geometry (presentation only)")]
        public float distanceM = 34f;
        public float heightM = 5f;
        [Min(0f)] public float positionSharpness = 14f;
        [Min(0f)] public float rotationSharpness = 12f;
        public float markerDistanceM = 1500f;

        [Header("Overlay")]
        public bool showOverlay = true;
        public KeyCode overlayKey = KeyCode.F3;

        private GUIStyle textStyle;
        private GUIStyle markerStyle;
        private bool placed;

        public bool HasLiveCamera
        {
            get { return viewCamera != null && viewCamera.isActiveAndEnabled; }
        }

        private void OnEnable()
        {
            if (viewCamera == null)
                viewCamera = GetComponent<Camera>();
            if (source == null)
                source = Object.FindFirstObjectByType<MavMouseAimPilotCommandSource>();
            if (source != null)
                source.boundCamera = this;
            placed = false;
        }

        private void OnDisable()
        {
            if (source != null && source.boundCamera == this)
                source.boundCamera = null;
        }

        private void Update()
        {
            if (MavFreshInput.GetKeyDown(overlayKey))
                showOverlay = !showOverlay;
        }

        private void LateUpdate()
        {
            if (source == null || source.aircraftBody == null)
                return;

            Vector3 look = source.ViewDirectionWorld;
            if (!MavMouseAimGuidance.IsFinite(look) || look.sqrMagnitude < 1e-8f)
                return;
            look.Normalize();

            Vector3 pivot = source.aircraftBody.position;
            Vector3 wantedPosition = pivot - look * distanceM + Vector3.up * heightM;
            Quaternion wantedRotation = Quaternion.LookRotation(look, Vector3.up);

            Vector3 p;
            Quaternion r;
            if (!placed)
            {
                p = wantedPosition;
                r = wantedRotation;
                placed = true;
            }
            else
            {
                float dt = Time.unscaledDeltaTime;
                p = Vector3.Lerp(transform.position, wantedPosition, 1f - Mathf.Exp(-positionSharpness * dt));
                r = Quaternion.Slerp(transform.rotation, wantedRotation, 1f - Mathf.Exp(-rotationSharpness * dt));
            }

            transform.SetPositionAndRotation(p, r);
        }

        private void OnGUI()
        {
            if (source == null || viewCamera == null)
                return;

            if (markerStyle == null)
            {
                markerStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18, fontStyle = FontStyle.Bold };
                textStyle = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 12, wordWrap = true };
            }

            if (source.aircraftBody != null)
            {
                Vector3 pivot = source.aircraftBody.position;
                Marker(pivot + source.AimDirectionWorld.normalized * markerDistanceM, "[   ]", new Color(1f, 0.85f, 0.2f));
                Vector3 path = source.aircraftBody.linearVelocity;
                if (path.sqrMagnitude > 1f)
                    Marker(pivot + path.normalized * markerDistanceM, "-o-", new Color(0.3f, 1f, 0.4f));
                Marker(pivot + source.aircraftBody.rotation * Vector3.forward * markerDistanceM, "+", Color.white);
            }

            if (!showOverlay)
                return;

            string text = source.FormatTelemetry()
                + "keys: mouse = aim, hold C = free look, X = hands off (neutral stick), Esc = release cursor, click = capture, F3 overlay\n"
                + "markers: [ ] aim target   -o- flight path   + nose";
            float width = 560f;
            GUI.Box(new Rect(Screen.width - width - 10f, 10f, width, 330f), text, textStyle);
        }

        private void Marker(Vector3 world, string glyph, Color color)
        {
            Vector3 sp = viewCamera.WorldToScreenPoint(world);
            if (sp.z <= 0f)
                return;
            Color previous = GUI.color;
            GUI.color = color;
            GUI.Label(new Rect(sp.x - 30f, Screen.height - sp.y - 15f, 60f, 30f), glyph, markerStyle);
            GUI.color = previous;
        }
    }
}
