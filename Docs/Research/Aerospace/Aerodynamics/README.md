# Aerodynamics and Flight-Dynamics Fundamentals

Scope: aircraft-independent references on axes, coefficient conventions, derivatives, nonlinear and high-alpha modelling, and aerodynamic model identification. It also covers rigid-body flight dynamics: EOM, inertia, attitude integration, modes, trim and linearization.

Only sources present in [`SOURCE_INDEX.json`](../SOURCE_INDEX.json) are listed as references. "Collection targets" are topics to search for, not claims that a source exists.

---

## Planned subfolders

Each subfolder is created with its first source note.

| Subfolder | Covers |
|---|---|
| `SixDOF/` | Rigid-body 6-DOF equations, inertia tensor and products of inertia, Euler angles vs quaternions, load factors |
| `CoordinateSystems/` | Body / wind / stability axes, axis transfer of derivatives, sensor position corrections, Unity ↔ aircraft-body mapping notes |
| `StabilityDerivatives/` | Static and rate derivatives, dimensionalization (force vs moment; q̄·S vs q̄·S·c̄/b) |
| `ControlDerivatives/` | Control effectiveness, effective-deflection definitions, differential tails, flaps |
| `HighAlpha/` | Forebody vortices, LEX vortices, Reynolds-number effects, unsteady/rotary aerodynamics |
| `StallDepartureSpin/` | Departure criteria, falling leaf, spin, recovery |
| `ParameterEstimation/` | Output error, equation error, frequency domain, global (polynomial / MOF) modelling |

## Starter references (r0)

| Source ID | Use | Level |
|---|---|---|
| `NASA-SP-3070` (Gainer & Hoffman, 1972) | Transformations among five axis systems; axis transfer of derivatives; accelerometer/rate-gyro/α-vane position corrections; inertia measurement methods | L2 |
| `NASA-RP-1207` (Duke, Antoniewicz & Krambeer, 1988) | General rigid-body EOM and linearization along a general trajectory (flat non-rotating earth, constant mass, no symmetry assumption) | L2 |
| `NASA-TM-74097` (Chambers & Grafton, 1977) | High-alpha aerodynamics primer: stall, spin, rotary balance, forced oscillation, test techniques | L2 |
| `NASA-TM-X-74335` (U.S. Standard Atmosphere 1976) | Atmosphere model authority | L2 |
| `NASA-RP-1168` (Maine & Iliff, 1986) | Derivative extraction from flight data (output error); role of mass data and instrumentation | L2 |
| `JAIRCRAFT-1995-MORELLI-MOF` | Global nonlinear aero modelling with multivariate orthogonal functions (applied to the F-18 HARV database) | L3 |
| `NASA-TM-4783` | Ground-to-flight correlation at high α (HARV, X-29, X-31): Reynolds-number effects on forebodies | L2 |

Maverick's F-16 path already relies on the Morelli 1998 polynomial model and the Garza & Morelli NASA/TM-2003-212145 simulation structure. Both are indexed in `Docs/Reference/F16_SOURCE_PACK_V0.1.md` and are to be migrated in F16-R1.

## Review checklist for any aero implementation

These are the same failure modes listed in the repository `CLAUDE.md`, restated for data work:

- Unity axes vs aircraft body axes. The source's sign conventions (for example Ixz, deflection signs) must be captured in `reference_convention`.
- Degrees vs radians in derivative tables. Derivatives "per degree" and "per radian" differ by a factor of 57.3.
- Dynamic pressure applied once. Coefficients × q̄ × S (× c̄ or b for moments).
- Rate derivatives nondimensionalized with c̄/(2V) or b/(2V), as the source defines them.
- Moment reference centre vs actual CG. Apply the transfer explicitly.

## Collection targets (not yet searched)

- Public NASA references on quaternion vs Euler integration in flight simulation.
- A NASA or NACA primer on force/moment coefficient conventions and dimensionalization.
- Public NASA work on interpolation/extrapolation policy for aerodynamic tables (DAVE-ML / ANSI/AIAA S-119 is a standard; check its public status before indexing).
