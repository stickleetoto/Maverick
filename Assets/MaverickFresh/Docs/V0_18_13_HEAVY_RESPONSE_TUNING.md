# v0.18.13 — Heavy Response Tuning

## Goal

Keep manual input detection immediate, but make the aircraft body respond with heavier inertia.

Expected debug behavior:

- `PIPE REC` changes immediately when W/S or A/D is pressed.
- `ACT` moves toward the received command more gradually.
- `FTORQ` stays moderate.
- `ANGVEL` builds over time instead of spiking instantly.

## Preset Targets

### F5 Balanced

- `accelerationModeTorque = (20, 9, 26)`
- `maxAppliedTorqueAccelerationMode = (42, 18, 52)`
- `controlSurfaceResponse = 7.5`
- `controlSurfaceReleaseResponse = 6.5`
- `maxPitchCommandRate = 4.8`
- `maxYawCommandRate = 2.8`
- `maxRollCommandRate = 5.8`
- `angularDamping = 1.6`
- `maxAngularVelocity = 6.0`
- `useDirectManualTorqueAssist = false`

### F6 Smooth

- `accelerationModeTorque = (16, 7, 21)`
- `maxAppliedTorqueAccelerationMode = (34, 14, 42)`
- `controlSurfaceResponse = 5.8`
- `controlSurfaceReleaseResponse = 5.5`
- `maxPitchCommandRate = 3.8`
- `maxYawCommandRate = 2.2`
- `maxRollCommandRate = 4.6`
- `angularDamping = 1.9`
- `maxAngularVelocity = 5.2`
- `useDirectManualTorqueAssist = false`

### F7 Aggressive

- `accelerationModeTorque = (28, 12, 36)`
- `maxAppliedTorqueAccelerationMode = (58, 24, 72)`
- `controlSurfaceResponse = 10`
- `controlSurfaceReleaseResponse = 7.5`
- `maxPitchCommandRate = 7`
- `maxYawCommandRate = 3.8`
- `maxRollCommandRate = 8.5`
- `angularDamping = 1.35`
- `maxAngularVelocity = 7.2`
- `useDirectManualTorqueAssist = true`
- `directManualPitchAssist = 9`
- `directManualRollAssist = 12`
- `directManualYawAssist = 3.5`

## Notes

`ForceMode.Acceleration` remains enabled. This pass intentionally avoids changing input detection, command handoff, envelope guard logic, weapons, scenes, missions, UI, or architecture.

`MavMouseFlightJet.SetupPublicRigidbody()` no longer forces `maxAngularVelocity` back to 8. It now preserves the preset's heavier response target while keeping a lower safety floor.

## Play Mode Checklist

1. Press F5.
2. Enable F2 debug.
3. Hold W for one second.
4. Confirm `PIPE REC` changes immediately.
5. Confirm `ACT`, `FTORQ`, and `ANGVEL` build gradually.
6. Repeat with S, A, and D.
7. Press F6 and confirm even softer response with direct assist off.
8. Press F7 and confirm a stronger response with direct assist on.
9. Release input and confirm center wobble does not return.
