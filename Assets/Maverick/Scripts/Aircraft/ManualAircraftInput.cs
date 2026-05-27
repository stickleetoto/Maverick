using UnityEngine;

namespace EaglePhysicalAI.Aircraft
{
    /// <summary>
    /// Simple keyboard input. Disable this when AI/autopilot owns the aircraft.
    /// </summary>
    [RequireComponent(typeof(AircraftPhysicsController))]
    public class ManualAircraftInput : MonoBehaviour
    {
        public AircraftPhysicsController controller;
        public bool inputEnabled = true;
        public float throttleChangePerSecond = 0.45f;
        public bool invertPitch = true;

        [Header("Last Input Snapshot")]
        public float lastPitch;
        public float lastRoll;
        public float lastYaw;
        public float lastThrottle;
        public bool strikePressed;
        public bool confirmPressed;
        public bool abortPressed;

        private void Awake()
        {
            if (controller == null) controller = GetComponent<AircraftPhysicsController>();
            lastThrottle = controller != null ? controller.targetThrottle : 0.6f;
        }

        private void Update()
        {
            // If a subclass such as WarThunderMouseAircraftInput is the active component,
            // do not let this base keyboard loop also write controls.
            if (GetType() != typeof(ManualAircraftInput)) return;

            // Button snapshots should not stay stuck when another AI mode owns the aircraft.
            strikePressed = false;
            confirmPressed = false;
            abortPressed = false;

            if (!inputEnabled || controller == null) return;

            float pitchAxis = MaverickInput.GetAxis("Vertical");
            float rollAxis = MaverickInput.GetAxis("Horizontal");
            float yawAxis = 0f;

            if (MaverickInput.GetKey(KeyCode.Q)) yawAxis -= 1f;
            if (MaverickInput.GetKey(KeyCode.E)) yawAxis += 1f;

            if (MaverickInput.GetKey(KeyCode.LeftShift)) lastThrottle += throttleChangePerSecond * Time.deltaTime;
            if (MaverickInput.GetKey(KeyCode.LeftControl)) lastThrottle -= throttleChangePerSecond * Time.deltaTime;
            lastThrottle = Mathf.Clamp01(lastThrottle);

            lastPitch = invertPitch ? -pitchAxis : pitchAxis;
            lastRoll = rollAxis;
            lastYaw = yawAxis;

            strikePressed = MaverickInput.GetKeyDown(KeyCode.Space);
            confirmPressed = MaverickInput.GetKeyDown(KeyCode.F);
            abortPressed = MaverickInput.GetKeyDown(KeyCode.R);

            controller.SetControlInputs(lastPitch, lastRoll, lastYaw, lastThrottle);
        }
    }
}
