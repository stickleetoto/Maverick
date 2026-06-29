# MaverickFresh v0.20.1 — Generic Player + Visual Switcher

## Goal

The flyable aircraft is no longer dependent on `F15E_Player` or any specific aircraft FBX.

New structure:

```txt
Mav_Player                         <- physics / input / weapons / camera target
└── AircraftVisuals                <- visual-only aircraft models
    ├── f15e_visual_active_model
    ├── f16c_visual_active_model
    ├── fa18e_visual_active_model
    ├── f22a_visual_active_model
    └── f35a_visual_active_model
```

Only one visual is active at a time. Switching aircraft changes:

1. visible model
2. aircraft flight profile
3. loadout values
4. HUD aircraft label

## Main files

- `Scripts/Aircraft/MavAircraftVisualSwitcher.cs`
- `Scripts/Aircraft/MavPlayerResolver.cs`
- `Scripts/Modes/MavInGameBootstrap.cs`
- `Scripts/Aircraft/MavAircraftProfileApplier.cs`
- `Scripts/MavFreshBootstrap.cs`

## How to use

In `Mav_InGame`, keep or create a manager object with `MavInGameBootstrap`.

Optional visual source slots accept either prefab assets or loose scene FBX objects:

- `f15Prefab`
- `f16Prefab`
- `fa18Prefab`
- `f22Prefab`
- `f35Prefab`

If the slots are empty, the switcher tries to auto-find loose scene objects named like:

- `F15...`
- `F16...`
- `F18...` / `FA18...`
- `F22...`
- `F35...`

If nothing is found, it creates a simple placeholder.

## Runtime test keys

In game:

```txt
1 = F-15E
2 = F-16C
3 = F/A-18E
4 = F-22A
5 = F-35A
```

These keys are development hotkeys. They switch the visual and re-apply that aircraft's profile without needing a scene reload.

## Important change

`MavFreshBootstrap` no longer applies the old F-15 profile by default.

`MavAircraftProfileApplier.renameObject` now defaults to false so the player root stays named `Mav_Player`.

CAS and PhysicalAI now resolve the current player generically through `MavPlayerResolver` instead of hard-coding `F15E_Player`.
