# Propulsion Fundamentals

Scope: turbojet/turbofan fundamentals; installed vs uninstalled thrust; inlet recovery; ram drag; gross vs net thrust; thrust lapse with altitude and Mach; spool dynamics; fuel flow; nozzle modelling; engine installation loads.

Maverick's architecture is **Profile + Engine** (`Docs/FlightDynamics/SHARED_PROPULSION_PROFILE_ARCHITECTURE_V0.2.md`). Engine data is shared across aircraft only when engine *and* installation compatibility is established.

---

## Planned subfolders

| Subfolder | Covers |
|---|---|
| `Turbojet_Turbofan/` | Cycle fundamentals, afterburning |
| `Inlets/` | Recovery, distortion, high-alpha inlet behaviour |
| `ThrustDecks/` | Tabulated thrust vs Mach/altitude/power, with provenance |
| `SpoolDynamics/` | Lag models, rate limiters, spool-up/down |
| `InstalledEffects/` | Installed vs uninstalled, ram drag, nozzle/afterbody interaction, thrust-line geometry |

## Starter references (r0)

| Source ID | Use | Level |
|---|---|---|
| `NASA-TP-3001` (Ray, 1990) | In-flight thrust calculation methods for the F404-GE-400; uninstalled gross thrust accuracy 1–4 %. Defines gross/net and installed/uninstalled bookkeeping in practice | L2 |
| `NASA-TM-4140` (Conners, 1989) | Measurement-error sensitivity of in-flight net thrust | L2 |
| `NASA-TM-4240` (Johnson, 1990) | Simplified real-time engine dynamics: throttle rate limiter + low-pass filter, TV axial-thrust-loss method, accuracy vs full model | L2 |
| `NASA-TM-88273` (Walton & Burcham, 1988) | Nozzle-exit survey of an afterburning turbofan in an altitude facility | L2 |
| `NASA-TM-104329`, `NTRS-19990024943` | Inlet distortion at high α and during departures (F/A-18A HARV) | L2 / L3 |

The existing Maverick F-16 thrust deck (NASA TP-1538 Table VI, F100 class) is documented in `Docs/Reference/F16_TP1538_THRUST_DECK_V0.1.md`. **It must never be reused for another engine or installation.**

## Bookkeeping rules for any implementation

- State whether a deck is **installed or uninstalled**, and **gross or net**. Ram drag is either inside the deck or modelled separately, never both.
- Thrust acts at the installed thrust line (position, cant, toe). Propulsive moments come from that geometry, not from aerodynamic coefficients.
- Fact-sheet thrust figures (e.g. "16,000 lb class") are identity data. `implementation_use = PROHIBITED`.

## Collection targets (not yet searched)

- Public NASA references on installed-thrust accounting (thrust/drag bookkeeping).
- Public NASA Glenn / Lewis engine-deck or cycle-model references.
- Public nozzle/afterbody interaction studies for twin-engine fighters.
