# Flight Controls Fundamentals

Scope: SAS, CAS and command augmentation; gain scheduling; ARI-like coupling; actuator models (rate limits, first-order dynamics); control allocation; trim; linearization; control-law validation.

Maverick rule (from the FDM architecture): **a controller is never called a real aircraft's FCS unless it is actually sourced and validated.** NASA research laws must stay labelled as research laws.

---

## Planned subfolders

| Subfolder | Covers |
|---|---|
| `SAS_CAS/` | Stability/command augmentation structures, feed-forward, command shaping |
| `GainScheduling/` | Scheduling variables (q̄, Mach, α), interpolation of gains |
| `Actuators/` | Position/rate limits, first-order and higher-order actuator models, hinge-moment limits |
| `Trim/` | Trim formulations and solvers; trim validation references |
| `Linearization/` | Linear models about trim; modal analysis |
| `Validation/` | Control-law verification: implementation vs truth model, HIL, closed-loop system identification |

## Starter references (r0)

| Source ID | Use | Level |
|---|---|---|
| `MIL-HDBK-1797` | Flying-qualities criteria (Distribution A per search extract; check the revision history before citing) | L2 |
| `NASA-RP-1207` | Linear model derivation for control design and checking | L2 |
| `NASA-TM-110217` | Worked example of a **code-level** research control-law specification (HARV ANSER) | L2 |
| `NASA-TP-3446`, `NASA-TP-1998-208465` | Research CAS design, linear analysis (eigenvalues, margins, robustness, servo-elastic) and nonlinear batch/piloted results | L2 |
| `NASA-TP-1998-208463` | CRAFT design method (title/authors to confirm) | L1 |
| `NASA-TM-110228` | Control allocation by inversion of effector data (TV mixer) | L2 |
| `NASA-CR-198250` | Verifying a re-coded control law against a batch "truth model" in HIL | L2 |
| `NTRS-19980200994` | Closed-loop system identification for flying-qualities evaluation | L2 |
| `JGCD-2011-CHAKRABORTY-LINEAR` | Linear vs nonlinear robustness analysis of baseline/revised laws (falling leaf). Cross-validation only | L2 |

## Actuator data found in r0 (F/A-18)

See [`Aircraft/FA18/KNOWN_DATA.md`](../Aircraft/FA18/KNOWN_DATA.md#actuators). HARV simulation limits/rates and AAW rates are both captured. **They belong to different configurations** and conflict on rudder rate (82 vs 56 deg/s). Neither is L4 yet.

## Collection targets (not yet searched)

- Public NASA references on actuator modelling (rate saturation effects, PIO and rate-limit onset).
- Public gain-scheduling methodology reports (NASA/AFRL).
- Aileron-rudder interconnect (ARI) descriptions in public NASA reports for any aircraft, labelled per aircraft.
