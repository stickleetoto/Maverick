using UnityEngine;
using MaverickFresh.FlightDynamics;
using MaverickFresh.FlightDynamics.F15;

namespace MaverickFresh.Gameplay
{
    /// <summary>Everything the flight session holds for one spawned player aircraft.</summary>
    public sealed class MavSpawnedAircraft
    {
        public MavPlayableAircraftDefinition definition;

        /// <summary>The physics root (the rig). Carries no renderer, camera, HUD or input.</summary>
        public MavF15PilotControlledRig rig;
        public MavSixDoFBody body;

        /// <summary>The aircraft's single operational command source.</summary>
        public MavManualPilotCommandSource commandSource;

        /// <summary>The gameplay root: player input, the smoothed render pose and the model.</summary>
        public GameObject gameplayRoot;
        public MavPlayerFlightInput input;
        public MavRenderPoseFollower renderPose;
    }

    /// <summary>
    /// The one place the game builds a player aircraft, from its <see cref="MavPlayableAircraftDefinition"/>.
    ///
    /// F-15: the pilot-controlled research rig, created from code in <see cref="MavF15PilotControlMode.AssistedV2"/>.
    /// The spawner takes no control-mode argument and there is no path from here to DirectV1. The rig is built
    /// without the V2 prefab's chase camera and without the developer HUD, whose F2 key switches laws: the
    /// session owns the one camera, and the player HUD offers no law choice. The rig still starts itself from the
    /// validated trim and enters its own ownership on the first physics step, exactly as validated.
    ///
    /// Anything without a spawn strategy is refused with a reason. Nothing is ever spawned in its place.
    /// </summary>
    public static class MavPlayableAircraftSpawner
    {
        public const string F15PhysicsName = "Mav_Player_F15";
        public const string F15GameplayName = "Mav_Player_F15_Gameplay";

        public static bool TrySpawn(MavPlayableAircraftDefinition definition, out MavSpawnedAircraft spawned, out string reason)
        {
            spawned = null;
            if (definition == null)
            {
                reason = "no aircraft definition";
                return false;
            }

            if (!definition.IsLaunchable)
            {
                reason = definition.displayName + " is not launchable (" + definition.statusText + ")";
                return false;
            }

            switch (definition.spawnStrategy)
            {
                case MavAircraftSpawnStrategy.PilotControlledF15AssistedV2:
                    return TrySpawnF15AssistedV2(definition, out spawned, out reason);
                default:
                    reason = definition.displayName + " has no player spawn strategy (" + definition.spawnStrategy + ")";
                    return false;
            }
        }

        private static bool TrySpawnF15AssistedV2(MavPlayableAircraftDefinition definition, out MavSpawnedAircraft spawned, out string reason)
        {
            spawned = null;

            // Scripted: the rig's operational manual source, which MavPlayerFlightInput writes. The rig starts itself
            // from the validated trim on its first physics step and enters its own ownership - the validated path.
            MavF15PilotControlledRig rig = MavF15PilotControlledRig.Create(
                F15PhysicsName, MavF15PilotCommandSourceKind.Scripted, true, MavF15PilotControlMode.AssistedV2);

            // Edit mode calls no Awake: build the (idempotent) stack exactly as Awake does in Play Mode.
            if (!Application.isPlaying)
                rig.EnsureStack();

            string error;
            if (!VerifyAssistedV2(rig, out error))
            {
                DestroyObject(rig.gameObject);
                reason = "F-15 rig refused: " + error;
                return false;
            }

            MavManualPilotCommandSource source = rig.commandSource as MavManualPilotCommandSource;

            GameObject gameplay = new GameObject(F15GameplayName);
            MavPlayerFlightInput input = gameplay.AddComponent<MavPlayerFlightInput>();
            input.source = source;
            input.throttleHasNoEffect = true;
            input.SetInputEnabled(false);

            MavGameplayVisuals.BuildAircraftModel(MavAircraftKind.F15E, gameplay.transform);
            MavRenderPoseFollower follower = gameplay.AddComponent<MavRenderPoseFollower>();
            follower.Bind(rig.transform);

            spawned = new MavSpawnedAircraft
            {
                definition = definition,
                rig = rig,
                body = rig.body,
                commandSource = source,
                gameplayRoot = gameplay,
                input = input,
                renderPose = follower
            };
            reason = definition.displayName + " spawned: " + rig.ActiveLaw.ControlLawName;
            return true;
        }

        /// <summary>The F-15 contract: AssistedV2 selected, V2 enabled and bound, V1 not requesting, one operational source.</summary>
        public static bool VerifyAssistedV2(MavF15PilotControlledRig rig, out string error)
        {
            if (rig == null || rig.body == null)
            {
                error = "no rig or no six-DoF body";
                return false;
            }

            if (rig.controlMode != MavF15PilotControlMode.AssistedV2)
            {
                error = "control mode is " + rig.controlMode + ", not AssistedV2";
                return false;
            }

            if (rig.lawV2 == null || !rig.lawV2.enabled || rig.body.controlLaw != rig.lawV2)
            {
                error = "the V2 law is not the enabled, bound law";
                return false;
            }

            if (rig.law != null && rig.law.enabled)
            {
                error = "the V1 law is enabled";
                return false;
            }

            if (rig.GetComponent<MavF15PilotControlledHud>() != null)
            {
                error = "the developer HUD (law switch key) is on the player aircraft";
                return false;
            }

            MavManualPilotCommandSource source = rig.commandSource as MavManualPilotCommandSource;
            if (source == null || !source.IsOperationalCommandSource || !source.commandAvailable)
            {
                error = "the command source is not the rig's operational source";
                return false;
            }

            error = null;
            return true;
        }

        private static void DestroyObject(GameObject go)
        {
            if (go == null)
                return;
            if (Application.isPlaying)
                Object.Destroy(go);
            else
                Object.DestroyImmediate(go);
        }
    }
}
