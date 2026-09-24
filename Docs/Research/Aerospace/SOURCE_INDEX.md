# Source Index (human-readable)

Canonical data: [`SOURCE_INDEX.json`](SOURCE_INDEX.json). Schema: [`SCHEMA.md`](SCHEMA.md). Library revision **r0** (2026-09-24).

## Summary

| Metric | r0 |
|---|---|
| Sources indexed | 75 |
| F/A-18 sources (incl. F404 engine-level) | 66 |
| Fundamentals (aircraft-independent) | 9 |
| L3 content extract / L2 abstract / L1 citation / L0 lead | 9 / 48 / 17 / 1 |
| L4 page-verified or above | **0** (PDF hosts blocked in the r0 session; see [README §4](README.md#4-r0-retrieval-limitation-read-this-before-trusting-anything)) |
| Recorded as not public / not located | 3 |

## Column key

- **Config**: `configuration_ids` with the `FA18-CFG-` prefix dropped. Definitions: [`Aircraft/FA18/KNOWN_DATA.md`](Aircraft/FA18/KNOWN_DATA.md#1-configuration-registry).
- **Provenance / Scope**: the two independent axes from `SCHEMA.md` §4.
- **Access**: `NTRS PDF` = NTRS record with PDF listed; `NTRS cit.` = record confirmed, PDF not confirmed; `NASA, ID?` = NASA report whose NTRS ID was not pinned down; `paywall` = publicly released, commercially published.
- **Lvl**: verification level (`SCHEMA.md` §2). Nothing is above L3 in r0.
- **Impl. / Valid.**: implementation and validation usefulness ratings. The reasoning is in the JSON `note` fields.

## Strongest F/A-18 sources (read these first)

1. `NASA-TM-110216`: f18harv nonlinear simulation, the most complete public HARV model description.
2. `NASA-TM-107601`: f18bas, its baseline F/A-18 predecessor (includes a *preliminary* TV model).
3. `NASA-TM-110217`: ANSER control law, specified "in sufficient detail to support coding the control law in flight software".
4. `NASA-TM-4772`: HARV overview with reference geometry and per-phase mass properties.
5. `NASA-TM-4786`, `NASA-TP-97-206539`, `NASA-TP-1999-206573`: Iliff & Wang flight-derived derivatives (basic F-18 and TV HARV).
6. `NASA-TP-3446`, `NASA-TP-1998-208465`: NASA research longitudinal and lateral-directional laws with nonlinear-simulation responses.
7. `NASA-TM-4240`: simplified F404 dynamic engine model used in the HARV TV simulation.
8. `NASA-TM-4783`: ground-to-flight correlation lessons (why tunnel data mispredict high-alpha forebody effects).

## Sources

### F/A-18: NASA simulation lineage

| ID | Title | Year | Report / link | Config | Provenance | Scope | Access | Lvl | Impl. | Valid. |
|---|---|---|---|---|---|---|---|---|---|---|
| `NASA-TM-107601` | Simulation Model of a Twin-Tail, High Performance Airplane | 1992 | [NASA-TM-107601](https://ntrs.nasa.gov/citations/19920024293) | SIM-F18BAS | DERIVED_SIM | SIM_ONLY | NTRS PDF | L3 | HIGH_IF_TABLES_PUBLIC | HIGH |
| `NASA-TM-110216` | Simulation Model of the F/A-18 High Angle-of-Attack Research Vehicle Utilized for the Design… | 1996 | [NASA-TM-110216](https://ntrs.nasa.gov/citations/19960027892) | SIM-F18HARV | DERIVED_SIM | SIM_ONLY | NTRS PDF | L3 | HIGH_IF_TABLES_PUBLIC | HIGH |
| `NASA-CR-1998-206937` | Implementation and Testing of Turbulence Models for the F18-HARV Simulation | 1998 | [NASA/CR-1998-206937](https://ntrs.nasa.gov/citations/19980028448) | SIM-F18HARV | DERIVED_SIM | SIM_ONLY | NTRS PDF | L2 | MEDIUM | LOW |
| `NASA-CR-198247` | Using the HARV Simulation Aerodynamic Model to Determine Forebody Strake Aerodynamic Coeffic… | 1995 | [NASA-CR-198247](https://ntrs.nasa.gov/citations/19960012499) | HARV-P3-ANSER, SIM-F18HARV | PRIMARY | RESEARCH_MOD | NTRS PDF | L2 | LOW | HIGH |

### F/A-18 HARV: program and configuration descriptions

| ID | Title | Year | Report / link | Config | Provenance | Scope | Access | Lvl | Impl. | Valid. |
|---|---|---|---|---|---|---|---|---|---|---|
| `NASA-TM-4772` | An Overview of the NASA F-18 High Alpha Research Vehicle | 1996 | [NASA-TM-4772](https://ntrs.nasa.gov/citations/19970003009) | HARV-P1-BASIC, HARV-P2-TV, HARV-P3-ANSER | PRIMARY | RESEARCH_MOD | NTRS PDF | L3 | HIGH_AFTER_PAGE_VERIFICATION | MEDIUM |
| `NASA-TM-104253` | The F-18 High Alpha Research Vehicle: A High-Angle-of-Attack Testbed Aircraft | 1992 | [NASA-TM-104253 (AIAA 92-4121)](https://ntrs.nasa.gov/citations/19920024160) | HARV-P2-TV | PRIMARY | RESEARCH_MOD | NTRS PDF | L2 | LOW | MEDIUM |
| `NASA-WEB-HARV-VDD` | NASA Dryden Past Projects: F-18 HARV Vehicle Description (VDD), Working Documents, and Fligh… | — | [NASA-WEB-HARV-VDD](https://www.nasa.gov/centers/dryden/history/pastprojects/HARV/index.html) | HARV-P1-BASIC, HARV-P2-TV, HARV-P3-ANSER | PRIMARY | RESEARCH_MOD | web | L3 | MEDIUM | HIGH_IF_DATA_PRESENT |
| `NASA-FS-002-DFRC` | NASA Facts: F-18 High Angle-of-Attack (Alpha) Research Vehicle (FS-002-DFRC) and NASA HARV r… | — | [NASA-FS-002-DFRC](https://www.nasa.gov/reference/f-18-harv/) | HARV-P1-BASIC, HARV-P2-TV, HARV-P3-ANSER | BACKGROUND | RESEARCH_MOD | web | L3 | NONE | LOW |

### F/A-18 HARV: flight control systems and research control laws

| ID | Title | Year | Report / link | Config | Provenance | Scope | Access | Lvl | Impl. | Valid. |
|---|---|---|---|---|---|---|---|---|---|---|
| `NASA-TM-104232` | Research Flight-Control System Development for the F-18 High Alpha Research Vehicle | 1991 | NASA-TM-104232 | HARV-P2-TV | PRIMARY | RESEARCH_MOD | NASA, ID? | L2 | MEDIUM | LOW |
| `NASA-TP-3446` | High-Alpha Research Vehicle (HARV) Longitudinal Controller: Design, Analyses, and Simulation… | 1994 | [NASA-TP-3446](https://ntrs.nasa.gov/citations/19950004448) | HARV-P2-TV, SIM-F18HARV | PRIMARY | RESEARCH_MOD | NTRS PDF | L2 | MEDIUM | HIGH |
| `NASA-TP-1998-208465` | High-Alpha Research Vehicle Lateral-Directional Control Law Description, Analyses, and Simul… | 1998 | [NASA/TP-1998-208465](https://ntrs.nasa.gov/citations/19990008184) | HARV-P2-TV, SIM-F18HARV | PRIMARY | RESEARCH_MOD | NTRS PDF | L2 | MEDIUM | HIGH |
| `NASA-TP-1998-208463` | A Control Law Design Method Facilitating Control Power, Robustness, Agility, and Flying Qual… | 1998 | [NASA/TP-1998-208463](https://ntrs.nasa.gov/citations/19990025985) | SIM-F18HARV | PRIMARY | SIM_ONLY | NTRS PDF | L1 | LOW | LOW |
| `NASA-TM-110217` | Design Specification for a Thrust-Vectoring, Actuated-Nose-Strake Flight Control Law for the… | — | [NASA-TM-110217](https://ntrs.nasa.gov/citations/19960044512) | HARV-P3-ANSER | PRIMARY | RESEARCH_MOD | NTRS cit. | L2 | HIGH_FOR_RESEARCH_CL | HIGH |
| `NASA-CR-198250` | Performance Validation of Version 152.0 ANSER Control Laws for the F-18 HARV | 1996 | [NASA-CR-198250](https://ntrs.nasa.gov/citations/19960017566) | HARV-P3-ANSER | PRIMARY | RESEARCH_MOD | NTRS PDF | L2 | LOW | HIGH |
| `NTRS-19960000845` | Performance Validation of the ANSER Control Laws for the F-18 HARV | — | [UNCONFIRMED](https://ntrs.nasa.gov/citations/19960000845) | HARV-P3-ANSER | PRIMARY | RESEARCH_MOD | NTRS cit. | L1 | LOW | MEDIUM |
| `NASA-TM-110228` | Design of a Mixer for the Thrust-Vectoring System on the High-Alpha Research Vehicle | 1996 | [NASA-TM-110228](https://ntrs.nasa.gov/citations/19960041445) | HARV-P2-TV, HARV-P3-ANSER | PRIMARY | RESEARCH_MOD | NTRS PDF | L2 | MEDIUM | MEDIUM |
| `NTRS-19990064010` | An Overview of Controls and Flying Qualities Technology on the F/A-18 High Alpha Research Ve… | — | [UNCONFIRMED](https://ntrs.nasa.gov/citations/19990064010) | HARV-P2-TV, HARV-P3-ANSER | PRIMARY | RESEARCH_MOD | NTRS PDF | L2 | LOW | MEDIUM |
| `NASA-TM-4773` | High-Alpha Handling Qualities Flight Research on the NASA F/A-18 High Alpha Research Vehicle | — | [NASA-TM-4773](https://ntrs.nasa.gov/citations/19970001693) | HARV-P2-TV, HARV-P3-ANSER | PRIMARY | RESEARCH_MOD | NTRS PDF | L2 | LOW | HIGH |
| `NTRS-19980200994` | Closed-Loop System Identification Experience for Flight Control Law and Flying Qualities Eva… | 1998 | [AGARD SCI Symposium paper (Madrid, May 1998)](https://ntrs.nasa.gov/citations/19980200994) | HARV-P3-ANSER | PRIMARY | RESEARCH_MOD | NTRS cit. | L2 | LOW | HIGH |
| `NTRS-19900019226` | Validation of the F-18 High Alpha Research Vehicle Flight Control and Avionics Systems Modif… | — | [UNCONFIRMED (conference paper)](https://ntrs.nasa.gov/citations/19900019226) | HARV-P2-TV | PRIMARY | RESEARCH_MOD | NTRS PDF | L2 | LOW | LOW |
| `NTRS-19970014822` | HARV ANSER Flight Test Data Retrieval and Processing Procedures | — | [UNCONFIRMED (NASA CR)](https://ntrs.nasa.gov/citations/19970014822) | HARV-P3-ANSER | PRIMARY | EXACT | NTRS PDF | L2 | LOW | HIGH |

### F/A-18: flight-control computers and later research laws (other airframes)

| ID | Title | Year | Report / link | Config | Provenance | Scope | Access | Lvl | Impl. | Valid. |
|---|---|---|---|---|---|---|---|---|---|---|
| `NTRS-19970041277` | Production Support Flight Control Computers: Research Capability for F/A-18 Aircraft at Dryd… | 1997 | [NASA TM (Digital Avionics Systems Conference, Oct 1997)](https://ntrs.nasa.gov/citations/19970041277) | PROD-AB | PRIMARY | PROD_FAMILY | NTRS PDF | L2 | LOW | LOW |
| `NTRS-19990060322` | Initial Flight Test of the Production Support Flight Control Computers at NASA Dryden Flight… | — | [UNCONFIRMED](https://ntrs.nasa.gov/citations/19990060322) | PROD-AB | PRIMARY | PROD_FAMILY | NTRS PDF | L2 | LOW | LOW |
| `NTRS-20110015950` | Nonlinear Dynamic Inversion Baseline Control Law: Flight-Test Results for the Full-scale Adv… | 2011 | [UNCONFIRMED](https://ntrs.nasa.gov/citations/20110015950) | FAST-853 | PRIMARY | RESEARCH_MOD | NTRS PDF | L2 | LOW | MEDIUM |

### F/A-18: parameter estimation and flight-derived derivatives

| ID | Title | Year | Report / link | Config | Provenance | Scope | Access | Lvl | Impl. | Valid. |
|---|---|---|---|---|---|---|---|---|---|---|
| `NASA-TM-4786` | Extraction of Lateral-Directional Stability and Control Derivatives for the Basic F-18 Aircr… | 1997 | [NASA-TM-4786](https://ntrs.nasa.gov/citations/19970010502) | HARV-P1-BASIC | PRIMARY | EXACT | NTRS PDF | L2 | MEDIUM_VIA_DIGITIZATION | HIGH |
| `NASA-TP-97-206539` | Flight-Determined Subsonic Longitudinal Stability and Control Derivatives of the F-18 High A… | 1997 | [NASA/TP-97-206539](https://ntrs.nasa.gov/citations/19980007172) | HARV-P2-TV | PRIMARY | EXACT | NTRS PDF | L2 | MEDIUM_VIA_DIGITIZATION | HIGH |
| `NASA-TP-1999-206573` | Flight-Determined, Subsonic, Lateral-Directional Stability and Control Derivatives of the Th… | 1999 | [NASA/TP-1999-206573](https://ntrs.nasa.gov/citations/19990019364) | HARV-P2-TV, HARV-P1-BASIC | PRIMARY | EXACT | NTRS PDF | L2 | MEDIUM_VIA_DIGITIZATION | HIGH |
| `NASA-CR-194838` | Determination of the Stability and Control Derivatives of the NASA F/A-18 HARV Using Flight… | 1993 | [NASA-CR-194838](https://ntrs.nasa.gov/citations/19940020331) | HARV-P2-TV | PRIMARY | EXACT | NTRS PDF | L2 | MEDIUM_VIA_DIGITIZATION | HIGH |
| `NASA-CR-191216` | Determination of the Stability and Control Derivatives of the F/A-18 HARV from Flight Data U… | — | [NASA-CR-191216](https://ntrs.nasa.gov/citations/19930003715) | HARV-P1-BASIC | PRIMARY | EXACT | NTRS PDF | L1 | LOW | MEDIUM |
| `NASA-CR-200251` | Estimation of the Longitudinal and Lateral-Directional Aerodynamic Parameters from Flight Da… | 1996 | [NASA-CR-200251](https://ntrs.nasa.gov/citations/19960014815) | HARV-P2-TV | PRIMARY | EXACT | NTRS PDF | L1 | LOW | MEDIUM |
| `AIAA-96-3419` | Estimation of the Longitudinal Aerodynamic Parameters from Flight Data for the NASA F/A-18 HARV | 1996 | AIAA-96-3419-CP | HARV-P2-TV | PRIMARY | EXACT | paywall | L1 | LOW | MEDIUM |
| `WVU-ETD-9553` | Estimation of the Longitudinal and Lateral-Directional Aerodynamic Parameters from Flight Da… | — | [WVU ETD 9553](https://researchrepository.wvu.edu/etd/9553/) | HARV-P2-TV | PRIMARY | EXACT | univ. | L1 | LOW | MEDIUM |
| `NASA-CR-198248` | F-18 High Alpha Research Vehicle (HARV) Parameter Identification Flight Test Maneuvers for O… | 1995 | [NASA-CR-198248](https://ntrs.nasa.gov/citations/19960012190) | HARV-P2-TV, HARV-P3-ANSER | PRIMARY | EXACT | NTRS PDF | L2 | LOW | HIGH |
| `NTRS-20040087105` | Real-Time Parameter Estimation in the Frequency Domain | — | [UNCONFIRMED (journal/conference version)](https://ntrs.nasa.gov/citations/20040087105) | HARV-P2-TV | PRIMARY | EXACT | NTRS PDF | L2 | LOW | HIGH |
| `JAIRCRAFT-1995-MORELLI-MOF` | Global Nonlinear Aerodynamic Modeling Using Multivariate Orthogonal Functions | 1995 | [J. Aircraft (doi 10.2514/3.46712); earlier AIAA 93-3636 'Nonlinear aerodynamic modeling using multivariate orthogonal functions'](https://arc.aiaa.org/doi/10.2514/3.46712) | SIM-F18HARV | CURVE_FIT | TUNNEL | paywall | L3 | POTENTIALLY_HIGH | HIGH |
| `NTRS-19940020628` | (Nonlinear) Dynamic Modeling Using Multivariate Orthogonal Functions — F-18 high angle of at… | 1994 | [UNCONFIRMED (NTRS accession N94-25110)](https://ntrs.nasa.gov/citations/19940020628) | SIM-F18HARV | CURVE_FIT | TUNNEL | NTRS PDF | L1 | UNASSESSED | UNASSESSED |

### F/A-18: wind tunnel, thrust vectoring, forebody controls, ground-to-flight

| ID | Title | Year | Report / link | Config | Provenance | Scope | Access | Lvl | Impl. | Valid. |
|---|---|---|---|---|---|---|---|---|---|---|
| `NASA-TP-3111` | Wind Tunnel Investigation of Vortex Flows on F/A-18 Configuration at Subsonic Through Transo… | 1991 | [NASA-TP-3111](https://ntrs.nasa.gov/citations/19920005750) | PROD-AB | PRIMARY | TUNNEL | NTRS PDF | L2 | LOW | MEDIUM |
| `AIAA-89-2222` | Experimental Investigation of the F/A-18 Vortex Flows at Subsonic Through Transonic Speeds | 1989 | [AIAA 89-2222](https://ntrs.nasa.gov/citations/19890060307) | PROD-AB | PRIMARY | TUNNEL | NTRS cit. | L1 | LOW | LOW |
| `NASA-CR-4582` | F/A-18 Forebody Vortex Control, Volume 1 — Static Tests (Volume 2: rotary-balance tests) | 1994 | [NASA-CR-4582](https://ntrs.nasa.gov/citations/19940031484) | PROD-AB | PRIMARY | TUNNEL | NTRS PDF | L2 | LOW | MEDIUM |
| `NASA-TP-3531` | Multiaxis Thrust-Vectoring Characteristics of a Model Representative of the F-18 High-Alpha… | 1995 | [NASA-TP-3531](https://ntrs.nasa.gov/citations/19970001863) | HARV-P2-TV | PRIMARY | TUNNEL | NTRS PDF | L3 | MEDIUM | HIGH |
| `NTRS-19920066113` | Thrust Vectoring Characteristics of the F-18 High Alpha Research Vehicle at Angles of Attack… | — | [UNCONFIRMED](https://ntrs.nasa.gov/citations/19920066113) | HARV-P2-TV | PRIMARY | TUNNEL | NTRS cit. | L1 | LOW | LOW |
| `NASA-TM-4771` | Thrust Vectoring on the NASA F-18 High Alpha Research Vehicle | 1996 | [NASA-TM-4771](https://ntrs.nasa.gov/citations/19970001572) | HARV-P2-TV | PRIMARY | RESEARCH_MOD | NTRS PDF | L2 | LOW | MEDIUM |
| `JAIRCRAFT-1995-MURRI-STRAKES` | Actuated Forebody Strake Controls for the F-18 High-Alpha Research Vehicle | 1995 | [J. Aircraft (doi 10.2514/3.46755); AIAA 93-3675](https://ntrs.nasa.gov/citations/19930060236) | HARV-P3-ANSER | PRIMARY | TUNNEL | NTRS cit. | L2 | LOW | MEDIUM |
| `NASA-TM-4783` | The F/A-18 High-Angle-of-Attack Ground-to-Flight Correlation: Lessons Learned | 1997 | [NASA-TM-4783 (DFRC H-2149)](https://www.nasa.gov/centers/dryden/news/DTRS/1997/Bib/H-2149.html) | HARV-P1-BASIC, HARV-P2-TV | PRIMARY | RESEARCH_MOD | web | L2 | LOW | HIGH |
| `RTO-MP-069-P45` | Forebody Aerodynamics of the F-18 High Alpha Research Vehicle (full-scale 80x120 and 30x60 t… | — | [RTO-MP-069 paper 45; DTIC ADA419094](https://ntrs.nasa.gov/citations/20010055691) | HARV-P1-BASIC | PRIMARY | RESEARCH_MOD | NTRS PDF | L2 | LOW | MEDIUM |
| `NASA-CP-10143` | Fourth High Alpha Conference (NASA Dryden, 12-14 July 1994), Volumes 1-3 | 1994 | [NASA-CP-10143](https://ntrs.nasa.gov/citations/19950007815) | HARV-P2-TV | PRIMARY | — | NTRS PDF | L2 | LOW | MEDIUM |

### F/A-18 / F404: propulsion and inlet

| ID | Title | Year | Report / link | Config | Provenance | Scope | Access | Lvl | Impl. | Valid. |
|---|---|---|---|---|---|---|---|---|---|---|
| `NASA-TM-4240` | A Simple Dynamic Engine Model for Use in a Real-Time Aircraft Simulation with Thrust Vectoring | 1990 | [NASA-TM-4240](https://ntrs.nasa.gov/citations/19910009766) | HARV-P2-TV, F404-GE-400 | DERIVED_SIM | SIM_ONLY | NTRS cit. | L2 | MEDIUM | MEDIUM |
| `NASA-TM-104329` | Inlet Distortion for an F/A-18A Aircraft During Steady Aerodynamic Conditions up to 60 deg A… | — | [NASA-TM-104329](https://ntrs.nasa.gov/citations/19970022131) | HARV-P1-BASIC, F404-GE-400 | PRIMARY | EXACT | NTRS PDF | L2 | LOW | MEDIUM |
| `NTRS-19990024943` | Factors Affecting Inlet-Engine Compatibility During Aircraft Departures at High Angle of Att… | — | [UNCONFIRMED](https://ntrs.nasa.gov/citations/19990024943) | HARV-P1-BASIC, F404-GE-400 | PRIMARY | EXACT | NTRS cit. | L3 | LOW | MEDIUM |
| `NASA-TP-3001` | Evaluation of Various Thrust Calculation Techniques on an F404 Engine | 1990 | [NASA-TP-3001](https://ntrs.nasa.gov/citations/19900015818) | F404-GE-400 | PRIMARY | PROD_FAMILY | NTRS PDF | L2 | LOW | MEDIUM |
| `NASA-TM-4140` | Measurement Effects on the Calculation of In-Flight Thrust for an F404 Turbofan Engine | 1989 | [NASA-TM-4140 (AIAA 89-2364)](https://ntrs.nasa.gov/citations/19900002425) | F404-GE-400 | PRIMARY | PROD_FAMILY | NTRS PDF | L2 | LOW | MEDIUM |
| `NASA-TM-88273` | Exhaust-Gas Pressure and Temperature Survey of F404-GE-400 Turbofan Engine | 1988 | [NASA-TM-88273](https://ntrs.nasa.gov/citations/19880010923) | F404-GE-400 | PRIMARY | PROD_FAMILY | NTRS PDF | L2 | LOW | LOW |

### F/A-18: SRA (NASA 845) and AAW (NASA 853)

| ID | Title | Year | Report / link | Config | Provenance | Scope | Access | Lvl | Impl. | Valid. |
|---|---|---|---|---|---|---|---|---|---|---|
| `NASA-TM-4433` | The F-18 Systems Research Aircraft Facility | — | [NASA-TM-4433 (OCR-derived, probable)](https://ntrs.nasa.gov/citations/19930007564) | SRA-845 | PRIMARY | PREPROD | NTRS PDF | L1 | NONE | LOW |
| `NASA-FS-039-DFRC` | NASA Facts: F/A-18 Systems Research Aircraft | — | [NASA-FS-039-DFRC](https://www.nasa.gov/wp-content/uploads/2021/09/120294main_fs-039-dfrc.pdf) | SRA-845 | BACKGROUND | PREPROD | web | L2 | NONE | NONE |
| `SRA-EPAD-EHA` | Performance of an Electro-Hydrostatic Actuator on the F-18 Systems Research Aircraft | — | UNCONFIRMED (NASA TM) | SRA-845 | PRIMARY | RESEARCH_MOD | NASA, ID? | L1 | LOW | MEDIUM |
| `NTRS-20010039533` | Flight Test Experience with an Electromechanical Actuator on the F-18 Systems Research Aircraft | — | [UNCONFIRMED (IEEE/DASC paper)](https://ntrs.nasa.gov/citations/20010039533) | SRA-845 | PRIMARY | RESEARCH_MOD | NTRS PDF | L1 | LOW | LOW |
| `NASA-TM-2005-213664` | Flight Test of the F/A-18 Active Aeroelastic Wing Airplane | 2005 | [NASA/TM-2005-213664](https://ntrs.nasa.gov/citations/20050212234) | AAW-853 | PRIMARY | RESEARCH_MOD | NTRS cit. | L3 | LOW | MEDIUM |
| `NASA-TM-2005-213668` | Active Aeroelastic Wing Aerodynamic Model Development and Validation for a Modified F/A-18A… | 2005 | [NASA/TM-2005-213668 (AIAA 2005-6312)](https://ntrs.nasa.gov/citations/20050204039) | AAW-853 | PRIMARY | RESEARCH_MOD | NTRS PDF | L2 | LOW | MEDIUM |
| `NASA-TM-2005-213666` | Development and Testing of Control Laws for the Active Aeroelastic Wing Program | 2005 | NASA/TM-2005-213666 | AAW-853 | PRIMARY | RESEARCH_MOD | NASA, ID? | L1 | LOW | LOW |
| `NTRS-20050204113` | Loads Model Development and Analysis for the F/A-18 Active Aeroelastic Wing Airplane | — | [UNCONFIRMED](https://ntrs.nasa.gov/citations/20050204113) | AAW-853 | PRIMARY | RESEARCH_MOD | NTRS PDF | L1 | NONE | LOW |

### F/A-18: derived / external / cross-validation only

| ID | Title | Year | Report / link | Config | Provenance | Scope | Access | Lvl | Impl. | Valid. |
|---|---|---|---|---|---|---|---|---|---|---|
| `JGCD-2011-CHAKRABORTY-LINEAR` | Susceptibility of F/A-18 Flight Controllers to the Falling-Leaf Mode: Linear Analysis | 2011 | [J. Guidance, Control, and Dynamics 34(1):57-72, doi 10.2514/1.50674](https://arc.aiaa.org/doi/10.2514/1.50674) | UMN-DERIVED | DERIVED_SIM | SIM_ONLY | author | L2 | CROSS_VALIDATION_ONLY | HIGH |
| `JGCD-2011-CHAKRABORTY-NONLINEAR` | Susceptibility of F/A-18 Flight Controllers to the Falling-Leaf Mode: Nonlinear Analysis | 2011 | [J. Guidance, Control, and Dynamics 34(1):73-85, doi 10.2514/1.50675](https://arc.aiaa.org/doi/10.2514/1.50675) | UMN-DERIVED | DERIVED_SIM | SIM_ONLY | paywall | L2 | CROSS_VALIDATION_ONLY | MEDIUM |
| `AIAA-2004-542` | Falling Leaf Motion Suppression in the F/A-18 Hornet with Revised Flight Control Software | 2004 | [AIAA 2004-0542](https://arc.aiaa.org/doi/abs/10.2514/6.2004-542) | PROD-AB | PRIMARY | PROD_FAMILY | paywall | L1 | UNASSESSED | MEDIUM |

### F/A-18: not public or not located (recorded so nobody searches again)

| ID | Title | Year | Report / link | Config | Provenance | Scope | Access | Lvl | Impl. | Valid. |
|---|---|---|---|---|---|---|---|---|---|---|
| `NAVAIR-A1-F18AC-NFM-000` | NATOPS Flight Manual, Navy Model F/A-18A/B/C/D 161353 and Up Aircraft (A1-F18AC-NFM-000) | — | A1-F18AC-NFM-000 | PROD-AB | BACKGROUND | PROD_FAMILY | **NOT PUBLIC** | L2 | PROHIBITED | PROHIBITED |
| `MCAIR-FA18-AERO-DATABASE` | McDonnell Aircraft Company F/A-18 aerodynamic data reports (upstream of NASA f18bas wind-tun… | — | UNKNOWN | PROD-AB | PRIMARY | — | **NOT LOCATED** | L0 | UNAVAILABLE | UNAVAILABLE |
| `GE-F404-COMPLETE-ENGINE-MODEL` | General Electric F404-GE-400 complete nonlinear dynamic engine model (supplied to NASA Dryden) | — | UNKNOWN | F404-GE-400 | PRIMARY | PROD_FAMILY | **PROPRIETARY** | L2 | UNAVAILABLE | UNAVAILABLE |

### Fundamentals (aircraft-independent)

| ID | Title | Year | Report / link | Config | Provenance | Scope | Access | Lvl | Impl. | Valid. |
|---|---|---|---|---|---|---|---|---|---|---|
| `NASA-RP-1207` | Derivation and Definition of a Linear Aircraft Model | 1988 | [NASA-RP-1207](https://ntrs.nasa.gov/citations/19890005752) | — | PRIMARY | — | NTRS PDF | L2 | HIGH | HIGH |
| `NASA-SP-3070` | Summary of Transformation Equations and Equations of Motion Used in Free-Flight and Wind-Tun… | 1972 | [NASA-SP-3070](https://ntrs.nasa.gov/citations/19720018825) | — | PRIMARY | — | NTRS PDF | L2 | HIGH | HIGH |
| `NASA-TM-2015-218675` | Check-Cases for Verification of 6-Degree-of-Freedom Flight Vehicle Simulations (Vols. I-II) | 2015 | [NASA/TM-2015-218675](https://ntrs.nasa.gov/citations/20150001264) | — | PRIMARY | SIM_ONLY | NTRS PDF | L2 | MEDIUM | HIGH |
| `NTRS-20150006038` | Further Development of Verification Check-Cases for Six-Degree-of-Freedom Flight Vehicle Sim… | — | [UNCONFIRMED (AIAA paper)](https://ntrs.nasa.gov/citations/20150006038) | — | PRIMARY | SIM_ONLY | NTRS PDF | L1 | LOW | HIGH |
| `NASA-RP-1168` | Application of Parameter Estimation to Aircraft Stability and Control: The Output-Error Appr… | 1986 | [NASA-RP-1168](https://ntrs.nasa.gov/citations/19870020066) | — | PRIMARY | — | NTRS PDF | L2 | LOW | HIGH |
| `NTRS-19850011474` | Identification of Dynamic Systems: Theory and Formulation | — | [UNCONFIRMED (NASA RP-1138, probable)](https://ntrs.nasa.gov/citations/19850011474) | — | PRIMARY | — | NTRS cit. | L1 | LOW | MEDIUM |
| `NASA-TM-X-74335` | U.S. Standard Atmosphere, 1976 | 1976 | [NASA-TM-X-74335; NOAA-S/T 76-1562](https://ntrs.nasa.gov/citations/19770009539) | — | PRIMARY | — | NTRS cit. | L2 | HIGH | HIGH |
| `NASA-TM-74097` | Aerodynamic Characteristics of Airplanes at High Angles of Attack | 1977 | [NASA-TM-74097](https://ntrs.nasa.gov/citations/19780005068) | — | BACKGROUND | — | NTRS cit. | L2 | LOW | MEDIUM |
| `MIL-HDBK-1797` | Flying Qualities of Piloted Aircraft (MIL-HDBK-1797, 19 December 1997) | 1997 | [MIL-HDBK-1797](https://everyspec.com/MIL-HDBK/MIL-HDBK-1500-1799/MIL-HDBK-1797_NOTICE-1_39380/) | — | PRIMARY | — | Dist A | L2 | MEDIUM | HIGH |

## Existing packs not yet migrated

These are already indexed, with their own provenance rules, in `Docs/Reference/`. They are **not** duplicated in `SOURCE_INDEX.json` yet. Migration is a queued package (see `Aircraft/F16/README.md` and `Aircraft/F15/README.md`).

| Pack | Covers | Notes |
|---|---|---|
| [`Docs/Reference/F16_SOURCE_PACK_V0.1.md`](../../Reference/F16_SOURCE_PACK_V0.1.md) | NASA TP-1538, Morelli 1998 F-16 polynomial model, Garza & Morelli NASA/TM-2003-212145, AFTI/F-16, F-16XL, VISTA/MATV, cross-validation implementations | TP-1538 Table VI is transcribed and cross-checked (L5-equivalent) in `Docs/Reference/Data/F16/TP1538/` |
| [`Docs/Reference/F15_SOURCE_PACK_V0.1.md`](../../Reference/F15_SOURCE_PACK_V0.1.md) and `F15_FULL_SCALE_*` | Baseline, high-alpha, spin, transonic and propulsion F-15 sources; configuration-separation rules | Priority C package will index the sources not yet catalogued centrally (A4172 / DN-1180 lineage, NASA 836, F100 family, AFIT reports) |
