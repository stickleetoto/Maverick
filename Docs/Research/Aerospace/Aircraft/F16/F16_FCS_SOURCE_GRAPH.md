# F-16 Flight-Control Source Graph (R1)

Parent: [`SOURCE_GRAPH.md`](SOURCE_GRAPH.md). Library rule (inherited from the FDM architecture): **no controller is called the real F-16 FLCS unless it is sourced and validated.** Nothing below meets that bar for a production block.

---

## 1. Four-layer matrix

Each cell names the source that supports the claim, or says that none was located. "Public" means publicly released. It does not mean the library has read the page.

| Axis / element | Layer 1: public architecture | Layer 2: public numeric gains | Layer 3: research approximations | Layer 4: production, not located |
|---|---|---|---|---|
| Pitch (Nz / AoA command, AoA and g limiting) | `DROSTE-WALKER-F16-FBW` (manufacturer case study, paywalled; content not seen); `NASA-TP-1538` (simulated F-16-type CAS with AoA feedback and limiter, repository reading); `NASA-TN-D-8176` (prototype AoA and Nz limiters) | **None located for any production block.** TP-1538 *likely* prints its simulated law's gains (not confirmed by the repository or the library) | `AFIT-GGC-EE-77-7` (pitch-rate command thesis); textbook example laws (`STEVENS-LEWIS-ACS`); NESC check-case control subsystems | F-16A/B analog FLCS and F-16C/D DFLCS gain schedules (`F16-PRODUCTION-FLCS-OFP`) |
| Roll (roll-rate command, limiting) | TP-1538 (simulated; "limited to only about 165 deg/s", repository) | None | AFTI DFCS research (`NTRS-19840012524`, `NASA-CR-4226`) | Production roll-rate limiter schedule |
| Yaw (ARI, yaw damper) | `NASA-TN-D-8176` (prototype ARI and stability-axis yaw damper); TP-1538 | None | VISTA lateral-directional designs (`AIAA-95-3249`, `ROBUST-MULTIVAR-FC-1994`) | Production ARI gains |
| Leading-edge flap schedule | TP-1538: scheduled with alpha and q/Ps (repository) | Schedule shown as a figure in TP-1538 (repository; not transcribed) | Morelli model holds LEF fixed at 25 deg | Production LEF schedule |
| Actuators | `DTIC-ADA213334` (horizontal tail / flaperon ISA hardware); TP-1538 stabilator actuator model in fig. 63 (repository: not transcribed) | **Positions:** TP-1538 Table I (+/-25, +/-21.5, +/-30, +/-5.375 differential, 60 speed brake), repository reading. **Rates: none sourced** | Textbook 60 / 80 / 120 deg/s and 0.0495 s (`ARXIV-1907-11913` extract citing Stevens & Lewis) | Production actuator performance specifications |
| Digital hardware | `NASA-TP-2004-212046` (Block 40 DFLCCs at 64 Hz, **in the F-16XL**) | n/a | F-16XL laws re-hosted on DFLCCs | Production OFP |
| Variable-stability / model-following | `AIAA-2023-1746` (X-62A); VISTA VSS (public histories) | None | `AFIT-GE-ENG-95D-26` (multiple-model adaptive control) | VISTA VSS internals |
| Thrust-vector control | `AIAA-94-3513`, `NTRS-19950007831` (MATV) | Not seen | MATV control laws | n/a (research only) |
| Departure/spin-resistance techniques | `NASA-TP-1689` (Langley control techniques); TP-1538 recovery modifications | Not seen | same | n/a |

## 2. Lineage

```
General Dynamics F-16 FBW design ──▶ DROSTE-WALKER-F16-FBW (AIAA case study; paywalled; numeric content UNKNOWN)
        │                             └── the only manufacturer-authored public FLCS description found
        │
        ├─ production analog FLCS (A/B, early C/D) ── gains NOT PUBLICLY LOCATED
        └─ production digital FLCS (Block 40 on) ─── gains NOT PUBLICLY LOCATED
                    │ hardware reused
                    ▼
            F-16XL research: NASA-TP-2004-212046 (F-16XL laws on Block 40 DFLCCs, 64 Hz, flights Dec 1997 - Mar 1998)

NASA Langley simulated F-16 CAS
  NASA-TN-D-8176 (prototype automatic control system) ──BUILDS_ON──▶ NASA-TP-1538 (RSS, deep-stall recovery modifications)
                                                                     └──▶ NASA-TP-1689 (departure/spin-resistance techniques)

Research laws on research airframes (never production)
  AFTI/F-16: NTRS-19840012524 (DFCS experience), NTRS-19840007091, NTRS-19840012499, NASA-CR-4226
  VISTA:     AIAA-95-3249, ROBUST-MULTIVAR-FC-1994, AFIT-GE-ENG-95D-26, AIAA-97-3787, AIAA-2023-1746
  MATV:      AIAA-94-3513 ─▶ NTRS-19950007831

Textbook / academic laws (cross-validation only)
  STEVENS-LEWIS-ACS ─▶ ARXIV-1907-11913, ARCH-2018-HEIDLAUF (autopilots), NESC check-case control subsystems
```

## 3. Verdict for a "source-honest FCS approximation" (verdict C)

A source-honest approximation is possible if it is labelled as a **research law with the published F-16 architecture**. It would use:

- the architecture elements in Layer 1 (Nz/AoA command with limiting, roll-rate command, ARI and yaw damper);
- TP-1538 Table I position limits for the TP-1538 configuration only, after page verification;
- handling-quality targets from `MIL-HDBK-1797` as the tuning objective, with tuned gains labelled `MAVERICK_TUNING`;
- **no** actuator rate taken from the textbook lineage without a primary trace. Until then the rate must be a labelled tuning value, or come from the TP-1538 fig. 63 actuator model once that figure is transcribed.

It must never be called "the F-16 FLCS". The Droste & Walker case study is the one source that could raise the architecture layer to manufacturer authority. It is paywalled, and its numeric content is unknown.

## 4. Open FCS questions

| ID | Question | Where to look |
|---|---|---|
| F16-Q-C1 | Does TP-1538 print the numeric gains of its simulated CAS? | TP-1538 control-system section and figures (page read needed) |
| F16-Q-C2 | Does TP-1538 fig. 63 give a stabilator actuator model with a rate limit? | TP-1538 fig. 63 (repository notes it; not transcribed) |
| F16-Q-C3 | Does the Droste & Walker case study print gains, schedules or actuator rates? | AIAA Library of Flight case study (paywalled) |
| F16-Q-C4 | What is the primary source of the textbook 60/80/120 deg/s rates and the 0.0495 s lag? | Stevens & Lewis bibliography |
