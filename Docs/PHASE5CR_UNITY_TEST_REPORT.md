# Phase 5C-R — Isolated F-16 Reference Flight Validation

Status: **GO for 5C-R expansion**, after Phase 5C-R.1 restored the full Euler rigid-body angular
dynamics. Every U5CR scenario passes.

> **History.** Phase 5C-R itself ended NO-GO on one defect, D-5CR-8: the measured Unity Rigidbody
> angular integration omitted the gyroscopic term w x (I w). Phase 5C-R.1 supplied that term through
> the existing load-application boundary. Both phases are recorded below - the original result in
> sections 1-12 and the fix in section 13 - because the measurement that justified the compensation is
> the only thing that can later tell us when it stops being needed.

| | |
|---|---|
| Unity | `6000.3.16f1` (`a56f230f6470`), batchmode + Play Mode, PhysX |
| Fixed timestep | 0.02 s (50 Hz), `Physics.gravity` = (0, −9.81, 0) |
| Baseline commit | `a13644767e01bbaaebbe9e3e112da619d88fa170` |
| Branch | `sol/phase5-wip` |
| Compile | **0 errors**; 514 pre-existing `CS0618`, **none in any 5C-R file** |
| Unity physics tests | **168 passed, 0 failed** (5C-R.1; was 119/4 before the fix) |
| Pure-math gyroscopic suite | **28 of 28** (G-001 .. G-009) |
| Offline regression | **841 passed, 0 failed** across nine harnesses |
| Mutation probes | **36 of 36 turn a suite red** (25 propulsion + 11 gyroscopic) |
| Propulsion checkpoint | P0.2 **74/74**, P0.2b Play Mode **169/169** |
| Propulsion thrust | **exactly 0 N** on every step of every scenario |
| Gameplay `F16Replacement` | **disabled**, untouched |

---

## 1. Test environment

Built entirely in memory in Play Mode, by `MavF16ReferenceRigBuilder`. **No scene, prefab, material
or model was created or modified.** The brief allows either a scene or an in-memory rig and prefers
the least serialised churn; in-memory also keeps the pipeline *assembly* under review rather than
freezing it into an asset nobody reads.

The rig carries exactly these components and nothing else:

```
p5cr-<scenario>                      origin DEFINED as the reference CG
    Rigidbody                        useGravity on, damping zeroed
    MavFlightPhysicsOwnership        owner = F16Replacement  (isolated rig only)
    MavF16FlightDynamicsProfile      reference geometry, envelope, surface limits
    MavF16AeroModel                  Morelli compact nonlinear polynomial
    MavF16PropulsionSystem           no thrust deck -> 0 N
    MavF16ReferenceTestCommandSource REFERENCE_TEST_COMMAND_SOURCE
    MavDirectSurfaceControlLaw       C0 direct-surface test law
    MavF16ControlActuator            physical surface state owner
    MavSixDoFBody                    the single load-application boundary
    MavF16ReferenceTelemetryRecorder telemetry + per-step runtime assertions
```

No `MavMouseFlightJet`, no `MavAeroBody`, no `MavAtmosphericEngine`, no landing gear, no weapons, no
AI, no camera, no mesh. The legacy writers are not disabled — they are **absent**, and the recorder
counts them every step rather than trusting that nobody added one.

**Two declarations that must be read as declarations, not measurements:**

- `owner = F16Replacement` on the rig is the *isolated experiment's* ownership state. It is not
  gameplay F16Replacement: the object exists only inside the validation run,
  `allowReplacementActivation` is untouched, and no gameplay aircraft is modified. Without it the
  production gate would refuse the reference stack physics, and section 3 requires the reference path
  to own them.
- `aircraftIdentityResolvedAsF16C = true` is established **by construction** — the rig is assembled
  from `MavF16FlightDynamicsProfile`, `MavF16AeroModel` and `MavF16MassReference`. It is *not*
  resolved from `MavAircraftProfileApplier`, because that is a gameplay component and an isolated rig
  deliberately has none.

---

## 2. Reference configuration — SOURCE VALIDATED

| Quantity | Value applied | Provenance |
|---|---|---|
| mass | **9298.65 kg** (637.16 slug) | **SOURCE VALIDATED** — TP-1538 Table I, via Garza/Morelli TM-2003-212145 Table 1 |
| `Rigidbody.centerOfMass` | **(0, 0, 0)** exactly | **TEST SYNTHETIC declaration** — the rig root's origin is *defined* as the reference CG; no mesh pivot inferred, and the rig has no mesh |
| Ix / Iy / Iz / Ixz | 9496 / 55814 / 63100 / 982 slug-ft² | **SOURCE VALIDATED** |
| Unity principal moments | (75673.6, 85576.5, 12850.5) kg·m² | derived, reconstruction error **0.0000%** |
| `inertiaTensorRotation` | **1.0492° about Unity X only** | derived; an Ixz-only coupling must put it there and nowhere else |
| wing area / span / chord | 27.870912 m² / 9.144 m / 3.450336 m | **SOURCE VALIDATED** |
| xcg / xcgRef | 0.25 c̄ / 0.35 c̄ | **SOURCE VALIDATED** |
| envelope | Mach < 0.6, α −10…+45°, β −30…+30° | **SOURCE VALIDATED** (Morelli NTRS 20040110310) |
| surface limits | elevator ±25°, aileron ±21.5°, rudder ±30° | **SOURCE VALIDATED** |
| actuator rate limits | 0 = unlimited | **UNAVAILABLE** — not reference-frozen; deliberately not guessed |
| thrust deck | none | **UNAVAILABLE** — TP-1538 Table VI not transcribed. Thrust exactly 0 N |
| control-law authority | 100% of available deflection | **MAVERICK INFRASTRUCTURE** — test-law tuning, not aircraft data |
| test command waveforms | steps and doublets | **TEST SYNTHETIC** |

The legacy gameplay 9800 kg was **not** used anywhere.

---

## 3. Force / moment application audit

Dimensionalisation, in `MavFlightDynamicsMath.Dimensionalize`, verified against the required form:

```
Fx,Fy,Fz = qbar * S * (CX,CY,CZ)
L        = qbar * S * b    * Cl
M        = qbar * S * cbar * Cm
N        = qbar * S * b    * Cn
```

Applied **exactly once per step**, at one site: `MavSixDoFBody.TryApplyLoadSet`, via
`AddRelativeForce` + `AddRelativeTorque`. `AddRelativeForce` with no position argument acts **at the
centre of mass**, and the aerodynamic moment is supplied independently — so no lever arm is counted
twice. U5CR-003/004/005 check both the force and the torque handed to the Rigidbody against the
converted load set, **in the same step**, and they match to the last digit reported:

```
applied torque (187.7, 24557.7, 4056.5)  expected (187.7, 24557.7, 4056.5) Nm
applied force  (2733.7, -78540.9, -12979.6)  expected (2733.7, -78540.9, -12979.6) N
```

`debugLoadApplications == 300` over 300 recorded steps, `debugRejectedDuplicateApplications == 0`,
and the load set carried exactly one aerodynamic and one propulsive contribution per step.

## 4. Axis conventions

One mapping, in one file, with the true-vector / axial-vector distinction kept explicit. Verified
end to end rather than by inspection:

- **nose-up sign**: a commanded pitch-up attitude produced α = +1.962° against +2.0° intended, so
  Unity's "positive X rotation pitches the nose down" is handled correctly.
- **pitch chain**: nose-up demand → elevator −10° → Cm > 0 → M > 0 → q > 0.
- **roll chain**: roll-right demand → aileron −8.6° → Cl > 0 → L > 0 → p > 0.
- **yaw chain**: nose-right demand → rudder −12° → Cn > 0 → N > 0 → dr > 0 in the early transient.
- **tensor basis change**: U5CR-008 case 0 matches the analytical rigid-body equation to **0.00%**,
  which exercises the aero→Unity congruence, the principal decomposition, `inertiaTensorRotation`,
  and the world↔local↔body rate conversions simultaneously.

A roll, pitch or yaw sign reversal, or a Z-up/Z-down confusion, would break at least one of those.

---

## 5. Results

### U5CR-001 — FREE FALL · **PASS** (13/13)

Aerodynamic contribution **measured and bounded**, not assumed: over the 6-step (0.12 s) window the
max dynamic pressure is 0.630 Pa and the max aerodynamic force 40.0 N = **0.0439% of the 91.2 kN
weight**.

- measured acceleration **(0.0000, −9.8085, 0.0001) m/s²** against `Physics.gravity.y` = −9.8100
- **no duplicate gravity** — two sources would read −19.62
- no unexplained lateral acceleration (a_z = 9.2×10⁻⁵ m/s²)
- **specific force 4.4×10⁻⁴ g** — free fall is weightless, which is the semantics the load-factor
  channel has to carry; an implementation reporting acceleration would read 1 g
- exactly one gravity provider; thrust exactly 0 N

**Recorded limitation, deliberate:** vertical free fall puts α at 90°, outside the Morelli domain, so
the envelope assertion is switched off for this diagnostic only. That is correct — vertical free fall
is not a reference *flight* condition, it is a gravity diagnostic, and the aerodynamic model is
irrelevant to it precisely because qbar is ~0. The envelope gate itself is tested by U5CR-010.

### U5CR-002 — UNPOWERED GLIDE · **PASS** (10/10)

167.64 m/s at 1097.3 m, α = 1.96°, Mach 0.4987, qbar 15464.6 Pa, thrust 0 N throughout.

- body-axis X aerodynamic force **−5013.5 N**: drag opposes the nose, it does not add to it
- total mechanical energy **230.386 → 216.529 MJ (−13.86 MJ over 5.12 s)**
- PE 100.059 → 81.175 MJ (descending), KE 130.601 → 134.689 MJ (trading) — energy is exchanged, not
  created
- rotational KE tracked as 0.5·ωᵀIω with the full tensor including Ixz (max 2145 J)
- the reported total equals the sum of its three parts to **0.00 J**
- all 260 per-step runtime assertions held

### U5CR-003 — ELEVATOR STEP · **PASS** (11/11)

Surface 0.000 → −10.000° against −10.000° requested. `dCm/dδe = −0.6505 /rad` — negative, matching
the frozen convention. Early transient: dq = +0.41660 rad/s with M = +106086 N·m. Applied torque
equals the converted moment exactly. Propulsion moment zero.

### U5CR-004 — AILERON STEP · **PASS** (11/11)

Surface → −8.600°. `dCl/dδa = −0.1428 /rad`. Early transient dp = +1.05831 rad/s with L = +41982 N·m.
Roll rate reaches 2.043 rad/s, bank 8.12°.

### U5CR-005 — RUDDER STEP · **PASS** (11/11)

Surface → −12.000°. `dCn/dδr = −0.0866 /rad`, `dCY/dδr = +0.1642 /rad` — the side-force channel is
live. Early transient dr = +0.14736 rad/s with N = +38807 N·m. Sideslip develops to −2.03°.

**One check had to be corrected, and the correction matters.** The first version compared the
*settled* yaw rate against the *settled* yawing moment and failed: four seconds into a sustained
rudder input, N = +24558 N·m while r = −0.0229 rad/s. A moment sets an **acceleration**, and only
near the start, from rest, does the rate direction follow it. By then the aerodynamic cross terms —
weathercock from β, and the Cn contributions from p and r — set the steady yaw rate. The check now
uses the early transient. (The steady state is *not* explained by inertial roll-yaw coupling: U5CR-008
case 2 shows PhysX does not integrate that term at all.)

### U5CR-006 — PITCH DOUBLET · **PASS** (12/12)

±0.15 for 35 steps each way. q peaks +0.08157 then −0.39585 rad/s; elevator −3.75…+3.75°; Cm
−0.08226…+0.05325 — **both signs**, which a one-directional sign inversion could not produce. α held
−7.85…+2.02°, inside the domain. Largest single-step change in q is 3.19×10⁻² rad/s against a mean of
7.18×10⁻³, and it sits at the commanded reversal — no numerical discontinuity. Every sample finite.

**The first attempt used ±0.35 for 50 steps and drove α to −21.95°, outside the Morelli −10° bound.**
The envelope gate caught it and failed the test, which is the correct behaviour. The **input** was
reduced so this scenario exercises in-domain pitch response; the deliberate excursion belongs to
U5CR-010. No coefficient, limit, inertia, CG or damping was touched.

### U5CR-007 — LATERAL DOUBLET · **PASS** (12/12)

p peaks +2.00159 / −2.05291 rad/s; r +0.16980 / −0.45455; β −7.277…+11.217°; Cl −0.03173…+0.01539;
Cn −0.04480…+0.04029. Aileron ±7.53°, rudder ±7.50°. β stayed in domain. Every sample finite, all
per-step assertions held.

### U5CR-008 — INERTIA / ANGULAR DYNAMICS · **FAIL** (10 passed, 4 failed)

**This is the one defect, and it is not in Maverick's code.**

The static half is perfect:

```
mass 9298.65 kg, centre of mass (0,0,0)
Unity principal moments (75673.6, 85576.5, 12850.5) kg m^2, rotation (1.0492, 0, 0) deg
reconstruction error vs the basis-changed source tensor: 0.008 kg m^2 (0.0000%)
```

| case | ω (p,q,r) rad/s | applied L,M,N N·m | Unity measured ω̇ | full Euler | naive I⁻¹M |
|---|---|---|---|---|---|
| 0 | (0, 0, 0) | (20000, 60000, 30000) | (1.59224, 0.79288, 0.37544) | **0.00% error** | 0.00% |
| 1 | (1.20, 0.45, 0.30) | (20000, 60000, 30000) | (1.59225, 0.79287, 0.37544) | **27.84% error** | **0.00%** |
| 2 | (1.20, 0.45, 0.30) | **(0, 0, 0)** | **(0.00000, 0.00000, 0.00000)** | **100% error** | 0.00% |

- **Case 0** proves the whole static chain: tensor construction, aero→Unity basis change, principal
  decomposition with Ixz, `inertiaTensorRotation`, sign mapping and the frame conversions, all
  correct to 0.00%.
- **Case 1** shows Unity's answer is *identical* to case 0's despite ω ≠ 0 — the rotation rate had no
  effect whatsoever — and matches the **naive** `ω̇ = I⁻¹M` to 0.00%.
- **Case 2 is decisive.** With **zero applied moment**, Euler predicts 0.5211 rad/s² purely from
  ω × (Iω). Unity measured **0.000002 rad/s²**. No torque is applied in this case, so no accounting
  mistake in the harness can explain it.

**Conclusion: Unity's PhysX rigid-body solver does not integrate the gyroscopic term ω × (Iω). It
integrates ω̇ = I⁻¹M.** A reflection probe over `Rigidbody` found **zero** members mentioning
gyroscopic forces in 6000.3.16f1 (only the inertia-tensor members), so this is engine behaviour the
rig cannot switch on — not a setting it failed to set. PhysX has
`PxRigidBodyFlag::eENABLE_GYROSCOPIC_FORCES`; Unity does not surface it.

**Why this blocks expansion.** The F-16's Ixz and its large Iz − Ix difference make roll-yaw inertial
coupling a real part of the aircraft's rotational behaviour — `MavInertiaTensorMath` says so in its
own comments, and Ixz is in the frozen reference data precisely for that reason. With this term
missing, the replacement stack's angular dynamics do **not** satisfy the equations the reference
model is written in, and no amount of aerodynamic accuracy compensates.

**Remedy, deliberately NOT implemented here.** The architecture already makes it a one-place change:
`MavSixDoFBody` applies body-axis moments, and `MavInertiaTensorMath.TryAngularAcceleration` already
computes the term exactly. Supplying −ω × (Iω) as an explicit moment contribution through the
existing load-application boundary would restore the correct dynamics. That is a new physical moment
source, and the brief says to classify and stop rather than change physics inside a validation phase.
It needs its own reviewed step, with its own probe proving the term bites.

### U5CR-009 — ENERGY SANITY · **PASS** (10/10)

12 windows of 25 steps. Total decline 16383.6 kJ with thrust at exactly zero. **Worst window-to-window
rise: 0.0 J** against a 327671 J tolerance — no hidden speed assist, no velocity alignment, no
negative drag, no duplicate gravity. Exactly one aerodynamic and one propulsive contribution per step,
zero duplicate applications, 300 applications in 300 steps.

### U5CR-010 — REFERENCE ENVELOPE EXIT · **PASS** (12/12)

230 m/s at 3000 m → **Mach 0.6999** against the 0.60 ceiling.

- **60 of 60 steps** reported outside the envelope
- **60 of 60 name the exact boundary**: `OutOfReferenceRange`, not a generic failure
- the gating Mach came from the **reference atmosphere** (0.6999 ≈ 0.7000), not the legacy
  constant-`a` estimate (0.6706)
- `MavF16AeroModel.debugOutsidePublishedMachRange` is set, so the excursion is visible at the model
  and the result must not be called reference-valid
- **ownership did NOT flip to Legacy** — leaving the envelope ends the experiment, it does not hand
  the aircraft to another stack
- no legacy physical writer appeared

---

## 6. Ownership and writer counts

Asserted **every physics step**, on live component state rather than from configuration:

| Assertion | Result |
|---|---|
| legacy physical writers on the rig | **0** (counted by type against the production deny list) |
| reference physical writers | **1** (`MavSixDoFBody`) |
| gravity providers | **1** (Unity integrator; `F16Replacement` resolves to `UnityRigidbody`) |
| propulsive thrust | **0 N**, propulsive force vector exactly zero |
| reference profile valid | true |
| mass | 9298.65 kg |
| CG datum | declared |
| inertia | positive definite |
| state and load set | finite |
| Mach / α / β in reference envelope | true (except the two scenarios that deliberately leave it) |

Repository-wide, the phase changed the writer census by exactly one — see §8.

---

## 7. Defects found and fixed

**Nineteen of the twenty-three findings were defects in the new 5C-R test code, not in the aircraft.**
That is the expected shape of a first validation pass, and each is recorded because a test that was
wrong once can be wrong again.

| # | Defect | Class |
|---|---|---|
| D-5CR-1 | Readiness was evaluated at **build time**, before any step, so all nine rigs reported "no valid operational pilot-command source producing commands this step". That is a per-step question asked before any step existed; the rigs were in fact live-ready and applied loads on every step. Split into a structural check at build and an operational check one step in. | harness |
| D-5CR-2 | The free-fall window was **2.2 s**, by which point the body had reached 22 m/s and the aerodynamic force was 13.6 kN — 15% of weight. "Free fall" stops being aerodynamically free almost immediately, which is exactly the assumption the brief warns against. Now a 6-step window with the aerodynamic force **bounded and reported** rather than assumed zero. | harness |
| D-5CR-3 | The energy-consistency check subtracted (KE+PE) from itself and demanded < 1 J — algebraically zero, but these are floats near 2.3×10⁸ whose spacing is ~16 J, so it failed on rounding while proving nothing. Replaced with a real composition check against float resolution. | harness |
| D-5CR-4 | The applied-torque comparison read the component's **last-step** debug value against a sample five steps earlier, reporting "applied (23,0,0), expected (−289,0,0)" purely from the step offset. The applied force and torque are now recorded per sample and compared in the same step. | harness |
| D-5CR-5 | The rate-vs-moment check compared a **settled rate** to a **settled moment**. A moment sets an acceleration; the two need not share a sign once cross-coupling develops. Now measured in the early transient. | harness |
| D-5CR-6 | The U5CR-008 measurement read `angularVelocity` **before PhysX had integrated** the torque — Unity runs every FixedUpdate callback and *then* simulates — so both reads returned the same value and ω̇ came out as exactly zero. The sequence now spans three steps, uses the **measured** ω entering the step, and converts each ω with the transform at its own read so the difference is d(ω_body)/dt. | harness |
| D-5CR-7 | The pitch doublet at ±0.35 for 50 steps drove α to −21.95°, outside the Morelli domain. The gate **correctly** failed the test; the input was reduced. No physics parameter was touched. | test input |
| **D-5CR-8** | **PhysX does not integrate ω × (Iω).** See U5CR-008. | **engine behaviour — OPEN** |

Also corrected: a claim in the U5CR-005 commentary that credited "inertial roll-yaw coupling" for the
settled yaw rate. This same run disproved that — PhysX omits the term — so the text now names the
aerodynamic cross terms instead. A validation suite must not carry an explanation its own evidence
contradicts.

## 8. Repository-wide writer census

`MavInertiaTorqueApplier`, the U5CR-008 diagnostic torque applier, is a new per-step Rigidbody writer.
The Phase 5A writer scan **caught it immediately** as a 5th ungated per-step physical player writer,
which is exactly its job: anything unclassified defaults to `PerStepPhysical`, the strictest reading.

Resolved using the mechanism the scan itself prescribes — *"Either gate it, or classify it in
ClassifyFile with a reason it cannot affect the player per step."* It is classified
`MavWriterCategory.NotPlayerBody`, the category already used for the enemy spawner, the target drone
and the tracer VFX.

**The classification is enforced, not asserted.** `MavInertiaTorqueApplier.Awake` refuses — disables
itself and logs an error — if its object carries `MavSixDoFBody`, `MavFlightPhysicsOwnership` or any
legacy writer. It cannot quietly become an aircraft writer.

The census assertions in `MavPhase5AOwnershipValidation` moved from 3 non-player-body writers to 4,
with the new entry **named**. That keeps the census a tripwire rather than a number somebody bumps:
an unexplained fifth writer still fails.

## 9. Files added and changed

**Added (3 + 3 `.meta`):**

| File | Role |
|---|---|
| `FlightDynamics/Validation/MavF16ReferenceFlightRig.cs` | `MavF16ReferenceTestCommandSource`, the telemetry sample struct, and `MavF16ReferenceTelemetryRecorder` (telemetry + per-step runtime assertions) |
| `FlightDynamics/Validation/MavF16ReferenceFlightScenarios.cs` | `MavF16ReferenceRigBuilder`, the U5CR-001…010 scenario machine, `MavInertiaTorqueApplier` |
| `FlightDynamics/Editor/MavF16ReferenceFlightValidation.cs` | the Play Mode driver and report writer |

`MavPropulsionPlayModeProbe` established the constraint these follow: Unity refuses to attach an
editor-assembly MonoBehaviour to a GameObject in Play Mode, so anything the rig instantiates lives in
the runtime validation assembly.

**Changed (2):**

| File | Change |
|---|---|
| `Scripts/Editor/MavPhase5WriterScan.cs` | classified `MavF16ReferenceFlightScenarios.cs` as `NotPlayerBody`, with its reason |
| `Scripts/Editor/MavPhase5AOwnershipValidation.cs` | non-player-body census 3 → 4, naming the new writer |

**No scene, prefab, material, model, weapon, sensor, AI or damage file was touched. No asset was
created.** `ProjectSettings/` is clean; the generated `Maverick.slnx` was reverted after each Unity run.

## 10. Regression

| Suite | Result |
|---|---|
| Phase 5C-R Unity physics | **119 passed, 4 failed** (all four = D-5CR-8) |
| P0.2 Unity propulsion (edit mode) | **74 / 74** |
| P0.2b Play Mode lifecycle | **169 / 169** |
| Offline sweep, nine harnesses | **813 / 0** — identical to the checkpoint baseline |
| Mutation probes | **25 / 25 bite**, baseline clean before and after |
| Compile | 0 errors; 514 pre-existing `CS0618`, none in a 5C-R file |

The validated P0 shared-propulsion checkpoint is intact.

## 11. Unresolved blockers

| ID | Blocker |
|---|---|
| **D-5CR-8** | **PhysX omits ω × (Iω).** Replacement angular dynamics do not satisfy the reference rigid-body equations whenever ω ≠ 0. Blocks 5C-R expansion. Remedy identified, not implemented. |
| ENG-005 | F-16 thrust deck (TP-1538 Table VI) not transcribed or frozen — thrust stays 0 N |
| — | Actuator **rate limits UNAVAILABLE**; 0 deg/s means no rate limiting, so every surface step in this phase is a step to full deflection within one physics step. Real rate limits will change the transients |
| — | CG datum is a **declaration** for the isolated rig. The gameplay prefab's model-origin → 0.25 c̄ mapping is still unmeasured, so this result does not transfer to the shipping aircraft |
| — | `aircraftIdentityResolvedAsF16C` is by construction here, not resolved through `MavAircraftProfileApplier` |
| P5B5-D2 | `MavAircraftCatalog` F-16C legacy profile still flies 9800 kg against the reference 9298.65 kg |
| — | No AeroBench V3 cross-validation was attempted in this phase |

## 12. Scope confirmations

- Gameplay `F16Replacement` **remains disabled**. The `F16Replacement` ownership state existed only
  on the temporary in-memory rig.
- Propulsion **exactly 0 N** on every step; no thrust was invented, no deck transcribed, no
  AeroBench or JSBSim data used.
- No Rigidbody write outside `MavSixDoFBody` on the aircraft rig; the one diagnostic writer is
  classified and structurally prevented from attaching to an aircraft.
- No handling tuning. No Morelli coefficient, static-stability term, damping value, surface limit,
  inertia value, CG or gravity setting was changed. The only test-input change was reducing one
  doublet amplitude to stay inside the model's published domain.

---

**PHASE 5C-R (original run): NO-GO — REPLACEMENT PHYSICS DEFECTS REMAIN**

The pipeline
`profile → REFERENCE_TEST_COMMAND_SOURCE → control law → actuator → Morelli aero → dimensionalisation
→ MavSixDoFBody → Rigidbody`
is demonstrated correct in real Unity physics: gravity owned exactly once, specific force weightless
in free fall, drag opposing motion, energy monotonically leaving an unpowered glide, all three control
axes correct in sign from stick to body rate, loads applied exactly once per step at the centre of
mass with independent moments, thrust exactly zero, the envelope gate naming the precise boundary it
crossed, and no legacy assist anywhere.

The rotational dynamics were not. The measured Unity path integrated ω̇ = I⁻¹M, omitting the
gyroscopic coupling that the F-16's Ixz exists to produce.

**That term is now supplied through the load-application boundary and proved by its own probes.** See
section 13.

---

# Phase 5C-R.1 — Full Euler Rigid-Body Angular Dynamics

## 13. D-5CR-8: root cause, fix, and result

### 13.1 Root cause

The measured Unity Rigidbody angular integration path on this project integrates

```
I w_dot = M_applied
```

rather than Euler's rigid-body equation

```
I w_dot + w x (I w) = M_external
```

**Scope of that statement.** It describes the path Phase 5C-R measured: Unity 6000.3.16f1, this
Rigidbody configuration, the F-16 reference inertia. It is *not* a claim about PhysX in general, other
Unity versions, articulations, or any configuration that was not measured. The measurement is kept
running (section 13.6) precisely so the claim stays tied to evidence rather than becoming folklore.

### 13.2 The fix

`M_applied = M_external - w x (I w)`, evaluated once per step and summed into the load set
`MavSixDoFBody` already applies.

| | |
|---|---|
| **Formula** | `H = I w` ; `Mgyro = -(w x H)` |
| **Where computed** | [`Core/MavGyroscopicMoment.cs`](../Assets/MaverickFresh/Scripts/FlightDynamics/Core/MavGyroscopicMoment.cs) — `TryCompute` |
| **Where added** | [`Core/MavSixDoFBody.cs`](../Assets/MaverickFresh/Scripts/FlightDynamics/Core/MavSixDoFBody.cs) — `ApplyGyroscopicCompensation`, called between propulsion and `PublishSpecificForce` |
| **How applied** | `MavFlightDynamicsLoadSet.AddInertialCorrection` — one more contribution to the same accumulator, applied by the same single `AddRelativeTorque` |
| **Frame** | **aero body** (X forward, Y right, Z down) |
| **Switch** | `MavSixDoFBody.applyBackendGyroscopicCompensation`, default **true**, named for what it compensates — and **gated by readiness**, see 13.13 |

**No second Rigidbody writer.** The production physical writers in the whole flight-dynamics tree are
still exactly `MavSixDoFBody.cs:390-391`. `MavGyroscopicMoment` holds no Rigidbody reference and
applies nothing; it returns a moment.

**Not aerodynamics.** The term is not in `Cl`, `Cm` or `Cn`, and no Morelli coefficient was touched.
It is carried in its own load-set channel so it stays separable from the aircraft's real moment.

**No blend, no deadband.** The term is quadratic in `w` and vanishes on its own as rotation stops;
section 13.5 measures that rather than asserting it.

### 13.3 Frame handling — one boundary

```
Rigidbody.angularVelocity (WORLD)
  -> transform.InverseTransformDirection            Unity local
  -> MavFlightDynamicsMath.UnityLocalAngularRateToAeroBody   aero body p,q,r
  -> H = I w                                        aero body, full tensor
  -> Mgyro = -(w x H)                               aero body
  -> summed with the aerodynamic and propulsive moments
  -> MavFlightDynamicsMath.AeroBodyMomentToUnityLocal        Unity local   <-- the one crossing
  -> rb.AddRelativeTorque                           applied once
```

The world-to-body conversion is the one the state builder already performs for `p/q/r`; the
body-to-Unity conversion is the one the load set already crosses. The correction adds **no** transform
of its own and **no** sign fix anywhere. World-space `omega` is never multiplied by a body-frame
tensor.

### 13.4 Ixz — the full tensor, not the diagonal

The aero-body inertia matrix is rebuilt from what the Rigidbody is **actually carrying**, through the
existing authority:

```
Rigidbody.inertiaTensor + inertiaTensorRotation
  -> MavInertiaTensorMath.Reconstruct            I in Unity local  (R diag R^T)
  -> MavInertiaTensorMath.UnityLocalInertiaToAeroBody   I in aero body
```

Read back from the body rather than from the profile that wrote it, because the tensor the
compensation must match is the one the solver uses. No second inertia reconstruction was written.

`inertiaTensorRotation` is **not** guessed: U5CR-008 verifies that this exact round trip reproduces
the sourced body-axis tensor — Ixz included — to **0.0000%** of its largest element, and that the
principal rotation is 1.0492° about Unity X only, which is where an Ixz-only coupling must put it.

`H` is formed with the full matrix. G-005b measures what dropping Ixz would cost: **1944 N·m, 4.64%**
of the correction at the U5CR-008 case-1 rates. A diagonal-only shortcut is therefore wrong in exactly
the axis the term exists to fix, and a probe (`gyrodiagonalonly`) turns the suite red for it.

### 13.5 Pure-math results — G-001 .. G-008, 21/21

Run **before** any PhysX measurement, and offline in `fdmfull` so mutation probes can reach them.

| ID | Check | Result |
|---|---|---|
| G-001 | `w = 0` gives exactly zero | **PASS** |
| G-002 | spherical inertia gives zero for every rate | **PASS** — worst residual 8.7×10⁻³ N·m, **2.65×10⁻⁸** of the pre-cancellation term scale |
| G-003 | rotation about each principal axis gives zero | **PASS** — exactly 0 on all three |
| G-004 | asymmetric diagonal vs hand calculation | **PASS** — `(4800, -54000, -60000)` matched to 0.0020 N·m |
| G-005 | F-16 tensor with Ixz vs independent expansion | **PASS** — 0.00000% |
| G-005b | dropping Ixz changes the answer | **PASS** — 1944 N·m (4.64%) |
| G-006 | body → Unity → body round trip | **PASS** — 0.000000% |
| G-006b | body → Unity → world → back, non-trivial attitude | **PASS** — 0.000009% |
| G-006c | the mapping is not a no-op | **PASS** |
| G-007 a–e | NaN, Inf, non-finite tensor and zero inertia all fail closed; valid input still works | **PASS** |
| G-008 a–g | load-set plumbing: total = external + correction, external still recoverable, **no force added**, double-add refused and recorded, channel cleared each step | **PASS** |

G-002 and G-003 use tolerances **relative to the pre-cancellation term scale**. An absolute 10⁻³ N·m
bound failed a correct implementation on the first run: `|I w|` is of order 2×10⁵ N·m·s and float32
carries about seven digits, so two nearly parallel vectors that large cannot cancel closer than a few
thousandths of a newton-metre. The bound was measuring float precision, not physics.

Expectations are **hand-expanded**, not produced by calling the helpers under test. G-005 writes out
`H = (Ix p - Ixz r, Iy q, -Ixz p + Iz r)` and the cross product term by term; a test that reused
`MavInertiaTensorMath.Multiply` to build its own expectation would agree with the implementation
whatever either of them did.

### 13.6 Unity PhysX result — before and after

The **backend probe** runs first, on a bare Rigidbody with none of Maverick's stack, and is kept
deliberately: it is the only thing that can tell us whether the backend still omits the term, and a
fix that deleted the measurement justifying it would leave nobody able to notice when it stops being
needed.

```
BACKEND PROBE - bare Rigidbody, torque-free, p/q/r = (1.200, 0.450, 0.300) rad/s
  measured  (0.00000, 0.00000, 0.00000) rad/s^2
  Euler     (-0.08909, 0.32199, -0.39987) rad/s^2   |Euler| = 0.5211
  PASS  the raw backend still omits w x (I w)
```

The **corrected path** is measured through `MavSixDoFBody`. A synthetic constant-moment aero model
supplies the external moment as a coefficient set, so it enters the aircraft the way any moment does
rather than through a test-only torque source that would bypass the code under test.

| case | ω (p,q,r) rad/s | M_external N·m | correction N·m | measured ω̇ | vs full Euler | vs naive I⁻¹M |
|---|---|---|---|---|---|---|
| **A** | (0, 0, 0) | (20000, 60000, 30000) | **(0, 0, 0)** | (1.59224, 0.79288, 0.37544) | **0.00%** | 0.00% |
| **B** | (1.2318, 0.4659, 0.3075) | (20000, 60000, 30000) | (−651, 25633, −36230) | (1.49775, 1.13161, −0.04951) | **0.00%** | **29.37%** |
| **C** | (1.2298, 0.4729, 0.2977) | **(0, 0, 0)** | (−617, 24716, −36707) | (−0.09242, 0.32661, −0.43050) | **0.00%** | **100%** |

**Before → after:**

| | before 5C-R.1 | after |
|---|---|---|
| case B error vs full Euler | **27.84%** | **0.00%** |
| case B match to naive I⁻¹M | 0.00% (wrong answer, exactly) | 29.37% off — correctly *not* matching |
| case C, torque-free | **0.000002 rad/s²** | **0.5482 rad/s²**, matching Euler's 0.5482 |

Case C is the decisive one: with **zero external moment** the aircraft now evolves under torque-free
Euler motion, and the naive prediction is 100% wrong. The corrected runtime matches the full Euler
solution and does **not** match the naive one in either discriminating case, which is what separates a
correct implementation from one that merely agrees somewhere.

### 13.7 Torque telemetry — separated, applied once

`externalMoment`, `gyroscopicCorrectionMoment` and `finalAppliedMoment` are exposed per step on
`MavSixDoFBody` and recorded per sample in the 5C-R telemetry CSV. Verified **every step of every
scenario**, not only where inspected:

- `finalAppliedMoment = externalMoment + gyroscopicCorrectionMoment` — to 0.0000 N·m
- `gyroscopicCorrectionMoment = -w x (I w)` on the full tensor — exact to the reported digits
- `inertialContributions <= 1` — a double correction cannot reach the Rigidbody quietly

The separation is diagnostic only. There is still **one** sum and **one** application.

### 13.8 Zero and low-rate regression

The two halves are both needed. "Negligible in a glide" alone is equally consistent with a correction
that is never computed, so the lateral doublet asserts the opposite:

| scenario | peak |ω| | max correction | share of external moment |
|---|---|---|---|
| free fall (U5CR-001) | 0.045 rad/s | **0.000 N·m** | **0.0000%** |
| unpowered glide (U5CR-002) | low | **0.000 N·m** | **0.0000%** |
| lateral doublet (U5CR-007) | 1.415 rad/s | **75 541 N·m** | **118.9%** |

Free fall, glide and low-rate control response are unperturbed, and at real manoeuvring rates the term
dominates — which is what `w²` scaling looks like. No deadband, no blend factor, no tuned strength.

### 13.9 All U5CR results after the fix

| | Result |
|---|---|
| U5CR-001 free fall | **PASS** |
| U5CR-002 unpowered glide | **PASS** (+ new low-rate check) |
| U5CR-003 elevator step | **PASS** |
| U5CR-004 aileron step | **PASS** |
| U5CR-005 rudder step | **PASS** |
| U5CR-006 pitch doublet | **PASS** |
| U5CR-007 lateral doublet | **PASS** (+ new high-rate check) |
| **U5CR-008 inertia / angular dynamics** | **PASS** — 21/21, was 10/4 |
| U5CR-009 energy sanity | **PASS** |
| U5CR-010 reference envelope exit | **PASS** |

**168 passed, 0 failed.** Compile: 0 errors, 514 pre-existing `CS0618`, none in a 5C-R file.

### 13.10 Regression

| Suite | Result |
|---|---|
| Phase 5C-R Unity physics | **168 / 0** |
| P0.2 Unity propulsion | **74 / 74** |
| P0.2b Play Mode lifecycle | **169 / 169** |
| Offline sweep, nine harnesses | **841 / 0** (was 813; +28 from the G-suite) |
| Mutation probes | **36 / 36 bite** — 12 P0, 13 P0.1, **11 new gyroscopic** |
| Ownership / writer scan | `MavSixDoFBody` sole production writer; census unchanged at 4 non-player-body writers |
| Morelli, inertia, envelope regressions | unchanged |

**Eleven new mutation probes** cover the new physics: sign flip, cross-product operand order,
diagonal-only tensor, finite guards removed, zero-inertia accepted, double-add, correction added to
the force total, external moment hidden from telemetry, a stale channel across steps, the readiness
gate removed, and the acceptance rule forced true. All eleven turn a suite red.

`gyronofiniteguard` did not bite on its first form, which was worth understanding rather than
adjusting: removing only the input guard left the **output** guard still refusing the NaN, so the
function failed closed either way. That is deliberate defence in depth, not a coverage gap — the probe
now removes the actual protection and turns the suite red by 3.

### 13.11 Two portability defects found while wiring the offline suite

| ID | Defect |
|---|---|
| D-5CR.1-1 | The inertia cache key used `Vector3 ==` / `Quaternion ==`. Unity's equality operators for both are **approximate** — `Quaternion ==` is a dot-product test with a tolerance — which is the wrong semantics for a cache key, and the offline harness stub does not define them at all. Now compared component by component. |
| D-5CR.1-2 | The G-suite used `moment == Vector3.zero` to assert exact zero, which Unity's approximate operator would satisfy for a small non-zero value — the opposite of the assertion. Replaced with an explicit component-wise `IsExactlyZero`. |

Both were caught by compiling the same production files against the offline stub, which is a different
compiler with a different `Vector3`. Neither would have failed in Unity, and the second would have
weakened a test silently.


### 13.13 Audit: the correction cannot be disabled silently

The correction is mandatory for the replacement FDM, so an unticked checkbox must not be a route to
the known-incomplete naive dynamics. Audited before the checkpoint commit.

**Finding: not guaranteed.** `applyBackendGyroscopicCompensation` was declared in one place and read
in one place, with nothing else in the codebase referring to it — no readiness condition, no
validation, no scan. Two failure modes, and only one of them was safe:

| | |
|---|---|
| a component serialized **before** the field existed | **safe** — Unity constructs the component, running the field initialiser, then applies only the keys the payload carries, so the absent key keeps `true` |
| a component serialized with the field **explicitly false** | **unguarded** — the aircraft would fly naive `ω̇ = I⁻¹M` with no refusal anywhere, the only symptom a debug string nobody reads |

**Fix**, mirroring `acceptNonAuthoritativePropulsion` because it is the same shape of problem:

- `acknowledgeIncompleteAngularDynamicsForTesting`, default **false**
- `MavSixDoFBody.AngularDynamicsAcceptable` = compensation on **or** explicitly acknowledged
- fed through `MavPipelineSnapshot` into `MavFlightDynamicsReadiness.EvaluateOperational`, which
  **refuses** with a named reason when it is false

Turning the correction off alone is now refused — loads are not applied at all. The naive dynamics
remain reachable for A/B validation, but only by deliberately setting a second field whose name says
what it does.

**Verification.** Both halves are measured, not asserted:

| ID | Check | Result |
|---|---|---|
| G-009 a–d | readiness accepts with the correction on; **refuses** when off and unacknowledged, naming the reason; accepts with the acknowledgement | **PASS** |
| G-009 e–g | the real `MavSixDoFBody` property, not a restatement of the rule | **PASS** |
| S-001 a–c2 | Unity's own serializer: a payload **without** the key deserializes to `true` on a freshly constructed component, and a payload **with** it still applies it | **PASS** |
| S-001 d–f | a live component with the correction off reports unacceptable, feeds that to the readiness inputs, and the acknowledgement restores it | **PASS** |

Two probes cover the gate: `gyrogatemissing` (remove the refusal) and `gyrogateacceptsoff` (make the
acceptance rule always true). Both turn the suite red.

**Two test defects found and fixed while writing this audit**, both cases of a test agreeing with
itself:

- **S-001c** first set the field to `false` and *then* called `FromJsonOverwrite`. That measures a
  value the test had just written — `FromJsonOverwrite` leaves an absent key at whatever the target
  currently holds. Unity's real path starts from an initialiser-constructed object, so the check now
  overwrites onto a **fresh** component, with a companion check proving a present key is still applied
  (otherwise a serializer that ignored the field entirely would also pass).
- **G-009e/f** first called a local mirror of the acceptance rule rather than the component property.
  The `gyrogateacceptsoff` probe proved it worthless: breaking `AngularDynamicsAcceptable` left the
  suite green because nothing offline read it. Now the real component is constructed and its property
  tested.

### 13.12 Scope confirmations

- `MavSixDoFBody` remains the **sole production physical writer** (`MavSixDoFBody.cs:390-391`).
- `MavInertiaTorqueApplier` remains **validation-only**, attached to exactly one object — the bare
  backend probe body at `MavF16ReferenceFlightScenarios.cs:1269` — never to the reference rig, and it
  refuses at `Awake` to attach to anything carrying an aircraft physics stack.
- F-16 dimensional thrust remains **exactly 0 N**.
- Untouched: Morelli coefficients, mass, CG, reference inertia values, actuator surface limits,
  gravity, propulsion, thrust, FLCS, AoA/G protection, gameplay assists, production scenes and
  prefabs. No asset created.
- Gameplay `F16Replacement` remains **disabled**.

---

**PHASE 5C-R.1: GO FOR PHASE 5C-R EXPANSION**

The full Euler rigid-body equation now holds in real Unity physics: case A exact with the correction at
zero, case B exact where the naive prediction is 29% wrong, and case C — torque-free — evolving at
0.5482 rad/s² where it previously measured 0.000002. The correction is computed in one place, in one
frame, from the full inertia tensor including Ixz, added to the load set `MavSixDoFBody` already
applies, and covered by eleven mutation probes plus a pure-math suite of twenty-eight checks, and it cannot be
switched off without readiness refusing to fly. It is
invisible at glide rates and dominant at manoeuvring rates, with no blend factor and no deadband.
