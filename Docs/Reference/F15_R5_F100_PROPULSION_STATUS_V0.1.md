# F-15 R5 — F100-PW-100 propulsion implementation status

**Status:** V0.4.
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
| engine configuration represented | **SOURCE-BACKED** | F100-PW-100**(3)** for the thrust characteristic (family A); prototype series 2 7/8 for the control schedules (family B). Held as **separate families and never combined**; both CompatibleSupport, neither the target build |
| steady non-augmented thrust | **SOURCE-BACKED (shape only)** | TP-1034 fig. 17, PLA 20–83°, 4 subsonic conditions. Fraction of design max |
| augmented thrust | **SOURCE-BACKED (shape only)** | TP-1034 fig. 17, PLA 83–130°, all 7 conditions |
| Mach dependence | **SOURCE-BACKED (at 7 points only)** | Mach 0, 0.9, 1.8, 2.15, 2.2. No interpolation between them |
| altitude dependence | **SOURCE-BACKED (at 7 points only)** | 0, 3.048, 6.096, 9.144, 12.19, 13.72, 17.83 km |
| **absolute thrust scale** | **UNAVAILABLE IN HELD SOURCES** | design maximum net thrust printed in none of the held documents. The primary requirement specification, CP2903B, is classified (verified, TP-1056 p.8) — a *restricted primary specification* flag and a search hint, **not** a verdict that no public figure exists. Bounded above at ~22 400 lbf by §6c — a bound on the **TP-1034 normalizer only**, never a NASA 836 limit. Four candidate values investigated and rejected. *(An earlier revision labelled this PUBLIC-SOURCE BLOCKED; withdrawn.)* |
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


---

## 3a. Three intentionally separate paths

R5 ends with three bodies of propulsion evidence that look adjacent and must not be combined.

| | **A — PW-100 simulation lineage** | **B — prototype series 2 7/8** | **C — NASA F-15B 836 target** |
|---|---|---|---|
| engine build | F100-PW-100**(1)** (TM X-3261), **(3)** (TP-1034, TP-1056) | P680059 / P680063 **in their 1977 2-7/8 configuration** — a serial is not a configuration (§3d) | F100-PW-100 on 836, sub-configuration unknown |
| sources | TM X-3261, TP-1034, TP-1056 | TP-1373, TP-1782, TP-1069/1228 when supplied | NASA/TM-2005-213670 and other F-15B reports |
| carries | figure-17 normalized characteristic, cycle equations, Y12 30 000 lbf machine scale, ~22.4 klbf derived bound | altitude calibrations, prototype control schedules and nozzle facts, F-15 flight cross-validation | engine identity; approximate SLS full-AB thrust |
| dimensional? | **no** | would be — holds nothing yet | **yes, one approximate value** |
| ceiling | `CompatibleSupport` | `CompatibleSupport` | `AuthoritativeExactTarget` |

An earlier revision of this document described the research path as "F100-PW-100(3), prototype
series 2 7/8 schedules". That silently married two builds whose differences the sources themselves
spell out — TP-1373 printed p. 4 records series 2 cores, control schedules differing from **both**
series 2 and 3, and series 2 actuated divergent nozzles where series 3 engines have free-floating
ones. The split above exists so that marriage cannot be performed by accident again.

There is a further distinction **inside** path A worth keeping: TM X-3261 is the **(1)** build and
TP-1034/TP-1056 the **(3)**, and TP-1034 printed p. 2 says the (3) "features improved fan
performance over the earlier F100-PW-100(1) version" with every component map regenerated. Items
carry their own build string.

### The gates

`MavF100EngineFamilies.MayCombine` refuses any cross-family combination without a named source
proving build equivalence. No source in this repository proves any.

`MavF100PathSeparation.DimensionalizeForTarget` is the only place path A and path C may meet. Four
conditions: path A produced a number; the path-C anchor is usable (citation, condition, precision
and family all present); the thrust **quantities match**; and the families may combine. The product
is graded `CompatibleSupport` — an exact-target anchor **scales** a research characteristic, it
does not promote one — and an **approximate** anchor yields an **approximate** product, because
multiplying an approximation by an exact fraction does not sharpen it.

`[E22]` asserts the headline case directly: the real NASA 836 anchor, on its own, **cannot**
dimensionalize the PW-100(3) curve. An exact-target aircraft figure is not a proof that the engine
builds match.

---

## 3b. What path C now holds

| field | value | precision | source |
|---|---|---|---|
| engine identity | Pratt & Whitney F100-PW-100 × 2 | exact | frozen earlier |
| uninstalled SLS full-AB thrust, per engine | **23 500 lbf** (104 533 N, derived) | **approximate** | NASA/TM-2005-213670, NTRS 20050241960 |

**A conflicting NASA figure exists and is recorded.** NASA/TM-2001-210395 and NASA/TM-2002-210736
give "approximately 25,000 lbf (91,188 N)" for the same aircraft — a sentence that is
arithmetically inconsistent with itself (25 000 lbf is 111 206 N; 91 188 N is 20 500 lbf) and that
states no power setting. TM-2005-213670 is preferred: internally consistent, condition-specific.

**One approximate sea-level point is not a thrust deck.** No NASA-836 source here gives altitude or
Mach lapse, part-power behaviour, airflow, fuel flow, spool dynamics or installation effects. The R5
pack supplies a characteristic shape — but for *other* engine builds, which is exactly why it may
not simply be scaled by this anchor.

---

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


---

## 3c. The equivalence gate, dimension by dimension

`MavF100ConfigurationEquivalence` is no longer a boolean. It carries four independent flags, because
the P680063 lineage proves they come apart:

| dimension | post-1980 P680063 vs production F100(3) |
|---|---|
| `SameDesignation` | **proven** — TM-84908 p. 4 |
| `SameGasPath` | **proven** — "updated to an F100(3) production engine configuration"; parts list in Burcham p. 4 |
| `SameControlSchedule` | **NOT proven** — it ran a DEEC, which replaced the production EEC and unified fuel control |
| `SamePerformanceDeck` | **NOT proven** — nothing claims one deck predicts both |

`RequiredForCharacteristicTransfer` = `SameGasPath | SameControlSchedule`. The gas path sets what
the engine *can* do; the control sets what it *does* at a given power lever angle, which is exactly
what figure 17 plots. Designation alone is never enough.

### Can the figure-17 gate move at all?

**No — for two independent reasons, and neither is close to being resolved.**

1. **The equivalence needed is not established.** The best available bridge reaches designation and
   gas path. Control schedule is positively contradicted for the one engine whose chronology is
   documented, and no source addresses the production-F100(3)-to-836 relationship at all.
2. **NASA 836's own installed sub-configuration is unknown.** Even a perfect equivalence proof
   would have nothing to be an equivalence *to*. `MavF100Nasa836EngineEvidence.SubConfigurationKnown`
   is `false`, and the gate checks it separately.

`[E28]` asserts both, including that with everything else satisfied the sub-configuration refusal is
the *only* one remaining — so the earlier conditions are real conditions, not decoration.

---

## 3d. Configuration date travels with every datum

`MavF100ConfigurationLineage` records P680063's six phases. Three consequences the code enforces:

- **1977 / 2-7/8 calibration data cannot be relabelled F100(3).** That phase proves no equivalence
  dimension at all. TP-1069, TP-1228, TP-1373 and TP-1782 all describe that configuration.
- **Post-1980 P680063 may carry F100(3) lineage** — designation and gas path, and no further.
- **">27 000 lbf" is 1993 EMD-era data** (EMD fan, single-crystal turbine blades and vanes,
  16-segment augmentor, overhauled increased-life core) and is never a baseline PW-100(3) anchor.

The same rule now applies to path C: **836 was re-engined to F100-PW-220E in 2014**, so any "24 000
lb thrust class" figure for that tail may describe a different engine model. The frozen target is
the pre-Quiet-Spike PW-100 baseline, which the 2005 and 2010 sources sit inside.

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

### Three specific numbers that must not be used to close it

**30 000 lbf (133.45 kN).** TP-1034 appendix C printed p. 28 gives `FN=Y12; FN=FN*30000.;
FNSI=FN*4.4482E-3` — a genuine printed dimensional thrust scale that the first pass of this audit
missed. It is the **full-scale factor of a `SCALED FRACTION` DAC channel**, not a design value, and
it provably cannot be the figure 17 normalizer: the channel's type cannot exceed 1.0, while figure
17 plots up to 1.338. It does yield a derived **upper bound** on the design maximum of about
22 400 lbf. §6c of the source audit has the full trace; `[E20]` holds it.

**23 500 lbf, the NASA 836 figure.** It is an exact-target *aircraft* datum and it belongs to path
C — but it is not the PW-100(3) figure 17 normalizer, and it is not proof that the builds match.
`[E22]` asserts that it cannot dimensionalize the research curve on its own.

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
| `MavF15PropulsionValidation` | **198 passed, 0 failed** (was 27) |
| `MavF15BaumannTranscriptionValidation` | **28 passed, 0 failed** |
| `MavF15ControlPathValidation` | **79 passed, 0 failed** |
| **total** | **305 passed, 0 failed** |

314 runtime scripts compile with 0 errors.

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
| `[E20]` | the TP-1034 30 000 lbf channel scale, and why it is not the normalizer |
| `[E21]` | CP2903B verified and restricted — a search hint, not a verdict |
| `[E22]` | the research characteristic and the NASA-836 target stay separate paths |
| `[E23]` | PW-100(3) and prototype 2 7/8 data cannot silently mix |
| `[E24]` | the NASA 836 approximate anchor, with its approximation metadata preserved |
| `[E25]` | the ~22.4 klbf bound belongs to TP-1034 and is not a NASA 836 limit |
| `[E26]` | a serial number is not a configuration — the P680063 chronology |
| `[E27]` | same designation is not same performance deck |
| `[E28]` | NASA 836's engine sub-configuration is unknown, and the gate stays shut |

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

**An unclassified source publishing the F100-PW-100(3) design maximum net thrust** — or,
equivalently, that build's sea-level-static maximum-augmentation gross thrust. Expected below
~22 400 lbf per §6c.

CP2903B, the requirement document that would state it, is classified (verified, TP-1056 printed
p. 8) and **must never be reconstructed**. That is a search hint, not a dead end: a performance
figure published elsewhere is a different document, and §6e shows NASA does publish such figures
for the target aircraft. Searching remains worthwhile.

**Or** — the other way to unblock this layer — a source proving build equivalence between
F100-PW-100(3) and the F100-PW-100 on NASA 836. That would let path C's approximate anchor scale
path A's characteristic, and the deck would go live at approximate precision.

**Not** TP-1069 or TP-1228. Those are worth having — they would populate the dimensional
gross-thrust dataset — but their test matrices contain no static point, so they cannot supply
this denominator however completely they are read.

Full ranking in §10 of the source audit.
