# F-16 TP-1538 Runtime Propulsion Validation v0.1

**Phase:** 5D follow-up
**Required base:** `ef682695a2d35962deceedc222fac321a90fbc19`
**Target branch requested:** `sol/f16-thrust-runtime`
**Execution boundary:** offline validation only in this environment. **No Unity validation is claimed. No push was performed.**

## 1. Follow-up corrections

This follow-up intentionally does not expand Phase-5D scope. It corrects four integration/validation details:

1. The static physics-writer validator now scans actual executable physics-write call sites/assignments after removing comments and string literals. The word `Rigidbody` by itself is no longer a failure.
2. `MavF16PropulsionSystem` retains the pre-Phase-5D default: `thrustDeck == null` is passed through unchanged and the shared `MavEngineRuntime` returns exactly `0 N`. TP-1538 is never silently attached by `Awake()` or ordinary `ConfigureF16Installation()`.
3. The isolated powered-reference path must explicitly call `ConfigureTp1538SourcedInstallation()`.
4. Runtime provenance is explicitly derived from the canonical frozen dataset identity, and every runtime thrust cell is compared individually with the canonical published-SI source values.

## 2. Canonical dataset/runtime equality

Canonical runtime numeric authority:

`Docs/Reference/Data/F16/TP1538/table_vi_raw_source.csv`, published **SI** section.

Canonical artifact SHA-256:

`9e0d9906daec75797560e37c6ae490ec10630495658ad9a93216ddacdc940972`

The deterministic offline validator parses both the frozen source CSV and `MavF16Tp1538ThrustDeck.cs`, transposes only the table storage order, and performs **90 individual exact equality checks**:

```text
30 physical (h, M) coordinates × 3 source thrust states = 90/90 exact
```

This is not a tolerance comparison and is not based on the derived `table_vi_si_converted.csv`. The runtime values are the published integer-N SI cells.

## 3. Provenance-hash derivation

The C# runtime identity embeds the canonical raw-source SHA-256 in `FrozenSourceIdentity`. The compact FNV hash is then computed over:

```text
canonical dataset identity containing rawSourceCsv SHA-256
+ runtime source version
+ altitude axis float bits
+ Mach axis float bits
+ Tidle float bits
+ Tmil float bits
+ Tmax float bits
```

Fresh offline recomputation:

```text
runtime provenance hash derived from canonical identity: 0xD8CD7773
```

The SHA-256 remains the canonical artifact identity. `0xD8CD7773` is a runtime drift detector derived from that identity and the exact runtime representation; it is not an independent claim of provenance.

## 4. Default-zero behavior / explicit opt-in

Static wiring validation proves:

- `public MavThrustDeckBase thrustDeck;` has no default initializer;
- `Awake()` does not call the TP-1538 attachment helper;
- ordinary `ConfigureF16Installation()` does not call the TP-1538 attachment helper;
- ordinary configuration passes `thrustDeck` unchanged to `MavF16PropulsionInstallation.CreateInstallation(thrustDeck)`;
- the inherited shared runtime still contains the base branch `deck == null -> result.thrustN = 0f -> return`;
- only `ConfigureTp1538SourcedInstallation()` invokes the TP-1538 attachment helper.

Therefore the production/default F-16 behavior remains:

```text
thrustDeck == null -> exactly 0 N
```

The new sourced thrust path is explicit opt-in only.

## 5. 5C-R separation

The historical 5C-R in-memory builder remains unchanged and explicitly sets:

```text
r.propulsion.thrustDeck = null;
```

before ordinary F-16 propulsion configuration. It contains no TP-1538 sourced-installation call.

The new `MavF16Tp1538RuntimeValidation` is a **separate powered-reference validation**. It starts from the isolated structural rig and then explicitly opts into TP-1538 for its own test only. No historical 5C-R scenario is converted to powered operation.

## 6. Direct physics-writer static check

The validator strips C# comments, character literals, and string literals, then searches executable code for direct physical writes such as:

- `.AddForce(...)`
- `.AddRelativeForce(...)`
- `.AddTorque(...)`
- `.AddRelativeTorque(...)`
- `.MovePosition(...)`
- `.MoveRotation(...)`
- direct velocity assignments such as `.linearVelocity = ...` / `.angularVelocity = ...`

It deliberately does **not** reject the identifier or English word `Rigidbody`.

Fresh result for the Phase-5D F-16 production files:

```text
direct physics-write call sites: 0
```

`MavSixDoFBody` therefore remains the established physical writer boundary; no shared Core file is modified by this patch.

## 7. Fresh offline validator result

Command:

```text
python3 Tools/validate_f16_tp1538_runtime_semantics.py
```

Fresh result:

```text
F-16 TP-1538 Phase 5D independent runtime validation PASS
required base: ef682695a2d35962deceedc222fac321a90fbc19
canonical rawSourceCsv SHA-256: 9e0d9906daec75797560e37c6ae490ec10630495658ad9a93216ddacdc940972
canonical runtime cells exact: 90/90
source cells reachable at Pa=0/50/100: 90/90
negative Tidle coordinates preserved: 12/12
runtime provenance hash derived from canonical identity: 0xD8CD7773
default F-16 propulsion: thrustDeck=null preserved; shared runtime null-deck thrust=0 N
powered TP-1538 reference path: explicit opt-in only
historical 5C-R: separate null-deck/unpowered path preserved
direct physics-write call sites in Phase-5D production files: 0
assertions: 358
mutation probes:
  M1 sign clamp on negative idle: KILLED
  M2 swapped Mach coordinates: KILLED
  M3 swapped altitude coordinates: KILLED
  M4 wrong power-state mapping: KILLED
  M5 wrong interpolation dimension: KILLED
  M6 unauthorized extrapolation: KILLED
  M7 stale last-known thrust: KILLED
  M8 silent default TP-1538 attachment: KILLED
mutations killed: 8/8
```

## 8. Frozen-data regression

The historical source files remain byte-identical:

- raw source SHA-256: `9e0d9906daec75797560e37c6ae490ec10630495658ad9a93216ddacdc940972`
- derived SI SHA-256: `f32592f99eb30de58c1fc42c02ad984ce3b371bbea3db8e369775b05becb8b47`

The existing primary and independent TP-1538 data validators are re-run as part of final offline validation; their source-data mutation protections remain separate from the new runtime mutation checks.

## 9. Unity status

`MavF16Tp1538RuntimeValidation.cs` remains an isolated Unity validation harness, but **it was not executed in this environment**. This report does not claim:

- Unity compilation PASS;
- Unity runtime PASS;
- P0.2/P0.2b/5C-R fresh PASS;
- allocation benchmark PASS;
- powered-flight certification.

Those remain downstream execution checks, not evidence supplied by this offline patch.

## 10. Disposition

Offline Phase-5D follow-up status: **GREEN**.

The patch preserves default zero thrust, makes sourced thrust explicit opt-in, binds runtime provenance to the canonical dataset identity, validates all 90 runtime cells exactly, keeps historical 5C-R unpowered, and removes the prior static-validator false positive without weakening the actual physics-write guard.
