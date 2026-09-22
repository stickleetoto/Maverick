using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// The complete outcome of one control-law evaluation: what was asked for, what each stage
    /// contributed, and under what provenance.
    /// </summary>
    public struct MavF15FcsSolution
    {
        public MavF15RequestedSurfaceState requested;

        /// <summary>Mechanical demand after the ratio changers, before any augmentation.</summary>
        public MavF15ControlDemand mechanicalDemand;

        public float rollDamperAuthority01;
        public bool modeConsistent;
        public string modeReason;
        public List<MavF15FcsStageResult> stages;

        /// <summary>
        /// Lowest provenance grade among stages that actually ran. This is the honest grade of the
        /// whole solution: a chain is only as sourced as its weakest contributing stage.
        /// Unavailable when nothing ran.
        /// </summary>
        public MavEngineDataProvenance EffectiveProvenance
        {
            get
            {
                MavEngineDataProvenance worst = MavEngineDataProvenance.Unavailable;
                bool any = false;

                if (stages == null)
                    return MavEngineDataProvenance.Unavailable;

                for (int i = 0; i < stages.Count; i++)
                {
                    if (!stages[i].applied)
                        continue;

                    if (!any || stages[i].provenance < worst)
                    {
                        worst = stages[i].provenance;
                        any = true;
                    }
                }

                return any ? worst : MavEngineDataProvenance.Unavailable;
            }
        }
    }

    /// <summary>
    /// F-15 control-law boundary: normalized pilot intent in, requested physical surface state out.
    ///
    /// NAMING
    /// ------
    /// This is NOT "the F-15 FLCS". A control law may only be called a real aircraft's
    /// flight-control system when it is sourced from and validated against that system, and no
    /// F-15 FCS gain has been recovered for NASA 836. What is implemented is the ARCHITECTURE of
    /// that system with every gain declared Unavailable, so the day a schedule is sourced it
    /// becomes a data change rather than a redesign.
    ///
    /// STAGES, in the order the aircraft applies them:
    ///
    ///   pilot command (normalized, stick-referenced, per-axis CAS engage)
    ///      -> mechanical path            stick/pedal gearing to surface degrees
    ///      -> PRAD / RRAD                ratio changers scale MECHANICAL authority only
    ///      = MavF15ControlDemand
    ///      -> pitch CAS                  pitch-rate and normal-acceleration feedback
    ///      -> stall inhibitor            AoA limiter on the pitch axis
    ///      -> roll CAS                   roll-rate feedback, scaled by the washout schedule
    ///      -> yaw CAS                    yaw damper (Dutch-roll damping)
    ///      -> turn coordination          rudder from coordinated-turn yaw-rate error
    ///      -> ARI                        roll COMMAND crossfed to rudder
    ///      -> roll-to-yaw crossfeed      roll RATE crossfed to rudder
    ///      = MavF15RequestedSurfaceState
    ///
    /// Each stage reports itself separately through <see cref="MavF15FcsStageResult"/>: whether it
    /// ran, what it contributed in degrees, the provenance of the gains it used, and why it did
    /// not run if it did not. A stage that cannot run contributes nothing and says so; it never
    /// silently passes its input through, because a missing augmentation stage that behaves like a
    /// working one is exactly the failure this project is trying to avoid.
    ///
    /// MODE
    /// ----
    /// <see cref="mode"/> declares a provenance floor and the law REFUSES to run at all if any
    /// declared gain sits below it. That is what stops a preproduction TM-72861 schedule and an
    /// exact NASA 836 value being quietly co-resident in one configuration.
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
        [Tooltip("Which body of evidence this configuration draws on. Declares a provenance floor; any declared gain below it makes the whole law refuse.")]
        public MavF15FcsMode mode = MavF15FcsMode.ExactNasa836Unavailable;

        [Tooltip("Gains and schedules for the whole control path. Every entry defaults to Unavailable; populate one only from a source you can cite, and record which configuration it describes.")]
        public MavF15ControlLawSchedules schedules = MavF15ControlLawSchedules.Unavailable();

        [Header("CAS Engagement")]
        [Tooltip("Per-axis CAS engage, as the real aircraft has. Disengaging an axis leaves its mechanical path intact - that is a real F-15 condition, not a fault.")]
        public bool pitchCasEngaged = true;
        public bool rollCasEngaged = true;
        public bool yawCasEngaged = true;

        [Header("Debug / Requested Surface State")]
        [Tooltip("What this law is asking the actuator for. A REQUEST - the actual position is owned by the actuator and may differ.")]
        public MavF15RequestedSurfaceState debugRequested;

        [Tooltip("Mechanical demand after the ratio changers, before augmentation.")]
        public MavF15ControlDemand debugMechanicalDemand;

        public bool debugMechanicalPathAvailable;
        public bool debugModeConsistent = true;
        public int debugAppliedStageCount;
        public bool debugClaimsExactTargetAuthority;
        public float debugRollDamperAuthority01 = 1f;
        public MavEngineDataProvenance debugEffectiveProvenance = MavEngineDataProvenance.Unavailable;

        [TextArea(3, 12)]
        public string debugStageReport = "not evaluated";

        private readonly List<MavF15FcsStageResult> stageBuffer = new List<MavF15FcsStageResult>(12);

        /// <summary>
        /// Identity used by readiness reporting and telemetry. States the current standing rather
        /// than a fixed name, so a partially-sourced law cannot be read as a validated one.
        /// </summary>
        public override string ControlLawName
        {
            get
            {
                if (schedules.IsFullyExactTargetAuthoritative)
                    return "F-15 control law (NASA 836 sourced)";

                string modeLabel = " [" + mode + "]";

                if (!schedules.MechanicalPathAvailable)
                    return "F-15 control law ARCHITECTURE ONLY (no sourced gains; outputs neutral)"
                           + modeLabel;

                return "F-15 control law PARTIALLY SOURCED (not NASA 836 FCS authority)" + modeLabel;
            }
        }

        public MavF15RequestedSurfaceState RequestedF15SurfaceState
        {
            get { return debugRequested; }
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
                f15Actuator.SetF15Command(debugRequested, debugLastOutput.throttle01);
        }

        public override MavControlInput Evaluate(
            MavFlightState state,
            MavAtmosphereSample atmosphere,
            MavPilotCommand command,
            MavFlightDynamicsProfile profile,
            float deltaTime)
        {
            MavF15PilotCommand pilot = MavF15PilotCommand.FromShared(command, true);
            pilot.pitchCasEngaged = pitchCasEngaged;
            pilot.rollCasEngaged = rollCasEngaged;
            pilot.yawCasEngaged = yawCasEngaged;

            MavF15FcsSolution solution = Solve(mode, schedules, pilot, state, stageBuffer);

            debugRequested = solution.requested;
            debugMechanicalDemand = solution.mechanicalDemand;
            debugRollDamperAuthority01 = solution.rollDamperAuthority01;
            debugModeConsistent = solution.modeConsistent;
            debugMechanicalPathAvailable = schedules.MechanicalPathAvailable;
            debugClaimsExactTargetAuthority = schedules.IsFullyExactTargetAuthoritative;
            debugEffectiveProvenance = solution.EffectiveProvenance;

            int applied = 0;
            for (int i = 0; i < solution.stages.Count; i++)
            {
                if (solution.stages[i].applied)
                    applied++;
            }
            debugAppliedStageCount = applied;
            debugStageReport = FormatStageReport(solution);

            return ToSharedInput(solution.requested, pilot.throttle01);
        }

        /// <summary>
        /// The whole stage chain, as a pure function of mode, gains, pilot intent and flight state.
        ///
        /// Static and side-effect-free so the control path can be exercised exhaustively without a
        /// GameObject or a play-mode session. It reads flight state and never writes anything: no
        /// Rigidbody, no actuator, no aerodynamics.
        /// </summary>
        /// <param name="stageBuffer">Reused list for stage results. May be null, in which case one is allocated.</param>
        public static MavF15FcsSolution Solve(
            MavF15FcsMode mode,
            MavF15ControlLawSchedules schedules,
            MavF15PilotCommand command,
            MavFlightState state,
            List<MavF15FcsStageResult> stageBuffer)
        {
            MavF15PilotCommand pilot = command.Clamped();

            List<MavF15FcsStageResult> stages = stageBuffer ?? new List<MavF15FcsStageResult>(12);
            stages.Clear();

            MavF15FcsSolution solution = new MavF15FcsSolution();
            solution.stages = stages;
            solution.rollDamperAuthority01 = 1f;
            solution.requested = MavF15RequestedSurfaceState.Neutral;
            solution.mechanicalDemand = MavF15ControlDemand.Zero;

            // ---- Mode consistency ---------------------------------------------------------
            // Checked before anything runs. A mixed-provenance configuration is refused outright
            // rather than partially applied, because a half-applied mix is the hardest kind to
            // notice afterwards.
            string modeReason;
            solution.modeConsistent = schedules.IsConsistentWith(mode, out modeReason);
            solution.modeReason = modeReason;

            if (!solution.modeConsistent)
            {
                AddAllUnavailable(stages, "mode " + mode + " refused the configuration: " + modeReason);
                return solution;
            }

            // ---- Stage 1: mechanical path -------------------------------------------------
            if (!schedules.MechanicalPathAvailable)
            {
                AddAllUnavailable(
                    stages, "no sourced mechanical gearing; no surface demand can be formed");
                return solution;
            }

            MavF15ControlDemand demand = MavF15ControlDemand.Zero;
            demand.symmetricStabilatorDeg =
                pilot.pitch * schedules.pitchStickToStabilatorDegPerUnit.value;
            demand.aileronDeg =
                pilot.roll * schedules.rollStickToAileronDegPerUnit.value;
            demand.differentialStabilatorDeg =
                pilot.roll * schedules.rollStickToDifferentialStabilatorDegPerUnit.value;
            demand.rudderDeg =
                pilot.yaw * schedules.pedalToRudderDegPerUnit.value;

            stages.Add(MavF15FcsStageResult.Applied(
                MavF15FcsStage.MechanicalPath,
                WorstOf(
                    schedules.pitchStickToStabilatorDegPerUnit,
                    schedules.rollStickToAileronDegPerUnit,
                    schedules.rollStickToDifferentialStabilatorDegPerUnit,
                    schedules.pedalToRudderDegPerUnit),
                demand.symmetricStabilatorDeg,
                "stick and pedal geared to surface degrees"));

            // ---- Stage 2: ratio changers (PRAD / RRAD) ------------------------------------
            // These scale MECHANICAL authority only, so they are applied here, before any
            // augmentation. A ratio changer does not attenuate the damper.
            if (schedules.StageAvailable(MavF15FcsStage.PitchRatioChanger))
            {
                float before = demand.symmetricStabilatorDeg;
                demand.symmetricStabilatorDeg *= schedules.pitchRatioChanger.value;
                stages.Add(MavF15FcsStageResult.Applied(
                    MavF15FcsStage.PitchRatioChanger,
                    schedules.pitchRatioChanger.provenance,
                    demand.symmetricStabilatorDeg - before,
                    "PRAD scaled mechanical pitch authority"));
            }
            else
            {
                stages.Add(MavF15FcsStageResult.NotApplied(
                    MavF15FcsStage.PitchRatioChanger,
                    "no sourced PRAD schedule; full mechanical pitch authority passed"));
            }

            if (schedules.StageAvailable(MavF15FcsStage.RollRatioChanger))
            {
                float before = demand.aileronDeg;
                demand.aileronDeg *= schedules.rollRatioChanger.value;
                demand.differentialStabilatorDeg *= schedules.rollRatioChanger.value;
                stages.Add(MavF15FcsStageResult.Applied(
                    MavF15FcsStage.RollRatioChanger,
                    schedules.rollRatioChanger.provenance,
                    demand.aileronDeg - before,
                    "RRAD scaled mechanical roll authority on both roll effectors"));
            }
            else
            {
                stages.Add(MavF15FcsStageResult.NotApplied(
                    MavF15FcsStage.RollRatioChanger,
                    "no sourced RRAD schedule; full mechanical roll authority passed"));
            }

            solution.mechanicalDemand = demand;
            MavF15SurfaceState surfaces = demand.ToSurfaceState();

            // Body rates come from the state the body published last step. The law READS them; it
            // does not derive them, and it certainly does not compute the aerodynamics that
            // produced them.
            float p = state.aeroBodyRatesRadSec.x;
            float q = state.aeroBodyRatesRadSec.y;
            float r = state.aeroBodyRatesRadSec.z;
            float alphaDeg = state.AlphaDeg;

            // ---- Stage 3: pitch CAS -------------------------------------------------------
            ApplyPitchCas(schedules, pilot, state, q, ref surfaces, stages);

            // ---- Stage 4: stall inhibitor -------------------------------------------------
            ApplyStallInhibitor(schedules, alphaDeg, ref surfaces, stages);

            // ---- Stage 5: roll CAS, scaled by the washout ---------------------------------
            solution.rollDamperAuthority01 =
                ApplyRollCas(schedules, pilot, p, alphaDeg, ref surfaces, stages);

            // ---- Stage 6: yaw CAS ---------------------------------------------------------
            ApplyYawCas(schedules, pilot, r, ref surfaces, stages);

            // ---- Stage 7: turn coordination -----------------------------------------------
            ApplyTurnCoordination(schedules, pilot, state, r, ref surfaces, stages);

            // ---- Stage 8: ARI and roll-to-yaw crossfeed -----------------------------------
            ApplyAri(schedules, pilot, ref surfaces, stages);
            ApplyRollToYawCrossfeed(schedules, pilot, p, ref surfaces, stages);

            solution.requested = MavF15RequestedSurfaceState.From(surfaces);
            return solution;
        }

        // ------------------------------------------------------------------ pitch

        private static void ApplyPitchCas(
            MavF15ControlLawSchedules schedules,
            MavF15PilotCommand pilot,
            MavFlightState state,
            float q,
            ref MavF15SurfaceState surfaces,
            List<MavF15FcsStageResult> stages)
        {
            if (!pilot.pitchCasEngaged)
            {
                stages.Add(MavF15FcsStageResult.NotApplied(
                    MavF15FcsStage.PitchCas, "pitch CAS disengaged by the pilot"));
                return;
            }

            if (!schedules.StageAvailable(MavF15FcsStage.PitchCas))
            {
                stages.Add(MavF15FcsStageResult.NotApplied(
                    MavF15FcsStage.PitchCas, "no sourced pitch CAS gain; unaugmented pitch"));
                return;
            }

            float contribution = 0f;
            StringBuilder what = new StringBuilder(96);

            if (schedules.pitchRateFeedbackDegPerRadSec.Available)
            {
                float term = -q * schedules.pitchRateFeedbackDegPerRadSec.value;
                surfaces.symmetricStabilatorDeg += term;
                contribution += term;
                what.Append("rate feedback");
            }

            // Load factor is only meaningful when the body has actually measured it. A zero
            // reading from an unflown body is not 0 g, and closing a loop on it would make the
            // CAS fight a phantom error.
            if (schedules.normalLoadFactorFeedbackDegPerG.Available)
            {
                if (state.specificForceValid)
                {
                    float term =
                        -state.LoadFactorNz * schedules.normalLoadFactorFeedbackDegPerG.value;
                    surfaces.symmetricStabilatorDeg += term;
                    contribution += term;
                    if (what.Length > 0) what.Append(" + ");
                    what.Append("load-factor feedback");
                }
                else
                {
                    if (what.Length > 0) what.Append("; ");
                    what.Append("load-factor feedback SKIPPED (accelerometer invalid)");
                }
            }

            stages.Add(MavF15FcsStageResult.Applied(
                MavF15FcsStage.PitchCas,
                WorstOf(
                    schedules.pitchRateFeedbackDegPerRadSec,
                    schedules.normalLoadFactorFeedbackDegPerG),
                contribution,
                what.ToString()));
        }

        private static void ApplyStallInhibitor(
            MavF15ControlLawSchedules schedules,
            float alphaDeg,
            ref MavF15SurfaceState surfaces,
            List<MavF15FcsStageResult> stages)
        {
            if (!schedules.StageAvailable(MavF15FcsStage.StallInhibitor))
            {
                stages.Add(MavF15FcsStageResult.NotApplied(
                    MavF15FcsStage.StallInhibitor,
                    "no sourced stall-inhibitor threshold/gradient; no AoA limiting"));
                return;
            }

            float threshold = schedules.stallInhibitorAlphaThresholdDeg.value;
            float exceedance = alphaDeg - threshold;

            if (exceedance <= 0f)
            {
                stages.Add(MavF15FcsStageResult.Applied(
                    MavF15FcsStage.StallInhibitor,
                    WorstOf(
                        schedules.stallInhibitorAlphaThresholdDeg,
                        schedules.stallInhibitorDegPerDegAlpha),
                    0f,
                    "alpha " + alphaDeg.ToString("F1") + " deg below the "
                    + threshold.ToString("F1") + " deg threshold; no limiting"));
                return;
            }

            // Sign convention is the aerodynamic model's, so the inhibitor's authority gain
            // carries the sign that produces nose-down on this airframe. The stage does not
            // impose one of its own.
            float term = exceedance * schedules.stallInhibitorDegPerDegAlpha.value;
            surfaces.symmetricStabilatorDeg += term;

            stages.Add(MavF15FcsStageResult.Applied(
                MavF15FcsStage.StallInhibitor,
                WorstOf(
                    schedules.stallInhibitorAlphaThresholdDeg,
                    schedules.stallInhibitorDegPerDegAlpha),
                term,
                "alpha " + alphaDeg.ToString("F1") + " deg exceeds threshold by "
                + exceedance.ToString("F1") + " deg"));
        }

        // ------------------------------------------------------------------ roll

        private static float ApplyRollCas(
            MavF15ControlLawSchedules schedules,
            MavF15PilotCommand pilot,
            float p,
            float alphaDeg,
            ref MavF15SurfaceState surfaces,
            List<MavF15FcsStageResult> stages)
        {
            string washoutReason;
            float authority = schedules.rollDamperWashout.AuthorityAt(alphaDeg, out washoutReason);

            if (schedules.StageAvailable(MavF15FcsStage.HighAoaRollDamperWashout))
            {
                stages.Add(MavF15FcsStageResult.Applied(
                    MavF15FcsStage.HighAoaRollDamperWashout,
                    schedules.rollDamperWashout.provenance,
                    authority,
                    washoutReason));
            }
            else
            {
                stages.Add(MavF15FcsStageResult.NotApplied(
                    MavF15FcsStage.HighAoaRollDamperWashout, washoutReason));
            }

            if (!pilot.rollCasEngaged)
            {
                stages.Add(MavF15FcsStageResult.NotApplied(
                    MavF15FcsStage.RollCas, "roll CAS disengaged by the pilot"));
                return authority;
            }

            if (!schedules.StageAvailable(MavF15FcsStage.RollCas))
            {
                stages.Add(MavF15FcsStageResult.NotApplied(
                    MavF15FcsStage.RollCas, "no sourced roll CAS gain; unaugmented roll"));
                return authority;
            }

            float term = -p * schedules.rollRateFeedbackDegPerRadSec.value * authority;
            surfaces.differentialStabilatorDeg += term;

            stages.Add(MavF15FcsStageResult.Applied(
                MavF15FcsStage.RollCas,
                schedules.rollRateFeedbackDegPerRadSec.provenance,
                term,
                "roll-rate feedback on the differential stabilator at "
                + authority.ToString("F3") + " authority"));

            return authority;
        }

        // ------------------------------------------------------------------ yaw

        private static void ApplyYawCas(
            MavF15ControlLawSchedules schedules,
            MavF15PilotCommand pilot,
            float r,
            ref MavF15SurfaceState surfaces,
            List<MavF15FcsStageResult> stages)
        {
            if (!pilot.yawCasEngaged)
            {
                stages.Add(MavF15FcsStageResult.NotApplied(
                    MavF15FcsStage.YawCas, "yaw CAS disengaged by the pilot"));
                return;
            }

            if (!schedules.StageAvailable(MavF15FcsStage.YawCas))
            {
                stages.Add(MavF15FcsStageResult.NotApplied(
                    MavF15FcsStage.YawCas, "no sourced yaw damper gain; undamped Dutch roll"));
                return;
            }

            float term = -r * schedules.yawRateFeedbackDegPerRadSec.value;
            surfaces.rudderDeg += term;

            stages.Add(MavF15FcsStageResult.Applied(
                MavF15FcsStage.YawCas,
                schedules.yawRateFeedbackDegPerRadSec.provenance,
                term,
                "yaw-rate feedback (Dutch-roll damping)"));
        }

        /// <summary>
        /// Turn coordination drives the rudder from the difference between the measured yaw rate
        /// and the yaw rate a coordinated turn at this bank and speed would need.
        ///
        /// It requires a valid attitude, because the coordinated yaw rate is g*tan(phi)/V and a
        /// body with no published attitude has no bank angle. Without one the stage is skipped
        /// rather than run against phi=0, which would silently make it a second yaw damper.
        /// </summary>
        private static void ApplyTurnCoordination(
            MavF15ControlLawSchedules schedules,
            MavF15PilotCommand pilot,
            MavFlightState state,
            float r,
            ref MavF15SurfaceState surfaces,
            List<MavF15FcsStageResult> stages)
        {
            if (!pilot.yawCasEngaged)
            {
                stages.Add(MavF15FcsStageResult.NotApplied(
                    MavF15FcsStage.TurnCoordination, "yaw CAS disengaged by the pilot"));
                return;
            }

            if (!schedules.StageAvailable(MavF15FcsStage.TurnCoordination))
            {
                stages.Add(MavF15FcsStageResult.NotApplied(
                    MavF15FcsStage.TurnCoordination, "no sourced turn-coordination gain"));
                return;
            }

            if (!state.attitude.valid)
            {
                stages.Add(MavF15FcsStageResult.NotApplied(
                    MavF15FcsStage.TurnCoordination,
                    "no valid attitude; coordinated yaw rate is undefined without bank angle"));
                return;
            }

            float speed = state.trueAirspeedMps;
            if (speed < 1f)
            {
                stages.Add(MavF15FcsStageResult.NotApplied(
                    MavF15FcsStage.TurnCoordination,
                    "airspeed too low for a coordinated-turn yaw rate"));
                return;
            }

            const float gravity = 9.80665f;
            float bankRad = state.attitude.bankAngleRad;
            float coordinatedYawRate = gravity * Mathf.Tan(bankRad) / speed;
            float error = r - coordinatedYawRate;

            float term = -error * schedules.turnCoordinationDegPerRadSec.value;
            surfaces.rudderDeg += term;

            stages.Add(MavF15FcsStageResult.Applied(
                MavF15FcsStage.TurnCoordination,
                schedules.turnCoordinationDegPerRadSec.provenance,
                term,
                "yaw-rate error " + error.ToString("F4") + " rad/s against coordinated "
                + coordinatedYawRate.ToString("F4")));
        }

        // ------------------------------------------------------------------ cross-axis

        /// <summary>
        /// ARI: roll COMMAND crossfed to rudder.
        ///
        /// Fed from the commanded roll, NOT from the augmented aileron position. The ARI exists to
        /// coordinate the pilot's roll demand; feeding it the damper's output would close an
        /// unintended loop through the roll axis, and a roll rate with the stick centred would
        /// produce rudder.
        ///
        /// OPEN MODELLING QUESTION: this takes the raw mechanical roll command, so the ARI is NOT
        /// scaled by RRAD. Whether the real F-15 crossfeeds before or after the roll ratio changer
        /// is not established by any source in this repository. The choice is visible here rather
        /// than buried, and it has no numeric consequence today because both gains are Unavailable.
        /// DN-1180.01-238-458 Rev. D would settle it.
        /// </summary>
        private static void ApplyAri(
            MavF15ControlLawSchedules schedules,
            MavF15PilotCommand pilot,
            ref MavF15SurfaceState surfaces,
            List<MavF15FcsStageResult> stages)
        {
            if (!schedules.StageAvailable(MavF15FcsStage.AileronRudderInterconnect))
            {
                stages.Add(MavF15FcsStageResult.NotApplied(
                    MavF15FcsStage.AileronRudderInterconnect,
                    "no sourced ARI gain; no roll/rudder coordination"));
                return;
            }

            float commandedAileronDeg =
                pilot.roll * schedules.rollStickToAileronDegPerUnit.value;
            float term = commandedAileronDeg * schedules.ariRudderPerAileron.value;
            surfaces.rudderDeg += term;

            stages.Add(MavF15FcsStageResult.Applied(
                MavF15FcsStage.AileronRudderInterconnect,
                WorstOf(schedules.ariRudderPerAileron, schedules.rollStickToAileronDegPerUnit),
                term,
                "roll command " + commandedAileronDeg.ToString("F2") + " deg crossfed to rudder"));
        }

        /// <summary>
        /// Roll RATE crossfed to rudder. Distinct from the ARI above, which is fed by roll command;
        /// both appear in F-15-family control-system descriptions and they are not the same path.
        /// </summary>
        private static void ApplyRollToYawCrossfeed(
            MavF15ControlLawSchedules schedules,
            MavF15PilotCommand pilot,
            float p,
            ref MavF15SurfaceState surfaces,
            List<MavF15FcsStageResult> stages)
        {
            if (!pilot.yawCasEngaged)
            {
                stages.Add(MavF15FcsStageResult.NotApplied(
                    MavF15FcsStage.RollToYawCrossfeed, "yaw CAS disengaged by the pilot"));
                return;
            }

            if (!schedules.StageAvailable(MavF15FcsStage.RollToYawCrossfeed))
            {
                stages.Add(MavF15FcsStageResult.NotApplied(
                    MavF15FcsStage.RollToYawCrossfeed, "no sourced roll-to-yaw crossfeed gain"));
                return;
            }

            float term = p * schedules.rollRateToYawCrossfeedDegPerRadSec.value;
            surfaces.rudderDeg += term;

            stages.Add(MavF15FcsStageResult.Applied(
                MavF15FcsStage.RollToYawCrossfeed,
                schedules.rollRateToYawCrossfeedDegPerRadSec.provenance,
                term,
                "roll rate " + p.ToString("F3") + " rad/s crossfed to rudder"));
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>
        /// Lowest provenance grade among the supplied gains, ignoring unavailable ones. A stage is
        /// only as sourced as the weakest number it used.
        /// </summary>
        private static MavEngineDataProvenance WorstOf(params MavF15ControlGain[] gains)
        {
            MavEngineDataProvenance worst = MavEngineDataProvenance.Unavailable;
            bool any = false;

            for (int i = 0; i < gains.Length; i++)
            {
                if (!gains[i].Available)
                    continue;

                if (!any || gains[i].provenance < worst)
                {
                    worst = gains[i].provenance;
                    any = true;
                }
            }

            return worst;
        }

        private static void AddAllUnavailable(List<MavF15FcsStageResult> stages, string reason)
        {
            for (int i = 0; i <= (int)MavF15FcsStage.RollToYawCrossfeed; i++)
                stages.Add(MavF15FcsStageResult.NotApplied((MavF15FcsStage)i, reason));
        }

        /// <summary>
        /// Projects the F-15 request onto the shared contract for the base pipeline. Differential
        /// stabilator is dropped here deliberately - it has no shared field, and folding it into
        /// the aileron field would hand every downstream reader two surfaces added together. The
        /// full four-channel state goes to the actuator separately in FixedUpdate.
        /// </summary>
        private static MavControlInput ToSharedInput(
            MavF15RequestedSurfaceState requested, float throttle01)
        {
            return new MavControlInput
            {
                throttle01 = Mathf.Clamp01(throttle01),
                elevatorDeg = requested.channels.symmetricStabilatorDeg,
                aileronDeg = requested.channels.aileronDeg,
                rudderDeg = requested.channels.rudderDeg,
                leadingEdgeFlapDeg = 0f
            };
        }

        private static string FormatStageReport(MavF15FcsSolution solution)
        {
            StringBuilder sb = new StringBuilder(1024);
            sb.Append("mode: ").AppendLine(solution.modeReason);
            sb.Append("effective provenance: ").AppendLine(solution.EffectiveProvenance.ToString());

            for (int i = 0; i < solution.stages.Count; i++)
            {
                MavF15FcsStageResult r = solution.stages[i];
                sb.Append(r.applied ? "  [on ] " : "  [off] ")
                  .Append(r.stage.ToString())
                  .Append(": ")
                  .Append(r.reason);

                if (r.applied)
                {
                    sb.Append("  (")
                      .Append(r.contributionDeg.ToString("F3"))
                      .Append(", ")
                      .Append(r.provenance.ToString())
                      .Append(")");
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
