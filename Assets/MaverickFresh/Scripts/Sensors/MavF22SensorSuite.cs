using System.Collections.Generic;
using UnityEngine;

namespace MaverickFresh
{
    public enum MavSensorMode
    {
        RWS = 0,
        TWS = 1,
        STT = 2,
        IRST = 3
    }

    /// <summary>
    /// v0.20.9 F-22 primary aircraft sensor prototype.
    /// This is a gameplay radar/IRST abstraction: it gives stealth aircraft a useful identity
    /// without trying to be a real-world radar model.
    /// </summary>
    [DisallowMultipleComponent]
    public class MavF22SensorSuite : MonoBehaviour
    {
        [Header("Sensor Suite")]
        public bool useSensorSuite = true;
        public MavSensorMode mode = MavSensorMode.TWS;
        public int ownTeam = 0;
        public float radarRangeMeters = 12000f;
        public float radarFovDeg = 70f;
        public float radarSensitivity = 1.0f;
        public float irstRangeMeters = 5500f;
        public float scanInterval = 0.20f;
        public float sttLockTime = 1.15f;

        [Header("Controls")]
        public KeyCode cycleTargetKey = KeyCode.X;
        public KeyCode sensorModeKey = KeyCode.Z;
        public KeyCode clearLockKey = KeyCode.Backspace;
        public bool allowInput = true;

        [Header("Debug")]
        public MavRadarSignature selectedTarget;
        public int debugContactCount;
        public string debugSelectedName = "NONE";
        public float debugSelectedDistance;
        public float debugSelectedAspectDeg;
        public float debugSelectedQuality;
        public bool debugHasLock;
        public float debugLockProgress01;
        public string debugModeLabel = "TWS";

        private readonly List<MavRadarSignature> contacts = new List<MavRadarSignature>();
        private float nextScanTime;
        private float lockTimer;
        private int selectedIndex = -1;

        private void Update()
        {
            if (allowInput)
            {
                if (MavFreshInput.GetKeyDown(sensorModeKey))
                    CycleMode();
                if (MavFreshInput.GetKeyDown(cycleTargetKey))
                    CycleTarget();
                if (MavFreshInput.GetKeyDown(clearLockKey))
                    ClearLock();
            }

            if (Time.time >= nextScanTime)
            {
                nextScanTime = Time.time + Mathf.Max(0.04f, scanInterval);
                Scan();
            }

            UpdateLockState();
        }

        public IReadOnlyList<MavRadarSignature> Contacts => contacts;

        public void ApplyProfile(MavAircraftRuntimeProfile profile)
        {
            if (profile == null)
                return;

            useSensorSuite = profile.useSensorSuite;
            radarRangeMeters = profile.radarRangeMeters;
            radarFovDeg = profile.radarFovDeg;
            radarSensitivity = profile.radarSensitivity;
            irstRangeMeters = profile.irstRangeMeters;
            scanInterval = Mathf.Max(0.05f, profile.sensorRefreshSeconds);
            mode = profile.aircraft == MavAircraftKind.F22A ? MavSensorMode.TWS : MavSensorMode.RWS;
            debugModeLabel = mode.ToString();
        }

        public void CycleMode()
        {
            mode = (MavSensorMode)(((int)mode + 1) % 4);
            debugModeLabel = mode.ToString();
            lockTimer = 0f;
        }

        public void CycleTarget()
        {
            if (contacts.Count == 0)
            {
                selectedTarget = null;
                selectedIndex = -1;
                return;
            }

            selectedIndex++;
            if (selectedIndex >= contacts.Count)
                selectedIndex = 0;
            selectedTarget = contacts[selectedIndex];
            lockTimer = 0f;
            RefreshSelectedDebug();
        }

        public void ClearLock()
        {
            selectedTarget = null;
            selectedIndex = -1;
            lockTimer = 0f;
            debugHasLock = false;
            debugLockProgress01 = 0f;
            debugSelectedName = "NONE";
        }

        private void Scan()
        {
            contacts.Clear();
            debugContactCount = 0;

            if (!useSensorSuite)
            {
                selectedTarget = null;
                selectedIndex = -1;
                RefreshSelectedDebug();
                return;
            }

            MavRadarSignature[] all = FindObjectsOfType<MavRadarSignature>();
            for (int i = 0; i < all.Length; i++)
            {
                MavRadarSignature sig = all[i];
                if (sig == null || !sig.CanBeDetected())
                    continue;
                if (sig.transform == transform || sig.transform.IsChildOf(transform))
                    continue;
                if (sig.team == ownTeam)
                    continue;

                float quality = DetectionQuality(sig);
                if (quality <= 0f)
                    continue;

                sig.debugLastDetectionQuality = quality;
                contacts.Add(sig);
            }

            contacts.Sort((a, b) => DetectionQuality(b).CompareTo(DetectionQuality(a)));
            debugContactCount = contacts.Count;

            if (contacts.Count == 0)
            {
                selectedTarget = null;
                selectedIndex = -1;
                lockTimer = 0f;
            }
            else if (selectedTarget == null || !contacts.Contains(selectedTarget))
            {
                selectedIndex = 0;
                selectedTarget = contacts[0];
                lockTimer = 0f;
            }

            RefreshSelectedDebug();
        }

        private float DetectionQuality(MavRadarSignature sig)
        {
            Vector3 to = sig.transform.position - transform.position;
            float dist = to.magnitude;
            if (dist <= 1f)
                return 0f;

            float aspect = Vector3.Angle(transform.forward, to / dist);
            bool inRadarCone = aspect <= radarFovDeg * 0.5f;

            float rcs = Mathf.Max(0.001f, sig.radarCrossSectionSqm);
            float stealthPenalty = Mathf.Clamp01(1f - sig.stealthRating * 0.055f);
            float jammingPenalty = 1f / Mathf.Max(1f, 1f + sig.radarJamming);
            float rcsFactor = Mathf.Clamp(Mathf.Sqrt(rcs / 5f), 0.08f, 2.5f);
            float radarEffectiveRange = radarRangeMeters * radarSensitivity * rcsFactor * stealthPenalty * jammingPenalty;

            float radarQuality = 0f;
            if (inRadarCone && dist <= radarEffectiveRange)
            {
                float rangeQ = 1f - Mathf.Clamp01(dist / Mathf.Max(1f, radarEffectiveRange));
                float angleQ = 1f - Mathf.Clamp01(aspect / Mathf.Max(1f, radarFovDeg * 0.5f));
                radarQuality = Mathf.Clamp01(rangeQ * 0.72f + angleQ * 0.28f);
            }

            float irEffectiveRange = irstRangeMeters * Mathf.Clamp(sig.irSignature, 0.1f, 3.5f);
            float irQuality = 0f;
            if ((mode == MavSensorMode.IRST || mode == MavSensorMode.TWS) && dist <= irEffectiveRange)
                irQuality = 0.55f * (1f - Mathf.Clamp01(dist / Mathf.Max(1f, irEffectiveRange)));

            sig.debugLastDistance = dist;
            sig.debugLastAspectDeg = aspect;

            return Mathf.Max(radarQuality, irQuality);
        }

        private void UpdateLockState()
        {
            if (selectedTarget == null || !contacts.Contains(selectedTarget))
            {
                debugHasLock = false;
                debugLockProgress01 = 0f;
                return;
            }

            bool canHardLock = mode == MavSensorMode.STT || mode == MavSensorMode.TWS;
            if (canHardLock)
                lockTimer += Time.deltaTime;
            else
                lockTimer = Mathf.Max(0f, lockTimer - Time.deltaTime * 1.5f);

            debugLockProgress01 = Mathf.Clamp01(lockTimer / Mathf.Max(0.05f, sttLockTime));
            debugHasLock = debugLockProgress01 >= 1f;
        }

        private void RefreshSelectedDebug()
        {
            if (selectedTarget == null)
            {
                debugSelectedName = "NONE";
                debugSelectedDistance = 0f;
                debugSelectedAspectDeg = 0f;
                debugSelectedQuality = 0f;
                return;
            }

            debugSelectedName = string.IsNullOrWhiteSpace(selectedTarget.displayName) ? selectedTarget.name : selectedTarget.displayName;
            debugSelectedDistance = Vector3.Distance(transform.position, selectedTarget.transform.position);
            Vector3 to = selectedTarget.transform.position - transform.position;
            debugSelectedAspectDeg = to.sqrMagnitude > 0.1f ? Vector3.Angle(transform.forward, to.normalized) : 0f;
            debugSelectedQuality = DetectionQuality(selectedTarget);
            debugModeLabel = mode.ToString();
        }
    }
}
