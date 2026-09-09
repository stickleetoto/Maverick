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

## Coefficient audit status

The compact Morelli polynomial constants and equation structure have been manually audited against the 1998 publication and frozen for v0.1.

Important policy result:

- NASA Morelli values remain authoritative.
- AeroBench differences do not overwrite Maverick coefficients.
- Known cross-check discrepancies are documented in `F16_MORELLI_COEFFICIENT_AUDIT_V0.1.md`.

## Control-surface ownership

`MavF16ControlActuator` owns actual elevator/aileron/rudder deflection for the new path.

- Morelli aerodynamic deflection limits are enforced.
- No direct torque is applied by the actuator.
- Optional rate limits exist structurally but default to disabled (`0 deg/s` means unlimited) because no actuator-rate numbers are being imported from AeroBench or guessed.
- The actuator runs before `MavSixDoFBody` so the aerodynamic model consumes the current surface state in the same physics step.

## Current force ownership

The new path is designed around one owner per physical effect:

- aircraft aerodynamic coefficients -> aircraft-specific aero model
- coefficient dimensionalization -> `MavFlightDynamicsMath`
- aerodynamic Rigidbody force/moment -> `MavSixDoFBody`
- atmosphere -> `MavAtmosphereModel`
- physical surface state -> `MavF16ControlActuator`
- mass / inertia preset -> `MavF16MassReference`

The legacy Maverick force/torque stack remains untouched on this branch. Do not enable both paths on the same live aircraft yet.

## Next development steps

1. Add coefficient-level regression vectors derived independently from the frozen NASA Morelli equations.
2. Add inertia-transform sanity tests.
3. Audit the NASA F-16 propulsion description and available thrust-map data before implementing reference propulsion.
4. Add propulsion as a separate force owner only after the source/data boundary is frozen.
5. Build a deterministic trim solver for straight-and-level subsonic flight.
6. Build deterministic pitch/roll/yaw step and doublet test cases.
7. Compare trajectories against AeroBenchVVPython as an external oracle only.
8. Correct implementation/sign/unit mistakes until the external traces agree within defined tolerances.
9. Only after validation, begin one-for-one ownership migration from the legacy Maverick flight stack.
