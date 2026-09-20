# Engagement Target Contract R0

Base: `b1b47b0` — the Lock Authority Migration Prep R0 tip.

> **Note on base.** The instruction for this phase said "current `origin/main` after Lock Authority
> Migration Prep R0 is merged". That merge has not happened: Migration Prep was never pushed, and
> `origin/main` is still `1703a38`. This branch is therefore cut from the local prep tip `b1b47b0`, which is
> the intended ancestry. Nothing was pushed or merged to arrange it.

**Contract, inventory and validation only.** No Missile Core, no seeker, no guidance. No consumer migrated:
`MavCASWeaponSystem` and `MavAirToAirLeadSight` are untouched, `preferAuthoritativeLock` is still `false`,
and `IMavTrackLockAuthority` is byte-identical to its previous state.

## 1. The problem this vocabulary solves

The two previous phases both ended at the same wall. From `LOCK_AUTHORITY_MIGRATION_PREP_R0.md` §4:

> A pod point lock and a CAS point designation are locks on a **bare ground point**. `MavTargetTrackOwner`
> issues track ids for observed contacts; a point nobody is tracking has no id. Either the authority gains
> a point-designation concept, or designation stays legacy and only *track* locks migrate.

Four of the six remaining consumer migrations are blocked on that, and `MavCASWeaponSystem` releases
weapons on exactly the case the track vocabulary cannot express.

**Two shortcuts were available, and both merge concepts to avoid admitting there are two.**

1. *Mint a track for the designated point.* This puts an entry in `MavTargetTrackOwner` that no sensor
   observed, which every consumer of `MavTargetTrackData` must then treat as real — including quality, age
   and the coast logic, none of which mean anything for a coordinate. A track is what a sensor *believes*;
   a fabricated one is a belief nobody holds.
2. *Add a `worldPoint` to `IMavTrackLockAuthority`.* This makes every lock consumer handle a case that can
   never be locked, and reintroduces the exact conflation the lock authority was extracted to end.

So: a third thing, which is neither a lock nor a track. A **reference**, which says only what is being
pointed at.

## 2. The vocabulary

`Assets/MaverickFresh/Scripts/Combat/Core/MavEngagementTargetContract.cs` — 1 enum, 1 readonly struct, no
interface.

```csharp
public enum MavEngagementTargetKind { None = 0, Track = 1, WorldPoint = 2 }

public readonly struct MavEngagementTargetRef
{
    static MavEngagementTargetRef None { get; }
    static MavEngagementTargetRef FromTrack(int trackId);       // id <= 0 collapses to None
    static MavEngagementTargetRef FromWorldPoint(Vector3 p);    // every coordinate is valid, origin included

    MavEngagementTargetKind Kind { get; }
    bool IsNone { get; }  bool IsTrack { get; }  bool IsWorldPoint { get; }

    int     TrackId    { get; }   // 0 unless Kind == Track
    Vector3 WorldPoint { get; }   // zero unless Kind == WorldPoint

    bool TryGetTrackId(out int id);        // the preferred accessors: they force the
    bool TryGetWorldPoint(out Vector3 p);  // caller to handle the other two cases
}
```

### Decisions worth stating

**Track identity stays `trackId`.** No object, no name, no source key. Resolving an id is
`MavTargetTrackOwner`'s job, and a reference that held the resolved object would be asserting truth no
authority claimed.

**World-point identity stays a point.** No id is invented for it, and it is never converted into a track.

**`FromTrack(0)` is `None`, not a Track with no id.** Id 0 already means "no track" throughout the combat
path; a Track case carrying 0 would be a second way to say nothing — one that reads as something.
`FromWorldPoint` is deliberately asymmetric: there is no coordinate that means "no point", the origin is a
real place, and that asymmetry is exactly why `Kind` is stored rather than inferred from the payload.

**Equality includes the kind**, so "track 5" can never equal "the point at (5,0,0)" — including as
dictionary keys, where a hash collision plus a loose `Equals` would silently merge them. `Vector3`
components are compared exactly rather than through Unity's approximate `==`, because a contract that
called two different designations equal within a tolerance would be making a policy decision.

**No conversions.** No `ToTrack()`, no `AsWorldPoint()`, no implicit or explicit operators. A caller that
wants the other kind has to construct it and be seen doing so.

**No interface, deliberately.** A designation *authority* would be speculative here — nothing produces or
consumes these references yet. This phase adds the noun; the verbs come with the consumer that needs them.

## 3. Semantics: five things, kept distinct

| Concept | Question it answers | Who owns it today | Vocabulary |
|---|---|---|---|
| **Track selection** | "which track am I interested in?" | `MavTrackLockController.SelectedTrackId`; `MavF22SensorSuite.selectedTarget`; `MavCASTargetingSystem.candidateTarget` | `Track` ref |
| **Track lock** | "which track am I *committed to*, across missed observations?" | `MavTrackLockController` only | `Track` ref + `MavLockState` |
| **Track designation** | "which track is marked for attack?" | `MavCASTargetingSystem.designatedTarget` (as a `MavCASTarget`) | `Track` ref |
| **World-point designation** | "which *place* is marked for attack?" | `MavCASTargetingSystem.designatedPoint`; `MavTargetingPodSystem.lockPoint` | `WorldPoint` ref |
| **Weapon authorization** | "may this store be released right now?" | `MavCASWeaponSystem` inline checks | *none yet* |

The rules, each of which is asserted:

- **A `Track` ref does NOT imply Locked.** A reference to track 9 is identical whether or not track 9 is
  locked (`E-052`). Lock lives in `IMavTrackLockAuthority`, and the reference type carries no
  `MavLockState`, no loss reason and no quality (`E-051`).
- **A `WorldPoint` does NOT imply a Track.** It exposes no id, creates nothing in the track owner, and
  referencing an unknown id does not make the owner know it (`E-012`, `E-061`, `E-062`).
- **A designation does NOT imply weapon authorization.** The reference type has no member that answers a
  firing question — `Lock`, `Authoriz`, `Fire`, `Launch`, `Weapon`, `Designat`, `Commit`, `Engage`, `Valid`
  and `Quality` are all forbidden in its member names (`E-050`).
- **Selection is not designation.** Being interested in a track is not marking it for attack. This one is
  currently violated in production — see §5.

## 4. Inventory

| System | PRODUCES | CONSUMES | Migration required? | Blocks Missile Core? |
|---|---|---|---|---|
| **`MavTrackLockController`** | Track lock (`MavLockState` lifecycle, `LockedTrackId`), track selection, publishes `authoritativeLockTrackId` | `MavTargetTrackOwner` tracks; selection commands | **No.** Already the authority and already track-only. Exposing a `Track` ref would be sugar, not need | **No** — it is what Missile Core should consume |
| **`MavTargetingPodSystem`** | Two different things: a lock on a `MavCASTarget` (`lockedToTarget`), and a lock on a bare `lockPoint`. Also **writes** the CAS designation at `:405-420` | Own input/camera; `casTargeting` | **Yes** — legacy lock owner. Its target lock maps to `Track`, its point lock to `WorldPoint` | **No** (air-to-ground). Blocks *retirement* of legacy authorities |
| **`MavCASTargetingSystem`** | Designation: `designatedTarget` (object) **or** `designatedPoint` + `hasDesignatedPoint` (bare point). Separately `candidateTarget`, which is a *selection* | Pod writes, AI writes, own input | **Yes** — the designation owner; needs a designation authority in this vocabulary | **No** (CAS path) |
| **`MavCASWeaponSystem`** | Weapon release (precision, missile) | `targeting.hasDesignatedPoint`, `GetDesignatedOrCandidateTarget()`, `GetBestStrikePoint()` | **Yes**, but only *after* a designation authority exists | **No for A2A**, provided A2A release is a separate path. Blocks any *unified* authorization layer |
| **`MavPhysicalAIController`** | Designation — writes `designatedTarget`, `designatedPoint`, `hasDesignatedPoint` at `:280-283` | Own target logic | **Yes** — a designation authority must accept an AI producer, not only the player | **No** |
| **`MavSensorHudOverlay`** | Nothing (presentation) | `sensor.debugHasLock`, `debugLockProgress01`, `selectedTarget.transform.position` | **Yes** for the lock half; the marker position is geometry | **No** |
| **`MavFreshHud`** | Nothing (read-only consumer) | `IMavTrackLockAuthority` + `MavEngagementView` (**already migrated**); plus legacy `targetingPod.isLocked`, `casTargeting.designatedTarget`/`candidateTarget`/`status` | **Partially** — the TGP and TGT lines, once a designation authority exists | **No** |
| **`MavAirToAirLeadSight`** | Nothing (presentation) | `sensorSuite.selectedTarget` — target **geometry** (position + velocity) | **Yes but DEFERRED** — needs a prediction/extrapolation decision, which this vocabulary does not supply | **Not the lock path — but see §5** |

## 5. Two findings

### Selection currently implies weapon authorization

`MavCASWeaponSystem:738` and `:767` gate precision and missile release on:

```csharp
if (targeting == null || (!targeting.hasDesignatedPoint && targeting.GetDesignatedOrCandidateTarget() == null))
    return false;   // precision_no_designation / missile_no_designation
```

and `MavCASTargetingSystem.GetDesignatedOrCandidateTarget()` falls back to **`candidateTarget`** when
nothing is designated. A candidate is what the cycling logic is *pointing at* — a selection. So today,
merely having a candidate in view is sufficient authorization to release, and the failure string says
`no_designation` for a state that is not about designation at all.

This is pre-existing and **not fixed here** — fixing it changes release behavior, which is a migration, and
this phase migrates nothing. It is the sharpest reason the CAS path needs a real designation authority
rather than a widened lock: the collapse is not hypothetical, it is shipping.

Related, smaller: `GetBestStrikePoint()` falls back to `hasAimGroundPoint` and then to a point 1000 m ahead
of the nose, so the point a weapon aims at can exist when no designation does. The release gate above is
what currently prevents that being reachable.

### The A2A lead sight reads the wrong authority

`MavAirToAirLeadSight` takes its target from `MavF22SensorSuite.selectedTarget` — the legacy sensor's
selection, not the authoritative lock. It does not block Missile Core from *consuming* the lock, but if
Missile Core ships first, the pilot's lead cue and the missile's lock can be pointed at **different
aircraft**, with nothing in the system noticing. That is an A2A coherence problem, not a contract problem,
and it argues for migrating the lead sight before or alongside Missile Core rather than after.

## 6. Validation

New suite: `MavEngagementTargetContractValidation` — **62 assertions, 62/0**.

| Required proof | Assertions |
|---|---|
| Track ref carries only trackId | `E-001`–`E-003b` |
| Point ref carries only world position | `E-010`–`E-012b` |
| None carries neither | `E-020`–`E-023` |
| Track != Point | `E-030`–`E-034` |
| No GameObject / reference ownership | `E-040`–`E-046b` |
| No fake track creation | `E-060`–`E-064` |
| No implicit lock | `E-051`, `E-052`, `E-053` |
| No implicit authorization | `E-050`, `E-054` |
| *plus:* the lock authority stays track-only | `E-070`–`E-074` |

`E-070`/`E-071` pin `IMavTrackLockAuthority`'s exact surface — five properties, one call, the call still
answering with a `MavTargetTrackData`. `E-072` scans its member names for `Point`, `World`, `Designat`,
`Position`, `Cas`, `Pod`. Both halves are needed: the token scan catches a member *named* for a point, the
exact list catches one that is not.

### Negative-tested with five injected regressions

| Injected | Failed |
|---|---|
| `bool HasWorldPointLock { get; }` added to `IMavTrackLockAuthority` | `E-070`, `E-072` |
| `FromWorldPoint` stores track id 99 | `E-061b` |
| `TrackId` stops gating on kind | `E-012` |
| `IsLockedTarget` property added | `E-050` |
| `public readonly GameObject targetObject` field added | `E-041`, `E-041b`, `E-043`, `E-046b` |

**53 passed, 9 failed.** Two observations from that run, both worth keeping:

- The `GameObject` field **would not compile** at first: the private constructor must assign every field,
  so adding object ownership cannot be done by editing one line. A small structural defence, not designed
  for, but real.
- `E-012b` (`TryGetTrackId`) kept passing while `E-012` (`TrackId`) failed, because only the property was
  broken. The two accessors are independent layers and the suite reports them independently, which is the
  honest result rather than a coincidence.

### Gates

| Gate | Result |
|---|---|
| Compile | 0 errors |
| **Engagement Target Contract (new)** | **62 / 0 PASS** |
| Lock Authority Migration Prep | **51 / 0 PASS** |
| Lock Consumer Migration | **75 / 0 PASS** |
| Track Lock | **147 / 0 PASS** |
| Radar Core | **71 / 0 PASS** |
| TargetTrack Core | **35 / 0 PASS** |
| Combat boundary | **3 / 0 PASS** |
| FDM baseline | **1585 / 0 PASS** |
| Missing Script scan | **PASS** |

C-5, the phase tripwire in the combat boundary scan, was **not relaxed**: no missile, seeker or guidance
type name became legal in this phase.

## 7. Is this contract sufficient?

**Yes for the A2A half, with one caveat, and yes for keeping CAS separate.**

**A2A Missile Core can consume only authoritative track locks.** `IMavTrackLockAuthority` is unchanged and
track-only, `TryGetLockedTrack` still answers with a `MavTargetTrackData`, and a missile that reads it needs
nothing from this contract at all — which is the point. The vocabulary exists so that the *CAS* path has
somewhere to go that is not through the lock authority, not so that missiles gain a new dependency.

**CAS/TGP point designation stays a separate path.** `WorldPoint` expresses a bare designation without an
id, without a fabricated track, and without touching the lock interface. The pod's two lock kinds and the
CAS system's two designation kinds all map onto `Track` / `WorldPoint` with nothing left over.

**The caveat is that this is a noun without verbs.** Nothing produces or consumes a
`MavEngagementTargetRef` yet. Before `MavCASWeaponSystem` can be migrated, a designation authority has to
exist — one that admits both a player producer (`MavTargetingPodSystem`, `MavCASTargetingSystem`) and an AI
producer (`MavPhysicalAIController`), and that distinguishes designation from the `candidateTarget`
fallback described in §5. That is the next phase, and it is a behavior change, so it needs its own
equivalence evidence.

**What would still be wrong if Missile Core shipped tomorrow** is not the contract: it is that
`MavAirToAirLeadSight` reads the legacy sensor's selection, so the pilot's cue and the missile's lock could
disagree silently. Contract-sufficient is not the same as A2A-coherent.

## 8. Scope held

Not done, deliberately: no Missile Core, seeker, guidance, autopilot or datalink; `MavCASWeaponSystem` not
migrated; `MavAirToAirLeadSight` not migrated; `preferAuthoritativeLock` still `false`; issue #16 still
open; no legacy authority retired; no gameplay consumer touched; FDM untouched. `IMavTrackLockAuthority`,
`MavTrackLockController`, `MavEngagementView`, `MavTargetTrackOwner` and `MavFreshHud` are all unchanged —
this phase adds two files and nothing else.
