# MAVERICK v1.0 Diagnostics

## Common issue: camera still wrong

Use `MaverickSceneDoctor`.

Checklist:
- Main Camera has Camera component enabled.
- Main Camera depth is high.
- Other cameras with names containing `pod`, `tgp`, `targeting`, `chase` are disabled unless they render to RenderTexture.

## Common issue: controls fight each other

Disable:
- `WarThunderMouseAircraftInput`
- old `MaverickInstructor`
- plain `ManualAircraftInput` if not needed

Use:
- `MaverickInstructorV10`

## Common issue: aircraft feels too twitchy

Set `MaverickFlightFeelTuner` preset to:
- `HeavyFighter`

Reduce:
- `MaverickInstructorV10.rollGain`
- `AircraftPhysicsController.rollTorque`

Increase:
- `AircraftPhysicsController.angularDamping`
- `MaverickInstructorV10.commandSmoothing`

## Common issue: aircraft does not follow mouse

Check:
- `MaverickAimDirectorV10.viewCamera = Main Camera`
- `MaverickInstructorV10.aimDirector = MaverickAimDirectorV10`
- `MaverickInstructorV10.controlMode = MouseAim`
