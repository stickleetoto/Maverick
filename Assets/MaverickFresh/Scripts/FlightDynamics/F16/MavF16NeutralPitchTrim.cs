using UnityEngine;

namespace MaverickFresh.FlightDynamics.F16
{
    /// <summary>
    /// Keeps the legacy instructor's fixed nose-down trim neutral while the authoritative F-16
    /// selection is active. This is intentionally F-16 scoped so other aircraft keep their own
    /// instructor tuning.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MavF16NeutralPitchTrim : MonoBehaviour
    {
        private const float CheckIntervalSeconds = 0.5f;

        private MavF16SelectionBinding binding;
        private global::MaverickFresh.MavInstructorController instructor;
        private float nextCheckTime;

        private void Awake()
        {
            Resolve();
        }

        private void OnEnable()
        {
            nextCheckTime = 0f;
            ApplyIfSelected();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextCheckTime)
                return;

            nextCheckTime = Time.unscaledTime + CheckIntervalSeconds;
            ApplyIfSelected();
        }

        private void Resolve()
        {
            if (binding == null)
                binding = GetComponent<MavF16SelectionBinding>();

            if (instructor == null)
                instructor = GetComponent<global::MaverickFresh.MavInstructorController>();
        }

        private void ApplyIfSelected()
        {
            Resolve();
            if (binding == null || !binding.f16Selected || instructor == null)
                return;

            if (!Mathf.Approximately(instructor.noseDownTrim, 0f))
                instructor.noseDownTrim = 0f;
        }
    }

    public static class MavF16NeutralPitchTrimBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            MavF16SelectionBinding[] bindings = Object.FindObjectsOfType<MavF16SelectionBinding>();
            for (int i = 0; i < bindings.Length; i++)
            {
                MavF16SelectionBinding binding = bindings[i];
                if (binding == null)
                    continue;

                if (binding.GetComponent<MavF16NeutralPitchTrim>() == null)
                    binding.gameObject.AddComponent<MavF16NeutralPitchTrim>();
            }
        }
    }
}
