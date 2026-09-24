# F-15 Missing Data (R1 consolidation)

This file centralises the repository's gap analysis (primarily `sol/f15-source-gap-audit` @1a3cd4b, `Docs/Reference/F15_MISSING_SOURCE_PRIORITY_V0.1.md`, and `claude/f15-full-implementation` @89140b9, `F15_REMAINING_GAPS_V1.0.md`) under the library's configuration IDs. Priorities follow the gap audit, which ranked them by implementation dependency. The library added no new F-15 primary source in R1.

Target: `F15-CFG-NASA836-PRE-QS`.

---

## 1. Ranked gaps for the target

| Rank | Missing | Status (repository) | Best public lead (library ID) | Unsafe substitutes (never use) |
|---:|---|---|---|---|
| 1 | Reference area S and mean aerodynamic chord cbar | CONFIG_MATCH_PENDING | trace references in `NASA-TM-2009-214651`, `NASA-TM-2012-215978`; `NASA-TM-2003-212027` shows the reference-dimension pattern (837) | RPV scaling; 837; preproduction No. 8; web F-15C/E specifications |
| 2 | Numeric baseline aerodynamic database | CONFIG_MATCH_PENDING | same two 836 reports; underlying Boeing/McDonnell simulation (Boeing supplied an F-15C simulation for piloted evaluation, per `NTRS-20070032807` reading) | RPV coefficients; `NASA-TM-X-62360`; 837 derivatives; three-surface data; spike increments |
| 3 | Aircraft datum and moment reference | UNAVAILABLE | `AFIT-GA-ENY-91D-1` family datum; `NASA-TM-2003-212027` (837) | assuming 837/835 datum |
| 4 | Surface travel limits and signs | UNAVAILABLE | `NASA-TM-72861` (No. 8) as a consistency check only | RPV limits; 837; F-16 values; games |
| 5 | Actuator rates and dynamics | UNAVAILABLE | references behind TM-72861's appendix (`AFFTC-TR-74-8-76-48`, not located) | F-16 rates; ACTIVE/IFCS servos; RPV servos |
| 6 | Mach/alpha/beta validity envelope of the eventual aero data | UNAVAILABLE | must come with rank 2 | Quiet Spike envelope; RPV M < 0.6 |
| 7 | F100-PW-100 dimensional thrust deck | UNAVAILABLE | `NASA-TP-1373`, `NASA-TP-1482`, `NASA-TP-1782` (prototype 2-7/8; config match pending) | PW-220E; PW1128; PW-229; generic F100; F-15E tables |
| 8 | Transient / spool model for the target build | UNAVAILABLE | `NASA-TM-X-3261`, `NASA-TP-1034`, `NASA-CP-2298` | Garza & Morelli F-16 lag; PW-229; PW-220E |
| 9 | Fuel flow | UNAVAILABLE | same F100 family | EMD, PW1128, PW-220E/-229 maps |
| 10 | Inlet recovery / distortion | UNAVAILABLE | `NASA-CR-144866`, `NASA-TP-2411` | PFTF centrebody inlet; PW1128/HIDEC schedules |
| 11 | Engine installation coordinates, thrust lines | UNAVAILABLE | `NASA-TP-3627` (835/PCA; XVAL only) | synthetic symmetric coordinates; 837 vectoring coordinates |
| 12 | Fuel-dependent mass/CG/inertia schedule | UNAVAILABLE | none | PCA/835 inertias; RPV scaling |
| 13 | Baseline CAS / ARI / gearing gains | CONFIG_MATCH_PENDING | `MDC-DN-1180-01-238-458` (not located) | 837 IFCS; 835 HIDEC; RPV augmentation; gameplay gains |
| 14 | Speed brake, flap, other device schedules | UNAVAILABLE | none | F-15E/ACTIVE schedules |

## 2. Not public (stop)

| Item | Status |
|---|---|
| `MDC-A4172` (Parts I, I Supp. 1, II) | Not publicly located; only AFIT reproductions |
| `MDC-DN-1180-01-238-458` Rev. D | Not publicly located; one qualitative citation |
| `AFFTC-TR-75-32` (AD-B045115) | Limited distribution. Not sought |
| `AFFTC-TR-74-8-76-48` | Not located |
| `ASD-F15-AEROBASE-ARO10` | Not located |
| `F100-SPEC-CP2903B` | Classified. **Never reconstruct, estimate or infer from** |
| `AFIT-BARTH-BECK-UPSTREAM` | Not located |

## 3. Library-level gaps (added by R1)

| Gap | Why |
|---|---|
| No F-15 source is above CATALOGUE/ABSTRACT level in the library | Egress blocked; all page readings are the repository's |
| F15-X2 (ARI/crossfeed switches) cannot be resolved without a page read | Main and implementation branches read text vs figure |
| Several repository-known sources lack a captured title or authors | Recorded as UNCONFIRMED rather than filled from memory |
