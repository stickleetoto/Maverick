using System.Text;
using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// F-15 control-law boundary: normalized pilot intent in, requested physical surface state out.
    ///
    /// NAMING
    /// ------
    /// This is NOT "the F-15 FLCS". The project rule is that a control law may only be called a
    /// real aircraft's flight-control system when it is sourced from and validated against that
    /// system, and no F-15 FCS gain has been recovered for NASA 836. What is implemented here is
    /// the ARCHITECTURE of that system with every gain declared Unavailable, so the day a
    /// schedule is sourced it becomes a data change rather than a redesign.
    /// <see cref="ControlLawName"/> reports the current standing and refuses to claim exact-target
    /// authority until every gain is Authoritative.
    ///
    /// STAGES, in the order the aircraft applies them:
    ///
    ///   pilot command (normalized, stick-referenced)
    ///      -> mechanical path            stick/pedal gearing to surface degrees
    ///      -> PRAD / RRAD                ratio changers scale mechanical authority
    ///      -> pitch / roll / yaw CAS     rate and load-factor augmentation
    ///      -> ARI                        roll command crossfed to rudder
    ///      -> high-AOA washout           roll-rate feedback faded out at high alpha
    ///      -> requested surface state
    ///
    /// Each stage is separately gated on its own gains. An unavailable stage contributes nothing
    /// and is named in the status string; it does not silently pass its input through, because a
    /// missing augmentation stage that behaves like a working one is exactly the failure this
    /// project is trying to avoid.
    ///
    /// FAIL-CLOSED
    /// -----------
    /// With no mechanical gearing there is no surface demand at all, so the law outputs neutral.
    /// That is the current state and it is intentional. An aircraft that cannot be flown because
    /// its control system is honestly unavailable is a better outcome than one flying on invented
    /// gains.
    ///
    /// OWNERSHIP
    /// ---------
    /// This computes a REQUEST. It never writes to the Rigidbody, never computes a force or
    /// moment, and never sets an actual surface position - <see cref="MavF15ControlActuator"/>
    /// owns those. The base class drives the pipeline at execution order -300, ahead of the
    /// actuator at -200 and the body at -100.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MavF15ControlLaw : MavFlightControlLawBase
    {
        [Header("F-15 Control System")]
        [Tooltip("Gains and schedules for the whole control path. Every entry defaults to Unavailable; populate one only from a source you can cite, and record which configuration it describes.")]
        public MavF15ControlLawSchedules schedules = MavF15ControlLawSchedules.Unavailable();

        [Header("Debug / Requested Surface State")]
        [Tooltip("What this law is asking the actuator for. A REQUEST - the actual position is owned by the actuator and may differ.")]
        public MavF15SurfaceState debugRequestedSurfaces;

        public bool debugMechanicalPathAvailable;
        public int debugAvailableStageCount;
        public bool debugClaimsExactTargetAuthority;
        public float debugRollDamperWashout01 = 1f;

        [TextArea(2, 6)]
        public string debugStageReport = "not evaluated";

        /// <summary>
        /// Identity used by readiness reporting and telemetry. It states the current standing
        /// rather than a fixed name, so a partially-sourced law cannot be read as a validated one.
        /// </summary>
        public override string ControlLawName
        {
            get
            {
                if (schedules.IsFullyExactTargetAuthoritative)
                    return "F-15 control law (NASA 836 sourced)";

                if (!schedules.MechanicalPathAvailable)
                    return "F-15 control law ARCHITECTURE ONLY (no sourced gains; outputs neutral)";

                return "F-15 control law PARTIALLY SOURCED (not NASA 836 FCS authority)";
            }
        }

        /// <summary>
        /// The four-channel request from the last evaluation. The F-15 actuator reads this rather
        /// than the shared <see cref="MavControlInput"/>, which has nowhere to put differential
        /// stabilator.
        /// </summary>
        public MavF15SurfaceState RequestedF15SurfaceState
        {
            get { return debugRequestedSurfaces; }
        }

        protected override void FixedUpdate()
        {
            // The base pipeline resolves the command, evaluates, and publishes the three shared
            // channels. Differential stabilator has no shared field, so it is pushed immediately
            // afterwards, from the SAME evaluation - nothing is recomputed and the four channels
            // cannot disagree.
            base.FixedUpdate();

            if (!driveActuatorInFixedUpdate || !debugDroveActuator)
                return;

            MavF15ControlActuator f15Actuator = actuator as MavF15ControlActuator;
            if (f15Actuator != null)
                f15Actuator.SetF15Command(debugRequestedSurfaces, debugLastOutput.throttle01);
        }

        public override MavControlInput Evaluate(
            MavFlightState state,
            MavAtmosphereSample atmosphere,
            MavPilotCommand command,
            MavFlightDynamicsProfile profile,
            float deltaTime)
        {
            MavPilotCommand pilot = command.Clamped();
            StringBuilder stages = new StringBuilder(512);

            debugMechanicalPathAvailable = schedules.MechanicalPathAvailable;
            debugClaimsExactTargetAuthority = schedules.IsFullyExactTargetAuthoritative;
            debugAvailableStageCount = CountAvailableStages(schedules);

            debugRequestedSurfaces =
                Solve(schedules, pilot, state, out debugRollDamperWashout01, stages);
            debugStageReport = stages.ToString();

            return ToSharedInput(debugRequestedSurfaces, pilot.throttle01);
        }

        /// <summary>
        /// The whole stage chain, as a pure function of gains, pilot intent and flight state.
        ///
        /// Static and side-effect-free so the control path can be exercised exhaustively without
        /// a GameObject or a play-mode session, exactly as the rest of the FDM validation does.
        /// It reads flight state and never writes anything: no Rigidbody, no actuator, no
        /// aerodynamics.
        /// </summary>
        /// <param name="stages">Optional per-stage log. May be null.</param>
        public static MavF15SurfaceState Solve(
            MavF15ControlLawSchedules schedules,
            MavPilotCommand command,
            MavFlightState state,
            out float rollDamperWashout01,
            StringBuilder stages)
        {
            MavPilotCommand pilot = command.Clamped();
            rollDamperWashout01 = 1f;

            MavF15SurfaceState surfaces = MavF15SurfaceState.Neutral;

            // ---- Stage 1: mechanical path -------------------------------------------------
            // Without gearing there is no demand to augment, so everything downstream is moot.
            if (!schedules.MechanicalPathAvailable)
            {
                Log(stages, "MechanicalPath: UNAVAILABLE - no surface demand can be formed");
                AppendRemainingStages(schedules, stages);
                return MavF15SurfaceState.Neutral;
            }

            surfaces.symmetricStabilatorDeg =
                pilot.pitch * schedules.pitchStickToStabilatorDegPerUnit.value;
            surfaces.aileronDeg =
                pilot.roll * schedules.rollStickToAileronDegPerUnit.value;
            surfaces.differentialStabilatorDeg =
                pilot.roll * schedules.rollStickToDifferentialStabilatorDegPerUnit.value;
            surfaces.rudderDeg =
                pilot.yaw * schedules.pedalToRudderDegPerUnit.value;

            Log(stages, "MechanicalPath: applied");

            // ---- Stage 2: ratio changers (PRAD / RRAD) ------------------------------------
            // These scale MECHANICAL authority only, so they are applied before augmentation -
            // a ratio changer does not attenuate the damper.
            if (schedules.StageAvailable(MavF15FcsStage.PitchRatioChanger))
            {
                surfaces.symmetricStabilatorDeg *= schedules.pitchRatioChanger.value;
                Log(stages, "PitchRatioChanger (PRAD): applied");
            }
            else
            {
                Log(stages, "PitchRatioChanger (PRAD): UNAVAILABLE - full mechanical authority passed");
            }

            if (schedules.StageAvailable(MavF15FcsStage.RollRatioChanger))
            {
                surfaces.aileronDeg *= schedules.rollRatioChanger.value;
                surfaces.differentialStabilatorDeg *= schedules.rollRatioChanger.value;
                Log(stages, "RollRatioChanger (RRAD): applied");
            }
            else
            {
                Log(stages, "RollRatioChanger (RRAD): UNAVAILABLE - full mechanical authority passed");
            }

            // ---- Stage 3: control augmentation --------------------------------------------
            // Body rates come from the state the body published last step. The law reads them; it
            // does not derive them, and it certainly does not compute the aerodynamics that
            // produced them.
            float p = state.aeroBodyRatesRadSec.x;
            float q = state.aeroBodyRatesRadSec.y;
            float r = state.aeroBodyRatesRadSec.z;

            if (schedules.StageAvailable(MavF15FcsStage.PitchCas))
            {
                if (schedules.pitchRateFeedbackDegPerRadSec.Available)
                {
                    surfaces.symmetricStabilatorDeg -=
                        q * schedules.pitchRateFeedbackDegPerRadSec.value;
                    Log(stages, "PitchCAS rate feedback: applied");
                }

                // Load factor is only meaningful when the body has actually measured it. A zero
                // reading from an unflown body is not 0 g, and treating it as one would make the
                // feedback fight a phantom error.
                if (schedules.normalLoadFactorFeedbackDegPerG.Available)
                {
                    if (state.specificForceValid)
                    {
                        surfaces.symmetricStabilatorDeg -=
                            state.LoadFactorNz * schedules.normalLoadFactorFeedbackDegPerG.value;
                        Log(stages, "PitchCAS load-factor feedback: applied");
                    }
                    else
                    {
                        Log(stages, "PitchCAS load-factor feedback: SKIPPED - no valid accelerometer reading");
                    }
                }
            }
            else
            {
                Log(stages, "PitchCAS: UNAVAILABLE - unaugmented pitch");
            }

            // High-AOA washout fades roll-rate feedback out, so it is resolved before roll CAS.
            rollDamperWashout01 = ResolveRollDamperWashout(schedules, state, stages);

            if (schedules.StageAvailable(MavF15FcsStage.RollCas))
            {
                surfaces.differentialStabilatorDeg -=
                    p * schedules.rollRateFeedbackDegPerRadSec.value * rollDamperWashout01;
                Log(stages, "RollCAS: applied");
            }
            else
            {
                Log(stages, "RollCAS: UNAVAILABLE - unaugmented roll");
            }

            if (schedules.StageAvailable(MavF15FcsStage.YawCas))
            {
                surfaces.rudderDeg -= r * schedules.yawRateFeedbackDegPerRadSec.value;
                Log(stages, "YawCAS (yaw damper): applied");
            }
            else
            {
                Log(stages, "YawCAS (yaw damper): UNAVAILABLE - undamped yaw");
            }

            // ---- Stage 4: aileron-rudder interconnect -------------------------------------
            // Crossfed from the COMMANDED roll, not from the augmented aileron position: the ARI
            // exists to coordinate the pilot's roll demand, and feeding it the damper's output
            // would close an unintended loop through the roll axis.
            if (schedules.StageAvailable(MavF15FcsStage.AileronRudderInterconnect))
            {
                float commandedAileronDeg =
                    pilot.roll * schedules.rollStickToAileronDegPerUnit.value;
                surfaces.rudderDeg +=
                    commandedAileronDeg * schedules.ariRudderPerAileron.value;
                Log(stages, "ARI: applied");
            }
            else
            {
                Log(stages, "ARI: UNAVAILABLE - no roll/rudder coordination");
            }

            return surfaces;
        }

        /// <summary>
        /// 1 below the washout start alpha, 0 above the end alpha, linear between. When the
        /// schedule is unavailable the washout is NOT applied - returning 1 leaves roll-rate
        /// feedback at full authority, which is the behaviour of an aircraft without the feature
        /// rather than an invented fade.
        /// </summary>
        private static float ResolveRollDamperWashout(
            MavF15ControlLawSchedules schedules,
            MavFlightState state,
            StringBuilder stages)
        {
            if (!schedules.StageAvailable(MavF15FcsStage.HighAoaRollDamperWashout))
            {
                Log(stages,
                    "HighAoaRollDamperWashout: UNAVAILABLE - roll damper stays at full authority");
                return 1f;
            }

            float alphaDeg = state.AlphaDeg;
            float start = schedules.rollDamperWashoutStartAlphaDeg.value;
            float end = schedules.rollDamperWashoutEndAlphaDeg.value;

            float washout = 1f - Mathf.Clamp01((alphaDeg - start) / (end - start));
            Log(stages, "HighAoaRollDamperWashout: applied, factor " + washout.ToString("F3"));

            return washout;
        }

        /// <summary>
        /// Projects the F-15 request onto the shared contract for the base pipeline. Differential
        /// stabilator is dropped here deliberately - it has no shared field, and folding it into
        /// the aileron field would hand every downstream reader two surfaces added together. The
        /// full four-channel state goes to the actuator separately in FixedUpdate.
        /// </summary>
        private static MavControlInput ToSharedInput(MavF15SurfaceState surfaces, float throttle01)
        {
            return new MavControlInput
            {
                throttle01 = Mathf.Clamp01(throttle01),
                elevatorDeg = surfaces.symmetricStabilatorDeg,
                aileronDeg = surfaces.aileronDeg,
                rudderDeg = surfaces.rudderDeg,
                leadingEdgeFlapDeg = 0f
            };
        }

        /// <summary>Stage logging is optional so Solve can be called in a tight validation loop.</summary>
        private static void Log(StringBuilder stages, string line)
        {
            if (stages != null)
                stages.AppendLine(line);
        }

        private static int CountAvailableStages(MavF15ControlLawSchedules schedules)
        {
            int n = 0;
            for (int i = 0; i <= (int)MavF15FcsStage.HighAoaRollDamperWashout; i++)
            {
                if (schedules.StageAvailable((MavF15FcsStage)i))
                    n++;
            }
            return n;
        }

        private static void AppendRemainingStages(
            MavF15ControlLawSchedules schedules, StringBuilder stages)
        {
            for (int i = 1; i <= (int)MavF15FcsStage.HighAoaRollDamperWashout; i++)
            {
                MavF15FcsStage stage = (MavF15FcsStage)i;
                Log(stages, stage + (schedules.StageAvailable(stage)
                    ? ": available but unreachable without the mechanical path"
                    : ": UNAVAILABLE"));
            }
        }
    }
}
