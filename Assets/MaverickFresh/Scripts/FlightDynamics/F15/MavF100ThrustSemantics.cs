using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// A thrust value that knows WHICH thrust it is.
    ///
    /// The R5 sources split cleanly on this: TP-1373 and TP-1782 are gross-thrust documents
    /// throughout, while TM X-3261 and TP-1034 compute gross and net and print the step between.
    /// A gross number used as net is wrong by the ram drag, which at Mach 2 is not a rounding
    /// error - it is comparable to the thrust itself.
    ///
    /// So dimensional thrust does not travel through this layer as a bare float.
    /// </summary>
    public struct MavF100ThrustValue
    {
        public bool valid;
        public float newtons;
        public MavF100ThrustQuantity quantity;
        public MavF100SourceClass sourceClass;
        public string reason;

        public static MavF100ThrustValue Invalid(string reason)
        {
            MavF100ThrustValue v = new MavF100ThrustValue();
            v.valid = false;
            v.newtons = 0f;
            v.quantity = MavF100ThrustQuantity.Unspecified;
            v.sourceClass = MavF100SourceClass.Unavailable;
            v.reason = reason;
            return v;
        }

        public static MavF100ThrustValue Of(
            float newtons,
            MavF100ThrustQuantity quantity,
            MavF100SourceClass sourceClass,
            string reason)
        {
            MavF100ThrustValue v = new MavF100ThrustValue();
            v.valid = true;
            v.newtons = newtons;
            v.quantity = quantity;
            v.sourceClass = sourceClass;
            v.reason = reason;
            return v;
        }
    }

    /// <summary>
    /// The gross/net thrust transformation, and nothing else.
    ///
    /// This is the one dimensional thrust calculation in the R5 pack that can be implemented
    /// outright. NASA TM X-3261 equation (B52) and NASA TP-1034 equation (B56) both print:
    ///
    ///     F_net = F_gross - 20.041 * w2 * M0 * sqrt(T0)
    ///
    /// in newtons, kg/sec and kelvin. The coefficient is sqrt(gamma*R) for air to four figures,
    /// so the subtracted term is just the inlet momentum flux w2*V0 with the speed of sound
    /// written out. That identity is what makes this safe to implement while the gross thrust
    /// equation printed immediately above it is not: that one needs the fan, compressor and
    /// turbine maps, which both reports publish as graphs and read from data cards.
    ///
    /// Both of these are UNINSTALLED. Neither report applies inlet spillage, bleed or
    /// nozzle/boattail interference, and nothing in the R5 pack closes those for NASA 836. There
    /// is therefore no path here from uninstalled net thrust to installed net thrust, and adding
    /// one would be invention.
    /// </summary>
    public static class MavF100ThrustSemantics
    {
        /// <summary>
        /// Inlet ram drag, w2 * V0, in newtons.
        ///
        /// Positive magnitude - it is SUBTRACTED from gross thrust, never added. Returns invalid
        /// rather than zero for a non-finite or negative input, because a silent zero here reads
        /// downstream as "no ram drag at this condition", which is a physical claim.
        /// </summary>
        public static MavF100ThrustValue RamDragN(
            float engineAirflowKgPerSec, float mach, float freeStreamTotalTemperatureK)
        {
            if (!IsFinite(engineAirflowKgPerSec) || !IsFinite(mach)
                || !IsFinite(freeStreamTotalTemperatureK))
            {
                return MavF100ThrustValue.Invalid("non-finite input to ram drag");
            }

            if (engineAirflowKgPerSec < 0f)
                return MavF100ThrustValue.Invalid("negative engine airflow");

            if (freeStreamTotalTemperatureK <= 0f)
                return MavF100ThrustValue.Invalid("non-positive free-stream temperature");

            if (mach < 0f)
                return MavF100ThrustValue.Invalid("negative Mach number");

            float drag = MavF100SourceData.RamDragCoefficient
                * engineAirflowKgPerSec
                * mach
                * Mathf.Sqrt(freeStreamTotalTemperatureK);

            return MavF100ThrustValue.Of(
                drag,
                MavF100ThrustQuantity.RamDrag,
                MavF100SourceClass.CompatibleSupport,
                MavF100SourceData.RamDragCitation);
        }

        /// <summary>
        /// Uninstalled net thrust from uninstalled gross thrust, per (B52)/(B56).
        ///
        /// Refuses a gross value that is not actually labelled gross. That check is the entire
        /// reason <see cref="MavF100ThrustValue"/> exists: handing this method a net thrust would
        /// otherwise subtract the ram drag a second time and produce a confidently wrong number.
        /// </summary>
        public static MavF100ThrustValue NetFromGross(
            MavF100ThrustValue gross,
            float engineAirflowKgPerSec,
            float mach,
            float freeStreamTotalTemperatureK)
        {
            if (!gross.valid)
                return MavF100ThrustValue.Invalid("gross thrust input is invalid: " + gross.reason);

            if (gross.quantity != MavF100ThrustQuantity.GrossThrust)
            {
                return MavF100ThrustValue.Invalid(
                    "refusing to apply ram drag to a value labelled " + gross.quantity
                    + "; (B52)/(B56) transform GROSS thrust only");
            }

            MavF100ThrustValue drag = RamDragN(
                engineAirflowKgPerSec, mach, freeStreamTotalTemperatureK);

            if (!drag.valid)
                return MavF100ThrustValue.Invalid("ram drag unavailable: " + drag.reason);

            // The weaker of the two inputs governs: a gross thrust that was only cross-validation
            // grade cannot become compatible-support merely by passing through this equation.
            MavF100SourceClass worst = gross.sourceClass < drag.sourceClass
                ? gross.sourceClass
                : drag.sourceClass;

            return MavF100ThrustValue.Of(
                gross.newtons - drag.newtons,
                MavF100ThrustQuantity.UninstalledNetThrust,
                worst,
                "F_net = F_gross - 20.041*w2*M0*sqrt(T0); " + MavF100SourceData.RamDragCitation);
        }

        private static bool IsFinite(float v)
        {
            return !float.IsNaN(v) && !float.IsInfinity(v);
        }
    }

    /// <summary>
    /// Inlet total-pressure recovery and fan-face conditions, NASA TM X-3261 equations (B1)-(B5).
    ///
    /// The source calls this "a steady-state representation of a TYPICAL inlet recovery". It is
    /// the standard reference schedule, not a measurement of any particular inlet, and certainly
    /// not of the F-15's variable-geometry inlet or of NASA 836's. It is implemented because it is
    /// printed and unambiguous, and it is labelled honestly so that nobody later mistakes it for
    /// F-15 inlet data.
    ///
    /// INLET DISTORTION is a separate matter and remains unavailable. TP-1373 tested two
    /// distortion screens and concluded (printed p. 19) that the effects were "small and
    /// uncorrelatable", explicitly declining to derive a correction. Turning that discussion into
    /// a multiplier would be manufacturing the very number the source refused to state.
    /// </summary>
    public static class MavF100InletRecovery
    {
        /// <summary>
        /// Inlet total-pressure recovery eta, equation (B3). Unity below Mach 1, falling as
        /// 1 - 0.075*(M0-1)^1.35 above it.
        /// </summary>
        public static float TotalPressureRecovery(float mach)
        {
            if (float.IsNaN(mach) || float.IsInfinity(mach) || mach <= 1f)
                return 1f;

            return 1f - MavF100SourceData.SupersonicRecoveryCoefficient
                * Mathf.Pow(mach - 1f, MavF100SourceData.SupersonicRecoveryExponent);
        }

        /// <summary>
        /// Fan-face total temperature ratio T2/T0, equation (B4) as implemented in the appendix C
        /// FORTRAN: 1 + 0.2*M0^2, i.e. isentropic stagnation with gamma = 1.4.
        /// </summary>
        public static float FanFaceTotalTemperatureRatio(float mach)
        {
            if (float.IsNaN(mach) || float.IsInfinity(mach))
                return 1f;

            return 1f + 0.2f * mach * mach;
        }

        /// <summary>
        /// Fan-face total pressure ratio P2/P0, equation (B5): the isentropic stagnation ratio
        /// (1 + 0.2*M0^2)^3.5 multiplied by the recovery of <see cref="TotalPressureRecovery"/>.
        /// </summary>
        public static float FanFaceTotalPressureRatio(float mach)
        {
            float tr = FanFaceTotalTemperatureRatio(mach);
            return Mathf.Pow(tr, 3.5f) * TotalPressureRecovery(mach);
        }
    }

    /// <summary>
    /// Engine control schedules that the R5 sources state in full, in prose, with both endpoints
    /// and the law between them.
    ///
    /// There are only two, and neither is a gain. Everything else about the F100's control - the
    /// fuel schedules, the nozzle area schedule, the augmentor zone sequencing, the EEC trim laws
    /// - is described qualitatively and its numbers withheld.
    ///
    /// These are PROTOTYPE SERIES 2 7/8 control schedules. TP-1373 printed p. 4 states outright
    /// that they differ from both series 2 and series 3 production engines, so they are
    /// compatible support and never exact-target.
    /// </summary>
    public static class MavF100EngineControlSchedules
    {
        /// <summary>
        /// Whether the nozzle divergent section is on its high-mode area-ratio schedule.
        /// TP-1373 printed p. 7: low mode below Mach 1.1, high mode above.
        /// </summary>
        public static bool IsHighModeNozzleSchedule(float mach)
        {
            return mach > MavF100SourceData.NozzleAreaRatioModeSwitchMach;
        }

        /// <summary>
        /// The EEC's minimum allowable power lever angle, as a fraction of the span from idle to
        /// intermediate power.
        ///
        /// TP-1373 printed p. 5: idle is permitted below Mach 0.90; the minimum then rises
        /// LINEARLY with Mach to intermediate power at Mach 1.4, and stays there above.
        ///
        /// Returns a FRACTION rather than an angle because the intermediate-power lever angle is
        /// itself contested in this pack - TM X-3261 puts military at 73 deg, TP-1034 works to 83
        /// deg. The schedule's shape is sourced; picking one of those two conventions to express
        /// it in is not, and is left to the caller who knows which document they are working in.
        /// </summary>
        public static float MinimumPowerFractionOfIntermediate(float mach)
        {
            if (float.IsNaN(mach) || float.IsInfinity(mach))
                return 0f;

            if (mach <= MavF100SourceData.EecIdlePermittedBelowMach)
                return 0f;

            if (mach >= MavF100SourceData.EecIntermediateFloorMach)
                return 1f;

            float span = MavF100SourceData.EecIntermediateFloorMach
                - MavF100SourceData.EecIdlePermittedBelowMach;

            return (mach - MavF100SourceData.EecIdlePermittedBelowMach) / span;
        }
    }
}
