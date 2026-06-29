using UnityEngine;

namespace MaverickFresh
{
    /// <summary>
    /// v0.20.9 lightweight target signature for the F-22 sensor pass.
    /// Put this on aircraft, drones, or mission targets. It intentionally stays game-level:
    /// RCS/IR values are abstract tuning values, not classified simulation data.
    /// </summary>
    [DisallowMultipleComponent]
    public class MavRadarSignature : MonoBehaviour
    {
        [Header("Identity")]
        public string displayName = "Target";
        public int team = 1;
        public bool isAirTarget = true;
        public bool isDestroyed;

        [Header("Detectability")]
        [Tooltip("Abstract radar cross-section tuning value. Larger = detected farther.")]
        public float radarCrossSectionSqm = 5.0f;
        [Tooltip("Abstract infrared signature tuning value. Afterburning/hot targets should be higher.")]
        public float irSignature = 1.0f;
        [Range(0f, 10f)] public float stealthRating = 0f;
        public float radarJamming = 0f;

        [Header("Debug")]
        public float debugLastDetectionQuality;
        public float debugLastDistance;
        public float debugLastAspectDeg;

        private void Awake()
        {
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = gameObject.name;
        }

        public bool CanBeDetected()
        {
            return !isDestroyed && gameObject.activeInHierarchy && enabled;
        }
    }
}
