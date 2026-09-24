# F-15 — Source-Faithful Research Trim Solver (V1.0, WP-3B)

> **Research configuration only** (`F15_AFIT_BAUMANN_DAVISON_MACH06_20K_RESEARCH`). **Not NASA 836 trim. Not flying trim.** No Rigidbody is flown, and no surface gains travel.

**Headline:**
- **What it is.** A dedicated trim plant for the AFIT/Baumann/Davison research model. It solves the source's own symmetric equilibrium problem: fixed source density, varying true airspeed, a fixed 8,300 lbf total thrust with its 0.25-in thrust-line moment, and the transcribed coefficient routine. It is built on the shared `MavDampedNewtonSolver`.
- **Recovery.** Every one of the **89** usable symmetric Table VII equilibria is recovered from **four** deterministic perturbed starts: **356 / 356 converged** in 2–3 iterations.
- **Accuracy.** Every recovered α, stabilator and θ lies **inside the Table VII print-resolution floor**: max |recovered − printed| is 6.7e-3°, 8.5e-3° and 6.8e-3°, and 0 of 89 states exceed the floor in any unknown.
- **Other roots.** The wide deterministic start grid found **no second root** at any V probed.

| | |
|---|---|
| Solver | `F15/MavF15AfitResearchTrimSolver.cs`: `MavF15AfitResearchTrimSolver`, `MavF15AfitResearchSourceEnvironment` |
| Round-trip harness | `Validation/MavF15TableViiTrimRecovery.cs` (validation only; the one place the published answer is read) |
| Tests | `Validation/MavF15ResearchTrimSolverValidation.cs` `[R1]`–`[R13]`, 34 / 0 |
| Source | Baumann, AFIT/GAE/ENY/89D-01, DTIC ADA217366: driver `D2ICCV28` (FUNX, PDF pp.95–96), Table VII (PDF pp.124–133) |

---

## 1. Audit — why not `MavSteadyFlightTrimSolver`

The generic solver (`Core/MavTrimSolver.cs`) is sound for what it models. It does not model the research source.

| Generic solver assumes | Research source does | Consequence of adapting the source |
|---|---|---|
| density `MavAtmosphereModel.Sample(altitude)` | `RHO = .0012673` slug/ft³ for every state; no altitude state | ISA at 6,096 m is **6.83e-4 below** RHO; q would shift by the same fraction |
| powered trim: flight-path angle **imposed**, thrust **solved** (`requiredThrustN`, throttle bisection) | thrust **fixed** at 8,300 lbf total; the flight-path angle **falls out** of the equilibrium | the source's problem would become a different problem |
| thrust along body X **through the CG**; any thrust-line moment is refused (`IsAxialThroughCentreOfGravity`) | thrust has a 0.25-in arm: `CMM += THRUST*(0.25/12)/(QBARS*CWING)` | the source's thrust is refused outright (`[R13]` shows it still is) |
| SI units, g₀ = 9.80665 | ft, slug, lbf; G = 32.174 ft/s² | conversion error in every term |

**Decision: option A.** This is a dedicated research trim plant, not a generalization.
- **Reused:** the shared `MavDampedNewtonSolver`, with no duplicated Newton or Gaussian-elimination code.
- **Unchanged:** the generic solver and every existing aircraft.

## 2. The source equations and the unknown set

Baumann's model has 8 states (α, β, p, q, r, θ, φ, V). The stabilator is AUTO's continuation parameter `PAR(1)`. At an equilibrium all eight FUNX derivatives vanish.

At a **symmetric** state, with β = p = r = φ = 0 and aileron = rudder = differential tail = 0:

| FUNX | Equation | Symmetric reduction |
|---|---|---|
| F(2) β̇, F(3) ṗ, F(5) ṙ | lateral | vanish identically: `[R4]` measures Y, L and N **exactly 0** |
| F(7) φ̇ = p + (q sin φ + r cos φ) tan θ | kinematics | 0 once q = 0 |
| F(6) θ̇ = q cos φ − r sin φ | kinematics | forces **q = 0** |
| F(1) α̇ and F(8) V̇ | wind-axis force balance | ≡ body **X** and **Z** force balance (a rotation by α) |
| F(4) q̇ | pitching moment, thrust-line moment included | ≡ **M** |

That leaves **three equations in four quantities** (α, θ, V, stabilator). One must be the parameter.
- **The source's choice** is the stabilator.
- **This solver's choice** is **V**, as Table VII prints it.
- **Unknowns:** α, symmetric stabilator, θ.
- **Residuals:**
  - X/W = (q̄S·CX + T − W sin θ) / W
  - Z/W = (q̄S·CZ + W cos θ) / W
  - M/q̄Sc̄ = CM + T·(0.25/12) / (q̄Sc̄)

**θ is its own unknown; θ = α is never assumed.** The source equilibria climb or descend: the recovered γ = θ − α runs from **+2.35°** (V 342.9 ft/s) to **−5.27°** (V 288.7 ft/s).

**Mass:** 37,000 lb. **Inertia:** not needed. With p = q = r = 0 every inertial coupling term is zero.

## 3. Source environment

`MavF15AfitResearchSourceEnvironment` holds:
- **RHO:** 0.0012673 slug/ft³.
- **G:** 32.174 ft/s².
- **20,000 ft:** a *label* for the density, not an input.
- **q:** `DynamicPressurePsf(V) = 0.5·RHO·V²`, whose only input is V.

It samples no atmosphere model, and nothing outside the research trim uses it (`[R10]`).

## 4. Thrust

Thrust enters as **8,300 lbf total** along body +X, and **8,300 × 0.25/12 ft·lbf nose-up**.
- **Applied once,** in `EvaluateSymmetricResidual`.
- **The transcribed routine carries no thrust term.** `[R3]` checks that the CM the plant uses is the routine's own.
- **Not in the model:**
  - throttle, required thrust or engines;
  - the `MavF100*` layer, a thrust deck or `MavPropulsiveLoads` (`[R2]`, `[R11]`).
- **Exactly once** (`[R3]`), checked two ways:
  - term by term, M/q̄Sc̄ = routine CM + T·arm/(q̄Sc̄);
  - against the independent WP-3A static evaluator, which agrees to ≤ 2e-16 at all 89 printed states.
  - A second thrust moment would shift M/q̄Sc̄ by ≥ 2.4e-4, about **1,100×** the largest pitch print floor.

## 5. Controls and search bounds

| Unknown | Search box | What it is |
|---|---|---|
| stabilator | **−25…−5°** | `MavF15ResearchDemonstratedControlRange`, what the source tabulated. **Not a hard stop.** |
| α | −4…90° | the transcribed routine's breakpoint span (`MavF15BaumannMach06Domain`); not a validity claim |
| θ | ±89° | the source's Euler kinematics are singular at ±90°; numerical only |

- **How the stabilator enters:** only through `MavF15ResearchStaticControlState`, labelled STATIC_EQUILIBRIUM_VALIDATION_ONLY. It admits only values inside the demonstrated range.
- **Nothing converts into authority:** no surface state, actuator limit or hard stop.
- **After every trim run** (`[R9]`):
  - the F-15 actuator still declares no hard stop and no rate;
  - the research profile still has zero travel.
- **At the range edge:** a result that reaches it is flagged `stabilatorAtDemonstratedRangeEdge` ("NOT a hard stop").

## 6. Numerical core

| Setting | Value | Why |
|---|---|---|
| `NumericalSolverTolerance` | **1e-6**, on max(\|X/W\|, \|Z/W\|, \|M/q̄Sc̄\|) | a **termination** epsilon, not a source-validation tolerance |
| finite-difference step | 1e-2° | central differences |
| iterations / line-search halvings | 60 / 30 | never reached: max 3 iterations in the round trip |

**Why 1e-6.**
- Unknowns are float degrees and the coefficients float, so converged points sit at 1e-8 to 1e-7. At 1e-7 one start at V = 293 ft/s stalled on noise at 1.01e-7.
- At 1e-6 the termination error in the unknowns is ~2e-5°. That is 300× below the print floor, and the four starts plus a generic start agree to 3.1e-5° (`[R6]`).

**Determinism:** two full runs are bit-identical (`[R7]`).

## 7. Table VII round trip (89 symmetric equilibria)

**Method** (`MavF15TableViiTrimRecovery`):
1. Take the published state.
2. Add one of four fixed perturbations: (Δα, Δstab, Δθ) = (+1, +0.5, −2), (−1, −0.5, +2), (+2, −1, +3), (−2, +1, −3)°.
3. Solve at the printed V.
4. Compare the recovered state with the printed one.

**Where the published row is used:**
- **Only** to build the start (every start moves every unknown by ≥ 0.5°) and as the reporting target.
- **Never inside the residual.** The solver takes only an id, V and a start. It lives in `F15/`, and its code names neither Table VII nor the Validation namespace (`[R6]`, `[W7]`).
- **Start independence:** a generic start (15, −12, 15)° that knows nothing of the table reaches the same root at every printed V.

**Recovered-state print floor.** The solver takes V as printed, to 4 digits. A perfect model could therefore still differ from print by

  floor(x) = |dx/dV| · ½ unit in V's last digit + ½ unit in x's own last digit

where x has 7 digits for α and the stabilator, and 4 for θ. dx/dV is measured with the solver itself. The floor is **context, not a pass threshold.**

**Results:**

| | α | stabilator | θ |
|---|---|---|---|
| max \|recovered − printed\| | 6.66e-3° | 8.47e-3° | 6.82e-3° |
| mean \|recovered − printed\| | 3.07e-3° | 3.94e-3° | 2.69e-3° |
| max diff / floor | 0.96 | 0.96 | 0.91 |
| states above floor | **0 of 89** | **0 of 89** | **0 of 89** |

- **Outcomes:** 356 / 356 `Converged`, none refused. Iterations: min 2, mean 2.77, max 3.
- **Independent check:** at the recovered states the WP-3A evaluator gives, in all six axes:

  | Residual | Max \|res\| |
  |---|---|
  | X/W | 2.5e-7 |
  | Z/W | 9.8e-7 |
  | M/q̄Sc̄ | 6.1e-8 |
  | Y/W, L/q̄Sb, N/q̄Sb | 0 |

- **Printed states in the trim plant, static:** X/W ≤ 8.9e-5, Z/W ≤ 3.1e-4 and M/q̄Sc̄ ≤ 1.7e-7, identical to WP-3A.

The per-state table (point, V, printed and recovered α, stabilator and θ, each difference and floor, and γ) is printed by `[R5]` in the suite report.

**Reading.**
- **What it shows:** the research model's own equations, solved from displaced starts, land on Baumann's published symmetric equilibria. Each difference is no larger than the table's own printed digits allow.
- **What it does not show:** the Mach 0.6 fit is applied at 288.7–342.9 ft/s (implied Mach 0.28–0.33). That is source-exercised, **not aerodynamically validated**.

## 8. Multiple roots and branches

The source does bifurcation work, so one V was never assumed to have one equilibrium.

**Grid probe** (`[R8]`):
- **Starts:** a wide deterministic grid of 13 α (−2…88°) × 5 stabilator (−24…−6°) × 7 θ (−80…80°) = **455 starts per V**.
- **Kept:** every distinct converged root, with its first start, its residual, and the nearest published state.
- **Root identity:** 1e-3°, a numerical epsilon.

| V, ft/s | Converged | Distinct roots | Root (α, stab, θ, γ)° | Nearest published | Not converged |
|---|---|---|---|---|---|
| 218.5 | 0 / 455 | 0 | — | — | 455 at demonstrated-range edge |
| 288.7 | 420 / 455 | 1 | 19.18503, −17.30685, 13.91759, −5.267 | point 46, 2.4e-3° | 31 at edge; 4 other, residual norm ≥ 0.69 |
| 315.1 | 369 / 455 | 1 | 15.74975, −12.89807, 15.04032, −0.709 | point 23, 5.9e-3° | 86 at edge |
| 342.9 | 366 / 455 | 1 | 13.04664, −9.43757, 15.39231, +2.346 | point 47, 3.5e-3° | 89 at edge |
| 400.0 | 345 / 455 | 1 | 9.35277, −5.14857, 14.82130, +5.469 | none printed at this V; preserved | 108 at edge, 2 at α/θ bound |
| 500.0 | 0 / 455 | 0 | — | — | 455 at edge |
| **622.14** (Baumann's M 0.6) | 0 / 455 | 0 | — | — | 455 at edge |
| 699.7 | 0 / 455 | 0 | — | — | 455 at edge |

**Findings:**
- **No branch ambiguity was found** at any probed V: one root or none. The non-converged starts are:
  - pinned at a search bound, or
  - stopped at high-α local minima of the residual norm (norm ≥ 0.69, α ≈ 79°), not equilibria.
- **Symmetric equilibria exist inside the demonstrated stabilator range from V ≈ 252.4 to 402.9 ft/s.**
  - Found by continuation in V, 0.5 ft/s steps, from the generic start at 342.9 ft/s.
  - Outside that span the equilibrium needs a stabilator beyond −25° (slow) or −5° (fast). The solver stops at the range edge and says so; it does **not** widen the range.
  - **So there is no symmetric trim at Baumann's own Mach 0.6 speed inside the demonstrated range.** His text-only continuation start point, (α, θ, δe) = (5.0, 12.02, −1.39)°, lies outside the tabulated −25…−5°.
- **Table VII's two symmetric runs** (points 1–46 and 47–91) print the same equilibria twice. They are one branch, not two roots per V.
- **The pitchfork, point 165.**
  - Table VII prints one symmetric state inside its turning section: point 165 (V 377.4 ft/s, φ 1e-6°, β 2e-9°), where the mirror-image turns leave the symmetric branch.
  - The symmetric solver recovers it from the generic start. Differences: α +2.2e-3° (floor 3.0e-3), stabilator −2.5e-3° (3.4e-3), θ +3.8e-3° (5.6e-3).

**Limit:** Newton from a finite grid cannot *prove* uniqueness. The claim is "no second root found from 455 starts", not "unique".

## 9. CFX2 — kept, not resolved

- **Unchanged:** the App. C constant 0.09833517 stays. Baumann and App. B print 0.09833617.
- **Why trim cannot decide it:**
  - Every recovered Table VII state is at α ≤ 19.2°, where CFX2 carries no weight.
  - Only the continuation probe near 252 ft/s reaches into the 20–30° blend (α up to 25.8°). There the two printings differ by at most 1e-6 in CFX, scaled by the blend weight.
  - No printed state lies there, so nothing can discriminate the two printings.
- **`[R12]`, after every trim run:**
  - CFX at 40° still sits on 0.09833517;
  - the routine returns bit-identical coefficients before and after;
  - neither it nor any solver type has a mutable static a trim could tune.

## 10. Runtime density semantics — the recorded gap, and a follow-up design

**The gap (not hidden).**
- `MavSixDoFBody.UpdateStateAndAtmosphere` computes q from `MavAtmosphereModel.Sample(transform.position.y)`.
- **Runtime `SourceReproduction` therefore does NOT reproduce the source's fixed-density q.** It is right only at 6,096 m, and even there it is 6.83e-4 low.
- `[R13]` measures it on the body's own `BuildFlightState`: 3,010.28 Pa vs the source's 3,012.33 Pa at 315.1 ft/s.
- **Gravity differs too:** Unity's project gravity is 9.81 m/s², against the source's G = 32.174 ft/s² = 9.80664 m/s², a relative +3.43e-4.
- **WP-3B does not touch the shared six-DoF atmosphere path** (`[R13]`: bit-identical atmosphere samples before and after).

**Follow-up design:** a research-only runtime environment policy (not implemented; D11).

**1. Where it lives.** A `MavResearchEnvironmentPolicy` on the **research profile**: `StandardAtmosphere` (default), or `SourceFixedDensity`.

**2. When it is honoured.** Only when:
- the six-DoF body's provider is that profile,
- the profile is in `SourceReproduction` mode, and
- the id is the research id.

This is the same gating as `conditionMode` (`MavF15AeroModel.ResolveConditionMode`). The exact 836 id is refused.

**3. What it changes.** Only the `MavAtmosphereSample` the body passes to `BuildFlightState`:
- **Density** becomes RHO in SI, 0.653142 kg/m³, at every altitude.
- **The altitude gate:** `SourceReproduction`'s ±1 m altitude gate can then be dropped for density purposes. Altitude has no meaning in the source.
- **Speed of sound** stays ISA at 6,096 m, labelled "report-only". The source computes no Mach, and the research gate only uses Mach to flag the fit condition.

**4. Gravity** needs its own decision: source G through the load set with `useGravity = false`, versus the project's 9.81. It touches the single load-application boundary, so it is a separate, reviewed change.

**5. What it must not do:**
- change `MavAtmosphereModel`;
- change any non-research body;
- default on.

Its status must be visible in telemetry (`debugStatus`).

**6. Validation before any flight:**
- **Static first:** build a body state at a WP-3B trim point, and check that its q, its loads and this plant's residuals agree.
- **Then** a hold-trim test in PlayMode, which is not available today.

**Answer to the brief:** **yes**, a future Rigidbody source-reproduction mode still needs a density override. Without it the body flies ISA density, which is 6.83e-4 off at 6,096 m and diverges with any altitude change.

## 11. Turning equilibria — WP-3C candidate (prepared, not solved)

WP-3A already shows the six-axis equations reproduce all 81 assembled turning states to print precision. Solving them is WP-3C.

**Unknowns (8)**, at a chosen parameter (see below):
- α, β
- p, q, r
- θ, φ
- symmetric stabilator

Aileron, rudder and differential tail stay 0, as the caption states.

**Residuals (8):**

| Equation | Source | Notes |
|---|---|---|
| X, Y, Z force / W | F(1) α̇, F(2) β̇, F(8) V̇ | includes gravity (−W sin θ, W cos θ sin φ, W cos θ cos φ) and ω×V (r·v − q·w, p·w − r·u, q·u − p·v) |
| L / q̄Sb | F(3) ṗ | L + (Iy − Iz)·q·r + Ixz·p·q = 0 |
| M / q̄Sc̄ | F(4) q̇ | M + T·arm + (Iz − Ix)·p·r + Ixz·(r² − p²) = 0 |
| N / q̄Sb | F(5) ṙ | N + (Ix − Iy)·p·q − Ixz·q·r = 0 |
| θ̇ = q cos φ − r sin φ | F(6) | kinematics, rad/s |
| φ̇ = p + (q sin φ + r cos φ) tan θ | F(7) | kinematics, rad/s |

- **Rates:** normalized as p̂ = pb/2V, q̂ = qc̄/2V, r̂ = rb/2V.
- **Mass and inertia:** Ix 25,480, Iy 166,620, Iz 186,930, Ixz −1,000 slug·ft².
- **Heading rate** ψ̇ = (q sin φ + r cos φ)/cos θ is an output, not an equation.
- **Helical, not level:** the source has no altitude state, so these are helical climbs and descents at fixed density.

**Parameterization.** This is the crux, and why WP-3C is not a trivial extension.

**What Table VII shows:**
- The turning states run V 377.4–662.0 ft/s, stabilator −6.494…−5.717° (inside the demonstrated range) and φ −64.5…+63.0°.
- They leave the symmetric branch in a **pitchfork at point 165** (V 377.4 ft/s, stabilator −6.494°).
- V, the stabilator, α and θ are even in φ, so all have their extremum there.

**Options:**
- **Fix V, as WP-3B does.** For V above 377.4 ft/s there are **three** roots: the symmetric one (where it lies inside the range) and the ±φ mirror pair. The Jacobian is singular at the pitchfork. Every root must be preserved and labelled by the sign of φ.
- **Fix φ (recommended for recovery).**
  - **Unknowns:** α, β, p, q, r, θ, V, stabilator.
  - **Why:** each φ ≠ 0 selects one side of the pair. The problem is regular away from φ = 0, and the printed φ is a 4-digit input just as V is here.
  - **Mirror check:** solving at −φ must return β, p, r mirrored.
- **Fix the stabilator (the source's own choice).** At a fixed stabilator there are several roots, and folds appear. Only arclength continuation handles those cleanly, which is AUTO's method.

**Also needed:**
- a print-resolution floor over all eight printed inputs;
- the same no-answer-read, determinism and multiple-root discipline as WP-3B.

Stability (eigenvalues) stays unassessed. Table VII lists stable equilibria, and the solver finds all equilibria regardless of stability.

**Justified?** Yes. The symmetric solver is stable: 356/356 converged in ≤ 3 iterations, bit-deterministic. The six-axis equations already reproduce the turning states statically. The pitchfork structure is the one thing WP-3C must design for.

## 12. Tests — `MavF15ResearchTrimSolverValidation`, 34 / 0

| | |
|---|---|
| `[R1]` | fixed RHO and G; q(V) takes V only; q independent of α, stabilator and θ; not the ISA density; no atmosphere model named in the solver's code |
| `[R2]` | 8,300 lbf and 8,300 × 0.25/12 ft·lbf at all 356 recovered states; the force is constant in V; no throttle, required-thrust or engine member |
| `[R3]` | the thrust moment enters exactly once: term-by-term bookkeeping, the routine's own CM, and identity with the WP-3A evaluator |
| `[R4]` | the printed symmetric states are finite in the plant (reported against the floor); lateral residuals exactly 0 |
| `[R5]` | the round trip: 356 attempts, none refused, all converged to the numerical epsilon; no start is the answer; errors and floors reported |
| `[R6]` | the solver has no parameter an answer could arrive through; its code names neither Table VII nor Validation; start independence |
| `[R7]` | bit-identical reruns |
| `[R8]` | every preserved root is an equilibrium; no root outside the demonstrated range; ambiguity, the pitchfork point and the symmetric domain reported |
| `[R9]` | the search box is the demonstrated range, not a physical limit; no limit or surface state held or returned; actuator and profile still at zero travel |
| `[R10]` | refused under the exact 836 id, an empty id, and V outside 218.5–699.7 ft/s; no other source names the research trim |
| `[R11]` | no MavF100, thrust-deck, propulsion-model, propulsive-loads or throttle reference; no MavF100 source names the research trim |
| `[R12]` | CFX2 still 0.09833517 after every trim run; coefficients bit-identical; no mutable statics |
| `[R13]` | shared atmosphere bit-identical; the generic solver still refuses the thrust-line moment; runtime q gap and gravity recorded |

**Numerical tolerances asserted** (all numerical, none source-fidelity):
- the termination epsilon;
- root identity at 1e-3°;
- 1e-12 identity between two implementations of the same equation.

## 13. What WP-3B does not do

- **No flying:** no Rigidbody is flown and no PlayMode run is made.
- **No turning trim** (WP-3C).
- **Nothing changed** in the Baumann coefficients, CFX2 included; mass or inertia; NASA 836 data; F100 R5; or the generic trim solver, `MavAtmosphereModel` or `MavSixDoFBody`.
- **No authority invented:** no hard stops, no actuator rates, and no surface travel for the flying research body.
- **Untouched:** scenes and prefabs. F15Replacement is not enabled.
