# Validation Fundamentals

Scope: flight-test parameter estimation; model correlation; trim validation; static-derivative validation; time-history comparison; perturbation testing; uncertainty (including digitization uncertainty); the difference between source fidelity and physical validity.

Existing Maverick validation docs: `Docs/Validation/FDM_VALIDATION_BASELINE_V1.md`, `FDM_VALIDATION_INVENTORY_V1.md`, `SHADOW_FDM_AUDIT_V1.md`.

---

## Planned subfolders

| Subfolder | Covers |
|---|---|
| `FlightTest/` | Flight-test data sets, manoeuvre design, instrumentation corrections |
| `Trim/` | Trim reference points and their provenance |
| `Perturbation/` | Doublets, 3-2-1-1, frequency sweeps; mode identification |
| `ParameterEstimation/` | Output error, equation error, frequency domain, closed-loop identification |
| `ModelVerification/` | EOM/environment check-cases, implementation-vs-truth-model testing |

## References

| Source ID | Use | Level |
|---|---|---|
| `NASA-TM-2015-218675` (NESC check-cases) | Time-history check-cases for 6-DOF EOM, atmosphere, gravitation and geodesy; includes a textbook-lineage F-16 (case 11 trim). Directly applicable to `MavSixDoFBody` | ABS |
| `NESC-AIAA-2013-5071` **R1** | Development of the check-cases | CAT |
| `NTRS-20150006038` | Follow-on check-case development | CAT |
| `NASA-SIMUPY-FLIGHT` **R1** | NASA open-source implementation of the check-cases with regression data (a second reference trajectory) | ABS |
| `AIAA-2002-4482` (Jackson & Hildreth) **R1** | DAVE-ML model exchange (format of the check-case models; ANSI/AIAA S-119 names) | CAT |
| `NASA-CR-2497` (McFarland) **R1** | Standard kinematic model: independent EOM statement for review | CAT |
| `NASA-RP-1168` | Output-error parameter estimation, end to end | ABS |
| `NTRS-19850011474` | Identification theory (report number to confirm) | CAT |
| `AIAA-2002-4704` (SIDPAC) **R1** | System identification toolbox description. The software itself is U.S.-persons only; not a project dependency | CAT |
| `JAIRCRAFT-2023-MORELLI-GRAUER` **R1** | Survey of NASA Langley identification methods | CAT |
| `MORELLI-SFTE-2023-RTPI` **R1** | Real-time piloted excitation (demonstrated on an F-16 simulation) | ABS |
| `NASA-TM-2013-218056` **R1** | Sensitivity of identified models to mass, geometry and sensor errors | LEAD |
| `NTRS-20070031030` **R1** | Automated simulation updates from flight data (HARV demonstration) | LEAD |
| `NASA-TR-R-433` **R1** | Inertia measurement accuracy: sets a floor on how tightly a model can be validated | CAT |
| `NTRS-20040087105` | Real-time frequency-domain estimation (HARV flight data) | ABS |
| `NASA-CR-198248` | Optimal input design validated in flight | ABS |
| `NASA-TM-4783` | Ground-to-flight correlation lessons | ABS |
| `NASA-CR-198250` | Implementation-vs-truth-model control-law validation | ABS |

## Validation vocabulary for Maverick claims

Use these words precisely in reports:

| Claim | Meaning |
|---|---|
| **Source-faithful** | The implementation reproduces the source model (tables, polynomials, laws) within stated tolerance |
| **Implementation-verified** | Agrees with a published check-case of the same model (e.g. NESC F-16 case 11). Proves the equations and table handling, not the aircraft |
| **Physically validated** | Agrees with **independent measurement** (flight or tunnel) within a stated uncertainty and domain |
| **Trim-consistent** | Trims exist and are self-consistent. No external trim reference was compared |
| **Behaviourally cross-checked** | Agrees with an external implementation (`CROSS_VALIDATION_ONLY` source). Not evidence of correctness |

A source-faithful model of a simulator is not a physically validated model of the aircraft. Independent evidence by pack: F/A-18 flight-derived derivatives (TM-4786 etc.); F-15 836 flight vs simulation time histories and derivative trends (repository digitization); F-16 only configuration-specific flight data (AFTI, VISTA, MATV); F-22 none.

## Independence rule

Descendants of one data set agree by construction: TP-1538 → Morelli → Stevens & Lewis → NESC → simupy-flight → AeroBench. Their agreement verifies code, not physics. See the repeated-number register in [`SOURCE_GRAPH.md`](../SOURCE_GRAPH.md#2-repeated-number-register).

## Digitization uncertainty

Any figure digitization follows `SCHEMA.md` §7. Record pixel resolution, axis-calibration error, line thickness and marker size. Propagate that into the tolerance used in a comparison. Never tighten a tolerance below the digitization uncertainty. The F-15 validation series (repository, 60 series) and the F/A-18 Max-AB thrust figure (±150 lbf reading uncertainty) are the existing examples.
