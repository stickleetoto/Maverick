using UnityEngine;
using EaglePhysicalAI.Battlefield;

namespace EaglePhysicalAI.Sensors.Radar
{
    public enum SensorContactKind
    {
        Unknown,
        Air,
        Ground,
        CasTarget,
        Friendly,
        Hostile,
        Neutral
    }

    [System.Serializable]
    public class SensorContact
    {
        public string contactId;
        public string displayName;
        public SensorContactKind kind;
        public GroundTeam team;
        public Transform targetTransform;
        public Vector3 worldPosition;
        public Vector3 localDirection;
        public float rangeMeters;
        public float bearingDegrees;
        public float elevationDegrees;
        public float closureRate;
        public float signalStrength;
        public float trackQuality;
        public float identificationConfidence;
        public float lastSeenTime;
        public float firstSeenTime;
        public bool detectedByRadar;
        public bool detectedByTargetingPod;
        public bool isSelected;
        public bool isLocked;
        public bool isAlive = true;

        public bool IsHostile => team == GroundTeam.Hostile;
        public bool IsFriendly => team == GroundTeam.Friendly;
        public bool IsGround => kind == SensorContactKind.Ground || kind == SensorContactKind.CasTarget;
        public bool IsAir => kind == SensorContactKind.Air;

        public float TrackAge => Mathf.Max(0f, Time.time - firstSeenTime);
        public float Staleness => Mathf.Max(0f, Time.time - lastSeenTime);

        public void CopyDynamicFrom(SensorContact other)
        {
            if (other == null) return;
            displayName = other.displayName;
            kind = other.kind;
            team = other.team;
            targetTransform = other.targetTransform;
            worldPosition = other.worldPosition;
            localDirection = other.localDirection;
            rangeMeters = other.rangeMeters;
            bearingDegrees = other.bearingDegrees;
            elevationDegrees = other.elevationDegrees;
            closureRate = other.closureRate;
            signalStrength = other.signalStrength;
            trackQuality = other.trackQuality;
            identificationConfidence = other.identificationConfidence;
            lastSeenTime = other.lastSeenTime;
            detectedByRadar = detectedByRadar || other.detectedByRadar;
            detectedByTargetingPod = detectedByTargetingPod || other.detectedByTargetingPod;
            isAlive = other.isAlive;
        }
    }
}
