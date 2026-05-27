# Eagle Physical AI v0.4 — War-Thunder-like Control Patch

## Goal

This patch changes the default manual control feel from raw keyboard aircraft torque to a War-Thunder-like mouse-aim flight director.

It is not a clone of War Thunder controls and does not use real aircraft flight-control laws. The goal is to make testing easier:

- mouse moves an aim reticle / desired flight direction
- aircraft follows that desired direction with assisted pitch/yaw/roll
- keyboard still supports roll, rudder, throttle, WEP/idle, and emergency direct control
- existing tester telemetry and behavior-cloning code still reads `ManualAircraftInput` fields because `WarThunderMouseAircraftInput` inherits from it

## New Files

```text
Scripts/Controls/
  WarThunderControlMode.cs
  WarThunderMouseAircraftInput.cs
  WarThunderControlHud.cs
  WarThunderChaseCamera.cs
```

## Updated Files

```text
Scripts/Aircraft/ManualAircraftInput.cs
Scripts/Setup/EagleSceneBootstrap.cs
Scripts/Setup/EaglePhysicalAIBootstrap.cs
README_SETUP.md
```

## Default Controls

```text
Mouse          Move aim reticle / desired flight direction
A / D          Roll assist override
Q / E          Rudder / yaw assist
Left Shift     Throttle up
Left Control   Throttle down
W              WEP / full throttle while held
X              Idle throttle while held
Mouse0         Primary strike/fire input
Space          Secondary strike/fire input
F              Confirm
Backspace      Abort / cancel
Left Alt       Free look
F5             Mouse Aim mode
F6             Keyboard Direct mode
F7             Stabilized Keyboard mode
```

## Modes

### MouseAim

Default mode. Mouse moves a reticle. The script projects the reticle through the camera and converts the desired direction into pitch/yaw/roll commands.

### KeyboardDirect

Raw keyboard-like control for debugging. Useful if mouse aim feels wrong or the aircraft needs recovery testing.

### StabilizedKeyboard

Keyboard control with gentle auto-level when no roll key is pressed.

## Scene Setup

Recommended:

1. Put `EaglePhysicalAIBootstrap` on an empty GameObject.
2. Assign your F-15E root object to `aircraftObject`.
3. Leave `createWarThunderControlHud` and `createWarThunderChaseCamera` enabled.
4. Press Play.

The bootstrap adds `WarThunderMouseAircraftInput` instead of the old raw keyboard-only `ManualAircraftInput`.

## Why This Matters for Physical AI

This makes human demonstration data cleaner. Instead of recording unstable raw keyboard-only control, testers can fly naturally with mouse aim while the logger still records normalized pitch/roll/yaw/throttle/actions. That data can then feed the behavior cloning and physical AI stack.
