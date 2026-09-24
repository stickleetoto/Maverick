# F/A-18 Known Data (r0)

Canonical data: [`KNOWN_DATA.json`](KNOWN_DATA.json) (the tables below are rendered from it; the JSON is authoritative if they differ).

> **No value on this page may be used in runtime code.** Every number was captured from a web-search extract of a named NASA document. None has been checked against the PDF page (verification ≤ L3; see [library README §4](../../README.md#4-r0-retrieval-limitation-read-this-before-trusting-anything)). The values say *what to verify and where*, and they expose configuration splits and conflicts early.

---

## 1. Configuration registry

Every source and every value is tagged with one or more of these IDs. "F/A-18" alone is never a configuration.

| ID | Label | Airframe / serial | Engine | FCS | Research hardware | Dates | Status |
|---|---|---|---|---|---|---|---|
| `FA18-CFG-PROD-AB` | Production F/A-18A/B (U.S. Navy) | Production family; block/lot not specified | F404-GE-400 x2 | Production digital FBW (PROM versions V8.x/V10.x family); production gains NOT public | — | — | REFERENCE_ONLY |
| `FA18-CFG-HARV-P1-BASIC` | NASA HARV Phase 1 — basic F-18 hardware/software configuration | F-18 full-scale development aircraft #6 (pre-production; built before the F/A-18 redesignation); **NASA 840, BuNo 160780** | F404-GE-400 x2 | Basic F-18 FCS (V8.3.3 per VDD; upgrade to V10.1 timing unverified) | Spin chute (why the Navy loaned this airframe), research instrumentation; no TV vanes | From April 1987 (phase end date unverified) | CANDIDATE_VALIDATION_CONFIG |
| `FA18-CFG-HARV-P2-TV` | NASA HARV Phase 2 — multi-axis thrust vectoring + RFCS | Same airframe as P1; **NASA 840, BuNo 160780** | F404-GE-400 x2 with 3 external TV vanes per engine | Basic F/A-18 FCS (V10.1 family) + RFCS (Pace 1750A, Ada) running NASA research laws (e.g., NASA-1A) | TV vane system (~2,200 lb); spin-chute / emergency-system / ballast changes (~1,500 lb) | Unverified (between Phase 1 and July 1995) | CANDIDATE_VALIDATION_CONFIG |
| `FA18-CFG-HARV-P3-ANSER` | NASA HARV Phase 3 — TV + Actuated Nose Strakes for Enhanced Rolling (ANSER) | Same airframe with modified forebody; **NASA 840, BuNo 160780** | F404-GE-400 x2 with TV vanes | RFCS running the ANSER research control law (NASA-TM-110217; v152.0 validated in NASA-CR-198250) | TV vanes + conformal actuated forebody strakes | ANSER flight data July 1995 - May 1996; program ended September 1996 | CANDIDATE_RESEARCH_TARGET |
| `FA18-CFG-SIM-F18BAS` | NASA Langley 'f18bas' simulation (NASA-TM-107601) | Simulation of the basic F/A-18 | F404 engine model (details unverified) | Basic F/A-18 control-system model (version unverified) | Preliminary HARV TV system model | 1992 | SIMULATION_REFERENCE |
| `FA18-CFG-SIM-F18HARV` | NASA Langley 'f18harv' simulation (NASA-TM-110216) | Simulation of the HARV; **Models NASA 840** | F404 engine model with TV effects (details unverified) | Host for NASA-1A / lateral-directional / ANSER research laws | TV vanes + actuated forebody strakes | 1996 | SIMULATION_REFERENCE |
| `FA18-CFG-SRA-845` | NASA F/A-18 Systems Research Aircraft | Pre-production two-seat F/A-18B; **NASA 845** | F404 (variant unverified) | Production-family FCS + systems experiments | EPAD EHA/EMA on left aileron; fly-by-light; power-by-wire | First research flight 21 May 1993 | NOT_AN_AERO_SOURCE |
| `FA18-CFG-AAW-853` | NASA F/A-18A Active Aeroelastic Wing (X-53) | Navy F/A-18A airframe 853 with wings taken from HARV 840, wing-box skins thinned; **NASA 853** | F404 (variant unverified) | AAW research control laws (NASA/TM-2005-213666) | Modified flexible wing; research instrumentation | Program from 1996; flight phases early 2003 and early 2005 | SEPARATE_CONFIGURATION |
| `FA18-CFG-FAST-853` | NASA Full-scale Advanced Systems Testbed (FAST) F/A-18 | F/A-18 (tail number per program pages 853; unverified); **NASA 853 (unverified)** | F404 (variant unverified) | Nonlinear dynamic inversion baseline research law (2011) | Research flight computers | c. 2011 | SEPARATE_CONFIGURATION |
| `FA18-CFG-UMN-DERIVED` | University of Minnesota falling-leaf analysis model | Derived simulation | Not the focus | Reconstructed baseline and revised F/A-18 control laws | — | 2011 | CROSS_VALIDATION_ONLY |
| `FA18-CFG-EF` | F/A-18E/F Super Hornet | Different aircraft (larger airframe, F414 engines) | F414-GE-400 | Different FCS | — | — | OUT_OF_SCOPE |
| `ENG-F404-GE-400` | General Electric F404-GE-400 (engine identity) | Installations: F/A-18A/B (twin), HARV (twin with TV vanes), X-29A (single) | F404-GE-400 | — | — | — | ENGINE_IDENTITY |

### Configuration traps found in r0

1. **One serial, three configurations.** NASA 840 changed mass (+4,119 lb), hardware (TV vanes plus spin-chute/emergency-system/ballast changes, later forebody strakes) and control laws (basic FCS, then RFCS research laws, then ANSER) across Phases 1–3. Mass properties in `NASA-TM-4772` are split by phase for this reason.
2. **One serial's parts on another serial.** HARV 840's wings, with thinned skins, flew on NASA 853 as the AAW/X-53.
3. **"Basic F-18" in NASA documents means the HARV airframe in its basic hardware/software state.** That is a pre-production FSD aircraft with research instrumentation and a spin chute. It is not a production F/A-18A lot.
4. **The f18bas simulation already contains a preliminary TV model.** "Baseline sim" does not automatically mean "no TV".
5. **F404-GE-400 data from X-29A tests** is engine-level only. The installation is incompatible.

## 2. Candidate values

Legend: `SEARCH_EXTRACT` = seen in a search-index extract of the named document, page not confirmed. `UNRESOLVED` source = extract did not say which of the candidate documents the value came from.

### Reference geometry

| Value ID | Quantity | Value | Units | Config | Source | Location | Exactness | Impl. use | Valid. use | Conflicts |
|---|---|---|---|---|---|---|---|---|---|---|
| `FA18-GEO-S` | Reference wing area | **400** | ft^2 | HARV-P1-BASIC, HARV-P2-TV, HARV-P3-ANSER | NASA-TM-4772 | Reference-geometry table (number/page not captured) | SEARCH_EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | CANDIDATE_AFTER_PAGE_VERIFICATION | — |
| `FA18-GEO-CBAR` | Reference mean aerodynamic chord | **11.52** | ft | HARV-P1-BASIC, HARV-P2-TV, HARV-P3-ANSER | NASA-TM-4772 | Reference-geometry table (number/page not captured) | SEARCH_EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | CANDIDATE_AFTER_PAGE_VERIFICATION | — |
| `FA18-GEO-B` | Reference span | **37.4** | ft | HARV-P1-BASIC, HARV-P2-TV, HARV-P3-ANSER | NASA-TM-4772 | Reference-geometry table (number/page not captured) | SEARCH_EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | CANDIDATE_AFTER_PAGE_VERIFICATION | — |
| `FA18-GEO-SPAN-PHYS` | Physical wing span (public fact page) | **37 ft 5 in** | ft-in | HARV-P1-BASIC, HARV-P2-TV, HARV-P3-ANSER | NASA-FS-002-DFRC | NOT_CAPTURED | PUBLIC_FACT_SHEET | PROHIBITED | IDENTITY_ONLY | — |

### Mass properties

| Value ID | Quantity | Value | Units | Config | Source | Location | Exactness | Impl. use | Valid. use | Conflicts |
|---|---|---|---|---|---|---|---|---|---|---|
| `FA18-MASS-P1-W` | Gross weight, Phase 1 (basic) loading | **31,980** | lbm | HARV-P1-BASIC | NASA-TM-4772 | Mass-properties table (number/page not captured) | SEARCH_EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | CANDIDATE_AFTER_PAGE_VERIFICATION | — |
| `FA18-MASS-P1-CG` | Longitudinal CG, Phase 1 | **21.9** | % MAC | HARV-P1-BASIC | NASA-TM-4772 | Mass-properties table | SEARCH_EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | CANDIDATE_AFTER_PAGE_VERIFICATION | — |
| `FA18-MASS-P1-FS` | CG fuselage station, Phase 1 | **454.33** | in (fuselage station) | HARV-P1-BASIC | NASA-TM-4772 | Mass-properties table | SEARCH_EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | CANDIDATE_AFTER_PAGE_VERIFICATION | — |
| `FA18-MASS-P1-IXX` | Roll moment of inertia, Phase 1 | **22,040** | slug-ft^2 | HARV-P1-BASIC | NASA-TM-4772 | Mass-properties table | SEARCH_EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | CANDIDATE_AFTER_PAGE_VERIFICATION | — |
| `FA18-MASS-P1-IYY` | Pitch moment of inertia, Phase 1 | **124,554** | slug-ft^2 | HARV-P1-BASIC | NASA-TM-4772 | Mass-properties table | SEARCH_EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | CANDIDATE_AFTER_PAGE_VERIFICATION | — |
| `FA18-MASS-P1-IZZ` | Yaw moment of inertia, Phase 1 | **139,382** | slug-ft^2 | HARV-P1-BASIC | NASA-TM-4772 | Mass-properties table | SEARCH_EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | CANDIDATE_AFTER_PAGE_VERIFICATION | — |
| `FA18-MASS-P23-W` | Gross weight, Phase 2/3 (TV / ANSER) loading | **36,099** | lbm | HARV-P2-TV, HARV-P3-ANSER | NASA-TM-4772 | Mass-properties table (number/page not captured) | SEARCH_EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | CANDIDATE_AFTER_PAGE_VERIFICATION | — |
| `FA18-MASS-P23-CG` | Longitudinal CG, Phase 2/3 | **23.8** | % MAC | HARV-P2-TV, HARV-P3-ANSER | NASA-TM-4772 | Mass-properties table | SEARCH_EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | CANDIDATE_AFTER_PAGE_VERIFICATION | — |
| `FA18-MASS-P23-FS` | CG fuselage station, Phase 2/3 | **456.88** | in (fuselage station) | HARV-P2-TV, HARV-P3-ANSER | NASA-TM-4772 | Mass-properties table | SEARCH_EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | CANDIDATE_AFTER_PAGE_VERIFICATION | — |
| `FA18-MASS-P23-IXX` | Roll moment of inertia, Phase 2/3 | **22,789** | slug-ft^2 | HARV-P2-TV, HARV-P3-ANSER | NASA-TM-4772 | Mass-properties table | SEARCH_EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | CANDIDATE_AFTER_PAGE_VERIFICATION | FA18-MASS-ALT-IXX |
| `FA18-MASS-P23-IYY` | Pitch moment of inertia, Phase 2/3 | **176,809** | slug-ft^2 | HARV-P2-TV, HARV-P3-ANSER | NASA-TM-4772 | Mass-properties table | SEARCH_EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | CANDIDATE_AFTER_PAGE_VERIFICATION | FA18-MASS-ALT-IYY |
| `FA18-MASS-P23-IZZ` | Yaw moment of inertia, Phase 2/3 | **191,744** | slug-ft^2 | HARV-P2-TV, HARV-P3-ANSER | NASA-TM-4772 | Mass-properties table | SEARCH_EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | CANDIDATE_AFTER_PAGE_VERIFICATION | FA18-MASS-ALT-IZZ |
| `FA18-MASS-FUEL-INT` | Internal fuel mass for tabulated loading | **6,480** | lbm | HARV-P1-BASIC, HARV-P2-TV, HARV-P3-ANSER | NASA-TM-4772 | Mass-properties table or text | SEARCH_EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | CANDIDATE_AFTER_PAGE_VERIFICATION | — |
| `FA18-MASS-ALT-IXX` | Roll moment of inertia (second extract set) | **22,632** | slug-ft^2 | HARV-P2-TV | UNRESOLVED: NASA-TP-3531 / NASA-TM-110216 / NASA-TP-3446 | NOT_CAPTURED | SEARCH_EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | NOT_USABLE_UNTIL_ATTRIBUTED | FA18-MASS-P23-IXX |
| `FA18-MASS-ALT-IYY` | Pitch moment of inertia (second extract set) | **174,246.3** | slug-ft^2 | HARV-P2-TV | UNRESOLVED: NASA-TP-3531 / NASA-TM-110216 / NASA-TP-3446 | NOT_CAPTURED | SEARCH_EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | NOT_USABLE_UNTIL_ATTRIBUTED | FA18-MASS-P23-IYY |
| `FA18-MASS-ALT-IZZ` | Yaw moment of inertia (second extract set) | **189,336.4** | slug-ft^2 | HARV-P2-TV | UNRESOLVED: NASA-TP-3531 / NASA-TM-110216 / NASA-TP-3446 | NOT_CAPTURED | SEARCH_EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | NOT_USABLE_UNTIL_ATTRIBUTED | FA18-MASS-P23-IZZ |
| `FA18-MASS-ALT-IXZ` | Product of inertia (second extract set) | **-2,131.8** | slug-ft^2 | HARV-P2-TV | UNRESOLVED: NASA-TP-3531 / NASA-TM-110216 / NASA-TP-3446 | NOT_CAPTURED | SEARCH_EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | NOT_USABLE_UNTIL_ATTRIBUTED | — |
| `FA18-MASS-DELTA-MOD` | Weight increase of HARV modifications (modified minus unmodified) | **4,119** | lb | HARV-P2-TV, HARV-P3-ANSER | NASA-WEB-HARV-VDD | VDD subsection 3.2.1 | SEARCH_EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | CONSISTENCY_CHECK | — |
| `FA18-MASS-DELTA-TV` | Weight of TV vane system installation (approximate) | **2,200** | lb | HARV-P2-TV, HARV-P3-ANSER | NASA-WEB-HARV-VDD | VDD subsection 3.2.1 | SEARCH_EXTRACT_APPROXIMATE | BLOCKED_PENDING_PAGE_VERIFICATION | CONSISTENCY_CHECK | — |
| `FA18-MASS-DELTA-CHUTE` | Spin chute, emergency systems and ballast (approximate) | **1,500** | lb | HARV-P2-TV, HARV-P3-ANSER | NASA-WEB-HARV-VDD | VDD subsection 3.2.1 | SEARCH_EXTRACT_APPROXIMATE | BLOCKED_PENDING_PAGE_VERIFICATION | CONSISTENCY_CHECK | — |

### Actuators

| Value ID | Quantity | Value | Units | Config | Source | Location | Exactness | Impl. use | Valid. use | Conflicts |
|---|---|---|---|---|---|---|---|---|---|---|
| `FA18-ACT-STAB-LIM` | Stabilator position limits | **24 up / 10.5 down** | deg | SIM-F18HARV | UNRESOLVED: NASA-TM-110216 / NASA-CR-198248 | NOT_CAPTURED | SEARCH_EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | CANDIDATE_AFTER_PAGE_VERIFICATION | — |
| `FA18-ACT-STAB-RATE` | Stabilator rate limit | **40** | deg/s | SIM-F18HARV | UNRESOLVED: NASA-TM-110216 / NASA-CR-198248 | NOT_CAPTURED | SEARCH_EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | CANDIDATE_AFTER_PAGE_VERIFICATION | — |
| `FA18-ACT-AIL-LIM` | Aileron position limits | **25 up / 45 down** | deg | SIM-F18HARV | UNRESOLVED: NASA-TM-110216 / NASA-CR-198248 | NOT_CAPTURED | SEARCH_EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | CANDIDATE_AFTER_PAGE_VERIFICATION | — |
| `FA18-ACT-AIL-RATE` | Aileron rate limit | **100** | deg/s | SIM-F18HARV | UNRESOLVED: NASA-TM-110216 / NASA-CR-198248 | NOT_CAPTURED | SEARCH_EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | CANDIDATE_AFTER_PAGE_VERIFICATION | — |
| `FA18-ACT-RUD-LIM` | Rudder position limits | **30 left / 30 right** | deg | SIM-F18HARV | UNRESOLVED: NASA-TM-110216 / NASA-CR-198248 / NASA-TP-1998-208465 / NASA-TM-110217 | NOT_CAPTURED | SEARCH_EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | CANDIDATE_AFTER_PAGE_VERIFICATION | — |
| `FA18-ACT-RUD-RATE` | Rudder rate limit | **82** | deg/s | SIM-F18HARV | UNRESOLVED: NASA-TM-110216 / NASA-CR-198248 | NOT_CAPTURED | SEARCH_EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | CANDIDATE_AFTER_PAGE_VERIFICATION | FA18-ACT-AAW-RUD-RATE |
| `FA18-ACT-LEF-LIM` | Leading-edge flap position limits | **3 up / 33 down** | deg | SIM-F18HARV | UNRESOLVED: NASA-CR-198248 / NASA-TP-1998-208465 / NASA-TM-110217 | NOT_CAPTURED | SEARCH_EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | CANDIDATE_AFTER_PAGE_VERIFICATION | — |
| `FA18-ACT-TEF-LIM` | Trailing-edge flap position limits | **8 up / 45 down** | deg | SIM-F18HARV | UNRESOLVED: NASA-CR-198248 / NASA-TP-1998-208465 / NASA-TM-110217 | NOT_CAPTURED | SEARCH_EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | CANDIDATE_AFTER_PAGE_VERIFICATION | — |
| `FA18-ACT-AAW-STAB-RATE` | Stabilator rate limit (AAW) | **40** | deg/s | AAW-853 | NASA-TM-2005-213664 | Actuator characteristics table (AAW document family; exact report not resolved) | SEARCH_EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | CANDIDATE_AFTER_PAGE_VERIFICATION | — |
| `FA18-ACT-AAW-RUD-RATE` | Rudder rate limit (AAW) | **56** | deg/s | AAW-853 | NASA-TM-2005-213664 | Actuator characteristics table | SEARCH_EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | CANDIDATE_AFTER_PAGE_VERIFICATION | FA18-ACT-RUD-RATE |
| `FA18-ACT-AAW-TEF-RATE` | Trailing-edge flap rate limit (AAW) | **18** | deg/s | AAW-853 | NASA-TM-2005-213664 | Actuator characteristics table | SEARCH_EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | CANDIDATE_AFTER_PAGE_VERIFICATION | — |
| `FA18-ACT-AAW-AIL-RATE` | Aileron rate limit (AAW) | **100** | deg/s | AAW-853 | NASA-TM-2005-213664 | Actuator characteristics table | SEARCH_EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | CANDIDATE_AFTER_PAGE_VERIFICATION | — |
| `FA18-ACT-AAW-LEF-RATE` | Leading-edge flap rate limit (AAW, inboard LEF) | **15** | deg/s | AAW-853 | NASA-TM-2005-213664 | Actuator characteristics table | SEARCH_EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | CANDIDATE_AFTER_PAGE_VERIFICATION | — |

### Database and envelope domains

| Value ID | Quantity | Value | Units | Config | Source | Location | Exactness | Impl. use | Valid. use | Conflicts |
|---|---|---|---|---|---|---|---|---|---|---|
| `FA18-DB-ALPHA` | Aerodynamic database angle-of-attack domain | **-10 .. +90** | deg | SIM-F18BAS, SIM-F18HARV | NASA-TM-110216 | Abstract | ABSTRACT_STATEMENT | DOMAIN_METADATA_ONLY | DOMAIN_GATE | — |
| `FA18-DB-BETA` | Aerodynamic database sideslip domain | **-20 .. +20** | deg | SIM-F18BAS, SIM-F18HARV | NASA-TM-110216 | Abstract | ABSTRACT_STATEMENT | DOMAIN_METADATA_ONLY | DOMAIN_GATE | — |
| `FA18-DB-MACH` | Aerodynamic database Mach domain | **0.0 .. 2.0** | - | SIM-F18HARV | NASA-TM-110216 | Abstract (per search extract) | SEARCH_EXTRACT | DOMAIN_METADATA_ONLY | DOMAIN_GATE | — |
| `FA18-RFCS-ENV` | Research flight control system engagement envelope | **M 0.2-0.7; 15,000-35,000 ft initial (later 45,000 ft)** | - | HARV-P2-TV, HARV-P3-ANSER | NASA-WEB-HARV-VDD | VDD section 3.3 | SEARCH_EXTRACT | DOMAIN_METADATA_ONLY | DOMAIN_GATE | — |

### Propulsion

| Value ID | Quantity | Value | Units | Config | Source | Location | Exactness | Impl. use | Valid. use | Conflicts |
|---|---|---|---|---|---|---|---|---|---|---|
| `FA18-ENG-THRUST-CLASS` | Afterburning thrust class per engine (fact sheet) | **16,000** | lbf | F404-GE-400 | NASA-FS-002-DFRC | NOT_CAPTURED | PUBLIC_FACT_SHEET | PROHIBITED | IDENTITY_ONLY | — |
| `FA18-ENG-PLA-IDLE` | Power lever angle at idle | **35** | deg | F404-GE-400 | UNRESOLVED: NASA-TM-4240 / NASA-TP-3001 / NASA-TM-4140 | NOT_CAPTURED | SEARCH_EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | CANDIDATE_AFTER_PAGE_VERIFICATION | — |
| `FA18-ENG-PLA-INT` | Power lever angle at intermediate (max non-afterburning) | **102** | deg | F404-GE-400 | UNRESOLVED: NASA-TM-4240 / NASA-TP-3001 / NASA-TM-4140 | NOT_CAPTURED | SEARCH_EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | CANDIDATE_AFTER_PAGE_VERIFICATION | — |
| `FA18-ENG-MODEL-ACC-SS` | Simple engine model steady-state accuracy vs GE complete model | **within 3** | % | F404-GE-400, HARV-P2-TV | NASA-TM-4240 | Abstract | ABSTRACT_STATEMENT | NOT_A_MODEL_PARAMETER | ACCEPTANCE_TOLERANCE_REFERENCE | — |
| `FA18-ENG-MODEL-ACC-TR` | Simple engine model transient accuracy vs GE complete model | **within 25** | % | F404-GE-400, HARV-P2-TV | NASA-TM-4240 | Abstract | ABSTRACT_STATEMENT | NOT_A_MODEL_PARAMETER | ACCEPTANCE_TOLERANCE_REFERENCE | — |
| `FA18-ENG-INFLIGHT-ACC` | Uninstalled gross thrust accuracy of in-flight thrust methods | **1 .. 4** | % | F404-GE-400 | NASA-TP-3001 | Abstract | ABSTRACT_STATEMENT | NOT_A_MODEL_PARAMETER | UNCERTAINTY_REFERENCE | — |

### Flight-test domains

| Value ID | Quantity | Value | Units | Config | Source | Location | Exactness | Impl. use | Valid. use | Conflicts |
|---|---|---|---|---|---|---|---|---|---|---|
| `FA18-PID-ALPHA-ILIFF-BASIC` | Alpha range of flight-derived lat-dir derivatives, basic F-18 | **3 .. 47** | deg | HARV-P1-BASIC | NASA-TM-4786 | Abstract | ABSTRACT_STATEMENT | DOMAIN_METADATA_ONLY | DOMAIN_GATE | — |
| `FA18-PID-ALPHA-NAPOLITANO` | Alpha range of flight-derived derivatives (WVU pEst) | **10 .. 60** | deg | HARV-P2-TV | NASA-CR-194838 | Abstract | ABSTRACT_STATEMENT | DOMAIN_METADATA_ONLY | DOMAIN_GATE | — |
| `FA18-DEP-TEST-COND` | Departure test entry conditions (inlet compatibility study) | **M 0.3-0.4; 35,000 ft; entry yaw rate 40-90 deg/s** | - | HARV-P1-BASIC | NTRS-19990024943 | Abstract | SEARCH_EXTRACT | DOMAIN_METADATA_ONLY | SCENARIO_REFERENCE | — |

## 3. Reading the candidates

- **Geometry** (S, c̄, b) is the same trio repeated throughout the F/A-18 literature. Page-verify it once, in `NASA-TM-4772` or `NASA-TM-110216`, and record the printed precision of b (37.4 vs 37.42 ft). Moment-reference-centre location for the aero data is **not captured**, and is required before any moment coefficient is used.
- **Mass properties** are per phase. Phase 1 has lower pitch and yaw inertia (Iyy 124,554 vs 176,809 slug-ft²) because the ~2,200 lb vane system and the ballast sit far from the CG. That is a physical explanation, not a verified breakdown. **No verified Ixz exists yet.** The only candidate (−2,131.8 slug-ft²) has an unresolved source and unknown sign convention.
- **Actuator limits and rates** exist for the HARV simulation (stabilator, aileron, rudder, LEF, TEF positions; stabilator, aileron and rudder rates). HARV LEF/TEF **rates** and all actuator **bandwidths/time constants** are not captured. AAW rates belong to a different airframe and must not fill HARV gaps.
- **Propulsion**: no thrust-deck point was captured. The 16,000 lbf figure is a fact-sheet class value (prohibited for code). PLA idle/intermediate angles (35°/102°) need their source resolved.
- **Domains**: database domain (α −10..+90°, β ±20°, M 0..2) is metadata only. The validated flight envelope for research-law comparisons is much narrower (RFCS: M 0.2–0.7, 15–35 kft, later 45 kft).

## 4. Public-recoverability matrix

Answers FA-18 question 4 at the level r0 can support. "Public" means a public document that probably or certainly contains the item. It does not mean the value has been recovered.

| Item | Public? | Best source(s) | r0 status |
|---|---|---|---|
| S, c̄, b | **Yes** | NASA-TM-4772; NASA-TM-110216 (likely) | Candidate captured (L3) |
| Mass (per phase) | **Yes** | NASA-TM-4772 | Candidate captured (L3) |
| CG (%MAC, FS) | **Yes** | NASA-TM-4772 | Candidate captured (L3); MAC LE station and FS datum not captured |
| Ixx, Iyy, Izz | **Yes** | NASA-TM-4772 | Candidate captured (L3); conflicting second set |
| Ixz | **Probably** | NASA-TM-110216 / NASA-TM-4772 (likely) | Only an unattributed candidate |
| Aero moment reference point | **Probably** | NASA-TM-110216 / NASA-TM-107601 | Not captured |
| Surface geometry (areas, hinge lines, mixing) | **Unknown** | NASA-TM-107601 / NASA-TM-110216 | Not captured |
| Actuator position limits | **Yes** (HARV sim) | NASA-TM-110216 / NASA-CR-198248 / NASA-TM-110217 | Candidate captured, source unresolved |
| Actuator rate limits | **Partly** | Same; AAW for 853 | Stab/ail/rudder only; LEF/TEF rates missing for HARV |
| Actuator dynamics (order, bandwidth) | **Partly** | NASA-TM-107601 (first-order + limits, abstract) | Time constants not captured |
| Full nonlinear aero database (tables) | **UNKNOWN: top blocker** | NASA-TM-107601, NASA-TM-110216 | Whether tables are printed in the public TMs is unverified |
| Aero derivatives vs α (flight) | **Yes** (plots) | NASA-TM-4786, NASA-TP-97-206539, NASA-TP-1999-206573, NASA-CR-194838 | Requires digitization |
| Global polynomial aero model | **Possibly** | JAIRCRAFT-1995-MORELLI-MOF (paywalled), NTRS-19940020628 (public) | Whether coefficients are printed is unverified |
| Production FCS gains/schedules | **No** | NATOPS is Distribution C; manufacturer docs not located | Unavailable |
| Research FCS gains/schedules | **Yes** | NASA-TM-110217 (code-level spec); NASA-TP-3446; NASA-TP-1998-208465 | Not transcribed |
| TV mixer / vane allocation | **Yes** | NASA-TM-110228 | Not transcribed |
| Engine dynamics structure | **Yes** | NASA-TM-4240 | Structure known from abstract |
| Installed thrust deck (F404 in F/A-18) | **Unknown** | NASA-TM-110216 / NASA-TM-107601 (engine model), NASA-TM-4240 | Manufacturer model is not public |
| Flight time histories | **Partly** | NASA-WEB-HARV-VDD (parameter lists), NTRS-19970014822 (retrieval procedures), report figures | Downloadable data not confirmed |
