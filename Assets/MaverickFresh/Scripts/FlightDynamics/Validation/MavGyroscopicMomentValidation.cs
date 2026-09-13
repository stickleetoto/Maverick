using System.Text;
using UnityEngine;
using MaverickFresh.FlightDynamics.F16;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>
    /// Deterministic pure-math tests for the gyroscopic coupling moment, G-001 .. G-007.
    ///
    /// These run before anything touches Unity physics, and they exist because a correction that is
    /// only ever checked end-to-end inside PhysX cannot be separated from the thing it is correcting.
    /// If the sign of the cross product were reversed, the Play Mode test would show a mismatch and
    /// give no clue whether the fault lay in the formula, the tensor, the frame conversion or the
    /// measurement. Here each of those is isolated.
    ///
    /// The expectations are HAND-EXPANDED, not computed by calling the same helpers the
    /// implementation calls. G-005 in particular writes out the F-16 tensor product term by term:
    ///
    ///     I = [ Ix   0   -Ixz ]        H = I w = ( Ix p - Ixz r ,  Iy q ,  -Ixz p + Iz r )
    ///         [  0  Iy     0  ]
    ///         [-Ixz  0    Iz  ]        Mgyro = -( w x H )
    ///
    /// A test that reused MavInertiaTensorMath.Multiply to build its own expectation would agree with
    /// the implementation no matter what either of them did.
    /// </summary>
    public static class MavGyroscopicMomentValidation
    {
        public static string Run(out int passed, out int failed)
        {
            StringBuilder report = new StringBuilder(8192);
            passed = 0;
            failed = 0;

            report.AppendLine("Gyroscopic coupling moment - pure math (G-001 .. G-007)");

            G001_ZeroRate(report, ref passed, ref failed);
            G002_SphericalInertia(report, ref passed, ref failed);
            G003_PrincipalAxisRotation(report, ref passed, ref failed);
            G004_DiagonalAsymmetric(report, ref passed, ref failed);
            G005_F16WithIxz(report, ref passed, ref failed);
            G006_FrameRoundTrip(report, ref passed, ref failed);
            G007_FiniteGuards(report, ref passed, ref failed);
            G008_LoadSetPlumbing(report, ref passed, ref failed);
            G009_ReadinessGate(report, ref passed, ref failed);

            report.Append("  G-RESULT: ").Append(failed == 0 ? "PASS" : "FAIL")
                .Append(" passed=").Append(passed).Append(" failed=").Append(failed);
            return report.ToString();
        }

        // ---------------------------------------------------------------- G-001

        private static void G001_ZeroRate(StringBuilder r, ref int passed, ref int failed)
        {
            MavInertiaMatrix inertia = MavInertiaTensorMath.BuildAeroBodyInertia(
                MavF16MassReference.IxKgM2, MavF16MassReference.IyKgM2,
                MavF16MassReference.IzKgM2, MavF16MassReference.IxzKgM2);

            Vector3 moment;
            bool ok = MavGyroscopicMoment.TryCompute(inertia, Vector3.zero, out moment);

            Check(ok && IsExactlyZero(moment), "G-001",
                "w = 0 gives exactly zero coupling moment (" + moment.ToString("F6")
                + "), so a stationary aircraft is untouched by this term",
                r, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- G-002

        private static void G002_SphericalInertia(StringBuilder r, ref int passed, ref int failed)
        {
            // Ix = Iy = Iz, no products. I w is parallel to w, so the cross product vanishes for
            // EVERY w - the one inertia shape that has no gyroscopic coupling at all.
            MavInertiaMatrix sphere = MavInertiaTensorMath.BuildAeroBodyInertia(
                50000f, 50000f, 50000f, 0f);

            Vector3[] rates =
            {
                new Vector3(1.3f, -0.7f, 2.1f),
                new Vector3(-4f, 0.2f, 0.05f),
                new Vector3(0.001f, 0.001f, 0.001f)
            };

            float worst = 0f;
            float worstRelative = 0f;
            for (int i = 0; i < rates.Length; i++)
            {
                Vector3 m;
                MavGyroscopicMoment.TryCompute(sphere, rates[i], out m);

                // The natural scale of the cross product's individual terms, before they cancel.
                float scale = rates[i].magnitude
                              * MavInertiaTensorMath.Multiply(sphere, rates[i]).magnitude;
                worst = Mathf.Max(worst, m.magnitude);
                worstRelative = Mathf.Max(worstRelative, m.magnitude / Mathf.Max(1f, scale));
            }

            Check(worstRelative < 1e-5f, "G-002",
                "spherical inertia gives zero coupling for every rate tested: worst residual "
                + worst.ToString("E3") + " Nm, which is " + worstRelative.ToString("E2")
                + " of the term scale before cancellation. I w stays parallel to w, so the cross "
                + "product is zero, and what is left is float32 cancelling two numbers of order 1e5",
                r, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- G-003

        private static void G003_PrincipalAxisRotation(StringBuilder r, ref int passed, ref int failed)
        {
            // A DIAGONAL tensor, so the body axes are the principal axes. Rotation about any one of
            // them makes I w parallel to w again.
            MavInertiaMatrix diagonal = MavInertiaTensorMath.BuildAeroBodyInertia(
                12875f, 75674f, 85552f, 0f);

            Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };
            string[] names = { "roll (body X)", "pitch (body Y)", "yaw (body Z)" };
            float worst = 0f;
            float worstRelative = 0f;
            string worstAxis = "none - every axis gave exactly zero";

            for (int i = 0; i < axes.Length; i++)
            {
                Vector3 rate = axes[i] * 2.5f;
                Vector3 m;
                MavGyroscopicMoment.TryCompute(diagonal, rate, out m);

                float scale = rate.magnitude
                              * MavInertiaTensorMath.Multiply(diagonal, rate).magnitude;
                float relative = m.magnitude / Mathf.Max(1f, scale);
                if (relative > worstRelative)
                {
                    worst = m.magnitude;
                    worstRelative = relative;
                    worstAxis = names[i];
                }
            }

            Check(worstRelative < 1e-5f, "G-003",
                "rotation about each principal axis in turn gives zero coupling (worst "
                + worst.ToString("E3") + " Nm on " + worstAxis + ", "
                + worstRelative.ToString("E2")
                + " of the term scale) - a body spinning about a principal axis has no torque-free "
                + "precession",
                r, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- G-004

        private static void G004_DiagonalAsymmetric(StringBuilder r, ref int passed, ref int failed)
        {
            const float ix = 10000f;
            const float iy = 60000f;
            const float iz = 70000f;
            Vector3 w = new Vector3(1.5f, 0.8f, -0.6f);

            MavInertiaMatrix inertia = MavInertiaTensorMath.BuildAeroBodyInertia(ix, iy, iz, 0f);

            // Hand-calculated. Diagonal tensor, so H = (Ix p, Iy q, Iz r):
            //   H  = (10000*1.5, 60000*0.8, 70000*-0.6) = (15000, 48000, -42000)
            //   wxH = ( q*Hz - r*Hy , r*Hx - p*Hz , p*Hy - q*Hx )
            //       = ( 0.8*-42000 - (-0.6)*48000 , -0.6*15000 - 1.5*(-42000) , 1.5*48000 - 0.8*15000 )
            //       = ( -33600 + 28800 , -9000 + 63000 , 72000 - 12000 )
            //       = ( -4800 , 54000 , 60000 )
            //   Mgyro = -(wxH) = ( 4800 , -54000 , -60000 )
            Vector3 expected = new Vector3(4800f, -54000f, -60000f);

            Vector3 moment;
            bool ok = MavGyroscopicMoment.TryCompute(inertia, w, out moment);
            float error = (moment - expected).magnitude;

            r.Append("        G-004 computed ").Append(moment.ToString("F1"))
              .Append(" Nm against hand-calculated ").Append(expected.ToString("F1"))
              .AppendLine(" Nm");

            Check(ok && error < 1f, "G-004",
                "an asymmetric diagonal tensor matches the hand-expanded -w x (I w) to "
                + error.ToString("F4") + " Nm",
                r, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- G-005

        private static void G005_F16WithIxz(StringBuilder r, ref int passed, ref int failed)
        {
            float ix = MavF16MassReference.IxKgM2;
            float iy = MavF16MassReference.IyKgM2;
            float iz = MavF16MassReference.IzKgM2;
            float ixz = MavF16MassReference.IxzKgM2;

            Vector3 w = new Vector3(1.2f, 0.45f, 0.30f);   // p, q, r - the U5CR-008 case 1 rates

            MavInertiaMatrix inertia = MavInertiaTensorMath.BuildAeroBodyInertia(ix, iy, iz, ixz);

            // INDEPENDENT expansion, written out rather than delegated:
            //   I = [ Ix  0  -Ixz ; 0  Iy  0 ; -Ixz  0  Iz ]
            //   H = ( Ix*p - Ixz*r , Iy*q , -Ixz*p + Iz*r )
            float hx = ix * w.x - ixz * w.z;
            float hy = iy * w.y;
            float hz = -ixz * w.x + iz * w.z;

            //   w x H = ( q*Hz - r*Hy , r*Hx - p*Hz , p*Hy - q*Hx )
            Vector3 cross = new Vector3(
                w.y * hz - w.z * hy,
                w.z * hx - w.x * hz,
                w.x * hy - w.y * hx);

            Vector3 expected = -cross;

            Vector3 moment;
            bool ok = MavGyroscopicMoment.TryCompute(inertia, w, out moment);
            float error = (moment - expected).magnitude;
            float scale = Mathf.Max(1f, expected.magnitude);

            r.Append("        G-005 F-16 tensor with Ixz = ").Append(ixz.ToString("F1"))
              .Append(" kg m^2: H = (").Append(hx.ToString("F1")).Append(", ")
              .Append(hy.ToString("F1")).Append(", ").Append(hz.ToString("F1")).AppendLine(")");
            r.Append("        computed ").Append(moment.ToString("F1"))
              .Append(" Nm against independent expansion ").Append(expected.ToString("F1"))
              .AppendLine(" Nm");

            Check(ok && error / scale < 1e-4f, "G-005",
                "the full F-16 tensor including Ixz matches an independent term-by-term expansion to "
                + (100f * error / scale).ToString("F5") + "%",
                r, ref passed, ref failed);

            // The off-diagonal term must actually matter here, or G-005 would pass on a diagonal-only
            // implementation and prove nothing about Ixz.
            MavInertiaMatrix diagonalOnly = MavInertiaTensorMath.BuildAeroBodyInertia(ix, iy, iz, 0f);
            Vector3 withoutIxz;
            MavGyroscopicMoment.TryCompute(diagonalOnly, w, out withoutIxz);
            float difference = (moment - withoutIxz).magnitude;

            Check(difference / scale > 0.01f, "G-005b",
                "and dropping Ixz changes the answer by " + difference.ToString("F1") + " Nm ("
                + (100f * difference / scale).ToString("F2")
                + "%), so this case genuinely exercises the product of inertia rather than passing on "
                + "a diagonal-only evaluation",
                r, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- G-006

        private static void G006_FrameRoundTrip(StringBuilder r, ref int passed, ref int failed)
        {
            MavInertiaMatrix inertia = MavInertiaTensorMath.BuildAeroBodyInertia(
                MavF16MassReference.IxKgM2, MavF16MassReference.IyKgM2,
                MavF16MassReference.IzKgM2, MavF16MassReference.IxzKgM2);

            Vector3 w = new Vector3(0.9f, -0.35f, 0.6f);
            Vector3 bodyMoment;
            MavGyroscopicMoment.TryCompute(inertia, w, out bodyMoment);

            // aero body -> Unity local -> aero body, through the production helpers only.
            Vector3 unityLocal = MavFlightDynamicsMath.AeroBodyMomentToUnityLocal(bodyMoment);
            Vector3 back = MavFlightDynamicsMath.UnityLocalAngularRateToAeroBody(unityLocal);
            float localError = (back - bodyMoment).magnitude;

            // aero body -> Unity local -> WORLD -> Unity local -> aero body, at a non-trivial
            // attitude, because the world hop is where a body/world mix-up would show.
            Quaternion attitude = Quaternion.Euler(-17f, 43f, 26f);
            Vector3 world = attitude * unityLocal;
            Vector3 backLocal = Quaternion.Inverse(attitude) * world;
            Vector3 backBody = MavFlightDynamicsMath.UnityLocalAngularRateToAeroBody(backLocal);
            float worldError = (backBody - bodyMoment).magnitude;
            float scale = Mathf.Max(1f, bodyMoment.magnitude);

            r.Append("        G-006 body ").Append(bodyMoment.ToString("F2"))
              .Append(" -> Unity ").Append(unityLocal.ToString("F2"))
              .Append(" -> body ").Append(back.ToString("F2")).AppendLine();

            Check(localError / scale < 1e-5f, "G-006",
                "the aero-body to Unity moment mapping round-trips to "
                + (100f * localError / scale).ToString("F6")
                + "% - the axial-vector helper is its own inverse, as the two conventions require",
                r, ref passed, ref failed);
            Check(worldError / scale < 1e-4f, "G-006b",
                "and a full body -> Unity local -> world -> back trip at a non-trivial attitude "
                + "preserves it to " + (100f * worldError / scale).ToString("F6") + "%",
                r, ref passed, ref failed);

            // The mapping must not be the identity, or the round trip would prove nothing.
            Check((unityLocal - bodyMoment).magnitude / scale > 0.1f, "G-006c",
                "the mapping is not a no-op (" + bodyMoment.ToString("F1") + " Nm becomes "
                + unityLocal.ToString("F1")
                + " Nm in Unity axes), so the round trip is a real test of it",
                r, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- G-007

        private static void G007_FiniteGuards(StringBuilder r, ref int passed, ref int failed)
        {
            MavInertiaMatrix inertia = MavInertiaTensorMath.BuildAeroBodyInertia(
                MavF16MassReference.IxKgM2, MavF16MassReference.IyKgM2,
                MavF16MassReference.IzKgM2, MavF16MassReference.IxzKgM2);

            Vector3 nanRate = new Vector3(float.NaN, 0.2f, 0.1f);
            Vector3 infRate = new Vector3(0.3f, float.PositiveInfinity, 0.1f);

            Vector3 m1;
            bool ok1 = MavGyroscopicMoment.TryCompute(inertia, nanRate, out m1);
            Vector3 m2;
            bool ok2 = MavGyroscopicMoment.TryCompute(inertia, infRate, out m2);

            Check(!ok1 && IsExactlyZero(m1), "G-007",
                "a NaN angular rate is refused and returns a zero moment, not a NaN - a NaN would be "
                + "rejected at the load boundary and the aircraft would go quiet with the cause "
                + "several layers away",
                r, ref passed, ref failed);
            Check(!ok2 && IsExactlyZero(m2), "G-007b",
                "an infinite angular rate is refused the same way",
                r, ref passed, ref failed);

            MavInertiaMatrix broken = inertia;
            broken.m11 = float.NaN;
            Vector3 m3;
            bool ok3 = MavGyroscopicMoment.TryCompute(broken, new Vector3(1f, 1f, 1f), out m3);
            Check(!ok3 && IsExactlyZero(m3), "G-007c",
                "and a non-finite tensor is refused too",
                r, ref passed, ref failed);

            // A degenerate tensor must not survive reconstruction either.
            MavInertiaMatrix rebuilt;
            bool okZero = MavGyroscopicMoment.TryAeroBodyInertiaFromRigidbody(
                Vector3.zero, Quaternion.identity, out rebuilt);
            Check(!okZero, "G-007d",
                "a zero principal-moment tensor is refused rather than producing a correction from a "
                + "body with no rotational inertia",
                r, ref passed, ref failed);

            // The positive control: valid input must still succeed, or the guards would be passing by
            // refusing everything.
            MavInertiaMatrix good;
            bool okGood = MavGyroscopicMoment.TryAeroBodyInertiaFromRigidbody(
                new Vector3(75673.62f, 85576.49f, 12850.46f),
                Quaternion.Euler(1.0492f, 0f, 0f), out good);
            Vector3 m4;
            bool okCompute = MavGyroscopicMoment.TryCompute(good, new Vector3(1f, 0.5f, 0.25f), out m4);
            Check(okGood && okCompute && m4.magnitude > 1f, "G-007e",
                "while the real F-16 principal moments and rotation still produce a finite non-zero "
                + "moment (" + m4.ToString("F1") + " Nm), so the guards are not simply refusing "
                + "everything",
                r, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- G-008

        /// <summary>
        /// The load-set plumbing, checked without Unity.
        ///
        /// The formula being right is not the same as it being carried correctly: the correction has to
        /// reach the total exactly once, stay separable from the external moment for telemetry, and be
        /// refused if something tries to add it twice. Those are properties of
        /// MavFlightDynamicsLoadSet, and they are checkable here rather than only inside PhysX.
        /// </summary>
        private static void G008_LoadSetPlumbing(StringBuilder r, ref int passed, ref int failed)
        {
            Vector3 aeroMoment = new Vector3(1000f, 2000f, 3000f);
            Vector3 propMoment = new Vector3(10f, 20f, 30f);
            Vector3 gyroMoment = new Vector3(-500f, 25000f, -36000f);

            MavAerodynamicLoads aero = MavAerodynamicLoads.Zero;
            aero.forceAeroBodyN = new Vector3(100f, 0f, -200f);
            aero.momentAeroBodyNm = aeroMoment;

            MavPropulsiveLoads prop = MavPropulsiveLoads.Zero;
            prop.momentAeroBodyNm = propMoment;

            MavFlightDynamicsLoadSet set = new MavFlightDynamicsLoadSet();
            set.BeginStep(1);
            set.AddAerodynamic(aero);
            set.AddPropulsive(prop);
            bool added = set.AddInertialCorrection(gyroMoment);

            Vector3 expectedTotal = aeroMoment + propMoment + gyroMoment;
            Vector3 expectedExternal = aeroMoment + propMoment;

            Check(added, "G-008",
                "the inertial correction is accepted by the load set",
                r, ref passed, ref failed);
            Check((set.totalMomentAeroBodyNm - expectedTotal).magnitude < 0.01f, "G-008b",
                "the total moment is external + correction (" + set.totalMomentAeroBodyNm.ToString("F1")
                + " against " + expectedTotal.ToString("F1") + " Nm)",
                r, ref passed, ref failed);
            Check((set.ExternalMomentAeroBodyNm - expectedExternal).magnitude < 0.01f, "G-008c",
                "and the EXTERNAL moment is still recoverable separately ("
                + set.ExternalMomentAeroBodyNm.ToString("F1") + " against "
                + expectedExternal.ToString("F1")
                + " Nm), so telemetry can tell an aerodynamic moment from a coupling term",
                r, ref passed, ref failed);

            // A moment only: the correction must not touch the force total.
            Check((set.totalForceAeroBodyN - aero.forceAeroBodyN).magnitude < 0.01f, "G-008d",
                "the correction adds NO force (" + set.totalForceAeroBodyN.ToString("F1")
                + " Nm is still just the aerodynamic force), so specific force and load factor are "
                + "untouched - an accelerometer does not feel it",
                r, ref passed, ref failed);

            // Adding it twice must be refused and must show up as an ownership violation.
            bool second = set.AddInertialCorrection(gyroMoment);
            Check(!second, "G-008e",
                "a second inertial correction in the same step is refused",
                r, ref passed, ref failed);
            Check(!set.HasSingleOwnerPerSource, "G-008f",
                "and the attempt is recorded as an ownership violation (" + set.inertialContributions
                + " contributions), so a double correction cannot reach the Rigidbody quietly",
                r, ref passed, ref failed);

            // A fresh step must clear it, or last step's correction would leak into this one.
            MavFlightDynamicsLoadSet clean = set;
            clean.BeginStep(2);
            Check(IsExactlyZero(clean.inertialCorrectionMomentAeroBodyNm)
                  && clean.inertialContributions == 0, "G-008g",
                "and BeginStep clears the correction channel, so a step that computes none cannot "
                + "inherit the previous one",
                r, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- G-009

        /// <summary>
        /// The Euler correction is MANDATORY, so readiness must refuse a body that has it switched
        /// off - an unticked checkbox is not an acceptable way to reach known-incomplete rotational
        /// dynamics.
        ///
        /// Tested on the pure readiness function rather than through a component, because that is
        /// where the rule lives and a pure test cannot be satisfied by some other condition failing
        /// first. Each case below starts from FullyReady and changes exactly one.
        /// </summary>
        private static void G009_ReadinessGate(StringBuilder r, ref int passed, ref int failed)
        {
            string reason;

            // Baseline: everything satisfied, including complete angular dynamics.
            MavFlightDynamicsReadinessInputs ok = MavFlightDynamicsReadinessInputs.FullyReady;
            bool okReady = MavFlightDynamicsReadiness.EvaluateOperational(ok, out reason);
            Check(okReady, "G-009",
                "with the compensation on, an otherwise complete stack is operationally live-ready ("
                + reason + ")",
                r, ref passed, ref failed);

            // The defect this gate exists to prevent: compensation off, nothing acknowledged.
            MavFlightDynamicsReadinessInputs off = MavFlightDynamicsReadinessInputs.FullyReady;
            off.angularDynamicsAccepted = false;
            bool offReady = MavFlightDynamicsReadiness.EvaluateOperational(off, out reason);
            Check(!offReady, "G-009b",
                "but with the gyroscopic compensation disabled and unacknowledged it is REFUSED, so "
                + "a serialized false cannot silently fly the naive I^-1 M dynamics: " + reason,
                r, ref passed, ref failed);
            Check(reason.Contains("angular dynamics"), "G-009c",
                "and the refusal names the reason rather than failing generically",
                r, ref passed, ref failed);

            // The A/B path the phase brief permits: deliberate, and it takes a second field.
            MavFlightDynamicsReadinessInputs acknowledged =
                MavFlightDynamicsReadinessInputs.FullyReady;
            acknowledged.angularDynamicsAccepted = true;
            Check(MavFlightDynamicsReadiness.EvaluateOperational(acknowledged, out reason),
                "G-009d",
                "while an explicit acknowledgement still allows it, which is what keeps A/B "
                + "validation possible without leaving the door open",
                r, ref passed, ref failed);

            // THE REAL COMPONENT PROPERTY, not a restatement of it.
            //
            // The first version of these two checks called a local mirror of the rule, and a mutation
            // probe showed why that was not enough: breaking MavSixDoFBody.AngularDynamicsAcceptable
            // left the suite entirely green, because nothing offline was reading it. A test that
            // duplicates the logic it is checking agrees with the implementation no matter what the
            // implementation does.
            GameObject host = new GameObject("g009-body");
            MavSixDoFBody body = host.AddComponent<MavSixDoFBody>();

            Check(body.applyBackendGyroscopicCompensation, "G-009e",
                "a freshly constructed MavSixDoFBody has the compensation ON, so the default "
                + "configuration satisfies the gate",
                r, ref passed, ref failed);

            body.applyBackendGyroscopicCompensation = false;
            body.acknowledgeIncompleteAngularDynamicsForTesting = false;
            bool offRefused = !body.AngularDynamicsAcceptable;

            body.acknowledgeIncompleteAngularDynamicsForTesting = true;
            bool acknowledgedAccepted = body.AngularDynamicsAcceptable;

            body.applyBackendGyroscopicCompensation = true;
            body.acknowledgeIncompleteAngularDynamicsForTesting = false;
            bool onAccepted = body.AngularDynamicsAcceptable;

            Check(offRefused, "G-009f",
                "the component refuses compensation-off with no acknowledgement",
                r, ref passed, ref failed);
            Check(acknowledgedAccepted && onAccepted, "G-009g",
                "and accepts either the compensation being on or an explicit acknowledgement",
                r, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- helper

        /// <summary>
        /// Exact component-wise zero. Unity's Vector3 equality is approximate, so "== Vector3.zero"
        /// would accept a small non-zero value - the opposite of what these checks assert.
        /// </summary>
        private static bool IsExactlyZero(Vector3 v)
        {
            return v.x == 0f && v.y == 0f && v.z == 0f;
        }

        private static void Check(
            bool condition, string id, string text, StringBuilder r, ref int passed, ref int failed)
        {
            if (condition)
            {
                passed++;
                r.Append("  PASS  ").Append(id).Append("  ").AppendLine(text);
            }
            else
            {
                failed++;
                r.Append("  FAIL  ").Append(id).Append("  ").AppendLine(text);
            }
        }
    }
}
