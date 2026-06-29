# MaverickFresh v0.20.9 — F-22 Sensor & Signature Pass

## Goal

The F-22 is now the primary aircraft, so this patch gives it a gameplay identity beyond high-AoA/TVC: sensor dominance and low detectability.

This is a game abstraction, not a real radar model.

## Added

- `MavRadarSignature`
  - Abstract RCS / IR / stealth rating values for aircraft and drones.
- `MavF22SensorSuite`
  - Lightweight radar/IRST contact scanning.
  - Modes: RWS / TWS / STT / IRST.
  - `Z` cycles sensor mode.
  - `X` cycles detected target.
  - `Backspace` clears lock.
- `MavSensorHudOverlay`
  - Compact top-right sensor readout.
  - Target marker in camera view.
  - `F9` toggles sensor HUD.
- Air target drones now get radar signatures automatically.
- Player aircraft gets signature and sensor values from `MavAircraftRuntimeProfile`.

## F-22 role

F-22A is tuned as the primary sensor/stealth aircraft:

- Very low abstract RCS.
- Strong radar sensitivity.
- Wider radar field of view.
- Faster sensor refresh.
- Better IRST range.

Secondary aircraft still have sensors, but with lower range/sensitivity and larger signatures.

## Testing

1. Enter `Mav_Hangar`.
2. Select F-22A.
3. Launch Free Flight or Dogfight.
4. Ensure air target drones spawn.
5. Check the top-right `SENSOR` panel.
6. Press `Z` to cycle sensor modes.
7. Press `X` to cycle target.
8. Confirm the target marker appears on visible contacts.

## Notes

- This patch does not implement missile guidance or BVR combat yet.
- This patch prepares the next step: F-22 target lock → missile seeker → BVR sandbox.
