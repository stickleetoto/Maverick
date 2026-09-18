# Legacy Weapon / Combat Inventory — R0

Inventory of every combat-related component in the active project, produced for Weapon System
Separation R0.

- Base: `ed778000f528eb8ebd2ff53a45ea59216e858267`
- Branch: `sol/weapon-system-separation-r0`
- Method: script GUIDs matched against every `.unity`, `.prefab` and `.asset` in `Assets/`; a
  cross-file type-reference graph over all 296 project scripts; `AddComponent<T>` and
  `RuntimeInitializeOnLoadMethod` sites traced to find what is actually installed at runtime.
  Filenames were used to find candidates, never to decide ownership.

## 1. The finding that shapes everything else

**The project contains two complete, parallel combat implementations, and only one of them runs.**

| | Live line | Dormant line |
|---|---|---|
| Root | `Assets/MaverickFresh/Scripts/` | `Assets/Maverick/Scripts/` |
| Serialized in any scene/prefab | yes | **no — zero GUID references** |
| Auto-installed at runtime | yes, via `MavInGameBootstrap` → `MavCASStarterBootstrap` | **no `RuntimeInitializeOnLoadMethod` anywhere** |
| Reachable how | bootstrap chain from the in-game scene | only from other dormant bootstraps |
| Compiles | yes | yes |

Of 296 scripts in the project, **23 are serialized into a scene, prefab or asset**, and not one of
them is under `Assets/Maverick/`. The dormant line's components are `AddComponent`-ed only by other
dormant bootstraps (`MaverickV09Bootstrap`, `EagleV05IntegratedBootstrap`, `MaverickAirCombatOnlyModeV13`
and similar), none of which is itself installed or serialized. It is dead weight that still compiles:
it cannot affect gameplay, but it can and does mislead a reader into thinking `F15ERadarSystem` or
`TargetingPodSystem` is the radar to build on.

That distinction drives the classifications below. The live line needs separation. The dormant line
needs deletion, later, once nothing references it.

## 2. Live combat components

Wired at runtime; these are what actually run. "Serialized" means a scene or prefab stores a direct
reference to the script.

| Path (`Assets/MaverickFresh/Scripts/`) | Component | Responsibility | Serialized in | Consumers | Runtime authority | Class | Risk |
|---|---|---|---|---|---|---|---|
| `CAS/MavCASWeaponSystem.cs` (1053) | `MavCASWeaponSystem` | Everything about firing: reads keys, selects weapon, runs the gun hitscan, spawns projectiles, applies damage, holds ammunition, drives tracer/muzzle VFX, applies gun recoil to the Rigidbody | — (runtime) | `MavFreshHud`, CAS bootstrap, 13 files | **Authoritative** for fire, ammo, gun damage | LEGACY | **HIGH** — single 1000-line owner of six responsibilities |
| `CAS/MavCASTargetingSystem.cs` (228) | `MavCASTargetingSystem` | Ground target designation: reads keys, raycasts, scans `FindObjectsOfType<MavCASTarget>` | `Maverick/Prefabs/Aircraft/F15E_Player.prefab` | `MavFreshHud`, weapon system | **Authoritative** for designated ground target | LEGACY | MEDIUM — serialized on a prefab |
| `CAS/MavCASCCIPPredictor.cs` (133) | `MavCASCCIPPredictor` | Continuously-computed impact point for unguided ordnance | — | HUD, weapon system | advisory | LEGACY | LOW |
| `CAS/MavCASBallisticProjectile.cs` (290) | `MavCASBallisticProjectile` | Projectile flight and impact, applies damage on hit | — | weapon system | **Authoritative** for projectile damage | LEGACY | MEDIUM |
| `CAS/MavCASTarget.cs` (110) | `MavCASTarget` | Ground target health, armor, destruction | — | 6 files | **Authoritative** for ground target health | **SEPARATE** — now implements `IMavDamageReceiver` | LOW |
| `CAS/MavCASOrdnanceAssets.cs` (120) | `MavCASOrdnanceAssets` | Prefab/material lookup for ordnance visuals | — | weapon system | asset provider | LEGACY | LOW |
| `CAS/MavCASStarterBootstrap.cs` (116) | `MavCASStarterBootstrap` | Runtime wiring of the whole CAS stack onto the player | — | `MavInGameBootstrap` | composition root | **SEPARATE** — this is the seam | LOW |
| `CAS/MavCASTestRangeSpawner.cs` (116) | `MavCASTestRangeSpawner` | Spawns practice ground targets | `Mav_InGame.unity` | bootstrap | scenario | LEGACY | LOW |
| `CAS/MavTargetingPodSystem.cs` (706) | `MavTargetingPodSystem` | TGP camera, zoom, slew, lock; reads its own keys | — | HUD, TGP state, 9 files | **Authoritative** for TGP lock | LEGACY | MEDIUM |
| `CAS/MavTGPStateManager.cs` (141) | `MavTGPStateManager` | TGP display mode state machine | — | bootstrap, HUD | display state | LEGACY | LOW |
| `CAS/MavNeonTracer.cs` (137) | `MavNeonTracer` | Gun tracer presentation | — | weapon system | presentation | LEGACY | LOW |
| `Weapons/MavGunTracerImpactVfx.cs` (215) | `MavGunTracerImpactVfx` | Impact sparks/VFX | `Mav_InGame.unity` | weapon system | presentation | LEGACY | LOW |
| `Weapons/MavAircraftDamageState.cs` (183) | `MavAircraftDamageState` | Aircraft health, kill, wreck handling | — | weapon system (was: by reflection) | **Authoritative** for aircraft health | **SEPARATE** — now implements `IMavDamageReceiver` | LOW |
| `Sensors/MavF22SensorSuite.cs` (252) | `MavF22SensorSuite` | Radar/IRST scan, TWS/STT modes, lock timing, target cycling; reads its own keys | `Mav_InGame.unity` | HUD overlay, 5 files | **Authoritative** for air target lock | LEGACY | **HIGH** — closest existing thing to a radar; must not become the new radar's base |
| `Sensors/MavRadarSignature.cs` (44) | `MavRadarSignature` | Per-object identity: display name, team, air/ground, RCS, IR signature, destroyed flag | `Mav_InGame.unity` | 8 files | target identity marker | **SEPARATE** — proto-track identity | LOW |
| `Sensors/MavSensorHudOverlay.cs` (69) | `MavSensorHudOverlay` | Sensor HUD drawing | — | bootstrap | presentation | LEGACY | LOW |
| `HUD/MavAirToAirLeadSight.cs` (348) | `MavAirToAirLeadSight` | Air-to-air lead pip; finds targets with `FindObjectsOfType<MavRadarSignature>` | `Mav_InGame.unity` | — | presentation | LEGACY | LOW |
| `MavFreshHud.cs` (527) | `MavFreshHud` | Main HUD. Reads weapon/targeting/TGP state; discovers them with `FindObjectOfType` | `Mav_InGame.unity` | 5 files | **reads only** | KEEP, decouple discovery | MEDIUM |
| `MavFreshInput.cs` (188) | `MavFreshInput` | Static keyboard/mouse facade | — | 18 files | input source | KEEP | LOW |
| `AI/MavEnemyF15Spawner.cs`, `AI/MavEnemyAircraftPilot.cs`, `AI/MavEnemyLBMBrain.cs` | enemy spawn + pilot AI | Spawns and flies opposition | — | bootstrap | scenario/AI | KEEP — not weapons | LOW |
| `MavAirTargetSpawner.cs`, `MavAirTargetDrone.cs`, `PhysicalAI/MavAITargetDrone.cs` | air target drones | Practice targets | — | bootstrap | scenario | LEGACY | LOW |

### Serialized-reference summary

Six combat scripts are referenced by a scene or prefab and therefore carry GUID risk if moved:
`MavCASTargetingSystem` (F15E_Player.prefab), `MavCASTestRangeSpawner`, `MavAirToAirLeadSight`,
`MavF22SensorSuite`, `MavRadarSignature`, `MavGunTracerImpactVfx` (all `Mav_InGame.unity`), plus
`MavFreshHud`. **None was moved in R0.** Every one stays physically in place; separation is by
contract and namespace instead. See §5.

## 3. Dormant combat components — `Assets/Maverick/Scripts/`

None of these is serialized anywhere or installed at runtime. They are classified `REMOVE_LATER`:
recoverable in git, deletable once the new architecture exists and nothing references them.

| Area | Files | Notes |
|---|---|---|
| `CAS/` | `AbstractStrikeSystem`, `MaverickWeaponSelector`, `MaverickWTWeaponSelectorV11`, `CasValidator`, `CasValidationResult` | An earlier strike/weapon-selection design, superseded by `MavCASWeaponSystem` |
| `Sensors/Radar/` | `F15ERadarSystem` (392), `RadarSignature`, `SensorContact`, `RadarSignatureAutoBinder`, `MaverickWTRadarHotasV11`, `F15ERadarMode` | A second, older radar attempt. **Not** the basis for Radar Core |
| `Sensors/TargetingPod/` | `TargetingPodSystem` (337), `TargetingPodMode`, `TargetingPodCameraAutoSetup`, `MaverickWTTargetingPodHotasV11` | Superseded by `MavTargetingPodSystem` |
| `Sensors/Fusion/` | `SensorFusionManager`, `SensorAidedCasValidator`, `SensorLogRecorder`, `SensorTelemetryFrame` | Track-fusion sketch; interesting as prior art only |
| `Sensors/UI/` | `RadarHud`, `TargetingPodHud`, `SensorCrosshairOverlay` | Old sensor HUDs |
| `Battlefield/` | `CasRequest`, `CasRequestManager`, `CasTargetDesignator`, `GroundBattleDirector`, `GroundUnit` | Ground-war layer |
| `Scenario/` | `GroundThreatZone`, `NoStrikeZone`, `MaverickAirTargetDroneV13`, `MaverickAirCombatTestRangeV13`, `ProceduralTestRangeBuilder`, `MaverickRunwayAndRangeBuilder` | Range/scenario builders |
| `AI/`, `SensorAI/` | `RuleCasPilot` + 10 `SensorAI*` files | Rule and learned pilots driving the dormant sensor stack |
| `UI/` | `MaverickWTHudV11`, `MaverickMouseFlightHudV12`, `MissionAndSafetyHUD`, others | Old HUDs |

There is genuine prior art here — `SensorContact` and `SensorFusionManager` in particular anticipate
a track abstraction. Read them for ideas; do not build on them.

## 4. Two separate identity concepts, no track

The live line has **two unrelated notions of "a thing worth shooting"**:

- `MavCASTarget` — ground targets. Health, armor, destruction. Found by `FindObjectsOfType`.
- `MavRadarSignature` — air targets. Name, team, RCS, IR. Found by `FindObjectsOfType`.

Nothing relates them, nothing ages them, and every consumer re-derives its own view by scanning the
scene. `MavF22SensorSuite`, `MavCASTargetingSystem` and `MavAirToAirLeadSight` each run their own
`FindObjectsOfType` sweep and each reaches a different answer about what is being looked at.

This is the specific gap `MavTargetTrackData` (added in R0, `Combat/Core`) is shaped to close. It is
a data contract only — nothing in R0 detects, correlates or tracks anything.

## 5. What R0 changed, and what it deliberately did not

Changed:

- Added `Assets/MaverickFresh/Scripts/Combat/Core/` with three contracts: `IMavDamageReceiver` +
  `MavDamageInfo`, `MavTargetTrackData` + `IMavTargetTrackSource`, `IMavCombatCommandSource` +
  `MavCombatCommand`.
- `MavCASTarget` and `MavAircraftDamageState` implement `IMavDamageReceiver` explicitly, delegating
  to the methods they already had.
- `MavCASWeaponSystem` damages through the contract first, keeping its reflection probe as a
  documented fallback.
- Added `Combat/Editor/MavCombatBoundaryScan.cs`, a static architecture scan.

Deliberately not changed:

- **No file moved.** Six combat scripts are serialized into a scene or prefab; moving them buys
  tidiness and risks reference breakage, which is the wrong trade in a separation phase.
- **No weapon behavior touched.** No damage value, spread, cooldown, ammunition count, lock time or
  effect was altered.
- **No deletion.** The dormant line is fully recoverable and still compiles.
- **No radar, missile, seeker, guidance or AIM-120/AIM-9 anything.**
- **The FDM validation manifest was not touched.** The new scan lives under `Scripts/Combat/Editor`
  rather than `Scripts/Editor`, because the latter is a declared authority root of the FDM baseline
  and a file there would have forced a manifest change.
