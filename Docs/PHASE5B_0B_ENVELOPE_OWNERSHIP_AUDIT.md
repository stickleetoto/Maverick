# Phase 5B.0b — Envelope Ownership Checkpoint

Branch: `sol/phase5-wip`  
Base: `sol/phase4a-turn-dynamics`  
WIP handoff commit: `428491692993a1ef82759ac3c50f3dfa1f2ca611`

## Status

The ownership correction for the F-16 envelope-protection values is structurally complete enough to stop editing and move to runtime validation.

Authoritative runtime consumer:

```text
MavAircraftRuntimeProfile
        ↓ authoritative apply
MavInstructorController
        ↓ one-way compatibility/diagnostic mirror
MavMouseFlightJet
```

The following values now follow that direction only:

- `aoaPitchReduction`
- `aoaSoftLimitDeg`
- `aoaHardLimitDeg`
- `softGLimit`
- `hardGLimit`

The F-16 catalog remains unchanged at `aoaSoftLimitDeg = 22`, `aoaHardLimitDeg = 30`, and `aoaPitchReduction = 0.05`. The F-16 does not explicitly override `softGLimit` or `hardGLimit`, so they currently resolve to the shared profile defaults `8.8 / 11.2`.

No Phase 5C/live replacement handoff is approved by this checkpoint.

## Soft-G semantic finding

`MavInstructorController.ApplyProtectionAssists` currently contains two G-limit branches:

```text
1. gEstimate > sustainedGLimit
2. gEstimate > softGLimit && gEstimate <= sustainedGLimit
```

The stock values are:

```text
softGLimit      = 8.8
sustainedGLimit = 8.8
```

Therefore branch 2 is unreachable for the stock configuration: no value can be simultaneously `> 8.8` and `<= 8.8`.

This does **not** mean G limiting is entirely disabled. Branch 1 starts immediately above `sustainedGLimit`, so the current setup behaves as a single-stage threshold at 8.8 g. What is dead is the distinct `softGLimit → sustainedGLimit` stage.

`MavAircraftRuntimeProfile` does not currently expose/override `sustainedGLimit`, so an aircraft profile cannot independently describe that two-stage region. Do not invent a new F-16 number here. Before the replacement FLCS work, either source a meaningful sustained/soft relationship or simplify the legacy semantics explicitly.

## Remaining duplicated profile/runtime fields

After removing the five protection-setting back-channels, 23 profile-facing fields still follow the older duplicated pattern: profile/applier writes the Jet copy, Instructor consumes its own copy, and Instructor mirrors back to Jet each physics step.

These are intentionally **not fixed in Phase 5B.0b** because changing all of them at once would make handling regressions impossible to attribute.

### A — Safety / envelope protection

None remaining in the audited 23. The five known protection values were migrated first.

### B — Control authority / gain

- `lowSpeedPitchAuthority` — F-16 explicitly overrides
- `bestSpeedPitchAuthority` — F-16 explicitly overrides
- `highSpeedPitchAuthority` — F-16 explicitly overrides
- `lowSpeedRollAuthority` — currently inherited from profile default
- `bestSpeedRollAuthority` — currently inherited from profile default
- `highSpeedRollAuthority` — currently inherited from profile default
- `pitchGain` — F-16 explicitly overrides
- `rollGain` — F-16 explicitly overrides
- `yawGain` — F-16 explicitly overrides
- `maxAutoPitch` — currently inherited from profile default
- `noseDownTrim` — currently inherited from profile default
- `keyboardYawAuthority` — currently inherited from profile default

Recommended future owner: resolved aircraft tuning/configuration, consumed by Instructor. Jet fields should become mirrors or be removed where no real consumer needs them.

### C — Input shaping / response

- `sensitivity` / profile `mouseSensitivity` — F-16 explicitly overrides
- `inputSmoothing` — currently inherited from profile default
- `keyboardElevatorResponse` — currently inherited from profile default
- `keyboardElevatorReleaseBlend` — currently inherited from profile default
- `throttleChangeRatePercentPerSecond` / profile `throttleChangeRate` — currently inherited from profile default

Recommended future owner: resolved input/control configuration. Avoid bidirectional runtime copying.

### D — Convenience / game-feel

- `manualPitchBoost`
- `manualRollBoost`
- `bestTurnPitchBoost`
- `bestTurnRollBoost`
- `maxScreenRollBankAngle`
- `speedAssistStrength`

These remain useful legacy-gameplay concepts while the Instructor owns mouse-flight accessibility. They should be migrated deliberately, not bundled into an aerodynamic/FLCS correctness patch.

### E — Telemetry / compatibility mirror only

None of the 23 underlying settings are telemetry-only: they all represent behavior configuration. Their **Jet-side copies**, however, should be treated as compatibility/inspection mirrors wherever the Jet is not the real consumer.

One confirmed mirror dependency already exists outside this list: `MavPhysicalAIRewardLogger` reads `jet.hardGLimit`, so that mirror must remain authority → mirror until the reader is migrated.

## Runtime maneuver gate still required

The branch contains `MavManeuverDiagnostics`, but a GitHub source review cannot certify the high-maneuver result. The remaining acceptance gate must run in Unity/Play Mode.

Primary mouse-aim cases:

1. `AbruptFullPitch`
2. `HighSpeedPull`
3. `LowSpeedHighAoAPull`
4. `NearStallRecovery`

For each case record:

- peak AoA
- time above the 22° soft limit
- time above the 30° hard limit
- peak Nz
- time above the configured G limits
- TAS/energy delta
- post-release recovery time

`MavManeuverDiagnostics.treatScriptedInputAsManual` should be **false** for the primary mouse-flight result. Run the keyboard/manual path separately with it set to **true**; `manualEnvelopeBypassFactor = 0.65` is intentionally unchanged at this checkpoint.

## Structural validation shortcut

Use:

```text
Maverick → Flight Dynamics → Run Phase 5 Checkpoint Validation
```

This invokes, in order:

1. Phase 4B Turn Dynamics Validation
2. Phase 5A Ownership Validation
3. AoA Limiter Ownership Validation
4. Envelope Protection Ownership Validation

Review each suite's own `RESULT:` line. The shortcut does not pretend to replace Play Mode maneuver testing.

## Go / no-go

- Phase 5A ownership foundation: structurally ready for Unity confirmation.
- Phase 5B.0a/0b limiter ownership correction: structurally ready for Unity confirmation.
- Phase 5C live Morelli/SixDoF ownership: **NO-GO** until runtime validation plus the known gravity, gear-drag, and state-handover blockers are resolved.

Do not tune the remaining duplicated fields merely to improve feel before the runtime measurements are captured.
