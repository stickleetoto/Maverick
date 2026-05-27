# Project MAVERICK v0.7 — Stabilization & Asset Integration Patch

## Goal

v0.7 turns the previous Eagle Physical AI scripts into a more usable Unity integration layer for the project now called **Project MAVERICK**.

The namespace remains `EaglePhysicalAI` for backwards compatibility, but the user-facing project name is now:

```text
Project MAVERICK
Physical AI Flight & CAS Simulator
```

## What v0.7 adds

### 1. Aircraft asset rig helper

New file:

```text
Scripts/Aircraft/F15EAssetRigHelper.cs
```

Purpose:

- create stable child anchors on imported F-15E/F-22 style assets
- create `NosePoint`, `CameraFollowPoint`, `TargetingPodMount`, `WeaponOrStrikeOrigin`, `EngineLeft`, `EngineRight`
- optionally create simple generated BoxColliders
- avoid expensive MeshCollider setup during early testing

Recommended root hierarchy:

```text
F15E_Player
  Model
  NosePoint
  CameraFollowPoint
  TargetingPodMount
  WeaponOrStrikeOrigin
  EngineLeft
  EngineRight
```

### 2. Aircraft training profiles

New files:

```text
Scripts/Aircraft/AircraftTrainingProfile.cs
Scripts/Aircraft/AircraftProfileApplier.cs
```

Purpose:

- tune abstract physics and sensor ranges without rewriting scripts
- built-in gameplay presets:
  - `F15EStyleCAS`
  - `F22StyleTestbed`

Hotkeys:

```text
Home = apply F-15E style profile
End = apply F-22 style profile
```

These are gameplay/AI-training profiles, not real aircraft datasheets.

### 3. Landing gear visual toggle

New file:

```text
Scripts/Aircraft/SimpleLandingGearController.cs
```

Purpose:

- hide/show gear objects by name hints
- default toggle key: `G`
- visual only, no landing gear physics

### 4. Control surface visual animator

New file:

```text
Scripts/Aircraft/AerodynamicSurfaceAnimator.cs
```

Purpose:

- optional visual animation for ailerons/elevator/rudder/flaps/airbrake
- reads `AircraftPhysicsController` inputs
- does not change physics

### 5. Episode reset and autonomous session runner

New files:

```text
Scripts/Training/EpisodeResetManager.cs
Scripts/Training/AutonomousTrainingSessionRunner.cs
```

Purpose:

- reset aircraft and ground units for repeated training episodes
- run short internal episodes at higher timeScale
- switch physical/sensor agents into rule or policy modes
- write simple episode summaries

Hotkey:

```text
Insert = toggle autonomous training runner
```

### 6. Dataset manifest writer

New file:

```text
Scripts/Data/MaverickDatasetManifestWriter.cs
```

Purpose:

- writes small `manifest.json` files under persistent data
- records project version, schema, profile, agent modes, mission stage, and dataset quality note

### 7. Procedural test range builder

New file:

```text
Scripts/Scenario/ProceduralTestRangeBuilder.cs
```

Purpose:

- creates placeholder hostile/friendly/neutral units
- creates waypoint markers
- creates one no-strike zone and one threat zone
- useful when assets are not fully placed yet

### 8. Readiness HUD

New file:

```text
Scripts/UI/MaverickReadinessHud.cs
```

Purpose:

- one-screen integration checklist
- shows missing components, sensor states, agent modes, dataset quality, training state

Toggle:

```text
F12 = show/hide readiness HUD
```

### 9. v0.7 bootstrap

New file:

```text
Scripts/Setup/MaverickV07Bootstrap.cs
```

Add this to the aircraft root or manager object.

It can automatically attach:

- v0.6 research bootstrap
- rig helper
- profile applier
- landing gear visual controller
- dataset manifest writer
- episode reset manager
- autonomous training runner
- readiness HUD
- optional procedural test range

## Fast setup

```text
1. Import Scripts into Assets/EaglePhysicalAI/Scripts.
2. Create F15E_Player root.
3. Put the aircraft model under F15E_Player/Model.
4. Add Rigidbody to F15E_Player.
5. Add MaverickV07Bootstrap to F15E_Player.
6. Assign aircraftObject = F15E_Player.
7. Enable createProceduralTestRange for first test if the scene is empty.
8. Press Play.
```

## Important fixes included

v0.7 also patches two v0.6 script issues:

- `CurriculumMissionManager` no longer references a missing `TargetingPodSystem.trackedUnit` property.
- `ProceduralTestRangeBuilder` uses `MissionWaypoint.radius`, matching the existing waypoint script.

## Tooling

New tools:

```text
Tools/validate_maverick_jsonl.py
Tools/make_maverick_training_index.py
```

Use them on exported JSONL datasets.

## Design stance

MAVERICK is a **game-style AI-training simulator**, not a real aircraft/weapon system.

The radar, targeting pod, CAS validation, strike, and threat-zone systems are deliberately abstract and intended for game AI research, tester data collection, and safe simulation workflows.
