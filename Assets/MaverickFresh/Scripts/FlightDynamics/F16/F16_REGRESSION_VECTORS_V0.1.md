# F-16 Morelli Regression Vectors v0.1

Status: **FROZEN FOR IMPLEMENTATION REGRESSION**

These vectors are deterministic coefficient-level checks for `MavF16MorelliPolynomial`.

They are derived from the frozen NASA Morelli v0.1 polynomial equations and constants documented in:

- `F16_REFERENCE_SPEC_V0.1.md`
- `F16_MORELLI_COEFFICIENT_AUDIT_V0.1.md`

AeroBenchVVPython is **not** the source of these expected values.

## Common configuration

- `xcg = 0.25 cbar`
- `xcgRef = 0.35 cbar`
- `cbar / b = 11.32 / 30`
- angular/control inputs shown below are converted to radians before evaluation
- `pHat/qHat/rHat` are already nondimensional

Expected coefficient tolerance in the current runtime validator: `5e-6` absolute.

## COEFF-000 — zero-state polynomial baseline

Inputs:

- alpha = 0 deg
- beta = 0 deg
- elevator = 0 deg
- aileron = 0 deg
- rudder = 0 deg
- pHat = 0
- qHat = 0
- rHat = 0

Expected:

- `CX = -0.019433670`
- `CY = 0.000000000`
- `CZ = -0.137827800`
- `Cl = 0.000000000`
- `Cm = -0.034076480`
- `Cn = 0.000000000`

This vector catches baseline constant and CG-correction regressions.

## COEFF-010 — pitch / q-rate coupling

Inputs:

- alpha = 10 deg
- beta = 0 deg
- elevator = -5 deg
- aileron = 0 deg
- rudder = 0 deg
- pHat = 0
- qHat = 0.02
- rHat = 0

Expected:

- `CX = 0.075518670`
- `CY = 0.000000000`
- `CZ = -1.356031491`
- `Cl = 0.000000000`
- `Cm = -0.215115393`
- `Cn = 0.000000000`

This vector exercises `Cxq`, `Czq`, `Cmq`, elevator coupling, and the longitudinal CG correction.

## COEFF-020 — lateral / directional coupling

Inputs:

- alpha = 10 deg
- beta = 5 deg
- elevator = 0 deg
- aileron = 10 deg
- rudder = -5 deg
- pHat = 0.03
- qHat = 0
- rHat = -0.02

Expected:

- `CX = 0.034331362`
- `CY = -0.117649055`
- `CZ = -0.768260057`
- `Cl = -0.056381778`
- `Cm = -0.088985246`
- `Cn = 0.036851210`

This vector exercises sideslip, aileron/rudder derivatives, roll/yaw rate derivatives, and yaw CG correction.

## COEFF-030 — mixed high-alpha state

Inputs:

- alpha = 30 deg
- beta = -8 deg
- elevator = 10 deg
- aileron = -12 deg
- rudder = 15 deg
- pHat = -0.04
- qHat = 0.025
- rHat = 0.035

Expected:

- `CX = 0.176577787`
- `CY = 0.193763457`
- `CZ = -2.653633497`
- `Cl = 0.066140018`
- `Cm = -0.489592761`
- `Cn = -0.032537919`

This vector deliberately exercises many polynomial terms simultaneously and is useful for catching decimal/sign mistakes that simpler symmetry cases miss.

## Mass / inertia regression values

For the frozen nominal reference:

- mass = `637.16 slug = 9298.6512 kg`
- body `Ix = 9496 slug-ft^2`
- body `Iy = 55814 slug-ft^2`
- body `Iz = 63100 slug-ft^2`
- body `Ixz = 982 slug-ft^2`

After the current body-axis to Unity-axis transform and principal-axis diagonalization, expected values are approximately:

- Unity principal X = `75673.6231 kg*m^2`
- Unity principal Y = `85576.4953 kg*m^2`
- Unity principal Z = `12850.4646 kg*m^2`
- principal-axis rotation about Unity X = `1.0491624 deg`

These are implementation regression values. A later Unity play-mode rigid-body test is still required to confirm that the `Rigidbody.inertiaTensorRotation` convention reproduces the intended full tensor under Unity physics.

## How to run

In Unity Editor:

`Maverick > Flight Dynamics > Run F-16 Reference Validation`

The validator checks:

- all four coefficient vectors
- frozen geometry
- mass conversion
- principal inertia values
- inertia trace preservation
- axis-conversion round trips

A failure means the reference implementation changed and should be investigated before continuing with propulsion or trim work.
