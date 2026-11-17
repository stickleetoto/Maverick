# 04 — Validation Audit

**Suites inspected (read in full):**
- `Validation/MavMouseAimInstructorV2Validation.cs` (headless `[MI1]`–`[MI14]`, 1017 lines)
- `Validation/MavMouseAimInstructorV2FlightValidationRunner.cs` (Play Mode `[RM]`/`[RF]`/`[RT]`/`[RI]`/`[RL]`, 667 lines)
- `Editor/MavMouseAimInstructorV2FlightValidation.cs`
- the committed reports `Docs/FlightDynamics/Data/mouse_instructor_v2/mi2_headless_report.txt` and `mi2_playmode_report.txt`

---

## 1. Independent re-execution

**Environment.** Unity 6000.3.16f1 (`D:\unitys\6000.3.16f1`), batch mode, Windows 11. Each run used a throwaway detached worktree at `ad34808`; no PR branch or user checkout was touched.

| Suite | Reported by PR | This audit | Notes |
|---|---|---|---|
| Headless `MavMouseAimInstructorV2Validation` | 61 / 0 | **61 / 0** | via `MavFdmValidationBatchAdapter` (`evidence/reproduction/mi2_headless.json`) |
| Play Mode `MavMouseAimInstructorV2FlightValidationRunner` | 29 / 0 | **29 / 0** | report **byte-identical** to the committed one after newline normalization (`evidence/reproduction/mi2_playmode_reproduced.txt`) |
| Official Baseline v1 (`Tools/run_fdm_validation.ps1`) | 1585 / 0 | **1585 / 0**, PASS, physics delta NONE | clean detached worktree, PowerShell 7; see `README.md` for the 5.1 infra note |
| Frozen F-15 (17 suites, 757/0), pilot V1/V2 headless and Play Mode, research closeout | 757/0, 49/0, 54/0, 31/0, 57/0, 39/0 | **not re-executed** | the PR diff touches none of the hashed research files. The only changed shared runtime file, `F15/MavF15PilotControlledRig.cs`, appears in V1/V2 source *scan lists* (`MavF15PilotControlValidation.cs:729`, `MavF15PilotControlV2Validation.cs:902`), not in hash lists. Accepted as reported, not independently proven |

**Conclusion.** The PR's numbers are honest and reproducible. The question this document answers is what those numbers *establish*.

## 2. What each Play Mode tracking check actually asserts

All checks are in `Validation/MavMouseAimInstructorV2FlightValidationRunner.cs`.

| Check | Assertion (line) | Asserts convergence? | What it actually shows |
|---|---|---|---|
| NEUTRAL AIM HOLD | max error < 1°, \|bank\| < 1° (`:497`) | n/a | Zero input does not perturb the trim. Trivial: the aim is anchored on the trim path |
| SMALL TURN 15 R/L | early bank sign; **`eEnd < 0.5·e0`** (`:520-522`) | **No**, only halving (7.5° threshold vs 3.2° end) | Response direction and approach. The 3.2° residual and the ±18° bank rocking (`bank −18..43` in the record) are not scored |
| LARGE TURN 90 R | same halving rule (`:521`) | **No**: would pass at 44° | End 12.0°. The record shows it stalled at ≈ 11–12° from 30 s. The mirrored 90° L probe (12.1° at 40 s) decomposes it: heading short 4.5°, path 11° below the aim |
| REVERSAL 180 | **`eMin < 0.5·e0`** (`:521`) | **No**: only the minimum | Any later divergence would pass |
| CLIMB 8 | path rises > 1° **and** `energyEnd < energyStart` (`:504`) | **No** (rescored after failure, report §5.3 step 3) | The "energy book" half is trivially true: the trim itself loses energy (neutral hold ΔE −94 m / 20 s) |
| DESCENT 8 | path drops > 1° **and** `eMin < 0.5·e0` (`:512`) | **No** (rescored after failure, report §5.3 step 4) | Passes with 10 % margin (3.6° vs 4.0°). Hold failure "reported as a limitation" |
| MOVING TARGET 2 °/s | over the last 10 s: `max − min < 3°` **and** `max < 20°` (`:530`) | **No.** The label says "bounded, not growing", but **growth is not tested** | Quartiles 4.1 → 4.6 → 5.8 → 7.8° are growing. Over 120 s (probe): 15.0° @ 50 s, 26.4° @ 70 s, ≈ 29.9° plateau at the low-speed cap |
| LOW-SPEED CLIMB 40 | cap engaged, energy fell, `eEnd > 5` (`:537`) | Asserts **non**-convergence (by design) | Visibility of the cap. Appropriate |
| RELEASE TO NEUTRAL | neutral within 1 s (`:544`) | n/a | The ramp. Appropriate |
| `[RL]` default loss | neutral within 1 s, `loads == steps`, `SourceSignal` (`:594`) | n/a | Mechanics over 5 s after loss. Long-term behavior not covered (probe: stable for 112 s) |
| `[RL]` DropCommandSignal | `loadsAtEnd == loadsAtEvent`, withheld > 0 (`:599`) | n/a | **Asserts that the non-physical coast happens and scores it PASS.** The record's α 33.6° / bank 77° after 3 s is not checked; restore not tested |
| `[RM]` mechanics | owner, one armed body, one law, `loads == steps`, no pose write, finite, `\|Δcmd\| ≤ 0.08`, `\|yaw\| ≤ 0.15`, surfaces in envelope, `\|Δv\| < 2 m/s/step`, aero refused 0 (`:452-454`) | n/a | **Strong invariant gates.** These are the PR's most valuable checks. `aeroRefused == 0` cannot see a domain exit that happens while loads are withheld (Drop) |
| `[RI]` timing | snapshot within one frame + one step; final aim equal across fps (`:572`, `:582`) | n/a | Strong; reproduced |

## 3. What the headless suite establishes

| Block | Strength | Gaps |
|---|---|---|
| `[MI1]` provenance, config listing, execution order | Good bookkeeping | Does not verify the *content* of the reasons. The K entry's "damping near 0.45" is measured here at ζ ≈ 0.03–0.08 (F-04, F-10) |
| `[MI2]`–`[MI5]` signs, inverted, vertical, behind | **Good**: single-evaluation geometry at canonical poses | One evaluation each; no closed loop |
| `[MI4]` "nadir … pull rather than push" (`MavMouseAimInstructorV2Validation.cs:291-296`) | **Label/assertion mismatch**: the assertion *requires* the settled command to be a push at the −0.6 limit with `PushLimited` set | Message misleads a reviewer (F-10) |
| `[MI6]`, `[MI7]` invalid inputs, random bounds | Good: finiteness and bound invariants over a 4,000-step sweep | Bounds only |
| `[MI8]`, `[MI9]` bank vs yaw, level-turn identity | Good at one point (8° right, 91.7 m/s) | Does not sample the nose-down/lateral region where the desired bank becomes singular (F-03; `pure_probe.txt` [K1]) |
| `[MI10]` soft caps | Good for the caps that exist | **No upper-speed cap exists to test** (F-01) |
| `[MI11]` input accumulation and timing | Strong | — |
| `[MI12]`, `[MI13]` source / rig / readiness | Strong for identity, wiring and grant | "Signal restored" (`:875-883`) restores after **5 steps** and also flips the policy back. A real long-loss restore is untested (F-05) |
| `[MI14]` source scan | Strong for no-write | — |

## 4. Independent dynamic probes (this audit)

**Method.** An audit-only runner and driver (`evidence/probe_source/*.cs.txt`), compiled only in a throwaway checkout, fly the PR's **own** `MavF15PilotControlledRig.Create(..., MouseAim, ..., AssistedV2)` from the validated trim. They use scripted targets and events, dt 0.02, `captureFramerate` 50, and the same one-step-before-start pattern as the PR runner. They read state only. CSV is logged every 0.1 s.

**Batch 1** (14 runs, `evidence/probe_batch1/`):
- DROP10_RESTORE, DROP3_RESTORE;
- HANDSOFF_IN_TURN_120S, INPUTLOSS_NEUTRAL_120S;
- MOVING_2DPS_120S;
- TURN15R_40S, TURN90L_40S;
- DESC8_LAT0, DESC8_LAT2L, DESC12_LAT3R;
- DIVE30_HOLD_60S, DIVE60_HOLD_60S;
- CLIMB80_HOLD_60S;
- REVERSAL180_120S;
- plus a pure-function probe of the production `MavMouseAimGuidance.Step` (`pure_probe.txt`).

**Batch 2** (10 runs, `evidence/probe_batch2/`):
- DROP15_RESTORE;
- DIVE85_HOLD_90S;
- DESC8_LAT0p5L, DESC8_LAT2L_40S;
- TURN15R_40S_{BASE, NOYAW, K0.3, TAU2};
- TURN5R_30S, TURN5UP_30S.

The probe runner file in `evidence/probe_source` holds the batch-2 scenario list. Batch-1 scenarios are listed by id in its summary; their parameters are encoded in the ids (target az/el offsets, durations, event times).

**Reproducibility check.** BASE in batch 2 reproduces batch 1's TURN15R_40S line-for-line in the summary, which confirms determinism.

### Key results

| Probe | Result | Finding |
|---|---|---|
| DIVE85_HOLD_90S | TAS crosses 213.3 m/s at 30.9 s; aero **and** thrust refused for all remaining 2,958 steps; TAS 780 m/s; α/β tumble; Δh −32 km | F-01 |
| DIVE60 / DIVE30 | Roll command saturated 96 % / 44 % of samples; bank through ±180°; n_z ≤ 3.9 / 3.1 g; no convergence in 60 s | F-02 |
| DESC8_LAT0p5L, DESC8_LAT2L(_40S), DESC12_LAT3R | Bank limit cycles −35..+22°, −65..+69° (growing), −56..+42°. With no lateral offset: bank 0 | F-03 |
| TURN5R / TURN15R / REVERSAL tail | Bank oscillation ζ ≈ 0.072 / 0.078 / 0.034. K = 0.3: no oscillation and **0.0°** final error. Hands-off: no oscillation | F-04 |
| DROP3/10/15 | Constant-velocity, constant-rate coast; α 53.5°, \|β\| up to 33.4°; restore after 15 s → 99 steps of aero refusal | F-05 |
| TURN5UP / CLIMB80 | Unreachable climb settles in a −13.7° / −14.5° glide at the low-speed cap, worse than hands-off (−2.9°) | F-06 |
| MOVING_2DPS_120S | Lag grows past the PR's 40-s window to ≈ 30° | F-07 |
| HANDSOFF / INPUTLOSS 120 s | Stable; bank decays; phugoid ζ ≈ 0.1 | refutes the spiral hypothesis |
| TURN90L | Mirror of 90 R (12.1° vs 12.0°) | symmetry confirmed |

## 5. Explaining the reported behaviors

| Reported | Asserted as | Explanation from the evidence |
|---|---|---|
| 15° turn ends ≈ 3.2° | halving only | The residual is **vertical** (dAz ≈ 0, dEl ≈ −3°) and still growing at 40 s (−3.9°). It is caused by energy lost to the aggressive K = 0.6 turn and to the ζ ≈ 0.08 bank rocking. **Not inherent**: at K = 0.3 the same turn converges to 0.0° and TAS stays ≥ 90.4 m/s |
| 90° turn ends ≈ 12° | halving only | Low-speed pull cap engaged for 975 steps (TAS 78 m/s). The aircraft is held at the cap: heading 4.5° short, path 11° below the aim. Energy-limited under the current gains; not shown to be energy-limited under all gains |
| 8° climb not maintained | rise > 1° plus a trivially true energy test | The plant cannot climb above the trim path at fixed thrust. The guidance pulls to the low-speed cap and ends in a steeper descent than releasing the stick (F-06) |
| 8° descent not maintained | approach only, 10 % margin | V2 has no integrator and the guidance push is capped at 0.6. As speed builds, α stays ≈ 11–14° and lift grows with q̄, so the path is pulled back up |
| Moving-target lag 5.8–7.8° | range over 10 s only | Not steady: it is the early part of a rise to ≈ 30° at the low-speed cap equilibrium |

## 6. Were thresholds weakened to pass?

**Yes, transparently.**
- Report §5.3 records that CLIMB 8 was rescored "on sign and energy" (step 3) and DESCENT 8 "on the approach" (step 4) after they failed. That changed 26–28/29 to 29/29.
- The moving-target and turn criteria were never convergence criteria.

The rescoring is disclosed, and the PR states that turn rates and times are "REPORTED, not scored". **But the suite as committed cannot detect a regression in convergence, damping, hold capability, domain exits under load, or long-duration behavior.** Its PASS is evidence of mechanics and invariants, not of flying quality.

## 7. Coverage gaps (what no test establishes)

1. Any trim other than point 36 (91.7 m/s, fixed density). PR #36 plan S6/S7 is not done.
2. High speed and upper-domain proximity (S6). Steep descents (none in the PR).
3. 5° and 10° corrections (S1/S2), 90° left / up / down (S4).
4. Closed-loop damping and margins (M2, M3, M14–M17).
5. Long durations (> 40 s, except the 60-s reversal).
6. Restore after long input loss (S11's restore at 6 s exists only in the probes).
7. The nose-down / small-lateral region of the bank rule (F-03).
8. dt sensitivity (S14). Render rates < 25 fps.
9. A human in the loop. The tests are scripted aims and cannot reveal PIO tendencies; with ζ ≈ 0.07 in the outer loop, a human adds lag to an already lightly damped loop (hypothesis H2).
