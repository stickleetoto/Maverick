# F-15 R3 Flight Control System — Implementation Status V0.1

Status: **ARCHITECTURE COMPLETE — ZERO SOURCED GAINS — NOT LIVE**

Branch: `claude/f15-full-implementation`

Date: 2026-09-22

Scope: the F-15-specific flight-control-system layer under
`Assets/MaverickFresh/Scripts/FlightDynamics/F15/`.

---

## 1. Summary

The full F-15 control-system architecture is implemented, from pilot command to requested surface
state, with eleven individually-gated stages. **Not one gain in it is sourced.** With full stick,
full pedal and body rates present, the law outputs neutral surfaces and names every missing stage.

That is the correct outcome, not a shortfall. No F-15 FCS gain, gearing, ratio-changer schedule or
limiter value has been recovered for NASA F-15B 836, and none was invented to fill the gap.

| | |
|---|---|
| FCS stages implemented | **11** |
| Stages with a sourced numeric schedule | **0** |
| Numeric values added to the repository this pass | **1** (a reported endpoint, not wired in) |
| Exact NASA 836 FCS content | **none** |
| Tests | **134 passed, 0 failed** in Unity 6000.3.16f1 headless |

---

## 2. The control chain as built

```
MavF15PilotCommand              normalized intent + per-axis CAS engage
      |
      v
mechanical path                 stick/pedal gearing -> surface degrees
      |
      v
PRAD / RRAD                     ratio changers, MECHANICAL authority only
      |
      v
MavF15ControlDemand             <-- named boundary: mechanical demand, pre-augmentation
      |
      v
pitch CAS -> stall inhibitor -> roll CAS (x washout) -> yaw CAS
      -> turn coordination -> ARI -> roll-to-yaw crossfeed
      |
      v
MavF15RequestedSurfaceState     <-- what the law ASKS for
      |
      v
MavF15ControlActuator           travel + rate authority, the only actual-state owner
      |
      v
MavF15ActualSurfaceState        <-- what the surfaces ARE
      |
      v
MavF15AeroModel                 consumes ACTUAL state only
```

`MavF15ControlDemand` exists because the ratio changers scale mechanical authority *only*. Without
a named boundary between mechanical demand and augmented demand it is very easy to write a ratio
changer that also attenuates the damper — which would be wrong and would not look wrong.

`MavF15RequestedSurfaceState` and `MavF15ActualSurfaceState` carry the same four numbers but are
**distinct types**. That makes handing a request to the aerodynamic model a compile error rather
than a silent bug, which is the ownership invariant this project keeps.

---

## 3. Stages implemented

Every stage reports itself through `MavF15FcsStageResult`: whether it ran, what it contributed in
degrees, the provenance of the gains it used, and why it did not run if it did not.

| # | stage | axis | inputs | source for its numbers | provenance | state |
|---|---|---|---|---|---|---|
| 0 | `MechanicalPath` | all | stick, pedal | none | Unavailable | architecture only |
| 1 | `PitchRatioChanger` (PRAD) | pitch | mechanical demand | none | Unavailable | architecture only |
| 2 | `RollRatioChanger` (RRAD) | roll | mechanical demand | none | Unavailable | architecture only |
| 3 | `PitchCas` | pitch | q, Nz | none | Unavailable | architecture only |
| 8 | `StallInhibitor` | pitch | alpha | none | Unavailable | architecture only |
| 4 | `RollCas` | roll | p, washout | none | Unavailable | architecture only |
| 7 | `HighAoaRollDamperWashout` | roll | alpha | **one reported endpoint**, see §5 | CrossValidationOnly if wired | architecture + 1 datum |
| 5 | `YawCas` | yaw | r | none | Unavailable | architecture only |
| 9 | `TurnCoordination` | yaw | r, bank, V | none | Unavailable | architecture only |
| 6 | `AileronRudderInterconnect` | cross | roll **command** | none | Unavailable | architecture only |
| 10 | `RollToYawCrossfeed` | cross | roll **rate** | none | Unavailable | architecture only |

Four behaviours are worth recording because they are design decisions, not consequences:

**ARI is fed from commanded roll, not augmented aileron.** The ARI coordinates the pilot's roll
demand. Feeding it the damper's output would close an unintended loop through the roll axis, and a
roll rate with the stick centred would produce rudder. `[C6]` holds this.

**ARI and roll-to-yaw crossfeed are separate stages.** The ARI is driven by roll *command*; the
crossfeed by roll *rate*. Both appear in F-15-family control-system descriptions and they are not
the same path. `[C6]` shows they gate independently.

**Turn coordination requires a valid attitude.** The coordinated yaw rate is `g·tan(φ)/V`, and a
body with no published attitude has no bank angle. Running it against `φ=0` would silently make it
a second yaw damper, so it is skipped instead.

**Load-factor feedback is skipped while the accelerometer reads invalid.** A zero from an unflown
body is not a real 0 g. `[C5]` holds both halves of this.

### Open modelling question

The ARI takes the raw mechanical roll command, so it is **not** scaled by RRAD. Whether the real
F-15 crossfeeds before or after the roll ratio changer is not established by any source available
here. The choice is marked in the code rather than buried, and has no numeric consequence today
because both gains are Unavailable. `DN-1180.01-238-458 Rev. D` would settle it.

---

## 4. Provenance and FCS modes

### Mode declares a floor, and the law refuses below it

`MavF15FcsMode` is not a preset. Each mode declares the lowest provenance grade a gain may carry
and still run; any *declared* gain below the floor makes the whole law refuse and output neutral.

| mode | floor | accepts | intended body of evidence |
|---|---|---|---|
| `ExactNasa836Unavailable` *(default)* | `Authoritative` | Authoritative only | exact NASA 836 — nothing qualifies today |
| `F15FamilyReference` | `PublicReference` | Authoritative, PublicReference | NASA TM-72861 lineage |
| `AFITResearch` | `CrossValidationOnly` | + CrossValidationOnly, Approximate, MaverickTuning | AFIT/Davison; pairs with the Baumann M=0.6 aero |

`Unavailable` is admitted by **no** mode — an absent number is not a low-grade one, it simply means
its stage will not run.

This is the anti-mixing mechanism the brief asked for. A research-grade gain sitting inside an
otherwise `F15FamilyReference` configuration is refused outright rather than partially applied,
because a half-applied mix is the hardest kind to notice afterwards. `[C12]` holds it.

### Mapping to the brief's vocabulary

The brief named four levels. The project already has an equivalent enum, `MavEngineDataProvenance`,
whose vocabulary is aircraft-data-generic despite the name. It is reused rather than duplicated, so
there is one provenance vocabulary across propulsion, surfaces and the FCS:

| brief | project enum |
|---|---|
| `AuthoritativeExactTarget` | `Authoritative` |
| `PublicReference` | `PublicReference` |
| `ResearchCrossValidation` | `CrossValidationOnly` |
| `Unavailable` | `Unavailable` |

The enum's two extra middle grades (`Approximate`, `MaverickTuning`) sit below `CrossValidationOnly`
and are admitted only in `AFITResearch`.

### TM-72861 restriction is enforced, not just documented

A gain taken from NASA TM-72861 grades as `PublicReference`, never `Authoritative`, because that
aircraft was preproduction F-15 No. 8. `IsFullyExactTargetAuthoritative` requires **every** gain
*and* the washout schedule to be `Authoritative`; one downgrade anywhere revokes the NASA 836 claim
for the whole law. `[C8]` holds it, including the case where only the washout is downgraded.

---

## 5. Exact numeric data used

### The only number added this pass

`MavF15RollDamperSchedule.ReportedAfitZeroAuthorityAlphaDeg = 20.2f`

The brief states that the F-15 roll damper is scheduled out with increasing alpha and reaches zero
gain at approximately 20.2° in the AFIT/Davison model.

**This could not be verified against any source in this repository, and it is not wired in.**

The Davison material available here is Appendix C (printed pages 111–140), which was audited in
full during the previous pass. It is an **open-loop** wing-rock simulation: its driver takes
control-surface deflections as fixed parameters — `PAR(1)=DELESD, PAR(2)=DRUDD, PAR(3)=DDA` on
printed page 115 — and its four subroutines are the driver, the equations of motion (`FUNX`), a
Hamming integrator, and the aerodynamic coefficient routine (`COEFF`). There is no control system
anywhere in it, and no roll-damper schedule. The 20.2° figure would be in the thesis body, which
was not supplied.

It is therefore carried as a **named constant with the situation stated on it**, graded
`CrossValidationOnly` at best, and nothing reads it unless someone assigns it deliberately.

### One endpoint is not a schedule

`MavF15RollDamperSchedule` requires **three** things before it produces a number: a full-authority
alpha, a zero-authority alpha, and a declared curve `shape`. Supplying only the 20.2° zero point
leaves the stage unavailable — because knowing where a fade ends says nothing about where it begins
or what curve it takes. `[C7]` asserts exactly that: a schedule carrying only the reported endpoint
is **not** available.

`AfitResearch(fullAuthorityAlpha, shape, citation)` exists so that the moment someone can cite
where the fade begins, wiring it is one call. That factory pins provenance to
`CrossValidationOnly` and **cannot** raise it to exact-target authority.

When fully declared, the schedule is validated to be monotonic non-increasing, to hold full
authority below the start alpha, to reach **exactly zero at the declared zero point**, and to
saturate at zero beyond it rather than going negative and driving the roll.

### Everything else

Nothing. No gearing, no ratio-changer schedule, no CAS gain, no ARI gain, no limiter, no actuator
rate, no travel limit. All `Unavailable`.

---

## 6. Unavailable numeric data — the full list

| datum | stage | best candidate source |
|---|---|---|
| pitch stick → stabilator gearing | MechanicalPath | DN-1180.01-238-458 Rev. D |
| roll stick → aileron gearing | MechanicalPath | DN-1180.01-238-458 Rev. D |
| roll stick → differential stabilator gearing | MechanicalPath | DN-1180.01-238-458 Rev. D |
| pedal → rudder gearing | MechanicalPath | DN-1180.01-238-458 Rev. D |
| PRAD schedule | PitchRatioChanger | DN-1180.01-238-458 Rev. D / TM-72861 |
| RRAD schedule | RollRatioChanger | DN-1180.01-238-458 Rev. D / TM-72861 |
| pitch-rate feedback gain | PitchCas | TM-72861 (→ PublicReference) |
| normal-acceleration feedback gain | PitchCas | TM-72861 (→ PublicReference) |
| roll-rate feedback gain | RollCas | TM-72861 (→ PublicReference) |
| yaw-rate feedback gain | YawCas | TM-72861 (→ PublicReference) |
| ARI gain/schedule | ARI | DN-1180.01-238-458 Rev. D |
| roll-to-yaw crossfeed gain | RollToYawCrossfeed | DN-1180.01-238-458 Rev. D |
| turn-coordination gain | TurnCoordination | DN-1180.01-238-458 Rev. D |
| stall-inhibitor alpha threshold | StallInhibitor | DN-1180.01-238-458 Rev. D |
| stall-inhibitor authority gradient | StallInhibitor | DN-1180.01-238-458 Rev. D |
| washout **start** alpha and curve shape | HighAoaRollDamperWashout | Davison thesis body |
| surface travel limits (4 channels) | actuator | MDC A4172 / DN-1180.01-238-458 Rev. D |
| actuator rate limits (4 channels) | actuator | DN-1180.01-238-458 Rev. D |
| actuator lag / servo dynamics | actuator | DN-1180.01-238-458 Rev. D |

---

## 7. Exact NASA 836 vs family/reference

**Exact NASA 836 FCS content in this branch: none.**

Nothing in the control system is `Authoritative`. The default mode is `ExactNasa836Unavailable`,
which admits only `Authoritative` gains, of which there are zero — so the default configuration
produces neutral surfaces by construction rather than by a flag someone could flip.

**Reference / research-only content:**

- The whole stage architecture — structural, carries no aircraft numbers.
- The reported 20.2° washout endpoint, `CrossValidationOnly`, **not wired in**.
- Every synthetic gain in the fixtures, marked `SYNTHETIC validation value. Not F-15 data.`,
  existing only to drive the architecture.

---

## 8. Tests

**RUN — Unity 6000.3.16f1 headless**, via `MavFdmValidationBatchAdapter`:

| suite | result |
|---|---|
| `MavF15ControlPathValidation.RunAll` | **79 passed, 0 failed** |
| `MavF15BaumannTranscriptionValidation.RunAll` | **28 passed, 0 failed** |
| `MavF15PropulsionValidation.RunAll` | **27 passed, 0 failed** |
| **total** | **134 passed, 0 failed** |

All 306 runtime scripts compile with **0 errors**.

The control-path suite grew from 41 to 79 checks. Every item the brief listed is covered:

| brief requirement | fixture |
|---|---|
| neutral command → neutral requested surfaces | `[C4]` |
| unavailable gain cannot create control authority | `[C4]`, `[C5]`, `[C6]` |
| pitch command maps only through intended stages | `[C10]` |
| roll command reaches aileron + differential stabilator | `[C10]` |
| yaw command reaches rudder | `[C10]` |
| ARI contributes only when its configuration enables it | `[C6]` |
| CAS disabled → zero CAS increment | `[C11]` |
| actuator remains sole actual-surface owner | `[C15]` |
| rate-limited surfaces cannot teleport | `[C3]` |
| dt=0 cannot teleport rate-limited surfaces | `[C3]` |
| washout reduces authority monotonically | `[C7]` |
| washed-out gain reaches the source-defined zero point | `[C7]` |
| no FCS code writes Rigidbody forces or torques | `[C13]` |
| all outputs finite | `[C14]` |

`[C13]` runs the existing `MavFlightDynamicsOwnershipScan` over the source tree — 104 files scanned,
clean. A source scan rather than a runtime assertion, because the rule is "no component may EVER
apply a force" and no runtime test can prove a negative about code paths it happens not to execute.

**NOT RUN:**

- Unity play mode. No scene, prefab, Rigidbody or `MavSixDoFBody` integration was exercised.
- Any flight, trim or trajectory test. There is nothing to fly: no sourced gearing, no thrust.
- Any comparison against TM-72861 response traces. No gains are sourced to compare.

---

## 9. Not attempted, deliberately

**Live ownership / Shadow mode.** Explicitly out of scope this pass, and it would be meaningless
anyway: a replacement owner has nothing to own while coefficients are zero in the default aero
mode, the control law outputs neutral, and thrust is zero.

**Scenes and prefabs.** Untouched.

**Any thrust value.** Untouched — no fake thrust was added to make anything move.

**Aerodynamic coefficients.** Frozen by instruction and not reopened. The two corrections from the
previous pass stand; no coefficient was retuned.

---

## 10. Remaining blockers, ranked

1. **`DN-1180.01-238-458 Rev. D` — F-15 Flight Control System Description.** Would close *fifteen*
   of the nineteen unavailable data items in one document: all four gearings, both ratio changers,
   the ARI, both crossfeeds, the stall inhibitor, and the actuator travel/rate/servo set. **By far
   the highest-value item for this layer.**
2. **NASA TM-72861.** Would supply the four CAS feedback gains at `PublicReference` grade —
   immediately usable in `F15FamilyReference` mode, and never promotable to exact-target without
   demonstrated equivalence to 836.
3. **Davison thesis body (chapters, not Appendix C).** Would supply the roll-damper washout start
   alpha and curve shape, making the 20.2° endpoint usable.
4. **`MDC A4172 Part II`.** Still the top blocker for the *aerodynamic* layer (`S`, `c̄`, the
   coefficient database) and a candidate for control travel limits.

A complete architecture with unavailable gains is the deliverable here. The next useful work on
this layer is sourcing, not coding: the stages, the gating, the provenance plumbing and the
fixtures are all in place, so each schedule that arrives is a data change.
