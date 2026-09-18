# TargetTrack Core R0

One authoritative track owner, a legacy adapter feeding it, and the first read-only consumer moved
onto it. No radar detection, no guidance, no missile, no real-world weapon parameters.

| | |
|---|---|
| Base | `0ba3f3824dcdb8a4c7f29dadcf984c4f35586ab6` (post Weapon Separation R0) |
| Branch | `sol/target-track-core-r0` |
| Branch HEAD | `a6fe576` |
| **Merged** | **2026-09-18, PR #17, merge commit `735ce0665dbc1377c1595eca962bdf88a62bab9a`** |
| **TargetTrack Core R0 authority** | **`735ce06`** — the base for Radar Core R0 and everything after |
| Unity | `6000.3.16f1` |

`735ce06` is the TargetTrack Core R0 authority. The implementation in it is **complete and closed**:
Radar Core and later phases build on the feed contract rather than editing the owner. Issue #16
remains **open** — lock authority has not moved, and this phase deliberately did not move it.

## 1. What exists now

```
Combat/
├── Core/
│   ├── MavDamageContract.cs            [R0 separation]
│   ├── MavCombatCommandContract.cs     [R0 separation]
│   ├── MavTargetTrackContract.cs       [R0 separation]  MavTargetTrackData, IMavTargetTrackSource
│   └── MavTrackObservationContract.cs  [NEW]  MavTrackObservation, IMavTargetObservationFeed
├── Targeting/
│   ├── MavTargetTrackOwner.cs          [NEW]  the single authority
│   └── MavEngagementView.cs            [NEW]  the three authorities, in one vocabulary
├── Legacy/
│   ├── MavLegacyTargetObservationFeed.cs  [NEW]  the only scene sweep left in Combat/
│   └── MavLegacyEngagementProbe.cs        [NEW]  read-only observer of the legacy locks
└── Editor/
    ├── MavCombatBoundaryScan.cs        [R0 separation]  C-1..C-6
    └── MavTargetTrackValidation.cs     [NEW]  23 assertions
```

## 2. Observation vs track

The split that makes a real sensor droppable later:

- **`MavTrackObservation`** — what one producer saw, this sample. No stable identity, no history.
- **`MavTargetTrackData`** — what the owner maintains over time: stable id, position, velocity,
  quality, and the time it was last actually observed.

A radar will emit observations only for what it can see, at the quality it can support. The owner
does not change when that happens — which is the entire reason the feed is an interface and the
legacy adapter is one implementation of it rather than the design.

## 3. Who owns identity

The single most important contract in this phase, and the one the first implementation got wrong:

| Thing | Owner | Notes |
|---|---|---|
| `sourceKey` | the **feed** | Local. Unique only within that feed. Two feeds may reuse the same value. |
| `trackId` | the **track owner** | Global and stable. No producer ever assigns one. |
| `MavTrackSource` | the feed declares it, the owner stamps it | Provenance/**category** — `Legacy`, `Radar`, `InfraRed`, `Datalink`. **Never an identity.** |
| feed identity | the **track owner**, on registration | Assigned, so it cannot be spoofed or duplicated by a producer. |
| correlation key | the track owner | `(feed identity, sourceKey)`. |

Correlating on `(source category, sourceKey)` — which the first version did — merges two different
objects the moment two feeds of the same category pick the same local key. Two radars are both
`MavTrackSource.Radar`, so the category cannot separate them. Feed identity can, and it is assigned
by the owner rather than claimed by the producer.

The owner also does not trust `MavTrackObservation.source`. It already knows which registered feed it
just called, so provenance is stamped from that feed; a producer declaring something different is
counted in `debugProvenanceMismatches` and overridden. Feeds are therefore collected **one at a
time** rather than into one shared list, because a single pooled list throws away which feed produced
which observation.

## 3.1 What the owner does, and refuses to do

Does: correlates observations on `(feed identity, sourceKey)`; assigns stable track ids; stamps
provenance from the registered feed; carries a reported velocity through unchanged and otherwise
differences position between samples; ages tracks from observation time; drops tracks unseen for
longer than `trackDropSeconds`; resolves a feed's local key back to a track id.

Refuses: detection, scan volumes, lock semantics, fusion between feeds, identification, seekers,
guidance. It also never searches the scene — feeds register themselves.

Two deliberate honesty rules:

- **Velocity is not invented.** A first sighting with no reported velocity does not get a zero
  velocity presented as fact; quality is capped to `Coarse` until a velocity actually exists.
- **The legacy feed reports `Coarse`/`Legacy`.** What it produces is ground truth from a scene sweep
  with no range, aspect or field of view behind it. Calling that `Tracked` would dress a scene query
  up as a measurement and would make the arrival of a real sensor invisible.

## 4. Lock-authority consolidation — begun, not completed

`MavEngagementView` expresses all three authorities in one vocabulary and reports when they disagree:

| Authority | Still owns its state | Projected as |
|---|---|---|
| `MavCASTargetingSystem` | yes | `designatedTrackId` |
| `MavF22SensorSuite` | yes | `sensorLockTrackId`, only when it reports an actual lock |
| `MavTargetingPodSystem` | yes | `podLockTrackId`, only when locked **to a target** rather than to a bare ground point |

`MavLegacyEngagementProbe` fills this by reading only. It never designates, locks or clears; remove
it and the three authorities behave exactly as before.

**Authority was deliberately not moved.** Taking the lock away from three live systems in the same
change that introduces tracks would alter gameplay and architecture together, leaving neither
trustworthy if something broke. The order is express, observe, then move. `PrimaryTrackId` exists
with a documented precedence — sensor lock, then pod, then designation — and **no consumer reads it
yet**; it is there so the question has one home when Radar Core gives locks real meaning.

An authority claiming something the owner has never observed projects as track id `0`. That is
information, not a bug, and the probe does not mint a track to paper over it: creating tracks belongs
to the owner alone.

## 5. Consumer migration

`MavFreshHud` is the first consumer reading from the owner. It gained a `TRK` line in the **F2
developer-debug panel** showing track count, observations per sweep, drops, the three projected ids
and a `DISAGREE` marker.

The existing `TGT` line is untouched, and the normal (non-developer) HUD is untouched, so nothing a
player sees changes. The HUD still resolves components by `FindObjectOfType` — that is O-8 and a
separate cleanup; what changed here is that its **target data** no longer comes from a legacy
targeting component.

`MavAirToAirLeadSight` was **not** migrated, deliberately. It sweeps `MavRadarSignature` itself and
picks a lead target, and its selection is gameplay-visible: moving it onto 0.25 s-sampled tracks
would lag the lead pip. It migrates once tracks carry a prediction good enough to replace a live
transform read.

## 6. Wiring

Installed by `MavCASStarterBootstrap.InstallTargetTrackCore`, the existing composition root, so one
place still decides what a Maverick aircraft is made of. Four components are added to the aircraft:
owner, view, legacy feed, legacy probe.

Installing by default is safe because the whole stack is observational: it reads markers and legacy
state, and writes nothing. Removing all four returns the aircraft to its previous behavior exactly.

Cost: one throttled scene rescan (default 1 Hz, cached between rescans) and one observation sweep
(default 4 Hz). Tracks age continuously; only sampling is throttled.

The rescan throttle is deliberately independent of what the previous scan found. "Found nothing" is a
cached scan result like any other — see §7.2.

## 7. Validation

| Check | Result |
|---|---|
| Compile | PASS, 0 `error CS` |
| TargetTrack Core validation | **PASS 35/0** |
| Combat boundary scan C-1..C-6 | **PASS 3/0** |
| FDM baseline, full, clean committed checkout | **PASS — 1585 / 0 across 25 counted suite results** |
| Aircraft spawns | PASS |
| Flight behavior vs pre-branch | **bit-identical** |
| Missing Scripts | PASS |
| No radar detection / guidance / AIM-120 / AIM-9 | PASS, enforced by C-5 |
| FDM independence | PASS, enforced by C-1 |

The 35 assertions cover identity, correlation, a second key producing a second track, reported
velocity carried unchanged, quality capping without velocity, aging from observation time, dropping,
key-to-id resolution, unknown-key rejection, zero-key rejection, inactive feeds, the invalid default,
all four engagement-view precedence cases, and — added after review — the feed-identity cases in §7.2
and the zero-target rescan throttle. Every one runs against a scripted feed, so none of it depends on
scene contents and all of it survives the legacy feed being deleted.

## 7.1 A defect the gate run found

The first play-mode gate run reported `feed.debugAirMarkers=1`, `owner.debugTrackCount=1` — in a
scene with no enemies. The one marker was the **ownship**: `MavInGameBootstrap` installs a
`MavRadarSignature` on the player aircraft so other sensors can see it, and the feed dutifully
observed it. The aircraft was tracking itself.

Fixed with `excludeOwnAircraft` (default on), which skips markers whose transform root is the feed's
own aircraft. That is platform self-exclusion, not a sensor filter: range, aspect and field of view
are still deliberately left to a real sensor, but "a platform does not observe itself" is basic
sanity, and leaving it out would force every later consumer to special-case the ownship.

After the fix, the same scene reports 1 air marker seen, 0 observations, 0 tracks. Flight behavior
remained bit-identical across the change.

## 7.2 Two defects found in review

### Correlation merged feeds of the same category — BLOCKER, fixed

Correlation was keyed on `(MavTrackSource, sourceKey)`. `MavTrackSource` is a **category**, not an
identity: two radars, or a radar and a datalink both reporting `Radar`, collide as soon as they pick
the same local key, and two different objects silently become one track. Nothing in the first
implementation could have detected that.

Fixed by making the owner assign each registered feed an identity and correlating on
`(feed identity, sourceKey)`. Provenance is now stamped from the registered feed instead of trusted
from the observation, feeds are collected one at a time so the owner always knows the producer, and
`TryResolveTrackId` takes the feed rather than a category. A feed that unregisters and re-registers
keeps its identity, so its tracks survive an enable cycle instead of being minted again.

Covered by T-016 … T-022: two feeds sharing a `FeedSource` get distinct identities; both emitting the
same `sourceKey` produce two distinct tracks; each key resolves to its own track and not the other's;
re-observation preserves each feed's own id; provenance is stamped from the feed; an observation
claiming the wrong source is counted and overridden; unregistering one feed leaves the other's
resolution intact, across a later sweep too; re-registration keeps the original identity.

Cross-feed **fusion is still not implemented** — two feeds observing the same physical object produce
two tracks, on purpose. Merging them needs a correlation model nobody has written, and guessing one
would be worse than leaving the duplication visible.

### Rescan throttle ignored with zero targets — fixed

`RescanIfDue` honoured `nextRescanTime` only when a cached marker array was non-empty, so a scene
with **zero** targets fell through the gate and re-swept the scene on every observation sample — at
the 4 Hz sweep rate rather than the 1 Hz rescan rate, in exactly the case where the sweep finds
nothing and buys nothing.

Fixed with a `hasScannedOnce` flag, so the time gate no longer depends on the result: "found nothing"
is a cached scan result like any other. `debugRescanCount` was added so the throttle is checkable
rather than assumed, and T-023/T-024 assert that repeated collects inside the interval do not
re-sweep with zero markers found.

## 8. Known limitations

1. **Tracks are not predicted.** A consumer reading a track between sweeps gets a position up to one
   sample interval old. That is why the lead sight was not migrated. Prediction belongs with Radar
   Core, where the sample rate becomes a sensor property.
2. **No correlation between feeds.** Two feeds observing the same object produce two tracks. Fusion
   is a later phase and pretending otherwise here would be a guess.
3. **`sourceKey` is an instance id in the legacy feed.** Adequate because the marker objects live as
   long as the target does, and it only has to be unique within that one feed. A real sensor assigns
   its own local keys, which the contract allows and documents.
4. **Ground and air remain separate concepts** at the marker level. The owner unifies them into one
   track set, which is already an improvement over three disconnected sweeps, but identification is
   not modelled.
5. **`PrimaryTrackId` is unconsumed.** Deliberate, and it stays that way until locks mean something.
6. **The three legacy authorities still own their locks.** Issue #16 is begun, not closed.

## 9. Next

Radar Core R0: a sensor that produces observations from a scan volume with range and aspect, rather
than from a scene sweep. It registers a feed and the legacy feed is deleted; nothing downstream
changes. `MavF22SensorSuite` is prior art to read, not a base to extend.

Relaxing boundary rule C-5 is that phase's first deliberate act, and should be its own commit with a
reason.
