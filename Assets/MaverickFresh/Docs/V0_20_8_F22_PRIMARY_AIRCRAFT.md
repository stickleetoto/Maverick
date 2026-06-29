# MaverickFresh v0.20.8 — F-22 Primary Aircraft Update

## Goal

Maverick now treats the **F-22A Raptor** as the main/default aircraft. F15EX, F16, F18, and F35 remain selectable secondary aircraft.

## Changes

- Default session aircraft changed to `F22A`.
- Hangar now opens centered on the F-22 when there is no saved selection.
- In-game fallback aircraft changed to F-22.
- Visual factory and visual switcher fallbacks changed to F-22.
- F-22 profile retuned as the main playable aircraft:
  - higher thrust and cruise speed,
  - stronger high-AoA envelope,
  - improved high-altitude / ram engine behavior,
  - lower wave drag,
  - reduced dependence on combat flaps.
- Added `MavThrustVectorControl` for F-22-style pitch-dominant TVC assist.
- HUD/F2 debug now reports TVC status and torque.

## Required Mav_Player structure

```txt
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
├── MavThrustVectorControl
└── AircraftVisuals
    ├── F15EX
    ├── F16
    ├── F18
    ├── F22
    └── F35
```

`MavInGameBootstrap` can auto-install `MavThrustVectorControl`, but manual installation is also safe.

## Test checklist

1. Delete old `MavSelectedAircraft` PlayerPrefs or enter Hangar with no saved selection.
2. Hangar should center on **F-22A RAPTOR**.
3. Launch Free Flight.
4. HUD should read `MAVERICK FRESH v0.20.8 | F22A`.
5. F2 debug should show `TVC ON`.
6. Pull high AoA / low-speed pitch and confirm `TVC ACTIVE` appears.
7. Switch to F15EX/F16/F18/F35 from Hangar and confirm TVC turns OFF for non-F22 aircraft.
