# F-15 Full-Scale Data Gaps V0.2

Status: **GAP-CLOSURE UPDATE — exact-target authority only**

Target remains exactly:

`NASA_F15B_836_SN74_0141_PRE_QUIET_SPIKE_BASELINE_F100_PW_100`

This document supersedes `F15_FULL_SCALE_DATA_GAPS_V0.1.md` as the current
missing-data surface. It does not change the target aircraft, does not import
RPV/F-16/ACTIVE/HIDEC values, and does not create a complete F-15 flight model.

## 1. Status vocabulary

- `FROZEN_DIRECT` — directly supported for the frozen target/configuration.
- `FROZEN_DERIVED` — deterministic conversion/derivation from a direct frozen value.
- `SUPPORTED_ONLY` — strong relevant primary/public support, but exact-target equivalence
  or implementation authority is not established.
- `CROSS_VALIDATION_ONLY` — useful for trend/methodology/independent checking only.
- `UNAVAILABLE` — no accepted public exact-target authority located.
- `CONFLICTING` — public evidence cannot yet be reconciled into one exact-target value.

## 2. Existing frozen values preserved

No R0/R0.5 frozen value was changed.

| Quantity | Frozen raw value | Frozen SI / derived value | Status |
|---|---:|---:|---|
| Length, excluding air-data nose boom | 63.7 ft | 19.41576 m | `FROZEN_DIRECT` / `FROZEN_DERIVED` |
| Span | 42.8 ft | 13.04544 m | `FROZEN_DIRECT` / `FROZEN_DERIVED` |
| Height | 18.7 ft | 5.69976 m | `FROZEN_DIRECT` / `FROZEN_DERIVED` |
| Fuel state | 8,000 lb | 3,628.73896 kg | `FROZEN_DIRECT` / `FROZEN_DERIVED` |
| Aircraft weight | 37,426 lb | 166,479.14217 N | `FROZEN_DIRECT` / `FROZEN_DERIVED` |
| Mass equivalent | from 37,426 lb | 16,976.14804 kg | `FROZEN_DERIVED` |
| CG | 26.34% MAC | same | `FROZEN_DIRECT` |
| Ixx | 30,345 slug-ft^2 | 41,142.29564 kg m^2 | `FROZEN_DIRECT` / `FROZEN_DERIVED` |
| Iyy | 198,687 slug-ft^2 | 269,383.40070 kg m^2 | `FROZEN_DIRECT` / `FROZEN_DERIVED` |
| Izz | 223,214 slug-ft^2 | 302,637.54752 kg m^2 | `FROZEN_DIRECT` / `FROZEN_DERIVED` |
| Ixz | **-5,070 slug-ft^2** | -6,873.99700 kg m^2 | `FROZEN_DIRECT` / `FROZEN_DERIVED`; source sign preserved |

> **Column correction.** These values previously carried NASA/TM-2012-215978 table 1's
> *Spike extended* column while labelled baseline. Corrected to the *Baseline F-15B test
> airplane* column, which is the one the pre-Quiet-Spike target requires. Full note in
> `F15_FULL_SCALE_TARGET_FREEZE_V0.1.md`.

| Engine identity | 2 x Pratt & Whitney F100-PW-100 | — | `FROZEN_DIRECT` |

The mass/inertia row remains the baseline F-15B 8,000-lb-fuel row from
NASA/TM-2012-215978 Table 1. No alternate mass state has been substituted.

## 3. New gap-closure results

### 3.1 Baseline flight-control architecture — additional direct closure

NASA/TM-2012-215978 and NASA/TM-2009-214651 describe the NASA DFRC F-15B
control system and explicitly state that the Quiet Spike program did not change the
production aircraft control system.

The following exact-target facts are now accepted as `FROZEN_DIRECT`:

- integrated mechanical and electrical, single-string analog CAS;
- stabilators commanded symmetrically for pitch and differentially for roll;
- ailerons and rudders are primary controlled surfaces;
- PRAD changes mechanical pitch-path gain using static/total-pressure information;
- normal-acceleration feedback trims stabilator to commanded g;
- with landing gear down, pitch CAS bypasses normal-acceleration feedback and provides
  pitch-rate command;
- pitch CAS uses angle-of-attack feedback for stall-inhibitor behavior;
- RRAD varies mechanical roll-path gain with calibrated airspeed in the applicable
  supersonic regime;
- roll CAS closes roll-rate feedback;
- roll CAS command limiting is scheduled with calibrated airspeed and angle of attack;
- ARI commands rudder in response to lateral-stick input and is scheduled through the
  mechanical lateral system;
- yaw CAS blends lateral acceleration and yaw rate for Dutch-roll damping/coordination;
- roll-to-yaw crossfeed is scheduled with angle of attack.

Two numeric schedule boundaries are directly printed in the exact-target report:

| Field | Printed value | Source locator | Status |
|---|---:|---|---|
| ARI operational Mach boundary | operative below **M 1.0**; output zero otherwise | NASA/TM-2012-215978, section **The Flight Control System**, text associated with Fig. 5; independently repeated in NASA/TM-2009-214651 | `FROZEN_DIRECT` |
| Roll-to-yaw crossfeed upper boundary | nullified above **M 1.5** | same locator | `FROZEN_DIRECT` |

These are control-schedule logic boundaries, not aerodynamic-coefficient validity limits.
No CAS gain, hard stop, actuator rate, or ARI gain schedule is inferred from them.

### 3.2 Baseline aerodynamic-model provenance and structure — direct closure

The exact NASA 836 pre-Quiet-Spike baseline aerodynamic model is now more tightly bounded:

- four research flights were flown before spike installation specifically to validate/update
  the baseline aerodynamic model;
- flight-derived updates were implemented as increments to the baseline model;
- NASA/TM-2009-214651 documents a baseline airplane lookup-table model with
  Mach-dependent aerodynamic uncertainty increments;
- parameter-estimation/validation covered longitudinal and lateral-directional derivative
  families, including static, control and damping terms;
- NASA/TM-2008-214634 reports reasonable baseline flight-to-simulation agreement for
  representative subsonic longitudinal and supersonic lateral-directional maneuvers;
- the same report explicitly warns that transonic lateral-directional agreement was not
  consistent, with rudder sweeps near transonic conditions particularly difficult to reproduce.

Accepted status:

| Field | Result | Status |
|---|---|---|
| Existence of exact-aircraft baseline nonlinear simulation/aero model | confirmed | `FROZEN_DIRECT` provenance |
| Four-flight baseline validation/update campaign | confirmed | `FROZEN_DIRECT` provenance |
| Baseline model uses numeric lookup data plus flight-derived updates | confirmed | `FROZEN_DIRECT` provenance |
| Derivative families represented in the analysis | confirmed | `FROZEN_DIRECT` model-content description |
| Complete public numeric coefficient database | not located | `UNAVAILABLE` |
| Transonic lateral-directional validation quality | known limitation | `FROZEN_DIRECT` limitation record |

This closes provenance, not the missing coefficient numbers.

### 3.3 Same-airframe structural coordinate clue — not promoted

NASA TM-2001-210395, the F-15B Propulsion Flight Test Fixture report, states that an
analysis CG of 28% MAC corresponded to fuselage station 561.7. This is a valuable
same-airframe coordinate clue.

It remains `SUPPORTED_ONLY` because:

- the analysis used a PFTF-installed configuration rather than the frozen pre-Quiet-Spike
  baseline state;
- the report does not by itself define the absolute FS origin, MAC leading-edge station,
  or exact target aerodynamic moment reference;
- using this single relation with a family MAC value would create an undocumented derived datum.

No FS coordinate is promoted to the target freeze in this phase.

### 3.4 Original engineering-source chain identified — acquisition gap narrowed

Multiple public USAF/AFIT sources independently identify the original McDonnell Douglas
engineering documents behind the F-15 simulation lineage:

- **MDC A4172**, *F-15 Stability Derivatives Mass and Inertia Characteristics*,
  including a Part II described as aerodynamic coefficients and stability/control derivatives;
- USAF Series Manual `A-11-2-2-1-1` / Aero-Inertia lineage, 1976 with later supplement;
- **McDonnell Aircraft Design Note DN-1180.01-238-458 (Rev. D)**,
  *F-15 Flight Control System Description*, October 1981.

Public AFIT thesis ADA319164 cites MDC A4172; AD-A230462 cites Part II; AD-A244044
cites Part I Supplement 1.

These citations materially narrow the acquisition target, but the original documents themselves
were not recovered as verified public primary copies during this task. Therefore they remain
`UNKNOWN` source leads, not authority.

## 4. Geometry/reference gap matrix

| Field | Current evidence | Status | Closure decision |
|---|---|---|---|
| Wing reference area `S` | 608 ft^2 appears repeatedly in government/NASA F-15 family material and AFIT work tracing to McDonnell data | `SUPPORTED_ONLY` | **not frozen**; exact NASA 836 reference-definition equivalence not proven |
| Mean aerodynamic chord `cbar` | 191.3 in appears in AFIT/USAF-derived F-15 material tracing to McDonnell source lineage | `SUPPORTED_ONLY` | **not frozen** |
| Wing sweep | 45 deg repeatedly supported for F-15 A-D family | `SUPPORTED_ONLY` | **not frozen** as an exact-target numeric field |
| Absolute aircraft datum | FS/WL/BL convention clearly used by NASA F-15 programs | `SUPPORTED_ONLY` | absolute origin/definition still `UNAVAILABLE` |
| Aerodynamic moment reference | modified-aircraft reports expose F-15 reference stations, but not accepted for NASA 836 | `CROSS_VALIDATION_ONLY` | `UNAVAILABLE` for target |
| CG-to-structural-datum mapping | 28% MAC -> FS 561.7 in same-airframe PFTF analysis | `SUPPORTED_ONLY` | not promoted |
| Engine station/datum relationship | no exact NASA 836 installation coordinates recovered | `UNAVAILABLE` | open |

## 5. Control gap matrix

| Field | Current evidence | Status |
|---|---|---|
| Physical surface identities | exact NASA 836 reports | `FROZEN_DIRECT` |
| Symmetric/differential stabilator roles | exact NASA 836 reports | `FROZEN_DIRECT` |
| Analog CAS / PRAD / RRAD / ARI architecture | exact NASA 836 reports | `FROZEN_DIRECT` |
| ARI Mach boundary | exact NASA 836 report | `FROZEN_DIRECT` |
| Roll-to-yaw crossfeed upper Mach boundary | exact NASA 836 report | `FROZEN_DIRECT` |
| Stabilator hard limits | NASA family/high-alpha material gives candidate values | `SUPPORTED_ONLY`; target hard stop `UNAVAILABLE` |
| Aileron hard limits | NASA family/high-alpha material gives candidate values | `SUPPORTED_ONLY`; target hard stop `UNAVAILABLE` |
| Rudder hard limits | NASA family/high-alpha material gives candidate values | `SUPPORTED_ONLY`; target hard stop `UNAVAILABLE` |
| Speed-brake limits | other-aircraft/measurement-range sources exist | `SUPPORTED_ONLY`; target hard stop `UNAVAILABLE` |
| Surface sign conventions for software | insufficient exact-target source | `UNAVAILABLE` |
| Actuator rate limits | ACTIVE/NASA 837 numbers exist but are incompatible | target `UNAVAILABLE` |
| Actuator dynamics/time constants | no accepted exact-target public source | `UNAVAILABLE` |
| CAS gains / complete schedules | original design-note lineage identified, source not acquired | `UNAVAILABLE` |
| ARI gain schedule | architecture known; numeric schedule not acquired | `UNAVAILABLE` |

NASA CP-10143 Vol. 3 provides useful F-15 A-D family surface-travel/CAS-authority
information, but it is not promoted to direct NASA 836 hard-stop authority.

## 6. Aerodynamic gap matrix

| Quantity | Status | Notes |
|---|---|---|
| `CX` / `CA` baseline numeric model | `UNAVAILABLE` | no complete exact-target table/equation set recovered |
| `CY` baseline numeric model | `UNAVAILABLE` | derivative family documented, numbers not public in recovered chain |
| `CZ` / `CN` baseline numeric model | `UNAVAILABLE` | same |
| `Cl` baseline numeric model | `UNAVAILABLE` | same |
| `Cm` baseline numeric model | `UNAVAILABLE` | same |
| `Cn` baseline numeric model | `UNAVAILABLE` | same |
| control derivatives | `UNAVAILABLE` numerically | exact derivative families/provenance confirmed |
| rate derivatives | `UNAVAILABLE` numerically | exact derivative families/provenance confirmed |
| Mach dependence | `SUPPORTED_ONLY` structurally | lookup/uncertainty model is Mach-dependent, numeric grid unavailable |
| alpha/beta valid range | `UNAVAILABLE` | experiment envelope is not coefficient validity envelope |
| high-alpha target database | `UNAVAILABLE` | RPV/family sources remain cross-validation only |
| Reynolds assumptions | `UNAVAILABLE` for target database | no recovered complete target model |
| inlet/spillage terms in baseline aero | `UNAVAILABLE` | no accepted decomposition recovered |

The Quiet Spike spike-delta model is not a substitute for the baseline airplane model.

## 7. Propulsion gap matrix

| Field | Current evidence | Status |
|---|---|---|
| Engine model | 2 x F100-PW-100 on target | `FROZEN_DIRECT` |
| Approximate uninstalled SLS full-AB class | same-aircraft NASA reports quote roughly 23.5–25 klbf depending on report/context | `CONFLICTING` as a precise number; not a deck |
| Mach/altitude thrust deck | NASA TP-1373 and related prototype-engine reports are strong F100-PW-100 support | `SUPPORTED_ONLY`; target deck `UNAVAILABLE` |
| Installed thrust | prototype F-15 flight thrust-calculation reports exist on other aircraft/engines | `CROSS_VALIDATION_ONLY`; target `UNAVAILABLE` |
| Military-power thrust | no target-compatible public deck recovered | `UNAVAILABLE` |
| Afterburner schedule | exact engine-family research exists, target build/control equivalence unproven | `SUPPORTED_ONLY` |
| Idle behavior | engine simulation sources exist, exact target equivalence unproven | `SUPPORTED_ONLY` |
| Spool/transient dynamics | NASA TM-X-3261 / TP-1034 are strong F100-PW-100 family sources | `SUPPORTED_ONLY`; target transient `UNAVAILABLE` |
| Fuel flow | F100 simulation/thrust-calculation sources include fuel-flow variables | `SUPPORTED_ONLY`; target map `UNAVAILABLE` |
| Aircraft inlet recovery | full-scale F-15 inlet/engine research exists on other test aircraft | `CROSS_VALIDATION_ONLY`; NASA 836 side-inlet map `UNAVAILABLE` |
| Engine left/right stations | no exact target source recovered | `UNAVAILABLE` |
| Thrust vectors/lines | no exact target source recovered | `UNAVAILABLE` |
| Nozzle force-application geometry | no exact target source recovered | `UNAVAILABLE` |

### F100 source boundary

NASA TP-1373 is particularly valuable because it explicitly calibrates two **prototype
F100-PW-100** engines over altitude/Mach conditions and provides a corrected performance-model
framework. NASA TM-X-3261 and NASA TP-1034 provide real-time engine simulations and transient
model structure.

None is promoted to `FROZEN_DIRECT` target thrust/transient authority because the target's
installed engine build/serial/control state and inlet/install losses are not matched.

## 8. Exact baseline Mach/alpha/beta envelope

Still `UNAVAILABLE`.

Important distinction:

- Quiet Spike test flights demonstrate same-airframe operation through broad subsonic,
  transonic and supersonic regimes.
- The Quiet Spike program reached Mach 1.8 with the external spike.
- Baseline flights included representative subsonic and supersonic maneuvers.
- None of those facts proves the validity bounds of the complete numeric baseline coefficient
  database that has not yet been recovered.
- NASA/TM-2008-214634 explicitly records inconsistent transonic lateral-directional
  rudder-sweep reproduction, which argues against inventing a transonic validity claim.

## 9. Remaining highest-priority gaps

1. Original or public configuration-matched NASA 836 baseline coefficient database.
2. Exact-target `S`, `cbar`, and aerodynamic moment/reference datum.
3. McDonnell/USAF production F-15B control-system document proving hard stops, signs,
   actuator rates/dynamics and numeric schedules applicable to S/N 74-0141.
4. Exact-target F100-PW-100 installed thrust/performance data or a documented mapping
   from a specific public F100-PW-100 deck to the NASA 836 engines.
5. Exact NASA 836 side-inlet recovery relationship.
6. Left/right engine installation position and thrust-line geometry in the accepted aircraft datum.
7. Fuel-dependent NASA 836 mass/CG/inertia schedule.
8. Numeric Mach/alpha/beta validity bounds attached to the eventual baseline coefficient source.

## 10. V0.2 conclusion

The missing-data surface is smaller in **provenance and control architecture**, but not yet
small enough for a full numerical F-15 implementation without assumptions.

No family value was promoted merely because it looked plausible.
