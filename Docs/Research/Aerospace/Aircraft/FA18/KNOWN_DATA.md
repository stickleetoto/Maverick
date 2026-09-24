# F/A-18 Known Data (r0, R1 corrections)

Canonical data: [`KNOWN_DATA.json`](KNOWN_DATA.json). Tables between GENERATED markers are rendered by `Tools/build_aerospace_source_views.py` (schema `known-data.v2`). All values also appear in [`../../AIRCRAFT_NUMERIC_FIELD_INDEX.json`](../../AIRCRAFT_NUMERIC_FIELD_INDEX.json).

> r0 level names used in the prose below map to R1 names as L0 = `SEARCH_LEAD_ONLY`, L1 = `CATALOGUE_VERIFIED`, L2 = `ABSTRACT_VERIFIED`, L3 = `CONTENT_EXTRACT_VERIFIED`, L4/L5 = `PAGE_VERIFIED`.

> **No value on this page may be used in runtime code.** r0 values were captured from web-search extracts (library level `CONTENT_EXTRACT_VERIFIED` at best). R1 added values that the repository's F/A-18 consolidation branch (`sol/fa18-source-consolidation-r1` @e596c32) transcribed from retrieved NASA PDFs. Those carry `exactness = REPOSITORY_READING`, library level `SEARCH_LEAD_ONLY`, and the repository's locator and PDF hash in `existing_maverick_analysis`. None is `ALLOWED`.

---

## R1 corrections to r0 (summary)

| r0 statement | R1 finding (repository page readings) | Effect |
|---|---|---|
| `FA18-ACT-AIL-LIM` "25 up / 45 down" attributed to HARV sources | Matches the **f18bas** table (TM-107601 Table 8.7: -25/+45). f18harv prints -25/+42; TP-97-206539 Table 1 prints 24 up / 45 down | Attribution withdrawn; TM-107601 added as candidate; three lineage tables recorded separately |
| `FA18-ACT-RUD-RATE` 82 deg/s vs AAW 56 deg/s (conflict C1) | 82 deg/s is printed in f18harv Tables 6.1/6.2 and TP-97-206539 Table 1; f18bas uses 61 deg/s; 56 deg/s is also MDC A7813's **no-load** rudder rate reproduced in TM-107601 Table 8.6 | C1 re-described; not resolved |
| Phase 1 geometry and mass attributed to `NASA-TM-4772` | The same numbers are printed in `NASA-TP-97-206539` Table 3 (unmodified Phase I column), which also prints **Ixz = -2,039 slug-ft^2** | TM-4772 attribution left open; new Ixz value recorded |
| "No verified Ixz exists" | Ixz -2,039 (Phase 1, TP-97-206539) and -2,430 (f18bas Fighter Escort 60 % fuel) per repository | Recorded, not released |
| Q-A1: are the aero tables printed? | **No.** f18bas and f18harv describe but do not print the aero lookup arrays; f18harv's 29 engine arrays are not printed either | Top blocker confirmed |
| Q-C1: is a baseline FCS printed? | **Yes, simplified.** f18bas Section 9 prints an OFP 8.3.3 inner-loop CAS | Research-grade baseline law available |
| Engine dynamics | TM-4240 and TM-110216 disagree on PLA rate limits (19.03/26.81 vs 14/22 deg/s) with identical time constants | Recorded as a model-version conflict; never averaged |

## 1. Configuration registry

Every source and every value is tagged with one or more of these IDs. "F/A-18" alone is never a configuration.

<!-- BEGIN GENERATED: configurations (Tools/build_aerospace_source_views.py; do not edit by hand) -->
| ID | Label | Airframe / serial | Engine | FCS | Research hardware | Dates | Scope | Lineage | Status |
|---|---|---|---|---|---|---|---|---|---|
| `FA18-CFG-PROD-AB` | Production F/A-18A/B (U.S. Navy) | Production family; block/lot not specified | F404-GE-400 x2 | Production digital FBW (PROM versions V8.x/V10.x family); production gains NOT public | — | NOT_RECORDED | PROD_FAMILY | Production | REFERENCE_ONLY |
| `FA18-CFG-HARV-P1-BASIC` | NASA HARV Phase 1 — basic F-18 hardware/software configuration | F-18 full-scale development aircraft #6 (pre-production; built before the F/A-18 redesignation); **NASA 840, BuNo 160780** | F404-GE-400 x2 | Basic F-18 FCS (V8.3.3 per VDD; upgrade to V10.1 timing unverified) | Spin chute (why the Navy loaned this airframe), research instrumentation; no TV vanes | From April 1987 (phase end date unverified) | EXACT | NASA 840 HARV | CANDIDATE_VALIDATION_CONFIG |
| `FA18-CFG-HARV-P2-TV` | NASA HARV Phase 2 — multi-axis thrust vectoring + RFCS | Same airframe as P1; **NASA 840, BuNo 160780** | F404-GE-400 x2 with 3 external TV vanes per engine | Basic F/A-18 FCS (V10.1 family) + RFCS (Pace 1750A, Ada) running NASA research laws (e.g., NASA-1A) | TV vane system (~2,200 lb); spin-chute / emergency-system / ballast changes (~1,500 lb) | Unverified (between Phase 1 and July 1995) | RESEARCH_MOD | NASA 840 HARV | CANDIDATE_VALIDATION_CONFIG |
| `FA18-CFG-HARV-P3-ANSER` | NASA HARV Phase 3 — TV + Actuated Nose Strakes for Enhanced Rolling (ANSER) | Same airframe with modified forebody; **NASA 840, BuNo 160780** | F404-GE-400 x2 with TV vanes | RFCS running the ANSER research control law (NASA-TM-110217; v152.0 validated in NASA-CR-198250) | TV vanes + conformal actuated forebody strakes | ANSER flight data July 1995 - May 1996; program ended September 1996 | RESEARCH_MOD | NASA 840 HARV | CANDIDATE_RESEARCH_TARGET |
| `FA18-CFG-SIM-F18BAS` | NASA Langley 'f18bas' simulation (NASA-TM-107601) | Simulation of the basic F/A-18 | F404 engine model (details unverified) | Basic F/A-18 control-system model (version unverified) | Preliminary HARV TV system model | 1992 | SIM_ONLY | NASA Langley F/A-18 simulation lineage | SIMULATION_REFERENCE |
| `FA18-CFG-SIM-F18HARV` | NASA Langley 'f18harv' simulation (NASA-TM-110216) | Simulation of the HARV; **Models NASA 840** | F404 engine model with TV effects (details unverified) | Host for NASA-1A / lateral-directional / ANSER research laws | TV vanes + actuated forebody strakes | 1996 | SIM_ONLY | NASA Langley F/A-18 simulation lineage | SIMULATION_REFERENCE |
| `FA18-CFG-SRA-845` | NASA F/A-18 Systems Research Aircraft | Pre-production two-seat F/A-18B; **NASA 845** | F404 (variant unverified) | Production-family FCS + systems experiments | EPAD EHA/EMA on left aileron; fly-by-light; power-by-wire | First research flight 21 May 1993 | EXACT | NASA 845 SRA | NOT_AN_AERO_SOURCE |
| `FA18-CFG-AAW-853` | NASA F/A-18A Active Aeroelastic Wing (X-53) | Navy F/A-18A airframe 853 with wings taken from HARV 840, wing-box skins thinned; **NASA 853** | F404 (variant unverified) | AAW research control laws (NASA/TM-2005-213666) | Modified flexible wing; research instrumentation | Program from 1996; flight phases early 2003 and early 2005 | RESEARCH_MOD | NASA 853 | SEPARATE_CONFIGURATION |
| `FA18-CFG-FAST-853` | NASA Full-scale Advanced Systems Testbed (FAST) F/A-18 | F/A-18 (tail number per program pages 853; unverified); **NASA 853 (unverified)** | F404 (variant unverified) | Nonlinear dynamic inversion baseline research law (2011) | Research flight computers | c. 2011 | RESEARCH_MOD | NASA 853 | SEPARATE_CONFIGURATION |
| `FA18-CFG-UMN-DERIVED` | University of Minnesota falling-leaf analysis model | Derived simulation | Not the focus | Reconstructed baseline and revised F/A-18 control laws | — | 2011 | SIM_ONLY | Academic | CROSS_VALIDATION_ONLY |
| `FA18-CFG-EF` | F/A-18E/F Super Hornet | Different aircraft (larger airframe, F414 engines) | F414-GE-400 | Different FCS | — | NOT_RECORDED | PROD_FAMILY | Super Hornet (out of scope) | OUT_OF_SCOPE |
| `ENG-F404-GE-400` | General Electric F404-GE-400 (engine identity) | Installations: F/A-18A/B (twin), HARV (twin with TV vanes), X-29A (single) | F404-GE-400 | n/a (engine identity) | — | NOT_RECORDED | PROD_FAMILY | F404 | ENGINE_IDENTITY |
| `FA18-CFG-WT-CR3608` | 1/10-scale F-18 rotary-balance model (Langley Spin Tunnel; NASA CR-3608) | F-18 1/10-scale tunnel model | n/a | n/a | none | 1984 report | TUNNEL | NASA Langley tunnel | CROSS_VALIDATION_ONLY |
<!-- END GENERATED: configurations -->

### Configuration traps (r0)

1. **One serial, three configurations.** NASA 840 changed mass (+4,119 lb), hardware (TV vanes plus spin-chute/emergency-system/ballast changes, later forebody strakes) and control laws (basic FCS, then RFCS research laws, then ANSER) across Phases 1–3. Mass properties in `NASA-TM-4772` are split by phase for this reason.
2. **One serial's parts on another serial.** HARV 840's wings, with thinned skins, flew on NASA 853 as the AAW/X-53.
3. **"Basic F-18" in NASA documents means the HARV airframe in its basic hardware/software state.** That is a pre-production FSD aircraft with research instrumentation and a spin chute. It is not a production F/A-18A lot.
4. **The f18bas simulation already contains a preliminary TV model.** "Baseline sim" does not automatically mean "no TV".
5. **F404-GE-400 data from X-29A tests** is engine-level only. The installation is incompatible.

## 2. Candidate values

Legend: `SEARCH_EXTRACT` = seen in a search-index extract of the named document, page not confirmed. `REPOSITORY_READING` = transcribed by the repository from a retrieved PDF (see `existing_maverick_analysis`). `UNRESOLVED` source = the extract did not say which candidate document the value came from.

<!-- BEGIN GENERATED: values (Tools/build_aerospace_source_views.py; do not edit by hand) -->
### Geometry and reference quantities

| Value ID | Quantity | Value | Units | Precision | Config | Source | Location as recorded | Exactness | Level | Impl. use | Repo x-ref | Conflicts |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `FA18-GEO-S` | Reference wing area | **400** | ft^2 | as extracted (printed precision not con… | HARV-P1-BASIC, HARV-P2-TV, HARV-P3-ANSER | NASA-TM-4772 | Reference-geometry table (number/page not captured) | SEARCH_EXTRACT | EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `FA18-GEO-CBAR` | Reference mean aerodynamic chord | **11.52** | ft | as extracted (printed precision not con… | HARV-P1-BASIC, HARV-P2-TV, HARV-P3-ANSER | NASA-TM-4772 | Reference-geometry table (number/page not captured) | SEARCH_EXTRACT | EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `FA18-GEO-B` | Reference span | **37.4** | ft | as extracted (printed precision not con… | HARV-P1-BASIC, HARV-P2-TV, HARV-P3-ANSER | NASA-TM-4772 | Reference-geometry table (number/page not captured) | SEARCH_EXTRACT | EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `FA18-GEO-SPAN-PHYS` | Physical wing span (public fact page) | **37 ft 5 in** | ft-in | as extracted (printed precision not con… | HARV-P1-BASIC, HARV-P2-TV, HARV-P3-ANSER | NASA-FS-002-DFRC | NOT_CAPTURED | PUBLIC_FACT_SHEET | EXTRACT | PROHIBITED | — | — |
| `FA18-BAS-GEO-S` | Reference wing area (f18bas) | **400** | ft^2 | 1 ft^2 | SIM-F18BAS | NASA-TM-107601 | Table 3.4, PDF p.26 / printed p.20 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `FA18-BAS-GEO-CBAR` | Reference MAC (f18bas) | **11.523** | ft | 0.001 ft (138.275 in) | SIM-F18BAS | NASA-TM-107601 | Table 3.4, PDF p.26 / printed p.20 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `FA18-BAS-GEO-B` | Reference span (f18bas) | **37.42** | ft | 0.01 ft | SIM-F18BAS | NASA-TM-107601 | Table 3.4, PDF p.26 / printed p.20 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | FA18-GEO-B |

### Mass properties

| Value ID | Quantity | Value | Units | Precision | Config | Source | Location as recorded | Exactness | Level | Impl. use | Repo x-ref | Conflicts |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `FA18-MASS-P1-W` | Gross weight, Phase 1 (basic) loading | **31,980** | lbm | as extracted (printed precision not con… | HARV-P1-BASIC | NASA-TM-4772 | Mass-properties table (number/page not captured) | SEARCH_EXTRACT | EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `FA18-MASS-P1-CG` | Longitudinal CG, Phase 1 | **21.9** | % MAC | as extracted (printed precision not con… | HARV-P1-BASIC | NASA-TM-4772 | Mass-properties table | SEARCH_EXTRACT | EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `FA18-MASS-P1-FS` | CG fuselage station, Phase 1 | **454.33** | in (fuselage station) | as extracted (printed precision not con… | HARV-P1-BASIC | NASA-TM-4772 | Mass-properties table | SEARCH_EXTRACT | EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `FA18-MASS-P1-IXX` | Roll moment of inertia, Phase 1 | **22,040** | slug-ft^2 | as extracted (printed precision not con… | HARV-P1-BASIC | NASA-TM-4772 | Mass-properties table | SEARCH_EXTRACT | EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `FA18-MASS-P1-IYY` | Pitch moment of inertia, Phase 1 | **124,554** | slug-ft^2 | as extracted (printed precision not con… | HARV-P1-BASIC | NASA-TM-4772 | Mass-properties table | SEARCH_EXTRACT | EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `FA18-MASS-P1-IZZ` | Yaw moment of inertia, Phase 1 | **139,382** | slug-ft^2 | as extracted (printed precision not con… | HARV-P1-BASIC | NASA-TM-4772 | Mass-properties table | SEARCH_EXTRACT | EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `FA18-MASS-P23-W` | Gross weight, Phase 2/3 (TV / ANSER) loading | **36,099** | lbm | as extracted (printed precision not con… | HARV-P2-TV, HARV-P3-ANSER | NASA-TM-4772 | Mass-properties table (number/page not captured) | SEARCH_EXTRACT | EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | — | — |
| `FA18-MASS-P23-CG` | Longitudinal CG, Phase 2/3 | **23.8** | % MAC | as extracted (printed precision not con… | HARV-P2-TV, HARV-P3-ANSER | NASA-TM-4772 | Mass-properties table | SEARCH_EXTRACT | EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | — | — |
| `FA18-MASS-P23-FS` | CG fuselage station, Phase 2/3 | **456.88** | in (fuselage station) | as extracted (printed precision not con… | HARV-P2-TV, HARV-P3-ANSER | NASA-TM-4772 | Mass-properties table | SEARCH_EXTRACT | EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | — | — |
| `FA18-MASS-P23-IXX` | Roll moment of inertia, Phase 2/3 | **22,789** | slug-ft^2 | as extracted (printed precision not con… | HARV-P2-TV, HARV-P3-ANSER | NASA-TM-4772 | Mass-properties table | SEARCH_EXTRACT | EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | — | FA18-MASS-ALT-IXX |
| `FA18-MASS-P23-IYY` | Pitch moment of inertia, Phase 2/3 | **176,809** | slug-ft^2 | as extracted (printed precision not con… | HARV-P2-TV, HARV-P3-ANSER | NASA-TM-4772 | Mass-properties table | SEARCH_EXTRACT | EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | — | FA18-MASS-ALT-IYY |
| `FA18-MASS-P23-IZZ` | Yaw moment of inertia, Phase 2/3 | **191,744** | slug-ft^2 | as extracted (printed precision not con… | HARV-P2-TV, HARV-P3-ANSER | NASA-TM-4772 | Mass-properties table | SEARCH_EXTRACT | EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | — | FA18-MASS-ALT-IZZ |
| `FA18-MASS-FUEL-INT` | Internal fuel mass for tabulated loading | **6,480** | lbm | as extracted (printed precision not con… | HARV-P1-BASIC, HARV-P2-TV, HARV-P3-ANSER | NASA-TM-4772 | Mass-properties table or text | SEARCH_EXTRACT | EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `FA18-MASS-ALT-IXX` | Roll moment of inertia (second extract set) | **22,632** | slug-ft^2 | as extracted (printed precision not con… | HARV-P2-TV | UNRESOLVED: NASA-TP-3531, NASA-TM-110216, NASA-TP-3446 | NOT_CAPTURED | SEARCH_EXTRACT | EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | — | FA18-MASS-P23-IXX |
| `FA18-MASS-ALT-IYY` | Pitch moment of inertia (second extract set) | **174246.3** | slug-ft^2 | as extracted (printed precision not con… | HARV-P2-TV | UNRESOLVED: NASA-TP-3531, NASA-TM-110216, NASA-TP-3446 | NOT_CAPTURED | SEARCH_EXTRACT | EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | — | FA18-MASS-P23-IYY |
| `FA18-MASS-ALT-IZZ` | Yaw moment of inertia (second extract set) | **189336.4** | slug-ft^2 | as extracted (printed precision not con… | HARV-P2-TV | UNRESOLVED: NASA-TP-3531, NASA-TM-110216, NASA-TP-3446 | NOT_CAPTURED | SEARCH_EXTRACT | EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | — | FA18-MASS-P23-IZZ |
| `FA18-MASS-ALT-IXZ` | Product of inertia (second extract set) | **-2131.8** | slug-ft^2 | as extracted (printed precision not con… | HARV-P2-TV | UNRESOLVED: NASA-TP-3531, NASA-TM-110216, NASA-TP-3446 | NOT_CAPTURED | SEARCH_EXTRACT | EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | — | — |
| `FA18-MASS-DELTA-MOD` | Weight increase of HARV modifications (modified minus unmodified) | **4,119** | lb | as extracted (printed precision not con… | HARV-P2-TV, HARV-P3-ANSER | NASA-WEB-HARV-VDD | VDD subsection 3.2.1 | SEARCH_EXTRACT | EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | — | — |
| `FA18-MASS-DELTA-TV` | Weight of TV vane system installation (approximate) | **2,200** | lb | as extracted (printed precision not con… | HARV-P2-TV, HARV-P3-ANSER | NASA-WEB-HARV-VDD | VDD subsection 3.2.1 | SEARCH_EXTRACT_APPROXIMATE | EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | — | — |
| `FA18-MASS-DELTA-CHUTE` | Spin chute, emergency systems and ballast (approximate) | **1,500** | lb | as extracted (printed precision not con… | HARV-P2-TV, HARV-P3-ANSER | NASA-WEB-HARV-VDD | VDD subsection 3.2.1 | SEARCH_EXTRACT_APPROXIMATE | EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | — | — |
| `FA18-P1-IXZ` | Product of inertia, Phase 1 (unmodified) column | **-2,039** | slug-ft^2 | 1 slug-ft^2 | HARV-P1-BASIC | NASA-TP-97-206539 | Table 3, PDF p.16 / printed p.10 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `FA18-P1-WL` | CG waterline, Phase 1 column | **105.24** | in (waterline) | 0.01 in | HARV-P1-BASIC | NASA-TP-97-206539 | Table 3 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `FA18-BAS-MASS-W` | Weight, f18bas Fighter Escort 60% internal fuel | **31,665** | lbf (weight) | as printed | SIM-F18BAS | NASA-TM-107601 | Table 3.5, PDF p.26 / printed p.20 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `FA18-BAS-MASS-FS` | CG fuselage station, f18bas Fighter Escort 60% internal fuel | **457.3** | in (FS) | as printed | SIM-F18BAS | NASA-TM-107601 | Table 3.5, PDF p.26 / printed p.20 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `FA18-BAS-MASS-WL` | CG waterline, f18bas Fighter Escort 60% internal fuel | **101.6** | in (WL) | as printed | SIM-F18BAS | NASA-TM-107601 | Table 3.5, PDF p.26 / printed p.20 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `FA18-BAS-MASS-IXX` | Roll inertia, f18bas Fighter Escort 60% internal fuel | **22,337** | slug-ft^2 | as printed | SIM-F18BAS | NASA-TM-107601 | Table 3.5, PDF p.26 / printed p.20 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `FA18-BAS-MASS-IYY` | Pitch inertia, f18bas Fighter Escort 60% internal fuel | **120,293** | slug-ft^2 | as printed | SIM-F18BAS | NASA-TM-107601 | Table 3.5, PDF p.26 / printed p.20 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `FA18-BAS-MASS-IZZ` | Yaw inertia, f18bas Fighter Escort 60% internal fuel | **138,945** | slug-ft^2 | as printed | SIM-F18BAS | NASA-TM-107601 | Table 3.5, PDF p.26 / printed p.20 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `FA18-BAS-MASS-IXZ` | Product of inertia, f18bas Fighter Escort 60% internal fuel | **-2,430** | slug-ft^2 | as printed | SIM-F18BAS | NASA-TM-107601 | Table 3.5, PDF p.26 / printed p.20 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |

### Controls, actuators and FCS

| Value ID | Quantity | Value | Units | Precision | Config | Source | Location as recorded | Exactness | Level | Impl. use | Repo x-ref | Conflicts |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `FA18-ACT-STAB-LIM` | Stabilator position limits | **24 up / 10.5 down** | deg | as extracted (printed precision not con… | SIM-F18HARV | UNRESOLVED: NASA-TM-110216, NASA-CR-198248 | NOT_CAPTURED | SEARCH_EXTRACT | EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | — | — |
| `FA18-ACT-STAB-RATE` | Stabilator rate limit | **40** | deg/s | as extracted (printed precision not con… | SIM-F18HARV | UNRESOLVED: NASA-TM-110216, NASA-CR-198248 | NOT_CAPTURED | SEARCH_EXTRACT | EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | — | — |
| `FA18-ACT-AIL-LIM` | Aileron position limits | **25 up / 45 down** | deg | as extracted (printed precision not con… | SIM-F18HARV | UNRESOLVED: NASA-TM-107601, NASA-TM-110216, NASA-CR-198248 | NOT_CAPTURED | SEARCH_EXTRACT | EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | — | FA18-BAS-AIL-LIM, FA18-HARV-AIL-LIM, FA18-P2T1-AIL-LIM |
| `FA18-ACT-AIL-RATE` | Aileron rate limit | **100** | deg/s | as extracted (printed precision not con… | SIM-F18HARV | UNRESOLVED: NASA-TM-110216, NASA-CR-198248 | NOT_CAPTURED | SEARCH_EXTRACT | EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | — | — |
| `FA18-ACT-RUD-LIM` | Rudder position limits | **30 left / 30 right** | deg | as extracted (printed precision not con… | SIM-F18HARV | UNRESOLVED: NASA-TM-110216, NASA-CR-198248, NASA-TP-1998-208465, NASA-TM-110217 | NOT_CAPTURED | SEARCH_EXTRACT | EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | — | — |
| `FA18-ACT-RUD-RATE` | Rudder rate limit | **82** | deg/s | as extracted (printed precision not con… | SIM-F18HARV | UNRESOLVED: NASA-TM-110216, NASA-CR-198248 | NOT_CAPTURED | SEARCH_EXTRACT | EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | — | FA18-ACT-AAW-RUD-RATE, FA18-BAS-RUD-RATE, FA18-A7813-RUD-RATE |
| `FA18-ACT-LEF-LIM` | Leading-edge flap position limits | **3 up / 33 down** | deg | as extracted (printed precision not con… | SIM-F18HARV | UNRESOLVED: NASA-CR-198248, NASA-TP-1998-208465, NASA-TM-110217 | NOT_CAPTURED | SEARCH_EXTRACT | EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | — | — |
| `FA18-ACT-TEF-LIM` | Trailing-edge flap position limits | **8 up / 45 down** | deg | as extracted (printed precision not con… | SIM-F18HARV | UNRESOLVED: NASA-CR-198248, NASA-TP-1998-208465, NASA-TM-110217 | NOT_CAPTURED | SEARCH_EXTRACT | EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | — | — |
| `FA18-ACT-AAW-STAB-RATE` | Stabilator rate limit (AAW) | **40** | deg/s | as extracted (printed precision not con… | AAW-853 | NASA-TM-2005-213664 | Actuator characteristics table (AAW document family; exact report not… | SEARCH_EXTRACT | EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | — | — |
| `FA18-ACT-AAW-RUD-RATE` | Rudder rate limit (AAW) | **56** | deg/s | as extracted (printed precision not con… | AAW-853 | NASA-TM-2005-213664 | Actuator characteristics table | SEARCH_EXTRACT | EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | — | FA18-ACT-RUD-RATE |
| `FA18-ACT-AAW-TEF-RATE` | Trailing-edge flap rate limit (AAW) | **18** | deg/s | as extracted (printed precision not con… | AAW-853 | NASA-TM-2005-213664 | Actuator characteristics table | SEARCH_EXTRACT | EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | — | — |
| `FA18-ACT-AAW-AIL-RATE` | Aileron rate limit (AAW) | **100** | deg/s | as extracted (printed precision not con… | AAW-853 | NASA-TM-2005-213664 | Actuator characteristics table | SEARCH_EXTRACT | EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | — | — |
| `FA18-ACT-AAW-LEF-RATE` | Leading-edge flap rate limit (AAW, inboard LEF) | **15** | deg/s | as extracted (printed precision not con… | AAW-853 | NASA-TM-2005-213664 | Actuator characteristics table | SEARCH_EXTRACT | EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | — | — |
| `FA18-BAS-STAB-LIM` | Stabilator position limits (f18bas) | **-24 / +10.5** | deg | as printed | SIM-F18BAS | NASA-TM-107601 | Table 8.7, PDF p.87 / printed p.81 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `FA18-BAS-STAB-RATE` | Stabilator rate limit (f18bas) | **40** | deg/s | as printed | SIM-F18BAS | NASA-TM-107601 | Table 8.7, PDF p.87 / printed p.81 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `FA18-BAS-RUD-LIM` | Rudder position limits (f18bas) | **-30 / +30** | deg | as printed | SIM-F18BAS | NASA-TM-107601 | Table 8.7, PDF p.87 / printed p.81 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `FA18-BAS-RUD-RATE` | Rudder rate limit (f18bas) | **61** | deg/s | as printed | SIM-F18BAS | NASA-TM-107601 | Table 8.7, PDF p.87 / printed p.81 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | FA18-HARV-RUD-RATE, FA18-A7813-RUD-RATE |
| `FA18-BAS-AIL-LIM` | Aileron position limits (f18bas) | **-25 / +45** | deg | as printed | SIM-F18BAS | NASA-TM-107601 | Table 8.7, PDF p.87 / printed p.81 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | FA18-HARV-AIL-LIM, FA18-P2T1-AIL-LIM |
| `FA18-BAS-AIL-RATE` | Aileron rate limit (f18bas) | **100** | deg/s | as printed | SIM-F18BAS | NASA-TM-107601 | Table 8.7, PDF p.87 / printed p.81 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `FA18-BAS-TEF-LIM` | Trailing-edge flap position limits (f18bas) | **-8 / +45** | deg | as printed | SIM-F18BAS | NASA-TM-107601 | Table 8.7, PDF p.87 / printed p.81 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `FA18-BAS-TEF-RATE` | Trailing-edge flap rate limit (f18bas) | **18** | deg/s | as printed | SIM-F18BAS | NASA-TM-107601 | Table 8.7, PDF p.87 / printed p.81 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `FA18-BAS-LEF-LIM` | Leading-edge flap position limits (f18bas) | **-3 / +34** | deg | as printed | SIM-F18BAS | NASA-TM-107601 | Table 8.7, PDF p.87 / printed p.81 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | FA18-HARV-LEF-LIM |
| `FA18-BAS-LEF-RATE` | Leading-edge flap rate limit (f18bas) | **18** | deg/s | as printed | SIM-F18BAS | NASA-TM-107601 | Table 8.7, PDF p.87 / printed p.81 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | FA18-HARV-LEF-RATE |
| `FA18-HARV-STAB-LIM` | Stabilator position limits (f18harv) | **-24 / +10.5** | deg | as printed | SIM-F18HARV | NASA-TM-110216 | Tables 6.1/6.2, printed pp.90-93 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `FA18-HARV-STAB-RATE` | Stabilator rate limit (f18harv) | **40** | deg/s | as printed | SIM-F18HARV | NASA-TM-110216 | Tables 6.1/6.2, printed pp.90-93 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `FA18-HARV-RUD-LIM` | Rudder position limits (f18harv) | **-30 / +30** | deg | as printed | SIM-F18HARV | NASA-TM-110216 | Tables 6.1/6.2, printed pp.90-93 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `FA18-HARV-RUD-RATE` | Rudder rate limit (f18harv) | **82** | deg/s | as printed | SIM-F18HARV | NASA-TM-110216 | Tables 6.1/6.2, printed pp.90-93 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | FA18-BAS-RUD-RATE |
| `FA18-HARV-AIL-LIM` | Aileron position limits (f18harv) | **-25 / +42** | deg | as printed | SIM-F18HARV | NASA-TM-110216 | Tables 6.1/6.2, printed pp.90-93 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | FA18-BAS-AIL-LIM, FA18-P2T1-AIL-LIM |
| `FA18-HARV-AIL-RATE` | Aileron rate limit (f18harv) | **100** | deg/s | as printed | SIM-F18HARV | NASA-TM-110216 | Tables 6.1/6.2, printed pp.90-93 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `FA18-HARV-TEF-LIM` | Trailing-edge flap position limits (f18harv) | **-8 / +45** | deg | as printed | SIM-F18HARV | NASA-TM-110216 | Tables 6.1/6.2, printed pp.90-93 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `FA18-HARV-TEF-RATE` | Trailing-edge flap rate limit (f18harv) | **18** | deg/s | as printed | SIM-F18HARV | NASA-TM-110216 | Tables 6.1/6.2, printed pp.90-93 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `FA18-HARV-LEF-LIM` | Leading-edge flap position limits (f18harv) | **-3 / +33** | deg | as printed | SIM-F18HARV | NASA-TM-110216 | Tables 6.1/6.2, printed pp.90-93 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | FA18-BAS-LEF-LIM |
| `FA18-HARV-LEF-RATE` | Leading-edge flap rate limit (f18harv) | **15** | deg/s | as printed | SIM-F18HARV | NASA-TM-110216 | Tables 6.1/6.2, printed pp.90-93 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | FA18-BAS-LEF-RATE |
| `FA18-HARV-TVV-LIM` | TV vane position limits (f18harv) | **-10 / +25** | deg | as printed | SIM-F18HARV | NASA-TM-110216 | Tables 6.1/6.2, printed pp.90-93 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `FA18-HARV-TVV-RATE` | TV vane rate limit (f18harv) | **80** | deg/s | as printed | SIM-F18HARV | NASA-TM-110216 | Tables 6.1/6.2, printed pp.90-93 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `FA18-P2T1-STAB-LIM` | Stabilator position limits (HARV Phase II flight-test table) | **24 TEU / 10.5 TED** | deg | as printed | HARV-P2-TV | NASA-TP-97-206539 | Table 1, PDF p.14 / printed p.8 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `FA18-P2T1-STAB-RATE` | Stabilator rate limit (HARV Phase II flight-test table) | **40** | deg/s | as printed | HARV-P2-TV | NASA-TP-97-206539 | Table 1, PDF p.14 / printed p.8 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `FA18-P2T1-RUD-LIM` | Rudder position limits (HARV Phase II flight-test table) | **30 L / 30 R** | deg | as printed | HARV-P2-TV | NASA-TP-97-206539 | Table 1, PDF p.14 / printed p.8 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `FA18-P2T1-RUD-RATE` | Rudder rate limit (HARV Phase II flight-test table) | **82** | deg/s | as printed | HARV-P2-TV | NASA-TP-97-206539 | Table 1, PDF p.14 / printed p.8 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `FA18-P2T1-AIL-LIM` | Aileron position limits (HARV Phase II flight-test table) | **24 TEU / 45 TED** | deg | as printed | HARV-P2-TV | NASA-TP-97-206539 | Table 1, PDF p.14 / printed p.8 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | FA18-BAS-AIL-LIM, FA18-HARV-AIL-LIM |
| `FA18-P2T1-AIL-RATE` | Aileron rate limit (HARV Phase II flight-test table) | **100** | deg/s | as printed | HARV-P2-TV | NASA-TP-97-206539 | Table 1, PDF p.14 / printed p.8 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `FA18-P2T1-TEF-LIM` | Trailing-edge flap position limits (HARV Phase II flight-test table) | **8 up / 45 down** | deg | as printed | HARV-P2-TV | NASA-TP-97-206539 | Table 1, PDF p.14 / printed p.8 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `FA18-P2T1-TEF-RATE` | Trailing-edge flap rate limit (HARV Phase II flight-test table) | **18** | deg/s | as printed | HARV-P2-TV | NASA-TP-97-206539 | Table 1, PDF p.14 / printed p.8 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `FA18-P2T1-LEF-LIM` | Leading-edge flap position limits (HARV Phase II flight-test table) | **3 up / 33 down** | deg | as printed | HARV-P2-TV | NASA-TP-97-206539 | Table 1, PDF p.14 / printed p.8 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `FA18-P2T1-LEF-RATE` | Leading-edge flap rate limit (HARV Phase II flight-test table) | **15** | deg/s | as printed | HARV-P2-TV | NASA-TP-97-206539 | Table 1, PDF p.14 / printed p.8 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `FA18-P2T1-SB-LIM` | Speed brake position limits (HARV Phase II flight-test table) | **60 open** | deg | as printed | HARV-P2-TV | NASA-TP-97-206539 | Table 1, PDF p.14 / printed p.8 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `FA18-P2T1-SB-RATE` | Speed brake rate limit (HARV Phase II flight-test table) | **20-30** | deg/s | as printed | HARV-P2-TV | NASA-TP-97-206539 | Table 1, PDF p.14 / printed p.8 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `FA18-A7813-RUD-RATE` | Rudder no-load rate limit, MDC A7813 as reproduced | **56** | deg/s | 1 deg/s | SIM-F18BAS | NASA-TM-107601 | Table 8.6 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | FA18-BAS-RUD-RATE, FA18-ACT-RUD-RATE |

### Aerodynamic-data domains and envelopes

| Value ID | Quantity | Value | Units | Precision | Config | Source | Location as recorded | Exactness | Level | Impl. use | Repo x-ref | Conflicts |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `FA18-DB-ALPHA` | Aerodynamic database angle-of-attack domain | **-10 .. +90** | deg | as extracted (printed precision not con… | SIM-F18BAS, SIM-F18HARV | NASA-TM-110216 | Abstract | ABSTRACT_STATEMENT | ABS | DOMAIN_METADATA_ONLY | — | — |
| `FA18-DB-BETA` | Aerodynamic database sideslip domain | **-20 .. +20** | deg | as extracted (printed precision not con… | SIM-F18BAS, SIM-F18HARV | NASA-TM-110216 | Abstract | ABSTRACT_STATEMENT | ABS | DOMAIN_METADATA_ONLY | — | — |
| `FA18-DB-MACH` | Aerodynamic database Mach domain | **0.0 .. 2.0** | - | as extracted (printed precision not con… | SIM-F18HARV | NASA-TM-110216 | Abstract (per search extract) | SEARCH_EXTRACT | EXTRACT | DOMAIN_METADATA_ONLY | — | — |
| `FA18-RFCS-ENV` | Research flight control system engagement envelope | **M 0.2-0.7; 15,000-35,000 ft initial (later 45,000 ft)** | - | as extracted (printed precision not con… | HARV-P2-TV, HARV-P3-ANSER | NASA-WEB-HARV-VDD | VDD section 3.3 | SEARCH_EXTRACT | EXTRACT | DOMAIN_METADATA_ONLY | — | — |
| `FA18-PID-ALPHA-ILIFF-BASIC` | Alpha range of flight-derived lat-dir derivatives, basic F-18 | **3 .. 47** | deg | as extracted (printed precision not con… | HARV-P1-BASIC | NASA-TM-4786 | Abstract | ABSTRACT_STATEMENT | ABS | DOMAIN_METADATA_ONLY | — | — |
| `FA18-PID-ALPHA-NAPOLITANO` | Alpha range of flight-derived derivatives (WVU pEst) | **10 .. 60** | deg | as extracted (printed precision not con… | HARV-P2-TV | NASA-CR-194838 | Abstract | ABSTRACT_STATEMENT | ABS | DOMAIN_METADATA_ONLY | — | — |
| `FA18-DEP-TEST-COND` | Departure test entry conditions (inlet compatibility study) | **M 0.3-0.4; 35,000 ft; entry yaw rate 40-90 deg/s** | - | as extracted (printed precision not con… | HARV-P1-BASIC | NTRS-19990024943 | Abstract | SEARCH_EXTRACT | EXTRACT | DOMAIN_METADATA_ONLY | — | — |

### Propulsion

| Value ID | Quantity | Value | Units | Precision | Config | Source | Location as recorded | Exactness | Level | Impl. use | Repo x-ref | Conflicts |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `FA18-ENG-THRUST-CLASS` | Afterburning thrust class per engine (fact sheet) | **16,000** | lbf | as extracted (printed precision not con… | ENG-F404-GE-400 | NASA-FS-002-DFRC | NOT_CAPTURED | PUBLIC_FACT_SHEET | EXTRACT | PROHIBITED | — | — |
| `FA18-ENG-PLA-IDLE` | Power lever angle at idle | **35** | deg | as extracted (printed precision not con… | ENG-F404-GE-400 | UNRESOLVED: NASA-TM-4240, NASA-TP-3001, NASA-TM-4140 | NOT_CAPTURED | SEARCH_EXTRACT | EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | — | — |
| `FA18-ENG-PLA-INT` | Power lever angle at intermediate (max non-afterburning) | **102** | deg | as extracted (printed precision not con… | ENG-F404-GE-400 | UNRESOLVED: NASA-TM-4240, NASA-TP-3001, NASA-TM-4140 | NOT_CAPTURED | SEARCH_EXTRACT | EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | — | — |
| `FA18-ENG-PLARATE-4240` | Increasing-PLA rate limits, non-AB / AB (TM-4240, 1990) | **19.03 / 26.81** | deg/s | 0.01 deg/s | ENG-F404-GE-400, SIM-F18HARV | NASA-TM-4240 | Fig. 5 / text (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | FA18-ENG-PLARATE-110216 |
| `FA18-ENG-PLARATE-110216` | Increasing-PLA rate limits, non-AB / AB (TM-110216, 1996) | **14 / 22** | deg/s | 1 deg/s | SIM-F18HARV | NASA-TM-110216 | Section 4.1 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | FA18-ENG-PLARATE-4240 |
| `FA18-ENG-TAU` | Engine lag time constants (published identically in TM-4240 and TM-11… | **0.625 / 0.55** | s | 0.005 s | ENG-F404-GE-400, SIM-F18HARV | NASA-TM-4240 | per repository | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `FA18-ENG-GRID` | HARV engine table topology | **12 Mach (0-1.8) x 7 altitudes (0-50 kft) x 4 power states x 7 variables; 29 arrays** | - | exact structure | SIM-F18HARV | NASA-TM-110216 | Section 4.1, PDF p.44 / printed p.38 (per repository) | REPOSITORY_READING | LEAD | DOMAIN_METADATA_ONLY | yes | — |
| `FA18-ENG-MAXAB-FIG4` | Max-AB gross thrust surface (modified HARV nozzle), digitized | **DIGITIZED FIGURE (nearest 100 lbf; +/-150 lbf reading uncertainty)** | lbf | 100 lbf (digitized) | ENG-F404-GE-400, HARV-P2-TV | NASA-TM-4240 | Fig. 4, PDF p.14 / printed p.10 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |

### Accuracy statements

| Value ID | Quantity | Value | Units | Precision | Config | Source | Location as recorded | Exactness | Level | Impl. use | Repo x-ref | Conflicts |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `FA18-ENG-MODEL-ACC-SS` | Simple engine model steady-state accuracy vs GE complete model | **within 3** | % | as extracted (printed precision not con… | ENG-F404-GE-400, HARV-P2-TV | NASA-TM-4240 | Abstract | ABSTRACT_STATEMENT | ABS | NOT_A_MODEL_PARAMETER | — | — |
| `FA18-ENG-MODEL-ACC-TR` | Simple engine model transient accuracy vs GE complete model | **within 25** | % | as extracted (printed precision not con… | ENG-F404-GE-400, HARV-P2-TV | NASA-TM-4240 | Abstract | ABSTRACT_STATEMENT | ABS | NOT_A_MODEL_PARAMETER | — | — |
| `FA18-ENG-INFLIGHT-ACC` | Uninstalled gross thrust accuracy of in-flight thrust methods | **1 .. 4** | % | as extracted (printed precision not con… | ENG-F404-GE-400 | NASA-TP-3001 | Abstract | ABSTRACT_STATEMENT | ABS | NOT_A_MODEL_PARAMETER | — | — |
<!-- END GENERATED: values -->

## 3. Reading the candidates

> r0 text, kept for the record. Where it conflicts with the R1 corrections table at the top of this page, the R1 table wins.

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
