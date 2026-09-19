# NASA-CR-3608 Numeric Extraction Note V0.8

Status: **BASIC F-18 BETA 0 AND BETA +10 ROTARY DATASETS COMPLETE**

Source:

- NASA Contractor Report 3608
- Randy Hultberg, *Low Speed Rotary Aerodynamics of F-18 Configuration for 0° to 90° Angle of Attack — Test Results and Analysis*
- NTRS: https://ntrs.nasa.gov/citations/19870001403
- Maverick source ID: `NASA_CR_3608`

## Completed raw measurement tranches

| Tranche | Appendix | PDF pages | Configuration | Beta | Rows |
|---|---:|---:|---|---:|---:|
| 1 | A2–A6 | 65–69 | F-18 Body | 0 deg | 228 |
| 2 | A7–A12 | 70–75 | F-18 Body | +10 deg | 174 |
| 3 | A13–A17 | 76–80 | F-18 Body, Wing | 0 deg | 228 |
| 4 | A18–A22 | 81–85 | F-18 Body, Wing, LEX | 0 deg | 227 |
| 5 | A23–A27 | 86–90 | F-18 Body, Wing, LEX, Horizontal | 0 deg | 227 |
| 6 | A28–A32 | 91–95 | F-18 Body, Wing, LEX, Vertical | 0 deg | 226 |
| 7 | A33–A39 | 96–102 | Basic F-18 | 0 deg | 266 |
| 8 | A40–A46 | 103–109 | **Basic F-18** | **+10 deg** | **266** |

Current raw CR-3608 measurement total in Maverick: **1,842 rows**.

## A40–A46 extraction

Output:

`Docs/Reference/Data/FA18/CR3608_ROTARY_BASIC_F18_ALL_CONTROLS0_BETA10_V0.1.csv`

Table II identifies this block as the **Basic F-18** continuation at:

- beta: `+10 deg`
- `delta_H = 0 deg`
- `delta_a = 0 deg`
- `delta_r = 0 deg`
- `delta_d = 0 deg`
- `delta_f = 0 deg`

Coverage:

- alpha: `0–90 deg` in 5-deg increments;
- spin coefficient grid:
  `Omega*b/(2V) = -0.40, -0.30, -0.20, -0.10, -0.05, -0.03, 0.00, 0.00, +0.03, +0.05, +0.10, +0.20, +0.30, +0.40`;
- coefficients: `CA, CN, Cm, CY, Cl, Cn`;
- 19 alpha stations × 14 source rows = **266 measurements**.

The two zero-spin measurements at each alpha are preserved separately using `zero_replicate=1/2`.

No source points were synthesized or interpolated.

## QA

The PDF text layer contains recurring OCR faults, especially dropped minus signs and decimal corruption at high alpha. For A40–A46:

1. the embedded fixed-column text was used only as a transcription aid;
2. every normalized row was checked against the rendered source page;
3. source precision was retained;
4. repeated zero-spin measurements were preserved;
5. no smoothing/interpolation was applied.

Representative corrections made after page-image checking include:

- high-alpha `Cm` signs that were dropped by OCR;
- alpha=45 deg, spin +0.40: `Cl=-0.0194`;
- alpha=60 deg, spin +0.40: `Cn=-0.1712`;
- alpha=80 deg, zero-spin replicate 2: `Cn=-0.0330`;
- alpha=85 deg, spin +0.40: `Cn=-0.1186`.

QA tag:

`PAGE_IMAGE_CROSSCHECKED_NOT_DOUBLE_KEYED`

A fully independent second transcription remains recommended before irreversible implementation use.

## Authority boundary

A40–A46 is the report's 1/10-scale **Basic F-18** rotary-balance configuration at beta=+10 deg.

Classification:

- `PRIMARY_EXACT_MODEL_TEST` for the tested scale-model configuration;
- `CROSS_VALIDATION` / low-speed high-alpha rotary evidence for full-scale F/A-18/HARV modeling.

It is not the missing full-scale A7247/A8575 nonlinear aerodynamic lookup package.

## Basic F-18 matched-beta coverage

Maverick now has two complete Basic F-18 blocks on the same alpha/spin grid:

- beta=0 deg — A33–A39 — 266 rows
- beta=+10 deg — A40–A46 — 266 rows

This creates the positive-sideslip half of a matched beta comparison but does not yet justify a centered beta derivative.

## Current CR-3608 machine-readable total

- raw measured rows: **1,842**
- derived component rows: **908**
- combined research rows/products: **2,750**

## Next extraction tranche

Table II identifies the next block as:

- Appendix: `A47–A52`
- configuration: **Basic F-18**
- alpha: `0–90 deg`
- beta: `-10 deg`
- controls: same zero-control Basic F-18 configuration

Once A47–A52 is extracted, the matched beta=+10/-10 grids can support an explicitly derived centered finite-difference product:

`dC/dβ ≈ [C(+10°) - C(-10°)] / (20° in radians)`

Only exactly matched alpha/spin/replicate rows should be used, and the result must remain labeled as a derived finite-difference quantity rather than a source-reported derivative.
