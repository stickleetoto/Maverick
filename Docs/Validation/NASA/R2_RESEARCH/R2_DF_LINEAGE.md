# R2_DF_LINEAGE — Where D-F1 … D-F4 come from (Morelli 1998 polynomial vs NESC F-16 tables)

Research only; no production code was changed. Date: 2026-11-18.

Tags: **VERIFIED** means I read it in the named source at the named page or section, or I reproduced it numerically with the script named. **INFERENCE** means my reasoning from verified facts.

Scripts (committed under `Tools/nasa_6dof/research/`, reproducible, Python 3 + numpy/scipy, run with `-I`):
`Tools/nasa_6dof/research/scripts/`
(`refit.py`, `cx_probe.py`, `cx_grid.py`, `cx_perm.py`, `cx_bp.py`, `cx_mof.py`, `cz_probe.py`, `cz_mof.py`, `cl_check.py`, `cl_mof2.py`, `cnda.py`, `tab3_compare.py`, `trim_attr.py`).
Extracted source text (not committed; regenerate from the cited PDFs): (Morelli 1998 NTRS text, the IEEE/ACC scan OCR, Garza & Morelli 2003, NESC Vol II pp. 18/19/611, README hits).

---

## 0. Headline result

The question was whether each difference is (a) a fit or representation error, (b) a dataset difference, or (c) a transcription error. I answered it without the Stevens & Lewis book by **refitting Morelli's published model structures (his Table 2) to the NESC `F16_aero.dml` table values with his own published method.** The refit gives back his Table 3 coefficients to about 3 × 10⁻⁷ relative, which is the 7-significant-figure print precision:

| Function | Data used (NESC table) | Method that reproduces Table 3 | Max rel. error vs Table 3 |
|---|---|---|---|
| Cz(α) quartic, f0–f4 | CZ0, 12 α points | orthogonal functions in order 1, α, α², α³, α⁴; the **α³ orthogonal function dropped**, the other four kept and expanded | 3.4 × 10⁻⁷ |
| Cn(α,β), o0–o6 | Cn0, α 12 × **β 0…30 (7)**, i.e. the |β| half-table | plain least squares, all 7 terms | < 1 × 10⁻⁶ (all 7 digits) |
| Cx(α,δe), a0–a6 | CX, α 12 × δe 5 rows, **outer rows labelled ±25° (not ±24°)** | plain least squares | 3.5 × 10⁻⁷ |
| Cm(α,δe), m0–m7 | Cm0, same grid, **outer rows labelled ±25°** | plain least squares | 2.4 × 10⁻⁷ |
| Cl(α,β), h0–h7 | Cl0, |β| half-table | orthogonal functions, **β² and α³β functions dropped** | 3.6 × 10⁻⁷ |
| Cnδa(α,β), r0–r9 | dnda / (20° in rad), β −30…30 | orthogonal functions, **β, α², β³ functions dropped** | 4.0 × 10⁻⁷ |

(VERIFIED, scripts `refit.py`, `cz_mof.py`, `cx_bp.py`, `cx_mof.py`, `cl_mof2.py`, `cnda.py`.)

The "drop an orthogonal function but keep its regressor in the expansion" behaviour is exactly the procedure Morelli describes. He selects orthogonal modelling functions by PSE, then expands each retained function into ordinary polynomials and combines common terms (Morelli 1998 §2, p. 2, Eqs. 7–11 and the paragraph after them; VERIFIED). His text also says each component function was modelled from "tabulated values of that function" (p. 3; VERIFIED).

**What follows:**
- **The data Morelli fitted in 1998 has the same numbers as the NESC check-case tables** for CX, CZ, Cm, Cl, Cn and Cnδa. The probability of reproducing 7 to 10 coefficients to 7 digits from different data is nil (INFERENCE, very high confidence).
- So D-F1, D-F2 and D-F3 are **not (b)-type differences in the numbers**, and **not (c)-type typos** in the published Table 3 or in Maverick's typing of it.
- They are **(a) fit/representation errors** of the compact global polynomial against the same table. Two representation choices sit on top of that: the δe ±25 labelling, and fitting Cl and Cn on β ≥ 0 only.

---

## 1. Lineage chain

```
Low-speed (M 0.1–0.2) static + forced-oscillation tunnel tests, 16 % scale F-16
  │
NASA TP-1538, Nguyen et al., Dec 1979  (static α −20…90°)
  │                                                   [Morelli 1998 ref. 3; G&M 2003 ref. 9]
Stevens & Lewis 1992, App. A pp. 584–92: "slightly simplified version", α −10…45°
  │        (FORTRAN model, subsonic)                  [Morelli 1998 p. 3 + ref. 2; NESC Vol II §B.1.5]
  ├──► Morelli 1998 ACC polynomial (Tables 1–3), fitted to the tabulated S&L values
  │        └──► Maverick MavF16MorelliPolynomial (all 110 Table 3 values match, §3)
  │
  └──► Morelli f16_aero.m (created 05 Jun 1995; checkcase script dated 09 Oct 2000)
           └──► Garza & Morelli, NASA/TM-2003-212145 (MATLAB sim collection)
                    └──► Jackson, F16_aero.dml (created 2003-06-10, Mod P 2013-10-21)
                             └──► NESC TM-2015-218675 check cases (Vol II §B.1.5)
```

| Link | Evidence | Tag |
|---|---|---|
| TP-1538 → S&L App. A | Morelli 1998 p. 3 says the data was "a slightly simplified version" (ref. 2 = S&L App. A pp. 584–92) of the original tunnel database (ref. 3 = TP-1538). | VERIFIED (NTRS PDF p. 3, refs p. 4) |
| S&L → Morelli 1998 | Same passage. Each component function was modelled from its tabulated values over the Table 1 ranges, which "represent all the data available". Table 1 gives δe −25…25° (±0.4363 rad), δa ±21.5°, δr ±30°, β ±30°, α −10…45°. | VERIFIED (p. 3; Table 1 p. 4; the same ±25° appears in the IEEE/ACC scan) |
| S&L + TP-1538 → Garza & Morelli 2003 | G&M report p. 29 (PDF p. 44): aero data from 16 % model tests, refs 3 (S&L) and 9 (TP-1538). Static tables α −10…45°, β ±30°. "The aerodynamic database was simplified slightly by dropping second order dependencies (e.g., dependence of longitudinal aerodynamic forces on sideslip angle)". Elevator limited to ±25°. Engine power dynamics and gyroscopic effects cite ref. 9. | VERIFIED |
| Morelli f16_aero.m → DAVE-ML | `F16_aero.dml` header: "Based on Morelli's adaptation of Stevens and Lewis' F-16 example [1] described in Garza & Morelli's TM [2]. Obtained from E. A. Morelli in the form of Matlab scripts [3] & [4]". REF03 = `f16_aero.m` (1995-06-17). REF04 = `f16_aero_setup.m` (1995-06-17), by Morelli. NOTE1: check cases from "E. A. Morelli's f16 matlab script, dated 09-Oct-2000". The embedded script history says "05 June 1995 - Created". All basic tables cite REF01–REF03. | VERIFIED (dml lines 6–40, 190–240, 976–1150) |
| → NESC 2015 | Vol II §B.1.5 (report p. 14 / PDF p. 18): S&L "provided a FORTRAN model"; "Garza and Morelli [3] expanded this model with additional wind-tunnel data from another NASA report [4]" (= TP-1538). Converted from the G&M Matlab scripts into S-119. | VERIFIED |
| README | "This model is based on [TM212145] by Garza and Morelli, which implements aero data published in [TP1538] by Nguyen." | VERIFIED (README.html, F16_aero.dml section) |

**About NESC's "expanded with additional wind-tunnel data".** The refits in §0 show that the six static aerodynamic tables NESC ships carry exactly the values Morelli fitted from S&L in 1998. Whatever G&M added from TP-1538 therefore did not change these tables. Candidates are the engine and gyroscopic model, which G&M cite to ref. 9, and possibly some damping tables (INFERENCE; the damping tables were not refitted).

**Note on S&L editions.** Morelli 1998 cites the 1992 first edition. NESC Vol II ref. [2] and the README cite the 2003 second edition (VERIFIED p. 607 / PDF p. 611). This does not matter for D-F1…D-F3 because the refit pins the numbers directly.

---

## 2. Attribution of each difference

### D-F1: CX(α 5°, δe 0) is +0.003929 in Morelli and −0.004 in NESC

- **Attribution: (a) fit/representation. Confidence high.**
- The 7-term Cx model is cubic in α. It cannot follow the table shape between α 0° and 15°: the table has −0.021, −0.004, +0.032, +0.094 at α 0, 5, 10, 15°, while Morelli gives −0.0194, +0.0039, +0.0343, +0.0682 (VERIFIED, `refit.py`). Fit RMS over the 60-point grid is 0.0117 and the maximum residual is 0.025, so an error of 0.008 at 5° is well inside the fit band.
- A secondary dataset-labelling effect is present. Table 3's Cx coefficients come out exactly only if the δe = ±24° rows of the table are placed at **±25°**. Labelled at ±24° (as in S&L/NESC, breakpoints DE1 = −24, −12, 0, 12, 24), the same least squares gives a0 = −0.018639 and a different δe², δe and αδe (+10 %, +4 %, +3.5 %). The α, α² and α³ terms are identical. (VERIFIED, `cx_probe.py`, `cx_perm.py`, `cx_bp.py`: a free solve for the four outer breakpoints converges to −25.0000, −11.9999, 12.0001, 25.0000.)
- At α 5°, δe 0, that relabelling moves CX by only −0.0008 (+0.0047 → +0.0039), which is *toward* NESC. **The relabelling does not cause D-F1.** It is a property of Morelli's dataset representation (type (b)-like, but a breakpoint label, not different numbers). It matters at large |δe|: at α 0°, δe −24° the table gives −0.081, Morelli −0.069, and a ±24 fit −0.073 (VERIFIED, `trim_attr.py`).
- Why ±25: Morelli's Table 1, and G&M p. 29, give the elevator range as ±25°, the deflection limit, while the S&L/NESC table rows stop at ±24° (INFERENCE: he assigned the outer rows to the stated range limit).

### D-F2: CZ(α 5°) is −0.475307 in Morelli and −0.416 in NESC

- **Attribution: (a) fit/representation. Confidence very high.**
- The data is the 12-point NESC CZ0 table, identical numbers.
- Table 3's f0…f4 equal the orthogonal-function model with the α³ orthogonal function **rejected by PSE** and the other four kept. Expanding the kept α⁴ function brings α³ back into the ordinary polynomial, so the final quartic is *not* the least-squares quartic (VERIFIED, `cz_mof.py`, 3.4 × 10⁻⁷).
- At α 5° the full least-squares quartic would give −0.4364, an error of −0.020. The PSE-reduced model gives −0.4753, an error of −0.059. About two-thirds of D-F2 comes from the structure-selection step, and the rest from quartic truncation. Residuals of Morelli's Cz against the table are −0.038 to −0.059 at α 0–10° and +0.028 to +0.042 at α 20–30° (VERIFIED, `refit.py`).
- f5 = −4.354000e−01 matches the S&L analytic term −0.19·δe/25 per degree (= −0.4354479 /rad) to 4 significant figures only. It was evidently entered or rounded rather than fitted. The effect is negligible: < 2 × 10⁻⁵ at 25° (VERIFIED arithmetic; INFERENCE on why).

### D-F3: the Cn_β zero crossing is ≈ 25° in Morelli and ≈ 31° in NESC; at α 30° Morelli gives −0.0021/deg and NESC +0.0008/deg

- **Attribution: (a) fit/representation. Confidence very high.**
- Ordinary least squares of the 7 Table 2 terms on the NESC Cn0 half-table (α −10…45, |β| 0…30, 84 points) returns **all seven** o0–o6 to 7 digits (VERIFIED, `refit.py`). Same data, same model, so the difference is the polynomial's inability to follow the table.
- RMS residual is 0.0089 and the maximum 0.031. At β = 5° the table and Morelli values are:

  | α | Table | Morelli |
  |---|---|---|
  | 25° | +0.007 | +0.0011 |
  | 30° | +0.004 | −0.0080 |
  | 35° | −0.014 | −0.0179 |

  Morelli's Cn_β at β → 0 changes sign at α ≈ 24.9°: +0.0009/deg at 22.5° and −0.0021/deg at 30° (VERIFIED, `refit.py`).
- **Additional representation property (new finding).** Cn and Cl were fitted only on β ≥ 0, because S&L/NESC store |β| tables and apply sign(β) (dml variable `cnt`: `−absCn0` if β < 0; VERIFIED). Their Table 2 structures therefore contain even-in-β terms (β², αβ², α²β²). For β < 0 the polynomial is **not antisymmetric**. Examples:
  - α 45°, β −30°: Morelli Cn = **+0.576**, NESC +0.001. At β +30°: Morelli +0.012.
  - α 0°, β −30°: −0.212 vs −0.079.

  (VERIFIED arithmetic.) This explains the "unresolved" Cl and Cn rows in Maverick's `F16_STATIC_AERO_REPORT.md` §3, whose worst cases are at β −30°. Morelli's Table 1 nevertheless states β −30…30° (INFERENCE: the published model is only valid for β ≥ 0, or must be applied as sign(β)·f(α,|β|); this is a property of the publication, not Maverick's typing).

### D-F4: case-11 trim (α −0.73°, δe −0.82°, thrust −27 %)

- **Attribution: consequence of (a)** through CZ (D-F2), CX and Cm.
- At the NESC trim point α 2.639°, δe −3.233°:

  | Coefficient | Table | Morelli | ±24-labelled refit |
  |---|---|---|---|
  | CX | −0.0169 | −0.0084 | −0.0077 |
  | Cm | +0.0242 | +0.0161 | +0.0160 |

  The δe-relabelling contributes < 0.001 here (VERIFIED, `trim_attr.py`). The 27 % thrust deficit is the CX representation error (about 0.0085 less axial drag) plus the higher lift slope, which lowers α (INFERENCE consistent with the numbers).

---

## 3. Transcription cross-check

1. **Maverick vs the published Table 3.** All 110 coefficients in `Maverick-nasa-6dof/.../F16/MavF16MorelliPolynomial.cs` match the NTRS text layer of Morelli 1998 Table 3 (p. 5) in value and sign, in letter order a0…s5. The term ordering per block follows Table 2 (p. 4); I checked it by eye for Cx, Cz, Cn and Cnδa. (VERIFIED, `tab3_compare.py`.)
2. **Table 3 typo in the paper itself.** r8 is printed as "1.588105–01", with the "e" missing, in both the NTRS text layer and the ACC scan. Maverick uses +1.588105e−01. The refit of Cnδa from the NESC `dnda` table reproduces r8 = +0.1588105 (4 × 10⁻⁷), so the reading e−01 is correct (VERIFIED, `cnda.py`).
3. **Independent check from data.** The refits in §0 reproduce 41 of the Cx, Cz, Cm, Cl, Cn and Cnδa coefficients to print precision from NESC data. That rules out a typo in any of them, in the paper or in Maverick (VERIFIED). Cy (c0…c2) equals the S&L linear terms −0.02/deg, 0.021/20°, 0.086/30° converted per radian, to 7 digits (VERIFIED arithmetic).
4. **Second copy of the paper.** The IEEE/ACC Proceedings scan on Zenodo (OCR) agrees where legible: −3.348641e−03, −1.026225e+01, −4.354000e−01, −6.233977e…, −2.107885e+00, 2.141420e+00, 8.476901e−01. One OCR reading, "2.992363e−01" for o0, conflicts with the NTRS 2.993363e−01. The data refit gives exactly 2.993363e−01, so this is an OCR artefact or a print defect in the scan, and NTRS is correct (VERIFIED for the refit; whether the printed ACC page truly reads 2.992363 is unresolved, it is a low-quality OCR).
5. **Independent third-party implementations.** I found none that list Table 3 legitimately (search results only returned the paper, a Julia package based on TP-1538 tables, and AeroBench, which is excluded by policy). Not needed given item 3.

---

## 4. What would resolve the remaining uncertainty

| Remaining question | Status | What would settle it | Download or purchase? |
|---|---|---|---|
| D-F1…D-F3 attribution | **Settled**: (a), with identical data | — | none |
| Do the S&L 1992 App. A printed tables equal the NESC tables? | Not inspected. Moot for D-F1…D-F3, since Morelli's fitted values are proven equal to NESC's | Look at S&L 1st ed. pp. 584–92 (CXO, CZO, CNO tables and the elevator index EL/12) | Book purchase or library loan; copyrighted, so quote only single values |
| Why the outer δe rows were labelled ±25° in the Cx/Cm fits | INFERENCE: range-limit labelling | Morelli's original fitting script or data (SIDPAC examples, or a direct query to the author) | SIDPAC is NASA software, distributed by request or licence. User approval needed |
| Whether TP-1538 values differ from the S&L tables at the D-F points (e.g. CX α 5°, δe 0) | Not checked here | Read TP-1538 Table III (repository manifest already holds the scan, SHA `aae0ece6…`) | No new download; transcription effort only |
| Damping tables (Cxq…Cnp) and Clδa, Clδr, Cnδr | Not refitted | Same refit approach on the remaining NESC tables | none |

**Practical implication (INFERENCE).** No change to Maverick's typed coefficients is warranted. D-F1…D-F4 are faithful properties of the published polynomial. If a closer-to-table model is wanted, the NESC tables are the same dataset and can be used directly as a selectable validation model. If Cl and Cn are kept as polynomials, applying them as sign(β)·f(α,|β|) would remove the negative-β asymmetry. That is a model change and needs a decision.

---

## 5. Sources

| Source | Location used | URL / path |
|---|---|---|
| Morelli, E. A., "Global Nonlinear Parametric Modeling with Application to F-16 Aerodynamics", ACC 1998 (NTRS 20040110310) | p. 1 abstract (< 10 % doublet); p. 2 §2 Eqs. 1–11 and the expansion paragraph; p. 3 §3 data source, Eqs. 12–18, "tabulated values"; p. 4 refs, Table 1, Table 2; p. 5 Table 3 | https://ntrs.nasa.gov/api/citations/20040110310/downloads/20040110310.pdf |
| Same paper, IEEE/ACC Proceedings scan (DOI 10.1109/ACC.1998.703559) | Table 1 (±25°), Table 3 (OCR) | https://zenodo.org/record/1262949/files/article.pdf |
| Garza & Morelli, NASA/TM-2003-212145 (NTRS 20030013626) | report p. 29 (PDF p. 44) F-16 data source, simplification, limits; PDF p. 45 build-up equations; refs 3 and 9 | https://ntrs.nasa.gov/api/citations/20030013626/downloads/20030013626.pdf |
| NASA/TM-2015-218675 Vol II (NESC-RP-12-00770) | §B.1.5 report p. 14 (PDF p. 18); Table 7 PDF p. 19; refs report p. 607 (PDF p. 611) | local `E:\unity project\_nesc_source\NASA-TM-2015-218675-EOM_checkcase_appendices.pdf`; NTRS 20150001264 |
| NESC `F16_aero.dml` (Mod P, 2013-10-21) | header and refs lines 6–40; embedded f16_aero.m lines 190–285; breakpoints lines 952–970; CX/CZ/Cm/Cl/Cn tables lines 976–1150; dnda lines 1481–1520; `cnt` sign logic lines 639–663 | local `E:\unity project\_nesc_source\extracted\f16\F16_package\F16_S119_source\F16_aero.dml` |
| NESC F-16 package README | F16_aero.dml section; references | local `...\F16_package\README\README.html` |
| Maverick implementation and report | coefficient values; D-F definitions | `E:\unity project\Maverick-nasa-6dof\Assets\MaverickFresh\Scripts\FlightDynamics\F16\MavF16MorelliPolynomial.cs`; `...\Docs\Validation\NASA\F16_STATIC_AERO_REPORT.md` |
| Prior audit | lineage diagram, R1–R3 | `E:\unity project\Maverick-instructor-research-p2\Docs\Research\InstructorV2\Phase2\01_F16_AERODYNAMIC_SOURCE_AUDIT.md`, `09_RESEARCH_SOURCES.md` |
| Stevens & Lewis 1992 App. A | **not inspected** | book (ISBN 0-471-61397-5) |

No JSBSim or AeroBench material was used. Table values quoted above are single numbers from the public-domain NASA files.
