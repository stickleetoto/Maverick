# MAVERICK Fresh v0.8.3 — Screen X/Y Zones, No Boundary Overlay

## User request

- Remove HUD roll-zone boundary lines.
- Split the screen vertically too, not only left/right.

## Changes

### 1. Boundary overlay removed

The vertical roll-zone guide lines are no longer drawn on the HUD.

The HUD still shows zone state as text:

```text
ZONE X:NO-ROLL 0.00  Y:NO-PITCH 0.00
ZONE X:ROLL 0.72     Y:PITCH -0.44
```

### 2. Horizontal roll zones remain

```text
X center corridor = no roll
X left/right outer zones = roll / bank turn
```

Default values:

```text
rollNoRollHalfWidth = 0.22
rollFullAtHalfWidth = 0.42
rollZoneExponent = 1.25
```

### 3. New vertical pitch zones

```text
Y center band = no pitch assist / stable
Y upper zone = pitch up intent
Y lower zone = pitch down intent
```

Default values:

```text
pitchNoPitchHalfHeight = 0.16
pitchFullAtHalfHeight = 0.40
pitchZoneExponent = 1.20
```

## What this means on screen

```text
center rectangle:
- no roll
- no pitch assist
- stable aim / level recovery

left/right:
- roll zones

top/bottom:
- pitch zones

corners:
- roll + pitch together
```

## Tuning

### Make center stable rectangle bigger

```text
MavMouseFlightRig.rollNoRollHalfWidth = 0.28
MavMouseFlightRig.pitchNoPitchHalfHeight = 0.22
```

### Make pitch begin earlier

```text
MavMouseFlightRig.pitchNoPitchHalfHeight = 0.10
```

### Make pitch less aggressive

```text
MavMouseFlightJet.screenPitchZoneStrength = 0.75
MavMouseFlightRig.pitchZoneExponent = 1.6
```

### Make pitch stronger

```text
MavMouseFlightJet.screenPitchZoneStrength = 1.25
```

### Disable vertical pitch zones

```text
MavMouseFlightRig.useScreenPitchZones = false
MavMouseFlightJet.useScreenPitchZoneSteering = false
```
