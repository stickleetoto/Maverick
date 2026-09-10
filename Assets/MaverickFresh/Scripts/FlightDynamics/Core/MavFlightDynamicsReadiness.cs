using System;
using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>
    /// How ready the new flight-dynamics path is to own the aircraft.
    ///
    /// The two prepared levels are deliberately different questions:
    ///
    ///   STRUCTURALLY_PREPARED    "are the parts present and wired?"
    ///   OPERATIONALLY_LIVE_READY "would handing this the aircraft actually be correct?"
    ///
    /// Phase 1 conflated them: every component being non-null reported READY. That is not a safety
    /// gate, because a stack can be perfectly wired and still be wrong to fly - a control law
    /// component that is disabled, a propulsion model with no thrust data, no command source at
    /// all, or a legacy physics owner still applying forces to the same Rigidbody.
    /// </summary>
    public enum MavFlightDynamicsReadinessLevel
    {
        NotPrepared = 0,
        StructurallyPrepared = 1,
        OperationallyLiveReady = 2
    }

    /// <summary>
    /// Facts about a flight-dynamics stack, gathered by the caller and judged by
    /// <see cref="MavFlightDynamicsReadiness"/>.
    ///
    /// This is a plain data struct with no Unity lookups so the readiness rule itself is a pure
    /// function that validation can exercise exhaustively, with no GameObject in sight.
    /// </summary>
    [Serializable]
    public struct MavFlightDynamicsReadinessInputs
    {
        [Header("Structural: are the parts present?")]
        public bool hasRigidbody;
        public bool hasValidProfile;
        public bool hasAerodynamicModel;
        public bool hasControlSurfaceActuator;
        public bool hasControlLaw;
        public bool hasPropulsionModel;

        [Header("Operational: would flying this be correct?")]
        [Tooltip("Aerodynamic reference geometry is non-degenerate and matches the physical profile.")]
        public bool aerodynamicGeometryMatchesProfile;

        [Tooltip("The control-law component is enabled and actually driving THIS body's actuator each physics step.")]
        public bool controlLawEnabledAndDriving;

        [Tooltip("The control law reads THIS six-DoF body. Identity, not presence: a law that drives this actuator while reading a different body is flying on someone else's state.")]
        public bool controlLawBoundToThisBody;

        [Tooltip("The actuator component is enabled and bound to this six-DoF body.")]
        public bool actuatorEnabledAndBound;

        [Tooltip("Propulsion output is either authoritative, or non-authoritative and explicitly accepted by an operator.")]
        public bool propulsionAccepted;

        [Tooltip("The command source this body inspects is the SAME object the control law actually reads.")]
        public bool commandSourceIdentityMatches;

        [Tooltip("A pilot-command source exists, is enabled, declares operational capability, and the control law observed a command from it this step.")]
        public bool hasValidCommandSource;

        [Tooltip("No legacy physics owner is enabled on the same Rigidbody.")]
        public bool legacyPhysicsOwnershipClear;

        /// <summary>
        /// Packs the readiness flags into an int so a caller can cheaply detect that nothing has
        /// changed. <see cref="MavFlightDynamicsReadiness.Evaluate"/> builds explanatory strings,
        /// which is fine once but not fifty times a second, so the per-step path re-evaluates only
        /// when this value changes.
        /// </summary>
        public int ToBitmask()
        {
            int mask = 0;
            if (hasRigidbody) mask |= 1 << 0;
            if (hasValidProfile) mask |= 1 << 1;
            if (hasAerodynamicModel) mask |= 1 << 2;
            if (hasControlSurfaceActuator) mask |= 1 << 3;
            if (hasControlLaw) mask |= 1 << 4;
            if (hasPropulsionModel) mask |= 1 << 5;
            if (aerodynamicGeometryMatchesProfile) mask |= 1 << 6;
            if (controlLawEnabledAndDriving) mask |= 1 << 7;
            if (controlLawBoundToThisBody) mask |= 1 << 8;
            if (actuatorEnabledAndBound) mask |= 1 << 9;
            if (propulsionAccepted) mask |= 1 << 10;
            if (commandSourceIdentityMatches) mask |= 1 << 11;
            if (hasValidCommandSource) mask |= 1 << 12;
            if (legacyPhysicsOwnershipClear) mask |= 1 << 13;
            return mask;
        }

        public static MavFlightDynamicsReadinessInputs FullyReady
        {
            get
            {
                MavFlightDynamicsReadinessInputs inputs = new MavFlightDynamicsReadinessInputs();
                inputs.hasRigidbody = true;
                inputs.hasValidProfile = true;
                inputs.hasAerodynamicModel = true;
                inputs.hasControlSurfaceActuator = true;
                inputs.hasControlLaw = true;
                inputs.hasPropulsionModel = true;
                inputs.aerodynamicGeometryMatchesProfile = true;
                inputs.controlLawEnabledAndDriving = true;
                inputs.controlLawBoundToThisBody = true;
                inputs.actuatorEnabledAndBound = true;
                inputs.propulsionAccepted = true;
                inputs.commandSourceIdentityMatches = true;
                inputs.hasValidCommandSource = true;
                inputs.legacyPhysicsOwnershipClear = true;
                return inputs;
            }
        }
    }

    /// <summary>Judgement produced from <see cref="MavFlightDynamicsReadinessInputs"/>.</summary>
    [Serializable]
    public struct MavFlightDynamicsReadinessReport
    {
        public MavFlightDynamicsReadinessLevel level;
        public bool structurallyPrepared;
        public bool operationallyLiveReady;

        [Tooltip("Why structural preparation passed or failed.")]
        public string structuralReason;

        [Tooltip("Why operational live-readiness passed or failed. Only meaningful once structurally prepared.")]
        public string operationalReason;

        [TextArea(2, 4)] public string summary;
    }

    /// <summary>
    /// The readiness rule, as a pure function.
    ///
    /// Structural preparation asks only whether the pieces exist. Operational live-readiness adds
    /// every condition that distinguishes "assembled" from "safe to hand the aircraft to", and it
    /// is strictly stronger: operational readiness always implies structural preparation.
    ///
    /// Nothing here enables anything. It reports.
    /// </summary>
    public static class MavFlightDynamicsReadiness
    {
        public static MavFlightDynamicsReadinessReport Evaluate(MavFlightDynamicsReadinessInputs inputs)
        {
            MavFlightDynamicsReadinessReport report = new MavFlightDynamicsReadinessReport();

            string structuralReason;
            bool structural = EvaluateStructural(inputs, out structuralReason);
            report.structurallyPrepared = structural;
            report.structuralReason = structuralReason;

            if (!structural)
            {
                report.level = MavFlightDynamicsReadinessLevel.NotPrepared;
                report.operationallyLiveReady = false;
                report.operationalReason = "not structurally prepared";
                report.summary = "NOT_PREPARED: " + structuralReason;
                return report;
            }

            string operationalReason;
            bool operational = EvaluateOperational(inputs, out operationalReason);
            report.operationallyLiveReady = operational;
            report.operationalReason = operationalReason;
            report.level = operational
                ? MavFlightDynamicsReadinessLevel.OperationallyLiveReady
                : MavFlightDynamicsReadinessLevel.StructurallyPrepared;

            report.summary = operational
                ? "OPERATIONALLY_LIVE_READY: " + operationalReason
                : "STRUCTURALLY_PREPARED (not live-ready): " + operationalReason;

            return report;
        }

        /// <summary>Presence and wiring only. This is the Phase 1 question, kept intact.</summary>
        public static bool EvaluateStructural(MavFlightDynamicsReadinessInputs inputs, out string reason)
        {
            if (!inputs.hasRigidbody)
            {
                reason = "no Rigidbody";
                return false;
            }

            if (!inputs.hasValidProfile)
            {
                reason = "no valid physical flight-dynamics profile";
                return false;
            }

            if (!inputs.hasAerodynamicModel)
            {
                reason = "no aerodynamic model";
                return false;
            }

            if (!inputs.hasControlSurfaceActuator)
            {
                reason = "no control-surface actuator";
                return false;
            }

            if (!inputs.hasControlLaw)
            {
                reason = "no flight control law";
                return false;
            }

            if (!inputs.hasPropulsionModel)
            {
                reason = "no propulsion model";
                return false;
            }

            reason = "STRUCTURALLY_PREPARED";
            return true;
        }

        /// <summary>
        /// Everything beyond presence. Assumes structural preparation has already passed; callers
        /// must not use this on its own, which is why <see cref="Evaluate"/> is the entry point.
        /// </summary>
        public static bool EvaluateOperational(MavFlightDynamicsReadinessInputs inputs, out string reason)
        {
            if (!inputs.aerodynamicGeometryMatchesProfile)
            {
                reason = "aerodynamic reference geometry does not match the physical profile";
                return false;
            }

            if (!inputs.controlLawEnabledAndDriving)
            {
                reason = "control law is not enabled and driving this body's actuator";
                return false;
            }

            // Identity, not presence. A law that drives the right actuator while reading a
            // different body computes its commands from someone else's airspeed, alpha and rates.
            // The surfaces would move, the aircraft would fly, and nothing would look broken.
            if (!inputs.controlLawBoundToThisBody)
            {
                reason = "control law does not read this six-DoF body";
                return false;
            }

            if (!inputs.actuatorEnabledAndBound)
            {
                reason = "control-surface actuator is not enabled and bound to this six-DoF body";
                return false;
            }

            if (!inputs.propulsionAccepted)
            {
                reason = "propulsion model output is not authoritative and has not been explicitly accepted";
                return false;
            }

            // The body and the control law must be talking about the SAME source object. Checking
            // a declaration on one object and observed availability on another would assemble a
            // "valid" command path out of two halves that never met.
            if (!inputs.commandSourceIdentityMatches)
            {
                reason = "the command source this body inspects is not the one the control law reads";
                return false;
            }

            if (!inputs.hasValidCommandSource)
            {
                reason = "no valid operational pilot-command source producing commands this step";
                return false;
            }

            if (!inputs.legacyPhysicsOwnershipClear)
            {
                reason = "a legacy physics owner is still enabled on this Rigidbody";
                return false;
            }

            reason = "OPERATIONALLY_LIVE_READY";
            return true;
        }
    }
}
