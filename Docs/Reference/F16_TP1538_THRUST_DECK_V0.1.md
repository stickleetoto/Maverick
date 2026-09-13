# F-16 TP-1538 Thrust Deck v0.1

Status: **FROZEN DATA / PROVENANCE ONLY — NOT INTEGRATED**

This document freezes the public thrust table in NASA TP-1538 for the Maverick NASA Reference F-16.
It does **not** enable powered flight and does **not** connect the table to any production propulsion
or rigid-body component.

## 1. Authority and exact source

- Authority: **NASA Technical Paper 1538 (NASA-TP-1538)**
- NTRS record: **19800005879**
- Title: *Simulator Study of Stall/Post-Stall Characteristics of a Fighter Airplane With Relaxed Longitudinal Static Stability*
- Authors: Luat T. Nguyen, Marilyn E. Ogburn, William P. Gilbert, Kemper S. Kibler,
  Phillip W. Brown, and Perry L. Deal
- Publication: **December 1979**
- Relevant context: Appendix B, **Engine Simulation**
- Table: **TABLE VI.- THRUST VALUES USED IN SIMULATION**
- Visually verified scan location: **PDF page 99 / 233**
- Logical report page: **93**. The scan footer on PDF page 99 is clipped; PDF page 100 is visibly
  report page 94 and begins the figures section.
- Acquired PDF SHA-256: `aae0ece64474291368c0b4c816d3ab327c6100329e6eb030c2f4545d0913feb3`

The front matter of the acquired 233-page PDF visually identifies the same NASA Technical Paper 1538,
title, authors, and 1979 publication as NTRS record 19800005879. NASA/NTRS remains the provenance
authority; the locally supplied PDF is the visual-access copy used for transcription.

## 2. What Table VI contains

The table has two printed representations of the same deck:

- `(a) SI Units`: altitude in metres and thrust in newtons.
- `(b) U.S. Customary Units`: altitude in feet and thrust printed in `lb` (interpreted as pound-force
  because the quantity is thrust).
- The left independent-variable column is printed as `m`; the report's symbol list defines Mach number
  as `M`, and the row grid is `0.2, 0.4, 0.6, 0.8, 1.0`.
- Altitude grid:
  - SI: `0, 3048, 6096, 9144, 12192, 15240 m`
  - U.S. customary: `0, 10000, 20000, 30000, 40000, 50000 ft`
- Power-state blocks: `Tidle`, `Tmil`, and `Tmax`.

The Appendix B engine-simulation text states that the F-16 is represented with an afterburning turbofan
and explicitly points to Table VI for idle, military, and maximum thrust values.

## 3. Manual transcription and double verification

**OCR/search-index values were not used as numeric authority.**

Pass A transcribed every cell manually from a 400 dpi PDFium render of PDF page 99.
Pass B re-checked every cell against a separate 500 dpi Poppler/pdftoppm render of the same original
page.

Accepted result:

- 2 printed unit sections
- 30 Mach/altitude coordinates per section
- 90 thrust cells per section
- **180 raw printed thrust cells checked visually**
- **0 ambiguous/unavailable cells**
- **0 transcription disagreements between visual pass A and visual pass B**

There are **12 negative thrust cells in each printed section**, all in `Tidle`. These minus signs are
visibly present in the source and are preserved rather than “corrected” or clamped.

Raw transcription:
`Docs/Reference/Data/F16/TP1538/table_vi_raw_source.csv`

## 4. SI / U.S. customary cross-check

The report states that its measurements and calculations were made in U.S. Customary Units and that
SI values are also presented. The two halves of Table VI provide an unusually strong internal check.

Altitude conversion is exact on the printed grid:

```text
10000 ft * 0.3048 m/ft = 3048 m
...
50000 ft * 0.3048 m/ft = 15240 m
```

For thrust, the paired published table is internally consistent with:

```text
1 lb(force) -> 4.448 N
```

followed by rounding to the nearest integer newton for the printed SI half.

This rule reproduces **all 90 printed SI thrust cells with 0 mismatches**.
The largest absolute difference before integer rounding is **0.480 N**.

Independent hand checks:

```text
Idle, M 0.2, 0 ft:
635 lb * 4.448 = 2824.480 N -> printed 2824 N

Idle, M 0.8, 10000 ft:
-1900 lb * 4.448 = -8451.200 N -> printed -8451 N

Military, M 0.6, 30000 ft:
4660 lb * 4.448 = 20727.680 N -> printed 20728 N

Maximum, M 1.0, 50000 ft:
5057 lb * 4.448 = 22493.536 N -> printed 22494 N

Maximum, M 1.0, 0 ft:
28886 lb * 4.448 = 128484.928 N -> printed 128485 N
```

The project does **not** rewrite the published SI integers using the modern exact lbf definition. The
raw file preserves both printed source sections. A separate derived-SI file uses the table-consistent
`4.448 N/lbf` conversion at 0.001 N resolution:

`Docs/Reference/Data/F16/TP1538/table_vi_si_converted.csv`

## 5. Deterministic provenance and hashes

Frozen file hashes:

- raw source CSV: `9e0d9906daec75797560e37c6ae490ec10630495658ad9a93216ddacdc940972`
- derived SI CSV: `f32592f99eb30de58c1fc42c02ad984ce3b371bbea3db8e369775b05becb8b47`
- provenance manifest: `db776f5e5503d615025e1ee277fbbf2e731fc475fc30c7a6aeb52f357483fa49`

The manifest records the acquired source-PDF hash, table identity, grids, units, verification method,
conversion rule, row/cell counts, negative-cell policy, and integration status:

`Docs/Reference/Data/F16/TP1538/manifest.json`

Serialization is intentionally simple and deterministic: UTF-8, LF line endings, fixed CSV row order,
and sorted-key/2-space-indented JSON for the manifest.

## 6. Validation

`Tools/validate_f16_tp1538_thrust_deck.py` validates:

- schema and column order;
- expected Mach and altitude grids;
- exactly 60 raw coordinate rows and 180 raw thrust cells;
- exactly 30 derived SI coordinate rows and 90 derived thrust cells;
- no duplicate coordinates;
- finite numeric values;
- negative values only where explicitly present in the source idle block;
- exact ft -> m grid conversion;
- all 90 U.S. customary -> printed-SI thrust cross-checks;
- derived SI conversion consistency;
- raw and derived data SHA-256 values against the manifest;
- canonical manifest serialization stability.

## 7. Integration boundary

This branch is a **data freeze only**.

It does not connect the deck to:

- `MavEngineProfile`
- `MavPropulsionSystem`
- `MavSixDoFBody`
- gameplay aircraft
- scenes or prefabs

No shared FDM Core source is modified. Production F-16 dimensional thrust therefore remains
**exactly 0 N** until a separate integration phase deliberately consumes this frozen dataset.

## 8. Freeze verdict

**F16 TP-1538 DATA: FROZEN**

The freeze applies to the transcription/provenance of TP-1538 Table VI only. It is not a claim that
the TP-1538 research-simulation engine installation exactly represents every operational F-16 variant,
and it does not establish interpolation or extrapolation policy beyond the source grid.
