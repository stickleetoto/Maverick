# Realistic Mouse Instructor v2 — Implementation and Validation Report

**Branch:** `claude/realistic-mouse-instructor-v2` (from `5776617`, the handoff commit on top of the frozen `main` `71820a9`)
**Unity:** 6000.3.16f1
**Status:** EXPERIMENTAL, opt-in, **not flown by a human yet**. Draft PR only; nothing is merged. The frozen baseline stays authoritative.

> This is **not** War Thunder's instructor and **not** an F-15 / F-16 production FCS. Every guidance number is
> **MAVERICK_TUNED_NON_AUTHORITATIVE**. The aircraft is the frozen AFIT / Baumann / Davison F-15 **research** model with
> fixed research thrust (**THROTTLE INACTIVE**) and the unchanged Maverick pilot-control **V2** law.

---

## 1. Audit (handoff §1)

**Reused unchanged**

- `MavPilotCommandSourceBase` / `MavPilotCommand`: the socket. Its two readiness questions (`IsOperationalCommandSource`, `HasCommandSignal`) and the declared signal-loss policy are used as designed.
- `MavF15PilotControlLawV2`: the rate / sideslip law, **unchanged**. It already solves the inner loops a mouse instructor needs: stick → pitch rate, stick → roll rate about the velocity vector, pedal → sideslip, an aileron-rudder interconnect, and dynamic-pressure scheduling. So the instructor only needs to produce **rate intents**.
- `MavF15ControlActuator`, `MavF15PilotControlledAeroModel`, `MavF15PilotControlledFixedThrust`, `MavSixDoFBody`, `MavFlightPhysicsOwnership`: unchanged; one actual-surface owner, one aero model, one thrust, one load application.
- `MavF15PilotControlledRig`: the same rig a human and every validation fly.
- `MavFreshInput` for the mouse and keys.

**What V2 does not solve, and the instructor therefore adds**

- V2 is a pitch-**rate** command law with no attitude hold. A flight path other than its trim path needs sustained stick. The guidance supplies that stick through a slow, anti-windup pitch integral (§3.4).

**Ownership / readiness checks**

- `MavF15PilotControlledOwnershipGrant` constrains the law (V1 or V2, exactly one enabled), the actuator travel provenance, the aero model, the thrust, the profile and the environment. It does **not** constrain the command-source type.
- Live-readiness requires the body's and the law's source to be the **same object**, declared operational and producing a signal this step. The new source satisfies these truthfully.
- **No guard was weakened. No grant was expanded.**

**Legacy reference only**

- `MavMouseInstructorPilotCommandSource` bridges the legacy `MavInstructorController`. That controller is an arcade controller with its own speed bands, pitch / roll boosts, envelope bypass factors and 48 °/s pitch-rate targets. Nothing of it is reused.

---

## 2. Pipeline and ownership

```text
mouse deltas (Update, MavFreshInput)  ->  persistent WORLD aim direction (az / el)          MavMouseAimPilotCommandSource
   free-look (hold C) moves only the camera offset
physics step, order -350:  aim + Rigidbody pose / flight path (READ)  ->  MavMouseAimGuidance.Step (pure)
   -> bounded, smoothed, rate-limited MavPilotCommand (pitch / roll / yaw; throttle passed through)
order -300  MavF15PilotControlLawV2 (UNCHANGED)       -> requested F-15 surfaces
order -200  MavF15ControlActuator (sole surface owner) -> actual surfaces
order -100  research aero + fixed research thrust -> MavFlightDynamicsLoadSet -> MavSixDoFBody -> Rigidbody (sole load application)
render only: MavMouseAimCameraRig (moves its own camera; draws aim / flight-path / nose markers + telemetry)
```

- The new code never calls `AddForce` / `AddTorque` / `MoveRotation`, never writes a Rigidbody velocity, position or rotation, and never writes the aircraft Transform.
- It adds no lift, side force, damping, energy, speed hold or pose snapping.
- `[MI14]`, the repository ownership scan and the Play Mode "no pose write" check all verify this.
- The source runs after the ownership authority (-400) and before the law (-300), so the law reads this step's command.
- The law → actuator → body order and its one-step state latency are untouched.
- The guidance reads the Rigidbody pose of the last completed physics step. That is an outer-loop sample, one step fresher than the law's published state.

---

## 3. Algorithm and axis choices

### 3.1 Target semantics

- The aim is a **desired flight-path direction** (the velocity vector), not a nose direction.
- At the research trim, the nose sits 17.5° above the velocity vector. Nose semantics would turn "aim at the horizon" into a push.
- `MavAimReference.Nose` exists and is tested, but it is not the default. Below 5 m/s the guidance falls back to the nose, and flags it.
- The aim, the flight path and the nose are three different markers on screen:
  - `[ ]` the aim;
  - `-o-` the flight path;
  - `+` the nose.

### 3.2 Axes

- Everything is in Unity **world** vectors, taken from the pose:
  - nose = `rotation * +Z` (aero +X);
  - right wing = `rotation * +X` (aero +Y);
  - up = `rotation * +Y` (aero −Z).
- Output signs are `MavPilotCommand`'s: pitch +1 nose up, roll +1 roll right, yaw +1 nose right.
- The guidance is radians internally and degrees in telemetry.
- `[MI2]` checks the signs at headings 0 / 90 / 180 / 270 / 37. `[MI13]` checks the resulting aileron sign through the real V2 rig.

### 3.3 Bank-to-turn with gravity compensation

```
r     = flight path (or nose);  th = angle(r, aim);  d = unit part of the aim perpendicular to r
w_des = min(K th, w_cap)                      K = 0.6 /s; w_cap = the law's full-stick pitch rate (7 deg/s) x pull ceiling
f_req = V w_des d - g_perp                    specific force wanted perpendicular to r
l, s  = aircraft up / right wing made perpendicular to r
bank error = atan2(f_req . s, f_req . l)      roll command = bank error / 1.0 s / (law full-stick roll rate 15 deg/s)
pitch      = (w_des (d . l) + I) / (law full-stick pitch rate)
yaw        = fine-aim pedal only, faded out above 5 deg of error, |yaw| <= 0.15
```

What follows from these equations (nothing below is scripted as a special case):

- Aim straight ahead gives `f_req = −g_perp`, so the wings level.
- Small errors bank only a little.
- A horizontal turn banks to `atan(V ω / g)` and pulls exactly the level-turn pitch rate `ω sin(bank)` (`[MI9]`).
- A large nose-down error rolls inverted and pulls.
- Inverted with the aim ahead, the aircraft rolls upright.
- A target exactly behind starts a deterministic turn on the side the aircraft is already banked toward (right when level), and keeps that side through a ±10° hysteresis band (`[MI5]`).

### 3.4 Pitch integral (added on Play Mode evidence)

- **Evidence.** The first Play Mode run, with a proportional outer loop only, left a standing ~3° error after turns. It also never captured an 8° descent.
- **Cause.** V2 is a pitch-rate command law with no attitude hold. So a flight path that is not V2's trim path needs sustained stick, and a proportional loop only provides that with a standing error.
- **Fix.** A slow integral of the pitch-plane error: `Ki = 0.15 /s²`, so the PI zero sits at 0.25 rad/s.
- **Where it acts.** Only within 12° of the target. Beyond that it leaks away within about 1 s.
- **Anti-windup.**
  - It is frozen while the pitch command is saturated or soft-capped in the error's direction.
  - It is reset by hands-off.
  - It is limited to 0.4 stick.
- **Push limit.** `maxPushStick` went from 0.35 to 0.6 for the same evidence: 0.35 could not capture the descent.

### 3.5 Bounds, smoothing, soft caps

- **Command filter.** First-order, τ = 0.08 s. A rate limit of 4 stick units per second per channel, with fixed stick limits.
- **Dt.** The filter uses the fixed physics dt. Render dt is never used.
- **Envelope soft caps.** These are command ceilings, each labelled with the model limit it protects. None of them is an aircraft limit, and none creates a force:

| Cap | Range | Protects |
|---|---|---|
| Low-speed pull cap | Full pull ≥ 82 m/s. At 72 m/s the ceiling is −0.1 stick (gentle nose-down). | The research TAS floor, 218.5 ft/s = 66.60 m/s, below which the research aero and thrust refuse |
| High-alpha pull fade | 24 → 28° | Flight far outside the exercised α of 15.5–20.2° |
| Low-alpha push fade | 4 → 0° | The −4° transcribed α floor |

### 3.6 The mouse

- **Accumulation.** Delta-accumulated world azimuth / elevation, 1.2° per MavFreshInput mouse unit, elevation clamped to ±85°.
  - Additive, so the aim does not depend on how motion was split across frames.
  - A zenith target is reachable by the guidance, but not by the mouse.
- **No screen-space ray.** The mouse does not set a ray from the camera. The camera looks along the aim.
- **Free-look.** Hold C. The mouse then moves the camera offset only, and the aim is bit-identical (`[MI11]`).
- **Rejected input.** Non-finite samples and targets are rejected and counted.
- **Timing.**
  - `Update` only accumulates.
  - `FixedUpdate` snapshots.
  - `TryGetCommand` returns the cached step result and has no side effects.

### 3.7 Signal and input-loss policy

| Situation | Signal | Command |
|---|---|---|
| Guidance valid | yes | the guidance command |
| No aircraft bound, non-finite pose / flight path | **no** | law applies the declared `NeutralCommand` policy; live-readiness withholds loads (existing rule) |
| Focus lost, cursor released (Esc), no mouse device, aim camera missing — `NeutralStickHoldThrottle` (**default**) | yes (a deliberate command) | ramps to neutral stick, throttle held, aim re-anchored on the flight path (no jump on resume) |
| same — `HoldAimTarget` (opt-in) | yes | keeps steering toward the frozen aim |
| same — `DropCommandSignal` (opt-in) | **no** | as the "no signal" row: loads withheld, body coasts |
| Hands-off (X) | yes | neutral stick, aim follows the flight path |
| Bound law is V1 (not a rate-command law) | yes | neutral stick only, flagged |
| Source disabled | no | law has no source → bench path, but live-readiness withholds loads |

The inspector `pilotCommand` of the law never reaches the actuator while the source is wired. `[MI13]` sets it to 0.9 / −0.9 / 0.9 during a signal drop: the law flies the declared neutral policy, and all loads are withheld.

---

## 4. Files

**Added**

| File | Role |
|---|---|
| `Assets/MaverickFresh/Scripts/FlightDynamics/Core/MavMouseAimGuidance.cs` | Pure deterministic guidance, config with per-value provenance, state, input / output |
| `Assets/MaverickFresh/Scripts/FlightDynamics/Core/MavMouseAimInput.cs` | Pure aim accumulator, free-look offset, direction ↔ angles |
| `Assets/MaverickFresh/Scripts/FlightDynamics/Core/MavMouseAimPilotCommandSource.cs` | The opt-in operational command source (Update sampling, FixedUpdate guidance, policies, telemetry) |
| `Assets/MaverickFresh/Scripts/FlightDynamics/Core/MavMouseAimCameraRig.cs` | Render-only chase camera, markers, telemetry overlay |
| `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavMouseAimInstructorV2Validation.cs` | Headless suite `[MI1]`–`[MI14]` |
| `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavMouseAimInstructorV2FlightValidationRunner.cs` | Play Mode flight test runner |
| `Assets/MaverickFresh/Scripts/FlightDynamics/Editor/MavMouseAimInstructorV2FlightValidation.cs` | Play Mode driver + headless menu item |
| `Assets/MaverickFresh/Scripts/FlightDynamics/Editor/MavMouseAimInstructorV2Setup.cs` | Menu: place the playtest rig in the open scene (writes no asset) |
| `Docs/FlightDynamics/REALISTIC_MOUSE_INSTRUCTOR_V2_REPORT.md` | This report |
| `Docs/FlightDynamics/Data/mouse_instructor_v2/mi2_headless_report.txt`, `mi2_playmode_report.txt` | Unity validation records |

Each added `.cs` has a `.meta`.

**Modified**

- `F15/MavF15PilotControlledRig.cs`:
  - additive `MavF15PilotCommandSourceKind.MouseAim = 2` (Keyboard = 0 stays the default);
  - resolves the mouse source and disables a keyboard source on the same object;
  - tells the source which law is bound (V2 full-stick rates, read only), or that V1 is bound;
  - resets the source on trim start.
- `Tools/fdm_validation_baseline_v1.json` and `Docs/Validation/FDM_VALIDATION_INVENTORY_V1.md`:
  - 4 new uncounted `EXCLUDED_WITH_REASON` surfaces;
  - count 76 → 80;
  - counted set and totals unchanged.

**Not touched**

- Scenes, prefabs, materials, models.
- Weapons, sensors, AI, damage.
- Aero coefficients, atmosphere, mass / inertia, thrust, gravity, propulsive loads.
- The F-15 identity / research baseline.
- The V1 and V2 laws and gains, the keyboard source, the HUD, the legacy mouse stack, F-16 code.

---

## 5. Validation (Unity 6000.3.16f1, batch mode, this machine)

Committed records:
- `Docs/FlightDynamics/Data/mouse_instructor_v2/mi2_headless_report.txt`
- `Docs/FlightDynamics/Data/mouse_instructor_v2/mi2_playmode_report.txt`

### 5.1 New suites

| Suite | Result |
|---|---|
| Headless `MavMouseAimInstructorV2Validation` (`[MI1]`–`[MI14]`, the production guidance / input / source / rig code) | **61 / 0** |
| Play Mode `MavMouseAimInstructorV2FlightValidationRunner` (15 runs, real PhysX, dt 0.02) | **29 / 0**, two separate Unity launches byte-identical |

The handoff's acceptance list, and where each item is checked:

| # | Requirement | Evidence |
|---|---|---|
| 1 | Straight ahead, small left / right / up / down, behind, vertical, inverted / rolled | `[MI2]`–`[MI5]` |
| 2 | Zero / NaN / Inf target, bad dt, missing aircraft / camera / source; no non-finite command, no inspector fallback | `[MI6]`, `[MI12]`, `[MI13]` (inspector bench command 0.9 / −0.9 / 0.9 never reaches the actuator) |
| 3 | Mouse timing vs fixed step | `[MI11]`: 30 / 50 / 60 / 144 / jittered fps. `[RI]`: real Unity, 25 / 50 / 100 fps. In both, snapshots lag by at most one rendered frame of motion; the final aim agrees to 2E-6° in Unity; trajectories differ ≤ 0.64 m over 16 s |
| 4 | 180° reversal: only bounded commands | `[MI7]`: worst step 0.08 = rate × dt; the output type has no pose / velocity field. `[RM]` REVERSAL: no pose write, max \|Δv\| 0.26 m/s per step |
| 5 | Banked turn, not flat yaw | `[MI8]`: zero pedal beyond 5° of error. Play Mode: \|yaw\| ≤ 0.115, turns bank 43–65° |
| 6 | High demand, low authority | LOW-SPEED CLIMB 40: error grows 40 → 51.6°; the energy height falls 662 m; the low-speed cap is visible from 3.9 s; the TAS minimum stays 73.8 m/s, above the 66.60 m/s floor; no aero refusal. Nothing forces convergence |
| 7 | One law writer, one load owner, no duplicate; legacy cannot coexist | `[MI13]`; `[RM]` every run: one law, one armed body, loads = steps, 0 duplicates; the unchanged grant still refuses two laws; the ownership scan is clean |
| 8 | V1 / V2 defaults unchanged with the instructor off | `[MI13]` opt-in OFF, plus the regression diff below |
| 9 | Existing suites green | §5.2 |
| 10 | Play Mode with a moving target | MOVING TARGET 2°/s: steady lag 5.8–7.8° over the last 10 s, at a bank of 16–18° |

Flight record (reported, not scored; from the trim at 91.7 m/s, α 17.5°, path −2.9°):

| Run | Aim error start → end (min) | Bank | TAS (m/s) | Notes |
|---|---|---|---|---|
| Neutral aim hold, 20 s | 0 → 0.000 | 0 | 91.7 | Holds the trim path: a fixed-thrust 2.9° descent |
| 15° R / L, 25 s | 15 → 3.2 (2.7) | +43 / −43 | 86–91.7 | Mirror-identical. The residual ≈ the energy deficit vs the trim path |
| 90° R, 40 s | 89.8 → 12.0 (11.3) | 51 | 78–91.7 | Low-speed cap engaged: the turn is energy limited |
| 180°, 60 s | 174 → 3.4 (0.6) | −43 … 65 | 89–121 | Deterministic right turn; trades height for speed (−974 m) |
| Climb 8°, 20 s | 8 → 17.5 (5.3) | 0 | 74–92 | Path rises 2.7°, then the low-speed cap lowers the nose: **a sustained climb is not available at fixed thrust** |
| Descent 8°, 20 s | 8 → 11.1 (3.6) | 0 | 92–109 | Path drops 4.4° toward the target. **Not held** — see §6 |
| Moving 2°/s, 40 s | lag 5.8–7.8 | 16–24 | 81–92 | Bounded, stable |
| Release to neutral | — | 49 at release | — | Neutral command 0.64 s after X, rate-limited ramp |
| Input loss, default policy | — | — | — | Neutral in 0.60 s; 400 / 400 loads |
| Input loss, DropCommandSignal | — | — | — | Loads frozen at 150; law on its declared NeutralCommand; the body coasts. That is why this is not the default |

### 5.2 Regression (frozen and existing suites)

Both checkouts were run with the same commands:
- this branch, at the feature commit;
- a clean worktree of the base `5776617`, i.e. frozen `main` code.

The check lines were then compared.

| Gate | Base `5776617` | This branch | Difference |
|---|---|---|---|
| Frozen F-15 suites (17, `F15_USER_VALIDATION_PLAN_V1.0.md` §1) | 757 / 0 | **757 / 0** | Only scanned-file counts in 9 scan lines (+8 FlightDynamics files, +6 non-Validation files). No numeric output changed |
| Pilot V1 headless `MavF15PilotControlValidation` | 49 / 0 | **49 / 0** | none |
| Pilot V2 headless `MavF15PilotControlV2Validation` | 54 / 0 | **54 / 0** | none |
| Pilot V1 Play Mode | 31 / 0 | **31 / 0** | report byte-identical |
| Pilot V2 Play Mode (incl. V1 vs V2) | 57 / 0 | **57 / 0** | report byte-identical |
| Research Play Mode closeout | 39 / 0 | **39 / 0** | byte-identical to base and to the committed `runtime_closeout/f15rt_playmode_report.txt` |
| Compile (Unity Roslyn, runtime and runtime + editor) | — | 0 errors | — |

With the instructor off, the V1 and V2 baselines are bit-identical in Play Mode.

**Official Baseline v1 gate** (`Tools/run_fdm_validation.ps1`, F-16 / shared): result recorded in the follow-up commit.


### 5.3 Tuning history (all MAVERICK_TUNED_NON_AUTHORITATIVE, each change driven by a recorded run)

| Step | What happened | Change |
|---|---|---|
| 1 | First Play Mode run, proportional only, push cap 0.35 | 26 / 29. Descent 8° not captured; the DropCommandSignal mechanics check counted the source's discarded cache as a command step |
| 2 | Pitch integral 0.15 /s² added | 24 / 29, worse: 15° turns ended 11.6° off, the moving-target lag grew to 15–20°, the speed fell into the low-speed cap. At a descent of 112 m/s, an 8–12° correction rolled the aircraft to a 37° bank. → **The integral is evaluated, not adopted (gain 0)** |
| 3 | Inverted-roll rule phased in between 30° and 60° of error; push cap 0.6; climb scored on sign and energy (a sustained climb is physically unavailable) | 28 / 29. Only the descent hold failed |
| 4 | Descent scored on the approach; the hold failure is reported as a limitation | 29 / 29 |

---

## 6. Limitations (read before flying)

- **Not flown by a human.** Feel is unverified. The handoff requires a human acceptance test before any merge.
- **Research aircraft, research domain.**
  - The model flies the frozen F-15 research model at the source's fixed 20,000-ft density, with **fixed research thrust: throttle inactive**.
  - The trim is a slow, high-α (17.5°), 2.9° fixed-thrust descent.
  - So the aircraft cannot sustain a climb or a hard turn. It trades height and speed, and the instructor shows that instead of hiding it.
- **No path hold.** V2 is a rate-command law.
  - A standing error of about 3° remains after a turn while the aircraft is below its trim energy.
  - An 8° descent is approached but not held: the speed builds, and V2's speed stability brings the path back toward the trim path.
  - The guidance will not push beyond 0.6 stick.
  - An outer-loop integral was tried and made things worse (§5.3).
  - A real fix needs thrust management or a path-holding law. Both are outside this work package.
- **The mouse cannot aim above ±85° elevation.** Loops over the top are not aimable with the mouse yet (scripted targets can).
- **Gains are tuned at one trim.** Heading loop K 0.6 /s, bank τ 1 s, command rate 4 /s. There is no claim that they suit another aircraft or another trim.
- **DropCommandSignal is opt-in.** With it, the body coasts without loads when the mouse is lost (the existing fail-closed rule). The default is neutral stick.
- **F2 law switch.** It works only with the stick centred; press X (hands-off) first. Under V1 the instructor only flies neutral stick.
- **The existing HUD (F1)** still lists the keyboard keys. The mouse overlay (F3, top right) lists the mouse keys.
- **Actuators are instantaneous**, as in V1 / V2. No actuator rate is sourced.

---

## 7. Human playtest recipe

1. Open the project in Unity 6000.3.16f1 on `claude/realistic-mouse-instructor-v2`.
2. Create a new, empty scene. Do not use a production scene.
3. Run **Maverick ▸ F-15 ▸ Mouse Instructor V2 (EXPERIMENTAL) ▸ Place Playtest Rig In Open Scene**.
   - It adds the rig and a separate mouse-aim camera.
   - It writes no asset. Delete the objects, or discard the scene, afterwards.
4. Press **Play** and click in the Game view. The cursor locks, and the aircraft starts at the point-36 trim with the aim on its flight path.

**Controls**

| Input | Effect |
|---|---|
| Mouse | Moves the aim target `[ ]`. The aircraft is **not** rotated by the mouse |
| Hold **C** | Free look: camera only; the aim stays |
| **X** | Hands off: neutral stick, aim re-anchored on the flight path |
| **Esc** | Releases the cursor (input loss → neutral stick). Click to capture again |
| **F3** | Mouse overlay on / off |
| **F1** | F-15 HUD on / off |
| **F2** | V1 / V2 law switch; centred stick only (press X first) |

**Overlay signals to watch**
- input state;
- aim error (flight path and nose);
- mode, current → desired bank, desired path rate, requested lift g;
- commands;
- limiter flags;
- the bound law;
- TAS / Mach / AoA / β / Nz / p q r;
- energy height and its rate;
- requested vs actual surfaces;
- live-ready / armed / load counts.

There are also warnings for readiness, non-authoritative thrust and active soft caps. The F-15 HUD shows the research-domain status and the actual surfaces.

**Script to fly and log (handoff §4, human acceptance)**

1. **Small turn.** Move the aim about 15° right and hold it.
   - Expect: a bank to the right of about 40°, then a gentle roll-out.
   - Expect a residual of a few degrees, with the aircraft descending.
2. **Large turn.** Move the aim 90° right.
   - Expect: a bank of about 50°, speed falling toward 78 m/s, the low-speed cap flag appearing.
3. **Reversal.** Swing the aim behind you.
   - Expect: a committed turn, about 60° of bank, height traded for speed.
   - The aircraft does not snap around.
4. **Low speed.** Put the aim high (about 40° up) and hold it.
   - Expect: the nose rises, speed decays, then the low-speed cap lowers the nose.
   - The aim is not reached. This is correct.
5. **Release.** Press X during a turn.
   - Expect: the stick goes neutral within about 0.6 s.
   - The aircraft keeps its bank and turns gently (V2 has no attitude hold).
6. **Free look and Esc.** Check that neither changes where the aircraft goes, beyond the declared neutral-stick policy on Esc.

Record for each step whether the motion felt controllable and whether anything looked non-physical. Unit tests do not replace this.

---

## 8. Next worker

- **Do not merge** without the user's own flight-test acceptance. `main` stays frozen.
- If the feel is accepted, the next physical gap is energy: thrust management, or a source-backed throttle path.
  - The instructor exposes this gap; it must not be closed with guidance gains.
  - The integral evidence in §5.3 shows why.
- Aim elevation beyond ±85° needs an aircraft-up-referenced aim frame. Keep the free-look isolation test.
- Re-run the gates in this report, the regression table in §5.2 and the frozen F-15 suites after any change.
