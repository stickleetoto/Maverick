# F-15 RPV to Full-Scale Transfer Policy V0.1

Status: **FROZEN POLICY — R0.5**

This policy defines what the frozen 3/8-scale F-15 RPV research anchor may and may not contribute to the selected full-scale Maverick F-15 target.

## 1. Identities kept separate

RPV anchor:

`NASA_F15_RPV_3_8_BASIC1_CG26_BLOCKED_INLETS`

Full-scale target:

`NASA_F15B_836_SN74_0141_PRE_QUIET_SPIKE_BASELINE_F100_PW_100`

These are **not the same configuration at different sizes** for authority purposes.

The RPV is an unpowered, blocked-inlet, dynamically scaled research vehicle in its own Basic 1 / 26%-MAC state. The full-scale target is a powered NASA F-15B research test bed, S/N 74-0141, with F100-PW-100 engines and its own instrumentation/modification state.

No datum crosses this boundary automatically.

## 2. Core rule

A transfer from RPV to full scale is allowed only when the quantity is being used as:

- `METHODOLOGY_ONLY`, or
- `CROSS_VALIDATION_ONLY`.

An RPV datum does **not** become `DIRECT` full-scale authority through similarity, dynamic scaling, apparent geometric agreement, or lack of a better source.

If a full-scale field is missing, the field remains `UNAVAILABLE` or `CONFIG_MATCH_PENDING` in R0.5.

## 3. Allowed uses

### 3.1 Dimensionless aerodynamic trend cross-check

Allowed examples:

- sign and slope trend of a stability derivative over a bounded comparable regime;
- qualitative variation of control effectiveness with angle of attack;
- relative onset/reversal trends;
- comparison of normalized force/moment coefficient behavior;
- comparison of damping/stability trends when the definitions, axes, configuration state, and nondimensionalization are explicit.

Required label: `CROSS_VALIDATION_ONLY` unless a future configuration-specific analysis proves a stronger relation.

### 3.2 Stability/control trend cross-check

The flight-derived RPV derivative family may be used to ask questions such as:

- does the full-scale target model show the same sign of a major stability trend?
- does control effectiveness degrade or change character in a similar high-alpha region?
- do independently sourced full-scale derivatives violate obvious family-level behavior?

It may **not** provide the full-scale derivative number by default.

### 3.3 Sign/convention verification

The RPV source chain may help detect sign mistakes when:

1. both source conventions are explicitly mapped;
2. the same physical quantity is being compared;
3. the comparison is used as a diagnostic, not as the source of the full-scale sign definition.

Aircraft-specific control-deflection signs still require full-scale target evidence before implementation.

### 3.4 High-alpha and departure methodology

Allowed:

- identify useful maneuver types;
- identify which variables/derivatives should be monitored;
- compare qualitative departure behavior;
- guide future rotary/high-alpha validation design;
- use RPV spin/departure work as a methodology and trend oracle.

The RPV high-alpha program is particularly valuable because the full-scale target's public baseline coefficient database is incomplete.

### 3.5 Normalized comparison

A future validation tool may compare nondimensionalized quantities between RPV and full scale if it preserves:

- exact configuration tags;
- axes/sign conventions;
- nondimensional definitions;
- Mach/Reynolds/alpha/beta context;
- control state;
- CG state;
- source uncertainty.

The result must remain explicitly cross-validation unless a separate scaling argument is reviewed and approved.

## 4. Forbidden transfers

The following are forbidden in R0.5 and may not be used to fill full-scale gaps.

### 4.1 Dimensional geometry

Do not copy or scale RPV:

- wing area;
- span;
- mean aerodynamic chord;
- length;
- absolute reference datum;
- surface dimensions;
- control hinge/station geometry.

No `3/8 -> full-scale` multiplication is accepted as authoritative geometry.

### 4.2 Mass and inertia

Do not copy, scale, or reconstruct the full-scale target from RPV:

- mass/weight;
- CG;
- `Ix`;
- `Iy`;
- `Iz`;
- `Ixz`;
- fuel-state mass distribution.

The selected full-scale target already has an independent, direct 8,000-lb-fuel mass/inertia state. Other states must be sourced separately.

### 4.3 Propulsion/inlet data

The RPV is unpowered with blocked inlets. Therefore it supplies **no direct authority** for:

- F100 engine identity;
- thrust;
- spool/transient behavior;
- fuel flow;
- installed thrust loss;
- inlet recovery;
- engine installation coordinates;
- one-engine-out behavior.

### 4.4 Automatic coefficient authority

Do not directly install an RPV coefficient or derivative into the full-scale target merely because:

- both aircraft are called F-15;
- the coefficient is dimensionless;
- geometry looks similar;
- the full-scale value is unavailable;
- the resulting handling appears plausible.

Dimensionless does not mean configuration-independent.

### 4.5 Control limits/rates/laws

Do not transfer RPV:

- hard surface limits;
- actuator rates;
- augmentation gains;
- control mixing;
- schedules;
- pilot-command scaling.

The RPV remote augmentation system is a separate research control architecture.

### 4.6 Gap filling

The RPV must not be used as an undeclared `FAMILY_PRIOR`, `BOUNDED_ESTIMATE`, or gameplay-tuning source in R0.5. Those reconstruction mechanisms are explicitly deferred to a later phase.

## 5. Authority decision table

| Proposed RPV use | R0.5 decision | Maximum authority |
|---|---|---|
| Coefficient trend versus alpha | Allowed with matching definitions/config context | `CROSS_VALIDATION_ONLY` |
| Stability/control sign sanity check | Allowed | `CROSS_VALIDATION_ONLY` / `METHODOLOGY_ONLY` |
| High-alpha maneuver/test methodology | Allowed | `METHODOLOGY_ONLY` |
| Departure/spin qualitative comparison | Allowed | `CROSS_VALIDATION_ONLY` |
| Nondimensional derivative comparison | Allowed after axes/nondimensional mapping | `CROSS_VALIDATION_ONLY` |
| Full-scale `S`, `b`, `cbar`, length | Forbidden | none |
| Full-scale mass/CG/inertia | Forbidden | none |
| F100 data | Forbidden | none |
| Full-scale control limits or actuator rates | Forbidden | none |
| Direct copy of coefficient table | Forbidden by default | none |
| Fill any `UNAVAILABLE` field because the RPV has a value | Forbidden | none |

## 6. Promotion gate for any future stronger transfer

A future phase may promote an RPV-derived comparison only after documenting all of the following:

1. the exact RPV state and full-scale target state;
2. geometry differences relevant to the quantity;
3. inlet/stores/CFT/control-state differences;
4. Mach, Reynolds number, alpha, beta and rate regime;
5. CG/mass-property relevance;
6. axes and sign mapping;
7. nondimensionalization convention;
8. scale/Reynolds/dynamic-similarity argument;
9. independent full-scale evidence used to validate the proposed transfer;
10. uncertainty/bounds.

Even then, the source provenance must show that the value originated from the RPV. It must not be relabeled as measured full-scale data.

## 7. Validation rules

For every future F-15 data import or test:

- configuration tags are mandatory;
- no `DIRECT` full-scale field may cite only an RPV source;
- a validator should reject RPV mass/inertia/geometry/engine provenance for the full-scale target;
- numeric comparisons should retain source units and nondimensional definitions;
- any RPV coefficient used in a regression test must be named as a cross-validation oracle, not a full-scale golden value.

## 8. Policy conclusion

The RPV remains extremely valuable because it gives Maverick an independent, flight-tested F-15-family research anchor for subsonic stability/control and high-alpha/departure work.

Its value is **scientific comparison**, not permission to manufacture missing full-scale data.
