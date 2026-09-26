# F-15 — V1 Freeze Checkpoint: State and Authority

**Checkpoint:** F-15 V1 freeze. This is a coherent frozen baseline, not the end of F-15 work. Follow-on opportunities are ranked in `F15_POST_FREEZE_OPPORTUNITIES_V1.0.md`.
**Branch / PR:** `claude/f15-full-implementation` · Draft PR #27, **never merge** · base `sol/f15-r2-lateral`
**Target:** `NASA_F15B_836_SN74_0141_PRE_QUIET_SPIKE_BASELINE_F100_PW_100` · mass state `NASA_F15B_836_BASELINE_8K_FUEL_MASS_STATE`

---

## 1. Authority table

Precision uses four tags: **exact** (source-stated for 836), **research** (another model or build, graded separately), **approximate**, **unavailable**.

| Layer | Status | Authority / provenance | Source | Precision | Runtime consequence |
|---|---|---|---|---|---|
| Target identity | FROZEN | exact 836 | NASA/TM-2008-214634 p.6 ("tail number 836"); NTRS 20070032807 (S/N 74-0141); NTRS 20160006705 | **exact** | `MavF15ReferenceData.TargetConfigurationId` |
| Physical dimensions | FROZEN (physical only) | exact 836 | 63.7 / 42.8 / 18.7 ft — TM-4782 p.10, TM-2005-213670 p.6, TM-2006-213674 p.12 | **exact** | `AircraftLengthM`, `PhysicalWingSpanM`, `AircraftHeightM`. **Never** used to normalize a coefficient. |
| Mass / CG / inertia | FROZEN | exact 836 — Table 1 **Baseline** column | NASA/TM-2012-215978 table 1 p.8: 37,426 lb; 26.34 % MAC; Ixx 30,345; Iyy 198,687; Izz 223,214; Ixz −5,070 slug-ft²; 8,000 lb fuel | **exact** | `MavF15MassReference` → Unity principal inertia. CG is in % MAC only; its absolute station is **not** placed (§4). |
| Coefficient reference geometry (S, c̄, b, moment ref.) | **UNAVAILABLE** | none accepted | audit: `F15_NASA836_REFERENCE_GEOMETRY_AUDIT_V0.1.md`; lineage: `F15_A4172_SOURCE_LINEAGE_V0.1.md` | **unavailable** | `CreateExactTargetGeometry()` returns 0/0/0, so the profile is invalid and the aircraft fails closed |
| Aerodynamic model | exact: **UNAVAILABLE** · research: implemented | research = `F15_FAMILY_SUPPORT` / CROSS_VALIDATION (ARO10 → Baumann → Davison curve fits) | Davison AFIT/GAE/ENY/92M-01 App. C, audited coefficient-by-coefficient (`F15_R2_TRANSCRIPTION_AUDIT_V0.1.md`) | exact **unavailable** · **research** | default `ExactNasa836Unavailable` returns zero coefficients. Research modes need explicit opt-in and are gated to M 0.6 / 20,000 ft and the transcribed α/β span. |
| Flight-control architecture | implemented; **cross-checked against 836 (WP-2)** | 836's own block diagrams for structure; family / AFIT for the rest | NASA/TM-2009-214651 and TM-2012-215978 figs. 3–5 — `F15_836_FCS_STRUCTURE_V1.0.md` | **exact (structure only)** for 9 of 11 stages | 11 stages, each gated and refused when its gain is unavailable. **9 stages are EXACT_836_STRUCTURE_CONFIRMED** (mechanical path, PRAD, RRAD, pitch/roll/yaw CAS, ARI, stall inhibitor, roll-yaw crossfeed); the roll-damper washout is family-only; turn coordination is not shown for 836. The stall inhibitor's R3 routing differs from the diagram (recorded, not changed). **Two exact switch facts in code:** ARI out above Mach 1.5, roll-yaw crossfeed out above Mach 1.0, exact mode only. They can only remove a stage. |
| Flight-control numeric law | **UNAVAILABLE** | none | DN-1180.01-238-458 Rev. D **not publicly located**. TM-72861 publishes no feedback gain. | **unavailable** | the law outputs neutral by construction |
| Control-surface authority | exact: **UNAVAILABLE** · research: **demonstrated range declared, not a physical limit** | family sets conflict (four public sets, plus Baumann Table VI and Davison's −29°) | `F15_DN1180_SOURCE_LINEAGE_V0.1.md` §3; `F15_RESEARCH_CONTROL_AUTHORITY_V1.0.md` | exact **unavailable** · **research** | exact hard stops are zero travel. **Research** (WP-2): `ResearchDemonstratedControlRange`, stabilator −25…−5° from Baumann's tabulated equilibria, other channels 0°. It is a record of what the source commanded, **not** a hard stop. The flying research aircraft still has zero travel. Physical stops and actuator rates are separate types and stay unavailable. |
| Research stabilator sign convention | **VERIFIED (WP-2)** | research | Baumann PDF p.86 statement; Table VII pitch equilibria | **research** | positive = leading edge up / trailing edge down. Maverick's transcribed Cm closes Baumann's Table VII pitch equilibria to round-off with the source sign (≤1.2e-7) and misses by 0.13–0.19 with it flipped |
| **836 validation data (WP-2)** | digitized, **validation only** | exact 836 — OriginalPrimary × Exact836 | TM-2008-214634 figs. 12–15; TM-2012-215978 figs. 27–29 — `F15_836_VALIDATION_DATA_V1.0.md` | **exact** (flight estimates / flight / simulation), digitized with stated uncertainty | 60 series, 4,041 samples: baseline Cnβ and Cmα flight estimates and trends, baseline flight vs simulation time histories, CAS-off baseline simulation at M 0.60 / 0.95 / 1.80. **Never model data**; no runtime file may read them (`[V3]`). TM-2012's derivative borders and CAS-off Dutch-roll / short-period figures turned out to be spike-configuration or unscaled, so they are **excluded** |
| Engine identity | FROZEN | exact 836 | F100-PW-100 × 2 — TM-2008-214634 p.6; NTRS 20070032807 p.10 | **exact** | `MavF15PropulsionSkeleton`, two independent engine slots |
| Propulsion research characteristic | implemented (shape only) | CompatibleSupport, family A (PW-100(3) simulation lineage) | TP-1034 fig. 17, 63 digitized points, 7 conditions | **research** | `MavF100NormalizedNetThrustModel` answers in *fraction of design maximum*, never newtons |
| NASA 836 dimensional propulsion anchor | recorded, **not used** | exact 836 aircraft datum | ≈23,500 lbf (104,533 N) uninstalled SLS full augmentation — NASA/TM-2005-213670 | **approximate** | refused as a scale: `MavF100PathSeparation.DimensionalizeForTarget` needs build equivalence and 836's engine sub-configuration, and neither is known |
| Engine installation geometry | **UNAVAILABLE** | none | — | **unavailable** | `geometryDeclared = false`; no thrust-line moment, no engine-out yaw |
| Source envelope | exact aero: none · research: **two explicit modes (WP-3A)** | exact envelope is an inverted interval | Baumann/Davison driver and COEFF listings; TP-1034 condition list | exact **unavailable** · **research** | research aero and research thrust: `StrictFitCondition` (default) = M 0.6 ± 0.001 at 6,096 ± 1 m; `SourceReproduction` = the source-exercised 218.5–699.7 ft/s at 6,096 ± 1 m, flagged as extrapolated from the M 0.6 fit. Transcribed α/β span in both. Thrust model: 7 documented points ± 150 m / ± 0.02 M. Off-point queries are refused, not extrapolated. |
| **Research source condition (WP-3A)** | **RESOLVED** | research | `F15_BAUMANN_SOURCE_CONDITION_AUDIT_V1.0.md` | **research** | Mach 0.6 / 20,000 ft is the **coefficient-fit condition**. The source model holds density at 20,000 ft, varies V as a state, and never computes Mach. **Source-exercised ≠ aerodynamically validated**: only the fit condition is validated (Davison: 'only valid at M=0.6'). |
| **Baumann Table VII static reproduction (WP-3A)** | **CLOSES TO PRINT PRECISION** | research-model output, validation only | `F15_TABLE_VII_EQUILIBRIUM_VALIDATION_V1.0.md` | **research** | 201 points stored as printed. The two halves' turning columns are displaced two rows (a source finding, shown by kinematics). 170 equilibria assembled; all six force/moment residuals and both kinematic residuals lie within print precision at implied Mach 0.28–0.64. One listing difference found (CFX2 constant, App. C vs Baumann / App. B), inactive below α 20°, recorded and not changed. |
| **Research trim solver (WP-3B)** | **implemented, off the body** | research | `F15_RESEARCH_TRIM_SOLVER_V1.0.md` | **research** | `MavF15AfitResearchTrimSolver` solves the source's own symmetric equilibrium (α, stabilator and θ at fixed V). It runs with the fixed source density, 8,300 lbf total thrust and the 0.25-in thrust-line moment, on the shared `MavDampedNewtonSolver`. From perturbed starts it recovers all 89 symmetric Table VII states: 356 / 356 converged, 0 of 89 outside the print-resolution floor. No second root was found. Inside the demonstrated stabilator range, symmetric trim exists only for V ≈ 252–403 ft/s: none at Baumann's M 0.6 speed. Nothing is flown. The runtime body's q is still ISA-based, a recorded gap (D11). |
| **Research turning trim (WP-3C)** | **implemented, off the body** | research | `F15_RESEARCH_TURNING_TRIM_V1.0.md` | **research** | `SolveTurning` fixes only the printed bank angle and solves α, β, p, q, r, θ, V and the stabilator from all eight source equations; ψ̇ is an output. The WP-3A pairing gives 81 turning states: 80 non-symmetric, plus point 165, classified as the pitchfork and solved symmetric. 320 / 320 converged. 69 states lie within the print floor in all eight unknowns, and 11 within it plus the solver's termination uncertainty; none beyond. Mirror parity is exact. No second root was found at 8 probed φ. CFX2 is inactive at every root (α ≤ 10.6°). Nothing is flown. |
| **Research source stability (WP-3D)** | **analysed, off the body** | research | `F15_RESEARCH_STABILITY_ANALYSIS_V1.0.md` | **research** | **Method:** `MavF15AfitResearchSourceDynamics` reproduces the source's own FUNX x-dot = f(x, stabilator), equal to an independent Newton–Euler derivation to 4.8e-15 with all 11 printed K constants. It is linearized at all 170 recovered equilibria by audited central differences and solved by a validation-side eigensolver (numpy / LAPACK agrees to 4.2e-15). **Result:** 127 STABLE, 40 UNSTABLE, 3 NEAR_NEUTRAL; the source labels all stable. The disagreements are the fold-bounded saddles (22) and a longitudinal pair at α 13.0–14.1° from the printed CMMQ (18). The printed folds and the pitchfork are eigenvalue zeros, the pitchfork is supercritical, and Baumann's −19.73° Hopf is found at −19.33°. **Source model only:** not the F-15, not 836, not the FCS, not a Rigidbody. Nothing is flown. |
| **Research source stability, time domain + conflict audit (WP-3E)** | **verified, off the body** | research | `F15_RESEARCH_TIME_DOMAIN_STABILITY_V1.0.md`, `F15_RESEARCH_STABILITY_CONFLICT_AUDIT_V1.0.md` | **research** | **Method:** fixed-step RK4 on the unchanged source RHS (no Rigidbody, no Unity time) measures growth, decay and frequency from ±eigenvector perturbations; the antisymmetric pair cancels residual drift and the quadratic nonlinearity. **Result:** 35 cases agree with WP-3D within 1 %. The CMMQ band grows (+0.81 /s), the folds switch sign, the pitchfork breaks symmetry into opposite turns, and mirrored nonlinear trajectories are bit-identical. **Conflict audit:** every public CMMQ printing agrees and every source-supported alternative reading is ruled out by the source's own equilibria, yet both theses present α 11–14° as stable. **Verdict: PUBLIC PRINTINGS AGREE — EXECUTED AUTO MODEL MAY DIFFER.** Nothing was changed. |
| Live-flight readiness | exact: **FAIL-CLOSED** · research: **structurally prepared, not live-ready** | — | — | — | exact: profile invalid → not structurally prepared → no live or shadow loads. Research (WP-1): the separate profile `F15_AFIT_BAUMANN_DAVISON_MACH06_20K_RESEARCH` reaches STRUCTURALLY_PREPARED at M 0.6 / 20,000 ft only, with its propulsion non-authoritative and not accepted. F15Replacement ownership is not implemented; no scene or prefab is wired. |
| **Research configuration (WP-1)** | implemented, **separate** | research-model data only (Davison driver + ARO10 geometry) | `F15_RESEARCH_PROFILE_V1.0.md` | **research** | valid at M 0.6 / 20,000 ft only. Surfaces are held at zero travel, so the aircraft is uncontrolled. Fixed 8,300 lbf **total** thrust with its source's nose-up thrust-line moment. |

**Thrust today:**
- **Exact path and F100 layer:** zero. `MavF100ThrustDeck` has no declared scale and no power-lever convention, and `MavF15PropulsionSystem.thrustDeck` is null.
- **Research configuration only:** the research model's own constant 8,300 lbf total at its source condition, from `MavF15AfitResearchFixedThrust`, which lives outside `MavF100*` and refuses under any other profile.

---

## 2. Exact vs research — the boundary

### EXACT NASA 836 path

**Known:**
- NASA F-15B tail 836 / USAF 74-0141, pre-Quiet-Spike baseline target
- two F100-PW-100
- Table-1 **Baseline** mass state at 8,000 lb fuel
- physical length, span and height
- approximate ≈23,500 lbf SLS full-augmentation thrust anchor

**Unavailable:**
- `S`, `c̄`, coefficient reference `b`, moment reference
- exact aerodynamic database
- exact FCS gains and schedules
- exact control-surface hard stops and actuator rates
- engine sub-configuration sufficient for a PW-100(3) transfer
- engine mount coordinates
- a complete thrust deck (Mach × altitude × power)

**Therefore:** exact NASA 836 live flight remains **FAIL-CLOSED**.

### RESEARCH path

**Known:**
- Baumann / ARO10 aerodynamics at M 0.6 / 20,000 ft (longitudinal, and six-axis)
- F100-PW-100(3) TP-1034 normalized net-thrust characteristic
- preproduction and family FCS evidence — **documented only, not wired**
- **WP-1:** a separate research profile `F15_AFIT_BAUMANN_DAVISON_MACH06_20K_RESEARCH` with its own geometry (608 ft² / 42.8 ft / 15.94 ft), Davison's mass and inertia (37,000 lb; 25,480 / 166,620 / 186,930 / −1,000 slug-ft²), and the fixed 8,300 lbf total thrust. It reaches generic structural readiness. See `F15_RESEARCH_PROFILE_V1.0.md`.

It exists for research and cross-validation only, and **must not claim NASA 836 exactness**.

### How the separation is enforced

| Boundary | Mechanism | Test |
|---|---|---|
| research aero never becomes exact | `MavF15AeroSourceMode` defaults to `ExactNasa836Unavailable`; research needs `allowCrossValidationResearchModel` | `[G7]` covers the default; `[X4]` (WP-1) exercises the `MavF15AeroModel` opt-in refusal on a real component |
| family FCS never becomes exact | `MavF15FcsModes` provenance floor per mode (`ExactNasa836Unavailable`, `F15FamilyReference`, `AFITResearch`) | `[C8]` |
| 836 structure never becomes a gain | `MavF15Nasa836FcsStructure` grades and switches only; `AnyNumericFcsDataShown = false` | `[S5]`, `[S7]` |
| validation data never becomes model data | data types live in `Validation/`; source scan of every other file | `[V3]` |
| research demonstrated range never becomes a hard stop, rate or actuator limit | separate types; no member converts between them | `[A1]`, `[A2]`, `[A3]` |
| source reproduction never becomes 836 authority or unrestricted extrapolation | mode lives on the research profile and needs the body to fly it; bounded to source-exercised speeds at the fixed-density altitude | `[W7]`–`[W10]` |
| Table VII never reaches runtime | data types live in `Validation/`; source scan | `[W7]` |
| research trim never reaches the exact path or the answer it is judged against | refuses every id but the research one; no other source names it; takes only an id, V and a start; stabilator box is the demonstrated range, never travel | `[R6]`, `[R9]`, `[R10]` |
| turning trim never solves phi, never reads the answer, never holds a root on a numerical box | phi is an input only (`SolveTurning(id, phi, start)`); p, q, r and θ boxes are labelled NUMERICAL_SEARCH_BOUND, never physical; φ = 0 is refused and handed to the symmetric solver | `[H6]`, `[H7]`, `[H13]`, `[H2]` |
| research stability never reaches runtime or the exact path | the source RHS is pure and research-only, and a source scan shows nothing outside `Validation/` calls it; the linearization refuses every id but the research one; the eigensolver lives in `Validation/` and no runtime file names it | `[S6]` |
| time-domain stability never reaches runtime or a body | the RK4 integrator, harness and conflict audit live in `Validation/`, refuse every id but the research one, and name no Rigidbody, Unity time, F100 or atmosphere model | `[T2]`, `[T11]` |
| research thrust never wears 836 identity | `MavF100PathSeparation`, `MavF100EngineFamilies.MayCombine`, `MavF100Nasa836EngineEvidence.SubConfigurationKnown = false` | `[E22]`–`[E28]` |
| family geometry never fills exact S/c̄/b | `MavF15ReferenceGeometrySources.TrySelectReferenceSet` accepts only `DirectExact836` / `ExplicitProductionReferenceUsedBy836Model` from one set | `[G2]`–`[G8]` |
| physical span never normalizes | `PhysicalWingSpanM` is separate from `MavAeroReferenceGeometry.wingSpanM` | `[G4]` |
| wrong mass column never returns | `MavF15Table1MassStates` labels all three columns; the spike-extended tuple is pinned | `[M1]`, `[M5]` |
| research profile never aliases or contaminates the exact one | separate ID, mass state, provider and thrust type; research thrust refuses under any other identity | `MavF15ResearchContaminationValidation` `[X1]`–`[X6]` |

---

## 3. Source corrections applied (all known, all in-branch)

| Correction | Where |
|---|---|
| Frozen mass state was Table 1's **spike-extended** column → now **Baseline** | `F15_FULL_SCALE_TARGET_FREEZE_V0.1.md` |
| Triangle test ran on the body-axis diagonal → now runs on **principal moments** | `MavF15Table1MassStates` |
| Physical 42.8-ft span sat in the coefficient reference-span field → reference `b` = 0; physical span kept separately | `MavF15ReferenceData` |
| TM-72861 was described as publishing CAS feedback gains → it publishes none | `F15_DN1180_SOURCE_LINEAGE_V0.1.md` §6 |
| CP2903B was labelled "public-source blocked" → unavailable in held sources, restricted primary specification, search hint | `F15_R5_F100_PROPULSION_STATUS_V0.1.md` |
| Path C was described as empty → it holds the ≈23,500 lbf anchor; propulsion-gate comments corrected | `MavF100PropulsionPaths.cs` |
| Research propulsion path conflated PW-100(3) with prototype 2-7/8 → three families | `MavF100EngineFamilies` |
| ~22.4 klbf bound scoped to the TP-1034 normalizer only | `MavF100EngineFamilies.BoundAppliesTo` |
| P680063 serial treated as one configuration → six-phase chronology | `MavF100ConfigurationLineage` |
| Research SREF = 608 "not located" → located (Davison driver program) | `MavF15ReferenceGeometrySources` |

---

## 4. Recorded decision — the F-15A–D %MAC datum is NOT promoted

AD-A244044 reproduces `%MAC = (FS − 508.1)/191.33 × 100` "for all A through D models of the F-15". Applied to 836's Table-1 CG it would give ≈FS 558.50.

**Decision: not promoted.**
- The relation stays `PublicReproduction × ProductionF15Family` / `F15_FAMILY_SUPPORT`.
- The derived agreement with 836 stays `CROSS_VALIDATION_ONLY`.

**Reasons:**
- it is not the A4172 original;
- the equation carries no upstream citation;
- it would close only an absolute CG station, not `S`, `c̄`, `b` or the moment reference;
- the runtime does not need it today.

This is closed unless a primary source appears.
