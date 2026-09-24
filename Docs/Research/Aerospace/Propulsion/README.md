# Propulsion Fundamentals

Scope: turbojet/turbofan fundamentals; installed vs uninstalled thrust; inlet recovery and distortion; ram drag; gross vs net thrust; in-flight thrust determination; thrust lapse with altitude and Mach; spool dynamics; fuel flow; nozzle modelling; engine installation loads.

Maverick's architecture is **Profile + Engine** (`Docs/FlightDynamics/SHARED_PROPULSION_PROFILE_ARCHITECTURE_V0.2.md`). Engine data is shared across aircraft only when engine *and* installation compatibility is established. **An engine variant or build is part of the configuration.** A serial number is not.

---

## Planned subfolders

| Subfolder | Covers |
|---|---|
| `Turbojet_Turbofan/` | Cycle fundamentals, afterburning |
| `Inlets/` | Recovery, distortion, high-alpha inlet behaviour |
| `ThrustDecks/` | Tabulated thrust vs Mach/altitude/power, with provenance |
| `InFlightThrust/` | Gas-generator and simplified gross-thrust methods, uncertainty |
| `SpoolDynamics/` | Lag models, rate limiters, spool-up/down |
| `InstalledEffects/` | Installed vs uninstalled, ram drag, nozzle/afterbody interaction, thrust-line geometry |

## References

| Source ID | Use | Level |
|---|---|---|
| `AGARD-AG-237` **R1** | Guide to in-flight thrust measurement: gross/net and installed/uninstalled bookkeeping | CAT |
| `NASA-TP-3001` (Ray, 1990) | In-flight thrust techniques for the F404-GE-400; uninstalled gross thrust accuracy 1-4 % | ABS |
| `NASA-TM-4140` (Conners, 1989) | Measurement-error sensitivity of in-flight net thrust | ABS |
| `NASA-TM-4591` (Ray) **R1** | Dynamic response of in-flight thrust calculations during throttle transients | LEAD |
| `NASA-TM-104247` (Yuhas & Ray) **R1** | Bleed-air effects on installed F404 thrust (HARV on a thrust stand) | LEAD |
| `NASA-TP-1373`, `NASA-TP-1482`, `NASA-TP-1782` **R1** | Facility vs calculated F100 thrust and airflow; simplified gross-thrust method in the facility and in flight | ABS / LEAD |
| `NASA-TM-X-3261`, `NASA-TP-1034` **R1** | Real-time hybrid F100 engine simulations (structure of a transient model) | LEAD / CAT |
| `NASA-TM-4240` (Johnson, 1990) | Simplified real-time engine dynamics: throttle rate limiter + low-pass, TV axial-thrust-loss method | ABS |
| `NASA-TM-88273` (Walton & Burcham, 1988) | Nozzle-exit survey of an afterburning turbofan in an altitude facility | ABS |
| `NASA-TM-104329`, `NTRS-19990024943`, `NASA-CR-198052` | Inlet distortion and airflow estimation at high alpha and in departures (F/A-18 HARV) | ABS / EXTRACT / LEAD |
| `NASA-CR-144866`, `NASA-TP-2411` **R1** | F-15 inlet/engine distortion methodology | LEAD |
| `RTO-MP-SCI-162-P23` **R1** | Installed-thrust model architecture and validation statements (F119) | LEAD |

## Engine lineages in the packs

| Engine | Pack | Numeric status |
|---|---|---|
| F404-GE-400 | [F/A-18](../Aircraft/FA18/SOURCE_GRAPH.md) | Table topology and a digitized Max-AB figure; arrays not printed |
| F100 family | [F-15](../Aircraft/F15/F15_PROPULSION_SOURCE_GRAPH.md), [F-16](../Aircraft/F16/F16_PROPULSION_SOURCE_GRAPH.md) | Normalised PW-100(3) curves; no public dimensional deck for any variant |
| TP-1538 simulated engine | [F-16](../Aircraft/F16/F16_PROPULSION_SOURCE_GRAPH.md) | Table VI (repository transcription); variant unstated |
| F110-GE-100 / -129 | F-16 | Research installations only |
| F119-PW-100 | [F-22](../Aircraft/F22/PUBLIC_SOURCE_FEASIBILITY.md) | Architecture statements only; deck proprietary |

The Maverick F-16 thrust deck (NASA TP-1538 Table VI) is documented in `Docs/Reference/F16_TP1538_THRUST_DECK_V0.1.md`. **It must never be reused for another engine or installation.**

## Bookkeeping rules for any implementation

- State whether a deck is **installed or uninstalled**, and **gross or net**. Ram drag is either inside the deck or modelled separately, never both.
- Thrust acts at the installed thrust line (position, cant, toe). Propulsive moments come from that geometry, not from aerodynamic coefficients.
- Fact-sheet thrust figures (e.g. "16,000 lb class", "35,000 lb class") are identity data: `PROHIBITED`.
- A normalised curve (fraction of an unstated maximum) is never scaled with a figure from a different build or document.
- Classified or restricted specifications (e.g. F100 CP2903B) are never reconstructed or inferred from.
