# F-15 — Research Turning / Helical Trim (V1.0, WP-3C)

> **Research configuration only** (`F15_AFIT_BAUMANN_DAVISON_MACH06_20K_RESEARCH`). **Not NASA 836. Not flying trim.** No Rigidbody, no continuation, no stability analysis here — the stability of these equilibria is WP-3D, `F15_RESEARCH_STABILITY_ANALYSIS_V1.0.md`.

**Headline:** with only its printed bank angle φ fixed, every one of Baumann Table VII's **80** non-symmetric turning equilibria is recovered from the source equations.
- **Convergence:** 320 / 320 solves converge (80 states × 4 perturbations), in 3–7 iterations.
- **Accuracy:** all eight recovered unknowns lie inside the Table VII print-resolution floor in 69 states. The other 11 lie inside it once the solver's own termination uncertainty is added. **None** exceeds both.
- **Mirror symmetry:** holds exactly.
- **Other roots:** the wide start grid found no second root at any probed φ.
- **CFX2:** it cannot affect any recovered turning state.

| | |
|---|---|
| Solver | `F15/MavF15AfitResearchTrimSolver.cs`: `SolveTurning`, `EvaluateTurningResidual`, `TurningSearchBounds` (same WP-3B class and source environment) |
| Round-trip harness | `Validation/MavF15TableViiTurningRecovery.cs` — validation only; the one place the published answer is read |
| Tests | `Validation/MavF15ResearchTurningTrimValidation.cs` `[H1]`–`[H19]`, 31 / 0 |
| Source | Baumann, DTIC ADA217366: FUNX (PDF pp.95–96), Table VII (PDF pp.124–133). Davison, DTIC ADA256613: drivers (PDF pp.92–93, 125) |

---

## 1. Source semantics — unchanged from WP-3B

The source semantics are the same as WP-3B:
- **Environment:** fixed RHO = 0.0012673 slug/ft³, G = 32.174 ft/s², q = ½·RHO·V².
- **Thrust:** 8,300 lbf total plus the +0.25-in thrust-line moment.
- **Mass and inertia:** 37,000 lb; Ix 25,480, Iy 166,620, Iz 186,930, Ixz −1,000 slug·ft².
- **Coefficients:** the Baumann/Davison routine at the source's p̂ = pb/2V, q̂ = qc̄/2V, r̂ = rb/2V.
- **Controls:** aileron, rudder and differential tail at 0, as Table VII's caption states. The only solved control is the symmetric stabilator, entering through `MavF15ResearchStaticControlState`.
- **Not used:** throttle, engines, ISA density.

**No shared code changed:** `MavSteadyFlightTrimSolver`, `MavAtmosphereModel`, `MavSixDoFBody`, `MavF100*`, NASA 836 data, and every Baumann coefficient (CFX2 included) are untouched.

## 2. Formulation

**Fixed:** φ, the printed bank angle.

**Unknowns (8):** α, β, p, q, r, θ, V, symmetric stabilator.

**Residuals (8)**, written independently of any stored answer:

| # | Equation | Source |
|---|---|---|
| 1–3 | X/m − g sin θ + r v − q w · Y/m + g cos θ sin φ + p w − r u · Z/m + g cos θ cos φ + q u − p v (each / g) | F(1), F(2), F(8) |
| 4 | L + (Iy − Iz) q r + Ixz p q (/ q̄Sb) | F(3) |
| 5 | M + T·arm + (Iz − Ix) p r + Ixz (r² − p²) (/ q̄Sc̄) | F(4) |
| 6 | N + (Ix − Iy) p q − Ixz q r (/ q̄Sb) | F(5) |
| 7 | θ̇ = q cos φ − r sin φ | F(6) |
| 8 | φ̇ = p + (q sin φ + r cos φ) tan θ | F(7) |

Here u, v, w = V (cos α cos β, sin β, sin α cos β).

`[H5]` checks the residuals against the independent WP-3A evaluator: all eight agree **exactly** (largest difference 0) at all 81 printed turning states.

**Outputs, never driven:**
- **ψ̇** = (q sin φ + r cos φ)/cos θ. These are steady **helical** turns: heading keeps changing, and the source has no altitude state.
- **γ**, from sin γ = cos α cos β sin θ − (sin β sin φ + sin α cos β cos φ) cos θ.
- **Turn direction** = sign ψ̇ (ψ̇ > 0 is a right turn).
- **Not reported:** the helix radius V cos γ/|ψ̇| is kinematically determined, but it is not reported or claimed as authority. Load factor is not reported.

**Why φ is the parameter:**
- Above the pitchfork, a fixed V has **three** equilibria: the symmetric one and a left/right mirror pair.
- At φ = 0 the whole symmetric branch satisfies the equations, so the Jacobian is singular there.
- A nonzero φ picks one side of the pair, and V becomes an unknown instead of the branch selector.
- **It is a recovery parameterization, not a proof of uniqueness** (§8).
- `SolveTurning` refuses φ = 0 exactly and points to `SolveSymmetric`.

## 3. Dataset — WP-3A's pairing, the pitchfork found from the data

**Pairing:** WP-3A's displaced-column assembly, reused unchanged. In the turning segment, r, θ, φ and V belong to the state printed two rows above. Nothing is re-transcribed or rearranged. **81** assembled turning-section states.

**Unavailable — source print damage.** Neither is repaired from symmetry:
- first-half point 175 (β illegible);
- point 194 (its displaced r, at second-half 196, illegible).

**The pitchfork is found from the data, not named.**
- The printed φ changes sign **exactly once** along the branch: between 164 (−0.684°) and 165 (+1.0e-6°).
- Point 165 is effectively symmetric: φ 1.0e-6°, β 1.6e-9°, r 1.4e-9 rad/s. Every other turning state has |φ| ≥ 0.684°, a ratio of 6.8e5.
- **Classified PITCHFORK / SYMMETRIC BRANCH POINT.** It is solved with the WP-3B symmetric solver (`[H2]`): α +2.2e-3° (floor 3.0e-3), stabilator −2.5e-3° (3.4e-3), θ +3.8e-3° (5.6e-3).
- It is never fed to the turning solve.

**That leaves exactly 80 non-symmetric turning states.**

## 4. Search bounds — source audit first

**What the sources bound:**
- **Baumann 1989:** AUTO's parameter and norm bounds (RL0/RL1, A0/A1) are read from a data file whose values are not printed (PDF p.98).
- **Davison's bifurcation driver:** −8 ≤ α ≤ 50° and |β| ≤ 30° are advisory only. The code writes "CONTINUE TO RUN" and does not clamp (PDF pp.92–93). The simulator has the same check commented out (p.125).
- **Nothing** bounds p, q, r, θ, φ or V.

| Unknown | Box | Kind |
|---|---|---|
| α | −4…90° | source-semantic: the transcribed routine's breakpoint span (Davison's advisory is recorded, not adopted) |
| β | ±20° | source-semantic: the transcribed breakpoint span |
| p, q, r | ±1 rad/s | **NUMERICAL_SEARCH_BOUND** — more than 10× the largest printed rate |
| θ | ±89° | **NUMERICAL_SEARCH_BOUND** — Euler kinematics are singular at ±90° |
| V | 218.5–699.7 ft/s | source-semantic: source-exercised span |
| stabilator | −25…−5° | source-semantic: ResearchDemonstratedControlRange, **not a hard stop** |

- **None is a physical limit** (`IsPhysicalLimit` is false).
- **Nothing clipped:** no bound comes near any printed turning state. The closest is 3.6% of the box: point 117's stabilator, −5.717° against the −5° edge.
- **No root on a bound:** no recovered root sits on one (`[H13]`).

## 5. Round trip — 80 states × 4 perturbations

**Method:**
1. Take the published state and fix **only** its printed φ.
2. Perturb all eight unknowns deterministically (mixed signs): up to 1° α, 0.3° β, 0.003 rad/s p, 0.02 rad/s q and r, 3° θ, 20 ft/s V and 0.5° stabilator. Every unknown moves in every start (`[H8]`).
3. Solve.

**Where the published row is used:** only to build the start, and afterwards for error reporting, print analysis and branch labelling.

**Start independence:** a generic start that knows nothing of the table reaches the same root at 80 of 80 states: α 10, β 0, p 0, q 0.05, r 0.03·sign φ, θ 10, V 450, stabilator −10. Largest spread: 0.21 root-identity units.

**Two separate uncertainty measures.** Neither is a validation threshold.
- **Print floor** (source): |dx/dφ| · ½ unit in φ's last printed digit + ½ unit in x's own last digit. α and the stabilator are printed to 7 digits, everything else to 4.
- **Numerical uncertainty** (solver): |J⁻¹ r| at the root, i.e. the step one more Newton iteration would take. `NumericalSolverTolerance` (1e-6) is unchanged from WP-3B.

**Results.** 320 / 320 converged. Iterations: min 3, mean 4.25, max 7.

| Unknown | max \|recovered − printed\| | mean | max diff / floor | states above floor | max numerical uncertainty |
|---|---|---|---|---|---|
| α | 1.46e-3° | 1.30e-4° | 3.67 | 5 | 2.4e-5° |
| β | 1.24e-5° | 3.39e-6° | 0.90 | 0 | 8.1e-8° |
| p | 5.20e-6 rad/s | 1.49e-6 | 0.95 | 0 | 1.7e-8 |
| q | 1.44e-5 rad/s | 4.00e-6 | 0.91 | 0 | 5.0e-8 |
| r | 1.06e-5 rad/s | 3.17e-6 | 0.96 | 0 | 4.6e-8 |
| θ | 5.66e-3° | 1.91e-3° | 0.97 | 0 | 1.2e-5° |
| V | 0.123 ft/s | 0.029 ft/s | 0.89 | 0 | 6.2e-4 ft/s |
| stabilator | 1.40e-3° | 5.70e-5° | 15.17 | 11 | 4.0e-5° |

**Classification:**
- **69** states are within the print floor.
- **11** are within the print floor plus numerical uncertainty: points 138, 140–144, 160, 164 and 169–171.
- **0** are beyond both.
- **No failures** to classify (no transcription, pairing, conditioning, search-bound or additional-root case).

**Why those 11.**
- They sit where φ barely moves α and the stabilator: on the flat stretch just past the stabilator's local maximum at point 136 (points 138–144), and next to the pitchfork (160, 164, 169–171).
- There the floor collapses to the 7-digit print half-unit, about 5e-7°.
- The solver's own termination uncertainty (≤ 4e-5°) then dominates.
- That is numerical, not a model difference. Every one closes once it is counted.

**Independent WP-3A evaluator at the recovered states:**

| Residual | Max \|res\| |
|---|---|
| X/W | 9.2e-7 |
| Y/W | 2.7e-7 |
| Z/W | 9.9e-7 |
| L/q̄Sb | 6.1e-10 |
| M/q̄Sc̄ | 2.7e-7 |
| N/q̄Sb | 1.0e-8 |
| θ̇ | 3.0e-9 rad/s |
| φ̇ | 2.1e-8 rad/s |

**Per state:** the suite report (`[H9]`) prints φ, printed and recovered values of all eight unknowns, ψ̇, γ, residual norm, iterations, each difference with its floor and numerical uncertainty, and the classification.

## 6. Heading rate, flight path, turn direction

| Output | Range |
|---|---|
| ψ̇ | **−0.1027 … +0.1014 rad/s** (−5.88 … +5.81 °/s). Smallest \|ψ̇\| is 9.9e-4 rad/s, at φ −0.684°: more than 100× the epsilon every driven residual sits inside, so it is not driven. |
| γ | −2.93 … +4.58°: gentle helical descents at high bank, climbs near the pitchfork |
| Turn direction | = sign φ in 320 of 320 |

## 7. Mirror symmetry — derived, then checked

**Derivation.** Take the mirror image: (β, p, r, φ) → −(β, p, r, φ), with α, q, θ, V and the stabilator kept.
- **Kinematic and inertial terms:**
  - even (unchanged) in X, Z, M and θ̇: −W sin θ, r v, q w, W cos θ cos φ, q u, p v, (Iz − Ix) p r, Ixz (r² − p²);
  - odd (sign flips) in Y, L, N and φ̇.
- **So the equations are mirror-symmetric if and only if** the longitudinal coefficients do not depend on β, p or r, and the lateral ones are odd in them.
- **Checked:**
  - the longitudinal routine takes only α, stabilator and q̂;
  - CY, Cl and Cn are exactly odd at every turning state (defect 0). The one-sided high-α asymmetric terms are inactive at α ≤ 10.6°.
- **Therefore:**
  - **EVEN:** α, q, θ, V, stabilator;
  - **ODD:** β, p, r, φ, ψ̇.

**Check** (`[H12]`), solving at −φ for all 80 states:
- **From the mirrored start:** the exact mirror image, parity defect 0, in 80 of 80.
- **From the unmirrored +φ start** (the solver must cross to the other side alone): the mirror root in 80 of 80. Parity defects are at the numerical-termination scale: α 8.9e-5°, θ 8.7e-5°, V 2.2e-3 ft/s, stabilator 6.9e-5°, β 3.6e-7°, p/q/r ≤ 3.4e-7 rad/s.

**The source model breaks no symmetry.**

## 8. Multiple roots (`[H16]`)

**Grid:** 288 deterministic starts per φ (4 α × 4 V × 3 stabilator × 3 θ × 2 signs of r), at 8 printed bank angles: −64.52, −49.81, −25.37, −7.968, +8.74, +36.15, +52.09 and +63.02°.

**Converged roots:**
- **One distinct root per φ**, and it is the published state every time. The distance from it is 0.8–10.5 root-identity units (root identity is 1e-3°, 1e-5 rad/s and 1e-2 ft/s, all numerical).
- **Branch sign** always matches the side: LEFT for φ < 0, RIGHT for φ > 0.
- **Converged starts:** 84–116 per φ.

**Non-converged starts:**
- 125–200 per φ stopped at a source-semantic bound, mostly the demonstrated stabilator edge or the V span.
- Up to 56 per φ were "other". The closest were restarted once from where they stopped:
  - **Slow Newton convergence toward the same root.** At φ −64.52, −49.81 and −25.37 the restart reaches the root. At +8.74 it is still approaching: norm 2.1e-4, 14 root-identity units away.
  - **Unresolved stalls**, mostly at α 15–41° with the stabilator at −16…−25°, often near the demonstrated-range edge. Residual norms are ≥ 7.8e-3, so these are **not roots**. They may mark equilibria needing more stabilator than the demonstrated range, or nothing; this pass does not decide.

**No branch ambiguity found. No uniqueness claimed:** a finite grid proves none.

## 9. Approaching the pitchfork (`[H17]`, characterization only)

**Setup:** φ = ±1, 0.5, 0.2 … 1e-6°, each solved from the same fixed seed: the printed state nearest the pitchfork on that side (164 or 166). Solver settings are unchanged, and there is no continuation.

**Convergence never fails:** every solve converges in 1–2 iterations. The smallest |φ| tested and converged is **1e-6°**.

**Conditioning collapses.** The numerical uncertainty |J⁻¹r| grows as |φ| shrinks:

| \|φ\| | u(α) | u(V) | u(stabilator) |
|---|---|---|---|
| 0.2° | ~1e-6° | ~1e-5 ft/s | ~1e-6° |
| 0.01° | ~5e-5° | ~1e-3 ft/s | ~5e-5° |
| ≤ 1e-4° | ~1e-3° | ~0.02 ft/s | ~1e-3° |

- **What drifts:** the even variables slide along the symmetric-branch direction, which is the near-null direction of the Jacobian.
- **What stays exact:** β, p and r scale exactly linearly with φ.
- **Best-conditioned stretch: |φ| = 0.05–0.2°** (u ≤ 1.4e-5°). There the solutions run V 377.438–377.442 ft/s, α 10.5863–10.5865° and stabilator −6.4936…−6.4938°, trending as φ → 0 toward Table VII's printed pitchfork, point 165: V 377.4, α 10.58656°, stabilator −6.493874°.
  - The turning branch pins the fork more finely than the 4-digit printed V does.

## 10. CFX2

- **Unchanged:** the production constant is still 0.09833517 (Davison App. C). `[H18]` re-checks it at 40° after every solve, and the routine is bit-identical before and after.
- **Sensitivity at every recovered turning equilibrium: zero.**
  - The highest recovered α is 10.586°.
  - The routine returns the low-AoA drag fit alone below 20°, so CFX2 carries no weight.
  - Neither printing (0.09833517 or Baumann's 0.09833617) can change these states, and they cannot discriminate between the two.
- **No alternate evaluation was needed.**
- **The only higher-α points found** (§8, α 15–41°) are non-converged stalls, not equilibria.

## 11. Tests — `MavF15ResearchTurningTrimValidation`, 31 / 0

| | |
|---|---|
| `[H1]` | 81 assembled; 175 and 194 unavailable (print damage); one sign change, at 165; 80 non-symmetric |
| `[H2]` | 165 classified as pitchfork and solved by WP-3B; turning solve refuses φ = 0 |
| `[H3]` | refused under the exact 836 id, an empty id, and non-finite φ; no other source names the trim |
| `[H4]` | q = 0.5·0.0012673·V² at every recovered state; no atmosphere model named |
| `[H5]` | 8,300 lbf and moment at all 320; residuals identical to the WP-3A evaluator at all 81 printed states |
| `[H6]` | returned φ is the printed φ, bit for bit; no φ among the unknowns |
| `[H7]` | signatures take no answer; no Table VII in the solver's code; generic start reaches the same root at 80 / 80 |
| `[H8]` | every unknown displaced in every start |
| `[H9]` | 320 attempts, none refused, all converged; per-state report and classification |
| `[H10]` | all eight residuals within the epsilon at every root; independent evaluator agrees |
| `[H11]` | ψ̇ nonzero at every root; range, γ, turn direction reported |
| `[H12]` | parity derived; routine odd; mirrored start exact; unmirrored start reaches the mirror root in 80 / 80 |
| `[H13]` | bounds labelled; none physical; none clips a printed state or holds a root |
| `[H14]` | no actuator authority: no limit or surface-state member; actuator and profile still at zero travel |
| `[H15]` | bit-identical reruns |
| `[H16]` | multiple-root grid; every preserved root is an equilibrium |
| `[H17]` | near-pitchfork probe reported, both sides |
| `[H18]` | CFX2 constant untouched; zero sensitivity at every recovered root |
| `[H19]` | shared atmosphere bit-identical |

**Asserted tolerances:** numerical only — the termination epsilon, root identity, and 1e-12 implementation identity.

## 12. What this justifies next

- **Pseudo-arclength continuation — justified as a later WP, not needed to recover printed states.**
  - φ-parameterized Newton recovered all 80 printed states, and even solves at |φ| = 1e-6°.
  - But it cannot pass through φ = 0, and its conditioning collapses there (§9).
  - Reproducing the source's own diagram needs arclength (AUTO's method): continuation in the stabilator, which folds at every stabilator extremum along the printed branch (points 124, 136, 182, 193, and the fork at 165). Crossing between the symmetric and turning branches also needs it.
  - Arclength is also the tool to settle §8's unresolved high-α stalls.
- **WP-3D stability analysis — ✅ DONE** (`F15_RESEARCH_STABILITY_ANALYSIS_V1.0.md`).
  - The source's own FUNX is linearized at all 170 equilibria (not the trim residual): **127 stable, 40 unstable, 3 near-neutral.**
  - **The turning branch** is a saddle between the printed stabilator extrema, 125–135 and 183–193. The extrema 124/136/182/194 are real-eigenvalue zeros, located by φ-bisection to within 4e-7° of the printed stabilator.
  - **Point 165** is a supercritical pitchfork: a lateral real eigenvalue changes sign on the symmetric branch, not on the turning branches.
  - **Mirror spectra** are identical.
  - **Disagreements with Table VII's "stable" caption:** the fold saddles, and 18 symmetric rows at α 13.0–14.1°, where the source's printed CMMQ is positive.
- **WP-3E nonlinear time-domain check — ✅ DONE** (`F15_RESEARCH_TIME_DOMAIN_STABILITY_V1.0.md`).
  - **Folds:** these turning equilibria really are saddles between the printed folds. At all four folds the fold mode grows on one side and decays on the other, at the predicted rates.
  - **Pitchfork:** perturbed on the unstable symmetric side, the nonlinear model departs into mirror-image turns and settles on WP-3C turning equilibria (φ = ±62.82°).

## 13. Not done

- **Not built:** Rigidbody flight, PlayMode, continuation, the density override (D11). Eigenvalues: later, in WP-3D.
- **Not changed:** Baumann coefficients, mass, thrust, source density or gravity, F100, NASA 836, `MavSteadyFlightTrimSolver`, `MavAtmosphereModel`, `MavSixDoFBody`.
- **No authority invented:** no hard stops, no rates, no surface travel.
- **Untouched:** scenes and prefabs. F15Replacement is not enabled.
