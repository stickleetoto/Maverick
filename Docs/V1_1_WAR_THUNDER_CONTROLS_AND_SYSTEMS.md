# Project MAVERICK v1.1 — War Thunder Controls & Systems

## Sources used for design

War Thunder's key concept is not direct torque control. The game uses an Instructor layer that translates player input from mouse/keyboard/controller into the aircraft's control methods. The v1.1 design keeps this idea:

```text
Mouse Aim / Keys
-> MaverickInstructorV10
-> AircraftPhysicsController
```

War Thunder also has separate aircraft controls, weapon selector, radar/sensor workflow, and manual engine/aircraft systems. v1.1 adds a higher-level systems layer around those concepts.

## New entrypoint

Add this to `Maverick_Manager`:

```text
MaverickV11WarThunderBootstrap
```

Assign:

```text
Aircraft Object = F15E_Player
Main Camera = Main Camera
```

It installs:
- `MaverickV10Bootstrap`
- `MaverickWTKeybindProfileV11`
- `MaverickWTAircraftSystemsV11`
- `MaverickWTRadarHotasV11`
- `MaverickWTTargetingPodHotasV11`
- `MaverickWTWeaponSelectorV11`
- `MaverickWTHudV11`

## New controls

```text
Mouse Move = Mouse Aim / desired direction
Mouse2 = recenter aim
A / D = roll override
Q / E = rudder override
S = pitch override
W = WEP / full power
Shift / Ctrl = throttle up/down
X = idle
C / Alt = free look

G = gear
F = cycle flaps
[ / ] = flaps up/down
B = airbrake / brake
P = parking brake

R = radar mode
T = next radar track
O = lock selected radar track
U = unlock
` = radar power

P = targeting pod mode
Y = slave pod to radar
Enter = designate / confirm
Backspace = clear / abort

LMB = primary fire
Space = secondary / abstract CAS strike
1 = switch primary
2 = switch secondary

F5 = Mouse Aim
F6 = Assisted Direct
F7 = Realistic Direct
F8 = AI Managed
F12 = HUD
```

## Tuning notes

If A/D is reversed:
1. Try `MaverickInstructorV10 > Invert Keyboard Roll`.
2. If needed, set `AircraftPhysicsController > Roll Torque Sign = -1`.

If aircraft feels too floaty:
- Increase `AircraftPhysicsController.dragCoefficient`.
- Use `MaverickFlightFeelTuner > HeavyFighter`.

If aircraft turns too hard:
- Reduce `MaverickInstructorV10.rollGain`.
- Reduce `AircraftPhysicsController.rollTorque`.

If camera breaks:
- Use `MaverickSceneDoctor`.
- Ensure pod camera has RenderTexture or is disabled.
