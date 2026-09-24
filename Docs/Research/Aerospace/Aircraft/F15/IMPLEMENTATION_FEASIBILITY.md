# F-15 Implementation Feasibility (R1 consolidation)

**Feasibility only.** R1 does not redo or second-guess the implementation branch's F-15 work. This page restates, in the library's vocabulary, what public sources can and cannot support for the target `F15-CFG-NASA836-PRE-QS`, and where the repository's conclusions still need library-level confirmation.

---

## Verdicts (same scale as the F-16 pack)

| # | Question | NASA 836 target | Research profiles (AFIT / CR-186019) |
|---|---|---|---|
| A | Six-axis coefficient model without guessed coefficients | **NOT SUPPORTED.** No public 836 or production coefficient database; no reference geometry | PARTIAL: single-condition curve fits (AFIT, research only) |
| B | Mass / inertia closure | **SUPPORTED for one loading** (Table 1 Baseline column, 8,000 lb fuel; repository reading; conflict F15-X1 resolved on the implementation branch) | Davison mass set (research) |
| C | Source-honest FCS approximation | **PARTIAL.** Exact 836 *structure* is public (block diagrams); gains, limits and rates are not. Switch thresholds conflicted (F15-X2) | Thesis-specific laws |
| D | Dimensional propulsion | **NOT SUPPORTED.** Normalised PW-100(3) curves and an approximate SLS figure only; build unknown; spec classified | Fixed-thrust research profile only |
| E | Trim validation | **PARTIAL.** Flight-derived Cn_beta / Cm_alpha trends (validation only) | — |
| F | Trajectory validation | **SUPPORTED as validation data**: flight vs simulation time histories, 60 digitized series (repository), exact airframe | — |
| G | Research PlayMode | Structural readiness only (neutral FCS output by construction on the implementation branch) | A research profile exists on the implementation branch |

## Centralised, not redone

- The implementation branch already enforces the configuration boundaries this library would require: exact vs research profiles, RPV transfer policy, F100 build separation and the CP2903B prohibition.
- The library's contribution is the **source IDs, the configuration IDs and the conflict register** (F15-X1 to X8), so the F-15 work can be audited against the same rules as the F-16 and F/A-18.

## What would change a verdict

| Verdict | Would change with |
|---|---|
| A | A public baseline F-15B/836 aerodynamic model, or McDonnell/Boeing simulation data released publicly, with its reference geometry |
| C | Public DN-1180 (or the AFFTC reports behind TM-72861) plus actuator data |
| D | A public F100-PW-100 dimensional deck with a demonstrated build match to 836's engines |
