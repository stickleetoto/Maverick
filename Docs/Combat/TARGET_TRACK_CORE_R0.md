# TargetTrack Core R0

One authoritative track owner, a legacy adapter feeding it, and the first read-only consumer moved
onto it. No radar detection, no guidance, no missile, no real-world weapon parameters.

| | |
|---|---|
| Base | `0ba3f3824dcdb8a4c7f29dadcf984c4f35586ab6` (post Weapon Separation R0) |
| Branch | `sol/target-track-core-r0` |
| Unity | `6000.3.16f1` |

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

## 3. What the owner does, and refuses to do

Does: correlates observations on `(source, sourceKey)`; assigns stable track ids; carries a reported
velocity through unchanged and otherwise differences position between samples; ages tracks from
observation time; drops tracks unseen for longer than `trackDropSeconds`; resolves a producer's key
back to a track id.

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

## 7. Validation

| Check | Result |
|---|---|
| Compile | PASS, 0 `error CS` |
| TargetTrack Core validation | **PASS 23/0** |
| Combat boundary scan C-1..C-6 | **PASS 3/0** |
| FDM baseline, full, clean committed checkout | **PASS — 1585 / 0 across 25 counted suite results** |
| Aircraft spawns | PASS |
| Flight behavior vs pre-branch | **bit-identical** |
| Missing Scripts | PASS |
| No radar detection / guidance / AIM-120 / AIM-9 | PASS, enforced by C-5 |
| FDM independence | PASS, enforced by C-1 |

The 23 assertions cover identity, correlation, a second key producing a second track, reported
velocity carried unchanged, quality capping without velocity, aging from observation time, dropping,
key-to-id resolution, unknown-key rejection, zero-key rejection, inactive feeds, the invalid default,
and all four engagement-view precedence cases. Every one runs against a scripted feed, so none of it
depends on scene contents and all of it survives the legacy feed being deleted.

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

## 8. Known limitations

1. **Tracks are not predicted.** A consumer reading a track between sweeps gets a position up to one
   sample interval old. That is why the lead sight was not migrated. Prediction belongs with Radar
   Core, where the sample rate becomes a sensor property.
2. **No correlation between feeds.** Two feeds observing the same object produce two tracks. Fusion
   is a later phase and pretending otherwise here would be a guess.
3. **`sourceKey` is an instance id in the legacy feed.** Adequate because the marker objects live as
   long as the target does; a real sensor must assign its own keys, which the contract already
   allows and documents.
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
