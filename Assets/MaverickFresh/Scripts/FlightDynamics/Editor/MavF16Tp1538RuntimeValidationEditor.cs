using UnityEditor;
using UnityEngine;
using MaverickFresh.FlightDynamics.Validation;

namespace MaverickFresh.FlightDynamics.EditorTools
{
    /// <summary>
    /// Unity batch entry point for the isolated F-16 TP-1538 powered-reference validation.
    /// No scene, prefab, or gameplay ownership is modified.
    /// </summary>
    public static class MavF16Tp1538RuntimeValidationEditor
    {
        [MenuItem("Maverick/Flight Dynamics/Run F-16 TP-1538 Powered Reference Validation")]
        public static void RunFromMenu()
        {
            int passed;
            int failed;
            string report = MavF16Tp1538RuntimeValidation.RunAll(out passed, out failed);

            if (failed == 0)
                Debug.Log(report);
            else
                Debug.LogError(report);

            EditorUtility.DisplayDialog(
                "F-16 TP-1538 Powered Reference Validation",
                failed == 0
                    ? "PASS\n\n" + passed + " checks passed."
                    : "FAIL\n\n" + failed + " checks failed. See Console.",
                "OK");
        }

        /// <summary>Headless entry point for Unity -batchmode -executeMethod.</summary>
        public static void RunBatch()
        {
            int passed;
            int failed;
            string report = MavF16Tp1538RuntimeValidation.RunAll(out passed, out failed);

            Debug.Log(report);
            System.Console.WriteLine(report);

            if (failed != 0)
                EditorApplication.Exit(1);
        }
    }
}
