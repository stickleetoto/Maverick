# F-15 Missing Source Priority V0.1

Status: **SOURCE-GAP AUDIT — no new numeric freeze**

Target aircraft remains exactly:

`NASA_F15B_836_SN74_0141_PRE_QUIET_SPIKE_BASELINE_F100_PW_100`

This document ranks the missing public-source targets that most directly block future implementation of the frozen full-scale F-15 reference aircraft. It is a research-priority document only.

It does **not**:

- change any value frozen in F-15 R0 or R0.5;
- promote any new number to direct authority;
- select a new F-15 configuration;
- fill gaps with RPV, F-16, HIDEC, PCA, ACTIVE, F-15E, CFT, stores, or generic-family values;
- authorize implementation from a merely similar aircraft.

Status vocabulary used here:

- `UNAVAILABLE`
- `CONFIG_MATCH_PENDING`
- `CROSS_VALIDATION_ONLY`

A source lead listed below is **not** frozen authority. Numeric extraction and configuration promotion belong to a later source-recovery phase.

---

## 1. Priority ranking summary

| Priority | Missing source target | Current status | Why this is high priority |
|---:|---|---|---|
| **1** | Exact-target wing reference area `S` and mean aerodynamic chord `cbar` | `CONFIG_MATCH_PENDING` | Required to dimensionalize aerodynamic forces/moments and interpret CG percent-MAC physically. |
| **2** | Public numeric NASA 836 pre-Quiet-Spike baseline aerodynamic database | `CONFIG_MATCH_PENDING` | Core blocker for real full-scale F-15 coefficient implementation. |
| **3** | Exact aircraft/reference datum and aerodynamic moment-reference definition | `UNAVAILABLE` | Required to place CG, aerodynamic moments, engines, and other force application points in one coordinate system. |
| **4** | Target-compatible hard control-surface travel limits and software sign conventions | `UNAVAILABLE` | Required for physical control-surface state limits without borrowing RPV/preproduction values. |
| **5** | Target-compatible actuator rate/dynamics data | `UNAVAILABLE` | Required before an F-15 actuator model can be physically bounded. |
| **6** | Exact numeric Mach/alpha/beta validity envelope for the baseline aero data | `UNAVAILABLE` | Prevents silent extrapolation beyond the eventual coefficient source. |
| **7** | F100-PW-100 thrust/performance deck versus Mach, altitude, and power | `UNAVAILABLE` | Main blocker to authoritative powered flight after the unpowered aero stack exists. |
| **8** | F100-PW-100 transient/spool response compatible with the target engine/control state | `UNAVAILABLE` | Needed for throttle response and independent twin-engine runtime dynamics. |
| **9** | F100-PW-100 fuel-flow data over operating condition and power | `UNAVAILABLE` | Needed for fuel burn and future fuel-dependent mass/CG coupling. |
| **10** | F-15 production-type inlet total-pressure recovery/distortion map compatible with NASA 836 | `UNAVAILABLE` | Needed before inlet effects can modify engine available performance. |
| **11** | Left/right engine installation coordinates and thrust-line directions | `UNAVAILABLE` | Required by the existing shared propulsion architecture for authoritative `r x F` moments. |
| **12** | Fuel-dependent full-scale mass / CG / inertia schedule for NASA 836 | `UNAVAILABLE` | One 8,000-lb-fuel state is frozen, but a powered simulation eventually needs state evolution. |
| **13** | Baseline F-15 CAS/ARI/gearing schedules compatible with NASA 836 | `CONFIG_MATCH_PENDING` | Needed for later control-law fidelity after the physical airframe is established. |
| **14** | Speed-brake, flap, and other configuration-device limits/schedules | `UNAVAILABLE` | Needed for later landing/approach/full-envelope work, but not required for first clean subsonic reference implementation. |

Priority order follows implementation dependency rather than historical interest: close geometry/reference definitions first, then the numeric aero model and physical controls, then propulsion and broader configuration-device behavior.

---

## 2. Detailed missing-source targets

### Priority 1 — wing reference area `S` and mean aerodynamic chord `cbar`

- **Missing field:** full-scale reference geometry.
- **Exact data needed:** source-defined `S`; `cbar`; definition of what planform area is included; MAC location/definition if given; raw source units.
- **Preferred source organization:** NASA Dryden/Armstrong or original McDonnell Douglas / USAF technical material tied to production-representative F-15B geometry.
- **Likely source family:** NASA 836 Quiet Spike baseline simulation/reference-dimension material; original F-15 aerodynamic data/simulation manuals referenced by those reports.
- **Exact configuration requirement:** production-representative two-seat F-15B outer mold line compatible with NASA tail 836 / S/N 74-0141 before Quiet Spike; no canards, CFT, stores, or three-surface modifications affecting reference definitions.
- **Why it matters:** Maverick cannot dimensionally convert nondimensional coefficients into forces/moments without accepted reference geometry; percent-MAC CG also remains only partly physical without an accepted MAC definition.
- **Unsafe substitutes:** scaling the RPV; importing preproduction #8 values without configuration proof; importing NASA 837/ACTIVE reference dimensions merely because family values match; generic F-15C/F-15E web specifications.
- **Current status:** `CONFIG_MATCH_PENDING`.
- **Recommended next action:** trace the reference-dimension and aerodynamic-model citations in NASA/TM-2009-214651 and NASA/TM-2012-215978; search for the underlying McDonnell Douglas F-15 simulation/aerodynamic-data document and verify that its geometry definition applies to the production-representative F-15B outer mold line used by NASA 836.

**Promising lead, not authority:**

- *Real-Time Stability and Control Derivative Extraction From F-15 Flight Data* — NASA/TM-2003-212027, NTRS `20030079970`, https://ntrs.nasa.gov/citations/20030079970
  - Why relevant: contains explicit F-15 reference dimensions and an FS/WL/BL moment reference, proving that NASA F-15 flight-dynamics reports may expose the exact geometry/reference definitions needed.
  - Restriction: the test airplane is a highly modified preproduction F-15B with canards, F100-PW-229 engines, and research nozzles. Values are **not** direct NASA 836 authority unless a later configuration-equivalence proof is made.

### Priority 2 — numeric pre-Quiet-Spike baseline aerodynamic database

- **Missing field:** static longitudinal/lateral-directional coefficients, control derivatives, and rate derivatives.
- **Exact data needed:** numeric baseline coefficient tables/equations for the pre-spike NASA 836 simulation; axes/signs; reference geometry; independent variables; control definitions; interpolation conventions; any flight-derived update increments; uncertainty if available.
- **Preferred source organization:** NASA Dryden/Armstrong; McDonnell Douglas/Boeing source material cited by the NASA 836 simulation.
- **Likely source family:** Quiet Spike baseline stability/control analysis and simulation references.
- **Exact configuration requirement:** NASA 836 baseline before spike installation; production-representative F-15B airframe and its documented NASA research-state boundary.
- **Why it matters:** this is the central missing input for a real full-scale aerodynamic implementation.
- **Unsafe substitutes:** direct RPV coefficients; TM-X-62360 subscale values; NASA 837/ACTIVE identified derivatives; preproduction #8 flight derivatives; three-surface transonic data; Quiet-Spike-installed coefficient increments treated as clean baseline.
- **Current status:** `CONFIG_MATCH_PENDING`.
- **Recommended next action:** recursively trace every baseline-aerodynamic-model reference in NASA/TM-2009-214651 and NASA/TM-2012-215978, including contractor/internal report numbers, and determine whether a public NASA/DTIC copy of the base simulation database exists.

**Strong exact-aircraft leads:**

- *Stability and Control Analysis of the F-15B Quiet Spike Aircraft* — NASA/TM-2009-214651, NTRS `20090034255`, https://ntrs.nasa.gov/citations/20090034255
  - Why relevant: exact NASA 836 program; documents the baseline aerodynamic model used before adding spike effects and the baseline-flight validation/update process.
- *Flight Test Results on the Stability and Control of the F-15 Quiet Spike Aircraft* — NASA/TM-2012-215978, NTRS `20120013435`, https://ntrs.nasa.gov/citations/20120013435
  - Why relevant: exact target airframe/program; ties stability/control flight analysis to the baseline airplane and records the frozen mass-property state already used by R0.5.

Neither report is promoted here as a complete public coefficient database.

### Priority 3 — aircraft datum and aerodynamic moment-reference definition

- **Missing field:** absolute FS/WL/BL datum and target aerodynamic reference/moment point.
- **Exact data needed:** definition of fuselage station, butt line, and water line origins/directions for NASA 836-compatible geometry; aerodynamic coefficient moment reference; MAC leading-edge station if available; relation between percent-MAC CG and the aircraft structural coordinate system.
- **Preferred source organization:** McDonnell Douglas/Boeing, NASA Dryden/Armstrong, or USAF structural/aerodynamic technical documentation.
- **Likely source family:** F-15 simulation data package, aerodynamic reference-dimension table, weight-and-balance/loads documentation.
- **Exact configuration requirement:** coordinate system must apply to production-representative F-15B/NASA 836 geometry; research appendages may not redefine the baseline datum unnoticed.
- **Why it matters:** without a common datum, aerodynamic moments, CG, inlet forces, and left/right engine force locations cannot be combined authoritatively.
- **Unsafe substitutes:** assuming a datum from NASA 837 or NASA 835 is identical; using the current Maverick CG as an invented zero; using drawing pixel coordinates.
- **Current status:** `UNAVAILABLE`.
- **Recommended next action:** use modified-F-15 reports only to identify the likely F-15 FS/WL/BL convention, then locate an original production-compatible F-15 drawing/simulation source that defines the datum before promoting any coordinates.

**Promising cross-validation lead:**

- NASA/TM-2003-212027 / NTRS `20030079970` contains an explicit F-15 moment-reference station and FS/WL/BL notation, but it belongs to the modified NASA 837 configuration and therefore remains `CROSS_VALIDATION_ONLY` for the target.

### Priority 4 — control-surface hard limits and deflection signs

- **Missing field:** stabilator, differential stabilator, aileron, rudder, speed-brake and relevant flap hard travel bounds plus implementation sign definitions.
- **Exact data needed:** positive/negative hard stops for each physical surface; whether limits change with configuration; physical deflection definitions; left/right sign algebra; any mechanical/CAS authority distinction.
- **Preferred source organization:** USAF / McDonnell Douglas production F-15 flight-control documentation, NASA reports reproducing the production control system.
- **Likely source family:** F-15 control-system appendices, flight-control-system evaluation reports, handling-qualities reports.
- **Exact configuration requirement:** production-type F-15B controls compatible with NASA 836; no ACTIVE canards/TVC, no RPV limits, no F-16 actuator values.
- **Why it matters:** control effectiveness cannot be physically bounded in software without knowing real surface travel and sign convention.
- **Unsafe substitutes:** TN-D-8136 RPV limits; NASA 837 ACTIVE controls; F-16 TP-1538 actuator limits/rates; generic simulator/game values.
- **Current status:** `UNAVAILABLE`.
- **Recommended next action:** inspect target-compatible production-control references cited by NASA/TM-2012-215978; then compare with preproduction TM-72861 only as a consistency check.

**Promising but configuration-mismatched lead:**

- *Precision Controllability of the F-15 Airplane* — NASA TM-72861, NTRS `19790015808`, https://ntrs.nasa.gov/citations/19790015808
  - Why relevant: has a detailed F-15 control-system appendix and explicit surface/control authority information.
  - Restriction: preproduction aircraft. It is a search map and cross-check, not direct NASA 836 authority.

### Priority 5 — actuator rates and actuator dynamics

- **Missing field:** physical actuator slew/rate limits and any significant first/second-order actuator behavior.
- **Exact data needed:** rate limits by stabilator/aileron/rudder channel; position/rate dependence if any; hydraulic or servo dynamics relevant to the physical surface; whether CAS series servos have a separate authority/rate boundary.
- **Preferred source organization:** USAF/McDonnell Douglas control-system qualification reports; NASA F-15 flying-qualities/control-system research.
- **Likely source family:** F-15 hydromechanical/AFCS/CAS descriptions and simulator-validation reports.
- **Exact configuration requirement:** production-type F-15B system used by NASA 836 before later digital research modifications.
- **Why it matters:** a surface model with correct hard stops but arbitrary rate can still produce incorrect short-period/roll/yaw transients.
- **Unsafe substitutes:** F-16 actuator rates; ACTIVE/IFCS servo rates; RPV servos; gameplay tuning.
- **Current status:** `UNAVAILABLE`.
- **Recommended next action:** search the references behind the TM-72861 control-system appendix and NASA 836 control-system description for original actuator specifications; promote only after production-F-15B compatibility is demonstrated.

### Priority 6 — Mach/alpha/beta validity envelope for the eventual baseline database

- **Missing field:** exact source-supported interpolation envelope.
- **Exact data needed:** minimum/maximum Mach, angle of attack, sideslip, control deflection, and relevant rate ranges for each numeric coefficient table/equation; configuration changes within the source; explicit extrapolation restrictions.
- **Preferred source organization:** same organization/source that supplies the eventual numeric baseline aerodynamic database.
- **Likely source family:** NASA 836 baseline simulation documentation and underlying F-15 aero database manuals.
- **Exact configuration requirement:** same coefficient dataset selected at Priority 2.
- **Why it matters:** aircraft capability limits or Quiet Spike test envelopes are not coefficient validity limits.
- **Unsafe substitutes:** Mach 1.8 Quiet Spike envelope; NASA 836 maximum aircraft speed; RPV M<0.60 bounds; TM-X-62360 high-alpha bounds; ACTIVE/three-surface transonic ranges.
- **Current status:** `UNAVAILABLE`.
- **Recommended next action:** do not research this independently of Priority 2; require the eventual coefficient source to carry its own validity grid and record it point-for-point during extraction.

### Priority 7 — F100-PW-100 Mach/altitude/power thrust deck

- **Missing field:** gross/net/installed thrust model suitable for the frozen two-engine target.
- **Exact data needed:** thrust or sufficient thermodynamic/performance outputs versus Mach, altitude/ambient condition, power lever/power state, afterburner segment/augmentation state; gross versus ram drag/net thrust definition; correction factors and validity envelope.
- **Preferred source organization:** NASA Lewis/Glenn, Pratt & Whitney, USAF.
- **Likely source family:** prototype F100-PW-100 altitude-facility calibration and F-15 in-flight thrust-determination reports.
- **Exact configuration requirement:** F100-PW-100 only; exact subvariant/control/nozzle assumptions must be compared with NASA 836's pre-2014 engines before promotion.
- **Why it matters:** a single sea-level static thrust quotation cannot power a six-DoF simulation across Mach and altitude.
- **Unsafe substitutes:** F100-PW-220E; PW1128/EMD; F100-PW-229; generic `F100`; F-15E performance tables; hand-shaped thrust curves.
- **Current status:** `UNAVAILABLE`.
- **Recommended next action:** recover the NASA Lewis altitude-facility datasets and their calibrated engine models, then establish whether their prototype F100-PW-100 configuration is transferable to the target production F100-PW-100 engines or only useful as a reconstruction prior in a later phase.

**Strong leads:**

- *Comparison of calculated and altitude-facility-measured thrust and airflow of two prototype F100 turbofan engines* — NASA-TP-1373 / H-1015, NTRS `19790004873`, https://ntrs.nasa.gov/citations/19790004873
  - Why relevant: measured altitude-facility performance plus a corrected engine performance model over a flight envelope for prototype F100-PW-100 engines.
- *Evaluation of a simplified gross thrust calculation technique using two prototype F100 turbofan engines in an altitude facility* — NASA-TP-1482 / H-1061, NTRS `19790017886`, https://ntrs.nasa.gov/citations/19790017886
  - Why relevant: measured-versus-calculated gross-thrust work on the same prototype-engine family.
- *Flight evaluation of a simplified gross thrust calculation technique using an F100 turbofan engine in an F-15 airplane* — NASA-TP-1782 / H-1118, NTRS `19810006485`, https://ntrs.nasa.gov/citations/19810006485
  - Why relevant: flight evaluation on prototype F100-PW-100 engines installed in an F-15.

All three remain `CONFIG_MATCH_PENDING` to NASA 836's installed production engines.

### Priority 8 — F100-PW-100 transient/spool model

- **Missing field:** throttle-to-engine-state transient behavior.
- **Exact data needed:** spool-state definitions; acceleration/deceleration dynamics; throttle/power-command mapping; transient limits; afterburner light/segment behavior if modeled; validity versus flight condition.
- **Preferred source organization:** NASA Lewis/Glenn, Pratt & Whitney, NASA Dryden F100 control research.
- **Likely source family:** F100-PW-100 real-time engine simulations and standard-engine versus DEEC flight-test comparisons.
- **Exact configuration requirement:** target-compatible standard F100-PW-100 control/engine state; DEEC or F100-PW-100(3) data require explicit compatibility proof.
- **Why it matters:** the shared propulsion runtime can hold independent left/right states, but those states need a sourced law rather than an F-16 or generic lag model.
- **Unsafe substitutes:** Garza/Morelli F-16 engine law; PW1128 transient behavior; PW-229 behavior; post-2014 PW-220E behavior.
- **Current status:** `UNAVAILABLE`.
- **Recommended next action:** compare the standard NASA 836 engine/control description against the exact F100 build/control assumptions in the Lewis simulations before deciding whether they can be promoted or remain reconstruction references.

**Strong leads:**

- *Real-time simulation of F100-PW-100 turbofan engine using the hybrid computer* — NASA-TM-X-3261 / E-8136, NTRS `19750019996`, https://ntrs.nasa.gov/citations/19750019996
  - Why relevant: steady-state and sea-level transient simulation, with a baseline digital model and actual-engine comparison.
- *Development and verification of real-time, hybrid computer simulation of F100-PW-100(3) turbofan engine* — NASA-TP-1034 / E-9090, NTRS `19770024210`, https://ntrs.nasa.gov/citations/19770024210
  - Why relevant: equations and implementation material covering steady-state and transient behavior over a broad set of flight conditions.
- *Digital Electronic Engine Control (DEEC) Flight Evaluation in an F-15 Airplane* — NASA-CP-2298, NTRS `19860015871`, https://ntrs.nasa.gov/citations/19860015871
  - Why relevant: contains standard-F100-PW-100 versus DEEC transient/operability comparisons.
  - Restriction: DEEC-equipped states are not automatically the NASA 836 standard-engine configuration.

### Priority 9 — F100-PW-100 fuel-flow map

- **Missing field:** fuel flow versus engine state and flight condition.
- **Exact data needed:** main-combustor and, if available, augmentation fuel flow versus Mach/altitude/power; units and corrected variables; any transient fuel scheduling needed for dynamic simulation.
- **Preferred source organization:** NASA Lewis/Glenn, Pratt & Whitney, USAF.
- **Likely source family:** F100 baseline digital/hybrid simulation documentation and altitude-facility performance reports.
- **Exact configuration requirement:** F100-PW-100 compatible with target engine/control assumptions.
- **Why it matters:** powered endurance and future mass/CG evolution cannot be modeled physically without fuel consumption.
- **Unsafe substitutes:** F100 EMD/PW1128 fuel flow; PW-220E/PW-229 maps; F-16 mission fuel-burn curves.
- **Current status:** `UNAVAILABLE`.
- **Recommended next action:** inspect NASA-TM-X-3261 and NASA-TP-1034 output definitions/equations specifically for usable fuel-flow variables and then cross-check against altitude-facility/flight reports before promotion.

### Priority 10 — inlet recovery and distortion

- **Missing field:** total-pressure recovery/distortion presented in a form usable by the engine model.
- **Exact data needed:** inlet pressure recovery versus Mach/angle of attack/sideslip and inlet-ramp schedule; distortion descriptors at the engine face; configuration and engine-presence effects; bleed/bypass assumptions.
- **Preferred source organization:** NASA Dryden/Armstrong, NASA Lewis/Glenn, McDonnell Douglas.
- **Likely source family:** F-15 inlet/engine test-technique studies and F100/F-15 inlet-distortion flight work.
- **Exact configuration requirement:** production-type F-15 two-dimensional three-ramp inlet geometrically compatible with NASA 836; no centerline PFTF/channeled-inlet experiment may be confused with the aircraft's own inlets.
- **Why it matters:** inlet recovery is the bridge between free-stream condition and engine-face condition; thrust cannot be installation-accurate without it.
- **Unsafe substitutes:** PFTF centerbody-inlet data from NASA 836; PW1128/HIDEC inlet-control schedules without compatibility proof; generic fighter-inlet efficiency constants.
- **Current status:** `UNAVAILABLE`.
- **Recommended next action:** evaluate the original F-15 inlet/engine distortion program for geometric compatibility with production NASA 836 inlets; isolate recovery maps from methodology plots and document engine/ramp state at each point.

**Strong leads:**

- *F-15 inlet/engine test techniques and distortion methodologies studies. Volume 1: Technical discussion* — NASA-CR-144866, NTRS `19780022180`, https://ntrs.nasa.gov/citations/19780022180
  - Why relevant: F-15 inlet pressure recovery/distortion, scale/Reynolds effects, and engine-presence effects over a broad flight-condition family.
- *Effects of inlet distortion on a static pressure probe mounted on the engine hub in an F-15 airplane* — NASA-TP-2411 / H-1182, NTRS `19850014083`, https://ntrs.nasa.gov/citations/19850014083
  - Why relevant: full-scale F-15/F100 engine-face pressure and off-schedule inlet-ramp distortion flight data.

These are not yet proven to be the exact NASA 836 inlet/control state.

### Priority 11 — engine installation coordinates / thrust lines

- **Missing field:** left/right engine force application locations and thrust directions relative to the frozen physics datum.
- **Exact data needed:** engine centerline and/or effective thrust-force point coordinates in FS/WL/BL; nozzle/exhaust-plane geometry if needed; thrust-vector direction for normal production nozzle operation; exact coordinate datum.
- **Preferred source organization:** McDonnell Douglas/Boeing, NASA F-15 propulsion research, USAF installation/loads documentation.
- **Likely source family:** F-15 propulsion-system force/moment documentation, PCA/HIDEC reports, structural station drawings.
- **Exact configuration requirement:** production F100-PW-100 installation geometry compatible with NASA 836; coordinates may transfer only after airframe/engine/nozzle/datum identity is proven.
- **Why it matters:** Maverick already computes propulsion moment from `r x F`; zero or guessed offsets would make engine-out yaw physically wrong.
- **Unsafe substitutes:** synthetic symmetric coordinates; NASA 835/PW1128 coordinates treated as direct; NASA 837 vectoring-nozzle coordinates; F-15E installation geometry.
- **Current status:** `UNAVAILABLE`.
- **Recommended next action:** use known modified-F-15 propulsion reports to identify the standard F-15 station convention and likely engine/inlet stations, then locate a production-compatible installation drawing/report before promoting coordinates.

**Promising cross-validation lead:**

- *Development and Flight Test of an Emergency Flight Control System Using Only Engine Thrust on an F-15 Airplane* — NASA-TP-3627, NTRS `19970001362`, https://ntrs.nasa.gov/citations/19970001362
  - Why relevant: contains explicit F-15 inlet/engine force-location geometry in FS/WL/BL coordinates and shows how propulsion forces were reduced to moments.
  - Restriction: NASA 835/PCA configuration with PW1128 engines; coordinates are **CROSS_VALIDATION_ONLY** until production-NASA-836 installation equivalence is proven.

### Priority 12 — fuel-dependent mass / CG / inertia schedule

- **Missing field:** mass-property evolution away from the one frozen 8,000-lb-fuel state.
- **Exact data needed:** aircraft weight, CG, `Ixx`, `Iyy`, `Izz`, `Ixz` over multiple target-compatible fuel states; definition of payload/instrumentation state; fuel tank loading sequence if needed.
- **Preferred source organization:** NASA Dryden weight-and-balance/test-plan records, McDonnell Douglas/USAF mass-properties documentation.
- **Likely source family:** Quiet Spike loads/stability support documentation and test-flight mass-property sheets.
- **Exact configuration requirement:** NASA 836 baseline research instrumentation state before Quiet Spike installation, or an explicitly reconcilable NASA 836 weight-and-balance model.
- **Why it matters:** powered simulation with fuel burn eventually needs the aircraft rigid-body state to evolve without inventing inertia scaling.
- **Unsafe substitutes:** PCA/NASA 835 fuel-state inertias; RPV scaling; generic F-15B empty/full weights with interpolated inertias.
- **Current status:** `UNAVAILABLE`.
- **Recommended next action:** search NASA 836 Quiet Spike test planning/loads/flight-card records and appendices for additional mass-property rows or the mass-properties tool/source cited by NASA/TM-2012-215978.

### Priority 13 — CAS/ARI/gearing schedules

- **Missing field:** production-type augmentation/control gearing needed after the physical surface model exists.
- **Exact data needed:** pitch/roll/yaw control gearing schedules, CAS authority/limit schedules, aileron-rudder interconnect logic, relevant dynamic-pressure/Mach/alpha scheduling, trim/PRAD/RRAD behavior where needed.
- **Preferred source organization:** McDonnell Douglas/USAF; NASA F-15 handling-qualities/control-system reports.
- **Likely source family:** TM-72861 appendix and its original control-system references; NASA 836 production-control-system description.
- **Exact configuration requirement:** analog/hydromechanical production F-15B architecture compatible with NASA 836, not HIDEC/DEFCS/ACTIVE.
- **Why it matters:** later handling-quality fidelity requires separating physical airframe aerodynamics from control augmentation rather than baking gains into coefficients.
- **Unsafe substitutes:** NASA 837 IFCS; NASA 835 HIDEC; RPV augmentation; gameplay instructor gains.
- **Current status:** `CONFIG_MATCH_PENDING`.
- **Recommended next action:** trace the original production F-15 control-system citations behind TM-72861 and NASA/TM-2012-215978, then compare hardware/configuration effectors before accepting any schedule.

### Priority 14 — speed brake / flap / configuration-device schedules

- **Missing field:** hard limits and scheduling for speed brake, flaps and any other target-relevant configuration devices.
- **Exact data needed:** deployment limits; commanded versus actual schedule; airspeed/Mach restrictions if part of the physical model; aerodynamic database configuration states corresponding to each device.
- **Preferred source organization:** USAF/McDonnell Douglas flight-control/flight-manual technical material; NASA 836 test-bed reports where devices affect tests.
- **Likely source family:** production F-15 flight-control and handling-qualities documentation.
- **Exact configuration requirement:** production-type F-15B/NASA 836 baseline.
- **Why it matters:** needed for landing/approach/drag-device modeling, but lower priority than clean-airframe reference aerodynamics and propulsion closure.
- **Unsafe substitutes:** F-15E schedules; ACTIVE schedules; arbitrary game limits.
- **Current status:** `UNAVAILABLE`.
- **Recommended next action:** defer deep extraction until Priorities 1-7 are substantially closed; when researched, require exact aerodynamic configuration-state labels for deployed devices.

---

## 3. Strongest new source leads from this audit

The following were not promoted to frozen data. They are simply the best public leads found for the next source-recovery pass.

| Lead | Report / identifier | Potential gap | Audit disposition |
|---|---|---|---|
| *Comparison of calculated and altitude-facility-measured thrust and airflow of two prototype F100 turbofan engines* | NASA-TP-1373 / H-1015; NTRS `19790004873`; https://ntrs.nasa.gov/citations/19790004873 | F100-PW-100 thrust/airflow performance model | **High-value `CONFIG_MATCH_PENDING`** — prototype engines, exact NASA 836 installation match unproven. |
| *Real-time simulation of F100-PW-100 turbofan engine using the hybrid computer* | NASA-TM-X-3261 / E-8136; NTRS `19750019996`; https://ntrs.nasa.gov/citations/19750019996 | engine transient/steady-state model, possible fuel-flow variables | **High-value `CONFIG_MATCH_PENDING`** — engine/control-build compatibility must be checked. |
| *Development and verification of real-time, hybrid computer simulation of F100-PW-100(3) turbofan engine* | NASA-TP-1034 / E-9090; NTRS `19770024210`; https://ntrs.nasa.gov/citations/19770024210 | engine transient/steady-state equations | **High-value `CONFIG_MATCH_PENDING`** — `(3)` configuration cannot be assumed equal to NASA 836 engines. |
| *Flight evaluation of a simplified gross thrust calculation technique using an F100 turbofan engine in an F-15 airplane* | NASA-TP-1782 / H-1118; NTRS `19810006485`; https://ntrs.nasa.gov/citations/19810006485 | in-flight thrust validation | **High-value `CONFIG_MATCH_PENDING`** — prototype F100-PW-100 F-15 installation, not target airframe proof. |
| *F-15 inlet/engine test techniques and distortion methodologies studies. Volume 1* | NASA-CR-144866; NTRS `19780022180`; https://ntrs.nasa.gov/citations/19780022180 | inlet recovery/distortion | **High-value `CONFIG_MATCH_PENDING`** — geometric/configuration match to NASA 836 must be established. |
| *Effects of inlet distortion on a static pressure probe mounted on the engine hub in an F-15 airplane* | NASA-TP-2411 / H-1182; NTRS `19850014083`; https://ntrs.nasa.gov/citations/19850014083 | full-scale inlet/engine-face distortion | **Supporting lead** — exact airframe/engine/control state must be separated. |
| *Precision Controllability of the F-15 Airplane* | NASA TM-72861; NTRS `19790015808`; https://ntrs.nasa.gov/citations/19790015808 | control system, surface authority, geometry definitions | **`CROSS_VALIDATION_ONLY`** for NASA 836 until production/preproduction compatibility is proven. |
| *Real-Time Stability and Control Derivative Extraction From F-15 Flight Data* | NASA/TM-2003-212027; NTRS `20030079970`; https://ntrs.nasa.gov/citations/20030079970 | reference dimensions, moment reference, coefficient-definition pattern | **`CROSS_VALIDATION_ONLY`** — highly modified NASA 837. |
| *Development and Flight Test of an Emergency Flight Control System Using Only Engine Thrust on an F-15 Airplane* | NASA-TP-3627; NTRS `19970001362`; https://ntrs.nasa.gov/citations/19970001362 | engine/inlet station geometry and force-to-moment methodology | **`CROSS_VALIDATION_ONLY`** — NASA 835/PW1128 PCA state. |

Exact-aircraft NASA 836 reports already frozen in R0.5 remain the first place to trace citations for Priorities 1-3:

- NASA/TM-2009-214651 / NTRS `20090034255`
- NASA/TM-2012-215978 / NTRS `20120013435`

The highest-value next research action is **reference-chain recovery**, not immediate digitization: identify the underlying production-compatible simulation/aerodynamic-data documents referenced by these exact-aircraft reports before extracting any numbers from nearby F-15 families.

---

## 4. Unsafe substitution policy for the next source pass

The following remain explicitly disallowed as gap fillers:

- `NASA_F15_RPV_3_8_BASIC1_CG26_BLOCKED_INLETS` dimensional geometry, mass, inertia, control limits, or coefficients;
- F-16 control or propulsion data;
- NASA 835 HIDEC/PW1128 values as target values;
- NASA 837 ACTIVE/IFCS/F100-PW-229 values as target values;
- NASA 836 post-2014 F100-PW-220E engine values;
- Quiet-Spike-installed aerodynamic deltas treated as clean baseline coefficients;
- F-15E/F-15EX gameplay or public marketing specifications;
- generic `F100` performance without exact variant/build/control identification;
- generic F-15 family geometry/reference coordinates without configuration-equivalence proof;
- CFT/store/three-surface/canard data as direct baseline authority.

A mismatched source may still be useful as `CROSS_VALIDATION_ONLY` or as a search map to an original document. It must not silently close a target field.

---

## 5. Recommended research sequence

1. **Recover exact NASA 836 baseline aero references** from NASA/TM-2009-214651 and NASA/TM-2012-215978.
2. **Close `S`, `cbar`, and datum together** from one production-compatible geometry/reference source if possible.
3. **Locate the numeric baseline aerodynamic model and its own validity envelope.** Do not separate coefficient extraction from envelope extraction.
4. **Close physical surface limits/signs/rates** before implementing control actuators.
5. **Audit the F100-PW-100 Lewis/Dryden source chain**: TP-1373, TP-1482, TP-1782, TM-X-3261, TP-1034, and DEEC conference material, preserving prototype/production/control-build distinctions.
6. **Recover production F-15 inlet and installation geometry** only after the aircraft datum is explicit.
7. Defer fuel-dependent inertia, detailed augmentation schedules, and landing-device schedules until the first bounded clean-airframe aerodynamic model is source-complete.

---

## 6. Audit verdict

The frozen target remains unchanged:

`NASA_F15B_836_SN74_0141_PRE_QUIET_SPIKE_BASELINE_F100_PW_100`

No new numerical F-15 value is frozen by this document.

The next implementation-critical research target is not a newer aircraft or broader source family. It is the **underlying production-compatible reference chain behind the already selected NASA 836 baseline simulation**, followed by exact F100-PW-100 performance/configuration matching.

**F15 SOURCE GAP AUDIT: COMPLETE**
