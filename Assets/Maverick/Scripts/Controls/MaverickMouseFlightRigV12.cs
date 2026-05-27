using UnityEngine;

namespace EaglePhysicalAI.Controls
{
    /// <summary>
    /// MouseFlight-inspired rig for Project MAVERICK.
    /// 
    /// Important design:
    /// - The rig is NOT parented to the aircraft.
    /// - It follows aircraft position.
    /// - mouseAim is a world-space Transform rotated by mouse input.
    /// - Camera looks along mouseAim / frozen free-look direction.
    /// - Provides MouseAimPos and BoresightPos for HUD + Instructor.
    /// </summary>
    [DisallowMultipleComponent]
    public class MaverickMouseFlightRigV12 : MonoBehaviour
    {
        [Header("References")]
        public Transform aircraft;
        public Camera viewCamera;
        public Transform mouseAim;
        public Transform cameraRig;

        [Header("Aim")]
        public float aimDistance = 900f;
        public float mouseSensitivity = 3.2f;
        public float maxAimAngle = 70f;
        public bool lockCursor = true;
        public bool autoCenterOnStart = true;

        [Header("Camera")]
        public float cameraDistance = 24f;
        public float cameraHeight = 7f;
        public float cameraFollowSmooth = 12f;
        public float cameraRotationSmooth = 12f;
        public float lookAheadDistance = 70f;

        [Header("Free Look")]
        public KeyCode freeLookKey = KeyCode.C;
        public KeyCode freeLookAltKey = KeyCode.LeftAlt;
        public bool isMouseAimFrozen;
        public Vector3 frozenAimForward;

        [Header("Output")]
        public Vector3 MouseAimPos;
        public Vector3 BoresightPos;
        public Vector3 VelocityVectorPos;
        public Vector3 DesiredDirection;
        public string rigNote = "ready";

        private void Awake()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            EnsureRigObjects();

            // The rig must be world-space, not a child of the jet.
            transform.SetParent(null, true);

            if (aircraft != null)
            {
                transform.position = aircraft.position;
                transform.rotation = aircraft.rotation;
                mouseAim.position = aircraft.position;
                mouseAim.rotation = aircraft.rotation;
                cameraRig.position = aircraft.position;
                cameraRig.rotation = aircraft.rotation;
            }

            if (autoCenterOnStart)
                CenterAim();
        }

        private void OnEnable()
        {
            if (lockCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void OnDisable()
        {
            if (lockCursor)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void LateUpdate()
        {
            if (aircraft == null)
                return;

            EnsureRigObjects();

            transform.position = aircraft.position;
            cameraRig.position = aircraft.position;
            mouseAim.position = aircraft.position;

            HandleMouseAim();
            ClampAimAngle();
            UpdateOutputPoints();
            UpdateCamera();
        }

        public void CenterAim()
        {
            if (aircraft == null) return;
            EnsureRigObjects();
            mouseAim.position = aircraft.position;
            mouseAim.rotation = aircraft.rotation;
            frozenAimForward = mouseAim.forward;
            isMouseAimFrozen = false;
        }

        private void EnsureRigObjects()
        {
            if (mouseAim == null)
            {
                GameObject go = new GameObject("Maverick_MouseAim_V12");
                go.transform.SetParent(transform, false);
                mouseAim = go.transform;
            }

            if (cameraRig == null)
            {
                GameObject go = new GameObject("Maverick_CameraRig_V12");
                go.transform.SetParent(transform, false);
                cameraRig = go.transform;
            }
        }

        private void HandleMouseAim()
        {
            bool freeLookDown = MaverickInput.GetKeyDown(freeLookKey) || MaverickInput.GetKeyDown(freeLookAltKey);
            bool freeLookHeld = MaverickInput.GetKey(freeLookKey) || MaverickInput.GetKey(freeLookAltKey);
            bool freeLookUp = MaverickInput.GetKeyUp(freeLookKey) || MaverickInput.GetKeyUp(freeLookAltKey);

            if (freeLookDown)
            {
                isMouseAimFrozen = true;
                frozenAimForward = mouseAim.forward;
                rigNote = "free_look_freeze";
            }

            if (freeLookUp)
            {
                isMouseAimFrozen = false;
                mouseAim.forward = frozenAimForward.sqrMagnitude > 0.001f ? frozenAimForward : aircraft.forward;
                rigNote = "free_look_release";
            }

            if (MaverickInput.GetKeyDown(KeyCode.Mouse2))
            {
                CenterAim();
                rigNote = "recenter";
                return;
            }

            if (!freeLookHeld)
            {
                float mx = MaverickInput.GetAxisRaw("Mouse X") * mouseSensitivity;
                float my = -MaverickInput.GetAxisRaw("Mouse Y") * mouseSensitivity;

                // MouseFlight-style world rotation around camera/rig axes.
                Vector3 rightAxis = viewCamera != null ? viewCamera.transform.right : cameraRig.right;
                Vector3 upAxis = viewCamera != null ? viewCamera.transform.up : Vector3.up;

                mouseAim.Rotate(rightAxis, my, Space.World);
                mouseAim.Rotate(upAxis, mx, Space.World);
                rigNote = "aiming";
            }
            else
            {
                rigNote = "free_look";
            }
        }

        private void ClampAimAngle()
        {
            if (aircraft == null || mouseAim == null) return;

            float angle = Vector3.Angle(aircraft.forward, mouseAim.forward);
            if (angle <= maxAimAngle)
                return;

            Vector3 clamped = Vector3.RotateTowards(aircraft.forward, mouseAim.forward, maxAimAngle * Mathf.Deg2Rad, 0f);
            mouseAim.forward = clamped.normalized;
        }

        private void UpdateOutputPoints()
        {
            DesiredDirection = mouseAim.forward.normalized;
            MouseAimPos = mouseAim.position + mouseAim.forward * aimDistance;
            BoresightPos = aircraft.position + aircraft.forward * aimDistance;

            Rigidbody rb = aircraft.GetComponent<Rigidbody>();
            if (rb != null && rb.linearVelocity.sqrMagnitude > 4f)
                VelocityVectorPos = aircraft.position + rb.linearVelocity.normalized * aimDistance;
            else
                VelocityVectorPos = BoresightPos;
        }

        private void UpdateCamera()
        {
            if (viewCamera == null)
                return;

            // Camera position is behind the aircraft, but the rotation looks toward the mouse aim direction.
            Vector3 desiredPosition = aircraft.position - aircraft.forward * cameraDistance + Vector3.up * cameraHeight;
            float pt = 1f - Mathf.Exp(-cameraFollowSmooth * Time.deltaTime);
            viewCamera.transform.position = Vector3.Lerp(viewCamera.transform.position, desiredPosition, pt);

            Vector3 lookDir;
            if (isMouseAimFrozen)
                lookDir = frozenAimForward.sqrMagnitude > 0.001f ? frozenAimForward : mouseAim.forward;
            else
                lookDir = (MouseAimPos - viewCamera.transform.position).normalized;

            if (lookDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir, Vector3.up);
                float rt = 1f - Mathf.Exp(-cameraRotationSmooth * Time.deltaTime);
                viewCamera.transform.rotation = Quaternion.Slerp(viewCamera.transform.rotation, targetRot, rt);
            }

            Camera cam = viewCamera.GetComponent<Camera>();
            if (cam != null)
            {
                cam.enabled = true;
                cam.depth = 150;
            }
        }
    }
}
