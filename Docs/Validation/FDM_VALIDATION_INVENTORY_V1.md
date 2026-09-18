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
