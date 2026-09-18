#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MaverickFresh.FlightDynamics.EditorTools
{
    /// <summary>
    /// Lightweight inspector for MavSixDoFBody. The body publishes a large amount of per-physics-step
    /// diagnostic state; drawing every nested field while the aircraft is maneuvering can make the
    /// Unity Editor noticeably more expensive than the player/runtime path.
    /// </summary>
    [CustomEditor(typeof(MavSixDoFBody), true)]
    public sealed class MavSixDoFBodyInspector : Editor
    {
        private bool showFullInspector;

        public override void OnInspectorGUI()
        {
            MavSixDoFBody body = (MavSixDoFBody)target;

            EditorGUILayout.HelpBox(
                "Lightweight runtime view. Full per-step debug fields are hidden by default to avoid Inspector repaint cost while maneuvering.",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Toggle("Simulation Enabled", body.simulationEnabled);
                EditorGUILayout.Toggle("Profile Valid", body.debugProfileValid);
                EditorGUILayout.Toggle("Inside Envelope", body.debugInsideProfileEnvelope);
                EditorGUILayout.IntField("Physics Step", body.debugPhysicsStepIndex);
                EditorGUILayout.IntField("Shadow Steps", body.debugShadowComputeSteps);
                EditorGUILayout.Toggle("Shadow Load Finite", body.debugShadowLoadSetFinite);
                EditorGUILayout.IntField("Load Applications", body.debugLoadApplications);
                EditorGUILayout.TextField("Legacy Physics Owner", body.debugLegacyPhysicsOwner ?? "none");
                EditorGUILayout.TextField("Readiness", body.debugReadinessReason ?? "not evaluated");
            }

            EditorGUILayout.Space();
            showFullInspector = EditorGUILayout.Foldout(showFullInspector, "Show full MavSixDoFBody inspector", true);
            if (showFullInspector)
                DrawDefaultInspector();
        }
    }
}
#endif
