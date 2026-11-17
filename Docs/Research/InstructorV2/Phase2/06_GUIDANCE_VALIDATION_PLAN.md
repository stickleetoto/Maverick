# 06 — Guidance Validation Plan

**Purpose:** a reproducible measurement procedure for Instructor v2 that the development agent can run, extending Phase 1 metrics M1–M13 (Phase 1 doc 06 §8) and the scenarios already present in the development branch.

**Principle:** a gate is pass/fail only where the property is *invariant* (ownership, finiteness, determinism, envelope bounds, fail-closed behaviour). Performance quantities (settling time, overshoot, damping, margins) are **measured, recorded and regression-diffed**, not judged against invented thresholds. That is the same discipline the repository already applies (V1/V2 records) and the only defensible one: no public source sets acceptance thresholds for a research law on a research model.

**Evidence classes:** MP throughout, built on doc 05 (methods), Phase 1 docs 03/06, and the development branch's local commit `90add24` (read only).

---

## 1. Where the development branch already stands

| Area | Development runner tag / test (commit `90add24`) | Status vs this plan |
|---|---|---|
| Runtime mechanics (one owner, one armed body, one law, one load per step, no pose write, finite, bounded rate-limited commands, surfaces in envelope) | `[RM]` | covers gate G1–G3 |
| Flight record (error at 0/25/50/75/100 %, bank range, TAS, α, \|β\|, n_z, energy height, limiters) | `[RF]` — reported, not scored | matches the "record, don't gate" principle |
| Tracking behaviour | `[RT]`: neutral hold, 15° L/R, 90° R, 180°, climb 8°, descent 8°, moving target 2 °/s, low speed, release, input loss | scenarios S3, S5, S7–S10 partly covered |
| Input timing | `[RI]`: 25/50/100 fps mouse traces vs the fixed step | covers G5 |
| Input loss | `[RL]`: aim camera lost at 3 s | covers G4 |
| Headless guidance tests | `MavMouseAimInstructorV2Validation` | pure-function checks |

**Gaps against the brief:** 5° and 10° corrections; high-speed turn; high-α response; actuator/command saturation characterisation; measured loop dynamics (identification, margins); per-scenario metric definitions consistent with Phase 1 M1–M13; dt-sensitivity with latency held constant.

---

## 2. Hard gates (pass/fail, invariant properties)

| Gate | Property | How |
|---|---|---|
| G1 | Ownership: exactly one pilot-command source bound, one law enabled, one body applying loads, one load application per step | existing `[RM]` + check that a second enabled `MavPilotCommandSourceBase` is reported |
| G2 | No physical writes from the instructor path (no AddForce/AddTorque, no velocity/pose writes, no surface writes) | source scan + runtime check (existing pattern `[W11]`) |
| G3 | All commands finite and inside ±1; surfaces inside envelope; command rate within the configured limit | `[RM]` |
| G4 | Fail-closed: invalid/NaN aim, focus loss, missing descriptor, non-finite state → no signal → law's `NeutralCommand`; inspector values never used | `[RL]` + NaN/∞ injection cases |
| G5 | Determinism: two runs byte-identical (pinned `captureFramerate`); the same mouse trace at 25/50/100 fps gives command sequences that agree within one render frame of aim (as the dev `[RI]` already states) | `[RI]` extended to commands |
| G6 | Idempotent polling: repeated `TryGetCommand` in one physics step returns the same command, advances state once | headless test |
| G7 | Regression: instructor OFF ⇒ frozen F-15 suites, V1/V2 headless and Play Mode, Baseline v1 unchanged byte for byte | existing gates |
| G8 | Model-domain honesty: every step outside the aero/thrust domain or with clamped aero inputs is counted and reported; none is hidden | telemetry + run summary |

---

## 3. Scenarios and procedures

Common setup: F-15 pilot-controlled rig at Table VII point 36 (trim 300.8 ft/s, α 17.49°), dt 0.02 s, `captureFramerate` 50, fresh rig per run, scripted aim direction (not live mouse) unless the scenario is about input, all values below as **scripted inputs, not performance targets**.

| ID | Scenario | Script | Duration | Primary metrics | Notes |
|---|---|---|---|---|---|
| S1 | **5° correction** (R, L, up, down) | aim steps 5° at t = 1 s | 20 s | M1, M2, M4, M9 | new; tests the small-error regime (Phase 1 doc 03 §5.1) |
| S2 | **10° correction** (R, L, up, down) | 10° at t = 1 s | 20 s | M1, M2, M4, M5 | new |
| S3 | 15° correction | as dev `[RT]` | 25 s | same | existing |
| S4 | **90° direction change** (R, L, up, down) | 90° at t = 1 s | 40 s | M1, M3, M5, M6, M7, M11 | dev has 90° R only |
| S5 | **180° reversal**, with and without prior aim history | target behind; (a) step; (b) aim swept through 180° over 3 s | 60 s | M8, M9, M3, M7 | dev has the step |
| S6 | **High-speed turn** | start from a validated higher-speed research trim inside the domain (e.g. the 360–400 ft/s trims V2 evaluated); 45° lateral step | 40 s | M1, M2, M11, M7, M13 | new; inside the F-15 domain only (≤ 699.7 ft/s); report proximity to the gameplay stabilator edge (V2 §5.5) |
| S7 | **Low-speed turn** | from the slowest validated trim (~265 ft/s); 45° lateral step | 40 s | M6, M13, M7, shortfall `n_cmd − n_z` | dev has a low-speed case; verify the soft caps engage before the 218.5 ft/s domain floor |
| S8 | **Command release** | 30° turn, aim returned to current flight path at t = 10 s | 30 s | M10*, free-response damping (doc 05 §4.4 ERA/log-decrement) | dev "release" |
| S9 | **High-AoA response** | aim 20° above the flight path at the slowest trim, held | 30 s | M13, α max, soft-cap activity, M5 | new; must show the α soft cap (24–28° in the dev config) acting **before** the data region thins; never claims post-stall |
| S10 | **Actuator / command saturation** | aim steps of 30°, 60°, 90° and a 0.5 Hz aim oscillation of growing amplitude | 60 s | M6, M9, saturation onset, rate-limit counts | new; describes when commands stop scaling linearly |
| S11 | **Invalid or lost input** | NaN aim, ∞ aim, zero vector, focus loss, camera loss at 3 s; restore at 6 s | 12 s | G4 | dev covers camera loss |
| S12 | Moving target | constant 2 °/s and a 0.1 Hz sinusoidal target | 40 s | steady tracking error, phase lag | dev has 2 °/s |
| S13 | **Identification runs** (doc 05 §5) | multisines on pitch/roll/yaw commands, on desired bank, and on aim; 0.5×/1×/2× amplitudes | 3 cycles of lowest frequency + fade-in | M14–M17 | new |
| S14 | **dt sensitivity** | S2, S4, S5 repeated at dt 0.01 with the latency held at 0.02 s | as source | convergence of M1–M9 | new; doc 05 §8 |

---

## 4. Metrics

### 4.1 Reused from Phase 1 (definitions unchanged)

| ID | Definition |
|---|---|
| M1 | time until the aim error falls below 2° (and 5°) |
| M2 | overshoot: maximum error after first entering the 2° band, and the number of lateral-error sign changes |
| M3 | bank-capture overshoot `max \|μ − μ_des\|` after first reaching `μ_des` |
| M4 | roll reversals: sign changes of the roll command beyond a deadband per manoeuvre |
| M5 | `max \|β\|` |
| M6 | fraction of steps with any surface, gameplay envelope or law limiter saturated; stick-clamp flags |
| M7 | ΔE_s and mean P_s over the manoeuvre (reported, never optimised away) |
| M8 | turn-side changes during a reversal |
| M9 | maximum per-step change of each stick axis and each physical command |
| M10 | surface-envelope violations (must stay 0; it is gate G3) |
| M11 | peak n_z vs the instructor's n ceiling (and the law's limit where one exists) |
| M12 | maximum command step while the aim crosses the zenith |
| M13 | model-domain excursions and clamped-aero steps (count, duration) |

*M10 is a gate, kept in the list for continuity.

### 4.2 New metrics (each measures a behaviour M1–M13 do not)

| ID | Quantity | Method | Why distinct |
|---|---|---|---|
| M14 | identified inner closed-loop parameters per axis: `τ_R, τ_eff` (roll), LOES `ω_e, ζ_e, τ_eq` (pitch), β loop ω/ζ | doc 05 §4.1, §4.4 | M1–M9 are outcomes; M14 is the plant the guidance acts on |
| M15 | bank-capture closed-loop `τ_r` and the outer/inner ratio `ω_c,outer / ω_bw,inner` | doc 05 §3, §4.3 | quantifies loop separation instead of assuming it |
| M16 | gain margin, phase margin, `M_s`, delay margin per broken loop | doc 05 §4.6 | robustness, which time responses can hide |
| M17 | linearity index: max difference between frequency responses at 0.5× and 2× amplitude | doc 05 §3 | detects saturation and nonlinearity before it shows as overshoot |
| M18 | command rate-limit occupancy: fraction of steps at the instructor's own command rate limit | telemetry | rate saturation adds lag and PIO risk (Phase 1 doc 02 §4) |

---

## 5. Telemetry requirements

Per physics step (CSV, one file per run, header block as doc 05 §5):

| Group | Fields |
|---|---|
| Identity | commit, configuration ID, law name, guidance config hash, aim reference (flight path / nose), dt, captureFramerate, scenario ID, input-design file SHA-256 |
| Aim | aim direction (world), error to nose and to flight path, turn side, guidance mode, hysteresis latches |
| Guidance internals | desired bank, bank error, lift request (g), pull weight, commanded flight-path rate, soft-cap activity and which cap |
| Commands | stick pitch/roll/yaw/throttle before and after filter and rate limit; clamp flags |
| Law internals | commanded q / p_s / β, measured q / p_s / β, gain scale |
| Surfaces | requested and actual per channel; envelope-limit flags |
| State | p, q, r, p_s, r_s, α, β, φ, θ, ψ, flight-path angle, V, Mach, q̄, n_z, h, E_s |
| Validity | aero domain flags, clamped-input flags, thrust domain flags, readiness/owner state, signal state |

---

## 6. Running the plan (handoff to the development agent)

1. Add S1, S2, S4 (L/up/down), S6, S9, S10 to the existing Play Mode runner as scripted aim cases; keep `[RF]`-style "reported, not scored" lines for M1–M9, M11, M13.
2. Add a validation-only multisine command source (S13) and a validation-only wrapper law for broken-loop margins (doc 05 §3). Neither may be wired into production rigs; both must be listed in `Tools/fdm_validation_baseline_v1.json` (repository rule).
3. Write identification results to the CSV/JSON format above; record a **first baseline** of M14–M17 at point 36 and at two other trims.
4. Use M15 to revisit `K_θ` (doc 05 §6) and document the change with before/after M1–M5 and M16.
5. Run S14 (dt sensitivity) for any change that alters the discrete loop (filters, latency, step).
6. Human flight test only after G1–G8 pass; brief the tester that the F-15 research rig is slow by construction (Phase 1 doc 05 C4) and log the same telemetry.

---

## 7. Acceptance language

The report for any instructor change states:
- which gates passed (G1–G8, binary);
- the measured metrics (M1–M18) **as numbers with their run identity**, and their difference from the previous baseline;
- which numbers are outside the model's validity domain (M13) and therefore not evidence;
- what was not run.

No sentence of the form "the aircraft now handles realistically" is acceptable without the measurements that support it. A measurement shows how the model behaves; only validation data shows whether that matches an aircraft.
