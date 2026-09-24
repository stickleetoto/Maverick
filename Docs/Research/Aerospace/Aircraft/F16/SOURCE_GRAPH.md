# F-16 Source Graph (R1)

Status: **lineage rebuilt from catalogue records, abstracts, search extracts and the Maverick repository's own page readings.** The library itself has not opened any F-16 PDF. Where an edge depends on a repository reading, the edge says so. Levels are defined in [`SCHEMA.md` §2](../../SCHEMA.md#2-verification-levels).

Source IDs refer to [`SOURCE_INDEX.json`](../../SOURCE_INDEX.json) (F-16 slice: [`SOURCES.json`](SOURCES.json)). Configuration IDs refer to [`KNOWN_DATA.md`](KNOWN_DATA.md#1-configuration-registry).

Detailed sub-graphs:

| Topic | File |
|---|---|
| Aerodynamic / simulation model lineage | [`F16_MODEL_LINEAGE.md`](F16_MODEL_LINEAGE.md) |
| Flight-control sources (four-layer matrix) | [`F16_FCS_SOURCE_GRAPH.md`](F16_FCS_SOURCE_GRAPH.md) |
| F100 / F110 propulsion lineage | [`F16_PROPULSION_SOURCE_GRAPH.md`](F16_PROPULSION_SOURCE_GRAPH.md) |
| Validation data and cross-validation implementations | [`F16_VALIDATION_SOURCE_GRAPH.md`](F16_VALIDATION_SOURCE_GRAPH.md) |

---

## 1. Configuration spine

"F-16" is never a configuration. Every source is tagged with one or more of the IDs below. The families are kept apart even where they share a serial number or a data set.

```
 General Dynamics lightweight-fighter programme
 │
 ├─ YF-16 / FSD development tunnel models ............ F16-CFG-YF16-DEV
 │     tunnel data: NASA-CR-3053-V1, NASA-CR-3053-V2, NTRS-19790013829, NASA-TN-D-8176
 │
 ├─ Production family
 │     F-16A/B (Blocks 1-20) ........................ F16-CFG-PROD-AB   engine ENG-F100-PW-200 (later ENG-F100-PW-220)
 │     F-16C/D (Block 25 on) ........................ F16-CFG-PROD-CD   engine by block: ENG-F100-PW-220 / ENG-F100-PW-229 /
 │                                                                       ENG-F110-GE-100 / ENG-F110-GE-129
 │     production FLCS gains and manufacturer aero data: NOT PUBLICLY LOCATED (F16-PRODUCTION-FLCS-OFP)
 │
 ├─ NASA / USAF research airframes (each a separate configuration)
 │     AFTI/F-16 ..................................... F16-CFG-AFTI         digital flight control research
 │     F-16XL (cranked arrow) ........................ F16-CFG-F16XL        later Block 40 DFLCCs (NASA-TP-2004-212046)
 │     NF-16D VISTA .................................. F16-CFG-VISTA-NF16D  variable-stability system; later X-62A
 │       └─ MATV phase (AVEN nozzle, F110-GE-100) .... F16-CFG-VISTA-MATV   1993-1994
 │     Falcon 21 derivative (tunnel only) ............ F16-CFG-FALCON21
 │
 └─ Simulation models (never an airframe)
       NASA Langley TP-1538 simulation ............... F16-CFG-NASA-REF-TP1538   ◀ Maverick "NASA Reference F-16"
       Morelli 1998 polynomial fit ................... F16-CFG-SIM-MORELLI1998
       Stevens & Lewis textbook branch ............... F16-CFG-SIM-STEVENS-LEWIS (NESC check-case F-16, simupy-flight, AeroBench)
       academic re-implementations ................... F16-CFG-GENERIC-ACADEMIC
```

**Reading the spine.**

- The Maverick reference aircraft is a *simulation configuration*: TP-1538's own mass, geometry and thrust. It is not a production block. The repository already records this in `Docs/Reference/F16_SOURCE_PACK_V0.1.md` §7. The library adopts it unchanged.
- VISTA and MATV share an airframe but are two configurations. MATV added the AVEN nozzle and revised control laws for about a year (1993-1994). The later VISTA and X-62A flight-identified model (`AIAA-2018-0525`) is a third state of the same airframe.
- F-16XL shares the F-16 name, fuselage heritage and (later) Block 40 flight-control computers. Its wing, aerodynamics and control laws are different. Nothing moves between F-16XL and F-16 without an explicit delta model.

## 2. Top-level lineage (details in the sub-graphs)

```
                    [GD / NASA Langley wind-tunnel programme, 1970s]
                      NASA-CR-3053-V1/-V2, NTRS-19790013829 (YF-16/F-16 strakes)
                      NASA-TN-D-8176 (prototype, 0.15-scale; AoA/Nz limiter, ARI, yaw damper)
                                  │ same Langley group (BUILDS_ON; probable data path, not confirmed)
                                  ▼
   NASA-TP-1538 (Dec 1979) ── nonlinear tables, Table I mass/geometry, Table VI thrust (repository: page-transcribed)
          │                          │                                   │
          │ CURVE_FIT_OF             │ DERIVED_FROM                      │ DERIVED_FROM (textbook reduction)
          ▼                          ▼                                   ▼
   MORELLI-ACC-1998-F16       NASA-TM-2003-212145               STEVENS-LEWIS-ACS (1992/2003/2015)
   (M < 0.6, LEF 25 deg)      (Garza & Morelli: MATLAB,          │
          │                    engine lag Pc/RTAU)               ├─▶ NESC-AIAA-2013-5071 / NASA-TM-2015-218675 (check-case F-16, case 11 trim)
          │                          │                           │        └─▶ NASA-SIMUPY-FLIGHT (NASA open source, regression data)
          │                          └─▶ NASA-SW-LAR-17463-1     ├─▶ ARCH-2018-HEIDLAUF (AeroBench GCAS benchmark)
          │                              (U.S.-release-only;     ├─▶ ARXIV-1907-11913 (actuator 60/80/120 deg/s, 0.0495 s: textbook values)
          ▼                              not usable)             └─▶ RUSSELL-UMN-2003-F16, ISRLAB-F16-MODEL-MATLAB, AFIT theses
   Maverick F-16 reference (Assets/…/F16/, main branch)
```

Separate lineages that must not be joined to the chain above:

```
VISTA:   NASA Langley low-speed data sets ──▶ DTIC-ADA327869 (Bihrle, Aug 1997; alpha -80..+90, beta +/-30; F-16/VISTA)
         Calspan NF-16D flight data ────────▶ AIAA-2018-0525 (flight-identified full-envelope model; stitched)
         VISTA control research ────────────▶ AIAA-95-3249, AIAA-97-3787, ROBUST-MULTIVAR-FC-1994, AFIT-GE-ENG-95D-26, AIAA-2023-1746
MATV:    AIAA-94-3513 (control-law design) + NTRS-19950007831 (flight test: envelope, revised laws, nose chines, PID manoeuvres)
AFTI:    AIAA-84-2085 (flight-data derivatives vs tunnel and F-16A flight values), NTRS-19840012524, NTRS-19840007091,
         NTRS-19840012499, NASA-CR-4226
F-16XL:  NASA-TM-85776 ─▶ NASA-TM-97-206276, NASA-TM-1999-209703, NTRS-20040110755, AIAA-2000-3910 (aero/unsteady);
         NASA-TP-2004-212046 (Block 40 DFLCC re-host); NASA-TM-104326 (F110-GE-129 exhaust)
Falcon 21: NASA-TP-3355 (supersonic tunnel, compared with an F-16C model)
```

## 3. What each coefficient comes from (six axes)

This is the question "can the library support CX, CY, CZ, Cl, Cm, Cn without guessed coefficients?" The answer is split by configuration, because the answer differs by configuration.

| Configuration | CX | CY | CZ | Cl | Cm | Cn | Evidence and limits |
|---|---|---|---|---|---|---|---|
| `F16-CFG-NASA-REF-TP1538` (tables) | SOURCED | SOURCED | SOURCED | SOURCED | SOURCED | SOURCED | TP-1538 publishes nonlinear tables with LEF increments (repository App. B / nomenclature reading). The library has not seen the tables. Mach coverage: low-speed database |
| `F16-CFG-SIM-MORELLI1998` | SOURCED | SOURCED | SOURCED | SOURCED | SOURCED | SOURCED | Eqs. 12-17 and Tables 2-3 (repository page read). **M < 0.6, alpha -10..45, beta +/-30, LEF fixed at 25 deg.** No compressibility term. This is Maverick's implemented model |
| `F16-CFG-SIM-STEVENS-LEWIS` | SOURCED (textbook) | SOURCED | SOURCED | SOURCED | SOURCED | SOURCED | Textbook reduction of TP-1538 data (alpha typically limited to about 45 deg in the textbook branch; not checked by the library). Repetition of these tables in descendants is not confirmation |
| `F16-CFG-VISTA-NF16D` | CANDIDATE | CANDIDATE | CANDIDATE | CANDIDATE | CANDIDATE | CANDIDATE | `DTIC-ADA327869` claims a full nonlinear low-speed model to alpha +90. Numeric content not seen. `AIAA-2018-0525` is flight-identified for this airframe. Neither is a production block |
| `F16-CFG-PROD-AB` / `-PROD-CD` | NOT LOCATED | NOT LOCATED | NOT LOCATED | NOT LOCATED | NOT LOCATED | NOT LOCATED | No public manufacturer aero database located. `AIAA-2013-0972` (SEEK EAGLE CFD) may print F-16C derivatives; unassessed |
| `F16-CFG-AFTI` | PARTIAL (derivatives) | PARTIAL | PARTIAL | PARTIAL | PARTIAL | PARTIAL | Flight-derived *derivatives* at test points (`AIAA-84-2085`). Not a global model |
| `F16-CFG-F16XL` | tunnel/identified, **different aircraft** | | | | | | Never transferred to F-16 |
| Transonic/supersonic for any F-16 | NOT LOCATED | | | | | | `NASA-TP-3355` is a derivative planform (Falcon 21) with an F-16C comparison; comparison curves only |

**Verdict for this question:** a six-axis coefficient model *without guessed coefficients* is supported for the **TP-1538 / Morelli simulation configuration below Mach 0.6**. No other F-16 configuration is supported yet. See [`IMPLEMENTATION_FEASIBILITY.md`](IMPLEMENTATION_FEASIBILITY.md) verdict A.

## 4. Conflicts found in R1

| # | Quantity | Values | Configurations | Likely reason | Status |
|---|---|---|---|---|---|
| F16-C1 | Surface rate limits | "No sourced rate limit" (repository review of TP-1538) vs 60 / 80 / 120 deg/s and a 0.0495 s lag (arXiv extract citing Stevens & Lewis) | `F16-CFG-NASA-REF-TP1538` vs `F16-CFG-SIM-STEVENS-LEWIS` | The textbook added actuator values; their upstream primary source is not identified | Both recorded. Textbook values are `CROSS_VALIDATION_ONLY` until traced |
| F16-C2 | Nominal mass | 20,500 lb (637.16 slug; TP-1538 Table I) vs "approximately 647.2 slug" in "other NASA F-16 studies" (repository note, study not named) | `F16-CFG-NASA-REF-TP1538` vs unknown | Different simulation loading | Second value `UNRESOLVED`; never used |
| F16-C3 | Engine identity for Table VI | Not stated in the repository reading of TP-1538 | `F16-CFG-NASA-REF-TP1538` | Development-era simulation | Table VI stays tied to the simulation, never to an F100 variant |
| F16-C4 | Printed SI weight 91,188 N | TP-1538 Table I (F-16, 20,500 lb) and an F-15 report sentence "25,000 lbf (91,188 N)" | F-16 simulation vs F-15B 836 | Observation only (register R1-RN4) | No inference drawn |
| F16-C5 | "F-16" actuator/FCS values in academic codes | Several theses and repositories reuse textbook numbers | `F16-CFG-GENERIC-ACADEMIC` | Copying | Agreement is not confirmation |

## 5. Independence notes

- **Morelli 1998, Garza & Morelli, Stevens & Lewis, NESC, simupy-flight, AeroBench and most academic models all descend from TP-1538 data.** Agreement among them checks transcription and implementation. It does not check the aircraft.
- **Independent evidence about a real F-16** comes only from flight data: AFTI (`AIAA-84-2085`, AFTI airframe), VISTA/X-62A (`AIAA-2018-0525`, VISTA airframe), MATV flight test (`NTRS-19950007831`), pacer and air-data calibrations (`DTIC-ADA495484`, `AFFTC-TIM-04-01`) and engine flight tests (`CHILDRE-MCCOY-JPP-1989`, `ICAS-2008-286`). Each describes its own configuration.
- The NESC check-cases (`NASA-TM-2015-218675`) are **implementation-verification** evidence. A match proves the equations of motion and the table handling. It does not prove the aerodynamic data.
