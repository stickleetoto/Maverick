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

        /// <summary>
        /// The six-DoF body this actuator publishes its actual surface state into, or null when it
        /// is unbound or the implementation does not report a binding.
        ///
        /// Defaults to null deliberately. Operational live-readiness requires a confirmed binding,
        /// so an actuator that does not answer this question fails closed rather than being
        /// assumed correctly wired.
        /// </summary>
        public virtual MavSixDoFBody BoundBody
        {
            get { return null; }
        }
    }
}
