# MAVERICK Fresh v0.15 — War-Thunder-like Polish

## Goal

This version polishes the existing flight/CAS/TGP stack toward a more War-Thunder-like feel.

## New scripts

```text
MavWTFeelPolishController.cs
MavWTQuickHelpOverlay.cs
```

## New hotkeys

```text
F5 = WarThunderF15Balanced
F6 = WarThunderF15Smooth
F7 = WarThunderF15Aggressive
F8 = DirectDebug
H  = quick help overlay
```

## Presets

### F5 Balanced
Recommended default. Stable but still responsive.

### F6 Smooth
Use when the aircraft feels uncomfortable, twitchy, or too pitch-happy.

### F7 Aggressive
Use when the aircraft feels too slow or too damped.

### F8 Direct Debug
Disables most instructor-like helpers for debugging only.

## What is more War-Thunder-like now

```text
Mouse aim = desired flight attitude, not raw torque
W/S = elevator override
left/right = bank-hold roll target
camera = partial horizon stabilization
TGP = slower, less jumpy slew
```

## Recommended test

```text
1. Start Play mode.
2. Press H to view the quick help card.
3. Try F5, F6, F7 in the same flight.
4. Use F6 if the aircraft feels uncomfortable.
5. Use F7 if it feels too slow.
6. Use T/O/IJKL/5/6/R/G for TGP.
7. Use F/Tab/1/2/Mouse0/Space for CAS.
```

## Tuning notes

If it still climbs too much:

```text
Use F6 Smooth
or lower Pitch Gain / Max Auto Pitch
or raise Nose Down Trim
```

If camera rolls too much:

```text
Camera Roll Follow Strength = 0.15~0.25
```

If bank turn overshoots:

```text
Bank Hold Roll Rate Damping = 0.32
Bank Hold Proportional = 0.024
```

## Next version

v0.16 should add:
- main HUD CCIP marker
- CCRP-lite release cue
- TGP WHOT/BHOT/CCD fake filters
- laser on/off
- CAS mission scoring
