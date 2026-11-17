# 02 — Physics and Control Audit

**Audited:** `Core/MavMouseAimGuidance.cs` @ `ad34808` (778 lines, read in full), driven against the unchanged `F15/MavF15PilotControlLawV2.cs` and the frozen research aero.

**What "correct" means here.** The guidance being a correct implementation of its own equations is not the same as the aircraft being a real F-15. The plant is the AFIT/Baumann/Davison research model with:
- fixed density (20,000 ft);
- fixed 8,300 lbf research thrust;
- one trim (Table VII point 36: 91.7 m/s, α 17.5°, a −2.9° fixed-thrust descent);
- a Maverick-tuned rate law.

Nothing below is a claim about the real aircraft.

**Evidence tags.**

| Tag | Meaning | Where |
|---|---|---|
| **[V-static]** | Verified by reading the code | — |
| **[V-pure]** | Verified by running the production `MavMouseAimGuidance.Step` | `evidence/probe_batch1/pure_probe.txt` |
| **[V-dyn]** | Verified in Unity Play Mode through the PR's own MouseAim rig | `evidence/probe_batch*/` |
| **[H]** | Hypothesis, not tested | — |

---

## A. Physical correctness

### A.1 Guidance never alters pose, velocity, force or moment — **holds** [V-static][V-dyn]

The output type has only normalized pitch/roll/yaw and telemetry (`Core/MavMouseAimGuidance.cs:343-370`). Across 24 probe runs (63,000 physics steps), `loads == steps` with no duplicate rejection, except in the three `DropCommandSignal` runs, where loads were withheld by design.

### A.2 Sign and axis conventions — **correct** [V-static][V-pure]

| Item | Code | Result |
|---|---|---|
| Body axes | `FromPose` (`:327-339`): nose = R·(+Z), right = R·(+X), up = R·(+Y) | Matches the aero-body mapping (+X fwd, +Y right, +Z down = −Unity up) |
| Bank | `BankAboutReferenceDeg` (`:734-741`) | + right wing down |
| Bank error | atan2(f·wing, f·lift) (`:556`) | Positive when the requested lift is to the right, so roll right (+1). Correct |
| α | atan2(−v·up, v·nose) (`:430-432`) | Positive when the velocity is below the nose. Correct |
| Yaw | `fineAimYawPerDeg × lateralError` (`:599`) | Lateral error > 0 is the target to the right, so pedal right (+). Correct |

PR checks `[MI2]` (headings 0/90/180/270/37) and `[MI13]` (aileron sign through the real rig) cover this. Nothing was found wrong.

### A.3 Bank-to-turn geometry and gravity compensation — **equations correct; one singularity** [V-static][V-pure]

**The algebra.** `f_req = V·w_des·d − g_⊥` with `bank = atan2(f·s, f·l)` and `pitch = w_des (d·l)`. For a horizontal turn at the desired bank `tan φ = V w / g`, the commanded body pitch rate `w sin φ` is exactly the level-turn `q = ψ̇ sin φ`. At other banks it gives the right sign of altitude error:
- under-banked → excess lift → climb;
- over-banked → sink.

`[MI9]` confirms this at one point, and the derivation holds in general.

**The singularity (F-03).** When the nose-down part of `V·w_des·d` approaches `g`, `|f_req|` → 0 and its direction becomes undefined. `liftWeight` (`:564-566`) fades only the roll **rate** gain for requests under 0.25 g. It does not fade the **desired bank**, which stays a full atan2 of two small numbers.

The `InvertedRollDeferred` rule (`:536-550`) makes this worse. It subtracts the whole downward component below a 30° error, so whatever lateral component is left becomes the entire lift request.

Pure-function results (wings level, level path, α 17.5°):

| V (m/s) | Target | Desired bank | Raw roll | Note |
|---|---|---|---|---|
| 91.7 | 8° down, 0.25° left | −6.4° | | |
| 91.7 | 8° down, 1° left | −24.2° | | |
| 91.7 | 8° down, 2° left | −42.0° | −2.8 (saturated) | |
| 91.7 | ≥ 12° down, any lateral ≥ 0.25° | **−90°** | | deferral active |
| 150 | 6° down, 0.25° left | −45.7° | | |
| 150 | 6° down, 1° left | −76.3° | | |
| 150 | 8° down, any lateral ≥ 0.25° | −90° | | |

Desired bank against nose-down angle at 1° lateral, 91.7 m/s: −5.6° (0° down), −13.3° (6°), −24.2° (8°), **−77.9° (10°)**, −90° (12° and beyond). The direction flips sign with the sign of a sub-degree lateral offset.

Dynamically, this produces a growing roll limit cycle [V-dyn]:

| Run | Bank range | Roll-command reversals | Heading error |
|---|---|---|---|
| DESC8_LAT0 (no lateral) | 0..0 | 0 | 0 |
| DESC8_LAT0p5L | −35..+22° | 7 in 20 s | ±5° |
| DESC8_LAT2L_40S | −65..+69° | 9 in 40 s | ±13.6°, growing |

DESC8_LAT2L_40S bank extrema grow over the run: 20 → −18 → 20 → −25 → 39 → −59 → 69 → −65°.

### A.4 Flight path vs nose semantics — **correct and well separated** [V-static]

The flight path is the default reference, and nose semantics are tested (`[MI2]`). The fallback below 5 m/s is flagged (`:436-443`). Three markers are drawn.

One limitation: the aim's ±85° elevation clamp also clamps the re-anchoring. After a neutral-stick period on a path steeper than 85°, input resumes with a residual error of up to 4.5° (F-13, `evidence/probe_batch1/pure_probe.txt` [E1]).

### A.5 Pitch-rate demand — **correct form, wrong plant assumption** [V-static][V-dyn]

`pitch = (w_des (d·l) + I) / q_full` with `I = 0` by default (`:572`). The guidance treats V2 as an ideal rate tracker.

V2 is proportional + feed-forward with no integrator and a fixed trim bias (`F15/MavF15PilotControlLawV2.cs:81-84,258-263`). So:

- **At speed it cannot unload.** In every dive probe α stayed 14.2–14.8° with the guidance at the −0.6 push limit, while TAS rose to 178–214 m/s and n_z reached 3.1–4.3 g. A steep dive target is therefore unflyable as a straight path, and the guidance responds with continuous saturated rolling (F-02).
- **At low speed it cannot hold a climb.** Any climb above the trim path drains speed into the low-speed cap (F-06).

### A.6 Roll-rate demand — **correct form** [V-static]

`roll = bankError / τ_bank / p_full × liftWeight` (`:565-566`). With τ = 1 s and p_full = 15 °/s, the command saturates for bank errors above 15°. Large bank changes are therefore rate-limited at the V2 full-stick 15 °/s, and a 60° bank capture takes ≥ 4 s. That is a property of the V2 tuning, not a guidance defect.

### A.7 Sideslip command — **minimal and bounded** [V-static][V-dyn]

The pedal is used only within 5° of error, |yaw| ≤ 0.15 (`:597-600`). Probe max |β| was 0.84° in turns and 4.4° in the PR's mouse traces.

Removing the pedal (TURN15R_NOYAW) changed nothing material: ζ 0.078 → 0.107, so the pedal very slightly reduces damping. No flat-yaw turning was observed.

### A.8 Energy conservation and power accounting — **no energy created by the instructor; checks too weak to prove it** [V-static][V-dyn]

The instructor adds no load (A.1), so it cannot create energy. Energy changes come from the research model:

| Condition | Specific-energy change |
|---|---|
| Neutral hold | −94 m / 20 s |
| DESCENT 8 | +19 m / 20 s, plausibly T > D at α ≈ 10–11° (**[H] H3**, not power-balanced by this audit) |

The PR's `energyEnd < energyStart` "never created" assertion cannot detect a spurious source smaller than the trim's own ≈ 4.7 m/s drain (F-08).

### A.9 Low speed and high AoA — **protected** [V-dyn]

The low-speed pull cap and the α pull fade work as designed. In CLIMB80_HOLD (aim 80° above the path, 60 s), TAS min was 73.8 m/s (floor 66.6), α max 25.8°, with zero aero refusals. The aircraft settles wings level in a steady −14.5° glide: the unreachable target produces a stable degraded state, not a departure.

### A.10 Outside the supported envelope — **not protected at high speed (F-01, BLOCKER)** [V-dyn]

DIVE85_HOLD_90S, a mouse-reachable −85° aim held:

| t | Event |
|---|---|
| 30.9 s | TAS 213.8 m/s crosses the source-exercised ceiling; aero **and** thrust refuse |
| 30.9–90 s | No re-entry: 2,958 consecutive refused steps |
| after refusal | α and β diverge (α −179.7…179.8°, \|β\| 75°); TAS 780 m/s; Δh −32 km |

Meanwhile the mouse overlay shows `live-ready YES`, loads applied, and no warning.

The guidance's soft caps (`Core/MavMouseAimGuidance.cs:135-152,492-519`) contain no upper-speed term. Once the aero is zero, nothing on the aircraft can slow it, so the exit is unrecoverable.

DIVE60_HOLD_60S reached 200.7 m/s in 60 s; shallower held dives approach the same edge more slowly.

---

## B. Control stability

### B.1 Outer-loop gains and damping — **lightly damped; documented rationale contradicted (F-04)** [V-dyn]

The config entry for `flightPathRateGainPerSec` (`Core/MavMouseAimGuidance.cs:218`) states that K = 0.6/s "keeps damping near 0.45". This is an estimate (PR #36 doc 05 says so). Measured bank oscillation after the turn, by log decrement of the bank extrema:

| Run | Period | ζ | Bank amplitude |
|---|---|---|---|
| TURN5R_30S | 5.7 s | **0.072** | ±14° → ±2° over 23 s, for a 5° correction |
| TURN15R_40S | 6.8 s | **0.078** | ±18° → ±2.6° over 27 s |
| REVERSAL180_120S (tail) | 5.2 s | **0.034** | ±20° → ±2.3° over 55 s |
| TURN15R, τ_bank = 2 s | 8.9 s | 0.135 | |
| TURN15R, **K = 0.3** | — | **no oscillation** | error 15° → **0.0°** at 40 s; TAS min 90.4 m/s (vs 84.3 at K = 0.6) |
| Hands-off at 30° bank (no guidance) | — | **no oscillation** | bank decays monotonically 30° → 0° |

The oscillation is a property of the guidance closed loop, not of the aircraft + V2.

The K = 0.3 result also refutes the report's explanation that the 3° residual after a 15° turn is "≈ the energy deficit vs the trim path" and that "a real fix needs thrust management" (`Docs/FlightDynamics/REALISTIC_MOUSE_INSTRUCTOR_V2_REPORT.md:232,288`). With K = 0.3 the same turn converges completely at fixed thrust. At K = 0.6 the residual is vertical (path below the aim), and it is sustained by the extra drag of the more aggressive turn and of the persistent bank rocking: dEl −3.1° at 10 s → −3.9° at 40 s, still growing.

**Root-cause hypothesis.** The effective lag of the V2 roll/yaw closed loop plus the 0.08 s filter, the 4/s rate limit and the one-step latency is much larger than the 1-s bank time constant assumed. The textbook bank-to-turn estimate `ζ ≈ 1/(2√(K τ))` then over-predicts damping. This needs the identification in PR #36 doc 05 §4 to confirm; the probe only shows the symptom and that K = 0.3 removes it.

### B.2 Feedback latency — **consistent, small** [V-static]

There is one step (0.02 s) of state latency in the law. The guidance uses the post-simulate pose, so it is fresher. The latency is not the dominant lag (see B.1).

### B.3 Command filtering and rate limits — **correct, bounded** [V-static][V-dyn]

The filter is a first-order 0.08 s lag followed by a 4/s step limit (`:640-647`), then a clamp to fixed stick limits (`:651-654`). The worst observed per-step change is 0.080 = 4 × 0.02 in every run. A shrinking soft cap is reached through the rate limit, not by a step (`:649-650`); this is correct.

Rate-limit occupancy was not measured. PR #36's M18 asks for it.

### B.4 Bank reversals and 180° ambiguity — **correct** [V-pure][V-dyn]

- The ±10° hysteresis band keeps the turn side. A target swept through "behind" (az 150 → 210) flips the roll sign once, at az 190 (`pure_probe.txt` [R1]), which is the intended exit of the band.
- REVERSAL180_120S converges to 1.2° by 120 s, but with the low-damping tail of B.1.
- `lastTurnDirectionWorld` persists indefinitely, including through long hands-off periods (`:486-490`), so a much later reversal reuses stale side memory (F-19, IMPROVEMENT).

### B.5 Near-vertical targets — **finite; zenith unreachable by mouse** [V-static]

The degenerate branches (`:449-455`, `:536-537`, `:736-738`) keep everything finite (`[MI4]`). The mouse clamps at ±85°, so loops over the top are not aimable (documented).

### B.6 Saturation / feedback interaction — **saturation-driven cycling in dives (F-02)** [V-dyn]

In DIVE60_HOLD_60S, |roll command| ≥ 0.99 in 96 % of samples; bank sweeps through ±180° (51 samples at |bank| ≥ 150°); n_z is 0.8–3.9 g; the error cycles 14–57° and never converges.

DIVE30_HOLD_60S shows the same pattern at lower intensity: saturated 44 % of the time, n_z up to 3.1 g, path −69.9…+7.2°.

The mechanism (A.5): at speed the aircraft cannot unload with ≤ 0.6 push, so the gravity-compensated rule rolls to point the unavoidable lift at the target. Above 30–60° of error it rolls inverted, overshoots the target path, and repeats.

### B.7 Integral windup — **not active** [V-static]

`pitchIntegralGainPerSec2 = 0` (`:164`). The anti-windup logic (`:576-594`) is well formed but dead by default. The report still describes the integral as the adopted fix in §1, §3.3 and §3.4 (F-10).

### B.8 Inverted-roll transitions — **smooth band, but source of the F-03 singularity** [V-static][V-pure]

The 30–60° smoothstep removes the discontinuity in *roll intent at large errors*, but creates the zero-g-request singularity of A.3 at moderate nose-down errors.

### B.9 Unreachable targets — **stable but energy-unaware (F-06)** [V-dyn]

| Run | Target | Outcome |
|---|---|---|
| TURN5UP_30S | 5° above the trim path | after 30 s: −13.7° path at 75 m/s; error 15.8°, larger than at the start |
| CLIMB8 (PR) | 8° above | the same pattern |
| LOW-SPEED CLIMB 40 (PR) | 40° above | the same pattern |
| CLIMB80_HOLD | 80° above | −14.5° glide |
| MOVING_2DPS_120S | target moving 2 °/s | steady lag ≈ 30° (dAz −28°, dEl −11°), equilibrium at the low-speed cap (TAS 81 m/s) |

Physically legitimate on the back side of the drag curve at fixed thrust, but strictly worse than releasing the stick. Hands-off holds −2.9° at 91.7 m/s.

---

## C. Neutral stick (hands-off and the default input-loss policy) [V-dyn]

**Spiral hypothesis refuted.** HANDSOFF_IN_TURN_120S and INPUTLOSS_NEUTRAL_120S (bit-identical, same code path) start from a 49° bank:
- bank decays monotonically to 0° in about 100 s;
- the phugoid is lightly damped: period ≈ 49 s, path elevation −13.9 → +6.3 → −9.5 → +1.5 → −6.1;
- Δh −727 m in 120 s; no aero refusal.

Neutral stick under V2 is a slow, stable, attitude-holding drift at this trim, not wings-level recovery. That is acceptable and matches the report's description, but it is verified at one trim only.

## D. Summary table

| Area | Verdict |
|---|---|
| Axes / signs / units (deg vs rad, N vs g) | Correct |
| Gravity-compensated bank-to-turn algebra | Correct, except the zero-lift-request singularity (F-03) |
| Plant model assumed by the guidance | Wrong at speed and for damping (F-02, F-04) |
| Envelope protection | Low speed / α only; **upper speed missing (F-01)** |
| Stability near the target | Lightly damped, ζ ≈ 0.03–0.08 (F-04) |
| Behavior far from trim (dives) | Non-converging saturated rolling (F-02) → domain exit (F-01) |
| Neutral stick | Stable at this trim |
