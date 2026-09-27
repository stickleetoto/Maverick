# F-15 Pilot-Controlled Research Aircraft V1

> **A NEW PHASE, above the frozen research baseline.** The F-15 AFIT/BAUMANN/DAVISON RESEARCH BASELINE V1 stays **FROZEN** (`F15_RESEARCH_BASELINE_FREEZE_V1.0.md`); none of its physics, data or results changed.
>
> This aircraft is the frozen research model flown by a human through a **Maverick-owned control approximation**:
> - **NOT** NASA F-15B 836;
> - **NOT** the production F-15 flight-control system (no F-15 CAS, FLCS or NASA 836 control law);
> - **NOT** source-authoritative actuator behaviour.

**Configuration ID:** `F15_AFIT_BAUMANN_DAVISON_PILOT_CONTROLLED_V1`.
It is separate from the frozen research ID `F15_AFIT_BAUMANN_DAVISON_MACH06_20K_RESEARCH`, which stays **validation-only**. Neither runtime authority grants the other's ID (`[P2]`).

---

## 1. SOURCE PHYSICS — unchanged, taken from the frozen research model

| Item | Value | Where it comes from |
|---|---|---|
| Aerodynamics | Davison App. C six-axis coefficient fits (CMMQ as printed) | `MavF15BaumannMach06Longitudinal` / `…LateralDirectional`, called unchanged by `MavF15PilotControlledAeroModel` |
| Reference geometry | 608 ft² / 42.8 ft / 15.94 ft (ARO10) | `MavF15BaumannMach06Reference` |
| Mass / inertia | 37,000 lb; 25,480 / 166,620 / 186,930 / −1,000 slug·ft² | `MavF15AfitResearchMassReference` |
| Thrust | fixed 8,300 lbf TOTAL + 0.25-in nose-up thrust-line moment | `MavF15AfitResearchThrustSource` constants; **THROTTLE INACTIVE — FIXED RESEARCH THRUST** |
| Density / gravity | source RHO 0.6531396 kg/m³ at every state (no altitude state); source G 9.8066352 m/s² through the load set | `MavF15AfitResearchRuntimeEnvironment`; mandatory, no fallback |
| Domain | SourceReproduction: 218.5–699.7 ft/s at the fixed density; transcribed α −4…90°, \|β\| ≤ 20° | `MavF15ResearchConditionGate`, `MavF15BaumannMach06Domain` |

**Why wrapper components instead of reusing the research components.**
- The frozen `MavF15AeroModel`, `MavF15AfitResearchFixedThrust` and research environment are keyed ordinally to the research ID.
- `MavF15AeroModel.cs` is hashed by the frozen `[U14]` test.
- Changing any of them would change the frozen baseline. The pilot aircraft therefore has thin wrappers that call the same routines and constants:
  - `MavF15PilotControlledAeroModel`
  - `MavF15PilotControlledFixedThrust`
  - `MavF15PilotControlledFlightDynamicsProfile`
- **Proven identical:**
  - coefficients equal `MavF15AeroModel`'s bit for bit (72/72 states, `[P8]`);
  - environment and thrust equal the research path bit for bit (`[P7]`);
  - in Play Mode, at centred stick, the pilot aircraft flies **the same trajectory, bit for bit, as the frozen research path** over 20 s (`[H]`).

---

## 2. MAVERICK CONTROL APPROXIMATION — every value non-authoritative

### Command path
`MavPilotCommand` (pitch/roll/yaw ±1, throttle) → `MavF15PilotControlLaw` → requested F-15 surfaces → `MavF15ControlActuator` (the single actual-surface owner) → `MavF15ActualSurfaceState` → research aero → `MavFlightDynamicsLoadSet` → `MavSixDoFBody` (the single load application).

- The law is a direct, memoryless mapping (`MavF15PilotControlMapping`), with no feedback, protection or augmentation:

  ```
  symmetric stabilator = trim bias + NoseUpStabilatorSign x pitch x 10 deg
  aileron              = RollRightAileronSign x roll x 20 deg
  differential stab.   = 0.3 x commanded aileron
  rudder               = YawNoseRightRudderSign x yaw x 15 deg
  ```

- Each channel is then clamped to its gameplay envelope.
- Neutral stick returns exactly the trim bias and zero lateral surfaces.

### Values and their provenance

| Value | Number | Provenance |
|---|---|---|
| pitch gearing | 10° stabilator per unit | **MAVERICK_TUNED_NON_AUTHORITATIVE** |
| roll gearing | 20° aileron per unit | **MAVERICK_TUNED_NON_AUTHORITATIVE** |
| yaw gearing | 15° rudder per unit | **MAVERICK_TUNED_NON_AUTHORITATIVE** |
| symmetric-stabilator command envelope | −25 … −5° | **MAVERICK_TUNED_NON_AUTHORITATIVE** — chosen to stay inside the setting region the source exercised in its tabulated equilibria. A gameplay **command bound**, NOT a hard stop, NOT sourced travel. `MavF15ResearchDemonstratedControlRange` is unchanged and still `IsPhysicalLimit = false` (`[P3]`) |
| aileron command envelope | ±20° | **MAVERICK_TUNED_NON_AUTHORITATIVE** — inside the region audited monotonic and finite (§4). Table VII exercised 0° only |
| differential-stabilator envelope | ±6° | **MAVERICK_TUNED_NON_AUTHORITATIVE** (0.3 × the aileron envelope) |
| rudder command envelope | ±15° | **MAVERICK_TUNED_NON_AUTHORITATIVE** — inside the audited region. Table VII exercised 0°; the flat-spin tables about −3…+14° |
| actuator travel | = the envelopes | graded `MavEngineDataProvenance.MaverickTuning`, labelled. **No actuator rate** (Unavailable): no actuator dynamics are invented, so surfaces step to the command |
| keyboard input shaping | rise 2.5 /s, return 4 /s | Maverick input choice (`MavKeyboardPilotCommandSource`), not aircraft data |
| **differential tail = 0.3 × commanded aileron** | 0.3 | **SOURCED research-model relation**: Davison ADA256613 driver PDF p.97, `F(12) = 20.*(.3*CDAILD-DDTD)` |
| **surface sign conventions** | stab + = leading edge up (nose-down); rudder + = trailing edge left; aileron + = right aileron trailing edge down (as printed) | **SOURCED** (Baumann ADA217366 PDF p.86), and checked against the coefficient derivatives (§4) |
| **trim start** | Table VII point 36 | **VALIDATED research equilibrium** (§3) |

None of these values is stored in `MavF15ResearchDemonstratedControlRange`, `MavF15PhysicalSurfaceHardStops` or `MavF15ActuatorRateLimits`:
- the exact hard stops and rate limits still declare nothing;
- the research profile still carries zero travel (`[P3]`).

**Stabilization: NONE added (V1).**
- Direct control is stable and controllable at the trim; every research-model mode decays (§3).
- The Dutch roll is lightly damped (ζ 0.067) and roll response is slow (roll mode −0.47 /s). These are the research model's own dynamics, flown as they are.
- A "MAVERICK GAMEPLAY / RESEARCH ASSIST" would be the place for a yaw damper, but none was needed to meet V1. Its absence is deliberate.

---

## 3. Trim / start state

**Table VII point 36:**
- stabilator −15.1452856°, α 17.4864559°, θ 14.5460234°;
- V 300.8 ft/s (91.7 m/s), β = p = q = r = φ = 0;
- geometric start altitude 6,096 m.

It is the WP-3B symmetric equilibrium:
- **bit-identical** to a fresh trim-solver solve;
- source residual 9.1e-8;
- **STABLE** — largest real part −2.1e-2 /s (`[P6]`);
- modes: short period ζ 0.37, phugoid ζ 0.15, Dutch roll ζ 0.067, roll −0.47 /s, spiral −0.16 /s.

It is also the equilibrium the frozen closeout held on the Rigidbody.

The stabilator value is the **pilot-neutral trim bias**: trim, not a physical neutral surface position.

The trim is a stored, validated constant. Runtime code may not name the research trim solver (a frozen source scan forbids it), so `[P6]` re-solves it and checks.

---

## 4. Coefficient audit behind the envelopes (`[P4]`, research routines, β 0, zero rates)

| α (deg) | dCm/dstab (/deg) | dCl/dδa, with 0.3 diff (/deg) | dCn/dδr (/deg) |
|---:|---|---|---|
| 8 | −1.17e-2 … −6.98e-3 | +7.72e-4 | −1.22e-3 … −9.64e-4 |
| 12 | −1.14e-2 … −6.68e-3 | +7.23e-4 | −1.14e-3 … −9.04e-4 |
| 17.49 (trim) | −1.10e-2 … −6.28e-3 | +6.22e-4 | −9.96e-4 … −7.75e-4 |
| 20 | −1.08e-2 … −6.10e-3 | +5.71e-4 | −9.08e-4 … −6.95e-4 |
| 25 | −1.04e-2 … −5.73e-3 | +4.82e-4 | −7.09e-4 … −5.11e-4 |

- Over each whole envelope, at every audited α (8–25°), each control moment is **finite and strictly monotonic** with the expected sign.
- The mapping signs are therefore the model's:
  - nose-up → negative stabilator;
  - right roll → positive aileron;
  - nose-right → negative rudder.

**Recorded observations, not judged.**
- The transcribed CL_δa is positive, so positive aileron rolls right in the flown model. Baumann's printed physical convention says "right aileron trailing edge down", which on the usual physical reasoning would roll left. The mapping follows the model that is flown.
- Aileron yaw (Cn from +10° aileron) is proverse below α ≈ 21° and adverse above.
- Rudder roll (Cl from +10° rudder) is positive throughout.

---

## 5. Flight envelope and diagnostics (`MavF15PilotControlledDiagnostics`, shown by the HUD)

**Shown values:**
- current V (ft/s and m/s), α, β, p, q, r;
- geometric altitude — reported only, because the model flies the source's fixed 20,000-ft density;
- source-fit status (at M 0.6 or not);
- source-reproduction status (ADMITTED, or REFUSED with the reason);
- coefficients EXTRAPOLATED from the M 0.6 fit;
- inside the source-exercised speed range and the transcribed α/β span;
- gameplay control active (command resolution);
- envelope limiting;
- actual surfaces;
- whether the surfaces **exceed the source-exercised inputs** (Table VII: stab −25…−5, lateral 0);
- **THROTTLE INACTIVE — FIXED RESEARCH THRUST**.

**Outside the research domain** (V < 218.5 or > 699.7 ft/s, or α/β outside the span):
- the research aerodynamics and thrust **refuse** and the HUD says so;
- the body then carries only the source gravity;
- nothing is extrapolated beyond what the gates admit.

---

## 6. Ownership

New owner `MavFlightPhysicsOwner.F15PilotControlledResearch = 5`:
- replacement writes, legacy gated off, one gravity source (the replacement load set, `useGravity` off), exclusive;
- entered only through `TryEnterF15PilotControlledOwnership`, behind its own safety hold `allowF15PilotControlledOwnership` (**OFF by default**; the rig releases it on its own authority only).

**Entry requires all of:**
- the exact pilot ID;
- the source environment;
- no other armed body;
- quiet legacy writers;
- `MavF15PilotControlledOwnershipGrant`, which checks the rig is exactly the pilot stack:
  - the research aero with its opt-in, and no second aero model;
  - the pilot thrust;
  - `MaverickTuning`-graded actuator travel only;
  - the pilot law;
  - no research static hold or research validation component.

**While active:**
- it is re-verified every step, and a lost precondition is a **Fault** (disarm, no load);
- Shadow and F16Replacement activation are refused while it holds physics.

**Unchanged:**
- the research owner `F15AfitResearch`, its safety hold and its refusal strings;
- F16Replacement, Legacy, Shadow and Fault.

This is the one shared-core change (`Core/MavFlightPhysicsOwnership.cs`). It is additive; the frozen closeout reproduces byte for byte (§7).

---

## 7. Validation

| Gate | Result |
|---|---|
| `MavF15PilotControlValidation` (headless, `[P1]`–`[P12]`) | **49 / 0** |
| Play Mode flight test `MavF15PilotControlledFlightValidation` | **31 / 0**, two runs **byte-identical** |
| Frozen F-15 suites (17) | **757 / 0** — every numeric report line identical to the frozen run; only source-scan file counts differ (the new files are scanned too, and pass) |
| Frozen research Play Mode closeout | **39 / 0**, byte-identical to the report committed with the frozen baseline (`Data/F15/runtime_closeout/f15rt_playmode_report.txt`) |
| Official Baseline v1 gate (F-16/shared, incl. F-16 Play Mode) | **PASS — 1,585 / 0** over the 25 counted suites (22 synchronous + the F-16 reference-flight, scheduler and shared-propulsion Play Mode suites) and the 3 Python validators; physics delta NONE; the 4 new validation/editor surfaces are listed and hash-verified |
| compile | 374 runtime + 36 editor scripts, 0 errors |

### Play Mode results (dt 0.02, `captureFramerate` 50)

- **Neutral hold, 20 s:**
  - every state within 1.7e-7 of the trim;
  - surfaces exactly at the trim bias;
  - the **same trajectory bit for bit as the frozen research path** (research profile, `MavF15AeroModel`, static hold, research owner).
- **Human prefab**:
  - starts itself on its first physics step as the sole live owner;
  - with no key held, holds the trim within 1.6e-7 for 5 s;
  - carries its HUD and camera.
- **Pulses** (1 s, ±0.5), change over the pulse; surfaces moved with the stick and returned **exactly** to trim on release:

  | Command | Response |
  |---|---|
  | pitch + | q +0.0729 rad/s, θ +2.82° |
  | pitch − | q −0.0431 rad/s |
  | roll + | p +0.0401 rad/s, φ +4.12° |
  | roll − | p −0.0401 rad/s |
  | yaw + | r +0.0461 rad/s, β −0.54° |
  | yaw − | r −0.0462 rad/s |
  | pitch + roll (0.3, 0.3) | q +0.0423 and p +0.0048 rad/s |

- **Sequence** (neutral → pitch → neutral → roll → neutral → yaw → neutral, 24 s):
  - every pulse has the commanded sign;
  - after each release the surfaces sit at trim and the rate peak decays — q 0.073 → 0.009, p 0.141 → 0.072, r 0.048 → 0.012 rad/s (research-model natural dynamics);
  - the aircraft stays inside the research domain: α 16.6–20.8°, \|β\| ≤ 1.1°, V 293–306 ft/s, \|φ\| ≤ 14.6°.
- **Convergence** (dt, dt/2 against dt/4): every state's ratio is 3.00–3.14, which is first order (V dominant: 2.16e-2 → 6.86e-3 ft/s).
- **Mechanics, every run:**
  - one load application per step;
  - 0 duplicate refusals;
  - exactly 1 initial-state write (the trim start);
  - owner held;
  - one armed body;
  - Unity gravity off;
  - finite;
  - no teleport (\|Δx − v·dt\| ≤ 2.6e-4 m).
- **Fail-closed:** removing the pilot profile as the body's provider faults the owner before the next body step; the body is disarmed and no further load is applied.

---

## 8. How a human flies it

1. Open an empty scene.
2. Choose **Maverick / F-15 / Place Pilot-Controlled Research F-15 In Scene**. This instantiates `Assets/MaverickFresh/Prefabs/F15/F15_PilotControlledResearch_V1.prefab`. The menu **Build Pilot-Controlled Research F-15 Prefab** rebuilds it.
3. Press Play. The aircraft starts at the point-36 trim at 6,096 m and becomes the sole live owner.
4. Keys follow Maverick's keyboard convention:
   - **W/S** (or ↑/↓): pitch;
   - **A/D** (or ←/→): roll;
   - **Q/E**: yaw;
   - Shift/Ctrl: throttle — shown on the HUD, **inactive**;
   - **F1**: hide the HUD.

The prefab has a chase camera, an AudioListener and primitive-shape visuals (no colliders). It uses no existing scene, prefab, model or F-16 content.

**Headless:**

```
Unity.exe -batchmode -nographics -projectPath <project> -executeMethod MaverickFresh.FlightDynamics.EditorTools.MavF15PilotControlledFlightValidation.RunBatch -f15pcOut <report>
```

Pass no `-quit`. For the headless suite, run the batch adapter with `-fdmType MaverickFresh.FlightDynamics.Validation.MavF15PilotControlValidation -fdmMethod RunAll`.

---

## 9. Limitations (V1)

- **Not flown by a human in this work.** Headless and Play Mode tests fly the same rig with scripted normalized commands, and the prefab was shown to start and hold trim with its keyboard source idle. No human session was recorded, and "feel" is not validated.
- **Research domain only:**
  - the model is defined at the source's fixed 20,000-ft density, with no altitude effect;
  - coefficients away from M 0.6 are extrapolations of the fit;
  - outside 218.5–699.7 ft/s or the α/β span, aero and thrust refuse.
- **Slow, lightly damped lateral-directional dynamics** at the trim (roll mode −0.47 /s, Dutch roll ζ 0.067). These are research-model dynamics, with no assist.
- **Thrust is fixed**; the throttle is inactive. There is no F100 deck, no ground or landing-gear model, and no weapons, sensors or AI.
- **Keyboard only.** The mouse-instructor bridge is not wired to this rig.

---

## 10. Files

| File | Role |
|---|---|
| `F15/MavF15PilotControlledIdentity.cs` | ID and what is / is not claimed |
| `F15/MavF15PilotControlApproximation.cs` | `MavF15GameplayControlAuthority`, conventions, `MavF15PilotTrimStart`, `MavF15PilotControlMapping` |
| `F15/MavF15PilotControlledFlightDynamicsProfile.cs` | profile + mandatory source environment |
| `F15/MavF15PilotControlledAeroModel.cs` | research routines, SourceReproduction, fail-closed |
| `F15/MavF15PilotControlledFixedThrust.cs` | 8,300 lbf, throttle inactive |
| `F15/MavF15PilotControlledAuthority.cs` | authority + ownership grant |
| `F15/MavF15PilotControlLaw.cs` | the Maverick pilot-control law |
| `F15/MavF15PilotControlledDiagnostics.cs`, `F15/MavF15PilotControlledHud.cs` | §5 diagnostics and HUD |
| `F15/MavF15PilotControlledRig.cs` | the one rig: stack, trim start, ownership entry |
| `Core/MavKeyboardPilotCommandSource.cs` | aircraft-independent keyboard source |
| `Core/MavFlightPhysicsOwnership.cs` | + `F15PilotControlledResearch` owner (additive) |
| `Validation/MavF15PilotControlValidation.cs`, `Validation/MavF15PilotControlledFlightValidationRunner.cs`, `Editor/MavF15PilotControlledFlightValidation.cs` | validation |
| `Editor/MavF15PilotControlledPrefabBuilder.cs`, `Prefabs/F15/F15_PilotControlledResearch_V1.prefab` | the prefab |
| `Tools/fdm_validation_baseline_v1.json`, `Docs/Validation/FDM_VALIDATION_INVENTORY_V1.md` | 4 new validation/editor surfaces listed, not counted |
