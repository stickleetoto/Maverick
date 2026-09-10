using System;
using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>
    /// Angle-of-attack protection settings.
    ///
    /// EVERY number here is Maverick tuning. No angle-of-attack limit in this file is taken from
    /// the NASA Morelli/Garza reference set, and none of it describes the real F-16 FLCS. The
    /// aerodynamic model's published validity envelope (-10..+45 deg alpha) is a *modelling*
    /// range, not a control limit, and is deliberately not reused as one.
    /// </summary>
    [Serializable]
    public struct MavAngleOfAttackLimiterSettings
    {
        public bool enabled;

        [Tooltip("MAVERICK TUNING. Maximum commanded angle of attack, degrees.")]
        public float maxAlphaDeg;

        [Tooltip("MAVERICK TUNING. Minimum commanded angle of attack, degrees.")]
        public float minAlphaDeg;

        [Tooltip("MAVERICK TUNING. Pitch-rate authority given up per radian of remaining alpha margin, 1/s.")]
        public float rateCeilingGainPerSec;

        public static MavAngleOfAttackLimiterSettings Default
        {
            get
            {
                MavAngleOfAttackLimiterSettings settings = new MavAngleOfAttackLimiterSettings();
                settings.enabled = true;
                settings.maxAlphaDeg = 25f;
                settings.minAlphaDeg = -5f;
                settings.rateCeilingGainPerSec = 2.5f;
                return settings;
            }
        }
    }

    /// <summary>
    /// Normal-load-factor protection settings.
    ///
    /// The +9 / -3 g values are the commonly published F-16 airframe design load factors. They are
    /// NOT part of the frozen NASA reference data in this repository, so they are treated as
    /// Maverick tuning and must not be described as sourced aircraft limits.
    /// </summary>
    [Serializable]
    public struct MavLoadFactorLimiterSettings
    {
        public bool enabled;

        [Tooltip("MAVERICK TUNING. Maximum commanded normal load factor, g.")]
        public float maxLoadFactorG;

        [Tooltip("MAVERICK TUNING. Minimum commanded normal load factor, g.")]
        public float minLoadFactorG;

        [Tooltip("MAVERICK TUNING. Dimensionless. Remaining load-factor margin is converted into remaining pitch-rate authority as gain*(limit-measured)*g0/V, so the protection behaves identically at every airspeed. At 1.0 the closed-loop ceiling coincides with the open-loop one in 1 g flight. Used only when a measured load factor is available.")]
        public float measuredMarginGain;

        public static MavLoadFactorLimiterSettings Default
        {
            get
            {
                MavLoadFactorLimiterSettings settings = new MavLoadFactorLimiterSettings();
                settings.enabled = true;
                settings.maxLoadFactorG = 9f;
                settings.minLoadFactorG = -3f;
                settings.measuredMarginGain = 1f;
                return settings;
            }
        }
    }

    /// <summary>
    /// Roll-rate protection settings. All Maverick tuning.
    /// </summary>
    [Serializable]
    public struct MavRollRateLimiterSettings
    {
        public bool enabled;

        [Tooltip("MAVERICK TUNING. Maximum commanded roll rate, deg/s.")]
        public float maxRollRateDegSec;

        [Tooltip("MAVERICK TUNING. Alpha at which roll authority starts fading, degrees.")]
        public float authorityFadeStartAlphaDeg;

        [Tooltip("MAVERICK TUNING. Alpha at which roll authority reaches its floor, degrees.")]
        public float authorityFadeEndAlphaDeg;

        [Tooltip("MAVERICK TUNING. Roll authority remaining at and beyond the fade end, 0..1.")]
        [Range(0f, 1f)] public float minimumAuthorityFactor;

        public static MavRollRateLimiterSettings Default
        {
            get
            {
                MavRollRateLimiterSettings settings = new MavRollRateLimiterSettings();
                settings.enabled = true;
                settings.maxRollRateDegSec = 270f;
                settings.authorityFadeStartAlphaDeg = 15f;
                settings.authorityFadeEndAlphaDeg = 28f;
                settings.minimumAuthorityFactor = 0.25f;
                return settings;
            }
        }
    }

    /// <summary>
    /// Pure, deterministic command-limiting mathematics shared by Maverick flight control laws.
    ///
    /// Design rules for everything in this class:
    ///
    ///   1. Limiting is SMOOTH. A hard Mathf.Clamp on a pilot command produces a discontinuous
    ///      derivative at the knee, which a pilot feels as the command "hitting a wall" and which
    ///      can excite a limit cycle when the aircraft rides the boundary. The functions here are
    ///      continuous and reach their limit with zero slope.
    ///
    ///   2. Limiting is CONSERVATIVE. <see cref="SmoothMin"/> never returns more than the true
    ///      minimum and <see cref="SmoothMax"/> never returns less than the true maximum, so a
    ///      smoothed protection can only ever be tighter than the hard one, never looser.
    ///
    ///   3. Nothing here touches a Rigidbody, applies a force, or knows what an aircraft is.
    ///      These are scalar functions on commands.
    /// </summary>
    public static class MavControlLawProtections
    {
        public const float StandardGravityMps2 = 9.80665f;

        /// <summary>
        /// Smooth minimum with compact support.
        ///
        /// Returns exactly min(a, b) whenever the arguments differ by at least <paramref name="band"/>,
        /// and blends quadratically inside the band with a continuous first derivative, undershooting
        /// the true minimum by at most band/4 where the two meet.
        ///
        /// The exactness outside the band matters more than it looks. A smooth minimum without it
        /// (the common sqrt form) shifts its result slightly even when neither argument is anywhere
        /// near binding, so chaining several protections leaves a small permanent offset on the
        /// command - a control law with a hidden trim bias at neutral stick. This form cannot do
        /// that: away from a limit, the command passes through untouched.
        ///
        /// The undershoot direction is deliberate: a protection may be slightly conservative, never
        /// slightly permissive.
        /// </summary>
        public static float SmoothMin(float a, float b, float band)
        {
            if (band <= 0f)
                return Mathf.Min(a, b);

            float overlap = Mathf.Clamp(band - Mathf.Abs(a - b), 0f, band) / band;
            return Mathf.Min(a, b) - overlap * overlap * band * 0.25f;
        }

        /// <summary>
        /// Smooth maximum, the mirror of <see cref="SmoothMin"/>. Exact outside the band, and
        /// overshoots the true maximum by at most band/4 at the crossing, which again keeps the
        /// resulting limit conservative.
        /// </summary>
        public static float SmoothMax(float a, float b, float band)
        {
            if (band <= 0f)
                return Mathf.Max(a, b);

            return -SmoothMin(-a, -b, band);
        }

        /// <summary>
        /// Symmetric soft saturation onto [-limit, +limit].
        ///
        /// Below the knee (|value| &lt;= limit - band) the mapping is exactly the identity, so small
        /// commands are never distorted. Above the knee the slope falls smoothly from 1 to 0,
        /// reaching the limit exactly at |value| = limit + band and holding it beyond. The result is
        /// continuous, monotone, and never exceeds the limit.
        /// </summary>
        public static float SoftSaturate(float value, float limit, float band)
        {
            if (limit <= 0f)
                return 0f;

            float clampedBand = Mathf.Clamp(band, 0f, limit);
            if (clampedBand <= 0f)
                return Mathf.Clamp(value, -limit, limit);

            float sign = value < 0f ? -1f : 1f;
            float magnitude = Mathf.Abs(value);
            float knee = limit - clampedBand;

            if (magnitude <= knee)
                return value;

            // Quadratic ease from the knee: slope 1 at the knee, slope 0 at the limit.
            float u = Mathf.Clamp01((magnitude - knee) / (2f * clampedBand));
            float eased = knee + clampedBand * (2f * u - u * u);
            return sign * Mathf.Min(eased, limit);
        }

        /// <summary>
        /// Open-loop pitch-rate ceiling implied by a normal-load-factor limit.
        ///
        /// For a symmetric pull the incremental load factor and the body pitch rate are related by
        /// n ~= 1 + V*q/g, so the pitch rate that corresponds to the limit is q = g*(nLimit-1)/V.
        /// This needs no attitude reference and no accelerometer, which is why it is the fallback
        /// whenever a measured load factor is unavailable.
        /// </summary>
        public static float LoadFactorPitchRateCeilingRadSec(
            float loadFactorLimitG,
            float trueAirspeedMps,
            float minimumSpeedMps)
        {
            float speed = Mathf.Max(Mathf.Max(1f, minimumSpeedMps), trueAirspeedMps);
            return StandardGravityMps2 * (loadFactorLimitG - 1f) / speed;
        }

        /// <summary>
        /// Closed-loop pitch-rate ceiling from a MEASURED load factor: the remaining margin to the
        /// limit is converted into remaining pitch-rate authority, so the allowed rate reaches zero
        /// exactly as the measured load factor reaches the limit, and goes negative beyond it
        /// (commanding recovery).
        /// </summary>
        public static float MeasuredLoadFactorPitchRateCeilingRadSec(
            float loadFactorLimitG,
            float measuredLoadFactorG,
            float gainRadSecPerG)
        {
            return Mathf.Max(0f, gainRadSecPerG) * (loadFactorLimitG - measuredLoadFactorG);
        }

        /// <summary>
        /// Pitch-rate ceiling from an angle-of-attack limit: remaining alpha margin converted into
        /// remaining nose-up rate authority. Beyond the limit the value is negative, which commands
        /// a nose-down recovery rate rather than merely removing authority.
        /// </summary>
        public static float AngleOfAttackPitchRateCeilingRadSec(
            float alphaLimitRad,
            float alphaRad,
            float gainPerSec)
        {
            return Mathf.Max(0f, gainPerSec) * (alphaLimitRad - alphaRad);
        }

        /// <summary>
        /// Roll authority scaling with angle of attack: full authority below the fade start,
        /// smoothly reducing to the configured floor at the fade end. Smoothstep is used so the
        /// derivative is zero at both ends and the roll command does not step as alpha crosses a
        /// threshold.
        /// </summary>
        public static float RollAuthorityFactorAtAlpha(
            float alphaDeg,
            float fadeStartAlphaDeg,
            float fadeEndAlphaDeg,
            float minimumFactor)
        {
            float floorFactor = Mathf.Clamp01(minimumFactor);
            if (fadeEndAlphaDeg <= fadeStartAlphaDeg)
                return alphaDeg >= fadeEndAlphaDeg ? floorFactor : 1f;

            float t = Mathf.Clamp01((alphaDeg - fadeStartAlphaDeg) / (fadeEndAlphaDeg - fadeStartAlphaDeg));
            float smooth = t * t * (3f - 2f * t);
            return Mathf.Lerp(1f, floorFactor, smooth);
        }

        /// <summary>
        /// First-order washout (high-pass) step for a yaw damper.
        ///
        /// A yaw damper that opposes raw yaw rate also opposes the steady yaw rate of a coordinated
        /// turn, so it fights the pilot in every turn. Washing out the low-frequency content leaves
        /// only the oscillatory part - the Dutch roll - which is what the damper is for.
        ///
        /// Returns the washed-out signal and advances the low-pass state by reference.
        /// A non-positive time constant or step disables the filter and passes the signal through.
        /// </summary>
        public static float StepWashout(
            float input,
            ref float lowPassState,
            float timeConstantSeconds,
            float deltaTime)
        {
            if (timeConstantSeconds <= 0f || deltaTime <= 0f)
            {
                lowPassState = 0f;
                return input;
            }

            float blend = deltaTime / (timeConstantSeconds + deltaTime);
            lowPassState += (input - lowPassState) * blend;
            return input - lowPassState;
        }

        /// <summary>
        /// Conditional-integration anti-windup: integrate only when the integrator is not pushing
        /// further into an already-saturated actuator. Also hard-clamps the accumulated state, so
        /// even a mis-tuned gain cannot store an unbounded command.
        /// </summary>
        public static float StepIntegratorWithAntiWindup(
            float integralState,
            float error,
            float gain,
            float deltaTime,
            float integralLimit,
            bool actuatorSaturatedPositive,
            bool actuatorSaturatedNegative)
        {
            if (deltaTime <= 0f)
                return Mathf.Clamp(integralState, -Mathf.Abs(integralLimit), Mathf.Abs(integralLimit));

            float increment = gain * error * deltaTime;

            if (increment > 0f && actuatorSaturatedPositive)
                increment = 0f;
            if (increment < 0f && actuatorSaturatedNegative)
                increment = 0f;

            float limit = Mathf.Abs(integralLimit);
            return Mathf.Clamp(integralState + increment, -limit, limit);
        }

        /// <summary>
        /// Dynamic-pressure gain scheduling.
        ///
        /// Surface effectiveness scales with qbar, so a fixed surface gain is far too weak at low
        /// speed and far too strong at high speed. Scaling by qbarRef/qbar keeps the loop gain
        /// roughly constant. The result is clamped so a near-zero qbar cannot produce an enormous
        /// gain on the ground or at a stop.
        ///
        /// MAVERICK TUNING: the reference dynamic pressure and clamp range are chosen for handling,
        /// not derived from any published F-16 control-law schedule.
        /// </summary>
        public static float DynamicPressureGainScale(
            float dynamicPressurePa,
            float referenceDynamicPressurePa,
            float minimumScale,
            float maximumScale)
        {
            if (referenceDynamicPressurePa <= 0f)
                return 1f;

            float qbar = Mathf.Max(1f, dynamicPressurePa);
            float scale = referenceDynamicPressurePa / qbar;
            float low = Mathf.Max(1e-3f, minimumScale);
            float high = Mathf.Max(low, maximumScale);
            return Mathf.Clamp(scale, low, high);
        }
    }
}
