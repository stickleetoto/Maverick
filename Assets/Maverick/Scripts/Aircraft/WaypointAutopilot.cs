using UnityEngine;
using EaglePhysicalAI.Utils;

namespace EaglePhysicalAI.Aircraft
{
    [RequireComponent(typeof(AircraftPhysicsController))]
    public class WaypointAutopilot : MonoBehaviour
    {
        public AircraftPhysicsController controller;
        public WaypointPath path;
        public bool autopilotEnabled;
        public int currentWaypointIndex;

        [Header("Targets")]
        public Transform directTarget;
        public float targetSpeed = 210f;
        public float targetAltitude = 550f;
        public bool holdAltitude = true;

        [Header("Gains")]
        public float pitchGain = 1.35f;
        public float rollGain = 1.15f;
        public float yawGain = 0.28f;
        public float altitudePitchGain = 0.0022f;
        public float throttleGain = 0.004f;
        public float maxPitchCommand = 0.85f;
        public float maxRollCommand = 0.95f;

        [Header("Orbit")]
        public bool orbitMode;
        public Transform orbitCenter;
        public float orbitRadius = 900f;
        public int orbitDirection = 1;

        private void Awake()
        {
            if (controller == null) controller = GetComponent<AircraftPhysicsController>();
        }

        private void FixedUpdate()
        {
            if (!autopilotEnabled || controller == null) return;

            Vector3 targetPosition;
            if (orbitMode && orbitCenter != null)
            {
                targetPosition = ComputeOrbitTarget();
            }
            else
            {
                Transform target = directTarget != null ? directTarget : GetCurrentPathTarget();
                if (target == null) return;
                targetPosition = target.position;
                AdvanceWaypointIfNeeded(target);
            }

            FlyToward(targetPosition);
        }

        public void SetDirectTarget(Transform target, bool enableAutopilot = true)
        {
            directTarget = target;
            orbitMode = false;
            autopilotEnabled = enableAutopilot;
        }

        public void SetOrbitTarget(Transform center, float radius, int direction, bool enableAutopilot = true)
        {
            orbitCenter = center;
            orbitRadius = Mathf.Max(50f, radius);
            orbitDirection = direction >= 0 ? 1 : -1;
            orbitMode = true;
            autopilotEnabled = enableAutopilot;
        }

        private Transform GetCurrentPathTarget()
        {
            if (path == null) return null;
            return path.GetWaypoint(currentWaypointIndex);
        }

        private void AdvanceWaypointIfNeeded(Transform target)
        {
            if (path == null || target == null) return;
            float distance = Vector3.Distance(transform.position, target.position);
            if (distance <= path.reachDistance)
            {
                currentWaypointIndex = path.GetNextIndex(currentWaypointIndex);
            }
        }

        private Vector3 ComputeOrbitTarget()
        {
            Vector3 toAircraft = transform.position - orbitCenter.position;
            toAircraft.y = 0f;
            if (toAircraft.sqrMagnitude < 1f) toAircraft = transform.forward;

            Vector3 radial = toAircraft.normalized;
            Vector3 tangent = Vector3.Cross(Vector3.up, radial).normalized * orbitDirection;
            Vector3 desired = orbitCenter.position + radial * orbitRadius + tangent * orbitRadius * 0.55f;
            desired.y = targetAltitude;
            return desired;
        }

        private void FlyToward(Vector3 targetPosition)
        {
            Vector3 toTarget = targetPosition - transform.position;
            Vector3 localTarget = transform.InverseTransformDirection(toTarget.normalized);

            float altitudeError = targetAltitude - transform.position.y;
            float altitudePitch = holdAltitude ? altitudeError * altitudePitchGain : 0f;

            float pitch = Mathf.Clamp(localTarget.y * pitchGain + altitudePitch, -maxPitchCommand, maxPitchCommand);
            float roll = Mathf.Clamp(localTarget.x * rollGain, -maxRollCommand, maxRollCommand);
            float yaw = Mathf.Clamp(localTarget.x * yawGain, -0.45f, 0.45f);

            float speedError = targetSpeed - controller.Speed;
            float throttle = Mathf.Clamp01(controller.throttle + speedError * throttleGain);

            if (controller.StallRisk > 0.65f)
            {
                throttle = 1f;
                pitch = Mathf.Min(pitch, 0.05f);
            }

            controller.SetControlInputs(pitch, roll, yaw, throttle);
        }
    }
}
