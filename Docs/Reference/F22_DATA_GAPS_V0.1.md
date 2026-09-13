# F-22 Data Gaps V0.1

Status: **R0 public-evidence ceiling / gap map**

Preferred target when exact public authority exists:

`F22A_PRODUCTION_PUBLIC_BASELINE`

This file distinguishes what can be reproduced directly from public evidence from what can only be constrained, approximated, or left unavailable.

## 1. Data availability matrix

| Quantity | Status | Current public evidence | Missing authority |
|---|---|---|---|
| REFERENCE GEOMETRY | `PARTIAL` | USAF production fact sheet closes length/span/height | wing reference area `S`, `cbar`, aerodynamic reference point/datum from a production-compatible technical source |
| MASS | `PARTIAL` | USAF publishes a 43,340-lb “weight,” 83,500-lb max takeoff and 18,000-lb internal fuel | exact mass-state definitions and usable reference mass states |
| CG | `OPEN` | no production primary CG dataset located | percent-MAC or structural station versus loading/fuel |
| INERTIA | `OPEN` | no public production tensor located | `Ixx/Iyy/Izz/Ixz` for defined mass states |
| STATIC AERO COEFFICIENTS | `OPEN` | NASA history proves F-22 static-force testing occurred; underlying tables not acquired | numeric `CX/CY/CZ/Cl/Cm/Cn` maps and exact model/configuration definitions |
| DYNAMIC DERIVATIVES | `PARTIAL` | AFRL AIAA 99-4015 is a known primary source describing rotary-balance and forced-oscillation tests, but full paper/data not acquired | lawful primary full text/data and production/EMD geometry mapping |
| CONTROL DERIVATIVES | `OPEN` | control-law and wind-tunnel literature establishes relevance but no public production derivative deck located | aerodynamic effector/TVC derivative maps |
| MACH RANGE | `QUALITATIVE_ONLY` | USAF publishes Mach-2-class and supercruise statements; test literature includes point/range context | coefficient-model validity grid and Mach breakpoints |
| ALPHA RANGE | `PARTIAL` | AFFTC EMD test source demonstrates below -40 to above +60 deg and 1-g expansion beyond +60 | production flight-model alpha validity and configuration-specific coefficient coverage |
| BETA RANGE | `OPEN` | no public production coefficient/test grid located | numeric beta envelope/grid |
| CONTROL LIMITS | `OPEN` for aerodynamic surfaces | no public final surface hard-stop table located | stabilator/aileron/rudder/flaperon limits/signs and scheduled authority |
| ACTUATOR DYNAMICS | `OPEN` | public control-law paper discusses nonlinear actuator limits as a design issue but publishes no final plant model | transfer functions, rate/position limits, hydraulic/load effects |
| FLIGHT CONTROL LAW | `QUALITATIVE_ONLY` / `PARTIAL` | 1996 program-engineer paper documents architecture/design goals; EMD flight paper confirms triplex FLCS and continuing updates | final production OFP/gains, scheduling, command laws, control allocation, mode transitions |
| PROPULSION MODEL | `QUALITATIVE_ONLY` | exact 2 x F119-PW-100 identity and high-level engine/nozzle architecture | engine state equations, throttle/FADEC mapping, transients, fuel flow |
| THRUST DECK | `OPEN` | official public sources give 35,000-lbf **class** thrust, not a deck | thrust vs Mach/altitude/power, installed losses and uncertainty |
| THRUST VECTORING | `CLOSED_EXACT` for nozzle semantics/range only | Pratt & Whitney: 2-D convergent/divergent nozzle vectors up/down as much as 20 deg | final aircraft control-allocation law, nozzle rate/position schedules and installed force/moment map |
| FLIGHT-TEST VALIDATION | `PARTIAL` | EMD high-AOA envelope/test configuration and official program milestones are public | quantitative time histories/model residuals covering the production envelope |

## 2. Publicly reproducible now

The following can be represented in a future **reference-only** profile without inventing facts, provided each field keeps its source limitations:

- production F-22A identity and public external dimensions;
- published USAF high-level weight/fuel/MTOW anchors as named, without reinterpretation into a detailed mass schedule;
- two F119-PW-100 engines;
- two-dimensional pitch thrust-vectoring nozzle architecture;
- public nozzle vector range of up/down as much as 20 deg;
- EMD high-AOA test range and triplex-FLCS fact when explicitly tagged `F22_EMD_HIGH_AOA_1999_2000`;
- F-22 control-law design philosophy/handling-quality objectives from AIAA 96-3379 when explicitly tagged as 1996 development evidence.

This is not enough to produce source-exact forces and moments.

## 3. Publicly constrainable behavior

### Control-law response

The 1996 F-22 design paper provides a strong public constraint on intended longitudinal behavior:

- designers sought a low-order/classical closed-loop response;
- CAP and short-period damping were explicit design variables;
- integrator dynamics were deliberately shaped/cancelled in the closed-loop response;
- Power Approach and Up&Away handling-quality goals were made consistent to reduce mode-transition/Pio risk;
- pitch thrust vectoring is part of integrated control design, not an independent pilot gimmick.

The paper also records development-era target ranges such as CAP/damping choices. Those are **design-state targets**, not final production gains or guarantees, and R0 does not freeze them into an F-22A controller.

### High-alpha behavior

The AFFTC EMD source proves:

- test expansion from below -40 deg to above +60 deg AOA;
- 1-g maneuvering beyond +60 deg during the 1999 campaign;
- departure-resistance testing;
- pitch-axis TVC, F119 propulsion, air-data and flight-control integration were central;
- flight test found differences from simulation/wind-tunnel predictions and drove control-law changes.

This constrains a future approximate model but does not publish the aerodynamic tables that caused those responses.

### Aerodynamic test lineage

NASA/SP-2000-4519 proves that the post-YF F-22 geometry was retested because the geometry changed materially. It documents:

- 1992 F-22 high-AOA Full-Scale Tunnel tests;
- static-force and forced-oscillation work;
- 1993 spin and rotary-balance tests.

AFRL AIAA 99-4015 is a direct lead for later dynamic wind-tunnel results. The numeric datasets remain unacquired.

## 4. Highest-priority missing documents/datasets

### P1 — AIAA 99-4015 full primary paper/data

**Title:** *AFRL F-22 Dynamic Wind Tunnel Test Results* — W. J. Gillard, AIAA 99-4015.

Needed:

- exact wind-tunnel model configuration and geometry revision;
- Mach/Reynolds ranges;
- alpha/beta ranges;
- rotary-rate and forced-oscillation definitions;
- stability/damping coefficient tables or plots;
- comparison with NASA Langley data.

Current status: `NOT_ACQUIRED`.

### P2 — 1992 Langley Full-Scale Tunnel F-22 force/oscillation database

NASA history confirms the program but not the report/data package. Need the original public technical report, if released, with:

- static force/moment coefficients;
- control-effectiveness data;
- forced-oscillation derivatives;
- exact model geometry and control state.

Current status: `OPEN` / report identity unresolved.

### P3 — 1993 Langley Spin Tunnel / rotary-balance database

Need original public report/data containing dynamic high-alpha/rotary coefficients and departure/spin analysis for the **F-22**, not YF-22.

Current status: `OPEN` / report identity unresolved.

### P4 — final EMD/production flight-control and actuator description

Need lawfully public primary material with:

- longitudinal and lateral-directional command variables;
- gain schedules;
- control allocation among aerodynamic surfaces and pitch-vectoring nozzles;
- surface hard limits and signs;
- actuator rate/dynamic limits;
- OFP/revision boundary;
- handling-quality validation results.

Current status: `OPEN`. AIAA 96-3379 closes architecture/design philosophy only.

### P5 — F119-PW-100 performance/installation deck

Need public primary data for:

- military and afterburning thrust vs Mach/altitude;
- idle/part-power behavior;
- spool/transient response;
- fuel flow;
- inlet recovery/installed loss;
- nozzle vector rate/force application point if propulsion moments are modeled.

Current status: `OPEN`. USAF/P&W public material closes identity, thrust class and nozzle range only.

## 5. Additional major gaps

### Mass properties

No production-compatible public source was located for:

- CG versus loading/fuel;
- inertia tensor;
- aerodynamic reference datum relationship;
- per-engine station/thrust line.

These remain `OPEN` even though common public websites repeat estimated values. Repetition is not authority.

### Production aero database

No public complete production F-22A database was located for:

- `CX/CA`;
- `CY`;
- `CZ/CN`;
- `Cl`;
- `Cm`;
- `Cn`;
- control derivatives;
- rate/dynamic derivatives;
- Mach/alpha/beta interpolation grids.

Do not synthesize this from YF-22, generic ATF, CFD hobby models, DCS/community mods, or photos.

## 6. Configuration conflicts / rejection rules

### YF-22 is not production F-22A

NASA public history explicitly documents significant geometry changes after the prototype. Therefore YF-22 coefficients/control gains are rejected as direct F-22A data.

### EMD is not automatically final production

The EMD high-AOA paper states the program was still finding unpredicted aero/system characteristics and modifying control laws. EMD test envelopes can validate architecture/trends but cannot silently define final production gains/limits.

### Early F-22 wind-tunnel model is not automatically EMD/production

The NASA fin-buffet paper explicitly says **early F-22 model**. It is useful family data only.

### 35,000-lbf class is not a thrust deck

USAF and Pratt & Whitney public data describe engine thrust class. That number must not become a constant installed thrust model.

## 7. Maximum defensible public-model fidelity

A public-evidence F-22 model can defensibly reach:

- correct production identity and gross external dimensions;
- correct engine count/model and pitch-vectoring architecture;
- source-tagged high-level mass/fuel anchors;
- source-constrained longitudinal handling philosophy;
- source-constrained EMD high-AOA envelope/behavior goals;
- fail-closed placeholders for unavailable aero/mass-property/propulsion maps;
- qualitative or separately labeled approximation layers for demonstration/research, if future policy explicitly permits approximation.

It cannot currently reach a provenance-grade production F-22A 6-DOF force/moment model without invented or approximated physics.

## 8. Implementation-readiness verdict

**F-22: REFERENCE-ONLY — INSUFFICIENT NUMERIC DATA**

Approximation is technically possible, but it must be labeled as approximation and kept separate from the public-reference authority layer. R0 does not authorize such approximation.
