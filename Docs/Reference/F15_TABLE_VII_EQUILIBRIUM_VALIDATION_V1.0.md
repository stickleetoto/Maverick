# F-15 — Baumann Table VII: Dataset and Static Equilibrium Reproduction (V1.0, WP-3A)

> **Research configuration only** (`F15_AFIT_BAUMANN_DAVISON_MACH06_20K_RESEARCH`). **§1–§6 are not a trim solver.** Published states go in, residuals come out. Nothing is iterated, adjusted or tuned. **§7 (WP-3B)** adds the trim recovery: the symmetric states solved from perturbed starts by `MavF15AfitResearchTrimSolver`.

**Headline:** Maverick's transcription of Davison's 1992 coefficient routine, with the research mass, inertia, 8,300 lbf thrust and the source's fixed density, **reproduces Baumann's 1989 Table VII equilibria to print precision in all six axes**. That covers all 170 states that can be assembled, symmetric and turning, at implied Mach 0.28–0.64.

**No systematic difference is detected.** One version difference exists in the source listings themselves (§6). It lies outside every channel Table VII exercises.

| | |
|---|---|
| Source | Baumann, AFIT/GAE/ENY/89D-01, DTIC ADA217366, App. C Table VII, PDF pp.124–133 |
| Dataset | `Validation/MavF15BaumannTableVii.cs` + generated `MavF15BaumannTableViiData.cs` |
| Transcription | `Docs/Reference/Data/F15/table_vii/` (the two halves as printed, and the generator) |
| Evaluator | `Validation/MavF15TableViiEquilibriumReproduction.cs` |
| Tests | `Validation/MavF15BaumannSourceConditionValidation.cs` `[W2]`–`[W7]`, PASS |

---

## 1. The dataset — stored exactly as printed

**Structure.** Table VII prints each of its 201 points across two halves.

| Half | PDF pages | Columns |
|---|---|---|
| First | pp.124–128 | stabilator, α, β, p |
| Second | pp.129–133 | q, r, θ, φ, V (kft/s) |

**Common to every point** (from the caption): aileron, rudder and differential stabilator 0°; thrust 8,300 lb; altitude 20,000 ft.

**How it was read.**
- **Two independent readings.** Every row was read off the rendered page images. That reading was then checked value by value against the PDF's text layer, which drops rows (e.g. first-half points 7–8 and second-half point 13) and garbles digits.
- **Result:** apart from the rows the text layer drops or garbles, and the scan-damaged fields below, the two readings agree **verbatim** on every value. Every remaining disagreement was re-read on a zoomed image.
- **Precision as printed:** stabilator and α to 7 significant digits; every other column to 4.

**Scan-damaged fields.** Nothing is guessed.

| Point | Field | Printed | Stored |
|---|---|---|---|
| 1st-half 40 | α | `1.?08001E+01` | `1.808001E+01`, as printed clearly at point 90 — the same equilibrium on the parallel branch |
| 1st-half 53, 54 | p | `1.126E-?1`, `1.032E-?1` | **NaN** (illegible) |
| 1st-half 175 | β | `3.97?E-02` | **NaN** (illegible) |
| 2nd-half 60 | θ | `1.530?+01` (exponent letter damaged) | `1.530E+01`, legible, and point 10 prints the same equilibrium |
| 2nd-half 152 | q | exponent letter damaged | `7.948E-03`, legible |
| 2nd-half 196 | r | `4.96?E-02` | **NaN** — the mirror state 126 is not the same equilibrium |

**Ranges as printed:** stabilator −17.30722 to −5.275754°; α 7.875753 to 19.18532°; V 288.7 to 699.7 ft/s.

**Branch structure, read from the values:**

| Points | Branch |
|---|---|
| 1–46 and 47–92 | two symmetric branches through the same equilibria; points 51–92 repeat points 1–42 |
| 93–116 | turning branch |
| 117–135 | a turning segment starting at a new stabilator |
| 136–159 | reprints 93–116 exactly |
| 160–201 | continues through the symmetric state at point 165 into the mirror-image turn |

## 2. The two halves do not align row-for-row — a source finding

In the turning region, the second half's r, θ, φ and V columns are printed **two rows lower** than the first half's p, α, β and stabilator, and than the second half's own q column.

**Evidence** (no aerodynamics involved). The source's own steady-state kinematics must hold at every equilibrium. θ̇ = q cos φ − r sin φ = 0 (source `F(6)`) and φ̇ = p + (q sin φ + r cos φ) tan θ = 0 (`F(7)`).
- Under the displaced pairing (first-half point i; q from second-half point i; r, θ, φ, V from second-half point i+2), both close **to print precision** for every turning point.
- Under same-number pairing they fail: `[W3]` finds the displaced pairing better in **80 of 80** turning states.
- Matching the roll rate the kinematics imply against the printed p reproduces it to 3–4 digits, in sign and magnitude, throughout points 117–199.

**What cannot be paired.** First-half points 92–116, 200 and 201 have no demonstrable companion. Their second-half candidates are either the **mirror-image** turn (opposite bank, yaw rate and implied p) or not printed at all. They stay printed but are **not assembled** (`UnassembledReason`).

**Assembled:**

| Group | States | V, ft/s | Implied Mach at 20,000 ft |
|---|---|---|---|
| Symmetric (points 1–91, less 53, 54) | **89** | 288.7–342.9 | 0.278–0.331 |
| Turning (points 117–199, less 175, 194) | **81** | 377.4–662.0 | 0.364–0.638 |

## 3. Static reproduction — method

**Inputs, per state:**
- **Aerodynamics:** Maverick's `MavF15BaumannMach06Longitudinal` + `MavF15BaumannMach06LateralDirectional`, the six-axis transcription.
- **Rate normalization:** the source's own p̂ = pb/2V, q̂ = qc̄/2V, r̂ = rb/2V.
- **Stabilator:** from a `MavF15ResearchStaticControlState` (STATIC_EQUILIBRIUM_VALIDATION_ONLY), inside the WP-2 demonstrated range. Other surfaces are 0.
- **Mass and inertia:** 37,000 lb; Ix 25,480, Iy 166,620, Iz 186,930, Ixz −1,000 slug-ft².
- **Thrust:** 8,300 lbf total along +X, plus THRUST·(0.25/12) ft nose-up.
- **Source atmosphere semantics:** ρ = 0.0012673 slug/ft³ fixed, q = ½ρV² with the state's V, g = 32.174 ft/s².
- **Geometry:** S 608 ft², b 42.8 ft, c̄ 15.94 ft. Units are the source's throughout.

**Equations:** standard body-axis Newton–Euler equilibrium with the inertia tensor [Ix 0 −Ixz; 0 Iy 0; −Ixz 0 Iz]. The source's K-constant form reduces to exactly these (its K12, K13 and K9 are the standard coefficients).

| Residual | Normalized by |
|---|---|
| X, Y, Z (body axes; X fwd, Y right, Z down), including gravity and ω×V | weight |
| rolling moment L | q·S·b |
| pitching moment M | q·S·c̄ |
| yawing moment N | q·S·b |
| θ̇, φ̇ | rad/s (kinematics) |

**Print-precision floor, per residual:** the first-order change when every printed field moves by half a unit in its last printed digit. It is context for reading a residual, **not a pass threshold**.

## 4. Results

| Residual | Symmetric (89): max \|res\| | max res/floor | Turning (81): max \|res\| | max res/floor | States above floor |
|---|---|---|---|---|---|
| X / W | 8.9e-5 | 0.95 | 8.7e-5 | 0.92 | 0 |
| Y / W | ~1e-22 † | — | 1.6e-4 | 0.85 | 0 |
| Z / W | 3.1e-4 | 0.90 | 3.0e-4 | 0.84 | 0 |
| L / qSb | ~1e-22 † | — | 1.5e-7 | 0.78 | 0 |
| M / qSc̄ | 1.7e-7 | 0.92 | 9.6e-7 | 0.89 | 0 |
| N / qSb | ~1e-22 † | — | 1.4e-7 | 0.68 | 0 |
| θ̇ (rad/s) | ~1e-24 † | — | 1.1e-5 | 0.73 | 0 |
| φ̇ (rad/s) | 7.9e-25 | 0.78 | 7.5e-6 | 0.82 | 0 |

† **Numerically zero.** At symmetric points the printed lateral values are ~1e-19 continuation noise. Their floors are smaller still, so a ratio there carries no information.

**Reading.**
- **Every residual sits inside its print-precision floor.** The residuals are what the published digits allow, and no larger.
- **What this establishes:** the transcription reproduces the source equilibria to print precision. That holds at implied Mach 0.28–0.64, far from the Mach 0.6 fit condition for most points. It also confirms:
  - **the sign conventions**: flipping the stabilator worsens the pitch residual at all 170 states (`[W5]`);
  - **the thrust-line moment**;
  - **the inertia coupling**;
  - **the rate normalizations**;
  - **the source's fixed-density dynamic pressure**.
- **What it cannot establish:**
  - that the coefficients are *valid* at those speeds (source-exercised ≠ validated, `F15_BAUMANN_SOURCE_CONDITION_AUDIT_V1.0.md`);
  - anything about channels the table never exercises: aileron, rudder, differential tail, α > 19.2°, |β| > 0.043°.

## 5. Version comparison — Baumann 1989 vs Davison 1992

| Item | Baumann 1989 | Davison 1992 | Effect on Table VII |
|---|---|---|---|
| Driver constants | RHO .0012673; S 608, b 42.8, c̄ 15.94; 37,000 lb; IX 25,480 / IY 166,620 / IZ 186,930 / IXZ −1,000; THRUST 8,300 | identical (App. B and C drivers) | none |
| K1–K17 as printed | all 11 printed values reproduce from the inertias to ≤ 5e-8 relative. `K16 = K13` is an exact identity. | same values | none |
| Thrust in COEFF | `CX += THRUST/QBARS`, `CMM += THRUST*(0.25/12)/(QBARS*CWING)` (p.117) | identical (pp.120, 150) | none |
| State vector | 8 states; surfaces are parameters; δΔe = 0.3·δa inside COEFF | App. B: 12 states, adding first-order actuator lags and an independent differential-tail state | none at equilibrium (lagged surfaces settle at their commands) |
| **CFX2 trailing constant** | **+0.09833617** (PDF p.108) | App. B: **+0.09833617** (p.110) · **App. C: +0.09833517** (p.140) — **Maverick follows App. C** | **none**: CFX2 is weighted zero below α 20° and Table VII tops out at 19.2° |

**CFX2 constant.**
- **What it is:** the Appendix C simulator listing differs from the other two printings by 1e-6 in the high-AoA drag fit. All three were read on rendered pages.
- **Status:** recorded in `MavF15BaumannMach06Longitudinal` beside the constant. **Not changed** — the brief forbids changing Baumann coefficients, and Table VII cannot say which printing is right.
- **Why it matters later:** F15-AUDIT-010's earlier "5" reading was correct *for App. C*. Its uniqueness is what is now in question.

**Not compared:** a coefficient-by-coefficient diff of the two `COEFF` listings beyond what Table VII exercises. The text layers are too noisy for an automated diff, and Table VII already shows agreement in every exercised channel. Unexercised channels (aileron, rudder, differential tail, high-α and asymmetric terms) are **unverified against Baumann 1989**.

## 6. What this unblocks — and what it does not

- **Static checks are meaningful now.** The research model, driven by the source's own states, lands on the source's own equilibria. A trim solver built on this model therefore has a sourced target, and a reproduction test to pass first. **WP-3B built that solver (§7).**
- **Still open for flying trim** (WP-3B solved trim off the body; nothing was flown):
  - the flying research body still has **zero surface travel** (WP-2);
  - Maverick's density **follows altitude**, while the source's does not (`SourceReproduction` admits only 6,096 ± 1 m);
  - Maverick's standard-atmosphere density at 6,096 m is **6.8e-4 below** the source's RHO, which shifts q by the same fraction;
  - trajectory-level behaviour (Davison's simulator time histories) is **unvalidated**.

## 7. WP-3B — the symmetric equilibria recovered by trim

**Solver:** `MavF15AfitResearchTrimSolver` (`F15_RESEARCH_TRIM_SOLVER_V1.0.md`). It solves the source's own symmetric equilibrium:
- **Parameter:** V, as printed.
- **Unknowns:** α, stabilator, θ.
- **Residuals:** X, Z and M.
- **Inputs:** the fixed RHO, 8,300 lbf total thrust, and its 0.25-in thrust-line moment.

**Method:**
- Each of the **89** symmetric states is solved from **four** deterministic perturbed starts (up to 2° α, 1° stabilator, 3° θ). The published row builds the start and is the reporting target; it never enters the residual.

**Results:**
- **356 / 356 converged** in 2–3 iterations.
- **Recovered minus printed:**

  | | α | stabilator | θ |
  |---|---|---|---|
  | max | 6.66e-3° | 8.47e-3° | 6.82e-3° |
  | mean | 3.07e-3° | 3.94e-3° | 2.69e-3° |

- **Print floor:** `|dx/dV|·½ unit of the printed V + ½ unit of x`, where V has 4 digits. It is context, not a threshold. **0 of 89 states exceed it in any unknown**, and the largest difference is 0.96 of the floor.
- **Independent check:** the §3 evaluator, at the recovered states, gives X/W ≤ 2.5e-7, Z/W ≤ 9.8e-7 and M/q̄Sc̄ ≤ 6.1e-8. The lateral residuals are exactly 0.
- **Point 165** — the symmetric pitchfork state printed inside the turning section — is also recovered within its floor.
- **Other roots:** a 455-start grid finds no second root at any V probed.

**What this does not add:** anything about validity away from Mach 0.6, and anything about the turning states. Those are WP-3C (`F15_RESEARCH_TRIM_SOLVER_V1.0.md` §11).
