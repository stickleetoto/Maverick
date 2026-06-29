# v0.18.11 Flight Control Pipeline Audit & Actuator Fix

## Actual bottleneck

The manual control command was reaching the pipeline, but the physical response was neutralized by the actuator/profile layer:

- `MavMouseFlightJet` still defaulted to `ForceMode.Force` for control torque.
- F5/F6/F7 and F-15E/startup profiles repeatedly restored `useAccelerationTorqueMode = false`, low command rates, and `maxAngularVelocity` values around 4.
- The single legacy torque clamp did not distinguish Force torque from Acceleration torque.
- Manual input still passed through envelope/damping code that could reduce pitch and roll authority below useful response levels.

The command was not primarily lost in input. It was visible in Instructor output, then made sluggish by physics actuator mode, profile overwrite, angular velocity limits, and damping.

## Input pipeline audit

Current intended path:

`MavFreshInput / MavMouseFlightRig -> MavInstructorController -> MavMouseFlightJet -> Rigidbody`

W/S and A/D are read in `MavInstructorController.UpdateControls()`.

- W/S set `debugManualPitchInput` and the pitch override path.
- A/D set `debugManualRollInput` and the roll override path.
- Mouse aim still supplies `debugMousePitchInput` and `debugMouseYawInput`.
- Raw combined commands are exposed as `debugRawPitchCommand`, `debugRawYawCommand`, and `debugRawRollCommand`.
- Final Instructor commands are exposed as `debugFinalPitchCommand`, `debugFinalYawCommand`, and `debugFinalRollCommand`.

`MavMouseFlightJet.SetControlCommand(...)` is now the explicit command handoff. The Jet debug fields should match the Instructor final command:

- `debugReceivedPitchCommand`
- `debugReceivedYawCommand`
- `debugReceivedRollCommand`

## ForceMode.Acceleration actuator

The main control torque path now uses mode-specific torque scales:

- Acceleration mode torque: `(38, 16, 52)`
- Force mode torque: `(6200, 3200, 8200)`
- Acceleration clamp: `(80, 34, 105)`
- Force clamp: `(9000, 4200, 10500)`

The applied local torque is:

```csharp
new Vector3(
    pitchCommand * torqueScale.x,
    yawCommand * torqueScale.y,
    -rollCommand * torqueScale.z
)
```

The final torque is applied with:

```csharp
rb.AddRelativeTorque(finalTorque, useAccelerationTorqueMode ? ForceMode.Acceleration : ForceMode.Force);
```

The torque is not multiplied by `Time.deltaTime`.

## Damping and envelope bypass

Manual input now uses:

- `manualDampingReduction = 0.45`
- `manualEnvelopeBypassFactor = 0.65`
- `manualPitchMinAuthority = 0.78`
- `manualRollMinAuthority = 0.82`
- `manualYawMinAuthority = 0.60`

When W/S, A/D, or Q/E are active, angular damping and semi-aero damping are reduced, and envelope guard factors cannot pull manual pitch/roll/yaw below the configured minimum authority.

Normal damping and stability return when there is no manual input.

## Rigidbody setup

`MavMouseFlightJet.SetupPublicRigidbody()` now verifies the aircraft body can rotate:

- Ensures the Rigidbody exists before setup.
- Forces `isKinematic = false`.
- Removes freeze rotation constraints on X/Y/Z.
- Preserves position constraints.
- Ensures `maxAngularVelocity >= 8`.
- Exposes `debugRigidbodyConstraints`, `debugAngularDragEffective`, `debugAngularVelocity`, and `debugAngularResponseMagnitude`.

## Profile overwrite check

Updated overwrite paths:

- `MavWTFeelPolishController`
- `MavFreshControlProfile`
- `MavF15EFlightSpecProfile`
- `MavFreshBootstrap`

F5 Balanced now keeps:

- `useAccelerationTorqueMode = true`
- `accelerationModeTorque = (38, 16, 52)`
- `maxAppliedTorqueAccelerationMode = (80, 34, 105)`
- `controlSurfaceResponse = 16`
- `controlSurfaceReleaseResponse = 9`
- `maxPitchCommandRate = 11`
- `maxYawCommandRate = 5.5`
- `maxRollCommandRate = 13`
- `angularDamping = 1.2`
- `maxAngularVelocity = 8`

F6 Smooth is lower but still acceleration-mode playable. F7 Aggressive uses stronger acceleration torque and faster command rates.

## Emergency manual torque assist

`MavMouseFlightJet` has a temporary diagnostic fallback:

- `useDirectManualTorqueAssist = true`
- `directManualPitchAssist = 18`
- `directManualRollAssist = 24`
- `directManualYawAssist = 6`

This only applies while W/S, A/D, or Q/E are actively held. It adds a small extra `ForceMode.Acceleration` torque and exposes:

- `debugDirectManualAssistActive`
- `debugDirectManualAssistTorque`

This is a safety net to prove the Rigidbody can respond, not a replacement for the main actuator.

## F2 debug guide

During W:

- `INPUT W/S` should show W as `1`.
- `KEY P/R` should show non-zero pitch.
- `raw/final` pitch should be non-zero.
- `PIPE REC` pitch should match the Instructor final pitch.
- `ACT` pitch should move toward the received pitch.
- `FTORQ` should show non-zero pitch torque.
- `ANGVEL` / `ANGMAG` should change.

During A/D:

- `INPUT A/D` should show A or D as `1`.
- `KEY P/R` should show non-zero roll.
- `PIPE REC` roll should match Instructor final roll.
- `ACT` roll should move toward received roll.
- `FTORQ` should show non-zero roll torque.
- `ANGVEL` / `ANGMAG` should change.

If one stage is zero while the previous stage is non-zero, that stage is the next bug.

## Play Mode checklist

1. Press F5 in Play Mode.
2. Toggle F2 debug.
3. Hold W for one second and confirm received pitch, actuated pitch, final torque, and angular velocity change.
4. Hold S and confirm opposite pitch response.
5. Hold A and D and confirm visible roll response.
6. Toggle F6 and F7 and confirm neither preset reverts to Force mode.
7. Test mouse aim after releasing manual input.
8. Verify throttle, TGP, radar, CAS weapons, and Physical AI still route through the existing systems.
