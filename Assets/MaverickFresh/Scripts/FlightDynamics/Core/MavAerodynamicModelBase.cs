using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>
    /// Aircraft-specific aerodynamic model contract.
    /// Implementations return dimensionless coefficients only; this base class
    /// deliberately does not touch Rigidbody or Unity forces.
    /// </summary>
    public abstract class MavAerodynamicModelBase : MonoBehaviour
    {
        [Header("Reference Geometry")]
        public MavAeroReferenceGeometry referenceGeometry;

        public abstract MavAeroCoefficients Evaluate(
            MavFlightState state,
            MavControlInput input,
            MavAtmosphereSample atmosphere
        );
    }
}
