using System.Collections.Generic;
using UnityEngine;

namespace MaverickFresh
{
    /// <summary>
    /// Lightweight targeting pod / CAS designation system.
    /// It uses camera ray + mouse aim / viewport center to find and designate ground targets.
    /// </summary>
    [DisallowMultipleComponent]
    public class MavCASTargetingSystem : MonoBehaviour
    {
        [Header("References")]
        public MavMouseFlightRig rig;
        public Camera playerCamera;

        [Header("Controls")]
        public KeyCode designateKey = KeyCode.F;
        public KeyCode cycleTargetKey = KeyCode.Tab;
        public KeyCode clearTargetKey = KeyCode.Backspace;

        [Header("Search")]
        public float maxDesignateDistance = 6000f;
        public float maxScreenDistance = 0.13f;
        public float autoScanInterval = 0.25f;
        public LayerMask raycastMask = ~0;
        public bool allowRaycastGroundPoint = true;

        [Header("State")]
        public MavCASTarget designatedTarget;
        public Vector3 designatedPoint;
        public bool hasDesignatedPoint;
        public MavCASTarget candidateTarget;
        public Vector3 aimGroundPoint;
        public bool hasAimGroundPoint;
        public string status = "ready";

        private readonly List<MavCASTarget> targets = new List<MavCASTarget>();
        private float nextScanTime;
        private int cycleIndex = -1;

        private void Awake()
        {
            Resolve();
        }

        private void Update()
        {
            Resolve();

            if (Time.time >= nextScanTime)
            {
                ScanTargets();
                PickCandidateFromScreen();
                UpdateGroundPoint();
                nextScanTime = Time.time + autoScanInterval;
            }

            if (MavFreshInput.GetKeyDown(designateKey))
                DesignateCandidateOrPoint();

            if (MavFreshInput.GetKeyDown(cycleTargetKey))
                CycleTarget();

            if (MavFreshInput.GetKeyDown(clearTargetKey))
                ClearDesignation();
        }

        public void Resolve()
        {
            if (rig == null) rig = FindObjectOfType<MavMouseFlightRig>();
            if (playerCamera == null) playerCamera = Camera.main;
        }

        public void ScanTargets()
        {
            targets.Clear();
            MavCASTarget[] all = FindObjectsOfType<MavCASTarget>();

            foreach (MavCASTarget t in all)
            {
                if (t != null && t.IsAlive())
                    targets.Add(t);
            }
        }

        public void PickCandidateFromScreen()
        {
            candidateTarget = null;

            if (playerCamera == null || targets.Count == 0)
                return;

            Vector2 aimViewport = rig != null ? rig.cursorViewport : new Vector2(0.5f, 0.5f);
            float best = float.MaxValue;

            foreach (MavCASTarget t in targets)
            {
                Vector3 vp = playerCamera.WorldToViewportPoint(t.transform.position + Vector3.up * 2f);
                if (vp.z <= 0f)
                    continue;

                if (Vector3.Distance(transform.position, t.transform.position) > maxDesignateDistance)
                    continue;

                Vector2 p = new Vector2(vp.x, vp.y);
                float d = Vector2.Distance(p, aimViewport);

                if (d < best && d <= maxScreenDistance)
                {
                    best = d;
                    candidateTarget = t;
                }
            }

            status = candidateTarget != null ? "candidate_" + candidateTarget.displayName : "no_candidate";
        }

        public void UpdateGroundPoint()
        {
            hasAimGroundPoint = false;

            if (!allowRaycastGroundPoint || playerCamera == null)
                return;

            Vector2 aimViewport = rig != null ? rig.cursorViewport : new Vector2(0.5f, 0.5f);
            Ray ray = playerCamera.ViewportPointToRay(new Vector3(aimViewport.x, aimViewport.y, 0f));

            if (Physics.Raycast(ray, out RaycastHit hit, maxDesignateDistance, raycastMask, QueryTriggerInteraction.Ignore))
            {
                aimGroundPoint = hit.point;
                hasAimGroundPoint = true;
            }
        }

        public bool DesignateCandidateOrPoint()
        {
            if (candidateTarget != null)
            {
                designatedTarget = candidateTarget;
                designatedPoint = designatedTarget.transform.position;
                hasDesignatedPoint = true;
                status = "designated_" + designatedTarget.displayName;
                return true;
            }

            if (hasAimGroundPoint)
            {
                designatedTarget = null;
                designatedPoint = aimGroundPoint;
                hasDesignatedPoint = true;
                status = "designated_point";
                return true;
            }

            status = "designate_failed";
            return false;
        }

        public void CycleTarget()
        {
            ScanTargets();

            if (targets.Count == 0)
            {
                designatedTarget = null;
                hasDesignatedPoint = false;
                status = "cycle_no_targets";
                return;
            }

            cycleIndex = (cycleIndex + 1) % targets.Count;
            designatedTarget = targets[cycleIndex];
            designatedPoint = designatedTarget.transform.position;
            hasDesignatedPoint = true;
            status = "cycle_" + designatedTarget.displayName;
        }

        public void ClearDesignation()
        {
            designatedTarget = null;
            hasDesignatedPoint = false;
            status = "clear";
        }

        public Vector3 GetBestStrikePoint()
        {
            if (designatedTarget != null && designatedTarget.IsAlive())
                return designatedTarget.transform.position;

            if (hasDesignatedPoint)
                return designatedPoint;

            if (hasAimGroundPoint)
                return aimGroundPoint;

            return transform.position + transform.forward * 1000f;
        }

        public MavCASTarget GetDesignatedOrCandidateTarget()
        {
            if (designatedTarget != null && designatedTarget.IsAlive())
                return designatedTarget;

            if (candidateTarget != null && candidateTarget.IsAlive())
                return candidateTarget;

            return null;
        }

        private void OnDrawGizmos()
        {
            if (hasDesignatedPoint)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(designatedPoint + Vector3.up * 3f, 16f);
                Gizmos.DrawLine(designatedPoint, designatedPoint + Vector3.up * 35f);
            }

            if (candidateTarget != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(candidateTarget.transform.position + Vector3.up * 6f, 14f);
            }
        }
    }
}
