# Maverick Flight Dynamics Core

This folder is an isolated replacement flight-dynamics path under development.
It must not be enabled on the production `Mav_Player` while the legacy flight stack is still applying aerodynamic forces and control torques.

## Source policy

- NASA Morelli F-16 nonlinear aerodynamic publications: technical / mathematical reference.
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

Unity local axes are converted only at the `MavSixDoFBody` boundary:

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

## Implemented foundation

- `MavAtmosphereModel`: ISA-style temperature, pressure, density, and speed of sound.
- `MavFlightState`: TAS, Mach, dynamic pressure, alpha, beta, and p/q/r state.
- `MavMassProperties`: explicit mass, CG, and inertia tensor.
- `MavAerodynamicModelBase`: aircraft model returns coefficients only.
- `MavFlightDynamicsMath`: axis conversion and coefficient dimensionalization.
- `MavSixDoFBody`: converts `CX/CY/CZ/Cl/Cm/Cn` into physical Rigidbody force/moment.

`MavSixDoFBody.simulationEnabled` defaults to `false` intentionally.

## Next development steps

1. Extract the F-16 state/input/coefficient definitions used by the selected NASA Morelli reference.
2. Implement `MavF16AeroModel` independently from the NASA mathematical description.
3. Add actuator limits and control-surface states.
4. Add propulsion as a separate force owner.
5. Build deterministic trim / step / doublet test cases.
6. Compare the resulting trajectories against AeroBenchVVPython as an external oracle.
7. Only after validation, begin one-for-one ownership migration from the legacy Maverick flight stack.
