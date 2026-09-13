using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>
    /// The gyroscopic (inertial coupling) moment -w x (I w), and the one place it is computed.
    ///
    /// ============================ WHY THIS EXISTS
    ///
    /// Euler's rigid-body rotational equation is
    ///
    ///     I w_dot + w x (I w) = M_external
    ///
    /// Phase 5C-R measured what the Maverick Unity Rigidbody path actually integrates, in Unity
    /// 6000.3.16f1 Play Mode, on the F-16 reference inertia. The decisive case applied ZERO external
    /// moment to a body spinning at (p,q,r) = (1.20, 0.45, 0.30) rad/s. Euler predicts an angular
    /// acceleration of 0.5211 rad/s^2 from the coupling term alone; the measured value was
    /// 0.000002 rad/s^2. With a moment applied, the measurement matched the naive w_dot = I^-1 M to
    /// 0.00% and the full Euler solution to 27.84%.
    ///
    /// So the backend this project runs on integrates
    ///
    ///     I w_dot = M_applied
    ///
    /// and the w x (I w) term has to be supplied by Maverick if the reference model is to satisfy the
    /// equation its data was written for. The F-16's Ixz and its large Iz - Ix difference are in the
    /// frozen reference set precisely because roll-yaw inertial coupling is part of how the aircraft
    /// behaves; dropping the term is not a small error at rate.
    ///
    /// SCOPE OF THE CLAIM. This is a compensation for the integration path THIS PROJECT MEASURED, on
    /// this Unity version, with this Rigidbody configuration. It is not a claim about PhysX in
    /// general, about other Unity versions, or about articulations - none of which were measured. If
    /// a future backend does integrate the term, applying it here as well would double-count it, and
    /// the Phase 5C-R U5CR-008 case C is what would catch that: it compares the measured result
    /// against BOTH the full Euler and the naive prediction, and a double correction matches neither.
    /// That test is the guard on this file, and it must keep running.
    ///
    /// ============================ WHERE IT IS APPLIED
    ///
    /// Nowhere near here. This class computes a moment and returns it; it holds no Rigidbody
    /// reference and applies nothing. <see cref="MavSixDoFBody"/> adds the result to the load set it
    /// already composes and applies once, so the aircraft keeps exactly one physical writer. This is
    /// rigid-body rotational dynamics, not aerodynamics: it does NOT belong in Cl, Cm or Cn, and the
    /// Morelli coefficients are untouched by it.
    ///
    /// ============================ FRAME
    ///
    /// Everything here is in AERO BODY axes - X forward, Y right, Z down - because that is the frame
    /// the load set totals, the sourced inertia tensor and the p/q/r rates are all already expressed
    /// in. Choosing that frame means the correction needs no transform of its own: it is summed with
    /// the aerodynamic and propulsive moments and crosses the existing single aero-body-to-Unity
    /// boundary in <see cref="MavFlightDynamicsMath.AeroBodyMomentToUnityLocal"/> with them. There is
    /// no second transform, and no sign correction anywhere but that one boundary.
    /// </summary>
    public static class MavGyroscopicMoment
    {
        /// <summary>
        /// Rebuilds the aero-body inertia matrix from what the Rigidbody is actually carrying.
        ///
        /// Read back from the body rather than from the profile that wrote it, because the tensor the
        /// correction must match is the one the solver is using - if Unity normalised or reordered
        /// anything, the compensation has to be computed against the result, not the intent.
        ///
        /// Both steps come from <see cref="MavInertiaTensorMath"/>, the existing authority: Reconstruct
        /// undoes the principal-axis decomposition, and UnityLocalInertiaToAeroBody undoes the basis
        /// change. Phase 5C-R U5CR-008 verifies that exact round trip reproduces the sourced
        /// body-axis tensor - Ixz included - to 0.0000% of its largest element, which is what makes it
        /// safe to rely on here instead of re-deriving the decomposition a second time.
        /// </summary>
        public static bool TryAeroBodyInertiaFromRigidbody(
            Vector3 principalMomentsKgM2,
            Quaternion principalRotation,
            out MavInertiaMatrix aeroBodyInertia)
        {
            aeroBodyInertia = new MavInertiaMatrix();

            if (!IsFinite(principalMomentsKgM2)
                || float.IsNaN(principalRotation.x) || float.IsNaN(principalRotation.y)
                || float.IsNaN(principalRotation.z) || float.IsNaN(principalRotation.w))
                return false;

            if (principalMomentsKgM2.x <= 0f || principalMomentsKgM2.y <= 0f
                || principalMomentsKgM2.z <= 0f)
                return false;

            MavInertiaMatrix unityLocal = MavInertiaTensorMath.Reconstruct(
                principalMomentsKgM2, principalRotation);
            aeroBodyInertia = MavInertiaTensorMath.UnityLocalInertiaToAeroBody(unityLocal);
            return aeroBodyInertia.IsFinite();
        }

        /// <summary>
        /// The correction moment, in aero body axes:
        ///
        ///     H     = I w                      angular momentum
        ///     Mgyro = -(w x H)
        ///
        /// so that adding it to the external moment makes the backend's I w_dot = M_applied reproduce
        /// I w_dot + w x (I w) = M_external.
        ///
        /// The FULL matrix is used, not diag(Ix, Iy, Iz). For the F-16 the off-diagonal Ixz is what
        /// couples roll into yaw, and a diagonal-only evaluation would silently return a correction
        /// that is wrong in exactly the axis the term exists to fix.
        ///
        /// FAILS CLOSED. A non-finite rate or tensor returns false with a zero moment rather than a
        /// NaN, because a NaN reaching the load set would be refused at the boundary and the aircraft
        /// would simply stop responding, with the cause several layers away from the symptom.
        ///
        /// No deadband and no blend factor. The term is quadratic in w, so it vanishes on its own as
        /// the aircraft stops rotating; suppressing it near zero would be inventing a threshold the
        /// physics does not have.
        /// </summary>
        public static bool TryCompute(
            MavInertiaMatrix inertiaAeroBody,
            Vector3 angularRateAeroBodyRadSec,
            out Vector3 momentAeroBodyNm)
        {
            momentAeroBodyNm = Vector3.zero;

            if (!IsFinite(angularRateAeroBodyRadSec) || !inertiaAeroBody.IsFinite())
                return false;

            Vector3 angularMomentum = MavInertiaTensorMath.Multiply(
                inertiaAeroBody, angularRateAeroBodyRadSec);

            Vector3 gyroscopic = -Vector3.Cross(angularRateAeroBodyRadSec, angularMomentum);
            if (!IsFinite(gyroscopic))
                return false;

            momentAeroBodyNm = gyroscopic;
            return true;
        }

        /// <summary>
        /// The angular acceleration the backend will produce for a given applied moment, i.e.
        /// <c>I^-1 M</c> with no coupling term.
        ///
        /// Exposed so validation can state the naive prediction explicitly and show the corrected
        /// runtime does NOT match it in the cases that discriminate. A test that only compared against
        /// the right answer could not tell a correct implementation from one that happened to agree.
        /// </summary>
        public static bool TryNaiveAngularAcceleration(
            MavInertiaMatrix inertiaAeroBody,
            Vector3 momentAeroBodyNm,
            out Vector3 angularAccelerationRadSec2)
        {
            angularAccelerationRadSec2 = Vector3.zero;

            MavInertiaMatrix inverse;
            if (!MavInertiaTensorMath.TryInvert(inertiaAeroBody, out inverse))
                return false;

            angularAccelerationRadSec2 = MavInertiaTensorMath.Multiply(inverse, momentAeroBodyNm);
            return IsFinite(angularAccelerationRadSec2);
        }

        private static bool IsFinite(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z)
                   && !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
        }
    }
}
