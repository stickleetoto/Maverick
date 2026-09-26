using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    public enum MavF15AeroSourceMode
    {
        /// <summary>
        /// The exact NASA 836 coefficient database has not been recovered. Returning
        /// zero is safer than silently substituting another F-15.
        /// </summary>
        ExactNasa836Unavailable = 0,

        /// <summary>
        /// AFIT/Baumann fixed-condition research fit. Cross-validation only.
        /// Longitudinal CX/CZ/Cm slice only.
        /// </summary>
        BaumannMach06LongitudinalResearch = 1,

        /// <summary>
        /// AFIT/Baumann fixed-condition research fit with the transcribed
        /// lateral-directional CY/Cl/Cn channels added.
        /// </summary>
        BaumannMach06SixAxisResearch = 2
    }

    /// <summary>
    /// Unity-facing F-15 aerodynamic-model boundary.
    ///
    /// Default behavior is intentionally zero coefficients because the exact NASA 836
    /// baseline coefficient database is still unavailable.
    ///
    /// Separately tagged AFIT/Baumann research modes can be enabled explicitly for
    /// coefficient work. They are hard-gated to the single published Mach/altitude
    /// condition and must never be presented as the NASA 836 target model.
    ///
    /// Like the F-16 bridge, this component returns coefficients only. MavSixDoFBody
    /// remains the one force/moment application owner.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MavF15AeroModel : MavAerodynamicModelBase
    {
        [Header("Source Selection")]
        public MavF15AeroSourceMode sourceMode =
            MavF15AeroSourceMode.ExactNasa836Unavailable;

        [Tooltip("Explicit opt-in required because the Baumann model is CROSS-VALIDATION only, not the exact NASA 836 target.")]
        public bool allowCrossValidationResearchModel;

        [Header("F-15 Surface State Source")]
        [Tooltip("The actuator that owns actual F-15 surface positions, including differential stabilator. Resolved from this GameObject when left empty. When one is bound it is ALWAYS preferred over the research adapter below.")]
        public MavF15ControlActuator surfaceOwner;

        [Header("F-15 Research Surface Adapter (fallback only)")]
        [Tooltip("Used ONLY when no actuator is bound. The Appendix C routine carries differential tail as its own state; with no owner for that channel the adapter falls back to the documented DTALD = 0.3 * DAILD research relation. Enable this to inject a differential tail for research/debug instead.")]
        public bool useIndependentDifferentialTailResearchInput;

        [Tooltip("Research/debug differential-tail state in source degrees. Ignored unless the independent-input toggle is enabled.")]
        public float independentDifferentialTailResearchDeg;

        [Header("Numerics")]
        [Min(0.1f)]
        public float minimumRateNormalizationSpeedMps = 1f;

        [Header("Debug")]
        public bool debugRefused;

        [Tooltip("True when the state is AT the Mach 0.6 / 20,000 ft coefficient-fit condition.")]
        public bool debugAtFixedSourceCondition;

        [Tooltip("The research condition mode in force. SourceReproduction is honoured only through a research profile that is this body's provider; anything else is strict.")]
        public MavF15ResearchConditionMode debugConditionMode = MavF15ResearchConditionMode.StrictFitCondition;

        [Tooltip("True when the coefficients just published were evaluated AWAY from the Mach 0.6 fit condition (SourceReproduction only): source-exercised, not aerodynamically validated.")]
        public bool debugExtrapolatedFromFitCondition;

        [Tooltip("True when alpha/beta are inside the span of breakpoints the transcribed routine itself declares. NOT a claimed F-15 validity envelope - see MavF15BaumannMach06Domain.")]
        public bool debugInsideTranscribedSpan;

        public bool debugLongitudinalOnly;
        public bool debugSixAxisResearch;

        [Tooltip("Where the surface positions fed to the aerodynamic routine came from: the actuator that owns them, or the research fallback adapter.")]
        public string debugSurfaceSource = "not evaluated";

        public string debugStatus = "not evaluated";
        public float debugPHat;
        public float debugQHat;
        public float debugRHat;
        public float debugDifferentialTailUsedDeg;
        public MavAeroCoefficients debugLastCoefficients;

        private void Reset()
        {
            sourceMode = MavF15AeroSourceMode.ExactNasa836Unavailable;
            allowCrossValidationResearchModel = false;
            useIndependentDifferentialTailResearchInput = false;
            independentDifferentialTailResearchDeg = 0f;
            referenceGeometry = new MavAeroReferenceGeometry();
        }

        private void OnValidate()
        {
            minimumRateNormalizationSpeedMps =
                Mathf.Max(0.1f, minimumRateNormalizationSpeedMps);
        }

        public override MavAeroCoefficients Evaluate(
            MavFlightState state,
            MavControlInput input,
            MavAtmosphereSample atmosphere)
        {
            debugRefused = false;
            debugAtFixedSourceCondition = false;
            debugExtrapolatedFromFitCondition = false;
            debugConditionMode = MavF15ResearchConditionMode.StrictFitCondition;
            debugInsideTranscribedSpan = false;
            debugLongitudinalOnly = false;
            debugSixAxisResearch = false;
            debugPHat = 0f;
            debugQHat = 0f;
            debugRHat = 0f;
            debugDifferentialTailUsedDeg = 0f;
            debugSurfaceSource = "not evaluated";
            debugLastCoefficients = MavAeroCoefficients.Zero;

            if (sourceMode == MavF15AeroSourceMode.ExactNasa836Unavailable)
            {
                return Refuse(
                    "exact NASA 836 baseline coefficient database is unavailable; "
                    + "no substitute selected"
                );
            }

            if (!allowCrossValidationResearchModel)
            {
                return Refuse(
                    "Baumann research model selected but explicit CROSS-VALIDATION opt-in is false"
                );
            }

            // Strict unless a research profile that this body actually flies asks for source
            // reproduction. See MavF15BaumannSourceSemantics.
            debugConditionMode = ResolveConditionMode();

            string conditionReason;
            bool insideFitCondition;
            bool admitted = MavF15ResearchConditionGate.Admits(
                debugConditionMode,
                state,
                atmosphere,
                out insideFitCondition,
                out conditionReason
            );

            debugAtFixedSourceCondition = admitted && insideFitCondition;
            if (!admitted)
                return Refuse(conditionReason);

            debugExtrapolatedFromFitCondition = !insideFitCondition;

            // F15-AUDIT-002: Mach and altitude were already fail-closed, alpha and beta were not.
            // The research fits are 6th- to 9th-order polynomials and diverge outside the span
            // the transcription itself declares - Cm reaches -730 at alpha=180 deg, which the
            // finiteness check below cannot see. Refuse instead of publishing it.
            string spanReason;
            debugInsideTranscribedSpan =
                MavF15BaumannMach06Domain.IsInsideTranscribedSpan(
                    state.alphaRad,
                    state.betaRad,
                    out spanReason
                );

            if (!debugInsideTranscribedSpan)
                return Refuse(spanReason);

            // The research fit owns its own reference geometry. It is deliberately
            // separate from the exact NASA 836 profile, whose S/cbar remain unresolved.
            referenceGeometry =
                MavF15BaumannMach06Reference.CreateReferenceGeometry();

            float speed = state.trueAirspeedMps;
            if (speed >= minimumRateNormalizationSpeedMps)
            {
                float halfInverseSpeed = 0.5f / speed;
                debugPHat =
                    state.aeroBodyRatesRadSec.x
                    * referenceGeometry.wingSpanM
                    * halfInverseSpeed;
                debugQHat =
                    state.aeroBodyRatesRadSec.y
                    * referenceGeometry.meanAerodynamicChordM
                    * halfInverseSpeed;
                debugRHat =
                    state.aeroBodyRatesRadSec.z
                    * referenceGeometry.wingSpanM
                    * halfInverseSpeed;
            }

            MavF15BaumannSurfaceState surface = ResolveSurfaceState(input);
            debugDifferentialTailUsedDeg = surface.differentialTailDeg;

            MavAeroCoefficients longitudinal =
                MavF15BaumannMach06Longitudinal.Evaluate(
                    state.alphaRad,
                    surface.symmetricStabilatorDeg,
                    debugQHat
                );

            if (sourceMode == MavF15AeroSourceMode.BaumannMach06LongitudinalResearch)
            {
                debugLongitudinalOnly = true;
                debugLastCoefficients = longitudinal;
                return ValidateAndPublish(
                    conditionReason,
                    "LONGITUDINAL ONLY"
                );
            }

            MavAeroCoefficients lateral =
                MavF15BaumannMach06LateralDirectional.Evaluate(
                    state.alphaRad,
                    state.betaRad,
                    surface,
                    debugPHat,
                    debugRHat
                );

            debugSixAxisResearch = true;
            debugLastCoefficients = longitudinal;
            debugLastCoefficients.cy = lateral.cy;
            debugLastCoefficients.cl = lateral.cl;
            debugLastCoefficients.cn = lateral.cn;

            return ValidateAndPublish(
                conditionReason,
                "SIX-AXIS RESEARCH"
            );
        }

        /// <summary>
        /// Where the surface positions fed to the research routine come from.
        ///
        /// The actuator is preferred whenever one is bound, because it is the declared owner of
        /// actual surface state and it carries a real differential-stabilator channel. Only when
        /// no actuator is present does this fall back to expanding the shared input through the
        /// AFIT research relation DTALD = 0.3 * DAILD, and the fallback says so in the status
        /// string so a research bridge is never mistaken for a measured surface position.
        /// </summary>
        private MavF15BaumannSurfaceState ResolveSurfaceState(MavControlInput input)
        {
            ResolveActuator();

            if (surfaceOwner != null)
            {
                debugSurfaceSource = "actuator (owns differential stabilator)";
                return MavF15BaumannSurfaceState.FromPhysicalSurfaceState(
                    surfaceOwner.ActualF15SurfaceState
                );
            }

            debugSurfaceSource =
                useIndependentDifferentialTailResearchInput
                    ? "no actuator bound; shared input + injected research differential tail"
                    : "no actuator bound; shared input + AFIT research relation DTALD = 0.3*DAILD";

            return MavF15BaumannSurfaceState.FromCommonInput(
                input,
                useIndependentDifferentialTailResearchInput,
                independentDifferentialTailResearchDeg
            );
        }

        /// <summary>
        /// SourceReproduction takes effect only when a research profile on this GameObject asks for
        /// it AND is the provider of the six-DoF body here. The exact NASA 836 profile, a missing
        /// body, or a research profile the body does not fly all leave the gate strict.
        /// </summary>
        private MavF15ResearchConditionMode ResolveConditionMode()
        {
            MavF15AfitResearchFlightDynamicsProfile research =
                GetComponent<MavF15AfitResearchFlightDynamicsProfile>();
            if (research == null
                || research.conditionMode != MavF15ResearchConditionMode.SourceReproduction)
                return MavF15ResearchConditionMode.StrictFitCondition;

            MavSixDoFBody body = GetComponent<MavSixDoFBody>();
            if (body == null || body.profileProvider != research)
                return MavF15ResearchConditionMode.StrictFitCondition;

            return MavF15ResearchConditionMode.SourceReproduction;
        }

        private void ResolveActuator()
        {
            if (surfaceOwner == null)
                surfaceOwner = GetComponent<MavF15ControlActuator>();
        }

        private MavAeroCoefficients ValidateAndPublish(
            string conditionReason,
            string modeLabel)
        {
            if (!IsFinite(debugLastCoefficients))
            {
                return Refuse(
                    "research coefficient evaluation produced a non-finite value"
                );
            }

            debugStatus =
                MavF15BaumannMach06Reference.ModelId
                + " / "
                + modeLabel
                + " / "
                + conditionReason;
            return debugLastCoefficients;
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
            return IsFinite(c.cx)
                && IsFinite(c.cy)
                && IsFinite(c.cz)
                && IsFinite(c.cl)
                && IsFinite(c.cm)
                && IsFinite(c.cn);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
