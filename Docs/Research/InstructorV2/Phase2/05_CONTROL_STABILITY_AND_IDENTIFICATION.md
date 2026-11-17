# 05 — Closed-Loop Stability and System Identification

**Purpose:** give Maverick a way to *measure* how the guidance + control-law + aircraft loop behaves, so that guidance gains, limits and claims ("damping ≈ 0.45", "slower than the pitch loop") rest on measurements instead of trial and error.

**Evidence classes:** PV (read in the original; "lead" = by the lead researcher), SV, EI, UV, MP. External methods were collected by a research sub-agent of this session (NASA NTRS PDFs, rendered where scanned); the lead re-read Morelli AIAA-99-4043 for the core frequency-domain method. Source keys: doc 09.

**No universal constant is proposed.** Every number below is a source statement, a worked example (labelled), or a quantity to be measured.

---

## 1. The loop as it exists

From the development branch's local commit `90add24` (read only; unpushed; may change) and the repository:

```text
mouse aim (Update) ─► MavMouseAimGuidance (FixedUpdate, order −350)
     outer: flight-path rate gain K_θ = 0.6 /s; bank P-loop with time constant 1.0 s; roll command capped at the law's 15 °/s
     output filter: first-order, τ = 0.08 s; rate limit 4 /s (stick units)
 ─► MavPilotCommand ─► MavF15PilotControlLawV2 (order −300; reads the previous step's state)
     pitch: q command, 7 °/s full stick, FF + feedback; roll: stability-axis p command, 15 °/s, FF + feedback + ARI
     yaw: β command 1.5°, washed-out stability-axis yaw-rate term; gains scaled by q̄_ref/q̄ in [0.5, 2]
 ─► MavF15ControlActuator (order −200; instantaneous, gameplay travel) ─► research aero ─► MavSixDoFBody (−100) ─► PhysX
```

| Element | Value | Status |
|---|---|---|
| Physics step | 0.02 s | REPO_DOC |
| Sensor latency | one step: the law reads the state published in the previous step | REPO_DOC |
| Actuators | instantaneous; no sourced rate (F-15 and F-16) | REPO_DOC |
| Guidance gains | `MAVERICK_TUNED_NON_AUTHORITATIVE`; the outer-gain rationale ("damping near 0.45") is an *estimate* in the code comments, not a measurement | dev-branch code (read) |
| V2 law gains | `MAVERICK_TUNED_NON_AUTHORITATIVE`; designed on a linearisation at point 36 | REPO_DOC (V2 §5) |

**Effective delay in the inner loop (EI):** one-step state latency τ = T = 0.02 s, plus roughly T/2 for holding the command over the step (zero-order-hold equivalence, `[MIT-16.06-L31]`, SV) — about 0.03 s. Phase cost ∠e^{−jωτ} = −ωτ: ≈ 1.7° at 1 rad/s, 8.6° at 5 rad/s, 17° at 10 rad/s. For the slow research F-15 (open-loop short period ≈ 1.46 rad/s, from V2 §5.1’s −0.535 ± 1.359i; EI) this is small; for an agile model with crossovers near 10 rad/s it is not.

---

## 2. Outer/inner separation — what the sources do and do not say

| Statement | Provenance | Class |
|---|---|---|
| Inner loop at least ~4× faster than the outer loop; 10× "normally recommended"; typical σ = τ_c,outer/τ_c,inner = 4–10 | Skogestad (NTNU lecture slides) citing Shinskey 1981 p. 12 (process control) | SV |
| "A factor of ten has often been used" (a pole a decade away contributes little phase) | Mallik 2022 (arXiv abstract) | SV (heuristic) |
| Aircraft outer/inner ratio ≈ 5–10 | reportedly in Tischler et al. 2017 (excerpt not reachable) | UV |
| NDI fast-rate / slow-attitude split | Snell, Enns, Garrard; no numeric ratio found in readable text | UV for any number |
| "3–5×" | vendor/process blogs only | UV |

**Conclusion:** separation ratios are rules of thumb with process-control provenance. They are a *starting point*, not a gate. What can be gated is what they are meant to protect: **stability margins and response shape, measured on the actual loop.**

A useful design device with a traceable source (Skogestad slides, SV): model the *closed* inner loop as an equivalent first-order lag plus delay, `T_inner(s) ≈ e^{−θ s}/(τ_c s + 1) ≈ e^{−(θ + τ_c) s}`, and design the outer loop on that. Phase 1's rule `ζ ≈ 1/(2√(K_θ τ_r))` is the same idea for the bank-to-turn loop with `τ_r` = the identified closed-loop bank time constant (doc 03 Phase 1 §5.1). **Both need `τ_c` / `τ_r` measured** (§4).

**Worked check of the development values (illustrative, EI):** with `K_θ = 0.6 /s` and the guidance's own bank time constant `τ = 1.0 s`, the first-order rule gives ζ ≈ 1/(2√0.6) ≈ 0.65. The V2 roll response and the 0.08 s output filter add lag, so the true τ_r is > 1 s and ζ lower — the code comment's ≈ 0.45 is plausible but unmeasured. **The measurement in §4.3 decides.**

---

## 3. What to identify, and where to inject

All injections are **commands**, never forces or surface overrides, so the aerodynamic model and ownership rules are untouched.

| Quantity | Injection point | Measured output | What it characterises |
|---|---|---|---|
| Inner closed loop, pitch | add a test signal to `MavPilotCommand.pitch` from a scripted, validation-only command source | q (and n_z) vs q_cmd | V2 pitch tracking: bandwidth, equivalent delay, LOES |
| Inner closed loop, roll | `MavPilotCommand.roll` | stability-axis p vs p_cmd; bank φ | roll mode τ_R, equivalent delay |
| Inner closed loop, yaw | `MavPilotCommand.yaw` | β vs β_cmd | sideslip loop |
| Bank-capture loop (guidance + law) | add a test signal to the guidance's **desired bank** (guidance-internal, validation-only hook) | φ vs φ_des | τ_r used in the K_θ rule |
| Outer guidance loop | perturb the **aim direction** (scripted target) | angular error, flight-path direction | outer crossover, damping of the aim loop |
| Broken-loop margins of the law | validation-only wrapper law that calls the real `MavF15PilotControlLawV2.Compute(...)` and adds the injection to its **surface request** before the actuator | total surface request vs injection | loop transfer L, sensitivity S, gain/phase/delay margins at the plant input |
| Bare airframe (optional) | same wrapper, law output frozen at trim + injection (open loop) | rates, α, β | control effectiveness and modal parameters; collinearity-free only if the law is open |

Rules for injection (from the sources):
- Inject **before** any software command limiter (Grauer, Heeg & Morelli; Morelli 2021 slides — PV).
- Size the amplitude to stay linear and **prove it**: repeat at 0.5× and 2×; frequency responses must overlay (EI from cited practice). Non-overlay is the saturation signature (§6).
- Under feedback, states and controls become nearly proportional and individual derivatives cannot be identified from closed-loop data alone (Morelli AIAA-99-4043, **lead PV**). Identify closed-loop *transfer functions* from closed-loop data; identify *derivatives* only from open-loop (law frozen) runs.

---

## 4. Methods

### 4.1 Time-domain step, doublet and 3211 responses (fast, intuitive)

| Metric | Definition | Source |
|---|---|---|
| Roll-mode τ_R and effective delay τ_eff | `t1` = stick reaches 50 % of max; `t2` = maximum-slope tangent of p(t) crosses zero; `t3` = p reaches 63 % of max; `τ_eff = t2 − t1`, `τ_R = t3 − t2` | NASA TP-2004-212046 p. 8, Fig. 4 (PV) |
| Better in simulation | least-squares fit of `p/δ = K e^{−τ s}/(τ_R s + 1)` to the whole response | EI |
| Second-order fit | overshoot `M_p = exp(−ζπ/√(1−ζ²))`; log decrement `δ = ln(x_n/x_{n+1})`, `ζ = δ/√(4π² + δ²)`; `ω_d = ω_n√(1−ζ²)`; 2 % settling ≈ `4/(ζω_n)` | standard (EI; no source inspected this session) |
| Inputs | doublet and 3211 (3:2:1:1 step ratio) | UV this session (Plaetschke & Schulz; Koehler) |

Use these for quick regression numbers; prefer fitted models (4.2–4.4) for design decisions.

### 4.2 Multisine design (preferred input for a deterministic simulator)

- `u_j(t) = Σ_{k∈K_j} a_k sin(2π k t / T + φ_k)`, uniform `a_k = A_j/√M_j`; relative peak factor `RPF = (max u − min u)/(2√2 · rms u)` (Grauer & Boucher; Morelli 2011 — PV).
- Harmonics assigned alternately to different inputs (orthogonal, simultaneous excitation); phases optimised (simplex) for low RPF, or Schroeder phases `Φ(n) = π n²/n_F` (Bosworth & Burken TM-113085, PV).
- Frequency resolution 1/T; **at least two** cycles of the lowest frequency (Grauer & Boucher, PV); Bosworth & Burken found **three** cycles gave reasonable results (PV).
- Start and end at zero; fade in, or discard the first period (Grauer 2021; Morelli & Grauer — PV).
- Band: rigid-body dynamics of piloted aircraft lie in ≈ 0.01–1.5 Hz; 0.04 Hz spacing over 0.1–1.5 Hz (36 frequencies) was adequate in Morelli's examples (**lead PV**, AIAA-99-4043). A NASA F-16 *simulation* example used T = 20 s, 0.1–2.0 Hz in 0.1 Hz steps, 2° peak-to-peak elevator, and obtained short-period frequency responses within 10 s (Grauer, Heeg & Morelli, PV). At 50 Hz the Nyquist frequency is 25 Hz, so the band is far from aliasing.

### 4.3 Frequency-response estimation

- Direct ratio at the injected harmonics: `Ĝ_ij(jω_k) = y_i(jω_k)/u_j(jω_k)` (Grauer 2021, PV); Fourier transform by chirp-z for accuracy (SIDPAC `fint.m`), or the recursive DFT `X_i(ω) = X_{i−1}(ω) + x_i e^{−jω i Δt}` (Morelli AIAA-99-4043 Eqs. 19–20, **lead PV**).
- With feedback, use the joint input-output form `Ĝ = (y/r)(u/r)⁻¹` with r the injected reference (Grauer 2021 Eq. 6, PV).
- **Coherence caveat:** for fixed-frequency multisine/Fourier-ratio estimates without windowing or averaging, coherence is identically 1 and carries no information (Morelli & Grauer §IV.C, PV). In a noise-free deterministic simulator, use per-frequency R² and least-squares error bounds; treat poor fits as nonlinearity, transients or leakage, not noise (EI).

### 4.4 Model fitting

| Method | Equations | Use in Maverick | Source |
|---|---|---|---|
| Frequency-domain equation error | `jω x̃ = A x̃ + B ũ`; per state row `Y = Xθ + ε`; `θ̂ = [Re(X†X)]⁻¹ Re(X†Y)`; `cov θ̂ = σ²[Re(X†X)]⁻¹`; excluding ω = 0 removes trims and biases | bare-airframe derivatives from open-loop runs; starting values | Morelli AIAA-99-4043 Eqs. 9–18 (**lead PV**) |
| Frequency-domain output error | `ỹ(ω) = [C(jωI − A)⁻¹B + D] ũ(ω)`; Gauss–Newton on frequency residuals; biases estimated afterwards in the time domain | refined LTI models | Morelli & Grauer Eqs. 87–91 (PV) |
| Low-order equivalent system | `q/δ = K_q (s + L_α) e^{−τ_eq s}/(s² + 2ζ_e ω_e s + ω_e²)`; cost `(ΔGain_dB)² + 0.0175 (ΔPhase_deg)²`; `L_α = (N_z/α) g/V` held fixed; fit 0.2–10 rad/s | characterise the V2 pitch response (and later any law) with interpretable parameters | NASA TP-2004-212046 Eqs. 1–3 (PV) |
| Modal estimation (ERA/Prony) | discrete poles `z_i`; `λ_i = ln(z_i)/T` | free responses (release-to-hold) | Juang & Pappa 1985 (bibliographic only); EI |

### 4.5 Handling-quality descriptors (reported, not used as gates)

- **Bandwidth / phase delay** from θ/stick: `ω_BW = min(ω at +6 dB gain margin, ω at −135° phase)`; `τ_p = −(φ(2ω_180) + 180°)/(57.3 · 2ω_180)` (TP-2004-212046 p. 23–24, PV; the printed Eq. 5 appears to carry a sign typo — EI).
- **CAP** from the LOES fit: `ω_e²` and `n/α ≈ V L_α/g` (EI; the CAP source itself not inspected this session; definition PV in MIL-F-8785C per Phase 1 doc 02).
- These describe the *tuned Maverick law at one research trim*. Comparing them with MIL-F-8785C levels is informative, but MIL boundaries apply to aircraft, and none of these laws is an aircraft FCS.

### 4.6 Stability margins by loop breaking (validation-only)

- Inject `u` at the actuator input with orthogonal multisines and measure the controller output `x`; then `x/u = HG(I + HG)⁻¹` (G controller, H vehicle) and the peak singular value marks the smallest tolerable multiplicative perturbation (Bosworth & Burken TM-113085 pp. 3–6, PV).
- SISO extraction (EI): `T_xu = −L/(1 + L)` ⇒ `L = −T_xu/(1 + T_xu)`; input sensitivity `S = 1 + T_xu = (x + u)/u`. Read gain margin, phase margin, `M_s = max|S|`, and delay margin `DM = PM_rad/ω_c` (EI).
- Margin bounds from `M_s`: `GM ≥ M_s/(M_s − 1)` (SV via Skogestad & Postlethwaite); `PM ≥ 2 arcsin(1/(2M_s))` (UV this session).
- NASA practice: loops opened one at a time with the others closed; broken-loop responses at mixer inputs and sensor outputs simultaneously with orthogonal multisines (Grauer 2021, PV).

---

## 5. Characterising F-15 V2 without touching its aerodynamics

| Characteristic | Procedure | Output data |
|---|---|---|
| Pitch response | from the point-36 trim: multisine on `pitch` (0.1–2 Hz, small amplitude, 3 cycles of the lowest frequency), plus 0.5×/2× repeats; LOES fit of q/q_cmd and n_z/q_cmd | `ω_e, ζ_e, τ_eq, K_q`; per-frequency gain/phase with error bounds |
| Roll response | multisine and steps on `roll`; fit `p_s/p_cmd = K e^{−τs}/(τ_R s+1)` | `τ_R, τ_eff`, steady gain (V2 measured ≈ 0.8 of command at 1 s in its own report) |
| Yaw / sideslip | multisine on `yaw`; fit β/β_cmd (second order) | ω, ζ, delay; Dutch-roll damping (compare with V2 §5.4 design values 0.21–0.31) |
| Dynamic-pressure sensitivity | repeat the three at the trims V2 already evaluated (265, 280, 300.8, 330, 360, 380, 400 ft/s) | parameter vs q̄ tables; check that `q̄`-scheduled gains keep the identified parameters flat (V2 §5.5 table is the design-harness counterpart) |
| Saturation effects | amplitude sweep 0.25×…2× of full command; record the amplitude at which responses stop overlaying; count steps at surface/gameplay envelope limits | describing-function-like gain/phase vs amplitude; saturation onset |
| Turn-energy losses | constant-bank, constant-command turns at several stick levels; log `E_s = h + V²/2g` and `P_s = dE_s/dt` | `P_s(n_z, V)` table — a property of the research model at fixed thrust, not a performance claim |
| Broken-loop margins | validation-only wrapper law (calls the real V2 `Compute`, adds injection to the surface request) | GM, PM, M_s, DM per axis at each trim |

**Data format (MP):** one CSV per run with a header block (`commit`, `aircraft configuration`, `law`, `trim id`, `dt`, `captureFramerate`, `input design: band, harmonics, amplitudes, phases, T`, `SHA-256 of the input file`) and per-step columns `t, inputs, commands, surfaces, p, q, r, p_s, r_s, α, β, φ, θ, ψ, V, Mach, q̄, n_z, h, E_s, limiter flags, clamp flags`. Identification outputs in a sibling JSON: model structure, parameters, standard errors, fit band, R² per frequency.

---

## 6. Guidance gain selection from measurements

| Parameter (dev branch) | Present basis | Measurement-based selection |
|---|---|---|
| `K_θ` (flight-path rate gain, 0.6 /s) | estimate | identify the closed bank-capture loop's `τ_r` (§3, §4.1); choose `K_θ` for the target damping with `ζ ≈ 1/(2√(K_θ τ_r))`; confirm with the outer broken-loop margin and the aim-step overshoot (doc 06 M2) |
| bank time constant (1.0 s ⇒ K_μ = 1 /s) | "no faster than the law can roll" | identify the V2 roll closed loop (`τ_R`, `τ_eff`); keep the bank loop's crossover a measured factor below the roll loop's bandwidth; report the ratio, do not gate on a universal one |
| roll-capture deceleration (Phase 1 `a_p`) | not implemented as such in the dev branch | from identified roll response: `a_p ≈ p_max/τ_R` as an upper bound (EI); verify no overshoot in 90° bank captures |
| command filter `τ = 0.08 s`, rate 4 /s | jitter removal; "no discontinuity" | measure the phase it costs at the outer crossover (`≈ ωτ`); count steps at the rate limit (rate-limit saturation adds lag and PIO risk, Klyde & Mitchell — Phase 1 doc 02 §4) |
| reversal hysteresis 10°, lift-fade 0.05–0.25 g | behavioural | verify by M8/M9 (doc 06) and by sweeping the threshold ± 50 % to show the behaviour is not knife-edge |

**Comparison of selection methods**

| Method | Needs | Strength | Weakness for Maverick |
|---|---|---|---|
| Classical (root locus / loop shaping on identified models) | identified `T_inner`, outer kinematics | transparent; matches Phase 1 rule | needs identification first |
| Step-response tuning | step tests | simple | a single test; easily tuned to one case |
| Frequency-domain (margins, bandwidth) | multisine + loop breaking | quantitative robustness | more tooling |
| Offline LQR | linear model of guidance + law + aircraft | multivariable | weights are tuning in disguise; still needs the identified model |
| Gain scheduling | identified parameters vs q̄ | covers the envelope | only where identification exists |
| MPC (later) | prediction model | constraints | duplicate physics evaluator; heavy |

Recommendation: **identify, then tune classically, then verify with margins.** More sophisticated design does not automatically give better handling; on the F-15 rig the dominant limit is the aircraft's narrow envelope, not the controller structure.

---

## 7. Flight-condition adaptation: who owns what

| Quantity | Belongs to | Reason |
|---|---|---|
| Surface gains vs q̄ (and later Mach) | **control law** | V2 already schedules with `q̄_ref/q̄` (V2 §5.5); the instructor must not schedule surfaces |
| α / n / roll-rate protections | **control law** (F-16 V0.1 has them) | duplicating them in the instructor creates two limiters that can fight |
| Model-validity domain caps (speed floor, α band of the research data) | **rig / instructor** (F-15 only, because V2 has no protections), labelled as model-validity protection | V2 doc §6; dev branch `enableEnvelopeSoftCaps` |
| Command scale (what full stick means) | **law**, exposed to the instructor | Phase 1 descriptor; the dev branch copies the law's full-stick rates through the rig |
| Outer-loop gain vs flight condition | **instructor**, but scheduled on the **measured** closed-loop `τ_r(q̄)`, not on q̄ directly | the plant the instructor sees is the closed inner loop |
| Mass, altitude, Mach effects on response | the aircraft model | the instructor sees them only through the measured response |
| Gameplay tuning (sensitivity, fine-aim) | instructor, labelled | not physics |

---

## 8. Numerical and scheduling stability

| Topic | Test that separates a real instability from a numerical artefact |
|---|---|
| Fixed step | repeat at dt and dt/2 **with the physical latency held constant** (a two-step delay at dt/2), otherwise a stability change may be a genuine latency effect (EI). Report convergence order of the trajectory error |
| One-step latency | model it explicitly in identified loops (`e^{−sT}`); compare the identified `τ_eq` with T + T/2 |
| Controller discretisation | V2's washout is a discrete first-order filter; check its identified corner frequency against its design time constant; the dev branch's exponential filters (`1 − e^{−dt/τ}`) are exact for constant input — verify by a step test |
| Discrete-loop eigenvalues | finite-difference the actual FixedUpdate map around trim: `x_{k+1} = Φx_k + Γu_k`, `u_k = Kx_{k−1}`; spectral radius of `[[Φ, ΓK],[I, 0]]` < 1; compare with `e^{λT}` of the continuous model plus delay (EI) |
| Input sampling | the dev branch's `[RI]` test (25/50/100 fps) already checks aim snapshots; extend to command sequences |
| Repeated polling | `TryGetCommand` called twice in one step must return the same command and advance state once (Phase 1 doc 06 §2[D]) |
| Determinism | byte-identical repeated runs with pinned `captureFramerate` (repository practice) |
| Simulation-rate sensitivity | run the identification at dt = 0.02 and 0.01 (latency held); parameters must agree within their error bounds, otherwise the result is a discretisation artefact |
