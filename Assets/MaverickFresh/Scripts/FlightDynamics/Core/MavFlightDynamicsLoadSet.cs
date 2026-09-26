using System;
using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>
    /// Single-physics-step accumulator for every dimensional load acting on the aircraft,
    /// expressed in conventional aircraft body axes (X forward, Y right, Z down).
    ///
    /// The purpose of this type is ownership observability. Each physical source may
    /// contribute exactly once per step, and the accumulated total may be applied to the
    /// Rigidbody exactly once. Contribution counters and the applied flag turn
    /// "did something apply this force twice?" from a guess into a check.
    ///
    /// Units: force = N, moment = N*m. No coefficients and no dynamic pressure appear
    /// here; dimensionalization happens before a load reaches this accumulator.
    /// </summary>
    [Serializable]
    public struct MavFlightDynamicsLoadSet
    {
        public MavAerodynamicLoads aerodynamic;
        public MavPropulsiveLoads propulsive;

        [Tooltip("Rigid-body inertial coupling, -w x (I w), in aero body axes. NOT an aerodynamic or propulsive load: it compensates for the angular integration the measured Unity Rigidbody path performs. See MavGyroscopicMoment.")]
        public Vector3 inertialCorrectionMomentAeroBodyNm;

        public Vector3 totalForceAeroBodyN;
        public Vector3 totalMomentAeroBodyNm;

        [Tooltip("How many times an aerodynamic contribution was added this step. Must be 0 or 1.")]
        public int aerodynamicContributions;

        [Tooltip("How many times a propulsive contribution was added this step. Must be 0 or 1.")]
        public int propulsiveContributions;

        [Tooltip("How many times the inertial coupling correction was added this step. Must be 0 or 1.")]
        public int inertialContributions;

        [Tooltip("Gravity, in aero body axes, ONLY when a research environment owns gravity through the load set (Rigidbody.useGravity off). Zero otherwise. Deliberately NOT part of totalForceAeroBodyN, which stays the non-gravitational (specific) force an accelerometer feels.")]
        public Vector3 gravitationalForceAeroBodyN;

        [Tooltip("How many times gravity was added this step. Must be 0 or 1, and is always 0 unless the environment owns gravity.")]
        public int gravitationalContributions;

        [Tooltip("Set once the accumulated total has been handed to the load-application boundary.")]
        public bool applied;

        [Tooltip("Physics step index this accumulator was opened for. Used to reject stale/duplicate application.")]
        public int stepIndex;

        public bool HasAerodynamic
        {
            get { return aerodynamicContributions > 0; }
        }

        public bool HasPropulsive
        {
            get { return propulsiveContributions > 0; }
        }

        public bool HasInertialCorrection
        {
            get { return inertialContributions > 0; }
        }

        public bool HasGravitational
        {
            get { return gravitationalContributions > 0; }
        }

        /// <summary>
        /// The force handed to the Rigidbody: the non-gravitational total, plus gravity when - and
        /// only when - the load set owns gravity. Bit-identical to <see cref="totalForceAeroBodyN"/>
        /// whenever gravity is Unity's, which is every aircraft except an opted-in research one.
        /// </summary>
        public Vector3 AppliedForceAeroBodyN
        {
            get
            {
                return gravitationalContributions > 0
                    ? totalForceAeroBodyN + gravitationalForceAeroBodyN
                    : totalForceAeroBodyN;
            }
        }

        /// <summary>
        /// The moment from EXTERNAL sources only - aerodynamics and propulsion - with the inertial
        /// coupling correction excluded.
        ///
        /// Kept separable because the correction is not a load acting on the aircraft; it is the term
        /// the backend's integrator omits. Reporting one number would make it impossible to tell a
        /// large aerodynamic moment from a large coupling term, and those two call for opposite
        /// responses.
        /// </summary>
        public Vector3 ExternalMomentAeroBodyNm
        {
            get { return totalMomentAeroBodyNm - inertialCorrectionMomentAeroBodyNm; }
        }

        /// <summary>
        /// True when no physical source contributed more than once. A false result means an
        /// ownership bug: two components believe they own the same physical effect.
        /// </summary>
        public bool HasSingleOwnerPerSource
        {
            get
            {
                return aerodynamicContributions <= 1
                       && propulsiveContributions <= 1
                       && inertialContributions <= 1
                       && gravitationalContributions <= 1;
            }
        }

        /// <summary>
        /// Clears the accumulator for a new physics step. Every field is reset explicitly so a
        /// stale total from the previous step can never leak into the current one.
        /// </summary>
        public void BeginStep(int physicsStepIndex)
        {
            aerodynamic = MavAerodynamicLoads.Zero;
            propulsive = MavPropulsiveLoads.Zero;
            inertialCorrectionMomentAeroBodyNm = Vector3.zero;
            totalForceAeroBodyN = Vector3.zero;
            totalMomentAeroBodyNm = Vector3.zero;
            aerodynamicContributions = 0;
            propulsiveContributions = 0;
            inertialContributions = 0;
            gravitationalForceAeroBodyN = Vector3.zero;
            gravitationalContributions = 0;
            applied = false;
            stepIndex = physicsStepIndex;
        }

        /// <summary>
        /// Adds the aerodynamic contribution. Returns false (and adds nothing) if aerodynamic
        /// loads were already contributed this step or the set was already applied.
        /// </summary>
        public bool AddAerodynamic(MavAerodynamicLoads loads)
        {
            if (applied || aerodynamicContributions > 0)
            {
                aerodynamicContributions++;
                return false;
            }

            aerodynamic = loads;
            aerodynamicContributions = 1;
            totalForceAeroBodyN += loads.forceAeroBodyN;
            totalMomentAeroBodyNm += loads.momentAeroBodyNm;
            return true;
        }

        /// <summary>
        /// Adds the propulsive contribution. Returns false (and adds nothing) if propulsive
        /// loads were already contributed this step or the set was already applied.
        /// </summary>
        public bool AddPropulsive(MavPropulsiveLoads loads)
        {
            if (applied || propulsiveContributions > 0)
            {
                propulsiveContributions++;
                return false;
            }

            propulsive = loads;
            propulsiveContributions = 1;
            totalForceAeroBodyN += loads.forceAeroBodyN;
            totalMomentAeroBodyNm += loads.momentAeroBodyNm;
            return true;
        }

        /// <summary>
        /// Adds the rigid-body inertial coupling correction. Returns false (and adds nothing) if it
        /// was already contributed this step or the set was already applied.
        ///
        /// A MOMENT ONLY. The coupling term produces no force, so nothing is added to the force total
        /// and the specific-force channel is unaffected - an accelerometer does not feel it.
        /// </summary>
        public bool AddInertialCorrection(Vector3 momentAeroBodyNm)
        {
            if (applied || inertialContributions > 0)
            {
                inertialContributions++;
                return false;
            }

            inertialCorrectionMomentAeroBodyNm = momentAeroBodyNm;
            inertialContributions = 1;
            totalMomentAeroBodyNm += momentAeroBodyNm;
            return true;
        }

        /// <summary>
        /// Adds gravity as its own channel. Returns false (and adds nothing) if gravity was already
        /// contributed this step or the set was already applied.
        ///
        /// Called only by <see cref="MavSixDoFBody"/>, and only when the environment owns gravity
        /// through the load set - in which case Unity's own gravity is off, so the aircraft still
        /// feels gravity exactly once. It is NOT added to <see cref="totalForceAeroBodyN"/>: gravity
        /// is not a specific force, and an accelerometer does not measure it.
        /// </summary>
        public bool AddGravitational(Vector3 forceAeroBodyN)
        {
            if (applied || gravitationalContributions > 0)
            {
                gravitationalContributions++;
                return false;
            }

            gravitationalForceAeroBodyN = forceAeroBodyN;
            gravitationalContributions = 1;
            return true;
        }

        /// <summary>
        /// Marks the accumulated total as handed to the Rigidbody boundary.
        /// Returns false when application must be refused: already applied, or a source
        /// contributed more than once.
        /// </summary>
        public bool TryMarkApplied(out string reason)
        {
            if (applied)
            {
                reason = "load set was already applied this physics step";
                return false;
            }

            if (!HasSingleOwnerPerSource)
            {
                reason = "duplicate load contribution detected (aero="
                         + aerodynamicContributions
                         + ", propulsion=" + propulsiveContributions
                         + ", inertial=" + inertialContributions
                         + (gravitationalContributions > 0 ? ", gravity=" + gravitationalContributions : "")
                         + ")";
                return false;
            }

            applied = true;
            reason = "OK";
            return true;
        }

        /// <summary>
        /// True when at least one finite, non-degenerate load channel is present.
        /// NaN/Infinity in a load is treated as invalid so it can be refused before it
        /// reaches the Rigidbody and destroys the simulation state.
        /// </summary>
        public bool IsFinite()
        {
            return IsFiniteVector(totalForceAeroBodyN) && IsFiniteVector(totalMomentAeroBodyNm)
                && IsFiniteVector(gravitationalForceAeroBodyN);
        }

        private static bool IsFiniteVector(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsInfinity(v.x)
                && !float.IsNaN(v.y) && !float.IsInfinity(v.y)
                && !float.IsNaN(v.z) && !float.IsInfinity(v.z);
        }
    }
}
