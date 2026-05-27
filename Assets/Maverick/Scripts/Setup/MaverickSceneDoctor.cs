using UnityEngine;
using EaglePhysicalAI.Aircraft;
using EaglePhysicalAI.Controls;
using EaglePhysicalAI.CAS;
using EaglePhysicalAI.PhysicalAI;

namespace EaglePhysicalAI.Setup
{
    public class MaverickSceneDoctor : MonoBehaviour
    {
        public GameObject aircraftObject;
        public Camera mainCamera;
        public bool autoFixOnStart = true;
        public bool disableConflictingInputs = true;
        public bool disableBadCameras = true;
        public bool printReport = true;

        [TextArea(4, 12)] public string lastReport;

        private void Start()
        {
            if (autoFixOnStart) RunDiagnosisAndFix();
        }

        [ContextMenu("Run Diagnosis And Fix")]
        public void RunDiagnosisAndFix()
        {
            if (aircraftObject == null) aircraftObject = GameObject.Find("F15E_Player");
            if (mainCamera == null) mainCamera = Camera.main;

            int issues = 0;
            lastReport = "MAVERICK Scene Doctor\n";

            if (aircraftObject == null)
            {
                lastReport += "- ERROR: F15E_Player not assigned/found.\n";
                issues++;
                if (printReport) Debug.LogWarning(lastReport);
                return;
            }

            if (aircraftObject.GetComponent<Rigidbody>() == null)
            {
                aircraftObject.AddComponent<Rigidbody>();
                lastReport += "- Added Rigidbody.\n";
                issues++;
            }

            if (aircraftObject.GetComponent<AircraftPhysicsController>() == null)
            {
                aircraftObject.AddComponent<AircraftPhysicsController>();
                lastReport += "- Added AircraftPhysicsController.\n";
                issues++;
            }

            if (aircraftObject.GetComponent<MaverickAimDirectorV10>() == null)
            {
                aircraftObject.AddComponent<MaverickAimDirectorV10>();
                lastReport += "- Added MaverickAimDirectorV10.\n";
                issues++;
            }

            if (aircraftObject.GetComponent<MaverickInstructorV10>() == null)
            {
                aircraftObject.AddComponent<MaverickInstructorV10>();
                lastReport += "- Added MaverickInstructorV10.\n";
                issues++;
            }

            if (disableConflictingInputs)
            {
                foreach (var old in aircraftObject.GetComponents<WarThunderMouseAircraftInput>())
                {
                    if (old.enabled)
                    {
                        old.enabled = false;
                        lastReport += "- Disabled old WarThunderMouseAircraftInput.\n";
                        issues++;
                    }
                }

                foreach (var old in aircraftObject.GetComponents<MaverickInstructor>())
                {
                    if (old.enabled)
                    {
                        old.enabled = false;
                        lastReport += "- Disabled old MaverickInstructor v0.9.\n";
                        issues++;
                    }
                }
            }

            var ai = aircraftObject.GetComponent<PhysicalAIRuntimeAgent>();
            var inst = aircraftObject.GetComponent<MaverickInstructorV10>();
            if (ai != null && inst != null && ai.manualInput != inst)
            {
                ai.manualInput = inst;
                lastReport += "- Routed PhysicalAIRuntimeAgent.manualInput to MaverickInstructorV10.\n";
                issues++;
            }

            if (mainCamera != null)
            {
                var stable = mainCamera.GetComponent<MaverickStableChaseCameraV10>();
                if (stable == null)
                {
                    stable = mainCamera.gameObject.AddComponent<MaverickStableChaseCameraV10>();
                    lastReport += "- Added MaverickStableChaseCameraV10 to Main Camera.\n";
                    issues++;
                }
                stable.target = aircraftObject.transform;

                var hud = mainCamera.GetComponent<MaverickFlightHudV10>();
                if (hud == null)
                {
                    hud = mainCamera.gameObject.AddComponent<MaverickFlightHudV10>();
                    lastReport += "- Added MaverickFlightHudV10 to Main Camera.\n";
                    issues++;
                }
                hud.viewCamera = mainCamera;
                hud.aircraft = aircraftObject.GetComponent<AircraftPhysicsController>();
                hud.instructor = aircraftObject.GetComponent<MaverickInstructorV10>();
                hud.aim = aircraftObject.GetComponent<MaverickAimDirectorV10>();
            }

            if (disableBadCameras)
            {
                int disabled = DisablePodAndAutoCameras();
                if (disabled > 0)
                {
                    issues += disabled;
                    lastReport += $"- Disabled {disabled} non-main screen camera(s).\n";
                }
            }

            lastReport += issues == 0 ? "- OK: no obvious issues.\n" : $"- Fixed/flagged {issues} issue(s).\n";
            if (printReport) Debug.Log(lastReport);
        }

        public int DisablePodAndAutoCameras()
        {
            int count = 0;
            foreach (Camera cam in FindObjectsOfType<Camera>(true))
            {
                if (cam == null || cam == mainCamera) continue;
                if (cam.targetTexture != null) continue;

                string n = cam.name.ToLowerInvariant();
                if (n.Contains("targeting") || n.Contains("pod") || n.Contains("tgp") || n.Contains("chase"))
                {
                    if (cam.enabled)
                    {
                        cam.enabled = false;
                        count++;
                    }
                }
            }

            if (mainCamera != null)
            {
                mainCamera.enabled = true;
                mainCamera.depth = 100;
            }

            return count;
        }
    }
}
