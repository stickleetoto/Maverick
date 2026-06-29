# v0.18.6 Flight Feel / Turn Response / Camera UX Hotfix

## User Feedback Summary
- Hard turns looked visually correct, but the aircraft kept moving along its old velocity direction too long.
- Centered mouse flight still had too much left/right roll and yaw shake.
- Mouse aim felt weak and slow, especially for visible pitch response.
- W/S pitch needed to feel more direct while still respecting G/AoA protection.
- The camera felt too rigidly attached behind the aircraft, and steep dives could feel cramped.
- The developer telemetry HUD was useful, but normal gameplay needs a cleaner default surface.

## Velocity Turn Assist
- `MavMouseFlightJet` now has a v0.18.6 velocity turn assist inside the existing Semi-Aero Stabilizer.
- The assist compares aircraft forward against `Rigidbody.linearVelocity`, then applies a clamped acceleration toward `transform.forward * speed`.
- It scales by speed, forward-velocity angle, and active pitch/yaw/roll demand.
- It keeps inertia by using `velocityTurnAssistStrength = 0.055` and `velocityTurnAssistMaxAccel = 18`.
- At very high forward-velocity angle, it reduces alignment force and adds damping drag instead of snapping the velocity vector.

## Roll / Yaw Oscillation Fixes
- Added center-stability fields:
  - `rollCommandDeadzone = 0.035`
  - `bankHoldDeadzoneDeg = 2.5`
  - `rollCommandSlewRate = 8`
  - `yawAssistDeadzoneAosDeg = 1.2`
  - `centerRollStabilizeStrength = 0.12`
- Screen roll commands now deadzone and slew before bank hold uses them.
- Bank hold ignores tiny bank errors near center, reducing hunting around wings-level.
- Coordinated yaw assist ignores tiny AoS noise and uses `coordinatedYawDamping = 0.28`.

## Mouse And W/S Tuning
- Balanced/F5:
  - `pitchGain = 0.82`, `yawGain = 0.24`, `rollGain = 1.10`
  - `maxAutoPitch = 0.68`, `inputSmoothing = 5.8`
  - `mousePitchDeadzoneY = 0.020`, `centerPitchLevelStrength = 0.08`
  - `noseHighPitchDownAssist = 0.10`, `maxScreenRollBankAngle = 70`
  - `bankHoldProportional = 0.044`, `bankHoldRollRateDamping = 0.28`
- Smooth/F6:
  - `pitchGain = 0.66`, `yawGain = 0.18`, `rollGain = 1.00`
  - `maxAutoPitch = 0.54`, `inputSmoothing = 8.5`
  - `mousePitchDeadzoneY = 0.035`, `centerPitchLevelStrength = 0.14`
  - `maxScreenRollBankAngle = 60`
- Aggressive/F7:
  - `pitchGain = 1.02`, `yawGain = 0.30`, `rollGain = 1.25`
  - `maxAutoPitch = 0.82`, `inputSmoothing = 4.6`
  - `mousePitchDeadzoneY = 0.012`, `centerPitchLevelStrength = 0.05`
  - `maxScreenRollBankAngle = 82`
- Common W/S:
  - `pitchUpCommand = -2.55`
  - `pitchDownCommand = 2.35`
  - `manualPitchBoost = 2.35`
  - `keyboardElevatorMouseBlend = 0.03`
  - `keyboardElevatorResponse = 26`
  - `keyboardElevatorReleaseBlend = 7`
  - `keyboardElevatorRateDamping = 0.05`

## Camera Lag / FOV
- `MavMouseFlightRig` now supports camera position lag, rotation lag, hard-maneuver lag, and lag distance clamping.
- Speed-based FOV uses:
  - `minSpeedFov = 60`
  - `cruiseSpeedFov = 68`
  - `maxSpeedFov = 78`
  - `fovSpeedMin = 80`
  - `fovSpeedMax = 360`
  - `fovSmooth = 4`
- Steep dives pull the camera back with:
  - `cameraMinDistance = 9`
  - `cameraMaxDistance = 18`
  - `diveCameraPullback = 4`
  - `steepDivePitchThreshold = -55`
  - `cameraCollisionRadius = 0.8`
- Free-look still overrides normal camera follow.
- TGP fullscreen/focus still disables mouse aim camera follow.

## HUD Direction
- `MavFreshHud.showDeveloperDebugOnHud` defaults to false.
- The normal HUD now stays compact with speed, altitude, throttle, weapon, and TGP summary.
- Detailed control telemetry remains available through F2 and can still be shown on the HUD by enabling the developer flag.

## Manual Play Mode Checklist
- Press F5, F6, and F7 and confirm presets do not toggle TGP PIP/focus.
- Center the mouse and do not press A/D for 20 seconds; roll/yaw should settle instead of hunting.
- Perform hard 90-degree and 180-degree turns; velocity should follow the nose sooner while keeping some slip.
- Hold W or UpArrow; the nose should pull up clearly.
- Hold S or DownArrow; the nose should push down clearly.
- Fly fast and confirm FOV widens smoothly.
- Enter a steep dive and confirm the camera pulls back rather than feeling inside the aircraft.
- Check TGP `T/V/Escape`, radar `Y/N/M`, weapons `Mouse0/Space/1/2/3/4`, mount validator, recorder, telemetry, and Physical AI `F11`.

## Tuning Guide
- If hard turns still slide too much, raise `velocityTurnAssistStrength` slightly or lower `velocityTurnAssistMinSpeed`.
- If the aircraft feels too arcade, lower `velocityTurnAssistStrength` or `velocityTurnAssistMaxAccel`.
- If mouse still feels weak, raise `pitchGain`, `yawGain`, or `maxAutoPitch` in the active preset.
- If roll shakes near center, raise `rollCommandDeadzone` or `bankHoldDeadzoneDeg` slightly.
- If W/S still feels weak, raise `manualPitchBoost` or `keyboardElevatorResponse`.
- If the camera feels too laggy, raise `cameraPositionLag` and `cameraRotationLag`.
- If FOV feels too wide or narrow, tune `cruiseSpeedFov` first, then `minSpeedFov` and `maxSpeedFov`.
