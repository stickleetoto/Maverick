# F-15 — Research Source Stability / Eigenvalue Reproduction (V1.0, WP-3D)

> **SOURCE MODEL STABILITY ONLY.** This is the local stability of the AFIT/Baumann/Davison **research model** (`F15_AFIT_BAUMANN_DAVISON_MACH06_20K_RESEARCH`) at its published equilibria. It says **nothing** directly about the real F-15, NASA 836, the production flight-control system, a Unity Rigidbody or handling qualities. The source model has **no CAS** (Baumann PDF pp.33–34: "the CAS was not implemented in this model"), so these are open-loop eigenvalues of that model. Nothing was flown, nothing was continued, nothing was tuned.

**Headline.** Stability comes from the source's own time-derivative system x-dot = f(x, stabilator), Baumann's `FUNX` F(1)..F(8), never from the trim residual. It is linearized at all **170** Table VII equilibria recovered in WP-3B/C.

- **The source RHS is reproduced exactly.** It equals an independent Newton–Euler derivation to 4.8e-15. All 11 printed K constants reproduce. x-dot is inside the solver-termination bound at all 170 recovered states, and inside the print floor at all 170 printed rows.
- **Computed classification:** **127 STABLE, 40 UNSTABLE, 3 NEAR_NEUTRAL.** The source labels all 170 stable. Every disagreement has one of two located causes:
  - **22 turning states lie between printed stabilator extrema.** One real eigenvalue there is > 0: a saddle, structurally forced by the folds.
  - **18 symmetric states have α 13.0–14.1°.** A longitudinal pair there has Re up to +0.81 /s. The cause is the source's own printed pitch-damping fit, CMMQ, which is positive for α ≈ 10.6–14.3°.
- **The source's special points are reproduced as eigenvalue zeros:**
  - **Folds:** the 4 printed stabilator extrema (124/136/182/194) each carry a real eigenvalue zero. There the stabilator matches the printed value to ≤ 4e-7°.
  - **Pitchfork:** at 165, a lateral real eigenvalue changes sign on the symmetric branch and not on the turning branches, so the pitchfork is **supercritical**. It is located 4.6e-5° in stabilator from the printed 165.
- **Baumann's stated Hopf at stabilator −19.73° is found at −19.33°** (α 20.8°), as a lateral oscillatory pair. That is the wing-rock type Davison reports at α 20°.
- **Baumann's own Figure C-7 line styles:**
  - they **agree** on the symmetric branch's instability beside the fork and beyond −20°;
  - they **disagree** on the α 11–14° longitudinal pair and on the fold saddles.
- **Mirror spectra:** identical. A(mirror) = S·A·S to 2.5e-14.

> **WP-3E verified these eigenvalues in the nonlinear source dynamics** (`F15_RESEARCH_TIME_DOMAIN_STABILITY_V1.0.md`), by RK4 integration of this unchanged RHS over 35 cases.
> - **Agreement:** rates and frequencies match within 1 % (typically 0.001–0.4 %).
> - **The CMMQ band** grows at the predicted +0.81 /s.
> - **The fold saddles** grow and their neighbours decay.
> - **Pitchfork:** it breaks symmetry into opposite turns.
>
> The source conflict is audited in `F15_RESEARCH_STABILITY_CONFLICT_AUDIT_V1.0.md`: **PUBLIC PRINTINGS AGREE — EXECUTED AUTO MODEL MAY DIFFER.**

| | |
|---|---|
| Source RHS | `F15/MavF15AfitResearchSourceDynamics.cs`: `EvaluateStateDerivative`, `KConstants`. Pure and research-only; nothing outside Validation/ calls it. |
| Eigensolver | `Validation/MavValidationEigenSolver.cs`: balancing, Householder Hessenberg, complex shifted QR, inverse iteration. Validation-side only. |
| Analysis | `Validation/MavF15ResearchStabilityAnalysis.cs`: equilibria, Jacobians, step audit, classification, tracking, crossings, export. |
| Tests | `Validation/MavF15ResearchStabilityValidation.cs` `[S1]`–`[S19]`, **40 / 0** (Unity 6000.3.16f1) |
| Dataset | `Docs/Reference/Data/F15/stability/` (5 CSVs, README, extraction and cross-check scripts) |
| Sources | Baumann, DTIC ADA217366: driver/FUNX (PDF pp.91–96), AUTO (pp.49–58), App. C text/figures (pp.118–123), Table VII (pp.124–133). Davison, DTIC ADA256613: theory (pp.18–26), results (p.42), App. B driver / K constants / FUNX (pp.92–97), COEFF (pp.108, 116), App. C (p.147) |

---

## 1. Source audit — what the theses say

**State and equations.**

| Item | Source (Baumann D2ICCV28, PDF pp.91–97; Davison App. B identical) |
|---|---|
| State order | U(1..8) = α (deg), β (deg), p, q, r (rad/s), θ (deg), φ (deg), V/1000 (ft/s). ψ is not a state: eq. 3.17 is "eliminated since equations 3.9 to 3.16 do not depend on ψ" (p.46). |
| Parameter | PAR(1) = symmetric stabilator (deg); rudder, aileron are the other parameters (0 here). |
| Forces | Wind-axis form (below). CX includes THRUST/QBARS, and CMM includes THRUST·(0.25/12)/(QBARS·CWING). |
| ṗ, ṙ | The Ixz coupling is already **inverted into K constants**: F(3) = −K12·QR + K13·PQ + K14(CLM + K7·CNM)V², F(5) = K15·PQ − K16·QR + K17·V²(K5·CLM + CNM). |
| Gravity | G = 32.174 through G·sinθ/V, G·cosθ·sinφ/V, G·cosθ·cosφ/V. |
| Kinematics | F(6) = Q cosφ − R sinφ; F(7) = P + Q tanθ sinφ + R tanθ cosφ. |
| Rate normalization | PB = P·BWING/2V, QB = Q·CWING/2V, RB = R·BWING/2V (Davison COEFF, PDF p.108). |
| Units | F(1), F(2), F(6), F(7) × DEGRAD (degrees); F(8) = U(8)·[…], i.e. d(V/1000)/dt. |

**K constants.**
- **Formulas, read on the page image (p.92):** K12 = (K2+K3)/(1−K3), **K13 = (1−K4)·K5/(1−K3)** (the text layer garbles this), K14 = K6/(1−K3), K15 = (K3−K4)/(1−K3), K16 = (1+K2)·K7/(1−K3), K17 = K11/(1−K3).
- **The code uses 11 hard-coded printed values,** with "K16 = K13".

**What "stable" means in the source.**
- **Definition.** Stability is the sign of the Jacobian's eigenvalues:
  - *"the stability of equilibria changes when one or more eigenvalues of the Jacobian crosses from the Left Half Plane (−) to the Right Half Plane (+)"* (Baumann p.32);
  - AUTO's Fort.9 *"provided the eigenvalues of the Jacobian matrix which were used as the primary indication whether the equilibrium solutions were stable or not"* (p.54);
  - AUTO detects Hopf points *"by monitoring the number of eigenvalues in the left-half plane"* (p.57);
  - stable branches are drawn *"heavy lines"*, unstable ones dashed (p.25, App. C p.118);
  - Davison adds that the system is *"considered stable if the eigenvalues reside in the left-half plane; i.e. if they are negative or zero"* (p.19), and that a limit point is *"a real eigenvalue [moving] across the imaginary axis, … a change in stability. At least one portion of the branch departing the limit point is unstable"* (p.21).
- **How AUTO's Jacobian was built:** central differences, F(x+dx) − F(x−dx) over 2dx (eq. 4.1, p.49). The step is DX0 = 1e-9 scaled per state (×50, 10, 0.5, 0.25, 0.5, 50, 50, 0.5 in source units; p.93), in double precision.
- **Eigenvalues are never printed.** Neither thesis prints an eigenvalue, an unstable-root count, or a per-row stability flag.
- **What Table VII carries:** only its caption, *"a compilation of stable low α equilibrium states"* (p.124).
- **Other stability evidence in the source:**
  - App. C text: *"The branch loses its stability at −19.73° through a Hopf bifurcation and is unstable for δe < −19.73°"*;
  - Baumann p.60: a Hopf *"at 19° angle of attack"*;
  - Davison p.42: 12-state model, CAS off, *"At 20 degrees (30 units) AOA, a Hopf bifurcation point was detected, signalling the onset of wing rock"*;
  - the solid/dashed line styles of Figures C-1..C-8.
- **Davison's extra states.** His App. B model (PDF p.97) adds actuator states F(9..12) = 20·(cmd − x), 28·(…). They are driven by constant commands and feed the airframe, never the reverse, so they only append eigenvalues −20, −28, −20, −20 and move none of the eight airframe eigenvalues.

## 2. The RHS — `MavF15AfitResearchSourceDynamics.EvaluateStateDerivative`

**Inputs and outputs.**
- **State:** x = [α, β, p, q, r, θ, φ, V] in rad, rad, rad/s ×3, rad, rad, ft/s. This is FUNX's order, with a constant diagonal rescaling of its degree / kft·s⁻¹ units, so no eigenvalue changes.
- **Stabilator:** held fixed (degrees), entering through a `MavF15ResearchStaticControlState` inside the demonstrated range. Aileron, rudder and differential tail are 0.

**The equations,** with ax = K1·V·CX − G·sinθ/V, ay = K1·V·CY + G·cosθ·sinφ/V, az = K1·V·CZ + G·cosθ·cosφ/V:

```
α̇ = Q + (−(ax + R sinβ) sinα + (az − P sinβ) cosα) / cosβ
β̇ = −(ax sinβ + R) cosα + ay cosβ − (az sinβ − P) sinα
ṗ = −K12 Q R + K13 P Q + K14 (CLM + K7 CNM) V²
q̇ =  K8 V² CMM + K9 P R + K10 (R² − P²)
ṙ =  K15 P Q − K16 Q R + K17 V² (K5 CLM + CNM)
θ̇ = Q cosφ − R sinφ
φ̇ = P + Q tanθ sinφ + R tanθ cosφ
V̇ = V (ax cosα cosβ + ay sinβ + az sinα cosβ)
```

- **Inputs:** CX = cx + T/(q̄S) and CMM = cm + T·(0.25/12)/(q̄S·c̄), each thrust term once.
- **Constants:** q̄ = ½·0.0012673·V²; K1..K17 are derived from RHO, S 608, b 42.8, c̄ 15.94, 37,000/32.174 slug, Ix 25,480, Iy 166,620, Iz 186,930, Ixz −1,000.
- **Excluded:** no atmosphere model, throttle, F100, Rigidbody or UnityEngine.

## 3. RHS verification (`[S1]`–`[S5]`)

**Source constants and the inertia inversion.**

| Check | Result |
|---|---|
| Printed K constants | all 11 reproduce; largest gap K1 4.9e-8 relative (the printing), others ≤ 2e-10; K16 − K13 = 1.8e-16 |
| K constants vs the standard inverted Newton–Euler moment equations (Γ = IxIz − Ixz²) | K12 = (Iz(Iz−Iy)+Ixz²)/Γ, K13 = K16 = Ixz(Ix−Iy+Iz)/Γ, K15 = (Ix(Ix−Iy)+Ixz²)/Γ …: gap 1.1e-16 |

**The RHS against an independent derivation** (WP-3A body-axis evaluator: u̇, v̇, ẇ; then V̇, α̇, β̇; p/r by 2×2 inverse; q̇ = M/Iy).

| Where | Largest \|FUNX − independent\| / (1 + \|ẋ\|) |
|---|---|
| 170 recovered equilibria + 170 printed rows + 36 off-equilibrium probes | **4.8e-15** |
| ṗ, ṙ at 36 probes with p, q, r ≠ 0 | 3.3e-16 |

**ẋ at the 170 equilibria.**

| ẋ | Recovered: max \|ẋ\| | ÷ solver-termination bound | Printed row: max \|ẋ\| | ÷ print floor |
|---|---|---|---|---|
| α̇ rad/s | 1.9e-8 | 0.24 | 3.3e-5 | 0.95 |
| β̇ rad/s | 3.9e-9 | 0.07 | 1.2e-5 | 0.85 |
| ṗ rad/s² | 1.1e-8 | < 0.001 | 1.8e-5 | 0.77 |
| q̇ rad/s² | 1.2e-7 | 0.02 | 1.1e-5 | 0.92 |
| ṙ rad/s² | 3.3e-9 | < 0.001 | 4.1e-6 | 0.68 |
| θ̇ rad/s | 2.8e-9 | 0.003 | 1.1e-5 | 0.73 |
| φ̇ rad/s | 1.4e-9 | 0.001 | 7.5e-6 | 0.82 |
| V̇ ft/s² | 1.9e-6 | 0.05 | 5.2e-3 | 0.91 |

- **Bound:** the ẋ that NumericalSolverTolerance (1e-6 on each normalized trim residual) allows.
- **Floor:** Σ\|∂f/∂field\|·½ unit in each printed field's last digit.
- **Numerically zero:** 446 printed-row components where ẋ and floor are both < 1e-15 (the symmetric rows' lateral states and q, printed as 1e-19…1e-31 continuation noise) are excluded, as in WP-3A.
- **The equilibria are equilibria of the source dynamics, not only of the trim equations.**

## 4. Jacobian and finite-difference step audit (`[S7]`, `[S8]`, `[S11]`)

**Method.**
- **A = ∂f/∂x** by central differences in physical coordinates, stabilator fixed.
- **Float snapping:** for α and β, the two states the float coefficient routine takes directly, the perturbed values are snapped to float and the quotient uses the true spacing.
- **Deterministic:** bit-identical on repeat at all 170.

**Structure checks.**
- **Parity:** at all 90 symmetric states the longitudinal (α, q, θ, V) and lateral (β, p, r, φ) blocks decouple **exactly** (every cross entry 0.0).
- **Units:** the kinematic rows match their closed forms to 3.4e-12 (∂θ̇/∂q = cosφ, ∂φ̇/∂p = 1, ∂φ̇/∂θ = (q sinφ + r cosφ)/cos²θ, ∂α̇/∂q = 1). A degree slip would show as 57.3.

**Per-column audit** (each column's step varied alone; relative column change between successive steps).

| State | Plateau | Chosen (NUMERICAL) | Why |
|---|---|---|---|
| α | 3e-4…1e-3 rad | **5e-4 rad** | truncation above, float-coefficient noise (~6e-8·\|C\|/h) below; ~1e-5 relative |
| β | ≤ 3e-6 rad (turning); first order at β = 0 | **1e-6 rad** | CL, CN = fit(\|β\|)·EPA02S(β): the \|β\| terms make differencing across β = 0 O(h) (error ~1e-5 relative at 1e-6) |
| p, q, r | 3e-3…3e-2 rad/s | **1e-2 rad/s** | f is at most quadratic in the rates: no truncation, and a large step keeps float noise ~1e-7 |
| θ, φ | 1e-6…1e-4 rad | **1e-5 rad** | pure double-precision trigonometry |
| V | 0.5…1.5 ft/s | **1 ft/s** | truncation ~1e-7 vs rate-normalization float noise ~5e-7 |

**Joint audit.** All steps are scaled together. The table gives the largest \|Δλ\| to the chosen steps ÷ max(\|λ\|, 0.1/s); the floor is the phugoid-like scale, since absolute eigenvalue error follows \|δA\|, not \|λ\|.

| point | ×100 | ×30 | ×10 | ×3 | ×1/3 | ×0.1 | ×0.03 | ×0.01 | ×0.003 |
|---|---|---|---|---|---|---|---|---|---|
| 47 (sym, start) | 2.4e-2 | 2.0e-3 | 2.2e-4 | 1.8e-5 | 8.0e-6 | 2.1e-5 | 1.1e-5 | 3.1e-4 | 3.5e-4 |
| 20 (sym) | 2.8e-2 | 2.3e-3 | 2.5e-4 | 2.0e-5 | 4.7e-6 | 6.4e-6 | 1.1e-5 | 2.7e-5 | 3.1e-5 |
| 46 (sym, end) | 3.6e-2 | 2.8e-3 | 3.1e-4 | 2.5e-5 | 8.4e-6 | 2.7e-5 | 1.1e-4 | 1.6e-4 | 5.8e-4 |
| 117 (turn, end) | 1.3e-1 | 1.3e-2 | 1.5e-3 | 1.2e-4 | 1.3e-5 | 1.5e-5 | 2.0e-5 | 9.9e-5 | 2.9e-5 |
| 124 (fold) | 3.1e-1 | 7.8e-3 | 6.2e-4 | 4.8e-5 | 1.2e-5 | 1.6e-5 | 3.2e-5 | 2.4e-4 | 5.2e-4 |
| 130 (saddle) | 8.8e-1 | 7.4e-2 | 8.1e-3 | 6.5e-4 | 7.0e-5 | 7.7e-5 | 9.1e-5 | 1.7e-4 | 6.2e-4 |
| 150 | 5.9e-2 | 3.9e-3 | 4.2e-4 | 3.4e-5 | 3.7e-6 | 2.2e-5 | 2.9e-5 | 1.6e-4 | 4.3e-4 |
| 165 (fork) | 1.9e-2 | 1.6e-3 | 1.8e-4 | 1.5e-5 | 3.6e-6 | 4.9e-6 | 6.6e-6 | 7.6e-5 | 4.3e-4 |
| 199 (turn, end) | 1.2e-1 | 1.4e-2 | 1.6e-3 | 1.3e-4 | 1.6e-5 | 1.6e-5 | 2.2e-5 | 1.1e-4 | 5.7e-4 |

- **Plateau:** ×3 and ×1/3 stay ≤ 6.5e-4 everywhere (asserted ≤ 1e-3).
- **Resolving power:** ×100 leaves the band everywhere.
- **Why the source's step cannot be used here:** the source's DX0 = 1e-9 relies on its double-precision routine. Maverick's transcription returns float coefficients, so it cannot, and no step is borrowed from the source.
- **These are NUMERICAL settings,** not physical uncertainty.

**Per-eigenvalue uncertainty.**
- **Measured as** the spread to the ×3 / ×1/3 spectra plus the scaled check (§5).
- **Zero band:** it enters the NUMERICAL zero band max(1e-6 /s, 10 × uncertainty).

## 5. Conditioning — direct vs scaled (`[S10]`)

- **Scaled coordinates:** z = D⁻¹x, D = diag(1° ×2, 1°/s ×3, 1° ×2, 10 ft/s). A_z is differenced **directly in z** at steps 0.7× the direct ones, so it shares no evaluation point with A or its plateau neighbours.
- **Independently differenced A_z vs A:** largest \|Δλ\| **2.95e-5**, i.e. 4.2e-5 of max(\|λ\|, 0.1). The plateau criterion is ≤ 1e-3.
- **Analytic similarity D⁻¹AD:** same eigenvalues to 3.7e-15. The eigensolver is insensitive to the units' 1e-4…1e2 scale spread.

## 6. Eigensolver (`[S9]`)

**Method.** Validation-side, no dependency:
- power-of-two balancing (exact);
- Householder Hessenberg reduction;
- single-shift complex QR with the Wilkinson shift;
- conjugate pairing;
- complex inverse iteration for eigenvectors.

The runtime aircraft code never names it (`[S6]` scan).

**Results.**

| Check | Result |
|---|---|
| 3 matrices with known spectra (conjugate pairs, a ±1e-6 pair, a near-double real pair) behind a dense similarity with a 1e-3…1e3 scale spread | 1.6e-15 relative |
| 170 Jacobians: \|trace − Σλ\| | 4.9e-15 |
| 170 Jacobians: \|det − Πλ\|/\|det\| | 5.0e-10 (largest where one eigenvalue is ~1e-6) |
| 1,360 eigenpairs: \|\|Av − λv\|\| / \|\|A\|\| | 1.7e-16 |
| Offline: numpy 2.3.5 / LAPACK `geev` on the exported Jacobians (`crosscheck_numpy.py`) | 4.2e-15 relative, all 1,360 |

## 7. Classification (`[S12]`)

**Rule.**
- **STABLE:** every Re < −band.
- **UNSTABLE:** any Re > +band.
- **NEAR_NEUTRAL:** some Re inside the NUMERICAL band. This is floating-point classification only; there is no source threshold.
- **Raw eigenvalues:** always kept (dataset).

**Counts.**

| Branch | STABLE | UNSTABLE | NEAR_NEUTRAL |
|---|---|---|---|
| symmetric (89) | 71 | 18 | 0 |
| turning (80) | 55 | 22 | 3 |
| pitchfork (1) | 1 | 0 | 0 |
| **total (170)** | **127** | **40** | **3** |

**Representative spectra** (1/s; tracked names, §11):

| point | stab° | α° | V | class | A (long. osc.) | B (long. slow) | C (lat. osc.) | D (lat. fast real) | E (lat. slow real) |
|---|---|---|---|---|---|---|---|---|---|
| 47 sym | −9.44 | 13.05 | 342.9 | UNSTABLE | **+0.814 ± 0.868i** | −0.0396 ± 0.155i | −0.206 ± 4.08i | −0.466 | −0.0587 |
| 8 sym | −10.78 | 14.11 | 331.0 | UNSTABLE | **+0.115 ± 1.357i** | −0.0228 ± 0.141i | −0.163 ± 3.89i | −0.400 | −0.0992 |
| 9 sym | −10.90 | 14.20 | 330.0 | STABLE | −0.031 ± 1.399i | −0.0199 ± 0.138i | −0.160 ± 3.87i | −0.396 | −0.103 |
| 20 sym | −12.44 | 15.40 | 318.3 | STABLE | −1.41 ± 1.000i | −0.0060 ± 0.115i | −0.131 ± 3.65i | −0.382 | −0.141 |
| 46 sym | −17.31 | 19.19 | 288.7 | STABLE | −0.481 ± 1.322i | −0.0281 ± 0.145i | −0.130 ± 2.90i | −0.477 | −0.190 |
| 117 turn | −5.72 | 8.30 | 661.9 | STABLE | −1.39 ± 2.26i | −0.0032 ± 0.121i | −0.489 ± 7.96i | −1.64 | −0.0198 |
| 124 fold | −6.49 | 9.80 | 533.4 | NEAR_NEUTRAL | −0.741 ± 0.743i | −0.0055 ± 0.117i | −0.421 ± 6.46i | −1.13 | **−6.8e-6** |
| 130 turn | −6.44 | 10.25 | 472.5 | UNSTABLE | −0.452 ± 0.523i | −0.0083 ± 0.111i | −0.371 ± 5.73i | −0.942 | **+0.0117** |
| 136 fold | −6.42 | 10.42 | 438.4 | NEAR_NEUTRAL | −0.332 ± 0.829i | −0.0089 ± 0.112i | −0.341 ± 5.32i | −0.853 | **+7.8e-7** |
| 150 turn | −6.46 | 10.56 | 391.1 | STABLE | −0.233 ± 1.357i | −0.0083 ± 0.116i | −0.298 ± 4.74i | −0.760 | −6.0e-4 |
| 165 fork | −6.50 | 10.59 | 377.4 | STABLE* | −0.209 ± 1.504i | −0.0074 ± 0.118i | −0.284 ± 4.56i | −0.736 | **−3.05e-5** |
| 199 turn | −6.13 | 8.79 | 625.7 | STABLE | −1.24 ± 1.85i | −0.0037 ± 0.122i | −0.477 ± 7.55i | −1.47 | −0.0180 |

\* **Point 165:** its critical −3.05e-5 is outside the numerical band (1.1e-5) but **inside its print spread (4.0e-5)**: printed V 377.4 has 4 digits. It is consistent with the zero a pitchfork requires (§10).

## 8. Source vs computed (`[S13]`)

**The source's label.** Table VII labels every row stable, and nothing finer is printed. Computed: **AGREE 127, DISAGREE 40, UNRESOLVED 3.**

**Disagreements.**

| Disagreement | Points | Computed | Cause |
|---|---|---|---|
| Turning, between printed stabilator extrema | 125–135, 183–193 (22) | one **real** eigenvalue > 0 (max +0.0149 /s at 128/189, doubling ≈ 46 s) — a **saddle** | **Structural.** A stabilator extremum along a branch is a limit point: ∂f/∂x is singular, a real eigenvalue crosses zero (Davison p.21), and §9 locates exactly that. One side of each fold must be unstable whatever the coefficients. |
| Symmetric, α 13.05–14.11° | 1–8, 47–52, 55–58 (18) | a **longitudinal complex pair** with Re > 0 (up to +0.81 /s, ω ≈ 0.87–1.36 rad/s) | **The source's own CMMQ.** The pitch-damping fit (ATAB05) is positive for α ≈ 10.6–14.3° (+19.8 /rad at 13°, −10.2 at 8°, −25.9 at 15.4°). All three printings (Baumann p.114, Davison App. B p.116, App. C p.147) were re-read on the page images, and Maverick's transcription matches every coefficient and sign; the trailing "−" / continuation "+" makes the RAL⁸ term negative. CMM = CMM1 + CMMQ·QB with no other q term. The instability follows analytically: Mq = K8·V·c̄/2·CMMQ ≈ +2.0 /s at point 47. **Table VII cannot test CMMQ there: its symmetric rows have q = 0.** |

**Unresolved.** The 3 unresolved points are the printed limit points 124, 136 and 182 themselves, where one eigenvalue is ~0 by definition.

**Baumann's own plotted stability.** App. C: stable solid, unstable dashed. Figure C-7 (φ vs stabilator, PDF p.122) was read on the page image:
- **symmetric branch:** **dashed** for stabilator ≈ −6.5…0 and below ≈ −20, **solid** between;
- **turning loop:** solid throughout.

Against this figure:

| Figure C-7 | Computed | |
|---|---|---|
| symmetric dashed on the turning side of the fork (≈ −6.5…0) | lateral real eigenvalue > 0 for V > 377.44 (+0.011 /s at stabilator −5.29; +0.012 at −5.15, next to the demonstrated range's −5° edge, beyond which WP-3B cannot solve) | **agree** — the supercritical pitchfork |
| symmetric dashed below ≈ −20 ("unstable for δe < −19.73") | lateral Hopf at −19.33 (§11) | **agree** (0.40° apart) |
| symmetric solid −20 … −6.5 | unstable for −10.88 … −6.96 (longitudinal pair), stable elsewhere | **disagree** on α 11.0–14.2° |
| turning loop solid | saddles between the folds | **disagree** |

**Reading.**
- **The CMMQ disagreement** says Baumann's *executed* model had pitch damping that the *printed* CMMQ does not. Consistent with that, Table VII shows no inserted, irregularly spaced point near either longitudinal Hopf, as it does at every fold and at the fork.
- **The fold disagreement** cannot be reconciled with any smooth 8-state model that has these equilibria, at the figure's resolution.
- **Neither is resolved by tuning.** The coefficients are the source's as printed.

> **WP-3E follow-up** (`F15_RESEARCH_STABILITY_CONFLICT_AUDIT_V1.0.md`).
> - **Nonlinear confirmation:** both instabilities hold in the nonlinear time domain.
> - **Davison agrees with Baumann's presentation:** his Figure 7 (12-state, CAS off; the same equilibria) also draws α 11–14° stable.
> - **Every source-supported alternative reading is ruled out** by the source's own turning equilibria: sign, normalization, units, inertia, actuator states and Jacobian construction.
> - **Unresolved:** which CMMQ the executed AUTO runs used.

## 9. Folds (`[S14]`)

**Method.** Each fold is bracketed by the printed neighbours of a printed stabilator extremum. Along φ, where WP-3C's parameterization passes through folds smoothly:
- the real eigenvalue nearest zero is **bisected** for its sign change (30 WP-3C solves);
- a parabola is fitted through 9 recovered stabilator values across the bracket, to find the extremum.

**Results.**

| Bracket | φ where λ = 0 | φ of stabilator extremum | stabilator at λ = 0 | printed extremum |
|---|---|---|---|---|
| 123–125 | −56.52820 | −56.5571 | −6.492752 | −6.492752 (124) |
| 135–137 | −41.04874 | −41.0481 | −6.421912 | −6.421912 (136) |
| 181–183 | +41.04872 | +41.0398 | −6.421912 | −6.421912 (182) |
| 193–195 | +56.52820 | +56.5369 | −6.492752 | −6.492752 (194, unavailable) |

- **The Jacobian is singular exactly where the source's branch folds.** The printed extrema are AUTO's limit points; their irregular spacing in α (123→124 is 0.026° against 0.109° for 124→125) is the signature of an inserted special point.
- **Mirror:** left and right folds sit at mirror bank angles to 2e-5°.
- **The crossing mode** is Mode E, tracked from the fork (§11). It is spiral-like at the fork and becomes V/θ-dominated toward the folds (lateral fraction 0.07 at 136, 0.01 at 124).

## 10. The pitchfork (`[S15]`)

**Method.** Point 165 is approached three ways, with every point re-solved and no continuation:
- the **symmetric** branch: WP-3B at V = 377.4 − 20 … + 20 ft/s;
- both **turning** branches: WP-3C at \|φ\| = 20 … 1e-4°, from printed 164 / 166.

**Symmetric branch.**
- **A real eigenvalue changes sign:** −2.48e-2 at V 357.4, −3.05e-5 at 377.40, +9.9e-6 at 377.45, +1.09e-2 at 397.4. Bisection puts the zero at **V 377.4378, stabilator −6.493828, α 10.58652.**
- **It is the symmetry-breaking mode:** its eigenvector is **purely lateral** (lateral fraction 1.000). In deg / deg·s⁻¹ it is φ 1.00, r 0.080, p −0.022, β 0.002: a φ-dominated, spiral-type shape. This is reported qualitatively; no stronger physical reading is claimed.

**Turning branches (±φ).**
- **The same eigenvalue stays negative on both sides:** −5.5e-4 at \|φ\| 20°, −3.2e-5 at 2°, −1.6e-6 at 0.1°. Below \|φ\| ≈ 0.05° it sits inside the numerical band, where WP-3C found conditioning collapsing. **It touches zero at the fork without crossing.**
- **Eigenvector:** it gains θ, q and V components as \|φ\| grows (at 20°: φ 1.0, V 0.18, θ 0.11, r 0.07).

**Classification.** The turning pair exists on the side where the symmetric branch is unstable, and is stable near the fork, so the pitchfork is **supercritical**. This agrees with Figure C-7's dashed symmetric segment beside the loop.

**Location against the source.** The crossing's own numerical uncertainty is ±1.3e-3 ft/s in V and ±9.1e-5° in stabilator.
- **Printed 165:** stabilator 4.6e-5° away.
- **The turning branches' \|φ\| = 0.1° limit:** 7.4e-5° away.
- **V** is within the printed half unit of 0.05.

**At the recovered 165** (V = 377.4 as printed), the critical eigenvalue is −3.05e-5. That is inside its print spread (4.0e-5): the fork sits within the printed V's half unit.

## 11. Mode tracking, crossings, degeneracies, and the Hopf points (`[S17]`, `[S18]`)

**Tracking method.**
- Modes follow the minimum-distance assignment between neighbours (all 8! permutations; deterministic), **never** a per-point sort.
- The symmetric branch is seeded at point 47 in stabilator order. The turning branch is seeded at the fork 165 and tracked outward both ways.
- Largest tracking ambiguity (step ÷ distance to the nearest other eigenvalue): 0.16 symmetric, 0.28 turning. None is flagged (> 0.5), and no coalescence occurs.

**Modes.** Names are neutral; the interpretation is given only where block membership, frequency and eigenvector agree, and always with the caveat that these are **the source model's modes, without CAS**:

| Mode | At the seeds | Shape (deg, deg/s, ΔV/V ×57.3) | Interpretation |
|---|---|---|---|
| A | longitudinal complex, ω 0.87–1.50 | q 1.0, θ 0.66–0.84, α 0.65 | short-period-like |
| B | longitudinal complex, ω ≈ 0.12–0.16 | θ 1.0, ΔV/V 0.6–0.7 | phugoid-like |
| C | lateral complex, ω ≈ 4.1–4.6 | p 1.0, φ 0.21–0.24, r 0.14–0.19, β 0.08–0.09 | Dutch-roll-like (the only lateral oscillatory mode; β and r both participate; roll-rate dominated, \|φ/β\| ≈ 2.5–2.7) |
| D | lateral real, −0.47 / −0.74 | φ 1.0, p 0.46–0.72 | roll-subsidence-like |
| E | lateral real, −0.059 / −3e-5 | φ 1.0, r 0.07–0.08 | spiral-like; on the turning branch it becomes the fold mode |

**Events.**
- **Symmetric:** A changes sign between 58 and 9 (Hopf).
- **Turning:** E changes sign at 124/125, 136/137, 181/182 and 193/195 (the folds).

**Hopf crossings on the symmetric branch.** Located by bisection in V with WP-3B solves from a fixed printed seed; no continuation.

| Crossing | V ft/s | stabilator° | α° | ω rad/s (period) | Content | Source |
|---|---|---|---|---|---|---|
| A, near the fork (untabulated) | 370.84 | −6.963 | 11.00 | 1.446 (4.35 s) | longitudinal | not mentioned |
| A, between points 8 and 9 | 330.20 | −10.878 | 14.18 | 1.392 (4.52 s) | longitudinal | not mentioned; Figure C-7 solid across it |
| C, beyond Table VII | 278.38 | **−19.326** | **20.82** | 2.563 (2.45 s) | lateral | Baumann: −19.73°, "19° α", unstable below; Davison: wing rock at 20° α |

- **The lateral Hopf** lies inside the source-exercised V span. At α 20.8° the high-α CFX fit carries weight 0.019, so the two CFX2 printings (1e-6 apart) differ by ~2e-8 in CFX there, below float resolution.
- **The two longitudinal Hopf points** bound the positive-CMMQ band.

## 12. Mirror spectra (`[S16]`)

**The mirrored state** (β, p, r, φ negated):
- **matrix:** A(mirror) = S·A·S **bit for bit at 79/80** turning equilibria, S = diag(+1 −1 −1 +1 −1 +1 −1 +1). The 80th is within 2.5e-14 of max\|A\|. The routine's β sign blends EPA02S/L are odd in exact arithmetic, but differ by up to 4e-10 in double (cancellation near 0); the float coefficient cast erases almost all of it.
- **spectrum:** spectrum(+φ) = spectrum(−φ), reproduced by the eigensolver to 3.4e-10.

**Other mirror checks.**
- **80 solver-found mirror roots** (WP-3C at −φ from the *unmirrored* start): within 8.1e-6 of max(\|λ\|, 0.1). This carries the solver's root defect (WP-3C: α 8.9e-5°, V 2.2e-3 ft/s), and is numerically indistinguishable by the plateau criterion.
- **Table VII's own printed mirror pair 136 / 182,** each recovered from its own row: identical spectra (0.0).

## 13. Scope — source model vs runtime

**What this is.** The open-loop local stability of the **research source model** as transcribed: the Mach 0.6 fit at source-exercised speeds, fixed RHO, 8,300 lbf and no CAS.

**It is not:**
- the real F-15's stability;
- NASA 836's (a different aero database and configuration);
- the production FCS/CAS;
- a Unity Rigidbody's (different integration, atmosphere, surfaces);
- handling qualities.

**No control-law stability is claimed.** The source model contains no control law.

## 14. Dataset — `Docs/Reference/Data/F15/stability/`

Generated by `MavF15ResearchStabilityValidation.ExportDataset` in Unity 6000.3.16f1 and extracted from the batch log (`extract_from_log.py`). The Table VII dataset is not touched.

| File | Rows | Content |
|---|---|---|
| `f15_research_stability_table_vii.csv` | 1,360 | one per eigenvalue per equilibrium. **Equilibrium:** point, branch, stabilator, V, α, β, p, q, r, θ, φ. **Eigenvalue:** tracked mode, real, imag, natural frequency, damping ratio, lateral fraction. **Class:** mode and equilibrium classification, unstable count, source classification, agreement. **Uncertainty:** FD step spread, scaled difference, zero band, print spread, tracking ambiguity. |
| `f15_research_stability_jacobians.csv` | 170 | A (64 entries, physical units, row-major) |
| `f15_research_stability_step_audit.csv` | 720 | 9 equilibria × 10 step factors × 8 eigenvalues |
| `f15_research_stability_pitchfork_approach.csv` | 58 | the three approaches, critical eigenvalue and eigenvector |
| `f15_research_stability_crossings.csv` | 8 | pitchfork, 3 Hopf, 4 folds: bracket, state, ω, lateral fraction |

## 15. Tests — `MavF15ResearchStabilityValidation`, 40 / 0

**Checks:**
- `[S1]` source semantics and K constants;
- `[S2]` finite / pure;
- `[S3]` independent RHS;
- `[S4]` inertia inversion;
- `[S5]` ẋ at 170 equilibria;
- `[S6]` NASA 836 refused; no F100 / atmosphere / propulsion / eigensolver dependency;
- `[S7]` determinism;
- `[S8]` structure and units;
- `[S9]` eigensolver;
- `[S10]` direct vs scaled;
- `[S11]` step plateau;
- `[S12]` classification;
- `[S13]` source comparison;
- `[S14]` folds;
- `[S15]` pitchfork;
- `[S16]` mirror;
- `[S17]` tracking;
- `[S18]` Hopf;
- `[S19]` coefficients, CFX2, atmosphere and WP-3B/C trim roots bit-identical before and after.

**Asserted tolerances:** numerical only.
- **Implementation identity:** 1e-12.
- **Printed-constant identity:** 1e-7.
- **Eigensolver:** 1e-10.
- **Plateau:** 1e-3 of max(\|λ\|, 0.1/s).

Agreement with the source is reported, never asserted.

**All 15 F-15 suites:** 673 / 0. The 14 existing suites are unchanged at 633 / 0. Their reports are textually identical to the WP-3C run except the file-scan counts (+4 files).

## 16. What this justifies next

**Pseudo-arclength continuation (D16) — still justified, and now sharper.**
- **Branches:** it can trace the branches through the four folds and the fork located here.
- **Hopf points:** it can follow the three Hopf points and map the symmetric segment between the fork and Table VII's start (stabilator −6.49 … −9.43), which no printed row covers.
- **Figure C-7:** it can check whether the source's full diagram (C-1…C-9) reproduces with the printed CMMQ.
- **Periodic orbits:** AUTO also computed periodic solutions from the Hopf points, which needs continuation.

**A research-only runtime flight phase — justified only in stages.** Stage 1 (off-body integration) was **done in WP-3E**, and it confirmed every prediction below.
- **What the eigenvalues now predict**, as concrete time-domain checks:
  - hold at stable points (e.g. 20, 150);
  - drift at the saddles (doubling ≈ 46 s at 128);
  - grow at the α 13° pair (doubling ≈ 0.85 s, period ≈ 7 s).
- **First step:** an **off-body integration of this RHS** (no Rigidbody), to confirm those predictions against the linear model.
- **Only then a Rigidbody phase,** and only after the flying research body's prerequisites:
  - density override (D11);
  - stabilator authority;
  - the fixed-thrust semantics.
- **Frame:** a flown result is a comparison with the source model, not with the aircraft.

## 17. Not done

- **Not built:** Rigidbody flight, PlayMode flight, continuation, periodic orbits, runtime density override, actuator limits or rates.
- **Not changed:** Baumann coefficients (CMMQ included), mass or inertia, thrust, source density, gravity, F100, NASA 836, `MavSteadyFlightTrimSolver`, `MavAtmosphereModel`, `MavSixDoFBody`, the WP-3B/C trim solver.
- **Untouched:** scenes and prefabs. F15Replacement is not enabled.
