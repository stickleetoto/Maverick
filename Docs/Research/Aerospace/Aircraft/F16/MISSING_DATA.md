# F-16 Missing Data (R1)

What the public record does not give (or the library has not yet confirmed), ranked by what blocks a sourced F-16 model. Each row names the configuration it applies to. "Not located" means searched and time-boxed, not proven absent.

---

## 1. Blockers for the TP-1538 / Morelli simulation configuration

These block the *library* from releasing values that the repository already uses. All of them are page-verification tasks, not research gaps.

| # | Missing | Configuration | Why it blocks | Next action |
|---|---|---|---|---|
| M1 | Library page verification of TP-1538 Table I (S, b, cbar, weight, inertias, surface limits) | `F16-CFG-NASA-REF-TP1538` | No value may be `ALLOWED` without it | Retrieve NTRS 19800005879, hash, confirm the sha256 matches the repository manifest (`aae0ece6…`), read Table I |
| M2 | Library page verification of Morelli 1998 Tables 1-3 and Eqs. 12-18 | `F16-CFG-SIM-MORELLI1998` | Same | NTRS 20040110310 |
| M3 | Library page verification of Garza & Morelli engine section | `F16-CFG-NASA-REF-TP1538` | Same | NTRS 20030013626 |
| M4 | Whether TP-1538 "span" is physical or reference | `F16-CFG-NASA-REF-TP1538` | Reference vs physical dimensions must be tracked separately | TP-1538 Table I wording |
| M5 | Installed/uninstalled and gross/net definition of Table VI; engine identity | `F16-CFG-NASA-REF-TP1538` | Thrust bookkeeping (see `Propulsion/README.md`) | TP-1538 App. B |
| M6 | Actuator rate limits and dynamics | `F16-CFG-NASA-REF-TP1538` | Rate limits drive PIO and transient response; the repository currently uses tuning values | TP-1538 fig. 63; Droste & Walker case study |
| M7 | Numeric gains of the TP-1538 simulated CAS | `F16-CFG-NASA-REF-TP1538` | Needed for a *source-faithful* reference FCS (still a research law, never "the F-16 FLCS") | TP-1538 control-system section |
| M8 | LEF schedule | `F16-CFG-NASA-REF-TP1538` | Only matters if the model leaves the Morelli LEF-fixed state | TP-1538 figure (repository: not transcribed) |

## 2. Gaps for any production F-16

| # | Missing | Configurations | Status | Notes |
|---|---|---|---|---|
| P1 | Manufacturer aerodynamic database (any block) | `PROD-AB`, `PROD-CD` | NOT PUBLICLY LOCATED | No public GD/Lockheed database found |
| P2 | Production FLCS / DFLCS gain schedules, limiter schedules | `PROD-AB`, `PROD-CD` | NOT PUBLICLY LOCATED | `F16-PRODUCTION-FLCS-OFP`. Do not reconstruct from performance claims |
| P3 | Production actuator performance | same | NOT PUBLICLY LOCATED | `DTIC-ADA213334` covers ISA hardware research; numeric content unknown |
| P4 | Block-specific mass/inertia with fuel schedule | same | NOT PUBLICLY LOCATED | Air-data/pacer reports do not print inertia (per abstracts) |
| P5 | Installed thrust deck for any named F100/F110 variant in the F-16 | same | NOT LOCATED (one lead) | `ICAS-2008-286` (F-16A/B, F100). `CHILDRE-MCCOY-JPP-1989` (PW-220) is paywalled |
| P6 | Transonic/supersonic aerodynamics of the production shape | same | NOT LOCATED | `NASA-TP-3355` is a derivative planform with F-16C comparison curves only |
| P7 | Public trim/trajectory data for a production F-16 | same | NOT LOCATED | NESC data are textbook-lineage |

## 3. Research-configuration gaps (only if a research profile is ever wanted)

| # | Missing | Configuration | Lead |
|---|---|---|---|
| R1 | Numeric content of the Bihrle VISTA model (alpha -80..+90) | `F16-CFG-VISTA-NF16D` | `DTIC-ADA327869` (Distribution A per catalogue) |
| R2 | Numeric identified parameters of the VISTA/X-62A model | `F16-CFG-VISTA-NF16D` | `AIAA-2018-0525` (author-hosted copy) |
| R3 | MATV vectoring effectiveness and nozzle geometry | `F16-CFG-VISTA-MATV` | `AIAA-94-3513`, `NTRS-19950007831` |
| R4 | AFTI derivative tables | `F16-CFG-AFTI` | `AIAA-84-2085` |

## 4. Not public (stop)

| Item | Status | Rule |
|---|---|---|
| `NASA-SW-LAR-17463-1` (Garza & Morelli software package) | U.S.-release-only | Do not seek. The public TM describes the models |
| `F16-PRODUCTION-FLCS-OFP` | Not publicly located | Do not reconstruct |
| `JSBSIM-F16-MODEL` | Public, but excluded by Maverick policy | Do not use |
