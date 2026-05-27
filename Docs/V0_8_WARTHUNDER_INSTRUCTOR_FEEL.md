# MAVERICK Fresh v0.8 — WarThunder Instructor Feel

## Goal

Reduce the subtle discomfort that came from:
- camera rolling too much with the aircraft
- stabilizer fighting turns
- direct Rigidbody torque feel
- weak speed-dependent control behavior

## Main changes

### 1. Camera horizon stabilization

`MavMouseFlightRig` now has:

```text
stabilizeCameraHorizon = true
cameraRollFollowStrength = 0.30
```

Meaning:
- 0.0 = camera horizon stays almost level
- 1.0 = camera fully follows aircraft roll
- default 0.30 = War-Thunder-like partial roll follow

This should make the screen feel less uncomfortable during aggressive rolling.

### 2. Conditional attitude stabilizer

`MavMouseFlightJet` no longer applies the same leveling force all the time.

New logic:
- aim near center -> stronger wings-level recovery
- active turning -> weaker leveling
- free look -> partial leveling
- strong A/D input -> auto-level suppressed

Important fields:

```text
centerAimAngleForLeveling = 7
activeTurnLevelingMultiplier = 0.22
centerLevelingMultiplier = 1.0
freeLookLevelingMultiplier = 0.55
```

### 3. Speed-dependent authority curve

New fields:

```text
lowSpeedPitchAuthority = 0.58
bestSpeedPitchAuthority = 1.15
highSpeedPitchAuthority = 0.72

lowSpeedRollAuthority = 0.70
bestSpeedRollAuthority = 1.05
highSpeedRollAuthority = 0.82
```

Effect:
- low speed = less authority
- best turn speed = best authority
- high speed = pitch/roll authority softened

### 4. G limiter-ish pitch suppression

New fields:

```text
softGLimit = 8.5
hardGLimit = 11.0
gPitchReduction = 0.42
```

If G estimate is high and the jet is pulling, pitch command is reduced.

### 5. Low-speed nose-down assist

New fields:

```text
stallAssistSpeed = 92
stallNoseDownAssist = 0.28
stallAssistMaxPitchClamp = 0.25
```

This prevents the aircraft from mushing nose-up forever when too slow.

## Tuning guide

### If camera still rolls too much

```text
cameraRollFollowStrength = 0.15
```

### If camera feels too detached

```text
cameraRollFollowStrength = 0.45
```

### If auto-level fights turning

```text
activeTurnLevelingMultiplier = 0.10
rollLevelStrength = 0.38
```

### If it does not recover enough

```text
rollLevelStrength = 0.65
rateDampingStrength = 0.15
centerAimAngleForLeveling = 10
```

### If high-speed pull is still too strong

```text
highSpeedPitchAuthority = 0.55
gPitchReduction = 0.25
```

### If W/S feels too nerfed

```text
manualPitchBoost = 1.75
pitchUpCommand = -2.0
pitchDownCommand = 2.0
```
