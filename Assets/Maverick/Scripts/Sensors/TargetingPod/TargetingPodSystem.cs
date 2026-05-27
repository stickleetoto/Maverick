using UnityEngine;
using EaglePhysicalAI.Battlefield;
using EaglePhysicalAI.CAS;
using EaglePhysicalAI.Sensors.Radar;

namespace EaglePhysicalAI.Sensors.TargetingPod
{
    /// <summary>
    /// F-15E-inspired targeting pod for gameplay: stabilized look point, zoom, target tracking, and CAS designation.
    /// This is an abstract camera/sensor system, not a real targeting workflow.
    /// </summary>
    public class TargetingPodSystem : MonoBehaviour
    {
        [Header("Mode")]
        public TargetingPodMode mode = TargetingPodMode.WideArea;
        public bool podOn = true;
        public bool stabilizeLookPoint = true;
        public KeyCode cycleModeKey = KeyCode.P;
        public KeyCode designateKey = KeyCode.Return;
        public KeyCode slaveToRadarKey = KeyCode.Y;
        public KeyCode clearTrackKey = KeyCode.Backspace;

        [Header("References")]
        public Camera podCamera;
        public F15ERadarSystem radar;
        public AbstractStrikeSystem strikeSystem;
        public CasRequestManager requestManager;
        public Transform gimbalTransform;

        [Header("Gimbal / View")]
        public float yawLimit = 145f;
        public float pitchUpLimit = 25f;
        public float pitchDownLimit = 115f;
        public float slewRateDegPerSec = 65f;
        public float minFov = 8f;
        public float maxFov = 55f;
        public float zoomSpeed = 20f;
        public float defaultGroundLookDistance = 3500f;
        public LayerMask podMask = ~0;
        public LayerMask losBlockMask = 0;
        public bool requireLineOfSight = true;

        [Header("Track Quality")]
        public float maxRecognitionRange = 6500f;
        public float pointTrackAngleLimit = 18f;
        public float trackRiseRate = 1.5f;
        public float trackDecayRate = 0.8f;
        public float identificationRiseRate = 0.8f;

        [Header("State")]
        public Transform trackedTarget;
        public GroundUnit designatedGroundUnit;
        public Vector3 lookPoint;
        public Vector2 gimbalAngles;
        public float targetTrackQuality;
        public float identificationConfidence;
        public bool hasLineOfSight;
        public string lastDesignateResult = "none";
        public SensorContact lastPodContact;

        private float _desiredFov = 35f;

        private void Awake()
        {
            if (radar == null) radar = GetComponent<F15ERadarSystem>();
            if (strikeSystem == null) strikeSystem = GetComponent<AbstractStrikeSystem>();
            if (requestManager == null) requestManager = FindObjectOfType<CasRequestManager>();
            if (gimbalTransform == null) gimbalTransform = transform;
            if (podCamera != null) _desiredFov = podCamera.fieldOfView;
            lookPoint = transform.position + transform.forward * defaultGroundLookDistance;
        }

        private void Update()
        {
            HandleDebugKeys();
            if (!podOn || mode == TargetingPodMode.Stowed) return;

            HandleManualSlew();
            HandleZoom();
            UpdateLookPointAndTrack();
            UpdatePodCamera();
            UpdatePodContact();
        }

        public void CycleMode()
        {
            if (mode == TargetingPodMode.Stowed) mode = TargetingPodMode.WideArea;
            else if (mode == TargetingPodMode.WideArea) mode = TargetingPodMode.AreaTrack;
            else if (mode == TargetingPodMode.AreaTrack) mode = TargetingPodMode.PointTrack;
            else if (mode == TargetingPodMode.PointTrack) mode = TargetingPodMode.RadarSlave;
            else if (mode == TargetingPodMode.RadarSlave) mode = TargetingPodMode.CasConfirm;
            else mode = TargetingPodMode.Stowed;
        }

        public void SlaveToRadarTrack()
        {
            if (radar == null) return;
            SensorContact track = radar.lockedTrack != null ? radar.lockedTrack : radar.selectedTrack;
            if (track == null || track.targetTransform == null) return;
            trackedTarget = track.targetTransform;
            lookPoint = track.worldPosition;
            mode = TargetingPodMode.RadarSlave;
        }

        public void ClearTrack()
        {
            trackedTarget = null;
            designatedGroundUnit = null;
            targetTrackQuality = 0f;
            identificationConfidence = 0f;
            lastPodContact = null;
            lastDesignateResult = "cleared";
        }

        public bool TryDesignateCurrentTarget()
        {
            GroundUnit unit = ResolveTrackedGroundUnit();
            if (unit == null)
            {
                lastDesignateResult = "no_ground_unit_in_pod_track";
                return false;
            }

            if (!unit.isAlive)
            {
                lastDesignateResult = "target_not_alive";
                return false;
            }

            if (identificationConfidence < 0.45f)
            {
                lastDesignateResult = "low_identification_confidence";
                return false;
            }

            designatedGroundUnit = unit;
            if (strikeSystem != null) strikeSystem.selectedTarget = unit;
            if (requestManager != null && unit.team == GroundTeam.Hostile)
            {
                requestManager.CreateRequest(null, unit, unit.transform.position, Mathf.Clamp01(identificationConfidence), "pod_designated_target");
            }

            lastDesignateResult = "designated_" + unit.unitId;
            return true;
        }

        public GroundUnit ResolveTrackedGroundUnit()
        {
            if (trackedTarget != null)
            {
                GroundUnit unit = trackedTarget.GetComponentInParent<GroundUnit>();
                if (unit != null) return unit;
            }

            RaycastHit hit;
            if (Physics.Raycast(transform.position, GetLookDirection(), out hit, maxRecognitionRange, podMask, QueryTriggerInteraction.Collide))
            {
                return hit.collider.GetComponentInParent<GroundUnit>();
            }
            return null;
        }

        public Vector3 GetLookDirection()
        {
            Vector3 local = Quaternion.Euler(gimbalAngles.y, gimbalAngles.x, 0f) * Vector3.forward;
            return transform.TransformDirection(local).normalized;
        }

        public SensorContact BuildPodContact()
        {
            GroundUnit unit = ResolveTrackedGroundUnit();
            if (unit == null) return null;
            Vector3 targetPoint = unit.transform.position;
            Vector3 toTarget = targetPoint - transform.position;
            if (toTarget.magnitude < 1f) return null;

            Vector3 local = transform.InverseTransformDirection(toTarget.normalized);
            var contact = new SensorContact
            {
                contactId = unit.unitId,
                displayName = unit.unitId,
                kind = unit.team == GroundTeam.Hostile ? SensorContactKind.CasTarget : SensorContactKind.Ground,
                team = unit.team,
                targetTransform = unit.transform,
                worldPosition = targetPoint,
                localDirection = local,
                rangeMeters = toTarget.magnitude,
                bearingDegrees = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg,
                elevationDegrees = Mathf.Asin(Mathf.Clamp(local.y, -1f, 1f)) * Mathf.Rad2Deg,
                signalStrength = Mathf.Clamp01(targetTrackQuality),
                trackQuality = Mathf.Clamp01(targetTrackQuality),
                identificationConfidence = Mathf.Clamp01(identificationConfidence),
                lastSeenTime = Time.time,
                firstSeenTime = Time.time,
                detectedByTargetingPod = true,
                isAlive = unit.isAlive
            };
            return contact;
        }

        private void HandleDebugKeys()
        {
            if (MaverickInput.GetKeyDown(cycleModeKey)) CycleMode();
            if (MaverickInput.GetKeyDown(slaveToRadarKey)) SlaveToRadarTrack();
            if (MaverickInput.GetKeyDown(clearTrackKey)) ClearTrack();
            if (MaverickInput.GetKeyDown(designateKey)) TryDesignateCurrentTarget();
        }

        private void HandleManualSlew()
        {
            float yaw = 0f;
            float pitch = 0f;
            if (MaverickInput.GetKey(KeyCode.J)) yaw -= 1f;
            if (MaverickInput.GetKey(KeyCode.L)) yaw += 1f;
            if (MaverickInput.GetKey(KeyCode.I)) pitch += 1f;
            if (MaverickInput.GetKey(KeyCode.K)) pitch -= 1f;

            gimbalAngles.x = Mathf.Clamp(gimbalAngles.x + yaw * slewRateDegPerSec * Time.deltaTime, -yawLimit, yawLimit);
            gimbalAngles.y = Mathf.Clamp(gimbalAngles.y - pitch * slewRateDegPerSec * Time.deltaTime, -pitchUpLimit, pitchDownLimit);
        }

        private void HandleZoom()
        {
            float zoom = 0f;
            if (MaverickInput.GetKey(KeyCode.Equals) || MaverickInput.GetKey(KeyCode.KeypadPlus)) zoom -= 1f;
            if (MaverickInput.GetKey(KeyCode.Minus) || MaverickInput.GetKey(KeyCode.KeypadMinus)) zoom += 1f;
            _desiredFov = Mathf.Clamp(_desiredFov + zoom * zoomSpeed * Time.deltaTime, minFov, maxFov);
        }

        private void UpdateLookPointAndTrack()
        {
            if (mode == TargetingPodMode.RadarSlave && radar != null)
            {
                SensorContact track = radar.lockedTrack != null ? radar.lockedTrack : radar.selectedTrack;
                if (track != null && track.targetTransform != null)
                {
                    trackedTarget = track.targetTransform;
                    lookPoint = track.worldPosition;
                    AimGimbalAt(lookPoint);
                }
            }
            else if ((mode == TargetingPodMode.PointTrack || mode == TargetingPodMode.CasConfirm) && trackedTarget != null)
            {
                lookPoint = trackedTarget.position;
                AimGimbalAt(lookPoint);
            }
            else
            {
                RaycastHit hit;
                if (Physics.Raycast(transform.position, GetLookDirection(), out hit, defaultGroundLookDistance * 2f, podMask, QueryTriggerInteraction.Collide))
                {
                    lookPoint = hit.point;
                    if (mode == TargetingPodMode.PointTrack || mode == TargetingPodMode.CasConfirm)
                    {
                        RadarSignature signature = hit.collider.GetComponentInParent<RadarSignature>();
                        GroundUnit unit = hit.collider.GetComponentInParent<GroundUnit>();
                        if (signature != null || unit != null) trackedTarget = hit.collider.transform;
                    }
                }
                else if (!stabilizeLookPoint)
                {
                    lookPoint = transform.position + GetLookDirection() * defaultGroundLookDistance;
                }
            }

            EvaluateTrackQuality();
        }

        private void AimGimbalAt(Vector3 worldPoint)
        {
            Vector3 local = transform.InverseTransformDirection((worldPoint - transform.position).normalized);
            float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Asin(Mathf.Clamp(local.y, -1f, 1f)) * Mathf.Rad2Deg;
            gimbalAngles.x = Mathf.Clamp(Mathf.MoveTowardsAngle(gimbalAngles.x, yaw, slewRateDegPerSec * Time.deltaTime), -yawLimit, yawLimit);
            gimbalAngles.y = Mathf.Clamp(Mathf.MoveTowardsAngle(gimbalAngles.y, pitch, slewRateDegPerSec * Time.deltaTime), -pitchUpLimit, pitchDownLimit);
        }

        private void EvaluateTrackQuality()
        {
            GroundUnit unit = ResolveTrackedGroundUnit();
            bool hasTarget = unit != null && unit.isAlive;
            Vector3 direction = GetLookDirection();
            Vector3 toLookPoint = lookPoint - transform.position;
            float range = toLookPoint.magnitude;
            float angle = Vector3.Angle(direction, toLookPoint.normalized);
            hasLineOfSight = !requireLineOfSight || !Physics.Raycast(transform.position, toLookPoint.normalized, range, losBlockMask, QueryTriggerInteraction.Ignore);

            if (hasTarget && hasLineOfSight && range <= maxRecognitionRange && angle <= pointTrackAngleLimit)
            {
                float rangeFactor = 1f - Mathf.Clamp01(range / maxRecognitionRange);
                float angleFactor = 1f - Mathf.Clamp01(angle / pointTrackAngleLimit);
                float targetScore = 0.25f + rangeFactor * 0.35f + angleFactor * 0.35f;
                targetTrackQuality = Mathf.MoveTowards(targetTrackQuality, Mathf.Clamp01(targetScore), trackRiseRate * Time.deltaTime);
                identificationConfidence = Mathf.MoveTowards(identificationConfidence, Mathf.Clamp01(targetTrackQuality * 0.75f + targetScore * 0.25f), identificationRiseRate * Time.deltaTime);
            }
            else
            {
                targetTrackQuality = Mathf.MoveTowards(targetTrackQuality, 0f, trackDecayRate * Time.deltaTime);
                identificationConfidence = Mathf.MoveTowards(identificationConfidence, 0f, identificationRiseRate * 0.5f * Time.deltaTime);
                if (targetTrackQuality <= 0.02f && mode != TargetingPodMode.AreaTrack) trackedTarget = null;
            }
        }

        private void UpdatePodCamera()
        {
            if (gimbalTransform != null && gimbalTransform != transform)
            {
                gimbalTransform.rotation = Quaternion.LookRotation(GetLookDirection(), transform.up);
            }
            if (podCamera != null)
            {
                podCamera.transform.position = gimbalTransform != null ? gimbalTransform.position : transform.position;
                podCamera.transform.rotation = Quaternion.LookRotation(GetLookDirection(), transform.up);
                podCamera.fieldOfView = Mathf.MoveTowards(podCamera.fieldOfView, _desiredFov, zoomSpeed * Time.deltaTime);
            }
        }

        private void UpdatePodContact()
        {
            lastPodContact = BuildPodContact();
        }

        private void OnDrawGizmosSelected()
        {
            if (!podOn) return;
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, lookPoint);
            Gizmos.DrawWireSphere(lookPoint, Mathf.Lerp(5f, 45f, targetTrackQuality));
            if (trackedTarget != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(trackedTarget.position, 25f);
            }
        }
    }
}
