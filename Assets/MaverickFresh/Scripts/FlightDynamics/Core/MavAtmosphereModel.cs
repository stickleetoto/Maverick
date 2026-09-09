using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>
    /// Lightweight ISA-style atmosphere for the flight-dynamics core.
    /// SI units only. This intentionally owns atmosphere state, not thrust or drag.
    /// Validated design range for the first Maverick FDM pass is approximately
    /// sea level through 20 km; the implementation remains continuous to 32 km.
    /// </summary>
    public static class MavAtmosphereModel
    {
        private const float SeaLevelTemperatureK = 288.15f;
        private const float SeaLevelPressurePa = 101325f;
        private const float GasConstantAir = 287.05287f;
        private const float GammaAir = 1.4f;
        private const float GravityMps2 = 9.80665f;

        private const float TropopauseAltitudeM = 11000f;
        private const float TropopauseTemperatureK = 216.65f;
        private const float TropopausePressurePa = 22632.06f;

        private const float TwentyKmPressurePa = 5474.889f;

        public static MavAtmosphereSample Sample(float geometricAltitudeM)
        {
            float altitudeM = Mathf.Clamp(geometricAltitudeM, -1000f, 32000f);
            float temperatureK;
            float pressurePa;

            if (altitudeM <= TropopauseAltitudeM)
            {
                const float lapseRate = -0.0065f;
                temperatureK = SeaLevelTemperatureK + lapseRate * altitudeM;

                float exponent = -GravityMps2 / (lapseRate * GasConstantAir);
                pressurePa = SeaLevelPressurePa * Mathf.Pow(
                    temperatureK / SeaLevelTemperatureK,
                    exponent
                );
            }
            else if (altitudeM <= 20000f)
            {
                temperatureK = TropopauseTemperatureK;
                pressurePa = TropopausePressurePa * Mathf.Exp(
                    -GravityMps2 * (altitudeM - TropopauseAltitudeM) /
                    (GasConstantAir * TropopauseTemperatureK)
                );
            }
            else
            {
                const float lapseRate = 0.001f;
                const float layerBaseAltitudeM = 20000f;
                const float layerBaseTemperatureK = 216.65f;

                temperatureK = layerBaseTemperatureK + lapseRate * (altitudeM - layerBaseAltitudeM);
                float exponent = -GravityMps2 / (lapseRate * GasConstantAir);
                pressurePa = TwentyKmPressurePa * Mathf.Pow(
                    temperatureK / layerBaseTemperatureK,
                    exponent
                );
            }

            temperatureK = Mathf.Max(1f, temperatureK);
            pressurePa = Mathf.Max(0f, pressurePa);

            float densityKgM3 = pressurePa / (GasConstantAir * temperatureK);
            float speedOfSoundMps = Mathf.Sqrt(GammaAir * GasConstantAir * temperatureK);

            MavAtmosphereSample sample = new MavAtmosphereSample();
            sample.altitudeM = geometricAltitudeM;
            sample.temperatureK = temperatureK;
            sample.pressurePa = pressurePa;
            sample.densityKgM3 = densityKgM3;
            sample.speedOfSoundMps = speedOfSoundMps;
            return sample;
        }
    }
}
