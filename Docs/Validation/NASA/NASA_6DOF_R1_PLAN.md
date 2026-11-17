# NASA Independent 6-DoF Validation R1 — Plan and Run Record

**Branch:** `claude/nasa-6dof-validation-r1`, from `main @ 71820a97b8d58c69229799fb00868bce4ded87d1`.
**Research basis:** `claude/instructor-v2-research-phase2 @ 7a1b7f7` (draft PR #36), documents `07_NASA_6DOF_BENCHMARK_STUDY.md`, `08_FLIGHT_MODEL_ROADMAP.md`, `09_RESEARCH_SOURCES.md`, `NESC_DOWNLOAD_MANIFEST.md`.
**Unity:** `6000.3.16f1` (repository-declared).
**Question answered:** does Maverick's six-degree-of-freedom backend integrate the equations of motion correctly under independently specified initial conditions and loads? This is *verification* of the integrator and load path. It does **not** establish real-aircraft fidelity.

---

## 1. Scope

| Phase | NASA case | What is tested | Status |
|---|---|---|---|
| B1 | Atmospheric case 2, dragless tumbling brick | torque-free rigid-body rotation through `MavSixDoFBody` + PhysX, incl. the gyroscopic compensation | implemented, run |
| B2 | Atmospheric case 3, tumbling brick with aerodynamic damping | rate-dependent aerodynamic moments, dynamic pressure, atmosphere, dissipation | implemented, run |
| B3 | NESC F-16 DAVE-ML aerodynamic model | Maverick's Morelli F-16 polynomial vs NASA's tables at matched static inputs; units, axes, signs | implemented, run |
| B4 | Atmospheric case 11, F-16 subsonic trim | static trim feasibility at NASA's published trimmed state; trajectory comparison assessed | static part implemented; trajectory **not run** (blocked, see `VALIDATION_LIMITATIONS.md`) |

Nothing in the measured model was changed: `MavSixDoFBody`, aerodynamic coefficients, mass/inertia references, thrust data, control laws, prefabs, scenes and existing baseline tests are untouched.

---

## 2. Sources and integrity

All NASA material comes from the NESC site `https://nescacademy.nasa.gov/flightsim/2015` (NASA/TM-2015-218675; NTRS: "Work of the US Gov. Public Use Permitted"). The files were downloaded once on the research branch with the user's approval and copied, unchanged, to a folder outside every Git tree (`E:\unity project\_nesc_source\`). Their SHA-256 were re-verified before use:

| File | SHA-256 | Matches manifest |
|---|---|---|
| `Atmospheric_checkcases.zip` | `1a83df6d00fa186973e8fb7bb8aefa5f7ed580d2ce8aa1327f50ac0c7bd5c869` | yes |
| `Atmospheric_models.zip` | `10e2dc267856ef37eaedbcdd16eb49a8c44b39301054de0fa78292c1c4aba3bc` | yes |
| `NASA-TM-2015-218675-EOM_checkcase_appendices.pdf` (Vol II) | `f5d702972352331d8f9a7e3867f0cfdec2f91305da26a13c47d4c9a80fb0eb77` | yes |
| `NASA-TM-2015-218675-EOM_checkcase_summary.pdf` (Vol I) | `541db40684ee5663064ccf4b5e4ede7ffd536d38adb5493ce3af84da4f961d5f` | yes |
| `Initial_Conditions.xlsx` | `57b986c1ea50feec1881f1a45cf42c6c426ae78c8c364611857de2c4061fdb77` | yes |
| `brick_inertia.dml` / `brick_aero.dml` (inside the models zip) | `eae595902d2f…bfa1` / `a5883cfbde33…f31d` | pinned in the generated data file |
| `F16_aero.dml` / `F16_prop.dml` / `F16_inertia.dml` (inside `F16_package.zip`) | `82796577b989…7e51` / `e5fd7772e5c0…15c6` / `e11d4eba5b15…3c4a` | pinned in the F-16 suite; byte-identical to the library's simupy-flight copies |

**What is committed:** no NASA binary. Only (a) `Tools/nasa_6dof/extract_nesc_brick_reference.py`, which refuses to run unless the two zip hashes match, and (b) its output `MavNasaBrickReferenceData.cs`: the brick model constants and a sampled subset (every 2 s, five tools) of NASA's case 2/3 outputs, each source CSV's SHA-256 in its header. The F-16 suite reads NASA's DAVE-ML files at run time from a user-supplied path (`-nescF16AeroDml <path>` or `MAVERICK_NESC_F16_AERO_DML`) and refuses any file whose SHA-256 differs; without the file it reports **NOT RUN as a failure**, so it can never pass silently.

---

## 3. Executable validation

| File (all `Assets/MaverickFresh/Scripts/FlightDynamics/Validation/`, editor-only) | Role |
|---|---|
| `MavNasaBrickReferenceData.cs` | generated NASA constants and reference samples |
| `MavNasaRigidBodyReference.cs` | **independent** reference: double precision, RK4, Euler's equations + quaternion kinematics, independent US76 atmosphere and gravity models; no UnityEngine, no production routine |
| `MavNasaBrickAeroModel.cs` | validation-only aero model implementing `brick_aero.dml`, plugged into the production `MavSixDoFBody` |
| `MavNasaBrickCheckCaseValidation.cs` | suite for B1/B2 |
| `MavDaveMlEvaluator.cs` | **independent** minimal ANSI/AIAA S-119 (DAVE-ML 2.0) evaluator for the subset NASA's files use |
| `MavNasaF16StaticAeroValidation.cs` | suite for B3/B4 |

**How the backend is exercised.** Each run builds a temporary `HideAndDontSave` GameObject with a real `Rigidbody`, the validation aero model and a production `MavSixDoFBody` (mass and inertia through the production `MavMassProperties` path; `requireOperationalReadinessForLoadApplication = false` because a brick has no aircraft readiness). Physics runs in script mode (restored afterwards): each step calls `MavSixDoFBody.StepPhysicsForValidation(dt, t)` — the same `StepPhysicsCore` that `FixedUpdate` runs — and then `Physics.Simulate(dt)`. Unity's own PhysX therefore integrates the loads Maverick applies, exactly as at run time.

**Commands** (Unity batch, synchronous adapter):

```text
Unity.exe -batchmode -quit -nographics -projectPath <worktree>
  -executeMethod MaverickFresh.FlightDynamics.EditorTools.MavFdmValidationBatchAdapter.RunBatch
  -fdmSuite nasa_brick -fdmMode sync -fdmType MavNasaBrickCheckCaseValidation -fdmMethod RunAll
  -fdmOut <json> -logFile <log>

Unity.exe ... -fdmSuite nasa_f16 -fdmType MavNasaF16StaticAeroValidation -fdmMethod RunAll
  -fdmOut <json> -logFile <log> -nescF16AeroDml <...>/F16_package/F16_S119_source/F16_aero.dml
```

---

## 4. Acceptance logic

- **Gated (PASS/FAIL):** source identity; mechanics (one load application and one gyroscopic correction per step, no refusals, finiteness, zero Unity damping, inertia readback); agreement of the independent reference with NASA where the tools themselves agree; first-order convergence of every error measure; the compensation-off control; dissipation in case 3; the production damping-moment and density chains; NASA check-shot reproduction by the independent evaluator; dimensionalisation; derivative-sign agreement for stability and damping derivatives; case 11 force and thrust consistency.
- **Reported, not gated:** absolute error magnitudes at each step size; differences between Maverick's Morelli polynomial and NASA's tables (classified); Maverick vs NASA directly where model assumptions differ.
- **Thresholds** are justified from numerical analysis or from the quantity being protected, never from NASA's published inter-tool percentages (Vol II Table 80), which are observed differences, not acceptance criteria. Each threshold's basis is in `NUMERICAL_CONVERGENCE.md`.

---

## 5. Reports

| Report | Content |
|---|---|
| `CASE2_TORQUE_FREE_REPORT.md` | B1 |
| `CASE3_DAMPING_REPORT.md` | B2 |
| `F16_STATIC_AERO_REPORT.md` | B3, B4 |
| `NUMERICAL_CONVERGENCE.md` | step-size studies, orders, invariant drift, threshold bases |
| `VALIDATION_LIMITATIONS.md` | what was not compared and why; what the results do not mean |
