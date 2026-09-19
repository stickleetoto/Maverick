# NASA-CR-3608 Numeric Extraction Note V0.3

Status: **BODY / BODY+WING BUILDUP COMPLETE AT BETA 0; BODY BETA 10 COMPLETE**

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

Current raw CR-3608 measurement total in Maverick: **630 rows**.

All three tranches preserve repeated zero-spin measurements instead of averaging them.

## New A13–A17 extraction

Output:

`Docs/Reference/Data/FA18/CR3608_ROTARY_BODY_WING_BETA0_V0.1.csv`

Coverage:

- alpha: `0–90 deg` in 5-deg increments;
- beta: `0 deg`;
- spin coefficient: `Omega*b/(2V) = -0.40, -0.30, -0.20, -0.10, -0.05, 0.00, 0.00, +0.05, +0.10, +0.20, +0.30, +0.40`;
- coefficients: `CA, CN, Cm, CY, Cl, Cn`;
- 19 alpha stations × 12 source rows = **228 rows**.

The embedded text layer contains recurring decimal/sign corruption. Every normalized row in A13–A17 was therefore checked against rendered source page images before commit.

QA tag:

`PAGE_IMAGE_CROSSCHECKED_NOT_DOUBLE_KEYED`

An independent second transcription has not yet been performed.

## Derived wing-increment product

Because A2–A6 and A13–A17 use the same beta=0 alpha/spin grid, Maverick can compute the component-build-up increment:

`Wing increment = (Body + Wing) - Body`

Derived output:

`Docs/Reference/Data/FA18/CR3608_DERIVED_WING_INCREMENT_BETA0_V0.1.csv`

Rows: **228**

This is exact arithmetic on the two extracted source tables. It is **not** a separately measured aerodynamic database and is labeled:

`DERIVED_EXACT_ARITHMETIC_FROM_PAGE_IMAGE_CROSSCHECKED_SOURCE_TABLES`

The derived product exists to make component-build-up analysis queryable while preserving both raw source tables as the authority.

## Authority boundary

CR-3608 is a **1/10-scale Langley Spin Tunnel rotary-balance experiment**.

The raw tables are:

- `PRIMARY_EXACT_MODEL_TEST` for their tested scale-model configurations;
- high-value `CROSS_VALIDATION` / component-build-up evidence for full-scale F/A-18/HARV work.

The derived wing increment is:

- `DERIVED_COMPONENT_INCREMENT`;
- valid only as the arithmetic difference between the two matched scale-model test configurations.

None of these products should be relabeled as exact full-scale `NASA_F18_HARV_160780_PHASE1_BASIC` coefficients.

## Why this tranche matters

A13–A17 is the first component-build-up dataset beyond the isolated body. With A2–A6 already extracted, the repository can now inspect how adding the wing changes:

- axial and normal force;
- pitching moment;
- side force;
- rolling moment;
- yawing moment;

as functions of both alpha and normalized rotation rate.

This is more useful than treating CR-3608 as a monolithic spin dataset because it preserves the experiment's component-separation design.

## Next extraction tranche

According to the Appendix configuration index:

- Appendix: `A18–A22`
- configuration: `F-18 Body, Wing, LEX`
- beta: `0 deg`

This will allow the next matched buildup:

`LEX increment = (Body + Wing + LEX) - (Body + Wing)`

using the same provenance discipline.
