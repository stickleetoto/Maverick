# F-15 Flight Physics R2 — Gap Audit

Base: `main` @ `f0f9519`. Written **before** any R2 code change.

**Subject:** the pilot-controlled F-15 (`F15_AFIT_BAUMANN_DAVISON_PILOT_CONTROLLED_V1`), which runs the frozen AFIT / Baumann / Davison research physics under a Maverick control law (V2 active, V1 kept for regression). The frozen research reference (`F15_AFIT_BAUMANN_DAVISON_MACH06_20K_RESEARCH`) is evidence and is not modified.

**Classification:**

| Tag | Meaning |
|---|---|
| **SOURCE-BACKED** | Taken from a named public source, for this model lineage |
| **MAVERICK APPROXIMATION** | Project choice, labelled non-authoritative |
| **MISSING** | Not modelled at all |
| **UNKNOWN** | No held source addresses it |

**Sources read for this audit:**
- the F-15 code under `Scripts/FlightDynamics/F15/`;
- `Docs/Reference/F15_*` (in particular `F15_REMAINING_GAPS_V1.0`, `F15_POST_FREEZE_OPPORTUNITIES_V1.0`, `F15_BAUMANN_SOURCE_CONDITION_AUDIT_V1.0`, `F15_PILOT_CONTROLLED_V1/V2`, `F15_R5_F100_*`);
- the two theses held at `E:\f15-sources\`: Baumann, DTIC ADA217366; Davison, DTIC ADA256613.

Page numbers are PDF pages.

---

## Current pilot physics path

```
MavPilotCommand → MavF15PilotControlLawV2 → MavF15PilotControlMapping (conventions, 0.3 differential tail, envelopes)
  → MavF15ControlActuator (travel clamp; surfaces move INSTANTLY - no sourced rate)
  → MavF15PilotControlledAeroModel (frozen Baumann Mach 0.6 routines, SourceReproduction gate, α/β span gate)
  + MavF15PilotControlledFixedThrust (8,300 lbf total, 0.25-in thrust-line moment, throttle inactive)
  → MavSixDoFBody (source fixed density, source gravity through the load set) → Rigidbody
```

## Item-by-item

| # | Item | Status | Current behaviour | Evidence / notes |
|---|---|---|---|---|
| A | **Atmosphere** | **SOURCE-BACKED** (as the source defines it) | Density is fixed at RHO 0.0012673 slug/ft³ (20,000 ft) at every state; g = 32.174 ft/s². Altitude is reported, not used. | The source has **no altitude state** and never computes Mach (Baumann pp.91, 95–98; Davison pp.91, 124). This is the source's own model, and it is internally consistent: with fixed thrust, the aircraft's energy balance does not depend on altitude. A standard atmosphere is **source-backed as a table** (US Std. Atm. 1976, `MavAtmosphereModel`), but applying it here is **coupled to propulsion** — see §2.1. |
| B | **Propulsion** | **SOURCE-BACKED** (a single research constant) / **MISSING** (a deck) | 8,300 lbf total thrust, constant; a 0.25-in nose-up thrust-line moment; throttle ignored. | Baumann p.34 ("thrust setting for trim … at 0.6 Mach and 20,000 feet"), p.124; Davison App. C p.124 (`THRUST=8300`). An F100 deck is **not publicly available** (`F15_REMAINING_GAPS` G4, G6: design maximum net thrust unavailable, CP2903B restricted, TP-1034 only normalized). No spool, lapse, Mach or altitude dependence. |
| C | **Mach / altitude dependence** | **MISSING** (by source design) | Coefficients take α, β, rates and surfaces only; there is no Mach term. | Baumann p.36: Mach and altitude "fixed at 0.6 and 20,000 feet". No public F-15 dataset with Mach dependence is held or located (`F15_REMAINING_GAPS` G2; A3 low odds). |
| D | **Baumann Mach 0.6 coefficient domain** | **SOURCE-BACKED** (fit condition) + **MAVERICK APPROXIMATION** (runtime span) | SourceReproduction admits 218.5–699.7 ft/s at the fixed density, reporting "extrapolated from the M 0.6 fit". Outside it, aero **and** thrust refuse, and only gravity acts. | The source exercised that V span (Tables III/V/VII). **Davison p.44:** flight test found wing-rock onset ~4 AoA units earlier than predicted, attributed to Mach; "the model aerodynamics were only valid at M=0.6", and the 1-g stall map "was not physically realizable". No widening is defensible. |
| E | **Angle-of-attack range** | **SOURCE-BACKED** | α −4 … 90° (transcribed breakpoint hull); refuses outside. | `MavF15BaumannMach06Domain`: below −4° the polynomials run out of their fit; above 90° the source's compact-support terms diverge (Davison App. C printed pp.134, 140). |
| F | **Sideslip range** | **SOURCE-BACKED** | \|β\| ≤ 20° (breakpoint magnitude hull). | `MavF15BaumannMach06Domain.SourceAbsBetaMaxDeg`. |
| G | **Control-surface authority (travel)** | **MAVERICK APPROXIMATION** | Stab −25…−5°, aileron ±20°, differential ±6° (0.3 × aileron), rudder ±15°; command envelopes, not hard stops. | Four public travel sets conflict (`F15_POST_FREEZE_OPPORTUNITIES` B3); exact values fall under G3. Adopting any one set would pick a side without authority. |
| H | **Actuator dynamics** | **MISSING** in the pilot path, although **a version-matched source exists** | Surfaces move to the bounded command **instantly**. | **Davison App. B `STATE12` FUNX, PDF p.97 (printed 87)**, "Revised 23 Aug 89 … CAS off and Aileron-Rudder Interconnect off":<br>`F(9)=20.*(CDSTBD-DSTBD)`<br>`F(10)=28.*(CDRUDD-DRUDD)`<br>`F(11)=20.*(CDAILD-DAILD)`<br>`F(12)=20.*(.3*CDAILD-DDTD)` (differential tail "acting through the stabilator actuators").<br>These are first-order lags of the same research-model lineage whose aerodynamics the aircraft already flies. **Rate limits: none sourced** (G3). This is the one physics gap with defensible data. |
| I | **Mass / inertia** | **SOURCE-BACKED** | 37,000 lb; Ix 25,480, Iy 166,620, Iz 186,930, Ixz −1,000 slug·ft². | Davison App. C p.124 (the research model's own values). NASA 836 Table 1 states (`MavF15Table1MassStates`) belong to the exact path and a different configuration; mixing them is refused. |
| J | **Trim dependence** | **SOURCE-BACKED** | The start state is Table VII point 36 (V 300.8 ft/s, α ≈ 17.5°), recovered by the WP-3B solver. The V2 gains were designed at this point. | Table VII (Baumann pp.124–129); `F15_RESEARCH_TRIM_SOLVER_V1.0`. Only symmetric/turning equilibria at the source condition exist; there is no trim at other altitudes, because the source has no altitude. |
| K | **High-speed behaviour** | **SOURCE-BACKED limit** | Refused above 699.7 ft/s (≈ M 0.675 at 20,000 ft). | Baumann discounts velocities "much higher" than 622 ft/s (pp.118–119): compressibility. No transonic data is held. |
| L | **Low-speed / stall behaviour** | **SOURCE-BACKED data, source-refuted validity** | Refused below 218.5 ft/s. High-α terms exist inside the α span. | Davison p.44 (above): the model's stall behaviour did not match flight at M ≈ 0.35. Extending the low-speed domain would present data the source itself calls "not physically realizable" at those Mach numbers. |
| M | **Dynamic-pressure behaviour** | **SOURCE-BACKED** | q̄ = ½·RHO·V² with the fixed RHO, as in the source (`QBARS`, Baumann pp.91, 101). The thrust coefficient scales 1/q̄ because the force is constant (source). | — |
| N | **Aerodynamic damping** | **SOURCE-BACKED** | Clp, Cmq (CMMQ), Cnr, Cyp, Cyr and cross terms from the fit. Nothing artificial on the plant. | **Known source issue:** the printed CMMQ is **positive** over α ≈ 10.6–14.3° (WP-3D / WP-3E: all public printings agree; "executed AUTO model may differ"). A pilot who holds α in that band flies with destabilizing pitch damping from the source. It is recorded, not tuned (the McDonnell 1990 refit is not held; download needs permission). |
| O | **Control-law dependence on the limited aero model** | **MAVERICK APPROXIMATION** | V2 gains (MAVERICK_TUNED_NON_AUTHORITATIVE) were designed at point 36 **on instantaneous surfaces**, with q̄ scheduling against the fixed-density q̄. | Every V2 value is labelled. Any plant change that adds phase lag (e.g. item H) changes the loop V2 closes and must be checked, not assumed. The washed-out yaw-rate term is labelled MAVERICK GAMEPLAY / RESEARCH ASSIST. There is no fake plant damping to migrate. |

## 2. What this means for R2

### 2.1 Atmosphere — **not implemented**. Decision and reason.

A variable-density atmosphere changes q̄ at every altitude, and the coefficients themselves (being nondimensional) would carry over. **But thrust is a fixed 8,300 lbf, chosen for 20,000 ft**, and no sourced lapse exists (item B).

Consequences of ISA with fixed thrust:
- **Above 20,000 ft:** the aircraft would have too much thrust. At 40,000 ft, density falls to ~44 % while thrust stays at 100 %.
- **Below 20,000 ft:** it would have too little.

The combination is **less** physically consistent than the source's own model, which is consistent at every altitude precisely because it has no altitude. Scaling thrust by a density ratio would be an invented lapse law. The blocker is recorded as **atmosphere requires a sourced thrust deck first (G6)**.

The SOURCE CONDITION (RHO 20,000 ft, g 32.174) stays the RUNTIME ATMOSPHERE of this aircraft, and remains documented as such.

### 2.2 Propulsion — **not implemented**

No public F100-PW-100 dimensional deck exists (G4, G6). A time-boxed search on 2026-09-28 found only the families R5 already classified: NASA altitude-facility reports, with no static point. The fixed research thrust stays.

### 2.3 Aerodynamic domain — **not widened**

Davison's flight-test finding (p.44) rules out the low-speed and stall extension. Compressibility rules out the high-speed extension. The domain stays and stays explicit, with refusal and status.

### 2.4 Actuator dynamics — **implemented in R2** (see `F15_PHYSICS_R2.md`)

The only gap with version-matched public data: Davison `STATE12`'s first-order surface lags.

**Scope:**
- **Lags only.** There are no rate limits (none sourced) and no change to travel (Maverick envelopes stay).
- **Isolated from the frozen research path**, which holds surfaces static.
- **Versioned:** `R2SourceActuatorLags`. **R1 (instantaneous) stays the default**, so every recorded V1/V2 pilot result reproduces exactly.

### 2.5 Trim — unchanged by construction

A first-order lag has unit static gain. The surfaces start snapped to the trim bias, so point 36 remains an equilibrium of the R2 plant. R2 verifies this by residual rather than assuming it.

### 2.6 Mass / inertia, travel, damping, V2 gains — unchanged

There is no stronger evidence (items G, I, N). V2 is retuned only if the lagged plant makes it objectively unstable.
