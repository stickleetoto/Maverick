# F-15 — User Validation Plan (V1 Freeze)

**For:** the user running Unity 6000.3.16f1 locally.
**Rule:** record what actually ran. A test that is not run is **NOT RUN**, never "validated".
**No pass criteria are invented here.** Where a check has no sourced tolerance, the plan says *observe and record*.

---

## Phase 1 — open, compile, run the existing suites

1. Open the project in Unity 6000.3.16f1 on branch `claude/f15-full-implementation`. Let it import.
2. **Compile.** The Console must show **0 compile errors**. The branch was checked at 352 runtime + 33 editor scripts, 0 errors.
3. **Close the Editor** (batch mode needs the project unlocked). Run each F-15 suite headless. PowerShell, one line per suite:

   ```
   & "D:\unitys\6000.3.16f1\Editor\Unity.exe" -batchmode -quit -nographics -projectPath "E:\unity project\Maverick" -executeMethod MaverickFresh.FlightDynamics.EditorTools.MavFdmValidationBatchAdapter.RunBatch -fdmSuite f15-mass -fdmMode sync -fdmType MaverickFresh.FlightDynamics.Validation.MavF15MassReferenceValidation -fdmMethod RunAll -fdmOut "E:\f15-results\f15-mass.json" -logFile "E:\f15-results\f15-mass.log"
   ```

   Repeat, changing `-fdmSuite`, `-fdmType` and the output names:

   | suite id | `-fdmType` (namespace `MaverickFresh.FlightDynamics.Validation.`) | V1 known-good |
   |---|---|---:|
   | `f15-mass` | `MavF15MassReferenceValidation` | 45 / 0 |
   | `f15-geometry` | `MavF15ReferenceGeometryValidation` | 51 / 0 |
   | `f15-transcription` | `MavF15BaumannTranscriptionValidation` | 28 / 0 |
   | `f15-controlpath` | `MavF15ControlPathValidation` | 79 / 0 |
   | `f15-propulsion` | `MavF15PropulsionValidation` | 198 / 0 |
   | `f15-contamination` | `MavF15ResearchContaminationValidation` | 32 / 0 |
   | `f15-research-profile` | `MavF15ResearchProfileValidation` | 35 / 0 |
   | `f15-research-pipeline` | `MavF15ResearchPipelineValidation` (editor-only) | 13 / 0 |
   | `f15-fcs-structure` | `MavF15Nasa836FcsStructureValidation` | 24 / 0 |
   | `f15-validation-data` | `MavF15Nasa836ValidationDataValidation` | 16 / 0 |
   | `f15-research-authority` | `MavF15ResearchControlAuthorityValidation` | 21 / 0 |
   | `f15-source-condition` | `MavF15BaumannSourceConditionValidation` | 26 / 0 |
   | `f15-research-trim` | `MavF15ResearchTrimSolverValidation` | 34 / 0 |
   | `f15-turning-trim` | `MavF15ResearchTurningTrimValidation` | 31 / 0 |
   | `f15-stability` | `MavF15ResearchStabilityValidation` | 40 / 0 |
   | `f15-time-domain` | `MavF15ResearchTimeDomainValidation` | 30 / 0 |
   | **total** | | **703 / 0** |

4. **Inspect the logs.**
   - Each JSON must say `"status": "PASS"`.
   - Each `.log` holds the full report, one line per check, `PASS`/`FAIL`.
   - A `FAIL` line names the violated rule.
   - A run with no JSON is usually a lost result on batch shutdown, not a regression. Re-run once before investigating.

The F-15 suites are **not** on the `Maverick/Flight Dynamics` menu. "Run All Flight Dynamics Validation" runs F-16 and shared suites only.

---

## Phase 2 — research-mode PlayMode smoke test

**Status after WP-1:** a separate research profile exists and reaches structural readiness. `f15-research-pipeline` already drives the real `MavSixDoFBody` through the editor seam (not PlayMode) and checks finite loads.

**Optional user PlayMode smoke, unsaved scratch scene only:**
- Put `MavSixDoFBody`, `MavF15AfitResearchFlightDynamicsProfile`, `MavF15AeroModel` (six-axis research mode, `allowCrossValidationResearchModel` on), `MavF15ControlActuator`, `MavManualPilotCommandSource`, `MavF15ControlLaw` and `MavF15AfitResearchFixedThrust` on one GameObject.
- Wire them as the pipeline suite's rig does.
- Place it at 6,096 m moving +Z at Mach 0.6.
- Set `simulationEnabled` and `allowStructuralOnlyLoadApplication`.

**Expect:**
- the research profile ID in the status;
- STRUCTURALLY_PREPARED, not live-ready;
- finite loads;
- neutral surfaces (zero travel), so the aircraft is **uncontrolled**;
- aero and thrust refusing the moment Mach or altitude leaves the source condition, which happens almost immediately in free flight.

Check only finiteness, identity and envelope refusal. Claim no handling or trajectory.

The **exact** path is still fail-closed; the check below still applies.

**What can be checked now, in an unsaved scratch scene** (do not save scenes or prefabs):

1. Put `MavSixDoFBody`, `MavF15FlightDynamicsProfile`, `MavF15AeroModel` and `MavF15PropulsionSystem` on one GameObject with a Rigidbody. Enter Play mode.
2. **Expected, fail-closed:**
   - `MavF15FlightDynamicsProfile.debugProfileStatus` begins `INCOMPLETE: reference geometry is invalid`.
   - The body's `debugProfileValid` is false and readiness is not structurally prepared.
   - No FDM loads are applied.
   - `MavF15PropulsionSystem.debugInstallationGaps` lists the missing deck and undeclared geometry.
3. Setting `MavF15AeroModel.sourceMode` to a Baumann research mode **must not** change any of the above. The exact profile stays invalid, and research geometry never enters it.

**Claim nothing about NASA 836 from this phase.**

---

## Phase 3 — static and structural checks

| Check | Can it run now? | How / why not |
|---|---|---|
| Static ground checks (weight on gear, taxi) | **NO** | no F-15 ground-contact or landing-gear model is part of this branch |
| Control direction / sign — control law → actuator → surface state | **YES**, headless | covered by `MavF15ControlPathValidation` (routing, sole actual-state owner, neutral output) |
| Control direction / sign — surface → aircraft response | **NO** (exact) · **coefficient level only** (research) | exact sign conventions and hard stops are unavailable. Research-mode sign conventions come from Davison's listing (CX +fwd, CY +right, CZ +down, Cl right-wing-down, Cm nose-up, Cn nose-right). The research profile now exists (WP-1), but it holds every surface at **zero travel**, so no surface response can be observed in the body. **WP-2:** the stabilator sign convention is verified at coefficient level against Baumann Table VII (`[A5]`). |
| Left/right engine independence | **YES**, headless | `[E*]` propulsion checks, independent slot runtimes. In PlayMode, only independence of per-engine state can be observed: F100 thrust is zero in every mode. The research configuration's fixed thrust is one total force with no engines. |
| Zero / failed-engine asymmetric loads | **NO** | needs engine mount coordinates (`geometryDeclared = false`) **and** nonzero thrust. Meaningless until both exist. |

---

## Phase 4 — trim and response (research mode only, once a research profile exists)

**Prerequisites:**
- a research-tagged profile provider — **done (WP-1)**;
- a research-only thrust input — **done (WP-1)**;
- **research-scoped control-surface travel — NOT available.** Trim needs stabilator deflection, and the research profile holds zero travel. WP-2 declared a research *demonstrated* range (−25…−5°) for static evaluation only; it is not travel.
- ~~a research speed-domain decision~~ — **resolved in WP-3A.** Set `conditionMode = SourceReproduction` on the research profile to admit the source-exercised 218.5–699.7 ft/s at 6,096 m. Every coefficient away from Mach 0.6 is flagged as extrapolated.
- **Static reference already reproduced:** `f15-source-condition` `[W4]` evaluates all 170 assembled Table VII equilibria and reports residuals against print precision.
- **Nonlinear time-domain check off the body (WP-3E):** `f15-time-domain` `[T4]`–`[T10]` print predicted vs measured growth, decay and frequency for 35 cases, `[T8]` the symmetry-breaking runs, and `[T14]` the source-conflict hypotheses. `-fdmMethod ExportDataset` writes the time-domain CSVs into the log; extract them with `Docs/Reference/Data/F15/stability/extract_from_log.py <log> Docs/Reference/Data/F15/stability_time_domain`. Nothing is flown: it integrates the source equations only.
- **Source stability off the body (WP-3D):** `f15-stability` `[S12]` prints every equilibrium's class and its largest-real-part eigenvalue. `[S13]` gives the source comparison, and `[S14]`/`[S15]`/`[S18]` the folds, the pitchfork and the Hopf points. The same method with `-fdmMethod ExportDataset` writes the CSV dataset into the log; extract it with `Docs/Reference/Data/F15/stability/extract_from_log.py`. These are the SOURCE MODEL's eigenvalues, not the aircraft's.
- **Turning trim solved off the body (WP-3C):** `f15-turning-trim` `[H9]` recovers the 80 non-symmetric turning states with φ fixed. Per state it prints the eight printed/recovered values, ψ̇, the print floor and the numerical uncertainty.
- **Research trim solved off the body (WP-3B):** `f15-research-trim` `[R5]` recovers the 89 symmetric states from perturbed starts, and prints per-state differences against the print-resolution floor.
- **Runtime density gap — NOT closed:** the body computes q from ISA density (`[R13]`), so a flying research trim does not yet reproduce the source's fixed-density q. See D11.

Baumann's own equilibria use a fixed **8,300 lb** total thrust at 20,000 ft (DTIC ADA217366, PDF p.34 and p.124). That figure is a research-model constant and must never enter R5 propulsion.

| Test | Reference available? | Criterion |
|---|---|---|
| Trim at the source condition | **Yes, sourced:** Baumann Table VII "Tabulation of Low α Equilibrium Conditions" (ADA217366 PDF pp.124–129) — α, β, rates, θ, φ, V against stabilator deflection, thrust 8,300 lb, 20,000 ft | **Observe and record differences.** Maverick transcribes Davison's 1992 version of the model, which is not identical to Baumann's 1989 original, so no tolerance is asserted without a version-matched source. |
| Straight-and-level | No sourced level-flight trim at M 0.6 | observe and record only |
| Small-perturbation response (pitch, roll, yaw doublets) | No sourced response traces for the research model | observe: finite, bounded, inside the transcribed α/β span. No numeric criterion. |
| Longitudinal / lateral response vs flight data | NASA 836 flight-identified derivative **trends** exist (TM-2008-214634, TM-2012-215978; plots) | comparison needs matching reference dimensions and moment reference, which are unavailable for 836. **Not meaningful as pass/fail.** |

---

## Phase 5 — research mode against its own source envelope only

- **Aero:** M 0.6 ± 0.001 at 20,000 ft (6,096 ± 1 m), within the transcribed α/β span. Outside it the model **refuses**; confirm that refusal is what you see.
- **Thrust characteristic:** the 7 documented TP-1034 conditions only, ± 150 m / ± 0.02 M. Off-point queries return no number.
- Never compare research results against NASA 836 numbers and call it agreement.

---

## Tests that cannot be meaningful while the exact gaps remain

- **Any exact NASA 836 flight, trim, handling or performance test.** `S`, `c̄` and `b`, the exact aerodynamic database, FCS gains, hard stops and thrust are all unavailable.
- **Any thrust-dependent test in any mode** (climb, acceleration, engine-out, thrust asymmetry). Dimensional thrust is zero.
- **Any engine-out yaw test.** No mount geometry.
- **Any closed-loop FCS handling test.** Every stage is refused, and the law outputs neutral.
- **Any ground test.** No F-15 ground-contact model.
