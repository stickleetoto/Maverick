# 03 — F-15 Source Gap Analysis

**Scope:** what the F-15 model Maverick flies actually represents, how far its public sources reach, and whether any public source can extend it toward a manoeuvring envelope.

**Starting point.** The repository has already run deep F-15 source hunts: `Docs/Reference/F15_REMAINING_GAPS_V1.0.md`, `F15_FULL_SCALE_DATA_GAPS_V0.2.md`, `F15_R5_F100_SOURCE_AUDIT_V0.1.md`, `Docs/Research/Aerospace/Aircraft/F15/*`. This document does not repeat them. It records (a) what this phase verified or added, (b) the consequence for an instructor and a manoeuvring simulator, and (c) a ranked acquisition strategy.

**Evidence classes:** PV / SV / EI / UV / MP. "Repo" marks repository documents. Sources: doc 09. External items were read by a sub-agent of this session; **no AFIT thesis full text could be opened** (DTIC and AFIT Scholar returned HTTP 403).

---

## 1. Which F-15 Maverick actually flies

| Item | Value | Source |
|---|---|---|
| Configuration | `F15_AFIT_BAUMANN_DAVISON_PILOT_CONTROLLED_V1`, built on the frozen research model `F15_AFIT_BAUMANN_DAVISON_MACH06_20K_RESEARCH` | Repo: `F15_PILOT_CONTROLLED_V1.md` |
| Aero | Davison thesis App. C six-axis curve fits of Baumann's model, which fits McDonnell "ARO10" / 1988 "F-15 Aerobase" simulator tables (not public) | Repo |
| Flight condition | **one**: Mach 0.6, 20 000 ft, source density 0.6531396 kg/m³ at every state | Repo |
| Speed domain | 218.5–699.7 ft/s at that density | Repo |
| α / β | transcribed α −4…90°, \|β\| ≤ 20°; Maverick pilot flights exercised α ≈ 15.5–20.2° | Repo (V2 §7.2) |
| Mass | 37 000 lb; inertia 25 480 / 166 620 / 186 930 / Ixz −1 000 slug·ft² | Repo |
| Geometry | S 608 ft², b 42.8 ft, c̄ 15.94 ft (ARO10 lineage) | Repo |
| Thrust | fixed 8 300 lbf total, throttle inactive | Repo |
| Trim | Table VII point 36: V 300.8 ft/s, α 17.49°, stabilator −15.15° | Repo |

**Variant identity (this phase).** AFIT Scholar abstracts of two later theses in the same lineage (McDonnell, AFIT-GAE-ENY-90D-16; Fero, AFIT-GA-ENY-91D-4) describe the model as an **F-15B** model (SV, catalogue abstracts). Davison's own title says only "F-15"; a search snippet links his flight work to an F-15D (UV). Conclusion: the lineage is best recorded as "AFIT research model, F-15B per later lineage abstracts; variant of the underlying McDonnell tables not established". It must not be labelled F-15A, C, E or EX.

**Cross-checks this phase found:**
- An abstract snippet attributed to Fero says the 1988 Aerobase model was run at **8 300 lb thrust for level flight at M 0.6 / 20 000 ft** (UV, snippet). That matches the repository's fixed research thrust and would be an independent consistency check if the page is ever read.
- **Reference span.** NASA TM-2003-212027 Table 1 (F-15B 837) gives S 608 ft², c̄ 15.94 ft, **b 42.7 ft** (PV, sub-agent). Scaling TM-X-62360's 0.075-scale model geometry gives the same (EI). The repository's 42.8 ft is the physical span; the 42.7 vs 42.83 pair is already registered as `F15-X4` (physical vs reference span). **Consequence:** if the AFIT fits were non-dimensionalised with a 42.7-ft reference span, Maverick's Cl and Cn are scaled by 42.8/42.7 ≈ 1.002 (EI; +0.23 %). Negligible for behaviour; recorded so it is not rediscovered as a "bug".

---

## 2. Coverage and validity

| Parameter | Maverick coverage | Public source | Verified domain | Missing domain | Confidence |
|---|---|---|---|---|---|
| Six-axis aero | Davison App. C fits | AFIT theses (full text not reachable this phase) | one Mach/altitude point; α −4…90 transcribed; \|β\| ≤ 20 | every other Mach and altitude; Reynolds/compressibility variation | Medium at the point; none elsewhere |
| Dynamic derivatives | in the fits (repo) | same | same | same | Medium |
| Mach dependence | **none** | none public for this model | M 0.6 only | all | none |
| Altitude / density | **fixed** density | — | 20 000 ft | all | none |
| Control surfaces | stabilator (symmetric + differential 0.3 × aileron, sourced research relation), aileron, rudder | Davison driver (repo) | gameplay envelopes (Maverick tuning) | actuator rates, hard stops (not public) | Medium |
| Mass / CG | one loading, 37 000 lb | AFIT lineage | — | fuel schedule | Medium |
| Thrust | fixed 8 300 lbf | AFIT lineage | — | dimensional deck | Low (fixed value) |
| FCS | Maverick laws V1/V2 (non-authoritative) | NASA 836 structure only (TM-2009-214651, TM-2012-215978) | structure | gains | — |

**Suitability for manoeuvring (EI):** the model can represent manoeuvres that stay near M 0.6 at the fixed density. Pulling to high g bleeds speed, and the domain floor (218.5 ft/s) is close to the trim (300.8 ft/s). A sustained turn, a zoom climb or a dive changes Mach and dynamic pressure in ways the fixed-point fits do not represent. **It is a stability and control research model, not an envelope model.**

---

## 3. Sources that could extend the model (assessed)

| Source | Configuration | What it holds | Usable as model data? | Status |
|---|---|---|---|---|
| NASA/TM-2003-212027 (Smith, Moes, Morelli 2003) | NF-15B **837** with canards, F100-PW-229 | flight-identified derivatives at M 0.75 / 20 kft, M 0.9 / 25 kft, M 1.2 / 32 kft — **figures only**; Table 1 reference set | No (configuration mismatch: canards; plots) | Repo-known; PV this phase |
| NASA TM-X-62360 (Petroff, Scher, Cohen 1974) | 0.075-scale early F-15 model, M 0.16 | α 0…90 (and inverted), β −20…30 — **plotted only** | Validation trends only | Repo-known; PV |
| NASA CR-3478 / CR-3479 (Barnhart 1982) | 1/12-scale F-15, rotary balance | α 8…90°, rotation rates — plotted | High-α validation trends only | Repo-known; PV/SV |
| NASA TN D-8136 (Iliff, Maine, Shafer 1976) | 3/8-scale unpowered RPRV | identified derivatives α −20…53° | Mismatch (scale model) | Repo-known |
| **NASA TM-86042 (Haering & Burcham 1984)** | F-15 (HIDEC) | **C_D(M, C_L) and α(C_L, M) plotted** (Figs. 2–3), credited to AFFTC-TR-77-7 "F-15A Performance" | Possible **performance cross-check** (drag polar vs Mach) after digitising with stated uncertainty; not a six-axis model | **New this phase**; PV |
| AFFTC-TR-77-7 (Zaloga 1977), AFFTC-TR-75-32 | F-15A | performance; stall/post-stall | Not located / limited distribution | Do not seek limited documents |
| MDC A4172, ARO10/Aerobase, DN-1180 | F-15 production databases | — | **Not public** (repo conclusion unchanged) | Stop |

**Conclusion:** no public source extends the six-axis F-15 model beyond its single condition for any identified variant. The best additions are **validation trends** (high-α tunnel and rotary-balance plots; 837 derivative trends with the configuration mismatch stated; TM-86042 drag polars as a performance cross-check).

---

## 4. Propulsion

| Item | Finding | Class |
|---|---|---|
| Maverick | fixed 8 300 lbf; `MavF100PathSeparation` refuses to combine incompatible F100 paths | Repo |
| 836 engine build | F100-PW-100 (exact); build (1)/(2)/(2-7/8)/(3) **unknown**; re-engined to -220E in 2014 | Repo |
| TP-1373, TP-1482, TP-1782 | prototype F100-PW-100 (S/N P680059/063); percent-error or model-relative results only; no dimensional deck | PV (sub-agent), repo-known |
| TP-1069, TP-1228 | prototype engines "essentially an F100(2)"; normalised curves with printed normalising constants (98.4 kg/s, 111 kN corrected gross thrust) | PV; repo-known (repo: cannot supply the design maximum net thrust — no static point) |
| **TM-86042** | **F100 EMD** engine (not -100): thrust and fuel flow vs Mach/altitude **plotted**, from the P&W EMD status deck | PV; **new** — a different engine than 836's |
| TM-1998-206560 (Conners & Sims) | F100-PW-229, F-15 ACTIVE, in-flight thrust to M 2.0 | SV (abstract); new lead; different engine and aircraft |
| MIPCC study (Kloesel & Clark 2013) | cites a non-public PW-100 status deck | PV; confirms the deck exists but is not public |

**Variant discipline (repository rule, reaffirmed):** PW-100 builds, -220, -220E, -229 and EMD are separate engines. None of the new items gives a dimensional deck for 836's engine. TM-86042's EMD plots could only support a *different*, explicitly labelled research engine profile, and digitised plots carry scan uncertainty. Not recommended for the flown research F-15.

---

## 5. Variant separation (required by the brief)

| Variant | Public data relevant to Maverick | Use |
|---|---|---|
| F-15A | AFFTC performance reports (cited, not located/limited); TM-86042 drag polars credited to "F-15A Performance" | performance cross-check only, after digitising |
| F-15B | AFIT lineage (per later abstracts); NASA 836 (mass, FCS structure), 837 (canard research aircraft — separate) | the research model and its validation |
| F-15C/D | nothing numeric located | none |
| F-15E / EX | nothing numeric located; production data not public | none — do not borrow C/E/EX "spec" numbers |

---

## 6. Ranked strategy

| Rank | Action | Value | Risk | Effort |
|---|---|---|---|---|
| 1 | Keep the F-15 as a **single-point research model**; publish its envelope to the instructor through the descriptor/domain caps (already done in the dev branch's guidance soft caps) | honest behaviour at the domain edges | none | low |
| 2 | Use TM-X-62360 / CR-3478/3479 plots as **high-α trend validation** of the AFIT fits (sign and shape of Cm(α), Cn_β(α) etc. above 25°) with stated digitising uncertainty and the configuration mismatch labelled | catches gross errors in the transcribed high-α region | scale/Reynolds mismatch | medium |
| 3 | Re-attempt the Baumann/Davison/Fero full texts from a host that is not blocked; verify the M 0.6 / 20 000 ft condition, the 8 300 lb thrust statement and the variant | closes provenance gaps | none | low (access) |
| 4 | TM-86042 drag-polar cross-check of the AFIT fits at M 0.6 (one point) | independent check of drag level | different aircraft state; digitising | medium |
| — | Do **not** build a multi-Mach F-15 by mixing 837 derivatives, scale-model data and F-15E/EX specifications | — | fabricated envelope | — |

**Highest-priority next action:** treat the F-15 as the instructor's correctness testbed (as Phase 1 concluded) and invest aircraft-data effort in the F-16 path, where a public extension (TP-1538 to α 90°) actually exists.
