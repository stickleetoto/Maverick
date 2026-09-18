using UnityEngine;

namespace MaverickFresh.Combat.Sensors
{
    /// <summary>
    /// Where a target sits relative to the sensor, in the sensor's own frame.
    ///
    /// Pure geometry. No detection decision lives here - this is the measurement a detection rule is
    /// then applied to, kept separate so the rule can change without the geometry changing.
    /// </summary>
    public struct MavRadarGeometry
    {
        /// <summary>Straight-line distance, metres.</summary>
        public float rangeMeters;

        /// <summary>
        /// Horizontal angle off the sensor boresight, degrees, always positive. Azimuth and elevation
        /// are separated rather than collapsed into one cone angle because a real scan volume is wider
        /// than it is tall, and a single cone cannot express that.
        /// </summary>
        public float azimuthDeg;

        /// <summary>Vertical angle off the boresight plane, degrees. Positive is above.</summary>
        public float elevationDeg;

        /// <summary>Total angle off boresight, degrees. Convenience for quality falloff.</summary>
        public float offBoresightDeg;

        /// <summary>
        /// Closure rate along the line of sight, m/s. Positive means closing. Zero when no relative
        /// velocity was supplied - it is not assumed.
        /// </summary>
        public float closureMps;

        /// <summary>Whether <see cref="closureMps"/> was actually computed from a supplied velocity.</summary>
        public bool hasClosure;
    }

    /// <summary>
    /// A configurable scan envelope: how far, and over what solid angle, a sensor can look.
    ///
    /// SYNTHETIC GAMEPLAY PARAMETERS. Every number here is a tuning value chosen for playability. None
    /// of it is sourced from, or intended to represent, the performance of any real radar: no real
    /// detection ranges, no real antenna pattern, no classified or unsupported performance figures.
    /// That is a deliberate boundary, and it matches how the flight-dynamics work treats unsourced
    /// values - if a number is not sourced, it is labelled as tuning rather than presented as fact.
    ///
    /// Deliberately absent, because they are later phases: PRF, clutter, notching, ECM/ECCM, sidelobes,
    /// scan patterns, beam dwell.
    /// </summary>
    [System.Serializable]
    public struct MavRadarScanVolume
    {
        [Tooltip("Minimum usable range, metres. Inside this the sensor reports nothing.")]
        public float minRangeMeters;

        [Tooltip("Maximum range for a reference-sized target, metres. SYNTHETIC tuning value.")]
        public float maxRangeMeters;

        [Tooltip("Half-width of the scan volume in azimuth, degrees.")]
        public float azimuthHalfAngleDeg;

        [Tooltip("Half-height of the scan volume in elevation, degrees.")]
        public float elevationHalfAngleDeg;

        public static MavRadarScanVolume Default
        {
            get
            {
                MavRadarScanVolume v;
                v.minRangeMeters = 150f;
                v.maxRangeMeters = 20000f;
                v.azimuthHalfAngleDeg = 60f;
                v.elevationHalfAngleDeg = 30f;
                return v;
            }
        }

        /// <summary>Clamps the envelope into a usable shape. Called before use so bad inspector values cannot poison geometry.</summary>
        public MavRadarScanVolume Sanitized()
        {
            MavRadarScanVolume v = this;
            v.minRangeMeters = Mathf.Max(0f, v.minRangeMeters);
            v.maxRangeMeters = Mathf.Max(v.minRangeMeters + 1f, v.maxRangeMeters);
            v.azimuthHalfAngleDeg = Mathf.Clamp(v.azimuthHalfAngleDeg, 0.5f, 180f);
            v.elevationHalfAngleDeg = Mathf.Clamp(v.elevationHalfAngleDeg, 0.5f, 90f);
            return v;
        }

        /// <summary>
        /// Whether a geometry sits inside the envelope, given an effective maximum range that the
        /// caller has already scaled for target size.
        ///
        /// Range and angle are checked separately and both must pass. Splitting them means a caller can
        /// report WHY something was not detected, which a single boolean cannot.
        /// </summary>
        public bool Contains(MavRadarGeometry geometry, float effectiveMaxRangeMeters)
        {
            if (geometry.rangeMeters < minRangeMeters)
                return false;
            if (geometry.rangeMeters > effectiveMaxRangeMeters)
                return false;
            if (geometry.azimuthDeg > azimuthHalfAngleDeg)
                return false;
            if (Mathf.Abs(geometry.elevationDeg) > elevationHalfAngleDeg)
                return false;
            return true;
        }

        /// <summary>
        /// Measures a target in the sensor's frame.
        ///
        /// Static and free of Unity components on purpose: it takes positions, a rotation and
        /// optionally velocities, so every geometry case can be validated deterministically without a
        /// scene, physics, or a running game.
        /// </summary>
        public static MavRadarGeometry Measure(
            Vector3 sensorPosition,
            Quaternion sensorRotation,
            Vector3 targetPosition,
            bool haveVelocities,
            Vector3 sensorVelocity,
            Vector3 targetVelocity)
        {
            MavRadarGeometry g = new MavRadarGeometry();

            Vector3 toTarget = targetPosition - sensorPosition;
            float range = toTarget.magnitude;
            g.rangeMeters = range;

            if (range < 1e-4f)
                return g;

            Vector3 local = Quaternion.Inverse(sensorRotation) * toTarget;

            // Azimuth in the sensor's horizontal plane, elevation out of it. Atan2 over the projected
            // components rather than a single Vector3.Angle, because the two axes have different limits.
            //
            // Atan2 is given the RAW local.z, including negatives. An earlier version clamped it with
            // Max(1e-4f, local.z) and then "corrected" rear targets with 180 - azimuth, which silently
            // destroyed the whole rear quadrant: clamping z to a positive epsilon made every rear
            // target read as 90 degrees before the correction, and 180 - 90 is 90 again. A target at
            // local (+1000, 0, -1000) - truly 135 degrees off boresight - reported 90, and one at
            // (+100, 0, -1000), truly 174 degrees, also reported 90. A scan volume wider than 90
            // degrees would then have admitted targets far outside its real limit.
            //
            // Atan2(x, z) over the unclamped components is already correct over the full circle, and
            // its absolute value is exactly the contract's unsigned 0..180 representation: front-right
            // and front-left both give 45, rear-right and rear-left both give 135, directly astern
            // gives 180. No rear correction is needed, and adding one is what caused the bug.
            g.azimuthDeg = Mathf.Abs(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg);

            // Elevation is measured off the sensor's horizontal plane, so it uses the horizontal
            // MAGNITUDE and is unaffected by whether the target is ahead or behind. The epsilon here
            // only avoids a zero denominator for a target directly above or below.
            float horizontal = new Vector2(local.x, local.z).magnitude;
            g.elevationDeg = Mathf.Atan2(local.y, Mathf.Max(1e-4f, horizontal)) * Mathf.Rad2Deg;

            g.offBoresightDeg = Vector3.Angle(Vector3.forward, local);

            if (haveVelocities)
            {
                Vector3 lineOfSight = toTarget / range;
                g.closureMps = Vector3.Dot(sensorVelocity - targetVelocity, lineOfSight);
                g.hasClosure = true;
            }

            return g;
        }

        /// <summary>
        /// Effective maximum range against a target of a given synthetic size factor.
        ///
        /// A fourth-root relationship is used because detection range against a scattering target
        /// scales with the fourth root of its cross-section under the plainest textbook radar range
        /// relation. That shape is public, first-principles physics, not a performance claim about any
        /// real system: the BASE range it scales is a synthetic tuning number, so the product is a
        /// gameplay value with a physically sensible shape rather than a spec.
        ///
        /// A size factor of 1 returns the configured base range unchanged.
        /// </summary>
        public float EffectiveMaxRange(float sizeFactor)
        {
            float clamped = Mathf.Clamp(sizeFactor, 0.01f, 100f);
            return maxRangeMeters * Mathf.Pow(clamped, 0.25f);
        }

        /// <summary>
        /// Detection quality for a geometry already known to be inside the envelope.
        ///
        /// Falls off with both range fraction and angle off boresight. Synthetic, monotonic, and
        /// bounded to [0,1]; it is used to choose a <see cref="MavTrackQuality"/>, not to model signal
        /// strength.
        /// </summary>
        public float Quality01(MavRadarGeometry geometry, float effectiveMaxRangeMeters)
        {
            float rangeFraction = Mathf.Clamp01(geometry.rangeMeters / Mathf.Max(1f, effectiveMaxRangeMeters));
            float rangeTerm = 1f - rangeFraction;

            float angleLimit = Mathf.Max(1f, Mathf.Max(azimuthHalfAngleDeg, elevationHalfAngleDeg));
            float angleTerm = 1f - Mathf.Clamp01(geometry.offBoresightDeg / angleLimit);

            return Mathf.Clamp01(rangeTerm * 0.65f + angleTerm * 0.35f);
        }
    }
}
