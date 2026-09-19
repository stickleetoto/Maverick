# NASA-CR-3608 Numeric Extraction Note V0.9

Status: **BASIC F-18 BETA 0 / +10 / -10 SET COMPLETE; CENTERED BETA PRODUCTS ADDED**

Source:

- NASA Contractor Report 3608
- Randy Hultberg, *Low Speed Rotary Aerodynamics of F-18 Configuration for 0° to 90° Angle of Attack — Test Results and Analysis*
- NTRS: https://ntrs.nasa.gov/citations/19870001403
- Maverick source ID: `NASA_CR_3608`

## Newly completed raw tranche

### A47–A52 — Basic F-18, beta = -10 deg

Output:

`Docs/Reference/Data/FA18/CR3608_ROTARY_BASIC_F18_ALL_CONTROLS0_BETA_NEG10_V0.1.csv`

Configuration:

- **Basic F-18**
- beta: `-10 deg`
- `delta_H = 0 deg`
- `delta_a = 0 deg`
- `delta_r = 0 deg`
- `delta_d = 0 deg`
- `delta_f = 0 deg`

Coverage:

- alpha: `0–90 deg`
- coefficients: `CA, CN, Cm, CY, Cl, Cn`
- zero-spin duplicate measurements preserved
- total raw rows: **260**

The beta=-10 source does not print the `Omega*b/(2V)=-0.40` row for alpha 15, 20, 25, 30, 35, and 40 deg. Those six points remain absent; Maverick does not synthesize them.

All normalized values were checked against rendered source page images A47–A52 after using the PDF text layer only as a transcription aid.

QA tag:

`PAGE_IMAGE_CROSSCHECKED_NOT_DOUBLE_KEYED`

## Basic F-18 beta-family now available

Maverick now has:

- beta=0 deg — A33–A39 — **266 rows**
- beta=+10 deg — A40–A46 — **266 rows**
- beta=-10 deg — A47–A52 — **260 rows**

The three datasets share the same Basic F-18 zero-control configuration and largely matched alpha / spin grids.

## Derived centered beta slope

Output:

`Docs/Reference/Data/FA18/CR3608_DERIVED_BASIC_F18_BETA_CENTERED_SLOPE_V0.1.csv`

Matched rows: **260**

For each matched alpha / spin / zero-replicate row:

`dC/dβ ≈ [C(+10 deg) - C(-10 deg)] / (20 deg in radians)`

The file contains per-radian centered finite-difference slopes for:

- `CA`
- `CN`
- `Cm`
- `CY`
- `Cl`
- `Cn`

This is **not** a derivative reported by CR-3608. It is a Maverick-derived finite-difference product and is labeled accordingly.

QA tag:

`DERIVED_MATCHED_GRID_FROM_PAGE_IMAGE_CROSSCHECKED_SOURCE_TABLES`

## Derived beta-midpoint consistency residual

Output:

`Docs/Reference/Data/FA18/CR3608_DERIVED_BASIC_F18_BETA_MIDPOINT_RESIDUAL_V0.1.csv`

Matched rows: **260**

For each matched row:

`residual = 0.5 * [C(+10 deg) + C(-10 deg)] - C(0 deg)`

This product is intended as a local symmetry / nonlinearity / cross-table consistency diagnostic. A nonzero residual is not automatically an extraction error; it can reflect real beta nonlinearity, asymmetry, repeatability, or test differences.

## Authority boundary

All A33–A52 Basic F-18 raw data remain:

- `PRIMARY_EXACT_MODEL_TEST` for the CR-3608 1/10-scale rotary-balance configuration;
- `CROSS_VALIDATION` / rotary high-alpha evidence for full-scale F/A-18/HARV modeling.

The centered slope and midpoint residual products are **derived analysis products**, not source-reported coefficients or the missing A7247/A8575 aerodynamic deck.

## Current CR-3608 machine-readable totals

Raw measured rows:

- previous total through A46: **1,842**
- A47–A52 beta=-10: **260**
- **current raw total: 2,102**

Derived rows:

- component buildup products: **908**
- centered beta slopes: **260**
- beta midpoint residuals: **260**
- **current derived total: 1,428**

Combined machine-readable research rows/products: **3,530**

## Next source block

Table II identifies the next sequence as:

- A53–A57 — **Basic minus LEX**, beta=0
- A58–A62 — **Basic minus LEX**, beta=+10
- A63–A67 — **Basic with vertical tails shifted aft 5.75 in**, beta=0
- A68–A72 — same shifted-vertical configuration, beta=+10

The next high-value extraction is A53–A57. It can be compared with the Basic F-18 beta=0 table to form a second, configuration-level LEX contribution estimate distinct from the earlier component-build-up LEX subtraction.
