# Project MAVERICK v0.8 Hotfix — Input / Camera / Air Start

## Fixes

1. A/D roll direction
   - `AircraftPhysicsController` now has:
     - `rollTorqueSign`
     - `pitchTorqueSign`
     - `yawTorqueSign`
   - Default `rollTorqueSign = 1` fixes the common reversed A/D roll issue.
   - If it is still reversed, set `rollTorqueSign = -1` in Inspector.

2. Targeting pod camera hijacking Game view
   - `TargetingPodCameraAutoSetup` now creates the pod camera disabled by default unless it has a RenderTexture.
   - This prevents the belly/landing-gear camera view from taking over.

3. Old bootstrap camera fighting the main camera
   - `EagleV05IntegratedBootstrap.createCamera` now defaults to `false`.
   - `EagleV06ResearchBootstrap` forces v05 `createCamera = false`.

4. Stable main chase camera
   - Add `MaverickStableChaseCamera` to Main Camera.
   - Assign `target = F15E_Player`.

5. Start already in flight
   - Add `MaverickAirStartConfigurator` to `F15E_Player`.
   - It starts the aircraft at altitude with forward speed and hides visual landing gear.

## Recommended setup

### Maverick_Manager
Add:
- `MaverickRuntimeCameraGuard`
  - `mainCamera = Main Camera`
  - `target = F15E_Player`
  - `installStableChaseCamera = true`

### Main Camera
Add:
- `MaverickStableChaseCamera`
  - `target = F15E_Player`
  - `distance = 24`
  - `height = 7`
  - `lookAheadDistance = 55`

### F15E_Player
Add:
- `MaverickAirStartConfigurator`
  - `startAltitude = 180`
  - `startSpeed = 170`
  - `startThrottle = 0.78`
  - `hideVisualLandingGear = true`

Check:
- `AircraftPhysicsController.rollTorqueSign = 1`
- If A/D is still reversed, set it to `-1`.

## Application
Copy the `Scripts` folder over:

`Assets/Maverick/Scripts`

Then let Unity recompile.
