# F-15 Full-Scale Source Compatibility V0.2

Status: **FIELD-SCOPED COMPATIBILITY MATRIX**

Frozen target:

`NASA_F15B_836_SN74_0141_PRE_QUIET_SPIKE_BASELINE_F100_PW_100`

No source is compatible merely because its title says "F-15" or "F100".

## 1. Compatibility fields

Every source is judged on:

- aircraft identity / tail or serial when known;
- single-seat / two-seat;
- engine model/build;
- flight-control/CAS state;
- canards;
- thrust vectoring;
- Quiet Spike / external research article;
- inlet modifications;
- stores/CFT/centerline fixture;
- mass/CG state;
- date/program phase;
- full-scale/subscale;
- flight/wind-tunnel/simulation character.

Classification:

`DIRECT`, `COMPATIBLE_SUPPORT`, `CROSS_VALIDATION_ONLY`, `INCOMPATIBLE`, `UNKNOWN`.

## 2. Matrix

| Source | Aircraft/config | Engine | Controls/modifications | Test/data type | Target compatibility | Allowed use |
|---|---|---|---|---|---|---|
| NASA/TM-2012-215978 | NASA 836 / S/N 74-0141 program; includes pre-spike baseline flights and spike state | F100-PW-100 target state | production mechanical + analog single-string CAS; spike is separable | full-scale flight + simulation/parameter estimation | `DIRECT` field-scoped | baseline mass/inertia; FCS architecture; baseline-model provenance; ARI/crossfeed schedule boundaries |
| NASA/TM-2009-214651 | same NASA 836 program | target engine context | baseline sim + additive spike models; production FCS unchanged | simulation/preflight analysis with flight-updated baseline | `DIRECT` provenance | aero-model structure/derivative-family provenance; exact FCS description; not missing numeric table values |
| NASA/TM-2008-214634 / AIAA-2007-6638 | NASA 836 baseline + spike program | target context | baseline flights then spike installation | flight parameter estimation / validation | `DIRECT` for baseline validation; spike effects config-specific | validation evidence and known transonic limitation |
| NTRS 20070032807 | NASA 836 / S/N 74-0141 | **production F100-PW-100 x2** | Quiet Spike program; baseline flights documented | full-scale flight test | `DIRECT` identity/engine | exact target identity, baseline research campaign |
| NASA/TM-2006-213674 | NASA F-15B research test bed | F100-PW-100 x2 | centerline flight-test fixture for experiment | full-scale research flight | `DIRECT` descriptive airframe fields; fixture aero not baseline | length/span/height, engine identity, broad control architecture |
| NASA/TM-2005-213670 | NASA 836 research test bed with PFTF | F100-PW-100 x2 | production-type side inlets; PFTF below fuselage | full-scale local-flow flight test | `DIRECT` for inlet architecture/engine identity; PFTF flow not baseline aero | inlet descriptive architecture only |
| NASA TM-2001-210395 | same F-15B/PFTF lineage; PFTF installed | F100-PW-100 lineage | centerline PFTF; analysis state CG 28% MAC | CFD/flight-facility design | `COMPATIBLE_SUPPORT` | FS/MAC coordinate clue only |
| NASA TM-4782 | NASA F-15B with FTF-II | F100-PW-100 | centerline FTF-II | full-scale flight | `COMPATIBLE_SUPPORT` | airframe/test-bed lineage; not baseline aero coefficients |
| NASA TP-1373 | no NASA 836 aircraft; two prototype F100-PW-100 engines | prototype F100-PW-100 | altitude facility, uninstalled | engine facility test + model | `COMPATIBLE_SUPPORT` | engine-family gross-thrust/airflow methodology and potential future deck after build mapping |
| NASA TM-X-3261 | no target airframe | F100-PW-100 simulation/actual engine comparison | baseline engine control state of 1975 program | hybrid/digital engine simulation | `COMPATIBLE_SUPPORT` | transient/performance model family; not target deck |
| NASA TP-1034 | no target airframe | F100-PW-100(3) | subvariant-specific control/simulation | engine simulation | `COMPATIBLE_SUPPORT` | method/equations; subvariant mismatch gate |
| NASA TP-1482 | prototype F100 family | prototype F100-PW-100 | altitude facility | thrust-method validation | `COMPATIBLE_SUPPORT` | gross-thrust methodology |
| NASA TP-1782 | other F-15 test aircraft | prototype F100-PW-100 | different aircraft/engine installation | full-scale flight | `CROSS_VALIDATION_ONLY` | installed-thrust method/uncertainty, not target values |
| NASA CR-144866 | full-scale F-15 inlet/engine test program; other vehicle/engine serials | F100-PW-100 family | production-type inlet research | wind-tunnel/full-scale inlet test methodology | `CROSS_VALIDATION_ONLY` | recovery/distortion trends/methodology |
| NASA CP-10143 Vol.3 F-15 material | F-15 A-D family overview | mixed family context | production-family analog control/high-alpha discussion | conference technical overview | `COMPATIBLE_SUPPORT` | candidate family surface limits/CAS authority only; not target hard stops |
| AFIT ADA319164 | F-15B S/N 76-0130 | production F-15 family, not target | USAF high-AOA study | flight/simulation thesis | `CROSS_VALIDATION_ONLY` | bibliography to original McDonnell data; trend checks |
| AFIT AD-A230462 | analytical F-15B model | not target-identified | simulation/high-alpha | bifurcation analysis | `CROSS_VALIDATION_ONLY` | bibliography to `MDC A4172 Part II`; not target numbers |
| AFIT AD-A244044 | other F-15 configuration | not target | weight/ballast study | USAF thesis | `CROSS_VALIDATION_ONLY` | bibliography to `MDC A4172 Part I Supplement 1` |
| MDC A4172 (not acquired) | unknown exact applicability until source inspected | unknown by edition/state | original engineering model lineage | original contractor/USAF manual | `UNKNOWN` | no numeric promotion until public primary copy and target equivalence review |
| DN-1180.01-238-458 Rev.D (not acquired) | production F-15 control-system lineage, exact block applicability unverified | N/A | original McDonnell FCS design note | engineering design note | `UNKNOWN` | no hard limits/rates/gains until source acquired |
| AFFTC-TR-76-48 | F/TF-15A development-test / air-superiority family | period-specific F100 | development test configuration | flight test | `CROSS_VALIDATION_ONLY` | baseline-family handling/aero trend; not NASA 836 direct |
| NASA 837 / TM-2003-212027 | preproduction F-15B highly modified | F100-PW-229 | canards, research nozzles/ACTIVE lineage | flight system ID | `INCOMPATIBLE` direct | methodology only |
| NASA 835 HIDEC/PW1128 | preproduction single-seat research F-15 | PW1128/F100 EMD lineage | digital FCS/DEEC/HIDEC | propulsion/control research | `INCOMPATIBLE` direct | methodology only |
| NASA 836 post-2014 | same airframe, later date | F100-PW-220E | digital engine state differs | later research test bed | `INCOMPATIBLE` for frozen propulsion state | future alternate target only |
| RPV TN D-8136 family | 3/8-scale unpowered blocked-inlet RPV | none | remote augmentation | subscale flight | `CROSS_VALIDATION_ONLY` | exact frozen transfer policy only |

## 3. Field-level promotion decisions

### 3.1 Geometry

A family geometry value may move from `SUPPORTED_ONLY` to `FROZEN_DIRECT` only if an
original source proves the reference definition is the same for the production-representative
F-15B configuration represented by NASA 836.

Current decisions:

- `S = 608 ft^2`: **not promoted**.
- `cbar = 191.3 in` family value: **not promoted**.
- `45 deg` leading-edge sweep family value: **not promoted** as a new exact-target frozen number.
- FS 561.7 at 28% MAC from PFTF analysis: **not promoted** to frozen target datum.

### 3.2 Controls

Exact NASA 836 reports directly support the architecture and two schedule boundaries but do not
publish the complete hard-stop/actuator-rate table.

Family candidate travel values in NASA CP-10143 Vol. 3 stay `COMPATIBLE_SUPPORT`.

ACTIVE/NASA 837 actuator rates are `INCOMPATIBLE` for target implementation.

### 3.3 Aerodynamics

Only the exact NASA 836 baseline-model **existence, update process, derivative families, and
validation limitations** are direct.

No coefficient number from:

- RPV,
- preproduction F-15 #8,
- NASA 837,
- three-surface research,
- FTF/PFTF added-airframe increments,
- Quiet-Spike delta models

may become a target baseline coefficient without a new compatibility proof.

### 3.4 Propulsion

The target engine identity is exact, but engine-family compatibility is not numeric-deck compatibility.

NASA TP-1373 / TM-X-3261 / TP-1034 are not direct target thrust/transient maps until:

1. target NASA 836 engine serial/build/control configuration is identified;
2. the engine-source build is matched or a documented conversion exists;
3. inlet recovery and installation losses are handled separately;
4. installed vs uninstalled thrust semantics are explicit.

### 3.5 Inlet

NASA CR-144866 is valuable full-scale F-15/F100 inlet research, but the test vehicle/engine
identity differs. It remains `CROSS_VALIDATION_ONLY` for the NASA 836 side-inlet recovery map.

Experimental centerline PFTF/CCIE inlet recovery is **not** the aircraft side-inlet recovery.

## 4. Conflict register

### C-01 — approximate static afterburning thrust

Exact-aircraft NASA documents quote approximate uninstalled sea-level-static full-afterburner
values in the roughly 23.5–25 klbf class. These are rounded/descriptive values, not one precision
performance deck.

Decision: do not reconcile or average. Precise static thrust remains non-authoritative for runtime.

### C-02 — family geometry versus exact-target authority

Multiple government sources repeat 608 ft^2, ~191.3 in MAC, and 45-deg sweep for F-15
family/reference models.

Decision: consistent family support is not proof that NASA 836 used the identical coefficient
reference definition. Keep `SUPPORTED_ONLY`.

### C-03 — baseline transonic lateral-directional model

NASA/TM-2008-214634 records inconsistent transonic lateral-directional response matching,
especially rudder sweeps.

Decision: even if numeric tables are recovered, the transonic region requires an explicit
validation/uncertainty boundary rather than blanket authority.

## 5. Compatibility verdict

No incompatible source was promoted to target numeric authority.

The exact-target source chain is stronger for **identity, mass/inertia, FCS architecture and model
provenance** than for **geometry reference definitions, coefficient numbers, actuator limits, or
propulsion performance**.
