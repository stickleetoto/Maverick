# Maverick Turn Dynamics Phase 4A

## Purpose

The current player-visible turn behaviour still comes from the legacy `MavMouseFlightJet` + `MavAeroBody` stack while the replacement FlightDynamics FDM remains off by default. Phase 4A therefore fixes the most obvious ownership overlap in the path the player is actually flying before any deeper tuning is attempted.

## Problem found

`MavMouseFlightJet.ApplyVelocityTurnAssist` directly applies acceleration that rotates the velocity vector toward the aircraft nose. At the same time `MavAeroBody` applies aerodynamic lift and drag.

Before Phase 4A, the aero body returned:

```text
legacyVelocityAssistScale = 1 - velocityAssistFade * aeroBlend
```

For the built-in F-22 profile:

```text
aeroBlend          = 0.54
velocityAssistFade = 0.58
legacy scale       = 1 - 0.58 * 0.54 = 0.6868
```

That meant 54% aerodynamic ownership could coexist with about 68.7% of the direct velocity-turn assist. Those are two independent mechanisms curving the trajectory, so the combined behaviour can feel rail-like and can overstate turn response.

## Phase 4A rule

Turn ownership is now migrated one-for-one:

```text
legacyVelocityAssistScale = 1 - clamp01(aeroBlend)
```

At the same F-22 setting:

```text
aero ownership   = 54%
legacy ownership = 46%
sum              = 100%
```

At `aeroBlend = 1`, direct velocity-turn assist receives zero ownership. At `aeroBlend = 0`, legacy assist receives full ownership.

The serialized `velocityAssistFade` field is retained only for compatibility with existing scenes/profiles. It no longer weakens the one-for-one ownership rule while `MavAeroBody` is active.

## Diagnostics

`MavAeroBody` publishes:

- `debugAeroTurnOwnership`
- `debugLegacyVelocityAssistScale`
- `debugTurnOwnershipSum`

Unity menu:

```text
Maverick > Flight Dynamics > Run Phase 4A Turn Ownership Validation
Maverick > Flight Dynamics > Report Current Turn Dynamics
```

The live report includes speed, bank, AoA/AoS, estimated G, body rates, aero/legacy ownership split, current velocity-assist gate, lift/drag g, gravity blend and active torque mode.

## Deliberately not changed yet

Phase 4A does not blindly retune lift slope, gravity, inertia, rate-control gains, induced drag, TVC, stall limits or yaw coordination. Those should be changed from recorded turn traces rather than by feel alone.

The next trace should compare sustained turns at approximately 30, 45 and 60 degrees of bank while recording speed, altitude/vertical speed, bank, AoA, AoS, G, p/q/r, aero lift/drag and remaining legacy assist.
