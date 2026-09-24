# Aerodynamics and Flight-Dynamics Fundamentals

Scope: aircraft-independent references on axes, coefficient conventions, derivatives, nonlinear and high-alpha modelling, unsteady aerodynamics and aerodynamic model identification. It also covers rigid-body flight dynamics (EOM, inertia and its measurement, attitude integration, modes, trim and linearization).

Only sources present in [`SOURCE_INDEX.json`](../SOURCE_INDEX.json) are listed. "Collection targets" are topics to search for, not claims that a source exists. Levels use the R1 vocabulary ([`SCHEMA.md` §2](../SCHEMA.md#2-verification-levels)).

---

## Planned subfolders

Each subfolder is created with its first source note.

| Subfolder | Covers |
|---|---|
| `SixDOF/` | Rigid-body 6-DOF equations, inertia tensor and products of inertia, Euler angles vs quaternions, load factors |
| `CoordinateSystems/` | Body / wind / stability axes, axis transfer of derivatives, sensor position corrections, Unity ↔ aircraft-body mapping notes |
| `MassProperties/` | Measuring and bounding mass, CG and inertia; product-of-inertia sign conventions |
| `StabilityDerivatives/` | Static and rate derivatives, dimensionalization (force vs moment; q̄·S vs q̄·S·c̄/b) |
| `ControlDerivatives/` | Control effectiveness, effective-deflection definitions, differential tails, flaps |
| `HighAlpha/` | Forebody vortices, LEX vortices, Reynolds-number effects, rotary aerodynamics, nose-down control margin |
| `Unsteady/` | Indicial and state-space unsteady aerodynamic models |
| `StallDepartureSpin/` | Departure criteria, falling leaf, spin, recovery |
| `ParameterEstimation/` | Output error, equation error, frequency domain, global (polynomial / MOF) modelling |

## References

| Source ID | Use | Level |
|---|---|---|
| `NASA-SP-3070` (Gainer & Hoffman, 1972) | Transformations among five axis systems; axis transfer of derivatives; sensor position corrections; inertia measurement methods | ABS |
| `NASA-RP-1207` (Duke, Antoniewicz & Krambeer, 1988) | General rigid-body EOM and linearization along a trajectory | ABS |
| `NASA-CR-2497` (McFarland, 1975) **R1** | Standard kinematic model for flight simulation (NASA Ames): an independent statement of the kinematics for reviewing `MavSixDoFBody` | CAT |
| `NASA-TR-R-433` (Wolowicz & Yancey, 1974) **R1** | Experimental determination of airplane mass and inertial characteristics: how good a published inertia can be | CAT |
| `NASA-TM-X-74335` (U.S. Standard Atmosphere 1976) | Atmosphere model authority | ABS |
| `NASA-TM-74097` (Chambers & Grafton, 1977) | High-alpha aerodynamics primer: stall, spin, rotary balance, forced oscillation | ABS |
| `NASA-TM-101684` (Nguyen & Foster, 1990) **R1** | Preliminary high-alpha nose-down pitch-control requirement for relaxed-stability fighters | CAT |
| `NADC-88020-60` (Seltzer & Rhodeside, 1988) **R1** | High-alpha flying-qualities fundamentals and departure criteria | CAT |
| `AFWAL-TR-80-3141` (Johnston, Mitchell & Myers, 1980) **R1** | High-alpha manoeuvre-limiting factors; Part III carries F-4J and F-14A aerodynamic models (future F-14 pack) | CAT |
| `NASA-TM-109120` (Klein & Noderer, 1994) **R1** | Unsteady aerodynamic model structures (Part 1; Parts 2-3 are TM-110161 and TM-110259) | CAT |
| `NASA-RP-1168` (Maine & Iliff, 1986) | Output-error derivative extraction from flight data | ABS |
| `MORELLI-ACC-1998-F16` **R1** | Global nonlinear polynomial modelling applied to the F-16 database (Maverick's implemented model) | ABS |
| `JAIRCRAFT-1995-MORELLI-MOF` | Multivariate orthogonal functions applied to the F-18 HARV database | EXTRACT |
| `JAIRCRAFT-2023-MORELLI-GRAUER` **R1** | Survey of NASA Langley system identification advances | CAT |
| `NASA-TM-4783` | Ground-to-flight correlation at high alpha (HARV, X-29, X-31) | ABS |
| `NASA-CR-186019` (Brumbaugh, 1991) **R1** | Benchmark fighter model for control design ("not representative of any particular aircraft"). Useful as a worked example of a complete, declared-generic model | CAT |

Aircraft-specific aerodynamic lineages are in the packs: [F-16 model lineage](../Aircraft/F16/F16_MODEL_LINEAGE.md), [F/A-18 source graph](../Aircraft/FA18/SOURCE_GRAPH.md), [F-15 public data matrix](../Aircraft/F15/F15_PUBLIC_DATA_MATRIX.md).

## Review checklist for any aero implementation

These are the same failure modes listed in the repository `CLAUDE.md`, restated for data work:

- Unity axes vs aircraft body axes. The source's sign conventions (for example Ixz, deflection signs) must be captured in `reference_convention`. F/A-18 f18bas, for instance, defines Ixz = ∫ρxz dV and puts its negative off-diagonal (repository reading).
- Degrees vs radians in derivative tables. "Per degree" and "per radian" differ by a factor of 57.3.
- Dynamic pressure applied once. Coefficients × q̄ × S (× c̄ or b for moments).
- Rate derivatives nondimensionalized with c̄/(2V) or b/(2V), as the source defines them.
- Moment reference centre vs actual CG. Apply the transfer explicitly. The F-16 Morelli model applies it differently to Cm and Cn on purpose.
- Physical span vs reference span. They differ in real reports (NF-15B 837: 42.83 ft three-view, 42.7 ft reference).

## Collection targets (not yet searched)

- Public NASA references on quaternion vs Euler integration in flight simulation.
- A NASA or NACA primer on force/moment coefficient conventions and dimensionalization.
- ANSI/AIAA S-119 public status (the DAVE-ML paper `AIAA-2002-4482` is indexed; the standard itself is not).
