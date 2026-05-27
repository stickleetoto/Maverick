using UnityEngine;

namespace EaglePhysicalAI.Controls
{
    public class MaverickStableChaseCameraV10 : MonoBehaviour
    {
        public Transform target;
        public float distance = 26f;
        public float height = 7.5f;
        public float lookAheadDistance = 60f;
        public float lookHeight = 2f;
        public float positionSmooth = 10f;
        public float rotationSmooth = 12f;

        public bool enableFreeLook = true;
        public KeyCode freeLookKey = KeyCode.LeftAlt;
        public KeyCode freeLookAltKey = KeyCode.C;
        public float freeLookSensitivity = 3f;
        public float freeLookReturnSpeed = 4f;
        public float maxPitch = 65f;

        public bool keepAboveTarget = true;
        public float minHeightAboveTarget = 2.2f;
        public bool disableOtherScreenCamerasOnStart = true;

        private float yawOffset;
        private float pitchOffset;

        private void Start()
        {
            if (disableOtherScreenCamerasOnStart) DisableOtherScreenCameras();
            SnapToTarget();
        }

        private void LateUpdate()
        {
            if (target == null) return;

            bool freeLook = enableFreeLook && (MaverickInput.GetKey(freeLookKey) || MaverickInput.GetKey(freeLookAltKey));
            if (freeLook)
            {
                yawOffset += MaverickInput.GetAxisRaw("Mouse X") * freeLookSensitivity;
                pitchOffset -= MaverickInput.GetAxisRaw("Mouse Y") * freeLookSensitivity;
                pitchOffset = Mathf.Clamp(pitchOffset, -maxPitch, maxPitch);
            }
            else
            {
                float t = 1f - Mathf.Exp(-freeLookReturnSpeed * Time.deltaTime);
                yawOffset = Mathf.Lerp(yawOffset, 0f, t);
                pitchOffset = Mathf.Lerp(pitchOffset, 0f, t);
            }

            Quaternion orbit = Quaternion.Euler(pitchOffset, yawOffset, 0f);
            Vector3 behind = orbit * (-target.forward);
            Vector3 desired = target.position + behind.normalized * distance + Vector3.up * height;

            if (keepAboveTarget && desired.y < target.position.y + minHeightAboveTarget)
                desired.y = target.position.y + minHeightAboveTarget;

            float pt = 1f - Mathf.Exp(-positionSmooth * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desired, pt);

            Vector3 lookPoint = target.position + target.forward * lookAheadDistance + Vector3.up * lookHeight;
            Vector3 dir = lookPoint - transform.position;
            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion desiredRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
                float rt = 1f - Mathf.Exp(-rotationSmooth * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, rt);
            }
        }

        [ContextMenu("Snap To Target")]
        public void SnapToTarget()
        {
            if (target == null) return;
            transform.position = target.position - target.forward * distance + Vector3.up * height;
            Vector3 lookPoint = target.position + target.forward * lookAheadDistance + Vector3.up * lookHeight;
            Vector3 dir = lookPoint - transform.position;
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
    }
}
