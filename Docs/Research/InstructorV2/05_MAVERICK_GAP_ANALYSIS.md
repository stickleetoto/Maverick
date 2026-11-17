# 05 — Maverick Gap Analysis for Realistic Mouse Instructor v2

**Status:** research record. No production code, scene, prefab, coefficient or tuning was changed.
**Repository state examined:**

| Item | Value |
|---|---|
| Research branch base | `origin/main @ 71820a97b8d58c69229799fb00868bce4ded87d1` (documentation-only freeze commit) |
| Stable research baseline | `f0f9519a785d7f327a0de7e452c93285c073d42f` (parent of the freeze commit; `Docs/PROJECT_FREEZE_2026.md`) |
| Development branch examined (read only) | `claude/realistic-mouse-instructor-v2 @ 5776617` — at the time of reading it contained **only** the handoff document `Docs/FlightDynamics/REALISTIC_MOUSE_INSTRUCTOR_V2_CLAUDE_HANDOFF.md`; no implementation yet. This snapshot will go stale. |
| Date of reading | 2026-11-17 |

Evidence classes used here: `REPO_VERIFIED` (read in the code at the cited line), `REPO_DOC` (stated by a repository document, not re-executed), `ENGINEERING_INFERENCE`, `HYPOTHESIS` (plausible, not demonstrated), `MAVERICK_PROPOSAL`. The research brief's external classes (`PRIMARY_VERIFIED`, …) are used in documents 01–04 and 07.

Line numbers refer to the research-branch checkout (= `71820a9`).

---

## 1. Summary

1. **The physical pipeline the new instructor needs already exists and is unusually well guarded.** `PilotCommand → ControlLaw → Actuator → Aero/Propulsion → LoadSet → SixDoFBody → Rigidbody` is implemented, ordered (−300/−200/−100), identity-checked, and fail-closed. The instructor's job reduces to producing a `MavPilotCommand`. Nothing in the physics layer needs to change.
2. **The legacy instructor (`MavInstructorController`) cannot simply be bridged.** Its output was tuned as a normalized *torque demand* for the legacy direct-torque jet; the new laws interpret the same numbers as *rate / load-factor / sideslip demands*. It also contains attitude-recovery and nose-down biases that fight the aim, and it runs in `Update` at render rate. The existing bridge (`MavMouseInstructorPilotCommandSource`) passes those numbers through unchanged.
3. **Mouse-aim guidance must know what the stick *means* for each control law, and today that is not exposed.** F-15 V2 full stick = 7 °/s pitch rate, 15 °/s stability-axis roll rate, 1.5° sideslip; the F-16 V0.1 law full aft stick = 9 g load factor, 270 °/s body-axis roll rate, 8° sideslip. A guidance layer that emits "normalized stick" without that knowledge is either aircraft-specific by accident or wrong.
4. **The only human-flyable FDM aircraft (F-15 research rig) has a very narrow envelope.** One trim (300.8 ft/s, α 17.5°, fixed 8,300 lbf thrust, fixed 20,000-ft density), 218.5–699.7 ft/s domain, no protections, and a full-stick pitch-rate command of 7 °/s. It is suitable for verifying guidance *correctness*; it cannot demonstrate "War Thunder-level" agility. Its 17.5° trim α also makes nose-vs-velocity semantics a first-order issue, not a detail.
5. **The F-16 path has the richer control law (load-factor command, α/g/roll limiters, turn compensation) but no authoritative thrust**, so it is not live-ready by its own gates.

---

## 2. What exists and should be preserved

| Capability | Where | Evidence | Why it matters to Instructor v2 |
|---|---|---|---|
| Aircraft-independent normalized intent | `Core/MavPilotCommand.cs` | REPO_VERIFIED | The only thing the guidance layer should output. |
| Command-source socket with declared vs live readiness | `Core/MavPilotCommandSource.cs:52-154` (`IsOperationalCommandSource`, `HasCommandSignal`, `SignalLossPolicy`, `ResolveOnSignalLoss`) | REPO_VERIFIED | A mouse source must declare itself and fail closed through this, not invent its own policy. |
| Signal-loss policy that never falls back to inspector values | `Core/MavFlightControlLawBase.cs` (`MavCommandResolution`, `StepControlLaw`) | REPO_VERIFIED | Camera loss / focus loss / invalid aim must route here. |
| Deterministic, ordered pipeline | `MavFlightControlLawBase` `[DefaultExecutionOrder(-300)]` (l.28), actuators −200 (`F15/MavF15ControlActuator.cs:33`, `F16/MavF16ControlActuator.cs:14`), `MavSixDoFBody` −100 (`Core/MavSixDoFBody.cs:9`) | REPO_VERIFIED | Guidance evaluated inside the law's poll sees exactly the state the law sees. |
| One-step sensor latency, documented | `FlightDynamics/README.md` "Runtime pipeline" | REPO_DOC | Guidance tests must include it (as V2 tuning did). |
| Single load-application boundary, duplicate/NaN refusal | `MavSixDoFBody` (`AddRelativeTorque` only at l.486), `MavFlightDynamicsLoadSet` | REPO_VERIFIED | Guarantees the instructor cannot add physics even by accident, if it stays a command source. |
| Gyroscopic (ω × Iω) compensation on by default | `Core/MavSixDoFBody.cs:30-48`, `MavGyroscopicMomentValidation` | REPO_VERIFIED | Inertial coupling in rapid rolls is modelled; guidance must not "fix" it. |
| Unity damping zeroed by profile | `Core/MavSixDoFBody.cs:634-636` | REPO_VERIFIED | No hidden PhysX damping in the FDM path (see §5.6 for `maxAngularVelocity`). |
| Ownership state machine with per-step grant re-check | `Core/MavFlightPhysicsOwnership.cs` (owners Legacy/Shadow/F16Replacement/Fault/F15AfitResearch, l.13-49); F-15 grant re-run every step (`F15_PILOT_CONTROLLED_V2.md` §8) | REPO_VERIFIED / REPO_DOC | Mouse source must not need a new owner or a widened grant. |
| Smooth, conservative limiter mathematics | `Core/MavControlLawProtections.cs` (`SmoothMin/Max`, `SoftSaturate`, `LoadFactorPitchRateCeilingRadSec`, `AngleOfAttackPitchRateCeilingRadSec`, `RollAuthorityFactorAtAlpha`, `StepWashout`, `StepIntegratorWithAntiWindup`, `DynamicPressureGainScale`) | REPO_VERIFIED | Reuse for guidance-side soft caps instead of writing new clamps. |
| Attitude reference with near-vertical flag | `Core/MavAttitudeMath.cs:18-35` (`bankAngleRad`, `flightPathAngleRad`, `nearVerticalSingularity`, `flightPathValid`) | REPO_VERIFIED | Telemetry; but guidance should avoid Euler angles (§4, doc 03). |
| Flight state with world velocity, α, β, rates, specific force | `Core/MavFlightDynamicsTypes.cs:33-89` | REPO_VERIFIED | Everything the proposed guidance needs is already published. |
| F-16 load-factor command with bank-aware turn compensation | `F16/MavF16ControlLawV01.cs:604-661` (`q = g (n − cos φ)/V`) | REPO_VERIFIED | The correct form of the level-turn relation; reusable for an n-command interface. |
| F-15 V2 stability-axis roll and yaw-rate damper, ARI | `F15/MavF15PilotControlLawV2.cs:266-290`; `Docs/Reference/F15_PILOT_CONTROLLED_V2.md` §3, §5.3 | REPO_VERIFIED / REPO_DOC | Rolling about the velocity vector is exactly what bank-to-turn guidance needs. |
| Fail-closed in-flight law switch | `F15/MavF15PilotControlledRig.cs:345-360`, V2 doc §8.1 | REPO_VERIFIED | Source switching (keyboard ↔ mouse) should follow the same rules. |
| Deterministic validation culture | Baseline v1 runner (1585/0), V2 headless 54/0 and Play Mode 57/0 byte-identical (`F15_PILOT_CONTROLLED_V2.md` §7) | REPO_DOC | Instructor metrics can be made exact diffs (`captureFramerate`). |
| NASA 836 F-15B FCS *structure* (no gains) | `Docs/Reference/F15_836_FCS_STRUCTURE_V1.0.md`; `F15/MavF15Nasa836FcsStructure.cs` | REPO_DOC (sources: NASA/TM-2009-214651, TM-2012-215978) | Public evidence that a production-family F-15 CAS has an α/q stall inhibitor, roll-rate feedback with α/airspeed-scheduled limiter, ARI and an α×p roll-yaw crossfeed. |
| Aerospace source library | `Docs/Research/Aerospace/**` (F-16 FCS source graph, F-22 feasibility) | REPO_DOC | Already establishes that no production F-16/F-22 FCS gains are publicly located. |

---

## 3. The legacy instructor — what it does and why it does not transfer

`Assets/MaverickFresh/Scripts/MavInstructorController.cs` (1,949 lines) with `MavMouseFlightRig.cs` (767 lines).

### 3.1 Algorithm as implemented (REPO_VERIFIED)

`RunAutopilot` (l.1649-1733), MouseFlight-derived:

```text
localFlyTarget = normalize(InverseTransformPoint(aimPoint)) * sensitivity     // body-frame target
angleOffTarget = angle(transform.forward, aimPoint - position)                 // NOSE-based error
yaw   = clamp(localFlyTarget.x) * yawGain (* 0.42 if preferBankTurnOverYaw)
pitch = -clamp(localFlyTarget.y) * pitchGain  (+ screen-Y "comfort" blend)  + noseDownTrim
roll  = lerp(wingsLevelRoll = transform.right.y,
             aggressiveRoll = clamp(localFlyTarget.x),
             InverseLerp(0, aggressiveTurnAngle, angleOffTarget)) * rollGain
```

Then (`UpdateControls`, l.1176-1345): keyboard overrides, coordinated-yaw assist from estimated sideslip (l.1347-1383), `ApplyProtectionAssists` (low-speed nose-down, AoA limiter; l.1402), `ApplyStabilizer` (l.1581-1647: wings-level, pitch-to-horizon recovery, nose-high push, rate damping from `rb.angularVelocity`), `ApplyEnvelopeProtectionGuards` (l.1505-1579: high-speed pitch scaling, α/β soft guard), then exponential smoothing with `Time.deltaTime`.

The aim point (`MavMouseFlightRig.MouseAimPos`, l.159-169) is a point 850 m along a `mouseAim` transform that rides with the aircraft. Two aim modes: cursor-viewport ray through the camera (l.508-533) or raw mouse delta rotating `mouseAim` about the camera's right/up axes (l.599-607). The camera itself looks toward `MouseAimPos` (l.363).

### 3.2 Findings

| # | Finding | Class | Evidence | Consequence |
|---|---|---|---|---|
| L1 | **Command semantics mismatch.** Legacy outputs were tuned as normalized torque demands for `MavMouseFlightJet`, which multiplies them by its own torque/authority factors and applies `AddRelativeTorque` (`MavMouseFlightJet.cs:1165`) plus velocity alignment and side-slip accelerations (l.1073-1277). The FDM laws treat the same numbers as rate / load-factor / sideslip commands. | REPO_VERIFIED (code), ENGINEERING_INFERENCE (consequence) | Instructor `CopyTuningFromJet` (l.568), turn-band "torque factors" (l.1855-1919) | Every legacy gain is calibrated against the wrong plant. Bridging is not a tuning problem; the gains are meaningless for the new laws. |
| L2 | **Bridge passes legacy values unchanged.** `MavMouseInstructorPilotCommandSource.TryGetCommand` copies `pitch` (sign-flipped), `roll`, `yaw` (l.56-58). | REPO_VERIFIED | `Core/MavMouseInstructorPilotCommandSource.cs` | L1 reaches the control law directly. Only used by `F16/MavF16PoweredReferenceShadowSmoke.cs` today. |
| L3 | **Constant nose-down bias.** `outPitch = clamp(autoPitchRaw + noseDownTrim, …)` with `noseDownTrim = 0.115` (l.27). | REPO_VERIFIED | l.1687 | Through the bridge, a permanent 11.5 % forward stick. On the F-16 law (neutral = level-turn n, full forward = −3 g, `F16/MavF16ControlLawV01.cs:115`) that commands ≈ 0.54 g wings-level instead of 1 g; on F-15 V2 it is −0.8 °/s pitch rate. A trim error, not intent. |
| L4 | **Aim-opposing recovery terms.** Stabilizer adds wings-level (`transform.right.y`), pitch-to-horizon (`-signedPitchAngle/55`) and nose-high pitch-down signals (l.1609-1630) whenever the stick is not overridden. | REPO_VERIFIED | l.1581-1647 | The aircraft is pulled toward level flight against the mouse target. Acceptable as legacy arcade feel; contradicts "target direction is intent". |
| L5 | **Render-rate control loop.** All of the above runs in `Update` (l.459-466) using `Time.deltaTime` and `rb.angularVelocity` sampled at render rate; the FDM law reads the snapshot at FixedUpdate. | REPO_VERIFIED | l.459, l.1312-1319 | Rate-damping terms close a loop at frame rate on a physics state that only changes at 50 Hz; behaviour depends on FPS. Not deterministic under the Baseline-v1 discipline. |
| L6 | **Roll command is proportional to the lateral component of the body-frame target, not to the bank needed to put lift on the target.** | REPO_VERIFIED | l.1696-1731 | For a target 90° off in the vertical plane, x ≈ 0 → no roll; the aircraft pitches. For a target directly behind, x ≈ 0 and y ≈ 0 → command ≈ 0 except smoothing residue: Scenario C has no defined behaviour. |
| L7 | **Nose-based error, velocity vector ignored.** `angleOffTarget` uses `transform.forward`; `VelocityVectorPos` exists (rig l.171-185) but guidance does not use it. | REPO_VERIFIED | | At high α the trajectory and the nose differ by α (17.5° at the F-15 research trim). |
| L8 | **Euler-style bank/pitch angles degenerate near vertical.** `SignedBankAngle` returns 0 when the projected up vector is tiny (l.1921-1926); same for `SignedPitchAngle`. | REPO_VERIFIED | | Discontinuity in recovery/bank-hold terms when the nose passes through vertical (Scenario F). |
| L9 | **Cursor-ray aim is camera-coupled while the camera looks at the aim.** | REPO_VERIFIED (structure), HYPOTHESIS (effect) | rig l.363, l.508-533 | With the cursor held off-centre the aim direction can keep moving because the camera that defines the ray keeps turning toward it. Possibly intended ("hold to keep turning"); it is not a stationary target and must be documented if reused. |
| L10 | **Keyboard command shaping uses `Time.unscaledDeltaTime` in `Update`.** | REPO_VERIFIED | `Core/MavKeyboardPilotCommandSource.cs` `Update` | Same frame-rate sampling issue in the new path's own source; ignores `timeScale`. Minor; worth matching when the mouse source is written. |
| L11 | **Throttle conversion is F-16-specific but lives in `Core`.** `ConvertThrottlePercentToFdm` hard-codes the Garza/Morelli military breakpoint 0.77. | REPO_VERIFIED | bridge l.71-83 | Aircraft-specific data in an aircraft-independent layer; wrong for the F-15 (fixed research thrust). |

**What to keep from the legacy instructor (design ideas, not code):** a persistent world-space aim direction decoupled from aircraft attitude (delta mode); free-look that freezes the aim (`frozenDirection`, rig l.314-325); the idea that small errors should not produce large roll (`aggressiveTurnAngle`); the debug telemetry vocabulary (angle off target, target bank, instructor state).

---

## 4. The new control laws as the instructor's plant

### 4.1 Stick semantics (REPO_VERIFIED)

| Axis | F-15 V2 (`MavF15PilotControlLawV2`, gains `MavF15PilotControlGainsV2`) | F-16 V0.1 (`MavF16ControlLawV01`, defaults l.113-136) |
|---|---|---|
| Pitch | pitch-rate command, **7 °/s** at full stick, feed-forward + feedback | **load-factor** command (`LoadFactorDemand`), 9 g at full aft, neutral = level-turn n (bank-compensated) |
| Roll | **stability-axis** roll-rate command `p cos α + r sin α`, **15 °/s** full stick | **body-axis** roll-rate command, **270 °/s** full stick, α-scheduled authority fade |
| Yaw | sideslip command, **1.5°** full pedal; ARI; washed-out stability-axis yaw-rate damper (labelled ASSIST) | sideslip command, **8°** full pedal; ARI; washed-out **body-axis** yaw-rate damper |
| Protections | **none** (V2 doc §6) | α limiter (25°/−5°), g limiter (+9/−3), roll-rate limiter — all labelled Maverick tuning (`Core/MavControlLawProtections.cs:14-104`) |
| Gain schedule | q̄, reference 2,745 Pa, clamp [0.5, 2] | q̄, reference 24,000 Pa |

Every value above is `MAVERICK_TUNED_NON_AUTHORITATIVE` in its own documentation.

### 4.2 Findings

| # | Finding | Class | Consequence for the instructor |
|---|---|---|---|
| C1 | **No machine-readable stick-semantics descriptor.** A guidance layer cannot ask a law "what does +1 pitch mean?". | REPO_VERIFIED (absence) | Guidance that wants to command "bank to 60°, pull 4 g" must hard-code per-aircraft conversions or emit stick values whose physical meaning it does not know. **This is the main missing seam.** |
| C2 | **F-16 law rolls about the body axis; its yaw damper washes out body-axis yaw rate.** At α > 0 a body-axis roll converts α into β (kinematic coupling); in a coordinated velocity-vector roll the body yaw rate is non-zero (`r = p tan α`) and the body-axis damper opposes it. | REPO_VERIFIED (code), ENGINEERING_INFERENCE (effect; the same mechanism was measured on the F-15 model, V2 doc §5.3) | Bank-to-turn guidance will see sideslip transients on the F-16 law at high α. Fixing it is a control-law change (outside instructor scope) and must be validated against the F-16 gates. |
| C3 | **F-15 V2 has no α, load-factor or roll protection.** The research aero fails closed outside the domain, which disarms the body rather than limiting the command. | REPO_DOC (V2 §6, §10) | A sustained mouse pull can drive the rig toward the domain edge; the instructor needs a guidance-side *domain* soft cap labelled as such (protects the model's validity, not an aircraft limit). |
| C4 | **F-15 V2 pitch command is rate, not load factor.** Kinematically, 7 °/s at 91.7 m/s corresponds to ≈1.5 g in a level turn (`ω = g√(n²−1)/V`). Whether the research aero can sustain that without leaving its domain is not established. | ENGINEERING_INFERENCE | A 90° heading change needs ≥ ~13 s of pull plus ~6 s to roll 90° at 15 °/s; a 180° reversal ≥ ~26 s. The rig is a correctness testbed, not an agility demonstrator. |
| C5 | **Trim α 17.5° on the F-15 rig.** | REPO_DOC (V1 §3) | If guidance steers the *velocity vector* onto the marker, the nose sits ~17.5° above it; if it steers the *nose*, the flight path passes ~17.5° below. The target semantics must be chosen explicitly (doc 06 §3). |
| C6 | **Instantaneous actuators on both rigs** (no sourced rate). | REPO_DOC | Guidance smoothing is the only rate limiting on the surface command path; PR #33 (actuator lag) was rejected for feel. Guidance must not rely on actuator lag to hide command discontinuities. |
| C7 | **F-15 rig source kinds are only `Keyboard` and `Scripted`.** | REPO_VERIFIED (`F15/MavF15PilotControlledRig.cs:5-13, 393-404`) | A mouse source needs an additive enum value and resolution branch; the ownership grant itself does not check the source type (no source reference in `F15/MavF15PilotControlledAuthority.cs`). |
| C8 | **Load-factor feedback exists.** `MavFlightState.specificForceAeroBodyG` / `LoadFactorNz` is published by the body. | REPO_VERIFIED | Guidance telemetry and n-command conversions can use measured Nz without computing forces. |

---

## 5. Physical ownership and double-application risks

| # | Risk | Status | Evidence | Mitigation already present / needed |
|---|---|---|---|---|
| O1 | Instructor writing Rigidbody, Transform or forces | Prevented if the instructor is a `MavPilotCommandSourceBase` | Source contract (`MavPilotCommandSource.cs:39-40`), V2 `[W11]` source scan | Extend the source scan to the new guidance files (as `[W11]` does for V2). |
| O2 | Legacy `MavMouseFlightJet` + new FDM on the same Rigidbody | Detected by type-name deny-list every armed step | `FlightDynamics/README.md` "Readiness" | Keep the new source independent of `MavInstructorController` so the legacy component can be absent. |
| O3 | Two control laws enabled | Refused by the F-15 grant each step | V2 doc §8 | No change. |
| O4 | Two command producers (keyboard + mouse) both "live" | **Not covered**: readiness checks one bound source; a second source on the same GameObject is ignored by `GetComponent` order. | `MavFlightControlLawBase.ResolvePipeline` resolves `GetComponent<MavPilotCommandSourceBase>()` when unset | Bind explicitly (the rig does); add a test that exactly one source is bound and a second enabled source is reported. |
| O5 | Guidance-side "assist" that is really a physical effect (e.g. auto-throttle, alignment) | Policy only | Handoff §2 | Keep throttle out of guidance; fixed research thrust stays fixed. |
| O6 | `Rigidbody.maxAngularVelocity` acting as a hidden limiter | **Low risk, not asserted**: no FDM file sets it, so the Unity 6000.3 default of 50 rad/s applies (doc 04 §4; it was 7 rad/s in 2021.3). Legacy code sets 5.4–8 rad/s on catalogue aircraft (`Aircraft/MavAircraftCatalog.cs:250-512`, `AI/MavEnemyF15Spawner.cs:90`) — relevant only if a legacy profile is applied to an FDM rig | REPO_VERIFIED (absence in FlightDynamics) + PV (Unity docs) | Set and assert the FDM rig's own value so behaviour does not depend on the Unity version or a legacy profile. |
| O7 | Gyroscopic compensation applied twice | Refused (one inertial correction per step) | `MavSixDoFBody.cs:1155-1169` | No change. |

---

## 6. Model validity envelopes the instructor must respect

| Aircraft path | Aerodynamic domain | Propulsion | Atmosphere | Source |
|---|---|---|---|---|
| F-15 pilot rig | 218.5–699.7 ft/s at fixed density; α −4…90° transcribed, \|β\| ≤ 20°; tuned at one trim; extrapolated away from M 0.6 | fixed 8,300 lbf, throttle inactive | fixed 20,000-ft density, no altitude state | `F15_PILOT_CONTROLLED_V1.md` §1, V2 §10 |
| F-16 reference | Morelli compact polynomial: α −10…45°, β ±30°, δe ±25°, δa ±21.5°, δr ±30°, M < 0.6, gear up, no stores, out of ground effect | power-state dynamics sourced; **dimensional thrust 0 N** | ISA | `FlightDynamics/README.md` |

Consequences (ENGINEERING_INFERENCE):
- Neither model supports claims about transonic/supersonic handling, post-stall, spin or ground effect. Guidance must not be "validated" there.
- The F-15 rig has no altitude-dependent density, so climbing/diving changes speed but not q̄ per altitude; energy trends are only partially physical (potential/kinetic exchange is real; density effects are absent).
- The F-16 cannot sustain powered flight live until a thrust deck is frozen; instructor work on the F-16 is limited to shadow/unpowered tests.

---

## 7. Opportunities that need no FDM change

| # | Opportunity | Reuses | New code location (proposal) |
|---|---|---|---|
| G1 | Pure, deterministic aim-guidance function (aim direction + published flight state → physical intent: bank / roll-rate, load factor or pitch rate, sideslip) | `MavFlightState`, `MavControlLawProtections` | `FlightDynamics/Core/` (aircraft-independent) |
| G2 | Stick-semantics descriptor per law (read-only, derived from the law's existing gains, not new tuning) | `MavF15PilotControlGainsV2`, `MavF16ControlLawGains` | additive interface implemented by each law, or a static per-law adapter |
| G3 | Mouse aim source: delta-integrated world aim direction, sampled in `Update`, consumed at the law's poll | `MavPilotCommandSourceBase`, `MavFreshInput` | `FlightDynamics/Core/` |
| G4 | Instructor telemetry (angle off target, bank error, commanded vs measured p_s/q/Nz, saturation, energy) | `MavFlightDynamicsTelemetry` | additive fields |
| G5 | Deterministic scenario suite A–G with metrics (doc 06 §8) | Play Mode runner pattern of V2 | `FlightDynamics/Validation/` |

G2 is the item most likely to be missed by an implementation that starts from the legacy instructor.

---

## 8. Verified defects vs hypotheses vs improvements

| Type | Items |
|---|---|
| **Verified defects (relative to the v2 product definition)** | L1, L2, L3, L5, L6 (no behaviour defined for target behind), L8, L11 |
| **Verified design limitations of current laws** | C1, C3, C4 (as a capability limit), C6 |
| **Hypotheses needing a test** | L9 (camera coupling drift), C2 effect size on the F-16; O6 is low risk with the Unity 6000.3 default but unasserted |
| **Improvements (not defects)** | G1–G5 |

Nothing here shows a defect in the physics layer (`MavSixDoFBody`, load set, aero models). The gaps are all above it.
