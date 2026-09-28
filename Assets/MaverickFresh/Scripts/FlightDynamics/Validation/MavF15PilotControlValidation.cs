#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using MaverickFresh.FlightDynamics;
using MaverickFresh.FlightDynamics.F15;
using Object = UnityEngine.Object;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>
    /// F-15 PILOT-CONTROLLED research aircraft V1 - headless validation (no flight; the Play Mode
    /// flight test is MavF15PilotControlledFlightValidationRunner).
    ///
    /// Every rig is a HideAndDontSave GameObject; no scene or prefab is opened or saved. Body loads are
    /// computed through the body's own SHADOW path (compute everything, apply nothing) via the editor
    /// StepPhysicsForValidation seam. Pass criteria are signs, finiteness, continuity, monotonicity and
    /// bit-equality with the frozen research path - never "looks right".
    ///
    ///   [P1]  identity: separate, labelled, not NASA 836, not the production FCS
    ///   [P2]  authority separation: the pilot id and the frozen research id never grant each other
    ///   [P3]  every gameplay value MAVERICK_TUNED_NON_AUTHORITATIVE; research authority types unchanged
    ///   [P4]  coefficient audit of the gameplay envelopes: control-moment signs, finite, monotonic
    ///   [P5]  mapping: neutral = trim, pitch/roll/yaw signs, combined, envelope, continuity, NaN, throttle
    ///   [P6]  trim start = the WP-3B solve (bit for bit), in trim, STABLE
    ///   [P7]  environment and thrust equal the frozen research path bit for bit; throttle inactive
    ///   [P8]  aerodynamics equal MavF15AeroModel bit for bit; fail-closed refusals
    ///   [P9]  neutral hold / +-pitch / +-roll / +-yaw / combined through the real rig: moment and rate signs
    ///   [P10] ownership: F15PilotControlledResearch rules, safety hold, refusals, entry, exit
    ///   [P11] source scan: no Rigidbody writes, no arming, no research-only or validation types, no F100
    ///   [P12] keyboard source: axes, shaping, operational declaration
    /// </summary>
    public static class MavF15PilotControlValidation
    {
        private const float Dt = 0.02f;
        private const float DegToRad = Mathf.PI / 180f;

        /// <summary>Alpha values the gameplay envelopes are audited at: around the trim (17.49 deg) and across the practical band.</summary>
        public static readonly float[] AuditAlphaDeg = { 8f, 10f, 12f, 14f, 16f, 17.4864559f, 18f, 20f, 22f, 25f };

        public static string RunAll(out int passed, out int failed)
        {
            passed = 0;
            failed = 0;
            StringBuilder report = new StringBuilder(32 * 1024);
            report.AppendLine("F-15 PILOT-CONTROLLED RESEARCH AIRCRAFT V1 - headless validation (" + MavF15PilotControlledIdentity.ConfigurationId + ")");
            report.AppendLine("Research aerodynamics + Maverick control approximation. Not NASA 836, not the production F-15 FCS. Nothing is flown here.");

            List<Object> created = new List<Object>();
            try
            {
                ValidateIdentity(report, ref passed, ref failed);
                ValidateAuthoritySeparation(report, ref passed, ref failed);
                ValidateProvenance(report, ref passed, ref failed);
                ValidateCoefficientAudit(report, ref passed, ref failed);
                ValidateMapping(report, ref passed, ref failed);
                ValidateTrimStart(report, ref passed, ref failed);
                ValidateEnvironmentAndThrust(created, report, ref passed, ref failed);
                ValidateAerodynamics(created, report, ref passed, ref failed);
                ValidateSignsThroughRig(created, report, ref passed, ref failed);
                ValidateOwnership(created, report, ref passed, ref failed);
                ValidateSourceScan(report, ref passed, ref failed);
                ValidateKeyboard(created, report, ref passed, ref failed);
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

        // ---------------------------------------------------------------- [P1]

        private static void ValidateIdentity(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P1] Identity");
            string id = MavF15PilotControlledIdentity.ConfigurationId;
            Record(id == "F15_AFIT_BAUMANN_DAVISON_PILOT_CONTROLLED_V1" && id != MavF15AfitResearchIdentity.ConfigurationId
                   && id != MavF15ReferenceData.TargetConfigurationId && MavF15AfitResearchIdentity.CarriesNoExactTargetToken(id)
                   && MavFlightPhysicsOwnership.F15PilotControlledConfigurationId == id,
                "a separate pilot-controlled id (" + id + "): not the frozen research id, not NASA 836, no exact-target token; the ownership authority names the same id",
                report, ref passed, ref failed);
            Record(MavF15PilotControlledIdentity.UnderlyingAerodynamics.Contains(MavF15AfitResearchIdentity.ConfigurationId)
                   && MavF15PilotControlledIdentity.PilotControlLayer.Contains("Maverick-owned")
                   && MavF15PilotControlledIdentity.NotClaimed.Contains("NOT NASA F-15B 836")
                   && MavF15PilotControlledIdentity.NotClaimed.Contains("NOT the production F-15 FCS")
                   && MavF15PilotControlledIdentity.NotClaimed.Contains("NOT source-authoritative actuator")
                   && MavF15PilotControlLaw.LawName.Contains("NOT F-15 CAS / FLCS, NOT NASA 836"),
                "labelled: underlying aero = the frozen research model; control layer = Maverick-owned; NOT NASA 836, NOT the production FCS, NOT source-authoritative actuator behaviour; the law is named a Maverick law",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [P2]

        private static void ValidateAuthoritySeparation(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P2] Authority separation");
            string pilot = MavF15PilotControlledIdentity.ConfigurationId;
            string research = MavF15AfitResearchIdentity.ConfigurationId;
            string r;
            Record(MavF15PilotControlledAuthority.TryGrant(pilot, out r), "the pilot authority grants " + pilot, report, ref passed, ref failed);

            string[] refused = { research, MavF15ReferenceData.TargetConfigurationId, pilot + " ", pilot.ToLowerInvariant(), pilot + "_EXACT",
                                 pilot.Replace("V1", "V2"), "f16-morelli-clean-subsonic-v0.1", "", null };
            int n = 0;
            string researchReason = null;
            foreach (string s in refused)
            {
                if (!MavF15PilotControlledAuthority.TryGrant(s, out r))
                    n++;
                if (s == research)
                    researchReason = r;
            }

            Record(n == refused.Length && researchReason != null && researchReason.Contains("frozen research VALIDATION configuration"),
                "the pilot authority refuses " + n + "/" + refused.Length + " ids - the frozen research id by name (" + researchReason + "), NASA 836, look-alikes, the F-16, empty, null",
                report, ref passed, ref failed);
            Record(!MavF15ResearchRuntimeAuthority.TryGrant(pilot, out r) && MavF15ResearchRuntimeAuthority.TryGrant(research, out r),
                "the frozen research runtime authority is unchanged: it still grants only the research id and refuses the pilot id",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [P3]

        private static void ValidateProvenance(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P3] Provenance of every control value; the research authority types are unchanged");
            MavF15GameplayControlAuthority a = MavF15GameplayControlAuthority.V1();
            string reason;
            bool labelled = true;
            for (int i = 0; i < 4; i++)
                labelled &= a.Get((MavF15SurfaceChannel)i).basis.StartsWith(MavF15PilotControlProvenance.MaverickTunedNonAuthoritative + ": ", StringComparison.Ordinal);
            Record(a.IsSelfConsistent(out reason) && labelled && MavF15GameplayControlAuthority.Provenance == "MAVERICK_TUNED_NON_AUTHORITATIVE",
                "the gameplay control authority is self-consistent and every envelope is labelled MAVERICK_TUNED_NON_AUTHORITATIVE", report, ref passed, ref failed);

            report.AppendLine("      gearing (deg / unit): pitch " + a.pitchStabilatorDegPerUnit + ", roll " + a.rollAileronDegPerUnit + ", yaw " + a.yawRudderDegPerUnit
                              + "  |  envelopes: stab " + a.symmetricStabilator.minDeg + ".." + a.symmetricStabilator.maxDeg
                              + ", diff " + a.differentialStabilator.minDeg + ".." + a.differentialStabilator.maxDeg
                              + ", ail " + a.aileron.minDeg + ".." + a.aileron.maxDeg + ", rud " + a.rudder.minDeg + ".." + a.rudder.maxDeg
                              + "  (all " + MavF15GameplayControlAuthority.Provenance + ")");
            report.AppendLine("      sourced research facts used: differential tail = " + MavF15PilotControlConventions.DifferentialTailPerCommandedAileron
                              + " x commanded aileron (" + MavF15PilotControlConventions.DifferentialTailCitation + "); surface sign conventions ("
                              + MavF15ResearchControlConventions.Citation + ")");

            MavF15SurfaceLimits travel = a.ToActuatorTravel();
            bool travelHonest = true;
            for (int i = 0; i < 4; i++)
            {
                MavF15SurfaceChannelLimits c = travel.Get((MavF15SurfaceChannel)i);
                MavF15GameplayChannelEnvelope e = a.Get((MavF15SurfaceChannel)i);
                travelHonest &= c.travelProvenance == MavEngineDataProvenance.MaverickTuning && c.rateProvenance == MavEngineDataProvenance.Unavailable
                                && c.rateLimitDegSec == 0f && !c.HasSourcedRate && c.minDeg == e.minDeg && c.maxDeg == e.maxDeg
                                && c.sourceNote.StartsWith(MavF15PilotControlProvenance.MaverickTunedNonAuthoritative, StringComparison.Ordinal)
                                && c.IsSelfConsistent(out reason);
            }

            Record(travelHonest && MavF15PhysicalSurfaceHardStops.FromActuatorLimits(travel).symmetricStabilator.provenance == MavEngineDataProvenance.MaverickTuning,
                "the actuator travel the gameplay layer needs is graded MaverickTuning (never PublicReference / Authoritative), labelled, with NO actuator rate: no invented actuator dynamics",
                report, ref passed, ref failed);

            MavF15ResearchDemonstratedControlRange range = MavF15ResearchDemonstratedControlRange.AfitBaumannTabulatedEquilibria();
            Record(range.symmetricStabilator.minDeg == -25f && range.symmetricStabilator.maxDeg == -5f && !range.symmetricStabilator.IsPhysicalLimit
                   && range.aileron.minDeg == 0f && range.aileron.maxDeg == 0f && range.rudder.minDeg == 0f && range.rudder.maxDeg == 0f
                   && range.differentialStabilator.minDeg == 0f && range.differentialStabilator.maxDeg == 0f
                   && range.configurationId == MavF15AfitResearchIdentity.ConfigurationId,
                "MavF15ResearchDemonstratedControlRange unchanged: stabilator -25..-5 (still NOT a physical limit), lateral [0, 0], research id only",
                report, ref passed, ref failed);
            Record(!MavF15PhysicalSurfaceHardStops.ExactTarget().AnyDeclared && !MavF15ActuatorRateLimits.ExactTarget().AnyDeclared
                   && MavF15SurfaceLimits.UnavailableExactTarget().AvailableChannelCount == 0,
                "MavF15PhysicalSurfaceHardStops and MavF15ActuatorRateLimits still declare nothing; the actuator's default limits still refuse every channel",
                report, ref passed, ref failed);
            MavControlSurfaceLimits research = MavF15AfitResearchFlightDynamicsProfile.CreateUnavailableResearchControlLimits();
            Record(research.elevatorMinDeg == 0f && research.elevatorMaxDeg == 0f && research.aileronMaxDeg == 0f && research.rudderMaxDeg == 0f,
                "the frozen research profile still carries zero surface travel: the gameplay envelopes did not leak into the research configuration",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [P4]

        private static void ValidateCoefficientAudit(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P4] Coefficient audit of the gameplay envelopes (research routines, beta 0, zero rates)");
            report.AppendLine("      per alpha: control derivative sign over the whole envelope, and whether the moment is finite and strictly monotonic in the deflection");
            MavF15GameplayControlAuthority a = MavF15GameplayControlAuthority.V1();
            float trim = (float)MavF15PilotTrimStart.TableViiPoint36().symmetricStabilatorDeg;

            bool pitchOk = true, rollOk = true, yawOk = true;
            StringBuilder cross = new StringBuilder();
            foreach (float alphaDeg in AuditAlphaDeg)
            {
                float al = alphaDeg * DegToRad;
                Sweep pitch = SweepChannel(a.symmetricStabilator.minDeg, a.symmetricStabilator.maxDeg,
                    d => MavF15BaumannMach06Longitudinal.Evaluate(al, d, 0f).cm);
                Sweep roll = SweepChannel(a.aileron.minDeg, a.aileron.maxDeg,
                    d => Lateral(al, trim, d, MavF15PilotControlConventions.DifferentialTailPerCommandedAileron * d, 0f).cl);
                Sweep yaw = SweepChannel(a.rudder.minDeg, a.rudder.maxDeg,
                    d => Lateral(al, trim, 0f, 0f, d).cn);

                pitchOk &= pitch.finite && pitch.monotonic && pitch.sign < 0;
                rollOk &= roll.finite && roll.monotonic && roll.sign > 0;
                yawOk &= yaw.finite && yaw.monotonic && yaw.sign < 0;

                float adverse = Lateral(al, trim, 10f, 3f, 0f).cn - Lateral(al, trim, 0f, 0f, 0f).cn;
                float rudderRoll = Lateral(al, trim, 0f, 0f, 10f).cl - Lateral(al, trim, 0f, 0f, 0f).cl;
                report.AppendLine("      alpha " + alphaDeg.ToString("F2").PadLeft(6)
                                  + "  dCm/dstab " + pitch.Describe() + "  dCl/dail(+0.3 diff) " + roll.Describe() + "  dCn/drud " + yaw.Describe());
                cross.AppendLine("      alpha " + alphaDeg.ToString("F2").PadLeft(6) + "  Cn from +10 deg aileron (+3 diff) "
                                 + adverse.ToString("+0.00000;-0.00000") + "  |  Cl from +10 deg rudder " + rudderRoll.ToString("+0.00000;-0.00000"));
            }

            report.Append("    cross-coupling, recorded not judged:\n").Append(cross);
            Record(pitchOk, "stabilator: over the whole gameplay envelope and every audited alpha, Cm is finite and strictly DEcreasing in stabilator (positive = nose-down, as Baumann states)", report, ref passed, ref failed);
            Record(rollOk, "aileron with the research 0.3 differential tail: Cl finite and strictly INcreasing in aileron - positive aileron rolls right in the model that is flown", report, ref passed, ref failed);
            Record(yawOk, "rudder: Cn finite and strictly DEcreasing in rudder (positive = trailing edge left = nose left, as Baumann states)", report, ref passed, ref failed);
            Record(MavF15PilotControlConventions.NoseUpStabilatorSign < 0f && MavF15PilotControlConventions.RollRightAileronSign > 0f
                   && MavF15PilotControlConventions.YawNoseRightRudderSign < 0f,
                "the mapping's sign conventions are the ones the audit measured (nose-up -> negative stabilator, right roll -> positive aileron, nose-right -> negative rudder)",
                report, ref passed, ref failed);
        }

        private struct Sweep
        {
            public bool finite;
            public bool monotonic;
            public int sign;
            public float minSlope;
            public float maxSlope;

            public string Describe()
            {
                return (finite ? "" : "NON-FINITE ") + (monotonic ? "" : "NON-MONOTONIC ")
                       + minSlope.ToString("+0.000E+0;-0.000E+0") + ".." + maxSlope.ToString("+0.000E+0;-0.000E+0") + " /deg";
            }
        }

        private static Sweep SweepChannel(float min, float max, Func<float, float> f)
        {
            const float step = 0.25f;
            Sweep s = new Sweep { finite = true, monotonic = true, sign = 0, minSlope = float.MaxValue, maxSlope = float.MinValue };
            float previous = f(min);
            if (!Finite(previous))
                s.finite = false;
            for (float d = min + step; d <= max + 1e-4f; d += step)
            {
                float v = f(d);
                if (!Finite(v))
                {
                    s.finite = false;
                    continue;
                }

                float slope = (v - previous) / step;
                s.minSlope = Mathf.Min(s.minSlope, slope);
                s.maxSlope = Mathf.Max(s.maxSlope, slope);
                int sign = slope > 0f ? 1 : slope < 0f ? -1 : 0;
                if (sign == 0 || (s.sign != 0 && sign != s.sign))
                    s.monotonic = false;
                if (s.sign == 0)
                    s.sign = sign;
                previous = v;
            }

            return s;
        }

        private static MavAeroCoefficients Lateral(float alphaRad, float stab, float aileron, float differential, float rudder)
        {
            return MavF15BaumannMach06LateralDirectional.Evaluate(alphaRad, 0f, new MavF15BaumannSurfaceState
            {
                symmetricStabilatorDeg = stab,
                aileronDeg = aileron,
                differentialTailDeg = differential,
                rudderDeg = rudder
            }, 0f, 0f);
        }

        // ---------------------------------------------------------------- [P5]

        private static void ValidateMapping(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P5] Stick-to-surface mapping (pure)");
            MavF15GameplayControlAuthority a = MavF15GameplayControlAuthority.V1();
            float bias = (float)MavF15PilotTrimStart.TableViiPoint36().symmetricStabilatorDeg;

            MavF15PilotControlSolution n = MavF15PilotControlMapping.Solve(MavPilotCommand.Neutral, a, bias);
            Record(n.requested.symmetricStabilatorDeg == bias && n.requested.aileronDeg == 0f && n.requested.differentialStabilatorDeg == 0f
                   && n.requested.rudderDeg == 0f && !n.AnyLimited,
                "neutral stick requests exactly the trim stabilator " + bias.ToString("R") + " deg (a trim bias, not a physical neutral) and zero lateral surfaces",
                report, ref passed, ref failed);

            MavF15PilotControlSolution pUp = Solve(a, bias, 0.5f, 0f, 0f), pDn = Solve(a, bias, -0.5f, 0f, 0f);
            MavF15PilotControlSolution rR = Solve(a, bias, 0f, 0.5f, 0f), rL = Solve(a, bias, 0f, -0.5f, 0f);
            MavF15PilotControlSolution yR = Solve(a, bias, 0f, 0f, 0.5f), yL = Solve(a, bias, 0f, 0f, -0.5f);
            Record(pUp.requested.symmetricStabilatorDeg < bias && pDn.requested.symmetricStabilatorDeg > bias
                   && pUp.requested.aileronDeg == 0f && pUp.requested.rudderDeg == 0f,
                "pitch +0.5 -> stabilator " + pUp.requested.symmetricStabilatorDeg.ToString("F3") + " (nose-up side of trim); pitch -0.5 -> "
                + pDn.requested.symmetricStabilatorDeg.ToString("F3") + "; no lateral output", report, ref passed, ref failed);
            Record(rR.requested.aileronDeg > 0f && rL.requested.aileronDeg < 0f
                   && Mathf.Abs(rR.requested.differentialStabilatorDeg - 0.3f * rR.requested.aileronDeg) <= 1e-5f
                   && rR.requested.symmetricStabilatorDeg == bias && rR.requested.rudderDeg == 0f,
                "roll +0.5 -> aileron " + rR.requested.aileronDeg.ToString("F3") + " + differential tail " + rR.requested.differentialStabilatorDeg.ToString("F3")
                + " (0.3 x, research relation); roll -0.5 -> " + rL.requested.aileronDeg.ToString("F3") + "; pitch and rudder untouched", report, ref passed, ref failed);
            Record(yR.requested.rudderDeg < 0f && yL.requested.rudderDeg > 0f && yR.requested.aileronDeg == 0f && yR.requested.symmetricStabilatorDeg == bias,
                "yaw +0.5 -> rudder " + yR.requested.rudderDeg.ToString("F3") + " (trailing edge right, nose right); yaw -0.5 -> "
                + yL.requested.rudderDeg.ToString("F3"), report, ref passed, ref failed);

            MavF15PilotControlSolution c = Solve(a, bias, 0.4f, 0.4f, 0f);
            Record(c.requested.symmetricStabilatorDeg == Solve(a, bias, 0.4f, 0f, 0f).requested.symmetricStabilatorDeg
                   && c.requested.aileronDeg == Solve(a, bias, 0f, 0.4f, 0f).requested.aileronDeg && !c.AnyLimited,
                "combined moderate pitch + roll (0.4, 0.4) is the superposition of the two - no hidden cross-channel mixing", report, ref passed, ref failed);

            MavF15PilotControlSolution full = Solve(a, bias, 1f, 1f, 1f), fullNeg = Solve(a, bias, -1f, -1f, -1f);
            bool inside = true;
            foreach (MavF15PilotControlSolution s in new[] { full, fullNeg })
            {
                for (int i = 0; i < 4; i++)
                {
                    MavF15GameplayChannelEnvelope e = a.Get((MavF15SurfaceChannel)i);
                    float v = s.requested.Get((MavF15SurfaceChannel)i);
                    inside &= v >= e.minDeg && v <= e.maxDeg;
                }
            }

            Record(inside && full.symmetricLimited && !fullNeg.symmetricLimited,
                "full stick stays inside every gameplay envelope (stab " + full.requested.symmetricStabilatorDeg.ToString("F2") + " nose-up, limited at the envelope / "
                + fullNeg.requested.symmetricStabilatorDeg.ToString("F2") + " nose-down, within it; ail " + full.requested.aileronDeg.ToString("F2") + ", rud "
                + full.requested.rudderDeg.ToString("F2") + ") and reports the limiting", report, ref passed, ref failed);

            float worstJump = 0f;
            bool monotone = true;
            float prev = Solve(a, bias, -1f, 0f, 0f).requested.symmetricStabilatorDeg;
            for (int i = 1; i <= 2000; i++)
            {
                float v = Solve(a, bias, -1f + i * 0.001f, 0f, 0f).requested.symmetricStabilatorDeg;
                worstJump = Mathf.Max(worstJump, Mathf.Abs(v - prev));
                monotone &= v <= prev;
                prev = v;
            }

            Record(monotone && worstJump <= a.pitchStabilatorDegPerUnit * 0.001f * 1.001f + 1e-5f,
                "continuous and monotonic: over 2,001 pitch commands the largest step is " + worstJump.ToString("E2")
                + " deg per 0.001 of stick (gearing bound " + (a.pitchStabilatorDegPerUnit * 0.001f).ToString("E2") + ")", report, ref passed, ref failed);

            MavPilotCommand nan = new MavPilotCommand { pitch = float.NaN, roll = float.PositiveInfinity, yaw = float.NegativeInfinity, throttle01 = float.NaN };
            MavF15PilotControlSolution z = MavF15PilotControlMapping.Solve(nan, a, bias);
            MavF15PilotControlSolution thr = MavF15PilotControlMapping.Solve(new MavPilotCommand { throttle01 = 1f }, a, bias);
            Record(z.requested.IsFinite() && z.requested.symmetricStabilatorDeg == bias && z.requested.aileronDeg == 0f
                   && thr.requested.symmetricStabilatorDeg == bias && thr.requested.aileronDeg == 0f && thr.requested.rudderDeg == 0f,
                "non-finite stick is centred, never passed on; throttle reaches no surface", report, ref passed, ref failed);
        }

        private static MavF15PilotControlSolution Solve(MavF15GameplayControlAuthority a, float bias, float pitch, float roll, float yaw)
        {
            return MavF15PilotControlMapping.Solve(new MavPilotCommand { pitch = pitch, roll = roll, yaw = yaw }, a, bias);
        }

        // ---------------------------------------------------------------- [P6]

        private static void ValidateTrimStart(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P6] Trim start");
            MavF15PilotTrimStart t = MavF15PilotTrimStart.TableViiPoint36();
            MavF15TableViiState row = default(MavF15TableViiState);
            bool found = false;
            foreach (MavF15TableViiState s in MavF15TableViiTrimRecovery.SymmetricStates())
            {
                if (s.part1Point == t.tableViiPoint)
                {
                    row = s;
                    found = true;
                }
            }

            MavF15ResearchSymmetricTrimResult solve = MavF15AfitResearchTrimSolver.SolveSymmetric(
                MavF15AfitResearchIdentity.ConfigurationId, t.trueAirspeedFtPerSec,
                new MavF15ResearchSymmetricTrimGuess((float)row.alphaDeg, (float)row.stabilatorDeg, (float)row.thetaDeg));
            Record(found && solve.converged && row.trueVelocityFtPerSec == t.trueAirspeedFtPerSec
                   && solve.symmetricStabilatorDeg == (float)t.symmetricStabilatorDeg && solve.alphaDeg == (float)t.alphaDeg
                   && solve.pitchAttitudeDeg == (float)t.thetaDeg,
                "the stored start equals a fresh WP-3B solve of Table VII point " + t.tableViiPoint + " bit for bit: stabilator "
                + solve.symmetricStabilatorDeg.ToString("R") + ", alpha " + solve.alphaDeg.ToString("R") + ", theta " + solve.pitchAttitudeDeg.ToString("R")
                + " deg at V " + t.trueAirspeedFtPerSec + " ft/s", report, ref passed, ref failed);

            MavF15ResearchSymmetricTrimResidual res = MavF15AfitResearchTrimSolver.EvaluateSymmetricResidual(
                t.trueAirspeedFtPerSec, t.alphaDeg, t.symmetricStabilatorDeg, t.thetaDeg);
            Record(res.DrivenNorm < 1e-5,
                "the source's own residual at the stored start is " + res.DrivenNorm.ToString("E2") + " (in trim)", report, ref passed, ref failed);

            MavF15ResearchSourceState x = new MavF15ResearchSourceState
            {
                alphaRad = t.alphaDeg * Math.PI / 180.0,
                thetaRad = t.thetaDeg * Math.PI / 180.0,
                trueAirspeedFtPerSec = t.trueAirspeedFtPerSec
            };
            double[,] jac;
            double[] re = null, im;
            bool ok = MavF15ResearchStabilityAnalysis.Jacobian(x, t.symmetricStabilatorDeg, MavF15ResearchStabilityAnalysis.DirectStep, out jac)
                      && MavValidationEigenSolver.Eigenvalues(jac, out re, out im);
            double worst = double.MinValue;
            if (ok)
            {
                for (int i = 0; i < re.Length; i++)
                    worst = Math.Max(worst, re[i]);
            }

            Record(ok && worst < 0.0 && t.symmetricStabilatorDeg > -25.0 && t.symmetricStabilatorDeg < -5.0,
                "the start is STABLE in the source model (largest real part " + worst.ToString("E3") + " /s) with its stabilator inside the source-exercised region",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [P7]

        private static void ValidateEnvironmentAndThrust(List<Object> created, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P7] Environment and thrust: the frozen research path, bit for bit");
            MavF15PilotControlledRig pilot = PilotRig(created, "p7-pilot");
            ResearchRig research = ResearchRig.Build(created, "p7-research");
            MavF15PilotTrimStart t = MavF15PilotTrimStart.TableViiPoint36();
            string reason;
            Inject(pilot.body, t, out reason);
            Inject(research.body, t, out reason);

            MavFlightEnvironment pe = pilot.body.ResolveEnvironment(pilot.body.transform.position.y);
            MavFlightEnvironment re = research.body.ResolveEnvironment(research.body.transform.position.y);
            Record(pe.valid && re.valid && SameAir(pe.atmosphere, re.atmosphere) && pe.densitySource == re.densitySource
                   && pe.gravitySource == re.gravitySource && pe.loadSetGravityMps2 == re.loadSetGravityMps2
                   && pe.configurationId == MavF15PilotControlledIdentity.ConfigurationId,
                "the pilot aircraft's environment is the research source environment bit for bit: density " + pe.atmosphere.densityKgM3.ToString("R")
                + " kg/m^3, gravity " + pe.loadSetGravityMps2.ToString("R") + " m/s^2 through the load set", report, ref passed, ref failed);

            MavFlightState state = pilot.body.debugState;
            MavPropulsiveLoads pl, rl;
            bool pOk = MavF15PilotControlledFixedThrust.TryEvaluate(pilot.body, state, pe.atmosphere, out pl, out reason);
            bool rOk = MavF15AfitResearchThrustSource.TryEvaluate(MavF15AfitResearchIdentity.ConfigurationId, MavF15ResearchConditionMode.SourceReproduction,
                state, re.atmosphere, out rl, out reason);
            MavPropulsiveLoads full = pilot.thrust.Evaluate(state, pe.atmosphere, 1f, Dt);
            MavPropulsiveLoads idle = pilot.thrust.Evaluate(state, pe.atmosphere, 0f, Dt);
            Record(pOk && rOk && SameBits(pl.forceAeroBodyN, rl.forceAeroBodyN) && SameBits(pl.momentAeroBodyNm, rl.momentAeroBodyNm)
                   && SameBits(full.forceAeroBodyN, idle.forceAeroBodyN) && !pl.hasAuthoritativeData
                   && pilot.thrust.debugStatus.Contains(MavF15PilotControlledFixedThrust.ThrottleStatus),
                "thrust = the research 8,300 lbf total + the 0.25-in moment bit for bit (" + pl.forceAeroBodyN.x.ToString("R") + " N, "
                + pl.momentAeroBodyNm.y.ToString("R") + " N m); throttle 0 and 1 give identical thrust: THROTTLE INACTIVE - FIXED RESEARCH THRUST",
                report, ref passed, ref failed);

            MavFlightState slow = state;
            slow.trueAirspeedMps = 200f * 0.3048f;
            bool slowRefused = !MavF15PilotControlledFixedThrust.TryEvaluate(pilot.body, slow, pe.atmosphere, out pl, out reason);
            bool otherRefused = !MavF15PilotControlledFixedThrust.TryEvaluate(research.body, state, re.atmosphere, out pl, out reason);
            Record(slowRefused && otherRefused,
                "no thrust below the source-exercised 218.5 ft/s, and none on a body that is not the pilot-controlled aircraft", report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [P8]

        private static void ValidateAerodynamics(List<Object> created, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P8] Aerodynamics: MavF15AeroModel's research routines, bit for bit");
            MavF15PilotControlledRig pilot = PilotRig(created, "p8-pilot");
            string reason;
            Inject(pilot.body, MavF15PilotTrimStart.TableViiPoint36(), out reason);

            // The frozen component, in its six-axis research mode under SourceReproduction, fed the same
            // surfaces through its documented research input (no actuator bound, injected differential tail).
            ResearchRig research = ResearchRig.Build(created, "p8-research");
            MavF15AeroModel frozen = research.aero;
            frozen.surfaceOwner = null;
            frozen.useIndependentDifferentialTailResearchInput = true;
            if (research.root.GetComponent<MavF15ControlActuator>() != null)
                Object.DestroyImmediate(research.root.GetComponent<MavF15ControlActuator>());

            MavFlightState baseState = pilot.body.debugState;
            MavAtmosphereSample air = MavF15AfitResearchRuntimeEnvironment.SourceAir();
            float[] alphas = { 8f, 12f, 17.4864559f, 22f };
            float[] betas = { -4f, 0f, 3f };
            float[][] surfaces = { new[] { -15.1452856f, 0f, 0f, 0f }, new[] { -20f, 12f, 3.6f, -9f }, new[] { -7f, -18f, -5.4f, 14f } };
            Vector3[] rates = { Vector3.zero, new Vector3(0.2f, -0.05f, 0.1f) };
            int cases = 0, same = 0;
            foreach (float al in alphas)
            foreach (float be in betas)
            foreach (float[] sfc in surfaces)
            foreach (Vector3 w in rates)
            {
                MavFlightState s = baseState;
                s.alphaRad = al * DegToRad;
                s.betaRad = be * DegToRad;
                s.aeroBodyRatesRadSec = w;
                MavF15SurfaceState surf = new MavF15SurfaceState { symmetricStabilatorDeg = sfc[0], aileronDeg = sfc[1], differentialStabilatorDeg = sfc[2], rudderDeg = sfc[3] };
                pilot.actuator.limits = pilot.profile.gameplayControlAuthority.ToActuatorTravel();
                pilot.actuator.SetF15Command(MavF15RequestedSurfaceState.From(surf), 0f);
                pilot.actuator.SnapToBoundedCommand();

                MavAeroCoefficients cp = pilot.aero.Evaluate(s, default(MavControlInput), air);
                frozen.independentDifferentialTailResearchDeg = sfc[2];
                MavAeroCoefficients cf = frozen.Evaluate(s, new MavControlInput { elevatorDeg = sfc[0], aileronDeg = sfc[1], rudderDeg = sfc[3] }, air);
                cases++;
                if (!pilot.aero.debugRefused && !frozen.debugRefused && SameBits(cp, cf))
                    same++;
            }

            Record(cases > 0 && same == cases && frozen.debugConditionMode == MavF15ResearchConditionMode.SourceReproduction,
                "the pilot aircraft's coefficients equal MavF15AeroModel's (six-axis research, SourceReproduction) bit for bit in " + same + "/" + cases
                + " states (alpha x beta x surfaces x rates): no coefficient, term or tuning was added", report, ref passed, ref failed);

            MavFlightState fast = baseState;
            fast.trueAirspeedMps = 720f * 0.3048f;
            MavFlightState highBeta = baseState;
            highBeta.betaRad = 25f * DegToRad;
            MavAeroCoefficients c1 = pilot.aero.Evaluate(fast, default(MavControlInput), air);
            bool r1 = pilot.aero.debugRefused && c1.cx == 0f && c1.cm == 0f;
            pilot.aero.Evaluate(highBeta, default(MavControlInput), air);
            bool r2 = pilot.aero.debugRefused;
            pilot.aero.acknowledgeResearchAerodynamics = false;
            pilot.aero.Evaluate(baseState, default(MavControlInput), air);
            bool r3 = pilot.aero.debugRefused;
            pilot.aero.acknowledgeResearchAerodynamics = true;
            pilot.aero.Evaluate(baseState, default(MavControlInput), air);
            bool admitted = !pilot.aero.debugRefused && pilot.aero.debugExtrapolatedFromFitCondition;
            Record(r1 && r2 && r3 && admitted,
                "fail-closed: zero coefficients (and a reason) above 699.7 ft/s, outside the transcribed beta span, and without the explicit research opt-in; admitted at the trim, flagged EXTRAPOLATED from the M 0.6 fit",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [P9]

        private static void ValidateSignsThroughRig(List<Object> created, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P9] Sign response through the real rig (law -> actuator -> aero -> load set), SHADOW: computed, never applied");
            MavF15PilotControlledRig rig = PilotRig(created, "p9-pilot");
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
            MavFlightDynamicsLoadSet set = rig.body.debugLoadSet;
            float mass = MavF15AfitResearchMassReference.MassKg;
            float q = rig.body.debugState.dynamicPressurePa;
            float cbar = MavF15BaumannMach06Reference.CreateReferenceGeometry().meanAerodynamicChordM;
            float s = MavF15BaumannMach06Reference.CreateReferenceGeometry().wingAreaM2;
            Vector3 f = set.AppliedForceAeroBodyN;
            float g = MavF15AfitResearchRuntimeEnvironment.SourceGravityMps2;
            Record(injected && shadow && set.IsFinite() && rig.body.debugLoadApplications == 0
                   && f.magnitude / (mass * g) < 2e-5f && neutral.magnitude / (q * s * cbar) < 2e-6f,
                "neutral hold: at the trim start the body's own loads (aero + thrust + source gravity) balance - |F|/W " + (f.magnitude / (mass * g)).ToString("E2")
                + ", |M|/qSc " + (neutral.magnitude / (q * s * cbar)).ToString("E2") + " - finite, and nothing is applied in shadow",
                report, ref passed, ref failed);

            Case(rig, source, neutral, "pitch +0.5", 0.5f, 0f, 0f, 1, +1, report, ref passed, ref failed);
            Case(rig, source, neutral, "pitch -0.5", -0.5f, 0f, 0f, 1, -1, report, ref passed, ref failed);
            Case(rig, source, neutral, "roll +0.5", 0f, 0.5f, 0f, 0, +1, report, ref passed, ref failed);
            Case(rig, source, neutral, "roll -0.5", 0f, -0.5f, 0f, 0, -1, report, ref passed, ref failed);
            Case(rig, source, neutral, "yaw +0.5", 0f, 0f, 0.5f, 2, +1, report, ref passed, ref failed);
            Case(rig, source, neutral, "yaw -0.5", 0f, 0f, -0.5f, 2, -1, report, ref passed, ref failed);

            Vector3 both = MomentFor(rig, source, new MavPilotCommand { pitch = 0.3f, roll = 0.3f }, 9) - neutral;
            Vector3 wdot = AngularAcceleration(both);
            Record(both.y > 0f && both.x > 0f && wdot.y > 0f && wdot.x > 0f && IsFinite(both),
                "combined moderate pitch +0.3 / roll +0.3: pitching moment " + both.y.ToString("E3") + " N m (q-dot " + wdot.y.ToString("E3")
                + ") and rolling moment " + both.x.ToString("E3") + " N m (p-dot " + wdot.x.ToString("E3") + ") both in the commanded sense",
                report, ref passed, ref failed);
            gate.ReturnToLegacy("p9 done");
        }

        private static void Case(MavF15PilotControlledRig rig, MavManualPilotCommandSource source, Vector3 neutral, string label,
            float pitch, float roll, float yaw, int axis, int expectedSign, StringBuilder report, ref int passed, ref int failed)
        {
            Vector3 dm = MomentFor(rig, source, new MavPilotCommand { pitch = pitch, roll = roll, yaw = yaw }, axis + 1) - neutral;
            Vector3 wdot = AngularAcceleration(dm);
            float m = axis == 0 ? dm.x : axis == 1 ? dm.y : dm.z;
            float w = axis == 0 ? wdot.x : axis == 1 ? wdot.y : wdot.z;
            string[] names = { "rolling moment L / p-dot", "pitching moment M / q-dot", "yawing moment N / r-dot" };
            MavF15SurfaceState act = rig.actuator.ActualF15SurfaceState.channels;
            Record(IsFinite(dm) && Math.Sign(m) == expectedSign && Math.Sign(w) == expectedSign,
                label + ": surfaces stab " + act.symmetricStabilatorDeg.ToString("F2") + " ail " + act.aileronDeg.ToString("F2") + " diff "
                + act.differentialStabilatorDeg.ToString("F2") + " rud " + act.rudderDeg.ToString("F2") + " -> " + names[axis] + " "
                + m.ToString("E3") + " N m / " + w.ToString("E3") + " rad/s^2 (" + (expectedSign > 0 ? "+" : "-") + " expected)",
                report, ref passed, ref failed);
        }

        private static Vector3 MomentFor(MavF15PilotControlledRig rig, MavManualPilotCommandSource source, MavPilotCommand command, int stepIndex)
        {
            source.command = command;
            rig.law.StepPilotControlLaw(Dt);
            rig.actuator.StepActuator(Dt);
            rig.body.StepPhysicsForValidation(Dt, Dt * (stepIndex + 1));
            return rig.body.debugLoadSet.totalMomentAeroBodyNm;
        }

        /// <summary>I^-1 M in aero body axes at zero body rate, with the research product of inertia.</summary>
        private static Vector3 AngularAcceleration(Vector3 moment)
        {
            double ix = MavF15AfitResearchMassReference.IxKgM2, iy = MavF15AfitResearchMassReference.IyKgM2;
            double iz = MavF15AfitResearchMassReference.IzKgM2, ixz = MavF15AfitResearchMassReference.IxzKgM2;
            double gamma = ix * iz - ixz * ixz;
            return new Vector3(
                (float)((iz * moment.x + ixz * moment.z) / gamma),
                (float)(moment.y / iy),
                (float)((ixz * moment.x + ix * moment.z) / gamma));
        }

        // ---------------------------------------------------------------- [P10]

        private static void ValidateOwnership(List<Object> created, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P10] Ownership: F15PilotControlledResearch");
            MavFlightPhysicsOwner o = MavFlightPhysicsOwner.F15PilotControlledResearch;
            Record(MavFlightPhysicsOwnership.IsReplacementPhysicsAllowed(o) && !MavFlightPhysicsOwnership.IsLegacyPhysicsAllowed(o)
                   && !MavFlightPhysicsOwnership.ViolatesExclusiveOwnership(o) && MavFlightPhysicsOwnership.HasExactlyOneGravitySource(o)
                   && MavFlightPhysicsOwnership.ResolveGravityProvider(o) == MavGravityProvider.ReplacementLoadSet
                   && !MavFlightPhysicsOwnership.ShouldRigidbodyUseUnityGravity(o),
                "F15PilotControlledResearch: replacement writes, legacy gated off, exclusive, one gravity source (the replacement load set), Rigidbody gravity off",
                report, ref passed, ref failed);

            bool unchanged = MavFlightPhysicsOwnership.IsReplacementPhysicsAllowed(MavFlightPhysicsOwner.F16Replacement)
                && MavFlightPhysicsOwnership.ResolveGravityProvider(MavFlightPhysicsOwner.F16Replacement) == MavGravityProvider.UnityRigidbody
                && MavFlightPhysicsOwnership.IsReplacementPhysicsAllowed(MavFlightPhysicsOwner.F15AfitResearch)
                && MavFlightPhysicsOwnership.ResolveGravityProvider(MavFlightPhysicsOwner.F15AfitResearch) == MavGravityProvider.ReplacementLoadSet
                && MavFlightPhysicsOwnership.IsLegacyPhysicsAllowed(MavFlightPhysicsOwner.Legacy)
                && !MavFlightPhysicsOwnership.IsReplacementPhysicsAllowed(MavFlightPhysicsOwner.Shadow)
                && MavFlightPhysicsOwnership.IsLegacyPhysicsAllowed(MavFlightPhysicsOwner.Fault);
            bool every = true;
            foreach (MavFlightPhysicsOwner m in (MavFlightPhysicsOwner[])Enum.GetValues(typeof(MavFlightPhysicsOwner)))
                every &= !MavFlightPhysicsOwnership.ViolatesExclusiveOwnership(m) && MavFlightPhysicsOwnership.HasExactlyOneGravitySource(m);
            Record(unchanged && every,
                "F16Replacement, F15AfitResearch, Legacy, Shadow and Fault rules unchanged; every mode exclusive with one gravity source",
                report, ref passed, ref failed);

            MavF15PilotControlledRig rig = PilotRig(created, "p10-pilot");
            string reason;
            Inject(rig.body, MavF15PilotTrimStart.TableViiPoint36(), out reason);
            MavFlightPhysicsOwnership gate = rig.gameObject.AddComponent<MavFlightPhysicsOwnership>();
            rig.body.physicsOwnership = gate;
            gate.AttachGovernedBody(rig.body);
            rig.body.NotifyOwnershipChanged();
            Record(gate.owner == MavFlightPhysicsOwner.Legacy && !gate.allowF15PilotControlledOwnership && !gate.allowResearchValidationOwnership
                   && !rig.body.ArmedForLiveFlight,
                "default: a new authority is Legacy with the pilot-controlled safety hold ON; the body is unarmed", report, ref passed, ref failed);

            string e1, e2, e3, e4, e5, e6, e7, e8;
            bool holdRefused = !gate.TryEnterF15PilotControlledOwnership(rig.body, MavF15PilotControlledOwnershipGrant.Instance, out e1);
            gate.allowF15PilotControlledOwnership = true;
            bool nullGrant = !gate.TryEnterF15PilotControlledOwnership(rig.body, null, out e2);
            bool researchGrant = !gate.TryEnterF15PilotControlledOwnership(rig.body, MavF15ResearchOwnershipGrant.Instance, out e3);
            gate.allowResearchValidationOwnership = true;
            bool researchOwnerRefusesPilot = !gate.TryEnterF15AfitResearchOwnership(rig.body, MavF15ResearchOwnershipGrant.Instance, out e4);
            gate.allowResearchValidationOwnership = false;

            MavF15SurfaceChannelLimits saved = rig.actuator.limits.aileron;
            rig.actuator.limits.aileron.travelProvenance = MavEngineDataProvenance.PublicReference;
            bool gradedTravel = !gate.TryEnterF15PilotControlledOwnership(rig.body, MavF15PilotControlledOwnershipGrant.Instance, out e5);
            rig.actuator.limits.aileron = saved;

            MavF15AfitResearchStaticSurfaceHold hold = rig.gameObject.AddComponent<MavF15AfitResearchStaticSurfaceHold>();
            bool withHold = !gate.TryEnterF15PilotControlledOwnership(rig.body, MavF15PilotControlledOwnershipGrant.Instance, out e6);
            Object.DestroyImmediate(hold);

            // Deliberately NOT HideAndDontSave: FindObjectsByType does not see hidden objects, and a live
            // aircraft in a scene is never hidden.
            GameObject other = new GameObject("p10-other-armed");
            created.Add(other);
            MavSixDoFBody otherBody = other.AddComponent<MavSixDoFBody>();
            otherBody.simulationEnabled = true;
            bool anotherArmed = !gate.TryEnterF15PilotControlledOwnership(rig.body, MavF15PilotControlledOwnershipGrant.Instance, out e7);
            otherBody.simulationEnabled = false;

            bool nothingChanged = gate.owner == MavFlightPhysicsOwner.Legacy && !rig.body.ArmedForLiveFlight;
            Record(holdRefused && nullGrant && researchGrant && researchOwnerRefusesPilot && gradedTravel && withHold && anotherArmed && nothingChanged,
                "refused, owner left Legacy and body unarmed: safety hold on; no grant; the RESEARCH grant; the research owner asked for the pilot body; "
                + "actuator travel graded PublicReference; a research static hold present; another armed body", report, ref passed, ref failed);
            report.AppendLine("      e.g. " + e1);
            report.AppendLine("      e.g. " + e4);
            report.AppendLine("      e.g. " + e5);

            ResearchRig research = ResearchRig.Build(created, "p10-research");
            Inject(research.body, MavF15PilotTrimStart.TableViiPoint36(), out reason);
            MavFlightPhysicsOwnership rgate = research.root.AddComponent<MavFlightPhysicsOwnership>();
            research.body.physicsOwnership = rgate;
            rgate.AttachGovernedBody(research.body);
            rgate.allowF15PilotControlledOwnership = true;
            bool researchBodyRefused = !rgate.TryEnterF15PilotControlledOwnership(research.body, MavF15PilotControlledOwnershipGrant.Instance, out e8);
            Record(researchBodyRefused && rgate.owner == MavFlightPhysicsOwner.Legacy && !research.body.ArmedForLiveFlight && e8.Contains("not exactly"),
                "the frozen research validation body cannot become the pilot-controlled owner: " + e8, report, ref passed, ref failed);

            bool entered = gate.TryEnterF15PilotControlledOwnership(rig.body, MavF15PilotControlledOwnershipGrant.Instance, out e1);
            bool armed = rig.body.ArmedForLiveFlight && gate.owner == MavFlightPhysicsOwner.F15PilotControlledResearch;
            string e9;
            bool shadowRefused = !gate.TryEnterShadow(out e9);
            gate.ReturnToLegacy("p10 done");
            Record(entered && armed && shadowRefused && !rig.body.ArmedForLiveFlight && gate.owner == MavFlightPhysicsOwner.Legacy,
                "the exact pilot-controlled rig is granted - and armed by the authority itself; Shadow is refused while it owns physics; returning to Legacy disarms it",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [P11]

        private static readonly string[] PilotFiles =
        {
            "F15/MavF15PilotControlledIdentity.cs", "F15/MavF15PilotControlApproximation.cs", "F15/MavF15PilotControlledFlightDynamicsProfile.cs",
            "F15/MavF15PilotControlledAuthority.cs", "F15/MavF15PilotControlledFixedThrust.cs", "F15/MavF15PilotControlLaw.cs",
            "F15/MavF15PilotControlledAeroModel.cs", "F15/MavF15PilotControlledDiagnostics.cs", "F15/MavF15PilotControlledRig.cs",
            "F15/MavF15PilotControlledHud.cs", "Core/MavKeyboardPilotCommandSource.cs"
        };

        private static void ValidateSourceScan(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P11] Source scan of the pilot-controlled layer (" + PilotFiles.Length + " files)");
            string root = Path.Combine(Application.dataPath, MavFlightDynamicsOwnershipScan.FlightDynamicsRelativePath);
            string[] writes = { "AddForce", "AddTorque", "AddRelativeForce", "AddRelativeTorque", "AddForceAtPosition", "AddExplosionForce",
                                "velocity =", "Velocity =", "MovePosition(", "MoveRotation(", ".position =", ".rotation =" };
            string[] arming = { "simulationEnabled =", "ArmedForLiveFlight =" };
            string[] forbidden = { "MavF15AfitResearchTrimSolver", "MavF15AfitResearchSourceEnvironment", "MavF15AfitResearchSourceDynamics",
                                   "MavValidation", "MavF15BaumannTableVii", "MavF15TableVii", "MavF100", "MavF15ResearchStaticControlState", "MavF15ResearchRuntimeAuthority",
                                   "FromActuator(", "new MavF15ResearchDemonstratedControlRange", "new MavF15PhysicalSurfaceHardStops",
                                   "new MavF15ActuatorRateLimits", "MavF15HardStop", "MavF15RateLimit" };
            string wrote = null, armed = null, named = null;
            int scanned = 0;
            foreach (string rel in PilotFiles)
            {
                string full = Path.Combine(root, rel);
                if (!File.Exists(full))
                {
                    named = rel + " (missing)";
                    continue;
                }

                scanned++;
                foreach (string line in File.ReadAllLines(full))
                {
                    string code = StripComment(line);
                    foreach (string t in writes)
                        if (code.IndexOf(t, StringComparison.Ordinal) >= 0) wrote = rel + ": " + code.Trim();
                    foreach (string t in arming)
                        if (code.IndexOf(t, StringComparison.Ordinal) >= 0) armed = rel + ": " + code.Trim();
                    foreach (string t in forbidden)
                        if (code.IndexOf(t, StringComparison.Ordinal) >= 0) named = rel + ": " + t;
                }
            }

            Record(scanned == PilotFiles.Length && wrote == null,
                wrote == null ? "no Rigidbody write in any pilot-controlled file: the body's sanctioned initializer and its single load application are the only paths" : "VIOLATION " + wrote,
                report, ref passed, ref failed);
            Record(armed == null,
                armed == null ? "no pilot-controlled file arms a body: the ownership authority stays the single arming authority" : "VIOLATION " + armed,
                report, ref passed, ref failed);
            Record(named == null,
                named == null ? "none names the research trim / source RHS / validation types / Table VII / F100 / the research runtime authority, constructs an actual surface state, or writes the research authority types"
                              : "VIOLATION " + named, report, ref passed, ref failed);
        }

        private static string StripComment(string line)
        {
            int c = line.IndexOf("//", StringComparison.Ordinal);
            return c >= 0 ? line.Substring(0, c) : line;
        }

        // ---------------------------------------------------------------- [P12]

        private static void ValidateKeyboard(List<Object> created, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P12] Keyboard pilot-command source");
            bool axes = MavKeyboardPilotCommandSource.Axis(true, false) == 1f && MavKeyboardPilotCommandSource.Axis(false, true) == -1f
                        && MavKeyboardPilotCommandSource.Axis(true, true) == 0f && MavKeyboardPilotCommandSource.Axis(false, false) == 0f;
            float up = MavKeyboardPilotCommandSource.Shape(0f, 1f, 2.5f, 4f, 0.1f);
            float back = MavKeyboardPilotCommandSource.Shape(1f, 0f, 2.5f, 4f, 0.1f);
            float reverse = MavKeyboardPilotCommandSource.Shape(0.5f, -1f, 2.5f, 4f, 0.1f);
            Record(axes && Mathf.Abs(up - 0.25f) < 1e-6f && Mathf.Abs(back - 0.6f) < 1e-6f && Mathf.Abs(reverse - 0.25f) < 1e-6f,
                "binary keys become ramped axes: 0 -> " + up + " rising, 1 -> " + back + " returning, 0.5 -> " + reverse + " reversing (per 0.1 s)",
                report, ref passed, ref failed);

            GameObject go = new GameObject("p12-keyboard");
            go.hideFlags = HideFlags.HideAndDontSave;
            created.Add(go);
            MavKeyboardPilotCommandSource k = go.AddComponent<MavKeyboardPilotCommandSource>();
            MavPilotCommand c;
            bool got = k.TryGetCommand(out c);
            Record(k.IsOperationalCommandSource && k.SignalLossPolicy == MavCommandSignalLossPolicy.NeutralCommand
                   && k.pitchUpKey == KeyCode.W && k.pitchDownKey == KeyCode.S && k.rollLeftKey == KeyCode.A && k.rollRightKey == KeyCode.D
                   && k.yawLeftKey == KeyCode.Q && k.yawRightKey == KeyCode.E && c.IsNeutral(0f) && got == k.HasCommandSignal,
                "declared an operational human input path with Maverick's keyboard convention (W/S pitch, A/D roll, Q/E yaw), neutral at rest, neutral on signal loss",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- rigs and helpers

        private static MavF15PilotControlledRig PilotRig(List<Object> created, string name)
        {
            MavF15PilotControlledRig rig = MavF15PilotControlledRig.Create(name, MavF15PilotCommandSourceKind.Scripted, false, MavF15PilotControlMode.DirectV1, MavF15PilotPhysicsRevision.R1InstantaneousSurfaces);
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

            // Refresh the body's published state from the new pose without stepping physics.
            body.StepPhysicsForValidation(0f, 0f);
            return true;
        }

        private sealed class ResearchRig
        {
            public GameObject root;
            public MavSixDoFBody body;
            public MavF15AeroModel aero;

            public static ResearchRig Build(List<Object> created, string name)
            {
                ResearchRig r = new ResearchRig();
                r.root = new GameObject(name);
                r.root.hideFlags = HideFlags.HideAndDontSave;
                created.Add(r.root);
                Rigidbody rb = r.root.AddComponent<Rigidbody>();
                rb.useGravity = false;
                MavF15AfitResearchFlightDynamicsProfile profile = r.root.AddComponent<MavF15AfitResearchFlightDynamicsProfile>();
                profile.conditionMode = MavF15ResearchConditionMode.SourceReproduction;
                profile.densityPolicy = MavF15ResearchDensityPolicy.SourceFixedDensity;
                profile.gravityPolicy = MavF15ResearchGravityPolicy.SourceGravity;
                r.aero = r.root.AddComponent<MavF15AeroModel>();
                r.aero.sourceMode = MavF15AeroSourceMode.BaumannMach06SixAxisResearch;
                r.aero.allowCrossValidationResearchModel = true;
                MavManualPilotCommandSource command = r.root.AddComponent<MavManualPilotCommandSource>();
                command.treatAsOperationalSource = true;
                MavF15ControlLaw law = r.root.AddComponent<MavF15ControlLaw>();
                law.mode = MavF15FcsMode.AFITResearch;
                MavF15ControlActuator actuator = r.root.AddComponent<MavF15ControlActuator>();
                MavF15AfitResearchFixedThrust thrust = r.root.AddComponent<MavF15AfitResearchFixedThrust>();
                r.body = r.root.AddComponent<MavSixDoFBody>();
                r.body.profileProvider = profile;
                r.body.aerodynamicModel = r.aero;
                r.body.controlSurfaceActuator = actuator;
                r.body.controlLaw = law;
                r.body.pilotCommandSource = command;
                r.body.propulsionModel = thrust;
                r.body.acceptNonAuthoritativePropulsion = true;
                law.sixDoFBody = r.body;
                law.actuator = actuator;
                law.commandSource = command;
                actuator.sixDoFBody = r.body;
                r.body.ApplyConfiguredProfile(true);
                r.body.NotifyOwnershipChanged();
                return r;
            }
        }

        private static bool SameAir(MavAtmosphereSample a, MavAtmosphereSample b)
        {
            return a.densityKgM3 == b.densityKgM3 && a.altitudeM == b.altitudeM && a.temperatureK == b.temperatureK
                   && a.pressurePa == b.pressurePa && a.speedOfSoundMps == b.speedOfSoundMps;
        }

        private static bool SameBits(Vector3 a, Vector3 b)
        {
            return a.x.Equals(b.x) && a.y.Equals(b.y) && a.z.Equals(b.z);
        }

        private static bool SameBits(MavAeroCoefficients a, MavAeroCoefficients b)
        {
            return a.cx.Equals(b.cx) && a.cy.Equals(b.cy) && a.cz.Equals(b.cz) && a.cl.Equals(b.cl) && a.cm.Equals(b.cm) && a.cn.Equals(b.cn);
        }

        private static bool IsFinite(Vector3 v)
        {
            return Finite(v.x) && Finite(v.y) && Finite(v.z);
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
