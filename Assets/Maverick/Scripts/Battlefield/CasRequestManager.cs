using System.Collections.Generic;
using UnityEngine;

namespace EaglePhysicalAI.Battlefield
{
    public class CasRequestManager : MonoBehaviour
    {
        public List<CasRequest> requests = new List<CasRequest>();
        public CasRequest activeRequest;
        public bool autoSelectHighestPriority = true;

        [Header("Debug Create Request")]
        public GroundUnit debugRequester;
        public GroundUnit debugTarget;
        public KeyCode debugCreateKey = KeyCode.C;

        private int _counter;

        private void Update()
        {
            if (MaverickInput.GetKeyDown(debugCreateKey) && debugTarget != null)
            {
                CreateRequest(debugRequester, debugTarget, debugTarget.transform.position, 1f, "debug_request");
            }

            if (autoSelectHighestPriority)
            {
                activeRequest = GetHighestPriorityActiveRequest();
            }
        }

        public CasRequest CreateRequest(GroundUnit requester, GroundUnit target, Vector3 point, float priority, string note = "")
        {
            var request = new CasRequest
            {
                requestId = $"CAS-{_counter++:0000}",
                requester = requester,
                target = target,
                requestedPoint = point,
                priority = priority,
                createdTime = Time.time,
                note = note,
                active = true
            };
            requests.Add(request);
            activeRequest = request;
            return request;
        }

        public void CompleteRequest(CasRequest request)
        {
            if (request == null) return;
            request.completed = true;
            request.active = false;
            if (activeRequest == request) activeRequest = GetHighestPriorityActiveRequest();
        }

        public void AbortRequest(CasRequest request, string reason)
        {
            if (request == null) return;
            request.aborted = true;
            request.active = false;
            request.note = reason;
            if (activeRequest == request) activeRequest = GetHighestPriorityActiveRequest();
        }

        public CasRequest GetHighestPriorityActiveRequest()
        {
            CasRequest best = null;
            float bestScore = float.MinValue;
            foreach (var request in requests)
            {
                if (request == null || !request.active || request.completed || request.aborted) continue;
                float ageBonus = Mathf.Clamp01((Time.time - request.createdTime) / 60f) * 0.25f;
                float score = request.priority + ageBonus;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = request;
                }
            }
            return best;
        }
    }
}
