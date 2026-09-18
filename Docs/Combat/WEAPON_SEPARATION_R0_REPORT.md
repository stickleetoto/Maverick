# Weapon System Separation R0 — Report

Architectural separation of Maverick's legacy weapon/combat implementation, so that targeting, radar
and missile work can be built later without inheriting its coupling. Cleanup and ownership only: no
radar, no AIM-120, no AIM-9, no guidance, no seeker.

## 1. Base and branch

| | |
|---|---|
| Authoritative base | `ed778000f528eb8ebd2ff53a45ea59216e858267` |
| Branch | `sol/weapon-system-separation-r0` |
| Branch HEAD | `a1b2929` + this document |
| Ancestry verified | `ed77800` is an ancestor of HEAD; branch was cut from it exactly |
| Unity | `6000.3.16f1` |

## 2. Changed files

Nine added, three modified, four documents. Nothing moved, nothing deleted.

```
A  Assets/MaverickFresh/Scripts/Combat.meta
A  Assets/MaverickFresh/Scripts/Combat/Core.meta
A  Assets/MaverickFresh/Scripts/Combat/Core/MavDamageContract.cs            (+ .meta)
A  Assets/MaverickFresh/Scripts/Combat/Core/MavTargetTrackContract.cs       (+ .meta)
A  Assets/MaverickFresh/Scripts/Combat/Core/MavCombatCommandContract.cs     (+ .meta)
A  Assets/MaverickFresh/Scripts/Combat/Editor.meta
A  Assets/MaverickFresh/Scripts/Combat/Editor/MavCombatBoundaryScan.cs      (+ .meta)
M  Assets/MaverickFresh/Scripts/CAS/MavCASTarget.cs                         (+16 lines)
M  Assets/MaverickFresh/Scripts/CAS/MavCASWeaponSystem.cs                   (+31 lines)
M  Assets/MaverickFresh/Scripts/Weapons/MavAircraftDamageState.cs           (+18 lines)
A  Docs/Combat/LEGACY_WEAPON_INVENTORY_R0.md
A  Docs/Combat/COMBAT_OWNERSHIP_R0.md
A  Docs/Combat/COMBAT_ARCHITECTURE_NEXT.md
A  Docs/Combat/WEAPON_SEPARATION_R0_REPORT.md
```

No scene, prefab or `.asset` was modified. No flight-dynamics, aero, aircraft, tuning, reference-data,
`ProjectSettings` or FDM-manifest file was touched.

## 3. Components discovered

Full detail in `LEGACY_WEAPON_INVENTORY_R0.md`. The structural finding:

**There are two complete combat implementations and only one runs.** Of 296 project scripts, 23 are
serialized into a scene, prefab or asset, and **none of them is under `Assets/Maverick/`**. That whole
older line — `F15ERadarSystem`, `TargetingPodSystem`, `AbstractStrikeSystem`, `MaverickWeaponSelector`,
`SensorFusionManager`, `CasRequestManager`, the ten-file `SensorAI` stack, the old HUDs — has zero GUID
references, no `RuntimeInitializeOnLoadMethod`, and is `AddComponent`-ed only by other dormant
bootstraps. It compiles and cannot run.

- **Live combat components: 21**, all under `Assets/MaverickFresh/Scripts/`, wired at runtime by
  `MavInGameBootstrap` → `MavCASStarterBootstrap`.
- **Dormant combat components: ~45**, all under `Assets/Maverick/Scripts/`.

Two unrelated target-identity concepts exist and nothing relates them: `MavCASTarget` for ground and
`MavRadarSignature` for air, each discovered independently by `FindObjectsOfType` in at least three
different consumers.

## 4. Components quarantined

Quarantine in R0 is logical, not physical, because six live combat scripts are serialized into
`Mav_InGame.unity` or `F15E_Player.prefab` and moving them trades reference safety for tidiness.

| Classification | Count | Treatment |
|---|---|---|
| `LEGACY` | 14 live components | classified, documented, left running and physically in place |
| `SEPARATE` | 4 (`MavCASTarget`, `MavAircraftDamageState`, `MavRadarSignature`, `MavCASStarterBootstrap`) | identified as the seams the new architecture attaches to; two now implement `IMavDamageReceiver` |
| `KEEP` | 3 (`MavFreshHud`, `MavFreshInput`, enemy AI/spawners) | not weapon simulation; keep, decouple later |
| `REMOVE_LATER` | ~45 dormant | proven unreachable, fully recoverable, deleted only after the new stack works |
| `UNKNOWN` | 0 | every combat-candidate file was resolved to one of the above |

## 5. Components preserved / deleted

Preserved: everything. **Nothing was deleted in R0**, and no weapon behavior was altered — no damage
value, spread, cooldown, ammunition count, lock time, projectile speed or effect changed. Existing
weapons work exactly as before through their original code paths.

Deleted: nothing.

## 6. Ownership problems discovered

Nine, detailed in `COMBAT_OWNERSHIP_R0.md`. One fixed here; the rest documented for the phases that
own them.

| | Problem | Status |
|---|---|---|
| O-1 | Input owns simulation — four components read the keyboard inside the `Update` that simulates | contract added (`IMavCombatCommandSource`), not yet wired |
| O-2 | `FindObjectsOfType` is the authoritative target interface, so the scene graph *is* the track database | contract added (`MavTargetTrackData`) |
| O-3 | `MavCASWeaponSystem` is 1053 lines owning six responsibilities | documented; belongs to Weapon Core |
| O-4 | Three competing lock/designation authorities, so there is no single answer to what the aircraft is engaging | documented; belongs to TargetTrack Core |
| O-5 | No AI or FAM can select, designate or fire — the architecture assumes a human | contract added |
| O-6 | **Damage delivered by reflection against a type-name string literal** | **FIXED** |
| O-7 | Kill state has two writers | documented |
| O-8 | HUD discovers concrete legacy implementations by `FindObjectOfType` (reads only — right direction, wrong binding) | documented |
| O-9 | `MavCASWeaponSystem` writes gun recoil straight to the airframe Rigidbody, bypassing `MavSixDoFBody` | inert today (`applyGunRecoil=false`, force `0f`); scan rule C-2/C-6 prevents the new architecture inheriting it |

O-6 in detail, since it was the worst: `TryDamageAircraft` matched receivers with
`component.GetType().Name != "MavAircraftDamageState"` and then probed
`ApplyDamage(float,string,Vector3)`, `ApplyDamage(float,string)` and `TakeDamage(float)` in turn
through `MethodInfo.Invoke`. No compiler could see that coupling; renaming the receiver would have
silently stopped all damage. `MavCASTarget` and `MavAircraftDamageState` now implement
`IMavDamageReceiver` explicitly, delegating to the methods they already had; the weapon system tries
the contract first and keeps the reflection probe as a documented fallback. The contract path
deliberately skips `MavCASTarget`, which the caller already handles in its own branch over the same
`GetComponentInParent` scope, so `TryDamageAircraft` keeps meaning aircraft and no outcome changes.

## 7. Validation results

| Check | Result |
|---|---|
| Project compiles | **PASS** — 0 `error CS` |
| FDM validation baseline (full, clean committed checkout) | **PASS — 1585 passed / 0 failed / 25 counted suite results**, 28 suites, `environment_churn` 0, authority `ef25b91` |
| FDM baseline identical to pre-branch result | **yes** — same 1585/0 |
| Combat boundary scan (C-1..C-6) | **PASS** 3/0, including two self-tests |
| Aircraft spawns | **PASS** — `Mav_Player`, `F-16C FIGHTING FALCON (F16C)`, `hasAuthoritativeAircraft=True`, Rigidbody present |
| Flight behavior unchanged | **PASS, bit-identical** — 30 tuning parameters identical, and 24 trajectory samples over 600 physics steps (position, velocity, euler, angular velocity) byte-identical to the pre-branch reference |
| Missing Scripts in scenes | **PASS** — 3 scenes, 6 prefabs, every component checked, `missing=0` |
| Broken serialized references | **PASS** — no scene/prefab/asset modified; no dangling `m_Script` GUID introduced |
| Legacy weapons recoverable | **PASS** — nothing deleted or moved |
| Combat core independent of FDM | **PASS** — enforced by scan rule C-1 |
| No radar implementation introduced | **PASS** — enforced by C-5 |
| No missile guidance introduced | **PASS** — enforced by C-5 |
| No AIM-120 / AIM-9 parameters added | **PASS** — enforced by C-5 |
| FDM authority manifest untouched | **PASS** — new scan placed under `Scripts/Combat/Editor`, deliberately not `Scripts/Editor` |

Flight determinism note: probe runs pin `Time.captureFramerate = 50`, which makes Maverick's
trajectories reproducible across separate Unity launches. The comparison above is therefore exact
rather than within-tolerance.

### 7.1 The baseline caught a real regression

The first full FDM baseline run against this branch **failed**: `phase5_writer_scan` reported one
ungated player-physics writer and `phase5a_ownership` failed three assertions downstream of it.

The writer was `MavTargetTrackContract.cs` — a file that writes no physics at all. It named a struct
field `velocity`, and the frozen Phase 5 writer scan counts the text `.velocity =` anywhere under
`MaverickFresh/Scripts` as a live Rigidbody write.

Resolved on the correct side: the field is now `velocityMps`. The writer scan is frozen
flight-dynamics validation authority whose blob is pinned by the FDM manifest, so editing it or
adding an exemption would mean re-pinning a baseline this phase is required not to touch. Boundary
rule **C-6** now checks combat files against the same token set, so the collision fails fast in the
combat scan with an explanation rather than resurfacing as an unexplained FDM ownership failure.

Worth recording plainly: a static architecture scan written this phase was caught by a scan written
two phases ago. That is the FDM baseline doing exactly what it was built for.

## 8. Remaining risks

1. **`MavCASWeaponSystem` is still a 1053-line single owner** (O-3). R0 removed its hidden coupling
   but did not split it. Any new munition still means editing the component that reads the keyboard
   and draws tracers. MEDIUM.
2. **The reflection damage fallback still exists.** Kept deliberately for receivers not yet migrated.
   It should be deleted once every receiver implements `IMavDamageReceiver`. LOW.
3. **Three lock authorities remain** (O-4). Until TargetTrack Core unifies them there is no single
   answer to hand a missile at launch. MEDIUM — it blocks Missile Core, not Radar Core.
4. **`MavF22SensorSuite` is the tempting wrong base.** It is live, it says radar, and it conflates
   scanning, lock timing, input and HUD. Radar Core should read it and start clean. MEDIUM.
5. **Recoil call site remains** (O-9). Inert, but enabling it would violate FDM ownership. LOW, and
   loud if it happens: the FDM writer scan already lists `MavCASWeaponSystem.cs` as an ungated writer
   classified as not-player-body — if that classification ever stops being true the baseline fails.
6. **The dormant line still compiles**, so a future contributor can still reference it by accident.
   Deletion is deliberately deferred until the new stack works. LOW.
7. **The contracts have no implementations yet.** `IMavCombatCommandSource` and
   `IMavTargetTrackSource` are shapes, not systems; they are worth nothing until TargetTrack Core
   wires them. Acceptable for a separation phase, but it means R0 proves no new behavior.
8. **Pre-existing and unrelated:** `Assets/Settings/DefaultVolumeProfile.asset` holds four script
   GUIDs that resolve nowhere, even against a freshly imported package cache. That asset dates from
   the initial commit and is untouched by this branch — stale URP volume-component references. Worth
   a separate look; not combat.

## 9. Recommendation for TargetTrack Core R0

Branch from this branch's merge commit once it lands.

1. **Build the track owner first, not the sensor.** Something owns a set of `MavTargetTrackData`,
   assigns stable ids, ages entries and drops them. No detection model yet.
2. **Ship a legacy adapter immediately.** Wrap today's `MavCASTarget` and `MavRadarSignature` sweeps
   behind `IMavTargetTrackSource` with `MavTrackSource.Legacy` and `MavTrackQuality.Coarse`. That
   lets HUD, AI and the future missile move onto the contract before any real sensor exists, and it
   makes the eventual radar a substitution rather than a rewrite.
3. **Move `MavFreshHud` onto tracks** as the first consumer. It only reads, so it is the safest
   migration, and it retires three `FindObjectOfType` bindings (O-8).
4. **Then unify the three lock authorities** (O-4) behind the track owner — CAS designation, sensor
   STT, pod lock. This is the prerequisite for anything launchable.
5. **Wire `IMavCombatCommandSource`** with a legacy keyboard implementation that reads exactly the
   keys the weapon system reads today, and have `MavCASWeaponSystem` consult the source instead of
   the keyboard directly. Behavior-preserving, and it is what finally lets AI and FAM fire (O-5).
6. **Keep C-5 in force** until Radar Core deliberately relaxes it. Relaxing it should be a commit
   with a reason, not a side effect.
7. **Run the full FDM baseline on every combat branch.** It cost one iteration this phase and caught
   a real regression that no combat-side check would have noticed.

Do not, in TargetTrack Core: implement detection, scan volumes, lock semantics, seekers, guidance or
any missile profile; extend `MavF22SensorSuite`; or delete anything from the dormant line.
