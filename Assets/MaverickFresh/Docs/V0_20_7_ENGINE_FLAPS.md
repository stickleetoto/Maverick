# MaverickFresh v0.20.7 — Atmospheric Engine & Combat Flaps

Base: v0.20.6 Aero Core Experimental.

## Added

- `MavAtmosphericEngine`
  - Altitude-based thrust loss.
  - Mach/ram recovery.
  - Transonic and supersonic wave drag.
  - HUD/debug values for thrust scale and wave drag.

- `MavCombatFlapSystem`
  - `F` cycles flaps: `UP -> COMBAT -> LANDING -> UP`.
  - Combat flaps increase low-speed lift/control but add drag.
  - Landing flaps add more lift/drag and auto-retract at high speed.
  - Profile reapply resets flaps to UP to avoid stacked modifiers.

## Recommended Mav_Player components

```text
Mav_Player
├── Rigidbody
├── MavMouseFlightJet
├── MavInstructorController
├── MavWTFeelPolishController
├── MavAircraftProfileApplier
├── MavAircraftVisualSwitcher
├── MavAeroBody
├── MavAtmosphericEngine
├── MavCombatFlapSystem
└── AircraftVisuals
```

## First test

1. Select F15EX in the hangar.
2. Enter in-game.
3. Press F2 for debug.
4. Confirm `ENG` and `FLAP` lines are visible.
5. At low speed, press `F` once for combat flaps and check if lift/control improves while drag increases.
6. Accelerate beyond flap limit and confirm auto-retract.

## Tuning notes

If thrust feels too weak at altitude, raise `highAltitudeThrustFloor` or lower `engineModelBlend`.
If transonic speed feels like hitting a wall, lower `waveDragStrength` or `maxWaveDragAccel`.
If flaps are too strong, lower `combatLiftSlopeMultiplier` and `landingLiftSlopeMultiplier`.
