# 02 — F-16 Propulsion Source Audit

**Scope:** the F-16 propulsion model Maverick actually has, what the public sources it relies on really say, and what could support a legitimately documented powered-flight model.

**Configuration identity (never "an F-16"):** `F16-CFG-NASA-REF-TP1538`. This is the 1979 NASA Langley piloted-simulation F-16 with relaxed static stability, as defined by NASA TP-1538 and repackaged by Garza & Morelli (NASA/TM-2003-212145). It is not an F-16A/B/C/D block and not a named engine.

**Evidence classes:** `PRIMARY_VERIFIED` (PV: read on the primary page by the lead researcher unless another reader is named), `SECONDARY_VERIFIED` (SV), `ENGINEERING_INFERENCE` (EI), `UNVERIFIED` (UV), `MAVERICK_PROPOSAL` (MP). `REPO_VERIFIED` / `REPO_DOC` mark repository code or documents. Source keys resolve in `09_RESEARCH_SOURCES.md`.

---

## 1. What the lead verified on the TP-1538 pages

The NTRS copy of TP-1538 retrieved for this phase (`https://ntrs.nasa.gov/api/citations/19800005879/downloads/19800005879.pdf`, 233 pages) has SHA-256 `aae0ece64474291368c0b4c816d3ab327c6100329e6eb030c2f4545d0913feb3`. That is **byte-identical** to the copy recorded in the repository manifest `Docs/Reference/Data/F16/TP1538/manifest.json`, so the repository's transcription and this audit read the same document. Pages were rendered locally and read as images.

| Item | Location | What the page shows | Class |
|---|---|---|---|
| Engine description | Appendix B, "Engine Simulation", PDF p. 46 (report p. 40) | The aircraft is powered by an afterburning turbofan. Thrust response to throttle uses the model of fig. 66(a); throttle gearing in fig. 66(b); response modelled as a first-order lag that varies as in fig. 66(c). Table VI gives idle, military and maximum thrust. | PV |
| Engine identity | same | **No engine variant is named** (no F100 build, no F110). No statement of installed vs uninstalled, gross vs net, or nozzle/inlet accounting. Garza & Morelli's aircraft description names "F110-GE-100 or F100-PW-220" only generically and does not tie the thrust tables to either (sub-agent PV). Negative idle values (e.g. −3 600 lb at M 1.0, sea level) suggest net thrust including ram drag (EI). | PV (absence on the page) |
| Engine angular momentum | same | Engine gyroscopic effects represented by a fixed engine angular momentum of **216.9 kg·m²/s (160 slug·ft²/s)**. | PV |
| Thrust table | Table VI, PDF p. 99 (report p. 93) | Two printed sections, (a) SI (N, m) and (b) U.S. customary (lb, ft); Mach rows 0.2, 0.4, 0.6, 0.8, 1.0; altitude columns 0 to 15 240 m / 0 to 50 000 ft in six steps; blocks T_idle, T_mil, T_max; negative T_idle cells present in both sections. | PV |
| Spot check | Table VI | T_max, M 0.8, sea level: **26 070 lb** and **115 959 N** (115 959 / 4.448 ≈ 26 070). T_max at sea level over Mach: 21 420, 22 700, 24 240, 26 070, 28 886 lb (smooth). Repository transcription agrees cell for cell at these points. | PV |

---

## 2. Maverick's current F-16 propulsion architecture

| Layer | Implementation | Status | Evidence |
|---|---|---|---|
| Throttle → commanded power `Pc` | `Pc = 64.94·δth` (δth ≤ 0.77); `Pc = 217.38·δth − 117.38` (δth > 0.77) | frozen (Garza & Morelli) | `F16/F16_PROPULSION_REFERENCE_V0.1.md`, `MavF16PropulsionValidation` (REPO_DOC) |
| Actual power `Pa` dynamics | Garza & Morelli piecewise first-order lag, `RTAU(ΔP)` = 1.0 / 0.1 / 1.9 − 0.036ΔP, rate factor 5 above 50 % | frozen; integrated by Maverick's fixed step | same |
| Thrust table | TP-1538 Table VI, published SI cells, bilinear in (h, M), Pa blend (idle→mil below 50 %, mil→max above) | **frozen and implemented, opt-in only** (`ConfigureTp1538SourcedInstallation`); default deck `null` ⇒ exactly 0 N | `Docs/Reference/F16_TP1538_THRUST_DECK_V0.1.md`, `Docs/Reference/F16_TP1538_RUNTIME_SEMANTICS_V0.1.md`, `F16/MavF16PropulsionSystem.cs` (REPO_VERIFIED) |
| Domain policy | Mach [0.2, 1.0], altitude [0, 15 240 m], Pa [0, 100]; **reject** outside (no clamp, no extrapolation) | implemented | runtime semantics §3.3–4 (REPO_DOC) |
| Installation | scalar thrust along aero-body +X; no thrust-line offset; negative idle passes through with its sign | implemented | runtime semantics §3.2 |
| Engine gyroscopic coupling | shared `MavEngineRotorAngularMomentum` exists (ENG-008 in `Core/MavEngineProfile.cs`, applied in `Core/MavEngineRuntime.cs`); **F-16 sets `NotAvailable`** | not used | `F16/MavF16PropulsionInstallation.cs:78` (REPO_VERIFIED) |
| Fuel flow / mass change | none | absent | — |

**Documentation drift (REPO_VERIFIED):** `Assets/MaverickFresh/Scripts/FlightDynamics/README.md` ("Propulsion status") and `F16/F16_PROPULSION_REFERENCE_V0.1.md` (header: "THRUST DECK NOT FROZEN") still say the thrust deck is unavailable. The later TP-1538 freeze and the Phase 5D runtime semantics document supersede them. Neither was edited by this research (scope rule); a documentation fix is recorded as a handoff in `08_FLIGHT_MODEL_ROADMAP.md`.

---

## 3. The five questions the brief asks

### 3.1 Which data is already frozen

| Data | Source | Frozen artefact |
|---|---|---|
| Table VI, 90 SI cells + 90 U.S. cells | TP-1538 p. 99 | `Docs/Reference/Data/F16/TP1538/table_vi_raw_source.csv` (SHA-256 `9e0d9906…`), manifest, validator `Tools/validate_f16_tp1538_thrust_deck.py` |
| Power gearing, power lag, Pa blend | NASA/TM-2003-212145 | `F16_PROPULSION_REFERENCE_V0.1.md`, `F16_TP1538_RUNTIME_SEMANTICS_V0.1.md` |

### 3.2 Which data exists but is not verified or not used

| Data | Where it is | Status |
|---|---|---|
| Engine angular momentum 216.9 kg·m²/s | TP-1538 App. B p. 46 (**PV this audit**: magnitude only, no axis); NASA/TM-2003-212145 rpt pp. 31–33 gives `heng = 160 slug·ft²/s`, fixed, **along the +X body axis** (PV by the F-16 source sub-agent; not re-read by the lead) | **not integrated** (F-16 `NotAvailable`). Axis and sign are now sourced to Garza & Morelli for the simulation configuration; the lead recommends one page re-check before implementation |
| TP-1538 fig. 66 (throttle model, gearing, lag schedule) | TP-1538 figures | the repository uses Garza & Morelli's equations instead; fig. 66 is not transcribed. Garza & Morelli is the packaging of the same simulation, so this is a consistency check, not a gap |
| NESC check-case thrust tables | `F16_prop.dml` (library file-verified) | a separate configuration (`F16-CFG-NESC-CHECKCASE`): steady-state net thrust, Mach 0–1.0, PLA with MIL at 50 %, no lag. Garza & Morelli also state 0 ≤ M ≤ 1 for their tables (sub-agent PV), so the **Mach 0 column is not from TP-1538** (which starts at 0.2); its origin (probably Stevens & Lewis) is UV |

### 3.3 Which published tables are incomplete

| Gap | Consequence | Class |
|---|---|---|
| No Mach < 0.2 | no static or take-off thrust, no very-low-speed powered flight | PV (grid) |
| No Mach > 1.0 | no supersonic powered flight; any supersonic thrust would be invention | PV (grid) |
| No altitude > 50 000 ft | upper envelope closed | PV |
| No fuel flow, no TSFC | no endurance, no mass change | PV (absence) |
| No installation geometry (thrust line, nozzle position) | thrust moments must stay zero | PV (absence) |
| Engine identity, installed/uninstalled, gross/net unstated | the deck cannot be called "F100-PW-200 thrust" or "installed thrust" | PV (absence) |

### 3.4 Which engine variants match the reference configuration

**None can be matched from the TP-1538 pages.** Appendix B names no engine. Assigning the F100-PW-200 because early F-16A/Bs used it would be a configuration assumption, not a source fact (EI). Under the repository's variant-separation rule (`F16_PROPULSION_SOURCE_GRAPH.md`), the deck stays tied to `F16-CFG-NASA-REF-TP1538` and is never relabelled.

### 3.5 What could support a legitimately documented powered-flight model

| Model | Basis | Envelope | Honest label |
|---|---|---|---|
| **TP-1538 simulation thrust** (exists, opt-in) | Table VI + Garza & Morelli power lag | M 0.2–1.0, 0–15 240 m | "NASA TP-1538 simulation thrust (1979 Langley piloted-simulation model; engine variant unstated)" |
| + engine gyroscopic term | TP-1538 App. B magnitude; Garza & Morelli axis (+X body) | same | as above |
| NESC check-case thrust | `F16_prop.dml` | M 0–1.0, 0–50 000 ft | only inside an NESC validation profile (see doc 07) |
| A named production engine (F100-PW-200/-220/-229, F110) | none located with a public deck tied to an F-16 configuration (library leads ICAS-2008-286, Childre & McCoy 1989: not verified) | — | **blocked** |

---

## 4. Discrepancy: NESC vs TP-1538 at sea-level maximum thrust

| Cell | TP-1538 Table VI (PV, p. 99) | NESC `F16_prop.dml` (library file-verified, lines 324–325) |
|---|---|---|
| T_max, M 0.8, h 0 | **26 070 lb** (SI 115 959 N) | 28 070 lb |
| T_max, M 1.0, h 0 | **28 886 lb** (SI 128 485 N) | 28 885 lb |

- The primary page prints 26 070 in the U.S. section, and the SI section's 115 959 N is consistent with it at the report's 4.448 N/lb. Two independent printed numbers therefore agree on 26 070.
- The sea-level T_max row on the page rises smoothly with Mach (≈ +1 300 to +2 800 lb per 0.2 Mach). A value of 28 070 at M 0.8 would make the M 0.8 → 1.0 step only +815 lb (EI).
- **Conclusion (PV + EI):** for `F16-CFG-NASA-REF-TP1538` the correct value is 26 070 lb. The 28 070 lb value belongs to the NESC check-case configuration's own data (its reference trajectories were generated with it) and most likely entered the textbook lineage as a transcription change. The library classified this conflict `F16-C6` as `RESOLVED_PER_CONFIGURATION` pending a page read; **this audit supplies the page read.** Any NESC-profile comparison must use the NESC value, and any TP-1538 run the TP-1538 value. Never merge them.

---

## 5. Engine dynamics: what the model is and is not

- Garza & Morelli's lag is a **power-level** lag (first-order in `Pa` with an amplitude-dependent time constant and a mil/afterburner crossover rule). It is not a spool (N1/N2) model, has no inlet or nozzle dynamics, and has no altitude or Mach dependence of the time constant (REPO_DOC + EI).
- TP-1538 states the lag "varied as shown in figure 66(c)" (PV). Comparing fig. 66(c) with the Garza & Morelli RTAU schedule is a recommended page check (MP), not a known discrepancy.
- For the instructor this matters because turn energy depends on thrust. The F-15 research rig has fixed thrust; the F-16 has sourced thrust only on the opt-in path. Turn-energy tests (doc 06) on the F-16 must state which propulsion configuration was active.

---

## 6. Recommendations

| P | Action | Basis | Effect | Validation |
|---|---|---|---|---|
| P0 | Correct the two stale "thrust deck not frozen" statements (README, `F16_PROPULSION_REFERENCE_V0.1.md`) to point at the Phase 5D opt-in | REPO_VERIFIED drift | removes a false statement developers will act on | doc review |
| P0 | Record the page-verified Table I / Table VI / App. B facts in the source library (library owner) | §1 | library entries move from `REPOSITORY_READING` to page-verified | library validator |
| P1 | Integrate the engine angular momentum (216.9 kg·m²/s along +X body, TP-1538 App. B + Garza & Morelli) through the existing `MavEngineRotorAngularMomentum` path, after one lead page re-check of the Garza & Morelli axis statement and an explicit mapping of "+X body" through the README's axial-vector rule | §3.2 | removes a known missing physical term (gyroscopic pitch/yaw coupling in rolls and pulls) | a rotor-coupling test: constant pitch rate must produce the predicted yaw moment `h × ω` with the correct sign, plus unchanged results when the term is off |
| P1 | Page-compare TP-1538 fig. 66(c) with the Garza & Morelli RTAU schedule | §5 | confirms the dynamics lineage | written comparison |
| P3 | ICAS-2008-286 (Hendrick et al.; full text read by the sub-agent) builds an installed net-thrust/TSFC deck for Belgian F-16A/B MLU aircraft, **subsonic, plots only, F100 dash-number not stated**. Not usable as data; at most a qualitative cross-check for a *different* configuration | sub-agent PV | none for the model | — |
| — | Do **not** extend thrust below M 0.2 or above M 1.0, or attach a production engine label | §3.3–3.4 | — | — |
