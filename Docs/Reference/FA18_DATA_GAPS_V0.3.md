# F/A-18 Data Gaps V0.3

Status: **POST-CONSOLIDATION — HIGH-ALPHA / PID / INLET COVERAGE STRENGTHENED**

This document supersedes V0.2 for gap-status interpretation. It preserves the same configuration-separation rules.

Target:

`NASA_F18_HARV_160780_PHASE1_BASIC`

## Availability matrix

| Field | Phase-I target status | Evidence added in this consolidation | Notes |
|---|---|---|---|
| Reference geometry | **CLOSED_EXACT** | TM-4772 cross-check | Existing exact Phase-I source chain remains authoritative |
| 60%-fuel mass/CG/inertia | **CLOSED_EXACT** | TM-4772 independent program-level cross-check | Full fuel-dependent schedule still open |
| Static nonlinear aero coefficients | **OPEN** | HATP overview improves source graph only | Full A7247/A8575-style numeric arrays still missing |
| Subsonic/high-AOA dynamic derivatives | **PARTIAL_STRONG** | TM-4786 direct Phase-I/basic-flight PID + CR-3608 rotary data | Not a full nonlinear dynamic deck |
| Subsonic/high-AOA control derivatives | **PARTIAL_STRONG** | TM-4786 flight PID | Some quantities are figure-only and require digitization |
| Rotary/spin aerodynamics | **CLOSED_MODEL / STRONG_CROSS_VALIDATION** | CR-3608 Appendix contains complete measured rotary-balance tables for 1/10-scale model | Do not relabel scale-model coefficients as full-scale exact |
| Transonic/supersonic derivatives | **CLOSED_FAMILY_CROSS_VALIDATION / OPEN_EXACT** | TP-2000-209033, Mach 0.85–1.30 | F-18B SRA differs from HARV 160780 |
| Control limits | **CLOSED_COMPATIBLE** | TP-2000-209033 confirms basic F-18A/B limits for its program | Preserve existing configuration conflicts |
| Actuator/FCS dynamics | **PARTIAL / MODEL-EXACT where documented** | SRA CPT/RVDT discussion adds aeroelastic measurement evidence | Exact Phase-I physical actuator transfer remains configuration-sensitive |
| Propulsion model structure | **PARTIAL_STRONG** | CR-198052 + existing TM-4240 chain | Full numeric installed thrust deck still missing |
| Installed inlet recovery/distortion | **CLOSED_EXACT for documented HARV campaign / COMPATIBLE for target use** | TM-104329, 79 steady maneuvers at Mach 0.3/0.4 | Later HARV research configuration |
| Engine airflow estimation | **CLOSED_EXACT for documented HARV propulsion campaign** | CR-198052 | Not equivalent to thrust deck |
| Thrust vectoring loss | **CLOSED_EXACT/PARTIAL for Phase-II test state** | TM-4341 direct full-scale ground test | N/A to Phase-I target |
| Flight-test validation | **STRONG** | TM-4786 + TP-2000-209033 + existing Phase-II PID sources | Coverage now spans subsonic high-alpha and family-level transonic/supersonic regimes |

## Newly strengthened areas

### 1. Phase-I basic lateral-directional flight evidence

NASA-TM-4786 uses the same development F-18 serial 160780 in the 1987–1988 basic configuration, before later LEX-fence and thrust-vectoring modifications. This materially strengthens the target's subsonic/high-AOA stability and control validation chain.

### 2. Rotary/high-alpha numerical evidence

NASA-CR-3608 provides tabulated rotary-balance measurements through 90 deg AOA, including component buildup and control-deflection cases. This closes the public-data availability problem for the **scale-model rotary test**, while leaving full-scale transfer as a compatibility/validation problem.

### 3. Transonic/supersonic family validation

NASA/TP-2000-209033 provides full longitudinal and lateral-directional PID coverage for the F-18B SRA at Mach 0.85–1.30 and high dynamic pressure. Appendices C/D provide tabulated derivative increments. This is a strong family cross-validation source but not direct Phase-I HARV authority.

### 4. Installed inlet and airflow

NASA-TM-104329 provides a rigorously validated steady-inlet test database at Mach 0.3/0.4 over high AOA/AOSS conditions. NASA-CR-198052 adds F404 airflow-estimation correlations under distorted inlet flow. Together they create a much stronger installed-propulsion validation layer without closing the missing thrust deck.

## Numeric extraction backlog

| Priority | Source | Product | Provenance requirement |
|---|---|---|---|
| P0 | CR-3608 | Rotary-balance tables | Preserve model scale, configuration, AOA, beta, spin coefficient, controls and component state |
| P0 | TP-2000-209033 | Appendix C/D derivative-increment grids | Preserve CPT vs RVDT, Mach, altitude, measured vs interpolated/hold-last-value |
| P1 | TM-104329 | Tables 4/5 inlet database | Preserve Mach, AOA, AOSS and pressure/distortion descriptor definitions |
| P1 | TM-4341 | Tables 2/3 TVC thrust-loss data | Phase-II-only tag; preserve NPR, A8, PLA and true/commanded vane angle distinctions |
| P1 | TM-4786 | Figures 8–11 digitization | Mark as digitized-from-figure; retain uncertainty/quality caveats |
| P2 | CR-198052 | Airflow correlation data | Preserve engine serial / right-inlet test-state provenance |

## Remaining highest-priority gaps

1. **Basic F/A-18 nonlinear aerodynamic numeric arrays** feeding the original simulation lineage.
2. **MDC A7247/A8575 or a lawful NASA derivative package** containing the actual static/control coefficient lookup values.
3. **Complete F404 29-array numeric deck**, especially idle, military and minimum-afterburner data plus installation effects.
4. **Phase-I BuNo 160780 fuel-dependent mass/CG/inertia schedule** beyond the published reference state.
5. **Absolute transonic/supersonic Phase-I HARV derivative data**; the SRA source is family-level cross-validation rather than exact target authority.

## Current verdict

**PARTIAL — MORE RESEARCH**

Maverick now has a substantially stronger provenance-grade validation envelope. The remaining blocker to a fully source-grounded production-quality F/A-18 model is still the missing complete nonlinear aerodynamic and propulsion numeric deck.
