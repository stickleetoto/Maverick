# F-15 R2 Baumann Coefficient Transcription — Audit V0.1

Status: **PARTIAL AUDIT — SOURCE VERIFICATION BLOCKED, STRUCTURAL VERIFICATION COMPLETE**

Branch: `claude/f15-full-implementation`

Date: 2026-09-22

Scope: the AFIT/Baumann research aerodynamic transcription in
`Assets/MaverickFresh/Scripts/FlightDynamics/F15/`.

---

## 1. Headline

The handoff asked for a coefficient-by-coefficient comparison against Davison Appendix C.

**That comparison could not be performed.** The source scans are not in this repository —
`find . -iname "*.pdf"` returns zero files, and `Docs/Reference/Data/` contains only F-16
TP-1538 material. Section 8 lists exactly what is needed.

Reconstructing the numbers from memory was rejected: it would convert an unverified
transcription into a fabricated one while making it look verified.

What was done instead is a **structural audit** — the properties a correct transcription of
this particular routine must have, all of which are decidable from the code alone. Those
properties are strong, because the source routine is built from fitted piecewise polynomials
that were constructed to join, and from smoothing functions with exact endpoint conditions. A
mistyped digit, a dropped exponent, a swapped sign or a mangled breakpoint breaks them.

Outcome:

| | count |
|---|---|
| Structural properties verified | 25 (all passing) |
| Confirmed implementation defects found and fixed | 2 |
| Transcription questions requiring the source scans | 5 |
| Numeric coefficients changed | **0** |

No transcribed number was altered. The two fixes are guard/ownership defects in the
surrounding Unity code, not edits to source data.

---

## 2. Confirmed defects — found, fixed

### F15-AUDIT-001 — compact-support terms had no upper alpha bound  (FIXED)

`MavF15BaumannMach06LateralDirectional.cs` —
`HighAlphaAsymmetricSideForce`, `HighAlphaAsymmetricYawingMoment`

Both high-alpha asymmetric terms declare compact support on an alpha interval and a beta
interval. Beta was guarded on both sides. Alpha was guarded only from below:

```csharp
if (ral < alphaMin || rbeta < betaMin || rbeta > betaMax)   // alphaMax never tested
```

`CompactSupportShape` decays to zero only *inside* its interval. Its window factor is
`(u² − 1)²`, which past `u = ±1` grows without limit. So above the declared `alphaMax` of
90°, a term intended to peak at 0.164 instead ramps away:

| alpha | CYRB (intended max 0.164) |
|---:|---:|
| 55° (star point) | +0.164 |
| 90° (alphaMax) | ~0 |
| 100° | −0.131 |
| 120° | −3.89 |
| 150° | −48.9 |
| 179° | **−237** |

That is ~1450× the declared amplitude, with the sign reversed. `MavF15AeroModel`'s finiteness
check cannot see it — −237 is a perfectly finite float.

This is unambiguously an implementation oversight, not a transcription question: the bound is
already named in the code as `alphaMax`, and the sibling beta guard shows the intended pattern.
Both functions now test both alpha bounds. Regression: `[T5]`.

### F15-AUDIT-002 — the research model had no alpha/beta domain gate  (FIXED)

`MavF15AeroModel.cs`, new `MavF15BaumannMach06Domain.cs`

The model already failed closed off-Mach and off-altitude, but nothing bounded alpha or beta.
The base fits are 6th- to 9th-order polynomials; outside the fitted region they diverge rather
than degrade. At the source condition with surfaces and rates neutral:

| alpha | Cm | CY |
|---:|---:|---:|
| inside the gate (worst case) | — | max abs coefficient **2.27** |
| 120° | −10.4 | −4.37 |
| 150° | −122 | −40.5 |
| 180° | **−730** | **−190** |

Again the finiteness check passes all of these. Dimensionalized, a Cm of −730 is a wholly
fictitious pitching moment being published as research data.

The new gate refuses outside the span of alpha/beta **breakpoints the transcribed routine
itself declares**:

| bound | value | declared by |
|---|---:|---|
| alpha min | −4.0° | constant extensions in `SideForceYawRateDerivative` (−0.06981) and `YawDampingDerivative` (−0.069813) — the lowest alpha breakpoint anywhere in the routine |
| alpha max | +90.0° | the compact-support `alphaMax` shared by both asymmetric terms — the highest |
| abs beta max | 20.0° | the +0.34906 rad upper support bound of `HighAlphaAsymmetricYawingMoment` — the largest beta magnitude in any breakpoint |

**This is explicitly not an aerodynamic validity envelope**, and is documented as such in the
class. Neither Davison nor Nolan publishes an alpha/beta validity statement for the routine,
and Davison warns against assuming the M=0.6 fit transfers. No such statement is invented
here. The gate answers only the narrower, checkable question: is the routine still inside the
region its own piecewise structure was written for? Regression: `[T10]`.

---

## 3. Structural properties verified

Run with `MavF15BaumannTranscriptionValidation.RunAll`. **Executed 2026-09-22: 25 passed,
0 failed** (see section 7 for how, and for what that does and does not prove).

| ID | property | result |
|---|---|---|
| T1 | all 16 declared breakpoints join continuously through the public entry points | PASS — worst jump 2.7e-4 (Cm at 0.25307 rad); every join is source round-off, none is a step |
| T2 | 20–30° drag transition hands over with no slope kink at either end | PASS — dCFX/dα 4.0178→4.0247 at 20°, 2.1804→2.1977 at 30° |
| T2 | blended drag rises monotonically through the transition | PASS — 0.5382 → 0.9312 |
| T3 | both beta multipliers are exactly 0 at β=0 | PASS |
| T3 | ±1° band saturates with zero slope (no kink in Cn) | PASS — max slope jump 1.34e-4 |
| T3 | ±5° band saturates with zero slope (no kink in CY) | PASS — max slope jump 5.03e-5 |
| T3 | Cn monotonic through the small band | PASS |
| T4 | compact-support bump reaches exactly its declared amplitude at the star point | PASS — 0.164 and 0.034 recovered exactly |
| T5 | bump switched off above 90°, with no step at the boundary | PASS — F15-AUDIT-001 regression |
| T6 | β=0, neutral surfaces, zero rates ⇒ CY=Cl=Cn **exactly** 0 for α −4..34° | PASS — worst 0.0 |
| T6 | asymmetric departure yaw IS present at α=60°/β=0 | PASS — Cn=0.0269, intended source behaviour, not a symmetry fault |
| T7 | C(−β) = −C(+β) below the departure region | PASS — worst 0.0 |
| T8 | recovered CFX stays positive across the whole transcribed span | PASS — min 0.01807 at α=0 |
| T9 | dCFZ/dα = **0.0662 /deg** (3.791 /rad) at α=0 | PASS |
| T9 | CFZ monotonically increasing α −4..15° | PASS |
| T10 | all six coefficients bounded inside the gate | PASS — worst 2.266 |
| T11 | CX at α=0 is negative; CZ at α=5° is negative | PASS |

Three of these deserve comment.

**T9 is the one genuinely independent physical check available without the scans.** A real
F-15 at M=0.6 has a lift-curve slope near 0.06–0.07 per degree. The transcription yields
0.0662 /deg. A radian/degree confusion in CFZ would have landed near 0.0012 or near 3.8; a
lost or gained leading digit would be an order of magnitude out. CFZ is transcribed in the
right units, at the right magnitude, with the right slope.

**T6 distinguishes a fault from the model.** Exact zeros below 35° confirm no spurious
asymmetry anywhere in the lateral chain. The *non*-zero Cn at α=60°/β=0 is the source's
asymmetric departure terms switching on deliberately — this is a wing-rock research model, and
nose-slice asymmetry at zero sideslip is the phenomenon it exists to represent. The fixture
asserts it is present, so that losing the compact-support terms would fail rather than look
tidier.

**T1 is the strongest structural evidence.** Sixteen breakpoints across eight independent
piecewise channels, every one continuous to within source round-off. These segments were
fitted separately and then joined; a wrong breakpoint value, a wrong exponent, or a mistyped
coefficient inside any segment would show up here as a step. None does.

---

## 4. Open transcription questions — require the source scans

These are **not** decidable by any structural check. No number was changed. Each is marked in
the code at the exact line, with its audit ID.

### F15-AUDIT-003 — duplicated monomial in CMN1  (OPEN, **HIGH**)

`MavF15BaumannMach06LateralDirectional.cs`, `cmn1`:

```csharp
- (0.06290678 * ral * ral * dstbr * dstbr)
- (0.01404857 * ral * ral * dstbr * dstbr)
```

The same monomial `α²·δstab²` appears twice with different coefficients. A repeated monomial
in a transcribed polynomial listing is the classic signature of an exponent lost in OCR — one
of these is probably `α³·δstab²`, `α²·δstab³`, or `α·δstab²` in the source.

Impact: the pair contributes up to **−9.4e-3** to Cn at α=57°, δstab=−20°, which is the same
order as Cn itself in that region. Plausible alternative readings shift Cn by ~2e-4 to ~2e-3
there. Either way this is the highest-value single item the scans would close.

**Needs:** Davison AFIT/GAE/ENY/92M-01 Appendix C, the CMN1 listing.

### F15-AUDIT-004 — duplicated `rbeta` term in CNDRD  (OPEN, LOW)

`YawingMomentRudderDerivative` carries `−0.00000517304·β` and `+0.0000461872·β`. Same
monomial, net +4.10e-5·β. Unlike 003 this reads plausibly as two separately grouped source
lines, and the net is ~1% of the leading constant. Recorded, low priority.

### F15-AUDIT-005 — two bare constants in CFX2  (OPEN, LOW)

`MavF15BaumannMach06Longitudinal.cs`, `cfxHigh` has both `0.0267297` and a trailing
`+ 0.09833617`. This reads naturally as a separate source increment line
(`CFX2 = CFX2 + 0.09833617`) rather than a fault. CFX stays positive across the whole span
either way (T8), so the structural checks cannot discriminate.

### F15-AUDIT-006 — which beta smoothing width belongs to which channel  (OPEN, MEDIUM)

T3 proves both functions are exact smooth steps: `−1 + 1.5s² − 0.5s³` over ±1° and
`−1 + 0.06s² − 0.004s³` over ±5° are precisely the `−1 + 6s²/w² − 4s³/w³` form for w=2 and
w=10. Both saturate at exactly ±1 with exactly zero slope. The *shape* is certainly right.

What cannot be checked from code is the **assignment**: the transcription applies the ±5° band
to the CY basic term and the ±1° band to Cl and Cn. If the source swaps them, all three
lateral channels are wrong within a few degrees of sideslip — precisely the region ordinary
flight spends most of its time in. Medium priority for that reason.

**Needs:** Appendix C, the EPA02 definitions and their use sites.

### F15-AUDIT-007 — ~1e-17 coefficients in CNDAD  (OPEN, INFO)

`YawingMomentAileronDerivative` carries `−2.05815e-17·α·|δail|` and `+3.794816e-17·|δail|³`,
about 14 orders below the leading term. Numerically inert at any realistic deflection, so a
fault here cannot change Cn. Recorded for completeness.

### F15-AUDIT-008 — the "lift fit artifact" divisor  (**CLOSED**)

`cfz / 57.29578` looks like a stray rad→deg conversion. It is not: the drag-polar fit
immediately below carries +499, −1.45e4 and +2.13e6 on the 2nd/3rd/4th powers of its argument,
which only produce sane drag for an argument of order 1e-2. CFZ itself is order 1 (0.95 at
α=15°); undivided it would put ~1.7e6 into the quartic term. Divided, it gives 0.0166 there
and CFX_low = 0.253. The divisor and the polar coefficients are mutually consistent, so the
transcription corroborates itself. Closed on internal evidence; noted in code.

---

## 5. Conventions checked and found correct

| item | finding |
|---|---|
| Unity vs aero body axes | model returns coefficients only in the core's X-fwd/Y-right/Z-down contract; conversion is owned by `MavFlightDynamicsMath` and untouched |
| body-axis rotation | `CX = CFZ·sinα − CFX·cosα`, `CZ = −(CFZ·cosα + CFX·sinα)` — standard stability→body rotation, correct |
| moment sign convention | L roll-right, M nose-up, N nose-right, matching the core contract |
| degrees vs radians | consistent with the source's own Fortran naming: `RA`-prefixed values (`rabet`, `rarud`) and `dstbr` are radians; `D`-prefixed (`daila`) and the surface deflections multiplying per-degree derivatives are degrees. No mixed-unit term found |
| rate normalization | `pHat = p·b/2V`, `qHat = q·c̄/2V`, `rHat = r·b/2V` — correct reference lengths on each channel |
| dynamic pressure | applied exactly once, in `MavFlightDynamicsMath.Dimensionalize`; the aero model never sees q |
| force vs moment dimensionalization | `qS` for forces, `qS·b` for l/n, `qS·c̄` for m — in core, unchanged |
| thrust in the aero path | source folds thrust into CX and a thrust-line term into Cm; both correctly omitted, so propulsion cannot double-apply. `Evaluate` takes no throttle argument at all, which makes it structurally impossible. Asserted by T11 |
| duplicate load application | aero returns coefficients; `MavSixDoFBody` remains the sole application boundary. Unchanged by this work |
| null component behaviour | `MavF15AeroModel` refuses to zero with a visible reason on every failure path |
| accidental live-FDM activation | default mode remains `ExactNasa836Unavailable`; research modes still require explicit opt-in. The new gate only adds a refusal, never an enable |

---

## 6. Files changed

| file | change |
|---|---|
| `F15/MavF15BaumannMach06LateralDirectional.cs` | F15-AUDIT-001 fix (both alpha guards); audit markers for 003, 004, 006, 007. **No coefficient altered** |
| `F15/MavF15BaumannMach06Longitudinal.cs` | audit markers for 005 and 008. **No coefficient altered** |
| `F15/MavF15BaumannMach06Domain.cs` | **new** — F15-AUDIT-002 breakpoint-span gate |
| `F15/MavF15AeroModel.cs` | wires the gate in; adds `debugInsideTranscribedSpan` |
| `Validation/MavF15BaumannTranscriptionValidation.cs` | **new** — the 25 fixtures above |

---

## 7. Tests — what was actually run

**RUN.** `MavF15BaumannTranscriptionValidation.RunAll` — **25 passed, 0 failed**, 2026-09-22.

Executed inside Unity 6000.3.16f1, headless, through the project's own
`MavFdmValidationBatchAdapter` (`-batchmode -fdmMode sync`), which reported
`FDM_VALIDATION_RESULT_V1` status PASS. The whole project compiles clean in the editor.

**NOT RUN:**

- Unity Editor play-mode. No scene, prefab, Rigidbody or `MavSixDoFBody` integration was
  exercised.
- Any flight, trim or trajectory test.
- Any comparison against AFIT source plots or tables — the scans are unavailable, which is the
  blocker this document exists to report.

What a pass means: **internally consistent and structurally sound.** It does not mean
*verified against the source*. Items 003–007 stay open regardless of this result.

---

## 8. BLOCKED — what is needed to close the audit

| # | document | closes | why it is the right source |
|---|---|---|---|
| 1 | **Davison, *An Examination of Wing Rock for the F-15*, AFIT/GAE/ENY/92M-01, 1992 — Appendix C** | 003, 004, 005, 006, 007 | The reproduced COEFF routine itself. Every open item is a question about one listing in this appendix. **Single highest-value item.** |
| 2 | Nolan II, *Wing Rock Prediction Method for a High Performance Fighter Aircraft*, AFIT/GAE/ENY/92J-02, 1992 | cross-check on 003, 006 | Independent transcription of the same lineage; a second listing would resolve ambiguous digits |

Please supply **page images or the PDF**, not OCR text. Every open item is a question about a
character that OCR is most likely to have damaged — an exponent, a sign, a leading digit.
OCR text cannot settle a question about what OCR did.

Until then the research model keeps its present standing: **cross-validation only, explicitly
opt-in, fail-closed off-condition, and not NASA 836 authority.** These findings do not change
that standing in either direction.
