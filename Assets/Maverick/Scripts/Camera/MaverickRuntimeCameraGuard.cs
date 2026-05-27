using UnityEngine;

namespace EaglePhysicalAI.Controls
{
    public class MaverickRuntimeCameraGuard : MonoBehaviour
    {
        public Camera mainCamera;
        public Transform target;
        public bool installStableChaseCamera = true;
        public bool disablePodAndChaseScreenCameras = true;
        public bool runEveryFrameForFirstSeconds = true;
        public float guardSeconds = 3f;

        private float startTime;

        private void Start()
        {
            startTime = Time.time;
            Apply();
        }

        private void LateUpdate()
        {
            if (runEveryFrameForFirstSeconds && Time.time - startTime <= guardSeconds)
                Apply();
        }

        [ContextMenu("Apply Camera Guard")]
        public void Apply()
        {
            if (mainCamera == null) mainCamera = Camera.main;

            if (mainCamera != null)
            {
                mainCamera.enabled = true;
                mainCamera.depth = 100;

                if (installStableChaseCamera)
                {
                    var stable = mainCamera.GetComponent<MaverickStableChaseCamera>();
                    if (stable == null) stable = mainCamera.gameObject.AddComponent<MaverickStableChaseCamera>();
                    if (stable.target == null && target != null) stable.target = target;
                    stable.disableOtherScreenCamerasOnStart = true;
                }
            }

            if (!disablePodAndChaseScreenCameras) return;

            foreach (Camera cam in FindObjectsOfType<Camera>(true))
            {
                if (cam == null || cam == mainCamera) continue;
                if (cam.targetTexture != null) continue;
                string n = cam.name.ToLowerInvariant();
                if (n.Contains("targeting") || n.Contains("pod") || n.Contains("tgp") || n.Contains("chase"))
                    cam.enabled = false;
            }
        }
    }
}
