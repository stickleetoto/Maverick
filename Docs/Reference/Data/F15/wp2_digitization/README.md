# WP-2 digitization scripts — NASA F-15B 836 validation data

These scripts produced `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavF15Nasa836ValidationData.cs`
(60 series, 4,041 samples). The method, scope decisions and uncertainty are documented in
`Docs/Reference/F15_836_VALIDATION_DATA_V1.0.md`.

## Provenance of these files — read first

The digitization ran on 2026-09-24 from a session scratch folder. That folder was wiped while the
session was paused, before the scripts were committed. The files here were **reconstructed
verbatim from the session record** of what was written and run. The only differences:

- unused helpers were dropped (`polyline_points`, a first frame search that was overwritten);
- `gen_validation_data.py` takes its output path as an argument, and its header comment was changed
  to point here (the committed `.cs` header was edited to match).

Neither difference changes a number. The reconstructed scripts have **not been re-run**: the source
PDFs were lost with the scratch folder and have not been downloaded again. Re-running them against
the public PDFs and diffing the regenerated `.cs` would close that gap.

## Sources (public NASA reports)

| Report | NTRS id | Used |
|---|---|---|
| NASA/TM-2008-214634 | 20080015840 | figs. 12–13 (vector), figs. 14–15 (raster) |
| NASA/TM-2012-215978 | 20120013435 | figs. 27–29 (raster) |

## Run order

Requires Python 3 with `pymupdf`, `numpy`, `pillow`. Work in one folder holding the two PDFs.

```
python vec_extract.py 20080015840.pdf tm2008_fig12_13.json
python extract_images.py 20080015840.pdf 20120013435.pdf
python digitize_panels.py fig14.json
python digitize_panels.py fig15.json
python make_quad_specs.py
python gen_validation_data.py MavF15Nasa836ValidationData.cs
```

Every raster step writes an `*_overlay.png` with a ring on each kept sample.
- **Inspected by eye during the run:** TM-2008 figs. 14 and 15, TM-2012 figs. 27(a) and 29(a).
- **Checked only by calibration residual, grid-line count and value range:** the other four TM-2012
  images.

Those checks, and the grid-line calibration residual each step prints, are the verification.

## What the scripts decide, and what a person decided

- **Scripted:** marker shape → altitude (from the figure legend), axis calibration on grid lines,
  colour classification, sampling every 0.1 s, and omission of hidden or ambiguous samples. An
  omitted sample is never filled.
- **Read by a person from the rendered pages and written into the specs:**
  - frame pixel boxes, axis end labels and intermediate grid values;
  - which fig. 13 supersonic trend line is which altitude. This was confirmed by nearest-marker
    distance before being written in.
