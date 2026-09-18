# Mainline Consolidation — 2026-09

Audit and integration record for the post-Phase-5 work that had accumulated across several
`sol/*` branches. Purpose: produce one clean, defensible mainline before any radar / missile /
weapon-system work starts.

Scope discipline: no weapons, no radar, no missiles, no flight-physics retuning. Nothing in this
consolidation changes aircraft tuning, reference data, scenes, prefabs, weapons, sensors or AI.

- Audited by: Claude Opus 5, driven by stickleetoto
- Unity used for the gate: `6000.3.16f1` (repository-authoritative, `ProjectSettings/ProjectVersion.txt`)
- Old `main`: `bb3a5273a8baf43eeb0b46277269d4b130c852d4` ("chore: preserve source newline")
- Integration candidate: `origin/sol/f16-powered-reference-smoke` @ `8215e1ce71af39ce3e69604ccd52ca458ee99c67`
- Integration branch: `sol/mainline-consolidation-2026-09` (candidate + one revert + this document)
- Integration state: **MERGED 2026-09-18 via PR #11.** New authoritative `main`:
  `28c174e7ce6504e3353b7aba18c75b66b43bf621` (§5)

## 1. Verified branch graph

All figures verified with `git merge-base`, `git rev-list --count` and `git cherry` against the
local repository and `origin` refs; none of the expected relationships were taken on trust.

Ahead/behind are relative to `main` (`bb3a527`).

| Branch | HEAD | Merge base with main | Ahead | Behind | Contained by | Unique commits | Class | Disposition |
|---|---|---|---|---|---|---|---|---|
| `main` | `bb3a527` | — | 0 | 0 | — | — | — | base of this audit |
| `origin/sol/f16-powered-reference-smoke` | `8215e1c` | `bb3a527` | 21 | 0 | — | 21 | AUTHORITATIVE_CANDIDATE | integrate (this PR) |
| `sol/f16-powered-reference-smoke` (local) | `2edd3f1` | `bb3a527` | 20 | 0 | its own remote | 0 | CONTAINED | local branch is 1 commit **behind** its remote; fast-forward it |
| `sol/fdm-validation-fixes` | `460713a` | `bb3a527` | 11 | 0 | powered-reference (strict ancestor) | 0 | SUPERSEDED_BY_INTEGRATION_CANDIDATE | keep as history; PR #10 superseded, do not merge separately |
| `sol/fdm-runner-fix` | `cf673da` | `bb3a527` | 18 | 0 | no | 2 (`c510e9b`, `cf673da`) | REQUIRES_REVIEW | branched off `a849c89`; carries `Tools/run_fdm_validation.ps1` (599 lines). Separate validation-tooling decision |
| `sol/reference-integration-post-pr8` | `d267586` | `d267586` | 0 | 2 | `main` | 0 | CONTAINED | already in main; nothing to merge (confirms the expectation) |
| `sol/f16-thrust-runtime` | `6b22f13` | `6b22f13` | 0 | 3 | `main` | 0 | CONTAINED | history only |
| `sol/fdm-validation-baseline-v1` | `bb3a527` | `bb3a527` | 0 | 0 | `main` | 0 | CONTAINED | label on main; the actual baseline work is uncommitted WIP (§7) |
| `origin/sol/pr2-ownership-fix-main` | `bb3a527` | `bb3a527` | 0 | 0 | `main` | 0 | CONTAINED | history only |
| `origin/sol/phase4a-turn-dynamics` | `ef68269` | `ef68269` | 0 | 4 | `main` | 0 | HISTORICAL | PR #8 merge point |
| `origin/sol/phase5-wip` | `1738337` | `1738337` | 0 | 5 | `main` | 0 | HISTORICAL | merged via PR #8 |
| `origin/sol/f16-validation`, `f16-5d-data`, `f16-validation-r2`, `f15-reference-r0`, `f15-fullscale-r05`, `f15-gap-closure-r0` | — | behind main | 1–2 | 4–10 | content in `main` | 0 (all patch-equivalent per `git cherry`) | SUPERSEDED | history only |
| `origin/sol/f15-source-gap-audit` | `1a3cd4b` | `ad57692` | 3 | 10 | no | 1 (`1a3cd4b`, docs) | REQUIRES_REVIEW | "docs(f15): prioritize remaining reference source gaps" is not in main |
| `origin/sol/fa18-f22-source-acquisition-r0` | `30ff6ef` | `bb3a527` | 16 | 0 | no | 16 (docs/data only) | REQUIRES_REVIEW | F/A-18 + F-22 public source acquisition; own integration decision, out of scope here |
| `origin/fam-flight-observer-v0.1` | `75a9851` | `27109bf` | 6 | 110 | no | 6 | REQUIRES_REVIEW | PR #1 open draft; FAM observer scripts. Out of scope |
| `refactor/physics-tuning-ownership-phase1` | `38e266e` | `27109bf` | 2 | 110 | no | 2 | SUPERSEDED | PR #2 closed as superseded; main's `a684c2a` version of `MavAircraftProfileApplier.cs` is far more developed |
| `claude/f16-live-fdm-phase1`, `claude/f16-trim-control-phase2`, `feature/f16-flight-dynamics-core`, `origin/feature/f16-propulsion-phase2`, `origin/sol/f16-trim-control-reviewfix` | — | behind main | 0 | 23–85 | `main` | 0 | HISTORICAL | phase history, keep |

Key confirmed relationships:

- `sol/fdm-validation-fixes` is a **strict ancestor** of the powered-reference branch
  (`git merge-base --is-ancestor` → true; `git rev-list --count powered-ref..validation-fixes` = 0).
  Its 11 commits are therefore already in the candidate: merging PR #10 separately and then the
  descendant would duplicate identical work for no benefit.
- `sol/reference-integration-post-pr8` is behind `main` with zero unique commits — already
  integrated, as expected.
- The candidate is a strict descendant of `main` (0 behind), so integration needs no cherry-picking.
- The expected powered-reference HEAD `8215e1c` is the **remote** tip. The local branch was one
  commit behind it (`2edd3f1`); the missing commit is documentation only.

## 2. Unique-commit audit (21 commits, `main..8215e1c`)

`P` = production code, `V` = validation/editor only, `D` = documentation only.

### 2.1 From `sol/fdm-validation-fixes` (11 commits)

| SHA | Title | Files | Kind | Physics behavior change | Verdict |
|---|---|---|---|---|---|
| `fcf78bd` | chore: stage FDM validation fix applicator | `.github/workflows/apply-fdm-validation-fixes.yml` | V | no | keep (transient) |
| `1ae3ea5` | chore: enable PR-triggered FDM fix applicator | same workflow | V | no | keep (transient) |
| `f518f3f` | chore: replace inactive workflow with local validation fix driver | deletes the workflow | V | no | keep — **net effect: no workflow in the tree** |
| `fa85328` | test: add one-shot FDM validation fix driver | `Tools/test_fdm_validation_fixes.ps1` | V | no | keep (transient) |
| `9a08153` | fix: correct PowerShell validation driver syntax | same driver | V | no | keep (transient) |
| `ff66f18` | test: probe affected FDM suites directly after applying fixes | same driver | V | no | keep (transient) |
| `00ae6e0` | fix: align FDM validators with current readiness contract | ownership scan, 3 validators, `MavWTFeelPolishController.cs` | V + P (diagnostic string only) | **no** | keep |
| `c2ab850` | fix: refresh legacy ownership readiness after mutation | `MavPhysicsOwnershipController.cs` (+10) | P | no (invalidates cached readiness after legacy enable/disable) | keep |
| `d704138` | fix: rely on ownership invalidation API | `MavPhysicsOwnershipController.cs` (−1) | P | no | keep |
| `2b7677e` | test: align ownership validators with Phase5A authority | freeze-hardening + integration validators | V | no | keep |
| `460713a` | chore: remove temporary FDM validation driver | deletes the driver | V | no | keep — **net effect: no driver in the tree** |

Notes on the two production touches:

- `MavWTFeelPolishController.cs` changes only a reporting flag and the `lastApplied` string so a
  skipped aircraft-aware pass is not reported as applied. No tuning number changes: the diff adds
  one bool, resets it in `ApplyPreset`, and guards one string assignment.
- `MavPhysicsOwnershipController` gains `RefreshOwnershipReadinessAfterLegacyMutation()` called
  after `DisableLegacyOwners` and the restore path, which calls `sixDoFBody.NotifyOwnershipChanged()`.
  This makes readiness re-derive after legacy components are mutated instead of trusting a cached
  value — more fail-closed, not less.
- The six workflow/driver commits cancel out: the final tree contains **no** `.github` workflow and
  **no** `Tools/test_fdm_validation_fixes.ps1`. The deleted workflow exists only in history, where
  GitHub does not execute it.

### 2.2 Additional commits on `sol/f16-powered-reference-smoke` (10 commits)

| SHA | Title | Files | Kind | Physics behavior change | Verdict |
|---|---|---|---|---|---|
| `e31b3f0` | feat: add mouse instructor command bridge | `MavMouseInstructorPilotCommandSource.cs` (new, 85) | P | no (intent only, no Rigidbody) | keep |
| `e0e8344` | chore: add mouse instructor bridge meta | `.meta` | P | no | keep |
| `21865b8` | feat: add powered reference shadow smoke opt-in | `MavF16PoweredReferenceShadowSmoke.cs` (new, 334) | P | no while default OFF | keep |
| `ad356af` | chore: add powered shadow smoke meta | `.meta` | P | no | keep |
| `a849c89` | fix: allow governed shadow FDM computation without arming | `MavSixDoFBody.cs` (+26/−5) | P | no live write; enables shadow compute | keep |
| `b851643` | fix: stop per-frame powered shadow rewiring | `MavF16PoweredReferenceShadowSmoke.cs` (+89/−31) | P | no | keep |
| `7ef0c3b` | perf: add lightweight six-DoF editor inspector | `MavSixDoFBodyInspector.cs` (new, editor-only) | V | no | keep |
| `1ed8a03` | perf: throttle shadow readiness scans | `MavSixDoFBody.cs` (+26/−4) | P | no (throttle applies to shadow diagnostics only) | keep |
| `2edd3f1` | fix: neutralize F-16 legacy nose-down trim | `MavF16NeutralPitchTrim.cs` (new, 76) | P | **yes, if it installs** | **REJECTED — reverted in `1bc9dd0`** |
| `8215e1c` | docs: record shadow FDM performance audit | `Docs/Validation/SHADOW_FDM_AUDIT_V1.md` | D | no | keep |

### 2.3 Why `2edd3f1` was rejected

`MavF16NeutralPitchTrimBootstrap.Install` runs once from
`RuntimeInitializeOnLoadMethod(AfterSceneLoad)` and installs the component on every object that
already has a `MavF16SelectionBinding`. That binding is **not serialized in any scene or prefab**
(verified: no `.unity`/`.prefab` references it); it is created at runtime by
`MavF16SelectionAutoSetup`, whose own `AfterSceneLoad` install is not ordered against the trim
bootstrap. Unity does not guarantee ordering between `RuntimeInitializeOnLoadMethod` entries, and
`[DefaultExecutionOrder]` does not apply to them. So:

- in the ordering where the trim bootstrap runs first — the case that must be assumed — the
  component installs on zero objects and the intended neutralization silently never happens;
- in the other ordering it installs and then forces `MavInstructorController.noseDownTrim` to 0
  every 0.5 s, with no opt-in flag, overriding the value `MavAircraftProfileApplier`,
  `MavFreshControlProfile` and `MavWTFeelPolishController` apply (scene ships `0.115`). That gives
  one legacy tuning value two competing owners — the exact pattern the ownership work removes.

The sibling shadow-smoke bootstrap solves the same ordering problem with a 1 Hz rescan, which is
strong evidence the one-shot scan here is an oversight rather than a design choice.

Reverted by `1bc9dd0` on the integration branch, so legacy F-16 pitch trim stays exactly as `main`
has it. If the behavior is wanted it needs a rescanning installer plus an explicit opt-in, decided
as its own change.

## 3. Flight-physics authority — preserved

Checked against the candidate tree, not assumed:

- **One physical load writer.** In the new FDM path the only Rigidbody write is
  `MavSixDoFBody.TryApplyLoadSet` (`rb.AddRelativeForce` / `rb.AddRelativeTorque`,
  `MavSixDoFBody.cs:437-438`). `phase5_writer_scan` and `fdm_ownership_scan_runtime` pass.
- **No duplicated aero force/torque.** Aerodynamic, propulsive and gyroscopic contributions all go
  into the single `debugLoadSet` and leave through that one application.
- **Gyroscopic correction ownership unchanged.** `ApplyGyroscopicCompensation` is untouched; it
  still adds a moment only, so specific force is unaffected. `gyroscopic_moment` passes 28/28.
- **Propulsion ownership unchanged.** No propulsion model is rewired outside the opt-in smoke
  component; `MavF16SelectionAutoSetup` is not modified by the candidate.
- **Handover stays fail-closed.** The shadow path returns *before* `TryApplyLoadSet`, and
  `TryApplyLoadSet` independently re-checks `ReplacementOwnershipGranted()`, so no caller can reach
  the Rigidbody around the gate. `MavF16PoweredReferenceShadowSmoke` hard-sets
  `allowReplacementActivation = false` and refuses to run if ownership is `Fault` or already
  `F16Replacement`. The integration validators were updated in `2b7677e` to assert that the Phase-3
  controller **cannot** grant replacement ownership (state stays `LegacyOwned`/`Unowned`,
  `simulationEnabled` false) — a stricter expectation than before, not a softened one.
- **Reference-data provenance unchanged.** No file under `Docs/Reference`, `Assets/MaverickFresh/Resources`,
  `ProjectSettings` or `Packages` is touched. The three offline Python deck validators still pass,
  including the independent re-derivation (`derived_sha256=f32592f9…`) and their mutation kills.
- **F-15 reference documentation unchanged.** Not in the changed-file set.
- **No weapon / sensor / AI coupling.** The new FDM files reference only instructor, selection
  binding, propulsion and ownership types; no weapon, missile, radar, gun, targeting, damage or AI
  symbol appears in them.

Accepted risk (see §6): `00ae6e0` exempts the whole `FlightDynamics/Validation/` folder from the
ownership scan.

## 4. Unity gate — executed

Unity `6000.3.16f1` was available locally (`D:\unitys\6000.3.16f1\Editor\Unity.exe`), so the gate was
run rather than deferred. Project imported from scratch (no prior `Library/`).

| Check | Result |
|---|---|
| Project import (batch, cold `Library/`) | PASS, exit 0 |
| Compile | **0 `error CS`**, 524 pre-existing warnings; `Assembly-CSharp.dll` + `Assembly-CSharp-Editor.dll` built |
| 22 headless editor validation suites | **PASS — 1247 assertions, 0 failures** |
| 3 play-mode suites (headless) | **PASS — 338 assertions, 0 failures** |
| 3 offline Python deck validators | PASS (8/8 and 5/5 mutation kills) |
| Dangling `m_Script` GUIDs in scenes/prefabs/assets | none — every referenced GUID resolves (the three in `Mav_InGame.unity` are URP package scripts) |
| Repository cleanliness after all Unity runs | unchanged; no `ProjectSettings` drift, pre-existing WIP untouched |
| Recompile after the `2edd3f1` revert | PASS, 0 `error CS` |

Suite detail (passed/failed): f16_propulsion 20/0 · f16_reference 17/0 · f16_tp1538_runtime 137/0 ·
fdm_ownership_scan_runtime 1/0 · fdm_phase1 86/0 · fdm_phase2 192/0 · fdm_phase3 162/0 ·
gyroscopic_moment 28/0 · shared_propulsion 200/0 · fdm_freeze_hardening 11/0 · fdm_integration 40/0 ·
shared_propulsion_unity 74/0 · aircraft_identity_ownership_scan 1/0 · aircraft_identity_validation 60/0 ·
aircraft_scene_wiring_scan 1/0 · aircraft_startup_order 57/0 · aoa_limiter_ownership 16/0 ·
envelope_protection_ownership 34/0 · phase5a_ownership 59/0 · phase5_writer_scan 1/0 ·
turn_dynamics_migration 9/0 · turn_dynamics_phase4b 41/0 · f16_reference_flight 168/0 ·
shared_propulsion_lifecycle 169/0 · fdm_scheduler 1/0.

`fdm_scheduler` trace: `before-law -> after-law:dropout -> after-ownership:legacy ->
after-body:new-disarmed -> legacy-fixedupdate -> end | lawCurrent=True legacyKept=True
newStayedOff=True ownership=LegacyOwned`.

### 4.1 Scene/Play-Mode gates — executed

These five were originally listed as requiring a human. They were instead executed in Unity
`6000.3.16f1` with a temporary reflection-based probe (created for the run and deleted afterwards;
it is not part of this PR). `Time.captureFramerate = 50` made the runs reproducible: two independent
launches of the same tree produced byte-identical trajectories, so the `main` comparison below is
exact rather than approximate.

| # | Gate | Result |
|---|---|---|
| 1 | Missing Script / broken references | **PASS** — 3 scenes + 6 prefabs opened, every component checked, `totalMissing=0` |
| 2 | Player aircraft spawn | **PASS** — `Mav_Player`, `F-16C FIGHTING FALCON (F16C)` applied, `hasAuthoritativeAircraft=True`, Rigidbody present, `f16Selected=True` |
| 3 | Control feel vs `main` | **PASS, bit-identical** — see below |
| 4 | Shadow FDM OFF behavior | **PASS** — `OFF - legacy flight unchanged`, `owner=Legacy`, `simulationEnabled=False`, `debugLoadApplications=0`, `debugShadowComputeSteps=0`, legacy still the live writer |
| 5 | One enabled shadow run | **NOT SATISFIABLE** — see 4.2 |

Gate 3 detail, comparing this branch against `main` checked out in a separate worktree, same probe,
same Unity version: 30 flight-relevant tuning parameters identical (including `jet.noseDownTrim` and
`inst.noseDownTrim` both `0.115`, confirming legacy F-16 trim is untouched), spawn/mass/damping block
identical, and 24 trajectory samples over 600 physics steps (12 s) of position, velocity, euler and
angular velocity **bit-identical, 24/24**. Flight behavior on this branch is not merely similar to
`main`, it is the same to the last bit.

Weapon/combat behavior was not separately exercised; nothing in the diff touches it.

### 4.2 Gate 5 — the powered shadow smoke cannot arm

Enabling `MavF16PoweredReferenceShadowSmoke.enablePoweredReferenceShadowSmoke` produces:

```
REFUSED - TP-1538 propulsion is present but not acceptable for live flight
```

The cause is repository-wide, not environmental. `MavEngineProfile.IsAcceptableForLiveFlight`
requires `thrustDeck.IsAcceptableForLiveFlight && sourceEnvelope.declared`. The deck reports
acceptable, but **no code path anywhere declares a source envelope**: `MavF16PropulsionInstallation`
and `MavF15PropulsionSkeleton` both set `sourceEnvelope = MavEngineSourceEnvelope.Undeclared`,
deliberately, because the power model's validity envelope has not been separately established. So
`tp1538PropulsionAcceptable` cannot become true and the component can never reach `Shadow`.

The refusal is correct and fail-closed: zero live loads, ownership stays `Legacy`, legacy keeps
flying, the status string is accurate, and disabling restores cleanly. What is unproven is the
healthy shadow run itself.

This is a pre-existing property of `main` — `MavF16PropulsionInstallation` is not in this PR's diff.
This PR adds a component whose arming precondition the repository deliberately does not satisfy. It
is therefore inert until a sourced engine validity envelope is declared, which is a sourcing
decision rather than a code fix and is tracked separately. Merging changes no flight behavior, as
gate 3 proves.

The suite runs used the uncommitted `MavFdmValidationBatchAdapter.cs` (§7) as the headless entry
point. It is editor-only and does not change validator semantics, but it is not part of this PR, so
the numbers above were produced with one editor script present that `main` will not have.

The suite runs used the uncommitted `MavFdmValidationBatchAdapter.cs` (§7) as the headless entry
point. It is editor-only and does not change validator semantics, but it is not part of this PR, so
the numbers above were produced with one editor script present that `main` will not have.

## 5. Integration

Chosen shape: **PR, not a direct merge.**

**MERGED — 2026-09-18.**

| | |
|---|---|
| Old `main` | `bb3a5273a8baf43eeb0b46277269d4b130c852d4` |
| Integration source | `sol/mainline-consolidation-2026-09` @ `255978e` |
| Pull request | #11 (`--merge`, no squash, no rebase) |
| Merge commit | `28c174e7ce6504e3353b7aba18c75b66b43bf621` |
| **New authoritative `main`** | **`28c174e7ce6504e3353b7aba18c75b66b43bf621`** |
| Commits integrated | 21 audited + 1 revert + 2 documentation |

`28c174e` is the authoritative base for all subsequent combat-system work. Every later branch —
`sol/weapon-system-separation-r0` and everything after it — is cut from this commit or a descendant,
never from a `sol/*` branch audited here.

- Branch: `sol/mainline-consolidation-2026-09` = `8215e1c` + `1bc9dd0` (revert of `2edd3f1`) + this document.
- PR base: `main`. Merging it integrates all 21 audited commits at once, with the one rejected
  commit reverted in the same change. No cherry-picking, no duplicated commits, no rewritten history.
- PR #10 (`sol/fdm-validation-fixes` → `main`, draft): **superseded by this integration.** Left open
  deliberately, per the consolidation rule that it must not be closed before the decision is
  recorded — which this document does. Do not merge it: its 11 commits are already in this PR.
- No branch deleted. `sol/fdm-validation-fixes` and `sol/f16-powered-reference-smoke` stay as
  history and stay recoverable.

PR #10 was left open through the merge and can now be closed as superseded; its 11 commits are in
`28c174e`. Gate 5's blocker is tracked as issue #12.

## 6. Known risks carried forward

1. **Validation-folder exemption (medium).** `00ae6e0` makes the ownership scan skip every file
   under `FlightDynamics/Validation/`, so a future real Rigidbody writer there will not be flagged.
   It was added because `MavInertiaTorqueApplier` legitimately applies a torque to a bare diagnostic
   body; that component refuses to attach to anything carrying `MavSixDoFBody`,
   `MavFlightPhysicsOwnership` or a legacy writer, so today's exemption hides nothing real. Prefer
   narrowing it to a file+line allowlist, the way `IsAllowedHandoverStateTransfer` does.
2. **Reflection in validators (low).** `2b7677e` reaches `DisableLegacyOwners` and `ReturnToLegacy`
   by reflection. It throws `MissingMethodException` on rename rather than silently passing, but it
   is brittle against refactoring.
3. **Stale shadow readiness (low).** With `1ed8a03`, readiness/legacy-owner discovery refreshes
   every 10 physics steps (~0.2 s at 50 Hz) while shadowing. The shadow structural gate can be that
   stale. Live application is unaffected: non-shadow refreshes every step, and ownership changes
   reset the countdown.
4. **Shadow-smoke bootstrap always installs (low).** A `DontDestroyOnLoad` bootstrap object rescans
   at 1 Hz and attaches the (default-OFF) smoke component to F-16 bindings in every session. Zero
   physics effect, small constant cost.
5. **Residual components after a shadow run (low).** Stopping a run restores wiring but leaves any
   `MavF16PropulsionSystem` / `MavFlightPhysicsOwnership` it added attached and unreferenced.
6. **Unfixed perf findings (informational).** `SHADOW_FDM_AUDIT_V1.md` V1-02/03/04 (physics-rate
   diagnostic string building) are documented but not fixed, and H1–H3 are unconfirmed hypotheses.
   Only the readiness throttle and the lightweight inspector landed.
7. **Deleted workflow in history (informational).** `fcf78bd`/`1ae3ea5` added a PR-triggered
   auto-fix workflow that `f518f3f` deleted. Nothing executes from history, but the commits remain.

## 7. Loose ends outside this consolidation

- **Uncommitted validation-baseline WIP** in the working tree, deliberately left uncommitted:
  `Assets/MaverickFresh/Scripts/FlightDynamics/Editor/MavFdmValidationBatchAdapter.cs` (+ `.meta`),
  `Tools/fdm_validation_baseline_v1.json`, `Docs/Validation/FDM_VALIDATION_BASELINE_V1.md`,
  `Docs/Validation/FDM_VALIDATION_INVENTORY_V1.md`, plus a `Maverick.slnx` edit and deleted
  `Tools/__pycache__/*.pyc`. Its manifest names `authority_commit: 460713a` and lists
  `Tools/run_fdm_validation.ps1`, which lives only on `sol/fdm-runner-fix`. Landing the baseline
  needs those three pieces brought together as their own change.
- `sol/fdm-runner-fix` — 2 unique commits, the 599-line validation runner. REQUIRES_REVIEW.
- `origin/sol/fa18-f22-source-acquisition-r0` — 16 unique docs/data commits. REQUIRES_REVIEW.
- `origin/sol/f15-source-gap-audit` — 1 unique docs commit. REQUIRES_REVIEW.
- `origin/fam-flight-observer-v0.1` / PR #1 — 6 unique commits. REQUIRES_REVIEW.
- Local `sol/f16-powered-reference-smoke` is 1 commit behind its remote; fast-forward it to avoid
  confusion about which tip was audited.

## 8. Verdict

The candidate is **safe to use as the base for `sol/weapon-system-separation-r0` once this PR is
merged**, on this evidence: the physics-authority invariants hold, the whole automated validation
surface passes with zero failures on the exact tree, no reference data or aircraft tuning changed,
no scene/prefab/weapon/sensor/AI file is touched, and the single commit that did change legacy
flight behavior — non-deterministically — has been rejected and reverted.

The scene and Play-Mode gates in §4.1 have now been executed: scenes and prefabs carry no missing
scripts, the player aircraft spawns and applies the F-16C, and flight behavior is bit-identical to
`main` across 30 tuning parameters and 24 trajectory samples. Shadow FDM is inert when off.

One limitation is carried knowingly into `main`: gate 5 could not be satisfied, because no engine
profile in the repository declares a source envelope, so `MavF16PoweredReferenceShadowSmoke` refuses
to arm (§4.2). It refuses fail-closed and changes no flight behavior, and the blocker predates this
PR. The component is therefore inert on `main` until a sourced engine validity envelope is declared.
That must be resolved before any live-handover phase relies on shadow evidence.
