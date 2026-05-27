using UnityEngine;
using EaglePhysicalAI.Sensors.Radar;

namespace EaglePhysicalAI.Sensors.UI
{
    public class RadarHud : MonoBehaviour
    {
        public F15ERadarSystem radar;
        public bool show = true;
        public Vector2 position = new Vector2(12, 260);
        public Vector2 size = new Vector2(420, 260);
        public int maxRows = 8;

        private void Awake()
        {
            if (radar == null) radar = FindObjectOfType<F15ERadarSystem>();
        }

        private void OnGUI()
        {
            if (!show || radar == null) return;
            GUILayout.BeginArea(new Rect(position.x, position.y, size.x, size.y), GUI.skin.box);
            GUILayout.Label("F-15E-Style Radar / Abstract");
            GUILayout.Label("Mode: " + radar.mode + " | Tracks: " + radar.tracks.Count + " | R:Mode T:Next O:Lock U:Unlock");
            GUILayout.Label("Selected: " + NameOf(radar.selectedTrack) + " | Locked: " + NameOf(radar.lockedTrack));
            GUILayout.Space(4);

            int rows = 0;
            foreach (var track in radar.tracks)
            {
                if (track == null) continue;
                string prefix = track.isLocked ? "[LOCK]" : (track.isSelected ? "[SEL]" : "     ");
                string line = string.Format(
                    "{0} {1,-14} {2,7:0}m brg {3,5:0} q {4:0.00} id {5:0.00} {6}",
                    prefix,
                    Short(track.displayName, 14),
                    track.rangeMeters,
                    track.bearingDegrees,
                    track.trackQuality,
                    track.identificationConfidence,
                    track.team);
                GUILayout.Label(line);
                rows++;
                if (rows >= maxRows) break;
            }

            GUILayout.EndArea();
        }

        private static string NameOf(SensorContact c)
        {
            return c == null ? "none" : c.displayName;
        }

        private static string Short(string value, int max)
        {
            if (string.IsNullOrEmpty(value)) return "none";
            return value.Length <= max ? value : value.Substring(0, max - 1) + "…";
        }
    }
}
