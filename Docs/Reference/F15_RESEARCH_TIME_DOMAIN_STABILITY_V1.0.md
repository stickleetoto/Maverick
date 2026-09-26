# F-15 — Research Source Stability in the Time Domain (V1.0, WP-3E Goal A)

> **SOURCE MODEL ONLY.** This integrates the AFIT/Baumann/Davison research model's own time-derivative system in time. That system is WP-3D's `MavF15AfitResearchSourceDynamics`, unchanged; it has no CAS, fixed RHO, 8,300 lbf and a fixed stabilator. It says nothing directly about the real F-15, NASA 836, the production FCS, a Unity Rigidbody or handling qualities. **Nothing was flown:** no Rigidbody, no PlayMode, no Unity time. Nothing was tuned.

**Headline.** Direct RK4 integration of the nonlinear source dynamics reproduces every WP-3D eigenvalue it was asked about. There are 35 cases across the brief's groups A–G:

| What | Result |
|---|---|
| Growth and decay rates | agree with Re(λ) inside the numerical band at amplitude 1e-4; typically 0.001–0.4 %, largest 0.8 % beside fold 194 |
| Frequencies | agree with \|Im(λ)\| by the modal phase (≤ 0.12 %) and, eigen-free, by zero crossings (≤ 0.06 %) |
| **The CMMQ-positive band** | points 47, 5 and 8 at α 13.1–14.1° **grow** at +0.814, +0.461 and +0.115 /s and oscillate at the predicted 0.87–1.36 rad/s. Point 9, past the located Hopf, decays at −0.031 /s. **The printed source equations are themselves unstable there.** |
| **Folds** | at all four, the fold mode decays on one side and grows on the other at its predicted rate. At the limit points it is near-neutral (\|σ\| ≤ 2.3e-7 /s). |
| **Pitchfork** | on the symmetric branch, the symmetry-breaking mode decays below V 377.438 and grows above it. On both turning branches it decays. Perturbed by ±v on the unstable side, the nonlinear model breaks symmetry into opposite turns. **Observed, not assumed:** it then settles on the steady turns φ = ±62.82°, which are WP-3C equilibria at the held stabilator. |
| **Mirror** | a finite perturbation and its mirror give **bit-identical** mirrored nonlinear trajectories. |
| **Suite** | `MavF15ResearchTimeDomainValidation`, 30 / 0. All 16 F-15 suites pass (§9). |

| | |
|---|---|
| Integrator | `Validation/MavValidationRk4Integrator.cs`: fixed-step classical RK4 on x-dot = f(x). Validation-side, deterministic, no Unity time. |
| Harness | `Validation/MavF15ResearchTimeDomainStability.cs`: case catalogue, eigenvector perturbations, modal and eigen-free measurement, symmetry breaking, mirror |
| Tests | `Validation/MavF15ResearchTimeDomainValidation.cs` `[T1]`–`[T14]` (`[T14]` is Goal B: `F15_RESEARCH_STABILITY_CONFLICT_AUDIT_V1.0.md`) |
| Dataset | `Docs/Reference/Data/F15/stability_time_domain/` (Unity export; two separate Unity runs byte-identical). Every number in this document is Unity's. |
| RHS | WP-3D's, byte-identical: SHA-256 of `MavF15AfitResearchSourceDynamics.cs`, and seven re-linearized Jacobians equal to the committed WP-3D ones bit for bit (`[T1]`) |

---

## 1. Method

**RHS.** `MavF15AfitResearchSourceDynamics.EvaluateStateDerivative(x, stabilator)`, unchanged: the state is α, β, p, q, r, θ, φ, V; the stabilator is held at the equilibrium value; the source environment is fixed.

**Integrator.** Classical RK4, fixed dt. Sample times are n·dt, never accumulated. `[T2]`:
- **Order:** fourth order on an exact damped rotation. Halving dt shrinks the error 15.92× and 15.99×.
- **Determinism:** bit-identical on rerun.
- **Isolation:** no Rigidbody, MonoBehaviour, Transform, Unity time, F100, atmosphere model or 6DoF body is named.

**Perturbation** (from WP-3D's eigenvector v, largest component scaled to 1):
- **Real eigenvalue:** d = v.
- **Complex pair:** d = Re(v) = (v + v̄)/2, a real state that excites exactly that pair.
- **Scaling:** δx₀ = a·d/m, with m = maxᵢ \|dᵢ\|/sᵢ and s = (1 rad, 1 rad, 1 rad/s ×3, 1 rad, 1 rad, V₀). **The amplitude a is the largest perturbed component in rad, rad/s or ΔV/V₀.**

**Response — the antisymmetric pair.** δx(t) = (x₊(t) − x₋(t))/2 from x* ± δx₀. It cancels two things:
- the recovered equilibrium's own residual drift;
- every **even-order** nonlinear term. The quadratic term dominates near a fold; the one-sided runs in §6 show why this matters.

What remains is the linear response plus O(a³). **The eigenvalue is never an input to the integration;** it is only the prediction under test.

**Measurement** (the local linear window runs from t = 0 while the modal amplitude stays between max(10⁻² a, 10⁻⁷) and 3·10⁻³):
- **Modal.** η(t) = (u·δx)/(u·v), with u the left eigenvector (the eigenvector of Aᵀ). σ is the least-squares slope of ln\|η\|. ω is the slope of unwrapped arg η.
- **Eigen-free.** One physical state, the largest in d: y(t) = δxⱼ/sⱼ.
  - Real modes: σ is the slope of ln\|y\|.
  - Oscillatory modes: ω = π(n−1)/(t_last − t_first) from n interpolated zero crossings, and σ is the slope of ln\|y\| at interior extrema (parabola-refined).

**Agreement band** (NUMERICAL: a linearization compared with a nonlinear integration of the same equations; not a physical or source tolerance):
- **Rate:** \|σ − Re λ\| ≤ 1 %·\|λ\| + 3u + 10⁻⁷ /s.
- **Frequency:** \|ω − Im λ\| ≤ 0.5 %·\|Im λ\| + 3u.
- **u** is the WP-3D eigenvalue's own measured numerical uncertainty (step spread and scaled check).
- **Budget:** float floor ≤ 0.1 % at a = 10⁻⁴ for \|λ\| ≥ 3·10⁻⁴; residual cubic nonlinearity ≤ 0.8 % beside fold 194; dt < 10⁻⁴ (§3).

## 2. The float floor — why slow modes need larger amplitudes

The transcribed coefficient routine takes and returns **float**. The WP-3D step audit already found that α is quantized at ~1.5·10⁻⁸ rad and coefficients at ~6·10⁻⁸ relative.

**Effect on a perturbation.** The two trajectories see different roundings, which acts like a bias of order 10⁻⁸ /s in x-dot, or a false state offset of ~10⁻⁸/\|λ\|:
- fast modes (\|λ\| ≳ 0.1 /s) are clean down to a = 10⁻⁶;
- fold and pitchfork modes (\|λ\| ~ 10⁻⁵–10⁻³ /s) need a ≥ 10⁻⁴;
- at 10⁻⁶, φ = ±20° reads 45 % low and point 195 87 % off (§5).

**Drift ruled out.** An alternative explanation, the reference state drifting, was tested and **ruled out**. Along the unperturbed trajectory the eigenvalue moves by < 10⁻⁴ relative (e.g. 137: −6.9449 → −6.9441·10⁻⁴; a diagnostic probe run outside Unity, not part of the suite).

## 3. Integrator convergence — dt, dt/2, dt/4 (`[T3]`, amplitude 10⁻⁴)

| case | dt (s) | σ (1/s) | ω (rad/s) |
|---|---|---|---|
| A20-A short-period-like | 0.01 / 0.005 / 0.0025 | −1.41132 / −1.41151 / −1.41143 | 0.99977 / 0.99975 / 0.99976 |
| A20-B phugoid-like | 0.05 / 0.025 / 0.0125 | −5.99262 / −5.99257 / −5.99261 ·10⁻³ | 0.114594 (all) |
| B47-A CMMQ-unstable | 0.01 / … | +0.8139104 / +0.8139105 / +0.8139112 | 0.868976 (all) |
| C117-C 8 rad/s lateral | 0.01 / … | −0.488724 / −0.488727 / −0.488727 | 7.963011 / 7.963019 / 7.963017 |
| D130-E saddle | 0.1 / 0.05 / 0.025 | +1.165253 / +1.165253 / +1.165253 ·10⁻² | — |
| F382.4 pitchfork side | 0.1 / … | +3.6229477·10⁻³ (all) | — |

- **Plateau:** every measured rate and frequency changes by < 10⁻³ of itself across dt → dt/4 (asserted). The largest move is 1.3·10⁻⁴ (A20-A σ); most move by < 10⁻⁵.
- **Trajectory gaps** (≤ 6.3·10⁻⁴ relative) sit at the float floor, not at RK4 truncation: they do not shrink 16× per halving.
- **Chosen steps (NUMERICAL):** **fast 0.01 s, medium (phugoid) 0.05 s, slow (fold, pitchfork) 0.1 s.**

## 4. Growth, decay and frequency at amplitude 10⁻⁴ (`[T4]`–`[T6]`)

| Group | Case (mode) | Predicted λ | Measured σ (modal / eigen-free) | Measured ω (modal / zero crossings) |
|---|---|---|---|---|
| A stable symmetric, pt 20 (α 15.4°) | A short-period-like | −1.41143 ± 0.99974i | −1.41132 / n/a (1 crossing) | 0.99977 / n/a |
| | B phugoid-like | −5.99293·10⁻³ ± 0.114594i | −5.99262·10⁻³ / −5.99264·10⁻³ | 0.114594 / 0.114595 |
| | C Dutch-roll-like | −0.130710 ± 3.65041i | −0.130711 / −0.130713 | 3.65047 / 3.65047 |
| | D roll-subsidence-like | −0.381620 | −0.381620 / −0.381618 | — |
| | E spiral-like | −0.140692 | −0.140691 / −0.140692 | — |
| **B CMMQ band** | pt 47 (α 13.05°) A | **+0.813899 ± 0.867927i** | **+0.813910 / +0.813885** | 0.868976 / (1 crossing; 0.867932 at a = 10⁻⁵) |
| | pt 5 (α 13.82°) A | **+0.461085 ± 1.186868i** | **+0.461088 / +0.461363** | 1.185458 / 1.186154 |
| | pt 8 (α 14.11°) A | **+0.115406 ± 1.356614i** | **+0.115412 / +0.115531** | 1.356204 / 1.355842 |
| | pt 9 (α 14.20°) A, past the Hopf | −0.0312359 ± 1.399409i | −0.0312331 / −0.0312332 | 1.399412 / 1.399413 |
| C stable turning, pt 150 (φ −21°) | A | −0.233050 ± 1.356598i | −0.233053 / −0.233057 | 1.356590 / 1.356592 |
| | B | −8.33334·10⁻³ ± 0.115525i | −8.34975·10⁻³ / −8.34889·10⁻³ | 0.115517 / 0.115507 |
| | C | −0.297830 ± 4.739604i | −0.297861 / −0.297858 | 4.739586 / 4.739589 |
| | D | −0.759467 | −0.759479 / −0.759470 | — |
| | E | −6.01987·10⁻⁴ | −6.01921·10⁻⁴ / −6.01921·10⁻⁴ | — |
| | pt 117 C (7.96 rad/s) | −0.488741 ± 7.962995i | −0.488724 / −0.488726 | 7.963011 / 7.962993 |

**Growth at point 47.** The pitch-mode perturbation grows 29.5× in 4.2 s with a 7.23 s period, i.e. doubling every 0.85 s.

**This is the printed CMMQ in the nonlinear source equations**, not an artifact of linearization. Its provenance is audited in `F15_RESEARCH_STABILITY_CONFLICT_AUDIT_V1.0.md`.

## 5. Amplitude sweep (`[T10]`, relative rate error (σ − Re λ)/\|Re λ\|)

| case | \|λ\| | a = 10⁻⁶ | 10⁻⁵ | 10⁻⁴ | 10⁻³ |
|---|---|---|---|---|---|
| A20-A | 1.73 | −0.03 % | −0.15 % | 0.01 % | 0.00 % |
| A20-B | 0.115 | −6.89 % | 0.03 % | 0.01 % | −0.00 % |
| B47-A | 1.19 | −0.02 % | −0.02 % | 0.00 % | −0.07 % |
| B9-A | 1.40 | 0.98 % | 0.01 % | 0.01 % | −0.06 % |
| C150-E | 6.0·10⁻⁴ | 30.7 % | 0.02 % | 0.01 % | −0.00 % |
| D123-E | 1.1·10⁻³ | 18.4 % | 5.18 % | 0.19 % | 0.33 % |
| D125-E | 5.1·10⁻³ | −0.24 % | 0.04 % | 0.16 % | 0.37 % |
| D193-E | 1.65·10⁻³ | 1.56 % | 0.13 % | 0.82 % | 1.70 % |
| D195-E | 4.9·10⁻³ | 87.2 % | 56.5 % | 0.37 % | 0.01 % |
| G+20 | 5.5·10⁻⁴ | 45.4 % | 0.18 % | 0.01 % | 0.00 % |
| G+2 | 3.2·10⁻⁵ | 16.7 % | −0.27 % | 0.03 % | 0.01 % |

The full table for all 32 non-limit-point cases is in `[T10]` of the Unity report and in `f15_time_domain_measurements.csv`. Its band is 1 %·\|λ\| + 3u + 10⁻⁷, so complex modes are held to \|λ\|, not \|Re λ\|.

**Reading.**
- **Fast modes** converge to Re(λ) as a → 0, down to the float floor at 10⁻⁶.
- **Slow modes** hit the float floor below ~10⁻⁵, with error ∝ 10⁻⁸/(a\|λ\|).
- **Beside fold 194** (D193) the cubic nonlinearity leaves the band already at 10⁻³: the linear regime narrows next to a fold.

**Asserted.** For every mode, a = 10⁻⁴ and at least one neighbouring amplitude both lie inside the band. The comparison is therefore made on an amplitude plateau: the local linear regime.

## 6. Folds (`[T7]`)

| fold | stable / unstable side (measured σ, predicted) | limit point (measured σ, WP-3D zero band) |
|---|---|---|
| 124 | 123 decays −1.110·10⁻³ (−1.112·10⁻³) · 125 **grows** +5.155·10⁻³ (+5.147·10⁻³) | 124: −2.3·10⁻⁷ (1.4·10⁻⁵) |
| 136 | 135 **grows** +2.986·10⁻⁴ (+2.984·10⁻⁴) · 137 decays −6.931·10⁻⁴ (−6.945·10⁻⁴) | 136: −8.6·10⁻⁸ (3.0·10⁻⁵) |
| 182 | 181 decays −3.642·10⁻⁴ (−3.656·10⁻⁴) · 183 **grows** +1.075·10⁻³ (+1.074·10⁻³) | 182: −8.6·10⁻⁸ (3.0·10⁻⁵) |
| 194 | 193 **grows** +1.668·10⁻³ (+1.655·10⁻³) · 195 decays −4.854·10⁻³ (−4.872·10⁻³) | 194 unavailable (print damage) |

- **The saddles WP-3D found are real in the nonlinear dynamics.** The fold-bounded segments 125–135 and 183–193 are genuinely unstable.
- **Limit points:** near-neutral, with rates below 10⁻⁵ /s next to neighbours at ≥ 3·10⁻⁴ /s.
- **136 and 182 give identical numbers** because Table VII prints them as exact mirror images (same stabilator, V, α, θ; β, p, r, φ negated). The folds at 124 and 194 are not mirror-printed.

**One-sided runs.** x₊ or x₋ alone, each against the unperturbed reference, keep the even terms:
- **Beside the folds they bracket the antisymmetric rate**, and the spread grows with a:
  - point 125: +v 4.98·10⁻³, −v 5.31·10⁻³ at a = 3·10⁻⁴ (4.88 / 5.42·10⁻³ at 10⁻³), around the antisymmetric 5.155·10⁻³;
  - point 123: +v −1.12·10⁻³, −v −1.10·10⁻³ (−1.15 / −1.07·10⁻³ at 10⁻³), around −1.110·10⁻³.
- **At the limit points, +v and −v take opposite signs, growing roughly ∝ a:**
  - 124: +v −3.4·10⁻⁵, −v +2.2·10⁻⁵ at 3·10⁻⁴ (−9.8·10⁻⁵ / +1.06·10⁻⁴ at 10⁻³);
  - 136 and 182: +v +5.9·10⁻⁶, −v −3.6·10⁻⁶ (+1.9·10⁻⁵ / −1.6·10⁻⁵ at 10⁻³).
  - This is what a quadratic (even-order) term does at a fold: with η̇ ≈ λη + cη², the ±a runs see λ ± c·a. Which side comes out positive depends only on the eigenvector's arbitrary sign. The antisymmetric pair cancels the c·a term, leaving the near-zero linear rate.

## 7. Pitchfork (`[T8]`)

**Symmetric branch** (WP-3B solves at V, symmetry-breaking lateral real mode):

| V | predicted | measured |
|---|---|---|
| 372.4 | −4.51318·10⁻³ | −4.51262·10⁻³ |
| 377.3 | −1.11510·10⁻⁴ | −1.11020·10⁻⁴ |
| 377.6 | **+1.30538·10⁻⁴** | **+1.31004·10⁻⁴** |
| 382.4 | **+3.62299·10⁻³** | **+3.62295·10⁻³** |

- **The sign changes across V 377.438**, the WP-3D crossing, in the nonlinear dynamics.
- **Near the crossing** the 0.4 % differences are inside WP-3D's own eigenvalue uncertainty there (3u ≈ 3·10⁻⁶ /s; the differences are 5·10⁻⁷ /s). The time-domain measurement is the more precise of the two.

**Turning branches** (WP-3C solves at φ): the same mode decays on both sides.

| φ | predicted | measured |
|---|---|---|
| +20° / −20° | −5.4762·10⁻⁴ / −5.4762·10⁻⁴ | −5.4758·10⁻⁴ / −5.4756·10⁻⁴ |
| +2° | −3.2277·10⁻⁵ | −3.2266·10⁻⁵ |
| −2° | −3.2287·10⁻⁵ | −3.2286·10⁻⁵ |

**Local symmetry breaking** (V 382.4, stabilator −6.1654, critical +3.62·10⁻³ /s; ±10⁻⁴ along v; full nonlinear run, dt 0.05 s, 6,000 s):
- **Departure.** \|φ\| reaches 1° at t = 1,426 s, with opposite signs:
  - **+v:** φ +1.001°, β +1.5·10⁻³°, r +1.4·10⁻³, ψ̇ +1.4·10⁻³ rad/s — a right turn;
  - **−v:** everything mirrored — a left turn.
  - The two runs stay mirror images to 3.2·10⁻⁷ over 6,000 s: the rounding, grown by the instability. The finite-perturbation check in §8 is exact.
- **Observed, not assumed: both settle.** At t = 6,000 s, \|x-dot\| ≈ 10⁻⁸ and the state is φ = ±62.820°, V 621.59 ft/s, α 8.845°, ψ̇ = ±0.1011 rad/s.
  - A WP-3C solve at that φ returns the held stabilator −6.1654° and V 621.59: it is a steady turn on the stable outer turning segment of Table VII (198–199: φ ≈ 63°, V ≈ 626).
  - This goes beyond the required local claim, and is recorded as an observation of this one run.

## 8. Mirror in the nonlinear dynamics (`[T9]`)

**Setup.**
- **States:** turning point 150 and its mirror.
- **Perturbation:** finite and nonlinear — α +0.5°, β +0.3°, p +0.02, q −0.01, r +0.01 rad/s, θ −0.3°, φ +2°, V +5 ft/s — mirrored at the mirror state.
- **Run:** 60 s at dt 0.01.

**Result.** The two trajectories are **mirror images bit for bit at every sample**: the even states α, q, θ, V are equal, and the odd states β, p, r, φ are negated. This check is independent of the eigensolver.

## 9. Tests and scope

`MavF15ResearchTimeDomainValidation`: **30 / 0**.

| Check | Covers |
|---|---|
| `[T1]` | RHS, trim solver and coefficient routines byte-identical to WP-3D; Jacobians bit-identical |
| `[T2]` | RK4 order, determinism, isolation |
| `[T3]` | dt plateau |
| `[T4]` | stable decay |
| `[T5]` | frequencies |
| `[T6]` | CMMQ band |
| `[T7]` | folds and limit points |
| `[T8]` | pitchfork and symmetry breaking |
| `[T9]` | mirror |
| `[T10]` | amplitude plateau |
| `[T11]` | NASA 836 refused; no runtime file names the harness |
| `[T12]` | coefficients, CFX2, CMMQ, atmosphere unchanged |
| `[T13]` | WP-3A/B/C/D datasets byte-identical; trim roots bit-identical |
| `[T14]` | Goal B |

**All F-15 suites:** see `F15_HANDOFF_V1.0.md` for the Unity totals.

**Not done:** Rigidbody or PlayMode flight, continuation, periodic-orbit computation, coefficient or CMMQ changes, density override, actuator limits or rates. The time-domain runs integrate the source equations only.
