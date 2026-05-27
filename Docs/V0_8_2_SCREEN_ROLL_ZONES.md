# MAVERICK Fresh v0.8.2 — Screen Roll Zones

## Problem

The aircraft rolled too easily unless the mouse was kept close to the exact center.
The desired behavior is closer to a screen-zone model:

```text
center corridor = aim / pitch, no roll
outer left/right zones = roll / bank turn
```

## What changed

`MavMouseFlightRig` now generates a screen-based roll command:

```text
useScreenRollZones = true
rollNoRollHalfWidth = 0.22
rollFullAtHalfWidth = 0.42
rollZoneExponent = 1.25
screenRollCommand = -1..1
isInRollZone = true/false
```

`MavMouseFlightJet` now uses that command:

```text
useScreenRollZoneSteering = true
suppressAutoRollInNoRollZone = true
noRollZoneLevelingBoost = 1.35
```

## Default zones

With `rollNoRollHalfWidth = 0.22`:

```text
x = 0.28 ~ 0.72
```

is the no-roll corridor.

With `rollFullAtHalfWidth = 0.42`:

```text
x <= 0.08 or x >= 0.92
```

is full roll.

Between those, roll fades in smoothly.

## HUD

The HUD now shows:

```text
RZONE NO-ROLL CMD 0.00
RZONE ROLL CMD 0.73
```

It also draws vertical guide lines:
- inner lines = no-roll corridor boundary
- outer faint lines = full-roll boundary

## Tuning

### If it still rolls too early

```text
MavMouseFlightRig.rollNoRollHalfWidth = 0.28
MavMouseFlightRig.rollFullAtHalfWidth = 0.44
```

### If roll starts too late

```text
MavMouseFlightRig.rollNoRollHalfWidth = 0.16
MavMouseFlightRig.rollFullAtHalfWidth = 0.38
```

### If roll comes in too suddenly

```text
MavMouseFlightRig.rollZoneExponent = 1.7
```

### If roll feels too weak

```text
MavMouseFlightJet.screenRollZoneStrength = 1.25
```

### If roll feels too strong

```text
MavMouseFlightJet.screenRollZoneStrength = 0.75
```

## Design intent

This makes the screen behave more like a practical flight game control surface:

```text
middle = stable aim / nose control
sides = intentional bank turn
```
