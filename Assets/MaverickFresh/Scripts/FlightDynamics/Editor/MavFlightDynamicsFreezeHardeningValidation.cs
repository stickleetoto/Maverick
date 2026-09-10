#if UNITY_EDITOR
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using MaverickFresh.FlightDynamics;
using MaverickFresh.FlightDynamics.F16;

namespace MaverickFresh.FlightDynamics.EditorTools
{
    /// <summary>
    /// Freeze-review regressions for bugs found after the first component-integration suite landed.
    /// These checks are intentionally reported separately from the pure/static count.
    /// </summary>
    public static class MavFlightDynamicsFreezeHardeningValidation
    {
        private const float Dt = 0.02f;
        private const float G0 = MavControlLawProtections.StandardGravityMps2;

        [MenuItem("Maverick/Flight Dynamics/Run Freeze Hardening Validation")]
        public static void RunFromMenu()
        {
            int passed;
            int failed;
            string report = RunAll(out passed, out failed);

            if (failed == 0)
                Debug.Log(report);
            else
                Debug.LogError(report);

            EditorUtility.DisplayDialog(
                "Maverick Freeze Hardening Validation",
                failed == 0
                    ? "PASS\n\n" + passed + " checks passed."
                    : "FAIL\n\n" + failed + " checks failed. See Console for details.",
                "OK"
            );
        }

        public static string RunAll(out int passed, out int failed)
        {
            passed = 0;
            failed = 0;
            StringBuilder report = new StringBuilder(4096);
            report.AppendLine("Maverick Flight Dynamics FREEZE HARDENING Validation");
            report.AppendLine("====================================================");

            ValidatePartialRestoreRule(report, ref passed, ref failed);
            ValidatePartialRestoreAndStickyFault(report, ref passed, ref failed);
            ValidateInitiallyUnownedRig(report, ref passed, ref failed);
            ValidateSpecificForceProductionInvocation(report, ref passed, ref failed);
            ValidatePhysicsStepSurface(report, ref passed, ref failed);

            report.AppendLine();
            report.Append("RESULT: ")
                .Append(failed == 0 ? "PASS" : "FAIL")
                .Append("  passed=").Append(passed)
                .Append(" failed=").Append(failed);
            return report.ToString();
        }

        private static void ValidatePartialRestoreRule(
            StringBuilder report,
            ref int passed,
            ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[H0] Partial restoration can never settle as LegacyOwned");

            MavOwnershipObservation observation = new MavOwnershipObservation();
            observation.structurallyPrepared = true;
            observation.readyExceptLegacyOwnership = true;
            observation.operationallyLiveReady = false;
            observation.legacyOwnerActive = true;      // one owner came back
            observation.legacyOwnerPresent = true;
            observation.legacyRestorationWasRequired = true;
            observation.legacyRestorationSucceeded = false; // another owner did not
            observation.newFdmArmed = false;

            string reason;
            MavPhysicsOwnershipState settled =
                MavPhysicsOwnershipRules.ResolveSettledOwnership(observation, out reason);

            Record(
                settled == MavPhysicsOwnershipState.Fault,
                "one surviving legacy owner does not launder a failed multi-owner restore into LegacyOwned ("
                + settled + ": " + reason + ")",
                report, ref passed, ref failed);
        }

        private static void ValidatePartialRestoreAndStickyFault(
            StringBuilder report,
            ref int passed,
            ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[H1] Partial restore + restoration debt on real components");

            HardeningRig rig = HardeningRig.Build(legacyOwnerCount: 2, withOwnershipController: true);
            try
            {
                rig.SetFlightCondition(3000f, 200f);
                rig.MakeOperationallyLiveReady();
                rig.Step();

                rig.ownership.RequestTransitionToNewFdm();
                rig.Step();

                Record(
                    rig.ownership.state == MavPhysicsOwnershipState.NewOwned
                    && !rig.legacyA.enabled && !rig.legacyB.enabled,
                    "handover disabled both legacy owners before the partial-restore test",
                    report, ref passed, ref failed);

                Object.DestroyImmediate(rig.legacyB);
                rig.legacyB = null;

                rig.ownership.RequestReturnToLegacy();
                rig.Step();

                Record(
                    rig.ownership.state == MavPhysicsOwnershipState.Fault,
                    "restoring A while B is destroyed is a Fault, never LegacyOwned ("
                    + rig.ownership.state + ": " + rig.ownership.stateReason + ")",
                    report, ref passed, ref failed);

                Record(
                    rig.legacyA != null && rig.legacyA.enabled && !rig.body.simulationEnabled,
                    "the surviving legacy owner is restored while the new FDM is held disarmed",
                    report, ref passed, ref failed);

                int debtBeforeClear = rig.ownership.debugDisabledLegacyOwnerCount;
                rig.ownership.ClearFault();

                Record(
                    rig.ownership.state == MavPhysicsOwnershipState.Fault,
                    "ClearFault cannot reinterpret an unresolved restoration failure as an Unowned bench rig",
                    report, ref passed, ref failed);

                Record(
                    rig.ownership.debugDisabledLegacyOwnerCount == debtBeforeClear
                    && debtBeforeClear > 0,
                    "the failed restoration obligation remains sticky for diagnosis/retry (debt="
                    + rig.ownership.debugDisabledLegacyOwnerCount + ")",
                    report, ref passed, ref failed);
            }
            finally
            {
                rig.Destroy();
            }
        }

        private static void ValidateInitiallyUnownedRig(
            StringBuilder report,
            ref int passed,
            ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[H2] Initially-Unowned is exact, not LegacyOwned-or-Unowned");

            HardeningRig rig = HardeningRig.Build(legacyOwnerCount: 0, withOwnershipController: true);
            try
            {
                rig.SetFlightCondition(3000f, 200f);
                rig.MakeOperationallyLiveReady();
                rig.ownership.RequestReturnToLegacy();
                rig.Step();

                Record(
                    rig.ownership.state == MavPhysicsOwnershipState.Unowned,
                    "a rig that never had a legacy owner remains exactly Unowned ("
                    + rig.ownership.state + ")",
                    report, ref passed, ref failed);
            }
            finally
            {
                rig.Destroy();
            }
        }

        private static void ValidateSpecificForceProductionInvocation(
            StringBuilder report,
            ref int passed,
            ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[H3] StepPhysics actually publishes summed specific force");

            HardeningRig rig = HardeningRig.Build(legacyOwnerCount: 0, withOwnershipController: false);
            try
            {
                rig.SetFlightCondition(1000f, 200f);
                rig.body.requireOperationalReadinessForLoadApplication = false;
                rig.body.simulationEnabled = true;
                rig.commandSource.command = MavPilotCommand.Neutral;

                rig.Step();

                float expectedNz = -rig.body.debugLoadSet.totalForceAeroBodyN.z
                                   / (rig.rigidbody.mass * G0);

                Record(
                    rig.body.debugState.specificForceValid,
                    "the real body step publishes a valid specific-force measurement",
                    report, ref passed, ref failed);

                Record(
                    Mathf.Abs(rig.body.debugState.LoadFactorNz - expectedNz) < 1e-3f,
                    "published Nz equals the actual summed non-gravitational load divided by mass ("
                    + rig.body.debugState.LoadFactorNz.ToString("F4") + " vs "
                    + expectedNz.ToString("F4") + ")",
                    report, ref passed, ref failed);
            }
            finally
            {
                rig.Destroy();
            }
        }

        private static void ValidatePhysicsStepSurface(
            StringBuilder report,
            ref int passed,
            ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[H4] Runtime physics step token is not caller-controlled");

            MethodInfo oldPublic = typeof(MavSixDoFBody).GetMethod(
                "StepPhysics",
                BindingFlags.Public | BindingFlags.Instance);
            MethodInfo editorSeam = typeof(MavSixDoFBody).GetMethod(
                "StepPhysicsForValidation",
                BindingFlags.Public | BindingFlags.Instance);

            Record(
                oldPublic == null,
                "MavSixDoFBody no longer exposes public StepPhysics(dt, callerFixedTime)",
                report, ref passed, ref failed);

            Record(
                editorSeam != null,
                "the deterministic manual step exists only as the explicitly named UNITY_EDITOR validation seam",
                report, ref passed, ref failed);
        }

        private sealed class HardeningRig
        {
            public GameObject gameObject;
            public Transform transform;
            public Rigidbody rigidbody;
            public MavSixDoFBody body;
            public MavF16FlightDynamicsProfile profile;
            public MavF16AeroModel aeroModel;
            public MavF16ControlActuator actuator;
            public MavManualPilotCommandSource commandSource;
            public MavF16ControlLawV01 controlLaw;
            public MavF16EnginePowerModel engine;
            public MavPhysicsOwnershipController ownership;
            public MavIntegrationTestLegacyOwner legacyA;
            public MavIntegrationTestLegacyOwner legacyB;

            private float stepTime;

            public static HardeningRig Build(int legacyOwnerCount, bool withOwnershipController)
            {
                HardeningRig rig = new HardeningRig();
                rig.gameObject = new GameObject("MavFreezeHardeningRig");
                rig.gameObject.hideFlags = HideFlags.HideAndDontSave;
                rig.transform = rig.gameObject.transform;

                rig.body = rig.gameObject.AddComponent<MavSixDoFBody>();
                rig.rigidbody = rig.gameObject.GetComponent<Rigidbody>();
                rig.rigidbody.useGravity = false;

                rig.profile = rig.gameObject.AddComponent<MavF16FlightDynamicsProfile>();
                rig.aeroModel = rig.gameObject.AddComponent<MavF16AeroModel>();
                rig.actuator = rig.gameObject.AddComponent<MavF16ControlActuator>();
                rig.commandSource = rig.gameObject.AddComponent<MavManualPilotCommandSource>();
                rig.controlLaw = rig.gameObject.AddComponent<MavF16ControlLawV01>();
                rig.engine = rig.gameObject.AddComponent<MavF16EnginePowerModel>();

                if (withOwnershipController)
                    rig.ownership = rig.gameObject.AddComponent<MavPhysicsOwnershipController>();

                if (legacyOwnerCount > 0)
                    rig.legacyA = rig.gameObject.AddComponent<MavIntegrationTestLegacyOwner>();
                if (legacyOwnerCount > 1)
                    rig.legacyB = rig.gameObject.AddComponent<MavIntegrationTestLegacyOwner>();

                rig.body.profileProvider = rig.profile;
                rig.body.aerodynamicModel = rig.aeroModel;
                rig.body.controlSurfaceActuator = rig.actuator;
                rig.body.controlLaw = rig.controlLaw;
                rig.body.pilotCommandSource = rig.commandSource;
                rig.body.propulsionModel = rig.engine;
                rig.body.simulationEnabled = false;

                rig.actuator.sixDoFBody = rig.body;
                rig.controlLaw.sixDoFBody = rig.body;
                rig.controlLaw.actuator = rig.actuator;
                rig.controlLaw.commandSource = rig.commandSource;
                rig.controlLaw.driveActuatorInFixedUpdate = true;
                rig.controlLaw.ResetLawState();

                rig.body.conflictingLegacyPhysicsComponents =
                    new string[] { MavIntegrationTestLegacyOwner.TypeName };

                if (rig.ownership != null)
                {
                    rig.ownership.sixDoFBody = rig.body;
                    rig.ownership.autoTransitionToNewFdm = false;
                    rig.ownership.legacyPhysicsOwners =
                        new string[] { MavIntegrationTestLegacyOwner.TypeName };
                    rig.ownership.state = legacyOwnerCount > 0
                        ? MavPhysicsOwnershipState.LegacyOwned
                        : MavPhysicsOwnershipState.Unowned;
                }

                rig.body.ApplyConfiguredProfile(true);
                rig.body.NotifyOwnershipChanged();
                return rig;
            }

            public void MakeOperationallyLiveReady()
            {
                commandSource.treatAsOperationalSource = true;
                commandSource.commandAvailable = true;
                commandSource.command = MavPilotCommand.Neutral;
                body.acceptNonAuthoritativePropulsion = true;
            }

            public void SetFlightCondition(float altitudeM, float trueAirspeedMps)
            {
                transform.position = new Vector3(0f, altitudeM, 0f);
                transform.rotation = Quaternion.identity;
                rigidbody.linearVelocity = new Vector3(0f, 0f, trueAirspeedMps);
            }

            public void Step()
            {
                stepTime += Dt;
                controlLaw.StepControlLaw(Dt);

                if (ownership != null)
                    ownership.StepOwnership();

                actuator.StepActuator(Dt);
                body.StepPhysics(Dt, stepTime);

                if (legacyA != null)
                    legacyA.StepLegacyOwner();
                if (legacyB != null)
                    legacyB.StepLegacyOwner();
            }

            public void Destroy()
            {
                if (gameObject != null)
                    Object.DestroyImmediate(gameObject);
                gameObject = null;
            }
        }

        private static void Record(
            bool ok,
            string name,
            StringBuilder report,
            ref int passed,
            ref int failed)
        {
            if (ok)
            {
                passed++;
                report.Append("  PASS  ").AppendLine(name);
            }
            else
            {
                failed++;
                report.Append("  FAIL  ").AppendLine(name);
            }
        }
    }
}
#endif
