# Library Source Graph: lineage rules and cross-aircraft map

This file defines how lineage is recorded, and holds the lineage that crosses aircraft (shared engines, shared airframes, shared methods). Each aircraft's own graph is in `Aircraft/<type>/SOURCE_GRAPH.md`: [F/A-18](Aircraft/FA18/SOURCE_GRAPH.md), [F-16](Aircraft/F16/SOURCE_GRAPH.md), [F-15](Aircraft/F15/SOURCE_GRAPH.md), [F-22](Aircraft/F22/SOURCE_GRAPH.md).

The validator checks that every library ID named in a `*SOURCE_GRAPH*` or `*LINEAGE*` document resolves to a source, configuration or value.

---

## 1. Relation vocabulary

These values are used in `related_sources[].relation` in `SOURCE_INDEX.json`.

| Relation | Meaning | Does agreement count as confirmation? |
|---|---|---|
| `SAME_SOURCE` / `SAME_TEST_FAMILY` | Same data, different publication (conference vs TM, volume 1 vs 2) | **No** |
| `DERIVED_FROM` | Built from upstream data, possibly with fits, fixes or increments | **No**. Agreement is expected by construction |
| `UPSTREAM_OF` | Inverse of `DERIVED_FROM` | **No** |
| `REPRODUCED_FROM` / `PUBLIC_REPRODUCTION` | Reprint or transcription | **No** |
| `CURVE_FIT_OF` | Analytic fit to an upstream table | **No**. It only checks the fit |
| `USES` / `USED_BY` / `DESIGNED_ON` | A control law or analysis built on a given model | **No** |
| `VALIDATED_AGAINST` / `VALIDATES` / `VALIDATED_BY` | Compared with an independent measurement (e.g. flight vs simulation) | **Partially.** It confirms within the stated agreement and domain |
| `INDEPENDENT_ESTIMATE_SAME_AIRFRAME` | Different team or method, same test aircraft | **Yes**, for that airframe and phase |
| `INDEPENDENT_SOURCE` | Different data origin entirely | **Yes**, if the configurations match |
| `CONFIGURATION_INCOMPATIBLE` | Same model name, different configuration | **Not comparable** without an explicit delta model |
| `CONSISTENT_WITH` | Numbers agree, but independence is not established | **No** until independence is shown |
| `RELATED`, `COMPANION`, `EXTENDS`, `PRECEDES`, `BUILDS_ON`, `PARALLEL`, `CITES`, `USES_FORMAT`, `USES_METHOD` | Context links | n/a |
| *(not a relation)* `existing_maverick_analysis` | A Maverick document read or cited the source | **No.** The project reading its own sources is not confirmation of anything |

## 2. Repeated-number register

A number that appears in many places is usually one number copied many times. Before treating agreement as confirmation, trace each occurrence to its origin.

| ID | Number | Where it appears | Presumed single origin | Status |
|---|---|---|---|---|
| R0-RN1 | F/A-18 **S = 400 ft²** | NASA-TM-4772 (extract); countless derived F/A-18 works | McDonnell Aircraft reference geometry via NASA HARV documents | Repetition ≠ confirmation. Page-verify one NASA primary |
| R0-RN2 | F/A-18 **c̄ = 11.52 ft** | NASA-TM-4772 (extract); derived works | Same | Same |
| R0-RN3 | F/A-18 **b = 37.42 ft** / **37.4 ft** / **37 ft 5 in** | Derived works print 37.42; the TM-4772 extract shows 37.4; NASA fact page says 37 ft 5 in (physical) | Same reference geometry (reference vs physical and rounding differences) | Record all three. Do not "fix" any of them |
| R0-RN4 | F/A-18 HARV Phase 2/3 **W = 36,099 lbm** and Phase 1 **W = 31,980 lbm** | NASA-TM-4772 (extract) | NASA Dryden HARV mass-properties data | Difference 4,119 lb matches the HARV VDD web page. Both are NASA HARV program documents, so this shows **internal consistency, not independence** |
| R1-RN1 | F-16 **S = 300 ft², c̄ = 11.32 ft, b = 30 ft** | TP-1538 Table I, Stevens & Lewis, Morelli 1998, Garza & Morelli, NESC check-case, simupy-flight, AeroBench, academic codes | TP-1538 (simulation configuration) | **Traced (R1).** Every public occurrence descends from TP-1538. Repository constants: `MavF16MorelliReference` (27.870912 m² / 3.450336 m / 9.144 m). Library: `F16-GEO-*`, blocked pending page verification |
| R1-RN2 | F-15 **608 ft² / 15.94 ft / 42.8 ft** | NF-15B 837 (TM-2003-212027: 608 / 15.94 / **42.7** reference span), AFIT theses (Baumann, Davison, …), CR-186019 (608.0 / **15.95** / 42.8) | McDonnell family reference data (MDC A4172 lineage, not public) | **Traced (R1).** Not an NASA 836 source. 42.8 ft is also 836's *physical* span (R1-RN7), which makes the triple look like an 836 value when it is not |
| R1-RN3 | F-16 actuator **60 / 80 / 120 deg/s** and **0.0495 s** | arXiv 1907.11913 extract, textbook-derived codes | Stevens & Lewis textbook | Upstream primary source **not identified**. TP-1538 material has no sourced rate (repository). Cross-validation only |
| R1-RN4 | **91,188 N** | TP-1538 Table I (F-16 simulation weight, 20,500 lb) and NASA F-15B reports' "approximately 25,000 lbf (91,188 N)" | Unknown for the F-15 sentence | **Observation only.** 91,188 N = 20,500 lbf exactly; 25,000 lbf = 111,206 N. The F-15 sentence is internally inconsistent. No inference is drawn about how the number got there |
| R1-RN5 | **25,000 lbf ≈ 111.2 kN** | F-15 report sentence (25,000 lbf); TP-1373 gross-thrust axis scale (111.2 kN) | Unrelated quantities | Coincidence recorded so nobody treats one as confirming the other (implementation-branch F100 audit §6e) |
| R1-RN6 | F/A-18 Phase I set (400 ft², 11.52 ft, 37.4 ft, 31,980 lb, 21.9 % MAC, 22,040 / 124,554 / 139,382 slug-ft²) | r0 extract attributed to TM-4772; repository transcription of TP-97-206539 Table 3 | NASA Dryden HARV programme mass-properties data | Two NASA reports from the same programme. **Consistency, not independence** |
| R1-RN7 | F-15B 836 **42.8 ft** physical span | TM-4782, TM-2005-213670, TM-2006-213674, TM-2006-213675, AIAA 2001-3303 (repository) | NASA Dryden aircraft description | Same organisation describing the same aircraft; consistent, not independent. Physical, never a reference span |
| R2-RN9 | F-16 thrust table (idle / MIL / MAX, 90 cells at Mach 0.2-1.0) | Repository visual transcription of TP-1538 Table VI; NESC `F16_prop.dml` (NASA digital file, Stevens & Lewis 2003 lineage) | TP-1538 Table VI | **Two independent digitisations of the same table agree in 88 of 90 cells (R2).** That confirms the transcription, not the aircraft. The two differing cells are conflict F16-C6 |
| R2-RN10 | F-16 mass/inertia/geometry set (20,500 lb; 9,496 / 55,814 / 63,100 / 982; 300 / 30 / 11.32; 35 % MRC) | TP-1538 Table I (repository); NESC `F16_inertia.dml` and `F16_aero.dml` (file-verified) | TP-1538 via Stevens & Lewis | Same numbers, same lineage: **consistent, not independent** |
| R1-RN8 | F100-PW-100 thrust figures (23,500 / 24,000 / 25,000 lbf) for tail 836 | TM-2005-213670 / 2016 briefing / TM-2001-210395 | Different statements, different epochs (24,000 is the post-2014 PW-220E) | Not three measurements of one quantity. Configuration date travels with each |

## 3. Cross-aircraft lineage

```
GE F404-GE-400 engine (manufacturer "complete nonlinear dynamic model": NOT PUBLIC)
 ├─ DERIVED_FROM ─▶ NASA-TM-4240 simple dynamic engine model ─▶ used in F-18 HARV TV simulation
 ├─ NASA Lewis PSL altitude tests ─▶ NASA-TM-88273 (exhaust survey)
 └─ X-29A single-engine installation (CONFIGURATION_INCOMPATIBLE with F/A-18 installed thrust)
       ├─ NASA-TP-3001 (in-flight thrust techniques; uninstalled gross thrust accuracy 1–4 %)
       └─ NASA-TM-4140 (measurement effects on in-flight thrust)
   ⇒ Engine-level (uninstalled) facts may transfer between F404-GE-400 installations.
     Installed thrust, inlet and nozzle effects do NOT transfer.

NASA 840 airframe (F-18 FSD #6, BuNo 160780)
 ├─ HARV Phase 1 (basic) ─▶ Phase 2 (TV) ─▶ Phase 3 (ANSER)       [same serial, three configurations]
 └─ its wings, modified with thinner skins ─▶ NASA 853 AAW / X-53  [serial identity ≠ configuration identity]

Morelli global-model methodology
 ├─ F-16: Morelli 1998 polynomial model (implementation authority for Maverick F-16; Docs/Reference)
 └─ F/A-18 HARV: JAIRCRAFT-1995-MORELLI-MOF, NTRS-19940020628 (multivariate orthogonal functions on the HARV tunnel database)
   ⇒ Same method on a different aircraft database. Whether HARV model coefficients are printed is an open R2 question.

NESC 6-DOF check-cases (NASA-TM-2015-218675)
 └─ aircraft-independent EOM/atmosphere verification. Per its abstract the suite uses
    increasingly complex example vehicles; applicability to the Maverick F-16 is to be
    confirmed when the report is opened.
```

### R1 additions to the cross-aircraft graph

```
F100 engine family (Pratt & Whitney)  -- shared by F-15 and F-16, NEVER merged across variants or builds
 ├─ F100-PW-100 builds (1) / (2) / (2-7/8) / (3) / (3)+DEEC / EMD ── F-15 era; NASA Lewis + Dryden reports
 │    (NASA-TM-X-3261, NASA-TP-1034, NASA-TP-1056, NASA-TP-1373, NASA-TP-1482, NASA-TP-1782, NTRS-19990064011, NASA-TM-84908)
 ├─ F100-PW-200 ── F-16A/B (ICAS-2008-286 flight thrust deck lead)
 ├─ F100-PW-220 ── F-15 and F-16 (CHILDRE-MCCOY-JPP-1989 in the F-16)
 ├─ F100-PW-220E ─ NASA 836 after 2014 (NTRS-20160006705)
 └─ F100-PW-229 ── F-16C/D Block 52; NF-15B 837 with vectoring nozzles (NASA-TM-2003-212027)
   ⇒ The requirement specification F100-SPEC-CP2903B is classified (NASA-TP-1056, repository reading).
     It is never reconstructed, estimated or inferred from.

NASA Dryden output-error identification (NASA-RP-1168 method family)
 ├─ F/A-18 HARV: NASA-TM-4786, NASA-TP-97-206539, NASA-TP-1999-206573
 └─ F-15 3/8 RPV: NASA-TN-D-8136
   ⇒ Same method; different aircraft. Method agreement says nothing about the aircraft.

Morelli (NASA Langley) global modelling and real-time identification
 ├─ F-16: MORELLI-ACC-1998-F16 (implemented in Maverick), MORELLI-SFTE-2023-RTPI, NASA-TM-2013-218056, NTRS-20200003104
 └─ F/A-18: JAIRCRAFT-1995-MORELLI-MOF, NTRS-19940020628, NTRS-20040087105, NASA-CR-198248

VISTA NF-16D (F-16 pack)
 ├─ its own flight-identified model (AIAA-2018-0525) and MATV phase (NTRS-19950007831)
 └─ in-flight simulation of the F-22 control laws (AIAA-96-3379). The F-22 evaluation is F-22 evidence; the VISTA
    aerodynamics are not F-22 aerodynamics.

NESC 6-DOF check-cases (NASA-TM-2015-218675; NESC-AIAA-2013-5071)
 ├─ F-16 model files in DAVE-ML (AIAA-2002-4482 format), textbook lineage ─▶ NASA-SIMUPY-FLIGHT
 │    R2, file-verified: NESC-F16-AERO-DML (Stevens & Lewis tables via Morelli MATLAB, NOT Morelli 1998),
 │    NESC-F16-PROP-DML (steady-state thrust), NESC-F16-INERTIA-DML, NESC-F16-CONTROL-DML (LQR check-case controller),
 │    NESC-F16-GNC-DML, NESC-F16-PACKAGE-README (case 11 trim), NESC-F16-CHECKCASE-TRAJECTORIES
 └─ aircraft-independent atmosphere/EOM cases ─▶ applicable to MavSixDoFBody verification for any aircraft

Langley 1990s history (NASA-SP-2000-4519) spans F/A-18 and YF-22/F-22 test programmes: history only.
```

## 4. Configuration-incompatibility register (cross-aircraft)

| Pair | Why they must not be merged |
|---|---|
| F/A-18A/B vs F/A-18E/F | Different airframe, engine (F404 vs F414) and FCS |
| F/A-18 HARV (any phase) vs production F/A-18A | Research hardware, ballast, research control laws; TV vanes affect aft-body aerodynamics even when not vectoring (effect size unverified) |
| AAW (853) vs HARV (840) | Different airframe; 840's wings were structurally modified for 853 |
| F404 in X-29A vs F404 in F/A-18 | Single vs twin installation; different inlets, nozzles and afterbody |
| F-16 reference (TP-1538) vs AFTI/F-16, F-16XL, VISTA/MATV | See `Docs/Reference/F16_SOURCE_PACK_V0.1.md` §7 and `Aircraft/F16/SOURCE_GRAPH.md` §1 |
| TP-1538 simulation vs production F-16A/B/C/D | The simulation's mass, geometry and thrust are its own; no block identity |
| VISTA (variable-stability) vs VISTA/MATV vs X-62A | One airframe, three configurations (engine, nozzle, control laws) |
| NASA 836 pre-Quiet-Spike vs Quiet Spike vs PFTF vs post-2014 | One airframe; mass column, nose boom, centreline fixtures and engines differ |
| NF-15B 837 / NASA 835 vs NASA 836 | Canards, vectoring nozzles, different engines and FCS (837); HIDEC/PCA systems, PW1128 (835) |
| F100 builds and variants (PW-100(1)/(2-7/8)/(3), -200, -220, -220E, -229) | Different fans, cores, controls and nozzles; a serial number spans several |
| YF-22 vs F-22 EMD vs production F-22A | Different geometry (YF-22), development laws and test envelopes (EMD) |
| ICE 101 and other "F-22-like" shapes vs F-22 | Unrelated configurations |
