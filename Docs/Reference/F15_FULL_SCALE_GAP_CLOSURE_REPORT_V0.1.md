# F-15 Full-Scale Gap Closure Report V0.1

Status: **PARTIAL CLOSURE — no production implementation**

Branch target:

`sol/f15-fullscale-gap-closure`

Required base:

`ef682695a2d35962deceedc222fac321a90fbc19`

Frozen aircraft:

`NASA_F15B_836_SN74_0141_PRE_QUIET_SPIKE_BASELINE_F100_PW_100`

## 1. Executive result

This phase **did not find enough exact-target public numeric data to justify a full F-15
aerodynamic or powered implementation**.

It did close several important provenance/control gaps and substantially improved the source
acquisition path:

1. exact NASA 836 FCS architecture and two Mach schedule boundaries are now directly frozen;
2. exact NASA 836 baseline aerodynamic-model structure and four-flight validation/update
   provenance are directly established;
3. the public reports expose a real validation limitation in the transonic lateral-directional regime;
4. the original McDonnell Douglas source lineage (`MDC A4172` and
   `DN-1180.01-238-458 Rev. D`) is now explicitly identified as the highest-value acquisition target;
5. high-quality F100-PW-100 NASA propulsion sources were found and classified, without falsely
   promoting prototype-engine data to the NASA 836 installed-engine deck.

Recommendation:

**PARTIAL — MORE RESEARCH**

## 2. Exact gaps closed

### GC-01 — exact-target FCS architecture detail

Closed to `FROZEN_DIRECT`:

- production mechanical + single-string analog CAS unchanged by Quiet Spike;
- PRAD, RRAD, pitch/roll/yaw CAS functional roles;
- stabilator symmetric-pitch / differential-roll role;
- ARI functional role;
- roll-CAS command limiter scheduling basis;
- yaw-CAS and roll-to-yaw crossfeed functional role;
- ARI operational boundary: below Mach 1.0; zero otherwise;
- roll-to-yaw crossfeed nullified above Mach 1.5.

Primary authority:
NASA/TM-2012-215978 and NASA/TM-2009-214651.

No gains, actuator rates, or hard travel limits were inferred.

### GC-02 — baseline aerodynamic-model provenance

Closed to `FROZEN_DIRECT` provenance:

- exact NASA 836 pre-spike baseline model existed in the DFRC nonlinear 6-DOF simulation;
- four baseline research flights were used to validate/update it before spike installation;
- updates were implemented as increments to the baseline model;
- the analysis contains lookup-table/uncertainty structure and static/control/damping derivative families;
- the model was validated by replaying flight-measured control-surface positions and comparing
  simulation with flight response.

This is sufficient to define what source object must be recovered next, but not enough to
implement the missing coefficient database.

### GC-03 — exact validation limitation

NASA/TM-2008-214634 / AIAA-2007-6638 directly states that transonic lateral-directional
maneuver agreement was inconsistent and transonic rudder sweeps were especially difficult to
reproduce.

This is now an auditable source limitation. It prevents a future implementation from claiming that
a recovered baseline model is uniformly validated through the transonic regime without further review.

### GC-04 — original engineering-source acquisition path

Public USAF/AFIT documents identify:

- McDonnell Douglas `MDC A4172`, including an aerodynamic-coefficient/stability-derivative Part II;
- McDonnell Aircraft `DN-1180.01-238-458 Rev. D`, *F-15 Flight Control System Description*.

The original primary documents were not obtained, so numeric fields are not frozen from secondary
citations. The gap is narrowed from "unknown source" to "known document not yet acquired."

## 3. Gaps partially narrowed but not closed

### Geometry/reference

Strong government/public-family support exists for:

- 608 ft^2 F-15 reference wing area;
- approximately 191.3 in mean aerodynamic chord in USAF/AFIT F-15 modeling lineage;
- 45-deg leading-edge sweep;
- FS/WL/BL structural-coordinate convention;
- a same-airframe PFTF analysis relation of 28% MAC to FS 561.7.

None is newly frozen for NASA 836 because the exact coefficient-reference definition and baseline
configuration equivalence were not proven.

Status: `SUPPORTED_ONLY`.

### Controls

NASA CP-10143 Vol. 3 contains strong F-15 A-D family surface-travel/CAS-authority information.
Those values were visually surfaced in the public NASA proceedings during research, but they are
not exact NASA 836 hard-stop/actuator-rate authority.

Status: `SUPPORTED_ONLY`; target hard limits/rates remain `UNAVAILABLE`.

### Propulsion

NASA TP-1373, TM-X-3261, TP-1034, TP-1482 and TP-1782 form a strong public F100-PW-100
research chain for performance, thrust calculation and transients.

They do **not** establish that their prototype/subvariant engines are the exact NASA 836 installed
engine builds, nor do they include the target inlet/installation loss mapping.

Status: `SUPPORTED_ONLY` or `CROSS_VALIDATION_ONLY`, not a frozen target deck.

## 4. Gaps still open

### High priority

- `S` — exact NASA 836 coefficient reference area.
- `cbar` — exact NASA 836 mean aerodynamic chord definition.
- absolute reference datum / aerodynamic moment reference.
- baseline numeric `CX/CA`, `CY`, `CZ/CN`, `Cl`, `Cm`, `Cn` database.
- numeric control derivatives.
- numeric rate derivatives.
- exact coefficient Mach/alpha/beta validity envelope.
- stabilator/aileron/rudder hard stops for NASA 836.
- actuator rates/dynamics and surface sign algebra.
- numeric CAS/ARI gains/schedules beyond directly documented logic boundaries.
- installed F100-PW-100 thrust versus Mach/altitude/power.
- military/afterburner thrust deck semantics.
- engine spool/transient model proven equivalent to target engines.
- target fuel-flow map.
- target side-inlet recovery/distortion map.
- left/right engine installation coordinates/thrust-line vectors.
- fuel-dependent NASA 836 mass/CG/inertia schedule.

### Later/full-envelope

- high-alpha exact full-scale target coefficients;
- rotary/departure data for the exact full-scale target;
- transonic/supersonic baseline validation beyond the known limited campaign;
- configuration-device schedules for flaps/speed brake/other devices.

## 5. Configuration conflicts found

### Conflict A — F100-PW-100 does not identify one numeric engine deck

The target engine name is exact, but public F100-PW-100 research spans prototype engines,
serial-specific calibrations, `(1)/(3)` model states, analog/electronic control variants and later DEEC
programs.

Resolution: keep target thrust/transient/fuel-flow data unavailable until engine build/control mapping
is explicit.

### Conflict B — descriptive target thrust numbers differ by source

Same-aircraft NASA reports use rounded uninstalled sea-level-static full-afterburner descriptions
around 23.5–25 klbf per engine.

Resolution: treat these as descriptive capability context, not a precision thrust datum; do not average.

### Conflict C — F-15 family geometry is consistent but exact reference authority is missing

Government/NASA/USAF sources strongly converge on the familiar F-15 family geometry.

Resolution: consistency is not enough for exact NASA 836 aerodynamic dimensionalization. Original
coefficient-reference documentation is still required.

### Conflict D — transonic lateral-directional validation is incomplete

Exact NASA 836 baseline simulation reproduced representative maneuvers reasonably, but the published
flight analysis reports inconsistent transonic rudder-sweep reproduction.

Resolution: future transonic authority must carry an uncertainty/validation qualifier.

## 6. Numeric datasets frozen in this phase

No new coefficient table, thrust deck, actuator table, inlet-recovery map, or engine-installation
dataset was frozen.

Two exact-target control schedule boundaries were added as direct scalar source facts:

| Quantity | Value | Source / locator | Provenance |
|---|---:|---|---|
| ARI operational boundary | operative below M 1.0; zero otherwise | NASA/TM-2012-215978, *The Flight Control System*, text associated with Fig. 5 | `FROZEN_DIRECT` |
| Roll-to-yaw crossfeed boundary | nullified above M 1.5 | same | `FROZEN_DIRECT` |

Because these are isolated control logic thresholds rather than a reusable numeric data grid, no
`Docs/Reference/Data/F15/` package was created.

Existing R0.5 mass/inertia/dimension values were carried forward unchanged.

## 7. Values deliberately rejected

The following were **not** promoted:

- `S = 608 ft^2` — strong family support, insufficient exact NASA 836 reference-definition proof.
- `cbar = 191.3 in` — traces to F-15/AFIT/McDonnell lineage, but no exact NASA 836 promotion proof.
- 45-deg wing sweep — family-level support, not needed badly enough to weaken exact-target policy.
- 28% MAC = FS 561.7 — same-airframe PFTF analysis clue, but not the frozen target mass state or a
  complete datum definition.
- family F-15 stabilator/aileron/rudder travel values — not exact NASA 836 hard-stop authority.
- ACTIVE/NASA 837 actuator rates — canard/TVC/digital research configuration is incompatible.
- prototype F100-PW-100 TP-1373 thrust values — engine build/install equivalence not established.
- TM-X-3261 / TP-1034 transient response — exact target engine/control state not established.
- NASA CR-144866 inlet recovery values — full-scale F-15 inlet research, but different aircraft/engine
  test state.
- approximate 23.5–25 klbf static-afterburner descriptions — rounded capability statements, not a deck.
- RPV coefficient/control/geometry values — prohibited by frozen RPV transfer policy.

## 8. Required final matrix

| Major quantity | Status after closure | Implementable without guessing? |
|---|---|---|
| Target identity | `FROZEN_DIRECT` | yes |
| Length/span/height | `FROZEN_DIRECT` + `FROZEN_DERIVED` | yes |
| 8,000-lb fuel mass/CG/inertia state | `FROZEN_DIRECT` + `FROZEN_DERIVED` | yes |
| Engine identity/count | `FROZEN_DIRECT` | yes, identity only |
| Primary control identities | `FROZEN_DIRECT` | yes |
| FCS architecture / ARI-crossfeed logic boundaries | `FROZEN_DIRECT` | yes, architecture/logic only |
| Wing area `S` | `SUPPORTED_ONLY` | **no** |
| `cbar` | `SUPPORTED_ONLY` | **no** |
| aerodynamic/structural datum | `SUPPORTED_ONLY` / `UNAVAILABLE` | **no** |
| Surface hard limits | `SUPPORTED_ONLY` family; target `UNAVAILABLE` | **no** |
| Actuator rates/dynamics | `UNAVAILABLE` | **no** |
| Baseline aero-model provenance | `FROZEN_DIRECT` | yes, provenance only |
| Baseline numeric coefficients | `UNAVAILABLE` | **no** |
| Control/rate derivative numbers | `UNAVAILABLE` | **no** |
| Mach/alpha/beta coefficient envelope | `UNAVAILABLE` | **no** |
| Full-scale high-alpha/rotary target data | `CROSS_VALIDATION_ONLY` | **no** |
| F100-PW-100 family performance sources | `SUPPORTED_ONLY` | no target deck |
| Installed target thrust deck | `UNAVAILABLE` | **no** |
| Engine transient/fuel-flow target maps | `SUPPORTED_ONLY` family; target `UNAVAILABLE` | **no** |
| Target inlet recovery | `UNAVAILABLE` | **no** |
| Engine installation geometry | `UNAVAILABLE` | **no** |
| Fuel-dependent mass schedule | `UNAVAILABLE` | **no** |

## 9. What can be implemented next without guessing?

A **documentation/profile identity skeleton** can use:

- exact target ID;
- frozen dimensions already accepted;
- the frozen 8,000-lb-fuel mass/CG/inertia state;
- primary control surface identities;
- the documented FCS architecture labels/logic boundaries;
- two exact F100-PW-100 engine slots with no authoritative thrust deck.

A real **full-scale aerodynamic implementation** is still blocked by `S`, `cbar`, reference datum
and the missing numeric coefficient database.

A real **powered implementation** is still blocked by target thrust performance and installation geometry.

## 10. Source-acquisition next move

Highest return on research time:

1. recover `MDC A4172` Part I/II + supplements from DTIC/AFRL/USAF/National Archives/Boeing
   public-release channels;
2. recover McDonnell `DN-1180.01-238-458 Rev. D`;
3. trace NASA 836 simulation source files/references through Dryden/Armstrong technical archives;
4. identify NASA 836 pre-2014 engine serial/build/control configuration and map it against public
   F100-PW-100 NASA calibration/simulation reports;
5. seek NASA 836 weight-and-balance/installation drawings for engine stations and exact datum.

## 11. Source-file/PDF handling note

NTRS citation pages and machine-readable report text were available, but the environment's PDF
viewer repeatedly failed to render several original NTRS PDF files because the server returned
403/content-type failures to the screenshot path. No new critical numeric table was frozen from OCR
alone. Ambiguous/unverified numeric tables were left unpromoted.

## 12. Production/code impact

Expected and achieved: **zero production-code changes**.

This phase is documentation/provenance only.

## 13. Recommendation

**PARTIAL — MORE RESEARCH**

The target remains coherent and defensible. The source graph is stronger, but the exact public data
needed for coefficient dimensionalization and powered flight is still incomplete.
