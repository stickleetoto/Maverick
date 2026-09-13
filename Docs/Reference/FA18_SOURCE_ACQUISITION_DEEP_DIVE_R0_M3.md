# F/A-18 Public Source Acquisition — Deep Dive R0 M3

Status: **RESEARCH MILESTONE — F404 propulsion provenance strengthened; exact Phase-I thrust deck still open**

Target physical configuration remains:

`NASA_F18_HARV_160780_PHASE1_BASIC`

This milestone maps the strongest public F404-GE-400 propulsion evidence found after the Phase-I geometry/mass/control closure. It does not import X-29 installation data, later HARV TVC nozzle effects, or generic F404 ratings into the Phase-I thrust model.

## 1. Major result

The public-source problem for F/A-18 propulsion is now much more precise.

Public NASA sources establish a coherent F404-GE-400 model/validation chain:

1. General Electric engine specification and in-flight-thrust programs existed for the F404-GE-400;
2. NASA calibrated an individual flight F404-GE-400 in the Lewis Propulsion Systems Laboratory and used the calibration to correct the GE in-flight-thrust model;
3. NASA quantified measurement sensitivity/uncertainty and dynamic response of F404 in-flight-thrust calculation techniques;
4. NASA built the HARV real-time engine model from manufacturer nonlinear-model tables and explicit F-18 installation-effect models;
5. the two actual F404-GE-400 engines installed in the F-18 HARV were tied to a thrust stand for same-airframe bleed-extraction testing;
6. later NASA F/A-18 flight research used the GE 83112 IFT model with explicit gross-thrust, ram-drag, engine-dependent drag, bleed and horsepower-extraction accounting.

This is strong provenance for **how** a defensible F404/F/A-18 propulsion model should be decomposed and validated.

It is not the missing numeric Phase-I Mach/altitude/PLA thrust deck.

## 2. NASA TP-3001 — calibrated F404-GE-400 thrust methods

### FA18-M3-SRC-001

- **Title:** *Evaluation of Various Thrust Calculation Techniques on an F404 Engine*
- **Report:** NASA-TP-3001 / H-1505 / NTRS 19900015818
- **Author:** Ronald J. Ray
- **Organization:** NASA Ames Research Center, Dryden Flight Research Facility
- **Publication date:** April 1990; NASA PDF includes an erratum correcting the title wording
- **Repository:** NASA NTRS
- **Stable URL:** https://ntrs.nasa.gov/citations/19900015818
- **PDF:** `19900015818.pdf`, 31 PDF pages in the current NTRS copy including errata/front matter
- **Distribution:** Public / U.S. Government work
- **Configuration:** F404-GE-400 flight engine in the X-29A research airplane
- **Classification:** `PRIMARY_EXACT` for the tested X-29/F404 engine/calculation methods; `PRIMARY_FAMILY_ONLY` for HARV propulsion performance

### Publicly supported findings

NASA calibrated the X-29 flight F404-GE-400 at the Lewis Propulsion Systems Laboratory. The resulting test data were used to:

- correct the manufacturer's in-flight-thrust program for the specific engine;
- develop an independent simplified real-time gross-thrust method;
- compare against the F404 engine specification model;
- quantify uncertainties and actual flight-test accuracy.

The report states uninstalled gross-thrust accuracy on the order of **1–4 percent** for the evaluated in-flight-thrust methods.

The engine is explicitly the F404-GE-400, 16,000-lb-thrust-class, low-bypass twin-spool augmented turbofan.

### Upstream General Electric references exposed by NASA

NASA TP-3001 identifies:

- **GE program 80031A(U)** — *F404-GE-400 Engine Specification Model*, August 1981;
- **GE program 83112** — *F404-GE-400 Engine In-flight Thrust Calculation Program*, August 1983.

No lawful public copy of either GE program was acquired in this milestone. The `(U)` notation in a reference does not by itself prove unrestricted public release.

### Visual-inspection caveat

The NTRS PDF opened as a 31-page PDF and its report structure/text were directly available. Screenshot rendering was attempted for relevant pages, but the web cache returned a cache-miss error. Therefore no figure/table cell was visually transcribed for a numeric thrust deck.

## 3. NASA TM-4140 — F404 thrust-calculation measurement uncertainty

### FA18-M3-SRC-002

- **Title:** *Measurement Effects on the Calculation of In-Flight Thrust for an F404 Turbofan Engine*
- **Report:** NASA-TM-4140 / H-1556 / AIAA 89-2364 / NTRS 19900002425
- **Author:** Timothy R. Conners
- **Organization:** NASA Ames/Dryden
- **Publication date:** September 1989
- **Distribution:** Unclassified — Unlimited
- **Stable URL:** https://ntrs.nasa.gov/citations/19900002425
- **Configuration:** X-29A F404-GE-400 installation
- **Classification:** `PRIMARY_EXACT` for measurement/thrust-calculation uncertainty methodology; `PRIMARY_FAMILY_ONLY` for HARV engine performance

The public source analyzes:

- six flight conditions;
- five engine power settings per condition;
- area-pressure and mass-flow/temperature gas-generator methods;
- net-thrust uncertainty and influence coefficients;
- nonlinearity effects in uncertainty propagation.

This is valuable as a future validator for any F404 deck, but does not establish HARV installed thrust values.

## 4. NASA TM-4591 — dynamic validation of thrust calculations

### FA18-M3-SRC-003

- **Title:** *Evaluating the Dynamic Response of In-Flight Thrust Calculation Techniques During Throttle Transients*
- **Report:** NASA-TM-4591 / H-1990 / AIAA 94-2115 / NTRS 19940030735
- **Author:** Ronald J. Ray
- **Organization:** NASA Dryden Flight Research Center
- **Publication date:** June 1994
- **Distribution:** Unclassified — Unlimited
- **Stable URL:** https://ntrs.nasa.gov/citations/19940030735
- **Classification:** `PRIMARY_FAMILY_ONLY` for the F404/F/A-18 propulsion-validation methodology unless a specific analyzed case is configuration-matched

The report defines throttle-step and throttle-frequency-sweep maneuvers and frequency-domain analysis methods for identifying combined thrust-model and instrumentation dynamics.

Use in Maverick: validation methodology for spool/transient surrogate fitting after a source-compatible numeric engine model is acquired.

## 5. NASA TM-4240 — HARV-specific simple dynamic engine model

### FA18-M3-SRC-004

- **Title:** *A Simple Dynamic Engine Model for Use in a Real-Time Aircraft Simulation With Thrust Vectoring*
- **Report:** NASA-TM-4240 / H-1643 / AIAA 90-2166 / NTRS 19910009766
- **Author:** Steven A. Johnson
- **Organization:** NASA Ames/Dryden
- **Publication date:** October 1990
- **Configuration:** F-18 HARV F404-GE-400 simulation lineage, including research nozzle/vectoring installation effects
- **Classification:** `PRIMARY_EXACT` for the documented HARV simulation model; `PRIMARY_COMPATIBLE` for unchanged F404 core-engine architecture; not a direct Phase-I installed deck

Public model content includes:

- gross thrust;
- ram drag;
- net propulsive force construction;
- nozzle pressure ratio;
- nozzle throat area;
- Mach / altitude / power-lever-angle dependence;
- inlet-spillage drag;
- nozzle/aft-end drag increment;
- throttle rate limiting and low-pass-filter dynamics;
- vectoring axial-thrust-loss treatment.

Power-lever-angle anchors in the public report:

- flight idle: **31 deg**;
- intermediate / military power: **87 deg**;
- full afterburner: **130 deg**.

The simple model is derived from tables generated by the manufacturer nonlinear dynamic model, **GE R88AEB427 — Software User's Manual for the HARV F404-GE-400 Dynamic Real Time Model**.

For two evaluated conditions at 35,000 ft, Mach 0.2 and 0.7, the simple model is reported within approximately **3 percent steady-state** and **25 percent transient response** of the full nonlinear model.

### Installation warning

The report explicitly includes HARV nozzle modifications and aft-end increments. Inlet-drag data were based on force/moment testing of a 6-percent-scale production F-18 model, while the nozzle aft-end increment is unique to the modified HARV.

Therefore the complete TM-4240 net-thrust result must not be transplanted into the clean Phase-I target.

## 6. NASA TM-104247 — same-airframe HARV engine thrust-stand validation

### FA18-M3-SRC-005

- **Title:** *Effects of Bleed Air Extraction on Thrust Levels on the F404-GE-400 Turbofan Engine*
- **Report:** NASA-TM-104247 / H-1806 / AIAA 92-3092 / NTRS 19920020182
- **Authors:** Andrew J. Yuhas; Ronald J. Ray
- **Organization:** NASA Dryden / PRC Kentron
- **Publication date:** July 1992
- **Repository:** NASA NTRS
- **Stable URL:** https://ntrs.nasa.gov/citations/19920020182
- **Configuration:** the two F404-GE-400 engines installed in the NASA F/A-18 HARV, aircraft tied down to a thrust-measuring stand
- **Classification:** `PRIMARY_EXACT` for the same-airframe engine/bleed test state; `PRIMARY_COMPATIBLE` for core F404 bleed-vs-thrust behavior, but not a complete Phase-I flight deck

The public report establishes:

- both HARV-installed engines were directly tested;
- multiple power settings were used;
- compressor bleed extraction was deliberately varied, including beyond manufacturer maximum-specification levels;
- measured thrust loss was essentially linear with bleed extraction at all tested power settings;
- the F404-GE-400 steady-state simulation predicted large bleed-induced thrust losses within approximately **±1 percent** of measured values.

This is a powerful same-airframe validation anchor for future engine-accessory/bleed modeling.

It does not provide the full flight Mach/altitude/PLA thrust surface and occurred in the HARV research-program timeline rather than proving the exact Phase-I flight-11–38 installed state.

## 7. Later NASA F/A-18 use of GE 83112 IFT

### FA18-M3-SRC-006

NASA/TP-2002-210730 and related Dryden F/A-18 formation-flight work document later F/A-18 research aircraft powered by F404-GE-400 engines and using the engine manufacturer's **GE 83112 In-Flight Thrust Calculation Program**.

The public description separates:

- gross thrust `FG`;
- ram drag;
- engine throttle-dependent external drag;
- inlet spillage/nozzle effects;
- bleed-air extraction;
- horsepower extraction.

Classification:

- `PRIMARY_EXACT` for those later NASA F/A-18 research installations;
- `PRIMARY_FAMILY_ONLY` for Phase-I HARV numeric values;
- strong `COMPATIBLE_SUPPORT` for the required force-accounting architecture.

This reinforces an important design rule: an F404 gross-thrust table alone is not an installed F/A-18 propulsion model.

## 8. Propulsion source graph

```text
GE 80031A F404-GE-400 Engine Specification Model
        |                     [NOT_ACQUIRED]
        +--> GE 83112 In-Flight Thrust Calculation Program
        |                     [NOT_ACQUIRED]
        |        +--> NASA X-29 calibration / TP-3001
        |        +--> NASA TM-4140 uncertainty study
        |        +--> later NASA F/A-18 IFT use
        |
        +--> GE R88AEB427 HARV Dynamic Real Time Model
                              [NOT_ACQUIRED]
                 |
                 +--> NASA TM-4240 reduced HARV real-time model
                 |
                 +--> HARV same-airframe test/validation chain
                         +--> NASA TM-104247 bleed/thrust stand
```

This graph is coherent at the **engine-family/model-method** level. It is not yet coherent enough at the **exact Phase-I installed numeric-deck** level.

## 9. Phase-I propulsion availability matrix

| Field | M3 status | Evidence |
|---|---|---|
| Engine identity | `CLOSED_EXACT` | HARV program: 2 x F404-GE-400 |
| Public static thrust class | `CLOSED_EXACT` as descriptive rating only | NASA/official HARV sources: 16,000-lb class per engine |
| PLA semantics | `CLOSED_COMPATIBLE` | F404/HARV public model: idle 31°, Mil 87°, full AB 130° |
| Gross-thrust calculation methodology | `CLOSED_COMPATIBLE` | GE IFT lineage + NASA calibration/validation |
| Ram-drag accounting | `CLOSED_COMPATIBLE` architecture | NASA F404/F/A-18 flight research |
| Inlet/nozzle external drag accounting | `CLOSED_COMPATIBLE` architecture | NASA F/A-18/HARV models |
| Bleed/thrust behavior | `CLOSED_COMPATIBLE`; same-airframe measured | NASA TM-104247 |
| Engine transient validation method | `CLOSED_COMPATIBLE` | NASA TM-4591 / TM-4240 |
| Installed Phase-I thrust vs Mach/altitude/PLA | **`OPEN`** | no exact public numeric deck held |
| Phase-I inlet-recovery surface | **`OPEN`** | production-model/HARV research data do not establish exact Phase-I map |
| Fuel-flow deck | **`OPEN`** | calculation programs exist; no exact public Phase-I arrays held |
| Exact spool/state dynamics | `PARTIAL` | public reduced dynamics exist; manufacturer full model unavailable |
| Left/right engine installation coordinates / thrust lines | `OPEN` | not closed in the acquired Phase-I source chain |

## 10. Rejected shortcuts

The following remain prohibited:

- using X-29 calibrated thrust values as HARV thrust because the engine designation matches;
- using the HARV TVC/nozzle aft-end increment in Phase I;
- converting a 16,000-lbf class rating into a full thrust deck;
- assuming GE 83112 outputs are public simply because NASA publicly used the program;
- treating the same-airframe bleed thrust-stand experiment as a complete flight installation deck;
- substituting later NASA F/A-18 research-aircraft engine calibration for BuNo 160780 without serial/configuration evidence.

## 11. Readiness implication

This milestone does **not** change the overall F/A-18 verdict from:

**`PARTIAL — MORE RESEARCH`**

It does strengthen the narrower engineering conclusion:

- Phase-I geometry/mass/CG/inertia: closed;
- conventional controls: sufficiently bounded for an unpowered profile;
- bounded aero validation: available;
- engine architecture and validation methodology: now strongly bounded;
- exact powered performance: still not source-complete.

The two highest-value remaining numeric acquisitions are now unmistakable:

1. a Phase-I-compatible nonlinear aerodynamic coefficient model/deck;
2. a Phase-I-compatible F404-GE-400 installed thrust/fuel/transient deck.

Until those are acquired, a physical unpowered/bounded Phase-I implementation can be defensible, but a claimed exact powered full-envelope model cannot.
