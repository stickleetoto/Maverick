# MAVERICK Fresh v0.14 — Targeting Pod

## Goal

Add a targeting pod system for CAS gameplay.

The TGP connects to:
- `MavCASTargetingSystem`
- `MavCASWeaponSystem`
- precision strike
- guided missile
- ground point designation

This is a sim-lite gameplay targeting pod, not a real military sensor model.

## New script

```text
Scripts/CAS/MavTargetingPodSystem.cs
```

`MavCASStarterBootstrap` now installs it automatically when CAS starter is enabled.

## Controls

```text
T = toggle TGP picture-in-picture on/off
O = cycle TGP display mode: PIP -> Fullscreen -> Off
I/J/K/L = slew pod camera
LeftShift + I/J/K/L = fine slew
5 = zoom in
6 = zoom out
+ / - = zoom in/out alternative
R = lock/unlock target or ground point
G = push TGP lock/look point to CAS designation
U = recenter pod to boresight
```

## How to use

### Basic target designation

```text
1. Press T to show TGP.
2. Slew with I/J/K/L.
3. Put crosshair on a ground target.
4. Press R to lock.
5. Press G to send designation to CAS.
6. Select Missile or PrecisionStrike.
7. Press Space to fire.
```

### Ground point designation

```text
1. Aim TGP at ground.
2. Press R to point lock.
3. Press G to designate point.
4. Fire PrecisionStrike / Missile / later bombs.
```

## Display modes

```text
Off
PictureInPicture
Fullscreen
```

Default is PictureInPicture.

## Inspector fields

`MavTargetingPodSystem`:

```text
Pod Mount
Pod Camera
Pod Texture
Display Mode
FOV / Min FOV / Max FOV
Slew Speed Deg
Max Yaw
Min Pitch / Max Pitch
Max Range
Target Snap Radius Screen
```

If no Pod Mount is assigned, the script creates:

```text
F15E_Player/MavTGP_Mount
```

Default local position:

```text
0.7, -2.0, 3.5
```

You can move this to match your F-15E targeting pod asset/location.

## Recommended setup with a TGP asset

If you add a visual targeting pod model:

```text
F15E_Player
- MavTGP_Mount
  - YourTGPModel
```

Then assign `MavTGP_Mount` to:

```text
MavTargetingPodSystem > Pod Mount
```

## Integration with CAS

The TGP writes to `MavCASTargetingSystem` when you press `G`:

```text
designatedTarget
designatedPoint
hasDesignatedPoint
status
```

So existing weapons can use it.

Current good pairings:
- `Missile`
- `PrecisionStrike`

Future pairings:
- laser-guided bomb
- CCRP
- JDAM-like point strike
- AI CAS training targets

## Troubleshooting

### TGP screen is black

Check:
```text
MavTargetingPodSystem > Pod Camera
MavTargetingPodSystem > Pod Texture
Camera far clip / Max Range
```

Usually it auto-creates both.

### TGP points wrong way

Move or rotate `MavTGP_Mount`, or press `U` to recenter.

### It does not designate

Use:
```text
R = lock
G = push to CAS
```

Then check HUD:
```text
TGT ...
```

### It locks wrong target

Decrease:
```text
Target Snap Radius Screen
```

### It is too sensitive

Decrease:
```text
Slew Speed Deg
```

or use:
```text
LeftShift + I/J/K/L
```

## Next step

v0.15 should add:
- proper TGP polarity modes: CCD/WHOT/BHOT fake filters
- laser code / laser on/off
- laser-guided bomb
- TGP-to-CCIP/CCRP workflow
- CAS mission scoring
