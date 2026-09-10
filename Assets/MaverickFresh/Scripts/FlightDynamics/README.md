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
- `MavNullPropulsionModel`: zero-thrust placeholder with power-state plumbing, for any aircraft with no frozen propulsion data at all. The F-16 has since moved to `MavF16EnginePowerModel` (sourced power dynamics, dimensional thrust still unavailable) - see **Propulsion status**.
- `MavPropulsiveLoads`: dimensional propulsive force/moment in aerodynamic body axes.
- `MavFlightDynamicsLoadSet`: single-step load accumulator with per-source contribution counters, an applied flag, and a finiteness check.
- `MavFlightDynamicsTelemetry`: one pushed sample per physics step, optional rate-limited console logging (off by default), and optional in-memory CSV capture (off by default, written to disk only on an explicit call).
- Editor runners: `Run Phase 1 Live-FDM Validation` and `Run All Flight Dynamics Validation`.

### Phase 2C/2D trim, control law and readiness

Design document: `Docs/FlightDynamics/F16_TRIM_AND_CONTROL_PHASE2_V0.1.md`.

- `MavTrimCondition` / `MavTrimPlant` / `MavTrimResult` / `MavTrimResidual` / `MavTrimSolverSettings`:
  aircraft-independent trim problem definition. A plant is frozen numbers plus two pure functions, so
  the solver holds no reference to a Rigidbody, Transform or component.
- `MavDampedNewtonSolver`: deterministic bounded damped-Newton iteration. Central differences,
  Gaussian elimination with partial pivoting, strict-decrease line search, hard box bounds. No
  randomness and no wall-clock input.
- `MavSteadyFlightTrimSolver`: steady symmetric flight trim. `PoweredSteadyFlight` imposes the
  flight-path angle and solves for the required thrust; `UnpoweredGlide` pins thrust at zero and
  solves for the flight-path angle.
- `MavF16TrimReference`: F-16 plant built from the frozen Morelli geometry, NASA nominal mass, and the
  Garza/Morelli engine power state. Powered straight-and-level reports
  `ConvergedButThrustUnavailable`, because there is no frozen thrust deck; unpowered glide converges.
- `MavF16ControlLawV01`: Phase C1 rate / load-factor augmentation. **Maverick tuning throughout, and
  explicitly not the real F-16 FLCS.**
- `MavControlLawProtections`: pure limiter mathematics shared by control laws — soft saturation,
  compact-support smooth min/max, load-factor and angle-of-attack rate ceilings, roll authority fade,
  yaw-rate washout, anti-windup integration, dynamic-pressure gain scheduling.
- `MavPilotCommandSourceBase` / `MavManualPilotCommandSource`: the socket a real input path plugs
  into. A manual/test source reports itself non-operational unless an operator says otherwise.
- `MavFlightDynamicsReadiness`: splits `STRUCTURALLY_PREPARED` from `OPERATIONALLY_LIVE_READY`, and
  verifies pipeline **identity** - the control law must read *this* body, drive *this* actuator, and
  read the *same* command source the body inspects. Presence is not identity.
- `MavFlightState.specificForceAeroBodyG` / `LoadFactorNz`: measured accelerometer channel published
  by `MavSixDoFBody`, so a control law can close a load-factor loop without computing forces itself.
- `MavFlightDynamicsPhase2Validation` and `MavFlightDynamicsOwnershipScan`.
- Editor runners: `Run Phase 2 Trim and Control Validation` and `Report F-16 Trim Survey`.

### Runtime pipeline

```text
MavPilotCommandSourceBase           optional command producer (test rig today)
  -> MavPilotCommand                normalized intent, no physical units
  -> MavFlightControlLawBase        (order -300) command shaping + protections
  -> MavControlInput                physical deflection degrees + throttle
  -> MavControlSurfaceActuatorBase  (order -200) bounded / rate-limited actual surfaces
  -> MavAerodynamicModelBase + MavPropulsionModelBase
  -> MavFlightDynamicsLoadSet       summed exactly once
  -> MavSixDoFBody                  (order -100) applied exactly once
  -> Unity Rigidbody
                                    and back out as MavFlightState.specificForceAeroBodyG
```

The state a control law reads is the state `MavSixDoFBody` sampled during the previous physics
step, because the law runs ahead of the body. That is one fixed step of sensor latency. It is
deterministic and intentional, not an artifact of component ordering.

To fly the C0 test law, set `MavDirectSurfaceControlLaw.pilotCommand`. Do not write
`MavF16ControlActuator.command` directly while the law is driving the actuator; the law owns that
field and overwrites it every physics step.

`MavSixDoFBody.simulationEnabled` defaults to `false` intentionally, and since Phase 2 it is no
longer sufficient on its own: `requireOperationalReadinessForLoadApplication` defaults to `true`, so
loads are applied only at `OPERATIONALLY_LIVE_READY`. Arming a stack that is merely wired together
refuses to fly, increments `debugRejectedNotLiveReadyApplications`, and logs the reason once.

## Readiness: prepared is not the same as live-ready

```text
NOT_PREPARED
STRUCTURALLY_PREPARED     are the parts present and wired?
OPERATIONALLY_LIVE_READY  would handing this the aircraft actually be correct?
```

Structural preparation is the Phase 1 rule, unchanged, and `MavSixDoFBody.EvaluateReadiness(...)` still
answers exactly that question for existing callers. Operational live-readiness additionally requires
matching aerodynamic geometry, a control law that is enabled and driving *this* actuator, an actuator
enabled and bound to *this* body, propulsion output that is authoritative or explicitly accepted, a
command source that reports itself operational, and no legacy physics owner enabled on the same
Rigidbody.

Legacy ownership is detected by component **type name** against a serialized deny-list, so the new
core takes no compile dependency on the stack it is replacing and no legacy file has to be edited.
While load application is armed that scan runs on **every** physics step — caching is diagnostics
only, because a stale "no conflict" verdict is exactly the failure the gate exists to prevent. Call
`NotifyOwnershipChanged()` to invalidate the verdict and the readiness judgement immediately.

The command-path criterion has two halves that must not be collapsed:

- `MavPilotCommandSourceBase.IsOperationalCommandSource` — a **declaration** about the kind of path.
- `MavPilotCommandSourceBase.HasCommandSignal` — **live state**: is a command arriving right now?

Live-readiness requires both, corroborated by the control law's actual last poll, and requires that
the body and the control law are talking about the **same source object**. On signal loss the
law applies the source's declared `MavCommandSignalLossPolicy` (`NeutralCommand` centres the axes and
holds the last throttle; `HoldLastCommand` holds everything). It never falls back to the inspector
field — that is a bench affordance for when no source is wired, or when a non-operational source has
nothing to say. Dropouts are logged once and counted in `debugCommandSignalLossEvents`.

The current F-16 configuration is honestly reported as `STRUCTURALLY_PREPARED`, not live-ready: it has
no frozen thrust deck and no operational command source.

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

- normalized pilot intent -> `MavPilotCommandSourceBase` -> `MavPilotCommand`
- command shaping / protections / control allocation -> `MavFlightControlLawBase`
  (`MavF16ControlLawV01` for the F-16, `MavDirectSurfaceControlLaw` for isolated C0 testing;
  exactly one law is ever left enabled, because two laws at execution order -300 would make the
  actuator command depend on component order)
- measured specific force / load factor -> published by `MavSixDoFBody` into `MavFlightState`
- physical surface state -> `MavControlSurfaceActuatorBase` (`MavF16ControlActuator` for the F-16)
- aircraft aerodynamic coefficients -> aircraft-specific aero model
- coefficient dimensionalization -> `MavFlightDynamicsMath`
- propulsive force/moment -> `MavPropulsionModelBase` (`MavF16EnginePowerModel` for the F-16: sourced Garza/Morelli power-state dynamics, dimensional thrust unavailable and therefore zero)
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

The F-16 runs `MavF16EnginePowerModel`, whose throttle gearing and power-state dynamics are sourced
from NASA/TM-2003-212145 (Garza/Morelli) and frozen in `F16/F16_PROPULSION_REFERENCE_V0.1.md`.

The **thrust deck is not frozen**. The altitude/Mach idle/military/maximum tables are unavailable to
this repository, so the model:

- advances and exposes the sourced commanded/actual power state,
- returns exactly `0 N` force and `0 N*m` moment at every throttle setting,
- reports `HasAuthoritativeData == false`, which telemetry, the readiness report and the trim solver
  all surface.

`MavNullPropulsionModel` remains available as the zero-thrust placeholder for any aircraft with no
frozen propulsion data at all.

No thrust map or installed-thrust figure has been invented. The consequence is visible rather than
worked around: a powered F-16 trim is not achievable, and the trim solver says so — see
**Trim status** below.

**Source of truth for F-16 propulsion.** The F-16 may use sourced engine power-state dynamics, but
without an authoritative dimensional thrust deck the runtime dimensional thrust remains
unavailable/zero, and must not be represented anywhere as validated F-16 thrust. Dimensional thrust
obtained from a non-authoritative deck is labelled as such end to end - in `HasAuthoritativeData`, in
readiness, in telemetry and in the trim result - and is never accepted for live flight by default.

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

## Trim status

`MavF16TrimReference` solves steady symmetric flight from the frozen data. Because F-16 thrust is
exactly zero on this branch:

- **powered straight-and-level reports `ConvergedButThrustUnavailable`**, with `converged == false`,
  `throttle01 == NaN`, and the required thrust quantified against 0 N available. No throttle is
  invented and no thrust curve is fabricated;
- **unpowered glide converges**, because it is the physically well-posed problem at zero thrust, and
  exercises the solver against real reference aerodynamics.

Level and glide solutions agree on alpha and elevator to three decimals, and imposing the solved glide
angle back as a powered condition converges needing ~0 N — a cross-check between the two modes.

An L/D near 13 from the compact Morelli `CX` fit is optimistic for a real airframe. These numbers are
a self-consistency result for *this model*, not a claim about the aircraft.

## Next development steps

1. Run `Maverick > Flight Dynamics > Run All Flight Dynamics Validation` and keep it green before further flight-dynamics changes.
2. Record a golden trace for the corrected axis boundary before relying on C0 flight-test results.
3. Compare the trim points and step/doublet responses against AeroBenchVVPython as an external oracle only, in explicitly matched initial conditions.
4. Freeze the altitude/Mach F-16 thrust deck from an approved source, then re-run the powered trim and expect `Converged`.
5. Build deterministic pitch/roll/yaw step and doublet test cases driven through `MavPilotCommand`.
6. Tune the Maverick F-16 control law v0.1 gains against real trajectories; they are currently reasoned and bounds-checked, but unflown.
7. Add an attitude reference so the load-factor command stops relying on the level-flight relation, and extend the trim solver to steady banked turns.
8. Only after that, build the War-Thunder-style instructor **above** the control law, and begin one-for-one ownership migration from the legacy Maverick flight stack.
