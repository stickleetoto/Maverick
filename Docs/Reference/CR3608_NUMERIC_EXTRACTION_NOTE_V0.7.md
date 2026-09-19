# NASA-CR-3608 Numeric Extraction Note V0.7

Status: **FIRST FULL BASIC F-18 ROTARY DATASET COMPLETE — A33–A39**

Source:

- NASA Contractor Report 3608
- Randy Hultberg, *Low Speed Rotary Aerodynamics of F-18 Configuration for 0° to 90° Angle of Attack — Test Results and Analysis*
- NTRS: https://ntrs.nasa.gov/citations/19870001403
- Maverick source ID: `NASA_CR_3608`

## Completed raw measurement tranches

| Tranche | Appendix | PDF pages | Configuration | Beta | Rows |
|---|---:|---:|---|---:|---:|
| 1 | A2–A6 | 65–69 | F-18 Body | 0 deg | 228 |
| 2 | A7–A12 | 70–75 | F-18 Body | 10 deg | 174 |
| 3 | A13–A17 | 76–80 | F-18 Body, Wing | 0 deg | 228 |
| 4 | A18–A22 | 81–85 | F-18 Body, Wing, LEX | 0 deg | 227 |
| 5 | A23–A27 | 86–90 | F-18 Body, Wing, LEX, Horizontal | 0 deg | 227 |
| 6 | A28–A32 | 91–95 | F-18 Body, Wing, LEX, Vertical | 0 deg | 226 |
| 7 | A33–A39 | 96–102 | **Basic F-18** | 0 deg | **266** |

Current raw CR-3608 measurement total in Maverick: **1,576 rows**.

## A33–A39 extraction

Output:

`Docs/Reference/Data/FA18/CR3608_ROTARY_BASIC_F18_ALL_CONTROLS0_BETA0_V0.1.csv`

Table II identifies this block as the **Basic F-18** configuration with:

- beta: `0 deg`
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

The two zero-spin rows at every alpha are preserved separately using `zero_replicate=1/2`.

Unlike several preceding buildup blocks, A33–A39 contains the complete printed nominal grid at every alpha. No rows were synthesized.

## QA

The PDF embedded text layer was used as a fixed-column transcription aid, but it contains recurring OCR substitutions and sign/decimal corruption.

For A33–A39:

1. the source pages were re-rendered from the original PDF at high resolution;
2. every normalized row was checked against the rendered page image;
3. source coefficient precision was retained;
4. duplicated zero-spin measurements were preserved;
5. no interpolation or smoothing was applied.

QA tag:

`PAGE_IMAGE_CROSSCHECKED_NOT_DOUBLE_KEYED`

This is a research-quality extraction. A fully independent second transcription remains recommended before irreversible use as implementation authority.

## Authority boundary

The A33–A39 table is the report's **Basic F-18** 1/10-scale rotary-balance configuration.

Classification:

- `PRIMARY_EXACT_MODEL_TEST` for the tested scale-model configuration;
- `CROSS_VALIDATION` / low-speed rotary and high-alpha evidence for full-scale F/A-18/HARV modeling.

It is not the missing full-scale A7247/A8575 nonlinear aerodynamic lookup package and must not be relabeled as such.

## Component-buildup context

The earlier tranches allow component analysis:

- Body
- Body + Wing
- Body + Wing + LEX
- horizontal-tail branch
- vertical-tail branch

A33–A39 is different: it is the report's complete **Basic F-18** block. It is therefore preserved as its own configuration authority rather than treated as a simple arithmetic continuation of the component-buildup chain.

Existing derived data remain:

- Wing increment — 228 rows
- LEX increment — 227 rows
- Horizontal-tail increment — 227 rows
- Vertical-tail increment — 226 rows

Current derived-data total: **908 rows**.

## Current CR-3608 machine-readable total

- raw measured rows: **1,576**
- derived component rows: **908**
- combined research rows/products: **2,484**

## Next extraction tranche

The next Appendix block after A33–A39 should be reviewed against Table II before ingestion. The extraction procedure remains:

1. freeze the exact configuration/control setting;
2. render and page-check the source tables;
3. ingest raw measurements without filling missing points;
4. derive differences only where matched test grids and configuration logic make the subtraction physically meaningful.
