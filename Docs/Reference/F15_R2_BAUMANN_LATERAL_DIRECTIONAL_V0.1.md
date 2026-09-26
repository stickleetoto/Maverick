# F-15 R2 Baumann Lateral-Directional V0.1

Status: **IMPLEMENTED SOURCE TRANSCRIPTION — CROSS-VALIDATION ONLY**

Branch: `sol/f15-r2-lateral`

This patch is stacked on `sol/f15-r2-baumann-static`.

## 1. Scope

Extend the already-transcribed Mach 0.6 / 20,000 ft AFIT/Baumann longitudinal research slice to
the source routine's lateral-directional channels.

This remains intentionally separate from the exact Maverick target:

`NASA_F15B_836_SN74_0141_PRE_QUIET_SPIKE_BASELINE_F100_PW_100`

No statement in this patch promotes the Baumann/AFIT research database to exact NASA 836
aerodynamic authority.

## 2. Source

Primary transcription source:

- Michael T. Davison, *An Examination of Wing Rock for the F-15*,
  AFIT/GAE/ENY/92M-01, Appendix C.

The included `COEFF` routine states that its primary coefficient-equation source is the McAir
F-15 baseline-simulator ARO10 code and that most coefficients were derived from raw F-15 simulator
data tables.

The same routine explicitly identifies the research database condition as:

- Mach 0.6
- 20,000 ft
- 608 ft^2 reference area
- 42.8 ft span
- 15.94 ft mean aerodynamic chord

The Unity bridge keeps the existing fixed-condition refusal gate.

## 3. Added coefficient channels

`MavF15BaumannMach06LateralDirectional.cs` now reproduces the Appendix C structure for:

### Side force, CY

- basic side force versus beta;
- beta sign smoothing;
- aileron derivative;
- rudder derivative;
- differential-tail derivative;
- roll-rate derivative;
- yaw-rate derivative;
- high-alpha asymmetric side-force increment.

### Rolling moment, Cl

- basic rolling moment versus beta;
- aileron derivative;
- rudder derivative;
- differential-tail derivative;
- roll damping;
- yaw-rate derivative;
- two-place-canopy beta increment.

### Yawing moment, Cn

- basic yawing moment versus beta;
- aileron derivative;
- rudder derivative;
- differential-tail derivative;
- roll-rate derivative;
- yaw damping;
- two-place-canopy beta increment;
- high-alpha asymmetric yawing-moment increment.

The source flex multipliers are preserved:

- differential tail: 0.975;
- CY rudder: 0.89;
- Cl rudder: 0.85;
- Cn rudder: 0.89.

The speedbrake-dependent rudder-effectiveness multiplier is fixed at 1.0 because the research
configuration omits/retracts the speedbrake contribution.

## 4. Differential-tail ownership

The Appendix C coefficient routine accepts differential tail as an independent state.

The aircraft-independent `MavControlInput` currently has no F-15-only differential-tail channel,
and this patch deliberately does not modify shared FDM Core merely to solve that aircraft-specific
problem.

Instead, `MavF15BaumannSurfaceState` makes differential tail first-class inside the F-15 research
aerodynamic boundary.

Default bridge behavior uses the source-documented relation:

`DTALD = 0.3 * DAILD`

An explicit research/debug override exists for injecting an independent differential-tail state.
That override is not an F-15 production control law.

## 5. Unity-facing modes

`MavF15AeroModel` now exposes three distinct modes:

1. `ExactNasa836Unavailable`
   - default;
   - returns zero coefficients.

2. `BaumannMach06LongitudinalResearch`
   - previous R2 slice;
   - CX/CZ/Cm only.

3. `BaumannMach06SixAxisResearch`
   - CX/CY/CZ/Cl/Cm/Cn;
   - fixed-condition, explicit opt-in research model.

Both research modes refuse off-condition Mach/altitude evaluation.

## 6. Non-goals

This patch does not:

- claim a full-envelope F-15 database;
- infer Mach dependence away from M=0.6;
- infer altitude dependence away from 20,000 ft;
- replace the exact NASA 836 target profile;
- add a production F-15 control law;
- add exact NASA 836 hard stops or actuator rates;
- add F100 thrust;
- modify shared FDM Core.

## 7. Next slice

The next useful F-15 implementation work can proceed in parallel:

- build an F-15 research control-law/FCS boundary around PRAD/RRAD/CAS/ARI evidence;
- add source-to-code provenance tables for each transcribed coefficient family;
- add validation fixtures later against selected Appendix C coefficient points;
- continue acquisition of MDC A4172 Part II / exact NASA 836 baseline coefficient authority.

Runtime/Unity validation remains deferred.
