using UnityEngine;

namespace MaverickFresh.FlightDynamics.F16
{
    /// <summary>
    /// The F-16's engine profile and its single-slot installation, built in code so the sourced and
    /// unsourced parts of the configuration are visible in a diff rather than buried in a serialized
    /// asset.
    ///
    /// What is sourced here:
    ///   - the power-dynamics law: NASA/TM-2003-212145 Garza &amp; Morelli, marked PUBLIC_REFERENCE.
    ///
    /// What is NOT sourced here, and is therefore left unavailable:
    ///   - dimensional thrust. No deck is attached, so thrust is exactly zero (ENG-005). NASA TP-1538
    ///     Table VI is the public candidate and its transcription is not frozen; attaching an
    ///     unverified table would be worse than reporting no thrust.
    ///   - the thrust-line offset from the CG. Left at zero WITH geometryDeclared false, so the
    ///     architecture reports it as unmeasured rather than as a measured zero. A single engine on
    ///     the centreline is a reasonable expectation, but "reasonable expectation" is not a source.
    ///   - rotor angular momentum. The F-16 reference material documents an engine angular-momentum
    ///     term, but it is not wired into this load path yet (ENG-008).
    ///
    /// Behaviour preservation: with no deck attached this installation produces exactly zero force and
    /// zero moment while advancing the same sourced power state as before, which is bit-for-bit what
    /// MavF16EnginePowerModel did. The migration is structural.
    /// </summary>
    public static class MavF16PropulsionInstallation
    {
        public const string EngineProfileId = "f16-reference-engine-v0.1";
        public const string InstallationId = "f16-reference-installation-v0.1";

        /// <summary>
        /// The F-16 reference engine profile.
        ///
        /// A fresh object per call, deliberately. These are mutable configuration objects and a shared
        /// static instance could be edited by one aircraft and observed by another - the same class of
        /// accidental sharing the profile/runtime split exists to prevent.
        /// </summary>
        public static MavEngineProfile CreateEngineProfile(MavThrustDeckBase thrustDeck)
        {
            // Route 3 of law registration: covers offline harnesses and any caller that reaches this
            // factory before Unity's own initialisation hooks have run. Idempotent.
            MavF16EngineLawRegistrar.EnsureRegistered();

            MavEngineProfile profile = MavEngineProfile.CreateInMemory(EngineProfileId);

            profile.displayName = "F-16 reference engine (power dynamics sourced, thrust pending)";

            // Specific on purpose. The Garza/Morelli model is a reference F-16 engine model; it is not
            // a claim about a particular production F110/F100 serial configuration.
            profile.engineVariantIdentity =
                "F-16 reference-engine model as published in NASA/TM-2003-212145 "
                + "(not a specific production F100/F110 variant)";

            profile.sourceIdentity =
                "Garza & Morelli, NASA/TM-2003-212145: throttle gearing, piecewise actual-power "
                + "derivative, RTAU schedule. Dimensional thrust NOT from this source.";

            // The profile as a whole is a strong public reference whose configuration match to any
            // specific production jet still has to be argued - exactly what PublicReference means.
            profile.provenance = MavEngineDataProvenance.PublicReference;

            profile.powerDynamicsLaw = MavEnginePowerDynamicsLaw.F16GarzaMorelliPowerState;
            profile.powerDynamicsProvenance = MavEngineDataProvenance.PublicReference;

            // The reference model blends military to maximum inside the continuous power state; there
            // is no separate augmentor stage to switch (ENG-011).
            profile.augmentation = MavEngineAugmentationSemantics.ContinuousWithinPowerState;

            // Null unless a caller supplies a deck. Zero thrust, reported as unavailable.
            profile.thrustDeck = thrustDeck;

            // UNDECLARED. The power model's validity envelope is not the same statement as the
            // aerodynamic model's, and it has not been separately established here.
            profile.sourceEnvelope = MavEngineSourceEnvelope.Undeclared;

            profile.fuelFlow = MavEngineFuelFlowCapability.Unavailable;
            profile.rotorAngularMomentum = MavEngineRotorAngularMomentum.NotAvailable;

            return profile;
        }

        /// <summary>
        /// The F-16's single-engine installation.
        ///
        /// Position is zero and <c>geometryDeclared</c> is FALSE. That pairing is the honest one: the
        /// old code assumed the thrust line passed through the CG and produced zero propulsive moment
        /// (ENG-003), and this preserves that numeric behaviour exactly while recording that the
        /// assumption is an assumption. Declaring it true would assert a measurement nobody made.
        /// </summary>
        public static MavPropulsionInstallationProfile CreateInstallation(MavThrustDeckBase thrustDeck)
        {
            MavEngineInstallation slot = new MavEngineInstallation();
            slot.slotId = 0;
            slot.slotName = "single";
            slot.engineProfile = CreateEngineProfile(thrustDeck);

            // Aerodynamic body axes, X forward. Thrust acts along the body X axis, as before.
            slot.positionAeroBodyM = Vector3.zero;
            slot.thrustDirectionAeroBody = new Vector3(1f, 0f, 0f);

            slot.geometryDeclared = false;
            slot.geometryProvenance = "UNAVAILABLE";

            slot.throttleChannel = 0;
            slot.enabled = true;

            MavPropulsionInstallationProfile installation = new MavPropulsionInstallationProfile();
            installation.installationId = InstallationId;
            installation.displayName = "F-16 single-engine reference installation";
            installation.aircraftConfiguration =
                "F-16 reference configuration as used by the Maverick reference aero/mass stack";
            installation.engines = new MavEngineInstallation[] { slot };

            return installation;
        }
    }
}
