# F-16 Propulsion Source Graph: F100 / F110 lineage (R1)

Parent: [`SOURCE_GRAPH.md`](SOURCE_GRAPH.md). Shared F100 lineage with the F-15: [`../F15/F15_PROPULSION_SOURCE_GRAPH.md`](../F15/F15_PROPULSION_SOURCE_GRAPH.md).

**Rule: engine variants are never merged.** F100-PW-100 (and its builds (1), (2), (2-7/8), (3)), -200, -220, -220E and -229 are separate engines. So are F110-GE-100 and -129. A serial number is not a configuration: one F100 serial (P680063) flew in four configurations between 1972 and 1994 (repository reading of `NTRS-19990064011`).

---

## 1. Engine configuration registry (F-16 relevant)

| Configuration ID | Engine | F-16 installations (general public identity; not a library-verified claim) | Public numeric performance sources located |
|---|---|---|---|
| `ENG-F100-PW-200` | F100-PW-200 | F-16A/B | None with a deck. `ICAS-2008-286` builds an F-16A/B flight thrust deck; the F100 variant is to be confirmed from the paper |
| `ENG-F100-PW-220` | F100-PW-220 | F-16 (and F-15) | `CHILDRE-MCCOY-JPP-1989` (flight test in the F-16; paywalled); `DTIC-ADA495484` (F-16B pacer with F100-PW-220 per catalogue listing) |
| `ENG-F100-PW-229` | F100-PW-229 | F-16C/D Block 52; later NF-16D VISTA (public histories) | None located |
| `ENG-F110-GE-100` | F110-GE-100 | F-16C/D Block 30/40/50 family; VISTA/MATV with AVEN | MATV reports (`AIAA-94-3513`, `NTRS-19950007831`) for the vectoring installation only |
| `ENG-F110-GE-129` | F110-GE-129 | F-16C/D; F-16XL-2 | `NASA-TM-104326` (exhaust flow properties in the F-16XL) |
| *(unnamed)* | TP-1538 simulated engine | TP-1538 simulation (`F16-CFG-NASA-REF-TP1538`) | **TP-1538 Table VI**: idle / military / maximum thrust vs Mach 0.2-1.0 and altitude 0-15,240 m, page-transcribed and cross-checked by the repository (`Docs/Reference/Data/F16/TP1538/manifest.json`). Engine variant not stated |

## 2. Lineage

```
F100 family (Pratt & Whitney)
 ├─ F100-PW-100 builds (F-15 era): see F15_PROPULSION_SOURCE_GRAPH.md
 │     TM X-3261 (PW-100(1) hybrid simulation), TP-1034 / TP-1056 (PW-100(3)), TP-1373 / TP-1482 / TP-1782 /
 │     TP-1069-TP-1228 (prototype series 2-7/8), NTRS-19990064011 + NASA-TM-84908 (P680063 chronology, DEEC),
 │     NASA-TM-85902 (EMD). F100-SPEC-CP2903B is classified: NEVER reconstruct.
 │
 ├─ F100-PW-200 (F-16A/B) ───────▶ ICAS-2008-286 (F-16A/B flight thrust deck from in-flight engine measurements)
 │                                 EUCASS-F100-THRUST-DECK (companion lead; not confirmed)
 ├─ F100-PW-220 (F-15, F-16) ────▶ CHILDRE-MCCOY-JPP-1989 (flight test in the F-16)
 │                                 DTIC-ADA495484 (F-16B pacer calibration)
 │                                 [search extract, attribution uncertain: "F100-PW-200 with DEEC ... closely approximates the
 │                                  F100-PW-220 that powers the F-15 and F-16". Not used]
 └─ F100-PW-229 ─────────────────▶ no performance source located (also NF-15B 837, with vectoring nozzles)

F110 family (General Electric)
 ├─ F110-GE-100 ─── + AVEN (17 deg) ─▶ MATV: AIAA-94-3513, NTRS-19950007831 (1993-1994, 95 flights per public summaries)
 └─ F110-GE-129 ─────────────────────▶ NASA-TM-104326 (flight and static exhaust flow, F-16XL; acoustics context)

Simulation engines (not a named engine)
  NASA-TP-1538 Table VI thrust ──DERIVED_FROM──▶ NASA-TM-2003-212145 engine model (Pc gearing, RTAU power lag)
                                                 └─▶ STEVENS-LEWIS-ACS engine (textbook) ─▶ NESC / simupy / AeroBench
```

## 3. What is dimensional and what is not

| Item | Dimensional? | Configuration | Status |
|---|---|---|---|
| TP-1538 Table VI (3 power levels x 5 Mach x 6 altitudes) | **Yes**, N and lbf | TP-1538 simulation | The only dimensional F-16 thrust table the project holds. Repository: two-pass visual transcription with SI/US cross-check (90 of 90 cells). Library: `BLOCKED_PENDING_PAGE_VERIFICATION` because the library has not read the page. Installed/uninstalled and gross/net bookkeeping are **not stated** in the repository reading |
| Garza & Morelli power lag (Pc = 64.94 dth for dth <= 0.77; 217.38 dth - 117.38 above; RTAU piecewise) | Dynamics, not thrust | TP-1538 simulation | Repository page reading; library blocked |
| Engine angular momentum 160 slug-ft^2/s (216.9 kg m^2/s) | Yes | TP-1538 simulation | Repository page reading; not implemented in the repository |
| `ICAS-2008-286` flight thrust deck | Yes (if tabulated in the paper) | F-16A/B with F100 | Abstract only. Whether numbers are printed is unknown |
| Fact-sheet thrust classes | Identity only | any | `PROHIBITED` for modelling |

## 4. Verdict for dimensional propulsion (verdict D)

- **TP-1538 simulation configuration:** a dimensional propulsion model is **supported** from Table VI plus the Garza & Morelli power lag, both tied to the simulation and not to a named engine. The library still needs its own page verification before it can mark any value `ALLOWED`.
- **Production F-16 with a named engine:** **not supported.** No public installed deck for any F-16 engine variant was located. `ICAS-2008-286` is the one lead that might change this for the F-16A/B with F100.
- **VISTA/MATV thrust vectoring:** architecture and flight-test narrative only.

## 5. Open propulsion questions

| ID | Question | Where to look |
|---|---|---|
| F16-Q-P1 | Is TP-1538 Table VI installed or uninstalled, gross or net? | TP-1538 App. B text around Table VI |
| F16-Q-P2 | Does `ICAS-2008-286` print its deck, and for which F100 variant? | ICAS 2008 paper 286 (author-hosted) |
| F16-Q-P3 | Does `CHILDRE-MCCOY-JPP-1989` print installed thrust or only operability results? | J. Propulsion and Power, 1989 |
| F16-Q-P4 | Which engine did the TP-1538 simulation represent? | TP-1538 App. B |
