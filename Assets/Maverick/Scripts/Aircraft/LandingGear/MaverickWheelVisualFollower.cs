using UnityEngine;

public class MaverickWheelVisualFollower : MonoBehaviour
{
    public WheelCollider wheelCollider;
    public bool followPosition = true;
    public bool followRotation = true;
    public Vector3 positionOffset;
    public Vector3 rotationOffsetEuler;

    private void LateUpdate()
    {
        if (wheelCollider == null) return;
        wheelCollider.GetWorldPose(out Vector3 pos, out Quaternion rot);
        if (followPosition) transform.position = pos + transform.TransformVector(positionOffset);
        if (followRotation) transform.rotation = rot * Quaternion.Euler(rotationOffsetEuler);
    }
}
