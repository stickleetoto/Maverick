# Project MAVERICK v0.9 — War Thunder Control Rework

## Goal

Move from direct torque-style keyboard/mouse control to:

```text
Mouse aim cursor
-> desired flight direction
-> MaverickInstructor
-> pitch/roll/yaw command
-> AircraftPhysicsController
```

## Added scripts

```text
Scripts/Controls/
- MaverickControlMode.cs
- MaverickMouseAimDirector.cs
- MaverickInstructor.cs
- MaverickControlModeManager.cs
- MaverickFlightHudMarkers.cs

Scripts/Camera/
- MaverickStableChaseCamera.cs
- MaverickRuntimeCameraGuard.cs

Scripts/CAS/
- MaverickWeaponSelector.cs

Scripts/Setup/
- MaverickV09Bootstrap.cs
- MaverickAirStartConfigurator.cs

Scripts/Aircraft/LandingGear/
- MaverickLandingGearPhysics.cs
- MaverickWheelVisualFollower.cs
```

## Required setup

### Maverick_Manager

Add:

```text
MaverickV09Bootstrap
```

Assign:

```text
Aircraft Object = F15E_Player
Main Camera = Main Camera
```

Recommended:
- Keep `MaverickV07Bootstrap` if you need old research systems.
- Add `MaverickV09Bootstrap` after v0.7 setup.
- v0.9 will disable old `WarThunderMouseAircraftInput`.

### Main Camera

v0.9 can auto-add:
- `MaverickStableChaseCamera`
- `MaverickFlightHudMarkers`

### F15E_Player

v0.9 can auto-add:
- `MaverickMouseAimDirector`
- `MaverickInstructor`
- `MaverickControlModeManager`
- `MaverickWeaponSelector`
- `MaverickAirStartConfigurator`

## Controls

```text
Mouse Move = aim cursor / desired flight direction
A / D      = roll override
Q / E      = rudder override
S          = pitch override
Shift      = throttle up
Ctrl       = throttle down
W          = WEP / full power
X          = idle / throttle cut
C / Alt    = free look
G          = landing gear
B          = brake
P          = parking brake

F5 = Mouse Aim
F6 = Assisted Direct
F7 = Realistic Direct
F8 = AI Managed

R = radar mode
T = next radar track
O = lock
U = unlock

P = targeting pod mode
Y = slave pod to radar
I/J/K/L = pod slew
+/- = zoom
Enter = CAS confirm / designate
Backspace = abort

LMB = primary fire
Space = secondary / abstract CAS strike
1 = switch primary
2 = switch secondary
```

## If A/D is reversed

Select `F15E_Player` and find `AircraftPhysicsController`.

Change:

```text
Roll Torque Sign = 1
```

If still reversed:

```text
Roll Torque Sign = -1
```

## If the camera shows the belly again

1. Check there is only one screen-rendering camera.
2. `Targeting_Pod_Camera` must be disabled or assigned to a RenderTexture.
3. Add `MaverickRuntimeCameraGuard` to `Maverick_Manager`.
4. Add `MaverickStableChaseCamera` to `Main Camera`.
