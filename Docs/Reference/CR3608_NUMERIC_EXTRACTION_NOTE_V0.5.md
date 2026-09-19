# NASA-CR-3608 Numeric Extraction Note V0.5

Status: **BODY → WING → LEX → HORIZONTAL-TAIL BUILDUP COMPLETE AT BETA 0**

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

Current raw CR-3608 measurement total in Maverick: **1,084 rows**.

The A23–A27 configuration is identified by Table II as:

- alpha range: `0–90 deg`;
- beta: `0 deg`;
- horizontal-tail setting: `delta_H = 0 deg`.

The source again omits the alpha=50 deg / `Omega*b/(2V)=+0.40` point, so the tranche contains **227**, not 228, rows. Maverick does not synthesize it.

## A23–A27 extraction

Output:

`Docs/Reference/Data/FA18/CR3608_ROTARY_BODY_WING_LEX_HORIZONTAL_DH0_BETA0_V0.1.csv`

Coverage:

- alpha: `0–90 deg` in 5-deg increments;
- beta: `0 deg`;
- horizontal tail: `delta_H = 0 deg`;
- nominal spin grid: `-0.40, -0.30, -0.20, -0.10, -0.05, 0.00, 0.00, +0.05, +0.10, +0.20, +0.30, +0.40`;
- source exception: alpha=50 deg lacks the +0.40 row;
- coefficients: `CA, CN, Cm, CY, Cl, Cn`.

All normalized values were checked against rendered source-page images for A23–A27 because the embedded text contains recurring OCR sign/decimal corruption.

QA tag:

`PAGE_IMAGE_CROSSCHECKED_NOT_DOUBLE_KEYED`

Independent second transcription is still pending.

## Derived horizontal-tail increment

A18–A22 and A23–A27 use the same beta=0 alpha/spin grid, including the same missing alpha=50/+0.40 point. Therefore Maverick derives:

`Horizontal-tail increment = (Body + Wing + LEX + Horizontal) - (Body + Wing + LEX)`

Output:

`Docs/Reference/Data/FA18/CR3608_DERIVED_HORIZONTAL_INCREMENT_DH0_BETA0_V0.1.csv`

Rows: **227**

Classification:

- raw A23–A27 table: `PRIMARY_EXACT_MODEL_TEST` for the tested 1/10-scale configuration;
- derived horizontal-tail increment: `DERIVED_COMPONENT_INCREMENT`;
- full-scale HARV use: `CROSS_VALIDATION` / high-alpha rotary evidence.

The derived file is tagged:

`DERIVED_EXACT_ARITHMETIC_FROM_PAGE_IMAGE_CROSSCHECKED_SOURCE_TABLES`

## Component-build-up products now available

At beta=0:

`Body`

→ `Body + Wing`

→ `Body + Wing + LEX`

→ `Body + Wing + LEX + Horizontal (delta_H=0)`

Derived products:

- `Wing increment = (Body + Wing) - Body` — 228 rows
- `LEX increment = (Body + Wing + LEX) - (Body + Wing)` — 227 rows
- `Horizontal increment = (Body + Wing + LEX + Horizontal) - (Body + Wing + LEX)` — 227 rows

Current derived-data total: **682 rows**.

## Next extraction tranche

Table II identifies the next block as:

- Appendix: `A28–A32`
- configuration: `F-18 Body, Wing, LEX, Vertical`
- alpha: `0–90 deg`
- beta: `0 deg`
- rudder setting: `delta_r = 0 deg`

Once extracted, Maverick can derive:

`Vertical-tail increment = (Body + Wing + LEX + Vertical) - (Body + Wing + LEX)`

without mixing it with the horizontal-tail buildup branch.
