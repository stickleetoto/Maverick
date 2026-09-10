#if UNITY_EDITOR
using UnityEngine;

namespace MaverickFresh.FlightDynamics.EditorTools
{
    /// <summary>
    /// A stand-in legacy physics owner, used only by the integration validation suite.
    ///
    /// It applies no physics of any kind - it exists purely to be something the ownership
    /// controller can find, disable, verify, restore and fail to restore. The controller matches
    /// legacy owners by type NAME against a configurable deny-list, so the suite points that list
    /// at this type instead of at the real legacy stack. That keeps every ownership test away from
    /// components that actually fly the aircraft.
    ///
    /// It lives in an Editor folder, so it is never compiled into a build.
    /// </summary>
    public sealed class MavIntegrationTestLegacyOwner : MonoBehaviour
    {
        /// <summary>The deny-list entry the integration suite configures controllers with.</summary>
        public const string TypeName = "MavIntegrationTestLegacyOwner";

        [Tooltip("Counts how many times this stand-in was stepped, so a test can show it was or was not running.")]
        public int debugStepCount;

        /// <summary>
        /// Stands in for whatever a real legacy owner would do to the Rigidbody. It deliberately
        /// does nothing but count, because the point of these tests is WHO owns physics, not what
        /// the owner does with it.
        /// </summary>
        public void StepLegacyOwner()
        {
            if (!enabled)
                return;

            debugStepCount++;
        }
    }
}
#endif
