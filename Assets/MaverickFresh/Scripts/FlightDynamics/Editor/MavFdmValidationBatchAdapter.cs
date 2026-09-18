#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using MaverickFresh.FlightDynamics.Validation;

namespace MaverickFresh.FlightDynamics.EditorTools
{
    /// <summary>
    /// Headless-only adapter for FDM Validation Baseline v1.
    ///
    /// This file does not change validator semantics. Synchronous validators are invoked through
    /// their existing result-producing methods. The FixedUpdate scheduler suite is special only
    /// because its existing menu path displays an interactive dialog and returns no batch result;
    /// this adapter reuses the existing private StartProbeInPlayMode implementation and the existing
    /// MavFlightDynamicsSchedulerProbeState, while replacing only interactive orchestration.
    ///
    /// Output contract:
    ///   FDM_VALIDATION_RESULT_V1 {json}
    /// and, when -fdmOut is supplied, the same JSON is written to that path.
    /// </summary>
    [InitializeOnLoad]
    public static class MavFdmValidationBatchAdapter
    {
        private const string Marker = "FDM_VALIDATION_RESULT_V1";
        private const string SchedulerActiveKey = "Mav.FdmBaselineV1.Scheduler.Active";
        private const string SchedulerSuiteKey = "Mav.FdmBaselineV1.Scheduler.Suite";
        private const string SchedulerOutKey = "Mav.FdmBaselineV1.Scheduler.Out";
        private const string SchedulerDeadlineKey = "Mav.FdmBaselineV1.Scheduler.Deadline";
        private const string SchedulerOutcomeKey = "Mav.FdmBaselineV1.Scheduler.Outcome";
        private const string SchedulerSummaryKey = "Mav.FdmBaselineV1.Scheduler.Summary";
        private const string SchedulerPassedKey = "Mav.FdmBaselineV1.Scheduler.Passed";
        private const string SchedulerFailedKey = "Mav.FdmBaselineV1.Scheduler.Failed";

        private static readonly Regex ResultRegex = new Regex(
            @"RESULT:\s*(PASS|FAIL)\s+passed\s*=\s*(\d+)\s+failed\s*=\s*(\d+)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly StringBuilder CapturedLog = new StringBuilder(16384);
        private static bool captureEnabled;

        [Serializable]
        private sealed class ResultRecord
        {
            public int schema_version = 1;
            public string suite = string.Empty;
            public string status = "INFRA_FAILURE";
            public int passed = -1;
            public int failed = -1;
            public string detail = string.Empty;
            public string unity_version = string.Empty;
        }

        static MavFdmValidationBatchAdapter()
        {
            if (SessionState.GetBool(SchedulerActiveKey, false))
                HookSchedulerCallbacks();
        }

        /// <summary>
        /// Unity batch entry point.
        ///
        /// Required args:
        ///   -fdmSuite &lt;id&gt;
        ///   -fdmOut &lt;absolute-result-json-path&gt;
        ///
        /// Sync mode:
        ///   -fdmMode sync -fdmType &lt;type-name-or-full-name&gt; [-fdmMethod AUTO|name]
        ///
        /// Scheduler mode:
        ///   -fdmMode scheduler
        ///
        /// Do not pass -quit. This method exits Unity with 0/1/2 after producing a result.
        /// </summary>
        public static void RunBatch()
        {
            string suite = CommandLineValue("-fdmSuite");
            string outPath = CommandLineValue("-fdmOut");
            string mode = CommandLineValue("-fdmMode") ?? "sync";

            if (string.IsNullOrEmpty(suite) || string.IsNullOrEmpty(outPath))
            {
                FinishImmediate(
                    suite ?? "(missing)", outPath,
                    "INFRA_FAILURE", -1, -1,
                    "-fdmSuite and -fdmOut are required.", 2);
                return;
            }

            if (!Application.isBatchMode)
            {
                FinishImmediate(
                    suite, outPath, "INFRA_FAILURE", -1, -1,
                    "MavFdmValidationBatchAdapter is headless-only and requires -batchmode.", 2);
                return;
            }

            if (string.Equals(mode, "scheduler", StringComparison.OrdinalIgnoreCase))
            {
                StartSchedulerBatch(suite, outPath);
                return;
            }

            if (!string.Equals(mode, "sync", StringComparison.OrdinalIgnoreCase))
            {
                FinishImmediate(
                    suite, outPath, "INFRA_FAILURE", -1, -1,
                    "Unknown -fdmMode: " + mode, 2);
                return;
            }

            RunSynchronousBatch(
                suite,
                outPath,
                CommandLineValue("-fdmType"),
                CommandLineValue("-fdmMethod") ?? "AUTO");
        }

        private static void RunSynchronousBatch(
            string suite, string outPath, string typeToken, string methodToken)
        {
            if (string.IsNullOrEmpty(typeToken))
            {
                FinishImmediate(
                    suite, outPath, "INFRA_FAILURE", -1, -1,
                    "sync mode requires -fdmType.", 2);
                return;
            }

            CapturedLog.Length = 0;
            Application.logMessageReceived -= CaptureLog;
            Application.logMessageReceived += CaptureLog;
            captureEnabled = true;

            try
            {
                Type type = ResolveType(typeToken);

                if (string.Equals(methodToken, "AUTO", StringComparison.OrdinalIgnoreCase)
                    && TryRunKnownSourceScan(suite, outPath, type))
                {
                    return;
                }

                MethodInfo method = ResolveValidationMethod(type, methodToken);
                ParameterInfo[] parameters = method.GetParameters();
                object[] args = parameters.Length == 2
                    ? new object[] { 0, 0 }
                    : new object[0];

                object returnValue;
                try
                {
                    returnValue = method.Invoke(null, args);
                }
                catch (TargetInvocationException tie)
                {
                    Exception inner = tie.InnerException ?? tie;
                    throw new InvalidOperationException(
                        type.FullName + "." + method.Name + " threw "
                        + inner.GetType().Name + ": " + inner.Message, inner);
                }

                int passed = -1;
                int failed = -1;
                string report = returnValue as string ?? string.Empty;

                if (!string.IsNullOrEmpty(report))
                    Debug.Log("FDM_VALIDATION_REPORT\n" + report);

                if (parameters.Length == 2)
                {
                    passed = Convert.ToInt32(args[0]);
                    failed = Convert.ToInt32(args[1]);
                }
                else if (returnValue is bool)
                {
                    bool ok = (bool)returnValue;
                    passed = ok ? 1 : 0;
                    failed = ok ? 0 : 1;
                }
                else
                {
                    TryExtractCounts(returnValue, ref passed, ref failed);
                }

                Match marker = LastResultMarker(report + "\n" + CapturedLog);
                if (marker != null)
                {
                    int markerPassed = int.Parse(marker.Groups[2].Value);
                    int markerFailed = int.Parse(marker.Groups[3].Value);
                    if (passed >= 0 && (passed != markerPassed || failed != markerFailed))
                    {
                        throw new InvalidDataException(
                            "validator result disagreement: out/result counts " + passed + "/" + failed
                            + " but RESULT marker reports " + markerPassed + "/" + markerFailed + ".");
                    }

                    passed = markerPassed;
                    failed = markerFailed;
                }

                if (passed < 0 || failed < 0)
                {
                    throw new InvalidDataException(
                        "validator executed but produced neither out counts nor a parseable "
                        + "RESULT: PASS|FAIL passed=<n> failed=<n> marker.");
                }

                string detail = "invoked " + type.FullName + "." + method.Name
                                + "; validator-reported counts are authoritative";
                FinishImmediate(
                    suite, outPath,
                    failed == 0 ? "PASS" : "FAIL",
                    passed, failed, detail,
                    failed == 0 ? 0 : 1);
            }
            catch (Exception e)
            {
                FinishImmediate(
                    suite, outPath, "INFRA_FAILURE", -1, -1,
                    e.GetType().Name + ": " + e.Message, 2);
            }
            finally
            {
                captureEnabled = false;
                Application.logMessageReceived -= CaptureLog;
            }
        }

        private static bool TryRunKnownSourceScan(
            string suite, string outPath, Type type)
        {
            string fullName = type.FullName ?? type.Name;

            if (fullName ==
                "MaverickFresh.FlightDynamics.Validation.MavFlightDynamicsOwnershipScan")
            {
                MaverickFresh.FlightDynamics.Validation.MavOwnershipScanResult result =
                    MaverickFresh.FlightDynamics.Validation.MavFlightDynamicsOwnershipScan.Scan();

                if (!result.sourcesAvailable)
                {
                    FinishImmediate(
                        suite, outPath, "INFRA_FAILURE", -1, -1,
                        "ownership source scan could not access its source tree.", 2);
                    return true;
                }

                int violations = result.violations != null
                    ? result.violations.Count
                    : 0;
                bool clean = violations == 0;

                if (result.violations != null)
                {
                    for (int i = 0; i < result.violations.Count; i++)
                        Debug.Log("FDM_OWNERSHIP_VIOLATION " + result.violations[i]);
                }

                FinishImmediate(
                    suite, outPath,
                    clean ? "PASS" : "FAIL",
                    clean ? 1 : 0,
                    clean ? 0 : 1,
                    "ownership source scan executed; filesScanned="
                    + result.filesScanned
                    + "; violations=" + violations,
                    clean ? 0 : 1);
                return true;
            }

            if (fullName ==
                "MaverickFresh.EditorTools.MavAircraftIdentityOwnershipScan")
            {
                string scriptsRoot = Path.Combine(
                    Application.dataPath, "MaverickFresh/Scripts");

                MaverickFresh.EditorTools.MavIdentityOwnershipScanResult result =
                    MaverickFresh.EditorTools.MavAircraftIdentityOwnershipScan.Scan(
                        scriptsRoot);

                if (!result.sourcesAvailable)
                {
                    FinishImmediate(
                        suite, outPath, "INFRA_FAILURE", -1, -1,
                        "aircraft identity ownership scan could not access "
                        + scriptsRoot, 2);
                    return true;
                }

                int violations = result.violations != null
                    ? result.violations.Count
                    : 0;
                bool clean = violations == 0;

                FinishImmediate(
                    suite, outPath,
                    clean ? "PASS" : "FAIL",
                    clean ? 1 : 0,
                    clean ? 0 : 1,
                    "aircraft identity ownership scan executed; filesScanned="
                    + result.filesScanned
                    + "; violations=" + violations,
                    clean ? 0 : 1);
                return true;
            }

            if (fullName ==
                "MaverickFresh.EditorTools.MavAircraftSceneWiringScan")
            {
                MaverickFresh.EditorTools.MavSceneWiringScanResult result =
                    MaverickFresh.EditorTools.MavAircraftSceneWiringScan.Scan(
                        Application.dataPath);

                if (!result.sourcesAvailable)
                {
                    FinishImmediate(
                        suite, outPath, "INFRA_FAILURE", -1, -1,
                        "aircraft scene wiring scan could not access "
                        + Application.dataPath, 2);
                    return true;
                }

                int violations = result.violations != null
                    ? result.violations.Count
                    : 0;
                bool clean = violations == 0;

                FinishImmediate(
                    suite, outPath,
                    clean ? "PASS" : "FAIL",
                    clean ? 1 : 0,
                    clean ? 0 : 1,
                    "aircraft scene wiring scan executed; scenesScanned="
                    + result.scenesScanned
                    + "; violations=" + violations,
                    clean ? 0 : 1);
                return true;
            }

            if (fullName ==
                "MaverickFresh.EditorTools.MavPhase5WriterScan")
            {
                string reason;
                MaverickFresh.EditorTools.MavPhysicsWriterScanResult result =
                    MaverickFresh.EditorTools.MavPhase5WriterScan.Scan(
                        Application.dataPath, out reason);

                if (!result.sourcesAvailable)
                {
                    FinishImmediate(
                        suite, outPath, "INFRA_FAILURE", -1, -1,
                        "Phase 5 writer scan could not access its source tree: "
                        + (reason ?? string.Empty), 2);
                    return true;
                }

                int ungated = result.ungatedPlayerWriters != null
                    ? result.ungatedPlayerWriters.Count
                    : 0;

                int unknownDuplicates = 0;
                if (result.duplicateBaseNames != null)
                {
                    for (int i = 0; i < result.duplicateBaseNames.Count; i++)
                    {
                        string duplicate = result.duplicateBaseNames[i]
                                           ?? string.Empty;
                        string fileName = duplicate;

                        int suffix = duplicate.LastIndexOf(
                            " x", StringComparison.Ordinal);
                        if (suffix > 0)
                            fileName = duplicate.Substring(0, suffix);

                        if (!MaverickFresh.EditorTools.MavPhase5WriterScan
                                .IsKnownDuplicateBaseName(fileName))
                        {
                            unknownDuplicates++;
                        }
                    }
                }

                bool clean = ungated == 0 && unknownDuplicates == 0;

                FinishImmediate(
                    suite, outPath,
                    clean ? "PASS" : "FAIL",
                    clean ? 1 : 0,
                    clean ? 0 : 1,
                    "Phase 5 writer scan executed; filesScanned="
                    + result.filesScanned
                    + "; ungatedPlayerWriters=" + ungated
                    + "; unknownDuplicateBaseNames=" + unknownDuplicates
                    + "; scanRoot=" + (reason ?? string.Empty),
                    clean ? 0 : 1);
                return true;
            }

            return false;
        }
        private static Type ResolveType(string token)
        {
            List<Type> matches = new List<Type>();
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int a = 0; a < assemblies.Length; a++)
            {
                Type[] types;
                try
                {
                    types = assemblies[a].GetTypes();
                }
                catch (ReflectionTypeLoadException rtle)
                {
                    types = rtle.Types;
                }

                for (int i = 0; i < types.Length; i++)
                {
                    Type t = types[i];
                    if (t == null)
                        continue;
                    if (string.Equals(t.FullName, token, StringComparison.Ordinal)
                        || string.Equals(t.Name, token, StringComparison.Ordinal))
                    {
                        matches.Add(t);
                    }
                }
            }

            if (matches.Count != 1)
                throw new TypeLoadException(
                    "expected exactly one loaded type matching '" + token + "', found " + matches.Count + ".");
            return matches[0];
        }

        private static MethodInfo ResolveValidationMethod(Type type, string methodToken)
        {
            BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            MethodInfo[] methods = type.GetMethods(flags);

            if (!string.Equals(methodToken, "AUTO", StringComparison.OrdinalIgnoreCase))
            {
                List<MethodInfo> exact = new List<MethodInfo>();
                for (int i = 0; i < methods.Length; i++)
                {
                    if (methods[i].Name == methodToken && IsSupportedSignature(methods[i]))
                        exact.Add(methods[i]);
                }

                if (exact.Count != 1)
                    throw new MissingMethodException(
                        type.FullName + ": expected one supported static method named "
                        + methodToken + ", found " + exact.Count + ".");
                return exact[0];
            }

            List<MethodInfo> runAllWithCounts = new List<MethodInfo>();
            for (int i = 0; i < methods.Length; i++)
            {
                if (methods[i].Name == "RunAll" && HasOutIntPair(methods[i]))
                    runAllWithCounts.Add(methods[i]);
            }
            if (runAllWithCounts.Count == 1)
                return runAllWithCounts[0];
            if (runAllWithCounts.Count > 1)
                throw new AmbiguousMatchException(type.FullName + " has multiple RunAll(out int,out int) methods.");

            string[] preferred = new string[] { "RunValidation", "ValidateAll", "RunAll", "Validate", "Run" };
            for (int n = 0; n < preferred.Length; n++)
            {
                List<MethodInfo> named = new List<MethodInfo>();
                for (int i = 0; i < methods.Length; i++)
                {
                    if (methods[i].Name == preferred[n] && IsSupportedSignature(methods[i]))
                        named.Add(methods[i]);
                }
                if (named.Count == 1)
                    return named[0];
                if (named.Count > 1)
                    throw new AmbiguousMatchException(
                        type.FullName + " has multiple supported methods named " + preferred[n] + ".");
            }

            List<MethodInfo> generic = new List<MethodInfo>();
            for (int i = 0; i < methods.Length; i++)
            {
                string n = methods[i].Name;
                if (!IsSupportedSignature(methods[i]))
                    continue;
                if (!(n.StartsWith("Run", StringComparison.Ordinal)
                      || n.StartsWith("Validate", StringComparison.Ordinal)))
                    continue;
                if (n.IndexOf("Menu", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("Batch", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                generic.Add(methods[i]);
            }

            if (generic.Count == 1)
                return generic[0];

            throw new MissingMethodException(
                type.FullName + ": AUTO could not select exactly one non-interactive validation entry point; found "
                + generic.Count + " fallback candidates.");
        }

        private static bool IsSupportedSignature(MethodInfo method)
        {
            ParameterInfo[] p = method.GetParameters();
            return p.Length == 0 || HasOutIntPair(method);
        }

        private static bool HasOutIntPair(MethodInfo method)
        {
            ParameterInfo[] p = method.GetParameters();
            if (p.Length != 2)
                return false;
            Type byRefInt = typeof(int).MakeByRefType();
            return p[0].ParameterType == byRefInt && p[1].ParameterType == byRefInt;
        }

        private static void TryExtractCounts(object value, ref int passed, ref int failed)
        {
            if (value == null)
                return;

            Type type = value.GetType();
            object p = ReadMember(type, value, "Passed") ?? ReadMember(type, value, "passed");
            object f = ReadMember(type, value, "Failed") ?? ReadMember(type, value, "failed");
            if (p == null || f == null)
                return;

            passed = Convert.ToInt32(p);
            failed = Convert.ToInt32(f);
        }

        private static object ReadMember(Type type, object value, string name)
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            PropertyInfo property = type.GetProperty(name, flags);
            if (property != null && property.CanRead)
                return property.GetValue(value, null);
            FieldInfo field = type.GetField(name, flags);
            return field != null ? field.GetValue(value) : null;
        }

        private static Match LastResultMarker(string text)
        {
            MatchCollection matches = ResultRegex.Matches(text ?? string.Empty);
            return matches.Count == 0 ? null : matches[matches.Count - 1];
        }

        private static void CaptureLog(string condition, string stackTrace, LogType type)
        {
            if (!captureEnabled)
                return;
            CapturedLog.AppendLine(condition ?? string.Empty);
        }

        // ---------------------------------------------------------------- scheduler batch bridge

        private static void StartSchedulerBatch(string suite, string outPath)
        {
            string scenePath = SceneManager.GetActiveScene().path;
            if (!string.IsNullOrEmpty(scenePath))
            {
                FinishImmediate(
                    suite, outPath, "INFRA_FAILURE", -1, -1,
                    "scheduler batch refuses to enter Play Mode with saved scene open: " + scenePath, 2);
                return;
            }

            SessionState.SetBool(SchedulerActiveKey, true);
            SessionState.SetString(SchedulerSuiteKey, suite);
            SessionState.SetString(SchedulerOutKey, outPath);
            SessionState.SetFloat(SchedulerDeadlineKey, (float)EditorApplication.timeSinceStartup + 300f);
            SessionState.SetString(SchedulerOutcomeKey, string.Empty);
            SessionState.SetString(SchedulerSummaryKey, string.Empty);
            SessionState.SetInt(SchedulerPassedKey, -1);
            SessionState.SetInt(SchedulerFailedKey, -1);

            HookSchedulerCallbacks();

            try
            {
                MavFlightDynamicsSchedulerProbeState.Reset();
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e)
            {
                CompleteScheduler(
                    "INFRA_FAILURE", -1, -1,
                    "could not request Play Mode: " + e.GetType().Name + ": " + e.Message);
            }
        }

        private static void HookSchedulerCallbacks()
        {
            EditorApplication.playModeStateChanged -= OnSchedulerPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnSchedulerPlayModeStateChanged;
            EditorApplication.update -= OnSchedulerUpdate;
            EditorApplication.update += OnSchedulerUpdate;
            DetachInteractiveSchedulerPoll();
        }

        private static void DetachInteractiveSchedulerPoll()
        {
            try
            {
                MethodInfo poll = typeof(MavFlightDynamicsSchedulerValidationEditor).GetMethod(
                    "PollResult", BindingFlags.Static | BindingFlags.NonPublic);
                if (poll == null)
                    return;
                EditorApplication.CallbackFunction callback =
                    (EditorApplication.CallbackFunction)Delegate.CreateDelegate(
                        typeof(EditorApplication.CallbackFunction), poll);
                EditorApplication.update -= callback;
            }
            catch (Exception)
            {
                // The batch bridge independently polls the public probe state. If Unity changes the
                // private callback shape, the run still fails closed if an interactive path blocks.
            }
        }

        private static void OnSchedulerPlayModeStateChanged(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(SchedulerActiveKey, false))
                return;

            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                DetachInteractiveSchedulerPoll();
                try
                {
                    MethodInfo start = typeof(MavFlightDynamicsSchedulerValidationEditor).GetMethod(
                        "StartProbeInPlayMode", BindingFlags.Static | BindingFlags.NonPublic);
                    if (start == null)
                        throw new MissingMethodException(
                            "MavFlightDynamicsSchedulerValidationEditor.StartProbeInPlayMode was not found.");
                    start.Invoke(null, null);
                }
                catch (TargetInvocationException tie)
                {
                    Exception inner = tie.InnerException ?? tie;
                    CompleteScheduler(
                        "INFRA_FAILURE", -1, -1,
                        "scheduler probe setup threw " + inner.GetType().Name + ": " + inner.Message);
                }
                catch (Exception e)
                {
                    CompleteScheduler(
                        "INFRA_FAILURE", -1, -1,
                        "scheduler probe setup failed: " + e.GetType().Name + ": " + e.Message);
                }
                return;
            }

            if (change == PlayModeStateChange.EnteredEditMode
                && !string.IsNullOrEmpty(SessionState.GetString(SchedulerOutcomeKey, string.Empty)))
            {
                FinishSchedulerAfterExit();
            }
        }

        private static void OnSchedulerUpdate()
        {
            if (!SessionState.GetBool(SchedulerActiveKey, false))
                return;

            DetachInteractiveSchedulerPoll();

            if (EditorApplication.timeSinceStartup
                > SessionState.GetFloat(SchedulerDeadlineKey, 0f))
            {
                CompleteScheduler("INFRA_FAILURE", -1, -1, "scheduler validation timed out after 300 seconds.");
                return;
            }

            if (!EditorApplication.isPlaying || !MavFlightDynamicsSchedulerProbeState.completed)
                return;

            bool ok = MavFlightDynamicsSchedulerProbeState.passed;
            CompleteScheduler(
                ok ? "PASS" : "FAIL",
                ok ? 1 : 0,
                ok ? 0 : 1,
                MavFlightDynamicsSchedulerProbeState.summary ?? string.Empty);
        }

        private static void CompleteScheduler(
            string status, int passed, int failed, string summary)
        {
            if (!string.IsNullOrEmpty(SessionState.GetString(SchedulerOutcomeKey, string.Empty)))
                return;

            SessionState.SetString(SchedulerOutcomeKey, status);
            SessionState.SetString(SchedulerSummaryKey, summary ?? string.Empty);
            SessionState.SetInt(SchedulerPassedKey, passed);
            SessionState.SetInt(SchedulerFailedKey, failed);

            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.ExitPlaymode();
                return;
            }

            FinishSchedulerAfterExit();
        }

        private static void FinishSchedulerAfterExit()
        {
            string suite = SessionState.GetString(SchedulerSuiteKey, "fdm_scheduler");
            string outPath = SessionState.GetString(SchedulerOutKey, string.Empty);
            string status = SessionState.GetString(SchedulerOutcomeKey, "INFRA_FAILURE");
            string summary = SessionState.GetString(SchedulerSummaryKey, string.Empty);
            int passed = SessionState.GetInt(SchedulerPassedKey, -1);
            int failed = SessionState.GetInt(SchedulerFailedKey, -1);

            SessionState.SetBool(SchedulerActiveKey, false);
            EditorApplication.playModeStateChanged -= OnSchedulerPlayModeStateChanged;
            EditorApplication.update -= OnSchedulerUpdate;

            FinishImmediate(
                suite, outPath, status, passed, failed,
                "existing scheduler probe/state reused; " + summary,
                status == "PASS" ? 0 : status == "FAIL" ? 1 : 2);
        }

        // ---------------------------------------------------------------- result/CLI helpers

        private static string CommandLineValue(string flag)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == flag)
                    return args[i + 1];
            }
            return null;
        }

        private static void FinishImmediate(
            string suite, string outPath, string status,
            int passed, int failed, string detail, int exitCode)
        {
            ResultRecord record = new ResultRecord();
            record.suite = suite ?? string.Empty;
            record.status = status ?? "INFRA_FAILURE";
            record.passed = passed;
            record.failed = failed;
            record.detail = detail ?? string.Empty;
            record.unity_version = Application.unityVersion;

            string json = JsonUtility.ToJson(record, true);
            string markerLine = Marker + " " + JsonUtility.ToJson(record, false);

            if (!string.IsNullOrEmpty(outPath))
            {
                try
                {
                    string parent = Path.GetDirectoryName(outPath);
                    if (!string.IsNullOrEmpty(parent))
                        Directory.CreateDirectory(parent);
                    File.WriteAllText(outPath, json, new UTF8Encoding(false));
                }
                catch (Exception e)
                {
                    record.status = "INFRA_FAILURE";
                    record.passed = -1;
                    record.failed = -1;
                    record.detail = "could not write result file: " + e.GetType().Name + ": " + e.Message;
                    json = JsonUtility.ToJson(record, true);
                    markerLine = Marker + " " + JsonUtility.ToJson(record, false);
                    exitCode = 2;
                }
            }

            Debug.Log(markerLine);
            Console.WriteLine(markerLine);
            EditorApplication.Exit(exitCode);
        }
    }
}
#endif
