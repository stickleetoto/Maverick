# F-15 — NASA 836 Validation Data (V1.0, WP-2)

**What this is:** exact-scope NASA F-15B 836 data digitized from held public NASA reports, stored as **OriginalPrimary × Exact836 × ValidationOnly**.

These are comparison **targets**:
- They are **never** model coefficients.
- Nothing in `F15/` or `Core/` may read them. `[V3]` scans for that.
- They cannot dimensionalize anything for 836 while `S`, `c̄` and `b` stay unavailable.

| | |
|---|---|
| Data | `Validation/MavF15Nasa836ValidationData.cs` — generated; 60 series, 4,041 samples |
| Types | `Validation/MavF15Nasa836ValidationSeries.cs` |
| Tests | `Validation/MavF15Nasa836ValidationDataValidation.cs`, `[V1]`–`[V5]`, 16 checks, PASS |
| Scripts | `Docs/Reference/Data/F15/wp2_digitization/` (see its README for the reconstruction note) |

---

## 1. Scope correction — what the held figures actually are

WP-2 was planned from the opportunities list, which called TM-2012-215978's derivative and CAS-off figures "baseline". **Read in full, they are not.**

| Figure set | What it is | Stored? |
|---|---|---|
| TM-2008-214634 figs. 12–13, **closed** symbols and solid trend lines | Cnβ and Cmα parameter estimates from the **baseline** flights — 836 with its standard air-data boom and an added sideslip vane (report pp.5–6) | **YES** |
| TM-2008-214634 figs. 14–15 | **baseline** flight vs baseline simulation time histories | **YES** |
| TM-2012-215978 figs. 27–29, the "F-15B" curve | **baseline** F-15B **simulation**, CAS off, at report table 2's three conditions | **YES** |
| TM-2008-214634 figs. 12–13, open symbols and dashed lines; figs. 16–19 | experimental-nose-boom (research) configuration | no — ResearchModified |
| TM-2012-215978 figs. 15–18, 20–23 (derivative borders) | **ordinate unscaled** (only "+", "0", "−" marks) **and** spike-extended estimates | no — no value can be read |
| TM-2012-215978 figs. 30–33 (CAS-off Dutch roll and short period) | flight estimates for the **spike-extended** airplane (report p.11); the bands are stress-analysis regions for that configuration at three fuel weights (legend 2,000 / 8,000 / 12,000) | no — not 836 baseline |
| TM-2012-215978 figs. 7–12, 19, 34–35 | spike-equipped flight / simulation / handling qualities | no |
| TM-2012-215978 figs. 24–26 | CAS-**on** baseline simulation | not this pass: exact scope, but the response depends on the unavailable FCS |

All of these are listed, with reasons, in `MavF15Nasa836ValidationData.Excluded`.

## 2. Datasets digitized

| Series group | Report / figure | Kind | Configuration | Flight condition as stated | n |
|---|---|---|---|---|---|
| `TM2008_F12_CnBeta_BASELINE_PE_*FT` (6) | TM-2008-214634 fig. 12, PDF p.19 | flight parameter estimate | 836 baseline | altitude by symbol; Mach per point; **α not stated** | 21 |
| `TM2008_F12_CnBeta_BASELINE_TREND_*` (2) | same | author trend line | 836 baseline | subsonic / supersonic; altitude not stated | 57 + 60 |
| `TM2008_F13_CmAlpha_BASELINE_PE_*FT` (6) | fig. 13, PDF p.20 | flight parameter estimate | 836 baseline | as fig. 12 | 21 |
| `TM2008_F13_CmAlpha_BASELINE_TREND_*` (4) | same | author trend line | 836 baseline | subsonic; supersonic at 30k / 40k / 45k ft (report p.16) | 58, 41, 62, 44 |
| `TM2008_F14_POPU_*` (7) | fig. 14, PDF p.21 | flight + open-loop simulation | 836 baseline | subsonic push-over / pull-up; **Mach, altitude, weight not stated** | 84–89 each |
| `TM2008_F15_RUDDER_SWEEP_*` (11) | fig. 15, PDF p.22 | flight + open-loop simulation | 836 baseline | supersonic rudder sweep; **Mach, altitude, weight not stated** | 69–79 each |
| `TM2012_F27*_COND1_*` (8) | TM-2012-215978 fig. 27, PDF p.36 | simulation | baseline F-15B, CAS off | **M 0.60, 25,000 ft**; fuel weight not stated | 90–98 each |
| `TM2012_F28*_COND2_*` (8) | fig. 28, PDF p.37 | simulation | same | **M 0.95, 35,000 ft** | 81–98 each |
| `TM2012_F29*_COND3_*` (8) | fig. 29, PDF p.38 | simulation | same | **M 1.80, 45,000 ft** | 71–97 each |

**Quantities and units**
- Mach (–), Cnβ and Cmα (1/deg), time (s).
- Angles in deg; rates in deg/s; accelerations in g.
- TM-2008 surfaces in deg: stabilator, rudder, aileron, differential stabilator.
- TM-2012 inputs: horizontal tail and rudder.

### Baseline flight estimates, figs. 12–13 (vector-exact positions)

| Altitude | Cnβ, 1/deg: (Mach, value) | Cmα, 1/deg: (Mach, value) |
|---|---|---|
| 15,000 ft | (0.4112, 0.0026207) (0.5049, 0.002637) (0.594, 0.002667) (0.6993, 0.0027106) (0.8112, 0.0031521) | (0.4009, −0.006355) (0.511, −0.006202) (0.6107, −0.006168) (0.7028, −0.006236) (0.7974, −0.007598) |
| 25,000 ft | (0.8048, 0.0030773) (0.9061, 0.0036111) (1.0978, 0.0034171) | (0.8001, −0.006995) (0.9001, −0.007575) (1.105, −0.023981) |
| 30,000 ft | (0.9509, 0.0039096) (1.1961, 0.0028592) (1.389, 0.001277) (1.5981, 0.0006552) | (0.9509, −0.012741) (1.1919, −0.023026) (1.3908, −0.018442) (1.5799, −0.015763) |
| 35,000 ft | (0.7971, 0.0030953) | (0.8018, −0.006575) |
| 40,000 ft | (1.197, 0.0033646) (1.2791, 0.0021968) (1.381, 0.0015637) (1.7029, 0.0007338) (1.7991, 0.0006193) | (1.191, −0.025191) (1.2821, −0.02331) (1.3908, −0.020347) (1.697, −0.014882) (1.8039, −0.014137) |
| 45,000 ft | (1.4035, 0.0015718) (1.5693, 0.0010276) (1.8103, 0.0006974) | (1.3784, −0.022095) (1.5905, −0.016848) (1.8103, −0.014873) |

The report calls these estimates "that produce the best match to the flight data when added to the six-degree-of-freedom simulation" (p.15).

## 3. Method and uncertainty

**Vector (figs. 12–13).** Both figures are PDF vector graphics, so positions come from the path geometry, not a raster.
- **Baseline markers:** black-filled symbols. Shape gives altitude, per the legend.
- **Axes:** calibrated on the plot's own grid lines. Maximum residual 1.4e-4 Mach, 4e-7 (Cnβ), 2.7e-6 (Cmα).
- **Uncertainty = marker half-size:** ±0.009…0.014 Mach; ±2.7e-5…4.5e-5 (Cnβ); ±1.7e-4…2.8e-4 (Cmα). Where inside the marker the plotting program anchored the point is not known.
- **Trend lines:** the drawn vertices exactly; uncertainty is half the 1-pt stroke.
- **Fig. 13 trend-line altitudes:** the report text says the three supersonic trends are 30k, 40k and 45k ft (p.16). Each was matched to its line by nearest baseline markers. Fig. 12's two lines state no altitude.

**Raster (figs. 14–15, 27–29).** The traces are embedded images.
- **Per panel:**
  - frame from long dark pixel runs;
  - axis end labels and intermediate grid values read from the rendered page;
  - calibration fitted on frame plus grid lines. Maximum residual ≤ 2.1 px; the value is recorded per series.
- **Trace finding:**
  - traces separated by colour; TM-2012 needs hue separation because of JPEG chroma bleed;
  - one median row per pixel column;
  - sampled every 0.1 s.
- **Omitted samples:** a sample is omitted — never filled — when:
  - its colour is absent within a few pixels (dash gaps, hidden under the other curve); or
  - its colour run spans more than 10–12 px (steep edges, overlapping surfaces).

  Each series records its omitted count.
- **Uncertainty = (3 px + calibration residual)** in both axes, converted per panel. For example ±0.2° α and ±0.05 s for TM-2008 fig. 14.
- **Verification:** every run wrote an overlay image with a ring on each kept sample. Overlays inspected by eye: TM-2008 figs. 14 and 15, TM-2012 figs. 27(a) and 29(a). TM-2012 figs. 27(b), 28(a), 28(b) and 29(b) were checked only by calibration residual, grid-line count and value range against the printed axes.

**Reading rules** (`MavF15Nasa836ValidationSeries.TryInterpolate`):
- a digitized sample returns exactly itself;
- line-type series interpolate only between two adjacent samples;
- **no** value outside the digitized domain;
- **no** value across an omitted sample (gap wider than 0.15 s);
- **never** between scattered flight estimates.

## 4. Source anomalies recorded, not corrected

- **TM-2012-215978 figs. 27–29, lateral acceleration.**
  - The axis is printed ×10⁻³ g at conditions 1–2, and ±0.010 g at condition 3.
  - Those magnitudes look small for the sideslip shown.
  - Stored exactly as printed; each series says so.
- **TM-2008-214634 figs. 14–15** state only "subsonic" / "supersonic". No Mach, altitude or weight is given, so those time histories cannot be reproduced at a known condition.
- **TM-2012 figs. 27–29** state Mach and altitude (table 2), but not fuel weight.

## 5. Why these cannot enter the model

- **No reference geometry.** Cnβ and Cmα are nondimensional. Using them needs 836's `S`, `c̄`, `b` and moment reference, which stay unavailable. `[V3]` asserts they are still 0.
- **Estimates, not a database.** These are a few flight estimates and one simulation's responses. Promoting them to coefficients would be exactly the fabrication this project forbids.
- **Enforced isolation:**
  - `[V3]` scans all 105 non-Validation sources and fails if any names the data types;
  - the types live in the `Validation` namespace.

## 6. Reproducing

`Docs/Reference/Data/F15/wp2_digitization/README.md`.

The scripts there were reconstructed verbatim after the session scratch folder (scripts, PDFs, intermediate JSON) was lost. They have **not** been re-run since, because the PDFs have not been downloaded again. A re-run and diff against the committed `.cs` is recorded as a follow-on.
