using UnityEngine;

namespace MaverickFresh.FlightDynamics.F16
{
    /// <summary>
    /// Unity-facing F-16 aerodynamic model using the compact Morelli nonlinear
    /// polynomial representation. This component returns coefficients only;
    /// MavSixDoFBody owns dimensionalization and Rigidbody force/moment application.
    /// </summary>
    [DisallowMultipleComponent]
    public class MavF16AeroModel : MavAerodynamicModelBase
    {
        [Header("Morelli Reference Configuration")]
        [Range(0.15f, 0.45f)] public float xCgCbar = MavF16MorelliReference.DefaultXcgCbar;
        [Range(0.15f, 0.45f)] public float xCgReferenceCbar = MavF16MorelliReference.XcgReferenceCbar;
        public bool clampToPublishedInputEnvelope = true;
        public float minimumRateNormalizationSpeedMps = 1f;

        [Header("Validity / Debug")]
        public bool debugOutsidePublishedMachRange;
        public bool debugInputWasClamped;
        public float debugAlphaUsedDeg;
        public float debugBetaUsedDeg;
        public float debugElevatorUsedDeg;
        public float debugAileronUsedDeg;
        public float debugRudderUsedDeg;
        public float debugPHat;
        public float debugQHat;
        public float debugRHat;
        public MavAeroCoefficients debugLastCoefficients;

        private void Reset()
        {
            referenceGeometry = MavF16MorelliReference.CreateReferenceGeometry();
            xCgCbar = MavF16MorelliReference.DefaultXcgCbar;
            xCgReferenceCbar = MavF16MorelliReference.XcgReferenceCbar;
        }

        private void Awake()
        {
            EnsureReferenceGeometry();
        }

        private void OnValidate()
        {
            EnsureReferenceGeometry();
            minimumRateNormalizationSpeedMps = Mathf.Max(0.1f, minimumRateNormalizationSpeedMps);
        }

        public override MavAeroCoefficients Evaluate(
            MavFlightState state,
            MavControlInput input,
            MavAtmosphereSample atmosphere)
        {
            EnsureReferenceGeometry();

            float alpha = state.alphaRad;
            float beta = state.betaRad;
            float elevator = input.elevatorDeg * Mathf.Deg2Rad;
            float aileron = input.aileronDeg * Mathf.Deg2Rad;
            float rudder = input.rudderDeg * Mathf.Deg2Rad;

            debugInputWasClamped = false;
            if (clampToPublishedInputEnvelope)
            {
                float rawAlpha = alpha;
                float rawBeta = beta;
                float rawElevator = elevator;
                float rawAileron = aileron;
                float rawRudder = rudder;

                alpha = MavF16MorelliReference.ClampAlphaRad(alpha);
                beta = MavF16MorelliReference.ClampBetaRad(beta);
                elevator = MavF16MorelliReference.ClampElevatorRad(elevator);
                aileron = MavF16MorelliReference.ClampAileronRad(aileron);
                rudder = MavF16MorelliReference.ClampRudderRad(rudder);

                debugInputWasClamped =
                    !Mathf.Approximately(rawAlpha, alpha)
                    || !Mathf.Approximately(rawBeta, beta)
                    || !Mathf.Approximately(rawElevator, elevator)
                    || !Mathf.Approximately(rawAileron, aileron)
                    || !Mathf.Approximately(rawRudder, rudder);
            }

            float speed = state.trueAirspeedMps;
            if (speed >= Mathf.Max(0.1f, minimumRateNormalizationSpeedMps))
            {
                float halfInverseSpeed = 0.5f / speed;
                debugPHat = state.aeroBodyRatesRadSec.x * referenceGeometry.wingSpanM * halfInverseSpeed;
                debugQHat = state.aeroBodyRatesRadSec.y * referenceGeometry.meanAerodynamicChordM * halfInverseSpeed;
                debugRHat = state.aeroBodyRatesRadSec.z * referenceGeometry.wingSpanM * halfInverseSpeed;
            }
            else
            {
                debugPHat = 0f;
                debugQHat = 0f;
                debugRHat = 0f;
            }

            debugOutsidePublishedMachRange = state.mach >= MavF16MorelliReference.MaxReferenceMach;
            debugAlphaUsedDeg = alpha * Mathf.Rad2Deg;
            debugBetaUsedDeg = beta * Mathf.Rad2Deg;
            debugElevatorUsedDeg = elevator * Mathf.Rad2Deg;
            debugAileronUsedDeg = aileron * Mathf.Rad2Deg;
            debugRudderUsedDeg = rudder * Mathf.Rad2Deg;

            float chordOverSpan = referenceGeometry.meanAerodynamicChordM
                                  / Mathf.Max(0.01f, referenceGeometry.wingSpanM);

            debugLastCoefficients = MavF16MorelliPolynomial.Evaluate(
                alpha,
                beta,
                elevator,
                aileron,
                rudder,
                debugPHat,
                debugQHat,
                debugRHat,
                xCgCbar,
                xCgReferenceCbar,
                chordOverSpan
            );

            return debugLastCoefficients;
        }

        private void EnsureReferenceGeometry()
        {
            if (referenceGeometry.wingAreaM2 <= 0.01f)
                referenceGeometry.wingAreaM2 = MavF16MorelliReference.WingAreaM2;
            if (referenceGeometry.wingSpanM <= 0.01f)
                referenceGeometry.wingSpanM = MavF16MorelliReference.WingSpanM;
            if (referenceGeometry.meanAerodynamicChordM <= 0.01f)
                referenceGeometry.meanAerodynamicChordM = MavF16MorelliReference.MeanAerodynamicChordM;
        }
    }
}
