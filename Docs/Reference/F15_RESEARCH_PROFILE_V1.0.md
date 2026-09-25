# F-15 Research Profile V1.0 — AFIT / Baumann / Davison, M 0.6 / 20,000 ft

> **THIS IS NOT NASA F-15B 836.** It is a separately tagged research configuration built only from one research model's own source data. It fills no gap in the exact NASA 836 path, which stays fail-closed (`F15_FINAL_STATE_V1.0.md`).

| | |
|---|---|
| **Configuration / profile ID** | `F15_AFIT_BAUMANN_DAVISON_MACH06_20K_RESEARCH` |
| Display name | `F-15 AFIT/Baumann/Davison research - M 0.6 / 20,000 ft (6,096 m) - NOT NASA 836` |
| Mass-state ID | `F15_AFIT_DAVISON_DRIVER_37000LB_RESEARCH_MASS_STATE` |
| Exact-target tokens present | **none** (`NASA_F15B_836`, `74-0141`, `PRE_QUIET_SPIKE`, `EXACT` are checked by `[X6]`) |
| Readiness reached | **STRUCTURALLY_PREPARED**, not live-ready |
| Work package | **WP-1 — complete** |

---

## 1. Source lineage

McAir ARO10 / 1988 F-15 Aerobase (not public)
→ Baumann, AFIT/GAE/ENY/89D-01 (DTIC ADA217366): SAS curve fits
→ Davison, AFIT/GAE/ENY/92M-01 (DTIC ADA256613) Appendix C
→ Maverick R2 transcription (`MavF15BaumannMach06*`), audited coefficient by coefficient.

Every number in this profile comes from that chain (Davison's driver program and coefficient routine). Nothing comes from NASA 836 sources, and nothing is borrowed from the exact profile.

## 2. Reference geometry — reused, not duplicated

| | Source | SI | Where in code |
|---|---:|---:|---|
| `S` | 608 ft² | 56.4851 m² | `MavF15BaumannMach06Reference.CreateReferenceGeometry()` |
| `b` | 42.8 ft | 13.04544 m | same |
| `c̄` | 15.94 ft | 4.858512 m | same |
| Moment reference | 0.2565 c̄ | — | `MomentReferenceCgCbar` |

The source is Davison's driver (`BWING=42.8`, `CWING=15.94`, `SREF=608.`, PDF p.91) and ARO10's `CMCGR=.2565` (printed p.123). This is the same set the geometry audit grades `F15_FAMILY_SUPPORT` (`ARO10_S/CBAR/B_REFERENCE`). **The exact NASA 836 `S`, `c̄` and `b` remain 0.**

## 3. Mass and inertia — Davison's, version-matched

| | Source (slug-ft², lb) | SI |
|---|---:|---:|
| Weight / mass | 37,000 lb (`RMASS=37000./32.174`, PDF p.91) | 16,782.920 kg |
| Ixx | 25,480 | derived |
| Iyy | 166,620 | derived |
| Izz | 186,930 | derived |
| Ixz | −1,000 | derived, sign as printed |

- **The inertia lines on Davison PDF p.92 are commented out.** The running code uses precomputed constants K1…K17, and those reproduce from these values:
  - K1 = 3.350088890e−4
  - K5 = −3.924646781e−2
  - K7 = −5.349596105e−3
  - K9 = 0.96897131196
  - K10 = −6.001680471e−3

  So these are the values the model actually used. `[P2]` recomputes each constant.
- **Sign convention.** The model couples roll and yaw through +IXZ/IZ, which is the conventional tensor `[Ix 0 −Ixz; 0 Iy 0; −Ixz 0 Iz]`, the same one `MavF15InertiaBasis` takes.
- **Positive definite.**
- **Principal moments:** 25,473.81 / 166,620.00 / 186,936.19 slug-ft². Triangle margin 5,157.61.
- **Unity principal-axis rotation:** −0.3549° about Unity X.
- **Conversion math.** `MavF15InertiaBasis` reproduces the exact `MavF15MassReference.CreateUnityMassProperties` **bit for bit** on the exact inputs (`[P2]`). The research state uses the audited math, and the exact code was not touched.
- **Center of mass: local origin — a declared simulation reference choice, not source data.** ARO10 states the aero data are referenced to the model's CG and that there is "no 'CG offset' to be computed" (Davison printed p.123), so CG and moment reference coincide *in this model*. No absolute CG position is claimed.

## 4. Thrust — exact source semantic

`MavF15AfitResearchThrustSource` / `MavF15AfitResearchFixedThrust` (deliberately **outside** `MavF100*`).

| Property | Value | Source |
|---|---|---|
| Magnitude | **8,300 lbf = 36,920.24 N** | `THRUST=8300.` (Davison driver PDF p.91) |
| Scope | **TOTAL aircraft thrust — not per engine** | "THRUST - TOTAL A/C THRUST, LBS" (same page) |
| Direction | body **+X** | `CX = … + THRUST/QBARS` (Davison PDF p.120, p.150) |
| Pitching moment | **+234.444 N·m, nose-up** = THRUST × 0.25 in | `CMM = CMM + THRUST*(0.25/12.0)/(QBARS*CWING)`, "the offset of the thrust vector from the CG" |
| Variation | **none** — constant | Baumann PDF p.34 ("military power … trim conditions … 0.6 Mach and 20,000 feet"), p.124 ("fixed constant at 8300 lbs") |
| Throttle | **ignored**, and reported as ignored in `debugStatus` | no source varies it |
| Where valid | the research source condition only; zero elsewhere | — |
| Identity gate | refuses under any profile but the research one, including the exact 836 ID, and on a body whose provider is not the research profile | — |
| Authority | `HasAuthoritativeData = false`, `IsAcceptableForLiveFlight = false` | — |

The transcribed coefficient routine omits both thrust terms on purpose (`MavF15BaumannMach06Longitudinal`), so the force and the moment are applied **once**, through the propulsion load path.

## 5. Envelope — and what "Mach 0.6" means (WP-3A)

**Mach 0.6 / 20,000 ft is the coefficient-fit condition, not a runtime condition of the source model** (`F15_BAUMANN_SOURCE_CONDITION_AUDIT_V1.0.md`).
- Baumann's own driver holds density at the 20,000-ft value.
- It lets true velocity vary as a state, with q = ½ρV².
- It never computes Mach.

The profile now carries `conditionMode`, and every mode keeps the same α/β span:

| Mode | Admits | Status |
|---|---|---|
| `StrictFitCondition` *(default)* | Mach 0.6 ± 0.001, altitude 6,096 ± 1 m, enforced by the aero model and the research thrust | the fit condition |
| `SourceReproduction` | true airspeed 218.5–699.7 ft/s (the source-exercised span) at 6,096 ± 1 m only; the envelope widens to Mach 0.2107–0.6748 | every coefficient away from Mach 0.6 is flagged **EXTRAPOLATED from the M=0.6 fit — source-exercised, NOT aerodynamically validated** |

`SourceReproduction` is honoured only when this profile is the six-DoF body's provider.
- **α / β:** −4.0…90.0° α, ±20.0° β — the span the transcribed routine already declares (`MavF15BaumannMach06Domain`). **Not broadened.**
- **Recorded, not applied:** Davison's driver also prints continuation bounds of −8…50° α and ±30° β (PDF p.92). Adopting them would narrow α and widen β relative to the declared span. That is a separate decision.

## 6. Known limitations

- **Control-surface travel: unavailable → zero travel.** Four public F-15 travel sets conflict (`F15_DN1180_SOURCE_LINEAGE_V0.1.md` §3), so none is adopted.
  - The research aircraft is **structurally flyable but uncontrolled**: surfaces neutral, 0 actuator channels available.
  - The FCS applies 0 stages (no sourced gains).
  - This is exposed in `[Q1]`, not bypassed.
- **Not live-ready.** The research propulsion is non-authoritative and not accepted. There is no operational command source.
- **One coefficient-fit condition; no throttle or altitude variation.** The source's own model does vary true velocity (WP-3A). In `SourceReproduction` mode Maverick admits the velocities the source ran, at the source's fixed-density altitude only.
- **Static reproduction done (WP-3A); research trim solver done (WP-3B symmetric, WP-3C turning); flying trim not started.**
  - **WP-3C:** `SolveTurning`, with the printed bank angle fixed, recovers all 80 non-symmetric turning (helical) Table VII states within print resolution plus the solver's own termination uncertainty. Point 165 is the pitchfork, solved symmetric (`F15_RESEARCH_TURNING_TRIM_V1.0.md`).
  - Baumann's Table VII equilibria close to print precision in all six axes when fed through this configuration's aero, mass, inertia and thrust (`F15_TABLE_VII_EQUILIBRIUM_VALIDATION_V1.0.md`).
  - **WP-3B:** `MavF15AfitResearchTrimSolver` solves the source's own symmetric equilibrium off the body. It uses the fixed source density, 8,300 lbf total thrust and the thrust-line moment. From perturbed starts it recovers all 89 symmetric Table VII states within print resolution (`F15_RESEARCH_TRIM_SOLVER_V1.0.md`).
  - Flying trim still needs research surface travel. The flying aircraft holds zero travel, so the surfaces enter only through the STATIC_EQUILIBRIUM_VALIDATION_ONLY control.
  - **Recorded runtime gap:** the body computes q from ISA density at its altitude (`MavSixDoFBody`). So `SourceReproduction` in the body does **not** reproduce the source's fixed-density q, which is 6.83e-4 off even at 6,096 m. Unity's gravity is also 9.81 against the source's 9.80664 m/s². The research-only environment policy that would close this is designed, not built (`F15_RESEARCH_TRIM_SOLVER_V1.0.md` §10; D11).
- **PlayMode: not run.** The batch adapter's Play Mode bridge is hard-wired to the FDM scheduler probe, and extending shared infrastructure was out of scope. Instead, `MavF15ResearchPipelineValidation` drives the real components through the editor-only `StepPhysicsForValidation` seam — the same `StepPhysicsCore` that `FixedUpdate` runs — on a hidden temporary object. No scene or prefab is touched. Unity does not integrate the Rigidbody in edit mode, so this checks the pipeline, not a trajectory.

## 7. Code and validation

| Code | Role |
|---|---|
| `MavF15AfitResearchIdentity` | ID, labels, exact-token check |
| `MavF15AfitResearchMassReference` | Davison mass/inertia, research mass-state ID |
| `MavF15InertiaBasis` | shared inertia math (proven identical to the exact conversion) |
| `MavF15AfitResearchFlightDynamicsProfile` | the research provider (exact provider untouched) |
| `MavF15AfitResearchThrustSource`, `MavF15AfitResearchFixedThrust` | research-only thrust |
| `MavF15AfitResearchTrimSolver`, `MavF15AfitResearchSourceEnvironment` | WP-3B/3C research trim (symmetric; turning with φ fixed), off the body, in the source's own units and environment |

| Suite | Checks | Result |
|---|---:|---|
| `MavF15ResearchContaminationValidation` — `[X1]`–`[X5]` written and run **before** the research code (22/0), `[X6]` after | 32 | PASS |
| `MavF15ResearchProfileValidation` — `[P0]`–`[P6]` | 35 | PASS |
| `MavF15ResearchPipelineValidation` — `[Q1]`–`[Q3]`, editor seam, **not PlayMode** | 13 | PASS |
| `MavF15ResearchTrimSolverValidation` — `[R1]`–`[R13]` (WP-3B) | 34 | PASS |
| `MavF15ResearchTurningTrimValidation` — `[H1]`–`[H19]` (WP-3C) | 31 | PASS |

No flight-performance tolerance is asserted anywhere.
