#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using MaverickFresh.FlightDynamics.Validation;

namespace MaverickFresh.FlightDynamics.EditorTools
{
    public static class MavF16ReferenceValidationEditor
    {
        [MenuItem("Maverick/Flight Dynamics/Run F-16 Reference Validation")]
        public static void RunValidation()
        {
            int passed;
            int failed;
            string report = MavF16ReferenceValidation.RunAll(out passed, out failed);

            if (failed == 0)
                Debug.Log(report);
            else
                Debug.LogError(report);

            EditorUtility.DisplayDialog(
                "F-16 Reference Validation",
                failed == 0
                    ? "PASS\n\n" + passed + " checks passed."
                    : "FAIL\n\n" + failed + " checks failed. See Console for details.",
                "OK"
            );
        }
    }
}
#endif
