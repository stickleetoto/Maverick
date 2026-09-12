# Shared Propulsion — Implementation Report v0.1

Phase **P0 — shared propulsion profile + engine runtime foundation**, plus
**P0.1 — architecture hardening** (sections 13-24) and **P0.2 — Unity validation** (section 25).

Branch `sol/phase5-wip`, from `859e1db`. **Not committed.** `F16Replacement` remains disabled. No scene,
prefab, model, material, weapon, sensor or AI asset was touched.

**P0.1 changed three things P0 got wrong** and closed four gaps it left open. Sections 1-12 describe
P0 as built and are still accurate except where a P0.1 section supersedes them; the superseding
sections say so explicitly.

Companion architecture document: `SHARED_PROPULSION_PROFILE_ARCHITECTURE_V0.1.md`, which this
implements. That document remains the authority; this one records what was actually built.

---

## 1. Architecture implemented

```text
MavFlightDynamicsProfile
    propulsionInstallationId / declaredEngineCount    <- identity only, no runtime state
        |
MavPropulsionInstallationProfile                      <- N engine slots, count is DATA
        |
        +-- MavEngineInstallation  slot 0             <- position, thrust direction, throttle channel
        |       engineProfile ------------------+
        +-- MavEngineInstallation  slot 1       |     <- may point at the SAME profile object
                engineProfile ------------------+
                                                |
                                      MavEngineProfile        <- static data + provenance, NO state
                                                |
                                      MavEnginePowerDynamicsLaw
                                                |
                                      IMavEnginePowerDynamics  <- stateless strategy
                                                |
                                MavF16GarzaMorelliEngineDynamics (F-16 only)

MavPropulsionSystem : MavPropulsionModelBase
        one MavEngineRuntime per slot                 <- ALL mutable engine state lives here
        F_total = sum(F_i)
        M_total = sum(r_i x F_i + intrinsic_i)
        |
        v
MavSixDoFBody                                         <- unchanged; still the only load applier
```

### The one decision that shaped everything else

`MavPropulsionSystem` **is a** `MavPropulsionModelBase`. `MavSixDoFBody` already held exactly one
propulsion model and added exactly one propulsive contribution per step, so presenting N engines as one
model delivered multi-engine support with **zero changes to `MavSixDoFBody`** — the load-application
boundary, the duplicate-contribution guard, and the shadow-mode return point are all untouched.

The alternative — teaching `MavSixDoFBody` about engine arrays — would have put aggregation on both
sides of the ownership boundary, which is the thing this phase exists to prevent.

### Ownership, class by class

| Class | Owns | Explicitly does NOT own |
|---|---|---|
| `MavEngineProfile` | engine identity, provenance, law selection, deck reference, declared envelope, augmentation semantics, fuel-flow capability, rotor momentum | any mutable state; any installation geometry |
| `IMavEnginePowerDynamics` | the throttle→power law and the power transient, **stateless** | the power value itself |
| `MavEngineRuntime` | commanded power, actual power, failed flag, last result — **one instance per physical engine** | any Rigidbody; any aircraft-specific equation |
| `MavEngineInstallation` | slot id, engine reference, mount position, thrust direction, throttle channel, enabled, geometry provenance | engine data; runtime state |
| `MavPropulsionInstallationProfile` | the ordered set of slots | runtime state |
| `MavPropulsionSystem` | the runtime array, aggregation, per-engine and aggregate telemetry | any Rigidbody; any engine equation |
| `MavSixDoFBody` | **the single load-application boundary** — unchanged | — |

State exclusion from the profile is the load-bearing decision. Two F-15 engines share one
`MavEngineProfile` *object*; if spool state lived there they would share it, and a twin could not run
90% on one side and 40% on the other.

---

## 2. Files added

| File | Role |
|---|---|
| `Core/MavEngineProfile.cs` | engine profile, `MavEngineDataProvenance`, `MavEnginePowerDynamicsLaw`, `MavEngineAugmentationSemantics`, `MavEngineFuelFlowCapability`, `MavEngineRotorAngularMomentum`, `MavEngineSourceEnvelope` |
| `Core/MavEnginePowerDynamics.cs` | `IMavEnginePowerDynamics`, `MavInstantEnginePowerDynamics`, `MavEnginePowerDynamicsFactory` |
| `Core/MavEngineInstallation.cs` | `MavEngineInstallation`, `MavPropulsionInstallationProfile` |
| `Core/MavEngineRuntime.cs` | `MavEngineRuntime`, `MavEngineLoadResult` |
| `Core/MavPropulsionSystem.cs` | the N-engine aggregator |
| `F16/MavF16GarzaMorelliEngineDynamics.cs` | **canonical** Garza/Morelli equations |
| `F16/MavF16EngineLawRegistrar.cs` | registers the F-16 law with the shared factory |
| `F16/MavF16PropulsionInstallation.cs` | F-16 engine profile + single-slot installation |
| `F16/MavF16PropulsionSystem.cs` | the F-16's configured propulsion component |
| `F15/MavF15PropulsionSkeleton.cs` | twin-engine **shape only**, no engine data |
| `Validation/MavSharedPropulsionValidation.cs` | P-001 … P-024 |

**Added in P0.1:**

| File | Role |
|---|---|
| `Core/MavPropulsionCommand.cs` | `MavPropulsionCommand`, `MavPropulsionCommandMode`, `MavPropulsionCommandSourceBase` |
| `Editor/MavF16EngineLawRegistrarEditor.cs` | editor-assembly registration hook, so no runtime script names `UnityEditor` |

Plus `.meta` for each and a folder `.meta` for `FlightDynamics/F15`.

## 3. Files changed

| File | Change |
|---|---|
| `F16/MavF16EnginePowerModel.cs` | **TRANSITIONAL.** Its four sourced statics now forward to `MavF16GarzaMorelliEngineDynamics`. No equation, breakpoint or constant remains duplicated. |
| `Core/MavFlightDynamicsProfile.cs` | added `propulsionInstallationId`, `declaredEngineCount`, `MatchesPropulsionInstallation(...)`. Identity only. |
| `Editor/MavF16ReferenceValidationEditor.cs` | menu entry *Run Shared Propulsion Architecture Validation* |

**Changed in P0.1:**

| File | Change |
|---|---|
| `Core/MavEngineProfile.cs` | now a `ScriptableObject`; `CreateInMemory` factory (ENG-013) |
| `Core/MavEngineRuntime.cs` | commanded-cutoff overload; cutoff/failure are operational, not provenance |
| `Core/MavEngineInstallation.cs` | live-readiness hardened: identity, finite direction/position, declared geometry + provenance |
| `Core/MavEnginePowerDynamics.cs` | registration idempotent and countable; passing null clears |
| `Core/MavPropulsionSystem.cs` | command boundary replaces the channel side-door; provenance/operational split; explicit aggregate telemetry |
| `Core/MavFlightDynamicsTypes.cs` | `MavPropulsiveLoads` aggregate semantics defined; `powerStateSpread01` and `contributingEngineCount` added |
| `F16/MavF16EngineLawRegistrar.cs` | editor hook moved to the Editor assembly; `IsRegistered` asks the factory |
| `F16/MavF16PropulsionInstallation.cs` | profile built through `CreateInMemory` |
| `F15/MavF15PropulsionSkeleton.cs` | profile built through `CreateInMemory` |

`MavSixDoFBody`, `MavPropulsionModelBase`, `MavThrustDeck` and every gravity/aero/FLCS/protection path
remain **unmodified** across both phases. `MavPropulsiveLoads` gained two additive fields in P0.1; no
existing field changed meaning.

---

## 4. F-16 migration status

Migrated, with the old component retained as an adapter.

- The sourced Garza/Morelli throttle gearing, piecewise actual-power derivative and RTAU schedule are
  preserved **exactly**, and now exist in exactly one place.
- `MavF16PropulsionSystem` + `MavF16PropulsionInstallation` is the new path: one slot, thrust along
  body +X, position `(0,0,0)`, **`geometryDeclared = false`**.
- **No behaviour change.** With no deck attached, the new path produces exactly zero force and zero
  moment while advancing the same power state — bit-for-bit what the old component did. P-005m
  reproduces the sourced power trajectory step-for-step over 40 steps to 1e-4.

### What remains transitional

`MavF16EnginePowerModel` still exists and is still what `MavF16SelectionAutoSetup` installs. It is kept
deliberately:

- several existing suites (`MavF16PropulsionValidation`, Phase 3, integration, freeze-hardening,
  `MavF16TrimReference`) reference it directly;
- the F-16 is single-engine, so the two designs are numerically identical;
- swapping the auto-setup wiring changes what the live aircraft instantiates, and this phase was
  explicitly not allowed to change F-16 runtime behaviour.

It is now a **shim**: it holds no equations. Retiring it and repointing `MavF16SelectionAutoSetup` at
`MavF16PropulsionSystem` is the first task of the next phase.

### One honest gap recorded rather than papered over

The F-16 thrust-line offset is `(0,0,0)` with `geometryDeclared = false`. The old code assumed the
thrust line passed through the CG (ENG-003); this preserves that number while recording that it is an
assumption. A single engine on the centreline is a reasonable expectation — but a reasonable
expectation is not a source, and declaring the geometry measured would assert a measurement nobody made.

---

## 5. F-15 multi-engine readiness

**The architecture is ready. The engine data is not, and nothing pretends otherwise.**

Proven by P-013: two slots, one shared `MavEngineProfile` object, two independent `MavEngineRuntime`
instances, separate throttle channels.

Deliberately absent from `MavF15PropulsionSkeleton`:

| Item | State | Why |
|---|---|---|
| engine variant | `Unavailable` | "F100" is not one numeric engine (ENG-006). PW-100/220/229, EMD and DEEC research configurations are distinct data sets. |
| power law | `InstantNoSourcedTransient` | **Does NOT borrow the F-16 law.** Garza/Morelli is F-16 reference-engine behaviour; applying it to an F100-family engine would be an unproven claim. |
| thrust deck | `null` | zero thrust |
| mount coordinates | `Vector3.zero`, `geometryDeclared = false` | a plausible spacing would be a guessed aircraft dimension |

The zero mount offsets have a useful consequence: an engine-out condition on the skeleton produces
**no** yawing moment, which is obviously wrong and therefore cannot be mistaken for a working F-15.
The asymmetric-thrust behaviour is proven with clearly labelled synthetic geometry instead.

---

## 6. Asymmetric thrust

**Hard requirement met with no yaw special case anywhere.** The only moment source is
`Vector3.Cross(installation.positionAeroBodyM, force)`.

Synthetic geometry: engines at `y = ±3 m`, 50 kN each.

| Case | Net force | Net Mz | Hand-calculated |
|---|---|---|---|
| both running | 100 000 N | **0** | `(0,-3,0)×F = (0,0,+150000)`, `(0,+3,0)×F = (0,0,-150000)`, sum 0 |
| right engine failed | 50 000 N | **+150 000 N·m** | `(0,-3,0)×(50000,0,0) = (0,0,+150000)` |
| left engine failed | 50 000 N | **−150 000 N·m** | mirrored exactly |

Body Z is down, so positive Mz is nose-right: losing the right engine yaws *toward* the dead engine,
the correct physical direction. P-003c/d/e check magnitude **and sign in both directions** — a
magnitude-only test would pass an implementation that yawed the wrong way. P-003f confirms the moment
returns to zero with both engines running, so it came from geometry and not from an engine-out branch.

P-011c asserts the aggregator source contains no yaw special case at all.

---

## 7. Provenance behaviour

Three separate gates, each stricter than the last:

1. **`HasAuthoritativeThrustData`** — reads the *deck*, never the profile's own paperwork. The F-16 is
   exactly this case: sourced power dynamics, no frozen deck, zero thrust.
2. **Aggregate authority is UNANIMOUS** over contributing engines. A twin with one sourced and one
   synthetic engine reports **non-authoritative**, because the summed force contains the guess. P-007c
   checks the mixed case specifically; P-007e confirms two authoritative engines *do* pass, so the
   check is not always-false.
3. **`IsAcceptableForLiveFlight`** additionally requires a non-extrapolating deck, a declared source
   envelope, and measured installation geometry. Two authoritative engines still fail it when the
   envelope is undeclared (P-007f). An undeclared envelope is not a wide envelope.

Zero engines is **false** for authority: "no engines" is not "engines ready". A caller wanting
unpowered flight checks the engine count, which says what it means.

---

## 8. Telemetry added

**Per engine** (`MavEngineLoadResult`, one array entry per slot, reused in place — no per-step
allocation): slot id, engine profile id, throttle, commanded power, actual power, power rate, thrust,
force vector, moment contribution, thrust authority, inside-envelope flag, running flag, status reason.

**Aggregate** (`MavPropulsionSystem`): engine count, contributing count, authoritative count,
outside-envelope count, total thrust, total force, total moment, status string.

`MavPropulsiveLoads.powerState01` is retained for compatibility and is a **mean** for a twin. That is
recorded in code: a mean cannot express 90/40, so anything diagnosing an asymmetry must read the
per-engine array. This is ENG-004 closed. **See section 16 for the full aggregate semantics defined in
P0.1**, including the `powerStateSpread01` channel that says when the mean is hiding an asymmetry.

No LINQ, no per-step allocation, no reflection on the propulsion path.

---

## 9. Validations

`propcheck` offline harness compiling the **real production files** against a UnityEngine stub.

| Suite | Result |
|---|---|
| `MavSharedPropulsionValidation` (P-001 … P-014) | **96 passed, 0 failed** |
| `MavF16PropulsionValidation` (pre-existing, unchanged) | **20 passed, 0 failed** |

Full regression sweep across all nine harnesses: **709 passed, 0 failed.**

| Case | Covers |
|---|---|
| P-001 | single engine: aggregate == that engine, incl. hand-calculated `r×F = (0,−25000,0)` |
| P-002 | symmetric twin: forces add, moments cancel — and each is individually ±150 kN·m, not both zero |
| P-003 | asymmetric thrust, both signs, plus both-running returns to zero |
| P-004 | independent runtime state; literal 89.996% / 40.263% held simultaneously from one shared profile |
| P-005 | Garza/Morelli regression: gearing, RTAU, all four derivative branches, legacy entry points, and the trajectory through the shared runtime |
| P-006 | no deck and dead deck both give exactly zero thrust while the power state still advances |
| P-007 | provenance cannot be laundered, including the mixed pair |
| P-008 | zero engines: safe, no NaN from an empty average |
| P-009 | disabled installation contributes nothing |
| P-010 | NaN throttle, infinite dt, NaN deck result, degenerate direction, non-finite mount |
| P-011 | no engine file writes rigid-body state — via the **production** ownership scan |
| P-012 | `r×F` on hand-calculated vectors, then the same case through the runtime |
| P-013 | F-15 twin shape with every engine value unavailable |
| P-014 | an unresolvable law fails closed instead of substituting another |

### Deliberate-break probes: 12 of 12 turn the suite red

`rxfsign` +14 · `noinstallationmoment` +16 · `authorityanyof` +2 · `disabledcontributes` +2 ·
`sharedruntime` +40 · `fakethrust` +4 · `gearing` +10 · `rtau` +10 · `f15borrowsf16law` +2 ·
`f15guessedmounts` +6 · `silentlawfallback` +8 · `nanpassthrough` +2

### Three defects the probes found in the validation itself

Recorded because they are the reason the numbers above can be trusted.

1. **P-011 matched its own documentation.** A naive substring search for `Rigidbody` flagged the engine
   files' comments saying they hold no Rigidbody reference. Now uses
   `MavFlightDynamicsOwnershipScan.IsOwnershipViolation`, the production classifier, which strips
   comments and string literals first — and whose token list is *broader*, also catching a direct
   velocity or rotation assignment. Three sub-checks prove that classifier still flags a real
   `AddForce`, still flags a velocity write, and still ignores a comment mentioning both.

2. **P-004 encoded a spool rate I assumed rather than measured.** The sourced model is far slower from
   cold than a first-order lag: RTAU is 0.1 while the power deficit exceeds 50 and only rises to 1.0 as
   the gap closes, giving 2.9% at 0.5 s, 11.0% at 2 s, 46.2% at 4 s, 100% at 6 s. The thresholds were
   wrong, not the code. They are now the measured trajectory plus exact gearing arithmetic.

3. **A probe that crashed the suite was reported as "no failure".** `sharedruntime` made
   `GetRuntimeBySlotId(1)` return null; the run aborted with a `NullReferenceException` and the
   probe-runner, parsing a `failed=` count that never appeared, read the strongest possible failure as
   the weakest. Two fixes: the validation now uses null-safe accessors so a shared or missing runtime
   reports FAIL and the run completes, and the probe runner treats a crash, a build error or a missing
   RESULT line as red, hard-stops on a restore that does not apply, and re-verifies the baseline after
   every restore. `sharedruntime` now reports **RED +40**.

---

## 10. Engine data still unavailable

| Item | Status | Blocks |
|---|---|---|
| F-16 dimensional thrust (TP-1538 Table VI) | public candidate, **transcription not frozen** | powered F-16 |
| F-16 thrust-line offset from CG | undeclared | propulsive moment fidelity |
| F-15 engine variant | **not selected** | everything F-15 propulsion |
| F-15 thrust deck | unavailable | powered F-15 |
| F-15 mount coordinates | unavailable | F-15 asymmetric-thrust fidelity |
| rotor angular momentum (ENG-008) | channel exists, F-16 value not wired | gyroscopic coupling |
| fuel flow (ENG-007) | capability enum only | mass/CG evolution |
| inlet recovery / distortion (ENG-009) | absent | installation effects |
| engine operating states (ENG-010) | `SetFailed` only, no gameplay | start/flameout/relight |

No number in this phase was invented to fill any of these.

---

## 11. Known risks and limitations

- **`MavF16EnginePowerModel` is still the live path.** The new system is validated but not yet
  installed by `MavF16SelectionAutoSetup`. Nothing behaves differently in game today — which was the
  requirement — but the migration is not finished.
- **Offline harness, not Unity.** `propcheck` compiles production files against a stub. It cannot prove
  Unity's serialization of `MavEngineProfile` as a nested reference type behaves as expected, nor that
  `Awake` ordering on `MavF16PropulsionSystem` is right in a real scene. Both need the editor.
- **Profile serialization of shared references.** Two `MavEngineInstallation` entries pointing at one
  `MavEngineProfile` object is correct in C# but Unity's `[Serializable]` class fields **do not
  preserve reference identity** — a serialized twin would deserialize as two equal copies. Runtime
  state is unaffected (it never lives on the profile), but anything that later relies on reference
  identity across a domain reload must use a `ScriptableObject` asset instead. Code-built installations
  like `MavF15PropulsionSkeleton` are unaffected.
- `ENG-012` remains open: `F16_PROPULSION_REFERENCE_V0.1.md` still describes infrastructure as deferred
  that now exists. Not touched here to keep this phase's diff to propulsion architecture.
- The 0.5 kg mass-invariance and other Phase 5 gates are untouched and unaffected.

---

## 12. Recommended next phase

**P1 — retire the F-16 shim.** Repoint `MavF16SelectionAutoSetup` at `MavF16PropulsionSystem`, migrate
the suites that reference `MavF16EnginePowerModel`, confirm zero behaviour change in the editor, then
delete the shim. Small, self-contained, and it removes the only transitional piece this phase left.

Then **P3** (freeze the TP-1538 Table VI transcription and attach the deck) before **P4** (select the
first F-15 configuration and engine variant). P2 is already done: the generic engine array,
installation loads and per-engine telemetry all landed here.

**F15-R0 remains the gate for any real F-15 propulsion work** — configuration identity first, engine
variant second, numbers last.

---
---

# P0.1 — Architecture Hardening

Everything above describes P0 as built. This half records the audit pass that followed, the defects it
found, and what changed. Where it contradicts a section above, this half is current.

## 13. Defects found

| # | Defect | Severity | Status |
|---|---|---|---|
| **A** | **No multi-engine command path.** The entire pilot pipeline carries one scalar throttle, so a twin could hold two independent power states but could not be *commanded* into them through production code. P-004 proved independent STATE by calling a side-door setter that nothing in the pipeline reaches. | **architecture gap** | **FIXED** |
| **B** | **ENG-013 profile identity.** Unity serializes `[Serializable]` classes by value, so an inspector-authored twin sharing one profile would deserialize as two copies that then drift. | **authoring hazard** | **FIXED** |
| **C** | **Registration lifecycle unproven.** The editor hook lived in a runtime script behind `#if UNITY_EDITOR`; fail-closed behaviour and idempotence were untested. | **medium** | **FIXED** |
| **D** | **Per-step authority flag answered two questions.** It required every *installed* profile to be authoritative *and* every contributing result - conflating "are these numbers sourced?" with "is this aircraft's configuration sourced?". | **semantic defect** | **FIXED** |
| **E** | **Aggregate scalar telemetry was undefined.** `reportedThrustN` and `powerState01` had no stated multi-engine meaning. | **ambiguity** | **FIXED** |
| **F** | **Readiness checks were unreachable.** Every test failed on thrust data before geometry was ever checked, so the geometry and provenance checks could have been deleted unnoticed. | **untested invariant** | **FIXED** |
| **G** | **Authoring hazards untested.** Null profile reference, null slot, duplicate slot id - none covered, and the first becomes the *normal* mistake now that the profile is an asset reference. | **untested** | **FIXED** |

## 14. Command-channel architecture (Gap A)

```text
MavPilotCommand.throttle01          scalar, unchanged
        -> control law              scalar, unchanged
        -> MavControlInput           scalar, unchanged
        -> actuator                  scalar, unchanged
        -> MavPropulsionModelBase.Evaluate(..., throttle01, ...)   scalar, unchanged
                |
                v
        MavPropulsionCommand         <-- NEW boundary
            BeginStep(pipelineThrottle)      reset to LINKED every step
            optional MavPropulsionCommandSourceBase publishes per-engine intent
                |
                +--> EngineRuntime 0   throttle, cutoff
                +--> EngineRuntime 1   throttle, cutoff
```

**Why a boundary and not a wider `MavControlInput`.** Putting a throttle array in that struct would
have touched every control law, actuator, trim solver and validation that copies it; made a serialized
struct hold a variable-length array; and pushed engine-count knowledge into layers with no business
knowing it. The scalar pipeline instead keeps meaning exactly what it always meant — *the* throttle —
and anything needing per-engine authority publishes into the command object.

**Blast radius: zero production callers.** The P0 channel API (`useChannelThrottles`,
`SetChannelThrottle`, `GetChannelThrottle`, `channelThrottles`) was used only by P0's own validation,
so it was removed rather than left as a second, competing mechanism.

| Property | How |
|---|---|
| F-16 stays trivial | no command source attached → linked. `MavF16PropulsionSystem` declares nothing about commands |
| F-15 normally linked | same; a source is what you add *to get* differential throttle |
| independent L/R | a `MavPropulsionCommandSourceBase` component publishes per engine |
| no "two engines" assumption | the command is sized from the installation's slot count |
| cutoff without fake yaw | cutoff zeroes that engine; the other engine's `r × F` yaws the aircraft |
| no per-step allocation | arrays sized once in `Build`; `BeginStep` only overwrites |
| fails safe | reset to linked *before* consulting the source, so a source that stops publishing reverts to linked rather than leaving a stale asymmetric command |

The last row is a deliberate failure direction: a stuck differential throttle is a control failure, a
reverted link is not.

## 15. Profile asset semantics (Gap B / ENG-013)

**Decision: converted to `ScriptableObject`.** Done now because it was cheap now — three construction
sites, all in P0 code, zero scene or prefab dependencies — and expensive later, once assets exist to
migrate.

```text
ONE MavEngineProfile asset          <- identity, provenance, law selection, deck reference
        |
        +--> MavEngineRuntime Left   <- all mutable state
        +--> MavEngineRuntime Right  <- all mutable state
```

A `ScriptableObject` field is a genuine object reference, so one asset means one definition, one
provenance record, and no left/right drift. Runtime behaviour was never at risk — state has never
lived on the profile — the hazard was *authoring*: two copies that diverge as someone edits one, while
the code still claims they are the same engine.

The asset makes the no-state rule **more** important, not less: an asset is shared by every aircraft
referencing it, so a state field there would be shared globally rather than per-aircraft. P-020 checks
the rule behaviourally *and* at source level, because no behavioural test can prove a field is absent.

`MavEngineProfile.CreateInMemory(id)` is the only sanctioned code construction path. No engine assets
were created and no engine data was invented.

## 16. Aggregate telemetry semantics (E)

Defined in the tooltips on `MavPropulsiveLoads`, so a reader finds the answer at the field:

| Field | Meaning | Multi-engine |
|---|---|---|
| `reportedThrustN` | **SUM** over contributing engines of each engine's scalar thrust along its own thrust direction | a sum, not a max, not a per-engine value |
| `powerState01` | **MEAN** actual power over contributing engines | exact for one engine; **cannot express asymmetry** |
| `powerStateSpread01` | **max − min** power across contributing engines | **non-zero exactly when `powerState01` is hiding an asymmetry**; always 0 for one engine |
| `contributingEngineCount` | engines that contributed | 0 when unpowered or all shut down |
| `hasAuthoritativeData` | is every number *in these loads* sourced | see section 17 |

`powerState01` is kept rather than deprecated because the whole existing pipeline reads it, and it is
exact in the single-engine case that is all the F-16 has. What made it safe to keep is
`powerStateSpread01`: a consumer can now *tell* when the mean is unrepresentative instead of having to
guess. **Per-engine `debugEngineResults` remains the source of truth for anything multi-engine.**

## 17. Provenance versus operational state (Gap D / section 6)

The rule "aggregate authority is unanimous" was right about data and wrong about operation. Two flags
now, each answering exactly one question:

| Flag | Question | Operation-dependent? |
|---|---|---|
| `MavPropulsiveLoads.hasAuthoritativeData` | is every number **in these loads** backed by authoritative data? | yes — it describes this step's numbers |
| `MavPropulsionSystem.HasAuthoritativeData` | does **every installed engine** have authoritative data? | **no** — configuration only |
| `installation.IsAcceptableForLiveFlight` | plus non-extrapolating deck, declared envelope, measured geometry | **no** |

**Operational states never affect provenance.** Commanded cutoff, engine failure, disabled
installation and zero throttle all leave the aggregate authoritative when the data behind it is
sourced. An authoritative twin with *both* engines deliberately shut down reports authoritative data
and zero thrust — its thrust is a number we *know*, not one we lack (P-017e/f).

**How the split was found.** A mutation probe that deleted the installed-profile term from the
per-step flag changed no test result. The two terms were redundant for every case the suite covered:
they differ only when a non-authoritative engine is **not running**, and nothing tested that.
Investigating it showed the per-step flag had been answering a configuration question — and a
live-flight gate reading it would have flipped the moment a pilot started a second, unsourced engine.
P-022 is the discriminating case, and the probe now targets the gate that actually owns the
configuration question.

## 18. Registration lifecycle (Gap C)

Three independent routes, any one sufficient, overlapping safely because registration is idempotent:

| Route | Mechanism | Covers |
|---|---|---|
| 1 | `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` in `MavF16EngineLawRegistrar` | **player build** and play mode. A UnityEngine attribute — it ships |
| 2 | `[InitializeOnLoadMethod]` in `MavF16EngineLawRegistrarEditor`, **in the Editor assembly** | editor, without entering play mode |
| 3 | explicit `EnsureRegistered()` from `MavF16PropulsionInstallation.CreateEngineProfile` | offline harnesses; and any ordering surprise, since anything building an F-16 profile registers first |

**The editor hook moved out of the runtime script.** P0 had it behind `#if UNITY_EDITOR` in
`MavF16EngineLawRegistrar`, which does keep `UnityEditor` out of a player build — but while the editor
runs, the runtime assembly still carries the reference. It now lives under `Editor/`, so **no runtime
script names `UnityEditor` at all** and the question does not arise.

**Ordering:** route 1 runs before any scene loads, therefore before any `Awake`, therefore before any
`Build`. Route 3 runs inside profile construction. There is no ordering in which a profile exists but
its law does not.

**Domain reload:** Unity clears statics, so the factory's registration resets. `IsRegistered` asks the
**factory** rather than caching a bool, so it cannot drift from the thing it describes, and
`EnsureRegistered` is self-healing (P-018f). A cached one-way flag is probe `cachedregistrationflag`.

**Fail closed:** an unregistered law is **not** substituted. `Resolve` returns null and `Build` refuses,
naming the law (P-018c/d). Reaching that state in a test needs no back door — the factory's
`RegisterF16GarzaMorelli(null)` is a documented, supported clear.

**Idempotence:** registering the same instance is a no-op and does not increment
`F16RegistrationCount`; registering a *different* instance does, so the counter is not one that never
moves (P-019).

## 19. Readiness conditions (Gap F / section 7)

`IsAcceptableForLiveFlight` now requires, per slot: valid engine identity; authoritative
non-extrapolating thrust data; a **declared source envelope**; a finite thrust direction long enough to
normalise meaningfully; a finite mount position; **declared geometry**; and **geometry provenance**.

P-023 builds a fully acceptable installation and knocks out each condition individually, proving all
nine are load bearing — then restores it, proving each failure was caused by the condition removed and
nothing else. This case exists because a probe deleting the geometry check left the suite green: every
prior test failed earlier on thrust data, so that check was never the deciding factor.

That matters soon rather than hypothetically: the moment the TP-1538 deck is attached to the F-16,
undeclared geometry and an undeclared envelope become the **only** things between it and live-flight
acceptance.

## 20. Unity compile status — HONEST ANSWER

**Unity was NOT invoked. No Unity editor is installed on this machine.** Searched
`C:\Program Files\Unity`, `C:\Program Files (x86)\Unity`, `C:\Unity`, `D:\Unity`, `E:\Unity`, the
Unity Hub default path, `PATH`, and the editor registry key — no `Unity.exe`, no Unity Hub. The project
requires `6000.3.16f1`.

**The offline harness result is NOT a Unity compile result** and is not presented as one. It compiles
production sources against a hand-written `UnityEngine` stub with a `net8.0` C# compiler. It cannot
verify: real `ScriptableObject` serialization, actual assembly split, `Awake`/domain-reload ordering,
inspector serialization of the new fields, or Unity's own compiler on these files.

Static checks that *were* completed:

| Check | Result |
|---|---|
| `UnityEditor` namespace references in P0/P0.1 propulsion files | **0** |
| `#if UNITY_EDITOR` guards in those files | **0** |
| editor hook under `Editor/` | yes |
| runtime files referencing the editor class | **0** (correct direction) |
| `.asmdef` files that could override the folder convention | none — the `Editor/` convention applies |
| serialized enums with explicit numeric values | **5 of 5**, every member explicit — safe against reordering |
| `MavEngineProfile` declared `ScriptableObject`, no public constructor, no redundant `[Serializable]` | confirmed |
| `.meta` for every new `.cs` | confirmed |
| null profile / null slot / duplicate slot id behaviour | P-024 |

**Three of these checks initially reported false positives by matching the word `UnityEditor` inside
doc comments explaining its absence** — the same failure mode P-011 was fixed for in P0. Each was
re-run comment-aware before being believed.

**Unity-only work still required:** open the project in `6000.3.16f1`, confirm a clean compile of both
assemblies, run the *Run Shared Propulsion Architecture Validation* menu item in-editor, and confirm a
`MavEngineProfile` asset reference survives a domain reload shared by two slots.

## 21. Validation

| Suite | Result |
|---|---|
| `MavSharedPropulsionValidation` (P-001 … P-024) | **181 passed, 0 failed** |
| `MavF16PropulsionValidation` (pre-existing, unchanged) | **20 passed, 0 failed** |
| Full sweep, nine harnesses | **794 passed, 0 failed** |

New in P0.1: **P-015** independent commands through the public API · **P-016** linked broadcast and
revert-on-silence · **P-017** authoritative engine off preserves provenance (cutoff, both-off, failed,
disabled, zero throttle) · **P-018** registration fails closed · **P-019** duplicate registration
deterministic · **P-020** profile/runtime separation, behavioural and source-level · **P-021**
aggregate scalar semantics · **P-022** per-step vs configuration authority · **P-023** readiness ladder
· **P-024** authoring hazards.

### Mutation probes: 24 of 24 turn the suite red

P0.1 set: `sharedcommand` +28 · `ignorecommandsource` +46 · `stalecommand` +8 · `offmeansunsourced` +2
· `cutoffstillthrusts` +8 · `configauthorityanyof` +4 · `cachedregistrationflag` +6 ·
`nonidempotentregistration` +2 · `thrustnotsum` +6 · `hidespread` +2 · `acceptundeclaredgeometry` +2 ·
`stateonprofile` +2

P0 set, re-run: `rxfsign` +16 · `noinstallationmoment` +18 · `authorityanyof` +12 ·
`disabledcontributes` +18 · `sharedruntime` +62 · `fakethrust` +4 · `gearing` +12 · `rtau` +10 ·
`f15borrowsf16law` +2 · `f15guessedmounts` +6 · `silentlawfallback` +14 · `nanpassthrough` +2

`sharedcommand` is the probe the brief asked for: it makes every engine read the linked throttle,
reintroducing exactly the P0 defect, and P-015 catches it.

**Three probes needed retargeting** after P0.1 rewrote the code they pointed at (`authorityanyof`,
`disabledcontributes`, `offmeansunsourced`). A probe whose anchor has gone stale reports "setup
failed", not "passed", which is why the runner distinguishes them.

## 22. F-16 behaviour regression

**Unchanged.** The F-16 has one engine, no command source, and therefore flies linked on the pipeline
throttle exactly as before. `MavF16EnginePowerModel` remains the component
`MavF16SelectionAutoSetup` installs, still forwarding to the canonical equations. `MavSixDoFBody`,
gravity ownership, inertia, aero coefficients, AoA/G protection and FLCS tuning are untouched.
P-005 and the pre-existing 20-check `MavF16PropulsionValidation` both still pass.

## 23. F-15 architecture readiness

The architecture now satisfies every twin-engine requirement: two slots, one shared profile asset, two
independent runtimes, **independent commands through production code**, commanded cutoff, and
asymmetric yaw from `r × F` with no special case. Engine data remains `Unavailable` /`UNFROZEN` on
every axis.

## 24. Unresolved gaps

| ID | Gap | Blocks |
|---|---|---|
| ENG-005 | F-16 thrust deck not frozen | powered F-16 |
| ENG-006 | F-15 engine variant not selected | all F-15 propulsion |
| ENG-007 | fuel flow — capability enum only | mass/CG evolution |
| ENG-008 | rotor angular momentum — channel applied, no aircraft declares a value | gyroscopic coupling |
| ENG-009 | inlet recovery / distortion absent | installation effects |
| ENG-010 | engine states minimal — `SetFailed` and commanded cutoff only; no shutdown transient, windmilling drag, or relight | realistic failure/start |
| ENG-012 | `F16_PROPULSION_REFERENCE_V0.1.md` still describes existing infrastructure as deferred | documentation accuracy |
| ~~ENG-014~~ | ~~Unity never invoked~~ — **CLOSED in P0.2.** The editor was found at a non-standard path and the project compiles and validates in it; see section 25 | — |
| — | `MavF16EnginePowerModel` shim still live | P1 |

**ENG-013 is CLOSED** by the ScriptableObject conversion.

---
---

# P0.2 — Unity Validation

## 25. Unity validation result

### 25.1 Correction to P0.1

P0.1 reported ENG-014, "no Unity editor is installed on this machine". **That was wrong.** The editor is
installed at `D:\unitys\6000.3.16f1\Editor\Unity.exe` — a non-standard directory that the P0.1 search
did not cover, because it only checked `C:\Program Files\Unity`, the Unity Hub default, `PATH` and the
editor registry key. A full fixed-drive search finds it immediately.

The consequence is not just a wrong status line: P0.2 was executable all along, and running it found
three defects that the offline harness structurally could not.

### 25.2 Environment

| | |
|---|---|
| Editor | `6000.3.16f1` (`a56f230f6470`), exact match to `ProjectVersion.txt` |
| Invocation | `-batchmode -nographics -quit -executeMethod ...MavSharedPropulsionUnityValidation.RunBatch` |
| Project state | previously imported; `Library/ScriptAssemblies` present |

Note for anyone repeating this: the project path contains a space, and passing it unquoted through
PowerShell's `-ArgumentList` splits it — Unity then reports
`Couldn't set project path to: E:/unity project/Maverick/E:/unity` and exits 1. Embed the quotes.

### 25.3 Compile result

**Zero compile errors.** Both `Assembly-CSharp` and `Assembly-CSharp-Editor` build.

**Warnings: 514, every one `CS0618`, none in any propulsion file.** They are the Unity 6 deprecation of
`Object.FindObjectOfType` across pre-existing weapons, CAS, aircraft and UI code. Not introduced by this
work, and not touched: fixing 514 call sites would mean editing weapons, sensors and gameplay systems
this phase is explicitly forbidden to modify. Recorded as a project-wide item, not a propulsion one.

No serialization warnings, no missing-script warnings, no domain-reload errors.

### 25.4 Defects found by Unity

Three, all invisible offline.

**D-P02-1 — a broken string literal that never compiled.**
`MavF16ReferenceValidationEditor.cs` contained `"PASS\n\n"` written as two literal newlines inside a
string, from a heredoc escaping mistake in P0. It produced `CS1010: Newline in constant` and a cascade
of ~30 syntax errors. **The offline harness never compiled that file**, so 794 offline checks passed
against a project that could not build. Fixed.

*The lesson is about coverage, not about the typo:* a harness that compiles a chosen subset proves
nothing about the files outside it.

**D-P02-2 — a destroyed engine profile reported authoritative data.**
When a `ScriptableObject` profile is destroyed mid-session it becomes Unity "fake-null". No engine then
contributes, so the per-step `everyContributingResultAuthoritative` term was **vacuously true** and the
aggregate claimed authoritative data for an aircraft whose engine definition had been *lost*.

That is the exact distinction P0.1 drew, failing in the direction P0.1 did not consider: "the engine is
switched off" keeps provenance, "the engine definition is gone" cannot. **Unreachable offline** — a
plain C# class cannot become fake-null.

Fixed by adding an `everySlotHasProfile` term: a slot with a missing or destroyed profile makes the
aggregate non-authoritative. Covered by U-012e and, offline, by P-024d2.

**D-P02-3 — 907 bytes of GC allocation per physics step.**
Measured in Unity, ~45 KB/s at 50 Hz. Two telemetry causes, neither load-bearing:

- `enum.ToString()` per step for the command mode — reflective and allocating. Replaced with interned
  `const string` literals.
- the `debugStatus` roll-up — a nine-field concatenation every step. Moved behind
  `composeStatusString` (default **off**) and exposed as `DescribeStatus()`. Every number in it is
  already a live numeric field, so gating the string costs no information.
- a third, smaller one in `MavEngineRuntime`: `deck.Authority + " / " + deckResult.statusReason` per
  engine per step. The result now carries the authority as an **enum** and the deck's own string by
  reference; `DescribeThrustStatus()` composes on demand.

Final measurement: **0 bytes/step** minimum over 7 trials, below the control loop's own 24.58 noise
floor. Verbose telemetry on costs 522 bytes/step, which is why it is off by default.

### 25.5 A measurement method that had to be fixed before it could be believed

The first allocation readings were 907, then 289, then 291 bytes/step — barely moving despite real
fixes. Measuring the *same* loop at 500 / 2000 / 8000 steps gave **786 / 51 / 174** bytes per step: a
15x spread for identical code, while an empty control loop read zero.

`GC.GetTotalMemory` measures the whole managed heap of a live editor domain, not one method. Background
editor allocation was landing inside the sample window, and **no single reading from that instrument
could have certified a budget** — a passing result would have been luck.

Noise from that source can only *add* to a reading, never subtract, so the **minimum across repeated
trials** is a sound upper bound. U-009 now takes 7 trials and judges the minimum, reports the spread,
and runs an empty control loop to establish the floor. It also A/B-compares verbose-on against
verbose-off on the same instrument, where the shared noise cancels.

### 25.6 Results, by requested item

| Item | Result |
|---|---|
| Unity compile | **0 errors**; 514 pre-existing `CS0618`, none in propulsion |
| ScriptableObject profile | U-001: real `CreateInstance`, `UnityEngine.Object` semantics, fake-null detection, and a full serialization round-trip incl. the provenance enum and nested struct |
| Two installations, one profile | U-002: same object by reference, two distinct runtimes, **0** state fields in Unity's serialized view — with a positive control proving the scan finds the 3 state fields on `MavEngineRuntime` |
| Registration lifecycle | U-003: registered in a freshly reloaded domain **without entering play mode**; idempotent (count stays 1 across 2 further hook calls); clears and self-heals; `[RuntimeInitializeOnLoadMethod]` present for the player; **`Assembly-CSharp` references no `UnityEditor` assembly**, checked against compiled metadata |
| Independent commands | U-004: left 1.0 → **100.000%**, right 0.25 → **16.235%**, pipeline scalar 0.5 matching neither, through a real component seam |
| Linked fallback | U-005: both engines 78.262%, identical to 1e-4; **destroying the source component** reverts to LINKED_SCALAR |
| Command authority | U-006: full coverage → PER_ENGINE_SOURCE; partial coverage **refused in full** and reported |
| Asymmetric r × F | U-007: equal thrust cancels to 0; one engine out gives Mz = **+150 000 N·m**, equal to an independently computed `r × F`; mirrored exactly |
| Rigidbody ownership | U-008: **0** Rigidbody fields/properties across 8 compiled propulsion types; evaluating propulsion moves no body; the load set refuses a second propulsive contribution |
| Allocation | U-009: **0 bytes/step** minimum over 7 trials |
| Provenance vs operation | U-010: cutoff halves thrust, keeps provenance; an unsourced engine shut down keeps **load** provenance but fails the **configuration** answer and readiness |
| F-16 regression | U-011: gearing, RTAU, Pdot all exact; shim agrees; 200-step spool trajectory matches to 1e-4; thrust still **exactly zero**; authority LINKED_SCALAR as before |
| Missing profile | U-012: unassigned refuses to build; **destroyed mid-session** yields zero loads and non-authoritative |

**Unity suite: 73 passed, 0 failed.**

### 25.7 Command authority (section 6 of the brief)

P0.1 had a genuine ambiguity: `BeginStep` seeded every engine's per-engine slot with the scalar, so a
source addressing only one engine of a twin left the other silently on the scalar — and the numbers
were indistinguishable from a fully commanded asymmetry.

Now all-or-nothing. `MavPropulsionCommandAuthority` is `LinkedScalar` or `PerEngineSource`, never both.
A source owns per-engine commands only if it addressed **every** installed engine; partial coverage is
refused in full, reverts to linked, and raises `debugPartialCommandRefused`. Refusing is the safe
direction: linked flight is a defined state, a half-applied differential command is not. Cutoff is a
per-engine command and does not act under linked authority.

Diagnostic: `debugCommandAuthority` (`LINKED_SCALAR` / `PER_ENGINE_SOURCE`), `debugAddressedEngineCount`,
`debugPartialCommandRefused`. Telemetry only — nothing reads them to make a decision.

### 25.8 Telemetry warning (section 9 of the brief)

Carried on the field itself, where a reader will meet it:

> `powerState01` — MEAN over contributing engines. **COMPATIBILITY / DEBUG ONLY.** Exact for one
> engine. **DO NOT use for engine failure, cutoff, imbalance, or individual-engine control logic** — a
> mean cannot express 90/40 and will read as a healthy 65. Use the per-engine `MavEngineLoadResult`
> array; check `powerStateSpread01` to tell whether this scalar is hiding an asymmetry.

### 25.9 Working tree

Unity reordered one `<Project>` line in the generated `Maverick.slnx`. Cosmetic, reverted. **No other
tracked file changed**, no scene or prefab was touched, no asset was created — every synthetic object
in the suite is created and destroyed in memory and tagged `SYNTHETIC_VALIDATION_ONLY`.

### 25.10 Validation totals

| | |
|---|---|
| Unity suite (U-001 … U-012) | **73 passed, 0 failed** |
| Offline harnesses, nine suites | **808 passed, 0 failed** |
| Mutation probes | **24 of 24 turn a suite red** |

Five probes needed retargeting after P0.2 rewrote the code they anchored to, and one
(`sharedcommand`) had a restore string that was no longer unique — it left the tree dirty, which the
runner's post-restore baseline check caught immediately. Both the file and the probe were repaired.

### 25.11 Remaining blockers

| ID | Gap | Blocks |
|---|---|---|
| ENG-005 | F-16 thrust deck not frozen | powered F-16 |
| ENG-006 | F-15 engine variant not selected | all F-15 propulsion |
| ENG-007 / 008 / 009 / 010 | fuel flow, rotor momentum value, inlet effects, engine states | later phases |
| ENG-012 | `F16_PROPULSION_REFERENCE_V0.1.md` still calls existing infrastructure deferred | documentation |
| **ENG-015** | **515 project-wide `CS0618` warnings** (`FindObjectOfType` deprecated): 514 in `Assembly-CSharp` plus one site in `Assembly-CSharp-Editor` (`Scripts/Editor/MavTurnDynamicsMigrationValidation.cs:78`). P0.2 reported 514 — that was the runtime assembly alone, before a run that recompiled both. Pre-existing, none in propulsion; fixing them means editing weapons/sensors/gameplay, out of scope here | code hygiene |
| — | `MavF16EnginePowerModel` shim still the component auto-setup installs | P1 |

**ENG-013 and ENG-014 are CLOSED.**

~~Not attempted, because the brief forbids it: Play Mode entry/exit cycling.~~ **Done in P0.2b — see
section 26.** Registration was validated here across editor domain reload, the batchmode compile, and
the compiled-metadata check that the player path carries `[RuntimeInitializeOnLoadMethod]`; lifecycle
items B and C rested on that inference. P0.2b cycles Play Mode twice under both domain-reload settings
and closes it by direct observation.

---

# P0.2b — Play Mode Lifecycle Closure

## 26. Play Mode validation result

P0.2 ended with one registration item taken on inference: Play Mode enter/exit cycling was not
performed, so lifecycle items B and C rested on an editor domain reload plus a compiled-metadata
check that the player path carries `[RuntimeInitializeOnLoadMethod]`. P0.2b performs the cycling, and
in doing so closes that inference and finds four further defects — three of them in the validation
apparatus itself, which is the more useful kind of finding at this stage.

### 26.1 How Play Mode is driven

`Editor/MavSharedPropulsionPlayModeLifecycle.cs` is a state machine, because entering Play Mode may
reload the managed domain and destroy the call stack asking the question. Its progress lives in
`SessionState`, the only store that survives a domain reload inside one editor session; an
`[InitializeOnLoad]` static constructor re-hooks `playModeStateChanged` and `update` on every domain
load, which is exactly how the machine survives one.

Each mode runs: fresh edit mode → enter → exit → re-enter → exit. The whole sequence runs twice, once
with domain reload enabled and once disabled.

Isolation. It enters Play Mode on the empty unsaved scene batchmode already has, and refuses to run
at all if a saved scene is open (`L-000`), so it cannot start a production scene's gameplay. Every
object is created in memory and labelled `SYNTHETIC_VALIDATION_ONLY`. `F16Replacement` is not
enabled. Entering Play Mode on an empty scene is not the same thing as enabling the gameplay FDM.

Crash containment. An exception inside a `playModeStateChanged` callback would abandon the machine
mid-session, leaving the editor-side loop waiting on the probe until the watchdog fired — a hang with
no diagnosis. Each phase now records a crash as a FAILURE and unblocks the machine so it can finish
and report. Same lesson as the crashing probe in Phase 5B.7: a suite that hangs is worse than one
that fails.

### 26.2 Project setting, and a discrepancy worth recording

`ProjectSettings/EditorSettings.asset` holds `m_EnterPlayModeOptionsEnabled: 1` and
`m_EnterPlayModeOptions: 0`. The API read back `enterPlayModeOptionsEnabled = False` in batchmode,
which does not match the serialized 1. This is recorded rather than chased: the suite does not trust
the setting either way. It measures the domain reload directly, with a per-domain GUID fingerprint
compared across each transition (`L-1x0a`), and every reload claim in this section is that
measurement rather than the setting's word.

Both settings were driven and restored to the values found (`L-099`). `ProjectSettings/` is clean in
git afterwards.

### 26.3 Registration across enter / exit / re-enter

| | mode 0 (domain reload ON) | mode 1 (domain reload OFF) |
|---|---|---|
| reload observed on entry | yes | no |
| law available in Play Mode after a deliberate edit-mode clear | yes | yes |
| registration count | 1 | 3, being at most 2 above the pre-clear 1 |
| repeated `EnsureRegistered` + `RegisterOnGameStart` | no change | no change |
| registry intact after exit | yes | yes |
| after the full cycle | resolvable, bounded count | resolvable, bounded count |

**Mode 1 is what closes the P0.2 inference.** The registration is cleared deliberately in edit mode
immediately before the transition; with domain reload disabled, the editor-assembly
`[InitializeOnLoadMethod]` does not run again, and nothing has yet built an engine profile, so route 3
cannot be responsible either. The law was nevertheless registered on entry. Only
`[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` can have done that — the player-build route,
demonstrated rather than inferred from compiled metadata.

Idempotency is shown two ways: the count does not move when both entry points are called again, and
it is bounded relative to the pre-clear value rather than growing per transition. The absolute bound
of "at most 2" that this suite started with was wrong, and mode 1 proved it: with domain reload off
the counter is not reset between sessions, and clearing the registration is itself a change that
increments it. The bound is now relative, which holds under either setting.

### 26.4 Runtime state leakage

No stale `MavEngineRuntime` state crosses sessions. Session 2 reproduces session 1's spool exactly
(100.000 / 16.235 to 1e-4, `L-140`), and a deliberate continuation from a different initial condition
is shown to diverge, so `L-140` is a check that could have failed (`L-140b`).

The structural reason is checked separately: the six propulsion types hold **zero mutable static
fields** (`L-151`), so nothing can persist even with domain reload disabled — with a positive control
proving the scan finds a mutable static when one exists, and the one deliberate static (the law
registry) named explicitly rather than silently excluded.

`L-150c` records a Unity lifecycle fact this suite got wrong at first: **Unity reloads the domain when
ENTERING Play Mode, not when leaving it.** A static armed during a session is still there in edit mode
afterwards and is cleared by the next entry. The check now ties the canary's fate to the reload
actually observed on that transition, so it holds under either setting and would catch a change in
Unity's behaviour instead of encoding one belief about it.

### 26.5 Command authority in Play Mode

Per-engine: left 1.0 → **100.000 %**, right 0.25 → **16.235 %**, with the pipeline scalar at 0.5 and
neither engine landing on the 32.47 that scalar would have produced. Authority reads
`PER_ENGINE_SOURCE` with 2/2 addressed.

**No frame blends the two authorities.** Every one of 900 Play Mode steps is checked, and the check is
not that the diagnostic string holds one of two values — which is trivially true — but that the string
and the throttles the engines will actually read cannot disagree. 900 fully per-engine, 0 ambiguous.

Source destroyed mid-session: authority falls back to `LINKED_SCALAR`, both engines converge, no
exception, and all 900 subsequent frames are unambiguously linked — including the frame on which the
source vanished. Partial coverage (1 of 2 engines) is still refused in full and reported via
`debugPartialCommandRefused`.

### 26.6 Destroyed profile in Play Mode

A live profile produces 50 000 N and reports authoritative data; `DestroyImmediate` makes it Unity
fake-null; evaluating then throws nothing, thrust is exactly zero, readiness fails naming the slot,
and the load set no longer claims authoritative data.

That last point is the one that needed a second check, and D-P02b-4 below is why.

### 26.7 Allocation — method, and what it is allowed to claim

This is where P0.2b found the most. **The P0.2 measurement could not see what it was used to rule
out.**

| instrument | verdict |
|---|---|
| `GC.GetTotalMemory` delta, minimum over trials | **a LEVEL, not a counter.** Reports **0 bytes/step** for a loop deliberately allocating a `float[4]` per step: the arrays are collected inside the window and the level returns to where it started. Its sensitivity floor sits between 40 and 100 bytes/step. |
| `GC.CollectionCount(0)` | **threshold scales with heap size.** 38 MB of deliberate garbage moved it by **zero** in this editor. Rejected as primary on that evidence. |
| `GC.GetTotalAllocatedBytes(precise)` | **unavailable** — needs .NET Standard 2.1; this project is set to 2.0, which a validation pass has no business changing to suit its own measurement. |
| **`ProfilerRecorder` on "GC Allocated In Frame"** | **a COUNTER, so short-lived garbage is included. PRIMARY.** |

The primary instrument needed two fixes before it worked. The profiler is **off in batchmode**, and a
recorder on the counter still reports `Valid` and still returns a number — a flat 40 bytes/frame for
every phase, including one allocating 1600 bytes/frame. And in batchmode the editor's player loop runs
`Update` roughly an order of magnitude more often than `FixedUpdate` (20 calls against ~240 frames),
so a phase measured per Update frame while its work happened per FixedUpdate was mostly measuring
frames in which nothing had been done.

The working measurement is a differential across three phases of equal length, each judged by its
minimum frame reading:

```
EVALUATING   40 bytes/frame     (40 propulsion steps per frame)
IDLE         40 bytes/frame     (the player loop's own cost)
SENSITIVITY  1960 bytes/frame   (deliberate float[4] per step, no propulsion)
```

- propulsion: (40 − 40) / 40 = **0 bytes/step**
- demonstrated sensitivity: (1960 − 40) / 40 = **48 bytes/step** against a known 40

The sensitivity phase is judged **first**, because it decides whether the propulsion figure means
anything: a flat instrument reports zero for everything and would hand out a free pass. If neither
instrument passes its control, the suite makes **no allocation claim from Play Mode** and says so.

What this supports: **no observed steady per-step allocation, on an instrument demonstrated in the
same run to resolve 40 bytes per step.** It is not a formal zero-allocation proof — only an
allocation-free-by-construction analysis of the IL would be that. The heap delta and collection count
over a million steps in one real `FixedUpdate` agree, and are reported as corroboration explicitly
marked not load-bearing.

### 26.8 Defects found in P0.2b

| ID | Defect | Where |
|---|---|---|
| **D-P02b-1** | **The P0.2 allocation instrument was blind to short-lived garbage.** `GC.GetTotalMemory` deltas report 0 bytes/step for a deliberate 40 bytes/step. P0.2's "0 bytes/step" was an upper bound at a resolution P0.2 had never established, not a measurement of zero. It caught the original 907 bytes/step string defect only because that garbage outgrew what the window reclaimed — which is exactly why the blind spot went unnoticed. | measurement |
| **D-P02b-2** | **`U-009b` asserted the instrument was noisy.** It required the readings to vary, and failed the first time the editor was quiet enough for all seven trials to read zero — i.e. it failed because the measurement got *better*. An assertion about the environment, not the system. Replaced with a sensitivity control. | validation |
| **D-P02b-3** | **`P-024d2`, the offline coverage P0.2 added for the destroyed-profile fix, does not exercise it.** Its build is *refused*, so `debugEngineCount` is 0 and the aggregate is non-authoritative because of the engine-count term; the `everySlotHasProfile` term is never consulted. Deleting that term left `P-024d2` green. Found by a new mutation probe. | validation |
| **D-P02b-4** | **The Play Mode destroyed-profile check tested the wrong expression.** `sys.HasAuthoritativeData` is the CONFIGURATION gate, which fails on a null profile for its own separate reason; `loads.hasAuthoritativeData` is the DATA AUTHORITY the `everySlotHasProfile` guard protects, and the suite never inspected it. Deleting the guard left the whole Play Mode suite green. | validation |

Three of the four are defects in the evidence, not in the aircraft. That is the expected shape of a
pass whose job is to check whether the previous pass's evidence held.

Fixes: the primary instrument is now a counter with a sensitivity control judged first; `U-009` states
its own resolution and points at `L-1x5` for the region underneath; `P-024d3`–`P-024d7` build
successfully and *then* lose the profile, so the guard is the only thing that can hold; and `L-1x4b2`
/ `L-1x4e2` / `L-1x4e3` check the load set's authority and its transition, separately from the
configuration gate.

### 26.9 Safety

| Item | State |
|---|---|
| `F16Replacement` | not enabled anywhere new |
| gameplay aircraft ownership | unchanged |
| production scenes / prefabs / materials / models | untouched; 0 asset files created |
| `Rigidbody` writes outside `MavSixDoFBody` | none — 0 Rigidbody members across the 8 compiled propulsion types |
| F-16 thrust without an authoritative deck | exactly zero |
| `stash@{0}` | untouched |
| `ProjectSettings/` | clean in git; Enter Play Mode Options driven and restored |
| project API compatibility level | unchanged at .NET Standard 2.0, deliberately, even though 2.1 would have supplied a better allocation counter |

### 26.10 Counts

| Suite | Result |
|---|---|
| Unity P0.2b Play Mode lifecycle | **169 passed, 0 failed** (2 modes × enter/exit/re-enter/exit) |
| Unity P0.2 edit-mode suite | **74 passed, 0 failed** |
| Unity compile | **0 errors**; 515 pre-existing `CS0618` (514 in `Assembly-CSharp`, 1 site in `Assembly-CSharp-Editor`), **zero in any file this phase added or changed** |
| Offline sweep, nine harnesses | **808 passed, 0 failed** |
| Mutation probes | **25 of 25 turn a suite red** |
| Play Mode mutation probes | `sharedcommand` +12, `noprofileguard` +8 |

The Play Mode suite is itself probed, because a suite whose value rests on being able to fail must be
shown to fail. `sharedcommand` (every engine reads the linked scalar) collapses both engines onto
32.4699 and turns it red by 12; `noprofileguard` (the D-P02-2 fix removed) turns it red by 8, showing
`True -> True` across the profile's destruction. Both were also observed failing naturally during
development, four runs in a row, before the apparatus was correct.

The offline sweep runner needed fixing too: the harnesses disagree about how they print totals.
`aoacheck` prints three independent sub-suite lines (16 + 34 + 31 = 81) while `propcheck` prints two
sub-suites and then a combined total (195 + 20, then 215). Summing every line reports 430 for
propcheck; taking the last line reports 31 for aoacheck. Both mistakes were made, in that order. The
runner now decides per harness — if the last line equals the sum of the ones before it, it is a
combined total — and prints which shape it found.

### 26.11 Remaining blockers

Unchanged from section 25.11, minus the inference note, which is now closed:

| ID | Gap | Blocks |
|---|---|---|
| ENG-005 | F-16 thrust deck not frozen | powered F-16 |
| ENG-006 | F-15 engine variant not selected | all F-15 propulsion |
| ENG-007 / 008 / 009 / 010 | fuel flow, rotor momentum value, inlet effects, engine states | later phases |
| ENG-012 | `F16_PROPULSION_REFERENCE_V0.1.md` still calls existing infrastructure deferred | documentation |
| ENG-015 | 515 project-wide `CS0618` warnings (514 runtime + 1 editor-assembly site), pre-existing, none in propulsion | code hygiene |
| — | `MavF16EnginePowerModel` shim still the component auto-setup installs | P1 |

**Play Mode lifecycle items B and C are CLOSED**, by direct observation rather than inference. Nothing
in the registration lifecycle now rests on inference except the player build itself, which cannot be
checked without producing one — and mode 1 demonstrates the exact hook that build would use.

What remains genuinely unmeasured: behaviour in a **built player**, as opposed to editor Play Mode.
IL2CPP or Mono AOT could differ from the editor's JIT in allocation behaviour, and no build was made.
