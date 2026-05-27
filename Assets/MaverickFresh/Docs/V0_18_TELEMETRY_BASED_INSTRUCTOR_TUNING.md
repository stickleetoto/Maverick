# MAVERICK Fresh v0.18 — Telemetry-Based Instructor + Camera Tuning

## Summary

v0.18 uses the War Thunder telemetry samples collected from F/A-18 and F-14 test flights to tune the MAVERICK Instructor layer and add comparison tooling.

The key design target is:

```text
mouse center / high speed / straight flight = stable
large mouse input / combat maneuver = strong authority
turning = bank-led with contextual rudder assist
camera = subtly follows mouse aim direction
```

This is still a game/sim-lite model, not a real flight model.

## War Thunder observations used

### F/A-18 high-speed straight

```text
IAS ~1210 km/h
M ~0.99
AoA ~-0.7
Ny ~1.04
aileron/elevator/rudder near 0
```

Meaning: high-speed straight flight should not constantly inject roll/yaw/pitch.

### F/A-18 strong maneuver

```text
IAS ~674 km/h
AoA ~28 deg
Ny ~10.5
aileron ~30%
elevator ~32%
rudder ~-61%
```

Meaning: the Instructor may use strong short-term authority during combat maneuvers. G/AoA protections should not kill response too early.

### F-14 pitch and turn samples

```text
F-14 pitch maneuver: elevator reached -100, aileron/rudder near 0
F-14 high-speed left turn: aileron ~28, rudder ~-9, AoS ~-2.7
```

Meaning: pure pitch should stay pitch-dominant, while turns should be bank/aileron-led with contextual rudder assist.

## New telemetry fields

`MavInstructorController` and mirrored `MavMouseFlightJet` now expose:

```text
aoaEstimateDeg
aosEstimateDeg
verticalSpeed
localVelocity
velocityPitchAngleDeg
velocityYawAngleDeg
coordinatedYawAssistOutput
```

AoA/AoS are estimated from local velocity:

```text
localVel = transform.InverseTransformDirection(rb.linearVelocity)
AoA = atan2(-localVel.y, abs(localVel.z))
AoS = atan2(localVel.x, abs(localVel.z))
```

The sign convention is documented and consistent for tuning rather than treated as real aerodynamic truth.

## Coordinated Yaw Assist

Added to `MavInstructorController`:

```text
enableCoordinatedYawAssist
aosYawAssistStrength
maxAutoRudderAssist
coordinatedYawSpeedMin
coordinatedYawFullSpeed
coordinatedYawBankFactor
coordinatedYawTurnDemandFactor
coordinatedYawDamping
coordinatedYawAssistOutput
```

Behavior:

```text
AoS estimate -> rudder/yaw correction -> clamp -> blend with existing yaw
```

It scales with speed, AoS magnitude, bank, and turn demand. It is reduced during pure pitch input so pitch maneuvers do not inject unwanted roll/yaw.

## G/AoA tuning

Added/tuned:

```text
aoaSoftLimitDeg = 24
aoaHardLimitDeg = 34
aoaPitchReduction = 0.45
sustainedGLimit = 8.8
hardGLimit = 11.2
```

Strong short-term maneuvers are allowed, but sustained high AoA/G reduces pitch-up command.

## Speed tuning

Balanced target:

```text
thrust = 215
maxThrottle = 1.35
targetCruiseSpeed = 305
maxCombatSpeed = 510
minCombatSpeed = 135
lowSpeedThrustBoost = 0.55
overspeedDrag = 0.055
speedAssistStrength = 0.22
```

## Mouse aim camera follow

`MavMouseFlightRig` now has:

```text
useMouseAimCameraFollow = true
mouseAimCameraFollowStrength = 0.25
mouseAimCameraFollowSmooth = 6.0
mouseAimCameraMaxAngle = 18
```

The camera subtly looks toward the mouse aim direction. This is camera-only. It does not move `mouseAim`, does not modify physics, and is disabled during free-look and fullscreen TGP mode.

Preset values:

```text
F5 Balanced: strength 0.25, smooth 6.0, maxAngle 18
F6 Smooth: strength 0.18, smooth 5.0, maxAngle 12
F7 Aggressive: strength 0.34, smooth 8.0, maxAngle 24
```

## Flight recorder changes

`MavFlightDataRecorder` now writes extra columns:

```text
instructor_pitch
instructor_yaw
instructor_roll
instructor_throttleIntent
aoaEstimateDeg
aosEstimateDeg
verticalSpeed
localVelocityX
localVelocityY
localVelocityZ
coordinatedYawAssistOutput
```

## War Thunder telemetry recorder

Script:

```text
tools/telemetry/wt_telemetry_recorder.py
```

Run while War Thunder is in a test flight:

```powershell
python tools/telemetry/wt_telemetry_recorder.py
```

It polls:

```text
http://localhost:8111/state
http://localhost:8111/indicators
```

It records CSV only. No game automation.

## Comparison tool

Script:

```text
tools/telemetry/compare_wt_maverick.py
```

Example:

```powershell
python tools/telemetry/compare_wt_maverick.py --wt wt.csv --mav mav.csv --out compare_out
```

Outputs:

```text
comparison_summary.txt
normalized_comparison.csv
optional PNG charts if matplotlib exists
```

## Tuning guide

### Mouse still weak

Use F7, or increase:

```text
pitchGain
yawGain
maxAutoPitch
mouseAimCameraFollowStrength
```

### Mouse yanks too hard

Use F6, or reduce:

```text
pitchGain
maxAutoPitch
mouseAimCameraFollowStrength
```

Increase:

```text
inputSmoothing
mousePitchDeadzoneY
```

### Pure pitch adds unwanted roll/yaw

Reduce:

```text
aosYawAssistStrength
maxAutoRudderAssist
coordinatedYawTurnDemandFactor
```

### Camera feels too fixed

Increase:

```text
mouseAimCameraFollowStrength
mouseAimCameraMaxAngle
```

### Camera moves too much

Reduce:

```text
mouseAimCameraFollowStrength
mouseAimCameraMaxAngle
```

### Turns feel slidey

Increase slightly:

```text
aosYawAssistStrength
sideSlipDampingStrength
```

### Yaw assist is too strong

Reduce:

```text
maxAutoRudderAssist
aosYawAssistStrength
```

### High-G feels too limited

Raise carefully:

```text
sustainedGLimit
hardGLimit
```

### Speed feels too slow

Increase carefully:

```text
thrust
targetCruiseSpeed
maxCombatSpeed
```

## Safety note

War Thunder telemetry use here is observation only:

```text
read localhost:8111 telemetry
record CSV
compare with MAVERICK logs
```

Do not add online automation or game control automation.

## Next recommended work

v0.18.1 should be a small playtest tuning patch based on:

```text
F5/F6/F7 feel
AoA/AoS HUD values
WT CSV vs MAVERICK CSV comparison
Gun/CAS/TGP regression testing
```
