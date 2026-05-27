# MAVERICK Fresh v0.16 — Physical AI Core

## Goal

This version returns to the original goal: **Physical AI**.

Not full reinforcement learning yet.  
This is the core closed-loop physical AI layer:

```text
sense physics state
decide desired aim/throttle/fire
drive the same aircraft controller as the player
log reward-like signals
create target drone scenario
prepare for imitation/RL later
```

## New scripts

```text
Scripts/PhysicalAI/MavPhysicalAIController.cs
Scripts/PhysicalAI/MavPhysicalAIRewardLogger.cs
Scripts/PhysicalAI/MavAITargetDrone.cs
Scripts/PhysicalAI/MavPhysicalAIScenarioBootstrap.cs
```

Existing PhysicalAI tools are kept:
```text
MavFlightDataRecorder
MavSimpleAIPilot
MavGhostTrailRecorder
MavPhysicalAIStarterBootstrap
```

## Controls

```text
F11 = Physical AI on/off
F3  = cycle AI mode
F4  = reward log on/off
F9  = flight CSV recorder on/off
```

Existing:
```text
F5/F6/F7 = WT feel presets
F8 = direct debug
H = help overlay
```

## AI modes

```text
Player
AirIntercept
CASAttack
Orbit
Recovery
```

### AirIntercept

The AI finds `AirTarget_AI_Drone`, predicts lead, and points the existing mouse flight rig toward the predicted intercept point.

It still uses the same physical aircraft and instructor system as the player.

### CASAttack

The AI finds a `MavCASTarget`, designates it through CAS targeting, approaches, dives, and optionally fires.

By default:
```text
allowAutoFire = false
```

Turn it on only after the approach behavior is stable.

### Orbit

The AI flies a simple orbit route.

### Recovery

The AI pulls up / throttles up when:
- altitude is too low
- speed is too low near ground
- raycast detects terrain ahead

## Automatic setup

`MavFreshBootstrap` now has:

```text
Install Physical AI Starter = On
```

This installs the Physical AI components but the AI itself starts disabled:

```text
MavPhysicalAIController.aiEnabled = false
```

Press `F11` to let AI fly.

## Scenario

`MavPhysicalAIScenarioBootstrap` can create:

```text
AirTarget_AI_Drone
```

The drone moves in a circle/figure-eight pattern using `MavAITargetDrone`.

## Reward logging

`MavPhysicalAIRewardLogger` writes CSV logs to:

```text
Application.persistentDataPath/MaverickFresh/PhysicalAILogs
```

Log columns include:
- time
- mode
- AI state
- speed
- altitude
- target distance
- target angle
- ground distance
- pitch/roll/bank/G
- throttle
- reward
- total reward
- hits/kills
- last decision

This is a future RL dataset/reward design foundation.

## Recommended test order

```text
1. Import v0.16.
2. Press Play.
3. Confirm AirTarget_AI_Drone exists.
4. Press F5 for Balanced feel.
5. Press F11 to enable Physical AI.
6. Press F3 to cycle mode if needed.
7. Watch AirIntercept behavior.
8. Press F4 to record reward log.
9. Press F9 to record full flight CSV.
```

## For CAS AI test

```text
1. Build CAS range.
2. Select F15E_Player > MavPhysicalAIController.
3. Set Mode = CASAttack.
4. Set Allow Auto Fire = false first.
5. Press F11.
6. If approach is stable, enable Allow Auto Fire.
```

## Design note

This AI is **physical** because it does not teleport or directly set rotation.
It drives the same flight-control stack:

```text
PhysicalAIController
-> MavMouseFlightRig mouseAim direction
-> MavMouseFlightJet instructor
-> Rigidbody physics
```

So its behavior can be logged and later learned.

## Next step

v0.17 should add:
- replay imitation loader
- CSV to JSONL converter
- action label extraction
- stable episode reset
- score/reward dashboard
- optional ML-Agents adapter
