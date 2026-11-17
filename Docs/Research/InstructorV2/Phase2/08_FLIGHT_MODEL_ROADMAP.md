# 08 — Flight-Model Roadmap

**Purpose:** a development strategy for Maverick's flight model, prioritised by engineering value, strength of evidence and effort, separating what is possible now from what is blocked by missing public data. It also carries the aircraft-readiness assessment, ranked improvements, concrete handoffs and blocked items the brief asks for.

**No engine rewrite is proposed.** Phase 1 doc 04 found no demonstrated benefit; nothing in Phase 2 changes that. Every item below extends or validates the existing pipeline.

Classes: PV / SV / EI / UV / MP. Detailed evidence lives in docs 01–07; sources in doc 09.

---

## 1. Aircraft-model readiness

### F-16 — `F16-CFG-NASA-REF-TP1538` (aero `SIM-MORELLI1998`)

| Item | Status |
|---|---|
| Supported capabilities | six-axis Morelli polynomial aero; TP-1538 mass/inertia (lead PV); F-16 V0.1 control law with α/g/roll limiters (Maverick tuning); sourced power dynamics; TP-1538 Table VI thrust **opt-in** powered-reference path; trim solver |
| Physical completeness | aero α −10…45°, β ±30°, low-speed data (tested M 0.1–0.2, authors' validity < ~0.6); no Mach, Reynolds or aeroelastic effects; LEF base state not stated by the fitted source; no engine gyroscopic term; no fuel burn |
| Evidence-supported envelope | **Mach < ~0.6** (judgement), α −10…45°, β ±30°, 0–15 240 m for thrust, Mach 0.2–1.0 for thrust; inside both: Mach 0.2–~0.6 |
| Important limits | silent α clamp above 45°; Mach > 0.6 only flagged; default propulsion 0 N (opt-in for thrust); stale "thrust deck not frozen" statements in two documents |
| Highest-priority next action | NESC Level 1–2 validation (doc 07) and the docs cleanup; then decide on a TP-1538 tabular extension to α 90° (doc 01 §5.1) |

### F-15 — `F15_AFIT_BAUMANN_DAVISON_PILOT_CONTROLLED_V1`

| Item | Status |
|---|---|
| Supported capabilities | AFIT six-axis research fits at one condition; V1/V2 Maverick laws; deterministic Play Mode validation; the development branch's mouse instructor targets it |
| Physical completeness | one Mach/altitude point (M 0.6, 20 000 ft) at fixed density; fixed 8 300 lbf thrust; one mass state; F-15B per later lineage abstracts (SV) |
| Evidence-supported envelope | 218.5–699.7 ft/s at the fixed density; exercised α ≈ 15.5–20.2° |
| Important limits | no Mach/altitude variation; no throttle; no protections in V2 (guidance soft caps instead); no public source extends it |
| Highest-priority next action | use it as the instructor's **correctness and identification testbed** (docs 05–06); add high-α trend validation from tunnel plots (doc 03 §6) |

### F-22

| Item | Status |
|---|---|
| Supported / completeness / envelope | none; no public reference geometry, mass, inertia, coefficients, gains or thrust (doc 04) |
| Highest-priority next action | **close the topic** |

### NESC check-case F-16 — `F16-CFG-NESC-CHECKCASE` (validation configuration)

| Item | Status |
|---|---|
| Capabilities | file-verified tables, mass, thrust, LQR SAS/autopilot; multi-tool reference trajectories (doc 07) |
| Use | validation profile only; never a gameplay aircraft |

---

## 2. Ranked technical improvements

Scoring is qualitative (H/M/L) against the brief's criteria.

| Rank | Improvement | Engineering value | Evidence | Fidelity impact | Compatibility | Risk | Complexity | Ease of validation |
|---:|---|---|---|---|---|---|---|---|
| 1 | **Closed-loop identification + margins for the instructor/law loop** (docs 05–06) | H | H (NASA methods, PV) | indirect (removes guesswork) | H (commands only) | L | M | H |
| 2 | **NESC Level 1 (tumbling brick) and Level 2 (static shots, coefficient sweep, case 11 trim)** | H | H (NASA data, PV) | verifies EOM and aero implementation | H | L | M | H |
| 3 | **Documentation corrections** (thrust status; Morelli lineage; LEF wording; TP-1538 Table I/VI/App. B page verification into the source library) | M | H (lead PV) | none directly; prevents wrong decisions | H | L | L | H |
| 4 | **Engine gyroscopic term for the TP-1538 F-16** (216.9 kg·m²/s, +X body) | M | H magnitude (lead PV), axis SV-level (re-check) | small, physically real coupling | H (existing `MavEngineRotorAngularMomentum`) | L | L | M |
| 5 | **Make the α clamp and the Mach flag visible to readiness/telemetry as domain violations** | M | H (code read) | prevents silent unphysical flight | H | L | L | H |
| 6 | **TP-1538 Table III tabular aero to α 90° (low speed)**, as a separate selectable model | H for high-α realism | H (data exists, lead PV) | large at high α, none at speed | M (new aero model + validation suite) | M (transcription volume; LEF base-state question) | H | M |
| 7 | **NESC Level 3 (cases 13.1–13.4) with the check-case LQR ported as a validation-only law** | M | H | verifies propagation + controller integration | M | M (Coriolis bookkeeping) | H | M |
| 8 | **F-15 high-α trend validation** from TM-X-62360 / CR-3478/3479 plots | L–M | M (plots, scale-model mismatch) | catches gross errors | H | M (digitising) | M | M |
| 9 | **F-16 law: stability-axis roll and yaw damping** (Phase 1 P2) | M | M (TP-1538 sim CAS + F-15 V2 measurements) | smaller β in rolls at α | M (moves F-16 baselines) | M | M | M |
| 10 | JSBSim offline cross-check with Maverick's own data | L–M | — | numerical only | H (offline) | L | M | M |

---

## 3. Concrete development handoffs (recommendations only; nothing implemented here)

### H1 — Instructor loop identification and margins
- **Subsystem:** validation (`Assets/MaverickFresh/Scripts/FlightDynamics/Validation/`), development branch runner.
- **Current location:** `MavMouseAimInstructorV2FlightValidationRunner` (dev commit `90add24`); `MavF15PilotControlLawV2.Compute`.
- **Proposed modification:** add a validation-only multisine command source and a validation-only wrapper law that adds an injection to the V2 surface request; add scenarios S1, S2, S4 (all directions), S6, S9, S10, S13, S14 (doc 06 §3).
- **Evidence:** Morelli AIAA-99-4043 (lead PV); Grauer 2021; Bosworth & Burken TM-113085; TP-2004-212046 (PV).
- **Expected measurable effect:** first measured values of M14–M17; `K_θ` chosen from measured `τ_r`.
- **Required tests:** G1–G8; 0.5×/2× linearity; dt sensitivity with latency held.
- **Risks:** a validation hook leaking into production rigs — mitigate by keeping it in Validation and listing it in the baseline manifest as excluded.

### H2 — NESC Level 1 and Level 2
- **Subsystem:** validation + reference data.
- **Location:** new `Docs/Reference/Data/NESC/` (fixtures with SHA-256 and provenance), new offline/editor suites.
- **Modification:** case 2 brick (torque-free) in Maverick; 16 aero and 9 propulsion static shots against Morelli/TP-1538; coefficient sweep over the NESC grid; case 11 trim in a validation profile with NESC mass, CG 25 %, US76, reference local g and the NESC thrust table.
- **Evidence:** doc 07 (PV); library file-level verification.
- **Expected effect:** quantified EOM integration error vs dt; per-coefficient Morelli-vs-table fit band; trim difference against the three-tool spread.
- **Tests:** convergence order; inside/outside the tool envelope.
- **Risks:** confusing the validation profile with the reference aircraft — keep configuration IDs separate.

### H3 — Documentation corrections
- **Locations:** `Assets/MaverickFresh/Scripts/FlightDynamics/README.md` ("Propulsion status"); `F16/F16_PROPULSION_REFERENCE_V0.1.md` (header); repository statements that Morelli fitted TP-1538 directly; "LEF fixed at 25°"; library entries `F16-GEO-*`, `F16-MASS-*`, `F16-ACT-*-LIM`, `F16-ENG-TABLE-VI`, `F16-ENG-HENG`, conflict `F16-C6`.
- **Modification:** state the Phase 5D opt-in thrust status; lineage TP-1538 → Stevens & Lewis simplified → Morelli / Garza & Morelli; "LEF position not stated by the fitted source"; record lead page verification (PDF SHA-256 `aae0ece6…`, pages 15, 43–46, 49, 51, 99).
- **Evidence:** docs 01–02 (lead PV).
- **Effect:** removes false statements; promotes repository readings to page-verified.
- **Tests:** library validator; no code change.

### H4 — F-16 engine gyroscopic term
- **Subsystem:** propulsion installation.
- **Location:** `F16/MavF16PropulsionInstallation.cs:78` (`NotAvailable`); shared path in `Core/MavEngineProfile.cs` / `Core/MavEngineRuntime.cs` (ENG-008).
- **Modification:** after one page re-check of the Garza & Morelli axis statement, set rotor angular momentum 216.9 kg·m²/s on the TP-1538 configuration only, axis +X body mapped through the README's axial-vector rule.
- **Effect:** gyroscopic yaw/pitch coupling `h × ω` during pitch/yaw rates.
- **Tests:** constant pitch-rate case gives the predicted yaw moment and sign; term off reproduces current baselines exactly.
- **Risks:** sign error (axial vector across the Unity/aero boundary) — the README warns that round-trip tests cannot catch it.

### H5 — Domain-violation visibility
- **Location:** `F16/MavF16AeroModel.cs` (`debugInputWasClamped`, `debugOutsidePublishedMachRange`), readiness/telemetry.
- **Modification:** count and surface clamped-aero steps and Mach > 0.6 steps in telemetry and run summaries (doc 06 G8/M13); do not change the aerodynamics.
- **Effect:** no silent flight outside the data.
- **Tests:** a scripted run above α 45° and above Mach 0.6 must report counts.

### H6 — TP-1538 tabular aerodynamic model (decision, then work package)
- **Location:** new F-16 aero model class beside `MavF16MorelliPolynomial`; data under `Docs/Reference/Data/F16/TP1538/`.
- **Modification:** two-pass visual transcription of Table III (≈ PDF pp. 51–97), with manifest, validator and cross-check against Morelli inside α −10…45°; implement the App. B build-up; selectable, off by default.
- **Evidence:** lead PV of App. B and Table III layout.
- **Effect:** α −20…90° at low speed.
- **Tests:** doc 01 §6.
- **Risks:** volume of transcription; LEF base-state ambiguity; no Mach effects — must be labelled low-speed only.

---

## 4. Blocked improvements (do not implement until data exists)

| Feature | Blocking gap | What would unblock it |
|---|---|---|
| F-16 transonic/supersonic aerodynamics | no public tabulated data for this configuration (TP-3355 tables are microfiche; other data are developmental configurations or plots) | a public tabulated dataset for a defined configuration, with the configuration change accepted explicitly |
| F-16 thrust below Mach 0.2 or above 1.0 | Table VI grid | a sourced table for the same simulated engine |
| Named production-engine thrust (F100-PW-200/-220/-229, F110) | no public deck tied to an F-16 configuration | a public installed deck with variant identity |
| Fuel burn / mass change (F-16, F-15) | no fuel-flow data; no mass schedule | sourced TSFC and mass schedule |
| Actuator rates/dynamics (F-16, F-15) | no primary source for this configuration (TP-1538 sim CAS figure values are moderate-confidence readings of a *simulated* actuator; library conflict F16-C1) | page transcription of TP-1538 figs. 62–65 with stated uncertainty, labelled "TP-1538 simulated actuator" |
| F-15 beyond one flight condition | MDC A4172 / Aerobase not public | public release of the underlying database |
| F-15 throttle/thrust deck | 836 engine build unknown; no dimensional deck | build identity + a public deck |
| Any F-22 model | nothing public | — |
| Production FCS fidelity (any type) | gains not public | — |

---

## 5. Long-term development path (Research D)

| Area | Possible now | Blocked |
|---|---|---|
| 1. Aircraft-specific data completeness | F-16 doc fixes, engine gyroscopics, TP-1538 α extension | Mach effects, production engines, fuel |
| 2. Extended envelope | high-α low-speed (F-16 via TP-1538) | transonic/supersonic for both aircraft |
| 3. Transonic/supersonic | none | data |
| 4. High-α | F-16 TP-1538 tables to 90°; F-15 trend validation | F-15 data beyond the AFIT transcription |
| 5. Engine/airframe coupling | gyroscopic term; thrust along +X (already) | thrust-line offsets, inlet effects |
| 6. Control effectiveness across conditions | identification of the existing laws/aero (docs 05–06) | Mach-dependent effectiveness |
| 7. Numerical verification | NESC Level 1; dt-convergence studies; discrete-loop eigenvalues | — |
| 8. Headless suitability | existing batch runner; script-stepped physics for training (Phase 1 doc 04) | — |
| 9. Multi-aircraft architecture | already present (profiles, laws, descriptors); add the NESC validation profile as a third configuration | — |

Sequencing: H3 and H5 (cheap, remove risk) → H1 and H2 (measurement and verification) → H4 → decision on H6 → H6/L3 if justified. A broader model is pursued only where it can be validated; a narrower validated model stays preferable to a broader unsupported one.
