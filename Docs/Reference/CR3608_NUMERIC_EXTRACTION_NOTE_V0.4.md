# NASA-CR-3608 Numeric Extraction Note V0.4

Status: **BODY → WING → LEX COMPONENT BUILDUP COMPLETE AT BETA 0**

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

Current raw CR-3608 measurement total in Maverick: **857 rows**.

The A18–A22 block contains **227**, not 228, measurements because the source table at alpha=50 deg stops at `Omega*b/(2V)=+0.30`; no +0.40 row is printed. Maverick does not synthesize the missing point.

## A18–A22 extraction

Output:

`Docs/Reference/Data/FA18/CR3608_ROTARY_BODY_WING_LEX_BETA0_V0.1.csv`

Coverage:

- alpha: `0–90 deg` in 5-deg increments;
- beta: `0 deg`;
- nominal spin grid: `-0.40, -0.30, -0.20, -0.10, -0.05, 0.00, 0.00, +0.05, +0.10, +0.20, +0.30, +0.40`;
- source exception: alpha=50 deg has no printed +0.40 row;
- coefficients: `CA, CN, Cm, CY, Cl, Cn`.

The PDF embedded text has decimal/sign corruption, so all normalized values were checked against rendered source-page images A18–A22 before commit.

QA tag:

`PAGE_IMAGE_CROSSCHECKED_NOT_DOUBLE_KEYED`

A separate independent full transcription is still required before irreversible promotion to implementation authority.

## Derived LEX-increment product

A13–A17 and A18–A22 are matched component-build-up configurations:

`LEX increment = (Body + Wing + LEX) - (Body + Wing)`

Derived output:

`Docs/Reference/Data/FA18/CR3608_DERIVED_LEX_INCREMENT_BETA0_V0.1.csv`

Rows: **227**

The missing alpha=50 / +0.40 point remains missing in the derived product. It is not interpolated.

Classification:

- raw A18–A22 table: `PRIMARY_EXACT_MODEL_TEST` for the tested 1/10-scale configuration;
- derived LEX increment: `DERIVED_COMPONENT_INCREMENT`;
- full-scale HARV use: `CROSS_VALIDATION` / high-alpha rotary evidence.

The derived file is tagged:

`DERIVED_EXACT_ARITHMETIC_FROM_PAGE_IMAGE_CROSSCHECKED_SOURCE_TABLES`

## Component-build-up chain now available

At beta=0 Maverick can now query:

`Body`

→ `Body + Wing`

→ `Body + Wing + LEX`

and the exact matched-table arithmetic products:

`Wing increment = (Body + Wing) - Body`

`LEX increment = (Body + Wing + LEX) - (Body + Wing)`

This preserves the experiment's component-buildup design rather than collapsing all CR-3608 data into a single undifferentiated dataset.

## Current numeric products

Raw:

- `CR3608_ROTARY_BODY_BETA0_V0.1.csv` — 228 rows
- `CR3608_ROTARY_BODY_BETA10_V0.1.csv` — 174 rows
- `CR3608_ROTARY_BODY_WING_BETA0_V0.1.csv` — 228 rows
- `CR3608_ROTARY_BODY_WING_LEX_BETA0_V0.1.csv` — 227 rows

Derived:

- `CR3608_DERIVED_WING_INCREMENT_BETA0_V0.1.csv` — 228 rows
- `CR3608_DERIVED_LEX_INCREMENT_BETA0_V0.1.csv` — 227 rows

## Next extraction tranche

Table II identifies the next block as:

- Appendix: `A23–A27`
- configuration: `F-18 Body, Wing, LEX, horizontal tail`
- alpha: `0–90 deg`
- beta: `0 deg`
- horizontal-tail setting: `delta_H = 0 deg`

Once extracted, Maverick can derive:

`Horizontal-tail increment = (Body + Wing + LEX + horizontal tail) - (Body + Wing + LEX)`

with the same source-preserving arithmetic and QA rules.
