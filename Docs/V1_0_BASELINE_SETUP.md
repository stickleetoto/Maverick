# Project MAVERICK v1.0 Baseline Setup

## What changed

v1.0 makes the control stack more serious and less fragile.

Core stack:

```text
MaverickAimDirectorV10
-> MaverickInstructorV10
-> AircraftPhysicsController
-> Rigidbody
```

Scene fixing stack:

```text
MaverickV10Bootstrap
-> MaverickSceneDoctor
-> camera cleanup
-> old input disabling
-> stable chase camera
```

## Required setup

### 1. Copy Scripts

Copy the whole `Scripts` folder into:

```text
Assets/Maverick/Scripts
```

### 2. Add v1.0 Bootstrap

On `Maverick_Manager`, add:

```text
MaverickV10Bootstrap
```

Assign:

```text
Aircraft Object = F15E_Player
Main Camera = Main Camera
```

Keep old `MaverickV07Bootstrap` only if you still need the older research systems.
The new v1.0 bootstrap will disable conflicting old inputs/cameras.

### 3. Recommended components after setup

`F15E_Player` should have:

```text
AircraftPhysicsController
MaverickAimDirectorV10
MaverickInstructorV10
MaverickFlightFeelTuner
MaverickWeaponSelector
MaverickAirStartV10
```

`Main Camera` should have:

```text
MaverickStableChaseCameraV10
MaverickFlightHudV10
```

`Maverick_Manager` should have:

```text
MaverickV10Bootstrap
MaverickSceneDoctor
```

## Controls

```text
Mouse Move = aim cursor / desired direction
Mouse2 = recenter aim cursor
A / D = roll override
Q / E = rudder override
S = pitch override
Shift = throttle up
Ctrl = throttle down
W = WEP / full power
X = idle
C / LeftAlt = free look

F5 = Mouse Aim
F6 = Assisted Direct
F7 = Realistic Direct
F8 = AI Managed

G = gear
B = brake
P = parking brake

LMB = primary weapon
Space = secondary / abstract CAS strike
1 = switch primary
2 = switch secondary
Enter/F = confirm
Backspace = abort

Home = HeavyFighter feel
PageUp = ArcadeStable feel
End = RealisticDebug feel
F12 = HUD toggle
```

## If A/D is reversed

Try in this order:

1. `F15E_Player > MaverickInstructorV10 > Invert Keyboard Roll`
2. `F15E_Player > AircraftPhysicsController > Roll Torque Sign = -1`

## If camera goes under aircraft

1. Use only `Main Camera` for Game View.
2. `Targeting_Pod_Camera` must be disabled or use a RenderTexture.
3. Run `MaverickSceneDoctor > Run Diagnosis And Fix`.
4. Ensure `Main Camera` has `MaverickStableChaseCameraV10`.

## Recommended next tuning

Start with:
- Flight feel preset: `HeavyFighter`
- Air start altitude: 260
- Air start speed: 205
- Air start throttle: 0.84

Then adjust:
- `MaverickInstructorV10.pitchGain`
- `MaverickInstructorV10.rollGain`
- `MaverickInstructorV10.commandSmoothing`
- `AircraftPhysicsController.rollTorqueSign`
