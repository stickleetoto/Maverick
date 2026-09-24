# F-16 Known Data (R1, R2 update)

Canonical data: [`KNOWN_DATA.json`](KNOWN_DATA.json). The tables between the GENERATED markers are rendered from it by `Tools/build_aerospace_source_views.py`. Do not edit them by hand. All values also appear in [`../../AIRCRAFT_NUMERIC_FIELD_INDEX.json`](../../AIRCRAFT_NUMERIC_FIELD_INDEX.json).

> **R2: 34 values are `ALLOWED`, all for `F16-CFG-NESC-CHECKCASE`** (NASA's NESC check-case F-16, verified from the DAVE-ML files at file-line level). They support a validation profile that reproduces the NESC check-cases. **No value of any other F-16 configuration is `ALLOWED`.**
>
> For every other configuration: **no value on this page may be used in runtime code from this library.** Most values come from page readings made elsewhere in the Maverick repository (`exactness = REPOSITORY_READING`, library level `SEARCH_LEAD_ONLY`). The library records those readings in `existing_maverick_analysis` but does not treat them as primary evidence. The repository's own implementation decisions (for example the main-branch `MavF16MorelliReference` constants) are governed by the repository's reference documents, not by this page.

---

## 1. Configuration registry

Every source and every value is tagged with one or more of these IDs. "F-16" alone is never a configuration.

<!-- BEGIN GENERATED: configurations (Tools/build_aerospace_source_views.py; do not edit by hand) -->
| ID | Label | Airframe / serial | Engine | FCS | Research hardware | Dates | Scope | Lineage | Status |
|---|---|---|---|---|---|---|---|---|---|
| `F16-CFG-NASA-REF-TP1538` | NASA Langley TP-1538 simulation configuration (development-era F-16 with relaxed static stability) | F-16 (as simulated; wind-tunnel-derived aerodynamics) | Afterburning turbofan; engine model named in App. B not identified by variant in repository reading | F-16-type Nz/AoA-limiting CAS as simulated, plus Langley research modifications | none (simulation) | December 1979 report | SIM_ONLY | NASA Langley F-16 simulation lineage | SIMULATION_REFERENCE |
| `F16-CFG-SIM-MORELLI1998` | Morelli (1998) global polynomial fit to the TP-1538-lineage subsonic F-16 database | F-16 (16% scale tunnel database per repository) | n/a (aerodynamics only) | n/a | none | 1998 | TUNNEL | NASA Langley F-16 simulation lineage | SIMULATION_REFERENCE |
| `F16-CFG-SIM-STEVENS-LEWIS` | Stevens & Lewis textbook F-16 model and its descendants (NESC check-case F-16, simupy-flight, AeroBench) | F-16 (textbook reduction of TP-1538 data) | Textbook engine lag + thrust tables (lineage per Garza & Morelli / textbook) | None or textbook example laws | none | 1992 / 2003 / 2015 editions | SIM_ONLY | NASA Langley F-16 simulation lineage (textbook branch) | CROSS_VALIDATION_REFERENCE |
| `F16-CFG-YF16-DEV` | YF-16 / lightweight-fighter prototype and FSD development configurations (tunnel models) | YF-16 prototype / FSD F-16 tunnel models | n/a (tunnel) | n/a | none | 1970s | TUNNEL | General Dynamics / NASA Langley development tunnel programme | HISTORICAL_TUNNEL |
| `F16-CFG-GENERIC-ACADEMIC` | Academic re-implementations of the textbook/NASA F-16 model (theses, journal papers) | F-16 (derived) | varies | varies | none | varies | SIM_ONLY | NASA Langley F-16 simulation lineage (academic branch) | CROSS_VALIDATION_ONLY |
| `F16-CFG-PROD-AB` | Production F-16A/B family (Blocks 1-20) | F-16A/B | F100-PW-200 (F100-PW-220 on later/upgraded aircraft) | Analog FLCS (architecture described in a public manufacturer case study; gains NOT public) | none | 1978 onward | PROD_FAMILY | Production | REFERENCE_ONLY |
| `F16-CFG-PROD-CD` | Production F-16C/D family (Block 25 onward) | F-16C/D | F100-PW-200/-220/-229 or F110-GE-100/-129 depending on block | Analog (early blocks) / digital FLCS (Block 40 onward) - production gains NOT public | none | 1984 onward | PROD_FAMILY | Production | REFERENCE_ONLY |
| `F16-CFG-FALCON21` | Falcon 21 F-16 derivative (supersonic tunnel model) | F-16 derivative tunnel model | n/a | n/a | none | TP-3355 era | TUNNEL | NASA/GD derivative studies | SEPARATE_CONFIGURATION |
| `F16-CFG-F16XL` | F-16XL (cranked-arrow) research aircraft and models | F-16XL-1 (single seat) / F-16XL-2 (two seat) | F-16XL-2 with F110-GE-129 (per NASA TM-104326 abstract); XL-1 engine not recorded here | Research; later Block 40 DFLCCs (TP-2004-212046) | cranked-arrow wing; research gloves/instrumentation | 1980s-1990s | RESEARCH_MOD | F-16XL | SEPARATE_CONFIGURATION |
| `F16-CFG-AFTI` | AFTI/F-16 (Advanced Fighter Technology Integration) | AFTI/F-16 (modified F-16A FSD airframe; serial not recorded by the library) | n/a | Triplex digital flight control system (research) | research hardware varied by phase (not recorded by the library) | 1980s | RESEARCH_MOD | AFTI | SEPARATE_CONFIGURATION |
| `F16-CFG-VISTA-MATV` | VISTA NF-16D in the MATV (axisymmetric vectoring nozzle) configuration | NF-16D VISTA (Block 30 F-16D base) | F110-GE-100 with AVEN axisymmetric vectoring nozzle (17 deg) | Revised control laws for MATV; VSS | AVEN, nose chines | 1993-1994 (programme ended 1994) | RESEARCH_MOD | VISTA | SEPARATE_CONFIGURATION |
| `F16-CFG-VISTA-NF16D` | VISTA NF-16D variable-stability aircraft (later X-62A) | NF-16D VISTA (F-16D Block 30 base) | Later F100-PW-229 (per public histories) | Variable Stability System in parallel with F-16 control laws | none | 1992 onward | RESEARCH_MOD | VISTA | SEPARATE_CONFIGURATION |
| `ENG-F100-PW-200` | Pratt & Whitney F100-PW-200 (F-16A/B) | (engine) | F100-PW-200 | n/a | none | UNKNOWN | PROD_FAMILY | F100 | ENGINE_IDENTITY |
| `ENG-F100-PW-220` | Pratt & Whitney F100-PW-220 (F-15 and F-16) | (engine) | F100-PW-220 | n/a | none | UNKNOWN | PROD_FAMILY | F100 | ENGINE_IDENTITY |
| `ENG-F100-PW-229` | Pratt & Whitney F100-PW-229 | (engine) | F100-PW-229 | n/a | none | UNKNOWN | PROD_FAMILY | F100 | ENGINE_IDENTITY |
| `ENG-F110-GE-100` | General Electric F110-GE-100 | (engine) | F110-GE-100 | n/a | none | UNKNOWN | PROD_FAMILY | F110 | ENGINE_IDENTITY |
| `ENG-F110-GE-129` | General Electric F110-GE-129 | (engine) | F110-GE-129 | n/a | none | UNKNOWN | PROD_FAMILY | F110 | ENGINE_IDENTITY |
| `F16-CFG-NESC-CHECKCASE` | NESC 6-DOF check-case F-16 (DAVE-ML package, 2012-2013) | F-16 check-case simulation model (not an airframe or production block) | Steady-state net thrust tables (Stevens & Lewis 2nd ed.); no engine lag; no engine angular momentum | NESC LQR SAS and autopilot check-case controller (Jackson 2013); NOT an F-16 FCS; no actuator dynamics | none | Files 2012-2013 (aero Mod P 2013-10-21); redistributed by NASA simupy-flight (data accessed summer 2020) | SIM_ONLY | NASA Langley F-16 simulation lineage: TP-1538 data -> Stevens & Lewis -> Morelli MATLAB (Garza & Morelli) -> NESC DAVE-ML | VALIDATION_REFERENCE |
<!-- END GENERATED: configurations -->

### Configuration traps (F-16)

1. **The Maverick reference F-16 is a simulation configuration.** TP-1538's mass (20,500 lb), geometry and Table VI thrust belong to `F16-CFG-NASA-REF-TP1538`. They are not the values of an F-16A or F-16C block.
2. **One airframe, three configurations.** The NF-16D VISTA flew as a variable-stability aircraft, then with the MATV nozzle (1993-1994), then later with other engines and as the X-62A. `AIAA-2018-0525` describes the VISTA state, not a production F-16D.
3. **F-16XL is a different aircraft.** It shares the name and, later, Block 40 flight-control computers. Its aerodynamics and control laws do not transfer.
4. **Textbook numbers look authoritative because they are everywhere.** Stevens & Lewis values (including actuator rates) appear in dozens of codes. They trace back to one textbook, and the rates have no identified primary source.
5. **Engine variant is part of the configuration.** F100-PW-200, -220, -229 and F110-GE-100, -129 are separate engines. TP-1538 Table VI names none of them.
6. **R2: the NESC check-case F-16 is not Maverick's reference F-16.** Same mass, inertia and reference geometry, but a different aero model (Stevens & Lewis tables, not Morelli 1998), steady-state thrust with no lag, an LQR check-case controller and no actuators. Its test CG is 25 % MAC while the inertia file's default input is 35 %.

## 2. Candidate values

Legend: `REPOSITORY_READING` = value read in another Maverick document (see the JSON `existing_maverick_analysis`); `SEARCH_EXTRACT` = seen in a search-index extract; `ABSTRACT_STATEMENT` = stated in an abstract. Level: LEAD / CAT / ABS / EXTRACT / PAGE (see [`SCHEMA.md`](../../SCHEMA.md#2-verification-levels)).

<!-- BEGIN GENERATED: values (Tools/build_aerospace_source_views.py; do not edit by hand) -->
### Geometry and reference quantities

| Value ID | Quantity | Value | Units | Precision | Config | Source | Location as recorded | Exactness | Level | Impl. use | Repo x-ref | Conflicts |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `F16-GEO-S` | Reference wing area | **300** | ft^2 | printed 300 ft^2 (27.87 m^2) | NASA-REF-TP1538 | NASA-TP-1538 | Table I (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `F16-GEO-B` | Reference wing span | **30** | ft | printed 30 ft (9.144 m) | NASA-REF-TP1538 | NASA-TP-1538 | Table I (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `F16-GEO-CBAR` | Mean aerodynamic chord | **11.32** | ft | 0.01 ft (printed 3.45 m) | NASA-REF-TP1538 | NASA-TP-1538 | Table I (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `F16-CG-REF` | Aerodynamic moment reference CG | **0.35** | fraction of cbar | 0.01 cbar | NASA-REF-TP1538, SIM-MORELLI1998 | NASA-TP-1538 | Table I; Morelli nomenclature (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `F16-TN8176-SCALE` | Tunnel model scale in TN D-8176 | **0.15** | - | 0.01 | YF16-DEV | NASA-TN-D-8176 | Abstract | ABSTRACT_STATEMENT | ABS | NOT_A_MODEL_PARAMETER | — | — |
| `F16-NESC-S` | Reference wing area | **300** | ft^2 | as encoded ('300.') | NESC-CHECKCASE | NESC-F16-AERO-DML | F16_aero.dml line 380, variableDef varID=sref (referenceWingArea, out… | EXACT | **PAGE** | ALLOWED | — | — |
| `F16-NESC-B` | Reference wing span | **30** | ft | as encoded ('30.') | NESC-CHECKCASE | NESC-F16-AERO-DML | F16_aero.dml line 374, variableDef varID=bspan (referenceWingSpan) | EXACT | **PAGE** | ALLOWED | — | — |
| `F16-NESC-CBAR` | Reference chord | **11.32** | ft | 0.01 ft | NESC-CHECKCASE | NESC-F16-AERO-DML | F16_aero.dml line 368 varID=cbar; also F16_inertia.dml line 56 varID=… | EXACT | **PAGE** | ALLOWED | — | — |
| `F16-NESC-MRC` | Moment reference centre | **35** | percent MAC (+aft of MAC leading edge) | exact (35) | NESC-CHECKCASE | NESC-F16-INERTIA-DML | F16_inertia.dml line 141 varID=DXCG (description and calculation DXCG… | EXACT | **PAGE** | ALLOWED | — | — |

### Mass properties

| Value ID | Quantity | Value | Units | Precision | Config | Source | Location as recorded | Exactness | Level | Impl. use | Repo x-ref | Conflicts |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `F16-CG-DEMO` | CG of the Morelli demonstration manoeuvre | **0.25** | fraction of cbar | 0.01 cbar | SIM-MORELLI1998 | MORELLI-ACC-1998-F16 | Section 3 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `F16-MASS-W` | Weight | **20,500** | lbf (weight) | printed 20,500 lb / 91,188 N | NASA-REF-TP1538 | NASA-TP-1538 | Table I (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | F16-MASS-ALT-647 |
| `F16-MASS-ALT-647` | Alternative mass quoted for 'other NASA F-16 studies' | **647.2** | slug | 0.1 slug | GENERIC-ACADEMIC | UNRESOLVED: NASA-TM-2003-212145, STEVENS-LEWIS-ACS | NOT_CAPTURED | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | F16-MASS-W |
| `F16-MASS-IX` | Roll moment of inertia | **9,496** | slug-ft^2 | 1 slug-ft^2 | NASA-REF-TP1538 | NASA-TP-1538 | Table I (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `F16-MASS-IY` | Pitch moment of inertia | **55,814** | slug-ft^2 | 1 slug-ft^2 | NASA-REF-TP1538 | NASA-TP-1538 | Table I (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `F16-MASS-IZ` | Yaw moment of inertia | **63,100** | slug-ft^2 | 1 slug-ft^2 (printed to hundreds) | NASA-REF-TP1538 | NASA-TP-1538 | Table I (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `F16-MASS-IXZ` | Product of inertia | **982** | slug-ft^2 | 1 slug-ft^2 | NASA-REF-TP1538 | NASA-TP-1538 | Table I (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `F16-NESC-MASS` | Total mass | **637.1595** | slug | 0.0001 slug | NESC-CHECKCASE | NESC-F16-INERTIA-DML | F16_inertia.dml line 111 varID=XMASS ('Total mass of vehicle (20,500… | EXACT | **PAGE** | ALLOWED | — | — |
| `F16-NESC-CG` | CG position for the check cases | **25** | percent MAC (+aft) | exact (25.0) | NESC-CHECKCASE | NESC-F16-PACKAGE-README | README.html (NESC F-16 package, v6) section F16_inertia.dml ('input v… | EXACT | **PAGE** | ALLOWED | — | — |
| `F16-NESC-IXX` | Roll moment of inertia | **9496.0** | slug-ft^2 | as encoded | NESC-CHECKCASE | NESC-F16-INERTIA-DML | F16_inertia.dml line 63 varID=XIXX | EXACT | **PAGE** | ALLOWED | — | — |
| `F16-NESC-IYY` | Pitch moment of inertia | **55814.0** | slug-ft^2 | as encoded | NESC-CHECKCASE | NESC-F16-INERTIA-DML | F16_inertia.dml line 71 varID=XIYY | EXACT | **PAGE** | ALLOWED | — | — |
| `F16-NESC-IZZ` | Yaw moment of inertia | **63100.0** | slug-ft^2 | as encoded | NESC-CHECKCASE | NESC-F16-INERTIA-DML | F16_inertia.dml line 79 varID=XIZZ | EXACT | **PAGE** | ALLOWED | — | — |
| `F16-NESC-IXZ` | Product of inertia, X-Z plane | **982.0** | slug-ft^2 | as encoded | NESC-CHECKCASE | NESC-F16-INERTIA-DML | F16_inertia.dml line 87 varID=XIZX (bodyProductOfInertia_ZX); README.… | EXACT | **PAGE** | ALLOWED | — | — |
| `F16-NESC-IXY` | Product of inertia, X-Y plane | **0.0** | slug-ft^2 | exact | NESC-CHECKCASE | NESC-F16-INERTIA-DML | F16_inertia.dml line 95 varID=XIXY | EXACT | **PAGE** | ALLOWED | — | — |
| `F16-NESC-IYZ` | Product of inertia, Y-Z plane | **0.0** | slug-ft^2 | exact | NESC-CHECKCASE | NESC-F16-INERTIA-DML | F16_inertia.dml line 103 varID=XIYZ | EXACT | **PAGE** | ALLOWED | — | — |

### Aerodynamic model definition and tables

| Value ID | Quantity | Value | Units | Precision | Config | Source | Location as recorded | Exactness | Level | Impl. use | Repo x-ref | Conflicts |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `F16-NESC-AERO-DEF` | Output coefficient definitions | **CX +fwd, CY +right, CZ +down, Cl +RWD, Cm +ANU, Cn +ANR; body axes; about the MRC** | nd | n/a (definition) | NESC-CHECKCASE | NESC-F16-AERO-DML | F16_aero.dml lines 784-905 (variableDefs cx, cy, cz, cl, cm, cn); REA… | EXACT | **PAGE** | ALLOWED | — | — |
| `F16-NESC-BUILDUP` | Coefficient build-up equations | **cx = CX0 + cq2v*CXq; cy = CY0 + b2v*(CYp*p + CYr*r); cz = CZ1 + cq2v*CZq; cl = Cl0 + ClDA*dail + ClDR*drdr + b2v*(Clp*p + Clr*r); cm = Cm0 + cq2v*Cmq; cn = Cn0 + CnDA*dail + CnDR*drdr + b2v*(Cnp*p + Cnr*r)** | nd | n/a (equations) | NESC-CHECKCASE | NESC-F16-AERO-DML | F16_aero.dml calculation elements of cx, cy, cz, cl1, cl, cm, cn1, cn… | EXACT | **PAGE** | ALLOWED | — | — |
| `F16-NESC-RATE-NORM` | Rate normalisation | **b2v = bspan/(2*vt) multiplies p and r; cq2v = cbar*q/(2*vt); p, q, r in rad/s; vt in ft/s (minValue 0.1)** | nd | n/a (equations) | NESC-CHECKCASE | NESC-F16-AERO-DML | F16_aero.dml line 549 (b2v), line 562 (cq2v), line 292 (vt), lines 31… | EXACT | **PAGE** | ALLOWED | — | — |
| `F16-NESC-CTRL-DEF` | Control inputs to the aero model | **el deg +TED; ail deg +LWD, (right - left)/2; rdr deg +TEL; normalised del = el/25, dail = ail/20, drdr = rdr/30** | deg | n/a (definition) | NESC-CHECKCASE | NESC-F16-AERO-DML | F16_aero.dml lines 332-345 (el, ail, rdr), 390 (del), 405 (dail), 418… | EXACT | **PAGE** | ALLOWED | — | — |
| `F16-NESC-TBL-LONG` | Longitudinal static tables | **CX0(alpha 12 x el 5) CX_table; CZ0(alpha 12) CZ0_table with CZ1 = CZ0*(1 - (beta/rtd)^2) - 0.19*del; Cm0(alpha 12 x el 5) Cm0_table** | nd | as encoded (3 significant digits typica… | NESC-CHECKCASE | NESC-F16-AERO-DML | F16_aero.dml griddedTableDefs CX_table line 994, CZ0_table line 1026,… | EXACT | **PAGE** | ALLOWED | — | — |
| `F16-NESC-TBL-LAT` | Lateral-directional static terms | **CY0 = -0.02*beta + 0.021*dail + 0.086*drdr (beta in deg); Cl0(alpha 12 x /beta/ 7) and Cn0(alpha 12 x /beta/ 7), sign of beta applied** | nd | as encoded | NESC-CHECKCASE | NESC-F16-AERO-DML | F16_aero.dml CY0 line 441; Cl0_table line 1095; Cn0_table line 1131;… | EXACT | **PAGE** | ALLOWED | — | — |
| `F16-NESC-TBL-DAMP` | Damping derivative tables | **CXq, CYr, CYp, CZq, Clr, Clp, Cmq, Cnr, Cnp vs alpha (12 breakpoints)** | per unit normalised rate | as encoded | NESC-CHECKCASE | NESC-F16-AERO-DML | F16_aero.dml griddedTableDefs lines 1175, 1201, 1227, 1253, 1279, 130… | EXACT | **PAGE** | ALLOWED | — | — |
| `F16-NESC-TBL-CTRL` | Lateral-directional control-power tables | **ClDA, ClDR, CnDA, CnDR vs alpha (12) x beta (-30:10:30)** | per unit normalised deflection (file labels say per degree; see F16-NESC-BUILDUP) | as encoded | NESC-CHECKCASE | NESC-F16-AERO-DML | F16_aero.dml griddedTableDefs lines 1418, 1459, 1499, 1540; variableD… | EXACT | **PAGE** | ALLOWED | — | — |

### Controls, actuators and FCS

| Value ID | Quantity | Value | Units | Precision | Config | Source | Location as recorded | Exactness | Level | Impl. use | Repo x-ref | Conflicts |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `F16-ACT-DH-LIM` | Horizontal tail (symmetric) travel | **+/-25** | deg | 1 deg | NASA-REF-TP1538, SIM-MORELLI1998 | NASA-TP-1538 | Table I (per repository); Morelli Table 1 | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `F16-ACT-DA-LIM` | Aileron (flaperon) travel | **+/-21.5** | deg | 0.5 deg | NASA-REF-TP1538, SIM-MORELLI1998 | NASA-TP-1538 | Table I (per repository); Morelli Table 1 | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `F16-ACT-DR-LIM` | Rudder travel | **+/-30** | deg | 1 deg | NASA-REF-TP1538, SIM-MORELLI1998 | NASA-TP-1538 | Table I (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `F16-ACT-DD-LIM` | Differential tail travel, per surface | **+/-5.375** | deg | 0.001 deg | NASA-REF-TP1538 | NASA-TP-1538 | Table I (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `F16-ACT-SB-LIM` | Speed brake travel | **60** | deg | 1 deg | NASA-REF-TP1538 | NASA-TP-1538 | Table I (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `F16-ACT-RATE-TP1538` | Surface rate limits in the TP-1538 material | **NOT FOUND** | deg/s | UNKNOWN | NASA-REF-TP1538 | NASA-TP-1538 | Repository review of TP-1538 found no sourced rate limit; stabilator… | REPOSITORY_READING | LEAD | NOT_A_MODEL_PARAMETER | yes | F16-ACT-RATE-DH-SL, F16-ACT-RATE-DA-SL, F16-ACT-RATE-DR-SL, F16-ACT-TAU-SL |
| `F16-ACT-RATE-DH-SL` | Horizontal tail rate limit (textbook lineage) | **60** | deg/s | 1 deg/s | SIM-STEVENS-LEWIS | ARXIV-1907-11913 | NOT_CAPTURED | SEARCH_EXTRACT | EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | — | F16-ACT-RATE-TP1538 |
| `F16-ACT-RATE-DA-SL` | Aileron rate limit (textbook lineage) | **80** | deg/s | 1 deg/s | SIM-STEVENS-LEWIS | ARXIV-1907-11913 | NOT_CAPTURED | SEARCH_EXTRACT | EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | — | F16-ACT-RATE-TP1538 |
| `F16-ACT-RATE-DR-SL` | Rudder rate limit (textbook lineage) | **120** | deg/s | 1 deg/s | SIM-STEVENS-LEWIS | ARXIV-1907-11913 | NOT_CAPTURED | SEARCH_EXTRACT | EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | — | F16-ACT-RATE-TP1538 |
| `F16-ACT-TAU-SL` | First-order actuator time constant (textbook lineage) | **0.0495** | s | 0.0001 s (= 1/20.2 s) | SIM-STEVENS-LEWIS | ARXIV-1907-11913 | NOT_CAPTURED | SEARCH_EXTRACT | EXTRACT | BLOCKED_PENDING_PAGE_VERIFICATION | — | F16-ACT-RATE-TP1538 |
| `F16-FCS-ROLL-165` | Commanded roll rate limit in the simulated FCS | **about 165** | deg/s | approximate ('about') | NASA-REF-TP1538 | NASA-TP-1538 | Text (per repository) | REPOSITORY_READING | LEAD | NOT_A_MODEL_PARAMETER | yes | — |
| `F16-FCS-DFLCC-RATE` | Block 40 DFLCC frame rate on the F-16XL research installation | **64** | Hz | 1 Hz | F16XL | NASA-TP-2004-212046 | NOT_CAPTURED | SEARCH_EXTRACT | EXTRACT | NOT_A_MODEL_PARAMETER | — | — |
| `F16-NESC-MIXER` | Check-case pilot-to-surface mixer | **el = -25*stick; ail = -21.5*lat; rdr = -30*pedal + 0.008*ail; PWR = 100*throttle** | deg per unit input | as encoded | NESC-CHECKCASE | NESC-F16-CONTROL-DML | F16_control.dml calculation elements of el (line 1117), ail (1133), r… | EXACT | **PAGE** | ALLOWED | — | — |
| `F16-NESC-LQR` | Check-case LQR gain matrices | **longLQR (2x4: stick and throttle vs airspeed, alpha, q, theta) and latdLQR (2x4: lateral stick and pedal vs phi, beta, p, r)** | mixed (per kt, per deg, per rad/s) | as encoded (15 digits) | NESC-CHECKCASE | NESC-F16-CONTROL-DML | F16_control.dml variableDefs longLQR11 (line 574) through latdLQR24 (… | EXACT | **PAGE** | ALLOWED | — | — |
| `F16-NESC-ACTUATORS` | Actuators in the check-case model | **none: controller outputs are applied as surface deflections directly; no rate limits, no position limits beyond aero-table clamping** | - | n/a | NESC-CHECKCASE | NESC-F16-CONTROL-DML | F16_control.dml outputs el, ail, rdr (lines 1117-1160) feed F16_aero.… | EXACT | **PAGE** | NOT_A_MODEL_PARAMETER | — | — |

### Aerodynamic-data domains and envelopes

| Value ID | Quantity | Value | Units | Precision | Config | Source | Location as recorded | Exactness | Level | Impl. use | Repo x-ref | Conflicts |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `F16-DB-ALPHA` | Morelli model alpha domain | **-10 .. +45** | deg | 1 deg | SIM-MORELLI1998 | MORELLI-ACC-1998-F16 | Table 1 (per repository) | REPOSITORY_READING | LEAD | DOMAIN_METADATA_ONLY | yes | — |
| `F16-DB-BETA` | Morelli model beta domain | **-30 .. +30** | deg | 1 deg | SIM-MORELLI1998 | MORELLI-ACC-1998-F16 | Table 1 (per repository) | REPOSITORY_READING | LEAD | DOMAIN_METADATA_ONLY | yes | — |
| `F16-DB-MACH` | Morelli model Mach applicability | **< 0.6** | - | 0.1 | SIM-MORELLI1998 | MORELLI-ACC-1998-F16 | Section 3 (per repository) | REPOSITORY_READING | LEAD | DOMAIN_METADATA_ONLY | yes | — |
| `F16-DB-LEF` | Leading-edge flap state represented by the Morelli base functions | **25** | deg (fixed) | 1 deg | SIM-MORELLI1998 | MORELLI-ACC-1998-F16 | Repository analysis of TP-1538 build-up equations + Morelli Table 1 | REPOSITORY_READING | LEAD | DOMAIN_METADATA_ONLY | yes | — |
| `F16-VISTA-DB-ALPHA` | Bihrle VISTA low-speed nonlinear model alpha domain | **-80 .. +90** | deg | 1 deg | VISTA-NF16D | DTIC-ADA327869 | Abstract | ABSTRACT_STATEMENT | ABS | DOMAIN_METADATA_ONLY | — | — |
| `F16-VISTA-DB-BETA` | Bihrle VISTA low-speed nonlinear model beta domain | **-30 .. +30** | deg | 1 deg | VISTA-NF16D | DTIC-ADA327869 | Abstract | ABSTRACT_STATEMENT | ABS | DOMAIN_METADATA_ONLY | — | — |
| `F16-NESC-ALPHA` | Angle-of-attack domain | **-10 .. 45 (breakpoints -10:5:45)** | deg | exact breakpoints | NESC-CHECKCASE | NESC-F16-AERO-DML | F16_aero.dml breakpointDef ALPHA1 and every independentVarRef varID=a… | EXACT | **PAGE** | ALLOWED | — | — |
| `F16-NESC-BETA` | Sideslip domain | **/beta/ 0 .. 30 (breakpoints 0:5:30) for Cl0/Cn0 with the sign of beta applied; -30 .. 30 (breakpoints -30:10:30) for control-power tables** | deg | exact breakpoints | NESC-CHECKCASE | NESC-F16-AERO-DML | F16_aero.dml breakpointDefs BETA1, BETA2; lines 1088 and 1124 (absbet… | EXACT | **PAGE** | ALLOWED | — | — |
| `F16-NESC-ELEV-DOMAIN` | Elevator domain of the aero tables | **-24 .. 24 (breakpoints -24:12:24)** | deg | exact breakpoints | NESC-CHECKCASE | NESC-F16-AERO-DML | F16_aero.dml breakpointDef DE1 and independentVarRef varID=el lines 9… | EXACT | **PAGE** | ALLOWED | — | — |
| `F16-NESC-MACH` | Mach / flight-envelope statement | **No Mach input; envelope 'limited to the vicinity of 10,000 ft MSL and 287.8 knots equivalent airspeed (around Mach 0.5)'** | - | as stated | NESC-CHECKCASE | NESC-F16-PACKAGE-README | README.html (NESC F-16 package, v6) section 'Assumptions and Limitati… | EXACT | **PAGE** | DOMAIN_METADATA_ONLY | — | — |

### Propulsion

| Value ID | Quantity | Value | Units | Precision | Config | Source | Location as recorded | Exactness | Level | Impl. use | Repo x-ref | Conflicts |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `F16-ENG-HENG` | Engine angular momentum (fixed) | **160** | slug-ft^2/s | 1 slug-ft^2/s (216.9 kg m^2/s printed) | NASA-REF-TP1538 | NASA-TP-1538 | Appendix B (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `F16-ENG-TABLE-VI` | Thrust values used in simulation (idle / military / maximum vs Mach a… | **TABLE: Mach 0.2-1.0 (5 columns) x altitude 0-15,240 m (6 rows) x 3 power levels; 90 SI cells** | N (printed SI and lbf) | printed integers | NASA-REF-TP1538 | NASA-TP-1538 | Table VI, PDF p.99/233, report p.93 (per repository manifest) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `F16-ENG-PC-LOW` | Power-command gearing below delta_th 0.77 | **Pc = 64.94 * delta_th** | percent | 0.01 | NASA-REF-TP1538 | NASA-TM-2003-212145 | Engine model section (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `F16-ENG-PC-HIGH` | Power-command gearing above delta_th 0.77 | **Pc = 217.38 * delta_th - 117.38** | percent | 0.01 | NASA-REF-TP1538 | NASA-TM-2003-212145 | Engine model section (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `F16-ENG-RTAU` | Reciprocal time constant of the power lag | **1.0 (dP <= 25); 0.1 (dP >= 50); 1.9 - 0.036 dP otherwise** | 1/s | as printed | NASA-REF-TP1538 | NASA-TM-2003-212145 | Engine model section (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `F16-MATV-VECTOR` | AVEN nozzle vector angle (MATV) | **17** | deg | 1 deg | VISTA-MATV | UNRESOLVED: NTRS-19950007831, AIAA-94-3513 | NOT_CAPTURED | SEARCH_EXTRACT | EXTRACT | PROHIBITED | — | — |
| `F16-NESC-THRUST-TABLES` | Idle / military / maximum thrust tables | **T_IDLE, T_MIL, T_MAX on Mach 0:0.2:1.0 x altitude 0:10,000:50,000 ft (36 cells each)** | lbf | as encoded (lbf) | NESC-CHECKCASE | NESC-F16-PROP-DML | F16_prop.dml griddedTableDefs T_IDLE line 257, T_MIL line 281, T_MAX… | EXACT | **PAGE** | ALLOWED | — | — |
| `F16-NESC-PLA` | Power-lever semantics and interpolation | **PWR 0-100 %, MIL_PWR = 50; PWR < 50: T_IDLE + PWR*(T_MIL - T_IDLE)/50; PWR >= 50: T_MIL + (PWR - 50)*(T_MAX - T_MIL)/50** | percent | exact | NESC-CHECKCASE | NESC-F16-PROP-DML | F16_prop.dml line 44 (PWR), line 72 (MIL_PWR), line 107 (FEX piecewis… | EXACT | **PAGE** | ALLOWED | — | — |
| `F16-NESC-ENGINE-DYN` | Engine dynamics in the check-case model | **none: steady-state thrust only (no power lag, no engine angular momentum)** | - | n/a | NESC-CHECKCASE | NESC-F16-PROP-DML | F16_prop.dml FEX description line 107 ('Net steady-state thrust'); RE… | EXACT | **PAGE** | NOT_A_MODEL_PARAMETER | — | — |
| `F16-NESC-TMAX-M08-SL` | Maximum thrust, Mach 0.8, sea level | **28070.0** | lbf | as encoded | NESC-CHECKCASE | NESC-F16-PROP-DML | F16_prop.dml line 324 (MACH = 0.8 row, first value) | EXACT | **PAGE** | ALLOWED | — | F16-TP1538-TMAX-M08-SL |
| `F16-NESC-TMAX-M10-SL` | Maximum thrust, Mach 1.0, sea level | **28885.0** | lbf | as encoded | NESC-CHECKCASE | NESC-F16-PROP-DML | F16_prop.dml line 325 (MACH = 1.0 row, first value) | EXACT | **PAGE** | ALLOWED | — | F16-TP1538-TMAX-M10-SL |
| `F16-TP1538-TMAX-M08-SL` | Maximum thrust, Mach 0.8, sea level (TP-1538 Table VI) | **26,070** | lbf | printed integer; SI column 115,959 N | NASA-REF-TP1538 | NASA-TP-1538 | Table VI, PDF p.99, report p.93 (per repository transcription) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | F16-NESC-TMAX-M08-SL |
| `F16-TP1538-TMAX-M10-SL` | Maximum thrust, Mach 1.0, sea level (TP-1538 Table VI) | **28,886** | lbf | printed integer; SI column 128,485 N | NASA-REF-TP1538 | NASA-TP-1538 | Table VI, PDF p.99, report p.93 (per repository transcription) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | F16-NESC-TMAX-M10-SL |

### Trim and check-case validation data

| Value ID | Quantity | Value | Units | Precision | Config | Source | Location as recorded | Exactness | Level | Impl. use | Repo x-ref | Conflicts |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `F16-NESC-TRIM11-COND` | Case 11 trim condition | **altitude 10,013 ft MSL; true airspeed 565.6854 ft/s; CM 25.0 % MAC; wings level, un-accelerated, zero flight-path angle** | ft, ft/s, percent MAC | as printed | NESC-CHECKCASE | NESC-F16-PACKAGE-README | README.html (NESC F-16 package, v6) Table 11 (HTML lines 1841-1865) a… | EXACT | **PAGE** | ALLOWED | — | — |
| `F16-NESC-TRIM11-THETA` | Case 11 trimmed pitch attitude (= alpha) | **2.6538** | deg (+ANU) | 0.0001 deg (README); 16 digits (control… | NESC-CHECKCASE | NESC-F16-PACKAGE-README | README.html (NESC F-16 package, v6) Table 11 (HTML line 1870); F16_co… | EXACT | **PAGE** | ALLOWED | — | F16-NESC-CASE11-INIT |
| `F16-NESC-TRIM11-TAIL` | Case 11 trimmed horizontal tail | **-3.241** | deg (+TED) | 0.0001 deg | NESC-CHECKCASE | NESC-F16-PACKAGE-README | README.html (NESC F-16 package, v6) Table 11 (HTML line 1880) | EXACT | **PAGE** | ALLOWED | — | — |
| `F16-NESC-TRIM11-STICK` | Case 11 trimmed longitudinal stick | **0.1296382327486013** | fraction (+aft) | 16 digits | NESC-CHECKCASE | NESC-F16-CONTROL-DML | F16_control.dml line 231 trimmedPilotControl_long; README.html (NESC… | EXACT | **PAGE** | ALLOWED | — | — |
| `F16-NESC-TRIM11-THROTTLE` | Case 11 trimmed throttle / power-lever angle | **0.1390191130965607** | fraction (= 13.9019 % PLA) | 16 digits | NESC-CHECKCASE | NESC-F16-CONTROL-DML | F16_control.dml line 226 trimmedPilotControl_throttle; README.html (N… | EXACT | **PAGE** | ALLOWED | — | — |
| `F16-NESC-CASE11-INIT` | Case 11 initial pitch attitude in the three reference simulations | **2.64333 (sim 02), 2.63873 (sim 04), 2.63893 (sim 05)** | deg | as recorded | NESC-CHECKCASE | NESC-F16-CHECKCASE-TRAJECTORIES | Atmos_11_sim_02/04/05.csv, first data row, column eulerAngle_deg_Pitch | EXACT | **PAGE** | NOT_A_MODEL_PARAMETER | — | F16-NESC-TRIM11-THETA |
| `F16-NESC-CHECKCASES` | NESC F-16 trajectory check cases | **cases 11, 12, 13.1-13.4, 15, 16; simulations 02, 04, 05 (24 CSV files)** | - | as recorded | NESC-CHECKCASE | NESC-F16-CHECKCASE-TRAJECTORIES | NESC_data/Atmospheric_checkcases/Atmos_11 ... Atmos_16 (see SOURCE_FI… | EXACT | **PAGE** | NOT_A_MODEL_PARAMETER | — | — |
| `F16-NESC-STATIC-SHOTS` | DAVE-ML static check-shots | **16 aero shots (nominal, +/- sideslip, +/- p, q, r, +/- surfaces, skewed) and 9 propulsion shots** | - | as encoded | NESC-CHECKCASE | NESC-F16-AERO-DML | F16_aero.dml staticShot elements lines 1564-3919; F16_prop.dml static… | EXACT | **PAGE** | NOT_A_MODEL_PARAMETER | — | — |

### Accuracy statements

| Value ID | Quantity | Value | Units | Precision | Config | Source | Location as recorded | Exactness | Level | Impl. use | Repo x-ref | Conflicts |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `F16-VAL-MORELLI-ACC` | Polynomial model vs wind-tunnel database, simulated doublet | **< 10** | % difference | stated bound | SIM-MORELLI1998 | MORELLI-ACC-1998-F16 | Abstract | ABSTRACT_STATEMENT | ABS | NOT_A_MODEL_PARAMETER | — | — |
<!-- END GENERATED: values -->

## 3. Reading the candidates

- **Mass and inertia closure (verdict B).** TP-1538 Table I gives weight, Ix, Iy, Iz and Ixz for one simulation loading. That closes mass/inertia *for the simulation configuration only*, once page-verified. The 647.2-slug alternative (`F16-MASS-ALT-647`) has no named source and is never used.
- **Reference vs physical geometry.** TP-1538 Table I lists S, b and cbar. The repository reading does not say whether "span" is the physical tip-to-tip span or a reference span. The library records it as the reference span of the simulation (it is what normalises Cl and Cn in Morelli), and records the ambiguity. No physical F-16 span from a primary source is held.
- **Actuators.** Position limits are sourced (TP-1538 Table I, repository reading). Rates are not. The textbook rates are recorded with their lineage so they cannot be mistaken for TP-1538 data (conflict F16-C1).
- **Thrust.** Table VI is recorded as a table-valued field. The 90 cells live in the repository transcription (`Docs/Reference/Data/F16/TP1538/`). They are not duplicated here.

## 4. Public-recoverability matrix (F-16)

| Data area | Best public lineage | Configuration | Library status | Blocker |
|---|---|---|---|---|
| Six-axis coefficients, M < 0.6 | Morelli 1998 / TP-1538 | simulation | Candidate (repository page read) | Library page verification |
| Coefficients, M > 0.6 | none for the production shape | — | NOT LOCATED | No public source |
| Coefficients, alpha > 45 deg | `DTIC-ADA327869` (VISTA) | VISTA | Lead | Configuration and content |
| Mass / inertia | TP-1538 Table I | simulation | Candidate | Library page verification |
| Actuator position limits | TP-1538 Table I | simulation | Candidate | Library page verification |
| Actuator rates / dynamics | none primary | — | GAP | TP-1538 fig. 63; Droste & Walker |
| FCS gains | none public for production | — | NOT LOCATED | Proprietary |
| Thrust | TP-1538 Table VI | simulation | Candidate (repository L5-equivalent transcription) | Library page verification; engine identity |
| Engine dynamics | Garza & Morelli power lag | simulation | Candidate | Library page verification |
| Trim / trajectory validation | NESC check-cases | textbook | Available as implementation check | Not physical validation |
