# Maverick Shared Propulsion — Profile + Engine Architecture v0.1

Status: **ARCHITECTURE ROADMAP — NOT YET IMPLEMENTED**

## 1. Decision

Maverick propulsion will use a **Profile + Engine** architecture.

The reusable object is not “the F-16 engine script” or “the F-15 engine script.” The reusable system is:

```text
Aircraft Flight-Dynamics Profile
        |
        +-- Propulsion Installation Profile
        |       +-- Engine Slot 0 -> Engine Profile
        |       +-- Engine Slot 1 -> Engine Profile   (when present)
        |
        +-- Aerodynamic model / mass / inertia / controls

Engine Profile
        |
        v
Shared Engine Runtime (one independent state per installed engine)
        |
        v
Per-engine force / moment / fuel / telemetry
        |
        v
Propulsion System sums all engines
        |
        v
MavSixDoFBody
```

“Shared engine” means **shared runtime, profile format, validation, and load-composition code**. It does not mean F-15 and F-16 silently use identical numbers. An `EngineProfile` may be shared by two aircraft only when the exact engine variant and the source assumptions match.

Aircraft installation remains separate because inlet geometry, engine count, mount location, thrust line, throttle channel, and aircraft integration are not properties of the bare engine alone.

---

## 2. Target ownership split

### `MavFlightDynamicsProfile`

Aircraft-level physical definition:

- geometry
- mass / CG / inertia
- aerodynamic validity envelope
- surface limits
- propulsion installation profile reference/identity

It must not contain mutable spool state.

### `MavEngineProfile`

Static engine data and provenance:

- `engineProfileId`
- exact engine variant / configuration identity
- source/provenance
- throttle-command mapping or command-law identity
- power/spool dynamics model identity and parameters
- thrust-deck reference
- validated altitude/Mach/power envelope
- afterburner semantics
- fuel-flow data/model when sourced
- rotor angular momentum when sourced
- optional engine operating limits

It contains **data/configuration**, not live state.

### `MavEngineRuntime`

One instance per installed engine. Owns mutable state only:

- commanded power
- actual power / spool state
- running / stopped state later
- afterburner state when explicitly modeled
- failure/flameout state later
- current thrust result
- current fuel flow later
- current provenance/envelope status

Two F-15 engines using one `MavEngineProfile` still have **two independent `MavEngineRuntime` states**.

### `MavEngineInstallationProfile`

Aircraft-specific engine installation:

- engine-profile reference
- body-axis position relative to the declared physics datum / CG
- thrust direction
- throttle channel
- inlet/configuration tag
- optional installation-specific correction model only when sourced

### `MavPropulsionSystem`

Aircraft-level runtime aggregator:

```text
for each installed engine i:
    engineLoads_i = runtime_i.Evaluate(...)

F_total = sum(F_i)
M_total = sum(r_i x F_i + M_i)
```

It must not touch `Rigidbody`. `MavSixDoFBody` remains the single load-application boundary.

---

## 3. F-16 and F-15 shape

### F-16 reference path

```text
F16 FlightDynamicsProfile
  -> PropulsionInstallationProfile
       -> slot 0
            EngineProfile: selected/sourced F-16 reference engine model
            position/direction: F-16 installation

Shared Engine Runtime x1
```

The first migration must preserve the currently sourced Garza/Morelli power dynamics exactly. Do not change behavior just to make the architecture generic.

### F-15 reference path

```text
F15 FlightDynamicsProfile
  -> PropulsionInstallationProfile
       -> slot 0 LEFT
            EngineProfile: exact selected F100/F110-family variant when sourced
            independent runtime state
            left installation point
       -> slot 1 RIGHT
            usually same EngineProfile asset when the aircraft configuration really uses two matching engines
            independent runtime state
            right installation point

Shared Engine Runtime x2
```

This architecture must naturally support:

- symmetric thrust
- one-engine-out operation
- asymmetric spool/transient behavior
- differential throttle later
- physical yaw/roll moment from engine separation
- propulsion-controlled-aircraft experiments later without special-casing the rigid-body core

---

## 4. What already exists and should be preserved

Current code already provides useful generic infrastructure:

- `MavPropulsionModelBase` — aircraft-independent load contract
- `MavPropulsiveLoads` — force/moment output in aerodynamic body axes
- `MavThrustDeckBase` — pure altitude/Mach/power thrust lookup
- thrust-data authority labels
- explicit out-of-envelope policy
- deterministic interpolation
- provenance/content-hash support for frozen thrust decks
- `MavSixDoFBody` — single load-application boundary

The new architecture should **evolve these pieces**, not create a second propulsion stack beside them.

---

## 5. Current propulsion shortcomings / technical debt

### ENG-001 — F-16 identity and engine law are coupled

`MavF16EnginePowerModel` hardcodes F-16/Garza-Morelli throttle gearing and power dynamics in an aircraft-named runtime component.

**Problem:** the same runtime shape cannot cleanly host an F100/F110 profile for another aircraft without inheritance/copy-paste.

**Target:** move static law/data identity behind `MavEngineProfile` / engine-state strategy while preserving current F-16 equations.

### ENG-002 — one throttle / one engine state is baked into the main propulsion contract

`MavPropulsionModelBase.Evaluate(... throttle01 ...)` and `MavPropulsiveLoads.powerState01` are singular.

**Problem:** an F-15 requires two independent engines even if both share one profile.

**Target:** one engine runtime per slot plus an aircraft-level propulsion aggregator.

### ENG-003 — no first-class engine installation geometry

Current F-16 helper constructs axial thrust along body +X and zero propulsion moment, effectively assuming the thrust line passes through the CG.

**Problem:** twin-engine offset cannot produce asymmetric yaw/roll moments physically.

**Target:** installation position/direction and `r x F` moment composition.

### ENG-004 — no per-engine telemetry/provenance result

The current net load exposes one power state and one authority flag.

**Problem:** a twin-engine aircraft cannot tell which engine is out of envelope, failed, non-authoritative, or producing different thrust.

**Target:** per-engine result array/records plus separately summed aircraft loads.

### ENG-005 — F-16 dimensional thrust data is not yet frozen/connected

The existing F-16 power dynamics are sourced, but the approved dimensional thrust deck is still pending repository freeze/connection. The current safe behavior is zero thrust when the deck is unavailable.

**Target:** finish manual TP-1538 Table VI transcription/provenance verification, then attach through the generic deck interface without changing engine-core architecture.

### ENG-006 — F-15 engine variant is not selected/frozen

Public F100/F-15 research exists, but research/EMD/DEEC/PW-100/PW-229 material is configuration-specific.

**Problem:** “F100” is not one numeric engine.

**Target:** choose the first F-15 aircraft/engine configuration before freezing any deck or transient model. Keep EMD/DEEC/ACTIVE values tagged to their actual research configuration.

### ENG-007 — fuel flow, fuel mass, CG and inertia migration are absent

Current propulsion does not own fuel consumption.

**Impact:** long-duration aircraft mass/CG/inertia do not evolve with fuel state.

**Target:** add fuel-flow output only after source coverage exists; aircraft fuel system owns tank mass distribution, not the engine profile itself.

### ENG-008 — rotor angular momentum / gyroscopic moment is not integrated

The F-16 reference material already documents an engine angular-momentum term, but the current propulsion load path does not apply that gyroscopic coupling.

**Target:** add an optional sourced rotor-angular-momentum channel to the generic engine profile/runtime. Zero when unsupported.

### ENG-009 — inlet pressure recovery / distortion is absent

Current generic thrust-deck query is altitude + Mach + power.

**Impact:** AoA, beta, inlet distortion, and installation effects cannot reduce thrust or stability margin.

**Target:** later introduce a separately sourced inlet/installation modifier. Do not silently bake aircraft inlet behavior into a reusable bare-engine deck.

### ENG-010 — engine operating states are minimal

No first-class start, shutdown, flameout, relight/airstart, compressor stall/surge, DEEC/FADEC failure, or fault-accommodation model.

**Target:** later phase. Do not block initial reference flight on unsourced failure behavior.

### ENG-011 — afterburner semantics are not sufficiently generic

The F-16 reference model blends military-to-maximum thrust through its sourced power-state structure. That does not prove every future engine uses the same state interpretation.

**Target:** make afterburner/augmentation semantics profile-defined, never universal hard-coded core behavior.

### ENG-012 — several old propulsion docs describe infrastructure as “deferred” that now exists

`F16_PROPULSION_REFERENCE_V0.1.md` predates the current generic thrust-deck interpolation/envelope/provenance infrastructure.

**Target:** keep the document as a source-status record but update/supersede its architecture notes when propulsion implementation begins. Source-data gaps and code-infrastructure gaps must be listed separately.

---

## 6. Source candidates already indexed

### F-16

See `Docs/Reference/F16_SOURCE_PACK_V0.1.md` and `F16_PROPULSION_REFERENCE_V0.1.md`.

Current strongest chain:

- Garza & Morelli NASA/TM-2003-212145 — throttle gearing / power-state dynamics / thrust-model structure
- NASA TP-1538 — public thrust-table candidate (Table VI), transcription/provenance freeze still required

Do not substitute third-party decks while calling them NASA-authoritative.

### F-15 / F100 family

See `Docs/Reference/F15_SOURCE_PACK_V0.1.md`.

Useful public NASA research includes:

- real-time in-flight thrust-calculation work on F100-equipped F-15 research aircraft
- F100 EMD flight evaluation (`NASA TM-85902`) — research/development configuration only
- PCA F-15 work — useful for propulsion-control and fuel/mass/CG research, modified aircraft only
- propulsion/afterbody pressure research for configuration effects

Before implementation, freeze an exact first F-15 engine/configuration identity. Do not build a generic “F100 accurate” deck from mixed reports.

---

## 7. Migration sequence

### P0 — architecture freeze

- freeze Profile + Engine separation
- freeze per-engine runtime requirement
- freeze installation geometry ownership
- no gameplay behavior changes

### P1 — extract F-16 engine profile without changing results

- represent current Garza/Morelli throttle/power law as profile-selected behavior
- one engine slot
- regression must be bit/near-bit equivalent to current power-state tests
- dimensional thrust remains zero until the accepted deck is frozen

### P2 — generic engine array / installation loads

- N engine slots
- independent state per slot
- sum force
- compute `r x F`
- per-engine telemetry
- symmetric two-engine synthetic validation only; no F-15 numeric claims yet

### P3 — F-16 sourced thrust deck

- finish TP-1538 transcription/provenance freeze
- plug deck into F-16 engine profile
- powered F-16 reference validation

### P4 — F-15 engine-profile freeze

- select exact initial F-15 configuration
- select exact engine variant/source chain
- freeze installation spacing/direction from configuration-matched data
- keep unavailable values unavailable

### P5 — powered F-15 reference

- two independent engine runtimes
- symmetric powered flight
- one-engine-out force/moment validation
- throttle transient validation when source data supports it

### P6 — fuel / gyro / inlet extensions

Only after source coverage is adequate:

- fuel flow and aircraft fuel mass
- rotor angular momentum
- inlet recovery/distortion
- airstart/failure behavior
- augmentation/nozzle features

---

## 8. Hard rules

1. No engine runtime may call `Rigidbody` directly.
2. One engine profile may be reused across aircraft only when the exact variant/source identity is compatible.
3. Engine installation belongs to the aircraft profile, not the bare engine profile.
4. Twin engines always have independent runtime state, even when they share one profile asset.
5. Net propulsion moment is derived from each engine force and installation geometry, not hand-tuned yaw torque.
6. Missing source data returns unavailable/zero or clearly labeled approximation; never a plausible-looking “accurate” number.
7. Gameplay tuning stays above/outside the reference engine layer.
8. F-16 and F-15 must use the same generic propulsion infrastructure; aircraft-specific code supplies profiles/models, not a second engine core.
