# Page Verification R2: log and outcome

Library revision **R2** (2026-09-24). Start commit `8926fe5`. Research and documentation only.

## 1. What R2 set out to do

Upgrade the most implementation-critical sources from search/catalogue level to `PAGE_VERIFIED`, in priority order: F-16, F/A-18, F-15, selected fundamentals. Promote numeric fields only when the page (or file element), configuration, units, convention and meaning are all known.

## 2. What was possible in this session

**The PDF hosts are still blocked by the session's egress policy.** Every attempt returned a proxy policy denial (HTTP 403 on CONNECT), recorded in [`SOURCE_FILES_MANIFEST.json`](SOURCE_FILES_MANIFEST.json) under `blocked_hosts_r2`: `ntrs.nasa.gov`, `apps.dtic.mil`, `archive.org`, `scholar.archive.org`, `zenodo.org`, `arxiv.org`, `www.nasa.gov`, `nescacademy.nasa.gov`, `techreports.larc.nasa.gov`, `www.cs.odu.edu` (LTRS mirror), `core.ac.uk`, `www.osti.gov`, ResearchGate, Semantic Scholar, CiteSeerX, author sites, ICAS, EasyChair, JOSS, and the University of Minnesota repository. The WebFetch tool is blocked for the same hosts.

**One legitimate primary channel was open:** anonymous git reads of public GitHub repositories. NASA publishes `github.com/nasa/simupy-flight` under the NASA Open Source Agreement. Its `NESC_data/` folder redistributes the NESC 6-DOF check-case F-16 model package (DAVE-ML, AIAA S-119) and the check-case trajectories, "accessed summer 2020" from `nescacademy.nasa.gov/flightsim` (its README). These are NASA's own machine-readable model files, not a secondary summary.

| Item | Result |
|---|---|
| PDFs downloaded | **0** |
| Primary files downloaded and hashed | **32** (5 DAVE-ML model files, the package README, 24 check-case CSVs, the NESC data README, the NOSA licence) from `nasa/simupy-flight` commit `70754e6916afc206e8c0abb386d1a9c98bf8f561` |
| Storage | Outside the repository: `/home/user/aerospace-sources/F16/NESC_F16_package__nasa_simupy-flight_70754e6/` in this ephemeral container. Copy to the durable store (`E:\aerospace-sources\F16\`) and confirm each `sha256`. Per-file records (filename, source URL at the commit, ID, SHA-256, size, acquisition date) are in `SOURCE_FILES_MANIFEST.json` |
| Hash comparison with other Maverick branches | **Not possible.** No Maverick branch holds a hash for any NESC file. The repository's PDF hashes (TP-1538, the F/A-18 set) could not be compared because no PDF could be downloaded |

### Meaning of `PAGE_VERIFIED` for a machine-readable file

For a DAVE-ML or CSV file, the "page" is the file element. `PAGE_VERIFIED` means the cited element (a `variableDef`, `griddedTableDef`, calculation, check-shot or CSV row, with its line number) was read in the hashed file. Every promoted value carries that locator. **No PDF page was verified in R2.** The NESC report itself (`NASA-TM-2015-218675`) stays at `ABSTRACT_VERIFIED`.

## 3. F-16 outcome

### 3.1 Sources upgraded to `PAGE_VERIFIED` (7)

`NESC-F16-PACKAGE-README`, `NESC-F16-AERO-DML`, `NESC-F16-PROP-DML`, `NESC-F16-INERTIA-DML`, `NESC-F16-CONTROL-DML`, `NESC-F16-GNC-DML`, `NESC-F16-CHECKCASE-TRAJECTORIES`. They describe one configuration, `F16-CFG-NESC-CHECKCASE`.

### 3.2 What the NASA files establish (verbatim sources)

- **Lineage, in NASA's words.** Package README: "This model is based on [TM212145] by Garza and Morelli, which implements aero data published in [TP1538] by Nguyen." `F16_aero.dml` header: "Based on Morelli's adaptation of Stevens and Lewis' F-16 example ... Obtained from E. A. Morelli in the form of Matlab scripts" (f16_aero.m, 1995). Propulsion and inertia: "as described in Stevens & Lewis", 2nd edition 2003.
- **The NESC aero is a table model, not the Morelli 1998 polynomial model** that Maverick implements. This answers open question F16-Q-V2 and is recorded as conflict F16-C8 (configuration-incompatible for aerodynamics).
- **The NESC propulsion is steady-state net thrust only**: idle / military / maximum tables with a power-lever interpolation. There is no engine lag and no engine angular momentum.
- **The NESC controller is an LQR check-case controller with a simple mixer.** There are **no actuator dynamics**. It is not an F-16 FCS.

### 3.3 Required F-16 items

| Item | Status after R2 | Where |
|---|---|---|
| Reference area / chord / span | **PAGE_VERIFIED for the NESC configuration**: 300 ft^2, 11.32 ft, 30 ft (`F16_aero.dml` lines 380, 368, 374). TP-1538 Table I itself: still a repository reading | `F16-NESC-S`, `-CBAR`, `-B` |
| Mass / CG | **Verified (NESC):** 637.1595 slug "(20,500 lbm)"; check-case CG 25 % MAC; moment reference centre 35 % MAC. Trap: the file's default CG input is 35 | `F16-NESC-MASS`, `-CG`, `-MRC` |
| Ixx / Iyy / Izz / Ixz | **Verified (NESC):** 9,496 / 55,814 / 63,100 / 982 slug-ft^2, Ixy = Iyz = 0; Ixz is S-119 `bodyProductOfInertia_ZX`, "no sign reversal" | `F16-NESC-IXX` ... `-IYZ` |
| Coefficient definitions | **Verified (NESC):** body-axis CX +fwd, CY +right, CZ +down, Cl +RWD, Cm +ANU, Cn +ANR, about the MRC; full build-up equations | `F16-NESC-AERO-DEF`, `-BUILDUP` |
| Control inputs | **Verified (NESC):** el +TED, ail +LWD = (right - left)/2, rdr +TEL, degrees; normalised el/25, ail/20, rdr/30 | `F16-NESC-CTRL-DEF` |
| Validity range | **Verified (NESC):** alpha -10..45 deg, beta +/-30 deg, elevator +/-24 deg, all clamped (`extrapolate="neither"`); no Mach input; stated envelope near 10,000 ft and 287.8 KEAS (Mach 0.5) | `F16-NESC-ALPHA`, `-BETA`, `-ELEV-DOMAIN`, `-MACH` |
| Rate normalisation | **Verified (NESC):** b/(2V) for p and r, cbar q/(2V) for q; rates in rad/s | `F16-NESC-RATE-NORM` |
| Engine thrust-table semantics | **Verified (NESC):** net steady-state thrust along +X, Mach 0-1.0 x 0-50,000 ft, PLA 0-100 with MIL at 50 | `F16-NESC-THRUST-TABLES`, `-PLA` |
| Engine lag semantics | NESC: **none** (verified). Garza & Morelli lag: still a repository reading | `F16-NESC-ENGINE-DYN`; `F16-ENG-RTAU` |
| Control-system architecture | NESC check-case LQR + mixer (verified). F-16 architecture from TP-1538: still unread | `F16-NESC-MIXER`, `-LQR` |
| Actuator model | NESC: **none** (verified). TP-1538: **fig. 63 still unread. Whether TP-1538 prints an actuator model or rate limit is still unresolved** | `F16-NESC-ACTUATORS`; conflict F16-C1 |
| Trim / validation cases | **Verified (NESC):** case 11 trim (10,013 ft, 565.6854 ft/s, CG 25 %, pitch 2.6538 deg, tail -3.2410 deg, stick 0.12964, throttle 0.13902); 8 trajectory cases from 3 tools; 16 aero and 9 propulsion static check-shots | `F16-NESC-TRIM11-*`, `-CHECKCASES`, `-STATIC-SHOTS` |
| High-alpha / VISTA reports | Not reachable (DTIC and author hosts blocked) | — |
| Textbook actuator rates | **Not inherited.** NESC carries none; TP-1538 unread | F16-C1 |

### 3.4 Cross-check of the repository's TP-1538 Table VI transcription

The NESC thrust tables and the repository's two-pass visual transcription of TP-1538 Table VI were compared cell by cell. They **agree in 88 of 90 cells** (Mach 0.2-1.0, 6 altitudes, 3 power levels). They differ at two sea-level maximum-thrust cells:

| Cell | TP-1538 (repository transcription) | NESC `F16_prop.dml` | Printed SI column (repository) |
|---|---|---|---|
| Mach 0.8, 0 ft, MAX | 26,070 lbf | 28,070 lbf | 115,959 N = 26,070 x 4.448 |
| Mach 1.0, 0 ft, MAX | 28,886 lbf | 28,885 lbf | 128,485 N = 28,886 x 4.448 |

The SI column (per the repository) is consistent with the TP-1538 transcription, so the change most likely entered the textbook lineage. The NESC table also adds a Mach 0 column that TP-1538 Table VI does not have. Recorded as conflict F16-C6, `RESOLVED_PER_CONFIGURATION`. **This supports the repository transcription but does not page-verify TP-1538.** The TP-1538 values stay `BLOCKED_PENDING_PAGE_VERIFICATION`.

## 4. F/A-18 outcome

No F/A-18 primary file was reachable (NTRS blocked; no NASA-published F/A-18 repository located). **No F/A-18 source or value was promoted.** Every F/A-18 conflict is now classified in [`CONFLICT_REGISTER.md`](CONFLICT_REGISTER.md) (FA18-C1 to C10).

## 5. F-15 outcome

No F-15 primary file was reachable. **No F-15 source or value was promoted.** The six named questions are classified in the register:

| Question | Classification | Status |
|---|---|---|
| NASA 836 baseline mass column (F15-X1) | `DIFFERENT_CONFIGURATION` (two columns of the same table) | `RESOLVED` on the implementation branch; not reopened; library page check pending |
| ARI / crossfeed Mach switches (F15-X2) | `SAME_SOURCE_TYPO_OR_REVISION` + `STILL_UNRESOLVED` | **UNRESOLVED** (text vs figure) |
| 23,500 vs 25,000 lbf (F15-X3) | `SAME_SOURCE_TYPO_OR_REVISION` (the 25,000 sentence is internally inconsistent; both uninstalled SLS) | `RESOLVED` (23,500 preferred; both prohibited as model inputs) |
| Stabilator ranges (F15-X6) | `DIFFERENT_CONFIGURATION` (four lineages) | `RESOLVED_PER_CONFIGURATION` |
| 15.94 vs 15.95 ft (F15-X5) | `ROUNDING` + `STILL_UNRESOLVED` (191.33 in = 15.944 ft rounds to 15.94; 15.95 needs a different upstream value) | **UNRESOLVED**, low impact |
| Throttle-angle convention (F15-X7) | `DIFFERENT_CONFIGURATION` + `DIFFERENT_DEFINITION` | `RESOLVED_PER_CONFIGURATION` |

## 6. Numbers

| Metric | R1 | R2 |
|---|---:|---:|
| Sources | 227 | 234 |
| `PAGE_VERIFIED` sources | 0 | **7** (all NESC F-16 files) |
| Candidate numeric fields | 209 | 251 |
| Implementation-allowed fields | 0 | **34** (all in `F16-CFG-NESC-CHECKCASE`) |
| Conflicts registered | (in prose) | 25 (10 unresolved) |

## 7. Next highest-value verification batch

Run from a machine or environment that can reach `ntrs.nasa.gov` (in the cloud environment: add the host under Network access). Verify in this order, comparing each PDF's SHA-256 with the repository manifests where they exist:

1. **NASA TP-1538** (NTRS 19800005879; repository sha256 `aae0ece6...`): Table I, Table VI (p.99), fig. 63 and its text, Appendix B engine text. This settles F16-C1, F16-C3 and F16-C6, and would promote the TP-1538 configuration values.
2. **Morelli 1998** (NTRS 20040110310) and **Garza & Morelli TM-2003-212145** (NTRS 20030013626): the implemented model and the engine lag.
3. **NASA TM-2012-215978** p.8 Table 1 and fig. 5 (F15-X1 confirmation, F15-X2 resolution).
4. **F/A-18**: TP-97-206539 Tables 1 and 3, TM-107601 Tables 3.4, 3.5, 8.6 and 8.7, TM-110216 Tables 2.3, 6.1 and 6.2 (repository hashes exist for all of them).
