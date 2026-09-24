# F-15 Public Data Matrix (R1 consolidation)

One row per data field, one column per configuration family. Each cell names the public source (library ID) and the repository document that read it. **No cell is page-verified by the library.** "Repo" means the value was read on a repository branch listed in [`SOURCE_GRAPH.md`](SOURCE_GRAPH.md).

Cell codes: **EXACT** = the source describes this configuration; **FAMILY** = production-family support only; **XVAL** = different configuration, cross-validation only; **NONE** = not located; **N/A** = not applicable.

---

| Field | NASA 836 pre-Quiet-Spike (target) | 836 other states | Production A-D family | Preproduction No. 8 | NF-15B 837 | AFIT / CR-186019 simulations | Subscale RPV / tunnel |
|---|---|---|---|---|---|---|---|
| Physical length / span / height | **EXACT**: 63.7 / 42.8 / 18.7 ft (`NASA-TM-4782` p.10 etc.; repo) | same airframe | — | — | 42.83 ft span (three-view) | — | scaled |
| Reference area S | **NONE** (no 836 report prints it; repo audit) | NONE | FAMILY via reproductions only (608 ft^2) | XVAL: 56.61 m^2 = 609.3 ft^2 (`NASA-TM-72861`) | XVAL: 608 ft^2 (`NASA-TM-2003-212027`) | XVAL: 608 / 608.0 ft^2 | scaled |
| Reference chord cbar | **NONE** | NONE | FAMILY: MAC 191.33 in = 15.94 ft (`AFIT-GA-ENY-91D-1`) | XVAL: 4.86 m | XVAL: 15.94 ft | XVAL: 15.94 / 15.95 ft | scaled |
| Reference span b | **NONE** (42.8 ft is physical) | NONE | — | XVAL: 13.05 m | XVAL: 42.7 ft | XVAL: 42.8 ft | scaled |
| Datum / %MAC relation | Partial: 28 % MAC = FS 561.7 (`NASA-TM-2001-210395` p.12; repo) | PFTF analysis CG | FAMILY: %MAC = (FS - 508.1)/191.33 x 100 | — | XVAL: FS 557.2 = 25.66 % MAC | — | — |
| Mass, CG, Ixx/Iyy/Izz/Ixz | **EXACT** (repo): Baseline column 37,426 lb, 26.34 % MAC, 30,345 / 198,687 / 223,214 / -5,070 slug-ft^2 at 8,000 lb fuel (`NASA-TM-2012-215978` Table 1) | Spike-extended column 37,152 lb / 26.05 % / 27,953 / 190,777 / 213,957 / -460 (conflict F15-X1) | NONE public (`MDC-A4172` not located) | — | — | XVAL: Davison 37,000 lb; 25,480 / 166,620 / 186,930 / -1,000 | scaled |
| Fuel-dependent mass schedule | NONE | NONE | NONE | NONE | NONE | NONE | — |
| Aerodynamic coefficient database | **NONE** (baseline model exists at NASA; not public) | NONE | NONE (`MDC-A4172` Part II; `ASD-F15-AEROBASE-ARO10` not located) | NONE | XVAL: flight-identified derivatives (`NASA-TM-2003-212027`) | XVAL: single-condition curve fits (AFIT); CR-186019 benchmark | XVAL: RPV derivatives (`NASA-TN-D-8136`), rotary/high-alpha tunnel data |
| Flight-identified derivatives (validation) | **EXACT** (repo, digitized): Cn_beta, Cm_alpha estimates and trends (`NASA-TM-2008-214634` figs. 12-13) | spike-on data | — | — | XVAL | — | XVAL |
| Flight vs simulation time histories | **EXACT** (repo, digitized): `NASA-TM-2008-214634` figs. 14-15; `NASA-TM-2012-215978` figs. 27-29 (60 series, 4,041 samples) | — | — | — | — | — | — |
| FCS architecture | **EXACT** (repo): simplified block diagrams, pitch/roll/yaw (`NASA-TM-2009-214651`, `NASA-TM-2012-215978` figs. 3-5) | unchanged for Quiet Spike (reports) | FAMILY: `MDC-DN-1180-01-238-458` (not located; one qualitative citation) | XVAL: control-system appendix (`NASA-TM-72861`) | XVAL: research FCS | XVAL | XVAL: remote augmentation |
| FCS gains / schedules | **NONE** | NONE | NONE | NONE (TM-72861 prints no CAS gain) | — | XVAL (thesis-specific) | — |
| FCS switch logic | **EXACT but conflicted** (F15-X2): ARI and crossfeed Mach switches | — | — | — | — | — | — |
| Surface travel limits | **NONE** | NONE | NONE | XVAL: stab 15 / -26 (repo) | XVAL | XVAL: several conflicting sets (F15-X6) | XVAL: RPV limits |
| Actuator rates / dynamics | **NONE** (diagrams print none) | NONE | NONE | NONE | XVAL | XVAL: CR-186019 24 deg/s, 20/(s+20) | XVAL |
| Engine identity | **EXACT**: F100-PW-100 x2; **build unknown** | PW-220E after 2014 | F100-PW-100 / -220 by era | F100 (build not recorded) | F100-PW-229 + vectoring | fixed thrust models | none (unpowered RPV) |
| Dimensional thrust deck | **NONE** | NONE | NONE (CP2903B classified) | NONE | NONE | XVAL: fixed 8,300 lbf total (repo research profile) | N/A |
| Thrust statement | ~23,500 lbf SLS full AB per engine (approximate; `NASA-TM-2005-213670`); conflicting 25,000 lbf sentence | 24,000 lb class (PW-220E) | — | — | — | — | — |
| Normalised thrust curves | FAMILY only: PW-100(3) simulation fig. 17 (`NASA-TP-1034`) | — | — | — | — | — | — |
| Engine transients | FAMILY structure only (`NASA-TM-X-3261`, `NASA-TP-1034`) | — | — | — | — | — | — |
| Inlet recovery / distortion | FAMILY lead (`NASA-CR-144866`, `NASA-TP-2411`) | PFTF inlets (not the engine inlet) | — | — | — | — | — |
| Engine installation coordinates | **NONE** | NONE | NONE | NONE | NONE | NONE | — |

## Reading the matrix

- The target has **exact mass properties, exact FCS structure and exact validation data**, all from repository readings. It has **no public reference geometry, no coefficient database, no gains, no surface or rate limits and no dimensional thrust.**
- Every F-15 numeric aerodynamic source that exists publicly belongs to a different configuration.
- The repeated family triple 608 ft^2 / 15.94 ft / 42.8 ft is traced to McDonnell data reproduced by AFIT theses, NF-15B 837 and CR-186019. It is not an 836 source (register entry R1-RN2 in the library [`SOURCE_GRAPH.md`](../../SOURCE_GRAPH.md#2-repeated-number-register)).
