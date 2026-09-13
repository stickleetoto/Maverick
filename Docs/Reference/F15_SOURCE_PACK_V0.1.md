# F-15 Reference Source Pack v0.1

Status: **RESEARCH REFERENCE — configuration-tagged**

Purpose: collect the F-15 material found during Maverick research. The F-15 public record is broad and unusually rich in high-angle-of-attack, spin, flight-derivative, and propulsion work, but it is distributed across many different test configurations. This pack therefore emphasizes **configuration separation**.

The pack does **not** define a single unified F-15 model yet.

---

## 1. Baseline / geometry / flight-dynamics references

### 1.1 NASA TM-X-62360 — *Low Speed Aerodynamic Characteristics of an 0.075-Scale F-15 Airplane Model at High Angles of Attack and Sideslip*

- NTRS PDF: https://ntrs.nasa.gov/archive/nasa/casi.ntrs.nasa.gov/19780002076.pdf
- NTRS document ID: `19780002076`
- Role: **PUBLIC_REFERENCE — low-speed/high-alpha static aerodynamics**
- Test conditions / coverage surfaced during research:
  - Mach approximately 0.16
  - angle of attack from 0 to +90 deg and from -40 to -80 deg
  - sideslip roughly -20 to +30 deg
  - multiple Reynolds numbers
  - stabilator settings, differential stabilator, inlet-ramp angles, alternate nose shapes, and external-store configurations
- Maverick use:
  - high-alpha static coefficient development
  - inverted/high-alpha behavior research
  - configuration-effect studies
- Caveat:
  - wind-tunnel scale model; must not be treated as a full dynamic/spin model by itself

### 1.2 NASA TN-D-8136 — *Subsonic Stability and Control Derivatives for an Unpowered, Remotely Piloted 3/8-Scale F-15 Airplane Model Obtained from Flight Test*

- NTRS: https://ntrs.nasa.gov/citations/19760008088
- Role: **PUBLIC_REFERENCE — flight-derived stability/control derivatives**
- Coverage:
  - unpowered 3/8-scale remotely piloted F-15
  - alpha approximately -20 to +53 deg
  - flight-derived derivatives with uncertainty information
- Maverick use:
  - compare wind-tunnel model trends against actual flight-derived derivatives
  - validate high-alpha stability/control derivative behavior
- Strength:
  - especially valuable because it is derived from flight test rather than wind tunnel alone

### 1.3 NASA TN-D-7941 — *Development of a Remote Digital Augmentation System and Application to a Remotely Piloted Research Vehicle*

- NTRS: https://ntrs.nasa.gov/citations/19750012221
- Role: **PUBLIC_REFERENCE — 3/8-scale F-15 RPV architecture / high-alpha research**
- Provides:
  - remote digital augmentation approach
  - 3/8-scale F-15 research-vehicle control architecture
  - high-angle-of-attack flight-test context
- Maverick use:
  - configuration / control-system context for other 3/8-scale F-15 derivative papers

### 1.4 NASA TM-72861 — F-15 aerodynamic / geometry reference surfaced during research

- NTRS PDF: https://ntrs.nasa.gov/api/citations/19790015808/downloads/19790015808.pdf
- NTRS document ID: `19790015808`
- Role: **PUBLIC_REFERENCE — geometry/control-surface data**
- Values surfaced during research include approximately:
  - wing reference area 56.61 m^2
  - span 13.05 m
  - mean aerodynamic chord about 4.86 m
  - aspect ratio about 3.0
  - leading-edge sweep about 45 deg
  - aileron limit around +/-20 deg
  - stabilator travel roughly +15 / -26 deg
- Rule:
  - verify exact table/page and aircraft configuration before freezing values into code

---

## 2. High-alpha, departure, and spin references

This is the strongest part of the public F-15 data set and is a major reason to treat the F-15 as a second serious Maverick reference aircraft.

### 2.1 NASA CR-3478 — *F-15 Rotary Balance Data for an Angle-of-Attack Range of 8 deg to 90 deg*

- NTRS: https://ntrs.nasa.gov/citations/19820016292
- Role: **PUBLIC_REFERENCE — rotary-balance raw data**
- Coverage:
  - 1/12-scale F-15 model
  - alpha 8 to 90 deg
  - clockwise/counter-clockwise rotational flow
  - nondimensional rotational rate `omega*b/(2V)` 0 to 0.4
  - selected configurations extended to 0.9
  - component build-up and control deflections
- Maverick use:
  - spin/post-stall rotational aerodynamic model
  - rotary damping and control-effectiveness development

### 2.2 NASA CR-3479 — *Analysis of Rotary Balance Data for the F-15 Airplane Including the Effect of Conformal Fuel Tanks*

- NTRS: https://ntrs.nasa.gov/citations/19820014339
- Role: **PUBLIC_REFERENCE — rotary data analysis / spin prediction**
- Provides:
  - analysis of control deflections, Reynolds number, airframe components, and CFT effects
  - aerodynamic discussion up to 90 deg alpha
  - steady-state spin predictions
  - comparison with spin-tunnel and flight-test results
- Important finding surfaced during research:
  - conformal tanks caused only modest rotary-aero changes overall but could make some spin modes slightly flatter/faster and increase departure susceptibility
- Maverick use:
  - crucial bridge from raw rotary balance to actual spin-mode prediction

### 2.3 NASA CR-3516 — *Rotary Balance Data for an F-15 Model with Conformal Fuel Tanks for an Angle-of-Attack Range of 8 deg to 90 deg*

- NTRS: https://ntrs.nasa.gov/citations/19820016293
- Role: **PUBLIC_REFERENCE — F-15 CFT rotary-balance raw data**
- Coverage:
  - alpha 8 to 90 deg
  - rotational rate up to 0.4, selected cases to 0.9
- Restriction:
  - CFT configuration must be kept separate from a clean baseline F-15

### 2.4 NASA TN-D-8052 — F-15 large-scale RPV spin research

- NTRS: https://ntrs.nasa.gov/citations/19760010068
- Role: **PUBLIC_REFERENCE — departure/spin/recovery research**
- Research value:
  - large-scale / remotely piloted F-15 spin behavior
  - simulation and flight-test context extending to extreme alpha/beta regimes
- Use:
  - future departure/spin state-machine and rotational-aerodynamics validation

---

## 3. Full-scale / flight-test aerodynamic references

### 3.1 NASA TM-4604 — *Dynamic Ground Effects Flight Test of an F-15 Aircraft*

- NTRS: https://ntrs.nasa.gov/citations/19950005778
- Role: **PUBLIC_REFERENCE — full-scale dynamic ground effect**
- Flight-test content:
  - low/high sink-rate approaches
  - 150 kn flaps-down and 170 kn flaps-up cases
  - measured changes in lift, drag, and pitching moment near the ground
- Geometry surfaced in the report family:
  - span about 42.83 ft
  - wing area about 608 ft^2
  - aspect ratio about 3.02
  - leading-edge sweep about 45 deg
- Maverick use:
  - future landing / ground-effect model after airborne reference dynamics are stable

### 3.2 Dynamic ground-effects conference version

- NTRS PDF: https://ntrs.nasa.gov/archive/nasa/casi.ntrs.nasa.gov/19950026603.pdf
- NTRS document ID: `19950026603`
- Role: **supporting PUBLIC_REFERENCE**
- Notes:
  - related full-scale F-15 ground-effect results
  - 24 landings across seven flights are described

### 3.3 NASA/TM-2003-212027 — flight-data stability/control derivative extraction

- NTRS: https://ntrs.nasa.gov/citations/20030079970
- Role: **PUBLIC_REFERENCE — modified F-15 flight-system identification**
- Provides:
  - stability/control derivative extraction from actual flight data
  - comparison between identified derivatives and onboard/model predictions
- Caveat:
  - modified NASA aircraft; configuration must be recorded before using numeric derivatives

### 3.4 Real-Time Dynamic Modeling / F-15 flight research

- NTRS: https://ntrs.nasa.gov/citations/20080034476
- Role: **PUBLIC_REFERENCE — real-time derivative identification on modified F-15B**
- Use:
  - control-surface definitions
  - real-time stability/control identification methodology
  - future automated validation / AI-system-identification pipeline

### 3.5 PreSISE / simultaneous excitation research

- NTRS: https://ntrs.nasa.gov/citations/20110012932
- Role: **PUBLIC_REFERENCE — flight-test system-identification methodology**
- Use:
  - simultaneous control-surface excitation at distinct frequencies
  - near-real-time estimation of multiple stability/control derivatives
- Maverick value:
  - strong template for automated FDM identification and regression experiments later

---

## 4. Transonic / configuration-specific F-15 aerodynamic references

These reports are useful, but several are based on a **three-surface research configuration**, not a normal baseline Eagle. Treat them as configuration-specific unless a parameter is proven transferable.

### 4.1 NASA TP-2234 — three-surface F-15 lateral-directional data

- NTRS: https://ntrs.nasa.gov/citations/19840005097
- Role: **PUBLIC_REFERENCE — three-surface research configuration**
- Coverage surfaced during research:
  - Mach about 0.6 to 1.2
  - alpha roughly -2 to +15 deg
  - lateral-directional characteristics
  - rudder/nozzle effects
- Maverick use:
  - methodology and transonic trend study
- Restriction:
  - not a baseline F-15 coefficient authority without configuration matching

### 4.2 NASA TP-2043 — three-surface F-15 longitudinal data

- NTRS: https://ntrs.nasa.gov/citations/19820024444
- Role: **PUBLIC_REFERENCE — three-surface research configuration**
- Coverage:
  - Mach approximately 0.6 to 1.2
  - longitudinal aerodynamics
  - horizontal-tail/nozzle-related effects
- Restriction:
  - configuration-specific

### 4.3 NASA TP-2333 — fuselage / nozzle pressure-distribution research

- NTRS PDF: https://ntrs.nasa.gov/archive/nasa/casi.ntrs.nasa.gov/19840024284.pdf
- Role: **PUBLIC_REFERENCE — propulsion/airframe interaction research**
- Coverage surfaced during research:
  - F-15-related configuration
  - Mach about 0.6 to 1.2
  - low/moderate alpha
  - nozzle pressure-ratio effects
- Use:
  - future propulsion-airframe interaction / afterbody research

---

## 5. F100 / propulsion references

Exact engine variant matters. Data from an F100 EMD/research engine must not be advertised as an exact production F-15C/F-15E engine model unless the engine identity matches.

### 5.1 *Real-Time In-Flight Thrust Calculation*

- NTRS PDF: https://ntrs.nasa.gov/archive/nasa/casi.ntrs.nasa.gov/19860015884.pdf
- NTRS document ID: `19860015884`
- Role: **PUBLIC_REFERENCE — in-flight thrust-estimation methodology**
- Provides:
  - DEEC/F100-equipped F-15 flight-research context
  - real-time engine thrust calculation methodology
- Maverick use:
  - engine telemetry / thrust validation architecture

### 5.2 NASA TM-85902 — F100 EMD flight evaluation

- NTRS: https://ntrs.nasa.gov/citations/19840016520
- Role: **PUBLIC_REFERENCE — F100 EMD / F-15 flight research**
- Research areas:
  - thrust
  - fuel flow / airflow
  - throttle transients
  - airstart behavior
  - flight performance
- Caveat:
  - EMD engine is a research/development configuration; do not silently treat its numbers as a generic F100-PW-100/220/229 deck

### 5.3 PCA / propulsion-controlled-aircraft F-15 work

- NTRS PDF: https://ntrs.nasa.gov/api/citations/19970001362/downloads/19970001362.pdf
- NTRS document ID: `19970001362`
- Role: **PUBLIC_REFERENCE — modified NASA F-15/PCA configuration**
- Useful data surfaced during research:
  - mass / inertia / CG variation with fuel state
  - propulsion-control architecture context
- Restriction:
  - modified NASA aircraft; values are configuration-specific

---

## 6. F-15 ACTIVE / thrust-vectoring research

### 6.1 F-15 ACTIVE inner-loop thrust-vectoring control research

- NTRS PDF: https://ntrs.nasa.gov/api/citations/19990089839/downloads/19990089839.pdf
- NTRS document ID: `19990089839`
- Role: **PUBLIC_REFERENCE — F-15 ACTIVE only**
- Provides:
  - pitch/yaw thrust-vectoring nozzle integration
  - inner-loop control-law / dynamic-inversion research
- Maverick use:
  - future separate ACTIVE/TVC aircraft profile
- Restriction:
  - never merge directly into baseline F-15 handling

---

## 7. Recommended source graph for a Maverick F-15

Unlike the F-16 Morelli chain, the F-15 should be built as a source graph with explicit configuration tags.

### F15-R0 — clean low-speed / subsonic reference

Candidate evidence:

- TM-X-62360 for static/high-alpha coefficient trends
- TN-D-8136 for flight-derived stability/control derivatives
- TM-72861 family for geometry/control-surface data
- TN-D-7941 for 3/8-scale research-vehicle configuration context

Goal:

- establish one clean reference configuration
- freeze geometry, mass/inertia, axes, control conventions, and a bounded subsonic envelope

### F15-R1 — departure / spin extension

Candidate evidence:

- CR-3478 raw rotary-balance data
- CR-3479 analysis and spin-mode prediction
- TN-D-8052 RPV spin results
- CR-3516 only when intentionally modeling CFT effects

Goal:

- real rotational-flow aerodynamics instead of arbitrary “stall damping”
- departure/spin/recovery validation

### F15-R2 — transonic extension

Candidate evidence:

- TP-2234
- TP-2043
- TP-2333

Goal:

- research transonic trends and propulsion/afterbody effects

Warning:

- these reports include non-baseline research configurations; numeric data must not be transplanted until the geometry/configuration match is proven

### F15-R3 — propulsion

Candidate evidence:

- in-flight thrust-calculation report
- TM-85902 F100 EMD research
- PCA fuel/mass/CG reports

Goal:

- choose a specific engine / aircraft configuration first
- only then freeze a thrust model

---

## 8. Configuration identity — mandatory tags

At minimum distinguish:

- clean/basic F-15 aerodynamic model
- F-15 with external stores
- F-15 with conformal fuel tanks
- 3/8-scale remotely piloted research F-15
- 1/12-scale rotary-balance model
- three-surface F-15 research configuration
- modified NASA F-15B
- PCA F-15
- F-15 ACTIVE
- F-15E / Strike Eagle

A datum from one tag may be used for another only after explicit compatibility analysis.

This is especially important because the repository already contains `Docs/F15E_SPEC_RESEARCH_AND_GAME_TUNING.md`; F-15E gameplay tuning must not become the provenance source for a clean reference F-15 model.

---

## 9. High-value facts discovered so far

These are research-index facts, not yet a frozen implementation table:

- Baseline F-15 wing reference area around **56.6 m^2 / 608 ft^2** is independently visible in NASA report families.
- Span is around **13.05 m / 42.83 ft**.
- Aspect ratio is about **3.0**.
- Mean aerodynamic chord around **4.86 m** appears in the geometry reference surfaced during research.
- Static wind-tunnel data exists to **+90 deg alpha**.
- Flight-derived subsonic derivatives exist to roughly **+53 deg alpha** on the unpowered 3/8-scale RPV.
- Rotary-balance data exists to **90 deg alpha** and nondimensional rotation rates up to **0.9** in selected cases.
- Separate CFT rotary-balance and analysis data exist.
- Full-scale ground-effect flight data exist.
- Transonic research data exist into approximately **Mach 1.2**, but often on a three-surface research configuration.
- F100/F-15 flight-research material exists for thrust estimation, engine dynamics, and fuel/mass/CG changes.

Each number must be tied to its exact source/configuration before code freeze.

---

## 10. Why the F-15 is especially valuable for Maverick

The F-15 source set is unusually strong for the behaviors that game flight models often fake:

- high-alpha static aerodynamics
- flight-derived stability/control derivatives
- rotational-flow aerodynamics
- spin prediction
- departure susceptibility
- CFT effects
- real ground-effect flight data
- transonic research
- flight-based engine/thrust work

That makes the F-15 an excellent second reference aircraft after the F-16: the F-16 gives Maverick a clean end-to-end nonlinear reference chain, while the F-15 can push the project into **departure/spin/high-alpha dynamics backed by actual test data rather than arbitrary damping**.
