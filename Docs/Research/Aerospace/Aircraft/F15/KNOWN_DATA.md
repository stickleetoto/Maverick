# F-15 Known Data (R1 consolidation)

Canonical data: [`KNOWN_DATA.json`](KNOWN_DATA.json). Tables between GENERATED markers are rendered by `Tools/build_aerospace_source_views.py`. All values also appear in [`../../AIRCRAFT_NUMERIC_FIELD_INDEX.json`](../../AIRCRAFT_NUMERIC_FIELD_INDEX.json).

> **Every value here was read by Maverick repository work, not by the library.** Each carries `exactness = REPOSITORY_READING` (or an abstract statement) and an `existing_maverick_analysis` entry naming the document, branch and locator. None is `ALLOWED`. The implementation branch's own frozen values are governed by that branch's documents. This page centralises them with provenance and conflicts. It does not re-freeze them.

---

## 1. Configuration registry

<!-- BEGIN GENERATED: configurations (Tools/build_aerospace_source_views.py; do not edit by hand) -->
| ID | Label | Airframe / serial | Engine | FCS | Research hardware | Dates | Scope | Lineage | Status |
|---|---|---|---|---|---|---|---|---|---|
| `F15-CFG-NASA836-PRE-QS` | NASA F-15B 836 (USAF 74-0141), pre-Quiet-Spike baseline, 2 x F100-PW-100 - the Maverick F-15 target | F-15B (production-representative two-seat; radar/gun removed for research); **NASA 836 / USAF 74-0141** | F100-PW-100 x2; sub-build (1)/(2)/(2-7/8)/(3) NOT established | Production-type mechanical + single-string analog CAS (simplified NASA block diagrams) | standard air-data boom + sideslip vane during baseline flights | baseline flights before Quiet Spike (Quiet Spike flew 2006-2007) | EXACT | NASA 836 | MAVERICK_TARGET_CONFIGURATION |
| `F15-CFG-NASA836-QS` | NASA F-15B 836 with the Gulfstream Quiet Spike telescoping nose boom | F-15B; **NASA 836 / 74-0141** | F100-PW-100 x2 | as baseline (no change reported) | 24-ft multisegmented telescoping nose boom | 2006-2007 | RESEARCH_MOD | NASA 836 | SEPARATE_CONFIGURATION |
| `F15-CFG-NASA836-PFTF` | NASA F-15B 836 carrying centerline research fixtures (FTF-II, PFTF, LIFT and similar) | F-15B; **NASA 836 / 74-0141** | F100-PW-100 x2 | as baseline | centerline fixture (varies by experiment) | 1990s-2010s | RESEARCH_MOD | NASA 836 | SEPARATE_CONFIGURATION |
| `F15-CFG-NASA836-POST2014` | NASA F-15B 836 after the 2014 re-engine | F-15B; **NASA 836 / 74-0141** | F100-PW-220E x2 (per 2016 briefing, repository) | digital engine control | none | 2014 onward | EXACT | NASA 836 | OUT_OF_SCOPE |
| `F15-CFG-NF15B-837` | NF-15B 837 (preproduction F-15B, highly modified: canards, vectoring nozzles; ACTIVE / IFCS) | NF-15B; **NASA 837** | F100-PW-229 x2 with axisymmetric thrust-vectoring nozzles | Research digital FCS (ACTIVE / IFCS) | canards, P/YBBN nozzles | 1990s-2000s | RESEARCH_MOD | NASA 837 | SEPARATE_CONFIGURATION |
| `F15-CFG-NASA835` | NASA 835 (F-15A) HIDEC / PCA research aircraft | F-15A; **NASA 835** | PW1128 (per repository, PCA era) | HIDEC / PCA research systems | none | 1980s-1990s | RESEARCH_MOD | NASA 835 | SEPARATE_CONFIGURATION |
| `F15-CFG-NASA-F15-EARLY-ENGINE-TESTBED` | Early NASA F-15 engine test bed flying prototype F100 engines (TP-1782 era) | F-15 (airframe identity not captured) | Prototype F100 series 2-7/8 (engine 059 left) | n/a | none | late 1970s | EXACT | NASA F-15 engine research | SEPARATE_CONFIGURATION |
| `F15-CFG-PREPROD-8` | Preproduction F-15 No. 8 (TM-72861) | F-15A preproduction; **F-15 No. 8** | F100 (build not recorded) | Production-type mechanical + CAS as installed | none | 1970s | PREPROD | Preproduction | CROSS_VALIDATION_ONLY |
| `F15-CFG-PROD-AD` | Production F-15A-D family | F-15A/B/C/D | F100-PW-100 / -220 by era | Production mechanical + analog CAS (gains NOT public) | none | 1974 onward | PROD_FAMILY | Production | REFERENCE_ONLY |
| `F15-CFG-SIM-AFIT-AEROBASE` | AFIT F-15 research simulations built on curve fits to the ASD 'F-15 Aerobase' / McAir ARO10 tables | F-15 (simulation) | fixed thrust or simple models (thesis-specific) | thesis-specific | none | 1987-1996 | SIM_ONLY | AFIT / ASD Aerobase | CROSS_VALIDATION_ONLY |
| `F15-CFG-SIM-CR186019` | Brumbaugh (1991) AIAA controls design challenge aircraft model | Generic high-performance fighter (F-15-like) | model-specific | none (design challenge) | none | 1991 | SIM_ONLY | NASA Dryden derived model | CROSS_VALIDATION_ONLY |
| `F15-CFG-RPV-3-8` | NASA 3/8-scale F-15 remotely piloted research vehicle (unpowered) | 3/8-scale F-15 RPV | none (blocked inlets) | remote digital augmentation | none | 1970s | TUNNEL | NASA F-15 RPV | CROSS_VALIDATION_ONLY |
| `F15-CFG-WT-SUBSCALE` | Subscale F-15 wind-tunnel models (0.075, 1/12 rotary, three-surface transonic, CFT) | F-15 tunnel models | n/a | n/a | none | 1970s-1980s | TUNNEL | NASA tunnel | CROSS_VALIDATION_ONLY |
| `F15-CFG-ENTHUSIAST` | Hobbyist F-15 flight models | F-15 (hobbyist) | n/a | n/a | none | UNKNOWN | SIM_ONLY | Hobbyist | PROHIBITED_AS_AUTHORITY |
| `ENG-F100-PW-100-1` | F100-PW-100(1) as simulated in TM X-3261 | (engine) | F100-PW-100(1) | n/a | none | UNKNOWN | SIM_ONLY | F100 | ENGINE_IDENTITY |
| `ENG-F100-PW-100-2-7-8` | Prototype F100 series 2-7/8 (P680059, P680063; 1977 altitude calibration) | (engine) | F100 series 2-7/8 | n/a | none | 1974-1980 | EXACT | F100 | ENGINE_IDENTITY |
| `ENG-F100-PW-100-3` | F100-PW-100(3) improved-fan build | (engine) | F100-PW-100(3) | n/a | none | UNKNOWN | PROD_FAMILY | F100 | ENGINE_IDENTITY |
| `ENG-F100-PW-100-3-DEEC` | P680063 in F100(3) gas-path configuration with DEEC (from 1980) | (engine) | F100(3) + DEEC | n/a | none | 1980-1985 | RESEARCH_MOD | F100 | ENGINE_IDENTITY |
| `ENG-F100-EMD` | F100 Engine Model Derivative | (engine) | F100 EMD | n/a | none | 1985 onward | RESEARCH_MOD | F100 | ENGINE_IDENTITY |
<!-- END GENERATED: configurations -->

### Configuration traps (F-15)

1. **Tail 836 is five configurations.** Pre-Quiet-Spike baseline (target), centreline fixtures, Quiet Spike, and post-2014 with F100-PW-220E engines. Table 1 of TM-2012-215978 prints more than one mass column; picking the wrong one is the documented F15-X1 conflict.
2. **The same serial is not the same engine.** F100 P680063 flew as PW-100(2), (2-7/8), F100(3) with DEEC, and EMD. Calibration data from 1977 are (2-7/8) data.
3. **Physical span is not reference span.** 42.8 ft is 836's physical span. The only reference spans located (42.7 ft for 837, 13.05 m for No. 8) belong to other configurations.
4. **The family triple 608 ft^2 / 15.94 ft / 42.8 ft is copied, not independent.**
5. **An F-15 simulation that looks complete (AFIT, CR-186019) is a curve fit at one condition, or an explicitly "not representative" benchmark.**

## 2. Candidate values

<!-- BEGIN GENERATED: values (Tools/build_aerospace_source_views.py; do not edit by hand) -->
### Geometry and reference quantities

| Value ID | Quantity | Value | Units | Precision | Config | Source | Location as recorded | Exactness | Level | Impl. use | Repo x-ref | Conflicts |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `F15-836-PHYS-SPAN` | Physical wingspan | **42.8** | ft | 0.1 ft | NASA836-PFTF, NASA836-PRE-QS | NASA-TM-4782 | p.10 (per repository); also TM-2005-213670 p.6, TM-2006-213674 p.12,… | REPOSITORY_READING | LEAD | NOT_A_MODEL_PARAMETER | yes | — |
| `F15-836-PHYS-LEN` | Overall length | **63.7** | ft | 0.1 ft | NASA836-PFTF, NASA836-PRE-QS | NASA-TM-4782 | p.10 (per repository) | REPOSITORY_READING | LEAD | NOT_A_MODEL_PARAMETER | yes | — |
| `F15-836-PHYS-HT` | Overall height | **18.7** | ft | 0.1 ft | NASA836-PFTF | NASA-TM-2005-213670 | p.6 (per repository) | REPOSITORY_READING | LEAD | NOT_A_MODEL_PARAMETER | yes | — |
| `F15-836-REF-GEOM` | 836 reference area / MAC / reference span | **NOT PUBLICLY LOCATED** | ft^2 / ft | UNKNOWN | NASA836-PRE-QS | NASA-TM-2009-214651 | Searched TM-2008-214634, TM-2009-214651, TM-2012-215978 and cited ref… | REPOSITORY_READING | LEAD | NOT_A_MODEL_PARAMETER | yes | — |
| `F15-837-REF-S` | NF-15B 837 reference area | **608** | ft^2 | 1 ft^2 | NF15B-837 | NASA-TM-2003-212027 | Table 1 p.8 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `F15-837-REF-B` | NF-15B 837 reference span | **42.7** | ft | 0.1 ft | NF15B-837 | NASA-TM-2003-212027 | Table 1 p.8 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | F15-837-PHYS-SPAN |
| `F15-837-PHYS-SPAN` | NF-15B 837 three-view span | **42.83** | ft | 0.01 ft | NF15B-837 | NASA-TM-2003-212027 | Three-view figure (per repository) | REPOSITORY_READING | LEAD | NOT_A_MODEL_PARAMETER | yes | F15-837-REF-B |
| `F15-837-REF-CBAR` | NF-15B 837 reference chord | **15.94** | ft | 0.01 ft | NF15B-837 | NASA-TM-2003-212027 | Table 1 p.8 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `F15-837-MRC` | NF-15B 837 moment reference | **FS 557.2 (= 25.66 % MAC)** | in / % MAC | 0.1 in | NF15B-837 | NASA-TM-2003-212027 | Table 1 p.8 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `F15-PP8-REF-S` | Preproduction No. 8 wing area | **56.61 m^2 (609.3 ft^2)** | m^2 | 0.01 m^2 | PREPROD-8 | NASA-TM-72861 | Table 1 p.15 / PDF p.17 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `F15-PP8-REF-CBAR` | Preproduction No. 8 MAC | **4.86** | m | 0.01 m | PREPROD-8 | NASA-TM-72861 | Table 1 p.15 / PDF p.17 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `F15-FAM-MAC-EQ` | McDonnell %MAC datum equation 'for all A through D models' | **%MAC = (FS - 508.1) / 191.33 x 100** | in | 0.01 in | PROD-AD | AFIT-GA-ENY-91D-1 | PDF p.14, p.55-56 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `F15-AFIT-REF` | AFIT research-profile reference geometry | **608 ft^2 / 42.8 ft / 15.94 ft** | ft^2 / ft / ft | as printed | SIM-AFIT-AEROBASE | AFIT-GAE-ENY-92M-01 | per repository research profile | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `F15-CR186019-REF` | CR-186019 reference geometry | **608.0 ft^2 / 42.8 ft / 15.95 ft** | ft^2 / ft / ft | as printed | SIM-CR186019 | NASA-CR-186019 | Table 1 p.4 / PDF p.8 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | F15-AFIT-REF |

### Mass properties

| Value ID | Quantity | Value | Units | Precision | Config | Source | Location as recorded | Exactness | Level | Impl. use | Repo x-ref | Conflicts |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `F15-836-MASS-W` | Gross weight, Baseline column, 8,000 lb fuel | **37,426** | lbf (weight) | 1 lb | NASA836-PRE-QS | NASA-TM-2012-215978 | Table 1 p.8, Baseline column (per implementation branch) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | F15-836QS-MASS-W |
| `F15-836-MASS-CG` | CG, Baseline column | **26.34** | % MAC | 0.01 % MAC | NASA836-PRE-QS | NASA-TM-2012-215978 | Table 1 p.8 (per implementation branch) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | F15-836QS-MASS-CG |
| `F15-836-MASS-IXX` | Roll inertia, Baseline column | **30,345** | slug-ft^2 | 1 | NASA836-PRE-QS | NASA-TM-2012-215978 | Table 1 p.8 (per implementation branch) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | F15-836QS-MASS-IXX |
| `F15-836-MASS-IYY` | Pitch inertia, Baseline column | **198,687** | slug-ft^2 | 1 | NASA836-PRE-QS | NASA-TM-2012-215978 | Table 1 p.8 (per implementation branch) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | F15-836QS-MASS-IYY |
| `F15-836-MASS-IZZ` | Yaw inertia, Baseline column | **223,214** | slug-ft^2 | 1 | NASA836-PRE-QS | NASA-TM-2012-215978 | Table 1 p.8 (per implementation branch) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | F15-836QS-MASS-IZZ |
| `F15-836-MASS-IXZ` | Product of inertia, Baseline column | **-5,070** | slug-ft^2 | 1 | NASA836-PRE-QS | NASA-TM-2012-215978 | Table 1 p.8 (per implementation branch) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | F15-836QS-MASS-IXZ |
| `F15-836QS-MASS-W` | Gross weight, spike-extended column (read as baseline on main) | **37,152** | lbf (weight) | 1 lb | NASA836-QS | NASA-TM-2012-215978 | Table 1 (column identity per implementation branch) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | F15-836-MASS-W |
| `F15-836QS-MASS-CG` | CG, spike-extended column | **26.05** | % MAC | 0.01 | NASA836-QS | NASA-TM-2012-215978 | Table 1 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | F15-836-MASS-CG |
| `F15-836QS-MASS-IXX` | Roll inertia, spike-extended column | **27,953** | slug-ft^2 | 1 | NASA836-QS | NASA-TM-2012-215978 | Table 1 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | F15-836-MASS-IXX |
| `F15-836QS-MASS-IYY` | Pitch inertia, spike-extended column | **190,777** | slug-ft^2 | 1 | NASA836-QS | NASA-TM-2012-215978 | Table 1 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | F15-836-MASS-IYY |
| `F15-836QS-MASS-IZZ` | Yaw inertia, spike-extended column | **213,957** | slug-ft^2 | 1 | NASA836-QS | NASA-TM-2012-215978 | Table 1 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | F15-836-MASS-IZZ |
| `F15-836QS-MASS-IXZ` | Product of inertia, spike-extended column | **-460** | slug-ft^2 | 1 | NASA836-QS | NASA-TM-2012-215978 | Table 1 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | F15-836-MASS-IXZ |
| `F15-836-BOW` | Basic operating weight | **27,500** | lbm | 100 lb | NASA836-PFTF | NASA-TM-4782 | p.10 (per repository) | REPOSITORY_READING | LEAD | NOT_A_MODEL_PARAMETER | yes | — |
| `F15-836-CG-FS` | PFTF analysis CG as station | **28 % MAC = FS 561.7** | % MAC / in | 0.1 in | NASA836-PFTF | NASA-TM-2001-210395 | AIAA 2001-3303 p.12 (per repository) | REPOSITORY_READING | LEAD | NOT_A_MODEL_PARAMETER | yes | — |
| `F15-AFIT-MASS` | Davison research mass state | **37,000** | lbf (weight) | 1,000 lb | SIM-AFIT-AEROBASE | AFIT-GAE-ENY-92M-01 | per repository research profile | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `F15-AFIT-INERTIA` | Davison research inertias Ixx / Iyy / Izz / Ixz | **25,480 / 166,620 / 186,930 / -1,000** | slug-ft^2 | 10 slug-ft^2 | SIM-AFIT-AEROBASE | AFIT-GAE-ENY-92M-01 | per repository research profile | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |

### Controls, actuators and FCS

| Value ID | Quantity | Value | Units | Precision | Config | Source | Location as recorded | Exactness | Level | Impl. use | Repo x-ref | Conflicts |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `F15-PP8-STAB-LIM` | Stabilator travel (preproduction No. 8) | **15 / -26** | deg | 1 deg | PREPROD-8 | NASA-TM-72861 | control-system appendix (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | F15-AFITM-STAB-LIM, F15-BAU-STAB-LIM, F15-CR186019-STAB-LIM |
| `F15-AFITM-STAB-LIM` | Stabilator travel (McDonnell AFIT thesis) | **29 down / 15 up** | deg | 1 deg | SIM-AFIT-AEROBASE | AFIT-GAE-ENY-90D-16 | PDF p.83-84 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | F15-PP8-STAB-LIM |
| `F15-BAU-STAB-LIM` | Stabilator travel (Baumann) | **+20 / -30** | deg | 1 deg | SIM-AFIT-AEROBASE | AFIT-GAE-ENY-89D-01 | PDF p.87 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | F15-PP8-STAB-LIM |
| `F15-CR186019-STAB-LIM` | Symmetric stabilator travel (CR-186019) | **+15 / -25** | deg | 1 deg | SIM-CR186019 | NASA-CR-186019 | Table 2 p.4 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | F15-PP8-STAB-LIM |
| `F15-CR186019-OTHER-LIM` | CR-186019 aileron / differential stabilator / rudder travel | **+/-20 / +/-20 / +/-30** | deg | 1 deg | SIM-CR186019 | NASA-CR-186019 | Table 2 p.4 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `F15-CR186019-RATE` | CR-186019 actuator rate (all surfaces) | **24** | deg/s | 1 deg/s | SIM-CR186019 | NASA-CR-186019 | p.4 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `F15-CR186019-ACT` | CR-186019 actuator transfer function | **20/(s+20)** | - | as printed | SIM-CR186019 | NASA-CR-186019 | p.4 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `F15-836-ACT-RATE` | 836 actuator rates / dynamics | **NOT PUBLICLY LOCATED** | deg/s | UNKNOWN | NASA836-PRE-QS | NASA-TM-2012-215978 | block diagrams print no rate or limit (repository) | REPOSITORY_READING | LEAD | NOT_A_MODEL_PARAMETER | yes | — |
| `F15-PP8-DIFF-RATIO` | Differential-tail ratio (preproduction No. 8) | **0.3** | - | 0.1 | PREPROD-8 | NASA-TM-72861 | control-system appendix (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `F15-PP8-PEDAL-GEAR` | Pedal gearing (preproduction No. 8) | **1.8** | deg/cm | 0.1 | PREPROD-8 | NASA-TM-72861 | control-system appendix (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `F15-836-ARI-SW-FIG` | Mechanical ARI inputs switched to zero (figure reading) | **Mach > 1.5** | Mach | 0.1 | NASA836-PRE-QS | NASA-TM-2012-215978 | Fig. 5, PDF p.23, printed p.19 (per implementation branch); same in T… | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | F15-836-ARI-SW-TXT |
| `F15-836-XFEED-SW-FIG` | Roll-yaw crossfeed AoA input switched to zero (figure reading) | **Mach > 1.0** | Mach | 0.1 | NASA836-PRE-QS | NASA-TM-2012-215978 | Fig. 5 (per implementation branch) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | F15-836-XFEED-SW-TXT |
| `F15-836-ARI-SW-TXT` | ARI operative boundary (main-branch reading) | **operative below Mach 1.0** | Mach | 0.1 | NASA836-PRE-QS | NASA-TM-2012-215978 | Section 'The Flight Control System', text associated with Fig. 5 (per… | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | F15-836-ARI-SW-FIG |
| `F15-836-XFEED-SW-TXT` | Roll-to-yaw crossfeed upper boundary (main-branch reading) | **nullified above Mach 1.5** | Mach | 0.1 | NASA836-PRE-QS | NASA-TM-2012-215978 | same locator (per main-branch docs) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | F15-836-XFEED-SW-FIG |

### Propulsion

| Value ID | Quantity | Value | Units | Precision | Config | Source | Location as recorded | Exactness | Level | Impl. use | Repo x-ref | Conflicts |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `F15-836-THRUST-SLS` | F100-PW-100 uninstalled SLS thrust, full afterburner (per engine, app… | **23,500** | lbf | 'approximately', 500 lbf | NASA836-PRE-QS, NASA836-PFTF | NASA-TM-2005-213670 | aircraft description (per repository) | REPOSITORY_READING | LEAD | PROHIBITED | yes | F15-836-THRUST-25K |
| `F15-836-THRUST-25K` | Conflicting thrust statement '25,000 lbf (91,188 N)' | **25,000 lbf / 91,188 N (internally inconsistent)** | lbf / N | internally inconsistent | NASA836-PFTF | NASA-TM-2001-210395 | aircraft description (per repository); repeated in TM-2002-210736 | REPOSITORY_READING | LEAD | PROHIBITED | yes | F15-836-THRUST-SLS |
| `F15-836-THRUST-PW220E` | Post-2014 836 engines thrust class | **24,000 lb class** | lbf | class | NASA836-POST2014 | NTRS-20160006705 | briefing (per repository) | REPOSITORY_READING | LEAD | PROHIBITED | yes | — |
| `F15-F100-PLA-MIL-73` | Military power PLA (F100-PW-100(1) simulation) | **73** | deg PLA | 1 deg | ENG-F100-PW-100-1 | NASA-TM-X-3261 | printed p.6 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | F15-F100-PLA-83 |
| `F15-F100-PLA-83` | Top of non-augmented operation PLA (F100-PW-100(3) simulation) | **83** | deg PLA | 1 deg | ENG-F100-PW-100-3 | NASA-TP-1034 | printed p.11 / fig. 17(a) (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | F15-F100-PLA-MIL-73 |
| `F15-F100-NET-CURVES` | Net thrust vs PLA, 7 flight conditions, as fraction of an UNSTATED de… | **63 digitized points (repository)** | fraction | digitized | ENG-F100-PW-100-3 | NASA-TP-1034 | Fig. 17, printed pp.64-65 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |
| `F15-F100-CH-SCALE` | TP-1034 net-thrust output channel full scale | **30,000** | lbf | as printed | ENG-F100-PW-100-3 | NASA-TP-1034 | Appendix C, printed p.28 (per repository) | REPOSITORY_READING | LEAD | PROHIBITED | yes | — |
| `F15-F100-AXIS-111` | TP-1373 gross-thrust axis normalizer | **111.2** | kN | 0.1 kN | ENG-F100-PW-100-2-7-8 | NASA-TP-1373 | Figs. 6(b), 6(d) (per repository) | REPOSITORY_READING | LEAD | PROHIBITED | yes | — |
| `F15-F100-WA-DESIGN` | Design corrected airflow (series 2-7/8) | **98.4** | kg/s | 0.1 kg/s | ENG-F100-PW-100-2-7-8 | NASA-TP-1373 | Figs. 6(a), 6(c); p.12 (per repository) | REPOSITORY_READING | LEAD | BLOCKED_PENDING_PAGE_VERIFICATION | yes | — |

### Accuracy statements

| Value ID | Quantity | Value | Units | Precision | Config | Source | Location as recorded | Exactness | Level | Impl. use | Repo x-ref | Conflicts |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `F15-F100-ACC-WA` | TP-1373 calculated vs measured airflow accuracy (2 sigma) | **1.24** | % | 0.01 % | ENG-F100-PW-100-2-7-8 | NASA-TP-1373 | Abstract | ABSTRACT_STATEMENT | ABS | NOT_A_MODEL_PARAMETER | — | — |
| `F15-F100-ACC-FG` | TP-1373 calculated vs measured gross thrust accuracy (2 sigma) | **2.38** | % | 0.01 % | ENG-F100-PW-100-2-7-8 | NASA-TP-1373 | Abstract | ABSTRACT_STATEMENT | ABS | NOT_A_MODEL_PARAMETER | — | — |
| `F15-F100-SGTM` | Simplified vs gas-generator gross-thrust method agreement in flight | **+/-3** | % | 1 % | ENG-F100-PW-100-2-7-8 | NASA-TP-1782 | printed pp.1, 17 (per repository) | REPOSITORY_READING | LEAD | NOT_A_MODEL_PARAMETER | yes | — |
<!-- END GENERATED: values -->

## 3. Reading the candidates

- **Mass/inertia closure for the target** exists (Baseline column, 8,000 lb fuel) per the implementation branch. The library cannot release it until the page is read by the library, which would also settle F15-X1 at library level.
- **ARI and crossfeed switches** (F15-X2): both readings are kept, tagged `-FIG` (implementation branch, rendered figure) and `-TXT` (main, text). Neither is released.
- **Thrust**: every thrust figure for 836 is `PROHIBITED` for modelling. They are aircraft-description statements, not decks.
- **Gap records** (`F15-836-REF-GEOM`, `F15-836-ACT-RATE`) are deliberate. They record that a search happened and failed, so the same search is not repeated blindly.
