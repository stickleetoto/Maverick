# F/A-18 Implementation Feasibility (r0, R1 addendum)

> **R1 note.** This page is the r0 text plus an R1 addendum at the end. r0 level names map to R1 names as L0 = `SEARCH_LEAD_ONLY`, L1 = `CATALOGUE_VERIFIED`, L2 = `ABSTRACT_VERIFIED`, L3 = `CONTENT_EXTRACT_VERIFIED`, L4/L5 = `PAGE_VERIFIED` ([`SCHEMA.md` §2](../../SCHEMA.md#2-verification-levels)).


Status: **research assessment, not an implementation plan.** Nothing here authorises runtime work. Evidence is at L2/L3 at most (see [library README §4](../../README.md#4-retrieval-limitation-read-this-before-trusting-anything)), so every "yes" below is conditional on the page-verification package FA18-R1.

---

## 1. Short verdict

- The **NASA F/A-18 HARV (NASA 840)** is the only F/A-18 configuration with a public source set deep enough for a source-honest research model. That set covers a documented nonlinear simulation, per-phase mass properties, flight-derived derivatives from two independent teams, a code-level research control-law specification, a simplified engine model, and ground-to-flight correlation studies.
- A **production F/A-18A/B/C/D** model is **not** feasible source-honestly. The production FCS and the manufacturer aero database are not public.
- Whether a **table-level** HARV model is possible without guessing depends on one unverified fact: whether the f18bas/f18harv aerodynamic and engine tables are printed in the public NASA TMs (Q-A1, Q-P1). Two fallback routes exist (§3).
- **Feasibility: plausible, not yet demonstrated.** r0 cannot honestly say "yes".

## 2. Answers to the F/A-18 first-pass questions

### Q1. Which F/A-18 research configuration has the strongest public source set?

**NASA F/A-18 HARV, NASA 840 (BuNo 160780), as modelled by the NASA Langley `f18harv` simulation (NASA-TM-110216).** Supporting evidence:

| Need | HARV coverage |
|---|---|
| Nonlinear model description | NASA-TM-110216 (f18harv) and its predecessor NASA-TM-107601 (f18bas) |
| Mass / CG / inertia | NASA-TM-4772, split by phase |
| Reference geometry | NASA-TM-4772 (and likely TM-110216) |
| Actuator limits/rates | HARV simulation table (TM-110216 / CR-198248, attribution pending) |
| Control laws | NASA-TM-110217 (ANSER, code-level), NASA-TP-3446, NASA-TP-1998-208465, NASA-CR-198250 (implementation validation), NASA-TM-110228 (TV mixer) |
| Flight-derived aerodynamics | NASA-TM-4786 (basic), NASA-TP-97-206539 and NASA-TP-1999-206573 (TV), NASA-CR-194838 and the WVU line (independent team) |
| Propulsion | NASA-TM-4240 (simplified F404 dynamic model) |
| Validation context | NASA-TM-4783 (ground-to-flight), NASA-TM-4773 (handling qualities), NTRS-19980200994 (closed-loop sys-ID), NTRS-19970014822 (flight-data processing) |

Runner-up configurations: **AAW NASA 853** (flight-derived aero model NASA-TM-2005-213668, but a modified flexible wing and a transonic/supersonic roll-control focus), and **SRA NASA 845** (actuator technology only).

### Q2. Can we build a public-data 6-DOF model without guessed coefficients?

**Conditionally.** There are three candidate routes. None is confirmed yet:

| Route | Basis | Confirmed? | If it works |
|---|---|---|---|
| **T: tables** | f18bas/f18harv aero + engine tables printed in NASA-TM-107601/110216 | **No** (Q-A1, Q-P1) | Full nonlinear model over the database domain. Best outcome |
| **M: global polynomial** | Morelli multivariate-orthogonal-function HARV models (NTRS-19940020628; J. Aircraft 1995) | **No** (Q-A3) | Compact subsonic model, like the Maverick F-16 Morelli path. Probably longitudinal-heavy; lateral-directional coverage unknown |
| **D: digitized derivatives** | α-scheduled derivatives digitized from Iliff & Wang / Napolitano plots | Plots are public (abstract-level) | Quasi-linear model limited to the flight-tested α band (e.g. 3–47° lat-dir basic). Outside that band the model is `UNAVAILABLE`. Needs a digitization uncertainty budget. Basic-configuration **longitudinal** flight derivatives are a gap (Q-V5) |

Engine: the structure (rate limiter + low-pass filter + TV axial-loss method) is public (NASA-TM-4240). **Thrust numbers are not confirmed public** (Q-P1). Until they are, the honest propulsion model is a zero/null implementation, the same policy Maverick used for the F-16 before TP-1538 Table VI was frozen.

### Q3. Is a full aerodynamic database public, or only derivatives/figures?

- **Derivatives and figures: yes.** Flight-derived derivative-vs-α plots (several reports) and tunnel data (NASA-TP-3111, NASA-CR-4582, NASA-TP-3531) are in public NASA reports.
- **Full tabular database: unknown.** The simulation reports describe a wind-tunnel-derived database (α −10..+90°, β ±20°, M 0..2 per extract). r0 could not confirm whether the tables are printed or were distributed separately, or under what release terms. This is the single most important fact for FA18-R1 to establish.

### Q4. Which implementation-critical items are publicly recoverable?

Full matrix: [`KNOWN_DATA.md` §4](KNOWN_DATA.md#4-public-recoverability-matrix). Summary:

| Item | Verdict |
|---|---|
| S, c̄, b | **Yes** (candidate captured; page-verify) |
| Mass | **Yes**, per phase (candidate captured) |
| CG | **Yes** as %MAC and FS (candidates captured); MAC LE station, FS datum and aero moment reference not captured |
| Inertia | **Yes** Ixx/Iyy/Izz (captured, with a conflicting second set). **Ixz: not yet** |
| Surface geometry | **Unknown** (deflection definitions and mixing needed from the simulation TMs) |
| Actuator limits | **Yes** for HARV simulation surfaces (source attribution pending) |
| Actuator rates | **Partly** (stabilator, aileron, rudder; HARV LEF/TEF rates and all bandwidths missing) |
| FCS gains/schedules | **Production: no.** **NASA research laws: yes** (NASA-TM-110217 code-level; TP-3446 / TP-1998-208465 likely) |
| Propulsion data | **Structure yes, numbers unknown**; manufacturer model not public |

### Q5. What exact configuration should Maverick target first?

**`FA18-TGT-A`: the NASA HARV research reference, TV and strakes inactive.**

| Aspect | Definition |
|---|---|
| Aerodynamics | f18harv database (NASA-TM-110216), with TV and forebody-strake increments **excluded from the build-up** (not zeroed after the fact). Record exactly which HARV flight-data table updates remain (Q-A2) |
| Mass | NASA-TM-4772 **Phase 2/3** loading (candidate 36,099 lbm, 23.8 % MAC) once page-verified. Do **not** mix in the Phase 1 column |
| Actuators | HARV simulation limits/rates (after Q-ACT1/Q-ACT2) |
| Propulsion | F404 simplified model structure (NASA-TM-4240). Zero-thrust stub until thrust tables are verified public (Q-P1) |
| Control | Maverick C0 direct-surface mapping first. ANSER research law (NASA-TM-110217) later, as a separate research mode |
| Label | "NASA F/A-18 HARV research reference (TV/strakes inactive)". **Never** "F/A-18A", "F/A-18C" or "Hornet FCS" |

Why Phase 2/3 rather than Phase 1: the published research control laws and their nonlinear batch responses were produced on the f18harv model, which represents the TV-equipped airframe. Keeping the Phase 2/3 mass state lets every later stage (ANSER law, closed-loop responses) validate against the same aero and mass definitions without swapping configuration mid-programme. Phase 1 (`FA18-CFG-HARV-P1-BASIC`) stays a **validation-only comparison** through NASA-TM-4786 and the basic-vs-TV comparison in NASA-TP-1999-206573. It is never merged into the target.

A TV-active HARV and an ANSER-strake HARV are **separate later profiles** (mixer NASA-TM-110228, TV data NASA-TP-3531), each with its own validation.

### Q6. What validation levels can that configuration reach?

| Level | Verdict | Condition / evidence |
|---|---|---|
| **Coefficient-level validation** | **Conditional** | Route T or M confirmed → table/polynomial regression. Otherwise derivative-level only: finite-difference derivatives of the Maverick model against digitized Iliff & Wang / Napolitano derivatives within their α band, with documented digitization uncertainty. Keep NASA-TM-4783 caveats in mind (tunnel vs flight forebody effects at high α) |
| **Trim validation** | **Partial** | Trim *self-consistency* is always possible. *External* trim validation needs published trim points (Q-V2), which are not confirmed. Label results "trim-consistent" until then |
| **Headless trajectory validation** | **Conditional, closed-loop first** | After the ANSER law is implemented from NASA-TM-110217: compare with published nonlinear batch responses (NASA-TP-3446, NASA-TP-1998-208465, NASA-CR-198250) if initial conditions are complete (Q-V4). Open-loop comparison against flight time histories depends on data availability (Q-V1) |
| **Research-mode PlayMode flight** | **Yes, once aero + mass + actuators exist** | With C0 direct-surface control, or the ANSER research law behind a research flag. Live takeover stays off by default, following existing Maverick FDM policy |

### Q7. Top remaining blockers (ranked)

1. **Document access.** The r0 environment could not open NTRS/DTIC PDFs, so no number is page-verified. Run FA18-R1 where `ntrs.nasa.gov` is reachable.
2. **Aero table availability (Q-A1)** and the TV/strake separation question (Q-A2).
3. **Installed thrust numbers (Q-P1)** and engine dynamics constants (Q-P2, Q-P3).
4. **Ixz, moment reference centre, CG datum (Q-M1, Q-A7, Q-M3)** and the inertia conflict (Q-M2).
5. **No public production FCS.** A "baseline F/A-18 handling" claim is impossible. Only NASA research laws or clearly labelled Maverick-owned laws are allowed.
6. **Actuator dynamics completeness (Q-ACT1, Q-ACT2).**
7. **Flight time-history availability (Q-V1)** for open-loop trajectory validation.

## 3. Staged path (for a future implementation branch; not started)

| Stage | Output | Gate to pass |
|---|---|---|
| A0 | FA18-R1 page verification; decide route T / M / D | Q-A1, Q-A7, Q-M1, Q-M3, Q-P1 answered at L4 |
| A1 | Aerodynamic model + mass + geometry for FA18-TGT-A (headless) | Coefficient or derivative regression within stated domain |
| A2 | Actuators + engine (or zero-thrust stub) + trim | Trim self-consistency; external trim if Q-V2 yields data |
| A3 | Headless open-loop responses | Mode checks (short period, Dutch roll, roll subsidence) against derivative-based expectations; flight data if Q-V1 succeeds |
| A4 | ANSER research law from NASA-TM-110217 (research flag) | Closed-loop responses vs NASA-CR-198250 / TP-3446 / TP-1998-208465 |
| A5 | Research-mode PlayMode | Existing Maverick live-FDM safety policy |
| Later | TV-active and ANSER-strake profiles | Separate validation each |

## 4. What would change this assessment

- **Tables are printed publicly (Q-A1 = yes):** feasibility becomes "yes, table-level", and A1 can start directly after FA18-R1.
- **Tables are separately distributed under restriction:** Route T is closed. Choose between M (if coefficients are printed) and D (digitization). Downgrade the target to "derivative-level research model" with an explicit α band.
- **Thrust tables are not public:** powered flight needs a separately sourced engine deck. Do **not** borrow the F-16 TP-1538 F100 table or any F404 fact-sheet figure.

## 5. Next research packages

1. **FA18-R1: page verification and table census** (needs NTRS access). Open NASA-TM-107601, NASA-TM-110216, NASA-TM-4772, NASA-TM-110217, NASA-TM-4240 and NASA-CR-198248. Hash each file and index its pages. Answer Q-A1, Q-A2, Q-A7, Q-M1–M3, Q-ACT1–2, Q-C1–C2 and Q-P1–P3. Move `KNOWN_DATA` values to L4, and transcribe mass, geometry and actuator tables with a two-pass check (L5 pattern from `Docs/Reference/Data/F16/TP1538/`).
2. **FA18-R2: aerodynamic-model route decision.** Resolve the Morelli MOF lead (NTRS-19940020628, J. Aircraft 1995) and the upstream database identity from the TM-107601 bibliography. Write the digitization plan and pilot-digitize one NASA-TM-4786 derivative figure with a full uncertainty manifest. Fold in the NASA-TM-4783 ground-to-flight caveats.
3. **F16-R1 (Priority B): central F-16 lineage.** Migrate `Docs/Reference/F16_SOURCE_PACK_V0.1.md` into `SOURCE_INDEX.json` with verification levels. Extend it with NASA thrust data, high-alpha and departure studies and flight-test validation. Separate the specific NASA test aircraft from production-family data and from simulator/research models. Check whether the NESC 6-DOF check-cases (NASA-TM-2015-218675) include a case directly usable for the Maverick F-16 reference.

---

## 6. R1 addendum

- **Answers to Q2/Q3 hardened by repository readings.** The public f18bas and f18harv reports do **not** print their aerodynamic arrays (repository page reading). A table-level F/A-18 model without guessed coefficients therefore still depends on an unpublished MDC A7247/A8575 lineage. The public alternatives are flight-derived derivatives (validation), a possible Morelli polynomial fit (Q-A3 open) and the CR-3608 rotary data (1/10-scale, cross-validation only).
- **What improved.** Phase I mass state now has a printed Ixz (-2,039 slug-ft^2) and CG station and waterline (repository). A simplified baseline CAS is printed in f18bas. Three actuator lineages are separable.
- **What did not change.** No F/A-18 value is `ALLOWED`. The library's own page-verification pass (FA18-R2) is still the gate.
- **Cross-aircraft position** (no score): see [`../../AIRCRAFT_DATA_COVERAGE_MATRIX.md`](../../AIRCRAFT_DATA_COVERAGE_MATRIX.md).
