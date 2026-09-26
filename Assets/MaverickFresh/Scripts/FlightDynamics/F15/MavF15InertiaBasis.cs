using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// The F-15 inertia-basis arithmetic, stated once as a pure function of its inputs.
    ///
    /// <see cref="MavF15MassReference.CreateUnityMassProperties"/> performs the same conversion for
    /// the exact NASA 836 state with its own frozen constants, and is deliberately left untouched.
    /// This copy exists so the RESEARCH mass state can use the audited math without borrowing the
    /// exact type. The research validation proves the two agree on the exact inputs, so the math
    /// cannot drift apart unnoticed.
    ///
    /// Source body tensor (body X forward, Y right, Z down): [ Ix 0 -Ixz ; 0 Iy 0 ; -Ixz 0 Iz ].
    /// Unity local axes (X right, Y up, Z forward): Unity X = body Y, Unity Y = -body Z,
    /// Unity Z = body X, giving [ Iy 0 0 ; 0 Iz Ixz ; 0 Ixz Ix ]. The coupled Unity Y/Z block is
    /// diagonalized by a rotation about Unity X.
    /// </summary>
    public static class MavF15InertiaBasis
    {
        public static MavMassProperties CreateUnityMassProperties(
            float massKg, float ixKgM2, float iyKgM2, float izKgM2, float ixzKgM2,
            Vector3 centerOfMassLocalM)
        {
            float a = izKgM2;
            float d = ixKgM2;
            float b = ixzKgM2;

            float halfTrace = 0.5f * (a + d);
            float halfDifference = 0.5f * (a - d);
            float root = Mathf.Sqrt(halfDifference * halfDifference + b * b);

            float principalY = halfTrace + root;
            float principalZ = halfTrace - root;
            float rotationAboutUnityXRad = 0.5f * Mathf.Atan2(2f * b, a - d);

            MavMassProperties properties = new MavMassProperties();
            properties.massKg = massKg;
            properties.centerOfMassLocalM = centerOfMassLocalM;
            properties.inertiaTensorKgM2 = new Vector3(iyKgM2, principalY, principalZ);
            properties.inertiaTensorRotationEulerDeg = new Vector3(
                rotationAboutUnityXRad * Mathf.Rad2Deg, 0f, 0f);
            return properties;
        }

        /// <summary>
        /// Sylvester's criterion for the body tensor above: Ixx &gt; 0, Iyy &gt; 0, and
        /// Ixx*Izz &gt; Ixz^2.
        /// </summary>
        public static bool IsPositiveDefinite(double ixx, double iyy, double izz, double ixz)
        {
            if (!(ixx > 0.0) || !(iyy > 0.0) || !(izz > 0.0))
                return false;

            return ixx * izz > ixz * ixz;
        }

        /// <summary>Principal moments of the complete body tensor, ascending.</summary>
        public static void GetPrincipalMoments(
            double ixx, double iyy, double izz, double ixz,
            out double smallest, out double middle, out double largest)
        {
            double halfTrace = 0.5 * (ixx + izz);
            double halfDifference = 0.5 * (ixx - izz);
            double root = System.Math.Sqrt(halfDifference * halfDifference + ixz * ixz);

            double p = halfTrace - root;
            double q = iyy;
            double r = halfTrace + root;

            if (p > q) { double t = p; p = q; q = t; }
            if (q > r) { double t = q; q = r; r = t; }
            if (p > q) { double t = p; p = q; q = t; }

            smallest = p;
            middle = q;
            largest = r;
        }
    }
}
