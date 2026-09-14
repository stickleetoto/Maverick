# F/A-18 Public Numeric Extraction V0.1

Status: **PROVENANCE-GRADE EXTRACTION — DATA ONLY, NO RUNTIME IMPLEMENTATION**

Branch target: `sol/fa18-f22-source-acquisition-r0`

This package freezes only values that are directly supported by acquired, lawfully public NASA documents. It does not merge `f18bas`, HARV Phase I, HARV Phase II/III, or operational F/A-18C/D configurations.

## Provenance classes

- `EXACT_TABLE` — transcribed from a visually inspected table in an acquired primary PDF.
- `EXACT_TEXT` — explicit numeric value, equation, grid or algorithm in primary text.
- `DIGITIZED_PRIMARY_FIGURE` — digitized from a primary figure; uncertainty/rounding is recorded and it is **not** the original tabular value.
- `STRUCTURAL_ONLY` — table dimensions, breakpoint structure or algorithm known while numeric arrays remain unavailable.
- `CONFLICTED_MODEL_VERSION` — two public primary model versions publish different values; both are preserved and no averaging is allowed.

## Acquired source set

| Source | Configuration authority | PDF pages | SHA256 |
|---|---|---:|---|
| NASA-TM-107601 | `f18bas` basic F/A-18 simulation | 184 | `34ed8edd9452aed3afbd7677c3847b888ec4b234224e0dd016c114564f5bb9f3` |
| NASA-TM-110216 | `f18harv` simulation | 164 | `55ebc29f32152970b4117865299d16777c1b0a5cd760e75bde6979944e738f6c` |
| NASA/TP-97-206539 | HARV Phase-II TVC, NASA-0/mixer-1 flight-test state | 72 | `1905e23ca63d7d3e57153df2e168210bc74d8bbc5d071502ee89b44843dc5023` |
| NASA-CR-194838 | 29 Sep 1992 HARV OBES PID campaign | 124 | `7ba84d9f3772f78c746bf60a838da7af3f630748b5158cc993df68b812204c81` |
| NASA-TM-4240 | F404-GE-400 HARV modified-nozzle simple engine model | 26 | `36d9d13d9fcd6e4dde0e79bf536bc1bb3f80084f66a54d34d28e1402178316ff` |

Source PDFs are intentionally **not** committed to this repository.

## Closed exact fields for `NASA_F18_HARV_160780_PHASE1_BASIC`

NASA/TP-97-206539 Table 3 directly compares unmodified Phase I and modified Phase II/III at the same 6,480-lb fuel condition, gear up, clean, with pilot/support equipment. The Phase-I row closes:

- weight: 31,980 lb
- reference area: 400 ft²
- reference MAC: 11.52 ft
- reference span: 37.4 ft
- CG: 21.9% MAC, FS 454.33 in, WL 105.24 in
- Ixx/Iyy/Izz: 22,040 / 124,554 / 139,382 slug-ft²
- Ixz: **-2,039 slug-ft²**, sign preserved
- overall length: 56 ft
- aspect ratio: 3.5
- stabilator span/area: 21.6 ft / 88.26 ft²

These values are exact for the stated Phase-I table condition, not a fuel schedule.

## Conventional control limits

NASA/TP-97-206539 Table 1 provides HARV conventional aerodynamic-surface position/rate limits. These values are stored separately from `f18bas` actuator parameters because the rudder/LEF rates differ between model lineages.

## `f18bas` reference state

NASA-TM-107601 Tables 3.4 and 3.5 close simulation-defined reference geometry and one Fighter Escort 60%-fuel mass state. This is authority for `NASA_F18BAS_BASIC_F18_OFP_8_3_3_TVC_OFF_CAPABLE`, **not proof that every number is the exact BuNo 160780 Phase-I flight state**.

The report explicitly defines the published `Ixz` and states that the negative of that quantity appears in the off-diagonal inertia-matrix entries. Raw source sign is preserved in the CSV.

## Actuator separation

Two actuator files are intentionally separate:

1. `F18BAS_ACTUATORS_V0.1.csv` — lower-order `f18bas`/hardware-integration-simulator values from TM-107601.
2. `F18HARV_ACTUATORS_V0.1.csv` — updated Dryden HARV second-order actuator models from TM-110216.

Do not silently replace one with the other.

## F404-GE-400 public propulsion closure

TM-110216 closes the structure of the HARV engine table:

- 12 Mach rows: 0.0, 0.2, 0.4, 0.6, 0.8, 0.9, 1.0, 1.1, 1.2, 1.4, 1.6, 1.8
- 7 altitude columns: 0, 10, 20, 30, 35, 40, 50 kft
- 4 power states: flight idle, military, minimum afterburner, maximum afterburner
- 7 dependent variables: nozzle area, drag, gross thrust, nozzle pressure ratio, turbine discharge pressure, ram drag, fuel flow
- 29 arrays total: 4 × 7 plus windmill drag
- linear interpolation over Mach and altitude.

The actual full 29 numeric arrays remain unavailable.

### Max-AB surface digitized from TM-4240 Figure 4

TM-4240 Figure 4 publishes the Max-AB gross-thrust table points graphically for the modified HARV nozzle. `F404_HARV_MAXAB_GROSS_THRUST_DIGITIZED_V0.1.csv` stores a digitization rounded to the nearest 100 lbf with an estimated ±150 lbf reading uncertainty.

This data is `DIGITIZED_PRIMARY_FIGURE`, not `EXACT_TABLE`. It is appropriate for public-evidence approximation/cross-checking, but must not be mislabeled as the original GE/NASA lookup array.

### Engine-dynamics conflict

TM-4240 (1990 simple engine model) gives increasing-PLA rate limits of:

- non-afterburning: +19.03 deg/s
- afterburning: +26.81 deg/s

TM-110216 (1996 `f18harv` implementation) gives:

- non-afterburning: 14 deg/s
- afterburning: 22 deg/s

Both publish the same 0.625 s and 0.55 s time constants. This is treated as `CONFLICTED_MODEL_VERSION`; no averaging is permitted.

## Flight-derived derivative evidence

NASA-CR-194838 presents lateral and longitudinal derivative sets from the 29 September 1992 OBES campaign at nominal 10, 25, 30, 40, 50 and 60 deg AOA. Its figures distinguish:

- squares: pEst flight-data estimates,
- circles: Iliff prior analysis,
- triangles: simulator-data pEst,
- x: McDonnell Douglas wind-tunnel values,
- vertical bars: Cramér-Rao bounds plotted at 5× scale.

`CR194838_DERIVATIVE_FIGURE_INDEX_V0.1.csv` records every relevant figure and derivative family. Numeric digitization is intentionally not promoted in this milestone until each panel is individually calibrated and marker identity is verified. The report also states that its analysis excluded thrust-vectoring doublet information; therefore vane-derivative plots are blocked from direct flight-authority promotion pending source-specific audit.

For longitudinal Phase-II TVC derivatives, NASA/TP-97-206539 is the stronger later source.

## Current implementation-readiness boundary

### Can be implemented without invented values

A reference-profile skeleton can now use:

- exact Phase-I geometry/mass/CG/inertia at the published 60%-fuel condition,
- conventional surface identities and published limits,
- `f18bas` reference geometry and simulation-defined actuator/FCS architecture when explicitly selecting the `f18bas` profile,
- HARV updated actuator models when explicitly selecting the HARV simulation profile,
- exact F404 engine grid structure and transient-model form,
- a clearly labeled digitized Max-AB gross-thrust approximation surface,
- flight-derived derivatives as validation/reference evidence.

### Still cannot be implemented as an exact full nonlinear F/A-18 model

The following remain open:

1. complete basic-F/A-18 nonlinear `CL/CD/Cm/CY/Cl/Cn` lookup arrays and increments,
2. exact low/high-alpha Mach/alpha/beta/control grids associated with those coefficient arrays,
3. original numeric F404 idle/mil/min-AB/max-AB tables for all seven dependent variables,
4. exact Phase-I fuel-dependent mass/CG/inertia schedule,
5. exact mapping between every `f18bas` simulation parameter and BuNo 160780 Phase-I flight configuration.

Verdict remains **PARTIAL — MORE RESEARCH**. The evidence ceiling is substantially higher than R0, but a full public-evidence nonlinear aero implementation still requires the missing coefficient arrays.
