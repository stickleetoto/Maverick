# Project MAVERICK — Landing Gear / Wheel Setup

## Goal
Add basic landing gear physics to `F15E_Player` using Unity `WheelCollider`.

## Files
- `MaverickLandingGearPhysics.cs`
- `MaverickWheelVisualFollower.cs`
- `MaverickWheelAutoRig.cs`

## Recommended hierarchy

```text
F15E_Player
  Rigidbody
  MaverickLandingGearPhysics
  MaverickWheelAutoRig optional

  F15Eagle
    F-15E-landingOn
    F-15E-landingOff
    ...

  WheelColliders
    NoseWheelCollider
    LeftMainWheelCollider
    RightMainWheelCollider
```

## Manual setup
1. Create empty object under `F15E_Player` called `WheelColliders`.
2. Create:
   - `NoseWheelCollider`
   - `LeftMainWheelCollider`
   - `RightMainWheelCollider`
3. Add `WheelCollider` to each.
4. Put each wheel collider at the visual wheel axle center.
5. Add `MaverickLandingGearPhysics` to `F15E_Player`.
6. Assign:
   - `aircraftRigidbody = F15E_Player Rigidbody`
   - `noseWheel`
   - `leftMainWheel`
   - `rightMainWheel`
7. Set hitboxes under `Hitboxes` to `Is Trigger = On`.
8. Keep physical body colliders under `Colliders` as `Is Trigger = Off`.

## Suggested WheelCollider settings
Start with:

```text
Radius: 0.25 ~ 0.45
Suspension Distance: 0.35 ~ 0.6
Spring: 50000 ~ 90000
Damper: 6000 ~ 12000
Target Position: 0.5
Mass: 80
```

## Keys
- `G`: gear up/down
- `B`: wheel brake
- `P`: parking brake
- `A/D` or arrow keys: nose wheel steer on ground
- `W/Shift`: taxi assist throttle if enabled
- `X/Ctrl`: reverse/idle taxi assist if enabled

## Notes
This is not a full real aircraft landing model. It is a practical game/AI-training landing gear layer.
