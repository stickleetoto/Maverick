# F-15 Full-Scale Candidate Matrix V0.1

Status: **R0.5 selection record**

Selected target:

`NASA_F15B_836_SN74_0141_PRE_QUIET_SPIKE_BASELINE_F100_PW_100`

This matrix compares full-scale candidates without treating all NASA F-15 research as one interchangeable aircraft.

## 1. Scoring method

Each candidate receives 0-2 points in thirteen areas:

A identity clarity; B geometry availability; C mass availability; D CG availability; E inertia availability; F control-surface definition; G low-speed/subsonic aero availability; H transonic/supersonic source availability; I exact engine identity; J propulsion source availability; K flight-test correlation; L configuration purity; M public-source completeness.

Maximum score: 26.

The numeric score is only a selection aid. The absolute configuration rule overrides the score: a highly modified aircraft can score well for documentation and still be rejected as the baseline target.

## 2. Score/result matrix

| Candidate configuration | A | B | C | D | E | F | G | H | I | J | K | L | M | Total | Classification / result |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| `NASA_F15B_836_SN74_0141_PRE_QUIET_SPIKE_BASELINE_F100_PW_100` | 2 | 1 | 2 | 2 | 2 | 2 | 2 | 2 | 2 | 1 | 2 | 2 | 2 | **24/26** | **FULL_SCALE_TARGET_CANDIDATE — SELECTED** |
| `NASA_F15A_71_0287_TM72861_PRE_HIDEC` | 2 | 2 | 1 | 0 | 0 | 2 | 2 | 2 | 0 | 0 | 2 | 1 | 2 | **16/26** | FULL_SCALE_TARGET_CANDIDATE — rejected |
| `NASA_F15A_835_SN71_0287_HIDEC_PW1128_DEEC` | 2 | 2 | 1 | 1 | 1 | 1 | 1 | 1 | 2 | 2 | 2 | 0 | 2 | **18/26** | SUPPORTING_REFERENCE |
| `NASA_F15A_71_0281_PROPULSION_TEST_P680063` | 2 | 1 | 0 | 0 | 0 | 1 | 0 | 1 | 2 | 2 | 2 | 1 | 1 | **13/26** | SUPPORTING_REFERENCE |
| `NASA_NF15B_837_SN71_0290_ACTIVE_IFCS_F100_PW229` | 2 | 2 | 2 | 2 | 2 | 2 | 2 | 2 | 2 | 2 | 2 | 0 | 2 | **24/26** | **INCOMPATIBLE** as baseline target |
| `NASA_F15B_836_SN74_0141_POST2014_F100_PW220E` | 2 | 1 | 0 | 0 | 0 | 2 | 1 | 1 | 2 | 1 | 1 | 2 | 1 | **14/26** | SUPPORTING_REFERENCE; later engine state |
| `GENERIC_PRODUCTION_F15C_PUBLIC_FAMILY` | 0 | 2 | 1 | 0 | 0 | 1 | 0 | 1 | 0 | 0 | 1 | 2 | 1 | **9/26** | INCOMPATIBLE as an exact target identity |

The selected NASA 836 baseline ties the highly modified NASA 837 in raw documentation score, but NASA 837 fails the configuration-purity gate because of canards, thrust-vectoring nozzles, digital flight-control modifications, and later research propulsion. The score therefore cannot promote it to baseline target status.

## 3. Detailed candidate records

### 3.1 Selected — NASA F-15B 836 / S/N 74-0141, pre-Quiet-Spike baseline

**Configuration ID**  
`NASA_F15B_836_SN74_0141_PRE_QUIET_SPIKE_BASELINE_F100_PW_100`

- Aircraft designation: F-15B, two-seat full-scale aircraft.
- Serial/research identity: NASA tail 836; USAF S/N 74-0141.
- Status: production-representative aircraft converted to NASA research test bed.
- Clean/stores/CFT: selected direct state has no Quiet Spike and no accepted external centerline experiment; no CFT authority is imported. NASA research instrumentation and air-data hardware remain part of the identity.
- Inlet configuration: production-type two-dimensional external-compression horizontal-ramp inlets, three ramps per inlet, per NASA/TM-2005-213670.
- Engine variant: **2 x F100-PW-100**.
- Controls: ailerons, twin rudders, all-moving stabilators; stabilators symmetric for pitch and differential for roll; hydromechanical system with analog/electrical CAS.
- Research modifications: radar/gun/ammunition systems removed in the Quiet Spike research state; instrumentation pallet/data acquisition/telemetry; YAPS nose boom for baseline data flights; sideslip vane/fairing and covered gun port.
- Geometry source: NASA/TM-2006-213674 and same-aircraft NASA test-bed reports for length/span/height.
- Mass/CG source: NASA/TM-2012-215978 Table 1 baseline row at 8,000 lb fuel.
- Inertia source: same exact Table 1 row.
- Aero source: pre-spike baseline aerodynamic model validated/updated using four same-aircraft research flights; complete public coefficient database not yet located.
- Propulsion source: exact engine identity is direct; approximate engine performance and inlet research sources exist; no exact authoritative thrust deck frozen.
- Mach range: same aircraft has public subsonic/transonic/supersonic test history; exact baseline coefficient envelope remains unavailable. Quiet Spike external-article flights reached Mach 1.8 but are not direct baseline coefficient authority.
- Alpha/beta range: exact target coefficient bounds unavailable; Quiet Spike program bounds are external-article-specific.
- Flight-test data: strong; four baseline flights plus extensive later same-airframe programs.
- Compatibility with RPV anchor: useful for normalized trend/methodology cross-validation only.
- Major gaps: `S`, `cbar`, absolute datum, numeric baseline coefficient database, target-specific hard surface limits/rates/sign convention, thrust deck, engine installation geometry.
- Final score: **24/26**.
- Result: **SELECTED**.

Primary public sources:

- https://ntrs.nasa.gov/citations/20120013435
- https://ntrs.nasa.gov/citations/20090034255
- https://ntrs.nasa.gov/citations/20070032807
- https://ntrs.nasa.gov/citations/20050241960
- https://ntrs.nasa.gov/citations/20060022548
- https://www.nasa.gov/aeronautics/f-15b-test-bed/

### 3.2 Preproduction F-15 #8 / S/N 71-0287 before HIDEC

**Configuration ID**  
`NASA_F15A_71_0287_TM72861_PRE_HIDEC`

- Aircraft designation: early/preproduction single-seat F-15, NASA/USAF research aircraft #8.
- Serial identity: 71-0287; later NASA tail 835.
- Status: eighth preproduction F-15.
- Clean/stores/CFT: research-aircraft loading varies by program; exact TM-72861 test loading requires point-level extraction.
- Inlet: F-15 variable-geometry inlet family.
- Engine variant: **UNAVAILABLE for the exact early TM-72861 target state**. Production F-15s of the period and NASA propulsion programs used multiple F100 development states; the exact aircraft/engine pairing must not be inferred.
- Controls: strong handling/flying-qualities material exists, but later NASA 835 control architecture was modified.
- Geometry source: same-aircraft NASA summaries give 608 ft² reference wing area, 42.83-ft span, 63.75-ft length and 45-deg wing leading-edge sweep.
- Mass/CG: approximate zero-fuel weight exists in later NASA research-aircraft description; no configuration-matched TM-72861 CG state frozen.
- Inertia: unavailable for the selected early state.
- Aero source: NASA TM-72861, high-subsonic/low-transonic precision-controllability flight research.
- Propulsion source: early exact variant unresolved.
- Flight-test availability: strong.
- RPV compatibility: trend cross-validation only.
- Major gaps: exact engine, mass/CG/inertia for the specific TM-72861 state, changing preproduction/production geometry and control differences.
- Final score: **16/26**.
- Result: rejected in favor of the more coherent NASA 836 chain.

Sources:

- https://ntrs.nasa.gov/citations/19790015808
- https://ntrs.nasa.gov/citations/19950026589
- https://ntrs.nasa.gov/citations/19990064011

### 3.3 NASA 835 / S/N 71-0287 HIDEC with PW1128

**Configuration ID**  
`NASA_F15A_835_SN71_0287_HIDEC_PW1128_DEEC`

- Aircraft designation: preproduction single-seat F-15 research aircraft.
- Serial/research identity: 71-0287 / NASA 835.
- Status: heavily instrumented propulsion/flight-control research state.
- CFT/stores: program-specific; not baseline authority.
- Inlet: two-dimensional, three-ramp external-compression inlet with electronic inlet control in HIDEC descriptions.
- Engine variant: **2 x PW1128 / F100 engine model derivative**, DEEC-controlled.
- Control modifications: digital electronic flight-control integration and highly integrated propulsion/flight-control research.
- Geometry: available for the airframe family.
- Mass/CG/inertia: partial/configuration-specific program material exists, but not enough here to define a clean baseline without importing research-system state.
- Aero/transonic: good research coverage but tied to modified control/propulsion states.
- Propulsion: exceptionally strong public research chain.
- RPV compatibility: methodology/trend only.
- Major gap for baseline use: configuration purity; the research system is the point of the aircraft.
- Final score: **18/26**.
- Classification: **SUPPORTING_REFERENCE**, especially propulsion/control methodology.

Sources:

- https://ntrs.nasa.gov/citations/19950026591
- https://ntrs.nasa.gov/citations/19930027279
- https://ntrs.nasa.gov/citations/19920018136

### 3.4 Preproduction F-15 #2 / S/N 71-0281, propulsion-test state

**Configuration ID**  
`NASA_F15A_71_0281_PROPULSION_TEST_P680063`

- Aircraft designation: early/preproduction single-seat F-15.
- Serial identity: 71-0281.
- Status: NASA/USAF propulsion-test aircraft.
- Engine: the P680063 F100-PW-100 development engine is explicitly documented in this aircraft during propulsion research.
- Aero/geometry/mass/inertia: not sufficiently coherent for use as the main full-aircraft target.
- Propulsion source: strong calibration, thrust/airflow, inlet and afterbody research value.
- Major gap: aircraft-wide profile completeness.
- Final score: **13/26**.
- Classification: **SUPPORTING_REFERENCE** for exact program-tagged propulsion methodology/data only.

Source:

- https://ntrs.nasa.gov/citations/19990064011

### 3.5 NASA NF-15B 837 / S/N 71-0290, ACTIVE/IFCS

**Configuration ID**  
`NASA_NF15B_837_SN71_0290_ACTIVE_IFCS_F100_PW229`

- Aircraft designation: preproduction F-15B-derived research aircraft, later NF-15B/ACTIVE.
- Serial/research identity: 71-0290 / NASA 837.
- Status: **highly modified** research aircraft; NASA explicitly states it is not representative of production F-15 aircraft.
- Geometry: canards and other research modifications.
- Engine: F100-PW-229 in ACTIVE configuration.
- Controls: quad-redundant digital fly-by-wire/research control architecture.
- Propulsion: pitch/yaw thrust-vectoring nozzles and digital engine/nozzle integration.
- Mass/CG/inertia/aero: unusually well documented for the research state.
- Flight-test data: excellent.
- RPV compatibility: only broad methodology/trend comparison.
- Major gap for baseline use: not a gap but a hard incompatibility — lifting/control/propulsion geometry is intentionally different.
- Final score: **24/26**.
- Classification: **INCOMPATIBLE** for the selected baseline despite high documentation score.

Sources:

- https://ntrs.nasa.gov/citations/19980228162
- https://ntrs.nasa.gov/citations/20030079970

### 3.6 NASA F-15B 836 after 2014 engine upgrade

**Configuration ID**  
`NASA_F15B_836_SN74_0141_POST2014_F100_PW220E`

- Aircraft identity: same NASA 836 / S/N 74-0141 airframe.
- Status: later research-test-bed state.
- Engine: **2 x F100-PW-220E**, reported by NASA as upgraded in 2014.
- Compatibility: airframe identity is highly relevant, but the engine state is not compatible with the selected 2006 pre-Quiet-Spike F100-PW-100 propulsion identity.
- Mass/CG/inertia: no assumption that 2006 reference-state values automatically describe post-2014 state.
- Classification: **SUPPORTING_REFERENCE** for airframe continuity and a future alternate propulsion configuration.
- Final score: **14/26**.

Source:

- https://ntrs.nasa.gov/citations/20160006705

### 3.7 Generic production F-15C public family

**Configuration ID**  
`GENERIC_PRODUCTION_F15C_PUBLIC_FAMILY`

- Aircraft designation: F-15C family label only.
- Exact serial/block/research identity: absent.
- Engines: public F-15C material spans different F100 variants and upgrades.
- Geometry: broad public values exist.
- Mass/CG/inertia: not closed to one exact source state in the current primary-source chain.
- Aero: no single coherent public coefficient chain identified for one exact F-15C configuration.
- Flight-test correlation: broad family material exists, but configuration identity is not sufficiently bounded.
- Major gap: the label itself is too broad to satisfy R0.5.
- Final score: **9/26**.
- Classification: **INCOMPATIBLE** as the one exact Maverick reference target.

## 4. RPV anchor record

`NASA_F15_RPV_3_8_BASIC1_CG26_BLOCKED_INLETS` remains a **SUPPORTING_REFERENCE**, not a full-scale candidate.

It is valuable because its configuration, mass properties, subsonic flight-derived derivatives, and high-alpha research chain are unusually explicit. Its dimensional quantities do not score toward full-scale geometry, mass, inertia, or propulsion completeness.

## 5. Candidate-selection conclusion

The target-selection problem is not “which F-15 has the most NASA papers?” It is “which exact full-scale aircraft lets the project preserve a coherent configuration identity while exposing rather than hiding its data gaps?”

NASA 836 / S/N 74-0141 in the pre-Quiet-Spike F100-PW-100 baseline state gives the strongest answer.

**Exactly one `FULL_SCALE_TARGET` is selected: `NASA_F15B_836_SN74_0141_PRE_QUIET_SPIKE_BASELINE_F100_PW_100`.**
