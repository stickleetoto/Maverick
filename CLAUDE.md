# Claude Worker Instructions — Maverick F-16 Live FDM Phase 1

You are working on the dedicated worker branch:

`claude/f16-live-fdm-phase1`

Do not work on `main`.
Do not merge branches.
Your eventual PR base/target is:

`feature/f16-flight-dynamics-core`

## Read first

Before editing code, read completely:

`Docs/FlightDynamics/F16_LIVE_FDM_ARCHITECTURE_V0.1.md`

Then inspect the existing implementation under:

`Assets/MaverickFresh/Scripts/FlightDynamics/`

The design document is authoritative for this task unless a compile blocker or clear contradiction is discovered. If one is found, document it instead of broadening scope silently.

## Mission

Implement Phase 1 infrastructure for the replacement flight-physics layer. The F-16 is the first validation aircraft for a reusable 6DoF core, not a one-off special case.

Required work:

1. `MavPilotCommand`
2. `MavFlightControlLawBase`
3. `MavDirectSurfaceControlLaw` for isolated C0 testing
4. `MavPropulsionModelBase`
5. `MavPropulsiveLoads`
6. `MavFlightDynamicsLoadSet`
7. Extend `MavSixDoFBody` so it evaluates aero + propulsion, sums loads once, and applies total load once
8. `MavFlightDynamicsTelemetry` with optional rate-limited logging
9. Compile-safe F-16 runtime wiring
10. Validation for control mapping, load summation, null propulsion, readiness, and duplicate-load prevention

If authoritative propulsion data is not already frozen in the repository, use a zero/null propulsion implementation. Do not invent an accurate F-16 thrust map.

## Physical ownership rules

New FDM control path:

`Input -> PilotCommand -> ControlLaw -> physical surfaces -> actuator -> aero/propulsion -> MavSixDoFBody -> Rigidbody`

Rules:

- No player/instructor/control-law code may directly call `Rigidbody.AddForce` or `Rigidbody.AddTorque`.
- `MavSixDoFBody` is the single final load-application boundary for the new FDM path.
- Never allow legacy and new systems to own the same physical effect simultaneously.
- No new aero layered over legacy fake damping.
- No new thrust layered over legacy thrust.
- No aerodynamic surface moment layered with direct control torque.
- Live takeover stays OFF by default in this phase.

## Source policy

- NASA Morelli F-16 material: authoritative technical/mathematical reference.
- AeroBenchVVPython: external behavioral validation only.
- Do not copy AeroBench source, tables, coefficients, or implementation details.
- Do not use JSBSim F-16 data/code.
- Unknown physical values must be left disabled, provisional, or explicitly documented as tuning.
- Do not call a controller the real F-16 FLCS unless actually sourced and validated.

## Hard scope boundary

Do not modify or regenerate:

- Scenes
- Prefabs
- Models
- Materials
- Weapons/guns
- Sensors/radar
- AI
- Damage/combat feedback

Do not broadly refactor legacy flight code in this task:

- `MavMouseFlightJet`
- `MavInstructorController`
- `MavAeroBody`
- `MavAtmosphericEngine`

Preferred edit scope:

- `Assets/MaverickFresh/Scripts/FlightDynamics/Core/**`
- `Assets/MaverickFresh/Scripts/FlightDynamics/F16/**`
- `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/**`

`MavF16SelectionAutoSetup.cs` may be edited only as needed for wiring, while keeping live takeover disabled by default.

## Review checklist before finishing

Check every changed file for:

- Unity axis vs aircraft-body axis mistakes
- sign-convention mistakes
- degrees vs radians mistakes
- dynamic pressure multiplied twice
- Newtons vs acceleration confusion
- force vs moment dimensionalization
- mass/inertia mapping errors
- duplicate load application
- FixedUpdate/execution-order dependency
- null component behavior
- accidental live-FDM activation

Target Unity version: `6000.3.16f1`.

## Finish procedure

1. Perform a full static review of all changes.
2. Keep all commits on `claude/f16-live-fdm-phase1`.
3. Open a PR against `feature/f16-flight-dynamics-core`.
4. Do not merge it.
5. PR description must list:
   - every changed file
   - architecture/ownership changes
   - validation performed
   - provisional/unsourced items
   - known limitations
   - confirmation that scenes/prefabs/weapons/sensors/AI were untouched
