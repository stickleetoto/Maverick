using System.Text;
using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// What the pilot-controlled F-15 research aircraft is doing, and how far it is from the research
    /// model's validated domain - read from the body, the aerodynamic model, the control law and the
    /// actuator, never computed a second way. Read-only: evaluating it changes nothing.
    ///
    /// It says out loud what the model does not claim: when the coefficients are extrapolated from the
    /// Mach 0.6 fit, when the research aerodynamics refuse the state (outside the source-exercised
    /// speeds or the transcribed alpha/beta span - the body then gets no aerodynamic or thrust load),
    /// and when a surface is outside what the research source exercised in its tabulated equilibria.
    /// </summary>
    public struct MavF15PilotControlledDiagnostics
    {
        public bool evaluated;

        [Header("State")]
        public float trueAirspeedFtPerSec;
        public float trueAirspeedMps;
        public float alphaDeg;
        public float betaDeg;
        public float geometricAltitudeM;
        public float pRadSec, qRadSec, rRadSec;

        [Header("Research domain")]
        public bool atCoefficientFitCondition;
        public bool sourceReproductionAdmitted;
        public bool coefficientsExtrapolatedFromFit;
        public bool insideTranscribedAlphaBetaSpan;
        public bool insideSourceExercisedSpeed;
        public string aerodynamicStatus;

        [Header("Control")]
        [Tooltip("DIRECT V1, ASSISTED V2, or none: which Maverick pilot-control law is bound to the body.")]
        public string controlLaw;
        public bool assistedV2;
        public MavF15PilotControlLawV2Debug v2;
        public bool gameplayControlActive;
        public string commandResolution;
        public bool envelopeLimited;
        public MavF15SurfaceState actualSurfaces;
        public bool symmetricStabilatorInsideSourceExercised;
        public bool lateralSurfacesInsideSourceExercised;
        public bool commandExceedsSourceExercisedInputs;

        [Header("Thrust")]
        public float throttle01;
        public string throttleStatus;
        public bool thrustProduced;

        private static readonly MavF15ResearchDemonstratedControlRange SourceExercised =
            MavF15ResearchDemonstratedControlRange.AfitBaumannTabulatedEquilibria();

        public static MavF15PilotControlledDiagnostics Evaluate(MavSixDoFBody body)
        {
            MavF15PilotControlledDiagnostics d = new MavF15PilotControlledDiagnostics();
            if (body == null)
                return d;

            MavFlightState s = body.debugState;
            d.evaluated = true;
            d.trueAirspeedMps = s.trueAirspeedMps;
            d.trueAirspeedFtPerSec = s.trueAirspeedMps / MavF15BaumannMach06Reference.FootToM;
            d.alphaDeg = s.AlphaDeg;
            d.betaDeg = s.betaRad * Mathf.Rad2Deg;
            d.geometricAltitudeM = body.debugEnvironment.geometricAltitudeM;
            d.pRadSec = s.aeroBodyRatesRadSec.x;
            d.qRadSec = s.aeroBodyRatesRadSec.y;
            d.rRadSec = s.aeroBodyRatesRadSec.z;
            d.insideSourceExercisedSpeed = MavF15SourceExercisedOperatingDomain.ContainsTrueAirspeed(s.trueAirspeedMps);

            MavF15PilotControlledAeroModel aero = body.aerodynamicModel as MavF15PilotControlledAeroModel;
            if (aero != null)
            {
                d.atCoefficientFitCondition = aero.debugAtFixedSourceCondition;
                d.sourceReproductionAdmitted = !aero.debugRefused;
                d.coefficientsExtrapolatedFromFit = aero.debugExtrapolatedFromFitCondition;
                d.insideTranscribedAlphaBetaSpan = aero.debugInsideTranscribedSpan;
                d.aerodynamicStatus = aero.debugStatus;
            }
            else
            {
                d.aerodynamicStatus = "no F-15 aerodynamic model";
            }

            MavF15PilotControlLaw law = body.controlLaw as MavF15PilotControlLaw;
            MavF15PilotControlLawV2 lawV2 = body.controlLaw as MavF15PilotControlLawV2;
            if (law != null)
            {
                d.controlLaw = "DIRECT V1 (direct gearing about the trim)";
                d.gameplayControlActive = law.isActiveAndEnabled && law.debugDroveActuator;
                d.commandResolution = law.debugCommandResolution.ToString();
                d.envelopeLimited = law.debugEnvelopeLimited;
                d.throttle01 = law.debugThrottleInactive01;
            }
            else if (lawV2 != null)
            {
                d.controlLaw = "ASSISTED V2 (rate/sideslip loops + " + MavF15PilotControlProvenance.GameplayResearchAssist + ")";
                d.assistedV2 = true;
                d.v2 = lawV2.debugLaw;
                d.gameplayControlActive = lawV2.isActiveAndEnabled && lawV2.debugDroveActuator;
                d.commandResolution = lawV2.debugCommandResolution.ToString();
                d.envelopeLimited = lawV2.debugEnvelopeLimited;
                d.throttle01 = lawV2.debugThrottleInactive01;
            }
            else
            {
                d.controlLaw = "none";
                d.commandResolution = "no pilot-control law";
            }

            MavF15ControlActuator actuator = body.controlSurfaceActuator as MavF15ControlActuator;
            if (actuator != null)
                d.actualSurfaces = actuator.ActualF15SurfaceState.channels;

            d.symmetricStabilatorInsideSourceExercised =
                SourceExercised.symmetricStabilator.Contains(d.actualSurfaces.symmetricStabilatorDeg);
            d.lateralSurfacesInsideSourceExercised =
                SourceExercised.aileron.Contains(d.actualSurfaces.aileronDeg)
                && SourceExercised.differentialStabilator.Contains(d.actualSurfaces.differentialStabilatorDeg)
                && SourceExercised.rudder.Contains(d.actualSurfaces.rudderDeg);
            d.commandExceedsSourceExercisedInputs =
                !d.symmetricStabilatorInsideSourceExercised || !d.lateralSurfacesInsideSourceExercised;

            MavF15PilotControlledFixedThrust thrust = body.propulsionModel as MavF15PilotControlledFixedThrust;
            d.thrustProduced = thrust != null && !thrust.debugRefused;
            d.throttleStatus = MavF15PilotControlledFixedThrust.ThrottleStatus;
            return d;
        }

        /// <summary>A multi-line report for a HUD or a log.</summary>
        public string Format()
        {
            StringBuilder sb = new StringBuilder(640);
            sb.AppendLine(MavF15PilotControlledIdentity.ConfigurationId);
            sb.AppendLine("research aero + Maverick control approximation | NOT NASA 836 | NOT F-15 FCS");
            if (!evaluated)
                return sb.AppendLine("not evaluated").ToString();

            sb.Append("V ").Append(trueAirspeedFtPerSec.ToString("F1")).Append(" ft/s (")
              .Append(trueAirspeedMps.ToString("F1")).Append(" m/s)  alpha ").Append(alphaDeg.ToString("F2"))
              .Append("  beta ").Append(betaDeg.ToString("F2")).AppendLine(" deg");
            sb.Append("altitude ").Append(geometricAltitudeM.ToString("F0"))
              .AppendLine(" m (geometric; the model flies the source's fixed 20,000-ft density)");
            sb.Append("p ").Append(pRadSec.ToString("F3")).Append("  q ").Append(qRadSec.ToString("F3"))
              .Append("  r ").Append(rRadSec.ToString("F3")).AppendLine(" rad/s");
            sb.Append("source fit (M 0.6): ").Append(atCoefficientFitCondition ? "AT" : "no")
              .Append(" | source reproduction: ").Append(sourceReproductionAdmitted ? "ADMITTED" : "REFUSED - no aero/thrust load")
              .AppendLine(coefficientsExtrapolatedFromFit ? " | coefficients EXTRAPOLATED from the M 0.6 fit" : "");
            sb.Append("inside source-exercised speed 218.5-699.7 ft/s: ").Append(insideSourceExercisedSpeed ? "yes" : "NO")
              .Append(" | inside transcribed alpha/beta span: ").AppendLine(insideTranscribedAlphaBetaSpan ? "yes" : "NO");
            sb.Append("control law: ").AppendLine(controlLaw);
            if (assistedV2)
            {
                sb.Append("  V2 cmd q ").Append(v2.commandedPitchRateRadSec.ToString("F3"))
                  .Append(" p_s ").Append(v2.commandedRollRateRadSec.ToString("F3"))
                  .Append(" rad/s, beta ").Append(v2.commandedSideslipDeg.ToString("F2"))
                  .Append(" deg | gain scale ").Append(v2.gainScale.ToString("F2"))
                  .AppendLine(v2.stateUsable ? "" : " | STATE NOT FINITE - feedback zeroed");
            }
            sb.Append("gameplay control: ").Append(gameplayControlActive ? "ACTIVE" : "inactive").Append(" (")
              .Append(commandResolution).Append(")").AppendLine(envelopeLimited ? " | gameplay envelope limiting" : "");
            sb.Append("stab ").Append(actualSurfaces.symmetricStabilatorDeg.ToString("F2"))
              .Append("  ail ").Append(actualSurfaces.aileronDeg.ToString("F2"))
              .Append("  diff ").Append(actualSurfaces.differentialStabilatorDeg.ToString("F2"))
              .Append("  rud ").Append(actualSurfaces.rudderDeg.ToString("F2")).AppendLine(" deg");
            sb.AppendLine(commandExceedsSourceExercisedInputs
                ? "surfaces EXCEED the source-exercised inputs (Table VII: stab -25..-5, lateral 0)"
                : "surfaces inside the source-exercised inputs");
            sb.Append(throttleStatus).Append(" (throttle ").Append(throttle01.ToString("F2")).Append(")")
              .AppendLine(thrustProduced ? "" : " | thrust REFUSED outside the research domain");
            return sb.ToString();
        }
    }
}
