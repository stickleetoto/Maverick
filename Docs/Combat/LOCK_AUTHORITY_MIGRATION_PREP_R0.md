# Lock Authority Migration Prep R0

Base: `1703a38dbdf2245692cf9280da588297490c1464` (main, after Lock Consumer Migration R0)

**This phase moves no authority.** `preferAuthoritativeLock` stays `false`, all three legacy owners keep
running, issue #16 stays open. What it produces is the evidence that the eventual move needs: a complete
inventory of who consumes lock, selection and designation state today, and a disagreement signal that
actually reports during the window in which the decision is made.

## 1. The defect this phase fixes

`MavEngagementView.legacyDisagreesWithAuthoritative` was computed only while `preferAuthoritativeLock`
was `true`:

```csharp
if (preferAuthoritativeLock && authoritativeLockTrackId != 0)   // before
```

The migration plan keeps that switch `false` until consumers have migrated. So the one signal the plan
relies on — "do the legacy owners agree with the new authority?" — could only be answered *after* the
switch had been thrown. It could confirm a decision, never inform one.

The sharper problem is that it made the retirement criterion vacuous. `RADAR_TRACK_LOCK_R0.md` §7 says to
retire the legacy authorities one at a time, "each with `legacyDisagreesWithAuthoritative` staying false as
the evidence". A flag that is false because nothing computes it is indistinguishable, to a reader, from a
flag that is false because everything agrees. The criterion could have been signed off on an empty field.

### The change

One condition, plus the reasoning that keeps it:

```csharp
if (authoritativeLockTrackId != 0)                              // after
```

**Precedence and disagreement are different questions.** `PrimaryTrackId` answers *which* claim consumers
get, and the switch still decides that — unchanged, legacy order, asserted in both positions.
`legacyDisagreesWithAuthoritative` answers *whether the two available claims differ*, and nothing about
precedence should be able to hide that.

Zero still means no disagreement on both sides, for the same reason in each direction: a zero
authoritative id means the authority holds no committed lock, so there is nothing to disagree *with*
(the publisher is the controller, publishing `LockedTrackId`, which is 0 unless the lock is held), and a
zero legacy id is a claim of nothing. Absence is not conflict.

Nothing else in `MavEngagementView` changed. `PrimaryTrackId`, `PrimarySourceName`, every publisher, the
legacy state projections and `ClearAll` are untouched.

## 2. Consumer inventory

Everything in the active `MaverickFresh` combat path that reads lock, selection or designation state. The
classification is the one the migration plan needs: what each consumer actually wants, which is not always
what it currently reads.

### LOCK STATE

| Consumer | Reads | Notes |
|---|---|---|
| `MavFreshHud` `AuthoritativeLockHudLine` / `AuthoritativeLockDebugHudLine` | `IMavTrackLockAuthority`, `MavEngagementView` | **Already migrated** (Consumer Migration R0). Read-only, fails closed. |
| `MavLockHudPresentation` | `IMavTrackLockAuthority`, `MavEngagementView` | Pure presenter for the above. Derives disagreement itself; now asserted to agree with the view's field. |
| `MavFreshHud:267`, `:288` (TGP line) | `targetingPod.isLocked` | Legacy pod lock, read directly. Includes bare **point** locks. |
| `MavSensorHudOverlay:32` | `sensor.debugHasLock`, `sensor.debugLockProgress01` | Legacy STT lock and its acquisition progress, read directly off the sensor suite. |

### TARGET SELECTION

| Consumer | Reads | Notes |
|---|---|---|
| `MavF22SensorSuite` (self) | `selectedTarget`, `selectedIndex` | **LEGACY OWNER.** Scans, selects, times its own STT lock. |
| `MavCASTargetingSystem` (self) | `designatedTarget`, `candidateTarget`, `designatedPoint` | **LEGACY OWNER.** Cycles and designates. |
| `MavTargetingPodSystem:376-388` | own `lockedTarget` / `lockedToTarget` / `isLocked` | **LEGACY OWNER.** Also *writes* the CAS designation at `:405-420`. |
| `MavPhysicalAIController:280-283` | writes `casTargeting.designatedTarget` / `designatedPoint` | AI authoring designation. A second writer of a legacy owner's state. |

### TARGET GEOMETRY

| Consumer | Reads | Notes |
|---|---|---|
| `MavAirToAirLeadSight:109` | `sensorSuite.selectedTarget` (GameObject + velocity) | **DEFERRED.** Consumes geometry, not lock state. |
| `MavSensorHudOverlay:39-40` | `sensor.selectedTarget.transform.position` | Marker placement; geometry plus a lock label in one call. |
| `MavCASTargetingSystem:188-203` | `GetDesignatedPoint()` / `GetDesignatedOrCandidateTarget()` | Serves geometry to the weapon system, target **or bare point**. |

### WEAPON / FIRE CONTROL

| Consumer | Reads | Notes |
|---|---|---|
| `MavCASWeaponSystem:738` (precision) | `targeting.hasDesignatedPoint`, `GetDesignatedOrCandidateTarget()` | **Release authority.** Refuses with `precision_no_designation`. |
| `MavCASWeaponSystem:767` (missile) | same | Refuses with `missile_no_designation`. |

This is the only place where lock/designation state gates a weapon, and it gates on a **bare ground
point** as readily as on a target. Worth stating plainly: it is air-to-ground release, it predates all of
this, and the track vocabulary cannot express what it consumes.

### PRESENTATION

`MavFreshHud:266` (TGT line: `designatedTarget` / `candidateTarget` / `status`), `MavFreshHud:347` (the
DES/STT/POD debug line off the view), `MavTargetingPodSystem:659-701` (its own MFD and gizmos),
`MavCASTargetingSystem:213-217` (gizmos).

### DEBUG / VALIDATION

`MavLegacyEngagementProbe` — reads all three legacy owners and projects them into the view. This is the
observe half of the migration, not a consumer with an opinion. `MavTrackLockValidation`,
`MavTargetTrackValidation`, `MavLockConsumerMigrationValidation`,
`MavLockAuthorityMigrationPrepValidation`.

### Not lock consumers, though they look like it

`MavFreshHud:371`, `MavControlDebugOverlay:67`, `MavMouseFlightRig:470`, `MavInstructorController:1172`,
`MavTGPStateManager` — all read `targetingPod.displayMode`, i.e. whether the pod MFD is fullscreen. No
lock state. Listed so a later sweep does not mistake them for migration work.

## 3. What flipping `preferAuthoritativeLock` today would change

**Nothing in gameplay.** `PrimaryTrackId` and `PrimarySourceName` have **no runtime consumer at all** —
only `MavTrackLockValidation`, `MavTargetTrackValidation` and `MavLockConsumerMigrationValidation` read
them. Every gameplay consumer in the inventory above either reads the authority directly (the migrated HUD
lines) or reads a legacy owner directly (everything else). Neither path goes through the switch.

The HUD's migrated lines are asserted invariant under the switch (`M-035`, `M-035b`), which is the
property that matters: a consumer that changed its output when precedence changed would be reading
precedence, not the lock.

So the switch is safe to flip in the narrow sense that nothing observes it — and flipping it would
accomplish nothing. **That is the finding.** Authority does not move by changing a default; it moves when
the consumers reading legacy owners directly stop doing so. The switch is the last step, not the lever.

## 4. What still blocks the authority switch

In the order they have to be dealt with:

1. **`MavCASWeaponSystem`** — release authority on the CAS designation, including bare points. Needs a
   designation contract the track vocabulary does not have. Hardest, and nothing should touch weapon
   release until the rest is settled.
2. **`MavAirToAirLeadSight`** — deferred by decision. Consumes target geometry and needs a
   prediction/track-extrapolation design before it can leave its direct-target path.
3. **`MavSensorHudOverlay`** — reads `debugHasLock`, `debugLockProgress01` and `selectedTarget.transform`
   off the sensor suite. The lock half is migratable now; the marker position is geometry, so it has the
   same dependency as the lead sight.
4. **`MavFreshHud` TGP line** — `targetingPod.isLocked` covers point locks, which have no track.
5. **`MavTargetingPodSystem` → `MavCASTargetingSystem`** — one legacy owner writes another's state. Until
   that handoff is represented, retiring either changes the other.
6. **`MavPhysicalAIController`** — a second writer of the CAS designation. Any designation contract has to
   accommodate an AI author, not just the player.

### The representational blocker underneath four of those

A pod point lock and a CAS point designation are **locks on a bare ground point**. `MavTargetTrackOwner`
issues track ids for observed contacts; a point nobody is tracking has no id, and `MavLegacyLockProjection`
correctly maps both to `Idle` for exactly that reason. Forcing them through `IMavTrackLockAuthority` would
delete real state to make the architecture look tidier. Either the authority gains a point-designation
concept, or designation stays legacy and only *track* locks migrate. That decision is not this phase's.

## 5. Validation

New suite: `MavLockAuthorityMigrationPrepValidation` — **51 assertions, 51/0**.

| Required proof | Assertions |
|---|---|
| 1. disagreement visible while `preferAuthoritativeLock == false` | `M-002`, `M-007`–`M-007c`, `M-008b`, `M-031` |
| 2. agreement clears the signal | `M-004`, `M-008`, `M-033` |
| 3. zero legacy claims are not disagreement | `M-005`, `M-034` |
| 4. no authoritative committed track means no disagreement | `M-006`, `M-013`, `M-036`–`M-036d` |
| 5. toggling the switch does not change disagreement truth | `M-010b`, `M-012`, `M-013` |
| 6. toggling the switch DOES change `PrimaryTrackId` precedence | `M-011`, `M-011b`, `M-011c` |
| 7. the signal is read-only / diagnostic | `M-020`–`M-024` |
| 8. HUD consumer behavior unchanged | `M-035`–`M-035d` |
| plus: the HUD workaround agrees with the corrected field | `M-032`, `M-033`, `M-034`, and `C-012c` |
| plus: the gate cannot come back | `M-040`–`M-043` |

`M-030`–`M-036d` drive the real `MavTrackLockController` with an explicit clock, so the id the signal
compares against is a genuine committed lock rather than a hand-written field. The mapping cases
(`M-001`–`M-014`) set ids directly on purpose: they have to cover combinations no controller would produce
in one step.

`M-040`–`M-043` are a source-level tripwire, because the defect was one token and re-introducing it would
look like a tidy-up. It asserts that `RefreshDisagreement` does not mention `preferAuthoritativeLock` and
that `PrimaryTrackId` still does — both halves, so moving the gate from one to the other fails. Comment
lines are stripped before the check; the code deliberately discusses the switch in the comment above the
computation, and a naive substring search would forbid it explaining itself.

### The suite was negative-tested

Restoring the one-token gate and re-running: **39 passed, 11 failed** — `M-002`, `M-007`, `M-007b`,
`M-007c`, `M-008b`, `M-010`, `M-010b`, `M-020`, `M-031`, `M-032`, `M-041`. Both the behavioral cases and
the structural tripwire bite.

`M-020` failed in that run for the wrong reason: it had asserted idempotence *and* that the value was
true in one condition, so a signal stuck at false failed an idempotence check it actually satisfied. Split
into `M-020` (stable across recomputation) and `M-020b` (and the case it was stable across is a genuine
disagreement), so neither claim can be satisfied vacuously by the other.

### Existing suites

Three assertions changed expectation because the behavior they pinned was the defect. Counts unchanged.

- `L-065d` — was "with the migration not in effect there is no disagreement to report". Now asserts the
  disagreement **is** reported while the default is legacy.
- `L-066d` — was "the switch is reversible ... and loses nothing either way", asserting the flag went back
  to false. Now asserts it loses neither the lock nor the disagreement.
- `L-066b` — same expectation, reworded: the disagreement is *still* reported once the move is in effect,
  rather than *becoming meaningful* then.
- `C-012c` — was "it is visible even though the view's own flag stays off with the legacy default". Now
  asserts the view's field and the consumer's derivation **agree**, which is the property worth keeping.

### The HUD workaround is kept

`MavLockHudPresentation.AuthoritativeDisagreesWithLegacy` still derives disagreement rather than reading
the corrected field, for a reason that outlives the defect: the field is a snapshot, only as fresh as the
last `RefreshDisagreement`, while the presenter reads the authority's live committed lock at draw time. A
panel showing DISAGREE one frame late would be reporting the publisher's cadence. `M-032`–`M-034` assert
the two agree, so they cannot drift into two definitions of one word.

## 6. Gates

| Gate | Result |
|---|---|
| Compile | 0 errors |
| Lock Authority Migration Prep | **51 / 0 PASS** |
| Lock Consumer Migration | **75 / 0 PASS** |
| Track Lock | **147 / 0 PASS** |
| Radar Core | **71 / 0 PASS** |
| TargetTrack Core | **35 / 0 PASS** |
| Combat boundary | **3 / 0 PASS** |
| FDM baseline | **1585 / 0 PASS** |
| Missing Script scan | **PASS** |

## 7. Scope held

Not done, deliberately: `preferAuthoritativeLock` not flipped; no legacy authority retired; issue #16 left
open; `MavAirToAirLeadSight` untouched; no weapon authorization, missile core, seeker, guidance or
datalink; FDM untouched. `MavEngagementView`'s only change is the one condition and its comments;
`MavTrackLockController`, `MavTargetTrackOwner`, the legacy owners and `MavFreshHud` are unchanged.

## 8. Next

The consumer migrations in §4, in that order — each one a separate phase, and the decision about bare
point designations before any of the CAS work. `preferAuthoritativeLock` flips after them, not before,
and the legacy owners retire after that, one at a time, with `legacyDisagreesWithAuthoritative` as the
evidence. It can now actually serve as that evidence, which was the point of this phase.
