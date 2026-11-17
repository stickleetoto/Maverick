# Validation Limitations — NASA 6-DoF R1

## 1. What these results establish

- Maverick's rigid-body backend (`MavSixDoFBody` + PhysX) integrates torque-free and damped rotation **correctly and at first order**, with the gyroscopic term supplied exactly once.
- The aerodynamic load path (coefficients → q̄·S·(b, c̄) → aero-body to Unity axes → one application per step) is correct for rate damping, including at zero airspeed.
- Maverick's F-16 Morelli implementation reproduces Morelli's published polynomial; its units, axes and control-sign conventions agree with NASA's F-16 model.

## 2. What they do not establish

- **Real-aircraft fidelity.** Matching a NASA benchmark verifies numerics and bookkeeping. The NESC F-16 is itself a simulation model (Stevens & Lewis / Garza & Morelli lineage of 1979 low-speed tunnel data), not flight data.
- **Anything outside the tested envelopes:** no Mach effects (both F-16 models have none), no α beyond 45°, no propulsion dynamics, no control laws, no landing gear or ground, no atmosphere above 30 000 ft.
- **F-15, F-22 or any other aircraft.**

## 3. Not compared, with reasons

| Item | Reason | What would unblock it |
|---|---|---|
| Case 11 F-16 trajectory (180 s) | NASA runs on a rotating WGS-84 Earth; Maverick is flat and non-rotating. Maverick has no NESC-table aero or NESC steady-state thrust as a selectable profile | a validation-only NESC aero + thrust profile on `MavSixDoFBody`; explicit Coriolis/transport-rate/curvature bookkeeping or comparison of relative quantities only |
| Cases 13.1–13.4 (closed-loop steps) | need the NESC LQR SAS/autopilot ported as a validation-only control law, plus the above | port of `F16_control.dml` behind the existing control-law interface |
| Case 12 (supersonic trim) | aero model has no Mach input; the case exercises out-of-range table behaviour, not supersonic aerodynamics | not meaningful for verification |
| Cases 15, 16 (pole, date line) | test geodesy a flat simulator does not have | not applicable |
| Case 4 (non-rotating round Earth) | not run in R1 (translational EOM only); Vol II Table 73 now gives the constants | straightforward next step |
| Cases 1, 5–10 | rotating-Earth translational effects | partial use with explicit corrections |
| Separation of D-F1…D-F4 into fit error vs dataset difference | needs the Stevens & Lewis App. A tables Morelli fitted (not inspected) | inspect the book or another faithful copy of that dataset |

## 4. Known approximations in the validators

- The extractor's rotating-NED → inertial attitude transform and the case 3 gravity model (J2 gravitation minus Ω²r, radial, equatorial) are **engineering inferences**, confirmed by agreement with the NASA tools (≈ 10⁻¹⁰ deg/s case 2; 3.4 × 10⁻⁴ deg/s, 0.023 ft case 3) but not NASA statements.
- Case 3 airspeed is the vertical fall speed; the horizontal Coriolis component is neglected (≈ 2 × 10⁻⁶ relative at 30 s, inference).
- The DAVE-ML `minValue` on the brick's airspeed is applied to the aero model's input only; q̄ uses the true airspeed (inference consistent with S-119 semantics).
- The case 11 static check neglects SIM 04's trimmed pitch rate (−0.0039 deg/s) and rotating-Earth terms in the thrust balance; tolerances are sized to them (`NUMERICAL_CONVERGENCE.md` §4).
- `MavDaveMlEvaluator` implements only the S-119 subset NASA's F-16 and brick files use; anything else throws. It is validated only by NASA's check-shots.
- The derivative-sign classification and the sweep classification rule (correlation ≥ 0.98, max|d| ≤ 25 % of range) are reporting conventions chosen before the run, not sourced criteria.

## 5. Test-rig settings that differ from aircraft use (validation bodies only)

`requireOperationalReadinessForLoadApplication = false` (a brick has no aircraft readiness); `Rigidbody.sleepThreshold = 0` (a slowly tumbling body must not sleep); `automaticCenterOfMass = automaticInertiaTensor = false`; `Physics.simulationMode = Script` and `Physics.gravity` set for the run and restored afterwards; temporary `HideAndDontSave` objects destroyed after each run. No production default was changed.

## 6. Environment

One Windows machine, Unity 6000.3.16f1, headless batch mode. A fresh import re-serialises `ProjectSettings/DynamicsManager.asset`, `EditorBuildSettings.asset` and `ShaderGraphSettings.asset` to the current format; these changes were not committed.
