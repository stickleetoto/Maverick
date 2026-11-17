# 04 — Flight-Dynamics Engine Comparison: Maverick vs JSBSim vs Alternatives

**Question:** would replacing, wrapping or supplementing Maverick's C# 6DoF core with an external FDM produce a *measurable* improvement for Instructor v2 — and at what licensing and integration cost?

**Framing.** No result below shows that JSBSim or any other engine is more accurate than Maverick. Fidelity comes from (1) model data, (2) numerical integration, (3) validation. Maverick already uses NASA public data (Morelli F-16, AFIT/Baumann/Davison F-15) with an unusually strict validation discipline. The engines mostly differ in tooling, licensing, validation maturity and Unity integration cost.

Evidence classes: PV / SV / EI / UV as in doc 02. External sources were read by a research sub-agent of this session on 2026-11-17 (keys in `07_RESEARCH_SOURCES.md`). Repository facts are `REPO_VERIFIED` / `REPO_DOC`.

No engine was integrated, installed or run for this assignment.

---

## 1. Maverick's own FDM (baseline)

| Aspect | Status | Evidence |
|---|---|---|
| Structure | `PilotCommand → ControlLaw (−300) → Actuator (−200) → Aero + Propulsion → LoadSet → MavSixDoFBody (−100) → Rigidbody` | REPO_VERIFIED |
| Integrator | Unity PhysX; external forces integrated explicitly (symplectic-Euler-like) at the fixed step | PhysX developer statement `[NVIDIA-PHYSX-FORUM-2021]` (SV); no official PhysX/Unity statement of the update order (UV) |
| Step | `dt = 0.02 s` used by all Maverick Play Mode validation | REPO_DOC (V2 doc §7.2) |
| Gyroscopic term | **PhysX does not integrate ω × Iω** in Maverick's Unity build — shown empirically by a torque-free probe (U5CR-008 case 2); `MavSixDoFBody` adds the compensation once per step, on by default, readiness refuses a body without it | REPO_DOC `Docs/PHASE5CR_UNITY_TEST_REPORT.md` §U5CR-005 note; `Validation/MavF16ReferenceFlightScenarios.cs:1243-1491`; `Core/MavSixDoFBody.cs:30-48` (REPO_VERIFIED). PhysX 5 exposes `eENABLE_GYROSCOPIC_FORCES` (PV) but Unity 6000.3's Rigidbody API exposes no such setting (PV, by absence) |
| Unity damping | zeroed by profile | `Core/MavSixDoFBody.cs:634-636` (REPO_VERIFIED); Unity default angular damping 0.05 `[UNITY-RB-MANUAL]` (PV, current manual) would otherwise stack on aero damping |
| Angular-velocity cap | not set by FDM code → Unity default **50 rad/s** in 6000.3 (was 7 rad/s in 2021.3) | `[UNITY-RB-MAXANGVEL-6000.3]`, `[UNITY-RB-MAXANGVEL-2021.3]` (PV) |
| Inertia | explicit principal tensor + `inertiaTensorRotation` from the published body-axis tensor (Ixz included) | `FlightDynamics/README.md` (REPO_DOC) |
| Determinism | bit-reproducible Play Mode runs with pinned `captureFramerate` | REPO_DOC (V2 §7.2: two runs byte-identical); PhysX: repeatable for same scene, insertion order, release, platform (PV `[PHYSX-5-GUIDE]`) |
| Validation | Baseline v1 runner 1585/0; frozen F-15 suites; F-16 coefficient regression vectors | REPO_DOC |
| Licence | project-owned C# | — |

**Numerical caveat (EI).** First-order explicit integration at 50 Hz is coarse by classic real-time-simulation guidance: Howe & Fogarty suggest ~50 steps per cycle of the highest important mode for ~1 % dynamic accuracy with AB-2 `[NASA-CR-2877]` (PV). A short-period or Dutch-roll mode near 0.5–1 Hz is at the edge of that at 50 Hz with a *first-order* method. Maverick's own timestep-refinement test on F-15 V2 shows first-order convergence (`dt, dt/2` vs `dt/4` error ratio 3.00–3.07, V2 §7.2) — consistent and expected. For the instructor this matters only in that **all tuning is tied to dt = 0.02**; a change of fixed step is a physics change and must go through the baselines.

---

## 2. JSBSim

| Aspect | Finding | Class |
|---|---|---|
| Licence | LGPL-2.1 (`COPYING`); Python package LGPLv2+; Unreal plugin MIT; MATLAB interface BSD | PV |
| Language / release | C++, Python bindings with full feature set; v1.3.3 on PyPI 2026-10-16 | PV |
| Default rate | `dT = 1/120 s` in `FGFDMExec` | PV |
| Execution order | Propagate first (integrates previous frame's accelerations), …, Systems (FCS), …, Propulsion, Aerodynamics, …, Aircraft (sum), Accelerations, Output | PV (`FGFDMExec.h` enum) |
| Integrators | RectEuler, Trapezoidal, AB2–AB5, Buss1/2, LocalLinearization (rotational position only) | PV |
| Default integrators | **doc comment and code disagree**: code sets RectEuler (rotational rate), AB2 (translational rate), RectEuler (rotational position), AB3 (translational position); header comment says AB2/Trapezoidal. Read the four `simulation/integrator/*` properties at run time before any cross-check | PV |
| Equations of motion | includes ω × Jω, Coriolis and centrifugal terms | PV (`FGAccelerations.cpp`) |
| Aero format | XML `<axis>` blocks of `<function>` tables; one axis set per model | PV |
| FCS library | actuator (lag, rate limit, deadband, hysteresis, bias, clip, fail modes, saturation flag), PID, filters, switches, scheduled gains | PV/SV |
| Unity integration | **no official Unity plugin**; old community DLL/P-Invoke wrappers, unmaintained | PV (README) / SV |
| Validation | participant in NASA/TM-2015-218675 6DoF check cases (seven tools) | PV `[NASA-TM-2015-218675]` |
| Determinism | same binary/inputs → repeatable (EI); SPEC CPU 2026 benchmark built on JSBSim needed tolerances for cross-compiler floating-point deltas | PV `[SPEC-748]` + EI |
| Speed | only anecdotal (≥5× real time at 1 ms step after caching properties; string property access 10–20× slower) | SV |
| JSBSim F-16 | `aircraft/f16/f16.xml` (Hofman, 2001), "F-16A Block-32", **GPL-tagged**, cites TP-1538 among others | PV — **prohibited by Maverick policy**; also shares TP-1538 lineage, so agreement with Maverick would not be independent evidence |

---

## 3. YASim and other frameworks

| Engine | Licence | Approach / content | Fit for Maverick | Class |
|---|---|---|---|---|
| YASim (FlightGear) | GPL-2.0-or-later (`Airplane.cpp` SPDX) | geometry + two performance points (cruise, approach), iterative coefficient solver | poor: not table-based, not fighter-envelope, GPL, FlightGear-embedded | PV (licence, solver code) / EI |
| AeroBenchVVPython | GPL-3.0 | F-16 per Stevens & Lewis App. A, table or Morelli polynomial aero; LQR inner loop tracking Nz, ps, Ny+r; GCAS modes (roll wings level → pull) ; SciPy RK45 | external **behavioural** validation only (Maverick policy); never linked or copied | PV |
| NASA TM-2003-212145 (Garza & Morelli) | NASA public (terms UV) | MATLAB collection; F-16 from TP-1538 data; Euler/RK2/RK4/AB3 | primary NASA reference; already the source of Maverick's F-16 power-state dynamics | PV |
| **NASA 6DoF check cases (DAVE-ML)** | NASA public (terms UV) | F-16 nonlinear aero, mass, two control-law subsystems in ANSI/AIAA S-119; F-16 cases 11–16 (trims, climbs, turns, side-step, circling) with multi-tool time histories | **best engine-independent oracle**; provenance of its F-16 data not checked (Vol. II unread) | PV (Vol. I, NESC site) / UV |
| PyFME | MIT, archived 2020 | Cessna models only | none | PV |
| ArduPilot SITL + JSBSim / PX4 bridge | GPLv3 / BSD-3 | UAV autopilot co-simulation | none for fighters | PV/SV |
| Gazebo LiftDrag | Apache-2.0 | linear lift/drag + stall slopes | not fighter-grade | PV/EI |
| Microsoft AirSim | MIT, retired | multirotor/car; Unity experimental | none | PV |
| gym-jsbsim / CloseAirCombat | MIT / GPL-3.0 | RL environments over JSBSim | reference for training setups only | PV |

---

## 4. Unity PhysX integration facts relevant to the instructor

All PV from Unity 6000.3 documentation unless marked.

1. **Force application:** `AddForce`/`AddTorque` accumulate and apply on the next simulation step; PhysX resets accumulators after each step. One total load per FixedUpdate (Maverick's design) is the correct pattern.
2. **`maxAngularVelocity` = 50 rad/s default** (project-wide "Default Max Angular Speed" also 50). This does not constrain any Maverick FDM command (F-16 law 270 °/s ≈ 4.7 rad/s). The legacy aircraft catalogue sets 5.4–8 rad/s on legacy jets (`Aircraft/MavAircraftCatalog.cs`, REPO_VERIFIED), which only matters if a legacy profile is applied to an FDM rig. *Recommendation:* the FDM rig should set and assert its own value so behaviour does not depend on the Unity version (EI).
3. **Damping:** angular damping default 0.05 (current manual); `linearDamping` default not stated in the 6000.3 page (UV). Maverick zeroes both through the profile.
4. **Gyroscopic term:** not exposed by Unity 6000.3; empirically absent (REPO_DOC); compensated once by `MavSixDoFBody`. **Do not add any instructor-side compensation.**
5. **Time step:** `fixedDeltaTime` is quantized internally (getter may differ by one ULP); default 0.02 s per the current manual (SV for 6000.3).
6. **Script stepping for headless runs:** `Physics.simulationMode = Script` + `Physics.Simulate(fixed step)`; passing frame-dependent steps breaks determinism; `FixedUpdate` still runs at `fixedDeltaTime` regardless — so a headless runner must call the FDM's load computation explicitly before each `Simulate`, or it desynchronizes (EI).
7. **Interpolation:** off by default; the F-15 rig keeps it off deliberately because `MavSixDoFBody` reads the Transform (`F15/MavF15PilotControlledRig.cs:137-140`, REPO_VERIFIED). The instructor's camera/aim visuals may interpolate; the guidance must read the FDM state, not interpolated visuals.
8. **Determinism:** PhysX repeatable for same scene, insertion order, release and platform; Enhanced Determinism isolates islands; no cross-platform guarantee (PV `[PHYSX-5-GUIDE]`, `[UNITY-PHYSICS-SETTINGS]`).
9. **ML-Agents headless defaults** (`--time-scale 20`, `--capture-frame-rate 60`): high time scale with the maximum-allowed-timestep cap can merge/drop fixed steps under load (EI). Pin stepping for reproducible training.

---

## 5. Comparison

| Engine | Licence | Integrator | Aero model | FCS | Unity path | Headless | Determinism | Verdict for Maverick |
|---|---|---|---|---|---|---|---|---|
| **Maverick C# FDM** | project | PhysX explicit, 50 Hz; ω×Iω added by Maverick | NASA Morelli F-16; AFIT F-15 research | own laws (non-authoritative) | native | batchmode / Script mode | byte-identical runs demonstrated | **keep as runtime** |
| JSBSim | LGPL-2.1 | selectable; 120 Hz default | XML tables | rich component library | native DLL + P/Invoke (none official) | yes, Python | same-binary repeatable | **offline oracle only**, with Maverick's own model ported, never its F-16 |
| YASim | GPL-2+ | UV | 2-point solved | via FlightGear | impractical | via FG | UV | reject |
| AeroBenchVV | GPL-3.0 | RK45 / Euler | S&L / Morelli | LQR + GCAS | none | yes | not step-matched | behavioural reference only (policy) |
| NASA DAVE-ML check cases | NASA public | tool-dependent | F-16 nonlinear | included | port or compare traces | data | reference traces | **primary independent oracle** |

---

## 6. Would a hybrid help? (EI)

| Option | Benefit for Instructor v2 | Cost / risk | Recommendation |
|---|---|---|---|
| Replace Maverick runtime with JSBSim | none demonstrated for the instructor; loses frozen baselines and ownership gates | LGPL relinking obligations; native plugin per platform (static linking on iOS forces object-file shipping); new F-15/F-16 model ports; all validation re-done | **No** |
| JSBSim as live parallel "shadow" | none for the instructor | two physics evaluators of one aircraft, ownership confusion | **No** |
| JSBSim offline (Python) with Maverick's own aero/mass ported to XML | cross-check of integrator and axis conventions; independent of Unity | port effort; must read JSBSim's real integrator settings; data shared → agreement checks numerics, not data | P3, optional |
| NASA DAVE-ML F-16 check cases as oracle | engine-independent, NASA-sourced reference trajectories for trims and manoeuvres | data provenance (Vol. II) must be checked; model differs from Morelli compact fit | P3, preferred over JSBSim |
| Sub-stepping fast internal states inside the FDM (actuators/filters) | only matters once actuator lags exist (PR #33 lag was rejected for feel) | baseline changes | not for instructor work |

**Conclusion for Instructor v2:** the guidance layer is independent of the FDM engine, because it consumes `MavFlightState` and emits `MavPilotCommand`. No engine change would make the instructor easier, safer or more realistic. The real limits on what the instructor can demonstrate are *model data* (narrow F-15 research domain; no F-16 thrust deck), not the integrator.

---

## 7. Licensing summary (EI — not legal advice)

- LGPL-2.1 §6 permits combining with proprietary code if users can relink a modified library (shared-library mechanism, PV `[LGPL-2.1]`). Desktop: separate native DLL via P/Invoke resembles the Unreal plugin's approach. Apple mobile: Unity links native plug-ins statically → harder compliance (PV Unity docs, SV summary).
- GPL code and data (YASim, AeroBenchVV, CloseAirCombat, JSBSim's GPL-tagged aircraft files) must not be linked into or shipped with a closed Unity build; offline, undistributed use only.
- NASA report/data reuse terms were not checked in detail (UV); Maverick's existing source policy already governs them.
