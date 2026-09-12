# F-16 Reference Source Pack v0.1

Status: **RESEARCH REFERENCE — configuration-tagged**

Purpose: collect the F-16 material found during Maverick Phase 5 research in one place, with enough context to know what may be used directly and what must remain a cross-check or separate research-aircraft branch.

The current Maverick reference target is **NASA Reference F-16**, not a claim of exact production F-16C Block fidelity.

---

## 1. Primary reference chain

### 1.1 Eugene A. Morelli — *Global Nonlinear Parametric Modeling with Application to F-16 Aerodynamics*

- NTRS citation: https://ntrs.nasa.gov/citations/19990008037
- Public PDF record also used during audit: https://ntrs.nasa.gov/api/citations/20040110310/downloads/20040110310.pdf
- Publication: American Control Conference, 1998
- Role: **AUTHORITATIVE for the current compact F-16 aerodynamic polynomial**
- What it provides:
  - nonlinear parametric models for `CX CY CZ Cl Cm Cn`
  - dependence on alpha, beta, control deflections, and nondimensional body rates
  - polynomial coefficient set used by Maverick
  - CG correction structure
  - subsonic wind-tunnel basis
  - reported moderate-AoA doublet agreement within roughly 10% of the underlying wind-tunnel model
- Important limits:
  - compact model is subsonic
  - current Maverick reference gate uses the source-supported `Mach < 0.6` domain
  - wind-tunnel alpha/beta domain must not be confused with FLCS envelope limits
- Maverick use:
  - coefficient implementation authority
  - coefficient-level regression
  - reference-domain gate

### 1.2 NASA TP-1538 — Nguyen et al., *Simulator Study of Stall/Post-Stall Characteristics of a Fighter Airplane With Relaxed Longitudinal Static Stability*

- NTRS: https://ntrs.nasa.gov/citations/19800005879
- PDF: https://ntrs.nasa.gov/api/citations/19800005879/downloads/19800005879.pdf
- Report: NASA-TP-1538, December 1979
- Role: **AUTHORITATIVE / PUBLIC_REFERENCE for F-16-based nonlinear simulation data and high-alpha behavior**
- What it provides or supports:
  - F-16-based fighter geometry and mass-property reference data
  - stall/post-stall behavior
  - deep-stall and inertia-coupling departure behavior
  - aerodynamic build-up conventions
  - control-system structure used by the study
  - leading-edge-flap build-up convention
  - public thrust table material including idle / military / maximum thrust versus Mach and altitude (Table VI)
  - body-axis sign conventions used to validate load-factor and inertia equations
- Important caveat:
  - this is a research simulation configuration based on F-16 data, not proof of an exact operational F-16C FLCS implementation
- Maverick use:
  - reference mass/inertia cross-check
  - engine/thrust source candidate
  - high-alpha architecture research
  - body-axis / load-factor validation

### 1.3 Garza & Morelli — *A Collection of Nonlinear Aircraft Simulations in MATLAB*

- NTRS: https://ntrs.nasa.gov/citations/20030013626
- Report: NASA/TM-2003-212145
- Role: **AUTHORITATIVE / PUBLIC_REFERENCE for nonlinear simulation structure**
- What it provides:
  - six-degree-of-freedom simulation architecture
  - trim / integration / state-equation organization
  - F-16 aerodynamic and engine model structure
  - throttle-to-commanded-power mapping
  - engine-power dynamics
  - thrust interpolation structure as a function of altitude, Mach, and engine power
- Important restriction:
  - an associated NASA software package has release restrictions; Maverick must use the public report, not copy restricted files
- Maverick use:
  - engine-power equations
  - nonlinear 6DoF architecture cross-check
  - trim and integration design reference

---

## 2. F-16 flight-data / system-identification references

### 2.1 *A Preliminary Analysis of Flight Data from the AFTI/F-16 Airplane*

- NTRS: https://ntrs.nasa.gov/citations/19840059553
- AIAA Paper 84-2085
- Role: **PUBLIC_REFERENCE for AFTI/F-16 flight-derived derivatives; configuration-specific**
- Provides:
  - estimated stability and control derivatives from flight data
  - comparison with wind-tunnel and F-16A flight-test values
  - evidence that coordinated surface motions and near-neutral stability complicate identification
- Do not use as direct production F-16C values.

### 2.2 AFTI/F-16 flight-control research

- NTRS reference surfaced during research: https://ntrs.nasa.gov/citations/19840012499
- Role: **configuration-specific control-law research**
- Use:
  - architecture ideas such as digital / decoupled / adaptive control-law research
- Restriction:
  - not a production F-16C FLCS authority

### 2.3 NASA CR-4226 — AFTI/F-16 reconfigurable / variable-gain control-law research

- NTRS: https://ntrs.nasa.gov/citations/19890009945
- Role: **configuration-specific research**
- Use:
  - reconfigurable control-law / actuator-failure accommodation concepts
- Restriction:
  - not baseline F-16 control-law data

### 2.4 Morelli — dynamic-model accuracy / parameter-estimation sensitivity study

- NTRS: https://ntrs.nasa.gov/citations/20140003885
- NASA/TM-2013-218056
- Role: **PUBLIC_REFERENCE for validation methodology**
- Use:
  - demonstrates sensitivity of identified dynamics to mass properties, geometry, and sensor errors
  - supports Maverick policy that CG, inertia, and provenance must be closed before claiming model fidelity

### 2.5 *Autonomous Real-Time Global Aerodynamic Modeling*

- NTRS: https://ntrs.nasa.gov/citations/20200003104
- Role: **PUBLIC_REFERENCE for aerodynamic identification/model-building methodology**
- Use:
  - local-to-global derivative/model identification concepts using an F-16 nonlinear simulation
- Restriction:
  - not a new authoritative replacement coefficient table for Maverick

---

## 3. F-16XL research — useful but NOT baseline F-16 data

F-16XL material is valuable for delta-wing/high-alpha/unsteady-aerodynamic research, but its geometry and aerodynamics differ substantially from the baseline F-16 reference.

### 3.1 NASA/TM-1999-209703 — *Low-Speed Aerodynamic Data for an 0.18-Scale Model of an F-16XL with Various Leading-Edge Modifications*

- NTRS: https://ntrs.nasa.gov/citations/20000021569
- Role: **PUBLIC_REFERENCE, F-16XL only**
- Provides:
  - tabulated low-speed aerodynamic data
  - leading-edge configuration comparisons
  - tunnel test conditions and raw tables rather than only narrative analysis
- Use:
  - future high-alpha / vortex / LE modification research
- Do not transfer coefficients to baseline F-16.

### 3.2 F-16XL high-angle / oscillatory aerodynamic work

- NTRS PDF surfaced during research: https://ntrs.nasa.gov/archive/nasa/casi.ntrs.nasa.gov/19980007406.pdf
- Report family: NASA/TM-97-206276
- Role: **PUBLIC_REFERENCE, F-16XL only**
- Provides:
  - static and oscillatory aerodynamics at high angle of attack
  - control-deflection and dynamic-response information
- Use:
  - methodology for extending quasi-steady reference models toward unsteady/high-alpha work

### 3.3 *Progressive Aerodynamic Model Identification from Dynamic Water Tunnel Test*

- NTRS PDF: https://ntrs.nasa.gov/archive/nasa/casi.ntrs.nasa.gov/20040110755.pdf
- Role: **PUBLIC_REFERENCE, F-16XL only**
- Research value:
  - nonlinear/unsteady model identification in stall/high-alpha regimes
  - shows damping/unsteady terms can depend strongly on sideslip, oscillation amplitude, and state

### 3.4 *Unsteady Aerodynamic Effects on the Flight Characteristics of an F-16XL Configuration*

- NTRS: https://ntrs.nasa.gov/citations/20000109954
- Role: **PUBLIC_REFERENCE, F-16XL only**
- Important finding:
  - quasi-steady simulation can appear more laterally/directionally stable than the flight vehicle
  - unsteady models significantly change rapid sideslip/roll response
- Maverick implication:
  - later high-fidelity work may require unsteady aerodynamic states; do not “fix” such effects with arbitrary damping

---

## 4. VISTA / MATV — post-stall and thrust-vectoring research

### 4.1 *Vista/F-16 Multi-Axis Thrust Vectoring (MATV) Control Law Design and Evaluation*

- NTRS PDF: https://ntrs.nasa.gov/archive/nasa/casi.ntrs.nasa.gov/19950007834.pdf
- Role: **PUBLIC_REFERENCE for VISTA/MATV only**
- Provides:
  - control-law architecture using multi-axis thrust vectoring above the normal F-16 angle-of-attack limit
  - flight-test evidence that differences between the simulator aerodynamic database and actual aircraft aerodynamics materially changed lateral-directional flying qualities
- Maverick use:
  - basis for a future separate `F16_MATV`/VISTA-like profile
  - evidence that post-stall handling requires a different control/aero architecture
- Restriction:
  - never use this to claim a normal production F-16 can perform MATV post-stall maneuvers

---

## 5. External validation / comparison implementations

These are **CROSS_VALIDATION_ONLY** unless a specific datum is independently traced to a Tier 1 source.

### 5.1 AeroBench / AeroBenchVV

- https://github.com/pheidlauf/AeroBenchVV
- Role:
  - trim / trajectory / GCAS benchmark
  - useful behavior oracle and regression comparison
- Caveat:
  - surrounding damping/implementation details can differ from the exact Morelli compact polynomial path

### 5.2 AeroBenchVVPython

- https://github.com/stanleybak/AeroBenchVVPython
- Role:
  - Python implementation useful for reproducible coefficient/trajectory cross-validation
- Rule:
  - do not copy coefficient values over NASA/Morelli authority without independent source verification

### 5.3 Texas A&M `F16-Model-Matlab`

- https://github.com/isrlab/F16-Model-Matlab
- Role:
  - nonlinear F-16 implementation comparison
  - trim / linearization / aerodynamic table handling reference
- Classification: `CROSS_VALIDATION_ONLY`

### 5.4 JSBSim

- https://github.com/JSBSim-Team/jsbsim
- Role:
  - architecture and complete-FDM comparison
  - useful for checking conventions and feature decomposition
- Classification: `CROSS_VALIDATION_ONLY`

---

## 6. Data areas and current confidence

| Area | Best source(s) | Current confidence | Notes |
|---|---|---:|---|
| Geometry | TP-1538 / Morelli / Garza-Morelli | High | Keep configuration identity explicit |
| Mass / inertia | TP-1538 + NASA nonlinear-sim references | High | Maverick reference mass is 9298.65 kg; do not mix with legacy gameplay 9800 kg |
| Compact subsonic aerodynamics | Morelli 1998 | High | Current implementation authority |
| Alpha / beta domain | Morelli source database | High | Model validity, **not FLCS limit** |
| 6DoF equations | Garza & Morelli + TP-1538 conventions | High | Unity mapping separately validated |
| Engine power dynamics | Garza & Morelli | High | Commanded/actual power structure sourced |
| Dimensional thrust deck | TP-1538 Table VI | High candidate, transcription pending | Must be manually transcribed and cross-checked; no OCR guessing |
| Production F-16C FLCS gains | none accepted | Low / unavailable | Keep as `UNAVAILABLE` or `MAVERICK_TUNING` |
| Transonic / supersonic aero | no accepted baseline source yet | Low / unavailable | Current Morelli reference remains below Mach 0.6 |
| Normal production post-stall control | none accepted | Low | VISTA/MATV is a separate research configuration |
| Unsteady high-alpha aerodynamics | F-16XL research only | Medium methodology value | Not transferable numerically to baseline F-16 |

---

## 7. F-16 configuration separation rules

Never silently merge the following:

- NASA Reference F-16 / TP-1538-based simulation
- operational F-16A/B/C/D claims
- AFTI/F-16
- F-16XL
- VISTA/MATV
- third-party F-16 implementations

A source may be useful even when it cannot supply a direct number. For example, F-16XL unsteady-aero work can justify adding an unsteady-state architecture later, but it cannot justify copying an F-16XL damping derivative into the baseline F-16.

---

## 8. Recommended Maverick implementation order

1. Finish the current NASA Reference F-16 below the source-valid Mach boundary.
2. Validate isolated Unity 6DoF behavior against the reference equations.
3. Manually transcribe and freeze TP-1538 Table VI thrust data.
4. Add powered reference flight.
5. Build a separately sourced/identified control-law layer; do not call it production F-16C unless the public evidence supports that claim.
6. Treat transonic/high-alpha extensions as separate, provenance-tagged models.
7. If post-stall/TVC gameplay is desired, implement a separate VISTA/MATV-like aircraft profile rather than corrupting the standard reference model.
