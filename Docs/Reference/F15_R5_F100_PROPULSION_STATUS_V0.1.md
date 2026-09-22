# F-15 R5 — F100-PW-100 propulsion implementation status

**Status:** V0.1.
**Target aircraft:** `NASA_F15B_836_SN74_0141_PRE_QUIET_SPIKE_BASELINE_F100_PW_100`
**Target engine:** Pratt & Whitney F100-PW-100 × 2.
**Source audit:** `F15_R5_F100_SOURCE_AUDIT_V0.1.md` — read that first; this document assumes it.

---

## 1. Where this pass got to, in one paragraph

The **shape** of F100 net thrust over the whole F-15 envelope is now implemented, tested and
traceable to a figure: 63 digitized points across seven documented flight conditions from sea level
to 17.83 km and Mach 0 to 2.2, with envelope enforcement that refuses to interpolate between
scattered test points. The **scale** is not implemented, because no document in the R5 pack prints
it. The deck therefore produces **no newtons**, names both of its blockers, and the aircraft does
not move. Closing the scale is a declaration of one scalar, not a rewrite.

---

## 2. Field-level status

| field | status | source / provenance |
|---|---|---|
| engine identity | **SOURCE-BACKED** | F100-PW-100 × 2, frozen in an earlier pass. AuthoritativeExactTarget |
| engine configuration represented | **SOURCE-BACKED** | F100-PW-100**(3)** for the thrust characteristic; prototype series 2 7/8 for the control schedules. Both CompatibleSupport, neither the target build |
| steady non-augmented thrust | **SOURCE-BACKED (shape only)** | TP-1034 fig. 17, PLA 20–83°, 4 subsonic conditions. Fraction of design max |
| augmented thrust | **SOURCE-BACKED (shape only)** | TP-1034 fig. 17, PLA 83–130°, all 7 conditions |
| Mach dependence | **SOURCE-BACKED (at 7 points only)** | Mach 0, 0.9, 1.8, 2.15, 2.2. No interpolation between them |
| altitude dependence | **SOURCE-BACKED (at 7 points only)** | 0, 3.048, 6.096, 9.144, 12.19, 13.72, 17.83 km |
| **absolute thrust scale** | **BLOCKED** | design maximum net thrust is printed in none of the four documents |
| airflow | **UNAVAILABLE** (one datum recorded) | design corrected airflow 98.4 kg/s from TP-1373; no schedule, no map |
| fuel flow | **UNAVAILABLE** | an *input* to every model in the pack, never an output of a published schedule |
| transient / spool dynamics | **BLOCKED** | rotor inertias printed; turbine and fan torque come from unpublished maps, so no time constant follows |
| nozzle model | **UNAVAILABLE** (mode switch only) | area-ratio mode switch at Mach 1.1 implemented; discharge and velocity coefficients need the maps |
| inlet effects | **PARTIAL, labelled** | TM X-3261 (B3) recovery implemented and labelled **typical**, not F-15. Distortion unavailable — TP-1373 declined to derive a correction |
| installation geometry | **UNAVAILABLE** | `geometryDeclared = false`, unchanged from the previous pass |
| gross / net thrust semantics | **SOURCE-BACKED** | TM X-3261 (B52) / TP-1034 (B56), coefficient verified as √(γR) |
| installed thrust | **UNAVAILABLE** | everything in the pack is uninstalled |
| source envelope | **SOURCE-BACKED** | the 7 documented points, enforced; off-point queries refused |

---

## 3. What was implemented

### Layer A — source-backed steady thrust capability

`MavF100SourceData` holds the extracted source numbers and nothing else: no evaluation, no
interpolation, no policy. That separation is a requirement of the R5 brief for digitized figure
data, and it means the digitized points can be re-checked against the page without reading any
logic.

`MavF100NormalizedNetThrustModel` evaluates them. It answers in **fraction of design maximum net
thrust**, and is explicit that this is not a force.

### Layer B — source envelope

Queries are matched to a documented operating point within ±150 m and ±0.02 Mach. Off a documented
point, the model returns `OutsideSourceSupport` and **no number**.

**There is deliberately no interpolation between flight conditions.** The seven points are not a
grid — four of the seven differ in altitude *and* Mach simultaneously. Interpolating between, say,
9.144 km at Mach 0.9 and 6.096 km at Mach 1.8 would require assuming how thrust splits between the
two variables across the transonic region, which is exactly the physics such data would be needed to
establish. Any scheme would produce smooth, plausible, invented numbers that nothing downstream
could distinguish from the measured ones.

Interpolation **along power lever angle** is different and is performed: figure 17 draws a
continuous curve through its markers at each condition, so the source itself asserts the intermediate
values exist.

### Layer C — augmentation

Represented exactly as far as the source goes, and no further. The curves span PLA 20–130°, which
covers idle through maximum augmentation, and figure 17(a) shows the augmentation plateau near 83°.
What is **not** implemented is a burner-light condition, an augmentor transition law, or a zone
sequencing model: no document gives any of them. `MavEngineAugmentationSemantics` stays
`Unavailable`, and `[E13]` asserts it.

### Layer D — transient dynamics

**Not implemented, and the blocker is recorded rather than filled with a guessed first-order lag.**

TM X-3261 and TP-1034 both print transient-capable models, and their table I gives real rotor
inertias (I_H = 515.2, I_L = 610.0 N·cm·s²). But the angular-momentum equations (B77–B85) need
turbine and fan torque, which come from the component maps, which are published as graphs and read
from data cards. No spool time constant follows from what is printed.

The profile keeps `MavEnginePowerDynamicsLaw.InstantNoSourcedTransient` — declared absence, not a
modelled transient. The F-16 Garza/Morelli law describes a different engine and is not borrowed;
`[E5]` asserts that it is not selected.

### Section 7 of the brief — gross vs net

`MavF100ThrustSemantics` implements the one dimensional thrust calculation the pack fully supports:

> F_net = F_gross − 20.041 · w₂ · M₀ · √T₀

Every thrust value in this layer travels as a `MavF100ThrustValue` carrying its own
`MavF100ThrustQuantity`, so gross, ram drag, uninstalled net and installed net cannot be silently
interchanged. Handing a value already labelled net to the gross→net transform is **refused**, not
computed — that mistake subtracts the inlet momentum twice and produces an entirely plausible
number. `[E11]` covers it.

### Twin-engine ownership

Unchanged and still correct: two slots, two independent runtimes, two throttle channels, one shared
immutable profile object. Propulsion writes no Rigidbody; `[E15]` scans the source tree for it, and
scans the aerodynamic sources in the other direction to confirm the aero model has not grown a
thrust term.

---

## 4. The two blockers, and why both are reported together

`MavF100ThrustDeck.Evaluate` returns `Unavailable` and zero thrust, every time, naming:

1. **SCALE.** `MavF100SourceData.DesignMaximumNetThrust` is undeclared. Its *definition* is pinned
   exactly — uninstalled net thrust at sea level, Mach 0, PLA 130° — because figure 17(a) reaches
   1.0 there. Its *value* is printed in none of the four documents.

2. **POWER LEVER.** No R5 source relates a power lever angle to an engine power state. TP-1034
   plots thrust against PLA and names idle at 20° and maximum at 130°; the curve between them is an
   interface convention that somebody must own and declare.

Both are reported together rather than one at a time, so that closing the first does not create the
impression that thrust is one step away when it is two. `[E14]` asserts each blocks independently,
and that a scale declared **without a citation** or with a non-positive value is still refused —
that being precisely how an invented number would enter a sourced deck.

### Two specific numbers that must not be used to close it

**111.2 kN.** TP-1373's plot-axis scale for gross thrust, and reported by the user to be described
in TP-1228 as an arbitrarily chosen nominal *corrected* gross-thrust normalization. Wrong thrust
quantity, and a nominal scale rather than a design value. `[E17]` holds the prohibition, and records
the TP-1228 statement as unverified here since that report is not held. Full reasoning in §6a of the
source audit.

**Any thrust measured on engines P680059 or P680063.** There is a sound route from a static gross
thrust to this denominator — ram drag is identically zero at Mach 0, so gross and uninstalled net
coincide, and figure 17(a) is a static panel. But the two calibration reports are *altitude*
calibrations whose test matrices, reproduced in TP-1373 table 3, contain **no static point** at all;
the lowest condition either engine ran is Mach 0.80 at 4 020 m. The chain fails before configuration
compatibility is even reached. §6b of the source audit works through all three gates;
`MavF100DimensionalAnchor` encodes them as executable refusals.

### Do not close the scale with a general-specification F100 figure either

A published "F100-PW-100 sea-level static thrust" number from a spec sheet would be a different
engine build, at a different rating, on a different installation, and quite possibly installed
rather than uninstalled. It would silently rescale all 63 operating points, and every number
downstream would look sourced. The value needed is specifically the normalizer of TP-1034 figure 17.

---

## 5. Tests

Run in **Unity 6000.3.16f1 headless** through `MavFdmValidationBatchAdapter`.

| suite | result |
|---|---|
| `MavF15PropulsionValidation` | **111 passed, 0 failed** (was 27) |
| `MavF15BaumannTranscriptionValidation` | **28 passed, 0 failed** |
| `MavF15ControlPathValidation` | **79 passed, 0 failed** |
| **total** | **218 passed, 0 failed** |

311 runtime scripts compile with 0 errors.

New sections, covering every item the R5 brief's §9 lists:

| section | covers |
|---|---|
| `[E7]` | digitized data integrity; distinct conditions; the figure's own 0.004 accuracy self-check |
| `[E8]` | source-point regression: all **63** digitized points evaluate back to their own values |
| `[E9]` | envelope enforcement; zero silent extrapolation; clamp-vs-refuse behaviour |
| `[E10]` | finite outputs across a 7 × 41 sweep; NaN/∞ inputs refused |
| `[E11]` | gross / ram drag / net separation; double-subtraction refused; provenance not laundered |
| `[E12]` | EEC minimum power schedule; nozzle mode switch; inlet recovery and stagnation ratios |
| `[E13]` | augmentation boundaries; the two power lever conventions kept distinct |
| `[E14]` | the deck refuses to produce a force, and each blocker blocks alone |
| `[E15]` | propulsion writes no Rigidbody; aero sources add no engine thrust |
| `[E16]` | TP-1782 stays cross-validation only and is not standing in as the source envelope |
| `[E17]` | 111.2 kN is a plot scale and cannot become the design-maximum denominator |
| `[E18]` | the sea-level-static anchor: mechanism sound, three gates, fails at the first |
| `[E19]` | normalized net and dimensional gross remain separate datasets |

**NOT RUN:** Unity play mode; any flight, trim or trajectory test; any comparison against TP-1782
flight data (there is no dimensional thrust to compare, and TP-1782 publishes only percentages).

---

## 6. Not changed in this pass

Baumann aero coefficients · the R2 transcription audit · R3 FCS gains and schedules · the aero
alpha-domain gate · scenes and prefabs · live ownership mode · weapons, sensors and combat systems.

`MavF15PropulsionSystem.thrustDeck` remains **null**. The new deck type exists and is tested, but is
not attached: attaching it would change nothing today, and leaving it unattached keeps the
"no deck" state visible in the installation-gap report.

---

## 7. Next highest-value missing source

**The design maximum net thrust of the F100-PW-100(3)** — one scalar — or, equivalently, that
build's **sea-level-static maximum-augmentation gross thrust**, which §6b of the source audit
shows is the same number and is far more likely to be printed. A P&W status/specification deck,
or any F100-PW-100(3) sea-level test report.

**Not** TP-1069 or TP-1228. Those are worth having — they would populate the dimensional
gross-thrust dataset — but their test matrices contain no static point, so they cannot supply
this denominator however completely they are read.

Full ranking in §10 of the source audit.
