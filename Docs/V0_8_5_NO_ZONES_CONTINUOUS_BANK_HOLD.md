# MAVERICK Fresh v0.8.5 — No Zones / Continuous Bank Hold

## User request

Remove the screen zone splitting.

## What changed

Removed the active X/Y zone behavior:

```text
useScreenRollZones = false
useScreenPitchZones = false
useScreenRollZoneSteering = false
useScreenPitchZoneSteering = false
```

The HUD no longer shows zone state.

## What stayed

The useful part from v0.8.4 remains:

```text
Bank Hold
```

But it no longer uses zones.

Instead:

```text
mouse X position smoothly maps to target bank angle
center = target bank 0
right = target right bank
left = target left bank
```

So it should not roll endlessly, but also does not require artificial screen rectangles.

## New fields in MavMouseFlightJet

```text
useContinuousScreenBankHold = true
continuousBankDeadzone = 0.035
continuousBankFullAtX = 0.42
continuousBankExponent = 1.15
```

## Tuning

### If it banks too easily near center

```text
continuousBankDeadzone = 0.06
```

### If it feels unresponsive near center

```text
continuousBankDeadzone = 0.015
```

### If full bank happens too late

```text
continuousBankFullAtX = 0.32
```

### If full bank happens too early

```text
continuousBankFullAtX = 0.46
```

### If the bank curve feels too sudden

```text
continuousBankExponent = 1.6
```

### If right mouse banks left

```text
invertScreenRollBankTarget = true
```

## Design intent

This version removes the visible/logical zone system and keeps a smoother War-Thunder-like mapping:

```text
mouse horizontal position -> desired bank attitude
not
mouse zone -> roll command
```
