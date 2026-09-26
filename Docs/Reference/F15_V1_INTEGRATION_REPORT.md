# F-15 V1 — Integration Report

**Integration only.** This branch brings the frozen F-15 V1 research stack onto current `main`.
- It adds no new F-15 research, work package, feature, physics or tuning.
- The **F-15 AFIT/BAUMANN/DAVISON RESEARCH BASELINE V1 stays FROZEN** (`F15_RESEARCH_BASELINE_FREEZE_V1.0.md`).

| | |
|---|---|
| `main` at start | `e0d2b0e3328b87359da96d987f194e790e981856` (Merge aerospace public-source library R0-R2) |
| Frozen F-15 baseline | `6970b43491cc186e743dad074b62c2504d4241c8` (final research runtime closeout) |
| Merge base | `6499d0d815bd82437df852fb4a2771ecdf9c44c4` (Merge PR #23: A2A-Only Combat Freeze R0) |
| Integration branch | `sol/f15-integration-v1`, created fresh from `e0d2b0e` |
| Integration merge | `f99d2e3` (no-ff merge of `6970b43`) |
| Integration fix | `4690687` — Baseline v1 manifest accounts for the 31 F-15 validation surfaces (§4a) |
| Unity | 6000.3.16f1, headless, in a clean worktree of the integration branch |

---

## 1. Stack and order

| PR | Branch | Tip | Base |
|---|---|---|---|
| #24 | `sol/f15-r1-profile` | `e2b4b36` | `main` |
| #25 | `sol/f15-r2-baumann-static` | `bf18f94` | #24 |
| #26 | `sol/f15-r2-lateral` | `2fdd442` | #25 |
| #27 | `claude/f15-full-implementation` | `6970b43` | #26 |

- All three lower tips are ancestors of `6970b43`, so the stack is linear.
- The integration is one `--no-ff` merge of `6970b43` into `main`. It keeps all 32 stack commits in their original order as the merge's second parent.
- No intermediate commit was rewritten, squashed or redesigned.
- The old PRs #24–#27 are left untouched and open.

## 2. What `main` added since the merge base

4 commits (`c65aaa3`, `8926fe5`, `fdad147`, `e0d2b0e`) — the Aerospace Source Library R0–R2:
- **53 files, all added:** 51 under `Docs/Research/Aerospace/`, plus `Tools/build_aerospace_source_views.py` and `Tools/validate_aerospace_source_library.py`.
- **65,650 insertions, 0 deletions, 0 modifications.**
- **No overlap** with the 221 files the F-15 stack changes.
- No file under `Assets/`, and no shared flight-dynamics, validation-infrastructure or baseline-manifest file (`Tools/fdm_validation_baseline_v1.json` is unchanged).

## 3. Conflicts

**None.** Git merged automatically: the two sides touch disjoint file sets.

**Tree equivalence, checked on the merge result:**
- integrated tree vs `6970b43`: exactly the 53 main-added files differ (all additions). **No F-15 or shared file differs from the frozen baseline.**
- integrated tree vs `e0d2b0e`: exactly the 221 F-15-stack files differ. **No main file differs.**

## 4. Shared / core files — resolution per file

Each file was compared as **A** (`main` at `e0d2b0e`) against **B** (frozen `6970b43`).

| File | A (main) vs merge base | B (F-15) change | Resolution |
|---|---|---|---|
| `Core/MavFlightPhysicsOwnership.cs` | unchanged | research validation owner `F15AfitResearch`, safety hold OFF by default | B taken verbatim. Nothing on `main` to reconcile. F16Replacement, Legacy, Shadow and Fault rules are unchanged inside B. |
| `Core/MavSixDoFBody.cs` | unchanged | WP-4A environment resolution, load-set gravity channel, `TryApplyInitialKinematicState` (refused while armed) | B verbatim |
| `Core/MavFlightDynamicsLoadSet.cs` | unchanged | gravitational channel, `AppliedForceAeroBodyN`, single-owner checks | B verbatim |
| `Core/MavFlightDynamicsProfile.cs` | unchanged | virtual `ResolveEnvironment` (base returns the standard sample unchanged) | B verbatim |
| `Core/MavFlightEnvironment.cs` | absent | new (WP-4A) | B verbatim |
| `Validation/MavValidationEigenSolver.cs`, `Validation/MavValidationRk4Integrator.cs` | absent | new (WP-3D / 3E), validation-only | B verbatim |
| `Docs/F15_CLAUDE_FULL_IMPLEMENTATION_HANDOFF_V0.1.md` | unchanged | F-15 doc | B verbatim |

- No shared **source** file needed a hand-merged version, and **no shared-core code adaptation was made.**
- F-16 behaviour is covered by §6.

## 4a. Shared validation infrastructure — one integration defect, fixed

**Found.** On `main`, the official gate `Tools/run_fdm_validation.ps1` enumerates every C# file under its three authority roots at HEAD. It requires each file to be listed in `Tools/fdm_validation_baseline_v1.json`:
- `main`: 38 files, all listed;
- frozen `6970b43`, and so the merge: **69**.

The F-15 stack added 31 files under `FlightDynamics/Validation` and `FlightDynamics/Editor` and never listed them. It had always run its suites through the batch adapter directly, never through this runner. After the merge, the runner refused to start:
`INFRA_FAILURE — HEAD C# surface count drift: expected 38, found 69`.
Merged unchanged, this would have broken `main`'s official validation gate.

**Fixed (`4690687`, documented in `Docs/Validation/FDM_VALIDATION_INVENTORY_V1.md` §"F-15 V1 integration").** The 31 files are listed under the manifest's own convention for post-authority surfaces, the one already used for `MavFdmValidationBatchAdapter` and `MavSixDoFBodyInspector`:
- `EXCLUDED_WITH_REASON`, `execution_mode: EXCLUDED`, `counted: false`;
- `pinned_at: HEAD` with the file's blob;
- a stated reason: they are F-15 research validation, outside the F-16/shared Baseline v1 scope, and run by the F-15 procedure.

`expected_head_cs_surface_count` goes from 38 to 69.

**Verified unchanged:**
- the authority commit;
- the 41 existing entries and their blobs;
- every other manifest key;
- the counted set (25 suites) and the Baseline v1 totals;
- every validator source.

The runner now hash-verifies the 31 F-15 files like any other surface.

## 5. Frozen-baseline integrity (checked on the integrated **working tree**)

Every file was hashed with `git hash-object` and compared with its blob at the reference commit.

| Set | Files | Result |
|---|---|---|
| every file the F-15 stack changed, vs `6970b43` | 221 | **identical** |
| protected source/data files, vs `6970b43` | 50 | **identical** |
| every file `main` added, vs `e0d2b0e` | 53 | **identical** |

The 50 protected files are:
- Table VII data;
- WP-3D stability and WP-3E time-domain CSVs;
- Baumann/Davison coefficients, including CMMQ;
- research source dynamics and trim equations;
- research mass/inertia and the 8,300 lbf fixed thrust;
- source semantics and density/gravity;
- aero model and F100 layer;
- NASA 836 reference, profile, mass and FCS structure;
- control authority and the WP-4A runtime files;
- the shared core files;
- the Play Mode runner.

**Unchanged:** configuration separation (`NASA_F15B_836_SN74_0141_PRE_QUIET_SPIKE_BASELINE_F100_PW_100` vs `F15_AFIT_BAUMANN_DAVISON_MACH06_20K_RESEARCH`), NASA 836 fail-closed, research-only authority, a single live FDM owner, one gravity source, one thrust source, and no direct research Rigidbody writes. The suites below confirm this.

## 6. Validation after integration (actually executed on `sol/f15-integration-v1`)

| Gate | Result | Compared with the frozen baseline |
|---|---|---|
| Compile (Unity's Roslyn, Unity's own import) | **361 runtime + 34 editor scripts, 0 errors**; `.meta` GUIDs valid and unique | same |
| 17 headless F-15 suites | **757 / 0** | same per suite. Every PASS/FAIL line **and every numeric report line** (~5,500) is identical to the frozen run. |
| F-15 research Play Mode closeout | **39 / 0**, two runs | **byte-identical** to each other, to the frozen-baseline run, and to the report committed at `6970b43` (`Data/F15/runtime_closeout/f15rt_playmode_report.txt`) |
| 22 synchronous shared / F-16 suites | **1,247 / 0** | same per suite. The only line differences are the scan-root path (the run was in a worktree) and the U-009b/c telemetry-noise byte reading (468.99 vs 491.52 bytes/step, documented as noise). Scan file counts are unchanged (261 / 160). |
| Official baseline runner `Tools/run_fdm_validation.ps1` (sync + F-16 Play Mode suites), after `4690687` | **EXECUTION STATUS PASS — 1,585 / 0 over the 25 counted suites** (22 sync + the F-16 reference-flight, scheduler and shared-propulsion Play Mode suites), and the 3 TP-1538 Python validators PASS. Environment churn: one line-ending-only `ProjectSettings/EditorSettings.asset`, content-identical. Physics delta: NONE. | before `4690687`: INFRA_FAILURE (surface count drift, §4a) |
| Aerospace Source Library validator (`Tools/validate_aerospace_source_library.py`, from `main`) | see §7 | same result on pristine `main` |

## 7. Intentional non-numerical differences, and one pre-existing note

- **Differences from the frozen baseline:**
  - the 53 Aerospace Source Library files from `main`;
  - the Baseline v1 manifest and inventory entries for the 31 F-15 validation surfaces (§4a) — additive, no validator or total changed;
  - this report;
  - the scan-root path and telemetry-noise lines above, which are environmental.
- **No numerical F-15 result changed.** No expected value was edited.
- **Pre-existing on `main`, not caused by the integration:**
  - `validate_aerospace_source_library.py` reports `AIRCRAFT_NUMERIC_FIELD_INDEX.json: generated view is stale` when run on Windows. It does the same on a pristine checkout of `e0d2b0e`.
  - Cause: the builder renders `known_data_file` with the OS path separator (`Docs\\Research\\…` on Windows), while the committed view uses `/`.
  - With separators normalized, all 11 generated views match exactly, on `main` and on the integrated tree.
  - Left unchanged, because this integration does not modify Aerospace Source Library semantics. It is recorded for the library's owners.

## 8. Verdict

**FROZEN BASELINE INTACT.**
- Every F-15 file, dataset and expected result is byte-identical to `6970b43`.
- Every F-15 suite, the Play Mode closeout and the shared/F-16 regression reproduce the frozen results.
- `main`'s newer work is preserved unchanged.

**READY for review as the single integration PR into `main`** (not merged here).
