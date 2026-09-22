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
| profile | built, fail-closed | partial — mass/inertia/span frozen; S and c̄ missing |

Every one of those "none" entries is a declared `Unavailable`, not a zero someone will mistake for
a measurement. The aircraft cannot currently be flown. That is the intended outcome of the source
policy, not a gap in the implementation.

---

## 2. Phases completed

### Phase 1 — R2 transcription audit and domain gate  (`cea9b9a`)

The coefficient-by-coefficient comparison against Davison Appendix C is **BLOCKED**: no source
scan is in the repository. A structural audit was done instead, and found two defects, both in
the Unity code around the data rather than in the data.

- **F15-AUDIT-001 (fixed)** — the two high-alpha asymmetric terms guarded beta on both sides but
  alpha only from below, so past their declared 90° `alphaMax` the compact-support window grew
  instead of decaying: a term meant to peak at 0.164 reached **−237 at 179°**, sign reversed, and
  the finiteness check could not see it.
- **F15-AUDIT-002 (fixed)** — no alpha/beta domain gate existed. These are 6th- to 9th-order fits
  that diverge rather than degrade outside their fitted region; **Cm reaches −730 at α=180°**.
  `MavF15BaumannMach06Domain` now refuses outside the span of breakpoints the routine itself
  declares, and documents that this is a transcription-derived bound, not an aerodynamic validity
  envelope the sources never published.

Five transcription questions remain open and are marked at the exact lines. Full detail, including
what would close each one, is in **`F15_R2_TRANSCRIPTION_AUDIT_V0.1.md`**.

**No transcribed coefficient was changed.** The only executable edit in either transcription file
is the two alpha guards.

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
- wing span;
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

| suite | result |
|---|---|
| `MavF15BaumannTranscriptionValidation.RunAll` | **25 passed, 0 failed** |
| `MavF15ControlPathValidation.RunAll` | **41 passed, 0 failed** |
| full-project compile, 304 runtime scripts, Unity 6000.3.16f1 Roslyn | **0 errors** |

Method: every runtime script compiled with Unity's own Roslyn against Unity's managed assemblies,
then the validation entry points invoked on the resulting assembly from a .NET 8 host. Both suites
are pure static math over `MavAeroCoefficients`, `Mathf` and `Math`, so they execute faithfully
outside the engine.

`MavF15PropulsionValidation.RunAll` cannot run that way — `MavEngineProfile` is a
`ScriptableObject`, so it needs the real Unity runtime. It was run through the project's own
headless batch adapter instead; the result is recorded in §9.

### NOT RUN

- **Unity Editor play mode.** No scene, prefab, Rigidbody or `MavSixDoFBody` integration was
  exercised by this work.
- **Any flight, trim or trajectory test.** There is nothing to fly: with no sourced gearing the
  control law outputs neutral and with no deck the engines produce no thrust.
- **Any comparison against AFIT source plots or tables.** The scans are unavailable — this is the
  blocker, not an omission.

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
| 1 | **Davison, AFIT/GAE/ENY/92M-01, Appendix C** (page images, not OCR) | closes all five open transcription questions; lets the research model be trusted rather than merely structurally sound |
| 2 | **MDC A4172 Part II** — *F/TF-15 Stability Derivatives, Mass and Inertia Characteristics* | the exact NASA 836 coefficient database, `S` and `c̄`; would make the exact-target profile valid and everything downstream live-capable |
| 3 | **DN-1180.01-238-458 Rev. D** — *F-15 Flight Control System Description* | mechanical gearing, PRAD/RRAD, CAS gains, ARI, limiters, actuator travel and rates — the entire `MavF15ControlLawSchedules` set at once |
| 4 | **NASA TP-1373 / TM-X-3261 / TP-1034** | an F100-PW-100 thrust deck; would need engine-build differences kept explicit |
| 5 | **NASA 836 engine installation geometry** (any configuration-matched source) | mount coordinates and thrust-line offsets; without it engine-out yaw stays unmodelled and `r × F` stays zero by absence |
| 6 | **NASA TM-72861** | F-15-family FCS architecture and gains at `PublicReference` grade — useful immediately, but preproduction F-15 No. 8, so it cannot be promoted to exact-target authority without demonstrated equivalence |
| 7 | **Nolan II, AFIT/GAE/ENY/92J-02** | independent cross-check on the Davison transcription |

Item 1 is the cheapest and the highest-value: it is one appendix, and it would convert the largest
body of already-implemented code in this branch from "candidate transcription" to "verified".

---

## 10. Recommended next action

Supply **Davison Appendix C as page images**. Every open item in the transcription audit is a
question about a character OCR is most likely to have damaged — an exponent, a sign, a leading
digit — so OCR text cannot settle it.

Until then the useful work is sourcing, not coding. The architecture is ahead of the data, which
is the correct place for it to be, and further implementation would mostly consist of inventing
numbers to fill it.
