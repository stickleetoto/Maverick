# Phase 5 Physics Defect Audit v0.1

**Phase 5B.5 — F-16 Reference Closure + Physics Defect Audit**
Branch `sol/phase5-wip`. Compiled 2026-09-11. Nothing committed.

**Revision 4 (2026-09-12)** — Phase 5B.7. Twelve defects. **Seven fixed** (D1, D4, D5, D6, D7, D8, D9),
one **tracked with a gate** (D3), four open.

Revision 4: D4 closed — every G consumer traced and classified, and all seven protection
reads migrated from the legacy gravity-inclusive quantity to true body-normal specific-force
Nz. The 5C-R mass policy is frozen, the CG/physics datum is resolved for the isolated rig by
defining a reference root whose origin IS the CG, and the concrete handover adapter is
implemented and failure-injected.

Revision 3: D5’s negative limits reclassified as `GAMEPLAY_SAFETY` after conflating a
model-validity domain with a flight-control limit; D7 moved from 5D to 5C-R and closed; D8
validated dynamically as well as statically; the leading-edge flap question resolved from the
primary sources.

Blockers are now split by the phase they block — see the **Summary** at the end. Propulsion is **not**
a Phase 5C-R blocker: 5C-R is an unpowered cutover by definition.

---

## P5B5-D1 — Zero gravity in F16Replacement mode — **BLOCKER — FIXED**

**Description.** `Rigidbody.useGravity` and the custom gravity force were decided by two independent
conditions in two components, and those conditions disagreed in one mode.

**Observed evidence.** Static read of the two sites:

- `MavAeroBody.ApplyRigidbodyGravityPolicy()` set `rb.useGravity = false` whenever
  `manageRigidbodyGravity` was true (the default), **every physics step**, with no reference to who
  owned physics. Called from `Awake`, `OnEnable`, and the top of `FixedUpdate`.
- `MavAeroBody.FixedUpdate` applied `Physics.gravity * mass * gravityBlend` only when
  `LegacyPhysicsAllowed()` — which is `owner != F16Replacement`.

In `F16Replacement` the flag was still forced off while the force had stopped being applied.
**Net acceleration: zero.** Reproduced as a test: with the defect reinstated,
`GRAVITY-004b` reports `got (0.0000, 0.0000, 0.0000)`.

**Relevant source.** TP-1538 nomenclature — `an`, normal acceleration, *positive along negative Z
body axis* — is the definition the aircraft's weight must appear in. An aircraft with no weight term
is not a flight model.

**Runtime consequence.** Any transition to replacement ownership would have produced a weightless
aircraft. Legacy and Shadow were unaffected, which is why it had not been seen.

**How to reproduce.** Set `MavFlightPhysicsOwnership.owner = F16Replacement` with `MavAeroBody`
present; observe `Rigidbody.useGravity == false` and no gravity force.

**Proposed correction.** One explicit gravity ownership contract: a named provider per mode, with the
authority owning `Rigidbody.useGravity` and asserting it every step.

**Implemented.** Yes.
- New `MavGravityProvider { None, UnityRigidbody, LegacyAeroCustomGravity, ReplacementLoadSet }`.
- `ResolveGravityProvider(owner)`: Legacy / Shadow / Fault → `LegacyAeroCustomGravity`;
  `F16Replacement` → `UnityRigidbody`.
- `EnforceGravityOwnership()` runs first in the authority's `FixedUpdate`
  (`DefaultExecutionOrder(-400)`), owns the flag, and counts corrections.
- `MavAeroBody.ApplyRigidbodyGravityPolicy()` yields the flag when an authority exists and keeps its
  old behaviour when none does, so pre-Phase-5 rigs are unchanged.
- The custom-gravity gate now asks the gravity question (`LegacyCustomGravityAllowed`) rather than
  the general legacy-write question.

`F16Replacement` resolves to `UnityRigidbody` and **not** `ReplacementLoadSet` deliberately:
`MavSixDoFBody` does not carry a gravity term in its load set today. Naming the provider after the
intention rather than the implementation would have re-created the same hole.

**Validation.** `GRAVITY-001` (a = `Physics.gravity` at rest, exactly one force write),
`GRAVITY-002` (every mode has exactly one source), `GRAVITY-002b` (no mode permits two),
`GRAVITY-003` (Legacy→Shadow unchanged), `GRAVITY-004/b/c` (replacement has 1 g, not 0),
`GRAVITY-005/b`. Probe `gravityflag` reinstates the defect → suite fails 2 checks.

**Honest limit.** A second probe (`gravitygate`) reverting only the gate change leaves the suite
green, because `LegacyPhysicsAllowed` and `LegacyCustomGravityAllowed` happen to agree on all four
current modes. That half of the change is defence in depth, not a behavioural fix, and
`GRAVITY-005b` records it in the suite output so it is never credited with more than it does.

---

## P5B5-D2 — Two different masses for one aircraft — **HIGH — NOT FIXED**

**Description.** The reference stack and the legacy stack disagree about what the F-16 weighs.

**Observed evidence.**
- `MavF16MassReference.MassKg` = `637.16 slug × 14.59390294` = **9 298.65 kg** (TP-1538 Table I,
  20 500 lb).
- `MavAircraftCatalog.CreateF16C()` line 286: `p.mass = 9800f` — **9 800 kg**.
- `MavFreshBootstrap:131` sets `rb.mass = 12000f` before the profile applies.

A 5.4% mass disagreement between the model whose inertias are used and the model that actually flies.

**Relevant source.** TP-1538 Table I; TM-2003-212145 Table 1.

**Runtime consequence.** Every load factor, every specific force and every inertia-derived angular
acceleration computed from the reference inertias is inconsistent with the mass being integrated.
The Morelli-vs-legacy pitch-authority comparison in the Phase 5B report divided a reference moment by
a reference inertia while the aircraft carried a different mass.

**How to reproduce.** Compare `MavF16MassReference.MassKg` with `MavAircraftCatalog` F-16C `p.mass`.

**Proposed correction.** Decide which mass is authoritative for the reference aircraft and make the
other a declared deviation. Changing `p.mass` changes handling, so it is **not** an audit-phase fix.

**Implemented.** No — this is a tuning-affecting change and out of scope for this phase.

**Validation.** None yet; the discrepancy is recorded in the coverage matrix §2.

---

## P5B5-D3 — `MavLandingGearSystem` declared twice — **MEDIUM — TRACKED, NOT FIXED**

**Description.** Two different types share one file name, in two namespaces.

**Observed evidence.**

| File | Namespace | Lines | Rigidbody writes | Referenced by scenes/prefabs |
|---|---|---|---|---|
| `Aircraft/MavLandingGearSystem.cs` | `MaverickFresh` | 325 | **1** (`rb.AddForce` gear drag, line 123) | **0** |
| `Aero/MavLandingGearSystem.cs` | **global** | 431 | **0** | **1** |

C# treats these as `MaverickFresh.MavLandingGearSystem` and `global::MavLandingGearSystem`, so it
compiles. Only one duplicate base name exists in the whole project.

**Runtime consequence.** Two:
1. **It invalidated a Phase 5A conclusion of mine.** I classified `MavLandingGearSystem.cs` as a
   scoped exemption that "MUST be gated before replacement mode goes live". That was read from the
   `Aircraft/` file — the one **nothing instantiates**. The gear system actually in the scene writes
   no Rigidbody state, so that blocker is not live.
2. Which type a future `using MaverickFresh;` resolves to can change silently.

**How to reproduce.** `find Assets -name 'MavLandingGearSystem.cs'` returns two paths.

**Proposed correction.** Delete or rename one type. The orphan is the `Aircraft/` one, but it
contains the gear-drag force, so the choice is a maintainer's call, not an audit pass's.

**Implemented.** No. **Gated instead:** `MavPhase5WriterScan.KnownDuplicateBaseNames` pins it with
this defect ID, the Phase 5A validation fails on any *untracked* duplicate, and the suite prints
`OPEN DEFECT P5B5-D3` every run. Named, never hidden.

---

## P5B5-D4 — Load factor telemetry is not load factor — **HIGH — FIXED (Phase 5B.7)**

**Description.** Three different quantities are called "G", and the one on the HUD is not the one a
G meter measures.

**Observed evidence.**

| Site | Formula | What it is |
|---|---|---|
| `MavInstructorController:463` | `Dot(dv/dt, transform.up) / 9.80665` | **kinematic** acceleration, signed |
| `MavMouseFlightJet:874` | same formula, computed independently | duplicate of the above |
| `MavMouseFlightJet.CurrentLoadFactorG():1289` | `Abs(aeroBody.debugAeroCurvatureG)` | `|aerodynamic lift| / mg`, unsigned |

`gEstimate` omits the gravity subtraction. Measured (tests `LOADFACTOR-001..004`):

| Condition | Runtime `gEstimate` | Accelerometer (`an`) |
|---|---|---|
| steady level flight | **0.00 g** | **+1.00 g** |
| free fall | **−1.00 g** | **0.00 g** |

**Relevant source.** TP-1538 nomenclature: `an` = normal acceleration, *positive along negative Z
body axis*, g units — i.e. specific force, gravity excluded.

**Runtime consequence.** The HUD reads 1 g low in level flight. Worse, `gEstimate` feeds the G
limiter, so the limiter's thresholds are being compared against the wrong quantity. And the Phase 4B
thrust-boost suppression uses `CurrentLoadFactorG()` — a *different* quantity — so two protections
called "G limits" respond to two different physical things.

**How to reproduce.** Fly straight and level; observe HUD `G` ≈ 0 rather than 1.

**Proposed correction.** Make specific force the single definition, and keep exactly one
implementation. **Changing `gEstimate` changes limiter behaviour and therefore handling**, so it is
not an audit-phase fix.

**Implemented in Phase 5B.7.** Every consumer was traced and classified, and the control paths
migrated to true Nz.

### Consumers found, and what each became

| Consumer | Class | Outcome |
|---|---|---|
| `MavInstructorController` negative-G reduction (`:1412`) | **CONTROL / PROTECTION** | **→ `nzEstimate`** |
| `MavInstructorController` negative-G gate (`:1416`) | **CONTROL / PROTECTION** | **→ `nzEstimate`** |
| `MavInstructorController` sustained-G limiter + ramp (`:1440`, `:1442`) | **CONTROL / PROTECTION** | **→ `nzEstimate`** |
| `MavInstructorController` soft-G branch + ramp (`:1448`, `:1450`) | **CONTROL / PROTECTION** | **→ `nzEstimate`** |
| `MavInstructorController` keyboard hard-G cutoff (`:1456`) | **CONTROL / PROTECTION** | **→ `nzEstimate`** |
| `MavMouseFlightJet.CurrentLoadFactorG` → thrust-boost suppression | CONTROL, but a *third* quantity (\|lift\|/mg, unsigned) | **renamed `CurrentAeroLiftG`**, behaviour unchanged. It was never the legacy HUD quantity; the name was the defect |
| `MavFreshHud` ×3, `MavControlDebugOverlay` | HUD / TELEMETRY | unchanged, still `gEstimate` |
| `MavFlightDataRecorder` | AI / REWARD | unchanged |
| `MavPhysicalAIRewardLogger` (log + `> hardGLimit` penalty) | AI / REWARD | unchanged — a reward signal is not physical control |
| `MavCASWeaponSystem:525` | GAMEPLAY | unchanged |
| `MavTurnDynamicsMigrationValidation` | DEBUG | unchanged |
| `MavTurnDynamicsDiagnostics.loadFactorG`, `MavManeuverDiagnostics.nzG` | DEBUG | unchanged (both are the aero-lift channel, now named so) |
| `Assets/Maverick/.../AircraftPhysicsController.LoadFactorEstimate` | **LEGACY COMPATIBILITY** | untouched. The old Maverick stack, disabled by `MavFreshBootstrap`. Note it has its own third definition, `Dot(accel + Physics.gravity, up)`, which is neither of the two above |

**Seven protection reads migrated. `MavInstructorController` now contains exactly two mentions of
`gEstimate`: its declaration and the one line that computes it for the HUD.** Verified by
`NZCTRL-007b/d`.

### The gravity subtlety that would have broken a naive fix

Specific force is acceleration minus **the gravity actually acting**. The legacy stack applies gravity
as an explicit force scaled by `MavAeroBody.gravityBlend`, which is 1.0 for the F-16 but **0.45** for
several other aircraft profiles. Subtracting a full g from an aircraft only being pulled down by 0.45 g
would have put a 0.55 g error straight into the G limiter.

So the gravity **owner** now publishes the answer: `MavAeroBody` reports its scale each step via
`ReportLegacyGravityScale`, and `MavFlightPhysicsOwnership.EffectiveGravityAccelerationWorld` resolves
it per provider. `NZCTRL-005` holds the aerodynamic force constant while gravity drops to 0.45 g and
confirms true Nz is unchanged at 5.000 g.

### Measured

| condition | true Nz | legacy HUD G |
|---|---|---|
| level supported flight | **+1.000** | 0.000 |
| free fall | **0.000** | −1.000 |
| +5 g aerodynamic specific force | **+5.000** | 3.999 |
| −3 g aerodynamic specific force | **−3.000** | — |
| +5 g with gravity at 0.45 g | **+5.000** (unchanged) | 4.550 (moved) |

**Validation.** `NZCTRL-001` … `NZCTRL-009d`, 25 checks, including a **behavioural** case that invokes
the real private `ApplyProtectionAssists` at a state where the two quantities straddle the 8.8 g limit
(true Nz 9.200 vs legacy 8.200): a full nose-up command is shaped from −1.0000 to −0.8833 and the
instructor reports `g_limiter`.

**Probe** `hudgtolimiter` reconnects the legacy quantity to the limiter → **4 checks fail, two of them
behavioural**: the same frame passes through unattenuated at −1.0000 with `instructorState normal`.

**G limit numbers unchanged** — 8.8 soft, 8.8 sustained, 11.2 hard (`NZCTRL-008`).

---

## P5B5-D5 — Envelope protection is one-sided — **HIGH — FIXED**

**Description.** Every AoA and G protection branch is gated on a nose-up command. Negative AoA and
negative g have no protection at all.

**Observed evidence.** All four branches in `MavInstructorController.ApplyProtectionAssists`:

```
1325:  if (targetPitch < 0f && aoaAbs > aoaSoftLimitDeg)                                  // AoA limiter
1336:  if (useGLimiter && gEstimate > sustainedGLimit && targetPitch < 0f)                 // sustained G
1344:  if (useGLimiter && gEstimate > softGLimit && gEstimate <= sustainedGLimit && …)     // soft G
1352:  if (keyboardElevatorUsesGLimit && pitchOverride && gEstimate > hardGLimit && …)     // keyboard G
```

`aoaAbs` is an absolute value, so a large **negative** AoA does exceed the breakpoint — but the
branch only fires when `targetPitch < 0f`, i.e. while the pilot is pulling. During a push-over
(`targetPitch > 0`) nothing limits AoA. And `gEstimate > limit` can never be true for negative g.

**Relevant source.** TP-1538 describes the AoA limiter as a system to "maintain α at or below" its
limit; it does not describe a one-sided limiter.

**Runtime consequence — the reported observation.** Mach 0.72, CL ≈ −1.13, HUD G ≈ −9.9 is
**internally consistent and is not a sign bug**:

1. `CL = −1.13` requires **negative AoA**. Verified by test `CL-002`: production
   `ComputeLiftCoefficient` returns negative CL for negative α, as it must.
2. Negative AoA produces lift toward the aircraft's belly. `MavAeroBody` clamps lift magnitude to
   `maxLiftG = 9.4` for the F-16, so belly-ward acceleration saturates near 9.4 g; adding the gravity
   component gives ≈ 9.9 g.
3. `gEstimate` is `Dot(dv/dt, transform.up)/g`, so belly-ward acceleration reads **negative**.
   −9.9 g is the arithmetic, not an inversion.
4. Nothing prevented the aircraft from reaching that AoA, because the limiter does not act on
   push-overs.

**Ruled out, with evidence, rather than by assumption:**

| Candidate | Verdict | Evidence |
|---|---|---|
| AoA sign inversion | **Ruled out** | `AOA-001`, `AOA-002`, `AOA-002b`: legacy and replacement alpha agree to 5.7106° vs 5.7106° for the same state, both positive for velocity below the nose |
| Coefficient sign inversion | **Ruled out** | `CL-001`/`CL-002`: +α → +CL, −α → −CL |
| Unity/aero axis conversion error | **Ruled out** | `AXIS-001..004`, including a check that the true-vector and axial-vector conversions still genuinely differ |
| Load-factor telemetry sign error | **Ruled out as the sign**; D4 is a *magnitude* offset of ~1 g, not an inversion | `LOADFACTOR-001..004` |
| Physically valid negative lift | **Confirmed** — the readings are mutually consistent | above |
| Unprotected negative-AoA excursion | **Confirmed as the reason it was reachable** | the four gated branches above |

**How to reproduce.** Push over hard from level flight and hold; AoA passes the 22°/30° breakpoints
with no limiter engagement (`instructorState` never becomes `aoa_limiter`).

**Proposed correction.** Attenuate only a command that would push the excursion FURTHER out, on
either side of zero — explicitly **not** `Mathf.Abs` everywhere, which would also attenuate the
*recovery* command and trap the aircraft at the edge of the envelope it was meant to protect.

**Implemented.** Yes.

The rule is one line, in `MavTurnDynamicsRules.CommandWorsensExcursion`:

```
worsens = pitchCommand * signedState < 0
```

derived from the traced conventions — negative pitch command is nose up, positive α is velocity below
the nose, positive Nz is toward the canopy — so a nose-up command worsens a positive excursion and a
nose-down command worsens a negative one. Two new pure functions,
`ComputeSymmetricAoAReduction` and `ComputeSymmetricGReduction`, apply it.

Both one-sided sites were fixed: `ApplyProtectionAssists` (the limiter) and
`ApplyEnvelopeProtectionGuards` (the guard layer), which carried the same `targetPitch < 0f` gate.

The positive branch is **untouched** — the negative side was added as a separate block rather than a
rewrite, and `POS-AOA-001` / `POS-G-001` assert bit-identical agreement with the pre-existing
production curves across 0–45° and 0–12 g.

**New numbers introduced.** The negative envelope did not exist, so it had to be created. Existing
numbers were not changed.

| Field | Value | Provenance |
|---|---|---|
| `aoaNegativeHardLimitDeg` | −10° | **`MAVERICK_TUNING` / `GAMEPLAY_SAFETY`** |
| `aoaNegativeSoftLimitDeg` | −7° | **`MAVERICK_TUNING` / `GAMEPLAY_SAFETY`** |
| `negativeGLimit` | −3 g | **`MAVERICK_TUNING` / `GAMEPLAY_SAFETY`** |

### Revision 3 correction to this defect's reasoning

Revision 2 justified the −10° hard limit as "the lower bound of the Morelli validated α domain". That
**conflated two different things**, and the reasoning is withdrawn:

| | Concept | Status |
|---|---|---|
| **A** | **Morelli aerodynamic validity domain** — α −10…+45°, β ±30°, Mach < 0.6 | `AUTHORITATIVE`. Enforced by `MavF16ReferenceEnvelope`, a gate on the **replacement** stack |
| **B** | **Legacy / gameplay envelope protection** — the block this defect added | `GAMEPLAY_SAFETY`. Limits what the legacy instructor commands on the aircraft flying today |
| **C** | **Future reference FLCS protection** | **NOT IMPLEMENTED, NOT SOURCED.** TP-1538 describes an AoA limiter qualitatively for a 1979 research aircraft and publishes no gains |

"The aerodynamic model has no data past −10°" is a statement about **A**. It is not evidence for a
value in **B** and says nothing about **C**. The number happens to be defensible, but a wrong reason
that lands on a plausible number is worse than an honest guess, because it *looks* sourced.

**None of these three values is an F-16 value.** No primary source consulted in 5B.5 or 5B.6 publishes
a negative AoA or negative-g limit for this aircraft. The bidirectional protection logic is retained
in full — the one-sided bug is real — and only the justification changed.

These reach `MavInstructorController` (the consumer) directly via `ApplyToInstructor`. There is
deliberately **no jet mirror** — a second home for a setting is what caused D4 and the 5B.0b family.

**Validation.** `POS-AOA-001`, `NEG-AOA-001/001b/002/002b/003`, `POS-G-001`, `NEG-G-001/001b`,
`RECOVERY-001/001b/001c/002/003`, `MOUSE-001/001b`, `MANUAL-001/001b/001c` — 19 checks, all passing.
Probe `onesided` restores the one-sided gate → **5 checks fail**.

**Not changed, as instructed.** `manualEnvelopeBypassFactor` stays 0.65. The negative side routes
through the *same* bypass as the positive side, so keyboard pitch weakens both identically rather than
one of them secretly: at −12° α the negative limiter goes from 0.05 to 0.6675 under the bypass, i.e.
33% limiting left. That is the D4 family, still open.

---

## P5B5-D6 — Wrong citation on the mass reference — **MEDIUM — FIXED**

**Description.** `MavF16MassReference` cited NASA NTRS **20200003104** (*Autonomous Real-Time Global
Aerodynamic Modeling in the Frequency Domain*, 2020) as the authoritative source for the F-16 mass
and inertia table. That paper is about real-time modelling; the numbers are Table I of TP-1538
(1979), reproduced as Table 1 of TM-2003-212145.

**Observed evidence.** TP-1538 Table I, verbatim: `Weight, N (lb) … 91 188 (20 500)`,
`IX … 12 875 (9496)`, `Iy … 75 674 (55 814)`, `IZ … 85 552 (63 100)`, `IXz … 1331 (982)`,
`Reference center-of-gravity location … 0.35c̄`. Every repo constant matches.

**Runtime consequence.** None on physics — **the numbers are correct**. The consequence is on
auditability: a wrong citation cannot be checked, and this project's provenance discipline is the
thing that makes the model trustworthy.

**Proposed correction.** Cite the primary source actually verified.

**Implemented.** Yes — citation corrected, values untouched.

---

## P5B5-D7 — Mach uses a constant speed of sound — **MEDIUM — FIXED (5C-R prerequisite)**

**Description.** `MavMouseFlightJet:865`: `machEstimate = speed / 343f`.

**Runtime consequence.** Mach is increasingly wrong with altitude (real `a` falls toward ~295 m/s in
the stratosphere, so true Mach is understated by ~16% up high). This matters beyond the HUD: the
Morelli reference envelope is gated on Mach < 0.6, so the gate itself is being evaluated with an
approximate Mach. It is also a prerequisite for any TP-1538 Table VI thrust lookup, which is indexed
by Mach and altitude.

**Reclassified in 5B.6 from a 5D blocker to a 5C-R prerequisite**, correctly: the Morelli validity
gate *is* a Mach bound, so a gate fed an approximate Mach is not protecting the model it claims to.

**Implemented.** Yes. `MavAtmosphereModel` already contained a full ISA implementation (T, p, ρ, and
`a = sqrt(γRT)`); the defect was that nothing used it for gating. Three changes:

- `MavAtmosphereModel.ReferenceMach(tas, altitude)` — the single definition of reference Mach.
- `LegacyConstantSpeedOfSoundMps = 343f` and `LegacyMachEstimate(tas)` — the legacy approximation,
  **named** so it can be referred to and compared but not mistaken for the reference.
- `MavMachSource { Unspecified, ReferenceAtmosphere, LegacyApproximation }` on the envelope gate,
  which **refuses** anything but `ReferenceAtmosphere`. That makes "do not gate on legacy Mach" a
  structural rule rather than a documented intention; `Unspecified` is also refused, so forgetting to
  say is not a pass.

**Measured.** The legacy estimate is wrong by up to **14.0%** at 15 km (ref 0.678 vs legacy 0.583 at
TAS 200 m/s). Mach 0.6 is **204.2 m/s** at sea level and only **177.0 m/s** at the tropopause — a
27.1 m/s difference the legacy estimate cannot see. `ATMO-007` demonstrates the consequence: 190 m/s
is inside the envelope at sea level and outside it at 12.2 km, and the legacy estimate reports 0.554
at both — it would have let the replacement model extrapolate.

**Validation.** `ATMO-001` … `ATMO-007`, 16 checks, including ISA reference values at six altitudes.

**Still open, separately:** the legacy stack keeps its own exponential density (`MavAeroBody`), so two
atmosphere implementations coexist. That is a legacy-consistency item, not a gate correctness item —
`IMPORTANT LATER`.

---

## P5B5-D8 — Ixz was transformed into Unity principal axes but never validated — **FIXED**

**Correction to revision 1.** Revision 1 said "Ixz is unrepresentable in Unity's diagonal tensor".
**That was wrong, twice over.**

1. Unity does not carry a diagonal-only tensor. It carries principal moments
   (`Rigidbody.inertiaTensor`) **plus the orientation of the principal frame**
   (`Rigidbody.inertiaTensorRotation`), which together represent any symmetric positive-definite
   inertia matrix exactly — diagonalize, hand over the eigenvalues and the eigenvector rotation.
2. The project was **already doing this**. `MavF16MassReference.CreateUnityMassProperties` performed
   the 2×2 diagonalization of the Unity-frame Y/Z block, and the result was live:
   `MavF16FlightDynamicsProfile` → `MavSixDoFBody.ApplyTo(rb)`.

The real gap was narrower than either claim: **nothing ever reconstructed the matrix from the Unity
representation to check the transformation was right.** An unverified transformation is not a
representation problem; it is a missing test.

**Corrected wording:** *"Ixz was transformed into Unity principal inertia axes and applied, but the
transformation was never validated by reconstruction."*

**Sign convention, derived not guessed.** Aircraft body axes are X forward, Y right, Z down, and the
inertia matrix carries products of inertia with **negative** off-diagonal signs:

```
[  Ix   0  -Ixz ]        with Ixz published as a POSITIVE number
[   0  Iy    0  ]
[ -Ixz  0   Iz  ]
```

Confirmed from TP-1538 Appendix B, whose rotational equations take the standard form
`ṗ = (Iz−Iy)/Ix·qr + Ixz/Ix·(ṙ+pq) + …`. Those `+Ixz/Ix` terms follow from the matrix above with a
positive Ixz and would carry the opposite sign if the off-diagonals were `+Ixz`. TP-1538 Table I
publishes `IXz = 1331 kg·m² (982 slug-ft²)` as positive.

**Implemented.** `MavInertiaTensorMath` — a general, testable module:

- `BuildAeroBodyInertia(Ix, Iy, Iz, Ixz)` with the derived sign convention
- `AeroBodyInertiaToUnityLocal` — the congruence `I_unity = P·I_aero·Pᵀ` computed **generally** from
  the stated basis change `P = [[0,1,0],[0,0,−1],[1,0,0]]`, rather than written out by hand, so it
  cannot drift from the convention it claims. `det(P) = −1`; inertia is a rank-2 tensor and transforms
  by congruence, so the determinant's sign is irrelevant here — it only matters for axial *vectors*
- `Diagonalize` — cyclic Jacobi, chosen over the closed-form 2×2 the aircraft case permits so it keeps
  working if a future aircraft carries Ixy or Iyz
- `Reconstruct(principal, rotation)` → `R·diag(λ)·Rᵀ`, and `MaxAbsDifference`

**Measured result.**

```
aero body      [ 12874.8      0.0  -1331.4 ;      0.0  75673.6      0.0 ;  -1331.4      0.0  85552.1 ]
unity local    [ 75673.6      0.0      0.0 ;      0.0  85552.1   1331.4 ;      0.0   1331.4  12874.8 ]
principal      ( 75673.6, 85576.5, 12850.5 ) kg m^2
rotation       1.0492 deg about Unity X
reconstructed  [ 75673.6      0.0      0.0 ;      0.0  85552.1   1331.4 ;      0.0   1331.4  12874.8 ]
```

**Reconstruction error: 0.0020 kg·m² absolute, 2.28 × 10⁻⁶ % of the largest element.** Trace preserved
to 0.016 kg·m² out of 174 100. The **shipped** production mass properties reconstruct the sourced
matrix to the same 0.0020 kg·m², and converting them back to body axes returns TP-1538 Table I exactly:
Ix 12874.8, Iy 75673.6, Iz 85552.1, Ixz 1331.4.

**Handedness.** Jacobi may return a left-handed eigenbasis, which a quaternion cannot carry.
`Quaternion.LookRotation` derives its first basis vector as `cross(up, forward)` and so silently uses
`−axis0` in that case — which is not a loss, because negating an eigenvector leaves its eigenvalue
untouched and `I = R·diag(λ)·Rᵀ` holds either way. The handedness is *recorded*
(`debugLastEigenBasisWasLeftHanded`, currently `False`) rather than corrected, and the reconstruction
check is what proves the claim. Reasoning about sign conventions is how this project got a wrong answer
before; measuring the round trip is how it stops.

**Validation.** `INERTIA-001` … `INERTIA-005c`, 16 checks. Non-vacuity is explicit: `INERTIA-004`
asserts the input really has a 1331 kg·m² off-diagonal term and `INERTIA-004b` that the principal frame
is genuinely rotated, so none of this is a round trip through an already-diagonal matrix.

**Probes — all three bite:**

| Probe | Result |
|---|---|
| `ixzsign` — flip the off-diagonal sign convention | **FAIL, 3 checks**, incl. shipped reconstruction error 2662.8 kg·m² |
| `ixzrotation` — negate the principal-axis rotation | **FAIL, 2 checks**, reconstruction error 2662.8 kg·m², recovered Ixz sign inverted |
| `ixzpairing` — swap which principal moment pairs with which Unity axis | **FAIL, 2 checks**, reconstruction error 72 677 kg·m² |

**Remaining, and separate from this defect.** Unity integrates rotation with its own solver. The
principal-axis representation is exact, but whether Unity's integrator reproduces TP-1538 Appendix B's
explicit `Ixz(ṙ+pq)` and `Ixz(r²−p²)` coupling terms under large simultaneous p, q, r is a question
about the *integrator*, not about the representation, and it is untested. Recorded as an open
**BLOCKER FOR 5D** item rather than folded back into this defect.

## P5B5-D9 — Writer scan keyed on file base name — **MEDIUM — FIXED**

**Description.** `MavPhase5WriterScan` classified writers by `Path.GetFileName`, so two files sharing
a name received one verdict. Combined with D3 this produced a wrong Phase 5A conclusion.

**Implemented.** Yes — the scan now records each writer's relative path, detects duplicate base
names, and exposes them; the Phase 5A validation fails on any untracked duplicate.

**Validation.** Phase 5A suite prints `duplicate file names: MavLandingGearSystem.cs x2` and passes
only because that one is pinned with its defect ID.

---

## P5B5-D10 — Morelli model is valid only below Mach 0.6 — **BLOCKER FOR LIVE REPLACEMENT**

**Description.** Not a coding defect — a validity-envelope mismatch. See §H of the coverage matrix
and §10 of the Phase 5B.5 validation report. Recorded here because it blocks replacement ownership.

**Observed evidence.** Morelli §3, verbatim: "a 16% scale model of the F-16 aircraft flying at
relatively low Mach numbers (< 0.6)". Normal Maverick gameplay reaches Mach ≈ 0.7–0.8.

**Implemented.** No, and deliberately not: inventing a transonic correction is explicitly out of
scope. Recommendation in the validation report.

---

## P5B5-D11 — Soft-G branch is unreachable as configured — **LOW — NOT FIXED**

**Description.** `softGLimit` (8.8) equals `sustainedGLimit` (8.8), so the branch guarded by
`gEstimate > softGLimit && gEstimate <= sustainedGLimit` can never be true. `sustainedGLimit` is also
written by no aircraft profile at all — it exists only as a prefab default.

**Runtime consequence.** One of the two G-limiter stages is dead code. Harmless today; misleading to
anyone reading the limiter.

**Implemented.** No — changing either value changes handling.

---

## P5B5-D12 — Five setup-time writers of `Rigidbody.useGravity` — **LOW — MITIGATED**

**Description.** `MavFreshBootstrap:139`, `MavMouseFlightJet.SetupPublicRigidbody:670`,
`MavAircraftProfileApplier:234`, `MavSixDoFBody:510`, and formerly `MavAeroBody` all write the flag.
All except `MavSixDoFBody` write `false`; `MavSixDoFBody` writes `activeProfile.useGravity`, which is
`true` for the F-16 profile — so two components wrote opposite values.

**Runtime consequence.** Before D1's fix, whichever ran last won, and `MavAeroBody` won because it
wrote every step. After the fix the authority writes every step and wins by execution order
(`-400`), so the setup writers are seeds rather than owners.

**Implemented.** Mitigated, not removed. `debugGravityFlagCorrections` counts how often the authority
has to correct the flag, so a remaining fight is observable rather than theoretical.

---

## Phase 5B.7 hardening — three findings from the writer/authority scan

These were not in the original twelve. They surfaced because the new 5B.7 files had to pass the
existing ownership scan, and two of them did not.

### H1 — the handover adapter defaulted to "per-step physical writer"

`MavPhase5WriterScan` classifies an **unclassified** file as `PerStepPhysical` and then demands it
consult the ownership gate. `MavRuntimeHandoverTarget` writes `position`, `rotation`,
`linearVelocity` and `angularVelocity` through `MavCapturedRigidbodyState.RestoreTo`, so it was
flagged ungated. That is the fail-closed rule working: a new writer must be judged, not assumed
harmless.

**Resolved** by classifying it `TransitionOrSetupPose` with the reason recorded in code — those writes
happen only while a handover executes or rolls back, never per step — and by raising the expected
transition-writer count from 2 to 3 so the number stays load-bearing rather than becoming a ceiling.

### H2 — rollback could have granted arming

`Rollback` restored `captured.replacementBodyArmed` to `MavSixDoFBody.ArmedForLiveFlight`. Two
problems, one of them real:

- **The invariant.** A rollback ends with Legacy owning the aircraft. Restoring a captured `true`
  would leave an armed replacement body behind a legacy-owned aircraft — two owners for one physical
  effect, the exact condition this phase exists to prevent.
- **The authority.** It made this file a *second* place in the codebase capable of granting arming,
  which the single-arming-authority check (Blocker 2) correctly flagged.

**Resolved.** `Rollback` now disarms **unconditionally**. `captured.replacementBodyArmed` is retained
for auditing a handover, and the field says so. The detector was narrowed in step with this: an
explicit `= false` provably cannot grant arming, so it no longer counts as a rival authority, while
assignment from a *variable* still does. Probe `rivalarming` reverts exactly that — a variable
assignment — and turns both suites red (+2 each), so the narrowing did not hollow the check out.

### H3 — the separated gravity gate was an unfalsifiable claim

D1's fix asks a gravity-specific question (`LegacyCustomGravityAllowed`) rather than reusing the
general "may legacy write forces" gate. With today's four owner modes those two predicates **agree on
every value**:

| owner | `LegacyPhysicsAllowed` | `LegacyCustomGravityAllowed` |
|---|---|---|
| `Legacy`, `Shadow`, `Fault` | true | true |
| `F16Replacement` | false | false |

So reverting the call site changed no behaviour and no behavioural test could see it — probe
`gravitygate` left the whole suite green, which `p5b5check` GRAVITY-005b had recorded openly rather
than papered over. The separation still matters: the day a mode wants legacy aerodynamics with gravity
from elsewhere, a conflated gate is how the zero-gravity hole returns.

**Resolved** by pinning the claim where it is actually made. A source-level check asserts every
custom-gravity decision site in `MavAeroBody` asks the gravity-specific gate (2/2), and fails vacuous
if it finds no site at all. Probe `gravitygate` now reports `1/2` and turns `fdmfull` red.

---

## Summary

| ID | Severity | Status |
|---|---|---|
| D1 zero gravity in replacement | BLOCKER | **FIXED** |
| D2 two masses for one aircraft | HIGH | open — mass policy now frozen, see below |
| D3 duplicate gear-system class | MEDIUM | tracked + gated |
| D4 load factor is not load factor | HIGH | **FIXED** — 7 protection reads migrated to true Nz |
| D5 one-sided envelope protection | HIGH | **FIXED** |
| D6 wrong mass citation | MEDIUM | **FIXED** |
| D7 constant speed of sound | MEDIUM | **FIXED** |
| D8 Ixz transformed but unvalidated | was mis-stated in rev 1 | **FIXED and validated** |
| D9 writer scan keyed on base name | MEDIUM | **FIXED** |
| D10 Morelli valid below Mach 0.6 | constraint, not a defect | **gated** by the 5C-R envelope |
| D11 unreachable soft-G branch | LOW | open — IMPORTANT LATER |
| D12 five gravity-flag writers | LOW | mitigated |

## Blockers by phase

**BLOCKER FOR 5C-R** — **one remains**, down from three

| Item | Why | What closes it |
|---|---|---|
| No 5C-R test scene | Every component exists, is validated offline, and is deliberately not auto-installed. What does not exist is a scene with a `MavF16ReferencePhysicsRoot` in it, and an operator to fly the ten cases | Build the isolated scene and run `Docs/PHASE5CR_UNITY_VALIDATION_PLAN.md` |

**Closed in Phase 5B.7:**

| Was | Now |
|---|---|
| Mass policy not chosen | **Frozen.** 5C-R spawns directly into `ReferenceF16` at 9298.65 kg. Three failure conditions are enforced in code: reference-mode-with-legacy-mass, mass-changed-after-physics-begins, and unspecified provenance |
| CG mapping unmeasured | **Resolved for the isolated rig by option B.** `MavF16ReferencePhysicsRoot`'s origin is *defined* as the reference CG, so `centerOfMass` is exactly zero by construction rather than measured. The gameplay prefab stays fail-closed and untouched |
| No concrete handover target | **Implemented.** `MavRuntimeHandoverTarget` captures pose, velocities, mass, centre of mass, inertia tensor, tensor rotation, gravity state and legacy-writer state, and restores all of it on failure. Failure-injected at all eight steps with the real Rigidbody state verified restored 8/8 |

**Closed earlier:** transonic (gated), Ixz (validated statically and dynamically), atmosphere/Mach
(reference atmosphere enforced), leading-edge flaps (resolved from source — the Morelli base functions
**are** the LEF-deployed state, so no LEF input is required).

**BLOCKER FOR 5D** (powered replacement flight)

| Item | Why |
|---|---|
| Thrust tables not transcribed | Public in TP-1538 Table VI; workflow, parser, validator and hash are in place, values are not |
| Engine gyroscopic moment | `h_eng = 216.9 kg·m²/s` sourced, not applied |
| Throttle gearing / power dynamics wiring | Structure sourced (`tgear`, `rtau`, the interpolation form), not connected |
| Unity integrator fidelity under coupling | The principal-axis representation is validated dynamically against both the full-matrix Euler solution and TP-1538's own scalar equations. Whether Unity's own solver reproduces it at large simultaneous p, q, r still needs Unity — case 8 of the validation plan |

**IMPORTANT LATER**

D11 dead soft-G branch · two coexisting atmosphere implementations · actuator rate limits
(`MAVERICK_TUNING`) · differential tail · wind-relative airspeed · trim · fuel burn · the legacy
`AircraftPhysicsController.LoadFactorEstimate` in the old Maverick tree, which carries a third
load-factor definition of its own.

**OUTSIDE V0.1 REFERENCE SCOPE**

Leading-edge flap dynamics (the reference model is a fixed 25°-deployed configuration) · speed brake ·
landing-gear aerodynamics · external stores · ground effect — all excluded by the Morelli data's own
stated configuration.

**NOT a Phase 5C-R blocker.** Propulsion. Phase 5C-R is an unpowered cutover by definition, and the
reference-envelope gate *requires* `propulsionIntentionallyDisabled = true`.
