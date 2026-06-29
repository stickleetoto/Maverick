# v0.18.10 — Physics Actuator Authority Fix

## Summary

This patch fixes two practical blockers found during Play Mode testing:

1. `MavMouseFlightRig` could log `aircraft not assigned` because Unity calls `Awake()` immediately after `AddComponent`, before the bootstrap assigns references. The rig now supports explicit bootstrap initialization and no longer emits a false startup warning.
2. W/S and A/D reached the Instructor and even produced large torque values, but Rigidbody response still felt dead. Control torque now uses `ForceMode.Acceleration` by default, with separate acceleration-mode torque scales and clamps. Manual inputs also reduce damping and bypass envelope guards more strongly.

## Key changes

- Added `MavMouseFlightRig.Initialize(Transform, Camera)` and called it from `MavFreshBootstrap`.
- Enabled acceleration torque mode by default in `MavMouseFlightJet`.
- Added separate acceleration/force torque scales and clamps.
- Added manual envelope bypass, manual minimum authority, and manual damping reduction.
- Added debug overlay values for torque mode, manual input, bypass, and damping reduction.
- Added safer fallback ordnance mounts so `GunMuzzle` is not created inside the aircraft collider.

## Test checklist

1. Press Play and confirm no false `MavMouseFlightRig - aircraft not assigned` warning.
2. Confirm `GunMuzzle` warning is gone or clearly points to a scene-authored muzzle that must be moved.
3. Press F5 and F2.
4. Hold W/S and verify visible pitch response.
5. Hold A/D and verify visible roll response.
6. Confirm debug mode shows `MODE ACCEL`.
7. Fire gun and check that rounds no longer spawn from inside the aircraft collider.

## Tuning guide

- Still too slow: raise `accelerationModeTorque.x/z` and `maxAppliedTorqueAccelerationMode.x/z`.
- Too twitchy: lower `accelerationModeTorque` or raise `angularDamping`.
- Center wobble returns: lower `manualDampingReduction` or increase `angularDamping`.
- High-speed W breaks flight: lower `manualHighSpeedPitchMinAuthority` slightly or increase drag guards.
