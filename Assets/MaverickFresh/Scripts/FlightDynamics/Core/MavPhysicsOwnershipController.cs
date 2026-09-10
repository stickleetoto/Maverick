using System;
using System.Collections.Generic;
using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>
    /// Who owns the aircraft's physics right now.
    ///
    /// The two transition states are not decoration. They exist so that a handover which fails
    /// half-way is a nameable condition with a defined recovery, rather than an aircraft left in
    /// whatever configuration the failure happened to produce.
    /// </summary>
    public enum MavPhysicsOwnershipState
    {
        /// <summary>The legacy flight stack owns physics. The new FDM is disarmed.</summary>
        LegacyOwned = 0,

        /// <summary>Mid-handover to the new FDM. Transient within a single physics step.</summary>
        TransitionToNew = 1,

        /// <summary>The new FDM owns physics. Legacy physical owners are disabled.</summary>
        NewOwned = 2,

        /// <summary>Mid-handover back to legacy. Transient within a single physics step.</summary>
        TransitionToLegacy = 3,

        /// <summary>
        /// NOBODY owns physics, and that is a known, declared configuration - an aircraft with no
        /// legacy physics owner components at all, such as a bench rig.
        ///
        /// This state exists because calling that situation "LegacyOwned" would be a lie: it would
        /// claim a legacy owner is flying the aircraft when none exists. An aircraft that is
        /// supposed to have a legacy owner and does not is a FAULT, not this.
        /// </summary>
        Unowned = 5,

        /// <summary>
        /// Ownership could not be established or restored. The new FDM is disarmed and the
        /// controller stops acting until an operator clears the fault.
        /// </summary>
        Fault = 4
    }

    /// <summary>Observed facts a handover decision is made from.</summary>
    [Serializable]
    public struct MavOwnershipObservation
    {
        public bool structurallyPrepared;

        [Tooltip("Every operational condition except the legacy-ownership one. Checked BEFORE legacy is disabled, because that criterion cannot hold until after.")]
        public bool readyExceptLegacyOwnership;

        [Tooltip("Full operational live-readiness, checked AFTER legacy has been released.")]
        public bool operationallyLiveReady;

        [Tooltip("A legacy physics owner is currently enabled on the aircraft.")]
        public bool legacyOwnerActive;

        [Tooltip("A legacy physics owner component EXISTS on the aircraft, enabled or not. Distinguishes 'legacy is switched off' from 'there is no legacy stack here at all'.")]
        public bool legacyOwnerPresent;

        [Tooltip("The new FDM is armed to apply loads.")]
        public bool newFdmArmed;
    }

    /// <summary>
    /// The ownership rules, as pure functions.
    ///
    /// Kept separate from the component so the safety invariant and every transition decision can
    /// be enumerated in validation without a GameObject, a Rigidbody, or a physics step.
    /// </summary>
    public static class MavPhysicsOwnershipRules
    {
        /// <summary>
        /// THE invariant. Two systems applying the same physical effect to one Rigidbody is the
        /// failure this entire mechanism exists to prevent, and it is never acceptable - not for a
        /// frame, not during a handover, not "briefly".
        /// </summary>
        public static bool ViolatesExclusiveOwnership(bool legacyOwnerActive, bool newFdmArmed)
        {
            return legacyOwnerActive && newFdmArmed;
        }

        /// <summary>
        /// Whether a handover to the new FDM may begin.
        ///
        /// Note what is deliberately NOT required here: legacy ownership being clear. It cannot be,
        /// because legacy is still flying the aircraft. Everything else must already hold, so that
        /// disabling legacy physics is never done on an aircraft that was not fit to take over.
        /// </summary>
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

        /// <summary>
        /// Whether a handover has genuinely completed. Every clause is a confirmation of something
        /// the transition attempted, not a restatement of the attempt.
        /// </summary>
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
        /// The state to settle in after legacy ownership has been released or restored.
        ///
        /// This is the rule that stops a false LegacyOwned. Committing to LegacyOwned requires an
        /// actually active legacy owner - not merely "the new FDM is off". The three outcomes are
        /// distinct facts:
        ///
        ///   a legacy owner is active            -> LegacyOwned
        ///   none active, and none exists at all -> Unowned    (a declared bench configuration)
        ///   none active, but one EXISTS         -> Fault      (restore failed; nothing is flying it)
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

            if (observation.legacyOwnerActive)
            {
                reason = "a legacy physics owner is active";
                return MavPhysicsOwnershipState.LegacyOwned;
            }

            if (observation.legacyOwnerPresent)
            {
                reason = "a legacy physics owner exists on this aircraft but could not be "
                         + "restored, so nothing owns its physics";
                return MavPhysicsOwnershipState.Fault;
            }

            reason = "this aircraft has no legacy physics owner; nothing owns its physics by design";
            return MavPhysicsOwnershipState.Unowned;
        }

        /// <summary>
        /// Whether ownership must be handed back. Any of these means the new path can no longer be
        /// trusted with the aircraft.
        /// </summary>
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

        /// <summary>
        /// Whether the observed configuration matches the state the controller believes it is in.
        /// A mismatch means something outside the controller changed ownership behind its back.
        /// </summary>
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
                    // The claim is that LEGACY OWNS PHYSICS. Nothing being active is a different
                    // situation entirely, and calling it LegacyOwned would assert an owner that
                    // does not exist.
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
                        reason = "state says nothing owns physics, but a legacy owner exists on "
                                 + "this aircraft and is merely disabled";
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
    /// This replaces "detect a conflict and refuse to fly" with "perform the handover, or do not
    /// perform it, and never sit in between".
    ///
    /// WHAT THIS COMPONENT IS NOT: it is not a third physics engine. It applies no force, no
    /// torque, and no corrective anything. It decides WHO owns physics and flips exactly the flags
    /// that express that. If a handover would leave the aircraft in a bad state, it does not
    /// happen.
    ///
    /// ATOMICITY. The controller runs at execution order -20000, ahead of the control law (-300),
    /// the actuator (-200), MavSixDoFBody (-100) and any legacy owner (default 0). The whole
    /// sequence - verify, release legacy, confirm released, recompute readiness, arm, confirm -
    /// completes inside a single FixedUpdate, before any physics owner has run that step. So:
    ///
    ///   - no physics step ever executes with BOTH owners active, and
    ///   - no physics step ever executes with NEITHER owner active,
    ///
    /// even though there is a window inside the sequence where neither is armed. The window exists
    /// between statements, not between physics steps.
    ///
    /// ROLLBACK. Every failure path restores the previous safe owner: the legacy components this
    /// controller disabled are re-enabled, and the new FDM is disarmed. Only components this
    /// controller disabled are ever re-enabled, so it cannot switch on something a designer had
    /// deliberately turned off.
    ///
    /// DEFAULTS. autoTransitionToNewFdm is OFF and simulationEnabled stays OFF. Nothing here takes
    /// ownership of an aircraft unless something explicitly asks it to.
    /// </summary>
    [DefaultExecutionOrder(-20000)]
    [DisallowMultipleComponent]
    public sealed class MavPhysicsOwnershipController : MonoBehaviour
    {
        [Header("Target")]
        public MavSixDoFBody sixDoFBody;

        [Header("Safety")]
        [Tooltip("OFF by default. When false the controller never initiates a handover by itself; ownership only moves when RequestTransitionToNewFdm is called.")]
        public bool autoTransitionToNewFdm = false;

        [Tooltip("Component type names treated as legacy physics owners. Defaults to the same list MavSixDoFBody uses for conflict detection. Names that do not exist in the project are harmless: matching is by type name, so listing a type defensively costs nothing.")]
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
        /// Legacy components THIS controller disabled. Rollback re-enables only these, so a
        /// component a designer had deliberately left off is never switched on by a failed
        /// handover.
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
            Resolve();
            if (sixDoFBody == null)
            {
                SetState(MavPhysicsOwnershipState.Fault, "no MavSixDoFBody to own physics for");
                return;
            }

            debugObservation = Observe();

            // Consistency first. If something outside this controller has changed ownership behind
            // its back, that is discovered before any decision is made on stale beliefs.
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
                    // Fail closed and stay closed: the new FDM is held disarmed until an operator
                    // clears the fault deliberately.
                    DisarmNewFdm();
                    break;
            }
        }

        /// <summary>Asks for a handover to the new FDM on the next physics boundary.</summary>
        public void RequestTransitionToNewFdm()
        {
            transitionRequested = true;
        }

        /// <summary>Asks for ownership to be handed back to legacy on the next physics boundary.</summary>
        public void RequestReturnToLegacy()
        {
            returnRequested = true;
        }

        /// <summary>
        /// Clears a fault and returns the aircraft to legacy ownership. Deliberate operator action:
        /// a fault is not cleared by time passing or by the condition happening to go away.
        /// </summary>
        public void ClearFault()
        {
            if (state != MavPhysicsOwnershipState.Fault)
                return;

            DisarmNewFdm();
            bool restored = RestoreLegacyOwners();

            // Clearing a fault does not assert an owner into existence. The settled state is
            // whatever is actually true afterwards, and it can legitimately remain Fault.
            SettleAfterRelease("fault cleared by operator", restored);
        }

        /// <summary>
        /// The full handover sequence, start to finish, inside one physics boundary.
        ///
        /// Every step that changes something is followed by a step that confirms it changed. A
        /// failure at any point rolls back to legacy ownership rather than continuing.
        /// </summary>
        private void AttemptTransitionToNew()
        {
            SetState(MavPhysicsOwnershipState.TransitionToNew, "verifying preconditions");

            // 1. Everything except the legacy-ownership criterion, which cannot hold yet.
            string reason;
            if (!MavPhysicsOwnershipRules.CanBeginTransitionToNew(debugObservation, out reason))
            {
                RollBackTransition("precondition failed: " + reason);
                return;
            }

            // 2. Release legacy physical ownership.
            DisableLegacyOwners();

            // 3. Confirm it was actually released, by re-observing rather than by assuming.
            debugObservation = Observe();
            if (debugObservation.legacyOwnerActive)
            {
                RollBackTransition("legacy owners could not be released");
                return;
            }

            // 4. Invalidate the cached readiness so the next evaluation reflects the release.
            sixDoFBody.NotifyOwnershipChanged();

            // 5. Now that legacy is clear, full operational live-readiness must hold.
            MavFlightDynamicsReadinessReport readiness = sixDoFBody.EvaluateReadinessReport();
            if (!readiness.operationallyLiveReady)
            {
                RollBackTransition("not operationally live-ready after release: " + readiness.summary);
                return;
            }

            // 6. Arm the new FDM.
            sixDoFBody.simulationEnabled = true;

            // 7. Confirm the resulting ownership, including the exclusivity invariant.
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
            bool restored = RestoreLegacyOwners();

            if (!SettleAfterRelease("rolled back to legacy: " + reason, restored))
                return;

            Debug.LogWarning("[Maverick/FDM/Ownership] Handover rolled back: " + reason, this);
        }

        /// <summary>
        /// Commits to a settled non-new ownership state, having actually VERIFIED what owns
        /// physics rather than inferring it from the new FDM being off.
        ///
        /// Returns false when the outcome was a fault, so callers can stop.
        /// </summary>
        private bool SettleAfterRelease(string context, bool restoreSucceeded)
        {
            debugObservation = Observe();

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

            if (!restoreSucceeded && settled != MavPhysicsOwnershipState.Unowned)
            {
                EnterFault("legacy owners could not all be restored (" + context + ")");
                return false;
            }

            SetState(settled, context + " | " + settleReason);
            return true;
        }

        private void ReturnToLegacy(string reason)
        {
            SetState(MavPhysicsOwnershipState.TransitionToLegacy, reason);

            // Disarm FIRST. If a legacy owner has been re-enabled behind our back, this is the
            // statement that ends the double-ownership condition, and it runs before any physics
            // owner executes this step.
            DisarmNewFdm();
            bool restored = RestoreLegacyOwners();

            debugReturnsToLegacy++;
            if (!SettleAfterRelease("returned from new FDM: " + reason, restored))
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

            // The safe direction is always to disarm the new path: legacy flying alone is a
            // supported configuration, both flying together is not.
            DisarmNewFdm();
            bool restored = RestoreLegacyOwners();

            if (!SettleAfterRelease("ownership inconsistency: " + reason, restored))
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

        /// <summary>
        /// Whether a legacy physics owner component EXISTS on this aircraft, enabled or not.
        ///
        /// This is what separates "legacy is switched off" from "this aircraft has no legacy stack
        /// at all". The first, with nothing else flying it, is a fault; the second is a declared
        /// bench configuration.
        /// </summary>
        private bool AnyLegacyOwnerPresent()
        {
            behaviourScratch.Clear();
            GetComponents(behaviourScratch);

            for (int i = 0; i < behaviourScratch.Count; i++)
            {
                MonoBehaviour behaviour = behaviourScratch[i];
                if (behaviour == null)
                    continue;

                // enabled:true so presence is judged regardless of the current enabled flag.
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
        /// Re-enables only what this controller disabled. A legacy component that was already off
        /// before the handover stays off: restoring it would be this controller inventing a
        /// configuration nobody asked for.
        /// </summary>
        private bool RestoreLegacyOwners()
        {
            bool allRestored = true;

            for (int i = 0; i < disabledByThisController.Count; i++)
            {
                MonoBehaviour behaviour = disabledByThisController[i];

                // A destroyed component reads as null through Unity's overloaded operator. It
                // cannot be restored, and pretending otherwise would leave the aircraft with no
                // owner while the controller claimed legacy had it back.
                if (behaviour == null)
                {
                    allRestored = false;
                    continue;
                }

                behaviour.enabled = true;

                // Verify rather than assume: another component may re-disable it in its own
                // OnEnable, or the object may be inactive.
                if (!behaviour.enabled)
                    allRestored = false;
            }

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
