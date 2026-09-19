# NASA-CR-3608 Numeric Extraction Note V0.6

Status: **BODY → WING → LEX COMPONENT BUILDUP COMPLETE; HORIZONTAL AND VERTICAL TAIL BRANCHES COMPLETE AT BETA 0**

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

Current raw CR-3608 measurement total in Maverick: **1,310 rows**.

## A28–A32 extraction

Output:

`Docs/Reference/Data/FA18/CR3608_ROTARY_BODY_WING_LEX_VERTICAL_DR0_BETA0_V0.1.csv`

Table II identifies this configuration as:

- `Body, wing, LEX, vert.`
- alpha range: `0–90 deg`
- beta: `0 deg`
- rudder: `delta_r = 0 deg`

Coverage:

- alpha: `0–90 deg` in 5-deg increments;
- nominal spin grid: `-0.40, -0.30, -0.20, -0.10, -0.05, 0.00, 0.00, +0.05, +0.10, +0.20, +0.30, +0.40`;
- coefficients: `CA, CN, Cm, CY, Cl, Cn`.

Source-table exceptions:

- alpha=45 deg has no printed `Omega*b/(2V)=+0.40` row;
- alpha=50 deg has no printed `Omega*b/(2V)=+0.40` row.

No missing values were inferred or interpolated. This yields **226** raw rows.

All normalized values were checked against rendered source-page images A28–A32 because the embedded PDF text layer contains recurring decimal/sign errors.

QA tag:

`PAGE_IMAGE_CROSSCHECKED_NOT_DOUBLE_KEYED`

Independent second transcription is still pending.

## Derived vertical-tail increment

A18–A22 provides the matched `Body + Wing + LEX` baseline. Maverick derives:

`Vertical-tail increment = (Body + Wing + LEX + Vertical) - (Body + Wing + LEX)`

Output:

`Docs/Reference/Data/FA18/CR3608_DERIVED_VERTICAL_INCREMENT_DR0_BETA0_V0.1.csv`

Rows: **226**

The two missing source points remain missing in the derived product.

Classification:

- raw A28–A32 table: `PRIMARY_EXACT_MODEL_TEST` for the tested 1/10-scale configuration;
- derived vertical-tail increment: `DERIVED_COMPONENT_INCREMENT`;
- full-scale HARV use: `CROSS_VALIDATION` / high-alpha rotary evidence.

Derived QA tag:

`DERIVED_EXACT_ARITHMETIC_FROM_PAGE_IMAGE_CROSSCHECKED_SOURCE_TABLES`

## Component-build-up products now available

At beta=0:

`Body`

→ `Body + Wing`

→ `Body + Wing + LEX`

with two tail branches:

- `Body + Wing + LEX + Horizontal (delta_H=0)`
- `Body + Wing + LEX + Vertical (delta_r=0)`

Derived products:

- Wing increment — **228 rows**
- LEX increment — **227 rows**
- Horizontal-tail increment — **227 rows**
- Vertical-tail increment — **226 rows**

Current derived-data total: **908 rows**.

## Next extraction tranche

Table II identifies the next block as:

- Appendix: `A33–A39`
- configuration: **Basic F-18**
- alpha: `0–90 deg`
- beta: `0 deg`
- `delta_H = 0 deg`
- `delta_a = 0 deg`
- `delta_r = 0 deg`
- `delta_d = 0 deg`
- `delta_f = 0 deg`

This is the first full **Basic F-18** rotary-balance block and should be preserved as a separate configuration authority rather than treated as a simple additive continuation of the component-build-up chain.
