# F-16 TP-1538 Runtime Semantics v0.1

**Status:** PHASE 5D SOURCE SEMANTICS FREEZE — ISOLATED POWERED-REFERENCE OPT-IN ONLY
**Required base:** `ef682695a2d35962deceedc222fac321a90fbc19`
**Scope:** NASA TP-1538 Table VI dimensional thrust + Garza/Morelli F-16 actual-power state
**Not authorized by this document:** gameplay `F16Replacement`, default/production TP-1538 attachment, full powered-flight certification, extrapolation outside the frozen thrust table, F-15 use

## 1. Authority chain

1. **Nguyen et al., NASA Technical Paper 1538 (1979), NTRS 19800005879**, Appendix B, Engine Simulation, Table VI, “THRUST VALUES USED IN SIMULATION”. This is the numeric authority for `Tidle`, `Tmil`, and `Tmax` as functions of altitude and Mach.
2. **Garza & Morelli, NASA/TM-2003-212145 (2003), _A Collection of Nonlinear Aircraft Simulations in MATLAB_**, F-16 simulation section. This is the runtime-semantics authority for actual engine power `Pa`, the `Tidle/Tmil/Tmax` power blend, and linear interpolation of the engine thrust database as a function of actual power, altitude, and Mach.
3. `Docs/Reference/F16_TP1538_THRUST_DECK_V0.1.md` and `Docs/Reference/Data/F16/TP1538/manifest.json` are the repository freeze/provenance authority for the accepted transcription.

The runtime canonical numeric dataset is the **published SI section** of:

`Docs/Reference/Data/F16/TP1538/table_vi_raw_source.csv`

with canonical artifact identity:

`SHA-256 = 9e0d9906daec75797560e37c6ae490ec10630495658ad9a93216ddacdc940972`

`table_vi_si_converted.csv` is a derived U.S.-customary cross-check using the report-era factor `1 lbf = 4.448 N`; it is not allowed to overwrite the published SI integers used by the runtime.

## 2. Exact source domain

| Variable | Frozen source domain | Runtime policy |
|---|---:|---|
| Mach | `0.2, 0.4, 0.6, 0.8, 1.0` | Linear interpolation inside `[0.2, 1.0]`; reject outside |
| Altitude | `0, 3048, 6096, 9144, 12192, 15240 m` | Linear interpolation inside `[0, 15240] m`; reject outside |
| Actual power `Pa` | `0..100 %` | Source equation below; reject non-finite or outside `[0,100]` |
| Thrust states | `Tidle`, `Tmil`, `Tmax` | Exactly three source planes |
| Force unit | N | Published SI values are runtime authority |

There are `5 × 6 × 3 = 90` physical thrust cells. Twelve physical coordinates have negative `Tidle`. They are retained with their published sign.

## 3. Decision classification

### 3.1 AUTHORITATIVE

| Decision | Authority |
|---|---|
| `Tidle` means idle engine thrust | NASA/TM-2003-212145 nomenclature and TP-1538 Table VI |
| `Tmil` means military engine thrust | same |
| `Tmax` means maximum engine thrust | same |
| `Pa` is actual engine power level, percent | NASA/TM-2003-212145 |
| `Pa < 50`: blend `Tidle -> Tmil` | NASA/TM-2003-212145 F-16 engine equation |
| `Pa >= 50`: blend `Tmil -> Tmax` | same |
| Engine thrust database is linearly interpolated as a function of actual power, altitude, and Mach | NASA/TM-2003-212145 |
| The published Table VI SI cells, including negative idle cells, are accepted source values | NASA TP-1538 + frozen transcription provenance |
| `Pa=0`, `Pa=50`, `Pa=100` recover `Tidle`, `Tmil`, `Tmax` exactly | direct consequence of the authoritative equation |

The source power law is implemented as:

```text
Pa < 50:
    T = Tidle + (Tmil - Tidle) * (Pa / 50)

Pa >= 50:
    T = Tmil + (Tmax - Tmil) * ((Pa - 50) / 50)
```

The `Pa` passed to this equation is the existing F-16 Garza/Morelli **actual** power state from `MavF16GarzaMorelliEngineDynamics`, not raw throttle and not commanded power.

### 3.2 DERIVED

These are mathematical/implementation consequences, not extra aircraft data:

- On the rectilinear altitude/Mach grid, linear interpolation in each independent variable is implemented as bilinear interpolation inside the enclosing cell.
- For bilinear interpolation, evaluating Mach then altitude or altitude then Mach gives the same result to floating-point tolerance; no extra interpolation-priority rule is introduced.
- Existing Maverick F-16 installation convention applies scalar thrust along aerodynamic body `+X`. Therefore a negative table value remains negative and produces a `-X` force. The runtime does not reinterpret or clamp the source sign.
- A source-authoritative deck result can be authoritative while the complete aircraft is still not operationally live-ready. Full-aircraft readiness includes other gates outside the thrust table.
- The compact runtime FNV hash is an implementation-integrity check derived from the canonical dataset identity; it is not a replacement for the canonical SHA-256 artifact identity.

### 3.3 UNAVAILABLE / NOT AUTHORIZED

No primary source reviewed for this phase establishes an engine-thrust rule for:

- Mach below `0.2`;
- Mach above `1.0`;
- altitude below `0 m`;
- altitude above `15,240 m`;
- `Pa < 0` or `Pa > 100`;
- NaN or infinity;
- continuation of a previous valid thrust when the current query is invalid.

Therefore all of those states are **rejected**. They are not clamped, extrapolated, replaced with a boundary value, or reported as authoritative zero thrust.

NASA/TM-2003-212145 separately describes linear extrapolation for some **aerodynamic** data outside aerodynamic tables. Phase 5D does **not** transfer that aerodynamic policy to the engine thrust database; the engine authority establishes interpolation, not an engine extrapolation rule.

## 4. Boundary behavior

Exact source endpoints are in-envelope and valid:

- `M = 0.2` and `M = 1.0`;
- `h = 0 m` and `h = 15,240 m`;
- `Pa = 0`, `50`, and `100`.

Anything numerically outside those intervals is unavailable. The evaluator returns an invalid/out-of-envelope result with zero thrust for that query and does not carry forward the last valid value.

## 5. Negative idle thrust

The frozen source contains 12 negative physical `Tidle` coordinates. Both printed SI and U.S.-customary sections agree in sign at those coordinates. Negative idle is therefore not treated as a transcription defect and is not clamped.

No additional physical interpretation is invented for runtime integration. The signed table value is passed through the same axial thrust direction as every other scalar thrust value.

## 6. Runtime architecture boundary

```text
NASA TP-1538 frozen Table VI published-SI values
            ↓
MavF16Tp1538ThrustDeck
  - canonical dataset identity check
  - source-domain validation
  - h/M linear interpolation
  - Pa source equation
  - fail-closed outside source domain
            ↓
MavF16GarzaMorelliEngineDynamics
(actual Pa state is owned by MavEngineRuntime)
            ↓
MavEngineRuntime
            ↓
MavPropulsionSystem / MavF16PropulsionSystem
            ↓
MavFlightDynamicsLoadSet
            ↓
MavSixDoFBody — sole physical load writer
```

The thrust-deck evaluator does not hold spool state and contains no direct physics-write call sites. `MavEngineRuntime` owns mutable actual power. `MavPropulsionSystem` sums the one F-16 engine contribution once. `MavSixDoFBody` remains the physical load-application boundary.

## 7. Default-zero and explicit powered-reference opt-in

Phase 5D does **not** change the default F-16 propulsion state.

`MavF16PropulsionSystem.thrustDeck` remains an uninitialized nullable field. Therefore:

```text
new/default MavF16PropulsionSystem
    thrustDeck == null
        ↓
ConfigureF16Installation()
    passes null unchanged into MavF16PropulsionInstallation
        ↓
shared MavEngineRuntime
    deck == null
        ↓
result.thrustN = 0 N
```

`Awake()` does not manufacture or attach TP-1538. `ConfigureF16Installation()` also does not manufacture a deck.

The only Phase-5D convenience path that attaches the frozen deck is the explicit call:

```text
ConfigureTp1538SourcedInstallation()
```

That call is reserved for the isolated powered-reference validation path. A deliberately serialized non-null deck is also explicit configuration, not a default.

## 8. Historical 5C-R remains a separate unpowered scenario

The historical 5C-R builder remains unchanged and explicitly constructs:

```text
r.propulsion.thrustDeck = null;
r.propulsion.ConfigureF16Installation();
```

It does not call `ConfigureTp1538SourcedInstallation()` and therefore retains exactly-zero-thrust regression semantics.

The new Phase-5D powered-reference validation is a separate scenario. It may reuse the in-memory structural rig builder, but only **after** the historical null-deck rig is built does the new validation explicitly opt into TP-1538. This does not redefine 5C-R and does not make powered thrust its default.

## 9. Runtime provenance derivation

Canonical artifact identity:

- `table_vi_raw_source.csv` SHA-256: `9e0d9906daec75797560e37c6ae490ec10630495658ad9a93216ddacdc940972`
- `table_vi_si_converted.csv` SHA-256: `f32592f99eb30de58c1fc42c02ad984ce3b371bbea3db8e369775b05becb8b47` — derived cross-check only

The runtime class declares:

```text
CanonicalRawSourceSha256 = 9e0d9906...40972
FrozenSourceIdentity =
  "NASA TP-1538 / NTRS 19800005879 / Table VI / rawSourceCsv sha256 "
  + CanonicalRawSourceSha256
FrozenSourceVersion = F16_TP1538_THRUST_DECK_V0.1
```

`MavThrustDeckProvenance.ComputeTableHash` then computes FNV-1a over, in order:

1. `FrozenSourceIdentity` — therefore already containing the canonical raw CSV SHA-256;
2. `FrozenSourceVersion`;
3. altitude-axis float bit patterns;
4. Mach-axis float bit patterns;
5. all 30 `Tidle` float bit patterns;
6. all 30 `Tmil` float bit patterns;
7. all 30 `Tmax` float bit patterns.

For the frozen runtime arrays this deterministically yields:

- runtime provenance hash: `0xD8CD7773`

The SHA-256 is the canonical dataset identity. The FNV hash is only a compact runtime drift detector **derived from that identity plus the exact runtime arrays**. A value mutation, axis mutation, dataset-identity mutation, or version mutation changes the derived hash and removes the authoritative runtime claim.

## 10. Deterministic offline validation

`Tools/validate_f16_tp1538_runtime_semantics.py` independently:

- recomputes the canonical raw CSV SHA-256 and checks it against the manifest and C# constant;
- parses the published SI rows from the frozen raw source CSV;
- parses the runtime C# arrays;
- compares every one of the 90 `(altitude, Mach, state)` runtime cells individually for exact equality;
- recomputes `0xD8CD7773` from the canonical identity and runtime float bits;
- validates interpolation/power semantics and fail-closed behavior;
- verifies default `thrustDeck == null` remains on the shared `0 N` branch;
- verifies TP-1538 attachment is explicit opt-in only;
- verifies the historical 5C-R null-deck path remains separate;
- scans executable C# code for actual direct physics-write calls/assignments instead of rejecting the literal word `Rigidbody` in comments or documentation.

## 11. Sources reviewed

- Nguyen, L. T. et al., **NASA TP-1538**, _Simulator Study of Stall/Post-Stall Characteristics of a Fighter Airplane With Relaxed Longitudinal Static Stability_, December 1979, NTRS `19800005879`, Appendix B / Engine Simulation / Table VI.
- Garza, F. R.; Morelli, E. A., **NASA/TM-2003-212145**, _A Collection of Nonlinear Aircraft Simulations in MATLAB_, January 2003, NTRS `20030013626`, F-16 engine simulation section.
- Morelli, E. A., _Global Nonlinear Parametric Modeling with Application to F-16 Aerodynamics_, NASA NTRS `19990008037` / `20040110310`, for aerodynamic-model scope context only; it is not used to invent propulsion rules.
