# Maverick F-15 Reference Development Roadmap v0.1

Status: **ROADMAP — reference-aircraft development**

Purpose: develop the F-15 as Maverick's second serious reference aircraft while reusing the same six-DoF, atmosphere, ownership, telemetry, profile, and propulsion infrastructure being validated by the F-16.

The first F-15 reference target must be configuration-tagged and must not inherit the old `F15E_SPEC_RESEARCH_AND_GAME_TUNING.md` values as physical truth. That document is gameplay tuning, not a reference flight model.

---

## 1. Architecture invariant

The F-15 does **not** get a separate flight-physics engine.

```text
Aircraft Profile
      |
      +--> aircraft aerodynamic model
      +--> control/actuator model
      +--> propulsion installation profile
                 |
                 +--> Engine Profile x engine slots

Common Maverick FDM Core
      atmosphere
      load dimensionalization
      ownership
      telemetry
      trim/validation
      SixDoF/Rigidbody boundary
```

For propulsion, see:

`Docs/FlightDynamics/SHARED_PROPULSION_PROFILE_ARCHITECTURE_V0.1.md`

F-16: one installed engine runtime.
F-15: two installed engine runtimes, normally sharing the same engine-profile asset when the exact configuration uses matching engines.

---

## 2. Source policy

Primary research index:

`Docs/Reference/F15_SOURCE_PACK_V0.1.md`

Mandatory configuration tags include at least:

- clean/basic F-15 aerodynamic model
- external-store configuration
- CFT configuration
- 3/8-scale RPV
- 1/12-scale rotary-balance model
- three-surface research F-15
- modified NASA F-15B
- PCA F-15
- F-15 ACTIVE
- F-15E / Strike Eagle

A value may cross configuration boundaries only after explicit compatibility analysis.

No “NASA F-15” bucket is allowed to combine all reports into one anonymous data set.

---

## 3. Roadmap

### F15-R0 — Reference configuration freeze

Goal: define exactly what the first Maverick F-15 represents.

Deliverables:

- reference-aircraft identity and configuration tag
- body-axis convention
- geometry: `S`, `b`, `cbar`
- declared physics datum / CG convention
- mass and inertia source table
- control-surface identity/sign convention
- initial bounded Mach / alpha / beta reference envelope
- source coverage matrix with `AUTHORITATIVE / PUBLIC_REFERENCE / CROSS_VALIDATION_ONLY / UNAVAILABLE`

Candidate source families already indexed:

- NASA TM-X-62360 — low-speed/high-alpha static aero
- NASA TN-D-8136 — flight-derived subsonic stability/control derivatives
- NASA TM-72861 family — geometry/control-surface data
- NASA TN-D-7941 — 3/8-scale RPV configuration context

Acceptance:

- no gameplay tuning value masquerades as source data
- no F-15E/F-15 ACTIVE/CFT/three-surface value is silently used in the clean reference

### F15-R1 — F-15 physical profile skeleton

Goal: make the F-15 a first-class client of `MavFlightDynamicsProfile`.

Implement:

- `MavF15FlightDynamicsProfile` / equivalent provider
- geometry
- mass/CG/inertia
- reference envelope
- physical surface limits
- reference-datum declaration
- propulsion installation profile with two engine slots, initially allowed to use null/zero engine data

Do **not** add new Rigidbody physics code.

Acceptance:

- core remains aircraft-independent
- F-16 regression unchanged
- F-15 profile can build and fail closed when source fields are incomplete

### F15-R2 — bounded low-speed/subsonic aerodynamic model

Goal: create the first real F-15 aerodynamic model inside a source-matched envelope.

Work:

- freeze static coefficient representation from a configuration-matched NASA data set
- preserve source axes and sign conventions
- implement dimensionalization through existing common math
- keep all interpolation/extrapolation policy explicit
- add coefficient regression vectors

Do not attempt Mach 2.5/full-envelope behavior in this phase.

Acceptance:

- deterministic coefficient tests
- symmetry/sign tests
- zero/known-control deflection tests
- no silent extrapolation

### F15-R3 — controls and actuators

Goal: represent the physical F-15 control effectors without direct torque shortcuts.

Candidate channels, subject to exact configuration/source confirmation:

- stabilators
- differential stabilator behavior
- ailerons
- rudders
- any configuration-specific flap/leading-edge devices only when the selected data model requires them

Architecture:

```text
pilot/test command
 -> control law
 -> requested physical surface state
 -> F-15 actuator model
 -> actual surface state
 -> F-15 aero model
```

No `Rigidbody.AddTorque` from pilot control code.

### F15-R4 — shared twin-engine propulsion foundation

Goal: prove the common Profile + Engine architecture on the first twin-engine aircraft.

Before using real F-15 thrust numbers:

- generic N-engine installation runtime
- two independent engine states
- per-engine position/direction
- force summation
- `r x F` moment calculation
- per-engine authority/envelope telemetry
- symmetric synthetic bench tests
- left-engine-only/right-engine-only sign tests

Then select the exact first F-15 engine profile from compatible public data.

Acceptance:

- identical symmetric engines produce no spurious yaw moment
- one-engine-out produces the expected moment sign from geometry
- no hand-authored “asymmetric yaw torque” tuning

### F15-R5 — isolated Unity subsonic validation

Goal: demonstrate the bounded F-15 reference stack in Unity before gameplay integration.

Tests:

- free fall / gravity semantics
- unpowered glide
- elevator/stabilator step
- roll-control step
- rudder step
- longitudinal/lateral doublets
- energy sanity
- inertia response
- propulsion symmetric/asymmetric tests when powered data becomes available

Validation target:

- compare local stability/control trends against configuration-compatible flight-derived data such as TN-D-8136 where possible

No handling tuning until a defect is classified.

### F15-R6 — high-alpha static extension

Goal: exploit one of the strongest parts of the public F-15 record.

Sources:

- TM-X-62360 static data to extreme alpha
- related configuration-matched high-alpha material

Work:

- extend the static coefficient model only where configuration compatibility is demonstrated
- validate control-effectiveness trends
- preserve a clear boundary between static high-alpha and rotational/spin aerodynamics

Do not simulate spin by merely increasing generic damping.

### F15-R7 — rotary/departure/spin model

Goal: implement real rotational-flow aerodynamic behavior rather than game-style stall hacks.

Sources:

- NASA CR-3478 — rotary-balance raw data
- NASA CR-3479 — analysis / spin prediction
- NASA TN-D-8052 — RPV departure/spin/recovery research
- NASA CR-3516 only for deliberate CFT configuration work

Work:

- add rotational-rate-dependent high-alpha data/model
- departure/spin equilibrium analysis
- recovery/control-effectiveness validation
- configuration tags on every rotary data set

This phase may require a separate aerodynamic regime/blending architecture; do not force all data into the initial low-speed model if that destroys provenance.

### F15-R8 — propulsion reference closure

Goal: move from engine architecture to a sourced powered F-15.

Tasks:

- select exact engine variant/configuration
- freeze thrust data / estimation model with source identity
- validate Mach/altitude envelope
- validate spool/transient behavior only when source data supports it
- add engine installation geometry
- one-engine-out validation

Useful source families already indexed:

- F100/F-15 real-time in-flight thrust-calculation research
- NASA TM-85902 F100 EMD flight evaluation — research configuration only
- PCA reports for propulsion-control and fuel/mass/CG methodology

Do not create a mixed “generic F100 accurate” model from incompatible variants.

### F15-R9 — transonic/supersonic extension

Goal: expand beyond the initial bounded subsonic model.

Candidate sources:

- NASA TP-2234
- NASA TP-2043
- NASA TP-2333

Major restriction:

Several of these use a three-surface or otherwise modified research configuration. Use them first for trend/methodology study; only freeze numerical data after compatibility with the target configuration is proven.

If no compatible source exists, keep the envelope bounded rather than inventing a correction.

### F15-R10 — control augmentation / instructor

Goal: make the physical aircraft controllable and accessible without contaminating the FDM.

Layers:

```text
player/mouse intent
 -> instructor
 -> F-15 control law / augmentation
 -> physical surfaces
 -> aerodynamic model
```

Research-aircraft control laws from ACTIVE/PCA/modified NASA aircraft remain separate from claims about operational F-15C/F-15E control laws.

Unsourced gains are `MAVERICK_TUNING`.

### F15-R11 — variants and gameplay integration

Only after the reference aircraft is stable:

- create distinct F-15C/F-15E/etc profiles when source coverage supports them
- stores/CFT become configuration modifiers with provenance
- integrate weapons, gear, ground effect and gameplay instructor around the common physical core
- preserve the old gameplay-tuning document as historical/gameplay material, not reference authority

---

## 4. Engine dependency on the roadmap

F-15 development must not wait for a perfect engine before any aero work, but powered claims must wait for a sourced engine profile.

Recommended order:

```text
F15 R0-R3: profile + aero + surfaces, propulsion may be NULL/zero
          |
          +--> shared propulsion P0-P2 in parallel
          |
F15 R4: twin-engine architecture proven
          |
F15 R5-R7: bounded Unity / high-alpha / rotary work
          |
F15 R8: source-frozen powered propulsion
```

This allows F-16 and F-15 to improve the same engine infrastructure instead of creating aircraft-specific duplicates.

---

## 5. Cross-aircraft component policy

### Common core

Must remain shared:

- `MavFlightDynamicsProfile`
- atmosphere
- body/axis conversion
- mass/inertia representation
- load dimensionalization
- SixDoF/Rigidbody boundary
- ownership
- force/moment accounting
- trim/validation infrastructure
- generic thrust-deck machinery
- shared engine runtime / engine-array aggregation

### Aircraft-specific

Expected to differ:

- aerodynamic coefficient model/data
- control-surface layout and actuator limits
- control laws
- mass/CG/inertia profile
- propulsion installation geometry
- selected engine profile(s)
- configuration modifiers

The common core must never contain `if (aircraft == F15)` physics hacks.

---

## 6. First implementation milestone after the current F-16 Unity test

Recommended first F-15 code milestone:

**F15-R0/R1 only.**

Create the configuration/source matrix and F-15 profile provider using frozen high-confidence geometry/mass/control data. Wire a two-slot propulsion installation whose engines may remain `Unavailable`/zero-thrust. Do not start high-alpha/spin/transonic implementation in the same patch.

This keeps the initial F-15 change reviewable and proves the profile architecture before adding another large aerodynamic model.
