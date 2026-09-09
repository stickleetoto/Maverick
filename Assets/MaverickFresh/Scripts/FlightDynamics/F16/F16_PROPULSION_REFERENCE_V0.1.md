# F-16 Propulsion Reference v0.1

Status: **POWER DYNAMICS FROZEN / THRUST DECK NOT FROZEN**

## Source policy

Implementation authority for this phase:

- Frederico R. Garza and Eugene A. Morelli, **NASA/TM-2003-212145, A Collection of Nonlinear Aircraft Simulations in MATLAB**.
- NASA Software Catalog entry **LAR-17463-1** is treated as provenance for the Garza/Morelli simulation package, not as a source of files copied into Maverick.

AeroBenchVVPython remains external validation only. No AeroBench code, constants, tables, or implementation are copied into Maverick. JSBSim is not used.

## Frozen engine power-state equations

Throttle `delta_th` is normalized to `[0, 1]`. Commanded engine power `Pc` is percent-power `[0, 100]`:

```text
Pc = 64.94 * delta_th                       delta_th <= 0.77
Pc = 217.38 * delta_th - 117.38            delta_th > 0.77
```

Actual engine power `Pa` follows the Garza/Morelli piecewise transition logic.

```text
if Pc >= 50:
    if Pa >= 50: target = Pc; rateFactor = 5
    else:        target = 60; rateFactor = RTAU(target - Pa)
else:
    if Pa >= 50: target = 40; rateFactor = 5
    else:        target = Pc; rateFactor = RTAU(target - Pa)

Pdot = rateFactor * (target - Pa)
```

with reciprocal time constant:

```text
RTAU(dP) = 1.0                     dP <= 25
RTAU(dP) = 0.1                     dP >= 50
RTAU(dP) = 1.9 - 0.036*dP          otherwise
```

Maverick integrates this derivative with its own deterministic fixed-step numerical integration. The derivative is sourced; the specific numerical integrator is Maverick infrastructure and is not claimed to reproduce the original MATLAB integrator exactly.

## Thrust equation structure

Garza/Morelli specifies thrust as a function of altitude, Mach, and actual engine power. The reported model interpolates idle, military, and maximum thrust decks and blends them with actual power:

```text
Pa < 50:
T = Tidle + (Tmil - Tidle) * (Pa / 50)

Pa >= 50:
T = Tmil + (Tmax - Tmil) * ((Pa - 50) / 50)
```

However, the numerical `Tidle/Tmil/Tmax` altitude/Mach deck is **not frozen in this repository**. The associated NASA software package is cataloged as U.S.-release-only, and this project will not fill the gap from a third-party reimplementation while claiming NASA-reference accuracy.

Therefore `MavF16EnginePowerModel` currently:

- advances sourced throttle gearing and power dynamics;
- exposes commanded/actual power for telemetry;
- returns exactly `0 N` thrust and `0 N*m` propulsion moment;
- reports `HasAuthoritativeData == false` for dimensional propulsion output;
- keeps live six-DoF takeover OFF.

## Explicitly deferred

- altitude/Mach idle thrust deck;
- altitude/Mach military thrust deck;
- altitude/Mach maximum/afterburner thrust deck;
- thrust interpolation/extrapolation policy;
- engine angular-momentum / gyroscopic coupling (`160 slug-ft^2/s` is documented by Garza/Morelli but not yet integrated into the Unity rigid-body ownership model);
- fuel consumption;
- operational LIVE_READY gate.

## Validation vectors

`MavF16PropulsionValidation` pins:

- throttle `0 -> Pc 0`;
- throttle `0.77 -> Pc ~50`;
- throttle `1 -> Pc 100`;
- RTAU boundary/intermediate values;
- all four piecewise Pdot branches;
- bounded fixed-step integration;
- zero-thrust safety boundary at 0/50/100 percent actual power.
