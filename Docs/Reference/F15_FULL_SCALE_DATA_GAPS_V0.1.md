# F-15 Full-Scale Data Gaps V0.1

Status: **R0.5 missing-data surface — no reconstruction permitted**

Selected target:

`NASA_F15B_836_SN74_0141_PRE_QUIET_SPIKE_BASELINE_F100_PW_100`

This document records what the public-source chain actually closes and what remains missing. `FAMILY_PRIOR`, `BOUNDED_ESTIMATE`, RPV scaling, F-16 borrowing, and gameplay tuning are not allowed to close any field in this phase.

Status vocabulary:

- `FROZEN`
- `DERIVED`
- `CONFIG_MATCH_PENDING`
- `CROSS_VALIDATION_ONLY`
- `UNAVAILABLE`

## 1. Data gap matrix

| FIELD | VALUE | SOURCE | AUTHORITY | CONFIDENCE | STATUS |
|---|---|---|---|---|---|
| Full-scale target identity | NASA F-15B tail 836 / USAF S/N 74-0141, pre-Quiet-Spike baseline | NTRS 20070032807; NASA 836 test-bed material | DIRECT | High | **FROZEN** |
| Research-state boundary | production-representative F-15B with NASA instrumentation; baseline before spike installation; radar/gun/ammunition systems removed in research state; air-data hardware as documented | NTRS 20070032807; NASA/TM-2012-215978 | DIRECT | High | **FROZEN** |
| Engine count | 2 | same-aircraft NASA reports | DIRECT | High | **FROZEN** |
| Engine variant | Pratt & Whitney F100-PW-100, pre-2014 state | NTRS 20070032807; NASA/TM-2005-213670; NASA test-bed page | DIRECT | High | **FROZEN** |
| Later engine state | F100-PW-220E x2 after 2014 upgrade | NTRS 20160006705 | Same airframe, different time/config | High | **CROSS_VALIDATION_ONLY** |
| Length | 63.7 ft, excluding air-data nose boom | NASA/TM-2006-213674 | DIRECT | High | **FROZEN** |
| Length SI | 19.41576 m | conversion from 63.7 ft | DERIVED | High | **DERIVED** |
| Span | 42.8 ft | NASA/TM-2006-213674 | DIRECT | High | **FROZEN** |
| Span SI | 13.04544 m | conversion from 42.8 ft | DERIVED | High | **DERIVED** |
| Height | 18.7 ft | NASA/TM-2006-213674 | DIRECT | High | **FROZEN** |
| Height SI | 5.69976 m | conversion from 18.7 ft | DERIVED | High | **DERIVED** |
| Wing reference area `S` | exact NASA 836 direct value not yet established | other F-15 NASA configurations contain 608 ft² | not direct to selected state | Medium that family value is correct; insufficient target authority | **CONFIG_MATCH_PENDING** |
| Mean aerodynamic chord `cbar` | exact NASA 836 direct value not yet established | public modified/preproduction F-15 reports contain family values | not direct to selected state | Low for direct promotion | **CONFIG_MATCH_PENDING** |
| Wing sweep numeric freeze | exact-target direct numeric citation not yet established in selected chain | family/preproduction sources | not direct | Medium | **CONFIG_MATCH_PENDING** |
| Absolute reference datum | — | no accepted target-specific source yet | none | — | **UNAVAILABLE** |
| Aerodynamic reference point / moment station | — | no accepted target-specific source yet | none | — | **UNAVAILABLE** |
| CG definition | percent mean aerodynamic chord | NASA/TM-2012-215978 | DIRECT | High | **FROZEN** |
| Reference fuel state | 8,000 lb fuel | NASA/TM-2012-215978 Table 1 context | DIRECT | High | **FROZEN** |
| Reference fuel state SI | 3,628.73896 kg | unit conversion | DERIVED | High | **DERIVED** |
| Reference weight | 37,426 lb | NASA/TM-2012-215978 Table 1, **Baseline F-15B test airplane** column | DIRECT | High | **FROZEN** |
| Reference weight SI | 165,260.329 N | unit conversion | DERIVED | High | **DERIVED** |
| Mass-equivalent | 16,976.14804 kg | conversion from 37,426 lb | DERIVED, not independent measurement | High | **DERIVED** |
| Reference CG | 26.34% MAC | NASA/TM-2012-215978 Table 1, Baseline column | DIRECT | High | **FROZEN** |
| `Ixx` | 30,345 slug-ft² | NASA/TM-2012-215978 Table 1, Baseline column | DIRECT | High | **FROZEN** |
| `Ixx` SI | 37,899.17911 kg m² | unit conversion | DERIVED | High | **DERIVED** |
| `Iyy` | 198,687 slug-ft² | NASA/TM-2012-215978 Table 1, Baseline column | DIRECT | High | **FROZEN** |
| `Iyy` SI | 258,658.88073 kg m² | unit conversion | DERIVED | High | **DERIVED** |
| `Izz` | 223,214 slug-ft² | NASA/TM-2012-215978 Table 1, Baseline column | DIRECT | High | **FROZEN** |
| `Izz` SI | 290,086.74077 kg m² | unit conversion | DERIVED | High | **DERIVED** |
| `Ixz` | -5,070 slug-ft² | NASA/TM-2012-215978 Table 1, Baseline column | DIRECT source-reported sign | High | **FROZEN** |
| `Ixz` SI | -623.676256 kg m² | unit conversion | DERIVED | High | **DERIVED** |
| Fuel-dependent mass/CG/inertia model | — | only one frozen reference state in R0.5 | none | — | **UNAVAILABLE** |
| Primary stabilator identity | paired all-moving stabilators; symmetric pitch, differential roll | NASA/TM-2012-215978; NTRS 20070028417 | DIRECT | High | **FROZEN** |
| Aileron identity | left/right ailerons | same | DIRECT | High | **FROZEN** |
| Rudder identity | twin rudders | same | DIRECT | High | **FROZEN** |
| Baseline flight-control architecture | hydromechanical/mechanical plus single-string analog/electrical CAS | NASA/TM-2012-215978 | DIRECT description | High | **FROZEN** |
| Hard control-surface travel limits | — | target-specific clean transcription not accepted yet | none | — | **UNAVAILABLE** |
| Actuator rates | — | target-specific source not frozen | none | — | **UNAVAILABLE** |
| Surface-deflection sign conventions for implementation | — | RPV signs do not transfer automatically | none | — | **UNAVAILABLE** |
| CAS/ARI gains/schedules | — | full implementation outside R0.5 | source research exists but not frozen | — | **CONFIG_MATCH_PENDING** |
| Speed-brake schedule/limits | — | no accepted target-specific implementation source | none | — | **UNAVAILABLE** |
| Flap/other configuration-device schedule | — | no accepted target-specific implementation source | none | — | **UNAVAILABLE** |
| Inlet architecture | two 2-D external-compression horizontal-ramp inlets; three horizontal ramps per inlet; bleed/bypass/variable geometry | NASA/TM-2005-213670 | DIRECT description | High | **FROZEN** |
| Inlet recovery map | — | target-specific powered map not frozen | none | — | **UNAVAILABLE** |
| Baseline aerodynamic model existence | NASA DFRC full-scale F-15B baseline simulation/model, validated/updated from four pre-spike research flights | NASA/TM-2009-214651; NASA/TM-2012-215978 | DIRECT provenance | High | **FROZEN** |
| Complete static longitudinal coefficients | numeric public target table not yet located | baseline-model references | target match plausible but data missing | — | **CONFIG_MATCH_PENDING** |
| Complete lateral-directional coefficients | same | same | target match plausible but data missing | — | **CONFIG_MATCH_PENDING** |
| Control derivatives | same | same | target match plausible but data missing | — | **CONFIG_MATCH_PENDING** |
| Rate derivatives | same | same | target match plausible but data missing | — | **CONFIG_MATCH_PENDING** |
| High-alpha coefficient authority | RPV / 0.075-scale families exist | RPV/TM-X-62360 | not full-scale target direct | Medium as trend references | **CROSS_VALIDATION_ONLY** |
| Rotary/departure/spin authority | TN-D-8052 / CR-3478 / CR-3479 families | subscale/config-specific | not direct | Medium as methodology/trend | **CROSS_VALIDATION_ONLY** |
| Transonic/supersonic same-airframe evidence | extensive NASA 836 programs including Quiet Spike/fixtures | same airframe but external research hardware/configuration changes | strong correlation context, not baseline numeric authority | High as context | **CROSS_VALIDATION_ONLY** |
| Exact baseline Mach coefficient envelope | — | numeric baseline dataset not frozen | none | — | **UNAVAILABLE** |
| Exact baseline alpha coefficient envelope | — | numeric baseline dataset not frozen | none | — | **UNAVAILABLE** |
| Exact baseline beta coefficient envelope | — | numeric baseline dataset not frozen | none | — | **UNAVAILABLE** |
| Approximate F100-PW-100 static afterburning thrust point | NASA same-aircraft reports quote approximately 23,500-25,000 lbf class depending on document | NASA 836 reports | contextual public reference only; not a deck | Medium | **CROSS_VALIDATION_ONLY** |
| Engine thrust deck vs Mach/altitude | — | exact target engine/config dataset not frozen | none | — | **UNAVAILABLE** |
| Engine transient/spool model | — | exact F100-PW-100 target source not frozen | none | — | **UNAVAILABLE** |
| Engine fuel flow | — | exact target source not frozen | none | — | **UNAVAILABLE** |
| Left engine installation position | — | exact target source not located | none | — | **UNAVAILABLE** |
| Right engine installation position | — | exact target source not located | none | — | **UNAVAILABLE** |
| Thrust direction / thrust-line coordinates | — | exact target source not located | none | — | **UNAVAILABLE** |
| Two-slot propulsion architecture compatibility | LEFT + RIGHT slots may share one exact F100-PW-100 profile asset; runtime state independent | existing Maverick shared propulsion architecture | architectural | High | **FROZEN** |
| RPV coefficient trends | may be used only for normalized trend/methodology comparison | R0 RPV source chain | supporting only | High for policy | **CROSS_VALIDATION_ONLY** |
| RPV dimensional values | prohibited from filling full-scale fields | transfer policy | incompatible as direct authority | High | **UNAVAILABLE** for full-scale use |

## 2. Highest-priority unresolved gaps

### GAP-F15FS-001 — exact target `S` and `cbar`

These are required before a physical aerodynamic profile can dimensionalize coefficients cleanly. They must come from a source explicitly compatible with NASA 836 or be promoted only after a documented configuration-match proof.

R0.5 does not scale the 3/8 RPV and does not import a value from NASA 837, preproduction #8, or a generic F-15 handbook without identity review.

### GAP-F15FS-002 — full-scale baseline coefficient database

The NASA 836 reports prove that a baseline aerodynamic model existed and was flight-validated/updated. The complete numeric database has not yet been established as a public direct source.

Required future work:

- trace the underlying references used by NASA/TM-2009-214651 and NASA/TM-2012-215978;
- distinguish original baseline model, flight-update increments, and spike deltas;
- recover numeric static/control/rate data only when the exact pre-spike configuration is clear.

### GAP-F15FS-003 — full-scale reference datum

The absolute aircraft datum, aerodynamic reference station, and a source-compatible way to place the 26.34%-MAC CG in meters are not frozen.

Until this is closed, installation coordinates and moment-reference conversions cannot be claimed authoritative.

### GAP-F15FS-004 — controls limits/rates/signs

Primary surface identities are known, but implementation-level travel limits, actuator rates, and deflection sign conventions remain open.

Do not borrow F-16 rates or RPV control limits.

### GAP-F15FS-005 — F100-PW-100 thrust deck

The exact engine variant is now closed. The numeric thrust model is not.

Needed:

- target-compatible F100-PW-100 thrust/performance source;
- Mach/altitude/power definition;
- installation versus uninstalled thrust boundary;
- afterburner semantics;
- uncertainty/validity envelope.

A quoted approximate sea-level static thrust is not sufficient.

### GAP-F15FS-006 — engine transient/fuel/inlet coupling

No target-compatible transient model, fuel-flow map, or inlet-recovery schedule is frozen.

NASA F100 research on development engines, DEEC, PW1128, HIDEC, and other exact engine serials may become methodology or configuration-specific supporting data, but cannot be silently relabeled F100-PW-100 baseline behavior.

### GAP-F15FS-007 — engine installation geometry

Left/right engine position vectors and thrust-line vectors relative to the declared physics datum are unavailable. The existing shared propulsion architecture must continue to fail closed for authoritative live powered use until these are sourced.

### GAP-F15FS-008 — baseline coefficient envelope

Aircraft capability and experiment envelopes do not automatically define coefficient validity. Mach/alpha/beta bounds for the eventual baseline dataset must come from the actual data source.

## 3. Explicitly rejected gap fillers

R0.5 rejects the following shortcuts:

- RPV geometry scaling;
- RPV mass/inertia scaling;
- RPV coefficient direct-copy;
- F-16 actuator or propulsion data;
- generic `F100` numbers;
- F100-PW-220E values from post-2014 NASA 836;
- PW1128/HIDEC values from NASA 835;
- F100-PW-229/ACTIVE values from NASA 837;
- F-15E gameplay tuning;
- generic F-15C numbers without an exact aircraft/configuration identity;
- modified three-surface/canard aerodynamic values;
- CFT/store values for the clean/pre-spike baseline.

No `FAMILY_PRIOR` or `BOUNDED_ESTIMATE` is introduced in this phase.

## 4. Promotion rules

A gap may move to `FROZEN` only when the future source record answers the relevant identity questions:

1. exact aircraft/serial or demonstrably production-identical configuration;
2. date/research modification state;
3. stores/CFT/test-article state;
4. inlet/control configuration;
5. engine variant where propulsion is involved;
6. mass/CG state where dynamics are involved;
7. axes/sign/reference definitions;
8. validity envelope;
9. raw units and conversion path;
10. whether the datum is measured, modeled, estimated, or flight-derived.

## 5. R0.5 gap verdict

The missing-data surface is substantial but now explicit.

The target identity, exact engine variant, dimensions, one mass/CG/inertia state, primary controls, inlet architecture, and baseline-flight-correlation provenance are strong enough to select and freeze the aircraft.

The remaining gaps are intentionally left open for later source recovery/reconstruction phases rather than being hidden behind mixed-configuration numbers.
