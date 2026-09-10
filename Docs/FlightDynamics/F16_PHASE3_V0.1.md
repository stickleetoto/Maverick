# Maverick Flight Dynamics — Phase 3

Status: IN PROGRESS
Branch: `claude/f16-trim-control-phase2`
Base: the Phase 2 closeout commit on this branch, NOT `feature/f16-flight-dynamics-core`
Target Unity: 6000.3.16f1

`MavSixDoFBody.simulationEnabled` remains `false` by default throughout Phase 3.

## Validation provenance

**All check counts in this document are from an OFFLINE HARNESS, not from a Unity editor run.** The
suites are pure static C# and were compiled and executed outside Unity against a minimal API stub.
That is the same production code the editor menu runs, but it does not exercise Unity's own `Mathf`
or `Vector3`, does not touch `Rigidbody`/`Transform`/scenes, and does not prove the project compiles
inside the editor.

`Maverick > Flight Dynamics > Run All Flight Dynamics Validation` under Unity 6000.3.16f1 remains
the authority. No phase here is marked PASS on the offline run alone.

## Phase status

| Phase | Status | Why |
| --- | --- | --- |
| 3A Propulsion | **CONDITIONAL** | The architecture is complete and tested, but there is still **no authoritative F-16 dimensional thrust data**. Runtime thrust remains 0 N. A phase whose whole purpose is dimensional thrust cannot be PASS while the deck is empty. |
| | | *Independent review found an authority-escalation loophole here; fixed, see 3A.7.* |
| 3B Attitude | **CONDITIONAL** | Logic complete and directionally validated offline; pending the Unity run. |
| | | *Independent review found attitude was never published by the runtime path; fixed, see 3B.6.* |
| 3C Ownership | **CONDITIONAL** | Mechanism complete and validated offline; pending the Unity run, and it has never performed a handover on a real aircraft because no aircraft is live-ready yet. |
| | | *Independent review found a false `LegacyOwned` state; fixed, see 3C.5.* |

---

## Phase 3A — Propulsion — **CONDITIONAL**

Marked CONDITIONAL deliberately, not as a formality. 3A exists to deliver dimensional thrust, and
there is no authoritative F-16 thrust deck. What ships is the *architecture* for one, validated
against explicitly synthetic data, while the aircraft still produces 0 N. 3A becomes PASS when, and
only when, a real deck is frozen from an approved source and the powered trim returns plain
`Converged`.

### The problem being solved

Phase 2 left the F-16 with sourced engine power-state dynamics and **no dimensional thrust at all**.
That was honest but structural: there was no place to put a thrust deck if one ever arrived, and no
way to test the powered path without inventing numbers.

Phase 3A adds the architecture. It does not add F-16 data.

### What was NOT done, deliberately

No F-16 thrust data was invented. No altitude/Mach tables were fabricated, nothing was interpolated
from unsourced figures, and no generic F110/F100 headline thrust was converted into a flight
envelope. **The shipped F-16 still produces exactly 0 N and still reports
`ConvergedButThrustUnavailable` for a powered trim.** `[A3]` pins that as a regression.

### Separation of concerns

```text
throttle -> [ sourced Garza/Morelli gearing + spool dynamics ] -> actual power %
actual power % + altitude + Mach -> [ thrust deck ] -> thrust N
thrust N -> [ propulsion model ] -> body-axis force / moment / reported thrust / authority
```

The split is the point. Maverick genuinely has sourced *power dynamics*; it does not have sourced
*thrust*. Keeping them in separate types means one can be true without implying the other.
`MavF16EnginePowerModel` keeps the sourced half and gains an optional `MavThrustDeckBase`; with no
deck attached, behaviour is byte-for-byte what it was.

### Provenance travels with the number

```csharp
enum MavThrustDataAuthority { Unavailable, SyntheticBench, Authoritative }
```

Every `MavThrustDeckResult` carries its own authority, so a value cannot be laundered by passing it
through a layer. `MavPropulsiveLoads.hasAuthoritativeData` and `MavTrimResult.propulsionDataAuthoritative`
carry it onward to telemetry, readiness and trim reports.

### Out-of-envelope policy

```csharp
enum MavEnvelopeExcursionPolicy {
    ClampToValidatedEnvelope,           // edge value, excursion reported, stays authoritative
    RejectUnsupportedState,             // no result at all
    MarkNonAuthoritativeExtrapolation   // genuinely extrapolates, authority DOWNGRADED
}
```

Two details worth stating:

- **Extrapolation really extrapolates.** The first implementation clamped and merely relabelled the
  result, which would have made the policy name a lie. It now uses an unclamped interpolation
  weight — and is bounded to one grid cell beyond the edge, because "mark it non-authoritative" is
  not a licence to return an arbitrary number.
- **Clamping stays authoritative.** The edge value *is* supported data. The excursion is reported
  separately via `insideEnvelope`.

Only the first two policies are acceptable for live flight, and
`MavPropulsionModelBase.IsAcceptableForLiveFlight` — which is what readiness now consults, rather
than `HasAuthoritativeData` — enforces it. An authoritative deck configured to extrapolate is
rejected for flight.

### Powered trim

`ConvergedButThrustUnavailable` can now become a real powered result, with a third outcome between
them:

| Status | Meaning |
| --- | --- |
| `Converged` | closed using **accepted** data, or needed no thrust at all |
| `ConvergedWithNonAuthoritativeThrust` | closed, thrust available, but the data is synthetic/extrapolated. Never a reference result |
| `ConvergedButThrustUnavailable` | the engine cannot supply the required thrust |
| `ThrustNotMonotonic` | thrust cannot be inverted for a throttle at this condition |

A condition needing essentially no thrust is never downgraded — it required no thrust data to begin
with, which is why the F-16 glide trim still reports plain `Converged`.

### Throttle inversion is no longer assumed to be well posed

The old code took idle and full thrust, checked the required value fell between them, and bisected.
Both halves of that were unsound for a general deck:

1. **Monotonicity was assumed.** A deck that rises, dips and rises again has different endpoints
   while a bisection on it converges happily on a throttle that does not produce the requested
   thrust.
2. **The endpoints were treated as the range.** For the same non-monotonic curve the achievable
   thrust range is *wider* than its endpoints, so an endpoint-derived availability test rejects
   conditions the engine can actually hold.

Now a single fixed 17-point sweep answers both, in the right order: monotonicity first, and only
once it is established are the endpoints treated as the range. A non-monotonic deck yields
`ThrustNotMonotonic` and **no throttle is invented**. `[A5]` pins this with a deliberately folded
thrust curve.

### The synthetic bench deck

`MavSyntheticBenchThrustDeck` exists so the architecture can be tested end to end. Its safety
properties are structural, not procedural:

- `Authority` is hard-coded to `SyntheticBench`. There is no inspector field that can raise it.
- It therefore can never satisfy propulsion acceptance for live flight.
- The F-16 auto-setup never attaches it; it must be added by hand.
- Its numbers are round and obviously artificial.

Its shape (thrust falling with density ratio, a mild Mach term, sourced power blend on top) exists
only so the magnitudes are realistic enough to exercise the solver. None of it is a claim about any
aircraft.

### Phase 3A validation

| Section | Covers |
| --- | --- |
| `[A0]` | authority and live-flight acceptance rules; unconfigured and malformed decks; the sourced power blend |
| `[A1]` | deterministic bounded interpolation, clamping, genuine bounded extrapolation, repeatability |
| `[A2]` | the three excursion policies and their effect on authority |
| `[A3]` | **regression: F-16 dimensional thrust is still unavailable** and the shipped trim results are unchanged |
| `[A4]` | powered trim through a deck, including that the solved throttle really produces the required thrust, and that accepted data yields a true `Converged` |
| `[A5]` | monotonicity guard and refusal to invent a throttle |

---

## Phase 3B — Attitude and flight state

### The problem being solved

The Phase 2 control law converted a commanded load factor into a pitch rate with

```text
q = g * (n - 1) / V
```

which is the wings-level special case. In a bank it under-commands pitch rate, so a banked turn
sags. `MavFlightState` carried no attitude at all, so the law had nothing better to use.

### What was added

`MavAttitude` on `MavFlightState`, carrying pitch attitude, bank angle, heading, flight-path angle,
and three honesty flags: `valid`, `nearVerticalSingularity` and `flightPathValid`.

### Coordinate discipline

This is the part most likely to be broken by a later "simplification", so it is stated plainly.

Maverick already carries a handedness hazard between Unity local axes (X right, Y up, Z forward) and
aerodynamic body axes (X forward, Y right, Z down). The change of basis has determinant -1, so true
vectors and axial vectors convert differently, and `MavFlightDynamicsMath` keeps those cases apart
on purpose.

**`MavAttitudeMath` does not participate in that conversion at all.** Every angle is defined
geometrically, from world-space direction vectors and world "up":

```text
theta = asin(forward.y)                nose above the horizon
psi   = atan2(forward.x, forward.z)    compass direction of the nose
phi   = atan2(-right.y, up.y)          how far the right wing has dropped
gamma = asin(velocity.y / |velocity|)  climb angle of the velocity vector
```

None of those depend on rotation handedness, Euler ordering, or a basis change. They are statements
about where vectors point, which makes them immune to the class of sign error this project has
already hit. They must not be replaced with a quaternion-to-Euler extraction later.

`[B0]` asserts **physical directions**, not round trips: nose above the horizon gives positive
theta, right wing down gives positive phi, nose right increases psi, climbing velocity gives
positive gamma. Round-trip tests are deliberately not relied on, because a matching pair of sign
errors cancels in a round trip and survives it untouched.

### Corrected load-factor relation

```text
q = g * (n - cos(phi)) / V
```

At `phi = 0` this reduces exactly to the Phase 2 expression, so wings-level behaviour is unchanged -
`[B1]` and `[B2]` both pin that as a regression. In a steady coordinated level turn `n = 1/cos(phi)`,
and substituting gives `q = (g/V)·sin²(phi)/cos(phi)`, the standard result.

### Turn compensation

Neutral stick now commands the load factor that holds altitude at the current bank, `n = 1/cos(phi)`,
instead of a flat 1 g. A 45° bank commands 1.41 g, a 60° bank commands 2 g, and the aircraft holds
its turn without the pilot pulling manually - the War-Thunder-like usability the design goal asks
for, produced entirely through physical surface commands.

It is bounded and it withdraws rather than escalating:

- clamped to `maxTurnCompensationG` (default 4 g, Maverick tuning)
- withdrawn past `minimumCosBankForTurnCompensation` (default cos 75°), because approaching
  knife-edge, holding altitude is no longer a matter of pulling harder
- withdrawn entirely when the attitude reference is unavailable, degrading to the Phase 2
  wings-level relation rather than acting on a meaningless bank angle

Every one of those is reported in `MavF16ControlLawDebug`, so a withdrawal is visible rather than
inferred from handling.

This remains the **Maverick F-16 Control Law**. Nothing here is derived from the real F-16 FLCS.

### Phase 3B validation

| Section | Covers |
| --- | --- |
| `[B0]` | attitude physical directions, degenerate basis, vertical singularity, low-speed flight path, safe degradation |
| `[B1]` | the corrected q relation, its exact reduction to Phase 2 at wings level, and bounded turn-compensation load factor |
| `[B2]` | wings level, moderate bank, steep bank, positive-g turn, unloaded, command direction, bank-aware g limiting, and loss of attitude |

---

## Phase 3C — Atomic physics ownership — **CONDITIONAL**

### The problem being solved

Phase 2 could only *detect* a conflicting legacy physics owner and refuse to fly. That is safe but
inert: it can never produce a flying aircraft on the new path, because the legacy owner is always
there until something turns it off, and nothing was allowed to.

Phase 3C adds the something.

### What it is not

`MavPhysicsOwnershipController` is **not a third physics engine**. It applies no force, no torque
and no corrective anything. It decides *who* owns physics and flips exactly the flags that express
that. `[O0]`'s source scan covers it like every other file, and `[C2]` asserts explicitly that
disabling a component is not a Rigidbody write.

### States

```text
LegacyOwned          the legacy stack owns physics, new FDM disarmed
TransitionToNew      mid-handover
NewOwned             new FDM owns physics, legacy owners disabled
TransitionToLegacy   mid-hand-back
Fault                ownership could not be established or restored; fail closed
```

### The invariant

```text
legacy owner active AND new FDM armed  ->  NEVER
```

Not for a frame, not during a handover, not "briefly". `MavPhysicsOwnershipRules.ViolatesExclusiveOwnership`
is the single predicate, and it outranks whatever state the controller believes it is in: a
double-ownership observation is inconsistent with *any* state, and forces an immediate disarm.

### How atomicity is achieved

The controller runs at execution order **-20000**, ahead of the control law (-300), the actuator
(-200), `MavSixDoFBody` (-100) and any legacy owner (default 0). The entire sequence completes
inside one `FixedUpdate`, before any physics owner has run that step.

There *is* a window inside the sequence where neither owner is armed. It exists between statements,
not between physics steps, so no physics step ever executes with both owners and none ever executes
with neither.

### The sequence

```text
1. verify structurally prepared
2. verify every operational condition EXCEPT legacy ownership
3. disable legacy physical owners
4. re-observe: confirm they are actually inactive
5. invalidate the cached readiness
6. re-evaluate: full OPERATIONALLY_LIVE_READY must now hold
7. arm the new FDM
8. re-observe: confirm exclusive new ownership
```

Step 2 is the interesting one. Full readiness *requires* legacy ownership to be clear, which cannot
be true while legacy is still flying the aircraft — checking it first would deadlock the handover,
and skipping it would disable legacy physics on an aircraft that was never fit to take over.
`MavFlightDynamicsReadiness.EvaluateAssumingLegacyOwnershipCleared` relaxes that one criterion and
nothing else; `[C1]` asserts both halves of that.

Every step that changes something is followed by a step that confirms it changed, by re-observing
rather than by assuming.

### Rollback

Any failure restores the previous safe owner: the new FDM is disarmed and the legacy components are
re-enabled. **Only components this controller disabled are ever re-enabled** — a legacy component a
designer had deliberately switched off stays off, because restoring it would be the controller
inventing a configuration nobody asked for.

If a rollback cannot end a double-ownership condition, the controller enters `Fault` and holds the
new FDM disarmed until an operator calls `ClearFault()`. A fault is not healed by time passing or by
the condition happening to go away.

### Failure detection

While the new FDM owns physics, any of these hands ownership back:

| Condition | Detected via |
| --- | --- |
| legacy owner re-enabled | per-step legacy scan (no stale window, from the Phase 2 fix) |
| operational readiness lost | full readiness re-evaluation |
| operational input disappeared | command-path criterion |
| propulsion no longer acceptable | propulsion acceptance criterion |
| more than one control law enabled | **new** `singleControlLawEnabled` criterion |
| mismatched pipeline references | identity criteria from the Phase 2 closeout |
| ownership state inconsistent | `IsStateConsistent`, checked before any decision |

The hand-back disarms the new FDM *first*, because that is the statement that ends a double-ownership
condition, and it runs before any physics owner executes that step.

### Defaults

`autoTransitionToNewFdm` is **OFF** and `simulationEnabled` stays **OFF**. Ownership only moves when
something explicitly calls `RequestTransitionToNewFdm()`. The controller is **not** attached by the
F-16 auto-setup; it must be added deliberately.

### Why CONDITIONAL

The mechanism is complete and its rules are exhaustively validated, but it has never actually
performed a handover on a real aircraft — because no aircraft on this branch is operationally
live-ready, and none can be until the F-16 has an accepted propulsion model and a real command
source. 3C becomes PASS after a handover has been demonstrated in Unity on an aircraft that
genuinely reaches `OPERATIONALLY_LIVE_READY`.

### Phase 3C validation

| Section | Covers |
| --- | --- |
| `[C0]` | the exclusive-ownership invariant, and that it outranks any believed state |
| `[C1]` | handover preconditions, completion confirmation, and the readiness-assuming-release resolution of the chicken-and-egg |
| `[C2]` | every hand-back cause, fault semantics, and that the controller applies no physics |

---

## Independent review fixes

Three blockers were found by independent review after the initial Phase 3 commits. All three were
real, and all three are fixed with regressions that were verified to fail without the fix.

### 3B.6 — attitude was never published by the runtime path

`MavFlightState.attitude` and the bank-aware control law shipped, and every attitude test passed,
while `MavSixDoFBody` published **no attitude at all**. The live control law therefore always saw
`attitude.valid == false` and fell back to its wings-level behaviour: the entire 3B improvement was
dead in the runtime path.

Cause: the patch that was supposed to add the publication used a text replacement whose anchor did
not match, and it was applied without asserting. It silently did nothing.

Why the tests did not catch it: the Phase 3B helper **built its own flight state** and called
`MavAttitudeMath` directly. It exercised the attitude maths and never touched the runtime wiring.

Fix:

- `MavSixDoFBody.BuildFlightState(...)` is now a public static that builds the whole per-step state,
  attitude included. `UpdateStateAndAtmosphere` calls it, and so does validation.
- `[B3]` drives that production builder and asserts `attitude.valid`, wings-level zero, positive
  bank for right wing down, negative for left, and that the control law's turn compensation actually
  responds to the production-published attitude.

Verified to bite: with the publication removed again, `[B3]` fails 5 checks — while `[B0]` and
`[B2]` still pass, which is precisely the blind spot that existed.

### 3C.5 — false `LegacyOwned`

`ReturnToLegacy`, `ClearFault` and `IsStateConsistent(LegacyOwned, ...)` all treated "the new FDM is
disarmed" as sufficient to declare legacy ownership. An aircraft whose legacy owner was destroyed,
missing, or could not be re-enabled would be committed to `LegacyOwned` with **nothing flying it**.

Fix:

- New state `Unowned`: nobody owns physics, and that is a *declared* configuration - an aircraft
  with no legacy physics owner components at all, such as a bench rig. It is deliberately not called
  `LegacyOwned`, because that would assert an owner that does not exist.
- New observation `legacyOwnerPresent`, separating "legacy is switched off" from "there is no legacy
  stack here".
- `MavPhysicsOwnershipRules.ResolveSettledOwnership` decides between three distinct facts:

  | observation | settles as |
  | --- | --- |
  | a legacy owner is active | `LegacyOwned` |
  | none active, none exists | `Unowned` |
  | none active, but one **exists** | `Fault` |

- `RestoreLegacyOwners` now returns success and **verifies** it: a destroyed component reads as null
  through Unity's overloaded operator and cannot be restored; a component that fails to become
  enabled is detected by re-reading the flag rather than assuming the write took.
- `IsStateConsistent(LegacyOwned, ...)` now requires an actually active legacy owner.
- `ClearFault` no longer asserts an owner into existence: it settles into whatever is actually true,
  which can legitimately remain `Fault`.

`[C3]` covers all of it, including the destroyed/unrestorable case.

### 3C.6 — execution-order audit

Audited against the project as it actually is, not as assumed.

| Component | Exists? | Execution order | Writes Rigidbody motion |
| --- | --- | --- | --- |
| `MavAeroBody` | yes | default (0) | yes |
| `MavAtmosphericEngine` | yes | default (0) | yes |
| `MavMouseFlightJet` | yes | default (0) | yes |
| `MavThrustVectorControl` | yes | default (0) | yes |
| `MavInstructorController` | yes | default (0) | no |
| `MavAeroTorqueAssist` | **absent** | - | - |
| `MavSimpleJetEngine` | **absent** | - | - |
| `MavFlightAssist` | **absent** | - | - |
| `MavPlayerPhysicsConfig` | **absent** | - | - |

`ProjectSettings` contains **no `MonoManager.asset`**, so there are no custom script execution order
overrides in the project at all. No legacy physics owner carries a `DefaultExecutionOrder`
attribute either.

Conclusion: every legacy physics owner runs at order 0. The ownership controller at **-20000** runs
ahead of all of them, ahead of the control law (-300), the actuator (-200) and `MavSixDoFBody`
(-100). **The atomicity guarantee holds.**

The four absent names are kept in the deny-list defensively. Matching is by type name, so listing a
type that does not exist costs nothing and covers it if it is added later.

One open question for Phase 4, recorded rather than acted on: `MavLandingGearSystem` also applies
forces to the aircraft Rigidbody. It is ground-reaction rather than flight aerodynamics, so it is
deliberately **not** on the deny-list - but an aircraft on the ground with the new FDM live would
have two systems applying forces to one body. That needs a decision before any live takeover on the
ground.

### 3A.7 — authority escalation loophole

`MavTabulatedThrustDeck` exposed `declaredAuthority` as a plain inspector enum. Any arbitrary table
could be set to `Authoritative`, and with a clamping policy it would then pass
`IsAcceptableForLiveFlight`. A citation string sat next to it, but nothing verified the words
matched the numbers.

Fix: **authority is derived, never declared.**

- The `declaredAuthority` field is gone. `dataSourceCitation` remains but is documented as
  documentation only - it does not affect authority.
- `MavTabulatedThrustDeck.Authority` is computed by `ResolveAuthority(hasUsableTables,
  frozenProvenanceVerified)`, and the base class's `VerifyFrozenProvenance` **always returns false**.
  An arbitrary table therefore caps out at `SyntheticBench` no matter how it is configured.
- `MavFrozenThrustDeckBase` is the only route to `Authoritative`. A subclass must declare a source
  identity, a version, and a content hash; `MavThrustDeckProvenance` recomputes that hash from the
  live table arrays and requires a match. Editing one thrust value changes the hash and the deck
  immediately stops claiming authority, so the numbers and the claim cannot drift apart.
- There is no inspector field anywhere in the hierarchy that raises authority.

`[A6]` proves the escalation is closed: an arbitrary table is not acceptable for live flight under
**any** of the three excursion policies, a single-value edit moves the hash, and identity and version
are part of the hashed content so a hash cannot be reused across sources.

The hash is FNV-1a over exact float bit patterns. It is not cryptographic and is not meant to resist
an attacker - it is meant to make the careless case impossible: "I tweaked a number and it is still
labelled as reference data".

No `MavFrozenThrustDeckBase` subclass exists for the F-16, because no F-16 thrust deck has been
frozen from an approved source. That absence is the honest state of the project.

### Phase 3 validation after the fixes

| Section | Covers |
| --- | --- |
| `[B3]` | attitude published by the PRODUCTION state builder, wings level and both bank directions, and the control law responding to it |
| `[C3]` | no false `LegacyOwned`; `Unowned` versus `Fault`; destroyed/unrestorable legacy owner |
| `[A6]` | authority cannot be escalated from the inspector under any policy; provenance hash binding |

---

## Freeze-review follow-up

A second independent review found two further production blockers and six validation defects. All
eight are addressed.

### F1 - ownership arbitrated on a stale command result

The controller ran at **-20000**, ahead of everything, and read the control law's command resolution
from the PREVIOUS step. A source that dropped out on the current step still looked healthy to it:

```text
step N-1: SourceSignal
step N:   source drops out
          arbiter (-20000) sees stale SourceSignal -> releases legacy, arms new FDM
          law (-300)       observes the dropout
          body (-100)      refuses loads: no valid command path
          legacy (0)       already disabled
          -> a physics step with NO owner
```

Fixed by reordering:

```text
control law        -300
ownership arbiter  -250   <- moved here
actuator           -200
six-DoF body       -100
legacy owners         0
```

The arbiter now decides on the CURRENT step's command result while still preceding both physics
owners. The law running before ownership is settled is harmless: it may publish a surface command on
a step where legacy ends up owning the aircraft, and the legacy stack does not read those surfaces.

`[I3]` proves it on real components, and demonstrates the failure by deliberately stepping the
arbiter ahead of the law and showing that ordering releases legacy on a dropped signal.

### F2 - a failed legacy restoration could settle as Unowned

`RestoreLegacyOwners()` returned false for a destroyed owner and then cleared its tracking list.
`Observe()` subsequently saw `legacyOwnerPresent == false`, `ResolveSettledOwnership` returned
`Unowned`, and `SettleAfterRelease` explicitly permitted `restoreSucceeded == false` when the settled
state was `Unowned`. The controller would report a healthy bench rig for an aircraft it had just
taken the physics away from.

Fixed: the observation now carries `legacyRestorationWasRequired` and `legacyRestorationSucceeded`,
and `ResolveSettledOwnership` faults whenever a restoration was required and did not put an active
owner back - whether or not the component still exists. **`Unowned` is now reachable only when no
restoration was required at all.** `IsStateConsistent` enforces the same.

`[C3]` covers the rule; `[I4]` destroys a real disabled component mid-handover and asserts the
hand-back becomes `Fault`.

### Validation defects

| # | Defect | Fix |
| --- | --- | --- |
| 3 | `[A6]` contained an unconditional `|| true`, so one assertion could never fail | replaced with a real changed-identity test: recompute the hash AS the other source and verify it against the original declaration |
| 4 | `[B3]` checked only attitude | expanded to the full `BuildFlightState` contract: position, world velocity, aero-body velocity, p/q/r through the axial-vector conversion, TAS, Mach, qbar, alpha, beta, attitude, and initial specific-force invalidity |
| 5 | specific force was tested by assigning the field | now goes through `MavSixDoFBody.PublishSpecificForce`, the same path production uses, including non-finite and zero-mass rejection |
| 6 | `[R4]` rebuilt the readiness mapping instead of running it | `MavPipelineSnapshot` + `MavFlightDynamicsReadiness.BuildInputs` is now the single mapping; the body captures a snapshot and validation feeds one in |
| 7 | no component-level coverage at all | new Unity integration suite, below |
| 8 | CSV validation compared header count against constants | now serializes a real row through `BuildCsvRow` and checks count **and order**, looking each column up by header name |

### The Unity integration layer

`MavFlightDynamicsIntegrationValidation` builds real GameObjects with real components, wires them the
way the runtime does, and drives them in the real execution order. Nothing in it fabricates a
`MavOwnershipObservation`, a `MavPipelineSnapshot` or a `MavFlightState`.

| Section | Covers |
| --- | --- |
| `[I0]` | command source -> control law -> actuator -> body, and attitude published from a real transform |
| `[I1]` | engine `Evaluate` -> thrust deck -> summed loads, with and without a deck |
| `[I2]` | ownership handover, hand-back and rollback on real components |
| `[I3]` | **current-step command dropout at the handover boundary**, plus the stale-ordering demonstration |
| `[I4]` | **destroyed legacy owner** produces `Fault`, not `Unowned` |
| `[I5]` | bench rig with no legacy owner settles as `Unowned` |

To make this possible each component's per-step work is now a public method - `StepControlLaw`,
`StepActuator`, `StepPhysics`, `StepOwnership` - with the timestep passed in rather than read from
`Time`. `FixedUpdate` just calls it. That is a better shape independent of testing, and it is what
lets the suite order the pipeline itself rather than depending on Unity's scheduler.

The suite lives under `Editor/` and never ships. `MavIntegrationTestLegacyOwner` is a stand-in that
applies no physics, so no real legacy component is touched; the controllers under test have their
deny-list pointed at it. The ownership scan exempts the integration rig, narrowly and by name,
because it assigns a Rigidbody velocity to establish an initial condition - and that exemption is
itself pinned by a test.

### Counts, reported separately

| Suite | Count | Status |
| --- | --- | --- |
| pure / static (reference, Phase 1, propulsion, Phase 2, Phase 3) | **475 passed, 0 failed** | run offline |
| Unity component integration | **not yet executed** | requires the Unity editor |

The integration suite is written and compiles, but it has never been run: it needs real
`GameObject`/`Component`/`Rigidbody` behaviour that the offline harness cannot provide. Its result is
unknown, and it is deliberately **not** added to the 475.
