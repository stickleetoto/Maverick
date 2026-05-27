using UnityEngine;

namespace EaglePhysicalAI.PhysicalAI
{
    /// <summary>
    /// Small tester UI helper for labeling demonstrations as good/bad/crash/friendly-fire/etc.
    /// The tag is written into future PhysicalAIDemonstrationRecorder samples.
    /// </summary>
    public class RunTagger : MonoBehaviour
    {
        public PhysicalAIDemonstrationRecorder recorder;
        public string currentTag = "untagged";

        public KeyCode goodKey = KeyCode.Alpha1;
        public KeyCode badKey = KeyCode.Alpha2;
        public KeyCode crashKey = KeyCode.Alpha3;
        public KeyCode friendlyFireKey = KeyCode.Alpha4;
        public KeyCode interestingKey = KeyCode.Alpha5;

        private void Awake()
        {
            if (recorder == null) recorder = GetComponent<PhysicalAIDemonstrationRecorder>();
        }

        private void Update()
        {
            if (MaverickInput.GetKeyDown(goodKey)) SetTag("good_run");
            if (MaverickInput.GetKeyDown(badKey)) SetTag("bad_run");
            if (MaverickInput.GetKeyDown(crashKey)) SetTag("crash_run");
            if (MaverickInput.GetKeyDown(friendlyFireKey)) SetTag("friendly_fire_risk");
            if (MaverickInput.GetKeyDown(interestingKey)) SetTag("interesting_run");
        }

        public void SetTag(string tag)
        {
            currentTag = string.IsNullOrWhiteSpace(tag) ? "untagged" : tag;
            if (recorder != null) recorder.SetRunTag(currentTag);
        }
    }
}
