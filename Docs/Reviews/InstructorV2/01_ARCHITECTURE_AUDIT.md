# 01 — Architecture Audit

**Audited:** PR #35, `claude/realistic-mouse-instructor-v2` @ `ad348086e09f7761e5e3e3ecef7cb45f97e8cff0`
**Base:** frozen `main` @ `71820a97b8d58c69229799fb00868bce4ded87d1`
**Method:** full read of the PR diff (23 files, +4470/−2) and the unchanged core it depends on, plus dynamic probes (see `04_VALIDATION_AUDIT.md` §4).

Every path below is relative to `Assets/MaverickFresh/Scripts/FlightDynamics/` unless it starts with `Docs/` or `Tools/`.

---

## 1. Pipeline as implemented

```text
Update (render frame)
  MavMouseAimPilotCommandSource.Update            reads MavFreshInput, accumulates mouse deltas into a WORLD az/el aim
                                                   (Core/MavMouseAimPilotCommandSource.cs:270-309, Core/MavMouseAimInput.cs:62-72)
FixedUpdate, by DefaultExecutionOrder
  -600  (validation runners only)
  -500  MavF15PilotControlledRig.FixedUpdate       start from trim on the first step (F15/MavF15PilotControlledRig.cs:130-136)
  -400  MavFlightPhysicsOwnership.FixedUpdate      re-verifies the pilot-controlled grant, gravity flag, arming (Core/MavFlightPhysicsOwnership.cs:359-372)
  -350  MavMouseAimPilotCommandSource.FixedUpdate  READS Rigidbody pose + velocity -> MavMouseAimGuidance.Step -> caches MavPilotCommand
                                                   (Core/MavMouseAimPilotCommandSource.cs:363-449)
  -300  MavF15PilotControlLawV2 (unchanged)        TryGetCommand -> rate/sideslip law -> requested surfaces (F15/MavF15PilotControlLawV2.cs:149-221)
  -200  MavF15ControlActuator                       sole actual-surface owner
  -100  MavSixDoFBody                               research aero + fixed thrust + load-set gravity -> ONE load application
                                                   (Core/MavSixDoFBody.cs:239-385)
LateUpdate / OnGUI
  MavMouseAimCameraRig                              moves only its own camera; markers + overlay (Core/MavMouseAimCameraRig.cs:163-224)
```

## 2. What is architecturally correct (verified)

| Property | Evidence | Verified how |
|---|---|---|
| The guidance never produces a force, moment, surface, pose or velocity | `MavMouseAimGuidance.Step` returns only `MavMouseAimGuidanceOutput` (normalized pitch/roll/yaw + telemetry), `Core/MavMouseAimGuidance.cs:403-669` | code read; PR `[MI14]` source scan; probe runs show `loads == steps`, no duplicate rejections |
| The source only reads the Rigidbody | reads `aircraftBody.rotation`, `.linearVelocity`, `.position` (`Core/MavMouseAimPilotCommandSource.cs:383-385`); no setter anywhere in the four new Core files | code read; `[MI14]`; PR `[RM] no pose write` |
| One law writer, one load owner | rig enables exactly one of V1/V2 (`F15/MavF15PilotControlledRig.cs:218-221`); grant still refuses two enabled laws (`F15/MavF15PilotControlledAuthority.cs`, re-checked every step by `Core/MavFlightPhysicsOwnership.cs:984-991`) | code read; PR `[MI13]`; reproduced |
| Ownership grant not widened | the grant constrains law/actuator/aero/thrust/profile/environment, not the command-source type; the PR adds no grant code | `git diff` touches no ownership file |
| Command reaches the law in the same physics step | source at −350 < law at −300 (`Core/MavMouseAimPilotCommandSource.cs:61,65`) | code read; `[MI1]` |
| `TryGetCommand` is side-effect free (idempotent polling) | returns `cached`; only `StepGuidance` advances state (`Core/MavMouseAimPilotCommandSource.cs:167-172`) | code read; `[MI12]` |
| Opt-in only | `MavF15PilotCommandSourceKind.MouseAim = 2` is additive; defaults stay `Keyboard` / `DirectV1` (`F15/MavF15PilotControlledRig.cs:6-20,67-70`) | code read; `[MI13]`; PR's V1/V2 regression byte-identical |
| Minimal rig change | +44/−2 lines: enum value, resolver branch, law declaration to the source, reset on trim start | `git diff` |

**Conclusion on ownership:** the PR respects the single-writer architecture. No ownership protection was weakened and none should be weakened to address the findings below.

## 3. Architectural concerns

### A1. Two different meanings of "fail closed" meet in `DropCommandSignal` (finding F-05)

The readiness gate (`Core/MavSixDoFBody.cs:294-299`, `1080-1110`) was designed as an **arming** gate: do not apply loads from a pipeline that is not proven complete. For the pilot-controlled F-15, gravity lives **inside** the load set (`Core/MavSixDoFBody.cs:334-345`; `Rigidbody.useGravity` off, `F15/MavF15PilotControlledRig.cs:149`). When an airborne aircraft's operational command signal disappears, the same gate withholds **every** load — aero, thrust, gravity, gyroscopic compensation — while PhysX keeps integrating the Rigidbody with zero force and zero torque (Unity damping zeroed).

That is not "safe": the aircraft becomes a gravity-free, drag-free body coasting with constant velocity and constant angular velocity. The ownership authority does not fault (`TryGrantResearchOwnership` does not check the signal), so the aircraft stays "armed, owner held" and resumes from whatever attitude it tumbled into. Measured: 15 s of loss → α 53.5°, |β| 33.4°, and on restore the research aero refuses for 99 steps (`evidence/probe_batch2`, DROP15_RESTORE).

The PR's own `[RL] INPUT LOSS (DropCommandSignal)` check scores this coast as PASS ("fail closed, counted"). It is opt-in and documented as "why this is not the default", but the Play Mode record shows α 33.6° / bank 77° after only 3 s, and that was not flagged.

**Assessment.** For an airborne body, withholding all loads is **not** an acceptable runtime signal-loss response. It is acceptable as a start-up arming refusal. The default `NeutralStickHoldThrottle` is the correct airborne fail-closed policy: the physics continues and the controls go to a defined neutral. The fix belongs in the source/rig policy (do not offer `DropCommandSignal` on a live airborne rig), **not** in the readiness or ownership guards.

### A2. Domain exit is not an ownership or readiness event (findings F-01, F-11)

When the research aero or thrust refuse a state, they return **zero** (`F15/MavF15PilotControlledAeroModel.cs:18-22`, `Refuse` at `:171-175`). The body keeps applying the load set, so gravity is the only load, readiness stays "live-ready YES", and the owner stays `F15PilotControlledResearch`. The design accepts this ("fail-closed: returns zero and says why"), and the F-15 HUD (F1) prints `REFUSED - no aero/thrust load` (`F15/MavF15PilotControlledDiagnostics.cs:151`).

The architecture therefore relies on something upstream keeping the aircraft inside the domain. The instructor claims to be that something ("command caps that keep the requested pilot intent away from the edges of the aerodynamic model's admitted domain", `Core/MavMouseAimGuidance.cs:55-60`). It protects only the **low-speed** floor and α. It does **not** protect the **upper** speed bound (699.7 ft/s = 213.3 m/s). Measured: a held −85° aim crosses 213.3 m/s at t = 30.9 s, and the aircraft never re-enters the domain (`evidence/probe_batch2`, DIVE85_HOLD_90S).

The mouse overlay (F3) has no aero/thrust-refusal warning (`Core/MavMouseAimPilotCommandSource.cs:570-581`), so a tester watching only the mouse overlay sees `live-ready YES` while the aircraft has no aerodynamics.

### A3. The guidance assumes a plant it is not connected to (findings F-02, F-04)

The guidance's stick scaling assumes that V2 tracks `q_cmd = stick × 7 °/s` and `p_s,cmd = stick × 15 °/s` (`Core/MavMouseAimGuidance.cs:522-533,565-572`). V2 is a proportional + feed-forward rate law with **no pitch integrator** and a **fixed trim stabilator bias** (`F15/MavF15PilotControlLawV2.cs:81-84,258-263`).

Consequences the guidance does not model:
- at speeds above trim, neutral-stick α stays ≈ 14°. Lift then scales with q̄, so the aircraft pulls 3–4 g at 180–200 m/s even with the guidance at its −0.6 push limit (DIVE30/60/85 probes);
- the closed bank/heading loop has much more lag than the "~1-s bank and V2 roll lags" assumed in the gain rationale (`Core/MavMouseAimGuidance.cs:218`). Measured ζ ≈ 0.07, not ≈ 0.45.

This is a separation-of-concerns gap, not an ownership defect. The outer loop was tuned at one trim against an unidentified inner loop. PR #36 `05_CONTROL_STABILITY_AND_IDENTIFICATION.md` and `06_GUIDANCE_VALIDATION_PLAN.md` already call for identifying the inner loop. This audit's measurements confirm why that is needed.

### A4. Coupling and binding notes (LOW)

- `MavMouseAimCameraRig.OnEnable` binds `FindFirstObjectByType<MavMouseAimPilotCommandSource>()` (`Core/MavMouseAimCameraRig.cs:144-147`). With two aircraft the binding is arbitrary.
- The source copies the law's full-stick rates once per `BindControlLaw` (`F15/MavF15PilotControlledRig.cs:229-240`). Editing `lawV2.gains` at runtime leaves the guidance's stick scale stale until the next bind.
- The guidance reads the inertial `Rigidbody.linearVelocity` as the flight path and derives its own α (`Core/MavMouseAimGuidance.cs:430-432`). That is correct today only because there is no wind. The body's air-relative `debugState` is the authoritative source if wind is ever added.
- `MavMouseAimGuidanceConfig.IsSelfConsistent` is called only by validation, never at runtime. `commandRatePerSec = 0` silently disables the rate limit (`Core/MavMouseAimGuidance.cs:641`).

## 4. Scheduling and data freshness (summary; details in 03)

- **Guidance input.** The guidance reads the Rigidbody pose after the previous `Physics.Simulate`. The law reads `body.debugState`, which the body built before that simulate. The guidance is therefore one step *fresher* than the law. This is consistent and documented; no stale-step defect was found.
- **Mouse latency.** Mouse deltas are accumulated in `Update`. In Unity's loop, `FixedUpdate` runs before `Update` within a frame, so a physics step sees input up to the previous rendered frame. The PR's `[RI]` and `[MI11]` checks bound this correctly at 25–144 fps.
- **Repeated steps per frame.** At render rates below 50 fps, several physics steps share one aim snapshot. This is bounded by `[RI]` at 25 fps; rates below 25 fps were not tested (hypothesis H6).

## 5. Architecture verdict

The ownership architecture is sound and intact. The instructor is a well-isolated intent producer. The architectural problems are in what the instructor **assumes about**:
1. the plant: no V2 integrator, unidentified lag;
2. the model domain: no upper-speed protection;
3. what "fail closed" means for an airborne body: `DropCommandSignal`.

None of these requires touching ownership, readiness or the grant.
