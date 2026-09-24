# Library Source Graph: lineage rules and cross-aircraft map

This file defines how lineage is recorded, and holds the lineage that crosses aircraft (shared engines, shared airframes, shared methods). Each aircraft's own graph is in `Aircraft/<type>/SOURCE_GRAPH.md`. For r0 that is [`Aircraft/FA18/SOURCE_GRAPH.md`](Aircraft/FA18/SOURCE_GRAPH.md).

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
| `RELATED`, `COMPANION`, `EXTENDS`, `PRECEDES`, `BUILDS_ON` | Context links | n/a |

## 2. Repeated-number register

A number that appears in many places is usually one number copied many times. Before treating agreement as confirmation, trace each occurrence to its origin.

| Number | Where it appears | Presumed single origin | Status |
|---|---|---|---|
| F/A-18 **S = 400 ft²** | NASA-TM-4772 (extract); countless derived F/A-18 works | McDonnell Aircraft reference geometry via NASA HARV documents | Repetition ≠ confirmation. Page-verify one NASA primary |
| F/A-18 **c̄ = 11.52 ft** | NASA-TM-4772 (extract); derived works | Same | Same |
| F/A-18 **b = 37.42 ft** / **37.4 ft** / **37 ft 5 in** | Derived works print 37.42; the TM-4772 extract shows 37.4; NASA fact page says 37 ft 5 in (physical) | Same reference geometry (reference vs physical and rounding differences) | Record all three. Do not "fix" any of them |
| F/A-18 HARV Phase 2/3 **W = 36,099 lbm** and Phase 1 **W = 31,980 lbm** | NASA-TM-4772 (extract) | NASA Dryden HARV mass-properties data | Difference 4,119 lb matches the HARV VDD web page. Both are NASA HARV program documents, so this shows **internal consistency, not independence** |
| F-16 **S = 300 ft², c̄ = 11.32 ft, b = 30 ft** | TP-1538, Stevens & Lewis tables, Morelli 1998, Garza & Morelli, AeroBench and other derived codes | TP-1538 / manufacturer data | Already governed by `Docs/Reference/F16_SOURCE_PACK_V0.1.md` (Maverick constants: `MavF16MorelliReference`, 27.870912 m² / 3.450336 m / 9.144 m); listed here as the pattern |
| F-15 **608 ft² / 15.94 ft / 42.8 ft** | F-15 NASA reports and derived models | Manufacturer reference geometry | Already governed by the F-15 packs in `Docs/Reference/`; trace in Priority C |

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

## 4. Configuration-incompatibility register (cross-aircraft)

| Pair | Why they must not be merged |
|---|---|
| F/A-18A/B vs F/A-18E/F | Different airframe, engine (F404 vs F414) and FCS |
| F/A-18 HARV (any phase) vs production F/A-18A | Research hardware, ballast, research control laws; TV vanes affect aft-body aerodynamics even when not vectoring (effect size unverified) |
| AAW (853) vs HARV (840) | Different airframe; 840's wings were structurally modified for 853 |
| F404 in X-29A vs F404 in F/A-18 | Single vs twin installation; different inlets, nozzles and afterbody |
| F-16 reference (TP-1538) vs AFTI/F-16, F-16XL, VISTA/MATV | See `Docs/Reference/F16_SOURCE_PACK_V0.1.md` §7 |
