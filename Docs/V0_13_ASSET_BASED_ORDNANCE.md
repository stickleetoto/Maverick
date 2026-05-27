# MAVERICK Fresh v0.13 — Asset-Based Ordnance

## Goal

The user will add bomb, bullet, rocket, and missile assets.  
v0.13 lets CAS weapons spawn those assigned prefabs instead of only primitive placeholders.

## New / changed scripts

```text
Scripts/CAS/MavCASOrdnanceAssets.cs
Scripts/CAS/MavCASBallisticProjectile.cs
Scripts/CAS/MavCASWeaponSystem.cs
Scripts/CAS/MavCASStarterBootstrap.cs
```

## New component

`MavCASOrdnanceAssets`

This is automatically added to `F15E_Player` by `MavCASStarterBootstrap`.

Assign your imported prefabs here:

```text
Gun Shell Prefab
Rocket Prefab
Bomb Prefab
Missile Prefab
```

Optional spawn points:

```text
Gun Muzzle
Left Rocket Rail
Right Rocket Rail
Bomb Pylon
Missile Rail
```

If no prefab is assigned, the system falls back to primitive placeholder objects.

## Recommended prefab setup

Each prefab can be simple:

```text
Prefab Root
- Mesh / visual children
- Optional Collider
- Optional MavCASBallisticProjectile
```

If the prefab does not already have `MavCASBallisticProjectile`, it will be added automatically at runtime.

Recommended:
- Forward direction should be prefab +Z.
- Collider can be trigger.
- Keep Rigidbody off the prefab unless you intentionally want it. The script simulates velocity itself.
- Use reasonable scale. The script does not rescale assigned prefabs by default.

## Weapon behavior

### Gun

```text
Mouse0 -> spawns GunShell prefab
```

Uses:
```text
gunMuzzleSpeed
gunShellLife
gunDamage
```

### Rocket

```text
Space with Rockets selected -> spawns Rocket prefab
```

Uses:
```text
rocketMuzzleSpeed
rocketLife
rocketDamage
rocketRadius
```

### TrainingBomb

```text
Space with TrainingBomb selected -> spawns Bomb prefab
```

Uses:
```text
aircraft velocity
gravity
bombDamage
bombRadius
bombReleaseForwardFactor
```

### Missile

New weapon:

```text
Missile
```

It uses the `Missile Prefab` and simple guidance to target/designated point.

Requires:
```text
F or Tab designation first
```

Uses:
```text
missileMuzzleSpeed
missileGuidanceDelay
missileGuidanceStrength
missileMaxTurnRateDeg
missileMotorAcceleration
missileMaxSpeed
missileDamage
missileRadius
```

## Controls

```text
F = designate target or ground point
Tab = cycle target
1 / 2 = previous / next weapon
Mouse0 = gun
Space = fire selected secondary weapon
```

Weapon list:

```text
Gun
Rockets
TrainingBomb
PrecisionStrike
Missile
```

## How to connect assets in Unity

1. Import your asset files into Unity.
2. Make each ordnance asset into a prefab.
3. Select `F15E_Player`.
4. Find `MavCASOrdnanceAssets`.
5. Drag prefabs into:
   - `Gun Shell Prefab`
   - `Rocket Prefab`
   - `Bomb Prefab`
   - `Missile Prefab`
6. Optionally create child empty objects under F15E:
   - `GunMuzzle`
   - `LeftRocketRail`
   - `RightRocketRail`
   - `BombPylon`
   - `MissileRail`
7. Drag those transforms into the spawn point fields.

## Suggested child object placement

Approximate setup:

```text
F15E_Player
- GunMuzzle      near nose / gun position
- LeftRocketRail under left wing
- RightRocketRail under right wing
- BombPylon      under centerline or wing
- MissileRail    under wing/fuselage
```

## Troubleshooting

### Asset appears sideways

Rotate the prefab so its forward direction is +Z.

### Asset too huge/small

Fix prefab scale in Unity. Assigned prefabs are not automatically rescaled.

### Missile flies dumb / does not guide

Check:
```text
MavCASWeaponSystem.selectedWeapon = Missile
F or Tab designated target exists
Missile Prefab assigned
```

### Projectile hits aircraft immediately

Make projectile prefab collider trigger, or place spawn points farther away from the aircraft mesh.

### No prefab appears

If no prefab is assigned, fallback primitive appears.  
If nothing appears at all, check `MavCASWeaponSystem` ammo and selected weapon.

## Design note

This is still sim-lite, not real weapons modeling.  
The goal is:
- asset-based visuals
- travel time
- gravity/drop
- simple missile guidance
- damage radius gameplay
