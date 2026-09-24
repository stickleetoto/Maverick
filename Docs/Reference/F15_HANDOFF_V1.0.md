# F-15 Handoff — V1 Freeze Checkpoint

```yaml
checkpoint:        F-15 V1 freeze (baseline, not end of work)
branch:            claude/f15-full-implementation
pr:                stickleetoto/Maverick#27   # DRAFT - never merge
base:              sol/f15-r2-lateral
freeze_sha:        43ba2d9ed207e2e3cd81fafd2be5087c200e312f   # V1 freeze content; this line added by the next commit
target_config_id:  NASA_F15B_836_SN74_0141_PRE_QUIET_SPIKE_BASELINE_F100_PW_100
mass_state_id:     NASA_F15B_836_BASELINE_8K_FUEL_MASS_STATE
unity:             6000.3.16f1
edit_scope:        Assets/MaverickFresh/Scripts/FlightDynamics/**, Docs/Reference/**
known_good:        481 passed / 0 failed (8 headless suites); 327 runtime + 33 editor scripts compile, 0 errors
playmode_flight:   NOT RUN
exact_path:        FAIL-CLOSED
research_path:     WP-1 done - F15_AFIT_BAUMANN_DAVISON_MACH06_20K_RESEARCH, structurally prepared at M0.6/20k only; uncontrolled (zero surface travel); 8,300 lbf total fixed thrust
```

## Code entry points — `Assets/MaverickFresh/Scripts/FlightDynamics/F15/`

| Concern | Start here |
|---|---|
| identity, physical dimensions, exact coefficient geometry | `MavF15ReferenceData` (`CreateExactTargetGeometry`, `PhysicalWingSpanM`) |
| mass / inertia | `MavF15MassReference`, `MavF15Table1MassStates` |
| geometry provenance (every S/c̄/b candidate, graded) | `MavF15ReferenceGeometrySources`, `MavF15SourceLineage` |
| profile provider (exact, fail-closed) | `MavF15FlightDynamicsProfile` |
| aerodynamics | `MavF15AeroModel` (modes) → `MavF15BaumannMach06Reference` / `…Longitudinal` / `…LateralDirectional` / `…Domain` |
| flight control | `MavF15FlightControlSystem` (gains, modes, provenance floor) → `MavF15ControlLaw` (11 stages) → `MavF15ControlActuator` |
| propulsion | `MavF15PropulsionSystem`, `MavF15PropulsionSkeleton`; F100 layer in `MavF100*` (deck, source data, paths, families, lineage) |
| the gate that must stay shut | `MavF100PathSeparation.DimensionalizeForTarget` |
| **research configuration** (WP-1) | `MavF15AfitResearchFlightDynamicsProfile`, `MavF15AfitResearchMassReference`, `MavF15AfitResearchFixedThrust` / `MavF15AfitResearchThrustSource`, `MavF15AfitResearchIdentity`, `MavF15InertiaBasis` |

## Validation suites — `…/FlightDynamics/Validation/`

`MavF15MassReferenceValidation` (45) · `MavF15ReferenceGeometryValidation` (51) · `MavF15BaumannTranscriptionValidation` (28) · `MavF15ControlPathValidation` (79) · `MavF15PropulsionValidation` (198) · `MavF15ResearchContaminationValidation` (32) · `MavF15ResearchProfileValidation` (35) · `MavF15ResearchPipelineValidation` (13, editor-only, drives the real body through the `StepPhysicsForValidation` seam — **not PlayMode**)

Run them headless via `MaverickFresh.FlightDynamics.EditorTools.MavFdmValidationBatchAdapter.RunBatch`. The exact command line is in `F15_USER_VALIDATION_PLAN_V1.0.md` §1.

## Source docs — `Docs/Reference/`

| Read for | Doc |
|---|---|
| state and authority at this checkpoint | `F15_FINAL_STATE_V1.0.md` |
| what blocks implementation | `F15_REMAINING_GAPS_V1.0.md` |
| what to do next, ranked | `F15_POST_FREEZE_OPPORTUNITIES_V1.0.md` |
| how the user validates | `F15_USER_VALIDATION_PLAN_V1.0.md` |
| research configuration | `F15_RESEARCH_PROFILE_V1.0.md` |
| target freeze, mass correction | `F15_FULL_SCALE_TARGET_FREEZE_V0.1.md` |
| S / c̄ / b audit | `F15_NASA836_REFERENCE_GEOMETRY_AUDIT_V0.1.md` |
| McDonnell lineage | `F15_A4172_SOURCE_LINEAGE_V0.1.md`, `F15_DN1180_SOURCE_LINEAGE_V0.1.md` |
| aero transcription | `F15_R2_TRANSCRIPTION_AUDIT_V0.1.md` |
| FCS | `F15_R3_FCS_IMPLEMENTATION_STATUS_V0.1.md` |
| propulsion | `F15_R5_F100_SOURCE_AUDIT_V0.1.md`, `F15_R5_F100_PROPULSION_STATUS_V0.1.md` |

## Must NOT be mixed

- The Baumann/ARO10 research aero, or its 608 / 15.94 / 42.8 geometry, with the exact 836 profile.
- The physical 42.8-ft span with the coefficient reference `b`.
- The spike-retracted or spike-extended mass columns with the Baseline target.
- The PW-100(3) characteristic (family A), the prototype 2-7/8 data (family B), and the 836 anchor (family C).
- The ≈23,500 lbf 836 anchor as a scale for the TP-1034 curve (gate shut: equivalence and sub-configuration unknown).
- The 30,000 lbf channel scale or the ~22.4 klbf bound as a design thrust, or the bound as an 836 limit.
- TM-72861 (preproduction F-15 No. 8) values as exact 836 FCS data.
- Research-model constants (e.g. Baumann's 8,300 lb trim thrust) inside R5 propulsion. The research thrust lives in `MavF15AfitResearchThrustSource` and refuses under any profile but the research one.
- The research profile, mass state or thrust with the exact profile, in either direction.
- The F-15A–D %MAC datum as 836 authority (decided: not promoted).

## Hard rules still in force

- Never merge.
- No scenes, prefabs, weapons, sensors or AI.
- F15Replacement ownership is not implemented; do not enable live flight.
- No guessed S/c̄/b, FCS gains, engine mounts or thrust scale.
- CP2903B must never be reconstructed.

## Next developer's first action

1. Check out the branch.
2. Run the eight suites (validation plan §1) and confirm **481 / 0**.
3. WP-1 is complete. Pick up **WP-2** from `F15_POST_FREEZE_OPPORTUNITIES_V1.0.md` §5. WP-3's trim comparison first needs research-scoped surface authority (§5 note).
