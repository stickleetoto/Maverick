using UnityEngine;

namespace MaverickFresh
{
    /// <summary>
    /// Simulator-style air-to-air gun lead sight.
    /// Attach to Mav_Player. It reads the F-22 sensor suite selected target and draws
    /// a predicted gun lead pipper on the HUD. This intentionally avoids FPS-style
    /// hitmarkers/killfeed/damage-direction UI.
    /// </summary>
    [DisallowMultipleComponent]
    public class MavAirToAirLeadSight : MonoBehaviour
    {
        [Header("References")]
        public Camera playerCamera;
        public MavF22SensorSuite sensorSuite;
        public MavCASWeaponSystem gunSystem;
        public Rigidbody ownRigidbody;

        [Header("Behavior")]
        public bool showLeadSight = true;
        public bool requireSensorOn = true;
        public bool requireSelectedTarget = true;
        public bool requireTargetInFront = true;
        public bool useOwnVelocityInheritance = true;
        [Range(0f, 1.25f)] public float ownVelocityInheritanceFactor = 0.75f;
        public float fallbackGunMuzzleSpeed = 1030f;
        public float maxLeadTimeSeconds = 3.6f;
        public float maxDisplayRangeMeters = 2800f;
        public float maxReticleOffscreenMargin = 80f;

        [Header("Reticle")]
        public int circleSegments = 48;
        public float reticleRadiusPixels = 15f;
        public float reticleGapPixels = 6f;
        public float reticleLinePixels = 13f;
        public float reticleLineWidth = 2f;
        public Color goodColor = new Color(0.25f, 1.0f, 0.95f, 0.92f);
        public Color marginalColor = new Color(1.0f, 0.82f, 0.25f, 0.92f);
        public Color noSolutionColor = new Color(1.0f, 0.25f, 0.18f, 0.78f);
        public bool showText = true;
        public Vector2 textOffset = new Vector2(20f, -30f);

        [Header("Debug")]
        public MavRadarSignature debugTarget;
        public Vector3 debugLeadPoint;
        public float debugTimeToImpact;
        public float debugRangeMeters;
        public float debugAspectDeg;
        public float debugSolutionQuality;
        public string debugCue = "NO TARGET";

        private Texture2D whiteTex;
        private GUIStyle textStyle;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
        }

        private void Update()
        {
            if (playerCamera == null || sensorSuite == null || ownRigidbody == null || gunSystem == null)
                ResolveReferences();

            UpdateSolution();
        }

        private void ResolveReferences()
        {
            if (playerCamera == null)
                playerCamera = Camera.main;
            if (sensorSuite == null)
                sensorSuite = GetComponent<MavF22SensorSuite>();
            if (gunSystem == null)
                gunSystem = GetComponent<MavCASWeaponSystem>();
            if (ownRigidbody == null)
                ownRigidbody = GetComponent<Rigidbody>();
        }

        private void UpdateSolution()
        {
            debugTarget = null;
            debugLeadPoint = Vector3.zero;
            debugTimeToImpact = 0f;
            debugRangeMeters = 0f;
            debugAspectDeg = 0f;
            debugSolutionQuality = 0f;
            debugCue = "NO TARGET";

            if (!showLeadSight)
                return;
            if (sensorSuite == null)
            {
                debugCue = "NO SENSOR";
                return;
            }
            if (requireSensorOn && !sensorSuite.useSensorSuite)
            {
                debugCue = "SENSOR OFF";
                return;
            }

            MavRadarSignature target = sensorSuite.selectedTarget;
            if (target == null && !requireSelectedTarget)
                target = FindFallbackTarget();
            if (target == null || !target.CanBeDetected())
            {
                debugCue = "NO TARGET";
                return;
            }

            Vector3 muzzle = GetMuzzleWorldPosition();
            Vector3 toTarget = target.transform.position - muzzle;
            float range = toTarget.magnitude;
            debugRangeMeters = range;
            debugAspectDeg = range > 1f ? Vector3.Angle(transform.forward, toTarget / range) : 0f;

            if (requireTargetInFront && Vector3.Dot(transform.forward, toTarget) <= 0f)
            {
                debugTarget = target;
                debugCue = "TARGET AFT";
                return;
            }
            if (range > maxDisplayRangeMeters)
            {
                debugTarget = target;
                debugCue = "OUT OF GUN RANGE";
                return;
            }

            Vector3 ownVelocity = ownRigidbody != null ? ownRigidbody.linearVelocity : Vector3.zero;
            Rigidbody targetRb = target.GetComponent<Rigidbody>();
            Vector3 targetVelocity = targetRb != null ? targetRb.linearVelocity : EstimateTargetVelocity(target.transform);
            float muzzleSpeed = gunSystem != null ? Mathf.Max(1f, gunSystem.gunMuzzleSpeed) : Mathf.Max(1f, fallbackGunMuzzleSpeed);
            float inherited = useOwnVelocityInheritance ? ownVelocityInheritanceFactor : 0f;

            float t;
            bool solution = SolveInterceptTime(
                target.transform.position - muzzle,
                targetVelocity - ownVelocity * inherited,
                muzzleSpeed,
                out t);

            debugTarget = target;
            if (!solution || t <= 0f || t > maxLeadTimeSeconds)
            {
                debugLeadPoint = target.transform.position;
                debugTimeToImpact = Mathf.Clamp(t, 0f, maxLeadTimeSeconds);
                debugSolutionQuality = 0f;
                debugCue = "NO GUN SOLUTION";
                return;
            }

            debugTimeToImpact = t;
            debugLeadPoint = target.transform.position + targetVelocity * t;
            float timeQ = 1f - Mathf.Clamp01(t / maxLeadTimeSeconds);
            float rangeQ = 1f - Mathf.Clamp01(range / maxDisplayRangeMeters);
            float angleQ = 1f - Mathf.Clamp01(debugAspectDeg / 55f);
            debugSolutionQuality = Mathf.Clamp01(timeQ * 0.36f + rangeQ * 0.34f + angleQ * 0.30f);
            debugCue = debugSolutionQuality > 0.50f ? "LEAD" : "MARGINAL";
        }

        private Vector3 lastFallbackPosition;
        private float lastFallbackTime;
        private Vector3 fallbackVelocity;
        private Transform fallbackVelocityTarget;

        private Vector3 EstimateTargetVelocity(Transform target)
        {
            if (target == null)
                return Vector3.zero;

            if (fallbackVelocityTarget != target)
            {
                fallbackVelocityTarget = target;
                lastFallbackPosition = target.position;
                lastFallbackTime = Time.time;
                fallbackVelocity = Vector3.zero;
                return Vector3.zero;
            }

            float dt = Mathf.Max(0.0001f, Time.time - lastFallbackTime);
            fallbackVelocity = (target.position - lastFallbackPosition) / dt;
            lastFallbackPosition = target.position;
            lastFallbackTime = Time.time;
            return fallbackVelocity;
        }

        private Vector3 GetMuzzleWorldPosition()
        {
            // Keep independent from ordnance assets; this matches the existing fallback gun muzzle convention.
            return transform.position + transform.forward * 11.5f + transform.right * 0.85f - transform.up * 1.1f;
        }

        private MavRadarSignature FindFallbackTarget()
        {
            MavRadarSignature[] all = FindObjectsOfType<MavRadarSignature>();
            MavRadarSignature best = null;
            float bestScore = -999f;
            int ownTeam = sensorSuite != null ? sensorSuite.ownTeam : 0;

            for (int i = 0; i < all.Length; i++)
            {
                MavRadarSignature sig = all[i];
                if (sig == null || sig.transform == transform || sig.transform.IsChildOf(transform) || !sig.CanBeDetected())
                    continue;
                if (sig.team == ownTeam)
                    continue;

                Vector3 to = sig.transform.position - transform.position;
                float dist = to.magnitude;
                if (dist <= 1f || dist > maxDisplayRangeMeters)
                    continue;
                float angle = Vector3.Angle(transform.forward, to / dist);
                float score = -dist * 0.001f - angle * 0.03f;
                if (score > bestScore)
                {
                    best = sig;
                    bestScore = score;
                }
            }

            return best;
        }

        private static bool SolveInterceptTime(Vector3 relativePosition, Vector3 relativeVelocity, float projectileSpeed, out float time)
        {
            float a = Vector3.Dot(relativeVelocity, relativeVelocity) - projectileSpeed * projectileSpeed;
            float b = 2f * Vector3.Dot(relativePosition, relativeVelocity);
            float c = Vector3.Dot(relativePosition, relativePosition);

            if (Mathf.Abs(a) < 0.0001f)
            {
                if (Mathf.Abs(b) < 0.0001f)
                {
                    time = 0f;
                    return false;
                }
                time = -c / b;
                return time > 0f;
            }

            float disc = b * b - 4f * a * c;
            if (disc < 0f)
            {
                time = 0f;
                return false;
            }

            float sqrt = Mathf.Sqrt(disc);
            float t1 = (-b - sqrt) / (2f * a);
            float t2 = (-b + sqrt) / (2f * a);

            time = float.MaxValue;
            if (t1 > 0.02f) time = Mathf.Min(time, t1);
            if (t2 > 0.02f) time = Mathf.Min(time, t2);

            if (time == float.MaxValue)
            {
                time = 0f;
                return false;
            }

            return true;
        }

        private void OnGUI()
        {
            if (!showLeadSight || debugTarget == null || playerCamera == null)
                return;
            if (whiteTex == null)
                whiteTex = Texture2D.whiteTexture;
            if (textStyle == null)
                BuildTextStyle();

            Vector3 sp = playerCamera.WorldToScreenPoint(debugLeadPoint);
            if (sp.z <= 0.05f)
                return;

            Vector2 gui = new Vector2(sp.x, Screen.height - sp.y);
            if (gui.x < -maxReticleOffscreenMargin || gui.x > Screen.width + maxReticleOffscreenMargin ||
                gui.y < -maxReticleOffscreenMargin || gui.y > Screen.height + maxReticleOffscreenMargin)
                return;

            Color color = debugSolutionQuality > 0.50f ? goodColor : (debugSolutionQuality > 0.05f ? marginalColor : noSolutionColor);
            DrawLeadPipper(gui, color);

            if (showText)
            {
                textStyle.normal.textColor = color;
                string label = debugCue + "  " + Mathf.RoundToInt(debugRangeMeters) + "m  " + debugTimeToImpact.ToString("0.0") + "s";
                GUI.Label(new Rect(gui.x + textOffset.x, gui.y + textOffset.y, 260f, 24f), label, textStyle);
            }
        }

        private void BuildTextStyle()
        {
            textStyle = new GUIStyle(GUI.skin.label);
            textStyle.fontSize = 12;
            textStyle.fontStyle = FontStyle.Bold;
            textStyle.alignment = TextAnchor.MiddleLeft;
        }

        private void DrawLeadPipper(Vector2 c, Color color)
        {
            float r = Mathf.Max(4f, reticleRadiusPixels);
            int seg = Mathf.Clamp(circleSegments, 12, 96);
            Vector2 prev = c + new Vector2(Mathf.Cos(0f), Mathf.Sin(0f)) * r;
            for (int i = 1; i <= seg; i++)
            {
                float a = (Mathf.PI * 2f) * i / seg;
                Vector2 next = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                DrawLine(prev, next, color, reticleLineWidth);
                prev = next;
            }

            float gap = Mathf.Max(0f, reticleGapPixels);
            float len = Mathf.Max(2f, reticleLinePixels);
            DrawLine(c + Vector2.left * (gap + len), c + Vector2.left * gap, color, reticleLineWidth);
            DrawLine(c + Vector2.right * gap, c + Vector2.right * (gap + len), color, reticleLineWidth);
            DrawLine(c + Vector2.up * (gap + len), c + Vector2.up * gap, color, reticleLineWidth);
            DrawLine(c + Vector2.down * gap, c + Vector2.down * (gap + len), color, reticleLineWidth);
        }

        private void DrawLine(Vector2 a, Vector2 b, Color color, float width)
        {
            Matrix4x4 matrix = GUI.matrix;
            Color old = GUI.color;
            GUI.color = color;

            Vector2 d = b - a;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            float length = d.magnitude;
            GUIUtility.RotateAroundPivot(angle, a);
            GUI.DrawTexture(new Rect(a.x, a.y - width * 0.5f, length, width), whiteTex);

            GUI.matrix = matrix;
            GUI.color = old;
        }
    }
}
