using System.Collections.Generic;
using UnityEngine;
using MaverickFresh.FlightDynamics;
using MaverickFresh.FlightDynamics.F15;
using MaverickFresh.FlightDynamics.F16;

namespace MaverickFresh.Gameplay
{
    /// <summary>What the game may do with an aircraft. Explicit - never inferred from a missing reference.</summary>
    public enum MavPlayableAircraftStatus
    {
        /// <summary>Launches on its validated flight model.</summary>
        Playable = 0,

        /// <summary>Launches, with a stated limitation.</summary>
        Limited = 1,

        /// <summary>Shown to the player, not launchable: its flight model is not ready, and the blockers say why.</summary>
        NotReady = 2,

        /// <summary>Not a player aircraft. Reachable only through an explicit development launch.</summary>
        Development = 3
    }

    /// <summary>How the game puts an aircraft in the air. Describes the launch, never the aerodynamics.</summary>
    public enum MavAircraftSpawnStrategy
    {
        None = 0,

        /// <summary>The pilot-controlled F-15 research aircraft with the Maverick assisted control law (V2). Always V2.</summary>
        PilotControlledF15AssistedV2 = 1,

        /// <summary>The legacy Mav_Player stack in Mav_InGame. Development only; never a player launch.</summary>
        LegacyMouseFlightDevelopment = 2
    }

    public enum MavFlightCameraMode
    {
        Chase = 0,
        CloseChase = 1,
        Nose = 2
    }

    /// <summary>
    /// How the GAME launches one aircraft: identity, player-facing text, availability, spawn strategy and camera.
    ///
    /// Deliberately holds no flight-physics number. The aerodynamics, mass, thrust and control law live in the
    /// aircraft's own flight-dynamics profile; this only says which of them the game starts, and how honestly
    /// it may describe the result.
    ///
    /// Three identities, kept apart on purpose:
    ///   aircraft               - the SERIALIZED legacy id (MavAircraftKind). F15E stays F15E so PlayerPrefs,
    ///                            scenes and the legacy catalog keep working.
    ///   displayName            - what the player reads. Neutral where the model is not a specific variant.
    ///   physicsConfigurationId - what the flight model actually is. Developer diagnostics only.
    /// </summary>
    public sealed class MavPlayableAircraftDefinition
    {
        public MavAircraftKind aircraft;
        public string displayName;
        public string shortName;
        public string role;
        public string description;
        public string physicsConfigurationId;
        public string physicsPathDescription;
        public string flightModelLabel;
        public MavPlayableAircraftStatus status;
        public string statusText;
        public string[] blockers = new string[0];
        public MavAircraftSpawnStrategy spawnStrategy;
        public MavFlightCameraMode defaultCamera = MavFlightCameraMode.Chase;
        public string controlHint;
        public string[] facts = new string[0];

        /// <summary>Whether the normal FLY button may launch it.</summary>
        public bool IsLaunchable
        {
            get
            {
                return (status == MavPlayableAircraftStatus.Playable || status == MavPlayableAircraftStatus.Limited)
                       && spawnStrategy != MavAircraftSpawnStrategy.None
                       && spawnStrategy != MavAircraftSpawnStrategy.LegacyMouseFlightDevelopment;
            }
        }
    }

    /// <summary>
    /// The one list of aircraft the game offers, and the rule for launching them.
    ///
    /// R1 player-facing: F-15 and F-16. Everything else is Development: listed, never presented as playable,
    /// and never substituted for a selection.
    /// </summary>
    public static class MavPlayableAircraftRegistry
    {
        /// <summary>Used only when nothing has been selected at all - never as a stand-in for a failed selection.</summary>
        public const MavAircraftKind DefaultPlayerAircraft = MavAircraftKind.F15E;

        public const string F15DisplayName = "F-15 EAGLE";
        public const string F16DisplayName = "F-16 FIGHTING FALCON";
        public const string FlightControlHint = "W/S pitch   A/D roll   Q/E yaw   V camera   Esc pause";

        /// <summary>The player-facing aircraft, in display order. Fresh objects each call; the F-16 status is evaluated now.</summary>
        public static List<MavPlayableAircraftDefinition> PlayerFacing()
        {
            return new List<MavPlayableAircraftDefinition> { F15(), F16() };
        }

        /// <summary>Aircraft that exist in the code but are not finished. Development status, legacy flight model.</summary>
        public static List<MavPlayableAircraftDefinition> Development()
        {
            return new List<MavPlayableAircraftDefinition>
            {
                LegacyDevelopment(MavAircraftKind.F16C, "F-16 (LEGACY FLIGHT MODEL)", "F-16 legacy"),
                LegacyDevelopment(MavAircraftKind.FA18E, "F/A-18E (LEGACY FLIGHT MODEL)", "F/A-18E"),
                LegacyDevelopment(MavAircraftKind.F22A, "F-22A (LEGACY FLIGHT MODEL)", "F-22A"),
                LegacyDevelopment(MavAircraftKind.F35A, "F-35A (LEGACY FLIGHT MODEL)", "F-35A")
            };
        }

        public static bool TryGetPlayerFacing(MavAircraftKind aircraft, out MavPlayableAircraftDefinition definition)
        {
            List<MavPlayableAircraftDefinition> all = PlayerFacing();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].aircraft == aircraft)
                {
                    definition = all[i];
                    return true;
                }
            }

            definition = null;
            return false;
        }

        public static bool TryGetDevelopment(MavAircraftKind aircraft, out MavPlayableAircraftDefinition definition)
        {
            List<MavPlayableAircraftDefinition> all = Development();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].aircraft == aircraft)
                {
                    definition = all[i];
                    return true;
                }
            }

            definition = null;
            return false;
        }

        /// <summary>The normal launch rule. Refuses with a reason; never offers another aircraft instead.</summary>
        public static bool CanLaunchAsPlayer(MavAircraftKind aircraft, out MavPlayableAircraftDefinition definition, out string reason)
        {
            if (!TryGetPlayerFacing(aircraft, out definition))
            {
                reason = aircraft + " is not a player aircraft in this release. Nothing else is launched in its place.";
                return false;
            }

            if (!definition.IsLaunchable)
            {
                reason = definition.displayName + " cannot fly yet: " + definition.statusText
                         + (definition.blockers.Length > 0 ? " - " + string.Join("; ", definition.blockers) : "");
                return false;
            }

            reason = definition.displayName + " ready";
            return true;
        }

        /// <summary>
        /// The F-15: the pilot-controlled AFIT/Baumann/Davison research aircraft with the Maverick assisted control
        /// law V2. Always V2 - there is no field here, or anywhere in the gameplay layer, that selects V1.
        /// </summary>
        public static MavPlayableAircraftDefinition F15()
        {
            return new MavPlayableAircraftDefinition
            {
                aircraft = MavAircraftKind.F15E,
                displayName = F15DisplayName,
                shortName = "F-15",
                role = "Twin-engine air-superiority fighter",
                description = "Flies on Maverick's validated research flight model with assisted rate control. "
                              + "Air start, fixed test thrust.",
                physicsConfigurationId = MavF15PilotControlledIdentity.ConfigurationId,
                physicsPathDescription = "MavF15PilotControlledRig (AssistedV2): MavF15PilotControlLawV2 -> MavF15ControlActuator -> "
                                         + "MavF15PilotControlledAeroModel + MavF15PilotControlledFixedThrust -> MavSixDoFBody, "
                                         + "owner F15PilotControlledResearch",
                flightModelLabel = "VALIDATED FLIGHT MODEL",
                status = MavPlayableAircraftStatus.Playable,
                statusText = "READY",
                spawnStrategy = MavAircraftSpawnStrategy.PilotControlledF15AssistedV2,
                defaultCamera = MavFlightCameraMode.Chase,
                controlHint = FlightControlHint,
                facts = new[]
                {
                    "Flight model|Research aerodynamics (validated)",
                    "Controls|Assisted rate control",
                    "Thrust|Fixed test thrust",
                    "Start|Air start, 300 ft/s",
                    "Speed envelope|218 - 700 ft/s"
                }
            };
        }

        /// <summary>The F-16, with its status taken from the real state of its reference flight model.</summary>
        public static MavPlayableAircraftDefinition F16()
        {
            MavF16GameplayReadinessReport readiness = MavF16GameplayReadiness.Evaluate();
            return new MavPlayableAircraftDefinition
            {
                aircraft = MavAircraftKind.F16C,
                displayName = F16DisplayName,
                shortName = "F-16",
                role = "Single-engine multirole fighter",
                description = "The reference F-16 flight model (NASA Morelli aerodynamics) is built but not yet cleared "
                              + "to fly in the game.",
                physicsConfigurationId = "F-16 Morelli reference FDM (MavF16SelectionBinding)",
                physicsPathDescription = readiness.summary,
                flightModelLabel = "REFERENCE FLIGHT MODEL",
                status = readiness.liveReady ? MavPlayableAircraftStatus.Playable : MavPlayableAircraftStatus.NotReady,
                statusText = readiness.liveReady ? "READY" : "NOT READY",
                blockers = readiness.playerBlockers,
                spawnStrategy = MavAircraftSpawnStrategy.None,
                defaultCamera = MavFlightCameraMode.Chase,
                controlHint = FlightControlHint,
                facts = new[]
                {
                    "Flight model|NASA Morelli aerodynamics (reference)",
                    "Controls|Maverick F-16 control law v0.1",
                    "Engine|Power dynamics sourced, thrust not cleared"
                }
            };
        }

        private static MavPlayableAircraftDefinition LegacyDevelopment(MavAircraftKind aircraft, string display, string shortName)
        {
            return new MavPlayableAircraftDefinition
            {
                aircraft = aircraft,
                displayName = display,
                shortName = shortName,
                role = "Development only",
                description = "Flies on the legacy Maverick flight model (mouse-aim instructor, legacy aerodynamics). "
                              + "Not a validated flight model and not part of the player release.",
                physicsConfigurationId = "LEGACY Mav_Player stack (MavMouseFlightJet / MavInstructorController / MavAeroBody)",
                physicsPathDescription = "legacy Mav_Player in Mav_InGame, owner Legacy",
                flightModelLabel = "LEGACY FLIGHT MODEL",
                status = MavPlayableAircraftStatus.Development,
                statusText = "DEVELOPMENT",
                spawnStrategy = MavAircraftSpawnStrategy.LegacyMouseFlightDevelopment,
                controlHint = "Legacy mouse-aim controls",
                facts = new[] { "Flight model|Legacy (not validated)" }
            };
        }
    }

    /// <summary>The F-16's gameplay readiness, as the code stands - not as we would like it.</summary>
    public struct MavF16GameplayReadinessReport
    {
        public bool liveReady;
        public bool engineSourceEnvelopeDeclared;
        public bool gameplaySafetyHoldReleased;
        public bool operationalPilotCommandBridge;
        public string[] playerBlockers;
        public string summary;
    }

    /// <summary>
    /// Why the F-16 cannot fly yet, evaluated rather than assumed.
    ///
    /// Two blockers are properties of the code the game would run, stated here as constants and pinned by the
    /// gameplay validation, which reads <c>MavF16SelectionAutoSetup.cs</c> and fails if either stops being true:
    ///   - MavF16SelectionBinding holds <c>simulationEnabled = false</c> unconditionally (the safety hold);
    ///   - it wires its pilot command source as NOT operational.
    /// The third is evaluated live on the engine profile the F-16 installation builds: it declares no source
    /// envelope, so it is not acceptable for live flight.
    ///
    /// None of this is bypassed. When all three clear, the F-16 becomes Playable through the same contract.
    /// </summary>
    public static class MavF16GameplayReadiness
    {
        /// <summary>MavF16SelectionBinding.ReconcileNow sets sixDoFBody.simulationEnabled = false with no condition.</summary>
        public const bool BindingKeepsSafetyHold = true;

        /// <summary>MavF16SelectionBinding.WireReferenceStack sets pilotCommandSource.treatAsOperationalSource = false.</summary>
        public const bool BindingCommandSourceIsBench = true;

        public const string BlockerSafetyHold = "Live flight-model hold is on";
        public const string BlockerEngine = "Engine data not cleared for live flight";
        public const string BlockerInput = "Player controls not connected to the flight model";

        public static MavF16GameplayReadinessReport Evaluate()
        {
            MavF16GameplayReadinessReport r = new MavF16GameplayReadinessReport();
            r.gameplaySafetyHoldReleased = !BindingKeepsSafetyHold;
            r.operationalPilotCommandBridge = !BindingCommandSourceIsBench;

            string engineDetail;
            r.engineSourceEnvelopeDeclared = EngineSourceEnvelopeDeclared(out engineDetail);

            List<string> blockers = new List<string>();
            if (!r.gameplaySafetyHoldReleased)
                blockers.Add(BlockerSafetyHold);
            if (!r.engineSourceEnvelopeDeclared)
                blockers.Add(BlockerEngine);
            if (!r.operationalPilotCommandBridge)
                blockers.Add(BlockerInput);

            r.playerBlockers = blockers.ToArray();
            r.liveReady = blockers.Count == 0;
            r.summary = r.liveReady
                ? "F-16 reference FDM operationally ready for gameplay"
                : "F-16 reference FDM NOT_READY for gameplay: MavF16SelectionBinding safety hold "
                  + (r.gameplaySafetyHoldReleased ? "released" : "ON (simulationEnabled = false, unconditional)")
                  + "; pilot command source " + (r.operationalPilotCommandBridge ? "operational" : "bench (treatAsOperationalSource = false)")
                  + "; engine: " + engineDetail;
            return r;
        }

        /// <summary>Evaluated on the engine profile the F-16 installation actually creates.</summary>
        public static bool EngineSourceEnvelopeDeclared(out string detail)
        {
            MavEngineProfile profile = MavF16PropulsionInstallation.CreateEngineProfile(null);
            bool declared = profile != null && profile.sourceEnvelope.declared;
            detail = profile == null
                ? "no F-16 engine profile"
                : (declared ? "source envelope declared" : "source envelope UNDECLARED, so not acceptable for live flight");
            if (profile != null)
                Object.DestroyImmediate(profile);
            return declared;
        }
    }
}
