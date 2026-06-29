# v0.18.14 - Rate-Based Heavy Control + Anti-Wobble Patch

## User Feedback Summary

- Controls are no longer dead and `ForceMode.Acceleration` works, but the aircraft still needs more weight.
- W/S and A/D should register immediately while the aircraft body builds response more slowly.
- Center left/right wobble returned after heavy-response tuning.
- Direct input-to-torque behavior still feels too sharp when authority is high and too dead when authority is low.

## Why Direct Torque Felt Twitchy Or Dead

The previous heavy-response path smoothed control surfaces and then scaled the command into torque. That made low values feel calm, but it also meant the same input value could feel dead if torque was low or twitchy if torque was raised. The controller was still mostly asking for torque, not a physical rotation rate.

## Rate-Based Control Concept

`MavMouseFlightJet` now has an optional rate-based layer:

- Instructor output still produces pitch, yaw, and roll intent immediately.
- The jet converts that intent into a target local angular rate in degrees per second.
- Rigidbody local angular velocity is compared against that target.
- A PD controller applies `ForceMode.Acceleration` torque toward the target rate.
- Direct manual torque assist is disabled for F5/F6 and only kept for F7 as a small aggressive-mode helper.

This makes the command visible immediately while the Rigidbody has to accelerate into the requested rotation.

## Immediate Input, Heavy Aircraft Response

Expected F2 behavior when pressing W:

- INPUT W becomes `1` immediately.
- Instructor REC/final pitch changes immediately.
- RATE target pitch changes immediately.
- ANG current local angular velocity rises gradually.
- The aircraft pitches with weight instead of snapping.

Manual W/S, A/D, and Q/E now bypass command smoothing on their active axis. Mouse aim still uses smoothing when no manual key is held.

## Anti-Wobble Deadzone And Hysteresis

The center wobble patch adds:

- `mouseRollDeadzone = 0.055`
- `mouseYawDeadzone = 0.045`
- `bankHoldDeadzoneDeg = 4.0`
- `rollRateDeadzoneDeg = 3.0`
- `aosYawAssistDeadzoneDeg = 2.5`
- `rollInputEnterDeadzone = 0.065`
- `rollInputExitDeadzone = 0.040`

Roll input hysteresis prevents tiny center roll values from repeatedly changing sign. Near center, bank hold and coordinated yaw assist ignore tiny errors, and the center stabilizer damps roll rate without injecting alternating left/right commands.

## F5/F6/F7 Tuning

F5 Balanced:

- Target rates: pitch `48`, yaw `18`, roll `72` deg/s
- P: `(0.42, 0.28, 0.38)`
- D: `(0.10, 0.12, 0.09)`
- Max rate torque: `(32, 14, 40)`
- Angular damping `1.75`, max angular velocity `6.0`
- Turn drag `0.018`, side slip damping `0.14`, high-AoS side slip damping `0.30`
- Direct manual torque assist off

F6 Smooth:

- Target rates: pitch `36`, yaw `14`, roll `55` deg/s
- P: `(0.34, 0.23, 0.30)`
- D: `(0.13, 0.15, 0.12)`
- Max rate torque: `(24, 10, 30)`
- Angular damping `2.0`, max angular velocity `5.2`
- Direct manual torque assist off

F7 Aggressive:

- Target rates: pitch `62`, yaw `24`, roll `95` deg/s
- P: `(0.52, 0.34, 0.48)`
- D: `(0.08, 0.10, 0.07)`
- Max rate torque: `(44, 18, 56)`
- Angular damping `1.35`, max angular velocity `7.2`
- Direct manual torque assist on with pitch `8`, roll `10`, yaw `3`

## F2 Debug Guide

Use the F2 control panel to check:

- `PIPE REC`: Instructor command received by the jet.
- `RATE ON`: rate-based mode is active.
- `TGT`: desired local angular rate in degrees per second.
- `ANG`: current local angular velocity in degrees per second.
- `ERR`: target rate minus current rate.
- `RTORQ`: rate controller torque after per-axis clamp.
- `HYST`: roll hysteresis is engaged.
- `CENTER`: mouse roll/yaw is inside quiet center.
- `MAN`: manual pitch/roll/yaw input is active.
- `ASSIST`: direct manual torque assist is active.

## Play Mode Checklist

- Press F5 and verify Balanced is the main playable preset.
- Press W/S and confirm REC and RATE target change immediately.
- Confirm aircraft pitch angular velocity builds gradually instead of snapping.
- Press A/D and confirm roll target rate changes immediately without mouse fighting it.
- Center the mouse with no A/D pressed and confirm target roll rate returns to `0`.
- Fly hands-off near center and confirm there is no left/right hunting.
- Press Q/E and confirm modest yaw target rate.
- Press F6 and verify smoother, heavier camera/recording feel.
- Press F7 and verify faster response with direct assist reported only when manual input is active.
- Confirm throttle, afterburner/WEP, TGP, radar, gun, secondary weapon, mount validator, and Physical AI routing still work.
