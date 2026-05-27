using System;

namespace EaglePhysicalAI.Data
{
    [Serializable]
    public class TelemetryFrame
    {
        public float time;
        public int frame;
        public string sessionId;

        public AircraftTelemetry aircraft = new AircraftTelemetry();
        public CasTelemetry cas = new CasTelemetry();
        public PlayerActionTelemetry playerAction = new PlayerActionTelemetry();
        public OutcomeTelemetry outcome = new OutcomeTelemetry();
    }

    [Serializable]
    public class AircraftTelemetry
    {
        public float px, py, pz;
        public float rx, ry, rz;
        public float vx, vy, vz;
        public float speed;
        public float forwardSpeed;
        public float altitude;
        public float angleOfAttack;
        public float stallRisk;
        public bool isStalling;
        public bool isCrashed;
        public float pitchInput;
        public float rollInput;
        public float yawInput;
        public float throttle;
    }

    [Serializable]
    public class CasTelemetry
    {
        public bool requestActive;
        public string requestId;
        public string targetId;
        public float targetDistance;
        public bool targetAlive;
        public string validationReason;
        public bool strikeAllowed;
        public float friendlyRisk;
        public float geometryScore;
    }

    [Serializable]
    public class PlayerActionTelemetry
    {
        public float pitchInput;
        public float rollInput;
        public float yawInput;
        public float throttleInput;
        public bool strikePressed;
        public bool confirmPressed;
        public bool abortPressed;
        public string selectedIntent;
    }

    [Serializable]
    public class OutcomeTelemetry
    {
        public float missionScore;
        public int successfulStrikes;
        public int abortedStrikes;
        public int friendlyFireIncidents;
        public bool crashed;
    }
}
