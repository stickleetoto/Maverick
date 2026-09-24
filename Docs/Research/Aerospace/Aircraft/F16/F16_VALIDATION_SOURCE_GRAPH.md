# F-16 Validation Source Graph (R1)

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

**Caveat recorded in R1.** A third-party note claims that the NESC F-16 propulsion file is a bounded static-thrust surface from the Stevens & Lewis lineage. The library has **not** verified this and does not rely on it.

## 3. Verdicts for trim and trajectory validation (verdicts E and F)

| Verdict | Supported for | Evidence | Not supported for |
|---|---|---|---|
| **E: trim validation** | `F16-CFG-SIM-STEVENS-LEWIS` (and, via the shared data lineage, a consistency check of the TP-1538/Morelli implementation) | NESC case 11; simupy-flight regression data | A real F-16. No public production-F-16 trim table was located |
| **F: trajectory validation** | Same textbook configuration (NESC time histories; AeroBench behaviour) | Check-case time histories | Production F-16 trajectories. VISTA/AFTI flight data are configuration-specific |

A Maverick claim built on these must read "implementation-verified against the NESC F-16 check-case", never "validated against the F-16".

## 4. Open validation questions

| ID | Question | Where to look |
|---|---|---|
| F16-Q-V1 | Which NESC F-16 cases exist besides case 11, and do they include time histories suited to the Morelli model (same aero data)? | NESC Vol. II, check-case files |
| F16-Q-V2 | Is the NESC F-16 aero data identical to Morelli 1998, or the textbook table set? | NESC Vol. II; DAVE-ML file header |
| F16-Q-V3 | Does `AIAA-2018-0525` publish numeric identified parameters? | Author-hosted copy |
| F16-Q-V4 | Does `AIAA-84-2085` tabulate derivatives with flight conditions? | NTRS 19840059553 |
