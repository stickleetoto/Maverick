# F-15 — Research Source Stability Conflict Audit (V1.0, WP-3E Goal B)

> **Research source model only.** This audits why the AFIT/Baumann/Davison model's **printed equations** and the theses' **stability presentation** disagree. It was done from public source evidence and isolated, validation-side experiments. **No model correction was made:** no coefficient (CMMQ included) was edited, and no "corrected" stability model exists. Unavailable manufacturer data was not inferred.

## Verdict

**PUBLIC PRINTINGS AGREE — EXECUTED AUTO MODEL MAY DIFFER.**

| Finding | Status |
|---|---|
| Every public printing of the pitch-damping fit CMMQ is identical, and Maverick's transcription matches it | **established**: Baumann 1989 App. B (PDF p.114) = Davison 1992 App. B (p.116) = App. C (p.147), all read on page images |
| With the printed CMMQ, symmetric equilibria at α ≈ 11.0–14.2° are unstable (a longitudinal oscillatory pair) | **established twice**: WP-3D eigenvalues, and here the nonlinear time integration (+0.814 /s at point 47, measured) |
| Both theses present that range as stable | **established**: Baumann Figures C-1…C-8 (solid there) and Table VII caption; Davison Figure 7 (solid) and text *"At flight conditions up to 20 degrees (30 units) AOA, the aircraft can be flown with impunity"* (PDF p.43) |
| The executed AUTO run located no Hopf where the printed CMMQ puts one | **indirect evidence**: Table VII shows AUTO's located special points as short steps at every fold and the fork, but none at the located Hopf (§4) |
| Which CMMQ the executed runs used | **UNRESOLVED**: not public |
| Why Baumann draws the fold saddles solid | **UNRESOLVED**: no source explanation; the instability there is structural (any smooth model with these equilibria) and is confirmed nonlinearly |

**Can the printed equations and the thesis presentation be reconciled?**
- **Partly.**
  - Table VII's caption is best read as a loose label for the printed branch segments (SUPPORTED, §5).
  - The figures agree with the printed equations about the fork-side and the beyond-Hopf instabilities.
- **Not the rest.**
  - No source-supported reading of the printed equations makes α 11–14° stable. Every alternative reading tested is ruled out by the source's own equilibria (§3).
  - The fold saddles cannot be stable in any smooth model with Table VII's equilibria.

---

## 1. What the sources say about stability

| Evidence | Source (read on page images) | What it says |
|---|---|---|
| Definition | Baumann p.25, p.32; Davison p.19, p.21 | Stable = Jacobian eigenvalues in the left half plane ("negative or zero", Davison). A limit point is a real eigenvalue crossing: "At least one portion of the branch departing the limit point is unstable." |
| AUTO output | Baumann p.54, p.57 | Fort.9 gives "the eigenvalues of the Jacobian matrix which were used as the primary indication whether the equilibrium solutions were stable or not". Hopf points are detected "by monitoring the number of eigenvalues in the left-half plane". Too large a step can miss a Hopf (p.58). |
| Table VII caption | Baumann p.124 | "a compilation of stable low α equilibrium states (for plots C-1 through C-8)". No per-row flag and no eigenvalue anywhere. |
| Figure legend | Baumann p.25, App. C p.118 | Stable solid ("heavy lines"), unstable dashed. Appendix C figures "depict both stable and unstable equilibrium solutions". |
| Hopf statements | Baumann p.118; p.60; Davison p.42 | "loses its stability at −19.73° through a Hopf bifurcation"; "a Hopf bifurcation to periodic motion at 19° angle of attack"; Davison: wing-rock Hopf at 20° AOA. |
| Version remarks | Davison App. B p.100 (Beck, 30 May 90); p.101 (Davison, 22 Jul 90) | COEFF "was taken directly from Dan Baumann's program D2ICC. The only changes made were …" (interface only). Also "a merger of a later subroutine to an earlier version". |
| Other remarks | — | No statement anywhere that a figure or table came from a different model version than the printed listing. No statement distinguishing the listing from the executed code. |

**Line styles, read on the page images:**
- **Baumann Figures C-2 to C-8** show the same pattern throughout:
  - the symmetric branch is **dashed** for stabilator ≈ −6.5…0° (inside the turning loop) and below ≈ −20°;
  - it is **solid** between −20° and −6.5°;
  - the turning loop is **solid** throughout.
- **Davison Figure 7** (12-state, CAS off) has equilibria that match Table VII: α 10.0 at elevator −5.9°, ≈ 17.4 at −15.2°, Hopf ≈ 21° at −19.8°. It is **solid** from −5.9° to the Hopf and dashed beyond.

**Against the printed equations** (WP-3D eigenvalues, confirmed nonlinearly in WP-3E):

| Segment | Printed equations | Baumann figures | Davison Fig. 7 |
|---|---|---|---|
| symmetric, stabilator −6.5…0 (turning side of the fork) | **unstable** (spiral-type real mode, supercritical pitchfork) | dashed — **agree** | solid down to −5.9 (a ≲ 13 px segment at that scale — not resolvable) |
| symmetric, −6.96…−10.88 (α 11.0–14.2°) | **unstable** (longitudinal pair, CMMQ > 0) | solid — **disagree** | solid — **disagree** |
| symmetric, −10.88…−19.33 | stable | solid — agree | solid — agree |
| symmetric, below −19.33 | unstable (lateral Hopf, wing-rock type) | dashed below ≈ −20 — agree | dashed beyond ≈ −19.8 — agree |
| turning, between folds (125–135, 183–193) | **unstable** (saddle, structural) | solid — **disagree** | — |

## 2. CMMQ provenance

| Item | Value |
|---|---|
| Name | CMMQ = ATAB05, "PITCH DAMPING DERIVATIVE — CMQ" (Davison COEFF comments) |
| Use | CMM = CMM1 + CMMQ·QB (Baumann p.114); then CMM += THRUST·(0.25/12)/(QBARS·CWING) (p.117) |
| Rate normalization | QB = (Q·CWING)/(2·VTRFPS) (Baumann p.106), with Q = U(4) in rad/s and VTRFPS = 1000·U(8) (p.100) |
| Variable | RAL = AL/DEGRAD, α in radians (p.101) |
| Units | per unit QB (dimensionless); CMM dimensionless; q̇ = K8·V²·CMM |
| Fit | α ≤ 0.25307 rad: −3.8386262 + 13.54661297 r + 402.53011559 r² − 6660.95327122 r³ − 62257.89908743 r⁴ + 261526.10242329 r⁵ + 2177190.33155227 r⁶ − 703575.13709062 r⁷ − 20725000.34643054 r⁸ − 27829700.53333649 r⁹ |
| | 0.25307–0.29671: a cubic spline about 0.2530699968 |
| | ≥ 0.29671: a 6th-order polynomial |
| Signs | the RAL⁸ term is negative: the line ends in "−" and the next line opens with the Fortran continuation mark "+". Identical in all three printings. |
| Values | −3.84 (0°), −10.2 (8°), +0.5 (10.6°), **+19.8 (13°)**, −8.5 (14.5°), −25.9 (15.4°), −7.3 (20°) |

The polynomial's large, cancelling terms (±300 summing to +20 at 13°) look like a high-order fit wiggle. That is an observation, not a correction.

| Model / version | CMMQ | Status |
|---|---|---|
| McAir ARO10 / 1988 F-15 Aerobase table ATAB05 | tabulated data | **not public** (lineage: `F15_A4172_SOURCE_LINEAGE_V0.1.md`) |
| Baumann 1989 SAS fit (App. B COEFF) | the polynomial above | printed |
| Beck 1989 (Model 5) | not held | — |
| **McDonnell 1990** (AFIT/GAE/ENY/90D-16, DTIC ADA230462) | "found the Cmq fit wrong at low α and above 70°, and refit it after comparing against the aero database" (PDF pp.41–42; Fig. 4-2) | **recorded in repository lineage from an earlier reading; not held locally and not re-read here** |
| Davison 1992 App. B / App. C | identical to Baumann | printed |
| Nolan 1992; Fero 1991 | not a CMMQ printing in the held material; Fero substituted NASA Langley rotary-balance data for rate derivatives | lineage record |

**Next source step (not taken; needs download permission).** Read McDonnell 1990 pp.41–42 and Fig. 4-2. It is the one public document known to discuss this fit against the aero database.

## 3. Hypotheses — source-supported only, each tested in isolation

**Isolated test: rescaling the pitch-rate term.** The CMMQ·QB term is scaled by k at the 80 printed turning rows. Those rows are at α 8.30–10.59°, where q ≠ 0 and the term reaches 1.1·10⁻² in Cm. The resulting pitching-moment residual is compared with WP-3A's print floor. Nothing is changed in the model: the term comes from the unchanged routine as cm(q̂) − cm(0).

| Reading (k) | Rows above floor | Largest residual/floor | Verdict |
|---|---|---|---|
| printed (1) | **0 / 80** | 0.89 | consistent |
| sign flip of CMMQ·QB (−1) | 80 / 80 | 1.5·10⁴ | **RULED OUT** |
| no pitch-rate term, e.g. QB never updated (0) | 80 / 80 | 7.7·10³ | **RULED OUT** |
| QB = Q·CWING/VTRFPS, no factor 2 (2) | 79 / 80 | 7.7·10³ | **RULED OUT** |
| QB with 4V (½) | 80 / 80 | 3.9·10³ | **RULED OUT** |
| span instead of chord (b/c̄) | 80 / 80 | 1.3·10⁴ | **RULED OUT** |
| Q in deg/s (57.3) | 80 / 80 | 4.4·10⁵ | **RULED OUT** |
| CMMQ per degree (1/57.3) | 80 / 80 | 7.6·10³ | **RULED OUT** |

**Scope of this test.** The source's own turning equilibria fix the term's sign, normalization and units everywhere. They fix CMMQ's *value* only at α ≤ 10.6°, and the positive band starts at 10.55°. **A value-level difference confined to α ≈ 10.6–14.3° is exactly what Table VII cannot test.** Its symmetric rows have q = 0.

**Every hypothesis:**

| Hypothesis | Verdict | Evidence |
|---|---|---|
| Sign convention of q / CMM | **RULED OUT** | the table above (k = −1) |
| q̂ normalization | **RULED OUT** | k = 2, ½, b/c̄; QB printed explicitly (p.106) |
| Degree vs radian scaling | **RULED OUT** | q in deg/s or CMMQ per degree (table). α fed as degrees makes CMMQ(13) = 3.5·10⁹ instead of 19.76. COEFF defines RAL = AL/DEGRAD with radian breakpoints. |
| Inertia sign convention (Ixz) | **RULED OUT** | at point 47 the unstable pair is an eigenpair of the longitudinal block (α, q, θ, V) to 2·10⁻¹⁵. That block contains no Ixz: q̇ = K8V²CMM + K9PR + K10(R²−P²) with P = R = 0. dq̇/dq = +1.9903 /s equals K8·V·(c̄/2)·CMMQ to FD precision. |
| Actuator-state inclusion (Davison 12-state) | **RULED OUT** | surface states x' = 20(cmd − x), 28(…) feed the airframe and are fed by nothing (CAS off; Davison p.97). The stabilator state appended at point 47 gives a 9×9 spectrum equal to WP-3D's (shift 0.0) plus −20. |
| AUTO's Jacobian froze the coefficients | **RULED OUT** | Baumann FUNC calls COEFF before FUNX for every ±DX (pp.93–94). Davison moved COEFF into FUNX, "Revised 13 Aug 89" (p.91). |
| COMMON /SEIZE/ storage slip | **RULED OUT** | the text layer's "CLM,CCMM,CNM" is OCR. The image reads CX,CY,CZ,CLM,CMM,CNM (p.99). |
| QB built from another state | **RULED OUT** | COEFF sets P = U(3), Q = U(4), R = U(5) (p.100) |
| Davison's diagram at a different condition | **RULED OUT** | his Figure 7 equilibria match Table VII (§1) |
| AUTO executed a coefficient routine other than the printed one | **UNRESOLVED**, with indirect support | both executed presentations lack the α 11–14° instability; Table VII shows no special point there (§4); Davison records version merging; McDonnell refit Cmq. The executed CMMQ itself is not public. |
| Table VII's caption is loose | **SUPPORTED** | it prints the fold saddles (structural, coefficient-independent) and the neutral pitchfork |
| Figures draw the fold saddles solid | **UNRESOLVED** | the saddles are forced by the printed stabilator extrema and confirmed nonlinearly. There is no source explanation of the line style. |

## 4. Where AUTO put its special points

**Method.** For each printed step along a branch, divide its length (in the stabilator–α plane) by the mean of its two neighbours. AUTO inserts the special points it locates, so they appear as short steps.

| Branch | Steps | Ratios |
|---|---|---|
| symmetric (47–50, 1–46) | all | **0.89–1.11**; 8→9, where the printed CMMQ puts the Hopf: **1.000** |
| turning (117–201) | folds and fork | 123→124 **0.21**; 135→136 **0.32**; 164→165 **0.46**; 181→182 **0.47**; 193→194 **0.32** |
| | printing seam, not a special point | 172→173 0.09 and 174→175 0.24 |

**Reading.** Every located fold and the fork left a short step, but the symmetric branch is uniform across the printed-CMMQ Hopf. That suggests the executed run located no Hopf there.

**Weight.** This is indirect evidence. AUTO's output rules for Hopf points are not printed, and Baumann notes Hopf points can be missed at large steps (p.58). The eigenvalue there moves cleanly from +0.115 to −0.031 between adjacent printed points, so the step size alone would not hide it.

## 5. Table VII's caption

**The caption cannot be read strictly.**
- **Printed saddles:** Table VII prints the fold-bounded segments 125–135 and 183–193. A stabilator extremum along a smooth branch forces a real eigenvalue through zero, whatever the coefficients. WP-3D found these segments unstable, and WP-3E confirms it nonlinearly (e.g. point 130 grows at +0.01165 /s).
- **Neutral pitchfork:** it prints the pitchfork 165, which is neutral.

**So "stable" is a label for the printed segments of the plotted branches, not a per-row property.**

## 6. What would settle it

1. **Read McDonnell 1990 pp.41–42 and Fig. 4-2** (DTIC ADA230462, public; needs download permission). It compares the Baumann Cmq fit with the aero database.
2. **Pseudo-arclength continuation (D16)** of the printed model. Check whether AUTO's own step logic would flag Hopf points at α 11.0° and 14.2°, and reproduce the full solid/dashed diagram for direct comparison with Figures C-1…C-8.
3. **Any executed-code artifact** (an AUTO Fort.9, a later D2ICC listing). None is public in the held material.

**Until then, the research model stays as printed.** Its α 11–14° symmetric equilibria are **unstable in the source model as published**, and they are labelled that way.
