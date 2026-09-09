using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>
    /// Aircraft-independent physical control-surface actuator contract.
    ///
    /// An actuator is the only owner of *actual* surface state. It accepts a commanded
    /// <see cref="MavControlInput"/> (physical deflection degrees plus throttle), applies the
    /// airframe's physical limits and rate limits, and publishes the resulting actual state.
    ///
    /// An actuator must never call Rigidbody.AddForce/AddTorque. Surfaces produce loads only
    /// through the aerodynamic model and <see cref="MavSixDoFBody"/>.
    /// </summary>
    public abstract class MavControlSurfaceActuatorBase : MonoBehaviour
    {
        /// <summary>Sets the commanded surface deflection / throttle. Bounding happens inside the actuator.</summary>
        public abstract void SetCommand(MavControlInput command);

        /// <summary>The current actual surface state after bounding and rate limiting.</summary>
        public abstract MavControlInput ActualSurfaceState { get; }
    }
}
