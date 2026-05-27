using UnityEngine;

namespace EaglePhysicalAI.Aircraft
{
    [RequireComponent(typeof(AircraftPhysicsController))]
    public class AircraftStateSensor : MonoBehaviour
    {
        public AircraftPhysicsController controller;

        private void Awake()
        {
            if (controller == null) controller = GetComponent<AircraftPhysicsController>();
        }

        public AircraftStateSnapshot Capture()
        {
            return new AircraftStateSnapshot
            {
                position = transform.position,
                rotationEuler = transform.rotation.eulerAngles,
                velocity = controller.rb.linearVelocity,
                speed = controller.Speed,
                forwardSpeed = controller.ForwardSpeed,
                altitude = controller.Altitude,
                angleOfAttack = controller.AngleOfAttack,
                stallRisk = controller.StallRisk,
                isStalling = controller.IsStalling,
                isCrashed = controller.IsCrashed,
                pitchInput = controller.pitchInput,
                rollInput = controller.rollInput,
                yawInput = controller.yawInput,
                throttle = controller.throttle
            };
        }
    }

    [System.Serializable]
    public struct AircraftStateSnapshot
    {
        public Vector3 position;
        public Vector3 rotationEuler;
        public Vector3 velocity;
        public float speed;
        public float forwardSpeed;
        public float altitude;
        public float angleOfAttack;
        public float stallRisk;
        public bool isStalling;
        public bool isCrashed;
        public float pitchInput;
        public float rollInput;
        public float yawInput;
        public float throttle;
    }
}
