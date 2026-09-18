# Radar Core R0

The first real sensor: it produces `MavTrackObservation` for what it can actually see, through the
existing feed contract, with `MavTargetTrackOwner` unchanged.

| | |
|---|---|
| Base | `49626857b1e155e6e150d295de5285a04248fae5` (post TargetTrack Core R0) |
| Branch | `sol/radar-core-r0` |
| Unity | `6000.3.16f1` |

**Not implemented, deliberately:** AIM-120, AIM-9, missile guidance, seeker logic, datalink fusion,
ECM/ECCM, clutter, PRF, notching, scan patterns, beam dwell, lock semantics, lock-authority
migration, lead-sight migration.

## 1. Architecture

```
MavRadarSensor            detects, on its own cadence
        │  MavTrackObservation
        ▼
IMavTargetObservationFeed  unchanged contract
        ▼
MavTargetTrackOwner        unchanged implementation
        ▼
TargetTrack consumers      unchanged
```

`MavTargetTrackOwner` was **not touched**. That was the test of the previous phase's design: a real
sensor arriving should be a registration, not a redesign. It was.

```
Combat/
├── Core/          contracts                              [unchanged]
├── Targeting/     MavTargetTrackOwner, MavEngagementView [unchanged]
├── Sensors/
│   ├── MavRadarScanVolume.cs  [NEW]  envelope + geometry, free of Unity components
│   └── MavRadarSensor.cs      [NEW]  the feed
├── Legacy/        legacy feed + engagement probe          [unchanged, still registered]
└── Editor/
    ├── MavCombatBoundaryScan.cs     C-5 relaxed for radar, in its own commit
    ├── MavTargetTrackValidation.cs  [unchanged, still 35/0]
    └── MavRadarCoreValidation.cs    [NEW]  39 assertions
```

## 2. What makes it a sensor rather than a scene query

The legacy feed reports every marker in the scene: ground truth with nothing between it and the
consumer. This one reports only what survives

- a **range envelope** — minimum and maximum, the maximum scaled by target size
- a **scan volume** — separate azimuth and elevation half-angles
- optional **line-of-sight gating** against a configurable blocker mask
- its own **scan cadence**, independent of how often the owner asks

so a target behind the aircraft, too close, too far, or behind terrain is **genuinely not known**.
That is the first time anything in Maverick can represent that.

Azimuth and elevation are separate limits rather than one cone angle, because a real scan volume is
wider than it is tall and a single cone cannot express that. Rejections are counted **per reason**
(`debugRejectedRange`, `debugRejectedAngle`, `debugRejectedLineOfSight`, `debugRejectedOwnship`), so
the sensor can say *why* it saw nothing — which one "not detected" boolean cannot.

## 3. Synthetic parameters, and the one physical shape

**Every number is a gameplay tuning value.** No real radar's performance is encoded; nothing
classified or unsupported is used. Defaults: 150 m minimum range, 20 km base maximum, ±60° azimuth,
±30° elevation, 4 Hz scan, 1 Hz candidate discovery.

The single physically-shaped relation is that effective range scales with the **fourth root** of
target cross-section, so sixteen times the size doubles the range. That shape is the plainest textbook
radar-range relation — public first-principles physics — applied to a *synthetic* base range. The
product is therefore a gameplay number that behaves sensibly, not a specification. Target size comes
from the existing `MavRadarSignature.radarCrossSectionSqm`, which that component's own documentation
already describes as an abstract tuning value rather than measured RCS.

This follows the discipline the flight-dynamics work established: a number is either sourced, or it is
labelled as tuning. It is never presented as fact because it looks plausible.

## 4. Radar-local source keys

The sensor assigns keys from **its own counter**, starting at 1, remembered per object — deliberately
not instance ids.

The owner correlates on `(feed identity, sourceKey)`, so a local counter is sufficient. Using one also
demonstrates the property: this sensor's key `1` and the legacy feed's key `1` are different objects,
and the owner keeps them as separate tracks with distinct provenance. An instance id would have hidden
that by being globally unique for the wrong reason.

## 5. Quality, and the line it will not cross

The sensor reports `Coarse` without a velocity and `Tracked` above a quality threshold with one. It
**never reports `Locked`**.

A lock is a maintained commitment that means something to a weapon. Lock semantics are a later phase,
and reporting `Locked` here would let a future launcher believe something no system has actually
established. `R-025` asserts it.

## 6. The legacy feed stays

`MavLegacyTargetObservationFeed` remains registered beside the radar and was not modified. It is the
architectural coverage the radar has to match before anything is removed — it sees ground targets and
everything outside the radar's envelope — and keeping both registered is also what proves two feeds
with identical local keys stay separate tracks.

Deleting it because Radar Core exists would be deleting the comparison that shows Radar Core works.

## 7. Wiring and the disabled path

Installed by `MavCASStarterBootstrap.InstallTargetTrackCore`, the existing composition root, which now
also registers the radar. `enableRadar` defaults **on**: the sensor is this phase's deliverable, and
nothing consumes tracks for gameplay yet, so running it changes no behavior a player can see.

`enableRadar = false` makes `IsFeedActive` false, so the owner stops consulting it, and it performs no
scans and no candidate sweeps from that moment. Verified in play mode: after disabling, zero further
scans, zero further candidate sweeps, zero tracks, legacy feed still registered, flight behavior
bit-identical.

## 8. Boundary rule C-5

Relaxed in **commit `6d7757b`, which does nothing else**. The two radar entries were removed; seekers,
guidance laws, proportional navigation, missile autopilots and the AIM-120/AIM-9/AMRAAM profiles
remain forbidden.

C-5 is a phase tripwire, not a permanent prohibition: each phase that legitimately begins removes
exactly its own entries and says why. Keeping the rest means the rule still means something — if a
seeker type appears while the list still forbids it, the boundary really has been crossed.

## 9. Validation

| Check | Result |
|---|---|
| Compile | **PASS**, 0 `error CS` |
| Radar Core validation | **PASS 39/0** |
| TargetTrack validation (unchanged) | **PASS 35/0** |
| Combat boundary scan, after the C-5 update | **PASS 3/0** |
| Full FDM baseline | **PASS — 1585 / 0 across 25 counted suite results** |
| Flight behavior, radar enabled | **bit-identical** to base |
| Flight behavior, radar disabled | **bit-identical** to base |
| No ownship radar track | **PASS** — ownship rejected, 0 contacts, 0 tracks |
| Radar + legacy feed, identical local keys | **PASS** — separate tracks, distinct provenance |
| Disabling radar leaves legacy unchanged | **PASS** — 0 scans and 0 sweeps after disable, legacy still registered |
| No Missing Scripts | **PASS** |

The 39 assertions split three ways:

- **Geometry (R-001…R-006c)** against `MavRadarScanVolume.Measure` directly — positions and a rotation
  in, measurement out, so no scene, physics or running game is involved. Boresight, left/right
  symmetry of unsigned azimuth, signed elevation, a target astern never reading as boresight,
  measurement in the sensor frame under rotation, closure sign both ways, and closure **not** being
  invented when no velocity was supplied.
- **Envelope (R-010…R-018)** — minimum range, effective maximum, azimuth limit, symmetric elevation
  limit, azimuth and elevation as *independent* limits rather than one cone, fourth-root size scaling
  including the exact sixteen-times-doubles check, quality falloff with range and with angle staying
  inside [0,1], and a nonsense envelope being clamped instead of producing negative geometry.
- **Sensor and integration (R-020…R-032)** — envelope filtering against synthetic markers, ownship
  rejection, per-reason rejection counters, `Radar` provenance, never-`Locked`, local key stability
  across scans, keys being a small counter rather than an instance id, the disabled path producing
  nothing, throttled candidate discovery, two feeds with identical local keys producing separate
  tracks with distinct provenance, and the legacy track surviving radar unregistration.

## 10. Known limitations

1. **Candidate discovery is still a scene query.** `FindObjectsOfType<MavRadarSignature>` finds
   candidates, throttled to 1 Hz. The difference from the legacy feed is what happens next — these are
   candidates subject to envelope, range and line-of-sight tests, not contacts. When a registry of
   radar-visible objects exists, that one method changes and nothing else does.
2. **Line-of-sight is off unless a blocker mask is set.** A zero mask means "nothing blocks", treated
   as unobstructed rather than as everything-blocks, so an unconfigured mask cannot silently blind the
   sensor. The project has no dedicated terrain layer convention yet.
3. **No target-size model beyond one scalar.** Aspect-dependent cross-section, aspect ratio and
   altitude effects are absent.
4. **No detection probability or dropout.** A contact inside the envelope is always detected. Real
   radars miss; modelling that needs a signal model this phase does not have.
5. **Ground targets are not radar-visible.** Only `MavRadarSignature` markers are candidates;
   `MavCASTarget` reaches the owner through the legacy feed. Air-to-ground radar modes are a later
   decision.
6. **No prediction.** Tracks still carry the position observed at the last scan, so the lead sight
   remains unmigrated for the same reason as before.
7. **`MavF22SensorSuite` still runs** and still owns its own lock. Radar Core does not replace it, and
   issue #16 stays open.

## 11. Next

Radar track/lock: what a lock means, acquisition and loss, and absorbing the three legacy lock
authorities behind the track owner (issue #16). That phase gives `MavTrackQuality.Locked` its
definition and is the first consumer of `MavEngagementView.PrimaryTrackId`.
