# F-15 Pilot Control V2 — Maverick closed-loop control law

> **THIS IS NOT THE F-15 FCS.** V2 is a Maverick-owned control law. It is **not** the F-15 CAS, FLCS or SAS, **not** a NASA 836 control law, and it is not derived from or validated against any of them. Every number in it is **MAVERICK_TUNED_NON_AUTHORITATIVE**.
>
> The F-15 AFIT/BAUMANN/DAVISON RESEARCH BASELINE V1 stays **FROZEN** (`F15_RESEARCH_BASELINE_FREEZE_V1.0.md`). V2 changes **pilot control only**: no coefficient, CMMQ, mass, inertia, thrust, density, gravity, trim solver, dataset, RHS or frozen result changed.
>
> **V1 is unchanged and stays the default.** V2 is a second, independently selectable law on the same pilot-controlled aircraft (`F15_PILOT_CONTROLLED_V1.md`).

**Configuration ID:** unchanged, `F15_AFIT_BAUMANN_DAVISON_PILOT_CONTROLLED_V1`. The ID names the pilot-controlled aircraft configuration (frozen physics + Maverick control layer + its owner). V2 is a control-law option of that same configuration: same physics, same gameplay envelopes, same actuator travel, same owner. The active law is reported by the diagnostics/HUD (`control law: DIRECT V1 …` / `ASSISTED V2 …`).

---

## 1. Selecting V1 or V2

| Where | How |
|---|---|
| Rig | `MavF15PilotControlledRig.controlMode` — `DirectV1` (default, value 0) or `AssistedV2` |
| Prefabs | `Assets/MaverickFresh/Prefabs/F15/F15_PilotControlledResearch_V1.prefab` (unchanged, V1 law only) · `…/F15_PilotControlledResearch_V2.prefab` (starts in V2, carries both laws, V1 disabled) |
| In flight | HUD key **F2** → `MavF15PilotControlledRig.TrySetControlMode`. Accepted only with stick and pedals centred (\|axis\| ≤ 0.05); refused otherwise, changing nothing. A V1 rig gains the V2 law on demand. |
| Menu | Maverick / F-15 / Place Pilot-Controlled Research F-15 V2 (Assisted) In Scene · Build … V2 (Assisted) Prefab |

Exactly one pilot-control law is enabled and bound to the body at any time; the other, when present, is disabled. The ownership grant enforces it every physics step (§8).

Keyboard handling stays out of the physics: the pilot flies through `MavPilotCommand` (`MavKeyboardPilotCommandSource`, W/S · A/D · Q/E); only the HUD reads F1/F2.

---

## 2. Architecture adapted from the Maverick F-16 law

The Maverick F-16 control law (`MavF16ControlLawV01`, itself Maverick tuning and not the F-16 FLCS) was used as a **software-architecture** reference. No F-16 number was used — `[W2]` checks that all 12 V2 values with an F-16 counterpart differ from it.

| F-16 law element | V2 | Why |
|---|---|---|
| Gains in one serializable struct, law state in an explicit struct, a static deterministic `Compute(…)` | **Reused** (`MavF15PilotControlGainsV2`, `MavF15PilotControlLawV2State`, `MavF15PilotControlLawV2.Compute`) | validation drives exactly the code that flies |
| Pitch: stick → pitch-rate demand → q error → pitch surface | **Reused**, plus a feed-forward term | at this slow, high-α trim, feedback alone gives ~⅓ of the commanded rate |
| Load-factor demand, turn compensation | **Not reused** | the trim is at 300.8 ft/s and α 17.5°; a pitch-rate command is the predictable choice, and nothing here needs level-turn holding |
| Pitch integrator with anti-windup | **Evaluated, not adopted** | at the fixed-thrust trim it drains airspeed toward the 218.5 ft/s domain edge and lowers phugoid damping (§5.2) |
| Roll: stick → roll-rate demand → rate error → aileron | **Adapted**: closes on **stability-axis** roll rate `p cos α + r sin α`, plus a feed-forward term; aileron + research 0.3 differential tail | at α 17.5° a body-axis roll builds sideslip that the model's very large dihedral effect turns straight back into an opposing roll (§5.3) |
| Yaw: pedal → sideslip demand → β error → rudder | **Reused**, plus a pedal feed-forward | |
| Aileron-rudder interconnect | **Reused** | supplies the yaw rate a velocity-vector roll needs (`r = p tan α`) |
| Washed-out yaw-rate damper | **Adapted**: on **stability-axis** yaw rate `r cos α − p sin α`, labelled **MAVERICK GAMEPLAY / RESEARCH ASSIST** | zero in a coordinated roll, so it coordinates body-axis rolling and adds Dutch-roll damping; never called an F-15 yaw damper/SAS/CAS |
| Dynamic-pressure gain scheduling, clamped | **Reused**, reference = the source's own qbar at the point-36 trim | §5.5 |
| Command limiting | the commanded rates / sideslip and the **unchanged V1 gameplay envelopes** | |
| α limiter, load-factor limiter, roll-rate limiter | **Not added** (§6) | |
| Leading-edge flap, throttle management | **Not used** | the research model has no LEF; **THROTTLE INACTIVE — FIXED RESEARCH THRUST** |
| Sign constants | **Not copied**: signs come from the research model's own conventions (`MavF15PilotControlConventions`) through the V1 mapping | |

---

## 3. The V2 law

`MavF15PilotControlLawV2.Compute(command, state, gains, envelopes, trimBias, ref lawState, dt)`. `s` = gain scale (§5.5); rates in rad/s, β in degrees, surface demands in degrees.

```
pitch   q_c    = pitch · 7°/s
        noseUp = s · (60 · q_c + 30 · (q_c − q))
roll    p_c    = roll · 15°/s                         (stability-axis)
        p_s    = p cos α + r sin α
        rollR  = s · (20 · p_c + 50 · (p_c − p_s))
yaw     β_c    = −yaw · 1.5°                          (nose right ⇒ β < 0)
        r_s    = r cos α − p sin α                    (stability-axis yaw rate)
        r_wo   = washout(r_s, 1.5 s)                  (explicit law state)
        noseR  = s · (4 · (−β_c) + 4 · (β − β_c))     sideslip feed-forward + feedback
               + s · 40 · p_c                         aileron-rudder interconnect
               − s · 40 · r_wo                        MAVERICK GAMEPLAY / RESEARCH ASSIST
```

The demands go through **the V1 mapping** (`MavF15PilotControlMapping.SolveFromDemands`), the one owner of the conventions and envelopes for both laws:

```
symmetric stabilator = trim bias + NoseUpStabilatorSign(−1) · noseUp     clamp −25 … −5
aileron              = RollRightAileronSign(+1) · rollR                  clamp ±20
differential stab.   = 0.3 · commanded aileron   (Davison relation)       clamp ±6
rudder               = YawNoseRightRudderSign(−1) · noseR                clamp ±15
```

Path: `MavPilotCommand` → `MavF15PilotControlLawV2` → requested F-15 surfaces → `MavF15ControlActuator` (sole actual-surface owner) → research aero → load set → `MavSixDoFBody` (sole load application). The law touches no Rigidbody, force, moment, damping torque, velocity or attitude (`[W11]`).

- **Neutral stick.** Every term is either command or feedback of rates/sideslip, all zero at the trim, so centred stick at the trim requests **exactly** the trim bias and zero lateral surfaces (`[W4]`).
- **Non-finite input.** A non-finite command is centred; a non-finite state zeroes every feedback term and is reported (`stateUsable`), and no non-finite value reaches a surface (`[W5]`).
- **Timing.** The law runs at −300 and reads the flight state the body published on the previous step: a one-step delay, included in the tuning and in the convergence test.

---

## 4. Every Maverick-tuned value (MAVERICK_TUNED_NON_AUTHORITATIVE)

The same list, with the same reasons, is in code (`MavF15PilotControlGainsV2.Entries()`), and `[W2]` checks that it covers every field.

| Value | Default | Unit | Why this value |
|---|---:|---|---|
| commandedPitchRateAtFullStick | 7 | deg/s | full stick then asks ~10° of stabilator, the V1 full-stick travel; the nose-up side of the envelope is 9.9° from the trim bias |
| pitchRateFeedForward | 60 | deg per rad/s | inverse of the model's own 1-s pitch response (V1 pulse ≈ 0.015 rad/s per deg); half stick reaches ~0.9 of the command in 1 s (40 → 0.69, 80 → 1.08) |
| pitchRateGain | 30 | deg per rad/s | short-period ζ 0.37 → ~0.6 with the delay; 10–20 leave a 6–7 s post-release bobble, 40 buys nothing and costs phugoid damping |
| commandedRollRateAtFullStick | 15 | deg/s | keeps the coordinating rudder clear of ±15° at full stick (~11–12°); 18 reached ~14° |
| rollRateFeedForward | 20 | deg per rad/s | ≈ \|L_p\| / L_δa at the trim (0.774 / 0.0364 per deg) |
| rollRateGain | 50 | deg per rad/s | also the main Dutch-roll damper at this α (ζ 0.21 → 0.25 for 40 → 50 without the yaw-rate term) |
| commandedSideslipAtFullPedal | 1.5 | deg | small on purpose: ½° of β already rolls the aircraft with about half the aileron's moment |
| sideslipFeedForward | 4 | deg per deg | half pedal reaches ~60 % of the commanded β in 1 s; 6 overshoots at full pedal |
| sideslipGain | 4 | deg per deg | keeps \|β\| to a few tenths of a degree in a commanded roll; 6–8 saturate the rudder at full pedal |
| aileronRudderInterconnect | 40 | deg per rad/s | what makes roll entry fast (0 → ~0.3, 40 → ~0.8 of the command after 1 s); 50–60 overshoot a sustained full-stick roll by 30–50 % |
| stabilityAxisYawRateGain (**ASSIST**) | 40 | deg per rad/s | Dutch-roll ζ 0.25 → 0.31 on top of the roll loop, and trims the full-stick overshoot |
| yawRateWashoutTimeConstant | 1.5 | s | passes the Dutch roll (period ~2 s), removes a turn's steady yaw rate; 1 s and 2 s behaved alike |
| scheduleGainsWithDynamicPressure | on | — | §5.5 |
| referenceDynamicPressure | 2,745.12 | Pa | **derived**, not chosen: ½ · source density · (300.8 ft/s)², so the schedule is exactly 1 at the tuned trim |
| minimumGainScale | 0.5 | — | never binds in the trimmable range (qbarRef/qbar ≥ 0.57 at 400 ft/s) |
| maximumGainScale | 2 | — | never more than twice the tuned gains; 218.5 ft/s gives 1.9 |

V2 flies inside the **unchanged V1 gameplay envelopes** and actuator travel (`MavF15GameplayControlAuthority.V1()`, MaverickTuning travel, no rate). `MavF15ResearchDemonstratedControlRange`, `MavF15PhysicalSurfaceHardStops` and `MavF15ActuatorRateLimits` are untouched.

---

## 5. How the gains were chosen

Deterministic, scripted, from the validated point-36 trim — never by feel. The design work used an **offline design harness**: the research equations of motion (the same form as `MavF15AfitResearchSourceDynamics`, extended to all four surfaces, calling the frozen coefficient routines), RK4, the one-step law delay, the real `MavF15PilotControlLawV2.Compute` and the real envelopes. Its numbers are **design evidence**, run outside Unity (CoreCLR); the **validation results are the Unity ones** in §7. The harness is not committed; nothing in the repository depends on it.

### 5.1 The aircraft at point 36 (linearization of the research model)

Open-loop modes reproduce WP-3D: short period −0.535 ± 1.359i (ζ 0.37), phugoid ζ 0.15, Dutch roll −0.218 ± 3.238i (ζ 0.067), roll −0.472 /s, spiral −0.158 /s.

| Derivative (per degree of surface, or per rad) | Value |
|---|---:|
| q̇ per stabilator deg | −0.0261 rad/s² |
| ṗ per aileron deg (with 0.3 differential tail) | +0.0364 rad/s² |
| ṙ per aileron deg | +0.00066 rad/s² |
| ṙ / ṗ per rudder deg | −0.00795 / +0.00146 rad/s² |
| L_β (ṗ per rad of β) | **−32.0** rad/s² |
| N_β (ṙ per rad of β) | +1.075 rad/s² |
| L_p | −0.774 /s |
| β̇ per p (kinematic, sin α) | +0.300 |

The key fact: **1° of sideslip makes ṗ ≈ 0.56 rad/s², close to the 0.73 rad/s² of full aileron.** Any sideslip from a body-axis roll stops the roll — which is V1's slow roll.

### 5.2 Pitch

- Pure rate feedback tracks only ~⅓ of the command after 1 s; a feed-forward sized from the model's own response fixes that (`60`).
- The numbers in this subsection come from the linear design model (the research model linearized at point 36, discretized at dt 0.02 with the one-step delay); the nonlinear harness agreed on the chosen gains (half-stick q 0.93 of the command, as Unity then measured).
- **Integrator rejected.** A non-leaky integrator (e.g. 40 deg per rad/s per s) turns the law into attitude hold. At this fixed-thrust, slow trim, that drains airspeed: −22 ft/s after a 1-s half-stick pulse, −66 ft/s after 3 s, toward the 218.5 ft/s domain edge (−82 ft/s away). A leaky integrator avoids that but lowers phugoid damping to ~0.09–0.12 and stretches settling to 6–13 s. Neither is an improvement.
- `Kq 30`: short-period ζ ≈ 0.61; phugoid ζ 0.15 → 0.115 (the cost).

### 5.3 Roll

- Closing on body-axis p (the F-16 form) with no rudder gives ~0.14–0.3 of the command after 1 s at half stick, because sideslip builds.
- Closing on stability-axis p and feeding the interconnect gives ~0.8 of the command after 1 s with \|β\| ≤ 0.35°.
- A constant interconnect sized for half stick (50) pinned the rudder at 15° in a full-stick roll and over-yawed (roll rate +30 % over command). Hence 15°/s full stick and interconnect 40.

### 5.4 Yaw and the ASSIST term

- A body-axis yaw-rate damper barely moves the Dutch roll here: the rudder is weak and this Dutch roll is mostly rolling. **Roll-rate feedback is what damps it** (0.067 → 0.21–0.25).
- The washed-out **stability-axis** yaw-rate term (zero in a coordinated roll) adds 0.25 → 0.31 and makes full-stick rolls better coordinated. Kept, labelled **MAVERICK GAMEPLAY / RESEARCH ASSIST**.
- Pedal: β commands of 3–4° only roll the aircraft and saturate the rudder, hence 1.5° at full pedal with feed-forward + feedback.

Dutch-roll damping (harness, pedal doublet, log decrement): V1 0.068 · V2 (roll loop only, Kp 40) 0.213 · V2 default 0.311.

### 5.5 Dynamic-pressure scheduling — evaluated, adopted

At research trims from 265 to 400 ft/s (above ~400 ft/s the trim stabilator leaves the gameplay envelope; below ~260 ft/s the trim solver stalls). Half-stick 1-s pulses; the bias set to each trim's own stabilator for this evaluation:

| V ft/s | qbarRef/qbar | roll p_s/p_c unscheduled → scheduled | pitch q/q_c unscheduled → scheduled | DR ζ unscheduled / scheduled (V1) |
|---:|---:|---|---|---|
| 265 | 1.29 | 0.32 → 0.41 | 0.73 → 0.73 | 0.29 / **0.46** (V1 **−0.07**, unstable) |
| 280 | 1.15 | 0.50 → 0.57 | 0.98 → 1.09 | 0.31 / 0.34 (0.01) |
| 300.8 | 1.00 | 0.79 → 0.79 | 0.93 → 0.93 | 0.31 / 0.31 (0.07) |
| 330 | 0.83 | 1.21 → 1.08 | 0.59 → 0.49 | 0.26 / 0.23 (0.04) |
| 360 | 0.70 | 1.42 → 1.18 | 0.85 → 1.12 | 0.25 / 0.20 (0.06) |
| 380 | 0.63 | 1.47 → 1.18 | 2.12 → 2.05 | 0.23 / 0.18 (0.06) |
| 400 | 0.57 | 1.48 → 1.16 | **2.26 → 1.04** | 0.22 / 0.16 (0.06) |

Scheduling keeps the roll response closer to the command across the speed range and removes the 2.3× pitch overshoot at 400 ft/s. Dutch-roll damping stays ≥ 0.16 either way, always far above V1. **Adopted**, with the reference at the tuned trim and a clamp of [0.5, 2]. It scales surface demands only — never a load (`[W6]`: exactly 1 at the reference, clamped, continuous, 1 for a non-positive or non-finite qbar).

Pitch at 360–400 ft/s still overshoots after release: the trim stabilator there (−7.8 … −5.2°) sits near the −5° envelope edge, so nose-down authority is clipped. This is a limitation of the V1 gameplay envelope, not of scheduling.

---

## 6. Protections

**None added.** The α, load-factor and roll-rate limiters were considered after the closed loop was stable. None was needed:
- the commanded rates and the unchanged gameplay envelopes keep every scripted manoeuvre inside the research domain;
- in Play Mode, α stays 15.5–20.2°, \|β\| ≤ 0.48° and V 294.8–305.0 ft/s in the full sequence;
- the research aero and thrust already fail closed outside 218.5–699.7 ft/s and the α/β span.

A future limiter would be Maverick-owned gameplay protection, not an F-15 limit.

---

## 7. Results

### 7.1 Headless — `MavF15PilotControlV2Validation`: **54 / 0** (40 before the final cleanup + 14 in `[W12]`)

Covers:
- `[W1]` identity;
- `[W2]` provenance and no F-16 number;
- `[W3]` V1 mapping unchanged and shared;
- `[W4]` neutral = trim bias exactly, plus command and feedback signs and the 0.3 differential tail;
- `[W5]` envelopes, NaN handling, continuity (a 0.01 step moves a surface ≤ 0.2247°, the analytic slope bound), throttle;
- `[W6]` the schedule;
- `[W7]` washout decay (0.370 after one time constant) and reset;
- `[W8]` rig selection and in-flight switch rules;
- `[W9]` ownership (V2 granted; two enabled laws, a disabled bound law, or a non-pilot law refused; V1 still granted);
- `[W10]` moment signs through the real V2 rig in shadow;
- `[W11]` source scan;
- `[W12]` fail-closed law switch (§8.1).

### 7.2 Play Mode — `MavF15PilotControlledV2FlightValidationRunner`: **57 / 0**, two runs byte-identical (52 before the final cleanup; the 52 earlier lines are unchanged, and the new run adds one `[M]` line and four `[X]` checks)

Conditions: dt 0.02, `captureFramerate` 50, from Table VII point 36, real Unity physics.

- **Mechanics** `[M]`, all 26 runs: owner held, one armed body, one enabled law, one load application per step, no duplicate refusals, finite, no teleport, every surface inside its envelope, no aero refusal.
- **Neutral** `[H]`:
  - V1 surfaces exactly at the trim bias; every state within 1.7 × 10⁻⁷.
  - V2 every state within 9.4 × 10⁻⁸ for 20 s; surfaces within 2.9 × 10⁻⁶° of trim; V1 and V2 trajectories differ by ≤ 8.7 × 10⁻⁸.
- **Human V2 prefab** `[K]`: starts itself in V2, holds trim for 5 s within 9.4 × 10⁻⁸; carries its HUD, camera and both laws.
- **V2 pulses** `[S]`: every sign correct; after release the response decays and the surfaces return toward trim.

**V1 vs V2 under identical inputs** `[C]` (0.5 stick/pedal for 1 s, then 6 s centred):

| Input | Law | Δ over pulse (rad/s) | release swing | settle (<10 % peak) | last 1 s | \|β\|max | α range | surfaces max (stab dev / ail / rud) |
|---|---|---:|---:|---:|---:|---:|---|---|
| pitch + | V1 | +0.0729 (q) | 0.68 | > 6 s | 0.0073 | 0 | 16.60–20.82 | 5.00 / 0 / 0 |
| pitch + | V2 | +0.0569 (cmd 0.0611) | 0.52 | 2.74 s | 0.0033 | 0 | 17.39–20.16 | 5.50 / 0 / 0 |
| pitch − | V1 | −0.0431 | 0.48 | 2.76 s | 0.0040 | 0 | 15.25–17.91 | 5.00 / 0 / 0 |
| pitch − | V2 | −0.0378 | 0.37 | 2.70 s | 0.0023 | 0 | 15.46–17.52 | 5.50 / 0 / 0 |
| roll ± | V1 | ±0.0420 (p_s) | 1.30 | > 6 s | 0.0473 | 1.09 | 17.46–17.50 | 0 / 10.00 / 0 |
| roll ± | V2 | ±0.1045 (cmd 0.1309) | 0.08 | 1.66 s | 0.0060 | 0.24 | 17.43–17.52 | 0.04 / 9.16 / 5.85 |
| yaw ± | V1 | ±0.0461 (r) | 0 | > 6 s | 0.0156 | 0.55 | 17.39–17.59 | 0 / 0 / 7.50 |
| yaw ± | V2 | ±0.0278 | 0 | > 6 s | 0.0073 | 0.47 | 17.46–17.52 | 0.02 / 4.48 / 6.00 |
| pitch+roll 0.3 | V1 | +0.0423 (q) | 0.67 | 4.26 s | 0.0041 | 0.66 | 16.97–19.41 | 3.00 / 6.00 / 0 |
| pitch+roll 0.3 | V2 | +0.0338 | 0.51 | 2.68 s | 0.0015 | 0.15 | 17.41–19.05 | 3.30 / 5.50 / 3.52 |

**The tradeoff, stated plainly:**
- **Roll — the target of V2.**
  - 2.5× V1's roll rate over the same half-stick second (80 % of the commanded rate).
  - Less sideslip (0.24° vs 1.09°).
  - The aircraft **stops rolling when the stick is released**: last second 0.006 vs 0.047 rad/s. V1 keeps rolling off after release (release swing 1.30).
  - Cost: the rudder works too (5.9°), and aileron is about the same.
- **Pitch.**
  - V2 is a pitch-rate command: 93 % of the commanded rate, better damped after release, settles in 2.7 s where V1 is still moving after 6 s.
  - Cost: **less raw pitch rate** than V1's direct gearing at the same stick (0.057 vs 0.073 rad/s). That is by design, not an improvement.
- **Yaw.**
  - Pedal gives yaw rate and sideslip of the commanded sense under both laws.
  - V2's roll loop resists the dihedral roll that the sideslip makes, so V2 **yaws less per pedal** than V1 (0.028 vs 0.046 rad/s). That is a tradeoff, not an improvement.
- **Neither is claimed "more realistic".** V2 is more controllable and better damped; V1 is more direct.

**§15 sequence under V2** `[Q]` (neutral – pitch± – roll± – yaw± – pitch+roll – neutral, 42 s):
- every window has the commanded sign;
- every post-release peak decays: roll 0.104 → 0.008 and 0.111 → 0.004 rad/s, where V1 goes 0.079 → 0.079 and 0.111 → 0.111 at the same points;
- no refusal: α 15.55–20.16°, \|β\| ≤ 0.48°, V 294.8–305.0 ft/s, \|φ\| ≤ 9.09°. V1 on the same sequence: \|β\| ≤ 1.60°, \|φ\| ≤ 12.69°;
- every body rate is below 0.0044 rad/s in the final 2 s.

**Timestep refinement** `[D]`: dt / dt/2 against dt/4, ratio 3.00–3.07 on every state. That is first order, as expected for a law one step behind the body with surfaces held over the step.

**In-flight law switch** `[W]`:
- V1 → V2 at 2 s (centred) is accepted.
- At 3.5 s, with roll held, the switch is refused and nothing changes.
- V2 → V1 at 8 s is accepted, and V1 then flies its own gearing again (roll +0.5 → aileron exactly 10°, no rudder).
- Throughout: owner held, exactly one law, 600/600 load applications. The largest surface step across a switch is 0.396°: the residual V2 feedback on decaying rates, since V1 at centred stick is exactly the trim.

**Fail-closed law switch** `[X]` (final cleanup, §8.1), V1 at the trim:
- The trim holds for the first second, within 9.4 × 10⁻⁸.
- Four switch attempts are refused, each with its own reason, and change nothing: stick deflected at 1.5 s, command unavailable at 2.0 s, command source missing at 2.5 s, command source disabled at 2.8 s.
- V1 → V2 at 3 s and V2 → V1 at 6 s are accepted.
- 400/400 load applications, exactly one law every step, no FAULT; the largest surface step across a switch is 0.000°.

**Fail-closed** `[N]`:
- Pilot profile removed → FAULT, body disarmed, no further load.
- The V1 law enabled next to the bound V2 law (a second surface requester) → FAULT, body disarmed, no further load.

### 7.3 Regression (clean worktree of the V2 code commit)

| Gate | Result |
|---|---|
| Frozen F-15 suites (17) | **757 / 0**; every check line identical to the V1 record except the scanned-file counts (+5 FDM files / +3 non-Validation files = the new V2 sources). No frozen numeric output changed |
| Frozen research Play Mode closeout | **39 / 0**, byte-identical to the committed `runtime_closeout/f15rt_playmode_report.txt` |
| Pilot V1 headless | **49 / 0**, check lines identical to the V1 record |
| Pilot V1 Play Mode | **31 / 0**, byte-identical to the V1 record |
| Pilot V2 headless / Play Mode | **40 / 0** headless; Play Mode **52 / 0**, two runs byte-identical |
| Official Baseline v1 gate (F-16/shared) | **PASS 1585 / 0** (25 counted suites), physics delta NONE |

**Final cleanup (fail-closed switch), clean worktree of 7550e85:**

| Gate | Result |
|---|---|
| Compile (Unity Roslyn: runtime, runtime + editor, editor assemblies) | 0 errors |
| Frozen F-15 suites (17) | **757 / 0**, every check line identical to the run above |
| Frozen research Play Mode closeout | **39 / 0**, byte-identical to the committed report |
| Pilot V1 headless / Play Mode | **49 / 0** / **31 / 0**, identical to the V1 record |
| Pilot V2 headless | **54 / 0**, the 40 earlier check lines unchanged |
| Pilot V2 Play Mode | **57 / 0**; two runs byte-identical, and the 52 earlier lines unchanged |
| Official Baseline v1 gate (F-16/shared) | **PASS 1585 / 0**, physics delta NONE |

---

## 8. Ownership and the one shared-core change

- `MavF15PilotControlledOwnershipGrant` accepts the V1 **or** V2 law, and refuses when:
  - both laws are enabled;
  - the bound law is disabled or not on the aircraft;
  - a non-pilot law is bound.
- **`Core/MavFlightPhysicsOwnership.cs`** (additive): the `F15PilotControlledResearch` owner now keeps the grant it was entered with and **re-runs it every physics step**, after the existing body/ID/environment checks. A lost aircraft-layer condition, e.g. a second enabled law, becomes a Fault.
  - Every existing refusal string, the research owner and the F-16/Legacy/Shadow/Fault rules are unchanged.
  - Found by the Play Mode test: the entry check alone let a second law be enabled in flight.
- **In-flight switch.** The rig rebinds only the law (`BindControlLaw`: no mass, profile or physics state is touched).
  - Inside a physics step it evaluates the newly selected law once, immediately, from the same published state it would read at its own turn. Without that, the body re-checks readiness, finds no command from the new law and withholds **every** load for one step, gravity included. The Play Mode test caught this: 598/600 loads before the fix, 600/600 after.
  - V2 then skips its own turn in that step, so its washout never advances twice.

### 8.1 V1/V2 switch fail-closed semantics

Added in the V2 final cleanup. `MavF15PilotControlledRig.TrySetControlMode` handles each case as follows:

| Situation | Result |
|---|---|
| Requested mode already selected, bound to the body, active and enabled, other law not enabled | **Success** ("already …"). No command is read and nothing changes. |
| Command source missing (`commandSource == null`) | **Refused**: "no pilot command source is wired" |
| Command source disabled, or on an inactive GameObject | **Refused**: "the pilot command source is inactive or disabled" |
| `TryGetCommand` fails (no signal) | **Refused**: "the current pilot command is unavailable" |
| A pitch, roll or yaw value that is NaN or infinite | **Refused**: "the current pilot command is not finite" |
| Any of pitch, roll, yaw outside ±0.05 (`SwitchCentredTolerance`, unchanged) | **Refused**: "centre the stick and pedals first" |
| All three within ±0.05 (throttle ignored) | **Switch.** Exactly one law is enabled and bound, and the new law starts with zero filter state. |

- **Before the cleanup**, a missing, inactive or silent source skipped the centring check and the switch went ahead. A NaN axis also counted as centred, because `Mathf.Clamp` keeps NaN and `|NaN| > tol` is false.
- **A refused switch changes nothing**: controlMode, body.controlLaw, which law is enabled, the V2 filter state, the actuator request, the surfaces, either law's request, the owner and its reason, and the arming.
- **A fake "already" is not accepted.** If the other law is also enabled, the fast path is skipped and the full gate runs. A switch that then succeeds leaves exactly one law enabled.
- **An accepted switch inside a physics step** still primes the new law in that step (see above), so loads continue without a gap. The body applies exactly one load set per step, with no dropout and no duplicate.
- Tests:
  - headless `[W12]` covers each row above, both signs of every axis, a NaN and an infinite axis, and live steps with one load application per step after each accepted switch;
  - Play Mode `[X]` runs "SWITCH FAIL-CLOSED V1->V2->V1": V1 holds the trim, four refused attempts (deflected, unavailable, missing, disabled) change nothing, then V1 → V2 and V2 → V1 are accepted, with one law and one load application in every step.

---

## 9. How a human flies it

1. Open an empty scene.
2. **Maverick / F-15 / Place Pilot-Controlled Research F-15 V2 (Assisted) In Scene** (or the V1 item).
3. Press Play. The aircraft starts itself at the point-36 trim.
4. Fly with W/S pitch, A/D roll, Q/E yaw (arrows too).
   - **F2** switches Direct V1 ↔ Assisted V2 with the controls centred.
   - F1 hides the HUD.
   - The HUD shows the active law, and for V2 the commanded q / p_s / β and the gain scale.

**Not done in this work:** no person has flown V1 or V2. The prefab starting itself and holding trim, the scripted responses and the in-flight switch are verified; handling feel is not.

---

## 10. Limitations

- **This is not the F-15 FCS**, and nothing in it is F-15 control data. Every value is Maverick tuning, chosen for the frozen research model at point 36.
- **Research domain only.**
  - Aero and thrust refuse outside 218.5–699.7 ft/s and the α/β span.
  - The model flies the source's fixed 20,000-ft density.
  - Coefficients are extrapolated away from M 0.6.
- **Tuned at one trim.** Scheduling helps between 265 and 400 ft/s (§5.5), but pitch still overshoots after release near 360–400 ft/s. There, the trim stabilator is near the −5° envelope edge.
- **No attitude hold.** After a pedal or roll input the aircraft keeps its new bank and turns gently. Pedal yields a modest sideslip (~60 % of the command in 1 s) and yaws less than V1.
- **Actuators are instantaneous.** No actuator rate is sourced, so the gameplay travel has no rate, as in V1.
- **Other gaps.**
  - Throttle is inactive; there is no F100 throttle law.
  - No protections.
  - No ground model, weapons, sensors, AI or mouse-instructor wiring.
  - Not flown by a human.
- The design harness numbers (§5) are offline design evidence; the Unity results (§7) are the validation.

---

## 11. Files

**Added:**
- `F15/MavF15PilotControlLawV2.cs`
- `F15/MavF15PilotControlGainsV2.cs`
- `Validation/MavF15PilotControlV2Validation.cs`
- `Validation/MavF15PilotControlledV2FlightValidationRunner.cs`
- `Editor/MavF15PilotControlledV2FlightValidation.cs`
- `Assets/MaverickFresh/Prefabs/F15/F15_PilotControlledResearch_V2.prefab`
- this document

**Modified:**
- `F15/MavF15PilotControlApproximation.cs` — shared `SolveFromDemands`; V1 `Solve` unchanged in effect.
- `F15/MavF15PilotControlledRig.cs` — control mode, `BindControlLaw`, `TrySetControlMode`.
- `F15/MavF15PilotControlledAuthority.cs` — grant accepts V1/V2, exactly one law.
- `F15/MavF15PilotControlledDiagnostics.cs` and `…Hud.cs` — active law, V2 internals, F2.
- `Core/MavFlightPhysicsOwnership.cs` — per-step grant re-check.
- `Editor/MavF15PilotControlledPrefabBuilder.cs` — V2 prefab.
- `Tools/fdm_validation_baseline_v1.json` and `Docs/Validation/FDM_VALIDATION_INVENTORY_V1.md` — 3 new uncounted surfaces, builder re-pinned.
- `F15_FINAL_STATE_V1.0.md` and `F15_HANDOFF_V1.0.md` — pointers only.

**Unchanged:** every frozen research file and dataset, the V1 law (`MavF15PilotControlLaw.cs`) and the V1 prefab.
