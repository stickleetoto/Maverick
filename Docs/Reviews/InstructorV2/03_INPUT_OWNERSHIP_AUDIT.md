# 03 — Input, Scheduling, Ownership and Fail-Closed Audit

**Files read in full:**
- `Core/MavMouseAimPilotCommandSource.cs` (585)
- `Core/MavMouseAimInput.cs` (98)
- `Core/MavMouseAimCameraRig.cs` (139)
- `Core/MavFlightControlLawBase.cs` (262)
- `F15/MavF15PilotControlledRig.cs` (454)
- `F15/MavF15PilotControlLawV2.cs` (341)

**Files read for the relevant paths:**
- `Core/MavSixDoFBody.cs`: step, readiness, gravity
- `Core/MavFlightPhysicsOwnership.cs`: step verification, grant re-check, gravity provider
- `Core/MavPilotCommandSource.cs`: signal-loss rules
- `F15/MavF15PilotControlledAuthority.cs`: the grant

---

## C. Input and scheduling

| Item | Behavior (code) | Verdict |
|---|---|---|
| Update vs FixedUpdate | `Update` accumulates mouse deltas only (`:270-309`, `:330-359`). `FixedUpdate` → `StepGuidance(Time.fixedDeltaTime)` (`:363-366`). Render dt is never used by physics code | Correct. `[MI11]` (30/50/60/144/jittered fps) and `[RI]` (25/50/100 fps, real Unity) bound the snapshot lag to one render frame. Reproduced byte-identically |
| Repeated command-source polling | `TryGetCommand` returns the cached step result and has no side effects (`:167-172`) | Correct. The rig's switch gate may read it any number of times |
| Step-cache correctness | `cached` and `hasCommand` are set only in `StepGuidance`. They are cleared by `Invalidate`, `OnEnable` and `ResetGuidanceAndAim` | Correct. No path consumes a command from a different step: the source always steps at −350 before the law at −300 in the same step. No stale-command defect found |
| Wrong physics-step state | Guidance reads `Rigidbody.rotation`, `.linearVelocity` and `.position` after the previous simulate; the law reads `body.debugState`, built before that simulate | Consistent; documented as "one step fresher than the law". Not a defect |
| Device disconnection | `MousePresent()` → `NoMouseDevice` → input loss (`:298-299`, `:313-322`) | Correct mapping. Not dynamically tested (no device in batch mode) |
| Lost application focus | `Application.isFocused == false` → `FocusLost` (`:300-301`) | Correct mapping. On refocus, the state stays `CursorReleased` until a click (`:288-294`, `:302-303`), so the aircraft remains on the input-loss policy until the user re-captures. Intended; worth a playtest note |
| Large delta on refocus | `ApplyDelta` has no per-frame magnitude clamp (`MavMouseAimInput.cs:62-72`) | **[H] H4.** Whether `MavFreshInput` emits a spike on refocus or cursor re-lock is untested. A spike would move the aim but is bounded by the guidance's rate limit |
| Free-look | While held, deltas go to the camera offset only; release zeroes the offset (`:344-351`) | Correct. `[MI11]` asserts the aim is bit-identical |
| Cursor release | Esc → unlocked → `CursorReleased` → input loss. Mouse0 re-captures; auto-capture happens only on the first frame (`:281-295`) | Correct for the playtest rig. **LOW (F-14):** Mouse0 is a likely fire button in a production integration |
| Invalid vectors | Non-finite or zero scripted targets and non-finite mouse samples are rejected and counted (`:243-254`, `:351-357`). Non-finite pose or velocity → `Invalidate` (no signal) (`:386-391`). Guidance `InputsUsable` refuses with a reason and returns exactly neutral (`MavMouseAimGuidance.cs:673-714`) | Correct. `[MI6]` / `[MI12]` |
| Input recovery | Default policy: the aim is re-anchored on the flight path every neutral step (`:424-425`), so resume has zero error (except the ±85° clamp, **F-13**: up to 4.5°). `DropCommandSignal`: the aim is **not** re-anchored; guidance state is reset every lost step (`:408-414`), so on restore the guidance resumes toward the pre-loss aim from a zeroed filter | Default correct. Drop: see D.3 |
| Source switching | Rig → MouseAim disables an existing keyboard source (`F15/MavF15PilotControlledRig.cs:433-443`). Scripted ↔ mouse switching (`ClearScriptedTarget`, `:256-261`) and policy changes do **not** reset guidance state | **LOW (F-17)**: the handoff asked for a reset "when … input source changes". The rate limit bounds the effect |
| Camera failure | `requireAimCamera` and a missing, disabled or dead camera → `CameraUnavailable` → input loss (`:398-399`) | Correct; exercised by `[RL]` and by the probes |
| Multiple aircraft | The camera binds `FindFirstObjectByType` (`MavMouseAimCameraRig.cs:144-147`) | **LOW (F-15)** |

**Stale or wrong-step consumption.** None found. The only time-skew effect is the documented sampling of input up to the previous rendered frame. The worst case is bounded by one frame at ≥ 25 fps; < 25 fps is untested (**H6**).

---

## D. Fail-closed behavior, policy by policy

All four policies were exercised dynamically by the PR (3–8 s) and by this audit (up to 120 s).

### D.1 `NeutralStickHoldThrottle` (default) — **acceptable airborne fail-closed policy** [V-dyn]

- **Mechanics.** The source keeps a signal and commands neutral pitch/roll/yaw, ramped by the rate limit. Throttle is passed through (ignored by the fixed-thrust rig). The aim is re-anchored on the flight path (`:417-425`). Readiness holds, so **all loads continue: gravity, aero, thrust**.
- **Gravity ownership.** Unchanged and correct: environment gravity in the load set, `useGravity` off, asserted every step by the ownership authority.
- **Long duration.** INPUTLOSS_NEUTRAL_120S (camera lost at 8 s in a 49° bank) is stable for 112 s:
  - bank decays to 0;
  - phugoid lightly damped (≈ 49 s period);
  - Δh −727 m;
  - no aero refusal.
- **Recovery.** Zero-error resume (aim re-anchored).
- **Caveat.** "Neutral" under V2 means **zero rate demand** (attitude-holding drift), not wings-level. A loss during a steep dive continues the dive (DIVE probes show where that leads, F-01). Verified at one trim only.

### D.2 `HoldAimTarget` (opt-in) — **acceptable but semantically risky** [V-static]

The aircraft keeps steering toward a frozen aim with no human able to correct it. If the frozen aim is a steep dive, this reproduces F-01 and F-02 with no input able to stop it. `[MI12]` tests one step only. Recommended: do not ship this as a loss policy; at minimum, time-limit it.

### D.3 `DropCommandSignal` (opt-in) — **not acceptable for an airborne live rig (F-05)** [V-dyn]

**Mechanics.** `Invalidate` → `HasCommandSignal` false (`:408-414`, `:465-473`) → the law resolves `SignalLossPolicy` → `NeutralCommand` (never the inspector value; `Core/MavFlightControlLawBase.cs:240-254`). That part is correct.

Then the readiness snapshot's `observedSourceSignalThisStep` goes false (`Core/MavSixDoFBody.cs:966-969`) → `IsLoadApplicationPermitted` false (`:1080-1110`) → the body returns before computing anything (`:294-299`). **No** aero, thrust, **gravity** or gyroscopic load is applied.

**Gravity ownership.** The ownership authority still declares `ReplacementLoadSet` as gravity provider and keeps `Rigidbody.useGravity` off (`Core/MavFlightPhysicsOwnership.cs:415-420`). So during the drop **nobody** applies gravity. This is the existing, intentional arming rule; applied to an airborne aircraft, it removes gravity.

**Physical integration.** PhysX integrates zero force and zero torque with zero damping. Velocity and body rates are frozen exactly (DROP10: p 11.98, q 3.661, r 8.000 °/s, TAS 90.864 m/s, path −3.301° for 10 s) while the attitude keeps rotating:

| Loss | α | \|β\| | Bank |
|---|---|---|---|
| 3 s (PR run) | 33.6° | — | 77° |
| 10 s | 53.5° | 18° | 175° |
| 15 s | 53.5° | **33.4°** (beyond the ±20° transcribed span) | through ±180° |

**Readiness / ownership.** The owner stays `F15PilotControlledResearch` and the body stays armed. Nothing faults: the grant does not check the signal, which is correct for the grant's purpose.

**Recovery after restore:**

| Loss | Outcome |
|---|---|
| 3 s (DROP3) | Loads resume; recovery dive to −27°; Nz 1.8; Δh −432 m |
| 10 s (DROP10) | Restored inverted (bank 175°, α 53°); \|ω\| up to 45.6 °/s; dive to −34.9°; Nz 2.0; Δh −631 m; recovered by about 17 s after restore |
| 15 s (DROP15) | Restored at β 33°; **aero refused for 99 steps (2 s), n_z 0**; then recovered; dive −32.8°; Δh −806 m |

A longer loss (a user leaving the window) can restore anywhere in attitude space. Recovery is not guaranteed.

**Assessment.** As an *arming* refusal, withholding loads is correct and must stay. As an airborne runtime signal-loss policy, it is a non-physical state: a gravity-free, drag-free coast with free rotation. Afterwards the aircraft is handed back to the physics at an arbitrary, possibly out-of-domain state.

The PR's `[RL]` check scores the coast as PASS; `[MI13]` tests a 5-step restore only.

**Fix direction (no guard weakened):**
- remove `DropCommandSignal` from the live rig's selectable policies, or make the rig refuse it while airborne;
- if a "freeze" is really wanted, model it as an explicit, declared pause owned outside the FDM (simulation stopped), never as "physics running, loads withheld".

### D.4 Hands-off (X) — **acceptable** [V-dyn]

This is the same code path as D.1 (`neutralStick = handsOff || neutralOnLoss || !boundLawIsRateCommand`, `:418`). HANDSOFF_IN_TURN_120S is bit-identical to INPUTLOSS_NEUTRAL_120S.

The command reaches neutral 0.64 s after X (PR `[RT]`). The guidance's integral is zeroed by hands-off (dead by default anyway). The release-to-neutral ramp is rate-limited, with no step.

### D.5 Bound law not rate-command (V1) — **acceptable** [V-static]

Neutral stick only, flagged (`:418`, `:445-447`); `[MI13]`. Switching laws requires a centred stick: press X first.

### D.6 Source disabled — **acceptable** [V-static]

`HasCommandSignal` → false (`isActiveAndEnabled`). The law resolves `NoSource` and uses its inspector bench command (`Core/MavFlightControlLawBase.cs:199-200`, `256-258`), but readiness then fails and withholds loads. A disabled source on an airborne rig therefore produces the same gravity-free coast as D.3.

This is pre-existing architecture and was not introduced by PR #35. It is noted because the PR's report table (§3.7, last row) describes it as safe ("live-readiness withholds loads").

## E. Ownership summary

| Check | Result |
|---|---|
| Instructor path writes no Rigidbody or Transform state | Holds (code, `[MI14]`, `[RM]`, probes) |
| One law enabled; grant re-checked every step | Holds |
| One load application per step; no duplicates | Holds (probes: `dup 0` in every run) |
| Legacy writers cannot coexist | Unchanged grant (`AllLegacyWritersQuiet` at entry; not altered) |
| Gravity: exactly one provider | Holds while armed and ready. During the opt-in `DropCommandSignal` (or a disabled source) the count is **zero** (D.3, D.6) |
| Live takeover by default | No: MouseAim is opt-in; defaults unchanged |
