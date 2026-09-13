# Maverick Shared Propulsion — Profile + Engine Architecture v0.2

Status: **IMPLEMENTED (P0/P1/P2), HARDENED (P0.1), UNITY-VALIDATED (P0.2)** — the F-16 migration
retains one transitional shim, and no engine thrust data is frozen for any aircraft.

Unity `6000.3.16f1` compiles the project with **zero errors** and the editor validation suite passes
**73/73**. P0.1's claim that no editor was installed was wrong: it is at a non-standard path. See
section 25 of the implementation report.

Supersedes `SHARED_PROPULSION_PROFILE_ARCHITECTURE_V0.1.md`, which remains in the repository as the
original decision record. v0.1 is marked "ARCHITECTURE ROADMAP — NOT YET IMPLEMENTED"; that status is
no longer true, and rather than overwrite the decision it is superseded here.

Implementation detail, file lists and validation results:
`SHARED_PROPULSION_IMPLEMENTATION_REPORT_V0.1.md`.

---

## 1. The decision (unchanged from v0.1)

Profile + Engine. The reusable object is not "the F-16 engine script" — it is the runtime, the profile
format, the validation, and the load-composition code.

An `EngineProfile` may be shared by two aircraft **only** when the exact engine variant and source
assumptions match. Shared infrastructure never implies shared numbers.

---

## 2. As built

```text
MavFlightDynamicsProfile
    propulsionInstallationId, declaredEngineCount     <- IDENTITY ONLY
    MatchesPropulsionInstallation(actual, out reason) <- cross-check, not a runtime link
        |
MavPropulsionInstallationProfile
        engines[]                                     <- engine count is DATA, not a code path
        |
        +-- MavEngineInstallation slot 0
        |       slotId / slotName
        |       engineProfile ---------------------+
        |       positionAeroBodyM                  |   <- r, from the declared physics datum / CG
        |       thrustDirectionAeroBody            |
        |       throttleChannel                    |
        |       enabled                            |
        |       geometryDeclared + provenance      |
        |                                          |
        +-- MavEngineInstallation slot 1 ----------+   <- SAME profile object for a matching twin
                                                   |
                                        MavEngineProfile
                                            identity / variant / source
                                            provenance
                                            powerDynamicsLaw (+ its own provenance)
                                            thrustDeck
                                            sourceEnvelope
                                            augmentation
                                            fuelFlow
                                            rotorAngularMomentum
                                                   |
                              MavEnginePowerDynamicsFactory.Resolve(law)
                                                   |
                                        IMavEnginePowerDynamics     <- STATELESS
                                            MavInstantEnginePowerDynamics
                                            MavF16GarzaMorelliEngineDynamics

MavPropulsionCommand                              <- P0.1: the COMMAND boundary
        BeginStep(pipelineThrottle)                    reset to LINKED every step
        optional MavPropulsionCommandSourceBase        publishes per-engine intent
        ThrottleForEngine(i) / IsCutoff(i)
        |
MavPropulsionSystem : MavPropulsionModelBase
        MavEngineRuntime[]        one per slot, ALL mutable state
        per-engine MavEngineLoadResult[]
        F_total = sum(F_i)
        M_total = sum(r_i x F_i + intrinsic_i)
        |
        v
MavSixDoFBody             <- UNCHANGED. Still the single load-application boundary.
```

### The command boundary (P0.1)

The scalar pilot pipeline is unchanged and still means what it always meant - *the* throttle. Anything
needing per-engine authority publishes into `MavPropulsionCommand` through a
`MavPropulsionCommandSourceBase` component.

P0 delivered independent engine STATE but not independent engine COMMANDS: every stage from
`MavPilotCommand` to `MavPropulsionModelBase.Evaluate` carries one scalar, so a twin could hold two
power states with no production path to being commanded into them. Widening `MavControlInput` would
have touched every control law, actuator and trim solver, put a variable-length array in a serialized
struct, and pushed engine-count knowledge into layers that should not have it. A boundary at the
propulsion layer costs none of that.

No source attached means LINKED, which is all the F-16 needs and the normal case for a twin. The
command is reset to linked *before* the source is consulted, so a source that stops publishing reverts
to linked rather than leaving a stale asymmetric command applied - a stuck differential throttle is a
control failure, a reverted link is not.

### Why the aggregator is a propulsion model

`MavPropulsionSystem` derives from `MavPropulsionModelBase`. `MavSixDoFBody` already held exactly one
propulsion model and added exactly one propulsive contribution per step, so N engines arrive as one
model and **`MavSixDoFBody` needed no change at all**.

Teaching `MavSixDoFBody` about engine arrays would have placed aggregation on both sides of the
ownership boundary. This keeps it on one side.

---

## 3. Ownership rules, as enforced in code

| Rule | Enforced by |
|---|---|
| No engine code touches `Rigidbody` | P-011, via the production `MavFlightDynamicsOwnershipScan` classifier — which also catches a direct velocity/rotation write, not just force calls |
| Mutable state never lives on a profile | structural: `MavEngineProfile` has no state fields; `MavEngineRuntime` holds all of it |
| Two slots sharing a profile still get two runtimes | `MavPropulsionSystem.Build` constructs one runtime per slot index; P-004, P-013 |
| Net propulsion moment comes from geometry | the only moment term is `Cross(positionAeroBodyM, force)`; P-003, P-012 |
| Missing data returns zero, never a plausible number | `MavEngineRuntime` returns zero thrust with `thrustAuthoritative = false` when the deck is null or `Unavailable`; P-006 |
| Aggregate authority is unanimous over CONTRIBUTING engines | `MavPropulsionSystem.Evaluate`; P-007. The configuration-level question is `HasAuthoritativeData`; see section 3.1 |
| An unresolvable law fails closed | `Resolve` returns null, `Build` refuses and names the law; P-014, P-018 |
| Per-engine commands reach engines only through the command boundary | `MavPropulsionSystem.Evaluate` reads `MavPropulsionCommand`; P-015 |
| No source means linked, and a silent source reverts to linked | `BeginStep` precedes the source call; P-016 |
| Operational state never changes provenance | cutoff/failed/disabled/idle are counted separately; P-017 |
| Each live-readiness condition is individually load bearing | P-023 knocks out all nine in turn |
| Registration is idempotent and self-healing | `IsRegistered` asks the factory, not a cached flag; P-018f, P-019 |
| A declared power law must carry provenance | `MavEngineProfile.IsValid` |
| A declared installation geometry must carry provenance | `MavEngineInstallation.IsValid` |

### Provenance vocabulary, now a type

`MavEngineDataProvenance` promotes the six labels from `Docs/Reference/README.md` into an enum:
`Unavailable` (the **zero value**, so a forgotten field reads as unsourced), `MaverickTuning`,
`Approximate`, `CrossValidationOnly`, `PublicReference`, `Authoritative`.

Thrust provenance is asked of the **deck**, never of the profile's own paperwork. The F-16 is exactly
that case: `PublicReference` power dynamics, no frozen deck, zero thrust.

### Three acceptance gates, each stricter

1. `HasAuthoritativeThrustData` — the deck is `Authoritative`.
2. aggregate `hasAuthoritativeData` — **every** contributing engine is, because the summed force
   contains every engine's number.
3. `IsAcceptableForLiveFlight` — plus a non-extrapolating deck, a **declared** source envelope, and
   **measured** installation geometry. An undeclared envelope is not a wide envelope.

### 3.1 One flag, one question (P0.1)

| Flag | Question | Operation-dependent? |
|---|---|---|
| `MavPropulsiveLoads.hasAuthoritativeData` | is every number **in these loads** sourced? | yes — it describes this step |
| `MavPropulsionSystem.HasAuthoritativeData` | does **every installed engine** have sourced data? | **no** — configuration |
| `installation.IsAcceptableForLiveFlight(...)` | plus non-extrapolating deck, declared envelope, measured geometry | **no** |

P0 had the per-step flag also requiring every *installed* profile to be authoritative, which sounds
stricter and is a different question wearing this one’s name. A live-flight gate reading the conflated
version would have flipped the moment a pilot started a second, unsourced engine. P-022 is the
discriminating case: an unsourced engine that is cut off leaves the per-step flag true and the
configuration answer false.

---

## 4. F-16 and F-15, as built

### F-16 — migrated, one shim retained

```text
MavF16PropulsionSystem : MavPropulsionSystem
  -> MavF16PropulsionInstallation.CreateInstallation(deck)
       slot 0, thrust along body +X, position (0,0,0), geometryDeclared = FALSE
       -> MavEngineProfile: F-16 reference engine, PublicReference
            powerDynamicsLaw = F16GarzaMorelliPowerState
            thrustDeck = null  ->  thrust exactly zero
```

Sourced throttle gearing, the piecewise actual-power derivative and the RTAU schedule are preserved
exactly and now exist in **one** place: `MavF16GarzaMorelliEngineDynamics`.
`MavF16EnginePowerModel` is a **transitional shim** whose statics forward there; it holds no equations.
It is still what `MavF16SelectionAutoSetup` installs, so live behaviour is unchanged.

The `(0,0,0)` mount with `geometryDeclared = false` is deliberate: it reproduces the old zero-moment
behaviour while recording that a centreline thrust line is an assumption, not a measurement.

### F-15 — shape only

```text
MavF15PropulsionSkeleton.CreateTwinSkeleton()
       slot 0 "left",  channel 0  ---+
       slot 1 "right", channel 1  ---+--> ONE MavEngineProfile object
                                           provenance     = Unavailable
                                           powerDynamicsLaw = InstantNoSourcedTransient
                                           thrustDeck     = null
       both: positionAeroBodyM = zero, geometryDeclared = FALSE
```

It does **not** borrow the F-16 power law. Garza/Morelli is F-16 reference-engine behaviour; applying
it to an F100-family engine would be an unproven claim (ENG-006).

Zero mount offsets have a useful property: an engine-out condition on the skeleton produces **no**
yawing moment, which is obviously wrong and so cannot be mistaken for a working F-15. Asymmetric
thrust is proven with clearly labelled synthetic geometry instead.

---

## 5. Gap status against v0.1

| ID | v0.1 | Now |
|---|---|---|
| ENG-001 | F-16 identity and engine law coupled | **CLOSED.** Law selected by profile, implemented behind `IMavEnginePowerDynamics` |
| ENG-002 | one throttle / one engine state baked in | **CLOSED.** One runtime per slot + aircraft-level aggregator + throttle channels |
| ENG-003 | no installation geometry | **CLOSED.** Position, direction, and `r x F` composition |
| ENG-004 | no per-engine telemetry | **CLOSED.** `MavEngineLoadResult[]` plus aggregate counters |
| ENG-005 | F-16 thrust not frozen | **OPEN.** Deck interface ready; TP-1538 Table VI transcription not frozen |
| ENG-006 | F-15 variant not selected | **OPEN.** Skeleton keeps it `Unavailable` |
| ENG-007 | fuel flow absent | **OPEN.** `MavEngineFuelFlowCapability` declared, no data |
| ENG-008 | rotor angular momentum absent | **PARTIAL.** Channel exists and is applied when `available`; no aircraft declares it |
| ENG-009 | inlet recovery absent | **OPEN** |
| ENG-010 | engine states minimal | **PARTIAL.** `SetFailed` exists for architecture validation; no failure gameplay |
| ENG-011 | afterburner semantics not generic | **CLOSED.** `MavEngineAugmentationSemantics`, profile-defined |
| ENG-012 | old propulsion doc stale | **OPEN.** `F16_PROPULSION_REFERENCE_V0.1.md` not yet updated |

### New, found while implementing

**ENG-013 — CLOSED in P0.1.** Unity serializes `[Serializable]` class fields **by value**, so a twin
authored in the inspector with both slots pointing at one profile would have deserialized as two
independent copies that then drift as someone edits one — while the code still claimed they were the
same engine.

`MavEngineProfile` is now a **`ScriptableObject`**, so a serialized reference is a genuine object
reference: one asset, one definition, one provenance record, no drift. Converted before any scene or
prefab authoring began, which was the only cheap moment to do it.

The asset makes the no-state rule MORE important rather than less: an asset is shared by every aircraft
that references it, so a state field there would be shared globally rather than per-aircraft. P-020
checks the rule behaviourally and at source level, because no behavioural test can prove a field is
absent.

`MavEngineProfile.CreateInMemory(id)` is the only sanctioned code construction path. No engine assets
were created and no engine data was invented.

**ENG-014 — CLOSED in P0.2.** The editor was found at a non-standard path, the project compiles with
zero errors, and the Unity validation suite passes 73/73. Everything the offline harness could not
reach is now covered: real `ScriptableObject` serialization and fake-null, the compiled assembly split,
domain-reload registration, GC allocation per step, and Rigidbody ownership checked against compiled
metadata rather than source text.

The Unity run found three defects the harness structurally could not — a file that never compiled, a
destroyed profile reporting authoritative data, and 907 bytes/step of telemetry allocation. All fixed;
see implementation report section 25.

**ENG-015 — 514 project-wide `CS0618` warnings.** `Object.FindObjectOfType` is deprecated in Unity 6 and
is used throughout pre-existing weapons, CAS, aircraft and UI code. **None are in propulsion files.**
Recorded rather than fixed: 514 call sites across systems this phase may not modify.

---

## 6. Migration sequence, updated

- **P0 architecture freeze** — DONE
- **P1 extract F-16 engine profile without changing results** — DONE, shim retained.
  **Remaining:** repoint `MavF16SelectionAutoSetup` at `MavF16PropulsionSystem`, migrate the suites
  that reference `MavF16EnginePowerModel`, confirm no behaviour change in the editor, delete the shim.
- **P2 generic engine array / installation loads** — DONE
- **P3 F-16 sourced thrust deck** — NEXT after the shim is retired
- **P4 F-15 engine-profile freeze** — gated on F15-R0 configuration identity
- **P5 powered F-15 reference**
- **P6 fuel / gyro / inlet extensions**

---

## 7. Hard rules

Unchanged from v0.1, with one addition.

1. No engine runtime may call `Rigidbody` directly.
2. One engine profile may be reused across aircraft only when the exact variant/source identity is
   compatible.
3. Engine installation belongs to the aircraft profile, not the bare engine profile.
4. Twin engines always have independent runtime state, even when they share one profile.
5. Net propulsion moment derives from each engine force and installation geometry, never hand-tuned
   yaw torque.
6. Missing source data returns unavailable/zero or a clearly labelled approximation; never a
   plausible-looking "accurate" number.
7. Gameplay tuning stays above/outside the reference engine layer.
8. F-16 and F-15 use the same generic infrastructure; aircraft-specific code supplies profiles and
   models, not a second engine core.
9. **an unresolvable or undeclared model selection fails closed.** A missing power law returns null
   and refuses to build. It is never silently replaced by a different law: an aircraft flying a model
   its profile does not declare is worse than an aircraft that will not start.
10. **NEW in P0.1 — operational state is never provenance.** Commanded cutoff, engine failure, a
    disabled installation and zero throttle are OPERATIONAL states. They change the loads; they do not
    change whether the data behind those loads is sourced. An authoritative engine that is switched off
    has a thrust of zero that we know, not one we lack, and conflating the two would make "the pilot
    shut it down" indistinguishable from "we have no engine data".
11. **NEW in P0.1 — one flag, one question.** `MavPropulsiveLoads.hasAuthoritativeData` describes THIS
    STEP’s numbers. `MavPropulsionSystem.HasAuthoritativeData` and
    `installation.IsAcceptableForLiveFlight` describe the CONFIGURATION and are operation-independent.
    A gate reading the per-step flag for a configuration question would flip the moment a pilot started
    a second, unsourced engine.
