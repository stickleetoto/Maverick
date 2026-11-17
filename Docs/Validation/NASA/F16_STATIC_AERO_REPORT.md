# F-16 Static Aerodynamics and Case 11 Trim Feasibility

**Suite:** `MavNasaF16StaticAeroValidation` · **Unity** `6000.3.16f1` · **Result:** **37 passed / 1 failed** (run `f16b`). The single failure is a genuine, attributed model discrepancy (§5, D-F3), not an implementation or infrastructure error.

**Configurations are kept separate throughout:** Maverick's F-16 is `F16-CFG-SIM-MORELLI1998` aerodynamics on the TP-1538 mass/geometry; NASA's is `F16-CFG-NESC-CHECKCASE`. Nothing of one is written into the other.

**Data path.** NASA's `F16_aero.dml`, `F16_prop.dml` and `F16_inertia.dml` are read at run time from a user-supplied path and accepted only with their pinned SHA-256 (`82796577…7e51`, `e5fd7772…15c6`, `e11d4eba…3c4a`, byte-identical to the repository library's copies). They are evaluated by `MavDaveMlEvaluator`, an independent minimal ANSI/AIAA S-119 interpreter that shares no code with Maverick.

---

## 1. The NASA side is verified first ([F0], [F1], [F7])

| Check | Result |
|---|---|
| file identity | **PASS** |
| all 16 NASA aerodynamic static check-shots (9 outputs each, tolerance 10⁻⁶) | **PASS**, worst error 8.2 × 10⁻¹² |
| all 9 NASA propulsion check-shots | **PASS** |
| NESC tables at SIM 04's published trimmed state (α = θ = 2.63872639635°, q̄ 280.7878 psf, CG 0.25 c̄ with moments transferred from the 0.35 c̄ MRC) reproduce SIM 04's aerodynamic Z force | **PASS**, relative error 2.7 × 10⁻⁵ (−20 401.86 vs −20 401.30 lbf) |
| … and X force | **PASS**, 5.5 × 10⁻⁶ (−1 420.45 vs −1 420.44 lbf) |
| required thrust m·g·sinθ − F_x = 2 364.65 lbf vs NESC deck at the published trim throttle 0.1390191 (2 366.27 lbf) | **PASS**, 6.9 × 10⁻⁴ |

The trim elevator solved from the tables is **−3.2329°** (NASA README Table 11: −3.241° at its slightly different nominal condition). The CG-transfer convention is corroborated by the tools themselves: SIM 02 logs a pitching moment of 23 119.7 ft·lbf about the MRC where SIMs 04/05 log ≈ 0 about the CM, and 20 423.7 lbf × 0.10 × 11.32 ft = 23 119.6 ft·lbf.

---

## 2. Maverick's units, axes and dimensionalisation ([F2], [F3])

| Check | Result |
|---|---|
| reference geometry equals 300 ft² / 30 ft / 11.32 ft converted (27.87091 m², 9.144 m, 3.450336 m) | **PASS** |
| `MavFlightDynamicsMath.Dimensionalize`: force = q̄·S·C, moment = q̄·S·(b or c̄)·C, against an English-unit computation | **PASS**, 9.3 × 10⁻⁸ |
| `MavSixDoFBody.BuildFlightState` q̄ = ½ρV² | **PASS** |
| production `MavF16AeroModel` component (deg → rad, rate normalisation p·b/2V etc.) equals the polynomial | **PASS**, 3.7 × 10⁻⁹ |
| control and stability derivative **signs**: elevator, aileron and rudder effects, Clβ, Clp, Cmq, Cnr | identical between Maverick and NESC (table in §4) — **no axis or sign convention mismatch** |

---

## 3. Maverick (Morelli) vs NESC tables over the shared domain ([F4], [F5])

Sweep: α −10…45° in 2.5° steps × β {−30…30} × elevator {−24, −12, 0, 12, 24}°, aileron = rudder = 0, rates 0 (1 035 points; Morelli finite at all: **PASS**). Difference = Morelli − NESC.

| Coef. | max \|d\| | rms d | NESC range | max\|d\|/range | correlation | worst at (α, β, δe) | classification (rule below) |
|---|---:|---:|---:|---:|---:|---|---|
| CX | 0.0262 | 0.0113 | 0.278 | 0.094 | 0.9911 | 20, −30, 0 | fit/representation |
| CY | 2.0 × 10⁻⁷ | 1.2 × 10⁻⁷ | 1.20 | 0.000 | 1.0000 | — | identical (both linear in β) |
| CZ | 0.0593 | 0.0310 | 3.383 | 0.018 | 0.9994 | 5, 0, −24 | fit/representation |
| Cl | 0.193 | 0.0317 | 0.182 | 1.061 | 0.7525 | 45, −30, −24 | **unresolved** |
| Cm | 0.0396 | 0.0145 | 0.511 | 0.077 | 0.9946 | −10, −30, 24 | fit/representation |
| Cn | 0.575 | 0.0852 | 0.212 | 2.711 | 0.5866 | 45, −30, −24 | **unresolved** |

Rule (reporting only): correlation ≥ 0.98 and max|d| ≤ 25 % of the NESC range ⇒ fit/representation difference; otherwise unresolved. The rolling- and yawing-moment differences concentrate at high α combined with large sideslip (the corners of the fitted domain), where a global polynomial departs most from a table.

At NASA's check-shot inputs (α 5°, V 300 ft/s), Morelli − NESC: **ΔCX = +0.0079, ΔCZ = −0.0593, ΔCm = −0.0112** in the nominal shot; lateral differences ≤ 0.003 except rate terms (ΔCY ±0.018 for ±roll rate).

---

## 4. Derivative signs ([F6]), central differences at β = 0, V 500 ft/s

| Derivative | Gated | Agreement where both are ≥ 10 % of the NESC maximum |
|---|---|---|
| Cl_β | yes | 9/9 — **PASS** |
| **Cn_β** | yes | **8/9 — FAIL** (α 30°: Morelli −0.0021 /deg, NESC +0.0008 /deg) |
| Cl_p, Cm_q, Cn_r | yes | 10/10 each — **PASS** |
| Cm_α | reported | 6/8 (Morelli's Cm(α) is linear at δe = 0: constant +0.0008 /deg about 0.35 c̄; the table varies) |
| Cm_δe, Cl_δa, Cn_δa, Cl_δr, Cn_δr | reported | all agree in sign (9–10/10) |

---

## 5. Discrepancies, attributed and classified

| ID | Finding | Attribution ([F8]: Morelli Table 3 typed from the paper and evaluated independently) | Classification |
|---|---|---|---|
| D-F1 | **Drag near cruise α.** CX(α 5°, δe 0): Morelli **+0.003929**, NESC −0.004 — opposite sign (a small net forward force in body axes instead of drag) | Maverick equals Table 3 exactly → in the published polynomial | fit/representation difference **or** source-data difference (unresolved, see note) |
| D-F2 | **Lift at low α.** CZ(α 5°): Morelli **−0.475307**, NESC −0.416 (≈ 14 % more lift) | Maverick equals Table 3 | same |
| D-F3 | **Directional-stability zero crossing.** Cn_β changes sign near α ≈ 25° in Morelli and ≈ 31° in the NESC tables; at α 30° the signs differ | Maverick equals Table 3 | same; **physically significant** (departure onset ≈ 6° α earlier in Maverick's model) |
| D-F4 | **Trim and thrust at case 11.** At equal lift and zero pitching moment, Morelli trims at **α 1.906°** (NESC 2.639°, −0.733°), elevator **−4.055°** (NESC −3.233°), and needs **1 719.9 lbf** of thrust (NESC 2 364.7 lbf, **−27 %**). The production `MavF16TrimReference` gives α 1.923°, δe −4.064°, 1 719.1 lbf (status `ConvergedButThrustUnavailable`; its own g = 9.80665 and atmosphere) | consequence of D-F1/D-F2 | same |

**Why "or source-data difference".** Morelli (1998, §3; lead-verified) fitted "a slightly simplified version" of the TP-1538 data given in Stevens & Lewis App. A. NESC Vol II §B.1.5 (lead-verified) states that Garza & Morelli *expanded* the Stevens & Lewis model "with additional wind-tunnel data" from TP-1538 before NASA converted it. The tables NESC uses may therefore not be the tables Morelli fitted. Separating a fit error from a dataset difference needs the Stevens & Lewis App. A tables, which were not inspected. **None of D-F1…D-F4 is a Maverick implementation defect.** Each is a property of the published compact model relative to NASA's tables, and each changes physical behaviour (cruise drag and energy, trim attitude, high-α directional stability).

**What was not done:** Maverick's aerodynamic coefficients were not changed or tuned toward NESC.

---

## 6. Case 11 trajectory

**NOT RUN.** A 180-s trajectory comparison requires (a) a rotating WGS-84 Earth or explicit removal of Coriolis, transport-rate and curvature effects (≈ 790 ft lateral at 180 s by estimate), (b) the NESC table aerodynamics and steady-state thrust as a selectable validation profile in Maverick, and (c) for cases 13.x the check-case LQR controller ported as a validation-only law. None exists today; building them was out of scope for R1. The static trim above is the part of case 11 that could be compared safely. See `VALIDATION_LIMITATIONS.md`.

---

## 7. Reproduce

`Unity.exe … -fdmSuite nasa_f16 -fdmMode sync -fdmType MavNasaF16StaticAeroValidation -fdmMethod RunAll -fdmOut <json> -logFile <log> -nescF16AeroDml <dir>/F16_package/F16_S119_source/F16_aero.dml` (`F16_prop.dml` and `F16_inertia.dml` must sit next to it). Without the file the suite reports NOT RUN as a failure.
