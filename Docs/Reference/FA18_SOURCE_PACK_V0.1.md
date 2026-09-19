# F/A-18 Source Pack V0.1

Status: **CONSOLIDATED PUBLIC-SOURCE INDEX — RAW PDFS NOT VENDORED**

Target physical configuration remains:

`NASA_F18_HARV_160780_PHASE1_BASIC`

This pack consolidates the newly reviewed NASA/NTRS F/A-18 material with the existing Maverick F/A-18 reference chain. It is an index and provenance guide, not permission to mix values across different F-18 research vehicles.

Repository rule: official PDFs stay at the source. Maverick stores stable URLs, byte hashes for locally reviewed copies, configuration tags, extraction products, and compatibility notes.

## 1. Newly consolidated source set

| ID | Report / source | Primary use | Configuration authority | Numeric extraction status |
|---|---|---|---|---|
| FA18-SP-001 | NASA-TM-4786 — *Extraction of Lateral-Directional Stability and Control Derivatives for the Basic F-18 Aircraft at High Angles of Attack* | Flight-derived lateral/directional derivatives, AOA 3–47 deg | **PRIMARY_EXACT** for basic F-18 development aircraft 160780 in 1987–1988 basic configuration | Figures 8–11 require digitization |
| FA18-SP-002 | NASA-CR-3608 — *Low Speed Rotary Aerodynamics of F-18 Configuration for 0° to 90° Angle of Attack — Test Results and Analysis* | Rotary/high-alpha/spin aerodynamics | **PRIMARY_EXACT** for the 1/10-scale Langley rotary-balance model; **CROSS_VALIDATION** for full-scale HARV | Appendix contains tabulated measured data; high priority |
| FA18-SP-003 | NASA-TM-4772 — *An Overview of the NASA F-18 High Alpha Research Vehicle* | Phase I/II/III configuration boundaries; geometry/mass/inertia cross-check | **PRIMARY_EXACT** for stated HARV phase/configuration facts | Table 1 already suitable for reference-property cross-check |
| FA18-SP-004 | NASA-TM-4341 — *Aircraft Ground Test and Subscale Model Results of Axial Thrust Loss Caused by Thrust Vectoring Using Turning Vanes* | Phase-II TVC axial thrust-loss model | **PRIMARY_EXACT** for tested HARV TVC ground-test configuration | Tables 2–3 can be transcribed directly |
| FA18-SP-005 | Morelli & Ward — *Automated Simulation Updates based on Flight Data* | Reconstruction/update methodology for aerodynamic databases | **METHODOLOGY / RECONSTRUCTION_SUPPORT** only | Table/model coefficients are local demonstration data, not a full F/A-18 aero deck |
| FA18-SP-006 | NASA-CR-198052 — *Estimating Engine Airflow in Gas-Turbine Powered Aircraft with Clean and Distorted Inlet Flows* | F404 airflow estimation and inlet/engine coupling | **PRIMARY_EXACT** for documented HARV propulsion test state; compatible evidence for F404 installation behavior | Appendix C / correlations are extraction candidates |
| FA18-SP-007 | NTRS 19970012895 — *Overview of HATP Experimental Aerodynamics Data for the Baseline F/A-18 Configuration* | Source graph for tunnel/full-scale/flight comparisons; Reynolds/Mach/geometry uncertainty | **PRIMARY_OVERVIEW** | Use mainly to trace upstream experiments; selected comparison curves may be digitized if needed |
| FA18-SP-008 | NASA-TM-104329 — *Inlet Distortion for an F/A-18A Aircraft During Steady Aerodynamic Conditions up to 60° Angle of Attack* | Installed-inlet recovery/distortion database | **PRIMARY_EXACT** for the tested HARV inlet campaign | Tables 4–5 and pressure-location tables are direct extraction candidates |
| FA18-SP-009 | NASA/TP-2000-209033 — *Results From F-18B Stability and Control Parameter Estimation Flight Tests at High Dynamic Pressures* | Transonic/supersonic stability/control cross-validation, Mach 0.85–1.30 | **PRIMARY_EXACT** for the F-18B SRA; **FAMILY_CROSS_VALIDATION** for HARV Phase I | Appendices C/D contain tabulated derivative increments by Mach and altitude |

## 2. Canonical links and reviewed-file hashes

| ID | NTRS citation | Reviewed filename | SHA256 |
|---|---|---|---|
| FA18-SP-001 | https://ntrs.nasa.gov/citations/19970010502 | `19970010502.pdf` | `1651c86eb109cad8043cdd9970256a4cf338b9c98b9303602fcd8b58b60f5c9f` |
| FA18-SP-002 | https://ntrs.nasa.gov/citations/19870001403 | `19870001403.pdf` | `5d95e8b15654dafba13821a88947bdb6d5a0a444036e19cf5bfeee95f7585705` |
| FA18-SP-003 | https://ntrs.nasa.gov/citations/19970003009 | `19970003009.pdf` | `c043eba6429035ff2267b8be596b2d8fff51504db56d486bc90e339439e56919` |
| FA18-SP-004 | https://ntrs.nasa.gov/citations/19920007853 | `19920007853.pdf` | `409b1226e77e305eaa00f2ec2358aba7977c5546f18aebfe279fad95a2171c8b` |
| FA18-SP-005 | https://ntrs.nasa.gov/citations/20070031030 | `20070031030.pdf` | `5cfa6e311ca452730d425ceda0f179b8bdaf84de95ce84d013d59646d592e736` |
| FA18-SP-006 | https://ntrs.nasa.gov/citations/19970027377 | `19970027377.pdf` | `229d5880dd2805d6821b28baeb8abfed9b1fe47d54b35a2ba07c1bcd51f8c6eb` |
| FA18-SP-007 | https://ntrs.nasa.gov/citations/19970012895 | `19970012895.pdf` | `23d73248e33f8d16d70b7102cb20775fc5b40f9735e42370cc9cbc1f5c3aba9e` |
| FA18-SP-008 | https://ntrs.nasa.gov/citations/19970022131 | `19970022131.pdf` | `8c331f34123ac32d0c8afbc43f17abb38cf873a545fd16d289aaaada65901856` |
| FA18-SP-009 | https://ntrs.nasa.gov/citations/20010002099 | `20010002099.pdf` | `8963f512e2147bb07ded66a2970ef9a9cb837978aee96febbf06d980990bafdb` |

Duplicate local uploads of CR-3608 and TM-4341 were byte-identical to the canonical reviewed copies and are not recorded as separate sources.

## 3. Configuration notes

### Phase-I basic aircraft authority

NASA-TM-4786 is especially important because the testbed is development F-18 serial 160780, flown in the 1987–1988 basic hardware/software configuration, without the later LEX-fence and thrust-vectoring changes. For the current Maverick target this is stronger direct evidence than later Phase-II/III HARV PID sources for lateral-directional behavior.

NASA-TM-4772 remains the program-level authority for separating baseline Phase I, thrust-vectoring Phase II, and nose-strake Phase III. Never merge later research-system mass properties, control laws, or TVC effects into the Phase-I profile.

### Rotary / spin data

NASA-CR-3608 is a rich numeric source, but it is a 1/10-scale rotary-balance test. Treat its measured tables as exact for that test configuration and as high-value validation/extension evidence for full-scale high-alpha and spin behavior. Do not silently promote it into a full-scale static nonlinear coefficient deck.

### Transonic / supersonic F-18B SRA data

NASA/TP-2000-209033 is not the HARV 160780 airframe. It is the preproduction two-seat F-18B Systems Research Aircraft. Its left outboard wing panel also differs from the original preproduction panel. Use it as family-level transonic/supersonic validation and as an aeroelastic/control-surface measurement reference, not as direct Phase-I coefficients.

Its Appendices C and D are unusually useful because they contain tabulated **derivative increments** versus Mach and altitude. These are increments from the source simulation model to the flight-determined values, not a standalone absolute aerodynamic database.

### Propulsion / inlet

NASA-TM-104329 and NASA-CR-198052 strongly improve the installed-inlet and F404 airflow evidence. They do not provide the missing complete Phase-I Mach/altitude/PLA thrust deck.

NASA-TM-4341 is Phase-II TVC-specific. Its axial-thrust-loss data belongs in the TVC extension layer only.

### Database reconstruction

Morelli & Ward demonstrate a defensible method for combining approximate wind-tunnel aerodynamic tables with HARV flight data using local response surfaces, statistical weighting, and smooth blending. Maverick may use this as provenance for a future explicitly labeled reconstructed/approximate layer if the original full numeric F/A-18 tables remain unavailable. It must not be represented as recovered original A7247/A8575 data.

## 4. Extraction queue

Priority order for source-derived numeric ingestion:

1. **CR-3608 Appendix** — **IN PROGRESS**. A2–A32 cover Body beta=0/10 and the Body→Wing→LEX horizontal/vertical-tail buildup branches. A33–A39 now add the first complete **Basic F-18** block: beta=0 with delta_H/delta_a/delta_r/delta_d/delta_f all zero, 266 rows on the full printed 14-point spin grid at every alpha. Current raw total: **1,576 rows**. Derived component data: **908 rows**. Review Table II before selecting the next block.
2. **NASA/TP-2000-209033 Appendices C/D** — CPT/RVDT derivative increments by Mach/altitude, preserving interpolated/hold-last-value provenance.
3. **NASA-TM-104329 Tables 4/5** — steady inlet recovery/distortion database at Mach 0.3/0.4.
4. **NASA-TM-4341 Tables 2/3** — single/dual-vane axial thrust-loss comparison data.
5. **NASA-TM-4786 Figures 8–11** — digitized flight-derived lateral/directional derivative curves with figure-derived provenance.
6. **NASA-CR-198052 Appendix C / airflow correlations** — installed airflow-estimation support.
7. HATP overview figures only where a specific validation need exists.

Suggested file families under `Docs/Reference/Data/FA18/`:

- `CR3608_ROTARY_BODY_BETA0_V0.1.csv` — first verified tranche (A2–A6, 228 rows)
- `CR3608_ROTARY_BALANCE_*.csv` — subsequent configuration blocks
- `F18B_SRA_PID_CPT_INCREMENT_*.csv`
- `F18B_SRA_PID_RVDT_INCREMENT_*.csv`
- `FA18_HARV_INLET_STEADY_M03_V0.1.csv`
- `FA18_HARV_INLET_STEADY_M04_V0.1.csv`
- `FA18_HARV_TVC_AXIAL_THRUST_LOSS_V0.1.csv`
- `TM4786_FLIGHT_DERIVATIVES_DIGITIZED_V0.1.csv`

## 5. Remaining hard blockers

The consolidated sources materially strengthen high-alpha/rotary dynamics, flight-derived derivatives, transonic/supersonic family validation, installed-inlet behavior, F404 airflow estimation, and TVC loss modeling.

They still do **not** provide:

1. the full basic F/A-18 nonlinear aerodynamic numeric arrays used by the original simulation lineage;
2. the underlying MDC A7247/A8575 coefficient lookup package;
3. the complete F404 29-array numeric deck across idle/military/minimum-AB/maximum-AB and installation effects;
4. the exact Phase-I BuNo 160780 fuel-dependent mass/CG/inertia schedule.

Current physical target remains:

`NASA_F18_HARV_160780_PHASE1_BASIC`

Current provenance verdict:

**PARTIAL — STRONGLY IMPROVED PUBLIC VALIDATION COVERAGE; FULL NONLINEAR DECK STILL OPEN**
