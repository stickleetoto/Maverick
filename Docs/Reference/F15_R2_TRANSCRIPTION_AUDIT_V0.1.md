# F-15 R2 Baumann Coefficient Transcription — Audit

**Revision V0.2 — supersedes V0.1 in place.** V0.1 recorded the audit as BLOCKED because the
source was unavailable, and reported one finding (F15-AUDIT-001) that the source has since
disproved. Both are corrected here; §4 says exactly what changed and why.

Status: **AUDIT COMPLETE — VERIFIED AGAINST ORIGINAL PAGE IMAGES**

Branch: `claude/f15-full-implementation`

Date: 2026-09-22 (V0.1 same day: source unavailable, audit blocked)

Source, now in hand:

> Michael T. Davison, *An Examination of Wing Rock for the F-15*,
> AFIT/GAE/ENY/92M-01, 1992, Appendix C (`COEFF` subroutine).

Verified from **original scanned page images**, not OCR text. Supplied as
`Davison_AFIT_92M01_Appendix_C_pages_122-151.pdf` and a 20-image PNG set. The PDF carries the
same scans at higher resolution (2560×3264 vs 2048×2600); ambiguous glyphs were re-read from the
PDF-embedded TIFFs at up to 22× with contrast enhancement.

**Page numbering.** Image filenames are PDF page numbers; the thesis printed number is
**PDF − 11** (PNG `p132` = printed 121). This document cites **printed** pages throughout, since
those are what appear in the page footer.

---

## 1. Outcome

| | count |
|---|---|
| Coefficient families checked against the page image | **34** |
| Individual numeric literals checked | **~330** |
| **Corrections made** | **2** |
| Previously-suspicious items resolved as source-confirmed | **6** |
| Prior "fix" withdrawn as a deviation from source | **1** |
| Residual glyph ambiguities (physical scan damage) | **2** |

The transcription was in far better shape than V0.1 could establish. Both corrections are single
wrong digits with negligible numerical effect; **no sign, exponent, term, threshold or structural
element was wrong anywhere.**

The most important result is not a correction but a **withdrawal**: V0.1 reported a defect that
the source shows was never a defect. See §4.

---

## 2. Corrections made

Only these two. Both verified visually on the page image before changing code.

### F15-AUDIT-009 — CYDAD `RAL**4` coefficient

| | |
|---|---|
| Code symbol | `MavF15BaumannMach06LateralDirectional.SideForceAileronDerivative` |
| Source symbol | `CYDAD`, first branch (`RAL .LT. 0.55851`) |
| Source page | printed **132** (PDF p143) |
| Source expression | `-(0.0365611*(RAL**4))` |
| Old transcription | `0.03656114` |
| Corrected | `0.0365611` |
| Reason | A trailing `4` was added in transcription. The source prints **seven** decimals here while every other coefficient on the same statement prints eight — which is presumably what invited the padding. Read at 5× on the page image; the digit string `0.0365611` is unambiguous and is followed directly by `*(RAL**4))`. |
| Numerical effect | ≤ 3.0e-9 in CYDAD. Negligible, but it is a wrong digit. |
| Regression | `[T4b]` |

### F15-AUDIT-010 — CFX2 trailing constant

| | |
|---|---|
| Code symbol | `MavF15BaumannMach06Longitudinal`, `cfxHigh` |
| Source symbol | `CFX2` |
| Source page | printed **130** (PDF p141) |
| Source expression | `...+(0.30604211*(DSTBR**2))+0.09833517` |
| Old transcription | `0.09833617` |
| Corrected | `0.09833517` |
| Reason | Sixth decimal is **5**, not **6**. Confirmed at 12×: the glyph has the flat top bar and open upper-left of a `5`; the adjacent `3`s and the `6` elsewhere in the same number are plainly different shapes. |
| Numerical effect | 1.0e-6 in CFX above 20° AoA. Negligible. |
| Regression | `[T4b]` |

---

## 3. Previously-suspicious items — all resolved as SOURCE-CONFIRMED

V0.1 flagged five items as possible OCR damage. **None of them were.** Every one is exactly what
the source prints.

### F15-AUDIT-003 — duplicated `RAL²·DSTBR²` monomial in CMN1 — **VERIFIED**

This was V0.1's highest-priority concern: the same monomial appearing twice looked like a
classic dropped exponent. Printed page **137** prints both lines, consecutively:

```
+-(0.06290678*(RAL**2)*(DSTBR**2))
+-(0.01404857*(RAL**2)*(DSTBR**2))
```

The duplication is the source's own. Both are kept verbatim rather than folded into
`-0.07695535`, which would be arithmetically identical but would erase the evidence.

### F15-AUDIT-004 — duplicated `RBETA` term in CNDRD — **VERIFIED**

Printed page **138** prints both `-(0.00000517304*RBETA)` and `+(0.0000461872*RBETA)`, several
lines apart within the same statement. Source-confirmed.

### F15-AUDIT-005 — two bare constants in CFX2 — **VERIFIED (shape)**

The double-constant structure is real: printed page **130** shows `CFX2=0.0267297-...` ending
with a trailing `+0.09833517`. Only the digit was wrong — that is F15-AUDIT-010 above.

### F15-AUDIT-006 — which beta multiplier belongs to which channel — **VERIFIED**

V0.1 could not decide this from code and rated it MEDIUM because a wrong assignment would corrupt
all three lateral channels at small sideslip. The source settles it completely.

Printed page **129** defines **both** multipliers:

```
IF(BETA.LE.-1.0)THEN EPA02S=-1.00
ELSEIF(BETA.GE.1.0)THEN EPA02S=1.00
ELSE EPA02S=-1.00+(1.5*((BETA+1.0)**2))-(0.5*((BETA+1.0)**3))

IF(BETA.LE.-5.0)THEN EPA02L=-1.00
ELSEIF(BETA.GE.5.0)THEN EPA02L=1.00
ELSE EPA02L=-1.00+(0.06*((BETA+5.0)**2))-(0.004*((BETA+5.0)**3))
```

Both match `BetaSignSmall` / `BetaSignLarge` exactly, `BETA` in degrees. And each assembly
statement names the one it uses:

| channel | source line | printed page | our code |
|---|---|---|---|
| CFY | `CFY=(CFY1*EPA02L)+...` | 134 | `epa02Large` ✓ |
| CML | `CML=(CML1*EPA02S)+...` | 136 | `epa02Small` ✓ |
| CMN | `CMN=(CMN1*EPA02S)+...` | 140 | `epa02Small` ✓ |

Wide band on side force, narrow band on both moments. The transcription was already correct.

### F15-AUDIT-007 — ~1e-17 coefficients in CNDAD — **VERIFIED**

Printed page **139**: `-(2.05815E-17*(RAL*DAILA))+(3.794816E-17*(DAILA**3))`. The exponents
really are `E-17`. The terms are numerically inert — that is the source's behaviour, not damage.

### F15-AUDIT-008 — the `/57.29578` "lift fit artifact" divisor — **VERIFIED**

Printed page **130** has `CL=CFZ1/57.29578` verbatim, with the source's own explanation
immediately below: the curve fit took every independent variable in radians, and for `CFX1` one
of those variables was not an angle but a dimensionless coefficient. V0.1 closed this on internal
consistency; it is now closed on the source.

---

## 4. WITHDRAWN — F15-AUDIT-001 was not a defect

**This corrects V0.1.** V0.1 reported, as a confirmed defect, that the two high-alpha asymmetric
terms guarded beta on both sides but alpha only from below, and "fixed" it by adding an
upper-alpha test.

The source has no upper-alpha test. Printed page **134**:

```
IF (RAL .LT. 0.6108652) THEN
   CYRB=0.0
   GOTO 500
ENDIF
IF ((RBETA .LT. -0.0872665) .OR. (RBETA .GT. 0.1745329)) THEN
   CYRB=0.0
   GOTO 500
ENDIF
```

and printed page **140** does the same for `CNRB` with `RAL .LT. 0.69813`. Beta both sides, alpha
from below only, in both terms.

So the original transcription was **faithful**, and V0.1's "fix" was a silent deviation from
source inside a file whose entire purpose is faithful transcription — precisely the class of
error this project exists to prevent. **The added guard has been removed.**

The divergence V0.1 measured is real (CY reaches −237 at α=179°), but it belongs to the source.
It is now bounded where Maverick's own additions belong: `MavF15BaumannMach06Domain`, which
refuses α outside [−4°, +90°]. That gate's upper bound is therefore **load-bearing** — it is the
only thing between the research model and a fictitious coefficient — and fixture `[T5]` now
asserts all three facts together: the transcription still diverges above 90°, the gate refuses
exactly that state, and the gate's bound still equals the terms' own declared `alphaMax`.

F15-AUDIT-002 (the domain gate itself) stands unchanged and is now better justified than when it
was written.

---

## 5. Verification table — every implemented coefficient family

All printed-page references. **V** = verified identical to source. **C** = corrected.

### Conventions and setup

| code symbol | source | printed page | state |
|---|---|---|---|
| `DegPerRad = 57.2957795131` | `DEGRAD=57.2957795131` | 121 | **V** |
| `ral = alphaRad` (signed) | `RAL=AL/DEGRAD` | 123 | **V** — see note below |
| `rabet = abs(betaRad)` | `ABET=ABS(BETA)`, `RABET=ABET/DEGRAD` | 123 | **V** |
| `rbeta = betaRad` (signed) | `RBETA=BETA/DEGRAD` | 123 | **V** |
| `rarud = abs(rudderDeg)/DegPerRad` | `ARUD=ABS(DRUDD)`, `RARUD=ARUD/DEGRAD` | 123 | **V** |
| `daila = abs(aileronDeg)` (degrees) | `DAILA=ABS(DAILD)` | 123 | **V** |
| `dstbr = stabDeg/DegPerRad` (signed) | `DSTBR=DSTBD/DEGRAD` | 123 | **V** |
| `pHat = p·b/2V` | `PB=(P*BWING)/(2*VTRFPS)` | 129 | **V** |
| `qHat = q·c̄/2V` | `QB=(Q*CWING)/(2*VTRFPS)` | 129 | **V** |
| `rHat = r·b/2V` | `RB=(R*BWING)/(2*VTRFPS)` | 129 | **V** |
| `MomentReferenceCgCbar = 0.2565` | `DATA CMCGR /.2565/, CNCGR /.2565/` | 123 | **V** |
| `WingSpanFt = 42.8` | `SPAN = WING SPAN = 42.8 FEET` | 126 | **V** |
| `MeanAerodynamicChordFt = 15.94` | `MAC = MEAN AERODYNAMIC CHORD = 15.94 FEET` | 127 | **V** |
| M=0.6 / 20,000 ft | `1988 F15 AEROBASE (0.6 MACH, 20000 FEET)` | 123 | **V** |
| axis/sign conventions | CX +fwd, CY +right, CZ +down, CLM +R-wing-down, CMM +nose-up, CNM +nose-right | 122 | **V** |

> **`RAL` is signed.** Printed page 122's variable glossary calls `RAL` "ABSOLUTE VALUE OF ALPHA",
> but the executable assignment on page 123 is `RAL=AL/DEGRAD` with no `ABS`. The glossary line is
> a stale comment in the source itself; the assignment is authoritative. Our signed `ral` is
> correct. This matters — treating alpha as a magnitude would destroy every negative-alpha branch.

### Longitudinal

| code symbol | source symbol | printed page | state |
|---|---|---|---|
| `cfz` | `CFZ1` (and `CFZ=CFZ1`) | 130 | **V** (7 coefficients) |
| `clArtifact` | `CL=CFZ1/57.29578` | 130 | **V** |
| `cfxLow` | `CFX1` | 130 | **V** (5 coefficients) |
| `cfxHigh` | `CFX2` | 130 | **C** — F15-AUDIT-010 (8 coefficients, 1 corrected) |
| `BlendLowHighAoaDrag` | `A1/A2/A12/BA/BB/BC/BD/F1/F2` | 130 | **V** — all 9 expressions identical |
| blend branches | `IF(RAL.LT.A1) CFX=CFX1 / ELSEIF(RAL.GT.A2) CFX=CFX2 / ELSE CFX=CFX1*F1+CFX2*F2` | 130–131 | **V** |
| `cmm1` | `CMM1` | 136 | **V** (12 coefficients) |
| `PitchDampingDerivative` seg 1 | `CMMQ`, `RAL.LE.0.25307` | 137 | **V** (10 coefficients) |
| `PitchDampingDerivative` seg 2 | `CMMQ`, `0.25307<RAL<0.29671` | 137 | **V** (4 coefficients) |
| `PitchDampingDerivative` seg 3 | `CMMQ`, `RAL.GE.0.29671` | 137 | **V** (7 coefficients) |
| `cmm = cmm1 + cmmq*qHat` | `CMM=CMM1+(CMMQ*QB)` | 137 | **V** |
| `result.cx`, `result.cz` | `CX=CFZ*SIN(RAL)-CFX*COS(RAL)`, `CZ=-(CFZ*COS(RAL)+CFX*SIN(RAL))` | 140 | **V** |

### Lateral-directional

| code symbol | source symbol | printed page | state |
|---|---|---|---|
| `BetaSignSmall` | `EPA02S` | 129 | **V** |
| `BetaSignLarge` | `EPA02L` | 129 | **V** |
| `cfy1` | `CFY1` | 131 | **V** (13 coefficients) |
| `SideForceRollRateDerivative` | `CFYP`, 3 branches | 131 | **V** (10 + 4 + const) |
| `SideForceYawRateDerivative` | `CFYR`, 5 branches | 131, 132 | **V** (const + 3 + 8 + 4 + const) |
| `SideForceAileronDerivative` | `CYDAD`, 3 branches | 132 | **C** — F15-AUDIT-009 (9 + 4 + 8, 1 corrected) |
| `SideForceRudderDerivative` | `CYDRD` | 133 | **V** (12 coefficients) |
| `SideForceDifferentialTailDerivative` | `CYDTD` | 133 | **V** (19 coefficients) |
| `HighAlphaAsymmetricSideForce` | `CYRB`, `RALY1/RALY2/RBETY1/RBETY2`, `AY/ASTARY/BSTARY`, `FY`, `GY` | 133, 134 | **V** — guards withdrawn per §4 |
| `cml1` | `CML1` | 134 | **V** (12 coefficients) |
| `RollDampingDerivative` | `CMLP`, 3 branches | 135 | **V** (9 + 4 + 6) |
| `RollingMomentYawRateDerivative` | `CMLR`, 3 branches | 135 | **V** (9 + 4 + 7) — see §6 |
| `RollingMomentAileronDerivative` | `CLDAD` | 135 | **V** (10 coefficients) |
| `RollingMomentRudderDerivative` | `CLDRD` | 136 | **V** (12 coefficients) |
| `RollingMomentDifferentialTailDerivative` | `CLDTD` | 136 | **V** (19 coefficients) |
| `F15BCanopyRollingMomentIncrement` | `DCLB`, 3 branches | 136 | **V** |
| `cmn1` | `CMN1` | 137 | **V** (23 coefficients) — see §6 |
| `YawingMomentRudderDerivative` | `CNDRD` | 137, 138 | **V** (23 coefficients) |
| `YawingMomentRollRateDerivative` | `CMNP`, 3 branches | 138 | **V** (10 + 4 + 8) |
| `YawDampingDerivative` | `CMNR`, 5 branches | 138, 139 | **V** (const + 3 + 8 + 4 + const) |
| `YawingMomentDifferentialTailDerivative` | `CNDTD` | 139 | **V** (10 coefficients) |
| `YawingMomentAileronDerivative` | `CNDAD` | 139 | **V** (9 coefficients) |
| `dcnb = -0.00025` | `DCNB=-2.500E-4` | 139 | **V** |
| `HighAlphaAsymmetricYawingMoment` | `CNRB`, `RALN1/RALN2/RBETN1/RBETN2`, `AN/ASTARN/BSTARN`, `FN`, `GN` | 139, 140 | **V** — guards withdrawn per §4 |
| `CompactSupportShape` | `FY`/`GY`/`FN`/`GN` shape | 134, 139, 140 | **V** |

### Flex multipliers and assembly

| code symbol | source | printed page | state |
|---|---|---|---|
| `SideForceRudderFlex = 0.89` | `DRFLX5=0.89` | 131 | **V** |
| `RollRudderFlex = 0.85` | `DRFLX1=0.85` | 134 | **V** |
| `YawRudderFlex = 0.89` | `DRFLX3=0.89` | 137 | **V** |
| `DifferentialTailFlex = 0.975` | `DTFLX5=0.975`, `DTFLX1=0.975`, `DTFLX3=0.975` | 131, 134, 137 | **V** |
| `cy` assembly | `CFY=(CFY1*EPA02L)+(CYDAD*DAILD)+(CYDRD*DRUDD*DRFLX5*EPA43)+((CYDTD*DTFLX5)*DTALD)+(CFYP*PB)+(CFYR*RB)+CYRB` | 134 | **V** |
| `cl` assembly | `CML=(CML1*EPA02S)+(CLDAD*DAILD)+(CLDRD*DRUDD*DRFLX1*EPA43)+((CLDTD*DTFLX1)*DTALD)+(CMLP*PB)+(CMLR*RB)+(DCLB*BETA)` | 136 | **V** |
| `cn` assembly | `CMN=(CMN1*EPA02S)+(CNDAD*DAILD)+((CNDRD*DRUDD*DRFLX3)*EPA43)+((CNDTD*DTFLX3)*DTALD)+(CMNP*PB)+(CMNR*RB)+(DCNB*BETA)+CNRB` | 140 | **V** |

### Terms correctly ABSENT from our code — confirmed zero or cancelling in source

| omitted term | source justification | printed page |
|---|---|---|
| `EPA43` (speedbrake multiplier) | alpha-dependent form is commented out; active code is `EPA43=1.0` | 133 |
| `CXDSPD`, `CZDSPD`, `CMDSPD`, `CLDSPD`, `CNDSPD` | all "SET TO 0" for this study | 124–128 |
| `DCXLG` + `DCD` in CFX | `-0.0005` and `+0.0005`; source states *"NOTE THAT DCXLG AND DCD CANCEL EACH OTHER"* | 124 |
| `DCL` (canopy lift increment) | `=0.0` | 125 |
| `DCYB` (canopy side-force increment) | `=0.0`; source explains the same canopy is on F-15A and F-15B | 125 |
| `DCM` (canopy pitch increment) | `=0.0` | 127 |
| `CXRB` | `=0.0` | 124 |
| `DCNB2 * EPA36` | `DCNB2=0.0` | 128 |
| `DTFLX2`, `DTFLX4`, `DTFLX6` | all `=0.0` | 126, 127, 125 |
| store increments | not present in the clean research configuration | 124–128 |

### Propulsion terms — deliberately omitted, now exactly identified

The source folds thrust into two places. Printed page **140**:

```
CX=CFZ*SIN(RAL)-CFX*COS(RAL)+THRUST/QBARS
CMM=CMM+THRUST*(0.25/12.0)/(QBARS*CWING)
C   THE (0.25/12.0) IS THE OFFSET OF THE THRUST VECTOR FROM THE CG
```

Both are omitted from Maverick, because propulsion is a separate load owner and importing them
would double-apply thrust. Our `Evaluate` takes no throttle argument at all, which makes the
omission structural rather than a convention. Fixture `[T11]`. This confirms the V0.1 code
comment was describing the source accurately.

---

## 6. Residual ambiguities — physically damaged glyphs, NO code change

Two characters are damaged in the scan and cannot be read with certainty. In both the surviving
strokes match the transcribed digit, so there is no unambiguous disagreement and nothing was
changed. Re-read from the PDF's higher-resolution TIFFs at 16–22× with contrast enhancement, and
at raw pixel level; the damage is in the paper, not the rendering.

### CMLR `RAL**4` coefficient — printed page 135

Source reads `-(45.8316?842*(RAL**4))`; our value is `45.83162842`.

The character between `6` and `8` survives as an upper-right diagonal and a lower-left bar, with
no vertical stem. The `1` two positions earlier in the same number has a strong vertical stem and
base serif, so the damaged glyph is not a `1`; the surviving strokes are where a `2`'s top curve
and bottom bar would be. **Consistent with our `2`.**

Worst-case numerical exposure if it were a `1` instead: ≤ 4e-7 in Cl.

### CMN1 `RABET` coefficient — printed page 137

Source reads `+(0.?7225609*RABET)`; our value is `0.07225609`.

The character after the decimal point survives as a top arc and a bottom arc with both verticals
missing. The intact `0` immediately before it (in `(0.`) shows the same two arcs *plus* verticals,
so a faded `0` is the natural reading. A `6` would leave a descending upper-left stroke and a
closed lower bowl, neither of which is present. **Consistent with our `0`.**

A wrong leading digit here would be large — `0.37225609` would roughly quintuple CMN1's dominant
sideslip term — so this one is worth a second pair of eyes if anyone has a cleaner scan. The
physical plausibility of the transcribed value, and fixtures `[T6]`/`[T7]`, both support `0`.

---

## 7. Conventions checked and found correct

Unchanged from V0.1 and now confirmed against the source rather than inferred:

| item | finding |
|---|---|
| Unity vs aero body axes | source declares CX +fwd, CY +right, CZ +down, CLM +right-wing-down, CMM +nose-up, CNM +nose-right (printed p122) — identical to the Maverick core contract |
| body-axis rotation | `CX=CFZ*SIN(RAL)-CFX*COS(RAL)`, `CZ=-(CFZ*COS(RAL)+CFX*SIN(RAL))` verbatim (p140) |
| degrees vs radians | confirmed per-variable from the assignment block (p123). `RA`-prefixed values and `DSTBR` are radians; `DAILA`, `DAILD`, `DRUDD`, `BETA` in the `DCLB`/`DCNB` products are degrees |
| rate normalization | `PB`/`QB`/`RB` verbatim (p129); span on p/r, chord on q |
| dynamic pressure | `QBARS` is applied by the caller, never inside `COEFF`; our `Dimensionalize` does the same, once |
| thrust in the aero path | both source thrust terms identified and omitted (§5) |
| differential tail | Davison's header (p121) states this version uses "the differential tail deflection state rather than .3*DAILD" and takes `DTALD=U(11)`. The `DTALD=0.3*DAILD` lines in the comment blocks are stale ARO10 text. Our owned-actuator path is the correct behaviour; the 0.3 relation is a labelled fallback only |

---

## 8. Tests

**RUN — Unity 6000.3.16f1 headless**, via `MavFdmValidationBatchAdapter`, after the corrections:

| suite | result |
|---|---|
| `MavF15BaumannTranscriptionValidation.RunAll` | **28 passed, 0 failed** |
| `MavF15ControlPathValidation.RunAll` | **41 passed, 0 failed** |
| `MavF15PropulsionValidation.RunAll` | **27 passed, 0 failed** |
| **total** | **96 passed, 0 failed** |

All 304 runtime scripts compile with **0 errors**.

New fixtures added this pass:

- `[T4b]` — two source-digit regressions, one per correction, each asserting the production value
  is closer to the corrected digit than to the one it replaced.
- `[T5]` — rewritten. Now asserts the *opposite* of what it asserted in V0.1: that the
  transcription still diverges above 90°, that the domain gate refuses that state, and that the
  gate's bound still equals the terms' declared `alphaMax`.

**NOT RUN:** Unity play mode; any flight, trim or trajectory test.

---

## 9. What this audit does and does not establish

**Established:** the implemented coefficients are a faithful transcription of Davison Appendix C,
verified symbol by symbol against the original page images, with two corrected digits and two
damaged glyphs whose surviving strokes match what we have.

**Not established, and not affected by this audit:** that the Baumann model is valid for the
Maverick target. It remains what it was — a **CROSS-VALIDATION / RESEARCH** model of the 1988 F-15
aerobase at a single flight condition, fixed at M=0.6 / 20,000 ft, explicitly opt-in, and gated on
Mach, altitude, alpha and beta.

Verifying a transcription proves the numbers were copied correctly. It says nothing about which
aircraft they describe. **The Baumann model is still not NASA F-15B 836 authority**, and nothing
here moves it closer to being so — that needs MDC A4172 Part II, not this appendix.
