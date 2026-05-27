# MAVERICK Fresh v0.7 — Stability & Camera Polish

## User feedback

- Roll is too fast.
- Camera should be closer.
- Rendering distance should be longer.
- The aircraft needs basic attitude recovery.
- Even after wild movement, it should naturally recover its attitude.

## Changes

### Roll slowed down

```text
turnTorque.z: 142 -> 104
rollGain: 1.42 -> 1.08
bestTurnRollBoost: 1.34 -> 1.12
manualRollBoost: 1.25 -> 1.05
maxAngularVelocity: 4.2 -> 3.45
angularDamping: 1.42 -> 1.72
```

### Camera closer

```text
cameraLocalPosition:
old: 0, 7.0, -28
new: 0, 5.4, -16.5
```

### Render distance increased

```text
cameraFarClip = 24000
aimDistance = 850
```

### Attitude stabilizer added

New fields in `MavMouseFlightJet`:

```text
attitudeStabilizer = true
rateDampingStrength = 0.115
rollLevelStrength = 0.42
pitchRecoveryStrength = 0.10
pilotInputSuppress = 0.85
maxStabilizerTorque = 135
```

This adds:
- angular velocity damping
- natural wings-level recovery
- gentle pitch recovery
- pilot input suppression so A/D and W/S still matter

## If roll is still too fast

```text
turnTorque.z = 88
rollGain = 0.9
bestTurnRollBoost = 1.0
maxAngularVelocity = 3.0
```

## If recovery is too weak

```text
rateDampingStrength = 0.15
rollLevelStrength = 0.55
maxStabilizerTorque = 180
```

## If recovery fights the player

```text
rollLevelStrength = 0.28
pilotInputSuppress = 1.0
pitchRecoveryStrength = 0.05
```

## If camera is too close

```text
cameraLocalPosition = 0, 6.0, -20
```
