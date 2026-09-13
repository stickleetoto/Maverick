using System;
using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>
    /// Where a piece of engine data came from.
    ///
    /// These are exactly the six labels defined by Docs/Reference/README.md, promoted from
    /// documentation prose into a type so a field cannot carry an unlabelled number. The repository
    /// rule is that missing public data is recorded rather than filled in, so
    /// <see cref="Unavailable"/> is the default and the zero value: a profile that forgets to say
    /// where a number came from reads as having no source, never as authoritative.
    /// </summary>
    public enum MavEngineDataProvenance
    {
        /// <summary>No accepted public source yet. The value must not be used as engine data.</summary>
        Unavailable = 0,

        /// <summary>Project tuning. Not claimed as aircraft data.</summary>
        MaverickTuning = 1,

        /// <summary>Deliberately approximate extension, with its provenance recorded.</summary>
        Approximate = 2,

        /// <summary>External implementation or mismatched configuration: an oracle, not a value source.</summary>
        CrossValidationOnly = 3,

        /// <summary>Strong public source, but configuration matching still has to be checked.</summary>
        PublicReference = 4,

        /// <summary>Primary source, accepted for the configuration being modelled.</summary>
        Authoritative = 5
    }

    /// <summary>
    /// Which power/spool dynamics law an engine profile selects.
    ///
    /// An enum rather than a type name string: the shared runtime resolves this through a switch in
    /// <see cref="MavEnginePowerDynamicsFactory"/>, so there is no reflection, no magic string, and a
    /// profile cannot name a law that does not exist.
    ///
    /// Each entry names the SOURCE the law came from, not the aircraft that happens to use it. A
    /// second aircraft may select <see cref="F16GarzaMorelliPowerState"/> only if its engine really
    /// does follow that model, which for a different engine variant has to be proven, not assumed.
    /// </summary>
    public enum MavEnginePowerDynamicsLaw
    {
        /// <summary>
        /// No sourced dynamics. Actual power tracks commanded power immediately.
        ///
        /// This is honest plumbing, not a modelled engine: a real engine spools, and claiming
        /// otherwise would be an invented time constant. Used by engine profiles whose transient
        /// data is still <see cref="MavEngineDataProvenance.Unavailable"/>.
        /// </summary>
        InstantNoSourcedTransient = 0,

        /// <summary>
        /// NASA/TM-2003-212145 (Garza &amp; Morelli) F-16 power-state dynamics: throttle gearing,
        /// the piecewise actual-power derivative, and the reciprocal time-constant schedule.
        ///
        /// F-16 reference-engine behaviour. NOT established for any other engine.
        /// </summary>
        F16GarzaMorelliPowerState = 1
    }

    /// <summary>
    /// How an engine profile treats augmentation, per ENG-011.
    ///
    /// Deliberately profile-defined rather than core behaviour: the F-16 reference model blends
    /// military to maximum thrust inside its sourced power-state variable, and that is a property of
    /// that model, not a universal truth about jet engines.
    /// </summary>
    public enum MavEngineAugmentationSemantics
    {
        /// <summary>Not established for this engine.</summary>
        Unavailable = 0,

        /// <summary>Engine is modelled without augmentation.</summary>
        NotModelled = 1,

        /// <summary>
        /// Augmentation is already inside the continuous power state; there is no separate
        /// afterburner stage to switch. The F-16 reference model's arrangement.
        /// </summary>
        ContinuousWithinPowerState = 2,

        /// <summary>A discrete augmentor stage, switched separately from the core power state.</summary>
        DiscreteAugmentorStage = 3
    }

    /// <summary>
    /// Fuel-flow capability, per ENG-007. Fuel MASS and tank distribution belong to an aircraft fuel
    /// system, never to a bare engine profile, so this only describes whether the engine can report a
    /// flow rate at all.
    /// </summary>
    public enum MavEngineFuelFlowCapability
    {
        /// <summary>No sourced fuel-flow data. The engine reports no flow.</summary>
        Unavailable = 0,

        /// <summary>Fuel flow is available from the engine's own sourced data.</summary>
        SourcedFromEngineData = 1
    }

    /// <summary>
    /// Rotor angular momentum for the gyroscopic coupling term of ENG-008.
    ///
    /// Carries its own availability flag because zero is a meaningful physical value and
    /// "we do not know" is not zero. The load path must apply nothing unless
    /// <see cref="available"/> is true.
    /// </summary>
    [Serializable]
    public struct MavEngineRotorAngularMomentum
    {
        [Tooltip("False means no sourced value. Zero magnitude with available=true means a deliberately non-rotating model.")]
        public bool available;

        [Tooltip("Rotor angular momentum magnitude, kg*m^2/s, about the engine spool axis.")]
        public float magnitudeKgM2PerSec;

        [Tooltip("Spin axis in aerodynamic body axes (X forward, Y right, Z down). Sign carries the spool direction.")]
        public Vector3 spinAxisAeroBody;

        public MavEngineDataProvenance provenance;

        public static MavEngineRotorAngularMomentum NotAvailable
        {
            get { return new MavEngineRotorAngularMomentum(); }
        }
    }

    /// <summary>
    /// The altitude / Mach / power box an engine's data is actually valid inside.
    ///
    /// Separate from the thrust deck's own envelope on purpose: a deck knows the extent of its table,
    /// while this records the extent of the ENGINE MODEL as published. They are usually the same and
    /// occasionally are not, and collapsing them would lose the difference.
    /// </summary>
    [Serializable]
    public struct MavEngineSourceEnvelope
    {
        [Tooltip("False until someone states the bounds. An undeclared envelope cannot be checked, so the profile is not live-flight acceptable.")]
        public bool declared;

        public float minAltitudeM;
        public float maxAltitudeM;
        public float minMach;
        public float maxMach;

        public static MavEngineSourceEnvelope Undeclared
        {
            get { return new MavEngineSourceEnvelope(); }
        }

        public bool Contains(float altitudeM, float mach)
        {
            if (!declared)
                return false;

            return altitudeM >= minAltitudeM && altitudeM <= maxAltitudeM
                && mach >= minMach && mach <= maxMach;
        }
    }

    /// <summary>
    /// STATIC definition of one engine: what it is, where its numbers came from, which laws it
    /// follows, and which thrust data it reads. No mutable state of any kind lives here.
    ///
    /// That exclusion is the whole point. Two engines on a twin-engine aircraft normally share ONE
    /// profile object, because they are the same engine variant, while each has its own
    /// <see cref="MavEngineRuntime"/>. If spool state lived in the profile the two engines would
    /// share it, and a twin could not run 90% on one side and 40% on the other - which is the
    /// requirement this split exists to satisfy.
    ///
    /// Installation geometry is also absent: where an engine is bolted and which way it points are
    /// properties of the aircraft, not the bare engine. Those live in
    /// <see cref="MavEngineInstallation"/>.
    ///
    /// A ScriptableObject, decided in P0.1 to close ENG-013.
    ///
    /// A plain [Serializable] class would have been simpler, and for RUNTIME behaviour it was
    /// sufficient - state never lives here, so two copies behave identically to one shared object.
    /// The problem is authoring. Unity serializes plain [Serializable] class fields BY VALUE, so a
    /// twin authored in the inspector with both slots pointing at one profile would deserialize as
    /// two independent copies. Nothing would break immediately; instead the two copies would drift as
    /// someone edited one of them, and the aircraft would quietly be flying two different engine
    /// definitions while the code still claimed they were the same engine.
    ///
    /// A ScriptableObject field is a genuine object reference, so one asset means one definition, one
    /// provenance record, and no drift. This was converted BEFORE any scene or prefab authoring began,
    /// which is the only cheap moment to do it.
    ///
    /// Mutable engine state still never lives here - that is what MavEngineRuntime is for, and the
    /// ScriptableObject makes the rule more important rather than less: an asset is shared across
    /// every aircraft that references it, so a state field here would be shared globally.
    /// </summary>
    public sealed class MavEngineProfile : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable identifier for this exact engine definition. Appears in per-engine telemetry.")]
        public string engineProfileId = "unconfigured";

        public string displayName = "Unconfigured Engine Profile";

        [Tooltip("EXACT engine variant and configuration. 'F100' is not one numeric engine (ENG-006), so this must be specific enough to identify a data set.")]
        public string engineVariantIdentity = "unspecified";

        [TextArea(2, 6)]
        [Tooltip("Where this engine's data comes from: report numbers, tables, configuration tags.")]
        public string sourceIdentity = "no source recorded";

        [Header("Provenance")]
        [Tooltip("Provenance of the engine definition as a whole. The thrust deck declares its own separately and is not upgraded by this.")]
        public MavEngineDataProvenance provenance = MavEngineDataProvenance.Unavailable;

        [Header("Model Selection")]
        public MavEnginePowerDynamicsLaw powerDynamicsLaw = MavEnginePowerDynamicsLaw.InstantNoSourcedTransient;

        [Tooltip("Provenance of the power/spool dynamics specifically. Independent of thrust data: Maverick has sourced F-16 power dynamics WITHOUT a frozen F-16 thrust deck, and that distinction must survive in the data model.")]
        public MavEngineDataProvenance powerDynamicsProvenance = MavEngineDataProvenance.Unavailable;

        public MavEngineAugmentationSemantics augmentation = MavEngineAugmentationSemantics.Unavailable;

        [Header("Dimensional Thrust Data")]
        [Tooltip("Altitude/Mach/power thrust lookup. NULL means dimensional thrust is exactly zero, which is a valid and current state. Attaching a deck does not make it authoritative - the deck declares that itself.")]
        public MavThrustDeckBase thrustDeck;

        [Header("Declared Model Envelope")]
        public MavEngineSourceEnvelope sourceEnvelope = MavEngineSourceEnvelope.Undeclared;

        [Header("Optional Sourced Channels")]
        public MavEngineFuelFlowCapability fuelFlow = MavEngineFuelFlowCapability.Unavailable;

        public MavEngineRotorAngularMomentum rotorAngularMomentum = MavEngineRotorAngularMomentum.NotAvailable;

        /// <summary>
        /// Creates an in-memory engine profile.
        ///
        /// The only sanctioned way to build one in code: `new MavEngineProfile()` does not work on a
        /// ScriptableObject and Unity warns about it, so funnelling construction through here means
        /// the compiler catches every old call site rather than leaving a silently broken one.
        ///
        /// In-memory, NOT an asset. Callers that create one are responsible for destroying it; an
        /// authored aircraft references an asset instead and destroys nothing.
        /// </summary>
        public static MavEngineProfile CreateInMemory(string engineProfileId)
        {
            MavEngineProfile profile = CreateInstance<MavEngineProfile>();
            profile.engineProfileId = engineProfileId;
            return profile;
        }

        /// <summary>
        /// Whether the DIMENSIONAL THRUST behind this profile is authoritative.
        ///
        /// Reads the deck, not this profile's own <see cref="provenance"/>. A profile's paperwork
        /// being in order says nothing about whether a thrust table exists, and the F-16 is exactly
        /// that case today: sourced power dynamics, no frozen deck, zero thrust.
        /// </summary>
        public bool HasAuthoritativeThrustData
        {
            get
            {
                return thrustDeck != null
                    && thrustDeck.Authority == MavThrustDataAuthority.Authoritative;
            }
        }

        /// <summary>
        /// Stricter than <see cref="HasAuthoritativeThrustData"/>: the deck must also refuse to
        /// extrapolate, and this profile must declare the envelope its own data covers. An undeclared
        /// envelope is not a wide envelope.
        /// </summary>
        public bool IsAcceptableForLiveFlight
        {
            get
            {
                return thrustDeck != null
                    && thrustDeck.IsAcceptableForLiveFlight
                    && sourceEnvelope.declared;
            }
        }

        /// <summary>Human-readable thrust provenance, for telemetry and readiness reports.</summary>
        public string ThrustDataStatus
        {
            get
            {
                if (thrustDeck == null)
                    return "no thrust deck: dimensional thrust unavailable";

                return thrustDeck.Authority + " / " + thrustDeck.DeckName;
            }
        }

        /// <summary>
        /// Whether this profile is coherent enough to install. Deliberately does NOT require
        /// sourced thrust: an engine whose deck is pending is installable and produces zero thrust,
        /// which is the current F-16 condition and has to stay expressible.
        /// </summary>
        public bool IsValid(out string reason)
        {
            if (string.IsNullOrWhiteSpace(engineProfileId) || engineProfileId == "unconfigured")
            {
                reason = "engineProfileId is unset";
                return false;
            }

            if (string.IsNullOrWhiteSpace(engineVariantIdentity)
                || engineVariantIdentity == "unspecified")
            {
                reason = "engineVariantIdentity is unspecified, so it is not knowable which engine's "
                         + "data this profile claims to hold";
                return false;
            }

            // A law that is not "no sourced transient" is a claim about a real engine, so it needs a
            // provenance to back it. This is the check that stops a sourced-looking law appearing on
            // a profile that never recorded where it came from.
            if (powerDynamicsLaw != MavEnginePowerDynamicsLaw.InstantNoSourcedTransient
                && powerDynamicsProvenance == MavEngineDataProvenance.Unavailable)
            {
                reason = "power-dynamics law " + powerDynamicsLaw + " is selected but its provenance "
                         + "is Unavailable; a sourced law must say where it came from";
                return false;
            }

            if (rotorAngularMomentum.available
                && rotorAngularMomentum.spinAxisAeroBody.sqrMagnitude <= 0f)
            {
                reason = "rotor angular momentum is marked available but has no spin axis";
                return false;
            }

            reason = "OK";
            return true;
        }
    }
}
