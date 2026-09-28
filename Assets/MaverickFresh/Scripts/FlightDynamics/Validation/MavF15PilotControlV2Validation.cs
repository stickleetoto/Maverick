#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using MaverickFresh.FlightDynamics;
using MaverickFresh.FlightDynamics.F15;
using MaverickFresh.FlightDynamics.F16;
using Object = UnityEngine.Object;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>
    /// F-15 PILOT-CONTROL V2 - headless validation (no flight; the Play Mode flight test, including the V1 vs
    /// V2 comparison, is MavF15PilotControlledV2FlightValidationRunner).
    ///
    /// Every rig is a HideAndDontSave GameObject; no scene or prefab is opened or saved. Body loads are
    /// computed through the body's own SHADOW path (compute everything, apply nothing). Pass criteria are
    /// exact identities, signs, bounds, finiteness and continuity - never "looks right".
    ///
    ///   [W1] identity: V2 is labelled a Maverick law, NOT F-15 CAS / FLCS / SAS / NASA 836; V1 untouched
    ///   [W2] provenance: every V2 value MAVERICK_TUNED_NON_AUTHORITATIVE, with a rationale; no F-16 number
    ///   [W3] V1 mapping unchanged (bit for bit) and shared: V2 goes through the same conventions / envelopes
    ///   [W4] pure law: neutral = trim bias exactly; command and feedback signs; 0.3 differential tail
    ///   [W5] pure law: envelopes, NaN, continuity, throttle pass-through
    ///   [W6] dynamic-pressure schedule: exactly 1 at the reference, clamped, continuous, fail-safe
    ///   [W7] washout: the explicit law state, its decay, and its reset
    ///   [W8] rig: V1 rig carries V1 only; V2 rig carries both with exactly one enabled; in-flight switch rules
    ///   [W9] ownership: V2 granted; two enabled laws, or a non-pilot law, refused; V1 still granted
    ///   [W10] sign response through the real V2 rig (law -> actuator -> aero -> load set), SHADOW
    ///   [W11] source scan of the V2 layer: no Rigidbody writes, no arming, no forbidden types, no F-16 type
    ///   [W12] the in-flight law switch fails closed: a missing, inactive, disabled or silent command source, a
    ///         non-finite command or any axis outside the centred tolerance refuses it and changes nothing; an
    ///         already-bound mode succeeds without a command read; an accepted switch keeps one law and one load per step
    /// </summary>
    public static class MavF15PilotControlV2Validation
    {
        private const float Dt = 0.02f;

        public static string RunAll(out int passed, out int failed)
        {
            passed = 0;
            failed = 0;
            StringBuilder report = new StringBuilder(32 * 1024);
            report.AppendLine("F-15 PILOT-CONTROL V2 (Maverick closed-loop augmentation) - headless validation (" + MavF15PilotControlledIdentity.ConfigurationId + ")");
            report.AppendLine("Research aerodynamics (frozen) + Maverick control law V2. THIS IS NOT THE F-15 FCS. Not NASA 836. Nothing is flown here.");

            List<Object> created = new List<Object>();
            try
            {
                ValidateIdentity(report, ref passed, ref failed);
                ValidateProvenance(report, ref passed, ref failed);
                ValidateSharedMapping(report, ref passed, ref failed);
                ValidateLawSigns(report, ref passed, ref failed);
                ValidateLawBounds(report, ref passed, ref failed);
                ValidateSchedule(report, ref passed, ref failed);
                ValidateWashout(report, ref passed, ref failed);
                ValidateRig(created, report, ref passed, ref failed);
                ValidateOwnership(created, report, ref passed, ref failed);
                ValidateSignsThroughRig(created, report, ref passed, ref failed);
                ValidateSourceScan(report, ref passed, ref failed);
                ValidateSwitchFailClosed(created, report, ref passed, ref failed);
            }
            catch (Exception e)
            {
                failed++;
                report.AppendLine("  FAIL  exception: " + e);
            }
            finally
            {
                for (int i = 0; i < created.Count; i++)
                {
                    if (created[i] != null)
                        Object.DestroyImmediate(created[i]);
                }
            }

            report.AppendLine();
            report.AppendLine("RESULT: " + (failed == 0 ? "PASS" : "FAIL") + "  passed=" + passed + " failed=" + failed);
            return report.ToString();
        }

        // ---------------------------------------------------------------- helpers

        private static float Bias
        {
            get { return (float)MavF15PilotTrimStart.TableViiPoint36().symmetricStabilatorDeg; }
        }

        /// <summary>The exact trim state as the law sees it: rates and sideslip zero, alpha and qbar of the trim.</summary>
        private static MavFlightState TrimState()
        {
            MavF15PilotTrimStart t = MavF15PilotTrimStart.TableViiPoint36();
            MavFlightState s = new MavFlightState();
            s.alphaRad = (float)(t.alphaDeg * Math.PI / 180.0);
            s.trueAirspeedMps = (float)(t.trueAirspeedFtPerSec * MavF15BaumannMach06Reference.FootToM);
            s.dynamicPressurePa = MavF15PilotControlGainsV2.ReferenceDynamicPressureAtTrimPa();
            return s;
        }

        private static MavF15PilotControlSolution Law(MavPilotCommand c, MavFlightState s, out MavF15PilotControlLawV2Debug d)
        {
            MavF15PilotControlLawV2State st = MavF15PilotControlLawV2State.Zero;
            return MavF15PilotControlLawV2.Compute(c, s, MavF15PilotControlGainsV2.V2(), MavF15GameplayControlAuthority.V1(), Bias, ref st, Dt, out d);
        }

        private static MavF15PilotControlSolution Law(MavPilotCommand c, MavFlightState s)
        {
            MavF15PilotControlLawV2Debug d;
            return Law(c, s, out d);
        }

        private static MavF15PilotControlledRig PilotRig(List<Object> created, string name, MavF15PilotControlMode mode)
        {
            MavF15PilotControlledRig rig = MavF15PilotControlledRig.Create(name, MavF15PilotCommandSourceKind.Scripted, false, mode, MavF15PilotPhysicsRevision.R1InstantaneousSurfaces);
            rig.gameObject.hideFlags = HideFlags.HideAndDontSave;

            // Edit mode calls no Awake: build the (idempotent) stack explicitly, exactly as Awake does in Play Mode.
            rig.EnsureStack();
            created.Add(rig.gameObject);
            return rig;
        }

        private static bool Inject(MavSixDoFBody body, MavF15PilotTrimStart t, out string reason)
        {
            Vector3 p, v, w;
            Quaternion q;
            if (!MavF15AfitResearchStateInjection.TryComputeKinematics(t.ToKinematicState(), out p, out q, out v, out w, out reason))
                return false;
            if (!body.TryApplyInitialKinematicState(p, q, v, w, out reason))
                return false;

            body.StepPhysicsForValidation(0f, 0f);
            return true;
        }

        // ---------------------------------------------------------------- [W1]

        private static void ValidateIdentity(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[W1] Identity");
            string n = MavF15PilotControlLawV2.LawName;
            Record(n.Contains("NOT F-15 CAS / FLCS / SAS, NOT NASA 836") && n.Contains(MavF15PilotControlProvenance.MaverickTunedNonAuthoritative)
                   && n.Contains("Maverick F-15 pilot-control V2"),
                "V2 law name: \"" + n + "\"", report, ref passed, ref failed);
            Record(MavF15PilotControlLaw.LawName.Contains("pilot-control approximation V1") && MavF15PilotControlLaw.LawName.Contains("NOT F-15 CAS / FLCS, NOT NASA 836"),
                "V1 law name unchanged: \"" + MavF15PilotControlLaw.LawName + "\"", report, ref passed, ref failed);
            Record((int)MavF15PilotControlMode.DirectV1 == 0 && (int)MavF15PilotControlMode.AssistedV2 == 1
                   && default(MavF15PilotControlMode) == MavF15PilotControlMode.DirectV1,
                "control modes: DirectV1 = 0 is the default of every rig (V1 stays the baseline); AssistedV2 = 1 is opt-in",
                report, ref passed, ref failed);
            Record(MavF15PilotControlledIdentity.ConfigurationId == "F15_AFIT_BAUMANN_DAVISON_PILOT_CONTROLLED_V1",
                "V2 is a second control law of the same pilot-controlled aircraft configuration (" + MavF15PilotControlledIdentity.ConfigurationId
                + "): same frozen physics, same authority, same owner", report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [W2]

        private static void ValidateProvenance(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[W2] Provenance of every V2 value");
            MavF15PilotControlGainsV2 g = MavF15PilotControlGainsV2.V2();
            MavF15TunedValue[] entries = MavF15PilotControlGainsV2.Entries();

            FieldInfo[] fields = typeof(MavF15PilotControlGainsV2).GetFields(BindingFlags.Public | BindingFlags.Instance);
            bool everyFieldListed = true, valuesMatch = true, labelled = true;
            string missing = "";
            foreach (FieldInfo f in fields)
            {
                MavF15TunedValue e = Array.Find(entries, x => x.name == f.Name);
                if (e.name == null)
                {
                    everyFieldListed = false;
                    missing += f.Name + " ";
                    continue;
                }

                float v = f.FieldType == typeof(bool) ? ((bool)f.GetValue(g) ? 1f : 0f) : (float)f.GetValue(g);
                valuesMatch &= v == e.value;
                labelled &= e.provenance == MavF15PilotControlProvenance.MaverickTunedNonAuthoritative
                            && !string.IsNullOrEmpty(e.rationale) && e.rationale.Length > 40 && !string.IsNullOrEmpty(e.unit);
            }

            Record(everyFieldListed && entries.Length == fields.Length && valuesMatch && labelled,
                "all " + fields.Length + " V2 values are listed with unit, rationale and the label " + MavF15PilotControlProvenance.MaverickTunedNonAuthoritative
                + (missing.Length > 0 ? " - MISSING: " + missing : ""), report, ref passed, ref failed);
            foreach (MavF15TunedValue e in entries)
                report.AppendLine("      " + e.name + " = " + e.value.ToString("R") + " " + e.unit);

            MavF15TunedValue assist = Array.Find(entries, x => x.name == "stabilityAxisYawRateGainDegPerRadSec");
            Record(assist.rationale != null && assist.rationale.StartsWith(MavF15PilotControlProvenance.GameplayResearchAssist, StringComparison.Ordinal),
                "the washed-out yaw-rate term is labelled \"" + MavF15PilotControlProvenance.GameplayResearchAssist + "\" (never F-15 yaw CAS / SAS / production yaw damper)",
                report, ref passed, ref failed);

            string reason;
            Record(g.IsSelfConsistent(out reason) && MavF15GameplayControlAuthority.V1().IsSelfConsistent(out reason),
                "the V2 gains are self-consistent, and V2 flies inside the unchanged V1 gameplay envelopes", report, ref passed, ref failed);

            // No F-16 number: every analogous F-16 control-law default differs.
            MavF16ControlLawGains f16 = MavF16ControlLawGains.Default;
            float[,] pairs =
            {
                { g.commandedPitchRateAtFullStickDegSec, f16.commandedPitchRateAtFullStickDegSec },
                { g.pitchRateGainDegPerRadSec, f16.pitchRateGainDegPerRadSec },
                { g.commandedRollRateAtFullStickDegSec, f16.commandedRollRateAtFullStickDegSec },
                { g.rollRateGainDegPerRadSec, f16.rollRateGainDegPerRadSec },
                { g.commandedSideslipAtFullPedalDeg, f16.commandedSideslipAtFullPedalDeg },
                { g.sideslipGainDegPerDeg, f16.sideslipGainDegPerDeg },
                { g.stabilityAxisYawRateGainDegPerRadSec, f16.yawDamperGainDegPerRadSec },
                { g.yawRateWashoutTimeConstantSeconds, f16.yawRateWashoutTimeConstantSeconds },
                { g.aileronRudderInterconnectDegPerRadSec, f16.aileronRudderInterconnectDegPerRadSec },
                { g.referenceDynamicPressurePa, f16.referenceDynamicPressurePa },
                { g.minimumGainScale, f16.minimumGainScale },
                { g.maximumGainScale, f16.maximumGainScale }
            };
            bool noneCopied = true;
            for (int i = 0; i < pairs.GetLength(0); i++)
                noneCopied &= pairs[i, 0] != pairs[i, 1];
            Record(noneCopied,
                "no F-16 number: all " + pairs.GetLength(0) + " V2 values differ from their Maverick F-16 control-law counterparts (architecture reused, numbers not)",
                report, ref passed, ref failed);

            Record(Math.Abs(g.referenceDynamicPressurePa - 0.5 * MavF15SourceExercisedOperatingDomain.SourceAirDensitySlugPerFt3 * 300.8 * 300.8 * 47.88025898033584) < 1e-2,
                "schedule reference qbar " + g.referenceDynamicPressurePa.ToString("F2") + " Pa = the source's fixed density at the point-36 trim speed (derived, not chosen)",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [W3]

        private static void ValidateSharedMapping(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[W3] The V1 mapping is unchanged and is the one owner of conventions and envelopes for both laws");
            MavF15GameplayControlAuthority a = MavF15GameplayControlAuthority.V1();
            float bias = Bias;
            bool v1Exact = true, sharedExact = true;
            int n = 0;
            for (float pi = -1.2f; pi <= 1.21f; pi += 0.1f)
            for (float ro = -1.2f; ro <= 1.21f; ro += 0.3f)
            for (float ya = -1.2f; ya <= 1.21f; ya += 0.6f)
            {
                n++;
                MavPilotCommand c = new MavPilotCommand { pitch = pi, roll = ro, yaw = ya, throttle01 = 0.4f };
                MavF15PilotControlSolution s = MavF15PilotControlMapping.Solve(c, a, bias);

                // The V1 formula, written out independently.
                MavPilotCommand cc = c.Clamped();
                float ail = 1f * cc.roll * a.rollAileronDegPerUnit;
                float stab = bias + -1f * cc.pitch * a.pitchStabilatorDegPerUnit;
                float rud = -1f * cc.yaw * a.yawRudderDegPerUnit;
                // To float precision: Mono may keep an inline expression in wider precision than a stored field.
                // The bit-for-bit proof that V1 is unchanged is its own suites (49/0 headless, 31/0 Play Mode),
                // byte-identical to the V1 record.
                const float tol = 1e-5f;
                v1Exact &= Mathf.Abs(s.unbounded.symmetricStabilatorDeg - stab) < tol && Mathf.Abs(s.unbounded.aileronDeg - ail) < tol
                           && Mathf.Abs(s.unbounded.differentialStabilatorDeg - 0.3f * ail) < tol && Mathf.Abs(s.unbounded.rudderDeg - rud) < tol
                           && Mathf.Abs(s.requested.symmetricStabilatorDeg - Mathf.Clamp(stab, -25f, -5f)) < tol
                           && Mathf.Abs(s.requested.aileronDeg - Mathf.Clamp(ail, -20f, 20f)) < tol
                           && Mathf.Abs(s.requested.differentialStabilatorDeg - Mathf.Clamp(0.3f * ail, -6f, 6f)) < tol
                           && Mathf.Abs(s.requested.rudderDeg - Mathf.Clamp(rud, -15f, 15f)) < tol;

                // V2's entry point with the V1-equivalent demands gives the same surfaces.
                MavF15PilotControlSolution d = MavF15PilotControlMapping.SolveFromDemands(
                    c, cc.pitch * a.pitchStabilatorDegPerUnit, cc.roll * a.rollAileronDegPerUnit, cc.yaw * a.yawRudderDegPerUnit, a, bias);
                sharedExact &= d.requested.symmetricStabilatorDeg == s.requested.symmetricStabilatorDeg && d.requested.aileronDeg == s.requested.aileronDeg
                               && d.requested.differentialStabilatorDeg == s.requested.differentialStabilatorDeg && d.requested.rudderDeg == s.requested.rudderDeg
                               && d.AnyLimited == s.AnyLimited && d.command.throttle01 == s.command.throttle01;
            }

            Record(v1Exact, "V1 Solve equals its written-out formula (to float precision) on " + n + " commands, incl. beyond +-1: the shared refactor changed nothing",
                report, ref passed, ref failed);
            Record(sharedExact, "SolveFromDemands with the V1-equivalent demands returns V1's surfaces bit for bit: one mapping, one set of conventions, one set of envelopes",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [W4]

        private static void ValidateLawSigns(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[W4] Pure law: neutral, command signs, feedback signs, differential tail");
            MavFlightState trim = TrimState();
            MavF15PilotControlLawV2Debug d;
            MavF15PilotControlSolution n = Law(MavPilotCommand.Neutral, trim, out d);
            Record(n.requested.symmetricStabilatorDeg == Bias && n.requested.aileronDeg == 0f && n.requested.differentialStabilatorDeg == 0f
                   && n.requested.rudderDeg == 0f && !n.AnyLimited && d.gainScale == 1f && d.stateUsable,
                "centred stick at the trim state requests EXACTLY the trim bias (" + Bias.ToString("R") + " deg) and zero lateral surfaces; gain scale exactly 1",
                report, ref passed, ref failed);

            MavF15PilotControlSolution pu = Law(new MavPilotCommand { pitch = 0.5f }, trim, out d);
            MavF15PilotControlSolution pd = Law(new MavPilotCommand { pitch = -0.5f }, trim);
            Record(pu.requested.symmetricStabilatorDeg < Bias && pd.requested.symmetricStabilatorDeg > Bias && d.commandedPitchRateRadSec > 0f
                   && pu.requested.aileronDeg == 0f && pu.requested.rudderDeg == 0f,
                "pitch +0.5 commands q " + d.commandedPitchRateRadSec.ToString("F4") + " rad/s and moves the stabilator nose-up (negative) to "
                + pu.requested.symmetricStabilatorDeg.ToString("F3") + "; pitch -0.5 nose-down to " + pd.requested.symmetricStabilatorDeg.ToString("F3"),
                report, ref passed, ref failed);

            MavF15PilotControlSolution rr = Law(new MavPilotCommand { roll = 0.5f }, trim, out d);
            MavF15PilotControlSolution rl = Law(new MavPilotCommand { roll = -0.5f }, trim);
            Record(rr.requested.aileronDeg > 0f && rl.requested.aileronDeg < 0f
                   && Mathf.Abs(rr.requested.differentialStabilatorDeg - 0.3f * rr.requested.aileronDeg) < 1e-5f
                   && rr.requested.rudderDeg < 0f && rl.requested.rudderDeg > 0f && rr.requested.symmetricStabilatorDeg == Bias,
                "roll +0.5: aileron +" + rr.requested.aileronDeg.ToString("F3") + " (roll right), differential tail = 0.3 x aileron ("
                + rr.requested.differentialStabilatorDeg.ToString("F3") + "), interconnect rudder " + rr.requested.rudderDeg.ToString("F3")
                + " (nose right, into the roll); roll -0.5 mirrors", report, ref passed, ref failed);

            MavF15PilotControlSolution yr = Law(new MavPilotCommand { yaw = 0.5f }, trim, out d);
            MavF15PilotControlSolution yl = Law(new MavPilotCommand { yaw = -0.5f }, trim);
            Record(yr.requested.rudderDeg < 0f && yl.requested.rudderDeg > 0f && d.commandedSideslipDeg < 0f && yr.requested.aileronDeg == 0f,
                "yaw +0.5 (nose right) commands sideslip " + d.commandedSideslipDeg.ToString("F2") + " deg (negative, beta = asin(v/V)) and rudder "
                + yr.requested.rudderDeg.ToString("F3") + " (trailing edge right = nose right); yaw -0.5 mirrors", report, ref passed, ref failed);

            MavFlightState s = trim;
            s.aeroBodyRatesRadSec = new Vector3(0f, 0.05f, 0f);
            bool qFeedback = Law(MavPilotCommand.Neutral, s).requested.symmetricStabilatorDeg > Bias;
            s = trim;
            float a = trim.alphaRad;
            s.aeroBodyRatesRadSec = new Vector3(0.05f * Mathf.Cos(a), 0f, 0.05f * Mathf.Sin(a));
            MavF15PilotControlSolution roll = Law(MavPilotCommand.Neutral, s, out d);
            bool pFeedback = roll.requested.aileronDeg < 0f && Mathf.Abs(d.stabilityAxisYawRateRadSec) < 1e-6f && Mathf.Abs(roll.requested.rudderDeg) < 1e-4f;
            s = trim;
            s.betaRad = 0.01f;
            bool betaFeedback = Law(MavPilotCommand.Neutral, s).requested.rudderDeg < 0f;
            s = trim;
            s.aeroBodyRatesRadSec = new Vector3(0f, 0f, 0.05f);
            MavF15PilotControlSolution yawRate = Law(MavPilotCommand.Neutral, s, out d);
            bool rFeedback = yawRate.requested.rudderDeg > 0f && d.washedOutYawRateRadSec > 0f;
            s = trim;
            s.aeroBodyRatesRadSec = new Vector3(0.05f, 0f, 0f);
            bool bodyRoll = Law(MavPilotCommand.Neutral, s, out d).requested.rudderDeg < 0f && d.stabilityAxisYawRateRadSec < 0f;
            Record(qFeedback && pFeedback && betaFeedback && rFeedback && bodyRoll,
                "feedback opposes motion: +q -> nose-down; a roll about the velocity vector (+p_s) -> left aileron and NO yaw-rate rudder; +beta -> nose-right rudder; "
                + "+stability-axis yaw rate -> nose-left rudder; a pure body-axis roll (+p, r = 0) -> nose-right rudder (coordination)",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [W5]

        private static void ValidateLawBounds(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[W5] Pure law: envelopes, non-finite inputs, continuity, throttle");
            MavF15GameplayControlAuthority a = MavF15GameplayControlAuthority.V1();
            MavFlightState trim = TrimState();
            bool bounded = true, limitedSeen = false;
            float[] big = { -50f, -1f, 0f, 1f, 50f };
            foreach (float cmd in new[] { -1f, 0f, 1f })
            foreach (float q in big)
            foreach (float p in big)
            foreach (float b in big)
            {
                MavFlightState s = trim;
                s.aeroBodyRatesRadSec = new Vector3(p, q, -p);
                s.betaRad = b * 0.01f;
                MavF15PilotControlSolution r = Law(new MavPilotCommand { pitch = cmd, roll = -cmd, yaw = cmd }, s);
                bounded &= r.requested.symmetricStabilatorDeg >= a.symmetricStabilator.minDeg && r.requested.symmetricStabilatorDeg <= a.symmetricStabilator.maxDeg
                           && Mathf.Abs(r.requested.aileronDeg) <= 20f && Mathf.Abs(r.requested.differentialStabilatorDeg) <= 6f
                           && Mathf.Abs(r.requested.rudderDeg) <= 15f && r.requested.IsFinite();
                limitedSeen |= r.AnyLimited;
            }

            Record(bounded && limitedSeen,
                "extreme commands and state errors (rates to 50 rad/s) stay inside the unchanged gameplay envelopes (stab -25..-5, ail +-20, diff +-6, rud +-15) and report limiting",
                report, ref passed, ref failed);

            MavF15PilotControlLawV2Debug d;
            MavF15PilotControlSolution nanCmd = Law(new MavPilotCommand { pitch = float.NaN, roll = float.PositiveInfinity, yaw = float.NaN, throttle01 = float.NaN }, trim);
            MavFlightState bad = trim;
            bad.aeroBodyRatesRadSec = new Vector3(float.NaN, 0f, 0f);
            bad.betaRad = float.NegativeInfinity;
            MavF15PilotControlSolution nanState = Law(new MavPilotCommand { pitch = 0.5f }, bad, out d);
            MavF15PilotControlSolution ffOnly = Law(new MavPilotCommand { pitch = 0.5f }, trim);
            Record(nanCmd.requested.symmetricStabilatorDeg == Bias && nanCmd.requested.aileronDeg == 0f && nanCmd.requested.rudderDeg == 0f
                   && !d.stateUsable && nanState.requested.IsFinite() && nanState.requested.symmetricStabilatorDeg < Bias
                   && Mathf.Abs(nanState.requested.symmetricStabilatorDeg - ffOnly.requested.symmetricStabilatorDeg) < 1e-3f,
                "a non-finite command is centred (trim bias, zero lateral); a non-finite state zeroes every feedback term and is reported - the command still acts, nothing non-finite reaches a surface",
                report, ref passed, ref failed);

            float worst = 0f;
            for (int i = 0; i < 200; i++)
            {
                float c0 = -1f + i * 0.01f;
                MavF15PilotControlSolution x0 = Law(new MavPilotCommand { pitch = c0, roll = c0, yaw = c0 }, trim);
                MavF15PilotControlSolution x1 = Law(new MavPilotCommand { pitch = c0 + 0.01f, roll = c0 + 0.01f, yaw = c0 + 0.01f }, trim);
                worst = Mathf.Max(worst, Mathf.Abs(x1.requested.symmetricStabilatorDeg - x0.requested.symmetricStabilatorDeg));
                worst = Mathf.Max(worst, Mathf.Abs(x1.requested.aileronDeg - x0.requested.aileronDeg));
                worst = Mathf.Max(worst, Mathf.Abs(x1.requested.rudderDeg - x0.requested.rudderDeg));
            }

            // Analytic slope bound of the law at a fixed state (schedule 1): a 0.01 step on every axis at once.
            MavF15PilotControlGainsV2 g = MavF15PilotControlGainsV2.V2();
            float qMax = g.commandedPitchRateAtFullStickDegSec * Mathf.Deg2Rad, pMax = g.commandedRollRateAtFullStickDegSec * Mathf.Deg2Rad;
            float bound = 0.01f * Mathf.Max(
                (g.pitchRateFeedForwardDegPerRadSec + g.pitchRateGainDegPerRadSec) * qMax,
                Mathf.Max((g.rollRateFeedForwardDegPerRadSec + g.rollRateGainDegPerRadSec) * pMax,
                    (g.sideslipFeedForwardDegPerDeg + g.sideslipGainDegPerDeg) * g.commandedSideslipAtFullPedalDeg + g.aileronRudderInterconnectDegPerRadSec * pMax));
            Record(worst <= bound * 1.001f && worst > 0f,
                "continuous and linear in the command: a 0.01 step on every axis moves no surface more than " + worst.ToString("F4")
                + " deg, the law's analytic slope bound (" + bound.ToString("F4") + " deg: rudder = pedal feed-forward + feedback + interconnect) - no switching, no jump",
                report, ref passed, ref failed);

            MavF15PilotControlSolution th = Law(new MavPilotCommand { throttle01 = 0.73f }, trim);
            Record(th.command.throttle01 == 0.73f && th.requested.symmetricStabilatorDeg == Bias,
                "throttle is carried to telemetry only and moves no surface: " + MavF15PilotControlledFixedThrust.ThrottleStatus,
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [W6]

        private static void ValidateSchedule(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[W6] Dynamic-pressure gain schedule (surface gains only - never a load)");
            MavF15PilotControlGainsV2 g = MavF15PilotControlGainsV2.V2();
            float q0 = g.referenceDynamicPressurePa;
            bool exact = MavF15PilotControlLawV2.GainScale(g, q0) == 1f;
            bool clampLow = MavF15PilotControlLawV2.GainScale(g, q0 * 10f) == g.minimumGainScale;
            bool clampHigh = MavF15PilotControlLawV2.GainScale(g, q0 * 0.1f) == g.maximumGainScale;
            bool failSafe = MavF15PilotControlLawV2.GainScale(g, 0f) == 1f && MavF15PilotControlLawV2.GainScale(g, float.NaN) == 1f
                            && MavF15PilotControlLawV2.GainScale(g, -5f) == 1f && MavF15PilotControlLawV2.GainScale(g, float.PositiveInfinity) == 1f;
            MavF15PilotControlGainsV2 off = g;
            off.scheduleGainsWithDynamicPressure = false;
            bool disabled = MavF15PilotControlLawV2.GainScale(off, q0 * 3f) == 1f;
            float jump = 0f;
            float prev = MavF15PilotControlLawV2.GainScale(g, q0 * 0.3f);
            for (int i = 1; i <= 4000; i++)
            {
                float next = MavF15PilotControlLawV2.GainScale(g, q0 * (0.3f + i * 0.001f));
                jump = Mathf.Max(jump, Mathf.Abs(next - prev));
                prev = next;
            }

            float lowEdge = MavF15PilotControlLawV2.GainScale(g, (float)(q0 * Math.Pow(300.8 / 218.5, 2.0)));
            Record(exact && clampLow && clampHigh && failSafe && disabled && jump < 0.01f,
                "scale = qbarRef/qbar clamped to [" + g.minimumGainScale + ", " + g.maximumGainScale + "]: exactly 1 at the reference, clamped both ways, 1 when disabled "
                + "or when qbar is not a positive finite number, continuous (largest step " + jump.ToString("E2") + " over a 0.1 % qbar grid); "
                + lowEdge.ToString("F2") + " at the 218.5 ft/s domain edge", report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [W7]

        private static void ValidateWashout(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[W7] Law state: the stability-axis yaw-rate washout (" + MavF15PilotControlProvenance.GameplayResearchAssist + ")");
            MavF15PilotControlGainsV2 g = MavF15PilotControlGainsV2.V2();
            MavF15GameplayControlAuthority a = MavF15GameplayControlAuthority.V1();
            MavFlightState s = TrimState();
            s.aeroBodyRatesRadSec = new Vector3(0f, 0f, 0.05f);
            MavF15PilotControlLawV2State st = MavF15PilotControlLawV2State.Zero;
            MavF15PilotControlLawV2Debug d;
            float first = 0f, afterTau = 0f, afterLong = 0f;
            int stepsPerTau = Mathf.RoundToInt(g.yawRateWashoutTimeConstantSeconds / Dt);
            for (int i = 0; i < 20 * stepsPerTau; i++)
            {
                MavF15PilotControlLawV2.Compute(MavPilotCommand.Neutral, s, g, a, Bias, ref st, Dt, out d);
                if (i == 0) first = d.washedOutYawRateRadSec;
                if (i == stepsPerTau - 1) afterTau = d.washedOutYawRateRadSec;
                afterLong = d.washedOutYawRateRadSec;
            }

            float input = 0.05f * Mathf.Cos(s.alphaRad);
            Record(first > 0.9f * input && afterTau > 0.3f * input && afterTau < 0.42f * input && Mathf.Abs(afterLong) < 1e-4f * input && st.yawRateLowPassRadSec > 0.99f * input,
                "a held yaw rate passes at first (" + (first / input).ToString("F3") + ") and washes out with the " + g.yawRateWashoutTimeConstantSeconds
                + " s time constant (" + (afterTau / input).ToString("F3") + " after one time constant, ~e^-1; " + (afterLong / input).ToString("E1")
                + " after 20): a sustained turn is not opposed", report, ref passed, ref failed);

            GameObject go = new GameObject("w7-law");
            go.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                MavF15PilotControlLawV2 law = go.AddComponent<MavF15PilotControlLawV2>();
                law.lawState.yawRateLowPassRadSec = 0.3f;
                law.ResetLawState();
                Record(law.lawState.yawRateLowPassRadSec == 0f && law.gains.stabilityAxisYawRateGainDegPerRadSec == g.stabilityAxisYawRateGainDegPerRadSec,
                    "ResetLawState (called on enable, at the trim start and on every law switch) zeroes the filter; a new law carries the V2 defaults",
                    report, ref passed, ref failed);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ---------------------------------------------------------------- [W8]

        private static void ValidateRig(List<Object> created, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[W8] Rig: law selection and the in-flight switch");
            MavF15PilotControlledRig v1 = PilotRig(created, "w8-v1", MavF15PilotControlMode.DirectV1);
            Record(v1.lawV2 == null && v1.GetComponent<MavF15PilotControlLawV2>() == null && v1.law != null && v1.law.enabled
                   && v1.body.controlLaw == v1.law && v1.ActiveLaw == v1.law,
                "a Direct V1 rig is the V1 baseline: it carries the V1 law only, enabled and bound", report, ref passed, ref failed);

            MavF15PilotControlledRig v2 = PilotRig(created, "w8-v2", MavF15PilotControlMode.AssistedV2);
            Record(v2.lawV2 != null && v2.law != null && v2.lawV2.enabled && !v2.law.enabled && v2.body.controlLaw == v2.lawV2
                   && v2.lawV2.configuration == v2.profile && v2.lawV2.actuator == v2.actuator && v2.lawV2.commandSource == v2.commandSource
                   && v2.actuator.limits.symmetricStabilator.minDeg == -25f && v2.actuator.limits.rudder.maxDeg == 15f,
                "an Assisted V2 rig carries both laws - V2 enabled and bound, V1 disabled - wired to the same profile, actuator (same travel) and command source",
                report, ref passed, ref failed);

            MavManualPilotCommandSource src = (MavManualPilotCommandSource)v2.commandSource;
            src.command = new MavPilotCommand { roll = 0.3f };
            string r1;
            bool refused = !v2.TrySetControlMode(MavF15PilotControlMode.DirectV1, out r1);
            bool unchanged = v2.controlMode == MavF15PilotControlMode.AssistedV2 && v2.lawV2.enabled && !v2.law.enabled && v2.body.controlLaw == v2.lawV2;
            src.command = new MavPilotCommand { roll = 0.02f, throttle01 = 1f };
            string r2;
            bool accepted = v2.TrySetControlMode(MavF15PilotControlMode.DirectV1, out r2);
            bool nowV1 = v2.controlMode == MavF15PilotControlMode.DirectV1 && v2.law.enabled && !v2.lawV2.enabled && v2.body.controlLaw == v2.law;
            v2.lawV2.lawState.yawRateLowPassRadSec = 0.2f;
            string r3;
            bool back = v2.TrySetControlMode(MavF15PilotControlMode.AssistedV2, out r3);
            bool nowV2 = v2.lawV2.enabled && !v2.law.enabled && v2.body.controlLaw == v2.lawV2 && v2.lawV2.lawState.yawRateLowPassRadSec == 0f;
            Record(refused && unchanged && accepted && nowV1 && back && nowV2,
                "switch refused with the stick deflected (nothing changes: \"" + r1 + "\"); accepted with the controls centred (within "
                + MavF15PilotControlledRig.SwitchCentredTolerance + "; throttle ignored) both ways; exactly one law enabled and bound after each; V2 restarts with zero filter state",
                report, ref passed, ref failed);

            string r4;
            bool addV2 = v1.TrySetControlMode(MavF15PilotControlMode.AssistedV2, out r4);
            Record(addV2 && v1.lawV2 != null && v1.lawV2.enabled && !v1.law.enabled && v1.body.controlLaw == v1.lawV2,
                "a V1 rig switched to V2 gains the V2 law on demand (the V1 prefab can fly V2 without being rebuilt)", report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [W9]

        private static void ValidateOwnership(List<Object> created, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[W9] Ownership: F15PilotControlledResearch with the V2 law");
            string reason;
            MavF15PilotControlledRig rig = PilotRig(created, "w9-v2", MavF15PilotControlMode.AssistedV2);
            Inject(rig.body, MavF15PilotTrimStart.TableViiPoint36(), out reason);
            MavFlightPhysicsOwnership gate = rig.gameObject.AddComponent<MavFlightPhysicsOwnership>();
            rig.body.physicsOwnership = gate;
            gate.AttachGovernedBody(rig.body);
            rig.body.NotifyOwnershipChanged();
            gate.allowF15PilotControlledOwnership = true;

            string e1, e2, e3, e4;
            bool grant = MavF15PilotControlledOwnershipGrant.Instance.TryGrantResearchOwnership(rig.body, out e1);

            rig.law.enabled = true;
            bool both = !MavF15PilotControlledOwnershipGrant.Instance.TryGrantResearchOwnership(rig.body, out e2);
            bool bothEntry = !gate.TryEnterF15PilotControlledOwnership(rig.body, MavF15PilotControlledOwnershipGrant.Instance, out e3);
            rig.law.enabled = false;

            rig.lawV2.enabled = false;
            string e5;
            bool disabledBound = !MavF15PilotControlledOwnershipGrant.Instance.TryGrantResearchOwnership(rig.body, out e5);
            rig.lawV2.enabled = true;

            MavFlightControlLawBase saved = rig.body.controlLaw;
            MavDirectSurfaceControlLaw foreign = rig.gameObject.AddComponent<MavDirectSurfaceControlLaw>();
            rig.body.controlLaw = foreign;
            bool foreignRefused = !MavF15PilotControlledOwnershipGrant.Instance.TryGrantResearchOwnership(rig.body, out e4);
            rig.body.controlLaw = saved;
            Object.DestroyImmediate(foreign);

            Record(grant && both && bothEntry && disabledBound && foreignRefused && gate.owner == MavFlightPhysicsOwner.Legacy && !rig.body.ArmedForLiveFlight,
                "the V2 rig is granted; refused, owner left Legacy and body unarmed: V1 AND V2 both enabled (a second surface requester), the bound law disabled, "
                + "a non-pilot law bound", report, ref passed, ref failed);
            report.AppendLine("      e.g. " + e2);
            report.AppendLine("      e.g. " + e4);

            bool entered = gate.TryEnterF15PilotControlledOwnership(rig.body, MavF15PilotControlledOwnershipGrant.Instance, out e1);
            bool armed = rig.body.ArmedForLiveFlight && gate.owner == MavFlightPhysicsOwner.F15PilotControlledResearch;
            gate.ReturnToLegacy("w9 done");
            Record(entered && armed && !rig.body.ArmedForLiveFlight,
                "the V2 rig enters F15PilotControlledResearch (armed by the authority itself) and returns to Legacy disarmed", report, ref passed, ref failed);

            MavF15PilotControlledRig v1 = PilotRig(created, "w9-v1", MavF15PilotControlMode.DirectV1);
            Inject(v1.body, MavF15PilotTrimStart.TableViiPoint36(), out reason);
            string e6;
            Record(MavF15PilotControlledOwnershipGrant.Instance.TryGrantResearchOwnership(v1.body, out e6),
                "the V1 rig is still granted: " + e6, report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [W10]

        private static void ValidateSignsThroughRig(List<Object> created, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[W10] Sign response through the real V2 rig (law -> actuator -> aero -> load set), SHADOW: computed, never applied");
            MavF15PilotControlledRig rig = PilotRig(created, "w10-v2", MavF15PilotControlMode.AssistedV2);
            string reason;
            bool injected = Inject(rig.body, MavF15PilotTrimStart.TableViiPoint36(), out reason);
            MavFlightPhysicsOwnership gate = rig.gameObject.AddComponent<MavFlightPhysicsOwnership>();
            rig.body.physicsOwnership = gate;
            gate.AttachGovernedBody(rig.body);
            string e;
            bool shadow = gate.TryEnterShadow(out e);
            rig.body.NotifyOwnershipChanged();
            MavManualPilotCommandSource source = (MavManualPilotCommandSource)rig.commandSource;

            Vector3 neutral = MomentFor(rig, source, MavPilotCommand.Neutral, 0);
            MavF15SurfaceState atNeutral = rig.actuator.ActualF15SurfaceState.channels;
            float q = rig.body.debugState.dynamicPressurePa;
            MavAeroReferenceGeometry geo = MavF15BaumannMach06Reference.CreateReferenceGeometry();
            float scale = q * geo.wingAreaM2 * geo.meanAerodynamicChordM;
            Record(injected && shadow && rig.body.debugLoadSet.IsFinite() && rig.body.debugLoadApplications == 0
                   && Mathf.Abs(atNeutral.symmetricStabilatorDeg - Bias) < 1e-4f && Mathf.Abs(atNeutral.aileronDeg) < 1e-4f
                   && Mathf.Abs(atNeutral.rudderDeg) < 1e-4f && neutral.magnitude / scale < 1e-5f,
                "neutral: at the trim start V2 requests the trim bias (stab " + atNeutral.symmetricStabilatorDeg.ToString("R") + ") and ~zero lateral surfaces; |M|/qSc "
                + (neutral.magnitude / scale).ToString("E2") + "; nothing applied in shadow", report, ref passed, ref failed);

            Case(rig, source, neutral, "pitch +0.5", 0.5f, 0f, 0f, 1, +1, report, ref passed, ref failed);
            Case(rig, source, neutral, "pitch -0.5", -0.5f, 0f, 0f, 1, -1, report, ref passed, ref failed);
            Case(rig, source, neutral, "roll +0.5", 0f, 0.5f, 0f, 0, +1, report, ref passed, ref failed);
            Case(rig, source, neutral, "roll -0.5", 0f, -0.5f, 0f, 0, -1, report, ref passed, ref failed);
            Case(rig, source, neutral, "yaw +0.5", 0f, 0f, 0.5f, 2, +1, report, ref passed, ref failed);
            Case(rig, source, neutral, "yaw -0.5", 0f, 0f, -0.5f, 2, -1, report, ref passed, ref failed);
            gate.ReturnToLegacy("w10 done");
        }

        private static void Case(MavF15PilotControlledRig rig, MavManualPilotCommandSource source, Vector3 neutral, string label,
            float pitch, float roll, float yaw, int axis, int expectedSign, StringBuilder report, ref int passed, ref int failed)
        {
            rig.lawV2.ResetLawState();
            Vector3 dm = MomentFor(rig, source, new MavPilotCommand { pitch = pitch, roll = roll, yaw = yaw }, axis + 1) - neutral;
            float m = axis == 0 ? dm.x : axis == 1 ? dm.y : dm.z;
            string[] names = { "rolling moment L", "pitching moment M", "yawing moment N" };
            MavF15SurfaceState act = rig.actuator.ActualF15SurfaceState.channels;
            Record(Finite(dm.x) && Finite(dm.y) && Finite(dm.z) && Math.Sign(m) == expectedSign,
                label + ": surfaces stab " + act.symmetricStabilatorDeg.ToString("F2") + " ail " + act.aileronDeg.ToString("F2") + " diff "
                + act.differentialStabilatorDeg.ToString("F2") + " rud " + act.rudderDeg.ToString("F2") + " -> " + names[axis] + " "
                + m.ToString("E3") + " N m (" + (expectedSign > 0 ? "+" : "-") + " expected)", report, ref passed, ref failed);
        }

        private static Vector3 MomentFor(MavF15PilotControlledRig rig, MavManualPilotCommandSource source, MavPilotCommand command, int stepIndex)
        {
            source.command = command;
            rig.lawV2.StepPilotControlLaw(Dt);
            rig.actuator.StepActuator(Dt);
            rig.body.StepPhysicsForValidation(Dt, Dt * (stepIndex + 1));
            return rig.body.debugLoadSet.totalMomentAeroBodyNm;
        }

        // ---------------------------------------------------------------- [W12]

        /// <summary>Everything a refused law switch must leave untouched.</summary>
        private struct SwitchSnapshot
        {
            public MavF15PilotControlMode mode;
            public bool v1Enabled, v2Enabled;
            public MavFlightControlLawBase bound;
            public MavFlightPhysicsOwner owner;
            public string ownerReason;
            public bool armed;
            public MavF15SurfaceState actuatorRequest, actualSurfaces, v1Request, v2Request;
            public float v2Filter;

            public static SwitchSnapshot Of(MavF15PilotControlledRig rig, MavFlightPhysicsOwnership gate)
            {
                return new SwitchSnapshot
                {
                    mode = rig.controlMode,
                    v1Enabled = rig.law != null && rig.law.enabled,
                    v2Enabled = rig.lawV2 != null && rig.lawV2.enabled,
                    bound = rig.body.controlLaw,
                    owner = gate.owner,
                    ownerReason = gate.ownerReason,
                    armed = rig.body.ArmedForLiveFlight,
                    actuatorRequest = rig.actuator.requested.channels,
                    actualSurfaces = rig.actuator.ActualF15SurfaceState.channels,
                    v1Request = rig.law.debugRequested.channels,
                    v2Request = rig.lawV2 != null ? rig.lawV2.debugRequested.channels : MavF15SurfaceState.Neutral,
                    v2Filter = rig.lawV2 != null ? rig.lawV2.lawState.yawRateLowPassRadSec : 0f
                };
            }

            public bool SameAs(SwitchSnapshot o)
            {
                return mode == o.mode && v1Enabled == o.v1Enabled && v2Enabled == o.v2Enabled && bound == o.bound && owner == o.owner
                       && ownerReason == o.ownerReason && armed == o.armed && SameSurfaces(actuatorRequest, o.actuatorRequest)
                       && SameSurfaces(actualSurfaces, o.actualSurfaces) && SameSurfaces(v1Request, o.v1Request)
                       && SameSurfaces(v2Request, o.v2Request) && v2Filter.Equals(o.v2Filter);
            }
        }

        private static bool SameSurfaces(MavF15SurfaceState a, MavF15SurfaceState b)
        {
            for (int c = 0; c < 4; c++)
            {
                if (!a.Get((MavF15SurfaceChannel)c).Equals(b.Get((MavF15SurfaceChannel)c)))
                    return false;
            }

            return true;
        }

        private static double LargestSurfaceStep(MavF15SurfaceState a, MavF15SurfaceState b)
        {
            double worst = 0;
            for (int c = 0; c < 4; c++)
                worst = Math.Max(worst, Math.Abs(b.Get((MavF15SurfaceChannel)c) - a.Get((MavF15SurfaceChannel)c)));
            return worst;
        }

        /// <summary>A refused attempt: false return, the expected reason, and a snapshot identical before and after.</summary>
        private static bool RefusedUnchanged(MavF15PilotControlledRig rig, MavFlightPhysicsOwnership gate, MavF15PilotControlMode request,
            string expectedReasonPrefix, out string reason)
        {
            SwitchSnapshot before = SwitchSnapshot.Of(rig, gate);
            bool refused = !rig.TrySetControlMode(request, out reason);
            return refused && reason != null && reason.StartsWith(expectedReasonPrefix, StringComparison.Ordinal)
                   && SwitchSnapshot.Of(rig, gate).SameAs(before);
        }

        /// <summary>One live step of the bound law, the actuator and the body (edit mode: the validation seam, a fresh step time).</summary>
        private static void LiveStep(MavF15PilotControlledRig rig, ref float fixedTime)
        {
            fixedTime += Dt;
            if (rig.controlMode == MavF15PilotControlMode.AssistedV2)
                rig.lawV2.StepPilotControlLaw(Dt);
            else
                rig.law.StepPilotControlLaw(Dt);
            rig.actuator.StepActuator(Dt);
            rig.body.StepPhysicsForValidation(Dt, fixedTime);
        }

        /// <summary>Steps the bound law live and reports whether every step applied exactly one load, granted, with one law enabled.</summary>
        private static bool LiveStepsClean(MavF15PilotControlledRig rig, MavFlightPhysicsOwnership gate, int steps, ref float fixedTime,
            out int applied, out double largestSurfaceStep)
        {
            int loads0 = rig.body.debugLoadApplications, dup0 = rig.body.debugRejectedDuplicateApplications;
            bool ok = true;
            largestSurfaceStep = 0;
            MavF15SurfaceState prev = rig.actuator.ActualF15SurfaceState.channels;
            for (int i = 0; i < steps; i++)
            {
                LiveStep(rig, ref fixedTime);
                MavF15SurfaceState now = rig.actuator.ActualF15SurfaceState.channels;
                largestSurfaceStep = Math.Max(largestSurfaceStep, LargestSurfaceStep(prev, now));
                prev = now;
                string r;
                int enabled = (rig.law.enabled ? 1 : 0) + (rig.lawV2 != null && rig.lawV2.enabled ? 1 : 0);
                ok &= enabled == 1 && rig.body.controlLaw == rig.ActiveLaw && rig.body.debugLoadApplications == loads0 + i + 1
                      && gate.owner == MavFlightPhysicsOwner.F15PilotControlledResearch && rig.body.ArmedForLiveFlight
                      && MavF15PilotControlledOwnershipGrant.Instance.TryGrantResearchOwnership(rig.body, out r)
                      && rig.body.debugLoadSet.IsFinite() && now.IsFinite();
            }

            applied = rig.body.debugLoadApplications - loads0;
            return ok && rig.body.debugRejectedDuplicateApplications == dup0;
        }

        private static void ValidateSwitchFailClosed(List<Object> created, StringBuilder report, ref int passed, ref int failed)
        {
            float tol = MavF15PilotControlledRig.SwitchCentredTolerance;
            report.AppendLine();
            report.AppendLine("[W12] The in-flight law switch fails closed (proceeds only with a wired, active and enabled command source whose "
                              + "current command reads, is finite and is within " + tol + " on pitch, roll and yaw)");
            MavF15PilotControlledRig rig = PilotRig(created, "w12-v2", MavF15PilotControlMode.AssistedV2);
            string reason;
            bool injected = Inject(rig.body, MavF15PilotTrimStart.TableViiPoint36(), out reason);
            MavFlightPhysicsOwnership gate = rig.gameObject.AddComponent<MavFlightPhysicsOwnership>();
            rig.body.physicsOwnership = gate;
            gate.AttachGovernedBody(rig.body);
            rig.body.NotifyOwnershipChanged();
            gate.allowF15PilotControlledOwnership = true;
            bool entered = gate.TryEnterF15PilotControlledOwnership(rig.body, MavF15PilotControlledOwnershipGrant.Instance, out reason);
            MavManualPilotCommandSource source = (MavManualPilotCommandSource)rig.commandSource;
            source.command = MavPilotCommand.Neutral;

            float fixedTime = 0f;
            int loads0 = rig.body.debugLoadApplications;
            LiveStep(rig, ref fixedTime);
            rig.lawV2.lawState.yawRateLowPassRadSec = 0.123f;   // distinctive: any accepted switch would zero it
            SwitchSnapshot s0 = SwitchSnapshot.Of(rig, gate);
            Record(injected && entered && s0.armed && s0.owner == MavFlightPhysicsOwner.F15PilotControlledResearch && s0.mode == MavF15PilotControlMode.AssistedV2
                   && rig.body.debugLoadApplications == loads0 + 1,
                "set-up: V2 rig at the trim, in F15PilotControlledResearch (armed), one live step applied; a distinctive V2 filter state marks any reset",
                report, ref passed, ref failed);

            const MavF15PilotControlMode toV1 = MavF15PilotControlMode.DirectV1;
            string ra, rb1, rb2, rc, rn, r1, r2;

            // A. no command source wired.
            rig.commandSource = null;
            bool a = RefusedUnchanged(rig, gate, toV1, MavF15PilotControlledRig.SwitchRefusedNoCommandSource, out ra);
            rig.commandSource = source;
            Record(a, "A  command source missing -> refused, nothing changed: \"" + ra + "\"", report, ref passed, ref failed);

            // B. a command source that exists but is disabled, or on an inactive GameObject.
            source.enabled = false;
            bool b1 = RefusedUnchanged(rig, gate, toV1, MavF15PilotControlledRig.SwitchRefusedCommandSourceInactive, out rb1);
            source.enabled = true;
            GameObject inactiveHost = new GameObject("w12-inactive-source");
            inactiveHost.hideFlags = HideFlags.HideAndDontSave;
            created.Add(inactiveHost);
            inactiveHost.SetActive(false);
            MavManualPilotCommandSource parked = inactiveHost.AddComponent<MavManualPilotCommandSource>();
            parked.treatAsOperationalSource = true;
            rig.commandSource = parked;
            bool b2 = RefusedUnchanged(rig, gate, toV1, MavF15PilotControlledRig.SwitchRefusedCommandSourceInactive, out rb2);
            rig.commandSource = source;
            Record(b1, "B  command source disabled -> refused, nothing changed: \"" + rb1 + "\"", report, ref passed, ref failed);
            Record(b2, "B  command source on an inactive GameObject -> refused, nothing changed", report, ref passed, ref failed);

            // C. an active source whose TryGetCommand fails; and a command that reads but is not finite.
            source.commandAvailable = false;
            bool c = RefusedUnchanged(rig, gate, toV1, MavF15PilotControlledRig.SwitchRefusedCommandUnavailable, out rc);
            source.commandAvailable = true;
            Record(c, "C  active source, TryGetCommand fails -> refused, nothing changed: \"" + rc + "\"", report, ref passed, ref failed);
            source.command = new MavPilotCommand { pitch = float.NaN };
            bool nan = RefusedUnchanged(rig, gate, toV1, MavF15PilotControlledRig.SwitchRefusedCommandNotFinite, out rn);
            string rinf;
            source.command = new MavPilotCommand { yaw = float.PositiveInfinity };
            bool inf = RefusedUnchanged(rig, gate, toV1, "law switch refused: ", out rinf);
            Record(nan && inf, "C  a NaN axis -> refused as not finite (\"" + rn + "\"; before this fix NaN passed the centred test); an infinite axis "
                + "(the manual source clamps it to full deflection) -> refused (\"" + rinf + "\"); nothing changed either time",
                report, ref passed, ref failed);

            // D, E, F. each axis just outside the tolerance, both signs, the other axes centred.
            float over = tol + 0.001f;
            string n = MavF15PilotControlledRig.SwitchRefusedNotCentred;
            string[] axes = { "D  pitch", "E  roll", "F  yaw" };
            for (int axis = 0; axis < 3; axis++)
            {
                bool both = true;
                foreach (float sign in new[] { 1f, -1f })
                {
                    MavPilotCommand cmd = MavPilotCommand.Neutral;
                    if (axis == 0) cmd.pitch = sign * over;
                    else if (axis == 1) cmd.roll = sign * over;
                    else cmd.yaw = sign * over;
                    source.command = cmd;
                    string rr;
                    both &= RefusedUnchanged(rig, gate, toV1, n, out rr);
                }

                Record(both, axes[axis] + " at +/-" + over.ToString("R") + " (outside " + tol + ") -> refused, nothing changed", report, ref passed, ref failed);
            }

            // I. after every refusal: nothing moved since the set-up - mode, bound law, enabled laws, law state, requests, surfaces, ownership.
            source.command = MavPilotCommand.Neutral;
            Record(SwitchSnapshot.Of(rig, gate).SameAs(s0) && rig.commandSource == source && rig.lawV2.lawState.yawRateLowPassRadSec == 0.123f,
                "I  after the 12 refused attempts: controlMode, body.controlLaw, both laws' enabled states, the V2 filter state, the actuator request, "
                + "the surfaces, both laws' requests, the owner, its reason and the arming are exactly as before", report, ref passed, ref failed);

            // H. the requested mode is already bound and active: success with no command read (source silent AND deflected).
            source.commandAvailable = false;
            source.command = new MavPilotCommand { roll = 1f };
            bool already = rig.TrySetControlMode(MavF15PilotControlMode.AssistedV2, out r1) && SwitchSnapshot.Of(rig, gate).SameAs(s0);
            // ...but not when "already" is not valid: V1 enabled next to the bound V2 is not a bound-and-active mode.
            rig.law.enabled = true;
            SwitchSnapshot invalid = SwitchSnapshot.Of(rig, gate);
            bool notFooled = !rig.TrySetControlMode(MavF15PilotControlMode.AssistedV2, out r2) && SwitchSnapshot.Of(rig, gate).SameAs(invalid);
            source.commandAvailable = true;
            source.command = MavPilotCommand.Neutral;
            bool repaired = rig.TrySetControlMode(MavF15PilotControlMode.AssistedV2, out r2) && !rig.law.enabled && rig.lawV2.enabled
                            && rig.body.controlLaw == rig.lawV2 && rig.lawV2.lawState.yawRateLowPassRadSec == 0f;
            Record(already && r1 == "already AssistedV2",
                "H  requesting the already bound and active mode succeeds with the source silent and the stick deflected, and disturbs nothing (\"" + r1 + "\")",
                report, ref passed, ref failed);
            Record(notFooled && repaired,
                "H  the fast path is not taken when the other law is also enabled: refused while the source is silent, then with a centred source the "
                + "switch path runs and leaves exactly one law enabled", report, ref passed, ref failed);

            // G + J. every axis exactly at the tolerance -> accepted; one law, one load per step, no gap, both directions.
            int applied;
            double step;
            bool warm = LiveStepsClean(rig, gate, 3, ref fixedTime, out applied, out step) && applied == 3;
            MavF15SurfaceState beforeV1 = rig.actuator.ActualF15SurfaceState.channels;
            source.command = new MavPilotCommand { pitch = tol, roll = -tol, yaw = tol, throttle01 = 1f };
            bool toV1Ok = rig.TrySetControlMode(toV1, out r1);
            bool v1Bound = rig.controlMode == toV1 && rig.law.enabled && !rig.lawV2.enabled && rig.body.controlLaw == rig.law
                           && gate.owner == MavFlightPhysicsOwner.F15PilotControlledResearch && rig.body.ArmedForLiveFlight;
            Record(toV1Ok && v1Bound, "G  pitch +" + tol + ", roll -" + tol + ", yaw +" + tol + " (exactly at the tolerance, throttle ignored) -> V2 to V1 accepted: \""
                + r1 + "\"; V1 enabled and bound, V2 disabled, owner and arming kept", report, ref passed, ref failed);

            source.command = MavPilotCommand.Neutral;
            bool cleanV1 = LiveStepsClean(rig, gate, 5, ref fixedTime, out applied, out step);
            double jumpV1 = LargestSurfaceStep(beforeV1, rig.actuator.ActualF15SurfaceState.channels);
            int appliedV1 = applied;

            MavF15SurfaceState beforeV2 = rig.actuator.ActualF15SurfaceState.channels;
            source.command = new MavPilotCommand { pitch = -tol, roll = tol, yaw = -tol };
            bool toV2Ok = rig.TrySetControlMode(MavF15PilotControlMode.AssistedV2, out r2);
            bool v2Bound = rig.lawV2.enabled && !rig.law.enabled && rig.body.controlLaw == rig.lawV2 && rig.lawV2.lawState.yawRateLowPassRadSec == 0f;
            source.command = MavPilotCommand.Neutral;
            bool cleanV2 = LiveStepsClean(rig, gate, 5, ref fixedTime, out applied, out step);
            double jumpV2 = LargestSurfaceStep(beforeV2, rig.actuator.ActualF15SurfaceState.channels);
            Record(warm && cleanV1 && appliedV1 == 5 && toV2Ok && v2Bound && cleanV2 && applied == 5 && Math.Max(jumpV1, jumpV2) < 1.0,
                "J  after each accepted switch (V2 -> V1, then V1 -> V2 at the opposite tolerance corner): 5 live steps each, exactly one law enabled and bound, "
                + "granted, one load application per step (" + appliedV1 + " + " + applied + "), no duplicate refusal, finite; largest surface change across a switch "
                + Math.Max(jumpV1, jumpV2).ToString("F3") + " deg", report, ref passed, ref failed);
            gate.ReturnToLegacy("w12 done");
        }

        // ---------------------------------------------------------------- [W11]

        private static readonly string[] V2Files =
        {
            "F15/MavF15PilotControlLawV2.cs", "F15/MavF15PilotControlGainsV2.cs", "F15/MavF15PilotControlledRig.cs",
            "F15/MavF15PilotControlledHud.cs", "F15/MavF15PilotControlledDiagnostics.cs", "F15/MavF15PilotControlApproximation.cs",
            "F15/MavF15PilotControlledAuthority.cs"
        };

        private static void ValidateSourceScan(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[W11] Source scan of the V2 layer (" + V2Files.Length + " files)");
            string root = Path.Combine(Application.dataPath, MavFlightDynamicsOwnershipScan.FlightDynamicsRelativePath);
            string[] writes = { "AddForce", "AddTorque", "AddRelativeForce", "AddRelativeTorque", "AddForceAtPosition", "AddExplosionForce",
                                "velocity =", "Velocity =", "MovePosition(", "MoveRotation(", ".position =", ".rotation =", "angularDamping", "linearDamping" };
            string[] arming = { "simulationEnabled =", "ArmedForLiveFlight =" };
            string[] forbidden = { "MavF15AfitResearchTrimSolver", "MavF15AfitResearchSourceEnvironment", "MavF15AfitResearchSourceDynamics",
                                   "MavValidation", "MavF15BaumannTableVii", "MavF15TableVii", "MavF100", "MavF15ResearchStaticControlState", "MavF15ResearchRuntimeAuthority",
                                   "FromActuator(", "new MavF15ResearchDemonstratedControlRange", "new MavF15PhysicalSurfaceHardStops",
                                   "new MavF15ActuatorRateLimits", "MavF15HardStop", "MavF15RateLimit" };
            string wrote = null, armed = null, named = null, f16 = null;
            int scanned = 0;
            foreach (string rel in V2Files)
            {
                string full = Path.Combine(root, rel);
                if (!File.Exists(full))
                {
                    named = rel + " (missing)";
                    continue;
                }

                scanned++;
                bool v2Only = rel.Contains("V2");
                foreach (string line in File.ReadAllLines(full))
                {
                    string code = StripComment(line);
                    foreach (string t in writes)
                        if (code.IndexOf(t, StringComparison.Ordinal) >= 0) wrote = rel + ": " + code.Trim();
                    foreach (string t in arming)
                        if (code.IndexOf(t, StringComparison.Ordinal) >= 0) armed = rel + ": " + code.Trim();
                    foreach (string t in forbidden)
                        if (code.IndexOf(t, StringComparison.Ordinal) >= 0) named = rel + ": " + t;
                    if (v2Only && code.IndexOf("MavF16", StringComparison.Ordinal) >= 0) f16 = rel + ": " + code.Trim();
                }
            }

            Record(scanned == V2Files.Length && wrote == null,
                wrote == null ? "no Rigidbody write, force, torque or damping in any V2-layer file: the law requests surfaces, the body applies the one load set" : "VIOLATION " + wrote,
                report, ref passed, ref failed);
            Record(armed == null,
                armed == null ? "no V2-layer file arms a body: the ownership authority stays the single arming authority" : "VIOLATION " + armed,
                report, ref passed, ref failed);
            Record(named == null && f16 == null,
                named == null && f16 == null
                    ? "none names the research trim / source RHS / validation types / Table VII / F100 / research authority types; the V2 law and gains name no F-16 type (architecture only, in comments)"
                    : "VIOLATION " + (named ?? f16), report, ref passed, ref failed);
        }

        private static string StripComment(string line)
        {
            int c = line.IndexOf("//", StringComparison.Ordinal);
            return c >= 0 ? line.Substring(0, c) : line;
        }

        private static bool Finite(float v)
        {
            return !float.IsNaN(v) && !float.IsInfinity(v);
        }

        private static void Record(bool condition, string label, StringBuilder report, ref int passed, ref int failed)
        {
            if (condition)
                passed++;
            else
                failed++;
            report.Append(condition ? "  PASS  " : "  FAIL  ").AppendLine(label);
        }
    }
}
#endif
