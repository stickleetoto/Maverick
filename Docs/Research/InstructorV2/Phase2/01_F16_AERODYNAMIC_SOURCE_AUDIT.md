# 01 — F-16 Aerodynamic Source Audit

**Scope:** the public F-16 aerodynamic data and models, their real validity domains, what Maverick implements, and which gaps block realistic fighter manoeuvring.

**Configuration identity.** Maverick's F-16 is `F16-CFG-NASA-REF-TP1538` with aerodynamics `F16-CFG-SIM-MORELLI1998`: a 1979 NASA Langley simulation configuration of a development-era F-16 with relaxed static stability. It is not a production block. Nothing below may be read as "F-16C data".

**Evidence classes:** PV (read on the primary page; "lead" = by the lead researcher, otherwise by the named sub-agent), SV, EI, UV, MP. Repository evidence: REPO_VERIFIED / REPO_DOC. Sources: doc 09.

---

## 1. Lineage, corrected by page reading

```text
Low-speed (M 0.1–0.2) static + forced-oscillation tunnel tests, subscale F-16 models, NASA Ames and Langley
        │
NASA TP-1538 (Nguyen et al., Dec 1979), App. B + Table III                       [lead PV, PDF pp. 15, 43–46, 51]
  static tables α −20…90°, β ±30°; dynamic tables β = 0 over the same α;
  LEF (1 − δlef/25) and speed-brake (δsb/60) increments; no Mach, Reynolds or aeroelastic effects
        │
Stevens & Lewis App. A: "slightly simplified version" (α −10…45° grid)           [Morelli 1998 §3, ref. 2: lead PV]
        │                                     │
Morelli 1998 compact global polynomial      Garza & Morelli NASA/TM-2003-212145 lookup tables  [sub-agent PV]
  (Tables 1–3)  [lead PV]                       │
        │                                     NESC F16_aero.dml (α −10:5:45 grid)               [library file-verified; README SV]
Maverick MavF16MorelliPolynomial
```

Three corrections to the repository record follow from the pages:

| # | Repository statement | What the pages show | Class |
|---|---|---|---|
| R1 | "Morelli fit to the TP-1538 16 %-scale database" | Morelli fitted Stevens & Lewis's *simplified* version of that database; his Table 1 ranges are "all the data available" **in that simplified set** | lead PV (Morelli p. 3) |
| R2 | "Domain α −10…45°" as a property of the F-16 data | α −10…45° is the simplified set's range. **TP-1538 itself tabulates α −20…90°** (static) and the same α range for dynamic derivatives at β = 0 | lead PV (TP-1538 p. 9 / PDF 15; p. 37 / PDF 43; Table III PDF 51) |
| R3 | "LEF fixed at 25°" | TP-1538 models the LEF explicitly through increment terms scaled by (1 − δlef/25), and its LEF is scheduled with α and q̄/p_s (Phase 1 doc 02 §1.1). **Neither Morelli 1998 nor Garza & Morelli states which LEF position the simplified tables represent.** The repository's wording should become "LEF position not stated by the fitted source" | lead PV (build-up equations, PDF 43–44); sub-agent PV for the Morelli/TM absence |

**Mach statement.** TP-1538 says the data came from low-speed tests at M = 0.1 to 0.2, and that Mach, Reynolds-number and aeroelastic effects were not included (lead PV, PDF p. 15). The sub-agent also read a later statement that results can be considered valid only below about Mach 0.6 (PDF p. 38; sub-agent PV). **The "M < 0.6" limit is therefore a validity judgement by the authors on M 0.1–0.2 data, not a tested range.**

---

## 2. What Maverick implements

| Item | Implementation | Evidence |
|---|---|---|
| Aero model | Morelli 1998 polynomial, six coefficients, regressors in rad and nondimensional rates (`p̃ = pb/2V, q̃ = qc̄/2V, r̃ = rb/2V`) | `F16/MavF16MorelliPolynomial.cs`; coefficient audit `F16/F16_MORELLI_COEFFICIENT_AUDIT_V0.1.md` (REPO_DOC); structure lead-PV against Morelli Eqs. 12–18 |
| Input envelope | `clampToPublishedInputEnvelope = true` by default: α, β, δe, δa, δr **clamped** to Morelli Table 1; clamping flagged in `debugInputWasClamped` | `F16/MavF16AeroModel.cs:16, 64–78` (REPO_VERIFIED) |
| Mach | above `MaxReferenceMach = 0.6` the aero is **still evaluated**; only `debugOutsidePublishedMachRange` is set | `MavF16AeroModel.cs:101`, `MavF16MorelliReference.cs:34` (REPO_VERIFIED) |
| CG transfer | Cm and Cn include the `Cz (xcg_ref − xcg)` and `Cy (xcg_ref − xcg) c̄/b` terms; reference 0.35 c̄, default 0.25 c̄ | Morelli Eqs. 16–17 (lead PV); repository coverage matrix |
| LEF, speed brake, differential tail | not consumed | `FlightDynamics/README.md` (REPO_DOC) |
| Mass, inertia, geometry | TP-1538 Table I: 20 500 lb; 9 496 / 55 814 / 63 100 / Ixz 982 slug·ft²; S 300 ft², span 30 ft, c̄ 11.32 ft; reference CG 0.35 c̄ | lead PV (TP-1538 p. 43 / PDF 49) |
| Surface limits | δh ±25°, δd ±5.375° per surface, δa ±21.5°, δr ±30°, LEF 25°, speed brake 60° | lead PV (same page) |

**Consequence of the clamp (EI):** at α > 45° the polynomial returns its 45° values for every α. That is a silent saturation, not post-stall aerodynamics. The flag makes it observable, but nothing stops a manoeuvre from flying there. For the instructor this means **the α domain gate must act on commands (law or guidance soft cap), and validation must count clamped steps** (doc 06 metric M13).

---

## 3. Coverage matrix

| Parameter | Existing Maverick coverage | Public source | Verified domain | Missing domain | Confidence |
|---|---|---|---|---|---|
| Lift / normal force (Cz) | Morelli: quartic in α × (1 − β²) + δe + Czq(α) | Morelli Tables 2–3; TP-1538 Table III | α −10…45, β ±30, δe ±25 (Morelli); α −20…90 (TP-1538 static) | α > 45 / < −10 (data exist in TP-1538, not implemented); Mach effects (no source) | High in-domain |
| Drag / axial force (Cx) | Cx(α, δe) + Cxq(α); **no β term** | Morelli; Garza & Morelli notes β dependence of longitudinal forces dropped in the simplification | same | β effect on Cx (exists in TP-1538 tables) | High in-domain; known simplification |
| Side force (Cy) | linear β, δa, δr + Cyp(α), Cyr(α) | Morelli | same | nonlinear β; α > 45 | Medium |
| Pitching moment (Cm) | Cm(α, δe) + Cmq(α) + CG transfer; **no β term** | Morelli | same | Cm(β), deep-stall ΔCm terms (TP-1538 build-up has ΔCm(α) and ΔCm,ds(α, δh), lead PV report p. 38 / PDF p. 44) | High in-domain; post-stall missing |
| Rolling moment (Cl) | Cl(α, β) + Clp + Clr + Clδa(α, β) + Clδr(α, β) | Morelli | same | α > 45; differential-tail roll power | Medium–high |
| Yawing moment (Cn) | analogous + CG transfer | Morelli | same | same | Medium–high |
| Dynamic derivatives | Cxq, Cyp, Cyr, Czq, Clp, Clr, Cmq, Cnp, Cnr vs α; α̇ effects folded into q terms | Morelli p. 3 (lead PV); TP-1538 forced-oscillation data | α −10…45 (Morelli); −20…90 at β = 0 (TP-1538) | rate derivatives at β ≠ 0; separated α̇ | Medium |
| Mach dependence | **none** (flag only above 0.6) | TP-1538: not modelled (lead PV); no public tabulated production-F-16 transonic/supersonic database found (sub-agent: CR-3053/CR-158922 developmental configurations, plots; TP-3355 F-16C at M 1.6–2.16 tabulated only in a microfiche supplement) | M 0.1–0.2 tested; "valid < ~0.6" (authors' judgement) | everything above ~0.6 | **None** above 0.6 |
| α dependence | polynomial in α | Morelli; TP-1538 | −10…45 | 45…90 (data exist), < −10 (data to −20) | High in-domain |
| β dependence | polynomial in β | same | ±30 | — | Medium |
| Control effectiveness | δe in Cx/Cz/Cm; δa, δr in Cy/Cl/Cn as functions of (α, β) | same | δe ±25, δa ±21.5, δr ±30 | LEF, speed brake, differential tail | Medium |
| Reynolds / configuration | subscale, low-speed tunnel; gear up, no stores, out of ground effect | Morelli p. 3; TP-1538 p. 9 | that configuration only | ground effect, stores, gear | — |
| Transonic effects | none | none public for this configuration | — | all | none |
| Supersonic | none | TP-3355 (F-16C, M 1.6–2.16, α −4…20) exists but tables are microfiche-only; different configuration (F-16C vs 1979 simulation) | — | all | none |
| Stall / post-stall | stall to 45° in the fit; clamp above | TP-1538 Table III to 90°; DTIC ADA327869 (Bihrle VISTA, α −80…90 per abstract) **not accessible** (HTTP 403) | to 45 (Maverick); to 90 (TP-1538 data) | 45…90 in Maverick; spin/departure validation data | Medium for the data, none for Maverick above 45 |
| Mass / CG / inertia | TP-1538 Table I constant mass | TP-1538; Garza & Morelli Table 1 (same values) | one loading | fuel burn, stores | High (one loading) |

---

## 4. Highest-priority missing data for realistic manoeuvring

| Rank | Missing | Why it matters for fighter manoeuvring | Source status |
|---|---|---|---|
| 1 | **α 45…90° (and −20…−10°)** | post-stall entry, departure, high-α roll coupling, "cobra"-type requests; currently a silent clamp | **Available**: TP-1538 Table III (public NASA report, scanned tables). Needs a verified transcription |
| 2 | **Mach > 0.6** | all realistic fighter combat speeds; compressibility changes lift slope, stability and control power | **Not available** for this configuration. Blocked |
| 3 | LEF effects | high-α lift and lateral stability depend on the LEF schedule | Available in TP-1538 (increments to α 45 only, schedule in Phase 1 doc 02 §1.1); ambiguous base state (R3) |
| 4 | β effects on Cx, Cm | sideslip drag and pitch in uncoordinated manoeuvres | Available in TP-1538 tables |
| 5 | Mass variation (fuel, stores) | energy and turn performance change through a sortie | Not available |

---

## 5. Candidate model improvements

### 5.1 TP-1538 Table III tabular model (the main candidate)

**What it is (lead PV):** the coefficient build-up of TP-1538 App. B (PDF pp. 43–46), e.g. for the X-axis force

```text
C_X,t = C_X(α, β, δh) + ΔC_X,lef (1 − δlef/25) + ΔC_X,sb(α) (δsb/60)
        + (c̄ q / 2V) [ C_Xq(α) + ΔC_Xq,lef(α) (1 − δlef/25) ]
with ΔC_X,lef = C_X,lef(α, β) − C_X(α, β, δh = 0°)
```

and analogous forms for C_Z, C_m (including the CG transfer `C_Z,t (x_cg,ref − x_cg)`, `ΔC_m(α)` and `ΔC_m,ds(α, δh)`), C_Y, C_l and C_n with aileron (20°) and rudder (30°) increments. Static tables are functions of α and β at several δh values (Table III shows C_X at δh = −25° with α rows −20…90 and β columns −30…+30, PDF p. 51). Units: angles in degrees in the tables, moments about 0.35 c̄.

**What it gives:** the same configuration Maverick already uses, extended to α 90° and the full β grid, with LEF and speed-brake terms and the deep-stall moment terms.

**What it does not give:** any Mach effect; LEF increments above α 45°; validation data beyond the 1979 simulation's own results.

**Effort and risk:**
- Table III spans roughly PDF pp. 51–97 of rotated, scanned, 5-digit tables (lead observation of the page images). The repository's Table VI transcription standard (two independent visual passes at ≥ 400 dpi, zero disagreements, cross-checks) must be applied to every cell; OCR is not acceptable as authority.
- Internal checks available: continuity of each table, consistency between δh tables, and agreement with the Morelli polynomial inside α −10…45 (expected within Morelli's < 10 % fit band, and closer to the S&L simplified tables).
- The deep-stall and LEF terms need the LEF schedule; TP-1538 gives it, but which LEF state the base tables represent must be pinned on the pages before use.

**Label:** "NASA TP-1538 low-speed tabular aerodynamics (1979 simulation configuration), α −20…90°, no Mach effects". Valid only as low-speed post-stall research data.

### 5.2 Morelli fit kept as the default inside α −10…45°

Keep the frozen polynomial as the baseline (its regression vectors and coefficient audit are in place). A TP-1538 tabular model would be a separate, selectable aero model with its own validation suite, never a silent replacement.

### 5.3 NESC tables as a validation-only aero model

The NESC `F16_aero.dml` tables (library file-verified) are the S&L simplified tables. Implementing them as a validation-only model (doc 07, L2/L3) gives a direct check of the Morelli polynomial against its own data source.

### 5.4 What not to do

- Do not extrapolate the polynomial above α 45° or Mach 0.6 and call it valid.
- Do not borrow transonic/supersonic data from another F-16 configuration (F-16C tunnel model, developmental YF-16/401F configurations, F-16XL, Falcon 21) into this configuration.
- Do not use JSBSim's F-16 file (repository policy; GPL-tagged; same lineage).

---

## 6. Validation requirements for any F-16 aero change

| Requirement | How |
|---|---|
| Source identity | SHA-256 of the TP-1538 PDF (`aae0ece6…`, matches the repository manifest); page and table for every transcribed cell |
| Transcription quality | two independent visual passes; zero unresolved disagreements; cell-level manifest; validator script (as for Table VI) |
| Internal consistency | table continuity; LEF/no-LEF and δh tables coherent; symmetric β behaviour where physics requires it |
| Cross-model check | inside α −10…45: TP-1538 tables vs Morelli polynomial vs NESC tables at the same inputs; report per-coefficient differences |
| Regression | existing F-16 suites (Baseline v1, `MavF16ReferenceValidation`, TP-1538 runtime validation) unchanged with the new model off |
| Behaviour | TP-1538's own reported departure/deep-stall characteristics (abstract and pp. 18–19, Phase 1 doc 02 §1.4) as qualitative validation, not as tuning targets |
