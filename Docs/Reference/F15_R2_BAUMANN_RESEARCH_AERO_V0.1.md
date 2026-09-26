# F-15 R2 Baumann Research Aero V0.1

Status: **IMPLEMENTED PARTIAL RESEARCH MODEL — NOT NASA 836 TARGET AUTHORITY**

Branch: `sol/f15-r2-baumann-static`

## 1. Why this exists

The exact numeric coefficient database for the frozen Maverick target

`NASA_F15B_836_SN74_0141_PRE_QUIET_SPIKE_BASELINE_F100_PW_100`

has still not been recovered.

The AFIT material acquired for gap closure does, however, contain a reproducible F-15 research
aerodynamic model. The important discovery is that Davison's appendix includes the coefficient
routine itself, not only plots or derivative summaries.

That code identifies its primary lineage as McAir baseline-simulator ARO10 and states that the
version used in the research program is based on the 1988 F-15 aerobase at **Mach 0.6 and
20,000 ft**.

This is valuable enough to implement as a separate cross-validation model, but it must not be
laundered into the exact NASA 836 target.

## 2. Source boundary

Primary transcription source:

- Michael T. Davison, *An Examination of Wing Rock for the F-15*,
  AFIT/GAE/ENY/92M-01, 1992, Appendix C.

Independent/context source:

- Robert C. Nolan II, *Wing Rock Prediction Method for a High Performance Fighter Aircraft*,
  AFIT/GAE/ENY/92J-02, 1992.

The research model:

- is fixed to Mach 0.6 / 20,000 ft pressure altitude;
- uses a 608 ft^2 reference wing area;
- uses a 42.8 ft span;
- uses a 15.94 ft mean aerodynamic chord;
- records the underlying ARO10 moment reference at 0.2565 cbar;
- represents a clean F-15 research/simulator configuration;
- is not the recovered exact NASA 836 pre-Quiet-Spike coefficient database.

Davison's later flight-test discussion explicitly warns against assuming that the M=0.6
aerodynamics remain correct at other Mach numbers. The Unity bridge therefore refuses to evaluate
the research model away from the fixed source condition instead of extrapolating it.

## 3. Implemented code

### `MavF15BaumannMach06Reference.cs`

Freezes only the metadata needed to reproduce the research model:

- source identity;
- M=0.6;
- 20,000 ft;
- research-model S/b/cbar;
- moment-reference CG;
- SI conversions.

The Mach and altitude tolerances are numeric equality tolerances only. They are not a claimed
validity envelope.

### `MavF15BaumannMach06Longitudinal.cs`

Transcribes the longitudinal subset of the Appendix C coefficient routine:

- `CFZ`;
- low-AOA `CFX1`;
- high-AOA `CFX2`;
- the original 20-30 degree smooth drag transition;
- basic pitching moment `CMM1`;
- pitch damping `CMMQ`;
- final body-axis `CX`, `CZ`, and `Cm`.

`CY`, `Cl`, and `Cn` remain zero in this slice.

The original source mixes engine thrust into `CX` and a thrust-line term into `Cm`. Those terms
are deliberately omitted because Maverick has a separate propulsion load owner. Importing them
into aerodynamics would create a future double-application defect.

### `MavF15AeroModel.cs`

Adds the aircraft-specific aerodynamic boundary that the common FDM can call.

Default:

`ExactNasa836Unavailable`

Result:

- zero coefficients;
- visible refusal status.

Optional research mode:

`BaumannMach06LongitudinalResearch`

Requirements:

- `allowCrossValidationResearchModel == true`;
- Mach must match 0.6 within a numerical equality tolerance;
- standard-atmosphere altitude proxy must match 20,000 ft within a numerical equality tolerance.

If any precondition fails, the model returns zero coefficients and reports why.

## 4. Architecture relation to the F-16

The F-16 contributed the reusable pattern:

`aircraft aero model -> coefficients -> shared dimensionalization -> MavSixDoFBody`

It did **not** contribute any F-16 aerodynamic number.

The F-15 research implementation therefore reuses:

- `MavAerodynamicModelBase`;
- `MavAeroCoefficients`;
- normalized pitch-rate convention;
- the common SixDoF/load boundary.

It does not reuse:

- Morelli coefficients;
- F-16 control deflection limits;
- F-16 CG values;
- F-16 engine laws.

## 5. Safety / provenance rules

The following are deliberate:

1. Exact NASA 836 mode remains unavailable and returns zero.
2. Research mode requires explicit opt-in.
3. Research mode refuses off-condition Mach/altitude evaluation.
4. Longitudinal-only status is inspector-visible.
5. Missing lateral coefficients remain zero, not approximated.
6. Source thrust terms are removed from the aerodynamic model because propulsion owns thrust.
7. No exact-target `S` or `cbar` is promoted by this patch.

## 6. Next slice

The next R2 patch should transcribe the lateral-directional Appendix C channels while preserving the
source's unusual details:

- beta sign multipliers;
- aileron / rudder / differential-tail derivatives;
- p/r rate terms;
- high-alpha asymmetric `CY` and `Cn` increments;
- F-15B canopy increments;
- differential-tail state as a first-class F-15 surface channel.

That patch should remain on the research model until an exact NASA 836 database is recovered.
