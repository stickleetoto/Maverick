# Aircraft Data Coverage Matrix (R1, R2 update)

Coverage of the data a 6-DOF model needs, for the four aircraft packs. **There is no overall score or ranking.** Each cell is judged for one named configuration per aircraft, because coverage for "the F-16" does not exist, only coverage for an F-16 configuration.

| Aircraft | Configuration judged | Why this one |
|---|---|---|
| F-16 | `F16-CFG-NASA-REF-TP1538` / `F16-CFG-SIM-MORELLI1998` | Maverick's reference F-16; the only F-16 configuration with a public coefficient set |
| F-15 | `F15-CFG-NASA836-PRE-QS` | Maverick's F-15 target |
| F/A-18 | `FA18-CFG-HARV-P1-BASIC` (with `FA18-CFG-SIM-F18BAS` where stated) | r0 target recommendation |
| F-22 | `F22-CFG-PROD-F22A` | The only F-22 configuration anyone would want |
| F-16 (R2) | `F16-CFG-NESC-CHECKCASE` | The first configuration the library verified from primary files (NASA DAVE-ML); a validation configuration, not a replacement for the reference F-16 |

## Status vocabulary (only these six are used)

| Status | Rule |
|---|---|
| `STRONG_PUBLIC` | A public primary source for **this configuration** gives the item, the library has seen the item's content (level `CONTENT_EXTRACT_VERIFIED` or better, or an official public page), and no attribution conflict is open |
| `PARTIAL_PUBLIC` | Public, matching configuration, but only structure, a subset, targets, a normalised shape or one condition |
| `NOT_PAGE_VERIFIED` | A public source for this configuration is believed to hold the item (repository page reading, abstract or catalogue), but the library has not confirmed the content. **This is the expected state of most repository-read items** |
| `VALIDATION_ONLY` | Public data for this configuration that can only check behaviour (flight trends, time histories, check-cases, envelopes) |
| `CONFIGURATION_MISMATCH` | Public data exist only for a different configuration (another airframe, scale model, research modification, simulation) |
| `NOT_PUBLICLY_LOCATED` | Searched and time-boxed; nothing public found |

Source IDs are in [`SOURCE_INDEX.json`](SOURCE_INDEX.json); values in [`AIRCRAFT_NUMERIC_FIELD_INDEX.json`](AIRCRAFT_NUMERIC_FIELD_INDEX.json).

---

## Matrix

| Field | F-16 (TP-1538 / Morelli simulation) | F-15 (NASA 836 pre-Quiet-Spike) | F/A-18 (HARV Phase 1 / f18bas) | F-22 (production F-22A) | F-16 NESC check-case (R2, file-verified) |
|---|---|---|---|---|---|
| Reference area S | `NOT_PAGE_VERIFIED`: TP-1538 Table I (repository) | `NOT_PUBLICLY_LOCATED` for 836; `CONFIGURATION_MISMATCH` elsewhere (837, No. 8, AFIT) | `NOT_PAGE_VERIFIED`: 400 ft^2 in extract (TM-4772 attribution open) and TP-97-206539 Table 3 (repository) | `NOT_PUBLICLY_LOCATED` | `STRONG_PUBLIC`: 300 ft^2, file-verified (`F16_aero.dml` line 380) |
| Mean aerodynamic chord | `NOT_PAGE_VERIFIED` (TP-1538 Table I) | `NOT_PUBLICLY_LOCATED` (family 15.94 ft only via reproductions) | `NOT_PAGE_VERIFIED` (11.52 / 11.523 ft) | `NOT_PUBLICLY_LOCATED` | `STRONG_PUBLIC`: 11.32 ft (line 368) |
| Reference span | `NOT_PAGE_VERIFIED` (30 ft; physical-vs-reference wording open) | `NOT_PUBLICLY_LOCATED` (42.8 ft is physical) | `NOT_PAGE_VERIFIED` (37.4 / 37.42 ft) | `NOT_PUBLICLY_LOCATED` | `STRONG_PUBLIC`: 30 ft (line 374) |
| Physical dimensions | `NOT_PUBLICLY_LOCATED` (the simulation defines reference geometry only) | `NOT_PAGE_VERIFIED`: 63.7 / 42.8 / 18.7 ft (TM-4782 and others, repository) | `STRONG_PUBLIC`: NASA fact page 37 ft 5 in span (identity) | `STRONG_PUBLIC`: fact sheet span/length/height (identity only) | `NOT_PUBLICLY_LOCATED` (the model defines reference geometry only) |
| Datum and moment reference | `NOT_PAGE_VERIFIED`: xcg_ref 0.35 cbar (TP-1538/Morelli) | `PARTIAL_PUBLIC`: 28 % MAC = FS 561.7 (one equation, two unknowns; repository); family datum equation | `NOT_PAGE_VERIFIED`: FS 454.33 / WL 105.24 at 21.9 % MAC (repository) | `NOT_PUBLICLY_LOCATED` | `STRONG_PUBLIC`: MRC 35 % MAC; CM offset formula (`F16_inertia.dml` line 141) |
| Mass (defined loading) | `NOT_PAGE_VERIFIED`: 20,500 lb (Table I) | `NOT_PAGE_VERIFIED`: 37,426 lb at 8,000 lb fuel (Table 1 Baseline; conflict F15-X1 resolved in repository) | `NOT_PAGE_VERIFIED`: 31,980 lb at 6,480 lb fuel (extract + repository) | `NOT_PUBLICLY_LOCATED` (fact-sheet weight has no loading) | `STRONG_PUBLIC`: 637.1595 slug (20,500 lbm), constant |
| CG | `NOT_PAGE_VERIFIED` (reference and demonstration CG only) | `NOT_PAGE_VERIFIED` (26.34 % MAC) | `NOT_PAGE_VERIFIED` (21.9 % MAC) | `NOT_PUBLICLY_LOCATED` | `STRONG_PUBLIC`: 25 % MAC for the check cases (README) |
| Inertias incl. Ixz | `NOT_PAGE_VERIFIED` (Table I) | `NOT_PAGE_VERIFIED` (Table 1 Baseline) | `NOT_PAGE_VERIFIED` (Ixx/Iyy/Izz in extract; Ixz -2,039 repository only) | `NOT_PUBLICLY_LOCATED` | `STRONG_PUBLIC`: 9,496 / 55,814 / 63,100 / 982; Ixy = Iyz = 0 |
| Fuel-dependent mass schedule | `NOT_PUBLICLY_LOCATED` | `NOT_PUBLICLY_LOCATED` | `NOT_PUBLICLY_LOCATED` | `NOT_PUBLICLY_LOCATED` | `NOT_PUBLICLY_LOCATED` (constant-mass model) |
| Static aero coefficients, 6 axes | `NOT_PAGE_VERIFIED`: Morelli polynomials, M < 0.6 (repository page read) | `NOT_PUBLICLY_LOCATED`; `CONFIGURATION_MISMATCH` (AFIT fits, RPV, 837) | `CONFIGURATION_MISMATCH`: f18bas/f18harv arrays **not printed** (repository); tunnel/rotary data for scale models | `NOT_PUBLICLY_LOCATED` | `STRONG_PUBLIC`: table model, all six axes (Stevens & Lewis via Morelli MATLAB) |
| Rate / dynamic derivatives | `NOT_PAGE_VERIFIED` (Morelli q, p, r terms) | `NOT_PUBLICLY_LOCATED` | `VALIDATION_ONLY`: flight-derived (TM-4786, basic configuration); `CONFIGURATION_MISMATCH`: CR-3608 1/10-scale rotary | `NOT_PUBLICLY_LOCATED`; tunnel lead `AIAA-99-4015` unread | `STRONG_PUBLIC`: nine damping tables vs alpha |
| Control derivatives | `NOT_PAGE_VERIFIED` (Morelli) | `NOT_PUBLICLY_LOCATED` | `VALIDATION_ONLY` (flight-derived) | `NOT_PUBLICLY_LOCATED` | `STRONG_PUBLIC`: elevator in CX0/Cm0/CZ1; four lateral-directional control-power tables |
| High-alpha aero (> 45 deg) | `CONFIGURATION_MISMATCH` (VISTA model to +90 deg) | `CONFIGURATION_MISMATCH` (RPV, rotary tunnel) | `CONFIGURATION_MISMATCH` (f18harv arrays unprinted; CR-3608 scale model) | `NOT_PUBLICLY_LOCATED` | `NOT_PUBLICLY_LOCATED` (clamped at 45 deg) |
| Transonic / supersonic aero | `NOT_PUBLICLY_LOCATED` (Falcon 21 comparison curves only: `CONFIGURATION_MISMATCH`) | `NOT_PUBLICLY_LOCATED` | `CONFIGURATION_MISMATCH` (F-18B SRA flight PID, M 0.85-1.30) | `NOT_PUBLICLY_LOCATED` | `NOT_PUBLICLY_LOCATED` (no Mach term; envelope near Mach 0.5) |
| Aero data validity domain | `NOT_PAGE_VERIFIED` (alpha -10..45, beta +/-30, M < 0.6) | `NOT_PUBLICLY_LOCATED` | `PARTIAL_PUBLIC`: f18harv domain in abstract (alpha -10..90, beta +/-20) - but arrays unprinted | `NOT_PUBLICLY_LOCATED` | `STRONG_PUBLIC`: alpha -10..45, beta +/-30, elevator +/-24, clamped |
| FCS architecture | `NOT_PAGE_VERIFIED` (TP-1538 simulated CAS; TN D-8176 limiters) | `NOT_PAGE_VERIFIED`: exact 836 block diagrams (repository) | `PARTIAL_PUBLIC`: OFP 8.3.3 inner-loop CAS printed in f18bas (repository) | `PARTIAL_PUBLIC`: triplex FLCS, pitch TV, 1996 design philosophy | `STRONG_PUBLIC`: check-case LQR SAS/autopilot (not an F-16 FCS) |
| FCS numeric gains | `NOT_PUBLICLY_LOCATED` (TP-1538 gains unconfirmed) | `NOT_PUBLICLY_LOCATED` | `NOT_PAGE_VERIFIED`: f18bas prints gain/filter tables for its simplified CAS (repository); production gains `NOT_PUBLICLY_LOCATED` | `NOT_PUBLICLY_LOCATED` | `STRONG_PUBLIC`: check-case LQR gains and mixer |
| Surface position limits | `NOT_PAGE_VERIFIED` (Table I) | `NOT_PUBLICLY_LOCATED`; `CONFIGURATION_MISMATCH` (four other lineages) | `NOT_PAGE_VERIFIED`: three conflicting lineage tables (f18bas / f18harv / TP-97-206539), all repository | `NOT_PUBLICLY_LOCATED` | `PARTIAL_PUBLIC`: mixer spans +/-25 / +/-21.5 / +/-30; tables clamp elevator at +/-24; no explicit limiter |
| Actuator rates / dynamics | `NOT_PUBLICLY_LOCATED` (textbook values: `CONFIGURATION_MISMATCH`) | `NOT_PUBLICLY_LOCATED` | `NOT_PAGE_VERIFIED`: f18bas first-order and f18harv second-order models (repository) | `NOT_PUBLICLY_LOCATED` | `STRONG_PUBLIC`: explicitly none in the model |
| Engine identity | `PARTIAL_PUBLIC`: simulated engine, variant unstated | `PARTIAL_PUBLIC`: F100-PW-100 x2, build unknown | `STRONG_PUBLIC`: F404-GE-400 x2 | `STRONG_PUBLIC`: F119-PW-100 x2 | `PARTIAL_PUBLIC`: unnamed engine (Stevens & Lewis tables) |
| Dimensional thrust | `NOT_PAGE_VERIFIED`: TP-1538 Table VI (repository two-pass transcription) | `NOT_PUBLICLY_LOCATED` (normalised PW-100(3) curves only: `PARTIAL_PUBLIC` shape) | `PARTIAL_PUBLIC`: Max-AB gross-thrust figure (digitized, repository); table topology only | `NOT_PUBLICLY_LOCATED` | `STRONG_PUBLIC`: idle/MIL/MAX net thrust, Mach 0-1.0 x 0-50,000 ft |
| Engine dynamics | `NOT_PAGE_VERIFIED`: Garza & Morelli power lag | `PARTIAL_PUBLIC` (F100 hybrid-simulation structure; build mismatch) | `NOT_PAGE_VERIFIED`: TM-4240 vs TM-110216 (conflicting PLA rates) | `NOT_PUBLICLY_LOCATED` | `STRONG_PUBLIC`: explicitly none (steady-state) |
| Fuel flow | `NOT_PUBLICLY_LOCATED` | `NOT_PUBLICLY_LOCATED` | `NOT_PUBLICLY_LOCATED` (a table variable in f18harv; values unprinted) | `NOT_PUBLICLY_LOCATED` | `NOT_PUBLICLY_LOCATED` |
| Inlet effects | `NOT_PUBLICLY_LOCATED` | `CONFIGURATION_MISMATCH` (inlet studies, match pending) | `VALIDATION_ONLY`: HARV inlet distortion to alpha 60 | `NOT_PUBLICLY_LOCATED` | `NOT_PUBLICLY_LOCATED` |
| Thrust vectoring | `CONFIGURATION_MISMATCH`: only MATV has TV; the reference configuration has none | `CONFIGURATION_MISMATCH`: only NF-15B 837 has TV | `CONFIGURATION_MISMATCH`: TV data belong to Phase 2/3, not Phase 1 | `PARTIAL_PUBLIC`: +/-20 deg pitch, identity only | `CONFIGURATION_MISMATCH`: the model has none |
| Engine installation geometry | `NOT_PUBLICLY_LOCATED` | `NOT_PUBLICLY_LOCATED` | `NOT_PUBLICLY_LOCATED` | `NOT_PUBLICLY_LOCATED` | `PARTIAL_PUBLIC`: thrust along body +X at the MRC, zero moments |
| Flight-derived derivatives | `CONFIGURATION_MISMATCH` (AFTI, VISTA) | `VALIDATION_ONLY`: Cn_beta, Cm_alpha trends (repository digitization) | `VALIDATION_ONLY`: TM-4786 (basic configuration, 1987-88) | `NOT_PUBLICLY_LOCATED` | `CONFIGURATION_MISMATCH` (simulation model) |
| Trim reference data | `VALIDATION_ONLY`: NESC case 11 (same data lineage) | `NOT_PUBLICLY_LOCATED` | `NOT_PUBLICLY_LOCATED` | `NOT_PUBLICLY_LOCATED` | `STRONG_PUBLIC`: case 11 trim table (README Table 11) |
| Time histories / trajectories | `VALIDATION_ONLY`: NESC, simupy-flight | `VALIDATION_ONLY`: 60 digitized flight/simulation series (repository) | `VALIDATION_ONLY`: HARV flight data in NASA reports (content per report) | `NOT_PUBLICLY_LOCATED` | `STRONG_PUBLIC`: 8 cases x 3 tools (implementation verification only) |
| Flight envelope statements | `VALIDATION_ONLY` (TP-1538 departure behaviour) | `VALIDATION_ONLY` | `VALIDATION_ONLY` | `VALIDATION_ONLY`: EMD high-AOA exploration (EMD, not production) | `VALIDATION_ONLY`: stated envelope near 10,000 ft and 287.8 KEAS |

## What the matrix says, without a score

- **R2:** the NESC check-case F-16 column is the only one where the library itself has inspected the primary source. Its `STRONG_PUBLIC` cells are implementation-allowed for that configuration only. Every other column is unchanged from R1 because the PDF hosts were still blocked.

- **F-16:** the only aircraft with a sourced coefficient set, but only for a 1979 simulation configuration below Mach 0.6. Almost every cell is `NOT_PAGE_VERIFIED`: the repository has read the pages and the library has not.
- **F-15:** exact-airframe mass, FCS structure and validation data (repository readings), and **no** public aerodynamics, geometry reference or dimensional thrust.
- **F/A-18:** rich public research record, but the baseline aero arrays are not printed and most numeric content sits in configuration-specific research states.
- **F-22:** identity and development narrative only.
