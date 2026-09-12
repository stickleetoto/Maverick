using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// F-15 twin-engine installation SKELETON.
    ///
    /// This file exists to prove the architecture can DESCRIBE a twin-engine aircraft. It does not
    /// contain an F-15 engine model, and nothing in it may be read as F-15 performance data.
    ///
    /// Everything real is deliberately absent:
    ///
    ///   - ENGINE VARIANT is not frozen. "F100" is not one numeric engine (ENG-006): the public
    ///     record spans research, EMD, DEEC, PW-100/220/229 and modified research aircraft, and those
    ///     are different data sets. So the engine profile here carries provenance Unavailable and the
    ///     no-sourced-transient power law. It does NOT select the F-16 Garza/Morelli law, because
    ///     nobody has shown that law describes an F100-family engine.
    ///
    ///   - THRUST DECK is not frozen. No deck is attached, so thrust is exactly zero.
    ///
    ///   - MOUNT COORDINATES are not frozen. The left/right lateral offsets are ZERO here and
    ///     geometryDeclared is FALSE. A plausible-looking spacing would be a guessed aircraft
    ///     dimension, which the repository rules forbid, and a zero offset makes the absence visible:
    ///     an engine-out condition on this skeleton produces NO yawing moment, which is obviously
    ///     wrong and therefore cannot be mistaken for a working F-15.
    ///
    /// What the skeleton does establish, and what the P-002/P-003 validations exercise with clearly
    /// labelled SYNTHETIC geometry instead:
    ///
    ///   - two slots, two independent runtime states, one shared engine profile object
    ///   - differential throttle channels, 0 for left and 1 for right
    ///   - asymmetric thrust producing a yawing moment through r x F with no special-case yaw term
    ///
    /// Per the F-15 roadmap this is F15-R1 territory: a propulsion installation with two engine slots,
    /// "initially allowed to use null/zero engine data". Freezing real values is F15-R0/F15-R4 work.
    /// </summary>
    public static class MavF15PropulsionSkeleton
    {
        public const string EngineProfileId = "f15-engine-UNFROZEN";
        public const string InstallationId = "f15-twin-installation-SKELETON";

        /// <summary>
        /// A placeholder engine profile with no engine data in it.
        ///
        /// Returns ONE object which both slots share, because that is the real intent for a twin
        /// running two matching engines - and because it is the configuration that proves runtime state
        /// is not carried on the profile. Two slots, one profile, two independent power states.
        /// </summary>
        public static MavEngineProfile CreateUnfrozenEngineProfile()
        {
            MavEngineProfile profile = MavEngineProfile.CreateInMemory(EngineProfileId);

            profile.displayName = "F-15 engine (NOT FROZEN - no engine data)";

            // Named so nothing can read this as a variant selection.
            profile.engineVariantIdentity =
                "UNFROZEN: F-15 engine variant not yet selected. F100-PW-100/220/229, EMD and DEEC "
                + "research configurations are distinct data sets and must not be merged (ENG-006).";

            profile.sourceIdentity =
                "UNAVAILABLE. See Docs/Reference/F15_SOURCE_PACK_V0.1.md; configuration freeze is "
                + "F15-R0 and engine-profile freeze is F15-R4.";

            profile.provenance = MavEngineDataProvenance.Unavailable;

            // NOT the F-16 law. That law is F-16 reference-engine behaviour and applying it to an
            // F100-family engine would be an unproven claim.
            profile.powerDynamicsLaw = MavEnginePowerDynamicsLaw.InstantNoSourcedTransient;
            profile.powerDynamicsProvenance = MavEngineDataProvenance.Unavailable;

            profile.augmentation = MavEngineAugmentationSemantics.Unavailable;

            // No deck: thrust is exactly zero.
            profile.thrustDeck = null;

            profile.sourceEnvelope = MavEngineSourceEnvelope.Undeclared;
            profile.fuelFlow = MavEngineFuelFlowCapability.Unavailable;
            profile.rotorAngularMomentum = MavEngineRotorAngularMomentum.NotAvailable;

            return profile;
        }

        /// <summary>
        /// A twin-engine installation shape with NO frozen mount coordinates.
        ///
        /// Both slots reference the SAME engine-profile object. Lateral offsets are zero and
        /// undeclared - see the class comment for why a plausible number is not acceptable here.
        /// </summary>
        public static MavPropulsionInstallationProfile CreateTwinSkeleton()
        {
            MavEngineProfile shared = CreateUnfrozenEngineProfile();

            MavEngineInstallation left = new MavEngineInstallation();
            left.slotId = 0;
            left.slotName = "left";
            left.engineProfile = shared;
            left.positionAeroBodyM = Vector3.zero;          // UNFROZEN, not a measured centreline
            left.thrustDirectionAeroBody = new Vector3(1f, 0f, 0f);
            left.geometryDeclared = false;
            left.geometryProvenance = "UNAVAILABLE";
            left.throttleChannel = 0;
            left.enabled = true;

            MavEngineInstallation right = new MavEngineInstallation();
            right.slotId = 1;
            right.slotName = "right";
            right.engineProfile = shared;                   // same profile object, by design
            right.positionAeroBodyM = Vector3.zero;         // UNFROZEN
            right.thrustDirectionAeroBody = new Vector3(1f, 0f, 0f);
            right.geometryDeclared = false;
            right.geometryProvenance = "UNAVAILABLE";
            right.throttleChannel = 1;                      // separate channel: differential throttle
            right.enabled = true;

            MavPropulsionInstallationProfile installation = new MavPropulsionInstallationProfile();
            installation.installationId = InstallationId;
            installation.displayName = "F-15 twin-engine installation (SKELETON, no engine data)";
            installation.aircraftConfiguration =
                "UNFROZEN: first Maverick F-15 reference configuration not yet selected (F15-R0)";
            installation.engines = new MavEngineInstallation[] { left, right };

            return installation;
        }
    }
}
