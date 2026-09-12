using System;
using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>
    /// ONE engine as installed in a particular aircraft: which engine it is, where it is bolted,
    /// which way it pushes, and which throttle channel drives it.
    ///
    /// Separate from <see cref="MavEngineProfile"/> because inlet geometry, mount location, thrust
    /// line and engine count are properties of the airframe, not of the bare engine. The same engine
    /// profile object is expected to appear in two installations on a twin-engine aircraft.
    ///
    /// Contains no mutable state: this is configuration. The running state of the installed engine
    /// lives in the <see cref="MavEngineRuntime"/> that the propulsion system creates for this slot.
    ///
    /// Coordinates are AERODYNAMIC BODY AXES throughout - X forward, Y right, Z down - measured from
    /// the declared physics datum / CG. That convention is stated on every field because a sign error
    /// in a thrust offset produces a yawing moment in the wrong direction, which is exactly the class
    /// of bug the r x F composition is supposed to make visible rather than hide.
    /// </summary>
    [Serializable]
    public sealed class MavEngineInstallation
    {
        [Header("Slot Identity")]
        [Tooltip("Slot index within the aircraft. Must be unique; appears in per-engine telemetry.")]
        public int slotId;

        [Tooltip("Human-readable position, e.g. 'left' / 'right' / 'single'. Telemetry only.")]
        public string slotName = "slot";

        [Header("Engine")]
        [Tooltip("The engine installed in this slot. Two slots may reference the SAME profile object when the aircraft really uses two matching engines; they still get independent runtime states.")]
        public MavEngineProfile engineProfile;

        [Header("Installation Geometry (aerodynamic body axes: X fwd, Y right, Z down)")]
        [Tooltip("Thrust application point relative to the declared physics datum / CG, metres. A non-zero Y is what makes an engine-out condition produce a real yawing moment through r x F.")]
        public Vector3 positionAeroBodyM = Vector3.zero;

        [Tooltip("Direction the thrust acts, in aerodynamic body axes. Normalised on use. (1,0,0) is straight forward.")]
        public Vector3 thrustDirectionAeroBody = new Vector3(1f, 0f, 0f);

        [Tooltip("False until someone measures this installation from configuration-matched data. Zero is a placeholder, not a measurement, and an undeclared installation cannot be accepted for live flight.")]
        public bool geometryDeclared;

        [TextArea(1, 4)]
        [Tooltip("Where the mount position and thrust line came from. Required whenever geometryDeclared is true.")]
        public string geometryProvenance = "UNAVAILABLE";

        [Header("Control")]
        [Tooltip("Throttle channel driving this engine. Several slots may share a channel; distinct channels are what allow differential throttle later without changing this architecture.")]
        public int throttleChannel;

        [Tooltip("A disabled installation contributes exactly nothing: no force, no moment, and no advancing state.")]
        public bool enabled = true;

        /// <summary>
        /// Unit thrust direction, or forward when the field is degenerate.
        ///
        /// A zero-length direction would otherwise silently produce zero thrust from a running
        /// engine, which looks like an engine failure rather than a misconfiguration.
        /// <see cref="IsValid"/> rejects it, and this fallback keeps the math finite in the meantime.
        /// </summary>
        public Vector3 UnitThrustDirection
        {
            get
            {
                float sqr = thrustDirectionAeroBody.sqrMagnitude;
                if (sqr <= 1e-12f)
                    return new Vector3(1f, 0f, 0f);

                return thrustDirectionAeroBody / Mathf.Sqrt(sqr);
            }
        }

        /// <summary>
        /// Whether this installation is coherent. Does NOT require sourced geometry: a slot with an
        /// undeclared mount is installable and reported honestly, because that is the only way to
        /// describe an aircraft whose installation data is not yet frozen.
        /// </summary>
        public bool IsValid(out string reason)
        {
            if (engineProfile == null)
            {
                reason = "slot " + slotId + " has no engine profile";
                return false;
            }

            if (thrustDirectionAeroBody.sqrMagnitude <= 1e-12f)
            {
                reason = "slot " + slotId + " has a zero-length thrust direction";
                return false;
            }

            if (!IsFinite(positionAeroBodyM) || !IsFinite(thrustDirectionAeroBody))
            {
                reason = "slot " + slotId + " has a non-finite position or thrust direction";
                return false;
            }

            if (geometryDeclared
                && (string.IsNullOrWhiteSpace(geometryProvenance)
                    || geometryProvenance == "UNAVAILABLE"))
            {
                reason = "slot " + slotId + " declares its geometry but records no provenance for it";
                return false;
            }

            string engineReason;
            if (!engineProfile.IsValid(out engineReason))
            {
                reason = "slot " + slotId + " engine profile invalid: " + engineReason;
                return false;
            }

            reason = "OK";
            return true;
        }

        internal static bool IsFinite(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsInfinity(v.x)
                && !float.IsNaN(v.y) && !float.IsInfinity(v.y)
                && !float.IsNaN(v.z) && !float.IsInfinity(v.z);
        }
    }

    /// <summary>
    /// Everything an aircraft is equipped with, propulsion-wise: an ordered set of engine slots.
    ///
    /// This is the object a flight-dynamics profile points at to say "this aircraft has these
    /// engines". One slot for the F-16, two for the F-15, N in general - the count is data, not a
    /// code path, which is the property that lets both aircraft use the same runtime.
    ///
    /// Holds no runtime state. <see cref="MavPropulsionSystem"/> builds one
    /// <see cref="MavEngineRuntime"/> per slot at initialisation.
    /// </summary>
    [Serializable]
    public sealed class MavPropulsionInstallationProfile
    {
        [Header("Identity")]
        public string installationId = "unconfigured";

        public string displayName = "Unconfigured Propulsion Installation";

        [TextArea(1, 4)]
        [Tooltip("Aircraft configuration this installation describes. Configuration identity is mandatory per Docs/Reference/README.md.")]
        public string aircraftConfiguration = "unspecified";

        [Header("Engine Slots")]
        [Tooltip("Installed engines in slot order. Empty is legal and means an unpowered aircraft, which must behave safely rather than throw.")]
        public MavEngineInstallation[] engines = new MavEngineInstallation[0];

        public int EngineCount
        {
            get { return engines != null ? engines.Length : 0; }
        }

        /// <summary>Number of slots that will actually contribute loads this step.</summary>
        public int EnabledEngineCount
        {
            get
            {
                if (engines == null)
                    return 0;

                int count = 0;
                for (int i = 0; i < engines.Length; i++)
                {
                    if (engines[i] != null && engines[i].enabled)
                        count++;
                }

                return count;
            }
        }

        /// <summary>
        /// Whether the installation is coherent. An EMPTY installation is valid on purpose: zero
        /// engines is a describable aircraft state with defined behaviour (no thrust), and making it
        /// invalid would force callers to special-case the unpowered configuration that Phase 5C
        /// deliberately flies.
        /// </summary>
        public bool IsValid(out string reason)
        {
            if (string.IsNullOrWhiteSpace(installationId) || installationId == "unconfigured")
            {
                reason = "installationId is unset";
                return false;
            }

            if (engines == null)
            {
                reason = "engine array is null";
                return false;
            }

            for (int i = 0; i < engines.Length; i++)
            {
                if (engines[i] == null)
                {
                    reason = "engine slot at index " + i + " is null";
                    return false;
                }

                string slotReason;
                if (!engines[i].IsValid(out slotReason))
                {
                    reason = slotReason;
                    return false;
                }

                // Slot ids name engines in telemetry and in bug reports. Two slots sharing an id
                // would make a per-engine reading ambiguous exactly when someone is trying to work
                // out which engine misbehaved.
                for (int j = 0; j < i; j++)
                {
                    if (engines[j] != null && engines[j].slotId == engines[i].slotId)
                    {
                        reason = "duplicate slotId " + engines[i].slotId
                                 + " at indices " + j + " and " + i;
                        return false;
                    }
                }
            }

            reason = "OK";
            return true;
        }

        private static bool IsFinite(Vector3 v)
        {
            return MavEngineInstallation.IsFinite(v);
        }

        /// <summary>
        /// Whether every installed engine is fit to fly for real: sourced thrust, a non-extrapolating
        /// deck, a declared model envelope, and measured installation geometry.
        ///
        /// False for an empty installation, because "no engines" is not "engines ready". A caller
        /// wanting to fly unpowered checks the engine count, which says what it means.
        /// </summary>
        public bool IsAcceptableForLiveFlight(out string reason)
        {
            if (EngineCount == 0)
            {
                reason = "no engines installed";
                return false;
            }

            string validReason;
            if (!IsValid(out validReason))
            {
                reason = validReason;
                return false;
            }

            for (int i = 0; i < engines.Length; i++)
            {
                MavEngineInstallation slot = engines[i];

                // Engine identity. A profile with no id or an unspecified variant cannot be shown to
                // be the engine this aircraft is supposed to have.
                string profileReason;
                if (!slot.engineProfile.IsValid(out profileReason))
                {
                    reason = "slot " + slot.slotId + " engine profile invalid: " + profileReason;
                    return false;
                }

                // Thrust data provenance, including a non-extrapolating deck and a declared model
                // envelope.
                if (!slot.engineProfile.IsAcceptableForLiveFlight)
                {
                    reason = "slot " + slot.slotId + " engine data is not acceptable for live "
                             + "flight: " + slot.engineProfile.ThrustDataStatus;
                    return false;
                }

                // Thrust direction: finite, and long enough to normalise meaningfully. A near-zero
                // direction normalises to something arbitrary, which would point the thrust somewhere
                // nobody chose.
                if (!IsFinite(slot.thrustDirectionAeroBody)
                    || slot.thrustDirectionAeroBody.sqrMagnitude < 1e-6f)
                {
                    reason = "slot " + slot.slotId + " thrust direction is non-finite or too short to "
                             + "normalise: " + slot.thrustDirectionAeroBody;
                    return false;
                }

                // Mount position: finite. Zero is legal - an engine really can sit on the datum - but
                // NaN silently poisons r x F and from there the whole moment sum.
                if (!IsFinite(slot.positionAeroBodyM))
                {
                    reason = "slot " + slot.slotId + " mount position is non-finite: "
                             + slot.positionAeroBodyM;
                    return false;
                }

                // Declared geometry, with provenance. This is the check that keeps the F-16's assumed
                // centreline mount and the F-15's unfrozen mounts out of live flight.
                if (!slot.geometryDeclared)
                {
                    reason = "slot " + slot.slotId + " installation geometry is undeclared, so "
                             + "its thrust line and r x F moment are not sourced";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(slot.geometryProvenance)
                    || slot.geometryProvenance == "UNAVAILABLE")
                {
                    reason = "slot " + slot.slotId + " declares its geometry but records no "
                             + "provenance for it";
                    return false;
                }
            }

            reason = "OK";
            return true;
        }
    }
}
