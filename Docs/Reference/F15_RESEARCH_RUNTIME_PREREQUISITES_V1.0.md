# F-15 — Research Runtime Flight Prerequisites (V1.0, WP-4A)

> **RESEARCH CONFIGURATION ONLY** (`F15_AFIT_BAUMANN_DAVISON_MACH06_20K_RESEARCH`, the AFIT/Baumann/Davison model). This WP prepares the runtime architecture for a later research-only Rigidbody phase. **Nothing was flown:**
> - no PlayMode, no free trajectory;
> - no pilot controls, CAS gains, actuator rates or travel limits;
> - no coefficient, CMMQ, mass, thrust, F100 or NASA 836 change;
> - no scene or prefab touched; F15Replacement not enabled.
>
> Live takeover stays **OFF by default**.

**Headline.** Each blocker between the validated source model and a research Rigidbody phase now has a research-only, fail-closed runtime path. It is verified statically on the real `MavSixDoFBody`, through its shadow compute path.

| | Result |
|---|---|
| **Density (D11)** | Opt-in `SourceFixedDensity` on the research profile: the source's RHO = 0.0012673 slug/ft³ (**0.6531396 kg/m³**) at every state. It never touches `MavAtmosphereModel`, reaches no other body, and never falls back silently. |
| **Gravity** | Unity's project gravity is **+3.432e-4** off the source's G = 32.174 ft/s². Opt-in `SourceGravity` applies 9.8066352 m/s² through the load set, once, with `Rigidbody.useGravity` off. `Physics.gravity` is untouched. **Runtime equivalence needs density AND gravity** (measured, §9). |
| **Fixed thrust** | The WP-1 component already carries the exact source semantic. Verified on the body: 8,300 lbf TOTAL once, its 0.25-in moment once, throttle ignored, no F100, no NASA 836 path. |
| **Surfaces** | A source-defined **static** stabilator setting, held by the F-15 actuator (still the single owner). It is admitted only inside the demonstrated range. No travel, hard stop, rate, gearing or pilot mapping. |
| **Initialization** | All 170 WP-3B/WP-3C equilibria inject and read back through the body's own state builder within float precision (≤ 1.7e-7 rad, ≤ 1.1e-7 relative V). |
| **Static equivalence** | At all 170 injected equilibria, the body's own loads balance to **3.9e-6 g** in the source environment. That residual is the documented mass convention; removing it leaves **8.0e-7 g**. The density-only environment is off by 3.4e-4 g, the standard one by 1.75e-3 g. |
| **Contamination** | One authority grants exactly `F15_AFIT_BAUMANN_DAVISON_MACH06_20K_RESEARCH`. The NASA 836 id is refused by name, and every other id is refused. A tampered id makes the body apply nothing. |
| **Suite** | `MavF15ResearchRuntimePrerequisitesValidation` `[U1]`–`[U14]`: **54 / 0**. |

---

## 1. Density policy — D11

**Where it lives.** It is a field on the research profile, `MavF15AfitResearchFlightDynamicsProfile.densityPolicy`:
- `StandardAtmosphere` (default);
- `SourceFixedDensity`.

It is resolved every step through a new virtual on the shared provider base, `MavFlightDynamicsProfileProvider.ResolveEnvironment`.

**The default is unchanged by construction.** The base implementation returns the shared `MavAtmosphereModel` sample exactly as given, labelled standard atmosphere / Unity gravity. Every non-research provider (F-16, exact NASA 836) inherits it, and so does the research profile with its defaults. `[U1]`/`[U2]` check this bit for bit at 7 altitudes and on armed F-16 and research bodies.

**When the override is honoured** — all must hold:
- the research profile is the body's own provider, on the same GameObject;
- `MavF15ResearchRuntimeAuthority` grants the body: the built profile is valid and its id is exactly the research id (§7).

**What it changes** — only the `MavAtmosphereSample` the body builds its flight state from:
- **Density:** RHO in SI, `(float)(0.0012673 × 515.378818)` = 0.6531396 kg/m³. The conversion is lbf-to-N over ft⁴ in double, checked against lbm·g₀/ft⁴ to 1e-12.
- **Label:** the sample is the source's fixed air, labelled with the 20,000-ft altitude the source gives that density ("AIR DENSITY AT 20000 FT ALTITUDE").
- **Report-only fields:** temperature, pressure and speed of sound are the shared standard atmosphere's values at 6,096 m. The source computes no Mach; the research gate uses Mach only to flag the fit condition.
- **Altitude:** the body's geometric altitude is reported separately. It is **not a source state**, so the research gate's 20,000-ft altitude check is satisfied wherever the body is. That is correct because the density no longer follows altitude.

**No silent fallback.** If an override is requested but not granted, the provider returns a **REFUSED** environment. The body then computes and applies **nothing** that step (`debugRejectedEnvironmentSteps`); it never substitutes the standard atmosphere. In `[U3]`, a research body re-labelled with the NASA 836 id is armed and stepped: it applies no load.

**Diagnostics.** The density source is visible in three places:
- `MavSixDoFBody.debugEnvironment` (source, value, status);
- the research profile's `debugEnvironmentStatus`;
- the preparation report (§8).

**Evidence** (`[U4]`):
- **Exactness:** at geometric 0, 6,096 and 9,000 m the sample carries RHO exactly. The body's q is 0.5·RHO·V² bit for bit, and equals the source's QBARS/SREF to 3.1e-8.
- **Admission:** SourceReproduction admits research aero and thrust at all three altitudes.
- **Without the override:** ISA/RHO − 1 = −6.826e-4 at 6,096 m, as WP-3B recorded. At 9,000 m the unchanged altitude gate refuses.

**Correction.** `F15_RESEARCH_TRIM_SOLVER_V1.0.md` §10 gave RHO in SI as "0.653142 kg/m³". The correct value is **0.6531396** (0.0012673 × 515.378818). The trim solver itself works in slug/ft³ and was never affected.

## 2. Gravity policy

**Audit** (`[U5]`):
- **Project gravity:** `Physics.gravity` = (0, −9.81, 0).
- **Source:** G = 32.174 ft/s² = 9.8066352 m/s².
- **Gap:** relative **+3.432e-4** — half the size of the density gap, and material at the source's print precision.

**Decision:**
- **Mechanism:** research-only, configuration-gated `gravityPolicy = SourceGravity`, with the same gating as density.
- `MavSixDoFBody` applies m·G as the load set's separate **gravitational channel**, once, through the single application.
- `InitializePhysicsOwnership` turns `Rigidbody.useGravity` **off** for such a body.
- `Physics.gravity` is never changed; no other body is affected.

**Guards — never both, never neither:**
- **Duplicate gravity:** the environment owns gravity but `useGravity` was switched back on. The step is refused and nothing is applied.
- **Missing gravity:** gravity was handed to the load set at initialization, the environment no longer owns it, and `useGravity` is still off. The step is refused.

`[U5]` exercises both cases.

**What the accelerometer sees.** `totalForceAeroBodyN` stays the non-gravitational force, and the published specific force excludes gravity. `AppliedForceAeroBodyN` = total + gravity is handed to the Rigidbody. For every body without the channel, the two are bit-identical.

**Verified** (`[U5]`, a banked state at θ 10.05°, φ −41.05° — point 136's attitude):
- gravity in body axes = m·G·(−sinθ, cosθ sinφ, cosθ cosφ);
- one contribution; `useGravity` off; applied force = total + gravity.

**Answer to the brief:**
- **Runtime equivalence requires density + gravity.** With density only, every equilibrium is off by the gravity gap (3.41–3.42e-4 g); see §9.
- **Mass (recorded, not changed).** The research mass (WP-1) is 37,000 lb × 0.45359237 kg. The source's RMASS is 37000/32.174 slug, which is 1.5e-6 heavier. This is the remaining equilibrium residual (§9). WP-4A does not modify mass.

## 3. Fixed-thrust policy

The source semantic already lives in `MavF15AfitResearchFixedThrust` (WP-1): Davison's `THRUST=8300.`, "TOTAL A/C THRUST", added as THRUST/QBARS to CX and THRUST·(0.25/12)/(QBARS·CWING) to CMM. WP-4A keeps it unchanged (hash-protected in `[U14]`) and verifies it on the body (`[U6]`/`[U7]`):

| Property | Verified |
|---|---|
| **Total aircraft force** | 36,920.24 N = 8,300 lbf along body +X. 0 engines reported; the profile declares no installation and no engine count. |
| **Thrust once** | One propulsive contribution. The aero CX is the transcribed routine's bit for bit, with no thrust inside it. Total X = aero + thrust exactly. A second contribution is rejected and the set refuses application. |
| **Moment once** | One propulsive moment, 234.444 N·m nose-up = 8,300 × 0.25/12 ft·lbf. The aero Cm is the routine's bit for bit. External moment = aero + thrust; the gyroscopic term is separate. |
| **No throttle** | Throttle 1 and throttle 0 give the same force, and the ignored throttle is reported. |
| **No F100, no NASA 836 path** | No WP-4A runtime file names an F100 type, a thrust deck, the F-15 F100 propulsion system or a NASA 836 FCS/data type. No MavF100 source names a WP-4A type. |
| **Blockers** | Preparation blocks a second propulsion model on the aircraft (duplicate-thrust risk), and any model other than the research fixed thrust. |

The 8,300 lbf is a research-model constant at M 0.6 / 20,000 ft. It is **not** an F-15 engine rating, and it is non-authoritative.

## 4. Surface-state policy

**What is added:**
- **The setting:** `MavF15ResearchStaticSurfaceSetting` — a source-defined static stabilator for one known equilibrium, with aileron, differential tail and rudder at the demonstrated 0.
- **The holder:** `MavF15AfitResearchStaticSurfaceHold` holds it.
- **The owner:** the F-15 actuator (`MavF15ControlActuator`) remains the **only owner of actual surface state**. When a hold is on its GameObject:
  - **granted:** the actuator publishes the held state as the actual one and ignores every command;
  - **not granted:** it holds NEUTRAL and says why — never the command.

**What it is not** — each is false by construction, and `[U8]`/`[U9]` check it:

| Property | How it is enforced |
|---|---|
| **Not travel, not a hard stop** | The setting is one point, not an interval. With the hold engaged, the actuator still declares 0 travel channels and no hard stop, and the profile's shared control limits stay zero. |
| **Not a rate** | The type carries only `{symmetricStabilatorDeg, sourceNote}`. No channel has a sourced rate. The hold has no Update/FixedUpdate. |
| **Static** | The setting can be neither engaged nor released while the body is armed. |
| **No gearing, no pilot mapping** | F-15 and shared-contract commands (stab +12, aileron 20, rudder −30 …) leave the held surfaces bit-identical. |
| **Not physical** | The setting is admitted only inside `ResearchDemonstratedControlRange` (stabilator −25…−5°, edges included), whose `IsPhysicalLimit` stays false on all four channels. Values outside it, non-finite values, and an unnamed source are refused. |

**Relation to earlier types.** `MavF15ResearchStaticControlState` (WP-2) stays STATIC_EQUILIBRIUM_VALIDATION_ONLY and unchanged. WP-4A adds a separate type because the research **body**, not only a coefficient check, must sit at an equilibrium's stabilator. It can become the actuator's held state only on a body the research authority grants.

## 5. Initialization strategy

`MavF15AfitResearchStateInjection.TryInitialize(body, state)` takes a solved equilibrium:
- α, β, p, q, r, θ, φ, V and the stabilator, in the WP-3B/C/D physical units;
- a heading and an altitude, which are not source states (defaults 0 and 6,096 m).

**Validated before anything is applied:**
- research authority; an unarmed body; a hold and an F-15 actuator on it;
- finite states; V inside the source-exercised 218.5–699.7 ft/s;
- α/β inside the transcribed span; |θ|, |φ| ≤ 85°;
- a stabilator inside the demonstrated range.

**Applied, in order:**
1. The hold is engaged.
2. The pose and motion go through the body's own new initializer, `MavSixDoFBody.TryApplyInitialKinematicState`. It refuses while armed, while shadowing, without an ownership grant, on non-finite input, and for a non-unit rotation.
3. The actuator is stepped once with dt = 0.

The injection code writes no Rigidbody state itself.

**Frames:**
- **Rotation:** 3-2-1 Euler (ψ, θ, φ) → `Quaternion.Euler(−θ, ψ, −φ)`. Unity's +x rotation pitches nose-down and its +z rotation rolls left.
- **Velocity:** a true vector, (V cosα cosβ, V sinβ, V sinα cosβ) through `AeroBodyVectorToUnityLocal`.
- **Rates:** an axial vector, so (p, q, r) → Unity local (−q, r, −p), the inverse of `UnityLocalAngularRateToAeroBody`.

**Round trip** (`[U11]`, all 170 equilibria: 89 symmetric, 80 turning, the pitchfork):
- **Readback:** the body's own state builder reads back within float precision — α 8.1e-8, β 3.8e-8, θ 6.3e-8, φ 1.7e-7 rad; p/q/r 1.9e-8 rad/s; V 1.1e-7 relative; stabilator exact.
- **Preparation:** the report has no blocker at every one.
- **Nothing flown:** never armed, no load applied.

## 6. Load ownership

```
research aero (MavF15AeroModel, transcribed routine, no thrust)  ─┐
research fixed thrust (MavF15AfitResearchFixedThrust, once)     ─┼─► MavFlightDynamicsLoadSet ─► MavSixDoFBody ─► Rigidbody
source gravity (research env only; the load set's own channel)  ─┤      (one AddRelativeForce + one AddRelativeTorque)
gyroscopic compensation (existing)                               ─┘
```

- **One writer.** No research component writes the Rigidbody. The FDM ownership scan is clean over 157 files. The 6 WP-4A runtime files (1,291 lines) contain no force, torque, velocity, position, rotation or `useGravity` write, and no arming (`[U10]`).
- **The new body method.** `TryApplyInitialKinematicState` is the body's sanctioned initializer, in the already-exempt `MavSixDoFBody.cs`. It is initialization only and cannot become a per-step override: it is refused once armed.
- **No duplicates.** There is no duplicate aero, thrust, moment or gravity: every channel counts its contributions, and the set refuses application on any duplicate. The FCS writes no body state; the held surfaces reach the physics only as coefficients.

## 7. Contamination guards

`MavF15ResearchRuntimeAuthority` is the one gate. It grants exactly `F15_AFIT_BAUMANN_DAVISON_MACH06_20K_RESEARCH`, compared **ordinally**. Evidence from `[U3]`:
- **Exact NASA 836:** the id is refused **by name**.
- **Other ids:** 14 more are refused — null, empty, blank, `unconfigured`, the exact 836 profile id, the F-16 profile id, lower case, leading or trailing space, `_EXACT` / `_2` suffixes, a truncated id, and both mass-state ids.
- **On a body:** the body must fly the research profile — its provider is the research profile component, on the same GameObject, with a valid built profile carrying the id.
- **Exact-836 body with every research component beside it:** its provider resolves the standard environment. The research profile, asked directly, refuses. Hold, injection and preparation all refuse, and no initial state is applied.
- **Tampered id:** the environment is REFUSED, the armed step applies nothing, and the engaged hold falls back to NEUTRAL.

## 8. Source-condition diagnostics

`MavF15AfitResearchRuntimePreparation.Evaluate(body)` returns `MavF15ResearchRuntimePreparationReport`. It is built at initialization and changes nothing.

**Fields:**
- configuration id and authority;
- density source and value; gravity source and value;
- aero model; thrust semantic; surface semantic;
- source-fit condition and condition mode;
- current V (ft/s, m/s) and a report-only Mach;
- **outside the M 0.6 fit condition**, and **inside the source-exercised speed span**;
- the condition gate's verdict; altitude semantics; source equivalence with its gaps; blockers.

Example (`[U13]`, V 533.37 ft/s at geometric 7,000 m, SourceReproduction): prepared, source-equivalent, **outside** the fit condition, **inside** the span, gate admits, and the altitude is "NOT a source state".

**Leaving Mach 0.6 does not block by itself.** What blocks is what the research condition gate refuses, so the existing distinction is kept exactly:
- **StrictFitCondition:** the same state is blocked (M 0.6 only).
- **Either mode:** 720 ft/s is outside the span and blocked.

**Default policies.** The report states "not source-equivalent", with both gaps: q × 0.999317, and gravity +3.43e-4.

## 9. Static equivalence — the body's own loads at the source equilibria (`[U12]`)

**Method:**
- Every one of the 170 equilibria is injected. The body computes its full pipeline in **shadow** (compute everything, apply nothing): environment, aero, thrust, gravity, and the gyroscopic term.
- **Linear residual:** |F/m − ω×V| / G, with F including gravity.
- **Angular residual:** |M_ext − ω×(Iω)| / (q·S·c̄).
- A true equilibrium of the body gives zero for both.

| environment | linear residual | angular residual |
|---|---|---|
| source density + source gravity | **1.34e-6 … 3.93e-6 g** | 1.07e-7 |
| source density + Unity gravity | 3.41e-4 … 3.42e-4 g | 1.07e-7 |
| standard atmosphere + Unity gravity | 9.94e-4 … 1.75e-3 g | 2.57e-7 |

- **Source environment:** every injected equilibrium is an equilibrium of the runtime body's own loads, inside a 3.4e-5 band (NUMERICAL: 10× below the smallest gap).
- **The remaining 1.3–3.9e-6 g is explained.** It is the research mass convention (§2): the body's F/m exceeds the source's by 1.49e-6 of the non-gravitational acceleration, which grows with load factor (point 199, φ 63°). Removing that term leaves **8.0e-7 g**, which is float noise.
- **Density only:** off by the gravity gap everywhere. **Neither override:** up to 1.75e-3 g.

**Conclusion:** runtime equivalence requires **density + gravity**.

## 10. Tests

`MavF15ResearchRuntimePrerequisitesValidation` (`Validation/`, editor-only): **54 / 0**.

| check | covers |
|---|---|
| `[U1]` | default environment bit-identical (no provider, exact 836, F-16, research defaults; 7 altitudes; an armed research body) |
| `[U2]` | F-16: standard environment, applied = total bit for bit, aero load equals an independent evaluation bit for bit |
| `[U3]` | NASA 836 and non-research ids refused; exact-836 body; tampered id fails closed |
| `[U4]` | exact fixed density, q bit for bit and vs the source, altitude not a source state, the recorded ISA gap |
| `[U5]` | gravity audit, source gravity once through the load set, specific force excludes it, both ownership guards |
| `[U6]` / `[U7]` | thrust and thrust-line moment once; no throttle; no F100 / NASA 836 path |
| `[U8]` / `[U9]` | demonstrated range not physical; no travel, hard stop, rate or gearing; static only |
| `[U10]` | ownership scan; no Rigidbody write or arming in WP-4A files; the body initializer's refusals |
| `[U11]` | 170 equilibria round-trip |
| `[U12]` | static equivalence per environment |
| `[U13]` | diagnostics; the Strict / SourceReproduction distinction; live takeover off by default |
| `[U14]` | 40 protected datasets and sources byte-identical: the WP-3A–3E datasets, plus the source RHS, trim solver, coefficient routines, atmosphere, gate, fixed thrust, aero model, authority, mass, exact 836 and every MavF100 file. The 170 re-solved equilibria match the WP-3D dataset exactly; the atmosphere and `Physics.gravity` are unchanged. |

**Totals:**
- **F-15 suites:** 17 suites, **757 / 0** (the 16 existing ones 703 / 0, plus 54 / 0 new).
- **Existing F-15 reports:** textually identical to WP-3E except file-scan counts.
- **F-16 / shared regression:** the 22 synchronous FDM baseline suites give **1,247 / 0** both before (at `68952f3`, run in a separate worktree) and after WP-4A. Their reports are line-identical except for two things:
  - `fdm_phase2`'s file-scan count (150 → 157);
  - `shared_propulsion_unity`'s verbose-telemetry-ON allocation reading (497.66 bytes/step before; 491.52 and 458.75 in two runs after), which is background noise by that suite's own statement. Its telemetry-OFF reading is unchanged at 0 bytes/step, so the environment hook allocates nothing on the F-16 path.
- **Not run:** the 3 PlayMode baseline suites, because the brief forbids entering PlayMode.
- **Compile:** 359 runtime + 33 editor scripts, 0 errors.

## 11. Files

**New (runtime):**
- `Core/MavFlightEnvironment.cs`
- `F15/MavF15ResearchRuntimeAuthority.cs`
- `F15/MavF15AfitResearchRuntimeEnvironment.cs`
- `F15/MavF15AfitResearchStaticSurfaceHold.cs`
- `F15/MavF15AfitResearchStateInjection.cs`
- `F15/MavF15AfitResearchRuntimePreparation.cs`

**New (validation):** `Validation/MavF15ResearchRuntimePrerequisitesValidation.cs`

**Modified:**

| File | Change |
|---|---|
| `Core/MavSixDoFBody.cs` | environment resolution, the fail-closed environment refusal, the gravity channel and its guards, applied force, telemetry altitude under the override, `TryApplyInitialKinematicState` |
| `Core/MavFlightDynamicsLoadSet.cs` | gravitational channel; `AppliedForceAeroBodyN` |
| `Core/MavFlightDynamicsProfile.cs` | virtual `ResolveEnvironment` |
| `F15/MavF15AfitResearchFlightDynamicsProfile.cs` | the two opt-in policies |
| `F15/MavF15ControlActuator.cs` | static hold |

**Unchanged:**
- `MavAtmosphereModel`; the research condition gate; the aero model; the fixed thrust;
- the coefficient routines, CMMQ, mass and inertia; F100; NASA 836;
- the source RHS and trim solver; every dataset.

## 12. Remaining blockers before the first research PlayMode flight

> **RESOLVED by the final research runtime closeout** (`F15_RESEARCH_BASELINE_FREEZE_V1.0.md`). The research body was flown in Play Mode and the baseline is **FROZEN**. Per blocker:
> 1. **Ownership:** resolved. New owner `F15AfitResearch`, validation only, safety hold OFF by default, exact id, one armed body. The environment is re-checked every step, with Fault on loss.
> 2. **Harness:** resolved. `MavF15ResearchRuntimeFlightValidation` is deterministic, with byte-identical reruns.
> 3. **Integrator fidelity:** resolved. Unity − source RK4 is first order in dt (orders 0.95–1.08), and the holds stay on the equilibrium up to that error.
> 4. **Gyroscopic compensation with the research Ixz:** resolved. (ṗ, q̇, ṙ) matches the source RHS to 3e-7, a flipped Ixz would be 1.7e-2 away, and turning off the compensation gives 13× the departure.
> 5. **Physics settings:** recorded. Sleep 0.005, max angular speed 50, damping 0/0, `useGravity` off.
> 6. **Source environment:** mandatory. The owner refuses without it and faults on loss.
> 7. **Surfaces:** still static by design.
> 8. **Mass convention:** unchanged; recorded.
>
> The text below is the WP-4A record as written.

1. **Ownership authority.**
   - The ownership gate knows only `F16Replacement` as a replacement owner, and F15Replacement is not enabled (it must not be here).
   - A research owner state needs its own reviewed change, and an arming decision.
   - The research body reaches only STRUCTURALLY_PREPARED: its fixed thrust is non-authoritative by design, so operational live-readiness needs an explicit research acceptance.
   - The arming policy must refuse to arm a body whose preparation report has blockers. A refused environment makes the body apply no loads — but, as with any refused step, Unity's own gravity still acts if `useGravity` is on.
2. **A deterministic research PlayMode harness.**
   - Fixed dt (the project uses 0.02 s) and `Time.captureFramerate` pinned.
   - It injects an equilibrium (§5), arms only the research body, and records telemetry.
   - None exists yet.
3. **Integrator fidelity.**
   - PhysX integrates each fixed step with loads held constant; the source is a continuous ODE.
   - Before any stability claim, the Rigidbody trajectory must be compared with the WP-3E RK4 reference (`Data/F15/stability_time_domain/`) at dt, dt/2 and dt/4.
   - The slow modes (down to 1e-4 /s) and the unstable α 13–14° band will expose any drift.
4. **Dynamic check of the gyroscopic compensation with the research Ixz.** Statically the moment balance holds to 1.1e-7 at all 170 equilibria, including the ω×Iω term. Dynamically it has been measured only on the F-16 (Phase 5C-R).
5. **Physics settings to confirm on the research body.**
   - sleep threshold 0.005 (irrelevant at ~150 m/s, but unverified);
   - max angular speed 50 rad/s (the research rates are ≤ 0.2 rad/s);
   - the profile already zeroes damping.
6. **The environment must be the source one for any flight.**
   - Under `StandardAtmosphere`, the ±1 m altitude gate refuses as soon as a helical equilibrium climbs or descends. The Table VII turns are helical: there is no altitude state.
   - SourceFixedDensity + SourceGravity must therefore be mandatory for a research flight. The preparation report shows it; a future arming policy should require it.
7. **Surfaces are static.** Only open-loop flight from a known equilibrium is supported — which is exactly D17 stage 2, comparing against WP-3E. Research surface travel, rates, gearing and a pilot mapping remain unavailable and unsourced.
8. **Mass convention (recorded).** The residual is 1.5e-6, below every print floor. Changing mass is out of scope.

## 13. Not done (per the brief)

- **No flight or trajectory:** no PlayMode, no free trajectory, no handling tuning.
- **No control additions:** no pilot controls, CAS gains, actuator rates or travel limits.
- **No continuation.**
- **No model or path changes:** no coefficient, CMMQ, F100 or NASA 836 change.
- **No out-of-scope assets:** no scenes, prefabs, weapons, sensors or AI.
- **Not merged.**
