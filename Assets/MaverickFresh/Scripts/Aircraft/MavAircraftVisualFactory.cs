using UnityEngine;

namespace MaverickFresh
{
    public static class MavAircraftVisualFactory
    {
        public static GameObject CreateDisplayVisual(MavAircraftRuntimeProfile profile, Transform parent, GameObject overridePrefab, bool hangarDisplay)
        {
            if (profile == null)
                profile = MavAircraftCatalog.GetBuiltIn(MavAircraftKind.F22A);

            GameObject root = new GameObject(profile.aircraftId + "_visual");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.Euler(profile.hangarRotationEuler);
            root.transform.localScale = Vector3.one * (hangarDisplay ? profile.hangarScale : 1f);

            GameObject prefab = overridePrefab != null ? overridePrefab : profile.modelPrefab;
            if (prefab != null)
            {
                GameObject model = Object.Instantiate(prefab, root.transform);
                model.name = profile.aircraftId + "_model_prefab";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one;
                DisableChildColliders(model);
                return root;
            }

            BuildPlaceholder(profile, root.transform);
            return root;
        }

        public static GameObject CreateFlightAircraftRoot(MavAircraftRuntimeProfile profile, GameObject overridePrefab)
        {
            if (profile == null)
                profile = MavAircraftCatalog.GetBuiltIn(MavAircraftKind.F22A);

            GameObject aircraft = new GameObject(profile.aircraftId.ToUpperInvariant() + "_Player");
            aircraft.transform.position = new Vector3(0f, profile.startAltitude, 0f);
            aircraft.transform.rotation = Quaternion.identity;

            GameObject visual = CreateDisplayVisual(profile, aircraft.transform, overridePrefab, false);
            visual.name = "Visual_" + profile.shortName;
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            return aircraft;
        }

        private static void BuildPlaceholder(MavAircraftRuntimeProfile p, Transform root)
        {
            Material mat = NewMaterial(ColorFor(p.aircraft));
            Material dark = NewMaterial(Color.Lerp(Color.black, ColorFor(p.aircraft), 0.45f));

            // +Z forward, +Y up, +X right.
            GameObject fuselage = Cube("fuselage", root, new Vector3(0f, 0f, 0.25f), new Vector3(1.05f, 0.55f, 4.8f), mat);
            GameObject nose = Cube("nose", root, new Vector3(0f, 0.02f, 2.95f), new Vector3(0.72f, 0.42f, 1.25f), mat);
            nose.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            float wingSpan = 5.3f;
            float wingChord = 1.15f;
            if (p.aircraft == MavAircraftKind.F16C) { wingSpan = 4.6f; wingChord = 0.95f; }
            if (p.aircraft == MavAircraftKind.F15E) { wingSpan = 5.8f; wingChord = 1.25f; }
            if (p.aircraft == MavAircraftKind.FA18E) { wingSpan = 5.2f; wingChord = 1.30f; }
            if (p.aircraft == MavAircraftKind.F35A) { wingSpan = 4.9f; wingChord = 1.15f; }

            GameObject leftWing = Cube("left_wing", root, new Vector3(-1.95f, -0.02f, 0.4f), new Vector3(wingSpan * 0.5f, 0.10f, wingChord), mat);
            leftWing.transform.localRotation = Quaternion.Euler(0f, -10f, 0f);
            GameObject rightWing = Cube("right_wing", root, new Vector3(1.95f, -0.02f, 0.4f), new Vector3(wingSpan * 0.5f, 0.10f, wingChord), mat);
            rightWing.transform.localRotation = Quaternion.Euler(0f, 10f, 0f);

            GameObject leftTail = Cube("left_tailplane", root, new Vector3(-1.15f, 0.05f, -1.85f), new Vector3(1.55f, 0.09f, 0.7f), mat);
            leftTail.transform.localRotation = Quaternion.Euler(0f, 15f, 0f);
            GameObject rightTail = Cube("right_tailplane", root, new Vector3(1.15f, 0.05f, -1.85f), new Vector3(1.55f, 0.09f, 0.7f), mat);
            rightTail.transform.localRotation = Quaternion.Euler(0f, -15f, 0f);

            if (p.aircraft == MavAircraftKind.F16C)
            {
                GameObject tail = Cube("single_tail", root, new Vector3(0f, 0.82f, -1.55f), new Vector3(0.16f, 1.45f, 0.75f), dark);
                tail.transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);
            }
            else
            {
                GameObject leftV = Cube("left_vertical_tail", root, new Vector3(-0.62f, 0.75f, -1.48f), new Vector3(0.16f, 1.25f, 0.7f), dark);
                leftV.transform.localRotation = Quaternion.Euler(-17f, 0f, 18f);
                GameObject rightV = Cube("right_vertical_tail", root, new Vector3(0.62f, 0.75f, -1.48f), new Vector3(0.16f, 1.25f, 0.7f), dark);
                rightV.transform.localRotation = Quaternion.Euler(-17f, 0f, -18f);
            }

            if (p.aircraft == MavAircraftKind.F22A || p.aircraft == MavAircraftKind.F35A)
            {
                Cube("left_weapon_bay_hint", root, new Vector3(-0.32f, -0.31f, 0.15f), new Vector3(0.45f, 0.04f, 1.45f), dark);
                Cube("right_weapon_bay_hint", root, new Vector3(0.32f, -0.31f, 0.15f), new Vector3(0.45f, 0.04f, 1.45f), dark);
            }

            Cube("engine_left", root, new Vector3(-0.34f, -0.02f, -2.22f), new Vector3(0.35f, 0.35f, 0.35f), dark);
            Cube("engine_right", root, new Vector3(0.34f, -0.02f, -2.22f), new Vector3(0.35f, 0.35f, 0.35f), dark);

            GameObject label = new GameObject("label_anchor_" + p.shortName);
            label.transform.SetParent(root, false);
            label.transform.localPosition = new Vector3(0f, 1.55f, 0f);

            DisableChildColliders(root.gameObject);
        }

        private static GameObject Cube(string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            Renderer r = go.GetComponent<Renderer>();
            if (r != null) r.material = mat;
            return go;
        }

        private static Material NewMaterial(Color color)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            Material m = shader != null ? new Material(shader) : new Material(Shader.Find("Hidden/InternalErrorShader"));
            m.color = color;
            return m;
        }

        private static Color ColorFor(MavAircraftKind kind)
        {
            switch (kind)
            {
                case MavAircraftKind.F15E: return new Color(0.46f, 0.50f, 0.53f);
                case MavAircraftKind.F16C: return new Color(0.62f, 0.66f, 0.70f);
                case MavAircraftKind.FA18E: return new Color(0.54f, 0.58f, 0.62f);
                case MavAircraftKind.F22A: return new Color(0.67f, 0.72f, 0.76f);
                case MavAircraftKind.F35A: return new Color(0.38f, 0.42f, 0.46f);
                default: return Color.gray;
            }
        }

        private static void DisableChildColliders(GameObject root)
        {
            if (root == null) return;
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = false;
        }
    }
}
