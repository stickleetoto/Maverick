# F-22 Public Source Acquisition — Deep Dive R0 M2

Status: **PUBLIC-EVIDENCE MILESTONE — propulsion/test provenance strengthened; no numeric aircraft implementation**

Target remains, where directly supported:

`PRODUCTION_F22A_PUBLIC_REFERENCE`

Mandatory separation remains:

- YF-22 prototype;
- F-22 EMD / developmental aircraft;
- production F-22A;
- generic ATF studies;
- F119 development/test configurations.

This milestone focuses on the public F119/F-22 propulsion-test evidence ceiling and the continuing search for the missing F-22 dynamic wind-tunnel dataset.

## 1. Major result

A strong public U.S. Air Force propulsion-test paper now establishes that the F/A-22/F119 development program possessed and flight/ground validated:

- a full-envelope real-time state-variable engine model (`STORM`);
- a nonlinear aerothermodynamic component-level model (`CLM`);
- real-time/post-flight engine gross-thrust estimation capability;
- Kalman-filter observer / predictor model structure;
- gas-generator state-variable dynamics plus an aerothermal augmentor model;
- explicit calibration for engine-to-engine variation and degradation;
- installed inlet-pressure-model corrections discovered during F/A-22 flight test;
- rapid pitch-axis nozzle-vectoring ground-test cases.

This materially strengthens the public propulsion architecture and validation record.

It does **not** publish the numerical F119 Mach/altitude/power thrust deck, internal engine maps, production control schedules, or installed inlet-recovery surface. Therefore F-22 propulsion remains `PUBLIC_BUT_INSUFFICIENT_FOR_NUMERIC_MODEL` rather than `CLOSED_EXACT` for a thrust model.

## 2. F/A-22/F119 propulsion-test source

### F22-M2-SRC-001

- **Title:** *Knowledge Gained from F/A-22/F119 Propulsion System Ground and Flight Test Analysis*
- **Authors:** Allan T. Webb; David S. Kidman; Donald J. Malloy
- **Organizations:** Air Force Flight Test Center, 412th Test Wing, Edwards AFB; Aerospace Testing Alliance / Arnold Engineering Development Center, Arnold AFB
- **Publication:** *Flight Test — Sharing Knowledge and Experience*
- **Proceedings:** RTO-MP-SCI-162, Paper 23
- **Publication date:** 2005
- **Printed pages:** 23-1 through 23-14
- **Publisher/program:** NATO Research and Technology Organisation (RTO), Flight Test symposium proceedings
- **Official-proceedings identity:** RTO-MP-SCI-162; public defense-library catalogs identify the proceedings as a 2005 NATO/RTO publication
- **Official NATO PDF retrieval:** attempted; NATO STO public URL returned HTTP 403 in this session, so no local hash is claimed
- **SHA256:** `NOT_COMPUTED — official PDF bytes not stored`
- **Configuration:** F/A-22 / F119 developmental ground and flight-test program
- **Classification:** `PRIMARY_EXACT` for the described F/A-22/F119 test/analysis architecture; `PUBLIC_BUT_INSUFFICIENT_FOR_NUMERIC_MODEL` for production F-22A/F119 performance reconstruction

### 2.1 Publicly documented STORM architecture

The Pratt & Whitney-developed **Full Envelope State Variable Piecewise Linear Self-Tuning On-Board Real-Time Model (STORM)** uses:

- an engine-model observer;
- Kalman filtering of differences between model output and engine-control sensor values;
- self-tuning component efficiencies;
- a predictor that estimates parameters including fan airflow and engine gross thrust;
- normalized piecewise-linear state-variable models for the gas generator;
- an aerothermal afterburner model;
- low-frequency spool / hot-section heat-transfer dynamics.

This is much stronger evidence than a generic public F119 thrust rating: it proves a test-validated, state-aware propulsion model existed and was used with F/A-22/F119 development data.

### 2.2 Nonlinear component-level model

The paper also describes a self-tuning nonlinear aerothermodynamic **component-level model (CLM)** including duct, fan, high-pressure compressor, burner, high/low-pressure turbines, augmentor and nozzle elements with conservation-law convergence.

Publicly stated uses include:

- post-flight analysis;
- online performance monitoring;
- in-flight thrust calculation;
- sensor validation;
- problem resolution;
- model calibration / fault detection.

### 2.3 Public validation anchors

For a representative middle-of-envelope maximum-power -> idle -> maximum-power throttle snap, the paper reports:

- transient gross-thrust RMS error between ground-test measurement and CLM prediction: **less than 2 percent**;
- transient gross-thrust RMS error between ground-test measurement and STORM prediction: approximately **2.5 percent**.

These are model-validation metrics, **not thrust values** and not a thrust deck.

The paper explicitly states that model fidelity and measurement uncertainty depend on altitude, Mach number and power setting.

### 2.4 Installed inlet / engine-control interaction

One of the most useful public technical findings concerns installed inlet pressure.

The F/A-22 engine control sensed inlet pressure through eight wall-static ports manifolded to a FADEC pressure transducer. During development, inaccurate characterization of the relationship between the sensed inlet static pressure and total pressure at the engine face required:

- engine-control-logic changes;
- repeat testing with revised logic;
- improved characterization of the installed total-to-static pressure relationship.

This directly proves that a standalone uninstalled F119 thrust rating is insufficient for a provenance-grade F-22 installed propulsion model.

### 2.5 Nozzle-vectoring test evidence

The paper includes ground-test analysis involving rapid sinusoidal pitch-axis vectoring of the two-dimensional convergent-divergent nozzle and associated augmentor-pressure instrumentation analysis.

This supports public evidence for integrated propulsion/nozzle test methodology, but it does not publish a production nozzle dynamic transfer function or final allocation schedule.

## 3. Additional F119 public evidence

### F22-M2-SRC-002 — NASA F119 nozzle-flap test record

- **Title:** *F119 Nozzle Flaps Tested at Lewis' CE-22 Facility*
- **NTRS:** 20050169937
- **Organization:** NASA Lewis Research Center with Pratt & Whitney / Air Force cooperative test program
- **Publication record date:** 2005
- **Distribution:** Public
- **Stable URL:** https://ntrs.nasa.gov/citations/20050169937
- **Classification:** `PRIMARY_FAMILY_ONLY`; `PUBLIC_BUT_INSUFFICIENT_FOR_NUMERIC_MODEL`

The NTRS record states that redesigned low-observability F119 nozzle flaps were tested over multiple geometric variations and nozzle-pressure ratios, with internal thrust and flow coefficients measured and compared with Pratt & Whitney CFD.

No downloadable numeric database is exposed by the NTRS record. It strengthens the existence/validation chain for nozzle internal performance but cannot supply an aircraft-installed thrust deck.

### F22-M2-SRC-003 — Pratt & Whitney production F119 public specification

Pratt & Whitney's public F119 product page remains direct production-family authority that:

- two F119 engines power the F-22;
- the nozzle is two-dimensional and convergent/divergent;
- pitch vectoring reaches **20 degrees up or down**.

Classification remains `PRIMARY_EXACT` for those high-level production facts only. No rate, thrust surface, fuel-flow map or installation loss is inferred.

## 4. F-22 aerodynamic source hunt — Gillard status

### F22-M2-LEAD-001

- **Title:** *AFRL F-22 Dynamic Wind Tunnel Test Results*
- **Author:** William J. Gillard
- **Document:** AIAA 1999-4015
- **Conference:** 24th Atmospheric Flight Mechanics Conference, Portland, Oregon, 1999
- **Pages:** 109–117 in public bibliographic records
- **DOI:** 10.2514/6.1999-4015
- **Current acquisition state:** `NOT_ACQUIRED`

The targeted search again did **not** locate a lawful freely accessible full primary paper/data package.

Public bibliographic summaries consistently state that the AFRL program included:

- rotary-balance testing;
- forced oscillation in all three body axes;
- comparison with NASA Langley Spin Tunnel and large/full-scale tunnel datasets;
- generally close rotary-balance correlation;
- less consistent forced-oscillation derivative correlation.

No derivative number from those summaries is promoted.

This remains the highest-value single missing F-22 aerodynamic document.

## 5. Why F119 is still not implementable exactly

The new source establishes a sophisticated test model and its accuracy, but does not release the model coefficients/data needed to reproduce it.

Still absent from public primary authority:

- net/gross thrust versus Mach, altitude and power setting;
- military/minimum-AB/maximum-AB scheduled surfaces;
- fuel-flow maps;
- spool/state-variable matrices;
- production engine-control schedules;
- installed inlet total-pressure-recovery map;
- detailed nozzle force/application geometry;
- nozzle actuator dynamics;
- left/right installed engine reference stations and thrust lines.

Therefore `STORM EXISTS` does not imply `STORM DATA ARE PUBLIC`.

## 6. Updated F-22 evidence ceiling

### PUBLICLY REPRODUCIBLE

- high-level production geometry/dimensions from official public sources;
- F119-PW-100 twin-engine identity;
- public thrust-class description only;
- 2D pitch-vectoring nozzle and +/-20 deg range;
- EMD high-AOA flight-test anchors;
- selected development-era closed-loop handling-quality design targets.

### PUBLICLY CONSTRAINABLE

Added by this milestone:

- F119 dynamic-model architecture;
- existence of full-envelope piecewise-linear state-variable modeling;
- gross-thrust observer/predictor methodology;
- representative transient-model error bounds at a test condition;
- installed inlet `Pt/Ps` coupling significance;
- nozzle-vectoring propulsion-test behavior and validation methodology.

### REQUIRES APPROXIMATION

Still required for a flyable public-evidence F-22 model:

- complete nonlinear aerodynamic coefficient surfaces;
- dynamic/control derivatives outside any future acquired public test points;
- final FCS gains/control allocation;
- surface/nozzle actuator dynamics;
- exact installed F119 thrust/fuel/transient surfaces;
- mass/CG/inertia schedule.

### NOT PUBLICLY AVAILABLE / NOT ACQUIRED

No held public source supplies:

- production `CX/CY/CZ/Cl/Cm/Cn` deck;
- production dynamic-derivative dataset;
- final production OFP tables;
- production mass/inertia package;
- full STORM/CLM numerical engine model;
- full Gillard AIAA 99-4015 numeric wind-tunnel dataset.

## 7. Top five next F-22 acquisitions

1. Lawful full primary copy/data supplement for **AIAA 99-4015, Gillard, AFRL F-22 Dynamic Wind Tunnel Test Results**.
2. Original public report/data package for the **1992 Langley F-22 Full-Scale Tunnel** static/high-AOA campaign.
3. Original public report/data package for the **1993 Langley F-22 Spin Tunnel / rotary-balance** campaign.
4. Public EMD/production-compatible **F-22 flight-control / control-allocation / actuator report** containing final surface/nozzle limits, rates and schedules.
5. Public F119 test/performance package containing **actual Mach-altitude-power thrust/fuel/transient arrays**, not only validation metrics.

## 8. Readiness verdict

**F-22: `REFERENCE-ONLY — INSUFFICIENT NUMERIC DATA`.**

This milestone materially improves propulsion-model constraints and validation provenance, but it does not change the overall implementation-readiness class.

A public-evidence approximation can be made more disciplined than before: it can reproduce official geometry/engine/nozzle identity, honor EMD envelope observations, target published handling-quality behavior, and use a propulsion surrogate shaped to respect the published STORM/CLM validation architecture.

It still cannot honestly be labeled an exact production F-22A aerodynamic/propulsion model without unavailable or approximate plant data.
