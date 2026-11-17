# 05 — Findings and Severity

**Audited commit:** `ad348086e09f7761e5e3e3ecef7cb45f97e8cff0`.

**Status tags:**
- **VERIFIED-DYN**: reproduced in Unity Play Mode through the PR's own rig;
- **VERIFIED-PURE**: reproduced by running the production pure function;
- **VERIFIED-STATIC**: code reading;
- **HYPOTHESIS**: not tested.

**Evidence paths** are relative to `Docs/Reviews/InstructorV2/evidence/`.

**Severity scale** (per the assignment):

| Level | Meaning |
|---|---|
| BLOCKER | Unsafe or invalid behavior that should prevent human testing or integration |
| HIGH | Significant correctness, ownership or numerical defect |
| MEDIUM | Behavioral flaw or insufficient validation |
| LOW | Limited impact |
| IMPROVEMENT | Not a defect |

| ID | Severity | Status | Title |
|---|---|---|---|
| F-01 | **BLOCKER** | VERIFIED-DYN | No upper-airspeed protection: a held dive aim leaves the model domain permanently |
| F-02 | **HIGH** | VERIFIED-DYN | Held steep-dive targets produce non-converging, saturated rolling at 3–4 g |
| F-03 | **HIGH** | VERIFIED-PURE + DYN | Desired-bank singularity near a zero-g lift request → roll limit cycle in offset descents |
| F-04 | **HIGH** | VERIFIED-DYN | Outer bank/heading loop is lightly damped (ζ ≈ 0.03–0.08), contradicting its documented rationale |
| F-05 | **HIGH** | VERIFIED-DYN | `DropCommandSignal` turns signal loss into a gravity-free, drag-free tumble; restore can be out of domain |
| F-06 | MEDIUM | VERIFIED-DYN | Unreachable climb targets drive the aircraft into a steeper glide than releasing the stick |
| F-07 | MEDIUM | VERIFIED-DYN | Moving-target check is labelled "not growing" but does not test growth; the lag grows to ≈ 30° |
| F-08 | MEDIUM | VERIFIED-STATIC | Tracking checks assert halving/approach/sign, not convergence; climb/descent rescored after failure; energy check cannot detect a source |
| F-09 | MEDIUM | VERIFIED-STATIC | Coverage: one trim, ≤ 60 s, no dives, no high speed, no long-loss restore, no damping/margins |
| F-10 | MEDIUM | VERIFIED-STATIC | Report and test labels contradict the code and the evidence (integral, damping, residual cause, nadir label) |
| F-11 | LOW | VERIFIED-STATIC + DYN | Mouse overlay has no aero/thrust-refusal warning (the F-15 HUD does) |
| F-12 | LOW | VERIFIED-DYN | Neutral stick = attitude-holding drift, not wings level; verified stable at one trim only |
| F-13 | LOW | VERIFIED-PURE | Re-anchoring under the ±85° clamp leaves up to 4.5° of error on resume |
| F-14 | LOW | VERIFIED-STATIC | Cursor capture on Mouse0 will collide with a fire button in production |
| F-15 | LOW | VERIFIED-STATIC | Camera rig binds the first source found (multi-aircraft ambiguity) |
| F-16 | LOW | VERIFIED-STATIC | No runtime config validation; `commandRatePerSec = 0` silently disables rate limiting; copied stick scale can go stale |
| F-17 | LOW | VERIFIED-STATIC | Guidance state not reset on scripted ↔ mouse switch or policy change |
| F-18 | IMPROVEMENT | — | Use the air-relative state (body) rather than inertial velocity, ready for wind |
| F-19 | IMPROVEMENT | — | Turn-side memory never expires |

---

## F-01 — BLOCKER — No upper-airspeed protection; held dive leaves the model domain permanently

- **Where.**
  - `Core/MavMouseAimGuidance.cs:135-152`: the soft-cap config has low-speed and α terms only.
  - `Core/MavMouseAimGuidance.cs:492-519`: the caps applied.
  - `Core/MavMouseAimGuidance.cs:55-60`: the claim "keep the requested pilot intent away from the edges of the aerodynamic model's admitted domain".
  - `F15/MavF15PilotControlledAeroModel.cs:18-22,171-175`: refusal returns zero outside 218.5–699.7 ft/s.
  - `Core/MavMouseAimInput.cs:30`: the mouse can aim to −85°.
- **Reproduction.** MouseAim rig, Assisted V2, from the trim; aim −85° below the initial flight path (mouse-reachable) and hold for 90 s. `probe_batch2/probe_DIVE85_HOLD_90S.csv`.
- **Evidence.**
  - TAS 211.3 m/s at t = 30 s, 213.8 m/s at t = 30.9 s.
  - First aero **and** thrust refusal at 30.86 s.
  - 2,958 / 2,958 remaining steps refused; zero samples back in domain.
  - Afterwards: TAS 780 m/s, α −179.7…+179.8°, |β| 75°, Δh −32,260 m.
  - Readiness stays live-ready; owner stays `F15PilotControlledResearch`.
  - DIVE60 reached 200.7 m/s in 60 s.
- **Expected.** The instructor's caps keep commands away from **every** documented domain edge, including the TAS ceiling, as they do for the floor. The aircraft never leaves the domain through the instructor's request alone.
- **Actual.** It requests a steeper dive until the model domain is exited. With zero aero, nothing can slow the aircraft, so the state is unrecoverable.
- **Root cause.** The upper-speed edge was omitted from the soft-cap set. The guidance has no energy or speed awareness on the push side.
- **Minimal fix.** Add a high-speed cap symmetric to the low-speed one, labelled `MAVERICK_TUNED_NON_AUTHORITATIVE`:
  - between e.g. 185 and 200 m/s, raise the push floor and the nose-down part of the flight-path request toward a nose-up recovery (reduce the commanded descent angle);
  - flag `HighSpeedCap`;
  - add an overlay warning (see F-11).

  Do **not** alter the aero refusal or ownership.
- **Regression test.** Play Mode: hold aims at −30/−60/−85° for 120 s from the trim. Assert:
  - TAS max < 213.3 m/s minus a margin;
  - aero-refused steps == 0;
  - the `HighSpeedCap` flag was seen.

  Headless: a cap table at 180/195/205 m/s.

## F-02 — HIGH — Held steep-dive targets: non-converging saturated rolling at 3–4 g

- **Where.**
  - `Core/MavMouseAimGuidance.cs:533-566`: lift request, inverted-roll phase-in, bank rule.
  - `:177`: `maxPushStick = 0.6`.
  - `F15/MavF15PilotControlLawV2.cs:81-84,258-263`: no pitch integrator, fixed trim bias.
- **Reproduction.** Aim −60° (or −30°) from the trim, hold for 60 s. `probe_batch1/probe_DIVE60_HOLD_60S.csv`, `probe_DIVE30_HOLD_60S.csv`.
- **Evidence.**

  | | DIVE60 | DIVE30 |
  |---|---|---|
  | \|roll cmd\| ≥ 0.99 | 96 % of samples | 44 % |
  | Bank | sweeps through ±180° (51 samples at \|bank\| ≥ 150°) | — |
  | n_z | 0.8–3.9 g | ≤ 3.07 g |
  | α | 14.2–14.8° throughout | — |
  | Path | — | −69.9…+7.2° |
  | Error | cycles 14–57° | 30° at 60 s |
  | Δh | −5,621 m | — |

- **Expected.** A dive target the aircraft cannot hold is approached and then held off at a bounded error (an energy/speed-limited degradation), as in LOW-SPEED CLIMB. No continuous roll cycling.
- **Actual.** At rising q̄ the aircraft cannot unload with ≤ 0.6 push: V2 trims to its fixed bias, so α stays ≈ 14° and lift grows with q̄. The gravity-compensated bank rule then rolls to aim the unavoidable lift at the target (inverted above 30–60° of error), overshoots, and repeats.
- **Root cause.** The guidance assumes an ideal rate tracker. It has no speed-dependent pitch authority model and no limit on commanded descent.
- **Minimal fix.**
  1. The F-01 high-speed cap; it also bounds this.
  2. Limit the commanded flight-path depression to a speed-dependent achievable value. Clip the aim's elevation relative to the horizon before the geometry, with a reported limiter.
  3. Add a roll-reversal guard: do not command inverted roll while |n_z| > a threshold and the error is shrinking.
- **Regression test.** Held −20/−30/−45/−60° dives for 60 s. Assert:
  - roll-command saturation fraction below a recorded bound;
  - no |bank| > 150° unless the error exceeds 90°;
  - the error approaches a bounded value with no more than N cycles.

## F-03 — HIGH — Desired-bank singularity near zero-g lift request → roll limit cycle

- **Where.**
  - `Core/MavMouseAimGuidance.cs:533`: `liftRequest`.
  - `:536-550`: the deferral subtracts the downward component.
  - `:556-566`: `bankError = atan2(f·s, f·l)`; `liftWeight` scales only the roll rate.
- **Reproduction (pure).** Wings level, level path, α 17.5°, aim 8° down and 1–2° left at 91.7 m/s. `probe_batch1/pure_probe.txt` [K1]/[K3].
- **Reproduction (dynamic).** Aim 8° down and 0.5° or 2° left of the trim path. `probe_batch2/probe_DESC8_LAT0p5L.csv`, `probe_DESC8_LAT2L_40S.csv`; `probe_batch1/probe_DESC12_LAT3R.csv`.
- **Evidence.**

  Desired bank at 91.7 m/s, 8° down:

  | Lateral offset | Desired bank |
  |---|---|
  | 0.25° | −6.4° |
  | 1° | −24.2° |
  | 2° | −42.0° |

  - At ≥ 12° down (91.7 m/s) or ≥ 8° down (150 m/s): **−90° for any lateral offset ≥ 0.25°**.
  - At 1° lateral, the desired bank jumps from −24° (8° down) to −78° (10° down).
  - Dynamic: bank −35..+22° with 0.5° lateral (7 roll-command reversals in 20 s); −65..+69° with 2° lateral, growing over 40 s (20 → −18 → 20 → −25 → 39 → −59 → 69 → −65°); heading error ±13.6°. Same descent with zero lateral: bank 0.
- **Expected.** The desired bank is continuous and bounded in the aim direction. A sub-degree lateral offset asks a sub-degree-scale bank.
- **Actual.** Lateral sensitivity grows without bound as |f_req| → 0. The deferral makes the request purely lateral, so the bank goes to ±90°.
- **Root cause.** The rule "point the lift at f_req" is singular at f_req = 0, and the inverted-roll deferral drives f_req toward that singularity for moderate nose-down errors.
- **Minimal fix.** Regularize the bank direction:
  - compute the bank error from `atan2(f·s, max(f·l, k·g))` (a lift-up floor), or
  - fade the **desired bank** (not just the roll rate) to wings level as |f_req| / g → 0, and keep a vertical floor in the deferral instead of removing the whole downward part.
- **Regression test.**
  - Headless: a grid of down 0–30° × lateral ±0–4° at 90/150/200 m/s. Assert |desired bank| ≤ a continuous bound, e.g. |Δbank / Δlateral| ≤ L.
  - Play Mode: DESC8 with 0.5° and 2° lateral. Assert the bank stays within a small envelope and the reversal count is ≤ 2.

## F-04 — HIGH — Lightly damped outer loop (ζ ≈ 0.03–0.08) vs documented "≈ 0.45"

- **Where.**
  - `Core/MavMouseAimGuidance.cs:161-162`: K = 0.6, τ_bank = 1.
  - `:218`: the rationale "keeps damping near 0.45".
  - Report `Docs/FlightDynamics/REALISTIC_MOUSE_INSTRUCTOR_V2_REPORT.md:232,288`: residual attributed to energy; "real fix needs thrust management".
- **Reproduction.** Aim 5° or 15° right of the trim path; reversal 180°. `probe_batch2/probe_TURN5R_30S.csv`, `probe_TURN15R_40S_BASE.csv`, `probe_TURN15R_40S_K0.3.csv`, `probe_TURN15R_40S_TAU2.csv`; `probe_batch1/probe_REVERSAL180_120S.csv`, `probe_HANDSOFF_IN_TURN_120S.csv`.
- **Evidence.**

  | Case | Period | ζ (log decrement of bank extrema) |
  |---|---|---|
  | 5° turn | 5.7 s | 0.072 (±14° → ±2°) |
  | 15° turn | 6.8 s | 0.078 (±18° → ±2.6°) |
  | 180° reversal tail | 5.2 s | 0.034 (±20° → ±2.3° over 55 s) |
  | τ_bank = 2 | — | 0.135 |
  | **K = 0.3** | — | **no oscillation**; final error **0.0°** at 40 s (vs 3.9°); TAS min 90.4 vs 84.3 m/s |
  | No pedal | — | 0.107 |
  | Hands-off | — | bank decays monotonically (so the oscillation is the guidance loop) |

- **Expected.** Damping consistent with the documented rationale, and no persistent bank rocking after small corrections. The residual explanation should be supported by evidence.
- **Actual.** Every turn ends in tens of seconds of ±5–20° bank rocking. The documented damping figure is unsupported. The "energy deficit" explanation is refuted for the 15° case.
- **Root-cause hypothesis.** The effective lag of the V2 roll/yaw closed loop plus the filter and rate limit is much larger than the 1-s bank time constant assumed. With an unidentified inner loop, K = 0.6 is too high. This needs the identification in PR #36 doc 05 §4.1/§4.3 to confirm.
- **Minimal fix.**
  - Lower K (K = 0.3 is demonstrated) or add rate damping to the outer loop (e.g. subtract a flight-path-rate or bank-rate term).
  - Then **measure** ζ and record it; replace the "0.45" text with the measured value.
- **Regression test.** Play Mode 5°/15° R/L. Record the log-decrement ζ, the number of bank reversals > 2° after the first capture, and the final error. Gate on "no more than one overshoot" or a recorded ζ baseline (PR #36 M2/M3/M15).

## F-05 — HIGH — `DropCommandSignal`: gravity-free coast and tumble; restore can be out of domain

- **Where.**
  - `Core/MavMouseAimPilotCommandSource.cs:19-23,405-415,465-473`: the policy.
  - `Core/MavSixDoFBody.cs:294-299,966-969,1080-1110`: withholding.
  - `Core/MavFlightPhysicsOwnership.cs:415-420`: gravity stays assigned to the (withheld) load set.
  - Play Mode check: `Validation/MavMouseAimInstructorV2FlightValidationRunner.cs:599-602`.
- **Reproduction.** In a 25° turn, set `inputLossPolicy = DropCommandSignal`, remove the camera at 3 s, restore at 6/13/18 s. `probe_batch1/probe_DROP3_RESTORE.csv`, `probe_DROP10_RESTORE.csv`; `probe_batch2/probe_DROP15_RESTORE.csv`.
- **Evidence.**
  - During the loss, velocity and body rates are frozen exactly (p 11.98, q 3.661, r 8.000 °/s; TAS 90.864 m/s; path −3.301°) while the attitude rotates.
  - By 13 s: α 53.5°, |β| 18°. By 18 s: |β| 33.4°.
  - Restore after 15 s: **99 steps of aero refusal** (n_z 0), then recovery with a dive to −32.8° and Δh −806 m.
  - Restore after 10 s: inverted (bank 175°), |ω| 45.6 °/s, n_z 2.0, Δh −631 m.
  - The owner never faults.
  - The PR's own Play Mode record already shows α 33.6° / bank 77° after 3 s, and scores PASS.
- **Expected.** Airborne signal loss leaves the aircraft physically flying (gravity, aero) with controls at a defined safe state, and restores into the same physics.
- **Actual.** Physics keeps integrating with **no** loads: no gravity, no drag, no aerodynamic damping. Restore happens at an arbitrary, possibly out-of-domain state.
- **Root cause.** The policy reuses an arming gate as a runtime response. The gate is correct for arming and must not be weakened.
- **Minimal fix.** Remove `DropCommandSignal` from what a live, airborne rig may select (the rig refuses it, or the source maps it to `NeutralStickHoldThrottle` while armed). Delete or rescope `HoldAimTarget` as a time-limited option. Do not change readiness or ownership. If a "freeze" is wanted, design an explicit pause outside the FDM.
- **Regression test.**
  - Play Mode: losses of 3/10/30 s under every policy, then restore. Assert aero refused == 0 after restore and the state is inside α/β/TAS bounds.
  - Headless: the rig refuses `DropCommandSignal` while armed.

## F-06 — MEDIUM — Unreachable climb targets end in a steeper glide than hands-off

- **Where.** `Core/MavMouseAimGuidance.cs:521-528` (rate request vs the cap), `:497-502` (low-speed cap).
- **Reproduction.** Aim 5° above the trim path, 30 s (`probe_batch2/probe_TURN5UP_30S.csv`); 80° (`probe_batch1/probe_CLIMB80_HOLD_60S.csv`); PR CLIMB 8 / LOW-SPEED CLIMB 40.
- **Evidence.**

  | Target above the trim path | End state |
  |---|---|
  | 5° | −13.7° path at 75 m/s; error 5 → 15.8° |
  | 80° | steady −14.5° glide |
  | Hands-off (reference) | −2.9° at 91.7 m/s |

- **Expected.** An unreachable climb is approached at best and held near the best sustainable path. It is not made worse than neutral.
- **Actual.** The guidance pulls until the low-speed cap; the equilibrium at the cap is a steeper descent. Physically legitimate (the back side of the drag curve at fixed thrust), but poor intent mapping.
- **Root cause.** No energy awareness. Pull persists while the specific excess power is negative.
- **Minimal fix.** Limit the commanded climb angle to the sustainable path estimated from the measured specific-energy rate (the source already computes `debugSpecificEnergyRateMps`), or relax the pull toward the trim path while the low-speed cap is active.
- **Regression test.** A 5° climb aim held 30 s: the end path is ≥ the hands-off path (or flagged as energy-limited with the cap inactive).

## F-07 — MEDIUM — Moving-target "bounded, not growing" is not tested; the lag grows to ≈ 30°

- **Where.** `Validation/MavMouseAimInstructorV2FlightValidationRunner.cs:527-535`; report table row 10 and §5.1.
- **Reproduction.** The PR's MOVING TARGET run extended to 120 s (`probe_batch1/probe_MOVING_2DPS_120S.csv`).
- **Evidence.**
  - The check tests `max − min < 3°` and `max < 20°` over the last 10 s.
  - The PR's own quartiles are 4.1 → 4.6 → 5.8 → 7.8°.
  - At 120 s the lag is 15.0° (50 s), 19.6° (60 s), 26.4° (70 s), then a plateau of ≈ 29.9° (dAz −28°, dEl −11°) at TAS 81 m/s, the low-speed-cap equilibrium.
- **Expected.** The label matches the assertion; growth is tested.
- **Actual.** A growing lag passes, labelled "not growing".
- **Fix.** Assert the slope (e.g. a linear fit over the last 20 s), run ≥ 120 s, and report the steady state.
- **Regression test.** As in the fix.

## F-08 — MEDIUM — Tracking checks do not assert convergence; climb/descent rescored; energy check blind

- **Where.** Runner `:497-547`; report §5.3 steps 3–4 (`REALISTIC_MOUSE_INSTRUCTOR_V2_REPORT.md:271-272`); runner `:627-631`.
- **Evidence.**
  - Turns pass on `eEnd < 0.5·e0`, so 90° passes at 44°.
  - The reversal passes on its minimum.
  - Descent passes with a 10 % margin.
  - "Energy end < start" holds for **any** run that is not a power-positive dive: the neutral trim loses 94 m in 20 s.
- **Impact.** The suite cannot catch a regression in convergence, hold capability or a small spurious energy source.
- **Fix.**
  - Keep the invariant gates.
  - Add recorded-baseline metrics (PR #36 doc 06 M1–M9, M11, M13) with diff-against-baseline gating.
  - Add a specific-power consistency check: dE_s/dt ≈ (T − D)·V/W from the load set.
- **Regression test.** Same as the fix.

## F-09 — MEDIUM — Coverage gaps

The gaps are listed in `04_VALIDATION_AUDIT.md` §7:
- one trim;
- no high speed, no dives;
- no 5°/10° corrections, no 90° L/up/down;
- no damping or margins;
- ≤ 60 s runs;
- no long-loss restore;
- no < 25 fps;
- no dt sensitivity.

Each of F-01 to F-07 lives in one of these gaps.

**Fix.** Implement PR #36 doc 06 scenarios S1, S2, S4, S6, S8, S10, S11 (with restore), S12, S14 before any human acceptance claim.

## F-10 — MEDIUM — Documentation and labels contradict code and evidence

| Location | Says | Code / evidence says |
|---|---|---|
| Report §1 (`:25`), §3.3 formula (`:91`, "+ I"), §3.4 (`:108`, "Fix. … Ki = 0.15") | the pitch integral is the adopted fix | `pitchIntegralGainPerSec2 = 0` (`MavMouseAimGuidance.cs:164`); §5.3 says "not adopted" |
| `MavMouseAimGuidance.cs:218` | K = 0.6 keeps damping near 0.45 | measured 0.03–0.08 (F-04) |
| Report `:232`, `:288` | the residual ≈ energy deficit; thrust needed | K = 0.3 converges at fixed thrust (F-04) |
| Headless `[MI4]` label (`MavMouseAimInstructorV2Validation.cs:295`) | "pull rather than push" | the assertion requires a push at the −0.6 limit |
| Runner `[RT]` moving-target label | "not growing" | growth not tested (F-07) |
| Report §3.7, last row | a disabled source is safe ("live-readiness withholds loads") | same gravity-free coast as F-05 (pre-existing behavior) |

**Fix.** Correct the text. **Test:** none; review.

## F-11 — LOW — Mouse overlay lacks an aero/thrust-refusal warning

- **Where.** `Core/MavMouseAimPilotCommandSource.cs:570-581`.
- **Evidence.** In DIVE85 the overlay would read `live-ready YES` with loads applied and no warning while the aero is refused. The F-15 HUD (F1) does print `REFUSED - no aero/thrust load` (`F15/MavF15PilotControlledDiagnostics.cs:151`).
- **Fix.** Read `aero.debugRefused` / `thrust.debugRefused` (or the diagnostics) and print a WARNING line.

## F-12 — LOW — Neutral stick is attitude-holding drift

- **Evidence.** HANDSOFF / INPUTLOSS 120 s: bank 49° → 0° in ≈ 100 s, monotonic; phugoid ≈ 49 s period, ζ ≈ 0.1; Δh −727 m. **Spiral-divergence hypothesis refuted at this trim.**
- **Risk.** A loss in a dive continues the dive (→ F-01).
- **Fix.** Document. Optionally add a wings-level, path-to-horizon recovery mode for input loss, explicitly labelled as guidance.
- **Test.** Long hands-off at 2–3 trims and from a 30° dive.

## F-13 — LOW — ±85° clamp in re-anchoring

- **Where.** `Core/MavMouseAimInput.cs:53`; `Core/MavMouseAimPilotCommandSource.cs:451-463`.
- **Evidence.** Path 88° → residual 3°; path 89.5° → 4.5° (`pure_probe.txt` [E1]).
- **Fix.** Anchor with a 89.9° limit, or carry an aim frame that is not clamped.
- **Test.** Headless anchor at 86/88/89.5°: the residual must be < 0.01°.

## F-14 — LOW — Mouse0 cursor capture

- **Where.** `Core/MavMouseAimPilotCommandSource.cs:92,288-294`.
- **Impact.** Collides with a fire binding at integration.
- **Fix.** A configurable or dedicated key at integration time.

## F-15 — LOW — First-found source binding

- **Where.** `Core/MavMouseAimCameraRig.cs:144-147`.
- **Fix.** Explicit binding by the rig or setup tool. Refuse to bind when more than one source exists.

## F-16 — LOW — Runtime config robustness

- `IsSelfConsistent` is never called at runtime.
- `commandRatePerSec = 0` gives `maxStep = +∞` (`MavMouseAimGuidance.cs:641`).
- The stick scale is copied once per bind (`F15/MavF15PilotControlledRig.cs:236-237`).

**Fix.** Validate on enable and bind; refuse (or flag) an inconsistent config; re-sync the scale each step from the bound law.

## F-17 — LOW — No reset on source/policy switch

- **Where.** `Core/MavMouseAimPilotCommandSource.cs:256-261` (`ClearScriptedTarget`); policy assignment.
- **Fix.** Reset the guidance state on those transitions, as the handoff §3A asks.

## F-18 — IMPROVEMENT — Use the air-relative state

The guidance derives the flight path and α from `Rigidbody.linearVelocity` (`MavMouseAimGuidance.cs:430-432`; source `:384`). Use the body's `debugState` air-relative velocity so that future wind cannot split guidance α from model α.

## F-19 — IMPROVEMENT — Expire turn-side memory

`lastTurnDirectionWorld` (`MavMouseAimGuidance.cs:486-490`) persists through hands-off and long straight flight. Clear it after hands-off or after N seconds near zero error.

---

## Hypotheses requiring further tests

| ID | Hypothesis | Test needed |
|---|---|---|
| H1 | Damping (F-04), the singularity (F-03) and dive cycling (F-02) are worse at higher-speed trims (higher q̄, scheduled V2 gains) | PR #36 S6 at 360–400 ft/s trims |
| H2 | A human in the loop (≈ 0.1–0.3 s neuromuscular lag) on a ζ ≈ 0.07 outer loop will excite PIO-like bank rocking during fine aiming | Supervised human session with telemetry; identification first |
| H3 | DESCENT 8's +19 m energy gain is physical (T > D at α ≈ 10–11°) | Power balance from the load set per step |
| H4 | `MavFreshInput` delivers a large delta on refocus or cursor re-lock | Interactive editor test; per-frame delta clamp |
| H5 | dt 0.01 with latency held changes M1–M9 beyond the error bounds | PR #36 S14 |
| H6 | < 25 fps rendering degrades the snapshot bound | `[RI]` at 10/15 fps |
| H7 | A `HoldAimTarget` loss with a frozen steep-dive aim reproduces F-01 with no recovery input possible | Play Mode: HoldAimTarget + −60° aim + camera loss |
