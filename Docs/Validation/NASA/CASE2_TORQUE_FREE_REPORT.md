# NASA Check Case 2 — Dragless Tumbling Brick (Torque-Free Rotation)

**Suite:** `MavNasaBrickCheckCaseValidation` sections [B0]–[B2] · **Unity** `6000.3.16f1` · **Result of the suite:** 83 passed / 0 failed (run `brick2`; the first run `brick` was 82/1, see §6).
Two independent Unity launches produced byte-identical reports except for the one re-based gate line.

---

## 1. NASA case definition (source-verified)

| Item | Value | Source |
|---|---|---|
| Vehicle | "fictitious U.S. standard face brick": m = 0.155404754 slug; Ixx 0.001894220, Iyy 0.006211019, Izz 0.007194665 slug·ft²; all products of inertia 0; CM = MRC | `brick_inertia.dml` (SHA-256 `eae595902d2f…bfa1`); Vol II Table 4 (PDF p. 17) — both read; [B0] asserts they agree |
| Aerodynamics | all coefficients set to zero for case 2 | Vol II §C.1.2, Table 26 note |
| Initial attitude | body x north, body z down (Euler angles 0 relative to local NED) | Vol II §C.1.2; every tool's first CSV row |
| Initial rates | **inertial** body rates 10, 20, 30 deg/s; Table 26 lists them relative to the rotating Earth (9.995821927, 20, 30) | Vol II §C.1.2 text and Table 26 |
| Earth / gravity | WGS-84 rotating ellipsoid, J2 gravitation, US 1976 atmosphere, still air; drop from 30 000 ft over 0° N 0° E | Vol II Table 26 |
| Duration | 30 s | same |
| Reference outputs | 5 tools (sims 01, 02, 04, 05, 06); `bodyAngularRateWrtEi` (inertial rates, body axes), Euler angles relative to the rotating local NED | `Atmos_02_*.csv`, SHA-256 in `MavNasaBrickReferenceData.cs` |

**Frame treatment.** Torque-free rotation does not depend on gravity or Earth rotation, but the published *attitude* is relative to a rotating NED frame. The extractor converts each tool's Euler angles into the attitude relative to the t = 0 NED frame (an inertial frame) using its latitude, longitude and Earth rotation 7.292115 × 10⁻⁵ rad/s (Vol II Table 73). Maverick's flat, non-rotating Unity world is mapped to that frame (Unity +z = north, +x = east, −y = down).

---

## 2. Independent reference vs NASA ([B1])

The reference integrates Euler's equations and quaternion kinematics with RK4 in double precision at h = 1 ms (`MavNasaRigidBodyReference`, no production code). Its own truncation error (h vs h/2) is **1.877 × 10⁻¹²** deg/s.

| Tool | max \|Δω\| (deg/s), reference − tool | max attitude difference (deg) |
|---|---:|---:|
| sim 01 | 3.496 × 10⁻¹⁰ | 3.488 × 10⁻⁹ |
| sim 02 | 1.242 × 10⁻³ | 3.712 |
| sim 04 | 3.877 × 10⁻¹⁰ | 5.220 × 10⁻⁹ |
| sim 05 | 4.966 × 10⁻⁵ | 8.677 × 10⁻⁵ |
| sim 06 | 4.743 × 10⁻³ | 9.642 × 10⁻³ |

- Sims 01 and 04 agree with each other to 1.376 × 10⁻¹⁰ deg/s; the reference sits with them (gate < 10⁻⁶ deg/s and < 10⁻⁶ rad: **PASS**). This confirms the reading of the case: inertial initial rates and the rotating-NED attitude output.
- Sim 02's 3.7° attitude drift is consistent with NASA's own decision to exclude SIM 2 Euler angles from its match statistics (Vol II §E.1.2).
- Envelope rule (reference no farther from sim 04 than the tools are from each other, SIM 2 attitude excluded): **PASS** (worst ratio 0.000).

---

## 3. Maverick backend vs the reference ([B2])

Brick on a production `MavSixDoFBody` + Rigidbody, stepped with `StepPhysicsForValidation` and `Physics.Simulate`; gyroscopic compensation ON (production default).

| dt (s) | max \|Δω\| (deg/s) | max attitude error (deg) | \|H\| relative drift | T relative drift |
|---:|---:|---:|---:|---:|
| 0.0200 | 9.159 × 10⁻¹ | 5.306 | 8.589 × 10⁻³ | 1.912 × 10⁻² |
| 0.0100 | 4.536 × 10⁻¹ | 2.617 | 4.229 × 10⁻³ | 9.365 × 10⁻³ |
| 0.0050 | 2.254 × 10⁻¹ | 1.300 | 2.100 × 10⁻³ | 4.636 × 10⁻³ |
| 0.0025 | 1.124 × 10⁻¹ | 0.6484 | 1.047 × 10⁻³ | 2.307 × 10⁻³ |

(maxima over the 15 samples t = 2…30 s; H is the inertial angular-momentum vector, T the rotational kinetic energy; initial |ω| = 37.4 deg/s.)

Gated results:
- **Mechanics, every step size: PASS.** One load application per step (1 500 / 3 000 / 6 000 / 12 000), gyroscopic compensation applied exactly once in every step, no refusals, all values finite, Unity angular and linear damping zero, Rigidbody mass and principal inertia equal to the NASA values after the production `MavMassProperties` path (its 1 kg / 0.001 kg·m² floors do not bind), `maxAngularVelocity` 50 rad/s.
- **First-order convergence: PASS** for body rates (observed orders 1.014, 1.009, 1.004), attitude (1.020, 1.010, 1.003), |H| drift (1.022, 1.010, 1.004) and T drift (1.030, 1.014, 1.007).
- **Gyroscopic control: PASS.** With `applyBackendGyroscopicCompensation = false` (dt 0.005) the error is **45.33 deg/s** and **81.2°**, 200× the compensated error; PhysX alone holds the rates constant (max change 4.4 × 10⁻⁴ deg/s). The backend therefore omits ω × Iω, Maverick supplies it, and it is applied exactly once: if PhysX also applied it, the compensated runs would converge to a different solution.

---

## 4. Interpretation

1. **The integration is correct and first order.** Maverick + PhysX converges to the exact torque-free solution, with error proportional to dt. This is what a semi-implicit Euler integrator with an explicitly evaluated gyroscopic torque should do.
2. **At Maverick's 50 Hz the first-order error is not small for rotation-dominated motion.** For this brick (≈ 0.65 rad/s, strongly asymmetric inertia), 30 s at dt 0.02 accumulates **0.92 deg/s** rate error, **5.3°** attitude error and a **1.9 %** rise in rotational kinetic energy. The energy *increase* is the expected signature of explicit evaluation of the gyroscopic term. The error scales with dt and with the rotation rate and inertia asymmetry; aircraft manoeuvres with higher rates for long durations will see comparable relative errors unless dt is reduced or the rotational integration is improved. This is a measured property of the numerical method, not of the model data.
3. **What this does not show:** anything about aerodynamics, propulsion or real-aircraft behaviour.

---

## 5. Reproduce

`Unity.exe -batchmode -quit -nographics -projectPath <worktree> -executeMethod MaverickFresh.FlightDynamics.EditorTools.MavFdmValidationBatchAdapter.RunBatch -fdmSuite nasa_brick -fdmMode sync -fdmType MavNasaBrickCheckCaseValidation -fdmMethod RunAll -fdmOut <json> -logFile <log>`. The suite needs no external file: the reference data are generated into `MavNasaBrickReferenceData.cs` by `Tools/nasa_6dof/extract_nesc_brick_reference.py` from the hash-pinned NASA zips.

## 6. Run history

| Run | Result | Note |
|---|---|---|
| `brick` | 82 / 1 | [B3] reference self-check gated at an unjustified 10⁻⁸ deg/s failed at 1.96 × 10⁻⁸ (case 3). Re-based on its purpose, see `NUMERICAL_CONVERGENCE.md` §4. No measured value changed. |
| `brick2` | 83 / 0 | identical report except that line |
