using UnityEngine;

namespace MaverickFresh
{
    /// <summary>
    /// CCIP-lite predictor for game CAS.
    /// Predicts approximate impact point for gun, rocket, or bomb using step simulation.
    /// </summary>
    [DisallowMultipleComponent]
    public class MavCASCCIPPredictor : MonoBehaviour
    {
        [Header("References")]
        public MavMouseFlightRig rig;
        public Camera playerCamera;
        public Rigidbody aircraftRb;

        [Header("Prediction")]
        public LayerMask hitMask = ~0;
        public int steps = 160;
        public float dt = 0.035f;
        public float gravity = 9.81f;
        public float maxTime = 8f;
        public float gunMuzzleSpeed = 960f;
        public float rocketMuzzleSpeed = 520f;
        public float bombForwardSpeedFactor = 1.0f;

        [Header("Runtime")]
        public Vector3 predictedGunImpact;
        public Vector3 predictedRocketImpact;
        public Vector3 predictedBombImpact;
        public bool hasGunImpact;
        public bool hasRocketImpact;
        public bool hasBombImpact;

        private void Awake()
        {
            Resolve();
        }

        private void Update()
        {
            Resolve();
            PredictAll();
        }

        public void Resolve()
        {
            if (rig == null) rig = FindObjectOfType<MavMouseFlightRig>();
            if (playerCamera == null) playerCamera = Camera.main;
            if (aircraftRb == null) aircraftRb = GetComponent<Rigidbody>();
        }

        public void PredictAll()
        {
            Vector3 origin = GetFireOrigin();
            Vector3 aimDir = GetAimDirection();

            Vector3 aircraftVel = aircraftRb != null ? aircraftRb.linearVelocity : Vector3.zero;

            hasGunImpact = PredictImpact(origin, aircraftVel + aimDir * gunMuzzleSpeed, false, out predictedGunImpact);
            hasRocketImpact = PredictImpact(origin, aircraftVel + aimDir * rocketMuzzleSpeed, true, out predictedRocketImpact);
            hasBombImpact = PredictImpact(origin, aircraftVel * bombForwardSpeedFactor, true, out predictedBombImpact);
        }

        public Vector3 GetFireOrigin()
        {
            return transform.position + transform.forward * 10f + -transform.up * 1.5f;
        }

        public Vector3 GetAimDirection()
        {
            if (playerCamera != null && rig != null)
            {
                Vector2 aim = rig.cursorViewport;
                Ray ray = playerCamera.ViewportPointToRay(new Vector3(aim.x, aim.y, 0f));
                return ray.direction.normalized;
            }

            return transform.forward;
        }

        public bool PredictImpact(Vector3 start, Vector3 initialVelocity, bool gravityOn, out Vector3 impact)
        {
            Vector3 pos = start;
            Vector3 vel = initialVelocity;
            float elapsed = 0f;

            for (int i = 0; i < steps && elapsed < maxTime; i++)
            {
                Vector3 old = pos;

                if (gravityOn)
                    vel += Vector3.down * gravity * dt;

                pos += vel * dt;
                Vector3 delta = pos - old;

                if (delta.sqrMagnitude > 0.0001f && Physics.Raycast(old, delta.normalized, out RaycastHit hit, delta.magnitude, hitMask, QueryTriggerInteraction.Ignore))
                {
                    impact = hit.point;
                    return true;
                }

                elapsed += dt;
            }

            impact = pos;
            return false;
        }

        private void OnDrawGizmos()
        {
            if (hasGunImpact)
            {
                Gizmos.color = Color.white;
                Gizmos.DrawWireSphere(predictedGunImpact, 6f);
            }

            if (hasRocketImpact)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(predictedRocketImpact, 12f);
            }

            if (hasBombImpact)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(predictedBombImpact, 18f);
            }
        }
    }
}
