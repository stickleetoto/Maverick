# F-16 Model Lineage (R1, R2 update)

Which F-16 "model" is which, where its numbers came from, and what each one may be used for. Parent: [`SOURCE_GRAPH.md`](SOURCE_GRAPH.md).

The library has read none of these PDFs itself. A row marked *repository* relies on page readings recorded under `Assets/MaverickFresh/Scripts/FlightDynamics/F16/` and `Docs/Reference/F16_*` on the main-derived branch. Those readings are cited in each source's `existing_maverick_analysis`. They are not treated as primary evidence.

---

## 1. Lineage table

| Model | Source ID | Configuration ID | Provenance | Built from | Domain | Library level | Repository reading | Use in Maverick |
|---|---|---|---|---|---|---|---|---|
| GD / NASA development tunnel data | `NASA-CR-3053-V1`, `NASA-CR-3053-V2`, `NTRS-19790013829` | `F16-CFG-YF16-DEV` | ORIGINAL_PRIMARY | YF-16/F-16 tunnel models (strakes, forebody) | Low speed, high alpha (per abstract) | ABS / CAT | none | Background for the strake/forebody contribution; not a coefficient source |
| Prototype automatic-control simulation | `NASA-TN-D-8176` | `F16-CFG-YF16-DEV` | ORIGINAL_PRIMARY | 0.15-scale model data, low Re / low M | Prototype configuration | ABS | none | Architecture history (AoA limiter, Nz limiter, ARI, yaw damper) |
| **TP-1538 simulation** | `NASA-TP-1538` | `F16-CFG-NASA-REF-TP1538` | ORIGINAL_PRIMARY (simulation-defined) | F-16 wind-tunnel testing (abstract); 16 %-scale model per repository reading of Morelli | alpha to post-stall; low speed | ABS | Table I and App. B page read; Table VI page-transcribed and cross-checked | Mass, geometry and thrust authority *in the repository*. Library: `BLOCKED_PENDING_PAGE_VERIFICATION` |
| **Morelli global polynomial** | `MORELLI-ACC-1998-F16` | `F16-CFG-SIM-MORELLI1998` | CURVE_FIT | TP-1538 database (cited as ref. 3, repository) | M < 0.6; alpha -10..45; beta +/-30; LEF fixed 25 deg | ABS | Tables 1-3, Eqs. 12-18 page read | Implemented aero model on main. Library: blocked |
| Garza & Morelli MATLAB collection | `NASA-TM-2003-212145` | `F16-CFG-NASA-REF-TP1538`, `F16-CFG-SIM-MORELLI1998` | DERIVED_SIMULATOR | TP-1538 mass set; Morelli polynomials; engine lag | as parents | ABS | Table 1 and engine-model section page read | Engine power-lag structure (Pc gearing, RTAU) in the repository |
| Garza & Morelli software package | `NASA-SW-LAR-17463-1` | `F16-CFG-NASA-REF-TP1538` | DERIVED_SIMULATOR | as TM | — | LEAD | release-status note | **PROHIBITED** (U.S.-release-only). Use the public TM text instead |
| Stevens & Lewis textbook model | `STEVENS-LEWIS-ACS` | `F16-CFG-SIM-STEVENS-LEWIS` | DERIVED_SIMULATOR | TP-1538 data reduced for a textbook | textbook alpha/beta grid | CAT | none | Cross-validation only |
| NESC check-case F-16 report | `NASA-TM-2015-218675`, `NESC-AIAA-2013-5071` | `F16-CFG-NESC-CHECKCASE` | ORIGINAL_PRIMARY (as a check-case) | — | case-specific | ABS / CAT | none | Report PDF still unread |
| **NESC check-case F-16 model files (R2)** | `NESC-F16-AERO-DML`, `-PROP-DML`, `-INERTIA-DML`, `-CONTROL-DML`, `-GNC-DML`, `-PACKAGE-README`, `-CHECKCASE-TRAJECTORIES` | `F16-CFG-NESC-CHECKCASE` | ORIGINAL_PRIMARY | **Verified lineage (file header and README):** TP-1538 data → Stevens & Lewis (1992 aero; 2003 prop and inertia) → Morelli MATLAB `f16_aero.m` (1995) / Garza & Morelli TM → DAVE-ML (Jackson, NASA LaRC, 2003-2013) | alpha -10..45, beta +/-30, el +/-24 (clamped); no Mach | **PAGE (file)** | none | **34 values implementation-allowed** for a NESC validation profile. Not the Morelli 1998 model |
| simupy-flight | `NASA-SIMUPY-FLIGHT` | `F16-CFG-SIM-STEVENS-LEWIS` | CROSS_VALIDATION_ONLY | NESC check-cases | case-specific | ABS | none | Independent implementation of the check-cases with regression data |
| AeroBench / AeroBenchVV | `ARCH-2018-HEIDLAUF` | `F16-CFG-SIM-STEVENS-LEWIS` | CROSS_VALIDATION_ONLY | Stevens, Lewis & Johnson (2015) | textbook | ABS | external-validation-only policy | Behavioural oracle only. **No code, tables or coefficients copied** |
| Academic re-implementations | `RUSSELL-UMN-2003-F16`, `ISRLAB-F16-MODEL-MATLAB`, `EVANGELOU-AERJ-2001`, `AFIT-GGC-EE-77-7`, `ARXIV-1907-11913` | `F16-CFG-SIM-STEVENS-LEWIS` / `-GENERIC-ACADEMIC` | DERIVED / XVAL | textbook | textbook | CAT / EXTRACT / LEAD | none | Cross-validation only |
| JSBSim F-16 | `JSBSIM-F16-MODEL` | `F16-CFG-GENERIC-ACADEMIC` | XVAL | third-party | — | LEAD | project policy | **PROHIBITED by Maverick policy** |
| VISTA low-speed nonlinear model | `DTIC-ADA327869` | `F16-CFG-VISTA-NF16D` | DERIVED_SIMULATOR | NASA Langley low-speed data sets (abstract) | alpha -80..+90, beta +/-30, full nonlinear control/sideslip interactions | ABS | none | **Strongest high-alpha lead**, but for VISTA, and the numeric content is unseen |
| VISTA flight-identified model | `AIAA-2018-0525` | `F16-CFG-VISTA-NF16D` | ORIGINAL_PRIMARY | Calspan NF-16D flight data, stitched models | "full envelope" (abstract) | ABS | none | Validation or research profile for VISTA only |
| AFTI flight-derived derivatives | `AIAA-84-2085` | `F16-CFG-AFTI` | ORIGINAL_PRIMARY | AFTI flight data; compared with tunnel and F-16A flight values | test points | ABS | pack entry | Validation of trends; AFTI only |
| F-16XL family | `NASA-TM-85776`, `NASA-TM-97-206276`, `NASA-TM-1999-209703`, `NTRS-20040110755`, `AIAA-2000-3910` | `F16-CFG-F16XL` | ORIGINAL_PRIMARY | F-16XL models | — | ABS | pack entries | **Different aircraft.** Method value only (unsteady aerodynamics architecture) |
| Falcon 21 | `NASA-TP-3355` | `F16-CFG-FALCON21`, `F16-CFG-PROD-CD` | ORIGINAL_PRIMARY | Langley UPWT, M 1.60-2.16 | supersonic | ABS | none | Only F-16C comparison curves near supersonic; derivative planform |

## 2. Build-up conventions that travel with the data

These come from the repository's reading of TP-1538 and Morelli. They are recorded because a wrong convention breaks a correct table.

| Item | Statement | Where (repository reading) |
|---|---|---|
| Axes | Body axes X forward, Y right, Z down | TP-1538 nomenclature |
| LEF | TP-1538 tables carry `lef` increments for retraction from 25 deg to 0 deg. At 25 deg the increment vanishes, so Morelli's base functions **are** the LEF-deployed state | TP-1538 nomenclature; Morelli Table 1 |
| Moment reference | xcg_ref = 0.35 cbar. The CG correction is applied to Cm without cbar/b, and to Cn with cbar/b. The two differ on purpose | Morelli Eqs. 16-17 |
| Rate normalisation | p-hat = pb/2V, q-hat = qcbar/2V, r-hat = rb/2V | Morelli Eq. 18 |
| alpha-dot | Folded into the q terms by the way the data were collected | Morelli section 3 |
| Mach | No Mach term. The model is Mach-independent inside its band and invalid above about 0.6 | Morelli section 3 |

## 2a. R2: two F-16 aero models, not one

Maverick's implemented reference aero is the **Morelli 1998 global polynomial** (`F16-CFG-SIM-MORELLI1998`). The NESC check-case aero is the **Stevens & Lewis table model** as adapted by Morelli in 1995 MATLAB scripts (`F16-CFG-NESC-CHECKCASE`). Both trace to TP-1538 data, but they are different models with different functional forms. Consequences:

- NESC trajectories can verify Maverick's EOM, atmosphere and integration **only if Maverick runs the NESC table model** as a separate validation profile.
- Agreement between Morelli-model Maverick and NESC trajectories would be a coincidence-grade comparison, not a check of either.
- The NESC control-power derivatives are labelled "per degree" in the file but are multiplied by normalised deflections (aileron/20, rudder/30). Implement the calculation elements, not the labels (`F16-NESC-BUILDUP`).

## 3. What would add a new lineage (not a copy of TP-1538)

1. **Production F-16 aerodynamics.** Not located. Candidates: none public. `AIAA-2013-0972` (SEEK EAGLE CFD) is a lead for F-16C derivative *comparisons* and is unassessed.
2. **Transonic F-16.** Not located for the production shape. `NASA-TP-3355` gives F-16C comparison curves supersonically.
3. **High alpha beyond 45 deg.** `DTIC-ADA327869` (VISTA, to +90 deg) is the only lead, and it belongs to the VISTA configuration.
4. **Flight-identified F-16.** `AIAA-2018-0525` (VISTA/X-62A) and `AIAA-84-2085` (AFTI). Each is configuration-specific.
