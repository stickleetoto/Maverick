#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using MaverickFresh.FlightDynamics;
using MaverickFresh.FlightDynamics.F15;
using Object = UnityEngine.Object;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>
    /// REALISTIC MOUSE INSTRUCTOR V2 - headless validation of the PRODUCTION guidance function
    /// (<see cref="MavMouseAimGuidance.Step"/>), the production aim accumulator (<see cref="MavMouseAimInput"/>), the production
    /// source component (<see cref="MavMouseAimPilotCommandSource"/>) and its opt-in wiring into the pilot-controlled F-15 rig.
    /// Nothing is flown here (edit mode has no PhysX); the Play Mode flight test is MavMouseAimInstructorV2FlightValidationRunner.
    ///
    /// Every GameObject is HideAndDontSave and destroyed; no scene or prefab is opened or saved. Pass criteria are signs,
    /// bounds, identities, finiteness and continuity - never "feels right".
    ///
    ///   [MI1]  identity and provenance: every guidance number listed, MAVERICK_TUNED_NON_AUTHORITATIVE, self-consistent
    ///   [MI2]  axes / signs at canonical headings; flight-path vs nose semantics at the alpha-17.5 trim geometry
    ///   [MI3]  rolled and inverted aircraft
    ///   [MI4]  near-vertical targets and a near-vertical flight path
    ///   [MI5]  target behind (180 deg): reversal side, history, hysteresis (no thrash)
    ///   [MI6]  zero / NaN / Infinity / bad dt / slow flight: no non-finite command, neutral when invalid
    ///   [MI7]  bounds and continuity over a deterministic sweep; a sudden 180-deg reversal only moves bounded commands
    ///   [MI8]  banked turn, not flat yaw
    ///   [MI9]  gravity compensation: level-turn bank and pitch rate emerge
    ///   [MI10] envelope soft caps: visible, command-only
    ///   [MI11] mouse aim input: additive accumulation, free-look isolation, rejection, clamp, frame-rate independence
    ///   [MI12] source readiness and fail-closed policies (missing aircraft / camera / disabled source; no inspector fallback)
    ///   [MI13] opt-in F-15 rig: one law, one load per step, V2 scale, V1 neutral, grant unchanged, no pose write, opt-in OFF
    ///   [MI14] source scan of the new layer
    /// </summary>
    public static class MavMouseAimInstructorV2Validation
    {
        private const float Dt = 0.02f;
        private const float TrimAlphaDeg = 17.5f;
        private const float TrimSpeedMps = 91.68f;
        private const float G = 9.80665f;

        public static readonly string[] NewLayerFiles =
        {
            "Core/MavMouseAimGuidance.cs", "Core/MavMouseAimInput.cs", "Core/MavMouseAimPilotCommandSource.cs", "Core/MavMouseAimCameraRig.cs"
        };

        public static string RunAll(out int passed, out int failed)
        {
            passed = 0;
            failed = 0;
            StringBuilder report = new StringBuilder(32 * 1024);
            report.AppendLine("REALISTIC MOUSE INSTRUCTOR V2 - headless validation");
            report.AppendLine(MavMouseAimProvenance.Identity);
            report.AppendLine("Production guidance + input + source + opt-in F-15 rig wiring. Nothing is flown here.");

            List<Object> created = new List<Object>();
            try
            {
                ValidateIdentity(report, ref passed, ref failed);
                ValidateCanonicalAxes(report, ref passed, ref failed);
                ValidateRolledAndInverted(report, ref passed, ref failed);
                ValidateVertical(report, ref passed, ref failed);
                ValidateReversal(report, ref passed, ref failed);
                ValidateInvalidInputs(report, ref passed, ref failed);
                ValidateBoundsAndContinuity(report, ref passed, ref failed);
                ValidateBankNotFlatYaw(report, ref passed, ref failed);
                ValidateGravityCompensation(report, ref passed, ref failed);
                ValidateSoftCaps(report, ref passed, ref failed);
                ValidateMouseInput(created, report, ref passed, ref failed);
                ValidateSourceFailClosed(created, report, ref passed, ref failed);
                ValidateRig(created, report, ref passed, ref failed);
                ValidateSourceScan(report, ref passed, ref failed);
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

        // ================================================================== geometry helpers

        /// <summary>
        /// Aircraft pose built from its flight path: heading, flight-path angle, bank about the velocity vector, then alpha
        /// (nose up about the wing). Unity Euler: +x pitches nose DOWN, +z rolls LEFT, hence the signs.
        /// </summary>
        private static Quaternion Pose(float headingDeg, float gammaDeg, float bankDeg, float alphaDeg)
        {
            return Quaternion.Euler(0f, headingDeg, 0f) * Quaternion.Euler(-gammaDeg, 0f, 0f)
                   * Quaternion.Euler(0f, 0f, -bankDeg) * Quaternion.Euler(-alphaDeg, 0f, 0f);
        }

        private static Vector3 PathDir(float headingDeg, float gammaDeg)
        {
            return MavMouseAimInput.DirectionFromAngles(headingDeg, gammaDeg);
        }

        private static MavMouseAimGuidanceInput Scenario(float headingDeg, float gammaDeg, float bankDeg, float alphaDeg, float speed, Vector3 target)
        {
            return MavMouseAimGuidanceInput.FromPose(Pose(headingDeg, gammaDeg, bankDeg, alphaDeg), PathDir(headingDeg, gammaDeg) * speed, target, G);
        }

        private static MavMouseAimGuidanceOutput Once(MavMouseAimGuidanceInput input)
        {
            MavMouseAimGuidanceState s = MavMouseAimGuidanceState.Reset;
            return MavMouseAimGuidance.Step(input, MavMouseAimGuidanceConfig.Default(), ref s, Dt);
        }

        private static MavMouseAimGuidanceOutput Once(MavMouseAimGuidanceInput input, MavMouseAimGuidanceConfig cfg)
        {
            MavMouseAimGuidanceState s = MavMouseAimGuidanceState.Reset;
            return MavMouseAimGuidance.Step(input, cfg, ref s, Dt);
        }

        /// <summary>The limited command a held input converges to (the filter's fixed point), stepped 200 times.</summary>
        private static MavMouseAimGuidanceOutput Settled(MavMouseAimGuidanceInput input)
        {
            MavMouseAimGuidanceState s = MavMouseAimGuidanceState.Reset;
            MavMouseAimGuidanceOutput o = default(MavMouseAimGuidanceOutput);
            for (int i = 0; i < 200; i++)
                o = MavMouseAimGuidance.Step(input, MavMouseAimGuidanceConfig.Default(), ref s, Dt);
            return o;
        }

        private static string C(MavPilotCommand c)
        {
            return "p " + c.pitch.ToString("F3") + " r " + c.roll.ToString("F3") + " y " + c.yaw.ToString("F3");
        }

        // ================================================================== [MI1]

        private static void ValidateIdentity(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[MI1] Identity and provenance");
            Record(MavMouseAimProvenance.MaverickTunedNonAuthoritative == MavF15PilotControlProvenance.MaverickTunedNonAuthoritative
                   && MavMouseAimProvenance.Identity.Contains("NOT War Thunder") && MavMouseAimProvenance.Identity.Contains("NOT an F-15 / F-16 production FCS"),
                "label \"" + MavMouseAimProvenance.Identity + "\"", report, ref passed, ref failed);

            MavMouseAimGuidanceConfig cfg = MavMouseAimGuidanceConfig.Default();
            MavMouseAimGuidanceConfig.Entry[] entries = cfg.Entries();
            bool everyFloatListed = true, valuesMatch = true, reasoned = true;
            string missing = "";
            foreach (FieldInfo f in typeof(MavMouseAimGuidanceConfig).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (f.FieldType != typeof(float))
                    continue;
                MavMouseAimGuidanceConfig.Entry e = Array.Find(entries, x => x.name == f.Name);
                if (e.name == null)
                {
                    everyFloatListed = false;
                    missing += f.Name + " ";
                    continue;
                }

                valuesMatch &= (float)f.GetValue(cfg) == e.value;
                reasoned &= !string.IsNullOrEmpty(e.why) && !string.IsNullOrEmpty(e.unit) && !string.IsNullOrEmpty(e.protects);
            }

            string reason;
            Record(everyFloatListed && valuesMatch && reasoned && cfg.IsSelfConsistent(out reason),
                "every float of the guidance config (" + entries.Length + ") is listed with value, unit, reason and protected limit; self-consistent"
                + (missing.Length > 0 ? " MISSING: " + missing : ""), report, ref passed, ref failed);
            Record(cfg.aimReference == MavAimReference.FlightPath && default(MavAimReference) == MavAimReference.FlightPath
                   && cfg.pitchRateAtFullStickDegSec == MavF15PilotControlGainsV2.V2().commandedPitchRateAtFullStickDegSec
                   && cfg.rollRateAtFullStickDegSec == MavF15PilotControlGainsV2.V2().commandedRollRateAtFullStickDegSec,
                "first-slice semantics: the aim is a desired FLIGHT PATH; default stick scale equals the unchanged V2 law's full-stick rates (7 / 15 deg/s)",
                report, ref passed, ref failed);
            Record(MavMouseAimPilotCommandSource.ExecutionOrder > -400 && MavMouseAimPilotCommandSource.ExecutionOrder < -300,
                "execution order " + MavMouseAimPilotCommandSource.ExecutionOrder + ": after the ownership authority (-400), before the control law (-300), actuator (-200), body (-100)",
                report, ref passed, ref failed);
        }

        // ================================================================== [MI2]

        private static void ValidateCanonicalAxes(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[MI2] Axes and signs at canonical headings (level flight path, wings level, alpha 17.5 deg, 91.7 m/s)");
            float[] headings = { 0f, 90f, 180f, 270f, 37f };
            bool ahead = true, right = true, left = true, up = true, down = true, mirror = true, invariant = true;
            string sample = "";
            MavMouseAimGuidanceOutput r0 = default(MavMouseAimGuidanceOutput);
            for (int i = 0; i < headings.Length; i++)
            {
                float h = headings[i];
                MavMouseAimGuidanceOutput a = Once(Scenario(h, 0f, 0f, TrimAlphaDeg, TrimSpeedMps, PathDir(h, 0f)));
                MavMouseAimGuidanceOutput rr = Once(Scenario(h, 0f, 0f, TrimAlphaDeg, TrimSpeedMps, PathDir(h + 4f, 0f)));
                MavMouseAimGuidanceOutput ll = Once(Scenario(h, 0f, 0f, TrimAlphaDeg, TrimSpeedMps, PathDir(h - 4f, 0f)));
                MavMouseAimGuidanceOutput uu = Once(Scenario(h, 0f, 0f, TrimAlphaDeg, TrimSpeedMps, PathDir(h, 4f)));
                MavMouseAimGuidanceOutput dd = Once(Scenario(h, 0f, 0f, TrimAlphaDeg, TrimSpeedMps, PathDir(h, -4f)));

                ahead &= a.valid && Mathf.Abs(a.rawCommand.pitch) < 1e-4f && Mathf.Abs(a.rawCommand.roll) < 1e-4f && Mathf.Abs(a.rawCommand.yaw) < 1e-4f
                         && a.mode == MavAimGuidanceMode.OnTarget && a.targetErrorDeg < 1e-2f;
                right &= rr.rawCommand.roll > 0.1f && rr.rawCommand.yaw > 0f && Mathf.Abs(rr.rawCommand.pitch) < 1e-3f;
                left &= ll.rawCommand.roll < -0.1f && ll.rawCommand.yaw < 0f;
                up &= uu.rawCommand.pitch > 0.1f && Mathf.Abs(uu.rawCommand.roll) < 1e-3f;
                down &= dd.rawCommand.pitch < -0.1f && Mathf.Abs(dd.rawCommand.roll) < 1e-3f;
                mirror &= Mathf.Abs(rr.rawCommand.roll + ll.rawCommand.roll) < 1e-4f && Mathf.Abs(rr.rawCommand.yaw + ll.rawCommand.yaw) < 1e-4f
                          && Mathf.Abs(rr.rawCommand.pitch - ll.rawCommand.pitch) < 1e-4f;
                if (i == 0)
                {
                    r0 = rr;
                    sample = "4 deg right " + C(rr.rawCommand) + "; 4 deg up " + C(uu.rawCommand) + "; 4 deg down " + C(dd.rawCommand);
                }
                else
                {
                    invariant &= Mathf.Abs(rr.rawCommand.roll - r0.rawCommand.roll) < 1e-4f && Mathf.Abs(rr.rawCommand.yaw - r0.rawCommand.yaw) < 1e-4f
                                 && Mathf.Abs(rr.rawCommand.pitch - r0.rawCommand.pitch) < 1e-4f;
                }
            }

            Record(ahead, "aim along the flight path -> zero pitch / roll / yaw, OnTarget, at headings 0/90/180/270/37", report, ref passed, ref failed);
            Record(right && left, "target right -> roll right (+) and fine-aim pedal right (+); left -> roll left (-), pedal left (-)", report, ref passed, ref failed);
            Record(up && down, "target above the flight path -> pitch up (+); below -> push (-); no roll for a purely vertical error. " + sample, report, ref passed, ref failed);
            Record(mirror && invariant, "left/right exact mirror (roll and yaw negate, pitch equal) and invariant under heading", report, ref passed, ref failed);

            // Flight path vs nose: at alpha 17.5 the nose is 17.5 deg above the path.
            MavMouseAimGuidanceOutput alongNose = Once(MavMouseAimGuidanceInput.FromPose(Pose(0f, 0f, 0f, TrimAlphaDeg), PathDir(0f, 0f) * TrimSpeedMps,
                Pose(0f, 0f, 0f, TrimAlphaDeg) * Vector3.forward, G));
            MavMouseAimGuidanceConfig noseCfg = MavMouseAimGuidanceConfig.Default();
            noseCfg.aimReference = MavAimReference.Nose;
            MavMouseAimGuidanceOutput noseOnNose = Once(MavMouseAimGuidanceInput.FromPose(Pose(0f, 0f, 0f, TrimAlphaDeg), PathDir(0f, 0f) * TrimSpeedMps,
                Pose(0f, 0f, 0f, TrimAlphaDeg) * Vector3.forward, G), noseCfg);
            MavMouseAimGuidanceOutput noseOnPath = Once(Scenario(0f, 0f, 0f, TrimAlphaDeg, TrimSpeedMps, PathDir(0f, 0f)), noseCfg);
            Record(Mathf.Abs(alongNose.targetErrorDeg - TrimAlphaDeg) < 0.01f && alongNose.rawCommand.pitch > 0.5f
                   && noseOnNose.targetErrorDeg < 0.01f && Mathf.Abs(noseOnNose.rawCommand.pitch) < 1e-4f && noseOnPath.rawCommand.pitch < 0f,
                "semantics are never conflated: aiming at the NOSE under flight-path semantics is a " + alongNose.targetErrorDeg.ToString("F2")
                + " deg climb request (pitch " + alongNose.rawCommand.pitch.ToString("F2") + "); under nose semantics it is on target, and the level flight path is a push ("
                + noseOnPath.rawCommand.pitch.ToString("F2") + ")", report, ref passed, ref failed);
        }

        // ================================================================== [MI3]

        private static void ValidateRolledAndInverted(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[MI3] Rolled and inverted aircraft (level flight path)");
            Vector3 ahead = PathDir(0f, 0f);
            MavMouseAimGuidanceOutput r90 = Once(Scenario(0f, 0f, 90f, TrimAlphaDeg, TrimSpeedMps, ahead));
            MavMouseAimGuidanceOutput r60 = Once(Scenario(0f, 0f, 60f, TrimAlphaDeg, TrimSpeedMps, ahead));
            MavMouseAimGuidanceOutput l60 = Once(Scenario(0f, 0f, -60f, TrimAlphaDeg, TrimSpeedMps, ahead));
            Record(r90.rawCommand.roll < -0.5f && r60.rawCommand.roll < -0.5f && l60.rawCommand.roll > 0.5f
                   && Mathf.Abs(r60.rawCommand.roll + l60.rawCommand.roll) < 1e-4f
                   && Mathf.Abs(r60.currentBankDeg - 60f) < 0.01f && Mathf.Abs(l60.currentBankDeg + 60f) < 0.01f,
                "banked with the target ahead -> roll back to wings level: bank +90 roll " + r90.rawCommand.roll.ToString("F2") + ", +60 " + r60.rawCommand.roll.ToString("F2")
                + ", -60 " + l60.rawCommand.roll.ToString("F2") + " (bank telemetry +60 / -60 deg exact)", report, ref passed, ref failed);

            MavMouseAimGuidanceOutput inv = Once(Scenario(0f, 0f, 180f, TrimAlphaDeg, TrimSpeedMps, ahead));
            MavMouseAimGuidanceOutput invUp = Once(Scenario(0f, 0f, 180f, TrimAlphaDeg, TrimSpeedMps, PathDir(0f, 3f)));
            MavMouseAimGuidanceOutput invUpS = Settled(Scenario(0f, 0f, 180f, TrimAlphaDeg, TrimSpeedMps, PathDir(0f, 3f)));
            Record(inv.valid && Mathf.Abs(inv.rawCommand.roll) > 1f && Mathf.Abs(inv.rawCommand.pitch) < 1e-4f && Mathf.Abs(Mathf.Abs(inv.currentBankDeg) - 180f) < 0.01f,
                "inverted, target ahead -> full roll toward upright (raw " + inv.rawCommand.roll.ToString("F2") + "), no pitch", report, ref passed, ref failed);
            Record(invUp.rawCommand.pitch < 0f && invUpS.command.pitch < 0f && invUpS.command.pitch >= -MavMouseAimGuidanceConfig.Default().maxPushStick - 1e-6f,
                "inverted, target 3 deg above (world) -> PUSH (aircraft lift points down): raw pitch " + invUp.rawCommand.pitch.ToString("F3")
                + ", settled " + invUpS.command.pitch.ToString("F3") + " (push capped at -" + MavMouseAimGuidanceConfig.Default().maxPushStick + ")",
                report, ref passed, ref failed);
        }

        // ================================================================== [MI4]

        private static void ValidateVertical(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[MI4] Near-vertical targets and flight path");
            MavMouseAimGuidanceOutput zen = Once(Scenario(0f, 0f, 0f, TrimAlphaDeg, TrimSpeedMps, Vector3.up));
            MavMouseAimGuidanceOutput zenRolled = Once(Scenario(0f, 0f, 90f, TrimAlphaDeg, TrimSpeedMps, Vector3.up));
            Record(zen.valid && zen.rawCommand.pitch >= 1f - 1e-4f && Mathf.Abs(zen.rawCommand.roll) < 1e-3f
                   && (zen.limiters & MavAimGuidanceLimiter.FlightPathRateCapped) != 0,
                "zenith from level -> full pull (" + zen.rawCommand.pitch.ToString("F3") + "), no roll, flight-path rate capped at the law's full-stick rate ("
                + zen.desiredFlightPathRateDegSec.ToString("F2") + " deg/s)", report, ref passed, ref failed);
            Record(zenRolled.rawCommand.roll < -0.5f && Mathf.Abs(zenRolled.rawCommand.pitch) < 1e-3f,
                "zenith while banked 90 right -> roll left toward the lift-up attitude first, no pull while the lift is sideways (pitch "
                + zenRolled.rawCommand.pitch.ToString("F3") + ")", report, ref passed, ref failed);

            MavMouseAimGuidanceOutput nadir = Once(Scenario(0f, 0f, 0f, TrimAlphaDeg, TrimSpeedMps, Vector3.down));
            MavMouseAimGuidanceOutput nadirS = Settled(Scenario(0f, 0f, 0f, TrimAlphaDeg, TrimSpeedMps, Vector3.down));
            Record(nadir.valid && Mathf.Abs(nadir.rawCommand.roll) > 1f && Mathf.Abs(Mathf.Abs(nadir.bankErrorDeg) - 180f) < 0.5f
                   && nadirS.command.pitch >= -MavMouseAimGuidanceConfig.Default().maxPushStick - 1e-6f && (nadirS.limiters & MavAimGuidanceLimiter.PushLimited) != 0,
                "nadir from level -> roll to inverted (bank error " + nadir.bankErrorDeg.ToString("F1") + ") and pull rather than push: settled push "
                + nadirS.command.pitch.ToString("F3") + " limited to -" + MavMouseAimGuidanceConfig.Default().maxPushStick, report, ref passed, ref failed);

            // Aircraft climbing straight up, target on the horizon.
            Quaternion vertical = Quaternion.Euler(-89.5f, 0f, 0f);
            MavMouseAimGuidanceOutput vp = Once(MavMouseAimGuidanceInput.FromPose(vertical, vertical * Vector3.forward * 80f, PathDir(0f, 0f), G));
            MavMouseAimGuidanceOutput vp2 = Once(MavMouseAimGuidanceInput.FromPose(vertical, vertical * Vector3.forward * 80f, PathDir(90f, 0f), G));
            Record(vp.valid && vp2.valid && Finite(vp.command) && Finite(vp2.command) && Finite(vp.rawCommand) && Finite(vp2.rawCommand)
                   && MavMouseAimGuidance.IsFinite(vp.currentBankDeg) && vp.rawCommand.pitch < 0f,
                "flight path vertical (89.5 deg), target on the horizon ahead / to the side -> finite, bounded commands (push toward the belly-side horizon "
                + vp.rawCommand.pitch.ToString("F2") + "; side target roll " + vp2.rawCommand.roll.ToString("F2") + ")", report, ref passed, ref failed);
        }

        // ================================================================== [MI5]

        private static void ValidateReversal(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[MI5] Target behind (180 deg)");
            Vector3 behind = PathDir(180f, 0f);
            MavMouseAimGuidanceOutput level = Once(Scenario(0f, 0f, 0f, TrimAlphaDeg, TrimSpeedMps, behind));
            MavMouseAimGuidanceOutput bankL = Once(Scenario(0f, 0f, -20f, TrimAlphaDeg, TrimSpeedMps, behind));
            Record(level.valid && level.mode == MavAimGuidanceMode.Reversal && level.rawCommand.roll > 0f && Finite(level.rawCommand)
                   && bankL.rawCommand.roll < 0f && Mathf.Abs(level.rawCommand.yaw) < 1e-6f,
                "exactly behind: Reversal mode; wings level -> deterministic right turn (roll " + level.rawCommand.roll.ToString("F2")
                + "), banked left -> keeps turning left (" + bankL.rawCommand.roll.ToString("F2") + "); no pedal", report, ref passed, ref failed);

            // History: a target swinging left through 150 -> 180 keeps the left side.
            MavMouseAimGuidanceState s = MavMouseAimGuidanceState.Reset;
            MavMouseAimGuidanceConfig cfg = MavMouseAimGuidanceConfig.Default();
            for (int i = 0; i <= 30; i++)
                MavMouseAimGuidance.Step(Scenario(0f, 0f, 0f, TrimAlphaDeg, TrimSpeedMps, PathDir(-150f - i, 0f)), cfg, ref s, Dt);
            MavMouseAimGuidanceOutput kept = MavMouseAimGuidance.Step(Scenario(0f, 0f, 0f, TrimAlphaDeg, TrimSpeedMps, behind), cfg, ref s, Dt);
            Record(kept.reversalSideFromHistory && kept.rawCommand.roll < 0f,
                "a target that swung LEFT to behind keeps the left turn through the reversal band (roll " + kept.rawCommand.roll.ToString("F2") + ")",
                report, ref passed, ref failed);

            // Hysteresis: dithering +-0.05 deg around 180 never flips the roll command's sign.
            s = MavMouseAimGuidanceState.Reset;
            int flips = 0;
            float lastSign = 0f;
            for (int i = 0; i < 100; i++)
            {
                float az = 180f + ((i & 1) == 0 ? 0.05f : -0.05f);
                MavMouseAimGuidanceOutput o = MavMouseAimGuidance.Step(Scenario(0f, 0f, 0f, TrimAlphaDeg, TrimSpeedMps, PathDir(az, 0f)), cfg, ref s, Dt);
                float sign = Mathf.Sign(o.rawCommand.roll);
                if (i > 0 && sign != lastSign)
                    flips++;
                lastSign = sign;
            }

            Record(flips == 0, "dithering +-0.05 deg about 180 deg for 100 steps: " + flips + " roll-sign flips (no thrash)", report, ref passed, ref failed);
        }

        // ================================================================== [MI6]

        private static void ValidateInvalidInputs(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[MI6] Invalid inputs fail closed (no non-finite command; neutral when invalid)");
            MavMouseAimGuidanceInput good = Scenario(0f, 0f, 0f, TrimAlphaDeg, TrimSpeedMps, PathDir(30f, 0f));
            string[] names = { "zero target", "NaN target", "+Inf target", "NaN flight path", "NaN attitude", "zero attitude", "dt 0", "dt NaN", "NaN gravity" };
            bool allNeutral = true;
            string reasons = "";
            for (int i = 0; i < names.Length; i++)
            {
                MavMouseAimGuidanceInput bad = good;
                float dt = Dt;
                switch (i)
                {
                    case 0: bad.targetDirectionWorld = Vector3.zero; break;
                    case 1: bad.targetDirectionWorld = new Vector3(float.NaN, 0f, 1f); break;
                    case 2: bad.targetDirectionWorld = new Vector3(float.PositiveInfinity, 0f, 1f); break;
                    case 3: bad.flightPathWorldMps = new Vector3(0f, float.NaN, 90f); break;
                    case 4: bad.noseWorld = new Vector3(float.NaN, 0f, 1f); break;
                    case 5: bad.aircraftUpWorld = Vector3.zero; break;
                    case 6: dt = 0f; break;
                    case 7: dt = float.NaN; break;
                    case 8: bad.gravityMps2 = float.NaN; break;
                }

                MavMouseAimGuidanceState s = MavMouseAimGuidanceState.Reset;
                s.pitch = 0.7f;   // distinctive filter state: an invalid step must not emit it either
                MavMouseAimGuidanceOutput o = MavMouseAimGuidance.Step(bad, MavMouseAimGuidanceConfig.Default(), ref s, dt);
                bool neutral = !o.valid && o.mode == MavAimGuidanceMode.Invalid && o.command.pitch == 0f && o.command.roll == 0f && o.command.yaw == 0f
                               && !string.IsNullOrEmpty(o.invalidReason);
                allNeutral &= neutral;
                reasons += (i > 0 ? "; " : "") + names[i] + ": " + (neutral ? o.invalidReason : "NOT REFUSED");

                MavMouseAimGuidanceOutput after = MavMouseAimGuidance.Step(good, MavMouseAimGuidanceConfig.Default(), ref s, Dt);
                allNeutral &= after.valid && Finite(after.command);
            }

            Record(allNeutral, "9 invalid inputs refused with a reason and an exactly neutral command; the next valid step is finite: " + reasons, report, ref passed, ref failed);

            MavMouseAimGuidanceOutput slow = Once(Scenario(0f, 0f, 0f, TrimAlphaDeg, 1f, PathDir(10f, 0f)));
            Record(slow.valid && (slow.limiters & MavAimGuidanceLimiter.NoseReferenceFallback) != 0 && Finite(slow.command),
                "1 m/s: the flight path is not a usable direction -> nose reference, FLAGGED (" + slow.limiters + ")", report, ref passed, ref failed);
        }

        // ================================================================== [MI7]

        private static void ValidateBoundsAndContinuity(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[MI7] Bounds and continuity (deterministic sweep, 4000 steps)");
            MavMouseAimGuidanceConfig cfg = MavMouseAimGuidanceConfig.Default();
            MavMouseAimGuidanceState s = MavMouseAimGuidanceState.Reset;
            uint seed = 12345u;
            bool bounded = true, continuous = true, finite = true;
            float maxStep = cfg.commandRatePerSec * Dt + 1e-5f, worstStep = 0f;
            MavPilotCommand prev = MavPilotCommand.Neutral;
            for (int i = 0; i < 4000; i++)
            {
                float h = Rand(ref seed) * 360f, gam = Rand(ref seed) * 160f - 80f, bank = Rand(ref seed) * 360f - 180f;
                float alpha = Rand(ref seed) * 40f - 8f, speed = 0.5f + Rand(ref seed) * 250f;
                Vector3 t = new Vector3(Rand(ref seed) - 0.5f, Rand(ref seed) - 0.5f, Rand(ref seed) - 0.5f);
                if (i % 50 == 0)
                    t = PathDir(h + 180f, -gam);
                MavMouseAimGuidanceOutput o = MavMouseAimGuidance.Step(Scenario(h, gam, bank, alpha, speed, t), cfg, ref s, Dt);
                if (!o.valid)
                    continue;
                MavPilotCommand c = o.command;
                finite &= Finite(c) && Finite(o.rawCommand);
                bounded &= c.pitch <= cfg.pitchStickLimit + 1e-6f && c.pitch >= -cfg.maxPushStick - 1e-6f && Mathf.Abs(c.roll) <= cfg.rollStickLimit + 1e-6f
                           && Mathf.Abs(c.yaw) <= cfg.yawStickLimit + 1e-6f;
                float step = Mathf.Max(Mathf.Abs(c.pitch - prev.pitch), Mathf.Max(Mathf.Abs(c.roll - prev.roll), Mathf.Abs(c.yaw - prev.yaw)));
                worstStep = Mathf.Max(worstStep, step);
                continuous &= step <= maxStep;
                prev = c;
            }

            Record(finite && bounded, "random poses (any heading, |gamma| <= 80, any bank, alpha -8..32, 0.5..250 m/s), random and behind targets: every command finite, "
                + "pitch in [-" + cfg.maxPushStick + ", " + cfg.pitchStickLimit + "], |roll| <= " + cfg.rollStickLimit + ", |yaw| <= " + cfg.yawStickLimit,
                report, ref passed, ref failed);
            Record(continuous, "even with the scenario jumping every step, no channel moves more than commandRate x dt = " + (cfg.commandRatePerSec * Dt).ToString("F3")
                + " per step (worst " + worstStep.ToString("F4") + ")", report, ref passed, ref failed);

            // Sudden 180-deg reversal from on-target: only bounded command steps; the guidance has no pose / velocity output at all.
            s = MavMouseAimGuidanceState.Reset;
            MavMouseAimGuidanceInput on = Scenario(0f, 0f, 0f, TrimAlphaDeg, TrimSpeedMps, PathDir(0f, 0f));
            for (int i = 0; i < 50; i++)
                MavMouseAimGuidance.Step(on, cfg, ref s, Dt);
            MavMouseAimGuidanceInput rev = Scenario(0f, 0f, 0f, TrimAlphaDeg, TrimSpeedMps, PathDir(180f, 0f));
            MavPilotCommand before = s.Command();
            float worstRev = 0f;
            for (int i = 0; i < 25; i++)
            {
                MavMouseAimGuidanceOutput o = MavMouseAimGuidance.Step(rev, cfg, ref s, Dt);
                worstRev = Mathf.Max(worstRev, Mathf.Abs(o.command.roll - before.roll));
                before = o.command;
            }

            FieldInfo[] outputFields = typeof(MavMouseAimGuidanceOutput).GetFields(BindingFlags.Public | BindingFlags.Instance);
            bool noPoseOutput = Array.TrueForAll(outputFields, f => f.FieldType != typeof(Quaternion) && f.FieldType != typeof(Vector3));
            Record(worstRev <= maxStep && noPoseOutput && s.roll > 0.4f,
                "180-deg mouse reversal: the roll command ramps (worst step " + worstRev.ToString("F3") + ", reaches " + s.roll.ToString("F2")
                + " after 0.5 s); the guidance output has no pose, velocity or rate field - only bounded intent", report, ref passed, ref failed);
        }

        // ================================================================== [MI8]

        private static void ValidateBankNotFlatYaw(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[MI8] Banked turn, not flat yaw (aircraft body axes)");
            MavMouseAimGuidanceConfig cfg = MavMouseAimGuidanceConfig.Default();
            float[] errors = { 10f, 30f, 60f, 90f, 135f };
            bool ok = true;
            string rows = "";
            foreach (float e in errors)
            {
                MavMouseAimGuidanceOutput o = Settled(Scenario(0f, 0f, 0f, TrimAlphaDeg, TrimSpeedMps, PathDir(e, 0f)));
                ok &= o.command.roll > 0.9f && o.command.yaw == 0f && o.desiredBankDeg > 20f;
                rows += (rows.Length > 0 ? "; " : "") + e + " deg: roll " + o.command.roll.ToString("F2") + " yaw " + o.command.yaw.ToString("F2")
                        + " desired bank " + o.desiredBankDeg.ToString("F0");
            }

            Record(ok, "horizontal targets right: the instructor banks (full roll, desired bank > 20 deg) and asks NO pedal beyond " + cfg.fineAimFadeEndDeg + " deg: " + rows,
                report, ref passed, ref failed);
            MavMouseAimGuidanceOutput fine = Settled(Scenario(0f, 0f, 0f, TrimAlphaDeg, TrimSpeedMps, PathDir(1f, 0f)));
            Record(fine.command.yaw > 0f && fine.command.yaw <= cfg.yawStickLimit + 1e-6f && fine.command.roll > 0f,
                "1 deg right: fine-aim pedal " + fine.command.yaw.ToString("F3") + " (<= " + cfg.yawStickLimit + ") alongside the bank " + fine.command.roll.ToString("F2"),
                report, ref passed, ref failed);
        }

        // ================================================================== [MI9]

        private static void ValidateGravityCompensation(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[MI9] Gravity compensation: a level turn emerges");
            MavMouseAimGuidanceConfig cfg = MavMouseAimGuidanceConfig.Default();
            float errDeg = 8f;
            MavMouseAimGuidanceOutput o = Once(Scenario(0f, 0f, 0f, TrimAlphaDeg, TrimSpeedMps, PathDir(errDeg, 0f)));
            float w = Mathf.Min(cfg.flightPathRateGainPerSec * errDeg * Mathf.Deg2Rad, cfg.pitchRateAtFullStickDegSec * Mathf.Deg2Rad);
            float expectedBank = Mathf.Atan2(TrimSpeedMps * w, G) * Mathf.Rad2Deg;
            Record(Mathf.Abs(o.desiredBankDeg - expectedBank) < 0.05f,
                "wings level, " + errDeg + " deg right: desired bank " + o.desiredBankDeg.ToString("F2") + " deg = atan(V w / g) = " + expectedBank.ToString("F2"),
                report, ref passed, ref failed);

            // At that bank (still 8 deg off), the bank error vanishes and the pitch command is exactly the level-turn pitch rate w sin(bank).
            MavMouseAimGuidanceOutput atBank = Once(Scenario(0f, 0f, expectedBank, TrimAlphaDeg, TrimSpeedMps, PathDir(errDeg, 0f)));
            float expectedPitch = w * Mathf.Sin(expectedBank * Mathf.Deg2Rad) / (cfg.pitchRateAtFullStickDegSec * Mathf.Deg2Rad);
            Record(Mathf.Abs(atBank.bankErrorDeg) < 0.05f && Mathf.Abs(atBank.rawCommand.pitch - expectedPitch) < 2e-3f,
                "at that bank: bank error " + atBank.bankErrorDeg.ToString("F3") + " deg, pitch " + atBank.rawCommand.pitch.ToString("F4")
                + " = w sin(bank) / q_full = " + expectedPitch.ToString("F4") + " (the level-turn body pitch rate)", report, ref passed, ref failed);

            MavMouseAimGuidanceConfig naive = cfg;
            naive.gravityCompensation = 0f;
            MavMouseAimGuidanceOutput n = Once(Scenario(0f, 0f, 0f, TrimAlphaDeg, TrimSpeedMps, PathDir(errDeg, 0f)), naive);
            Record(Mathf.Abs(n.desiredBankDeg - 90f) < 0.05f && o.desiredBankDeg < 60f,
                "without gravity compensation the same error asks a knife-edge " + n.desiredBankDeg.ToString("F1") + " deg bank - the compensation is what keeps small errors small",
                report, ref passed, ref failed);

            // Fast, modest nose-down correction: V w > g, but the aircraft unloads and pushes instead of rolling inverted.
            MavMouseAimGuidanceOutput fastDown = Once(Scenario(0f, -3f, 0f, 12f, 112f, PathDir(0f, -15f)));
            MavMouseAimGuidanceOutput steepDown = Once(Scenario(0f, -3f, 0f, 12f, 112f, PathDir(0f, -78f)));
            Record(Mathf.Abs(fastDown.rawCommand.roll) < 1e-3f && fastDown.rawCommand.pitch < 0f && (fastDown.limiters & MavAimGuidanceLimiter.InvertedRollDeferred) != 0
                   && Mathf.Abs(steepDown.rawCommand.roll) > 1f && Mathf.Abs(Mathf.Abs(steepDown.bankErrorDeg) - 180f) < 0.5f,
                "112 m/s, 12 deg nose-down correction: no roll (" + fastDown.rawCommand.roll.ToString("F4") + "), push " + fastDown.rawCommand.pitch.ToString("F2")
                + " (inverted roll deferred); 75 deg below: roll to inverted (bank error " + steepDown.bankErrorDeg.ToString("F0") + ") and pull", report, ref passed, ref failed);
        }

        // ================================================================== [MI10]

        private static void ValidateSoftCaps(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[MI10] Envelope soft caps (command ceilings, labelled with the model limit they protect)");
            MavMouseAimGuidanceConfig cfg = MavMouseAimGuidanceConfig.Default();
            Vector3 up30 = PathDir(0f, 30f);
            MavMouseAimGuidanceOutput fast = Settled(Scenario(0f, 0f, 0f, TrimAlphaDeg, 90f, up30));
            MavMouseAimGuidanceOutput mid = Settled(Scenario(0f, 0f, 0f, TrimAlphaDeg, 77f, up30));
            MavMouseAimGuidanceOutput slow = Settled(Scenario(0f, 0f, 0f, TrimAlphaDeg, 70f, up30));
            Record((fast.limiters & MavAimGuidanceLimiter.LowSpeedPullCap) == 0 && fast.command.pitch > 0.99f
                   && (mid.limiters & MavAimGuidanceLimiter.LowSpeedPullCap) != 0 && mid.command.pitch < fast.command.pitch && mid.command.pitch > 0f
                   && (slow.limiters & MavAimGuidanceLimiter.LowSpeedPullCap) != 0 && slow.command.pitch <= 0f && slow.command.pitch >= -cfg.lowSpeedNoseDownStick - 1e-6f,
                "30 deg climb request: 90 m/s pull " + fast.command.pitch.ToString("F2") + ", 77 m/s " + mid.command.pitch.ToString("F2") + " (cap), 70 m/s "
                + slow.command.pitch.ToString("F2") + " (nose-down ceiling) - protects the research TAS floor 66.60 m/s", report, ref passed, ref failed);

            MavMouseAimGuidanceOutput highA = Settled(Scenario(0f, 0f, 0f, 27f, TrimSpeedMps, up30));
            MavMouseAimGuidanceOutput lowA = Settled(Scenario(0f, 0f, 0f, 1f, TrimSpeedMps, PathDir(0f, -30f)));
            MavMouseAimGuidanceOutput normalA = Settled(Scenario(0f, 0f, 0f, TrimAlphaDeg, TrimSpeedMps, PathDir(0f, -3f)));
            Record((highA.limiters & MavAimGuidanceLimiter.HighAlphaPullCap) != 0 && highA.command.pitch < 0.2f
                   && (lowA.limiters & MavAimGuidanceLimiter.LowAlphaPushCap) != 0 && lowA.command.pitch > normalA.command.pitch * 0.5f - 1e-6f && lowA.command.pitch <= 0f,
                "alpha 27 deg: pull faded to " + highA.command.pitch.ToString("F2") + " (protects the exercised alpha region); alpha 1 deg: push faded to "
                + lowA.command.pitch.ToString("F2") + " (protects the -4 deg transcribed floor)", report, ref passed, ref failed);

            MavMouseAimGuidanceConfig off = cfg;
            off.enableEnvelopeSoftCaps = false;
            MavMouseAimGuidanceOutput offSlow = Once(Scenario(0f, 0f, 0f, TrimAlphaDeg, 70f, up30), off);
            Record((offSlow.limiters & (MavAimGuidanceLimiter.LowSpeedPullCap | MavAimGuidanceLimiter.HighAlphaPullCap | MavAimGuidanceLimiter.LowAlphaPushCap)) == 0,
                "caps are explicit: disabled, none is reported (and none acts)", report, ref passed, ref failed);
        }

        // ================================================================== [MI11]

        private static MavMouseAimPilotCommandSource BareSource(List<Object> created, string name)
        {
            GameObject go = new GameObject(name);
            go.hideFlags = HideFlags.HideAndDontSave;
            created.Add(go);
            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.useGravity = false;
            MavMouseAimPilotCommandSource src = go.AddComponent<MavMouseAimPilotCommandSource>();
            src.readDeviceInput = false;
            src.requireAimCamera = false;
            src.aircraftBody = rb;
            src.ResetGuidanceAndAim();
            src.StepGuidance(Dt);     // zero velocity: the aim anchors on the nose (+Z): az 0, el 0
            return src;
        }

        private static void ValidateMouseInput(List<Object> created, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[MI11] Mouse aim input (the production component, fed scripted frames)");
            MavMouseAimPilotCommandSource a = BareSource(created, "mi11-a");
            MavMouseAimPilotCommandSource b = BareSource(created, "mi11-b");
            a.ProcessFrameInput(10f, 5f, false, true);
            for (int i = 0; i < 10; i++)
                b.ProcessFrameInput(1f, 0.5f, false, true);
            float k = a.degreesPerMouseUnit;
            Record(a.debugAimInitialized && Mathf.Abs(a.debugAim.azimuthDeg - 10f * k) < 1e-4f && Mathf.Abs(a.debugAim.elevationDeg - 5f * k) < 1e-4f
                   && Mathf.Abs(a.debugAim.azimuthDeg - b.debugAim.azimuthDeg) < 1e-4f && Mathf.Abs(a.debugAim.elevationDeg - b.debugAim.elevationDeg) < 1e-4f,
                "delta-accumulated world aim: one (10, 5) unit frame == ten (1, 0.5) frames -> az " + a.debugAim.azimuthDeg.ToString("F3") + " el "
                + a.debugAim.elevationDeg.ToString("F3") + " (mouse right = aim right, mouse up = aim up; " + k + " deg/unit)", report, ref passed, ref failed);

            MavMouseAimAngles held = a.debugAim;
            Vector3 viewBefore = a.ViewDirectionWorld;
            a.ProcessFrameInput(30f, -12f, true, true);
            bool aimUnchanged = a.debugAim.azimuthDeg == held.azimuthDeg && a.debugAim.elevationDeg == held.elevationDeg;
            bool viewMoved = Vector3.Angle(viewBefore, a.ViewDirectionWorld) > 10f;
            a.ProcessFrameInput(0f, 0f, false, true);
            Record(aimUnchanged && viewMoved && a.debugFreeLookYawPitchDeg == Vector2.zero && Vector3.Angle(a.ViewDirectionWorld, a.AimDirectionWorld) < 1e-3f,
                "free look: a (30, -12) frame with the key held turns the VIEW only; the aim target is bit-identical; released -> view back on the aim",
                report, ref passed, ref failed);

            int rejected0 = a.debugRejectedInputSamples;
            a.ProcessFrameInput(float.NaN, 1f, false, true);
            a.ProcessFrameInput(1f, float.PositiveInfinity, false, true);
            Record(a.debugRejectedInputSamples == rejected0 + 2 && a.debugAim.azimuthDeg == held.azimuthDeg && a.debugAim.elevationDeg == held.elevationDeg
                   && !a.SetScriptedTargetDirection(new Vector3(float.NaN, 0f, 0f)) && !a.SetScriptedTargetDirection(Vector3.zero),
                "non-finite mouse samples and non-finite / zero scripted targets are rejected and counted; the aim is unchanged", report, ref passed, ref failed);

            a.ProcessFrameInput(0f, 1000f, false, true);
            float elevationTop = a.debugAim.elevationDeg;
            a.ProcessFrameInput(0f, -5000f, false, true);
            Record(Mathf.Abs(elevationTop - a.elevationLimitDeg) < 1e-4f && Mathf.Abs(a.debugAim.elevationDeg + a.elevationLimitDeg) < 1e-4f,
                "mouse elevation clamps at +-" + a.elevationLimitDeg + " deg (the yaw axis never degenerates; zenith targets come from scripted aims)",
                report, ref passed, ref failed);

            int lost0 = a.debugRejectedInputSamples;
            MavMouseAimAngles beforeLoss = a.debugAim;
            a.ProcessFrameInput(50f, 50f, false, false);
            Record(a.debugAim.azimuthDeg == beforeLoss.azimuthDeg && a.debugAim.elevationDeg == beforeLoss.elevationDeg && a.debugFrameInputState == MavMouseAimInputState.FocusLost
                   && a.debugRejectedInputSamples == lost0,
                "a frame with the device unavailable moves nothing and is reported as input loss", report, ref passed, ref failed);

            // Frame-rate independence: the same continuous mouse trace at 30 / 50 / 60 / 144 / jittered fps, Unity loop order
            // (fixed steps run at the start of a frame, before that frame's Update reads input).
            float[] fps = { 50f, 30f, 60f, 144f, -1f };
            float worstLag = 0f, worstFinal = 0f;
            bool withinBound = true;
            float traceSpeedBound = 0f;
            for (int f = 0; f < fps.Length; f++)
            {
                MavMouseAimPilotCommandSource s = BareSource(created, "mi11-fps" + f);
                uint seed = 777u;
                double t = 0.0, prevT = 0.0, nextFixed = 0.0, maxFrame = 0.0;
                List<float> snaps = new List<float>();
                while (t < 4.0)
                {
                    double frame = fps[f] > 0f ? 1.0 / fps[f] : 1.0 / (30.0 + 114.0 * Rand(ref seed));
                    maxFrame = Math.Max(maxFrame, frame);
                    t += frame;
                    while (nextFixed <= t - 1e-9 && nextFixed <= 4.0)
                    {
                        snaps.Add(s.debugAim.azimuthDeg);
                        float ideal = (float)(TraceX(nextFixed) * s.degreesPerMouseUnit);
                        float bound = (float)(TraceSpeedMax() * s.degreesPerMouseUnit * maxFrame) + 1e-3f;
                        traceSpeedBound = Mathf.Max(traceSpeedBound, bound);
                        float lag = Mathf.Abs(s.debugAim.azimuthDeg - ideal);
                        worstLag = Mathf.Max(worstLag, lag);
                        withinBound &= lag <= bound;
                        nextFixed += Dt;
                    }

                    double tc = Math.Min(t, 4.0);
                    s.ProcessFrameInput((float)(TraceX(tc) - TraceX(prevT)), (float)(TraceY(tc) - TraceY(prevT)), false, true);
                    prevT = tc;
                }

                float finalIdeal = (float)(TraceX(4.0) * s.degreesPerMouseUnit);
                worstFinal = Mathf.Max(worstFinal, Mathf.Abs(s.debugAim.azimuthDeg - finalIdeal));
            }

            Record(withinBound && worstFinal < 5e-3f,
                "same mouse trace at 50 / 30 / 60 / 144 / jittered fps vs fixed 0.02 s: every physics-step snapshot lags the continuous trace by at most one render frame of motion "
                + "(worst " + worstLag.ToString("F3") + " deg, bound <= " + traceSpeedBound.ToString("F3") + "); the aim after the trace is identical at every fps (worst "
                + worstFinal.ToString("E1") + " deg). Unavoidable difference: a snapshot sees input up to the last rendered frame", report, ref passed, ref failed);
        }

        private static double TraceX(double t)
        {
            return 15.0 * t + 12.0 * Math.Sin(0.8 * t);
        }

        private static double TraceY(double t)
        {
            return 6.0 * Math.Sin(1.3 * t);
        }

        private static double TraceSpeedMax()
        {
            return 15.0 + 12.0 * 0.8;
        }

        // ================================================================== [MI12]

        private static void ValidateSourceFailClosed(List<Object> created, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[MI12] Source readiness and fail-closed policies");
            GameObject go = new GameObject("mi12-no-body");
            go.hideFlags = HideFlags.HideAndDontSave;
            created.Add(go);
            MavMouseAimPilotCommandSource orphan = go.AddComponent<MavMouseAimPilotCommandSource>();
            orphan.readDeviceInput = false;
            orphan.requireAimCamera = false;
            orphan.StepGuidance(Dt);
            MavPilotCommand c;
            bool got = orphan.TryGetCommand(out c);
            Record(orphan.IsOperationalCommandSource && !orphan.HasCommandSignal && !got && c.IsNeutral(0f) && orphan.debugStatus.StartsWith("NO COMMAND SIGNAL"),
                "no aircraft bound: declared operational but NO signal, TryGetCommand false + neutral: \"" + orphan.debugStatus + "\"", report, ref passed, ref failed);

            MavMouseAimPilotCommandSource s = BareSource(created, "mi12-a");
            Place(s.aircraftBody, Pose(0f, 0f, 0f, TrimAlphaDeg), PathDir(0f, 0f) * TrimSpeedMps);
            s.ResetGuidanceAndAim();
            s.StepGuidance(Dt);
            s.SetScriptedTargetDirection(PathDir(40f, 0f));
            for (int i = 0; i < 20; i++)
                s.StepGuidance(Dt);
            MavPilotCommand steering;
            bool live = s.TryGetCommand(out steering);
            MavPilotCommand again;
            s.TryGetCommand(out again);
            Record(live && s.HasCommandSignal && steering.roll > 0.5f && again.roll == steering.roll && again.pitch == steering.pitch,
                "valid guidance: signal true, steering (" + C(steering) + "); TryGetCommand has no side effects (two reads identical)", report, ref passed, ref failed);

            // Camera required but missing -> the declared policy.
            s.requireAimCamera = true;
            s.inputLossPolicy = MavMouseAimInputLossPolicy.NeutralStickHoldThrottle;
            for (int i = 0; i < 60; i++)
                s.StepGuidance(Dt);
            MavPilotCommand neutralLoss;
            bool neutralSignal = s.TryGetCommand(out neutralLoss);
            Record(neutralSignal && s.debugInputState == MavMouseAimInputState.CameraUnavailable && neutralLoss.IsNeutral(1e-4f)
                   && neutralLoss.throttle01 == s.throttle01 && s.debugStatus.StartsWith("NEUTRAL STICK"),
                "camera required and missing, NeutralStickHoldThrottle (default): ramps to neutral stick, throttle held, still a deliberate command (" + C(neutralLoss) + ")",
                report, ref passed, ref failed);

            s.inputLossPolicy = MavMouseAimInputLossPolicy.HoldAimTarget;
            s.SetScriptedTargetDirection(PathDir(40f, 0f));
            for (int i = 0; i < 20; i++)
                s.StepGuidance(Dt);
            MavPilotCommand hold;
            s.TryGetCommand(out hold);
            Record(hold.roll > 0.5f, "HoldAimTarget (opt-in): keeps steering toward the frozen aim (" + C(hold) + ")", report, ref passed, ref failed);

            s.inputLossPolicy = MavMouseAimInputLossPolicy.DropCommandSignal;
            s.StepGuidance(Dt);
            MavPilotCommand dropped;
            bool droppedGot = s.TryGetCommand(out dropped);
            Record(!droppedGot && !s.HasCommandSignal && dropped.IsNeutral(0f),
                "DropCommandSignal: no signal at all (the law then applies the declared NeutralCommand policy and readiness withholds loads - see [MI13])",
                report, ref passed, ref failed);

            s.inputLossPolicy = MavMouseAimInputLossPolicy.NeutralStickHoldThrottle;
            s.requireAimCamera = false;
            s.StepGuidance(Dt);
            s.enabled = false;
            MavPilotCommand disabled;
            Record(!s.HasCommandSignal && !s.TryGetCommand(out disabled), "a disabled source reports no signal", report, ref passed, ref failed);
            s.enabled = true;

            // Hands-off (release to neutral): neutral stick, aim re-anchored on the flight path.
            s.SetHandsOff(true);
            for (int i = 0; i < 30; i++)
                s.StepGuidance(Dt);
            MavPilotCommand released;
            s.TryGetCommand(out released);
            s.SetHandsOff(false);
            Record(released.IsNeutral(1e-4f) && s.debugInputState == MavMouseAimInputState.HandsOff,
                "hands off: the command ramps to neutral (" + C(released) + ") and the aim follows the flight path", report, ref passed, ref failed);
        }

        // ================================================================== [MI13]

        private static MavF15PilotControlledRig MouseRig(List<Object> created, string name, MavF15PilotControlMode mode)
        {
            MavF15PilotControlledRig rig = MavF15PilotControlledRig.Create(name, MavF15PilotCommandSourceKind.MouseAim, false, mode);
            rig.gameObject.hideFlags = HideFlags.HideAndDontSave;
            rig.EnsureStack();
            created.Add(rig.gameObject);
            MavMouseAimPilotCommandSource src = (MavMouseAimPilotCommandSource)rig.commandSource;
            src.readDeviceInput = false;
            src.requireAimCamera = false;
            return rig;
        }

        private static bool Inject(MavSixDoFBody body, out string reason)
        {
            Vector3 p, v, w;
            Quaternion q;
            if (!MavF15AfitResearchStateInjection.TryComputeKinematics(MavF15PilotTrimStart.TableViiPoint36().ToKinematicState(), out p, out q, out v, out w, out reason))
                return false;
            if (!body.TryApplyInitialKinematicState(p, q, v, w, out reason))
                return false;
            body.StepPhysicsForValidation(0f, 0f);
            return true;
        }

        private static void LiveStep(MavF15PilotControlledRig rig, ref float fixedTime)
        {
            fixedTime += Dt;
            ((MavMouseAimPilotCommandSource)rig.commandSource).StepGuidance(Dt);
            if (rig.controlMode == MavF15PilotControlMode.AssistedV2)
                rig.lawV2.StepPilotControlLaw(Dt);
            else
                rig.law.StepPilotControlLaw(Dt);
            rig.actuator.StepActuator(Dt);
            rig.body.StepPhysicsForValidation(Dt, fixedTime);
        }

        private static void ValidateRig(List<Object> created, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[MI13] Opt-in F-15 rig (" + MavF15PilotControlledIdentity.ConfigurationId + ", Assisted V2)");
            Record((int)MavF15PilotCommandSourceKind.Keyboard == 0 && (int)MavF15PilotCommandSourceKind.Scripted == 1 && (int)MavF15PilotCommandSourceKind.MouseAim == 2
                   && default(MavF15PilotCommandSourceKind) == MavF15PilotCommandSourceKind.Keyboard,
                "MouseAim = 2 is additive; Keyboard = 0 stays every rig's default, Scripted = 1 unchanged", report, ref passed, ref failed);

            MavF15PilotControlledRig kb = MavF15PilotControlledRig.Create("mi13-kb", MavF15PilotCommandSourceKind.Keyboard, false, MavF15PilotControlMode.AssistedV2);
            kb.gameObject.hideFlags = HideFlags.HideAndDontSave;
            kb.EnsureStack();
            created.Add(kb.gameObject);
            MavF15PilotControlledRig sc = MavF15PilotControlledRig.Create("mi13-sc", MavF15PilotCommandSourceKind.Scripted, false, MavF15PilotControlMode.AssistedV2);
            sc.gameObject.hideFlags = HideFlags.HideAndDontSave;
            sc.EnsureStack();
            created.Add(sc.gameObject);
            Record(kb.GetComponent<MavMouseAimPilotCommandSource>() == null && sc.GetComponent<MavMouseAimPilotCommandSource>() == null
                   && kb.commandSource is MavKeyboardPilotCommandSource && sc.commandSource is MavManualPilotCommandSource,
                "opt-in OFF: keyboard and scripted rigs carry no mouse-instructor component; their sources are unchanged", report, ref passed, ref failed);

            MavF15PilotControlledRig rig = MouseRig(created, "mi13-v2", MavF15PilotControlMode.AssistedV2);
            MavMouseAimPilotCommandSource src = (MavMouseAimPilotCommandSource)rig.commandSource;
            int laws = (rig.law.enabled ? 1 : 0) + (rig.lawV2 != null && rig.lawV2.enabled ? 1 : 0);
            MavF15PilotControlGainsV2 g2 = MavF15PilotControlGainsV2.V2();
            Record(src != null && rig.body.pilotCommandSource == src && rig.lawV2.commandSource == src && laws == 1 && rig.body.controlLaw == rig.lawV2
                   && src.boundLawIsRateCommand && src.guidance.pitchRateAtFullStickDegSec == g2.commandedPitchRateAtFullStickDegSec
                   && src.guidance.rollRateAtFullStickDegSec == g2.commandedRollRateAtFullStickDegSec && src.boundLawName == MavF15PilotControlLawV2.LawName
                   && src.aircraftBody == rig.body3d,
                "MouseAim rig: one source wired to body AND law (identity), exactly one law (V2) enabled and bound, the source scaled by the V2 law's own gains (read, unchanged)",
                report, ref passed, ref failed);

            string reason;
            bool injected = Inject(rig.body, out reason);
            MavFlightPhysicsOwnership gate = rig.gameObject.AddComponent<MavFlightPhysicsOwnership>();
            rig.body.physicsOwnership = gate;
            gate.AttachGovernedBody(rig.body);
            rig.body.NotifyOwnershipChanged();
            gate.allowF15PilotControlledOwnership = true;
            bool entered = gate.TryEnterF15PilotControlledOwnership(rig.body, MavF15PilotControlledOwnershipGrant.Instance, out reason);
            string grantReason;
            bool granted = MavF15PilotControlledOwnershipGrant.Instance.TryGrantResearchOwnership(rig.body, out grantReason);
            src.ResetGuidanceAndAim();
            Record(injected && entered && granted && rig.body.ArmedForLiveFlight,
                "the existing pilot-controlled grant accepts the rig unchanged (it constrains law, actuator, aero, thrust - not the input device): " + grantReason,
                report, ref passed, ref failed);

            float fixedTime = 0f;
            int loads0 = rig.body.debugLoadApplications;
            Quaternion poseBefore = rig.body3d.rotation;
            Vector3 pathBefore = rig.body3d.linearVelocity;
            Vector3 posBefore = rig.body3d.position;
            float bias = (float)MavF15PilotTrimStart.TableViiPoint36().symmetricStabilatorDeg;
            for (int i = 0; i < 10; i++)
                LiveStep(rig, ref fixedTime);
            MavF15SurfaceState trimSurfaces = rig.actuator.ActualF15SurfaceState.channels;
            Record(rig.body.debugLoadApplications == loads0 + 10 && rig.lawV2.debugCommandResolution == MavFlightControlLawBase.MavCommandResolution.SourceSignal
                   && Mathf.Abs(trimSurfaces.symmetricStabilatorDeg - bias) < 1e-3f && Mathf.Abs(trimSurfaces.aileronDeg) < 1e-3f && Mathf.Abs(trimSurfaces.rudderDeg) < 1e-3f
                   && src.debugGuidance.targetErrorDeg < 0.01f,
                "trim start, untouched mouse: the aim anchors on the flight path (error " + src.debugGuidance.targetErrorDeg.ToString("E1") + " deg) and V2 requests the trim bias "
                + "(stab " + trimSurfaces.symmetricStabilatorDeg.ToString("F4") + " vs " + bias.ToString("F4") + ", ail/rud ~0); one load per step, law fed by the source",
                report, ref passed, ref failed);

            src.SetScriptedTargetDirection(Quaternion.AngleAxis(30f, Vector3.up) * pathBefore.normalized);
            for (int i = 0; i < 25; i++)
                LiveStep(rig, ref fixedTime);
            MavF15RequestedSurfaceState req = rig.lawV2.RequestedF15SurfaceState;
            MavF15SurfaceState act = rig.actuator.ActualF15SurfaceState.channels;
            bool noPoseWrite = rig.body3d.rotation == poseBefore && rig.body3d.linearVelocity == pathBefore && rig.body3d.position == posBefore;
            Record(src.debugCommand.roll > 0.3f && act.aileronDeg * MavF15PilotControlConventions.RollRightAileronSign > 0f
                   && rig.body.debugLoadApplications == loads0 + 35 && rig.body.debugRejectedDuplicateApplications == 0 && rig.body.debugLoadSet.IsFinite()
                   && noPoseWrite && act.IsFinite(),
                "target 30 deg right: intent " + C(src.debugCommand) + " -> V2 request ail " + req.channels.aileronDeg.ToString("F2") + " rud " + req.channels.rudderDeg.ToString("F2")
                + " -> actual ail " + act.aileronDeg.ToString("F2") + " (roll-right sign); 35 / 35 loads, no duplicate; the source wrote no pose, velocity or position",
                report, ref passed, ref failed);

            // Fail closed: signal dropped -> the law's declared NeutralCommand policy, never its inspector bench input; readiness withholds loads.
            rig.lawV2.pilotCommand = new MavPilotCommand { pitch = 0.9f, roll = -0.9f, yaw = 0.9f, throttle01 = 0.2f };
            src.inputLossPolicy = MavMouseAimInputLossPolicy.DropCommandSignal;
            src.requireAimCamera = true;
            int applied0 = rig.body.debugLoadApplications, withheld0 = rig.body.debugRejectedNotLiveReadyApplications;
            for (int i = 0; i < 5; i++)
                LiveStep(rig, ref fixedTime);
            Record(rig.lawV2.debugCommandResolution == MavFlightControlLawBase.MavCommandResolution.SignalLossPolicy && rig.lawV2.debugLastCommand.IsNeutral(0f)
                   && rig.body.debugLoadApplications == applied0 && rig.body.debugRejectedNotLiveReadyApplications == withheld0 + 5,
                "signal dropped (camera lost, DropCommandSignal): the law flies its declared NeutralCommand policy - NOT the inspector bench command (0.9 / -0.9 / 0.9) - and "
                + "live-readiness withholds all 5 loads (fail closed, counted)", report, ref passed, ref failed);
            src.inputLossPolicy = MavMouseAimInputLossPolicy.NeutralStickHoldThrottle;
            src.requireAimCamera = false;
            rig.lawV2.pilotCommand = MavPilotCommand.Neutral;
            for (int i = 0; i < 3; i++)
                LiveStep(rig, ref fixedTime);
            Record(rig.body.debugLoadApplications == applied0 + 3 && rig.lawV2.debugCommandResolution == MavFlightControlLawBase.MavCommandResolution.SourceSignal,
                "signal restored: loads resume, one per step", report, ref passed, ref failed);

            // Second requester appears -> the grant refuses (unchanged authority behaviour with this source).
            rig.law.enabled = true;
            string refused;
            bool refusedTwoLaws = !MavF15PilotControlledOwnershipGrant.Instance.TryGrantResearchOwnership(rig.body, out refused);
            rig.law.enabled = false;
            Record(refusedTwoLaws && refused.Contains("both pilot-control laws"), "a second enabled law is still refused by the unchanged grant: " + refused,
                report, ref passed, ref failed);

            // V1 + mouse: neutral stick only.
            MavF15PilotControlledRig v1 = MouseRig(created, "mi13-v1", MavF15PilotControlMode.DirectV1);
            MavMouseAimPilotCommandSource s1 = (MavMouseAimPilotCommandSource)v1.commandSource;
            Place(v1.body3d, Pose(0f, 0f, 0f, TrimAlphaDeg), PathDir(0f, 0f) * TrimSpeedMps);
            s1.ResetGuidanceAndAim();
            s1.StepGuidance(Dt);
            s1.SetScriptedTargetDirection(PathDir(60f, 0f));
            for (int i = 0; i < 30; i++)
                s1.StepGuidance(Dt);
            MavPilotCommand v1c;
            Record(!s1.boundLawIsRateCommand && s1.TryGetCommand(out v1c) && v1c.IsNeutral(1e-4f) && s1.debugStatus.Contains("not a rate-command law"),
                "under Direct V1 (not a rate-command law) the source flies neutral stick only, flagged: \"" + s1.debugStatus + "\"", report, ref passed, ref failed);

            // The F2 switch gate still refuses while the mouse is steering (stick not centred), and accepts in hands-off.
            src.SetScriptedTargetDirection(Quaternion.AngleAxis(60f, Vector3.up) * pathBefore.normalized);
            for (int i = 0; i < 25; i++)
                LiveStep(rig, ref fixedTime);
            string sw;
            bool refusedSteering = !rig.TrySetControlMode(MavF15PilotControlMode.DirectV1, out sw);
            src.SetHandsOff(true);
            for (int i = 0; i < 40; i++)
                LiveStep(rig, ref fixedTime);
            string sw2;
            bool acceptedHandsOff = rig.TrySetControlMode(MavF15PilotControlMode.DirectV1, out sw2);
            Record(refusedSteering && sw.StartsWith(MavF15PilotControlledRig.SwitchRefusedNotCentred) && acceptedHandsOff && !src.boundLawIsRateCommand
                   && rig.body.controlLaw == rig.law,
                "F2 law switch: refused while the instructor deflects the stick (\"" + sw + "\"); accepted in hands-off, and the source is re-declared for V1 (neutral only)",
                report, ref passed, ref failed);
            gate.ReturnToLegacy("mi13 done");
        }

        // ================================================================== [MI14]

        private static void ValidateSourceScan(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[MI14] Source scan of the new layer (" + NewLayerFiles.Length + " files)");
            string root = Path.Combine(Application.dataPath, MavFlightDynamicsOwnershipScan.FlightDynamicsRelativePath);
            string[] writes = { "AddForce", "AddTorque", "AddRelativeForce", "AddRelativeTorque", "AddForceAtPosition", "AddExplosionForce",
                                "velocity =", "Velocity =", "MovePosition(", "MoveRotation(", ".position =", ".rotation =", "angularVelocity =" };
            string[] arming = { "simulationEnabled =", "ArmedForLiveFlight =", "TryEnter" };
            string[] coreForbidden = { "MaverickFresh.FlightDynamics.F15", "MavF15", "MavF16", "Time.deltaTime", "Time.unscaledDeltaTime" };
            string wrote = null, armed = null, named = null;
            int scanned = 0;
            foreach (string rel in NewLayerFiles)
            {
                string full = Path.Combine(root, rel);
                if (!File.Exists(full))
                {
                    named = rel + " (missing)";
                    continue;
                }

                scanned++;
                bool camera = rel.EndsWith("MavMouseAimCameraRig.cs", StringComparison.Ordinal);
                foreach (string line in File.ReadAllLines(full))
                {
                    string code = MavFlightDynamicsOwnershipScan.StripCommentsAndStringLiterals(line);
                    foreach (string t in writes)
                        if (code.IndexOf(t, StringComparison.Ordinal) >= 0) wrote = rel + ": " + code.Trim();
                    foreach (string t in arming)
                        if (code.IndexOf(t, StringComparison.Ordinal) >= 0) armed = rel + ": " + code.Trim();
                    foreach (string t in coreForbidden)
                    {
                        // The render-only camera may smooth with unscaled render time; nothing physical reads it.
                        if (camera && t == "Time.unscaledDeltaTime")
                            continue;
                        if (code.IndexOf(t, StringComparison.Ordinal) >= 0) named = rel + ": " + t;
                    }
                }
            }

            Record(scanned == NewLayerFiles.Length && wrote == null,
                wrote == null ? "no force / torque / velocity / pose write in any new file (the camera moves only itself, through SetPositionAndRotation)" : "VIOLATION " + wrote,
                report, ref passed, ref failed);
            Record(armed == null, armed == null ? "no new file arms a body or enters an owner" : "VIOLATION " + armed, report, ref passed, ref failed);
            Record(named == null, named == null ? "the Core layer names no aircraft type, and no physics-side code reads render delta time" : "VIOLATION " + named,
                report, ref passed, ref failed);

            string guidance = File.ReadAllText(Path.Combine(root, "Core/MavMouseAimGuidance.cs"));
            bool pure = guidance.IndexOf("MonoBehaviour", StringComparison.Ordinal) < 0 && guidance.IndexOf("Rigidbody", StringComparison.Ordinal) < 0
                        && guidance.IndexOf("Time.", StringComparison.Ordinal) < 0 && guidance.IndexOf("Transform", StringComparison.Ordinal) < 0;
            MavOwnershipScanResult scan = MavFlightDynamicsOwnershipScan.Scan();
            Record(pure && scan.IsClean, "the guidance is a pure function (no MonoBehaviour / Rigidbody / Transform / Time); the repository-wide ownership scan is clean ("
                + scan.filesScanned + " files)", report, ref passed, ref failed);
        }

        // ================================================================== utilities

        /// <summary>Validation-only pose seed (edit mode, no physics): Transform and Rigidbody together, as the body's own initializer does.</summary>
        private static void Place(Rigidbody rb, Quaternion rotation, Vector3 flightPathMps)
        {
            rb.transform.SetPositionAndRotation(Vector3.zero, rotation);
            rb.rotation = rotation;
            rb.linearVelocity = flightPathMps;
        }

        private static float Rand(ref uint seed)
        {
            seed = seed * 1664525u + 1013904223u;
            return (seed >> 8) / 16777216f;
        }

        private static bool Finite(MavPilotCommand c)
        {
            return MavMouseAimGuidance.IsFinite(c.pitch) && MavMouseAimGuidance.IsFinite(c.roll) && MavMouseAimGuidance.IsFinite(c.yaw);
        }

        private static void Record(bool ok, string label, StringBuilder report, ref int passed, ref int failed)
        {
            if (ok) passed++;
            else failed++;
            report.AppendLine("  " + (ok ? "PASS" : "FAIL") + "  " + label);
        }
    }

    internal static class MavMouseAimGuidanceStateExtensions
    {
        public static MavPilotCommand Command(this MavMouseAimGuidanceState s)
        {
            return new MavPilotCommand { pitch = s.pitch, roll = s.roll, yaw = s.yaw };
        }
    }
}
#endif
