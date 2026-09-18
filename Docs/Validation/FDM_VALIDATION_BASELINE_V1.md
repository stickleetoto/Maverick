# FDM Validation Baseline v1 — CANDIDATE

**Authority:** `ef25b91857b2bd4ee4961d4eba1da390d8ee2180` (post-consolidation mainline)
**State:** `CANDIDATE`
**Execution status:** `PASS` — 1585 passed / 0 failed across 25 counted suite results
**Last executed:** 2026-09-18, Unity `6000.3.16f1`, from a clean fully-committed checkout

This package defines a fail-closed, executable candidate for the current Maverick flight-dynamics validation baseline. It does **not** freeze a result and it does **not** promote historical Phase 5 totals into current expectations.

Historical `841 / 0` offline assertions and `36 / 36` mutation bites remain **EVIDENCE ONLY**. They are preserved in the manifest solely as historical context and are never used as expected output by the runner.

## Baseline authority and count policy

The runner requires the authority commit to be an **ancestor** of `HEAD`, not to equal it. Exact-HEAD was unshippable: committing the runner itself necessarily moves `HEAD` off the authority, so the baseline could never be recorded in the repository it validates. Integrity is preserved by the per-surface checks instead. It verifies the Git blob hash of every tracked validation surface in the manifest, and it compares **both** the authority tree and the `HEAD` tree against the manifest, so that a newly added, removed, or unlisted validation surface is a hard failure whether it was introduced before or after the authority commit.

Seven deterministic suites have source-derived cardinality known before execution:

| Suite | Source-derived cardinality |
|---|---:|
| `MavF16ReferenceValidation` | 17 |
| `MavF16PropulsionValidation` | 20 |
| `MavFlightDynamicsPhase1Validation` | 86 |
| `MavFlightDynamicsPhase2Validation` | 192 |
| `MavFlightDynamicsPhase3Validation` | 162 |
| `MavGyroscopicMomentValidation` | 28 |
| `MavF16Tp1538RuntimeValidation` | 137 |
| **Known subtotal** | **642** |

The subtotal is a source-cardinality sanity check, **not an execution result and not the final Baseline v1 total**. For every counted suite, the counts reported at runtime by the validator are authoritative. A deterministic cardinality mismatch is retained in the output but causes the candidate runner to fail closed.

Dynamic suites have no source count frozen in the manifest. Their runtime-reported counts determine the candidate total only after the real authority checkout is executed.

## Execution model

Classification and launch transport are intentionally separate. `OFFLINE_ELIGIBLE` means the validator semantics are runnable **without Unity Editor and without PhysX**. `UNITY_REQUIRED` means real UnityEngine object/component/editor/runtime behavior and/or Play Mode/PhysX is part of the semantics. In particular, `MavF16Tp1538RuntimeValidation` is Unity-required because D4 builds the real in-memory F-16 reference rig/shared-propulsion path, and `MavSharedPropulsionValidation` is Unity-required because it creates real `GameObject`/`AddComponent` production components.

The manifest carries a separate execution mode. `UNITY_EDITOR_SYNC` is used for synchronous C# suites in the real editor, `UNITY_PLAYMODE` for the existing Play Mode/scheduler state machines, `PYTHON` for the three TP-1538 Python validators, and `EXCLUDED` for non-independent surfaces. A C# suite may be semantically `OFFLINE_ELIGIBLE` while this candidate runner still launches it with `UNITY_EDITOR_SYNC`; that transport choice does not change its classification. No Unity/PhysX stubs are introduced.

`UNITY_REQUIRED` suites execute in the real Unity Editor. The existing batch state machines are reused unchanged for:

- `MavF16ReferenceFlightValidation.RunBatch` with `-p5crOut`.
- `MavSharedPropulsionPlayModeLifecycle.RunBatch` with `-p02bOut`.

The new `MavFdmValidationBatchAdapter` supplies only headless orchestration for synchronous suites and the FixedUpdate scheduler. For the scheduler it reuses the existing `MavFlightDynamicsSchedulerValidationEditor.StartProbeInPlayMode` implementation and `MavFlightDynamicsSchedulerProbeState`; it suppresses the existing interactive polling/dialog path and adds deterministic result-file and process-exit behavior. It does not reimplement scheduler physics or assertions.

A counted Unity suite must produce a valid runtime result with numeric `passed`/`failed` counts. Missing or malformed result markers are infrastructure failures, never passes.

## Fail-closed conditions

The runner exits nonzero for any of the following: wrong HEAD, source hash mismatch, unlisted/missing validation surface, unexpected dirty path, missing Python, missing Unity, Python validator nonzero exit, Unity validator failure, Unity compile/import failure, timeout, malformed or missing result file/marker, deterministic source-cardinality drift, checkout dirty-state change during execution, or post-run validation-source hash change.

Only the six candidate implementation files are allowed to be dirty when the runner begins. Any other dirty path is rejected before execution. The same status set and every authority validation-surface blob hash are rechecked in `finally`.

## Mutation v1

Mutation v1 is a **new-only** framework. The historical 36 mutations are not imported. This candidate intentionally contains zero admitted mutation probes because no historical probe is allowed to become a current mutation merely by renaming it, and no new production mutation is admitted without a reviewed exact one-occurrence preimage.

If `-RunMutations` is requested while the manifest contains zero reviewed new probes, the runner fails closed. When a new probe is added, the runner contract is:

1. Create a temporary workspace with `git archive <authority>`.
2. Copy the six uncommitted Baseline v1 implementation files into that temporary project.
3. Require the declared production preimage to occur **exactly once**.
4. Apply the replacement only in the temporary workspace.
5. Run the relevant validator in real Unity/Python as applicable.
6. Treat compile/import/runner failure as `INFRA_FAILURE`, never `KILLED`.
7. Record `KILLED` only when the relevant validator successfully runs and reports a semantic failure.
8. Delete the temporary workspace in `finally` and verify the authority checkout remained unchanged.

Mutation results are separate from the baseline assertion total.

## Invocation

From the real Windows authority checkout, with these six files applied but uncommitted:

```powershell
pwsh -NoProfile -File .\Tools\run_fdm_validation.ps1 `
  -UnityPath "C:\Program Files\Unity\Hub\Editor\<PROJECT_VERSION>\Editor\Unity.exe" `
  -PythonPath python
```

If `-UnityPath` is omitted, the runner first uses `UNITY_EXE`; otherwise it reads `ProjectSettings/ProjectVersion.txt` and accepts only that exact Unity Hub version. It does not silently choose a different installed editor.

The default result directory is outside the repository under `%TEMP%\MaverickFdmValidation-<timestamp>`. A specific external directory may be supplied with `-ResultsDir`.

Do **not** request `-RunMutations` for this initial candidate: the new mutation set is intentionally empty and the switch therefore fails closed by design.

## Output schema

The final file is `baseline_v1_result.json` and has this shape:

```json
{
  "schema_version": 1,
  "baseline": "FDM Validation Baseline v1",
  "baseline_state": "CANDIDATE",
  "authority_commit": "ef25b91857b2bd4ee4961d4eba1da390d8ee2180",
  "execution_status": "PASS | FAIL | INFRA_FAILURE",
  "started_utc": "...",
  "completed_utc": "...",
  "historical_841_36": "EVIDENCE_ONLY",
  "source_derived_known_subtotal": 642,
  "assertion_totals": {
    "passed": 0,
    "failed": 0,
    "counted_suite_results": 0
  },
  "suites": [
    {
      "id": "...",
      "path": "...",
      "classification": "OFFLINE_ELIGIBLE | UNITY_REQUIRED",
      "execution_mode": "UNITY_EDITOR_SYNC | UNITY_PLAYMODE | PYTHON",
      "counted": true,
      "status": "PASS | FAIL | INFRA_FAILURE",
      "passed": 0,
      "failed": 0,
      "exit_code": 0,
      "duration_ms": 0,
      "source_expected_assertions": null,
      "source_cardinality_match": null,
      "detail": "...",
      "log_file": "...",
      "result_file": "..."
    }
  ],
  "mutation": {
    "requested": false,
    "policy": "NEW_ONLY",
    "historical_probes_imported": false,
    "results": []
  },
  "fatal_error": "",
  "commit": null,
  "push": null,
  "physics_delta": "NONE"
}
```

Python validators that do not emit assertion counts can still gate the baseline by exit code; their `passed` and `failed` remain `null` and they are not added to the assertion total. A Python validator that does emit the standard `RESULT:` marker has those counts captured, but the manifest currently leaves the Python surfaces non-counted to avoid silently changing the C# assertion-total semantics.

## Candidate completion state

```text
BASELINE V1          CANDIDATE
IMPLEMENTATION FILES COMMITTED (all six, sol/fdm-validation-baseline-v1-r1)
EXECUTION            RUN 2026-09-18 ON A CLEAN COMMITTED CHECKOUT
RESULT               1585 passed / 0 failed / 25 counted suite results
COMMIT               yes
PUSH                 yes
PHYSICS DELTA        NONE
HISTORICAL 841/36    EVIDENCE ONLY
```

---

## Baseline V1 observed failure classification — RESOLVED

The earlier investigation on this baseline observed **1566 passed / 19 failed across 25 counted
suite results**. Those 19 were classified then as validation/fixture/scanner debt, with zero
demonstrated production flight-physics regressions.

All 19 have since been repaired, by the commits that the 2026-09 mainline consolidation integrated
(see `Docs/Mainline/MAINLINE_CONSOLIDATION_2026-09.md`). The suite population is unchanged: the same
25 counted suite results, the same 1585 total assertions.

| Suite | Failures then | Repaired by | Now |
|---|---:|---|---|
| `fdm_ownership_scan_runtime` | 1 | `00ae6e0` — `ContainsForbiddenToken` removed the `==` lexical false positive, `IsAllowedHandoverStateTransfer` allowlists the atomic handover's motion-state transfer, `IsValidationOnlyPath` excludes validation-only writes | PASS |
| `fdm_phase2` | 4 | `00ae6e0` — fixtures declare `angularDynamicsAcceptable`, plus the scan fix above | PASS |
| `fdm_freeze_hardening` | 4 | `2b7677e` — validators realigned to the Phase 5A authority, which forbids the Phase-3 controller from granting replacement ownership | PASS |
| `fdm_integration` | 9 | `2b7677e` — same realignment; these were a cascade downstream of the rejected initial handover | PASS |
| `aircraft_startup_order` | 1 | `00ae6e0` — `MavWTFeelPolishController` now records the skip reason instead of reporting a preset it did not apply | PASS |
| **Total** | **19** | | **0 failures** |

Verified execution on authority `ef25b91857b2bd4ee4961d4eba1da390d8ee2180`, Unity `6000.3.16f1`:

- 22 synchronous editor suites: 1247 passed / 0 failed
- 3 Play Mode suites: 338 passed / 0 failed (`f16_reference_flight` 168, `shared_propulsion_lifecycle` 169, `fdm_scheduler` 1)
- 3 TP-1538 Python validators: PASS, including the independent re-derivation and its mutation probes
- **25 counted suite results, 1585 passed, 0 failed**

Note on counting: the four scan-type surfaces (`fdm_ownership_scan_runtime`,
`aircraft_identity_ownership_scan`, `aircraft_scene_wiring_scan`, `phase5_writer_scan`) produce a
single binary result each rather than an assertion count, so they contribute 1 apiece to the 1585.

One known limitation is unrelated to these suites: `MavF16PoweredReferenceShadowSmoke` cannot arm,
because no engine profile declares a source envelope. Tracked as issue #12.


These failures are baseline evidence. They are not silently converted to PASS and are
not repaired as part of Baseline V1 creation.

