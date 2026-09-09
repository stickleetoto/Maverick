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

Aircraft-specific aerodynamic polynomial inputs are converted to the units used by the source model before evaluation. The Morelli F-16 polynomial uses radians for alpha, beta, and control-surface deflections.

## Implemented foundation

- `MavAtmosphereModel`: ISA-style temperature, pressure, density, and speed of sound.
- `MavFlightState`: TAS, Mach, dynamic pressure, alpha, beta, and p/q/r state.
- `MavMassProperties`: explicit mass, CG, and inertia tensor.
- `MavAerodynamicModelBase`: aircraft model returns coefficients only.
- `MavFlightDynamicsMath`: axis conversion and coefficient dimensionalization.
- `MavSixDoFBody`: converts `CX/CY/CZ/Cl/Cm/Cn` into physical Rigidbody force/moment.
- `MavF16MorelliPolynomial`: compact nonlinear Morelli F-16 aerodynamic coefficient model.
- `MavF16AeroModel`: Unity bridge that computes nondimensional rates and feeds the polynomial model.

`MavSixDoFBody.simulationEnabled` defaults to `false` intentionally.

## F-16 Morelli reference envelope

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

## Current force ownership

The new path is designed around one owner per physical effect:

- aircraft aerodynamic coefficients -> aircraft-specific aero model
- coefficient dimensionalization -> `MavFlightDynamicsMath`
- aerodynamic Rigidbody force/moment -> `MavSixDoFBody`
- atmosphere -> `MavAtmosphereModel`

The legacy Maverick force/torque stack remains untouched on this branch. Do not enable both paths on the same live aircraft yet.

## Next development steps

1. Add deterministic coefficient-level test vectors for the Morelli implementation.
2. Add F-16 mass/CG/inertia setup with an explicit aircraft-body-to-Unity inertia transform.
3. Add control-surface actuator states without importing AeroBench implementation code.
4. Add propulsion as a separate force owner.
5. Build deterministic trim / step / doublet test cases.
6. Compare trajectories against AeroBenchVVPython as an external oracle only.
7. Correct implementation/sign/unit mistakes until the external traces agree within defined tolerances.
8. Only after validation, begin one-for-one ownership migration from the legacy Maverick flight stack.
