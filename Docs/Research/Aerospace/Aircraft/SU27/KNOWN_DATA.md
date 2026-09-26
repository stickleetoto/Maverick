# Su-27 Known Data (SU27-R0 feasibility)

Canonical data: [`KNOWN_DATA.json`](KNOWN_DATA.json). Tables between GENERATED markers are rendered by `Tools/build_aerospace_source_views.py`.

> **Nothing on this page is a model input.** Only three values were located. The AL-31F thrust class is an official identity figure (`PROHIBITED`). The two tunnel-test ranges describe where unpublished data were generated (`NOT_A_MODEL_PARAMETER`). No reference geometry, mass property, coefficient, gain, limit or thrust table was located for any Su-27 configuration. Those gaps are in [`MISSING_DATA.md`](MISSING_DATA.md). Numbers seen only in secondary, game or enthusiast sources are listed there and deliberately not recorded here.

---

## 1. Configuration registry

Rule: data attach to `SU27-CFG-BASELINE-SU27S` only when the source names the production Su-27 or Su-27S. A source that says only "Su-27" goes to `SU27-CFG-FAMILY-UNSPECIFIED` until its content shows which aircraft. Derivatives, games and generic shapes each have their own configuration so that they cannot be merged by accident.

<!-- BEGIN GENERATED: configurations (Tools/build_aerospace_source_views.py; do not edit by hand) -->
| ID | Label | Airframe / serial | Engine | FCS | Research hardware | Dates | Scope | Lineage | Status |
|---|---|---|---|---|---|---|---|---|---|
| `SU27-CFG-BASELINE-SU27S` | Baseline production Su-27 / Su-27S (single-seat): the study target | Su-27 / Su-27S production | AL-31F x2 (engine type per UEC family statement) | Production fly-by-wire (designation 'SDU-10' in secondary sources); laws, gains and limiter settings not public | none | UNKNOWN (production dates not captured from a located primary source) | PROD_FAMILY | Production | BASELINE_TARGET_NO_MODEL_DATA |
| `SU27-CFG-T10-PROTOTYPE` | T-10 prototypes (original configuration, superseded) | T-10 (T-10-1 and following prototypes) | Not established from a located public source | Not public | none | UNKNOWN (not captured from a located source) | PREPROD | T-10 | PROTOTYPE_ONLY |
| `SU27-CFG-T10S-DEVELOPMENT` | T-10S redesigned development aircraft | T-10S | Not established from a located public source | Development fly-by-wire (not public) | none | First flight 1981 (Sukhoi history extract) | PREPROD | T-10S | DEVELOPMENT_REFERENCE |
| `SU27-CFG-FAMILY-UNSPECIFIED` | 'Su-27' named without variant, phase or model identity | Su-27 (unspecified) | Not stated | Not stated | none | UNKNOWN | — | Unassigned | CONFIGURATION_UNKNOWN |
| `SU27-CFG-WT-TSAGI-MODELS` | TsAGI wind-tunnel and free-flight scale models (incl. the 1987 T-105 tests) | Su-27 scale models (scale and geometry state unknown) | n/a | n/a | none | T-105 tests 1987 (Zhelnin extract); other dates UNKNOWN | TUNNEL | Tunnel | CONFIGURATION_UNKNOWN |
| `SU27-CFG-DERIV-SU30` | Su-30 family (two-seat multirole derivatives, incl. Su-30MK) | Su-30 family | AL-31F-family variants (not captured per aircraft) | SDU-10MK and later (derivative) | none | UNKNOWN | PROD_FAMILY | Su-30 | DERIVATIVE_ONLY |
| `SU27-CFG-DERIV-OTHER` | Other Su-27 derivatives: Su-27UB, export Su-27SK/SKM, Su-33, Su-27M/Su-35, Su-37, Su-27LL-PS, Su-34 | Various derivatives | Varies by derivative (not captured) | Varies; canard and thrust-vectoring derivatives have different control laws | none | UNKNOWN | — | Derivatives | DERIVATIVE_ONLY |
| `SU27-CFG-ENTHUSIAST-GAME` | Game and enthusiast Su-27 models (DCS, FlightGear/JSBSim and similar) | Simulations of Su-27 variants (as modelled) | As modelled | As modelled | none | UNKNOWN | SIM_ONLY | Enthusiast/game | PROHIBITED_AS_AUTHORITY |
| `SU27-CFG-NOT-SU27-GENERIC` | Configurations that are not the Su-27 (generic Cobra/high-alpha studies, TsAGI method papers) | NOT a Su-27 | n/a | n/a | none | UNKNOWN | SIM_ONLY | Generic | GENERIC_CONFIGURATION_ONLY |
| `ENG-AL-31F` | Lyulka / UEC-Saturn AL-31F | (engine) | AL-31F afterburning turbofan with variable nozzle; 12,500 kgf class (UEC statement) | n/a | none | UNKNOWN | PROD_FAMILY | AL-31F | ENGINE_IDENTITY |
<!-- END GENERATED: configurations -->

## 2. Candidate values

<!-- BEGIN GENERATED: values (Tools/build_aerospace_source_views.py; do not edit by hand) -->
### Aerodynamic-data domains and envelopes

| Value ID | Quantity | Value | Units | Precision | Config | Source | Location as recorded | Exactness | Level | Impl. use | Repo x-ref | Conflicts |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `SU27-TSAGI-T105-ALPHA-RANGE` | Angle-of-attack range of the 1987 TsAGI T-105 tests of Su-27 models | **0 to 180** | deg | as stated | WT-TSAGI-MODELS | SU27-NIZH-2008-ZHELNIN | article text (search extract; page not opened) | SEARCH_EXTRACT | EXTRACT | NOT_A_MODEL_PARAMETER | — | — |
| `SU27-TSAGI-T105-BETA-RANGE` | Sideslip range of the 1987 TsAGI T-105 tests of Su-27 models | **-90 to +90** | deg | as stated | WT-TSAGI-MODELS | SU27-NIZH-2008-ZHELNIN | article text (search extract; page not opened) | SEARCH_EXTRACT | EXTRACT | NOT_A_MODEL_PARAMETER | — | — |

### Propulsion

| Value ID | Quantity | Value | Units | Precision | Config | Source | Location as recorded | Exactness | Level | Impl. use | Repo x-ref | Conflicts |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `SU27-AL31F-THRUST-CLASS` | AL-31F thrust class (official statement; rating condition not stated) | **12,500** | kgf | stated as '12 500 кгс' (class figure) | ENG-AL-31F | SU27-UEC-2017-AL31F-NEWS | news text: 'АЛ-31Ф тягой 12 500 кгс устанавливается на самолеты семей… | SEARCH_EXTRACT | EXTRACT | PROHIBITED | — | — |
<!-- END GENERATED: values -->

## 3. Unit note

12,500 kgf = 12,500 x 9.80665 N = 122.6 kN, about 27,560 lbf. This is an arithmetic conversion only. The rating condition (afterburner, installed or uninstalled, altitude, Mach) is not stated in the source extract.
