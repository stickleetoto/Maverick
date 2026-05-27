using UnityEngine;

[DisallowMultipleComponent]
public class MaverickLandingGearPhysics : MonoBehaviour
{
    public Rigidbody aircraftRigidbody;
    public WheelCollider noseWheel;
    public WheelCollider leftMainWheel;
    public WheelCollider rightMainWheel;

    public bool gearDeployed = true;
    public KeyCode toggleGearKey = KeyCode.G;
    public KeyCode brakeKey = KeyCode.B;
    public KeyCode parkingBrakeKey = KeyCode.P;
    public bool parkingBrake;

    public float maxSteerAngleDeg = 28f;
    public float brakeTorque = 45000f;
    public float parkingBrakeTorque = 80000f;
    public float taxiMotorTorque = 12000f;
    public bool enableTaxiMotorAssist = false;

    public bool isGrounded;
    public float steerInput;
    public float brakeInput;

    private void Awake()
    {
        if (aircraftRigidbody == null) aircraftRigidbody = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (MaverickInput.GetKeyDown(toggleGearKey)) gearDeployed = !gearDeployed;
        if (MaverickInput.GetKeyDown(parkingBrakeKey)) parkingBrake = !parkingBrake;

        steerInput = 0f;
        if (MaverickInput.GetKey(KeyCode.A) || MaverickInput.GetKey(KeyCode.LeftArrow)) steerInput -= 1f;
        if (MaverickInput.GetKey(KeyCode.D) || MaverickInput.GetKey(KeyCode.RightArrow)) steerInput += 1f;

        brakeInput = MaverickInput.GetKey(brakeKey) || MaverickInput.GetKey(KeyCode.Space) ? 1f : 0f;
        if (parkingBrake) brakeInput = 1f;

        ApplyWheelState();
    }

    private void FixedUpdate()
    {
        isGrounded = gearDeployed && ((noseWheel != null && noseWheel.isGrounded) || (leftMainWheel != null && leftMainWheel.isGrounded) || (rightMainWheel != null && rightMainWheel.isGrounded));
    }

    private void ApplyWheelState()
    {
        SetEnabled(noseWheel, gearDeployed);
        SetEnabled(leftMainWheel, gearDeployed);
        SetEnabled(rightMainWheel, gearDeployed);

        if (!gearDeployed) return;

        if (noseWheel != null) noseWheel.steerAngle = steerInput * maxSteerAngleDeg;
        float b = parkingBrake ? parkingBrakeTorque : brakeInput * brakeTorque;
        SetBrake(noseWheel, b * 0.35f);
        SetBrake(leftMainWheel, b);
        SetBrake(rightMainWheel, b);

        float motor = enableTaxiMotorAssist && isGrounded ? taxiMotorTorque : 0f;
        SetMotor(leftMainWheel, motor);
        SetMotor(rightMainWheel, motor);
    }

    private void SetEnabled(WheelCollider w, bool enabled) { if (w != null) w.enabled = enabled; }
    private void SetBrake(WheelCollider w, float t) { if (w != null) w.brakeTorque = t; }
    private void SetMotor(WheelCollider w, float t) { if (w != null) w.motorTorque = t; }
}
