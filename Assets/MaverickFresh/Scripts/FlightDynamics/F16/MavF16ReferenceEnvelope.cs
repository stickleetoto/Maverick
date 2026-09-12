using UnityEngine;

namespace MaverickFresh.FlightDynamics.F16
{
    /// <summary>
    /// Where a Mach number came from. The gate refuses anything but the reference atmosphere.
    ///
    /// This is an enum rather than a comment because "do not gate on the legacy Mach estimate" is only
    /// a rule if something enforces it. A caller that passes a legacy figure has to say so, and saying
    /// so is a refusal.
    /// </summary>
    public enum MavMachSource
    {
        /// <summary>Nobody said. Refused.</summary>
        Unspecified = 0,

        /// <summary>MavAtmosphereModel.ReferenceMach - the ISA speed of sound at the actual altitude.</summary>
        ReferenceAtmosphere = 1,

        /// <summary>The legacy TAS / 343 estimate. Valid for the HUD, refused for reference gating.</summary>
        LegacyApproximation = 2
    }

    /// <summary>Why the reference envelope refused, or that it did not.</summary>
    public enum MavReferenceEnvelopeStatus
    {
        /// <summary>Not evaluated this step.</summary>
        Unevaluated = 0,

        /// <summary>Every precondition holds. Phase 5C-R would be permitted to run here.</summary>
        WithinReference = 1,

        /// <summary>Mach at or above the Morelli validated ceiling. The aerodynamic data simply stops.</summary>
        OutOfReferenceRange = 2,

        /// <summary>The Mach number did not come from the reference atmosphere, so it cannot gate.</summary>
        MachSourceNotReference = 7,

        /// <summary>Alpha or beta outside the Morelli validated domain.</summary>
        OutOfFlowAngleDomain = 3,

        /// <summary>The aircraft is not in the clean reference configuration the data describes.</summary>
        ConfigurationMismatch = 4,

        /// <summary>Identity, mass, inertia, CG mapping or gravity ownership is not established.</summary>
        ReferenceDataIncomplete = 5,

        /// <summary>A legacy physical writer is still live, or replacement state is not finite.</summary>
        OwnershipNotClean = 6
    }

    /// <summary>
    /// The Phase 5C-R reference-envelope gate: may the unpowered replacement stack own physics RIGHT NOW?
    ///
    /// WHY THIS IS A SEPARATE CONCEPT FROM MavReplacementReadiness. Readiness asks whether the
    /// replacement stack is BUILT - components present, identity applied, telemetry finite. This asks
    /// whether the aircraft is currently INSIDE the flight condition the reference data was measured
    /// in. Those are different questions with different lifetimes: readiness is established once at
    /// setup, while the envelope can be left and re-entered several times a minute by flying faster.
    ///
    /// Phase 5C-R is deliberately UNPOWERED, so zero authoritative thrust is not a precondition
    /// failure here - it is the intended state, and <see cref="propulsionIntentionallyDisabled"/> must
    /// be TRUE for the gate to pass. Thrust becomes a blocker at Phase 5D, not before.
    ///
    /// FAIL CLOSED. Every condition must hold. Anything else returns a named status and the caller
    /// must stay on, or return to, Legacy. There is no branch that extrapolates the aerodynamic model
    /// past its published domain, and adding one would defeat the purpose of having measured a domain.
    ///
    /// SOURCES for the numeric bounds, all from Morelli NTRS 20040110310:
    ///   Table 1  alpha  -10 deg .. +45 deg
    ///   Table 1  beta   -30 deg .. +30 deg
    ///   Section 3  "relatively low Mach numbers (&lt; 0.6)"
    /// </summary>
    [System.Serializable]
    public struct MavF16ReferenceEnvelope
    {
        [Header("Identity and configuration")]
        public bool aircraftIdentityResolvedAsF16C;
        public bool cleanReferenceConfiguration;
        public bool gearUp;
        public bool noExternalStores;

        [Header("Phase 5C-R is unpowered by design")]
        public bool propulsionIntentionallyDisabled;

        [Header("Flight condition")]
        [Tooltip("Mach used for gating. MUST come from MavAtmosphereModel.ReferenceMach.")]
        public float mach;

        [Tooltip("Where 'mach' came from. Anything but ReferenceAtmosphere is refused, because the "
                 + "Morelli validity bound is meaningless when evaluated with a different atmosphere "
                 + "than the model uses.")]
        public MavMachSource machSource;

        public float alphaDeg;
        public float betaDeg;

        [Header("Reference data")]
        public bool gravityOwnerValid;
        public bool massAndInertiaValid;
        public bool cgMappingValidated;

        [Header("Ownership hygiene")]
        public bool allReplacementStateFinite;
        public bool legacyPhysicalWritersDisabledAtomically;

        /// <summary>
        /// Evaluate the gate. Returns true only when every condition holds.
        ///
        /// Order matters for the REASON, not for the verdict: the most fundamental failure is named
        /// first, so a report says "identity is not resolved" rather than "Mach is too high" when both
        /// are true and only one of them is the thing to fix.
        /// </summary>
        public bool IsWithinReferenceEnvelope(
            out MavReferenceEnvelopeStatus status,
            out string reason)
        {
            if (!aircraftIdentityResolvedAsF16C)
            {
                status = MavReferenceEnvelopeStatus.ReferenceDataIncomplete;
                reason = "aircraft identity is not authoritatively resolved as F16C, so there is no "
                         + "way to know the reference data belongs to the aircraft being flown";
                return false;
            }

            if (!massAndInertiaValid)
            {
                status = MavReferenceEnvelopeStatus.ReferenceDataIncomplete;
                reason = "mass / inertia have not been validated against the sourced table";
                return false;
            }

            if (!cgMappingValidated)
            {
                status = MavReferenceEnvelopeStatus.ReferenceDataIncomplete;
                reason = "the Unity model origin to aerodynamic datum (0.25 cbar) mapping has not been "
                         + "measured and declared, so the moment arms are unknown";
                return false;
            }

            if (!gravityOwnerValid)
            {
                status = MavReferenceEnvelopeStatus.ReferenceDataIncomplete;
                reason = "gravity ownership does not resolve to exactly one provider";
                return false;
            }

            if (!cleanReferenceConfiguration || !gearUp || !noExternalStores)
            {
                status = MavReferenceEnvelopeStatus.ConfigurationMismatch;
                reason = "the Morelli data is for a clean aircraft, gear retracted, no external stores"
                         + " (clean=" + cleanReferenceConfiguration + ", gearUp=" + gearUp
                         + ", noStores=" + noExternalStores + ")";
                return false;
            }

            if (!propulsionIntentionallyDisabled)
            {
                status = MavReferenceEnvelopeStatus.ConfigurationMismatch;
                reason = "Phase 5C-R is an UNPOWERED cutover: propulsion must be explicitly disabled, "
                         + "not merely producing zero thrust by accident";
                return false;
            }

            if (!legacyPhysicalWritersDisabledAtomically)
            {
                status = MavReferenceEnvelopeStatus.OwnershipNotClean;
                reason = "one or more legacy physical writers is still live; ownership must transfer "
                         + "atomically, not gradually";
                return false;
            }

            if (!allReplacementStateFinite)
            {
                status = MavReferenceEnvelopeStatus.OwnershipNotClean;
                reason = "replacement state contains a non-finite value";
                return false;
            }

            // Flight condition last, because it is the one that changes second to second.
            if (machSource != MavMachSource.ReferenceAtmosphere)
            {
                status = MavReferenceEnvelopeStatus.MachSourceNotReference;
                reason = "Mach was supplied as " + machSource + " rather than from the reference "
                         + "atmosphere; the Morelli 0.6 bound cannot be evaluated with a different "
                         + "speed of sound than the model's own";
                return false;
            }

            if (!(mach < MavF16MorelliReference.MaxReferenceMach))
            {
                status = MavReferenceEnvelopeStatus.OutOfReferenceRange;
                reason = "Mach " + mach.ToString("F3") + " is at or above the Morelli reference ceiling "
                         + MavF16MorelliReference.MaxReferenceMach.ToString("F2")
                         + "; the wind-tunnel data this model was fitted to stops there";
                return false;
            }

            if (alphaDeg < MavF16MorelliReference.AlphaMinDeg
                || alphaDeg > MavF16MorelliReference.AlphaMaxDeg)
            {
                status = MavReferenceEnvelopeStatus.OutOfFlowAngleDomain;
                reason = "alpha " + alphaDeg.ToString("F1") + " deg is outside the validated domain "
                         + MavF16MorelliReference.AlphaMinDeg.ToString("F0") + " .. "
                         + MavF16MorelliReference.AlphaMaxDeg.ToString("F0") + " deg";
                return false;
            }

            if (betaDeg < MavF16MorelliReference.BetaMinDeg
                || betaDeg > MavF16MorelliReference.BetaMaxDeg)
            {
                status = MavReferenceEnvelopeStatus.OutOfFlowAngleDomain;
                reason = "beta " + betaDeg.ToString("F1") + " deg is outside the validated domain "
                         + MavF16MorelliReference.BetaMinDeg.ToString("F0") + " .. "
                         + MavF16MorelliReference.BetaMaxDeg.ToString("F0") + " deg";
                return false;
            }

            status = MavReferenceEnvelopeStatus.WithinReference;
            reason = "within the NASA reference F-16 envelope: Mach " + mach.ToString("F3")
                     + ", alpha " + alphaDeg.ToString("F1") + " deg, beta "
                     + betaDeg.ToString("F1") + " deg, clean and unpowered";
            return true;
        }

        /// <summary>
        /// Nothing satisfied. The starting point for anything that has to prove itself, so a caller
        /// that forgets to fill a field gets a refusal rather than a pass.
        /// </summary>
        public static MavF16ReferenceEnvelope NothingEstablished()
        {
            return new MavF16ReferenceEnvelope();
        }

        /// <summary>
        /// A fully satisfied gate at a representative reference condition. Exists so validation can
        /// knock out one field at a time and confirm each one is load bearing.
        /// </summary>
        public static MavF16ReferenceEnvelope FullySatisfied()
        {
            MavF16ReferenceEnvelope e = new MavF16ReferenceEnvelope();
            e.aircraftIdentityResolvedAsF16C = true;
            e.cleanReferenceConfiguration = true;
            e.gearUp = true;
            e.noExternalStores = true;
            e.propulsionIntentionallyDisabled = true;
            e.mach = 0.45f;
            e.machSource = MavMachSource.ReferenceAtmosphere;
            e.alphaDeg = 8f;
            e.betaDeg = 0f;
            e.gravityOwnerValid = true;
            e.massAndInertiaValid = true;
            e.cgMappingValidated = true;
            e.allReplacementStateFinite = true;
            e.legacyPhysicalWritersDisabledAtomically = true;
            return e;
        }

        /// <summary>
        /// Fill the flight condition from a real state, computing Mach from the reference atmosphere.
        ///
        /// The only supported way to populate these fields, so a legacy Mach estimate cannot reach the
        /// gate by accident - it would have to be assigned by hand along with a machSource that the
        /// gate then refuses.
        /// </summary>
        public void SetReferenceFlightCondition(
            float trueAirspeedMps, float geometricAltitudeM, float alphaDegrees, float betaDegrees)
        {
            mach = MavAtmosphereModel.ReferenceMach(trueAirspeedMps, geometricAltitudeM);
            machSource = MavMachSource.ReferenceAtmosphere;
            alphaDeg = alphaDegrees;
            betaDeg = betaDegrees;
        }

        public string Describe()
        {
            MavReferenceEnvelopeStatus status;
            string reason;
            bool ok = IsWithinReferenceEnvelope(out status, out reason);
            return (ok ? "WITHIN" : "REFUSED") + " [" + status + "] " + reason;
        }
    }
}
