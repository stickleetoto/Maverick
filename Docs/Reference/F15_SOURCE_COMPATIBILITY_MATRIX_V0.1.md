# F-15 Source Compatibility Matrix V0.1

Status: **R0 freeze companion**

Chosen R0 configuration:

`NASA_F15_RPV_3_8_BASIC1_CG26_BLOCKED_INLETS`

This matrix prevents silent mixing of F-15 variants, scales, inlet states, stores, conformal-fuel-tank states, research modifications, and test methods.

Allowed usage categories:

- `DIRECT_REFERENCE`
- `CONFIG_MATCH_PENDING`
- `METHODOLOGY_ONLY`
- `CROSS_VALIDATION_ONLY`
- `INCOMPATIBLE_FOR_R0`

`DIRECT_REFERENCE` is field-scoped: a source may be direct for geometry or control identity without becoming blanket authority for every F-15 quantity.

## 1. Candidate-source compatibility matrix

| Source | Exact aircraft/config identity | Scale | Geometry/config | Inlet/configuration | Stores | CFT | Control surfaces | Test type | Mach range | Alpha range | Beta range | Data character | Mass/inertia availability | Propulsion configuration | Compatible with chosen R0? | Allowed usage |
|---|---|---:|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| **NASA TN D-8136** / NTRS **19760008088** | Unpowered remotely piloted F-15 RPV. Report explicitly separates **Basic** and later **Production** model configurations and multiple CG states. R0 selects **Basic 1 / 26% MAC** only. | 3/8 | Table 1 model geometry; Basic-to-Production differences explicitly identified | **Blocked inlets**; unpowered | No stores dataset accepted for R0 | No CFT configuration accepted or used | horizontal-tail/elevator symmetric pitch; differential horizontal-tail/elevator roll; wing ailerons; rudders | Flight test / parameter estimation | analyzed maneuvers **M < 0.60** | report-wide **-20 to +53 deg**; Basic-1 point subset not yet digitized | configuration-resolved bound **UNAVAILABLE** | flight-derived static/control/damping derivatives from small-perturbation maneuvers | **YES**. Table 2 gives Basic 1 CG, `Ix`, `Iy`, `Iz`, `Ixz`, weight | **Unpowered** | **YES, when row/point is Basic 1** | **DIRECT_REFERENCE** |
| **NASA TN D-7941** / NTRS **19750012221** | Unmanned 3/8-scale F-15 remotely piloted research vehicle used for remote digital augmentation; same NASA RPV program referenced by D-8136 | 3/8 | Three-view and RPV physical/control description; not used to override Basic/Production shape separation in D-8136 | RPV research configuration; engine thrust not part of authority | Not used | Not used | left/right stabilators, left/right ailerons, twin rudders; stabilator/aileron individual positions positive trailing-edge down | Flight control-system development and flight test | exact R0 bound **UNAVAILABLE** | high-angle-of-attack flight discussed; exact R0 bound **UNAVAILABLE** | **UNAVAILABLE** | flight/control-system source | **YES** at 26% and 30% MAC; 26% actual values cross-check D-8136 | No thrust authority | **YES for common RPV controls and 26% cross-check** | **DIRECT_REFERENCE** for those fields only |
| **NASA TN D-8052** / NTRS **19760010068** | Unpowered dynamically scaled large 3/8 fighter RPV from the same F-15 RPV research line; spin/departure program | 3/8 | same-family research vehicle, but exact Basic-1 state is not isolated for R0 | unpowered | Not used | Not used | augmented flight-control system / spin-recovery controls | Flight test + flight-support simulation | exact flight bound **UNAVAILABLE** | simulator database **±90 deg**; actual-flight configuration-resolved bound not frozen | simulator database **±40 deg** | spin/departure, dynamic/high-alpha | not used as R0 mass authority | unpowered | **Family-compatible, state match pending** | **CROSS_VALIDATION_ONLY** |
| **NASA TM-X-62360** / NTRS **19780002076** | 0.075-scale model representative of F-15; multiple nose, inlet-ramp, stabilator, and external-store configurations | 0.075 | wind-tunnel model; not proven geometrically identical to R0 Basic 1 | inlet ramp angles **0 to 11 deg** among configurations | **Several external-store configurations** included | not established as R0 match | left/right stabilator settings including symmetric and differential cases; other static-control configurations | Ames 12-ft pressure wind tunnel | **M = 0.16** | **0 to +90 deg** and **-40 to -80 deg** tested regions | **-20 to +30 deg** | static wind-tunnel | not an R0 mass/inertia authority | powered installation not represented as R0 engine deck | **NO direct match; pending configuration proof** | **CONFIG_MATCH_PENDING** |
| **NASA TM-72861** / NTRS **19790015808** | **Preproduction full-scale F-15** precision-controllability evaluation | full scale | preproduction airplane, not 3/8 Basic RPV | powered full-scale aircraft | test-aircraft state not R0 | not R0 | operational full-scale controls / augmentation | Flight test | **high subsonic to low transonic** | allowable flight-test alpha range; exact bound not frozen here | not frozen | flight-derived handling/controllability | not used for R0 | powered; exact engine variant/thrust not imported | **NO** | **CROSS_VALIDATION_ONLY** |
| **NASA CR-3478** / NTRS **19820016292** | 1/12-scale F-15 rotary-balance model; component buildup and basic airplane with control deflections | 1/12 | rotary-balance model; configuration identity not proven equal to R0 Basic 1 | wind-tunnel model | configuration-dependent; not R0 authority | clean/basic data family; exact R0 match not proven | various control deflections | Rotary-balance wind tunnel | **UNAVAILABLE** in R0 matrix | **8 to 90 deg** | not frozen | dynamic/rotary | not R0 mass/inertia authority | none for R0 | **NO direct match** | **METHODOLOGY_ONLY** |
| **NASA CR-3479** / NTRS **19820014339** | Analysis of F-15 rotary-balance data including component effects and **conformal fuel tanks** | rotary family associated with CR-3478/CR-3516 | mixes/compares basic component buildup and CFT-equipped effects | wind-tunnel rotary data | configuration-dependent | **YES in part of source; explicitly studied** | control deflections and component effects | Rotary-data analysis / spin prediction | **UNAVAILABLE** in R0 matrix | up to **90 deg** | not frozen | dynamic/rotary/spin analysis | not R0 mass/inertia authority | none for R0 | **NO** | **METHODOLOGY_ONLY**; CFT portions are **INCOMPATIBLE_FOR_R0** |
| **Docs/F15E_SPEC_RESEARCH_AND_GAME_TUNING.md** | Gameplay/historical F-15E material | full-scale/game abstraction | F-15E-oriented gameplay material | powered | gameplay-dependent | F-15E/CFT context possible | gameplay-oriented | secondary/gameplay | N/A | N/A | N/A | non-authoritative for reference physics | not accepted | not accepted | **NO** | **INCOMPATIBLE_FOR_R0** |

## 2. Source-specific configuration tags

These tags are intended to prevent broad labels such as `F15` from becoming accidental compatibility claims.

| Source/family | Required tag or tag family |
|---|---|
| TN D-8136 chosen state | `NASA_F15_RPV_3_8_BASIC1_CG26_BLOCKED_INLETS` |
| TN D-8136 other basic state | `NASA_F15_RPV_3_8_BASIC2_CG30_3_BLOCKED_INLETS` |
| TN D-8136 production states | `NASA_F15_RPV_3_8_PRODUCTION1_CG30_3_BLOCKED_INLETS`, `NASA_F15_RPV_3_8_PRODUCTION2_CG38_5_BLOCKED_INLETS` |
| TN D-7941 | `NASA_F15_RPV_3_8_EARLY_RPRV` plus explicit CG state |
| TN D-8052 | `NASA_F15_RPV_3_8_SPIN_PROGRAM` |
| TM-X-62360 | `NASA_F15_WT_0_075_<NOSE>_<INLET_RAMP>_<STAB>_<STORES>`; exact test state required before use |
| TM-72861 | `NASA_F15_FULLSCALE_PREPRODUCTION_PRECISION_CONTROL` |
| CR-3478 | `NASA_F15_ROTARY_1_12_<COMPONENT_CONTROL_STATE>` |
| CR-3479 | `NASA_F15_ROTARY_ANALYSIS_<CLEAN_OR_CFT>` |
| NASA modified F-15 research aircraft | use tail-number/program-specific tag; never generic `F15` |
| F-15E gameplay material | `GAMEPLAY_F15E_*`; never reference authority |

No duplicate canonical R0 tag is permitted.

## 3. Explicitly incompatible or non-direct families

The following must **not** be silently merged into R0:

| Family | R0 status | Reason |
|---|---|---|
| Production/full-scale F-15A/B/C/E data | `INCOMPATIBLE_FOR_R0` unless a future phase chooses that exact aircraft/configuration | R0 is a 3/8-scale unpowered Basic 1 RPV, not a production designation |
| TN D-8136 Production 1/2 | `INCOMPATIBLE_FOR_R0` for direct Basic-1 numeric import | wingtip/horizontal-tail geometry differs from Basic |
| TN D-8136 Basic 2 | `INCOMPATIBLE_FOR_R0` for mass/CG/inertia import | different CG/inertia state |
| 0.075-scale TM-X-62360 test states | `CONFIG_MATCH_PENDING` | different scale; inlet/nose/stabilator/stores variations |
| 1/12-scale rotary-balance family | `METHODOLOGY_ONLY` | different scale and dynamic/rotary regime |
| CFT-equipped configurations | `INCOMPATIBLE_FOR_R0` | R0 does not include CFT |
| stores-equipped configurations | `INCOMPATIBLE_FOR_R0` | R0 accepts no stores aerodynamic data |
| highly modified NASA F-15A/B research aircraft | `INCOMPATIBLE_FOR_R0` | modification/program state differs |
| HIDEC / DEEC / PSC research states | `INCOMPATIBLE_FOR_R0` for R0 direct data | powered full-scale research configuration |
| three-surface/canard F-15 | `INCOMPATIBLE_FOR_R0` | different lifting/control geometry |
| ACTIVE | `INCOMPATIBLE_FOR_R0` | highly modified research aircraft/control/propulsion state |
| PCA | `INCOMPATIBLE_FOR_R0` | propulsion-control research state, not unpowered RPV |
| F-15E gameplay/historical document | `INCOMPATIBLE_FOR_R0` | not clean reference authority |

## 4. Field-level authority rules

### Identity / geometry
Use TN D-8136 Basic 1 only.

### Mass / CG / inertia
Use TN D-8136 Table 2 Basic 1 only for frozen values. TN D-7941 26%-MAC actual values are a cross-check, not a blending source.

### Control identities/signs
Use the common RPV chain TN D-8136 + TN D-7941. If a composite differential-control equation or hard stop cannot be transcribed unambiguously, record `UNAVAILABLE`; do not reconstruct it from memory.

### Static/subsonic aerodynamics
TN D-8136 is the direct source family, but future numeric extraction must preserve each plotted point's configuration symbol. A curve containing Basic, Basic 2, and Production points is not automatically a Basic-1 table.

### Dynamic / rotary / spin
TN D-8052, CR-3478, and CR-3479 are deferred. They cannot overwrite R0 static/subsonic authority.

### Transonic
TM-72861 is cross-validation only. A future full-scale baseline must be selected before its values become direct.

### Propulsion
No candidate in this R0 matrix supplies an exact engine/configuration thrust deck compatible with the unpowered RPV. `F100` alone is insufficient identity.

## 5. Provenance gates for future promotion to DIRECT_REFERENCE

A source/configuration state may be promoted only if all applicable items are explicit:

1. exact aircraft/research identity;
2. scale;
3. Basic/Production or other geometry state;
4. inlet state;
5. stores state;
6. CFT state;
7. control-surface state;
8. CG state;
9. test type/regime;
10. Mach/alpha/beta domain for the data being imported;
11. mass/inertia provenance when dynamics are involved;
12. engine variant and installation state when propulsion is involved.

If any required identity field would need guessing, leave the source `CONFIG_MATCH_PENDING`, `CROSS_VALIDATION_ONLY`, `METHODOLOGY_ONLY`, or `INCOMPATIBLE_FOR_R0`.

## 6. R0 compatibility verdict

Direct R0 source chain:

```text
TN D-8136 Basic 1 / CG 26 / blocked inlets
  + TN D-7941 same-RPV control and 26%-CG cross-check
  + NASA RP-1168 methodology-only sign/axis convention
```

Everything else remains explicitly separated.

**Matrix verdict: PASS — no incompatible source is promoted to R0 direct numeric authority.**
