# F-22 Missing Data (R1 feasibility)

Everything needed for a 6-DOF model of the production F-22A is missing from the public record. The list is kept short on purpose: it is the reason for the feasibility verdict, not a shopping list.

| Missing | Configuration | Status | Notes |
|---|---|---|---|
| Reference area, MAC, reference span, moment reference | all | NOT_PUBLICLY_LOCATED | Do not derive from the fact-sheet span or photographs |
| Mass state with defined loading, CG, inertias | all | NOT_PUBLICLY_LOCATED | Fact-sheet weights are identity data |
| Static, dynamic and control aerodynamic coefficients | production, EMD | NOT_PUBLICLY_LOCATED | `F22-LANGLEY-1992-FST-1993-SPIN-DATA` packages exist (per NASA history) but are not public; `AIAA-99-4015` unread |
| FCS gains, schedules, limiter logic | production, EMD | NOT_PUBLICLY_LOCATED | `AIAA-96-3379` gives design philosophy and targets only |
| Actuator limits and rates (surfaces and nozzle) | all | NOT_PUBLICLY_LOCATED | |
| Installed F119 thrust deck, fuel flow, dynamics | production | NOT_PUBLIC_PROPRIETARY | `F119-PERFORMANCE-DECK`. Do not estimate from the class figure |
| Trim and trajectory data | all | NOT_PUBLICLY_LOCATED | |
| YF-22 coefficients, mass, gains | YF-22 | NOT_PUBLICLY_LOCATED | Would be `YF22_ONLY` even if found |

## Leads worth one more look (feasibility only)

1. `AIAA-99-4015` (Gillard, AFRL dynamic tunnel results): which model, which date, what is printed.
2. `AIAA-2013-0972` (SEEK EAGLE CFD, Distribution A): whether F-22 derivatives are printed.
3. `RTO-MP-SCI-162-P23`: public figures of the installed-thrust model architecture (no deck expected).
