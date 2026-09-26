using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// WP-4A. Everything a research runtime body is about to run with, stated before it runs.
    /// Diagnostics only: building one changes nothing on the body.
    /// </summary>
    [Serializable]
    public struct MavF15ResearchRuntimePreparationReport
    {
        [Tooltip("No blocker below. Not a readiness level and not arming: the body stays unarmed.")]
        public bool prepared;

        [Tooltip("Density AND gravity are the source's own. False means the runtime can run, but does not reproduce the source's equations - see equivalenceGaps.")]
        public bool sourceEquivalentEnvironment;

        [Tooltip("Whether the body is armed. Research preparation happens before arming; this phase never arms, so this should read false.")]
        public bool armed;

        public string configurationId;
        public bool authorityGranted;
        public string authorityReason;

        public string densitySource;
        public float densityKgM3;
        public string gravitySource;
        public float gravityMps2;

        public string aeroModel;
        public string thrustSemantic;
        public string surfaceSemantic;

        public string sourceFitCondition;
        public string conditionMode;

        public float trueAirspeedMps;
        public float trueAirspeedFtPerSec;

        [Tooltip("V over the speed of sound of the sample - report-only; the source computes no Mach.")]
        public float mach;

        [Tooltip("True when the state is away from the M 0.6 / 20,000 ft coefficient-fit condition: every coefficient is then the Mach 0.6 fit extrapolated (source-exercised, not aerodynamically validated).")]
        public bool outsideFitCondition;

        [Tooltip("True when V lies in the speeds the source's own tabulated equilibria span, 218.5-699.7 ft/s.")]
        public bool insideSourceExercisedSpeedSpan;

        public bool conditionGateAdmits;
        public string conditionGateReason;

        public float geometricAltitudeM;
        public string altitudeSemantic;

        public string blockers;
        public string equivalenceGaps;

        public string ToMultilineString()
        {
            StringBuilder b = new StringBuilder(1024);
            b.Append("configuration id        : ").AppendLine(configurationId);
            b.Append("research authority      : ").AppendLine(authorityReason);
            b.Append("armed (live FDM)        : ").AppendLine(armed ? "YES" : "no");
            b.Append("density source          : ").Append(densitySource).Append(" = ").Append(densityKgM3.ToString("F6")).AppendLine(" kg/m^3");
            b.Append("gravity source          : ").Append(gravitySource).Append(" = ").Append(gravityMps2.ToString("F7")).AppendLine(" m/s^2");
            b.Append("aero model              : ").AppendLine(aeroModel);
            b.Append("thrust semantic         : ").AppendLine(thrustSemantic);
            b.Append("surface semantic        : ").AppendLine(surfaceSemantic);
            b.Append("source-fit condition    : ").Append(sourceFitCondition).Append(" (mode ").Append(conditionMode).AppendLine(")");
            b.Append("current V               : ").Append(trueAirspeedFtPerSec.ToString("F2")).Append(" ft/s (")
                .Append(trueAirspeedMps.ToString("F3")).Append(" m/s), Mach ").Append(mach.ToString("F4")).AppendLine(" report-only");
            b.Append("outside M 0.6 fit       : ").AppendLine(outsideFitCondition ? "YES - coefficients extrapolated from the M 0.6 fit" : "no");
            b.Append("inside source V span    : ").AppendLine(insideSourceExercisedSpeedSpan ? "yes" : "NO");
            b.Append("condition gate          : ").Append(conditionGateAdmits ? "admits - " : "REFUSES - ").AppendLine(conditionGateReason);
            b.Append("altitude                : ").AppendLine(altitudeSemantic);
            b.Append("source-equivalent env   : ").AppendLine(sourceEquivalentEnvironment ? "yes" : "no - " + equivalenceGaps);
            b.Append("blockers                : ").AppendLine(string.IsNullOrEmpty(blockers) ? "none" : blockers);
            return b.ToString();
        }
    }

    /// <summary>
    /// WP-4A. Builds <see cref="MavF15ResearchRuntimePreparationReport"/> for a body: configuration,
    /// environment, aero model, thrust and surface semantics, and where the current state sits
    /// against the source-fit condition and the source-exercised speed span.
    ///
    /// Leaving Mach 0.6 is NOT a blocker by itself. What blocks is what the research condition
    /// gate refuses - which keeps the StrictFitCondition / SourceReproduction distinction exactly
    /// as the aerodynamic model and the research thrust apply it.
    /// </summary>
    public static class MavF15AfitResearchRuntimePreparation
    {
        public static MavF15ResearchRuntimePreparationReport Evaluate(MavSixDoFBody body)
        {
            MavF15ResearchRuntimePreparationReport r = new MavF15ResearchRuntimePreparationReport();
            List<string> blockers = new List<string>();
            List<string> gaps = new List<string>();

            r.sourceFitCondition = MavF15AfitResearchIdentity.SourceConditionLabel + " (coefficient-fit condition)";

            if (body == null)
            {
                r.configurationId = "(none)";
                r.authorityReason = "no body";
                r.blockers = "no six-DoF body";
                return r;
            }

            r.configurationId = body.activeProfile != null ? body.activeProfile.profileId : "(no profile built)";
            string reason;
            r.authorityGranted = MavF15ResearchRuntimeAuthority.TryGrant(body, out reason);
            r.authorityReason = reason;
            if (!r.authorityGranted)
                blockers.Add(reason);

            r.armed = body.simulationEnabled;
            if (r.armed)
                blockers.Add("the body is armed; research preparation happens before arming and this phase never arms");

            MavF15AfitResearchFlightDynamicsProfile profile = body.profileProvider as MavF15AfitResearchFlightDynamicsProfile;
            MavF15ResearchConditionMode mode = profile != null
                ? profile.conditionMode
                : MavF15ResearchConditionMode.StrictFitCondition;
            r.conditionMode = mode.ToString();

            // ---- environment -----------------------------------------------------------------
            MavFlightEnvironment environment = body.ResolveEnvironment(body.transform.position.y);
            r.densitySource = environment.densitySource + " - "
                + MavF15AfitResearchRuntimeEnvironment.DensityLabel(environment.densitySource);
            r.densityKgM3 = environment.atmosphere.densityKgM3;
            if (environment.OwnsGravityThroughLoadSet)
            {
                r.gravitySource = environment.gravitySource + " - "
                    + MavF15AfitResearchRuntimeEnvironment.GravityLabel(environment.gravitySource);
                r.gravityMps2 = environment.loadSetGravityMps2;
            }
            else
            {
                r.gravitySource = environment.gravitySource + " - "
                    + MavF15AfitResearchRuntimeEnvironment.GravityLabel(environment.gravitySource);
                r.gravityMps2 = Physics.gravity.magnitude;
            }

            if (!environment.valid)
                blockers.Add("environment " + environment.status);

            if (environment.densitySource != MavDensitySource.ResearchSourceFixedDensity)
            {
                gaps.Add("density: standard atmosphere, q x " + (environment.atmosphere.densityKgM3
                    / MavF15AfitResearchRuntimeEnvironment.SourceDensityKgM3).ToString("F6")
                    + " of the source's at this altitude");
            }

            if (!environment.OwnsGravityThroughLoadSet)
            {
                gaps.Add("gravity: Unity " + Physics.gravity.magnitude.ToString("F5") + " m/s^2 vs source "
                    + MavF15AfitResearchRuntimeEnvironment.SourceGravityMps2.ToString("F7") + " m/s^2 ("
                    + ((Physics.gravity.magnitude / MavF15AfitResearchRuntimeEnvironment.SourceGravityMps2) - 1f).ToString("E2") + ")");
            }

            r.sourceEquivalentEnvironment = environment.valid && gaps.Count == 0;
            r.equivalenceGaps = string.Join("; ", gaps.ToArray());

            // ---- aerodynamics ------------------------------------------------------------------
            MavF15AeroModel aero = body.aerodynamicModel as MavF15AeroModel;
            if (aero == null)
            {
                r.aeroModel = "NOT the F-15 research aero model ("
                    + (body.aerodynamicModel == null ? "none" : body.aerodynamicModel.GetType().Name) + ")";
                blockers.Add(r.aeroModel);
            }
            else
            {
                r.aeroModel = MavF15BaumannMach06Reference.ModelId + " / " + aero.sourceMode
                    + (aero.allowCrossValidationResearchModel ? " / cross-validation opt-in" : " / NO cross-validation opt-in")
                    + " - transcribed AFIT/Baumann/Davison routine, M 0.6 / 20,000 ft fit, thrust NOT included";
                if (aero.sourceMode != MavF15AeroSourceMode.BaumannMach06SixAxisResearch)
                    blockers.Add("research aero is not in six-axis research mode (" + aero.sourceMode + ")");
                if (!aero.allowCrossValidationResearchModel)
                    blockers.Add("research aero cross-validation opt-in is off: the model returns zero");
            }

            // ---- thrust ------------------------------------------------------------------------
            MavF15AfitResearchFixedThrust thrust = body.propulsionModel as MavF15AfitResearchFixedThrust;
            int propulsionModels = body.GetComponents<MavPropulsionModelBase>().Length;
            if (thrust == null)
            {
                r.thrustSemantic = "NOT the research fixed total thrust ("
                    + (body.propulsionModel == null ? "no propulsion model" : body.propulsionModel.GetType().Name) + ")";
                blockers.Add(r.thrustSemantic);
            }
            else
            {
                r.thrustSemantic = "fixed TOTAL aircraft thrust "
                    + MavF15AfitResearchThrustSource.SourceTotalThrustLbf.ToString("F0") + " lbf ("
                    + MavF15AfitResearchThrustSource.TotalThrustN.ToString("F1") + " N) along body +X, plus the "
                    + MavF15AfitResearchThrustSource.SourceThrustLineOffsetIn.ToString("F2") + "-in thrust-line moment ("
                    + MavF15AfitResearchThrustSource.ThrustLinePitchingMomentNm.ToString("F2")
                    + " N m nose-up); each once through the load set; no throttle (ignored), no engine split, no F100 "
                    + "deck, no NASA 836 path; a research constant, not an F-15 engine rating";
            }

            if (propulsionModels > 1)
                blockers.Add(propulsionModels + " propulsion models on the aircraft: thrust must have exactly one owner");

            // ---- surfaces ----------------------------------------------------------------------
            MavF15ControlActuator actuator = body.controlSurfaceActuator as MavF15ControlActuator;
            MavF15AfitResearchStaticSurfaceHold hold = body.GetComponent<MavF15AfitResearchStaticSurfaceHold>();
            if (actuator == null)
            {
                r.surfaceSemantic = "NO F-15 actuator owns actual surface state";
                blockers.Add(r.surfaceSemantic);
            }
            else
            {
                MavF15SurfaceState held;
                string holdReason = "no static surface hold on the aircraft";
                bool granted = hold != null && hold.enabled && hold.TryResolveHeldState(out held, out holdReason);

                r.surfaceSemantic = granted
                    ? "source-defined STATIC setting - " + MavF15AfitResearchStaticSurfaceHold.Describe(hold.setting)
                      + "; commands ignored; actuator travel channels declared: " + actuator.limits.AvailableChannelCount
                    : "no source-defined static setting (" + holdReason + "): surfaces neutral, zero travel";

                if (!granted)
                    blockers.Add("no granted source-defined static surface setting: " + holdReason);

                if (actuator.limits.AvailableChannelCount != 0
                    || MavF15ActuatorRateLimits.FromActuatorLimits(actuator.limits).AnyDeclared)
                {
                    blockers.Add("the research actuator declares travel or rate authority, which the research configuration does not have");
                }

                if (aero != null && aero.surfaceOwner != null && aero.surfaceOwner != actuator)
                    blockers.Add("the research aero model reads a different actuator than the body's");
            }

            // ---- state against the source condition --------------------------------------------
            MavFlightState state = MavF15AfitResearchStateInjection.ReadBack(body);
            r.trueAirspeedMps = state.trueAirspeedMps;
            r.trueAirspeedFtPerSec = state.trueAirspeedMps / MavF15BaumannMach06Reference.FootToM;
            r.mach = state.mach;
            r.insideSourceExercisedSpeedSpan = MavF15SourceExercisedOperatingDomain.ContainsTrueAirspeed(state.trueAirspeedMps);

            bool insideFit;
            string gateReason;
            r.conditionGateAdmits = MavF15ResearchConditionGate.Admits(
                mode, state, environment.atmosphere, out insideFit, out gateReason);
            r.conditionGateReason = gateReason;
            r.outsideFitCondition = !insideFit;
            if (!r.conditionGateAdmits)
                blockers.Add("the research condition gate refuses the current state: " + gateReason);

            r.geometricAltitudeM = body.transform.position.y;
            r.altitudeSemantic = environment.densitySource == MavDensitySource.ResearchSourceFixedDensity
                ? "geometric " + r.geometricAltitudeM.ToString("F1") + " m - NOT a source state: the density is the "
                  + "source's fixed 20,000-ft value wherever the body is"
                : "geometric " + r.geometricAltitudeM.ToString("F1") + " m - drives the standard-atmosphere density "
                  + "(and the research gate's 20,000-ft altitude check)";

            r.blockers = string.Join("; ", blockers.ToArray());
            r.prepared = blockers.Count == 0;
            return r;
        }
    }
}
