# v0.18.1 — Mouse Camera Follow Hotfix

## Problem

In v0.18 the camera follow existed, but it could feel almost invisible because the camera target was double-smoothed and derived from `MouseAimPos`, which itself is generated from the camera ray.

## Fix

`MavMouseFlightRig` now supports direct viewport-offset camera follow:

```text
useViewportOffsetCameraFollow = true
mouseAimCameraFollowStrength = 0.45
mouseAimCameraFollowSmooth = 10.0
mouseAimCameraMaxAngle = 30
mouseAimCameraFollowDeadzone = 0.01
```

The camera now visibly leans toward the mouse cursor direction while still keeping the aircraft view stable.

## Presets

```text
F5 Balanced: strength 0.45 / smooth 10 / max angle 30
F6 Smooth: strength 0.32 / smooth 8 / max angle 22
F7 Aggressive: strength 0.58 / smooth 12 / max angle 38
```

## Notes

- This is camera-only.
- It does not move `mouseAim`.
- It does not affect aircraft physics.
- Free-look and TGP fullscreen/focus disable this follow.

## Tuning

If the camera still feels too fixed:

```text
mouseAimCameraFollowStrength = 0.55
mouseAimCameraMaxAngle = 36
```

If it moves too much:

```text
mouseAimCameraFollowStrength = 0.30
mouseAimCameraMaxAngle = 20
```
