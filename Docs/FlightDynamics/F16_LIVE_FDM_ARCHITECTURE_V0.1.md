# Maverick F-16 Live Flight Dynamics Architecture v0.1

Status: DESIGN FREEZE CANDIDATE
Branch baseline: `feature/f16-flight-dynamics-core`
Target Unity: 6000.3.16f1

## 1. Goal

Build a reusable, physically coherent six-degree-of-freedom flight-dynamics engine in Maverick, with the F-16 as the first reference aircraft.

The F-16 is a validation aircraft, not a one-off hard-coded special case. The core must later accept F-15/F-22 profiles and aircraft-specific aerodynamic/propulsion/control-law models without rewriting the rigid-body engine.

## 2. Source policy

- NASA Morelli F-16 material is the authoritative technical/mathematical reference for the F-16 reference implementation.
- AeroBenchVVPython is external validation only.
- Do not copy AeroBench source code, lookup tables, coefficients, or implementation details into Maverick.
- Do not use JSBSim F-16 data/code in this branch.
- If a physical value is not supported by the selected NASA reference, do not silently invent it. Mark it `provisional`, keep it disabled, or expose it as an explicit unsourced tuning parameter.

## 3. Hard blast-radius constraints

Do not modify or regenerate:

- Scenes
- Prefabs
- Models
- Materials
- Weapons/gun code
- Sensors/radar code
- AI code
- Damage/combat feedback code

Do not make broad edits to legacy flight code during the first live-FDM phases.

In particular, do not refactor `MavMouseFlightJet`, `MavInstructorController`, `MavAeroBody`, or `MavAtmosphericEngine` merely to make the new engine compile. The new FDM is developed in parallel and takes ownership only through an explicit runtime ownership switch after readiness checks pass.

## 4. Existing architecture to preserve

The branch already contains:

- `MavFlightDynamicsProfile` — physics-only profile contract
- `MavSixDoFBody` — Unity Rigidbody integration boundary
- `MavAtmosphereModel` — atmosphere sampling
- `MavMassProperties` — mass/CG/inertia mapping
- `MavF16FlightDynamicsProfile` — F-16 physics profile
- `MavF16AeroModel` — Morelli aerodynamic coefficient model
- `MavF16ControlActuator` — physical surface-state owner
- `MavF16MorelliReference` / `MavF16MassReference`
- regression validation code
- runtime F-16 selection auto-setup

`MavAircraftRuntimeProfile` remains the legacy game-facing profile and must not become the source of truth for the new physical engine.

## 5. Ownership rule

Exactly one system owns each physical effect.

### New FDM ownership

- Gravity: `MavSixDoFBody` / Unity Rigidbody, according to physics profile
- Atmosphere: `MavAtmosphereModel`
- Aerodynamic force/moment: aircraft aerodynamic model (`MavF16AeroModel` for F-16)
- Mass/CG/inertia: `MavFlightDynamicsProfile.massProperties`
- Control-surface state: aircraft actuator (`MavF16ControlActuator`)
- Propulsive force/moment: propulsion model
- Pilot-command shaping / stability augmentation: flight-control-law model

### Legacy ownership while new FDM is inactive

Legacy Maverick continues to fly aircraft exactly as before.

### Migration invariant

Never allow both new and legacy systems to apply the same force/moment at the same time. A new physical term replaces its legacy owner 1:1; it is not layered on top.

## 6. Target runtime pipeline

```text
Player Input
    |
    v
MavPilotCommand                 normalized intent only
    |
    v
MavFlightControlLawBase         command shaping / augmentation
    |
    v
MavControlInput                 physical deflection degrees + throttle
    |
    v
MavF16ControlActuator           bounded/rate-limited actual surfaces
    |
    +-------------------------------+
    |                               |
    v                               v
MavF16AeroModel              MavPropulsionModelBase
    |                               |
    v                               v
Aerodynamic loads             Propulsive loads
    |                               |
    +---------------+---------------+
                    |
                    v
              MavSixDoFBody
                    |
                    v
             Unity Rigidbody
```

No player-control path may call `Rigidbody.AddTorque` or `AddForce` directly. Player/instructor/FBW code produces commands; physics models produce loads; `MavSixDoFBody` is the single load-application boundary.

## 7. Core types to add

### 7.1 `MavPilotCommand`

Aircraft-independent normalized pilot intent:

- pitch: -1..1
- roll: -1..1
- yaw: -1..1
- throttle: 0..1
- optional trim channels later

It must contain no Unity torque/force values.

### 7.2 `MavFlightControlLawBase`

Contract:

```text
(state, atmosphere, pilotCommand, profile) -> MavControlInput
```

Responsibilities:

- command shaping
- rate/G/AoA protection if implemented
- control allocation
- no Rigidbody access
- no aerodynamic force calculation

Phase 1 may use a deliberately simple direct-surface law so the physical engine can be tested before a more sophisticated instructor/FBW exists.

Do not call a control law “real F-16 FLCS” unless it is actually sourced and validated. Unsourced controllers must be named/described as Maverick control laws.

### 7.3 `MavPropulsionModelBase`

Contract:

```text
(state, atmosphere, throttle, dt) -> MavPropulsiveLoads
```

`MavPropulsiveLoads` contains:

- force in conventional aircraft body axes [N]
- moment in conventional aircraft body axes [N m]
- debug engine state / thrust if useful

Responsibilities:

- throttle/power state
- engine lag/spool dynamics
- Mach/altitude dependence
- thrust application location/moment if modeled

It must not call Rigidbody directly.

If authoritative thrust-map data is not yet frozen, first implement the interface/state machine with live output disabled or explicitly provisional. Do not fabricate a “NASA accurate” thrust map.

### 7.4 `MavFlightDynamicsLoadSet`

A small value type that carries:

- aerodynamic loads
- propulsive loads
- total loads

This makes ownership observable and helps detect double application.

### 7.5 `MavFlightDynamicsTelemetry`

Capture at minimum:

- time
- altitude
- TAS
- Mach
- dynamic pressure
- alpha / beta
- p / q / r
- control commands
- actual surface deflections
- CX/CY/CZ/Cl/Cm/Cn
- aerodynamic force/moment
- propulsion force/moment
- total force/moment
- profile validity / envelope status

Console output must be rate-limited and optional. CSV output can be added behind an explicit toggle.

## 8. `MavSixDoFBody` target responsibility

`MavSixDoFBody` must become a thin integrator, not an aircraft brain.

It should:

1. sample Rigidbody state
2. sample atmosphere
3. build `MavFlightState`
4. call aerodynamic model
5. call propulsion model
6. sum dimensional loads exactly once
7. apply total force/moment exactly once
8. expose telemetry/debug state

It should not:

- interpret mouse input
- implement F-16-specific control laws
- contain aircraft-specific coefficient tables
- perform arcade velocity alignment
- implement weapons/sensors/AI

## 9. F-16 profile responsibilities

`MavF16FlightDynamicsProfile` remains the authoritative physical preset for the F-16 reference path.

It owns/furnishes:

- reference geometry
- mass / CG / inertia
- published Morelli validity envelope
- physical surface limits
- gravity/damping ownership flags
- eventually propulsion-profile data only when the NASA reference source is frozen

It must not contain:

- mouse sensitivity
- camera tuning
- direct torque values
- velocity-turn assist
- fake damping
- weapons/sensors

## 10. F-16 aerodynamic path

The existing Morelli path remains coefficient-driven:

```text
state + physical surface deflection
    -> CX/CY/CZ/Cl/Cm/Cn
    -> qbar*S / qbar*S*b / qbar*S*c
    -> force / moment
```

The aerodynamic model owns aerodynamic damping through the Morelli rate terms. Therefore legacy fixed angular-rate damping must not remain active after F-16 live-FDM ownership is enabled.

Do not add a second generic angular damping term in the new core unless it represents a separately justified physical effect.

## 11. Control development phases

### Phase C0 — direct physical-surface test law

Purpose: prove that the Morelli/6DoF path is controllable without legacy torque.

- normalized pitch -> bounded elevator demand
- normalized roll -> bounded aileron demand
- normalized yaw -> bounded rudder demand
- no direct Rigidbody torque
- no fake attitude recovery
- no velocity-vector alignment

This is a test control law, not the final mouse instructor.

### Phase C1 — stability/rate augmentation

Add a Maverick F-16 control law that can command rates/G while still driving physical surfaces only.

- pitch-rate / normal-acceleration demand
- roll-rate demand
- yaw/sideslip coordination
- AoA/G protections
- actuator saturation awareness

Any value not supported by the selected reference must be clearly documented as tuning, not source-accurate F-16 data.

### Phase C2 — War-Thunder-like instructor

Arcade accessibility belongs here, above physics:

```text
mouse aim -> target flight direction / bank / G -> control law -> surfaces
```

Do not reintroduce `velocityTurnAssist`, direct torque, or fake aerodynamic forces into the physics layer.

## 12. Live ownership switch

Add an explicit runtime ownership controller only after the new path has:

- valid F-16 profile
- valid aerodynamic model
- valid actuator
- valid control law
- valid propulsion model
- Rigidbody present

Suggested state machine:

```text
LEGACY
  -> PREPARED
  -> NEW_FDM_ARMED
  -> NEW_FDM_LIVE
  -> FALLBACK_LEGACY
```

The transition to `NEW_FDM_LIVE` must be atomic for ownership:

- disable legacy aerodynamic force owner
- disable legacy direct torque/control-force owner
- disable legacy propulsion force owner
- zero Unity linear/angular damping if profile says so
- enable new 6DoF load application

If readiness fails, stay/fall back to LEGACY. Never remain in a hybrid state.

For the F-16 branch, the first implementation should keep automatic live takeover OFF by default until manually armed/tested.

## 13. Determinism and validation

### Unit/regression checks

Keep and extend existing coefficient regression vectors.

Add tests/checks for:

- atmosphere samples
- body-axis <-> Unity-axis conversions
- dimensionalization of force/moment coefficients
- mass/inertia mapping and principal-axis reconstruction
- control-surface clamping
- propulsion state transitions
- load summation
- ownership state machine

### Golden trace procedure

Before a physical ownership migration, record a defined test trace. After changes, compare:

- TAS
- alpha/beta
- p/q/r
- force/moment
- surface deflections

For NASA/AeroBench validation, compare the new FDM in explicitly matched initial conditions and conventions. AeroBench is a behavioral cross-check, not the source of implementation constants.

## 14. First Claude implementation task

Claude should implement **infrastructure only**, not invent missing F-16 data.

Required deliverables:

1. `MavPilotCommand`
2. `MavFlightControlLawBase`
3. a simple `MavDirectSurfaceControlLaw` suitable for isolated testing
4. `MavPropulsionModelBase`
5. `MavPropulsiveLoads`
6. `MavFlightDynamicsLoadSet`
7. extend `MavSixDoFBody` to aggregate aero + propulsion loads once
8. `MavFlightDynamicsTelemetry` with rate-limited optional logging
9. compile-safe wiring hooks in the existing F-16 runtime auto-setup
10. validation code for load summation / no double-application

The propulsion model used in the first task may be a zero-thrust/null implementation if authoritative F-16 propulsion data is not yet frozen. It must not invent an “accurate” engine curve.

## 15. Files Claude may modify in the first task

Preferred scope:

- `Assets/MaverickFresh/Scripts/FlightDynamics/Core/**`
- `Assets/MaverickFresh/Scripts/FlightDynamics/F16/**`
- `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/**`

Allowed existing-file edit:

- `MavF16SelectionAutoSetup.cs` only as needed to wire new components, while keeping live takeover OFF by default.

Do not modify legacy flight/combat/scene files in this task.

## 16. Acceptance criteria for Claude task

- Unity C# code is compile-safe for Unity 6000.3.16f1.
- No scene/prefab/model/material changes.
- No weapon/sensor/AI changes.
- No direct Rigidbody force/torque calls outside the new six-DoF load-application boundary for the new FDM path.
- F-16 selection can prepare the entire new stack automatically.
- New live-FDM takeover remains OFF by default.
- Legacy flight remains untouched while new FDM is inactive.
- New load aggregation cannot apply aerodynamic/propulsive loads twice.
- Missing propulsion data is explicit rather than guessed.
- Validation/logging makes ownership and load sources inspectable.

## 17. Review rule

Claude should finish on a dedicated worker branch and open a PR back into `feature/f16-flight-dynamics-core`. Do not merge directly to `main`. The PR description must list every changed file, ownership changes, assumptions, and anything still provisional.
