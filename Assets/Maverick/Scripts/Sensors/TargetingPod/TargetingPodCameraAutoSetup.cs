using UnityEngine;

namespace EaglePhysicalAI.Sensors.TargetingPod
{
    /// <summary>
    /// Optional helper that creates a child Camera for the targeting pod when the scene has no pod camera yet.
    /// The camera is intentionally simple so the user can replace it with a proper cockpit/MFD setup later.
    /// </summary>
    [RequireComponent(typeof(TargetingPodSystem))]
    public class TargetingPodCameraAutoSetup : MonoBehaviour
    {
        public bool createOnStart = true;
        public string cameraName = "Targeting_Pod_Camera";
        public Vector3 localMountPosition = new Vector3(0f, -1.2f, 2.5f);
        public float fieldOfView = 35f;
        public float nearClip = 0.3f;
        public float farClip = 12000f;
        public int depth = -50;

        [Header("v0.9 Render Safety")]
        [Tooltip("False by default. The targeting pod camera should not render directly to the Game view unless you explicitly want it.")]
        public bool renderToGameView = false;
        [Tooltip("Assign this later for an MFD/TGP UI. If null, the pod camera is created disabled.")]
        public RenderTexture targetTexture;

        private void Start()
        {
            if (!createOnStart) return;
            TargetingPodSystem pod = GetComponent<TargetingPodSystem>();
            if (pod == null || pod.podCamera != null) return;

            GameObject cameraObject = new GameObject(cameraName);
            cameraObject.transform.SetParent(transform, false);
            cameraObject.transform.localPosition = localMountPosition;
            cameraObject.transform.localRotation = Quaternion.identity;

            Camera cam = cameraObject.AddComponent<Camera>();
            cam.fieldOfView = fieldOfView;
            cam.nearClipPlane = nearClip;
            cam.farClipPlane = farClip;
            cam.depth = depth;
            cam.targetTexture = targetTexture;
            // Critical: keep pod camera from hijacking Game view.
            cam.enabled = renderToGameView || targetTexture != null;
            pod.podCamera = cam;
            pod.gimbalTransform = cameraObject.transform;
        }
    }
}
