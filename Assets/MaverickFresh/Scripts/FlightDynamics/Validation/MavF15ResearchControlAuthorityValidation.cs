using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using MaverickFresh.FlightDynamics.F15;
using Object = UnityEngine.Object;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>
    /// WP-2 checks for research surface authority, and for the three concepts it must not blur:
    ///
    ///   [A1] research demonstrated range, physical hard stops and actuator rate limits are separate
    ///        types, and no research-scoped type converts into a stop, a limit or a surface state
    ///   [A2] research ranges cannot populate exact hard stops; exact stops stay zero
    ///   [A3] the research demonstrated range is not labelled a physical limit
    ///   [A4] the demonstrated range is exactly what the source tabulates
    ///   [A5] the research stabilator sign convention matches the source (Baumann Table VII)
    ///   [A6] the static-equilibrium control can represent published equilibrium inputs, and
    ///        nothing outside the demonstrated range
    ///   [A7] actuator dynamics remain unavailable
    ///
    /// [A5] asserts no invented tolerance: it compares the source sign against the flipped sign,
    /// and the source thrust-line moment against its absence.
    /// </summary>
    public static class MavF15ResearchControlAuthorityValidation
    {
        /// <summary>
        /// Air density the research driver uses: "RHO - AIR DENSITY AT 20000 FT ALTITUDE,
        /// SLUG/FT^3", RHO=.0012673 (Davison, DTIC ADA256613 PDF p.91 and p.124).
        /// </summary>
        private const double SourceRhoSlugPerFt3 = 0.0012673;

        /// <summary>
        /// Stable low-alpha equilibria from Baumann Table VII (DTIC ADA217366), read on the
        /// rendered pages: stabilator and alpha from PDF p.124, true velocity from PDF p.129.
        /// Aileron, rudder and differential stabilator are 0; beta, p, q, r are ~1e-19 or smaller;
        /// thrust 8,300 lb; 20,000 ft. {point, stabilator deg, alpha deg, V kft/s}.
        /// </summary>
        private static readonly double[][] TableVii =
        {
            new[] { 1.0, -9.923545, 13.43360, 0.3384 },
            new[] { 10.0, -11.02902, 14.30227, 0.3290 },
            new[] { 20.0, -12.44352, 15.39903, 0.3183 },
            new[] { 30.0, -14.01293, 16.60968, 0.3077 }
        };

        public static string RunAll(out int passed, out int failed)
        {
            passed = 0;
            failed = 0;
            StringBuilder report = new StringBuilder(8192);
            report.AppendLine("F-15 Research Control Authority Validation");
            report.AppendLine("==========================================");
            report.AppendLine("Research configuration only. NOT NASA 836.");

            ValidateSeparateTypes(report, ref passed, ref failed);
            ValidateExactStopsZero(report, ref passed, ref failed);
            ValidateNotPhysicalLabel(report, ref passed, ref failed);
            ValidateDemonstratedRange(report, ref passed, ref failed);
            ValidateSignConvention(report, ref passed, ref failed);
            ValidateStaticControl(report, ref passed, ref failed);
            ValidateActuatorDynamicsUnavailable(report, ref passed, ref failed);

            report.AppendLine();
            report.Append("RESULT: ")
                .Append(failed == 0 ? "PASS" : "FAIL")
                .Append("  passed=").Append(passed)
                .Append(" failed=").Append(failed);
            return report.ToString();
        }

        // ---------------------------------------------------------------- [A1]

        private static void ValidateSeparateTypes(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[A1] Three concepts, three types, no conversion between them");

            Type range = typeof(MavF15ResearchDemonstratedSurfaceRange);
            Type stops = typeof(MavF15PhysicalSurfaceHardStops);
            Type rates = typeof(MavF15ActuatorRateLimits);
            Record(range != stops && stops != rates && range != rates
                   && typeof(MavF15ResearchDemonstratedControlRange) != stops,
                "ResearchDemonstratedSurfaceRange, PhysicalSurfaceHardStops and ActuatorRateLimits are distinct types",
                report, ref passed, ref failed);

            Type[] forbidden =
            {
                typeof(MavF15PhysicalSurfaceHardStops), typeof(MavF15HardStop),
                typeof(MavF15ActuatorRateLimits), typeof(MavF15RateLimit),
                typeof(MavF15SurfaceLimits), typeof(MavF15SurfaceChannelLimits),
                typeof(MavF15SurfaceState), typeof(MavF15RequestedSurfaceState),
                typeof(MavF15ActualSurfaceState), typeof(MavControlSurfaceLimits),
                typeof(MavControlInput)
            };
            Type[] research =
            {
                typeof(MavF15ResearchDemonstratedSurfaceRange),
                typeof(MavF15ResearchDemonstratedControlRange),
                typeof(MavF15ResearchStaticControlState)
            };

            string leak = null;
            for (int i = 0; i < research.Length && leak == null; i++)
                leak = FirstMemberTouching(research[i], forbidden);

            Record(leak == null,
                leak == null
                    ? "no research-scoped type takes or returns a hard stop, rate limit, actuator limit, "
                      + "surface state or control input"
                    : "LEAK: " + leak,
                report, ref passed, ref failed);

            Type[] consumers =
            {
                typeof(MavF15PhysicalSurfaceHardStops), typeof(MavF15ActuatorRateLimits),
                typeof(MavF15SurfaceLimits), typeof(MavF15SurfaceChannelLimits),
                typeof(MavF15ControlActuator), typeof(MavF15ControlLaw), typeof(MavF15AeroModel),
                typeof(MavF15AfitResearchFlightDynamicsProfile)
            };
            string intake = null;
            for (int i = 0; i < consumers.Length && intake == null; i++)
                intake = FirstMemberTouching(consumers[i], research);

            Record(intake == null,
                intake == null
                    ? "no stop, limit, actuator, control law, aero model or profile accepts a "
                      + "research-scoped range or static control"
                    : "INTAKE: " + intake,
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [A2]

        private static void ValidateExactStopsZero(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[A2] Research ranges cannot populate exact hard stops");

            MavF15PhysicalSurfaceHardStops exact = MavF15PhysicalSurfaceHardStops.ExactTarget();
            bool zero = !exact.AnyDeclared;
            for (int c = 0; c < 4; c++)
            {
                MavF15HardStop s = exact.Get((MavF15SurfaceChannel)c);
                if (s.minDeg != 0f || s.maxDeg != 0f || s.provenance != MavEngineDataProvenance.Unavailable)
                    zero = false;
            }

            Record(zero, "exact NASA 836 hard stops: undeclared, zero travel on all four channels",
                report, ref passed, ref failed);

            // Build the research range first, then re-read the exact stops: nothing is shared.
            MavF15ResearchDemonstratedControlRange r =
                MavF15ResearchDemonstratedControlRange.AfitBaumannTabulatedEquilibria();
            MavF15PhysicalSurfaceHardStops again = MavF15PhysicalSurfaceHardStops.ExactTarget();
            Record(r.symmetricStabilator.declared && !again.AnyDeclared
                   && MavF15SurfaceLimits.UnavailableExactTarget().AvailableChannelCount == 0,
                "declaring the research range leaves the exact stops and the exact actuator limits "
                + "at zero channels",
                report, ref passed, ref failed);

            Record(MavF15AfitResearchFlightDynamicsProfile.CreateUnavailableResearchControlLimits()
                       .Equals(new MavControlSurfaceLimits()),
                "the flying research profile still declares no surface travel - the range is not "
                + "its authority",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [A3]

        private static void ValidateNotPhysicalLabel(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[A3] The research demonstrated range is not labelled a physical limit");

            MavF15ResearchDemonstratedControlRange r =
                MavF15ResearchDemonstratedControlRange.AfitBaumannTabulatedEquilibria();

            bool notPhysical = true;
            for (int c = 0; c < 4; c++)
            {
                if (r.Get((MavF15SurfaceChannel)c).IsPhysicalLimit)
                    notPhysical = false;
            }

            string[] names =
            {
                MavF15ResearchDemonstratedControlRange.Kind,
                typeof(MavF15ResearchDemonstratedControlRange).Name,
                typeof(MavF15ResearchDemonstratedSurfaceRange).Name,
                typeof(MavF15ResearchStaticControlState).Name
            };
            bool namesClean = MavF15ResearchDemonstratedControlRange.Kind == "ResearchDemonstratedControlRange";
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i].IndexOf("Physical", StringComparison.OrdinalIgnoreCase) >= 0
                    || names[i].IndexOf("HardStop", StringComparison.OrdinalIgnoreCase) >= 0
                    || names[i].IndexOf("Limit", StringComparison.OrdinalIgnoreCase) >= 0)
                    namesClean = false;
            }

            Record(notPhysical && namesClean,
                "Kind = ResearchDemonstratedControlRange; no name says Physical, HardStop or Limit; "
                + "IsPhysicalLimit is false on every channel",
                report, ref passed, ref failed);

            Record(r.configurationId == MavF15AfitResearchIdentity.ConfigurationId
                   && MavF15AfitResearchIdentity.CarriesNoExactTargetToken(r.configurationId),
                "the range belongs to the research configuration and carries no exact-target token",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [A4]

        private static void ValidateDemonstratedRange(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[A4] The demonstrated range is what the source tabulates");

            MavF15ResearchDemonstratedControlRange r =
                MavF15ResearchDemonstratedControlRange.AfitBaumannTabulatedEquilibria();

            Record(r.symmetricStabilator.declared && r.symmetricStabilator.minDeg == -25f
                   && r.symmetricStabilator.maxDeg == -5f,
                "stabilator -25..-5 deg: Table III (-25) and Table V (-5) bound Table VII's "
                + "-17.30722..-5.275754",
                report, ref passed, ref failed);

            bool neutral = true;
            MavF15SurfaceChannel[] others =
            {
                MavF15SurfaceChannel.DifferentialStabilator, MavF15SurfaceChannel.Aileron,
                MavF15SurfaceChannel.Rudder
            };
            for (int i = 0; i < others.Length; i++)
            {
                MavF15ResearchDemonstratedSurfaceRange c = r.Get(others[i]);
                if (!c.declared || c.minDeg != 0f || c.maxDeg != 0f || !c.Contains(0f) || c.Contains(0.01f))
                    neutral = false;
            }

            Record(neutral,
                "differential tail, aileron, rudder: demonstrated at exactly 0 deg only",
                report, ref passed, ref failed);

            bool tableInside = true;
            for (int i = 0; i < TableVii.Length; i++)
            {
                if (!r.symmetricStabilator.Contains((float)TableVii[i][1]))
                    tableInside = false;
            }

            MavF15ResearchDemonstratedSurfaceRange undeclared =
                MavF15ResearchDemonstratedSurfaceRange.Undeclared(MavF15SurfaceChannel.Rudder, "test");
            Record(tableInside && !r.symmetricStabilator.Contains(float.NaN)
                   && !r.symmetricStabilator.Contains(-25.001f) && !r.symmetricStabilator.Contains(-4.999f)
                   && !undeclared.Contains(0f),
                "every Table VII test point is inside; NaN and values just outside are not; an "
                + "undeclared channel admits nothing, not even 0",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [A5]

        private static void ValidateSignConvention(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[A5] Research stabilator sign convention matches the source");

            Record(MavF15ResearchControlConventions.SymmetricStabilatorPositive.IndexOf("trailing edge down") >= 0
                   && MavF15ResearchControlConventions.SymmetricStabilatorPositive.IndexOf("nose-down") >= 0
                   && MavF15ResearchControlConventions.Citation.IndexOf("ADA217366") >= 0,
                "recorded convention: positive = leading edge up / trailing edge down (Baumann PDF p.86)",
                report, ref passed, ref failed);

            // Positive trailing-edge-down stabilator must pitch the nose down in the transcribed routine.
            bool noseDown = true;
            for (float a = -4f; a <= 30f; a += 2f)
            {
                float cmPlus = MavF15BaumannMach06Longitudinal.Evaluate(a * Mathf.Deg2Rad, 2f, 0f).cm;
                float cmMinus = MavF15BaumannMach06Longitudinal.Evaluate(a * Mathf.Deg2Rad, -2f, 0f).cm;
                if (!(cmPlus < cmMinus))
                    noseDown = false;
            }

            Record(noseDown,
                "alpha -4..30 deg: +2 deg stabilator gives less (more nose-down) Cm than -2 deg",
                report, ref passed, ref failed);

            // Baumann Table VII: at a published pitch equilibrium (q = 0), aero Cm plus the source's
            // thrust-line moment must vanish. Compare the source sign against the flipped sign, and
            // the source thrust moment against none. Relative checks only.
            double s = MavF15BaumannMach06Reference.WingAreaFt2;
            double cbar = MavF15BaumannMach06Reference.MeanAerodynamicChordFt;
            double thrustMomentLbfFt = MavF15AfitResearchThrustSource.SourceTotalThrustLbf
                                       * (MavF15AfitResearchThrustSource.SourceThrustLineOffsetIn / 12.0);

            bool signWins = true;
            bool thrustHelps = true;
            for (int i = 0; i < TableVii.Length; i++)
            {
                double de = TableVii[i][1];
                float alphaRad = (float)(TableVii[i][2] * Mathf.Deg2Rad);
                double v = TableVii[i][3] * 1000.0;
                double qbar = 0.5 * SourceRhoSlugPerFt3 * v * v;
                double cmThrust = thrustMomentLbfFt / (qbar * s * cbar);

                double cmSource = MavF15BaumannMach06Longitudinal.Evaluate(alphaRad, (float)de, 0f).cm;
                double cmFlipped = MavF15BaumannMach06Longitudinal.Evaluate(alphaRad, (float)-de, 0f).cm;
                double residual = cmSource + cmThrust;
                double residualFlipped = cmFlipped + cmThrust;

                report.Append("    Table VII pt ").Append(TableVii[i][0].ToString("F0"))
                    .Append(": de ").Append(de.ToString("F4"))
                    .Append("  Cm+CmT source ").Append(residual.ToString("E3"))
                    .Append("  flipped ").Append(residualFlipped.ToString("E3"))
                    .Append("  (CmT ").Append(cmThrust.ToString("E3")).AppendLine(")");

                if (!(Math.Abs(residual) < Math.Abs(residualFlipped)))
                    signWins = false;
                if (!(Math.Abs(residual) < Math.Abs(cmSource)))
                    thrustHelps = false;
            }

            Record(signWins,
                "at every Table VII point the source stabilator sign leaves a smaller pitch-moment "
                + "residual than the flipped sign - Maverick's research aero uses the source convention",
                report, ref passed, ref failed);
            Record(thrustHelps,
                "adding the source's nose-up thrust-line moment (8,300 lbf x 0.25 in) shrinks the "
                + "residual - its sign is the source's too",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [A6]

        private static void ValidateStaticControl(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[A6] STATIC_EQUILIBRIUM_VALIDATION_ONLY control");

            MavF15ResearchDemonstratedControlRange r =
                MavF15ResearchDemonstratedControlRange.AfitBaumannTabulatedEquilibria();

            bool all = true;
            for (int i = 0; i < TableVii.Length; i++)
            {
                MavF15ResearchStaticControlState state;
                string reason;
                bool ok = MavF15ResearchStaticControlState.TryCreate(
                    r, (float)TableVii[i][1], 0f, 0f, 0f, "Baumann Table VII", out state, out reason);
                MavF15BaumannSurfaceState b = state.ToBaumannSurfaceStateForStaticEvaluation();
                if (!ok || reason != MavF15ResearchStaticControlState.Label
                    || b.symmetricStabilatorDeg != (float)TableVii[i][1]
                    || b.aileronDeg != 0f || b.differentialTailDeg != 0f || b.rudderDeg != 0f)
                    all = false;
            }

            Record(all && MavF15ResearchStaticControlState.Label == "STATIC_EQUILIBRIUM_VALIDATION_ONLY",
                "every Table VII test point's published inputs are representable and reach the "
                + "research routine unchanged",
                report, ref passed, ref failed);

            MavF15ResearchStaticControlState st;
            string why;
            bool refusedOutside =
                !MavF15ResearchStaticControlState.TryCreate(r, -30f, 0f, 0f, 0f, "", out st, out why)
                && !MavF15ResearchStaticControlState.TryCreate(r, 0f, 0f, 0f, 0f, "", out st, out why)
                && !MavF15ResearchStaticControlState.TryCreate(r, -10f, 0f, 1f, 0f, "", out st, out why)
                && !MavF15ResearchStaticControlState.TryCreate(r, -10f, 0f, 0f, float.NaN, "", out st, out why);
            Record(refusedOutside,
                "-30 deg (Baumann's physical-table end), 0 deg, a nonzero aileron and a NaN rudder "
                + "are all refused - the static control is bounded by what the source tabulates",
                report, ref passed, ref failed);

            Record(!MavF15ResearchStaticControlState.TryCreate(
                    default(MavF15ResearchDemonstratedControlRange), -10f, 0f, 0f, 0f, "", out st, out why),
                "a range that does not belong to the research configuration is refused",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [A7]

        private static void ValidateActuatorDynamicsUnavailable(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[A7] Actuator dynamics remain unavailable");

            Record(!MavF15ActuatorRateLimits.ExactTarget().AnyDeclared,
                "exact actuator rate limits: none declared", report, ref passed, ref failed);

            string dynamicsMember = null;
            string[] words = { "lag", "bandwidth", "timeconstant", "tau", "naturalfrequency", "damping" };
            MemberInfo[] members = typeof(MavF15ControlActuator).GetMembers(
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public
                | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            for (int i = 0; i < members.Length; i++)
            {
                string n = members[i].Name.ToLowerInvariant();
                for (int w = 0; w < words.Length; w++)
                {
                    if (n.IndexOf(words[w], StringComparison.Ordinal) >= 0)
                        dynamicsMember = members[i].Name;
                }
            }

            Record(dynamicsMember == null,
                dynamicsMember == null
                    ? "the F-15 actuator has no lag, bandwidth or second-order dynamics member"
                    : "UNEXPECTED DYNAMICS MEMBER: " + dynamicsMember,
                report, ref passed, ref failed);

            GameObject host = new GameObject("MavF15AuthorityActuatorHost");
            host.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                MavF15ControlActuator actuator = host.AddComponent<MavF15ControlActuator>();
                MavF15ActuatorRateLimits rates = MavF15ActuatorRateLimits.FromActuatorLimits(actuator.limits);
                MavF15PhysicalSurfaceHardStops stops =
                    MavF15PhysicalSurfaceHardStops.FromActuatorLimits(actuator.limits);
                Record(!rates.AnyDeclared && !stops.AnyDeclared
                       && actuator.limits.AvailableChannelCount == 0,
                    "a fresh MavF15ControlActuator - the one the research rig uses too - declares no "
                    + "rate and no travel",
                    report, ref passed, ref failed);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>
        /// The first member of <paramref name="owner"/> whose field, property, return or parameter
        /// type is one of <paramref name="types"/> (or an array of one), or null.
        /// </summary>
        private static string FirstMemberTouching(Type owner, Type[] types)
        {
            HashSet<Type> set = new HashSet<Type>(types);
            const BindingFlags all =
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public
                | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            foreach (FieldInfo f in owner.GetFields(all))
            {
                if (Touches(f.FieldType, set))
                    return owner.Name + "." + f.Name;
            }

            foreach (PropertyInfo p in owner.GetProperties(all))
            {
                if (Touches(p.PropertyType, set))
                    return owner.Name + "." + p.Name;
            }

            foreach (MethodBase m in AllMethods(owner, all))
            {
                MethodInfo mi = m as MethodInfo;
                if (mi != null && Touches(mi.ReturnType, set))
                    return owner.Name + "." + m.Name;

                foreach (ParameterInfo pi in m.GetParameters())
                {
                    if (Touches(pi.ParameterType, set))
                        return owner.Name + "." + m.Name + "(" + pi.Name + ")";
                }
            }

            return null;
        }

        private static IEnumerable<MethodBase> AllMethods(Type owner, BindingFlags flags)
        {
            foreach (MethodInfo m in owner.GetMethods(flags))
                yield return m;
            foreach (ConstructorInfo c in owner.GetConstructors(flags))
                yield return c;
        }

        private static bool Touches(Type t, HashSet<Type> set)
        {
            if (t.IsByRef || t.IsArray)
                t = t.GetElementType();
            return t != null && set.Contains(t);
        }

        private static void Record(
            bool condition, string label,
            StringBuilder report, ref int passed, ref int failed)
        {
            if (condition) passed++; else failed++;
            report.Append(condition ? "  PASS  " : "  FAIL  ").AppendLine(label);
        }
    }
}
