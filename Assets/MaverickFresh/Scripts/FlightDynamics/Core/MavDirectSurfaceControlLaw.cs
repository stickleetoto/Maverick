using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>
    /// Phase C0 test control law: normalized pilot intent maps straight onto bounded physical
    /// surface deflection. Nothing else.
    ///
    /// This exists so the 6DoF + aerodynamic path can be proven controllable *without* legacy
    /// direct torque, fake damping, or velocity-vector assist. It is explicitly NOT a fly-by-wire
    /// system and explicitly NOT the real F-16 FLCS:
    ///
    ///   - no stability or rate augmentation
    ///   - no G or AoA protection
    ///   - no attitude recovery
    ///   - no velocity-vector alignment
    ///   - no Rigidbody access of any kind
    ///
    /// Sign convention is derived from the Morelli-style aerodynamic convention already frozen
    /// in this branch, where a POSITIVE surface deflection produces a NEGATIVE moment
    /// (positive elevator = trailing edge down = nose down; and correspondingly for aileron and
    /// rudder). Pilot intent is stick-referenced (pitch +1 = nose up), so each channel carries a
    /// -1 sign factor. See the surfaceSign* fields: they are exposed so a future airframe whose
    /// reference uses the opposite convention can be configured instead of patched.
    ///
    /// The authority fractions are Maverick test-law tuning, not source-accurate aircraft data.
    /// They default to full available deflection so the mapping stays trivially auditable.
    /// </summary>
    // Restated explicitly rather than relying on the base class attribute, so the pipeline order
    // law (-300) -> actuator (-200) -> six-DoF body (-100) is guaranteed for this concrete type.
    [DefaultExecutionOrder(-300)]
    [DisallowMultipleComponent]
    public sealed class MavDirectSurfaceControlLaw : MavFlightControlLawBase
    {
        [Header("Authority Fraction (Maverick test-law tuning, not F-16 source data)")]
        [Range(0f, 1f)] public float pitchAuthority01 = 1f;
        [Range(0f, 1f)] public float rollAuthority01 = 1f;
        [Range(0f, 1f)] public float yawAuthority01 = 1f;

        [Header("Deflection Sign Convention")]
        [Tooltip("Elevator sign for nose-up demand. -1 matches the frozen convention where positive elevator gives negative pitching moment.")]
        public float surfaceSignElevator = -1f;

        [Tooltip("Aileron sign for roll-right demand. -1 matches the frozen convention where positive aileron gives negative rolling moment.")]
        public float surfaceSignAileron = -1f;

        [Tooltip("Rudder sign for nose-right demand. -1 matches the frozen convention where positive rudder gives negative yawing moment.")]
        public float surfaceSignRudder = -1f;

        [Header("Fallback Limits (used only when no valid physical profile is available)")]
        [Tooltip("Deliberately conservative. A real airframe's limits must come from its physical profile, never from this component.")]
        public MavControlSurfaceLimits fallbackLimits = new MavControlSurfaceLimits
        {
            elevatorMinDeg = -5f,
            elevatorMaxDeg = 5f,
            aileronMinDeg = -5f,
            aileronMaxDeg = 5f,
            rudderMinDeg = -5f,
            rudderMaxDeg = 5f,
            leadingEdgeFlapMinDeg = 0f,
            leadingEdgeFlapMaxDeg = 0f
        };

        [Header("Debug")]
        public bool debugUsingProfileLimits;

        public override string ControlLawName
        {
            get { return "Maverick C0 direct-surface test law"; }
        }

        public override MavControlInput Evaluate(
            MavFlightState state,
            MavAtmosphereSample atmosphere,
            MavPilotCommand command,
            MavFlightDynamicsProfile profile,
            float deltaTime)
        {
            // Deliberately state-independent: a C0 mapping test must not hide a hidden
            // airspeed/altitude schedule. state, atmosphere and deltaTime are unused here
            // and remain in the signature because C1 augmentation will need them.
            debugUsingProfileLimits = profile != null;
            MavControlSurfaceLimits limits = debugUsingProfileLimits
                ? profile.controlSurfaceLimits
                : fallbackLimits;

            return Map(
                command,
                limits,
                pitchAuthority01,
                rollAuthority01,
                yawAuthority01,
                surfaceSignElevator,
                surfaceSignAileron,
                surfaceSignRudder
            );
        }

        /// <summary>
        /// Pure, deterministic C0 mapping. Kept static so validation can exercise the exact
        /// production mapping without instantiating a GameObject.
        /// </summary>
        public static MavControlInput Map(
            MavPilotCommand command,
            MavControlSurfaceLimits limits,
            float pitchAuthority01,
            float rollAuthority01,
            float yawAuthority01,
            float signElevator,
            float signAileron,
            float signRudder)
        {
            MavPilotCommand c = command.Clamped();

            MavControlInput output = new MavControlInput();
            output.throttle01 = c.throttle01;

            output.elevatorDeg = MapToLimits(
                c.pitch * Mathf.Clamp01(pitchAuthority01) * Mathf.Sign(signElevator),
                limits.elevatorMinDeg,
                limits.elevatorMaxDeg
            );

            output.aileronDeg = MapToLimits(
                c.roll * Mathf.Clamp01(rollAuthority01) * Mathf.Sign(signAileron),
                limits.aileronMinDeg,
                limits.aileronMaxDeg
            );

            output.rudderDeg = MapToLimits(
                c.yaw * Mathf.Clamp01(yawAuthority01) * Mathf.Sign(signRudder),
                limits.rudderMinDeg,
                limits.rudderMaxDeg
            );

            // The C0 law never schedules leading-edge flaps. The compact F-16 reference in this
            // branch does not consume LEF, so commanding it would be an invented effect.
            output.leadingEdgeFlapDeg = 0f;

            return output;
        }

        /// <summary>
        /// Maps a signed -1..1 demand onto an asymmetric deflection range so that the positive
        /// demand direction reaches the positive limit and the negative direction reaches the
        /// negative limit. A limit whose sign is inverted (e.g. a positive minimum) yields zero
        /// rather than an out-of-range deflection.
        /// </summary>
        public static float MapToLimits(float normalizedDemand, float minDeg, float maxDeg)
        {
            float d = Mathf.Clamp(normalizedDemand, -1f, 1f);
            if (d >= 0f)
                return d * Mathf.Max(0f, maxDeg);

            // d < 0 and minDeg <= 0, so (-d) * minDeg is negative: full negative demand -> minDeg.
            return -d * Mathf.Min(0f, minDeg);
        }
    }
}
