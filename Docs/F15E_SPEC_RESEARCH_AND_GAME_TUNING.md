# MAVERICK Fresh v0.5 — F-15E Spec Research & Game Tuning

## Public reference points used

Sources:
- U.S. Air Force F-15E Strike Eagle fact sheet
- Boeing F-15EX official specifications page

Public high-level F-15E/F-15-family characteristics used for tuning:
- F-15E is a dual-role fighter for air-to-air and air-to-ground missions.
- F-15E dimensions: wingspan about 42.8 ft / 13 m, length about 63.8 ft / 19.44 m, height about 18.5 ft / 5.6 m.
- F-15E weight reference: 37,500 lb empty-ish listed weight, 81,000 lb maximum takeoff weight.
- F-15E thrust reference: two F100 engines, 25,000–29,000 lbf each.
- F-15E speed reference: Mach 2.5+ public high-level maximum speed.
- F-15E ceiling reference: about 60,000 ft.
- F-15EX official specs have the same broad size class and 81,000 lb MTOW, with 29,500 lb payload.

## Important design choice

This is not a real flight model.

We are using public high-level dimensions/performance to shape a game feel:
- heavy twin-engine fighter
- strong acceleration
- less twitchy pitch
- strong roll authority
- high-speed energy feeling
- mouse-aim friendly control

## New file

```text
MavF15EFlightSpecProfile.cs
```

This component stores public reference values and converts them into Fresh branch game tuning.

## Main gameplay tuning

```text
Gameplay mass = 14500 kg
Start altitude = 1200 m
Start speed = 240 m/s
Target cruise speed = 240 m/s
High combat speed = 390 m/s
Emergency/overspeed = 470 m/s
```

## Fresh branch tuning

```text
thrust = 175
turnTorque = 58, 23, 94
pitchGain = 0.48
yawGain = 0.74
rollGain = 0.98
maxAutoPitch = 0.34
noseDownTrim = 0.16
inputSmoothing = 8.8
```

## Why pitch is softer

The previous prototype kept pitching up too aggressively. F-15-like gameplay should feel powerful, but not like a paper airplane. The new profile:
- reduces pitch authority
- increases damping
- increases roll importance
- adds nose-down trim
- adds speed assist

## If it still climbs too much

Try:
```text
MavF15EFlightSpecProfile.tunedNoseDownTrim = 0.18
MavF15EFlightSpecProfile.tunedPitchGain = 0.42
MavF15EFlightSpecProfile.tunedMaxAutoPitch = 0.28
```

Then right-click the component and run:
```text
Apply F-15E Inspired Profile
```
