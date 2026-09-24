# F/A-18 Source Graph (r0, R1 addendum)

> **R1 note.** This page is the r0 text plus an R1 addendum at the end. r0 level names map to R1 names as L0 = `SEARCH_LEAD_ONLY`, L1 = `CATALOGUE_VERIFIED`, L2 = `ABSTRACT_VERIFIED`, L3 = `CONTENT_EXTRACT_VERIFIED`, L4/L5 = `PAGE_VERIFIED` ([`SCHEMA.md` §2](../../SCHEMA.md#2-verification-levels)).


Status: **lineage reconstructed from catalogue records, abstracts and search extracts.** No PDF bibliography has been read yet (see [library README §4](../../README.md#4-retrieval-limitation-read-this-before-trusting-anything)). Edges marked *(probable)* must be confirmed from reference lists in FA18-R1.

Source IDs refer to [`SOURCE_INDEX.json`](../../SOURCE_INDEX.json). Configuration IDs refer to [`KNOWN_DATA.md`](KNOWN_DATA.md#1-configuration-registry).

---

## 1. Configuration timeline (the spine of the graph)

```
 McDonnell Douglas F-18 / F/A-18 programme
 ├─ production F/A-18A/B (FA18-CFG-PROD-AB)
 │    production FCS + manufacturer aero data: NOT PUBLIC / NOT LOCATED
 │
 ├─ F-18 FSD #6, BuNo 160780 (pre-production; built before the F/A-18 redesignation;
 │  borrowed from the Navy because it had a spin chute)
 │   │
 │   NASA 840 ── HARV Phase 1 BASIC ──▶ Phase 2 TV + RFCS ──▶ Phase 3 TV + ANSER ──▶ programme end Sep 1996
 │               FA18-CFG-HARV-P1-BASIC  FA18-CFG-HARV-P2-TV   FA18-CFG-HARV-P3-ANSER
 │               from Apr 1987           (dates unverified)    flight data Jul 1995–May 1996
 │                                                                        │
 │                                                wings (thinned skins) ──┘
 │                                                                        ▼
 ├─ Navy F/A-18A airframe 853 ── NASA 853 + 840 wings ──▶ AAW / X-53 (2003, 2005) ──▶ FAST (c. 2011)
 │                                FA18-CFG-AAW-853                                  FA18-CFG-FAST-853
 │
 └─ pre-production two-seat F/A-18B ── NASA 845 Systems Research Aircraft (from May 1993)
                                       FA18-CFG-SRA-845 (actuator/systems testbed only)
```

## 2. Simulation / aerodynamic-database lineage

```
[Manufacturer + NASA tunnel data]  MCAIR-FA18-AERO-DATABASE  (NOT PUBLICLY LOCATED; upstream identity unconfirmed)
        │ DERIVED_FROM (probable)
        ▼
NASA-TM-107601  'f18bas' (1992, Langley)  ── basic F/A-18 + PRELIMINARY HARV TV model
        │                                     ACSL; α −10..+90°, β −20..+20°; table look-up, linear interpolation;
        │                                     first-order actuators with rate + position limits; engine model; sensors
        │ outgrowth (stated in TM-110216 abstract/extract)
        ▼
NASA-TM-110216  'f18harv' (1996, Langley) ── HARV with multi-axis TV + actuated forebody strakes
        │   ▲  ▲                              DB domain per extract: α −10..+90°, β −20..+20°, M 0..2.0
        │   │  └── NASA-CR-198247 strake increments VALIDATED_AGAINST HARV flight data
        │   └───── HARV flight data used to update aero tables (search extract; scope unverified)
        │   ◀───── NASA-TM-4240 simple F404 dynamic engine model (probable integration path)
        │   ◀───── NASA-TP-3531 / NTRS-19920066113 TV effectiveness tunnel data (probable)
        │   ◀───── JAIRCRAFT-1995-MURRI-STRAKES strake tunnel data (probable)
        ├── EXTENDED_BY NASA-CR-1998-206937 (turbulence models)
        ├── USED_BY NASA-TP-3446 (longitudinal law), NASA-TP-1998-208465 (lat-dir law), NASA-TM-110217 (ANSER spec)
        ├── CURVE_FIT_OF (probable) JAIRCRAFT-1995-MORELLI-MOF / NTRS-19940020628 (subsonic tabular DB → MOF polynomials)
        └── DERIVED_FROM (probable) JGCD-2011-CHAKRABORTY-LINEAR / JGCD-2011-CHAKRABORTY-NONLINEAR ("HARV aerodynamic data in the open literature")
```

**Key reading:** f18harv is *not* a clean baseline F/A-18 model. It is f18bas plus TV, plus strakes, plus (per extract) flight-data table updates. A baseline F/A-18 model from this lineage has to be built from f18bas-origin tables with TV and strake increments **absent** (not zeroed after the fact), and it still carries the HARV-specific table updates. Whether that separation is possible depends on how TM-110216 structures its build-up. This is open question Q-A2 in [`MISSING_DATA.md`](MISSING_DATA.md).

## 3. Flight-control lineage

```
Basic F/A-18 FCS (701E FCCs; V8.3.3 → V10.1 on HARV per VDD; "HARV 1.2" primary law)  — gains NOT PUBLIC
        │ RFCS "piggybacked" (Pace 1750A, Ada)
        ▼
NASA-TM-104232 (RFCS development, 1991) ── NASA-TM-104253 (testbed, 1992) ── NTRS-19900019226 (TVCS V&V)
        │
        ├── NASA-TP-3446  NASA Langley longitudinal controller (1994)  ─┐
        ├── NASA-TP-1998-208465 lateral-directional law (CRAFT + Pseudo Controls) ─┤ BUILDS_ON
        │      └── USES_METHOD NASA-TP-1998-208463 (CRAFT)              │
        ├── NASA-TM-110228 TV mixer (vane allocation by inversion of TV effectiveness data)
        ▼                                                               ▼
NASA-TM-110217  ANSER control law design specification (code-level) ◀──┘
        │ implemented in Ada at Dryden
        ▼
NASA-CR-198250  v152.0 Ada vs Langley batch-Fortran "truth model" (HIL)   (+ NTRS-19960000845 earlier validation)
        │ flight test Jul 1995–May 1996
        ▼
NTRS-19970014822 flight data retrieval ── NTRS-19980200994 closed-loop sys-ID ── NASA-TM-4773 HQ flight research
NTRS-19990064010 controls & flying-qualities overview

Separate branches (other airframes / eras):
  NTRS-19970041277, NTRS-19990060322  PSFCC research capability (production-family FCC + research processor)
  NASA-TM-2005-213666                 AAW research laws (853)
  NTRS-20110015950                    FAST NDI baseline law (853)
  AIAA-2004-542 (Boeing/NAVAIR) ──▶ JGCD-2011-CHAKRABORTY-LINEAR / -NONLINEAR (university reconstruction of baseline vs revised laws)
```

## 4. Flight-derived aerodynamics lineage (validation branch)

```
NASA 840 flight data
 ├── Phase 1 BASIC ──▶ NASA-TM-4786 Iliff & Wang lat-dir derivatives, α 3–47° (MMLE with state noise), vs wind tunnel
 ├── Phase 2 TV ────▶ NASA-TP-97-206539 Iliff & Wang longitudinal derivatives (+ TV effectiveness, plume interference)
 │                ──▶ NASA-TP-1999-206573 Iliff & Wang lat-dir derivatives, compared to basic F-18 and predicted
 │                ──▶ NASA-CR-194838 Napolitano & Spagnuolo (pEst output error, α 10–60°)    ┐ INDEPENDENT_ESTIMATE_
 │                ──▶ NASA-CR-191216, NASA-CR-200251, AIAA-96-3419, WVU-ETD-9553 (WVU line) ┘ SAME_AIRFRAME
 │                ──▶ NASA-CR-198248 Morelli optimal-input maneuvers + lateral control effectiveness
 │                ──▶ NTRS-20040087105 Morelli real-time frequency-domain estimation (method demo)
 └── Phase 3 ANSER ─▶ NASA-CR-198247 strake increments; NTRS-19980200994 closed-loop models

Methods behind these: NASA-RP-1168 (output error), NTRS-19850011474 (identification theory).
```

**Independence note:** Dryden (Iliff & Wang) and WVU (Napolitano) analysed the **same airframe** with different methods. Agreement between them is a real, method-level cross-check, but not an independent measurement of the aircraft. Both are independent of the tunnel-derived simulation database, so flight-vs-f18harv comparisons are genuine validation.

## 5. Wind-tunnel and ground-to-flight branch

```
0.06-scale F/A-18 (DTRC 7x10 transonic): NASA-TP-3111 ← AIAA-89-2222            [TUNNEL, low Re]
Sub-scale F/A-18 forebody vortex control (Eidetics): NASA-CR-4582 vol 1 static / vol 2 rotary
0.10-scale jet-effects model of an F-18 PROTOTYPE + HARV vanes (LaRC 16-ft): NASA-TP-3531
Full-scale F/A-18 (Ames 80x120) + LaRC 30x60 + flight: RTO-MP-069-P45 (forebody aerodynamics)
Strake development (static, dynamic, transonic, full-scale): JAIRCRAFT-1995-MURRI-STRAKES
                     └──────────────────────┬──────────────────────┘
                                            ▼
            NASA-TM-4783 ground-to-flight correlation lessons (HARV, X-29, X-31):
            Reynolds-number effects on forebodies; grit strips improved the forebody pressure match
```

## 6. Propulsion branch

```
GE F404 complete nonlinear dynamic model (NOT PUBLIC) ─▶ NASA-TM-4240 simple model (rate limiter + low-pass;
                                                          TV axial-thrust-loss method; ≤3 % SS / ≤25 % transient)
HARV inlet: NASA-TM-104329 (distortion to α 60°), NTRS-19990024943 (departures, surges)
Engine-level, X-29A installation: NASA-TP-3001, NASA-TM-4140; NASA Lewis PSL: NASA-TM-88273
TV: NASA-TP-3531 (tunnel) ─▶ NASA-TM-4771 (scale model / full-scale / flight comparison) ─▶ NASA-TM-110228 (mixer)
```

## 7. Conflicts found in r0

| # | Quantity | Values | Configurations | Likely reason | Status |
|---|---|---|---|---|---|
| C1 | Rudder rate limit | 82 deg/s (HARV simulation, attributed to TM-110216/CR-198248) vs 56 deg/s (AAW actuator table) | FA18-CFG-SIM-F18HARV vs FA18-CFG-AAW-853 | Configuration/era difference, or loaded-vs-no-load rating. **Not established** | Both kept |
| C2 | HARV Phase 2/3 inertias | 22,789 / 176,809 / 191,744 slug-ft² (TM-4772) vs 22,632 / 174,246.3 / 189,336.4 (+ Ixz −2,131.8) of uncertain attribution | P2-TV (both) | Different loading state or document revision; attribution of the second set is doubtful | Both kept; second set not usable until attributed |
| C3 | Reference span | 37.4 ft (TM-4772 extract) vs 37.42 ft (derived works) vs 37 ft 5 in (fact page, physical) | HARV | Rounding; reference vs physical | Not a physics conflict; printed precision to confirm |
| C4 | NASA-TM-4783 author list | Two catalogue extracts disagree | n/a | Metadata error or conflation with another report | Recorded as UNCONFIRMED |
| C5 | NASA-TP-3111 affiliation | One extract says NASA Lewis | n/a | Probable catalogue error | Not relied on |
| C6 | "Controlled flight to 70°" vs "stabilized flight at 65–70°" vs "controlled to 60°" statements | Various overview extracts | P2/P3 | Different phases/effectors and different definitions of "controlled" | Treat as narrative only; use flight data for envelopes |

---

## 8. R1 addendum: repository acquisitions, metadata fixes and cross-aircraft links

R1 did **not** redo the F/A-18 pass. It added what the repository's F/A-18 consolidation branch (`sol/fa18-source-consolidation-r1` @e596c32) had already retrieved and read, fixed metadata that those readings contradict, and linked the pack to the other aircraft. Each change is recorded in the source's `existing_maverick_analysis` (repository path, branch, locator and the PDF sha256 the repository recorded). The library's own verification levels did not change.

### 8.1 Upstream identities now named

```
MCAIR-FA18-AERO-DATABASE  = MDC A7247 Vol I (low AoA; issue 31 Aug 1981, Rev B 15 Nov 1982) + Vol II (high AoA) + MDC A8575 (basic aero data)
        │  cited by NASA primary reports (repository reading)          ── still NOT PUBLICLY LOCATED
        ▼
NASA-TM-107601 f18bas ── also cites MDC A4107 / A7813 for the control-law lineage; Table 8.6 reproduces the A7813 rudder
                          no-load rate (56 deg/s) while Table 8.7 uses 61 deg/s (repository)
GE-F404-COMPLETE-ENGINE-MODEL = GE R88AEB427 "Software User's Manual for the HARV F404-GE-400 Dynamic Real Time Model"
        │  DERIVED_FROM
        ▼
NASA-TM-4240 (Oct 1990; also AIAA 90-2166; Unclassified-Unlimited per its documentation page)
```

### 8.2 Sources added in R1 (repository acquisitions)

| Source | What it adds | Configuration |
|---|---|---|
| `NASA-CR-3608` | Complete measured rotary-balance tables, 1/10-scale model, component build-up | `FA18-CFG-WT-CR3608` (new) |
| `NASA-TM-4341` | Full-scale ground test of TV-vane axial thrust loss (Tables 2/3) | `FA18-CFG-HARV-P2-TV` |
| `NASA-CR-198052` | Engine airflow estimation with clean and distorted inlet flow | HARV + `ENG-F404-GE-400` |
| `NTRS-19970012895` | Overview of HATP experimental aerodynamics for the baseline F/A-18 (source graph only) | `FA18-CFG-PROD-AB` |
| `NASA-TP-2000-209033` | F-18B SRA parameter estimation at high dynamic pressure, M 0.85-1.30 | `FA18-CFG-SRA-845` |
| `NTRS-20070031030` | Automated simulation updates from flight data (method, HARV demonstration) | `FA18-CFG-SIM-F18HARV` |
| `NASA-TM-104247` | Bleed-air effects on installed F404 thrust, HARV on a thrust stand | HARV + engine |
| `NASA-TM-4591` | Dynamic response of in-flight thrust calculations | engine |

### 8.3 Metadata corrected

| Source | r0 | R1 |
|---|---|---|
| `NASA-TM-110216` | 3 authors | 7 authors (repository catalogue). **Order conflict**: repository catalogue V0.1 lists Strickland first, V0.2 lists Messina first (conflict C7) |
| `NASA-TM-4240` | date unverified | October 1990; also AIAA 90-2166 |
| `NASA-TP-97-206539` | date/organisation unverified | December 1997; NASA Dryden and SPARTA; Phase II, NASA-0 law, mixer 1, LEX fence, flights 1992-1994 |
| `NASA-CR-194838` | "grant period May-Dec 1993" as the phase | December 1993; grant NCC 2-759; data from the 29 Sep 1992 OBES campaign |
| `NASA-TM-4786` | flight dates unverified | Phase I flights 11-38, June 1987 - March 1988 (basic hardware/software) |
| `NASA-TM-107601` | FCS version unverified | Simplified OFP 8.3.3 inner-loop CAS (printed; Q-C1 answered) |
| `NASA-TM-88273`, `GE-F404-COMPLETE-ENGINE-MODEL` | aircraft null | engine-level identity stated explicitly |

### 8.4 Conflicts updated

| # | Update |
|---|---|
| C1 | 82 deg/s rudder rate is printed in f18harv Tables 6.1/6.2 **and** TP-97-206539 Table 1. f18bas uses 61 deg/s. 56 deg/s is both the AAW table value and the MDC A7813 no-load rating reproduced in TM-107601 Table 8.6. Whether the AAW figure is the same rating is not established |
| C2 | The r0 "second inertia set" remains unattributed. Two further sets now exist: TP-97-206539 Table 3 Phase I (incl. Ixz -2,039) and f18bas Table 3.5 Fighter Escort 60 % fuel (incl. Ixz -2,430). Each is a different loading or configuration |
| C7 (new) | TM-110216 author order (see 8.3) |
| C8 (new) | Aileron limits: -25/+45 (f18bas), -25/+42 (f18harv), 24 up / 45 down (TP-97-206539 Table 1). r0's "25 up / 45 down" matches f18bas, so the r0 attribution to HARV sources is withdrawn |
| C9 (new) | Engine PLA rate limits: 19.03/26.81 deg/s (TM-4240) vs 14/22 deg/s (TM-110216), identical time constants. Model-version conflict; never averaged |

### 8.4a R2 status

No F/A-18 primary file was reachable in R2 (NTRS blocked), so nothing was promoted. All F/A-18 conflicts are classified in the library [`CONFLICT_REGISTER.md`](../../CONFLICT_REGISTER.md) (FA18-C1 to C10). Still **UNRESOLVED**: C1 (the AAW 56 deg/s rating), C2 (unattributed inertia set), C4, C5, C7 (metadata).

### 8.5 Cross-aircraft links

| Link | Why it matters |
|---|---|
| Morelli global-model method: `JAIRCRAFT-1995-MORELLI-MOF` (HARV) and `MORELLI-ACC-1998-F16` (F-16) | Same method, different databases. The F-16 fit is Maverick's implemented model; a HARV fit would be the F/A-18 analogue if its coefficients are printed (Q-A3) |
| `NASA-TM-2015-218675` NESC check-cases | Aircraft-independent EOM verification; its F-16 case is textbook lineage (F-16 pack) |
| VISTA NF-16D in-flight simulation | Used for F-22 control-law evaluation (`AIAA-96-3379`); unrelated to F/A-18 data but the same class of evidence as HARV RFCS research laws |
| Iliff output-error lineage (`NASA-RP-1168`) | The same Dryden method produced F/A-18 (TM-4786, TP-97-206539) and F-15 RPV derivatives (TN D-8136) |
| `NASA-SP-2000-4519` (Langley 1990s history) | Covers F/A-18 and F-22 tunnel programmes; history only |
