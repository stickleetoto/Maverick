# MAVERICK Fresh v0.17 Instructor Refactor Notes

## Architecture Summary

v0.17 splits flight control into three layers:

- `MavMouseFlightRig` owns mouse aim, cursor position, screen roll zones, camera follow, horizon stabilization, zoom, and free look.
- `MavInstructorController` owns War-Thunder-like control interpretation and produces normalized `pitch`, `yaw`, `roll`, and `throttleIntent`.
- `MavMouseFlightJet` owns Rigidbody setup, thrust, side-slip damping, and final physical torque application.

The intended flow is:

```text
Player or Physical AI
-> MavMouseFlightRig mouseAim / cursor intent
-> MavInstructorController pitch/yaw/roll/throttleIntent
-> MavMouseFlightJet Rigidbody forces and torque
```

## What Moved Into MavInstructorController

- Mouse aim to desired pitch/yaw/roll.
- Screen roll-zone bank hold and target bank angle.
- W/S elevator override.
- A/D roll or bank intent.
- Q/E weak rudder intent.
- Yaw damping intent.
- Mouse pitch comfort and center deadzone.
- Nose-high pitch-down assist.
- G limiter behavior.
- Low-speed/stall nose-down assist.
- Speed-dependent pitch and roll authority.
- Recovery and stabilizer command shaping.
- Output smoothing.

`MavInstructorController` also publishes debug telemetry for HUD/log compatibility: fly target, local fly target, angle-off-target, bank/pitch angles, target bank, bank-hold command, speed, G estimate, turn-band factor, authority factors, and instructor state.

## What Remains In MavMouseFlightJet

- Rigidbody setup with old-compatible Unity API: `velocity`, `drag`, and `angularDrag`.
- Air-start reset through `ResetAirborne`.
- Engine thrust and speed-assist effective throttle application.
- Overspeed drag.
- Side-slip damping.
- Final Rigidbody torque from Instructor `pitch`, `yaw`, and `roll`.
- Public runtime fields mirrored from the Instructor for existing HUD, logger, CAS, and Physical AI references.

Older scripts can still read and write the legacy public tuning fields on `MavMouseFlightJet`. Use `PushLegacyTuningToInstructor()` after changing legacy jet tuning fields at runtime.

## WT Feel Presets

`MavWTFeelPolishController` still owns:

- `F5` = `WarThunderF15Balanced`
- `F6` = `WarThunderF15Smooth`
- `F7` = `WarThunderF15Aggressive`
- `F8` = `DirectDebug`

The presets continue to tune jet physics values such as thrust and turn torque, then push high-level control values into `MavInstructorController`.

`DirectDebug` disables mouse-flight autopilot assists, attitude stabilizer, yaw damper, G limiter, mouse pitch comfort, and WT keyboard elevator blending. Keyboard control remains usable for debugging.

## Physical AI Connection

Physical AI does not rotate or teleport the player aircraft.

`MavPhysicalAIController` and `MavSimpleAIPilot` steer by moving `MavMouseFlightRig.mouseAim`. Throttle commands are sent to `MavInstructorController.throttleIntent` when present and mirrored to `MavMouseFlightJet.throttle` for compatibility.

Physical AI starts disabled until `F11`, preserving the v0.16 startup behavior.

## Controls

- Mouse = aim direction.
- Middle mouse = recenter aim.
- RMB = zoom.
- `C` or `LeftAlt` = free look.
- `W/S` = strong elevator override.
- `A/D` = roll or bank intent.
- `Q/E` = weak rudder.
- `LeftShift/LeftControl` = throttle up/down.
- `X` = reduce throttle.
- `F5/F6/F7/F8` = WT feel presets.
- `F11` = Physical AI on/off.
- `F3` = Physical AI mode cycle.
- `F4` = Physical AI reward log.
- `F9` = flight CSV recording.
- CAS/TGP controls remain unchanged.

## Tuning Guide

- Tune mouse aim response on `MavInstructorController`: `pitchGain`, `yawGain`, `rollGain`, `maxAutoPitch`, `noseDownTrim`, and `inputSmoothing`.
- Tune bank hold with `maxScreenRollBankAngle`, `bankHoldProportional`, and `bankHoldRollRateDamping`.
- Tune W/S feel with `pitchUpCommand`, `pitchDownCommand`, `keyboardElevatorMouseBlend`, `keyboardElevatorResponse`, and `keyboardElevatorReleaseBlend`.
- Tune high/low speed behavior with `bestTurnSpeed`, `turnBandWidth`, pitch/roll authority fields, `softGLimit`, `hardGLimit`, and stall assist fields.
- Tune physical force strength on `MavMouseFlightJet`: `thrust`, `turnTorque`, `forceMult`, damping, and side-slip damping.

## Known Limitations

- This remains a gameplay-oriented sim-lite flight model, not a full aerodynamic model.
- Stabilizer torque is now expressed through normalized Instructor output commands, so exact v0.16 transient feel may differ slightly.
- Unity Editor compilation still needs to be confirmed after import because this repository snapshot is an importable `MaverickFresh` folder, not a complete Unity project root.
- The legacy `MavMouseFlightJet` public tuning surface is retained for compatibility; future cleanup should move callers to the Instructor directly.

## Next Recommended Work

- Open the Unity project and confirm there are no Console compile errors.
- In Play Mode, verify `F15E_Player` has exactly one `MavMouseFlightJet` and one `MavInstructorController`.
- Tune F5/F6/F7 feel after the refactor, especially stabilizer damping and bank hold.
- Move profile scripts to write Instructor fields directly instead of relying on legacy push helpers.
- Add a small in-editor diagnostic panel showing Rig, Instructor, and Jet ownership separately.
