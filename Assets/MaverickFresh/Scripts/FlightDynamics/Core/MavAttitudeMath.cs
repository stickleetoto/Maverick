using System;
using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>
    /// Aircraft attitude and flight-path angles, in the conventional aerodynamic sense.
    ///
    /// Sign conventions, all consistent with the existing body-axis convention:
    ///   pitch attitude  theta : nose UP positive
    ///   bank angle      phi   : RIGHT wing down positive
    ///   heading         psi   : increases to the RIGHT (clockwise seen from above)
    ///   flight path     gamma : CLIMBING positive
    /// </summary>
    [Serializable]
    public struct MavAttitude
    {
        public float pitchAttitudeRad;
        public float bankAngleRad;
        public float headingRad;
        public float flightPathAngleRad;

        [Tooltip("False when the orientation basis was degenerate and no attitude could be derived.")]
        public bool valid;

        [Tooltip("True near vertical flight, where bank and heading become ill-conditioned. The values are still finite, but a control law should not lean on them.")]
        public bool nearVerticalSingularity;

        [Tooltip("False when airspeed was too low for a meaningful flight-path angle.")]
        public bool flightPathValid;

        public float PitchAttitudeDeg { get { return pitchAttitudeRad * Mathf.Rad2Deg; } }
        public float BankAngleDeg { get { return bankAngleRad * Mathf.Rad2Deg; } }
        public float HeadingDeg { get { return headingRad * Mathf.Rad2Deg; } }
        public float FlightPathAngleDeg { get { return flightPathAngleRad * Mathf.Rad2Deg; } }

        public static MavAttitude Invalid
        {
            get { return new MavAttitude(); }
        }
    }

    /// <summary>
    /// Derives attitude from an aircraft's world-space orientation basis.
    ///
    /// COORDINATE DISCIPLINE - read before changing anything here.
    ///
    /// Maverick already carries a handedness hazard between Unity local axes (X right, Y up,
    /// Z forward) and aerodynamic body axes (X forward, Y right, Z down). The component change of
    /// basis has determinant -1, so true vectors and axial vectors convert differently, and
    /// MavFlightDynamicsMath keeps those two cases deliberately separate.
    ///
    /// This class does NOT participate in that conversion at all, and that is the point. Every
    /// angle below is defined GEOMETRICALLY, from world-space direction vectors and the world "up"
    /// direction:
    ///
    ///   theta = asin(forward.y)              nose above the horizon
    ///   psi   = atan2(forward.x, forward.z)  compass direction of the nose
    ///   phi   = atan2(-right.y, up.y)        how far the right wing has dropped
    ///   gamma = asin(velocity.y / |velocity|) climb angle of the velocity vector
    ///
    /// None of those depend on the handedness of any rotation convention, on Euler ordering, or on
    /// a basis change. They are statements about where vectors point. That makes them immune to the
    /// class of sign error that bit this project before, and it is why they must not be "simplified"
    /// into a quaternion-to-Euler conversion later.
    ///
    /// Validation asserts PHYSICAL DIRECTIONS - nose up gives positive theta, right wing down gives
    /// positive phi, and so on. Round-trip tests are deliberately not relied upon: a matching pair
    /// of sign errors cancels in a round trip and survives it untouched.
    /// </summary>
    public static class MavAttitudeMath
    {
        /// <summary>Below this speed the flight-path angle is not meaningful.</summary>
        public const float MinSpeedForFlightPathMps = 0.5f;

        /// <summary>|sin(theta)| beyond this counts as near-vertical, where bank and heading degrade.</summary>
        public const float VerticalSingularitySine = 0.999f;

        /// <summary>
        /// Builds attitude from the aircraft's world-space basis vectors and world velocity.
        ///
        /// Inputs are Unity's transform directions: forward is Unity local +Z, up is +Y, right is
        /// +X. World up is +Y.
        /// </summary>
        public static MavAttitude FromWorldBasis(
            Vector3 forwardWorld,
            Vector3 upWorld,
            Vector3 rightWorld,
            Vector3 worldVelocityMps)
        {
            MavAttitude attitude = MavAttitude.Invalid;

            float forwardMagnitude = forwardWorld.magnitude;
            float upMagnitude = upWorld.magnitude;
            float rightMagnitude = rightWorld.magnitude;

            if (forwardMagnitude < 1e-4f || upMagnitude < 1e-4f || rightMagnitude < 1e-4f)
                return attitude;

            Vector3 forward = forwardWorld / forwardMagnitude;
            Vector3 up = upWorld / upMagnitude;
            Vector3 right = rightWorld / rightMagnitude;

            attitude.valid = true;

            // Nose elevation above the horizontal plane. Positive when the nose points up.
            float sinPitch = Mathf.Clamp(forward.y, -1f, 1f);
            attitude.pitchAttitudeRad = Mathf.Asin(sinPitch);
            attitude.nearVerticalSingularity = Mathf.Abs(sinPitch) > VerticalSingularitySine;

            // Compass direction of the nose. Zero along world +Z, increasing toward world +X, so
            // heading increases to the right.
            attitude.headingRad = Mathf.Atan2(forward.x, forward.z);

            // How far the right wing has dropped below the horizontal. With wings level the right
            // wing is horizontal (right.y == 0) and the aircraft's up axis is world up (up.y == 1),
            // giving zero. Rolling right drops the right wing (right.y < 0), giving a positive
            // angle. This is a direct geometric statement, not a Euler extraction.
            attitude.bankAngleRad = Mathf.Atan2(-right.y, up.y);

            float speed = worldVelocityMps.magnitude;
            if (speed >= MinSpeedForFlightPathMps)
            {
                attitude.flightPathValid = true;
                attitude.flightPathAngleRad = Mathf.Asin(Mathf.Clamp(worldVelocityMps.y / speed, -1f, 1f));
            }

            return attitude;
        }

        /// <summary>
        /// cos(bank) for use in load-factor relations, degrading safely.
        ///
        /// Returns 1 - the wings-level value - whenever the attitude is unavailable or
        /// ill-conditioned, so a control law that loses its attitude reference falls back to the
        /// level-flight relation rather than to a meaningless number.
        /// </summary>
        public static float SafeCosBank(MavAttitude attitude)
        {
            if (!attitude.valid || attitude.nearVerticalSingularity)
                return 1f;

            return Mathf.Cos(attitude.bankAngleRad);
        }

        /// <summary>
        /// Body pitch rate required to hold a given normal load factor at a given bank angle:
        ///
        ///   q = g * (n - cos(phi)) / V
        ///
        /// This is the corrected form of the Phase 2 relation, which used (n - 1) and was therefore
        /// only right with the wings level. In a steady coordinated level turn n = 1/cos(phi), and
        /// substituting gives q = (g/V) * sin^2(phi)/cos(phi), which is the standard result.
        ///
        /// At phi = 0 it reduces exactly to the Phase 2 expression, so wings-level behaviour is
        /// unchanged.
        /// </summary>
        public static float PitchRateForLoadFactor(
            float loadFactorG,
            float cosBank,
            float trueAirspeedMps,
            float minimumSpeedMps)
        {
            float speed = Mathf.Max(Mathf.Max(1f, minimumSpeedMps), trueAirspeedMps);
            return MavControlLawProtections.StandardGravityMps2 * (loadFactorG - cosBank) / speed;
        }

        /// <summary>
        /// Normal load factor needed to hold altitude at a given bank angle: n = 1/cos(phi).
        ///
        /// Bounded, and only applied while the aircraft is upright enough for the relation to mean
        /// anything. Past <paramref name="minimumCosBank"/> - approaching knife-edge, and beyond it
        /// inverted - holding altitude is no longer a matter of pulling harder, so the function
        /// returns 1 and the caller is expected to say so rather than command a huge load factor.
        /// </summary>
        public static float LevelTurnLoadFactor(
            float cosBank,
            float minimumCosBank,
            float maximumLoadFactorG,
            out bool compensationApplied)
        {
            compensationApplied = false;

            if (cosBank <= minimumCosBank || minimumCosBank <= 0f)
                return 1f;

            compensationApplied = true;
            return Mathf.Clamp(1f / cosBank, 1f, Mathf.Max(1f, maximumLoadFactorG));
        }
    }
}
