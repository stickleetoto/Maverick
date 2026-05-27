using UnityEngine;
using EaglePhysicalAI.Sensors.TargetingPod;

namespace EaglePhysicalAI.Sensors.UI
{
    public class SensorCrosshairOverlay : MonoBehaviour
    {
        public TargetingPodSystem pod;
        public bool show = true;
        public Color crosshairColor = Color.green;
        public Color lockColor = Color.red;
        public float size = 26f;

        private Texture2D _pixel;

        private void Awake()
        {
            if (pod == null) pod = FindObjectOfType<TargetingPodSystem>();
            _pixel = new Texture2D(1, 1);
            _pixel.SetPixel(0, 0, Color.white);
            _pixel.Apply();
        }

        private void OnGUI()
        {
            if (!show || pod == null || pod.podCamera == null) return;
            Vector3 screen = pod.podCamera.WorldToScreenPoint(pod.lookPoint);
            if (screen.z < 0f) return;
            screen.y = Screen.height - screen.y;

            Color color = pod.targetTrackQuality > 0.65f ? lockColor : crosshairColor;
            DrawLine(new Vector2(screen.x - size, screen.y), new Vector2(screen.x - 5f, screen.y), color);
            DrawLine(new Vector2(screen.x + 5f, screen.y), new Vector2(screen.x + size, screen.y), color);
            DrawLine(new Vector2(screen.x, screen.y - size), new Vector2(screen.x, screen.y - 5f), color);
            DrawLine(new Vector2(screen.x, screen.y + 5f), new Vector2(screen.x, screen.y + size), color);
        }

        private void DrawLine(Vector2 a, Vector2 b, Color color)
        {
            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            GUI.color = color;
            float angle = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
            float length = Vector2.Distance(a, b);
            GUIUtility.RotateAroundPivot(angle, a);
            GUI.DrawTexture(new Rect(a.x, a.y - 1f, length, 2f), _pixel);
            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
        }
    }
}
