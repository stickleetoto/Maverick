using UnityEngine;

namespace MaverickFresh.Gameplay
{
    /// <summary>
    /// A render-only pose that follows a physics body smoothly.
    ///
    /// The flight-dynamics body must run without Rigidbody interpolation: it builds its flight state from the
    /// Transform, which therefore has to be the physics pose. Rendering that pose directly steps once per
    /// physics tick and judders at any other frame rate. So the aircraft's visual and the camera target live on
    /// this separate object instead. It records the body's pose after every physics step and, each frame,
    /// interpolates between the last two.
    ///
    /// It only READS the body's transform. Nothing here writes physics state, and the body carries no renderer.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class MavRenderPoseFollower : MonoBehaviour
    {
        public Transform physicsTarget;

        [Tooltip("True once a physics pose has been recorded. Until then the visual stays hidden, so the aircraft never appears at a pre-start pose.")]
        public bool hasPose;

        private Vector3 previousPosition, currentPosition;
        private Quaternion previousRotation = Quaternion.identity, currentRotation = Quaternion.identity;
        private float currentFixedTime;
        private Renderer[] renderers;

        /// <summary>Velocity of the rendered pose, m/s (for camera effects only).</summary>
        public Vector3 RenderVelocity { get; private set; }

        public void Bind(Transform target)
        {
            physicsTarget = target;
            hasPose = false;
            SetVisible(false);
        }

        /// <summary>Starts following from the target's current pose with no interpolation history.</summary>
        public void Snap()
        {
            if (physicsTarget == null)
                return;
            previousPosition = currentPosition = physicsTarget.position;
            previousRotation = currentRotation = physicsTarget.rotation;
            currentFixedTime = Time.fixedTime;
            transform.SetPositionAndRotation(currentPosition, currentRotation);
            hasPose = true;
            SetVisible(true);
        }

        private void FixedUpdate()
        {
            if (physicsTarget == null || !hasPose)
                return;

            previousPosition = currentPosition;
            previousRotation = currentRotation;
            currentPosition = physicsTarget.position;
            currentRotation = physicsTarget.rotation;
            currentFixedTime = Time.fixedTime;
            float dt = Time.fixedDeltaTime;
            RenderVelocity = dt > 0f ? (currentPosition - previousPosition) / dt : Vector3.zero;
        }

        private void LateUpdate()
        {
            if (physicsTarget == null || !hasPose)
                return;

            float dt = Time.fixedDeltaTime;
            float alpha = dt > 0f ? Mathf.Clamp01((Time.time - currentFixedTime) / dt) : 1f;
            transform.SetPositionAndRotation(
                Vector3.LerpUnclamped(previousPosition, currentPosition, alpha),
                Quaternion.SlerpUnclamped(previousRotation, currentRotation, alpha));
        }

        private void SetVisible(bool visible)
        {
            if (renderers == null || renderers.Length == 0)
                renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    renderers[i].enabled = visible;
            }
        }
    }
}
