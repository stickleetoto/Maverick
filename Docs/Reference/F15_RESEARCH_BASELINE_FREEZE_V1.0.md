# F-15 AFIT/BAUMANN/DAVISON RESEARCH BASELINE V1 — FROZEN

> **RESEARCH CONFIGURATION ONLY** (`F15_AFIT_BAUMANN_DAVISON_MACH06_20K_RESEARCH`, the AFIT/Baumann/Davison model).
>
> This is the closeout of the research runtime path. The source model, reproduced off the body in WP-3A–3E, was flown through the real Unity runtime path in Play Mode:
> `MavSixDoFBody` → PhysX `Rigidbody` → `MavFlightPhysicsOwnership`.
> It held its source equilibria, and it matched the source RHS / RK4 up to first-order integration error.
>
> **Not claimed:** NASA 836, the production aircraft, or gameplay. See §2.

**Branch / PR:** `claude/f15-full-implementation` · Draft PR #27 (never merged) · base `sol/f15-r2-lateral`.
**Start:** `3918a22` (WP-4A).
**Unity:** 6000.3.16f1.

---

## 1. What is COMPLETE

| Layer | Status | Where |
|---|---|---|
| Public-source research configuration | profile, geometry, Davison mass/inertia, fixed 8,300 lbf total thrust with its 0.25-in moment | `F15_RESEARCH_PROFILE_V1.0.md` (WP-1) |
| Static source reproduction | Table VII stored as printed; 170 equilibria close to print precision in six axes | `F15_TABLE_VII_EQUILIBRIUM_VALIDATION_V1.0.md` (WP-3A) |
| Symmetric trim | 89 / 89 recovered from perturbed starts; 356 / 356 converged | `F15_RESEARCH_TRIM_SOLVER_V1.0.md` (WP-3B) |
| Turning trim | 80 non-symmetric states + the pitchfork; 320 / 320 converged | `F15_RESEARCH_TURNING_TRIM_V1.0.md` (WP-3C) |
| Eigenvalue stability | 127 stable / 40 unstable / 3 near-neutral; every disagreement with the source's caption explained | `F15_RESEARCH_STABILITY_ANALYSIS_V1.0.md` (WP-3D) |
| Nonlinear source-RHS validation | 35 RK4 cases within 1 % of the eigenvalues; conflict audit | `F15_RESEARCH_TIME_DOMAIN_STABILITY_V1.0.md`, `F15_RESEARCH_STABILITY_CONFLICT_AUDIT_V1.0.md` (WP-3E) |
| Runtime environment semantics | source fixed density (D11) + source gravity through the load set, both mandatory; static stabilator hold; injection | `F15_RESEARCH_RUNTIME_PREREQUISITES_V1.0.md` (WP-4A) |
| **Unity runtime equilibrium validation** | **this closeout**: research ownership state, deterministic Play Mode harness, symmetric + turning holds, dt refinement, A/B/C against the source RK4, frame/sign/Ixz audit, fail-closed environment | this document |

## 2. What is NOT CLAIMED

- actual NASA 836 fidelity (the exact path stays FAIL-CLOSED)
- production F-15 fidelity
- exact production FCS (no numeric law; the law outputs neutral)
- sourced physical actuator limits or rates (−25…−5° stays the **research demonstrated range**, not a hard stop)
- a sourced operational thrust deck (F100 stays zero; the research thrust is the source's 8,300 lbf constant)
- gameplay-ready pilot control (none exists; nothing is wired to a scene or prefab)

---

## 3. Research ownership — the minimum state

`Core/MavFlightPhysicsOwnership.cs` gets one new owner, `F15AfitResearch = 4`, documented as research validation only. It is **not** gameplay F15Replacement, which still does not exist.

| Rule | Implementation |
|---|---|
| Replacement writes, legacy gated off | `IsReplacementPhysicsAllowed` / `IsLegacyPhysicsAllowed` treat it like `F16Replacement` |
| One gravity source | `ResolveGravityProvider` → `ReplacementLoadSet`, so `Rigidbody.useGravity` is off and the load set carries the source G |
| **Safety hold, OFF by default** | `allowResearchValidationOwnership = false`. A new authority is still `Legacy`, and the body stays unarmed. |
| **Exact configuration only** | `TryEnterF15AfitResearchOwnership(body, grant, out error)` checks the grant's id and the body's built profile id against `F15AfitResearchConfigurationId`, by ordinal. It refuses NASA 836, trailing-space, lower-case and suffixed ids, and the F-16. |
| **Environment mandatory** | the body's resolved environment must be `ResearchSourceFixedDensity` **and** own gravity through the load set. There is no fallback to ISA, `Physics.gravity` or another profile. |
| **Exactly one live owner** | refused if any other `MavSixDoFBody` is armed, if a replacement stack already owns physics, or if a legacy writer is active |
| Aircraft-layer grant | `IMavResearchOwnershipGrant` (Core) ← `F15/MavF15ResearchOwnershipGrant`: the WP-4A authority grants the body, and its preparation report has no blocker and a source-equivalent environment |
| **Fails closed every step** | `VerifyResearchOwnership()` runs first in the authority's `FixedUpdate`. If the body, id or environment stops holding, the owner goes to `Fault`, which disarms the body before it steps. |
| Unchanged | `Legacy`, `Shadow`, `F16Replacement` and `Fault` rules and gravity providers; `TryEnterShadow` / `TryActivateReplacement` refuse while research owns physics |

**Verified in Play Mode:** `[O1]`–`[O5]` (5 / 5), and `[N2]` for mid-run environment loss.

---

## 4. Deterministic Play Mode harness

- **Editor driver** `Editor/MavF15ResearchRuntimeFlightValidation`:
  - `RunBatch`, argument `-f15rtOut <path>`;
  - enters Play Mode on the **empty unsaved batch scene**, and refuses with a saved scene open;
  - harvests the report before leaving Play Mode;
  - exits 0 only on PASS.
- **Runner** `Validation/MavF15ResearchRuntimeFlightValidationRunner`:
  - runs at execution order −500, before the authority (−400), actuator (−200) and body (−100);
  - no user input, camera or UI;
  - every rig is a fresh in-memory GameObject, destroyed at the end of its run.
- **Run sequence:**
  1. inject the equilibrium with the WP-4A initializer;
  2. attach the authority;
  3. enter the research owner;
  4. step N fixed steps;
  5. return to Legacy;
  6. `DestroyImmediate`.
- **Determinism:** `Time.captureFramerate = 50`, a fixed run order, and a fresh rig per run. **Two consecutive headless runs produced byte-identical reports.**
- **Environment and plant:**
  - research profile in `SourceReproduction`, `SourceFixedDensity` + `SourceGravity`;
  - 8,300 lbf total fixed thrust with the +0.25-in moment;
  - static stabilator hold at the equilibrium's printed setting;
  - neutral command source; no throttle, F100, pilot, CAS gain, actuator dynamics, travel or rate.

**Physics settings recorded (first armed step):**

| Setting | Value |
|---|---|
| `fixedDeltaTime` | 0.0199999921 s (Unity's stored 0.02; the series uses 0.009999993 and 0.004999993) |
| `captureFramerate` / `maximumDeltaTime` | 50 / 0.333333343 |
| `simulationMode` / `autoSyncTransforms` | FixedUpdate / False |
| `Physics.gravity` | (0, −9.81, 0), **not used**: `useGravity` off |
| solver iterations / sleep threshold / default max angular speed | 6/1 / 0.005 / 50 |
| mass / inertia tensor | 16,782.918 kg / (225,906.4, 253,451.4, 34,537.9) kg·m², rotation (359.6451, 0, 0) |
| damping / interpolation / collision / colliders | 0/0 / None / Discrete / 0 |

---

## 5. Representative equilibria

| Case | Table VII | Why this one |
|---|---|---|
| **Symmetric** | point **36**: stab −15.14529°, α 17.4865°, θ 14.5460°, V 300.800 ft/s, γ −2.94043° | Stabilator at the centre of −25…−5. α is 3.2° above the CMMQ band (≤ 14.3°) and far from the pitchfork. All modes are stable; the slowest is −2.1e-2 /s. |
| **Turning** | point **150**: stab −6.45954°, α 10.5556°, β −0.03014°, p 0.007370, q 0.010680, r −0.027765 rad/s, θ 13.9152°, φ −21.040°, V 391.118 ft/s. WP-3C: ψ̇ −0.030648 rad/s, γ 4.0298° | All five modes stable (WP-3D/3E). About 20° of bank from the fold at 136 and the fork at 165. |

Both are re-solved from the printed rows by the WP-3B / WP-3C solvers at start-up (`[EQ]`).

---

## 6. Results (Unity 6000.3.16f1, headless Play Mode — **39 / 0**, twice, byte-identical)

### 6.1 Symmetric trim hold — point 36, 30 s — **HELD**

| dt | max \|x − eq\| (worst state) | ψ̇ | γ (predicted −2.94043°) | load residual |
|---|---|---|---|---|
| 0.02 | 1.7e-7 (q) | 0 | −2.94043° | 1.6e-6 g, 3.6e-8 qSc̄ |
| 0.01 | 1.5e-7 | 0 | −2.94043° | 3.1e-6 g, 3.3e-8 qSc̄ |
| 0.005 | 1.5e-7 | 0 | −2.94043° | 3.1e-6 g, 3.3e-8 qSc̄ |

- Every state stays at the float32 floor for 30 s.
- World velocity stays (0, −4.703, 91.563) m/s, and angular velocity is 0.

### 6.2 Turning trim hold — point 150, 30 s — **HELD up to integration error**

| dt | θ drift | φ drift | V drift (rel.) | p / q / r drift | ψ̇ (WP-3C −0.030648) | γ (WP-3C 4.02976°) |
|---|---|---|---|---|---|---|
| 0.02 | 1.9e-4 | 8.3e-5 | 7.5e-5 | 6.9e-6 / 1.3e-5 / 5.3e-6 | −0.030652 | 4.03575° |
| 0.01 | 9.5e-5 | 4.3e-5 | 3.7e-5 | 9.8e-6 / 6.6e-6 / 4.2e-6 | −0.030650 | 4.03279° |
| 0.005 | 4.8e-5 | 2.5e-5 | 1.9e-5 | 5.0e-6 / 4.5e-6 / 1.6e-6 | −0.030649 | 4.03128° |

- **Errors against WP-3C, at dt / dt/2 / dt/4:**
  - heading rate: 4.2e-6 / 2.0e-6 / 1.3e-6 rad/s;
  - flight-path angle: 6.0e-3 / 3.0e-3 / 1.5e-3°;
  - largest drift: 1.9e-4 / 9.5e-5 / 4.8e-5.
- **Each halves with dt.**
- The world angular velocity is (0, −0.03065, 0) rad/s, about the vertical only (horizontal ≤ 1.4e-5).
- Load residuals are ≤ 2.0e-4 g and ≤ 2.6e-6 qSc̄.

### 6.3 dt refinement — dt, dt/2, dt/4 (max over time of \|Unity − source RK4\|)

| Case | dominant state | dt | dt/2 | dt/4 | observed order |
|---|---|---|---|---|---|
| S-HOLD | q | 1.7e-7 | 1.6e-7 | 1.6e-7 | float floor |
| T-HOLD (30 s) | θ | 1.91e-4 | 9.46e-5 | 4.80e-5 | 1.01 / 0.98 |
| S-PERT (Δα +0.5°, 10 s) | α | 1.09e-4 | 5.43e-5 | 2.73e-5 | 1.00 / 1.00 |
| T-PERT (Δβ +0.5°, Δp +0.02 rad/s, 10 s) | p | 2.24e-3 | 1.06e-3 | 5.15e-4 | 1.08 / 1.04 |

- **Every non-floor state is first order**, e.g. T-PERT β 1.01/1.00, φ 1.01/1.01, V 1.02/1.02; S-PERT q, θ 1.00/1.00.
- That is PhysX's semi-implicit Euler: loads held constant over each step.
- The differences therefore behave like **numerical integration error**, not a model or mapping mismatch.
- No target value was set; the orders are observed.

**Judgement rule, stated transparently.**
- The **first** run of this harness required every state to shrink strictly at each halving.
- T-HOLD p (6.9e-6, 9.8e-6, 5.0e-6) did not. It sits at ~1e-5 rad/s, where it follows the body's float32 moment residual (1.6e-6, 2.6e-6, 1.3e-6 qSc̄), not dt.
- The published rule is:
  - the **dominant** difference shrinks at every halving;
  - every other state shrinks net from dt to dt/4, or stays at the float floor (2e-6);
  - non-monotone states are labelled in the report.
- No number was changed to pass.

### 6.4 WP-3E reference — A (source RHS / RK4), B (Unity), C = B − A, at dt/4 = 0.005 s

- A is RK4 (h ≈ 0.0025 s) of the unchanged source RHS.
- A starts from **Unity's own t = 0 state**, on Unity's stored timestep.

| Case | A drift from eq. | B drift from eq. | C = max \|B − A\| |
|---|---|---|---|
| S-HOLD | ≤ 5.6e-8 | ≤ 1.5e-7 | ≤ 1.6e-7 (float floor) |
| T-HOLD | ≤ 6.1e-8 | θ 4.8e-5, φ 2.5e-5, V 1.9e-5 | θ 4.8e-5, φ 2.5e-5, V 1.9e-5 |
| S-PERT | α 8.7e-3, q 7.3e-3, θ 1.0e-2 | the same | α 2.7e-5, q 1.5e-5, θ 2.7e-5 |
| T-PERT | p 9.9e-2, r 2.2e-2, φ 2.9e-2, β 8.7e-3 | the same | p 5.2e-4, r 1.0e-4, φ 2.8e-4, β 1.1e-4 |

- Unity's perturbed trajectories follow the source's to 0.1–0.5 % of their excursion, and C shrinks first-order with dt (§6.3).
- In the holds, A stays on the equilibrium to 1e-7. B's turning drift is therefore Unity's integration error, and it halves with dt.
- **No tuning.**

### 6.5 Frame / sign audit — first PhysX step of T-PERT (every state non-zero)

| Check | dt 0.02 | dt 0.01 | dt 0.005 |
|---|---|---|---|
| **F1 force + gravity direction:** \|Δv − R F/m dt\| / \|Δv\| | 3.4e-5 | 6.9e-5 | 1.4e-4 |
| position: \|Δx − v₁ dt\| (m) | 9.6e-6 | 6.3e-6 | 4.1e-4 |
| **F2 moment:** \|Δω − I⁻¹ R τ dt\| / \|Δω\| | 4.2e-7 | 1.8e-7 | 3.4e-7 |
| **F3 Ixz / gyro:** measured (ṗ, q̇, ṙ) vs source RHS | 3.2e-7 | 7.6e-8 | 5.3e-7 |

- Measured (ṗ, q̇, ṙ) = (−0.5565, −5.339e-4, 0.09663) rad/s².
- An Ixz-flipped inertia would give (−0.5491, −5.336e-4, 0.09072), which is **1.67e-2 away**. The source sign is the one flown.
- **F4 initialization:** α, β, p, q, r, θ, φ and V read back to 2.8e-8, heading 0, at t = 0. This covers linear and angular velocity and the Euler mapping.
- Gravity: the body's load set carries the source G along world −Y. `useGravity` is off every step.
- **No mapping bug was found.** No mapping was changed.

**Ixz / gyro-sign dynamic check (the turning case).**
- With `applyBackendGyroscopicCompensation` off, the steady turn departs from the source: max \|p, q, r − source\| is 1.68e-4 rad/s against 1.30e-5 with it on (13×) (`[N1]`).
- So the turn is a sensitive test of the ω×Iω term, and with the term the body follows the source.

### 6.6 Environment fail-closed (`[N2]`)

- At step 10 of a live run, the profile's density policy is switched to the standard atmosphere.
- On the next step the research owner **faults** and the body is disarmed. **No further load is applied.**
- There is no fallback to ISA or Unity gravity.
- Ownership also refuses standard density and Unity gravity up front (`[O4]`).

### 6.7 Static surface policy

- The stabilator is held by `MavF15AfitResearchStaticSurfaceHold` at the equilibrium's printed setting: −15.14529° and −6.45954°.
- There is no pilot mapping, actuator dynamics, travel rate, hard stop or FCS.
- −25…−5° remains the **RESEARCH DEMONSTRATED RANGE**.

---

## 7. Runtime bugs found

**None in the model, the body, the ownership mapping or the physics path.** Every failure in the harness's development was the harness's own. None was fixed by changing physics.

| First-run failure | Cause | Fix |
|---|---|---|
| `[M]` dt check, 13 runs | exact float compare against 0.02; Unity stores 0.0199999921 (and 0.004999993 for 0.005, 1.4e-6 relative) | relative tolerance 1e-5; value printed per run |
| `[D]` T-HOLD | strict per-state monotonicity at the float32 level (§6.3) | dominant-strict + net-decrease rule, labelled |
| physics settings | captured before the body's first armed step configured the Rigidbody | captured at step 1 |
| reference time grid | RK4 used the nominal dt, not Unity's stored one (a 1.4e-6 relative grid skew) | A integrates on Unity's stored step |

---

## 8. Regression

| Suite set | Result |
|---|---|
| New Play Mode closeout (`MavF15ResearchRuntimeFlightValidation`) | **39 / 0**, twice, byte-identical |
| 17 headless F-15 suites | **757 / 0** (unchanged from WP-4A) |
| 22 synchronous F-16 / shared baseline suites | **1,247 / 0**, as at WP-4A. Reports are line-identical except for the source-scan file counts (+3 new files: 258 → 261, 157 → 160) and the known telemetry-noise reading in U-009b |
| protected source/data files (40, WP-4A list) | unchanged |
| compile | 361 runtime + 34 editor scripts, 0 errors |

---

## 9. Files

| File | Change |
|---|---|
| `Core/MavFlightPhysicsOwnership.cs` | `F15AfitResearch` owner, `IMavResearchOwnershipGrant`, `TryEnterF15AfitResearchOwnership`, per-step `VerifyResearchOwnership`, safety hold (OFF) |
| `F15/MavF15ResearchOwnershipGrant.cs` | new: aircraft-layer grant (authority + preparation + source-equivalent environment) |
| `Validation/MavF15ResearchRuntimeFlightValidationRunner.cs` | new: Play Mode runner |
| `Editor/MavF15ResearchRuntimeFlightValidation.cs` | new: headless / menu driver |
| `Docs/Reference/F15_RESEARCH_BASELINE_FREEZE_V1.0.md` | new: this document |
| `Docs/Reference/Data/F15/runtime_closeout/f15rt_playmode_report.txt` | new: the full Play Mode report (39 / 0) that the numbers above quote |
| five closeout docs | updated (final state, handoff, prerequisites, validation plan, post-freeze) |

No aero, coefficient, CMMQ, mass, inertia, thrust, density, gravity constant, F100, NASA 836, scene, prefab, weapon, sensor or AI change.

---

## 10. Optional future work — NOT a blocker

- **OPTIONAL RESEARCH EXTENSION:** pseudo-arclength continuation in the stabilator (D16), through the located folds, the fork and the Hopf points.
- **OPTIONAL RESEARCH EXTENSION:** the CMMQ question (D3), via McDonnell 1990 (ADA230462). Download permission is needed. Never tune CMMQ.
- **A NEW PHASE, not part of this baseline:** a pilot-controlled F-15. It needs sourced surface travel, rates, a pilot mapping and an FCS. None exists publicly for this configuration.

---

## 11. Verdict

**F-15 AFIT/BAUMANN/DAVISON RESEARCH BASELINE V1 — FROZEN**
