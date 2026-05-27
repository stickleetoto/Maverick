using System.IO;
using UnityEngine;

namespace EaglePhysicalAI.Data
{
    public class ReplayEventRecorder : MonoBehaviour
    {
        public bool recordEvents = true;
        public string replayId = "replay";
        public string filePath;
        private StreamWriter _writer;

        private void OnEnable()
        {
            if (!recordEvents) return;
            string dir = Path.Combine(Application.persistentDataPath, "EaglePhysicalAILab", "replays");
            Directory.CreateDirectory(dir);
            filePath = Path.Combine(dir, replayId + "_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".jsonl");
            _writer = new StreamWriter(filePath, false);
        }

        private void OnDisable()
        {
            if (_writer == null) return;
            _writer.Flush();
            _writer.Dispose();
            _writer = null;
        }

        public void RecordEvent(string eventType, string message, Vector3 position)
        {
            if (!recordEvents || _writer == null) return;
            var evt = new ReplayEvent
            {
                time = Time.time,
                frame = Time.frameCount,
                eventType = eventType,
                message = message,
                px = position.x,
                py = position.y,
                pz = position.z
            };
            _writer.WriteLine(JsonUtility.ToJson(evt, false));
            _writer.Flush();
        }

        [System.Serializable]
        private class ReplayEvent
        {
            public float time;
            public int frame;
            public string eventType;
            public string message;
            public float px, py, pz;
        }
    }
}
