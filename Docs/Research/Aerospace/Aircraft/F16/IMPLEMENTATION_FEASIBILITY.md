# F-16 Implementation Feasibility (R1, R2 update)

> **R2.** A fourth column now covers `F16-CFG-NESC-CHECKCASE`, the first F-16 configuration verified by the library itself (NASA DAVE-ML files, file-line level). It is a validation configuration, not a replacement for Maverick's reference F-16.

**This is a feasibility assessment, not an implementation plan.** It answers seven questions (verdicts A-G) from public sources only. The configuration each verdict applies to is named, because the answer changes with the configuration.

Evidence base: [`SOURCE_GRAPH.md`](SOURCE_GRAPH.md), [`KNOWN_DATA.md`](KNOWN_DATA.md), [`MISSING_DATA.md`](MISSING_DATA.md). Library verification ceiling in R1: **no F-16 source is PAGE_VERIFIED by the library.** Repository page readings exist for TP-1538, Morelli 1998 and Garza & Morelli. They are recorded, not relied on.

---

## Verdict scale

| Verdict | Meaning |
|---|---|
| **SUPPORTED** | Public primary sources exist for this configuration and the data type. Library page verification is still required before values are released |
| **PARTIAL** | Some elements are sourced; others would need labelled tuning or a research substitute |
| **NOT SUPPORTED** | No public source located; implementation would require invention |

## Verdicts

| # | Question | TP-1538 / Morelli simulation configuration | Production F-16 (any block) | Research airframes (VISTA / MATV / AFTI / F-16XL) | **NESC check-case (R2, verified)** |
|---|---|---|---|---|---|
| **A** | Six-axis coefficient model (CX, CY, CZ, Cl, Cm, Cn) without guessed coefficients | **SUPPORTED below Mach 0.6** (alpha -10..45, beta +/-30, LEF fixed at 25 deg). Above M 0.6: NOT SUPPORTED | **NOT SUPPORTED** | VISTA: PARTIAL lead (`DTIC-ADA327869`, content unseen). Others: not F-16 models | **VERIFIED + ALLOWED** (table model; clamped domain; near Mach 0.5) |
| **B** | Mass / inertia closure | **SUPPORTED for one loading** (TP-1538 Table I: weight, Ix, Iy, Iz, Ixz). No fuel schedule | NOT SUPPORTED | NOT SUPPORTED | **VERIFIED + ALLOWED** (constant mass; CG 25 %, MRC 35 %) |
| **C** | Source-honest FCS approximation | **PARTIAL.** Architecture is public (TP-1538 simulated CAS; TN D-8176 limiters/ARI/yaw damper); position limits are sourced; **rates and gains are not.** Allowed only as a labelled research law (see [`F16_FCS_SOURCE_GRAPH.md`](F16_FCS_SOURCE_GRAPH.md) §3) | NOT SUPPORTED as "the F-16 FLCS". The Droste & Walker case study might raise the architecture layer; gains not located | Research laws are described; numeric content unseen | Check-case LQR controller only (VERIFIED); not an F-16 FCS; no actuators |
| **D** | Dimensional propulsion | **SUPPORTED** for the simulation engine (Table VI + Garza & Morelli power lag); engine variant and thrust bookkeeping unstated | NOT SUPPORTED (one lead: `ICAS-2008-286`, F-16A/B F100) | MATV: narrative only | **VERIFIED + ALLOWED** (steady-state net thrust; no lag) |
| **E** | Trim validation | **PARTIAL.** NESC case 11 uses a different aero model; not a direct check of this configuration | NOT SUPPORTED | NOT SUPPORTED | **VERIFIED + ALLOWED** (case 11 trim; tolerance floor from the tool spread) |
| **F** | Trajectory validation | **PARTIAL.** Only through a separate NESC validation profile; AeroBench is a behavioural oracle only | NOT SUPPORTED | Flight data exist for VISTA/AFTI/MATV, configuration-specific, numeric content unseen | **SUPPORTED** as implementation verification (8 cases x 3 tools, verified files) |
| **G** | Research PlayMode (a Unity test aircraft behind a research flag) | **SUPPORTED as a research profile of the TP-1538 simulation**, subsonic, with labelled tuning for actuator rates and FCS gains | NOT SUPPORTED as a production F-16 | A separate VISTA/MATV-like research profile would need R1-R3 in `MISSING_DATA.md` | **SUPPORTED as a headless validation profile** (every input verified); not a gameplay aircraft (no actuators, one design point) |

## What "SUPPORTED" does and does not unlock

- It unlocks **a library page-verification pass** (FA18-R2-style) on TP-1538, Morelli 1998 and Garza & Morelli. After that pass, the TP-1538 Table I values and the Morelli domain could become `ALLOWED` for `F16-CFG-NASA-REF-TP1538` / `F16-CFG-SIM-MORELLI1998`.
- It does **not** change the repository's runtime code, which already follows `Docs/Reference/F16_SOURCE_PACK_V0.1.md`. This library neither endorses nor overrides those decisions. If the library's page pass disagrees with a repository reading, that is recorded as a conflict and reported, never silently corrected.
- It does **not** make any production-block claim. "F-16C" in gameplay text would need a public production source that R1 did not find.

## Strongest lineages and biggest blockers (summary)

| Area | Strongest public lineage | Biggest blocker |
|---|---|---|
| Aerodynamics | `NASA-TP-1538` ─▶ `MORELLI-ACC-1998-F16` (M < 0.6) | No production or transonic data; library page verification |
| FCS | Architecture: `NASA-TP-1538`, `NASA-TN-D-8176`; manufacturer description: `DROSTE-WALKER-F16-FBW` (paywalled) | No public gains or actuator rates |
| Propulsion | `NASA-TP-1538` Table VI (repository transcription, cross-checked) + `NASA-TM-2003-212145` power lag | Engine variant and bookkeeping unstated; no named-engine installed deck |
| Validation | `NASA-TM-2015-218675` + `NASA-SIMUPY-FLIGHT` (implementation); `AIAA-2018-0525` (VISTA flight) | No independent production-F-16 trim or trajectory data |
