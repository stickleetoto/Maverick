# F-15 Propulsion Source Graph: F100 lineage (R1 consolidation)

Parent: [`SOURCE_GRAPH.md`](SOURCE_GRAPH.md). F-16 side of the same engine family: [`../F16/F16_PROPULSION_SOURCE_GRAPH.md`](../F16/F16_PROPULSION_SOURCE_GRAPH.md).

Almost all of this was established by the implementation branch's F100 audit (`claude/f15-full-implementation` @89140b9, `Docs/Reference/F15_R5_F100_SOURCE_AUDIT_V0.1.md`). That audit retrieved and read several NTRS PDFs. The library has not, so the library level of most records is `SEARCH_LEAD_ONLY`.

---

## 1. Build registry: never merged

| Configuration ID | Build | Key sources | What they give (repository reading) |
|---|---|---|---|
| `ENG-F100-PW-100-1` | F100-PW-100(1) | `NASA-TM-X-3261` | Hybrid real-time simulation patterned after P&W CCD 1015; thrust equations B42-B52; component maps as graphs; **military PLA = 73 deg** (p.6) |
| `ENG-F100-PW-100-2-7-8` | Prototype series 2-7/8 (P680059, P680063; 1977 altitude calibration) | `NASA-TP-1373`, `NASA-TP-1482`, `NASA-TP-1782`, `NASA-TP-1069-TP-1228`, `NTRS-19780015152`, `NTRS-19790022017` | Calculated vs facility-measured thrust and airflow; accuracy 1.24 % airflow and 2.38 % gross thrust at 2 sigma (TP-1373 abstract); SGTM +/-3 % in flight (TP-1782); test matrices in TP-1373 table 3 |
| `ENG-F100-PW-100-3` | F100-PW-100(3) improved-fan build | `NASA-TP-1034`, `NASA-TP-1056`, `F100-SPEC-CP2903B` | TP-1034 fig. 17: net thrust vs PLA at 7 conditions as a **fraction of an unstated design maximum**; PLA 83 deg top of non-augmented. TP-1056: thrust requirements are in CP2903B, which is **classified** |
| `ENG-F100-PW-100-3-DEEC` | P680063 rebuilt to the production F100(3) gas path, with DEEC (from 1980) | `NASA-TM-84908`, `NASA-CP-2298`, `NTRS-19860015884`, `NTRS-19990064011` | Airstart and transient behaviour with DEEC; DEEC state is not the standard-engine state |
| `ENG-F100-EMD` | F100 Engine Model Derivative (1985 on) | `NASA-TM-85902`, `NTRS-19990064011` | Not F100-PW-100 |
| `ENG-F100-PW-220` (defined in the F-16 pack) | F100-PW-220 | (F-16 sources) | Shared engine family; not an 836 engine before 2014 |
| `ENG-F100-PW-229` (F-16 pack) | F100-PW-229 | `NASA-TM-2003-212027` (on NF-15B 837, with vectoring nozzles) | Not 836 |

**836's own engines.** F100-PW-100 x2 until 2014, sub-build **not established** by any public source (implementation branch §6g). F100-PW-220E after 2014 (`NTRS-20160006705`).

## 2. Lineage

```
P&W CCD 1015 ─▶ NASA-TM-X-3261 (PW-100(1) hybrid simulation)
P&W CCD 1103-1.0 ─▶ NASA-TP-1034 (PW-100(3) hybrid simulation) ─▶ NASA-TP-1056 (MVCS; CP2903B classified, p.8)
P&W CCD 1088-2.0 (deck, not public) ◀── compared by ── NASA-TP-1373 (series 2-7/8, altitude facility)
                                                        ├─ NASA-TP-1482 (simplified gross-thrust method, facility)
                                                        ├─ NASA-TP-1782 (same method in flight, F-15)
                                                        └─ NASA-TP-1069-TP-1228 / NTRS-19780015152 (altitude calibrations)
P680063 chronology: 1972 PW-100(2) ─▶ 1974 (2-7/8) ─▶ 1977 PSL calibration ─▶ 1980 F100(3) + DEEC ─▶ 1985 EMD ─▶ 1990 overhaul
                    (NTRS-19990064011, NASA-TM-84908 per repository)

836 aircraft-level statements (not decks):
  NASA-TM-2005-213670: ~23,500 lbf uninstalled SLS, full AB (preferred)
  NASA-TM-2001-210395 / NASA-TM-2002-210736: "~25,000 lbf (91,188 N)" (internally inconsistent; rejected)
  NTRS-20160006705: PW-220E since 2014, "24,000 lb thrust class" (out of scope)

Inlet: NASA-CR-144866 (F-15 inlet/engine distortion methodology; configuration match to 836 pending), NASA-TP-2411 (hub probe, distortion)
```

## 3. Dimensional status

| Question | Answer (repository reading) |
|---|---|
| Is there a public dimensional F100-PW-100 deck? | **No.** TP-1034 fig. 17 is normalised by an unstated design maximum. TP-1373 reports percentages against an absent P&W deck |
| Is there an absolute scale? | Not in the held sources. The 30,000 lbf TP-1034 channel scale and the 111.2 kN TP-1373 axis scale are not engine data. CP2903B is classified and **must never be reconstructed** |
| Can 836's 23,500 lbf scale the fig. 17 curves? | **No.** The builds are not shown to be equivalent, and the repository derived a ~22,400 lbf bound on the TP-1034 normaliser that 23,500 exceeds. Different quantities on different builds |
| Transient model | TM X-3261 and TP-1034 structure only; target build and control state unknown |

## 4. Verdict

Dimensional propulsion for NASA 836 is **NOT SUPPORTED** from public sources. What exists is a normalised shape (PW-100(3) simulation), facility calibrations of prototype engines, and an approximate aircraft-level thrust statement. See [`IMPLEMENTATION_FEASIBILITY.md`](IMPLEMENTATION_FEASIBILITY.md).
