using System;

namespace EaglePhysicalAI.FAM
{
    [Serializable]
    public class FamFlightFrame
    {
        public string schemaVersion = "FAM-MAVERICK-FLIGHT-v0.1";
        public string sessionId;
        public string source = "maverick_manual_flight";
        public int step;
        public float time;
        public float dt;
        public string behaviorToken;
        public FamAircraftState ego = new FamAircraftState();
        public FamControlInput control = new FamControlInput();
        public FamLeaderState leader = new FamLeaderState();
    }

    [Serializable]
    public class FamAircraftState
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
    }

    [Serializable]
    public class FamControlInput
    {
        public float pitch;
        public float roll;
        public float yaw;
        public float throttle;
        public float throttleDelta;
    }

    [Serializable]
    public class FamLeaderState
    {
        public bool available;
        public float relativeForward;
        public float relativeRight;
        public float relativeUp;
        public float relativeVx;
        public float relativeVy;
        public float relativeVz;
        public float distance;
        public float closingRate;
        public float headingDeltaDeg;
    }
}
