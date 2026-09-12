using UnityEngine;

namespace MaverickFresh
{
    /// <summary>
    /// The Phase 4B turn-dynamics rules, as pure functions.
    ///
    /// They live here rather than inside MavMouseFlightJet or MavTurnDynamicsDiagnostics because both
    /// production and validation need them, and a rule reachable only through a 1300-line MonoBehaviour
    /// is a rule that gets re-implemented in the test instead of tested. Every one of these is
    /// referenced by the production path; none of them is a copy.
    /// </summary>
    public static class MavTurnDynamicsRules
    {
        public const float G = 9.80665f;

        /// <summary>
        /// The proportional rate-nulling scale: 1 while a rate is commanded, falling to
        /// <paramref name="releaseScale"/> as the command returns to neutral.
        ///
        /// A rate-command controller asked for zero rate is a brake - the proportional term sees the
        /// whole current rate as error and fights it to a stop. Stacked on Rigidbody angular damping
        /// and the semi-aero rate dampers that is three mechanisms driving angular velocity to zero,
        /// which is why releasing the stick stopped the aircraft dead instead of letting it coast.
        ///
        /// Blended rather than switched, so crossing the threshold produces no torque step - a
        /// discontinuity here would twitch the aircraft exactly when the pilot is settling it.
        /// </summary>
        public static float ComputeRateNullingScale(
            float commandMagnitude,
            float threshold,
            float releaseScale)
        {
            float t = Mathf.Clamp01(Mathf.Abs(commandMagnitude) / Mathf.Max(0.0001f, threshold));
            return Mathf.Lerp(Mathf.Clamp01(releaseScale), 1f, t);
        }

        /// <summary>
        /// How much of the low-speed thrust boost survives the current load factor.
        ///
        /// The boost exists so slow level flight does not fall out of the sky. In a sustained hard
        /// turn it was silently paying the induced-drag bill - the exact energy loss the pilot is
        /// meant to feel and manage - so it is suppressed as load builds.
        /// </summary>
        public static float ComputeThrustBoostAllowance(float loadFactorG, float suppressionG)
        {
            float limit = Mathf.Max(1.05f, suppressionG);
            float t = Mathf.InverseLerp(1f, limit, Mathf.Abs(loadFactorG));
            return Mathf.Clamp01(1f - t);
        }

        /// <summary>
        /// How much of the nose-onto-velocity alignment assist may act, given how much ownership the
        /// legacy side still holds.
        ///
        /// The assist rotates the nose back onto the velocity vector, and angle of attack IS the gap
        /// between those two directions - so at full strength it erases the thing lift is built from.
        /// It is migrated with the velocity-turn assist, down to a floor: at literally zero the nose
        /// is free to sit far off the velocity vector at low speed, where there is not enough dynamic
        /// pressure for aerodynamic weathercocking to bring it back.
        /// </summary>
        public static float ComputeAlignmentAssistScale(float legacyOwnership, float floorAtFullAero)
        {
            return Mathf.Clamp01(
                Mathf.Lerp(Mathf.Clamp01(floorAtFullAero), 1f, Mathf.Clamp01(legacyOwnership)));
        }

        /// <summary>
        /// The instructor's pitch-command reduction from angle of attack and load factor.
        ///
        /// Both limiters exist in MavInstructorController and both matter to turn entry: without them
        /// a held full-pitch command drives AoA up without bound, because a rate-command controller is
        /// being asked for a rate rather than an attitude. Modelling turn entry without them produces
        /// an aircraft that stalls itself every time and makes the transient numbers meaningless.
        ///
        /// Same shape as production: reduce toward the configured factor as the measured value crosses
        /// from the soft limit to the hard limit.
        /// </summary>
        /// <summary>
        /// Would this pitch command push the aircraft FURTHER outside the envelope, or is it recovery?
        ///
        /// SIGN CONVENTIONS, traced not assumed:
        ///   pitch command  - NEGATIVE is nose up. A positive Unity local torque about +X pitches the
        ///                    nose DOWN (see MavFlightDynamicsMath), and the jet applies the pitch
        ///                    command directly as that torque.
        ///   angle of attack - POSITIVE when the velocity vector lies below the nose
        ///                    (alpha = atan2(-localVelocity.y, forwardSpeed)).
        ///   normal accel    - POSITIVE toward the canopy (Dot(dv/dt, transform.up)).
        ///
        /// So a nose-up command (negative) increases POSITIVE alpha and POSITIVE g, and a nose-down
        /// command (positive) drives alpha and g NEGATIVE. The command worsens an excursion exactly
        /// when its sign is opposite to the sign of the state:
        ///
        ///     worsens  =  command * state &lt; 0
        ///
        /// WHY NOT Mathf.Abs EVERYWHERE. Taking absolute values would limit the RECOVERY command too:
        /// at +30 deg alpha a nose-down push is the way out, and a limiter that attenuated it would
        /// trap the aircraft at the edge of the envelope it was meant to protect. The whole point is
        /// that protection has to be directional, and "symmetric" means symmetric about zero - not
        /// indifferent to direction.
        /// </summary>
        public static bool CommandWorsensExcursion(float pitchCommand, float signedState)
        {
            return pitchCommand * signedState < 0f;
        }

        /// <summary>
        /// The AoA-limiter reduction factor, valid on BOTH sides of zero.
        ///
        /// Returns 1 (no limiting) unless the state is outside the relevant soft limit AND the command
        /// would worsen it. The negative side uses its own breakpoints because the aircraft's negative
        /// envelope is not the mirror of its positive one - neither structurally nor aerodynamically.
        ///
        /// The positive branch reproduces ComputePitchCommandReduction's AoA term exactly, so enabling
        /// the negative side cannot alter positive-side behaviour.
        /// </summary>
        public static float ComputeSymmetricAoAReduction(
            float pitchCommand,
            float aoaDeg,
            float positiveSoftLimitDeg,
            float positiveHardLimitDeg,
            float negativeSoftLimitDeg,
            float negativeHardLimitDeg,
            float reductionAtHardLimit)
        {
            if (!CommandWorsensExcursion(pitchCommand, aoaDeg))
                return 1f;

            float t;
            if (aoaDeg > 0f)
            {
                if (aoaDeg <= positiveSoftLimitDeg)
                    return 1f;

                t = Mathf.InverseLerp(
                    positiveSoftLimitDeg,
                    Mathf.Max(positiveSoftLimitDeg + 0.1f, positiveHardLimitDeg),
                    aoaDeg);
            }
            else
            {
                // Negative limits are negative numbers; compare on magnitude so the ramp reads the
                // same way round as the positive branch.
                float soft = Mathf.Abs(negativeSoftLimitDeg);
                float hard = Mathf.Abs(negativeHardLimitDeg);
                float mag = Mathf.Abs(aoaDeg);
                if (mag <= soft)
                    return 1f;

                t = Mathf.InverseLerp(soft, Mathf.Max(soft + 0.1f, hard), mag);
            }

            return Mathf.Lerp(1f, Mathf.Clamp01(reductionAtHardLimit), Mathf.Clamp01(t));
        }

        /// <summary>
        /// The G-limiter reduction factor, valid on BOTH sides of zero. Same directional rule.
        /// </summary>
        public static float ComputeSymmetricGReduction(
            float pitchCommand,
            float loadFactorG,
            float positiveLimitG,
            float positiveHardLimitG,
            float negativeLimitG,
            float reductionAtLimit)
        {
            if (!CommandWorsensExcursion(pitchCommand, loadFactorG))
                return 1f;

            if (loadFactorG > 0f)
            {
                if (loadFactorG <= positiveLimitG)
                    return 1f;

                float t = Mathf.InverseLerp(
                    positiveLimitG, Mathf.Max(positiveLimitG + 0.1f, positiveHardLimitG), loadFactorG);
                return Mathf.Lerp(1f, Mathf.Clamp01(reductionAtLimit), Mathf.Clamp01(t));
            }

            float negLimit = Mathf.Abs(negativeLimitG);
            float mag = Mathf.Abs(loadFactorG);
            if (mag <= negLimit)
                return 1f;

            // Beyond the negative limit the ramp is completed over the same span the positive side
            // uses between its soft and hard limits, so the two sides share a shape without sharing
            // a number.
            float span = Mathf.Max(0.1f, positiveHardLimitG - positiveLimitG);
            float tn = Mathf.InverseLerp(negLimit, negLimit + span, mag);
            return Mathf.Lerp(1f, Mathf.Clamp01(reductionAtLimit), Mathf.Clamp01(tn));
        }

        public static float ComputePitchCommandReduction(
            float aoaDeg,
            float loadFactorG,
            float aoaSoftLimitDeg,
            float aoaHardLimitDeg,
            float aoaPitchReduction,
            float sustainedGLimit,
            float hardGLimit,
            float gPitchReduction)
        {
            float reduction = 1f;

            float aoaAbs = Mathf.Abs(aoaDeg);
            if (aoaAbs > aoaSoftLimitDeg)
            {
                float t = Mathf.InverseLerp(
                    aoaSoftLimitDeg, Mathf.Max(aoaSoftLimitDeg + 0.1f, aoaHardLimitDeg), aoaAbs);
                reduction *= Mathf.Lerp(1f, Mathf.Clamp01(aoaPitchReduction), Mathf.Clamp01(t));
            }

            float gAbs = Mathf.Abs(loadFactorG);
            if (gAbs > sustainedGLimit)
            {
                float t = Mathf.InverseLerp(
                    sustainedGLimit, Mathf.Max(sustainedGLimit + 0.1f, hardGLimit), gAbs);
                reduction = Mathf.Lerp(reduction, reduction * Mathf.Clamp01(gPitchReduction), Mathf.Clamp01(t));
            }

            return Mathf.Clamp01(reduction);
        }

        /// <summary>Turn rate implied by a curvature acceleration at a given speed: omega = a / V.</summary>
        public static float ComputeTurnRateDegPerSec(float curvatureAccel, float speed)
        {
            if (speed < 1f)
                return 0f;

            return (curvatureAccel / speed) * Mathf.Rad2Deg;
        }

        /// <summary>Turn radius r = V^2 / a. Zero when there is no meaningful curvature.</summary>
        public static float ComputeTurnRadiusMeters(float curvatureAccel, float speed)
        {
            if (curvatureAccel < 0.01f || speed < 1f)
                return 0f;

            return (speed * speed) / curvatureAccel;
        }

        /// <summary>
        /// Bank angle from the aircraft's geometry rather than Euler angles, which depend on Unity's
        /// rotation order and degenerate near vertical.
        /// </summary>
        public static float ComputeBankAngleDeg(Vector3 right, Vector3 up)
        {
            return Mathf.Atan2(-right.y, up.y) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// The share of trajectory curvature that aerodynamics produced, from the accelerations
        /// actually applied.
        ///
        /// This is the number Phase 4A could not report. Its accounting multiplied aeroBlend by
        /// nothing, so it claimed a tidy 1.00 ownership sum while the F-16 flew on 28% aerodynamic
        /// lift and 53% direct velocity steering. Only a measured ratio catches that.
        /// </summary>
        public static float ComputeAeroCurvatureShare(float aeroAccel, float legacyAccel)
        {
            float total = aeroAccel + legacyAccel;
            if (total <= 0.0001f)
                return 0f;

            return Mathf.Clamp01(aeroAccel / total);
        }
    }
}
