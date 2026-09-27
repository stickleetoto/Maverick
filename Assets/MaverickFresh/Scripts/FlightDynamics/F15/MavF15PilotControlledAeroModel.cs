using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// Aerodynamics of the PILOT-CONTROLLED F-15 research aircraft: the frozen AFIT / Baumann /
    /// Davison six-axis research routines, evaluated exactly as <see cref="MavF15AeroModel"/> evaluates
    /// them in its six-axis research mode under SourceReproduction - the same reference geometry, the
    /// same rate normalization, the same condition gate and transcribed alpha/beta span gate, the same
    /// longitudinal and lateral-directional coefficient routines, called unchanged.
    ///
    /// WHY A SEPARATE COMPONENT. <see cref="MavF15AeroModel"/> is a frozen research-baseline file: it
    /// honours SourceReproduction only for the frozen research profile, and changing it would change the
    /// frozen baseline. This wrapper adds no coefficient, term or tuning; MavF15PilotControlValidation
    /// ([P8]) shows its coefficients equal MavF15AeroModel's bit for bit at the same state and surfaces.
    /// A body carries one aerodynamic model - this one or that one, never both.
    ///
    /// FAIL-CLOSED. It publishes coefficients only for a body <see cref="MavF15PilotControlledAuthority"/>
    /// grants, only with an F-15 actuator owning the surfaces (no research fallback adapter), and only
    /// where the research condition gate (SourceReproduction: the source-exercised 218.5-699.7 ft/s at the
    /// source's fixed density) and the transcribed alpha/beta span admit the state. Anywhere else it
    /// returns zero and says why; the coefficients are never extrapolated beyond what the gates admit.
    /// Like every aerodynamic model it returns coefficients only; <see cref="MavSixDoFBody"/> applies loads.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MavF15PilotControlledAeroModel : MavAerodynamicModelBase
    {
        [Tooltip("Explicit opt-in: the AFIT/Baumann/Davison model is a research CROSS-VALIDATION model, not NASA 836 and not the production F-15. Off by default; the pilot-controlled rig sets it.")]
        public bool acknowledgeResearchAerodynamics;

        [Tooltip("The actuator that owns the actual F-15 surfaces. Resolved from this GameObject when empty. Required.")]
        public MavF15ControlActuator surfaceOwner;

        [Min(0.1f)]
        public float minimumRateNormalizationSpeedMps = 1f;

        [Header("Debug")]
        public bool debugRefused = true;
        public bool debugAtFixedSourceCondition;
        public bool debugExtrapolatedFromFitCondition;
        public bool debugInsideTranscribedSpan;
        public string debugStatus = "not evaluated";
        public float debugPHat;
        public float debugQHat;
        public float debugRHat;
        public MavAeroCoefficients debugLastCoefficients;

        public const MavF15ResearchConditionMode ConditionMode = MavF15ResearchConditionMode.SourceReproduction;

        public override MavAeroCoefficients Evaluate(
            MavFlightState state,
            MavControlInput input,
            MavAtmosphereSample atmosphere)
        {
            debugRefused = false;
            debugAtFixedSourceCondition = false;
            debugExtrapolatedFromFitCondition = false;
            debugInsideTranscribedSpan = false;
            debugPHat = 0f;
            debugQHat = 0f;
            debugRHat = 0f;
            debugLastCoefficients = MavAeroCoefficients.Zero;

            if (!acknowledgeResearchAerodynamics)
                return Refuse("research aerodynamics not acknowledged (cross-validation research model; explicit opt-in is false)");

            string reason;
            if (!MavF15PilotControlledAuthority.TryGrant(GetComponent<MavSixDoFBody>(), out reason))
                return Refuse(reason);

            if (surfaceOwner == null)
                surfaceOwner = GetComponent<MavF15ControlActuator>();
            if (surfaceOwner == null)
                return Refuse("no F-15 actuator owns the surfaces; the pilot-controlled aircraft has no fallback surface adapter");

            MavAeroCoefficients c;
            bool insideFit;
            bool insideSpan;
            float pHat, qHat, rHat;
            if (!TryEvaluateResearchSixAxis(
                    state, atmosphere, surfaceOwner.ActualF15SurfaceState, minimumRateNormalizationSpeedMps,
                    out c, out insideFit, out insideSpan, out pHat, out qHat, out rHat, out reason))
            {
                debugInsideTranscribedSpan = insideSpan;
                return Refuse(reason);
            }

            referenceGeometry = MavF15BaumannMach06Reference.CreateReferenceGeometry();
            debugAtFixedSourceCondition = insideFit;
            debugExtrapolatedFromFitCondition = !insideFit;
            debugInsideTranscribedSpan = insideSpan;
            debugPHat = pHat;
            debugQHat = qHat;
            debugRHat = rHat;
            debugLastCoefficients = c;
            debugStatus = MavF15BaumannMach06Reference.ModelId + " / SIX-AXIS RESEARCH (pilot-controlled) / " + reason;
            return c;
        }

        /// <summary>
        /// The six-axis research coefficients under SourceReproduction, as a pure function of state,
        /// air and actual surfaces - the same sequence of gates and routines MavF15AeroModel runs.
        /// </summary>
        public static bool TryEvaluateResearchSixAxis(
            MavFlightState state,
            MavAtmosphereSample atmosphere,
            MavF15ActualSurfaceState surfaces,
            float minimumRateNormalizationSpeedMps,
            out MavAeroCoefficients coefficients,
            out bool insideFitCondition,
            out bool insideTranscribedSpan,
            out float pHat,
            out float qHat,
            out float rHat,
            out string reason)
        {
            coefficients = MavAeroCoefficients.Zero;
            insideTranscribedSpan = false;
            pHat = 0f;
            qHat = 0f;
            rHat = 0f;

            string conditionReason;
            if (!MavF15ResearchConditionGate.Admits(ConditionMode, state, atmosphere, out insideFitCondition, out conditionReason))
            {
                reason = conditionReason;
                return false;
            }

            string spanReason;
            insideTranscribedSpan = MavF15BaumannMach06Domain.IsInsideTranscribedSpan(state.alphaRad, state.betaRad, out spanReason);
            if (!insideTranscribedSpan)
            {
                reason = spanReason;
                return false;
            }

            MavAeroReferenceGeometry geometry = MavF15BaumannMach06Reference.CreateReferenceGeometry();
            float speed = state.trueAirspeedMps;
            if (speed >= minimumRateNormalizationSpeedMps)
            {
                float halfInverseSpeed = 0.5f / speed;
                pHat = state.aeroBodyRatesRadSec.x * geometry.wingSpanM * halfInverseSpeed;
                qHat = state.aeroBodyRatesRadSec.y * geometry.meanAerodynamicChordM * halfInverseSpeed;
                rHat = state.aeroBodyRatesRadSec.z * geometry.wingSpanM * halfInverseSpeed;
            }

            MavF15BaumannSurfaceState surface = MavF15BaumannSurfaceState.FromPhysicalSurfaceState(surfaces);

            MavAeroCoefficients longitudinal = MavF15BaumannMach06Longitudinal.Evaluate(
                state.alphaRad, surface.symmetricStabilatorDeg, qHat);
            MavAeroCoefficients lateral = MavF15BaumannMach06LateralDirectional.Evaluate(
                state.alphaRad, state.betaRad, surface, pHat, rHat);

            coefficients = longitudinal;
            coefficients.cy = lateral.cy;
            coefficients.cl = lateral.cl;
            coefficients.cn = lateral.cn;

            if (!IsFinite(coefficients))
            {
                coefficients = MavAeroCoefficients.Zero;
                reason = "research coefficient evaluation produced a non-finite value";
                return false;
            }

            reason = conditionReason;
            return true;
        }

        private MavAeroCoefficients Refuse(string reason)
        {
            debugRefused = true;
            debugStatus = "REFUSED: " + reason;
            debugLastCoefficients = MavAeroCoefficients.Zero;
            return debugLastCoefficients;
        }

        private static bool IsFinite(MavAeroCoefficients c)
        {
            return Finite(c.cx) && Finite(c.cy) && Finite(c.cz) && Finite(c.cl) && Finite(c.cm) && Finite(c.cn);
        }

        private static bool Finite(float v)
        {
            return !float.IsNaN(v) && !float.IsInfinity(v);
        }
    }
}
