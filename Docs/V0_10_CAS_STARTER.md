# MAVERICK Fresh v0.10 — CAS Starter

## Goal

Before Physical AI training, MAVERICK needs a playable CAS loop.

v0.10 adds:
- ground targets
- target designation
- weapon selection
- gun / rockets / training bomb / precision strike
- damage and destruction
- CAS HUD status

## New scripts

```text
Scripts/CAS/MavCASTarget.cs
Scripts/CAS/MavCASTargetingSystem.cs
Scripts/CAS/MavCASWeaponSystem.cs
Scripts/CAS/MavCASTestRangeSpawner.cs
Scripts/CAS/MavCASStarterBootstrap.cs
```

## Controls

```text
F = designate target or ground point
Tab = cycle ground targets
Backspace = clear designation

1 = previous weapon
2 = next weapon

Mouse0 = fire gun
Space = fire selected secondary weapon
```

Weapons:
```text
Gun
Rockets
TrainingBomb
PrecisionStrike
```

## How to build a CAS test range

Add this to an empty object or `MaverickFresh_Manager`:

```text
MavCASTestRangeSpawner
```

Then use context menu:

```text
Build CAS Test Range
```

It creates:
- simple ground plane
- ground targets named `AirTarget_Ground_*`
- `MavCASTarget` components

## Recommended test loop

1. Press Play.
2. Build CAS Test Range if not already built.
3. Fly toward the ground targets.
4. Put cursor near a target.
5. Press `F` to designate.
6. Press `2` to select Rockets / Bomb / PrecisionStrike.
7. Press `Space` to fire.
8. Use `Mouse0` for gun strafing.

## HUD

HUD shows:

```text
CAS Gun/Rockets/TrainingBomb/PrecisionStrike ammo and kills
TGT candidate/designated target
```

## Notes

This is not a real ballistics model yet.

Current implementation:
- gun = screen-radius hit check
- rockets/bombs = abstract impact point + radius damage
- precision strike = designated target/point area damage

This gives a gameplay loop first.  
Detailed ballistics, bomb fall, CCRP/CCIP, targeting pod camera, and sensor modes can be added later.

## Next step

v0.11 should add:
- CCIP pipper
- bomb fall prediction
- rocket spread
- target score/missions
- CAS data logs for Physical AI
