# F/A-18 Data Gaps V0.1

Status: **R0 availability/gap map — no invented completion**

Recommended physical reference candidate for the next research phase:

`NASA_F18_HARV_160780_PHASE1_BASIC`

This selection is provisional and exists only to make the missing-data surface auditable. It does not promote `f18bas`, HARV TVC, ANSER, AAW, F/A-18C/D, or F/A-18E/F values across configuration boundaries.

## 1. Data availability matrix

| Quantity | Status | Current public evidence | What is still required for implementation without guessing |
|---|---|---|---|
| REFERENCE GEOMETRY | `PARTIAL` | `f18bas`, HARV simulation/report families and NASA program material expose geometry/reference variables; HATP baseline sources use well-defined models | exact Phase-I `S`, `b`, `cbar`, aerodynamic reference center and structural datum transcribed from a primary source with configuration proof |
| MASS | `PARTIAL` | NASA HARV public program material gives program mass states; later HARV simulation/flight papers contain W&B data | one exact Phase-I maneuver/reference mass state with source page/table |
| CG | `PARTIAL` | HARV simulation/Morelli flight-model sources expose CG/reference-station quantities | exact Phase-I CG and datum relation, not later TVC/ANSER W&B |
| INERTIA | `PARTIAL` | Morelli/Ward and simulation lineage expose HARV inertia quantities | original exact flight-state table and phase mapping; do not rely on OCR-only transcription |
| STATIC AERO COEFFICIENTS | `PARTIAL` | NASA-TM-107601 documents table-lookup wind-tunnel aerodynamics; HATP baseline experimental databases exist; NASA-TM-110216 exposes coefficient-build architecture | actual Phase-I/basic coefficient arrays/tables or original HATP/MDC database with exact configuration tags |
| DYNAMIC DERIVATIVES | `CLOSED_EXACT` for a bounded Phase-I subset | NASA-TM-4786 gives flight-determined lateral-directional derivatives for basic hardware/software, alpha 3–47 deg; NASA-TM-102692 adds early HARV flight-ID evidence | longitudinal and full-regime derivative coverage; point-level quality/uncertainty transcription |
| CONTROL DERIVATIVES | `CLOSED_EXACT` for a bounded Phase-I subset | NASA-TM-4786 includes control-effectiveness derivatives with flight-derived estimates | complete longitudinal/lateral control-derivative set, exact surface definitions and usable tables/curves |
| MACH RANGE | `PARTIAL` | simulation model ranges are broad; flight-ID reports give condition-specific ranges | numeric validity envelope for the selected Phase-I coefficient/derivative model, not simulation capability alone |
| ALPHA RANGE | `CLOSED_EXACT` for specific derivative source / `PARTIAL` overall | Phase-I TM-4786 alpha 3–47 deg; early HARV flight ID 10–50 deg; `f18bas` database -10–90 deg | one declared model envelope tied to chosen numeric dataset |
| BETA RANGE | `PARTIAL` | `f18bas` database beta -20–+20 deg; HATP tests contain sideslip work | exact Phase-I flight/model beta envelope and sampling grid |
| CONTROL LIMITS | `SUPPORTED_ONLY` | `f18bas` and `f18harv` have position/rate limiting and primary-control tables; later HARV actuator changes are documented | exact Phase-I physical hard stops and sign convention; separate OFP command limits from mechanical limits |
| ACTUATOR DYNAMICS | `SUPPORTED_ONLY` | `f18bas` first-order/rate-position limiting lineage; `f18harv` documents later enhanced actuator/hinge-moment models | exact Phase-I actuator transfer functions/rate/load behavior or explicit proof that `f18bas` values match the airframe |
| FLIGHT CONTROL LAW | `PARTIAL` | `f18bas` documents simplified F/A-18 OFP 8.3.3 inner-loop up-and-away law; Phase-I flight derivatives explicitly had CAS engaged | exact Phase-I flight software/OFP mapping, schedules and control mixing sufficient for reproduction |
| PROPULSION MODEL | `PARTIAL` | `f18bas` includes an engine model; `f18harv` later uses a DFRC F404/GE-404 table model with PLA/Mach/altitude and engine dynamics | exact Phase-I F404-GE-400 engine model and installation boundary; no TVC losses |
| THRUST DECK | `OPEN` for exact Phase I | later HARV simulation contains table-driven engine performance; NASA public overview gives only approximate static class | primary Phase-I installed/uninstalled thrust tables versus Mach/altitude/power, including interpolation and inlet/install effects |
| THRUST VECTORING | `CLOSED_EXACT` as **ABSENT** for Phase I | phase history separates Phase-I baseline from Phase-II TVC | none for Phase-I; do not import TVC forces into target |
| FLIGHT-TEST VALIDATION | `CLOSED_EXACT` | exact Phase-I maneuver set and flight-estimated derivatives; HATP cross-facility tunnel/full-scale/flight comparisons | additional low/moderate-alpha nonlinear force/moment time histories would improve model validation but are not required to establish existence |

## 2. Highest-value closed evidence

### GAP-FA18-001 — exact airframe/configuration boundary: materially closed

NASA public HARV material identifies BuNo **160780**, the sixth full-scale developmental F-18. NASA-TM-4772 separates Phase I baseline/basic aircraft, Phase II thrust vectoring and Phase III actuated forebody strakes.

NASA-TM-4786 further isolates the best baseline flight dataset as **basic hardware and software configuration**, Phase-I flights 11–38, June 1987–March 1988.

Result: future work can use an exact research-airframe target without pretending HARV TVC/ANSER is a production Hornet.

### GAP-FA18-002 — bounded flight-derived lateral-directional derivatives: materially closed

NASA-TM-4786 supplies a direct Phase-I source for lateral-directional stability/control derivatives over alpha 3–47 deg. The report also records the major identification limitation: control augmentation remained engaged and introduced control/response correlations.

Result: these derivatives can become validation/reference data after page-level numeric transcription; no cross-configuration substitution is necessary for this bounded regime.

### GAP-FA18-003 — nonlinear public simulation architecture: materially closed at provenance level

NASA-TM-107601 publicly documents a complete nonlinear 6-DOF F/A-18 research simulation (`f18bas`) with table-lookup aerodynamics, an engine model, sensors, actuator rate/position limiting and simplified OFP 8.3.3 inner-loop control laws. It also exposes the lineage to `dmsf18` and McDonnell Douglas source material.

Result: Maverick has a strong public architecture/source graph to follow. The remaining problem is acquisition of the original lookup arrays/data package and exact compatibility proof to Phase-I HARV, not absence of a credible model lineage.

## 3. Critical gaps still open

### P1 — original baseline aerodynamic database / source code arrays

Needed:

- actual `f18bas` rigid-body aerodynamic lookup arrays or the original `dmsf18`/MDC database;
- independent variables and grids for Mach, alpha, beta, rates and surface positions;
- coefficient reference dimensions and aerodynamic reference center;
- interpolation/extrapolation behavior;
- sign/axis convention.

Why critical: without these data, the public report describes the model but does not yet provide a provenance-grade ready-to-import full coefficient deck.

### P2 — *HARV Aerodynamic Model* project memo

Known citation:

Albion H. Bowers, *HARV Aerodynamic Model*, NASA Dryden HARV project memo, 22 January 1990, cited as ref. 3.0 by NASA-TM-110216.

Status: `NOT_ACQUIRED`.

Why critical: likely the shortest path to the exact baseline/HARV aerodynamic database definition.

### P3 — exact Phase-I geometry / CG / inertia table and datum

Required in one configuration-controlled source:

- `S`, `b`, `cbar`;
- aerodynamic reference center;
- fuselage/waterline/buttline datum definitions if used;
- mass/weight, CG, `Ixx/Iyy/Izz/Ixz` for the actual flight state(s).

Do not promote common legacy-Hornet geometry values merely because multiple websites repeat them.

### P4 — Phase-I physical controls and actuator model

Required:

- stabilator, aileron, rudder, leading/trailing-edge flap and speed-brake mechanical limits;
- sign conventions;
- no-load and load-dependent rates;
- actuator transfer functions;
- exact OFP/CAS version mapping used in Phase-I flights.

NASA-TM-110216 later documents sophisticated HARV actuator changes and cites MDC A7813/A7247/A8450; these later values must not be automatically backported.

### P5 — exact Phase-I F404-GE-400 propulsion deck

Required:

- installed or explicitly uninstalled thrust vs Mach/altitude/power/PLA;
- military/min/max afterburner interpolation;
- idle and transient/spool behavior;
- inlet/install losses;
- left/right engine placement/thrust lines if propulsion moments are modeled.

The later `f18harv` engine model is a valuable lead but is not automatically the Phase-I engine truth model.

## 4. Conflicting / unsafe evidence

### Basic-Hornet versus HARV

“Basic F-18” appears in several NASA sources but does not always mean the same dated geometry. Later HARV Phase-II material includes the LEX fence introduced after the early Phase-I flights. Any curve labeled “basic” must be tied to its date/test article before direct import.

### `f18bas` versus physical Phase-I HARV

`f18bas` includes a hypothetical two-paddle TVC model, and its exact airframe serial is not stated. The report permits TVC to be disabled, which yields a documented non-vectoring simulation configuration, but it still cannot be silently declared identical to BuNo 160780 Phase I.

### Morelli/Ward Table 1 OCR

Searchable text exposes a useful HARV geometry/mass-property table, but at least one reference-station unit/layout is visibly suspect in OCR. R0 therefore does **not** freeze those numbers. Page-level visual transcription from the primary PDF is required.

### AAW and Super Hornet

AAW derivatives reflect intentional wing-stiffness/FCS changes. F/A-18E/F is a different airframe and engine family. Neither can fill legacy-Hornet gaps.

## 5. What could be implemented next without invented physics?

A **bounded validation/research model skeleton** could be created after a future numeric transcription phase using:

- exact Phase-I target identity;
- flight-derived derivative vectors from NASA-TM-4786;
- source-defined axes/signs;
- zero TVC / zero ANSER;
- no propulsion or a fail-closed propulsion placeholder.

A **full nonlinear F/A-18 flight model is not yet justified** because the complete baseline coefficient tables, exact mass/datum package, physical actuator limits and exact Phase-I thrust deck remain open.

## 6. Implementation-readiness verdict

**F/A-18: PARTIAL — MORE RESEARCH**

The public evidence is unusually strong and materially better than a typical combat-aircraft open-data situation. The blocker is not lack of credible primary research; it is acquisition/transcription of the underlying numeric database and exact Phase-I configuration tables.

### Strongest exact public configuration found

**NASA F-18 HARV BuNo 160780 — Phase I basic hardware/software configuration, specifically the flights 11–38 research state used by NASA-TM-4786.**

This is preferred over a generic F/A-18C claim because it has an exact airframe identity and bounded flight-test provenance. `f18bas` remains the strongest simulation corpus, but its configuration identity is simulation-defined rather than a specific fleet aircraft.
