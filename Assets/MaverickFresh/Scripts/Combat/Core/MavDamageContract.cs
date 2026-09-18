using UnityEngine;

namespace MaverickFresh.Combat
{
    /// <summary>
    /// One damage event, as delivered by whatever produced it.
    ///
    /// Deliberately a plain value: a damage receiver must not need to know which weapon, projectile
    /// or sensor produced the hit, and a future missile warhead must be able to produce one without
    /// referencing any legacy weapon type.
    /// </summary>
    public struct MavDamageInfo
    {
        /// <summary>Damage amount before any receiver-side armor or falloff handling.</summary>
        public float amount;

        /// <summary>Free-form attribution string, e.g. "DetailedGun". Never parsed for control flow.</summary>
        public string source;

        /// <summary>World-space impact point. Only meaningful when <see cref="hasHitPoint"/> is true.</summary>
        public Vector3 hitPoint;

        /// <summary>Whether <see cref="hitPoint"/> was actually measured. Area damage has no single point.</summary>
        public bool hasHitPoint;

        public static MavDamageInfo AtPoint(float amount, string source, Vector3 hitPoint)
        {
            MavDamageInfo info;
            info.amount = amount;
            info.source = source;
            info.hitPoint = hitPoint;
            info.hasHitPoint = true;
            return info;
        }

        public static MavDamageInfo Unlocated(float amount, string source)
        {
            MavDamageInfo info;
            info.amount = amount;
            info.source = source;
            info.hitPoint = Vector3.zero;
            info.hasHitPoint = false;
            return info;
        }
    }

    /// <summary>
    /// Anything that can absorb damage.
    ///
    /// WHY THIS EXISTS. Before this contract, the legacy weapon system found damage receivers by
    /// comparing `component.GetType().Name` against the string "MavAircraftDamageState" and then
    /// invoking `ApplyDamage` / `TakeDamage` / `IsAlive` through reflection, probing three different
    /// signatures in turn. That is a coupling with no compiler visible to it at all: renaming the
    /// receiver silently stops all damage, and a new munition has no declared way to say "I hit
    /// something".
    ///
    /// The member names are intentionally NOT `ApplyDamage` / `IsAlive`. Legacy receivers already
    /// have methods by those names with several different signatures, and the point of this
    /// interface is to sit beside them without disturbing them: existing callers keep working
    /// unchanged while new callers get a checked contract.
    ///
    /// A receiver must not apply flight-physics forces in response to damage. Aircraft physics stays
    /// owned by the flight-dynamics stack; a damaged aircraft changes its own state, not its
    /// Rigidbody, from here.
    /// </summary>
    public interface IMavDamageReceiver
    {
        /// <summary>Whether this receiver can still take damage. A destroyed target returns false.</summary>
        bool IsDamageReceiverAlive { get; }

        /// <summary>
        /// Applies one damage event. Implementations own their own armor, falloff and destruction
        /// rules; the caller must not assume the full amount was applied.
        /// </summary>
        void ReceiveDamage(MavDamageInfo info);
    }
}
