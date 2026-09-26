#if UNITY_EDITOR
using System.Text;
using UnityEngine;
using MaverickFresh.FlightDynamics;
using MaverickFresh.FlightDynamics.F15;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>
    /// Real-component smoke test of the F-15 RESEARCH configuration through the production
    /// <see cref="MavSixDoFBody"/> step.
    ///
    /// NOT a PlayMode test. It builds the aircraft on a HideAndDontSave GameObject - no scene or
    /// prefab is opened, modified or saved - and advances it through the editor-only
    /// <c>StepPhysicsForValidation</c> seam, which runs the same <c>StepPhysicsCore</c> that
    /// FixedUpdate runs. Unity does not integrate the Rigidbody in edit mode, so this checks the
    /// pipeline, not a trajectory.
    ///
    /// Lives under Validation/ and compiles only in the editor. The rig assigns an initial
    /// Rigidbody velocity to establish the flight condition; the ownership scan treats
    /// Validation/ as validation-only, so no new exemption was added to it.
    ///
    /// Checks only: finite state, finite forces and moments, no NaN/Inf, profile identity, the
    /// source envelope, structural (not live) readiness, and that the FCS limitation is exposed.
    /// No handling or performance claim is made.
    ///
    ///   [Q1] research stack at the source condition
    ///   [Q2] the same stack away from the source condition refuses, and stays finite
    ///   [Q3] the same thrust under the exact NASA 836 profile produces nothing
    /// </summary>
    public static class MavF15ResearchPipelineValidation
    {
        private const float FixedDeltaTime = 0.02f;

        public static string RunAll(out int passed, out int failed)
        {
            passed = 0;
            failed = 0;
            StringBuilder report = new StringBuilder(4096);
            report.AppendLine("F-15 Research Pipeline Smoke (editor seam, NOT PlayMode) - "
                + MavF15AfitResearchIdentity.ConfigurationId);
            report.AppendLine("======================================================================");

            ValidateAtSourceCondition(report, ref passed, ref failed);
            ValidateAwayFromSourceCondition(report, ref passed, ref failed);
            ValidateUnderExactProfile(report, ref passed, ref failed);

            report.AppendLine();
            report.Append("RESULT: ")
                .Append(failed == 0 ? "PASS" : "FAIL")
                .Append("  passed=").Append(passed)
                .Append(" failed=").Append(failed);
            return report.ToString();
        }

        // ================================================================= [Q1]

        private static void ValidateAtSourceCondition(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[Q1] Research stack at " + MavF15AfitResearchIdentity.SourceConditionLabel);

            Rig rig = Rig.Build();
            try
            {
                rig.SetSourceCondition(MavF15BaumannMach06Reference.SourcePressureAltitudeM);
                rig.Arm();
                for (int i = 0; i < 5; i++)
                    rig.Step();

                MavSixDoFBody body = rig.body;

                Record(body.debugProfileValid
                        && body.debugProfileStatus.StartsWith(MavF15AfitResearchIdentity.ConfigurationId),
                    "the body runs the research profile: " + body.debugProfileStatus,
                    report, ref passed, ref failed);

                Record(body.debugReadiness.structurallyPrepared
                        && !body.debugReadiness.operationallyLiveReady,
                    "generic readiness: STRUCTURALLY_PREPARED, not live-ready - "
                    + body.debugReadiness.summary,
                    report, ref passed, ref failed);

                MavFlightState s = body.debugState;
                Record(Finite(s.worldPositionM) && Finite(s.worldVelocityMps)
                        && Finite(s.aeroBodyRatesRadSec) && Finite(s.mach) && Finite(s.alphaRad)
                        && Mathf.Abs(s.mach - MavF15BaumannMach06Reference.SourceMach) < 1e-3f
                        && body.debugInsideProfileEnvelope,
                    "state finite and inside the research envelope (M="
                    + s.mach.ToString("F4") + ", alpha=" + s.AlphaDeg.ToString("F2") + " deg)",
                    report, ref passed, ref failed);

                Record(!rig.aero.debugRefused
                        && rig.aero.debugStatus.Contains(MavF15BaumannMach06Reference.ModelId),
                    "research aero evaluates and labels itself CROSS_VALIDATION: " + rig.aero.debugStatus,
                    report, ref passed, ref failed);

                MavFlightDynamicsLoadSet set = body.debugLoadSet;
                Record(set.IsFinite() && Finite(set.totalForceAeroBodyN)
                        && Finite(set.totalMomentAeroBodyNm)
                        && set.aerodynamicContributions == 1 && set.propulsiveContributions == 1,
                    "summed loads finite, aero and propulsion once each: force "
                    + set.totalForceAeroBodyN.ToString("F1") + " N, moment "
                    + set.totalMomentAeroBodyNm.ToString("F1") + " N m",
                    report, ref passed, ref failed);

                Record(set.propulsive.forceAeroBodyN.x == MavF15AfitResearchThrustSource.TotalThrustN
                        && Mathf.Abs(set.propulsive.momentAeroBodyNm.y
                            - MavF15AfitResearchThrustSource.ThrustLinePitchingMomentNm) < 1e-3f
                        && !set.propulsive.hasAuthoritativeData
                        && !rig.thrust.debugRefused,
                    "research thrust reached the body: "
                    + set.propulsive.forceAeroBodyN.x.ToString("F1") + " N along +X, "
                    + set.propulsive.momentAeroBodyNm.y.ToString("F2") + " N m nose-up, "
                    + "non-authoritative",
                    report, ref passed, ref failed);

                // The FCS limitation, exposed rather than bypassed.
                MavControlInput actual = rig.actuator.ActualSurfaceState;
                Record(rig.law.debugAppliedStageCount == 0
                        && rig.actuator.debugAvailableChannelCount == 0
                        && actual.elevatorDeg == 0f && actual.aileronDeg == 0f
                        && actual.rudderDeg == 0f,
                    "FCS limitation exposed: 0 law stages applied, 0 actuator channels available, "
                    + "surfaces neutral - the research aircraft is structurally flyable but "
                    + "uncontrolled (" + rig.actuator.debugStatus + ")",
                    report, ref passed, ref failed);

                Record(rig.thrust.debugStatus.Contains("throttle ignored"),
                    "the ignored throttle is reported: " + rig.thrust.debugStatus,
                    report, ref passed, ref failed);
            }
            finally
            {
                rig.Destroy();
            }
        }

        // ================================================================= [Q2]

        private static void ValidateAwayFromSourceCondition(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[Q2] Away from the source condition the research stack refuses");

            Rig rig = Rig.Build();
            try
            {
                rig.SetSourceCondition(5000f);
                rig.Arm();
                rig.Step();

                MavFlightDynamicsLoadSet set = rig.body.debugLoadSet;
                Record(rig.aero.debugRefused && rig.thrust.debugRefused,
                    "at 5,000 m both research aero and research thrust refuse: "
                    + rig.aero.debugStatus,
                    report, ref passed, ref failed);

                Record(set.IsFinite() && set.aerodynamic.forceAeroBodyN == Vector3.zero
                        && set.propulsive.forceAeroBodyN == Vector3.zero,
                    "and the load set stays finite with zero aero and zero thrust - nothing is "
                    + "extrapolated",
                    report, ref passed, ref failed);
            }
            finally
            {
                rig.Destroy();
            }
        }

        // ================================================================= [Q3]

        private static void ValidateUnderExactProfile(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[Q3] Research thrust under the exact NASA 836 profile");

            Rig rig = Rig.Build();
            try
            {
                MavF15FlightDynamicsProfile exact =
                    rig.gameObject.AddComponent<MavF15FlightDynamicsProfile>();
                rig.body.profileProvider = exact;
                rig.body.ApplyConfiguredProfile(true);

                rig.SetSourceCondition(MavF15BaumannMach06Reference.SourcePressureAltitudeM);
                rig.Arm();
                rig.Step();

                Record(!rig.body.debugProfileValid
                        && !rig.body.debugReadiness.structurallyPrepared,
                    "the body with the exact profile is NOT prepared: "
                    + rig.body.debugProfileStatus,
                    report, ref passed, ref failed);

                MavFlightDynamicsLoadSet set = rig.body.debugLoadSet;
                Record(set.totalForceAeroBodyN == Vector3.zero
                        && set.totalMomentAeroBodyNm == Vector3.zero
                        && set.aerodynamicContributions == 0 && set.propulsiveContributions == 0,
                    "armed identically, the load gate refuses it: no aero, no thrust, no loads "
                    + "handed over",
                    report, ref passed, ref failed);

                MavFlightState at;
                MavAtmosphereSample atmosphere;
                MavF15ResearchContaminationValidation.SourceCondition(0f, out at, out atmosphere);
                MavPropulsiveLoads loads = rig.thrust.Evaluate(at, atmosphere, 1f, FixedDeltaTime);
                Record(loads.forceAeroBodyN == Vector3.zero && rig.thrust.debugRefused,
                    "and the research thrust component on it refuses: " + rig.thrust.debugStatus,
                    report, ref passed, ref failed);
            }
            finally
            {
                rig.Destroy();
            }
        }

        // ================================================================= rig

        private sealed class Rig
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
            private float time;

            public static Rig Build()
            {
                Rig rig = new Rig();
                rig.gameObject = new GameObject("MavF15ResearchPipelineRig");
                rig.gameObject.hideFlags = HideFlags.HideAndDontSave;

                rig.body = rig.gameObject.AddComponent<MavSixDoFBody>();
                rig.rigidbody = rig.gameObject.GetComponent<Rigidbody>();
                rig.rigidbody.useGravity = false;

                rig.profile = rig.gameObject.AddComponent<MavF15AfitResearchFlightDynamicsProfile>();

                rig.aero = rig.gameObject.AddComponent<MavF15AeroModel>();
                rig.aero.sourceMode = MavF15AeroSourceMode.BaumannMach06SixAxisResearch;
                rig.aero.allowCrossValidationResearchModel = true;

                rig.actuator = rig.gameObject.AddComponent<MavF15ControlActuator>();
                rig.command = rig.gameObject.AddComponent<MavManualPilotCommandSource>();
                rig.law = rig.gameObject.AddComponent<MavF15ControlLaw>();
                rig.law.mode = MavF15FcsMode.AFITResearch;
                rig.thrust = rig.gameObject.AddComponent<MavF15AfitResearchFixedThrust>();

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

            public void SetSourceCondition(float altitudeM)
            {
                MavAtmosphereSample atmosphere =
                    MavAtmosphereModel.Sample(MavF15BaumannMach06Reference.SourcePressureAltitudeM);
                float speed = MavF15BaumannMach06Reference.SourceMach * atmosphere.speedOfSoundMps;

                gameObject.transform.position = new Vector3(0f, altitudeM, 0f);
                gameObject.transform.rotation = Quaternion.identity;
                rigidbody.linearVelocity = new Vector3(0f, 0f, speed);
                rigidbody.angularVelocity = Vector3.zero;
            }

            /// <summary>
            /// Arms the hidden temporary body through the body's own sanctioned override,
            /// allowStructuralOnlyLoadApplication: loads are handed over ONLY once generic
            /// structural readiness is actually reached, and the body logs that operational
            /// readiness is not met. requireOperationalReadinessForLoadApplication stays at its
            /// runtime default (true), so an unprepared stack - such as one on the exact profile -
            /// is still refused by the gate rather than by the test.
            /// </summary>
            public void Arm()
            {
                body.simulationEnabled = true;
                body.allowStructuralOnlyLoadApplication = true;
            }

            public void Step()
            {
                time += FixedDeltaTime;
                law.StepControlLaw(FixedDeltaTime);
                actuator.StepActuator(FixedDeltaTime);
                body.StepPhysicsForValidation(FixedDeltaTime, time);
            }

            public void Destroy()
            {
                if (gameObject != null)
                    Object.DestroyImmediate(gameObject);
                gameObject = null;
            }
        }

        // ================================================================= helpers

        private static bool Finite(float v)
        {
            return !float.IsNaN(v) && !float.IsInfinity(v);
        }

        private static bool Finite(Vector3 v)
        {
            return Finite(v.x) && Finite(v.y) && Finite(v.z);
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
#endif
