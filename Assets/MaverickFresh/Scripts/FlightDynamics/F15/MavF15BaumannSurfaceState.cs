using System;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// F-15-specific control-surface state consumed by the AFIT/Baumann research
    /// aerodynamic model.
    ///
    /// The common Maverick MavControlInput intentionally does not gain an F-15-only
    /// differential-tail field. Instead, the aircraft-specific bridge expands the
    /// common input into this state.
    ///
    /// The Davison/Beck coefficient routine treats differential tail as an independent
    /// state. Earlier/source comments also document the conventional research-model
    /// relation DTALD = 0.3 * DAILD. Until the F-15 control path owns an explicit
    /// differential-tail channel, that sourced relation is the default adapter.
    /// </summary>
    [Serializable]
    public struct MavF15BaumannSurfaceState
    {
        public const float SourceDifferentialTailPerAileron = 0.3f;

        public float symmetricStabilatorDeg;
        public float aileronDeg;
        public float differentialTailDeg;
        public float rudderDeg;

        public static MavF15BaumannSurfaceState FromCommonInput(
            MavControlInput input,
            bool useIndependentDifferentialTail,
            float independentDifferentialTailDeg)
        {
            MavF15BaumannSurfaceState state = new MavF15BaumannSurfaceState();
            state.symmetricStabilatorDeg = input.elevatorDeg;
            state.aileronDeg = input.aileronDeg;
            state.differentialTailDeg = useIndependentDifferentialTail
                ? independentDifferentialTailDeg
                : SourceDifferentialTailPerAileron * input.aileronDeg;
            state.rudderDeg = input.rudderDeg;
            return state;
        }
    }
}
