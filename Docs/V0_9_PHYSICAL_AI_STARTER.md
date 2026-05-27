# MAVERICK Fresh v0.9 — Physical AI Starter

## Goal

This is not full reinforcement learning yet.

v0.9 starts the Physical AI pipeline safely:

```text
1. Record player flight data
2. Visualize trajectories
3. Add a simple AI pilot that can chase a target
4. Prepare data for imitation learning later
```

## New scripts

```text
Scripts/PhysicalAI/MavFlightDataRecorder.cs
Scripts/PhysicalAI/MavSimpleAIPilot.cs
Scripts/PhysicalAI/MavGhostTrailRecorder.cs
Scripts/PhysicalAI/MavPhysicalAIStarterBootstrap.cs
```

## Controls

```text
F9 = start/stop CSV recording
F10 = toggle Player / SimpleAI mode
```

## Data recording

`MavFlightDataRecorder` writes CSV files to:

```text
Application.persistentDataPath/MaverickFresh/FlightLogs
```

In Unity, select `F15E_Player > MavFlightDataRecorder` and use context menu:

```text
Open Log Folder Path
```

The CSV includes:
- time
- position
- rotation
- velocity
- angular velocity
- speed / Mach estimate / G estimate
- bank angle / pitch angle
- throttle
- pitch/yaw/roll commands
- mouse cursor viewport
- bank target
- instructor state

## Simple AI pilot

`MavSimpleAIPilot` does not train anything yet.

It simply:
- finds an object with name containing `AirTarget`
- points the mouse aim rig toward the target
- adjusts throttle by distance
- recovers altitude if too low

This validates that our current flight system can be driven by AI.

## Ghost trail

`MavGhostTrailRecorder` draws sampled flight positions in Scene view with gizmos.

Use it to compare:
- player trajectories
- AI trajectories
- turn stability
- oscillation

## Recommended test

1. Press Play.
2. Fly manually for 30 seconds.
3. Press `F9` to record.
4. Do a few turns / climbs / dives.
5. Press `F9` to stop.
6. Check CSV output.
7. Add `MavAirTargetSpawner` and build targets.
8. Press `F10` to let SimpleAI chase a target.

## Next step after v0.9

v0.10 should add:

```text
- Dataset cleaner
- CSV to JSONL converter
- imitation learning labels
- target-following reward logs
- replay tool
```

Full reinforcement learning comes later.
