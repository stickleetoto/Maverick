using UnityEngine;

/// <summary>
/// Optional helper to create a clean WheelCollider hierarchy under F15E_Player.
/// Attach to F15E_Player, assign approximate wheel visual transforms, then click context menu.
/// You can remove this component after setup.
/// </summary>
public class MaverickWheelAutoRig : MonoBehaviour
{
    [Header("Approximate visual wheel transforms")]
    public Transform noseWheelVisual;
    public Transform leftMainWheelVisual;
    public Transform rightMainWheelVisual;

    [Header("Creation Settings")]
    public float defaultRadius = 0.35f;
    public float defaultSuspensionDistance = 0.45f;
    public float spring = 65000f;
    public float damper = 8000f;
    public float targetPosition = 0.5f;

    [Header("Created")]
    public WheelCollider noseWheelCollider;
    public WheelCollider leftMainWheelCollider;
    public WheelCollider rightMainWheelCollider;

    [ContextMenu("Create WheelCollider Rig")]
    public void CreateWheelColliderRig()
    {
        Transform root = FindOrCreateChild(transform, "WheelColliders");

        noseWheelCollider = CreateWheel(root, "NoseWheelCollider", noseWheelVisual);
        leftMainWheelCollider = CreateWheel(root, "LeftMainWheelCollider", leftMainWheelVisual);
        rightMainWheelCollider = CreateWheel(root, "RightMainWheelCollider", rightMainWheelVisual);

        var gear = GetComponent<MaverickLandingGearPhysics>();
        if (gear == null)
            gear = gameObject.AddComponent<MaverickLandingGearPhysics>();

        gear.aircraftRigidbody = GetComponent<Rigidbody>();
        gear.noseWheel = noseWheelCollider;
        gear.leftMainWheel = leftMainWheelCollider;
        gear.rightMainWheel = rightMainWheelCollider;
    }

    private WheelCollider CreateWheel(Transform parent, string name, Transform visual)
    {
        Transform existing = parent.Find(name);
        GameObject go = existing != null ? existing.gameObject : new GameObject(name);
        go.transform.SetParent(parent, true);

        if (visual != null)
        {
            go.transform.position = visual.position;
            go.transform.rotation = visual.rotation;
        }
        else
        {
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
        }

        var wheel = go.GetComponent<WheelCollider>();
        if (wheel == null)
            wheel = go.AddComponent<WheelCollider>();

        wheel.radius = defaultRadius;
        wheel.suspensionDistance = defaultSuspensionDistance;
        wheel.mass = 80f;

        JointSpring s = wheel.suspensionSpring;
        s.spring = spring;
        s.damper = damper;
        s.targetPosition = targetPosition;
        wheel.suspensionSpring = s;

        wheel.forceAppPointDistance = 0.15f;
        wheel.wheelDampingRate = 0.25f;

        return wheel;
    }

    private Transform FindOrCreateChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null)
            return child;

        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }
}
