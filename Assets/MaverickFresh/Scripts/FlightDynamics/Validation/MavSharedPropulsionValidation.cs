using System.Text;
using UnityEngine;
using MaverickFresh.FlightDynamics.F15;
using MaverickFresh.FlightDynamics.F16;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>
    /// Deterministic validation for the shared Profile + Engine propulsion architecture.
    ///
    /// Every case runs PRODUCTION code: real <see cref="MavEngineRuntime"/> instances, the real
    /// <see cref="MavPropulsionSystem"/> aggregation, and the real F-16 power law. Nothing here
    /// reimplements an equation it is checking.
    ///
    /// Where an expected value could have been produced by calling the code under test, it is instead
    /// HAND-CALCULATED and written as a literal with the arithmetic shown. A test whose expectation
    /// comes from the implementation passes whatever the implementation does, including the wrong
    /// thing - that failure mode has already been caught once in this project and is not repeated here.
    ///
    /// All geometry in this file is SYNTHETIC. The 3 m lateral offsets and 50 kN thrusts are invented
    /// to exercise r x F and are not F-15 or F-16 data. Nothing in this file may be cited as aircraft
    /// data.
    /// </summary>
    public static class MavSharedPropulsionValidation
    {
        private const float Tol = 1e-3f;

        // ---- SYNTHETIC test geometry. NOT aircraft data. ----------------------------------------
        private const float SynthLateralOffsetM = 3f;
        private const float SynthThrustN = 50000f;

        public static string RunAll(out int passed, out int failed)
        {
            passed = 0;
            failed = 0;
            StringBuilder report = new StringBuilder(8192);
            report.AppendLine("Maverick Shared Propulsion Architecture Validation");
            report.AppendLine("=================================================");
            report.AppendLine("All installation geometry and thrust magnitudes below are SYNTHETIC.");

            P001_SingleEngineAggregate(report, ref passed, ref failed);
            P002_SymmetricTwinCancels(report, ref passed, ref failed);
            P003_AsymmetricThrustYaws(report, ref passed, ref failed);
            P004_IndependentRuntimeState(report, ref passed, ref failed);
            P005_F16PowerRegression(report, ref passed, ref failed);
            P006_UnavailableDeckNoFakeThrust(report, ref passed, ref failed);
            P007_ProvenanceCannotLaunder(report, ref passed, ref failed);
            P008_ZeroEngines(report, ref passed, ref failed);
            P009_DisabledInstallation(report, ref passed, ref failed);
            P010_FiniteGuards(report, ref passed, ref failed);
            P011_ApplicationOwnership(report, ref passed, ref failed);
            P012_MomentTransform(report, ref passed, ref failed);
            P013_F15SkeletonKeepsDataUnavailable(report, ref passed, ref failed);
            P014_UnresolvableLawFailsClosed(report, ref passed, ref failed);
            P015_IndependentCommandsThroughPublicApi(report, ref passed, ref failed);
            P016_LinkedThrottleBroadcasts(report, ref passed, ref failed);
            P017_AuthoritativeEngineOffKeepsProvenance(report, ref passed, ref failed);
            P018_RegistrationFailsClosed(report, ref passed, ref failed);
            P019_DuplicateRegistrationDeterministic(report, ref passed, ref failed);
            P020_ProfileRuntimeSeparation(report, ref passed, ref failed);
            P021_AggregateScalarSemantics(report, ref passed, ref failed);
            P022_StepAuthorityVersusConfiguration(report, ref passed, ref failed);
            P023_LiveReadinessLadder(report, ref passed, ref failed);
            P024_AuthoringHazards(report, ref passed, ref failed);
            P025_CommandAuthorityIsExclusive(report, ref passed, ref failed);

            report.AppendLine();
            report.Append("RESULT: ")
                .Append(failed == 0 ? "PASS" : "FAIL")
                .Append(" passed=").Append(passed)
                .Append(" failed=").Append(failed);

            return report.ToString();
        }

        // ==================================================================== synthetic scaffolding

        /// <summary>
        /// A deck returning a fixed thrust, labelled SyntheticBench so it can never be mistaken for
        /// aircraft data. Not a MonoBehaviour subclass of the real deck base by accident - it IS the
        /// real deck base, so the production code path is exercised.
        /// </summary>
        private static MavFixedSyntheticDeck MakeDeck(
            float thrustN, MavThrustDataAuthority authority, bool insideEnvelope)
        {
            GameObject host = new GameObject("synthetic-deck");
            MavFixedSyntheticDeck deck = host.AddComponent<MavFixedSyntheticDeck>();
            deck.fixedThrustN = thrustN;
            deck.declaredAuthority = authority;
            deck.reportInsideEnvelope = insideEnvelope;
            return deck;
        }

        private static MavEngineProfile MakeProfile(
            string id, MavThrustDeckBase deck, MavEnginePowerDynamicsLaw law)
        {
            MavEngineProfile p = MavEngineProfile.CreateInMemory(id);
            p.displayName = id;
            p.engineVariantIdentity = "SYNTHETIC TEST ENGINE - not aircraft data";
            p.sourceIdentity = "synthetic";
            p.provenance = MavEngineDataProvenance.CrossValidationOnly;
            p.powerDynamicsLaw = law;
            p.powerDynamicsProvenance = law == MavEnginePowerDynamicsLaw.InstantNoSourcedTransient
                ? MavEngineDataProvenance.Unavailable
                : MavEngineDataProvenance.PublicReference;
            p.thrustDeck = deck;
            return p;
        }

        private static MavEngineInstallation MakeSlot(
            int slotId, string name, MavEngineProfile profile, Vector3 positionM, int channel)
        {
            MavEngineInstallation s = new MavEngineInstallation();
            s.slotId = slotId;
            s.slotName = name;
            s.engineProfile = profile;
            s.positionAeroBodyM = positionM;
            s.thrustDirectionAeroBody = new Vector3(1f, 0f, 0f);
            s.geometryDeclared = true;
            s.geometryProvenance = "SYNTHETIC validation geometry - not aircraft data";
            s.throttleChannel = channel;
            s.enabled = true;
            return s;
        }

        private static MavPropulsionSystem MakeSystem(params MavEngineInstallation[] slots)
        {
            GameObject host = new GameObject("propulsion-system-under-test");
            MavPropulsionSystem system = host.AddComponent<MavPropulsionSystem>();
            system.installation = new MavPropulsionInstallationProfile();
            system.installation.installationId = "synthetic-installation";
            system.installation.aircraftConfiguration = "SYNTHETIC";
            system.installation.engines = slots;

            string error;
            system.Build(true, out error);
            return system;
        }

        /// <summary>
        /// Attaches a scripted command source to a system under test.
        ///
        /// This is the PRODUCTION seam - a <see cref="MavPropulsionCommandSourceBase"/> component on
        /// the aircraft, which is exactly how a real control layer would deliver per-engine commands.
        /// Nothing here reaches into runtime internals.
        /// </summary>
        private static MavScriptedPropulsionCommandSource AttachCommandSource(
            MavPropulsionSystem system)
        {
            MavScriptedPropulsionCommandSource source =
                system.gameObject.AddComponent<MavScriptedPropulsionCommandSource>();
            source.Resize(system.RuntimeCount);
            system.commandSource = source;
            return source;
        }

        private static MavFlightState LevelState()
        {
            MavFlightState s = new MavFlightState();
            s.worldPositionM = new Vector3(0f, 3000f, 0f);
            s.mach = 0.5f;
            s.trueAirspeedMps = 160f;
            s.aeroBodyRatesRadSec = Vector3.zero;
            return s;
        }

        private static MavAtmosphereSample Atmosphere()
        {
            MavAtmosphereSample a = new MavAtmosphereSample();
            a.altitudeM = 3000f;
            a.densityKgM3 = 0.9093f;
            a.speedOfSoundMps = 328.6f;
            return a;
        }

        // ==================================================================== P-001

        private static void P001_SingleEngineAggregate(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P-001] one installed engine: aggregate == that engine");

            MavFixedSyntheticDeck deck =
                MakeDeck(SynthThrustN, MavThrustDataAuthority.SyntheticBench, true);
            MavEngineProfile profile = MakeProfile(
                "p001-engine", deck, MavEnginePowerDynamicsLaw.InstantNoSourcedTransient);

            // Offset in Z so there IS a moment to carry through; a zero-offset engine would make the
            // aggregate trivially equal on the moment channel and prove less.
            MavEngineInstallation slot =
                MakeSlot(0, "single", profile, new Vector3(0f, 0f, -0.5f), 0);
            MavPropulsionSystem system = MakeSystem(slot);
            system.ResetEngineState(1f);

            MavPropulsiveLoads agg = system.Evaluate(LevelState(), Atmosphere(), 1f, 0.02f);
            MavEngineLoadResult single = ResultOfIndex(system, 0);

            Record((agg.forceAeroBodyN - single.forceAeroBodyN).magnitude < Tol, "P-001",
                "aggregate force equals the single engine's force: " + V(agg.forceAeroBodyN),
                report, ref passed, ref failed);
            Record((agg.momentAeroBodyNm - single.momentAeroBodyNm).magnitude < Tol, "P-001b",
                "aggregate moment equals the single engine's moment: " + V(agg.momentAeroBodyNm),
                report, ref passed, ref failed);
            Record(Near(agg.reportedThrustN, single.thrustN), "P-001c",
                "aggregate thrust equals the single engine's thrust: " + F(agg.reportedThrustN) + " N",
                report, ref passed, ref failed);

            // Hand-calculated. Thrust 50000 N along +X at r = (0, 0, -0.5):
            //   F = (50000, 0, 0)
            //   M = r x F = (0,0,-0.5) x (50000,0,0)
            //             = (0*0 - (-0.5)*0,  (-0.5)*50000 - 0*0,  0*0 - 0*50000)
            //             = (0, -25000, 0)
            Record(Near(agg.forceAeroBodyN.x, 50000f, 1f)
                   && Near(agg.forceAeroBodyN.y, 0f)
                   && Near(agg.forceAeroBodyN.z, 0f), "P-001d",
                "and matches the hand-calculated force (50000, 0, 0) N",
                report, ref passed, ref failed);
            Record(Near(agg.momentAeroBodyNm.x, 0f, 1f)
                   && Near(agg.momentAeroBodyNm.y, -25000f, 1f)
                   && Near(agg.momentAeroBodyNm.z, 0f, 1f), "P-001e",
                "and the hand-calculated moment r x F = (0, -25000, 0) N*m: got "
                + V(agg.momentAeroBodyNm),
                report, ref passed, ref failed);

            Object.DestroyImmediate(system.gameObject);
            Object.DestroyImmediate(deck.gameObject);
        }

        // ==================================================================== P-002

        private static void P002_SymmetricTwinCancels(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P-002] two equal symmetric engines: forces add, lateral moments cancel");

            MavFixedSyntheticDeck deck =
                MakeDeck(SynthThrustN, MavThrustDataAuthority.SyntheticBench, true);

            // ONE profile object shared by both slots - the F-15 arrangement.
            MavEngineProfile shared = MakeProfile(
                "p002-shared-engine", deck, MavEnginePowerDynamicsLaw.InstantNoSourcedTransient);

            MavEngineInstallation left = MakeSlot(
                0, "left", shared, new Vector3(0f, -SynthLateralOffsetM, 0f), 0);
            MavEngineInstallation right = MakeSlot(
                1, "right", shared, new Vector3(0f, SynthLateralOffsetM, 0f), 1);

            MavPropulsionSystem system = MakeSystem(left, right);
            system.ResetEngineState(1f);

            MavPropulsiveLoads agg = system.Evaluate(LevelState(), Atmosphere(), 1f, 0.02f);

            Record(system.RuntimeCount == 2, "P-002pre",
                "two slots produced two runtimes (" + system.RuntimeCount + ")",
                report, ref passed, ref failed);
            Record(ReferenceEquals(left.engineProfile, right.engineProfile), "P-002pre-b",
                "and both slots share ONE engine profile object, which is the case that would break "
                + "if state lived on the profile",
                report, ref passed, ref failed);

            // Hand-calculated: two engines at 50000 N each along +X -> 100000 N total.
            Record(Near(agg.forceAeroBodyN.x, 100000f, 1f), "P-002",
                "total force is the sum: 2 x 50000 = 100000 N, got " + F(agg.forceAeroBodyN.x),
                report, ref passed, ref failed);

            // M_left  = (0,-3,0) x (50000,0,0) = (-3*0-0*0, 0*50000-0*0, 0*0-(-3)*50000) = (0,0,150000)
            // M_right = (0,+3,0) x (50000,0,0) = (0, 0, -150000)
            // Sum = 0. The individual moments are large; it is their cancellation that is the point.
            Record(agg.momentAeroBodyNm.magnitude < 1f, "P-002b",
                "installation moments cancel to zero: " + V(agg.momentAeroBodyNm),
                report, ref passed, ref failed);

            RequireDistinctRuntimes(system, 0, 1, "P-002pre-c", report, ref passed, ref failed);
            MavEngineLoadResult l = ResultOfSlot(system, 0);
            MavEngineLoadResult r = ResultOfSlot(system, 1);
            Record(Near(l.momentAeroBodyNm.z, 150000f, 1f)
                   && Near(r.momentAeroBodyNm.z, -150000f, 1f), "P-002c",
                "and they cancel because each is individually large and opposite, not because both "
                + "are zero: left Mz=" + F(l.momentAeroBodyNm.z)
                + ", right Mz=" + F(r.momentAeroBodyNm.z),
                report, ref passed, ref failed);

            Object.DestroyImmediate(system.gameObject);
            Object.DestroyImmediate(deck.gameObject);
        }

        // ==================================================================== P-003

        private static void P003_AsymmetricThrustYaws(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P-003] asymmetric thrust: one engine out produces a real yawing moment");

            MavFixedSyntheticDeck deck =
                MakeDeck(SynthThrustN, MavThrustDataAuthority.SyntheticBench, true);
            MavEngineProfile shared = MakeProfile(
                "p003-shared-engine", deck, MavEnginePowerDynamicsLaw.InstantNoSourcedTransient);

            MavEngineInstallation left = MakeSlot(
                0, "left", shared, new Vector3(0f, -SynthLateralOffsetM, 0f), 0);
            MavEngineInstallation right = MakeSlot(
                1, "right", shared, new Vector3(0f, SynthLateralOffsetM, 0f), 1);

            MavPropulsionSystem system = MakeSystem(left, right);
            system.ResetEngineState(1f);

            // Fail the RIGHT engine. Nothing anywhere adds a yaw term; the moment can only come from
            // the remaining engine's r x F.
            FailSlot(system, 1, true, report, ref passed, ref failed);

            MavPropulsiveLoads agg = system.Evaluate(LevelState(), Atmosphere(), 1f, 0.02f);

            // Hand-calculated. Only the left engine at (0, -3, 0) produces 50000 N along +X:
            //   F = (50000, 0, 0)
            //   M = (0,-3,0) x (50000,0,0) = (0, 0, +150000) N*m
            // Body Z is DOWN, so a positive Mz is a nose-RIGHT yaw. Losing the right engine yaws the
            // aircraft toward the dead engine, which is the correct physical direction.
            Record(Near(agg.forceAeroBodyN.x, 50000f, 1f), "P-003",
                "half the thrust remains: 50000 N, got " + F(agg.forceAeroBodyN.x),
                report, ref passed, ref failed);
            Record(Mathf.Abs(agg.momentAeroBodyNm.z) > 1f, "P-003b",
                "and the net yawing moment is NON-ZERO: Mz=" + F(agg.momentAeroBodyNm.z) + " N*m",
                report, ref passed, ref failed);
            Record(Near(agg.momentAeroBodyNm.z, 150000f, 1f), "P-003c",
                "matching the hand-calculated r x F = (0,-3,0) x (50000,0,0) -> Mz = +150000 N*m",
                report, ref passed, ref failed);
            Record(agg.momentAeroBodyNm.z > 0f, "P-003d",
                "with the SIGN yawing toward the dead engine: body Z is down, so positive Mz is nose-"
                + "right, and the failed engine is the right one",
                report, ref passed, ref failed);

            // The mirrored case. Sign must flip; a magnitude-only check would pass an implementation
            // that yawed the wrong way.
            system.ResetEngineState(1f);
            FailSlot(system, 0, true, report, ref passed, ref failed);
            MavPropulsiveLoads mirrored = system.Evaluate(LevelState(), Atmosphere(), 1f, 0.02f);

            Record(Near(mirrored.momentAeroBodyNm.z, -150000f, 1f), "P-003e",
                "failing the LEFT engine instead mirrors the moment exactly: Mz="
                + F(mirrored.momentAeroBodyNm.z) + " N*m",
                report, ref passed, ref failed);

            // And there must be no fake yaw anywhere: with both engines running the net is zero.
            system.ResetEngineState(1f);
            MavPropulsiveLoads both = system.Evaluate(LevelState(), Atmosphere(), 1f, 0.02f);
            Record(Mathf.Abs(both.momentAeroBodyNm.z) < 1f, "P-003f",
                "and with both engines running the yawing moment returns to zero, so the asymmetric "
                + "moment came from the geometry and not from an engine-out special case",
                report, ref passed, ref failed);

            Object.DestroyImmediate(system.gameObject);
            Object.DestroyImmediate(deck.gameObject);
        }

        // ==================================================================== P-004

        private static void P004_IndependentRuntimeState(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P-004] independent runtime state per engine");

            // The F-16 law is used here BECAUSE it has a real transient. With an instant law both
            // engines would reach their commanded power in one step and the test could not distinguish
            // independent state from shared state.
            MavF16EngineLawRegistrar.EnsureRegistered();

            MavEngineProfile shared = MakeProfile(
                "p004-shared-engine", null, MavEnginePowerDynamicsLaw.F16GarzaMorelliPowerState);

            MavEngineInstallation left = MakeSlot(0, "left", shared, Vector3.zero, 0);
            MavEngineInstallation right = MakeSlot(1, "right", shared, Vector3.zero, 1);

            MavPropulsionSystem system = MakeSystem(left, right);

            // Per-engine commands now arrive through the production command boundary, exactly as a
            // control layer would deliver them. P-015 exercises the same seam end to end; here it is
            // the mechanism for producing a divergence whose INDEPENDENCE is what is being checked.
            MavScriptedPropulsionCommandSource source = AttachCommandSource(system);

            // Start both from idle so the divergence below is produced by the throttles, not by the
            // initial condition.
            source.SetEngineThrottle(0, 0f);
            source.SetEngineThrottle(1, 0f);
            system.ResetEngineState(0f);

            RequireDistinctRuntimes(system, 0, 1, "P-004pre", report, ref passed, ref failed);

            // Channel 0 to full, channel 1 stays at idle.
            //
            // 200 steps = 4.0 s. The sourced model spools SLOWLY from cold: RTAU is 0.1 while the
            // power deficit exceeds 50 and only rises to 1.0 as the gap closes, so a cold engine at
            // full throttle passes 2.9% at 0.5 s, 11.0% at 2 s and 46.2% at 4 s. Choosing 4 s is
            // deliberate - it is long enough for a large separation and short enough that engine A
            // has NOT saturated at 100%, so the two states are distinguishable by value and not just
            // by one being pinned at a limit.
            source.SetEngineThrottle(0, 1f);
            source.SetEngineThrottle(1, 0f);

            for (int i = 0; i < 200; i++)
                system.Evaluate(LevelState(), Atmosphere(), 0f, 0.02f);

            float leftPower = PowerOf(system, 0);
            float rightPower = PowerOf(system, 1);

            Record(leftPower > 40f && leftPower < 60f, "P-004",
                "engine A spooled up on its own channel to " + F(leftPower)
                + "%, matching the sourced cold-start trajectory at 4 s (~46%) and NOT saturated",
                report, ref passed, ref failed);
            Record(rightPower < 0.01f, "P-004b",
                "while engine B stayed at exactly idle: " + F(rightPower) + "%",
                report, ref passed, ref failed);
            Record(Mathf.Abs(leftPower - rightPower) > 40f, "P-004c",
                "so the two power states are genuinely independent, differing by "
                + F(Mathf.Abs(leftPower - rightPower)) + " percentage points while sharing ONE "
                + "engine profile object",
                report, ref passed, ref failed);

            // The requirement names 90% and 40% specifically, so hit those exactly rather than
            // approximately. Both throttles are solved from the sourced gearing:
            //   channel 0: throttle > 0.77 branch -> 217.38*0.954 - 117.38 = 89.996 percent
            //   channel 1: throttle <= 0.77 branch -> 64.94*0.62         = 40.263 percent
            // 600 steps = 12 s is comfortably past settling for both (full power settles by 6 s).
            source.SetEngineThrottle(0, 0.954f);
            source.SetEngineThrottle(1, 0.62f);
            for (int i = 0; i < 600; i++)
                system.Evaluate(LevelState(), Atmosphere(), 0f, 0.02f);

            float a = PowerOf(system, 0);
            float b = PowerOf(system, 1);

            Record(Near(a, 89.996f, 0.05f), "P-004d",
                "engine A settles at the hand-calculated 217.38*0.954 - 117.38 = 89.996 percent: got "
                + F(a) + "%",
                report, ref passed, ref failed);
            Record(Near(b, 40.263f, 0.05f), "P-004e",
                "and engine B at 64.94*0.62 = 40.263 percent: got " + F(b) + "%",
                report, ref passed, ref failed);
            Record(Near(a, 89.996f, 0.05f) && Near(b, 40.263f, 0.05f), "P-004f",
                "so the requirement is met literally: approximately 90 percent on one engine and 40 "
                + "percent on the other, at the same simulation instant, from one shared profile",
                report, ref passed, ref failed);

            Object.DestroyImmediate(system.gameObject);
        }

        // ==================================================================== P-005

        private static void P005_F16PowerRegression(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P-005] F-16 Garza/Morelli power regression survives the migration");

            // Hand-calculated from the sourced equations, not from the code:
            //   throttle 0.5  <= 0.77  -> 64.94 * 0.5 = 32.47
            //   throttle 0.77 <= 0.77  -> 64.94 * 0.77 = 50.0038
            //   throttle 0.9  >  0.77  -> 217.38 * 0.9 - 117.38 = 195.642 - 117.38 = 78.262
            Record(Near(MavF16GarzaMorelliEngineDynamics.ThrottleToCommandedPowerPercent(0.5f),
                       32.47f, 0.01f), "P-005",
                "throttle 0.50 -> 64.94*0.50 = 32.47 percent",
                report, ref passed, ref failed);
            Record(Near(MavF16GarzaMorelliEngineDynamics.ThrottleToCommandedPowerPercent(0.77f),
                       50.0038f, 0.01f), "P-005b",
                "throttle 0.77 -> 64.94*0.77 = 50.0038 percent (military breakpoint)",
                report, ref passed, ref failed);
            Record(Near(MavF16GarzaMorelliEngineDynamics.ThrottleToCommandedPowerPercent(0.9f),
                       78.262f, 0.01f), "P-005c",
                "throttle 0.90 -> 217.38*0.90 - 117.38 = 78.262 percent",
                report, ref passed, ref failed);

            // RTAU schedule, hand-evaluated: dp=30 -> 1.9 - 0.036*30 = 1.9 - 1.08 = 0.82
            Record(Near(MavF16GarzaMorelliEngineDynamics.ReciprocalTimeConstant(10f), 1f), "P-005d",
                "RTAU(10) = 1.0 (dp <= 25 branch)", report, ref passed, ref failed);
            Record(Near(MavF16GarzaMorelliEngineDynamics.ReciprocalTimeConstant(30f), 0.82f, 0.001f),
                "P-005e", "RTAU(30) = 1.9 - 0.036*30 = 0.82 (linear branch)",
                report, ref passed, ref failed);
            Record(Near(MavF16GarzaMorelliEngineDynamics.ReciprocalTimeConstant(60f), 0.1f), "P-005f",
                "RTAU(60) = 0.1 (dp >= 50 branch)", report, ref passed, ref failed);

            // Power rate, hand-evaluated on each of the four branches:
            //   commanded 80, actual 70 : both >= 50 -> target=80, factor=5 -> 5*(80-70) = 50
            //   commanded 80, actual 20 : cmd>=50, act<50 -> target=60, dp=40,
            //                             RTAU=1.9-0.036*40=0.46 -> 0.46*(60-20) = 18.4
            //   commanded 20, actual 70 : cmd<50, act>=50 -> target=40, factor=5 -> 5*(40-70) = -150
            //   commanded 20, actual 30 : both <50 -> target=20, dp=-10 -> RTAU=1 -> 1*(20-30) = -10
            Record(Near(MavF16GarzaMorelliEngineDynamics.ComputePowerRatePercentPerSec(70f, 80f),
                       50f, 0.01f), "P-005g",
                "Pdot(actual 70, cmd 80) = 5*(80-70) = 50 %/s", report, ref passed, ref failed);
            Record(Near(MavF16GarzaMorelliEngineDynamics.ComputePowerRatePercentPerSec(20f, 80f),
                       18.4f, 0.01f), "P-005h",
                "Pdot(actual 20, cmd 80) = RTAU(40)*(60-20) = 0.46*40 = 18.4 %/s",
                report, ref passed, ref failed);
            Record(Near(MavF16GarzaMorelliEngineDynamics.ComputePowerRatePercentPerSec(70f, 20f),
                       -150f, 0.01f), "P-005i",
                "Pdot(actual 70, cmd 20) = 5*(40-70) = -150 %/s", report, ref passed, ref failed);
            Record(Near(MavF16GarzaMorelliEngineDynamics.ComputePowerRatePercentPerSec(30f, 20f),
                       -10f, 0.01f), "P-005j",
                "Pdot(actual 30, cmd 20) = RTAU(-10)*(20-30) = 1*(-10) = -10 %/s",
                report, ref passed, ref failed);

            // The legacy entry points must still agree, because existing suites call them. If the
            // forwarding were wrong these would diverge.
            Record(Near(MavF16EnginePowerModel.ThrottleToCommandedPowerPercent(0.9f),
                       MavF16GarzaMorelliEngineDynamics.ThrottleToCommandedPowerPercent(0.9f)),
                "P-005k",
                "the transitional MavF16EnginePowerModel entry point forwards to the same equation",
                report, ref passed, ref failed);
            Record(Near(MavF16EnginePowerModel.ComputePowerRatePercentPerSec(20f, 80f), 18.4f, 0.01f)
                   && Near(MavF16EnginePowerModel.ReciprocalTimeConstant(30f), 0.82f, 0.001f)
                   && Near(MavF16EnginePowerModel.StepActualPowerPercent(20f, 80f, 0.02f),
                          20f + 18.4f * 0.02f, 0.001f),
                "P-005l",
                "and so do its rate, RTAU and integration entry points",
                report, ref passed, ref failed);

            // The same equations reached THROUGH the shared runtime must give the same answer. This is
            // what proves the migration preserved behaviour rather than merely compiling.
            MavF16EngineLawRegistrar.EnsureRegistered();
            MavPropulsionInstallationProfile f16 =
                MavF16PropulsionInstallation.CreateInstallation(null);
            MavPropulsionSystem system = MakeSystem(f16.engines[0]);
            system.ResetEngineState(0f);

            float expected = 0f;
            bool matched = true;
            for (int i = 0; i < 40; i++)
            {
                system.Evaluate(LevelState(), Atmosphere(), 0.9f, 0.02f);

                // Independent integration using the sourced statics directly.
                expected = MavF16GarzaMorelliEngineDynamics.StepActualPower(
                    expected,
                    MavF16GarzaMorelliEngineDynamics.ThrottleToCommandedPowerPercent(0.9f),
                    0.02f);

                if (!Near(PowerOf(system, 0), expected, 1e-4f))
                    matched = false;
            }

            Record(matched, "P-005m",
                "the shared runtime reproduces the sourced power trajectory step for step over 40 "
                + "steps, ending at " + F(PowerOf(system, 0)) + "% vs expected "
                + F(expected) + "%",
                report, ref passed, ref failed);

            Object.DestroyImmediate(system.gameObject);
        }

        // ==================================================================== P-006

        private static void P006_UnavailableDeckNoFakeThrust(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P-006] unavailable thrust deck: no fabricated thrust");

            MavF16EngineLawRegistrar.EnsureRegistered();

            // No deck at all - the real F-16 condition today.
            MavPropulsionInstallationProfile f16 =
                MavF16PropulsionInstallation.CreateInstallation(null);
            MavPropulsionSystem system = MakeSystem(f16.engines[0]);
            system.ResetEngineState(1f);

            MavPropulsiveLoads loads = system.Evaluate(LevelState(), Atmosphere(), 1f, 0.02f);

            Record(loads.forceAeroBodyN == Vector3.zero, "P-006",
                "force is exactly zero with no deck: " + V(loads.forceAeroBodyN),
                report, ref passed, ref failed);
            Record(loads.momentAeroBodyNm == Vector3.zero, "P-006b",
                "moment is exactly zero: " + V(loads.momentAeroBodyNm),
                report, ref passed, ref failed);
            Record(Near(loads.reportedThrustN, 0f), "P-006c",
                "reported thrust is exactly zero", report, ref passed, ref failed);
            Record(!loads.hasAuthoritativeData, "P-006d",
                "and the aggregate says so rather than reporting an authoritative zero",
                report, ref passed, ref failed);

            // The sourced power state must still advance. Zero thrust is a data gap, not a reason to
            // stop modelling what IS sourced.
            Record(PowerOf(system, 0) > 0f, "P-006e",
                "while the sourced power state still advances: "
                + F(PowerOf(system, 0)) + "%",
                report, ref passed, ref failed);

            // A deck that exists but declares Unavailable must behave the same way.
            MavFixedSyntheticDeck unavailable =
                MakeDeck(99999f, MavThrustDataAuthority.Unavailable, true);
            MavPropulsionInstallationProfile withDeadDeck =
                MavF16PropulsionInstallation.CreateInstallation(unavailable);
            MavPropulsionSystem system2 = MakeSystem(withDeadDeck.engines[0]);
            system2.ResetEngineState(1f);
            MavPropulsiveLoads loads2 = system2.Evaluate(LevelState(), Atmosphere(), 1f, 0.02f);

            Record(loads2.forceAeroBodyN == Vector3.zero && Near(loads2.reportedThrustN, 0f),
                "P-006f",
                "a deck declaring Unavailable yields zero thrust even though it holds a 99999 N "
                + "number, so the declaration governs and not the value",
                report, ref passed, ref failed);

            Object.DestroyImmediate(system.gameObject);
            Object.DestroyImmediate(system2.gameObject);
            Object.DestroyImmediate(unavailable.gameObject);
        }

        // ==================================================================== P-007

        private static void P007_ProvenanceCannotLaunder(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P-007] synthetic input cannot become authoritative aggregate output");

            MavFixedSyntheticDeck synthetic =
                MakeDeck(SynthThrustN, MavThrustDataAuthority.SyntheticBench, true);
            MavFixedSyntheticDeck authoritative =
                MakeDeck(SynthThrustN, MavThrustDataAuthority.Authoritative, true);

            MavEngineProfile syntheticEngine = MakeProfile(
                "p007-synthetic", synthetic, MavEnginePowerDynamicsLaw.InstantNoSourcedTransient);
            MavEngineProfile authoritativeEngine = MakeProfile(
                "p007-authoritative", authoritative,
                MavEnginePowerDynamicsLaw.InstantNoSourcedTransient);

            // Both synthetic.
            MavPropulsionSystem bothSynthetic = MakeSystem(
                MakeSlot(0, "left", syntheticEngine, new Vector3(0f, -3f, 0f), 0),
                MakeSlot(1, "right", syntheticEngine, new Vector3(0f, 3f, 0f), 1));
            bothSynthetic.ResetEngineState(1f);
            MavPropulsiveLoads a = bothSynthetic.Evaluate(LevelState(), Atmosphere(), 1f, 0.02f);

            Record(!a.hasAuthoritativeData, "P-007",
                "two synthetic engines do not sum to an authoritative aggregate",
                report, ref passed, ref failed);
            Record(Near(a.reportedThrustN, 100000f, 1f), "P-007b",
                "even though they produce real force (" + F(a.reportedThrustN)
                + " N), which is the point: the numbers exist but are labelled",
                report, ref passed, ref failed);

            // MIXED: one authoritative engine, one synthetic. The aggregate must NOT claim authority,
            // because the summed force contains the synthetic half.
            MavPropulsionSystem mixed = MakeSystem(
                MakeSlot(0, "left", authoritativeEngine, new Vector3(0f, -3f, 0f), 0),
                MakeSlot(1, "right", syntheticEngine, new Vector3(0f, 3f, 0f), 1));
            mixed.ResetEngineState(1f);
            MavPropulsiveLoads m = mixed.Evaluate(LevelState(), Atmosphere(), 1f, 0.02f);

            Record(!m.hasAuthoritativeData, "P-007c",
                "and a MIXED pair is not authoritative either: one sourced engine cannot launder the "
                + "other half of the summed force",
                report, ref passed, ref failed);
            Record(mixed.debugAuthoritativeEngineCount == 1
                   && mixed.debugContributingEngineCount == 2, "P-007d",
                "with the telemetry naming the split: " + mixed.debugAuthoritativeEngineCount
                + " of " + mixed.debugContributingEngineCount + " engines authoritative",
                report, ref passed, ref failed);

            // Both authoritative -> the aggregate may finally say so.
            MavPropulsionSystem bothAuth = MakeSystem(
                MakeSlot(0, "left", authoritativeEngine, new Vector3(0f, -3f, 0f), 0),
                MakeSlot(1, "right", authoritativeEngine, new Vector3(0f, 3f, 0f), 1));
            bothAuth.ResetEngineState(1f);
            MavPropulsiveLoads b = bothAuth.Evaluate(LevelState(), Atmosphere(), 1f, 0.02f);

            Record(b.hasAuthoritativeData, "P-007e",
                "while two authoritative engines DO produce an authoritative aggregate, so the check "
                + "above is not simply always-false",
                report, ref passed, ref failed);

            // Live-flight acceptance is stricter still: it needs a declared envelope and measured
            // installation geometry, which the synthetic profiles do not have.
            Record(!bothAuth.IsAcceptableForLiveFlight, "P-007f",
                "yet still not acceptable for live flight, because the engine profiles declare no "
                + "source envelope: " + bothAuth.ThrustDataStatus,
                report, ref passed, ref failed);

            Object.DestroyImmediate(bothSynthetic.gameObject);
            Object.DestroyImmediate(mixed.gameObject);
            Object.DestroyImmediate(bothAuth.gameObject);
            Object.DestroyImmediate(synthetic.gameObject);
            Object.DestroyImmediate(authoritative.gameObject);
        }

        // ==================================================================== P-008

        private static void P008_ZeroEngines(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P-008] zero engines: defined, safe, no exception");

            MavPropulsionSystem system = MakeSystem(new MavEngineInstallation[0]);
            system.ResetEngineState(1f);

            MavPropulsiveLoads loads = system.Evaluate(LevelState(), Atmosphere(), 1f, 0.02f);

            Record(loads.forceAeroBodyN == Vector3.zero
                   && loads.momentAeroBodyNm == Vector3.zero, "P-008",
                "an unpowered aircraft produces exactly zero loads",
                report, ref passed, ref failed);
            Record(!float.IsNaN(loads.powerState01) && Near(loads.powerState01, 0f), "P-008b",
                "power state is 0, not NaN from an empty average",
                report, ref passed, ref failed);
            Record(!loads.hasAuthoritativeData, "P-008c",
                "and no engines is not reported as authoritative thrust",
                report, ref passed, ref failed);
            Record(system.IsBuilt && system.RuntimeCount == 0, "P-008d",
                "the system still builds cleanly with an empty installation, because unpowered is a "
                + "configuration Phase 5C flies deliberately",
                report, ref passed, ref failed);

            string reason;
            Record(!system.installation.IsAcceptableForLiveFlight(out reason), "P-008e",
                "but 'no engines' is not 'engines ready': " + reason,
                report, ref passed, ref failed);

            Object.DestroyImmediate(system.gameObject);
        }

        // ==================================================================== P-009

        private static void P009_DisabledInstallation(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P-009] disabled installation contributes nothing physical");

            MavFixedSyntheticDeck deck =
                MakeDeck(SynthThrustN, MavThrustDataAuthority.SyntheticBench, true);
            MavEngineProfile profile = MakeProfile(
                "p009-engine", deck, MavEnginePowerDynamicsLaw.InstantNoSourcedTransient);

            MavEngineInstallation left = MakeSlot(
                0, "left", profile, new Vector3(0f, -3f, 0f), 0);
            MavEngineInstallation right = MakeSlot(
                1, "right", profile, new Vector3(0f, 3f, 0f), 1);
            right.enabled = false;

            MavPropulsionSystem system = MakeSystem(left, right);
            system.ResetEngineState(1f);
            MavPropulsiveLoads loads = system.Evaluate(LevelState(), Atmosphere(), 1f, 0.02f);

            Record(Near(loads.forceAeroBodyN.x, 50000f, 1f), "P-009",
                "only the enabled engine contributes force: " + F(loads.forceAeroBodyN.x) + " N",
                report, ref passed, ref failed);
            Record(system.debugContributingEngineCount == 1, "P-009b",
                "and the disabled slot is not counted as contributing ("
                + system.debugContributingEngineCount + ")",
                report, ref passed, ref failed);
            Record(Near(loads.momentAeroBodyNm.z, 150000f, 1f), "P-009c",
                "so the moment is the single-engine r x F rather than a cancelled pair: Mz="
                + F(loads.momentAeroBodyNm.z) + " N*m",
                report, ref passed, ref failed);

            MavEngineLoadResult disabled = ResultOfSlot(system, 1);
            Record(disabled.forceAeroBodyN == Vector3.zero
                   && disabled.momentAeroBodyNm == Vector3.zero
                   && !disabled.running, "P-009d",
                "and the disabled engine's own result is zero and marked not running: "
                + disabled.statusReason,
                report, ref passed, ref failed);

            Object.DestroyImmediate(system.gameObject);
            Object.DestroyImmediate(deck.gameObject);
        }

        // ==================================================================== P-010

        private static void P010_FiniteGuards(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P-010] malformed input fails closed");

            MavFixedSyntheticDeck deck =
                MakeDeck(SynthThrustN, MavThrustDataAuthority.SyntheticBench, true);
            MavEngineProfile profile = MakeProfile(
                "p010-engine", deck, MavEnginePowerDynamicsLaw.InstantNoSourcedTransient);
            MavPropulsionSystem system = MakeSystem(MakeSlot(0, "single", profile, Vector3.zero, 0));
            system.ResetEngineState(0.5f);

            MavPropulsiveLoads nanThrottle =
                system.Evaluate(LevelState(), Atmosphere(), float.NaN, 0.02f);
            Record(IsFinite(nanThrottle.forceAeroBodyN) && IsFinite(nanThrottle.momentAeroBodyNm),
                "P-010",
                "a NaN throttle produces finite loads: " + V(nanThrottle.forceAeroBodyN),
                report, ref passed, ref failed);
            Record(nanThrottle.forceAeroBodyN == Vector3.zero, "P-010b",
                "specifically ZERO rather than a clamped guess, because a NaN command has no "
                + "defensible interpretation",
                report, ref passed, ref failed);

            MavPropulsiveLoads infDt =
                system.Evaluate(LevelState(), Atmosphere(), 0.5f, float.PositiveInfinity);
            Record(IsFinite(infDt.forceAeroBodyN) && IsFinite(infDt.momentAeroBodyNm), "P-010c",
                "an infinite deltaTime produces finite loads", report, ref passed, ref failed);
            Record(!float.IsNaN(PowerOf(system, 0))
                   && !float.IsInfinity(PowerOf(system, 0)), "P-010d",
                "and does not poison the engine's power state: "
                + F(PowerOf(system, 0)) + "%",
                report, ref passed, ref failed);

            // A deck returning NaN must be rejected, not summed.
            MavFixedSyntheticDeck nanDeck =
                MakeDeck(float.NaN, MavThrustDataAuthority.SyntheticBench, true);
            MavEngineProfile nanProfile = MakeProfile(
                "p010-nan", nanDeck, MavEnginePowerDynamicsLaw.InstantNoSourcedTransient);
            MavPropulsionSystem nanSystem =
                MakeSystem(MakeSlot(0, "single", nanProfile, Vector3.zero, 0));
            nanSystem.ResetEngineState(1f);
            MavPropulsiveLoads nanLoads = nanSystem.Evaluate(LevelState(), Atmosphere(), 1f, 0.02f);

            Record(IsFinite(nanLoads.forceAeroBodyN) && nanLoads.forceAeroBodyN == Vector3.zero,
                "P-010e",
                "a deck returning NaN thrust is rejected at the engine rather than summed into the "
                + "load set, where it would have reached the Rigidbody",
                report, ref passed, ref failed);
            Record(!nanLoads.hasAuthoritativeData, "P-010f",
                "and the result is not claimed as authoritative", report, ref passed, ref failed);

            // A zero-length thrust direction is a configuration error, and IsValid must say so.
            MavEngineInstallation degenerate =
                MakeSlot(0, "bad", profile, Vector3.zero, 0);
            degenerate.thrustDirectionAeroBody = Vector3.zero;
            string reason;
            Record(!degenerate.IsValid(out reason), "P-010g",
                "a zero-length thrust direction is rejected by validation: " + reason,
                report, ref passed, ref failed);

            MavEngineInstallation nanPos = MakeSlot(0, "bad", profile, Vector3.zero, 0);
            nanPos.positionAeroBodyM = new Vector3(float.NaN, 0f, 0f);
            Record(!nanPos.IsValid(out reason), "P-010h",
                "as is a non-finite mount position: " + reason,
                report, ref passed, ref failed);

            Object.DestroyImmediate(system.gameObject);
            Object.DestroyImmediate(nanSystem.gameObject);
            Object.DestroyImmediate(deck.gameObject);
            Object.DestroyImmediate(nanDeck.gameObject);
        }

        // ==================================================================== P-011

        private static void P011_ApplicationOwnership(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P-011] no engine code writes the Rigidbody");

            // Source-level, because this is a claim about what the code CANNOT do, and a behavioural
            // test can only show that it did not happen in the cases it happened to try.
            //
            // Reuses the PRODUCTION scan rather than re-implementing a file walk. Two reasons: its
            // token list is broader than the obvious force calls - it also catches a direct
            // velocity/rotation assignment, which bypasses the load path just as completely - and its
            // line classifier strips comments and string literals first. An earlier version of this
            // check did its own naive substring match and flagged the engine files' own
            // documentation, which says they hold no Rigidbody reference. Matching prose is exactly
            // the failure mode the production stripper exists to avoid.
            string root = global::System.IO.Path.Combine(
                Application.dataPath, "MaverickFresh/Scripts/FlightDynamics");

            string[] engineFiles =
            {
                "Core/MavEngineRuntime.cs",
                "Core/MavEngineProfile.cs",
                "Core/MavEngineInstallation.cs",
                "Core/MavEnginePowerDynamics.cs",
                "Core/MavPropulsionSystem.cs",
                "F16/MavF16GarzaMorelliEngineDynamics.cs",
                "F16/MavF16PropulsionInstallation.cs",
                "F16/MavF16PropulsionSystem.cs",
                "F15/MavF15PropulsionSkeleton.cs"
            };

            int checkedFiles = 0;
            int offenders = 0;
            StringBuilder detail = new StringBuilder();

            for (int i = 0; i < engineFiles.Length; i++)
            {
                string path = global::System.IO.Path.Combine(root, engineFiles[i]);
                if (!global::System.IO.File.Exists(path))
                {
                    detail.Append(" MISSING:").Append(engineFiles[i]);
                    continue;
                }

                checkedFiles++;
                string[] lines = global::System.IO.File.ReadAllLines(path);

                for (int k = 0; k < lines.Length; k++)
                {
                    if (MavFlightDynamicsOwnershipScan.IsOwnershipViolation(lines[k]))
                    {
                        offenders++;
                        detail.Append(' ').Append(engineFiles[i]).Append(":L").Append(k + 1);
                    }
                }
            }

            Record(checkedFiles == engineFiles.Length, "P-011pre",
                "found all " + engineFiles.Length + " engine/propulsion source files (" + checkedFiles
                + ") - a zero here would mean this check had gone vacuous" + detail,
                report, ref passed, ref failed);
            Record(offenders == 0, "P-011",
                "no engine or propulsion file writes rigid-body motion state - not a force call, not "
                + "a torque call, and not a direct velocity or rotation assignment ("
                + offenders + " offender(s))" + detail,
                report, ref passed, ref failed);

            // Prove the classifier can still fail, so the clean result above means something. These
            // are strings handed to the production rule, not edits to any file.
            Record(MavFlightDynamicsOwnershipScan.IsOwnershipViolation(
                       "            rb.AddForce(force, ForceMode.Force);"), "P-011pre-b",
                "and the rule it uses does flag a real AddForce call",
                report, ref passed, ref failed);
            Record(MavFlightDynamicsOwnershipScan.IsOwnershipViolation(
                       "            rb.linearVelocity = v;"), "P-011pre-c",
                "and a direct velocity assignment, which is the other way to bypass the load path",
                report, ref passed, ref failed);
            Record(!MavFlightDynamicsOwnershipScan.IsOwnershipViolation(
                       "            // this file never calls AddForce and holds no Rigidbody"),
                "P-011pre-d",
                "while NOT flagging a comment that mentions them, which is why the production "
                + "stripper is used here instead of a raw substring match",
                report, ref passed, ref failed);

            // And the boundary still exists where it was: SixDoF remains the only applier.
            string sixDoF = global::System.IO.Path.Combine(root, "Core/MavSixDoFBody.cs");
            Record(global::System.IO.File.Exists(sixDoF)
                   && global::System.IO.File.ReadAllText(sixDoF).Contains("AddPropulsive"), "P-011b",
                "while MavSixDoFBody still owns the single propulsive contribution, so the load-"
                + "application boundary did not move",
                report, ref passed, ref failed);

            Record(!global::System.IO.File.ReadAllText(
                       global::System.IO.Path.Combine(root, "Core/MavPropulsionSystem.cs"))
                   .Contains("yaw"), "P-011c",
                "and the aggregator contains no yaw special case: the asymmetric moment in P-003 came "
                + "only from r x F",
                report, ref passed, ref failed);
        }

        // ==================================================================== P-012

        private static void P012_MomentTransform(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P-012] r x F with hand-calculated vectors");

            // Pure cross products, hand-evaluated. These pin the convention itself, independently of
            // any engine, deck or power law.
            //
            // r = (0, 2, 0), F = (10, 0, 0)
            // r x F = (ry*Fz - rz*Fy, rz*Fx - rx*Fz, rx*Fy - ry*Fx)
            //       = (2*0 - 0*0, 0*10 - 0*0, 0*0 - 2*10) = (0, 0, -20)
            Vector3 m1 = Vector3.Cross(new Vector3(0f, 2f, 0f), new Vector3(10f, 0f, 0f));
            Record(Near(m1.x, 0f) && Near(m1.y, 0f) && Near(m1.z, -20f), "P-012",
                "r=(0,2,0) x F=(10,0,0) = (0,0,-20): a right-side engine yaws nose-left, got " + V(m1),
                report, ref passed, ref failed);

            // r = (0, -2, 0), F = (10, 0, 0) -> (0, 0, +20)
            Vector3 m2 = Vector3.Cross(new Vector3(0f, -2f, 0f), new Vector3(10f, 0f, 0f));
            Record(Near(m2.z, 20f), "P-012b",
                "r=(0,-2,0) x F=(10,0,0) = (0,0,+20): mirrored exactly, got " + V(m2),
                report, ref passed, ref failed);

            // r = (0, 0, -1), F = (100, 0, 0):
            //   = (0*0 - (-1)*0, (-1)*100 - 0*0, 0*0 - 0*100) = (0, -100, 0)
            // An engine mounted ABOVE the CG (body Z down, so negative Z is up) pitches the nose down.
            Vector3 m3 = Vector3.Cross(new Vector3(0f, 0f, -1f), new Vector3(100f, 0f, 0f));
            Record(Near(m3.x, 0f) && Near(m3.y, -100f) && Near(m3.z, 0f), "P-012c",
                "r=(0,0,-1) x F=(100,0,0) = (0,-100,0): an engine above the CG pitches nose-down, "
                + "got " + V(m3),
                report, ref passed, ref failed);

            // Thrust through the CG produces no moment at all.
            Vector3 m4 = Vector3.Cross(Vector3.zero, new Vector3(50000f, 0f, 0f));
            Record(m4 == Vector3.zero, "P-012d",
                "r=(0,0,0) gives exactly zero moment, which is why the F-16's undeclared centreline "
                + "mount reproduces the old zero-moment behaviour",
                report, ref passed, ref failed);

            // Now the same arithmetic THROUGH the production engine path, so the convention checked
            // above is the one the runtime actually uses.
            MavFixedSyntheticDeck deck = MakeDeck(10f, MavThrustDataAuthority.SyntheticBench, true);
            MavEngineProfile profile = MakeProfile(
                "p012-engine", deck, MavEnginePowerDynamicsLaw.InstantNoSourcedTransient);
            MavPropulsionSystem system = MakeSystem(
                MakeSlot(0, "right", profile, new Vector3(0f, 2f, 0f), 0));
            system.ResetEngineState(1f);
            MavPropulsiveLoads loads = system.Evaluate(LevelState(), Atmosphere(), 1f, 0.02f);

            Record(Near(loads.momentAeroBodyNm.z, -20f, 1e-2f), "P-012e",
                "and the runtime reproduces the first case exactly: Mz="
                + F(loads.momentAeroBodyNm.z) + " N*m for a 10 N engine 2 m to the right",
                report, ref passed, ref failed);

            Object.DestroyImmediate(system.gameObject);
            Object.DestroyImmediate(deck.gameObject);
        }

        // ==================================================================== P-013

        private static void P013_F15SkeletonKeepsDataUnavailable(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P-013] F-15 skeleton describes a twin without inventing engine data");

            MavPropulsionInstallationProfile f15 = MavF15PropulsionSkeleton.CreateTwinSkeleton();

            Record(f15.EngineCount == 2, "P-013",
                "the architecture describes two engine slots (" + f15.EngineCount + ")",
                report, ref passed, ref failed);
            Record(ReferenceEquals(f15.engines[0].engineProfile, f15.engines[1].engineProfile),
                "P-013b",
                "both slots share ONE engine profile object, as a twin with matching engines should",
                report, ref passed, ref failed);
            Record(f15.engines[0].throttleChannel != f15.engines[1].throttleChannel, "P-013c",
                "on separate throttle channels, so differential throttle needs no new architecture",
                report, ref passed, ref failed);

            MavPropulsionSystem system = MakeSystem(f15.engines[0], f15.engines[1]);
            Record(system.RuntimeCount == 2, "P-013d",
                "and the shared profile still yields two runtimes ("
                + system.RuntimeCount + ")",
                report, ref passed, ref failed);
            RequireDistinctRuntimes(system, 0, 1, "P-013d-b", report, ref passed, ref failed);

            // Now the honesty half.
            Record(f15.engines[0].engineProfile.provenance == MavEngineDataProvenance.Unavailable,
                "P-013e",
                "the engine profile's provenance is Unavailable: no F-15 engine variant is frozen",
                report, ref passed, ref failed);
            Record(f15.engines[0].engineProfile.powerDynamicsLaw
                   != MavEnginePowerDynamicsLaw.F16GarzaMorelliPowerState, "P-013f",
                "and it does NOT borrow the F-16 power law, which is F-16 reference-engine behaviour "
                + "and unproven for an F100-family engine",
                report, ref passed, ref failed);
            Record(f15.engines[0].engineProfile.thrustDeck == null, "P-013g",
                "no thrust deck is attached, so thrust is exactly zero",
                report, ref passed, ref failed);
            Record(!f15.engines[0].geometryDeclared && !f15.engines[1].geometryDeclared, "P-013h",
                "and the mount coordinates are UNDECLARED rather than guessed",
                report, ref passed, ref failed);

            system.ResetEngineState(1f);
            MavPropulsiveLoads loads = system.Evaluate(LevelState(), Atmosphere(), 1f, 0.02f);
            Record(loads.forceAeroBodyN == Vector3.zero && loads.momentAeroBodyNm == Vector3.zero,
                "P-013i",
                "so the skeleton flies nowhere: zero force, zero moment",
                report, ref passed, ref failed);

            string reason;
            Record(!f15.IsAcceptableForLiveFlight(out reason), "P-013j",
                "and it is not acceptable for live flight: " + reason,
                report, ref passed, ref failed);

            Object.DestroyImmediate(system.gameObject);
        }

        // ==================================================================== P-014

        /// <summary>
        /// An unresolvable power law must fail CLOSED, not fall back to a different law.
        ///
        /// This case exists because a probe that made Resolve substitute the instant law left the
        /// whole suite green: every other test registers the F-16 law first, so the fallback branch
        /// was never reached and the documented refusal was never actually exercised.
        ///
        /// An out-of-range enum value is the honest way to reach it without adding a production
        /// de-registration hook that nothing else would want. It is also a real scenario rather than a
        /// contrived one: a serialized asset can hold a stale numeric enum value after a refactor, and
        /// what must NOT happen then is the aircraft quietly flying some other engine's dynamics.
        /// </summary>
        private static void P014_UnresolvableLawFailsClosed(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P-014] an unresolvable power law fails closed");

            MavEnginePowerDynamicsLaw unknown = (MavEnginePowerDynamicsLaw)999;

            Record(!MavEnginePowerDynamicsFactory.CanResolve(unknown), "P-014",
                "CanResolve reports an unknown law as unresolvable",
                report, ref passed, ref failed);
            Record(MavEnginePowerDynamicsFactory.Resolve(unknown) == null, "P-014b",
                "and Resolve returns NULL rather than substituting a law the profile did not declare",
                report, ref passed, ref failed);

            // The known laws must still resolve, or the check above would pass for the wrong reason.
            MavF16EngineLawRegistrar.EnsureRegistered();
            Record(MavEnginePowerDynamicsFactory.Resolve(
                       MavEnginePowerDynamicsLaw.InstantNoSourcedTransient) != null
                   && MavEnginePowerDynamicsFactory.Resolve(
                       MavEnginePowerDynamicsLaw.F16GarzaMorelliPowerState) != null, "P-014c",
                "while both declared laws DO resolve, so P-014b is not simply always-null",
                report, ref passed, ref failed);

            // And the system must refuse to build rather than run on a substituted law.
            MavEngineProfile profile = MakeProfile("p014-unknown-law", null,
                MavEnginePowerDynamicsLaw.InstantNoSourcedTransient);
            profile.powerDynamicsLaw = unknown;
            profile.powerDynamicsProvenance = MavEngineDataProvenance.PublicReference;

            GameObject host = new GameObject("p014-system");
            MavPropulsionSystem system = host.AddComponent<MavPropulsionSystem>();
            system.installation = new MavPropulsionInstallationProfile();
            system.installation.installationId = "p014-installation";
            system.installation.engines = new MavEngineInstallation[]
            {
                MakeSlot(0, "single", profile, Vector3.zero, 0)
            };

            string error;
            bool built = system.Build(true, out error);

            Record(!built, "P-014d",
                "MavPropulsionSystem.Build REFUSES the installation: " + error,
                report, ref passed, ref failed);
            Record(!built && error.Contains("refusing to substitute"), "P-014e",
                "naming the refusal explicitly, so the reason is legible in a log rather than "
                + "appearing as an aircraft with mysteriously wrong throttle response",
                report, ref passed, ref failed);
            Record(system.RuntimeCount == 0, "P-014f",
                "and no runtime is created (" + system.RuntimeCount + "), so a later Evaluate cannot "
                + "accidentally run the substituted law",
                report, ref passed, ref failed);

            MavPropulsiveLoads loads = system.Evaluate(LevelState(), Atmosphere(), 1f, 0.02f);
            Record(loads.forceAeroBodyN == Vector3.zero
                   && loads.momentAeroBodyNm == Vector3.zero
                   && !loads.hasAuthoritativeData, "P-014g",
                "Evaluate on the refused system produces exactly zero loads rather than throwing",
                report, ref passed, ref failed);

            Object.DestroyImmediate(host);
        }

        // ==================================================================== P-015

        /// <summary>
        /// Independent left/right engine commands through the REAL public command API.
        ///
        /// This is the case P0 could not satisfy. P0 proved independent engine STATE by calling a
        /// side-door setter on the propulsion component; it did not prove independent engine COMMANDS,
        /// because nothing in the pilot pipeline could reach that setter. The whole chain -
        /// MavPilotCommand, the control law, MavControlInput, the actuator, and
        /// MavPropulsionModelBase.Evaluate - carries one scalar throttle.
        ///
        /// Here a real MavPropulsionCommandSourceBase component publishes left 1.0 and right 0.25, the
        /// system is driven through its normal Evaluate, and the two engines are checked to diverge.
        /// No runtime internals are touched.
        /// </summary>
        private static void P015_IndependentCommandsThroughPublicApi(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P-015] independent engine commands through the public command API");

            MavF16EngineLawRegistrar.EnsureRegistered();

            MavEngineProfile shared = MakeProfile(
                "p015-shared-engine", null, MavEnginePowerDynamicsLaw.F16GarzaMorelliPowerState);

            MavPropulsionSystem system = MakeSystem(
                MakeSlot(0, "left", shared, new Vector3(0f, -SynthLateralOffsetM, 0f), 0),
                MakeSlot(1, "right", shared, new Vector3(0f, SynthLateralOffsetM, 0f), 1));

            MavScriptedPropulsionCommandSource source = AttachCommandSource(system);
            source.SetEngineThrottle(0, 0f);
            source.SetEngineThrottle(1, 0f);
            system.ResetEngineState(0f);

            // The requested condition: left full, right quarter.
            source.SetEngineThrottle(0, 1.0f);
            source.SetEngineThrottle(1, 0.25f);

            // Driven through the ordinary Evaluate the SixDoF body calls, with the pipeline's single
            // scalar throttle deliberately set to something that matches NEITHER engine - so if the
            // command boundary were ignored, both engines would land on 0.5 and the test would fail.
            for (int i = 0; i < 900; i++)
                system.Evaluate(LevelState(), Atmosphere(), 0.5f, 0.02f);

            float left = PowerOf(system, 0);
            float right = PowerOf(system, 1);

            // Hand-calculated from the sourced gearing, both on the throttle <= 0.77 / > 0.77 branches:
            //   left  throttle 1.00 -> 217.38*1.00 - 117.38 = 100.000 percent
            //   right throttle 0.25 -> 64.94*0.25           =  16.235 percent
            // The pipeline scalar 0.5 would have given 64.94*0.5 = 32.47 for both.
            Record(Near(left, 100f, 0.05f), "P-015",
                "left engine follows its OWN command to 100.000 percent: got " + F(left),
                report, ref passed, ref failed);
            Record(Near(right, 16.235f, 0.05f), "P-015b",
                "right engine follows its own command to 64.94*0.25 = 16.235 percent: got " + F(right),
                report, ref passed, ref failed);
            Record(!Near(left, 32.47f, 1f) && !Near(right, 32.47f, 1f), "P-015c",
                "and NEITHER landed on 32.47 percent, which is what the single pipeline throttle of "
                + "0.5 would have produced - so the per-engine command really was honoured",
                report, ref passed, ref failed);
            Record(Mathf.Abs(left - right) > 80f, "P-015d",
                "the two commanded states differ by " + F(Mathf.Abs(left - right))
                + " percentage points at the same simulation instant",
                report, ref passed, ref failed);

            // And it went through a component seam, not internal mutation.
            Record(system.commandSource != null
                   && system.commandSource is MavPropulsionCommandSourceBase, "P-015e",
                "delivered by a MavPropulsionCommandSourceBase component ('"
                + system.commandSource.CommandSourceName + "'), which is the seam a real control "
                + "layer uses - no runtime internals were mutated",
                report, ref passed, ref failed);
            Record(system.Command.Mode == MavPropulsionCommandMode.PerEngineThrottle, "P-015f",
                "with the command reporting PerEngineThrottle mode",
                report, ref passed, ref failed);

            Object.DestroyImmediate(system.gameObject);
            Object.DestroyImmediate(shared);
        }

        // ==================================================================== P-016

        /// <summary>
        /// Linked throttle broadcasts deterministically, and is the DEFAULT.
        ///
        /// The F-16 must stay trivial and a normal F-15 must fly linked, so "no command source" has to
        /// mean "every engine follows the pipeline throttle" - not "engines read a stale array".
        /// </summary>
        private static void P016_LinkedThrottleBroadcasts(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P-016] linked throttle is the default and broadcasts deterministically");

            MavF16EngineLawRegistrar.EnsureRegistered();
            MavEngineProfile shared = MakeProfile(
                "p016-shared-engine", null, MavEnginePowerDynamicsLaw.F16GarzaMorelliPowerState);

            MavPropulsionSystem system = MakeSystem(
                MakeSlot(0, "left", shared, new Vector3(0f, -3f, 0f), 0),
                MakeSlot(1, "right", shared, new Vector3(0f, 3f, 0f), 1));

            Record(system.commandSource == null, "P-016pre",
                "no command source is attached, which is the normal configuration",
                report, ref passed, ref failed);

            system.ResetEngineState(0f);
            for (int i = 0; i < 900; i++)
                system.Evaluate(LevelState(), Atmosphere(), 0.9f, 0.02f);

            float left = PowerOf(system, 0);
            float right = PowerOf(system, 1);

            // Hand-calculated: throttle 0.9 -> 217.38*0.9 - 117.38 = 78.262 percent, both engines.
            Record(Near(left, 78.262f, 0.05f) && Near(right, 78.262f, 0.05f), "P-016",
                "both engines reach the pipeline throttle's 78.262 percent: left " + F(left)
                + ", right " + F(right),
                report, ref passed, ref failed);
            Record(Near(left, right, 1e-4f), "P-016b",
                "and are IDENTICAL to 1e-4, not merely close: difference "
                + F(Mathf.Abs(left - right)),
                report, ref passed, ref failed);
            Record(system.Command.Mode == MavPropulsionCommandMode.LinkedThrottle, "P-016c",
                "with the command in LinkedThrottle mode",
                report, ref passed, ref failed);
            Record(Near(system.debugPowerStateSpread01, 0f, 1e-5f), "P-016d",
                "and a zero power spread, so the aggregate scalar is not hiding anything",
                report, ref passed, ref failed);

            // A source that stops publishing must REVERT to linked, not leave a stale asymmetry.
            MavScriptedPropulsionCommandSource source = AttachCommandSource(system);
            source.SetEngineThrottle(0, 1f);
            source.SetEngineThrottle(1, 0f);
            system.Evaluate(LevelState(), Atmosphere(), 0.9f, 0.02f);
            Record(system.Command.Mode == MavPropulsionCommandMode.PerEngineThrottle, "P-016e",
                "attaching a source switches to per-engine mode",
                report, ref passed, ref failed);

            source.publish = false;
            system.Evaluate(LevelState(), Atmosphere(), 0.9f, 0.02f);
            Record(system.Command.Mode == MavPropulsionCommandMode.LinkedThrottle
                   && Near(system.Command.ThrottleForEngine(0), 0.9f, 1e-4f)
                   && Near(system.Command.ThrottleForEngine(1), 0.9f, 1e-4f), "P-016f",
                "and a source that stops publishing REVERTS to linked rather than leaving a stale "
                + "asymmetric command applied - a stuck differential throttle would be a control "
                + "failure, a reverted link is not",
                report, ref passed, ref failed);

            Object.DestroyImmediate(system.gameObject);
            Object.DestroyImmediate(shared);
        }

        // ==================================================================== P-017

        /// <summary>
        /// An authoritative engine deliberately shut down must NOT make the aggregate
        /// non-authoritative.
        ///
        /// This separates DATA AUTHORITY from OPERATIONAL STATE. A shut-down engine with a sourced
        /// model produces zero thrust, and that zero is a number we KNOW rather than one we lack.
        /// Folding the two together would make "the pilot shut it down" indistinguishable from "we
        /// have no engine data", and those call for opposite responses.
        /// </summary>
        private static void P017_AuthoritativeEngineOffKeepsProvenance(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P-017] authoritative engine intentionally off preserves provenance");

            MavFixedSyntheticDeck deck =
                MakeDeck(SynthThrustN, MavThrustDataAuthority.Authoritative, true);
            MavEngineProfile shared = MakeProfile(
                "p017-authoritative", deck, MavEnginePowerDynamicsLaw.InstantNoSourcedTransient);

            MavPropulsionSystem system = MakeSystem(
                MakeSlot(0, "left", shared, new Vector3(0f, -SynthLateralOffsetM, 0f), 0),
                MakeSlot(1, "right", shared, new Vector3(0f, SynthLateralOffsetM, 0f), 1));
            system.ResetEngineState(1f);

            MavPropulsiveLoads both = system.Evaluate(LevelState(), Atmosphere(), 1f, 0.02f);
            Record(both.hasAuthoritativeData, "P-017pre",
                "baseline: two authoritative engines running report authoritative data",
                report, ref passed, ref failed);

            // ---- 1. COMMANDED CUTOFF on the right engine ----
            MavScriptedPropulsionCommandSource source = AttachCommandSource(system);
            source.SetEngineThrottle(0, 1f);
            source.SetEngineThrottle(1, 1f);
            source.SetEngineCutoff(1, true);

            MavPropulsiveLoads cutoff = system.Evaluate(LevelState(), Atmosphere(), 1f, 0.02f);

            Record(cutoff.hasAuthoritativeData, "P-017",
                "a COMMANDED CUTOFF on one engine keeps the aggregate AUTHORITATIVE - being switched "
                + "off is not a data gap",
                report, ref passed, ref failed);
            Record(Near(cutoff.forceAeroBodyN.x, 50000f, 1f), "P-017b",
                "while the thrust really did halve to " + F(cutoff.forceAeroBodyN.x) + " N",
                report, ref passed, ref failed);
            Record(Mathf.Abs(cutoff.momentAeroBodyNm.z - 150000f) < 1f, "P-017c",
                "and the asymmetry produced a real yawing moment through r x F with no fake torque: "
                + "Mz=" + F(cutoff.momentAeroBodyNm.z) + " N*m",
                report, ref passed, ref failed);
            Record(system.debugCutoffEngineCount == 1
                   && system.debugContributingEngineCount == 1
                   && system.debugAuthoritativeProfileCount == 2, "P-017d",
                "with telemetry separating the two questions: cutoff=" + system.debugCutoffEngineCount
                + " contributing=" + system.debugContributingEngineCount
                + " authoritativeProfiles=" + system.debugAuthoritativeProfileCount,
                report, ref passed, ref failed);

            // ---- 2. BOTH engines shut down ----
            source.SetEngineCutoff(0, true);
            MavPropulsiveLoads allOff = system.Evaluate(LevelState(), Atmosphere(), 1f, 0.02f);

            Record(allOff.forceAeroBodyN == Vector3.zero, "P-017e",
                "both engines cut off gives exactly zero thrust",
                report, ref passed, ref failed);
            Record(allOff.hasAuthoritativeData, "P-017f",
                "and the aggregate is STILL authoritative: an authoritative twin with both engines "
                + "deliberately shut down has a thrust of zero that we know, not one we lack",
                report, ref passed, ref failed);
            Record(allOff.contributingEngineCount == 0, "P-017g",
                "reported honestly as zero contributing engines rather than hidden",
                report, ref passed, ref failed);

            // ---- 3. FAILED engine ----
            source.SetEngineCutoff(0, false);
            source.SetEngineCutoff(1, false);
            FailSlot(system, 1, true, report, ref passed, ref failed);
            MavPropulsiveLoads failedOne = system.Evaluate(LevelState(), Atmosphere(), 1f, 0.02f);

            Record(failedOne.hasAuthoritativeData, "P-017h",
                "a FAILED engine likewise keeps the aggregate authoritative",
                report, ref passed, ref failed);
            Record(system.debugFailedEngineCount == 1, "P-017i",
                "and is counted as an operational failure (" + system.debugFailedEngineCount + ")",
                report, ref passed, ref failed);
            FailSlot(system, 1, false, report, ref passed, ref failed);

            // ---- 4. DISABLED installation ----
            system.installation.engines[1].enabled = false;
            MavPropulsiveLoads disabled = system.Evaluate(LevelState(), Atmosphere(), 1f, 0.02f);
            Record(disabled.hasAuthoritativeData, "P-017j",
                "a DISABLED installation likewise: configuration is not provenance",
                report, ref passed, ref failed);
            system.installation.engines[1].enabled = true;

            // ---- 5. ZERO THROTTLE ----
            source.SetEngineThrottle(0, 0f);
            source.SetEngineThrottle(1, 0f);
            MavPropulsiveLoads idle = system.Evaluate(LevelState(), Atmosphere(), 0f, 0.02f);
            Record(idle.hasAuthoritativeData && Near(idle.reportedThrustN, 0f, 1f), "P-017k",
                "and ZERO THROTTLE gives zero thrust that is still authoritative: "
                + F(idle.reportedThrustN) + " N",
                report, ref passed, ref failed);

            // ---- and the check is still capable of going false, for a real DATA reason ----
            MavFixedSyntheticDeck synthetic =
                MakeDeck(SynthThrustN, MavThrustDataAuthority.SyntheticBench, true);
            system.installation.engines[1].engineProfile = MakeProfile(
                "p017-synthetic", synthetic, MavEnginePowerDynamicsLaw.InstantNoSourcedTransient);
            string err;
            system.Build(true, out err);
            system.ResetEngineState(1f);
            MavPropulsiveLoads mixed = system.Evaluate(LevelState(), Atmosphere(), 1f, 0.02f);

            Record(!mixed.hasAuthoritativeData, "P-017l",
                "while swapping one engine for a SYNTHETIC-data engine DOES make the aggregate "
                + "non-authoritative - so the checks above are not simply always-true",
                report, ref passed, ref failed);

            Object.DestroyImmediate(system.gameObject);
            Object.DestroyImmediate(deck.gameObject);
            Object.DestroyImmediate(synthetic.gameObject);
            Object.DestroyImmediate(shared);
        }

        // ==================================================================== P-018

        /// <summary>
        /// Registration fails CLOSED when the law is absent.
        ///
        /// Reaches the unregistered state through the factory's own public API - registering null -
        /// rather than a test-only back door, then restores it. P-014 covers the same rule via an
        /// unknown enum value; this covers the real F-16 law actually being missing, which is the case
        /// a startup-ordering bug would produce.
        /// </summary>
        private static void P018_RegistrationFailsClosed(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P-018] registration fails closed when the law is absent");

            MavF16EngineLawRegistrar.EnsureRegistered();
            Record(MavF16EngineLawRegistrar.IsRegistered, "P-018pre",
                "baseline: the F-16 law is registered",
                report, ref passed, ref failed);

            MavEngineProfile profile = MakeProfile(
                "p018-engine", null, MavEnginePowerDynamicsLaw.F16GarzaMorelliPowerState);

            // Clear the registration through the public API.
            MavEnginePowerDynamicsFactory.RegisterF16GarzaMorelli(null);
            try
            {
                Record(!MavF16EngineLawRegistrar.IsRegistered, "P-018",
                    "with the law cleared, IsRegistered reports false - it asks the factory rather "
                    + "than a cached flag, so it cannot claim success the factory would deny",
                    report, ref passed, ref failed);
                Record(MavEnginePowerDynamicsFactory.Resolve(
                           MavEnginePowerDynamicsLaw.F16GarzaMorelliPowerState) == null, "P-018b",
                    "Resolve returns null rather than substituting the instant law",
                    report, ref passed, ref failed);

                GameObject host = new GameObject("p018-system");
                MavPropulsionSystem system = host.AddComponent<MavPropulsionSystem>();
                system.installation = new MavPropulsionInstallationProfile();
                system.installation.installationId = "p018-installation";
                system.installation.engines = new MavEngineInstallation[]
                {
                    MakeSlot(0, "single", profile, Vector3.zero, 0)
                };

                string error;
                bool built = system.Build(true, out error);

                Record(!built, "P-018c",
                    "and the propulsion system REFUSES to build: " + error,
                    report, ref passed, ref failed);
                Record(!built && error.Contains("F16GarzaMorelliPowerState"), "P-018d",
                    "naming the missing law, so a startup-ordering bug is diagnosable from the message",
                    report, ref passed, ref failed);

                MavPropulsiveLoads loads = system.Evaluate(LevelState(), Atmosphere(), 1f, 0.02f);
                Record(loads.forceAeroBodyN == Vector3.zero && !loads.hasAuthoritativeData, "P-018e",
                    "producing zero loads rather than flying on a substituted engine model",
                    report, ref passed, ref failed);

                Object.DestroyImmediate(host);
            }
            finally
            {
                // Self-healing: EnsureRegistered checks the factory, so it re-registers here.
                MavF16EngineLawRegistrar.EnsureRegistered();
            }

            Record(MavF16EngineLawRegistrar.IsRegistered, "P-018f",
                "and EnsureRegistered is SELF-HEALING: it re-registers after the clear, because it "
                + "checks the factory instead of trusting a one-way local flag",
                report, ref passed, ref failed);

            Object.DestroyImmediate(profile);
        }

        // ==================================================================== P-019

        /// <summary>
        /// Duplicate registration is deterministic.
        ///
        /// Three lifecycle hooks call EnsureRegistered, and on some paths more than one of them will
        /// fire. Registering repeatedly must not accumulate registrations or change which instance
        /// resolves.
        /// </summary>
        private static void P019_DuplicateRegistrationDeterministic(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P-019] duplicate registration is deterministic");

            MavF16EngineLawRegistrar.EnsureRegistered();

            int countBefore = MavEnginePowerDynamicsFactory.F16RegistrationCount;
            IMavEnginePowerDynamics first = MavEnginePowerDynamicsFactory.Resolve(
                MavEnginePowerDynamicsLaw.F16GarzaMorelliPowerState);

            for (int i = 0; i < 5; i++)
                MavF16EngineLawRegistrar.EnsureRegistered();

            // Also call the runtime hook directly, as Unity would.
            MavF16EngineLawRegistrar.RegisterOnGameStart();

            // And re-register the identical instance through the factory.
            MavEnginePowerDynamicsFactory.RegisterF16GarzaMorelli(
                MavF16GarzaMorelliEngineDynamics.Instance);

            int countAfter = MavEnginePowerDynamicsFactory.F16RegistrationCount;
            IMavEnginePowerDynamics second = MavEnginePowerDynamicsFactory.Resolve(
                MavEnginePowerDynamicsLaw.F16GarzaMorelliPowerState);

            Record(countAfter == countBefore, "P-019",
                "seven further registration calls changed the registration count by "
                + (countAfter - countBefore) + " - registering the same instance is a no-op",
                report, ref passed, ref failed);
            Record(ReferenceEquals(first, second), "P-019b",
                "and the SAME instance still resolves, so nothing was replaced",
                report, ref passed, ref failed);
            Record(ReferenceEquals(second, MavF16GarzaMorelliEngineDynamics.Instance), "P-019c",
                "specifically the canonical Garza/Morelli singleton",
                report, ref passed, ref failed);

            // A genuinely different instance DOES count, or the counter would be meaningless.
            MavF16GarzaMorelliEngineDynamics other = new MavF16GarzaMorelliEngineDynamics();
            MavEnginePowerDynamicsFactory.RegisterF16GarzaMorelli(other);
            int countChanged = MavEnginePowerDynamicsFactory.F16RegistrationCount;
            MavEnginePowerDynamicsFactory.RegisterF16GarzaMorelli(
                MavF16GarzaMorelliEngineDynamics.Instance);

            Record(countChanged == countAfter + 1, "P-019d",
                "while registering a DIFFERENT instance does increment it, so the no-op above is not "
                + "simply a counter that never moves",
                report, ref passed, ref failed);
            Record(ReferenceEquals(
                       MavEnginePowerDynamicsFactory.Resolve(
                           MavEnginePowerDynamicsLaw.F16GarzaMorelliPowerState),
                       MavF16GarzaMorelliEngineDynamics.Instance), "P-019e",
                "and the canonical instance is restored afterwards",
                report, ref passed, ref failed);
        }

        // ==================================================================== P-020

        /// <summary>
        /// Profile / runtime state separation, now that the profile is a ScriptableObject.
        ///
        /// An asset is shared across every aircraft that references it, which makes the no-state rule
        /// MORE important than it was for a plain class: a state field here would be shared globally
        /// rather than per-aircraft.
        /// </summary>
        private static void P020_ProfileRuntimeSeparation(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P-020] engine profile holds no state; runtimes hold all of it");

            MavF16EngineLawRegistrar.EnsureRegistered();
            MavEngineProfile shared = MakeProfile(
                "p020-shared-engine", null, MavEnginePowerDynamicsLaw.F16GarzaMorelliPowerState);

            Record(shared is ScriptableObject, "P-020",
                "the engine profile is a ScriptableObject, so a serialized reference is a genuine "
                + "object reference and two slots cannot deserialize as drifting copies (ENG-013)",
                report, ref passed, ref failed);

            MavPropulsionSystem system = MakeSystem(
                MakeSlot(0, "left", shared, Vector3.zero, 0),
                MakeSlot(1, "right", shared, Vector3.zero, 1));

            Record(ReferenceEquals(system.installation.engines[0].engineProfile,
                                   system.installation.engines[1].engineProfile), "P-020b",
                "both slots reference ONE profile object",
                report, ref passed, ref failed);

            MavScriptedPropulsionCommandSource source = AttachCommandSource(system);
            source.SetEngineThrottle(0, 1f);
            source.SetEngineThrottle(1, 0f);
            system.ResetEngineState(0f);
            for (int i = 0; i < 200; i++)
                system.Evaluate(LevelState(), Atmosphere(), 0f, 0.02f);

            Record(Mathf.Abs(PowerOf(system, 0) - PowerOf(system, 1)) > 40f, "P-020c",
                "yet hold different power states (" + F(PowerOf(system, 0)) + " vs "
                + F(PowerOf(system, 1)) + "), which is only possible because no state lives on the "
                + "shared profile",
                report, ref passed, ref failed);

            // Source-level: the profile type must declare no mutable engine-state field. A behavioural
            // test cannot prove the absence of a field.
            string profilePath = global::System.IO.Path.Combine(
                Application.dataPath,
                "MaverickFresh/Scripts/FlightDynamics/Core/MavEngineProfile.cs");
            bool readable = global::System.IO.File.Exists(profilePath);
            Record(readable, "P-020pre",
                "MavEngineProfile.cs is readable for a source-level field check",
                report, ref passed, ref failed);

            if (readable)
            {
                string[] lines = global::System.IO.File.ReadAllLines(profilePath);
                string[] stateNames =
                {
                    "actualPower", "commandedPower", "powerState", "lastThrust", "lastResult",
                    "running", "failed", "spool"
                };
                int stateFields = 0;
                StringBuilder found = new StringBuilder();
                for (int i = 0; i < lines.Length; i++)
                {
                    string code = MavFlightDynamicsOwnershipScan.StripCommentsAndStringLiterals(
                        lines[i]);
                    if (code.IndexOf("public", global::System.StringComparison.Ordinal) < 0)
                        continue;

                    for (int k = 0; k < stateNames.Length; k++)
                    {
                        if (code.IndexOf(stateNames[k],
                                global::System.StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            stateFields++;
                            found.Append(' ').Append("L").Append(i + 1);
                        }
                    }
                }

                Record(stateFields == 0, "P-020d",
                    "and the profile type declares NO mutable engine-state member (" + stateFields
                    + ")" + found + " - checked at source level because no behavioural test can prove "
                    + "a field is absent",
                    report, ref passed, ref failed);
            }

            Object.DestroyImmediate(system.gameObject);
            Object.DestroyImmediate(shared);
        }

        // ==================================================================== P-021

        /// <summary>
        /// Aggregate scalar telemetry has defined, checkable meanings.
        ///
        /// reportedThrustN is a SUM. powerState01 is a MEAN, and powerStateSpread01 is non-zero
        /// exactly when that mean is hiding an asymmetry - which is what makes the mean safe to keep
        /// rather than ambiguous.
        /// </summary>
        private static void P021_AggregateScalarSemantics(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P-021] aggregate scalar telemetry semantics");

            MavFixedSyntheticDeck deck =
                MakeDeck(SynthThrustN, MavThrustDataAuthority.SyntheticBench, true);
            MavEngineProfile shared = MakeProfile(
                "p021-shared-engine", deck, MavEnginePowerDynamicsLaw.InstantNoSourcedTransient);

            MavPropulsionSystem system = MakeSystem(
                MakeSlot(0, "left", shared, new Vector3(0f, -3f, 0f), 0),
                MakeSlot(1, "right", shared, new Vector3(0f, 3f, 0f), 1));
            MavScriptedPropulsionCommandSource source = AttachCommandSource(system);

            // ---- symmetric: mean is exact, spread is zero ----
            source.SetEngineThrottle(0, 1f);
            source.SetEngineThrottle(1, 1f);
            system.ResetEngineState(1f);
            MavPropulsiveLoads sym = system.Evaluate(LevelState(), Atmosphere(), 1f, 0.02f);

            Record(Near(sym.reportedThrustN, 100000f, 1f), "P-021",
                "reportedThrustN is the SUM over engines: 2 x 50000 = 100000, got "
                + F(sym.reportedThrustN),
                report, ref passed, ref failed);
            Record(Near(sym.powerState01, 1f, 1e-4f), "P-021b",
                "powerState01 is the mean, exact when symmetric: " + F(sym.powerState01),
                report, ref passed, ref failed);
            Record(Near(sym.powerStateSpread01, 0f, 1e-5f), "P-021c",
                "and powerStateSpread01 is ZERO, so the mean is known to be trustworthy here",
                report, ref passed, ref failed);
            Record(sym.contributingEngineCount == 2, "P-021d",
                "with contributingEngineCount = 2", report, ref passed, ref failed);

            // ---- asymmetric: the mean hides it, the spread reveals it ----
            source.SetEngineThrottle(0, 1f);
            source.SetEngineThrottle(1, 0.4f);
            MavPropulsiveLoads asym = system.Evaluate(LevelState(), Atmosphere(), 1f, 0.02f);

            // Instant law: power = throttle*100, so 100% and 40%.
            // thrust = 50000 * power/100 -> 50000 + 20000 = 70000 N summed.
            Record(Near(asym.reportedThrustN, 70000f, 1f), "P-021e",
                "asymmetric sum is 50000 + 20000 = 70000 N, got " + F(asym.reportedThrustN),
                report, ref passed, ref failed);
            Record(Near(asym.powerState01, 0.7f, 1e-3f), "P-021f",
                "powerState01 is the MEAN of 100% and 40% = 70%, which matches NEITHER engine: "
                + F(asym.powerState01),
                report, ref passed, ref failed);
            Record(Near(asym.powerStateSpread01, 0.6f, 1e-3f), "P-021g",
                "and powerStateSpread01 = 0.6 says so explicitly, so a consumer can tell the mean is "
                + "hiding an asymmetry instead of having to guess",
                report, ref passed, ref failed);
            Record(Near(system.debugCommandedThrottleSpread01, 0.6f, 1e-3f), "P-021h",
                "with the COMMANDED spread also 0.6, distinguishing 'asymmetric because commanded' "
                + "from 'asymmetric and nobody asked'",
                report, ref passed, ref failed);

            // ---- single engine: the mean is exact and the spread must be zero ----
            MavPropulsionSystem single = MakeSystem(MakeSlot(0, "single", shared, Vector3.zero, 0));
            single.ResetEngineState(0.5f);
            MavPropulsiveLoads one = single.Evaluate(LevelState(), Atmosphere(), 0.5f, 0.02f);
            Record(Near(one.powerStateSpread01, 0f, 1e-5f) && one.contributingEngineCount == 1,
                "P-021i",
                "a single engine always reports zero spread, so the F-16's scalar telemetry means "
                + "exactly what it always meant",
                report, ref passed, ref failed);
            Record(Near(one.reportedThrustN, 25000f, 1f), "P-021j",
                "and its sum-of-one is just its own thrust: " + F(one.reportedThrustN) + " N",
                report, ref passed, ref failed);

            Object.DestroyImmediate(system.gameObject);
            Object.DestroyImmediate(single.gameObject);
            Object.DestroyImmediate(deck.gameObject);
            Object.DestroyImmediate(shared);
        }

        // ==================================================================== P-022

        /// <summary>
        /// The per-step load flag and the configuration gate answer DIFFERENT questions, and the case
        /// that discriminates them is a non-running unsourced engine.
        ///
        /// This case exists because a mutation probe would not bite without it. Removing the
        /// installed-profile term from the per-step flag changed no test result, which meant the two
        /// authority terms were redundant for every case the suite covered - they only differ when a
        /// non-authoritative engine is NOT running, and nothing tested that.
        ///
        /// Investigating it exposed a design confusion rather than just a missing test. The per-step
        /// flag had been asking two questions at once: "are these numbers sourced?" and "is this
        /// aircraft's propulsion configuration sourced?". Those have different answers here, and a
        /// live-flight gate reading the conflated version would have flipped the moment a pilot
        /// started a second, unsourced engine. So the per-step flag now answers only the first, and
        /// the component's own HasAuthoritativeData / IsAcceptableForLiveFlight answer the second,
        /// operation-independently.
        /// </summary>
        private static void P022_StepAuthorityVersusConfiguration(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P-022] per-step load authority vs configuration-level authority");

            MavFixedSyntheticDeck authoritative =
                MakeDeck(SynthThrustN, MavThrustDataAuthority.Authoritative, true);
            MavFixedSyntheticDeck synthetic =
                MakeDeck(SynthThrustN, MavThrustDataAuthority.SyntheticBench, true);

            MavEngineProfile sourcedEngine = MakeProfile(
                "p022-sourced", authoritative, MavEnginePowerDynamicsLaw.InstantNoSourcedTransient);
            MavEngineProfile unsourcedEngine = MakeProfile(
                "p022-unsourced", synthetic, MavEnginePowerDynamicsLaw.InstantNoSourcedTransient);

            MavPropulsionSystem system = MakeSystem(
                MakeSlot(0, "left", sourcedEngine, new Vector3(0f, -SynthLateralOffsetM, 0f), 0),
                MakeSlot(1, "right", unsourcedEngine, new Vector3(0f, SynthLateralOffsetM, 0f), 1));

            MavScriptedPropulsionCommandSource source = AttachCommandSource(system);
            source.SetEngineThrottle(0, 1f);
            source.SetEngineThrottle(1, 1f);
            system.ResetEngineState(1f);

            // ---- both running: the unsourced number IS in the sum ----
            MavPropulsiveLoads both = system.Evaluate(LevelState(), Atmosphere(), 1f, 0.02f);
            Record(!both.hasAuthoritativeData, "P-022",
                "with both engines running, the summed force contains the unsourced engine's number, "
                + "so the per-step flag is FALSE",
                report, ref passed, ref failed);

            // ---- the discriminating case: the unsourced engine is cut off ----
            source.SetEngineCutoff(1, true);
            MavPropulsiveLoads sourcedOnly = system.Evaluate(LevelState(), Atmosphere(), 1f, 0.02f);

            Record(sourcedOnly.hasAuthoritativeData, "P-022b",
                "cut the UNSOURCED engine off and the per-step flag becomes TRUE - every number now "
                + "summed does come from authoritative data",
                report, ref passed, ref failed);
            Record(sourcedOnly.contributingEngineCount == 1
                   && Near(sourcedOnly.reportedThrustN, 50000f, 1f), "P-022c",
                "with one contributing engine and " + F(sourcedOnly.reportedThrustN)
                + " N of sourced thrust",
                report, ref passed, ref failed);

            // ---- and the CONFIGURATION question is unmoved by any of that ----
            Record(!system.HasAuthoritativeData, "P-022d",
                "yet the component's HasAuthoritativeData stays FALSE, because an unsourced engine is "
                + "still bolted to this aircraft - switching it off does not source its data",
                report, ref passed, ref failed);

            string reason;
            Record(!system.installation.IsAcceptableForLiveFlight(out reason), "P-022e",
                "and it is not acceptable for live flight: " + reason,
                report, ref passed, ref failed);
            Record(system.debugAuthoritativeProfileCount == 1
                   && system.debugEngineCount == 2, "P-022f",
                "with telemetry reporting " + system.debugAuthoritativeProfileCount + " of "
                + system.debugEngineCount + " installed profiles authoritative, so both questions are "
                + "visible at once and neither has to impersonate the other",
                report, ref passed, ref failed);

            // ---- the gate must not flip on an operational change ----
            source.SetEngineCutoff(1, false);
            system.Evaluate(LevelState(), Atmosphere(), 1f, 0.02f);
            Record(!system.HasAuthoritativeData, "P-022g",
                "restarting the unsourced engine leaves the configuration answer unchanged - a gate "
                + "that flipped on an engine start would be reading the wrong flag",
                report, ref passed, ref failed);

            Object.DestroyImmediate(system.gameObject);
            Object.DestroyImmediate(authoritative.gameObject);
            Object.DestroyImmediate(synthetic.gameObject);
            Object.DestroyImmediate(sourcedEngine);
            Object.DestroyImmediate(unsourcedEngine);
        }

        // ==================================================================== P-023

        /// <summary>
        /// Every live-readiness condition is INDIVIDUALLY load bearing.
        ///
        /// This case exists because a mutation probe that deleted the undeclared-geometry check left
        /// the suite green. Not because the check is wrong - because no test ever reached it. Every
        /// existing case fails earlier, on missing thrust data or an undeclared envelope, so the
        /// geometry check was never the deciding factor and could have been removed unnoticed.
        ///
        /// That matters concretely and soon: the moment someone attaches the TP-1538 thrust deck to
        /// the F-16, missing geometry and an undeclared envelope become the ONLY things standing
        /// between it and "acceptable for live flight". Those checks have to be known to work before
        /// that happens, not after.
        ///
        /// So this builds a fully acceptable installation and then knocks out one condition at a
        /// time, the same way MavReplacementReadiness.FullyReady is used. All geometry here is
        /// SYNTHETIC.
        /// </summary>
        private static void P023_LiveReadinessLadder(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P-023] each live-readiness condition is individually load bearing");

            MavFixedSyntheticDeck deck =
                MakeDeck(SynthThrustN, MavThrustDataAuthority.Authoritative, true);
            deck.policy = MavEnvelopeExcursionPolicy.ClampToValidatedEnvelope;

            // A profile that satisfies everything. NOT aircraft data - the envelope numbers are
            // invented to exercise the readiness rule.
            MavEngineProfile good = MavEngineProfile.CreateInMemory("p023-acceptable");
            good.displayName = "p023-acceptable";
            good.engineVariantIdentity = "SYNTHETIC TEST ENGINE - not aircraft data";
            good.sourceIdentity = "synthetic";
            good.provenance = MavEngineDataProvenance.Authoritative;
            good.powerDynamicsLaw = MavEnginePowerDynamicsLaw.InstantNoSourcedTransient;
            good.powerDynamicsProvenance = MavEngineDataProvenance.Unavailable;
            good.thrustDeck = deck;
            good.sourceEnvelope.declared = true;
            good.sourceEnvelope.minAltitudeM = 0f;
            good.sourceEnvelope.maxAltitudeM = 12000f;
            good.sourceEnvelope.minMach = 0f;
            good.sourceEnvelope.maxMach = 0.6f;

            MavPropulsionInstallationProfile install = new MavPropulsionInstallationProfile();
            install.installationId = "p023-installation";
            install.aircraftConfiguration = "SYNTHETIC";
            install.engines = new MavEngineInstallation[]
            {
                MakeSlot(0, "single", good, new Vector3(0f, 0f, -0.5f), 0)
            };

            string reason;
            Record(install.IsAcceptableForLiveFlight(out reason), "P-023",
                "baseline: a fully specified installation IS acceptable for live flight - without "
                + "this the knock-outs below would pass for the wrong reason (" + reason + ")",
                report, ref passed, ref failed);

            // ---- knock out the DECLARED ENVELOPE ----
            good.sourceEnvelope.declared = false;
            Record(!install.IsAcceptableForLiveFlight(out reason), "P-023b",
                "an UNDECLARED source envelope alone blocks it: " + reason,
                report, ref passed, ref failed);
            good.sourceEnvelope.declared = true;

            // ---- knock out GEOMETRY DECLARATION - the condition the probe reached ----
            install.engines[0].geometryDeclared = false;
            Record(!install.IsAcceptableForLiveFlight(out reason), "P-023c",
                "UNDECLARED installation geometry alone blocks it, even with perfect thrust data - "
                + "an unmeasured thrust line means the r x F moment is unsourced: " + reason,
                report, ref passed, ref failed);
            install.engines[0].geometryDeclared = true;

            // ---- knock out GEOMETRY PROVENANCE ----
            install.engines[0].geometryProvenance = "UNAVAILABLE";
            Record(!install.IsAcceptableForLiveFlight(out reason), "P-023d",
                "geometry declared but with no PROVENANCE alone blocks it: " + reason,
                report, ref passed, ref failed);
            install.engines[0].geometryProvenance = "SYNTHETIC validation geometry - not aircraft data";

            // ---- knock out the DECK AUTHORITY ----
            deck.declaredAuthority = MavThrustDataAuthority.SyntheticBench;
            Record(!install.IsAcceptableForLiveFlight(out reason), "P-023e",
                "a SyntheticBench deck alone blocks it: " + reason,
                report, ref passed, ref failed);
            deck.declaredAuthority = MavThrustDataAuthority.Authoritative;

            // ---- knock out the EXCURSION POLICY ----
            deck.policy = MavEnvelopeExcursionPolicy.MarkNonAuthoritativeExtrapolation;
            Record(!install.IsAcceptableForLiveFlight(out reason), "P-023f",
                "an EXTRAPOLATING deck alone blocks it, even with authoritative tables - a number "
                + "outside the validated envelope is not evidence about the engine: " + reason,
                report, ref passed, ref failed);
            deck.policy = MavEnvelopeExcursionPolicy.ClampToValidatedEnvelope;

            // ---- knock out ENGINE IDENTITY ----
            install.engines[0].engineProfile.engineVariantIdentity = "unspecified";
            Record(!install.IsAcceptableForLiveFlight(out reason), "P-023g",
                "an UNSPECIFIED engine variant alone blocks it: " + reason,
                report, ref passed, ref failed);
            install.engines[0].engineProfile.engineVariantIdentity =
                "SYNTHETIC TEST ENGINE - not aircraft data";

            // ---- knock out a FINITE thrust direction ----
            install.engines[0].thrustDirectionAeroBody = new Vector3(1e-6f, 0f, 0f);
            Record(!install.IsAcceptableForLiveFlight(out reason), "P-023h",
                "a thrust direction too short to normalise meaningfully alone blocks it: " + reason,
                report, ref passed, ref failed);
            install.engines[0].thrustDirectionAeroBody = new Vector3(1f, 0f, 0f);

            // ---- knock out a FINITE mount position ----
            install.engines[0].positionAeroBodyM = new Vector3(0f, float.NaN, 0f);
            Record(!install.IsAcceptableForLiveFlight(out reason), "P-023i",
                "a non-finite mount position alone blocks it, before NaN can reach r x F: " + reason,
                report, ref passed, ref failed);
            install.engines[0].positionAeroBodyM = new Vector3(0f, 0f, -0.5f);

            // ---- restored: acceptable again, so every knock-out above was the only cause ----
            Record(install.IsAcceptableForLiveFlight(out reason), "P-023j",
                "and with everything restored it is acceptable again, so each failure above was "
                + "caused by the condition removed and nothing else",
                report, ref passed, ref failed);

            // The F-16's real installation must still be blocked, for the two reasons it declares.
            MavF16EngineLawRegistrar.EnsureRegistered();
            MavPropulsionInstallationProfile f16 =
                MavF16PropulsionInstallation.CreateInstallation(null);
            Record(!f16.IsAcceptableForLiveFlight(out reason), "P-023k",
                "and the real F-16 installation remains blocked: " + reason,
                report, ref passed, ref failed);

            Object.DestroyImmediate(deck.gameObject);
            Object.DestroyImmediate(good);
            if (f16.engines.Length > 0 && f16.engines[0].engineProfile != null)
                Object.DestroyImmediate(f16.engines[0].engineProfile);
        }

        // ==================================================================== P-024

        /// <summary>
        /// The configuration mistakes an inspector-authored installation will actually make.
        ///
        /// These were untested, and one of them became MORE likely in P0.1 rather than less: now that
        /// the engine profile is a ScriptableObject asset reference, "added a slot and forgot to
        /// assign the profile" is the ordinary authoring error. A null reference there must produce a
        /// refusal that names the slot, not a NullReferenceException during a physics step.
        ///
        /// None of this can be fully proven without Unity - only the editor can actually serialize a
        /// missing asset reference - but the behaviour on encountering null is production code and is
        /// checkable here.
        /// </summary>
        private static void P024_AuthoringHazards(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P-024] inspector authoring hazards fail closed and name the slot");

            MavF16EngineLawRegistrar.EnsureRegistered();
            MavEngineProfile good = MakeProfile(
                "p024-engine", null, MavEnginePowerDynamicsLaw.InstantNoSourcedTransient);

            // ---- 1. a slot with NO engine profile assigned ----
            MavEngineInstallation nullProfile = MakeSlot(3, "unassigned", good, Vector3.zero, 0);
            nullProfile.engineProfile = null;

            string reason;
            Record(!nullProfile.IsValid(out reason), "P-024",
                "a slot with an unassigned engine profile is INVALID: " + reason,
                report, ref passed, ref failed);
            Record(reason.Contains("3"), "P-024b",
                "and the reason names the slot id, so an authoring mistake is findable in a scene "
                + "with several engines",
                report, ref passed, ref failed);

            MavPropulsionInstallationProfile install = new MavPropulsionInstallationProfile();
            install.installationId = "p024-installation";
            install.engines = new MavEngineInstallation[] { nullProfile };

            GameObject host = new GameObject("p024-system");
            MavPropulsionSystem system = host.AddComponent<MavPropulsionSystem>();
            system.installation = install;

            string error;
            Record(!system.Build(true, out error), "P-024c",
                "the propulsion system REFUSES to build: " + error,
                report, ref passed, ref failed);

            // The important part: a refused build must still survive being stepped.
            MavPropulsiveLoads loads = system.Evaluate(LevelState(), Atmosphere(), 1f, 0.02f);
            Record(loads.forceAeroBodyN == Vector3.zero
                   && loads.momentAeroBodyNm == Vector3.zero
                   && !loads.hasAuthoritativeData, "P-024d",
                "and stepping it produces zero loads rather than a NullReferenceException mid-physics",
                report, ref passed, ref failed);

            // A missing profile must make the aggregate NON-authoritative, not vacuously
            // authoritative because nothing contributed. Added after the Unity run found exactly
            // that: a destroyed ScriptableObject left no contributing engine, so the
            // every-contributing-result term was trivially true and the loads claimed sourced data
            // for an aircraft whose engine definition was gone. Offline the profile can only be a
            // plain null, which reaches the same production branch.
            Record(!loads.hasAuthoritativeData, "P-024d2",
                "specifically NOT authoritative - a missing engine definition is lost DATA, not an "
                + "engine that happens to be switched off, and the two must not report alike",
                report, ref passed, ref failed);
            Record(system.RuntimeCount == 0, "P-024e",
                "with no runtimes created (" + system.RuntimeCount + ")",
                report, ref passed, ref failed);

            // ---- 1b. a profile LOST AFTER a successful build ----
            //
            // P-024d2 above is a real check but it does not exercise the fix it was written for.
            // Its build is REFUSED, so debugEngineCount is 0 and hasAuthoritativeData is false
            // because of the engine-count term - the everySlotHasProfile term is never consulted.
            // Deleting that term leaves P-024d2 passing, which a mutation probe demonstrated.
            //
            // The term only matters when the build SUCCEEDED and the profile disappeared
            // afterwards, which is precisely the Unity destroyed-ScriptableObject case. Offline the
            // reference can only be set to a plain null, but that reaches the same production
            // branch: runtimes exist, so the engine count is non-zero, and no engine contributes,
            // so the per-contribution unanimity is vacuously true. Without the guard the aggregate
            // would claim authoritative data for an aircraft whose engine definition is gone.
            MavEngineProfile sourced = MakeProfile(
                "p024-sourced", MakeDeck(60000f, MavThrustDataAuthority.Authoritative, true),
                MavEnginePowerDynamicsLaw.InstantNoSourcedTransient);

            MavEngineInstallation liveSlot = MakeSlot(0, "live", sourced, Vector3.zero, 0);
            MavPropulsionInstallationProfile lost = new MavPropulsionInstallationProfile();
            lost.installationId = "p024-lost-profile";
            lost.engines = new MavEngineInstallation[] { liveSlot };

            GameObject lostHost = new GameObject("p024-lost");
            MavPropulsionSystem lostSystem = lostHost.AddComponent<MavPropulsionSystem>();
            lostSystem.installation = lost;
            Record(lostSystem.Build(true, out error), "P-024d3",
                "a slot with a sourced profile builds first: " + error,
                report, ref passed, ref failed);

            lostSystem.ResetEngineState(1f);
            MavPropulsiveLoads before = lostSystem.Evaluate(LevelState(), Atmosphere(), 1f, 0.02f);
            Record(before.hasAuthoritativeData, "P-024d4",
                "and reports authoritative data while its profile is intact, which is the state the "
                + "next step has to take away",
                report, ref passed, ref failed);

            liveSlot.engineProfile = null;
            MavPropulsiveLoads after = lostSystem.Evaluate(LevelState(), Atmosphere(), 1f, 0.02f);
            Record(!after.hasAuthoritativeData, "P-024d5",
                "with the profile lost AFTER a successful build, the aggregate is no longer "
                + "authoritative - engines are still installed, so the count term does not save it "
                + "and the missing-profile term is what must",
                report, ref passed, ref failed);
            Record(after.forceAeroBodyN == Vector3.zero
                   && after.momentAeroBodyNm == Vector3.zero, "P-024d6",
                "and it contributes no load rather than a last-known thrust",
                report, ref passed, ref failed);
            Record(lostSystem.debugEngineCount > 0, "P-024d7",
                "with debugEngineCount still " + lostSystem.debugEngineCount
                + " - confirming this case reaches the missing-profile term instead of failing "
                + "earlier on the engine count, which is how P-024d2 passed without exercising it",
                report, ref passed, ref failed);

            Object.DestroyImmediate(lostHost);

            // ---- 2. a NULL SLOT in the array - an inspector list with an empty element ----
            install.engines = new MavEngineInstallation[] { null };
            Record(!install.IsValid(out reason), "P-024f",
                "a null slot in the engine array is refused: " + reason,
                report, ref passed, ref failed);
            Record(!system.Build(true, out error), "P-024g",
                "and the system refuses to build on it: " + error,
                report, ref passed, ref failed);

            // ---- 3. DUPLICATE slot ids - copy/paste in the inspector ----
            install.engines = new MavEngineInstallation[]
            {
                MakeSlot(0, "left", good, new Vector3(0f, -3f, 0f), 0),
                MakeSlot(0, "right", good, new Vector3(0f, 3f, 0f), 1)
            };
            Record(!install.IsValid(out reason), "P-024h",
                "duplicate slot ids are refused: " + reason,
                report, ref passed, ref failed);
            Record(reason.Contains("duplicate"), "P-024i",
                "naming the collision, because a duplicate id makes per-engine telemetry ambiguous "
                + "exactly when someone is trying to work out which engine misbehaved",
                report, ref passed, ref failed);

            // ---- 4. a null command source field must not be dereferenced ----
            install.engines = new MavEngineInstallation[]
            {
                MakeSlot(0, "single", good, Vector3.zero, 0)
            };
            system.commandSource = null;
            system.autoResolveCommandSource = false;
            Record(system.Build(true, out error), "P-024j",
                "a valid installation with NO command source builds fine: " + error,
                report, ref passed, ref failed);

            system.ResetEngineState(0.5f);
            MavPropulsiveLoads linked = system.Evaluate(LevelState(), Atmosphere(), 0.5f, 0.02f);
            Record(system.Command.Mode == MavPropulsionCommandMode.LinkedThrottle
                   && Near(system.Command.ThrottleForEngine(0), 0.5f, 1e-4f), "P-024k",
                "and runs linked at the pipeline throttle without dereferencing the empty source "
                + "field - which is the F-16's configuration",
                report, ref passed, ref failed);
            Record(!float.IsNaN(linked.powerState01), "P-024l",
                "producing finite telemetry", report, ref passed, ref failed);

            // ---- 5. a command source that addresses engines that do not exist ----
            MavScriptedPropulsionCommandSource source = AttachCommandSource(system);
            source.Resize(4);                       // four channels for a one-engine aircraft
            source.SetEngineThrottle(0, 0.8f);      // the one real engine
            source.SetEngineThrottle(3, 1f);        // three engines that do not exist
            system.Evaluate(LevelState(), Atmosphere(), 0.2f, 0.02f);

            // Exact, not "either of two plausible values": slot 0 must carry precisely what was
            // published for it, and the writes to non-existent engines must be dropped rather than
            // wrapping onto a real slot.
            Record(Near(system.Command.ThrottleForEngine(0), 0.8f, 1e-4f), "P-024m",
                "a source addressing four engines on a ONE-engine aircraft leaves slot 0 at exactly "
                + "its own published 0.8 and drops the rest: slot 0 throttle "
                + F(system.Command.ThrottleForEngine(0)),
                report, ref passed, ref failed);
            Record(Near(system.Command.ThrottleForEngine(9), 0f, 1e-6f), "P-024n",
                "and an out-of-range read returns idle rather than throwing",
                report, ref passed, ref failed);

            Object.DestroyImmediate(host);
            Object.DestroyImmediate(good);
        }

        // ==================================================================== P-025

        /// <summary>
        /// Command authority is EXCLUSIVE: one owner per step, never a blend.
        ///
        /// P0.1 had a real ambiguity here. BeginStep seeded every engine's per-engine slot with the
        /// scalar throttle, so a source that addressed only one engine of a twin left the other
        /// silently running on the scalar - and the resulting numbers were indistinguishable from a
        /// fully commanded asymmetry. A reader could not tell which engines the source had actually
        /// commanded.
        ///
        /// The rule is now all-or-nothing: a source owns per-engine commands only if it addressed
        /// EVERY installed engine. Partial coverage is refused in full and the aircraft reverts to
        /// linked, which is the safe direction - linked flight is a defined state, a half-applied
        /// differential command is not.
        /// </summary>
        private static void P025_CommandAuthorityIsExclusive(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P-025] command authority is exclusive: LINKED_SCALAR or PER_ENGINE_SOURCE");

            MavF16EngineLawRegistrar.EnsureRegistered();
            MavEngineProfile shared = MakeProfile(
                "p025-shared-engine", null, MavEnginePowerDynamicsLaw.F16GarzaMorelliPowerState);

            MavPropulsionSystem system = MakeSystem(
                MakeSlot(0, "left", shared, new Vector3(0f, -3f, 0f), 0),
                MakeSlot(1, "right", shared, new Vector3(0f, 3f, 0f), 1));

            // ---- 1. no source at all -> LINKED_SCALAR ----
            system.ResetEngineState(0f);
            system.Evaluate(LevelState(), Atmosphere(), 0.9f, 0.02f);

            Record(system.debugCommandAuthority == "LINKED_SCALAR", "P-025",
                "with no source the authority is LINKED_SCALAR: " + system.debugCommandAuthority,
                report, ref passed, ref failed);
            Record(system.Command.Authority == MavPropulsionCommandAuthority.LinkedScalar, "P-025b",
                "and the command agrees", report, ref passed, ref failed);
            Record(system.debugAddressedEngineCount == 0, "P-025c",
                "with zero engines addressed (" + system.debugAddressedEngineCount + ")",
                report, ref passed, ref failed);

            // ---- 2. a source covering EVERY engine -> PER_ENGINE_SOURCE ----
            MavScriptedPropulsionCommandSource source = AttachCommandSource(system);
            source.SetEngineThrottle(0, 1f);
            source.SetEngineThrottle(1, 0.25f);
            system.Evaluate(LevelState(), Atmosphere(), 0.9f, 0.02f);

            Record(system.debugCommandAuthority == "PER_ENGINE_SOURCE", "P-025d",
                "a source addressing every engine takes PER_ENGINE_SOURCE authority: "
                + system.debugCommandAuthority,
                report, ref passed, ref failed);
            Record(system.debugAddressedEngineCount == 2 && !system.debugPartialCommandRefused,
                "P-025e",
                "addressing " + system.debugAddressedEngineCount + " of 2 engines, nothing refused",
                report, ref passed, ref failed);
            Record(Near(system.Command.ThrottleForEngine(0), 1f, 1e-4f)
                   && Near(system.Command.ThrottleForEngine(1), 0.25f, 1e-4f), "P-025f",
                "and both engines carry the source's values, not the 0.9 scalar",
                report, ref passed, ref failed);

            // ---- 3. a source covering only SOME engines -> refused, revert to linked ----
            source.SetPartialCoverage(1);       // address engine 0 only
            source.SetEngineThrottle(0, 1f);
            system.Evaluate(LevelState(), Atmosphere(), 0.9f, 0.02f);

            Record(system.debugAddressedEngineCount == 1, "P-025g",
                "a source addressing only one engine of a twin addresses "
                + system.debugAddressedEngineCount + " of 2",
                report, ref passed, ref failed);
            Record(system.debugCommandAuthority == "LINKED_SCALAR", "P-025h",
                "which is REFUSED in full and reverts to LINKED_SCALAR rather than half-applying: "
                + system.debugCommandAuthority,
                report, ref passed, ref failed);
            Record(system.debugPartialCommandRefused, "P-025i",
                "with the refusal surfaced in telemetry, so a control-law defect is visible instead "
                + "of producing numbers that look like a deliberate asymmetry",
                report, ref passed, ref failed);
            Record(Near(system.Command.ThrottleForEngine(0), 0.9f, 1e-4f)
                   && Near(system.Command.ThrottleForEngine(1), 0.9f, 1e-4f), "P-025j",
                "and BOTH engines are on the scalar 0.9 - the partially commanded engine did not keep "
                + "its 1.0",
                report, ref passed, ref failed);

            // ---- 4. cutoff is ignored under linked authority ----
            source.SetPartialCoverage(1);
            source.SetEngineCutoff(0, true);
            system.Evaluate(LevelState(), Atmosphere(), 0.9f, 0.02f);
            Record(!system.Command.IsCutoff(0), "P-025k",
                "a cutoff published inside a refused partial command is NOT honoured - cutoff is a "
                + "per-engine command and cannot act under linked authority",
                report, ref passed, ref failed);

            // ---- 5. full coverage again -> authority returns ----
            source.SetPartialCoverage(0);       // full coverage
            source.SetEngineCutoff(0, false);
            source.SetEngineThrottle(0, 0.8f);
            source.SetEngineThrottle(1, 0.3f);
            system.Evaluate(LevelState(), Atmosphere(), 0.9f, 0.02f);

            Record(system.debugCommandAuthority == "PER_ENGINE_SOURCE"
                   && !system.debugPartialCommandRefused, "P-025l",
                "restoring full coverage returns PER_ENGINE_SOURCE authority, so the refusal above "
                + "was caused by the partial coverage and nothing else",
                report, ref passed, ref failed);

            // ---- 6. a source that declines -> linked ----
            source.publish = false;
            system.Evaluate(LevelState(), Atmosphere(), 0.9f, 0.02f);
            Record(system.debugCommandAuthority == "LINKED_SCALAR"
                   && !system.debugPartialCommandRefused, "P-025m",
                "a source that declines to publish yields LINKED_SCALAR without being reported as a "
                + "partial refusal - declining is legitimate, half-publishing is not",
                report, ref passed, ref failed);

            Object.DestroyImmediate(system.gameObject);
            Object.DestroyImmediate(shared);
        }

        // ==================================================================== null-safe accessors
        //
        // A validation suite that throws is worse than one that fails. When a deliberate-break probe
        // shared one runtime across two slots, GetRuntimeBySlotId(1) returned null and the whole run
        // aborted with a NullReferenceException at the first dereference - so the probe's real damage
        // was hidden behind a crash and every later case went unreported. These accessors turn a
        // missing runtime into a recorded FAIL and let the rest of the suite finish.

        /// <summary>Power state of a slot, or NaN when that runtime is missing.</summary>
        private static float PowerOf(MavPropulsionSystem system, int index)
        {
            MavEngineRuntime runtime = system != null ? system.GetRuntime(index) : null;
            return runtime != null ? runtime.ActualPowerPercent : float.NaN;
        }

        /// <summary>Last result for a slot id, or a zeroed result when that runtime is missing.</summary>
        private static MavEngineLoadResult ResultOfSlot(MavPropulsionSystem system, int slotId)
        {
            MavEngineRuntime runtime =
                system != null ? system.GetRuntimeBySlotId(slotId) : null;
            return runtime != null ? runtime.LastResult : MavEngineLoadResult.Zero;
        }

        /// <summary>Last result for a slot index, or a zeroed result when that runtime is missing.</summary>
        private static MavEngineLoadResult ResultOfIndex(MavPropulsionSystem system, int index)
        {
            MavEngineRuntime runtime = system != null ? system.GetRuntime(index) : null;
            return runtime != null ? runtime.LastResult : MavEngineLoadResult.Zero;
        }

        /// <summary>
        /// Records that every listed slot id resolves to a distinct runtime, and returns whether it
        /// does. Called before the cases that depend on it, so a shared or missing runtime is reported
        /// as its own failure rather than surfacing as a confusing downstream number.
        /// </summary>
        private static bool RequireDistinctRuntimes(
            MavPropulsionSystem system, int slotA, int slotB, string id,
            StringBuilder report, ref int passed, ref int failed)
        {
            MavEngineRuntime a = system.GetRuntimeBySlotId(slotA);
            MavEngineRuntime b = system.GetRuntimeBySlotId(slotB);
            bool ok = a != null && b != null && !ReferenceEquals(a, b);

            Record(ok, id,
                "slots " + slotA + " and " + slotB + " resolve to two DISTINCT runtime objects"
                + (a == null ? " [slot " + slotA + " missing]" : "")
                + (b == null ? " [slot " + slotB + " missing]" : "")
                + (a != null && b != null && ReferenceEquals(a, b) ? " [SHARED - the one thing this "
                    + "architecture must never do]" : ""),
                report, ref passed, ref failed);

            return ok;
        }

        /// <summary>Fails the engine in a slot, reporting instead of throwing when it is missing.</summary>
        private static void FailSlot(
            MavPropulsionSystem system, int slotId, bool failedState,
            StringBuilder report, ref int passed, ref int failed)
        {
            MavEngineRuntime runtime = system.GetRuntimeBySlotId(slotId);
            if (runtime == null)
            {
                Record(false, "SLOT-" + slotId,
                    "slot " + slotId + " has no runtime, so it cannot be failed for this case",
                    report, ref passed, ref failed);
                return;
            }

            runtime.SetFailed(failedState);
        }

        // ==================================================================== helpers

        private static bool Near(float a, float b)
        {
            return Mathf.Abs(a - b) <= Tol;
        }

        private static bool Near(float a, float b, float tol)
        {
            return Mathf.Abs(a - b) <= tol;
        }

        private static bool IsFinite(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsInfinity(v.x)
                && !float.IsNaN(v.y) && !float.IsInfinity(v.y)
                && !float.IsNaN(v.z) && !float.IsInfinity(v.z);
        }

        private static string F(float v)
        {
            return v.ToString("0.####");
        }

        private static string V(Vector3 v)
        {
            return "(" + F(v.x) + ", " + F(v.y) + ", " + F(v.z) + ")";
        }

        private static void Record(
            bool condition, string id, string description,
            StringBuilder report, ref int passed, ref int failed)
        {
            if (condition)
            {
                passed++;
                report.Append("  PASS  ").Append(id).Append("  ").AppendLine(description);
            }
            else
            {
                failed++;
                report.Append("  FAIL  ").Append(id).Append("  ").AppendLine(description);
            }
        }
    }

    /// <summary>
    /// A command source that publishes whatever a test tells it to.
    ///
    /// This stands in for a control layer. It is a real
    /// <see cref="MavPropulsionCommandSourceBase"/> component, which is the point: P-015 has to prove
    /// per-engine commands arrive through the PRODUCTION seam, not through a test reaching into
    /// runtime internals.
    ///
    /// Lives in the Validation namespace so it cannot be wired into an aircraft by accident.
    /// </summary>
    public sealed class MavScriptedPropulsionCommandSource : MavPropulsionCommandSourceBase
    {
        private float[] throttles = new float[0];
        private bool[] cutoffs = new bool[0];

        [Tooltip("When false, publishes nothing - so the propulsion system's linked default applies.")]
        public bool publish = true;

        public override string CommandSourceName
        {
            get { return "scripted validation command source (NOT a flight control law)"; }
        }

        public void Resize(int engineCount)
        {
            int count = engineCount > 0 ? engineCount : 0;
            throttles = new float[count];
            cutoffs = new bool[count];
        }

        public void SetEngineThrottle(int index, float throttle01)
        {
            if (index < 0 || index >= throttles.Length)
                return;

            throttles[index] = throttle01;
        }

        public void SetEngineCutoff(int index, bool cutoff)
        {
            if (index < 0 || index >= cutoffs.Length)
                return;

            cutoffs[index] = cutoff;
        }

        /// <summary>
        /// Publishes only the first N engines, to exercise the partial-coverage refusal. 0 means
        /// publish all of them, which is what a correct control law does.
        /// </summary>
        public void SetPartialCoverage(int engineCount)
        {
            coverage = engineCount;
        }

        private int coverage;

        public override bool TryPublishPropulsionCommand(
            MavPropulsionCommand command, float pipelineThrottle01)
        {
            if (!publish)
                return false;

            int limit = coverage > 0 ? Mathf.Min(coverage, throttles.Length) : throttles.Length;
            for (int i = 0; i < limit; i++)
            {
                command.SetEngineThrottle(i, throttles[i]);
                command.SetEngineCutoff(i, cutoffs[i]);
            }

            return true;
        }
    }

    /// <summary>
    /// A thrust deck returning a constant, for validation only.
    ///
    /// Declares whatever authority the test sets so the provenance rules can be exercised in both
    /// directions. Lives in the Validation namespace precisely so it cannot be wired into an aircraft
    /// by accident.
    /// </summary>
    public sealed class MavFixedSyntheticDeck : MavThrustDeckBase
    {
        public float fixedThrustN = 0f;
        public MavThrustDataAuthority declaredAuthority = MavThrustDataAuthority.SyntheticBench;
        public bool reportInsideEnvelope = true;
        public MavEnvelopeExcursionPolicy policy = MavEnvelopeExcursionPolicy.RejectUnsupportedState;

        public override string DeckName
        {
            get { return "fixed synthetic validation deck (NOT aircraft data)"; }
        }

        public override MavThrustDataAuthority Authority
        {
            get { return declaredAuthority; }
        }

        public override MavEnvelopeExcursionPolicy ExcursionPolicy
        {
            get { return policy; }
        }

        public override MavThrustDeckResult Evaluate(MavThrustDeckQuery query)
        {
            if (declaredAuthority == MavThrustDataAuthority.Unavailable)
                return MavThrustDeckResult.Unavailable("synthetic deck declared Unavailable");

            MavThrustDeckResult result = new MavThrustDeckResult();
            result.valid = true;

            // Scale by power so a power-state change is visible in thrust, which the asymmetric and
            // independence cases rely on.
            result.thrustN = fixedThrustN * Mathf.Clamp(query.actualPowerPercent, 0f, 100f) * 0.01f;
            result.insideEnvelope = reportInsideEnvelope;
            result.authority = declaredAuthority;
            result.statusReason = "synthetic fixed deck";
            return result;
        }
    }
}
