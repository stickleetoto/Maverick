#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using MaverickFresh.FlightDynamics.F16;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>
    /// Shared state for the transient Play Mode FixedUpdate scheduling probe. This is editor-only
    /// despite living outside an Editor folder so its MonoBehaviours participate in the normal
    /// runtime script scheduler while the editor is in Play Mode.
    /// </summary>
    public static class MavFlightDynamicsSchedulerProbeState
    {
        public static readonly List<string> Events = new List<string>(16);
        public static int fixedTick;
        public static bool completed;
        public static bool passed;
        public static string summary = "not run";

        public static void Reset()
        {
            Events.Clear();
            fixedTick = 0;
            completed = false;
            passed = false;
            summary = "running";
        }

        public static void Record(string value)
        {
            if (fixedTick == 2)
                Events.Add(value);
        }
    }

    /// <summary>
    /// Runs immediately before the real control law. Tick 1 is a healthy priming step. On tick 2
    /// it drops the operational command signal and requests a handover on that exact physics step.
    /// </summary>
    [DefaultExecutionOrder(-350)]
    public sealed class MavSchedulerBeforeLawProbe : MonoBehaviour
    {
        public MavManualPilotCommandSource commandSource;
        public MavPhysicsOwnershipController ownership;

        private void FixedUpdate()
        {
            MavFlightDynamicsSchedulerProbeState.fixedTick++;
            int tick = MavFlightDynamicsSchedulerProbeState.fixedTick;

            if (tick == 2)
            {
                MavFlightDynamicsSchedulerProbeState.Record("before-law");
                commandSource.commandAvailable = false;
                ownership.RequestTransitionToNewFdm();
            }
        }
    }

    /// <summary>Observes the real control law after its -300 FixedUpdate.</summary>
    [DefaultExecutionOrder(-275)]
    public sealed class MavSchedulerAfterLawProbe : MonoBehaviour
    {
        public MavF16ControlLawV01 controlLaw;

        private void FixedUpdate()
        {
            if (MavFlightDynamicsSchedulerProbeState.fixedTick != 2)
                return;

            MavFlightDynamicsSchedulerProbeState.Record(
                controlLaw.debugCommandResolution
                    == MavFlightControlLawBase.MavCommandResolution.SignalLossPolicy
                    ? "after-law:dropout"
                    : "after-law:STALE"
            );
        }
    }

    /// <summary>Observes the real ownership arbiter after its -250 FixedUpdate.</summary>
    [DefaultExecutionOrder(-225)]
    public sealed class MavSchedulerAfterOwnershipProbe : MonoBehaviour
    {
        public MavPhysicsOwnershipController ownership;
        public MavSixDoFBody body;
        public MavSchedulerLegacyOwner legacyOwner;

        private void FixedUpdate()
        {
            if (MavFlightDynamicsSchedulerProbeState.fixedTick != 2)
                return;

            bool safe = ownership.state != MavPhysicsOwnershipState.NewOwned
                        && legacyOwner != null && legacyOwner.enabled
                        && !body.simulationEnabled;

            MavFlightDynamicsSchedulerProbeState.Record(
                safe ? "after-ownership:legacy" : "after-ownership:BAD"
            );
        }
    }

    /// <summary>Observes the real body after its -100 FixedUpdate.</summary>
    [DefaultExecutionOrder(-50)]
    public sealed class MavSchedulerAfterBodyProbe : MonoBehaviour
    {
        public MavSixDoFBody body;

        private void FixedUpdate()
        {
            if (MavFlightDynamicsSchedulerProbeState.fixedTick != 2)
                return;

            MavFlightDynamicsSchedulerProbeState.Record(
                !body.simulationEnabled ? "after-body:new-disarmed" : "after-body:BAD"
            );
        }
    }

    /// <summary>
    /// Legacy-owner stand-in that actually receives Unity FixedUpdate at order 0. It applies no
    /// force; its enabled state and callback count are enough to prove which owner would execute.
    /// </summary>
    [DefaultExecutionOrder(0)]
    public sealed class MavSchedulerLegacyOwner : MonoBehaviour
    {
        public const string TypeName = "MavSchedulerLegacyOwner";
        public int fixedStepCount;

        private void FixedUpdate()
        {
            fixedStepCount++;
            MavFlightDynamicsSchedulerProbeState.Record("legacy-fixedupdate");
        }
    }

    /// <summary>Final observer after all production owners have had their FixedUpdate opportunity.</summary>
    [DefaultExecutionOrder(50)]
    public sealed class MavSchedulerEndProbe : MonoBehaviour
    {
        public MavF16ControlLawV01 controlLaw;
        public MavPhysicsOwnershipController ownership;
        public MavSixDoFBody body;
        public MavSchedulerLegacyOwner legacyOwner;

        private void FixedUpdate()
        {
            if (MavFlightDynamicsSchedulerProbeState.fixedTick != 2
                || MavFlightDynamicsSchedulerProbeState.completed)
            {
                return;
            }

            MavFlightDynamicsSchedulerProbeState.Record("end");

            bool lawCurrent = controlLaw.debugCommandResolution
                              == MavFlightControlLawBase.MavCommandResolution.SignalLossPolicy;
            bool legacyKept = legacyOwner != null && legacyOwner.enabled
                              && legacyOwner.fixedStepCount >= 2;
            bool newStayedOff = !body.simulationEnabled;
            bool notNewOwned = ownership.state != MavPhysicsOwnershipState.NewOwned;

            string sequence = string.Join(" -> ", MavFlightDynamicsSchedulerProbeState.Events.ToArray());
            bool orderedTrace = sequence ==
                "before-law -> after-law:dropout -> after-ownership:legacy -> after-body:new-disarmed -> legacy-fixedupdate -> end";

            MavFlightDynamicsSchedulerProbeState.passed =
                lawCurrent && legacyKept && newStayedOff && notNewOwned && orderedTrace;
            MavFlightDynamicsSchedulerProbeState.summary =
                "trace=" + sequence
                + " | lawCurrent=" + lawCurrent
                + " legacyKept=" + legacyKept
                + " newStayedOff=" + newStayedOff
                + " ownership=" + ownership.state;
            MavFlightDynamicsSchedulerProbeState.completed = true;
        }
    }
}
#endif
