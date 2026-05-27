using UnityEngine;
using EaglePhysicalAI.Aircraft;
using EaglePhysicalAI.Battlefield;

namespace EaglePhysicalAI.Training
{
    /// <summary>
    /// Resets aircraft and simple ground targets for repeatable AI training episodes.
    /// </summary>
    public class EpisodeResetManager : MonoBehaviour
    {
        [Header("Aircraft")]
        public AircraftPhysicsController aircraft;
        public Rigidbody aircraftRigidbody;
        public Transform spawnPoint;
        public Vector3 fallbackSpawnPosition = new Vector3(0f, 800f, -1200f);
        public Vector3 fallbackSpawnEuler = new Vector3(3f, 0f, 0f);
        public float spawnForwardSpeed = 230f;
        public float spawnThrottle = 0.72f;

        [Header("Ground Units")]
        public bool resetGroundUnits = true;
        public float hostileHealth = 100f;
        public string lastResetReason = "none";
        public int episodeIndex;

        private void Awake()
        {
            if (aircraft == null) aircraft = GetComponent<AircraftPhysicsController>();
            if (aircraftRigidbody == null && aircraft != null) aircraftRigidbody = aircraft.rb;
        }

        [ContextMenu("Reset Episode")]
        public void ResetEpisodeFromContext()
        {
            ResetEpisode("context_menu");
        }

        public void ResetEpisode(string reason)
        {
            if (aircraft == null) aircraft = GetComponent<AircraftPhysicsController>();
            if (aircraftRigidbody == null && aircraft != null) aircraftRigidbody = aircraft.rb;

            Transform t = aircraft != null ? aircraft.transform : transform;
            Vector3 pos = spawnPoint != null ? spawnPoint.position : fallbackSpawnPosition;
            Quaternion rot = spawnPoint != null ? spawnPoint.rotation : Quaternion.Euler(fallbackSpawnEuler);

            t.position = pos;
            t.rotation = rot;

            if (aircraftRigidbody != null)
            {
                aircraftRigidbody.linearVelocity = t.forward * spawnForwardSpeed;
                aircraftRigidbody.angularVelocity = Vector3.zero;
                aircraftRigidbody.Sleep();
                aircraftRigidbody.WakeUp();
            }

            if (aircraft != null)
            {
                aircraft.SetCrashed(false);
                aircraft.throttle = Mathf.Clamp01(spawnThrottle);
                aircraft.targetThrottle = Mathf.Clamp01(spawnThrottle);
                aircraft.SetControlInputs(0f, 0f, 0f, spawnThrottle);
            }

            if (resetGroundUnits)
            {
                var units = FindObjectsOfType<GroundUnit>();
                foreach (var unit in units)
                {
                    if (unit == null) continue;
                    unit.isAlive = true;
                    unit.health = hostileHealth;
                }
            }

            episodeIndex++;
            lastResetReason = reason;
        }
    }
}
