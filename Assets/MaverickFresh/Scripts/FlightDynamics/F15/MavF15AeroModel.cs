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

        [Header("F-15 Research Surface Adapter")]
        [Tooltip("The Appendix C routine carries differential tail as its own state. Until the F-15 control path exposes that channel, the default adapter uses the documented DTALD = 0.3 * DAILD relation. Enable only for research/debug injection.")]
        public bool useIndependentDifferentialTailResearchInput;

        [Tooltip("Research/debug differential-tail state in source degrees. Ignored unless the independent-input toggle is enabled.")]
        public float independentDifferentialTailResearchDeg;

        [Header("Numerics")]
        [Min(0.1f)]
        public float minimumRateNormalizationSpeedMps = 1f;

        [Header("Debug")]
        public bool debugRefused;
        public bool debugAtFixedSourceCondition;

        [Tooltip("True when alpha/beta are inside the span of breakpoints the transcribed routine itself declares. NOT a claimed F-15 validity envelope - see MavF15BaumannMach06Domain.")]
        public bool debugInsideTranscribedSpan;

        public bool debugLongitudinalOnly;
        public bool debugSixAxisResearch;
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
            debugInsideTranscribedSpan = false;
            debugLongitudinalOnly = false;
            debugSixAxisResearch = false;
            debugPHat = 0f;
            debugQHat = 0f;
            debugRHat = 0f;
            debugDifferentialTailUsedDeg = 0f;
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

            string conditionReason;
            debugAtFixedSourceCondition =
                MavF15BaumannMach06Reference.IsAtSourceCondition(
                    state,
                    atmosphere,
                    out conditionReason
                );

            if (!debugAtFixedSourceCondition)
                return Refuse(conditionReason);

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

            MavF15BaumannSurfaceState surface =
                MavF15BaumannSurfaceState.FromCommonInput(
                    input,
                    useIndependentDifferentialTailResearchInput,
                    independentDifferentialTailResearchDeg
                );
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
