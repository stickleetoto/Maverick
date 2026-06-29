using UnityEngine;

namespace MaverickFresh
{
    /// <summary>
    /// Self-cleaning neon line tracer used by the fast gun pass.
    /// v0.22.6 adds a brighter core/glow look without requiring scene materials.
    /// </summary>
    [DisallowMultipleComponent]
    public class MavNeonTracer : MonoBehaviour
    {
        public LineRenderer line;
        public LineRenderer glowLine;
        public float life = 0.045f;
        public float startWidth = 0.045f;
        public float endWidth = 0.012f;
        public Color startColor = new Color(0.15f, 0.95f, 1f, 1f);
        public Color endColor = new Color(1f, 0.35f, 0.08f, 0.25f);
        public bool useGlow = true;
        public float glowWidthMultiplier = 2.8f;
        public float pulse = 0f;

        private float spawnTime;
        private Material coreMaterial;
        private Material glowMaterial;

        public static MavNeonTracer Spawn(Vector3 start, Vector3 end, float duration, float width, Color color)
        {
            return SpawnAdvanced(start, end, duration, width, color, 0f);
        }

        public static MavNeonTracer SpawnAdvanced(Vector3 start, Vector3 end, float duration, float width, Color color, float pulse01)
        {
            GameObject go = new GameObject("Mav_Neon_Gun_Tracer");
            MavNeonTracer tracer = go.AddComponent<MavNeonTracer>();
            tracer.life = Mathf.Max(0.005f, duration);
            tracer.startWidth = Mathf.Max(0.002f, width);
            tracer.endWidth = Mathf.Max(0.001f, width * 0.22f);
            tracer.pulse = Mathf.Clamp01(pulse01);

            float hot = Mathf.Clamp01(pulse01);
            Color hotColor = Color.Lerp(color, new Color(1f, 0.55f, 0.12f, color.a), hot * 0.55f);
            tracer.startColor = hotColor;
            tracer.endColor = new Color(hotColor.r, hotColor.g, hotColor.b, 0f);
            tracer.Initialize(start, end);
            return tracer;
        }

        public void Initialize(Vector3 start, Vector3 end)
        {
            spawnTime = Time.time;

            line = gameObject.AddComponent<LineRenderer>();
            ConfigureLine(line, start, end, startWidth, endWidth, startColor, endColor, GetCoreMaterial());

            if (useGlow)
            {
                GameObject glow = new GameObject("Glow");
                glow.transform.SetParent(transform, false);
                glowLine = glow.AddComponent<LineRenderer>();
                Color glowStart = new Color(startColor.r, startColor.g, startColor.b, startColor.a * 0.32f);
                Color glowEnd = new Color(endColor.r, endColor.g, endColor.b, 0f);
                ConfigureLine(glowLine, start, end, startWidth * glowWidthMultiplier, endWidth * glowWidthMultiplier, glowStart, glowEnd, GetGlowMaterial());
            }
        }

        private void ConfigureLine(LineRenderer lr, Vector3 start, Vector3 end, float sw, float ew, Color sc, Color ec, Material mat)
        {
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.SetPosition(0, start);
            lr.SetPosition(1, end);
            lr.startWidth = sw;
            lr.endWidth = ew;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 2;
            lr.material = mat;
            lr.startColor = sc;
            lr.endColor = ec;
        }

        private void Update()
        {
            float t = Mathf.Clamp01((Time.time - spawnTime) / Mathf.Max(0.001f, life));
            float fade = 1f - t;
            ApplyFade(line, startWidth, endWidth, startColor, endColor, fade);
            if (glowLine != null)
            {
                Color gs = new Color(startColor.r, startColor.g, startColor.b, startColor.a * 0.32f);
                Color ge = new Color(endColor.r, endColor.g, endColor.b, 0f);
                ApplyFade(glowLine, startWidth * glowWidthMultiplier, endWidth * glowWidthMultiplier, gs, ge, fade);
            }

            if (t >= 1f)
                Destroy(gameObject);
        }

        private void ApplyFade(LineRenderer lr, float sw, float ew, Color sc, Color ec, float fade)
        {
            if (lr == null)
                return;
            sc.a *= fade;
            ec.a *= fade;
            lr.startColor = sc;
            lr.endColor = ec;
            lr.startWidth = Mathf.Lerp(sw, 0f, 1f - fade);
            lr.endWidth = Mathf.Lerp(ew, 0f, 1f - fade);
        }

        private Material GetCoreMaterial()
        {
            if (coreMaterial != null)
                return coreMaterial;
            coreMaterial = new Material(FindTracerShader());
            coreMaterial.color = startColor;
            return coreMaterial;
        }

        private Material GetGlowMaterial()
        {
            if (glowMaterial != null)
                return glowMaterial;
            glowMaterial = new Material(FindTracerShader());
            glowMaterial.color = new Color(startColor.r, startColor.g, startColor.b, startColor.a * 0.35f);
            return glowMaterial;
        }

        private Shader FindTracerShader()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Standard");
            return shader;
        }
    }
}
