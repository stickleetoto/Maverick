# FDM Validation Inventory v1 — Authority-exact candidate

**Authority:** `ef25b91857b2bd4ee4961d4eba1da390d8ee2180` (post-consolidation mainline)
**Baseline state:** `CANDIDATE`

The agreed authority inventory contains **37 tracked C# surfaces** across the three declared roots and **3 TP-1538 Python validators**, for **40 explicit manifest entries**. No entry is implicit. Helpers, aggregate wrappers, probes, and editor-only utilities remain visible as `EXCLUDED_WITH_REASON` rather than disappearing.

Classification totals: **10 `OFFLINE_ELIGIBLE`**, **18 `UNITY_REQUIRED`**, **12 `EXCLUDED_WITH_REASON`**. Of the 40 entries, **25 are counted C# suites**; the three Python validators are gating validators but are not added to the C# assertion aggregate unless policy is explicitly changed in a later baseline revision.

| # | ID | Classification | Counted | Source cardinality | Path / exclusion reason |
|---:|---|---|:---:|---:|---|
| 1 | `f16_propulsion` | `OFFLINE_ELIGIBLE` | yes | 20 | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavF16PropulsionValidation.cs` |
| 2 | `f16_reference_flight_rig` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavF16ReferenceFlightRig.cs` — fixture/rig consumed by the reference-flight suite; not an independent assertion surface |
| 3 | `f16_reference_flight_scenarios` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavF16ReferenceFlightScenarios.cs` — scenario implementation consumed by MavF16ReferenceFlightValidation; counting it separately would double count |
| 4 | `f16_reference` | `OFFLINE_ELIGIBLE` | yes | 17 | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavF16ReferenceValidation.cs` |
| 5 | `f16_tp1538_runtime` | `UNITY_REQUIRED` | yes | 137 | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavF16Tp1538RuntimeValidation.cs` |
| 6 | `fdm_ownership_scan_runtime` | `OFFLINE_ELIGIBLE` | yes | dynamic | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavFlightDynamicsOwnershipScan.cs` |
| 7 | `fdm_phase1` | `OFFLINE_ELIGIBLE` | yes | 86 | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavFlightDynamicsPhase1Validation.cs` |
| 8 | `fdm_phase2` | `OFFLINE_ELIGIBLE` | yes | 192 | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavFlightDynamicsPhase2Validation.cs` |
| 9 | `fdm_phase3` | `OFFLINE_ELIGIBLE` | yes | 162 | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavFlightDynamicsPhase3Validation.cs` |
| 10 | `fdm_scheduler_probe` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavFlightDynamicsSchedulerProbe.cs` — probe state/components are consumed by the scheduler validation; not an independent suite |
| 11 | `gyroscopic_moment` | `OFFLINE_ELIGIBLE` | yes | 28 | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavGyroscopicMomentValidation.cs` |
| 12 | `integration_legacy_owner_fixture` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavIntegrationTestLegacyOwner.cs` — fixture/legacy-owner test component; no standalone result contract |
| 13 | `propulsion_playmode_probe` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavPropulsionPlayModeProbe.cs` — probe consumed by MavSharedPropulsionPlayModeLifecycle; not an independent suite |
| 14 | `shared_propulsion` | `UNITY_REQUIRED` | yes | dynamic | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavSharedPropulsionValidation.cs` |
| 15 | `f16_engine_law_registrar_editor` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Editor/MavF16EngineLawRegistrarEditor.cs` — registration/bootstrap infrastructure; no independent validation result |
| 16 | `f16_reference_flight` | `UNITY_REQUIRED` | yes | dynamic | `Assets/MaverickFresh/Scripts/FlightDynamics/Editor/MavF16ReferenceFlightValidation.cs` |
| 17 | `f16_reference_editor_wrapper` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Editor/MavF16ReferenceValidationEditor.cs` — menu/editor presentation wrapper around MavF16ReferenceValidation; excluded to prevent duplicate assertions |
| 18 | `f16_tp1538_editor_wrapper` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Editor/MavF16Tp1538RuntimeValidationEditor.cs` — menu/editor presentation wrapper around MavF16Tp1538RuntimeValidation; excluded to prevent duplicate assertions |
| 19 | `fdm_freeze_hardening` | `UNITY_REQUIRED` | yes | dynamic | `Assets/MaverickFresh/Scripts/FlightDynamics/Editor/MavFlightDynamicsFreezeHardeningValidation.cs` |
| 20 | `fdm_integration` | `UNITY_REQUIRED` | yes | dynamic | `Assets/MaverickFresh/Scripts/FlightDynamics/Editor/MavFlightDynamicsIntegrationValidation.cs` |
| 21 | `fdm_scheduler` | `UNITY_REQUIRED` | yes | dynamic | `Assets/MaverickFresh/Scripts/FlightDynamics/Editor/MavFlightDynamicsSchedulerValidationEditor.cs` |
| 22 | `shared_propulsion_lifecycle` | `UNITY_REQUIRED` | yes | dynamic | `Assets/MaverickFresh/Scripts/FlightDynamics/Editor/MavSharedPropulsionPlayModeLifecycle.cs` |
| 23 | `shared_propulsion_unity` | `UNITY_REQUIRED` | yes | dynamic | `Assets/MaverickFresh/Scripts/FlightDynamics/Editor/MavSharedPropulsionUnityValidation.cs` |
| 24 | `sixdof_validation_extensions` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Editor/MavSixDoFBodyValidationExtensions.cs` — helper extensions consumed by validation suites; no independent result contract |
| 25 | `aircraft_identity_ownership_scan` | `UNITY_REQUIRED` | yes | dynamic | `Assets/MaverickFresh/Scripts/Editor/MavAircraftIdentityOwnershipScan.cs` |
| 26 | `aircraft_identity_validation` | `UNITY_REQUIRED` | yes | dynamic | `Assets/MaverickFresh/Scripts/Editor/MavAircraftIdentityValidation.cs` |
| 27 | `aircraft_scene_wiring_scan` | `UNITY_REQUIRED` | yes | dynamic | `Assets/MaverickFresh/Scripts/Editor/MavAircraftSceneWiringScan.cs` |
| 28 | `aircraft_startup_order` | `UNITY_REQUIRED` | yes | dynamic | `Assets/MaverickFresh/Scripts/Editor/MavAircraftStartupOrderValidation.cs` |
| 29 | `aoa_limiter_ownership` | `UNITY_REQUIRED` | yes | dynamic | `Assets/MaverickFresh/Scripts/Editor/MavAoALimiterOwnershipValidation.cs` |
| 30 | `envelope_protection_ownership` | `UNITY_REQUIRED` | yes | dynamic | `Assets/MaverickFresh/Scripts/Editor/MavEnvelopeProtectionOwnershipValidation.cs` |
| 31 | `phase5a_ownership` | `UNITY_REQUIRED` | yes | dynamic | `Assets/MaverickFresh/Scripts/Editor/MavPhase5AOwnershipValidation.cs` |
| 32 | `phase5_writer_scan` | `UNITY_REQUIRED` | yes | dynamic | `Assets/MaverickFresh/Scripts/Editor/MavPhase5WriterScan.cs` |
| 33 | `turn_dynamics_migration` | `UNITY_REQUIRED` | yes | dynamic | `Assets/MaverickFresh/Scripts/Editor/MavTurnDynamicsMigrationValidation.cs` |
| 34 | `turn_dynamics_phase4b` | `UNITY_REQUIRED` | yes | dynamic | `Assets/MaverickFresh/Scripts/Editor/MavTurnDynamicsPhase4BValidation.cs` |
| 35 | `phase5_checkpoint_wrapper` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/Editor/MavPhase5CheckpointValidation.cs` — aggregate wrapper over constituent validation surfaces; excluded to prevent duplicate counting |
| 36 | `scene_pack_editor` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/Editor/MavScenePackEditor.cs` — editor tooling, not an FDM assertion suite; tracked explicitly because it resides in the agreed Editor inventory root |
| 37 | `tp1538_python_runtime_semantics` | `OFFLINE_ELIGIBLE` | no | — | `Tools/validate_f16_tp1538_runtime_semantics.py` |
| 38 | `tp1538_python_thrust_deck` | `OFFLINE_ELIGIBLE` | no | — | `Tools/validate_f16_tp1538_thrust_deck.py` |
| 39 | `tp1538_python_thrust_deck_independent` | `OFFLINE_ELIGIBLE` | no | — | `Tools/validate_f16_tp1538_thrust_deck_independent.py` |

## Authority roots

- `Assets/MaverickFresh/Scripts/FlightDynamics/Validation` — every tracked `.cs` file at the authority commit is represented above.
- `Assets/MaverickFresh/Scripts/FlightDynamics/Editor` — every tracked `.cs` file at the authority commit is represented above.
- `Assets/MaverickFresh/Scripts/Editor` — every tracked `.cs` file at the authority commit is represented above.
- `Tools/validate_f16_tp1538_*.py` — exactly the three TP-1538 Python validators represented above.

## Inventory enforcement

The PowerShell runner obtains the C# path set directly from `git ls-tree -r --name-only <authority>` for all three roots and compares it to the manifest. It separately enumerates `Tools/validate_f16_tp1538_*.py`. Any missing, added, or unlisted path is a hard failure before validation begins. It then verifies each file with `git hash-object` against the `git_blob` recorded in the manifest.

The new Baseline v1 adapter itself is **implementation infrastructure**, not an authority validation surface: it did not exist at `bb3a527...`, so it is listed under `implementation_paths` rather than being smuggled into the authority inventory.

## Execution classification semantics

- `OFFLINE_ELIGIBLE`: the validator semantics are runnable **without Unity Editor and without PhysX**. This is a semantic classification, not an execution-transport label. The candidate runner may still use `UNITY_EDITOR_SYNC` for a C# offline-eligible suite to avoid introducing a separate stub/compiler apparatus.
- `UNITY_REQUIRED`: the validator semantics require **real UnityEngine object/component/editor/runtime behavior and/or Play Mode/PhysX**. A suite is Unity-required even when it is synchronous and does not enter Play Mode.
- `EXCLUDED_WITH_REASON`: tracked and hash-verified but not independently executed/counted because it is a helper, fixture, wrapper, probe, registration hook, or non-validator utility.

The manifest therefore carries a separate `execution_mode`: `UNITY_EDITOR_SYNC`, `UNITY_PLAYMODE`, `PYTHON`, or `EXCLUDED`. Classification states what semantics the validator requires; execution mode states how this candidate runner launches it.

Two corrections are material here. `MavF16Tp1538RuntimeValidation` is `UNITY_REQUIRED` because D4 builds the real in-memory F-16 reference rig and exercises the shared propulsion path using Unity object/component semantics. `MavSharedPropulsionValidation` is `UNITY_REQUIRED` because its synthetic scaffolding creates real `GameObject` instances and attaches production components with `AddComponent`. Neither is offline-eligible under the strict definition above.

`MavF16ReferenceFlightValidation` and `MavSharedPropulsionPlayModeLifecycle` retain their existing self-exiting batch entry points. The scheduler uses only the new headless orchestration bridge; the existing scheduler probe construction and result state remain authoritative.

No line in this inventory constitutes an execution PASS. Runtime logs from the authority checkout are still required before any candidate total can be reported.

## Re-pin, 2026-09-18

Re-pinned from `460713aeb93ad5345edf02562f9ab20c8c3efe9b` to the post-consolidation mainline `ef25b91857b2bd4ee4961d4eba1da390d8ee2180` after PR #11 merged.

The 40th entry is new. `MavSixDoFBodyInspector.cs` was added to `FlightDynamics/Editor` by `7ef0c3b`,
which is inside a declared authority root, and the previous inventory could not see it: the runner
enumerated the roots only at the authority commit, so anything added afterwards was invisible to the
integrity checks. The runner now enumerates at `HEAD` as well, and an unlisted surface is a hard
failure there too.

| # | ID | Classification | Counted | Source cardinality | Path / exclusion reason |
|---:|---|---|:---:|---:|---|
| 40 | `sixdof_body_inspector` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Editor/MavSixDoFBodyInspector.cs` — editor-only custom Inspector: it draws existing debug fields of `MavSixDoFBody` to reduce repaint cost and contains no assertions, so it is not an independent assertion surface |

No other blob pin changed: no validation surface drifted between `460713aeb9` and `ef25b91857`.

## F-15 V1 integration, 2026-09-27

The frozen F-15 V1 research stack (#24–#27, frozen at `6970b43`) was integrated onto `main` at `e0d2b0e`. It adds **31 C# files** under the declared `FlightDynamics/Validation` and `FlightDynamics/Editor` roots:
- 17 F-15 validation suites;
- the F-15 research Play Mode closeout driver and runner;
- 10 F-15 helper/data files;
- 2 validation-only numerical helpers (`MavValidationEigenSolver`, `MavValidationRk4Integrator`).

The HEAD enumeration therefore listed 69 surfaces against the manifest's 38, and the runner refused to start (`HEAD C# surface count drift`).

**Resolution.** Each of the 31 is listed explicitly. `expected_head_cs_surface_count` is now 69. The existing convention for post-authority surfaces is followed:
- `EXCLUDED_WITH_REASON`, `execution_mode: EXCLUDED`, `counted: false`;
- `pinned_at: HEAD`;
- the file's blob;
- a mandatory reason.

**Why excluded.** They are F-15 research validation, outside the F-16/shared scope of Baseline v1. The F-15 suites run through their own documented procedure (`Docs/Reference/F15_USER_VALIDATION_PLAN_V1.0.md` §1: 17 suites, 757 / 0, plus the Play Mode closeout, 39 / 0).

**Unchanged.** The authority commit, the 41 earlier entries (38 C# + 3 Python) and their blobs, the counted set, the execution modes and the Baseline v1 totals. No validator semantics changed. Every one of the 31 is now hash-verified by the runner, as for any other surface.

Rows are numbered in manifest order. Entry 41 is `fdm_validation_batch_adapter`, which is listed under `implementation_paths`.

| # | ID | Classification | Counted | Source cardinality | Path / role |
|---:|---|---|:---:|---:|---|
| 42 | `f15_research_runtime_flight_validation` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Editor/MavF15ResearchRuntimeFlightValidation.cs` — F-15 research Play Mode closeout driver |
| 43 | `f15_baumann_source_condition_validation` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavF15BaumannSourceConditionValidation.cs` — F-15 research validation suite |
| 44 | `f15_baumann_table_vii` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavF15BaumannTableVii.cs` — F-15 research validation helper/data |
| 45 | `f15_baumann_table_vii_data` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavF15BaumannTableViiData.cs` — F-15 research validation helper/data |
| 46 | `f15_baumann_transcription_validation` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavF15BaumannTranscriptionValidation.cs` — F-15 research validation suite |
| 47 | `f15_control_path_validation` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavF15ControlPathValidation.cs` — F-15 research validation suite |
| 48 | `f15_mass_reference_validation` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavF15MassReferenceValidation.cs` — F-15 research validation suite |
| 49 | `f15_nasa836_fcs_structure_validation` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavF15Nasa836FcsStructureValidation.cs` — F-15 research validation suite |
| 50 | `f15_nasa836_validation_data` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavF15Nasa836ValidationData.cs` — F-15 research validation helper/data |
| 51 | `f15_nasa836_validation_data_validation` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavF15Nasa836ValidationDataValidation.cs` — F-15 research validation suite |
| 52 | `f15_nasa836_validation_series` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavF15Nasa836ValidationSeries.cs` — F-15 research validation helper/data |
| 53 | `f15_propulsion_validation` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavF15PropulsionValidation.cs` — F-15 research validation suite |
| 54 | `f15_reference_geometry_validation` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavF15ReferenceGeometryValidation.cs` — F-15 research validation suite |
| 55 | `f15_research_contamination_validation` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavF15ResearchContaminationValidation.cs` — F-15 research validation suite |
| 56 | `f15_research_control_authority_validation` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavF15ResearchControlAuthorityValidation.cs` — F-15 research validation suite |
| 57 | `f15_research_pipeline_validation` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavF15ResearchPipelineValidation.cs` — F-15 research validation suite |
| 58 | `f15_research_profile_validation` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavF15ResearchProfileValidation.cs` — F-15 research validation suite |
| 59 | `f15_research_runtime_flight_validation_runner` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavF15ResearchRuntimeFlightValidationRunner.cs` — F-15 research Play Mode closeout runner |
| 60 | `f15_research_runtime_prerequisites_validation` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavF15ResearchRuntimePrerequisitesValidation.cs` — F-15 research validation suite |
| 61 | `f15_research_stability_analysis` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavF15ResearchStabilityAnalysis.cs` — F-15 research validation helper/data |
| 62 | `f15_research_stability_conflict_audit` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavF15ResearchStabilityConflictAudit.cs` — F-15 research validation helper/data |
| 63 | `f15_research_stability_validation` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavF15ResearchStabilityValidation.cs` — F-15 research validation suite |
| 64 | `f15_research_time_domain_stability` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavF15ResearchTimeDomainStability.cs` — F-15 research validation helper/data |
| 65 | `f15_research_time_domain_validation` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavF15ResearchTimeDomainValidation.cs` — F-15 research validation suite |
| 66 | `f15_research_trim_solver_validation` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavF15ResearchTrimSolverValidation.cs` — F-15 research validation suite |
| 67 | `f15_research_turning_trim_validation` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavF15ResearchTurningTrimValidation.cs` — F-15 research validation suite |
| 68 | `f15_table_vii_equilibrium_reproduction` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavF15TableViiEquilibriumReproduction.cs` — F-15 research validation helper/data |
| 69 | `f15_table_vii_trim_recovery` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavF15TableViiTrimRecovery.cs` — F-15 research validation helper/data |
| 70 | `f15_table_vii_turning_recovery` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavF15TableViiTurningRecovery.cs` — F-15 research validation helper/data |
| 71 | `validation_eigen_solver` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavValidationEigenSolver.cs` — F-15 research validation helper/data |
| 72 | `validation_rk4_integrator` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavValidationRk4Integrator.cs` — F-15 research validation helper/data |

## F-15 pilot-controlled research aircraft V1, 2026-09-27

The pilot-controlled F-15 research aircraft adds **4 C# files** under the declared roots:
- the headless suite `MavF15PilotControlValidation`;
- the Play Mode flight-test runner and its driver;
- the editor prefab builder.

`expected_head_cs_surface_count` goes from 69 to 73.

They follow the F-15 V1 integration convention:
- `EXCLUDED_WITH_REASON`, not counted, `pinned_at: HEAD` with the file's blob;
- the reason is that they are F-15 validation or editor tooling, outside the F-16/shared Baseline v1 scope, run through the procedure in `Docs/Reference/F15_PILOT_CONTROLLED_V1.md`.

The counted set, the totals, the authority commit and every earlier entry are unchanged. Rows are numbered in manifest order.

| # | ID | Classification | Counted | Source cardinality | Path / role |
|---:|---|---|:---:|---:|---|
| 73 | `f15_pilot_controlled_flight_validation` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Editor/MavF15PilotControlledFlightValidation.cs` — F-15 pilot-controlled Play Mode flight-test driver |
| 74 | `f15_pilot_controlled_prefab_builder` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Editor/MavF15PilotControlledPrefabBuilder.cs` — F-15 pilot-controlled prefab builder (editor tooling) |
| 75 | `f15_pilot_control_validation` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavF15PilotControlValidation.cs` — F-15 pilot-controlled research aircraft headless validation suite |
| 76 | `f15_pilot_controlled_flight_validation_runner` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavF15PilotControlledFlightValidationRunner.cs` — F-15 pilot-controlled Play Mode flight-test runner |

## F-15 pilot control V2, 2026-09-27

F-15 pilot control V2 (a Maverick closed-loop control law for the same pilot-controlled aircraft) adds **3 C# files** under the declared roots:
- the headless suite `MavF15PilotControlV2Validation`;
- the Play Mode flight-test runner (V2 flight test + V1 vs V2 comparison) and its driver.

It also changes one listed file, the editor prefab builder `MavF15PilotControlledPrefabBuilder` (it now builds the V2 prefab as well). Its entry keeps its id and classification; only its `git_blob` is re-pinned.

`expected_head_cs_surface_count` goes from 73 to 76. Same convention as above: `EXCLUDED_WITH_REASON`, not counted, `pinned_at: HEAD` with the file's blob; the procedure is in `Docs/Reference/F15_PILOT_CONTROLLED_V2.md`. The counted set, the totals, the authority commit and every other entry are unchanged.

| # | ID | Classification | Counted | Source cardinality | Path / role |
|---:|---|---|:---:|---:|---|
| 77 | `f15_pilot_control_v2_flight_validation` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Editor/MavF15PilotControlledV2FlightValidation.cs` — F-15 pilot-control V2 Play Mode flight-test driver |
| 78 | `f15_pilot_control_v2_validation` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavF15PilotControlV2Validation.cs` — F-15 pilot-control V2 headless validation suite |
| 79 | `f15_pilot_control_v2_flight_validation_runner` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavF15PilotControlledV2FlightValidationRunner.cs` — F-15 pilot-control V2 Play Mode flight-test runner |

## F-15 pilot physics R2, 2026-09-28

F-15 pilot physics R2 (the research model's own first-order actuator lags as an opt-in revision of the pilot-controlled aircraft) adds **3 C# files** under the declared roots:
- the headless suite `MavF15PilotPhysicsR2Validation`;
- the Play Mode flight-test runner (`Validation/`) and its editor-only driver (`Editor/`, like the V1/V2 drivers, so no UnityEditor reference enters the runtime assembly).

`expected_head_cs_surface_count` goes from 76 to 79. Same convention: `EXCLUDED_WITH_REASON`, not counted, `pinned_at: HEAD` with the file's blob; see `Docs/FlightDynamics/F15_PHYSICS_R2.md`. The counted set, the totals, the authority commit and every other entry are unchanged.

| # | ID | Classification | Counted | Source cardinality | Path / role |
|---:|---|---|:---:|---:|---|
| 80 | `f15_pilot_physics_r2_validation` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavF15PilotPhysicsR2Validation.cs` — F-15 pilot physics R2 headless validation suite |
| 81 | `f15_pilot_physics_r2_flight_validation_runner` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavF15PilotPhysicsR2FlightValidationRunner.cs` — F-15 pilot physics R2 Play Mode flight-test runner |
| 82 | `f15_pilot_physics_r2_flight_validation` | `EXCLUDED_WITH_REASON` | no | — | `Assets/MaverickFresh/Scripts/FlightDynamics/Editor/MavF15PilotPhysicsR2FlightValidation.cs` — F-15 pilot physics R2 Play Mode flight-test driver |
