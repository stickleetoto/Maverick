# Maverick Flight Dynamics Core

This folder is an isolated replacement flight-dynamics path under development.
It must not be enabled on the production `Mav_Player` while the legacy flight stack is still applying aerodynamic forces and control torques.

## Source policy

- NASA Morelli F-16 publications: technical / mathematical reference.
- AeroBenchVVPython: external validation only.
- No AeroBench source code is incorporated into Maverick.
- JSBSim F-16 data/model is not used for this implementation.

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

The isolated reference preset uses the F-16 nonlinear-simulation values published by Eugene A. Morelli in NASA work on aircraft inertia identification:

- mass: 637 slug (~9296 kg)
- Ix: 9,496 slug-ft^2
- Iy: 55,814 slug-ft^2
- Iz: 63,100 slug-ft^2
- Ixz: 982 slug-ft^2

`MavF16MassReference` transforms the conventional aircraft body-axis tensor into Unity local axes and diagonalizes the coupled Y/Z block so `Rigidbody.inertiaTensor` and `Rigidbody.inertiaTensorRotation` represent the same tensor.

The Rigidbody local center-of-mass offset is not guessed. `MavF16ReferenceConfigurator.centerOfMassLocalM` remains explicit because a Unity model/prefab origin is an asset convention and is not guaranteed to coincide with the aerodynamic reference point.

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

1. Add coefficient-level and inertia-transform sanity tests.
2. Add propulsion as a separate force owner. NASA Morelli material describes the engine architecture as altitude/Mach/power-level table lookup with throttle gearing and first-order power lag; numerical engine tables must come from an explicitly permitted source before claiming reference accuracy.
3. Build a deterministic trim solver for straight-and-level subsonic flight.
4. Build deterministic pitch/roll/yaw step and doublet test cases.
5. Compare trajectories against AeroBenchVVPython as an external oracle only.
6. Correct implementation/sign/unit mistakes until the external traces agree within defined tolerances.
7. Only after validation, begin one-for-one ownership migration from the legacy Maverick flight stack.
