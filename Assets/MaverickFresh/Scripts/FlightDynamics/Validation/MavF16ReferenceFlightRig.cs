using System.Text;
using UnityEngine;
using MaverickFresh.FlightDynamics.F16;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>
    /// Phase 5C-R: the scripted command source for the isolated F-16 reference-flight experiment.
    ///
    /// THIS IS NOT AN FLCS. It is not a fly-by-wire system, it is not the F-16's flight control
    /// system, and it is not sourced from anything. It is a waveform generator: it emits normalised
    /// pitch/roll/yaw intent on a schedule so that a step or a doublet can be commanded
    /// reproducibly. Naming it after a real aircraft system would be a claim the implementation
    /// cannot support, so it is named after what it is.
    ///
    /// It reaches the aircraft only through the normal path
    ///
    ///     command source -> control law -> actuator -> aero -> MavSixDoFBody -> Rigidbody
    ///
    /// and it holds no Rigidbody reference of any kind. There is no AddForce, no AddTorque, no
    /// velocity write, no attitude stabilisation, no damping, no fake lift and no fake G in this
    /// file. That absence is the point of the experiment: if the aircraft responds, the response came
    /// from the Morelli aerodynamic model through the load-application boundary and from nowhere else.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MavF16ReferenceTestCommandSource : MavPilotCommandSourceBase
    {
        /// <summary>The name this source reports. Deliberately not an aircraft system name.</summary>
        public const string SourceName = "REFERENCE_TEST_COMMAND_SOURCE";

        [Header("Commanded intent (normalised, -1..1)")]
        public MavPilotCommand command = MavPilotCommand.Neutral;

        [Tooltip("Live signal state. False makes this source stop producing commands, which costs the stack its live readiness - used to test that path, never to fly.")]
        public bool commandAvailable = true;

        public override string CommandSourceName
        {
            get { return SourceName; }
        }

        /// <summary>
        /// Declares itself an operational command path.
        ///
        /// MavManualPilotCommandSource deliberately defaults this to FALSE, because a hand-driven
        /// inspector field is not an operational input path. This source says true for a narrower
        /// reason: a 5C-R scenario is a scripted, deterministic command schedule, and the readiness
        /// gate has to be satisfiable for the experiment to run at all. It is still a TEST path, and
        /// nothing here should ever be installed on a gameplay aircraft.
        /// </summary>
        public override bool IsOperationalCommandSource
        {
            get { return true; }
        }

        public override bool HasCommandSignal
        {
            get { return commandAvailable; }
        }

        public override MavCommandSignalLossPolicy SignalLossPolicy
        {
            get { return MavCommandSignalLossPolicy.NeutralCommand; }
        }

        public override bool TryGetCommand(out MavPilotCommand pilotCommand)
        {
            pilotCommand = command.Clamped();
            return commandAvailable;
        }

        /// <summary>Commands a constant normalised pitch/roll/yaw. Throttle stays at zero: 5C-R is unpowered.</summary>
        public void SetIntent(float pitch, float roll, float yaw)
        {
            command.pitch = Mathf.Clamp(pitch, -1f, 1f);
            command.roll = Mathf.Clamp(roll, -1f, 1f);
            command.yaw = Mathf.Clamp(yaw, -1f, 1f);
            command.throttle01 = 0f;
        }

        public void Neutral()
        {
            SetIntent(0f, 0f, 0f);
        }
    }

    /// <summary>
    /// One physics step of the isolated reference rig, recorded whole.
    ///
    /// A struct in a preallocated array rather than a log line, for two reasons: the physics loop must
    /// not allocate, and a defect in this phase is usually a RELATIONSHIP between channels - a sign
    /// that disagrees with a rate, an energy that rises when a drag should be taking it away - which
    /// can only be seen with every channel of the same step side by side.
    /// </summary>
    public struct MavF16ReferenceTelemetrySample
    {
        public float time;
        public Vector3 positionM;
        public Vector3 eulerDeg;
        public Vector3 worldVelocityMps;
        public Vector3 aeroBodyVelocityMps;
        public Vector3 worldAngularVelocityRadSec;
        public Vector3 bodyRatesRadSec;          // p, q, r about forward/right/down

        public float alphaDeg;
        public float betaDeg;
        public float mach;
        public float densityKgM3;
        public float dynamicPressurePa;

        public float requestedElevatorDeg;
        public float requestedAileronDeg;
        public float requestedRudderDeg;
        public float actualElevatorDeg;
        public float actualAileronDeg;
        public float actualRudderDeg;

        public float cx, cy, cz, cl, cm, cn;

        public Vector3 aeroForceAeroBodyN;
        public Vector3 aeroMomentAeroBodyNm;
        public Vector3 propulsionForceAeroBodyN;
        public Vector3 propulsionMomentAeroBodyNm;
        public float propulsionThrustN;

        public Vector3 totalForceAeroBodyN;
        public Vector3 totalMomentAeroBodyNm;

        // The moment split required by Phase 5C-R.1 section 8. Diagnostic only: these three are
        // recorded so the correction can be audited, and the sum is applied ONCE.
        public Vector3 externalMomentAeroBodyNm;
        public Vector3 gyroscopicMomentAeroBodyNm;
        public Vector3 finalAppliedMomentAeroBodyNm;

        public Vector3 appliedUnityLocalForceN;
        public Vector3 appliedUnityLocalTorqueNm;
        public float gravityAccelMps2;
        public Vector3 specificForceG;
        public float loadFactorNz;

        public float translationalKeJ;
        public float rotationalKeJ;
        public float potentialEnergyJ;
        public float totalMechanicalEnergyJ;

        public int legacyWriterCount;
        public int referenceWriterCount;
        public int gravityProviderCount;
        public bool loadApplied;
        public bool envelopeWithinReference;
        public int envelopeStatus;
    }

    /// <summary>
    /// Phase 5C-R telemetry recorder and per-step runtime assertion gate for the isolated rig.
    ///
    /// One component does both jobs on purpose: the assertions are statements about the very values
    /// being recorded, and splitting them would let a run report "assertions passed" against numbers
    /// nobody kept. Execution order is after MavSixDoFBody (-100) so every value it reads is this
    /// step's, not last step's.
    ///
    /// The energy accounting is deliberately complete - translational KE, rotational KE and
    /// gravitational PE - because an incomplete total would show energy appearing and disappearing as
    /// the aircraft traded rotation for translation, and that artefact is indistinguishable from the
    /// hidden speed assist this phase is looking for.
    /// </summary>
    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
    public sealed class MavF16ReferenceTelemetryRecorder : MonoBehaviour
    {
        public const float StandardGravityMps2 = 9.80665f;

        [Header("Wiring")]
        public MavSixDoFBody body;
        public MavF16ControlActuator actuator;
        public MavF16ReferenceTestCommandSource commandSource;
        public MavFlightControlLawBase controlLaw;
        public MavFlightPhysicsOwnership ownership;

        [Header("Capacity")]
        [Tooltip("Preallocated sample capacity. The physics loop must not allocate, so recording stops when this is full rather than growing.")]
        public int capacity = 4000;

        [Header("Read-only")]
        public int sampleCount;
        public bool recording;
        public int assertionFailures;
        public string firstAssertionFailure = "";

        private MavF16ReferenceTelemetrySample[] samples;
        private Rigidbody rb;
        private MavInertiaMatrix aeroBodyInertia;
        private bool inertiaBuilt;

        // Assertion expectations, established by the rig builder rather than assumed here.
        [Header("Assertion expectations")]
        public float expectedMassKg = MavF16MassReference.MassKg;
        public bool expectCgDatumDeclared = true;
        public bool assertEnvelope = true;

        public MavF16ReferenceTelemetrySample[] Samples
        {
            get { return samples; }
        }

        private void Awake()
        {
            samples = new MavF16ReferenceTelemetrySample[Mathf.Max(16, capacity)];
            rb = GetComponent<Rigidbody>();
            aeroBodyInertia = MavInertiaTensorMath.BuildAeroBodyInertia(
                MavF16MassReference.IxKgM2,
                MavF16MassReference.IyKgM2,
                MavF16MassReference.IzKgM2,
                MavF16MassReference.IxzKgM2);
            inertiaBuilt = true;
        }

        public void BeginRecording()
        {
            sampleCount = 0;
            assertionFailures = 0;
            firstAssertionFailure = "";
            recording = true;
        }

        public void StopRecording()
        {
            recording = false;
        }

        private void FixedUpdate()
        {
            if (!recording || body == null || rb == null || sampleCount >= samples.Length)
                return;

            MavF16ReferenceTelemetrySample s = new MavF16ReferenceTelemetrySample();
            MavFlightState st = body.debugState;

            s.time = Time.fixedTime;
            s.positionM = transform.position;
            s.eulerDeg = transform.rotation.eulerAngles;
            s.worldVelocityMps = rb.linearVelocity;
            s.aeroBodyVelocityMps = st.aeroBodyVelocityMps;
            s.worldAngularVelocityRadSec = rb.angularVelocity;
            s.bodyRatesRadSec = st.aeroBodyRatesRadSec;

            s.alphaDeg = st.AlphaDeg;
            s.betaDeg = st.BetaDeg;
            s.mach = st.mach;
            s.densityKgM3 = body.debugAtmosphere.densityKgM3;
            s.dynamicPressurePa = st.dynamicPressurePa;

            if (controlLaw != null)
            {
                s.requestedElevatorDeg = actuator != null ? actuator.command.elevatorDeg : 0f;
                s.requestedAileronDeg = actuator != null ? actuator.command.aileronDeg : 0f;
                s.requestedRudderDeg = actuator != null ? actuator.command.rudderDeg : 0f;
            }

            if (actuator != null)
            {
                s.actualElevatorDeg = actuator.actual.elevatorDeg;
                s.actualAileronDeg = actuator.actual.aileronDeg;
                s.actualRudderDeg = actuator.actual.rudderDeg;
            }

            MavAeroCoefficients c = body.debugCoefficients;
            s.cx = c.cx; s.cy = c.cy; s.cz = c.cz;
            s.cl = c.cl; s.cm = c.cm; s.cn = c.cn;

            MavFlightDynamicsLoadSet set = body.debugLoadSet;
            s.aeroForceAeroBodyN = set.aerodynamic.forceAeroBodyN;
            s.aeroMomentAeroBodyNm = set.aerodynamic.momentAeroBodyNm;
            s.propulsionForceAeroBodyN = set.propulsive.forceAeroBodyN;
            s.propulsionMomentAeroBodyNm = set.propulsive.momentAeroBodyNm;
            s.propulsionThrustN = set.propulsive.reportedThrustN;
            s.totalForceAeroBodyN = set.totalForceAeroBodyN;
            s.totalMomentAeroBodyNm = set.totalMomentAeroBodyNm;
            s.externalMomentAeroBodyNm = set.ExternalMomentAeroBodyNm;
            s.gyroscopicMomentAeroBodyNm = set.inertialCorrectionMomentAeroBodyNm;
            s.finalAppliedMomentAeroBodyNm = set.totalMomentAeroBodyNm;
            s.appliedUnityLocalForceN = body.debugUnityLocalForceN;
            s.appliedUnityLocalTorqueNm = body.debugUnityLocalTorqueNm;
            s.loadApplied = set.applied;

            s.gravityAccelMps2 = rb.useGravity ? Mathf.Abs(Physics.gravity.y) : 0f;
            s.specificForceG = st.specificForceAeroBodyG;
            s.loadFactorNz = st.LoadFactorNz;

            // ENERGY. Rotational KE uses the full body-axis tensor including Ixz, evaluated on the
            // body rates: 0.5 * w^T I w. Using the Unity principal tensor with world rates would be a
            // different quantity, and the cross term is exactly what would go missing.
            float speed = rb.linearVelocity.magnitude;
            s.translationalKeJ = 0.5f * rb.mass * speed * speed;
            s.rotationalKeJ = inertiaBuilt
                ? 0.5f * Vector3.Dot(s.bodyRatesRadSec,
                    MavInertiaTensorMath.Multiply(aeroBodyInertia, s.bodyRatesRadSec))
                : 0f;
            s.potentialEnergyJ = rb.mass * StandardGravityMps2 * transform.position.y;
            s.totalMechanicalEnergyJ = s.translationalKeJ + s.rotationalKeJ + s.potentialEnergyJ;

            s.legacyWriterCount = CountLegacyPhysicalWriters();
            s.referenceWriterCount = body != null ? 1 : 0;
            s.gravityProviderCount = CountGravityProviders();

            MavF16ReferenceEnvelope env = BuildEnvelope(st);
            MavReferenceEnvelopeStatus status;
            string reason;
            s.envelopeWithinReference = env.IsWithinReferenceEnvelope(out status, out reason);
            s.envelopeStatus = (int)status;

            samples[sampleCount++] = s;

            RunRuntimeAssertions(s, reason);
        }

        /// <summary>
        /// The section-9 runtime assertions, every step.
        ///
        /// A violation is RECORDED as a failure. Nothing here repairs anything, and in particular
        /// nothing falls back to the legacy stack: an isolated experiment that quietly hands the
        /// aircraft back to legacy physics would report a pass for a flight the replacement model did
        /// not fly.
        /// </summary>
        private void RunRuntimeAssertions(MavF16ReferenceTelemetrySample s, string envelopeReason)
        {
            Fail(s.legacyWriterCount == 0,
                "legacy physical writer present on the isolated rig (" + s.legacyWriterCount + ")");
            Fail(s.referenceWriterCount == 1,
                "reference physical writer count is " + s.referenceWriterCount + ", expected 1");
            Fail(s.gravityProviderCount == 1,
                "gravity provider count is " + s.gravityProviderCount + ", expected exactly 1");
            Fail(Mathf.Abs(s.propulsionThrustN) < 1e-6f,
                "propulsive thrust is " + s.propulsionThrustN.ToString("F6")
                + " N; phase 5C-R is unpowered and no authoritative deck is frozen");
            Fail(s.propulsionForceAeroBodyN.sqrMagnitude < 1e-9f,
                "propulsive force is " + s.propulsionForceAeroBodyN + " N, expected exactly zero");
            Fail(body.debugProfileValid,
                "reference profile is not valid: " + body.debugProfileStatus);
            Fail(Mathf.Abs(rb.mass - expectedMassKg) < 0.5f,
                "mass is " + rb.mass.ToString("F2") + " kg, expected "
                + expectedMassKg.ToString("F2"));
            Fail(expectCgDatumDeclared, "CG datum is not declared");
            Fail(rb.inertiaTensor.x > 0f && rb.inertiaTensor.y > 0f && rb.inertiaTensor.z > 0f,
                "inertia tensor is not positive definite: " + rb.inertiaTensor);
            Fail(Finite(s.worldVelocityMps) && Finite(s.worldAngularVelocityRadSec)
                 && Finite(s.totalForceAeroBodyN) && Finite(s.totalMomentAeroBodyNm),
                "state or load set is not finite");

            // The moment split must add up on EVERY step, not only in the scenario that inspects it.
            // A correction applied twice, or applied without being reported, would show here.
            Vector3 sum = s.externalMomentAeroBodyNm + s.gyroscopicMomentAeroBodyNm;
            float momentScale = Mathf.Max(1f, s.finalAppliedMomentAeroBodyNm.magnitude);
            Fail((s.finalAppliedMomentAeroBodyNm - sum).magnitude / momentScale < 1e-4f,
                "the applied moment is not external + gyroscopic ("
                + s.finalAppliedMomentAeroBodyNm + " vs " + sum + ")");
            Fail(body.debugLoadSet.inertialContributions <= 1,
                "the inertial correction was contributed more than once this step ("
                + body.debugLoadSet.inertialContributions + ")");

            if (assertEnvelope)
                Fail(s.envelopeWithinReference, "outside the reference envelope: " + envelopeReason);
        }

        private void Fail(bool condition, string message)
        {
            if (condition)
                return;

            assertionFailures++;
            if (string.IsNullOrEmpty(firstAssertionFailure))
                firstAssertionFailure = "step " + sampleCount + ": " + message;
        }

        /// <summary>
        /// Counts legacy physical writers actually present on THIS rig.
        ///
        /// By type, against the same deny list MavSixDoFBody uses, so the isolated rig's cleanliness
        /// is measured rather than asserted from the fact that nobody added one on purpose.
        /// </summary>
        public int CountLegacyPhysicalWriters()
        {
            int count = 0;
            MonoBehaviour[] all = GetComponents<MonoBehaviour>();
            string[] deny = MavSixDoFBody.DefaultConflictingLegacyPhysicsComponents;

            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null || !all[i].enabled)
                    continue;

                string typeName = all[i].GetType().Name;
                for (int d = 0; d < deny.Length; d++)
                {
                    if (typeName == deny[d])
                    {
                        count++;
                        break;
                    }
                }
            }

            return count;
        }

        /// <summary>
        /// Counts live gravity sources: Unity's own integrator plus any legacy custom-gravity writer.
        ///
        /// Two is the failure this exists to catch - a Rigidbody with useGravity on AND a component
        /// adding mg by hand, which is exactly the state MavAeroBody and useGravity would produce
        /// together.
        /// </summary>
        public int CountGravityProviders()
        {
            int count = rb != null && rb.useGravity ? 1 : 0;

            MonoBehaviour[] all = GetComponents<MonoBehaviour>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null || !all[i].enabled)
                    continue;

                // MavAeroBody is the only legacy component that adds its own mg. Detected by name so
                // this file carries no dependency on the legacy aero stack.
                if (all[i].GetType().Name == "MavAeroBody")
                    count++;
            }

            return count;
        }

        private MavF16ReferenceEnvelope BuildEnvelope(MavFlightState st)
        {
            MavF16ReferenceEnvelope e = new MavF16ReferenceEnvelope();

            // Identity is established BY CONSTRUCTION here: the rig is assembled from
            // MavF16FlightDynamicsProfile, MavF16AeroModel and MavF16MassReference by the builder. It
            // is not resolved from MavAircraftProfileApplier, because that is a gameplay component and
            // an isolated rig deliberately has none. Recorded as such in the report.
            e.aircraftIdentityResolvedAsF16C = true;
            e.cleanReferenceConfiguration = true;
            e.gearUp = true;
            e.noExternalStores = true;
            e.propulsionIntentionallyDisabled = true;

            e.massAndInertiaValid = Mathf.Abs(rb.mass - expectedMassKg) < 0.5f
                                    && rb.inertiaTensor.x > 0f;
            e.cgMappingValidated = expectCgDatumDeclared;
            e.gravityOwnerValid = ownership != null
                ? MavFlightPhysicsOwnership.HasExactlyOneGravitySource(ownership.owner)
                : CountGravityProviders() == 1;
            e.allReplacementStateFinite = Finite(rb.linearVelocity) && Finite(rb.angularVelocity);
            e.legacyPhysicalWritersDisabledAtomically = CountLegacyPhysicalWriters() == 0;

            e.SetReferenceFlightCondition(
                st.trueAirspeedMps, transform.position.y, st.AlphaDeg, st.BetaDeg);

            return e;
        }

        private static bool Finite(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z)
                   && !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
        }

        /// <summary>CSV of every recorded channel, for offline inspection of a run.</summary>
        public string ToCsv()
        {
            StringBuilder sb = new StringBuilder(sampleCount * 320 + 1024);
            sb.AppendLine(
                "t,px,py,pz,eulerX,eulerY,eulerZ,vwx,vwy,vwz,ubody,vbody,wbody,"
                + "wx,wy,wz,p,q,r,alphaDeg,betaDeg,mach,rho,qbar,"
                + "reqElev,reqAil,reqRud,actElev,actAil,actRud,"
                + "CX,CY,CZ,Cl,Cm,Cn,"
                + "aeroFx,aeroFy,aeroFz,aeroL,aeroM,aeroN,"
                + "propFx,propFy,propFz,propThrustN,"
                + "totFx,totFy,totFz,totL,totM,totN,"
                + "extL,extM,extN,gyroL,gyroM,gyroN,finalL,finalM,finalN,"
                + "appliedUFx,appliedUFy,appliedUFz,appliedUTx,appliedUTy,appliedUTz,"
                + "gravAccel,nxG,nyG,nzG,Nz,"
                + "keTrans,keRot,pe,eTotal,"
                + "legacyWriters,refWriters,gravityProviders,applied,envelopeOk,envelopeStatus");

            for (int i = 0; i < sampleCount; i++)
            {
                MavF16ReferenceTelemetrySample s = samples[i];
                sb.Append(s.time.ToString("F4")).Append(',')
                  .Append(V(s.positionM)).Append(',')
                  .Append(V(s.eulerDeg)).Append(',')
                  .Append(V(s.worldVelocityMps)).Append(',')
                  .Append(V(s.aeroBodyVelocityMps)).Append(',')
                  .Append(V(s.worldAngularVelocityRadSec)).Append(',')
                  .Append(V(s.bodyRatesRadSec)).Append(',')
                  .Append(s.alphaDeg.ToString("F4")).Append(',')
                  .Append(s.betaDeg.ToString("F4")).Append(',')
                  .Append(s.mach.ToString("F5")).Append(',')
                  .Append(s.densityKgM3.ToString("F5")).Append(',')
                  .Append(s.dynamicPressurePa.ToString("F2")).Append(',')
                  .Append(s.requestedElevatorDeg.ToString("F4")).Append(',')
                  .Append(s.requestedAileronDeg.ToString("F4")).Append(',')
                  .Append(s.requestedRudderDeg.ToString("F4")).Append(',')
                  .Append(s.actualElevatorDeg.ToString("F4")).Append(',')
                  .Append(s.actualAileronDeg.ToString("F4")).Append(',')
                  .Append(s.actualRudderDeg.ToString("F4")).Append(',')
                  .Append(s.cx.ToString("F6")).Append(',')
                  .Append(s.cy.ToString("F6")).Append(',')
                  .Append(s.cz.ToString("F6")).Append(',')
                  .Append(s.cl.ToString("F6")).Append(',')
                  .Append(s.cm.ToString("F6")).Append(',')
                  .Append(s.cn.ToString("F6")).Append(',')
                  .Append(V(s.aeroForceAeroBodyN)).Append(',')
                  .Append(V(s.aeroMomentAeroBodyNm)).Append(',')
                  .Append(V(s.propulsionForceAeroBodyN)).Append(',')
                  .Append(s.propulsionThrustN.ToString("F4")).Append(',')
                  .Append(V(s.totalForceAeroBodyN)).Append(',')
                  .Append(V(s.totalMomentAeroBodyNm)).Append(',')
                  .Append(V(s.externalMomentAeroBodyNm)).Append(',')
                  .Append(V(s.gyroscopicMomentAeroBodyNm)).Append(',')
                  .Append(V(s.finalAppliedMomentAeroBodyNm)).Append(',')
                  .Append(V(s.appliedUnityLocalForceN)).Append(',')
                  .Append(V(s.appliedUnityLocalTorqueNm)).Append(',')
                  .Append(s.gravityAccelMps2.ToString("F4")).Append(',')
                  .Append(V(s.specificForceG)).Append(',')
                  .Append(s.loadFactorNz.ToString("F4")).Append(',')
                  .Append(s.translationalKeJ.ToString("F1")).Append(',')
                  .Append(s.rotationalKeJ.ToString("F1")).Append(',')
                  .Append(s.potentialEnergyJ.ToString("F1")).Append(',')
                  .Append(s.totalMechanicalEnergyJ.ToString("F1")).Append(',')
                  .Append(s.legacyWriterCount).Append(',')
                  .Append(s.referenceWriterCount).Append(',')
                  .Append(s.gravityProviderCount).Append(',')
                  .Append(s.loadApplied ? 1 : 0).Append(',')
                  .Append(s.envelopeWithinReference ? 1 : 0).Append(',')
                  .Append(s.envelopeStatus)
                  .AppendLine();
            }

            return sb.ToString();
        }

        private static string V(Vector3 v)
        {
            return v.x.ToString("F5") + "," + v.y.ToString("F5") + "," + v.z.ToString("F5");
        }
    }
}
