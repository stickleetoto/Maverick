# F-15 R0 Configuration Freeze V0.1

Status: **FROZEN — research configuration only**

This document freezes the first defensible F-15 reference configuration for Maverick. It is a source/provenance freeze, not a complete aerodynamic model and not a production-F-15 claim.

## 1. Frozen configuration identity

Canonical configuration tag:

`NASA_F15_RPV_3_8_BASIC1_CG26_BLOCKED_INLETS`

Human-readable identity:

> NASA Flight Research Center unpowered, remotely piloted 3/8-scale F-15 research vehicle, **Basic 1** configuration, center of gravity at 26% mean aerodynamic chord, blocked inlets.

Primary authority: NASA TN D-8136, NTRS 19760008088.

The report explicitly separates the **basic** model from a later so-called **production configuration**. The production configuration changed the wingtip trailing-edge shape and removed a section of the horizontal stabilizer. Therefore `Basic 1` is not interchangeable with `Production 1/2`, nor is this R0 identity asserted to be F-15A/B/C/E.

## 2. Why this configuration was selected

This is the smallest coherent public NASA chain found that simultaneously provides:

- an explicit research-aircraft identity and scale,
- reference wing geometry,
- a configuration-specific CG and inertia row,
- named pitch/roll/yaw control surfaces,
- flight-derived subsonic stability/control data,
- an independent same-program control-system/mass-property source.

NASA TN D-8136 is the primary configuration/aerodynamics authority. NASA TN D-7941 is direct supporting authority for the same 3/8-scale RPV program and is used for control-surface identities/signs and mass-property cross-checking. NASA TN D-8052 is deferred to spin/departure cross-validation.

This choice intentionally favors a precisely identified research vehicle over pretending that a mixed public dataset represents a production F-15 variant.

## 3. Direct source chain

| Source | Configuration tag | R0 authority |
|---|---|---|
| NASA TN D-8136 / NTRS 19760008088 | `NASA_F15_RPV_3_8_BASIC1_CG26_BLOCKED_INLETS` plus other explicitly separated RPV configurations in the same report | **DIRECT_REFERENCE** for R0 identity, geometry, Basic 1 mass/CG/inertia, source envelope, and future configuration-resolved flight-derivative extraction |
| NASA TN D-7941 / NTRS 19750012221 | `NASA_F15_RPV_3_8_EARLY_RPRV` with 26% and 30% MAC mass-property states | **DIRECT_REFERENCE** only for same-RPV control architecture/signs and 26% CG mass-property cross-check; it does not replace the D-8136 Basic 1 row |
| NASA RP-1168 | `NASA_FRC_PARAMETER_ESTIMATION_CONVENTION` | **METHODOLOGY_ONLY** for body-axis, force/moment, alpha/beta, and generic control-sign convention mapping |

Permanent NTRS records:

- https://ntrs.nasa.gov/citations/19760008088
- https://ntrs.nasa.gov/citations/19750012221

## 4. Geometry freeze

The following values are from TN D-8136 Table 1. The report table is already in SI units, so the SI conversion is identity.

| Quantity | Published value/unit | Frozen SI value | Provenance |
|---|---:|---:|---|
| Reference wing area, `S` | 7.94 m^2 | **7.94 m^2** | TN D-8136 Table 1 |
| Reference span, `b` | 4.89 m | **4.89 m** | TN D-8136 Table 1 |
| Mean aerodynamic chord, `cbar` | 1.82 m | **1.82 m** | TN D-8136 Table 1 |
| Model length | 7.15 m | **7.15 m** | TN D-8136 Table 1 |
| Wing aspect ratio | 3.0 | **3.0 (published rounded value)** | TN D-8136 Table 1 |
| Wing leading-edge sweep | 45.0 deg | **45.0 deg** | TN D-8136 Table 1 |
| Wing taper ratio | 0.25 | **0.25** | TN D-8136 Table 1 |
| Wing dihedral | -1.0 deg | **-1.0 deg** | TN D-8136 Table 1 |
| Wing incidence | 0 deg | **0 deg** | TN D-8136 Table 1 |

Reference datum status:

- CG is source-defined as a percentage of mean aerodynamic chord.
- R0 freezes **CG = 26% MAC** for Basic 1.
- An absolute longitudinal station/datum for the MAC leading edge is **UNAVAILABLE** in the verified R0 source extraction and is not guessed.

Geometry relation check:

`b^2 / S = 4.89^2 / 7.94 = 3.011599...`, consistent with the report's rounded aspect ratio of 3.0.

## 5. Mass, CG, and inertia freeze

TN D-8136 Table 2 is authoritative because it explicitly labels the configuration as **Basic 1**.

| Quantity | Published value/unit | Frozen value | Notes |
|---|---:|---:|---|
| Weight, `W` | 10,960 N | **10,960 N** | TN D-8136 Table 2, Basic 1 |
| Mass-equivalent, `m = W/g0` | derived | **1117.608969 kg** | `g0 = 9.80665 m/s^2`; derived, not an independently published mass |
| CG | 26% mean aerodynamic chord | **26% MAC** | TN D-8136 Table 2, Basic 1 |
| `Ix` | 373 kg m^2 | **373 kg m^2** | TN D-8136 Table 2 |
| `Iy` | 2,579 kg m^2 | **2,579 kg m^2** | TN D-8136 Table 2 |
| `Iz` | 3,021 kg m^2 | **3,021 kg m^2** | TN D-8136 Table 2 |
| `Ixz` table quantity | 16 kg m^2 | **16 kg m^2** | TN D-8136 Table 2 |

Important provenance boundary:

- TN D-8136 Table 1 gives a generic model weight of 10,964 N.
- TN D-7941 gives an actual 26%-MAC vehicle state of 10,964 N, `Ix=373`, `Iy=2579`, `Iz=3021`, `Ixz=15.7 kg m^2`.
- R0 does **not average or blend** those values with the configuration-specific TN D-8136 Table 2 Basic 1 row. The 4 N / 0.3 kg m^2-scale differences are recorded as cross-source/state/rounding differences, not silently reconciled.

The source table's `Ixz` scalar is frozen as a reported quantity. The exact sign placement of `Ixz` inside a software inertia tensor must follow the selected rigid-body tensor convention when/if a profile is implemented; R0 does not alter shared transform or FDM code.

## 6. Control-surface identity freeze

The same RPV source family identifies:

- paired horizontal stabilators/horizontal tails:
  - collective/symmetric motion for pitch,
  - differential motion for roll;
- left and right wing ailerons for roll;
- twin rudders for yaw.

TN D-8136 often uses the aerodynamic-analysis aliases `elevator` and `differential elevator`; TN D-7941 describes the physical surfaces as left/right stabilators. R0 treats these as source aliases for the paired all-moving horizontal-tail controls of this research vehicle, not as permission to import unrelated production-F-15 control schedules.

### Deflection signs

Direct TN D-7941 notation states that individual left/right aileron and left/right stabilator positions are positive **trailing-edge down**.

Composite aileron/differential-stabilator algebra is not frozen from OCR-corrupted notation in R0. Rudder deflection sign is likewise not promoted to aircraft-specific authority without a clean page-level verification.

### Surface limits

**UNAVAILABLE for R0 implementation.**

TN D-8136 Table 1 and report text contain control-limit information, but the currently verified text extraction corrupts plus/minus symbols in several entries and the report also discusses a design maximum elevator value in a different context. R0 will not guess which number belongs in a software limiter. Surface limits require a page-level source transcription before implementation.

## 7. Axes and coefficient-sign mapping

TN D-8136 explicitly defines `alpha` as angle of attack of the body axis and uses the FRC parameter-estimation coefficient family (`CN`, `CY`, `Cl`, `Cm`, `Cn`). For an explicit software mapping, R0 adopts the NASA Flight Research Center convention documented by Maine & Iliff in NASA RP-1168, a methodology source from the same parameter-estimation lineage.

### Source/Maverick body axes

| Axis | NASA FRC convention | Maverick aero-body convention | Mapping |
|---|---|---|---|
| `+X` | forward | forward | identity |
| `+Y` | right | right | identity |
| `+Z` | down | down | identity |

Therefore **no axis reflection is required** for the R0 reference convention.

### Flow-angle signs

From NASA RP-1168:

- `alpha = atan(w/u)`: positive alpha corresponds to positive body-`Z` wind-relative velocity component (`w > 0`, with `+Z` down).
- `beta = asin(v/V)`: positive beta corresponds to positive body-`Y` wind-relative velocity component (`v > 0`, with `+Y` right).

### Force and moment signs

Using the same convention:

- `CX`: positive forward (`+X`)
- `CY`: positive right (`+Y`)
- `CZ`: positive down (`+Z`)
- `CN = -CZ`: positive normal-force coefficient is upward
- `CA = -CX`: positive axial-force coefficient is aft
- `Cl`: positive roll is right wing down
- `Cm`: positive pitch is nose up
- `Cn`: positive yaw is nose right

R0 freezes this mapping for future import of the TN D-8136 derivative notation. It does **not** change any shared transform code.

## 8. Aerodynamic validity envelope

TN D-8136 states:

- flight-derived subsonic derivatives,
- analyzed maneuvers at **Mach < 0.60**,
- report-wide angle-of-attack coverage **-20 deg to +53 deg**,
- 168 maneuvers from 12 of the first 16 flights.

Important restriction: the report-wide alpha range spans multiple CG/configuration states. R0 therefore freezes these as **source-family bounds**, not as proof that every Basic 1 derivative exists at every point in that range.

For future Basic 1 aerodynamic tables:

- Mach upper bound: source requires `M < 0.60`;
- Basic-1 point-by-point alpha coverage: **CONFIG_MATCH_PENDING** until the Basic 1 symbols/data are digitized per figure/table;
- beta range: **UNAVAILABLE** as a configuration-resolved bound in R0;
- no coefficient values are digitized or implemented in this phase.

## 9. Propulsion / engine identity

For the chosen R0 research vehicle:

- physical propulsion state: **UNPOWERED**;
- inlet state: **BLOCKED**;
- engine variant: **NONE / NOT APPLICABLE TO THIS RPV**;
- real F-15/F100 thrust deck: **UNAVAILABLE**;
- real engine installation coordinates: **UNAVAILABLE**.

`F100` is not treated as a single numeric engine profile.

The shared propulsion architecture's future two-slot F-15 provision is left unchanged. Because this R0 reference identity is an unpowered research vehicle, no powered F-15 profile/provider skeleton is created in this phase; doing so would conflate the RPV reference with a production twin-engine aircraft.

## 10. Source graph

```text
R0 direct reference
  NASA TN D-8136 / 19760008088
    -> 3/8-scale RPV identity
    -> Basic vs Production separation
    -> Basic 1 / CG 26% state
    -> S, b, cbar
    -> Basic 1 W / inertia
    -> subsonic flight-derived derivative provenance
  NASA TN D-7941 / 19750012221
    -> same 3/8-scale RPV program
    -> stabilator/aileron/rudder control architecture
    -> individual aileron/stabilator sign convention
    -> 26% CG mass/inertia cross-check
  NASA RP-1168
    -> FRC body-axis / force / moment / flow-angle methodology

R1/R2 cross-validation / extension
  NASA TN D-8052 / 19760010068
    -> departure / spin / high-alpha flight and simulator support

Future static/config-matching work
  NASA TM-X-62360 / 19780002076
    -> 0.075-scale low-speed static tunnel trends
    -> NOT direct until geometry/inlet/nose/stores configuration is matched

Future dynamic/rotary work
  NASA CR-3478 / 19820016292
    -> 1/12-scale rotary-balance methodology/data
  NASA CR-3479 / 19820014339
    -> rotary analysis / spin prediction
    -> CFT effects explicitly separated

Future full-scale/transonic cross-validation
  NASA TM-72861 / 19790015808
    -> preproduction full-scale F-15 handling/precision-controllability context

Future propulsion
  exact production/research F-15 identity + exact engine variant
    -> UNAVAILABLE in R0
```

## 11. Deterministic consistency checks

| Check | Result |
|---|---|
| Required config tag is unique in this freeze | **PASS** |
| No incompatible source is marked direct authority for R0 numeric values | **PASS** |
| `S`, `b`, `cbar`, mass-equivalent, and diagonal inertias are positive | **PASS** |
| `b^2/S = 3.011599...` agrees with published rounded AR 3.0 | **PASS** |
| `10960 / 9.80665 = 1117.608969... kg` | **PASS** |
| `Ix*Iz - Ixz^2 = 1,126,577 > 0` | **PASS** |
| Inertia positive-definiteness check using the reported `|Ixz|` magnitude gives determinant `2,905,442,083 > 0` | **PASS** |
| Frozen numeric fields have report/table provenance | **PASS** |
| Powered engine/thrust numbers guessed | **NO / PASS** |
| Shared FDM/F-16 files modified | **NO / PASS** |

These are consistency checks only; they are not substitutes for future aerodynamic-data validation.

## 12. Explicitly not frozen / unavailable

R0 does **not** freeze:

- a production designation such as F-15A/B/C/E,
- full aerodynamic coefficient tables,
- a Basic 1 point-resolved alpha/beta grid,
- transonic or supersonic data,
- spin/rotary coefficients,
- stores aerodynamics,
- conformal-fuel-tank aerodynamics,
- exact software control-surface limits,
- aircraft-specific rudder/composite differential-control sign algebra beyond the direct facts above,
- absolute CG station in meters from a fuselage datum,
- engine type/variant, thrust, spool dynamics, fuel flow, or installation coordinates,
- powered-flight behavior.

## 13. Authority boundary

`Docs/F15E_SPEC_RESEARCH_AND_GAME_TUNING.md` remains gameplay/historical material only. It is not a source for this freeze.

The compatibility decisions for all candidate sources are recorded in:

`Docs/Reference/F15_SOURCE_COMPATIBILITY_MATRIX_V0.1.md`

## 14. Freeze verdict

**F15 R0: FROZEN**

Meaning: the exact R0 *research configuration identity and its defensible geometry/mass/inertia/control/source boundary* are frozen.

It does **not** mean a full F-15 aerodynamic or propulsion model exists.
