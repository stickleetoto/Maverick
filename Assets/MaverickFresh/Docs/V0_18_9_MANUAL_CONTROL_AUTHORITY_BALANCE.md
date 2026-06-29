# v0.18.9 Manual Control Authority Balance Patch

## User Feedback Summary

Recent heavy-flight tuning made the aircraft feel more substantial, but W/S pitch and A/D roll became too slow and sometimes felt almost unresponsive. The target is a middle ground: heavier than the earlier toy-like feel, but still clearly reactive when the pilot holds manual controls.

## Why Controls Became Too Slow

The current control chain smooths instructor commands, applies stabilizer damping, applies high-speed and AoA/AoS protection, then smooths final Rigidbody torque. That works well for mouse aim stability, but it can over-damp keyboard pitch and roll because manual input was passing through the same smoothing and limiter behavior as automatic mouse aim.

## Manual Input Authority Boost

Manual input now has a focused boost path:

- `useManualControlAuthorityBoost = true`
- `manualPitchAuthorityBoost = 1.35`
- `manualRollAuthorityBoost = 1.45`
- `manualYawAuthorityBoost = 1.15`
- `manualPitchResponseMultiplier = 1.7`
- `manualRollResponseMultiplier = 1.8`
- `manualLimiterBypassFactor = 0.35`

This does not disable safety. It partially resists smoothing, stabilizer damping, and limiter reduction only while W/S, A/D, or Q/E are actively held.

## W/S Pitch Retune

Balanced/F5 now targets:

- `pitchUpCommand = -2.85`
- `pitchDownCommand = 2.65`
- `manualPitchBoost = 2.75`
- `keyboardElevatorResponse = 34`
- `keyboardElevatorReleaseBlend = 8.5`
- `keyboardElevatorMouseBlend = 0.03`
- `keyboardElevatorRateDamping = 0.045`

Smooth/F6 stays calmer, and Aggressive/F7 is stronger for testing.

## A/D Roll Retune

Balanced/F5 now targets:

- `rollGain = 1.25`
- `maxScreenRollBankAngle = 72`
- `bankHoldProportional = 0.048`
- `bankHoldRollRateDamping = 0.24`
- `rollCommandDeadzone = 0.025`
- `rollCommandSlewRate = 12`

Manual roll also uses `manualRollAuthorityBoost` and faster roll smoothing while A/D is held.

## Actuator Smoothing Retune

`MavMouseFlightJet` now has an explicit v0.18.9 control-surface actuator stage:

- `controlSurfaceResponse = 10.5`
- `controlSurfaceReleaseResponse = 7.5`
- `maxPitchCommandRate = 5.6`
- `maxYawCommandRate = 3.4`
- `maxRollCommandRate = 6.8`

Manual pitch/roll/yaw can temporarily multiply this response while still preserving final torque clamp behavior.

## Damping Retune

Balanced/F5 uses:

- `angularDamping = 1.45`
- `maxAngularVelocity = 4.6`
- `angularRateDampingPitch = 0.09`
- `angularRateDampingYaw = 0.12`
- `angularRateDampingRoll = 0.075`

Smooth/F6 is more damped. Aggressive/F7 is less damped.

## Velocity And Energy Protection Retune

Balanced/F5 uses:

- `velocityTurnAssistStrength = 0.042`
- `velocityTurnAssistMaxAccel = 14`
- `velocityTurnAssistInputFactor = 0.58`
- `sideSlipDamping = 0.12`
- `sideSlipDampingHighAoS = 0.24`
- `aoaDragStrength = 0.018`
- `aosDragStrength = 0.026`
- `highSpeedTurnDragStrength = 0.014`

High-speed pitch limiting now starts at `310` and reaches full effect at `500`, with manual pitch retaining at least `0.68` authority.

## Manual Play Mode Checklist

- F5: hold W, then S. Pitch should visibly respond without snapping.
- F5: hold A/D. Roll should be obvious and should stop stabilizing when released.
- F6: repeat pitch/roll checks. It should feel smoother and calmer than F5.
- F7: repeat pitch/roll checks. It should be stronger but still protected.
- At high speed, hold W. Pitch should be moderated but visible.
- Confirm mouse aim remains smooth after releasing keys.
- Confirm Q/E yaw still works and does not fight TGP key handling.
- Confirm F2 shows manual boost P/R/Y changing from 0 to 1 when controls are held.
- Confirm Physical AI still uses Instructor -> Jet without a separate control path.

## Tuning Guide

If W/S still feels too slow, increase `manualPitchResponseMultiplier` first, then `keyboardElevatorResponse`.

If A/D still feels too slow, increase `manualRollAuthorityBoost` or `maxRollCommandRate`.

If the aircraft becomes too light again, lower `manualPitchAuthorityBoost`, `manualRollAuthorityBoost`, or raise `angularDamping` slightly.

If center wobble returns, raise `rollCommandDeadzone` toward `0.035` or increase `bankHoldRollRateDamping`.

If high-speed W causes instability, lower `highSpeedManualPitchAuthorityMin` toward `0.62` or lower `manualLimiterBypassFactor`.

If F5/F6/F7 feel too similar, widen `controlSurfaceResponse`, `angularDamping`, and `rollGain` differences first.
