using System;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>
    /// One snapshot of the new flight-dynamics pipeline for a single physics step.
    /// SI units throughout; the angle fields report degrees for human readability and
    /// their names say so explicitly.
    /// </summary>
    [Serializable]
    public struct MavFlightDynamicsTelemetrySample
    {
        public float timeSeconds;
        public float altitudeM;
        public float trueAirspeedMps;
        public float mach;
        public float dynamicPressurePa;
        public float alphaDeg;
        public float betaDeg;

        public float rollRateDegSec;
        public float pitchRateDegSec;
        public float yawRateDegSec;

        public MavControlInput commandedSurfaces;
        public MavControlInput actualSurfaces;
        public MavAeroCoefficients coefficients;

        public Vector3 aeroForceAeroBodyN;
        public Vector3 aeroMomentAeroBodyNm;
        public Vector3 propulsionForceAeroBodyN;
        public Vector3 propulsionMomentAeroBodyNm;
        public Vector3 totalForceAeroBodyN;
        public Vector3 totalMomentAeroBodyNm;

        public bool profileValid;
        public bool insideEnvelope;
        public bool loadsApplied;
        public bool propulsionDataAuthoritative;
        public int aerodynamicContributions;
        public int propulsiveContributions;
    }

    /// <summary>
    /// Optional telemetry sink for the new flight-dynamics path.
    ///
    /// <see cref="MavSixDoFBody"/> pushes exactly one sample per physics step by calling
    /// <see cref="Capture"/>. Push, rather than a pull inside this component's own FixedUpdate,
    /// removes any execution-order dependency: the recorded sample is always the one the body
    /// just produced and never a mixture of two steps.
    ///
    /// Console logging is OFF by default and rate limited when enabled. CSV capture is OFF by
    /// default, buffers in memory, and writes to disk only when explicitly asked.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MavFlightDynamicsTelemetry : MonoBehaviour
    {
        /// <summary>Number of numeric CSV columns emitted before the integer/flag columns.</summary>
        public const int CsvNumericColumnCount = 33;

        /// <summary>Number of integer/flag CSV columns emitted after the numeric columns.</summary>
        public const int CsvFlagColumnCount = 6;

        [Header("Console Logging")]
        [Tooltip("OFF by default. Console output at physics rate is never acceptable.")]
        public bool logToConsole = false;

        [Tooltip("Minimum seconds between console lines. Values <= 0 are treated as 1 second.")]
        [Min(0f)] public float consoleLogIntervalSeconds = 1f;

        [Tooltip("When true, console lines are emitted only while loads are actually being applied.")]
        public bool logOnlyWhenLoadsApplied = true;

        [Header("CSV Capture")]
        [Tooltip("OFF by default. When enabled, samples buffer in memory; nothing reaches disk until WriteCsvToFile is called.")]
        public bool captureCsv = false;

        [Tooltip("Maximum buffered CSV rows, so a forgotten toggle cannot grow without bound.")]
        [Min(16)] public int maxCsvRows = 20000;

        [Header("Debug")]
        public MavFlightDynamicsTelemetrySample latest;
        public int debugSampleCount;
        public int debugCsvRowCount;
        public bool debugCsvBufferFull;

        private StringBuilder csvBuffer;
        private float nextConsoleLogTime = float.NegativeInfinity;

        /// <summary>
        /// Records one sample. Called by <see cref="MavSixDoFBody"/> at the end of its physics step.
        /// </summary>
        public void Capture(MavFlightDynamicsTelemetrySample sample)
        {
            latest = sample;
            debugSampleCount++;

            if (captureCsv)
                AppendCsvRow(sample);

            if (logToConsole)
                MaybeLogToConsole(sample);
        }

        /// <summary>Clears buffered CSV rows and the sample counters.</summary>
        public void ResetCapture()
        {
            csvBuffer = null;
            debugCsvRowCount = 0;
            debugSampleCount = 0;
            debugCsvBufferFull = false;
            nextConsoleLogTime = float.NegativeInfinity;
        }

        /// <summary>
        /// Writes the buffered CSV to <paramref name="absolutePath"/>, or into
        /// Application.persistentDataPath when the path is empty. Explicit call only: telemetry
        /// never writes to disk by itself. Returns the written path, or null when nothing was written.
        /// </summary>
        public string WriteCsvToFile(string absolutePath)
        {
            if (csvBuffer == null || debugCsvRowCount <= 0)
            {
                Debug.LogWarning("[Maverick/FDM/Telemetry] No CSV rows buffered; nothing written.", this);
                return null;
            }

            string path = string.IsNullOrWhiteSpace(absolutePath)
                ? System.IO.Path.Combine(
                    Application.persistentDataPath,
                    "MavFdmTelemetry_"
                    + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)
                    + ".csv")
                : absolutePath;

            try
            {
                System.IO.File.WriteAllText(path, CsvHeader() + Environment.NewLine + csvBuffer.ToString());
                Debug.Log(
                    "[Maverick/FDM/Telemetry] Wrote " + debugCsvRowCount + " rows to " + path,
                    this
                );
                return path;
            }
            catch (Exception e)
            {
                Debug.LogError("[Maverick/FDM/Telemetry] CSV write failed: " + e.Message, this);
                return null;
            }
        }

        private void MaybeLogToConsole(MavFlightDynamicsTelemetrySample s)
        {
            if (logOnlyWhenLoadsApplied && !s.loadsApplied)
                return;

            float interval = consoleLogIntervalSeconds > 0f ? consoleLogIntervalSeconds : 1f;
            if (s.timeSeconds < nextConsoleLogTime)
                return;

            nextConsoleLogTime = s.timeSeconds + interval;

            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "[Maverick/FDM] t={0:F2}s alt={1:F0}m TAS={2:F1}m/s M={3:F3} qbar={4:F0}Pa"
                + " alpha={5:F2}deg beta={6:F2}deg pqr=({7:F1},{8:F1},{9:F1})deg/s"
                + " actual=(elev {10:F2}, ail {11:F2}, rud {12:F2})deg thr={13:F2}"
                + " totalF=({14:F0},{15:F0},{16:F0})N totalM=({17:F0},{18:F0},{19:F0})Nm"
                + " aeroContrib={20} propContrib={21} applied={22} profileValid={23}"
                + " insideEnvelope={24} propulsionSourced={25}",
                s.timeSeconds, s.altitudeM, s.trueAirspeedMps, s.mach, s.dynamicPressurePa,
                s.alphaDeg, s.betaDeg,
                s.rollRateDegSec, s.pitchRateDegSec, s.yawRateDegSec,
                s.actualSurfaces.elevatorDeg, s.actualSurfaces.aileronDeg,
                s.actualSurfaces.rudderDeg, s.actualSurfaces.throttle01,
                s.totalForceAeroBodyN.x, s.totalForceAeroBodyN.y, s.totalForceAeroBodyN.z,
                s.totalMomentAeroBodyNm.x, s.totalMomentAeroBodyNm.y, s.totalMomentAeroBodyNm.z,
                s.aerodynamicContributions, s.propulsiveContributions,
                s.loadsApplied, s.profileValid, s.insideEnvelope, s.propulsionDataAuthoritative
            ), this);
        }

        private void AppendCsvRow(MavFlightDynamicsTelemetrySample s)
        {
            int cap = Mathf.Max(16, maxCsvRows);
            if (debugCsvRowCount >= cap)
            {
                if (!debugCsvBufferFull)
                {
                    debugCsvBufferFull = true;
                    Debug.LogWarning(
                        "[Maverick/FDM/Telemetry] CSV row cap reached (" + cap + "); capture stopped.",
                        this
                    );
                }
                return;
            }

            if (csvBuffer == null)
                csvBuffer = new StringBuilder(65536);

            AppendNumeric(csvBuffer, s);
            AppendFlags(csvBuffer, s);
            csvBuffer.Append(Environment.NewLine);
            debugCsvRowCount++;
        }

        private static void AppendNumeric(StringBuilder sb, MavFlightDynamicsTelemetrySample s)
        {
            // Keep this list in the same order as CsvHeader(); the count is asserted by validation.
            float[] numeric =
            {
                s.timeSeconds, s.altitudeM, s.trueAirspeedMps, s.mach, s.dynamicPressurePa,
                s.alphaDeg, s.betaDeg,
                s.rollRateDegSec, s.pitchRateDegSec, s.yawRateDegSec,
                s.commandedSurfaces.elevatorDeg, s.commandedSurfaces.aileronDeg,
                s.commandedSurfaces.rudderDeg,
                s.actualSurfaces.elevatorDeg, s.actualSurfaces.aileronDeg,
                s.actualSurfaces.rudderDeg, s.actualSurfaces.throttle01,
                s.coefficients.cx, s.coefficients.cy, s.coefficients.cz,
                s.coefficients.cl, s.coefficients.cm, s.coefficients.cn,
                s.aeroForceAeroBodyN.x, s.aeroForceAeroBodyN.y, s.aeroForceAeroBodyN.z,
                s.aeroMomentAeroBodyNm.x, s.aeroMomentAeroBodyNm.y, s.aeroMomentAeroBodyNm.z,
                s.propulsionForceAeroBodyN.x,
                s.totalForceAeroBodyN.x, s.totalForceAeroBodyN.y, s.totalForceAeroBodyN.z
            };

            for (int i = 0; i < numeric.Length; i++)
            {
                if (i > 0)
                    sb.Append(',');
                sb.Append(numeric[i].ToString("R", CultureInfo.InvariantCulture));
            }
        }

        private static void AppendFlags(StringBuilder sb, MavFlightDynamicsTelemetrySample s)
        {
            sb.Append(',').Append(s.profileValid ? 1 : 0);
            sb.Append(',').Append(s.insideEnvelope ? 1 : 0);
            sb.Append(',').Append(s.loadsApplied ? 1 : 0);
            sb.Append(',').Append(s.propulsionDataAuthoritative ? 1 : 0);
            sb.Append(',').Append(s.aerodynamicContributions);
            sb.Append(',').Append(s.propulsiveContributions);
        }

        /// <summary>
        /// CSV column header. Column count is asserted by validation so a header/row mismatch
        /// cannot silently corrupt an analysis run.
        /// </summary>
        public static string CsvHeader()
        {
            return "t_s,alt_m,tas_mps,mach,qbar_pa,alpha_deg,beta_deg"
                 + ",p_degs,q_degs,r_degs"
                 + ",cmd_elev_deg,cmd_ail_deg,cmd_rud_deg"
                 + ",act_elev_deg,act_ail_deg,act_rud_deg,act_throttle01"
                 + ",CX,CY,CZ,Cl,Cm,Cn"
                 + ",aeroFx_n,aeroFy_n,aeroFz_n,aeroL_nm,aeroM_nm,aeroN_nm"
                 + ",propFx_n"
                 + ",totFx_n,totFy_n,totFz_n"
                 + ",profileValid,insideEnvelope,loadsApplied,propulsionSourced"
                 + ",aeroContributions,propContributions";
        }
    }
}
