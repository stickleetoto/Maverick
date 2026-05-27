# Eagle Physical AI Lab v0.5 — Integrated Physical + Sensor AI

## Goal

v0.5 connects the aircraft physical AI loop with the radar / targeting pod / sensor-fusion loop.

The project now has two learnable policy surfaces:

```text
Flight AI:
Unity aircraft state -> PhysicalAIObservation(32) -> PhysicalAIAction(6)

Sensor AI:
Radar + targeting pod + sensor fusion state -> SensorAIObservation(28) -> SensorAIAction(8)
```

This keeps the system simple enough for a Unity MVP while preparing it for behavior cloning and later reinforcement learning.

## New systems

```text
Scripts/SensorAI/
  SensorAIObservation.cs
  SensorAIAction.cs
  SensorAIObservationBuilder.cs
  SensorActionValidator.cs
  SensorRuleCasPilot.cs
  SensorBehaviorCloningLinearPolicy.cs
  SensorAIRuntimeAgent.cs
  OnlineSensorBehaviorCloningTrainer.cs
  SensorAIHUD.cs

Scripts/Data/
  IntegratedAIDemonstrationRecorder.cs

Scripts/Setup/
  EagleV05IntegratedBootstrap.cs

Tools/
  train_sensor_policy.py
```

## Modes

Flight AI modes remain:

```text
F1 Manual
F2 RulePilot
F3 LinearPolicy
F4 ShadowPolicy
```

Sensor AI modes:

```text
F8 Manual
F9 RuleSensor
F10 LinearSensor
F11 ShadowSensor
F12 Sensor AI HUD toggle
```

## Recommended test flow

1. Put `EagleV05IntegratedBootstrap` on the F-15E root object or an empty bootstrap object.
2. Assign `aircraftObject` to the F-15E root.
3. Add `GroundUnit` to ground actors.
4. Set team: `Friendly`, `Hostile`, or `Neutral`.
5. Press Play.
6. Use War Thunder style mouse aim for flight.
7. Use R/T/O/Y/Enter for manual sensor actions.
8. Collect integrated demonstrations.
9. Try F9 RuleSensor first.
10. Try F11 ShadowSensor before F10 LinearSensor.

## Data output

Integrated samples are saved under:

```text
Application.persistentDataPath/EaglePhysicalAILab/integrated_demonstrations/
```

Each line contains:

```json
{
  "physicalObservation": [32 floats],
  "physicalAction": [6 floats],
  "sensorObservation": [28 floats],
  "sensorAction": [8 floats],
  "physicalReward": 0.0,
  "flightMode": "Manual",
  "sensorMode": "Manual"
}
```

## Why this matters

Before real reinforcement learning, the project needs stable data contracts.
This patch fixes those contracts:

- aircraft control is a 6-output action space
- sensor operation is an 8-output action space
- flight and sensor AI can be trained separately
- integrated demonstrations preserve both at once
- validators remain between policy output and execution

## Codex next work

Codex should integrate this into a real Unity project, compile, and patch errors in place.
Do not redesign the architecture until the v0.5 scene runs.
