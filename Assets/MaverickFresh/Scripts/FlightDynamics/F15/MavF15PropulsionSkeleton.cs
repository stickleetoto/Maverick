using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// NASA 836 F-15 twin-engine installation SKELETON.
    ///
    /// The target engine IDENTITY is frozen: two Pratt & Whitney F100-PW-100 engines on the
    /// pre-Quiet-Spike NASA F-15B 836 configuration.
    ///
    /// What is NOT frozen is the numeric engine PERFORMANCE model:
    ///   - no exact-target thrust deck;
    ///   - no accepted exact-target spool/transient law;
    ///   - no fuel-flow map;
    ///   - no inlet-recovery schedule;
    ///   - no exact mount/thrust-line coordinates.
    ///
    /// Therefore the engine profile remains data-provenance Unavailable, uses the explicit
    /// no-sourced-transient plumbing law, carries no thrust deck, and produces zero dimensional
    /// thrust. This file must not be read as a performance model merely because the variant identity
    /// is now known.
    ///
    /// The installation still proves the common architecture can describe:
    ///   - two slots;
    ///   - two independent runtime states;
    ///   - one shared engine-profile object;
    ///   - differential throttle channels;
    ///   - r x F installation moments once sourced mount coordinates exist.
    /// </summary>
    public static class MavF15PropulsionSkeleton
    {
        public const string EngineProfileId = "f15-f100-pw-100-nasa836-PERFORMANCE-UNAVAILABLE";
        public const string InstallationId = "f15-nasa836-twin-installation-SKELETON";

        /// <summary>
        /// Creates the frozen engine identity with deliberately unavailable performance data.
        /// Both installed slots share this ONE profile object; runtime state remains per-slot.
        /// </summary>
        public static MavEngineProfile CreateUnfrozenEngineProfile()
        {
            MavEngineProfile profile = MavEngineProfile.CreateInMemory(EngineProfileId);

            profile.displayName =
                "NASA 836 F100-PW-100 (performance data unavailable)";

            profile.engineVariantIdentity =
                "Pratt & Whitney F100-PW-100; NASA F-15B 836 pre-Quiet-Spike baseline";

            profile.sourceIdentity =
                "ENGINE IDENTITY FROZEN by Docs/Reference/F15_FULL_SCALE_TARGET_FREEZE_V0.1.md "
                + "(NASA/TM-2012-215978 and same-aircraft NASA source chain). "
                + "Numeric thrust/transient/inlet/install data remain UNAVAILABLE for exact target.";

            // Identity is known, but this object is a numeric engine-performance definition and those
            // numbers are still missing. Keep overall data provenance unavailable until a real deck /
            // dynamics source is accepted for the target.
            profile.provenance = MavEngineDataProvenance.Unavailable;

            // Deliberately NOT the F-16 Garza/Morelli law.
            profile.powerDynamicsLaw = MavEnginePowerDynamicsLaw.InstantNoSourcedTransient;
            profile.powerDynamicsProvenance = MavEngineDataProvenance.Unavailable;
            profile.augmentation = MavEngineAugmentationSemantics.Unavailable;

            // No exact-target thrust deck: dimensional thrust is exactly zero.
            profile.thrustDeck = null;

            profile.sourceEnvelope = MavEngineSourceEnvelope.Undeclared;
            profile.fuelFlow = MavEngineFuelFlowCapability.Unavailable;
            profile.rotorAngularMomentum = MavEngineRotorAngularMomentum.NotAvailable;

            return profile;
        }

        /// <summary>
        /// A two-slot installation with frozen engine identity but UNDECLARED mount coordinates.
        /// Zero positions are not measured centrelines; geometryDeclared=false is the authority bit.
        /// </summary>
        public static MavPropulsionInstallationProfile CreateTwinSkeleton()
        {
            MavEngineProfile shared = CreateUnfrozenEngineProfile();

            MavEngineInstallation left = new MavEngineInstallation();
            left.slotId = 0;
            left.slotName = "left";
            left.engineProfile = shared;
            left.positionAeroBodyM = Vector3.zero;
            left.thrustDirectionAeroBody = new Vector3(1f, 0f, 0f);
            left.geometryDeclared = false;
            left.geometryProvenance = "UNAVAILABLE: exact NASA 836 left engine mount/thrust line";
            left.throttleChannel = 0;
            left.enabled = true;

            MavEngineInstallation right = new MavEngineInstallation();
            right.slotId = 1;
            right.slotName = "right";
            right.engineProfile = shared;
            right.positionAeroBodyM = Vector3.zero;
            right.thrustDirectionAeroBody = new Vector3(1f, 0f, 0f);
            right.geometryDeclared = false;
            right.geometryProvenance = "UNAVAILABLE: exact NASA 836 right engine mount/thrust line";
            right.throttleChannel = 1;
            right.enabled = true;

            MavPropulsionInstallationProfile installation = new MavPropulsionInstallationProfile();
            installation.installationId = InstallationId;
            installation.displayName =
                "NASA F-15B 836 twin F100-PW-100 installation (performance/geometry incomplete)";
            installation.aircraftConfiguration =
                MavF15ReferenceData.TargetConfigurationId;
            installation.engines = new MavEngineInstallation[] { left, right };

            return installation;
        }
    }
}
