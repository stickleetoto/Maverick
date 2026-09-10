using System;
using UnityEngine;

namespace MaverickFresh.FlightDynamics.F16
{
    /// <summary>How the pitch stick is interpreted before the protections run.</summary>
    public enum MavPitchCommandMode
    {
        /// <summary>Stick commands a normal load factor. Usable and predictable; the default.</summary>
        LoadFactorDemand = 0,

        /// <summary>Stick commands a body pitch rate directly. Simpler, and useful for isolating the inner loop.</summary>
        PitchRateDemand = 1
    }

    /// <summary>
    /// Every tunable number in the Maverick F-16 control law v0.1.
    ///
    /// SOURCE STATUS: all of it is Maverick tuning. Not one gain, limit, time constant or schedule
    /// in this struct comes from the frozen NASA Morelli/Garza reference material, and none of it
    /// describes the real F-16 flight control system. See the class remarks on
    /// <see cref="MavF16ControlLawV01"/>.
    /// </summary>
    [Serializable]
    public struct MavF16ControlLawGains
    {
        [Header("Pitch Command Shaping (MAVERICK TUNING)")]
        public MavPitchCommandMode pitchCommandMode;

        [Tooltip("Load factor commanded at full aft stick, g. Used in LoadFactorDemand mode.")]
        public float commandedLoadFactorAtFullAftStickG;

        [Tooltip("Load factor commanded at full forward stick, g. Used in LoadFactorDemand mode.")]
        public float commandedLoadFactorAtFullForwardStickG;

        [Tooltip("Pitch rate commanded at full stick, deg/s. Used in PitchRateDemand mode.")]
        public float commandedPitchRateAtFullStickDegSec;

        [Tooltip("MAVERICK TUNING. When true, neutral stick commands the load factor needed to hold altitude at the current bank angle (n = 1/cos phi) instead of a flat 1 g. This is what makes a banked turn hold its altitude without the pilot pulling manually.")]
        public bool turnCompensationEnabled;

        [Tooltip("MAVERICK TUNING. Upper bound on the turn-compensation load factor, g. Without it a steep bank would demand an unbounded pull.")]
        public float maxTurnCompensationG;

        [Tooltip("MAVERICK TUNING. Minimum cos(bank) for turn compensation to apply. Past this the aircraft is approaching knife-edge, where holding altitude is no longer a matter of pulling harder.")]
        public float minimumCosBankForTurnCompensation;

        [Tooltip("Closed-loop load-factor feedback, dimensionless. Applied as gain*(nzCommanded-nzMeasured)*g0/V so the response is the same at every airspeed. 0 disables it, leaving a pure feed-forward command. Only active when a measured load factor is available.")]
        public float loadFactorFeedbackGain;

        [Header("Pitch Inner Loop (MAVERICK TUNING)")]
        [Tooltip("Proportional pitch-rate gain, degrees of surface demand per rad/s of rate error.")]
        public float pitchRateGainDegPerRadSec;

        [Tooltip("Integral pitch-rate gain, degrees of surface demand per rad/s of rate error per second. 0 disables the integrator.")]
        public float pitchRateIntegralGainDegPerRadSecPerSec;

        [Tooltip("Hard bound on accumulated integral surface demand, degrees.")]
        public float pitchIntegralLimitDeg;

        [Header("Roll (MAVERICK TUNING)")]
        [Tooltip("Roll rate commanded at full stick, deg/s.")]
        public float commandedRollRateAtFullStickDegSec;

        [Tooltip("Proportional roll-rate gain, degrees of surface demand per rad/s of rate error.")]
        public float rollRateGainDegPerRadSec;

        [Header("Yaw (MAVERICK TUNING)")]
        [Tooltip("Sideslip commanded at full pedal, degrees. Right pedal commands a nose-right yaw, which is a NEGATIVE sideslip in the beta = asin(v/V) convention.")]
        public float commandedSideslipAtFullPedalDeg;

        [Tooltip("Turn-coordination gain, degrees of rudder demand per degree of sideslip error.")]
        public float sideslipGainDegPerDeg;

        [Tooltip("Yaw damper gain, degrees of rudder demand per rad/s of washed-out yaw rate.")]
        public float yawDamperGainDegPerRadSec;

        [Tooltip("Yaw-rate washout time constant, seconds. Removes the steady turn rate so the damper does not fight a coordinated turn. 0 disables the washout, which makes the damper oppose steady turns.")]
        public float yawRateWashoutTimeConstantSeconds;

        [Tooltip("Aileron-rudder interconnect, degrees of rudder demand per rad/s of commanded roll rate. Feed-forward help against adverse yaw.")]
        public float aileronRudderInterconnectDegPerRadSec;

        [Header("Gain Scheduling (MAVERICK TUNING)")]
        public bool scheduleGainsWithDynamicPressure;
        public float referenceDynamicPressurePa;
        public float minimumGainScale;
        public float maximumGainScale;

        [Header("Numerical Guards")]
        [Tooltip("Airspeed floor used when converting a load factor to a pitch rate, m/s. Prevents an unbounded command near zero airspeed.")]
        public float minimumControlSpeedMps;

        [Tooltip("Blend width used by the smooth command limiters, rad/s. Larger is smoother and slightly more conservative.")]
        public float limiterBlendBandRadSec;

        [Header("Deflection Sign Convention")]
        [Tooltip("Elevator sign for nose-up demand. -1 matches the frozen convention where positive elevator gives negative pitching moment.")]
        public float surfaceSignElevator;

        [Tooltip("Aileron sign for roll-right demand. -1 matches the frozen convention where positive aileron gives negative rolling moment.")]
        public float surfaceSignAileron;

        [Tooltip("Rudder sign for nose-right demand. -1 matches the frozen convention where positive rudder gives negative yawing moment.")]
        public float surfaceSignRudder;

        public static MavF16ControlLawGains Default
        {
            get
            {
                MavF16ControlLawGains gains = new MavF16ControlLawGains();

                gains.pitchCommandMode = MavPitchCommandMode.LoadFactorDemand;
                gains.commandedLoadFactorAtFullAftStickG = 9f;
                gains.commandedLoadFactorAtFullForwardStickG = -3f;
                gains.commandedPitchRateAtFullStickDegSec = 30f;
                gains.turnCompensationEnabled = true;
                gains.maxTurnCompensationG = 4f;
                gains.minimumCosBankForTurnCompensation = 0.2588f;   // cos(75 deg)
                gains.loadFactorFeedbackGain = 2f;

                gains.pitchRateGainDegPerRadSec = 5f;
                gains.pitchRateIntegralGainDegPerRadSecPerSec = 2f;
                gains.pitchIntegralLimitDeg = 8f;

                gains.commandedRollRateAtFullStickDegSec = 270f;
                gains.rollRateGainDegPerRadSec = 2.5f;

                gains.commandedSideslipAtFullPedalDeg = 8f;
                gains.sideslipGainDegPerDeg = 1.5f;
                gains.yawDamperGainDegPerRadSec = 3f;
                gains.yawRateWashoutTimeConstantSeconds = 2f;
                gains.aileronRudderInterconnectDegPerRadSec = 0.5f;

                gains.scheduleGainsWithDynamicPressure = true;
                gains.referenceDynamicPressurePa = 24000f;
                gains.minimumGainScale = 0.25f;
                gains.maximumGainScale = 4f;

                gains.minimumControlSpeedMps = 30f;
                gains.limiterBlendBandRadSec = 0.05f;

                gains.surfaceSignElevator = -1f;
                gains.surfaceSignAileron = -1f;
                gains.surfaceSignRudder = -1f;

                return gains;
            }
        }
    }

    /// <summary>
    /// The control law's internal memory: everything that makes it a dynamic system rather than a
    /// pure function of the current state. Kept in an explicit struct so validation can drive the
    /// production maths deterministically, step by step, with no MonoBehaviour involved.
    /// </summary>
    [Serializable]
    public struct MavF16ControlLawState
    {
        [Tooltip("Accumulated integral pitch-surface demand, degrees.")]
        public float pitchIntegralDeg;

        [Tooltip("Low-pass state of the yaw-rate washout filter, rad/s.")]
        public float yawRateLowPassRadSec;

        public static MavF16ControlLawState Zero
        {
            get { return new MavF16ControlLawState(); }
        }
    }

    /// <summary>Inspectable internals of one control-law evaluation.</summary>
    [Serializable]
    public struct MavF16ControlLawDebug
    {
        public float gainScale;
        public float measuredLoadFactorG;
        public bool loadFactorMeasurementValid;

        public float commandedLoadFactorG;
        public float bankAngleDeg;
        public float cosBank;
        public bool attitudeValid;
        public bool turnCompensationApplied;
        public float levelTurnLoadFactorG;
        public float rawPitchRateCommandRadSec;
        public float limitedPitchRateCommandRadSec;
        public float pitchRateErrorRadSec;
        public float pitchSurfaceDemandDeg;
        public bool elevatorSaturated;

        public float alphaPitchRateCeilingRadSec;
        public float loadFactorPitchRateCeilingRadSec;
        public float loadFactorPitchRateFloorRadSec;
        public bool alphaLimiterBinding;
        public bool loadFactorLimiterBinding;

        public float rawRollRateCommandRadSec;
        public float limitedRollRateCommandRadSec;
        public float rollAuthorityFactor;
        public bool rollRateLimiterBinding;

        public float commandedSideslipDeg;
        public float sideslipErrorDeg;
        public float washedOutYawRateRadSec;
    }

    /// <summary>
    /// MAVERICK F-16 CONTROL LAW v0.1 - Phase C1 rate/load-factor augmentation.
    ///
    /// THIS IS NOT THE F-16 FLCS. It is not derived from, validated against, or intended to
    /// reproduce the real General Dynamics / Lockheed Martin flight control system. Every gain,
    /// limit, schedule and time constant in <see cref="MavF16ControlLawGains"/> is Maverick tuning
    /// chosen for handling, and none of it is sourced from the frozen NASA reference material in
    /// this repository. The F-16 name here identifies which airframe the tuning was chosen for,
    /// nothing more.
    ///
    /// What it does:
    ///   pitch stick -> commanded load factor (or pitch rate) -> protected pitch-rate demand
    ///                  -> pitch-rate error -> ELEVATOR degrees
    ///   roll stick  -> commanded roll rate -> protected roll-rate demand
    ///                  -> roll-rate error -> AILERON degrees
    ///   pedal       -> commanded sideslip, plus turn coordination and a washed-out yaw damper
    ///                  -> RUDDER degrees
    ///   throttle    -> passed straight through; this law does not manage thrust
    ///
    /// What it deliberately does NOT do:
    ///   - no Rigidbody access of any kind: no AddForce, no AddTorque, no velocity write
    ///   - no aerodynamic force or moment calculation, and no dynamic-pressure dimensionalization
    ///     (qbar appears only as a gain schedule, never as a load)
    ///   - no velocity-vector alignment and no attitude "recovery" assist
    ///   - no fake damping: the yaw damper and the rate loops command PHYSICAL SURFACES, and the
    ///     resulting moment comes from the aerodynamic model like any other. Nothing here writes a
    ///     damping torque
    ///   - no leading-edge-flap command, because the compact Morelli model does not consume it
    ///
    /// Known limitation, stated rather than hidden: <see cref="MavFlightState"/> carries no attitude
    /// reference, so the load-factor command is converted to a pitch rate using the level-flight
    /// relation n ~= 1 + V*q/g. In a steep bank this under-commands pitch rate for a given stick
    /// position. Closing that gap needs a bank angle, which belongs to a later phase.
    /// </summary>
    [DefaultExecutionOrder(-300)]
    [DisallowMultipleComponent]
    public sealed class MavF16ControlLawV01 : MavFlightControlLawBase
    {
        [Header("Gains (ALL MAVERICK TUNING - none of it is F-16 source data)")]
        public MavF16ControlLawGains gains = MavF16ControlLawGains.Default;

        [Header("Protections (ALL MAVERICK TUNING)")]
        public MavAngleOfAttackLimiterSettings angleOfAttackLimiter = MavAngleOfAttackLimiterSettings.Default;
        public MavLoadFactorLimiterSettings loadFactorLimiter = MavLoadFactorLimiterSettings.Default;
        public MavRollRateLimiterSettings rollRateLimiter = MavRollRateLimiterSettings.Default;

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

        [Header("Law State / Debug")]
        public MavF16ControlLawState lawState = MavF16ControlLawState.Zero;
        public MavF16ControlLawDebug debugLaw;
        public bool debugUsingProfileLimits;

        public override string ControlLawName
        {
            get { return "Maverick F-16 control law v0.1 (Maverick tuning; NOT the F-16 FLCS)"; }
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            // A control law must never inherit an accumulated integral or filter state from a
            // previous flight. Starting from a stale integrator would put a surface command on the
            // aircraft before the pilot has touched anything.
            ResetLawState();
        }

        public void ResetLawState()
        {
            lawState = MavF16ControlLawState.Zero;
        }

        public override MavControlInput Evaluate(
            MavFlightState state,
            MavAtmosphereSample atmosphere,
            MavPilotCommand command,
            MavFlightDynamicsProfile profile,
            float deltaTime)
        {
            debugUsingProfileLimits = profile != null;
            MavControlSurfaceLimits limits = debugUsingProfileLimits
                ? profile.controlSurfaceLimits
                : fallbackLimits;

            return Compute(
                command,
                state,
                limits,
                gains,
                angleOfAttackLimiter,
                loadFactorLimiter,
                rollRateLimiter,
                ref lawState,
                deltaTime,
                out debugLaw
            );
        }

        /// <summary>
        /// The whole control law as one deterministic function of (command, state, gains, law state,
        /// dt). Kept static and state-explicit so validation exercises exactly the code that flies,
        /// with no GameObject, no Rigidbody, and no Time.
        /// </summary>
        public static MavControlInput Compute(
            MavPilotCommand command,
            MavFlightState state,
            MavControlSurfaceLimits limits,
            MavF16ControlLawGains gains,
            MavAngleOfAttackLimiterSettings alphaLimiter,
            MavLoadFactorLimiterSettings gLimiter,
            MavRollRateLimiterSettings rollLimiter,
            ref MavF16ControlLawState lawState,
            float deltaTime,
            out MavF16ControlLawDebug debug)
        {
            MavPilotCommand c = command.Clamped();
            debug = new MavF16ControlLawDebug();

            float dt = Mathf.Max(0f, deltaTime);
            float band = Mathf.Max(0f, gains.limiterBlendBandRadSec);

            float speedMps = Mathf.Max(
                Mathf.Max(1f, gains.minimumControlSpeedMps),
                state.trueAirspeedMps
            );

            float bodyRollRate = state.aeroBodyRatesRadSec.x;
            float bodyPitchRate = state.aeroBodyRatesRadSec.y;
            float bodyYawRate = state.aeroBodyRatesRadSec.z;

            float alphaRad = state.alphaRad;
            float alphaDeg = state.AlphaDeg;
            float betaDeg = state.BetaDeg;

            bool loadFactorValid = state.specificForceValid;
            float measuredLoadFactorG = loadFactorValid ? state.LoadFactorNz : 1f;

            // Bank angle enters every load-factor relation below. SafeCosBank returns the
            // wings-level value when the attitude reference is unavailable or ill-conditioned, so
            // losing attitude degrades to the Phase 2 behaviour rather than to a meaningless number.
            float cosBank = MavAttitudeMath.SafeCosBank(state.attitude);
            debug.attitudeValid = state.attitude.valid;
            debug.bankAngleDeg = state.attitude.valid ? state.attitude.BankAngleDeg : 0f;
            debug.cosBank = cosBank;

            float gainScale = gains.scheduleGainsWithDynamicPressure
                ? MavControlLawProtections.DynamicPressureGainScale(
                    state.dynamicPressurePa,
                    gains.referenceDynamicPressurePa,
                    gains.minimumGainScale,
                    gains.maximumGainScale)
                : 1f;

            debug.gainScale = gainScale;
            debug.measuredLoadFactorG = measuredLoadFactorG;
            debug.loadFactorMeasurementValid = loadFactorValid;

            // ---------------------------------------------------------------- pitch command
            float commandedLoadFactorG;
            float levelTurnLoadFactorG;
            bool turnCompensationApplied;
            float rawPitchRateCommand = ComputePitchRateCommand(
                c.pitch,
                gains,
                speedMps,
                cosBank,
                measuredLoadFactorG,
                loadFactorValid,
                out commandedLoadFactorG,
                out levelTurnLoadFactorG,
                out turnCompensationApplied
            );

            debug.levelTurnLoadFactorG = levelTurnLoadFactorG;
            debug.turnCompensationApplied = turnCompensationApplied;
            debug.commandedLoadFactorG = commandedLoadFactorG;
            debug.rawPitchRateCommandRadSec = rawPitchRateCommand;

            // ---------------------------------------------------------------- pitch protections
            // Order matters. The negative-side floor is applied first and the nose-up ceiling last,
            // so when protections disagree the ceiling wins. That is the safe precedence: the
            // angle-of-attack and positive-g limits are the ones that protect the airframe.
            float limitedPitchRateCommand = rawPitchRateCommand;

            float loadFactorCeiling = float.PositiveInfinity;
            float loadFactorFloor = float.NegativeInfinity;

            if (gLimiter.enabled)
            {
                // Bank-aware: the pitch rate corresponding to a load-factor limit depends on how
                // much of the lift is already being spent holding the aircraft up.
                loadFactorCeiling = MavAttitudeMath.PitchRateForLoadFactor(
                    gLimiter.maxLoadFactorG, cosBank, speedMps, gains.minimumControlSpeedMps);
                loadFactorFloor = MavAttitudeMath.PitchRateForLoadFactor(
                    gLimiter.minLoadFactorG, cosBank, speedMps, gains.minimumControlSpeedMps);

                if (loadFactorValid)
                {
                    // The measured channel closes the loop on what the airframe is actually
                    // pulling, so the open-loop estimate cannot let a limit be exceeded.
                    // The margin gain is scaled by g0/V so the protection behaves identically at
                    // every airspeed, exactly like the open-loop ceiling it refines.
                    float marginGainRadSecPerG =
                        Mathf.Max(0f, gLimiter.measuredMarginGain)
                        * MavControlLawProtections.StandardGravityMps2 / speedMps;

                    loadFactorCeiling = MavControlLawProtections.SmoothMin(
                        loadFactorCeiling,
                        MavControlLawProtections.MeasuredLoadFactorPitchRateCeilingRadSec(
                            gLimiter.maxLoadFactorG,
                            measuredLoadFactorG,
                            marginGainRadSecPerG),
                        band);

                    loadFactorFloor = MavControlLawProtections.SmoothMax(
                        loadFactorFloor,
                        MavControlLawProtections.MeasuredLoadFactorPitchRateCeilingRadSec(
                            gLimiter.minLoadFactorG,
                            measuredLoadFactorG,
                            marginGainRadSecPerG),
                        band);
                }

                limitedPitchRateCommand = MavControlLawProtections.SmoothMax(
                    limitedPitchRateCommand, loadFactorFloor, band);
                limitedPitchRateCommand = MavControlLawProtections.SmoothMin(
                    limitedPitchRateCommand, loadFactorCeiling, band);
            }

            debug.loadFactorPitchRateCeilingRadSec = loadFactorCeiling;
            debug.loadFactorPitchRateFloorRadSec = loadFactorFloor;
            debug.loadFactorLimiterBinding =
                gLimiter.enabled
                && (rawPitchRateCommand > loadFactorCeiling || rawPitchRateCommand < loadFactorFloor);

            float alphaCeiling = float.PositiveInfinity;
            if (alphaLimiter.enabled)
            {
                float alphaFloor = MavControlLawProtections.AngleOfAttackPitchRateCeilingRadSec(
                    alphaLimiter.minAlphaDeg * Mathf.Deg2Rad,
                    alphaRad,
                    alphaLimiter.rateCeilingGainPerSec);

                alphaCeiling = MavControlLawProtections.AngleOfAttackPitchRateCeilingRadSec(
                    alphaLimiter.maxAlphaDeg * Mathf.Deg2Rad,
                    alphaRad,
                    alphaLimiter.rateCeilingGainPerSec);

                // Below the minimum alpha the floor is positive, which commands a nose-up recovery;
                // above the maximum the ceiling is negative, which commands a nose-down recovery.
                limitedPitchRateCommand = MavControlLawProtections.SmoothMax(
                    limitedPitchRateCommand, alphaFloor, band);
                limitedPitchRateCommand = MavControlLawProtections.SmoothMin(
                    limitedPitchRateCommand, alphaCeiling, band);

                debug.alphaLimiterBinding = rawPitchRateCommand > alphaCeiling || rawPitchRateCommand < alphaFloor;
            }

            debug.alphaPitchRateCeilingRadSec = alphaCeiling;
            debug.limitedPitchRateCommandRadSec = limitedPitchRateCommand;

            // ---------------------------------------------------------------- pitch inner loop
            float pitchRateError = limitedPitchRateCommand - bodyPitchRate;
            debug.pitchRateErrorRadSec = pitchRateError;

            float pitchSurfaceDemandDeg =
                gainScale * gains.pitchRateGainDegPerRadSec * pitchRateError
                + lawState.pitchIntegralDeg;

            debug.pitchSurfaceDemandDeg = pitchSurfaceDemandDeg;

            float rawElevatorDeg = Mathf.Sign(gains.surfaceSignElevator) * pitchSurfaceDemandDeg;
            float elevatorDeg = Mathf.Clamp(rawElevatorDeg, limits.elevatorMinDeg, limits.elevatorMaxDeg);
            bool elevatorSaturated = Mathf.Abs(rawElevatorDeg - elevatorDeg) > 1e-6f;
            debug.elevatorSaturated = elevatorSaturated;

            // Anti-windup: integrate for the NEXT step, and only when the integrator is not pushing
            // deeper into an already saturated surface. Without this the integral keeps growing
            // while the elevator is on its stop, and the aircraft cannot recover until the stored
            // command bleeds off - the classic windup overshoot.
            lawState.pitchIntegralDeg = MavControlLawProtections.StepIntegratorWithAntiWindup(
                lawState.pitchIntegralDeg,
                pitchRateError,
                gainScale * gains.pitchRateIntegralGainDegPerRadSecPerSec,
                dt,
                gains.pitchIntegralLimitDeg,
                elevatorSaturated && pitchSurfaceDemandDeg > 0f,
                elevatorSaturated && pitchSurfaceDemandDeg < 0f
            );

            // ---------------------------------------------------------------- roll
            float rawRollRateCommand = c.roll * gains.commandedRollRateAtFullStickDegSec * Mathf.Deg2Rad;
            debug.rawRollRateCommandRadSec = rawRollRateCommand;

            float rollAuthorityFactor = 1f;
            float limitedRollRateCommand = rawRollRateCommand;

            if (rollLimiter.enabled)
            {
                rollAuthorityFactor = MavControlLawProtections.RollAuthorityFactorAtAlpha(
                    alphaDeg,
                    rollLimiter.authorityFadeStartAlphaDeg,
                    rollLimiter.authorityFadeEndAlphaDeg,
                    rollLimiter.minimumAuthorityFactor);

                float rollRateLimit = Mathf.Max(0f, rollLimiter.maxRollRateDegSec)
                                      * Mathf.Deg2Rad
                                      * rollAuthorityFactor;

                limitedRollRateCommand = MavControlLawProtections.SoftSaturate(
                    rawRollRateCommand, rollRateLimit, band);

                debug.rollRateLimiterBinding =
                    Mathf.Abs(rawRollRateCommand) > rollRateLimit + 1e-6f;
            }

            debug.rollAuthorityFactor = rollAuthorityFactor;
            debug.limitedRollRateCommandRadSec = limitedRollRateCommand;

            float rollRateError = limitedRollRateCommand - bodyRollRate;
            float rollSurfaceDemandDeg = gainScale * gains.rollRateGainDegPerRadSec * rollRateError;
            float aileronDeg = Mathf.Clamp(
                Mathf.Sign(gains.surfaceSignAileron) * rollSurfaceDemandDeg,
                limits.aileronMinDeg,
                limits.aileronMaxDeg
            );

            // ---------------------------------------------------------------- yaw
            // Right pedal commands a nose-right yaw. Yawing the nose right moves it to the right of
            // the velocity vector, so the body-Y velocity component - and therefore
            // beta = asin(v/V) - becomes NEGATIVE. Hence the leading minus sign.
            float commandedSideslipDeg = -c.yaw * gains.commandedSideslipAtFullPedalDeg;
            float sideslipErrorDeg = betaDeg - commandedSideslipDeg;

            float washedOutYawRate = MavControlLawProtections.StepWashout(
                bodyYawRate,
                ref lawState.yawRateLowPassRadSec,
                gains.yawRateWashoutTimeConstantSeconds,
                dt
            );

            debug.commandedSideslipDeg = commandedSideslipDeg;
            debug.sideslipErrorDeg = sideslipErrorDeg;
            debug.washedOutYawRateRadSec = washedOutYawRate;

            // Positive rudder demand means nose-right.
            //   sideslip term : positive beta means the nose sits left of the velocity vector, so
            //                   yawing right reduces it -> +gain * betaError
            //   damper term   : opposes the oscillatory part of the yaw rate -> -gain * washout
            //   interconnect  : feed-forward against adverse yaw in a commanded roll
            float rudderSurfaceDemandDeg =
                gainScale * (gains.sideslipGainDegPerDeg * sideslipErrorDeg
                             - gains.yawDamperGainDegPerRadSec * washedOutYawRate)
                + gains.aileronRudderInterconnectDegPerRadSec * limitedRollRateCommand;

            float rudderDeg = Mathf.Clamp(
                Mathf.Sign(gains.surfaceSignRudder) * rudderSurfaceDemandDeg,
                limits.rudderMinDeg,
                limits.rudderMaxDeg
            );

            // ---------------------------------------------------------------- output
            MavControlInput output = new MavControlInput();
            output.throttle01 = c.throttle01;
            output.elevatorDeg = elevatorDeg;
            output.aileronDeg = aileronDeg;
            output.rudderDeg = rudderDeg;

            // The compact Morelli reference does not consume leading-edge flap, so commanding it
            // would be an invented aerodynamic effect.
            output.leadingEdgeFlapDeg = 0f;

            return output;
        }

        /// <summary>
        /// Pitch stick to unprotected pitch-rate demand.
        ///
        /// In LoadFactorDemand mode the stick sets a target normal load factor. The feed-forward
        /// conversion uses the level-flight relation n ~= 1 + V*q/g, so q = g*(n-1)/V, and an
        /// optional closed-loop term corrects toward the target using the measured load factor.
        /// Neutral stick commands 1 g, which in level flight is exactly zero pitch rate.
        /// </summary>
        public static float ComputePitchRateCommand(
            float pitchStick,
            MavF16ControlLawGains gains,
            float speedMps,
            float cosBank,
            float measuredLoadFactorG,
            bool loadFactorValid,
            out float commandedLoadFactorG,
            out float levelTurnLoadFactorG,
            out bool turnCompensationApplied)
        {
            float stick = Mathf.Clamp(pitchStick, -1f, 1f);

            // Load factor that would hold altitude at this bank angle. At wings level it is 1, so
            // everything below reduces exactly to the Phase 2 behaviour when the wings are level.
            levelTurnLoadFactorG = gains.turnCompensationEnabled
                ? MavAttitudeMath.LevelTurnLoadFactor(
                    cosBank,
                    gains.minimumCosBankForTurnCompensation,
                    gains.maxTurnCompensationG,
                    out turnCompensationApplied)
                : NoTurnCompensation(out turnCompensationApplied);

            if (gains.pitchCommandMode == MavPitchCommandMode.PitchRateDemand)
            {
                commandedLoadFactorG = loadFactorValid ? measuredLoadFactorG : levelTurnLoadFactorG;
                return stick * gains.commandedPitchRateAtFullStickDegSec * Mathf.Deg2Rad;
            }

            // Neutral stick commands the altitude-holding load factor rather than a flat 1 g, so a
            // banked turn holds its altitude without the pilot pulling manually.
            commandedLoadFactorG = stick >= 0f
                ? Mathf.Lerp(levelTurnLoadFactorG, gains.commandedLoadFactorAtFullAftStickG, stick)
                : Mathf.Lerp(levelTurnLoadFactorG, gains.commandedLoadFactorAtFullForwardStickG, -stick);

            // q = g * (n - cos(phi)) / V. The Phase 2 form used (n - 1), which is the phi = 0
            // special case and under-commands pitch rate in every turn.
            float feedForward = MavAttitudeMath.PitchRateForLoadFactor(
                commandedLoadFactorG, cosBank, speedMps, gains.minimumControlSpeedMps);

            if (!loadFactorValid || gains.loadFactorFeedbackGain == 0f)
                return feedForward;

            float safeSpeed = Mathf.Max(Mathf.Max(1f, gains.minimumControlSpeedMps), speedMps);
            float ratePerG = MavControlLawProtections.StandardGravityMps2 / safeSpeed;

            return feedForward
                   + ratePerG * gains.loadFactorFeedbackGain
                     * (commandedLoadFactorG - measuredLoadFactorG);
        }

        private static float NoTurnCompensation(out bool applied)
        {
            applied = false;
            return 1f;
        }
    }
}
