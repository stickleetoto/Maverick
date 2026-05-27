using UnityEngine;
using EaglePhysicalAI.Battlefield;

namespace EaglePhysicalAI.Sensors.Radar
{
    /// <summary>
    /// Convenience helper for prototype scenes: adds RadarSignature to GroundUnit objects if missing.
    /// Place this on any manager object or let EaglePhysicalAIBootstrap create it.
    /// </summary>
    public class RadarSignatureAutoBinder : MonoBehaviour
    {
        public bool bindOnStart = true;
        public bool bindPeriodically = true;
        public float interval = 2f;
        public float hostileRadarVisibility = 0.9f;
        public float friendlyRadarVisibility = 0.75f;
        public float neutralRadarVisibility = 0.55f;

        private float _lastBindTime;

        private void Start()
        {
            if (bindOnStart) BindAllGroundUnits();
        }

        private void Update()
        {
            if (!bindPeriodically) return;
            if (Time.time - _lastBindTime >= interval) BindAllGroundUnits();
        }

        public void BindAllGroundUnits()
        {
            _lastBindTime = Time.time;
            GroundUnit[] units = FindObjectsOfType<GroundUnit>();
            foreach (GroundUnit unit in units)
            {
                if (unit == null) continue;
                RadarSignature signature = unit.GetComponent<RadarSignature>();
                if (signature == null) signature = unit.gameObject.AddComponent<RadarSignature>();
                signature.signatureId = unit.unitId;
                signature.team = unit.team;
                signature.kind = unit.team == GroundTeam.Hostile ? SensorContactKind.CasTarget : SensorContactKind.Ground;
                signature.canBeDesignatedForCas = unit.team == GroundTeam.Hostile;
                signature.radarVisibility = unit.team == GroundTeam.Hostile ? hostileRadarVisibility : (unit.team == GroundTeam.Friendly ? friendlyRadarVisibility : neutralRadarVisibility);
            }
        }
    }
}
