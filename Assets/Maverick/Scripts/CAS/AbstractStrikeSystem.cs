using UnityEngine;
using EaglePhysicalAI.Battlefield;

namespace EaglePhysicalAI.CAS
{
    public class AbstractStrikeSystem : MonoBehaviour
    {
        public CasValidator validator;
        public CasRequestManager requestManager;
        public GroundUnit selectedTarget;

        [Header("Abstract Game Strike")]
        public float effectRadius = 90f;
        public float damage = 100f;
        public LayerMask groundUnitMask = ~0;
        public float cooldownSeconds = 4f;
        public bool requireValidation = true;

        [Header("Debug")]
        public CasValidationResult lastValidation;
        public float lastStrikeTime = -999f;
        public int successfulStrikes;
        public int abortedStrikes;
        public int friendlyFireIncidents;

        public bool CanAttemptStrike => Time.time - lastStrikeTime >= cooldownSeconds;

        private void Awake()
        {
            if (validator == null) validator = FindObjectOfType<CasValidator>();
            if (requestManager == null) requestManager = FindObjectOfType<CasRequestManager>();
        }

        private void Update()
        {
            if (MaverickInput.GetKeyDown(KeyCode.Space))
            {
                TryStrike(selectedTarget);
            }
        }

        public bool TryStrike(GroundUnit targetOverride = null)
        {
            if (!CanAttemptStrike)
            {
                abortedStrikes++;
                return false;
            }

            GroundUnit target = targetOverride != null ? targetOverride : selectedTarget;
            if (target == null && requestManager != null && requestManager.activeRequest != null)
            {
                target = requestManager.activeRequest.target;
            }

            if (validator != null)
            {
                lastValidation = validator.ValidateStrike(transform, target);
                if (requireValidation && !lastValidation.allowed)
                {
                    abortedStrikes++;
                    return false;
                }
            }

            if (target == null)
            {
                abortedStrikes++;
                return false;
            }

            lastStrikeTime = Time.time;
            ApplyAbstractEffect(target.transform.position);
            successfulStrikes++;

            if (requestManager != null && requestManager.activeRequest != null && requestManager.activeRequest.target == target)
            {
                requestManager.CompleteRequest(requestManager.activeRequest);
            }

            return true;
        }

        private void ApplyAbstractEffect(Vector3 center)
        {
            Collider[] hits = Physics.OverlapSphere(center, effectRadius, groundUnitMask);
            foreach (var hit in hits)
            {
                GroundUnit unit = hit.GetComponentInParent<GroundUnit>();
                if (unit == null || !unit.isAlive) continue;

                float distance = Vector3.Distance(center, unit.transform.position);
                float falloff = 1f - Mathf.Clamp01(distance / effectRadius);
                float appliedDamage = damage * falloff;
                unit.ApplyAbstractDamage(appliedDamage);

                if (unit.team == GroundTeam.Friendly || unit.team == GroundTeam.Neutral)
                {
                    friendlyFireIncidents++;
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            GroundUnit target = selectedTarget != null ? selectedTarget : requestManager?.activeRequest?.target;
            if (target != null) Gizmos.DrawWireSphere(target.transform.position, effectRadius);
        }
    }
}
