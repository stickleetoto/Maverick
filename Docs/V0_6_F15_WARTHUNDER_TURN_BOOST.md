# MAVERICK Fresh v0.6 — F-15 WarThunder-like Turn Boost

## Why

User feedback:
- W/S pitch authority is too weak.
- Turning feels too slow.
- Desired direction: closer to the way F-15 feels in War Thunder.

## Reference used

War Thunder public F-15E wiki values:
- F-15E has very high public in-game top speed and climb figures.
- Listed turn time is around 21 seconds depending on mode.
- The page notes that the F-15's best turning speed is around 800–900 km/h.

For gameplay tuning:
- 800 km/h ≈ 222 m/s
- 850 km/h ≈ 236 m/s
- 900 km/h ≈ 250 m/s

So v0.6 adds a best-turn band centered around 236 m/s.

## New feel changes

```text
W/S much stronger
Turn speed faster
Roll-driven turning stronger
Best turn band added
Manual pitch/roll boost added
Less overspeed braking
Higher angular velocity cap
Faster mouse-aim response
```

## Important new fields in MavMouseFlightJet

```text
bestTurnSpeed = 236
turnBandWidth = 95
bestTurnPitchBoost = 1.28
bestTurnRollBoost = 1.34
manualPitchBoost = 1.55
manualRollBoost = 1.25
pitchUpCommand = -1.75
pitchDownCommand = 1.75
```

## Main tuning defaults

```text
thrust = 190
turnTorque = 92, 34, 142
sensitivity = 4.55
aggressiveTurnAngle = 7.5
pitchGain = 0.82
yawGain = 0.98
rollGain = 1.42
maxAutoPitch = 0.66
noseDownTrim = 0.06
```

## If it is now too wild

Try:

```text
turnTorque = 78, 30, 118
sensitivity = 3.9
rollGain = 1.2
pitchGain = 0.68
manualPitchBoost = 1.25
manualRollBoost = 1.1
```

## If W/S are still weak

Try:

```text
turnTorque.x = 110
manualPitchBoost = 1.8
pitchUpCommand = -2.0
pitchDownCommand = 2.0
```

## If A/D turn is still slow

Try:

```text
turnTorque.z = 165
rollGain = 1.65
bestTurnRollBoost = 1.55
aggressiveTurnAngle = 6
```
