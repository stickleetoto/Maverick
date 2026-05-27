using UnityEngine;

namespace EaglePhysicalAI.UI
{
    public class ThirdPersonFlightCamera : MonoBehaviour
    {
        public Transform target;
        public Vector3 localOffset = new Vector3(0f, 8f, -24f);
        public float positionSmooth = 6f;
        public float rotationSmooth = 7f;
        public bool lookAhead = true;
        public float lookAheadDistance = 50f;

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 desiredPosition = target.TransformPoint(localOffset);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, 1f - Mathf.Exp(-positionSmooth * Time.deltaTime));

            Vector3 lookPoint = target.position + (lookAhead ? target.forward * lookAheadDistance : Vector3.zero);
            Quaternion desiredRotation = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, 1f - Mathf.Exp(-rotationSmooth * Time.deltaTime));
        }
    }
}
