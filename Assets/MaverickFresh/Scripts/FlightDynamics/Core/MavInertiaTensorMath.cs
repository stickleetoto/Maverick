using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>A symmetric 3x3 inertia matrix, kg m^2, in whatever frame the caller is working in.</summary>
    public struct MavInertiaMatrix
    {
        public float m00, m01, m02;
        public float m10, m11, m12;
        public float m20, m21, m22;

        public float Get(int r, int c)
        {
            if (r == 0) return c == 0 ? m00 : (c == 1 ? m01 : m02);
            if (r == 1) return c == 0 ? m10 : (c == 1 ? m11 : m12);
            return c == 0 ? m20 : (c == 1 ? m21 : m22);
        }

        public void Set(int r, int c, float v)
        {
            if (r == 0) { if (c == 0) m00 = v; else if (c == 1) m01 = v; else m02 = v; return; }
            if (r == 1) { if (c == 0) m10 = v; else if (c == 1) m11 = v; else m12 = v; return; }
            if (c == 0) m20 = v; else if (c == 1) m21 = v; else m22 = v;
        }

        public bool IsFinite()
        {
            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 3; c++)
                {
                    float v = Get(r, c);
                    if (float.IsNaN(v) || float.IsInfinity(v))
                        return false;
                }
            }
            return true;
        }

        public override string ToString()
        {
            return string.Format(
                "[{0,10:F1} {1,10:F1} {2,10:F1}; {3,10:F1} {4,10:F1} {5,10:F1}; {6,10:F1} {7,10:F1} {8,10:F1}]",
                m00, m01, m02, m10, m11, m12, m20, m21, m22);
        }
    }

    /// <summary>
    /// Inertia-tensor algebra for the aero-body to Unity boundary.
    ///
    /// WHY THIS EXISTS. Phase 5B.5 recorded a defect (D8) claiming the product of inertia Ixz was
    /// "unrepresentable in Unity's diagonal tensor". That was wrong twice over. Unity carries inertia
    /// as principal moments (<c>Rigidbody.inertiaTensor</c>) PLUS the orientation of the principal
    /// frame (<c>Rigidbody.inertiaTensorRotation</c>), which can represent any symmetric positive
    /// definite inertia matrix exactly; and the project already performed that diagonalization in
    /// MavF16MassReference. The real gap was that nothing ever reconstructed the matrix from the Unity
    /// representation to check the transformation was right.
    ///
    /// So this class exists to make the round trip checkable:
    ///
    ///     Ix, Iy, Iz, Ixz  ->  aero-body matrix  ->  Unity-local matrix
    ///                      ->  principal moments + rotation  ->  reconstructed matrix
    ///
    /// and the reconstruction is compared against the input. A transformation that cannot be
    /// reconstructed is not verified, whatever the code looks like.
    ///
    /// SIGN CONVENTION, DERIVED NOT GUESSED. Aircraft body axes are X forward, Y right, Z down, and
    /// the inertia matrix carries the products of inertia with NEGATIVE off-diagonal signs:
    ///
    ///     [  Ix   -Ixy  -Ixz ]      symmetric aircraft (XZ plane of symmetry) =>  [  Ix   0  -Ixz ]
    ///     [ -Ixy   Iy   -Iyz ]                                                    [   0  Iy    0  ]
    ///     [ -Ixz  -Iyz   Iz  ]                                                    [ -Ixz  0   Iz  ]
    ///
    /// with Ixz published as a POSITIVE number. This is confirmed by the rotational equations in
    /// NASA TP-1538 Appendix B, which take the standard form
    ///
    ///     p_dot = (Iz-Iy)/Ix qr + Ixz/Ix (r_dot + pq) + qSb/Ix Cl
    ///     q_dot = (Iz-Ix)/Iy pr + Ixz/Iy (r^2 - p^2)  + qSc/Iy Cm
    ///     r_dot = (Ix-Iy)/Iz pq + Ixz/Iz (p_dot - qr) + qSb/Iz Cn
    ///
    /// Those +Ixz/Ix terms follow from the matrix above with a positive Ixz; they would carry the
    /// opposite sign if the off-diagonals were +Ixz. TP-1538 Table I publishes IXz = 1331 kg-m^2
    /// (982 slug-ft^2) as a positive value, so that is the convention implemented here.
    /// </summary>
    public static class MavInertiaTensorMath
    {
        /// <summary>Diagnostic: did the last Diagonalize call get a left-handed eigenbasis?</summary>
        public static bool debugLastEigenBasisWasLeftHanded;

        /// <summary>
        /// The body-axis inertia matrix for an aircraft with an XZ plane of symmetry.
        /// Off-diagonal terms are NEGATIVE Ixz - see the class remarks for the derivation.
        /// </summary>
        public static MavInertiaMatrix BuildAeroBodyInertia(float ix, float iy, float iz, float ixz)
        {
            MavInertiaMatrix m = new MavInertiaMatrix();
            m.m00 = ix; m.m01 = 0f; m.m02 = -ixz;
            m.m10 = 0f; m.m11 = iy; m.m12 = 0f;
            m.m20 = -ixz; m.m21 = 0f; m.m22 = iz;
            return m;
        }

        /// <summary>
        /// Change of basis from aero body axes to Unity local axes, applied to a rank-2 tensor.
        ///
        /// The basis change P maps a true vector as v_unity = P v_aero, with
        ///
        ///     unity.x = aero.y      (right)
        ///     unity.y = -aero.z     (up, because aero Z is DOWN)
        ///     unity.z = aero.x      (forward)
        ///
        /// so P = [[0,1,0],[0,0,-1],[1,0,0]], and det(P) = -1: this is a reflection, the same
        /// handedness change MavFlightDynamicsMath documents. Inertia is a rank-2 tensor and
        /// transforms by congruence, I_unity = P I_aero P^T, which is valid for any orthogonal P
        /// regardless of the determinant's sign. The determinant only matters for axial VECTORS.
        ///
        /// Computed generally rather than written out by hand, so the result cannot drift from the
        /// stated basis change.
        /// </summary>
        public static MavInertiaMatrix AeroBodyInertiaToUnityLocal(MavInertiaMatrix aero)
        {
            return Congruence(aero, UnityFromAeroBasis());
        }

        /// <summary>Inverse of <see cref="AeroBodyInertiaToUnityLocal"/>.</summary>
        public static MavInertiaMatrix UnityLocalInertiaToAeroBody(MavInertiaMatrix unity)
        {
            return Congruence(unity, Transpose(UnityFromAeroBasis()));
        }

        /// <summary>P, where v_unity = P v_aero.</summary>
        public static MavInertiaMatrix UnityFromAeroBasis()
        {
            MavInertiaMatrix p = new MavInertiaMatrix();
            p.m00 = 0f; p.m01 = 1f; p.m02 = 0f;
            p.m10 = 0f; p.m11 = 0f; p.m12 = -1f;
            p.m20 = 1f; p.m21 = 0f; p.m22 = 0f;
            return p;
        }

        /// <summary>B A B^T.</summary>
        public static MavInertiaMatrix Congruence(MavInertiaMatrix a, MavInertiaMatrix b)
        {
            return Multiply(Multiply(b, a), Transpose(b));
        }

        public static MavInertiaMatrix Multiply(MavInertiaMatrix a, MavInertiaMatrix b)
        {
            MavInertiaMatrix r = new MavInertiaMatrix();
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    float s = 0f;
                    for (int k = 0; k < 3; k++)
                        s += a.Get(i, k) * b.Get(k, j);
                    r.Set(i, j, s);
                }
            }
            return r;
        }

        public static MavInertiaMatrix Transpose(MavInertiaMatrix a)
        {
            MavInertiaMatrix r = new MavInertiaMatrix();
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    r.Set(i, j, a.Get(j, i));
            return r;
        }

        /// <summary>
        /// Diagonalize a symmetric matrix into principal moments and the rotation of the principal
        /// frame, in the form Unity wants: I = R diag(principal) R^T.
        ///
        /// Cyclic Jacobi. Chosen over the closed-form 2x2 solution that the aircraft case permits
        /// because a general routine keeps working if a future aircraft carries Ixy or Iyz, and
        /// because it is the same routine whether or not the matrix is already diagonal.
        ///
        /// The eigenvector matrix is forced RIGHT-HANDED before it becomes a quaternion. Jacobi is
        /// free to return an improper basis (det = -1), and a quaternion cannot represent one; left
        /// unchecked that silently produces a reflected principal frame, which is exactly the class
        /// of error this whole exercise is about.
        /// </summary>
        public static bool Diagonalize(
            MavInertiaMatrix input,
            out Vector3 principalMoments,
            out Quaternion principalRotation)
        {
            principalMoments = Vector3.zero;
            principalRotation = Quaternion.identity;

            if (!input.IsFinite())
                return false;

            MavInertiaMatrix a = input;
            MavInertiaMatrix v = Identity();

            const int maxSweeps = 32;
            for (int sweep = 0; sweep < maxSweeps; sweep++)
            {
                float off = a.m01 * a.m01 + a.m02 * a.m02 + a.m12 * a.m12;
                if (off < 1e-18f)
                    break;

                RotateJacobi(ref a, ref v, 0, 1);
                RotateJacobi(ref a, ref v, 0, 2);
                RotateJacobi(ref a, ref v, 1, 2);
            }

            // Columns of v are the principal axes expressed in the input frame.
            Vector3 axis0 = new Vector3(v.m00, v.m10, v.m20);
            Vector3 axis1 = new Vector3(v.m01, v.m11, v.m21);
            Vector3 axis2 = new Vector3(v.m02, v.m12, v.m22);

            // HANDEDNESS. Jacobi may return a left-handed eigenvector set, and a quaternion cannot
            // carry a reflection. Quaternion.LookRotation derives its first basis vector as
            // cross(up, forward), so when (axis0, axis1, axis2) is left-handed it silently uses
            // -axis0 instead - and that is not a loss, because negating an eigenvector leaves the
            // eigenvalue untouched and I = R diag(L) R^T holds either way.
            //
            // The handedness is therefore recorded rather than corrected, and the reconstruction
            // check is what proves the claim. Reasoning about sign conventions is how this project
            // got a wrong answer before; measuring the round trip is how it stops.
            debugLastEigenBasisWasLeftHanded =
                Vector3.Dot(Vector3.Cross(axis0, axis1), axis2) < 0f;

            principalMoments = new Vector3(a.m00, a.m11, a.m22);
            principalRotation = Quaternion.LookRotation(axis2, axis1);

            return !float.IsNaN(principalRotation.x) && !float.IsNaN(principalRotation.w);
        }

        /// <summary>I = R diag(principal) R^T. The inverse of <see cref="Diagonalize"/>.</summary>
        public static MavInertiaMatrix Reconstruct(Vector3 principalMoments, Quaternion rotation)
        {
            Vector3 right = rotation * new Vector3(1f, 0f, 0f);
            Vector3 up = rotation * new Vector3(0f, 1f, 0f);
            Vector3 forward = rotation * new Vector3(0f, 0f, 1f);

            MavInertiaMatrix r = new MavInertiaMatrix();
            r.m00 = right.x; r.m01 = up.x; r.m02 = forward.x;
            r.m10 = right.y; r.m11 = up.y; r.m12 = forward.y;
            r.m20 = right.z; r.m21 = up.z; r.m22 = forward.z;

            MavInertiaMatrix d = new MavInertiaMatrix();
            d.m00 = principalMoments.x;
            d.m11 = principalMoments.y;
            d.m22 = principalMoments.z;

            return Multiply(Multiply(r, d), Transpose(r));
        }

        /// <summary>Largest absolute element-wise difference, kg m^2.</summary>
        public static float MaxAbsDifference(MavInertiaMatrix a, MavInertiaMatrix b)
        {
            float worst = 0f;
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    float d = Mathf.Abs(a.Get(i, j) - b.Get(i, j));
                    if (d > worst)
                        worst = d;
                }
            }
            return worst;
        }

        /// <summary>Largest element magnitude, for expressing an error as a relative figure.</summary>
        public static float MaxAbsElement(MavInertiaMatrix a)
        {
            float worst = 0f;
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    worst = Mathf.Max(worst, Mathf.Abs(a.Get(i, j)));
            return worst;
        }

        /// <summary>
        /// Inverse of a 3x3 matrix by cofactors. Returns false on a singular matrix rather than
        /// producing infinities, because an inertia tensor that cannot be inverted means the mass
        /// properties are wrong and that should surface as a refusal.
        /// </summary>
        public static bool TryInvert(MavInertiaMatrix a, out MavInertiaMatrix inverse)
        {
            inverse = new MavInertiaMatrix();

            float det =
                a.m00 * (a.m11 * a.m22 - a.m12 * a.m21)
                - a.m01 * (a.m10 * a.m22 - a.m12 * a.m20)
                + a.m02 * (a.m10 * a.m21 - a.m11 * a.m20);

            if (Mathf.Abs(det) < 1e-6f)
                return false;

            float inv = 1f / det;

            inverse.m00 = (a.m11 * a.m22 - a.m12 * a.m21) * inv;
            inverse.m01 = (a.m02 * a.m21 - a.m01 * a.m22) * inv;
            inverse.m02 = (a.m01 * a.m12 - a.m02 * a.m11) * inv;
            inverse.m10 = (a.m12 * a.m20 - a.m10 * a.m22) * inv;
            inverse.m11 = (a.m00 * a.m22 - a.m02 * a.m20) * inv;
            inverse.m12 = (a.m02 * a.m10 - a.m00 * a.m12) * inv;
            inverse.m20 = (a.m10 * a.m21 - a.m11 * a.m20) * inv;
            inverse.m21 = (a.m01 * a.m20 - a.m00 * a.m21) * inv;
            inverse.m22 = (a.m00 * a.m11 - a.m01 * a.m10) * inv;

            return true;
        }

        /// <summary>Matrix times vector.</summary>
        public static Vector3 Multiply(MavInertiaMatrix a, Vector3 v)
        {
            return new Vector3(
                a.m00 * v.x + a.m01 * v.y + a.m02 * v.z,
                a.m10 * v.x + a.m11 * v.y + a.m12 * v.z,
                a.m20 * v.x + a.m21 * v.y + a.m22 * v.z);
        }

        /// <summary>
        /// Euler's rigid-body rotational equation, solved for angular acceleration:
        ///
        ///     M = I w_dot + w x (I w)      =>      w_dot = I^-1 ( M - w x (I w) )
        ///
        /// The cross-product term is the gyroscopic coupling. With a non-zero product of inertia it is
        /// what makes rolling produce a yawing acceleration and vice versa - the behaviour the F-16's
        /// departure characteristics are built on, and the reason the Ixz term matters at all.
        ///
        /// Frame-agnostic: pass the inertia matrix and the vectors in the SAME frame.
        /// </summary>
        public static bool TryAngularAcceleration(
            MavInertiaMatrix inertia,
            Vector3 angularVelocity,
            Vector3 moment,
            out Vector3 angularAcceleration)
        {
            angularAcceleration = Vector3.zero;

            MavInertiaMatrix inverse;
            if (!TryInvert(inertia, out inverse))
                return false;

            Vector3 iOmega = Multiply(inertia, angularVelocity);
            Vector3 gyroscopic = Vector3.Cross(angularVelocity, iOmega);
            angularAcceleration = Multiply(inverse, moment - gyroscopic);
            return true;
        }

        /// <summary>
        /// The same dynamics, expressed the way Unity stores inertia: principal moments plus the
        /// rotation of the principal frame.
        ///
        /// This is the equation a PhysX-style solver evaluates given
        /// <c>Rigidbody.inertiaTensor</c> and <c>Rigidbody.inertiaTensorRotation</c>: rotate the state
        /// into the principal frame, solve there with a diagonal tensor, rotate the result back.
        ///
        /// Validating it against <see cref="TryAngularAcceleration"/> on the full matrix is what shows
        /// the principal-axis representation carries the COUPLED dynamics and not merely the same
        /// three diagonal numbers. It does not validate Unity's integrator - that needs Unity - but it
        /// does validate the representation Unity is being handed.
        /// </summary>
        public static Vector3 AngularAccelerationViaPrincipalAxes(
            Vector3 principalMoments,
            Quaternion principalRotation,
            Vector3 angularVelocity,
            Vector3 moment)
        {
            Quaternion toPrincipal = Quaternion.Inverse(principalRotation);

            Vector3 omegaP = toPrincipal * angularVelocity;
            Vector3 momentP = toPrincipal * moment;

            Vector3 iOmegaP = new Vector3(
                principalMoments.x * omegaP.x,
                principalMoments.y * omegaP.y,
                principalMoments.z * omegaP.z);

            Vector3 rhs = momentP - Vector3.Cross(omegaP, iOmegaP);

            Vector3 alphaP = new Vector3(
                rhs.x / Mathf.Max(1e-6f, principalMoments.x),
                rhs.y / Mathf.Max(1e-6f, principalMoments.y),
                rhs.z / Mathf.Max(1e-6f, principalMoments.z));

            return principalRotation * alphaP;
        }

        public static MavInertiaMatrix Identity()
        {
            MavInertiaMatrix m = new MavInertiaMatrix();
            m.m00 = 1f; m.m11 = 1f; m.m22 = 1f;
            return m;
        }

        private static void RotateJacobi(ref MavInertiaMatrix a, ref MavInertiaMatrix v, int p, int q)
        {
            float apq = a.Get(p, q);
            if (Mathf.Abs(apq) < 1e-20f)
                return;

            float app = a.Get(p, p);
            float aqq = a.Get(q, q);

            float theta = (aqq - app) / (2f * apq);
            float t = Mathf.Sign(theta == 0f ? 1f : theta)
                      / (Mathf.Abs(theta) + Mathf.Sqrt(theta * theta + 1f));
            float c = 1f / Mathf.Sqrt(t * t + 1f);
            float s = t * c;

            // A <- J^T A J
            for (int k = 0; k < 3; k++)
            {
                float akp = a.Get(k, p);
                float akq = a.Get(k, q);
                a.Set(k, p, c * akp - s * akq);
                a.Set(k, q, s * akp + c * akq);
            }
            for (int k = 0; k < 3; k++)
            {
                float apk = a.Get(p, k);
                float aqk = a.Get(q, k);
                a.Set(p, k, c * apk - s * aqk);
                a.Set(q, k, s * apk + c * aqk);
            }

            // V <- V J
            for (int k = 0; k < 3; k++)
            {
                float vkp = v.Get(k, p);
                float vkq = v.Get(k, q);
                v.Set(k, p, c * vkp - s * vkq);
                v.Set(k, q, s * vkp + c * vkq);
            }
        }
    }
}
