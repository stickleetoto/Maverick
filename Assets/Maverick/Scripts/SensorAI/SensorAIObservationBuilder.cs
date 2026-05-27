using UnityEngine;
using EaglePhysicalAI.Battlefield;
using EaglePhysicalAI.CAS;
using EaglePhysicalAI.Sensors.Fusion;
using EaglePhysicalAI.Sensors.Radar;
using EaglePhysicalAI.Sensors.TargetingPod;

namespace EaglePhysicalAI.SensorAI
{
    public class SensorAIObservationBuilder : MonoBehaviour
    {
        [Header("References")]
        public F15ERadarSystem radar;
        public TargetingPodSystem targetingPod;
        public SensorFusionManager fusion;
        public CasRequestManager requestManager;
        public AbstractStrikeSystem strikeSystem;

        [Header("Normalization")]
        public float maxRadarRange = 8500f;
        public float maxTrackCount = 16f;
        public float minPodFov = 8f;
        public float maxPodFov = 55f;
        public float friendlyRiskRadius = 260f;

        [Header("Debug")]
        public SensorAIObservation lastObservation = new SensorAIObservation();

        private void Awake()
        {
            if (radar == null) radar = FindObjectOfType<F15ERadarSystem>();
            if (targetingPod == null) targetingPod = FindObjectOfType<TargetingPodSystem>();
            if (fusion == null) fusion = FindObjectOfType<SensorFusionManager>();
            if (requestManager == null) requestManager = FindObjectOfType<CasRequestManager>();
            if (strikeSystem == null) strikeSystem = FindObjectOfType<AbstractStrikeSystem>();
        }

        public SensorAIObservation Build()
        {
            var obs = new SensorAIObservation();

            SensorContact selected = radar != null ? radar.selectedTrack : null;
            SensorContact locked = radar != null ? radar.lockedTrack : null;
            GroundUnit requestTarget = requestManager != null && requestManager.activeRequest != null
                ? requestManager.activeRequest.target
                : null;

            obs[0] = radar != null && radar.radarOn ? 1f : 0f;
            obs[1] = radar != null ? EnumNorm((int)radar.mode, 6) : 0f;
            obs[2] = radar != null ? Mathf.Clamp01(radar.tracks.Count / Mathf.Max(1f, maxTrackCount)) : 0f;
            obs[3] = selected != null ? 1f : 0f;
            obs[4] = selected != null ? Mathf.Clamp01(selected.rangeMeters / Mathf.Max(1f, maxRadarRange)) : 1f;
            obs[5] = selected != null ? Mathf.Clamp(selected.bearingDegrees / 180f, -1f, 1f) : 0f;
            obs[6] = selected != null ? Mathf.Clamp01(selected.trackQuality) : 0f;
            obs[7] = selected != null ? Mathf.Clamp01(selected.identificationConfidence) : 0f;
            obs[8] = locked != null ? 1f : 0f;
            obs[9] = locked != null ? Mathf.Clamp01(locked.rangeMeters / Mathf.Max(1f, maxRadarRange)) : 1f;
            obs[10] = locked != null ? Mathf.Clamp01(locked.trackQuality) : 0f;

            obs[11] = targetingPod != null && targetingPod.podOn ? 1f : 0f;
            obs[12] = targetingPod != null ? EnumNorm((int)targetingPod.mode, 5) : 0f;
            obs[13] = targetingPod != null && targetingPod.trackedTarget != null ? 1f : 0f;
            obs[14] = targetingPod != null ? Mathf.Clamp01(targetingPod.targetTrackQuality) : 0f;
            obs[15] = targetingPod != null ? Mathf.Clamp01(targetingPod.identificationConfidence) : 0f;
            obs[16] = targetingPod != null && targetingPod.hasLineOfSight ? 1f : 0f;
            obs[17] = targetingPod != null && targetingPod.podCamera != null
                ? Mathf.InverseLerp(maxPodFov, minPodFov, targetingPod.podCamera.fieldOfView)
                : 0f;

            if (fusion != null) fusion.RefreshFusion();
            obs[18] = fusion != null && fusion.fusedBestGroundUnit != null ? 1f : 0f;
            obs[19] = fusion != null ? Mathf.Clamp01(fusion.fusedConfidence) : 0f;
            obs[20] = fusion != null ? Mathf.Clamp01(fusion.GetFriendlyRiskNearFusedTarget(friendlyRiskRadius)) : 0f;

            bool requestActive = requestManager != null && requestManager.activeRequest != null && requestManager.activeRequest.active;
            obs[21] = requestActive ? 1f : 0f;
            obs[22] = requestActive ? Mathf.Clamp01(requestManager.activeRequest.priority) : 0f;
            obs[23] = strikeSystem != null && strikeSystem.CanAttemptStrike ? 1f : 0f;
            obs[24] = targetingPod != null && targetingPod.designatedGroundUnit != null ? 1f : 0f;

            GroundUnit fused = fusion != null ? fusion.fusedBestGroundUnit : null;
            GroundUnit currentTarget = fused != null ? fused : requestTarget;
            obs[25] = currentTarget != null && currentTarget.team == GroundTeam.Hostile ? 1f : 0f;

            Transform targetTransform = currentTarget != null ? currentTarget.transform : null;
            if (targetTransform != null)
            {
                Vector3 toTarget = targetTransform.position - transform.position;
                obs[26] = Mathf.Clamp(Vector3.Dot(transform.forward, toTarget.normalized), -1f, 1f);
            }

            bool readyToStrike = obs[18] > 0.5f && obs[19] > 0.55f && obs[20] < 0.35f && obs[23] > 0.5f && obs[25] > 0.5f;
            obs[27] = readyToStrike ? 1f : 0f;

            obs.ClampAll();
            lastObservation = obs;
            return obs;
        }

        private static float EnumNorm(int value, int maxInclusive)
        {
            if (maxInclusive <= 0) return 0f;
            return Mathf.Clamp01(value / (float)maxInclusive);
        }
    }
}
