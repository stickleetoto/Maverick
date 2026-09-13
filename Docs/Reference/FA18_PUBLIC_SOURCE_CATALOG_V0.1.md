# F/A-18 Public Source Catalog V0.1

Status: **R0 PUBLIC-SOURCE ACQUISITION — no implementation authority implied**

This catalog records public, lawfully released technical sources for the F/A-18 family. It deliberately separates early/basic F-18/F/A-18, NASA HARV phases, AAW, and F/A-18E/F. A source being technically strong does not make it transferable across those configurations.

## 1. Source-class vocabulary

- `PRIMARY_EXACT` — primary authority for the exact configuration named in the source record.
- `PRIMARY_COMPATIBLE` — primary source with a demonstrated compatible configuration for the stated field.
- `PRIMARY_FAMILY_ONLY` — authoritative F/A-18-family source, but exact-configuration equivalence is not established.
- `SECONDARY_LEAD` — useful public citation/bibliography leading toward primary material.
- `UNVERIFIED_LEAD` — plausible lead not yet verified.
- `NOT_ACQUIRED` — known primary document not acquired as a verified public copy in this pass.

Acquisition note: source PDFs were not added to this repository. Where the session obtained NTRS searchable full text but not downloadable bytes, `SHA256` is recorded as `NOT_COMPUTED — bytes not stored`. A missing page/table anchor is explicitly marked rather than reconstructed from OCR.

## 2. Configuration tags used by this catalog

- `NASA_F18BAS_BASIC_F18_OFP_8_3_3_TVC_OFF_CAPABLE`
- `NASA_F18_HARV_160780_PHASE1_BASIC`
- `NASA_F18_HARV_160780_PHASE2_TVC_NASA0`
- `NASA_F18_HARV_160780_PHASE2_TVC_NASA1A`
- `NASA_F18_HARV_160780_PHASE3_TVC_ANSER`
- `NASA_F18_AAW_MODIFIED_F18A`
- `NASA_F18B_SRA_AAW_BASELINE_SUPPORT`
- `FA18EF_SUPER_HORNET_FAMILY`

These tags are intentionally research-state tags, not operational fleet-block claims.

---

## 3. High-value source records

### FA18-SRC-001 — `f18bas` nonlinear F/A-18 simulation

- **Aircraft/configuration:** basic F/A-18 simulation with OFP 8.3.3 inner-loop control laws; preliminary hypothetical HARV thrust-vectoring model is present, but can be disabled with `LTHVEC=false`.
- **Title:** *Simulation Model of a Twin-Tail, High Performance Airplane*
- **Report:** NASA-TM-107601 / NAS 1.15:107601 / N92-33537
- **Revision:** published baseline report, July 1992.
- **Authors:** Carey S. Buttrill; P. Douglas Arbuckle; Keith D. Hoffler.
- **Organization:** NASA Langley Research Center; ViGYAN, Inc.
- **Publication date:** July 1992.
- **Repository:** NASA NTRS.
- **Stable URL:** https://ntrs.nasa.gov/citations/19920024293
- **PDF filename:** `19920024293.pdf`
- **SHA256:** `NOT_COMPUTED — PDF bytes not stored`.
- **PDF page count:** 180 pages, as indexed by NASA/GPO technical-report catalog metadata.
- **Relevant anchors:** Summary; sec. 1.2 simulation lineage/configuration; aerodynamic-model, engine, actuator, sensor and digital-control-law sections; exact PDF page numbers require a later byte-level acquisition because NTRS direct-PDF retrieval returned HTTP 403 in this session.
- **Data type:** nonlinear 6-DOF simulation specification; table-lookup aerodynamics; engine model; first-order actuators with rate/position limiting; sensors; simplified F/A-18 digital control law version 8.3.3.
- **Documented aerodynamic range:** alpha -10 to +90 deg; beta -20 to +20 deg.
- **Configuration statement:** the report documents `f18bas`; the frozen version includes a hypothetical two-paddle-per-engine thrust-vectoring concept. The report states the non-vectoring model can be recovered by setting `LTHVEC=false`.
- **Lineage:** rigid-body aero and engine models were largely derived from the Langley DMS real-time F/A-18 simulation (`dmsf18`), itself derived from McDonnell Douglas material.
- **Applicability:** strongest public simulation-level source for a basic F/A-18 research model; useful foundation for geometry, aero structure, actuator/FCS architecture and engine-model investigation.
- **Limitations:** no exact airframe serial or operational C/D block is established; hypothetical TVC must not be mistaken for a production feature; underlying `dmsf18`/MDC source package has not been acquired.
- **Classification:** `PRIMARY_EXACT` for the documented `f18bas` simulation configuration; `PRIMARY_FAMILY_ONLY` for an operational F/A-18A/B/C/D.

### FA18-SRC-002 — `f18harv` nonlinear HARV simulation

- **Aircraft/configuration:** NASA F/A-18 HARV with multi-axis thrust-vectoring system and actuated forebody strakes represented; research flight-control system included.
- **Title:** *Simulation Model of the F/A-18 High Angle-of-Attack Research Vehicle Utilized for the Design of Advanced Control Laws*
- **Report:** NASA-TM-110216 / NTRS 19960027892.
- **Revision:** published May 1996.
- **Authors:** Mark E. Strickland; W. Thomas Bundick; Michael D. Messina; Keith D. Hoffler; Susan W. Carzoo; Jessie C. Yeager; Fred L. Beissner Jr.
- **Organization:** NASA Langley / NASA Dryden program contributors.
- **Repository:** NASA NTRS.
- **Stable URL:** https://ntrs.nasa.gov/citations/19960027892
- **PDF filename:** `19960027892.pdf`
- **SHA256:** `NOT_COMPUTED — PDF bytes not stored`.
- **PDF page count:** not independently verified from PDF bytes in this session.
- **Relevant printed pages/anchors:** p. 1 Introduction; sec. 2 configuration/axes; sec. 3 aerodynamic model; sec. 4 thrust-vectoring/engine model; sec. 6 actuator modifications; sec. 7 RFCS; p. 119 references. Searchable text identifies Tables 3.1/3.3, 4.22/4.23, 6.1 and related figures.
- **Data type:** nonlinear 6-DOF simulation architecture; HARV aero increments; F404 engine lookup model; TVC; ANSER aero; actuator dynamics/nonlinear limits; sensors; research FCS.
- **Documented simulation envelope:** alpha -10 to +90 deg; beta -20 to +20 deg; Mach 0 to 2; altitude 0 to 60,000 ft.
- **Important model warning:** the report states the aerodynamic model is based on wind-tunnel results and had not itself been flight validated at the time of publication.
- **Applicability:** extremely rich exact source for the modified HARV simulation and a bridge to underlying model/data files.
- **Limitations:** incompatible as direct authority for Phase-I/basic HARV whenever TVC/ANSER-specific increments, research FCS, updated actuators, mass balance, or engine model are active.
- **Classification:** `PRIMARY_EXACT` for `f18harv`; `PRIMARY_FAMILY_ONLY` for a basic/production Hornet.

### FA18-SRC-003 — HARV exact airframe identity and phase boundaries

- **Aircraft/configuration:** NASA F-18 HARV, BuNo 160780, sixth full-scale developmental F-18.
- **Title:** *An Overview of the NASA F-18 High Alpha Research Vehicle*
- **Report:** NASA-TM-4772 / NTRS 19970003009.
- **Authors:** Albion H. Bowers; Joseph W. Pahle; R. Joseph Wilson; Bradley C. Flick; Richard L. Rood.
- **Organization:** NASA Dryden Flight Research Center.
- **Publication date:** 1996.
- **Repository:** NASA NTRS.
- **Stable URL:** https://ntrs.nasa.gov/citations/19970003009
- **PDF filename:** NTRS record download associated with 19970003009.
- **SHA256:** `NOT_COMPUTED — PDF bytes not stored`.
- **Page count / exact PDF page:** not byte-verified in this session.
- **Data type:** configuration history, program phases, hardware, flight-test context.
- **Configuration statement:** Phase I baseline/basic F-18 research; Phase II adds multi-axis thrust vectoring; Phase III adds actuated forebody strakes. NASA public program material identifies BuNo 160780 and two F404-GE-400 engines.
- **Applicability:** primary configuration-control authority for deciding whether a HARV source is Phase I, Phase II or Phase III.
- **Limitations:** overview source, not a coefficient database.
- **Classification:** `PRIMARY_EXACT` for HARV identity/program phase definitions.

### FA18-SRC-004 — exact Phase-I/basic HARV flight derivatives

- **Aircraft/configuration:** NASA F-18 research aircraft in **basic hardware and software configuration**, Phase-I flights 11 through 38, June 1987–March 1988; pre-TVC and pre-ANSER.
- **Title:** *Extraction of Lateral-Directional Stability and Control Derivatives for the Basic F-18 Aircraft at High Angles of Attack*
- **Report:** NASA-TM-4786 / H-2143 / NTRS 19970010502.
- **Authors:** Kenneth W. Iliff; Kon-Sheng Charles Wang.
- **Organization:** NASA Dryden Flight Research Center; SPARTA, Inc.
- **Publication date:** February 1997.
- **Repository:** NASA NTRS.
- **Stable URL:** https://ntrs.nasa.gov/citations/19970010502
- **PDF filename:** `19970010502.pdf`
- **SHA256:** `NOT_COMPUTED — PDF bytes not stored`.
- **Page count:** not byte-verified in this session.
- **Relevant anchor:** derivative results are plotted versus alpha 3–47 deg; report text states 42 maneuvers from Phase-I flights 11–38. Exact figure/page numbers remain to be transcribed from an acquired PDF.
- **Data type:** flight-determined lateral-directional stability/control derivatives with uncertainty/estimation context.
- **Applicability:** strongest exact flight-identification source located for the unvectored/basic HARV airframe state.
- **Limitations:** CAS was engaged, causing correlations and degrading some parameter estimates; not a full nonlinear coefficient database.
- **Classification:** `PRIMARY_EXACT` for `NASA_F18_HARV_160780_PHASE1_BASIC` derivative evidence.

### FA18-SRC-005 — early HARV flight-estimated parameters

- **Aircraft/configuration:** NASA F/A-18 HARV in early HATP flight program; publication predates Phase-II TVC operations.
- **Title:** *Aerodynamic Parameters of High-Angle-of-Attack Research Vehicle (HARV) Estimated from Flight Data*
- **Report:** NASA-TM-102692 / NTRS 19900019262.
- **Authors:** Vladislav Klein; Thomas R. Ratvasky; Brent R. Cobleigh.
- **Organization:** NASA Dryden Flight Research Facility.
- **Publication date:** August 1990.
- **Repository:** NASA NTRS.
- **Stable URL:** https://ntrs.nasa.gov/citations/19900019262
- **PDF filename:** NTRS 19900019262 download.
- **SHA256:** `NOT_COMPUTED — PDF bytes not stored`.
- **Page anchors:** exact PDF pages not visually acquired; NTRS abstract identifies alpha 10–50 deg and flight-estimated aerodynamic coefficients/derivatives.
- **Data type:** flight parameter identification; longitudinal/lateral-directional aerodynamic quantities.
- **Applicability:** strong Phase-I-compatible flight evidence.
- **Limitations:** each maneuver/configuration point still requires exact phase/hardware confirmation before numerical import; known air-data bias issues are discussed in the report.
- **Classification:** `PRIMARY_COMPATIBLE` for Phase-I/basic HARV pending point-level configuration audit.

### FA18-SRC-006 — Phase-II longitudinal TVC derivatives

- **Aircraft/configuration:** HARV Phase-II, three post-exit vanes per nozzle, specialized research FCS; no Phase-III ANSER authority.
- **Title:** *Flight-Determined Subsonic Longitudinal Stability and Control Derivatives of the F-18 High Angle of Attack Research Vehicle (HARV) with Thrust Vectoring*
- **Report:** NASA/TP-97-206539 / H-2175 / NTRS 19980007172.
- **Authors:** Kenneth W. Iliff; Kon-Sheng Charles Wang.
- **Organization:** NASA Dryden Flight Research Center; SPARTA, Inc.
- **Publication date:** 1997.
- **Repository:** NASA NTRS.
- **Stable URL:** https://ntrs.nasa.gov/citations/19980007172
- **PDF filename:** `19980007172.pdf`
- **SHA256:** `NOT_COMPUTED — PDF bytes not stored`.
- **Relevant anchors:** vehicle/control description; flights 156, 226, 250, 253; longitudinal derivative equations and plotted results; NASA-0 RFCS family used for the analyzed flights.
- **Data type:** flight-derived longitudinal stability/control derivatives including TVC effects.
- **Applicability:** direct Phase-II TVC authority.
- **Limitations:** TVC plume/vane interactions and research FCS make these data incompatible with Phase-I/basic F-18 as direct coefficients.
- **Classification:** `PRIMARY_EXACT` for the analyzed Phase-II TVC configuration.

### FA18-SRC-007 — Phase-II lateral-directional TVC derivatives

- **Aircraft/configuration:** HARV thrust-vectoring configuration used 1992–1994; LEX fence present; actuated forebody strakes absent.
- **Title:** *Flight-Determined, Subsonic, Lateral-Directional Stability and Control Derivatives of the Thrust-Vectoring F-18 HARV, and Comparisons to the Basic F-18 and Predicted Derivatives*
- **Report:** NASA/TP-1999-206573 / NTRS 19990019364.
- **Authors:** Kenneth W. Iliff; Kon-Sheng Charles Wang.
- **Organization:** NASA Dryden Flight Research Center.
- **Publication date:** January 1999.
- **Repository:** NASA NTRS.
- **Stable URL:** https://ntrs.nasa.gov/citations/19990019364
- **PDF filename:** `19990019364.pdf`
- **SHA256:** `NOT_COMPUTED — PDF bytes not stored`.
- **Relevant printed page:** searchable PDF text identifies vehicle description on report p. 8; 26 maneuvers on flights 155, 226, 248, 250, 274; alpha 10–70 deg in 10-deg increments; subsonic Mach approximately 0.52 down to 0.23 over the series.
- **Data type:** flight-determined lateral-directional derivatives and basic-F-18 comparison.
- **Applicability:** direct for Phase-II TVC state and valuable cross-validation against Phase-I/basic.
- **Limitations:** report itself notes differences between HARV/predicted/basic configurations and external changes; cannot backfill baseline coefficients indiscriminately.
- **Classification:** `PRIMARY_EXACT` for the stated TVC configuration.

### FA18-SRC-008 — HATP baseline aerodynamics cross-source overview

- **Aircraft/configuration:** baseline F/A-18 experimental aerodynamics program; includes scale models, full-scale wind tunnel, and HARV flight comparisons.
- **Title:** *Overview of HATP Experimental Aerodynamics Data for the Baseline F/A-18 Configuration*
- **Report:** NASA-TM-112360 / NTRS 19970012895.
- **Authors:** Robert M. Hall; Daniel G. Murri; Gary E. Erickson; David F. Fisher; Daniel W. Banks; Wendy R. Lanser et al.
- **Organization:** NASA Langley / Dryden / Ames.
- **Publication date:** 1996.
- **Repository:** NASA NTRS.
- **Stable URL:** https://ntrs.nasa.gov/citations/19970012895
- **PDF filename:** `19970012895.pdf`
- **SHA256:** `NOT_COMPUTED — PDF bytes not stored`.
- **Relevant anchors:** 0.06-scale Langley high-speed tunnel, 0.16-scale Langley 30x60, full-scale Ames 80x120 and HARV flight comparisons; Mach/Reynolds effects on forebody/LEX vortex aerodynamics.
- **Data type:** baseline static aerodynamic database lineage and validation overview.
- **Applicability:** critical source graph for tracing original baseline wind-tunnel databases.
- **Limitations:** overview/plots rather than one ready-to-import coefficient deck; geometry/configuration of each test article must remain tagged.
- **Classification:** `PRIMARY_FAMILY_ONLY` overall; individual explicitly baseline configurations may later be promoted field-by-field.

### FA18-SRC-009 — automated flight update of F/A-18 tables

- **Aircraft/configuration:** simplified F/A-18 wind-tunnel database updated with NASA HARV flight data.
- **Title:** *Automated Simulation Updates based on Flight Data*
- **Document:** AIAA 2007-6714 / NTRS 20070031030.
- **Authors:** Eugene A. Morelli; David G. Ward.
- **Organization:** NASA Langley Research Center; Barron Associates.
- **Publication date:** August 2007.
- **Repository:** NASA NTRS.
- **Stable URL:** https://ntrs.nasa.gov/citations/20070031030
- **PDF filename:** `20070031030.pdf`
- **SHA256:** `NOT_COMPUTED — PDF bytes not stored`.
- **Relevant anchors:** method description; aircraft geometry/mass-property Table 1; aerodynamic-table update/prediction cases.
- **Data type:** statistical flight-data update of lookup tables; demonstration of model correction without ad-hoc analyst tuning.
- **Applicability:** excellent methodology and potential compact public model seed.
- **Limitations:** starts from a *simplified* wind-tunnel database; Table-1 OCR has at least one suspicious unit/layout field for reference station, so no numeric value is frozen from OCR in R0; exact flight configuration must be identified before import.
- **Classification:** `PRIMARY_EXACT` for the documented update experiment; `PRIMARY_FAMILY_ONLY` for Phase-I/basic aircraft numbers until configuration is proven.

### FA18-SRC-010 — AAW modified F/A-18A aerodynamic model

- **Aircraft/configuration:** F/A-18A modified for Active Aeroelastic Wing research; reduced wing torsional stiffness and custom research control system.
- **Title:** *Active Aeroelastic Wing Aerodynamic Model Development and Validation for a Modified F/A-18A Airplane*
- **Report:** NASA/TM-2005-213688 / NTRS 20050237835.
- **Authors:** Stephen B. Cumming; Corey G. Diebler.
- **Organization:** NASA Dryden Flight Research Center.
- **Publication date:** November 2005.
- **Repository:** NASA NTRS.
- **Stable URL:** https://ntrs.nasa.gov/citations/20050237835
- **PDF filename:** `20050237835.pdf`
- **SHA256:** `NOT_COMPUTED — PDF bytes not stored`.
- **Relevant anchors:** phase-1 parameter estimation; model validation; phase-2 flight comparison; Mach 0.85–1.30 and q 600–1250 psf test envelope.
- **Data type:** flight-identified stability/control derivative model for modified AAW aircraft.
- **Applicability:** exact AAW research-state source and useful parameter-identification methodology.
- **Limitations:** wing structure/control system intentionally differ from baseline Hornet.
- **Classification:** `PRIMARY_EXACT` for AAW; `INCOMPATIBLE AS DIRECT BASELINE DATA` in compatibility matrix.

### FA18-SRC-011 — F-18B baseline support for AAW

- **Aircraft/configuration:** NASA F-18B Systems Research Aircraft used to obtain baseline derivatives for AAW work.
- **Title:** *Results From F-18B Stability and Control Parameter Estimation Flight Tests at High Dynamic Pressures*
- **Document:** NTRS 20010002099.
- **Organization:** NASA Dryden/Armstrong program.
- **Repository:** NASA NTRS.
- **Stable URL:** https://ntrs.nasa.gov/citations/20010002099
- **PDF filename:** NTRS 20010002099 download.
- **SHA256:** `NOT_COMPUTED — PDF bytes not stored`.
- **Relevant envelope:** Mach 0.85–1.30; q 600–1500 psf; automated single-surface doublets.
- **Data type:** flight-estimated baseline F-18B stability/control derivatives.
- **Applicability:** strong F/A-18 family cross-validation and possible compatible baseline source after exact SRA configuration audit.
- **Limitations:** not automatically the same configuration as HARV Phase I or an operational C/D.
- **Classification:** `PRIMARY_FAMILY_ONLY` pending exact geometry/FCS equivalence.

### FA18-SRC-012 — F/A-18E/F high-alpha dynamic model research

- **Aircraft/configuration:** F/A-18E/F Super Hornet 22%-dynamically-scaled drop model; separate aircraft family branch.
- **Title:** *Research on the F/A-18E/F Using a 22%-Dynamically-Scaled Drop Model*
- **Document:** AIAA 2000-3913 / NTRS 20000091591.
- **Authors:** M. Croom; H. Kenney; D. Murri; K. Lawson.
- **Organizations:** NASA Langley; Naval Air Systems Command.
- **Publication date:** 2000.
- **Repository:** NASA NTRS.
- **Stable URL:** https://ntrs.nasa.gov/citations/20000091591
- **PDF filename:** `20000091591.pdf`
- **SHA256:** `NOT_COMPUTED — PDF bytes not stored`.
- **Data type:** subsonic longitudinal response, departure/spin resistance, developed spins/recoveries, falling-leaf/cartwheel behavior, comparison to full-scale flight test.
- **Applicability:** exact research authority for F/A-18E/F high-alpha model/validation family.
- **Limitations:** cannot be transferred to legacy Hornet/HARV geometry or controls.
- **Classification:** `PRIMARY_EXACT` for its scale-model test article; `PRIMARY_FAMILY_ONLY` for full-scale E/F.

---

## 4. Known source leads not yet acquired

### FA18-LEAD-001 — HARV aerodynamic model memo

- **Citation:** Albion H. Bowers, *HARV Aerodynamic Model*, HARV project memo, NASA Dryden Flight Research Center, 22 January 1990.
- **Citation chain:** NASA-TM-110216 reference 3.0 -> Bowers memo.
- **Why critical:** likely the direct description of the Phase-I/HARV baseline aerodynamic database that later simulations inherited.
- **Status/classification:** `NOT_ACQUIRED`.

### FA18-LEAD-002 — McDonnell Douglas / DMS F/A-18 baseline data package

- **Citation chain:** NASA-TM-107601 states `f18bas` rigid-body aero and engine models were largely derived from Langley `dmsf18`, itself derived from McDonnell Douglas simulation/documents.
- **Why critical:** likely original source for exact lookup grids, reference geometry, control limits and possibly engine tables.
- **Status/classification:** `NOT_ACQUIRED`.

### FA18-LEAD-003 — McDonnell control/aero reports used by HARV actuator model

- **Identifiers exposed in NASA-TM-110216:** MDC reports A7813, A7247, A8450 are cited for hinge-moment/actuator behavior.
- **Why critical:** possible direct primary authority for no-load rate, load-dependent rate and surface-limit logic.
- **Status/classification:** `NOT_ACQUIRED`.

---

## 5. Catalog conclusion

Two different public-data strengths must not be conflated:

1. **Best documented simulation corpus:** `f18bas` / NASA-TM-107601, especially with its hypothetical TVC disabled, plus the later `f18harv` documentation.
2. **Best exact flight-identified airframe state:** NASA HARV BuNo 160780, Phase-I basic hardware/software configuration, supported by NASA-TM-4786 and early HATP flight-identification work.

For Maverick R0 source acquisition, the recommended physical reference candidate is **`NASA_F18_HARV_160780_PHASE1_BASIC`** because it has an exact airframe identity, a clean pre-TVC/pre-ANSER boundary, and flight-derived stability/control evidence. `f18bas` should be retained as the strongest simulation/data-lineage source, not silently relabeled as the exact flight vehicle.
