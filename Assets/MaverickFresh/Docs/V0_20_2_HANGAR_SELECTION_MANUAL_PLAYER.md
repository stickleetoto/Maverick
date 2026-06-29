# MaverickFresh v0.20.2 — Hangar Selection + Manual Mav_Player

## Goal

This patch removes the dependency on a specific `F15E_Player` object and stops using in-game 1~5 aircraft swap keys as the normal workflow.

The intended flow is now:

```text
Mav_MainLobby
  -> Mav_Hangar: choose aircraft
  -> Mav_InGame: fly the selected aircraft
```

## Manual Player Setup

In `Mav_InGame`, create the player root yourself:

```text
Mav_Player
└── AircraftVisuals
    ├── F15EX
    ├── F16
    ├── F18
    ├── F22
    └── F35
```

Attach flight/player components to `Mav_Player` as needed. `MavInGameBootstrap` will no longer silently create a new player root by default. If `Mav_Player` is missing, it logs a clear error instead.

## Aircraft Visual Names

The visual switcher recognizes these names:

- `F15EX` / `F-15EX` / `F15` / `F15E`
- `F16` / `F-16` / `F16C`
- `F18` / `F-18` / `FA18` / `F/A-18`
- `F22` / `F-22` / `F22A`
- `F35` / `F-35` / `F35A`

Only the selected aircraft visual is active. The others are hidden.

## What changed

- In-game 1~5 hotkeys are no longer part of the default workflow.
- Hangar selection is the source of truth for the selected aircraft.
- `MavGameSession.SelectedAircraft` defaults to F-15 instead of F-22.
- `Mav_Player` is the generic player root.
- Aircraft FBX/model objects are just visuals under `AircraftVisuals`.
- `MavAircraftVisualSwitcher` now scans children under `AircraftVisuals` first, so manual setup is easy.
- Placeholder visual creation is disabled by default in `MavInGameBootstrap`.
- Loose scene visual claiming is disabled by default to avoid stealing random scene models.

## Unity checklist

1. Open `Mav_InGame`.
2. Create `Mav_Player`.
3. Create child `AircraftVisuals`.
4. Put `F15EX`, `F16`, `F18`, `F22`, `F35` model objects under `AircraftVisuals`.
5. Add/keep `MavAircraftVisualSwitcher` on `Mav_Player`.
6. Put `MavInGameBootstrap` on a separate scene object, not on the model.
7. In `Mav_Hangar`, choose an aircraft and press Test Flight.
8. Confirm the selected aircraft appears in game.
