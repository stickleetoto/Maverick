# Project MAVERICK v1.2 — MouseFlight Core Rework

## Goal

v1.2 absorbs the structure of the MouseFlight example into MAVERICK.

Core idea:

```text
MouseFlightRig
- NOT parented to the aircraft
- follows aircraft position
- owns mouseAim Transform
- provides MouseAimPos and BoresightPos

MouseFlightInstructor
- uses localFlyTarget = aircraft.InverseTransformPoint(MouseAimPos)
- pitch/yaw from local target
- roll from aggressiveRoll + wingsLevelRoll blend
```

## New files

```text
Scripts/Controls/MaverickMouseFlightRigV12.cs
Scripts/Controls/MaverickMouseFlightInstructorV12.cs
Scripts/UI/MaverickMouseFlightHudV12.cs
Scripts/Setup/MaverickV12MouseFlightBootstrap.cs
```

## Scene setup

Add to `Maverick_Manager`:

```text
MaverickV12MouseFlightBootstrap
```

Assign:

```text
Aircraft Object = F15E_Player
Main Camera = Main Camera
```

It will:
- install v1.1 systems if enabled
- install `MaverickMouseFlightRigV12`
- install `MaverickMouseFlightInstructorV12`
- install `MaverickMouseFlightHudV12`
- disable older input stacks
- disable old HUDs
- disable pod/chase cameras that render to Game View

## Controls

```text
Mouse Move = rotate mouseAim Transform
Mouse2 = center aim
C / LeftAlt = free look, freezes mouse aim
A / D = roll override
Q / E = rudder override
S = pitch override
W = WEP
Shift / Ctrl = throttle
X = idle
```

## HUD markers

```text
◇ = MouseAimPos
+ = BoresightPos / aircraft nose direction
○ = velocity vector
```

## Tuning

Start with:
- `MaverickMouseFlightInstructorV12.sensitivity = 3.6`
- `aggressiveTurnAngle = 12`
- `commandSmoothing = 9.5`
- `rollSign = 1`

If roll is reversed:
1. Toggle `MaverickMouseFlightInstructorV12.invertKeyboardRoll`
2. Or set `rollSign = -1`
3. Or set `AircraftPhysicsController.rollTorqueSign = -1`

If aim feels too twitchy:
- lower `MaverickMouseFlightRigV12.mouseSensitivity`
- lower `MaverickMouseFlightInstructorV12.sensitivity`
- increase `commandSmoothing`
