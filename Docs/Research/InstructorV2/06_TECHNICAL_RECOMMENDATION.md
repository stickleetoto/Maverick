# 06 — Technical Recommendation: Realistic Mouse Instructor v2

**Classification of this whole document:** `MAVERICK_PROPOSAL`, built on the repository audit (doc 05), the algorithm analysis (doc 03) and the external evidence in docs 01, 02, 04 and 07. It is a handoff for the development branch `claude/realistic-mouse-instructor-v2`; it changes no code.

**Design principle:** *the instructor decides what the pilot would ask for; the aircraft decides what happens.* Everything the instructor outputs is a pilot command that the existing control law turns into surface requests. Nothing it does can create a force, a moment, a pose or a velocity.

---

## 1. Architecture

```text
 Update (render rate)                         FixedUpdate (physics, 50 Hz)
 ────────────────────                         ─────────────────────────────────────────────────────────────
 MavFreshInput raw mouse delta                order −300  MavFlightControlLawBase.StepControlLaw
        │                                                  │ ResolveCommand()
        ▼                                                  ▼
 [A] MavMouseAimInput  ── snapshot{seq, aimRotation,  ──▶ [D] MavMouseAimPilotCommandSource.TryGetCommand
     (world aim orientation,   freeLook, focus, valid}      │   ├─ state  = body.debugState   (same state the law reads)
      free-look freeze,                                     │   ├─ intent = [B] MavAimGuidance.Step(snapshot, state,
      no aircraft access)                                   │   │                 descriptor, ref guidanceState, dt)
                                                            │   ├─ stick  = [C] descriptor.ToPilotCommand(intent)
                                                            │   └─ publish [E] telemetry
                                                            ▼
                                              existing law (F-15 V2 / F-16 V0.1)  → actuator (−200)
                                                            → aero + propulsion → LoadSet → MavSixDoFBody (−100)
                                                            → Rigidbody (only place a load is applied)
```

| Module | New / existing | Location (proposal) | Aircraft-independent? |
|---|---|---|---|
| [A] Mouse aim input | new `MonoBehaviour` | `FlightDynamics/Core/` | yes |
| [B] Aim guidance | new pure static class + state struct | `FlightDynamics/Core/` | yes |
| [C] Pilot-command semantics descriptor | new interface; implemented by/for each law from the law's **existing** gains | `Core/` (interface), `F15/`, `F16/` (adapters) | interface yes; values per law |
| [D] Mouse-aim command source | new `MavPilotCommandSourceBase` | `FlightDynamics/Core/` | yes |
| [E] Instructor telemetry | additive fields / sample | `MavFlightDynamicsTelemetry` or a sibling | yes |
| Control laws, actuators, aero, body | **existing, unchanged** | — | — |
| `MavInstructorController`, `MavMouseFlightRig`, `MavMouseFlightJet` | **legacy, untouched, not used by this path** | — | — |

The bridge `MavMouseInstructorPilotCommandSource` stays as it is (it serves the F-16 shadow smoke) but is **not** the base for v2 (doc 05 L1–L3).

---

## 2. Module responsibilities

### [A] `MavMouseAimInput` (render rate, no physics access)

- Integrates raw mouse delta into a **world aim orientation** `Q_aim` (store the full orientation, not only a direction, so that "aim up" stays defined near the zenith). Rotate about `Q_aim`'s own right axis for vertical delta and about its own up axis for horizontal delta; re-orthonormalize. *Alternative* (camera-axis rotation, as the legacy rig) must be tested at the zenith before use (doc 05 L9, doc 03 §5.6).
- Delta is **summed counts**, not scaled by `Time.deltaTime`, so the same physical mouse motion gives the same aim regardless of frame rate. Sensitivity is a labelled input preference.
- Free-look: freeze `Q_aim` while held (same idea as the legacy `frozenDirection`).
- Publishes an immutable snapshot `{sequence, Q_aim, d̂, freeLookHeld, hasFocus, valid}`. It never reads or writes the aircraft, the Rigidbody or the camera transform's physics.
- Initialization: `Q_aim` = aircraft nose orientation on enable (no surprise turn at start).

### [B] `MavAimGuidance` (pure, deterministic)

`Step(in AimSnapshot, in MavFlightState, in IMavPilotCommandSemantics, ref MavAimGuidanceState, float dt, out MavAimGuidanceDebug) → MavPhysicalIntent`

- Implements doc 03 §4: lift-vector pursuit, push-or-roll hysteresis, square-root bank capture, roll-then-pull weighting, turn-direction memory near 180°, optional terminal sideslip.
- `MavPhysicalIntent = { p_s,cmd [rad/s], n_cmd [g], β_cmd [rad], valid, mode flags }`.
- Owns its state (`ê_prev`, last `n_cmd`, last `p_s,cmd`, push/roll latch, last step id). Explicit `Reset()`.
- No `UnityEngine.Time`, no `Transform`, no `Rigidbody`: everything enters through arguments (same rule as `MavF15PilotControlLawV2.Compute`, so the validated function is the one that flies).

### [C] `IMavPilotCommandSemantics` (per law, read-only)

The missing seam (doc 05 C1). Each law answers "what does +1 mean?" from its **own existing gains**, so no number is duplicated:

```csharp
// MAVERICK_PROPOSAL — illustrative shape, not committed code
public enum MavPitchAxisSemantics { PitchRateRadSec, LoadFactorG }
public enum MavRollAxisSemantics  { BodyRollRateRadSec, StabilityAxisRollRateRadSec }

public interface IMavPilotCommandSemantics
{
    string LawName { get; }
    MavPitchAxisSemantics PitchSemantics { get; }
    MavRollAxisSemantics  RollSemantics  { get; }

    // Full-scale values of the law's own command, taken from its gains struct.
    float PitchFullAft { get; }      // q (rad/s) or n (g) at stick +1
    float PitchFullForward { get; }  // at stick −1 (sign included)
    float RollFullScaleRadSec { get; }
    float SideslipFullPedalRad { get; }

    // Inverse of the law's own stick-to-command map, at the current state
    // (needed because e.g. the F-16 neutral load factor depends on bank).
    MavPilotCommand ToPilotCommand(MavPhysicalIntent intent, in MavFlightState state);

    // Guidance request ceilings this law tolerates, derived from its limiter settings
    // (F-16) or from model-domain caps documented for the rig (F-15). Labelled provenance.
    MavGuidanceCapabilities Capabilities(in MavFlightState state);
}
```

Values today (from code; all `MAVERICK_TUNED_NON_AUTHORITATIVE` at their source):

| | F-15 V2 | F-16 V0.1 |
|---|---|---|
| Pitch | `PitchRateRadSec`, ±7 °/s (`commandedPitchRateAtFullStick`) | `LoadFactorG`, +9 / −3 g, neutral = level-turn n |
| Roll | `StabilityAxisRollRateRadSec`, 15 °/s | `BodyRollRateRadSec`, 270 °/s |
| Yaw | β, 1.5° | β, 8° |
| Capabilities | none in the law → rig-level **domain caps** (new, labelled) | from `MavAngleOfAttackLimiterSettings`, `MavLoadFactorLimiterSettings`, `MavRollRateLimiterSettings` |

Conversion rules in `ToPilotCommand`:
- pitch-rate law: `q_cmd = (g/V)(n_cmd − cos γ cos μ)`, stick = `q_cmd / q_full` (signed, per side).
- load-factor law: stick from the law's own piecewise-linear map inverted around its bank-dependent neutral.
- roll: stick = `p_s,cmd / p_full`; for a body-axis law the same number is sent and the β transient is accepted and reported (doc 05 C2).
- yaw: stick = `−β_cmd / β_full` (the laws' sign convention: nose right ⇒ β < 0, see `MavF16ControlLawV01` yaw comment).
- Clamp to ±1; if any clamp binds, set a telemetry flag (the request exceeded the law's command range).

### [D] `MavMouseAimPilotCommandSource`

- `IsOperationalCommandSource = true` (declaration).
- `HasCommandSignal = snapshot.valid && hasFocus && descriptorBound && stateUsable && !nonFinite` (live), consistent with `TryGetCommand` as the base contract requires.
- `SignalLossPolicy = NeutralCommand` (centre stick, hold throttle).
- **Per-step idempotence (important):** `TryGetCommand` can be called more than once per physics step (by the law at −300, by `MavF15PilotControlledRig.TrySetControlMode`, by readiness/diagnostics). Guidance state must advance **once per step**: cache the result keyed on the physics step (e.g. `Time.fixedTime` or an injected step counter) and return the cached command on repeat calls. V2 had the analogous problem with its washout during an in-flight switch (V2 doc §8) — it is a known trap in this repository.
- Throttle: not produced by guidance. Pass through a throttle owner (keyboard throttle state) when the aircraft has live throttle; on the F-15 research rig report `THROTTLE INACTIVE — FIXED RESEARCH THRUST` and output the held value. Do not reuse `ConvertThrottlePercentToFdm` (F-16-specific, doc 05 L11).
- Rig wiring: additive `MavF15PilotCommandSourceKind.MouseAim` and a branch in `ResolveCommandSource` (doc 05 C7); keyboard remains default; the ownership grant is unchanged.

### [E] Telemetry

Per step: `θ_nose, θ_path, Δμ, ê_turn side, w (pull weight), ω_cmd, n_req, n_cmd, n_z measured, p_s,cmd, p_s measured, β_cmd, β, α, V, Mach, q̄, E_s, P_s estimate (from dE_s/dt), mode (fine / turn / reversal-lock / hold), push-or-roll latch, stick clamps, law limiter binding flags, surface saturation flags, domain-cap active, signal state`. All already available or derivable from `MavFlightState` and law debug structs.

---

## 3. Target semantics (decision required)

Default proposal: **nose pointing** (`θ_v` from `b̂`, turn direction from flight-path geometry; doc 03 §6 option 3), because

- on the F-15 research rig the nose and the flight path differ by ~17.5° at trim (doc 05 C5), so flight-path pursuit would put the nose far off the marker;
- War Thunder's mouse aim controls a **body-fixed axis**, not the velocity vector: it matched the longitudinal axis to the cursor until April 2024 and the aiming reticle since update 2.35.1.40 (doc 01 W9, `PRIMARY_VERIFIED`). For Maverick, "nose" and "gun line" coincide until a rig defines a sight offset; keep the aimed axis a named parameter so a gun-line offset can be added later.

Keep flight-path pursuit selectable, and always show both errors. This is a product decision; record it in the implementation report.

---

## 4. State variables

| Owner | State | Reset when |
|---|---|---|
| [A] | `Q_aim`, frozen-aim orientation, accumulated delta since last snapshot, sequence | enable, aircraft respawn, explicit recentre |
| [B] | `ê_prev`, push/roll latch, last physical commands (for rate limits), last step id | enable, source switch, law switch, invalid state |
| [D] | cached command + step key, last valid command (for policy) | enable, source switch |
| laws | unchanged (V2 washout, F-16 integrator/washout) | per existing rules |

---

## 5. Pseudocode (one physics step)

```text
TryGetCommand(out cmd):
    key = currentPhysicsStepKey()
    if key == cachedKey: cmd = cached; return cachedOk

    s     = aimInput.LatestSnapshot()                 # produced in Update
    st    = body.debugState                           # previous-step state, same as the law
    sem   = boundLaw as IMavPilotCommandSemantics
    if !s.valid or !s.hasFocus or sem == null or !Finite(st):
        cachedOk = false; cached = Neutral; return false   # law applies SignalLossPolicy

    intent = MavAimGuidance.Step(s, st, sem, ref gState, fixedDt, out dbg)
    if !intent.valid: cachedOk = false; ...; return false
    cmd = sem.ToPilotCommand(intent, st)              # clamps, flags
    cmd.throttle01 = throttleOwner.Current()          # not guidance
    publish(dbg, cmd)
    cachedKey = key; cached = cmd; cachedOk = true
    return true

MavAimGuidance.Step:
    b, v, up, V, gvec  from st (validate finite, V > Vmin_guidance)
    d  = s.d
    caps = sem.Capabilities(st)                       # n_max_cmd, n_min_cmd, p_cap, beta_cap, domain flags
    θ_nose = angle(b, d);  θ_path = angle(v, d)
    θ_v    = semantics == Nose ? θ_nose : θ_path
    e      = d − dot(d, v) v
    ê      = TurnDirection(e, θ_v, ref gState)        # memory/hysteresis near 180°
    ω_cap  = g * sqrt(max(caps.n_max_cmd², 1) − 1) / V
    ω      = min(Kθ * θ_v, ω_cap)
    a_lift = V*ω*ê − (gvec − dot(gvec, v) v)
    n_req  = |a_lift| / g
    l      = normalize(up − dot(up, v) v)
    if |a_lift| < a_min:  l_des = l                   # hold bank, n ≈ |a_lift|/g
    else:                 l_des = a_lift / |a_lift|
    (Δμ, n_sel) = PushOrRoll(l, l_des, n_req, caps, ref gState)
    p_s = sign(Δμ) * min(Kμ*|Δμ|, sqrt(2*a_p*|Δμ|), caps.p_cap)
    w   = cos²(clamp(Δμ, −π/2, π/2))
    n_hold = clamp(cosγ cosμ, caps.n_min_cmd, caps.n_max_cmd)
    n   = SoftSaturate(n_hold + w*(n_sel − n_hold), caps)
    β   = FineAimEnabled && θ_nose < θ_fine ? −sat(Kβ * e_lat_nose, caps.beta_cap) : 0
    rate-limit (p_s, n, β) against previous; store
    return {p_s, n, β, valid = true}
```

Every symbol (`Kθ, Kμ, a_p, a_min, θ_fine, Kβ, H_push, H_side, θ_lock`, rate limits) is guidance tuning, labelled `MAVERICK_TUNED_NON_AUTHORITATIVE`, set per law from measured responses (doc 03 §5.1 rule `Kθ τ_r ≈ 0.5`), never from feel alone.

---

## 6. Stability and safety considerations

1. **Loop separation.** Guidance (outer, `K_θ`) must be several times slower than the law's roll/pitch response; enforce with the `K_θ τ_r` rule and verify with step responses (M1–M4).
2. **One-step latency** is part of the plant (law reads previous-step state). Guidance runs inside the law's poll, so it adds no further latency.
3. **No integrators in guidance** → no windup when authority is short (Scenario D).
4. **Continuity.** No mode switch is discontinuous: pull weight `w` is smooth, push/roll and turn-side choices have hysteresis, physical commands are rate-limited, filters reset explicitly.
5. **Fail closed.** Invalid aim, lost focus, missing descriptor, non-finite state → no signal → law's `NeutralCommand` policy. Never the inspector field.
6. **Physical honesty.** No thrust, drag, damping, alignment or pose effect; throttle not owned; energy loss is shown, not compensated; out-of-domain is reported, not hidden.
7. **Ownership.** One source bound; one law; one body applying loads. Add a check that a second enabled `MavPilotCommandSourceBase` on the rig is reported (doc 05 O4).
8. **Angular-velocity cap.** Unity 6000.3's default is 50 rad/s, far above any commanded rate; still set and assert it on the FDM rig so a legacy profile (5.4–8 rad/s) or another Unity version cannot make it bind (doc 05 O6, doc 04 §4).
9. **Gyroscopic term.** Already compensated once by `MavSixDoFBody` because PhysX omits it (repository torque-free probe, doc 04 §1). The instructor must add nothing.

---

## 7. Aircraft-independent vs aircraft-specific

| Aircraft-independent (Core) | Aircraft-specific (per law / rig) |
|---|---|
| aim input integration, free-look, snapshot | stick semantics and full-scale values (from existing gains) |
| guidance geometry, sequencing, hysteresis, rate limiting | guidance capability ceilings (from law limiters, or rig model-domain caps) |
| fail-closed rules, per-step idempotence | guidance tuning `Kθ, Kμ, a_p` (derived from that law's measured response) |
| telemetry schema, metrics, scenario definitions | model validity envelope (speed, α, β, Mach) used for domain caps and test selection |
| conversion `n ↔ q` (physics) | whether roll is body- or stability-axis (law property) |

---

## 8. Validation approach

### 8.1 Three tiers, different questions

| Tier | Question it answers | What it cannot answer |
|---|---|---|
| T1 pure guidance tests (headless) | Is the geometry/sign/continuity correct for every canonical case? | Whether the aircraft can follow |
| T2 deterministic Play Mode with the real FDM (`captureFramerate` pinned, dt 0.02) | Does the closed loop converge, without oscillation or ownership faults, inside the model's domain? | Whether it feels right |
| T3 human flight test | Is it controllable and intelligible? | Physical correctness (tiers 1–2 do that) |

### 8.2 T1 cases (each asserts signs and finiteness)

Target straight ahead; ±5°/±10° left/right/up/down; 90° in each quadrant; directly behind (with and without history); at zenith and nadir with level, vertical-up and vertical-down flight paths; inverted aircraft (target "above" in world = below in body); 60° bank; zero, NaN, ∞ aim vectors; `V` below guidance minimum; descriptor missing; repeated `TryGetCommand` in one step returns identical output and advances state once.

### 8.3 T2 scenarios and metrics

Scenarios A–G of doc 03 §5, each from the F-15 point-36 trim (in-domain), with scripted aim trajectories: step 5°, 10°, 90° (four directions), 180° behind, aim sweep through the zenith, a constant-rate circling target, and a release-to-hold case.

| Metric | Definition |
|---|---|
| M1 | time until `θ_v < 2°` (and `< 5°`) |
| M2 | overshoot: max `θ_v` after first entering the 2° band, and lateral-error sign changes |
| M3 | bank capture overshoot `max \|μ − μ_des\|` after first reaching `μ_des` |
| M4 | roll reversals: sign changes of `p_s,cmd` beyond a deadband per manoeuvre |
| M5 | `max \|β\|` |
| M6 | fraction of steps with any surface or law limiter saturated; stick-clamp flags |
| M7 | `ΔE_s` and mean `P_s` over the manoeuvre (reported, never optimized away) |
| M8 | turn-side changes during a reversal |
| M9 | max per-step change of each stick axis and each physical command |
| M10 | surface-envelope violations (must stay 0; already enforced) |
| M11 | peak `n_z` vs `n_max_cmd` |
| M12 | max command step while the aim crosses the zenith |
| M13 | model-domain excursions (count, duration) |
| mech. | exactly one law, one source, one load application per step, no teleport (existing `[M]` style) |
| det. | two runs byte-identical; same mouse-delta trace at several `captureFramerate` values → documented, bounded command differences |

Acceptance thresholds are **not** proposed here (no evidence base for numbers). The first T2 run establishes the measured baseline; later changes are judged as diffs against it, the way V1/V2 were.

### 8.4 T3 human test protocol

Per the handoff: small and large turns, reversal, low-speed degraded control, release-to-hold. Record the T2 telemetry during the flight; ask for a Cooper–Harper rating per task `[COOPER-HARPER-1969]` plus free text. The rating is evidence about controllability, not about physical fidelity.

### 8.5 Regression gates

Unchanged frozen F-15 suites, V1/V2 headless and Play Mode, Baseline v1 runner (1585/0) — with the instructor **off** they must be byte-identical; new validation files must be added to `Tools/fdm_validation_baseline_v1.json` (repository rule).

---

## 9. Implementation priorities

**Immediately implementable (no FDM change, all inside the handoff's allowed scope):**

| P | Item |
|---|---|
| P0 | [C] semantics descriptor for F-15 V2 (and V1 for completeness) derived from existing gains; T1 tests that it matches the law's actual stick map |
| P0 | [B] guidance pure function + T1 suite (signs, continuity, 180°, zenith, NaN) |
| P0 | [D] source with fail-closed signal logic and per-step idempotence; ownership tests |
| P0 | [E] telemetry + T2 scenario runner with M1–M13 on the F-15 rig |
| P1 | [A] delta-integrated world aim with free-look; FPS-independence test |
| P1 | F-15 rig domain caps (speed/α), labelled as model-validity protection |
| P1 | nose vs flight-path semantics switch; α-coupling check at trim |
| P1 | human test with Cooper–Harper and telemetry capture |

**Longer-term research:**

| P | Item |
|---|---|
| P2 | terminal fine-aim sideslip |
| P2 | F-16 descriptor and shadow/unpowered guidance tests (powered live flight blocked on thrust data) |
| P2 | stability-axis roll and yaw damper for the F-16 law (a *control-law* change, separate package, F-16 gates) |
| P3 | NDI/INDI inner laws using the frozen aero model as effectiveness source; LQR as offline gain-design aid |
| P3 | offline cross-check of trims/step responses against an external FDM used only as an oracle (doc 04) |
| P3 | an aircraft model with a wider public-data envelope, so the instructor can be judged at fighter speeds |

---

## 10. Ranked shortlist

| # | Technology | Purpose | Evidence | Existing implementation | Integration method | Expected benefit (hypothesis) | Risks | Validation | Priority |
|---|---|---|---|---|---|---|---|---|---|
| 1 | **Pilot-command semantics descriptor** | Guidance knows what the stick means for each law | doc 05 C1 (`REPO_VERIFIED` divergent semantics: rate vs load factor, body vs stability axis) | gains in `MavF15PilotControlGainsV2`, `MavF16ControlLawGains`, limiter settings | additive interface + per-law adapter reading existing gains | aircraft-independent guidance; no hidden per-aircraft gains | descriptor drifting from the law | T1: descriptor inverse ∘ law forward map = identity on samples | **P0** |
| 2 | **Lift-vector pursuit with roll-then-pull** | Converts aim direction into bank + load factor that a real airframe can fly | point-mass and coordinated-turn equations (standard, EI); `[NASA-TP-1538]` stability-axis kinematics; doc 03 §4 | F-16 `q = g(n − cos φ)/V`, `MavControlLawProtections` | pure function in Core called by the source | no roll thrash on small errors; banked turns not flat yaw; continuous everywhere except chosen hysteresis | `Kθ` too high → lateral oscillation | T1 signs/continuity; T2 M1–M5 | **P0** |
| 3 | **Turn-direction memory + push/roll hysteresis** | Defined, flicker-free behaviour at 180° and for "below" targets | geometry (doc 03 §5.3); `ENGINEERING_INFERENCE` | none (legacy undefined, doc 05 L6) | inside [B] state | reversals without oscillation or discontinuous demands | wrong default side with no history | T1 behind cases; T2 M8, M9 | **P0** |
| 4 | **Guidance in the law's poll, idempotent per step; input integrated from raw deltas** | Determinism and frame-rate independence | repository execution order and one-step latency (`REPO_VERIFIED`); legacy Update-rate loop (doc 05 L5) | `StepControlLaw`, V2 switch priming (V2 §8) | [D] caches by step key; [A] sums deltas | byte-identical Play Mode runs; no FPS-dependent behaviour | multiple callers; cache key errors | T1 repeat-call test; T2 det. | **P0** |
| 5 | **Instructor telemetry and scenario metrics M1–M13** | Objective evaluation, not feel-only tuning | V1/V2 measurement practice (`REPO_DOC`); handling-quality practice `[MIL-F-8785C]`, `[COOPER-HARPER-1969]` | `MavFlightDynamicsTelemetry`, V2 Play Mode runner | additive fields + runner | regressions visible as diffs; human feedback tied to data | metrics without thresholds can be ignored | baseline run recorded and pinned | **P0** |
| 6 | Model-domain soft caps on the F-15 rig | Keep guidance requests inside the research model's validity | V1/V2 domain statements (`REPO_DOC`) | `SoftSaturate`, `SmoothMin` | descriptor `Capabilities` | no fail-closed disarm from guidance pulls | being mistaken for an aircraft limit | T2 M13; labels | P1 |
| 7 | Nose vs flight-path target semantics | Marker means what the player expects | doc 05 C5; War Thunder aims a body-fixed axis (doc 01 W9, PV) | none | guidance option + telemetry | intelligible aiming at high α | α coupling loop | T2 M2 at trim | P1 |
| 8 | Terminal sideslip fine-aim | Small aim corrections without banking | engineering inference only; no public War Thunder source says fine aim uses sideslip (doc 01) | law sideslip channels | guidance β term bounded by law range | finer gun tracking | adverse roll from β (strong on F-15 model: V2 §5.1) | T2 M4/M5 | P2 |
| 9 | Stability-axis roll / yaw damper on the F-16 law | Remove α→β coupling in bank-to-turn | F-15 V2 measurements (`REPO_DOC`), TP-1538 simulated F-16 damper on r − pα `[NASA-TP-1538]`; early F-15 CAS r − αp `[NASA-TN-D-7941]`; public F-15 CAS α×p crossfeed (`F15_836_FCS_STRUCTURE_V1.0.md`) | `MavF16ControlLawV01` | control-law change, separate package | smaller β in rolls at α | moves F-16 baselines | F-16 Baseline v1 gates | P2 |
| 10 | NDI / INDI inner laws | Aircraft-independent inner loops, fewer schedules | `[ENNS-1994]`, `[SNELL-1992]`, `[SIEBERLING-2010]` | frozen aero models | research prototype off the stable baseline | uniform response across envelope | ill-conditioned effectiveness on F-15 model; duplicate physics evaluator | offline + T2 | P3 |

---

## 11. Recommendations for the development Claude

1. **Do not build v2 on `MavInstructorController` or its bridge.** Reuse ideas (persistent aim, free-look freeze, telemetry names), not gains or code paths (doc 05 L1–L5).
2. **Add the semantics descriptor first** and make the guidance emit physical intent; otherwise every gain you tune is secretly F-15-V2-specific.
3. **Compute guidance inside `TryGetCommand` and cache per physics step** — `TrySetControlMode` and diagnostics also call `TryGetCommand`.
4. **Pick and document the target semantics** (nose vs flight path). At the F-15 trim the difference is ~17.5°.
5. **Set `K_θ` from the measured roll response** (`K_θ τ_r ≈ 0.5` for ζ ≈ 0.7), and show the step-response metrics; do not raise it to make the F-15 rig "feel faster" — its 7 °/s / 15 °/s command ranges are the law's, and the law is the place to change them, under its own gates.
6. **Expect the F-15 rig to be slow** (≈1.5 g equivalent at 7 °/s at its trim speed, doc 05 C4). Validate correctness there; do not tune feel there.
7. **Respect domain edges** with labelled guidance caps; report, do not hide, out-of-domain excursions.
8. **Set `maxAngularVelocity` explicitly** on the FDM rig's Rigidbody (Unity 6000.3 default 50 rad/s is fine, but do not depend on it).
