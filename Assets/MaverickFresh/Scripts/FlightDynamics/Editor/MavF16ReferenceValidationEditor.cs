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

        [MenuItem("Maverick/Flight Dynamics/Run Phase 1 Live-FDM Validation")]
        public static void RunPhase1Validation()
        {
            int passed;
            int failed;
            string report = MavFlightDynamicsPhase1Validation.RunAll(out passed, out failed);

            if (failed == 0)
                Debug.Log(report);
            else
                Debug.LogError(report);

            EditorUtility.DisplayDialog(
                "Phase 1 Live-FDM Validation",
                failed == 0
                    ? "PASS\n\n" + passed + " checks passed."
                    : "FAIL\n\n" + failed + " checks failed. See Console for details.",
                "OK"
            );
        }

        [MenuItem("Maverick/Flight Dynamics/Run All Flight Dynamics Validation")]
        public static void RunAllValidation()
        {
            int referencePassed;
            int referenceFailed;
            string referenceReport = MavF16ReferenceValidation.RunAll(
                out referencePassed,
                out referenceFailed
            );

            int phase1Passed;
            int phase1Failed;
            string phase1Report = MavFlightDynamicsPhase1Validation.RunAll(
                out phase1Passed,
                out phase1Failed
            );

            int passed = referencePassed + phase1Passed;
            int failed = referenceFailed + phase1Failed;
            string report = referenceReport + "\n\n" + phase1Report;

            if (failed == 0)
                Debug.Log(report);
            else
                Debug.LogError(report);

            EditorUtility.DisplayDialog(
                "Maverick Flight Dynamics Validation",
                failed == 0
                    ? "PASS\n\n" + passed + " checks passed."
                    : "FAIL\n\n" + failed + " checks failed. See Console for details.",
                "OK"
            );
        }
    }
}
#endif
