# F/A-18 Public Numeric Extraction R0 M2

Status: **REFERENCE DATA EXTRACTION ONLY — NO FLIGHT-MODEL IMPLEMENTATION**

Branch target: `sol/fa18-f22-source-acquisition-r0`

This note records numeric data that can be extracted now from lawfully released NASA sources already acquired by the project. It deliberately separates exact tabulated values from figure-digitized values and from simulation-only structures.

## 1. Acquired source bytes

| Source | Local filename | PDF pages | SHA256 |
|---|---|---:|---|
| NASA TM-107601, *Simulation Model of a Twin-Tail, High Performance Airplane* | `19920024293 (2).pdf` | 184 | `34ed8edd9452aed3afbd7677c3847b888ec4b234224e0dd016c114564f5bb9f3` |
| NASA CR-194838, *Determination of the Stability and Control Derivatives of the NASA F/A-18 HARV Using Flight Data* | `19940020331.pdf` | 124 | `7ba84d9f3772f78c746bf60a838da7af3f630748b5158cc993df68b812204c81` |
| NASA TM-4240, *A Simple Dynamic Engine Model for Use in a Real-Time Aircraft Simulation With Thrust Vectoring* | `19910009766.pdf` | 26 | `36d9d13d9fcd6e4dde0e79bf536bc1bb3f80084f66a54d34d28e1402178316ff` |
| NASA TM-110216, *Simulation Model of the F/A-18 High Angle-of-Attack Research Vehicle Utilized for the Design of Advanced Control Laws* | `19960027892 (2).pdf` | 166 | `55ebc29f32152970b4117865299d16777c1b0a5cd760e75bde6979944e738f6c` |

Source PDFs remain outside the repository.

## 2. Exact `f18bas` reference geometry and mass state

Authority: NASA TM-107601, Tables 3.3–3.5.  
Configuration: `NASA_F18BAS_BASIC_F18_OFP_8_3_3_TVC_OFF_CAPABLE`; the hypothetical preliminary TVC must be disabled for the basic state.

### Reference geometry — exact transcription

- `SREF = 400 ft^2`
- `CREF = 138.275 in = 11.523 ft`
- `bREF = 37.42 ft`
- L.E. MAC at `FS 423.99`
- aerodynamic-reference dimensions are explicitly tied to MDC A7247 page 3-2.

Additional geometry directly reproduced by TM-107601 includes wing, LEX, horizontal-tail, vertical-tail, rudder, flap, aileron, fuselage and speed-brake dimensions. These values are authoritative for the documented `f18bas` simulation configuration, not automatically for an F/A-18C/D fleet block.

### Fighter Escort / 60% internal fuel mass state — exact transcription

- Weight: `31,665 lb`
- CG: `FS 457.3 in`, `WL 101.6 in`
- `Ixx = 22,337 slug-ft^2`
- `Iyy = 120,293 slug-ft^2`
- `Izz = 138,945 slug-ft^2`
- `Ixz = -2,430 slug-ft^2`

The source explicitly explains that the listed `Ixz` is the volume integral of `xz` weighted by mass density and that the negative of that quantity appears in the (1,3)/(3,1) inertia-matrix entries. Preserve the raw source sign; do not silently remap it.

### Default `f18bas` simulation mass state — exact transcription

- Weight: `33,310 lb`
- CG: `FS 455.0 in`, `WL 102.8 in`
- `Ixx = 23,000 slug-ft^2`
- `Iyy = 151,293 slug-ft^2`
- `Izz = 169,945 slug-ft^2`
- `Ixz = -2,971 slug-ft^2`

The report states this default state is an early estimate of an F-18 HARV after adding thrust-vectoring hardware. It is not the basic Phase-I HARV mass state.

## 3. Exact HARV simulation mass states

Authority: NASA TM-110216, Table 2.3.  
Configuration: modified HARV simulation state; do not merge with Phase-I/basic values.

| State | Weight lb | FS in | WL in | Ixx slug-ft^2 | Iyy slug-ft^2 | Izz slug-ft^2 | Ixz slug-ft^2 |
|---|---:|---:|---:|---:|---:|---:|---:|
| Fighter Escort, 60% internal fuel | 35764.6 | 456.3 | 105.4 | 22632.6 | 174246.3 | 189336.4 | -2131.8 |
| Light Weight | 31617.7 | 460.89 | 103.36 | 22163.0 | 172237.5 | 186823.1 | -2043.1 |
| Heavy Weight | 37618.7 | 456.31 | 105.91 | 22938.3 | 179130.1 | 194003.0 | -2507.3 |
| Empty Weight | 29615.0 | 463.75 | 102.31 | 21643.3 | 171184.9 | 185408.8 | -1967.4 |

## 4. Control-surface limits and actuator models

### Basic/F-18-compatible physical surface limits

NASA TP-97-206539 reproduces conventional F-18 aerodynamic-control surface position/rate limits and states they apply to the HARV and basic F-18:

- Stabilator: TE up 24 deg; TE down 10.5 deg; rate 40 deg/s.
- Aileron: TE up 24 deg; TE down 45 deg; rate 100 deg/s.
- Rudder: 30 deg left/right; rate 82 deg/s.
- TEF: up 8 deg; down 45 deg; rate 18 deg/s.
- LEF: up 3 deg; down 33 deg; rate 15 deg/s.
- Speed brake: TE up 60 deg; rate 20–30 deg/s.

Keep this source separate from the older `f18bas` simplification.

### `f18bas` actuator implementation

TM-107601 Table 8.7 documents the low-order hardware-integration-simulator implementation used by `f18bas`:

- Stabilator: ±40 deg/s, position `-24/+10.5 deg`, first-order lag `tau = 1/30 s`.
- Rudder: ±61 deg/s, position `-30/+30 deg`, first-order lag `tau = 1/40 s`.
- Aileron: ±100 deg/s, position `-25/+45 deg`, first-order lag `tau = 1/48 s`.
- TEF: ±18 deg/s, position `-8/+45 deg`, first-order lag `tau = 1/20 s`.
- LEF: ±18 deg/s, position `-3/+34 deg`, first-order lag `tau = 1/20 s`.

TM-107601 itself records the rudder-rate discrepancy: MDC A7813 Table 8.6 gives 56 deg/s while the MDC/DMS simulation and Table 8.7 use 61 deg/s. Therefore `56 vs 61 deg/s` remains an explicit source/implementation conflict rather than something to average.

### HARV higher-fidelity actuator models

TM-110216 Table 6.1:

- Stabilator: `30.74^2 / (s^2 + 2(0.509)(30.74)s + 30.74^2)`
- Rudder: `72.1^2 / (s^2 + 2(0.69)(72.1)s + 72.1^2)`
- Aileron: `75^2 / (s^2 + 2(0.59)(75)s + 75^2)`
- TEF: `35^2 / (s^2 + 2(0.71)(35)s + 35^2)`
- LEF: `(26.9)(82.9) / ((s+26.9)(s+82.9))`

These are HARV/Dryden-model actuators and must not silently replace `f18bas` actuators.

## 5. F404-GE-400 engine-model structure

### Exact table topology

TM-110216 section 4.1 states:

- 29 arrays total.
- Seven dependent variables: nozzle area, drag, gross thrust, nozzle pressure ratio, turbine discharge pressure, ram drag and fuel flow.
- Four power-state arrays for each dependent variable: flight idle, military, minimum afterburner, maximum afterburner.
- One additional windmill-drag array.
- Every array is `12 x 7`.
- Mach rows: `0.0, 0.2, 0.4, 0.6, 0.8, 0.9, 1.0, 1.1, 1.2, 1.4, 1.6, 1.8`.
- Altitude columns: `0, 10, 20, 30, 35, 40, 50 kft`.
- Interpolation is linear in Mach and altitude.

This closes the public topology of the engine deck, but not the full numeric deck.

### Engine dynamics — versioned conflict

NASA TM-4240 (1990) gives the simple dynamic-engine PLA shaping model as:

- non-afterburning time constant `0.625 s`
- afterburning time constant `0.550 s`
- increasing-PLA non-afterburning rate limit `+19.03 deg/s`
- increasing-PLA afterburning rate limit `+26.81 deg/s`
- no rate limiting on decreasing PLA in the described model.

NASA TM-110216 (1996) retains the same time constants but documents later `f18harv` rate limits of:

- non-A/B `14 deg/s`
- A/B `22 deg/s`

These are not interchangeable. They are recorded as **model-version differences**.

## 6. Digitized Max-AB thrust surface

NASA TM-4240 Figure 4 plots the actual table symbols for the **maximum-afterburner gross-thrust** surface generated from the GE Dynamic Real Time Model for the modified HARV nozzle.

A companion CSV, `Docs/Reference/Data/FA18/FA18_F404_MAX_AB_TM4240_DIGITIZED_V0.1.csv`, records a first-pass visual digitization at the exact 12 Mach x 7 altitude breakpoints.

Important limitations:

- status: `DIGITIZED_FROM_PRIMARY_FIGURE`
- rounded visual extraction, **not** the original GE/NASA array.
- conservative per-point uncertainty field: ±750 lbf.
- applies to the F404-GE-400 with the modified F-18 HARV nozzle represented by TM-4240.
- it must not be relabeled as a basic production-F/A-18 installed thrust deck.
- do not use it to infer idle, military or minimum-afterburner surfaces.

## 7. CR-194838 flight-derivative evidence

NASA CR-194838 states that a complete set of stability/control derivatives from roughly 10–60 deg AOA was estimated from NASA HARV flight data using `pEst`.

For Figures 8.7–8.15:

- squares = `pEst` coefficients computed from flight data.
- circles = prior Iliff analysis.
- triangles = `pEst` results from F/A-18 simulator data.
- x marks = McDonnell Douglas wind-tunnel values.
- vertical uncertainty bars on estimated values are Cramer-Rao bounds multiplied by 5 for plotting.

A companion figure inventory is stored in `Docs/Reference/Data/FA18/FA18_CR194838_DERIVATIVE_FIGURE_INDEX_V0.1.csv`.

Critical caveat: the report conclusion states that flight data containing thrust-vectoring doublets could not be used in that analysis because of ITAR restrictions. Therefore the thrust-vector derivative results in Figures 8.14, 8.15, 8.30 and 8.31 are **initial predictions**, not flight-derived authority. They must not be promoted as flight-identified TVC coefficients.

The report also warns that higher-AOA estimates exhibit accuracy problems/scatter and that simulator axial-force derivative estimates are expected to be invalid because angle-of-attack effects on thrust were not modeled.

## 8. What is now closed vs still open

### Closed or strongly closed for the documented public research configurations

- reference geometry
- several exact mass/CG/inertia states
- sign conventions
- physical/control-surface limits
- `f18bas` low-order actuators
- HARV higher-fidelity actuator transfer functions
- engine-deck dimensions, axes and interpolation topology
- F404 simple-engine dynamic constants, versioned by report
- Max-AB HARV-nozzle gross-thrust surface at validation/digitized precision
- broad longitudinal/lateral flight-derivative figure coverage for HARV

### Still open

1. Original numeric basic-F/A-18 nonlinear aerodynamic lookup arrays underlying `SFAERRF/USAERRF`.
2. Full exact numeric F404 arrays for idle / military / minimum-AB / maximum-AB and the other six dependent variables.
3. Full verified numeric Phase-I/basic HARV static coefficient deck.
4. Point-by-point digitization and independent QC of all non-TVC CR-194838 derivative figures.
5. Original MDC A7247 / A8575 and GE R88AEB427 public copies.

## 9. Readiness effect

This extraction materially improves the public-data position but does **not** change the central verdict to `GO` yet.

**F/A-18: PARTIAL — MORE RESEARCH**

A high-fidelity research-reference model is increasingly defensible. A provenance-grade complete nonlinear basic-airframe coefficient deck and full exact propulsion deck remain the main blockers.
