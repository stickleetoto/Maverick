# MAVERICK Fresh v0.8.6 — Horizontal Roll Zones Only

## User request

Restore only the left/right roll zones.
Remove only the top/bottom pitch zones.

## Result

Enabled again:

```text
useScreenRollZones = true
useScreenRollZoneSteering = true
useScreenRollZoneBankHold = true
```

Disabled:

```text
useScreenPitchZones = false
useScreenPitchZoneSteering = false
useContinuousScreenBankHold = false
```

## Behavior

```text
center horizontal corridor = no roll / wings-level target
left/right outer areas = bank-hold roll target
top/bottom = no artificial pitch zones
```

Pitch returns to the normal mouse aim / instructor logic.

## HUD

Boundary guide lines are still removed.

HUD text shows only the horizontal roll zone:

```text
RZONE NO-ROLL CMD 0.00
RZONE ROLL CMD 0.62
```

No Y/PITCH zone text is shown.

## Tuning

### Make the no-roll center wider

```text
MavMouseFlightRig.rollNoRollHalfWidth = 0.28
```

### Make roll begin sooner

```text
MavMouseFlightRig.rollNoRollHalfWidth = 0.16
```

### Make full bank happen earlier

```text
MavMouseFlightRig.rollFullAtHalfWidth = 0.36
```

### Make full bank happen later

```text
MavMouseFlightRig.rollFullAtHalfWidth = 0.46
```

### If it rolls past target bank

```text
MavMouseFlightJet.bankHoldRollRateDamping = 0.32
MavMouseFlightJet.bankHoldProportional = 0.024
```

### If bank target direction is reversed

```text
MavMouseFlightJet.invertScreenRollBankTarget = true
```
