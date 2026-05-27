using UnityEngine;

namespace EaglePhysicalAI.Controls
{
    /// <summary>
    /// v1.0 mouse-aim director.
    /// Produces a screen reticle and a desired world direction.
    /// Does not directly fly the aircraft.
    /// </summary>
    public class MaverickAimDirectorV10 : MonoBehaviour
    {
        public Camera viewCamera;
        public Transform aircraftRoot;

        [Header("Reticle")]
        [Range(0.05f, 0.49f)] public float reticleLimit = 0.39f;
        public Vector2 aimViewport = new Vector2(0.5f, 0.5f);
        public float mouseSensitivity = 0.00125f;
        public float returnToCenterSpeed = 0.55f;
        public bool lockCursorOnEnable = true;
        public bool centerOnStart = true;

        [Header("Free Look")]
        public KeyCode freeLookKey = KeyCode.LeftAlt;
        public KeyCode freeLookAltKey = KeyCode.C;
        public bool keepAimDuringFreeLook = true;

        [Header("Output")]
        public Vector3 desiredWorldDirection = Vector3.forward;
        public bool freeLookActive;
        public string note = "ready";

        private void Awake()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            if (aircraftRoot == null) aircraftRoot = transform;
            if (centerOnStart) Center();
        }

        private void OnEnable()
        {
            if (lockCursorOnEnable)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void OnDisable()
        {
            if (lockCursorOnEnable)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void Update()
        {
            if (viewCamera == null) viewCamera = Camera.main;

            freeLookActive = MaverickInput.GetKey(freeLookKey) || MaverickInput.GetKey(freeLookAltKey);

            if (!freeLookActive || !keepAimDuringFreeLook)
            {
                Vector2 mouseDelta = new Vector2(
                    MaverickInput.GetAxisRaw("Mouse X"),
                    MaverickInput.GetAxisRaw("Mouse Y")
                );

                aimViewport += new Vector2(mouseDelta.x, mouseDelta.y) * mouseSensitivity;
                ClampReticle();
                note = "aiming";
            }
            else
            {
                note = "free_look";
            }

            if (MaverickInput.GetKeyDown(KeyCode.Mouse2))
            {
                Center();
            }

            Ray ray = viewCamera != null
                ? viewCamera.ViewportPointToRay(new Vector3(aimViewport.x, aimViewport.y, 0f))
                : new Ray(aircraftRoot.position, aircraftRoot.forward);

            desiredWorldDirection = ray.direction.normalized;
        }

        public Vector2 ScreenPoint()
        {
            return new Vector2(aimViewport.x * Screen.width, (1f - aimViewport.y) * Screen.height);
        }

        public void Center()
        {
            aimViewport = new Vector2(0.5f, 0.5f);
        }

        private void ClampReticle()
        {
            Vector2 center = new Vector2(0.5f, 0.5f);
            Vector2 offset = aimViewport - center;
            if (offset.magnitude > reticleLimit)
                aimViewport = center + offset.normalized * reticleLimit;

            aimViewport.x = Mathf.Clamp(aimViewport.x, 0.5f - reticleLimit, 0.5f + reticleLimit);
            aimViewport.y = Mathf.Clamp(aimViewport.y, 0.5f - reticleLimit, 0.5f + reticleLimit);
        }
    }
}
