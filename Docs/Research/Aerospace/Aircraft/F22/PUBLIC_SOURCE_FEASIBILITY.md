# F-22 Public-Source Feasibility (R1)

**Feasibility audit only. No F-22 model is built or proposed.** Nothing here reconstructs unavailable military data or estimates coefficients from performance claims.

Evidence: [`SOURCE_GRAPH.md`](SOURCE_GRAPH.md), [`KNOWN_DATA.md`](KNOWN_DATA.md), [`MISSING_DATA.md`](MISSING_DATA.md), and the repository catalogue `Docs/Reference/F22_PUBLIC_SOURCE_CATALOG_V0.1.md` (branch `sol/fa18-source-consolidation-r1` @e596c32).

---

## 1. Field classification

| Class | Meaning |
|---|---|
| `STRONG_PUBLIC` | Public primary source gives the value for the named configuration |
| `PARTIAL_PUBLIC` | Public source gives structure, targets or a subset; not enough for a model |
| `VALIDATION_ONLY` | Public data usable only to check behaviour |
| `GENERIC_CONFIGURATION_ONLY` | Available only for a generic or unrelated shape |
| `YF22_ONLY` | Available only for the YF-22 prototype |
| `CONFIGURATION_UNKNOWN` | Data exist but the tested configuration is not established |
| `NOT_PUBLICLY_LOCATED` | Searched; nothing public found |

| Field | Production F-22A | EMD | YF-22 | Tunnel models | Source(s) |
|---|---|---|---|---|---|
| Identity: engines, nozzle type | `STRONG_PUBLIC` | `STRONG_PUBLIC` | `YF22_ONLY` (engine competition) | — | `USAF-F22-FACT-SHEET`, `PW-F119-PRODUCT-PAGE` |
| Physical span / length / height | `STRONG_PUBLIC` (identity) | — | — | — | fact sheet |
| Reference area S, MAC, reference span | `NOT_PUBLICLY_LOCATED` | `NOT_PUBLICLY_LOCATED` | `NOT_PUBLICLY_LOCATED` | `CONFIGURATION_UNKNOWN` (scaled models) | — |
| Mass state (defined loading), CG | `NOT_PUBLICLY_LOCATED` (fact-sheet weights have no loading definition) | `NOT_PUBLICLY_LOCATED` | — | — | fact sheet (identity only) |
| Inertias | `NOT_PUBLICLY_LOCATED` | `NOT_PUBLICLY_LOCATED` | `NOT_PUBLICLY_LOCATED` | — | — |
| Static aerodynamic coefficients | `NOT_PUBLICLY_LOCATED` | `NOT_PUBLICLY_LOCATED` | `NOT_PUBLICLY_LOCATED` | `CONFIGURATION_UNKNOWN` (`AIAA-99-4015` unread; `AIAA-2013-0972` CFD unassessed) | — |
| Dynamic / rotary derivatives | `NOT_PUBLICLY_LOCATED` | — | — | `CONFIGURATION_UNKNOWN` (`AIAA-99-4015`) | — |
| High-alpha behaviour | — | `VALIDATION_ONLY` (below -40 to above +60 deg AOA explored) | — | — | `SFTE-2000-PERON-F22-HIGH-AOA` |
| FCS architecture | `PARTIAL_PUBLIC` (triplex FLCS, pitch TV integration) | `PARTIAL_PUBLIC` (1996 design philosophy) | `YF22_ONLY` (prototype TV manoeuvring; PIO case) | — | `AIAA-96-3379`, `SFTE-2000-PERON-F22-HIGH-AOA`, `AIAA-94-2105`, NRC 1997 |
| Handling-quality targets | — | `PARTIAL_PUBLIC` (CAP and damping target bands, 1996) | — | — | `AIAA-96-3379` |
| FCS gains, actuator limits and rates | `NOT_PUBLICLY_LOCATED` | `NOT_PUBLICLY_LOCATED` | `NOT_PUBLICLY_LOCATED` | — | — |
| Thrust-vector range | `STRONG_PUBLIC` as identity (+/-20 deg pitch) | — | `YF22_ONLY` | — | P&W page |
| Installed thrust deck | `NOT_PUBLICLY_LOCATED` (proprietary) | `PARTIAL_PUBLIC` (model architecture and accuracy statements only) | — | nozzle-flap internal performance (`CONFIGURATION_UNKNOWN` variant) | `RTO-MP-SCI-162-P23`, `NTRS-20050169937` |
| Engine dynamics | `NOT_PUBLICLY_LOCATED` | `PARTIAL_PUBLIC` (architecture) | — | — | RTO paper |
| Buffet | — | — | — | `CONFIGURATION_UNKNOWN` (early 13.3 % model; V-9 model) | `NTRS-20000052124`, `JAIRCRAFT-2006-ANDERSON-F22-BUFFET` |
| Trim / trajectory data | `NOT_PUBLICLY_LOCATED` | `NOT_PUBLICLY_LOCATED` | — | — | — |
| Generic "F-22-like" configuration data | — | — | — | `GENERIC_CONFIGURATION_ONLY` (ICE 101 is **not** an F-22) | `AFRL-VA-WP-TR-1998-3043` |

## 2. The three questions

### Is a defensible production F-22 6-DOF model possible from public sources?

**No.** Reference geometry, mass properties, inertias, every aerodynamic coefficient, every FCS gain, actuator data and the installed thrust deck are all `NOT_PUBLICLY_LOCATED` for the production F-22A. A model built on public data would have to invent all of them. That fails the library's quality bar and is not proposed.

### Is a YF-22 research model possible?

**No.** The public YF-22 material (`AIAA-94-2105`, the NRC PIO case, NASA history) is narrative and manoeuvre-level. No YF-22 coefficient set, mass-property set or FCS gain set was located. `YF22_ONLY` data also never transfer to the F-22.

### Is only a generic educational model possible?

**Yes, and only if it is labelled as generic.** A model assembled from textbook methods, with an F-22-like planform chosen for gameplay and tuning labelled `MAVERICK_TUNING`, can be built. It must be called a generic high-performance fighter with F-22-like identity, not an F-22 model. The only public F-22 data it could honestly use are:

- identity data (twin engines, 2-D pitch vectoring to +/-20 deg) as feature choices, not as numbers to fit;
- the 1996 CAP and damping target bands (`AIAA-96-3379`) as handling-quality **goals** for a tuned controller, cited as development-era targets;
- the EMD AOA envelope statement as a **behavioural boundary** for playtesting, not as an aerodynamic-data domain.

## 3. What would change the answer

| Change | Effect |
|---|---|
| Public release of the 1992 full-scale-tunnel / 1993 spin-tunnel data packages | Tunnel-model aerodynamics for a known configuration; still not production |
| Full text of `AIAA-99-4015` with model geometry and date | Dynamic derivatives for a known tunnel state |
| `AIAA-2013-0972` printing F-22 derivatives | CFD derivative comparisons (validation only) |
| Any public mass-property statement with a defined loading | Mass closure for that loading |

None of these makes a production model defensible by itself.
