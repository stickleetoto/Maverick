using UnityEditor;
using MaverickFresh.FlightDynamics.F16;

namespace MaverickFresh.FlightDynamics.EditorTools
{
    /// <summary>
    /// Route 2 of the F-16 engine-law registration lifecycle: the editor, on domain reload.
    ///
    /// This is a separate file in the EDITOR assembly on purpose. The obvious alternative is a
    /// `#if UNITY_EDITOR` block inside the runtime registrar, and that does keep `UnityEditor` out of
    /// a player build - but while the editor is running, the runtime assembly still carries a
    /// reference to an editor-only API. Keeping it here means no runtime script names `UnityEditor` at
    /// all, so the question of leakage does not arise rather than being argued about.
    ///
    /// It calls the same public entry point the runtime hook calls, so there is one registration body
    /// and not two that could drift apart.
    ///
    /// Without this, an editor validation run that never entered play mode would depend on route 3 -
    /// the explicit call inside profile construction - which does work, but only for callers that
    /// build a profile. This makes the law available to anything in the editor.
    /// </summary>
    public static class MavF16EngineLawRegistrarEditor
    {
        [InitializeOnLoadMethod]
        private static void RegisterInEditor()
        {
            MavF16EngineLawRegistrar.EnsureRegistered();
        }
    }
}
