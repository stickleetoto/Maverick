# F/A-18 Public Source Acquisition — Deep Dive R0 M1

Status: **RESEARCH MILESTONE — no implementation or numeric freeze**

This milestone extends the R0 source-acquisition work for the F/A-18 family. It does not replace the existing source catalog, compatibility matrix, or data-gap map. It records newly recovered primary-source detail and narrows the remaining acquisition targets.

Recommended physical reference candidate remains:

`NASA_F18_HARV_160780_PHASE1_BASIC`

Strongest public simulation corpus remains:

`NASA_F18BAS_BASIC_F18_OFP_8_3_3_TVC_OFF_CAPABLE`

The two configurations remain separate.

## 1. Major result

The public NASA simulation lineage contains substantially more implementation-grade structure than the initial R0 pass established.

NASA TM-107601 (`f18bas`) directly documents:

- nonlinear six-degree-of-freedom rigid-body simulation architecture;
- aerodynamic reconstruction/database structure;
- reference geometry;
- simulation weight/CG/inertia states;
- production-PROM-derived OFP 8.3.3 inner-loop control-law architecture;
- actuator rate limits, position limits and first-order dynamics;
- engine-model lineage;
- explicit source chain to McDonnell Douglas aerodynamic/control reports.

NASA TM-110216 (`f18harv`) then documents the modified HARV simulation with:

- exact HARV simulation weight/CG/inertia states;
- Mach 0–2, alpha -10 to +90 deg, beta -20 to +20 deg modeled envelope;
- body/control sign conventions;
- HARV-specific aerodynamic increments added to the basic F/A-18 model;
- upgraded higher-fidelity actuator models;
- F404-GE-400 engine model integration;
- TVC and ANSER research-control states.

These facts do **not** prove that `f18bas` is identical to BuNo 160780 Phase I, and do **not** authorize back-porting Phase-II/III HARV actuator or propulsion data to Phase I.

## 2. Newly strengthened primary sources

### FA18-DEEP-SRC-001 — NASA TM-107601 (`f18bas`) numeric reference structure

- **Title:** *Simulation Model of a Twin-Tail, High Performance Airplane*
- **Report:** NASA-TM-107601 / NTRS 19920024293
- **Authors:** Carey S. Buttrill; P. Douglas Arbuckle; Keith D. Hoffler
- **Organization:** NASA Langley Research Center / ViGYAN
- **Publication date:** July 1992
- **Repository:** NASA NTRS
- **Stable URL:** https://ntrs.nasa.gov/citations/19920024293
- **Configuration:** source-defined basic F/A-18 simulation; OFP 8.3.3 inner loop; hypothetical TVC option present but disable-able with `LTHVEC=false`
- **Classification:** `PRIMARY_EXACT` for `f18bas`; `PRIMARY_FAMILY_ONLY` for a physical fleet/HARV airframe

Primary searchable report text exposes the following tables/anchors:

- Table 3.4 — aerodynamic reference dimensions;
- Table 3.5 — simulation weight/CG/inertia;
- Tables 8.6 and 8.7 — actuator nonlinearities / hardware-integration-simulator actuator model;
- section 9 — inner-loop FCS implementation and source lineage.

Candidate table transcriptions recovered from the primary searchable report text include:

| Field | Candidate transcription | Authority state in this milestone |
|---|---:|---|
| reference wing area | 400 ft^2 | `PRIMARY_EXACT SOURCE TEXT — VISUAL PAGE CHECK PENDING`; not frozen |
| mean aerodynamic chord | 138.275 in (11.523 ft) | same |
| leading edge of MAC | FS 423.99 in | same |
| reference span | 37.42 ft | same |
| baseline-simulation weight state | 31,665 lb | same |
| baseline-simulation CG | FS 457.3 in; WL 101.6 in | same |
| baseline `Ixx` | 22,337 slug-ft^2 | same |
| baseline `Iyy` | 120,293 slug-ft^2 | same |
| baseline `Izz` | 138,945 slug-ft^2 | same |
| baseline `Ixz` | -2,430 slug-ft^2 | same; preserve source sign |

The report explicitly discusses the product-of-inertia sign convention. These values are not promoted to `NASA_F18_HARV_160780_PHASE1_BASIC` merely because the aircraft family and geometry appear consistent.

### FA18-DEEP-SRC-002 — `f18bas` actuator tables

NASA TM-107601 Table 8.7 documents the F/A-18 Flight Hardware Integration Simulator actuator set used by `f18bas`:

| Effector | Rate limit | Position limit | Mechanization |
|---|---:|---:|---|
| stabilator | +/-40 deg/s | -24 to +10.5 deg | first-order lag, tau=1/30 s |
| rudder | +/-61 deg/s | -30 to +30 deg | first-order lag, tau=1/40 s |
| aileron | +/-100 deg/s | -25 to +45 deg | first-order lag, tau=1/48 s |
| trailing-edge flap | +/-18 deg/s | -8 to +45 deg | first-order lag, tau=1/20 s |
| leading-edge flap | +/-18 deg/s | -3 to +34 deg | first-order lag, tau=1/20 s |

**Important conflict record:** Table 8.6 reproduces an MDC A7813 rudder no-load rate limit of 56 deg/s, while the MDC/DMS/f18bas simulation uses 61 deg/s and Table 8.7 cites the Flight Hardware Integration Simulator value of 61 deg/s. This is not an error to average away. The source provenance differs and must be resolved at configuration/OFP/hardware level before a physical-aircraft freeze.

Classification:

- `PRIMARY_EXACT` for `f18bas` simulation actuator behavior;
- `PRIMARY_FAMILY_ONLY` / `COMPATIBLE_SUPPORT` candidate for Phase-I HARV pending exact hardware mapping;
- not direct authority for later `f18harv`, which replaces these with higher-fidelity actuator dynamics.

### FA18-DEEP-SRC-003 — NASA TM-110216 HARV simulation mass and actuator model

- **Title:** *Simulation Model of the F/A-18 High Angle-of-Attack Research Vehicle Utilized for the Design of Advanced Control Laws*
- **Report:** NASA-TM-110216 / NTRS 19960027892
- **Publication date:** May 1996
- **Repository:** NASA NTRS
- **Stable URL:** https://ntrs.nasa.gov/citations/19960027892
- **Configuration:** modified HARV simulation supporting TVC/ANSER research
- **Classification:** `PRIMARY_EXACT` for `f18harv`; incompatible as blanket Phase-I authority

The report's section 2.3 / Table 2.3 supplies four HARV simulation weight-and-balance states. Searchable primary text yields a Fighter Escort / 60%-internal-fuel state around 35,764.6 lb with FS/WL CG and full `Ixx/Iyy/Izz/Ixz`, plus Light, Heavy and Empty states. These are valuable because they establish an internally coherent full inertia schedule for the HARV simulation.

**Freeze rule:** no Table 2.3 number is promoted to Phase-I until the table is visually transcribed from an original PDF page and the phase/hardware state is matched. The modified HARV weight-balance state includes research hardware and ballast decisions.

Section 6 replaces the simpler `f18bas` actuator model with Dryden/HARV higher-fidelity second-order dynamics and load-sensitive behavior. The report gives actuator transfer functions and later HARV rate/position limits. These are direct to the modified HARV simulation only.

### FA18-DEEP-SRC-004 — NASA TM-4240 F404-GE-400 dynamic engine model

- **Title:** *A Simple Dynamic Engine Model for Use in a Real-Time Aircraft Simulation With Thrust Vectoring*
- **Report:** NASA-TM-4240 / H-1643 / AIAA 90-2166 / NTRS 19910009766
- **Author:** Steven A. Johnson
- **Organization:** NASA Dryden Flight Research Facility
- **Publication date:** October 1990
- **Repository:** NASA NTRS
- **Stable URL:** https://ntrs.nasa.gov/citations/19910009766
- **Distribution:** Public; U.S. Government work/public use permitted per NTRS metadata
- **Configuration:** F-18 HARV F404-GE-400 thrust-vectoring simulation lineage
- **Classification:** `PRIMARY_EXACT` for the documented HARV engine simulation; `PRIMARY_FAMILY_ONLY` for a generic/Phase-I F404 installation

The model was generated from tabular output of a manufacturer nonlinear component-level F404-GE-400 model. It includes:

- gross thrust;
- ram drag;
- nozzle pressure ratio;
- nozzle throat area;
- Mach / altitude / PLA dependence;
- inlet-spillage and nozzle/aft-end installation effects;
- throttle-rate limiter and low-pass-filter dynamics;
- axial thrust loss due to vectoring.

The paper reports evaluation at Mach 0.2 and 0.7, both at 35,000 ft, and states agreement within 3% steady-state and 25% transient response relative to the full nonlinear model for the conditions studied.

The upstream manufacturer source is explicitly identified as:

**General Electric Report R88AEB427 — *Software User's Manual for the HARV F404-GE-400 Dynamic Real Time Model*.**

This report was not acquired as a lawful public primary copy in this milestone and remains `NOT_ACQUIRED`.

The airframe installation-effects model is also not separately acquired. TM-4240 states that inlet drag data were derived from force/moment tests of a 6%-scale production F-18 model and that HARV nozzle modifications create configuration-specific aft-end increments.

### FA18-DEEP-SRC-005 — NASA CR-201687 HARV spin-model mass cross-check

- **Title:** *Spin-Tunnel Investigation of a 1/28-Scale Model of the NASA F-18 High Alpha Research Vehicle (HARV) With and Without Vertical Tails*
- **Report:** NASA-CR-201687 / NTRS 19970019598
- **Author:** C. Michael Fremaux
- **Organization:** Lockheed Martin Engineering and Sciences Co. for NASA Langley
- **Publication date:** April 1997
- **Stable URL:** https://ntrs.nasa.gov/citations/19970019598
- **Classification:** `PRIMARY_EXACT` for the dynamically scaled spin-test configuration; `PRIMARY_COMPATIBLE` as an independent mass-property cross-check for the corresponding HARV state

The spin model was ballasted to dynamically represent the full-scale HARV. Searchable report data give a full-scale weight/inertia state closely matching the TM-110216 Fighter Escort state. This materially strengthens confidence that the HARV simulation mass/inertia values are not arbitrary simulation-only placeholders.

It does **not** prove equivalence to the pre-TVC Phase-I flight-11–38 state.

### FA18-DEEP-SRC-006 — NASA TM-4440 independent high-fidelity simulation lineage

- **Title:** *A High-Fidelity, Six-Degree-of-Freedom Batch Simulation Environment for Tactical Guidance Research and Evaluation*
- **Report:** NASA-TM-4440 / L-17096 / NTRS 19930023191
- **Author:** Kenneth H. Goodrich
- **Organization:** NASA Langley Research Center
- **Publication date:** July 1993
- **Stable URL:** https://ntrs.nasa.gov/citations/19930023191
- **Distribution:** Public / unclassified-unlimited in public report metadata
- **Classification:** `PRIMARY_EXACT` for the Tactical Maneuvering Simulator aircraft models; `PRIMARY_FAMILY_ONLY` for a physical F/A-18

TM-4440 describes high-fidelity baseline and thrust-vectoring aircraft databases equivalent to piloted-simulation models and independently exposes the same general `f18bas` weight/CG/inertia lineage. It is useful as a provenance cross-check and simulation-validation source, not as a substitute for the original aerodynamic data package.

## 3. Original McDonnell Douglas source graph — now more precise

The NASA reports expose a coherent contractor-document chain:

| Identifier | Title / role | Current acquisition status |
|---|---|---|
| MDC A7247 Vol I | *F/A-18 Stability and Control Data Report — Low Angle of Attack* | `NOT_ACQUIRED`; repeatedly cited by NASA primary reports |
| MDC A7247 Vol II | *F/A-18 Stability and Control Data Report — High Angle of Attack* | `NOT_ACQUIRED` |
| MDC A8575 | *F/A-18 Basic Aerodynamic Data* | `NOT_ACQUIRED` |
| MDC A7813 Vol I | *F/A-18A Flight Control System Design Report — System Description and Theory of Operation* | `NOT_ACQUIRED` |
| MDC A7813 Vol II | FCS analysis / inner loops | `NOT_ACQUIRED` |
| MDC A7813 Vol III | FCS analysis / automatic flight modes | `NOT_ACQUIRED` |
| MDC A4107 | *F/A-18 Flight Control Electronic Set Control Laws* | `NOT_ACQUIRED` |
| MDC A8449 | F/A-18 Flight Hardware Integration Simulator specification used by TM-107601 actuator Table 8.7 | `NOT_ACQUIRED` |
| MDC A7248 | *F/A-18 Flight Control Description Report* | `NOT_ACQUIRED` |
| MDC A7249 | *F/A-18 Flying Qualities Report* | `NOT_ACQUIRED` |
| GE R88AEB427 | *Software User's Manual for the HARV F404-GE-400 Dynamic Real Time Model* | `NOT_ACQUIRED` |

The fact that a report title is identified or marked unclassified in a citation does **not** establish public-release authority. No unauthorized copy is accepted.

## 4. Compatibility consequences

### `f18bas` is now much closer to an implementable source-defined reference

For the simulation-defined configuration, public NASA evidence now closes or nearly closes:

- reference geometry;
- one or more mass/CG/inertia states;
- axis/coefficient structure;
- surface identities;
- actuator hard limits/rates/first-order dynamics;
- OFP 8.3.3 inner-loop architecture;
- engine-model architecture;
- broad aerodynamic alpha/beta envelope.

The major missing item remains the **actual numeric aerodynamic lookup arrays/data tables** and their original configuration provenance.

### Phase-I HARV remains the stronger physical-airframe target

The exact physical target still has superior configuration identity and direct flight-derived derivative validation. However, the implementation gap remains larger because direct proof that `f18bas` geometry/actuator/FCS/mass tables equal the Phase-I BuNo 160780 state has not been recovered.

### Phase-II/III HARV is richer numerically but not cleaner

The later modified HARV simulation provides more complete mass, actuator, engine/TVC and high-alpha model detail, but the modifications are precisely what prevent those values from being silently used as basic-Hornet authority.

## 5. Updated data-gap assessment

| Quantity | `f18bas` source-defined configuration | Phase-I HARV physical target |
|---|---|---|
| Reference geometry | `CLOSED_EXACT` at source-text level; visual table verification still required before freeze | `CLOSED_COMPATIBLE` candidate; exact physical-target mapping still required |
| Mass / CG / inertia | `CLOSED_EXACT` for published simulation states | `PARTIAL`; exact Phase-I W&B table still needed |
| Static coefficient database | `PARTIAL`; structure/source lineage documented, arrays not acquired | `PARTIAL`; flight/tunnel evidence exists, complete arrays not acquired |
| Dynamic/control derivatives | `PARTIAL` | `CLOSED_EXACT` for bounded flight-ID subsets |
| Control limits | `CLOSED_EXACT` for simulation actuator model | `PARTIAL` / compatibility proof needed |
| Actuator dynamics | `CLOSED_EXACT` for simplified simulation model | `PARTIAL`; later HARV model not automatically Phase-I |
| Flight-control law | `PARTIAL` — OFP 8.3.3 inner-loop architecture is public | `PARTIAL`; exact Phase-I OFP/software mapping needed |
| Propulsion model | `PARTIAL` | `PARTIAL`; TM-4240 is HARV/TVC lineage, not automatically Phase-I |
| Full thrust deck | `OPEN` | `OPEN` |
| Flight-test validation | `SUPPORTED_ONLY` against physical-family data | `CLOSED_EXACT` for bounded Phase-I campaigns |

## 6. Highest-value next acquisitions

1. Public lawful copy of **MDC A7247 Vol I/II** or a NASA release reproducing its numeric lookup arrays.
2. Public lawful copy of **MDC A8575 — F/A-18 Basic Aerodynamic Data**.
3. **Albion Bowers, HARV Aerodynamic Model, 22 Jan 1990** project memo.
4. **MDC A7813 / A4107 / A8449** originals sufficient to resolve actuator/FCS configuration mapping and the 56-vs-61 deg/s rudder-rate discrepancy.
5. **GE R88AEB427** or another public primary release containing the actual HARV F404-GE-400 Mach/altitude/PLA numeric tables.

## 7. Readiness change

Previous verdict: `PARTIAL — MORE RESEARCH`.

Current verdict: **`PARTIAL — MORE RESEARCH`, materially closer to GO.**

A defensible implementation of the **simulation-defined `f18bas` reference** is now plausible sooner than an exact physical Phase-I HARV full model, provided the missing aerodynamic arrays can be lawfully acquired or reconstructed only from primary released tables.

The recommended physical reference candidate nevertheless remains `NASA_F18_HARV_160780_PHASE1_BASIC` because physical flight-test provenance is stronger and the project should not silently redefine a simulator configuration as a real production/flight vehicle.