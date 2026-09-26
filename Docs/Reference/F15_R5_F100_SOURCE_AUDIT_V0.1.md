# F-15 R5 — F100-PW-100 source audit

**Status:** V0.5. The four documents supplied in `F100_R5_CLAUDE_SOURCE_PACK.zip`, plus NASA
TP-1056 and three NASA F-15B reports retrieved from NTRS and read here.
**Target aircraft:** `NASA_F15B_836_SN74_0141_PRE_QUIET_SPIKE_BASELINE_F100_PW_100`
**Target engine:** Pratt & Whitney F100-PW-100 × 2.

This document records what the four NASA reports actually contain, field by field, before any of it
was implemented. Section 9 records what they do **not** contain, which in this pack is the more
consequential half.

---

## 1. The finding that shapes the whole pass

**No absolute thrust value for any flight condition and power setting appears anywhere in the four
documents.**

> **Correction, V0.2.** An earlier revision said there was no absolute thrust value *anywhere* in
> the pack. That was too strong. TP-1034 appendix C prints a dimensional thrust **scale** —
> 30 000 lbf — as the full-scale factor of the simulation's net-thrust channel. It is not a
> thrust at a condition, and §6c shows it is provably *not* the figure 17 normalizer, but it is a
> real printed dimensional fact and this audit missed it on the first pass. The statement above
> is now scoped to thrust **results**.

Every thrust result in the pack is either a fraction of an unpublished normalizer or a percentage
difference against a proprietary manufacturer deck that is not included:

| document | what its thrust results are | why they are not absolute |
|---|---|---|
| TP-1034 fig. 17 | fraction of **design maximum net thrust** | the design maximum is never stated |
| TP-1373 figs. 6–9 | percent error vs P&W CCD 1088-2.0 | that deck is not in the pack |
| TM X-3261 | complete thrust **equations** (B42–B52) | driven by component maps published as graphs and read from data cards |
| TP-1782 | percent agreement between two methods | SGTM coefficients K1, K2, E, Cv withheld |

The one near-miss is **TP-1373 figure 10**, which plots facility-measured gross thrust in kN
(printed p. 23). It is discussed in §6.

The consequence: the **shape** of F100 net thrust over the F-15 envelope is recoverable and has been
implemented. The **scale** is not, and is carried as undeclared. See
`F15_R5_F100_PROPULSION_STATUS_V0.1.md`.

---

## 2. Provenance vocabulary

The R5 brief's four classes are implemented as `MavF100SourceClass` and map onto the repository's
existing single provenance scale so the "worst field wins" rule stays checkable:

| R5 class | `MavF100SourceClass` | `MavEngineDataProvenance` | meaning here |
|---|---|---|---|
| AuthoritativeExactTarget | `AuthoritativeExactTarget` | `Authoritative` | accepted for the F100-PW-100 on NASA 836 |
| CompatibleSupport | `CompatibleSupport` | `PublicReference` | a real F100 source, unproven build equivalence |
| CrossValidationOnly | `CrossValidationOnly` | `CrossValidationOnly` | an oracle, never a value source |
| Unavailable | `Unavailable` | `Unavailable` | no accepted source |

**Nothing in this pass is `AuthoritativeExactTarget`.** The engine *identity* of NASA 836 is
exact-target and was frozen in an earlier pass; no *performance* field reaches that grade here.

### Why every performance field is capped at CompatibleSupport

| document | engine build | why it is not the target build |
|---|---|---|
| TM X-3261 | F100-PW-100(1) | patterned after P&W CCD 1015, a simulation of the engine, not a measurement of NASA 836's |
| TP-1034 | **F100-PW-100(3)** | explicitly the improved-fan (3) build; maps, augmentor efficiency, duct loss and nozzle coefficients all regenerated (printed p. 2, p. 4) |
| TP-1373 | prototype **series 2 7/8** | series 2 core + series 3 improved-stability fan, control schedules differing from **both** series 2 and 3, series 2 actuated divergent nozzles (printed p. 4) |
| TP-1782 | same series 2 7/8 engines | "the results are not totally representative of production F100 engines" (printed p. 4) |
| TP-1056 | F100-PW-100**(3)** | follow-on on the same CCD 1103-1.0 lineage; retrieved from NTRS and read — §6d |
| TM-2005-213670 and other F-15B reports | F100-PW-100 **on NASA F-15B 836** | the exact target aircraft; publishes an approximate SLS thrust — §6e |
| Burcham et al. 19990064011; TM-84908 | P680063 across **four configurations** | the configuration chronology — §6f |
| NTRS 20160006705; 20100001729 | NASA 836 airframe and engines | tail identity, PFTF link, 2014 re-engine — §6g |

---

## 3. Extraction table — what was taken and used

| quantity | document | page / figure / equation | build | units | numeric or reconstructed | class | implementation decision |
|---|---|---|---|---|---|---|---|
| Net thrust vs PLA, 7 flight conditions | TP-1034 | fig. 17, printed pp. 64–65 (PDF 68–69) | PW-100(3) | fraction of design max | **digitized from figure** | CompatibleSupport | implemented as `MavF100SourceData.NetThrustCurves`; 63 points |
| Ram drag / net-from-gross | TM X-3261; TP-1034 | eq. (B52) p. 22; eq. (B56) p. 21 | both | N, kg/s, K | printed equation | CompatibleSupport | implemented, `MavF100ThrustSemantics.NetFromGross` |
| Inlet total-pressure recovery | TM X-3261 | eq. (B3) p. 13 + appendix C FORTRAN | PW-100(1) | — | printed equation | CompatibleSupport | implemented, labelled **typical**, not F-15 |
| Fan-face stagnation ratios | TM X-3261 | eqs. (B4),(B5) p. 13 | PW-100(1) | — | printed equation | CompatibleSupport | implemented |
| EEC minimum power lever schedule | TP-1373 | printed p. 5 | series 2 7/8 | Mach → fraction | printed prose, both endpoints + law | CompatibleSupport | implemented, returns a fraction (see §5) |
| Nozzle area-ratio mode switch | TP-1373 | printed p. 7 | series 2 7/8 | Mach | printed prose | CompatibleSupport | implemented as a boolean |
| Idle / max-AB / supersonic-minimum PLA | TP-1034 | printed p. 11 | PW-100(3) | deg | printed prose | CompatibleSupport | implemented as named constants |
| Military PLA = 73° | TM X-3261 | printed p. 6 | PW-100(1) | deg | printed prose | CompatibleSupport | implemented **separately** — see §5 |
| Design corrected airflow 98.4 kg/s | TP-1373 | figs. 6(a),6(c); p. 12 text | series 2 7/8 | kg/s | printed axis normalizer | CompatibleSupport | recorded, not yet used |
| Gross-thrust axis normalizer 111.2 kN | TP-1373 | figs. 6(b),6(d) | series 2 7/8 | N | printed axis scale | **not engine data** | recorded with a prohibition — see §6a |
| TP-1069 / TP-1228 test matrices (16 conditions) | TP-1373 | **table 3, printed p. 9** | series 2 7/8 | Mach, m, K | printed table | CompatibleSupport | implemented as `MavF100DimensionalAnchor.Engine059Conditions` / `Engine063Conditions` |
| Flight envelope actually flown | TP-1782 | printed p. 1 | series 2 7/8, engine 059 LEFT | Mach, m | printed prose | CrossValidationOnly | recorded; never used as a source envelope |
| SGTM/GGM agreement ±3 % | TP-1782 | printed pp. 1, 17 | series 2 7/8 | percent | printed prose | CrossValidationOnly | recorded as an oracle bound |
| Design parameters (inertias, volumes, cp, bleeds, HVF) | TM X-3261 tab. I p. 53; TP-1034 tab. I | both | SI | printed table | CompatibleSupport | **not implemented** — see §7 |
| **Net-thrust channel full scale, 30 000 lbf** | TP-1034 | **appendix C, printed p. 28**; Y12 declared p. 25, computed p. 26 | PW-100(3) | lbf / kN | printed FORTRAN constant | CompatibleSupport | recorded as a simulation-output fact; **not** applied to the deck — see §6c |

---

## 4. The digitization of TP-1034 figure 17

The brief permits figure digitization where no equation or table exists, and requires the method and
the extracted points to be recorded separately from runtime logic. Both conditions are met:
the points live in `MavF100SourceData`, the evaluation in `MavF100NormalizedNetThrustModel`.

**Method.** The scanned page was extracted from the PDF at its native 2544 × 3300, not re-rendered.
Axis calibration came from the printed tick marks, located by column/row density. Data markers are
open circles, so each was located as the enclosed white region bounded by dark pixels — no
curve-following and no eyeballing. Every panel was then checked by drawing the reconstructed values
back onto the page image as guide lines.

**Which trace.** The **hybrid** simulation markers (open circles) were digitized, because they are
discrete and therefore locatable exactly. The solid **baseline digital** curve was not digitized.
TP-1034 printed p. 13 records that the two differ by **up to 9 percent of design maximum thrust** at
the supersonic augmented conditions, which is the honest error bar on reading these markers as the
manufacturer's predicted performance.

**Accuracy.** The figure supplies its own check. Its normalizer is defined by panel (a) reaching
1.0 at sea level, Mach 0, PLA 130°. This digitization reads **1.004** there, so the reading error at
the defining point is 0.004 in fraction units. `[E7]` asserts it stays within 0.01.

**What was extracted:** 7 operating points, 63 points total.

| panel | altitude | Mach | PLA range | points |
|---|---|---|---|---|
| 17(a) | 0 m | 0.00 | 20 – 130 | 12 |
| 17(b) | 3 048 m | 0.90 | 20 – 130 | 12 |
| 17(c) | 9 144 m | 0.90 | 20 – 130 | 12 |
| 17(d) | 13 720 m | 0.90 | 20 – 130 | 12 |
| 17(e) | 6 096 m | 1.80 | 83 – 130 | 5 |
| 17(f) | 12 190 m | 2.20 | 83 – 130 | 5 |
| 17(g) | 17 830 m | 2.15 | 83 – 130 | 5 |

The three markers on the y-axis of panels (a), (c) and (d) are bisected by the axis line, so their
interiors do not form a single enclosed region. Those three were read individually against
calibrated guide lines at 4× magnification rather than by the automatic detector.

---

## 5. Two source conflicts, recorded and not resolved

**Power lever convention.** TM X-3261 printed p. 6 states that military power is **PLA = 73°**.
TP-1034 works to **83°** as the top of non-augmented operation, and figure 17(a) shows its
augmentation plateau beginning there. The two reports do not share a power lever convention.
Both constants are kept, separately named. Averaging them, or silently preferring one, would create
a third convention that neither document supports. `[E13]` asserts they remain distinct.

This is also why `MavF100EngineControlSchedules.MinimumPowerFractionOfIntermediate` returns a
**fraction of the idle-to-intermediate span** rather than an angle: the schedule's shape is sourced,
but expressing it in degrees would require picking one of the two conventions.

**Idle PLA at 13.72 km.** TP-1034 printed p. 11 says the idle setting at 13.72 km / Mach 0.9 was
**30°**. Figure 17(d)'s leftmost markers digitize at approximately 20°, 24° and 30°. The figure's
abscissa is labelled "**Corresponding** power lever angle", i.e. the PLA matching the set of control
variables rather than a commanded detent, which is the most likely explanation. Recorded, not
resolved; nothing implemented depends on resolving it.

---

## 6. TP-1373 figure 10 — the one absolute thrust plot, and why it is not used

Printed p. 23 plots facility-measured and model-calculated gross thrust for engine 059 in **kN**,
5 to about 64 kN, at Mach 0.80, 4 020 m, Tt2 = 296 K, against FTIT/θ.

It is not used as a runtime scale, for three independent reasons:

1. **The abscissa is FTIT/θ**, fan-turbine inlet temperature — an engine internal variable, not a
   pilot input and not a runtime state Maverick has.
2. **It cannot separate augmented from non-augmented operation.** During augmentation the control
   holds the gas generator at its military values, so FTIT stops rising while thrust continues to.
   Points from both regimes therefore stack at the same abscissa, and the legend distinguishes only
   hours and increasing/decreasing PLA. Whether the ~64 kN top of the curve is intermediate or
   maximum augmentation cannot be read off the figure. It is most consistent with **intermediate**,
   but "most consistent with" is not a source.
3. **It is GROSS thrust**, and figure 17 is **NET**. Converting needs engine airflow at that
   condition, which the figure does not carry.

Recorded here so that the next reader does not have to re-derive why an apparently usable kN axis
was left alone.

---

## 6a. 111.2 kN must not become the design-maximum denominator

The pack contains exactly one round thrust number, and the deck is short by exactly one scalar.
They are not the same scalar, and the temptation to join them is the most inviting wrong turn
available here. Three reasons, in order of how easy they are to check:

1. **It is gross thrust, where figure 17 is net.** TP-1373 figure 6(b) was re-read from the page
   image: the abscissa reads `F_g, percent of 111.2 kN`, with `F_g` defined in the symbol list as
   "gross thrust, kN" — plain `F_g`, with no `δ`. Between gross and net sits the ram drag, which at
   every condition TP-1373 tested is a large fraction of the gross thrust, not a correction.

2. **It is a nominal scale, not a design point.** The user reports that **NASA TP-1228 states
   111 kN (25 000 lbf) to be an arbitrarily chosen nominal *corrected* gross-thrust normalization
   value.** TP-1228 is not held in this repository and that statement is **not verified here** —
   it is carried on the user's authority, in the same way the R3 roll-damper figure is. It is
   consistent with what TP-1373's own page shows: the text never calls 111.2 kN a design, maximum
   or rated value.

3. **The two reported usages are not even the same quantity.** TP-1373's axis is uncorrected
   `F_g`; the reported TP-1228 usage is *corrected* gross thrust, `F_g/δ`. If both are right, the
   same round number serves as a scale for two different quantities in two different reports —
   which is what a nominal normalizer does and what a physical rating does not.

`[E17]` asserts the prohibition, and asserts that the TP-1228 statement is stored as unverified.

---

## 6b. The sea-level-static anchor — sound mechanism, does not close

A better route to the missing denominator exists in principle, and was investigated rather than
assumed.

### The mechanism is correct

Ram drag is `20.041 · w₂ · M₀ · √T₀`, so at `M₀ = 0` it is **identically zero** — not small, zero,
with no airflow value required. Gross thrust and uninstalled net thrust are therefore the same
number at a static condition. TP-1034 figure 17(a) is sea level, Mach 0, and reaches exactly 1.0 at
PLA 130, which is how its normalizer is *defined*. So:

> design maximum net thrust = uninstalled net thrust at SLS max augmentation = **gross thrust at
> SLS max augmentation**

That is a real result. It converts the blocker from "a normalizer nobody published" into
"sea-level-static maximum-augmentation gross thrust" — a far more findable quantity, and one that
engine test reports routinely contain.

### It fails at the first of three gates

**Gate 1 — no static point exists in the candidate sources. This is where the chain actually
breaks.** TP-1069 and TP-1228 are **altitude** facility calibrations. Their complete test matrices
are reproduced in TP-1373 **table 3, printed p. 9**, and figure 4 on the same page plots them:

| engine | report | conditions | lowest Mach | lowest altitude |
|---|---|---|---|---|
| P680059 | TP-1069 | 8 | **0.80** | 4 020 m |
| P680063 | TP-1228 | 8 | **0.80** | 4 020 m |

Neither report contains a sea-level-static point. Neither can supply this anchor at all. The
matrices are transcribed into `MavF100DimensionalAnchor` and `[E18]` *searches* them for a static
condition rather than asserting their absence, so the conclusion survives someone editing them.

**Gate 2 — at their actual conditions the identity is unavailable.** At Mach 0.80 ram drag is
large, so converting a measured gross thrust to net needs absolute engine airflow at that
condition — which TP-1373 publishes only as calibration *percentages* against the absent
manufacturer deck. And the result would be net thrust at 4 020 m / Mach 0.80, a condition figure 17
does not plot at all: its subsonic panels are 0, 3.048, 9.144 and 13.72 km. There would be nothing
to anchor *to*.

**Gate 3 — the engine builds are not shown to be the same.** TP-1373 printed p. 4 records that the
calibration engines are prototype series 2 7/8: series 2 cores, a series 3 fan, "control schedule
differences from both the series 2 and 3 engines", and series 2 actuated divergent nozzles where
series 3 engines have free-floating ones. Nozzle actuation and control schedule are precisely what
set maximum augmented gross thrust.

And the designation schemes do not meet. **Neither TP-1034 nor TM X-3261 uses the word "series"
anywhere** — checked across both full texts. Nothing in the pack relates the "(1)" and "(3)"
designations of the simulation reports to the "series 2 / 2 7/8 / 3" designations of the
calibration reports. Equivalence cannot be proven from these four documents even in principle,
because no document relates the two naming schemes.

### What was implemented instead

`MavF100DimensionalAnchor.DesignMaximumNetThrustFromStaticGross` encodes all three gates as
executable refusals: a candidate measured off-static is refused, a value not labelled gross is
refused, and an equivalence claim without a named proving source is refused. `[E18]` also asserts
the **positive** case — with all three gates satisfied the anchor does close — so the refusals are
demonstrably the evidence failing rather than the code being unable to proceed.

`MavF100DimensionalGrossThrustDataset` is where TP-1069/TP-1228 dimensional gross thrust would
live, kept structurally separate from the normalized net model. It is empty: those reports are not
held, and TP-1373 reports their results only as percentages against a deck that is also absent.

---

## 6c. TP-1034 appendix C prints a thrust scale — and it is not the normalizer

The first pass through appendix C stopped once it was clear the component maps were read from data
cards, and never reached the output-scaling block. That was a gap. Printed **p. 28** contains:

```fortran
FN=Y12
FN=FN*30000.
FNSI=FN*4.4482E-3
```

with the symbol list defining `FN` as uninstalled net thrust in lbf and `FNSI` the same in kN.
**NASA TM X-3261's appendix C carries the identical block**, so the two reports agree.

### The digit was verified, because it matters

Read from the page image at 6×. The leading digit is **3**: it matches the `3` of `T41=T41*3000.`
on the same page at the same magnification, and differs from the `2` and the `5` of
`WPLPT=WPLPT*2 5*.29326` two lines above. There are **four** trailing zeros, one more than
`T41*3000.`. The value is **30 000**, not 25 000.

`30 000 lbf = 133.45 kN`, using the source's own `4.4482E-3` to kN. Not 111.2 kN.

### Y12 traced

| step | source | what it establishes |
|---|---|---|
| `SCALED FRACTION Y0,Y1,...,Y12` | appendix C, printed **p. 25** | Y12 is a **DAC channel** of EAI fixed-point fractional type — it lives in `[-1, 1)` |
| `Y12=((X15*SSQRT(...)+X14))/.69633S-FRD-(AE*(.20000S*V3-PE))/.42933S)/.349335` | appendix C, printed **p. 26** | Y12 is net thrust: gross terms, less `FRD` (ram drag), less the pressure-area term, rescaled onto the channel |
| `FN=Y12; FN=FN*30000.` | appendix C, printed **p. 28** | 30 000 lbf is the value of `FN` at `Y12 = 1` — the channel's **full scale** |

### Why the full scale is not the design maximum

**It cannot be.** Y12 is a `SCALED FRACTION`, so the largest net thrust the simulation can
*represent* is 30 000 lbf. Figure 17 panel (e) — 6.096 km, Mach 1.8, maximum augmentation — plots
**1.338** of its own normalizer. If that normalizer were the channel's full scale, the point would
require `Y12 = 1.338`, which the type cannot hold. The plotted markers are this simulation's own
output, and the report's steady-state thrust printout derives from the same channel, so every hybrid
thrust value in TP-1034 passed through Y12.

Supporting pattern: every scale factor in that block is a round headroom value rather than a design
value. The clearest case is on the same page — `PLA=PLA*150.`, where the documented maximum power
lever angle is **130°** (printed p. 11). Likewise `XNL`/`XNH` at 15 000 rpm against corrected fan
speeds that TP-1373 shows peaking near 11 000, `T4` at 4 000, `T41` at 3 000, `WF7` at 20, `WA2` at
450.

### What this does buy: an upper bound

If figure 17's peak is `1.338 × D` and that thrust had to fit a channel whose full scale is 30 000
lbf:

> `1.338 · D ≤ 30 000 lbf`  ⇒  **`D ≤ 22 422 lbf` (99.7 kN)**

This is the first quantitative constraint on the missing scalar. It is a **bound, not a value**, and
it rests on three things holding together: the digitized 1.338 (±0.01, measured here), the
`SCALED FRACTION` range (printed), and the plotted hybrid markers having come through Y12 (strongly
implied by the printout deriving from it). Graded `CrossValidationOnly` — an inference from printed
facts rather than a printed fact — and nothing computes with it.

It also **independently rules out 25 000 lbf / 111.2 kN**, since 25 000 > 22 422.

### Action taken

Per the instruction's own fallback: the 30 000 lbf scaling is recorded as a separately documented
TP-1034 simulation-output fact, and is **not** applied to the 63-point deck. The design maximum
stays undeclared and the deck still produces no newtons. `[E20]` covers all of this.

---

## 6d. NASA TP-1056 — verified from the primary source

**TP-1056 was retrieved from NASA NTRS (document 19770026225, distribution PUBLIC) and read here.**
An earlier revision of this audit carried its findings as reported-but-unverified; they are now
verified and that hedging is withdrawn.

| finding | verified at |
|---|---|
| The MVCS engine is the Pratt & Whitney F100-PW-100(3) | printed pp. 2, 7 |
| The real-time hybrid simulation is patterned after CCD1103-1.0 | printed p. 17 |
| **Net thrust is computed in the DIGITAL portion** of the hybrid computer | printed p. 17 |
| Engine thrust and fuel-consumption **requirements are in F100 specification CP2903B**, and **those specifications are classified** | printed p. 8, checked against the page image |
| For the public MVCS work, thrust and fuel goals were taken as equal to CCD1103-1.0 predicted performance | printed p. 8, same paragraph |

Printed p. 17, verbatim: *"the digital computer was also used for computing the fan and compressor
surge margins and the engine net thrust."*

### This removes an assumption from the §6c bound

§6c derived `D ≤ 22 422 lbf` and flagged one assumption: that figure 17's hybrid markers passed
through the scaled-fraction channel Y12. Net thrust being computed **in the digital portion**, with
TP-1034's appendix C showing that computation producing `Y12`, closes it from the other side. The
ceiling binds where the value is **computed**, not merely where it is output, so the bound no longer
depends on how the figure was plotted.

### What CP2903B does and does not establish

It establishes that the primary *requirement* document for F100 thrust is restricted, and that even
NASA's own public programme worked from CCD1103-1.0 predictions instead. That explains why the
figure 17 normalizer is absent from this report chain.

**It does not establish that no unclassified source publishes an F100 thrust value.** A classified
requirements specification and a published performance figure are different documents — and as
§6e shows, NASA F-15B reports do publish the latter. The earlier "PUBLIC-SOURCE BLOCKED" label
overstated the case and is withdrawn.

The missing scalar is classified in code as:

> `MavF100BlockerKind.UnavailableInHeldSources` **+** `restrictedPrimarySpecification = true`

— absent from what is held, with a search hint attached, and **no** encoding of "looking will not
help". Public searching remains legitimate. **CP2903B itself must never be reconstructed,
estimated or inferred from.**

---

## 6e. NASA F-15B 836 publishes an approximate thrust — path C is not empty

Retrieved from NTRS and read here.

**NASA/TM-2005-213670** (H-2625), *Local Flow Conditions for Propulsion Experiments on the NASA
F-15B Propulsion Flight Test Fixture*, NTRS 20050241960, public:

> "The F-15B airplane is powered by two Pratt & Whitney (West Palm Beach, Florida) F100-PW-100
> turbofan engines that each produce an uninstalled, sea level static thrust of approximately
> **23,500 lbf** in full afterburner."

That is an exact-target-**aircraft** datum: the right airplane, the right engines, a stated
condition. It is **approximate**, and it is a descriptive figure in an airplane-description section,
not a calibrated engine deck. 104 533 N is derived here — the report prints only lbf.

### A conflicting NASA figure, recorded rather than discarded

**NASA/TM-2001-210395** (AIAA 2001-3303) and **NASA/TM-2002-210736** both state, of the same
aircraft:

> "Each engine has an uninstalled, sea-level static thrust rating of approximately 25,000 lbf
> (91,188 N)."

**That sentence is internally inconsistent.** 25 000 lbf is 111 206 N; 91 188 N is 20 500 lbf. The
two halves disagree by more than 20 percent, so neither number in it can be relied on. It also
states no power setting, where TM-2005-213670 says "in full afterburner".

TM-2005-213670 is therefore preferred: internally consistent and condition-specific. The conflict is
recorded so nobody rediscovers 25 000 lbf and assumes it was overlooked — and because 25 000 lbf
happens to equal TP-1373's 111.2 kN axis scale, a coincidence worth being suspicious of rather than
encouraged by.

### The bound does not apply to this number

`~22 422 lbf` is an upper bound on the **TP-1034 figure 17 normalizer** for the PW-100(3)
simulation, and on nothing else. NASA 836's published figure, 23 500 lbf, is **above** it.

That is not a contradiction, because the two are different quantities on builds never shown to be
equivalent: one is a simulation's internal normalizing constant, the other a quoted
installed-aircraft engine figure. If anything, the two failing to fit is further evidence that they
must not be treated as the same thing — applied across families the bound would "prove" a
published NASA figure impossible. `MavF100EngineFamilies.BoundAppliesTo` enforces the scope and
`[E25]` asserts it.

---

## 6f. The configuration-equivalence bridge — how far public sources close it

Two more primary sources were retrieved from NTRS and read here:

- **Burcham, Conners and Maxwell**, *Flight Research Using F100 Engine P680063 in the NASA F-15
  Airplane* (NTRS 19990064011, public)
- **NASA TM-84908**, *Airstart performance of a digital electronic engine control system in an
  F-15 airplane* (NTRS 19830013932, public)

### P680063 configuration chronology

NASA's own retrospective states that P680063 "has flown in four major configurations". The same
serial spans 1972 to 1994 and ends producing more than 27 000 lbf.

| from | designation | what changed | source |
|---|---|---|---|
| 1972 | F100-PW-100**(2)** | as manufactured, serial range P680050–P680084; USAF Combined Test Force | Burcham p. 2 |
| 1974 | F100-PW-100**(2-7/8)** | rebuilt with improved fan (bulged inner diameter) and control system; one of four | Burcham p. 2 |
| **1977** | F100-PW-100(2-7/8) | **altitude calibration at NASA Lewis PSL with P680059** — the configuration behind TP-1069, TP-1228, TP-1373, TP-1782 | Burcham p. 3 |
| **1980** | **F100-PW-100(3) w/ DEEC** | P&W modified it to represent the **gas path of the production F100(3)**: compressor 7th/8th-stage disk and blades, 13th-stage disk, combustor, fuel nozzles, turbine 1st/2nd-stage disks, 3rd/4th-stage disk and blades, nozzle divergent actuator. Plus DEEC and "partial swirl" augmentor | Burcham p. 4; TM-84908 p. 4 |
| 1985 | F100 **EMD** | EMD fan, single-crystal turbine blades and vanes, 16-segment augmentor | Burcham p. 7 |
| 1990 | F100 EMD, overhauled | new increased-life core; "little deterioration and better-than-average performance" | Burcham pp. 8–9 |

**The calibration data this project relies on is 1977 / 2-7/8 data.** It is not F100(3) data and
cannot be relabelled as such.

### The bridge, dimension by dimension

TM-84908 printed p. 4 is the strongest statement available: *"It had been updated to an F100(3)
production engine configuration prior to the DEEC installation."* Burcham printed p. 4 says the
intent was "to have the engine represent the gas path of the production F100(3) engine" by
incorporating "many production F100(3) parts".

| dimension | post-1980 P680063 vs production F100(3) | evidence |
|---|---|---|
| **SameDesignation** | **YES** | TM-84908 p. 4; Burcham figure 8 caption calls it "F100(3) CONFIGURATION" |
| **SameGasPath** | **YES** (source-stated intent plus a parts list) | Burcham p. 4; TM-84908 p. 4 |
| **SameControlSchedule** | **NO** | the engine ran a **DEEC**, which TM-84908 says "replaces the functions of the supervisory electronic engine control and hydromechanical unified fuel control on the standard F100 engine". Burcham figure 8 plots "Standard F100 UFC-EEC" against "F100 DEEC phase 2/3/4" as different things |
| **SamePerformanceDeck** | **NO** | nothing states it. TP-1034's characteristic comes from CCD1103-1.0; no source ties modified-P680063 measurements to that deck |

So a defensible statement is available, and it is narrower than it first looks:

> Post-1980 P680063 and the production F100(3) share a **designation** and, by the modifying
> organisation's own account, a **gas path**. They do **not** share a control system, and no source
> claims a common performance deck.

**Same designation, same gas path, categorically different control.** That is exactly why
`MavF100EquivalenceDimension` is a flags enum rather than a boolean, and why
`RequiredForCharacteristicTransfer` is gas path **and** control schedule — the gas path sets what
the engine can do, the control sets what it does at a given power lever angle, which is what
figure 17 plots.

### This bridge does not reach NASA 836 in any case

Worth stating plainly: the P680063 story runs on NASA F-15 **#2 (71-0281)** and **#8 (71-0287)**.
Neither is 74-0141. And TP-1034's figure 17 is the **CCD1103-1.0 simulation of production
F100-PW-100(3)**, not a measurement of P680063 in any configuration. The chronology establishes
that a particular test engine was brought into the F100(3) production family; it says nothing about
which build is bolted to tail 836.

---

## 6g. NASA 836's engine sub-configuration — searched, not found

**Result: NO public source located.**

Searched NTRS for tail 836, USAF serial 74-0141, F-15B PFTF and Quiet Spike aircraft descriptions,
the F-15B capability briefings, and the engine-configuration vocabulary (BOM, production
configuration, UFC/EEC, nozzle configuration, engine change). The F-15B reports describe the
airframe and quote a thrust figure; **none describes the engine build.** Whether 836's
F100-PW-100 engines were (1), (2), 2-7/8 or production (3) is not established by anything found.
Aircraft production year is not evidence of engine sub-configuration and was not used as such.

### What the search did establish

**Tail identity.** NTRS 20160006705, *F-15B 836 Supersonic Research Testbed Capabilities* (NASA
Armstrong, January 2016): "F-15B (74-0141), Obtained in 1993 from Hawaii ANG".

**A re-engine that bounds every 836 thrust figure.** The same briefing states: "Two **F100-PW-220E**
engines — upgraded in **2014** — **24,000 lb thrust class** — Digital engine control."

That matters directly. A "24,000 lb" figure quoted for tail 836 may describe the **PW-220E**, a
different engine model entirely. The frozen target is the *pre-Quiet-Spike F100-PW-100 baseline*
(Quiet Spike flew 2006–07), so the PW-100 era is the right epoch and 2014-and-later figures are
out of scope for it — but **configuration date has to travel with 836 figures too**, not only with
P680063 data.

### The 23 500 lbf identity chain is multi-source

**NASA/TM-2005-213670 never names tail 836 or 74-0141.** It says "the F-15B airplane" and discusses
the PFTF. The chain therefore takes two sources and is recorded as two:

| link | source |
|---|---|
| **A.** The PFTF flies on the NASA F-15B, **tail number 836**, which "is powered by two Pratt & Whitney F100-PW-100 afterburning turbofan engines" | NTRS 20100001729 (2010) |
| **B.** "The F-15B airplane is powered by two … F100-PW-100 turbofan engines that each produce an uninstalled, sea level static thrust of **approximately 23,500 lbf** in full afterburner" | NASA/TM-2005-213670 (2005) |

Both are inside the PW-100 era and before the 2014 re-engine. The chain is not collapsed into a
single-source claim.

---

## 7. Why the TM X-3261 / TP-1034 engine model was not ported

Both reports print a complete, transient-capable engine model: mass and energy storage, fluid
momentum in duct and augmentor, rotor dynamics, and gross and net thrust (TM X-3261 eqs. B1–B85).
TM X-3261 table I and TP-1034 table I print real design parameters — rotor inertias (I_H = 515.2,
I_L = 610.0 N·cm·s² for the (1) build), volumes, specific heats, bleed fractions, heating value.

It is nonetheless not runnable from this pack. **The component performance maps are not published
as numbers.** They are figures 16–20 (TM X-3261) and 3–8 (TP-1034), and the appendix C FORTRAN does
not contain them — it reads them from data cards:

```
C*****INPUT REAL COMPONENT DATA AND MAP SCALE FACTORS
      TYPE 1
    1 FORMAT(/3X,33HPLACE DATA CARDS FOR MAPS NO. 2-9)
```

Running the model would require digitizing six or more bivariate maps — fan tip, fan hub,
compressor, compressor stator-vane shift, and both turbines — each a family of curves. That is the
"port the 1970s hybrid simulation blindly into Unity" outcome the brief rules out, and it would
produce a large body of reconstructed numbers presented as an engine deck.

**What was taken from the model instead:** the parts that are printed as closed-form equations and
need no map — inlet recovery (B1–B5) and the ram-drag transformation (B52/B56).

The ram-drag coefficient is worth recording as verified rather than merely transcribed:

> F_net = F_gross − 20.041 · w₂ · M₀ · √T₀

20.041 is √(γR) for air (√(1.4 × 287.05) = 20.047), so the term is simply w₂·V₀ with the local speed
of sound expanded. That identity is what makes this equation safe to implement while the gross
thrust equation printed immediately above it is not.

---

## 8. Field-level provenance summary

| field | class | source |
|---|---|---|
| engine identity | AuthoritativeExactTarget | frozen in an earlier pass, not from this pack |
| normalized net thrust vs PLA / altitude / Mach | CompatibleSupport | TP-1034 fig. 17, digitized |
| gross → net transformation | CompatibleSupport | TM X-3261 (B52) / TP-1034 (B56) |
| inlet total-pressure recovery | CompatibleSupport | TM X-3261 (B3), described as *typical* |
| EEC minimum power schedule | CompatibleSupport | TP-1373 p. 5 |
| nozzle area-ratio mode | CompatibleSupport | TP-1373 p. 7 |
| PLA anchor angles | CompatibleSupport | TP-1034 p. 11; TM X-3261 p. 6 |
| design corrected airflow | CompatibleSupport | TP-1373 |
| flight-thrust method agreement | CrossValidationOnly | TP-1782 |
| **figure 17 normalizer (PW-100(3))** | **Unavailable in held sources**, restricted primary spec | CP2903B classified, verified TP-1056 p. 8 (§6d) |
| NASA 836 approximate SLS full-AB thrust | **AuthoritativeExactTarget**, approximate | NASA/TM-2005-213670 (§6e) |
| net-thrust channel full scale, 30 000 lbf | CompatibleSupport | TP-1034 appendix C p. 28 (§6c) |
| upper bound on the normalizer, ~22 422 lbf | CrossValidationOnly | derived (§6c) |
| installed thrust | Unavailable | — |
| fuel flow | Unavailable | — |
| transient / spool law | Unavailable | — |
| nozzle model | Unavailable | — |
| inlet distortion | Unavailable | — |
| installation geometry | Unavailable | — |

---

## 9. What these four documents do not contain

1. **The figure 17 normalizer** — unavailable in held sources, with a restricted primary
   specification (§6d). Its definition is pinned exactly (uninstalled net thrust at sea level,
   Mach 0, PLA 130°). Four candidates investigated and all rejected — TP-1373's 111.2 kN axis
   scale (§6a), engine 059/063 thrust (§6b), TP-1034's 30 000 lbf channel scale (§6c), and NASA
   836's 23 500 lbf (§6e — different engine family). The third yields a derived upper bound of
   about 22 400 lbf, which applies to the PW-100(3) normalizer only.
2. **Component performance maps as numbers** — published only as graphs, read from data cards.
3. **SGTM coefficients** K1, K2, E, Cv — TP-1782 names them and withholds all four.
4. **The P&W decks themselves** — CCD 1015, CCD 1103-1.0, CCD 1088-2.0 are all cited, none supplied.
5. **Fuel flow as a function of anything a simulation can supply** — fuel flow is an *input* to
   these models, never an output of a published schedule.
6. **A spool time constant.** The rotor inertias are printed, but turbine and fan torque come from
   the maps, so no time constant can be derived without them.
7. **Inlet distortion correction.** TP-1373 printed p. 19 tested two screens and concluded the
   effects were "small and uncorrelatable", explicitly declining to derive a correction.
8. **Installed thrust.** Every thrust in the pack is uninstalled. No inlet spillage, bleed or
   nozzle/boattail interference terms appear.
9. **Any NASA 836 installation geometry** — no mount coordinates, no thrust-line offsets. TP-1782
   establishes only that engine 059 was flown in the **left** position.

---

## 10. Highest-value missing sources, ranked

1. **An unclassified source publishing the F100-PW-100(3) design maximum net thrust** — or,
   equivalently per §6b, that build's sea-level-static maximum-augmentation gross thrust.
   Expected **below ~22 400 lbf** per §6c. The requirement document that would state it, CP2903B,
   is classified and must never be reconstructed — but a performance figure published elsewhere is
   a different document, and §6e shows NASA does publish such figures for the target aircraft.
   Searching remains worthwhile.
2. **NASA TP-1069** (P680059) and **NASA TP-1228** (P680063) — refs. 1 and 2 of TP-1373. These
   hold the absolute gross thrust and airflow that TP-1373 only summarises as percentages, and
   they would populate `MavF100DimensionalGrossThrustDataset` as a **separate** dataset. Note
   what they cannot do: per §6b their test matrices contain no static point, so they cannot
   supply the design-maximum denominator however completely they are read.
3. **NASA TP-1482** — the altitude-facility SGTM evaluation, ref. 7 of TP-1782, and the document in
   which the SGTM coefficients were developed.
4. **Component maps in tabular form** for either build, which would make the printed engine model
   runnable and would close the transient law, fuel flow and nozzle model together.
5. **NASA 836 engine installation geometry** — mount coordinates and thrust-line offsets. Until
   these exist, engine-out yaw stays unmodelled, regardless of thrust.
