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
| Flight-control architecture | implemented | family architecture (TM-72861, AFIT theses) | `F15_R3_FCS_IMPLEMENTATION_STATUS_V0.1.md` | **research** (structure) | 11 stages, each gated and refused when its gain is unavailable. **Not yet cross-checked** against 836's own simplified pitch/roll/yaw block diagrams, NASA/TM-2009-214651 figs. 3–5 (repeated in TM-2012-215978 figs. 3–5). Those are structure only, with two exact switch points printed: the mechanical ARI is switched out above **Mach 1.5**, the roll-yaw crossfeed above **Mach 1.0**. See the opportunities doc, A1. |
| Flight-control numeric law | **UNAVAILABLE** | none | DN-1180.01-238-458 Rev. D **not publicly located**. TM-72861 publishes no feedback gain. | **unavailable** | the law outputs neutral by construction |
| Control-surface authority | exact: **UNAVAILABLE** | family sets conflict (four public sets) | `F15_DN1180_SOURCE_LINEAGE_V0.1.md` §3 | **unavailable** | exact hard stops are zero travel |
| Engine identity | FROZEN | exact 836 | F100-PW-100 × 2 — TM-2008-214634 p.6; NTRS 20070032807 p.10 | **exact** | `MavF15PropulsionSkeleton`, two independent engine slots |
| Propulsion research characteristic | implemented (shape only) | CompatibleSupport, family A (PW-100(3) simulation lineage) | TP-1034 fig. 17, 63 digitized points, 7 conditions | **research** | `MavF100NormalizedNetThrustModel` answers in *fraction of design maximum*, never newtons |
| NASA 836 dimensional propulsion anchor | recorded, **not used** | exact 836 aircraft datum | ≈23,500 lbf (104,533 N) uninstalled SLS full augmentation — NASA/TM-2005-213670 | **approximate** | refused as a scale: `MavF100PathSeparation.DimensionalizeForTarget` needs build equivalence and 836's engine sub-configuration, and neither is known |
| Engine installation geometry | **UNAVAILABLE** | none | — | **unavailable** | `geometryDeclared = false`; no thrust-line moment, no engine-out yaw |
| Source envelope | exact aero: none · research: fixed | exact envelope is an inverted interval | Baumann/Davison source condition; TP-1034 condition list | exact **unavailable** · **research** | research aero: M 0.6 ± 0.001 at 6,096 ± 1 m, transcribed α/β span. Thrust model: 7 documented points ± 150 m / ± 0.02 M. Off-point queries are refused, not extrapolated. |
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
