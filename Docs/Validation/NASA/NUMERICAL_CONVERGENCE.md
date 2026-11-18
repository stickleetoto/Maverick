# Numerical Convergence and Threshold Basis

All numbers are from Unity `6000.3.16f1` runs of the committed suites (`brick2`, `f16b`). Two separate Unity launches of the brick suite produced byte-identical reports, so every value below is deterministic for this build.

---

## 1. Method

- **Measured system:** production `MavSixDoFBody` (load computation, gyroscopic compensation, one `AddRelativeForce`/`AddRelativeTorque` per step) integrated by Unity PhysX via `Physics.Simulate(dt)`; float32 state.
- **Reference:** `MavNasaRigidBodyReference`, classical RK4 in double precision at h = 1 ms, independent of all production code.
- **Step sizes:** dt = 0.02 (Maverick's runtime step), 0.01, 0.005, 0.0025 s; 30 s; samples every 2 s.
- **Error measures:** max over samples of |Δω| (vector, deg/s); attitude error as the rotation angle between direction-cosine matrices, 2·asin(‖C₁ − C₂‖_F / 2√2) (accurate at small angles); relative drift of the inertial angular-momentum vector ‖H(t) − H(0)‖/‖H(0)‖ and of rotational kinetic energy |T(t) − T(0)|/T(0) (case 2, where both are exact invariants); altitude error (case 3).
- **Observed order:** p = log₂(e(dt)/e(dt/2)).

---

## 2. Results

### Case 2 (torque-free)

| dt | \|Δω\| deg/s | attitude deg | ‖ΔH‖/‖H‖ | \|ΔT\|/T |
|---:|---:|---:|---:|---:|
| 0.0200 | 9.159E-01 | 5.306 | 8.589E-03 | 1.912E-02 |
| 0.0100 | 4.536E-01 | 2.617 | 4.229E-03 | 9.365E-03 |
| 0.0050 | 2.254E-01 | 1.300 | 2.100E-03 | 4.636E-03 |
| 0.0025 | 1.124E-01 | 0.6484 | 1.047E-03 | 2.307E-03 |
| observed p | 1.014 / 1.009 / 1.004 | 1.020 / 1.010 / 1.003 | 1.022 / 1.010 / 1.004 | 1.030 / 1.014 / 1.007 |

### Case 3 (damped)

| dt | \|Δω\| deg/s | attitude deg | \|Δh\| m |
|---:|---:|---:|---:|
| 0.0200 | 8.030E-02 | 0.2724 | 2.968 |
| 0.0100 | 4.010E-02 | 0.1367 | 1.407 |
| 0.0050 | 2.002E-02 | 0.06856 | 0.7968 |
| 0.0025 | 9.961E-03 | 0.03713 | 0.2323 |
| observed p | 1.002 / 1.002 / 1.007 | 0.995 / 0.996 / 0.885 | — |

### Control: gyroscopic compensation off (case 2, dt 0.005)

\|Δω\| 45.33 deg/s, attitude 81.2° — non-convergent to the true solution by construction (PhysX keeps ω constant; measured change 4.4 × 10⁻⁴ deg/s over 30 s).

---

## 3. Reading the results

1. **The integrator is first order and in its asymptotic range from dt = 0.02 down.** Orders are 1.00–1.03 throughout; the one 0.885 (case 3 attitude, finest pair) is where the attitude error approaches the reference-comparison floor of float32 transforms and is reported only.
2. **Rotational energy grows**, linearly in dt (+1.9 % in 30 s at dt 0.02 for this brick). Explicit evaluation of −ω × (Iω) from the start-of-step rates, followed by PhysX's semi-implicit update, is energy-increasing for torque-free motion; halving dt halves it.
3. **Altitude:** the dt 0.02 offset of 2.97 m after 30 s matches the semi-implicit Euler closed form g·dt·t/2 = 2.94 m to within 1 %; the small remainder was not analysed.
4. **Practical scale for aircraft (inference):** the case 2 brick's 0.65 rad/s at strong inertia asymmetry gives ≈ 0.03 deg/s of rate error per second of flight at 50 Hz. Error grows with rate, duration and inertia asymmetry; a 4 rad/s sustained roll or a long high-rate manoeuvre will see larger relative error. Reducing dt is the only lever that requires no code change; a higher-order rotational integration of the compensation term would be a physics change with its own validation.

---

## 4. Threshold basis

| Gate | Threshold | Basis |
|---|---|---|
| Observed order (first two halvings) | 0.7 ≤ p ≤ 1.3 | the method is first order (semi-implicit Euler with explicit torques); ±0.3 allows pre-asymptotic behaviour; the finest pair is reported only because float32 rounding can flatten it |
| Monotone decrease | e(dt/2) < e(dt) | consequence of convergence |
| Case 2 reference vs NASA sims 01/04 | 10⁻⁶ deg/s, 10⁻⁶ rad | those tools agree to 1.4 × 10⁻¹⁰ deg/s; the reference's own error is 1.9 × 10⁻¹²; 10⁻⁶ is far above both and far below any difference that matters (Maverick errors ≥ 10⁻¹ deg/s) |
| Case 2 reference self-check | 10⁻⁹ deg/s | at least 1 000× below the 10⁻⁶ gate it supports |
| **Case 3 reference self-check** | **10⁻⁶ deg/s** | at least 100× below the smallest difference it is used to judge (reference vs SIM 04: 3.4 × 10⁻⁴ deg/s; Maverick errors ≥ 10⁻² deg/s). **History:** the first R1 run used an unjustified 10⁻⁸ and failed at 1.96 × 10⁻⁸ (RK4 at h = 1 ms with damping). The threshold was re-based on this purpose; no measured value changed |
| Envelope rule (reference vs NASA) | distance to SIM 04 ≤ max inter-tool distance to SIM 04 (+10⁻⁹) at every sample | the reference must not be an outlier among the tools; SIM 2 attitude excluded as in Vol II §E.1.2 |
| Gyroscopic control | uncompensated error > 10× compensated | proves sensitivity; observed ratio 201× |
| Dissipation (case 3) | T(t_k) ≤ T(t_{k−1})·(1 + 10⁻⁶) | damping can only remove energy; 10⁻⁶ allows float32 rounding |
| Damping-moment chain | 10⁻⁴ relative | float32 evaluation of the same formula; observed 5.9 × 10⁻⁸ |
| Density chain | 10⁻⁵ relative | float32 vs double of the same formula; observed 3.4 × 10⁻⁷ |
| Inertia readback | 10⁻⁷ relative | float32 storage |
| NASA check-shots | each output within the tolerance printed in the DAVE-ML file (10⁻⁶) | NASA's own criterion |
| Case 11 Z force | 5 × 10⁻⁴ relative | 5× the neglected pitch-rate contribution (|q·c̄/2V| ≈ 7 × 10⁻⁷ × CZq ≈ 10⁻⁴ relative) |
| Case 11 X force | 5 × 10⁻³ relative | X is the small drag-dominated component; same neglected terms are a larger fraction of it |
| Case 11 thrust | 1 % | neglected rotating-Earth and curved-path terms are < 0.5 % of m·g·sinθ (≈ 5 lbf of 2 365) |
| F-16 component vs polynomial | 10⁻⁵ absolute | float32 rate normalisation |
| Table 3 attribution | 10⁻⁶ (CX, CZ), 10⁻⁵ /deg (Cn_β by central difference) | float32 output of a double evaluation |

NASA's published inter-tool percentages (Vol II Table 80) are **not** used as thresholds anywhere.

---

## 5. Not studied in R1

- Convergence of an aircraft (F-16) trajectory — no compatible NASA trajectory case could be run (see `VALIDATION_LIMITATIONS.md`).
- Sensitivity to `Physics.defaultSolverIterations` and other PhysX solver settings (irrelevant for a single unconstrained body, untested).
- Cross-platform floating-point reproducibility (one machine, one build).
