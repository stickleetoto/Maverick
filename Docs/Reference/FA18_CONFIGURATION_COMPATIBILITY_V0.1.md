# F/A-18 Configuration Compatibility V0.1

Status: **R0 configuration-separation matrix**

This document prevents public F/A-18 sources from collapsing into one anonymous `F18` dataset. Every direct use must identify the research/aircraft state that generated the datum.

## 1. Compatibility classes

- `DIRECT` — exact source configuration.
- `COMPATIBLE_SUPPORT` — same relevant physical configuration is demonstrated for the field, but the source is not the selected target itself.
- `CROSS_VALIDATION_ONLY` — useful trend/methodology/validation evidence only.
- `INCOMPATIBLE` — configuration difference directly affects the quantity.
- `UNKNOWN` — insufficient configuration evidence; never promote to direct authority.

Source-catalog provenance class remains separate: `PRIMARY_EXACT`, `PRIMARY_COMPATIBLE`, `PRIMARY_FAMILY_ONLY`, `SECONDARY_LEAD`, `UNVERIFIED_LEAD`, `NOT_ACQUIRED`.

## 2. Candidate configurations

| Configuration tag | Identity / state | TVC | Forebody strakes | Special structural/FCS modification | Engines | Direct-use boundary |
|---|---|---|---|---|---|---|
| `NASA_F18_HARV_160780_PHASE1_BASIC` | BuNo 160780, sixth full-scale developmental F-18; Phase-I basic hardware/software | No | No | baseline HARV instrumentation/CAS state only | 2 x F404-GE-400 | **Recommended R0 physical target candidate** |
| `NASA_F18BAS_BASIC_F18_OFP_8_3_3_TVC_OFF_CAPABLE` | NASA Langley simulation of basic F/A-18; OFP 8.3.3 simplified inner loops | Hypothetical two-paddle model present, can be disabled | No | simulation-specific; exact airframe serial absent | research simulation engine model | **Strong simulation corpus, not exact fleet aircraft** |
| `NASA_F18_HARV_160780_PHASE2_TVC_NASA0` | HARV 1992–94 TVC flight state, NASA-0 RFCS versions | Yes, three post-exit vanes/nozzle | No | RFCS, spin chute/research hardware, LEX fence | F404-GE-400 | direct only to Phase-II TVC |
| `NASA_F18_HARV_160780_PHASE2_TVC_NASA1A` | late Phase-II HARV with NASA-1A research law | Yes | No | NASA-1A RFCS | F404-GE-400 | direct only where flight/config version is NASA-1A |
| `NASA_F18_HARV_160780_PHASE3_TVC_ANSER` | HARV with TVC plus actuated forebody strakes | Yes | Yes | ANSER + research FCS | F404-GE-400 | direct only to Phase III |
| `NASA_F18_AAW_MODIFIED_F18A` | F/A-18A with reduced wing torsional stiffness | No HARV TVC | No HARV ANSER | custom AAW research FCS; structural modification | F404-family aircraft, exact state source-specific | direct only to AAW |
| `NASA_F18B_SRA_AAW_BASELINE_SUPPORT` | NASA F-18B Systems Research Aircraft used for AAW baseline derivatives | No HARV TVC | No | SRA instrumentation/test setup | source-specific | family/support until exact match proven |
| `FA18CD_OPERATIONAL_FAMILY` | operational F/A-18C/D families/blocks | No HARV TVC | No | production FCS/OFP varies by block | F404 variants vary | **not selected; block identity required** |
| `FA18EF_SUPER_HORNET_FAMILY` | F/A-18E/F | No HARV TVC | No | different airframe, controls, inlets, engines | F414 family | separate aircraft; never legacy-Hornet direct authority |

## 3. Source-to-configuration matrix

| Source | Phase-I basic HARV | `f18bas` TVC-off | Phase-II TVC | Phase-III ANSER | AAW | F/A-18C/D | F/A-18E/F |
|---|---|---|---|---|---|---|---|
| NASA-TM-107601 `f18bas` | `COMPATIBLE_SUPPORT` for generic/basic aero/FCS lineage; exact airframe match not proven | `DIRECT` | `CROSS_VALIDATION_ONLY` | `CROSS_VALIDATION_ONLY` | `CROSS_VALIDATION_ONLY` | `UNKNOWN` | `INCOMPATIBLE` |
| NASA-TM-110216 `f18harv` | `CROSS_VALIDATION_ONLY` for unchanged baseline submodels | `CROSS_VALIDATION_ONLY` | `DIRECT` for TVC portions when matching RFCS | `DIRECT` for ANSER-enabled simulation state | `INCOMPATIBLE` | `INCOMPATIBLE` for research modifiers | `INCOMPATIBLE` |
| NASA-TM-4772 HARV overview | `DIRECT` identity/phase boundary | `CROSS_VALIDATION_ONLY` | `DIRECT` identity/phase boundary | `DIRECT` identity/phase boundary | `INCOMPATIBLE` | `UNKNOWN` | `INCOMPATIBLE` |
| NASA-TM-4786 basic F-18 derivatives | `DIRECT` | `COMPATIBLE_SUPPORT` trend/derivative validation only | `CROSS_VALIDATION_ONLY` | `CROSS_VALIDATION_ONLY` | `INCOMPATIBLE` | `UNKNOWN` | `INCOMPATIBLE` |
| NASA-TM-102692 early HARV flight ID | `COMPATIBLE_SUPPORT` pending point-level audit | `CROSS_VALIDATION_ONLY` | `UNKNOWN` if later points present | `INCOMPATIBLE` | `INCOMPATIBLE` | `UNKNOWN` | `INCOMPATIBLE` |
| NASA/TP-97-206539 longitudinal TVC | `CROSS_VALIDATION_ONLY` | `CROSS_VALIDATION_ONLY` | `DIRECT` for analyzed Phase-II flights | `INCOMPATIBLE` if ANSER authority assumed | `INCOMPATIBLE` | `INCOMPATIBLE` | `INCOMPATIBLE` |
| NASA/TP-1999-206573 lateral TVC | `CROSS_VALIDATION_ONLY` | `CROSS_VALIDATION_ONLY` | `DIRECT` | `INCOMPATIBLE` for ANSER | `INCOMPATIBLE` | `INCOMPATIBLE` | `INCOMPATIBLE` |
| NASA-TM-112360 HATP baseline overview | `COMPATIBLE_SUPPORT` after individual test-state mapping | `COMPATIBLE_SUPPORT` | `CROSS_VALIDATION_ONLY` | `CROSS_VALIDATION_ONLY` | `INCOMPATIBLE` | `PRIMARY_FAMILY_ONLY` only | `INCOMPATIBLE` |
| Morelli/Ward AIAA 2007-6714 | `UNKNOWN` until exact flight cases mapped | `PRIMARY_FAMILY_ONLY` as simplified DB lineage | `UNKNOWN` | `UNKNOWN` | `INCOMPATIBLE` | `UNKNOWN` | `INCOMPATIBLE` |
| NASA/TM-2005-213688 AAW | `INCOMPATIBLE` for direct derivatives | `INCOMPATIBLE` | `INCOMPATIBLE` | `INCOMPATIBLE` | `DIRECT` | `INCOMPATIBLE` unless structural equivalence explicitly removed | `INCOMPATIBLE` |
| F-18B SRA parameter-estimation tests | `CROSS_VALIDATION_ONLY` / `UNKNOWN` | `CROSS_VALIDATION_ONLY` | `INCOMPATIBLE` | `INCOMPATIBLE` | `COMPATIBLE_SUPPORT` | `PRIMARY_FAMILY_ONLY` | `INCOMPATIBLE` |
| AIAA 2000-3913 F/A-18E/F drop model | `INCOMPATIBLE` | `INCOMPATIBLE` | `INCOMPATIBLE` | `INCOMPATIBLE` | `INCOMPATIBLE` | `INCOMPATIBLE` | `DIRECT` to test article / `COMPATIBLE_SUPPORT` to full-scale E/F trends |

## 4. HARV phase rules

### Phase I — recommended R0 candidate

Required tag: `NASA_F18_HARV_160780_PHASE1_BASIC`.

Direct Phase-I imports require explicit evidence that the maneuver/data predates the thrust-vectoring installation and ANSER. NASA-TM-4786 supplies the strongest current boundary: basic hardware/software, Phase-I flights 11–38, June 1987 through March 1988.

The LEX-fence modification documented in later HARV material was introduced after these Phase-I flights. Therefore later `basic F-18` comparison curves must not silently overwrite the exact flight-11–38 configuration without a date/configuration check.

### Phase II — thrust vectoring

Required tags must include RFCS version family when known. The physical TV system uses **three post-exit vanes per engine nozzle**, not the preliminary two-paddle `f18bas` concept. NASA-0 and NASA-1A research control laws are not interchangeable.

### Phase III — ANSER

ANSER sources require both `TVC=YES` and `ACTUATED_FOREBODY_STRAKES=YES` unless a report explicitly disables one effector. ANSER aerodynamic increments, actuator logic and control-allocation results are not baseline-Hornet data.

## 5. `f18bas` rules

`f18bas` is a source-defined research simulation, not a synonym for F/A-18A, F/A-18C, or HARV Phase I.

Allowed direct statement:

`NASA_F18BAS_BASIC_F18_OFP_8_3_3_TVC_OFF_CAPABLE`

The report says the hypothetical TVC can be disabled with `LTHVEC=false`; that creates a documented non-vectoring simulation state. It still does not establish a production block, airframe serial, exact mass state, or fleet OFP equivalence.

Do not copy a `f18bas` coefficient to Phase-I HARV merely because both are called “basic F-18.” Instead require an explicit validation/correlation link or classify the value `COMPATIBLE_SUPPORT`/`CROSS_VALIDATION_ONLY`.

## 6. AAW rules

AAW deliberately changes wing torsional stiffness and uses a custom research control system. Its flight-identified derivatives are excellent scientific data but are **INCOMPATIBLE** as direct basic-Hornet coefficients. The F-18B SRA baseline tests that supported AAW can be retained as a family cross-check only until exact geometry/FCS/mass equivalence is proven.

## 7. Super Hornet rules

F/A-18E/F is a different airframe/propulsion/control family from legacy F/A-18A/B/C/D and HARV. NASA drop-model and full-scale correlation work is valuable for future E/F development, but no E/F datum may fill a legacy-Hornet gap.

## 8. Current target recommendation

For a future Maverick physical reference freeze, the strongest exact public configuration currently identified is:

`NASA_F18_HARV_160780_PHASE1_BASIC`

Reason:

- exact research airframe identity, BuNo 160780;
- clean pre-TVC / pre-ANSER state;
- flight-derived stability/control derivatives from a clearly bounded Phase-I campaign;
- HATP baseline wind-tunnel/full-scale/flight comparison chain;
- direct link to NASA simulation lineages without pretending the simulation and flight vehicle are identical.

This is a **candidate recommendation**, not an aircraft implementation or numeric freeze.
