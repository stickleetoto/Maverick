# F/A-18 Public Source Acquisition — Deep Dive R0 M2

Status: **RESEARCH MILESTONE — Phase-I physical anchors materially closed; no aircraft implementation**

This milestone extends the R0/M1 source-acquisition work and keeps the physical target separate from the source-defined `f18bas` simulation.

Recommended physical target remains:

`NASA_F18_HARV_160780_PHASE1_BASIC`

Strongest public simulation corpus remains:

`NASA_F18BAS_BASIC_F18_OFP_8_3_3_TVC_OFF_CAPABLE`

No Phase-II TVC, Phase-III ANSER, AAW, F/A-18C/D, or Super Hornet datum is silently promoted into the Phase-I target.

## 1. Major result

NASA/TP-1999-206573 contains a direct side-by-side comparison of the **unmodified Phase-I** F-18 HARV and the **modified Phase-II/III** HARV. That table closes the previously open Phase-I reference geometry and one exact mass/CG/inertia state without relying on generic Hornet web specifications or on the later modified-HARV simulation.

The same NASA technical publication also states that the conventional aerodynamic-control-surface position and rate limits listed in its Table 1 are identical for the HARV and basic F-18. This supplies a stronger physical-aircraft authority than the simplified `f18bas` HILS actuator table for those fields.

Result: a provenance-grade Phase-I physical profile can now be defined for geometry, one W&B/inertia state, and conventional control hard limits/rates. The remaining implementation blockers are chiefly the complete nonlinear aerodynamic database, exact actuator dynamics under load, and a Phase-I-compatible F404-GE-400 propulsion deck.

## 2. New primary source closure — NASA/TP-1999-206573

### FA18-M2-SRC-001

- **Title:** *Flight-Determined, Subsonic, Lateral-Directional Stability and Control Derivatives of the Thrust-Vectoring F-18 High Angle of Attack Research Vehicle (HARV), and Comparisons to the Basic F-18 and Predicted Derivatives*
- **Report:** NASA/TP-1999-206573 / H-2252 / NTRS 19990019364
- **Authors:** Kenneth W. Iliff; Kon-Sheng Charles Wang
- **Organization:** NASA Dryden Flight Research Center / SPARTA, Inc.
- **Publication date:** January 1999
- **Repository:** NASA NTRS
- **Stable URL:** https://ntrs.nasa.gov/citations/19990019364
- **Public-release state:** Public; U.S. Government work/public use permitted in NTRS metadata
- **PDF filename:** `19990019364.pdf`
- **SHA256:** `NOT_COMPUTED — source PDF bytes not stored in repository`
- **Configuration applicability:** Table 3 explicitly separates `Unmodified (Phase I)` from `Modified (Phases II and III)`; Table 1 states conventional surface limits are identical for HARV and basic F-18.
- **Classification:** `PRIMARY_EXACT` for the Phase-I Table-3 physical state; `PRIMARY_COMPATIBLE` for the conventional-surface limits explicitly stated to match basic F-18/HARV.

### Visual-verification note

NTRS exposed the complete Table-3 text and source metadata through its public searchable record, but direct PDF rendering returned HTTP 403 in this acquisition session. Therefore the table is recorded with exact public-source transcription and source anchors, while a future locally acquired lawful PDF should still receive a visual page check before code/data freeze.

No OCR-only ambiguous cell was guessed.

## 3. Exact Phase-I physical reference state recovered

NASA/TP-1999-206573 Table 3 labels the left-hand column **Unmodified (Phase I)** and gives the following state:

| Field | Phase-I value | R0 status |
|---|---:|---|
| Aircraft weight | **31,980 lb** | `CLOSED_EXACT` |
| Reference wing area `S` | **400 ft^2** | `CLOSED_EXACT` |
| Reference mean aerodynamic chord `cbar` | **11.52 ft** | `CLOSED_EXACT` |
| Reference span `b` | **37.4 ft** | `CLOSED_EXACT` |
| CG | **21.9% MAC** | `CLOSED_EXACT` |
| CG fuselage reference station | **454.33 in** | `CLOSED_EXACT` |
| CG waterline | **105.24 in** | `CLOSED_EXACT` |
| `Ixx` | **22,040 slug-ft^2** | `CLOSED_EXACT` |
| `Iyy` | **124,554 slug-ft^2** | `CLOSED_EXACT` |
| `Izz` | **139,382 slug-ft^2** | `CLOSED_EXACT` |
| `Ixz` / product of inertia | **-2,039 slug-ft^2** | `CLOSED_EXACT`; preserve printed sign |
| Overall length | **56 ft** | `CLOSED_EXACT` |
| Wing aspect ratio | **3.5** | `CLOSED_EXACT` |
| Stabilator span | **21.6 ft** | `CLOSED_EXACT` |
| Stabilator area, both surfaces total | **88.26 ft^2** | `CLOSED_EXACT` |

Table-3 footnote configuration:

- fuel weight **6,480 lb**;
- approximately **60-percent fuel**;
- landing gear **up**;
- configuration **clean**;
- pilot and support equipment included in weight.

This state is now the recommended mass-property anchor for `NASA_F18_HARV_160780_PHASE1_BASIC`.

Do not substitute the modified Phase-II/III column, which has substantially different weight/CG/inertias and a different stabilator area because of research modifications.

## 4. Conventional control surface hard limits and rates

NASA/TP-1999-206573 Table 1 and associated text state that these conventional-surface limits are identical for HARV and basic F-18.

| Surface | Position limit | Rate limit | Phase-I authority |
|---|---|---:|---|
| Stabilator | 24.0 deg TE-up; 10.5 deg TE-down | **40 deg/s** | `CLOSED_COMPATIBLE` with explicit equality statement |
| Aileron | 24.0 deg TE-up; 45.0 deg TE-down | **100 deg/s** | same |
| Rudder | 30.0 deg left; 30.0 deg right | **82 deg/s** | same |
| Trailing-edge flap | 8.0 deg up; 45.0 deg down | **18 deg/s** | same |
| Leading-edge flap | 3.0 deg up; 33.0 deg down | **15 deg/s** | same |
| Speed brake | 60.0 deg TE-up | **20–30 deg/s** | same |

### Important conflict with `f18bas`

NASA TM-107601 `f18bas` uses simulation/HILS actuator numbers that are not identical to these physical limits. Examples include a rudder rate of 61 deg/s in the HILS model and other small position/rate differences.

This conflict is now resolved by provenance rather than averaging:

- use NASA/TP-1999-206573 Table 1 for the Phase-I physical target's conventional surface limits/rates;
- use NASA TM-107601 actuator values only when reproducing the source-defined `f18bas` simulation;
- do not infer actuator transfer-function dynamics merely from a hard rate limit.

## 5. Phase-I/basic flight-control architecture strengthened

The public NASA lineage now supports the following configuration statement for the basic/unmodified F-18 used as the Phase-I comparison state:

- digitally mechanized fly-by-wire control-augmentation system;
- quadruplex-redundant GE-701E flight-control computers;
- standard F/A-18 **V10.1** flight-control-law architecture is identified in the HARV/basic-F-18 literature;
- later HARV TVC phases add analog interface hardware and research flight-control-system paths rather than redefining the Phase-I basic state.

NASA TM-107601 independently reproduces much of the production-law theory/source lineage in section 9 and cites McDonnell Douglas A4107/A7813. Its implementation is described as based on an OFP 8.3.3 production PROM set while source figures/functions include V10.1 design material.

### Authority boundary

The public documents strongly close FCS architecture and software-lineage identity, but they do **not** yet justify claiming that every gain, schedule, filter, or mode in `f18bas` exactly equals the Phase-I BuNo 160780 flight software.

Therefore:

- architecture / major control-path identity: `CLOSED_COMPATIBLE`;
- exact Phase-I complete scheduled gain set: `PARTIAL`;
- final physical actuator dynamics under aerodynamic load: `PARTIAL`.

## 6. New aerodynamic-model source — NASA CR-187469

### FA18-M2-SRC-002

- **Title:** *Analytical Aerodynamic Model of a High Alpha Research Vehicle Wind-Tunnel Model*
- **Report:** NASA-CR-187469 / NAS 1.26:187469 / NTRS 19910004091
- **Authors:** Jichang Cao; Frederick Garrett Jr.; Eric Hoffman; Harold Stalford
- **Organization:** Georgia Institute of Technology for NASA Langley Research Center
- **Publication date:** September 1990
- **Repository:** NASA NTRS
- **Stable URL:** https://ntrs.nasa.gov/citations/19910004091
- **Distribution:** Unclassified-Unlimited
- **PDF page count:** 128 per NASA report-documentation metadata
- **Configuration:** source-defined HARV wind-tunnel-model aerodynamic representation
- **Classification:** `PRIMARY_EXACT` for its wind-tunnel analytical model; `PRIMARY_COMPATIBLE`/`PRIMARY_FAMILY_ONLY` for Phase-I physical aircraft pending exact test-model mapping

The report derives a six-degree-of-freedom analytical aerodynamic model from HARV wind-tunnel data and explicitly provides:

- nonlinear coefficient functions of angle of attack;
- interpolation between parameterized nonlinear functions;
- unsteady longitudinal terms involving angle-of-attack rate;
- Mach range **0.3–0.9**;
- nominal center around Mach **0.6**, altitude **15,000 ft**;
- maneuver-level simulation comparisons against the original wind-tunnel model.

Reported validity assessment:

- good overall representation at Mach 0.6;
- longitudinal model good over Mach 0.3–0.9;
- lateral model good over Mach 0.6–0.9.

This source is important because it offers a public compact analytic aerodynamic representation rather than only derivative plots. It may allow a bounded high-alpha research model without possession of the proprietary MDC lookup package.

It does **not** by itself establish exact Phase-I full-scale equivalence. A future numeric-transcription phase must map its wind-tunnel test article, reference point, control settings, and coefficient definitions before promotion.

## 7. Additional public flight-identification sources

### FA18-M2-SRC-003 — NASA CR-194838

- **Title:** *Determination of the Stability and Control Derivatives of the NASA F/A-18 HARV Using Flight Data*
- **Report:** NASA-CR-194838 / NTRS 19940020331
- **Authors:** Marcello R. Napolitano; Joelle M. Spagnuolo
- **Organization:** West Virginia University under NASA cooperative agreement NCC2-759
- **Publication date:** December 1993
- **Stable URL:** https://ntrs.nasa.gov/citations/19940020331
- **Classification:** `PRIMARY_EXACT` for its analyzed HARV flight state; not Phase-I direct unless maneuver/date configuration proves it

The report estimates a complete set of stability/control derivatives from approximately 10 to 60 deg AOA and explicitly treats thrust-vectoring and independent single-surface excitation effects. The program date places it in the modified-HARV era, so deleting TVC inputs from an analysis does not magically convert the airframe into Phase I.

Use: high-alpha validation and derivative-trend support for later HARV states.

### FA18-M2-SRC-004 — NASA CR-200251

- **Title:** *Estimation of the Longitudinal and Lateral-Directional Aerodynamic Parameters from Flight Data for the NASA F/A-18 HARV*
- **Report:** NASA-CR-200251 / NTRS 19960014815
- **Author:** Marcello R. Napolitano
- **Organization:** West Virginia University / NASA Dryden
- **Publication date:** January 1996
- **Distribution:** Public
- **Stable URL:** https://ntrs.nasa.gov/citations/19960014815
- **Classification:** `PRIMARY_EXACT` for the analyzed HARV program data; configuration must be mapped maneuver-by-maneuver before Phase-I use

The report estimates longitudinal and lateral-directional static/dynamic derivative models over approximately 5–60 deg AOA using multiple-doublet, optimal-input, frequency-sweep, pilot-stick and rudder maneuvers.

## 8. Original contractor source hunt — result

A targeted search was repeated for the following originals:

- MDC A7247 Vol I/II;
- MDC A8575;
- Albion H. Bowers, *HARV Aerodynamic Model*, 22 Jan 1990;
- GE R88AEB427.

### Result

No lawful public primary copy of those contractor/project documents was acquired in this milestone.

Their identity is now highly constrained by public NASA references:

- A7247 Vol I: *F/A-18 Stability and Control Data Report — Low Angle of Attack*, issue 31 Aug 1981, Rev B dated 15 Nov 1982, Contract N00019-75-C-0424;
- A7247 Vol II: *F/A-18 Stability and Control Data Report — High Angle of Attack*, issue 31 Aug 1981;
- A8575: *F/A-18 Basic Aerodynamic Data*, public NASA bibliographies identify 1984/Rev-A lineage;
- GE R88AEB427: *Software User's Manual for the HARV F404-GE-400 Dynamic Real Time Model*.

NASA TM-107601 directly cites A7247/A8575/A7813/A4107 as source lineage; NASA CR-187469 and later NASA sources provide public derivatives/equations derived from related wind-tunnel data.

### Release-status rule

Some NASA bibliographies show a historical `(U)` or unclassified marking for contractor documents. That is **not** treated as proof of public release. Until an authorized public copy is found, each original remains `NOT_ACQUIRED`.

## 9. Updated Phase-I availability matrix

| Quantity | R0 M2 status | Basis |
|---|---|---|
| Reference geometry `S/b/cbar` | **`CLOSED_EXACT`** | NASA/TP-1999-206573 Table 3, Unmodified Phase I |
| One exact mass state | **`CLOSED_EXACT`** | same table/footnote |
| CG | **`CLOSED_EXACT`** | 21.9% MAC + FS/WL coordinates |
| Inertia | **`CLOSED_EXACT`** | `Ixx/Iyy/Izz/Ixz`, source sign preserved |
| Static aero coefficient deck | `PARTIAL` | public wind-tunnel/analytic model exists; exact Phase-I full deck not transcribed/mapped |
| Dynamic derivatives | `CLOSED_EXACT` for bounded Phase-I flight-ID subset; `PARTIAL` globally | TM-4786 + early HATP flight ID |
| Control derivatives | same | bounded flight-derived evidence |
| Mach range | `PARTIAL` | source-specific ranges known; no one complete Phase-I nonlinear-deck validity surface |
| Alpha range | `CLOSED_EXACT` for individual sources; `PARTIAL` globally | TM-4786, CR-187469, other HATP sources |
| Beta range | `PARTIAL` | simulation/wind-tunnel ranges available, exact target grid still source-specific |
| Conventional control hard limits/rates | **`CLOSED_COMPATIBLE`** with explicit basic-F-18 equality | NASA/TP-1999-206573 Table 1 |
| Actuator transfer dynamics/load effects | `PARTIAL` | `f18bas` and later HARV models available, exact Phase-I mapping incomplete |
| FCS architecture | **`CLOSED_COMPATIBLE`** | standard V10.1/basic-F-18 architecture + NASA source lineage |
| Complete scheduled FCS gains | `PARTIAL` | substantial public reproduction, exact flight software equivalence incomplete |
| Propulsion architecture/model lineage | `PARTIAL` | F404-GE-400 + NASA TM-4240 |
| Phase-I thrust deck | **`OPEN`** | GE original numeric deck not publicly acquired; later HARV model has research-specific installation effects |
| TVC | **`CLOSED_EXACT: ABSENT`** | Phase-I configuration boundary |
| Flight-test validation | **`CLOSED_EXACT`** for bounded campaigns | Phase-I flight-identification program |

## 10. What is implementable next without guessing?

A defensible **Phase-I physical reference profile and bounded validation model** can now be implemented without inventing:

- `S`, `b`, `cbar`;
- mass/CG/inertia for the 60%-fuel clean gear-up anchor state;
- conventional control hard limits/rates;
- absence of TVC/ANSER;
- exact target identity and configuration phase;
- bounded flight-derived lateral-directional derivative validation vectors.

A full nonlinear aircraft implementation is still premature if it claims exact Phase-I aerodynamics or powered performance, because two core datasets remain missing:

1. one complete Phase-I-compatible nonlinear coefficient model/deck with reference/datum/validity mapping;
2. one Phase-I-compatible F404-GE-400 Mach/altitude/PLA thrust/install/transient deck.

## 11. Readiness verdict

**F/A-18 overall: `PARTIAL — MORE RESEARCH`.**

However, the narrower milestone is now:

**REFERENCE PROFILE / UNPOWERED BOUNDED VALIDATION: GO.**

This is a meaningful change from R0/M1. Geometry and the main physical-state anchors are no longer the blockers.

The fastest path to full-aircraft `GO` is no longer generic source discovery. It is targeted acquisition/transcription of:

1. the NASA CR-187469 public analytic aero equations and exact model/configuration mapping;
2. A7247/A8575 or an authorized NASA reproduction of the underlying coefficient arrays;
3. the Phase-I-compatible F404 performance/install deck.
