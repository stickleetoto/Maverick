using System;
using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>
    /// Explicit rigid-body mass properties for physically meaningful aircraft response.
    /// Inertia values are kg*m^2. This type does not guess aircraft values; aircraft-specific
    /// profiles must provide validated numbers before production use.
    /// </summary>
    [Serializable]
    public class MavMassProperties
    {
        [Min(1f)] public float massKg = 10000f;
        public Vector3 centerOfMassLocalM = Vector3.zero;
        public Vector3 inertiaTensorKgM2 = new Vector3(10000f, 10000f, 10000f);
        public Vector3 inertiaTensorRotationEulerDeg = Vector3.zero;

        public void ApplyTo(Rigidbody rb)
        {
            if (rb == null)
                return;

            rb.mass = Mathf.Max(1f, massKg);
            rb.centerOfMass = centerOfMassLocalM;
            rb.inertiaTensor = new Vector3(
                Mathf.Max(0.001f, inertiaTensorKgM2.x),
                Mathf.Max(0.001f, inertiaTensorKgM2.y),
                Mathf.Max(0.001f, inertiaTensorKgM2.z)
            );
            rb.inertiaTensorRotation = Quaternion.Euler(inertiaTensorRotationEulerDeg);
        }
    }
}
