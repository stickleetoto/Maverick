using System.Collections.Generic;
using UnityEngine;
using EaglePhysicalAI.Battlefield;
using EaglePhysicalAI.CAS;
using EaglePhysicalAI.Sensors.Radar;
using EaglePhysicalAI.Sensors.TargetingPod;

namespace EaglePhysicalAI.Sensors.Fusion
{
    /// <summary>
    /// Combines radar tracks and targeting pod confirmation into a single game-level target picture.
    /// Designed for AI training and CAS validation, not real sensor fusion.
    /// </summary>
    public class SensorFusionManager : MonoBehaviour
    {
        public F15ERadarSystem radar;
        public TargetingPodSystem targetingPod;
        public AbstractStrikeSystem strikeSystem;
        public CasRequestManager requestManager;

        [Header("Selection")]
        public bool autoSelectCasTarget = true;
        public float minimumFusionConfidenceForAutoSelect = 0.38f;
        public float podConfirmationBonus = 0.35f;
        public float radarTrackBonus = 0.22f;
        public float activeRequestBonus = 0.25f;

        [Header("State")]
        public SensorContact fusedBestContact;
        public GroundUnit fusedBestGroundUnit;
        public float fusedConfidence;
        public string fusedReason = "none";

        private readonly List<SensorContact> _scratch = new List<SensorContact>();

        private void Awake()
        {
            if (radar == null) radar = GetComponent<F15ERadarSystem>();
            if (targetingPod == null) targetingPod = GetComponent<TargetingPodSystem>();
            if (strikeSystem == null) strikeSystem = GetComponent<AbstractStrikeSystem>();
            if (requestManager == null) requestManager = FindObjectOfType<CasRequestManager>();
        }

        private void Update()
        {
            RefreshFusion();
            if (autoSelectCasTarget && fusedBestGroundUnit != null && fusedConfidence >= minimumFusionConfidenceForAutoSelect)
            {
                if (strikeSystem != null) strikeSystem.selectedTarget = fusedBestGroundUnit;
            }
        }

        public void RefreshFusion()
        {
            _scratch.Clear();
            if (radar != null) _scratch.AddRange(radar.GetTracksSnapshot());
            if (targetingPod != null && targetingPod.lastPodContact != null) _scratch.Add(targetingPod.lastPodContact);

            SensorContact best = null;
            GroundUnit bestUnit = null;
            float bestScore = float.MinValue;
            string bestReason = "none";

            foreach (var contact in _scratch)
            {
                if (contact == null || contact.targetTransform == null || !contact.isAlive) continue;
                GroundUnit unit = contact.targetTransform.GetComponentInParent<GroundUnit>();
                if (unit == null || !unit.isAlive) continue;

                float score = ScoreContact(contact, unit, out string reason);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = contact;
                    bestUnit = unit;
                    bestReason = reason;
                }
            }

            fusedBestContact = best;
            fusedBestGroundUnit = bestUnit;
            fusedConfidence = best == null ? 0f : Mathf.Clamp01(bestScore);
            fusedReason = bestReason;
        }

        public bool HasConfirmedCasTarget()
        {
            return fusedBestGroundUnit != null && fusedBestGroundUnit.team == GroundTeam.Hostile && fusedConfidence >= minimumFusionConfidenceForAutoSelect;
        }

        public float GetFriendlyRiskNearFusedTarget(float radius)
        {
            if (fusedBestGroundUnit == null) return 0f;
            Collider[] hits = Physics.OverlapSphere(fusedBestGroundUnit.transform.position, radius);
            float risk = 0f;
            foreach (var hit in hits)
            {
                GroundUnit unit = hit.GetComponentInParent<GroundUnit>();
                if (unit == null || !unit.isAlive) continue;
                if (unit.team == GroundTeam.Friendly || unit.team == GroundTeam.Neutral)
                {
                    float distance = Vector3.Distance(unit.transform.position, fusedBestGroundUnit.transform.position);
                    risk = Mathf.Max(risk, 1f - Mathf.Clamp01(distance / Mathf.Max(1f, radius)));
                }
            }
            return Mathf.Clamp01(risk);
        }

        private float ScoreContact(SensorContact contact, GroundUnit unit, out string reason)
        {
            float score = 0f;
            reason = "base";

            if (unit.team == GroundTeam.Hostile)
            {
                score += 0.22f;
                reason += "+hostile";
            }
            else if (unit.team == GroundTeam.Friendly)
            {
                score -= 0.45f;
                reason += "+friendly_penalty";
            }
            else
            {
                score -= 0.15f;
                reason += "+neutral_penalty";
            }

            if (contact.detectedByRadar)
            {
                score += radarTrackBonus * Mathf.Clamp01(contact.trackQuality);
                reason += "+radar";
            }

            if (contact.detectedByTargetingPod)
            {
                score += podConfirmationBonus * Mathf.Clamp01(contact.identificationConfidence);
                reason += "+pod";
            }

            if (requestManager != null && requestManager.activeRequest != null && requestManager.activeRequest.target == unit)
            {
                score += activeRequestBonus * Mathf.Clamp01(requestManager.activeRequest.priority + 0.2f);
                reason += "+active_cas_request";
            }

            float rangeScore = 0.18f * (1f - Mathf.Clamp01(contact.rangeMeters / 7000f));
            score += rangeScore;
            score += Mathf.Clamp01(unit.importance) * 0.08f;
            return Mathf.Clamp01(score);
        }
    }
}
