# Next Aircraft Data Availability V0.1

Status: **R0 cross-aircraft decision record — F/A-18 vs F-22 public evidence**

Scope:

- F/A-18 family public reference acquisition
- production-targeted F-22A public reference acquisition
- source discovery, catalog, configuration compatibility and gap analysis only

No flight-model implementation, tuning, source-PDF commit, or approximation is authorized by this document.

## 1. Executive decision

| Aircraft/reference candidate | Public-source outcome | R0 verdict |
|---|---|---|
| **NASA F-18 HARV BuNo 160780 Phase-I basic hardware/software** | Exact airframe + clean pre-TVC/pre-ANSER boundary + flight-derived derivatives + rich NASA simulation/wind-tunnel lineage; underlying full nonlinear coefficient/propulsion datasets still need acquisition | **F/A-18: PARTIAL — MORE RESEARCH** |
| **Production F-22A** | Exact high-level geometry/engine/nozzle facts and strong EMD/control-law qualitative evidence; production coefficient deck, mass tensor, final control law and thrust deck not publicly recovered | **F-22: REFERENCE-ONLY — INSUFFICIENT NUMERIC DATA** |

## 2. Cross-aircraft data availability matrix

| Quantity | F/A-18 Phase-I HARV candidate | F-22A production target |
|---|---|---|
| REFERENCE GEOMETRY | `PARTIAL` | `PARTIAL` |
| MASS | `PARTIAL` | `PARTIAL` |
| CG | `PARTIAL` | `OPEN` |
| INERTIA | `PARTIAL` | `OPEN` |
| STATIC AERO COEFFICIENTS | `PARTIAL` — public simulation/database lineage exists | `OPEN` — test lineage exists, public deck not acquired |
| DYNAMIC DERIVATIVES | `CLOSED_EXACT` for bounded Phase-I lateral-directional flight-ID subset | `PARTIAL` — AFRL dynamic-wind-tunnel paper known but primary full data not acquired |
| CONTROL DERIVATIVES | `CLOSED_EXACT` for bounded Phase-I subset | `OPEN` |
| MACH RANGE | `PARTIAL` | `QUALITATIVE_ONLY` for production model; EMD/test ranges separate |
| ALPHA RANGE | `CLOSED_EXACT` for specific F/A-18 flight-ID source; `PARTIAL` overall | `PARTIAL` for EMD test evidence, not production coefficient validity |
| BETA RANGE | `PARTIAL` | `OPEN` |
| CONTROL LIMITS | `SUPPORTED_ONLY` | `OPEN` for aerodynamic surfaces |
| ACTUATOR DYNAMICS | `SUPPORTED_ONLY` | `OPEN` |
| FLIGHT CONTROL LAW | `PARTIAL` | `QUALITATIVE_ONLY` / `PARTIAL` development architecture |
| PROPULSION MODEL | `PARTIAL` | `QUALITATIVE_ONLY` |
| THRUST DECK | `OPEN` for exact Phase-I state | `OPEN` |
| THRUST VECTORING | `CLOSED_EXACT` as absent for Phase I; separate rich Phase-II dataset exists | `CLOSED_EXACT` only for production nozzle architecture and ±20-deg public vector range; allocation/rates open |
| FLIGHT-TEST VALIDATION | `CLOSED_EXACT` | `PARTIAL` |

## 3. Why F/A-18 is the stronger next implementation candidate

The F/A-18 public record contains an unusually coherent chain:

1. NASA-TM-107601 documents `f18bas`, a nonlinear six-degree-of-freedom F/A-18 simulation with a wind-tunnel-derived lookup database, engine model, sensors, actuator limiting and simplified OFP 8.3.3 control laws.
2. NASA-TM-110216 documents how `f18bas` became `f18harv`, explicitly enumerating HARV-specific aero, engine, TVC, ANSER, actuator, sensor and RFCS changes rather than hiding them.
3. NASA HARV program records identify a single physical airframe, BuNo 160780, and separate Phase I, Phase II TVC and Phase III ANSER.
4. NASA-TM-4786 isolates Phase-I flights 11–38 in **basic hardware and software configuration** and provides flight-determined lateral-directional stability/control derivatives over alpha 3–47 deg.
5. HATP baseline experimental aerodynamics material links scale-model wind tunnels, full-scale wind tunnel and flight data.

This means the remaining F/A-18 problem is principally **numeric source acquisition/transcription and exact compatibility closure**, not absence of a defensible public research lineage.

### Recommended exact F/A-18 configuration

`NASA_F18_HARV_160780_PHASE1_BASIC`

Human-readable boundary:

> NASA F-18 HARV, U.S. Navy BuNo 160780, sixth full-scale developmental F-18, Phase-I basic hardware/software state used in the 1987–1988 basic-aircraft flight-identification campaign, before the later HARV multi-axis thrust-vectoring and ANSER forebody-strake configurations.

This target is preferred over claiming a generic F/A-18C because the public evidence is tied to an exact research airframe and flight campaign.

### Important alternate source identity

`NASA_F18BAS_BASIC_F18_OFP_8_3_3_TVC_OFF_CAPABLE`

`f18bas` is the strongest public **simulation corpus** located. The hypothetical thrust-vectoring model can be disabled, but the report does not establish a specific production airframe/block. Therefore it should support the Phase-I research target only after explicit model-to-flight compatibility review.

## 4. F-22 maximum defensible fidelity from public evidence

### A. What can be reproduced from public evidence

A future F-22 reference profile can directly reproduce or record:

- production F-22A identity;
- USAF-published length/span/height;
- USAF high-level public weight, MTOW and fuel anchors with their original labels;
- two F119-PW-100 engines;
- two-dimensional pitch-vectoring nozzle architecture;
- Pratt & Whitney public nozzle range of up/down as much as 20 deg;
- EMD high-AOA flight-test facts when kept explicitly EMD;
- triplex electronic FLCS existence in EMD test aircraft;
- 1996 F-22 control-law design philosophy/handling-quality targets as development evidence, not final production gains;
- documented existence of post-YF F-22 static, forced-oscillation, spin and rotary-balance test programs.

### B. What can only be constrained publicly

Public primary/program-engineer evidence constrains:

- low-order/classical longitudinal-response intent;
- CAP/damping-based handling-quality design;
- mode-transition/PIO-risk philosophy;
- integrated use of pitch thrust vectoring;
- departure/spin resistance goals;
- EMD high-alpha behavior and the fact that flight testing exposed prediction errors;
- Mach-2-class/supercruise performance class;
- F119 test/operability heritage.

### C. What would require approximation

Without additional public primary data, any working model of these quantities would be approximation:

- production `S`, `cbar` and aerodynamic reference point if no exact technical source is acquired;
- full `CX/CY/CZ/Cl/Cm/Cn` maps;
- dynamic/control derivative maps;
- final control allocation and gain schedules;
- aerodynamic-surface actuator rates/dynamics;
- CG/inertia schedule;
- F119 Mach/altitude thrust, fuel-flow and transient maps;
- inlet/nozzle/installation force corrections.

### D. What is currently unavailable as production numeric authority

No complete public primary source was recovered in R0 for:

- production F-22A aerodynamic database;
- production mass/CG/inertia tensor package;
- final production flight-control law/gain data;
- installed F119-PW-100 thrust deck.

Therefore an “accurate F-22” built now would necessarily blend public constraints with unverified assumptions. Maverick should not describe such a model as provenance-grade F-22A physics.

## 5. Top five missing F/A-18 documents/datasets

1. **Albion H. Bowers, *HARV Aerodynamic Model*, NASA Dryden HARV project memo, 22 Jan 1990.** Cited by NASA-TM-110216; not acquired.
2. **Original `dmsf18` / McDonnell Douglas aerodynamic database package** from which NASA-TM-107601 says the `f18bas` rigid-body aero/engine lineage was largely derived.
3. **Machine-readable or visually transcribed `f18bas`/Phase-I aerodynamic lookup arrays** with grids, reference dimensions, axes and interpolation rules.
4. **Exact Phase-I BuNo 160780 mass/CG/inertia/reference-datum record** for the flight-identification campaign.
5. **Exact Phase-I F404-GE-400 installed propulsion/actuator package**, including thrust vs Mach/altitude/power, engine dynamics, physical surface hard stops and actuator rates.

Secondary high-value acquisition targets include MDC reports A7813, A7247 and A8450 cited by `f18harv` for actuator/hinge-moment behavior.

## 6. Top five missing F-22 documents/datasets

1. **W. J. Gillard, *AFRL F-22 Dynamic Wind Tunnel Test Results*, AIAA 99-4015 — lawful public full primary paper/data.** Bibliographic/abstract evidence was found, but full primary data was not acquired.
2. **1992 Langley Full-Scale Tunnel F-22 static-force/forced-oscillation dataset/report** identified by NASA/SP-2000-4519.
3. **1993 Langley F-22 Spin Tunnel / rotary-balance dataset/report** identified by NASA/SP-2000-4519.
4. **Final EMD/production F-22 flight-control and actuator description**, including command variables, gain schedules, control allocation, surface/TVC limits and OFP boundary.
5. **Production F119-PW-100 performance/installation deck**, including military/afterburner thrust vs Mach/altitude, transients, fuel flow and installed losses.

A production-compatible mass-properties/reference-geometry package (`S`, `cbar`, datum, CG, inertia) is effectively tied for top-five importance and should be searched in parallel with items 2–4.

## 7. Configuration conflicts discovered

### F/A-18

- `f18bas` contains a hypothetical two-paddle TVC concept; physical HARV Phase II uses three post-exit vanes/nozzle.
- Phase-I basic HARV cannot inherit Phase-II TVC/RFCS or Phase-III ANSER increments.
- later HARV configurations include research modifications such as LEX-fence/TVC/spin-chute/RFCS states absent from the earliest Phase-I maneuver set.
- AAW intentionally changes wing torsional stiffness and FCS.
- F/A-18E/F is a different airframe/propulsion/control family.

### F-22

- NASA documents material external-geometry changes from YF-22 to F-22; prototype numeric data is not production authority.
- 1996 F-22 control-law targets are development goals, not final production gain schedules.
- 1999–2000 high-AOA results are EMD and explicitly describe ongoing discovery/correction of unpredicted behavior.
- early F-22 wind-tunnel models are not automatically production geometry.
- public “35,000-lbf class” F119 ratings are not a constant-thrust model or deck.

## 8. Repository/source-pack boundaries

This R0 pack intentionally contains **documentation only**. No source PDF, aircraft profile, aerodynamic table, control law, engine deck or production runtime file is added.

No community/wiki/mod/game value is promoted to authority. Community sources, where encountered during discovery, are treated only as search leads and are not used to close any matrix field.

## 9. Next actions for D review

Recommended review order:

1. verify the F/A-18 target recommendation and Phase-I boundary;
2. verify that `f18bas` is kept as a simulation-source identity rather than silently equated to BuNo 160780;
3. approve focused acquisition of Bowers HARV memo + `dmsf18`/MDC lineage;
4. confirm F-22 `REFERENCE-ONLY` ceiling and YF/EMD/production separation;
5. decide whether further F-22 work should be source acquisition only or whether a separately labeled approximation profile is ever desirable.

## 10. Final R0 verdicts

**F/A-18: PARTIAL — MORE RESEARCH**

There is enough public primary evidence to justify continued source closure toward a defensible exact research-aircraft model. The most important missing pieces are likely recoverable legacy data/document packages rather than inherently unavailable performance secrets.

**F-22: REFERENCE-ONLY — INSUFFICIENT NUMERIC DATA**

Public evidence supports a strong reference/validation envelope and control/propulsion architecture, but not a provenance-grade production six-degree-of-freedom numeric flight model.
