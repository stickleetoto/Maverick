# F-16 Validation Source Graph (R1, R2 update)

> **R2.** The NESC F-16 model files and check-case trajectories were retrieved from NASA's `nasa/simupy-flight` redistribution and verified at file level. Questions F16-Q-V1 and F16-Q-V2 are answered below. Case 11 trim values and all model parameters of the check-case configuration are now implementation-allowed for a validation profile.

Parent: [`SOURCE_GRAPH.md`](SOURCE_GRAPH.md). Vocabulary: [`../../Validation/README.md`](../../Validation/README.md#validation-vocabulary-for-maverick-claims). A source-faithful model of a simulator is not a physically validated model of an aircraft.

---

## 1. Validation evidence by type

| Evidence type | Source(s) | Configuration | Independence from TP-1538 data | What it can validate |
|---|---|---|---|---|
| **EOM / table-handling check-case with trim** | `NASA-TM-2015-218675` (Vol. I summary, Vol. II 614 pp.), `NESC-AIAA-2013-5071` | `F16-CFG-SIM-STEVENS-LEWIS` | **None** (same data lineage) | That Maverick's 6-DOF integration, atmosphere and table handling reproduce a reference implementation. Case 11 is a trimmed straight-and-level F-16 |
| Independent implementation of the check-cases | `NASA-SIMUPY-FLIGHT` (NASA open source, JOSS paper; regression data included) | same | None | A second reference trajectory for the same case |
| Behavioural trajectory oracle | `ARCH-2018-HEIDLAUF` (AeroBench; USAF public-release approvals recorded in its README) | same | None | GCAS-style manoeuvre behaviour; external validation only |
| Polynomial vs database accuracy | `MORELLI-ACC-1998-F16` (< 10 % in a simulated doublet, abstract) | `F16-CFG-SIM-MORELLI1998` | None (fit to the same data) | Fit quality at one condition |
| Flight-derived derivatives | `AIAA-84-2085` (AFTI, compared with tunnel and F-16A flight values) | `F16-CFG-AFTI` | **Yes** (flight) | Trends of static and control derivatives, AFTI airframe |
| Flight-identified full-envelope model | `AIAA-2018-0525` | `F16-CFG-VISTA-NF16D` | **Yes** (flight) | VISTA airframe behaviour; if published numerically, a validation source for a VISTA profile |
| Post-stall flight test | `NTRS-19950007831` (MATV: envelope expansion, revised laws, nose chines, PID manoeuvres) | `F16-CFG-VISTA-MATV` | Yes | MATV only |
| Air-data and pacer calibration | `DTIC-ADA495484`, `AFFTC-TIM-04-01` (F-16B 92-0457) | `F16-CFG-PROD-AB` | Yes | Instrumentation; not aerodynamics |
| Engine flight test | `CHILDRE-MCCOY-JPP-1989`, `ICAS-2008-286` | `PROD-CD` / `PROD-AB` | Yes | Engine behaviour in the named installation |
| Piloted-simulation behaviour | `NASA-TP-1538` (departure by inertia coupling in rapid large rolls at low speed; deep-stall trim) | `F16-CFG-NASA-REF-TP1538` | n/a (it is the source) | Qualitative behaviour Maverick should reproduce in the reference configuration |
| Deep-stall analysis | `EVANGELOU-AERJ-2001` | `F16-CFG-GENERIC-ACADEMIC` | None | Cross-check of trimmed deep-stall points |
| Method references | `MORELLI-SFTE-2023-RTPI`, `NASA-TM-2013-218056`, `NTRS-20200003104`, `JAIRCRAFT-2023-MORELLI-GRAUER` | simulation | n/a | How to identify Maverick's own model from piloted inputs |

## 2. Lineage of the check-case chain

```
NASA-TP-1538 data ─▶ STEVENS-LEWIS-ACS ─▶ F-16 in DAVE-ML (ANSI/AIAA S-119 names; AIAA-2002-4482 format)
                                             │
                     NESC-AIAA-2013-5071 ────┤ (development of the check-cases)
                                             ▼
                           NASA-TM-2015-218675 check-cases (case 11: trimmed F-16, straight and level)
                                             │ reproduced by
                                             ▼
                           NASA-SIMUPY-FLIGHT (NASA open source; regression data)
```

**R1 caveat resolved in R2.** The NESC propulsion file (`F16_prop.dml`, verified) is Stevens & Lewis (2003) **steady-state net thrust**: idle, military and maximum tables on Mach 0-1.0 x 0-50,000 ft, with linear power-lever interpolation (MIL at 50 %), thrust along body +X and zero thrust moments. It has no engine lag.

### R2 verified check-case content

| Item | Value | Locator |
|---|---|---|
| Configuration | `F16-CFG-NESC-CHECKCASE` (CG 25 % MAC; MRC 35 % MAC; mass 637.1595 slug) | `F16_inertia.dml`, package README |
| Case 11 trim | 10,013 ft MSL, 565.6854 ft/s, pitch = alpha 2.6538 deg, tail -3.2410 deg (+TED), stick 0.1296382, throttle 0.1390191 | README Table 11; `F16_control.dml` lines 226, 231, 471 |
| Tool spread at t = 0 (case 11) | pitch 2.64333 / 2.63873 / 2.63893 deg (sims 02 / 04 / 05) | CSV first rows (conflict F16-C7) |
| Trajectory cases | 11, 12, 13.1-13.4, 15, 16; three tools each | 24 CSV files |
| Case 12 | Mach 2.01 at 30,013 ft with the Mach-independent subsonic model: **EOM test only** | CSV first rows |
| Unit-level checks | 16 aero and 9 propulsion static check-shots | `F16_aero.dml` lines 1564-3919; `F16_prop.dml` lines 370-856 |

## 3. Verdicts for trim and trajectory validation (verdicts E and F)

| Verdict | Supported for | Evidence | Not supported for |
|---|---|---|---|
| **E: trim validation** | **`F16-CFG-NESC-CHECKCASE`: SUPPORTED and file-verified (R2)** | README Table 11 + control-file trim inputs; tool spread gives the tolerance floor | Maverick's Morelli-model configuration (different aero model); any real F-16 |
| **F: trajectory validation** | **`F16-CFG-NESC-CHECKCASE`: SUPPORTED as implementation verification (R2)** | 8 cases x 3 tools (file-verified) | Production F-16 trajectories. VISTA/AFTI flight data are configuration-specific |

A Maverick claim built on these must read "implementation-verified against the NESC F-16 check-case", never "validated against the F-16".

## 4. Open validation questions

| ID | Question | Where to look |
|---|---|---|
| F16-Q-V1 | **Answered (R2):** cases 11, 12, 13.1-13.4, 15, 16 with three tools each. None uses the Morelli 1998 model | check-case files (verified) |
| F16-Q-V2 | **Answered (R2):** the textbook table set, via Morelli's 1995 MATLAB adaptation, not Morelli 1998 | `F16_aero.dml` header (verified) |
| F16-Q-V3 | Does `AIAA-2018-0525` publish numeric identified parameters? | Author-hosted copy |
| F16-Q-V4 | Does `AIAA-84-2085` tabulate derivatives with flight conditions? | NTRS 19840059553 |
