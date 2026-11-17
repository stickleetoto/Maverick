# Instructor v2 Research — Phase 2: Flight-Model Data, Control Identification and Independent Validation

**Branch:** `claude/instructor-v2-research-phase2`, from `origin/main @ 71820a97b8d58c69229799fb00868bce4ded87d1` (stable research baseline `f0f9519`).
**Read, not modified:** Phase 1 research `claude/instructor-v2-research @ 35b3183` (draft PR #34); development branch `claude/realistic-mouse-instructor-v2` (pushed `5776617`; local unpushed commit `90add24` read for alignment).
**Date:** 2026-11-17. **Type:** research only. No production code, physics parameter, coefficient, scene or prefab changed.

## Documents

| # | Document | Content |
|---|---|---|
| 01 | [F-16 aerodynamic source audit](01_F16_AERODYNAMIC_SOURCE_AUDIT.md) | lineage corrected by page reading; coverage matrix; missing domains; TP-1538 tabular extension to α 90° |
| 02 | [F-16 propulsion source audit](02_F16_PROPULSION_SOURCE_AUDIT.md) | Table VI page check; frozen vs unused data; engine identity; NESC thrust discrepancy; gyroscopic term |
| 03 | [F-15 source gap analysis](03_F15_SOURCE_GAP_ANALYSIS.md) | what the AFIT research model is; why no public source extends it; validation trends; F100 variant discipline |
| 04 | [F-22 model feasibility](04_F22_MODEL_FEASIBILITY.md) | no defensible public model; close the topic |
| 05 | [Control stability and identification](05_CONTROL_STABILITY_AND_IDENTIFICATION.md) | the loop as built; separation rules and their provenance; multisine, frequency-domain, LOES and loop-breaking methods; gain selection; ownership of flight-condition adaptation; numerical tests |
| 06 | [Guidance validation plan](06_GUIDANCE_VALIDATION_PLAN.md) | hard gates G1–G8; scenarios S1–S14; metrics M1–M18; telemetry; handoff |
| 07 | [NASA 6-DoF benchmark study](07_NASA_6DOF_BENCHMARK_STUDY.md) | NESC cases, models, licensing; lineage link to Maverick; three-level validation; error separation; JSBSim role |
| 08 | [Flight-model roadmap](08_FLIGHT_MODEL_ROADMAP.md) | aircraft readiness; ranked improvements; handoffs H1–H6; blocked items; long-term path |
| 09 | [Research sources](09_RESEARCH_SOURCES.md) | bibliography with inspector and class; discrepancy register; acquisition status |

## Ten most important findings

1. **TP-1538 tabulates the F-16 aerodynamic database over α −20…90° and β ±30°** (static) with dynamic derivatives over the same α. The α −10…45° limit Maverick uses belongs to the Stevens & Lewis *simplified* set and the Morelli fit. A public, same-configuration extension to high α exists (doc 01).
2. **The TP-1538 data come from low-speed tests at M 0.1–0.2**; Mach, Reynolds-number and aeroelastic effects were not included (lead-read p. 9). "Valid below about Mach 0.6" is the authors' judgement, not a tested range. No public tabulated transonic/supersonic data exist for this configuration (doc 01).
3. **Maverick clamps α at 45° and only flags Mach > 0.6**, so flight beyond the data is silent unless telemetry counts it (doc 01 §2; handoff H5).
4. **Morelli fitted the Stevens & Lewis simplified dataset, and so do the NESC check-case tables** (via Garza & Morelli). Maverick's aero and NASA's check-case aero are two representations of the same data, which makes a coefficient-level comparison meaningful (docs 01, 07).
5. **TP-1538 Table VI page-checked:** T_max at M 0.8, sea level is 26 070 lb (SI 115 959 N). The NESC file's 28 070 lb belongs to that configuration only. The repository transcription is confirmed at the checked cells, and the NTRS PDF is hash-identical to the repository's copy (doc 02).
6. **A sourced physical term is missing:** engine angular momentum 216.9 kg·m²/s (TP-1538 App. B; +X body per Garza & Morelli) is available but set to `NotAvailable` for the F-16 (doc 02; handoff H4).
7. **Two repository documents still say the F-16 thrust deck is not frozen**, which Phase 5D superseded; the "LEF fixed at 25°" and "Morelli fitted TP-1538" statements also need correcting (doc 08 H3).
8. **No NESC case is flat-earth.** Every F-16 case runs on a rotating WGS-84 Earth with J2 gravity; an uncorrected Coriolis effect is of order 790 ft lateral at 180 s (EI). Validation must be split into EOM checks (tumbling brick), coefficient/trim checks, and relative closed-loop responses (cases 13.1–13.4). NASA set **no tolerances**; the reference tools' own spread is the only defensible floor (doc 07).
9. **Loop-separation ratios (4×, 5–10×, 10×) are rules of thumb** with process-control provenance; none is a sourced aircraft requirement. The development branch's outer-gain rationale ("damping ≈ 0.45") is an estimate. NASA's multisine / frequency-domain / loop-breaking methods let Maverick *measure* inner-loop dynamics, separation and margins instead (docs 05–06).
10. **No public source extends the F-15 research model beyond its single M 0.6 / 20 000 ft condition, and no defensible F-22 model is possible.** The F-15 is the right instructor *correctness and identification* testbed; aircraft-data effort belongs on the F-16 path (docs 03–04).

## Aircraft readiness (summary; detail in doc 08 §1)

| Aircraft | Evidence-supported envelope | Biggest limit | Next action |
|---|---|---|---|
| F-16 (TP-1538 / Morelli) | M 0.2–~0.6 (thrust from 0.2), α −10…45°, β ±30° | no Mach effects; silent α clamp | NESC Level 1–2; doc fixes; decide on TP-1538 α extension |
| F-15 (AFIT research) | 218.5–699.7 ft/s at one density; exercised α ≈ 15–20° | single flight condition, fixed thrust | identification testbed for the instructor |
| F-22 | none | no public data | close |
| NESC F-16 (validation) | near 10 000 ft / 287.8 KEAS | validation only | build as a profile |

## Most promising sources

- **Aerodynamics:** NASA TP-1538 Table III (α to 90°, low speed); Morelli 1998 (current model); NESC `F16_aero.dml` (validation tables).
- **Propulsion:** TP-1538 Table VI + Garza & Morelli power lag (already implemented, opt-in); NESC `F16_prop.dml` (validation only).
- **Control validation:** Morelli AIAA-99-4043; Grauer 2021; Bosworth & Burken TM-113085; Stachowiak & Bosworth TP-2004-212046.
- **Independent benchmark:** NASA/TM-2015-218675 Vol I, AIAA 2015-1810, NESC case data.

## Verification status

| Category | Items |
|---|---|
| **Directly verified by the lead** | TP-1538 pp. 15, 43–46, 49, 51, 99 (hash-matched PDF); Morelli 1998 full text; Morelli AIAA-99-4043 key statements; repository records and code cited in docs 01–02, 05, 07; development-branch configuration (read only) |
| **Verified by sub-agents (PV in their reading, not re-read by the lead)** | Garza & Morelli F-16 section (incl. engine axis); TP-1538 p. 38 Mach statement; NESC site, Vol I, AIAA 2015-1810; F-15/HARV/F-22 NASA reports listed in doc 09; NASA identification papers |
| **Inferred (EI)** | Coriolis magnitudes; delay phase costs; damping estimates; TP-1538 vs NESC thrust smoothness argument; NESC/Maverick comparability via shared lineage |
| **Requires further investigation** | NESC Vol II; Garza & Morelli axis re-check by the lead; LEF base state of the simplified tables; AFIT thesis full texts; DTIC ADA327869; TP-1538 figs. 62–66 transcription; whether the dev branch's outer-loop damping matches its estimate |

## Open questions for the user

1. Should Maverick invest in a **TP-1538 tabular aero model to α 90°** (a large, careful transcription) or keep the Morelli domain and only make violations visible?
2. May the NESC data zips and Vol II be **downloaded and stored as validation fixtures** (public-use NASA material; the case-data zip alone is 24.9 MB, Vol II is over 10 MB)?
3. Should the **NESC validation profile** become a permanent third F-16 configuration in the test suites?
