using UnityEngine;

namespace EaglePhysicalAI.Controls
{
    /// <summary>
    /// Third-person chase camera tuned for mouse-aim flight.
    /// It stays behind the aircraft but supports Alt free-look and smooth follow.
    /// </summary>
    public class WarThunderChaseCamera : MonoBehaviour
    {
        public Transform target;
        public WarThunderMouseAircraftInput input;
        public Vector3 followOffset = new Vector3(0f, 3.2f, -13.5f);
        public float positionSmooth = 8f;
        public float rotationSmooth = 10f;
        public float freeLookSensitivity = 3.0f;
        public float freeLookReturnSpeed = 4.0f;
        public bool createCameraIfMissing = false;

        private float yawOffset;
        private float pitchOffset;

        private void Awake()
        {
            if (input == null) input = FindObjectOfType<WarThunderMouseAircraftInput>();
            if (target == null && input != null) target = input.transform;
        }

        private void LateUpdate()
        {
            if (target == null) return;
            if (input != null && input.freeLookActive)
            {
                yawOffset += MaverickInput.GetAxisRaw("Mouse X") * freeLookSensitivity;
                pitchOffset -= MaverickInput.GetAxisRaw("Mouse Y") * freeLookSensitivity;
                pitchOffset = Mathf.Clamp(pitchOffset, -70f, 70f);
            }
            else
            {
                yawOffset = Mathf.Lerp(yawOffset, 0f, 1f - Mathf.Exp(-freeLookReturnSpeed * Time.deltaTime));
                pitchOffset = Mathf.Lerp(pitchOffset, 0f, 1f - Mathf.Exp(-freeLookReturnSpeed * Time.deltaTime));
            }

            Quaternion lookRot = target.rotation * Quaternion.Euler(pitchOffset, yawOffset, 0f);
            Vector3 desiredPosition = target.position + lookRot * followOffset;
            transform.position = Vector3.Lerp(transform.position, desiredPosition, 1f - Mathf.Exp(-positionSmooth * Time.deltaTime));

            Quaternion desiredRotation = Quaternion.LookRotation(target.position + target.forward * 60f - transform.position, Vector3.up);
            if (input != null && input.freeLookActive)
            {
                desiredRotation = Quaternion.LookRotation((target.position + lookRot * Vector3.forward * 60f) - transform.position, Vector3.up);
            }
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, 1f - Mathf.Exp(-rotationSmooth * Time.deltaTime));
        }
    }
}
