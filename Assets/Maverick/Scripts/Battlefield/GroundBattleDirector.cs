using System.Collections.Generic;
using UnityEngine;

namespace EaglePhysicalAI.Battlefield
{
    /// <summary>
    /// Lightweight dynamic battlefield driver. It periodically creates abstract CAS requests
    /// from friendly units against hostile units so the physical AI always has a task stream.
    /// </summary>
    public class GroundBattleDirector : MonoBehaviour
    {
        public CasRequestManager requestManager;
        public bool autoCreateRequests = true;
        public float requestInterval = 18f;
        public float minDistanceBetweenRequesterAndTarget = 80f;
        public float maxDistanceBetweenRequesterAndTarget = 2500f;
        public int maxActiveRequests = 3;
        public string generatedNote = "auto_ground_battle_request";

        public List<GroundUnit> friendlyUnits = new List<GroundUnit>();
        public List<GroundUnit> hostileUnits = new List<GroundUnit>();

        private float _nextRequestTime;

        private void Awake()
        {
            if (requestManager == null) requestManager = FindObjectOfType<CasRequestManager>();
        }

        private void Start()
        {
            RefreshUnits();
            _nextRequestTime = Time.time + 2f;
        }

        private void Update()
        {
            if (!autoCreateRequests || requestManager == null) return;
            if (Time.time < _nextRequestTime) return;
            _nextRequestTime = Time.time + requestInterval;

            RefreshUnits();
            if (CountActiveRequests() >= maxActiveRequests) return;
            TryCreateRequest();
        }

        [ContextMenu("Refresh Units")]
        public void RefreshUnits()
        {
            friendlyUnits.Clear();
            hostileUnits.Clear();
            GroundUnit[] units = FindObjectsOfType<GroundUnit>();
            foreach (GroundUnit unit in units)
            {
                if (unit == null || !unit.isAlive) continue;
                if (unit.team == GroundTeam.Friendly) friendlyUnits.Add(unit);
                if (unit.team == GroundTeam.Hostile) hostileUnits.Add(unit);
            }
        }

        [ContextMenu("Create Request Now")]
        public void TryCreateRequest()
        {
            GroundUnit requester = PickRandomAlive(friendlyUnits);
            GroundUnit target = PickBestHostileNearFriendlies(requester);
            if (requester == null || target == null) return;

            float priority = Mathf.Clamp01(1f - Vector3.Distance(requester.transform.position, target.transform.position) / maxDistanceBetweenRequesterAndTarget);
            requestManager.CreateRequest(requester, target, target.transform.position, Mathf.Max(0.25f, priority), generatedNote);
        }

        private GroundUnit PickBestHostileNearFriendlies(GroundUnit requester)
        {
            if (requester == null) return PickRandomAlive(hostileUnits);
            GroundUnit best = null;
            float bestScore = float.MinValue;
            foreach (GroundUnit hostile in hostileUnits)
            {
                if (hostile == null || !hostile.isAlive) continue;
                float distance = Vector3.Distance(requester.transform.position, hostile.transform.position);
                if (distance < minDistanceBetweenRequesterAndTarget || distance > maxDistanceBetweenRequesterAndTarget) continue;
                float score = 1f / Mathf.Max(1f, distance) + Random.value * 0.05f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = hostile;
                }
            }
            return best;
        }

        private GroundUnit PickRandomAlive(List<GroundUnit> units)
        {
            if (units == null || units.Count == 0) return null;
            for (int i = 0; i < 16; i++)
            {
                GroundUnit unit = units[Random.Range(0, units.Count)];
                if (unit != null && unit.isAlive) return unit;
            }
            return null;
        }

        private int CountActiveRequests()
        {
            int count = 0;
            foreach (CasRequest request in requestManager.requests)
            {
                if (request != null && request.active && !request.completed && !request.aborted) count++;
            }
            return count;
        }
    }
}
