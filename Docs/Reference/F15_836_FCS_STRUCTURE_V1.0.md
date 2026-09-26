# F-15 — NASA 836 Flight-Control STRUCTURE (V1.0, WP-2)

**Scope:** check every R3 control-law stage against NASA F-15B 836's own simplified control block diagrams.
- Record what those diagrams establish as **exact 836 structure**.
- Add the two Mach switches they print as **structural logic**.

**No gain, schedule, limit or rate appears on these diagrams, and none was inferred.** The exact law still outputs neutral.

**Code:** `F15/MavF15Nasa836FcsStructure.cs`, `F15/MavF15ControlLaw.cs` (switch gating)
**Tests:** `Validation/MavF15Nasa836FcsStructureValidation.cs` `[S1]`–`[S7]`, 24 checks, PASS

---

## 1. Sources — read on the rendered pages

| Diagram | NASA/TM-2009-214651 | NASA/TM-2012-215978 |
|---|---|---|
| Pitch (fig. 3) | PDF p.30, printed p.26, art 090121 | PDF p.22, printed p.18, art 110195 |
| Roll (fig. 4) | PDF p.30, printed p.26, art 090122 | PDF p.22, printed p.18, art 110196 |
| Yaw (fig. 5) | PDF p.31, printed p.27, art 090123 | PDF p.23, printed p.19, art 110197 |

- **TM-2009-214651:** McWherter, Moua, Gera, Cox, *Stability and Control Analysis of the F-15B Quiet Spike™ Aircraft*, Aug 2009.
- **TM-2012-215978:** Moua, McWherter, Cox, Gera, *Flight Test Results on the Stability and Control of the F-15 Quiet Spike Aircraft*, 2012.
- The two reports carry the same three diagrams, redrawn. Figure 5 was checked at 170 dpi in both.

**Scope of the diagrams.** Both reports caption them as the control model of the NASA DFRC F-15B test airplane, which is 836. The reports concern its Quiet Spike configuration, but neither reports any change to the control system for the spike. The structure is therefore taken as the aircraft's own. Every box is labelled generically — "Gain", "Filter", "Compensation", "Stall inhibitor" — and carries no number. `MavF15Nasa836FcsStructure.AnyNumericFcsDataShown = false`.

## 2. What the diagrams draw

**Pitch**
- **Mechanical path:**
  - pitch stick → × pitch ratio adjust device (fed by Pt/Ps and Pt − Ps);
  - a normal-acceleration gain feeds a mechanical integrator;
  - then boost actuator → power cylinder → symmetric stabilator.
- **CAS:**
  - stick gradient → prefilter;
  - summed with the stall inhibitor (fed by angle of attack **and** pitch rate) and with gear-switched compensation of washed-out pitch rate plus normal acceleration;
  - then structural filter plus integrator → CAS servo, and a CAS interconnect servo into the mechanical path.

**Roll**
- **Mechanical path:** lateral stick × roll ratio adjust device (fed by the symmetric-stabilator mechanical system and calibrated airspeed) → gain → power cylinder → aileron. The same product also feeds the differential-tail power cylinder.
- **CAS:** stick gradient → filter, summed with roll rate × gain → limiter scheduled on angle of attack and calibrated airspeed → CAS servo → differential tail.

**Yaw**
- **Mechanical aileron-rudder interconnect:** (symmetric-stabilator mechanical system → gain → filter) × (lateral stick → filter) → rudder power cylinder. **Both inputs pass through switches labelled "Mach > 1.5" that select 0.**
- **Pedal:** pedal → gain into the mechanical system, and pedal → gain → filter into the CAS.
- **Roll-yaw crossfeed:** angle of attack × roll rate → compensation → CAS summing junction. **The angle-of-attack input passes through a switch labelled "Mach > 1.0" that selects 0.**
- **Feedback:** yaw rate → structural filter → compensation, plus lateral acceleration (sensor location) → gain → structural filter.
- **CAS output:** proportional plus integral → CAS servo.

## 3. Stage-by-stage classification

Two columns:
- **Class** — does 836 have this stage?
- **R3 topology** — how much of the drawn stage R3 represents.

| R3 stage | Class | R3 topology | What R3 lacks, or does differently |
|---|---|---|---|
| `MechanicalPath` | **EXACT_836_STRUCTURE_CONFIRMED** | simplified subset | the mechanical pitch path's normal-acceleration gain and integrator |
| `PitchRatioChanger` (PRAD) | **EXACT_836_STRUCTURE_CONFIRMED** | matches | — (scales mechanical pitch only, as drawn; inputs unmodelled for lack of a schedule) |
| `RollRatioChanger` (RRAD) | **EXACT_836_STRUCTURE_CONFIRMED** | matches | — (scales aileron **and** differential tail, as drawn) |
| `PitchCas` | **EXACT_836_STRUCTURE_CONFIRMED** | simplified subset | stick command path, washout, gear switch, compensation, structural filter, integrator, interconnect servo |
| `RollCas` | **EXACT_836_STRUCTURE_CONFIRMED** | simplified subset | stick command path; the AoA / airspeed-scheduled limiter. Output on the differential tail matches. |
| `YawCas` | **EXACT_836_STRUCTURE_CONFIRMED** | simplified subset | lateral-acceleration feedback, pedal command path, proportional plus integral, filters |
| `AileronRudderInterconnect` | **EXACT_836_STRUCTURE_CONFIRMED** | simplified subset | constant gain where the diagram multiplies by a stabilator-dependent signal; no filters |
| `StallInhibitor` | **EXACT_836_STRUCTURE_CONFIRMED** | **routing differs** | R3 uses AoA only and adds to the stabilator whether or not the pitch CAS is engaged. The diagram feeds it pitch rate too and routes it through the pitch CAS. |
| `RollToYawCrossfeed` | **EXACT_836_STRUCTURE_CONFIRMED** | simplified subset | constant gain where the diagram multiplies by angle of attack; no compensation |
| `HighAoaRollDamperWashout` | **F15_FAMILY_STRUCTURE_ONLY** | — | Not drawn. The roll CAS's AoA dependence is drawn as a scheduled **limiter**. R3's washout comes from Davison fig. 27, a public reproduction of a production-F-15 schedule. Not a contradiction: the diagram is simplified. |
| `TurnCoordination` | **NOT_SHOWN_FOR_836** | — | Not drawn, and in no family or research source held. 836 draws lateral-acceleration feedback instead. |
| actuator path (not a stage) | **EXACT_836_STRUCTURE_CONFIRMED** | simplified subset | R3's four channels are exactly the diagram's four outputs: symmetric stabilator, aileron, differential tail, rudder. No boost-actuator, power-cylinder or servo dynamics modelled; none printed. |

**AFIT_RESEARCH_ONLY** and **CONTRADICTED** apply to no stage.

## 4. Exact structural logic added — the two Mach switches

| Switch | Threshold | Signals switched | Verified on | In code |
|---|---|---|---|---|
| Mechanical ARI | **Mach > 1.5** | both multiplier inputs | fig. 5 of both reports, rendered | `MavF15Nasa836FcsStructure.AileronRudderInterconnectSwitch` |
| Roll-yaw crossfeed | **Mach > 1.0** | angle-of-attack input | same | `MavF15Nasa836FcsStructure.RollYawCrossfeedSwitch` |

**Semantics.**
- The labels read ">", so each switch is open strictly above its threshold and closed at exactly the threshold.
- A non-finite Mach leaves the switch position undetermined, so the stage is held out.
- A switch not marked `verifiedOnRenderedPage` is never applied.

**Where they act.**
- Only in `MavF15FcsMode.ExactNasa836Unavailable`, inside `ApplyAri` and `ApplyRollToYawCrossfeed`, after the gain-availability check.
- They can only remove a stage. With the exact gains unavailable, `Solve` returns before either stage is reached, so the exact output is neutral with or without them (`[S5]`, `[S7]`).
- Family and research modes are **not** gated: they draw on other sources, and those were not checked for these switches.
- In exact mode the stage report now appends each stage's 836 class.

## 5. Findings that change the R3 record

1. **R3's open ARI question is settled for 836.** R3 asked whether the ARI takes lateral stick before or after the roll ratio changer. The 836 yaw diagram takes lateral stick directly, through its own filter. R3's choice (raw roll command, not RRAD-scaled) matches, so the `ApplyAri` comment no longer calls it open. The gain is still unavailable.
2. **Stall inhibitor routing differs**, and is **recorded, not changed**:
   - On 836 it acts through the pitch CAS, and it takes pitch rate as well as AoA.
   - R3 adds it independently of pitch-CAS engagement, from AoA alone.
   - There is no numeric consequence today, because the stage has no sourced threshold or gradient.
   - Gating it on pitch-CAS engagement in exact mode is a candidate follow-on. It would only remove output. It was not in the WP-2 brief's list of structural facts to add.
3. **Turn coordination is not an 836 element.** The yaw axis coordinates through lateral-acceleration feedback in the CAS.
4. **The ARI and the crossfeed are gated differently, as drawn:**
   - the ARI sits in the **mechanical** system, so it is not gated on yaw CAS;
   - the crossfeed feeds the **CAS**, so it is.

   R3 already had both right.

## 6. Tests — `MavF15Nasa836FcsStructureValidation`

| Check | What it holds |
|---|---|
| `[S1]` | every stage graded and cited; the nine confirmed stages; washout family-only; turn coordination not shown; none contradicted; stall-inhibitor routing difference recorded; actuator path graded; no numeric data |
| `[S2]` | ARI switch at 1.5, verified, cited; closed at 1.4 and 1.5, open above; an unverified switch never applies |
| `[S3]` | crossfeed switch at 1.0 on the AoA input, verified; closed at 0.95 and 1.0, open above |
| `[S4]` | exactly two switches; no other stage is ever switched out; unknown Mach holds both out |
| `[S5]` | the switch facts alone never activate an unavailable gain, even with only the switched stages' gains declared |
| `[S6]` | with SYNTHETIC gains: ARI on at 1.4 and 1.5, off at 1.6; crossfeed on at 0.9 and 1.0, off at 1.1. The rudder loses exactly the ARI contribution. Switches never add a stage. Research mode is ungated. |
| `[S7]` | exact law at Mach 0–2 × α −5…30° at full stick: neutral, 0 of 11 stages applied |
