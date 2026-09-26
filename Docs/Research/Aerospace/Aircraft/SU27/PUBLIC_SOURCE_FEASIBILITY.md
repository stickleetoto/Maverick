# Su-27 Public-Source Feasibility (SU27-R0)

**Question.** Can Maverick build a defensible public-source Su-27 research model without inventing major aerodynamic or flight-control data?

**Answer in one line.** No. For the baseline production Su-27 / Su-27S, only engine identity is strong; every model-defining number is `NOT_PUBLICLY_LOCATED`. The one designer-authored aerodynamics source (`ICAS-2002-POGOSYAN-SU27`) describes the model's *structure*; whether it prints any data is unknown.

Feasibility audit only. No model is built or proposed. Nothing here estimates coefficients from performance claims, photographs, game data or enthusiast models. Evidence: [`SOURCE_GRAPH.md`](SOURCE_GRAPH.md), [`KNOWN_DATA.md`](KNOWN_DATA.md), [`MISSING_DATA.md`](MISSING_DATA.md).

**How far the evidence goes.** Every source was seen only through web-search index extracts, because the document hosts are blocked in this session. The best level is `CONTENT_EXTRACT_VERIFIED`, and no page was opened. A page read could upgrade a few cells from "unknown" to "partial"; it would not change the answers below (see §3).

---

## 1. Field classification

| Class | Meaning |
|---|---|
| `STRONG_PUBLIC` | An official or primary public source gives the item for the named configuration, and the library has seen the content (extract or better) |
| `PARTIAL_PUBLIC` | A public source gives structure, qualitative features or a subset; not enough for a model |
| `VALIDATION_ONLY` | Public material usable only to check behaviour, not to build the model |
| `DERIVATIVE_ONLY` | Available only for a derivative (Su-30, Su-33, Su-27M/Su-35, Su-37, export or trainer variants). Never baseline authority |
| `CONFIGURATION_UNKNOWN` | A public source is known or believed to hold the item, but the configuration (T-10, T-10S, production, tunnel model, derivative) is not established, or the content was not seen |
| `NOT_PUBLICLY_LOCATED` | Searched; no public primary source found |

Main column: **baseline production Su-27 / Su-27S** (`SU27-CFG-BASELINE-SU27S`). Other columns show where material exists for configurations that must not be merged into it.

| # | Field | Baseline Su-27 / Su-27S | T-10 / T-10S, tunnel models, unspecified "Su-27" | Derivatives | Source(s) |
|---|---|---|---|---|---|
| 1 | Reference geometry (S, cbar, b, moment reference) | `NOT_PUBLICLY_LOCATED` | `CONFIGURATION_UNKNOWN` (tunnel data exist; scale and geometry unpublished) | — | `SU27-SUKHOI-TSAGI-AERO-MASS-DATA` (not public). Physical span and wing area circulate only in secondary and encyclopedic sources: identity, not recorded |
| 2 | Mass (defined loading) | `NOT_PUBLICLY_LOCATED` | — | — | Secondary weights have no loading definition; not recorded |
| 3 | CG | `NOT_PUBLICLY_LOCATED` | — | — | — |
| 4 | Inertia | `NOT_PUBLICLY_LOCATED` | — | — | — |
| 5 | Static aero coefficients | `NOT_PUBLICLY_LOCATED` | `CONFIGURATION_UNKNOWN`: the Sukhoi/TsAGI model is described in `ICAS-2002-POGOSYAN-SU27`; printed data unknown | — | ICAS 2002 (extract) |
| 6 | Rate / dynamic derivatives | `NOT_PUBLICLY_LOCATED` | `CONFIGURATION_UNKNOWN`: dynamic lag and hysteresis terms described (ICAS 2002), values unknown | — | ICAS 2002 |
| 7 | Control derivatives | `NOT_PUBLICLY_LOCATED` | `CONFIGURATION_UNKNOWN` (ICAS 2002 mentions aerodynamic and thrust-vector control effectiveness requirements) | Thrust-vector control belongs to derivatives only | ICAS 2002 |
| 8 | High-alpha aerodynamics | `NOT_PUBLICLY_LOCATED` (data) | `PARTIAL_PUBLIC` (structure only): asymmetric vortex breakdown, hysteresis, stall and spin covered (ICAS 2002). Tunnel domain alpha 0-180, beta +/-90 (T-105, 1987), data unpublished | — | `ICAS-2002-POGOSYAN-SU27`, `SU27-NIZH-2008-ZHELNIN`; method: `JAIRCRAFT-1994-GOMAN-KHRABROV` |
| 9 | FCS architecture | `PARTIAL_PUBLIC`: FBW with stability augmentation, subsonic static instability, AoA-scheduled leading-edge flaps, AoA/g limiting as a concept. Channel split and backup disputed in secondary sources | — | `DERIVATIVE_ONLY`: SDU-10MK (Su-30MK) | `SU27-TSAGI-CENTENARY-ARTICLE`, `SU27-SPACEPHYS-SDU-10MK` |
| 10 | FCS numeric gains and schedules | `NOT_PUBLICLY_LOCATED` | — | `DERIVATIVE_ONLY` lead (Su-30MK page, content unknown) | `SU27-SDU10-FCS-DESIGN-DATA` (not public) |
| 11 | Surface travel | `NOT_PUBLICLY_LOCATED` | — | — | Game and enthusiast values exist and are `PROHIBITED` |
| 12 | Actuator rates / dynamics | `NOT_PUBLICLY_LOCATED` | — | — | — |
| 13 | Engine identity | `STRONG_PUBLIC`: AL-31F afterburning turbofan with variable nozzle, Su-27 family (official UEC statement, extract level) | — | AL-31FP (vectoring) and AL-31FN named as variants | `SU27-UEC-2017-AL31F-NEWS` |
| 14 | Thrust data | `NOT_PUBLICLY_LOCATED` (deck). Only a 12,500 kgf class figure with no stated condition (identity, `PROHIBITED`) | — | — | `SU27-UEC-2017-AL31F-NEWS`, `SU27-AL31F-PERFORMANCE-DECK` (not public) |
| 15 | Engine dynamics | `NOT_PUBLICLY_LOCATED` | — | — | — |
| 16 | Trim data | `NOT_PUBLICLY_LOCATED` | — | — | — |
| 17 | Flight-test validation | `NOT_PUBLICLY_LOCATED` (quantitative). `VALIDATION_ONLY` at the qualitative level: the post-stall pitch-up and recovery (the Cobra) is publicly demonstrated and analysed, with no measured, configuration-identified values | ICAS 2002 says flight tests at Sukhoi and LII underpin the model; no data seen | — | `AIAA-93-0183`, `JAIRCRAFT-1995-ERICSSON-COBRA` (Western analyses, not Sukhoi data) |

**Count for the baseline column:** 1 `STRONG_PUBLIC` (engine identity), 1 `PARTIAL_PUBLIC` (FCS architecture), 1 `VALIDATION_ONLY` (qualitative flight behaviour), 14 `NOT_PUBLICLY_LOCATED`. The high-alpha structure is `PARTIAL_PUBLIC` only for the unspecified "Su-27" of the ICAS paper. Every quantity needed to integrate the equations of motion is `NOT_PUBLICLY_LOCATED`.

## 2. The three questions

### A. Is a source-honest Su-27 6-DOF model realistic?

**No.** Reference geometry, mass, CG, inertia, all three families of aerodynamic coefficients, FCS gains, surface travel, actuator rates, the thrust deck, engine dynamics and trim data are all `NOT_PUBLICLY_LOCATED` for the baseline Su-27 / Su-27S. A 6-DOF model would have to invent all of them. The only strong public item is engine identity. This is the same outcome as the F-22 audit. By contrast, the F-16, F-15 and F/A-18 packs each have at least one public NASA configuration with printed data; the Su-27 has none.

### B. Is only a partial research model realistic?

**Not with Su-27 numbers; only as a Su-27-*structured* model.**

- What public sources support is the **structure**: a TsAGI-style state-space unsteady aerodynamic model (flow-state variables with lag, giving hysteresis) as described for the Su-27 in `ICAS-2002-POGOSYAN-SU27`, following the method in `JAIRCRAFT-1994-GOMAN-KHRABROV`, plus a relaxed-stability fly-by-wire architecture with AoA-scheduled leading-edge flaps and AoA/g limiting, and twin AL-31F-class afterburning engines.
- Every parameter in that structure would be Maverick tuning. That makes it a research model *of the method*, not of the aircraft. It must not be called a Su-27 model.
- **Contingent upgrade.** If a page read of the ICAS 2002 paper shows printed coefficient curves for an identified configuration, a narrow longitudinal high-alpha slice (lift and pitching moment versus alpha, with hysteresis) could become `PARTIAL_PUBLIC` for that configuration. It would still lack mass, inertia, lateral-directional data, control effectiveness and gains, so it would be a validation curve, not a flyable model.

### C. Would a fictional Su-27-inspired adversary be more defensible?

**Yes. It is the defensible option.** Recommended framing:

1. **Name and label.** A fictional twin-engine, relaxed-stability, high-alpha-capable air-superiority adversary, "Flanker-class" at most. Never "Su-27" in model names, parameters or UI claims about fidelity.
2. **Numbers.** Every parameter is `MAVERICK_TUNING` or is taken from a *different, labelled* public configuration (for example textbook methods, or high-alpha shapes from a NASA research aircraft recorded under its own configuration ID). Nothing is relabelled as Su-27 data.
3. **Behaviour targets, qualitative only.** Post-stall pitch capability when the limiter is disabled, an AoA/g limiter in normal flight, twin afterburning engines of the class stated by UEC. Treat these as design goals, not as validated Su-27 behaviour.
4. **Excluded inputs.** No DCS, FlightGear or other game or enthusiast values, not even as tuning references. No Su-30, Su-33 or Su-35 data presented as Su-27. No unofficial flight-manual copies.
5. **Validation claim.** At most "trim-consistent" and "behaviourally plausible", in the library's vocabulary ([`../../Validation/README.md`](../../Validation/README.md)). Never "physically validated".

## 3. What would change the answer

| Change | Effect |
|---|---|
| Page read of `ICAS-2002-POGOSYAN-SU27` | Establishes whether any coefficient or hysteresis curve is printed, and for which configuration. At best a narrow validation slice |
| Lawful copy of `SU27-BYUSHGENS-1998-TSAGI-MONOGRAPH` checked for Su-27 or "integral layout" data | Possibly configuration-labelled tunnel trends; configuration identity likely remains the obstacle |
| An open TsAGI or Sukhoi publication printing tunnel data with model scale and geometry state | `CONFIGURATION_UNKNOWN` → `PARTIAL_PUBLIC` for that tunnel model |
| An official mass, CG and inertia statement with a defined loading | Mass closure for that loading |
| Official release of the flight manual | Operating limits (surface travel and limiter settings, if printed) |

None of these alone makes a defensible Su-27 6-DOF model. Together they would still leave FCS gains, actuator data and the engine deck missing.
