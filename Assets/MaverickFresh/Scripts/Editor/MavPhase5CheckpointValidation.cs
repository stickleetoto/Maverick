#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MaverickFresh.EditorTools
{
    /// <summary>
    /// Convenience checkpoint for the Phase 5 handoff. This intentionally does not replace the
    /// individual suites: each suite remains the authority for its own pass/fail report. The menu
    /// simply runs the four structural validations in the order required before any live FDM handoff.
    ///
    /// Runtime maneuver validation is deliberately NOT included here. High-maneuver cases must be
    /// flown in Play Mode through MavManeuverDiagnostics; an editor-only check cannot certify that
    /// the live Rigidbody and instructor are behaving correctly under load.
    /// </summary>
    public static class MavPhase5CheckpointValidation
    {
        [MenuItem("Maverick/Flight Dynamics/Run Phase 5 Checkpoint Validation")]
        public static void RunValidation()
        {
            Debug.Log("Maverick PHASE 5 CHECKPOINT: running Phase 4B, Phase 5A, AoA ownership, and envelope ownership suites. Review each suite's RESULT line; Play Mode maneuver validation remains separate.");

            MavTurnDynamicsPhase4BValidation.RunValidation();
            MavPhase5AOwnershipValidation.RunValidation();
            MavAoALimiterOwnershipValidation.RunValidation();
            MavEnvelopeProtectionOwnershipValidation.RunValidation();

            Debug.Log("Maverick PHASE 5 CHECKPOINT structural suites invoked. Next gate: Play Mav_InGame with F-16C, verify Legacy ownership, then run MavManeuverDiagnostics before any Phase 5C live handoff.");
        }
    }
}
#endif
