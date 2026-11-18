# R2_ROTATIONAL_INTEGRATION — rotational integration options for Maverick on Unity PhysX

Research only; no production code was changed. Every claim is tagged **VERIFIED** (named source, location/URL) or **INFERENCE**. "SIM" results come from a scratch double-precision model written for this note (scripts in `scratchpad/research/sim/`); they are predictions, not Unity measurements.

---

## 0. Inputs read

- `Maverick-nasa-6dof/Docs/Validation/NASA/NUMERICAL_CONVERGENCE.md`, `CASE2_TORQUE_FREE_REPORT.md` (R1 numbers: dt 0.02 → |Δω| 0.9159 deg/s, attitude 5.306°, ‖ΔH‖/‖H‖ 8.589e-3, ΔT/T +1.912e-2; order 1.00–1.03; gyro OFF → PhysX holds ω constant, 4.4e-4 deg/s change in 30 s).
- `MavSixDoFBody.cs` (read-only). **VERIFIED (file, `ApplyGyroscopicCompensation` ~L1128, `MavGyroscopicMoment.TryCompute` L113–132, `TryApplyLoadSet` L485–486):** the correction is `−ω × (Iω)` evaluated from `debugState.aeroBodyRatesRadSec` (Rigidbody.angularVelocity at the *start* of the step, converted to aero-body axes), inertia from `rb.inertiaTensor`/`inertiaTensorRotation` (cached), added to the load set as `inertialCorrectionMomentAeroBodyNm`, and leaves through the single `rb.AddRelativeTorque(..., ForceMode.Force)`. `ProjectSettings/TimeManager.asset`: Fixed Timestep 0.02.
- The only per-step state writes are the load application; `rb.angularVelocity`/`rb.rotation` are written only in `TryApplyInitialKinematicState` (initialization, refused while armed). **VERIFIED (file, ~L795–840).**

---

## 1. Unity / PhysX facts

| # | Claim | Tag / source |
|---|---|---|
| 1.1 | PhysX 4.1's `PxRigidBodyFlag` enum has **no** gyroscopic flag (members: eKINEMATIC … eRETAIN_ACCELERATIONS, eFORCE_*_NOTIFICATIONS, eRESERVED). | VERIFIED — `NVIDIAGameWorks/PhysX` branch 4.1, `physx/include/PxRigidBody.h` (https://raw.githubusercontent.com/NVIDIAGameWorks/PhysX/4.1/physx/include/PxRigidBody.h) |
| 1.2 | PhysX 5.x has `PxRigidBodyFlag::eENABLE_GYROSCOPIC_FORCES` ("Enables computation of gyroscopic forces on the rigid body"); the 5.5 guide says gyroscopic forces are opt-in and often disabled for efficiency/stability, needed for e.g. the Dzhanibekov effect. Default not stated. | VERIFIED — https://nvidia-omniverse.github.io/PhysX/physx/5.4.1/_api_build/structPxRigidBodyFlag.html ; https://nvidia-omniverse.github.io/PhysX/physx/5.5.1/docs/RigidBodyDynamics.html |
| 1.3 | Unity 6000.3 `Rigidbody` API exposes no gyroscopic switch (28 properties incl. `inertiaTensor`, `inertiaTensorRotation`, `maxAngularVelocity`, `solverIterations`, `solverVelocityIterations`; 25 methods). Physics Project Settings (6000.3) list solver type PGS/TGS, default max angular speed 50 rad/s, enhanced determinism, but nothing gyroscopic. | VERIFIED — https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/rigidbody ; https://docs.unity.com/en-us/engine/6000.3/manual/unity-editor/editor-settings-reference/comp-manager-group/class-physics-manager |
| 1.4 | Unity's last publicly stated PhysX upgrade is 3.4 → 4.1 (2019.3). No Unity 6 doc found stating PhysX 5; Unity's May-2025 status post frames PhysX 5 only as a possible future custom backend. | VERIFIED (absence-of-evidence) — https://blog.unity.com/en/technology/physics-updates-in-unity-2019-3 ; https://discussions.unity.com/t/physics-development-status-and-next-milestones-may-2025/1637988 |
| 1.5 | Built-in Unity 6000.3.16f1 PhysX does **not** apply ω×Iω. | VERIFIED empirically by Maverick R1 (gyro OFF: ω constant to 4.4e-4 deg/s over 30 s, `CASE2_TORQUE_FREE_REPORT.md` §3) |
| 1.6 | PhysX 4.1 body integrator (`DyBodyCoreIntegrator.h`): velocity update is explicit (gravity, then damping `1 − c·dt` clamped at 0, then clamp to max linear/angular velocity); orientation update is a closed-form exponential map using the **post-solve (end-of-step) angular velocity**, half-angle sin/cos, then `getNormalized()`; |ω| capped at 1e7 inside integrateCore. No ω×Iω term. Same file in PhysX 5 main also has no gyroscopic term (it lives elsewhere when the flag is on). | VERIFIED — https://raw.githubusercontent.com/NVIDIAGameWorks/PhysX/4.1/physx/source/lowleveldynamics/src/DyBodyCoreIntegrator.h ; https://raw.githubusercontent.com/NVIDIA-Omniverse/PhysX/main/physx/source/lowleveldynamics/src/DyBodyCoreIntegrator.h |
| 1.7 | So Unity's rotational step is: ω⁺ = ω + h·I_w⁻¹(R)·τ (start-of-step world inertia), then R⁺ = exp(h·ω̂⁺)·R. This is symplectic (semi-implicit) Euler; with explicit −ω×Iω it reproduces R1 to 3–4 digits (SIM: 0.9158 deg/s, 5.3069°, 8.590e-3, +1.912e-2 vs Unity 0.9159, 5.306, 8.589e-3, +1.912e-2). | INFERENCE, strongly supported by SIM-vs-Unity agreement |
| 1.8 | Because R⁺ = exp(hω̂⁺)R rotates about ω⁺ itself, the body rate seen at the next step equals R⁺ᵀω⁺ = Rᵀω⁺; body-frame rate sequence is therefore independent of attitude, and any body-frame rate update can be realised exactly by choosing τ_body = I_b(ω_b,target − ω_b)/h. | INFERENCE (algebra; confirmed by SIM) |
| 1.9 | `Physics.Simulate(step)` advances the whole scene; needs `SimulationMode.Script`; FixedUpdate still runs at `fixedDeltaTime` independently; Unity warns steps > 0.03 s are inaccurate. Substepping via repeated Simulate calls therefore requires Maverick's loads to be recomputed per sub-step (FixedUpdate won't fire), i.e. a scheduling change. | VERIFIED — https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/physics/simulate |
| 1.10 | `solverIterations`/TGS affect constraints/contacts only, not a free body's rotational integration. | VERIFIED for the docs' wording (Rigidbody page: "how accurately Rigidbody joints and collision contacts are resolved"); irrelevance to free flight is INFERENCE |
| 1.11 | `maxAngularVelocity` is a clamp (default 50 rad/s); Maverick measured 50. A corrected scheme must still never hit it. `inertiaTensorRotation` only matters for products of inertia (F-16 Ixz); Maverick already maps it. | VERIFIED (docs 1.3 + R1 report); INFERENCE for relevance |
| 1.12 | `ArticulationBody` (reduced-coordinate articulations) is not a route: would change the physics component type (prefab/scene change, out of scope) and I found no Unity documentation that it applies ω×Iω for a free root. | INFERENCE (not verified either way) |
| 1.13 | Unity's DOTS **Unity Physics** package (not PhysX) has an "Enable Gyroscopic Torque" step option — irrelevant to a GameObject Rigidbody. | VERIFIED — https://docs.unity3d.com/Packages/com.unity.physics@6.5/manual/component-step.html |

---

## 2. Literature

| Method | Order (rates / attitude) | Energy & momentum, torque-free | Cost | Torque-only (keeps Maverick's single AddRelativeTorque)? |
|---|---|---|---|---|
| **Explicit gyro + symplectic Euler (current)** | 1 / 1 | Energy **grows**; Catto: "kinetic energy grows unbounded" for the Dzhanibekov test | trivial | yes (status quo) |
| **Implicit (backward) Euler on gyro, 1 Newton step** — Catto GDC 2015 slides 71–76; Bullet `computeGyroscopicImpulseImplicit_Body` (default flag `BT_ENABLE_GYROSCOPIC_FORCE_IMPLICIT_BODY`) | 1 / 1 | Energy **decays** ("kinetic energy dissipates over time", slide 82); goal is stability, not accuracy | one 3×3 solve | yes: τ = I_b(ω₂−ω₁)/h |
| **Predictor–corrector (Heun / explicit trapezoid on gyro)** | 2 / 1 (attitude via end-of-step rate) | Energy drift O(h²), small, sign not guaranteed; not unconditionally stable | 2 gyro evaluations, no solve | yes |
| **Implicit midpoint on Euler's equations** (Gauss–Legendre s=1) | 2 / 1 (2 if staggered, §3) | **Exactly** conserves both quadratic invariants T = ½ωᵀIω and ‖Iω‖² (Cooper's condition bᵢaᵢⱼ + bⱼaⱼᵢ − bᵢbⱼ = 0 holds: ½+½−1 = 0); symplectic | 1–2 Newton steps, 3×3 solves | yes |
| **McLachlan / Reich / Touma–Wisdom Lie–Poisson splitting** | 2 (Strang); higher by composition | Exact Casimir ‖L‖; Poisson/symplectic → energy error bounded, no drift | explicit, a few axis rotations | rates: yes (realise via τ); attitude part of the splitting needs ownership of R |
| **Buss 2000 (augmented 2nd-order, 3rd, 4th; Lie-group Taylor; optional energy-correcting step)** | 2–4 | optional exact energy correction step | cheap, single rotation per step | no — defines the attitude update; needs `rb.rotation` writes |
| **RKMK / Crouch–Grossman Lie-group RK** | arbitrary (RK4 → 4) | not structure-preserving in general; energy drift O(h^p) | 4 stages, exp maps | no — owns R and ω |
| **Quaternion RK4 + normalisation** | 4 on rates; quaternion normalisation is a projection | drift O(h⁴), slow | 4 stages | no — owns R and ω |

Sources:
- Catto, "Numerical Methods", GDC 2015, slides 61–82 (gyroscopic section), refs slide 84 incl. Lacoursière "Stabilizing Gyroscopic Forces in Rigid Multibody Simulations". **VERIFIED** — https://box2d.org/files/ErinCatto_NumericalMethods_GDC2015.pdf (text extracted from the PDF; slide numbers from the deck).
- Bullet `btRigidBody.cpp`: `computeGyroscopicForceExplicit` (clamped), `computeGyroscopicImpulseImplicit_Body` (body frame, J = I_b + h(skew(ω_b)I_b − skew(I_bω_b)), one Newton step, returns Δω), `_World` variant. **VERIFIED** — https://raw.githubusercontent.com/bulletphysics/bullet3/master/src/BulletDynamics/Dynamics/btRigidBody.cpp (read for method description only; nothing copied).
- McLachlan, "Explicit Lie-Poisson integration and the Euler equations", PRL 71:3043 (1993): split H into exactly solvable rotations; Poisson map; Casimir preserved; symmetric composition 2nd order. **VERIFIED** — https://arxiv.org/abs/chao-dyn/9304011
- Buss, "Accurate and efficient simulation of rigid-body rotations", J. Comput. Phys. 164:377–406 (2000): 2nd-order-with-3rd-order-correction, 3rd and 4th-order single-step Lie-group methods; energy-correction step; "superior to naive Runge-Kutta or predictor-corrector". **VERIFIED** — https://mathweb.ucsd.edu/~sbuss/ResearchWeb/accuraterotation/index.html
- Iserles, Munthe-Kaas, Nørsett, Zanna, "Lie-group methods", Acta Numerica 9:215–365 (2000); Munthe-Kaas, BIT 38:92–111 (1998). **VERIFIED (bibliographic)** — https://cambridge.org/core/journals/acta-numerica/article/liegroup-methods/856125FF1EAF7762DEF6E37EEBA9CA5F ; https://doi.org/10.1023/A:1021950708869 (content characterisation in table: INFERENCE from general knowledge, not re-read)
- Cooper, "Stability of Runge-Kutta methods for trajectory problems", IMA J. Numer. Anal. 7:1–13 (1987) — quadratic-invariant condition. **VERIFIED (condition as quoted in search result citing Cooper; Hairer–Lubich–Wanner GNI §IV.2 restates it — not fetched)**.
- Reich (1994), Touma & Wisdom (1994), Simo & Wong (1991) energy–momentum schemes: named for completeness, **not fetched** — INFERENCE.

---

## 3. Quantitative prediction for the NASA brick (SIM, double precision, PhysX step modelled per §1.6–1.7)

Max over t = 2…30 s, same metrics as R1. "Torque-only" = Maverick computes τ_body and hands it to the existing single `AddRelativeTorque`; no state writes.

| dt 0.02 | \|Δω\| deg/s | attitude deg | ‖ΔH‖/‖H‖ | ΔT/T (signed) | order (rates/att) |
|---|---:|---:|---:|---:|---|
| A explicit (current; matches Unity R1) | 0.9158 | 5.307 | 8.59e-3 | **+1.91e-2** | 1 / 1 |
| B implicit Euler, 1 Newton (Catto/Bullet) | 0.8815 | 5.032 | 7.41e-3 | **−1.76e-2** | 1 / 1 |
| C predictor–corrector (Heun gyro) | 2.03e-3 | 0.195 | 3.37e-3 | +3.8e-6 | 2 / 1 |
| D implicit midpoint, 2 Newton steps | 9.7e-4 | 0.192 | 3.36e-3 | ~1e-14 (exact) | 2 / 1 |
| F = D + staggered half-step rate (see below) | 9.7e-4 (vs ref at t−h/2) | **1.6e-3** | 2.4e-3 (artifact) | +8e-9 | 2 / 2 |

Halving dt: A, B halve; C, D rates quarter, attitude halves; F rates and attitude quarter (dt 0.01: F att 4.1e-4°; dt 0.005: 1.0e-4°). (SIM)

Key readings (all INFERENCE from SIM unless stated):
1. **Catto/Bullet implicit Euler does not fix accuracy.** It is still first order (−4 % error vs current) and only flips the energy sign from +1.9 % to −1.8 %. It is a *stability* tool (bounded energy at large ω·h), matching Catto's own description (VERIFIED slide 82: energy dissipates).
2. **Midpoint-type gyro (C or D) is the big win** and is torque-only: rate error ~940× smaller, attitude ~28× smaller, energy drift eliminated (D exactly; C to O(h²)).
3. **The remaining first-order attitude error is caused by PhysX using the end-of-step rate for the whole orientation step.** In D it is a bounded phase offset, not secular drift: attitude error at t = 2/10/20/30 s is 0.105/0.103/0.110/0.056° for D vs 0.14/0.72/2.36/5.31° for A.
4. ‖ΔH‖/‖H‖ (inertial vector) in C/D is dominated by that attitude offset; ‖Iω‖ itself is exact in D.
5. **Staggered interpretation (F):** PhysX's symplectic Euler is leapfrog if the stored rate is read as ω(t − h/2). Initialise once with a backward half step (ω₋½ ≈ ω₀ − (h/2)ω̇₀ + (h²/8)Jω̇₀ — an initialization-time write, which `TryApplyInitialKinematicState` already permits), keep the midpoint gyro solve, and attitude becomes 2nd order. Costs: telemetry/sensor rates are then half a step old (average consecutive values for ω(tₙ)); rate-dependent aero damping evaluated from the stored rate becomes O(h) again unless also taken at the midpoint; validation must compare rates at t − h/2 or the comparison itself shows an O(h) error (E in the scripts shows this trap: 0.095 deg/s apparent error from mis-timed comparison).
6. **Stress case (SIM, F-16-like inertia 9496/55814/63100, Ixz −982, torque-free 30 s):** at ω₀ = (4, 0.5, 0.3) rad/s, dt 0.02, explicit A **diverges** (T × ~2900, attitude ~166°), B loses 10 % energy and 172° attitude, C 2.2 deg/s / 4.9°, D 1.1 deg/s / 2.2° with T exact. At ω₀ = (1,1,1) rad/s: A 21 deg/s / 140°, D 0.026 deg/s / 1.1°. Real aircraft have aero damping that would mask much of A's numerical anti-damping (explicit Euler on a mode of frequency λ adds growth ≈ hλ²/2 s⁻¹, ≈ 0.1 s⁻¹ for the roll-coupling mode at 4 rad/s — INFERENCE), but this is a known-wrong energy source during high-rate rolls.

---

## 4. Options ranked for Maverick

1. **(c/b-hybrid → recommended) Implicit-midpoint gyroscopic correction expressed as a torque.** Solve I_b(ω₂ − ω₁) + h·ω̄ × I_bω̄ = h·T_ext, ω̄ = (ω₁+ω₂)/2, in aero-body axes with 1–2 Newton steps (J = I_b + (h/2)(skew(ω̄)I_b − skew(I_bω̄))); set the inertial-correction moment to I_b(ω₂ − ω₁)/h − T_ext. Stays inside the existing load set and the single `AddRelativeTorque`; no `rb.angularVelocity`/`rb.rotation` writes. Predicted (brick, dt 0.02): ~1e-3 deg/s, ~0.19° bounded, T exact. Cost: two 3×3 solves per aircraft per step. (INFERENCE)
   - Simpler variant: **Heun predictor–corrector** (explicit, no solve): nearly identical accuracy, small O(h²) energy drift, conditionally stable. Good first step if a solve is unwanted. (INFERENCE)
2. **(d-lite) Staggered/leapfrog interpretation on top of option 1** for second-order attitude; requires an initialization-time half-step and a telemetry/aero-rate timing convention. Still torque-only per step. Second phase. (INFERENCE)
3. **(a) Reduce fixed dt.** No code change; error and energy drift scale ~dt (R1 VERIFIED); 4× cost for 4× accuracy, scene-wide (all FixedUpdate users, weapons/AI timing) — far worse value than option 1 (option 1 at dt 0.02 beats the current scheme at dt 0.0025 by >100× on rates). Substepping via `Physics.Simulate` changes scheduling (FixedUpdate does not fire per sub-step). (VERIFIED for the Unity facts; value judgement INFERENCE)
4. **(b) Catto/Bullet implicit Euler.** Torque-only, unconditionally damps energy; use only if a stability guarantee at very high ω·h is wanted; it does not improve accuracy (SIM). Could be a fallback branch when the midpoint Newton fails to converge. (INFERENCE)
5. **(d) Take over rotational integration** (Buss, Lie–Poisson splitting, RKMK, quaternion RK4) by writing `rb.rotation`/`rb.angularVelocity` each step. Highest attainable accuracy but conflicts with the single-load-application boundary, fights PhysX's own integrate step (double integration unless rates are zeroed / body made kinematic), breaks contacts/interpolation semantics, and would require a documented ownership change. Not recommended now. (INFERENCE)

---

## 5. What an independent check must verify

- **Torque-free invariants:** T(t) must not grow (option 1: |ΔT|/T ≲ float32 noise, ~1e-6 over 30 s; no monotone trend); ‖I_bω_b‖ conserved to the same level; inertial H vector drift reported separately (it measures attitude error).
- **Observed order:** rates p ≈ 2 for option 1 (dt 0.02→0.0025), attitude p ≈ 1 (≈ 2 only with staggering). An attitude p ≈ 2 claim without staggering would indicate a measurement bug.
- **Sample timing:** with staggering, compare stored rates to the reference at t − h/2 (or average consecutive rates); otherwise the comparison itself introduces O(h).
- **dt coupling:** the correction depends on h; it must use the step actually integrated (validation calls `Physics.Simulate(dt)` with dt ≠ fixedDeltaTime). A mismatch silently degrades to first order.
- **Torque→Δω mapping assumption:** PhysX uses start-of-step world inertia and no other angular terms; requires angularDamping = 0, ω below `maxAngularVelocity`, no contacts. Assert Δω_measured = I⁻¹τh per step (R1 already asserts damping 0 and the 50 rad/s limit).
- **Single application / no double counting:** correction still added once, still in the load set; gyro-OFF control must still differ by >10×.
- **Products of inertia:** test with Ixz ≠ 0 (F-16) — the brick has none.
- **Newton robustness:** at high ω·h (≥ 0.1) check 2 iterations converge; fall back to implicit Euler (B) if not.
- **Damped case 3** must still satisfy T(tₖ) ≤ T(tₖ₋₁)(1+1e-6).

---

## 6. Production flight sims (context only — no code/data copied)

- **JSBSim** integrates its own state (not a game-physics engine). FGPropagate's constructor defaults: rotational rate **rectangular Euler**, translational rate Adams–Bashforth 2, rotational position (quaternion) **rectangular Euler**, translational position Adams–Bashforth 3; selectable options include trapezoidal, AB2–AB5, **Buss1** (first-order exponential map, "exact solution … when the rate is constant"), **Buss2** (Buss's augmented second-order method) and local linearization (Barker et al.); quaternion normalized except on the Buss paths. FGAccelerations' rotational equation subtracts ω_i × (Jω_i) and adds planet-rotation terms. **VERIFIED** — https://raw.githubusercontent.com/JSBSim-Team/jsbsim/master/src/models/FGPropagate.cpp ; …/FGAccelerations.cpp (read for description only). So JSBSim's *default* rotational scheme is also first order with an explicit gyro term; accuracy is bought with a small dt (commonly 120 Hz — INFERENCE, not verified here) and optional higher-order integrators.
- Game engines: PhysX ≤4 omits the term (§1); PhysX 5 makes it opt-in; Bullet ships explicit-clamped and implicit variants; Catto (Blizzard) moved from "drop it" to implicit Euler for stability. **VERIFIED** (sources in §1–2).

## 7. Scratch artifacts

`Tools/nasa_6dof/research/sim/` — `brick_schemes.py` (A–E), `brick_staggered.py` (F–H), `f16_stress.py`, `d_growth.py`. Double precision, PhysX step modelled from the 4.1 source; Unity float32 will differ in the last digits.
