# F-15 R3 Implementation Status V0.1

Status: **ARCHITECTURE COMPLETE TO THE LIMIT OF AVAILABLE SOURCES — NOT LIVE-READY**

Branch: `claude/f15-full-implementation`

Base: `sol/f15-r2-lateral` @ `2fdd442`

Date: 2026-09-22

Supersedes nothing. Extends `F15_R1_IMPLEMENTATION_STATUS_V0.1.md` and the two R2 aero documents.

---

## 1. Where this got to

The handoff asked to carry the F-15 as far as the available public evidence defensibly permits.
The honest answer is that the evidence permits a **complete ownership architecture and almost no
aircraft data**, so that is what was built.

| layer | architecture | exact NASA 836 data |
|---|---|---|
| aerodynamics | research model transcribed, audited, domain-gated | **none** — coefficient database not recovered |
| surface state | four-channel owner, provenance-carrying limits | **none** — travel/sign authority not frozen |
| FCS | full stage chain, per-stage gating | **none** — no gain recovered |
| actuator | rate/travel owner, finite-rate path | **none** — actuator dynamics not sourced |
| propulsion | twin installation, independent runtimes | **none** — no F100-PW-100 deck accepted |
| profile | built, fail-closed | partial — mass/inertia frozen; coefficient reference S, c̄ and b missing (the 42.8-ft span is physical, recorded separately — later correction) |

Every one of those "none" entries is a declared `Unavailable`, not a zero someone will mistake for
a measurement. The aircraft cannot currently be flown. That is the intended outcome of the source
policy, not a gap in the implementation.

---

## 2. Phases completed

### Phase 1 — R2 transcription audit and domain gate  (`cea9b9a`)

Run before the source was available. A structural audit stood in for the
coefficient-by-coefficient comparison, and added `MavF15BaumannMach06Domain` (**F15-AUDIT-002**)
because the 6th- to 9th-order fits diverge rather than degrade outside their fitted region —
**Cm reaches −730 at α=180°**, which the finiteness check cannot see. That gate stands and is
now better justified than when it was written.

It also reported **F15-AUDIT-001** as a defect and "fixed" it. Phase 4 showed that was wrong.

### Phase 4 — source-verified transcription audit  (this commit)

Davison AFIT/GAE/ENY/92M-01 Appendix C was supplied as original page images, so the blocked audit
was completed: **34 coefficient families, ~330 numeric literals**, verified symbol by symbol
against the scans at up to 22× magnification.

- **2 corrections**, both single wrong digits with negligible numerical effect
  (F15-AUDIT-009, F15-AUDIT-010).
- **6 previously-suspicious items resolved as source-confirmed** — including the duplicated CMN1
  monomial, which is genuinely printed twice in the source, and the EPA02S/EPA02L assignment,
  which the transcription already had right.
- **F15-AUDIT-001 withdrawn.** The source guards those terms only from below; the transcription
  was faithful and Phase 1's added guard was a silent deviation from source. It has been removed,
  and the domain gate's 90° bound is now documented as load-bearing.
- **2 residual ambiguities** from physical scan damage, both consistent with the transcribed
  digit, neither changed.

Full per-symbol table with page references in **`F15_R2_TRANSCRIPTION_AUDIT_V0.1.md`** (rev V0.2).

### Phase 2 — F-15 surface-state and control-path ownership  (`060cef7`)

The chain the handoff specified now exists end to end:

```
pilot command -> MavF15ControlLaw -> MavF15SurfaceState (requested)
              -> MavF15ControlActuator -> MavF15SurfaceState (actual)
              -> MavF15AeroModel -> coefficients -> MavSixDoFBody -> Rigidbody
```

- `MavF15SurfaceState` carries symmetric stabilator, **differential stabilator**, aileron and
  rudder. The shared core is untouched — it keeps consuming coefficients and loads, which really
  are aircraft-independent.
- Differential stabilator is never folded into the generic aileron field. A consumer reading that
  field would otherwise be reading two physical surfaces added together.
- The AFIT research relation `DTALD = 0.3·DAILD` survives only as a clearly-labelled fallback for
  a rig with no actuator bound, and `MavF15AeroModel.debugSurfaceSource` says which path was taken.
- `MavF15ControlLaw` implements the whole stage chain — mechanical path, PRAD, RRAD, pitch/roll/yaw
  CAS, ARI, high-AOA roll-damper washout — each gated on its own gains.

Two behaviours worth recording:

- The **ARI is crossfed from commanded roll**, not from the augmented aileron position, so it
  cannot close an unintended loop through the roll damper. `[C6]` holds this.
- **Load-factor feedback is skipped while the accelerometer reads invalid.** A zero from an unflown
  body is not a real 0 g, and closing a loop on it would make the CAS fight a phantom error.

Writing the fixtures found a defect in the new actuator: with a sourced rate and a zero timestep,
the rate branch was guarded on `dt > 0` and fell through to the instant branch, teleporting a
rate-limited surface to its target. Fixed; `[C3]` holds it.

### Phase 3 — twin F100-PW-100 installation  (this commit)

The shared `MavPropulsionSystem` was already fully multi-engine, so this is a thin configuration
layer rather than a parallel implementation — two slots, two throttle channels, two independent
runtimes, one shared profile object.

The substantive contribution is naming **the engine-out trap**. Both mounts sit at the origin
because their coordinates are undeclared. With zero thrust that is harmless — `r × F` is zero
because `F` is zero. It stops being harmless the moment a thrust deck is attached: force becomes
real, `r` is still zero, and the aircraft quietly models both engines on the centreline. An engine
failure would then produce **no yaw at all**, and nothing about `0 N·m` would look wrong.

`MavPropulsionSystem.IsAcceptableForLiveFlight` already refuses undeclared geometry, and
`MavF15PropulsionSystem.DescribeInstallationGaps()` now spells it out in words. `[E4]` holds the
gate shut.

Measuring those coordinates off the F-15E visual mesh in this repository would **not** close the
gap: the physics target is NASA F-15B 836, and the mesh is a different aircraft.

---

## 3. What is exact NASA 836 authority today

**Nothing new.** This work added no exact-target datum, because none was recoverable from what is
in the repository.

Carried forward unchanged from R1:

- mass, CG and inertia for the frozen mass state;
- physical wing span (not the coefficient reference span; corrected in the reference-geometry audit);
- external dimensions;
- engine count and variant identity (two F100-PW-100).

Still missing, and still blocking a live exact-target profile:

- coefficient-reference wing area `S` and mean aerodynamic chord `c̄`;
- the coefficient database itself;
- control travel, sign authority, actuator rates and servo dynamics;
- every FCS gain and schedule;
- engine thrust, spool, inlet and fuel-flow data;
- engine mount coordinates and thrust-line offsets.

---

## 4. What is research-only

- The AFIT/Baumann aerodynamic model, fixed at **M=0.6 / 20,000 ft**, explicitly opt-in, and now
  also gated on alpha and beta. It is cross-validation material and must never be presented as the
  NASA 836 database.
- The `DTALD = 0.3·DAILD` bridge, now demoted to a fallback.
- Every synthetic gain in `MavF15ControlPathValidation`, marked `MaverickTuning`, which exists only
  to drive the architecture in fixtures and asserts nothing about the aircraft.

---

## 5. Provenance mechanics added

The recurring failure mode this project guards against is a number losing its source across edits.
Three types now carry provenance with the value, so the guard survives refactoring:

| type | guards |
|---|---|
| `MavF15SurfaceChannelLimits` | travel and rate authority, separately graded, with self-consistency checks that reject half-filled declarations |
| `MavF15ControlGain` | every FCS gain, with `IsExactTargetAuthority` narrow to `Authoritative` alone |
| `MavF15ControlLawSchedules.IsFullyExactTargetAuthoritative` | the whole law — one downgraded gain anywhere revokes the NASA 836 claim |

`MavEngineDataProvenance` is reused rather than duplicated. Despite the name its vocabulary is
aircraft-data-generic, and its middle grades are exactly the distinction this needs: a gain read
from **NASA TM-72861 is `PublicReference`, never `Authoritative`**, because the test aircraft was
preproduction F-15 No. 8. `[C8]` holds that a fully-populated `PublicReference` set is usable but
still does not claim exact-target authority.

---

## 6. Ownership invariants — held

| invariant | status |
|---|---|
| one live flight-dynamics owner | held — no second SixDoF stack was created |
| `MavSixDoFBody` is the only load-application boundary | held — unchanged by this work |
| aero returns coefficients only | held — `[T11]`; the routine takes no throttle argument at all |
| control law never writes Rigidbody force | held — `MavF15ControlLaw` computes a request and returns it |
| actuator owns actual surface state | held — `[C1]`, `[C3]` |
| propulsion owns thrust | held — thrust terms stay out of the aero path |
| no thrust double-application | held — structurally impossible, `[T11]` |
| live takeover off by default | held — `ExactNasa836Unavailable`, opt-in research mode, no ownership change made |

Runtime ownership migration (Legacy / Shadow / F15Replacement) was **not** attempted. See §8.

---

## 7. Tests

### RUN

All three suites were executed **inside Unity 6000.3.16f1**, headless, through the project's own
`MavFdmValidationBatchAdapter` (`-batchmode -fdmMode sync`). These are real engine runs, not a
simulated host.

| suite | result | evidence |
|---|---|---|
| `MavF15BaumannTranscriptionValidation.RunAll` | **28 passed, 0 failed** | `FDM_VALIDATION_RESULT_V1` status PASS |
| `MavF15ControlPathValidation.RunAll` | **79 passed, 0 failed** | `FDM_VALIDATION_RESULT_V1` status PASS |
| `MavF15PropulsionValidation.RunAll` | **27 passed, 0 failed** | `FDM_VALIDATION_RESULT_V1` status PASS |
| **total** | **134 passed, 0 failed** | |

> The control-path suite grew from 41 to 79 checks in the R3 FCS pass. See
> **`F15_R3_FCS_IMPLEMENTATION_STATUS_V0.1.md`** for the control-system layer, which supersedes
> the FCS parts of §2 Phase 2 below.

The whole project also compiles clean in the editor, and separately all 304 runtime scripts compile
with **0 errors** under Unity's Roslyn outside the editor.

To reproduce any one of them:

```bash
"D:/unitys/6000.3.16f1/Editor/Unity.exe" -batchmode -quit -nographics   -projectPath "E:/unity project/Maverick"   -executeMethod MaverickFresh.FlightDynamics.EditorTools.MavFdmValidationBatchAdapter.RunBatch   -fdmSuite f15-controlpath -fdmMode sync   -fdmType MavF15ControlPathValidation -fdmMethod RunAll   -fdmOut result.json -logFile unity.log
```

Note that `MavF15PropulsionValidation` can *only* run this way: `MavEngineProfile` is a
`ScriptableObject`, so it needs the real Unity runtime.

### NOT RUN

- **Unity Editor play mode.** No scene, prefab, Rigidbody or `MavSixDoFBody` integration was
  exercised by this work.
- **Any flight, trim or trajectory test.** There is nothing to fly: with no sourced gearing the
  control law outputs neutral and with no deck the engines produce no thrust.
- **Any comparison against AFIT source PLOTS or flight-test tables.** The Appendix C code listing
  is now verified, but that is a transcription check, not a behavioural one.

A pass on the fixtures means *internally consistent and structurally sound*. It does not mean
*verified against the source*.

---

## 8. Deliberately not attempted

**Runtime ownership migration (handoff Phase F).** Shadow and replacement modes were not wired.
The reason is that a replacement owner has nothing to own: aerodynamic coefficients are zero in
the default mode, the control law outputs neutral, and thrust is zero. Standing up an ownership
switch around a model that produces no loads would create a live-takeover path whose only testable
property is that it does nothing — while adding a real risk of accidental activation, which the
handoff explicitly warns against. The gate should be built when there is a load to gate.

**A live research profile.** A separate, unmistakably-tagged research flight profile is allowed by
the handoff. It was not built, because the research aero is fixed to one Mach/altitude point and a
profile that can only be evaluated at exactly M=0.6 / 20,000 ft is not a flight profile.

**Unity wiring / configurator (handoff Phase G).** Out of reach for the same reason, and it would
mean touching prefabs, which the scope boundary excludes.

---

## 9. BLOCKED — what would unblock what

Ranked by how much each source unlocks.

| # | source | unblocks |
|---|---|---|
| ~~1~~ | ~~Davison AFIT/GAE/ENY/92M-01 Appendix C~~ | **SUPPLIED AND CLOSED** — see the audit document |
| 1 | **MDC A4172 Part II** — *F/TF-15 Stability Derivatives, Mass and Inertia Characteristics* | the exact NASA 836 coefficient database, `S` and `c̄`; would make the exact-target profile valid and everything downstream live-capable. **Now the top blocker.** |
| 3 | **DN-1180.01-238-458 Rev. D** — *F-15 Flight Control System Description* | mechanical gearing, PRAD/RRAD, CAS gains, ARI, limiters, actuator travel and rates — the entire `MavF15ControlLawSchedules` set at once |
| 4 | **NASA TP-1373 / TM-X-3261 / TP-1034** | ~~an F100-PW-100 thrust deck~~ — *read in R5: they give the thrust characteristic's shape, not its scale* (`F15_R5_F100_PROPULSION_STATUS_V0.1.md`) |
| 5 | **NASA 836 engine installation geometry** (any configuration-matched source) | mount coordinates and thrust-line offsets; without it engine-out yaw stays unmodelled and `r × F` stays zero by absence |
| 6 | **NASA TM-72861** | F-15 FCS architecture, CAS authorities and a few gearings — **no numeric feedback gains** (corrected; `F15_DN1180_SOURCE_LINEAGE_V0.1.md` §6). Preproduction F-15 No. 8, so never exact-target authority |
| 7 | **Nolan II, AFIT/GAE/ENY/92J-02** | independent cross-check on the Davison transcription |

Item 1 is the cheapest and the highest-value: it is one appendix, and it would convert the largest
body of already-implemented code in this branch from "candidate transcription" to "verified".

---

## 10. Recommended next action

Davison Appendix C is closed. The top blocker is now **MDC A4172 Part II**, which carries the
exact NASA 836 coefficient database along with `S` and `c̄` — the two values that currently keep
the exact-target profile invalid and therefore keep everything downstream from going live.

The position is otherwise unchanged: the architecture is ahead of the data, which is the correct
place for it to be, and further implementation would mostly consist of inventing numbers to fill
it. Verifying the Baumann transcription raised confidence in the research model; it did not move
the research model any closer to being NASA 836.
