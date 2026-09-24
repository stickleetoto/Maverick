# F-16 Known Data (R1)

Canonical data: [`KNOWN_DATA.json`](KNOWN_DATA.json). The tables between the GENERATED markers are rendered from it by `Tools/build_aerospace_source_views.py`. Do not edit them by hand. All values also appear in [`../../AIRCRAFT_NUMERIC_FIELD_INDEX.json`](../../AIRCRAFT_NUMERIC_FIELD_INDEX.json).

> **No value on this page may be used in runtime code from this library.** None is `ALLOWED`. Most values come from page readings made elsewhere in the Maverick repository (`exactness = REPOSITORY_READING`, library level `SEARCH_LEAD_ONLY`). The library records those readings in `existing_maverick_analysis` but does not treat them as primary evidence. The repository's own implementation decisions (for example the main-branch `MavF16MorelliReference` constants) are governed by the repository's reference documents, not by this page.

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
<!-- END GENERATED: configurations -->

### Configuration traps (F-16)

1. **The Maverick reference F-16 is a simulation configuration.** TP-1538's mass (20,500 lb), geometry and Table VI thrust belong to `F16-CFG-NASA-REF-TP1538`. They are not the values of an F-16A or F-16C block.
2. **One airframe, three configurations.** The NF-16D VISTA flew as a variable-stability aircraft, then with the MATV nozzle (1993-1994), then later with other engines and as the X-62A. `AIAA-2018-0525` describes the VISTA state, not a production F-16D.
3. **F-16XL is a different aircraft.** It shares the name and, later, Block 40 flight-control computers. Its aerodynamics and control laws do not transfer.
4. **Textbook numbers look authoritative because they are everywhere.** Stevens & Lewis values (including actuator rates) appear in dozens of codes. They trace back to one textbook, and the rates have no identified primary source.
5. **Engine variant is part of the configuration.** F100-PW-200, -220, -229 and F110-GE-100, -129 are separate engines. TP-1538 Table VI names none of them.

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

### Aerodynamic-data domains and envelopes

| Value ID | Quantity | Value | Units | Precision | Config | Source | Location as recorded | Exactness | Level | Impl. use | Repo x-ref | Conflicts |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `F16-DB-ALPHA` | Morelli model alpha domain | **-10 .. +45** | deg | 1 deg | SIM-MORELLI1998 | MORELLI-ACC-1998-F16 | Table 1 (per repository) | REPOSITORY_READING | LEAD | DOMAIN_METADATA_ONLY | yes | — |
| `F16-DB-BETA` | Morelli model beta domain | **-30 .. +30** | deg | 1 deg | SIM-MORELLI1998 | MORELLI-ACC-1998-F16 | Table 1 (per repository) | REPOSITORY_READING | LEAD | DOMAIN_METADATA_ONLY | yes | — |
| `F16-DB-MACH` | Morelli model Mach applicability | **< 0.6** | - | 0.1 | SIM-MORELLI1998 | MORELLI-ACC-1998-F16 | Section 3 (per repository) | REPOSITORY_READING | LEAD | DOMAIN_METADATA_ONLY | yes | — |
| `F16-DB-LEF` | Leading-edge flap state represented by the Morelli base functions | **25** | deg (fixed) | 1 deg | SIM-MORELLI1998 | MORELLI-ACC-1998-F16 | Repository analysis of TP-1538 build-up equations + Morelli Table 1 | REPOSITORY_READING | LEAD | DOMAIN_METADATA_ONLY | yes | — |
| `F16-VISTA-DB-ALPHA` | Bihrle VISTA low-speed nonlinear model alpha domain | **-80 .. +90** | deg | 1 deg | VISTA-NF16D | DTIC-ADA327869 | Abstract | ABSTRACT_STATEMENT | ABS | DOMAIN_METADATA_ONLY | — | — |
| `F16-VISTA-DB-BETA` | Bihrle VISTA low-speed nonlinear model beta domain | **-30 .. +30** | deg | 1 deg | VISTA-NF16D | DTIC-ADA327869 | Abstract | ABSTRACT_STATEMENT | ABS | DOMAIN_METADATA_ONLY | — | — |

### Propulsion

| Value ID | Quantity | Value | Units | Precision | Config | Source | Location as recorded | Exactness | Level | Impl. use | Repo x-ref | Conflicts |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `F16-ENG-HENG` | Engine angular momentum (fixed) | **160** | slug-ft^2/s | 1 slug-ft^2/s (216.9 kg m^2/s printed) | NASA-REF-TP1538 | NASA-TP-1538 | Appendix B (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `F16-ENG-TABLE-VI` | Thrust values used in simulation (idle / military / maximum vs Mach a… | **TABLE: Mach 0.2-1.0 (5 columns) x altitude 0-15,240 m (6 rows) x 3 power levels; 90 SI cells** | N (printed SI and lbf) | printed integers | NASA-REF-TP1538 | NASA-TP-1538 | Table VI, PDF p.99/233, report p.93 (per repository manifest) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `F16-ENG-PC-LOW` | Power-command gearing below delta_th 0.77 | **Pc = 64.94 * delta_th** | percent | 0.01 | NASA-REF-TP1538 | NASA-TM-2003-212145 | Engine model section (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `F16-ENG-PC-HIGH` | Power-command gearing above delta_th 0.77 | **Pc = 217.38 * delta_th - 117.38** | percent | 0.01 | NASA-REF-TP1538 | NASA-TM-2003-212145 | Engine model section (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `F16-ENG-RTAU` | Reciprocal time constant of the power lag | **1.0 (dP <= 25); 0.1 (dP >= 50); 1.9 - 0.036 dP otherwise** | 1/s | as printed | NASA-REF-TP1538 | NASA-TM-2003-212145 | Engine model section (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `F16-MATV-VECTOR` | AVEN nozzle vector angle (MATV) | **17** | deg | 1 deg | VISTA-MATV | UNRESOLVED: NTRS-19950007831, AIAA-94-3513 | NOT_CAPTURED | SEARCH_EXTRACT | EXTRACT | PROHIBITED | — | — |

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
