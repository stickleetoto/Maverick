# FAM Virtual Aggressor R0

## Purpose

R0 provides the smallest safe bridge from the current Maverick flight sandbox to a repeatable FAM air-to-air opponent.

The repository already contains two different target concepts:

- `MavAirTargetDrone` / `MavAirTargetSpawner`: kinematic orbiting radar targets.
- `MavEnemyF15Spawner` + `MavEnemyAircraftPilot` + `MavEnemyLBMBrain`: a physically simulated enemy F-15 that uses the existing Maverick Rigidbody, flight, aero, engine, aircraft-profile, and radar-signature stack.

FAM training should start from the second path because it produces an opponent that actually flies through the same simulation stack instead of moving by direct transform updates.

## R0 behavior

`MavFamVirtualAggressorRuntime` installs a transient runtime host when a scene containing the Maverick player is loaded.

It does **not** spawn an enemy automatically.

Runtime controls:

- **F8**: spawn one virtual F-15 aggressor.
- **F9**: despawn the current virtual aggressor.

The same actions are also available as component context-menu commands in the Unity editor.

The runtime delegates aircraft construction to the existing `MavEnemyF15Spawner`.

## Safety / ownership

R0 intentionally does not modify:

- scenes
- prefabs
- shared FDM Core
- F-16/F-15 reference coefficients
- weapons
- radar implementation
- lock authority
- player controls

The aggressor remains a consumer of the existing flight stack. This patch adds only a development entry point.

## Why this comes before FAM training

Before trajectories or rewards are connected, Maverick needs a deterministic way to create and remove a real simulated opponent.

R0 establishes:

```text
Mav_InGame
    |
    +-- Player aircraft
    |
    +-- FAM virtual-aggressor runtime
            |
            +-- MavEnemyF15Spawner
                    |
                    +-- Rigidbody
                    +-- MavMouseFlightJet
                    +-- MavAeroBody
                    +-- MavAtmosphericEngine
                    +-- MavAircraftProfileApplier
                    +-- MavRadarSignature (team 1)
                    +-- MavEnemyLBMBrain
                    +-- MavEnemyAircraftPilot
```

## Next work

R1 should add an episode boundary and opponent-neutral observation interface without changing aircraft physics:

1. episode start/reset
2. stable aircraft/opponent IDs
3. relative-position and relative-velocity observations
4. energy/altitude/speed telemetry
5. terminal conditions
6. trajectory recording

Only after that should FAM actions replace or compete with the rule/LBM tactical layer.
