using System;
using System.Collections.Generic;
using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>
    /// Who owns the aircraft's physics right now.
    /// </summary>
    public enum MavPhysicsOwnershipState
    {
        LegacyOwned = 0,
        TransitionToNew = 1,
        NewOwned = 2,
        TransitionToLegacy = 3,
        Fault = 4,
        Unowned = 5
    }

    /// <summary>Observed facts a handover decision is made from.</summary>
    [Serializable]
    public struct MavOwnershipObservation
    {
        public bool structurallyPrepared;

        [Tooltip("Every operational condition except the legacy-ownership one.")]
        public bool readyExceptLegacyOwnership;

        [Tooltip("Full operational live-readiness, checked after legacy has been released.")]
        public bool operationallyLiveReady;

        [Tooltip("A legacy physics owner is currently enabled on the aircraft.")]
        public bool legacyOwnerActive;

        [Tooltip("A legacy physics owner component exists on the aircraft, enabled or not.")]
        public bool legacyOwnerPresent;

        [Tooltip("This controller had disabled legacy owners that therefore had to be restored.")]
        public bool legacyRestorationWasRequired;

        [Tooltip("Every legacy owner this controller disabled was verifiably re-enabled.")]
        public bool legacyRestorationSucceeded;

        [Tooltip("The new FDM is armed to apply loads.")]
        public bool newFdmArmed;
    }

    /// <summary>Pure ownership rules shared by production and validation.</summary>
    public static class MavPhysicsOwnershipRules
    {
        public static bool ViolatesExclusiveOwnership(bool legacyOwnerActive, bool newFdmArmed)
        {
            return legacyOwnerActive && newFdmArmed;
        }

        public static bool CanBeginTransitionToNew(MavOwnershipObservation observation, out string reason)
        {
            if (!observation.structurallyPrepared)
            {
                reason = "new stack is not structurally prepared";
                return false;
            }

            if (!observation.readyExceptLegacyOwnership)
            {
                reason = "new stack fails an operational precondition other than legacy ownership";
                return false;
            }

            if (observation.newFdmArmed)
            {
                reason = "new FDM is already armed while legacy still owns physics";
                return false;
            }

            reason = "OK";
            return true;
        }

        public static bool IsTransitionToNewComplete(MavOwnershipObservation observation, out string reason)
        {
            if (observation.legacyOwnerActive)
            {
                reason = "a legacy physics owner is still active after the handover";
                return false;
            }

            if (!observation.operationallyLiveReady)
            {
                reason = "the new stack is not operationally live-ready after releasing legacy";
                return false;
            }

            if (!observation.newFdmArmed)
            {
                reason = "the new FDM did not arm";
                return false;
            }

            reason = "OK";
            return true;
        }

        /// <summary>
        /// Resolves a settled non-new ownership state.
        ///
        /// Restoration failure has priority over the observation that some legacy owner is active.
        /// This matters when several owners were released and only a subset can be restored: one
        /// surviving owner must not launder a partial restoration into LegacyOwned.
        /// </summary>
        public static MavPhysicsOwnershipState ResolveSettledOwnership(
            MavOwnershipObservation observation,
            out string reason)
        {
            if (observation.newFdmArmed)
            {
                reason = "the new FDM is still armed; ownership is not settled on legacy";
                return MavPhysicsOwnershipState.Fault;
            }

            if (observation.legacyRestorationWasRequired && !observation.legacyRestorationSucceeded)
            {
                reason = "one or more legacy owners this controller disabled could not be restored";
                return MavPhysicsOwnershipState.Fault;
            }

            if (observation.legacyOwnerActive)
            {
                reason = "a legacy physics owner is active";
                return MavPhysicsOwnershipState.LegacyOwned;
            }

            if (observation.legacyRestorationWasRequired)
            {
                reason = "legacy restoration reported success but no legacy owner is active";
                return MavPhysicsOwnershipState.Fault;
            }

            if (observation.legacyOwnerPresent)
            {
                reason = "a legacy physics owner exists on this aircraft but is not active, so nothing owns its physics";
                return MavPhysicsOwnershipState.Fault;
            }

            reason = "this aircraft has no legacy physics owner and none was ever released; nothing owns its physics by design";
            return MavPhysicsOwnershipState.Unowned;
        }

        public static bool ShouldReturnToLegacy(MavOwnershipObservation observation, out string reason)
        {
            if (observation.legacyOwnerActive)
            {
                reason = "a legacy physics owner was re-enabled while the new FDM owned physics";
                return true;
            }

            if (!observation.structurallyPrepared)
            {
                reason = "the new stack lost structural preparation";
                return true;
            }

            if (!observation.operationallyLiveReady)
            {
                reason = "the new stack lost operational live-readiness";
                return true;
            }

            reason = "OK";
            return false;
        }

        public static bool IsStateConsistent(
            MavPhysicsOwnershipState state,
            MavOwnershipObservation observation,
            out string reason)
        {
            if (ViolatesExclusiveOwnership(observation.legacyOwnerActive, observation.newFdmArmed))
            {
                reason = "EXCLUSIVE OWNERSHIP VIOLATED: legacy and new FDM are both active";
                return false;
            }

            switch (state)
            {
                case MavPhysicsOwnershipState.LegacyOwned:
                    if (observation.newFdmArmed)
                    {
                        reason = "state says legacy owns physics but the new FDM is armed";
                        return false;
                    }
                    if (!observation.legacyOwnerActive)
                    {
                        reason = "state says legacy owns physics but no legacy owner is active";
                        return false;
                    }
                    break;

                case MavPhysicsOwnershipState.Unowned:
                    if (observation.newFdmArmed || observation.legacyOwnerActive)
                    {
                        reason = "state says nothing owns physics but an owner is active";
                        return false;
                    }
                    if (observation.legacyOwnerPresent)
                    {
                        reason = "state says nothing owns physics, but a legacy owner exists on this aircraft and is merely disabled";
                        return false;
                    }
                    if (observation.legacyRestorationWasRequired)
                    {
                        reason = "state says nothing owns physics by design, but this controller released legacy owners that were never restored";
                        return false;
                    }
                    break;

                case MavPhysicsOwnershipState.NewOwned:
                    if (!observation.newFdmArmed)
                    {
                        reason = "state says the new FDM owns physics but it is not armed";
                        return false;
                    }
                    if (observation.legacyOwnerActive)
                    {
                        reason = "state says the new FDM owns physics but a legacy owner is active";
                        return false;
                    }
                    break;
            }

            reason = "OK";
            return true;
        }
    }

    /// <summary>
    /// Explicit, atomic transfer of aircraft physics ownership between the legacy flight stack and
    /// the new six-DoF FDM.
    ///
    /// Execution order is deliberately between the control law and the actuator/body so arbitration
    /// consumes the current step's command-source result while still completing before either
    /// physical owner executes:
    ///
    ///     control law        -300
    ///     ownership arbiter  -250
    ///     actuator           -200
    ///     six-DoF body       -100
    ///     legacy owners         0
    ///
    /// The controller applies no force or torque. It changes only ownership flags/components.
    /// </summary>
    [DefaultExecutionOrder(-250)]
    [DisallowMultipleComponent]
    public sealed class MavPhysicsOwnershipController : MonoBehaviour
    {
        [Header("Target")]
        public MavSixDoFBody sixDoFBody;

        [Header("Safety")]
        [Tooltip("OFF by default. Ownership moves only after an explicit request unless enabled deliberately.")]
        public bool autoTransitionToNewFdm = false;

        [Tooltip("Component type names treated as legacy physics owners.")]
        public string[] legacyPhysicsOwners =
            (string[])MavSixDoFBody.DefaultConflictingLegacyPhysicsComponents.Clone();

        [Header("State")]
        public MavPhysicsOwnershipState state = MavPhysicsOwnershipState.LegacyOwned;
        [TextArea(2, 4)] public string stateReason = "legacy owns physics";

        [Header("Debug / Telemetry")]
        public MavOwnershipObservation debugObservation;
        public int debugSuccessfulTransitions;
        public int debugRolledBackTransitions;
        public int debugReturnsToLegacy;
        public int debugFaults;
        public int debugExclusiveOwnershipViolationsObserved;
        public int debugDisabledLegacyOwnerCount;
        [TextArea] public string debugDisabledLegacyOwners = "none";

        private readonly List<MonoBehaviour> behaviourScratch = new List<MonoBehaviour>(32);

        /// <summary>
        /// Legacy components this controller disabled. The list is intentionally retained after a
        /// failed restoration. That retained list is a restoration debt: ClearFault must retry the
        /// same obligation and may not reinterpret the aircraft as an initially-Unowned bench rig.
        /// </summary>
        private readonly List<MonoBehaviour> disabledByThisController = new List<MonoBehaviour>(8);

        private bool transitionRequested;
        private bool returnRequested;

        private void Awake()
        {
            Resolve();
        }

        private void FixedUpdate()
        {
            StepOwnership();
        }

        public void StepOwnership()
        {
            Resolve();
            if (sixDoFBody == null)
            {
                SetState(MavPhysicsOwnershipState.Fault, "no MavSixDoFBody to own physics for");
                return;
            }

            debugObservation = Observe();

            string consistencyReason;
            if (!MavPhysicsOwnershipRules.IsStateConsistent(state, debugObservation, out consistencyReason))
            {
                HandleInconsistency(consistencyReason);
                return;
            }

            switch (state)
            {
                case MavPhysicsOwnershipState.LegacyOwned:
                case MavPhysicsOwnershipState.Unowned:
                    returnRequested = false;
                    if (transitionRequested || autoTransitionToNewFdm)
                    {
                        transitionRequested = false;
                        AttemptTransitionToNew();
                    }
                    break;

                case MavPhysicsOwnershipState.NewOwned:
                    string returnReason;
                    if (returnRequested)
                    {
                        returnRequested = false;
                        ReturnToLegacy("ownership return requested");
                    }
                    else if (MavPhysicsOwnershipRules.ShouldReturnToLegacy(debugObservation, out returnReason))
                    {
                        ReturnToLegacy(returnReason);
                    }
                    break;

                case MavPhysicsOwnershipState.Fault:
                    DisarmNewFdm();
                    break;
            }
        }

        public void RequestTransitionToNewFdm()
        {
            transitionRequested = true;
        }

        public void RequestReturnToLegacy()
        {
            returnRequested = true;
        }

        public void ClearFault()
        {
            if (state != MavPhysicsOwnershipState.Fault)
                return;

            DisarmNewFdm();
            bool restorationRequired;
            bool restored = RestoreLegacyOwners(out restorationRequired);
            SettleAfterRelease("fault cleared by operator", restorationRequired, restored);
        }

        private void AttemptTransitionToNew()
        {
            SetState(MavPhysicsOwnershipState.TransitionToNew, "verifying preconditions");

            string reason;
            if (!MavPhysicsOwnershipRules.CanBeginTransitionToNew(debugObservation, out reason))
            {
                RollBackTransition("precondition failed: " + reason);
                return;
            }

            DisableLegacyOwners();

            debugObservation = Observe();
            if (debugObservation.legacyOwnerActive)
            {
                RollBackTransition("legacy owners could not be released");
                return;
            }

            sixDoFBody.NotifyOwnershipChanged();

            MavFlightDynamicsReadinessReport readiness = sixDoFBody.EvaluateReadinessReport();
            if (!readiness.operationallyLiveReady)
            {
                RollBackTransition("not operationally live-ready after release: " + readiness.summary);
                return;
            }

            sixDoFBody.simulationEnabled = true;

            debugObservation = Observe();
            if (!MavPhysicsOwnershipRules.IsTransitionToNewComplete(debugObservation, out reason))
            {
                RollBackTransition("handover could not be confirmed: " + reason);
                return;
            }

            debugSuccessfulTransitions++;
            SetState(MavPhysicsOwnershipState.NewOwned, "new FDM owns physics");
            Debug.Log("[Maverick/FDM/Ownership] Handover complete: new FDM owns physics.", this);
        }

        private void RollBackTransition(string reason)
        {
            debugRolledBackTransitions++;
            DisarmNewFdm();

            bool restorationRequired;
            bool restored = RestoreLegacyOwners(out restorationRequired);
            if (!SettleAfterRelease("rolled back to legacy: " + reason, restorationRequired, restored))
                return;

            Debug.LogWarning("[Maverick/FDM/Ownership] Handover rolled back: " + reason, this);
        }

        private bool SettleAfterRelease(string context, bool restorationRequired, bool restoreSucceeded)
        {
            debugObservation = Observe();
            debugObservation.legacyRestorationWasRequired = restorationRequired;
            debugObservation.legacyRestorationSucceeded = restoreSucceeded;

            if (MavPhysicsOwnershipRules.ViolatesExclusiveOwnership(
                    debugObservation.legacyOwnerActive, debugObservation.newFdmArmed))
            {
                EnterFault("both owners active after " + context);
                return false;
            }

            string settleReason;
            MavPhysicsOwnershipState settled =
                MavPhysicsOwnershipRules.ResolveSettledOwnership(debugObservation, out settleReason);

            if (settled == MavPhysicsOwnershipState.Fault)
            {
                EnterFault(settleReason + " (" + context + ")");
                return false;
            }

            SetState(settled, context + " | " + settleReason);
            return true;
        }

        private void ReturnToLegacy(string reason)
        {
            SetState(MavPhysicsOwnershipState.TransitionToLegacy, reason);

            DisarmNewFdm();
            bool restorationRequired;
            bool restored = RestoreLegacyOwners(out restorationRequired);

            debugReturnsToLegacy++;
            if (!SettleAfterRelease("returned from new FDM: " + reason, restorationRequired, restored))
                return;

            Debug.LogWarning("[Maverick/FDM/Ownership] Returned to legacy ownership: " + reason, this);
        }

        private void HandleInconsistency(string reason)
        {
            if (MavPhysicsOwnershipRules.ViolatesExclusiveOwnership(
                    debugObservation.legacyOwnerActive, debugObservation.newFdmArmed))
            {
                debugExclusiveOwnershipViolationsObserved++;
            }

            DisarmNewFdm();
            bool restorationRequired;
            bool restored = RestoreLegacyOwners(out restorationRequired);

            if (!SettleAfterRelease("ownership inconsistency: " + reason, restorationRequired, restored))
                return;

            Debug.LogError("[Maverick/FDM/Ownership] " + reason + " - forced back to legacy.", this);
        }

        private void EnterFault(string reason)
        {
            debugFaults++;
            SetState(MavPhysicsOwnershipState.Fault, reason);
            Debug.LogError("[Maverick/FDM/Ownership] FAULT: " + reason, this);
        }

        private void SetState(MavPhysicsOwnershipState next, string reason)
        {
            state = next;
            stateReason = reason;
        }

        private MavOwnershipObservation Observe()
        {
            MavOwnershipObservation observation = new MavOwnershipObservation();

            if (sixDoFBody == null)
                return observation;

            MavFlightDynamicsReadinessInputs inputs = sixDoFBody.BuildReadinessInputs();
            MavFlightDynamicsReadinessReport full = MavFlightDynamicsReadiness.Evaluate(inputs);
            MavFlightDynamicsReadinessReport withoutLegacy =
                MavFlightDynamicsReadiness.EvaluateAssumingLegacyOwnershipCleared(inputs);

            observation.structurallyPrepared = full.structurallyPrepared;
            observation.operationallyLiveReady = full.operationallyLiveReady;
            observation.readyExceptLegacyOwnership = withoutLegacy.operationallyLiveReady;
            observation.legacyOwnerActive = !inputs.legacyPhysicsOwnershipClear;
            observation.legacyOwnerPresent = AnyLegacyOwnerPresent();
            observation.newFdmArmed = sixDoFBody.simulationEnabled;
            return observation;
        }

        private bool AnyLegacyOwnerPresent()
        {
            behaviourScratch.Clear();
            GetComponents(behaviourScratch);

            for (int i = 0; i < behaviourScratch.Count; i++)
            {
                MonoBehaviour behaviour = behaviourScratch[i];
                if (behaviour == null)
                    continue;

                if (MavSixDoFBody.IsLegacyOwnershipConflict(
                        behaviour.GetType().Name, true, legacyPhysicsOwners))
                {
                    return true;
                }
            }

            return false;
        }

        private void DisarmNewFdm()
        {
            if (sixDoFBody != null)
                sixDoFBody.simulationEnabled = false;
        }

        private void DisableLegacyOwners()
        {
            // A previous failed restoration is sticky. A faulted controller never calls this path,
            // so clearing here is valid only for a fresh transition from a settled state.
            disabledByThisController.Clear();

            behaviourScratch.Clear();
            GetComponents(behaviourScratch);

            for (int i = 0; i < behaviourScratch.Count; i++)
            {
                MonoBehaviour behaviour = behaviourScratch[i];
                if (behaviour == null || !behaviour.enabled)
                    continue;

                if (!MavSixDoFBody.IsLegacyOwnershipConflict(
                        behaviour.GetType().Name, behaviour.enabled, legacyPhysicsOwners))
                {
                    continue;
                }

                behaviour.enabled = false;
                disabledByThisController.Add(behaviour);
            }

            UpdateDisabledOwnerDebug();
        }

        /// <summary>
        /// Re-enables only what this controller disabled. If any restoration fails, the tracking
        /// list is retained so the obligation survives Fault and a later ClearFault cannot turn the
        /// aircraft into an apparently clean Unowned bench rig.
        /// </summary>
        private bool RestoreLegacyOwners(out bool restorationWasRequired)
        {
            restorationWasRequired = disabledByThisController.Count > 0;
            bool allRestored = true;

            for (int i = 0; i < disabledByThisController.Count; i++)
            {
                MonoBehaviour behaviour = disabledByThisController[i];
                if (behaviour == null)
                {
                    allRestored = false;
                    continue;
                }

                behaviour.enabled = true;
                if (!behaviour.enabled)
                    allRestored = false;
            }

            // Clear the debt only after every owner we disabled is verifiably back. On failure the
            // list, including Unity-null destroyed references, is intentionally retained.
            if (allRestored)
                disabledByThisController.Clear();

            UpdateDisabledOwnerDebug();
            return allRestored;
        }

        private void UpdateDisabledOwnerDebug()
        {
            debugDisabledLegacyOwnerCount = disabledByThisController.Count;

            if (disabledByThisController.Count == 0)
            {
                debugDisabledLegacyOwners = "none";
                return;
            }

            System.Text.StringBuilder names = new System.Text.StringBuilder(64);
            for (int i = 0; i < disabledByThisController.Count; i++)
            {
                if (i > 0)
                    names.Append(", ");

                MonoBehaviour behaviour = disabledByThisController[i];
                names.Append(behaviour != null ? behaviour.GetType().Name : "<destroyed>");
            }

            debugDisabledLegacyOwners = names.ToString();
        }

        private void Resolve()
        {
            if (sixDoFBody == null)
                sixDoFBody = GetComponent<MavSixDoFBody>();
        }
    }
}
