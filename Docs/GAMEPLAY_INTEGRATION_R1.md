# Gameplay Integration R1 — the playable flight game foundation

**Branch:** `claude/gameplay-f15-f16-r1`, from `main` @ `f0f9519`.
**Audit written first:** `Docs/GAMEPLAY_INTEGRATION_R1_AUDIT.md`.

R1 builds the game around the validated aircraft. **No flight physics was changed**:
- no aerodynamics, mass, thrust, control law, gain, trim or frozen file;
- the F-15 V2 law and its gains are untouched;
- nothing adds legacy torque or a Rigidbody write to the validated path.

---

## 1. Player loop

```
Main Lobby ── HANGAR ──► Hangar ── click F-15 ── FLY ──► Mav_InGame (air start, flying)
          └─ QUICK FLIGHT (F-15) ───────────────────────► Mav_InGame
Mav_InGame: fly ── V camera ── Esc pause ─► RESUME / RESTART FLIGHT / RETURN TO HANGAR
```

No editor menu, Inspector edit, law choice, manual arming or hand-placed prefab is needed.

## 2. Aircraft selection

**Hangar:**
- The two cards, **F-15 EAGLE** and **F-16 FIGHTING FALCON**, are clickable.
- A click:
  1. selects the aircraft in `MavGameSession`;
  2. shows its model and its details: role, status, flight model, facts, blockers and controls;
  3. points **FLY** at its launch path.
- **FLY** is enabled only when the aircraft can launch.
- Left/Right and Enter work as secondary controls. Esc goes back to the lobby.

**Development aircraft** (F/A-18E, F-22A, F-35A, and the legacy F-16):
- listed only behind the **DEVELOPMENT** toggle;
- labelled *legacy flight model, not validated, not part of the player release*;
- launched with a separate **DEV LAUNCH** button, never with FLY.

**Main Lobby:** HANGAR, QUICK FLIGHT (F-15 EAGLE, through the same launch contract), QUIT.

**Removed from the player flow:**
- the F-22 "primary aircraft" and "prototype" wording;
- the invented stat bars;
- the unfinished mode list.

**Free Flight is the only mode.**

**Identity mapping (compatibility):**

| Concept | F-15 | F-16 |
|---|---|---|
| Serialized id (`MavAircraftKind`, PlayerPrefs, scenes, legacy catalog) | `F15E` / `f15e` (unchanged) | `F16C` / `f16c` |
| Player display name | **F-15 EAGLE** (not F-15EX, not NASA 836) | **F-16 FIGHTING FALCON** |
| Physics configuration id (debug HUD only) | `F15_AFIT_BAUMANN_DAVISON_PILOT_CONTROLLED_V1` | F-16 Morelli reference FDM (`MavF16SelectionBinding`) |

The legacy catalog keeps its own "F-15EX EAGLE II" tuning blob for the development path. The player never sees it.

## 3. F-15 gameplay architecture

```
MavFlightLauncher.TryLaunch(F15E) ─► MavGameSession launch request ─► Mav_InGame
MavFlightSessionDirector ─► MavPlayableAircraftSpawner.TrySpawn(F-15 definition)
   MavF15PilotControlledRig.Create(..., Scripted, startOnFirstPhysicsStep, AssistedV2)   ← always V2
      MavManualPilotCommandSource (operational, by the rig) ◄── MavPlayerFlightInput (W/S A/D Q/E)
      MavF15PilotControlLawV2 → MavF15ControlActuator → MavF15PilotControlledAeroModel
        + MavF15PilotControlledFixedThrust → MavSixDoFBody        (validated path, unchanged)
      first FixedUpdate: StartFromTrim (point 36) → owner F15PilotControlledResearch
   Mav_Player_F15_Gameplay: MavRenderPoseFollower + placeholder model (render only)
```

**How the F-15 always flies V2:**
- The definition's only strategy is `PilotControlledF15AssistedV2`.
- The spawner takes no control-mode argument, and `VerifyAssistedV2` refuses anything else.
- No gameplay type has a control-mode field, property or parameter; `[G4]` checks this by reflection.
- No gameplay runtime source names `DirectV1`, `TrySetControlMode` or the law-switching HUD; `[G4]` checks this by source scan.
- The player aircraft never gets `MavF15PilotControlledHud`, whose F2 key switched laws.

**V1 stays** in the repository as the regression baseline and a development mode. Its law, prefab, tests and editor menus are unchanged. The gameplay rig still carries the V1 component because the rig always builds it, but it is disabled.

**Canonical gameplay path:** built from code by the spawner. The V1 and V2 prefabs remain validation and development prefabs; the V2 prefab brings its own camera and the F2 developer HUD.

## 4. F-16 gameplay architecture — current truth

**F-16 = NOT READY. FLY is disabled.** The hangar card lists what blocks it. `MavF16GameplayReadiness` evaluates this from the code as it stands:

| Blocker (player text) | Evidence |
|---|---|
| Live flight-model hold is on | `MavF16SelectionBinding.ReconcileNow` sets `simulationEnabled = false` unconditionally. The gameplay validation pins the source. |
| Engine data not cleared for live flight | The engine profile the F-16 installation creates has an **undeclared source envelope**. This is evaluated live. |
| Player controls not connected to the flight model | The binding wires its pilot command source `treatAsOperationalSource = false`. Pinned by the source scan. |

**Enforcement:**
- A forced F-16 launch into `Mav_InGame` is refused there too, with *AIRCRAFT NOT AVAILABLE*.
- Nothing is spawned: no F-15, and no legacy aircraft under an FDM label.
- No hold is released.

When the three blockers clear, the F-16 becomes Playable through the same contract. The pinned checks fail first, so the status cannot silently go stale.

**Development only:** "F-16 (LEGACY FLIGHT MODEL)" in the DEVELOPMENT list flies the old legacy `Mav_Player` stack. It is labelled as such and never reached from FLY.

## 5. Spawn authority

- **`MavFlightSessionDirector`** (on `Mav_FlightSession` in `Mav_InGame`) is the only spawn authority. It is a singleton, and a second director removes itself.
- **`MavPlayableAircraftSpawner`** is the only place a player aircraft is built.
- **The legacy `Mav_Player` stack** is kept but saved **inactive**: CAS_TestRange, Mav_Player, MaverickFresh_MouseFlightRig and MaverickFresh_Manager. The director activates it only for a development launch, in saved order.
  - `MavGameplaySceneSetup` (menu *Maverick/Gameplay/Apply R1 Flight Session To Mav_InGame*, or batch) does this idempotently.
  - The scene keeps `MavFreshBootstrap` and `MavAircraftProfileApplier`, so the official scene-wiring scan stays clean.
- **Nothing is DontDestroyOnLoad** in the gameplay layer.

## 6. Input ownership

**`MavPlayerFlightInput`** is the one player-input owner:
- It reads W/S pitch, A/D roll and Q/E yaw (arrows work too), with the keyboard source's own shaping.
- It writes the rig's one operational command source.
- It reads no key inside physics.

**Input is OFF** until the rig has started from trim and holds its ownership, and while paused or failed. Off means the stick is held exactly neutral while the source keeps its signal.

**Throttle:** Shift/Ctrl move only a UI throttle request. The F-15 flies its **fixed research thrust**, and the HUD shows **FIXED TEST THRUST**. It also shows "THROTTLE HAS NO EFFECT" for 3 s after a throttle key.

## 7. Camera ownership

The scene's one Main Camera carries `MavFlightCameraController`:

| Mode | Description |
|---|---|
| **CHASE** | default |
| **CLOSE CHASE** | tighter chase |
| **NOSE** | first-person reference view; there is no cockpit model in R1 |

- **V** cycles the mode.
- Smoothing is exponential and frame-rate independent.
- FOV widens by up to +7° with speed, and the chase distance grows by up to +15%.
- Fast rotation lags the camera slightly; there is no shake.
- The camera follows `MavRenderPoseFollower`, a render-only interpolation of the physics pose. The body keeps running with Rigidbody interpolation off, as its design requires.

## 8. HUD ownership

**Player HUD** (`MavPlayerFlightHud`):
- aircraft name and camera mode;
- TAS in knots, and Mach;
- altitude in feet, and vertical speed;
- **G**: the model's own specific-force load factor, shown only when valid;
- AoA and heading;
- **FIXED TEST THRUST**;
- one warning line: *outside flight-model envelope / low altitude / throttle has no effect*;
- the control hint for the first 12 s.

**Debug HUD** (`MavFlightDebugHud`, **F1**, off by default):
- configuration id, FDM owner and reason, control law;
- V2 gain scale, qbar, p/q/r, α/β;
- loads and duplicate refusals;
- the full research diagnostics;
- the F-16 readiness summary;
- a census of cameras, listeners, rigs, armed bodies, operational sources, inputs and directors.

The UI is IMGUI for R1 and presentation-only (`MavGameplayUiStyle` and the OnGUI bodies). Moving to Canvas or UI Toolkit later replaces those alone.

## 9. Pause / restart

**Esc** pauses and resumes.

**Pause:**
- `Time.timeScale = 0` stops every FixedUpdate at once: law, actuator, body and ownership stop together and resume together;
- player input is suspended (stick neutral);
- the cursor is shown.

**Pause menu:** RESUME, RESTART FLIGHT, RETURN TO HANGAR; **R** also restarts while paused.

**Restart** reloads `Mav_InGame` with the same request (attempt +1):
- the old aircraft, session, camera state and world are destroyed with the scene;
- one new F-15 starts from the same validated trim, with zero V2 filter state.

**Return to hangar:**
- clears the flight request and keeps the selection;
- nothing from the flight survives the scene change.

## 10. Fail-closed behaviour

| Situation | Screen | Physics |
|---|---|---|
| Aircraft not a player aircraft, or not ready (F-16) | AIRCRAFT NOT AVAILABLE + reason | nothing spawned |
| Rig refuses to start, or does not start within 6 s | AIRCRAFT START FAILED + reason | nothing armed; the rig's own refusal stands |
| Ownership lost in flight (the rig's Fault) | FLIGHT MODEL STOPPED | clock stopped; the rig stays faulted |
| Ground contact (altitude ≤ 3 m) | GROUND IMPACT | clock stopped; the world has no colliders |
| Outside the research envelope | HUD warning *OUTSIDE FLIGHT MODEL ENVELOPE – NO AERODYNAMIC LIFT* | the model refuses aero/thrust as validated; only gravity acts |

The failure screens offer **RETRY** (launchable aircraft only) and **RETURN TO HANGAR**. **F1** shows the technical detail. Selection never falls back to another aircraft.

## 11. Playable vs development aircraft

| Aircraft | Status | Launch |
|---|---|---|
| F-15 EAGLE | **Playable** | FLY → pilot-controlled research F-15, Assisted V2 |
| F-16 FIGHTING FALCON | **NOT READY** (3 blockers, §4) | FLY disabled; refused if forced |
| F-16 (legacy), F/A-18E, F-22A, F-35A | **Development** | DEV LAUNCH only, legacy flight model, labelled |

## 12. Known limitations

- **The F-15 flies the research model's envelope.**
  - It is defined at the source's fixed 20,000-ft density, so geometric altitude does not change the aerodynamics.
  - It starts at 300.8 ft/s (≈ 178 kt) at α ≈ 17.5°: slow and nose-high by nature of the Table VII trim point.
  - Outside 218.5–699.7 ft/s or the α/β span, lift refuses and the HUD says so.
  - Thrust is fixed.
- **Not flown by a human in this work.** The Play Mode tests drive the player input path with scripted commands in batch mode, where OnGUI does not draw. The HUD, menus and camera **feel** need the manual smoke test (§13).
- **Placeholder art:** primitive aircraft models and a procedural world, with no cockpit model.
- **F-16 has no playable path** until its blockers clear (§4).
- **Development launch is minimal:** the legacy stack keeps its own camera, HUD, weapons and keys. The targeting pod also uses V, Esc and R, so Esc there can conflict. It is development-only.
- **`MavF16SelectionAutoSetup` still scans 4× a second** for appliers. It is F-16 FDM code and was left unchanged; in a player F-15 flight it finds only the inactive legacy player and does nothing. Replacing it with explicit wiring belongs with the F-16 live work.
- **`Mav_InGame` contains a pre-existing character FBX instance** at the origin (`Hoshimi Miyabi (1)`). It is left untouched.
- **The UI is IMGUI.** A Canvas / UI Toolkit migration is future work.

## 13. Manual smoke-test procedure

| Step | Action | Expected |
|---|---|---|
| A | Open the project in Unity 6000.3.16f1 | — |
| B | Open `Assets/MaverickFresh/Scenes/Mav_MainLobby.unity`, press Play | Lobby shows MAVERICK, HANGAR, QUICK FLIGHT, QUIT |
| C | Click **HANGAR** | Hangar shows two cards and the F-15 model |
| D | Click **F-15 EAGLE** | Card highlighted; details show READY |
| E | Click **FLY** | Short fade; "PREPARING F-15 EAGLE" |
| F | F-15 V2 starts | Aircraft flying at ~178 kt, ~20,000 ft. F1 shows control mode AssistedV2, law "Maverick F-15 pilot-control V2", owner F15PilotControlledResearch |
| G | W / S | Nose up / down |
| H | A / D | Roll left / right, and the roll stops when released |
| I | Q / E | Yaw left / right |
| J | **V** | CHASE → CLOSE CHASE → NOSE |
| K | **Esc** | Pause menu, cursor shown, aircraft frozen |
| L | **RESUME** | Same flight continues, controls work |
| M | Esc → **RESTART FLIGHT** | New flight from the same start |
| N | Esc → **RETURN TO HANGAR** | Hangar, F-15 still selected |
| O | Click **F-16 FIGHTING FALCON** | NOT READY, three blockers listed, FLY disabled |
| P | Verify F-16 behaviour and readiness | FLY does nothing; no aircraft is launched. Optional: DEVELOPMENT → F-16 (LEGACY FLIGHT MODEL) → DEV LAUNCH flies the labelled legacy stack |

---

## Validation

### Gameplay

| Suite | Result |
|---|---|
| Headless `MavGameplayIntegrationValidation` (G1-G10, F-16 truth, scenes, writer scan, wording) | **26 / 0** |
| Play Mode `MavGameplayFlowValidation` (real scenes: lobby, hangar clicks, F-15 flight incl. G11-G13, control pulses, pause/resume, restart, return, forced F-16, development launch) | **29 / 0**, two runs byte-identical |

Play Mode highlights:
- **Census in flight and after restart:** exactly 1 camera, 1 audio listener, 1 pilot rig, 1 armed body, 1 operational source, 1 player input, 1 director, 0 legacy jets.
- **Control pulses through the player input path** (+0.5 for 1 s, all signs correct): pitch Δq +0.0573, roll Δp +0.0965, yaw Δr +0.0246 rad/s. 500/500 loads, no duplicate, ownership held.
- **Pause:** 0 physics steps in 20 frames, stick held neutral against a full deflection.
- **Resume:** the same aircraft flies on, one load per step.
- **Restart:** the old aircraft is destroyed; the new one starts at TAS 300.80 ft/s, α 17.486°, rates 0, altitude 6095.9 m.
- **Return to hangar:** nothing survives; the F-15 stays selected.
- **Forced F-16:** AIRCRAFT NOT AVAILABLE, nothing spawned, selection kept.
- **Development launch:** the legacy stack alone (1 legacy jet, 0 pilot rigs).

### Physics regression (clean worktree)

| Gate (clean worktree of `be9cd00`) | Result |
|---|---|
| Compile (Unity Roslyn: runtime, runtime + editor, editor) | **0 errors** |
| Frozen F-15 suites (17) | **757 / 0**, every check line identical to the previous record (file counts included; no FlightDynamics file added) |
| Frozen F-15 Play Mode (closeout) | **39 / 0**, byte-identical to the committed report |
| F-15 pilot V1 headless / Play Mode | **49 / 0** / **31 / 0**, identical to the V1 record |
| F-15 pilot V2 headless / Play Mode | **54 / 0** / **57 / 0**, identical to the previous record; two runs byte-identical |
| Official Baseline v1 gate (shared / F-16) | **PASS 1585 / 0** (25 counted suites), physics delta NONE |

**Scenes:** no missing scripts in the 3 game scenes; one Camera and one AudioListener saved in Mav_InGame; official scene-wiring scan clean.
