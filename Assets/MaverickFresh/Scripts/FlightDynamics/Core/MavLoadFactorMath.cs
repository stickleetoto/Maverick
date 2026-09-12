using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>
    /// The six acceleration-like quantities this project keeps confusing, each defined exactly once.
    ///
    /// WHY THIS EXISTS. Phase 5B.5 found three different things in the runtime all called "G", and the
    /// Phase 5B.5 report then made the same mistake again by describing a gravity-inclusive
    /// acceleration projection as a load factor. They are genuinely different quantities:
    ///
    ///   WORLD ACCELERATION      dv/dt of the Rigidbody, in world axes. Includes gravity's effect on
    ///                           the motion, because gravity accelerates the aircraft.
    ///   GRAVITY ACCELERATION    Physics.gravity. Not a force the airframe feels.
    ///   SPECIFIC FORCE          worldAcceleration - gravity. What an accelerometer measures: the
    ///                           non-gravitational acceleration per unit mass. In free fall this is
    ///                           zero even though the aircraft is accelerating at 1 g.
    ///   BODY-NORMAL SPECIFIC    the component of specific force along the aircraft's normal axis.
    ///   FORCE
    ///   Nz / LOAD FACTOR        body-normal specific force divided by g0. Per NASA TP-1538
    ///                           nomenclature: "an, normal acceleration, positive along NEGATIVE Z
    ///                           body axis, g units". Aero body Z is DOWN, so -Z is up, so Nz is
    ///                           positive toward the canopy and reads +1 in steady level flight.
    ///   LEGACY HUD G            what MavInstructorController.gEstimate has always computed:
    ///                           Dot(worldAcceleration, bodyUp) / g0. Gravity INCLUSIVE, so it reads
    ///                           0 in level flight and -1 in free fall. It is not a load factor and
    ///                           this class refuses to call it one.
    ///
    /// Every one of those is a single function here, used by both the runtime diagnostic and the
    /// tests. A validation-only copy of a formula can agree with itself while the runtime is wrong,
    /// which is the failure this project has already had twice.
    /// </summary>
    public static class MavLoadFactorMath
    {
        /// <summary>Standard gravity, m/s^2. The divisor that turns an acceleration into "g units".</summary>
        public const float StandardGravityMps2 = 9.80665f;

        /// <summary>Rigidbody world acceleration from a velocity difference over a timestep.</summary>
        public static Vector3 WorldAcceleration(Vector3 velocityNow, Vector3 velocityPrevious, float dt)
        {
            if (dt <= 1e-6f)
                return Vector3.zero;

            return (velocityNow - velocityPrevious) / dt;
        }

        /// <summary>
        /// Specific force: what an accelerometer reads. World acceleration MINUS gravity.
        ///
        /// The subtraction is the whole point. An aircraft in free fall has 1 g of world acceleration
        /// and zero specific force; an aircraft parked on the ground has zero world acceleration and
        /// 1 g of specific force. Confusing the two is how a G meter ends up reading 0 in level flight.
        /// </summary>
        public static Vector3 SpecificForce(Vector3 worldAcceleration, Vector3 gravityAcceleration)
        {
            return worldAcceleration - gravityAcceleration;
        }

        /// <summary>Component of specific force along the aircraft normal axis, m/s^2.</summary>
        public static float BodyNormalSpecificForce(Vector3 specificForce, Vector3 bodyUpWorld)
        {
            return Vector3.Dot(specificForce, bodyUpWorld);
        }

        /// <summary>
        /// Normal load factor Nz, g units, per TP-1538: positive along the NEGATIVE Z body axis,
        /// i.e. positive toward the canopy. +1 in steady level flight, 0 in free fall.
        /// </summary>
        public static float Nz(Vector3 specificForce, Vector3 bodyUpWorld)
        {
            return BodyNormalSpecificForce(specificForce, bodyUpWorld) / StandardGravityMps2;
        }

        /// <summary>
        /// The legacy HUD quantity, reproduced exactly as MavInstructorController computes it.
        ///
        /// Named for what it is rather than what it was labelled. It is a gravity-INCLUSIVE projection
        /// of world acceleration onto the body up axis, so it differs from Nz by very nearly 1 g in
        /// upright flight. Kept because it feeds the legacy G limiter, and changing it would change
        /// handling - see defect D4.
        /// </summary>
        public static float LegacyHudG(Vector3 worldAcceleration, Vector3 bodyUpWorld)
        {
            return Vector3.Dot(worldAcceleration, bodyUpWorld) / StandardGravityMps2;
        }

        /// <summary>
        /// The difference between the legacy HUD reading and true Nz, in g.
        ///
        /// Equals -Dot(gravity, bodyUp)/g0, i.e. +1 in upright flight and 0 when the body up axis is
        /// horizontal. Published so the offset is a measured number rather than an assumption.
        /// </summary>
        public static float LegacyHudGMinusNz(Vector3 gravityAcceleration, Vector3 bodyUpWorld)
        {
            return Vector3.Dot(gravityAcceleration, bodyUpWorld) / StandardGravityMps2;
        }
    }
}
