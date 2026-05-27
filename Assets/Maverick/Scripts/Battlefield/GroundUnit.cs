using UnityEngine;

namespace EaglePhysicalAI.Battlefield
{
    public enum GroundTeam
    {
        Friendly,
        Hostile,
        Neutral
    }

    public class GroundUnit : MonoBehaviour
    {
        public string unitId = "unit";
        public GroundTeam team = GroundTeam.Hostile;
        public float health = 100f;
        public float importance = 1f;
        public bool isAlive = true;

        [Header("Optional CAS Request")]
        public bool canRequestCas;
        public float requestPriority = 1f;

        private void Awake()
        {
            if (string.IsNullOrWhiteSpace(unitId)) unitId = gameObject.name;
        }

        public void ApplyAbstractDamage(float amount)
        {
            if (!isAlive) return;
            health -= Mathf.Max(0f, amount);
            if (health <= 0f)
            {
                health = 0f;
                isAlive = false;
            }
        }
    }
}
