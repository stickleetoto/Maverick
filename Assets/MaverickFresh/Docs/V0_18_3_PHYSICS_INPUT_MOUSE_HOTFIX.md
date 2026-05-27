# v0.18.3 Physics/Input/Mouse Feel Hotfix

## Summary

This hotfix strengthens the existing v0.17+ control chain:

```text
MavMouseFlightRig -> MavInstructorController -> MavMouseFlightJet -> Rigidbody
```

CAS, TGP, ordnance, Physical AI, telemetry, and recorder paths remain on the same architecture.

## W/S Diagnosis And Fixes

- `MavFreshInput` already detects `W`, `S`, `UpArrow`, and `DownArrow` for both legacy and new Unity input backends.
- `MavInstructorController` now exposes:
  - `debugWPressed`
  - `debugSPressed`
  - `debugKeyboardPitchInput`
  - `debugFinalPitchBeforeSmoothing`
  - `debugFinalPitchAfterSmoothing`
- W/UpArrow remains nose-up through `pitchUpCommand = -2.35`.
- S/DownArrow remains nose-down through `pitchDownCommand = 2.15`.
- The old weak `MavFreshControlProfile` values `-0.85 / 0.85` were replaced with the hotfix values.
- Keyboard pitch override now uses `keyboardElevatorRateDamping = 0.06` for pitch-rate damping while held, instead of the stronger general stabilizer pitch damping.

## Mouse Authority Tuning

F5 Balanced:

```text
pitchGain 0.76
yawGain 0.22
rollGain 1.18
maxAutoPitch 0.62
inputSmoothing 6.8
mousePitchDeadzoneY 0.025
centerPitchLevelStrength 0.10
noseHighPitchDownAssist 0.12
maxScreenRollBankAngle 72
bankHoldProportional 0.052
bankHoldRollRateDamping 0.19
maxAutoYawCommand 0.24
keyboardYawAuthority 0.55
maxAutoRudderAssist 0.55
```

F6 Smooth:

```text
pitchGain 0.62
yawGain 0.17
maxAutoPitch 0.50
inputSmoothing 9.5
mousePitchDeadzoneY 0.040
centerPitchLevelStrength 0.16
maxScreenRollBankAngle 60
```

F7 Aggressive:

```text
pitchGain 0.95
yawGain 0.28
maxAutoPitch 0.78
inputSmoothing 5.2
mousePitchDeadzoneY 0.018
centerPitchLevelStrength 0.07
maxScreenRollBankAngle 82
```

## Physics Actuator Tuning

Balanced actuator values:

```text
thrust 220
turnTorque 125,16,150
angularDamping 1.35
maxAngularVelocity 4.8
maxThrottle 1.40
targetCruiseSpeed 315
maxCombatSpeed 530
```

Smooth and aggressive preset actuator variants:

```text
F6 Smooth: thrust 210, turnTorque 108,14,128, angularDamping 1.55, maxAngularVelocity 4.2, maxThrottle 1.32
F7 Aggressive: thrust 235, turnTorque 145,18,172, angularDamping 1.20, maxAngularVelocity 5.4, maxThrottle 1.48
```

`MavMouseFlightJet` now has `useAccelerationTorqueMode`, default `false`. Default behavior remains `ForceMode.Force`; if enabled for testing, control torque is applied with `ForceMode.Acceleration` without the `forceMult` multiplier.

## Q/E Rudder Behavior

- Q/E still feed `keyboardYawAuthority`, now `0.55` in the balanced hotfix path.
- Mouse left/right remains bank-led through screen roll zones and bank hold.
- Coordinated yaw assist still corrects sideslip/AoS but does not replace banked turns.

## Camera Follow Tuning

F5 Balanced:

```text
mouseAimCameraFollowStrength 0.55
mouseAimCameraFollowSmooth 12
mouseAimCameraMaxAngle 36
```

F6 Smooth:

```text
mouseAimCameraFollowStrength 0.38
mouseAimCameraFollowSmooth 9
mouseAimCameraMaxAngle 26
```

F7 Aggressive:

```text
mouseAimCameraFollowStrength 0.70
mouseAimCameraFollowSmooth 14
mouseAimCameraMaxAngle 44
```

Camera follow remains visual only. It does not move `mouseAim` and does not modify aircraft physics. Free-look overrides camera follow, and TGP fullscreen/focus disables it.

## F2 Control Debug Overlay

Press `F2` to toggle the temporary control debug overlay. It shows:

- W/S pressed state
- keyboard pitch input
- final pitch before/after smoothing
- mouse offset
- instructor P/Y/R
- jet P/Y/R
- speed, AoA, AoS, G
- target bank, bank hold command
- torque mode

`F12` still toggles the main HUD. The F2 debug overlay can remain visible even when the main HUD is hidden.

## Manual Unity Test Checklist

- Hold W: nose clearly pitches up.
- Hold S: nose clearly pitches down.
- Release W/S: aircraft smoothly returns to mouse aim control.
- Move mouse a little: flight direction visibly changes.
- Move mouse far left/right: aircraft banks strongly and avoids endless roll.
- A/D rolls the aircraft.
- Q/E yaws the nose modestly without becoming the main turn method.
- F5/F6/F7 apply distinct balanced/smooth/aggressive feels.
- F2 overlay toggles and reports W/S state accurately.
- Mouse0 gun, Space secondary, 1/2 cycling, 3 missile, 4 bomb still work.
- TGP PIP/fullscreen, CAS designation, Radar Y/N/M, Physical AI F11, and telemetry still work.
- Physical AI continues to drive `MavMouseFlightRig.mouseAim` and never directly rotates the aircraft.

## Known Limitations

- `ForceMode.Acceleration` torque is available for testing but is not the default.
- Dotnet builds may fail outside Unity if generated project references are stale or Unity editor assemblies are unavailable.
- Final flight feel still needs in-editor playtesting because mass, prefab overrides, and scene startup order can change the effective response.
