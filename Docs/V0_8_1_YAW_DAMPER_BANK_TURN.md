# MAVERICK Fresh v0.8.1 — Yaw Damper / Bank-Turn Patch

## Problem

The jet still rotated left/right too much while moving.  
This felt like flat yaw/sideways spinning instead of War-Thunder-like bank turning.

## Fix

v0.8.1 reduces yaw authority and adds a yaw damper.

## Main changes

```text
Yaw Torque Y: 34 -> 12
Yaw Gain: 0.96 -> 0.22
Keyboard Yaw Authority: 0.62 -> 0.32
Max Auto Yaw Command = 0.22
Yaw Damper = On
Yaw Rate Damping Strength = 0.36
Side Slip Damping Strength = 0.070
Prefer Bank Turn Over Yaw = On
```

## What this means

Before:
```text
Mouse left/right -> yaw + roll
```

Now:
```text
Mouse left/right -> mostly roll/bank-turn
Q/E -> weak manual rudder
Yaw damper -> stops side-to-side spinning
```

## If it still rotates left/right too much

On `F15E_Player > MavMouseFlightJet`:

```text
Yaw Rate Damping Strength = 0.50
Max Auto Yaw Command = 0.12
Yaw Gain = 0.12
Turn Torque Y = 6
Side Slip Damping Strength = 0.10
```

## If yaw feels too locked

```text
Yaw Rate Damping Strength = 0.22
Max Auto Yaw Command = 0.32
Yaw Gain = 0.35
Turn Torque Y = 18
Keyboard Yaw Authority = 0.45
```

## Design intent

F-15/War Thunder-like mouse aim should feel like:

```text
roll -> pitch -> turn
```

not:

```text
flat yaw spin left/right
```
