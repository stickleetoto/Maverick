# Lock Consumer Migration R0

The first gameplay-facing consumer reads the one lock authority. Authority itself does **not** move, and
no legacy lock owner is retired.

| | |
|---|---|
| Base | `0154e3ef0db7a547f98bc7de36dd59f33d3a10b6` (Radar Track/Lock R0 merge) |
| Branch | `sol/combat-lock-consumer-migration-r0` |
| Unity | `6000.3.16f1` |

`preferAuthoritativeLock` is still **`false`**. `MavTargetTrackOwner`, Radar Core and all three legacy lock
authorities are unchanged. Issue **#16 remains open**.

**Not implemented, deliberately:** missile launch logic, AIM-120, AIM-9, seeker logic, guidance, missile
autopilot, datalink, ECM/ECCM, clutter, PRF, notch modelling, lead-sight migration, legacy retirement, any
second consumer, any change to global authority.

## 1. What the HUD read before

Three HUD readouts touched target state, and they mean **three different things**. Conflating them was the
main risk in this phase, so they are listed exactly as they were.

| Readout | Panel | Source | Meaning |
|---|---|---|---|
| `TGT …` | developer (F2) | `casTargeting.designatedTarget` / `.candidateTarget` / `.status`, read **directly from the legacy component** | CAS designation, **including a bare ground point** (via `status`) |
| `TGP … LOCK/SEARCH` | **player** and developer | `targetingPod.isLocked`, `displayMode`, `fov`, read **directly from the legacy component** | targeting-pod lock, **`isLocked` is true for a bare point lock too** |
| `TRK … DES n STT n POD n [DISAGREE]` | developer (F2) | `trackOwner` counters plus `engagementView.designatedTrackId` / `sensorLockTrackId` / `podLockTrackId` / `authoritiesDisagree` | TargetTrack Core R0 diagnostics: what each legacy authority claims, as track ids |

So before this phase the HUD had **no representation of aircraft lock state at all**. The only lock-ish
thing a player could see was `TGP LOCK`, which is a pod state and can mean a patch of ground.

The HUD read `authoritiesDisagree` but **never** `PrimaryTrackId`, `authoritativeLockTrackId`, or
`authoritativeLockState`. Confirmed by enumerating every non-test reference to all five fields.

## 2. What the HUD reads now

Two additions. Nothing existing was changed.

- **Player panel:** one new line from `MavLockHudPresentation.FormatPlayerLine` —
  `LOCK ---` / `LOCK SEL` / `LOCK ACQ 62%` / `LOCK LOCKED <name>` / `LOCK COAST <name>`, and **nothing at
  all** when the authority is unavailable.
- **Developer panel:** one new line from `FormatDebugLine` — the full lifecycle with track id, name, hold
  time and last loss reason, followed by each legacy authority's own projected state and a `DISAGREE`
  marker.

The handle is typed `IMavTrackLockAuthority`, never the concrete controller.

### How it is resolved, and why that way

```csharp
if (lockAuthority == null && engagementView != null)
    lockAuthority = engagementView.GetComponent<IMavTrackLockAuthority>();
```

The authority lives on the same GameObject as `MavEngagementView`, which the HUD already holds, so a
`GetComponent` on that object is enough — and `GetComponent<T>()` accepts an interface, so no concrete type
is named. **The HUD's scene-search count is unchanged at 53**, pinned by an assertion so a later change has
to move the number deliberately.

## 3. Semantic separation, kept

> DETECTION ≠ TRACK ≠ SELECTION ≠ LOCK ≠ WEAPON AUTHORIZATION

`Selected` and `Acquiring` render as `SEL` and `ACQ`, visibly not `LOCKED`. Nothing is shown as locked
because a track exists, because the radar sees something, because `PrimaryTrackId` is non-zero, or because
a legacy authority claims it — the presentation reads `LockState` from the authority and nothing else.

`Coasting` is shown as a live commitment, because the commitment has not been abandoned and a HUD that
dropped the symbol on one missed observation would misreport what the aircraft is doing. It renders as
`COAST`, never as `LOCKED`, and `StateLabel` is asserted never to give the two states the same label.

Weapon authorization does not appear at all. Nothing here knows what a lock is *for*.

## 4. What stayed legacy, and why

**`TGT` (CAS designation) and `TGP` (pod) are untouched.** Both can refer to a bare ground point, and the
track vocabulary deliberately has no way to express that — the owner holds no track for a patch of ground.
Routing them through `IMavTrackLockAuthority` would have deleted real state to make the architecture look
tidier. They keep their own fields and their own lines.

The developer line also keeps the three legacy lifecycle projections beside the authoritative one, so a pod
point lock still reads `pod IDLE` and is still visible as such.

## 5. Fail-closed presentation

When `IsLockAuthorityActive` is false the player line is the **empty string** — the line disappears rather
than freezing on its last value — and the debug line says `AUTH offline` instead of showing a lock.

Nothing is cached. `MavLockHudPresentation` is a static class whose methods take their inputs as
parameters, so there is no field a previous lock could survive in, and the strings are rebuilt from the
authority every frame.

## 6. One interface addition

`IMavTrackLockAuthority` gained `float AcquisitionProgress01 { get; }`.

A consumer that can read `LockState` but not the progress behind `Acquiring` can only show that something
is happening, not how close it is. The alternative was for the HUD to reach past the interface to the
concrete controller, which is the coupling the interface exists to prevent. The controller already had the
property, already fail-closed, so this is an interface widening with no behavior change.

## 7. A finding: the view's disagreement flag is dead while the migration is in progress

`MavEngagementView.legacyDisagreesWithAuthoritative` is only computed while `preferAuthoritativeLock` is
**on**. This phase deliberately leaves it **off** — so that flag is always false for exactly as long as the
migration is under way, which is precisely when disagreement is worth watching.

Rather than change the view (out of scope) or read a field known to be dead,
`MavLockHudPresentation.AuthoritativeDisagreesWithLegacy` compares the published ids directly. It is
read-only, costs nothing, and works whichever answer currently wins. `C-012c` asserts that disagreement is
observable *while the view's own flag stays false*, so the gap is documented by a test rather than by a
comment.

## 8. Validation

`MavLockConsumerMigrationValidation`, **61 assertions, 0 failures**, deterministic across two independent
Unity launches. Every case drives the real `MavTrackLockController` with an explicit clock, so the lifecycle
being presented is the real one rather than a mock.

| Requirement | Assertions |
|---|---|
| 1. Idle → no authoritative lock | `C-001` |
| 2. Selected → not shown as Locked | `C-002`, `C-002b`, `C-002c` |
| 3. Acquiring → state and progress | `C-003`, `C-003b`, `C-003c` |
| 4. Locked → track id, name, state | `C-004`–`C-004d` |
| 5. Coasting → commitment kept, distinguishable | `C-005`–`C-005d` |
| 6. authority disabled → fails closed immediately | `C-006`–`C-006f`, `C-007`, `C-008`–`C-008e` |
| 7. legacy default remains | `C-010`, `C-010b` |
| 8. legacy-only pod/designation data still visible | `C-011`–`C-011d` |
| 9. disagreement observable | `C-012`–`C-013b` |
| 10. no HUD path writes selection or lock state | `C-020`–`C-024c` |

Requirement 10 is checked by reading the HUD source, because the absence of a call is not observable at
runtime: 16 forbidden tokens (`RequestSelection`, `BreakLock`, every `Publish*`, `RegisterFeed`,
`CollectObservations`, the test seams, `EnableLockAuthority`, `MavTrackLockController`, `MavRadarSensor`,
`MavF22SensorSuite`), the pinned scene-search count, and the presenter being static.

### Regressions were injected to prove the tests bite

Removing the availability check from `FormatPlayerLine` and adding one scene search to the HUD produced
exactly two failures — `C-006b` and `C-021` — and nothing else.

That negative run also exposed a **weak test of mine**: `C-006c` kept passing with the guard removed,
because `EnableLockAuthority = false` makes the controller withdraw internally, so the state was already
`Idle` and the presentation guard was never the thing under test. `C-008`–`C-008e` were added to exercise
the guard where it stands alone: losing the track source performs no withdrawal and raises no callback.

Isolating those cases in their own host mattered too. Run inline, restoring `owner` afterwards left a lock
that had never been withdrawn — the owner-reassignment residual recorded in `RADAR_TRACK_LOCK_R0.md` §8.2 —
and it leaked into `C-007`, which is about something else.

### Gates

| Gate | Result |
|---|---|
| Compile | **0 errors** |
| Lock consumer migration | **61 / 0 PASS** |
| Track Lock | **147 / 0 PASS** (unchanged) |
| Radar Core | **71 / 0 PASS** (unchanged) |
| TargetTrack | **35 / 0 PASS** (unchanged) |
| Combat boundary scan | **3 / 0 PASS** |
| FDM baseline | **1585 / 0 PASS** |
| Missing Script | **PASS** |

## 9. Player-visible change

**Yes, one, and it is additive:** the player panel gains a `LOCK …` line. No existing line was altered,
removed or re-sourced, and `TGP LOCK/SEARCH` behaves exactly as before. The line shows information the HUD
previously did not have at all — aircraft lock lifecycle — and it disappears entirely when the authority is
unavailable.

Global authority did not move: `PrimaryTrackId` still answers the legacy order, and `C-010b` asserts it.

## 9.1 Known debt

Carried deliberately out of this phase, not fixed here.

**`MavEngagementView.legacyDisagreesWithAuthoritative` is not yet a usable migration-evidence signal.**
It is only computed while `preferAuthoritativeLock` is `true`, and the migration plan keeps that `false`
until consumers have moved - so for the whole duration in which disagreement between the authoritative
lock and the legacy authorities is the evidence the migration depends on, the flag reads `false` because
nothing computed it, not because the authorities agree.

That matters because the retirement criterion in `RADAR_TRACK_LOCK_R0.md` §7 is "retire the legacy
authorities one at a time, each with `legacyDisagreesWithAuthoritative` staying false as the evidence".
As it stands that criterion would be satisfied vacuously.

This phase works around it without touching the view: `MavLockHudPresentation.AuthoritativeDisagreesWithLegacy`
compares the published ids directly, and `C-012c` asserts disagreement is observable while the view flag
stays false. So the HUD is not blind - but the field itself is still misleading to anyone who reads it
expecting evidence, and the fix belongs in whichever phase next touches `MavEngagementView`.

## 10. Next

The remaining steps, in order, none of them in this phase:

1. Migrate any further consumers that need the lock (a lead sight or a launch authorization path would be
   the real test, since both would *act* on it rather than draw it).
2. Only once consumers read the authoritative answer, flip `preferAuthoritativeLock` to `true`.
3. Only after the flip, and with `legacyDisagreesWithAuthoritative` staying false as evidence, retire the
   legacy authorities one at a time.

`MavAirToAirLeadSight` currently reads the F-22 sensor suite's selected target directly, which makes it the
obvious next candidate — and the one the brief explicitly defers.
