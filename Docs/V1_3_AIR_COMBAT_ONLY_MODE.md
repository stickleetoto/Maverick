# Project MAVERICK v1.3 — Air Combat Only Mode

## Why

Landing gear / wheel / runway logic is distracting from the core goal:
- War-Thunder-like mouse flight
- air combat feel
- radar / targeting / AI training
- airborne state logging

So v1.3 disables landing/taxi systems and starts the jet in the air.

## New entrypoint

Add this to `Maverick_Manager`:

```text
MaverickV13AirCombatBootstrap
```

Assign:

```text
Aircraft Object = F15E_Player
Main Camera = Main Camera
```

Recommended:

```text
Start Altitude = 850
Start Speed = 260
Start Throttle = 1
Disable Wheel Colliders = On
Force Gear Visual Up = On
Disable Ground Colliders = Off initially
```

## New scripts

```text
Scripts/Setup/MaverickAirCombatOnlyModeV13.cs
Scripts/Setup/MaverickV13AirCombatBootstrap.cs
Scripts/Scenario/MaverickAirTargetDroneV13.cs
Scripts/Scenario/MaverickAirCombatTestRangeV13.cs
```

## What it disables

- WheelCollider components under the aircraft
- `MaverickLandingGearPhysics`
- wheel visual follower scripts
- gear-down visuals by name heuristic
- non-main pod/chase cameras that try to render to Game View

## What it enforces

- start airborne
- high speed
- throttle set
- minimum altitude floor
- low-speed recovery if needed

## Suggested workflow

1. Ignore landing for now.
2. Tune `MaverickMouseFlightRigV12`.
3. Tune `MaverickMouseFlightInstructorV12`.
4. Add airborne target drones.
5. Add radar / TGP integration after the flight feel works.
6. Revisit landing much later as a separate module.
