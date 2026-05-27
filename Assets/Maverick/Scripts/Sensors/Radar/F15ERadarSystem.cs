using System.Collections.Generic;
using UnityEngine;
using EaglePhysicalAI.Battlefield;

namespace EaglePhysicalAI.Sensors.Radar
{
    /// <summary>
    /// F-15E-inspired game radar. This simulates sensor feel, track quality, and target selection.
    /// It intentionally avoids real-world radar implementation details and should be tuned for gameplay.
    /// </summary>
    public class F15ERadarSystem : MonoBehaviour
    {
        [Header("Mode")]
        public F15ERadarMode mode = F15ERadarMode.TrackWhileScan;
        public bool radarOn = true;
        public bool autoSelectBestTrack = true;
        public KeyCode cycleModeKey = KeyCode.R;
        public KeyCode nextTrackKey = KeyCode.T;
        public KeyCode lockKey = KeyCode.O;
        public KeyCode unlockKey = KeyCode.U;

        [Header("Scan Shape - Gameplay Tuned")]
        public float maxAirSearchRange = 8500f;
        public float maxGroundSearchRange = 6200f;
        public float azimuthHalfAngle = 60f;
        public float elevationHalfAngle = 35f;
        public float scanInterval = 0.18f;
        public LayerMask sensorMask = ~0;
        public bool requireLineOfSight = false;
        public LayerMask lineOfSightBlockMask = 0;

        [Header("Track Behavior")]
        public int maxTracks = 16;
        public float trackMemorySeconds = 4.0f;
        public float lockBreakQuality = 0.18f;
        public float trackQualityRiseRate = 2.0f;
        public float trackQualityDecayRate = 0.35f;
        public float identificationRiseRate = 0.65f;
        public float radarNoise = 0.04f;

        [Header("Debug")]
        public List<SensorContact> tracks = new List<SensorContact>();
        public SensorContact selectedTrack;
        public SensorContact lockedTrack;
        public float lastScanTime;
        public int scanCount;
        public bool drawGizmos = true;

        private readonly Collider[] _hits = new Collider[256];

        private void Update()
        {
            HandleDebugKeys();

            if (radarOn && Time.time - lastScanTime >= scanInterval)
            {
                Scan();
            }

            DecayTracks();
            CleanTracks();

            if (autoSelectBestTrack && selectedTrack == null)
            {
                selectedTrack = GetBestTrack();
            }

            if (lockedTrack != null && lockedTrack.trackQuality < lockBreakQuality)
            {
                lockedTrack.isLocked = false;
                lockedTrack = null;
                if (mode == F15ERadarMode.SingleTargetTrack) mode = F15ERadarMode.TrackWhileScan;
            }
        }

        public void SetMode(F15ERadarMode newMode)
        {
            mode = newMode;
            if (mode == F15ERadarMode.Standby)
            {
                lockedTrack = null;
            }
        }

        public void CycleMode()
        {
            if (mode == F15ERadarMode.Standby) mode = F15ERadarMode.AirSearch;
            else if (mode == F15ERadarMode.AirSearch) mode = F15ERadarMode.TrackWhileScan;
            else if (mode == F15ERadarMode.TrackWhileScan) mode = F15ERadarMode.GroundMap;
            else if (mode == F15ERadarMode.GroundMap) mode = F15ERadarMode.GroundMovingTarget;
            else if (mode == F15ERadarMode.GroundMovingTarget) mode = F15ERadarMode.CasTargetCue;
            else mode = F15ERadarMode.Standby;
        }

        public void SelectNextTrack()
        {
            if (tracks.Count == 0)
            {
                selectedTrack = null;
                return;
            }

            int current = selectedTrack == null ? -1 : tracks.IndexOf(selectedTrack);
            int next = (current + 1) % tracks.Count;
            SelectTrack(tracks[next]);
        }

        public void SelectTrack(SensorContact track)
        {
            foreach (var t in tracks) if (t != null) t.isSelected = false;
            selectedTrack = track;
            if (selectedTrack != null) selectedTrack.isSelected = true;
        }

        public bool LockSelectedTrack()
        {
            if (selectedTrack == null) return false;
            lockedTrack = selectedTrack;
            lockedTrack.isLocked = true;
            mode = F15ERadarMode.SingleTargetTrack;
            return true;
        }

        public void Unlock()
        {
            if (lockedTrack != null) lockedTrack.isLocked = false;
            lockedTrack = null;
            if (mode == F15ERadarMode.SingleTargetTrack) mode = F15ERadarMode.TrackWhileScan;
        }

        public SensorContact GetBestTrack()
        {
            SensorContact best = null;
            float bestScore = float.MinValue;
            foreach (var track in tracks)
            {
                if (track == null || !track.isAlive) continue;
                float hostileBonus = track.IsHostile ? 1.25f : 0f;
                float rangeScore = 1f - Mathf.Clamp01(track.rangeMeters / CurrentMaxRange());
                float score = track.trackQuality * 2f + track.signalStrength + hostileBonus + rangeScore;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = track;
                }
            }
            return best;
        }

        public List<SensorContact> GetTracksSnapshot()
        {
            return new List<SensorContact>(tracks);
        }

        private void Scan()
        {
            lastScanTime = Time.time;
            scanCount++;

            if (mode == F15ERadarMode.Standby || !radarOn) return;

            float maxRange = CurrentMaxRange();
            int count = Physics.OverlapSphereNonAlloc(transform.position, maxRange, _hits, sensorMask, QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                Collider hit = _hits[i];
                if (hit == null) continue;
                RadarSignature signature = hit.GetComponentInParent<RadarSignature>();
                if (signature == null || !signature.canBeDetectedByRadar || !signature.IsAlive) continue;
                if (signature.transform == transform || signature.transform.IsChildOf(transform)) continue;

                SensorContact candidate = BuildCandidate(signature);
                if (candidate == null) continue;
                if (!ModeAllows(candidate)) continue;
                if (!InsideScanCone(candidate)) continue;
                if (requireLineOfSight && !HasLineOfSight(signature.AimPoint)) continue;

                float detectionScore = ComputeDetectionScore(signature, candidate);
                if (detectionScore <= 0.05f) continue;

                candidate.signalStrength = Mathf.Clamp01(detectionScore);
                candidate.trackQuality = Mathf.Clamp01(detectionScore * 0.75f);
                candidate.identificationConfidence = Mathf.Clamp01(detectionScore * (1f - signature.identificationDifficulty));
                candidate.detectedByRadar = true;
                UpsertTrack(candidate, signature);
            }
        }

        private SensorContact BuildCandidate(RadarSignature signature)
        {
            Vector3 aimPoint = signature.AimPoint;
            Vector3 toTarget = aimPoint - transform.position;
            float range = toTarget.magnitude;
            if (range < 1f) return null;

            Vector3 local = transform.InverseTransformDirection(toTarget.normalized);
            float bearing = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            float elevation = Mathf.Asin(Mathf.Clamp(local.y, -1f, 1f)) * Mathf.Rad2Deg;

            float closure = 0f;
            Rigidbody ownBody = GetComponentInParent<Rigidbody>();
            Rigidbody targetBody = signature.Body;
            if (ownBody != null && targetBody != null)
            {
                Vector3 relativeVelocity = targetBody.linearVelocity - ownBody.linearVelocity;
                closure = -Vector3.Dot(relativeVelocity, toTarget.normalized);
            }

            var contact = new SensorContact
            {
                contactId = signature.signatureId,
                displayName = signature.ResolveDisplayName(),
                kind = signature.kind,
                team = signature.ResolveTeam(),
                targetTransform = signature.transform,
                worldPosition = aimPoint,
                localDirection = local,
                rangeMeters = range,
                bearingDegrees = bearing,
                elevationDegrees = elevation,
                closureRate = closure,
                lastSeenTime = Time.time,
                firstSeenTime = Time.time,
                isAlive = signature.IsAlive
            };

            if (contact.kind == SensorContactKind.Unknown && signature.GroundUnit != null) contact.kind = SensorContactKind.Ground;
            return contact;
        }

        private void UpsertTrack(SensorContact candidate, RadarSignature signature)
        {
            SensorContact existing = FindTrack(candidate.contactId, candidate.targetTransform);
            if (existing == null)
            {
                if (tracks.Count >= maxTracks) DropWorstTrack();
                candidate.firstSeenTime = Time.time;
                tracks.Add(candidate);
                existing = candidate;
            }
            else
            {
                float oldQuality = existing.trackQuality;
                float oldId = existing.identificationConfidence;
                existing.CopyDynamicFrom(candidate);
                existing.trackQuality = Mathf.Clamp01(Mathf.MoveTowards(oldQuality, Mathf.Max(oldQuality, candidate.trackQuality), trackQualityRiseRate * Time.deltaTime));
                existing.identificationConfidence = Mathf.Clamp01(Mathf.MoveTowards(oldId, Mathf.Max(oldId, candidate.identificationConfidence), identificationRiseRate * Time.deltaTime));
                existing.signalStrength = Mathf.Clamp01(candidate.signalStrength);
            }

            if (existing == selectedTrack) existing.isSelected = true;
            if (existing == lockedTrack) existing.isLocked = true;
        }

        private SensorContact FindTrack(string id, Transform target)
        {
            foreach (var track in tracks)
            {
                if (track == null) continue;
                if (!string.IsNullOrEmpty(id) && track.contactId == id) return track;
                if (target != null && track.targetTransform == target) return track;
            }
            return null;
        }

        private void DropWorstTrack()
        {
            int worstIndex = -1;
            float worstScore = float.MaxValue;
            for (int i = 0; i < tracks.Count; i++)
            {
                SensorContact track = tracks[i];
                if (track == lockedTrack) continue;
                float score = track.trackQuality - track.Staleness * 0.1f;
                if (score < worstScore)
                {
                    worstScore = score;
                    worstIndex = i;
                }
            }
            if (worstIndex >= 0) tracks.RemoveAt(worstIndex);
        }

        private void DecayTracks()
        {
            foreach (var track in tracks)
            {
                if (track == null) continue;
                float since = track.Staleness;
                if (since > scanInterval * 1.5f)
                {
                    track.trackQuality = Mathf.Clamp01(track.trackQuality - trackQualityDecayRate * Time.deltaTime);
                    track.identificationConfidence = Mathf.Clamp01(track.identificationConfidence - identificationRiseRate * 0.25f * Time.deltaTime);
                }
            }
        }

        private void CleanTracks()
        {
            for (int i = tracks.Count - 1; i >= 0; i--)
            {
                SensorContact track = tracks[i];
                if (track == null || track.targetTransform == null || track.Staleness > trackMemorySeconds || !track.isAlive)
                {
                    if (track == selectedTrack) selectedTrack = null;
                    if (track == lockedTrack) lockedTrack = null;
                    tracks.RemoveAt(i);
                }
            }
        }

        private bool ModeAllows(SensorContact contact)
        {
            if (contact == null) return false;
            if (mode == F15ERadarMode.AirSearch || mode == F15ERadarMode.TrackWhileScan || mode == F15ERadarMode.SingleTargetTrack)
            {
                return contact.IsAir || contact.kind == SensorContactKind.Unknown;
            }
            if (mode == F15ERadarMode.GroundMap || mode == F15ERadarMode.GroundMovingTarget || mode == F15ERadarMode.CasTargetCue)
            {
                return contact.IsGround || contact.kind == SensorContactKind.Unknown || contact.kind == SensorContactKind.CasTarget;
            }
            return true;
        }

        private bool InsideScanCone(SensorContact contact)
        {
            return Mathf.Abs(contact.bearingDegrees) <= azimuthHalfAngle && Mathf.Abs(contact.elevationDegrees) <= elevationHalfAngle;
        }

        private bool HasLineOfSight(Vector3 point)
        {
            Vector3 origin = transform.position;
            Vector3 toPoint = point - origin;
            float distance = toPoint.magnitude;
            if (distance < 1f) return true;
            return !Physics.Raycast(origin, toPoint.normalized, distance, lineOfSightBlockMask, QueryTriggerInteraction.Ignore);
        }

        private float ComputeDetectionScore(RadarSignature signature, SensorContact contact)
        {
            float maxRange = CurrentMaxRange();
            float rangeFactor = 1f - Mathf.Clamp01(contact.rangeMeters / Mathf.Max(1f, maxRange));
            float angleFactor = 1f - Mathf.Clamp01(Mathf.Abs(contact.bearingDegrees) / Mathf.Max(1f, azimuthHalfAngle));
            float elevationFactor = 1f - Mathf.Clamp01(Mathf.Abs(contact.elevationDegrees) / Mathf.Max(1f, elevationHalfAngle));
            float modeBonus = mode == F15ERadarMode.CasTargetCue && contact.kind == SensorContactKind.CasTarget ? 0.22f : 0f;
            float lockBonus = lockedTrack != null && lockedTrack.targetTransform == contact.targetTransform ? 0.20f : 0f;
            float randomNoise = Random.Range(-radarNoise, radarNoise);
            return Mathf.Clamp01(rangeFactor * 0.55f + angleFactor * 0.18f + elevationFactor * 0.12f + signature.radarVisibility * 0.32f + modeBonus + lockBonus + randomNoise);
        }

        private float CurrentMaxRange()
        {
            if (mode == F15ERadarMode.AirSearch || mode == F15ERadarMode.TrackWhileScan || mode == F15ERadarMode.SingleTargetTrack)
            {
                return maxAirSearchRange;
            }
            return maxGroundSearchRange;
        }

        private void HandleDebugKeys()
        {
            if (MaverickInput.GetKeyDown(cycleModeKey)) CycleMode();
            if (MaverickInput.GetKeyDown(nextTrackKey)) SelectNextTrack();
            if (MaverickInput.GetKeyDown(lockKey)) LockSelectedTrack();
            if (MaverickInput.GetKeyDown(unlockKey)) Unlock();
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;
            Gizmos.color = radarOn ? Color.cyan : Color.gray;
            Gizmos.DrawWireSphere(transform.position, Mathf.Min(CurrentMaxRange(), 1200f));

            Vector3 left = Quaternion.AngleAxis(-azimuthHalfAngle, transform.up) * transform.forward;
            Vector3 right = Quaternion.AngleAxis(azimuthHalfAngle, transform.up) * transform.forward;
            float gizmoRange = Mathf.Min(CurrentMaxRange(), 1800f);
            Gizmos.DrawLine(transform.position, transform.position + left * gizmoRange);
            Gizmos.DrawLine(transform.position, transform.position + right * gizmoRange);

            foreach (var track in tracks)
            {
                if (track == null) continue;
                Gizmos.color = track.isLocked ? Color.red : (track.isSelected ? Color.yellow : Color.green);
                Gizmos.DrawLine(transform.position, track.worldPosition);
                Gizmos.DrawWireSphere(track.worldPosition, Mathf.Lerp(8f, 35f, track.trackQuality));
            }
        }
    }
}
