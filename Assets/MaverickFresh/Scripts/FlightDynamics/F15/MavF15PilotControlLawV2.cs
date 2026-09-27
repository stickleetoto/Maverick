using System;
using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// The V2 law's memory: the low-pass state of the yaw-rate washout. Kept explicit so validation can
    /// drive the production law step by step, and zeroed on enable / law switch so no filter state
    /// survives from an earlier flight.
    /// </summary>
    [Serializable]
    public struct MavF15PilotControlLawV2State
    {
        [Tooltip("Low-pass state of the stability-axis yaw-rate washout, rad/s.")]
        public float yawRateLowPassRadSec;

        public static MavF15PilotControlLawV2State Zero
        {
            get { return new MavF15PilotControlLawV2State(); }
        }
    }

    /// <summary>Inspectable internals of one V2 evaluation.</summary>
    [Serializable]
    public struct MavF15PilotControlLawV2Debug
    {
        [Tooltip("False when the flight state was not finite; every feedback term was then zeroed (feed-forward only).")]
        public bool stateUsable;

        public float gainScale;

        public float commandedPitchRateRadSec;
        public float pitchRateErrorRadSec;
        public float noseUpDemandDeg;

        public float commandedRollRateRadSec;
        public float stabilityAxisRollRateRadSec;
        public float rollRateErrorRadSec;
        public float rollRightDemandDeg;

        public float commandedSideslipDeg;
        public float sideslipErrorDeg;
        public float interconnectDemandDeg;
        public float stabilityAxisYawRateRadSec;
        public float washedOutYawRateRadSec;
        public float yawRateDemandDeg;
        public float noseRightDemandDeg;
    }

    /// <summary>
    /// MAVERICK F-15 PILOT-CONTROL LAW V2 - closed-loop rate / sideslip augmentation for the
    /// pilot-controlled F-15 research aircraft.
    ///
    /// THIS IS NOT THE F-15 FCS. It is not the F-15 CAS, FLCS or SAS, not a NASA 836 control law, and not
    /// derived from or validated against any of them. <see cref="MavF15ControlLaw"/> (the F-15 FCS
    /// architecture with every gain unavailable) is untouched and unused.
    ///
    /// WHERE IT COMES FROM. The SOFTWARE ARCHITECTURE of the Maverick F-16 control law
    /// (<c>MavF16ControlLawV01</c>) - itself Maverick tuning, not the F-16 FLCS - adapted to this aircraft:
    ///   pitch stick -> pitch-rate demand -> q error -> symmetric stabilator
    ///   roll stick  -> roll-rate demand   -> roll-rate error -> aileron (+ research 0.3 differential tail)
    ///   pedal       -> sideslip demand    -> beta error -> rudder, plus an aileron-rudder interconnect
    ///                  and a washed-out yaw-rate term
    ///   dynamic-pressure gain scheduling of the surface gains, explicit law state (the washout filter)
    /// No F-16 number is used: every gain, command, schedule value and time constant is
    /// <see cref="MavF15PilotControlProvenance.MaverickTunedNonAuthoritative"/> and lives in
    /// <see cref="MavF15PilotControlGainsV2"/>. The surface sign conventions and the differential-tail
    /// relation are the research model's own (<see cref="MavF15PilotControlConventions"/>), applied through the
    /// V1 mapping (<see cref="MavF15PilotControlMapping.SolveFromDemands"/>) - one owner of the conventions
    /// and the gameplay envelopes for both laws.
    ///
    /// ADAPTED, NOT COPIED. Differences from the F-16 law, each for a reason measured and documented in
    /// Docs/Reference/F15_PILOT_CONTROLLED_V2.md:
    ///   - the roll loop closes on STABILITY-AXIS roll rate (p cos alpha + r sin alpha), and the yaw-rate
    ///     term on STABILITY-AXIS yaw rate (r cos alpha - p sin alpha): at the alpha-17.5 trim a body-axis
    ///     roll builds sideslip that the model's very large dihedral effect turns straight back into an
    ///     opposing roll - which is why V1 rolls slowly. Both are zero in a roll about the velocity vector;
    ///   - pitch, roll and pedal carry a feed-forward term sized from the research model's own response;
    ///   - the washed-out yaw-rate term is labelled MAVERICK GAMEPLAY / RESEARCH ASSIST - it coordinates the
    ///     roll and lifts the Dutch-roll damping; it is not an F-15 yaw damper, SAS or CAS;
    ///   - NOT used: a pitch integrator (at this fixed-thrust trim it drains airspeed toward the research
    ///     domain edge and lowers phugoid damping), load-factor demand, turn compensation, and alpha / g /
    ///     roll-rate protection (the gameplay envelopes and the commanded rates already bound the tested
    ///     manoeuvres inside the research domain).
    ///
    /// NEUTRAL STICK. The demands are all feedback of rates and sideslip, which are zero at the trim, so
    /// centred stick at the trim requests exactly the validated trim bias and zero lateral surfaces.
    ///
    /// OWNERSHIP. It computes a REQUEST only: no Rigidbody access, no force, no moment, no damping torque,
    /// no velocity or attitude write. <see cref="MavF15ControlActuator"/> owns the surfaces and every motion
    /// comes from the research aerodynamics. Runs at execution order -300 (base class), ahead of the
    /// actuator (-200) and the body (-100); it reads the flight state the body published on the previous
    /// physics step, a one-step delay that the tuning includes.
    ///
    /// THROTTLE. Passed through to telemetry only: <see cref="MavF15PilotControlledFixedThrust.ThrottleStatus"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MavF15PilotControlLawV2 : MavFlightControlLawBase
    {
        public const string LawName =
            "Maverick F-15 pilot-control V2 (closed-loop rate/sideslip augmentation, architecture adapted from the Maverick F-16 "
            + "control law; " + MavF15PilotControlProvenance.MaverickTunedNonAuthoritative + ") - NOT F-15 CAS / FLCS / SAS, NOT NASA 836";

        [Header("Gains (ALL MAVERICK_TUNED_NON_AUTHORITATIVE - no F-15 source, no F-16 number)")]
        public MavF15PilotControlGainsV2 gains = MavF15PilotControlGainsV2.V2();

        [Header("Configuration")]
        [Tooltip("Where the gameplay envelopes and the trim bias come from - the same ones V1 uses. Resolved from this GameObject when empty.")]
        public MavF15PilotControlledFlightDynamicsProfile configuration;

        [Header("Debug / Requested Surface State")]
        public MavF15RequestedSurfaceState debugRequested;
        public MavF15SurfaceState debugUnbounded;
        public bool debugEnvelopeLimited;
        public float debugTrimStabilatorBiasDeg;
        public MavF15PilotControlLawV2Debug debugLaw;

        [Header("Law State")]
        public MavF15PilotControlLawV2State lawState = MavF15PilotControlLawV2State.Zero;

        [Tooltip("The pilot's throttle, shown and ignored: THROTTLE INACTIVE - FIXED RESEARCH THRUST.")]
        public float debugThrottleInactive01;

        public override string ControlLawName
        {
            get { return LawName; }
        }

        public MavF15RequestedSurfaceState RequestedF15SurfaceState
        {
            get { return debugRequested; }
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            // Never inherit filter state from an earlier flight or from before a V1 -> V2 switch.
            ResetLawState();
        }

        public void ResetLawState()
        {
            lawState = MavF15PilotControlLawV2State.Zero;
        }

        private float steppedAtFixedTime = -1f;

        protected override void FixedUpdate()
        {
            // Already evaluated in this physics step (an in-flight law switch primes the new law before its turn):
            // never step the law state twice in one step.
            if (steppedAtFixedTime == Time.fixedTime)
                return;

            // As V1: the base pipeline publishes the three shared channels, then the full four-channel
            // request from the SAME evaluation is pushed - nothing is recomputed.
            base.FixedUpdate();
            PushF15Request();
        }

        /// <summary>
        /// Evaluates the law now, inside the current physics step, and marks the step as done so the law's
        /// own FixedUpdate in this same step does nothing. Used by the rig's in-flight law switch.
        /// </summary>
        public void StepPilotControlLawForThisPhysicsStep(float deltaTime)
        {
            StepPilotControlLaw(deltaTime);
            steppedAtFixedTime = Time.fixedTime;
        }

        /// <summary>One law step with the timestep supplied, so validation can drive the real pipeline.</summary>
        public void StepPilotControlLaw(float deltaTime)
        {
            StepControlLaw(deltaTime);
            PushF15Request();
        }

        private void PushF15Request()
        {
            if (!driveActuatorInFixedUpdate || !debugDroveActuator)
                return;

            MavF15ControlActuator f15Actuator = actuator as MavF15ControlActuator;
            if (f15Actuator != null)
                f15Actuator.SetF15Command(debugRequested, debugLastOutput.throttle01);
        }

        public override MavControlInput Evaluate(
            MavFlightState state,
            MavAtmosphereSample atmosphere,
            MavPilotCommand command,
            MavFlightDynamicsProfile profile,
            float deltaTime)
        {
            ResolveConfiguration();

            MavF15GameplayControlAuthority authority = configuration != null
                ? configuration.gameplayControlAuthority
                : MavF15GameplayControlAuthority.V1();
            float bias = configuration != null
                ? (float)configuration.trimStart.symmetricStabilatorDeg
                : (float)MavF15PilotTrimStart.TableViiPoint36().symmetricStabilatorDeg;

            MavF15PilotControlSolution solution = Compute(command, state, gains, authority, bias, ref lawState, deltaTime, out debugLaw);

            debugRequested = MavF15RequestedSurfaceState.From(solution.requested);
            debugUnbounded = solution.unbounded;
            debugEnvelopeLimited = solution.AnyLimited;
            debugTrimStabilatorBiasDeg = bias;
            debugThrottleInactive01 = solution.command.throttle01;

            return new MavControlInput
            {
                throttle01 = solution.command.throttle01,
                elevatorDeg = solution.requested.symmetricStabilatorDeg,
                aileronDeg = solution.requested.aileronDeg,
                rudderDeg = solution.requested.rudderDeg,
                leadingEdgeFlapDeg = 0f
            };
        }

        /// <summary>
        /// The whole V2 law as one deterministic function of (command, flight state, gains, envelopes, trim
        /// bias, law state, dt), so validation exercises exactly the code that flies. The law state is
        /// advanced by reference.
        /// </summary>
        public static MavF15PilotControlSolution Compute(
            MavPilotCommand command,
            MavFlightState state,
            MavF15PilotControlGainsV2 gains,
            MavF15GameplayControlAuthority authority,
            float trimStabilatorBiasDeg,
            ref MavF15PilotControlLawV2State lawState,
            float deltaTime,
            out MavF15PilotControlLawV2Debug debug)
        {
            debug = new MavF15PilotControlLawV2Debug();
            MavPilotCommand c = Finite(command).Clamped();

            // A non-finite state is a fault upstream: its feedback is zeroed (the command's feed-forward
            // still acts) and reported, never passed to a surface.
            float p = state.aeroBodyRatesRadSec.x;
            float q = state.aeroBodyRatesRadSec.y;
            float r = state.aeroBodyRatesRadSec.z;
            float alpha = state.alphaRad;
            float betaDeg = state.BetaDeg;
            debug.stateUsable = IsFinite(p) && IsFinite(q) && IsFinite(r) && IsFinite(alpha) && IsFinite(betaDeg);
            if (!debug.stateUsable)
            {
                p = q = r = alpha = betaDeg = 0f;
            }

            float s = GainScale(gains, state.dynamicPressurePa);
            debug.gainScale = s;

            // ---------------------------------------------------------------- pitch
            float qCommand = c.pitch * gains.commandedPitchRateAtFullStickDegSec * Mathf.Deg2Rad;
            float qError = qCommand - q;
            float noseUp = s * (gains.pitchRateFeedForwardDegPerRadSec * qCommand + gains.pitchRateGainDegPerRadSec * qError);
            debug.commandedPitchRateRadSec = qCommand;
            debug.pitchRateErrorRadSec = qError;
            debug.noseUpDemandDeg = noseUp;

            // ---------------------------------------------------------------- roll
            // Roll about the velocity vector: p_s = p cos(alpha) + r sin(alpha) (beta ~ 0 by the yaw loop).
            float pCommand = c.roll * gains.commandedRollRateAtFullStickDegSec * Mathf.Deg2Rad;
            float stabilityAxisRollRate = p * Mathf.Cos(alpha) + r * Mathf.Sin(alpha);
            float pError = pCommand - stabilityAxisRollRate;
            float rollRight = s * (gains.rollRateFeedForwardDegPerRadSec * pCommand + gains.rollRateGainDegPerRadSec * pError);
            debug.commandedRollRateRadSec = pCommand;
            debug.stabilityAxisRollRateRadSec = stabilityAxisRollRate;
            debug.rollRateErrorRadSec = pError;
            debug.rollRightDemandDeg = rollRight;

            // ---------------------------------------------------------------- yaw
            // Right pedal commands nose right, which puts the nose right of the velocity vector: beta < 0.
            // Positive beta (nose left of the velocity) is removed by yawing right: + gain x beta error.
            // The interconnect supplies the yaw rate a velocity-vector roll needs (r = p tan alpha).
            float betaCommandDeg = -c.yaw * gains.commandedSideslipAtFullPedalDeg;
            float betaErrorDeg = betaDeg - betaCommandDeg;
            float interconnect = s * gains.aileronRudderInterconnectDegPerRadSec * pCommand;

            // MAVERICK GAMEPLAY / RESEARCH ASSIST: washed-out stability-axis yaw rate, opposed. It is zero in
            // a roll about the velocity vector, so a body-axis roll (r too small) yaws the nose into the
            // roll; it also opposes the Dutch roll. The washout removes a sustained turn's steady yaw rate.
            float stabilityAxisYawRate = r * Mathf.Cos(alpha) - p * Mathf.Sin(alpha);
            float washedOut = MavControlLawProtections.StepWashout(
                stabilityAxisYawRate, ref lawState.yawRateLowPassRadSec, gains.yawRateWashoutTimeConstantSeconds, Mathf.Max(0f, deltaTime));
            float yawRateDemand = -s * gains.stabilityAxisYawRateGainDegPerRadSec * washedOut;

            float sideslipDemand = s * (-gains.sideslipFeedForwardDegPerDeg * betaCommandDeg + gains.sideslipGainDegPerDeg * betaErrorDeg);
            float noseRight = sideslipDemand + interconnect + yawRateDemand;
            debug.commandedSideslipDeg = betaCommandDeg;
            debug.sideslipErrorDeg = betaErrorDeg;
            debug.interconnectDemandDeg = interconnect;
            debug.stabilityAxisYawRateRadSec = stabilityAxisYawRate;
            debug.washedOutYawRateRadSec = washedOut;
            debug.yawRateDemandDeg = yawRateDemand;
            debug.noseRightDemandDeg = noseRight;

            return MavF15PilotControlMapping.SolveFromDemands(c, noseUp, rollRight, noseRight, authority, trimStabilatorBiasDeg);
        }

        /// <summary>
        /// qbarRef / qbar, clamped to [min, max]: continuous in qbar and exactly 1 at the reference. A qbar
        /// that is not a positive finite number (no evaluated state yet) gives 1 - the tuned gains.
        /// </summary>
        public static float GainScale(MavF15PilotControlGainsV2 gains, float dynamicPressurePa)
        {
            if (!gains.scheduleGainsWithDynamicPressure)
                return 1f;
            if (!(dynamicPressurePa > 0f) || float.IsInfinity(dynamicPressurePa) || !(gains.referenceDynamicPressurePa > 0f))
                return 1f;

            float low = Mathf.Max(1e-3f, gains.minimumGainScale);
            float high = Mathf.Max(low, gains.maximumGainScale);
            return Mathf.Clamp(gains.referenceDynamicPressurePa / dynamicPressurePa, low, high);
        }

        private static MavPilotCommand Finite(MavPilotCommand c)
        {
            if (!IsFinite(c.pitch)) c.pitch = 0f;
            if (!IsFinite(c.roll)) c.roll = 0f;
            if (!IsFinite(c.yaw)) c.yaw = 0f;
            if (!IsFinite(c.throttle01)) c.throttle01 = 0f;
            return c;
        }

        private static bool IsFinite(float v)
        {
            return !float.IsNaN(v) && !float.IsInfinity(v);
        }

        private void ResolveConfiguration()
        {
            if (configuration == null)
                configuration = GetComponent<MavF15PilotControlledFlightDynamicsProfile>();
        }
    }
}
