# 03 — Guidance and Control Algorithms for Mouse-Aim Flight

**Scope:** how a world-space aim direction becomes a *feasible physical intent* (bank / roll rate, load factor or pitch rate, sideslip) that the existing Maverick control laws can execute. The inner loops (rate / load-factor / sideslip augmentation) already exist (doc 05 §4); this document is mostly about the layer above them, and about which classic inner-loop methods are worth adopting later.

Source keys in `[brackets]` resolve in `07_RESEARCH_SOURCES.md`. Every gain, threshold or time constant written below is a **symbol**, not a value; where a number appears it is either a repository fact (cited) or an explicitly labelled worked example. Nothing here is a tuned Maverick value.

---

## 1. Problem statement

Inputs available every physics step (all already published by `MavSixDoFBody`, doc 05 §2):

| Symbol | Meaning | Repository field |
|---|---|---|
| `d̂` | aim direction, world unit vector (from the mouse source) | new |
| `v̂`, `V` | velocity direction and true airspeed | `worldVelocityMps`, `trueAirspeedMps` |
| `R` | body attitude (Transform rotation at the last step) | Transform / `attitude` |
| `b̂` | nose (body +X aero = Unity local +Z) | from `R` |
| `û_b` | body "up" (aero −Z = Unity local +Y) | from `R` |
| `α, β` | angle of attack, sideslip | `alphaRad`, `betaRad` |
| `p, q, r` | body rates | `aeroBodyRatesRadSec` |
| `n_z` | measured load factor | `LoadFactorNz` (when `specificForceValid`) |
| `q̄` | dynamic pressure | `dynamicPressurePa` |
| `g⃗` | gravity, world | profile / environment |

Required output: a `MavPilotCommand` whose *meaning* depends on the bound control law (doc 05 §4.1, finding C1). Therefore the guidance must produce **physical intent first** and convert to stick values last, through a per-law semantics descriptor (doc 06 §4).

Constraints from the product definition (handoff §0–2): no forces, torques, pose writes or velocity alignment; bank-to-turn preferred over flat yaw; target is intent, not promise; deterministic; fail closed.

---

## 2. Physics the guidance must respect

Point-mass equations in wind axes, flat Earth, thrust along the velocity vector folded into `n` (standard flight-mechanics form, `ENGINEERING_INFERENCE`; textbook reference `[STEVENS-LEWIS-ACS]` not inspected; `P_s` per doc 02 §7):

```
V̇            = (T cos α − D)/m − g sin γ
V γ̇           = g (n cos μ − cos γ)
V cos γ · χ̇   = g n sin μ
n             = (L + T sin α) / (m g)          (load factor normal to v̂)
```

- `γ` flight-path angle, `χ` heading of the velocity vector, `μ` bank of the lift vector about `v̂` (wind-axis bank).
- **Rotation rate of the velocity vector in the lift plane** (the quantity a pitch-rate command actually changes):
  `q_w = (g/V)(n − cos γ cos μ)`. For level flight (`γ = 0`) and `μ = φ` this is the relation `MavF16ControlLawV01.ComputePitchRateCommand` already uses (`q = g(n − cos φ)/V`, doc 05 §2).
- **Level coordinated turn:** `n = 1/cos φ`, `ω = g √(n² − 1) / V`. `ENGINEERING_INFERENCE` from the equations above.
- **Energy:** specific energy `E_s = h + V²/(2g)`, specific excess power `P_s = V (T − D)/W` (standard; Rutowski / Boyd–Christie primaries not inspected). Pulling `n` raises induced drag, so a sustained pull with fixed thrust bleeds `E_s`. **The guidance must let that happen**; it must never add thrust or reduce drag to "keep the turn going".
- **Kinematic roll–yaw coupling:** rolling about the body X axis at angle of attack α exchanges α and β: `β̇ ≈ p sin α − r cos α + (lateral force/gravity terms)` for small β (sign per the aero-body convention +X fwd, +Y right, +Z down). Rolling about the velocity vector requires `r = p tan α` `[NASA-TP-1538]`; this is why F-15 V2 closes on stability-axis `p_s = p cos α + r sin α` (doc 05 §4).

The F-15 research rig's numbers make the coupling concrete: trim α 17.5°, so a stability-axis roll needs a body yaw rate of `tan 17.5° ≈ 0.32` of the roll rate (`REPO_DOC` V2 §5.1 gives the kinematic β̇ per p as 0.300 = sin α).

---

## 3. Candidate methods

### 3.1 Cascaded PID / PD on attitude errors (the legacy approach)

`stick_axis = Kp · e_axis + Kd · ė_axis (+ Ki ∫e)` with `e` taken from the target's body-frame components (MouseFlight, `MavInstructorController.RunAutopilot`).

- **Strengths:** trivial, cheap, familiar.
- **Weaknesses:** the error components are not the physical control variables. Lateral body-frame error is mapped to roll *and* yaw, vertical to pitch; at 90° in the vertical plane there is no roll; at 180° every component vanishes (doc 05 L6). Integrators wind up when the aircraft cannot follow (Scenario D). Gains have no physical units, so they cannot be shared across aircraft.
- **Verdict:** keep only as a terminal fine-aim term (small angles), never as the outer guidance.

### 3.2 Quaternion attitude guidance

Compute a desired body attitude `q_des` (e.g. nose on `d̂`, a chosen up vector) and command body rates `ω_cmd = K · 2·sign(q_e,w)·q_e,vec` from the error quaternion `q_e = q_des ⊗ q⁻¹`; the `sign` term picks the short way round `[WIE-BARBA-1985]`.

- **Strengths:** no gimbal lock; well-defined for any error up to 180°; the standard for spacecraft and multirotors.
- **Weaknesses for an aircraft:** it controls *where the body points*, not *where the flight path goes*. An aircraft changes its flight path through lift, i.e. by banking and pulling; a quaternion law that drives the shortest body rotation produces nose slicing and flat yaw turns, which the handoff forbids. The choice of `q_des`'s up vector is exactly the bank decision, so the hard part is moved, not solved. At 180° the short-way sign flips discontinuously unless hysteresis is added.
- **Verdict:** use quaternion/vector algebra for **error representation** (singularity-free), not as the command law.

### 3.3 Body-rate command (rate-command / attitude-hold inner loop)

The pilot stick commands body (or stability-axis) rates; feedback closes on measured rates (`p_c − p`). This is what F-15 V2 and the F-16 V0.1 roll channel already are (doc 05 §4.1), and the public F-16/F-15 CAS architectures contain rate feedback loops `[NASA-TP-1538]`, `[NASA-TN-D-7941]`.
- **Role here:** the guidance's *plant*. The guidance commands `p_s` (via bank error) and `q_w` or `n` (via pursuit); the existing laws track them.

### 3.4 Load-factor / C* command

Pitch stick commands normal acceleration (or a blend `C* = n_z + (V_co/g) q`) `[NASA-TN-D-7941]` (C\* in the early F-15 pitch CAS), `[MIL-F-8785C]`; the original C\* paper was not located (doc 02 §3). Produces approximately speed-invariant stick force per g; protects structural limits naturally because the limit is expressed in the commanded variable.
- **Role here:** the preferred **interface** between guidance and pitch law. Guidance computes `n_cmd` from geometry (§4); a load-factor law (F-16 V0.1) consumes it directly; a pitch-rate law (F-15 V2) receives `q_cmd = (g/V)(n_cmd − cos γ cos μ)`.

### 3.5 Gain scheduling

Gains interpolated on `q̄`, Mach, α. Both Maverick laws already scale with `q̄_ref / q̄`, clamped (doc 05 §4.1). The guidance layer itself needs **no** aerodynamic gain schedule if it commands physical quantities (rad/s, g), because the inner laws own the conversion to surfaces. It does need *capability* scheduling: maximum commanded roll rate and `n` limits come from the law/descriptor, not from guidance tuning.

### 3.6 Linear Quadratic Regulator (LQR)

`u = −K x`, `K` from the Riccati equation for a linearization `ẋ = Ax + Bu` and weights `Q, R`.
- **Strengths:** systematic multivariable damping (Dutch roll + roll + spiral jointly); the repository already linearized the F-15 model at point 36 for V2 design (V2 doc §5.1), so an offline LQR design is technically within reach; NASA's F-16 check-case controller in the library is an LQR SAS (`Docs/Research/Aerospace/Aircraft/F16/F16_FCS_SOURCE_GRAPH.md`).
- **Weaknesses:** one design per flight condition (needs scheduling); handles neither saturation nor the large-angle geometry of Scenarios B/C/F; weights are tuning in disguise.
- **Verdict:** possible P3 tool for *inner-law* redesign (offline gain synthesis), not for the guidance.

### 3.7 Nonlinear Dynamic Inversion (NDI) and Incremental NDI (INDI)

NDI for rotational dynamics: with `ω̇ = J⁻¹(M(x,δ) − ω × Jω)` and control effectiveness `G = ∂M/∂δ`, choose `δ` such that `ω̇ = ν` (a desired angular acceleration from a linear outer loop): `δ = G⁻¹ (J ν + ω × Jω − M₀(x))` `[ENNS-1994]`, `[SNELL-1992]`, `[ENNS-MCT-1994]`. Time-scale separation: fast loop on rates, slow loop on attitude/flight path. INDI replaces the model `M₀` by the measured angular acceleration and only needs `G`: `Δδ = G⁻¹ J (ν − ω̇_measured)` `[SIEBERLING-2010]`. F-35 control laws are publicly described as dynamic-inversion based `[F35-NIXON-2004]` (see doc 02 §5 for verification status).

- **Strengths:** aircraft-independent structure; the outer loops become kinematic and can be shared across aircraft; handles changing `q̄` without explicit schedules.
- **Weaknesses for Maverick now:** needs an onboard control-effectiveness model. Maverick *has* the frozen aero models, but calling them from a control law creates a second evaluator of the same physics (an ownership/duplication question, not a load-application one), and the F-15 research model's weak, narrow-domain effectiveness (V2 doc §5.1: 1° β ≈ 0.77 of full aileron's roll acceleration) makes inversion ill-conditioned. INDI needs a clean angular-acceleration signal; Maverick has one-step latency and instantaneous actuators, which INDI tolerates poorly without a model of the delay.
- **Note:** the guidance layer proposed in §4 **is** the outer kinematic loop of an NDI design (it inverts the point-mass equations to get `n` and `μ`). That outer loop is reusable later if inner laws move to NDI.
- **Verdict:** P3 research for inner laws; the outer-loop half is adopted now.

### 3.8 Model Predictive Control (MPC)

Solve, each step, `min Σ ‖x_k − x_ref‖²_Q + ‖u_k‖²_R` subject to dynamics and limits over a horizon.
- **Strengths:** explicit constraint handling; principled reversal-direction choice (compare costs of left/right).
- **Weaknesses:** needs a prediction model (which model? the frozen aero → duplicate evaluator; a simplified model → model mismatch); QP per step at 50 Hz in C# with deterministic fixed-iteration solvers is possible but large engineering effort; tuning weights is again tuning; difficult to explain to a reviewer when it misbehaves.
- **Verdict:** not justified. The one problem where it would help (turn-direction choice at 180°) is solved more simply by geometry + hysteresis (§5.3).
- **Related prior art:** Gaijin's patent describes a lightweight *look-ahead* variant — predicting the aircraft's angular motion a fixed number of steps ahead and choosing controls from the target's direction in the predicted frame `[GAIJIN-PATENT-US8770979]` (doc 01 §1.1). It needs a model of the aircraft's angular response, which in Maverick would again be a second evaluator of the frozen physics or a simplified surrogate. Recorded as a P3 alternative for overshoot suppression; not proposed, and not to be implemented from the patent text without a proper legal-status check.

### 3.9 Control allocation / actuator-constrained control

Distribute a desired moment vector over redundant surfaces subject to limits: pseudo-inverse, redistributed pseudo-inverse, weighted least squares, direct allocation `[DURHAM-1993]`, `[BODSON-2002]`, `[HARKEGARD-2002]`.
- **Maverick status:** F-15 rig has four channels (symmetric stab, aileron, differential stab, rudder) but differential stab is fixed at 0.3 × aileron (sourced research relation, V1 doc §2); F-16 has three. There is no redundancy to allocate in the current models.
- **Relevant part:** *saturation awareness*. When a surface saturates, the guidance should not keep increasing demand; with a proportional guidance law and no integrators this is automatic. Allocation becomes relevant only if a future model adds independent flaperons, LEF or thrust vectoring.
- **Verdict:** not needed now; keep laws' anti-windup.

### 3.10 Comparison

| Method | Commands | Model needed at runtime | 180° / vertical robust | Saturation behaviour | Runtime cost / step | Fits current laws | Recommendation |
|---|---|---|---|---|---|---|---|
| PID on body-frame error | stick | no | **no** | windup if Ki | trivial | yes (wrongly) | terminal fine-aim only |
| Quaternion attitude | body rates | no | yes (with hysteresis) | ok | trivial | partially; promotes flat turns | error representation only |
| Body-rate command | rates | no | n/a (inner) | law anti-windup | trivial | **is** the current laws | keep (plant) |
| Load-factor / C* | n | no | n/a (inner) | natural g limit | trivial | F-16 yes; F-15 via q conversion | **interface** |
| Gain scheduling | — | schedule tables | n/a | — | trivial | already in laws | inner only |
| LQR | surfaces | linear model (offline) | no | none | small | would replace laws | P3 offline design aid |
| NDI / INDI | surfaces | effectiveness model | outer loop yes | needs allocation | moderate (aero calls) | would replace laws | P3 research; outer loop now |
| MPC | surfaces/rates | prediction model | yes | explicit | high | would replace laws | no |
| Control allocation | surfaces | effectiveness | n/a | explicit | small–moderate | no redundancy today | not now |
| **Lift-vector pursuit (this doc §4)** | `μ`/`p_s`, `n`/`q`, `β` | **no** | **yes** (vector + memory) | proportional, no windup | ~100 flops | yes, through descriptor | **adopt (P0/P1)** |

---

## 4. Recommended guidance: lift-vector pursuit with roll-then-pull sequencing

`MAVERICK_PROPOSAL`. Classical in structure (pure-pursuit acceleration command + coordinated bank-to-turn, the outer loop of an NDI design); no claim that War Thunder or any production FCS uses this exact form.

### 4.1 Geometry (all world-frame vectors, no Euler angles)

```
θ       = angle(b̂, d̂)                       nose error (pointing semantics, §6)
e       = d̂ − (d̂·v̂) v̂                       aim component normal to the flight path
ê_turn  = TurnDirection(e, memory)          unit, see §5.3 for |e| → 0 near 180°
g⊥      = g⃗ − (g⃗·v̂) v̂                       gravity normal to the flight path
l̂       = normalize(û_b − (û_b·v̂) v̂)        current lift direction (wind-axis "up")
```

### 4.2 Commanded turn rate and acceleration

```
ω_cmd   = min( K_θ · θ_v , ω_cap )           θ_v = angle used for turning (§6), K_θ [1/s]
a_turn  = V · ω_cmd · ê_turn                 desired change of v̂, m/s²
a_lift  = a_turn − g⊥                        lift-plane acceleration the airframe must supply
n_req   = |a_lift| / g                       (≥ 0)
l̂_des   = a_lift / |a_lift|                  (only if |a_lift| > a_min; else hold l̂)
```

- At `θ → 0`, `a_turn → 0` continuously and `l̂_des → −ĝ⊥` (wings level relative to gravity, 1 g); **no normalization of a vanishing vector is needed**. This is what removes Scenario A's roll thrash without an "aggressive turn angle" switch.
- `ω_cap` is not an aircraft limit: it is the guidance's request ceiling, derived from the law descriptor (`n_max` of the law or domain cap) as `ω_cap = g √(n_max² − 1)/V` in level flight, recomputed every step.

### 4.3 Push-or-roll choice (avoids rolling inverted for a small "below" correction)

Two lift solutions exist: `(l̂_des, +n_req)` and `(−l̂_des, −n_req)`.

```
Δμ₊ = signed angle about v̂ from l̂ to  l̂_des
Δμ₋ = signed angle about v̂ from l̂ to −l̂_des
choose (−) only if  −n_req ≥ n_min_cmd  and  |Δμ₋| + H_push < |Δμ₊|      (hysteresis H_push)
```

Positive-g preference is a physical asymmetry of fighters (negative-g limits are much smaller) and of the human pilot; the margin `H_push` and `n_min_cmd` are guidance tuning, labelled.

### 4.4 Bank channel → stability-axis roll-rate command

```
Δμ      = chosen bank error, wrapped to (−π, π]
p_s,cmd = sign(Δμ) · min( K_μ |Δμ| ,  √(2 · a_p · |Δμ|) ,  p_cap )
```

- The square-root term is the time-optimal approach with a deceleration bound `a_p` (rad/s²): it lets large bank changes run at `p_cap` and still arrive without overshoot. `a_p` is a guidance assumption about the law's roll-acceleration capability; it is tuning, and it must be validated per law (doc 06 §8, metric M3).
- `p_cap ≤` the law's full-stick roll rate (descriptor). On F-15 V2 this is 15 °/s; on F-16 V0.1, 270 °/s (both Maverick tuning).
- Roll is commanded about the **velocity vector**. F-15 V2 executes that natively; for the F-16 V0.1 body-axis law the guidance can only send `p_b,cmd = p_s,cmd` and accept the β transient (doc 05 C2).

### 4.5 Pitch channel with roll-then-pull sequencing

```
w       = cos²( clamp(Δμ, −π/2, π/2) )                         0 when 90° off the lift plane
n_hold  = clamp( cos γ · cos μ , n_min_cmd , n_max_cmd )       holds the flight path in the current bank
n_cmd   = n_hold + w · ( n_req − n_hold )
n_cmd   = SoftSaturate(n_cmd, [n_min_cmd, n_max_cmd])          MavControlLawProtections, existing
```

Conversion to the law's pitch semantics:
- load-factor law: `pitch_stick = DescriptorInverse(n_cmd)`;
- pitch-rate law: `q_cmd = (g/V)(n_cmd − cos γ cos μ)`, `pitch_stick = q_cmd / q_full`.

Pulling only when the lift vector is nearly on the turn plane is what real pilots do and what avoids "pulling the wrong way" in Scenario B. Because `w` is smooth, there is no mode switch.

### 4.6 Yaw channel

Default `β_cmd = 0` (coordinated); the law's ARI/sideslip loops do the coordination. Optional **terminal fine aim** (P2): for `θ < θ_fine`, `β_cmd = −sat(K_β · e_lat, β_cap)` where `e_lat` is the lateral nose error, `β_cap ≤` the law's full-pedal sideslip (1.5° F-15, 8° F-16). Sideslip moves the nose relative to the flight path physically (side force, extra drag); it is not a yaw-pivot shortcut because its authority is bounded by the law's own sideslip command range.

### 4.7 Output conditioning

- Rate-limit each *physical* command (`ṗ_s,cmd`, `ṅ_cmd`), not the stick, with limits from the descriptor; reset filters on enable, on source switch and on law switch (V2 §8.1 pattern).
- All outputs finite or the source reports no signal (fail closed through `SignalLossPolicy`).
- Publish `θ, Δμ, ω_cmd, n_req, n_cmd, p_s,cmd, w, ê_turn, mode flags, saturation flags` to telemetry.

### 4.8 Cost

Per step: two `normalize`, four dot/cross products, one `atan2`, one `sqrt`, a few clamps — on the order of 100 floating-point operations. Negligible against one aero evaluation.

---

## 5. Scenario analysis

`ENGINEERING_INFERENCE` unless marked. "Metric" names refer to doc 06 §8.

### 5.1 Scenario A — small correction (5–10°)

- **Strategy:** §4 as is. With `θ` small, `μ_des ≈ atan(V K_θ θ_lat / g)`: a coordinated shallow bank proportional to the lateral error, returning to wings level as the error closes.
- **Reasoning — no roll oscillation condition:** with a stationary target, heading error obeys `θ̇ ≈ −(g/V) tan μ`; with `μ` following `μ_des` through a first-order roll response of time constant `τ_r` (law + bank loop), the loop is second order with `ζ = 1 / (2 √(K_θ τ_r))`. **No overshoot requires `K_θ τ_r ≤ 1/4`; `ζ ≈ 0.7` requires `K_θ τ_r ≈ 0.5`.** This gives a principled way to set `K_θ` from the *measured* roll response of each law, instead of feel.
- **Worked example (illustrative only):** at `V = 91.7 m/s` (F-15 trim), `K_θ = 0.5 /s`, `θ_lat = 5°`: `V K_θ θ / g ≈ 0.41` → `μ_des ≈ 22°`. At 15 °/s that bank takes ≈1.5 s to reach, so `τ_r` is of order 1 s and `K_θ = 0.5 /s` would be underdamped by the rule above; `K_θ ≈ 0.25–0.5 /s` is the region to test. These are *not* recommended values.
- **State:** `d̂, v̂, b̂, û_b, V, g⃗`, roll response.
- **Numerical issues:** none at `θ → 0` (no normalization of `e`).
- **Protection:** command rate limits; terminal fine-aim yaw optional.
- **Validation:** M1 (time to 2°), M2 (overshoot), M4 (roll-reversal count = 0 for a step ≤ 10°).

### 5.2 Scenario B — 90° turn

- **Strategy:** `ω_cmd` saturates at `ω_cap`; `Δμ` large at first, so `w ≈ 0` → roll first at `p_cap` (square-root approach), then `w → 1` → pull to `n_req` (≈ `n_max_cmd`). As the target comes round, `θ` falls below `ω_cap/K_θ`, the pull relaxes, bank relaxes.
- **Reasoning:** time-optimal-like bank capture + maximum-rate turn without exceeding the law's own limits; energy bleeds as physics dictates (fixed thrust).
- **State:** as A, plus `n_z`, law limits.
- **Numerical issues:** `Δμ` wrap at ±π (use `atan2` of the cross/dot pair).
- **Protection:** law limiters (F-16); guidance domain cap on `n_cmd` for F-15 (doc 05 C3).
- **Validation:** M1, M3 (bank capture overshoot), M5 (`|β|max`), M7 (energy loss reported, not minimized), M6 (saturation time fraction).
- **F-15 rig expectation (inference):** ≈6 s to reach a 90° bank at 15 °/s and the turn itself at ≤ 7 °/s — the test validates sequencing and smoothness, not agility (doc 05 C4).

### 5.3 Scenario C — 180° reversal

- **Problem:** `e = d̂ − (d̂·v̂)v̂ → 0` as `d̂ → −v̂`, so `ê_turn` is undefined; any instantaneous choice flickers between sides.
- **Strategy (turn-direction memory with hysteresis):**
  ```
  if |e| > ε_dir:            ê_raw = e/|e|
  else                        ê_raw = ê_prev  (or, with no history, l̂  ⇒ "pull in the current lift plane")
  s     = smoothstep(θ_lock, π, θ_v)        0 below θ_lock, 1 at 180°
  ê_turn = normalize( (1 − s)·ê_raw + s·ê_prev )
  switch side only if the target is ≥ H_side beyond the plane through v̂ and ê_prev
  ```
  With no history (e.g. aim snapped behind on enable), choosing the *current lift plane* means "pull" (a vertical half-loop if wings are level), which needs no roll and no yaw — the least-discontinuous physical choice. Choosing "toward the lower-energy side" (pull down) is an alternative; both are tuning decisions and must be exposed.
- **Why not quaternion shortest path:** it would choose by body geometry, not by lift/energy, and flips at exactly 180°.
- **State:** `ê_prev` (guidance memory, reset on enable).
- **Validation:** M8 (number of turn-direction changes during a reversal = 0 for a stationary target), M9 (max command step per tick bounded), pose/velocity continuity checks already in Play Mode `[M]` style runners (no teleport).

### 5.4 Scenario D — low speed, insufficient authority

- **Strategy:** do nothing special in the geometry. The law cannot reach `n_cmd`/`p_s,cmd`; tracking error stays large; the aircraft turns as fast as physics allows. Because guidance is proportional with no integrator, there is **no windup**; when authority returns, there is no stored command to unwind.
- **Guidance must not:** raise gains as speed falls ("authority boost" in the legacy instructor, doc 05 §3), add thrust, or ignore the law's limiters.
- **F-15 rig specifics:** the trim (300.8 ft/s) is already near the domain floor (218.5 ft/s); a guidance-side *domain* soft cap should reduce `n_max_cmd` as `V → V_domain_min` and as `α → α_domain_max`, labelled `MAVERICK_TUNED_NON_AUTHORITATIVE — protects model validity, not an aircraft limit`.
- **Validation:** M6 (saturation visible in telemetry), M10 (no surface command beyond envelope — already enforced), and "authority shortfall" telemetry: `n_cmd − n_z`, `p_s,cmd − p_s`.

### 5.5 Scenario E — high speed

- **Strategy:** command load factor, not pitch rate. A fixed pitch-rate demand corresponds to `n ≈ 1 + Vq/g`, i.e. more g at higher V; a load-factor interface keeps the g limit explicit. For a pitch-rate law (F-15 V2) the conversion `q_cmd = (g/V)(n_cmd − cos γ cos μ)` automatically lowers `q` at high V.
- **Energy:** high-g turns at high speed bleed energy quickly; telemetry must show `P_s` sign so a human tester sees it is physics, not a bug.
- **Model limit:** neither current aero model is valid beyond M 0.6 (doc 05 §6). High-speed scenario tests can only be run inside that domain.
- **Validation:** M1/M2 at two speeds in-domain; M11 (peak `n_z` ≤ `n_max_cmd` + law tolerance).

### 5.6 Scenario F — target near zenith / nadir

- **Strategy:** the formulation uses `v̂`, `d̂`, `g⃗`, `û_b` only; nothing is Euler-angle based, so a vertical flight path is not singular. `g⊥ → 0` when `v̂` is vertical, so wings-level-to-gravity loses meaning exactly when it should.
- **Remaining singularity:** `l̂` is undefined only if `û_b ∥ v̂` (β or α ≈ 90°) — outside both models' meaningful domains; treat as invalid state → hold.
- **Telemetry:** report bank/heading only with `MavAttitude.nearVerticalSingularity` respected; never feed Euler bank back into the guidance.
- **Validation:** M12 (command continuity while the aim crosses the zenith; no step > bound), pure-math tests with `v̂` exactly vertical.

### 5.7 Scenario G — high AoA and sideslip

- **Strategy:** command stability-axis roll; keep `β_cmd = 0`; let the law's α limiter (F-16) or the guidance domain cap (F-15) bound `n_cmd`. The guidance never decides post-stall behaviour.
- **Model limits:** F-16 Morelli fit α ≤ 45°, |β| ≤ 30°, subsonic; F-15 research rig validated near α 15–20° in its Play Mode runs (V2 §7.2), domain wider but unexercised. No post-stall, spin or departure claims.
- **Validation:** M5 (`|β|max`), M13 (α excursions stay inside the stated domain; out-of-domain is reported, never silently clamped by guidance).

---

## 6. Target semantics: nose, flight path, or camera?

Three directions must not be conflated (handoff §3A):

| Direction | Definition | Who wants it |
|---|---|---|
| Camera / mouse aim `d̂` | what the player points at | input |
| Nose / boresight `b̂` | body +X; where fixed guns point | aiming |
| Flight path `v̂` | velocity direction | where the aircraft goes |

They differ by α (and β): ~17.5° on the F-15 research trim (doc 05 C5), a few degrees on a fast fighter in 1 g.

Options for `θ_v` (the turning error) in §4.2:
1. **Flight-path pursuit:** `θ_v = angle(v̂, d̂)`. Trajectory-honest; the nose ends α above the marker.
2. **Nose pointing:** `θ_v = angle(b̂, d̂)` projected into the turn plane. In steady straight flight with the nose on the marker, `θ_v = 0`, `n = 1`; the flight path passes α below. Consistent and stable for a stationary direction.
3. **Hybrid:** turn direction (`ê_turn`) from flight-path geometry, magnitude from nose error.

`MAVERICK_PROPOSAL`: option 2/3 as default ("the nose goes where the mouse is"; War Thunder officially matches a body-fixed axis — longitudinal axis until 2024, gun reticle since — to the cursor, doc 01 W9), option 1 selectable and labelled, and **both errors always in telemetry**. The coupling (more pull → more α → nose moves relative to `v̂`) is a feedback path to check in validation (M2 at the low-speed trim).

---

## 7. Guidance-level risks

| Risk | Mechanism | Mitigation |
|---|---|---|
| Lateral loop oscillation | `K_θ τ_r` too large | set `K_θ` from measured roll response (§5.1 rule); test M4 |
| Bank flicker near `θ ≈ 0` with vertical-only error | `l̂_des` direction jumps when `a_lift` crosses small lateral values | continuity is inherent in the vector sum; add `a_min` hold band |
| Turn-side flicker at 180° | `ê` undefined | memory + hysteresis (§5.3) |
| Push/roll flip | two lift solutions | hysteresis `H_push` (§4.3) |
| Frame-rate dependence | guidance or filters in `Update` | compute inside `TryGetCommand` at the law's poll; input integrated in `Update` from raw deltas only |
| Hidden physics | guidance adds speed/energy assists | forbidden; source scan |
| Wrong law semantics | stick values interpreted differently by another law | descriptor (doc 06 §4); validation that every law used with the instructor publishes one |
| Over-trust of model | guidance tuned where the model is invalid | domain caps + out-of-domain telemetry |
