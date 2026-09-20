# Radar Track/Lock R0

One authoritative lock: selection, acquisition, maintenance, coast and loss, expressed entirely in
track ids, with the three legacy lock authorities represented before any authority moves.

**Authority has not moved.** The new lock path is opt-in: `preferAuthoritativeLock` defaults to
`false`, so every consumer still gets the legacy answer. Consumers migrate first.

| | |
|---|---|
| Base | `1d722fde41fe49d85d3f1fc3334ea3be5e7b9f39` (post Radar Core R0 authority `c805eb9`) |
| Branch | `sol/radar-track-lock-r0` |
| Unity | `6000.3.16f1` |

Closed and untouched, per the Radar Core R0 record: `MavTargetTrackOwner` and Radar Core itself. This
phase changed neither. Issue #16 remains **open** — the three legacy authorities still own their own
locks, and this phase is the step that makes retiring them possible, not the step that does it.

**Not implemented, deliberately:** missile launch logic, AIM-120, AIM-9, seeker logic, missile
guidance, missile autopilot, datalink fusion, ECM/ECCM, clutter, PRF, notch modelling, lead-sight
migration.

## 1. The one thing this phase settles

Before it, "locked" was three different claims from three components, each of which also did
something else. `MavF22SensorSuite` scans, times an STT lock, reads its own keys and writes HUD state
in one class; `MavTargetingPodSystem` toggles a lock on a keypress; `MavCASTargetingSystem`
designates. Nothing could answer "what is this aircraft locked onto" without asking all three and
picking.

`MavTrackLockController` is the answer. It reads tracks from `MavTargetTrackOwner` and decides
commitment. It does not detect, does not scan, does not touch a sensor and does not read input — a
command source or an AI asks for a selection, and it answers with a lifecycle.

The split matters more than the class. **A sensor answers "what can I see"; a lock answers "what am I
committed to."** The second involves time, hysteresis, and a decision that deliberately survives a
missed observation. Folding the second into the first is exactly what the legacy stack did.

## 2. Vocabulary

`MavLockState`, ordered so that "more committed" compares greater and precedence needs no lookup
table:

| State | Meaning |
|---|---|
| `Idle` | nothing selected, nothing locked |
| `Selected` | a track is selected. **Selection is intent, not a lock** |
| `Acquiring` | a selected track is being held long enough to establish a lock |
| `Locked` | committed to one track, and that track is still being refreshed |
| `Coasting` | the locked track has gone stale; the commitment is held through the gap |

`MavLockLossReason` records why a lock ended rather than leaving a consumer to infer it:
`TrackDropped`, `CoastExpired`, `QualityTooLow`, `SelectionChanged`, `Commanded`,
`AuthorityUnavailable`.

`IMavTrackLockAuthority` is what future launchers depend on, never the concrete controller, so the
lock can be replaced without touching them. `TryGetLockedTrack` returns true while `Coasting` as well
as `Locked`: the commitment has not been abandoned, and a consumer treating a coast as "no lock"
would drop an engagement on the first missed observation.

## 3. What `MavTrackQuality.Locked` now means

Concretely: **the track this aircraft's lock authority is committed to.** It is asserted in exactly
one place, `MavTrackLockController.EffectiveQualityOf`, and never by a producer.

Two consequences worth being explicit about:

- A sensor reporting `Locked` would be claiming an engagement decision it plays no part in. Radar
  Core asserts the opposite of itself in its own validation, and that stays true.
- The owner's stored quality is **not rewritten**. `EffectiveQualityOf` layers the lock over the
  measurement, so a consumer wanting the raw sensor answer still gets it from the owner. Overwriting
  it would have destroyed information to save a lookup.

## 4. Lock lifecycle, and why each number exists

All values are **synthetic gameplay tuning**, not sourced performance data.

| Parameter | Default | Why |
|---|---|---|
| `acquisitionSeconds` | `1.15` | the legacy `MavF22SensorSuite.sttLockTime`, matched deliberately |
| `minimumLockQuality` | `Tracked` | a lock needs position *and* velocity from a repeated observation |
| `staleTrackSeconds` | `0.75` | above this a track is stale for lock purposes |
| `coastSeconds` | `2.0` | how long a commitment is held through a gap before it is lost |
| `minimumSelectableQuality` | `Coarse` | anything worth looking at can be selected; locking is stricter |

Acquisition is **all-or-nothing**: an interrupted attempt restarts rather than resuming, so a target
flickering in and out cannot accumulate a lock. Losing a lock demotes to `Selected`, keeping the
selection — losing a lock is not the same as losing interest.

One consequence follows from `minimumLockQuality = Tracked` and is easy to mistake for a bug: the
legacy scene-sweep feed reports nothing better than `Coarse`, so **a lock cannot be established on a
legacy track at all**. Only a radar observation good enough to be called `Tracked` can be locked.
That is the intended statement — the authoritative lock requires a real sensor track — and it is
asserted, not left to be discovered.

## 5. Lock is separate from detection

The radar knows nothing about the lock, and the lock knows nothing about the radar. The controller
reads `MavTargetTrackOwner` only. Removing `MavTrackLockController` leaves radar detection behaving
exactly as it did, and disabling the radar leaves the lock authority reporting `Idle` rather than
failing. Disabling the *lock authority* withdraws its claim entirely rather than freezing it — see
§8.2.

The controller is wired in `MavCASStarterBootstrap` after the radar and the owner, and owns no other
responsibility.

## 6. Representing the legacy authorities — the step before moving authority

The migration order is: legacy lock state → project into track vocabulary → **validate equivalence**
→ introduce one lock authority → migrate consumers → only then retire legacy authority.

`MavLegacyLockProjection` is the third step made checkable. It is pure — every method takes
primitives, not components — so every legacy state can be asserted without building a scene and
without instantiating components that read input and search the scene in their own `Update`. A
mapping that can only be tested by running the thing it describes is not much of a check.

`MavLegacyEngagementProbe` feeds it and publishes the result, so each authority's lifecycle state is
visible at runtime beside the track id it claims. The probe remains **strictly an observer**: it
never designates, locks, clears, or writes to a legacy component.

### 6.1 The semantics, read from the source

**`MavF22SensorSuite` — STT.** The only legacy authority with an acquisition phase. `lockTimer`
accumulates while the mode is STT or TWS and decays at 1.5× otherwise; `debugHasLock` is
`lockTimer / sttLockTime >= 1`; losing the selected target from `contacts` zeroes both immediately.
`ClearLock` (Backspace) is a commanded break.

| Legacy state | Projects to |
|---|---|
| no selection, or selection not among contacts | `Idle` |
| selection, no progress | `Selected` |
| selection, partial progress | `Acquiring` |
| `debugHasLock` | `Locked` |

**`MavTargetingPodSystem` — pod lock.** Instantaneous toggle (`R`), no acquisition time. Two kinds:
a lock onto a `MavCASTarget`, and a lock onto a bare ground point. When a locked target dies,
`UpdateLock` clears `lockedToTarget` but leaves `isLocked` set — a target lock **degrades into a
point lock** rather than ending. Projects to `Locked` only for a live target lock; everything else
`Idle`.

**`MavCASTargetingSystem` — designation.** Instantaneous, no timer, never expires; the field is not
even cleared when the target dies, callers notice by checking `IsAlive()` at read time. Projects to
`Locked` for a live designated target, `Idle` otherwise.

### 6.2 Where the new authority deliberately diverges

These are decisions on the record, each asserted, not drift:

1. **Coast.** The new authority holds a lock through a 2 s gap. **Not one** of the three legacy
   authorities does: the suite drops on the first sweep without the target, and the pod and the
   designation never age at all. `Coasting` is new behavior, and the divergence is in the safe
   direction.
2. **Acquisition decay.** Legacy STT decays its timer at 1.5×/s when out of a hard-lock mode; the new
   authority resets to zero on interruption. Stricter, and legacy's decay is driven by sensor *mode*,
   which the new authority has no concept of — there is nothing to decay against.
3. **Instant locks.** The pod and the designation lock instantly. The new authority always requires
   `acquisitionSeconds`. Modelling one lock lifecycle instead of three is the point; the instant path
   is part of why "locked" meant so little.
4. **Point locks and point designations are not tracks.** The owner has no track for a patch of
   ground, so there is no track id to express them in. Inventing one would give the projection the
   power to create tracks, which belongs to the owner alone. Real behavior the track vocabulary
   deliberately does not cover — stated rather than quietly papered over.
5. **A quality floor.** The new authority requires `Tracked`. No legacy authority has a quality floor
   at all.

Equivalence that **is** preserved: `acquisitionSeconds` defaults to `1.15`, the legacy `sttLockTime`.
A source-level assertion guards against a silent retune on the legacy side, because if that constant
moves the equivalence claim stops being true.

## 7. Moving authority, and the one switch that decides it

`MavEngagementView.PrimaryTrackId` **still answers the legacy order by default**: sensor STT lock,
pod lock, CAS designation, exactly as before this phase. The authoritative lock outranks all three
only once `preferAuthoritativeLock` is turned on.

`preferAuthoritativeLock` is the migration switch, in one place, so moving authority is a decision
rather than a side effect of a class existing. **It defaults to `false` — legacy authority.**

The reason is the migration order itself:

> legacy authority → project and observe → validate equivalence → introduce the new authority →
> **migrate consumers** → switch the default → retire the legacy owners

Consumers have not migrated. Defaulting to the new path would move authority ahead of the step that
proves the move is safe. That no gameplay system reads `PrimaryTrackId` today is not a reason to get
the order wrong — it only means the mistake would be invisible until the first consumer inherited a
default nobody had validated against it.

So this phase leaves the authoritative lock **computed, published and visible, but not preferred**.
Turning the switch on is the deliberate opt-in, it is the entire change when that happens, and it is
reversible at one field rather than by reverting code. Both positions are asserted, and the default is
asserted separately from the precedence order so a precedence case can never quietly become the thing
that documents the default.

The legacy fallback is kept, and the legacy components are **not deleted**. Removing them now would
change behavior in every case the authoritative lock has not taken over.
`legacyDisagreesWithAuthoritative` is the signal that says when that is no longer so: while it stays
false, the legacy authorities are agreeing with the new one and can be retired with evidence.

> As shipped in this phase that signal was computed only while `preferAuthoritativeLock` was on, which made
> the criterion above satisfiable by an uncomputed field. Corrected in Lock Authority Migration Prep R0 —
> see `LOCK_AUTHORITY_MIGRATION_PREP_R0.md` §1. The criterion stands; it is now non-vacuous.

`PublishLegacyLockStates` was added rather than widening `PublishSensorLock` and friends, because
those have consumers and assertions already and TargetTrack Core R0 is closed and passing.

## 8. A defect found by running the tests

**A selection that could never reach a lock starved one that could.**

Automatic selection ran only while nothing was selected. The legacy feed is always live and reports
nothing better than `Coarse`, while a lock needs `Tracked` — so a legacy track selected at startup
latched **forever**, and the controller sat in `Selected`, never acquiring, even once a radar track
good enough to lock existed. Nothing failed and nothing was logged; the selection simply could not
progress. A first read of the state machine does not show it, because every individual transition is
correct.

Confirmed before being fixed: with the assertions added and the controller untouched, exactly
`L-047c`, `L-047d` and `L-047e` failed and the other 97 passed.

The fix, `ReconsiderStalledSelection`, is deliberately narrow. It runs **only** in `Selected`, **only**
when the current selection is below the lock threshold, **only** when the selection was automatic, and
it moves only to a track that is lock-capable right now. So it can never interrupt acquisition, a lock,
or a coast — a rule that could re-pick mid-acquisition would make acquisition timing unpredictable,
which is the opposite of what this phase is for. `L-048b`, `L-048c`, `L-048d` and `L-097c` pin that
narrowness.

The "only when the selection was automatic" clause was **not** in the first version of this fix, and
§8.1 is about why that mattered.

## 8.1 Selection ownership — a defect found in review

The first version of the fix above carried a comment claiming it "never overrides an explicit
request", reasoning that a commanded selection sits in `Selected` only until its track becomes
lockable. **That reasoning is false** whenever the commanded track simply stays `Coarse`. Worse,
nothing in the controller recorded *which kind* of selection was being held: `RequestSelection` and
the automatic rule both landed in `Selected` with no distinguishing state, so reconsideration could
not have honoured the distinction even if it had tried.

The consequence: an explicit player, AI or FAM selection of a `Coarse` track was **silently replaced**
by the automatic selector on the very next step. Not gameplay-visible yet only because nothing
consumes the controller — it would have become visible the moment a command path was migrated.

### The rule, as it now actually is

The controller records selection origin in `selectionIsExplicit`, exposed as `SelectionIsExplicit`,
because the difference is not recoverable from the state machine afterwards:

| Origin | Rights |
|---|---|
| **Automatic** (`SelectBestTrack`) | may be replaced by reconsideration when it cannot reach a lock and another track can |
| **Commanded** (`RequestSelection`) | **may not** be replaced by reconsideration, at any quality |

A commanded selection is held until one of: **its quality improves**, **its track is dropped**,
**another selection is commanded**, a **commanded clear or break**, or the **authority is disabled**.
Waiting on a low-quality track is a legitimate thing to be told to do — the whole point of a command
is that the machine does not know better.

Every write to `selectedTrackId` now decides the origin beside it, so the set of places that can
change ownership is auditable by reading five lines rather than by reasoning about the state machine.

### A second instance in the same defect class

`RequestSelection` returned early when the requested track was already selected — correctly, since a
command source repeating itself must not reset acquisition progress — but that early return **skipped
marking the selection commanded**. So commanding the track the automatic rule happened to have already
picked left it flagged automatic, and therefore still replaceable. It now claims ownership without
touching progress, which keeps `L-042` true.

This one was found by the new tests rather than by reading: `L-095` failed, and the diagnosis had to
establish which side was wrong before anything changed.

### Confirmed, not assumed

The ownership guard was temporarily disabled and the suite re-run: **7 assertions failed**, all in the
commanded path — `L-091b`, `L-091c`, `L-092`, `L-092b`, `L-093`, `L-093b`, `L-095`.

`L-091` — the immediate return of `RequestSelection` — **passed with the guard disabled**. That is the
point: the override happened on the *following* step, so a case that checks only the immediate result
of `RequestSelection` cannot detect this class of defect at all. Every case in the ownership section
steps the controller after the request.

One of the two remaining failures turned out to be a **bad test, not bad code**: `L-094` broke a lock
from `Acquiring` and then asserted `LastLossReason == Commanded`, but `BreakLock` records a reason only
when a lock was actually held — which is right, there is nothing to lose from `Acquiring`. The case now
lets acquisition finish first, and `L-093c` pins the lock it then breaks.

## 8.2 The authority boundary must fail closed — a defect found in review

The third review defect, and the only one that failed **permanently** rather than for a step or a
frame.

`IsLockAuthorityActive` existed and was correct, but no lock-facing accessor consulted it. `LockState`,
`LockedTrackId`, `TimeInLockSeconds`, `TryGetLockedTrack` and `EffectiveQualityOf` all read the stored
state directly, and there was no `OnDisable`.

The persistent case: establish a lock, set `controller.enabled = false`. Unity stops calling `Update`,
so the inactive path inside `Step` **never runs again** — and both the internal state and the claim last
published to `MavEngagementView` stay `Locked` forever. A consumer would see a lock held by an authority
that reported itself unavailable and could never change its mind. That is not a stale read, it is a
permanent contradiction.

### The rule

When `IsLockAuthorityActive` is false, every lock-facing reading fails closed: `LockState` is `Idle`,
`LockedTrackId` and `TimeInLockSeconds` and `AcquisitionProgress01` are zero, `SelectedTrackId` is zero,
`SelectionIsExplicit` is false, `TryGetLockedTrack` returns false, and `EffectiveQualityOf` promotes
nothing to `Locked`.

`EffectiveQualityOf` returns the **producer's measurement** rather than `None` while unavailable. There
is no engagement to layer over the measurement, but the sensor's reading is still real, and discarding
it would destroy information to make a point.

A private `HasCommittedLock` is what the accessors consult, so there is one definition of "holding
something" rather than five copies of the same two-state test.

### One withdrawal path

Every way a claim can end now routes through `WithdrawAuthority(reason)`: a commanded break, the
selected track disappearing, the authority becoming unavailable, and component disable. Three of those
previously reset the same six fields in their own code, and the authority-unavailable case did not reset
them at all. A loss is *recorded* only when a lock was actually held — there is nothing to lose from
`Acquiring`, and recording one would make `LastLossReason` describe something that never happened.

`OnDisable` withdraws and republishes, because it is the only hook that fires when `Update` will not run
again. The enable switch became a property, `EnableLockAuthority`, so turning it off withdraws at the
moment of the decision rather than at some later frame; an Inspector edit writes the serialized field
and is caught by the next `Step`.

`PublishDebug` now reads through the **public accessors**, so what is published and what the Inspector
shows are exactly what a consumer would observe, and an unavailable authority publishes "nothing held"
without needing a second rule.

### Confirmed, and one thing the harness cannot confirm

With the gating, `OnDisable` and the eager withdrawal all removed, **18 assertions failed** — 14 in the
authority section including the permanent case, plus 4 in the selection-ownership section.

**Edit-mode Unity raises neither `OnEnable` nor `OnDisable`.** Measured, not assumed: a throwaway probe
in this project and Unity version added a component to a live GameObject, disabled it and re-enabled it,
and recorded **0 `OnEnable` and 0 `OnDisable` calls**. So the validation harness cannot make Unity raise
the callback. The cases therefore split in two:

- **Verified directly:** the accessor contract. `ctl.enabled = false` with *no callback of any kind* and
  the boundary already holds (`L-105`, `L-105b`, and `L-103b` for the wiring-loss variant). This is the
  part that protects a consumer even if a callback never arrives.
- **Verified by invoking the real method body** via reflection, since Unity will not raise it here
  (`L-105c`–`L-106d`). What this does *not* verify is Unity's own guarantee to call `OnDisable` on
  disable — documented engine behavior that play mode provides. A play-mode probe would close that last
  step and is a reasonable follow-up.

### A bug of mine that the existing tests caught

The first version of this fix added an `authorityActiveLastStep` guard, meant to stop anything resuming
across an availability gap. It defaulted to `false`, and `RequestSelection` sets the state to `Selected`
without a `Step` — so a fresh controller's very first `Step` read "returning from a gap" and **withdrew
the commanded selection immediately**, breaking four §8.1 ownership assertions.

On examination the guard could never fire for a legitimate reason: every other write of a non-`Idle`
state happens inside `Step` after the availability check, and `RequestSelection` refuses while inactive.
It was dead code that could only misfire, so it was **removed** rather than patched. Speculative
belt-and-braces that cannot help but can hurt is worse than nothing.

### Residual, stated rather than hidden

If `owner` is set to null and restored with **zero** `Step` calls in between, nothing observes the gap
and the internal state survives. `owner` is bootstrap wiring rather than a runtime control, so this is
outside the contract — but it is a real property of the design and worth knowing. `L-103e` covers the
realistic form, where a step observes the loss.

## 9. A behavior kept because legacy has it

A commanded break with automatic selection on returns the selection on the next step — but from zero
acquisition progress. That is the same shape as the legacy suite, where `ClearLock` nulls the
selection and the next contact sweep re-selects `contacts[0]` with the STT timer reset: a commanded
break costs a full re-acquisition rather than nothing. With `autoSelectBestTrack` off, a commanded
break stays broken. Both are asserted (`L-049c`, `L-049d`) so the behavior is deliberate rather than
incidental.

## 10. Validation

`MavTrackLockValidation`, **147 assertions, 0 failures.** Every case drives the controller with an
explicit clock and explicit step deltas rather than waiting on frames, because lock semantics are
entirely about elapsed time and editor time does not advance between calls. Nothing depends on a
scene, on physics, or on a real sensor.

| Section | Ids | Covers |
|---|---|---|
| Vocabulary | `L-001`–`L-003` | ordering, defaults, `Locked` as the strongest quality |
| Acquisition | `L-010`–`L-018` | timing, all-or-nothing restart, quality and staleness gates |
| Maintenance and coast | `L-020`–`L-024` | coast entry, resumption without re-acquiring, expiry |
| Loss reasons | `L-030`–`L-036` | every reason reached by its own cause |
| Selection | `L-040`–`L-046` | automatic rule, explicit override, no-op re-request, clearing |
| Stalled selection | `L-047`–`L-049d` | the §8 defect, the narrowness of its fix, commanded break |
| Selection ownership | `L-090`–`L-097c` | the §8.1 defect: automatic may yield, commanded may not |
| Authority withdrawal | `L-100`–`L-106d` | the §8.2 defect: an unavailable authority holds nothing |
| Effective quality | `L-050`–`L-055` | `Locked` asserted here and nowhere else; owner not rewritten |
| Engagement precedence | `L-060`–`L-064` | authoritative wins, legacy order preserved beneath it |
| Migration switch | `L-065`–`L-066d` | the legacy default, the opt-in, reversibility, nothing erased |
| Legacy equivalence | `L-070`–`L-085b` | the three projections, their reachable shapes, the divergences |

**Determinism** was checked rather than assumed: two independent Unity launches produced
byte-identical assertion reports, all 148 report lines.

### Gates

| Gate | Result |
|---|---|
| Track lock | **147 / 0 PASS** |
| Radar Core | **71 / 0 PASS** (unchanged) |
| TargetTrack Core | **35 / 0 PASS** (unchanged) |
| Combat boundary scan | **PASS**, 3 / 0 |
| FDM validation baseline | **1585 / 0 PASS** |
| Flight behavior | **bit-identical** |
| Missing Scripts | **PASS** |

Boundary rule **C-5 was not relaxed** in this phase. It forbids seeker, guidance and missile-profile
declarations, none of which a lock authority needs; if one had appeared, the phase boundary really
would have been crossed.

## 11. Known limitations

- Every lifecycle value is synthetic tuning. No sourced radar lock performance data is used.
- The automatic selection rule is highest quality, nearest as the tie-break. Deliberately plain and
  documented rather than clever — threat evaluation is a later concern, and a rule nobody can predict
  is worse than a simple one. It applies only to selections it made itself; see §8.1.
- A commanded selection of a track that never reaches `Tracked` will sit in `Selected` forever without
  locking. That is the intended reading of a command, not an oversight, and `L-091c` pins it.
- There is no multi-target track-while-scan lock. One authority holds one lock.
- The three legacy authorities still run and still own their own state. They are represented, not
  retired.
- Nothing consumes the lock yet, and the default therefore stays on legacy authority. That is the
  next phase's job, and it is why the migration switch exists.

## 12. Next

Migrate consumers onto `IMavTrackLockAuthority` — the HUD first, since it reads the engagement view
already. Only once they read the authoritative answer does `preferAuthoritativeLock` flip to `true`,
and only after that do the legacy authorities retire, one at a time, each with
`legacyDisagreesWithAuthoritative` staying false as the evidence. Three separate steps, in that order,
and none of them is this phase.
