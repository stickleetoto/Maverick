# A2A-Only Combat Freeze R0

Base: `2a4181222aeb20edf390c395d8d9d14acfd1cfc0` (origin/main, after Engagement Target Contract R0)

Combat development is **air-to-air first**. Air-to-ground is **not deleted** — every CAS, targeting-pod and
A2G weapon source file is still here, still compiles, and still carries its own logic. It is
**quarantined**: removed from the active runtime so the A2A lock and fire-control design can be built
without CAS, TGP and bare-world-point semantics leaking into it.

This phase is QUARANTINE + RUNTIME DISABLE + SCOPE CLEANUP. It is not a deletion phase, and it retires
nothing.

## 1. Why A2G is frozen rather than migrated

The previous two phases both ended at the same finding. From `ENGAGEMENT_TARGET_CONTRACT_R0.md` §5:

> `MavCASWeaponSystem:738` and `:767` gate release on `hasDesignatedPoint || GetDesignatedOrCandidateTarget() != null`, and that accessor falls back to **`candidateTarget`** — a selection. So merely having a candidate in view authorizes release today.

Air-to-ground is where the three concepts this architecture separates — selection, designation, weapon
authorization — are most thoroughly merged, and where the one case the track vocabulary deliberately cannot
express (a lock on a bare ground point) is load-bearing. Migrating it correctly requires a designation
authority that does not exist yet.

Doing that work *now* would mean designing designation semantics in order to reach A2A, which is the wrong
order. Freezing costs nothing that is not recoverable and buys a runtime in which only track locks exist.

## 2. The freeze, in one place

`Assets/MaverickFresh/Scripts/Combat/Core/MavCombatScopePolicy.cs`:

```csharp
public static readonly bool AirToGroundFrozen = true;
public static bool AirToGroundAllowed { get { return !AirToGroundFrozen; } }
```

**Why a policy and not a serialized flag.** Every A2G system already has serialized switches —
`setupOnAwake`, `installCASStarter`, `addTargetingPod`, `allowAutoDesignate` — and they are all `true` in
the scenes that exist. Freezing by editing those values would put the decision in scene data: invisible in
review, easy to flip by accident, impossible to assert. The decision lives in code, and each A2G entry point
consults it and fails closed. A scene that still has every install flag set is now harmless — asserted by
`F-015`.

**No runtime setter, deliberately.** Nothing — not a scene, not a debug menu, not a stray script — can
re-enable A2G. Restoring it is a one-line source change, reviewed like any other (`F-002`).

**Scenes and prefabs were not modified.** No scene surgery, no prefab edits. The freeze is entirely in code,
which is why it can be validated and reverted.

## 3. What is frozen, and how each is prevented from activating

| System | Barrier | Assertions |
|---|---|---|
| `MavCASStarterBootstrap` | `InstallAirToGround()` returns immediately — CAS targeting, CAS weapons, CCIP, ordnance, pod and TGP manager are **never added** | `F-014`–`F-014e` |
| `MavFreshBootstrap` | skips installing `MavTGPStateManager` | `F-014d` |
| `MavCASTargetingSystem` | `IsDesignationAllowed` false; `Awake` disables the component and records `a2g_frozen`; `Update` returns before reading any key; `DesignateCandidateOrPoint`, `CycleTarget`, `UpdateGroundPoint` all fail closed | `F-020`–`F-025` |
| `MavTargetingPodSystem` | `IsPodAllowed` false; `Awake`/`OnEnable`/`Start`/`Update` force dormant and disable; `ToggleLock`, `SetFocus`, `SetPip`, `PushDesignationToCAS` fail closed; `displayMode` forced `Off` | `F-030`–`F-034` |
| `MavCASWeaponSystem` | `IsWeaponReleaseAllowed` false; `Awake` disables; `Update` returns before reading the trigger; `TryFirePrimary`/`TryFireSecondary`/`TryFireSelected`/`TryFireWeapon` all refuse and record the reason | `F-040`–`F-045` |
| `MavPhysicalAIController` CAS designation | the direct field-write site is gated on the policy | `F-051`–`F-051c` |
| `MavPhysicalAIController` A2G release | its `TryFireSelected` call is gated too | `F-052`, `F-052b` |
| CAS/TGP input | every A2G key is read inside a guarded `Update`, and every command method behind those keys fails closed independently | `F-025`, `F-045`, `F-022`–`F-023b`, `F-031`–`F-033` |
| A2G player HUD | weapon-status and TGP lines removed from the player panel; A2G key hints removed | `F-061`–`F-061d` |
| Player quick help | CAS and TGP key rows removed, replaced by a single "A2G DISABLED" line | `F-064`, `F-064b` |
| Precision / rocket / bomb / CAS-missile release | barred at four layers: bootstrap, component enable, each `TryFire*`, and the single `TryFireWeapon` dispatch | `F-043`–`F-044` |
| Bare-world-point targeting | the pod cannot point-lock and CAS resolves no ground point | `F-024`, `F-024b`, `F-031` |

### Layered on purpose

The outermost barrier — not installing the components — is the one that matters: if nothing is added, there
is nothing reading input and nothing writing a designation. The per-component guards exist for aircraft that
already carry these components in **scene or prefab data**, which the bootstrap never sees. The
`TryFireWeapon` guard is the innermost, on the one path every store goes through, so a future consumer
calling a `Fire*` method directly cannot get past it without removing it deliberately.

### One barrier is structural, and says so

`MavPhysicalAIController` writes `casTargeting.designatedTarget` and three more public fields by **direct
assignment**. No guard inside the designator can intercept a field write, so that barrier lives at the write
site and is asserted as source (`F-051`), with a check that there is exactly one such site to gate
(`F-051b`). Marked as structural rather than dressed up as a behavioral proof.

## 4. The gun: **the gun is FROZEN**

Audited as §5 required, and it is not a close call. The gun is:

- declared in `MavCASWeaponSystem` — `MavCASWeapon.Gun = 0`, `selectedWeapon = MavCASWeapon.Gun`
- fed by that component's state — `gunAmmo`, heat, spin-up, spread, convergence, tracer and VFX fields
- fired from that component's `Update` via `TryFirePrimary()` → `FireGun()`
- dependent on `MavCASWeaponSystem.Resolve()` for its camera, rig, CCIP and tracer VFX

So the gun is **owned by `MavCASWeaponSystem`**. Keeping it would mean keeping that component enabled and
reading input, which is precisely the half-CAS ownership §5 forbids. It is frozen with the rest, and it is
not spared for being useful in air-to-air (`F-041`).

**An A2A gun, if one is wanted, is a later phase with an owner of its own.** Nothing of the gun's logic was
deleted — `FireGun`, `FireGunLegacy` and `FireNeonHitscanGunRound` are all intact (`F-091`).

## 5. What remains active: the A2A stack

```
MavRadarSensor  →  MavTargetTrackOwner  →  MavTrackLockController  →  IMavTrackLockAuthority  →  A2A consumers
```

| Active | Evidence |
|---|---|
| Radar Core (`MavRadarSensor`) | installed by the bootstrap, still registers as a radar observation feed — `F-011`, `F-080`, `F-080b` |
| TargetTrack Core (`MavTargetTrackOwner`, `MavEngagementView`) | installed, holds tracks — `F-010`, `F-010b`, `F-071` |
| `MavTrackLockController` | installed and wired to owner and view; establishes a real lock — `F-012`, `F-016`, `F-070` |
| `IMavTrackLockAuthority` | **track-only**, five properties and one call, unchanged — `F-081`, `F-081b`, `F-082` |
| Authoritative lock HUD | player line and developer line both intact — `F-063`, `F-070b`, `F-070c` |
| `MavLegacyTargetObservationFeed`, `MavLegacyEngagementProbe` | installed: migration evidence — `F-013`, `F-013b` |
| `MavEngagementTargetRef` | unchanged, all three kinds — `F-083`–`F-083e` |

`MavEngagementTargetRef.WorldPoint` **remains dormant infrastructure** for A2G restoration. It was not
removed, and `IMavTrackLockAuthority` did not gain it (`F-083b`, `F-081b`).

## 6. Exact role of `MavF22SensorSuite` STT

**SHADOW / LEGACY DIAGNOSTIC. It is NOT the active authority, and it is not retired in this phase.**

| Question | Answer |
|---|---|
| Does it still run? | **Yes.** It is not frozen and carries no scope guard — asserted by `F-102d` |
| Is it an authority? | **No.** `MavTrackLockController` is the only lock authority |
| Why keep it? | Its projection into `MavEngagementView` (`sensorLockTrackId`, `legacySensorLockState`) is the evidence the lock migration is judged by — the `legacyDisagreesWithAuthoritative` signal corrected in Migration Prep R0 compares against it |
| What changed? | Its overlay is **labelled**. `MavSensorHudOverlay` now prints `SHADOW STT LOCK` / `SHADOW STT ACQ n%` and draws a `SHADOW STT` marker instead of a bare `LOCK` — `F-112b`, `F-112c` |
| Why label it? | An unlabelled `LOCK` from a non-authority is indistinguishable from the authoritative lock. That is the exact confusion the authority was extracted to end, and §8 forbids presenting dormant or shadow state as gameplay authority |
| When is it retired? | After `preferAuthoritativeLock` flips and the disagreement signal has stayed clear. **Not this phase** |

Note this is *A2A* legacy, not A2G: it is deliberately outside the freeze. The three legacy **lock**
authorities of issue #16 all still exist as components (`F-102`) and the legacy projection still maps their
states unchanged (`F-102b`), so issue #16's assumptions hold and it stays **open**.

## 7. Player-visible changes

Removed from the normal player HUD:

- `GUN … SEC … RKT … BOMB … MSL …` — A2G weapon status
- `TGP <mode> LOCK/SEARCH` — targeting-pod state
- `Mouse0 neon gun | Space missile/secondary` — A2G key hints
- the quick-help `CAS:` and `TGP:` key rows, replaced by one `Air-to-ground (CAS / TGP / bombs / rockets / gun): A2G DISABLED` line

Changed:

- the legacy STT overlay now reads `SHADOW STT LOCK` rather than `LOCK`

Kept:

- the authoritative `LOCK` line — the only lock the player sees is now the real one
- all flight, engine, gear, TVC and aero readouts

Behaviourally: **A2G weapons no longer fire and the pod no longer opens.** That is the intended change, and
it is the whole player-visible cost of this phase. Air-to-air lock acquisition, maintenance, coast and loss
are unaffected.

The developer panel (F2) still shows dormant A2G state, every line prefixed `[A2G DISABLED]` and stating
whether each barrier is holding (`F-062`–`F-062d`).

## 8. Validation

New suite: `MavA2AFreezeValidation` — **F-001 … F-113c**.

All twenty required proofs, in the order §12 lists them:

| # | Proof | Assertions |
|---|---|---|
| 1 | `MavCASTargetingSystem` cannot become active | `F-014`, `F-020`, `F-021`–`F-021c`, `F-025` |
| 2 | `MavTargetingPodSystem` cannot establish an active point lock | `F-031`–`F-031d`, `F-034` |
| 3 | `MavCASWeaponSystem` cannot release A2G weapons | `F-040`–`F-044` |
| 4 | CAS designation cannot be written by player input | `F-022`–`F-023b`, `F-025` |
| 5 | CAS designation cannot be written by `MavPhysicalAIController` | `F-051`–`F-051c` |
| 6 | A2G input commands fail closed | `F-025`, `F-045`, `F-033` |
| 7 | A2G player HUD lines are absent | `F-061`–`F-061d`, `F-064` |
| 8 | authoritative A2A lock HUD remains functional | `F-063`, `F-070`–`F-070d` |
| 9 | Radar Core remains active | `F-011`, `F-080`, `F-080b` |
| 10 | TargetTrack remains active | `F-010`, `F-010b`, `F-071` |
| 11 | `MavTrackLockController` remains active | `F-012`, `F-016`, `F-082` |
| 12 | `IMavTrackLockAuthority` remains track-only | `F-081`, `F-081b` |
| 13 | `MavEngagementTargetRef` remains unchanged | `F-083`–`F-083e` |
| 14 | A2G source files still exist | `F-090`, `F-090b` |
| 15 | no A2G source deletion occurred | `F-091`–`F-091c` |
| 16 | no Missile Core / seeker / guidance type added | `F-100` |
| 17 | `preferAuthoritativeLock` remains false | `F-101` |
| 18 | issue #16 assumptions remain valid | `F-102`–`F-102c` |
| 19 | F-22 STT runtime role explicitly documented | `F-111`–`F-112c` |
| 20 | gun state explicitly classified | `F-113`–`F-113c` |

The central case is `F-010`–`F-016`, which runs the **real** `MavCASStarterBootstrap.Setup()` and asserts
both halves of what it produced. It is written that way because the obvious implementation of this phase —
returning early from `Setup` — silently freezes the A2A stack as well, since `InstallTargetTrackCore` is
called from the end of the same method. A test that only checked "no CAS components" would have passed that
mistake; this one was caught during implementation for exactly that reason.

Where a barrier lives in `Awake` or `Update`, it is invoked by reflection, because edit-mode Unity raises no
lifecycle callbacks. Same approach the consumer-migration suite settled on.

### Negative-tested twice

**Whole policy lifted** (`AirToGroundFrozen = false`): **76 passed, 30 failed** — failures across every
barrier category: the policy itself (`F-001`, `F-001b`), the bootstrap (`F-014`–`F-014e`), the designator
(`F-020`–`F-025`), the pod (`F-030`–`F-034`) and weapon release (`F-041b`–`F-045`).

**One barrier removed** (the `InstallAirToGround` guard only, policy still frozen): **101 passed, 5 failed** —
exactly `F-014`–`F-014e` and nothing else. The per-component guards still held, which is the layering claim
demonstrated rather than asserted.

Both were restored before committing, verified by re-running to 106/0.

### What the negative test corrected

The first run exposed weak assertions of my own. `F-041`, `F-043`, `F-043c` and `F-043d` originally checked
only that `TryFire*` returned false — and they **passed with the freeze lifted**, because in edit mode those
weapons cannot fire anyway: no rig, no camera, no ordnance assets. A false return proved nothing.

They now check the recorded reason as well, so "refused because frozen" is distinguished from "failed for its
own reasons". After the fix the same negative test produces 30 failures instead of 27, with all four store
assertions biting.

**One residual, stated rather than papered over:** `F-041` (the gun) still passes when the freeze is lifted,
because the gun genuinely cannot fire in edit mode. `F-041b` is the assertion that catches it, and `F-044` —
no ammunition consumed — is what proves a real release would otherwise have happened. The gun's barrier is
verified by those two, not by `F-041` alone.

## 9. Restoring air-to-ground

One line:

```csharp
public static readonly bool AirToGroundFrozen = false;   // in MavCombatScopePolicy
```

Everything comes back: the bootstrap installs the components again, the guards open, input works, the HUD
lines return. The A2A freeze suite will then fail loudly, which is correct — it asserts the frozen state, and
un-freezing is a decision that should have to change its expectations.

What should happen *before* that, rather than instead of it: the designation authority described in
`ENGAGEMENT_TARGET_CONTRACT_R0.md` §7, so that A2G comes back onto `MavEngagementTargetRef` with designation
separated from selection and from weapon authorization — rather than coming back as it was.

## 10. What remains before Track Prediction

`MavAirToAirLeadSight` still reads `sensorSuite.selectedTarget` — a GameObject, from the legacy sensor's
selection, not from the authoritative lock (`F-103`). It consumes target **geometry**: position and velocity
for a lead solution. Before it can migrate, a decision is needed on where extrapolated target state comes
from — the track owner, a prediction service, or the consumer — because a lead cue computed from a stale
track is worse than none. That decision is Track Prediction, and it is not started here.

## 11. What remains before Missile Core

1. **Track Prediction**, per §10 — a missile needs predicted target state, and so does the lead sight. Doing
   them separately would produce two answers to one question.
2. **Lead sight migration**, so the pilot's cue and the missile's lock come from the same authority. Today
   they would not: the lead sight reads legacy STT selection while a missile would read
   `IMavTrackLockAuthority`. Shipping Missile Core first would let the cue and the lock point at **different
   aircraft** with nothing noticing.
3. **Launch authorization** — deliberately absent. `MavEngagementTargetRef` has no member that answers a
   firing question, and this phase added none. A2A release authority needs its own owner, and it must not
   inherit the CAS pattern where a *selection* authorizes release.

The A2A runtime is now clean in the sense that matters for this: the only engagement state a new A2A consumer
can reach is a track lock from `IMavTrackLockAuthority`. There is no bare point, no designation and no
weapon-authorization path left running for it to accidentally depend on.

## 12. Scope held

Not done, deliberately: no Track Prediction; no lead-sight migration; no Missile Core, seeker, guidance,
autopilot, datalink or ECM/ECCM; no launch authorization; legacy STT **not** retired; issue #16 **open**;
`preferAuthoritativeLock` still **false**; `MavEngagementTargetRef` not redesigned; `IMavTrackLockAuthority`
unchanged; FDM untouched; no scene or prefab modified; **no A2G source file deleted**.
