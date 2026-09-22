# F-15 R5 — F100-PW-100 source audit

**Status:** V0.1, complete for the four documents supplied in `F100_R5_CLAUDE_SOURCE_PACK.zip`.
**Target aircraft:** `NASA_F15B_836_SN74_0141_PRE_QUIET_SPIKE_BASELINE_F100_PW_100`
**Target engine:** Pratt & Whitney F100-PW-100 × 2.

This document records what the four NASA reports actually contain, field by field, before any of it
was implemented. Section 9 records what they do **not** contain, which in this pack is the more
consequential half.

---

## 1. The finding that shapes the whole pass

**No absolute thrust value for any flight condition and power setting appears anywhere in the four
documents.**

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
| Gross-thrust axis normalizer 111.2 kN | TP-1373 | figs. 6(b),6(d) | series 2 7/8 | N | printed axis scale | **not engine data** | recorded explicitly so it is not mistaken for the missing design maximum |
| Flight envelope actually flown | TP-1782 | printed p. 1 | series 2 7/8, engine 059 LEFT | Mach, m | printed prose | CrossValidationOnly | recorded; never used as a source envelope |
| SGTM/GGM agreement ±3 % | TP-1782 | printed pp. 1, 17 | series 2 7/8 | percent | printed prose | CrossValidationOnly | recorded as an oracle bound |
| Design parameters (inertias, volumes, cp, bleeds, HVF) | TM X-3261 tab. I p. 53; TP-1034 tab. I | both | SI | printed table | CompatibleSupport | **not implemented** — see §7 |

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
| **absolute thrust scale** | **Unavailable** | — |
| installed thrust | Unavailable | — |
| fuel flow | Unavailable | — |
| transient / spool law | Unavailable | — |
| nozzle model | Unavailable | — |
| inlet distortion | Unavailable | — |
| installation geometry | Unavailable | — |

---

## 9. What these four documents do not contain

1. **Design maximum net thrust** — the normalizer of figure 17. Its definition is pinned exactly
   (uninstalled net thrust at sea level, Mach 0, PLA 130°); its value is printed nowhere.
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

1. **Design maximum net thrust for the F100-PW-100(3)**, from a P&W status/specification deck or an
   installed-thrust report that states it. This single scalar converts an implemented, tested,
   envelope-checked characteristic into dimensional thrust. Nothing else in this list comes close.
2. **NASA TP-1069** (engine P680059 calibration) and **NASA TP-1228** (P680063 altitude
   calibration) — cited as refs. 1 and 2 of TP-1373. These are the facility reports whose data
   TP-1373 only summarises as percentages, and they are the most likely public source of absolute
   gross thrust and airflow at stated conditions.
3. **NASA TP-1482** — the altitude-facility SGTM evaluation, ref. 7 of TP-1782, and the document in
   which the SGTM coefficients were developed.
4. **Component maps in tabular form** for either build, which would make the printed engine model
   runnable and would close the transient law, fuel flow and nozzle model together.
5. **NASA 836 engine installation geometry** — mount coordinates and thrust-line offsets. Until
   these exist, engine-out yaw stays unmodelled, regardless of thrust.
