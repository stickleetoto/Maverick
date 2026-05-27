# MAVERICK Fresh Changelog

## v0.18.3 — Physics/Input/Mouse Feel Hotfix
- Strengthened W/S elevator override with `pitchUpCommand = -2.35`, `pitchDownCommand = 2.15`, `manualPitchBoost = 2.15`, and faster keyboard elevator response/release tuning.
- Added W/S pitch debug fields on `MavInstructorController` and an optional F2 control debug overlay in `MavFreshHud`.
- Increased balanced mouse authority to `pitchGain = 0.76`, `yawGain = 0.22`, `rollGain = 1.18`, and `maxAutoPitch = 0.62`.
- Tuned physical actuator response to `turnTorque = (125, 16, 150)`, `angularDamping = 1.35`, and `maxAngularVelocity = 4.8`, with optional acceleration torque mode left off by default.
- Updated F5/F6/F7 feel presets and camera follow strengths while preserving CAS, TGP, ordnance, radar, telemetry, and Physical AI routing.

## v0.18.1 — Mouse Camera Follow Hotfix
- Made mouse camera follow use viewport cursor offset directly.
- Removed double-smoothing inside `ApplyMouseAimCameraFollow`, so the camera visibly leans toward the mouse direction.
- Increased F5/F6/F7 camera follow preset values.
- Added debug fields `mouseAimCameraViewportOffset` and `cameraFollowState`.

# MAVERICK Fresh Changelog

## v0.18 — Telemetry-Based Instructor + Camera Tuning

- Added AoA/AoS/vertical-speed telemetry estimates.
- Added `coordinatedYawAssistOutput` and Coordinated Yaw Assist tuning fields.
- Tuned F5/F6/F7 presets using F/A-18 and F-14 War Thunder telemetry observations.
- Added G/AoA protection fields for short-term high-G/high-AoA behavior.
- Improved speed feel with moderate thrust/cruise/max-combat-speed tuning.
- Added War-Thunder-like mouse aim camera follow to `MavMouseFlightRig`.
- Expanded `MavFlightDataRecorder` CSV columns for Instructor and aero telemetry.
- Added `tools/telemetry/wt_telemetry_recorder.py`.
- Added `tools/telemetry/compare_wt_maverick.py`.
- Updated HUD with compact AoA/AoS/Vy/CYAW display.

## v0.17.2 — WT Mouse Authority + Fixed Gun + WT-like Weapon Controls

- Gun shells fire from fixed GunMuzzle direction.
- Mouse0 fires primary gun.
- Space fires selected secondary weapon.
- 1/2 cycle secondary weapons.
- 3 quick-selects missile, 4 quick-selects bomb.
