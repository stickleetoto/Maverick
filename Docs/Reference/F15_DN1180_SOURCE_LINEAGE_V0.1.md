# F-15 DN-1180.01-238-458 Rev. D — Source Lineage V0.1

**Question:** Can McDonnell Aircraft's *F-15 Flight Control System Description*, Design Note DN-1180.01-238-458 Rev. D (October 1981), be acquired from a public source? If not, which public documents reproduce F-15 control-system data, and from what lineage?
**Also audited:** NASA TM-72861 (NTRS 19790015808), as an independent check.
**Code touched:** the provenance text in `MavF15FlightControlSystem.cs` only. **No gain, schedule or stage changed.**

---

## 1. Result

| | |
|---|---|
| **DN-1180.01-238-458 Rev. D original** | **NOT FOUND.** No public copy located (NTRS, the Internet Archive DTIC mirror, general web search). DTIC direct access is blocked to scripted requests and was not circumvented. |
| Public documents citing it | **One:** Nolan, AFIT/GAE/ENY/92J-02 (DTIC ADA256438). |
| Numeric values reproduced *from DN-1180* | **None found.** Nolan cites it for qualitative statements only. |
| NTRS 19790015808 | **This is NASA TM-72861 itself** (Sisk & Matheny, *Precision Controllability of the F-15 Airplane*, May 1979, H-1073). "Compare 19790015808 with TM-72861" is therefore a comparison of one document with itself. The meaningful comparison is TM-72861 **against the DN lineage**, made in §5. |
| TM-72861 lineage | Independent of DN-1180. It predates Rev. D by 2½ years, and its control-system appendix cites **AFFTC-TR-74-8** and **AFFTC-TR-76-48**. |
| FCS stages moved from Unavailable | **None.** |
| Correction made | The code and the R3 FCS status doc said TM-72861 publishes the four CAS feedback gains. **It does not.** Both are corrected (§6). |

---

## 2. Identity and the one public citation

**As cited** — Nolan ADA256438, bibliography ref. 14, printed p.F-2 / PDF p.122, checked against the rendered page:

> McDonnell Aircraft Company. *F-15 Flight Control System Description*. Design note DN1180.01-238-458 (Rev D), October 1981.

**What Nolan attributes to it** (PDF p.41–42, printed pp.2-21 – 2-22, p.42 checked against the rendered page):

| Statement | Citation | Numbers? |
|---|---|---|
| Differential tail, "disregarding control augmentation system inputs, is directly proportional to aileron deflection" | (14:15) — DN-1180 p.15 | **no ratio given** |
| "a primary mechanical control system operating in parallel with an electronic dual-channel three-axis control augmentation system (CAS)" | (14:20) — DN-1180 p.20 | no |
| The CAS "uses angular velocity feedback"; the system "also includes a stall inhibitor system … and an aileron-rudder interconnect" | follows the (14:20) sentence, uncited itself | no |

Nolan also records that "control surface deflection limits are neglected" in his model (PDF p.42).

---

## 3. Public F-15 control-system material, by item

Lineage × scope as in `MavF15SourceLineage`. **Nothing in this table is wired into the control law.**

| Item | Public value / statement | Source (page) | Lineage × scope |
|---|---|---|---|
| **PRAD** | "scheduled as a function of dynamic pressure and Mach number". No schedule printed. | TM-72861 p.12 (PDF p.14) | OriginalPrimary × Preproduction |
| **RRAD** | Scheduled with collective stabilator position. Aileron-to-stick gearing, deg per cm lateral stick, **plot only** (fig. 20). "Gearing ratio is **2.0** with gear down regardless of mechanical stabilator command." | TM-72861 p.12; fig. 20 p.40 (PDF p.42), from AFFTC-TR-76-48 | OriginalPrimary × Preproduction |
| **Pitch CAS** | "blends airplane normal acceleration and washed-out pitch rate to form a C* feedback system". Max authority **±10° stabilator**, reduced by the variable limiter "when mechanical stabilator commands exceed **5° and −19°**". **No feedback gains printed.** | TM-72861 p.12 | OriginalPrimary × Preproduction |
| **Roll CAS** | "provided through the stabilator CAS series servos according to the schedule shown in figure 21". Fig. 21 is the roll CAS **feel** characteristic (stick force vs commanded roll rate, ±13.3 N breakout), not a gain. **No feedback gain printed.** | TM-72861 p.12; fig. 21 p.41 | OriginalPrimary × Preproduction |
| **Yaw CAS** | Primary purpose Dutch-roll damping; "limited to **±5°** of rudder deflection". **No feedback gain printed.** | TM-72861 p.13 (PDF p.15) | OriginalPrimary × Preproduction |
| **Differential stabilator** | "**0.3°** of differential stabilator per degree of aileron deflection except where restricted by the stabilator actuator limits" | TM-72861 p.12 | OriginalPrimary × Preproduction |
| | δDT = 0.3 δA, "fixed by the manufacturer" | Baumann ADA217366 PDF p.34 | PublicReproduction × ProductionF15Family |
| | `DTALD = 0.3*DAILD` (ARO10 comment, stale in Davison's version) | Davison listing printed p.126–127 | DerivedSimulator × ProductionF15Family |
| **Rudder pedal gearing** | "fixed at **1.8°** of rudder per centimeter of differential pedal position" | TM-72861 p.13 | OriginalPrimary × Preproduction |
| **ARI** | Scheduled with stabilator position for two flap settings (fig. 23, plot only). Flaps up: acts above ≈**3° α**, all speeds below **Mach 1**. Three gradients: ≈0 near neutral, **0.55°** proverse rudder per degree aileron (back stick), **0.85°** adverse per degree (forward stick). "There is still some question as to whether it is optimum." | TM-72861 p.13; fig. 23 p.43 (PDF p.45) | OriginalPrimary × Preproduction |
| | ARI fed through a washout "(s/s+1)" that filters low-frequency inputs such as wing rock | Davison ADA256613 PDF p.66–67 and p.74 (uncited) | PublicReproduction × ProductionF15Family, `CROSS_VALIDATION_ONLY` |
| **Roll-to-yaw crossfeed** | none found | — | Unavailable |
| **Turn coordination** | none found beyond the ARI description | — | Unavailable |
| **Stall inhibitor** | exists (Nolan PDF p.42; Davison) — no threshold or gradient printed | — | Unavailable (numbers) |
| **Roll-damper washout** | Figure 27, "Washout Schedule for F-15 Roll Damper": gain plateau ≈5.0 from α = 0, then a linear fall to zero. Text: "After an angle of attack of **20.2 degrees**, the gain is set to zero, identical to the schedule found in the F-15." The recommendation traces to AFFTC-TR-75-32. **Start α is plot-only** (≈7–8°); gain units unstated. | Davison ADA256613 printed p.67–68 / PDF p.77–78 (rendered page checked) | PublicReproduction × ProductionF15Family |
| **Surface travel** | ailerons ±20; stabilators **15, −26**; rudders ±30 | TM-72861 table 1 p.15 | OriginalPrimary × Preproduction |
| | aileron ±20; horizontal tail **29° down, 15° up**; rudder ±30; flap 30° down; speed brake 45° up | McDonnell ADA230462 PDF p.83 (and Fero, Davison, Nolan) | PublicReproduction × ProductionF15Family |
| | aileron **+30°**; stabilator **+20 to −30°** | Baumann ADA217366 Table VI, PDF p.87 | PublicReproduction × ProductionF15Family |
| | aileron ±20; symmetric stabilator **+15/−25**; differential ±20; rudder ±30 | CR-186019 table 2, p.4 | DerivedSimulator × NotRepresentativeOfAnyAircraft |
| **Actuator rate / dynamics** | 24 deg/s all surfaces; first-order 20/(s+20) | CR-186019 p.4 | DerivedSimulator × NotRepresentativeOfAnyAircraft |
| **Limiter schedules** | CAS variable limiter uses "dynamic pressure and angle of attack limit schedules". Not printed. | TM-72861 p.12 | OriginalPrimary × Preproduction |

> **Addendum (V1 freeze).** This table missed an exact-scope source that was already held: **NASA 836's own simplified pitch, roll and yaw control models**, NASA/TM-2009-214651 figs. 3–5 (printed pp.26–27; repeated as TM-2012-215978 figs. 3–5). They are OriginalPrimary × **Exact836**, and structure only:
> - pitch: PRAD fed by Pt/Ps and Pt − Ps; a mechanical path with a normal-acceleration gain and integrator; a CAS with stick gradient, prefilter, structural filter and integrator; a stall inhibitor on α and pitch rate; washed-out pitch rate blended with normal acceleration; gear-switched compensation;
> - roll: RRAD fed by the symmetric-stabilator mechanical system and calibrated airspeed; a roll-rate CAS with α / calibrated-airspeed limiter schedules;
> - yaw: a mechanical ARI **switched out above Mach 1.5**; a roll-yaw crossfeed (α × roll rate) **switched out above Mach 1.0**; yaw-rate and lateral-acceleration feedback through proportional-plus-integral to the CAS servo.
>
> No gain value is printed. The two Mach switch points are the only numbers.

**The surface-travel rows disagree.** The stabilator limit appears as 15/−26, 15 up/29 down, +20/−30 and +15/−25, with unstated sign conventions and configurations. That disagreement is itself a reason not to promote any of them without the original.

---

## 4. Source graph

```
AFFTC-TR-74-8 (Jones & Winters, Apr 1974) ───┐
AFFTC-TR-76-48 (Tanaka & Huete, Jul 1977) ───┼─► NASA TM-72861 appendix + figs 17–23 (May 1979; F-15 No. 8)
                                             │      [OriginalPrimary × Preproduction]
AFFTC-TR-75-32 (Wilson & Winters, Jan 1976; AD-B045115, limited distribution — not sought)
  └─► roll-damper washout recommendation ──► Davison 1992 fig. 27 (zero at 20.2°)  [PublicReproduction]

McAir ARO10 / F-15 baseline simulator ──► "DTALD = 0.3*DAILD" (Davison listing)  [DerivedSimulator]
"the manufacturer" ──► Baumann 1989 "δDT = 0.3 δA"  [PublicReproduction]

DN-1180.01-238-458 Rev. D (McAir, Oct 1981) ── NOT HELD
  └─► Nolan 1992 (pp.15, 20 cited; qualitative only)  [no numeric reproduction]

NASA Dryden-derived model ──► Brumbaugh 1991, CR-186019 (actuators, limits; "not representative")
```

**TM-72861 cannot be a DN-1180 reproduction.** It was published in May 1979; Rev. D is dated October 1981. Its own detailed-description reference is AFFTC-TR-74-8 (its ref. 3), and every control-system figure is marked "(ref. 4)", which is AFFTC-TR-76-48.

The two lineages agree where they overlap: the 0.3 differential ratio appears in TM-72861, in Baumann's "fixed by the manufacturer", and in the ARO10 comment. They disagree on surface travel (above).

---

## 5. TM-72861, at the authority it supports

- **Aircraft:** "F-15 number 8", formerly the spin-research airplane, "had a preproduction control system". During the program the control system "was upgraded to meet production standards with regard to friction, hysteresis, and breakout forces", and the ARI was replaced with a production unit. Only "the last four tracking flights" came after that upgrade (p.5, PDF p.7).
- **Grade:** OriginalPrimary × **Preproduction**. This matches `MavF15FcsModes.F15FamilyReference`'s `PublicReference` ceiling. It is **not** NASA 836 authority.
- **What it does not contain:**
  - numeric pitch-rate, normal-acceleration, roll-rate or yaw-rate feedback gains;
  - a PRAD schedule;
  - stall-inhibitor numbers;
  - crossfeed or turn-coordination gains;
  - actuator rates.

  The RRAD and ARI schedules are plots only.

---

## 6. Correction to earlier claims

The R3 work said TM-72861 "carries F-15-family CAS architecture **and gains**", listed it as the source of the four CAS feedback gains, and ranked it as blocker #2 for that reason.

Read in full, that is wrong. The report publishes architecture, authorities and a few gearings, but no feedback gain.

**Corrected in this pass:**
- `MavF15FlightControlSystem.cs`: the `MavF15ControlGain` summary and the `cas` unavailability string. Text only; no value or grade changed.
- `F15_R3_FCS_IMPLEMENTATION_STATUS_V0.1.md` §6 (rows for the four feedback gains) and §10 (blocker #2).

---

## 7. FCS stage status

**No stage moved from Unavailable.** For each candidate:

| Stage | Nearest public value | Why it did not move |
|---|---|---|
| PitchCas / RollCas / YawCas feedback gains | none printed anywhere found | nothing to move |
| MechanicalPath gearings | 1.8°/cm rudder-pedal, 0.3 differential-per-aileron, RRAD 2.0 gear-down | **Units mismatch.** The code's gearings are per unit of stick/pedal command. TM-72861's differential ratio is per degree of *aileron*, which depends on RRAD, which is plot-only. Preproduction scope in any case. |
| ARI | 0.55 / 0.85 gradients, 3° α, M < 1 | A schedule against stabilator position, plot-only. The code's single `ariRudderPerAileron` cannot represent three gradients without inventing the blend. |
| HighAoaRollDamperWashout | zero at 20.2° (text); start ≈7–8° (plot) | The start α and curve shape would come from a plot reading of a thesis figure, and the gain units are unstated. Not "explicitly closed by a public primary source". |
| StallInhibitor, crossfeed, turn coordination | none | nothing to move |
| Surface travel / actuator | four conflicting public sets | conflict unresolved; none is 836 |

---

## 8. What would close it

1. **DN-1180.01-238-458 Rev. D** itself, which remains the single highest-value FCS document.
2. **AFFTC-TR-76-48** and **AFFTC-TR-74-8**, the sources behind TM-72861's figures. They are AFFTC reports; their distribution status was not checked in this pass.
3. For the washout: the AFFTC-TR-75-32 recommendation (limited distribution), or a NASA 836 source stating the schedule.

**If no originals are public, say so plainly: none of the three McDonnell / AFFTC originals was found public in this pass.**
