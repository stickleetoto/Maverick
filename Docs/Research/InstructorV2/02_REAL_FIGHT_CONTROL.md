# 02 — Real Fighter Flight Control: What the Public Record Supports

**Purpose:** collect the publicly verifiable principles of fighter flight-control laws that a physically honest mouse instructor can reuse, and mark clearly what is *not* publicly known.

**Evidence classes:** `PRIMARY_VERIFIED` (PV — read in the primary source), `SECONDARY_VERIFIED` (SV — read in a secondary source or only in a primary abstract/record), `ENGINEERING_INFERENCE` (EI), `UNVERIFIED` (UV). Source keys resolve in `07_RESEARCH_SOURCES.md`, which also records how each was inspected.

**How the external sources were read:** a research sub-agent of this session read the NASA reports (several are scans; pages were rendered and read as images) and author-hosted copies of journal papers. Figure readings of individual gains from scanned block diagrams are flagged *moderate confidence* and must be re-checked against the PDF before any number is used.

> **Configuration warning.** The richest public control-law description for an "F-16" is a **1979 NASA Langley piloted-simulation study** of an early F-16 configuration with relaxed static stability (`[NASA-TP-1538]`). The richest public description for an "F-15" CAS is a **1975 NASA simulation of the early full-scale F-15 mechanical system + analog CAS, flown on a 3/8-scale remotely piloted model** (`[NASA-TN-D-7941]`). Neither is the production FCS of any current F-15C/E or F-16C/D block. Maverick's own laws are, and stay, `MAVERICK_TUNED_NON_AUTHORITATIVE`.

---

## 1. F-16: the TP-1538 simulated flight-control system

`[NASA-TP-1538]` Appendix A (report pp. 34–35) and Figs. 62–65 (pp. 210–217) describe the simulated control system, including gains, filters, schedules and actuator models. **PV** unless noted.

### 1.1 Pitch — normal-acceleration command with α/g limiting

- Command augmentation through a force-sensing sidestick: the pilot commands **normal acceleration**.
- Feedbacks: washed-out pitch rate plus filtered normal acceleration; a forward-loop integrator drives steady-state acceleration toward the command; artificial static stability from α feedback (needed because the configuration is statically unstable at the studied CGs).
- Command limiter: upper limit +8 g **incremental**; lower limit scheduled with q̄ (roughly −1 g at low q̄ to about −4 g at higher q̄, Fig. 62b).
- **α/g limiter:** α feedback reduces the commanded-g ceiling by 0.322 g/deg between α = 15° and 20.4°, and by 1.322 g/deg above 20.4°. With +8 g available up to 15°, the ceiling reaches zero near α ≈ 25°, i.e. **an α limit of about 25° in 1-g flight** for this simulated system. (EI consistency check: 8 − 0.322·5.4 ≈ 6.26 g at 20.4°; 6.26 − 1.322(α − 20.4) = 0 → α ≈ 25.1°.)
- The often-quoted "9 g" corresponds to 8 g incremental + 1 g only by inference (EI); a production-block 9 g / 25° limiter is **UV** here.
- Gain scheduling with q̄ on the pitch loop and on the anticipating pitch-rate path (Figs. 62c–d).
- Stabilator actuator: first-order lag 0.0495 s, 60 °/s, ±25° (figure reading; moderate confidence for individual gains).

### 1.2 Roll — roll-rate command

- Roll-rate command up to **308 °/s** from lateral stick force; roll-rate feedback; ailerons plus differential tail at 4:1.
- Actuators: aileron 80 °/s, differential tail 60 °/s, ±21.5° / ±5.38°.
- Above α = 29° an automatic departure/spin-prevention mode drives the roll surfaces from yaw rate and disengages the roll-rate CAS.

### 1.3 Yaw — damper on stability-axis yaw rate, ARI

- Yaw damper feeds back **r − pα** (≈ stability-axis yaw rate) and lateral acceleration, washed out.
- Aileron–rudder interconnect (ARI) with an α-dependent gain at low speed.
- Pedal authority faded to zero between α = 20° and 30°.
- **ARI and roll reversal:** with the lateral control divergence parameter
  `LCDP = C_nβ − C_lβ (C_nδa + G_ARI C_nδr) / (C_lδa + G_ARI C_lδr)`,
  the unaugmented aircraft's LCDP goes negative (roll reversal) above about α = 25°; the ARI keeps it positive to about α = 40° (report p. 13, Fig. 14).

### 1.4 Departure and inertia coupling

- The basic system resisted classical yaw departure but was susceptible to **inertia-coupling pitch departures in rapid, large-amplitude rolls at low speed**, and had a deep-stall trim (abstract, pp. 18–19).
- A research modification ("control system B") cut the maximum commanded roll rate from 308 °/s to as little as 80 °/s as functions of q̄, α above 15° and nose-down stabilator demand. The stated reason: the nose-up inertia-coupling moment grows with sin 2α while nose-down control margin falls with α and q̄.

**Relevance to Maverick (EI):** the public F-16 architecture is *exactly* what Maverick's F-16 V0.1 law structurally imitates (n command, α/g limiter, roll-rate command with α fade, ARI, washed-out yaw damper). One difference is visible: TP-1538's damper feeds back **r − pα** (stability axis), while `MavF16ControlLawV01` washes out **body-axis** r (doc 05 C2). This is public evidence supporting the P2 recommendation to move the F-16 law's damper (and roll loop) to stability axes — as a control-law work package, not inside the instructor.

**Repository note:** `Docs/Research/Aerospace/Aircraft/F16/F16_FCS_SOURCE_GRAPH.md` lists open questions F16-Q-C1 ("does TP-1538 print its CAS gains?") and F16-Q-C2 (actuator model). This research indicates **yes** for both (Appendix A, Figs. 62–65). The source library should be updated by its own owner after a page-level transcription; this research branch does not edit it. Note also that Maverick's F-16 aerodynamics are Morelli's compact fit, not TP-1538's tables, and the TP-1538 configuration (CG cases, relaxed stability) must not be silently combined with Maverick's frozen Morelli/NASA mass reference.

---

## 2. F-15: CAS architecture (early full-scale system)

### 2.1 `[NASA-TN-D-7941]` (Edwards & Deets, 1975) — PV

- **Mechanical system:** hydraulic boost; stick → collective stabilators; lateral stick → ailerons + differential stabilators, with lateral authority scheduled on the pitch boost-servo output; rudders from pedals plus a mechanical ARI also scheduled on pitch.
- **CAS:** feedbacks p, q, r, n_z, n_y (notch-filtered).
  - **Pitch CAS:** a **modified C\*** command (blend of normal acceleration and pitch rate) from stick force; proportional-plus-integral; authority limited; summed into the stabilator series servos.
  - **Stall inhibitor:** nose-down stabilator when α and/or washed-out pitch rate exceed preset levels (disabled on the scaled model).
  - **Roll CAS:** roll-rate error to the differential stabilator, authority limited against α.
  - **Yaw CAS:** lateral acceleration and washed-out **stability-axis yaw rate r_s = r − αp** to the rudders.
  - **Downmode:** to mechanical (spin prevention) when |r| > 42 °/s.
- Servo limits/rates in its Table 3 are for the 3/8-scale model, not the full-scale aircraft.

### 2.2 Consistency with the repository's NASA 836 record

`Docs/Reference/F15_836_FCS_STRUCTURE_V1.0.md` (sources NASA/TM-2009-214651 and TM-2012-215978, read by the repository) shows the same **structure** for NASA F-15B 836: pitch CAS with a stall inhibitor fed by α and q, roll-rate feedback with an α/airspeed-scheduled limiter, ARI, and an α × p roll-yaw crossfeed; no numbers. Two independent NASA lineages (1975 RPV simulation, 2009/2012 836 reports) therefore agree on the CAS topology. **PV (TN D-7941) + REPO_DOC (836).**

### 2.3 Research testbeds — not production

- **NF-15B #837 IFCS** `[NASA-IFCS-BOSWORTH]` — PV: dynamic inversion with explicit model following and a PID wrapper; pitch reference model `q_ref/δ_lon = K ω_sp² (s + L_α)/(s² + 2ζ_sp ω_sp s + ω_sp²)`, roll reference `p_ref/δ_lat = K/(τ_r s + 1)`. Canard-equipped research aircraft; neural-network adaptation produced a roll PIO tendency in flight.
- HIDEC F-15A: digital computer with modified analog CAS — SV (snippet only).

---

## 3. Handling-quality criteria usable as *objectives*

`[MIL-F-8785C]` — PV (scan read); inactive for new design since 1996, superseded by MIL-STD-1797A (not inspected, UV).

| Item | Definition / requirement (Category A unless noted) |
|---|---|
| Control anticipation parameter | `CAP = ω_sp² / (n/α)`; Level 1 bounded roughly 0.28–3.6, Level 2 0.16–10 (Fig. 1, lines of constant CAP) |
| Short-period damping | Level 1: 0.35 ≤ ζ_sp ≤ 1.30 (Cat. A, C); Level 2: 0.25–2.00; Level 3 ≥ 0.15 |
| Dutch roll, Class IV (fighters), Level 1 | ζ_d ≥ 0.4, ω_nd ≥ 1.0 rad/s (with additional ζ_d ω_nd conditions) |
| Roll-mode time constant | τ_R ≤ 1.0 s (Level 1), ≤ 1.4 s (Level 2), Classes I and IV |

- **C\***: blend of normal acceleration and pitch rate; used by the F-15 pitch CAS (PV, TN D-7941) and listed by Enns et al. as a candidate controlled variable (PV, slides). The common form `C* = Δn_z + (V_co/g) q` with `V_co ≈ 400 ft/s` is **UV** (Tobie, Elliott & Malcom 1966 not located).
- **Cooper–Harper scale** `[COOPER-HARPER-1969]`: the standard pilot rating scale (record located on NTRS; SV).

**Use in Maverick (EI):** these are *design objectives for response shape*, not aircraft data. They can serve as validation targets for an instructor-plus-law response at a stated flight condition (e.g. "the closed-loop bank capture behaves like a first-order roll mode with τ ≈ …"), while every resulting gain stays labelled Maverick tuning. Comparing Maverick's F-15 V2 Dutch-roll damping (ζ ≈ 0.31 in the design harness, V2 doc §5.4) against the Class IV Level 1 ζ_d ≥ 0.4 indicates it would not meet that line — a fact about the tuned law at one research trim, not about the F-15.

---

## 4. Envelope protection and saturation

PV unless marked.

1. **Command limiting, not surface limiting**, is the primary protection: TP-1538 reduces the *commanded g* ceiling with α and anticipates with washed-out q; schedules the negative-g limit with q̄.
2. **Roll-rate limiting against α, q̄ and pitch-control usage** to prevent inertia-coupling departures (TP-1538 system B).
3. **Mode switching for departure prevention:** F-16 sim above α = 29°; F-15 CAS downmode at |r| > 42 °/s.
4. **Integrator limits** (TP-1538 pitch integrator path ±25°, figure reading).
5. **Design elements listed by Enns et al. 1994** `[ENNS-MCT-1994]`: actuator position/rate limits and integrator windup handling; command limits that keep a stable equilibrium possible; **pitch priority over roll**; roll-command limits to prevent departure; "daisy-chain" allocation; per-step rate-limit bounds `u ∈ [max(u_min, u_old − r_lim T_s), min(u_max, u_old + r_lim T_s)]`.
6. **Anti-windup** (EI, textbook): back-calculation `ẋ_I = K_i e + (u_sat − u_cmd)/T_t`; conditional integration (freeze while saturated in the same direction). Maverick's `StepIntegratorWithAntiWindup` implements conditional integration (REPO_VERIFIED).
7. **Rate limiting as a PIO trigger** `[KLYDE-MITCHELL-RL-PIO]`, `[NASA-CR-198055]`: heavy rate saturation adds large phase lag abruptly; rate limits can trigger PIO (YF-22 and JAS-39 cases are cited as rate-limit related); phase-compensated rate limiters (e.g. Saab's feedback-with-bypass, later used in the Gripen FCS per the paper) mitigate it.

**Implication for the instructor (EI):** guidance-side rate limits on *commands* (doc 03 §4.7) are a mild form of rate limiting. They should be set so they rarely saturate during normal aiming; if they saturate often, they become the PIO-like lag the literature warns about. Track the fraction of steps at a command rate limit (doc 06 metric M6/M9).

---

## 5. Nonlinear dynamic inversion and incremental NDI

- **NDI rotational law** `[SIEBERLING-2010]` Eqs. 20–23 (PV): `ω̇ = J⁻¹(M_a + M_c − ω × Jω)`; choose `δ = M_cδ⁻¹ (J ν + ω × Jω − M_a)` for a desired angular acceleration `ν`. Depends on the whole aero model, inertia and CG.
- **INDI** `[SIEBERLING-2010]` Eqs. 24–31 (PV): `Δδ = (J⁻¹ M_cδ)⁻¹ (ν − ω̇₀)`; `M_a` and `ω × Jω` drop out; needs measured/estimated angular acceleration and fast actuators; delay-sensitive (the paper adds a predictive filter); simulation-only validation in that paper.
- **Controlled-variable choice** `[ENNS-MCT-1994]` slide 6 (PV): at significant airspeed, **roll about the velocity vector**, a C\*-type pitch variable, and yaw blended with β; at negligible airspeed, body rates. Zeros of the controlled variable must be minimum-phase.
- **Time-scale separation** `[SNELL-1992]` (SV, abstract): fast inner loop on rates, slow outer loop on attitude/flight-path angles.
- **F-35** `[F35-NIXON-2004]` (PV, Lockheed Martin slides): dynamic-inversion structure `u = (CB)⁻¹(ċv_des − CAx)` with a flying-qualities "regulator" (e.g. roll `ṗ_des = (Cmd − p)/τ_roll`), onboard model, effector blending and limiting. "First production aircraft with NDI" is SV only (secondary report). Harris 2018 and Walker & Allen 2002 not inspected (UV).

**Relevance (EI):** Enns's choice of controlled variables — velocity-vector roll and a C\*/load-factor pitch variable — is the same set that doc 03 recommends as the guidance-to-law interface. That is the main transferable lesson: **the instructor should command physical, flying-qualities-oriented variables (stability-axis roll rate, load factor), and the law should own their realization.** NDI itself remains P3 research for Maverick (doc 03 §3.7).

---

## 6. Control allocation

- Problem `(CB) u = a_d`, `u_min ≤ u ≤ u_max`; pseudo-inverse, redistributed pseudo-inverse (can fail to reach attainable moments — counter-example in the paper), linear-programming/direct allocation; LP-based methods cost within about 10× of simple methods, well under 1 ms, and avoid occasional large errors `[BODSON-2002]` (PV). Durham's attainable moment subset `[DURHAM-1993]` (SV); active-set weighted least squares `[HARKEGARD-2002]` (SV).
- **When it matters (EI):** a model where pitch and roll share the stabilators. TP-1538 notes stabilators near saturation during roll reversals; once collective stabilator is near its stop, differential-tail roll authority disappears — hence *pitch priority* (Enns). In Maverick today, the F-15 rig's differential tail is a fixed 0.3 × aileron (sourced research relation) and the F-16 Morelli model has three independent channels, so no allocation is needed; pitch-priority becomes relevant if a future model shares surfaces with independent authority.

---

## 7. Aircraft-dynamics relations the instructor must respect

| Relation | Form | Class |
|---|---|---|
| Stability-axis roll | `r = p tan α`; `p = p_s cos α`, `r = p_s sin α` | PV (TP-1538 p. 14) |
| Kinematic coupling | `β̇ ≈ p sin α − r cos α (+ side force, gravity)`, `α̇ ≈ q − p cos α tan β` | PV (TP-1538 p. 14); full form EI |
| Inertia coupling | `M_ic = (I_z − I_x) p r = ½ (I_z − I_x) p_s² sin 2α` (nose-up for α > 0); `N_ic = (I_x − I_y) p q` | PV (TP-1538 p. 15) |
| Roll-coupling instability | steady roll destabilizes when roll rate exceeds the lower of the non-rolling pitch/yaw natural frequencies, if they differ | PV (`[NACA-TN-1627]` summary) |
| Euler equations (x–z symmetric) | `I_x ṗ − I_xz ṙ = L + (I_y − I_z) q r + I_xz p q`; `I_y q̇ = M + (I_z − I_x) p r + I_xz (r² − p²)`; `I_z ṙ − I_xz ṗ = N + (I_x − I_y) p q − I_xz q r` | EI (standard) |
| Level coordinated turn | `n = 1/cos φ`, `ω = g √(n² − 1)/V`, `R = V²/(g √(n² − 1))` | EI (standard) |
| Sustained vs instantaneous turn | sustained: `T = D` at `n`; instantaneous: bounded by `C_Lmax` (α) or g limit | EI |
| Specific excess power | `P_s = V (T − D)/W` | EI (equation standard; Rutowski / Boyd–Christie primaries not inspected) |
| Authority vs q̄ | control moment `= q̄ S (b or c̄) C_δ δ`, so angular acceleration per degree ∝ q̄/I; rate loops need gains ∝ 1/q̄ | PV (Sieberling Eq. 21) + EI |

**Sign note.** The kinematic relation above (TP-1538) is the one `03_GUIDANCE_ALGORITHMS.md` §2 uses. Maverick's aero-body convention (+X fwd, +Y right, +Z down) is the conventional one, so the signs carry over; re-derive only when working in Unity local axes (the README's axial-vector rule applies).

**What this means for the instructor (EI):**
- At the F-15 rig's 17.5° trim α, a *body-axis* roll trades α into β at sin 17.5° ≈ 0.30 per unit roll rate — the measured V2 coefficient (V2 doc §5.1). Bank-to-turn guidance must command stability-axis roll; F-15 V2 already provides it.
- Rapid, large rolls at low speed and high α are exactly the inertia-coupling regime TP-1538 flagged. The instructor must not demand full roll rate there as a matter of habit; the roll-rate cap belongs to the law/descriptor (`Capabilities`), not to a guidance constant.
- Energy loss in a turn is physics: the instructor must show it (P_s), never compensate it.

---

## 8. F-22: almost nothing verified

- `[F22-SETP-1999]` (Rainey & Kohn, SETP flight-test update; PV via a fetched copy): pilot-selectable test control-law options; ARI reductions of 20 % and 40 %; fully-checked rolls produced 4–6° nose kicks attributed to higher-than-predicted directional stability and rudder power, improved by the ARI cut; single-engine control-law changes. **No** pitch law, limiter or thrust-vector integration detail.
- `[ENNS-MCT-1994]` lists the YF-22 among applications of the USAF multivariable-control program — it does **not** establish that the production F-22 uses dynamic inversion.
- Pitch-only vectoring nozzle and YF-22 high-α demonstrations: UV in this research (snippets only). The repository's `Docs/Research/Aerospace/Aircraft/F22/PUBLIC_SOURCE_FEASIBILITY.md` already concludes that no defensible F-22 model or FCS can be built from public data.

**Conclusion:** no F-22 control-law claim should appear in Maverick documentation or code except as explicitly unverified context.

---

## 9. Reusable principles for Instructor v2 (summary)

| # | Principle | Evidence | Where it lands in Maverick |
|---|---|---|---|
| R1 | Pilot commands a **load-factor-like** pitch variable; the FCS limits it with α and q̄ | PV TP-1538; PV TN D-7941 (C\*); PV Enns | guidance emits `n_cmd` (doc 06 [C]) |
| R2 | Roll is commanded as a **rate about the velocity vector** at significant airspeed | PV Enns; PV TP-1538/TN D-7941 (r − αp damping) | guidance emits `p_s,cmd`; F-15 V2 native |
| R3 | **Coordination belongs to the FCS** (ARI, β/n_y feedback), not to the pilot | PV TP-1538, TN D-7941, 836 structure | guidance `β_cmd = 0` by default |
| R4 | Protections act on **commands** (g ceiling reduced with α; roll-rate limits vs α, q̄) | PV TP-1538 | law limiters (F-16); rig domain caps (F-15) |
| R5 | Authority and response scale with **q̄**; low q̄ = low achievable rate | PV/EI | no guidance "authority boost"; accept shortfall |
| R6 | Rate saturation adds lag and can trigger PIO | PV Klyde & Mitchell | keep guidance rate limits rarely binding; monitor |
| R7 | Flying-qualities criteria (CAP, ζ_sp, τ_R, Dutch roll) are **objectives**, not data | PV MIL-F-8785C | validation targets with labelled tuning |
| R8 | Production gains are **not** public for F-15/F-16/F-22 | repository library + this research | never claim FCS fidelity |
