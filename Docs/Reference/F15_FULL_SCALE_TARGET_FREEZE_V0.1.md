# F-15 Full-Scale Target Freeze V0.1

Status: **FROZEN — full-scale target identity and profile boundary only**

This document selects the first full-scale F-15 reference aircraft for Maverick. It does not implement aerodynamic coefficients, propulsion, flight-control laws, or gameplay tuning.

## 1. Frozen target identity

Canonical configuration tag:

`NASA_F15B_836_SN74_0141_PRE_QUIET_SPIKE_BASELINE_F100_PW_100`

Human-readable identity:

> NASA Dryden F-15B research test bed, NASA tail number 836, USAF serial number 74-0141, in the pre-Quiet-Spike baseline configuration used for baseline aerodynamic-model validation flights, equipped with two Pratt & Whitney F100-PW-100 engines.

This is a **specific full-scale research test-bed state**, not an anonymous `F-15B`, `F-15C`, or generic production Eagle.

Primary identity/configuration sources:

- NASA/TM-2012-215978, NTRS 20120013435 — baseline mass/inertia, control-system description, production-outer-mold-line boundary, and pre-spike aerodynamic-model validation.
- NASA/TM-2009-214651, NTRS 20090034255 — validated baseline aerodynamic model and four research flights before spike installation.
- Quiet Spike flight-test paper, NTRS 20070032807 — production-representative F-15B, USAF S/N 74-0141, production F100-PW-100 engines, and baseline-flight aircraft details.
- NASA/TM-2005-213670, NTRS 20050241960 — F-15B research-test-bed configuration, F100-PW-100 engines, and inlet-system description.
- NASA/TM-2006-213674, NTRS 20060022548 — full-scale dimensions, controls architecture, engines, and research-test-bed modifications.
- NASA F-15B Aeronautics Research Test Bed page — NASA tail 836 identity and research-test-bed context.

Canonical source links:

- https://ntrs.nasa.gov/citations/20120013435
- https://ntrs.nasa.gov/citations/20090034255
- https://ntrs.nasa.gov/citations/20070032807
- https://ntrs.nasa.gov/citations/20050241960
- https://ntrs.nasa.gov/citations/20060022548
- https://www.nasa.gov/aeronautics/f-15b-test-bed/

## 2. Why this target was selected

The target wins because a single identified full-scale aircraft provides an unusually coherent public chain for the fields required at this phase:

- exact research identity: NASA 836 / USAF S/N 74-0141;
- NASA describes the Quiet Spike test aircraft as **production representative**;
- exact pre-2014 engine variant: two F100-PW-100 engines;
- full-scale dimensions from NASA reports on the same research test bed;
- a configuration-specific baseline mass, CG, and full inertia row at a stated 8,000-lb fuel state;
- physical primary control-surface identities and the production control-system architecture;
- four pre-spike research flights explicitly used to validate/update the baseline F-15B aerodynamic model;
- later subsonic/transonic/supersonic flight work on the same airframe that can be retained as configuration-tagged cross-validation.

The selected aircraft is not completely stock. That is acceptable because the modifications are publicly bounded and can be represented explicitly instead of silently mixing several production blocks or research aircraft.

## 3. Absolute configuration boundary

The R0.5 target means the aircraft state immediately relevant to the **baseline flights before Quiet Spike installation**.

### Included identity/configuration facts

- full-scale two-seat F-15B;
- NASA tail number 836;
- USAF S/N 74-0141;
- production-representative airframe;
- two F100-PW-100 engines;
- NASA research instrumentation/data acquisition/telemetry state;
- radar, gun, ammunition drum/feed system removed in the Quiet Spike research state;
- instrumentation pallet in the former ammunition-drum volume;
- baseline-flight YAPS air-data nose boom;
- supplemental sideslip vane under the nose and its fairing;
- fiberglass fairing/covered gun-port state described by the NASA reports;
- no Quiet Spike assembly installed for the direct baseline-aerodynamic state;
- no external centerline research fixture accepted as direct authority for the frozen baseline.

### Explicitly not this target

- generic production F-15A/B/C/D;
- F-15E or F-15EX;
- preproduction F-15 #8 / S/N 71-0287;
- HIDEC/DEEC/PW1128 state of NASA 835;
- PCA state;
- NASA NF-15B 837 / S/N 71-0290;
- ACTIVE/IFCS, canards, thrust-vectoring nozzles, or F100-PW-229 state;
- NASA 836 after the 2014 F100-PW-220E engine upgrade;
- Quiet-Spike-attached configuration;
- FTF-II, PFTF, CLIP, LIFT, channeled-inlet, or other centerline experiment configuration;
- CFT or external-store aerodynamic configuration;
- RPV or wind-tunnel model as direct full-scale authority.

## 4. Geometry freeze

Only values directly supported for the NASA F-15B research test bed are frozen here.

| Field | Raw source value | SI | Authority/status |
|---|---:|---:|---|
| Aircraft length, excluding air-data nose boom | 63.7 ft | **19.41576 m** | FROZEN — NASA/TM-2006-213674 |
| Wingspan | 42.8 ft | **13.04544 m** | FROZEN — NASA/TM-2006-213674 |
| Height | 18.7 ft | **5.69976 m** | FROZEN — NASA/TM-2006-213674 |
| Reference wing area, `S` | UNAVAILABLE for exact 836 baseline authority | UNAVAILABLE | CONFIG_MATCH_PENDING |
| Mean aerodynamic chord, `cbar` | UNAVAILABLE for exact 836 baseline authority | UNAVAILABLE | CONFIG_MATCH_PENDING |
| Wing leading-edge sweep | UNAVAILABLE as an exact-target frozen numeric field | UNAVAILABLE | CONFIG_MATCH_PENDING |
| Absolute aircraft reference datum | UNAVAILABLE | UNAVAILABLE | UNAVAILABLE |
| Absolute aerodynamic reference point | UNAVAILABLE | UNAVAILABLE | UNAVAILABLE |
| Reference CG definition | percent mean aerodynamic chord | percent MAC | FROZEN — NASA/TM-2012-215978 |

The well-known 608-ft² / approximately 15.94-ft MAC F-15-family values occur in public NASA material for other full-scale research configurations, including preproduction/modified aircraft. They are **not promoted to direct authority for NASA 836 in R0.5** merely because the values appear plausible.

No geometry is scaled from the 3/8 RPV.

## 5. Frozen reference mass / CG / inertia state

The selected reference state is the **Baseline F-15B test airplane at 8,000 lb fuel** in NASA/TM-2012-215978 Table 1.

Configuration-state tag:

`NASA_F15B_836_BASELINE_8K_FUEL_MASS_STATE`

| Field | Raw source value | SI conversion | Status |
|---|---:|---:|---|
| Fuel state | 8,000 lb | **3,628.73896 kg** | FROZEN raw state; SI DERIVED |
| Weight | 37,152 lb | **165,260.329 N** | FROZEN raw; SI DERIVED using 4.4482216152605 N/lbf |
| Mass-equivalent | derived from 37,152 lb | **16,851.86373 kg** | DERIVED; not a separately published mass |
| CG | **26.05% MAC** | same | FROZEN |
| `Ixx` | **27,953 slug-ft²** | **37,899.17911 kg m²** | FROZEN raw; SI DERIVED |
| `Iyy` | **190,777 slug-ft²** | **258,658.88073 kg m²** | FROZEN raw; SI DERIVED |
| `Izz` | **213,957 slug-ft²** | **290,086.74077 kg m²** | FROZEN raw; SI DERIVED |
| `Ixz` | **-460 slug-ft²** | **-623.676256 kg m²** | FROZEN raw reported sign; SI DERIVED |

Conversion used for inertia: `1 slug-ft² = 1.3558179483314 kg m²`.

The published `Ixz` sign is preserved as source data. A later implementation must map product-of-inertia sign into Maverick's exact rigid-body tensor convention explicitly; R0.5 does not modify that convention.

This table is not a generic F-15B mass model. It is one source-defined reference state. Fuel-dependent interpolation or empty/full mass reconstruction is outside R0.5.

### Consistency checks

- all diagonal inertias are positive;
- `Ixx*Izz - Ixz² = 5,980,528,421 > 0` in the source unit system;
- the corresponding symmetric inertia matrix is positive definite for the reported values;
- no RPV inertia or other F-15 airframe inertia is used to fill this state.

## 6. Control-surface and flight-control boundary

Frozen physical primary control identities for NASA 836:

- left/right stabilators;
  - symmetric deflection for pitch;
  - differential deflection for roll;
- ailerons;
- twin rudders.

NASA/TM-2012-215978 describes the baseline control system as an integrated mechanical and electrical, single-string analog control augmentation system operating in parallel. The production aircraft control system was not changed for Quiet Spike.

R0.5 does **not** freeze:

- hard travel limits;
- actuator rates;
- target-specific surface sign convention suitable for software import;
- ARI/CAS gains or schedules;
- flap/speed-brake schedules;
- any leading-edge-device schedule;
- a Maverick flight-control-law implementation.

Those remain `UNAVAILABLE` or `CONFIG_MATCH_PENDING` until a target-specific primary source is transcribed cleanly.

## 7. Inlet and engine identity

Frozen propulsion identity:

- engine count: **2**;
- engine model: **Pratt & Whitney F100-PW-100**;
- aircraft state: pre-2014 NASA 836 configuration;
- inlet family: two two-dimensional external-compression horizontal-ramp inlets, three horizontal ramps per inlet, with variable geometry/bleed/bypass features described in NASA/TM-2005-213670.

The NASA 836 capability material explicitly records an upgrade to two F100-PW-220E engines in 2014. That later state is a distinct configuration and cannot be silently combined with this target.

### Not frozen

- thrust deck;
- installed thrust as a function of Mach/altitude/power;
- spool/transient model;
- fuel flow;
- inlet-recovery schedule;
- engine mount coordinates;
- thrust-line coordinates/directions beyond generic aircraft symmetry;
- rotor angular momentum.

NASA reports quote approximate sea-level static afterburning thrust values for the F100-PW-100, but R0.5 does not promote those approximate single-point figures into an authoritative thrust deck.

## 8. Shared propulsion architecture compatibility

No shared propulsion code changes are required.

Future target profile shape:

```text
AircraftProfile: NASA_F15B_836_SN74_0141_PRE_QUIET_SPIKE_BASELINE_F100_PW_100
  |
  +-- Engine installation LEFT  -> F100-PW-100 EngineProfile
  +-- Engine installation RIGHT -> same exact F100-PW-100 EngineProfile
```

Both slots may reference the same engine-profile asset because the frozen target identifies the same exact engine variant on both sides. Runtime state remains independent for left and right engines.

Installation coordinates remain `UNAVAILABLE`, so a live powered profile must fail closed until measured/sourced geometry is added according to the existing shared architecture.

## 9. Aerodynamic source plan

No aerodynamic coefficients are implemented or frozen in R0.5.

| Future data need | Source family | R0.5 authority |
|---|---|---|
| Baseline full-scale aero model existence/correlation | NASA/TM-2009-214651 + NASA/TM-2012-215978, four pre-spike flights | **DIRECT** for existence and flight-correlation provenance; coefficient tables still unavailable |
| Static longitudinal / lateral-directional coefficients | NASA 836 baseline simulation/model references in Quiet Spike reports | **CONFIG_MATCH_PENDING** until underlying numeric model/source is located |
| Control derivatives | same NASA 836 baseline model chain | **CONFIG_MATCH_PENDING** |
| Rate derivatives | same NASA 836 baseline model chain | **CONFIG_MATCH_PENDING** |
| Subsonic flight trend cross-check | Quiet Spike flight/parameter-estimation results | **CROSS_VALIDATION_ONLY** when spike attached; baseline pre-spike flights are direct correlation evidence |
| Transonic/supersonic trend cross-check | NASA 836 Quiet Spike / FTF/PFTF programs | **CROSS_VALIDATION_ONLY** when external research hardware is installed |
| High-alpha trends | frozen 3/8 RPV chain / TM-X-62360 | **CROSS_VALIDATION_ONLY** |
| Rotary/departure/spin | TN-D-8052, CR-3478, CR-3479 | **CROSS_VALIDATION_ONLY** |
| Preproduction full-scale handling | TM-72861 | **CROSS_VALIDATION_ONLY** |
| Three-surface / ACTIVE transonic data | modified research-aircraft sources | **INCOMPATIBLE** for direct target coefficients |

The existence of a validated NASA 836 baseline simulation is strong evidence for target selection, but it is **not equivalent to possession of a public coefficient database**.

## 10. Validity/envelope status

The exact complete aerodynamic implementation envelope is **UNAVAILABLE** because the numeric baseline coefficient database is not frozen.

Evidence available for future bounded validation includes:

- pre-spike baseline flights used to update/validate the NASA 836 simulation;
- same-airframe research across subsonic, transonic, and supersonic regimes;
- Quiet Spike flight work to Mach 1.8, but that external-article state is not direct baseline coefficient authority;
- NASA 836 general capability well above Mach 2, which is aircraft capability context, not a validated Maverick coefficient envelope.

R0.5 therefore does not declare a full Mach/alpha/beta coefficient envelope.

## 11. RPV relationship

The frozen R0 anchor remains:

`NASA_F15_RPV_3_8_BASIC1_CG26_BLOCKED_INLETS`

It is retained as a separate research anchor only. Transfer rules are defined in:

`Docs/Reference/F15_RPV_TO_FULL_SCALE_TRANSFER_POLICY_V0.1.md`

No RPV dimensional geometry, mass, inertia, engine data, control limits, or coefficients become direct full-scale authority merely by geometric similarity.

## 12. Profile/code decision

**No production profile code is added in R0.5.**

Reason: the identity and one mass/inertia state are strong enough to select the target, but `S`, `cbar`, installation geometry, complete control limits, and aerodynamic numeric data are still intentionally open. Creating a populated physical profile now would pressure the project to fill those gaps with guesses.

The existing F-15 propulsion skeleton remains unchanged.

## 13. What is frozen

- exact full-scale target identity and configuration tag;
- NASA tail / USAF serial identity;
- pre-Quiet-Spike baseline research-state boundary;
- exact engine variant and engine count;
- target length/span/height;
- 8,000-lb-fuel reference weight/CG/inertia state;
- primary control-surface identities;
- inlet family description;
- source graph and incompatible-configuration boundaries.

## 14. What is not frozen

- production F-15C/F-15E identity;
- wing reference area or MAC for exact NASA 836 authority;
- absolute datum/aerodynamic reference station;
- complete flight-envelope coefficient tables;
- high-alpha/rotary coefficients;
- control travel/rates/signs for implementation;
- FLCS/CAS/ARI numeric law;
- engine thrust/transient/fuel-flow deck;
- engine installation coordinates;
- any gameplay tuning.

## 15. Freeze verdict

**F15 FULL-SCALE R0.5: FROZEN**

Meaning: Maverick now has one exact full-scale F-15 target to build toward, while the unresolved numeric fields remain visibly unavailable instead of being filled from incompatible F-15 or RPV configurations.
