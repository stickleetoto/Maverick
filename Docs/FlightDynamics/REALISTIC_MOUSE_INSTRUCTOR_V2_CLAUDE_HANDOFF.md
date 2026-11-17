# Claude Implementation Handoff — Maverick Realistic Mouse Instructor v2

**Date:** 2026-11-17
**Repository:** `stickleetoto/Maverick`
**Dedicated branch:** `claude/realistic-mouse-instructor-v2`
**Branch creation base:** `71820a97b8d58c69229799fb00868bce4ded87d1` (`main`, documentation-only freeze commit; parent stable research commit `f0f9519a785d7f327a0de7e452c93285c073d42f`)
**Target Unity:** `6000.3.16f1`
**Assignment:** Claude — IMPLEMENT and VALIDATE a narrow, opt-in first vertical slice. Do not stop at a speculative architecture document if implementation and tests are feasible.

## 0. User intent / non-negotiable product definition

**"War Thunder-style intuitive mouse aiming, but fighter motion must emerge from a physically coherent aircraft model."**

The mouse indicates a desired flight/pointing direction; it must **not** set aircraft rotation, velocity, or turn rate by fiat. The instructor requests pilot control, and the existing aircraft-specific control law and actual aerodynamic surfaces produce forces/moments through the validated FDM.

- Mouse aiming should feel responsive and intelligible.
- Aircraft inertia, aerodynamic authority, actual actuator limits, trim, drag, energy loss, low/high speed differences, and the limits of the modeled flight envelope must remain visible.
- A 180-degree mouse reversal requests an achievable maneuver, **not** an instantaneous turn.
- Turns should prefer realistic bank + lift/pitch over flat yaw pivoting; yaw is only used where justified by the controller and available model.
- A target direction is an **intent**, not a promise of achievable attitude/trajectory.
- Never claim this reproduces War Thunder's proprietary instructor or a real F-15/F-16 production FCS.
- Everything newly tuned on the mouse guidance side must be labeled `MAVERICK_TUNED_NON_AUTHORITATIVE`; do not pretend measured or source-validated aircraft numbers exist.

## 1. Mandatory read/audit before editing

Read the following files, then follow their physical ownership and evidence rules:

- `Docs/PROJECT_FREEZE_2026.md` — frozen `main`, prior rejected integration/feel changes, no merge without explicit human authorization.
- `README.md` — physics-first goal, scripts-only/reversible patches.
- `Docs/FlightDynamics/F16_LIVE_FDM_ARCHITECTURE_V0.1.md` — common command/control/actuator/FDM separation.
- `Assets/MaverickFresh/Scripts/FlightDynamics/README.md` — FDM pipeline, readiness, coordinate conventions, units, source limitations.
- `Docs/Reference/F15_PILOT_CONTROLLED_V1.md` and `Docs/Reference/F15_PILOT_CONTROLLED_V2.md` — F-15 pilot-controlled authority, V2 rate/sideslip law and in-domain constraints.
- `Docs/Validation/FDM_VALIDATION_BASELINE_V1.md` — authoritative test runner prerequisites and baseline rules.
- `Assets/MaverickFresh/Scripts/FlightDynamics/Core/MavMouseInstructorPilotCommandSource.cs` — legacy bridge **for reference only**, not proof its old commands are physically ideal.
- `Assets/MaverickFresh/Scripts/FlightDynamics/Core/MavPilotCommandSource.cs`
- `Assets/MaverickFresh/Scripts/FlightDynamics/Core/MavFlightControlLawBase.cs`
- `Assets/MaverickFresh/Scripts/FlightDynamics/Core/MavSixDoFBody.cs`
- `Assets/MaverickFresh/Scripts/FlightDynamics/F15/MavF15PilotControlledRig.cs`
- `Assets/MaverickFresh/Scripts/FlightDynamics/F15/MavF15PilotControlLawV2.cs`
- `Assets/MaverickFresh/Scripts/MavInstructorController.cs`, `MavMouseFlightJet.cs`, `MavFreshInput.cs` (review only to understand the legacy controls and why physical writers must not coexist).
- Existing F-15, F-16, physics-ownership, scheduling and Unity validation code.

**Important repository caveat:** The root `CLAUDE.md` contains a *historical, task-specific F-16 Phase 1* worker branch/PR instruction. This assignment uses the dedicated branch above; do not redirect this work into the old F-16 Phase 1 branch. Preserve all relevant safety/ownership/source constraints from that document.

First report: what can be reused, what F-15 V2 already solves, and whether any current rig readiness/ownership checks forbid the proposed input source.

## 2. Physical boundaries that must never be crossed

Current intended pipeline:

```text
mouse aim / target direction (input)
  -> independent aim guidance (target error -> bounded normalized pilot intent)
  -> MavPilotCommandSourceBase / MavPilotCommand
  -> EXISTING MavF15PilotControlLawV2 (rate/sideslip feedback; first aircraft)
  -> MavF15ControlActuator (sole actual surface owner)
  -> F-15 research aero + existing fixed research thrust
  -> MavFlightDynamicsLoadSet -> MavSixDoFBody
  -> Unity Rigidbody (sole force/moment application)
```

- New mouse guidance/input components MUST NOT use `AddForce`, `AddTorque`, `MoveRotation`, or directly write Rigidbody `velocity`, `angularVelocity`, `position`, or `rotation`.
- Do not rotate the aircraft `Transform` directly.
- Do not add fake lift, lateral thrust, auto-velocity alignment, G, drag, damping, direct control torques, or energy/speed assists.
- Do not layer new physical effects on top of the existing aero/propulsion. One owner per physical effect.
- The current F-15 ownership grant explicitly accepts its pilot-control V1/V2 laws. **Do not replace that law or silently expand its grant merely to fit a new instructor.** Prefer a new command-source adapter that uses the existing `MavF15PilotControlLawV2`.
- Respect the existing law-to-actuator-to-body order and one-step state latency; no duplicate command or load application.
- Keep the baseline `DirectV1`, existing `AssistedV2`, keyboard source, scenes and default prefabs unchanged.

## 3. Scope: first real, usable vertical slice

### A. Independent deterministic aim guidance

Implement a testable calculation of:

```text
(current aircraft orientation / flight-state, target direction WORLD,
 previous guidance state, configuration, dt)
  -> normalized pitch / roll / yaw intent + debug target errors
```

A target direction can be supplied by an external input/camera adapter or a deterministic test.

Requirements:
- Explicit axis conventions and sign mapping. Resolve Unity world/local vs aircraft +X fwd/+Y right/+Z down. Test at canonical headings, inverted/rolled attitudes, and near-vertical target directions.
- Distinguish nose direction, aircraft velocity/flight-path direction, and camera aim direction. Choose/document the first-slice target semantics. The system must not silently conflate them.
- Prefer banking toward the target turn plane and using pitch authority; coordinated yaw should not become an unrealistically fast flat-turn shortcut.
- Graceful zero-angle behavior, target behind the aircraft (180 degrees), target at/near zenith/nadir, inverted flight, and negligible/nonfinite target vector.
- Bound and smooth commands to avoid discontinuities and thrashing. Explicitly reset filter/integrator state when enabled or input source changes.
- Expose target angular error, desired bank/roll demand, guidance mode, limiter activity and emitted normalized commands for telemetry.
- Preserve the F-15 model's documented envelope and do not extrapolate validated precision to unsupported speeds/Mach/AoA. Guidance protection is not a substitute for a missing high-AoA aerodynamic model.
- Do NOT guess real-aircraft G, AoA, roll-rate, actuator rate, or structural limits. If you add guidance-side soft caps, label and parameterize them as gameplay/controller tuning and show which underlying model limit they protect (if any).

### B. Independent mouse input adapter

Add a new opt-in `MavPilotCommandSourceBase` implementation (name at implementer's discretion) rather than modifying the legacy physical writer.

- Read the existing `MavFreshInput` abstraction or a new narrow adapter, supporting currently configured Unity input paths.
- Mouse input should control a *persistent aim target/reticle*, not aircraft orientation. Define whether it uses delta-accumulated aiming or a screen-space camera ray, and test/document its camera coupling and cursor-center behavior.
- Free-look must not accidentally change the aircraft aim target.
- Sample input in `Update`; consume a coherent cached snapshot in `FixedUpdate`/command polling. Avoid using render delta time as physics step time.
- Publish `IsOperationalCommandSource` and `HasCommandSignal` truthfully; on camera loss, focus loss, invalid target, or input disconnection, fail closed via an explicit policy (e.g., center surfaces while holding last throttle) and never use inspector test commands as a hidden live fallback.
- Throttle must remain owned by the existing pilot/propulsion path. For this F-15 research rig, note that live throttle is **inactive** because it uses fixed research thrust; do not fake a responsive engine.

### C. Optional F-15 pilot-controlled rig integration

- First human-flyable exercise should be against the already pilot-controlled F-15 **research** rig, not the F-16 reference path (its authoritative thrust/readiness constraints remain).
- Provide an explicit, opt-in way to select the new mouse source while keeping the existing pilot-controlled `AssistedV2` law and original startup/ownership grants. An additive enum/mode or an isolated editor/runtime test rig is acceptable after checking existing wiring; prefer smallest change.
- Leave V1, V2 keyboard default and exact research profile unchanged.
- If a genuine blocker prevents a human-playable F-15 integration, implement deterministic pure guidance + Unity isolated test rig anyway, and describe the blocker with its exact file/authority guard rather than weakening the guard.
- No automatic activation on existing aircraft/prefabs. A separate test invocation is preferable.

### D. Diagnostics and playtest

At minimum make it observable:
- mouse aim target and angular error;
- guidance output pitch/roll/yaw;
- active pilot-control law and physical ownership;
- real surface requests/actual travel;
- TAS/Mach, AoA/beta, Nz, pitch/roll/yaw rates, energy trend;
- warnings for out-of-envelope/unavailable thrust data or failed readiness.

Use existing telemetry where possible; minimal new debug fields, not a new HUD subsystem. Do not add hit markers, combat notifications, or arcade impact feedback.

## 4. Validation and acceptance gates

Add deterministic tests for the **actual production guidance function**, not an independently reimplemented mathematical sketch:

1. Straight-ahead target, small left/right/up/down targets, target behind, vertical target and inverted/rolled aircraft.
2. Zero vector / NaN / Infinity / missing camera or command source; no nonfinite command, no concealed inspector fallback.
3. Mouse input timing: variable render FPS vs fixed `0.02s` physics step produces consistent command snapshots for the same input trace (document unavoidable sampling differences if any).
4. Sudden 180-degree reversal does not teleport/instantaneously rewrite pose or velocity; only bounded control commands appear.
5. Banked turn intent vs excessive flat yaw, including sign conventions in aircraft/body axes.
6. High demand and low authority: no forced convergence by bypassing aero/actuator limits; saturation/unsupported envelope is visible.
7. Live readiness, one actual control-law writer, one force/moment owner, no duplicate application; legacy physics writers cannot coexist in active ownership.
8. F-15 V1 and existing V2 default behavior regressions unchanged; the test should demonstrate that opt-in OFF preserves the baseline.
9. Existing FDM/ownership/gyro/propulsion and F-16 regression suites stay green. Report *actual* checked suites, environment and exact counts; never assert a Unity pass if Unity was not executed.
10. When possible, one in-domain Play Mode end-to-end test with a deterministic moving target, scoring angular error trend and bounded surface/kinematic response (no invented physical performance target).

**Human acceptance required before merge:** demonstrate a small/large turn, reversal, low-speed degraded control and release-to-neutral; log whether motion feels controllable without violating aircraft physics. A human flight test is not replaced by unit tests.

## 5. Strict edit boundaries

**Allowed as necessary (subject to audit):**
- New scripts under `Assets/MaverickFresh/Scripts/FlightDynamics/Core/` for guidance + input source (plus `.meta`).
- Minimal opt-in F-15 pilot rig selection/wiring under `Assets/MaverickFresh/Scripts/FlightDynamics/F15/`.
- New tests/editor runners under `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/` and `Editor/`.
- This handoff and a new implementation/validation report under `Docs/FlightDynamics/`.

**Disallowed:**
- Scenes, prefabs, materials, meshes/models, cameras unrelated to the opt-in input adapter.
- Weapons, radar/sensors, damage feedback, AI/FAM.
- Aerodynamic coefficients/data, atmosphere, mass/inertia, engine thrust deck, gravity, propulsive loads, F-15 reference identity or physics validation baselines.
- Silent tuning of F-15 pilot-control V1/V2 values or legacy mouse settings.
- JSBSim/Dagor/Jolt/Bullet/Unity physics-backend swaps in this work package.
- Any merge, rebase, reset, force push, stash modification, automatic promotion of this experiment to `main`.

If a required change lies outside the allowed list, stop that edit and document the exact reason and a smaller alternative. Do not work around readiness or provenance guards.

## 6. Delivery contract

1. Confirm branch and ancestry, clean working tree, baseline Unity version.
2. Audit. Implement the smallest complete, opt-in mouse-aim -> pilot-command -> existing F-15 V2 control -> surface -> FDM path.
3. Add deterministic guidance/ownership/regression tests and an isolated Unity validation or Play Mode path.
4. Run available validations and report precise results, explicit gaps and flight-envelope limitations. No fake pass counts.
5. Provide a human playtest recipe with all controls, a runnable scene/rig setup *without rewriting existing scene/prefab assets*, and expected observable signals.
6. Commit only on `claude/realistic-mouse-instructor-v2`. Open a **draft** PR against `main` once there is code + evidence, but **do not merge**. Existing frozen baseline remains authoritative until the user personally accepts the feel and authorizes integration.
7. Final report must include changed files, algorithm/axis choices, safety gates, validation counts, untested areas and exact instructions for the next worker.

### Prioritized execution order

P0: audit existing law/authority and define deterministic aim-guidance seam.
P1: implement geometry-to-command guidance + pure tests.
P2: add new operational input source, snapshot/error handling + tests.
P3: opt-in F-15 research rig, Play Mode and user playtest path.
P4: refine only if recorded evidence shows a problem; avoid feel-only undocumented gain changes.

**Stop condition:** A request to make the aircraft turn faster must not be solved by adding force, torque, speed stabilization, pose snapping or untraceable gain overrides. The goal is accessible controls over defensible physics, not arcade physics disguised as realistic.
