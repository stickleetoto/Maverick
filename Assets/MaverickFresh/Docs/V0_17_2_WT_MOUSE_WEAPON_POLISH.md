# MAVERICK Fresh v0.17.2 WT Mouse Weapon Polish

## Fixed gun muzzle direction

- Gun shell origin uses `MavCASOrdnanceAssets.gunMuzzle` when assigned.
- With `gunUsesFixedMuzzleDirection = true`, gun shell direction uses `gunMuzzle.forward`.
- If no muzzle is assigned, gun shells spawn from a safe aircraft-relative nose position and fire along `transform.forward`.
- `gunUseConvergence` is optional and defaults to false.
- When convergence is enabled and a muzzle exists, gun shells point from the muzzle toward:
  `transform.position + transform.forward * gunConvergenceDistance`.

## Mouse authority tuning

Balanced/F5 and default values:
- `pitchGain = 0.62`
- `yawGain = 0.18`
- `maxAutoPitch = 0.50`
- `inputSmoothing = 8.8`
- `mousePitchDeadzoneY = 0.045`
- `centerPitchLevelStrength = 0.16`
- `noseHighPitchDownAssist = 0.20`
- `maxScreenRollBankAngle = 62`
- `bankHoldProportional = 0.038`
- `bankHoldRollRateDamping = 0.24`

Smooth/F6 values:
- `pitchGain = 0.52`
- `yawGain = 0.14`
- `maxAutoPitch = 0.42`
- `inputSmoothing = 11.5`
- `mousePitchDeadzoneY = 0.060`
- `centerPitchLevelStrength = 0.22`
- `noseHighPitchDownAssist = 0.24`
- `maxScreenRollBankAngle = 52`
- `bankHoldProportional = 0.030`
- `bankHoldRollRateDamping = 0.30`

Aggressive/F7 values:
- `pitchGain = 0.76`
- `yawGain = 0.22`
- `maxAutoPitch = 0.60`
- `inputSmoothing = 7.2`
- `mousePitchDeadzoneY = 0.035`
- `centerPitchLevelStrength = 0.13`
- `noseHighPitchDownAssist = 0.16`
- `maxScreenRollBankAngle = 70`
- `bankHoldProportional = 0.045`
- `bankHoldRollRateDamping = 0.21`

## WT-like weapon controls

- `Mouse0` fires the primary weapon: gun.
- `Space` fires the selected secondary weapon.
- `Alpha1` and `Alpha2` cycle secondary weapons only.
- Secondary cycle order: Rockets, TrainingBomb, Missile, PrecisionStrike.
- `Alpha3` quick-selects Missile.
- `Alpha4` quick-selects TrainingBomb.
- Missile still requires CAS/TGP designation and reports `missile_no_designation` when none exists.

## Manual Unity setup

- Create or assign `F15E_Player/GunMuzzle`.
- Rotate `GunMuzzle` so its local forward direction points exactly where bullets should travel.
- Assign `GunMuzzle` to `MavCASOrdnanceAssets.gunMuzzle`.
- Keep `GunMuzzle` slightly outside the aircraft collider to avoid immediate self-hit.
- Leave `MavCASWeaponSystem.gunUsesFixedMuzzleDirection` enabled for WT-like fixed guns.

## Tuning guide

- If mouse still feels weak: raise `pitchGain` slightly or lower `inputSmoothing`.
- If mouse yanks too hard: lower `maxAutoPitch`, raise `inputSmoothing`, or use F6 Smooth.
- If center mouse drifts or climbs: raise `mousePitchDeadzoneY` or `centerPitchLevelStrength`.
- If gun fires in the wrong direction: rotate `GunMuzzle`; its local forward axis controls bullet direction.
- If missile does not fire: designate a target or point with CAS/TGP first.
- If bomb/rocket selection feels wrong: check the HUD `SECONDARY` field and use `1/2`, `3`, or `4` to select.
