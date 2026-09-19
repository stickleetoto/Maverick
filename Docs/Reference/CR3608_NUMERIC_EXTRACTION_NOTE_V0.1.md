# NASA-CR-3608 Numeric Extraction Note V0.1

Status: **FIRST NUMERIC TRANCHE — BODY, BETA 0, APPENDIX A2–A6**

Source:

- NASA Contractor Report 3608
- Randy Hultberg, *Low Speed Rotary Aerodynamics of F-18 Configuration for 0° to 90° Angle of Attack — Test Results and Analysis*
- NTRS: https://ntrs.nasa.gov/citations/19870001403
- Maverick source ID: `NASA_CR_3608`

## Scope

This extraction covers the first complete Appendix configuration block:

- configuration: `F-18 Body`
- beta: `0 deg`
- Appendix pages: `A2–A6`
- PDF pages: `65–69`
- alpha: `0–90 deg`, 5-deg spacing
- spin coefficient: `Omega*b/(2V) = -0.40, -0.30, -0.20, -0.10, -0.05, 0.00, 0.00, +0.05, +0.10, +0.20, +0.30, +0.40`
- coefficients: `CA, CN, Cm, CY, Cl, Cn`
- rows: **228**

The two source rows at zero spin coefficient are intentionally preserved as separate measurements using `zero_replicate=1/2`.

Output:

`Docs/Reference/Data/FA18/CR3608_ROTARY_BODY_BETA0_V0.1.csv`

## Extraction method

The PDF contains an imperfect embedded text layer. Extraction therefore used:

1. fixed-column parsing of the embedded text;
2. source-defined numeric precision for coefficient normalization;
3. page-image cross-checking for OCR-corrupted digits and dropped minus signs on Appendix pages A2–A6;
4. explicit preservation of duplicate zero-spin rows.

The resulting CSV is marked:

`PAGE_IMAGE_CROSSCHECKED_NOT_DOUBLE_KEYED`

This means the page images were checked against the normalized extraction, but a second independent human-style transcription has **not** yet been performed. The data are suitable for research analysis and further QA, but should not be promoted to irreversible implementation authority without a second-pass verification.

## Configuration authority

CR-3608 used a **1/10-scale F-18 model** on the Langley Spin Tunnel rotary balance. The extracted values are therefore:

- `PRIMARY_EXACT_MODEL_TEST` for this scale-model configuration;
- `CROSS_VALIDATION` / high-alpha rotary evidence for the full-scale HARV target.

Do not silently relabel these coefficients as exact full-scale `NASA_F18_HARV_160780_PHASE1_BASIC` values.

## Immediate observations from this tranche

The numeric block makes several source-level trends directly queryable:

- low-alpha body roll/yaw behavior can now be evaluated as a function of rotation rate;
- the body contribution evolves strongly with alpha, including large high-alpha side-force/yawing-moment behavior;
- the full `alpha × Omega*b/(2V)` body grid is now machine-readable instead of figure-only.

These observations do not replace the report's own interpretation or establish scale transfer.

## Next extraction tranche

According to the Appendix configuration index, the next block is:

- Appendix `A7–A12`
- configuration: `F-18 Body`
- beta: `10 deg`

Continue the same schema and QA rules before moving into body-wing / LEX / tail buildup configurations.
