# 06 — Recommended Fix Plan

**Constraints carried from the handoff and from this audit.**
- No ownership, readiness or grant guard may be weakened.
- No force, torque, pose or energy assist may be added.
- Every new number is `MAVERICK_TUNED_NON_AUTHORITATIVE`, with the model limit it protects.
- Frozen research files stay untouched.
- Every fix ships with the regression test named in `05_FINDINGS_AND_SEVERITY.md`.

The order below is by risk removed per unit of change. Steps P0–P1 are the minimum before a human playtest; P2 before any integration discussion.

---

## P0 — before any human playtest

### P0.1 Upper-speed soft cap (F-01, part of F-02)

`Core/MavMouseAimGuidance.cs`:

1. Add config `highSpeedCapStartMps`, `highSpeedCapFullMps` (e.g. 185 / 200 m/s, tuning; protects the source-exercised 213.3 m/s ceiling) and `highSpeedNoseUpStick`.
2. Mirror the low-speed block (`:497-502`): as TAS rises through the band, raise `pushFloor` toward `+highSpeedNoseUpStick`, and cap the **downward** part of the flight-path request (scale the nose-down component of `turn` before `rateDes`).
3. Add limiter flag `HighSpeedCap`.
4. Add an `Entries()` row and the `[MI1]` coverage that follows automatically.

**Tests.**
- Headless: a cap table at 180/195/205 m/s.
- Play Mode: aims of −30/−60/−85° held for 120 s. Assert TAS max < 213.3 m/s minus a margin, aero refused == 0, and the flag was seen.

### P0.2 Remove `DropCommandSignal` from live airborne rigs (F-05)

- `F15/MavF15PilotControlledRig.cs` (or the source): when the body is armed, refuse or map `DropCommandSignal` to `NeutralStickHoldThrottle`, with telemetry saying so.
- Keep the enum for headless mechanics tests if wanted, explicitly marked "not a live policy".
- Do **not** touch `MavSixDoFBody` readiness or `MavFlightPhysicsOwnership`.
- Time-limit or remove `HoldAimTarget` as a loss policy (H7).

**Tests.**
- Headless: the rig refuses the policy while armed.
- Play Mode: loss of 3/10/30 s then restore, all policies. Assert aero refused == 0 after restore and the state stays inside the α/β/TAS bounds.

### P0.3 Overlay warning for domain refusal (F-11)

In `FormatTelemetry`, print `WARNING aero/thrust REFUSED - no aerodynamic load` when `aerodynamicModel` or `propulsionModel` reports refusal (read only).

## P1 — before a human playtest is used to judge "feel"

### P1.1 Regularize the bank direction near a zero-g request (F-03)

In `Core/MavMouseAimGuidance.cs:536-566`:
- **Option A.** Compute `bankError = atan2(f·s, max(f·l, k_floor·g))`, with `k_floor` ≈ 0.3.
- **Option B.** Multiply the bank error itself (not only the roll rate) by `liftWeight`, and in the deferral subtract only down to a vertical floor (e.g. keep `f·upright ≥ k·g`) instead of the full downward part.

Either way, keep the large-error inverted-roll behavior (≥ 60°) intact.

**Tests.**
- Headless: a Lipschitz grid (down 0–30° × lateral ±0–4° × 90/150/200 m/s).
- Play Mode: DESC8 with 0.5° and 2° lateral. Assert the bank stays within a small envelope and there are ≤ 2 roll reversals.

### P1.2 Retune or damp the outer loop with measurement (F-04)

1. Implement the PR #36 doc 05 identification:
   - a validation-only multisine command source;
   - fit the V2 roll/bank closed loop (τ_R, τ_eff) at point 36 and two other trims.
2. Choose K from the measured loop. Start from K = 0.3, which this audit showed removes the oscillation and converges the 15° turn to 0.0°. Alternatively, keep K and add a rate-damping term, for example `w_des = K θ − K_d · (dθ/dt)` computed from consecutive guidance steps, with the term reported.
3. Replace the "damping near 0.45" text with the measured ζ and its run identity.

**Tests.** Play Mode 5°/15° R/L and 180°. Record log-decrement ζ, bank overshoot (M3), reversal count (M4) and final error. Gate: no more than one overshoot > 2° after the first capture, or a diff against the recorded baseline.

### P1.3 Limit the commanded dive depression (F-02)

Clip the aim's commanded flight-path depression to a speed-dependent achievable value before the geometry, and report it as a limiter. Do not command inverted roll while n_z is above a threshold and the error is decreasing.

**Test.** Held −20/−30/−45/−60° aims for 60 s. Assert the roll-command saturation fraction stays under the recorded bound, |bank| > 150° never occurs unless the error exceeds 90°, and the error is bounded.

### P1.4 Correct the documents and labels (F-10)

- Report §1, §3.3, §3.4: the integral is not adopted.
- Report: replace the residual and thrust-management claims with the evidence.
- `[MI4]` and the moving-target labels: say what is asserted.
- §3.7 last row.

## P2 — before any production-integration discussion

### P2.1 Validation that can fail for the right reasons (F-07, F-08, F-09)

- Fix the moving-target check: slope over the last 20 s, 120-s run.
- Replace halving/approach checks with recorded-baseline metrics M1–M9, M11, M13 per PR #36 doc 06. Gate on diff vs baseline, keeping the invariant gates (`[RM]`, `[RI]`) as hard gates.
- Add a power-balance check: dE_s/dt vs (T − D)·V/W from the load set.
- Add scenarios S1, S2, S4 (L/up/down), S6 (high-speed trim), S8, S10, S11 (with restore), S12 sinusoid, S14 (dt 0.01, latency held), and render < 25 fps.

### P2.2 Energy-aware climb intent (F-06)

Limit the commanded climb angle using the measured specific-energy rate, or relax the pull toward the trim path while the low-speed cap is active.

**Test.** A 5° climb aim: the end path is ≥ the hands-off path or flagged.

### P2.3 LOW items

| Finding | Fix |
|---|---|
| F-13 | Anchor with 89.9° |
| F-14 | Capture key |
| F-15 | Explicit camera binding |
| F-16 | Runtime config validation; per-step stick-scale sync |
| F-17 | Reset on source or policy switch |
| F-18 | Air-relative state |
| F-19 | Turn-memory expiry |

## P3 — human acceptance (unchanged from the handoff)

Run only after P0 and P1 pass. Use a supervised session with the telemetry of PR #36 doc 06 §5, logged per step.

Brief the tester beforehand:
- the aircraft is the slow, fixed-thrust research rig;
- the "stop" conditions, below.

Stop the session when any of these occurs:
- TAS > 195 m/s;
- a `HighSpeedCap` or refusal warning;
- the bank cycling through inverted.

---

## Regression bundle required with the fixes

| Gate | Must stay |
|---|---|
| Frozen F-15 suites | 757 / 0, numerics unchanged |
| Pilot V1 / V2 headless | 49 / 0, 54 / 0 |
| Pilot V1 / V2 Play Mode | 31 / 0, 57 / 0, reports byte-identical |
| Research closeout | 39 / 0, byte-identical |
| Official Baseline v1 | 1585 / 0, manifest updated for any new validation file |
| Instructor headless + Play Mode | the new tests above added; existing invariants unchanged |
