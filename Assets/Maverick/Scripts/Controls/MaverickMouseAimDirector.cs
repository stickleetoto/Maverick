using UnityEngine;

namespace EaglePhysicalAI.Controls
{
    /// <summary>
    /// Converts mouse movement into a War-Thunder-like aim cursor and desired world direction.
    /// This is not direct flight control; it only creates "where the pilot wants to go/look".
    /// </summary>
    public class MaverickMouseAimDirector : MonoBehaviour
    {
        [Header("References")]
        public Camera viewCamera;
        public Transform aircraftRoot;

        [Header("Cursor")]
        public Vector2 aimViewport = new Vector2(0.5f, 0.5f);
        public float sensitivity = 0.00165f;
        public float reticleLimit = 0.42f;
        public float centerReturnSpeed = 0.12f;
        public bool lockCursor = true;

        [Header("Free Look")]
        public KeyCode freeLookKey = KeyCode.LeftAlt;
        public KeyCode freeLookAltKey = KeyCode.C;
        public bool returnCursorDuringFreeLook = false;
        public bool freeLookActive;

        [Header("Output")]
        public Vector3 desiredWorldDirection;
        public Ray aimRay;

        private void Awake()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            if (aircraftRoot == null) aircraftRoot = transform;
        }

        private void OnEnable()
        {
            if (lockCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void OnDisable()
        {
            if (lockCursor)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void Update()
        {
            if (viewCamera == null) viewCamera = Camera.main;

            freeLookActive = MaverickInput.GetKey(freeLookKey) || MaverickInput.GetKey(freeLookAltKey);

            if (!freeLookActive)
            {
                Vector2 delta = new Vector2(MaverickInput.GetAxisRaw("Mouse X"), MaverickInput.GetAxisRaw("Mouse Y"));
                aimViewport += delta * sensitivity;
            }
            else if (returnCursorDuringFreeLook)
            {
                aimViewport = Vector2.Lerp(aimViewport, new Vector2(0.5f, 0.5f), centerReturnSpeed * Time.deltaTime);
            }

            Vector2 offset = aimViewport - new Vector2(0.5f, 0.5f);
            if (offset.magnitude > reticleLimit)
            {
                offset = offset.normalized * reticleLimit;
                aimViewport = new Vector2(0.5f, 0.5f) + offset;
            }

            if (viewCamera != null)
            {
                aimRay = viewCamera.ViewportPointToRay(new Vector3(aimViewport.x, aimViewport.y, 0f));
                desiredWorldDirection = aimRay.direction.normalized;
            }
            else
            {
                desiredWorldDirection = aircraftRoot != null ? aircraftRoot.forward : Vector3.forward;
            }
        }

        public Vector2 GetScreenPoint()
        {
            return new Vector2(aimViewport.x * Screen.width, (1f - aimViewport.y) * Screen.height);
        }

        public void CenterCursor()
        {
            aimViewport = new Vector2(0.5f, 0.5f);
        }
    }
}
