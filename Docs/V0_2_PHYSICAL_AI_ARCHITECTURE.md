# Eagle Physical AI Lab v0.2 Architecture

## Goal

v0.2 turns the prototype from a flight/CAS script pack into a minimal physical-AI learning loop.

```text
Unity scene state
→ PhysicalAIObservationBuilder
→ 32D observation vector
→ Policy
→ PhysicalAIAction
→ PhysicalActionValidator
→ AircraftPhysicsController / AbstractStrikeSystem
→ Reward + logs
→ Demonstration dataset
→ Behavior cloning policy update
```

## Core contracts

### Observation

`PhysicalAIObservation` is the stable input interface. Keep it backward-compatible when possible.

### Action

`PhysicalAIAction` has six outputs:

```text
pitch, roll, yaw, throttle, strike, abort
```

The policy should not directly bypass `PhysicalActionValidator`.

### Reward

`PhysicalAIRewardTracker` produces shaped game reward. It is not yet a full RL trainer, but it makes the simulator ready for ML-Agents, offline analysis, or custom RL later.

## Control modes

```text
Manual        Human controls aircraft and demonstrations are recorded.
RulePilot     Legacy rule CAS pilot controls aircraft.
LinearPolicy  BehaviorCloningLinearPolicy controls aircraft through validator.
ShadowPolicy  Policy predicts actions, but human still controls aircraft.
```

## Safety design

The learned policy is intentionally weakly trusted.

```text
policy output
→ clamp ranges
→ stall/altitude recovery override
→ strike gate through CasValidator
→ aircraft control
```

## Dataset policy

Record lots of data, but initially train only on:

```text
good_run
interesting_run if reviewed
```

Keep these separate:

```text
bad_run
crash_run
friendly_fire_risk
```

They are useful later for negative examples, classifiers, and validator tuning.
