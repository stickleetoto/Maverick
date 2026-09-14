# F/A-18 Data Gaps V0.2

Status: **POST-ACQUISITION GAP CLOSURE — NASA PUBLIC SOURCES**

This document supersedes the F/A-18 gap-status conclusions in V0.1 where the acquired primary PDFs listed below provide stronger evidence. It does not change configuration-compatibility boundaries.

## Availability matrix

| Field | `NASA_F18_HARV_160780_PHASE1_BASIC` | `f18bas` basic simulation | HARV Phase-II/III | Notes |
|---|---|---|---|---|
| Reference geometry | **CLOSED_EXACT** | **CLOSED_EXACT** | **CLOSED_EXACT/PARTIAL by state** | Phase-I Table 3; f18bas Tables 3.3/3.4 |
| Mass | **CLOSED_EXACT** for 60%-fuel table condition | **CLOSED_EXACT** for published states | **CLOSED_EXACT** for published simulation states | Fuel schedule still open for exact Phase I |
| CG | **CLOSED_EXACT** for 60%-fuel table condition | **CLOSED_EXACT** for published states | **CLOSED_EXACT** for published simulation states | |
| Inertia | **CLOSED_EXACT** for 60%-fuel table condition | **CLOSED_EXACT** for published states | **CLOSED_EXACT** for published simulation states | Preserve negative Ixz |
| Static aero coefficients | **OPEN** | **PARTIAL** | **PARTIAL** | buildup equations known; full numeric lookup arrays missing |
| Dynamic derivatives | **PARTIAL** | **PARTIAL** | **CLOSED_COMPATIBLE/PARTIAL** | flight PID sources strong but not a complete nonlinear deck |
| Control derivatives | **PARTIAL** | **PARTIAL** | **CLOSED_COMPATIBLE/PARTIAL** | flight PID sources available; digitization/point audit ongoing |
| Mach range | **PARTIAL** | **CLOSED_EXACT** for simulation envelope | **CLOSED_EXACT** for simulation envelope | f18harv 0–2 |
| Alpha range | **PARTIAL** | **CLOSED_EXACT** -10..+90 deg | **CLOSED_EXACT** -10..+90 deg simulation envelope | flight-test subsets narrower |
| Beta range | **PARTIAL** | **CLOSED_EXACT** -20..+20 deg | **CLOSED_EXACT** -20..+20 deg simulation envelope | |
| Control limits | **CLOSED_COMPATIBLE** | **CLOSED_EXACT** for model | **CLOSED_EXACT** for HARV model/test state | do not mix 61 vs 82 deg/s rudder |
| Actuator dynamics | **PARTIAL** | **CLOSED_EXACT** for model | **CLOSED_EXACT** for HARV simulation | physical Phase-I exact hardware transfer still needs compatibility statement |
| Flight-control law | **PARTIAL** | **CLOSED_EXACT** for modeled 8.3.3 AFU inner-loop | **CLOSED_EXACT** for documented research-control architecture | production C/D not implied |
| Propulsion model | **PARTIAL** | **PARTIAL** | **PARTIAL** | F404 model structure and dynamics public |
| Thrust deck | **PARTIAL** | **OPEN/PARTIAL** | **PARTIAL** | Max-AB gross-thrust surface digitized; full 29 arrays missing |
| Thrust vectoring | N/A | preliminary hypothetical system only | **CLOSED_EXACT/PARTIAL** | actual HARV 3-vane system documented; full underlying numeric TV tables missing |
| Flight-test validation | **CLOSED_EXACT/PARTIAL** | **PARTIAL** | **CLOSED_EXACT** for reported PID campaigns | no claim of full-envelope validation |

## Exact gaps closed in this milestone

1. Phase-I HARV 60%-fuel clean reference geometry.
2. Phase-I HARV 60%-fuel clean mass, CG and inertia, including signed `Ixz=-2039 slug-ft²`.
3. Conventional HARV control-surface position/rate limits from a primary NASA table.
4. `f18bas` reference dimensions and published Fighter Escort 60%-fuel mass state.
5. `f18bas` first-order actuator limits/time constants.
6. `f18harv` higher-fidelity primary-control and TV-vane actuator models.
7. F404/HARV engine table topology, breakpoints, dependent variables, interpolation method and transient time constants.
8. Public Max-AB gross-thrust surface sufficient for a labeled digitized approximation/cross-check.

## Highest-priority remaining gaps

1. **Basic F/A-18 nonlinear aerodynamic numeric arrays** feeding `SFAERRF/USAERRF`.
2. **Underlying MDC A7247/A8575 data or lawful NASA derivative/source package** containing the actual coefficient lookup values.
3. **Full F404 29-array numeric deck**, especially idle, mil and minimum-AB values plus installation-effect tables.
4. **Phase-I exact fuel-dependent mass/CG/inertia schedule** for BuNo 160780.
5. **Point-level digitization and cross-validation of flight-derived derivatives**, preferring later Iliff/Wang NASA TPs over the 1993 contractor report where both cover the same quantity.

## Conflict register

- `f18bas` rudder no-load rate: 61 deg/s; HARV/Dryden conventional rudder rate: 82 deg/s.
- `f18bas` LEF rate/position: 18 deg/s, -3..+34 deg; HARV/Dryden: 15 deg/s, -3..+33 deg.
- F404 increasing-PLA rate limit: TM-4240 publishes 19.03/26.81 deg/s; TM-110216 publishes 14/22 deg/s for non-AB/AB.
- These are **configuration/model-version differences**, not values to average.

## Readiness

Strongest exact physical target remains:

`NASA_F18_HARV_160780_PHASE1_BASIC`

Current verdict:

**PARTIAL — MORE RESEARCH**

A defensible reference skeleton and substantial validation model can now be implemented without guessing. A provenance-grade full nonlinear aerodynamic model still cannot.
