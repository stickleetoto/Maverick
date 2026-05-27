# MAVERICK Fresh v0.12 — WT Elevator + CAS Ballistics

## Direction

This patch improves two things:

```text
1. W/S controls feel more like War Thunder elevator override.
2. CAS becomes more realistic with sim-lite ballistic projectiles and CCIP-lite prediction.
```

This is still a game system, not a real fire-control/weapon model.

---

## 1. W/S War Thunder-like elevator improvement

### Problem

W/S was stronger than before, but it still felt like the mouse instructor was fighting it.

### Change

New fields in `MavMouseFlightJet`:

```text
useWTKeyboardElevatorOverride = true
keyboardElevatorMouseBlend = 0.10
keyboardElevatorResponse = 18
keyboardElevatorReleaseBlend = 5.5
keyboardElevatorRateDamping = 0.10
keyboardElevatorUsesGLimit = true
```

Updated values:

```text
pitchUpCommand = -2.05
pitchDownCommand = 2.05
manualPitchBoost = 1.78
```

### Result

When W/S is held:
- W/S dominates pitch.
- Mouse pitch is mostly suppressed but not fully disconnected.
- Pitch damping prevents twitching.
- G-limiter still prevents extreme pull.

When W/S is released:
- Mouse aim gradually retakes pitch control.

---

## 2. CAS realism upgrade

### New scripts

```text
MavCASBallisticProjectile.cs
MavCASCCIPPredictor.cs
```

### Previous CAS

```text
Rockets/Bombs = instant damage near aim point
```

### New CAS

```text
Gun = fast projectile
Rocket = projectile with travel time and drop
TrainingBomb = dropped object with aircraft velocity and gravity
PrecisionStrike = still abstract designated strike for now
```

### CCIP-lite

`MavCASCCIPPredictor` predicts approximate impact points for:

```text
Gun
Rocket
Bomb
```

It uses step simulation and raycasts against scene colliders.

This gives a more believable CAS loop:
- diving matters
- distance matters
- release timing matters
- ground collider matters
- aircraft velocity affects bomb impact

---

## Controls

```text
F = designate target/ground point
Tab = cycle target
1/2 = previous/next weapon
Mouse0 = gun
Space = selected secondary weapon
```

---

## Tuning

### W/S too weak

```text
pitchUpCommand = -2.30
pitchDownCommand = 2.30
manualPitchBoost = 2.0
keyboardElevatorMouseBlend = 0.05
```

### W/S too violent

```text
pitchUpCommand = -1.65
pitchDownCommand = 1.65
manualPitchBoost = 1.45
keyboardElevatorRateDamping = 0.16
```

### CAS projectile too fast/slow

```text
MavCASWeaponSystem.gunMuzzleSpeed
MavCASWeaponSystem.rocketMuzzleSpeed
MavCASWeaponSystem.bombReleaseForwardFactor
```

### Bombs miss too far forward

```text
bombReleaseForwardFactor = 0.85
```

### Bombs fall too short

```text
bombReleaseForwardFactor = 1.15
```

### More arcade rockets

```text
rocketMuzzleSpeed = 700
projectileGravity = 6
```

### More realistic-feeling drop

```text
projectileGravity = 9.81
rocketMuzzleSpeed = 450
```

---

## Next CAS step

v0.13 should add:
- real HUD CCIP marker drawing
- target score/mission system
- rocket spread
- bomb arming delay
- CAS data logging for Physical AI
