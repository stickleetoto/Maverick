using System;
using UnityEngine;

namespace EaglePhysicalAI.Sensors.Fusion
{
    [Serializable]
    public class SensorTelemetryFrame
    {
        public float time;
        public string radarMode;
        public int radarTrackCount;
        public string selectedTrack;
        public string lockedTrack;
        public string podMode;
        public string podTrackedTarget;
        public float podTrackQuality;
        public float podIdentificationConfidence;
        public string fusedTarget;
        public float fusedConfidence;
        public string fusedReason;
        public Vector3 aircraftPosition;
    }
}
