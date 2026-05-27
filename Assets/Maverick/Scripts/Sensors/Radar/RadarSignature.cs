using UnityEngine;
using EaglePhysicalAI.Battlefield;

namespace EaglePhysicalAI.Sensors.Radar
{
    /// <summary>
    /// Game abstraction for how visible an object is to the simulated sensors.
    /// This is not a real radar model. Keep values tuned for gameplay and AI training.
    /// </summary>
    public class RadarSignature : MonoBehaviour
    {
        public string signatureId;
        public SensorContactKind kind = SensorContactKind.Ground;
        public GroundTeam team = GroundTeam.Hostile;
        public bool canBeDetectedByRadar = true;
        public bool canBeTrackedByPod = true;
        public bool canBeDesignatedForCas = true;
        public float radarVisibility = 1f;
        public float visualContrast = 1f;
        public float heatContrast = 1f;
        public float identificationDifficulty = 0.35f;
        public Vector3 aimPointOffset = Vector3.zero;

        private GroundUnit _groundUnit;
        private Rigidbody _rb;

        public bool IsAlive
        {
            get
            {
                if (_groundUnit != null) return _groundUnit.isAlive;
                return gameObject.activeInHierarchy;
            }
        }

        public Vector3 AimPoint => transform.TransformPoint(aimPointOffset);
        public Rigidbody Body => _rb;
        public GroundUnit GroundUnit => _groundUnit;

        private void Awake()
        {
            if (string.IsNullOrWhiteSpace(signatureId)) signatureId = gameObject.name;
            _groundUnit = GetComponentInParent<GroundUnit>();
            _rb = GetComponentInParent<Rigidbody>();

            if (_groundUnit != null)
            {
                team = _groundUnit.team;
                if (kind == SensorContactKind.Unknown) kind = SensorContactKind.Ground;
                if (_groundUnit.team == GroundTeam.Hostile && kind == SensorContactKind.Ground)
                {
                    kind = SensorContactKind.CasTarget;
                }
            }
        }

        public GroundTeam ResolveTeam()
        {
            if (_groundUnit != null) return _groundUnit.team;
            return team;
        }

        public string ResolveDisplayName()
        {
            if (_groundUnit != null && !string.IsNullOrWhiteSpace(_groundUnit.unitId)) return _groundUnit.unitId;
            return string.IsNullOrWhiteSpace(signatureId) ? gameObject.name : signatureId;
        }
    }
}
