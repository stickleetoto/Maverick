# Maverick Flight Dynamics Core

This folder is an isolated replacement flight-dynamics path under development.
It must not be enabled on the production `Mav_Player` while the legacy flight stack is still applying aerodynamic forces and control torques.

## Source policy

- NASA Morelli F-16 publications: technical / mathematical reference.
- AeroBenchVVPython: external validation only.
- No AeroBench source code is incorporated into Maverick.
- JSBSim F-16 data/model is not used for this implementation.

Frozen reference documents:

- `F16/F16_REFERENCE_SPEC_V0.1.md`
- `F16/F16_MORELLI_COEFFICIENT_AUDIT_V0.1.md`
- `F16/F16_REGRESSION_VECTORS_V0.1.md`

## Coordinate convention

Aerodynamic code uses conventional aircraft body axes:

- +X: forward
- +Y: right
- +Z: down
- p: roll rate about +X
- q: pitch rate about +Y
- r: yaw rate about +Z

Unity local axes are converted only at the flight-dynamics boundary:

- Unity +X: right
- Unity +Y: up
- Unity +Z: forward

The two bases differ in handedness, so forces and moments do **not** convert the same way. See
[Axis conventions: true vectors vs axial vectors](#axis-conventions-true-vectors-vs-axial-vectors).

## Units

The new core uses SI units only:

- distance: m
- time: s
- velocity: m/s
- mass: kg
- force: N
- moment: N*m
- angular rate: rad/s
- pressure: Pa
- aerodynamic coefficients: dimensionless

Aircraft-specific aerodynamic polynomial inputs are converted to the units used by the source model before evaluation. The Morelli F-16 polynomial uses radians for alpha, beta, and control-surface deflections.

## Implemented foundation

- `MavAtmosphereModel`: ISA-style temperature, pressure, density, and speed of sound.
- `MavFlightState`: TAS, Mach, dynamic pressure, alpha, beta, and p/q/r state.
- `MavMassProperties`: explicit mass, CG, principal inertia tensor, and inertia-axis rotation.
- `MavAerodynamicModelBase`: aircraft model returns coefficients only.
- `MavFlightDynamicsMath`: axis conversion and coefficient dimensionalization.
- `MavSixDoFBody`: converts `CX/CY/CZ/Cl/Cm/Cn` into physical Rigidbody force/moment.
- `MavF16MorelliPolynomial`: compact nonlinear Morelli F-16 aerodynamic coefficient model.
- `MavF16AeroModel`: Unity bridge that computes nondimensional rates and feeds the polynomial model.
- `MavF16MassReference`: published F-16 mass and full body-axis inertia reference transformed into Unity principal inertia.
- `MavF16ReferenceConfigurator`: safe reference-value helper; it does not enable the simulation by itself.
- `MavF16ControlActuator`: bounded physical control-surface state owner that publishes deflections to `MavSixDoFBody` without applying direct Rigidbody torque.
- `MavF16ReferenceValidation`: deterministic coefficient, geometry, inertia, and axis-mapping regression checks.
- Editor runner: `Maverick > Flight Dynamics > Run F-16 Reference Validation`.

### Phase 1 live-FDM infrastructure

- `MavPilotCommand`: aircraft-independent normalized pilot intent (pitch/roll/yaw -1..1, throttle 0..1). No torque, no forces, no deflections.
- `MavFlightControlLawBase`: `(state, atmosphere, pilotCommand, profile) -> MavControlInput`. Drives the actuator at execution order -300; no Rigidbody access.
- `MavDirectSurfaceControlLaw`: Phase C0 test law. A linear normalized-intent-to-bounded-deflection mapping and nothing else.
- `MavControlSurfaceActuatorBase`: aircraft-independent actuator contract, so the core never references an aircraft-specific actuator type.
- `MavPropulsionModelBase`: `(state, atmosphere, throttle, dt) -> MavPropulsiveLoads`, plus an explicit `HasAuthoritativeData` honesty flag.
- `MavNullPropulsionModel`: zero-thrust placeholder with power-state plumbing. Used for the F-16 because no authoritative F-16 propulsion data is frozen.
- `MavPropulsiveLoads`: dimensional propulsive force/moment in aerodynamic body axes.
- `MavFlightDynamicsLoadSet`: single-step load accumulator with per-source contribution counters, an applied flag, and a finiteness check.
- `MavFlightDynamicsTelemetry`: one pushed sample per physics step, optional rate-limited console logging (off by default), and optional in-memory CSV capture (off by default, written to disk only on an explicit call).
- Editor runners: `Run Phase 1 Live-FDM Validation` and `Run All Flight Dynamics Validation`.

### Runtime pipeline

```text
MavPilotCommand                     normalized intent, no physical units
  -> MavFlightControlLawBase        (order -300) command shaping
  -> MavControlInput                physical deflection degrees + throttle
  -> MavControlSurfaceActuatorBase  (order -200) bounded / rate-limited actual surfaces
  -> MavAerodynamicModelBase + MavPropulsionModelBase
  -> MavFlightDynamicsLoadSet       summed exactly once
  -> MavSixDoFBody                  (order -100) applied exactly once
  -> Unity Rigidbody
```

The state a control law reads is the state `MavSixDoFBody` sampled during the previous physics
step, because the law runs ahead of the body. That is one fixed step of sensor latency. It is
deterministic and intentional, not an artifact of component ordering.

To fly the C0 test law, set `MavDirectSurfaceControlLaw.pilotCommand`. Do not write
`MavF16ControlActuator.command` directly while the law is driving the actuator; the law owns that
field and overwrites it every physics step.

`MavSixDoFBody.simulationEnabled` defaults to `false` intentionally.

## F-16 Morelli aerodynamic reference envelope

The current F-16 aerodynamic implementation follows the compact nonlinear polynomial model published by Eugene A. Morelli (NASA NTRS 20040110310 / ACC 1998).

Reference geometry:

- wing area: 300 ft^2 = 27.870912 m^2
- wingspan: 30 ft = 9.144 m
- mean aerodynamic chord: 11.32 ft = 3.450336 m

Published independent-variable ranges used by the compact model:

- alpha: -10 to +45 deg
- beta: -30 to +30 deg
- elevator: -25 to +25 deg
- aileron: -21.5 to +21.5 deg
- rudder: -30 to +30 deg

The source wind-tunnel database is subsonic (Mach < 0.6), out of ground effect, gear retracted, and without external stores. `MavF16AeroModel` reports when the simulation is outside that published Mach range and can clamp the polynomial inputs to the published angular/control envelope.

Leading-edge-flap demand is intentionally not consumed by this compact polynomial model.

## F-16 nominal mass / inertia reference

The v0.1 reference preset uses the nominal F-16 nonlinear-simulation values frozen in `F16_REFERENCE_SPEC_V0.1.md`:

- mass: 637.16 slug (~9298.65 kg)
- Ix: 9,496 slug-ft^2
- Iy: 55,814 slug-ft^2
- Iz: 63,100 slug-ft^2
- Ixz: 982 slug-ft^2
- nominal aerodynamic CG: 0.25 cbar
- aerodynamic reference station: 0.35 cbar

`MavF16MassReference` transforms the conventional aircraft body-axis tensor into Unity local axes and diagonalizes the coupled Y/Z block so `Rigidbody.inertiaTensor` and `Rigidbody.inertiaTensorRotation` represent the same tensor.

The Rigidbody local center-of-mass offset is not guessed. `MavF16ReferenceConfigurator.centerOfMassLocalM` remains explicit because a Unity model/prefab origin is an asset convention and is not guaranteed to coincide with the aerodynamic reference point.

Other NASA studies use different F-16 nominal masses. Those are treated as different configurations and are not silently mixed into the v0.1 baseline.

## Coefficient audit and regression status

The compact Morelli polynomial constants and equation structure have been manually audited against the 1998 publication and frozen for v0.1.

Important policy result:

- NASA Morelli values remain authoritative.
- AeroBench differences do not overwrite Maverick coefficients.
- Known cross-check discrepancies are documented in `F16_MORELLI_COEFFICIENT_AUDIT_V0.1.md`.

Four deterministic coefficient regression vectors are frozen in `F16_REGRESSION_VECTORS_V0.1.md` and executable through `MavF16ReferenceValidation`.

The validator also checks the frozen geometry, mass conversion, Unity principal inertia values, inertia trace preservation, and axis-conversion round trips. These checks protect the reference implementation from silent sign, unit, decimal, and transform regressions before propulsion and trim work begins.

## Control-surface ownership

`MavF16ControlActuator` owns actual elevator/aileron/rudder deflection for the new path.

- Morelli aerodynamic deflection limits are enforced.
- No direct torque is applied by the actuator.
- Optional rate limits exist structurally but default to disabled (`0 deg/s` means unlimited) because no actuator-rate numbers are being imported from AeroBench or guessed.
- The actuator runs before `MavSixDoFBody` so the aerodynamic model consumes the current surface state in the same physics step.

## Current force ownership

The new path is designed around one owner per physical effect:

- normalized pilot intent -> `MavPilotCommand`
- command shaping / control allocation -> `MavFlightControlLawBase` (`MavDirectSurfaceControlLaw` for C0)
- physical surface state -> `MavControlSurfaceActuatorBase` (`MavF16ControlActuator` for the F-16)
- aircraft aerodynamic coefficients -> aircraft-specific aero model
- coefficient dimensionalization -> `MavFlightDynamicsMath`
- propulsive force/moment -> `MavPropulsionModelBase` (`MavNullPropulsionModel`, zero thrust, for the F-16)
- load summation -> `MavFlightDynamicsLoadSet`
- final Rigidbody force/moment -> `MavSixDoFBody`, and only `MavSixDoFBody`
- atmosphere -> `MavAtmosphereModel`
- mass / inertia preset -> `MavF16MassReference`

`MavSixDoFBody` is the single load-application boundary for the new path. Nothing else in the new
path may call `Rigidbody.AddForce` or `Rigidbody.AddTorque`.

Duplicate application is structurally prevented, not merely avoided by convention:

- `MavFlightDynamicsLoadSet` counts contributions per source and refuses a second one per step.
- The set refuses to be marked applied twice, and refuses application entirely while a duplicate
  contribution is recorded.
- `MavSixDoFBody` refuses a second application at the same `Time.fixedTime` and logs an error.
- A non-finite (NaN / Infinity) total is refused before it can reach the Rigidbody.

Every rejection increments an inspector-visible counter (`debugRejectedDuplicateApplications`,
`debugRejectedNonFiniteApplications`), so an ownership bug is observable rather than silent.

The legacy Maverick force/torque stack remains untouched on this branch.
`MavSixDoFBody.simulationEnabled` stays `false` by default, and the F-16 auto-setup explicitly holds
it off, so the two paths never own the same physical effect at once.

## Propulsion status

No authoritative F-16 propulsion data is frozen in this repository. The F-16 therefore runs
`MavNullPropulsionModel`, which:

- implements the full propulsion contract so the pipeline can be wired and inspected,
- returns zero force and zero moment at every throttle setting,
- reports `HasAuthoritativeData == false`, which telemetry and the readiness log both surface.

No thrust map, installed-thrust figure, or engine spool time constant has been invented. The
optional power-state lag defaults to 0 s (instant) precisely because no sourced value exists.

## Axis conventions: true vectors vs axial vectors

The Unity-local basis (X right, Y up, Z forward) and the aerodynamic body basis (X forward, Y right,
Z down) differ in handedness. The component change of basis has determinant -1, so the two kinds of
quantity do not convert the same way:

- A **true vector** (force, velocity, position) transforms with the basis change alone.
  Use `UnityLocalVectorToAeroBody` / `AeroBodyVectorToUnityLocal`.
- An **axial vector** or pseudo-vector (moment, angular rate) transforms with the basis change
  multiplied by its determinant, so it carries an extra sign on every axis.
  Use `UnityLocalAngularRateToAeroBody` / `AeroBodyMomentToUnityLocal`.

Physically, in Unity a positive rotation about `+X` pitches the nose **down**, about `+Y` yaws the
nose **right**, and about `+Z` rolls **left**. In conventional aircraft body axes a positive `L`
rolls **right**, a positive `M` pitches the nose **up**, and a positive `N` yaws the nose **right**.
So the roll and pitch channels reverse sign across the boundary and the yaw channel does not.

Outbound moment, `AeroBodyMomentToUnityLocal`:

| aerodynamic moment | Unity local torque | Unity physical effect |
| --- | --- | --- |
| `L = +1` (roll right) | `-Z` | roll right |
| `M = +1` (nose up) | `-X` | nose up |
| `N = +1` (nose right) | `+Y` | nose right |

Inbound rate, `UnityLocalAngularRateToAeroBody`:

| Unity local angular velocity | body rate | aircraft motion |
| --- | --- | --- |
| `-Z` | `p > 0` | rolling right |
| `-X` | `q > 0` | pitching nose up |
| `+Y` | `r > 0` | yawing nose right |

Do not collapse the axial-vector helpers into the true-vector helpers. A round-trip test cannot
catch that mistake: applying the same wrong sign on the way out and on the way back in cancels, so
the loop still closes while every static and control-derived moment is inverted. That is why
validation asserts physical directions:

- `[P7]` asserts the basis determinant, the true-vector force mapping, all six axial-vector
  directions above, and that the moment mapping is the negated true-vector mapping. The round-trip
  check is kept, but labelled necessary rather than sufficient.
- `[P8]` drives the whole boundary end to end: Unity angular velocity to body rates, through the
  frozen Morelli damping derivatives, back to Unity torque. It asserts that each imposed rotation
  produces an **opposing** Unity torque, that each pilot command reaches the Rigidbody in the
  demanded direction, and that positive alpha with neutral controls produces a nose-down restoring
  torque.

The coefficient-space regression vectors are unaffected by any of this, and were re-run unchanged
after the conversion fix.

## Next development steps

1. Run `Maverick > Flight Dynamics > Run All Flight Dynamics Validation` and keep it green before further flight-dynamics changes.
2. Record a golden trace for the corrected axis boundary before relying on C0 flight-test results.
3. Audit the NASA F-16 propulsion description, throttle gearing, power-state dynamics, thrust-map availability, and interpolation convention.
4. Freeze the propulsion source/data boundary, then replace `MavNullPropulsionModel` with a sourced F-16 propulsion model.
5. Build a deterministic trim solver for straight-and-level subsonic flight.
6. Build deterministic pitch/roll/yaw step and doublet test cases driven through `MavPilotCommand`.
7. Compare trajectories against AeroBenchVVPython as an external oracle only.
8. Correct implementation/sign/unit mistakes until the external traces agree within defined tolerances.
9. Add the C1 rate/G augmentation law above the C0 test law.
10. Only after validation, begin one-for-one ownership migration from the legacy Maverick flight stack.
