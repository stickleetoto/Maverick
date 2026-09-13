# F-22 Configuration Compatibility V0.1

Status: **R0 configuration-separation matrix**

Preferred eventual target when exact public authority exists:

`F22A_PRODUCTION_PUBLIC_BASELINE`

This file exists primarily to stop YF-22, EMD, early-model and generic ATF results from becoming anonymous “F-22” numbers.

## 1. Configuration matrix

| Tag | Program state | Geometry status | Propulsion | Control system | Public-use rule |
|---|---|---|---|---|---|
| `YF22_DEMVAL_PROTOTYPE` | ATF demonstration/validation prototype, 1990–92 | prototype geometry | YF119/YF120 program-dependent | prototype VMS/control-law iterations; TVC could be switched for tests | YF-22 data direct only to YF-22 |
| `F22_EMD_CONTROL_LAW_DEVELOPMENT_1996` | pre-first-flight F-22 EMD design/control-law development | post-YF design geometry concept | F119 integration planned | F-22 design architecture/targets under development | architecture/design-goal support, not final production gains |
| `F22_EMD_HIGH_AOA_1999_2000` | F-22 EMD high-AOA flight-test state | EMD aircraft | F119; pitch-axis TVC | triplex electronic FLCS; test OFP evolves during envelope expansion | direct only to EMD test evidence |
| `F22A_PRODUCTION_PUBLIC_BASELINE` | operational production F-22A | production | 2 x F119-PW-100 | operational integrated FLCS | target for exact production facts only |
| `F119_PW_100_PRODUCTION_PUBLIC` | production engine/nozzle family | N/A | F119-PW-100 | FADEC / 2D pitch vector nozzle | direct only to public engine architecture/nozzle facts |
| `F22_EARLY_WIND_TUNNEL_MODEL` | pre/early F-22 scale-model research | early model, not proven production | none/test-specific | wind-tunnel control settings | family/cross-validation unless exact geometry proves otherwise |
| `ATF_GENERIC_STUDY` | generic technology/concept studies | not an exact F-22 | study-specific | study-specific | never direct to F-22A |

## 2. Hard YF-22 -> F-22 boundary

NASA/SP-2000-4519 explicitly states that the external geometry of the F-22 changed significantly from the YF-22 prototype. Changes included:

- increased wingspan;
- decreased wing leading-edge sweep;
- reduced vertical-tail area and aft relocation;
- reconfigured horizontal tails.

NASA considered these differences large enough to repeat F-22 tests in Langley facilities after the YF-22 program.

Therefore:

- YF-22 static/dynamic aerodynamic numbers are `INCOMPATIBLE` as direct production F-22A authority;
- YF-22 flight-control gains, CAP/damping values, mode transitions and TVC test procedures are not F-22A gains;
- YF-22 performance demonstrations may be `CROSS_VALIDATION_ONLY` or historical context.

## 3. Source-to-configuration matrix

| Source | YF-22 | F-22 EMD | Production F-22A | F119 production | Allowed use |
|---|---|---|---|---|---|
| USAF F-22 Raptor fact sheet | `INCOMPATIBLE` for prototype numbers | `PRIMARY_FAMILY_ONLY` historical | `DIRECT` high-level identity/geometry/mass/fuel/engine | `COMPATIBLE_SUPPORT` identity | production summary only |
| Pratt & Whitney F119 product page | `INCOMPATIBLE` to YF119 unless separately proven | `COMPATIBLE_SUPPORT` | `DIRECT` engine/nozzle architecture | `DIRECT` | no numeric thrust deck |
| Harris & Black AIAA 96-3379 | `DIRECT` within explicit YF subsection | `DIRECT/COMPATIBLE_SUPPORT` for 1996 F-22 design state | `COMPATIBLE_SUPPORT` qualitative/design-target only | `CROSS_VALIDATION_ONLY` | do not treat development goals as final OFP gains |
| Peron AFFTC high-AOA results | `INCOMPATIBLE` | `DIRECT` | `COMPATIBLE_SUPPORT` for aircraft-family behavior only | `COMPATIBLE_SUPPORT` engine identity | EMD envelope/test evidence, not production limits |
| NASA/SP-2000-4519 | `DIRECT` program history | `COMPATIBLE_SUPPORT` wind-tunnel lineage | `PRIMARY_FAMILY_ONLY` | N/A | decisive configuration-separation authority |
| Gillard AIAA 99-4015 | `UNKNOWN` until paper acquired | likely `DIRECT/COMPATIBLE_SUPPORT` | `UNKNOWN` | N/A | cannot promote numeric derivatives until model geometry/date are known |
| Fin Buffeting early F-22 model | `INCOMPATIBLE/UNKNOWN` | `PRIMARY_FAMILY_ONLY` | `CROSS_VALIDATION_ONLY` | N/A | early scale-model aeroelastic data only |
| AEDC F-22A/F119 facility histories | N/A | `PRIMARY_FAMILY_ONLY` | `DIRECT` for test-program existence | `DIRECT` for test-program existence | numeric test results not released in those histories |

## 4. EMD -> production rule

EMD evidence is not automatically production authority.

A datum may move from EMD to `PRIMARY_COMPATIBLE` for production only when the relevant items are demonstrated unchanged or equivalent:

1. external aerodynamic geometry;
2. control-surface geometry and limits;
3. mass/CG state relevance;
4. engine/nozzle variant and control state;
5. flight-control software/control-allocation version;
6. open/closed weapon-bay configuration;
7. stores/asymmetry/test instrumentation configuration;
8. Mach/alpha/beta/rate domain.

The 1999–2000 high-AOA source explicitly describes EMD testing as work in progress and notes that unexpected aero/system characteristics were still being found and control laws changed. That fact itself prevents treating its entire envelope as a final production law/model.

## 5. F119 configuration rule

Do not merge:

- YF119 prototype engine data;
- F119 development/test engines;
- production F119-PW-100;
- generic “F119” performance claims.

The Pratt & Whitney production page and USAF fact sheet establish the production engine identity and pitch-vectoring nozzle semantics. They do **not** establish a thrust deck, transient model or installation loss model.

## 6. Wind-tunnel source rule

For any F-22 wind-tunnel number, record at minimum:

- model scale;
- date;
- geometry revision;
- static/forced-oscillation/rotary-balance method;
- Mach/Reynolds;
- alpha/beta/rate grid;
- control/nozzle state;
- whether the report calls the model YF-22, F-22, early F-22 or EMD configuration.

No `UNKNOWN` model may be promoted to production `DIRECT` merely because the title says “F-22.”

## 7. Public evidence ceiling

### A. PUBLICLY REPRODUCIBLE

With current sources, the following can be represented directly at high confidence:

- production F-22A basic identity;
- production external length/span/height from USAF public source;
- published high-level weight/fuel/MTOW anchors with their limited definitions;
- 2 x F119-PW-100 identity;
- 2-D pitch-vectoring nozzle architecture;
- nozzle pitch-vector range up/down as much as 20 deg from Pratt & Whitney;
- EMD high-AOA test facts only when tagged as EMD;
- 1996 F-22 control-law *design objectives/architecture statements* only when tagged as development-state design evidence.

### B. PUBLICLY CONSTRAINABLE

Public evidence constrains but does not numerically reproduce:

- low-order/classical longitudinal response goals;
- handling-quality target philosophy, CAP/damping use and PIO mitigation;
- control-law use of thrust vectoring as integrated pitch control;
- departure/spin resistance intent;
- high-alpha flight behavior at EMD level;
- existence of static/forced-oscillation/rotary-balance F-22 wind-tunnel databases;
- F119 altitude/operability/durability test history;
- Mach-2-class/supercruise performance class.

### C. REQUIRES APPROXIMATION

A simulator could approximate these, but current R0 evidence does not justify calling the results exact F-22A physics:

- `S`, `cbar` and aerodynamic reference point if not recovered from primary engineering data;
- full static coefficient maps;
- dynamic/control derivative maps;
- final control-allocation/gain schedules;
- actuator dynamics and rate limits;
- CG/inertia tensor;
- Mach/altitude engine performance, transients and fuel flow;
- installed propulsion-airframe interaction.

### D. NOT PUBLICLY AVAILABLE / NOT LOCATED

No defensible public primary authority has been located in this R0 pass for:

- production F-22A complete aerodynamic coefficient database;
- production full-envelope dynamic/control derivative deck;
- final production control-law source/gain tables;
- production CG/inertia dataset;
- production F119-PW-100 installed thrust deck.

## 8. Compatibility conclusion

The current source set supports a **configuration-faithful public reference profile**, but not a source-exact production F-22A flight model.

YF-22 provides historical/methodology evidence only; EMD supplies the strongest public flight-test/control-development evidence; production sources close only high-level airframe and propulsion identity. Those layers must remain visibly separate in all future Maverick work.
