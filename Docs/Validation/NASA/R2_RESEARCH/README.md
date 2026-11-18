# NASA 6-DoF Validation — R2 Research (research only)

Date: 2026-11-18. Follows R1 (`../NASA_6DOF_R1_PLAN.md`, PR #38). **No production code, coefficient, mass/inertia, thrust data, control law, scene or prefab was changed.** This folder holds research notes. The scripts that produced their numbers are in `Tools/nasa_6dof/research/` (stdlib Python, or numpy where noted; run with `python -I`, with the data paths as arguments). Each note tags its claims **VERIFIED** (read in a named source, or reproduced by a named script) or **INFERENCE**.

| Note | Question |
|---|---|
| `R2_DF_LINEAGE.md` | Where do R1 discrepancies D-F1…D-F4 (Morelli 1998 polynomial vs NESC F-16 tables) come from? |
| `R2_CASE4_ROUND_EARTH.md` | What does NESC atmospheric case 4 (sphere dropped over a round, non-rotating Earth) need, and how can a flat-world simulator be compared with it? |
| `R2_CASE11_13_PREREQ.md` | What is needed to run the case 11 trajectory and cases 13.1–13.4 (closed-loop F-16)? |
| `R2_ROTATIONAL_INTEGRATION.md` | How can the first-order rotational error measured in R1 be reduced without breaking the single load-application boundary? |

---

## 1. Findings

### 1.1 D-F1…D-F4 are fit errors of the published polynomial against the same data

- Morelli fitted **the same numbers that NESC tabulates**.
  - Re-fitting Morelli's Table 2 model forms to NESC `F16_aero.dml`, with his published method, gives back his Table 3 to print precision (about 3 × 10⁻⁷ relative).
  - That holds for CX, CZ, Cm, Cl, Cn and Cnδa.
- So D-F1 (CX), D-F2 (CZ) and D-F3 (Cn_β) are **representation (fit) errors**. They are neither dataset differences nor typos.
  - All 110 Table 3 values in `MavF16MorelliPolynomial.cs` match the paper.
  - The paper prints one value without its "e"; Maverick reads it correctly, and the re-fit confirms that reading.
- D-F4 (trim) follows from the CX, CZ and Cm fit errors.
- **D-F2:** about two-thirds of the CZ error at α 5° comes from Morelli's term-selection step (PSE), which dropped the α³ orthogonal function.
  - Plain least-squares quartic: CZ(5°) = −0.4364.
  - Published: −0.4753.
  - Table: −0.416.
- **New finding — β asymmetry.**
  - NESC looks up Cl0 and Cn0 by |β| and applies the sign of β afterwards.
  - Morelli fitted those two on β ≥ 0 only, with terms that are even in β. The polynomial is therefore **not antisymmetric in β**.
  - At α 45°, Cn0 is +0.012 at β +30° and **+0.576 at β −30°** (NESC +0.001).
  - This explains the R1 "unresolved" Cl/Cn sweep rows, whose worst points are at β −30°.
- **New finding — elevator labelling.** Table 3's CX and Cm come out exactly only when the ±24° elevator rows are labelled ±25°, the elevator limit in Morelli's Table 1. The effect at the D-F points is < 0.001.

### 1.2 Case 4 is directly comparable

- A vertical drop over a non-rotating sphere stays on one radial line. Curvature, Coriolis and transport-rate terms are zero, so only the magnitude of gravity differs from a flat world.
- Constant g(h₀) vs inverse-square gravity over 30 s: **+2.996 ft altitude, −0.374 ft/s speed**.
- Maverick's geometric-altitude atmosphere (R1 D-ATM) alone gives −0.63 ft and +0.064 ft/s.
- The independent reference matches NASA SIM 4 to 1 × 10⁻⁴ ft and 2 × 10⁻⁵ ft/s.
- **Recommended design:** a validation-only profile that supplies g = μ/(R+h)² through the existing gravity-through-load-set path. The F-15 research runtime already uses that path, and it refuses double application.
- The case-4 column of `Initial_Conditions.xlsx` holds stale constants. Use Vol II Table 73.

### 1.3 Case 11 / 13.x need validation-only parts, not new physics

- **What the DML models leave to the host.**
  - The NESC DML models have no dynamic states, controller integrators, actuator models or engine lag.
  - The host integrates only the rigid body.
- **The controller:**
  - two static 2×4 LQR gain matrices;
  - altitude, course and bank outer loops with limits;
  - a surface/throttle mixer.
- **Rotating Earth.**
  - Horizontal Coriolis acceleration is 0.0485 ft/s², about 786 ft of drift over 180 s.
  - A flat-world gravity of 32.0531 ft/s² reproduces SIM 5's vertical trim to 4 × 10⁻⁵ ft/s².
  - Robust comparison quantities: airspeed, α, β, θ, altitude change, q, aero forces, transient shapes.
- **R1 evaluator gap:** MathML `gt`, used 9 times in `F16_control.dml`. `cos` and `atan2` are needed only by `F16_gnc.dml`.
- **Risks:**
  - one-step control latency (the law reads the previous step's state) with high gains;
  - the trim-reference choice (SIM 4 and SIM 5 differ here);
  - the first-order integrator vs NASA's RK4/AB2 at about 100 Hz.
- **Observed spread, SIM 4 vs SIM 5, at end of run** (not thresholds): 4 ft (case 11), 2.5 ft (13.1), 2.2 ft (13.2), 5.4 ft (13.3), 11.6 ft (13.4).

### 1.4 Rotational integration: an implicit-midpoint gyroscopic torque would cut the error about 1 000× at 50 Hz

- **What Unity offers.**
  - Unity 6000.3 exposes no gyroscopic-force switch.
  - PhysX 5 has `eENABLE_GYROSCOPIC_FORCES`, but it is not reachable through Unity's API.
  - PhysX 4.1 updates velocity explicitly and then rotates the body with the end-of-step angular velocity.
- **Model check.** A double-precision model of that step with Maverick's current explicit correction reproduces the R1 Unity measurements to 4 digits:

  | dt | rate error, model | rate error, Unity | attitude, model | attitude, Unity |
  |---|---|---|---|---|
  | 0.02 | 0.9158 deg/s | 0.9159 deg/s | 5.307° | 5.306° |
  | 0.01 | 0.4535 deg/s | 0.4536 deg/s | 2.617° | 2.617° |

- **Predicted, brick at dt 0.02 (same model):**

  | Scheme | Rate error | Attitude error | Energy drift |
  |---|---|---|---|
  | Current (explicit) | 0.916 deg/s | 5.31° | +1.9 % |
  | Catto/Bullet implicit Euler | 0.88 deg/s | 5.03° | −1.8 % |
  | Predictor–corrector | 2.0 × 10⁻³ deg/s | 0.195° | +4 × 10⁻⁴ % |
  | **Implicit midpoint, 2 Newton steps** | **9.7 × 10⁻⁴ deg/s** | **0.19°** | **conserved (10⁻¹⁴)** |

  - The implicit-midpoint scheme is delivered as a torque through the existing single `AddRelativeTorque`. It writes nothing to `rb.angularVelocity` or `rb.rotation`.
  - Its attitude error stays first order, but bounded, because PhysX rotates the body with the end-of-step rate.
- **Stress case** (F-16-like inertia rolling at 4 rad/s, no aerodynamic torque): the current scheme's energy grows about 2 900×, while implicit midpoint conserves it.
- **Status:** a model prediction, not a Unity measurement. It needs its own validation, and adopting it is a **physics change**.

---

## 2. What the lead re-checked independently

| Claim | Check | Result |
|---|---|---|
| Morelli Cn fitted from NESC data | Own parser of `F16_aero.dml`, plain least squares of the 7 Cn terms on the β ≥ 0 table | All 7 coefficients equal `MavF16MorelliPolynomial.cs` to ≤ 4 × 10⁻⁷ relative |
| Cn_β sign change | Analytic ∂Cn0/∂β at β = 0 | α 24.9° |
| β asymmetry | Morelli Cn0 at α 45°, β ±30° | +0.0119 / +0.5757; NESC tables use `absbeta` |
| CZ term-selection effect | Own LS quartic on NESC CZ0 | CZ(5°) −0.4364 (table −0.416, Morelli −0.4753) |
| Case 4 reference vs NASA | Re-ran `case4_analysis.py` | SIM 4 Δh 1 × 10⁻⁴ ft, Δv 2 × 10⁻⁵ ft/s; constant-g effect +2.996 ft / −0.374 ft/s |
| Rotational model vs Unity | Re-ran `sim/brick_schemes.py` | Scheme A equals the R1 Unity results to 4 digits at dt 0.02/0.01/0.005 |
| NESC controller has no states | grep `isState` in all five DML files; count `<gt/>` in `F16_control.dml` | 0 and 9 |
| F-16 masses | `MavF16MassReference.MassSlug`; `F16_inertia.dml`; Vol II text | **637.16** (Maverick, TP-1538 20 500 lb); **637.1595** (NESC DML); **637.26** (Vol II Table 7) |

**Correction to earlier wording.** R1 was briefed to "preserve the 637.1595 vs 637.26 slug discrepancy". Both of those values are NASA's: 637.1595 is the normative DML and 637.26 is Vol II Table 7. Maverick's frozen F-16 uses 637.16 slug. The case 11 trim balance fits the DML value. The R1 suite documents never state Maverick's mass as 637.26. The PR #38 description's sentence on this point is ambiguous.

---

## 3. Decisions for the project (not taken here)

1. **β asymmetry of Morelli Cl/Cn.** Options:
   - keep it as published;
   - apply it as sign(β)·f(α,|β|);
   - offer the NESC tables as a selectable model.

   Any of the last two is a model change.
2. **Rotational scheme.** Whether to prototype the implicit-midpoint gyroscopic torque as a validated physics change. It would change every frozen trajectory baseline.
3. **Next validation phase order (recommended):**
   - case 4 first: smallest, and it needs only a validation profile;
   - then a validation-only NESC aero/thrust stack and case 11 trim/hold;
   - then 13.1/13.2, then 13.3/13.4.
4. **Optional source checks:**
   - Stevens & Lewis App. A printed tables (book). No longer needed for D-F1…D-F3.
   - Morelli's fitting script via SIDPAC, to settle the ±25° labelling. That needs a request or licence and the user's approval.

## 4. Not done

- No NESC aero/thrust profile, no LQR port, no case 4 run, no change to the rotational scheme.
- Not re-fitted: the damping tables, Clδa, Clδr and Cnδr.
- TP-1538 Table III values at the D-F points were not compared (the scan is already in the repository library).
