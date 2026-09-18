# Combat Ownership — R0

Where each combat responsibility is actually owned today, and which ownerships are unsound. Derived
from the live line only; the dormant `Assets/Maverick/` line owns nothing at runtime
(see `LEGACY_WEAPON_INVENTORY_R0.md` §1).

## 1. Current ownership table

| Responsibility | Current owner | How it is reached | Sound? |
|---|---|---|---|
| Target selection (ground) | `MavCASTargetingSystem` | own key reads + `FindObjectsOfType<MavCASTarget>` + raycast | **No** — O-1, O-2 |
| Target selection (air) | `MavF22SensorSuite` | own key reads + `FindObjectsOfType<MavRadarSignature>` | **No** — O-1, O-2, O-4 |
| Target lock (air) | `MavF22SensorSuite` | internal STT timer | **No** — O-4 |
| Target lock (pod) | `MavTargetingPodSystem` | own key reads, own lock flag | **No** — O-4 |
| Weapon selection | `MavCASWeaponSystem` | own key reads | **No** — O-1 |
| Fire command | `MavCASWeaponSystem` | `MavFreshInput.GetKey(firePrimaryKey)` inside its own `Update` | **No** — O-1, O-5 |
| Projectile spawning | `MavCASWeaponSystem` | `Instantiate` + `MavCASBallisticProjectile.Init` | Partly — O-3 |
| Damage application | `MavCASWeaponSystem`, `MavCASBallisticProjectile` | direct call for `MavCASTarget`; **reflection** for aircraft | **Was no** — O-6, fixed in R0 |
| Damage absorption | `MavCASTarget`, `MavAircraftDamageState` | own health/armor rules | Yes |
| Kill state | `MavCASWeaponSystem.destroyedCount` and each receiver's `isDestroyed` | incremented by whoever landed the hit | **No** — O-7 |
| HUD feedback | `MavFreshHud`, `MavSensorHudOverlay`, `MavAirToAirLeadSight` | `FindObjectOfType` on concrete combat types | Partly — O-8 |
| Input bindings | `MavCASWeaponSystem`, `MavCASTargetingSystem`, `MavF22SensorSuite`, `MavTargetingPodSystem` | four separate `KeyCode` sets, each read by the simulating component | **No** — O-1 |
| AI firing | **nobody** | no path exists | **No** — O-5 |
| Cooldown / ammunition state | `MavCASWeaponSystem` | public fields on the same component that fires | Partly — O-3 |
| Recoil on the airframe | `MavCASWeaponSystem` | `cachedRigidbody.AddForce(...)` | **No** — O-9 |

## 2. Ownership problems

### O-1 — Input owns simulation

Four components read the keyboard inside the same `Update` that performs the simulation.
`MavCASWeaponSystem` reads `firePrimaryKey` and, in the same method, runs the hitscan, consumes
ammunition and applies damage. There is no point at which "the pilot wants to fire" exists as a
separate fact from "a round was fired".

Consequence: the keyboard is the only possible trigger. Anything else that wants to fire must
impersonate a human.

Addressed in R0 by: `IMavCombatCommandSource` (contract only — not yet wired).

### O-2 — Scene search used as authoritative state

`FindObjectsOfType<MavCASTarget>()` and `FindObjectsOfType<MavRadarSignature>()` are the primary
interface for "what targets exist", called from the targeting system, the sensor suite, the lead
sight and the weapon system's area damage.

Consequence: the scene graph *is* the track database. There is no way to express a target that is
believed but not present, present but not detected, or detected a moment ago and now stale — the
three things a radar exists to represent. It also means every consumer gets truth, not sensor
output, so no sensor limitation can ever be modelled.

Addressed in R0 by: `MavTargetTrackData` / `IMavTargetTrackSource` (contract only).

### O-3 — One component owns six responsibilities

`MavCASWeaponSystem` is 1053 lines owning: input, weapon selection, gun ballistics, projectile
spawning, damage application, ammunition and heat state, tracer/muzzle presentation, and recoil.

Consequence: a new munition type cannot be added without editing the component that also reads the
keyboard and draws tracers.

Addressed in R0 by: nothing structural. Splitting it is Weapon Core work, not separation work. R0
records the boundary and removes its hidden coupling.

### O-4 — Three competing "what am I pointed at" authorities

`MavCASTargetingSystem.designatedTarget` (ground), `MavF22SensorSuite.selectedTarget` (air),
`MavTargetingPodSystem.isLocked` (pod). Each has its own keys, its own scan, its own lifetime. None
knows about the others, and the HUD prints all three.

Consequence: there is no single answer to "what is the aircraft engaging", which is precisely what a
missile needs to be handed at launch.

Addressed in R0 by: documenting it. Unification is TargetTrack Core.

### O-5 — Player-only firing

No AI or FAM path can select, designate or fire. `MavEnemyAircraftPilot` and `MavEnemyLBMBrain` fly
but do not shoot; nothing in the project constructs a fire command that did not come from a key.

Consequence: the architecture assumes a human. Section 10 of the R0 brief requires that it must not.

Addressed in R0 by: `IMavCombatCommandSource` establishes that firing is a request from *a* source,
not from the keyboard specifically.

### O-6 — Damage delivered by reflection against a type name — FIXED IN R0

`MavCASWeaponSystem.TryDamageAircraft` matched receivers with
`component.GetType().Name != "MavAircraftDamageState"` and then invoked one of three possible
signatures — `ApplyDamage(float,string,Vector3)`, `ApplyDamage(float,string)`, `TakeDamage(float)` —
through `MethodInfo.Invoke`.

Consequence: a coupling no compiler can see. Renaming the receiver silently stops all damage, and a
future warhead has no declared way to deliver a hit.

Fixed in R0: `IMavDamageReceiver` with `MavDamageInfo`. Both receivers implement it explicitly by
delegating to their existing methods, the weapon system tries the contract first, and the reflection
probe stays only as a documented fallback for receivers not yet migrated.

### O-7 — Kill state has two writers

`MavCASWeaponSystem.destroyedCount` is incremented by whoever observed the transition, while the
receiver separately owns `isDestroyed`/`destroyed`. A kill scored by a projectile, an area blast and
a gun round is counted in three different places.

Consequence: no single kill record exists to drive scoring, telemetry or mission logic.

Addressed in R0 by: documenting it. Kill/telemetry ownership belongs with Damage/Telemetry later.

### O-8 — HUD discovers concrete implementations

`MavFreshHud` holds typed fields for `MavCASTargetingSystem`, `MavCASWeaponSystem`,
`MavCASCCIPPredictor` and `MavTargetingPodSystem`, and fills them with `FindObjectOfType` when null.

The HUD does **not** own simulation — it only reads, which is the right direction. The problem is
narrower: it is bound to concrete legacy classes, so those classes cannot be replaced without
editing the HUD.

Addressed in R0 by: documenting it. The fix is for the HUD to read a state/telemetry contract, which
requires that contract to exist first.

### O-9 — Combat writes to the airframe Rigidbody

`MavCASWeaponSystem` calls `cachedRigidbody.AddForce(-transform.forward * gunRecoilForce, ...)`.

This bypasses `MavSixDoFBody`, the single load-application boundary the flight-dynamics work
established. It is currently inert — `applyGunRecoil` defaults to `false` and `gunRecoilForce` is
`0f` — so it changes no flight behavior today, but the call site exists and would violate FDM
ownership the moment someone enables it.

Addressed in R0 by: `MavCombatBoundaryScan` rule C-2 forbids Rigidbody writes in `Combat/Core`, so
the new architecture cannot inherit the pattern. The legacy call site is left in place and recorded
here; when recoil is wanted for real it must arrive as a load through the FDM, not as an `AddForce`.

## 3. Target ownership after separation

```
Player input            AI / FAM
     |                      |
     +----------+-----------+
                |
      IMavCombatCommandSource        "someone wants to fire"
                |
                v
        Combat command handling      selection, cooldown, authorisation
                |
     +----------+-----------+
     |                      |
 Targeting               Weapon launcher
     |                      |
 IMavTargetTrackSource   Munition
     |                      |
 MavTargetTrackData    IMavDamageReceiver
     ^                      |
     |                      v
  Sensors               Damage receivers
     |
     +-------> HUD / telemetry (read-only consumers)
```

Rules this expresses:

- Input **requests**; it never simulates.
- Sensors **produce** tracks; they never own weapons.
- Weapons **consume** tracks and **produce** damage; they never search the scene and never touch the
  airframe's Rigidbody.
- HUD and telemetry **read**; they own nothing.
- Aircraft physics is not in this diagram at all, and that is the point.
