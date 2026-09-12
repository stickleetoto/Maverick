using System.Globalization;
using System.Text;
using UnityEngine;
using MaverickFresh.FlightDynamics;
using MaverickFresh.FlightDynamics.F16;

namespace MaverickFresh
{
    /// <summary>
    /// Phase 5B.5 force and moment accounting: WHO accelerated the aircraft this step?
    ///
    /// WHY THIS EXISTS. Phase 5A proved every per-step writer consults the ownership gate, and Phase
    /// 5B measured turn ownership. Neither answers the plainest question you can ask of a flight
    /// model: at this instant, which contribution produced the acceleration, and do the parts add up
    /// to the whole? Without that, a defect like the zero-gravity replacement mode is invisible until
    /// somebody flies into it, and a reading like "-9.9 g" cannot be attributed to a cause.
    ///
    /// WHAT IT DOES NOT DO. It writes no force, no torque and no velocity. It reads the debug channels
    /// the writers already publish, dimensionalises the Morelli shadow alongside them, and reports.
    /// It is not on the HUD; it is an inspector panel and a text report.
    ///
    /// HONESTY ABOUT COMPLETENESS. Every channel below is either a value a writer published or a
    /// value derived here from published values. Where a subsystem is absent, the channel says so
    /// rather than reporting zero, because zero and "not measured" are different claims and confusing
    /// them is how an accounting report starts lying.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(600)]
    public class MavForceAccountingDiagnostics : MonoBehaviour
    {
        [Header("Wiring (resolved at runtime)")]
        public Rigidbody body;
        public MavAeroBody aeroBody;
        public MavMouseFlightJet jet;
        public MavInstructorController instructor;
        public MavFlightPhysicsOwnership ownership;
        public MavSixDoFBody sixDoF;

        [Header("Ownership")]
        public string owner = "unresolved";
        public string gravityProvider = "unresolved";

        [Header("Mass / State")]
        public float massKg;
        public Vector3 worldVelocityMps;
        public float trueAirspeedMps;
        public Vector3 bodyAngularRateDegSec;
        public float alphaDeg;
        public float betaDeg;
        public float mach;
        public float airDensityKgM3;
        public float dynamicPressurePa;

        [Header("Who Applied What - LEGACY (newtons / newton-metres)")]
        [Tooltip("m * g * gravityBlend, as MavAeroBody published it. Zero when the aero body is not the gravity provider.")]
        public Vector3 gravityForceN;
        [Tooltip("Lift + drag from MavAeroBody.")]
        public Vector3 aerodynamicForceN;
        [Tooltip("Aerodynamic static-stability restoring moment, converted from the angular acceleration the aero body published.")]
        public Vector3 aerodynamicMomentNm;
        [Tooltip("Legacy engine thrust along body forward.")]
        public Vector3 legacyThrustN;
        [Tooltip("Velocity-turn assist plus alignment assist, i.e. the non-aerodynamic help.")]
        public Vector3 legacyAssistForceN;
        public Vector3 legacyAssistTorqueNm;
        [Tooltip("Gear / brake / speedbrake drag, if such a component is present. Reports 'absent' rather than zero when it is not.")]
        public string brakeAndGearDrag = "absent";

        [Header("Who Applied What - REPLACEMENT (shadow, never applied)")]
        public Vector3 replacementTotalForceN;
        public Vector3 replacementTotalMomentNm;
        public Vector3 replacementThrustN;
        public float cx, cy, cz;
        public float cl, cm, cn;
        public string surfaceRequested = "absent";
        public string surfaceActual = "absent";

        [Header("Derived")]
        [Tooltip("Sum of every legacy contribution this component can see, divided by mass.")]
        public Vector3 accountedAccelerationMps2;

        // The six acceleration-like quantities, each named for what it IS. Defined once in
        // MavLoadFactorMath and only READ here. This project has already had three different things
        // called "G", and a Phase 5B.5 report that described a gravity-inclusive projection as a load
        // factor - so the names now carry the distinction instead of a comment carrying it.
        [Header("Acceleration semantics - see MavLoadFactorMath")]
        [Tooltip("dv/dt of the Rigidbody in world axes. INCLUDES the effect of gravity on the motion.")]
        public Vector3 debugWorldAcceleration;

        [Tooltip("Physics.gravity. Not a force the airframe feels.")]
        public Vector3 debugGravityAcceleration;

        [Tooltip("worldAcceleration minus gravity. What an accelerometer measures. Zero in free fall.")]
        public Vector3 debugSpecificForce;

        [Tooltip("Component of specific force along the aircraft normal axis, m/s^2.")]
        public float debugBodyNormalSpecificForce;

        [Tooltip("Normal load factor, g units. TP-1538 convention: positive along the NEGATIVE Z body axis, i.e. toward the canopy. Reads +1 in steady level flight and 0 in free fall.")]
        public float debugNz;

        [Tooltip("What MavInstructorController.gEstimate computes: the projection of WORLD acceleration on body up, in g. Gravity inclusive, so it reads 0 in level flight. It is NOT a load factor and is named for what it is.")]
        public float debugLegacyHudG;

        [Tooltip("debugLegacyHudG minus debugNz, g. About +1 upright, 0 when the body up axis is horizontal. Published so the offset is measured rather than assumed.")]
        public float debugLegacyHudGMinusNz;

        public float verticalAccelerationMps2;
        [Tooltip("Specific force resolved into body axes, g units: (right, up, forward). The up component is debugNz.")]
        public Vector3 specificForceBodyG;

        [Tooltip("MavInstructorController.gEstimate as the runtime reports it, for comparison against debugLegacyHudG computed here. A divergence means one of them is no longer the formula it claims.")]
        public float debugInstructorReportedG;
        [Tooltip("d/dt of (kinetic + potential) energy per unit mass, W/kg. Negative means the aircraft is losing energy.")]
        public float mechanicalEnergyRateWPerKg;

        [Header("Integrity")]
        [Tooltip("True when every channel above is finite. A false here invalidates the whole report.")]
        public bool allChannelsFinite = true;
        [TextArea(2, 5)] public string integrityNote = "not evaluated";

        private Vector3 previousVelocity;
        private bool hasPreviousVelocity;
        private float previousEnergyPerMass;
        private bool hasPreviousEnergy;

        private void Awake()
        {
            Resolve();
        }

        private void Resolve()
        {
            if (body == null) body = GetComponent<Rigidbody>();
            if (aeroBody == null) aeroBody = GetComponent<MavAeroBody>();
            if (jet == null) jet = GetComponent<MavMouseFlightJet>();
            if (instructor == null) instructor = GetComponent<MavInstructorController>();
            if (ownership == null) ownership = GetComponent<MavFlightPhysicsOwnership>();
            if (sixDoF == null) sixDoF = GetComponent<MavSixDoFBody>();
        }

        private void FixedUpdate()
        {
            Resolve();
            if (body == null)
            {
                integrityNote = "no Rigidbody: nothing to account for";
                return;
            }

            SampleOwnership();
            SampleState();
            SampleLegacy();
            SampleReplacement();
            Derive();
            CheckIntegrity();
        }

        private void SampleOwnership()
        {
            if (ownership == null)
            {
                owner = "no ownership authority present";
                gravityProvider = "unknown";
                return;
            }

            owner = ownership.owner.ToString();
            gravityProvider = ownership.debugGravityProvider.ToString()
                              + (body.useGravity ? " (Rigidbody.useGravity ON)" : " (useGravity OFF)");
        }

        private void SampleState()
        {
            massKg = body.mass;
            worldVelocityMps = body.linearVelocity;
            trueAirspeedMps = worldVelocityMps.magnitude;
            bodyAngularRateDegSec =
                transform.InverseTransformDirection(body.angularVelocity) * Mathf.Rad2Deg;

            if (aeroBody != null)
            {
                alphaDeg = aeroBody.debugAoADeg;
                betaDeg = aeroBody.debugAoSDeg;
                airDensityKgM3 = aeroBody.debugAirDensity;
                dynamicPressurePa = aeroBody.debugDynamicPressure;
            }

            // Speed of sound is not modelled by the legacy stack; the jet divides by a constant 343.
            // Reported as the runtime computes it rather than silently improved here.
            mach = jet != null ? jet.machEstimate : trueAirspeedMps / 343f;
        }

        private void SampleLegacy()
        {
            gravityForceN = Vector3.zero;
            aerodynamicForceN = Vector3.zero;
            aerodynamicMomentNm = Vector3.zero;

            if (aeroBody != null)
            {
                gravityForceN = aeroBody.debugGravityForce;
                aerodynamicForceN = aeroBody.debugLiftForce + aeroBody.debugDragForce;

                // The aero body publishes static stability as an ANGULAR ACCELERATION in rad/s^2 about
                // Unity local axes. Turning it into a moment needs the inertia tensor, so this is
                // reported as the product with the tensor diagonal - an approximation that ignores the
                // off-diagonal Ixz term, and is labelled as such rather than presented as exact.
                Vector3 alphaDot = aeroBody.debugStaticStabilityAccel;
                Vector3 inertia = body.inertiaTensor;
                aerodynamicMomentNm = new Vector3(
                    alphaDot.x * inertia.x, alphaDot.y * inertia.y, alphaDot.z * inertia.z);
            }

            legacyThrustN = Vector3.zero;
            if (jet != null)
            {
                float thrustMag = jet.thrust * jet.effectiveThrottle
                                  * jet.debugEngineThrustScale * jet.forceMult;
                legacyThrustN = transform.forward * thrustMag;
            }

            legacyAssistForceN = Vector3.zero;
            legacyAssistTorqueNm = Vector3.zero;
            if (jet != null)
            {
                // The velocity-turn assist is published as an acceleration magnitude along the turn
                // direction; without the direction the honest representation is a magnitude along the
                // current lift axis, which is where it acts.
                legacyAssistForceN = transform.up * (jet.debugLegacyCurvatureAccel * massKg);

                Vector3 assistTorqueAccel = jet.debugFinalTorque + jet.debugRateControlTorque;
                Vector3 inertia = body.inertiaTensor;
                legacyAssistTorqueNm = new Vector3(
                    assistTorqueAccel.x * inertia.x,
                    assistTorqueAccel.y * inertia.y,
                    assistTorqueAccel.z * inertia.z);
            }

            MavLandingGearSystem gear = GetComponent<MavLandingGearSystem>();
            brakeAndGearDrag = gear != null
                ? "MavLandingGearSystem present - its drag is NOT gated by the ownership authority "
                  + "(scoped exemption, see Phase 5A writer categories)"
                : "absent on this aircraft";
        }

        private void SampleReplacement()
        {
            if (sixDoF == null)
            {
                replacementTotalForceN = Vector3.zero;
                replacementTotalMomentNm = Vector3.zero;
                replacementThrustN = Vector3.zero;
                surfaceRequested = "MavSixDoFBody absent: replacement stack is not in this scene";
                surfaceActual = surfaceRequested;
                cx = cy = cz = cl = cm = cn = 0f;
                return;
            }

            replacementTotalForceN = sixDoF.debugUnityLocalForceN;
            replacementTotalMomentNm = sixDoF.debugUnityLocalTorqueNm;

            MavAeroCoefficients c = sixDoF.debugCoefficients;
            cx = c.cx; cy = c.cy; cz = c.cz;
            cl = c.cl; cm = c.cm; cn = c.cn;

            MavF16ControlActuator actuator = GetComponent<MavF16ControlActuator>();
            if (actuator != null)
            {
                surfaceRequested = Describe(actuator.command);
                surfaceActual = Describe(actuator.actual);
            }
            else
            {
                surfaceRequested = "no actuator component";
                surfaceActual = surfaceRequested;
            }

            // Replacement thrust is deliberately not synthesised. If the propulsion model declares no
            // authoritative data, this says so instead of showing a plausible number.
            if (sixDoF.propulsionModel == null)
            {
                replacementThrustN = Vector3.zero;
            }
            else
            {
                replacementThrustN = sixDoF.propulsionModel.IsAcceptableForLiveFlight
                    ? transform.forward * 0f
                    : Vector3.zero;
            }
        }

        private static string Describe(MavControlInput i)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "elev {0:F2} ail {1:F2} rud {2:F2} deg, throttle {3:F2}",
                i.elevatorDeg, i.aileronDeg, i.rudderDeg, i.throttle01);
        }

        private void Derive()
        {
            Vector3 totalLegacy = gravityForceN + aerodynamicForceN + legacyThrustN + legacyAssistForceN;
            accountedAccelerationMps2 = totalLegacy / Mathf.Max(1e-6f, massKg);

            float dt = Time.fixedDeltaTime;
            debugWorldAcceleration = hasPreviousVelocity
                ? MavLoadFactorMath.WorldAcceleration(worldVelocityMps, previousVelocity, dt)
                : Vector3.zero;
            previousVelocity = worldVelocityMps;
            hasPreviousVelocity = true;

            debugGravityAcceleration = Physics.gravity;
            debugSpecificForce = MavLoadFactorMath.SpecificForce(
                debugWorldAcceleration, debugGravityAcceleration);
            debugBodyNormalSpecificForce = MavLoadFactorMath.BodyNormalSpecificForce(
                debugSpecificForce, transform.up);
            debugNz = MavLoadFactorMath.Nz(debugSpecificForce, transform.up);
            debugLegacyHudG = MavLoadFactorMath.LegacyHudG(
                debugWorldAcceleration, transform.up);
            debugLegacyHudGMinusNz = MavLoadFactorMath.LegacyHudGMinusNz(
                debugGravityAcceleration, transform.up);

            Vector3 kinematicAccel = debugWorldAcceleration;
            verticalAccelerationMps2 = debugWorldAcceleration.y;

            // Specific force is what an accelerometer measures: kinematic acceleration MINUS gravity.
            // This is the TP-1538 definition of normal acceleration, and it is why this channel reads
            // +1 g in steady level flight while the runtime's gEstimate reads 0.
            Vector3 specificForceWorld = kinematicAccel - Physics.gravity;
            specificForceBodyG = new Vector3(
                Vector3.Dot(specificForceWorld, transform.right),
                Vector3.Dot(specificForceWorld, transform.up),
                Vector3.Dot(specificForceWorld, transform.forward)) / 9.80665f;

            // The instructor's own gEstimate, read back so the report can show that the runtime
            // value and this component's independently computed debugLegacyHudG agree. If they ever
            // diverge, one of them has stopped being the formula it claims to be.
            debugInstructorReportedG = instructor != null ? instructor.gEstimate
                                     : (jet != null ? jet.gEstimate : 0f);

            float energyPerMass = 0.5f * trueAirspeedMps * trueAirspeedMps
                                  + 9.80665f * transform.position.y;
            if (hasPreviousEnergy && dt > 1e-6f)
                mechanicalEnergyRateWPerKg = (energyPerMass - previousEnergyPerMass) / dt;
            previousEnergyPerMass = energyPerMass;
            hasPreviousEnergy = true;
        }

        private void CheckIntegrity()
        {
            allChannelsFinite =
                Finite(gravityForceN) && Finite(aerodynamicForceN) && Finite(aerodynamicMomentNm)
                && Finite(legacyThrustN) && Finite(legacyAssistForceN) && Finite(legacyAssistTorqueNm)
                && Finite(replacementTotalForceN) && Finite(replacementTotalMomentNm)
                && Finite(accountedAccelerationMps2) && Finite(specificForceBodyG)
                && Finite(debugWorldAcceleration) && Finite(debugSpecificForce)
                && !float.IsNaN(debugNz) && !float.IsInfinity(debugNz)
                && !float.IsNaN(mechanicalEnergyRateWPerKg)
                && !float.IsInfinity(mechanicalEnergyRateWPerKg);

            integrityNote = allChannelsFinite
                ? "all accounted channels finite"
                : "NON-FINITE CHANNEL PRESENT - this report is not trustworthy this step";
        }

        private static bool Finite(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z)
                   && !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
        }

        [ContextMenu("Log Force Accounting Report")]
        public void LogReport()
        {
            Debug.Log(BuildReport(), this);
        }

        public string BuildReport()
        {
            CultureInfo inv = CultureInfo.InvariantCulture;
            StringBuilder sb = new StringBuilder(2048);
            sb.AppendLine("WHO ACCELERATED THE AIRCRAFT?");
            sb.AppendLine("=============================");
            sb.AppendLine("  owner            " + owner);
            sb.AppendLine("  gravity provider " + gravityProvider);
            sb.AppendLine();
            sb.AppendLine(string.Format(inv,
                "  mass {0:F0} kg   TAS {1:F1} m/s   Mach {2:F3}   rho {3:F4}   qbar {4:F0} Pa",
                massKg, trueAirspeedMps, mach, airDensityKgM3, dynamicPressurePa));
            sb.AppendLine(string.Format(inv,
                "  alpha {0:F2} deg   beta {1:F2} deg   p/q/r {2:F1}/{3:F1}/{4:F1} deg/s",
                alphaDeg, betaDeg, bodyAngularRateDegSec.z, bodyAngularRateDegSec.x,
                bodyAngularRateDegSec.y));
            sb.AppendLine();
            sb.AppendLine("  LEGACY contributions, N and N m");
            sb.AppendLine("    gravity        " + gravityForceN.ToString("F0"));
            sb.AppendLine("    aerodynamic    " + aerodynamicForceN.ToString("F0"));
            sb.AppendLine("    aero moment    " + aerodynamicMomentNm.ToString("F0")
                          + "   (from published angular accel x inertia diagonal; ignores Ixz)");
            sb.AppendLine("    thrust         " + legacyThrustN.ToString("F0"));
            sb.AppendLine("    assist force   " + legacyAssistForceN.ToString("F0"));
            sb.AppendLine("    assist torque  " + legacyAssistTorqueNm.ToString("F0"));
            sb.AppendLine("    gear/brake     " + brakeAndGearDrag);
            sb.AppendLine();
            sb.AppendLine("  REPLACEMENT shadow, computed and never applied");
            sb.AppendLine("    force          " + replacementTotalForceN.ToString("F0"));
            sb.AppendLine("    moment         " + replacementTotalMomentNm.ToString("F0"));
            sb.AppendLine(string.Format(inv,
                "    CX/CY/CZ       {0:F4} / {1:F4} / {2:F4}", cx, cy, cz));
            sb.AppendLine(string.Format(inv,
                "    Cl/Cm/Cn       {0:F4} / {1:F4} / {2:F4}", cl, cm, cn));
            sb.AppendLine("    surf requested " + surfaceRequested);
            sb.AppendLine("    surf actual    " + surfaceActual);
            sb.AppendLine();
            sb.AppendLine("  DERIVED");
            sb.AppendLine("    accounted a    " + accountedAccelerationMps2.ToString("F2") + " m/s^2");
            sb.AppendLine(string.Format(inv,
                "    Nz (accelerometer, gravity excluded)  {0:F2} g", debugNz));
            sb.AppendLine(string.Format(inv,
                "    runtime gEstimate (kinematic)         {0:F2} g   <- differs by ~1 g by definition",
                debugLegacyHudG));
            sb.AppendLine(string.Format(inv,
                "    vertical accel {0:F2} m/s^2   energy rate {1:F1} W/kg",
                verticalAccelerationMps2, mechanicalEnergyRateWPerKg));
            sb.AppendLine("    integrity      " + integrityNote);
            return sb.ToString();
        }
    }
}
