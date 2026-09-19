# NASA-CR-3608 Numeric Extraction Note V0.2

Status: **TWO BODY-CONFIGURATION TRANCHES COMPLETE — BETA 0 AND BETA 10**

Source:

- NASA Contractor Report 3608
- Randy Hultberg, *Low Speed Rotary Aerodynamics of F-18 Configuration for 0° to 90° Angle of Attack — Test Results and Analysis*
- NTRS: https://ntrs.nasa.gov/citations/19870001403
- Maverick source ID: `NASA_CR_3608`

The report states that all measured rotary-balance data are tabulated in the Appendix. This extraction keeps the source's model configuration and test-grid boundaries explicit.

## Completed tranches

### Tranche 1 — F-18 Body, beta = 0 deg

- Appendix pages: `A2–A6`
- PDF pages: `65–69`
- alpha: `0–90 deg`, 5-deg spacing
- rows: **228**
- output: `Data/FA18/CR3608_ROTARY_BODY_BETA0_V0.1.csv`
- QA: page-image cross-checked; not independently double-keyed

### Tranche 2 — F-18 Body, beta = 10 deg

- Appendix pages: `A7–A12`
- PDF pages: `70–75`
- alpha: `0–90 deg`, 5-deg spacing
- unique measurement rows: **174**
- output: `Data/FA18/CR3608_ROTARY_BODY_BETA10_V0.1.csv`
- coefficients: `CA, CN, Cm, CY, Cl, Cn`
- beta: `10 deg`

The beta=10 block does **not** use a uniform spin-rate grid at every alpha. The source contains:

- 10 rows at alpha 0–25 deg;
- 9 rows at alpha 30–35 deg;
- 8 rows at alpha 40–70 deg;
- 10 rows at alpha 75–90 deg.

No missing spin-rate rows were synthesized.

As in the beta=0 tranche, the two source rows at zero spin coefficient are retained separately with `zero_replicate=1/2`.

## Appendix duplication used as internal QA

Appendix pages A10–A12 repeat portions of earlier beta=10 tables. The repeated values were used as a source-internal cross-check rather than being ingested as additional measurements.

Rows backed by an exact repeated Appendix occurrence are marked:

`PAGE_IMAGE_CROSSCHECKED_DUPLICATE_APPENDIX_MATCH`

Rows with only one Appendix occurrence are marked:

`PAGE_IMAGE_CROSSCHECKED_SINGLE_APPENDIX_OCCURRENCE`

The beta=10 CSV contains **88 rows** with a repeated Appendix occurrence and **86 rows** with a single occurrence.

Primary/duplicate page provenance is preserved in separate columns rather than silently deduplicating the source history.

## Extraction and QA method

The PDF text layer has recurring OCR defects, including:

- dropped decimal points;
- dropped minus signs;
- character substitutions such as `I`/1 and `O`/0;
- malformed coefficient labels.

For the completed tranches:

1. the embedded fixed-column text was used as an initial transcription aid;
2. all normalized rows were checked against rendered source page images;
3. source precision was retained;
4. zero-spin replicate rows were preserved;
5. repeated Appendix rows were compared and recorded as duplicate-source provenance, not as new measurements.

These CSVs remain research-data products. They should receive an independent second-pass verification before being promoted to irreversible implementation authority.

## Authority boundary

CR-3608 used a **1/10-scale F-18 model** on the Langley Spin Tunnel rotary balance.

Therefore:

- exact authority: `PRIMARY_EXACT_MODEL_TEST` for the tested scale-model configuration;
- full-scale use: `CROSS_VALIDATION` / high-alpha rotary evidence.

Do not relabel these values as exact full-scale `NASA_F18_HARV_160780_PHASE1_BASIC` coefficients.

## Current extracted total

- beta=0 body tranche: **228 rows**
- beta=10 body tranche: **174 rows**
- total CR-3608 rows currently machine-readable in Maverick: **402**

## Next extraction tranche

From the Appendix configuration index:

- Appendix: `A13–A17`
- configuration: `F-18 Body, Wing`
- beta: `0 deg`

This is the first component-build-up step beyond the isolated body and is useful for separating the wing contribution to rotary aerodynamics.
