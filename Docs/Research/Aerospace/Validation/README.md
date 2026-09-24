# Validation Fundamentals

Scope: flight-test parameter estimation; model correlation; trim validation; static-derivative validation; time-history comparison; perturbation testing; uncertainty (including digitization uncertainty); the difference between source fidelity and physical validity.

Existing Maverick validation docs: `Docs/Validation/FDM_VALIDATION_BASELINE_V1.md`, `FDM_VALIDATION_INVENTORY_V1.md`, `SHADOW_FDM_AUDIT_V1.md`.

---

## Planned subfolders

| Subfolder | Covers |
|---|---|
| `FlightTest/` | Flight-test data sets, maneuver design, instrumentation corrections |
| `Trim/` | Trim reference points and their provenance |
| `Perturbation/` | Doublets, 3-2-1-1, frequency sweeps; mode identification |
| `ParameterEstimation/` | Output error, equation error, frequency domain, closed-loop identification |
| `ModelVerification/` | EOM/environment check-cases, implementation-vs-truth-model testing |

## Starter references (r0)

| Source ID | Use | Level |
|---|---|---|
| `NASA-TM-2015-218675` (NESC check-cases) | Time-history check-cases for 6-DOF EOM, atmosphere, gravitation and geodesy. Directly applicable to `MavSixDoFBody` verification | L2 |
| `NTRS-20150006038` | Follow-on check-case development | L1 |
| `NASA-RP-1168` | Output-error parameter estimation, end to end | L2 |
| `NTRS-19850011474` | Identification theory (report number to confirm) | L1 |
| `NTRS-20040087105` | Real-time frequency-domain estimation (demonstrated on HARV flight data) | L2 |
| `NASA-CR-198248` | Optimal input design validated in flight | L2 |
| `NASA-TM-4783` | Ground-to-flight correlation lessons | L2 |
| `NASA-CR-198250` | Implementation-vs-truth-model control-law validation | L2 |

## Validation vocabulary for Maverick claims

Use these words precisely in reports:

| Claim | Meaning |
|---|---|
| **Source-faithful** | The implementation reproduces the source model (tables, polynomials, laws) within stated tolerance |
| **Physically validated** | The model agrees with **independent measurement** (flight or tunnel) within a stated uncertainty and domain |
| **Trim-consistent** | Trims exist and are self-consistent. No external trim reference was compared |
| **Behaviourally cross-checked** | Agrees with an external implementation (`CROSS_VALIDATION_ONLY` source). Not evidence of correctness |

A source-faithful model of a simulator is not a physically validated model of the aircraft. Flight-derived derivatives (e.g. NASA-TM-4786) are the independent evidence for the F/A-18 HARV.

## Digitization uncertainty

Any figure digitization follows `SCHEMA.md` §7. Record pixel resolution, axis-calibration error, line thickness and marker size. Propagate that into the derivative tolerance used in a comparison. Never tighten a tolerance below the digitization uncertainty.
