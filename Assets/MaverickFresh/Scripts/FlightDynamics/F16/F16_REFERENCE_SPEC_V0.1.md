# Maverick F-16 Reference Spec v0.1

Status: **FROZEN BASELINE FOR SUBSONIC REFERENCE DEVELOPMENT**

This document defines the reference configuration used to develop and validate Maverick's isolated F-16 flight-dynamics core.

## 1. Source hierarchy

### Authoritative technical source

NASA / Morelli F-16 publications are the implementation authority.

Primary aerodynamic reference:

- Eugene A. Morelli, **Global Nonlinear Parametric Modeling with Application to F-16 Aerodynamics**, ACC 1998.
- NASA NTRS: `20040110310` / `19990008037`.

Supporting NASA nonlinear-simulation reference:

- Frederico R. Garza and Eugene A. Morelli, **A Collection of Nonlinear Aircraft Simulations in MATLAB**, NASA/TM-2003-212145, NTRS `20030013626`.
- Later Morelli NASA publications may be used only to cross-check geometry, mass properties, and simulation conventions when they do not conflict with the primary aerodynamic source.

### External validation only

- AeroBenchVVPython.

Rules:

- No AeroBench source code is incorporated into Maverick.
- No AeroBench aerodynamic table or coefficient value overrides the NASA Morelli source.
- AeroBench is used to compare state/coefficient behavior under matched test conditions.
- JSBSim is outside the reference chain for this branch.

## 2. Reference aircraft configuration

Reference type:

- clean F-16 nonlinear simulation configuration
- landing gear retracted
- no external stores
- out of ground effect
- speed brake fixed at zero for the initial reference model
- flap effects outside the compact Morelli polynomial are not added in v0.1

The compact Morelli aerodynamic model is treated as a **subsonic reference model**, not as a full-envelope F-16 model.

Published wind-tunnel basis:

- `alpha`: -10 deg to +45 deg
- `beta`: -30 deg to +30 deg
- subsonic source data; Maverick currently flags Mach >= 0.6 as outside the selected reference range

## 3. Coordinate convention

Aerodynamic body axes:

- +X forward
- +Y right
- +Z down
- p roll rate about +X
- q pitch rate about +Y
- r yaw rate about +Z

Unity conversion occurs only at the flight-dynamics boundary.

Unity local axes:

- +X right
- +Y up
- +Z forward

## 4. Unit convention

Maverick internal flight-dynamics units are SI:

- distance: m
- velocity: m/s
- time: s
- mass: kg
- force: N
- moment: N*m
- angular rate: rad/s
- pressure: Pa
- aerodynamic coefficients: dimensionless

Source-model angular inputs are converted as required before coefficient evaluation.

## 5. Reference geometry

Frozen values:

- wing area `S = 300 ft^2 = 27.870912 m^2`
- wingspan `b = 30 ft = 9.144 m`
- mean aerodynamic chord `cbar = 11.32 ft = 3.450336 m`

Longitudinal stations:

- aerodynamic reference `xcgRef = 0.35 cbar`
- nominal reference-model CG `xcg = 0.25 cbar`

The numerical `0.25 cbar` CG is used in aerodynamic coefficient correction. The Unity `Rigidbody.centerOfMass` vector is not inferred from this number until the prefab/model aerodynamic datum is explicitly mapped.

## 6. Reference mass and inertia

Frozen nominal baseline for this branch:

- mass `m = 637.16 slug ~= 9298.65 kg`
- `Ix = 9,496 slug-ft^2`
- `Iy = 55,814 slug-ft^2`
- `Iz = 63,100 slug-ft^2`
- `Ixz = 982 slug-ft^2`

The body-axis inertia tensor includes the `Ixz` product of inertia and is transformed into Unity principal inertia axes before being applied to the Rigidbody.

Note: other NASA F-16 studies use different nominal masses, including approximately 647.2 slug. Those are treated as different simulation configurations and must not be silently mixed into this v0.1 baseline.

## 7. Aerodynamic model

Required outputs:

- force coefficients: `CX`, `CY`, `CZ`
- moment coefficients: `Cl`, `Cm`, `Cn`

Required independent variables include:

- alpha
- beta
- elevator deflection
- aileron deflection
- rudder deflection
- nondimensional p/q/r rates
- longitudinal CG correction

Rate normalization:

- `pHat = p*b/(2V)`
- `qHat = q*cbar/(2V)`
- `rHat = r*b/(2V)`

The polynomial constants and equation form are frozen by `F16_MORELLI_COEFFICIENT_AUDIT_V0.1.md`.

## 8. Control-surface reference limits

Initial published aerodynamic-input limits:

- elevator: -25 deg to +25 deg
- aileron: -21.5 deg to +21.5 deg
- rudder: -30 deg to +30 deg

The current actuator layer enforces these deflection bounds.

Actuator **rate limits are not yet reference-frozen**. They must not be guessed or copied from AeroBench. Until an accepted NASA/Morelli source is selected, `0 deg/s` in the current actuator implementation means no rate limiting.

## 9. Propulsion model requirements

The reference propulsion architecture will follow the NASA nonlinear-simulation description:

`throttle command -> throttle gearing -> commanded engine power -> first-order power dynamics -> thrust(altitude, Mach, power)`

The engine is a separate physical force owner and must not directly create aerodynamic moments unless a documented thrust-line / gyroscopic effect is intentionally modeled.

Reference-accurate thrust tables are **not frozen yet**.

Until the permitted NASA data and exact interpolation convention are fully audited, the propulsion implementation must be labeled experimental rather than reference-accurate.

## 10. Validation hierarchy

### V0 — coefficient sanity

At fixed alpha/beta/control/rate states:

- compare `CX/CY/CZ/Cl/Cm/Cn` against independently evaluated Morelli equations
- reject sign/unit/decimal regressions

### V1 — rigid-body sanity

Check:

- zero/control symmetry cases
- expected static directional signs
- inertia tensor reconstruction
- force and moment dimensionalization
- gravity and axis conversion

### V2 — deterministic maneuver tests

Initial targets:

- straight-and-level sanity condition
- pitch input step/doublet
- lateral-directional doublet
- free-decay angular-rate checks

### V3 — AeroBench cross-validation

AeroBench is used as an external oracle under documented matched conditions.

Exact trajectory identity is not required when configuration/model-layer differences are known. Every comparison must record:

- initial state
- CG/reference CG
- atmosphere convention
- control input
- engine state
- numerical integrator / timestep
- Morelli-only vs surrounding damping layers

## 11. Initial validation conditions

### SANITY-001 — straight flight cross-check

AeroBench provides a useful published example configuration approximately equivalent to:

- true airspeed: 550 ft/s (~167.64 m/s)
- altitude: 3600 ft (~1097.28 m)
- alpha: 1.8 deg
- beta: 0 deg

This is a behavioral cross-check condition, not an aerodynamic data source.

### NASA-MANEUVER-001 — moderate-alpha nonlinear cross-check

The original Morelli work demonstrates a moderate-angle-of-attack maneuver/doublet case where the compact nonlinear model reproduces the source wind-tunnel-based nonlinear behavior to within roughly 10% for the reported comparison.

Before implementing an automated golden trace for this case, the exact initial state and command waveform must be transcribed and independently verified from the NASA publication.

## 12. Ownership rules

One physical effect, one owner:

- atmosphere -> `MavAtmosphereModel`
- aircraft aerodynamic coefficients -> `MavF16AeroModel`
- coefficient dimensionalization -> `MavFlightDynamicsMath`
- aerodynamic force/moment application -> `MavSixDoFBody`
- actual surface state -> `MavF16ControlActuator`
- mass/inertia -> `MavF16MassReference`
- propulsion -> future dedicated propulsion component

No legacy Maverick fake damping, velocity alignment, thrust boost, or direct control torque is allowed inside the isolated reference model during validation.

## 13. Freeze policy

Changes to frozen values require:

1. exact source identifier,
2. reason the existing baseline is wrong or belongs to a different configuration,
3. explicit before/after values,
4. regression impact,
5. update to the coefficient/reference audit document where applicable.

This v0.1 freeze exists so the F-16 model is built by verification rather than by feel-based tuning.
