# F-15 Full Implementation — Claude Handoff V0.1

Status: **ACTIVE HANDOFF / CONTINUE IMPLEMENTATION**

Date: 2026-09-22

Repository: `stickleetoto/Maverick`

Recommended continuation base:

```text
branch: sol/f15-r2-lateral
head:   5e262c2dd569122814261e7169a19379bbe2401c
```

Recommended Claude continuation branch:

```text
claude/f15-full-implementation
```

Create it from the exact handoff head above unless the repository has moved and the user explicitly
chooses a newer reviewed base.

---

# 0. Mission

Continue the Maverick F-15 work from the current source-honest R1/R2 foundation and carry the
F-15 implementation as far as the available public evidence defensibly permits.

The intended architecture is:

```text
Player / AI command
        ↓
F-15 instructor / command source
        ↓
F-15 control law / FCS
        ↓
F-15 actuator + surface-state owner
        ↓
F-15 aerodynamic model + twin F100 propulsion
        ↓
shared MavFlightDynamicsLoadSet
        ↓
MavSixDoFBody
        ↓
Unity Rigidbody / world state
```

The F-16 is an **architecture reference**, not a source of F-15 aircraft numbers.

Reuse the common architecture.
Do not copy F-16 aerodynamic coefficients, FLCS gains, control limits, engine dynamics, or thrust
data into the F-15.

---

# 1. Branch / PR stack at handoff

The current work is intentionally stacked.

## PR #24

`sol/f15-r1-profile -> main`

Title:

`F15-R1: add NASA 836 source-honest profile skeleton`

Head at handoff:

`e2b4b362fb93ec7b658e7454c6831a9733336e6e`

## PR #25

`sol/f15-r2-baumann-static -> sol/f15-r1-profile`

Title:

`F15-R2: add bounded Baumann longitudinal research aero`

Head at handoff:

`bf18f9481add5d6a5420ae44787ba3ef1447df83`

## PR #26

`sol/f15-r2-lateral -> sol/f15-r2-baumann-static`

Title:

`F15-R2: transcribe Baumann lateral-directional aero`

Head at handoff:

`5e262c2dd569122814261e7169a19379bbe2401c`

Do not merge the stack out of order.

Do not rebase or rewrite these branches merely to make history prettier.

For continuation, branch from the latest reviewed head and keep later work reviewable.

---

# 2. Frozen target identity

The first full-scale target remains:

`NASA_F15B_836_SN74_0141_PRE_QUIET_SPIKE_BASELINE_F100_PW_100`

Meaning:

- NASA F-15B tail number 836;
- USAF serial 74-0141;
- pre-Quiet-Spike baseline configuration;
- two Pratt & Whitney F100-PW-100 engines.

This identity is the exact-target authority for the live/reference F-15 path.

Do not silently replace it with:

- an F-15A generic configuration;
- an F-15C;
- an F-15D;
- an F-15E;
- the 3/8-scale NASA RPV;
- the AFIT/Baumann research model.

Those may be useful as cross-validation or development references but must remain separately tagged.

---

# 3. Current implementation

Relevant code currently exists under:

`Assets/MaverickFresh/Scripts/FlightDynamics/F15/`

## R1 physical/profile layer

Implemented:

- `MavF15ReferenceData.cs`
- `MavF15MassReference.cs`
- `MavF15FlightDynamicsProfile.cs`
- `MavF15ControlActuator.cs`
- `MavF15PropulsionSkeleton.cs`

Current R1 behavior is deliberately fail-closed.

Known exact target data is populated where defensible.
Missing exact-target data is not replaced with plausible generic F-15 numbers.

In particular, exact-target coefficient-reference `S` and `cbar` remain unresolved in the NASA
836 profile, so the exact target profile is intentionally not live-ready.

## R2 research aerodynamic layer

Implemented:

- `MavF15BaumannMach06Reference.cs`
- `MavF15BaumannMach06Longitudinal.cs`
- `MavF15BaumannMach06LateralDirectional.cs`
- `MavF15BaumannSurfaceState.cs`
- `MavF15AeroModel.cs`

Available modes:

```text
ExactNasa836Unavailable
BaumannMach06LongitudinalResearch
BaumannMach06SixAxisResearch
```

The Baumann modes are:

- CROSS-VALIDATION / RESEARCH only;
- fixed to Mach 0.6 / 20,000 ft;
- not exact NASA 836 aerodynamic authority;
- explicitly opt-in;
- fail-closed outside the fixed source condition.

The six-axis research path now produces:

```text
CX CY CZ Cl Cm Cn
```

from the transcribed AFIT/Baumann coefficient equations.

---

# 4. CRITICAL: audit the existing coefficient transcription before trusting it

The current R2 polynomial code was transcribed from scanned/public AFIT material.

Treat the numeric transcription as a **candidate implementation**, not unquestioned truth.

Before promoting, tuning, validating, or expanding it:

1. reopen the source PDF;
2. compare every coefficient and every sign in the longitudinal routine;
3. compare every coefficient and every sign in the lateral-directional routine;
4. verify every piecewise threshold;
5. verify all uses of degrees versus radians;
6. verify the beta sign multipliers;
7. verify the 20-30 deg drag transition;
8. verify the high-alpha compact-support asymmetric terms;
9. verify all F-15B canopy increments;
10. record corrections with source page/equation provenance.

Do not "fix" a strange source number because it looks aerodynamically unusual.
First determine whether the source, OCR, or transcription is responsible.

A sign error has already been found once during manual review, so this audit is mandatory.

---

# 5. Source authority and source separation

## Exact NASA 836 authority

Primary exact-target material already used in project research includes:

- NASA/TM-2012-215978
- NASA/TM-2009-214651
- NASA/TM-2008-214634 / AIAA-2007-6638 lineage

These are preferred when a value is specific to NASA F-15B 836.

The exact NASA 836 baseline work includes flight-derived updates/validation of the baseline
aerodynamic model. Preserve that distinction from the AFIT research model.

## AFIT / Baumann research aerodynamic lineage

Primary working sources:

- Michael T. Davison,
  *An Examination of Wing Rock for the F-15*,
  AFIT/GAE/ENY/92M-01.
- Robert C. Nolan II,
  *Wing Rock Prediction Method for a High Performance Fighter Aircraft*,
  AFIT/GAE/ENY/92J-02.

Davison Appendix C contains the reproduced coefficient routine.

The routine identifies McAir F-15 baseline-simulator / 1988 F-15 aerobase lineage and uses the
fixed research condition:

```text
Mach:              0.6
pressure altitude: 20,000 ft
S:                 608 ft^2
b:                 42.8 ft
cbar:              15.94 ft
moment ref:        0.2565 cbar
```

These values belong to the research model until exact-target equivalence is proven.

## FCS evidence

Important sources acquired:

- NASA TM-72861, *Precision Controllability of the F-15 Airplane*
- AFIT/Davison and related Beck/Baumann control-system lineage

TM-72861 is especially useful for:

- mechanical control + CAS architecture;
- PRAD;
- RRAD;
- pitch CAS;
- roll CAS;
- yaw CAS;
- ARI;
- control gearing/authority information.

But the test aircraft was F-15 No. 8 / preproduction lineage, later modified toward production
standard.

Therefore its numbers are strong F-15-family/FCS evidence, not automatically exact NASA 836
authority.

## Engine sources

Important F100-PW-100 source set already acquired/researched:

- NASA TP-1373
- NASA TM-X-3261
- NASA TP-1034
- NASA CR-144866 for inlet/engine integration context

Keep prototype / engine-build differences explicit.

Do not copy the F-16 Garza/Morelli engine power law into the F100 merely because the shared
`MavEngineRuntime` supports it.

## Highest-value missing original sources

Continue looking for:

1. `MDC A4172 Part II`
   - *F/TF-15 Stability Derivatives, Mass and Inertia Characteristics Flight Test Basis,
     Part II — Aerodynamic Coefficients and Stability and Control Derivatives*
2. `MDC A4172 Part I Supplement 1`
3. `DN-1180.01-238-458 Rev. D`
   - *F-15 Flight Control System Description*
4. AFFTC source reports referenced by TM-72861, especially the flying-qualities/control-system
   reports.

If a missing source is not available, keep that datum unavailable or explicitly research-only.
Do not infer exact values merely to finish the code.

---

# 6. Architecture rules — do not violate

The F-16 Phase-5 ownership architecture remains the model for ownership discipline.

Required invariants:

```text
one physical flight-dynamics owner
one load-application boundary
one actuator owner of actual surface state
aero returns coefficients/loads only
control law never writes Rigidbody forces
instructor never writes trajectory-curving force
propulsion owns thrust
MavSixDoFBody owns final physical application
```

Unity body axes:

```text
+X right
+Y up
+Z forward
```

Aerodynamic body axes:

```text
+X forward
+Y right
+Z down
```

Moment convention:

```text
L = roll-right
M = nose-up
N = nose-right
```

Do not create another parallel SixDoF stack for the F-15.

Do not make `MavAeroBody` multi-surface.

Do not stack new "real" aero effects on top of legacy fake assists.
Use ownership migration / replacement.

---

# 7. F-15 control-surface representation

The F-15 differs from the current common F-16-shaped input contract because the research model needs:

- symmetric stabilator;
- differential stabilator;
- aileron;
- rudder.

Current R2 code intentionally avoids polluting shared FDM Core with an F-15-only field.

`MavF15BaumannSurfaceState` currently adapts the common input and uses the research/source relation:

`DTALD = 0.3 * DAILD`

by default.

This is a temporary F-15 research bridge.

For the real F-15 FCS, create a proper aircraft-specific requested/actual surface-state layer rather
than forcing differential tail into a generic field with misleading semantics.

Preferred structure:

```text
pilot/instructor command
        ↓
MavF15ControlLaw
        ↓
MavF15RequestedSurfaceState
        ↓
MavF15ControlActuator
        ↓
MavF15ActualSurfaceState
        ↓
MavF15AeroModel
```

The common core should consume final coefficients/loads, not know F-15 surface peculiarities.

---

# 8. Remaining implementation roadmap

## Phase A — Source-transcription stabilization

Before adding more behavior:

- audit all current Baumann coefficients against the source PDF;
- add source-page/equation provenance comments or a coefficient map document;
- add deterministic coefficient fixtures for selected source-condition points;
- verify zero-control / beta symmetry behavior where the source expects symmetry;
- verify finite output across the source model's intended AOA domain;
- verify no thrust remains inside the aero load path.

Do not tune coefficients to make the Unity aircraft "feel better."

## Phase B — F-15 FCS boundary

Implement a dedicated F-15 control-law layer.

At minimum model the sourced architecture for:

```text
mechanical pilot command
PRAD
RRAD
pitch CAS
roll CAS
yaw CAS
ARI
limiters/schedules
high-AOA roll-damper washout
```

Keep these distinct:

```text
pilot command
!= response/load/rate demand
!= requested surface state
!= actual actuator position
```

Where the exact NASA 836 schedule is unknown, mark the behavior as F-15-family/research provenance
or leave it disabled.

Do not label a reconstructed/preproduction schedule "NASA 836 exact".

## Phase C — F-15 actuators

Upgrade the current placeholder F-15 actuator layer to own actual:

- symmetric stabilator;
- differential stabilator;
- aileron;
- rudder.

Model, only when sourced:

- hard stops;
- rate limits;
- lags/servo dynamics.

Never teleport requested position directly to final surface position if the selected sourced actuator
model provides finite-rate dynamics.

Do not use conflicting hard-stop numbers without explaining the configuration difference.

## Phase D — F100-PW-100 twin propulsion

Replace the current zero-thrust skeleton only when the source model is defensible.

Required structure:

```text
left throttle  -> left F100 runtime  -> left thrust vector
right throttle -> right F100 runtime -> right thrust vector
                                     ↓
                         force + r x F moment
```

Preserve:

- independent engine runtime state;
- independent throttle channels;
- twin-engine installation identity;
- data provenance;
- source envelope;
- fail-closed behavior when data is unavailable.

Do not fabricate:

- mount coordinates;
- thrust-line offsets;
- thrust deck;
- AB schedule;
- transient spool constants;
- inlet-recovery map.

Use NASA F100 material to build a source-backed engine model.

If source data supports only a subset of Mach/altitude/power conditions, represent that envelope
explicitly.

## Phase E — Exact-target/profile closure

Try to close:

- exact NASA 836 coefficient-reference wing area;
- exact NASA 836 coefficient-reference mean aerodynamic chord;
- exact control hard-stop/sign authority;
- exact actuator behavior;
- exact engine installation/thrust-line geometry.

If exact closure fails, the target profile must remain incomplete.

A separate **research flight profile** may be made live for controlled development if its provenance
is unmistakable and it cannot be confused with the exact NASA 836 target.

## Phase F — Runtime integration

Do not bypass the existing ownership work.

Preferred ownership states should mirror the existing FDM concept:

```text
Legacy
Shadow
F15Replacement
```

or use a generalized aircraft-independent equivalent if the repository already provides one.

Shadow mode:

- replacement F-15 computes;
- live legacy aircraft still owns motion;
- replacement applies zero live Rigidbody loads;
- telemetry compares both paths.

Replacement mode:

- one owner only;
- disable/gate legacy aerodynamic writers;
- disable/gate legacy thrust writers;
- disable/gate fake lift / direct torque / velocity turn assists;
- ensure gravity is applied exactly once.

Do not solve F-15 integration by adding another independent physics owner.

## Phase G — Unity aircraft wiring

The repository currently contains F-15E visual/prefab assets.

The frozen physics target is NASA F-15B 836.

Do not conflate them.

If the F-15E asset is reused as a temporary development visual:

- mark it as a visual proxy;
- do not infer F-15B physics from the mesh;
- do not infer engine mount coordinates from an unverified imported asset;
- do not rename the NASA 836 physical target to F-15E just because the mesh is F-15E.

Eventually create a clean F-15 runtime/configurator path analogous to the F-16 path.

## Phase H — Validation

Add validation in layers.

### Pure math / source fixtures

Validate:

- coefficient equations at selected source points;
- units and deg/rad conversion;
- pHat/qHat/rHat normalization;
- beta sign functions;
- piecewise continuity where intended;
- finite values.

### Architecture validation

Validate:

- aero does not write Rigidbody;
- FCS does not write Rigidbody;
- actuator is the actual-surface owner;
- propulsion is the thrust owner;
- `MavSixDoFBody` is the only live physical application boundary.

### Research model validation

At M=0.6 / 20,000 ft:

- compare coefficient behavior against AFIT source plots/tables where available;
- compare high-AOA qualitative behavior only inside the source model's intended research use;
- do not generalize to other Mach numbers.

### NASA 836 validation

Use NASA 836 flight-test-derived trends such as available `Cm_alpha` and `Cn_beta` evidence as
independent validation targets.

Do not tune the research model until it matches a different configuration and then claim the result
is source-derived.

### Runtime validation

Only claim Unity/PlayMode evidence that was actually run.

If runtime testing is deferred, state exactly:

`NOT RUN`

Never write "validated" based only on code inspection.

---

# 9. Known project-wide blockers / neighboring issues

The F-15 work must respect the project-wide ownership contract already documented for the F-16.

Open issue #7 defines the replacement-FDM ownership direction.

Open issue #12 demonstrates the project's provenance policy for propulsion:
authoritative-looking thrust data is not enough if its source envelope is undeclared.
Do not weaken that policy for the F-15.

Issues #15 and #16 are combat/targeting ownership problems.
They are not reasons to modify F-15 weapons during this handoff.

Weapons, sensors, damage, hit feedback, kill feedback and unrelated combat polish are out of scope
for this F-15 implementation unless the user explicitly changes scope.

---

# 10. Files Claude should read first

Read these before coding:

```text
Docs/FlightDynamics/F15_DEVELOPMENT_ROADMAP_V0.1.md

Docs/Reference/F15_FULL_SCALE_TARGET_FREEZE_V0.1.md
Docs/Reference/F15_FULL_SCALE_SOURCE_CATALOG_V0.1.md
Docs/Reference/F15_FULL_SCALE_SOURCE_COMPATIBILITY_V0.2.md
Docs/Reference/F15_FULL_SCALE_DATA_GAPS_V0.2.md
Docs/Reference/F15_FULL_SCALE_GAP_CLOSURE_REPORT_V0.1.md
Docs/Reference/F15_RPV_TO_FULL_SCALE_TRANSFER_POLICY_V0.1.md

Docs/Reference/F15_R1_IMPLEMENTATION_STATUS_V0.1.md
Docs/Reference/F15_R2_BAUMANN_RESEARCH_AERO_V0.1.md
Docs/Reference/F15_R2_BAUMANN_LATERAL_DIRECTIONAL_V0.1.md

Docs/FlightDynamics/F16_LIVE_FDM_ARCHITECTURE_V0.1.md
Assets/MaverickFresh/Scripts/FlightDynamics/README.md
```

Then read all code in:

```text
Assets/MaverickFresh/Scripts/FlightDynamics/Core/
Assets/MaverickFresh/Scripts/FlightDynamics/F16/
Assets/MaverickFresh/Scripts/FlightDynamics/F15/
Assets/MaverickFresh/Scripts/FlightDynamics/Validation/
```

Before any live-ownership work, inspect the actual current force-writer call graph again.
Do not trust an old list if code has changed.

---

# 11. External/source files Claude may need from the user

Some research PDFs were supplied in the ChatGPT project/conversation and are not necessarily checked
into the Git repository.

If Claude cannot access them directly, ask the user for the PDFs rather than reconstructing missing
numbers from memory.

Important files include:

```text
An Examination of Wing Rock for the F-15
Wing Rock Prediction Method for a High Performance Fighter Aircraft
NASA TM-72861
NASA/TM-2012-215978
NASA/TM-2009-214651
NASA/TM-2008-214634
NASA TP-1373
NASA TM-X-3261
NASA TP-1034
NASA CR-144866
```

Do not rely on OCR text alone when the source scan/image can resolve an ambiguous sign or digit.

---

# 12. Do not do these

Do NOT:

- copy F-16 Morelli coefficients into the F-15;
- copy F-16 FLCS gains into the F-15;
- copy the F-16 engine dynamic law into the F100 and call it sourced;
- guess missing exact NASA 836 data;
- promote AFIT/Baumann M=0.6 data to a full Mach envelope;
- silently treat F-15A/B/D/E configurations as interchangeable;
- silently treat the F-15E visual mesh as the physical authority;
- stack real aero on top of legacy fake assists;
- create a second Rigidbody writer;
- put F-15-specific surface semantics into shared core unless genuinely aircraft-independent;
- merge the current PR stack out of order;
- claim tests passed if they were not run.

---

# 13. Definition of "full implementation"

For this handoff, "full implementation" means the maximum source-honest F-15 system the available
evidence supports, not filling every field at any cost.

The desired end state is:

```text
F-15 identity resolved
        ↓
instructor/pilot command
        ↓
F-15 sourced/research-tagged FCS
        ↓
physical F-15 actuator/surface state
        ↓
F-15 aero provider
+
twin F100 propulsion provider
        ↓
shared dimensional loads
        ↓
MavSixDoFBody
        ↓
single live physical owner
        ↓
telemetry / validation
```

The exact NASA 836 mode may remain partially unavailable if original source authority cannot be
recovered.

That is acceptable.

What is not acceptable is converting an unknown into an invented number merely so the aircraft can
fly.

---

# 14. Recommended first Claude action

Start by auditing the current R2 coefficient transcription against the original Davison Appendix C
PDF.

Produce a small provenance table:

```text
code symbol | source symbol/table | source page | condition | transcription checked
```

Correct any numeric/sign errors first.

Then implement the F-15-specific surface/FCS ownership path before attempting live flight.

After each coherent phase:

- commit it;
- update the F-15 status docs;
- report exactly what became source-backed, what remains research-only, and what is still
  unavailable;
- keep the branch reviewable.

Do not wait for perfect full-envelope source data before improving architecture, but never blur the
provenance boundary.
