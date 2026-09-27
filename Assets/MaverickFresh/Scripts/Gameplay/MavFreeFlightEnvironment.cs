using UnityEngine;

namespace MaverickFresh.Gameplay
{
    /// <summary>
    /// R1 free-flight world: a large textured ground, a runway strip under the start, big distant landmarks for
    /// speed and heading reference, light haze for depth. No colliders anywhere - nothing here can touch the
    /// aircraft; ground contact is detected by the flight session instead.
    ///
    /// Built at runtime, once, under one root, so a restart (scene reload) replaces it cleanly.
    /// </summary>
    public static class MavFreeFlightEnvironment
    {
        public const string RootName = "Mav_FreeFlightWorld";
        public const float GroundHeightM = 0f;
        public const float GroundSizeM = 240000f;

        public static GameObject Build()
        {
            GameObject existing = GameObject.Find(RootName);
            if (existing != null)
                return existing;

            GameObject root = new GameObject(RootName);

            Material ground = GroundMaterial();
            GameObject plane = MavGameplayVisuals.Primitive(PrimitiveType.Plane, "Ground", root.transform,
                new Vector3(0f, GroundHeightM, 0f), new Vector3(GroundSizeM / 10f, 1f, GroundSizeM / 10f), ground);
            Renderer groundRenderer = plane.GetComponent<Renderer>();
            if (groundRenderer != null)
                groundRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Material runway = MavGameplayVisuals.NewMaterial(new Color(0.18f, 0.18f, 0.19f), 0.1f);
            MavGameplayVisuals.Primitive(PrimitiveType.Cube, "Runway", root.transform,
                new Vector3(0f, GroundHeightM + 2f, 2000f), new Vector3(60f, 2f, 3500f), runway);

            // Landmarks: deterministic ring of large shapes, 6-60 km out, 400-2,600 m tall.
            Material rock = MavGameplayVisuals.NewMaterial(new Color(0.42f, 0.40f, 0.37f), 0.05f);
            Material snow = MavGameplayVisuals.NewMaterial(new Color(0.86f, 0.88f, 0.90f), 0.2f);
            Material tower = MavGameplayVisuals.NewMaterial(new Color(0.75f, 0.30f, 0.20f), 0.3f);
            System.Random rng = new System.Random(1515);
            for (int i = 0; i < 36; i++)
            {
                float angle = i * (360f / 36f) + (float)rng.NextDouble() * 8f;
                float distance = 6000f + (float)rng.NextDouble() * 54000f;
                Vector3 at = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * distance;
                float height = 400f + (float)rng.NextDouble() * 2200f;
                float width = height * (0.9f + (float)rng.NextDouble() * 1.4f);
                GameObject peak = MavGameplayVisuals.Primitive(PrimitiveType.Cube, "Peak_" + i, root.transform,
                    new Vector3(at.x, GroundHeightM + height * 0.35f, at.z), new Vector3(width, height, width), rock);
                peak.transform.localRotation = Quaternion.Euler(45f, angle, 45f);
                if (height > 1800f)
                {
                    GameObject cap = MavGameplayVisuals.Primitive(PrimitiveType.Cube, "Snow", peak.transform,
                        new Vector3(0f, 0.36f, 0f), new Vector3(0.45f, 0.3f, 0.45f), snow);
                    cap.transform.localRotation = Quaternion.identity;
                }
            }

            // A line of tall towers along the initial heading: an unmistakable direction and closure cue.
            for (int i = 1; i <= 12; i++)
            {
                MavGameplayVisuals.Primitive(PrimitiveType.Cylinder, "Tower_" + i, root.transform,
                    new Vector3(-1500f, GroundHeightM + 300f, i * 5000f), new Vector3(120f, 300f, 120f), tower);
                MavGameplayVisuals.Primitive(PrimitiveType.Cylinder, "Tower_R_" + i, root.transform,
                    new Vector3(1500f, GroundHeightM + 300f, i * 5000f), new Vector3(120f, 300f, 120f), tower);
            }

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.70f, 0.78f, 0.86f);
            RenderSettings.fogStartDistance = 12000f;
            RenderSettings.fogEndDistance = 90000f;
            return root;
        }

        private static Material GroundMaterial()
        {
            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            tex.hideFlags = HideFlags.HideAndDontSave;
            Color a = new Color(0.30f, 0.42f, 0.24f);
            Color b = new Color(0.36f, 0.47f, 0.28f);
            Color line = new Color(0.25f, 0.33f, 0.20f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool checker = ((x / 16) + (y / 16)) % 2 == 0;
                    bool edge = x % 16 == 0 || y % 16 == 0;
                    tex.SetPixel(x, y, edge ? line : (checker ? a : b));
                }
            }

            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Trilinear;
            tex.anisoLevel = 8;
            tex.Apply(true);

            Material m = MavGameplayVisuals.NewMaterial(Color.white, 0.05f);
            // One texture repeat per 2 km: fine enough to read speed and height, coarse enough not to shimmer.
            Vector2 tiling = new Vector2(GroundSizeM / 2000f, GroundSizeM / 2000f);
            if (m.HasProperty("_BaseMap"))
            {
                m.SetTexture("_BaseMap", tex);
                m.SetTextureScale("_BaseMap", tiling);
            }

            m.mainTexture = tex;
            m.mainTextureScale = tiling;
            return m;
        }
    }
}
