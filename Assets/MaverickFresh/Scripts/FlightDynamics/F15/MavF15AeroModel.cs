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
        /// Currently implements the longitudinal coefficient subset.
        /// </summary>
        BaumannMach06LongitudinalResearch = 1
    }

    /// <summary>
    /// Unity-facing F-15 aerodynamic-model boundary.
    ///
    /// Default behavior is intentionally zero coefficients because the exact NASA 836
    /// baseline coefficient database is still unavailable.
    ///
    /// A separately tagged AFIT/Baumann research model can be enabled explicitly for
    /// coefficient work. It is hard-gated to its single published Mach/altitude
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

        [Header("Numerics")]
        [Min(0.1f)]
        public float minimumRateNormalizationSpeedMps = 1f;

        [Header("Debug")]
        public bool debugRefused;
        public bool debugAtFixedSourceCondition;
        public bool debugLongitudinalOnly;
        public string debugStatus = "not evaluated";
        public float debugQHat;
        public MavAeroCoefficients debugLastCoefficients;

        private void Reset()
        {
            sourceMode = MavF15AeroSourceMode.ExactNasa836Unavailable;
            allowCrossValidationResearchModel = false;
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
            debugLongitudinalOnly = false;
            debugQHat = 0f;
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

            // The research fit owns its own reference geometry. It is deliberately
            // separate from the exact NASA 836 profile, whose S/cbar remain unresolved.
            referenceGeometry =
                MavF15BaumannMach06Reference.CreateReferenceGeometry();

            float speed = state.trueAirspeedMps;
            if (speed >= minimumRateNormalizationSpeedMps)
            {
                debugQHat =
                    state.aeroBodyRatesRadSec.y
                    * referenceGeometry.meanAerodynamicChordM
                    / (2f * speed);
            }

            debugLongitudinalOnly = true;
            debugLastCoefficients =
                MavF15BaumannMach06Longitudinal.Evaluate(
                    state.alphaRad,
                    input.elevatorDeg,
                    debugQHat
                );

            if (!IsFinite(debugLastCoefficients))
            {
                return Refuse(
                    "research coefficient evaluation produced a non-finite value"
                );
            }

            debugStatus =
                MavF15BaumannMach06Reference.ModelId
                + " / LONGITUDINAL ONLY / "
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
