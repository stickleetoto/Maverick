# Instructor v2 — Independent Engineering Audit (r1)

| | |
|---|---|
| **Auditor** | Claude A, independent flight-control code auditor. Not the implementer, not an advocate |
| **Target** | PR #35, `claude/realistic-mouse-instructor-v2` |
| **Audited commit** | `ad348086e09f7761e5e3e3ecef7cb45f97e8cff0`. Verified as the PR head and as `origin/claude/realistic-mouse-instructor-v2` at audit start |
| **Stable main** | `71820a97b8d58c69229799fb00868bce4ded87d1`. Verified as `origin/main` at audit start; this branch starts there |
| **Research context read (not modified)** | PR #34 @ `35b3183`, PR #36 @ `7a1b7f7` |
| **Audit branch** | `claude/instructor-v2-independent-audit-r1`. Documents only, under `Docs/Reviews/InstructorV2/` |
| **Unity** | 6000.3.16f1, batch mode, Windows 11 |

No PR branch, research branch, production file, scene, prefab, asset, profile, source dataset, existing test, original working tree or stash was modified. All dynamic runs used throwaway detached worktrees of `ad34808`. Audit probe code was compiled only there; it is kept here as `.cs.txt` evidence.

## Documents

| File | Content |
|---|---|
| `01_ARCHITECTURE_AUDIT.md` | Pipeline, ownership, scheduling, the two meanings of "fail closed", domain-exit architecture |
| `02_PHYSICS_AND_CONTROL_AUDIT.md` | Geometry and signs, gravity compensation, plant assumptions, envelope, measured damping, saturation, unreachable targets |
| `03_INPUT_OWNERSHIP_AUDIT.md` | Update/FixedUpdate, polling, cache, focus/cursor/device/camera, recovery, each loss policy separately |
| `04_VALIDATION_AUDIT.md` | Re-execution results, what every check actually asserts, threshold history, independent probes, coverage gaps |
| `05_FINDINGS_AND_SEVERITY.md` | 19 findings with file:line, reproduction, evidence, expected/actual, root cause, fix, test; 7 hypotheses |
| `06_RECOMMENDED_FIX_PLAN.md` | P0–P3 ordered fixes, each with its regression test |
| `evidence/` | Reproduced PR reports, probe summaries, per-run CSVs (0.1 s), pure-function probe output, probe source, analysis scripts |

## Files and lines inspected

**Read in full** (the complete PR diff, 23 files, +4470/−2):
- `Core/MavMouseAimGuidance.cs` (778)
- `Core/MavMouseAimPilotCommandSource.cs` (585)
- `Core/MavMouseAimInput.cs` (98)
- `Core/MavMouseAimCameraRig.cs` (139)
- `F15/MavF15PilotControlledRig.cs` (454, incl. the +44/−2 diff)
- `F15/MavF15PilotControlLawV2.cs` (341)
- `Core/MavFlightControlLawBase.cs` (262)
- `Validation/MavMouseAimInstructorV2Validation.cs` (1017)
- `Validation/MavMouseAimInstructorV2FlightValidationRunner.cs` (667)
- `Editor/MavMouseAimInstructorV2FlightValidation.cs` (198)
- `Docs/FlightDynamics/REALISTIC_MOUSE_INSTRUCTOR_V2_REPORT.md`
- `Docs/FlightDynamics/REALISTIC_MOUSE_INSTRUCTOR_V2_CLAUDE_HANDOFF.md`
- both committed reports

**Read for the relevant paths:**
- `Core/MavSixDoFBody.cs`: step 239-385, readiness snapshot 940-1012, gravity 694-760, load permission 1080-1110
- `Core/MavFlightPhysicsOwnership.cs`: step 359-372, gravity provider 398-430, pilot-controlled verification and entry 966-1080
- `Core/MavPilotCommandSource.cs`: 100-155
- `F15/MavF15PilotControlledAuthority.cs`: 60-200
- `F15/MavF15PilotControlledAeroModel.cs`: 15-60, 171-175
- `F15/MavF15BaumannMach06Domain.cs`: 40-100
- `F15/MavF15PilotControlledDiagnostics.cs`: domain lines
- `Editor/MavMouseAimInstructorV2Setup.cs`
- PR #36: `06_GUIDANCE_VALIDATION_PLAN.md` and `05_CONTROL_STABILITY_AND_IDENTIFICATION.md`, read; the rest of #34/#36 by listing

## Tests actually executed

| What | Result |
|---|---|
| PR headless suite `MavMouseAimInstructorV2Validation` (Unity, via `MavFdmValidationBatchAdapter`) | **61 / 0**, as reported |
| PR Play Mode suite `MavMouseAimInstructorV2FlightValidationRunner` (Unity) | **29 / 0**, report **byte-identical** to the committed one (newline-normalized) |
| Official Baseline v1 `Tools/run_fdm_validation.ps1`, clean detached worktree at `ad34808` | **PASS 1585 / 0** over 25 counted suites (28 suites, all PASS), physics delta NONE, run under PowerShell 7. A first attempt under Windows PowerShell 5.1 ran all 28 suites to PASS with the same 1585/0, but the runner reported `INFRA_FAILURE`: 5.1 turned a git LF→CRLF stderr warning into a terminating error. That was an environment effect of the invocation, not of the PR |
| Audit probes, Play Mode through the PR's own MouseAim rig | 24 runs, 63,000 physics steps, two batches (`evidence/probe_batch1`, `evidence/probe_batch2`) |
| Audit pure-function probe of the production `MavMouseAimGuidance.Step` | bank-rule grid, deferral continuity, re-anchor clamp, reversal sweep, hands-off output |

**Not executed (accepted as reported, not independently proven):**
- the frozen F-15 suites (757/0);
- pilot V1/V2 headless (49/0, 54/0) and Play Mode (31/0, 57/0);
- the research closeout (39/0).

No human flight was performed.

## Verified defects (severity-ranked)

| ID | Sev | One line |
|---|---|---|
| F-01 | **BLOCKER** | No upper-airspeed cap. A held −85° aim (mouse-reachable) crosses the 213.3 m/s research ceiling at 30.9 s; aero **and** thrust then refuse for good; TAS 780 m/s, tumbling, −32 km. Unrecoverable, and the mouse overlay still says live-ready |
| F-02 | HIGH | Held −30/−60° dive aims: roll command saturated 44 % / 96 % of the time; bank cycles through inverted; 3–4 g; never converges. V2 cannot unload at speed with ≤ 0.6 push |
| F-03 | HIGH | Bank rule singular near a zero-g request. 8° down + 0.25–2° lateral asks −6…−42° bank; ≥ 10–12° down asks ±90° for any lateral offset. Dynamically a growing roll limit cycle (±65–69° with 2° lateral) |
| F-04 | HIGH | Outer loop ζ ≈ 0.07–0.08 after 5°/15° turns, 0.03 after a reversal (doc says ≈ 0.45). K = 0.3 removes it **and** converges the 15° turn to 0.0°, refuting the report's "residual = energy deficit, needs thrust" explanation |
| F-05 | HIGH | `DropCommandSignal` (opt-in): all loads incl. gravity withheld while PhysX integrates, giving a constant-velocity, constant-rate tumble (α 53°, \|β\| 33° in 15 s). Restore can be outside the aero domain (99 refused steps). The PR scores it PASS |
| F-06 | MEDIUM | Unreachable climbs (5°, 8°, 40°, 80°) settle at the low-speed cap in a −13.7…−14.5° glide, worse than hands-off (−2.9°) |
| F-07 | MEDIUM | Moving-target check labelled "not growing" does not test growth; the lag grows 4 → 8° in the PR window and to ≈ 30° by 120 s |
| F-08 | MEDIUM | Tracking checks assert halving/approach/sign only; climb/descent rescored after failing; the energy check cannot detect a source |
| F-09 | MEDIUM | One trim, ≤ 60 s, no dives/high speed/damping/long-loss restore. Every HIGH finding lives in these gaps |
| F-10 | MEDIUM | Report and labels contradict code and evidence: integral "adopted" but gain 0; damping 0.45; residual cause; `[MI4]` "pull rather than push" asserts a push |
| F-11 … F-17 | LOW | Overlay lacks a refusal warning; neutral stick = attitude drift (stable, spiral refuted); ±85° re-anchor residual up to 4.5°; Mouse0 capture; first-found camera binding; no runtime config validation; no reset on source switch |
| F-18, F-19 | IMPROVEMENT | Air-relative state; expiring turn memory |

**Verified correct** (no defect found):
- ownership and single-writer integrity;
- no instructor write to pose, velocity, force or moment;
- axis, sign and unit conventions;
- the level-turn identity;
- idempotent polling and step cache;
- frame-rate-independent input accumulation;
- free-look isolation;
- invalid-vector rejection;
- the default `NeutralStickHoldThrottle` policy, stable for 112 s at this trim;
- 180° hysteresis;
- low-speed and α soft caps;
- the unreachable-zenith response (stable glide, no departure).

## Hypotheses requiring further tests

H1–H7 are listed in `05_FINDINGS_AND_SEVERITY.md`:
- behavior at other trims;
- human-in-the-loop PIO;
- power balance of the descent energy gain;
- refocus mouse spikes;
- dt sensitivity;
- < 25 fps rendering;
- `HoldAimTarget` with a frozen dive aim.

## Recommended fixes

See `06_RECOMMENDED_FIX_PLAN.md`.

| Stage | Fixes |
|---|---|
| **P0** | Upper-speed soft cap with refusal warning; remove `DropCommandSignal` (and an unbounded `HoldAimTarget`) from live airborne rigs |
| **P1** | Regularize the bank direction near a zero-g request; identify the inner loop and retune or damp the outer loop (K = 0.3 is demonstrated); limit commanded dive depression; correct the documents |
| **P2** | Validation that can fail for the right reasons (PR #36 doc 06 metrics and scenarios), energy-aware climb intent, LOW items |

No fix requires weakening an ownership, readiness or grant guard.

## Verdicts

### 1. Ready for isolated human playtest? — **NO (not yet)**

The default path is ownership-safe, and nothing here endangers a person. But ordinary mouse inputs reach verified invalid behavior within about 30 s:
- aiming down: domain exit with an unrecoverable ballistic fall (F-01);
- continuous inverted roll cycling (F-02);
- a descending, slightly offset aim: roll limit cycle (F-03);
- every turn: lightly damped bank rocking (F-04).

A playtest now would mostly measure these artefacts, not the instructor's intended feel, and its conclusions would be invalid.

**Minimum to proceed:** P0.1 + P0.3 (or, at the very least, a briefed tester with the stop rules of `06` §P3 and no aim below about −20°), P1.1 and P1.2, with their regression tests passing.

### 2. Ready for production integration? — **NO**

Production integration needs, in addition to the above:
- F-01 to F-05 fixed;
- validation that measures convergence, damping and long-duration behavior at more than one trim (F-07 to F-09);
- corrected documentation (F-10);
- the human acceptance the handoff requires.

### 3. Ready to merge into frozen main? — **NO**

`main` is frozen pending the user's own flight-test acceptance. Independently of that, the PR contains a BLOCKER-class finding and four HIGH findings. The PR remains correctly a **draft**.
