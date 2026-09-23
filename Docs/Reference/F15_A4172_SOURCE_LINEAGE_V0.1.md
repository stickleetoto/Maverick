# F-15 MDC A4172 — Source Lineage V0.1

**Question:** Can the McDonnell F-15 stability-derivative / mass-and-inertia report MDC A4172 be acquired from a public source? If not, what do public documents reproduce from it, how far can each reproduction be trusted, and does any of it close NASA 836's `S` / `c̄` / `b`?
**Code:** `MavF15SourceLineage.cs` (lineage × scope axes, `MavF15McDonnellSources`) · `MavF15ReferenceGeometrySources` (every candidate now carries both axes) · `MavF15ReferenceGeometryValidation` `[G8]`

---

## 1. Result

| | |
|---|---|
| **MDC A4172 original (any part)** | **NOT FOUND.** No public original of Part I, Part I Supplement 1 or Part II was located. |
| Public reproductions | **Found.** Seven public AFIT theses (1989–1996) cite it. Six of them print F-15 physical, reference or datum data that they attribute to it in whole or in part. |
| Part II aero database recoverable? | **No.** No public document reproduces A4172 Part II tables. The public aerodynamic lineage is a *different* McDonnell product — the ASD F-15 SPO "F-15 Aerobase" / McAir ARO10 simulator tables — curve-fitted at a single flight condition. |
| NASA 836 `S` / `c̄` / `b` | **Did not move.** All stay 0. No 836 source links to any of these values. |
| Strongest new finding | AD-A244044 publishes McDonnell's F-15 reference datum and a %MAC equation **"for all A through D models of the F-15"**: `%MAC = (FS − 508.1) / 191.33 × 100`. It reproduces NASA 836's own 28 % MAC = FS 561.7 statement to 0.03 in. It is family support, not an 836 link — see §7. |

---

## 2. Bibliographic identity, as cited

| Citing document | Part cited | Title / identifiers as printed | Date |
|---|---|---|---|
| Baumann, AFIT/GAE/ENY/89D-01, DTIC ADA217366 — bibliography ref. 7, PDF p.138 | **Parts I and II, Rev. C** | "F/TF-15 Stability Derivatives, Mass and Inertia Characteristics Flight Test Data Basis. Report No. MDC A4172 Parts I and II, Rev. C" | August 1976 |
| McDonnell, AFIT/GAE/ENY/90D-16, DTIC ADA230462 — ref. 23, PDF p.120 | **Part II** | "F/TF-15 Stability Derivatives, Mass and Inertia Characteristics Flight Test Basis, Part II Aerodynamic Coefficients and Stability and Control Derivatives. Report No. MDC A4172" | — |
| Fero, AFIT/GA/ENY/91D-4, DTIC ADA243969 — ref. 23, PDF p.165 | **Part II** | same title as McDonnell | — |
| AFIT/GA/ENY/91D-1, DTIC ADA244044 — ref. 7, PDF p.81 | **Part I, Supplement 1** | "F-15 Stability Derivatives Mass and Inertia Characteristics, Part I, Supplement 1. Report Number MDC A4172, USAF Series Manual A-11-2-2-1-1, Aero/Inertia" | 4 October 1979 |
| Nolan, AFIT/GAE/ENY/92J-02, DTIC ADA256438 — ref. 15, PDF p.122 | **Part I** | "F-15 Stability Derivatives, Mass and Inertia Characteristics, Part I. Report No MDC A4172, contract no F33657-70-0300" | 1 August 1976 |
| Davison, AFIT/GAE/ENY/92M-01, DTIC ADA256613 — ref. 23, PDF p.166 | **Part I** | "F/TF-15 Stability Derivatives, Mass, and Inertia Characteristics Flight Test Basis, Part I, Mass and Inertia Characteristics. Report No. MDC A4172" | — |
| Evans, AFIT/GAE/ENY/96M-1, DTIC ADA319164 — ref. 20, PDF p.94 | **Part I** | "F-15 Stability Derivatives Mass and Inertia Characteristics, Part I, Report Number MDC A4172. USAF Series Manual A-11-2-2-1-1, Aero/Inertia" | August 1, 1976 |
| Baumann ADA217366 PDF p.91; McDonnell ADA230462 PDF p.86; Fero ADA243969 PDF p.125; Davison ADA256613 PDF p.91 & p.123 | (program comment) | `DATA IS FROM MCAIR REPORT# A4172 AND AFFTC-TR-75-32 / F-15A APPROACH-TO-STALL/STALL/POST-STALL EVALUATION` | — |

**Identity, consolidated:** McDonnell Aircraft Company report **MDC A4172**, *F/TF-15 Stability Derivatives, Mass and Inertia Characteristics, Flight Test (Data) Basis*.
- **Part I** — Mass and Inertia Characteristics, 1 August 1976, contract F33657-70-0300 (as printed), also issued as **USAF Series Manual A-11-2-2-1-1, Aero/Inertia**.
- **Part I Supplement 1** — 4 October 1979.
- **Part II** — Aerodynamic Coefficients and Stability and Control Derivatives.
- **Rev. C** — August 1976 (Parts I and II).

"F/TF-15" is the 1976 designation pair F-15A / TF-15A. The TF-15A became the F-15B.

---

## 3. Search record

| Repository | Method | Result |
|---|---|---|
| NASA NTRS | API search, 6 queries | No A4172 record |
| DTIC (`apps.dtic.mil`) | direct PDF URLs | **Blocked to scripted access** ("The request is blocked"). Not circumvented. |
| Internet Archive DTIC mirror (`archive.org/details/DTIC_*`) | metadata search; PDF + OCR text | **Source of every thesis above.** The same DTIC accession scans, with public-release distribution statements. No DTIC item titled A4172. |
| AFIT Scholar | in-app browser (metadata page only) | Evans thesis record confirmed (AFIT-GAE-ENY-96M-1, ADA319164). Barth 1987 (AFIT/GAE/AA/87D-1) and Beck 1989 (AFIT/GAE/ENY/89D-02) — the upstream tables — **not located**. |
| General web search | "MDC A4172", title strings | No hits for the report |

**Not attempted:** AFFTC-TR-75-32 is cited with DTIC accession **AD-B045115** (Nolan p.121, Fero p.163). An AD-B accession is limited-distribution, so no copy was sought.

---

## 4. Source graph

```
MDC A4172 (McAir, 1976; Part I Supp.1 1979)  ── NOT HELD
  │  cited as "verified wherever possible" (Baumann) / "(23)" (McDonnell, Fero, Davison)
  │
  ├─► Barth 1987 AFIT (not held) ──► Baumann 1989 Table VI  [PUBLIC_REPRODUCTION]
  ├─► Beck 1989 AFIT  (not held) ──► McDonnell 1990 Table II ─┬─► Fero 1991 Table IX
  │                                                           ├─► Davison 1992 p.87
  │                                                           └─► Nolan 1992 App. A  [PUBLIC_REPRODUCTION]
  ├─► Part I Supp.1 p.vii datum figure ──► AD-A244044 Fig. 21 + %MAC equation (3)  [PUBLIC_REPRODUCTION]
  └─► Part I method ──► Evans 1996 (asymmetric inertia/CG calculation; no values)

ASD F-15 SPO "Aerodynamic Data" / "F-15 Aerobase" (1988) ◄── McAir ARO10, F-15 baseline simulator ── NOT HELD
  └─► Baumann 1989: SAS polynomial fits, M 0.6 / 20,000 ft  [CURVE_FIT]
        └─► Beck 1989 ─► McDonnell 1990 (Cmq refit vs "aero database")
              ├─► Fero 1991 (+ NASA Langley rotary-balance data)
              └─► Davison 1992 Appendix C ─► Maverick MavF15BaumannMach06* (CROSS_VALIDATION)
        driver constants BWING/CWING/SREF, CMCGR=.2565  [DERIVED_SIMULATOR]

NASA Dryden-derived model ─► Brumbaugh 1991, NASA CR-186019 (author: "not ... representative of any particular aircraft")
  └─► Univ. of Washington 1994 TECS theses (DTIC ADA288610, ADA289771)  [CROSS_VALIDATION_ONLY]

NASA TM-72861 (1979, F-15 No. 8) ── independent NASA original, preproduction scope
```

**Where simplification and curve fitting happened.**
- Baumann received McDonnell look-up tables from ASD (acknowledgement, PDF p.4).
- He replaced them with SAS polynomials in α and β at one condition, M 0.6 / 20,000 ft (PDF p.20, p.36–38).
- Near-zero asymmetric terms became "hat" functions (PDF p.43).
- McDonnell 1990 found the Cmq fit wrong at low α and above 70°, and refit it after comparing against the "aero database" (PDF p.41–42).
- Nothing downstream of Baumann is a table. Every coefficient is a fit.

---

## 5. Reproduced numeric fields

Grades use this brief's scale:
- `PRIMARY_A4172_ORIGINAL`
- `PUBLIC_REPRODUCTION_OF_A4172`
- `F15_FAMILY_SUPPORT`
- `CROSS_VALIDATION_ONLY`

The code carries the matching lineage × scope pair.

**Checked against rendered page images:** Baumann PDF p.87; McDonnell PDF p.83–84; Nolan PDF p.42, p.110, p.122; Davison PDF p.78, p.88, p.91; AD-A244044 PDF p.14, p.55–56; TM-72861 PDF p.14, p.15, p.17; CR-186019 PDF p.8. The remaining page references (Fero, Evans, and bibliography pages) were read from the scans' OCR text layer.

### 5.1 Reference / physical geometry

| Field | Value | Where | Grade | Lineage × scope |
|---|---:|---|---|---|
| Wing area (reference) | **608.00 ft²** | Baumann Table VI, printed p.72 / PDF p.87 | `PUBLIC_REPRODUCTION_OF_A4172` | PublicReproduction × ProductionF15Family |
| Wing area (actual) | **599.39 ft²** | same table | `PUBLIC_REPRODUCTION_OF_A4172` | PublicReproduction × ProductionF15Family |
| Wing area (theoretical) | 608 ft² | McDonnell Table II printed p.69 / PDF p.83; Fero PDF p.119; Davison PDF p.87; Nolan PDF p.110 | `PUBLIC_REPRODUCTION_OF_A4172` | PublicReproduction × ProductionF15Family |
| Span | 42.8 ft | same tables | `PUBLIC_REPRODUCTION_OF_A4172` | PublicReproduction × ProductionF15Family |
| Mean aerodynamic chord | **191.3 in** (15.94 ft) | same tables | `PUBLIC_REPRODUCTION_OF_A4172` | PublicReproduction × ProductionF15Family |
| Root / tip chord (theoretical) | 273.3 / 68.3 in | same tables | `PUBLIC_REPRODUCTION_OF_A4172` | — (not encoded) |
| Aspect ratio, taper, sweep (LE / c/4), dihedral | 3.01, 0.25, 45° / 38.6°, −1° | same tables | `PUBLIC_REPRODUCTION_OF_A4172` | — (not encoded) |
| "C.G. station" X / Z | **FS 557.173 / WL 116.173** | McDonnell printed p.70 / PDF p.84; Davison PDF p.88 | `PUBLIC_REPRODUCTION_OF_A4172` | PublicReproduction × ProductionF15Family |
| Reference datum | FS 0.0 ≈ 116.3 in fwd of nose; WL 0.0 ≈ 100 in below airplane; BL 0.0 centerline | AD-A244044 Fig. 21, printed p.48 / PDF p.56, cited "(7:vii)" = A4172 Part I Supp. 1 | `PUBLIC_REPRODUCTION_OF_A4172` | — (not encoded) |
| %MAC equation, "all A through D models" | `(FS − 508.1)/191.33 × 100` | AD-A244044 eq. (3), printed p.47 / PDF p.55. **The equation itself carries no citation.** | `F15_FAMILY_SUPPORT` | PublicReproduction × ProductionF15Family |
| BWING / CWING / SREF | 42.8 ft / 15.94 ft / **608.** | Davison driver PDF p.91 (printed p.81) and p.124; Baumann PDF p.91; McDonnell PDF p.86; Fero PDF p.125 | `F15_FAMILY_SUPPORT` | DerivedSimulator × ProductionF15Family |
| Moment reference | CMCGR = CNCGR = .2565 c̄ | Davison listing printed p.123 | `F15_FAMILY_SUPPORT` | DerivedSimulator × ProductionF15Family |

The last two rows are the ARO10 simulator constants. Their comment block names A4172 and AFFTC-TR-75-32 as the source, but they are simulator constants, so they are graded one step below a reproduction.

**The previous audit's open point is closed:** Davison's listing does print `SREF=608.`, in the driver program rather than the coefficient subroutine. `MavF15ReferenceGeometrySources.Aro10AreaProvenanceNote` now cites that page.

### 5.2 Mass / inertia (family, recorded, **not imported**)

| Field | Value | Where | Note |
|---|---|---|---|
| Baumann Table VI | TOGW 38,400 lb; Ixx 33,400; Iyy 172,800; Izz 192,000; Ixz −105 slug-ft² | PDF p.87 | "clean config." |
| Beck-lineage table | Ixx 25,480; Iyy 166,620; Izz 186,930; Ixz −1,000 slug-ft² | McDonnell PDF p.84; Davison PDF p.88; Nolan PDF p.112 | **The same numbers carry different captions.** McDonnell: "basic F-15 with 4 AIM-7F missiles, ammo, 50% fuel". Davison: "basic, clean F-15B with ammo, 50% fuel". Gross weight 37,000 lb (McDonnell) vs 38,400 lb takeoff (Davison). |

**None of these replaces the NASA 836 Table-1 state**, which is `DirectExact836`. They show that reproductions drift as they are copied. The same table picked up different captions and weights in successive theses.

### 5.3 Fields carried in the tables that are not A4172 subject matter

McDonnell PDF p.84 also lists "Afterburning Thrust **23,810 lb**" per engine. Davison PDF p.88 has the same line, but the scan is not legible enough to confirm the digit.
- A4172 is a stability / mass / inertia document, so the thrust figure's origin is unknown.
- It is **not imported**; R5 propulsion is untouched.

### 5.4 Other public dimension tables, for comparison

| Source | S | b | c̄ | Scope | Grade |
|---|---:|---:|---:|---|---|
| NASA TM-72861 table 1, p.15 / PDF p.17 | **56.61 m²** (= 609.3 ft²) | 13.05 m | 4.86 m | Preproduction (F-15 No. 8) | `CROSS_VALIDATION_ONLY` |
| NASA CR-186019 (Brumbaugh) table 1, p.4 / PDF p.8 | 608.0 ft² | 42.8 ft | **15.95 ft** | NotRepresentativeOfAnyAircraft | `CROSS_VALIDATION_ONLY` |
| NASA/TM-2003-212027 table 1 (NF-15B 837) | 608 ft² | **42.7 ft** | 15.94 ft | ResearchModified | Incompatible |

In public F-15-like material there are two reference-area figures (608 ft² and 56.61 m² = 609.3 ft²), two reference spans (42.8 and 42.7 ft) plus a 42.83-ft physical span, and two chords (15.94 and 15.95 ft). None of these differences is explained in the sources.

---

## 6. The Part II aero-database question

| Wanted | Recoverable from public sources? |
|---|---|
| CX / CY / CZ or CL / CD | **Only as curve fits** (Baumann → Davison), at M 0.6 / 20,000 ft, α ≈ −20…90°, β ≈ ±30°. Already held as the Maverick cross-validation model. |
| Cl / Cm / Cn | same |
| α / β derivatives | same (fits) |
| rate derivatives | same (fits). McDonnell 1990 refit Cmq. Fero 1991 substituted NASA Langley rotary-balance data. |
| control derivatives | same (fits) |
| Mach / altitude grids | **No.** Every public fit is at a single condition. |
| coefficient reference dimensions | 608 ft² / 42.8 ft / 15.94 ft — ARO10 driver constants (§5.1) |
| moment reference | 0.2565 c̄, i.e. FS 557.17 / WL 116.17 via the family datum |
| **A4172 Part II tables themselves** | **No.** McDonnell 1990 says the Baumann fits are "fairly representative of the actual F-15 aerodynamic coefficients (23)" (PDF p.36). That is a comparison against Part II, not a reproduction of it. |

**Lineage verdict.**
- The public F-15 coefficient models descend from McDonnell's **simulator** database (ARO10 / 1988 Aerobase), not from a published copy of A4172 Part II.
- Every public step is a curve fit at one flight condition.
- The reconstructed research model is **not** NASA 836's model, and nothing here says what 836's DFRC simulation used.

---

## 7. Why the %MAC equation still does not close 836

The F-15A–D equation `%MAC = (FS − 508.1)/191.33 × 100` agrees with four independent statements:

| Statement | Source | By the equation |
|---|---|---|
| 836 PFTF analysis CG: 28 % MAC = FS 561.7 | AIAA 2001-3303 p.12 (**NASA 836**) | FS **561.67** |
| NF-15B 837 moment reference FS 557.2 | NASA/TM-2003-212027 table 1 | **25.66 %** MAC |
| AFIT table "C.G. station" FS 557.173 | McDonnell PDF p.84 | **25.65 %** MAC |
| ARO10 moment reference | Davison listing p.123 | **25.65 %** c̄ |

That is strong family consistency, and it is enforced as `[G8]`. It still is not the link the brief requires, for three reasons:

1. **It is a CG datum, not a coefficient reference.** It says what "% MAC" means for the airframe. It does not say what chord NASA 836's aerodynamic model divides its pitching moment by. The code keeps the two quantities distinct (`CgDatumMacLength` ≠ `CoefficientReferenceChord`), and `[G8]` shows the selector refuses a CG-datum MAC in the chord slot even at exact authority.
2. **The equation is uncited.** It sits inside an appendix whose datum figure is cited to A4172 Part I Supp. 1, but the equation itself carries no citation.
3. **No 836 source names its MAC, its leading-edge station, or its model's reference dimensions.** 836's own statement is consistent with the family equation, which is what a family member would show. But consistency is not a link.

**This needs your decision.** The narrowest defensible promotion is the **CG datum only**:
- Accept "all A through D models" as covering production F-15B 74-0141.
- That would let the Table-1 CG of 26.34 % MAC be placed at **FS 558.50**.
- It would **not** close `S`, `c̄` or `b`.

This pass does not make that promotion.

---

## 8. What can be promoted, what remains blocked

**Promoted to NASA 836 exact authority: nothing.** `S = c̄ = b_ref = 0`. `PhysicalWingSpanM` stays separate, as the brief requires.

**Changed in code (provenance only):**
- Every geometry candidate now carries a `MavF15SourceLineage` and a `MavF15ConfigurationScope`, besides its authority grade.
- 14 new candidates were added, all at non-accepting grades: the A4172 reproductions, the CG datum, TM-72861 table 1 and CR-186019.
- The ARO10 SREF provenance note is corrected.
- `MavF15McDonnellSources` records that neither McDonnell original is held.

**Blocked:**
- A4172 Part I / II originals.
- The DFRC F-15B simulation documentation.
- Any 836 statement naming its model's reference dimensions.
- The Barth 1987 and Beck 1989 theses (upstream of the reproduced tables).
- AFFTC-TR-75-32 (limited distribution).

**What would close `S` / `c̄` / `b`:** unchanged from the geometry audit. A NASA 836 source naming its baseline model's reference dimensions, or naming the production database it uses together with that database's dimensions.
