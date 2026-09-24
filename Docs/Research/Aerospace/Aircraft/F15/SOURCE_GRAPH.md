# F-15 Source Graph (R1 consolidation)

Status: **centralises F-15 source lineage that already exists across the Maverick repository.** R1 did not redo the F-15 research. Almost every F-15 record here was identified and page-read by repository work on three branches. The library has not inspected those sources itself, so most are `SEARCH_LEAD_ONLY` at the library level. Each carries `existing_maverick_analysis` entries that say which repository document read what, on which branch and commit.

> **Maverick's own conclusions are not primary sources.** Where the repository says "p.8 table 1 reads 37,426 lb", the library records *that the repository says so*, with the path and commit. It does not promote the number.

Repository branches cited:

| Label | Branch @ commit | Role |
|---|---|---|
| main-derived | `claude/aerospace-source-library-r0` (inherits main @6499d0d) | `Docs/Reference/F15_SOURCE_PACK_V0.1.md`, `F15_FULL_SCALE_*`, `F15_R0_CONFIGURATION_FREEZE_V0.1.md` |
| implementation | `claude/f15-full-implementation` @89140b9 | `F15_A4172_SOURCE_LINEAGE_V0.1`, `F15_DN1180_SOURCE_LINEAGE_V0.1`, `F15_836_FCS_STRUCTURE_V1.0`, `F15_836_VALIDATION_DATA_V1.0`, `F15_NASA836_REFERENCE_GEOMETRY_AUDIT_V0.1`, `F15_R5_F100_SOURCE_AUDIT_V0.1`, `F15_FINAL_STATE_V1.0` |
| gap audit | `sol/f15-source-gap-audit` @1a3cd4b | `F15_MISSING_SOURCE_PRIORITY_V0.1` |

Source IDs refer to [`SOURCE_INDEX.json`](../../SOURCE_INDEX.json) (slice: [`SOURCES.json`](SOURCES.json)). Configuration IDs: [`KNOWN_DATA.md`](KNOWN_DATA.md#1-configuration-registry).

---

## 1. Configuration spine

```
McDonnell F-15 programme
 ├─ Preproduction F-15 No. 8 ..................... F15-CFG-PREPROD-8        (NASA-TM-72861: surface authority, reference geometry)
 ├─ Production F-15A-D family .................... F15-CFG-PROD-AD          (MDC-A4172, MDC-DN-1180-01-238-458: NOT PUBLICLY LOCATED)
 │
 ├─ NASA F-15B 836 (USAF 74-0141)   [one serial, several configurations]
 │    ├─ pre-Quiet-Spike baseline, 2 x F100-PW-100 .. F15-CFG-NASA836-PRE-QS   ◀ Maverick F-15 target
 │    ├─ centreline fixtures (FTF-II / PFTF / LIFT) . F15-CFG-NASA836-PFTF
 │    ├─ Quiet Spike (2006-2007) ...................... F15-CFG-NASA836-QS
 │    └─ after the 2014 re-engine (F100-PW-220E) ...... F15-CFG-NASA836-POST2014 (out of scope)
 │
 ├─ NF-15B 837 (ACTIVE / IFCS; canards; F100-PW-229 + vectoring) ... F15-CFG-NF15B-837
 ├─ NASA 835 (HIDEC / PCA) ............................................ F15-CFG-NASA835
 ├─ early NASA F-15 engine test bed (prototype F100) ................. F15-CFG-NASA-F15-EARLY-ENGINE-TESTBED
 │
 ├─ Subscale: 3/8 RPV (unpowered) .................... F15-CFG-RPV-3-8        (TN D-8136, D-7941, D-8052)
 │            tunnel models (0.075, 1/12 rotary, three-surface, CFT) ... F15-CFG-WT-SUBSCALE
 │
 └─ Simulations: AFIT theses on ASD "F-15 Aerobase" / McAir ARO10 fits ... F15-CFG-SIM-AFIT-AEROBASE
                 Brumbaugh CR-186019 ("not representative of any aircraft") F15-CFG-SIM-CR186019
                 hobbyist models ................................... F15-CFG-ENTHUSIAST (prohibited as authority)
```

## 2. NASA 836 lineage (exact airframe)

```
NASA-TM-4782 (Richwine, 1996; FTF-II description: 63.7 ft long, 42.8 ft span, BOW 27,500 lbm; no S or cbar)
      │ cited as ref. 1 by
      ▼
NASA-TM-2008-214634 (Cumming, Smith, Frederick; tail 836 on p.6; baseline flights "update the existing aerodynamic model";
      │                coefficients defined WITHOUT reference dimensions)
      │ baseline aero model reference for every Quiet Spike report
      ▼
NASA-TM-2009-214651 (McWherter et al., Aug 2009) ──COMPANION── NASA-TM-2012-215978 (Moua et al., May 2012)
   FCS block diagrams figs. 3-5                     Table 1: Baseline and spike-installed mass columns; figs. 3-5; figs. 27-29
      │                                                   │
      └──────────────────────┬────────────────────────────┘
                             ▼
       Maverick F-15 836 target (implementation branch): mass state, FCS structure, 60 validation series

Identity chain: NTRS-20070032807 (S/N 74-0141, "production F-100-PW-100 engines"), NTRS-20100001729 (PFTF on tail 836),
                NTRS-20160006705 (2016 briefing; PW-220E since 2014)
Physical dimensions only: NASA-TM-2005-213670, NASA-TM-2006-213674, NASA-TM-2001-210395 (also 28 % MAC = FS 561.7)
```

**No 836 source prints S, cbar or a reference span** (implementation-branch geometry audit). The family value 608 ft^2 / 15.94 ft appears only in other configurations (837, AFIT, CR-186019).

## 3. McDonnell lineage and its public reproductions

```
MDC-A4172 (McAir 1976; Part I mass/inertia; Part I Supp. 1, 1979; Part II derivatives)  ── NOT PUBLICLY LOCATED
MDC-DN-1180-01-238-458 Rev. D (FCS description, Oct 1981)                                ── NOT PUBLICLY LOCATED
AFFTC-TR-75-32 (AD-B045115, stall/post-stall evaluation)                                  ── LIMITED DISTRIBUTION: not sought
ASD-F15-AEROBASE-ARO10 (simulator tables)                                                 ── NOT PUBLICLY LOCATED
        │ cited / curve-fitted by public AFIT theses (Distribution A per repository)
        ▼
AFIT-GAE-ENY-89D-01 Baumann ── AFIT-GAE-ENY-90D-16 McDonnell ── AFIT-GA-ENY-91D-4 Fero ── AFIT-GA-ENY-91D-1 (datum, %MAC equation)
AFIT-GAE-ENY-92J-02 Nolan (only public DN-1180 citation; qualitative) ── AFIT-GAE-ENY-92M-01 Davison ── AFIT-GAE-ENY-96M-1 Evans
AFIT-BARTH-BECK-UPSTREAM (upstream table theses): NOT LOCATED
```

**Reading.** The public aerodynamic lineage is the ASD Aerobase / ARO10 product, curve-fitted at a single flight condition. It is **not** MDC A4172 Part II. The AFIT reference datum equation `%MAC = (FS - 508.1) / 191.33 x 100` ("for all A through D models") reproduces NASA 836's 28 % MAC = FS 561.7 to 0.03 in. That is family support, not an 836 link.

## 4. Independent NASA line (preproduction)

```
AFFTC-TR-74-8-76-48 (AFFTC TR-74-8 and TR-76-48; not located) ─▶ NASA-TM-72861 (Sisk & Matheny, May 1979, F-15 No. 8)
     control-system appendix: authorities, 0.3 differential ratio, 1.8 deg/cm pedal gearing, ARI gradients, RRAD/ARI plots;
     NO CAS feedback gain (repository correction of an earlier claim)
```

## 5. Subscale and tunnel lineage (cross-validation only)

`NASA-TN-D-8136`, `NASA-TN-D-7941`, `NASA-TN-D-8052` (3/8 RPV); `NASA-TM-X-62360` (0.075 scale, high alpha); `NASA-CR-3478`, `NASA-CR-3479`, `NASA-CR-3516` (rotary balance, incl. CFT); `NASA-TP-2234`, `NASA-TP-2043` (three-surface transonic); `NASA-TP-2333` (afterbody/nozzle). The repository's RPV transfer policy governs any use. The library adds nothing to it.

## 6. Propulsion

See [`F15_PROPULSION_SOURCE_GRAPH.md`](F15_PROPULSION_SOURCE_GRAPH.md).

## 7. Conflicts centralised in R1

> **R2.** No F-15 primary file was reachable (NTRS and DTIC blocked), so nothing was promoted or reopened. Each conflict below is now classified, with both claims recorded, in the library [`CONFLICT_REGISTER.md`](../../CONFLICT_REGISTER.md) (F15-X1 to X8). Still **UNRESOLVED**: F15-X2 (ARI/crossfeed switch points) and F15-X5 (15.94 vs 15.95 ft, low impact).

| # | Quantity | Values | Where | Status |
|---|---|---|---|---|
| F15-X1 | 836 mass state | Main-branch freeze: 37,152 lb, 26.05 % MAC, Ixx 27,953, Iyy 190,777, Izz 213,957, Ixz -460 slug-ft^2. Implementation branch: that is the **spike-extended** column; the Baseline column is 37,426 lb, 26.34 %, 30,345 / 198,687 / 223,214 / -5,070 | `F15_FULL_SCALE_TARGET_FREEZE_V0.1.md` (main) vs `F15_FINAL_STATE_V1.0.md` (implementation) | **Resolved in favour of the implementation branch for the target**, which read and labelled all three Table 1 columns. Both columns are kept, each tagged with its own configuration. Library confirmation still requires a page read |
| F15-X2 | ARI / crossfeed Mach switches | Implementation branch (rendered fig. 5 of both reports): ARI inputs zero above **M 1.5**; crossfeed AoA input zero above **M 1.0**. Main branch (text associated with fig. 5): ARI operative below **M 1.0**; crossfeed nullified above **M 1.5** | `F15_836_FCS_STRUCTURE_V1.0.md` vs `F15_FULL_SCALE_DATA_GAPS_V0.2.md` | **Open at library level.** The two readings are reversed. The implementation branch cites a rendered figure at 170 dpi; main cites text. Report text and figure may genuinely disagree. The library cannot adjudicate without a page read |
| F15-X3 | Uninstalled SLS thrust per engine | 23,500 lbf "in full afterburner" (TM-2005-213670) vs "approximately 25,000 lbf (91,188 N)" (TM-2001-210395, TM-2002-210736) | implementation branch §6e | 23,500 preferred (internally consistent, condition stated). 25,000 lbf = 111,206 N, but 91,188 N = 20,500 lbf: internally inconsistent. Neither is a deck |
| F15-X4 | Reference span | 837: 42.7 ft normalising vs 42.83 ft three-view (same report) | TM-2003-212027 | Not a physics conflict: reference vs physical |
| F15-X5 | cbar | 15.94 ft (837, AFIT) vs 15.95 ft (CR-186019) | | Rounding or a different source; CR-186019 is "not representative of any aircraft" |
| F15-X6 | Stabilator travel | 15 / -26 (TM-72861, No. 8), 29 down / 15 up (McDonnell thesis), +20 / -30 (Baumann), +15 / -25 (CR-186019) | four lineages | Four configurations; never merged. None is 836 |
| F15-X7 | PLA convention | Military = 73 deg (TM X-3261, PW-100(1)) vs top of non-augmented = 83 deg (TP-1034, PW-100(3)) | F100 audit | Different builds and definitions; not averaged |
| F15-X8 | "Real" absolute thrust scale for TP-1034 fig. 17 | Unstated. 30,000 lbf channel scale (TP-1034 App. C) and 111.2 kN axis (TP-1373) are **not** engine data | F100 audit | Recorded as prohibited scales |
