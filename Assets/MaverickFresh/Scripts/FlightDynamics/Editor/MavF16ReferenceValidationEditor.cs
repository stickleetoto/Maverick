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

        [MenuItem("Maverick/Flight Dynamics/Run F-16 Propulsion Validation")]
        public static void RunPropulsionValidation()
        {
            int passed;
            int failed;
            string report = MavF16PropulsionValidation.RunAll(out passed, out failed);

            if (failed == 0)
                Debug.Log(report);
            else
                Debug.LogError(report);

            EditorUtility.DisplayDialog(
                "F-16 Propulsion Validation",
                failed == 0
                    ? "PASS\n\n" + passed + " checks passed."
                    : "FAIL\n\n" + failed + " checks failed. See Console for details.",
                "OK"
            );
        }

        [MenuItem("Maverick/Flight Dynamics/Run Phase 2 Trim and Control Validation")]
        public static void RunPhase2Validation()
        {
            int passed;
            int failed;
            string report = MavFlightDynamicsPhase2Validation.RunAll(out passed, out failed);

            if (failed == 0)
                Debug.Log(report);
            else
                Debug.LogError(report);

            EditorUtility.DisplayDialog(
                "Phase 2 Trim / Control Validation",
                failed == 0
                    ? "PASS\n\n" + passed + " checks passed."
                    : "FAIL\n\n" + failed + " checks failed. See Console for details.",
                "OK"
            );
        }

        [MenuItem("Maverick/Flight Dynamics/Run Phase 3 Propulsion, Attitude and Ownership Validation")]
        public static void RunPhase3Validation()
        {
            int passed;
            int failed;
            string report = MavFlightDynamicsPhase3Validation.RunAll(out passed, out failed);

            if (failed == 0)
                Debug.Log(report);
            else
                Debug.LogError(report);

            EditorUtility.DisplayDialog(
                "Phase 3 Validation",
                failed == 0
                    ? "PASS\n\n" + passed + " checks passed."
                    : "FAIL\n\n" + failed + " checks failed. See Console for details.",
                "OK"
            );
        }

        /// <summary>
        /// Prints the F-16 trim survey. This computes and reports only: no scene object is touched,
        /// no Rigidbody is read or written, and no engine state is advanced.
        /// </summary>
        [MenuItem("Maverick/Flight Dynamics/Report F-16 Trim Survey")]
        public static void ReportF16TrimSurvey()
        {
            Debug.Log(MaverickFresh.FlightDynamics.F16.MavF16TrimReference.BuildTrimSurvey());

            EditorUtility.DisplayDialog(
                "F-16 Trim Survey",
                "Trim survey written to the Console.\n\n"
                + "Powered straight-and-level is expected to report ConvergedButThrustUnavailable "
                + "while the F-16 thrust deck is not frozen.",
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

            int propulsionPassed;
            int propulsionFailed;
            string propulsionReport = MavF16PropulsionValidation.RunAll(
                out propulsionPassed,
                out propulsionFailed
            );

            int phase2Passed;
            int phase2Failed;
            string phase2Report = MavFlightDynamicsPhase2Validation.RunAll(
                out phase2Passed,
                out phase2Failed
            );

            int phase3Passed;
            int phase3Failed;
            string phase3Report = MavFlightDynamicsPhase3Validation.RunAll(
                out phase3Passed,
                out phase3Failed
            );

            int passed = referencePassed + phase1Passed + propulsionPassed
                         + phase2Passed + phase3Passed;
            int failed = referenceFailed + phase1Failed + propulsionFailed
                         + phase2Failed + phase3Failed;
            string report = referenceReport + "\n\n" + phase1Report
                            + "\n\n" + propulsionReport
                            + "\n\n" + phase2Report
                            + "\n\n" + phase3Report;

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
