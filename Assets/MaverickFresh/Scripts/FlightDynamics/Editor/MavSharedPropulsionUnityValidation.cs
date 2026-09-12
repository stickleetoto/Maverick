using System.Text;
using UnityEditor;
using UnityEngine;
using MaverickFresh.FlightDynamics;
using MaverickFresh.FlightDynamics.F15;
using MaverickFresh.FlightDynamics.F16;
using MaverickFresh.FlightDynamics.Validation;

namespace MaverickFresh.FlightDynamics.EditorTools
{
    /// <summary>
    /// Phase P0.2: the shared-propulsion checks that only the real Unity editor can answer.
    ///
    /// The offline harness compiles production sources against a hand-written UnityEngine stub. That
    /// exercises the logic but cannot speak to anything the engine itself owns, and this suite is
    /// exactly the difference:
    ///
    ///   - real ScriptableObject creation, reference identity and destruction
    ///   - the actual assembly split, and whether the runtime assembly compiled without UnityEditor
    ///   - Unity's own domain-reload behaviour for the static engine-law registry
    ///   - GC allocation per FixedUpdate, measured with the real GC
    ///   - real Rigidbody ownership: that propulsion reaches the body exactly once, through SixDoF
    ///
    /// Everything here is created and destroyed in memory. No asset is written, no scene is modified,
    /// no prefab is touched. Every synthetic number is labelled SYNTHETIC_VALIDATION_ONLY.
    ///
    /// Runnable from the menu, or headless via
    /// -executeMethod MaverickFresh.FlightDynamics.EditorTools.MavSharedPropulsionUnityValidation.RunBatch
    /// </summary>
    public static class MavSharedPropulsionUnityValidation
    {
        private const string SyntheticTag = "SYNTHETIC_VALIDATION_ONLY";
        private const float SynthLateralOffsetM = 3f;
        private const float SynthThrustN = 50000f;

        private static int passed;
        private static int failed;
        private static StringBuilder report;

        [MenuItem("Maverick/Flight Dynamics/Run Unity P0.2 Propulsion Validation")]
        public static void RunFromMenu()
        {
            string text = Run();
            if (failed == 0)
                Debug.Log(text);
            else
                Debug.LogError(text);

            EditorUtility.DisplayDialog(
                "Unity P0.2 Propulsion Validation",
                failed == 0
                    ? "PASS\n\n" + passed + " checks passed."
                    : "FAIL\n\n" + failed + " checks failed. See Console.",
                "OK");
        }

        /// <summary>Headless entry point for -batchmode -executeMethod.</summary>
        public static void RunBatch()
        {
            string text = Run();
            Debug.Log(text);

            // Also write to stdout so a batchmode log is readable without parsing Unity's format.
            System.Console.WriteLine(text);

            if (failed != 0)
                EditorApplication.Exit(1);
        }

        public static string Run()
        {
            passed = 0;
            failed = 0;
            report = new StringBuilder(16384);
            report.AppendLine("Maverick Shared Propulsion - UNITY P0.2 Validation");
            report.AppendLine("==================================================");
            report.AppendLine("Unity " + Application.unityVersion);
            report.AppendLine("All synthetic values below are " + SyntheticTag);

            U001_ScriptableObjectProfile();
            U002_SharedProfileTwoRuntimes();
            U003_RegistrationLifecycle();
            U004_IndependentCommandsInUnity();
            U005_LinkedFallbackInUnity();
            U006_CommandAuthorityDiagnostic();
            U007_AsymmetricThrustInUnity();
            U008_RigidbodyOwnership();
            U009_AllocationPerStep();
            U010_ProvenanceVersusOperation();
            U011_F16Regression();
            U012_MissingProfileFailsSafe();

            report.AppendLine();
            report.Append("RESULT: ")
                .Append(failed == 0 ? "PASS" : "FAIL")
                .Append(" passed=").Append(passed)
                .Append(" failed=").Append(failed);

            return report.ToString();
        }

        // ================================================================== U-001

        private static void U001_ScriptableObjectProfile()
        {
            Section("[U-001] MavEngineProfile is a real Unity ScriptableObject");

            MavEngineProfile p = MavEngineProfile.CreateInMemory("u001-" + SyntheticTag);

            Check(p != null, "U-001", "CreateInMemory returns an instance under Unity's own CreateInstance");
            Check(p is ScriptableObject, "U-001b", "and it IS a ScriptableObject");
            Check(p is Object, "U-001c", "so it is a UnityEngine.Object with reference semantics");

            // Unity's fake-null: a destroyed Object compares == null while the C# reference lives.
            MavEngineProfile doomed = MavEngineProfile.CreateInMemory("u001-doomed-" + SyntheticTag);
            Object.DestroyImmediate(doomed);
            Check(doomed == null, "U-001d",
                "a destroyed profile compares == null through Unity's Object semantics, which a plain "
                + "C# class could not do - so a dangling profile reference is detectable");

            // A ScriptableObject must serialize its fields. Round-trip through Unity's own serializer.
            p.engineVariantIdentity = SyntheticTag + " variant";
            p.provenance = MavEngineDataProvenance.CrossValidationOnly;
            p.sourceEnvelope.declared = true;
            p.sourceEnvelope.maxMach = 0.6f;

            string json = EditorJsonUtility.ToJson(p);
            MavEngineProfile clone = MavEngineProfile.CreateInMemory("u001-clone-" + SyntheticTag);
            EditorJsonUtility.FromJsonOverwrite(json, clone);

            Check(clone.engineVariantIdentity == p.engineVariantIdentity, "U-001e",
                "string fields survive Unity serialization");
            Check(clone.provenance == MavEngineDataProvenance.CrossValidationOnly, "U-001f",
                "the provenance enum survives as " + clone.provenance
                + " - explicit numeric enum values make this stable against reordering");
            Check(clone.sourceEnvelope.declared
                  && Mathf.Abs(clone.sourceEnvelope.maxMach - 0.6f) < 1e-5f, "U-001g",
                "and the nested [Serializable] struct survives");

            Object.DestroyImmediate(clone);
            Object.DestroyImmediate(p);
        }

        // ================================================================== U-002

        private static void U002_SharedProfileTwoRuntimes()
        {
            Section("[U-002] one profile asset, two installations, two independent runtimes");

            MavF16EngineLawRegistrar.EnsureRegistered();
            MavEngineProfile shared = MakeProfile(
                "u002-shared-" + SyntheticTag, null,
                MavEnginePowerDynamicsLaw.F16GarzaMorelliPowerState);

            GameObject go = new GameObject("u002-" + SyntheticTag);
            MavPropulsionSystem sys = go.AddComponent<MavPropulsionSystem>();
            sys.installation = MakeTwin(shared);
            string err;
            Check(sys.Build(true, out err), "U-002", "twin installation builds: " + err);

            Check(ReferenceEquals(sys.installation.engines[0].engineProfile,
                                  sys.installation.engines[1].engineProfile), "U-002b",
                "both slots hold the SAME profile object by reference - the ENG-013 property");
            Check(sys.RuntimeCount == 2
                  && !ReferenceEquals(sys.GetRuntime(0), sys.GetRuntime(1)), "U-002c",
                "yet there are two DISTINCT runtimes");

            // No mutable engine state may exist on the profile. Checked through Unity's own
            // serialization view, which is the definitive list of what the asset persists.
            SerializedObject so = new SerializedObject(shared);
            SerializedProperty it = so.GetIterator();
            int stateLike = 0;
            StringBuilder names = new StringBuilder();

            // EXACT names, not substrings. An earlier version banned the substring "power" and
            // flagged powerDynamicsLaw and powerDynamicsProvenance - which select a law and record
            // where it came from. Both are configuration; neither is mutable engine state. A fuzzy
            // match that cannot tell "which law" from "how much power right now" is not a check.
            string[] bannedExact =
            {
                "actualPowerPercent", "commandedPowerPercent", "powerState01", "powerStatePercent",
                "spoolState", "lastThrustN", "lastResult", "isRunning", "hasFailed", "failed",
                "running", "cutoff"
            };
            while (it.NextVisible(true))
            {
                for (int i = 0; i < bannedExact.Length; i++)
                {
                    if (string.Equals(it.name, bannedExact[i], System.StringComparison.Ordinal))
                    {
                        stateLike++;
                        names.Append(' ').Append(it.name);
                    }
                }
            }
            Check(stateLike == 0, "U-002d",
                "and Unity's serialized view of the asset contains NO mutable engine-state field ("
                + stateLike + ")" + names
                + " - an asset is shared by every referencing aircraft, so state there would be "
                + "global rather than per-aircraft");

            // The scan must be able to find something, or "0 hits" means nothing. MavEngineRuntime
            // is where the state legitimately lives, so its fields are the positive control.
            int runtimeStateFields = 0;
            System.Reflection.FieldInfo[] rtFields = typeof(MavEngineRuntime).GetFields(
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            for (int i = 0; i < rtFields.Length; i++)
            {
                for (int k = 0; k < bannedExact.Length; k++)
                {
                    if (string.Equals(rtFields[i].Name, bannedExact[k], System.StringComparison.Ordinal))
                        runtimeStateFields++;
                }
            }
            Check(runtimeStateFields > 0, "U-002e",
                "while the same name list DOES find " + runtimeStateFields
                + " state field(s) on MavEngineRuntime - so the zero above is a real absence, not a "
                + "scan that matches nothing");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(shared);
        }

        // ================================================================== U-003

        private static void U003_RegistrationLifecycle()
        {
            Section("[U-003] engine-law registration under the real editor");

            // This method runs after a domain reload (the editor recompiled to get here), so the
            // registry state observed now is the post-reload state.
            Check(MavF16EngineLawRegistrar.IsRegistered, "U-003",
                "the F-16 law IS registered in a freshly reloaded domain, without entering play mode "
                + "- this is route 2, the Editor-assembly InitializeOnLoadMethod, actually firing");

            IMavEnginePowerDynamics first = MavEnginePowerDynamicsFactory.Resolve(
                MavEnginePowerDynamicsLaw.F16GarzaMorelliPowerState);
            Check(ReferenceEquals(first, MavF16GarzaMorelliEngineDynamics.Instance), "U-003b",
                "and it resolves to the canonical singleton");

            int before = MavEnginePowerDynamicsFactory.F16RegistrationCount;
            MavF16EngineLawRegistrar.EnsureRegistered();
            MavF16EngineLawRegistrar.RegisterOnGameStart();
            Check(MavEnginePowerDynamicsFactory.F16RegistrationCount == before, "U-003c",
                "calling both hooks again does not accumulate registrations (count "
                + MavEnginePowerDynamicsFactory.F16RegistrationCount + ")");

            // Fail closed, then self-heal.
            MavEnginePowerDynamicsFactory.RegisterF16GarzaMorelli(null);
            bool clearedReportsFalse = !MavF16EngineLawRegistrar.IsRegistered;
            bool resolveNull = MavEnginePowerDynamicsFactory.Resolve(
                MavEnginePowerDynamicsLaw.F16GarzaMorelliPowerState) == null;
            MavF16EngineLawRegistrar.EnsureRegistered();

            Check(clearedReportsFalse && resolveNull, "U-003d",
                "clearing the registration makes IsRegistered false and Resolve null in the real "
                + "editor, not just in the harness");
            Check(MavF16EngineLawRegistrar.IsRegistered, "U-003e",
                "and EnsureRegistered self-heals afterwards");

            // The runtime hook must be a UnityEngine attribute, or a player build has no registration.
            System.Reflection.MethodInfo hook = typeof(MavF16EngineLawRegistrar).GetMethod(
                "RegisterOnGameStart",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            bool hasRuntimeAttr = hook != null
                && hook.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false)
                       .Length > 0;
            Check(hasRuntimeAttr, "U-003f",
                "the player-build path carries [RuntimeInitializeOnLoadMethod] - a UnityEngine "
                + "attribute, so it ships");

            // And the runtime assembly must not reference UnityEditor at all.
            System.Reflection.Assembly runtimeAsm = typeof(MavPropulsionSystem).Assembly;
            bool referencesEditor = false;
            System.Reflection.AssemblyName[] refs = runtimeAsm.GetReferencedAssemblies();
            for (int i = 0; i < refs.Length; i++)
            {
                if (refs[i].Name != null && refs[i].Name.StartsWith("UnityEditor"))
                    referencesEditor = true;
            }
            Check(!referencesEditor, "U-003g",
                "and the runtime assembly '" + runtimeAsm.GetName().Name
                + "' references no UnityEditor assembly - checked against the COMPILED assembly, not "
                + "the source text");
            Check(typeof(MavSharedPropulsionUnityValidation).Assembly != runtimeAsm, "U-003h",
                "while this suite lives in a different (editor) assembly, confirming the split is "
                + "real and not just a folder name");
        }

        // ================================================================== U-004

        private static void U004_IndependentCommandsInUnity()
        {
            Section("[U-004] independent left/right commands through the public API, in Unity");

            MavF16EngineLawRegistrar.EnsureRegistered();
            MavEngineProfile shared = MakeProfile(
                "u004-shared-" + SyntheticTag, null,
                MavEnginePowerDynamicsLaw.F16GarzaMorelliPowerState);

            GameObject go = new GameObject("u004-" + SyntheticTag);
            MavPropulsionSystem sys = go.AddComponent<MavPropulsionSystem>();
            sys.installation = MakeTwin(shared);
            string err;
            sys.Build(true, out err);

            MavScriptedPropulsionCommandSource src =
                go.AddComponent<MavScriptedPropulsionCommandSource>();
            src.Resize(2);
            sys.commandSource = src;

            src.SetEngineThrottle(0, 0f);
            src.SetEngineThrottle(1, 0f);
            sys.ResetEngineState(0f);

            src.SetEngineThrottle(0, 1.0f);
            src.SetEngineThrottle(1, 0.25f);

            // Pipeline scalar deliberately 0.5, matching neither engine.
            for (int i = 0; i < 900; i++)
                sys.Evaluate(LevelState(), Atmosphere(), 0.5f, 0.02f);

            float left = sys.GetRuntime(0).ActualPowerPercent;
            float right = sys.GetRuntime(1).ActualPowerPercent;

            Check(Mathf.Abs(left - 100f) < 0.05f, "U-004",
                "left follows its own command: 217.38*1.00 - 117.38 = 100.000, got " + F(left));
            Check(Mathf.Abs(right - 16.235f) < 0.05f, "U-004b",
                "right follows its own: 64.94*0.25 = 16.235, got " + F(right));
            Check(Mathf.Abs(left - 32.47f) > 1f && Mathf.Abs(right - 32.47f) > 1f, "U-004c",
                "and neither landed on the 32.47 the 0.5 scalar would have produced");
            Check(sys.commandSource is MavPropulsionCommandSourceBase, "U-004d",
                "delivered through a real component seam: " + sys.commandSource.CommandSourceName);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(shared);
        }

        // ================================================================== U-005

        private static void U005_LinkedFallbackInUnity()
        {
            Section("[U-005] linked scalar fallback drives all engines deterministically");

            MavF16EngineLawRegistrar.EnsureRegistered();
            MavEngineProfile shared = MakeProfile(
                "u005-shared-" + SyntheticTag, null,
                MavEnginePowerDynamicsLaw.F16GarzaMorelliPowerState);

            GameObject go = new GameObject("u005-" + SyntheticTag);
            MavPropulsionSystem sys = go.AddComponent<MavPropulsionSystem>();
            sys.installation = MakeTwin(shared);
            sys.autoResolveCommandSource = false;
            string err;
            sys.Build(true, out err);

            sys.ResetEngineState(0f);
            for (int i = 0; i < 900; i++)
                sys.Evaluate(LevelState(), Atmosphere(), 0.9f, 0.02f);

            float a = sys.GetRuntime(0).ActualPowerPercent;
            float b = sys.GetRuntime(1).ActualPowerPercent;

            Check(Mathf.Abs(a - 78.262f) < 0.05f && Mathf.Abs(b - 78.262f) < 0.05f, "U-005",
                "with no source both engines reach 217.38*0.9 - 117.38 = 78.262: " + F(a) + " / " + F(b));
            Check(Mathf.Abs(a - b) < 1e-4f, "U-005b",
                "identically, to 1e-4 (difference " + F(Mathf.Abs(a - b)) + ")");

            // Now attach a source, then remove it, and confirm the revert.
            MavScriptedPropulsionCommandSource src =
                go.AddComponent<MavScriptedPropulsionCommandSource>();
            src.Resize(2);
            sys.commandSource = src;
            src.SetEngineThrottle(0, 1f);
            src.SetEngineThrottle(1, 0f);
            sys.Evaluate(LevelState(), Atmosphere(), 0.9f, 0.02f);
            bool tookAuthority = sys.debugCommandAuthority == "PER_ENGINE_SOURCE";

            Object.DestroyImmediate(src);
            sys.commandSource = null;
            sys.Evaluate(LevelState(), Atmosphere(), 0.9f, 0.02f);

            Check(tookAuthority, "U-005c", "attaching a source takes per-engine authority");
            Check(sys.debugCommandAuthority == "LINKED_SCALAR", "U-005d",
                "and DESTROYING the source component reverts to LINKED_SCALAR rather than leaving a "
                + "stale asymmetric command applied: " + sys.debugCommandAuthority);
            Check(Mathf.Abs(sys.Command.ThrottleForEngine(0) - 0.9f) < 1e-4f
                  && Mathf.Abs(sys.Command.ThrottleForEngine(1) - 0.9f) < 1e-4f, "U-005e",
                "with both engines back on the scalar throttle");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(shared);
        }

        // ================================================================== U-006

        private static void U006_CommandAuthorityDiagnostic()
        {
            Section("[U-006] command authority diagnostic is exclusive, never a blend");

            MavF16EngineLawRegistrar.EnsureRegistered();
            MavEngineProfile shared = MakeProfile(
                "u006-shared-" + SyntheticTag, null,
                MavEnginePowerDynamicsLaw.F16GarzaMorelliPowerState);

            GameObject go = new GameObject("u006-" + SyntheticTag);
            MavPropulsionSystem sys = go.AddComponent<MavPropulsionSystem>();
            sys.installation = MakeTwin(shared);
            string err;
            sys.Build(true, out err);

            MavScriptedPropulsionCommandSource src =
                go.AddComponent<MavScriptedPropulsionCommandSource>();
            src.Resize(2);
            sys.commandSource = src;
            sys.ResetEngineState(0f);

            src.SetEngineThrottle(0, 1f);
            src.SetEngineThrottle(1, 0.25f);
            sys.Evaluate(LevelState(), Atmosphere(), 0.9f, 0.02f);
            Check(sys.debugCommandAuthority == "PER_ENGINE_SOURCE"
                  && sys.debugAddressedEngineCount == 2, "U-006",
                "full coverage -> PER_ENGINE_SOURCE, " + sys.debugAddressedEngineCount + " addressed");

            src.SetPartialCoverage(1);
            sys.Evaluate(LevelState(), Atmosphere(), 0.9f, 0.02f);
            Check(sys.debugCommandAuthority == "LINKED_SCALAR" && sys.debugPartialCommandRefused,
                "U-006b",
                "partial coverage is REFUSED in full and reported: authority "
                + sys.debugCommandAuthority + ", refused " + sys.debugPartialCommandRefused);
            Check(Mathf.Abs(sys.Command.ThrottleForEngine(0) - 0.9f) < 1e-4f, "U-006c",
                "with the partially commanded engine back on the scalar, not holding its 1.0");

            src.SetPartialCoverage(0);
            sys.Evaluate(LevelState(), Atmosphere(), 0.9f, 0.02f);
            Check(sys.debugCommandAuthority == "PER_ENGINE_SOURCE" && !sys.debugPartialCommandRefused,
                "U-006d", "and restoring coverage restores authority");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(shared);
        }

        // ================================================================== U-007

        private static void U007_AsymmetricThrustInUnity()
        {
            Section("[U-007] asymmetric thrust via r x F, in Unity  (" + SyntheticTag + " geometry)");

            MavSyntheticUnityDeck deck = MakeDeck(
                SynthThrustN, MavThrustDataAuthority.SyntheticBench);
            MavEngineProfile shared = MakeProfile(
                "u007-shared-" + SyntheticTag, deck,
                MavEnginePowerDynamicsLaw.InstantNoSourcedTransient);

            GameObject go = new GameObject("u007-" + SyntheticTag);
            MavPropulsionSystem sys = go.AddComponent<MavPropulsionSystem>();
            sys.installation = MakeTwin(shared);
            string err;
            sys.Build(true, out err);
            sys.ResetEngineState(1f);

            MavPropulsiveLoads both = sys.Evaluate(LevelState(), Atmosphere(), 1f, 0.02f);
            Check(Mathf.Abs(both.forceAeroBodyN.x - 100000f) < 1f, "U-007",
                "equal thrust sums to 2 x 50000 = 100000 N: " + F(both.forceAeroBodyN.x));
            Check(both.momentAeroBodyNm.magnitude < 1f, "U-007b",
                "and the yaw moments cancel to " + F(both.momentAeroBodyNm.magnitude) + " N*m");

            // Right engine off: only the left engine's r x F remains.
            MavScriptedPropulsionCommandSource src =
                go.AddComponent<MavScriptedPropulsionCommandSource>();
            src.Resize(2);
            sys.commandSource = src;
            src.SetEngineThrottle(0, 1f);
            src.SetEngineThrottle(1, 1f);
            src.SetEngineCutoff(1, true);
            MavPropulsiveLoads leftOnly = sys.Evaluate(LevelState(), Atmosphere(), 1f, 0.02f);

            // Independent expectation, computed from the geometry rather than from the system:
            //   r = (0, -3, 0), F = (50000, 0, 0)  ->  r x F = (0, 0, +150000)
            Vector3 expected = Vector3.Cross(
                new Vector3(0f, -SynthLateralOffsetM, 0f), new Vector3(SynthThrustN, 0f, 0f));

            Check(Mathf.Abs(leftOnly.forceAeroBodyN.x - SynthThrustN) < 1f, "U-007c",
                "right engine cut off leaves " + F(leftOnly.forceAeroBodyN.x) + " N");
            Check((leftOnly.momentAeroBodyNm - expected).magnitude < 1f, "U-007d",
                "and the aggregate moment equals an independently computed r x F = " + V(expected)
                + ": got " + V(leftOnly.momentAeroBodyNm));
            Check(leftOnly.momentAeroBodyNm.z > 0f, "U-007e",
                "with a POSITIVE Mz - body Z is down, so this is nose-right, toward the dead engine");

            // Mirror.
            src.SetEngineCutoff(1, false);
            src.SetEngineCutoff(0, true);
            MavPropulsiveLoads rightOnly = sys.Evaluate(LevelState(), Atmosphere(), 1f, 0.02f);
            Check(Mathf.Abs(rightOnly.momentAeroBodyNm.z + expected.z) < 1f, "U-007f",
                "cutting the LEFT engine mirrors it exactly: Mz=" + F(rightOnly.momentAeroBodyNm.z));

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(deck.gameObject);
            Object.DestroyImmediate(shared);
        }

        // ================================================================== U-008

        private static void U008_RigidbodyOwnership()
        {
            Section("[U-008] Rigidbody ownership: propulsion never writes the body");

            // Reflection over the COMPILED types, not the source text. A source grep can be fooled by
            // a comment; a type's method bodies cannot reference a member the metadata does not list.
            System.Type[] engineTypes =
            {
                typeof(MavEngineRuntime), typeof(MavEngineProfile), typeof(MavEngineInstallation),
                typeof(MavPropulsionInstallationProfile), typeof(MavPropulsionSystem),
                typeof(MavPropulsionCommand), typeof(MavF16GarzaMorelliEngineDynamics),
                typeof(MavF16PropulsionSystem)
            };

            int rigidbodyMembers = 0;
            StringBuilder found = new StringBuilder();
            for (int i = 0; i < engineTypes.Length; i++)
            {
                System.Reflection.FieldInfo[] fields = engineTypes[i].GetFields(
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static);
                for (int k = 0; k < fields.Length; k++)
                {
                    if (fields[k].FieldType == typeof(Rigidbody))
                    {
                        rigidbodyMembers++;
                        found.Append(' ').Append(engineTypes[i].Name).Append('.')
                             .Append(fields[k].Name);
                    }
                }

                System.Reflection.PropertyInfo[] props = engineTypes[i].GetProperties(
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static);
                for (int k = 0; k < props.Length; k++)
                {
                    if (props[k].PropertyType == typeof(Rigidbody))
                    {
                        rigidbodyMembers++;
                        found.Append(' ').Append(engineTypes[i].Name).Append('.')
                             .Append(props[k].Name);
                    }
                }
            }

            Check(engineTypes.Length == 8, "U-008pre",
                "checking " + engineTypes.Length + " compiled propulsion types");
            Check(rigidbodyMembers == 0, "U-008",
                "none of them declares a Rigidbody field or property (" + rigidbodyMembers + ")"
                + found + " - so none can write one");

            // Live: a real Rigidbody driven through SixDoF must receive propulsion exactly once.
            GameObject go = new GameObject("u008-" + SyntheticTag);
            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.mass = 9300f;
            rb.useGravity = false;

            MavSyntheticUnityDeck deck = MakeDeck(
                SynthThrustN, MavThrustDataAuthority.SyntheticBench);
            MavEngineProfile profile = MakeProfile(
                "u008-" + SyntheticTag, deck, MavEnginePowerDynamicsLaw.InstantNoSourcedTransient);

            MavPropulsionSystem sys = go.AddComponent<MavPropulsionSystem>();
            sys.installation = MakeSingle(profile);
            string err;
            sys.Build(true, out err);
            sys.ResetEngineState(1f);

            Vector3 velocityBefore = rb.linearVelocity;
            MavPropulsiveLoads loads = sys.Evaluate(LevelState(), Atmosphere(), 1f, 0.02f);

            Check(rb.linearVelocity == velocityBefore, "U-008b",
                "evaluating propulsion does NOT move the Rigidbody by itself - it returns loads and "
                + "applies nothing");
            Check(loads.forceAeroBodyN.magnitude > 1f, "U-008c",
                "while genuinely producing " + F(loads.forceAeroBodyN.magnitude)
                + " N of load, so the check above is not passing because nothing happened");

            // The load-set accumulator must refuse a second propulsive contribution.
            MavFlightDynamicsLoadSet set = new MavFlightDynamicsLoadSet();
            set.BeginStep(0);
            bool firstAdd = set.AddPropulsive(loads);
            bool secondAdd = set.AddPropulsive(loads);
            string reason;
            bool applied = set.TryMarkApplied(out reason);

            Check(firstAdd && !secondAdd, "U-008d",
                "MavFlightDynamicsLoadSet accepts one propulsive contribution and refuses the second");
            Check(!applied, "U-008e",
                "and refuses to apply a load set with a duplicate contribution: " + reason);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(deck.gameObject);
            Object.DestroyImmediate(profile);
        }

        // ================================================================== U-009

        private static void U009_AllocationPerStep()
        {
            Section("[U-009] no per-step GC allocation, at this instrument's resolution");

            MavF16EngineLawRegistrar.EnsureRegistered();
            MavSyntheticUnityDeck deck = MakeDeck(
                SynthThrustN, MavThrustDataAuthority.SyntheticBench);
            MavEngineProfile shared = MakeProfile(
                "u009-shared-" + SyntheticTag, deck,
                MavEnginePowerDynamicsLaw.F16GarzaMorelliPowerState);

            GameObject go = new GameObject("u009-" + SyntheticTag);
            MavPropulsionSystem sys = go.AddComponent<MavPropulsionSystem>();
            sys.installation = MakeTwin(shared);
            string err;
            sys.Build(true, out err);

            MavScriptedPropulsionCommandSource src =
                go.AddComponent<MavScriptedPropulsionCommandSource>();
            src.Resize(2);
            sys.commandSource = src;
            src.SetEngineThrottle(0, 0.9f);
            src.SetEngineThrottle(1, 0.4f);
            sys.ResetEngineState(0.5f);

            MavFlightState state = LevelState();
            MavAtmosphereSample atmo = Atmosphere();

            for (int i = 0; i < 500; i++)
                sys.Evaluate(state, atmo, 0.5f, 0.02f);

            // ============================ WHAT THIS INSTRUMENT CAN AND CANNOT SEE
            //
            // GC.GetTotalMemory is a LEVEL, not a counter: it reports how large the managed heap is
            // right now, not how many bytes were allocated. Two consequences, both measured rather
            // than assumed, and both reported below:
            //
            //   - Background editor allocation lands inside the sample window. At 2000 steps the
            //     same loop has read 786 / 51 / 174 bytes per step. That noise can only ADD, so the
            //     MINIMUM across repeated trials is a sound upper bound. Widening the window does not
            //     help, because background allocation is proportional to elapsed time: a
            //     million-step window in this editor read ~71 bytes/step from 884 ms of background
            //     activity alone.
            //
            //   - Short-lived garbage is invisible. Allocate a float[4] per step for 2000 steps and
            //     this instrument reports ZERO, because the arrays are collected inside the window
            //     and the level returns to where it started. That control is run below. It means a
            //     zero here is an upper bound at the instrument's resolution, NOT a demonstration
            //     that nothing was allocated.
            //
            // So this check establishes a bound, and says how coarse the bound is. The definitive
            // figure comes from a COUNTER - Unity's "GC Allocated In Frame" - which requires real
            // player-loop frames and therefore lives in the P0.2b Play Mode suite.
            const int steps = 2000;
            const int trials = 7;

            double best = Trial(sys, state, atmo, steps, trials);

            // SENSITIVITY CONTROL A: a deliberate ~40 bytes/step. Establishes the floor below which
            // this instrument reports nothing.
            const int controlBytes = 4 * sizeof(float) + 24;
            double smallAllocBest = double.MaxValue;
            float[] keep = null;
            for (int t = 0; t < trials; t++)
            {
                System.GC.Collect();
                System.GC.WaitForPendingFinalizers();
                System.GC.Collect();

                long b0 = System.GC.GetTotalMemory(true);
                for (int i = 0; i < steps; i++)
                    keep = new float[4];

                double rate = (double)(System.GC.GetTotalMemory(false) - b0) / steps;
                if (rate < smallAllocBest) smallAllocBest = rate;
            }

            // SENSITIVITY CONTROL B: the system's own verbose telemetry, which allocates strings.
            // Establishes that the instrument is not simply stuck at zero.
            sys.composeStatusString = true;
            sys.Evaluate(state, atmo, 0.5f, 0.02f);
            bool statusPopulated = !string.IsNullOrEmpty(sys.debugStatus)
                                   && sys.debugStatus.Contains("authority=");
            double verboseBest = Trial(sys, state, atmo, steps, trials);
            sys.composeStatusString = false;

            report.Append("        ").Append(trials).Append(" trials x ").Append(steps)
                .Append(" steps, minimum taken:").AppendLine();
            report.Append("          propulsion, telemetry off : ").Append(best.ToString("0.##"))
                .AppendLine(" bytes/step");
            report.Append("          propulsion, telemetry on  : ")
                .Append(verboseBest.ToString("0.##")).AppendLine(" bytes/step");
            report.Append("          control, float[4]/step (~").Append(controlBytes)
                .Append(" bytes)  : ").Append(smallAllocBest.ToString("0.##"))
                .Append(" bytes/step   <-- SENSITIVITY FLOOR (kept ")
                .Append(keep != null ? keep.Length : 0).AppendLine(")");

            Check(best < 64d, "U-009",
                "the propulsion loop reads " + best.ToString("0.##")
                + " bytes/step as a minimum over " + trials + " trials - an upper bound, since the "
                + "editor background noise this instrument also picks up can only add");
            Check(verboseBest > 100d, "U-009b",
                "the same procedure reads " + verboseBest.ToString("0.##")
                + " bytes/step with verbose telemetry on, so the instrument is not stuck at zero "
                + "and the reading above is a measurement");
            Check(verboseBest > best + 32d, "U-009c",
                "and the A/B separation is clear (" + verboseBest.ToString("0.##") + " vs "
                + best.ToString("0.##") + " bytes/step) - both readings carry the same background "
                + "noise, so their DIFFERENCE is meaningful even though neither absolute figure is. "
                + "This is why verbose telemetry is off by default rather than merely documented");
            Check(statusPopulated, "U-009d",
                "turning verbose telemetry ON still produces the full roll-up, so the budget was "
                + "met by making the diagnostic cheap rather than by deleting it: " + sys.debugStatus);
            Check(smallAllocBest < 16d, "U-009e",
                "RECORDED LIMITATION: a deliberate ~" + controlBytes + " bytes/step control reads "
                + smallAllocBest.ToString("0.##") + " bytes/step on this instrument, so its "
                + "sensitivity floor lies somewhere between " + controlBytes + " and 100 bytes/step. "
                + "U-009 therefore bounds the propulsion path below ~100 bytes/step and no tighter; "
                + "the counter-based Play Mode measurement in P0.2b (L-115) is what resolves the "
                + "region underneath");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(deck.gameObject);
            Object.DestroyImmediate(shared);
        }

        /// <summary>
        /// One minimum-over-trials heap-delta reading for the propulsion loop, in bytes per step.
        /// Shared by the measurement and its verbose-telemetry control so both carry identical
        /// procedure - a control measured differently from the thing it controls proves nothing.
        /// </summary>
        private static double Trial(
            MavPropulsionSystem sys,
            MavFlightState state,
            MavAtmosphereSample atmo,
            int steps,
            int trials)
        {
            double best = double.MaxValue;
            for (int t = 0; t < trials; t++)
            {
                System.GC.Collect();
                System.GC.WaitForPendingFinalizers();
                System.GC.Collect();

                long b0 = System.GC.GetTotalMemory(true);
                for (int i = 0; i < steps; i++)
                    sys.Evaluate(state, atmo, 0.5f, 0.02f);

                double rate = (double)(System.GC.GetTotalMemory(false) - b0) / steps;
                if (rate < best) best = rate;
            }

            return best;
        }

        // ================================================================== U-010

        private static void U010_ProvenanceVersusOperation()
        {
            Section("[U-010] provenance vs operational state, in Unity");

            MavSyntheticUnityDeck auth = MakeDeck(
                SynthThrustN, MavThrustDataAuthority.Authoritative);
            MavEngineProfile sourced = MakeProfile(
                "u010-authoritative-" + SyntheticTag, auth,
                MavEnginePowerDynamicsLaw.InstantNoSourcedTransient);

            GameObject go = new GameObject("u010-" + SyntheticTag);
            MavPropulsionSystem sys = go.AddComponent<MavPropulsionSystem>();
            sys.installation = MakeTwin(sourced);
            string err;
            sys.Build(true, out err);

            MavScriptedPropulsionCommandSource src =
                go.AddComponent<MavScriptedPropulsionCommandSource>();
            src.Resize(2);
            sys.commandSource = src;
            src.SetEngineThrottle(0, 1f);
            src.SetEngineThrottle(1, 1f);
            sys.ResetEngineState(1f);

            MavPropulsiveLoads baseline = sys.Evaluate(LevelState(), Atmosphere(), 1f, 0.02f);
            Check(baseline.hasAuthoritativeData, "U-010pre",
                "baseline: two authoritative engines report authoritative loads");

            src.SetEngineCutoff(1, true);
            MavPropulsiveLoads cutoff = sys.Evaluate(LevelState(), Atmosphere(), 1f, 0.02f);

            Check(Mathf.Abs(cutoff.forceAeroBodyN.x - SynthThrustN) < 1f, "U-010",
                "right engine cutoff halves physical thrust to " + F(cutoff.forceAeroBodyN.x) + " N");
            Check(sys.debugEngineResults[1].thrustN == 0f
                  && !sys.debugEngineResults[1].running
                  && sys.debugEngineResults[1].cutoff, "U-010b",
                "the right engine reports zero thrust, not running, cutoff: "
                + sys.debugEngineResults[1].statusReason);
            Check(cutoff.hasAuthoritativeData, "U-010c",
                "and load provenance REMAINS authoritative - being switched off is not a data gap");
            Check(sys.HasAuthoritativeData, "U-010d",
                "as does the configuration-level answer, which does not depend on operation");
            Check(sys.debugCutoffEngineCount == 1 && sys.debugContributingEngineCount == 1,
                "U-010e",
                "with operational telemetry separate: cutoff=" + sys.debugCutoffEngineCount
                + " contributing=" + sys.debugContributingEngineCount);

            // Now swap one engine for a NON-authoritative profile and shut it down.
            MavSyntheticUnityDeck synth = MakeDeck(
                SynthThrustN, MavThrustDataAuthority.SyntheticBench);
            MavEngineProfile unsourced = MakeProfile(
                "u010-unsourced-" + SyntheticTag, synth,
                MavEnginePowerDynamicsLaw.InstantNoSourcedTransient);
            sys.installation.engines[1].engineProfile = unsourced;
            sys.Build(true, out err);
            src.SetEngineThrottle(0, 1f);
            src.SetEngineThrottle(1, 1f);
            src.SetEngineCutoff(1, true);
            sys.ResetEngineState(1f);
            MavPropulsiveLoads mixedOff = sys.Evaluate(LevelState(), Atmosphere(), 1f, 0.02f);

            Check(mixedOff.hasAuthoritativeData, "U-010f",
                "with the UNSOURCED engine shut down, the LOAD-RESULT provenance is authoritative - "
                + "every number actually summed came from sourced data");
            Check(!sys.HasAuthoritativeData, "U-010g",
                "but the CONFIGURATION answer is false, because an unsourced engine is still "
                + "installed - switching it off does not source its data");
            string reason;
            Check(!sys.installation.IsAcceptableForLiveFlight(out reason), "U-010h",
                "and readiness refuses it: " + reason);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(auth.gameObject);
            Object.DestroyImmediate(synth.gameObject);
            Object.DestroyImmediate(sourced);
            Object.DestroyImmediate(unsourced);
        }

        // ================================================================== U-011

        private static void U011_F16Regression()
        {
            Section("[U-011] F-16 regression under Unity");

            // The sourced equations, evaluated by Unity's own compiled assembly.
            Check(Mathf.Abs(MavF16GarzaMorelliEngineDynamics.ThrottleToCommandedPowerPercent(0.5f)
                            - 32.47f) < 0.01f, "U-011",
                "throttle 0.50 -> 64.94*0.50 = 32.47");
            Check(Mathf.Abs(MavF16GarzaMorelliEngineDynamics.ThrottleToCommandedPowerPercent(0.9f)
                            - 78.262f) < 0.01f, "U-011b",
                "throttle 0.90 -> 217.38*0.90 - 117.38 = 78.262");
            Check(Mathf.Abs(MavF16GarzaMorelliEngineDynamics.ReciprocalTimeConstant(30f) - 0.82f)
                  < 0.001f, "U-011c",
                "RTAU(30) = 1.9 - 0.036*30 = 0.82");
            Check(Mathf.Abs(MavF16GarzaMorelliEngineDynamics.ComputePowerRatePercentPerSec(20f, 80f)
                            - 18.4f) < 0.01f, "U-011d",
                "Pdot(20, 80) = RTAU(40)*(60-20) = 0.46*40 = 18.4 %/s");

            // The transitional shim must agree, since both now read one implementation.
            Check(Mathf.Abs(MavF16EnginePowerModel.ThrottleToCommandedPowerPercent(0.9f)
                            - MavF16GarzaMorelliEngineDynamics.ThrottleToCommandedPowerPercent(0.9f))
                  < 1e-5f, "U-011e",
                "the transitional MavF16EnginePowerModel forwards to the same equation");

            // Spool trajectory through the shared runtime must match an independent integration.
            MavF16EngineLawRegistrar.EnsureRegistered();
            MavPropulsionInstallationProfile f16 =
                MavF16PropulsionInstallation.CreateInstallation(null);

            GameObject go = new GameObject("u011-" + SyntheticTag);
            MavPropulsionSystem sys = go.AddComponent<MavPropulsionSystem>();
            sys.installation = f16;
            string err;
            Check(sys.Build(true, out err), "U-011f", "the real F-16 installation builds: " + err);
            sys.ResetEngineState(0f);

            float expected = 0f;
            bool matched = true;
            for (int i = 0; i < 200; i++)
            {
                sys.Evaluate(LevelState(), Atmosphere(), 0.9f, 0.02f);
                expected = MavF16GarzaMorelliEngineDynamics.StepActualPower(
                    expected,
                    MavF16GarzaMorelliEngineDynamics.ThrottleToCommandedPowerPercent(0.9f),
                    0.02f);
                if (Mathf.Abs(sys.GetRuntime(0).ActualPowerPercent - expected) > 1e-4f)
                    matched = false;
            }

            Check(matched, "U-011g",
                "and reproduces the sourced spool trajectory step for step over 200 steps, ending at "
                + F(sys.GetRuntime(0).ActualPowerPercent) + "% vs " + F(expected) + "%");

            MavPropulsiveLoads loads = sys.Evaluate(LevelState(), Atmosphere(), 1f, 0.02f);
            Check(loads.forceAeroBodyN == Vector3.zero
                  && loads.momentAeroBodyNm == Vector3.zero
                  && loads.reportedThrustN == 0f, "U-011h",
                "with dimensional thrust still EXACTLY zero - no authoritative deck exists");
            Check(!loads.hasAuthoritativeData, "U-011i",
                "reported as non-authoritative rather than as an authoritative zero");
            Check(sys.debugCommandAuthority == "LINKED_SCALAR", "U-011j",
                "and the F-16 runs on LINKED_SCALAR authority, exactly as before P0.1");

            Object.DestroyImmediate(go);
            if (f16.engines.Length > 0 && f16.engines[0].engineProfile != null)
                Object.DestroyImmediate(f16.engines[0].engineProfile);
        }

        // ================================================================== U-012

        private static void U012_MissingProfileFailsSafe()
        {
            Section("[U-012] a missing profile reference fails safe (the normal authoring mistake)");

            GameObject go = new GameObject("u012-" + SyntheticTag);
            MavPropulsionSystem sys = go.AddComponent<MavPropulsionSystem>();

            MavEngineInstallation slot = new MavEngineInstallation();
            slot.slotId = 0;
            slot.slotName = "unassigned";
            slot.engineProfile = null;              // the mistake
            slot.thrustDirectionAeroBody = new Vector3(1f, 0f, 0f);

            sys.installation = new MavPropulsionInstallationProfile();
            sys.installation.installationId = "u012-" + SyntheticTag;
            sys.installation.engines = new MavEngineInstallation[] { slot };

            string err;
            Check(!sys.Build(true, out err), "U-012",
                "an unassigned profile reference refuses to build: " + err);

            MavPropulsiveLoads loads = sys.Evaluate(LevelState(), Atmosphere(), 1f, 0.02f);
            Check(loads.forceAeroBodyN == Vector3.zero && !loads.hasAuthoritativeData, "U-012b",
                "and stepping it yields zero loads rather than a NullReferenceException");

            // A DESTROYED profile is Unity's other flavour of missing - fake-null, not real null.
            MavEngineProfile doomed = MakeProfile(
                "u012-doomed-" + SyntheticTag, null,
                MavEnginePowerDynamicsLaw.InstantNoSourcedTransient);
            slot.engineProfile = doomed;
            Check(sys.Build(true, out err), "U-012c",
                "with a live profile it builds: " + err);

            Object.DestroyImmediate(doomed);
            MavPropulsiveLoads afterDestroy = sys.Evaluate(LevelState(), Atmosphere(), 1f, 0.02f);
            Check(afterDestroy.forceAeroBodyN == Vector3.zero, "U-012d",
                "and after the profile asset is DESTROYED mid-session the system produces zero loads "
                + "instead of throwing - Unity's fake-null is handled, which no offline harness could "
                + "have shown");
            Check(!afterDestroy.hasAuthoritativeData, "U-012e",
                "reported as non-authoritative");

            Object.DestroyImmediate(go);
        }

        // ================================================================== helpers

        private static MavEngineProfile MakeProfile(
            string id, MavThrustDeckBase deck, MavEnginePowerDynamicsLaw law)
        {
            MavEngineProfile p = MavEngineProfile.CreateInMemory(id);
            p.displayName = id;
            p.engineVariantIdentity = SyntheticTag + " - not aircraft data";
            p.sourceIdentity = SyntheticTag;
            p.provenance = MavEngineDataProvenance.CrossValidationOnly;
            p.powerDynamicsLaw = law;
            p.powerDynamicsProvenance = law == MavEnginePowerDynamicsLaw.InstantNoSourcedTransient
                ? MavEngineDataProvenance.Unavailable
                : MavEngineDataProvenance.PublicReference;
            p.thrustDeck = deck;
            return p;
        }

        private static MavEngineInstallation MakeSlot(
            int id, string name, MavEngineProfile profile, Vector3 pos, int channel)
        {
            MavEngineInstallation s = new MavEngineInstallation();
            s.slotId = id;
            s.slotName = name;
            s.engineProfile = profile;
            s.positionAeroBodyM = pos;
            s.thrustDirectionAeroBody = new Vector3(1f, 0f, 0f);
            s.geometryDeclared = true;
            s.geometryProvenance = SyntheticTag + " - not aircraft geometry";
            s.throttleChannel = channel;
            s.enabled = true;
            return s;
        }

        private static MavPropulsionInstallationProfile MakeTwin(MavEngineProfile shared)
        {
            MavPropulsionInstallationProfile i = new MavPropulsionInstallationProfile();
            i.installationId = "unity-twin-" + SyntheticTag;
            i.aircraftConfiguration = SyntheticTag;
            i.engines = new MavEngineInstallation[]
            {
                MakeSlot(0, "left", shared, new Vector3(0f, -SynthLateralOffsetM, 0f), 0),
                MakeSlot(1, "right", shared, new Vector3(0f, SynthLateralOffsetM, 0f), 1)
            };
            return i;
        }

        private static MavPropulsionInstallationProfile MakeSingle(MavEngineProfile profile)
        {
            MavPropulsionInstallationProfile i = new MavPropulsionInstallationProfile();
            i.installationId = "unity-single-" + SyntheticTag;
            i.aircraftConfiguration = SyntheticTag;
            i.engines = new MavEngineInstallation[] { MakeSlot(0, "single", profile, Vector3.zero, 0) };
            return i;
        }

        private static MavSyntheticUnityDeck MakeDeck(float thrustN, MavThrustDataAuthority authority)
        {
            GameObject host = new GameObject("deck-" + SyntheticTag);
            MavSyntheticUnityDeck d = host.AddComponent<MavSyntheticUnityDeck>();
            d.fixedThrustN = thrustN;
            d.declaredAuthority = authority;
            return d;
        }

        private static MavFlightState LevelState()
        {
            MavFlightState s = new MavFlightState();
            s.worldPositionM = new Vector3(0f, 3000f, 0f);
            s.mach = 0.5f;
            s.trueAirspeedMps = 160f;
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

        private static void Section(string title)
        {
            report.AppendLine();
            report.AppendLine(title);
        }

        private static void Check(bool condition, string id, string description)
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

        private static string F(float v)
        {
            return v.ToString("0.####");
        }

        private static string V(Vector3 v)
        {
            return "(" + F(v.x) + ", " + F(v.y) + ", " + F(v.z) + ")";
        }
    }

    /// <summary>
    /// Fixed-thrust deck for the Unity suite. SYNTHETIC_VALIDATION_ONLY - never aircraft data.
    /// Separate from the harness's deck so the editor suite carries no dependency on the runtime
    /// validation assembly's types beyond what it explicitly uses.
    /// </summary>
    public sealed class MavSyntheticUnityDeck : MavThrustDeckBase
    {
        public float fixedThrustN;
        public MavThrustDataAuthority declaredAuthority = MavThrustDataAuthority.SyntheticBench;

        public override string DeckName
        {
            get { return "SYNTHETIC_VALIDATION_ONLY fixed deck"; }
        }

        public override MavThrustDataAuthority Authority
        {
            get { return declaredAuthority; }
        }

        public override MavEnvelopeExcursionPolicy ExcursionPolicy
        {
            get { return MavEnvelopeExcursionPolicy.RejectUnsupportedState; }
        }

        public override MavThrustDeckResult Evaluate(MavThrustDeckQuery query)
        {
            if (declaredAuthority == MavThrustDataAuthority.Unavailable)
                return MavThrustDeckResult.Unavailable("synthetic deck declared Unavailable");

            MavThrustDeckResult r = new MavThrustDeckResult();
            r.valid = true;
            r.thrustN = fixedThrustN * Mathf.Clamp(query.actualPowerPercent, 0f, 100f) * 0.01f;
            r.insideEnvelope = true;
            r.authority = declaredAuthority;
            r.statusReason = "SYNTHETIC_VALIDATION_ONLY";
            return r;
        }
    }
}
