using UnityEngine;

namespace MaverickFresh.Gameplay
{
    /// <summary>
    /// Placeholder art for R1: simple aircraft shapes and URP-safe materials, shared by the hangar and the flight.
    ///
    /// Every object built here is visual only - colliders are removed, never just disabled, so a model can never
    /// touch physics. Final art replaces this without changing any caller.
    /// </summary>
    public static class MavGameplayVisuals
    {
        /// <summary>A material that renders under URP (the project pipeline), falling back sensibly.</summary>
        public static Material NewMaterial(Color color, float smoothness = 0.35f)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            Material m = new Material(shader);
            m.color = color;
            if (m.HasProperty("_BaseColor"))
                m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Smoothness"))
                m.SetFloat("_Smoothness", smoothness);
            return m;
        }

        public static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            RemoveColliders(go);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
                r.sharedMaterial = material;
            return go;
        }

        public static void RemoveColliders(GameObject root)
        {
            if (root == null)
                return;
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (Application.isPlaying)
                    Object.Destroy(colliders[i]);
                else
                    Object.DestroyImmediate(colliders[i]);
            }
        }

        /// <summary>
        /// A recognisable placeholder aircraft, +Z forward, +Y up, about real length in metres. The F-15 has twin
        /// fins and a wide wing, the F-16 a single fin and a smaller wing.
        /// </summary>
        public static GameObject BuildAircraftModel(MavAircraftKind aircraft, Transform parent)
        {
            GameObject root = new GameObject("Model_" + aircraft);
            root.transform.SetParent(parent, false);

            bool f16 = aircraft == MavAircraftKind.F16C;
            float length = f16 ? 15f : 19.4f;
            float span = f16 ? 9.8f : 13f;
            Color body = f16 ? new Color(0.58f, 0.62f, 0.66f) : new Color(0.50f, 0.55f, 0.60f);
            Material skin = NewMaterial(body, 0.45f);
            Material dark = NewMaterial(Color.Lerp(Color.black, body, 0.35f), 0.3f);
            Material canopy = NewMaterial(new Color(0.12f, 0.16f, 0.22f), 0.9f);

            float half = length * 0.5f;
            Primitive(PrimitiveType.Cube, "fuselage", root.transform, new Vector3(0f, 0f, 0f), new Vector3(f16 ? 1.3f : 2.6f, 1.2f, length * 0.78f), skin);
            GameObject nose = Primitive(PrimitiveType.Capsule, "nose", root.transform, new Vector3(0f, 0.05f, half * 0.72f), new Vector3(1.05f, 2.2f, 1.05f), skin);
            nose.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            GameObject glass = Primitive(PrimitiveType.Sphere, "canopy", root.transform, new Vector3(0f, 0.75f, half * 0.42f), new Vector3(0.85f, 0.7f, 2.6f), canopy);
            glass.transform.localRotation = Quaternion.identity;

            float wingChord = f16 ? 3.6f : 4.8f;
            GameObject leftWing = Primitive(PrimitiveType.Cube, "left_wing", root.transform, new Vector3(-span * 0.26f, -0.1f, -half * 0.08f), new Vector3(span * 0.5f, 0.18f, wingChord), skin);
            leftWing.transform.localRotation = Quaternion.Euler(0f, -18f, 0f);
            GameObject rightWing = Primitive(PrimitiveType.Cube, "right_wing", root.transform, new Vector3(span * 0.26f, -0.1f, -half * 0.08f), new Vector3(span * 0.5f, 0.18f, wingChord), skin);
            rightWing.transform.localRotation = Quaternion.Euler(0f, 18f, 0f);

            float tailSpan = f16 ? 5.6f : 8.6f;
            GameObject leftTail = Primitive(PrimitiveType.Cube, "left_stabilator", root.transform, new Vector3(-tailSpan * 0.26f, 0f, -half * 0.82f), new Vector3(tailSpan * 0.45f, 0.14f, 2.2f), skin);
            leftTail.transform.localRotation = Quaternion.Euler(0f, -12f, 0f);
            GameObject rightTail = Primitive(PrimitiveType.Cube, "right_stabilator", root.transform, new Vector3(tailSpan * 0.26f, 0f, -half * 0.82f), new Vector3(tailSpan * 0.45f, 0.14f, 2.2f), skin);
            rightTail.transform.localRotation = Quaternion.Euler(0f, 12f, 0f);

            if (f16)
            {
                GameObject fin = Primitive(PrimitiveType.Cube, "fin", root.transform, new Vector3(0f, 1.9f, -half * 0.7f), new Vector3(0.2f, 3.2f, 2.6f), dark);
                fin.transform.localRotation = Quaternion.Euler(-22f, 0f, 0f);
                Primitive(PrimitiveType.Cylinder, "nozzle", root.transform, new Vector3(0f, 0f, -half * 0.86f), new Vector3(1.1f, 0.8f, 1.1f), dark)
                    .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            else
            {
                GameObject leftFin = Primitive(PrimitiveType.Cube, "left_fin", root.transform, new Vector3(-1.5f, 1.9f, -half * 0.68f), new Vector3(0.2f, 3.2f, 2.8f), dark);
                leftFin.transform.localRotation = Quaternion.Euler(-18f, 0f, 4f);
                GameObject rightFin = Primitive(PrimitiveType.Cube, "right_fin", root.transform, new Vector3(1.5f, 1.9f, -half * 0.68f), new Vector3(0.2f, 3.2f, 2.8f), dark);
                rightFin.transform.localRotation = Quaternion.Euler(-18f, 0f, -4f);
                Primitive(PrimitiveType.Cylinder, "left_nozzle", root.transform, new Vector3(-0.7f, 0f, -half * 0.86f), new Vector3(1.0f, 0.8f, 1.0f), dark)
                    .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                Primitive(PrimitiveType.Cylinder, "right_nozzle", root.transform, new Vector3(0.7f, 0f, -half * 0.86f), new Vector3(1.0f, 0.8f, 1.0f), dark)
                    .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }

            return root;
        }

        /// <summary>A small flat texture for OnGUI panels.</summary>
        public static Texture2D SolidTexture(Color color)
        {
            Texture2D t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            t.hideFlags = HideFlags.HideAndDontSave;
            Color[] px = { color, color, color, color };
            t.SetPixels(px);
            t.Apply();
            return t;
        }
    }
}
