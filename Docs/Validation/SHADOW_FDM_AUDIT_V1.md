# Shadow FDM Performance & Safety Audit V1

Branch: `sol/f16-powered-reference-smoke`

Purpose: record the independent Shadow-FDM audit and distinguish findings that have been directly cross-checked in the current branch from hypotheses that still require runtime profiling.

## Current architecture

- Legacy physics still owns and flies the live aircraft in Shadow.
- The replacement F-16 six-DoF stack computes in parallel.
- Shadow must never apply replacement loads to the Rigidbody.
- Powered Shadow uses the sourced TP-1538 propulsion path and operational mouse-instructor command bridge.
- `allowReplacementActivation` remains false in the powered-shadow smoke component.
- Live `F16Replacement` ownership is a later, separate atomic-handover stage.

## Verified hot-path findings

### V1-01 — ownership diagnostics run every FixedUpdate

`MavFlightPhysicsOwnership.FixedUpdate()` increments its step counter and unconditionally calls:

1. `EnforceGravityOwnership()`
2. `EnforceArmingForCurrentOwner()`
3. `RefreshWriterDiagnostics()`

This work is therefore part of every Shadow physics step while the ownership component is active.

### V1-02 — gravity ownership status allocates diagnostic strings at physics rate

`EnforceGravityOwnership()` rebuilds `debugGravityOwnershipStatus` using repeated string concatenation, including enum/int conversions. This is diagnostic output, not a physics requirement.

Safe optimization direction: preserve the numeric/state checks every step, but compose the human-readable string only when state changes or at a low diagnostic cadence.

### V1-03 — active legacy writer string is rebuilt at physics rate

`RefreshWriterDiagnostics()` walks registered legacy writers every step and allocates a new `StringBuilder` whenever at least one writer reports a live write, followed by `ToString()` for `debugActiveLegacyWriters`.

The per-step numeric fact `debugActiveLegacyWriterCount` is useful for ownership evidence. The formatted writer-name string does not need physics-rate refresh.

Safe optimization direction: keep writer observation/counting at physics rate, but rebuild the formatted string only on change or at a diagnostic cadence. Do not remove writer registration, observation, or quiet-step checks.

### V1-04 — control-law success status is rebuilt every FixedUpdate

`MavFlightControlLawBase` rebuilds its success `debugStatus` string after publishing actuator demand. This is also diagnostic text, not part of the control law itself.

Safe optimization direction: update this string only when command-resolution/status state changes, while leaving command evaluation and actuator publication at physics rate.

### V1-05 — FixedUpdate catch-up is a plausible amplifier

Shadow intentionally runs both legacy and replacement calculations. If frame time exceeds the fixed timestep, Unity may execute multiple FixedUpdates in a later frame to catch up. Any per-step allocation/diagnostic overhead is therefore multiplied during a stall.

This is an amplifier hypothesis and should be confirmed with the Unity Profiler by checking the number of FixedUpdate/physics steps executed in affected frames.

## Strong but not yet runtime-confirmed hypotheses

### H1 — Q+W increases active legacy-writer count

The independent audit linked W to manual pitch-up and Q to manual yaw, then hypothesized that high AoA can activate additional legacy writer paths such as thrust-vector control.

This remains a hypothesis until the runtime component set and `debugActiveLegacyWriterCount` are observed during W-only versus Q+W runs.

### H2 — negative-cache misses cause repeated component searches

Several `Resolve()`/`ResolveComponents()` patterns only cache successful lookups. If an optional component remains absent, the lookup can repeat. This must be measured on the actual `Mav_Player` configuration before changing the lookup contract.

### H3 — editor Inspector repaint contributes additional cost

Observed behavior already supports this partially: deselecting `Mav_Player` reduced lag. A lightweight `MavSixDoFBody` inspector has been added, but other high-churn components still expose many live fields.

Editor-only repaint cost must be separated from runtime cost using a Development Player + Profiler before using Editor timing as handover evidence.

## Shadow ownership safety status

Current powered-shadow design still returns before `MavSixDoFBody.TryApplyLoadSet`, so replacement force/torque application remains blocked in Shadow.

Do not optimize by:

- disabling the ownership authority,
- bypassing readiness,
- enabling `allowReplacementActivation`,
- enabling `acceptNonAuthoritativePropulsion`,
- enabling structural-only live load application,
- removing legacy-writer observation,
- changing F-16 aerodynamic or thrust data to hide a performance issue.

## Handover evidence gaps

A healthy Shadow run currently proves that the replacement pipeline can execute and remain finite without applying live loads. It does not yet prove numerical agreement with the legacy load set, post-handover timing headroom, or trim/equilibrium under the final live mass/inertia configuration.

Before atomic handover, add evidence for:

1. legacy-vs-replacement load residuals,
2. Shadow/live CPU and GC allocation envelope,
3. handover mass/inertia/gravity invariants,
4. trim/equilibrium at the intended handover state,
5. absence of ownership gaps or double writers during the transition.

## Recommended next implementation slice

Performance-only, no physics/tuning change:

1. Throttle/cache `debugGravityOwnershipStatus` string composition.
2. Keep active-writer counting every physics step but throttle/cache `debugActiveLegacyWriters` formatting.
3. Cache `MavFlightControlLawBase.debugStatus` when command-resolution state is unchanged.
4. Add Profiler markers around ownership diagnostics and the Shadow six-DoF step.
5. Profile W-only and Q+W with `Mav_Player` deselected, then repeat in a Development Player.

Any change above must preserve ownership gates, load-write boundaries, sourced propulsion requirements, and the existing validation baseline.