using System.Text;
using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>
    /// Whether the replacement F-16 stack is fit to be given the aircraft.
    ///
    /// Every field is a separate fact that has to be true, and each one is named after the thing that
    /// would be wrong if it were false. A single "ready" boolean would let a caller assert readiness;
    /// this makes it enumerate why.
    ///
    /// Deliberately a plain struct with no behaviour beyond judging itself, so the readiness rule can
    /// be tested without a scene, a Rigidbody or a physics step.
    /// </summary>
    [System.Serializable]
    public struct MavReplacementReadiness
    {
        [Tooltip("The aircraft identity has been authoritatively applied (FDM-OWN-009). Never inferred from a serialized field.")]
        public bool aircraftIdentityAuthoritative;

        [Tooltip("The authoritative identity is the F-16C. The replacement stack is F-16 specific.")]
        public bool aircraftIsF16C;

        [Tooltip("A six-DoF body exists to own load application (FDM-OWN-007).")]
        public bool sixDoFBodyPresent;

        [Tooltip("An aerodynamic model is wired and reports itself usable.")]
        public bool aeroModelReady;

        [Tooltip("A control law is wired and bound to this body (FDM-OWN-004).")]
        public bool controlLawReady;

        [Tooltip("An actuator model owns surface positions (FDM-OWN-006).")]
        public bool actuatorReady;

        [Tooltip("Propulsion is acceptable for live flight, or the caller has explicitly accepted non-authoritative thrust. Never fabricated (FDM-OWN-008).")]
        public bool propulsionAcceptable;

        [Tooltip("Gravity is owned exactly once across the whole stack.")]
        public bool gravityOwnedExactlyOnce;

        [Tooltip("The shadow path has actually produced finite telemetry, so the pipeline is known to run rather than merely to be wired.")]
        public bool shadowTelemetryFinite;

        [Tooltip("The shadow path has run for long enough to trust the line above.")]
        public bool shadowRunLongEnough;

        /// <summary>
        /// Judges readiness, naming the first missing condition.
        ///
        /// Order matters for the message, not the answer: identity first, because an unresolved
        /// identity makes every later question meaningless - a stack validated against the F-16 being
        /// handed an aircraft that turned out to be something else is the failure mode that produced
        /// commit 0851f4f.
        /// </summary>
        public bool IsReady(out string error)
        {
            if (!aircraftIdentityAuthoritative)
            {
                error = "aircraft identity has not been authoritatively applied, so there is no way "
                        + "to know which aircraft the replacement stack would be flying";
                return false;
            }

            if (!aircraftIsF16C)
            {
                error = "the authoritative aircraft is not the F-16C, and this replacement stack is "
                        + "F-16 specific";
                return false;
            }

            if (!sixDoFBodyPresent)
            {
                error = "no MavSixDoFBody to own load application";
                return false;
            }

            if (!aeroModelReady)
            {
                error = "the aerodynamic model is not ready";
                return false;
            }

            if (!controlLawReady)
            {
                error = "the control law is not ready or not bound to this body";
                return false;
            }

            if (!actuatorReady)
            {
                error = "the actuator model is not ready, so surface positions have no owner";
                return false;
            }

            if (!propulsionAcceptable)
            {
                error = "propulsion is not acceptable for live flight and non-authoritative thrust "
                        + "has not been explicitly accepted";
                return false;
            }

            if (!gravityOwnedExactlyOnce)
            {
                error = "gravity is not owned exactly once; it would be applied twice or not at all";
                return false;
            }

            if (!shadowTelemetryFinite)
            {
                error = "the shadow path has not produced finite telemetry, so the pipeline is wired "
                        + "but not known to run";
                return false;
            }

            if (!shadowRunLongEnough)
            {
                error = "the shadow path has not run long enough to trust its telemetry";
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>Human-readable breakdown, for the transition diagnostic.</summary>
        public string Describe()
        {
            StringBuilder text = new StringBuilder(512);
            Line(text, "IdentityAuthoritative", aircraftIdentityAuthoritative);
            Line(text, "AircraftIsF16C", aircraftIsF16C);
            Line(text, "SixDoFBodyPresent", sixDoFBodyPresent);
            Line(text, "AeroModelReady", aeroModelReady);
            Line(text, "ControlLawReady", controlLawReady);
            Line(text, "ActuatorReady", actuatorReady);
            Line(text, "PropulsionAcceptable", propulsionAcceptable);
            Line(text, "GravityOwnedExactlyOnce", gravityOwnedExactlyOnce);
            Line(text, "ShadowTelemetryFinite", shadowTelemetryFinite);
            Line(text, "ShadowRunLongEnough", shadowRunLongEnough);

            string error;
            text.Append("OwnershipValid=").AppendLine(IsReady(out error) ? "true" : "false");
            if (!string.IsNullOrEmpty(error))
                text.Append("Blocker=").AppendLine(error);

            return text.ToString();
        }

        private static void Line(StringBuilder text, string name, bool value)
        {
            text.Append("  ").Append(name).Append('=').AppendLine(value ? "true" : "false");
        }

        /// <summary>
        /// Everything false. The honest starting point: readiness is something that gets established,
        /// not something that is assumed until contradicted.
        /// </summary>
        public static MavReplacementReadiness NotReady()
        {
            return new MavReplacementReadiness();
        }
    }
}
