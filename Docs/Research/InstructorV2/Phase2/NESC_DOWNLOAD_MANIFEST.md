# NESC Check-Case Download Manifest

**Downloaded:** 2026-11-17, with the user's explicit approval, from the NASA NESC Academy flight-simulation check-case site (`https://nescacademy.nasa.gov/flightsim/2015`).
**Purpose:** validation fixtures for the three-level NASA benchmark design in `07_NASA_6DOF_BENCHMARK_STUDY.md`.
**Storage:** local, untracked folder `.nesc_downloads/` in the Phase 2 worktree, excluded through `.git/info/exclude`. **The binaries are not committed.** This manifest is the committed provenance record; any file can be re-downloaded and checked against the hashes below.
**Terms:** NTRS records for NASA/TM-2015-218675 state "Work of the US Gov. Public Use Permitted"; the NESC site and the F-16 README carry no separate licence text (doc 07 §1.6). The AIAA papers are not part of this download.

## Files

| File | Source URL | Size (bytes) | SHA-256 | Check |
|---|---|---:|---|---|
| `Atmospheric_checkcases.zip` | `https://nescacademy.nasa.gov/src/flightsim/Datasets/Atmospheric_checkcases.zip` | 24 932 914 | `1a83df6d00fa186973e8fb7bb8aefa5f7ed580d2ce8aa1327f50ac0c7bd5c869` | `zipfile.testzip()` clean; 225 entries (first attempt truncated at 6.9 MB by the server, completed with a resumed transfer) |
| `Atmospheric_models.zip` | `https://nescacademy.nasa.gov/src/flightsim/Models/Atmospheric_models.zip` | 1 498 326 | `10e2dc267856ef37eaedbcdd16eb49a8c44b39301054de0fa78292c1c4aba3bc` | `testzip()` clean; contains `F16_package.zip` (1 473 876 bytes) |
| `Initial_Conditions.xlsx` | `https://nescacademy.nasa.gov/src/flightsim/Scenarios/Initial_Conditions.xlsx` | 77 576 | `57b986c1ea50feec1881f1a45cf42c6c426ae78c8c364611857de2c4061fdb77` | not opened |
| `NASA-TM-2015-218675-EOM_checkcase_appendices.pdf` (Vol II, 614 pp.) | `https://nescacademy.nasa.gov/src/flightsim/Reports/NASA-TM-2015-218675-EOM_checkcase_appendices.pdf` | 16 603 034 | `f5d702972352331d8f9a7e3867f0cfdec2f91305da26a13c47d4c9a80fb0eb77` | text layer extracted and searched |
| `NASA-TM-2015-218675-EOM_checkcase_summary.pdf` (Vol I) | `https://nescacademy.nasa.gov/src/flightsim/Reports/NASA-TM-2015-218675-EOM_checkcase_summary.pdf` | 1 029 561 | `541db40684ee5663064ccf4b5e4ede7ffd536d38adb5493ce3af84da4f961d5f` | — |

## F-16 package contents (from `Atmospheric_models.zip` → `F16_package.zip`)

| File | SHA-256 | Matches the repository library copy? |
|---|---|---|
| `F16_S119_source/F16_aero.dml` | `82796577b989b5f3becb7d13430ff6c619b3ecd2e642a6427b743658fb177e51` | **yes** (`Docs/Research/Aerospace/SOURCE_FILES_MANIFEST.json`) |
| `F16_S119_source/F16_control.dml` | `bca7789dde61b741de69f697ce8799ee666214d0e1fa203973523774fc3a969b` | **yes** |
| `F16_S119_source/F16_gnc.dml` | `fc1dbff00cc14b0228973c43cfc17797fd48d2e36950ec039ff4e77d8577827c` | **yes** |
| `F16_S119_source/F16_inertia.dml` | `e11d4eba5b158d24db7034bd85682ad0b896dd2f3172f007ca3d6d6d4e693c4a` | **yes** |
| `F16_S119_source/F16_prop.dml` | `e5fd7772e5c05cdac666c94febbe4cb50139418b26045affd92dbd198f2215c6` | **yes** |
| `README/README.html` | `5496aa1314374d5d25f7c9f412351222eaee2f51b0fda6beebd348ace2483269` | **yes** |

The package also contains MATLAB setup/verify scripts (`F16_equiv_SL_files/*.m`) and README figures; they were not opened or executed.

## Spot checks by the lead in the downloaded files

| Item | Location | Value |
|---|---|---|
| Total mass | `F16_inertia.dml`, `XMASS` | 637.1595 slug |
| T_max, sea level, Mach 0.8 / 1.0 | `F16_prop.dml` lines 324–325 | 28 070 / 28 885 lb (TP-1538 Table VI: 26 070 / 28 886 lb). In the T_max rows for Mach 0.4–1.0 read here, every other cell equals the TP-1538 print except Mach 0.6 / 50 000 ft: NESC 3215.3 vs TP-1538 3 215 lb (0.3 lb, a decimal not printed in TP-1538) |
| Case directories used by doc 07 | `Atmospheric_checkcases/Atmos_02_…`, `Atmos_03_…`, `Atmos_11_…`, `Atmos_13p1…13p4_…` | case 2 has sims 01, 02, 04, 05, 06; F-16 cases have sims 02, 04, 05 |

## Not downloaded

Orbital datasets and models, AIAA papers, `All_models.zip` (orbital content is irrelevant to Maverick).
