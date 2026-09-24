# F-22 Known Data (R1 feasibility)

Canonical data: [`KNOWN_DATA.json`](KNOWN_DATA.json). Tables between GENERATED markers are rendered by `Tools/build_aerospace_source_views.py`.

> **Nothing on this page is a model input.** Fact-sheet values are `PROHIBITED` (identity only). Design targets and envelope statements are `NOT_A_MODEL_PARAMETER`. The gap record `F22-REF-GEOM` states that no reference geometry, mass properties, coefficients or gains were located.

---

## 1. Configuration registry

<!-- BEGIN GENERATED: configurations (Tools/build_aerospace_source_views.py; do not edit by hand) -->
| ID | Label | Airframe / serial | Engine | FCS | Research hardware | Dates | Scope | Lineage | Status |
|---|---|---|---|---|---|---|---|---|---|
| `F22-CFG-PROD-F22A` | Production F-22A (public baseline) | F-22A | F119-PW-100 x2 with 2-D pitch-vectoring nozzles | Production triplex digital FLCS (NOT public) | none | IOC 2005; production 1997-2011 | PROD_FAMILY | Production | REFERENCE_ONLY |
| `F22-CFG-EMD-1996-CLAW` | F-22 EMD control-law design state described in 1996 | F-22 EMD (design) | F119 | 1996 development control laws (design targets public; gains not) | none | 1996 | PREPROD | EMD | DEVELOPMENT_REFERENCE |
| `F22-CFG-EMD-HIGH-AOA` | F-22 EMD high-AOA test aircraft, 1999-2000 | F-22 EMD | F119 x2 | EMD triplex FLCS with pitch TV | none | 1999-2000 | PREPROD | EMD | DEVELOPMENT_REFERENCE |
| `F22-CFG-YF22-DEMVAL` | YF-22 prototype (Dem/Val) | YF-22 | YF119 or YF120 (by prototype) | Prototype FLCS (point-design demonstrations) | none | 1990-1991 | PREPROD | YF-22 | YF22_ONLY |
| `F22-CFG-WT-EARLY-F22` | Early / development F-22 wind-tunnel models (13.3% buffet model, dynamic models, V-9 model) | F-22 tunnel models | n/a | n/a | none | 1990s | TUNNEL | Tunnel | CONFIGURATION_UNKNOWN |
| `F22-CFG-GENERIC-UNRELATED` | Generic or unrelated F-22-like configurations (e.g. ICE 101, educational 'F-22-like' models) | NOT an F-22 | n/a | n/a | none | UNKNOWN | SIM_ONLY | Generic | GENERIC_CONFIGURATION_ONLY |
| `ENG-F119-PW-100` | Pratt & Whitney F119-PW-100 | (engine) | F119-PW-100 with 2-D C-D pitch-vectoring nozzle | n/a | none | UNKNOWN | PROD_FAMILY | F119 | ENGINE_IDENTITY |
<!-- END GENERATED: configurations -->

## 2. Candidate values

<!-- BEGIN GENERATED: values (Tools/build_aerospace_source_views.py; do not edit by hand) -->
### Geometry and reference quantities

| Value ID | Quantity | Value | Units | Precision | Config | Source | Location as recorded | Exactness | Level | Impl. use | Repo x-ref | Conflicts |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `F22-FS-SPAN` | Wingspan (fact sheet) | **44 ft 6 in** | ft-in | 1 in | PROD-F22A | USAF-F22-FACT-SHEET | 'General characteristics' | PUBLIC_FACT_SHEET | CAT | PROHIBITED | yes | — |
| `F22-FS-LEN` | Length (fact sheet) | **62 ft 1 in** | ft-in | 1 in | PROD-F22A | USAF-F22-FACT-SHEET | 'General characteristics' | PUBLIC_FACT_SHEET | CAT | PROHIBITED | yes | — |
| `F22-FS-HT` | Height (fact sheet) | **16 ft 8 in** | ft-in | 1 in | PROD-F22A | USAF-F22-FACT-SHEET | 'General characteristics' | PUBLIC_FACT_SHEET | CAT | PROHIBITED | yes | — |
| `F22-WT-SCALE` | Early F-22 fin-buffet model scale | **0.133** | - | 0.001 | WT-EARLY-F22 | NTRS-20000052124 | NOT_CAPTURED | ABSTRACT_STATEMENT | ABS | NOT_A_MODEL_PARAMETER | — | — |
| `F22-REF-GEOM` | F-22 reference area / MAC / reference span, CG, inertia, coefficients… | **NOT PUBLICLY LOCATED** | - | UNKNOWN | PROD-F22A, EMD-HIGH-AOA | F22-PRODUCTION-FCS-AERO-DATA | n/a | REPOSITORY_READING | LEAD | NOT_A_MODEL_PARAMETER | yes | — |

### Mass properties

| Value ID | Quantity | Value | Units | Precision | Config | Source | Location as recorded | Exactness | Level | Impl. use | Repo x-ref | Conflicts |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `F22-FS-WEIGHT` | Weight (fact sheet; loading undefined) | **43,340** | lb | 10 lb | PROD-F22A | USAF-F22-FACT-SHEET | 'General characteristics' | PUBLIC_FACT_SHEET | CAT | PROHIBITED | yes | — |
| `F22-FS-MTOW` | Maximum takeoff weight (fact sheet) | **83,500** | lb | 100 lb | PROD-F22A | USAF-F22-FACT-SHEET | 'General characteristics' | PUBLIC_FACT_SHEET | CAT | PROHIBITED | yes | — |
| `F22-FS-FUEL` | Internal fuel (fact sheet) | **18,000** | lb | 1,000 lb | PROD-F22A | USAF-F22-FACT-SHEET | 'General characteristics' | PUBLIC_FACT_SHEET | CAT | PROHIBITED | yes | — |

### Controls, actuators and FCS

| Value ID | Quantity | Value | Units | Precision | Config | Source | Location as recorded | Exactness | Level | Impl. use | Repo x-ref | Conflicts |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `F22-CLAW-CAP` | 1996 design CAP target band | **0.35 to 1.0** | g^-1 s^-2 (CAP) | as stated | EMD-1996-CLAW | AIAA-96-3379 | NOT_CAPTURED | SEARCH_EXTRACT | EXTRACT | NOT_A_MODEL_PARAMETER | yes | — |
| `F22-CLAW-DAMP` | 1996 design short-period damping target | **1.1 to 1.2** | - | 0.1 | EMD-1996-CLAW | AIAA-96-3379 | NOT_CAPTURED | SEARCH_EXTRACT | EXTRACT | NOT_A_MODEL_PARAMETER | — | — |

### Aerodynamic-data domains and envelopes

| Value ID | Quantity | Value | Units | Precision | Config | Source | Location as recorded | Exactness | Level | Impl. use | Repo x-ref | Conflicts |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `F22-EMD-AOA-RANGE` | EMD high-AOA characteristics explored | **below -40 to above +60** | deg AOA | 10 deg | EMD-HIGH-AOA | SFTE-2000-PERON-F22-HIGH-AOA | p.1 | ABSTRACT_STATEMENT | ABS | NOT_A_MODEL_PARAMETER | yes | — |

### Propulsion

| Value ID | Quantity | Value | Units | Precision | Config | Source | Location as recorded | Exactness | Level | Impl. use | Repo x-ref | Conflicts |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `F22-FS-THRUST` | Thrust class per engine (fact sheet) | **35,000 lb class** | lbf | class | PROD-F22A, ENG-F119-PW-100 | USAF-F22-FACT-SHEET | 'General characteristics' | PUBLIC_FACT_SHEET | CAT | PROHIBITED | yes | — |
| `F22-F119-VECTOR` | Pitch thrust-vector range | **up/down as much as 20** | deg | 'as much as' | ENG-F119-PW-100 | PW-F119-PRODUCT-PAGE | NOT_CAPTURED | PUBLIC_FACT_SHEET | CAT | PROHIBITED | yes | — |

### Accuracy statements

| Value ID | Quantity | Value | Units | Precision | Config | Source | Location as recorded | Exactness | Level | Impl. use | Repo x-ref | Conflicts |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `F22-F119-STORM-ACC` | F119 installed-thrust model accuracy statements | **< 2 % / ~2.5 % RMS** | % | as stated | ENG-F119-PW-100 | RTO-MP-SCI-162-P23 | sections 2.3 (per repository) | REPOSITORY_READING | LEAD | NOT_A_MODEL_PARAMETER | yes | — |
<!-- END GENERATED: values -->

## 3. Reading the values

- **Weight 43,340 lb** has no defined loading (empty, basic, operating?). It cannot close mass properties.
- **35,000-lb class thrust** is a class figure. It must not be used to scale any thrust curve.
- **CAP 0.35-1.0 and damping 1.1-1.2** are 1996 *design targets* from a development paper, seen in a search extract. They are goals for a tuned research controller, not measurements of the aircraft.
- **-40 to +60 deg AOA** describes what the EMD high-AOA test explored. It is not an aerodynamic-data domain.
