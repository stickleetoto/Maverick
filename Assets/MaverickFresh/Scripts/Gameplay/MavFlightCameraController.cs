using UnityEngine;
using MaverickFresh.FlightDynamics;

namespace MaverickFresh.Gameplay
{
    /// <summary>
    /// The player's flight camera: the scene's one camera, following the aircraft's smoothed render pose.
    ///
    ///   CHASE        behind and above, the default
    ///   CLOSE CHASE  tighter, for attitude
    ///   NOSE         first-person reference view from the nose (no cockpit model in R1)
    ///
    /// V cycles. Following is frame-rate independent (exponential smoothing). Speed widens the view a little and
    /// pushes the chase back a little; fast rotation lags the camera a little. There is no shake. The camera reads
    /// the aircraft and never writes it.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10100)]
    public sealed class MavFlightCameraController : MonoBehaviour
    {
        public MavRenderPoseFollower target;
        public MavSixDoFBody body;
        public MavFlightCameraMode mode = MavFlightCameraMode.Chase;
        public KeyCode cycleKey = KeyCode.V;
        public bool acceptInput = true;

        [Header("Chase")]
        public float chaseDistanceM = 30f;
        public float chaseHeightM = 7f;
        public float closeDistanceM = 17f;
        public float closeHeightM = 4f;
        public Vector3 noseOffsetM = new Vector3(0f, 1.1f, 7.8f);

        [Header("Feel (restrained)")]
        public float baseFieldOfView = 60f;
        public float maxExtraFieldOfView = 7f;
        [Tooltip("Speed above which the field of view starts to widen, m/s.")]
        public float fovSpeedStartMps = 80f;
        public float fovSpeedFullMps = 220f;
        public float positionSharpness = 9f;
        public float rotationSharpness = 6f;
        public float noseRotationSharpness = 30f;

        private Camera cam;
        private bool snapped;
        private Vector3 smoothedPosition;
        private Quaternion smoothedRotation = Quaternion.identity;

        public Camera Camera
        {
            get { return cam; }
        }

        public void Bind(MavRenderPoseFollower renderPose, MavSixDoFBody flightBody, MavFlightCameraMode startMode)
        {
            target = renderPose;
            body = flightBody;
            mode = startMode;
            snapped = false;
        }

        public void Cycle()
        {
            mode = (MavFlightCameraMode)(((int)mode + 1) % 3);
            snapped = false;
        }

        public static string Label(MavFlightCameraMode m)
        {
            switch (m)
            {
                case MavFlightCameraMode.CloseChase: return "CLOSE CHASE";
                case MavFlightCameraMode.Nose: return "NOSE";
                default: return "CHASE";
            }
        }

        private void Awake()
        {
            cam = GetComponent<Camera>();
            if (cam != null)
            {
                cam.nearClipPlane = 0.5f;
                cam.farClipPlane = 120000f;
                cam.fieldOfView = baseFieldOfView;
            }
        }

        private void Update()
        {
            if (acceptInput && MavFreshInput.GetKeyDown(cycleKey))
                Cycle();
        }

        private void LateUpdate()
        {
            if (target == null || !target.hasPose || cam == null)
                return;

            Transform t = target.transform;
            float speed = body != null ? body.debugState.trueAirspeedMps : target.RenderVelocity.magnitude;
            float speed01 = Mathf.InverseLerp(fovSpeedStartMps, fovSpeedFullMps, speed);

            Vector3 desiredPosition;
            Quaternion desiredRotation;
            float sharpness = rotationSharpness;
            if (mode == MavFlightCameraMode.Nose)
            {
                desiredPosition = t.TransformPoint(noseOffsetM);
                desiredRotation = t.rotation;
                sharpness = noseRotationSharpness;
            }
            else
            {
                bool close = mode == MavFlightCameraMode.CloseChase;
                float distance = (close ? closeDistanceM : chaseDistanceM) * (1f + 0.15f * speed01);
                float height = close ? closeHeightM : chaseHeightM;
                desiredPosition = t.position - t.forward * distance + t.up * height;
                desiredRotation = Quaternion.LookRotation(t.position + t.forward * 40f - desiredPosition, t.up);
            }

            float dt = Time.deltaTime;
            if (!snapped)
            {
                smoothedPosition = desiredPosition;
                smoothedRotation = desiredRotation;
                snapped = true;
            }
            else if (dt > 0f)
            {
                float kp = mode == MavFlightCameraMode.Nose ? 1f : 1f - Mathf.Exp(-positionSharpness * dt);
                float kr = 1f - Mathf.Exp(-sharpness * dt);
                smoothedPosition = Vector3.Lerp(smoothedPosition, desiredPosition, kp);
                smoothedRotation = Quaternion.Slerp(smoothedRotation, desiredRotation, kr);
            }

            if (mode == MavFlightCameraMode.Nose)
                smoothedPosition = desiredPosition;

            transform.SetPositionAndRotation(smoothedPosition, smoothedRotation);
            float fov = baseFieldOfView + maxExtraFieldOfView * speed01;
            cam.fieldOfView = dt > 0f ? Mathf.Lerp(cam.fieldOfView, fov, 1f - Mathf.Exp(-2f * dt)) : cam.fieldOfView;
        }
    }
}
