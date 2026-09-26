#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using MaverickFresh.FlightDynamics;
using MaverickFresh.FlightDynamics.F15;
using MaverickFresh.FlightDynamics.F16;
using Object = UnityEngine.Object;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>
    /// WP-4A. Research runtime flight PREREQUISITES for the AFIT/Baumann/Davison research
    /// configuration - validated WITHOUT flying it.
    ///
    /// NOT a PlayMode test and not a trajectory. Every rig is a HideAndDontSave GameObject; no scene
    /// or prefab is opened, modified or saved. Loads at an equilibrium are evaluated through the
    /// body's own SHADOW compute path (compute everything, apply nothing) via the editor-only
    /// StepPhysicsForValidation seam. The few checks that must exercise the single load-application
    /// boundary itself (gravity ownership, the fail-closed environment refusal, the F-16 regression)
    /// arm a hidden rig with the body's sanctioned structural-only override, take one or a few
    /// steps - Unity does not integrate a Rigidbody in edit mode - and disarm.
    ///
    ///   [U1]  default environment path unchanged (standard atmosphere, Unity gravity)
    ///   [U2]  F-16 behaviour unchanged
    ///   [U3]  NASA 836 and every non-research id refuse the research override, hold and injection
    ///   [U4]  the research configuration receives the exact fixed source density
    ///   [U5]  gravity policy: audit, source gravity through the load set once, ownership guards
    ///   [U6]  research fixed thrust applied once; no throttle, no F100, total not per engine
    ///   [U7]  thrust-line moment applied once
    ///   [U8]  the demonstrated range never reports itself as physical
    ///   [U9]  no actuator rate (or travel) authority appears
    ///   [U10] no direct Rigidbody writes from research components
    ///   [U11] initialization round-trips all 170 WP-3B/WP-3C equilibria
    ///   [U12] static load consistency at the injected equilibria, per environment (no flight)
    ///   [U13] runtime-preparation diagnostics; condition modes retained; live takeover off
    ///   [U14] WP-3A/B/C/D/E datasets and protected sources unchanged
    /// </summary>
    public static class MavF15ResearchRuntimePrerequisitesValidation
    {
        private const float FixedDeltaTime = 0.02f;
        private const double RadToDeg = 180.0 / Math.PI;

        /// <summary>
        /// [U12] band for the source environment: an order of magnitude below the smallest
        /// environment gap it must resolve (gravity, 3.43e-4). NUMERICAL - float32 loads against a
        /// double-precision trim - not a physical tolerance.
        /// </summary>
        private const double SourceEnvironmentResidualBand = 3.4e-5;

        private static readonly float[] AtmosphereProbeAltitudesM = { -500f, 0f, 3000f, 6096f, 11000f, 18000f, 25000f };

        /// <summary>SHA-256 (LF-normalized) of every WP-3A/B/C/D/E dataset and protected source, as of 68952f3.</summary>
        private static readonly string[][] ProtectedFiles =
        {
            new[] { "Docs/Reference/Data/F15/table_vii/baumann_table_vii_part1.txt", "97e64a3aa618118b03fb46852bbd3e808e7b9535a7cbf636d9e479f705bd3c9b" },
            new[] { "Docs/Reference/Data/F15/table_vii/baumann_table_vii_part2.txt", "0d99d473e5f5e9bd84aabd6ab31c0d0bb65dc2f20adeab2c6773452e36f20a85" },
            new[] { "Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavF15BaumannTableViiData.cs", "102c89853bb927fddce0b67374e4c3de290d8c7cbffa2c351cf66f048108905b" },
            new[] { "Docs/Reference/Data/F15/stability/f15_research_stability_crossings.csv", "f1a4a24b5d23ced6e13c0407748639aef4896c7fc183f0b4dd936064fdf852e8" },
            new[] { "Docs/Reference/Data/F15/stability/f15_research_stability_jacobians.csv", "370debb69e0f34a5a6ea9cf2612283f6c1a04f25c81c1b42a2907888b3159d1d" },
            new[] { "Docs/Reference/Data/F15/stability/f15_research_stability_pitchfork_approach.csv", "dd0d51c84c9b7bd48ab3761f701750f40b15d71e21b29e0ac6d6f38020931db9" },
            new[] { "Docs/Reference/Data/F15/stability/f15_research_stability_step_audit.csv", "34c080bc9afa23453f946b0b9cef3e0e15024aeaebc6b66314ae323e0da871f4" },
            new[] { "Docs/Reference/Data/F15/stability/f15_research_stability_table_vii.csv", "bd7065bf9b4b9e36db5da27d494214c4e714a8573e213d43929d13afa868e47e" },
            new[] { "Docs/Reference/Data/F15/stability_time_domain/f15_stability_conflict_hypotheses.csv", "6e602398055c1fa0fab2880e185012b95546b84fbda21de5c6f30b21523474b2" },
            new[] { "Docs/Reference/Data/F15/stability_time_domain/f15_table_vii_step_ratios.csv", "65d6315f12e09b6da8038ecae15e105dd721d7ef6f67b03cc3fe5bbd96c3c1a6" },
            new[] { "Docs/Reference/Data/F15/stability_time_domain/f15_time_domain_dt_audit.csv", "7a0a66c1e1498ac905049d6718d99aa1874b8a50d270d6c4b73b2d6b22880b5a" },
            new[] { "Docs/Reference/Data/F15/stability_time_domain/f15_time_domain_measurements.csv", "f0951bb9e1ac18491f71c16d7e84d69d185eb3f4b47431a129ae59f5f146db2c" },
            new[] { "Docs/Reference/Data/F15/stability_time_domain/f15_time_domain_mirror.csv", "82e115f373c7709893ba909bf7c4a47259313aae7cd00caa58219a038e08d5d5" },
            new[] { "Docs/Reference/Data/F15/stability_time_domain/f15_time_domain_symmetry_breaking.csv", "43aee83278720ba80910aa19cf85f35de5343ea0a1a035e92940a9f5f1341195" },
            new[] { "Docs/Reference/Data/F15/stability_time_domain/f15_time_domain_trajectories.csv", "965309c523e39587dfc0a2c075856053972f4644916f9a8c739ac9a3f621d3c4" },
            new[] { "Assets/MaverickFresh/Scripts/FlightDynamics/F15/MavF15AfitResearchSourceDynamics.cs", "9026632d61c722346ddc84e063da13bed15bf3628a65e56c26f61c05cba24fe3" },
            new[] { "Assets/MaverickFresh/Scripts/FlightDynamics/F15/MavF15AfitResearchTrimSolver.cs", "6b54e72b6e25050c062f4ea927bdbcc290a62275f355eb621349c28f7c8754b5" },
            new[] { "Assets/MaverickFresh/Scripts/FlightDynamics/F15/MavF15BaumannMach06Longitudinal.cs", "c58dc64b5c457ec16a9ef0b265bc7d2b3ecc178cab14858505cfb8492fae6399" },
            new[] { "Assets/MaverickFresh/Scripts/FlightDynamics/F15/MavF15BaumannMach06LateralDirectional.cs", "cd0daeec7915e0c0d64ce70ea3d04d89e8581824401721517f4c4f59c79250f9" },
            new[] { "Assets/MaverickFresh/Scripts/FlightDynamics/Core/MavAtmosphereModel.cs", "0d1b45cdee3ef439c0a629ca8d05a44f755ad894ef8a37bc3c2dab73f5860e90" },
            new[] { "Assets/MaverickFresh/Scripts/FlightDynamics/F15/MavF15BaumannSourceSemantics.cs", "c796d1145c632b989c34006381e059a1d8208bed816e9da8c29a1c5793fe2c02" },
            new[] { "Assets/MaverickFresh/Scripts/FlightDynamics/F15/MavF15AfitResearchFixedThrust.cs", "dc25163ef1e386096afeee7462e38c6bf583663225b76754ca92baa4e7bd6b6b" },
            new[] { "Assets/MaverickFresh/Scripts/FlightDynamics/F15/MavF15AeroModel.cs", "3e7b25fd000e4386b893939b10f64cd576ec9272a6a76450041940d0292d3d03" },
            new[] { "Assets/MaverickFresh/Scripts/FlightDynamics/F15/MavF15ResearchControlAuthority.cs", "3302418a90f1ad3e6be9db294c3725fa88e750e165b316fa9ec6b042c18d2250" },
            new[] { "Assets/MaverickFresh/Scripts/FlightDynamics/F15/MavF15AfitResearchMassReference.cs", "b0e48d795c8a71324e65f6c5045bbfb3456ed37db0da76265ba9869a6b4fc431" },
            new[] { "Assets/MaverickFresh/Scripts/FlightDynamics/F15/MavF15BaumannMach06Reference.cs", "5065f7e40dede31f7e3e1e41af9f5bce30e11749f18c997bbed32bbfc786f408" },
            new[] { "Assets/MaverickFresh/Scripts/FlightDynamics/F15/MavF15BaumannMach06Domain.cs", "90e7d1ba44f48552b1fc4987f4837e24e2c668533d21a708df8c9de1687dec89" },
            new[] { "Assets/MaverickFresh/Scripts/FlightDynamics/F15/MavF15ReferenceData.cs", "7b397d26b5e16c45f1afd7d775bec6ec0c27adea8a4933d4ed1f3e12d16d2314" },
            new[] { "Assets/MaverickFresh/Scripts/FlightDynamics/F15/MavF15FlightDynamicsProfile.cs", "c8dc1f12c8779a397d52489c3b3e54dc5a4c243e124b0bc1ec28a553fa1bc4ed" },
            new[] { "Assets/MaverickFresh/Scripts/FlightDynamics/F15/MavF15Nasa836FcsStructure.cs", "75e28522c353475032509d4d87118736bff6752e6c9327e4a3bdaa32a3b5c6e0" },
            new[] { "Assets/MaverickFresh/Scripts/FlightDynamics/F15/MavF15MassReference.cs", "df272a2a86eadc67de037b492ee79083b50749145350864fb9d330c2c8c8dbde" },
            new[] { "Assets/MaverickFresh/Scripts/FlightDynamics/F15/MavF15PropulsionSystem.cs", "80600a28e3a11d22bae8ac94267cf79c70b40da8426da7baa7b0f5ff450a9777" },
            new[] { "Assets/MaverickFresh/Scripts/FlightDynamics/F15/MavF100ConfigurationLineage.cs", "7d760fa431ed0904a368967112cf6e8e08c4fa912dba2a711b05173ea58140cb" },
            new[] { "Assets/MaverickFresh/Scripts/FlightDynamics/F15/MavF100DimensionalAnchor.cs", "53d324acc0a41ab829d423276d0604ade8f8507a5e30d255ee19245487d1095d" },
            new[] { "Assets/MaverickFresh/Scripts/FlightDynamics/F15/MavF100EngineFamilies.cs", "aef79604687b519223983a545e87be5cc44e0a820242008a7d1a4d357c135312" },
            new[] { "Assets/MaverickFresh/Scripts/FlightDynamics/F15/MavF100NormalizedNetThrustModel.cs", "54b3f0eb6ba70a048830a16219444c959b009a024f2a96e42b45d4f7167b2ebd" },
            new[] { "Assets/MaverickFresh/Scripts/FlightDynamics/F15/MavF100PropulsionPaths.cs", "feac4c1c4e3c93213f681b58ae0056c7982ce6c30d161a411323f92990be42ac" },
            new[] { "Assets/MaverickFresh/Scripts/FlightDynamics/F15/MavF100SourceData.cs", "a882d9a40706d6968db8d7e07e0d3709ed81967b9cbab258e29c9492c9d13faa" },
            new[] { "Assets/MaverickFresh/Scripts/FlightDynamics/F15/MavF100ThrustDeck.cs", "fee5a75e04dd57b8fb5a56fbb76ea0d1f43ddbea899778f4e3146a0d954a962e" },
            new[] { "Assets/MaverickFresh/Scripts/FlightDynamics/F15/MavF100ThrustSemantics.cs", "b5f5cf010f1433f27462a746d3c11bba773780600d5e196aea0c23cfd7681fe9" }
        };

        /// <summary>Runtime (non-Validation) files WP-4A adds; each is scanned for writes and contamination.</summary>
        private static readonly string[] NewRuntimeFiles =
        {
            "Core/MavFlightEnvironment.cs",
            "F15/MavF15ResearchRuntimeAuthority.cs",
            "F15/MavF15AfitResearchRuntimeEnvironment.cs",
            "F15/MavF15AfitResearchStaticSurfaceHold.cs",
            "F15/MavF15AfitResearchStateInjection.cs",
            "F15/MavF15AfitResearchRuntimePreparation.cs"
        };

        public static string RunAll(out int passed, out int failed)
        {
            passed = 0;
            failed = 0;
            StringBuilder report = new StringBuilder(32768);
            report.AppendLine("F-15 WP-4A Research Runtime Flight Prerequisites (editor seam, NOT PlayMode, nothing flown) - "
                + MavF15AfitResearchIdentity.ConfigurationId);
            report.AppendLine("=====================================================================================================");
            report.AppendLine("RESEARCH SOURCE MODEL ONLY - not the real F-15, NASA 836, the production FCS or a flown Rigidbody. "
                + "Tolerances are NUMERICAL.");

            MavAtmosphereSample[] atmosphereBefore = SampleAtmosphere();
            Vector3 gravityBefore = Physics.gravity;
            List<MavF15ResearchEquilibrium> equilibria = MavF15ResearchStabilityAnalysis.SourceEquilibria();

            Run(ValidateDefaultEnvironment, report, ref passed, ref failed);
            Run(ValidateF16Unchanged, report, ref passed, ref failed);
            Run(ValidateRefusals, report, ref passed, ref failed);
            Run(ValidateFixedDensity, report, ref passed, ref failed);
            Run(ValidateGravity, report, ref passed, ref failed);
            Run(ValidateThrustOnce, report, ref passed, ref failed);
            Run(ValidateThrustMomentOnce, report, ref passed, ref failed);
            Run(ValidateDemonstratedRangeNotPhysical, report, ref passed, ref failed);
            Run(ValidateNoRateAuthority, report, ref passed, ref failed);
            Run(ValidateNoDirectRigidbodyWrites, report, ref passed, ref failed);
            ValidateRoundTrip(equilibria, report, ref passed, ref failed);
            ValidateStaticLoadConsistency(equilibria, report, ref passed, ref failed);
            Run(ValidateDiagnostics, report, ref passed, ref failed);
            ValidateUnchanged(equilibria, atmosphereBefore, gravityBefore, report, ref passed, ref failed);

            report.AppendLine();
            report.Append("RESULT: ").Append(failed == 0 ? "PASS" : "FAIL")
                .Append("  passed=").Append(passed).Append(" failed=").Append(failed);
            return report.ToString();
        }

        private delegate void Section(StringBuilder report, ref int passed, ref int failed);

        private static void Run(Section section, StringBuilder report, ref int passed, ref int failed)
        {
            try
            {
                section(report, ref passed, ref failed);
            }
            catch (Exception e)
            {
                Record(false, "section threw " + e.GetType().Name + ": " + e.Message, report, ref passed, ref failed);
            }
        }

        // ================================================================= [U1]

        private static void ValidateDefaultEnvironment(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[U1] Default environment path unchanged: standard atmosphere, Unity project gravity");

            GameObject host = NewHost("MavWp4aDefaultHost");
            try
            {
                MavSixDoFBody body = host.AddComponent<MavSixDoFBody>();
                MavF15FlightDynamicsProfile exact = host.AddComponent<MavF15FlightDynamicsProfile>();
                MavF16FlightDynamicsProfile f16 = host.AddComponent<MavF16FlightDynamicsProfile>();
                MavF15AfitResearchFlightDynamicsProfile research = host.AddComponent<MavF15AfitResearchFlightDynamicsProfile>();

                bool noneStandard = true, exactStandard = true, f16Standard = true, researchStandard = true;
                foreach (float altitude in AtmosphereProbeAltitudesM)
                {
                    MavAtmosphereSample isa = MavAtmosphereModel.Sample(altitude);

                    body.profileProvider = null;
                    noneStandard &= IsStandard(body.ResolveEnvironment(altitude), isa, altitude, MavFlightEnvironment.StandardStatus);

                    body.profileProvider = exact;
                    exactStandard &= IsStandard(body.ResolveEnvironment(altitude), isa, altitude, MavFlightEnvironment.StandardStatus);

                    body.profileProvider = f16;
                    f16Standard &= IsStandard(body.ResolveEnvironment(altitude), isa, altitude, MavFlightEnvironment.StandardStatus);

                    body.profileProvider = research;
                    researchStandard &= IsStandard(body.ResolveEnvironment(altitude), isa, altitude,
                        MavF15AfitResearchRuntimeEnvironment.DefaultStatus);
                }

                Record(noneStandard && exactStandard && f16Standard,
                    "no provider, the exact NASA 836 provider and the F-16 provider all resolve the shared "
                    + "MavAtmosphereModel sample BIT FOR BIT, Unity project gravity, nothing in the load set, at "
                    + AtmosphereProbeAltitudesM.Length + " altitudes from -500 to 25,000 m",
                    report, ref passed, ref failed);

                Record(researchStandard
                        && research.densityPolicy == MavF15ResearchDensityPolicy.StandardAtmosphere
                        && research.gravityPolicy == MavF15ResearchGravityPolicy.UnityProjectGravity,
                    "the research profile's policies default OFF (StandardAtmosphere, UnityProjectGravity) and then "
                    + "resolve the same standard sample bit for bit - with a status saying the source is not reproduced",
                    report, ref passed, ref failed);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }

            ResearchRig rig = ResearchRig.Build(MavF15ResearchConditionMode.StrictFitCondition,
                MavF15ResearchDensityPolicy.StandardAtmosphere, MavF15ResearchGravityPolicy.UnityProjectGravity, false);
            try
            {
                rig.SetFitCondition(MavF15BaumannMach06Reference.SourcePressureAltitudeM);
                rig.rigidbody.useGravity = false;
                rig.ArmStructural();
                rig.StepLive();
                rig.StepLive();

                MavSixDoFBody b = rig.body;
                MavFlightDynamicsLoadSet set = b.debugLoadSet;
                Record(Same(b.debugAtmosphere, MavAtmosphereModel.Sample(MavF15BaumannMach06Reference.SourcePressureAltitudeM))
                        && b.debugEnvironment.valid && !b.debugEnvironment.OverridesDensity
                        && !b.debugEnvironment.OwnsGravityThroughLoadSet,
                    "a live research body with the default policies flies the shared standard atmosphere, bit for bit",
                    report, ref passed, ref failed);

                Record(b.debugLoadApplications == 2 && set.gravitationalContributions == 0
                        && set.gravitationalForceAeroBodyN == Vector3.zero
                        && SameBits(set.AppliedForceAeroBodyN, set.totalForceAeroBodyN)
                        && SameBits(b.debugUnityLocalForceN, MavFlightDynamicsMath.AeroBodyVectorToUnityLocal(set.totalForceAeroBodyN))
                        && rig.rigidbody.useGravity && b.debugRejectedEnvironmentSteps == 0
                        && b.debugRejectedGravityOwnershipSteps == 0,
                    "and its applied force is the non-gravitational total bit for bit, no gravity channel, "
                    + "Rigidbody.useGravity restored ON by initialization (profile useGravity), no refusal",
                    report, ref passed, ref failed);
            }
            finally
            {
                rig.Destroy();
            }
        }

        private static bool IsStandard(MavFlightEnvironment e, MavAtmosphereSample isa, float altitude, string status)
        {
            return e.valid && Same(e.atmosphere, isa) && e.densitySource == MavDensitySource.StandardAtmosphere
                && e.gravitySource == MavGravitySource.UnityProjectGravity && e.loadSetGravityMps2 == 0f
                && e.geometricAltitudeM == altitude && e.status == status;
        }

        // ================================================================= [U2]

        private static void ValidateF16Unchanged(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[U2] F-16 behaviour unchanged");

            GameObject go = NewHost("MavWp4aF16Rig");
            try
            {
                MavSixDoFBody body = go.AddComponent<MavSixDoFBody>();
                Rigidbody rb = go.GetComponent<Rigidbody>();
                MavF16FlightDynamicsProfile profile = go.AddComponent<MavF16FlightDynamicsProfile>();
                MavF16AeroModel aero = go.AddComponent<MavF16AeroModel>();
                MavF16ControlActuator actuator = go.AddComponent<MavF16ControlActuator>();
                MavManualPilotCommandSource command = go.AddComponent<MavManualPilotCommandSource>();
                MavF16ControlLawV01 law = go.AddComponent<MavF16ControlLawV01>();
                MavF16EnginePowerModel engine = go.AddComponent<MavF16EnginePowerModel>();

                body.profileProvider = profile;
                body.aerodynamicModel = aero;
                body.controlSurfaceActuator = actuator;
                body.controlLaw = law;
                body.pilotCommandSource = command;
                body.propulsionModel = engine;
                body.simulationEnabled = false;
                actuator.sixDoFBody = body;
                law.sixDoFBody = body;
                law.actuator = actuator;
                law.commandSource = command;
                law.driveActuatorInFixedUpdate = true;
                law.ResetLawState();
                command.command = MavPilotCommand.Neutral;
                body.ApplyConfiguredProfile(true);
                body.NotifyOwnershipChanged();

                const float altitude = 3000f;
                go.transform.position = new Vector3(0f, altitude, 0f);
                go.transform.rotation = Quaternion.identity;
                rb.linearVelocity = new Vector3(0f, 0f, 200f);

                body.simulationEnabled = true;
                body.allowStructuralOnlyLoadApplication = true;
                float time = 0f;
                for (int i = 0; i < 3; i++)
                {
                    time += FixedDeltaTime;
                    law.StepControlLaw(FixedDeltaTime);
                    actuator.StepActuator(FixedDeltaTime);
                    body.StepPhysicsForValidation(FixedDeltaTime, time);
                }

                MavFlightDynamicsLoadSet set = body.debugLoadSet;
                MavAtmosphereSample isa = MavAtmosphereModel.Sample(altitude);
                Record(body.debugEnvironment.valid && Same(body.debugAtmosphere, isa)
                        && body.debugEnvironment.status == MavFlightEnvironment.StandardStatus
                        && !body.debugEnvironment.OverridesDensity && !body.debugEnvironment.OwnsGravityThroughLoadSet,
                    "an armed F-16 body resolves the standard environment: the shared atmosphere at 3,000 m bit for bit, Unity gravity",
                    report, ref passed, ref failed);

                Record(body.debugLoadApplications == 3 && set.gravitationalContributions == 0
                        && set.gravitationalForceAeroBodyN == Vector3.zero
                        && SameBits(set.AppliedForceAeroBodyN, set.totalForceAeroBodyN)
                        && SameBits(body.debugUnityLocalForceN, MavFlightDynamicsMath.AeroBodyVectorToUnityLocal(set.totalForceAeroBodyN))
                        && rb.useGravity == body.activeProfile.useGravity
                        && body.debugRejectedEnvironmentSteps == 0 && body.debugRejectedGravityOwnershipSteps == 0,
                    "3/3 steps applied; the applied force is its non-gravitational total bit for bit (no gravity channel); "
                    + "Rigidbody.useGravity is the profile's; no environment or gravity refusal",
                    report, ref passed, ref failed);

                MavControlInput bounded = body.activeProfile.controlSurfaceLimits.Clamp(body.controlInput);
                MavAeroCoefficients c = aero.Evaluate(body.debugState, bounded, isa);
                MavAerodynamicLoads expected = MavFlightDynamicsMath.Dimensionalize(
                    c, aero.referenceGeometry, body.debugState.dynamicPressurePa);
                Record(SameBits(expected.forceAeroBodyN, set.aerodynamic.forceAeroBodyN)
                        && SameBits(expected.momentAeroBodyNm, set.aerodynamic.momentAeroBodyNm)
                        && set.aerodynamicContributions == 1 && set.propulsiveContributions == 1,
                    "the F-16 aerodynamic load equals an independent evaluation in the shared atmosphere, bit for bit "
                    + "(force " + set.aerodynamic.forceAeroBodyN.ToString("F1") + " N)",
                    report, ref passed, ref failed);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }

            report.AppendLine("    the 22 synchronous FDM baseline suites (F-16, shared propulsion, phases 1-3, ownership, identity) are");
            report.AppendLine("    run separately before and after WP-4A and compared; see F15_RESEARCH_RUNTIME_PREREQUISITES_V1.0.md");
        }

        // ================================================================= [U3]

        private static void ValidateRefusals(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[U3] NASA 836 and every non-research id refuse the research override, hold and injection");

            string reason;
            bool nasa = !MavF15ResearchRuntimeAuthority.TryGrant(MavF15ReferenceData.TargetConfigurationId, out reason)
                && reason.Contains("exact NASA F-15B 836");
            report.Append("    ").AppendLine(reason);

            string[] refused =
            {
                null, "", " ", "unconfigured", "f15-nasa836-pre-quiet-spike-r1", "f16-morelli-clean-subsonic-v0.1",
                MavF15AfitResearchIdentity.ConfigurationId.ToLowerInvariant(),
                MavF15AfitResearchIdentity.ConfigurationId + " ", " " + MavF15AfitResearchIdentity.ConfigurationId,
                MavF15AfitResearchIdentity.ConfigurationId + "_EXACT", MavF15AfitResearchIdentity.ConfigurationId + "_2",
                "F15_AFIT_BAUMANN_DAVISON_MACH06_20K", MavF15AfitResearchMassReference.ResearchMassStateId,
                MavF15ReferenceData.TargetMassStateId
            };
            int refusedCount = 0;
            foreach (string id in refused)
            {
                if (!MavF15ResearchRuntimeAuthority.TryGrant(id, out reason))
                    refusedCount++;
            }

            bool research = MavF15ResearchRuntimeAuthority.TryGrant(MavF15AfitResearchIdentity.ConfigurationId, out reason);
            Record(nasa && refusedCount == refused.Length && research,
                "authority: the exact NASA 836 id is refused BY NAME; " + refusedCount + "/" + refused.Length
                + " other ids refused (null, empty, the exact 836 profile, the F-16, case/space/suffix look-alikes, "
                + "mass-state ids); only " + MavF15AfitResearchIdentity.ConfigurationId + " is granted, ordinally",
                report, ref passed, ref failed);

            // A body flying the exact NASA 836 profile, with every research component beside it, opted in.
            ResearchRig rig = ResearchRig.Build(MavF15ResearchConditionMode.SourceReproduction,
                MavF15ResearchDensityPolicy.SourceFixedDensity, MavF15ResearchGravityPolicy.SourceGravity, true);
            try
            {
                MavF15FlightDynamicsProfile exact = rig.gameObject.AddComponent<MavF15FlightDynamicsProfile>();
                rig.body.profileProvider = exact;
                rig.body.ApplyConfiguredProfile(true);

                MavAtmosphereSample isa = MavAtmosphereModel.Sample(6096f);
                MavFlightEnvironment viaBody = rig.body.ResolveEnvironment(6096f);
                MavFlightEnvironment viaResearch = rig.profile.ResolveEnvironment(rig.body, isa, 6096f);
                Record(IsStandard(viaBody, isa, 6096f, MavFlightEnvironment.StandardStatus)
                        && !viaResearch.valid && viaResearch.status.StartsWith("REFUSED"),
                    "exact-836 body: its own provider resolves the standard environment (the research override never "
                    + "reaches it); asked directly, the research profile REFUSES: " + viaResearch.status,
                    report, ref passed, ref failed);

                MavF15ResearchStaticSurfaceSetting setting;
                MavF15ResearchStaticSurfaceSetting.TryCreate(-6.1654f, "validation", out setting, out reason);
                bool holdRefused = !rig.hold.TryEngage(setting, out reason);
                MavF15ResearchInitializationReport init = MavF15AfitResearchStateInjection.TryInitialize(
                    rig.body, InitialState(-6.0, 10.0, 450.0, "validation"));
                MavF15ResearchRuntimePreparationReport prep = MavF15AfitResearchRuntimePreparation.Evaluate(rig.body);
                Record(holdRefused && !init.initialized && !prep.prepared && !prep.authorityGranted
                        && rig.body.debugInitialStateApplications == 0,
                    "exact-836 body: the static hold, state injection and preparation all refuse, and no initial state "
                    + "was applied (" + init.reason + ")",
                    report, ref passed, ref failed);
            }
            finally
            {
                rig.Destroy();
            }

            // A research body whose built profile id has been tampered with: fail closed, never fall back.
            rig = ResearchRig.Build(MavF15ResearchConditionMode.SourceReproduction,
                MavF15ResearchDensityPolicy.SourceFixedDensity, MavF15ResearchGravityPolicy.SourceGravity, true);
            try
            {
                MavF15ResearchInitializationReport init = MavF15AfitResearchStateInjection.TryInitialize(
                    rig.body, InitialState(-8.0, 12.0, 450.0, "validation"));
                rig.body.autoApplyProfileConfiguration = false;
                rig.body.activeProfile.profileId = MavF15ReferenceData.TargetConfigurationId;

                rig.ArmStructural();
                int appliedBefore = rig.body.debugLoadApplications;
                rig.StepLive();
                MavFlightDynamicsLoadSet set = rig.body.debugLoadSet;
                Record(init.initialized && !rig.body.debugEnvironment.valid
                        && rig.body.debugRejectedEnvironmentSteps == 1
                        && rig.body.debugLoadApplications == appliedBefore
                        && set.aerodynamicContributions == 0 && set.propulsiveContributions == 0
                        && set.gravitationalContributions == 0 && set.totalForceAeroBodyN == Vector3.zero,
                    "a research body re-labelled with the NASA 836 id: the environment is REFUSED and the armed step "
                    + "applies NOTHING - no silent fallback to the standard atmosphere (" + rig.body.debugEnvironment.status + ")",
                    report, ref passed, ref failed);

                Record(!rig.actuator.debugResearchStaticHold
                        && rig.actuator.ActualF15SurfaceState.channels.symmetricStabilatorDeg == 0f
                        && rig.actuator.debugStatus.StartsWith("RESEARCH STATIC HOLD REFUSED"),
                    "and the engaged static hold is refused with it: surfaces held NEUTRAL, never the command ("
                    + rig.actuator.debugStatus.Substring(0, Math.Min(90, rig.actuator.debugStatus.Length)) + "...)",
                    report, ref passed, ref failed);
            }
            finally
            {
                rig.Destroy();
            }
        }

        // ================================================================= [U4]

        private static void ValidateFixedDensity(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[U4] The research configuration receives the exact fixed source density");

            // Independent route: 1 slug = 0.45359237 kg x 9.80665 / 0.3048 (lbm, g0, ft), 1 ft^3 = 0.3048^3 m^3.
            double slugKg = 0.45359237 * 9.80665 / 0.3048;
            double independent = slugKg / (0.3048 * 0.3048 * 0.3048);
            double rhoSi = MavF15SourceExercisedOperatingDomain.SourceAirDensitySlugPerFt3 * MavF15AfitResearchRuntimeEnvironment.SlugPerFt3ToKgPerM3;
            Record(Math.Abs(MavF15AfitResearchRuntimeEnvironment.SlugPerFt3ToKgPerM3 / independent - 1.0) < 1e-12
                    && MavF15AfitResearchRuntimeEnvironment.SourceDensityKgM3 == (float)rhoSi
                    && Math.Abs(rhoSi - 0.65313958) < 1e-8,
                "RHO 0.0012673 slug/ft^3 = " + rhoSi.ToString("F7") + " kg/m^3 (1 slug/ft^3 = "
                + MavF15AfitResearchRuntimeEnvironment.SlugPerFt3ToKgPerM3.ToString("F6") + " kg/m^3, lbf-to-N over ft^4, "
                + "checked against lbm x g0 / ft^4 to 1e-12)",
                report, ref passed, ref failed);

            ResearchRig rig = ResearchRig.Build(MavF15ResearchConditionMode.SourceReproduction,
                MavF15ResearchDensityPolicy.SourceFixedDensity, MavF15ResearchGravityPolicy.UnityProjectGravity, true);
            try
            {
                bool exact = true, qBits = true, admitted = true, qMatch = true;
                double worstQ = 0.0;
                foreach (float altitude in new[] { 0f, 6096f, 9000f })
                {
                    MavF15ResearchRuntimeInitialState s = InitialState(-8.0, 12.0, 450.0, "validation");
                    s.altitudeM = altitude;
                    MavF15ResearchInitializationReport init = MavF15AfitResearchStateInjection.TryInitialize(rig.body, s);
                    rig.ShadowStep();

                    MavSixDoFBody b = rig.body;
                    float tas = b.debugState.trueAirspeedMps;
                    exact &= init.initialized && b.debugEnvironment.valid
                        && b.debugEnvironment.densitySource == MavDensitySource.ResearchSourceFixedDensity
                        && b.debugAtmosphere.densityKgM3 == MavF15AfitResearchRuntimeEnvironment.SourceDensityKgM3
                        && b.debugAtmosphere.altitudeM == MavF15AfitResearchRuntimeEnvironment.SourceDensityAltitudeLabelM
                        && b.debugEnvironment.geometricAltitudeM == altitude;

                    // Explicit cast: the body's q is rounded to float when it is stored, and an
                    // unstored float expression may be evaluated at higher precision.
                    qBits &= b.debugState.dynamicPressurePa
                        == (float)(0.5f * MavF15AfitResearchRuntimeEnvironment.SourceDensityKgM3 * tas * tas);

                    double vFt = tas / 0.3048;
                    double qSourcePa = 0.5 * MavF15SourceExercisedOperatingDomain.SourceAirDensitySlugPerFt3 * vFt * vFt
                        * (MavF15MassReference.PoundForceToNewton / (0.3048 * 0.3048));
                    double qError = Math.Abs(b.debugState.dynamicPressurePa / qSourcePa - 1.0);
                    worstQ = Math.Max(worstQ, qError);
                    qMatch &= qError < 1e-6;
                    admitted &= !rig.aero.debugRefused && !rig.thrust.debugRefused && b.debugShadowLoadSetFinite;
                }

                Record(exact,
                    "at geometric 0, 6,096 and 9,000 m the body's sample carries RHO exactly (float), labelled with the "
                    + "source's 20,000-ft density altitude, with the true altitude reported separately",
                    report, ref passed, ref failed);
                Record(qBits,
                    "and the body's dynamic pressure is 0.5 RHO V^2 from that sample, bit for bit",
                    report, ref passed, ref failed);
                Record(qMatch,
                    "the body's q equals the source's QBARS/SREF = 0.5 RHO VTRFPS^2 to " + worstQ.ToString("E1")
                    + " (float), at every altitude",
                    report, ref passed, ref failed);
                Record(admitted,
                    "SourceReproduction admits research aero and thrust at every geometric altitude under the fixed density: "
                    + "altitude is not a source state",
                    report, ref passed, ref failed);
            }
            finally
            {
                rig.Destroy();
            }

            rig = ResearchRig.Build(MavF15ResearchConditionMode.SourceReproduction,
                MavF15ResearchDensityPolicy.StandardAtmosphere, MavF15ResearchGravityPolicy.UnityProjectGravity, true);
            try
            {
                MavF15ResearchRuntimeInitialState s = InitialState(-8.0, 12.0, 450.0, "validation");
                MavF15AfitResearchStateInjection.TryInitialize(rig.body, s);
                rig.ShadowStep();
                double ratio = rig.body.debugAtmosphere.densityKgM3 / (double)MavF15AfitResearchRuntimeEnvironment.SourceDensityKgM3;
                bool standardAt6096 = rig.body.debugEnvironment.densitySource == MavDensitySource.StandardAtmosphere
                    && !rig.aero.debugRefused;

                s.altitudeM = 9000f;
                MavF15AfitResearchStateInjection.TryInitialize(rig.body, s);
                rig.ShadowStep();
                bool refusedAt9000 = rig.aero.debugRefused && rig.thrust.debugRefused;

                Record(standardAt6096 && Math.Abs(ratio - 1.0 + 6.83e-4) < 0.02e-4 && refusedAt9000,
                    "without the override the gap is what WP-3B recorded: ISA/RHO - 1 = " + (ratio - 1.0).ToString("E3")
                    + " at 6,096 m, and at 9,000 m the unchanged altitude gate refuses research aero and thrust",
                    report, ref passed, ref failed);
            }
            finally
            {
                rig.Destroy();
            }
        }

        // ================================================================= [U5]

        private static void ValidateGravity(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[U5] Gravity: audit, source gravity through the load set once, ownership guards");

            Vector3 projectGravity = Physics.gravity;
            double unity = projectGravity.magnitude;
            double source = MavF15SourceExercisedOperatingDomain.SourceGravityFtPerSec2 * 0.3048;
            double gap = unity / source - 1.0;
            report.Append("    project Physics.gravity ").Append(projectGravity.ToString("F5")).Append(" m/s^2 vs source G 32.174 ft/s^2 = ")
                .Append(source.ToString("F7")).Append(" m/s^2: relative ").AppendLine(gap.ToString("E3"));
            Record(Math.Abs(gap - 3.43e-4) < 0.01e-4 && MavF15AfitResearchRuntimeEnvironment.SourceGravityMps2 == (float)source,
                "the audit: Unity's project gravity is " + gap.ToString("E3") + " off the source's G - the same order as the "
                + "density gap, so runtime equivalence needs BOTH overrides (measured in [U12])",
                report, ref passed, ref failed);

            ResearchRig rig = ResearchRig.Build(MavF15ResearchConditionMode.SourceReproduction,
                MavF15ResearchDensityPolicy.SourceFixedDensity, MavF15ResearchGravityPolicy.SourceGravity, true);
            try
            {
                MavF15ResearchRuntimeInitialState s = InitialState(-6.4219, 10.42, 438.4, "validation");
                s.thetaRad = 10.047 / RadToDeg;
                s.phiRad = -41.05 / RadToDeg;
                MavF15AfitResearchStateInjection.TryInitialize(rig.body, s);
                rig.rigidbody.useGravity = true;
                rig.ArmStructural();
                rig.StepLive();

                MavSixDoFBody b = rig.body;
                MavFlightDynamicsLoadSet set = b.debugLoadSet;
                float weight = rig.rigidbody.mass * MavF15AfitResearchRuntimeEnvironment.SourceGravityMps2;
                double th = s.thetaRad, ph = s.phiRad;
                Vector3 expected = new Vector3((float)(-weight * Math.Sin(th)),
                    (float)(weight * Math.Cos(th) * Math.Sin(ph)), (float)(weight * Math.Cos(th) * Math.Cos(ph)));

                Record(!rig.rigidbody.useGravity && Physics.gravity == projectGravity && b.debugLoadApplications == 1
                        && set.gravitationalContributions == 1,
                    "initialization turned Rigidbody.useGravity OFF because the environment owns gravity; Physics.gravity "
                    + "untouched; one step applied with exactly one gravity contribution",
                    report, ref passed, ref failed);

                Record(Math.Abs(set.gravitationalForceAeroBodyN.magnitude / weight - 1.0) < 1e-6
                        && (set.gravitationalForceAeroBodyN - expected).magnitude < 2e-6f * weight,
                    "gravity in body axes = m G (-sin theta, cos theta sin phi, cos theta cos phi) at theta 10.05, phi -41.05 deg: "
                    + set.gravitationalForceAeroBodyN.ToString("F1") + " N",
                    report, ref passed, ref failed);

                Record(SameBits(b.debugUnityLocalForceN, MavFlightDynamicsMath.AeroBodyVectorToUnityLocal(set.totalForceAeroBodyN + set.gravitationalForceAeroBodyN))
                        && SameBits(b.debugState.specificForceAeroBodyG, MavSixDoFBody.ComputeSpecificForceG(set.totalForceAeroBodyN, rig.rigidbody.mass)),
                    "the applied force is the non-gravitational total PLUS gravity, handed over once; the published specific "
                    + "force (accelerometer) EXCLUDES gravity",
                    report, ref passed, ref failed);

                int appliedBefore = b.debugLoadApplications;
                rig.rigidbody.useGravity = true;
                rig.StepLive();
                bool duplicateRefused = b.debugLoadApplications == appliedBefore && b.debugRejectedGravityOwnershipSteps == 1
                    && b.debugGravityStatus.Contains("twice");

                rig.rigidbody.useGravity = false;
                rig.profile.gravityPolicy = MavF15ResearchGravityPolicy.UnityProjectGravity;
                rig.StepLive();
                bool lostRefused = b.debugLoadApplications == appliedBefore && b.debugRejectedGravityOwnershipSteps == 2
                    && b.debugGravityStatus.Contains("no gravity would be applied");

                Record(duplicateRefused && lostRefused,
                    "ownership guard: Unity gravity switched back ON while the load set owns gravity -> the step applies "
                    + "NOTHING (twice); gravity handed back to Unity with useGravity still OFF -> NOTHING (none). Never both, never neither",
                    report, ref passed, ref failed);

                double sourceWeightN = MavF15AfitResearchMassReference.WeightLb * MavF15MassReference.PoundForceToNewton;
                report.Append("    weight: m G = ").Append(weight.ToString("F1")).Append(" N vs the source's 37,000 lbf = ")
                    .Append(sourceWeightN.ToString("F1")).Append(" N (")
                    .Append((weight / sourceWeightN - 1.0).ToString("E2"))
                    .AppendLine("; the research mass is 37,000 lb x 0.45359237 kg, the source's RMASS 37000/32.174 slug)");
            }
            finally
            {
                rig.Destroy();
            }
        }

        // ================================================================= [U6] / [U7]

        private static void ValidateThrustOnce(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[U6] Research fixed thrust: total aircraft, applied once, no throttle, no F100, no NASA 836 path");

            ResearchRig rig = ResearchRig.Build(MavF15ResearchConditionMode.SourceReproduction,
                MavF15ResearchDensityPolicy.SourceFixedDensity, MavF15ResearchGravityPolicy.SourceGravity, true);
            try
            {
                MavF15AfitResearchStateInjection.TryInitialize(rig.body, InitialState(-7.0, 11.0, 420.0, "validation"));
                rig.command.command = MavPilotCommand.Neutral;
                rig.ShadowStep();
                MavFlightDynamicsLoadSet set = rig.body.debugLoadSet;

                Record(set.propulsiveContributions == 1
                        && set.propulsive.forceAeroBodyN == new Vector3(MavF15AfitResearchThrustSource.TotalThrustN, 0f, 0f)
                        && set.propulsive.reportedThrustN == MavF15AfitResearchThrustSource.TotalThrustN
                        && set.propulsive.contributingEngineCount == 0 && !set.propulsive.hasAuthoritativeData
                        && Math.Abs(MavF15AfitResearchThrustSource.TotalThrustN / (8300.0 * 4.4482216152605) - 1.0) < 1e-7,
                    "one propulsive contribution: " + MavF15AfitResearchThrustSource.TotalThrustN.ToString("F2")
                    + " N = 8,300 lbf TOTAL along body +X; no engine split (0 engines reported), non-authoritative",
                    report, ref passed, ref failed);

                MavAeroCoefficients routine = MavF15BaumannMach06Longitudinal.Evaluate(
                    rig.body.debugState.alphaRad, rig.actuator.ActualF15SurfaceState.channels.symmetricStabilatorDeg, rig.aero.debugQHat);
                Record(rig.aero.debugLastCoefficients.cx == routine.cx
                        && set.totalForceAeroBodyN.x == set.aerodynamic.forceAeroBodyN.x + set.propulsive.forceAeroBodyN.x,
                    "the aerodynamic CX is the transcribed routine's bit for bit - no THRUST/QBARS inside it - and the total "
                    + "X is aero + thrust exactly: thrust enters once",
                    report, ref passed, ref failed);

                rig.command.command = new MavPilotCommand { throttle01 = 1f };
                rig.ShadowStep();
                float high = rig.body.debugLoadSet.propulsive.forceAeroBodyN.x;
                rig.command.command = new MavPilotCommand { throttle01 = 0f };
                rig.ShadowStep();
                float low = rig.body.debugLoadSet.propulsive.forceAeroBodyN.x;
                Record(high == MavF15AfitResearchThrustSource.TotalThrustN && low == high
                        && rig.thrust.debugStatus.Contains("throttle ignored"),
                    "throttle 1 and throttle 0 give the same 8,300 lbf; the ignored throttle is reported",
                    report, ref passed, ref failed);

                MavFlightDynamicsLoadSet manual = new MavFlightDynamicsLoadSet();
                manual.BeginStep(1);
                bool first = manual.AddPropulsive(set.propulsive);
                bool second = manual.AddPropulsive(set.propulsive);
                string why;
                bool refusedApplication = !manual.TryMarkApplied(out why);
                Record(first && !second && !manual.HasSingleOwnerPerSource && refusedApplication,
                    "a second propulsive contribution is rejected and the set refuses application: " + why,
                    report, ref passed, ref failed);

                MavNullPropulsionModel extra = rig.gameObject.AddComponent<MavNullPropulsionModel>();
                MavF15ResearchRuntimePreparationReport twoModels = MavF15AfitResearchRuntimePreparation.Evaluate(rig.body);
                rig.body.propulsionModel = extra;
                MavF15ResearchRuntimePreparationReport wrongModel = MavF15AfitResearchRuntimePreparation.Evaluate(rig.body);
                rig.body.propulsionModel = rig.thrust;
                Object.DestroyImmediate(extra);
                Record(!twoModels.prepared && twoModels.blockers.Contains("2 propulsion models")
                        && !wrongModel.prepared && wrongModel.thrustSemantic.StartsWith("NOT the research fixed total thrust"),
                    "preparation blocks a second propulsion model on the aircraft, and any propulsion model but the research "
                    + "fixed thrust",
                    report, ref passed, ref failed);
            }
            finally
            {
                rig.Destroy();
            }

            string root = FlightDynamicsRoot();
            string offender = null;
            string[] forbidden = { "MavF100", "MavThrustDeck", "MavF15PropulsionSystem", "MavEngineRuntime", "MavF15FlightControlSystem", "MavF15Nasa836" };
            foreach (string file in NewRuntimeFiles)
            {
                string code = CodeOf(Path.Combine(root, file));
                foreach (string f in forbidden)
                {
                    if (code == null || code.IndexOf(f, StringComparison.Ordinal) >= 0)
                        offender = file + (code == null ? " unreadable" : " names " + f);
                }
            }

            string[] newTypes = { "MavF15ResearchRuntimeAuthority", "MavF15AfitResearchRuntimeEnvironment",
                "MavF15AfitResearchStaticSurfaceHold", "MavF15ResearchStaticSurfaceSetting", "MavF15AfitResearchStateInjection",
                "MavF15AfitResearchRuntimePreparation", "MavFlightEnvironment" };
            int f100Files = 0;
            foreach (string f in Directory.GetFiles(Path.Combine(root, "F15"), "MavF100*.cs"))
            {
                f100Files++;
                string text = File.ReadAllText(f);
                foreach (string t in newTypes)
                {
                    if (text.IndexOf(t, StringComparison.Ordinal) >= 0)
                        offender = Path.GetFileName(f) + " names " + t;
                }
            }

            Record(offender == null && f100Files > 0,
                offender == null
                    ? "no WP-4A runtime file names an F100 type, a thrust deck, the F-15 F100 propulsion system or a NASA 836 FCS/data type, "
                      + "and none of " + f100Files + " MavF100 sources names a WP-4A type"
                    : "VIOLATION: " + offender,
                report, ref passed, ref failed);
        }

        private static void ValidateThrustMomentOnce(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[U7] The 0.25-in thrust-line moment: applied once");

            ResearchRig rig = ResearchRig.Build(MavF15ResearchConditionMode.SourceReproduction,
                MavF15ResearchDensityPolicy.SourceFixedDensity, MavF15ResearchGravityPolicy.SourceGravity, true);
            try
            {
                MavF15AfitResearchStateInjection.TryInitialize(rig.body, InitialState(-7.0, 11.0, 420.0, "validation"));
                rig.ShadowStep();
                MavFlightDynamicsLoadSet set = rig.body.debugLoadSet;

                double sourceMomentNm = 8300.0 * (0.25 / 12.0) * MavF15MassReference.SlugFt2ToKgM2;
                Record(set.propulsive.momentAeroBodyNm == new Vector3(0f, MavF15AfitResearchThrustSource.ThrustLinePitchingMomentNm, 0f)
                        && Math.Abs(MavF15AfitResearchThrustSource.ThrustLinePitchingMomentNm / sourceMomentNm - 1.0) < 1e-6,
                    "one propulsive moment, nose-up " + MavF15AfitResearchThrustSource.ThrustLinePitchingMomentNm.ToString("F3")
                    + " N m = THRUST x 0.25/12 ft (" + sourceMomentNm.ToString("F3") + " N m)",
                    report, ref passed, ref failed);

                MavAeroCoefficients routine = MavF15BaumannMach06Longitudinal.Evaluate(
                    rig.body.debugState.alphaRad, rig.actuator.ActualF15SurfaceState.channels.symmetricStabilatorDeg, rig.aero.debugQHat);
                Vector3 external = set.ExternalMomentAeroBodyNm;
                Vector3 parts = set.aerodynamic.momentAeroBodyNm + set.propulsive.momentAeroBodyNm;
                float scale = Mathf.Max(1f, set.aerodynamic.momentAeroBodyNm.magnitude);
                Record(rig.aero.debugLastCoefficients.cm == routine.cm && (external - parts).magnitude <= 1e-5f * scale
                        && set.inertialContributions == 1,
                    "the aerodynamic Cm is the routine's bit for bit - no THRUST*(0.25/12)/(QBARS*CWING) inside it - and the "
                    + "external moment is aero + thrust (the gyroscopic term is separate): the moment enters once",
                    report, ref passed, ref failed);
            }
            finally
            {
                rig.Destroy();
            }
        }

        // ================================================================= [U8] / [U9]

        private static void ValidateDemonstratedRangeNotPhysical(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[U8] The research demonstrated range never reports itself as physical");

            MavF15ResearchDemonstratedControlRange range = MavF15ResearchDemonstratedControlRange.AfitBaumannTabulatedEquilibria();
            bool notPhysical = true;
            for (int i = 0; i < 4; i++)
                notPhysical &= !range.Get((MavF15SurfaceChannel)i).IsPhysicalLimit;

            MavF15ResearchStaticSurfaceSetting setting;
            string reason;
            MavF15ResearchStaticSurfaceSetting.TryCreate(-6.0f, "validation", out setting, out reason);
            Record(notPhysical && range.symmetricStabilator.minDeg == -25f && range.symmetricStabilator.maxDeg == -5f
                    && !setting.IsPhysicalLimit && !setting.ProvidesTravel && !setting.HasRateLimit && !setting.HasGearing,
                MavF15ResearchDemonstratedControlRange.Kind + ": IsPhysicalLimit false on all 4 channels (stabilator -25..-5 "
                + "deg); the static setting reports no physical limit, no travel, no rate, no gearing",
                report, ref passed, ref failed);

            float[] admitted = { -25f, -17.30722f, -5.275754f, -5f };
            float[] rejected = { -25.001f, -4.999f, 0f, 5f, -40f, float.NaN, float.PositiveInfinity };
            int a = 0, r = 0;
            foreach (float v in admitted) if (MavF15ResearchStaticSurfaceSetting.TryCreate(v, "validation", out setting, out reason)) a++;
            foreach (float v in rejected) if (!MavF15ResearchStaticSurfaceSetting.TryCreate(v, "validation", out setting, out reason)) r++;
            bool unnamed = !MavF15ResearchStaticSurfaceSetting.TryCreate(-6f, "", out setting, out reason);
            Record(a == admitted.Length && r == rejected.Length && unnamed,
                "only stabilators inside the demonstrated range are admitted (edges included), " + r + "/" + rejected.Length
                + " outside/non-finite refused, and a setting must name its equilibrium",
                report, ref passed, ref failed);

            ResearchRig rig = ResearchRig.Build(MavF15ResearchConditionMode.SourceReproduction,
                MavF15ResearchDensityPolicy.SourceFixedDensity, MavF15ResearchGravityPolicy.SourceGravity, true);
            try
            {
                MavF15AfitResearchStateInjection.TryInitialize(rig.body, InitialState(-9.25, 13.0, 380.0, "validation"));
                MavF15SurfaceState before = rig.actuator.ActualF15SurfaceState.channels;

                MavF15RequestedSurfaceState wild = MavF15RequestedSurfaceState.From(new MavF15SurfaceState
                {
                    symmetricStabilatorDeg = 12f, differentialStabilatorDeg = -8f, aileronDeg = 20f, rudderDeg = -30f
                });
                rig.actuator.SetF15Command(wild, 1f);
                rig.actuator.StepActuator(FixedDeltaTime);
                rig.actuator.SetCommand(new MavControlInput { elevatorDeg = -20f, aileronDeg = 15f, rudderDeg = 10f });
                rig.actuator.StepActuator(FixedDeltaTime);
                MavF15SurfaceState after = rig.actuator.ActualF15SurfaceState.channels;

                Record(rig.actuator.debugResearchStaticHold && Same(before, after) && after.symmetricStabilatorDeg == -9.25f
                        && after.aileronDeg == 0f && after.differentialStabilatorDeg == 0f && after.rudderDeg == 0f,
                    "held: F-15 and shared-contract commands (stab +12, aileron 20, rudder -30 ...) do not move the held "
                    + "surfaces - no pilot-control mapping, no gearing",
                    report, ref passed, ref failed);

                Record(rig.actuator.limits.AvailableChannelCount == 0 && rig.actuator.debugAvailableChannelCount == 0
                        && !MavF15PhysicalSurfaceHardStops.FromActuatorLimits(rig.actuator.limits).AnyDeclared
                        && IsZero(rig.body.activeProfile.controlSurfaceLimits),
                    "and while held, the actuator still declares 0 travel channels, no hard stop, and the research profile's "
                    + "shared control limits stay zero: a static setting is not travel",
                    report, ref passed, ref failed);
            }
            finally
            {
                rig.Destroy();
            }
        }

        private static void ValidateNoRateAuthority(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[U9] No actuator rate authority appears");

            FieldInfo[] fields = typeof(MavF15ResearchStaticSurfaceSetting).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            string names = "";
            bool onlyTwo = fields.Length == 2;
            foreach (FieldInfo f in fields)
            {
                names += (names.Length > 0 ? ", " : "") + f.Name;
                onlyTwo &= f.Name == "symmetricStabilatorDeg" || f.Name == "sourceNote";
            }

            MethodInfo[] holdMethods = typeof(MavF15AfitResearchStaticSurfaceHold).GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            bool noStepping = true;
            foreach (MethodInfo m in holdMethods)
                noStepping &= m.Name != "FixedUpdate" && m.Name != "Update" && m.Name != "LateUpdate";

            Record(onlyTwo && noStepping,
                "the static setting carries only {" + names + "} - no rate, no min/max, no gain - and the hold has no "
                + "Update/FixedUpdate: nothing moves it over time",
                report, ref passed, ref failed);

            ResearchRig rig = ResearchRig.Build(MavF15ResearchConditionMode.SourceReproduction,
                MavF15ResearchDensityPolicy.SourceFixedDensity, MavF15ResearchGravityPolicy.SourceGravity, true);
            try
            {
                MavF15AfitResearchStateInjection.TryInitialize(rig.body, InitialState(-9.25, 13.0, 380.0, "validation"));
                MavF15ActuatorRateLimits rates = MavF15ActuatorRateLimits.FromActuatorLimits(rig.actuator.limits);
                bool noRate = !rates.AnyDeclared;
                for (int i = 0; i < 4; i++)
                    noRate &= !rig.actuator.limits.Get((MavF15SurfaceChannel)i).HasSourcedRate;

                rig.ArmStructural();
                MavF15ResearchStaticSurfaceSetting other;
                string reason;
                MavF15ResearchStaticSurfaceSetting.TryCreate(-12f, "validation", out other, out reason);
                bool engageRefused = !rig.hold.TryEngage(other, out reason) && reason.Contains("armed");
                string releaseReason;
                bool releaseRefused = !rig.hold.TryRelease(out releaseReason);
                string kinematicReason;
                bool injectRefused = !rig.body.TryApplyInitialKinematicState(Vector3.zero, Quaternion.identity,
                    Vector3.forward, Vector3.zero, out kinematicReason);
                rig.body.simulationEnabled = false;

                Record(noRate && engageRefused && releaseRefused && injectRefused
                        && rig.actuator.ActualF15SurfaceState.channels.symmetricStabilatorDeg == -9.25f,
                    "no channel has a sourced rate; once armed the setting can be neither changed nor released, and no initial "
                    + "state can be applied - the surface value is a static initial condition, never a commanded motion",
                    report, ref passed, ref failed);
            }
            finally
            {
                rig.Destroy();
            }
        }

        // ================================================================= [U10]

        private static void ValidateNoDirectRigidbodyWrites(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[U10] No direct Rigidbody writes from research components; nothing arms");

            MavOwnershipScanResult scan = MavFlightDynamicsOwnershipScan.Scan();
            Record(scan.IsClean && scan.filesScanned > 0,
                "the FDM ownership scan is clean over " + scan.filesScanned + " files, the WP-4A files included: only "
                + "MavSixDoFBody writes Rigidbody motion state"
                + (scan.IsClean ? "" : "; first violation: " + scan.violations[0]),
                report, ref passed, ref failed);

            string root = FlightDynamicsRoot();
            string offender = null;
            int lines = 0;
            foreach (string file in NewRuntimeFiles)
            {
                string path = Path.Combine(root, file);
                if (!File.Exists(path))
                {
                    offender = file + " missing";
                    continue;
                }

                string[] text = File.ReadAllLines(path);
                for (int i = 0; i < text.Length; i++)
                {
                    lines++;
                    string code = MavFlightDynamicsOwnershipScan.StripCommentsAndStringLiterals(text[i]);
                    if (MavFlightDynamicsOwnershipScan.IsOwnershipViolation(text[i])
                        || code.Contains("simulationEnabled = true") || code.Contains("ArmedForLiveFlight =")
                        || code.Contains(".useGravity =") || code.Contains(".position =") || code.Contains(".rotation ="))
                        offender = file + ":" + (i + 1) + " " + text[i].Trim();
                }
            }

            string injection = CodeOf(Path.Combine(root, "F15/MavF15AfitResearchStateInjection.cs"));
            Record(offender == null && injection != null && injection.Contains("TryApplyInitialKinematicState("),
                offender == null
                    ? "none of the " + NewRuntimeFiles.Length + " WP-4A runtime files (" + lines + " lines) writes a force, torque, "
                      + "velocity, position, rotation or useGravity, or arms; injection hands the pose to "
                      + "MavSixDoFBody.TryApplyInitialKinematicState"
                    : "VIOLATION: " + offender,
                report, ref passed, ref failed);

            ResearchRig rig = ResearchRig.Build(MavF15ResearchConditionMode.SourceReproduction,
                MavF15ResearchDensityPolicy.SourceFixedDensity, MavF15ResearchGravityPolicy.SourceGravity, true);
            try
            {
                string reason;
                Quaternion notUnit = new Quaternion(0f, 0f, 0f, 2f);
                bool nonFinite = !rig.body.TryApplyInitialKinematicState(new Vector3(float.NaN, 0f, 0f), Quaternion.identity,
                    Vector3.forward, Vector3.zero, out reason);
                bool badRotation = !rig.body.TryApplyInitialKinematicState(Vector3.zero, notUnit, Vector3.forward, Vector3.zero, out reason);

                MavFlightPhysicsOwnership gate = rig.gameObject.AddComponent<MavFlightPhysicsOwnership>();
                gate.owner = MavFlightPhysicsOwner.Shadow;
                rig.body.physicsOwnership = gate;
                bool shadowRefused = !rig.body.TryApplyInitialKinematicState(Vector3.zero, Quaternion.identity,
                    Vector3.forward, Vector3.zero, out reason) && reason.Contains("shadowing");
                gate.owner = MavFlightPhysicsOwner.Legacy;
                bool legacyRefused = !rig.body.TryApplyInitialKinematicState(Vector3.zero, Quaternion.identity,
                    Vector3.forward, Vector3.zero, out reason) && reason.Contains("ownership gate");
                Object.DestroyImmediate(gate);
                rig.body.physicsOwnership = null;

                Record(nonFinite && badRotation && shadowRefused && legacyRefused && rig.body.debugInitialStateApplications == 0,
                    "the body's initializer refuses non-finite input, a non-unit rotation, a shadowing body and a body whose "
                    + "ownership gate has not granted the replacement stack (and, [U9], an armed body)",
                    report, ref passed, ref failed);
            }
            finally
            {
                rig.Destroy();
            }
        }

        // ================================================================= [U11]

        private static void ValidateRoundTrip(List<MavF15ResearchEquilibrium> equilibria, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[U11] Initialization round-trips every WP-3B/WP-3C equilibrium (170: symmetric, turning, pitchfork)");

            ResearchRig rig = ResearchRig.Build(MavF15ResearchConditionMode.SourceReproduction,
                MavF15ResearchDensityPolicy.SourceFixedDensity, MavF15ResearchGravityPolicy.SourceGravity, true);
            try
            {
                int initialized = 0, within = 0, prepared = 0, recovered = 0, sym = 0, turn = 0, fork = 0;
                double alpha = 0, beta = 0, theta = 0, phi = 0, rate = 0, speed = 0, stab = 0;
                string firstFailure = null;
                foreach (MavF15ResearchEquilibrium eq in equilibria)
                {
                    if (!eq.recovered)
                        continue;
                    recovered++;
                    if (eq.branch == MavF15ResearchEquilibriumBranch.Symmetric) sym++;
                    else if (eq.branch == MavF15ResearchEquilibriumBranch.Turning) turn++;
                    else fork++;

                    MavF15ResearchInitializationReport r = MavF15AfitResearchStateInjection.TryInitialize(rig.body, FromEquilibrium(eq));
                    if (r.initialized) initialized++;
                    if (r.readbackWithinTolerance) within++;
                    if (r.preparation.prepared) prepared++;
                    else if (firstFailure == null) firstFailure = "point " + eq.point + ": " + r.reason + " / " + r.preparation.blockers;

                    alpha = Math.Max(alpha, r.alphaErrorRad);
                    beta = Math.Max(beta, r.betaErrorRad);
                    theta = Math.Max(theta, r.thetaErrorRad);
                    phi = Math.Max(phi, r.phiErrorRad);
                    rate = Math.Max(rate, r.rateErrorRadSec);
                    speed = Math.Max(speed, r.trueAirspeedRelativeError);
                    stab = Math.Max(stab, r.heldStabilatorErrorDeg);
                }

                report.Append("    largest readback error: alpha ").Append(alpha.ToString("E1")).Append(" rad, beta ").Append(beta.ToString("E1"))
                    .Append(", theta ").Append(theta.ToString("E1")).Append(", phi ").Append(phi.ToString("E1"))
                    .Append(" rad; p/q/r ").Append(rate.ToString("E1")).Append(" rad/s; V ").Append(speed.ToString("E1"))
                    .Append(" relative; held stabilator ").Append(stab.ToString("E1")).AppendLine(" deg");

                Record(recovered == 170 && sym == 89 && turn == 80 && fork == 1 && initialized == 170 && within == 170,
                    initialized + "/" + recovered + " equilibria (" + sym + " symmetric, " + turn + " turning, " + fork + " pitchfork) "
                    + "initialize the research body, and the body's own state builder reads back alpha, beta, p, q, r, theta, "
                    + "phi, V and the held stabilator within float round-trip tolerance ("
                    + MavF15AfitResearchStateInjection.AngleToleranceRad.ToString("E0") + " rad, "
                    + MavF15AfitResearchStateInjection.SpeedRelativeTolerance.ToString("E0") + " relative)",
                    report, ref passed, ref failed);

                Record(prepared == 170 && !rig.body.simulationEnabled && rig.body.debugLoadApplications == 0,
                    "and at every one the preparation report has no blocker - never armed, no load applied"
                    + (firstFailure == null ? "" : "; first blocker: " + firstFailure),
                    report, ref passed, ref failed);
            }
            finally
            {
                rig.Destroy();
            }
        }

        // ================================================================= [U12]

        private struct ResidualSummary
        {
            public int evaluated;
            public double maxLinearG;
            public double minLinearG;
            public double maxAngular;
            public int worstPoint;

            /// <summary>The linear residual after removing the known research-mass-convention term.</summary>
            public double maxLinearAfterMassConventionG;
        }

        /// <summary>
        /// The source's RMASS = 37000/32.174 slug, in kg. The research profile's mass (WP-1) is
        /// 37,000 lb x 0.45359237 kg - 1.52e-6 lighter - so at a source equilibrium the body's F/m
        /// exceeds the source's by that fraction of the non-gravitational acceleration. Recorded,
        /// not corrected: WP-4A does not modify mass.
        /// </summary>
        private static readonly double SourceMassKg =
            MavF15AfitResearchMassReference.WeightLb / MavF15SourceExercisedOperatingDomain.SourceGravityFtPerSec2
            * (0.45359237 * 9.80665 / 0.3048);

        private static void ValidateStaticLoadConsistency(List<MavF15ResearchEquilibrium> equilibria, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[U12] Static load consistency at the injected equilibria - the body's own loads, SHADOW (nothing applied)");
            report.AppendLine("    residual: |F/m - w x V| / G (F includes gravity) and |M_ext - w x I w| / (q S cbar); an equilibrium gives 0");

            ResidualSummary source = Residuals(equilibria, MavF15ResearchDensityPolicy.SourceFixedDensity, MavF15ResearchGravityPolicy.SourceGravity);
            ResidualSummary densityOnly = Residuals(equilibria, MavF15ResearchDensityPolicy.SourceFixedDensity, MavF15ResearchGravityPolicy.UnityProjectGravity);
            ResidualSummary standard = Residuals(equilibria, MavF15ResearchDensityPolicy.StandardAtmosphere, MavF15ResearchGravityPolicy.UnityProjectGravity);

            AppendResidual(report, "source density + source gravity", source);
            AppendResidual(report, "source density + Unity gravity ", densityOnly);
            AppendResidual(report, "standard atmosphere + Unity g  ", standard);

            Record(source.evaluated == 170 && source.maxLinearG <= SourceEnvironmentResidualBand
                    && source.maxAngular <= SourceEnvironmentResidualBand,
                "SOURCE environment: all 170 injected equilibria are equilibria of the runtime body's own loads to "
                + source.maxLinearG.ToString("E1") + " g and " + source.maxAngular.ToString("E1") + " qScbar - inside the "
                + SourceEnvironmentResidualBand.ToString("E1") + " band (10x below the smallest environment gap)",
                report, ref passed, ref failed);

            double massGap = 1.0 - (double)MavF15AfitResearchMassReference.MassKg / SourceMassKg;
            Record(source.maxLinearAfterMassConventionG <= 1e-6,
                "what remains is the research mass convention: the WP-1 mass (37,000 lb x 0.45359237 kg) is "
                + massGap.ToString("E2") + " below the source's RMASS 37000/32.174 slug; removing that term leaves "
                + source.maxLinearAfterMassConventionG.ToString("E1") + " g - float noise. Recorded, not corrected (no mass change)",
                report, ref passed, ref failed);

            Record(densityOnly.evaluated == 170 && densityOnly.minLinearG >= 3.0e-4 && densityOnly.maxLinearG <= 3.9e-4,
                "density override ONLY: every equilibrium is off by the gravity gap, " + densityOnly.minLinearG.ToString("E2")
                + ".." + densityOnly.maxLinearG.ToString("E2") + " g (Unity 9.81 vs 9.80664) - density alone is NOT equivalent",
                report, ref passed, ref failed);

            Record(standard.evaluated == 170 && standard.maxLinearG > densityOnly.maxLinearG * 1.5,
                "neither override: up to " + standard.maxLinearG.ToString("E2") + " g (ISA q 6.83e-4 low, plus gravity) - "
                + "runtime equivalence requires density + gravity",
                report, ref passed, ref failed);
        }

        private static void AppendResidual(StringBuilder report, string label, ResidualSummary s)
        {
            report.Append("    ").Append(label).Append(": linear ").Append(s.minLinearG.ToString("E2")).Append("..")
                .Append(s.maxLinearG.ToString("E2")).Append(" g, angular max ").Append(s.maxAngular.ToString("E2"))
                .Append(" (").Append(s.evaluated).Append(" evaluated; worst linear at point ").Append(s.worstPoint).AppendLine(")");
        }

        private static ResidualSummary Residuals(List<MavF15ResearchEquilibrium> equilibria,
            MavF15ResearchDensityPolicy density, MavF15ResearchGravityPolicy gravity)
        {
            ResidualSummary s = new ResidualSummary { minLinearG = double.MaxValue };
            ResearchRig rig = ResearchRig.Build(MavF15ResearchConditionMode.SourceReproduction, density, gravity, true);
            try
            {
                double g = MavF15AfitResearchRuntimeEnvironment.SourceGravityMps2;
                MavAeroReferenceGeometry geometry = MavF15BaumannMach06Reference.CreateReferenceGeometry();
                foreach (MavF15ResearchEquilibrium eq in equilibria)
                {
                    if (!eq.recovered)
                        continue;

                    MavF15ResearchInitializationReport init = MavF15AfitResearchStateInjection.TryInitialize(rig.body, FromEquilibrium(eq));
                    if (!init.initialized || !rig.ShadowStep())
                        continue;

                    MavSixDoFBody b = rig.body;
                    MavFlightDynamicsLoadSet set = b.debugLoadSet;
                    double m = rig.rigidbody.mass;

                    Vector3 f = set.AppliedForceAeroBodyN;
                    if (!set.HasGravitational)
                    {
                        f += MavSixDoFBody.ComputeGravitationalForceAeroBody(
                            rig.gameObject.transform.InverseTransformDirection(Vector3.down), rig.rigidbody.mass, Physics.gravity.magnitude);
                    }

                    Vector3 w = b.debugState.aeroBodyRatesRadSec;
                    Vector3 v = b.debugState.aeroBodyVelocityMps;
                    double cx = (double)w.y * v.z - (double)w.z * v.y;
                    double cy = (double)w.z * v.x - (double)w.x * v.z;
                    double cz = (double)w.x * v.y - (double)w.y * v.x;
                    double ax = f.x / m - cx, ay = f.y / m - cy, az = f.z / m - cz;
                    double linear = Math.Sqrt(ax * ax + ay * ay + az * az) / g;

                    // Remove the known mass-convention term (F_ng/m)(1 - m/m_source).
                    Vector3 fng = set.totalForceAeroBodyN;
                    double k = 1.0 - m / SourceMassKg;
                    double rx = ax - fng.x / m * k, ry = ay - fng.y / m * k, rz = az - fng.z / m * k;
                    s.maxLinearAfterMassConventionG = Math.Max(s.maxLinearAfterMassConventionG,
                        Math.Sqrt(rx * rx + ry * ry + rz * rz) / g);

                    double qsc = (double)b.debugState.dynamicPressurePa * geometry.wingAreaM2 * geometry.meanAerodynamicChordM;
                    double angular = set.totalMomentAeroBodyNm.magnitude / qsc;

                    s.evaluated++;
                    if (linear > s.maxLinearG)
                    {
                        s.maxLinearG = linear;
                        s.worstPoint = eq.point;
                    }
                    s.minLinearG = Math.Min(s.minLinearG, linear);
                    s.maxAngular = Math.Max(s.maxAngular, angular);
                }
            }
            finally
            {
                rig.Destroy();
            }

            return s;
        }

        // ================================================================= [U13]

        private static void ValidateDiagnostics(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[U13] Runtime-preparation diagnostics; StrictFitCondition vs SourceReproduction retained; live takeover off");

            ResearchRig rig = ResearchRig.Build(MavF15ResearchConditionMode.SourceReproduction,
                MavF15ResearchDensityPolicy.SourceFixedDensity, MavF15ResearchGravityPolicy.SourceGravity, true);
            try
            {
                MavF15ResearchRuntimeInitialState s = InitialState(-6.4927, 9.80, 533.37, "Baumann Table VII point 124 (validation)");
                s.altitudeM = 7000f;
                MavF15ResearchInitializationReport init = MavF15AfitResearchStateInjection.TryInitialize(rig.body, s);
                MavF15ResearchRuntimePreparationReport p = init.preparation;
                report.AppendLine("    preparation report at V 533.37 ft/s, geometric 7,000 m, SourceReproduction:");
                foreach (string raw in p.ToMultilineString().Split('\n'))
                {
                    string line = raw.TrimEnd('\r');
                    if (line.Length > 0)
                        report.Append("      ").AppendLine(line);
                }

                Record(p.prepared && p.authorityGranted && p.configurationId == MavF15AfitResearchIdentity.ConfigurationId
                        && p.sourceEquivalentEnvironment && !p.armed
                        && p.densitySource.StartsWith("ResearchSourceFixedDensity") && p.densityKgM3 == MavF15AfitResearchRuntimeEnvironment.SourceDensityKgM3
                        && p.gravitySource.StartsWith("ResearchSourceGravityThroughLoadSet") && p.gravityMps2 == MavF15AfitResearchRuntimeEnvironment.SourceGravityMps2
                        && p.aeroModel.Contains(MavF15BaumannMach06Reference.ModelId) && p.thrustSemantic.Contains("TOTAL")
                        && p.surfaceSemantic.Contains("STATIC") && p.surfaceSemantic.Contains("no travel")
                        && p.sourceFitCondition.Contains("M 0.6"),
                    "the report names the configuration, the density and gravity sources, the aero model, the thrust and "
                    + "surface semantics and the fit condition; prepared, source-equivalent, not armed",
                    report, ref passed, ref failed);

                Record(p.outsideFitCondition && p.insideSourceExercisedSpeedSpan && p.conditionGateAdmits
                        && Math.Abs(p.trueAirspeedFtPerSec - 533.37f) < 0.01f && p.altitudeSemantic.Contains("NOT a source state"),
                    "V 533.37 ft/s is OUTSIDE the M 0.6 fit condition and INSIDE the source-exercised span, and that alone does "
                    + "not block SourceReproduction; the 7,000-m altitude is reported as not a source state",
                    report, ref passed, ref failed);

                rig.profile.conditionMode = MavF15ResearchConditionMode.StrictFitCondition;
                MavF15ResearchRuntimePreparationReport strict = MavF15AfitResearchRuntimePreparation.Evaluate(rig.body);
                rig.profile.conditionMode = MavF15ResearchConditionMode.SourceReproduction;

                rig.rigidbody.linearVelocity = rig.rigidbody.linearVelocity.normalized * (720f * 0.3048f);
                MavF15ResearchRuntimePreparationReport fast = MavF15AfitResearchRuntimePreparation.Evaluate(rig.body);

                Record(!strict.prepared && !strict.conditionGateAdmits && strict.blockers.Contains("condition gate")
                        && !fast.insideSourceExercisedSpeedSpan && !fast.conditionGateAdmits && !fast.prepared,
                    "the same state under StrictFitCondition is blocked by the gate (M 0.6 only), and 720 ft/s is outside the "
                    + "source-exercised span in either mode - the existing distinction is kept, not widened",
                    report, ref passed, ref failed);
            }
            finally
            {
                rig.Destroy();
            }

            rig = ResearchRig.Build(MavF15ResearchConditionMode.SourceReproduction,
                MavF15ResearchDensityPolicy.StandardAtmosphere, MavF15ResearchGravityPolicy.UnityProjectGravity, true);
            try
            {
                MavF15ResearchInitializationReport init = MavF15AfitResearchStateInjection.TryInitialize(
                    rig.body, InitialState(-8.0, 12.0, 450.0, "validation"));
                MavF15ResearchRuntimePreparationReport p = init.preparation;
                rig.aero.sourceMode = MavF15AeroSourceMode.BaumannMach06LongitudinalResearch;
                MavF15ResearchRuntimePreparationReport longitudinal = MavF15AfitResearchRuntimePreparation.Evaluate(rig.body);

                Record(p.prepared && !p.sourceEquivalentEnvironment && p.equivalenceGaps.Contains("density")
                        && p.equivalenceGaps.Contains("gravity") && !longitudinal.prepared,
                    "with the default policies the report says the environment is NOT source-equivalent and why ("
                    + p.equivalenceGaps + "); a longitudinal-only aero model is a blocker",
                    report, ref passed, ref failed);
            }
            finally
            {
                rig.Destroy();
            }

            GameObject host = NewHost("MavWp4aDefaults");
            try
            {
                MavSixDoFBody body = host.AddComponent<MavSixDoFBody>();
                MavF15AfitResearchFlightDynamicsProfile profile = host.AddComponent<MavF15AfitResearchFlightDynamicsProfile>();
                Record(!body.simulationEnabled && profile.densityPolicy == MavF15ResearchDensityPolicy.StandardAtmosphere
                        && profile.gravityPolicy == MavF15ResearchGravityPolicy.UnityProjectGravity
                        && profile.conditionMode == MavF15ResearchConditionMode.StrictFitCondition
                        && host.GetComponent<MavF15AfitResearchStaticSurfaceHold>() == null,
                    "live takeover OFF by default: a new body is unarmed, the research profile's environment policies are off, "
                    + "its condition mode is strict, and no static hold exists unless added; no WP-4A file arms anything ([U10])",
                    report, ref passed, ref failed);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        // ================================================================= [U14]

        private static void ValidateUnchanged(List<MavF15ResearchEquilibrium> equilibria, MavAtmosphereSample[] atmosphereBefore,
            Vector3 gravityBefore, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[U14] WP-3A/B/C/D/E datasets and protected sources unchanged");

            string root = MavF15ResearchTimeDomainStability.ProjectRoot();
            int same = 0;
            string changed = null;
            foreach (string[] entry in ProtectedFiles)
            {
                string hash = root == null ? null : HashLf(Path.Combine(root, entry[0]));
                if (hash == entry[1]) same++;
                else if (changed == null) changed = entry[0] + " (" + (hash ?? "missing") + ")";
            }

            Record(root != null && same == ProtectedFiles.Length,
                same + "/" + ProtectedFiles.Length + " byte-identical (SHA-256, LF-normalized): Table VII transcription and data, "
                + "the 5 WP-3D and 7 WP-3E CSVs, the source RHS, trim solver, both coefficient routines, the atmosphere model, the "
                + "research condition gate, fixed thrust, aero model, control authority, mass, the exact NASA 836 sources and every "
                + "MavF100 source" + (changed == null ? "" : "; FIRST CHANGED: " + changed),
                report, ref passed, ref failed);

            string csv = root == null ? null : Path.Combine(root, "Docs/Reference/Data/F15/stability/f15_research_stability_table_vii.csv");
            int matched = 0, compared = 0;
            string mismatch = null;
            if (csv != null && File.Exists(csv))
            {
                string[] lines = File.ReadAllLines(csv);
                List<string> firstRows = new List<string>();
                string lastKey = null;
                for (int i = 1; i < lines.Length; i++)
                {
                    string[] cols = lines[i].Split(',');
                    if (cols.Length < 11) continue;
                    string key = cols[0] + "," + cols[1];
                    if (key == lastKey) continue;
                    lastKey = key;
                    firstRows.Add(lines[i]);
                }

                CultureInfo c = CultureInfo.InvariantCulture;
                for (int i = 0; i < Math.Min(firstRows.Count, equilibria.Count); i++)
                {
                    MavF15ResearchEquilibrium eq = equilibria[i];
                    MavF15ResearchSourceState x = eq.state;
                    string state = eq.stabilatorDeg.ToString("R", c) + "," + x.trueAirspeedFtPerSec.ToString("R", c) + ","
                        + (x.alphaRad * RadToDeg).ToString("R", c) + "," + (x.betaRad * RadToDeg).ToString("R", c) + ","
                        + x.pRadSec.ToString("R", c) + "," + x.qRadSec.ToString("R", c) + "," + x.rRadSec.ToString("R", c) + ","
                        + (x.thetaRad * RadToDeg).ToString("R", c) + "," + (x.phiRad * RadToDeg).ToString("R", c);
                    string[] cols = firstRows[i].Split(',');
                    string stored = string.Join(",", cols, 2, 9);
                    compared++;
                    if (cols[0] == eq.point.ToString(c) && stored == state) matched++;
                    else if (mismatch == null) mismatch = "row " + i + " point " + cols[0];
                }
            }

            Record(compared == 170 && matched == 170,
                "re-solving all 170 WP-3B/WP-3C equilibria reproduces the states stored in the WP-3D dataset exactly ("
                + matched + "/" + compared + ", 'R' round-trip strings)" + (mismatch == null ? "" : "; first mismatch " + mismatch),
                report, ref passed, ref failed);

            MavAtmosphereSample[] after = SampleAtmosphere();
            bool atmosphere = after.Length == atmosphereBefore.Length;
            for (int i = 0; atmosphere && i < after.Length; i++)
                atmosphere = Same(after[i], atmosphereBefore[i]);

            Record(atmosphere && Physics.gravity == gravityBefore,
                "MavAtmosphereModel returns bit-identical samples before and after the whole suite, and Physics.gravity is unchanged",
                report, ref passed, ref failed);
        }

        // ================================================================= rig

        private sealed class ResearchRig
        {
            public GameObject gameObject;
            public MavSixDoFBody body;
            public Rigidbody rigidbody;
            public MavF15AfitResearchFlightDynamicsProfile profile;
            public MavF15AeroModel aero;
            public MavF15ControlActuator actuator;
            public MavManualPilotCommandSource command;
            public MavF15ControlLaw law;
            public MavF15AfitResearchFixedThrust thrust;
            public MavF15AfitResearchStaticSurfaceHold hold;
            private float time;

            public static ResearchRig Build(MavF15ResearchConditionMode mode, MavF15ResearchDensityPolicy density,
                MavF15ResearchGravityPolicy gravity, bool withHold)
            {
                ResearchRig rig = new ResearchRig();
                rig.gameObject = NewHost("MavWp4aResearchRig");

                rig.body = rig.gameObject.AddComponent<MavSixDoFBody>();
                rig.rigidbody = rig.gameObject.GetComponent<Rigidbody>();

                rig.profile = rig.gameObject.AddComponent<MavF15AfitResearchFlightDynamicsProfile>();
                rig.profile.conditionMode = mode;
                rig.profile.densityPolicy = density;
                rig.profile.gravityPolicy = gravity;

                rig.aero = rig.gameObject.AddComponent<MavF15AeroModel>();
                rig.aero.sourceMode = MavF15AeroSourceMode.BaumannMach06SixAxisResearch;
                rig.aero.allowCrossValidationResearchModel = true;

                rig.actuator = rig.gameObject.AddComponent<MavF15ControlActuator>();
                rig.command = rig.gameObject.AddComponent<MavManualPilotCommandSource>();
                rig.law = rig.gameObject.AddComponent<MavF15ControlLaw>();
                rig.law.mode = MavF15FcsMode.AFITResearch;
                rig.thrust = rig.gameObject.AddComponent<MavF15AfitResearchFixedThrust>();

                if (withHold)
                {
                    rig.hold = rig.gameObject.AddComponent<MavF15AfitResearchStaticSurfaceHold>();
                    rig.hold.sixDoFBody = rig.body;
                }

                rig.body.profileProvider = rig.profile;
                rig.body.aerodynamicModel = rig.aero;
                rig.body.controlSurfaceActuator = rig.actuator;
                rig.body.controlLaw = rig.law;
                rig.body.pilotCommandSource = rig.command;
                rig.body.propulsionModel = rig.thrust;
                rig.body.simulationEnabled = false;

                rig.actuator.sixDoFBody = rig.body;
                rig.law.sixDoFBody = rig.body;
                rig.law.actuator = rig.actuator;
                rig.law.commandSource = rig.command;
                rig.law.driveActuatorInFixedUpdate = true;
                rig.command.command = MavPilotCommand.Neutral;

                // Awake/OnEnable do not run in edit mode; do explicitly what they would have.
                rig.body.ApplyConfiguredProfile(true);
                rig.body.NotifyOwnershipChanged();
                return rig;
            }

            public void SetFitCondition(float altitudeM)
            {
                MavAtmosphereSample atmosphere = MavAtmosphereModel.Sample(MavF15BaumannMach06Reference.SourcePressureAltitudeM);
                float speed = MavF15BaumannMach06Reference.SourceMach * atmosphere.speedOfSoundMps;
                gameObject.transform.position = new Vector3(0f, altitudeM, 0f);
                gameObject.transform.rotation = Quaternion.identity;
                rigidbody.linearVelocity = new Vector3(0f, 0f, speed);
                rigidbody.angularVelocity = Vector3.zero;
            }

            /// <summary>The body's sanctioned structural-only override on this hidden rig; see MavF15ResearchPipelineValidation.</summary>
            public void ArmStructural()
            {
                body.simulationEnabled = true;
                body.allowStructuralOnlyLoadApplication = true;
            }

            public void StepLive()
            {
                time += FixedDeltaTime;
                law.StepControlLaw(FixedDeltaTime);
                actuator.StepActuator(FixedDeltaTime);
                body.StepPhysicsForValidation(FixedDeltaTime, time);
            }

            /// <summary>
            /// One SHADOW step: a temporary ownership gate in Shadow makes the body compute its whole
            /// pipeline - environment, aero, thrust, gravity, gyroscopic term - and apply NOTHING.
            /// Returns whether the shadow load set was computed and finite.
            /// </summary>
            public bool ShadowStep()
            {
                MavFlightPhysicsOwnership gate = gameObject.AddComponent<MavFlightPhysicsOwnership>();
                gate.owner = MavFlightPhysicsOwner.Shadow;
                body.physicsOwnership = gate;
                body.NotifyOwnershipChanged();

                int shadowSteps = body.debugShadowComputeSteps;
                time += FixedDeltaTime;
                law.StepControlLaw(FixedDeltaTime);
                actuator.StepActuator(FixedDeltaTime);
                body.StepPhysicsForValidation(FixedDeltaTime, time);
                bool computed = body.debugShadowComputeSteps == shadowSteps + 1 && body.debugShadowLoadSetFinite;

                Object.DestroyImmediate(gate);
                body.physicsOwnership = null;
                body.NotifyOwnershipChanged();
                return computed;
            }

            public void Destroy()
            {
                if (gameObject != null)
                    Object.DestroyImmediate(gameObject);
                gameObject = null;
            }
        }

        // ================================================================= helpers

        private static GameObject NewHost(string name)
        {
            GameObject go = new GameObject(name);
            go.hideFlags = HideFlags.HideAndDontSave;
            return go;
        }

        private static MavF15ResearchRuntimeInitialState InitialState(double stabilatorDeg, double alphaDeg, double speedFtPerSec, string note)
        {
            return new MavF15ResearchRuntimeInitialState
            {
                alphaRad = alphaDeg / RadToDeg,
                thetaRad = alphaDeg / RadToDeg,
                trueAirspeedFtPerSec = speedFtPerSec,
                symmetricStabilatorDeg = stabilatorDeg,
                altitudeM = MavF15ResearchRuntimeInitialState.DefaultAltitudeM,
                sourceNote = note
            };
        }

        private static MavF15ResearchRuntimeInitialState FromEquilibrium(MavF15ResearchEquilibrium eq)
        {
            MavF15ResearchSourceState x = eq.state;
            return new MavF15ResearchRuntimeInitialState
            {
                alphaRad = x.alphaRad,
                betaRad = x.betaRad,
                pRadSec = x.pRadSec,
                qRadSec = x.qRadSec,
                rRadSec = x.rRadSec,
                thetaRad = x.thetaRad,
                phiRad = x.phiRad,
                trueAirspeedFtPerSec = x.trueAirspeedFtPerSec,
                symmetricStabilatorDeg = eq.stabilatorDeg,
                headingRad = 0.0,
                altitudeM = MavF15ResearchRuntimeInitialState.DefaultAltitudeM,
                sourceNote = "Baumann Table VII point " + eq.point + " (" + eq.branch + ", WP-3B/WP-3C solve)"
            };
        }

        private static MavAtmosphereSample[] SampleAtmosphere()
        {
            MavAtmosphereSample[] s = new MavAtmosphereSample[AtmosphereProbeAltitudesM.Length];
            for (int i = 0; i < s.Length; i++)
                s[i] = MavAtmosphereModel.Sample(AtmosphereProbeAltitudesM[i]);
            return s;
        }

        private static bool Same(MavAtmosphereSample a, MavAtmosphereSample b)
        {
            return a.altitudeM == b.altitudeM && a.temperatureK == b.temperatureK && a.pressurePa == b.pressurePa
                && a.densityKgM3 == b.densityKgM3 && a.speedOfSoundMps == b.speedOfSoundMps;
        }

        private static bool Same(MavF15SurfaceState a, MavF15SurfaceState b)
        {
            return a.symmetricStabilatorDeg == b.symmetricStabilatorDeg && a.differentialStabilatorDeg == b.differentialStabilatorDeg
                && a.aileronDeg == b.aileronDeg && a.rudderDeg == b.rudderDeg;
        }

        private static bool SameBits(Vector3 a, Vector3 b)
        {
            return BitConverter.ToInt32(BitConverter.GetBytes(a.x), 0) == BitConverter.ToInt32(BitConverter.GetBytes(b.x), 0)
                && BitConverter.ToInt32(BitConverter.GetBytes(a.y), 0) == BitConverter.ToInt32(BitConverter.GetBytes(b.y), 0)
                && BitConverter.ToInt32(BitConverter.GetBytes(a.z), 0) == BitConverter.ToInt32(BitConverter.GetBytes(b.z), 0);
        }

        private static bool IsZero(MavControlSurfaceLimits l)
        {
            return l.elevatorMinDeg == 0f && l.elevatorMaxDeg == 0f && l.aileronMinDeg == 0f && l.aileronMaxDeg == 0f
                && l.rudderMinDeg == 0f && l.rudderMaxDeg == 0f && l.leadingEdgeFlapMinDeg == 0f && l.leadingEdgeFlapMaxDeg == 0f;
        }

        private static string FlightDynamicsRoot()
        {
            string root = MavF15ResearchTimeDomainStability.ProjectRoot();
            return root == null ? null : Path.Combine(root, Path.Combine("Assets", MavFlightDynamicsOwnershipScan.FlightDynamicsRelativePath));
        }

        private static string CodeOf(string path)
        {
            if (path == null || !File.Exists(path))
                return null;

            StringBuilder code = new StringBuilder();
            foreach (string line in File.ReadAllLines(path))
                code.AppendLine(MavFlightDynamicsOwnershipScan.StripCommentsAndStringLiterals(line));
            return code.ToString();
        }

        private static string HashLf(string path)
        {
            if (!File.Exists(path))
                return null;
            byte[] raw = File.ReadAllBytes(path);
            List<byte> normalized = new List<byte>(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                if (raw[i] == 13 && i + 1 < raw.Length && raw[i + 1] == 10)
                    continue;
                normalized.Add(raw[i]);
            }

            using (SHA256 sha = SHA256.Create())
            {
                byte[] h = sha.ComputeHash(normalized.ToArray());
                StringBuilder s = new StringBuilder(64);
                foreach (byte b in h)
                    s.Append(b.ToString("x2"));
                return s.ToString();
            }
        }

        private static void Record(bool condition, string label, StringBuilder report, ref int passed, ref int failed)
        {
            if (condition) passed++; else failed++;
            report.Append(condition ? "  PASS  " : "  FAIL  ").AppendLine(label);
        }
    }
}
#endif
