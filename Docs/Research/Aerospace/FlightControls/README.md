# Flight Controls Fundamentals

Scope: SAS, CAS and command augmentation; gain scheduling; ARI-like coupling; actuator models (rate limits, first- and second-order dynamics); rate limiting and PIO; control allocation; trim; linearization; control-law validation.

Maverick rule (from the FDM architecture): **a controller is never called a real aircraft's FCS unless it is actually sourced and validated.** NASA research laws stay labelled as research laws. After R1 no production FCS gain set is public for any of the four packs.

---

## Planned subfolders

| Subfolder | Covers |
|---|---|
| `SAS_CAS/` | Stability/command augmentation structures, feed-forward, command shaping |
| `GainScheduling/` | Scheduling variables (q̄, Mach, alpha), interpolation of gains |
| `Actuators/` | Position/rate limits, actuator models, hinge-moment limits |
| `RateLimitingPIO/` | Rate saturation, PIO criteria, open-loop onset points |
| `Trim/` | Trim formulations and solvers; trim validation references |
| `Linearization/` | Linear models about trim; modal analysis |
| `Validation/` | Implementation vs truth model, HIL, closed-loop identification |

## References

| Source ID | Use | Level |
|---|---|---|
| `MIL-HDBK-1797` | Flying-qualities criteria (Distribution A per search extract; check revision history before citing) | ABS |
| `RTO-TR-029` **R1** | NATO RTO "Flight Control Design - Best Practices" (Dec 2000) | CAT |
| `AIAA-95-3204` (Duda) **R1** | Rate-limiting elements and a PIO criterion: why actuator rates must be sourced, not tuned casually | CAT |
| `NRC-1997-AVIATION-SAFETY-PILOT-CONTROL` **R1** | Adverse pilot-vehicle interactions, including the YF-22 PIO case | CAT |
| `NASA-TM-101684` **R1** | Nose-down pitch control requirement at high alpha | CAT |
| `NADC-88020-60` **R1** | High-alpha flying-qualities methods | CAT |
| `NASA-RP-1207` | Linear model derivation for control design and checking | ABS |
| `NASA-TM-110217` | Worked example of a **code-level** research control-law specification (HARV ANSER) | ABS |
| `NASA-TP-3446`, `NASA-TP-1998-208465` | Research CAS design, linear and nonlinear analysis | ABS |
| `NASA-TP-1998-208463` | CRAFT design method (title/authors to confirm) | CAT |
| `NASA-TM-110228` | Control allocation by inversion of effector data (TV mixer) | ABS |
| `NASA-CR-198250` | Verifying a re-coded control law against a batch "truth model" in HIL | ABS |
| `NTRS-19980200994` | Closed-loop identification for flying-qualities evaluation | ABS |
| `JGCD-2011-CHAKRABORTY-LINEAR` | Linear vs nonlinear robustness analysis (falling leaf). Cross-validation only | ABS |

## Per-aircraft FCS source status (R1)

| Aircraft | Public architecture | Public numeric gains | Research approximations | Details |
|---|---|---|---|---|
| F-16 | TP-1538 simulated CAS; TN D-8176 limiters/ARI/yaw damper; Droste & Walker case study (paywalled) | None for production; TP-1538's own gains unconfirmed | AFTI, VISTA, MATV laws; textbook laws | [`F16_FCS_SOURCE_GRAPH.md`](../Aircraft/F16/F16_FCS_SOURCE_GRAPH.md) |
| F-15 | Exact NASA 836 block diagrams (repository reading) | None | AFIT thesis laws | [`F15/SOURCE_GRAPH.md`](../Aircraft/F15/SOURCE_GRAPH.md) (conflict F15-X2 on switch thresholds) |
| F/A-18 | f18bas simplified OFP 8.3.3 CAS (printed, repository reading); RFCS/ANSER research laws | f18bas CAS tables (simplified); research laws | NASA-0/1A/2, ANSER | [`FA18/SOURCE_GRAPH.md`](../Aircraft/FA18/SOURCE_GRAPH.md) |
| F-22 | Triplex FLCS with pitch TV; 1996 design philosophy and CAP/damping targets | None | None public | [`PUBLIC_SOURCE_FEASIBILITY.md`](../Aircraft/F22/PUBLIC_SOURCE_FEASIBILITY.md) |

## Actuator data by lineage (never merged)

| Aircraft / lineage | What is recorded | Status |
|---|---|---|
| F/A-18 f18bas (TM-107601 Table 8.7) | First-order lags and rate/position limits for 5 surfaces | Repository reading |
| F/A-18 f18harv (TM-110216 Tables 6.1/6.2) | Second-order models, rates, limits (incl. TV vanes) | Repository reading |
| F/A-18 HARV Phase II (TP-97-206539 Table 1) | Surface limits and rates | Repository reading |
| F/A-18 AAW 853 | Rates (r0 extract) | Separate airframe |
| F-16 TP-1538 | Position limits only; **no sourced rate** | Repository reading |
| F-16 textbook | 60 / 80 / 120 deg/s, 0.0495 s | Cross-validation only (R1-RN3) |
| F-15 NASA 836 | None printed | Gap |
| F-15 CR-186019 | 24 deg/s, 20/(s+20) | Declared generic model |

## Collection targets (not yet searched)

- Public gain-scheduling methodology reports (NASA/AFRL).
- ARI descriptions in public NASA reports for any aircraft, labelled per aircraft.
