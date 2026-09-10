#if UNITY_EDITOR
using UnityEngine;

namespace MaverickFresh.FlightDynamics.EditorTools
{
    /// <summary>
    /// A stand-in legacy physics owner used only by the Unity integration validation suite.
    ///
    /// This file intentionally lives OUTSIDE an Editor folder. Unity refuses AddComponent<T>() for
    /// MonoBehaviours compiled into an editor-only assembly, which made the ownership integration
    /// rig create a null legacy owner and then fail with a misleading ownership error/NRE.
    ///
    /// #if UNITY_EDITOR still keeps the type out of player builds while allowing it to participate
    /// as a normal attachable MonoBehaviour when validation runs inside the editor.
    /// </summary>
    public sealed class MavIntegrationTestLegacyOwner : MonoBehaviour
    {
        public const string TypeName = "MavIntegrationTestLegacyOwner";

        [Tooltip("Counts how many times this stand-in was stepped, so validation can prove whether legacy ownership actually executed.")]
        public int debugStepCount;

        public void StepLegacyOwner()
        {
            if (!enabled)
                return;

            debugStepCount++;
        }
    }
}
#endif
