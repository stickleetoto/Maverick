using System;
using UnityEngine;

namespace EaglePhysicalAI.Battlefield
{
    [Serializable]
    public class CasRequest
    {
        public string requestId;
        public GroundUnit requester;
        public GroundUnit target;
        public Vector3 requestedPoint;
        public float priority;
        public float createdTime;
        public bool active = true;
        public bool completed;
        public bool aborted;
        public string note;
    }
}
