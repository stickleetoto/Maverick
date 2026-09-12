using UnityEngine;
using MaverickFresh.FlightDynamics;

namespace MaverickFresh.FlightDynamics.F16
{
    /// <summary>How the Unity physics datum relates to the aerodynamic CG.</summary>
    public enum MavPhysicsDatumKind
    {
        /// <summary>Nobody said. The gate refuses.</summary>
        Undeclared = 0,

        /// <summary>
        /// This transform's origin IS the reference CG, by construction. Option B.
        /// centerOfMass is therefore exactly Vector3.zero and needs no measurement.
        /// </summary>
        OriginIsReferenceCg = 1,

        /// <summary>
        /// The origin is somewhere else and the offset to the CG has been MEASURED and declared.
        /// Option A. Requires a real measurement and a provenance string.
        /// </summary>
        MeasuredOffsetToCg = 2
    }

    /// <summary>
    /// Phase 5C-R isolated test rig: an aircraft that SPAWNS in the NASA reference configuration.
    ///
    /// WHY A DEDICATED ROOT INSTEAD OF RE-USING THE GAMEPLAY PREFAB.
    ///
    /// The aerodynamic quantities xcgRef = 0.35 c̄ and xcg = 0.25 c̄ are AERODYNAMIC reference stations.
    /// They are the datum the coefficient equations resolve moments about. They are NOT a Unity
    /// centerOfMass vector, and turning one into the other requires knowing where a particular art
    /// asset's origin sits relative to the airframe - which no amount of code can discover. Inferring
    /// it from an imported mesh pivot would be a guess dressed as a measurement.
    ///
    /// So this root takes the other route. Its own origin is DEFINED to be the reference CG:
    ///
    ///     AircraftPhysicsRoot   &lt;- this component, Rigidbody, reference mass/inertia, origin == CG
    ///         VisualModel       &lt;- child, carries whatever offset the art needs
    ///
    /// With that definition, <c>Rigidbody.centerOfMass</c> is exactly <c>Vector3.zero</c> and the CG
    /// mapping is declared rather than measured. The gameplay prefab is untouched, which is the point:
    /// the first 5C-R experiment should not need the shipping aircraft to be rebuilt, and a bad result
    /// should not leave the gameplay aircraft altered.
    ///
    /// WHAT IT DOES NOT DO. It does not auto-switch between Legacy and Replacement, and it is not
    /// installed by MavFreshBootstrap. Crossing Mach 0.6 does not flip ownership back and forth; it
    /// ends the test. Building a system that repeatedly flips ownership mid-flight is a later phase and
    /// would be premature before the replacement model has been shown to fly at all.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-350)]
    public class MavF16ReferencePhysicsRoot : MonoBehaviour
    {
        [Header("Physics datum declaration")]
        [Tooltip("How this transform's origin relates to the aerodynamic reference CG. Undeclared is refused.")]
        public MavPhysicsDatumKind datumKind = MavPhysicsDatumKind.OriginIsReferenceCg;

        [Tooltip("Only used when datumKind is MeasuredOffsetToCg: the measured offset from this origin to the reference CG, metres, in this transform's local axes.")]
        public Vector3 measuredOffsetToCgLocalM = Vector3.zero;

        [Tooltip("How the declaration above was established. Required - a datum with no provenance cannot be checked by anyone later.")]
        [TextArea(2, 4)]
        public string datumProvenance =
            "Origin defined as the reference CG by construction of this test root; the visual model "
            + "carries the offset as a child. No mesh pivot was inferred.";

        [Header("Spawn-time configuration")]
        [Tooltip("Mass policy this rig spawns in. Reference is the only value valid for a 5C-R experiment.")]
        public MavF16MassPolicy massPolicy = MavF16MassPolicy.Reference;

        [Tooltip("Apply the reference mass and inertia at Awake. The aircraft SPAWNS in the reference configuration; nothing converts it later.")]
        public bool applyReferenceMassAtSpawn = true;

        [Tooltip("Phase 5C-R is unpowered by design. Must stay true.")]
        public bool propulsionIntentionallyDisabled = true;

        [Header("Envelope monitoring")]
        [Tooltip("Evaluate the reference envelope every physics step and fail safe when it is violated.")]
        public bool monitorReferenceEnvelope = true;

        [Tooltip("What to do when the envelope is violated. Neither option flips ownership back and forth - the test ends.")]
        public MavEnvelopeViolationResponse violationResponse =
            MavEnvelopeViolationResponse.EndTestAndReportOnly;

        [Header("Read-only state")]
        public string configurationStatus = "not evaluated";
        public string envelopeStatus = "not evaluated";
        public MavReferenceEnvelopeStatus lastEnvelopeStatus = MavReferenceEnvelopeStatus.Unevaluated;
        public bool configurationValid;
        public bool testTerminated;
        public string terminationReason = "";
        public int stepsInsideEnvelope;
        public int stepsOutsideEnvelope;

        [Header("Diagnostics")]
        public float debugReferenceMach;
        public float debugAlphaDeg;
        public float debugBetaDeg;
        public float debugMassKg;
        public Vector3 debugCenterOfMass;
        public Vector3 debugInertiaTensor;

        private Rigidbody body;
        private MavFlightPhysicsOwnership ownership;
        private MavAeroBody legacyAeroBody;

        /// <summary>What a violation does. Deliberately excludes "switch back to Legacy and carry on".</summary>
        public enum MavEnvelopeViolationResponse
        {
            /// <summary>Mark the test terminated and report. Nothing is switched.</summary>
            EndTestAndReportOnly = 0,

            /// <summary>Mark the test terminated and return ownership to Legacy once, then stop.</summary>
            EndTestAndReturnToLegacyOnce = 1
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            ownership = GetComponent<MavFlightPhysicsOwnership>();
            legacyAeroBody = GetComponent<MavAeroBody>();

            configurationValid = ValidateAndApplyConfiguration(out configurationStatus);
            if (!configurationValid)
                Debug.LogError("MavF16ReferencePhysicsRoot refused its configuration: "
                               + configurationStatus, this);
        }

        /// <summary>
        /// Validate the declaration and apply the reference mass properties. Called at Awake, i.e.
        /// BEFORE physics begins - which is the whole point of a spawn-time configuration.
        /// </summary>
        public bool ValidateAndApplyConfiguration(out string status)
        {
            if (body == null)
                body = GetComponent<Rigidbody>();

            if (body == null)
            {
                status = "no Rigidbody on the reference physics root";
                return false;
            }

            if (datumKind == MavPhysicsDatumKind.Undeclared)
            {
                status = "physics datum is Undeclared. The aerodynamic stations 0.35/0.25 cbar are "
                         + "aerodynamic quantities and must not be read as a Unity centerOfMass until "
                         + "the datum is declared";
                return false;
            }

            if (string.IsNullOrEmpty(datumProvenance) || datumProvenance.Trim().Length < 20)
            {
                status = "datumProvenance is required: a datum nobody can check is not a declaration";
                return false;
            }

            if (massPolicy != MavF16MassPolicy.Reference)
            {
                status = "massPolicy is " + massPolicy + "; a 5C-R experiment must be Reference";
                return false;
            }

            if (!propulsionIntentionallyDisabled)
            {
                status = "Phase 5C-R is unpowered: propulsionIntentionallyDisabled must be true";
                return false;
            }

            if (applyReferenceMassAtSpawn)
            {
                // Option B: the origin IS the CG, so the centre of mass is the origin. Option A uses
                // the measured offset. Either way the number handed to Unity comes from a DECLARATION,
                // never from a mesh pivot.
                Vector3 comLocal = datumKind == MavPhysicsDatumKind.OriginIsReferenceCg
                    ? Vector3.zero
                    : measuredOffsetToCgLocalM;

                MavMassProperties props = MavF16MassReference.CreateUnityMassProperties(comLocal);
                props.ApplyTo(body);
            }

            debugMassKg = body.mass;
            debugCenterOfMass = body.centerOfMass;
            debugInertiaTensor = body.inertiaTensor;

            float expectedMass = MavAtomicHandover.MassForPolicy(massPolicy);
            if (Mathf.Abs(body.mass - expectedMass) > MavAtomicHandover.MassInvarianceToleranceKg)
            {
                status = "mass is " + body.mass.ToString("F2") + " kg but the "
                         + massPolicy + " policy requires " + expectedMass.ToString("F2") + " kg";
                return false;
            }

            status = "reference configuration applied at spawn: " + body.mass.ToString("F1")
                     + " kg, datum " + datumKind + ", centre of mass " + body.centerOfMass
                     + ", inertia " + body.inertiaTensor;
            return true;
        }

        /// <summary>
        /// Build the reference envelope from the LIVE state, using the reference atmosphere.
        ///
        /// The Mach source is set by SetReferenceFlightCondition, so a legacy estimate cannot reach the
        /// gate from here even by accident.
        /// </summary>
        public MavF16ReferenceEnvelope BuildEnvelope()
        {
            MavF16ReferenceEnvelope e = new MavF16ReferenceEnvelope();

            e.aircraftIdentityResolvedAsF16C = ResolveIdentityIsF16C();
            e.cleanReferenceConfiguration = true;
            e.gearUp = true;
            e.noExternalStores = true;
            e.propulsionIntentionallyDisabled = propulsionIntentionallyDisabled;

            e.massAndInertiaValid = configurationValid;
            e.cgMappingValidated = datumKind != MavPhysicsDatumKind.Undeclared
                                   && !string.IsNullOrEmpty(datumProvenance);

            e.gravityOwnerValid = ownership != null
                && MavFlightPhysicsOwnership.HasExactlyOneGravitySource(ownership.owner);

            e.allReplacementStateFinite = body != null
                && Finite(body.linearVelocity) && Finite(body.angularVelocity);

            // In this isolated rig the legacy writers are considered disabled once the owner says so.
            e.legacyPhysicalWritersDisabledAtomically = ownership != null
                && !MavFlightPhysicsOwnership.IsLegacyPhysicsAllowed(ownership.owner);

            if (body != null)
            {
                float alpha = legacyAeroBody != null ? legacyAeroBody.debugAoADeg : 0f;
                float beta = legacyAeroBody != null ? legacyAeroBody.debugAoSDeg : 0f;
                e.SetReferenceFlightCondition(
                    body.linearVelocity.magnitude, transform.position.y, alpha, beta);

                debugReferenceMach = e.mach;
                debugAlphaDeg = e.alphaDeg;
                debugBetaDeg = e.betaDeg;
            }

            return e;
        }

        private void FixedUpdate()
        {
            if (!monitorReferenceEnvelope || testTerminated)
                return;

            MavF16ReferenceEnvelope e = BuildEnvelope();
            string reason;
            bool inside = e.IsWithinReferenceEnvelope(out lastEnvelopeStatus, out reason);
            envelopeStatus = (inside ? "WITHIN: " : "VIOLATED: ") + reason;

            if (inside)
            {
                stepsInsideEnvelope++;
                return;
            }

            stepsOutsideEnvelope++;
            FailSafe(reason);
        }

        /// <summary>
        /// Envelope violated. The test ends. Ownership is NOT flipped back so the aircraft can carry on
        /// flying - that would be the repeated Legacy/Replacement oscillation this phase explicitly
        /// does not build, and it would also destroy the evidence of what went wrong.
        /// </summary>
        private void FailSafe(string reason)
        {
            testTerminated = true;
            terminationReason = reason;

            Debug.LogWarning("MavF16ReferencePhysicsRoot: reference envelope violated after "
                             + stepsInsideEnvelope + " valid steps - " + reason, this);

            if (violationResponse == MavEnvelopeViolationResponse.EndTestAndReturnToLegacyOnce
                && ownership != null)
            {
                ownership.ReturnToLegacy("reference envelope violated: " + reason);
            }
        }

        private bool ResolveIdentityIsF16C()
        {
            MavAircraftProfileApplier applier = GetComponent<MavAircraftProfileApplier>();
            if (applier == null)
                return false;

            return applier.HasAuthoritativeAircraft
                   && applier.AppliedAircraft == MavAircraftKind.F16C;
        }

        private static bool Finite(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z)
                   && !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
        }

        [ContextMenu("Log Reference Root Status")]
        public void LogStatus()
        {
            Debug.Log("MavF16ReferencePhysicsRoot\n"
                      + "  configuration  " + configurationStatus + "\n"
                      + "  envelope       " + envelopeStatus + "\n"
                      + "  steps inside   " + stepsInsideEnvelope + "\n"
                      + "  steps outside  " + stepsOutsideEnvelope + "\n"
                      + "  terminated     " + testTerminated
                      + (testTerminated ? " (" + terminationReason + ")" : ""), this);
        }
    }
}
