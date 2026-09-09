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

        public Vector3 totalForceAeroBodyN;
        public Vector3 totalMomentAeroBodyNm;

        [Tooltip("How many times an aerodynamic contribution was added this step. Must be 0 or 1.")]
        public int aerodynamicContributions;

        [Tooltip("How many times a propulsive contribution was added this step. Must be 0 or 1.")]
        public int propulsiveContributions;

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

        /// <summary>
        /// True when no physical source contributed more than once. A false result means an
        /// ownership bug: two components believe they own the same physical effect.
        /// </summary>
        public bool HasSingleOwnerPerSource
        {
            get { return aerodynamicContributions <= 1 && propulsiveContributions <= 1; }
        }

        /// <summary>
        /// Clears the accumulator for a new physics step. Every field is reset explicitly so a
        /// stale total from the previous step can never leak into the current one.
        /// </summary>
        public void BeginStep(int physicsStepIndex)
        {
            aerodynamic = MavAerodynamicLoads.Zero;
            propulsive = MavPropulsiveLoads.Zero;
            totalForceAeroBodyN = Vector3.zero;
            totalMomentAeroBodyNm = Vector3.zero;
            aerodynamicContributions = 0;
            propulsiveContributions = 0;
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
                         + ", propulsion=" + propulsiveContributions + ")";
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
            return IsFiniteVector(totalForceAeroBodyN) && IsFiniteVector(totalMomentAeroBodyNm);
        }

        private static bool IsFiniteVector(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsInfinity(v.x)
                && !float.IsNaN(v.y) && !float.IsInfinity(v.y)
                && !float.IsNaN(v.z) && !float.IsInfinity(v.z);
        }
    }
}
