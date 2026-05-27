# MAVERICK Fresh v0.11 — War Thunder F-15 Control Polish

## Why

User feedback:
- The aircraft still tries to climb too much.
- Controls feel slightly uncomfortable compared to War Thunder's F-15.
- It should be a little more realistic and less twitchy.

## Research basis

War Thunder's aircraft Instructor translates mouse/keyboard input into aircraft control in real time. Mouse Aim works because an Instructor layer continuously protects/stabilizes the aircraft and maps mouse aim into control-surface behavior.

War Thunder F-15E reference notes:
- F-15E is very fast and powerful.
- Good turning speed is around 800–900 km/h.
- F-15E has high G limits in game, but pitch should not feel like unlimited nose-up torque.
- W/S keyboard elevator should feel stronger/more direct than mouse-only pitch.

## Main changes

### 1. Less unwanted climb

```text
pitchGain: 0.82 -> 0.58
maxAutoPitch: 0.66 -> 0.46
noseDownTrim: 0.06 -> 0.115
inputSmoothing: 8.2 -> 10.5
```

New mouse pitch comfort system:

```text
useMousePitchComfort = true
mousePitchDeadzoneY = 0.075
mousePitchFullAtY = 0.42
mousePitchExponent = 1.25
centerPitchLevelStrength = 0.20
noseHighPitchDownAssist = 0.20
```

Meaning:
- Cursor near center does not constantly pull the nose up.
- Nose-high attitude gets gentle pitch-down assist.
- Mouse vertical input is curved instead of raw.

### 2. More War Thunder-like keyboard elevator

```text
pitchUpCommand = -1.90
pitchDownCommand = 1.90
manualPitchBoost = 1.65
keyboardPitchSuppressesMouseAim = true
```

Meaning:
- W/S feel more like manual elevator override.
- Mouse aim does not instantly fight W/S while held.

### 3. Better F-15 speed/turn behavior

```text
bestTurnSpeed = 236 m/s
targetCruiseSpeed = 260 m/s
maxCombatSpeed = 430 m/s
bestSpeedPitchAuthority = 1.06
highSpeedPitchAuthority = 0.62
```

Meaning:
- Best turn feel remains around 800–900 km/h.
- High-speed pull is softer and more controlled.
- Low-speed mush gets nose-down assist.

### 4. Less flat sideways rotation

```text
Yaw Torque Y: 12 -> 10
yawGain: 0.22 -> 0.18
maxAutoYawCommand: 0.22 -> 0.16
yawRateDampingStrength: 0.36 -> 0.42
sideSlipDampingStrength: 0.070 -> 0.082
```

The aircraft should bank-turn more than flat-yaw.

### 5. More stable bank hold

```text
maxScreenRollBankAngle: 58 -> 54
bankHoldProportional: 0.032 -> 0.030
bankHoldRollRateDamping: 0.22 -> 0.28
```

This should reduce overshoot and rolling discomfort.

## Tuning guide

### If it still climbs too much

```text
noseDownTrim = 0.14
pitchGain = 0.50
maxAutoPitch = 0.38
noseHighPitchDownAssist = 0.28
```

### If mouse pitch feels too weak

```text
pitchGain = 0.70
maxAutoPitch = 0.56
mousePitchDeadzoneY = 0.045
```

### If W/S feels too weak

```text
pitchUpCommand = -2.15
pitchDownCommand = 2.15
manualPitchBoost = 1.85
```

### If W/S feels too violent

```text
pitchUpCommand = -1.45
pitchDownCommand = 1.45
manualPitchBoost = 1.35
```

### If it still feels too floaty

```text
angularDamping = 2.15
rateDampingStrength = 0.17
pitchRecoveryStrength = 0.16
```

### If it feels too heavy

```text
angularDamping = 1.65
inputSmoothing = 8.0
pitchGain = 0.68
rollGain = 1.15
```
