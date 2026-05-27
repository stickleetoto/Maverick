using UnityEngine;

namespace EaglePhysicalAI.Controls
{
    [DisallowMultipleComponent]
    public class MaverickStableChaseCamera : MonoBehaviour
    {
        public Transform target;

        [Header("Position")]
        public float distance = 24f;
        public float height = 7f;
        public float sideOffset = 0f;
        public float positionSmooth = 9f;

        [Header("Look")]
        public float lookAheadDistance = 55f;
        public float lookHeight = 2f;
        public float rotationSmooth = 11f;

        [Header("Free Look")]
        public bool enableFreeLook = true;
        public KeyCode freeLookKey = KeyCode.LeftAlt;
        public KeyCode freeLookAltKey = KeyCode.C;
        public float mouseSensitivity = 3f;
        public float returnSpeed = 4f;
        public float maxPitch = 65f;

        [Header("Safety")]
        public bool keepAboveTarget = true;
        public float minHeightAboveTarget = 2f;
        public bool disableOtherScreenCamerasOnStart = true;

        private float yawOffset;
        private float pitchOffset;

        private void Start()
        {
            if (disableOtherScreenCamerasOnStart)
                DisableOtherScreenCameras();
            SnapToTarget();
        }

        private void LateUpdate()
        {
            if (target == null) return;

            UpdateFreeLook();

            Quaternion orbit = Quaternion.Euler(pitchOffset, yawOffset, 0f);
            Vector3 behindDir = orbit * (-target.forward);
            Vector3 desired = target.position + behindDir.normalized * distance + Vector3.up * height + target.right * sideOffset;

            if (keepAboveTarget && desired.y < target.position.y + minHeightAboveTarget)
                desired.y = target.position.y + minHeightAboveTarget;

            float pt = 1f - Mathf.Exp(-positionSmooth * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desired, pt);

            Vector3 look = target.position + target.forward * lookAheadDistance + Vector3.up * lookHeight;
            Vector3 dir = look - transform.position;
            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
                float rt = 1f - Mathf.Exp(-rotationSmooth * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, rot, rt);
            }
        }

        [ContextMenu("Snap To Target")]
        public void SnapToTarget()
        {
            if (target == null) return;
            transform.position = target.position - target.forward * distance + Vector3.up * height + target.right * sideOffset;
            Vector3 look = target.position + target.forward * lookAheadDistance + Vector3.up * lookHeight;
            Vector3 dir = look - transform.position;
            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        [ContextMenu("Disable Other Screen Cameras")]
        public void DisableOtherScreenCameras()
        {
            Camera self = GetComponent<Camera>();
            foreach (Camera cam in FindObjectsOfType<Camera>(true))
            {
                if (cam == null || cam == self) continue;
                if (cam.targetTexture != null) continue;
                string n = cam.name.ToLowerInvariant();
                if (n.Contains("targeting") || n.Contains("pod") || n.Contains("tgp") || n.Contains("chase"))
                    cam.enabled = false;
            }

            if (self != null)
            {
                self.enabled = true;
                self.depth = 100;
            }
        }

        private void UpdateFreeLook()
        {
            if (!enableFreeLook) { yawOffset = 0f; pitchOffset = 0f; return; }

            bool freeLook = MaverickInput.GetKey(freeLookKey) || MaverickInput.GetKey(freeLookAltKey);
            if (freeLook)
            {
                yawOffset += MaverickInput.GetAxisRaw("Mouse X") * mouseSensitivity;
                pitchOffset -= MaverickInput.GetAxisRaw("Mouse Y") * mouseSensitivity;
                pitchOffset = Mathf.Clamp(pitchOffset, -maxPitch, maxPitch);
            }
            else
            {
                float t = 1f - Mathf.Exp(-returnSpeed * Time.deltaTime);
                yawOffset = Mathf.Lerp(yawOffset, 0f, t);
                pitchOffset = Mathf.Lerp(pitchOffset, 0f, t);
            }
        }
    }
}
