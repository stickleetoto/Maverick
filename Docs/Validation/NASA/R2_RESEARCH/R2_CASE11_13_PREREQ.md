# R2_CASE11_13_PREREQ — What it takes to run NESC atmospheric cases 11 and 13.1–13.4 against Maverick

Research only; no production code was changed. Every claim is tagged **VERIFIED** (source + location) or **INFERENCE**.

Abbreviations: "Vol II" = NASA/TM-2015-218675 Vol II (NESC-RP-12-00770 Vol II, appendices); page numbers are the document's own "N of 609" numbers. "DML dir" = `E:\unity project\_nesc_source\extracted\f16\F16_package\F16_S119_source\`. "Case dir" = `E:\unity project\_nesc_source\extracted\cases\Atmospheric_checkcases\`. "Maverick" = worktree `E:\unity project\Maverick-nasa-6dof` (branch `claude/nasa-6dof-validation-r1`).

Scripts (stdlib only, run with `python -I`), in `Tools/nasa_6dof/research/scripts/`:
`case11_13_spread.py` (inter-tool spread, α/β/track/cross-track derived from logged data), `env_estimates.py` (atmosphere and rotating-Earth magnitudes), `pgrep.py` (page search in the pdftotext extract `research\appendices.txt`).

---

## 0. Configuration identity — keep NESC and Maverick apart

| Item | NESC check-case F-16 (`F16-CFG-NESC-CHECKCASE`) | Maverick frozen F-16 | Tag |
|---|---|---|---|
| Mass | **637.1595 slug** (`F16_inertia.dml`, `totalMass` initialValue; README: "20,500 lbm" = 20 500/32.174049) | **637.16 slug** (`MavF16MassReference.MassSlug = 637.16f`, line 47) | VERIFIED |
| Mass in Vol II Table 7 | **637.26 slug** (p. 15, Table 7) — differs from the normative DML by 1.6 × 10⁻⁴ | — | VERIFIED (column alignment of the pdftotext extract is shifted by one row; the "slug" unit pins 637.26 to m) |
| Which mass the NASA tools flew | SIM 05 t = 0 trim balance −F_Z/(m cos θ) = 32.05313 ft/s² with 637.1595 vs 32.04808 with 637.26; the independently computed rotating-Earth relief (§2) predicts 32.05317 → the tools used ≈ 637.16 | — | INFERENCE (`env_estimates.py`) |
| **Note for the caller** | The brief says "Maverick 637.26 slug"; in this worktree Maverick is 637.16 slug and 637.26 is the Vol II Table 7 figure. | | VERIFIED |
| Inertia | Ixx 9 496, Iyy 55 814, Izz 63 100, Izx 982 slug·ft², Ixy = Iyz = 0 (`F16_inertia.dml`; "no sign reversal") | same numbers (`MavF16MassReference` lines 48–51) | VERIFIED |
| CG / MRC | DML default CG 35 % MAC; the cases use **25 %** (README Table 5; Vol II p. 604 "consensus was reached on fixing the CM at 25% MAC"); aero moments are about the 35 % MRC and must be transferred (DXCG = 0.01·11.32·(35 − 25) = +1.132 ft, CM forward) | Maverick reference 0.25 c̄ / 0.35 c̄ (MassReference doc comment) | VERIFIED |
| Aero | NESC gridded tables (`F16_aero.dml`, Garza & Morelli ← Stevens & Lewis + TP-1538) | Morelli 1998 polynomial (`MavF16MorelliPolynomial`) — differs in trim (R1 D-F1…D-F4) | VERIFIED (R1 `F16_STATIC_AERO_REPORT.md` §D-F4) |
| Thrust | Static Stevens & Lewis idle/MIL/MAX tables vs Mach, altitude, PWR (`F16_prop.dml`), **no power lag** | TP-1538 deck + Garza–Morelli engine dynamics (`MavF16Tp1538ThrustDeck`, `MavF16GarzaMorelliEngineDynamics`) | VERIFIED (files) |
| Actuators | **none** ("No actuator dynamics were modeled", Vol II p. 16) | `MavF16ControlActuator` (optional rate limits) | VERIFIED |
| Control law | LQR SAS + autopilot (`F16_control.dml`) | `MavF16ControlLawV01` (Maverick law) | VERIFIED |
| Atmosphere | US 1976 (tabular in NASA's own Simulink run, README "Results") | `MavAtmosphereModel`: ISA troposphere with **geometric altitude used as geopotential**, g0 9.80665 | VERIFIED |
| Gravity | J2 gravitation, WGS-84 rotating Earth (Vol II Tables 35–40); logged `localGravity` 32.18858 ft/s² at t = 0 | Unity project gravity, flat non-rotating | VERIFIED |

Nothing in the NESC column may be written into Maverick's frozen F-16 files; everything below is a separate validation-only stack.

---

## 1. Case definitions (Q1)

### 1.1 Common initial condition — VERIFIED (Vol II pp. 62–65, Tables 35, 37–40)
- Position: geodetic lat 36.01916667°, lon −75.67444444°, 10 013 ft MSL ("10,000 ft above KFFA", Kitty Hawk).
- Local-relative velocity [400, 400, 0] ft/s NED (565.685 ft/s, the table note says 335.15 KTAS; the narrative's "400 KTAS" on p. 62 is inconsistent with the table — the table/CSV values govern). Attitude [φ, θ, ψ] = [0, trim, 45°]; rates [0, 0, 0] (each tool trims its own rates).
- WGS-84 rotating Earth, J2 gravitation, US 1976 atmosphere, no wind.
- Trim: zero linear/angular acceleration at 400 ft/s components, 10 013 ft, by varying pitch attitude, elevator and throttle (p. 62). Trim with `stabilityAugmentationOn_disc` and `autopilotOn_disc` both false and solve `trimmedPilotControl_throttle` / `trimmedPilotControl_long` (p. 16; README).

### 1.2 Per-case — VERIFIED (Vol II pp. 62–65 and D-section text pp. 228–296)

| Case | Control | Command | Duration (spec) | Logged by tools |
|---|---|---|---|---|
| 11 | **Unaugmented** (SAS off, no autopilot) | none; trim-hold test | 180 s | SIM 02 (200 s), SIM 04 (180 s), SIM 05 (180 s) |
| 13.1 | AP on + SAS on (both > 0.5) | t = 5.0 s: `altitudeMslCommand` +100 ft (10 013 → 10 113) | 20 s | SIM 02/04: 20 s; SIM 05: 60 s |
| 13.2 | AP + SAS | t = 5.0 s: `equivalentAirspeedCommand` −5 kt (287.98 → 282.98 KEAS, p. 269). **SIM 02 flew −10 kt and was not updated** (p. 269) | 20 s | SIM 02/04: 20 s; SIM 05: 60 s |
| 13.3 | AP + SAS | t = 15.0 s: `trueBaseCourseCommand` +15° right (45° → 60°), `lateralDeviationError` held 0 | 30 s | SIM 02/04: 30 s; SIM 05: 239.9 s |
| 13.4 | AP + SAS | t = 20.0 s: 2 000-ft lateral offset **to the right**, via `lateralDeviationError`, base course stays 45°. "updates for the input feedback signal lateralDeviationError had to be calculated for each time step by additional user-supplied logic" (p. 65) | 60 s | SIM 02/04: 60 s; SIM 05: 239.9 s |

Notes:
- Before the step, commands equal the trimmed values (altCmd = 10 013 ft, keasCmd = trim KEAS 287.98 kt, baseChi = 45°) — VERIFIED for KEAS (p. 269 "to 282.98 KEAS"); INFERENCE for altCmd and course (implied by "increase"/"change" wording).
- Sign of the 13.4 input: `lateralDeviationError` is "+right of course" (`F16_control.dml` line 152); a new course 2 000 ft right means the aircraft is initially **left** of it → input −2 000 ft → Δχcmd = −0.01·(−2 000) = +20° → right bank. VERIFIED (DML) / INFERENCE (arithmetic). Logged data confirm a right bank to +30° at t ≈ 25 s (SIM 04/05, `case11_13_spread.py`).
- Before t = 20 s in 13.4 the cross-track grows to 3.31 ft in SIM 04 and 05 identically, exactly as in heading-hold case 13.1 → the input was 0 (or equivalent) before the step. INFERENCE (logged data).
- README's own examples differ from the TM (13.2 "10 knots"; 13.4 "2,000 ft to the left … trueBaseCourseCommand … 90 deg"). The TM definitions govern; SIM 02's 13.2 followed the older −10 kt plan (p. 269). VERIFIED (README.html; Vol II p. 269).
- Vol II p. 282 (13.3) contains two contradictory end-of-run sentences (5.4/14.3/19.1 ft, then a pasted 135/137 ft from 13.2). Recomputed SIM 04–05 separation 5.37 ft supports the first. VERIFIED (text) / INFERENCE (which is right).

### 1.3 What the CSVs contain — VERIFIED (case dir headers)
- All files sample at 0.1 s. Columns: `time`, `feVelocity_ft_s_X/Y/Z` (Earth-relative NED), `altitudeMsl_ft`, `longitude_deg`, `latitude_deg`, `localGravity_ft_s2`, `eulerAngle_deg_Yaw/Pitch/Roll`, `bodyAngularRateWrtEi_deg_s_Roll/Pitch/Yaw` (**wrt inertial**), atmosphere (`speedOfSound`, `airDensity`, `ambientPressure`, `ambientTemperature`), `aero_bodyForce_lbf_X/Y/Z`, `aero_bodyMoment_ftlbf_L/M/N`, `mach`; plus `eiPosition`/`eiVelocity` (SIM 04, 05), `gePosition` (SIM 05), `dynamicPressure` (04, 05), `altitudeRateWrtMsl_ft_min` (02, 05), `trueAirspeed_nmi_h` (02, 05). SIM 05 repeats `feVelocity_ft_s_Z` at the end.
- **Not logged:** α, β, control-surface positions, throttle/PWR, thrust, KEAS, autopilot internals. α/β must be derived (feVelocity + Euler, still air); surfaces/throttle can only be checked indirectly through aero forces/moments.
- Aero moments: SIM 02 logs them about the MRC, SIM 04/05 about the CM (p. 229; Table 78 fn d). VERIFIED.
- **SIM 02 is excluded by NASA from all F-16 comparisons** "due to large trim differences" (Table 79/80 fn b, pp. 593–594); the first 8 s of every F-16 case are excluded from NASA's percentage tables (fn a). VERIFIED.

---

## 2. Rotating-Earth effects at ~565.7 ft/s, 10 013 ft, lat 36°, heading 45° (Q2)

Numbers from `env_estimates.py` (Ω = 7.2921159 × 10⁻⁵ rad/s, WGS-84 radii R_N 20 949 910 ft, R_M 20 857 949 ft). All INFERENCE unless marked; the checks against logged data are what makes them credible.

| Effect | Magnitude | Over 20 / 30 / 60 / 180 s if uncompensated | Check vs NASA data |
|---|---|---|---|
| Horizontal Coriolis 2Ω sin φ V (to the right) | 0.0485 ft/s² | 9.7 / 21.8 / 87 / 786 ft lateral; track +0.10 / +0.15 / +0.30 / +0.88° | Case 11 SIM 04/05 end 633–636 ft right of the initial 45° line (tangent-plane), χ 45.53°, ψ 45.53° (logged); same order, the remainder is curvature/transport/weathercock coupling |
| Vertical Eötvös 2Ω cos φ V_E (up) | 0.0472 ft/s² | — | — |
| Path curvature V²/R | 0.0153 ft/s² | — | — |
| Centrifugal, vertical part | 0.0729 ft/s² | — | — |
| **Total vertical relief vs J2 gravitation** | **0.1354 ft/s² = 0.421 % g** | changes trim α by ≈ 0.017° (using dα/d ln q̄ ≈ 4.1° from 13.2 SIM 04/05) | SIM 05 t = 0: −F_Z/(m cos θ) = 32.0531 ft/s² vs 32.18858 − 0.1354 = 32.0532 → agreement 4 × 10⁻⁵ ft/s² |
| Bank needed for heading-hold (Coriolis + rhumb-line curvature V_E tan φ/R ≈ 0.0079 ft/s²) | 0.0564 ft/s² → **φ ≈ −0.101°** | — | SIM 04/05 13.1/13.4 steady φ = −0.101° (logged) — exact match |
| Heading of an inertially-held body vs local north | −(Ω sin φ + V_E tan φ/R) = −0.00325 °/s | −0.585° in 180 s | (case 11 is not inertially held in SIM 04/05) |
| Pitch rate to stay level | −0.00394 °/s | 0.71° in 180 s | SIM 04 trim logs q = −0.0039 °/s |
| Earth rate in logged body rates | ≤ 0.0042 °/s | — | logged rates are `WrtEi` |
| Ground distance / tangent-plane drop | 101 823 ft / 247 ft | — | flat y must be compared with MSL altitude, not with a tangent plane |

Robustness for a flat, non-rotating Maverick run:
- **Robust (compare directly):** TAS/KEAS/Mach, q̄, α, β (closed loop), θ, altitude and Δh(t), q, aero body forces (and CM moments vs SIM 04/05), p/q/r transient shapes and timing, closed-loop Δχ relative to the command, 13.4 cross-track relative to the commanded line. INFERENCE (justified by the table above).
- **Biased, correct with a stated offset:** φ (NASA heading-hold carries −0.10°; flat → ≈ 0°), body rates (≤ 0.004 °/s Earth-rate offset), trim α/θ/throttle if the 0.42 % g relief is not reproduced. INFERENCE.
- **Not robust (exclude or model):** case 11 ψ, χ, φ drift, lateral position, lat/lon, long-duration position. Case 11's open-loop lateral/heading signals are dominated by Earth rotation (SIM 04 vs 05 differ by only 0.003° in ψ while both drift +0.53°). VERIFIED (logged) / INFERENCE (attribution).
- Recommended equivalence (validation-only environment, no core change): gravity through the load set at **g_eff = 32.0531 ft/s² (9.76979 m/s²)** reproduces NASA's trim balance; Eötvös varies with heading (13.3 at 60°: +0.0106 ft/s², 0.03 % g) — document, do not chase. Do **not** inject Coriolis as a fake force: MavSixDoFBody has no channel for it and smuggling it through the propulsion slot would mislabel physics. Closed-loop cases absorb it (−0.10° bank). INFERENCE.

---

## 3. Components required (Q3)

### 3.1 The DAVE-ML package has no dynamic states
- None of the five files uses `isState`/`isStateDeriv` (grep count 0). VERIFIED.
- README: "perfect state feedback is assumed … and the control system has no integrated states" → Type-0 response with small steady offsets. VERIFIED (README "A note on initial closed-loop responses").
- Vol II p. 16: "No actuator dynamics were modeled". VERIFIED. `F16_prop.dml` is steady-state thrust only (outputs named "Net steady-state thrust"), no power lag. VERIFIED. The Simulink-equivalent `F16_*_create.m` files contain only Product/Sum/Saturate/Switch/Lookup blocks — no Integrator, TransferFcn, RateLimiter or UnitDelay. VERIFIED (grep).
- **The host integrates only the rigid body** (position, velocity, attitude, body rates). It must *derive* each step: α, β (deg), p, q, r (rad/s), φ, θ, ψ (deg, ψ clockwise from north), altitude (ft), Vequiv (kt), Mach, and for 13.4 the geometric `lateralDeviationError`. VERIFIED (DML inputs) / INFERENCE (list).

### 3.2 F16_control.dml structure — VERIFIED (DML dir `F16_control.dml`, Rev F 2013-10-22)
- Inputs: pilot throttle/long/lat/yaw (limited 0..1 / ±1), `sasOn`, `apOn`, `keasCmd` (kt), `altCmd` (ft), `latOffset` (ft, +right), `baseChiCmd` (deg), feedback `altMsl` (ft), `Vequiv` (kt), `alpha`, `beta` (deg, +wind in right ear), `phi`, `theta`, `psi` (deg), `pb`, `qb`, `rb` (**rad/s**), `throttleTrim` (default 0.1390191130965607), `longStkTrim` (default 0.1296382327486013).
- Outer loops: altitude Δθcmd = clamp(−0.05 °/ft · (alt − altCmd), ±5°), θcmd = 2.653813535191715° + Δθcmd; lateral Δχcmd = clamp(−0.01 °/ft · latOffset, ±30°), χcmd = baseChi + Δχcmd, χest = **ψ + β**, χerr wrapped to ±180°, φcmd = clamp(−10 · χerr, ±30°). Switches select commanded (AP on) or design references (AP off: θ = 2.6538°, KEAS = 287.8088596 kt, φ = 0).
- LQR: two **2 × 4** matrices, outputs negated. Longitudinal states [ΔKEAS (kt), Δα (deg), q (rad/s), Δθ (deg)] → [stick, throttle]: K_long = [[−0.06301, 0.11323, 10.11343, 3.15498], [0.99726, −0.02547, 1.21331, 0.20874]]. Lateral states [Δφ (deg), β (deg), p (rad/s), r (rad/s)] → [lat stick, pedal]: K_lat = [[3.07804, 0.03237, 4.55786, 0.58944], [−0.70582, −0.25636, −1.07367, 0.82211]]. Mixed deg / rad·s⁻¹ units are intentional.
- Engage: `fsasOn = sasOn + apOn` (> 0.5); AP on zeroes pilot inputs.
- Mixer/limits: totLong = clamp(longStkTrim + longStk + longLQR, ±1), el = −25·totLong (deg, +TED); ail = −21.5·clamp(lat, ±1) (deg, +LWD); rdr = −30·clamp(pedal, ±1) + 0.008·ail (ARI; max 30.17°); PWR = 100·clamp(throttleTrim + throttle + throttleLQR, 0..1) (%; 50 = MIL, 100 = MAX).
- Implications (INFERENCE): the throttle gain is ≈ 1 throttle-fraction per knot, so a KEAS offset of 0.02 kt already moves throttle 2 %; the altitude step saturates Δθcmd (+5°) and the stick (el = −25°, while the aero elevator table holds at −24°); the heading step saturates φcmd at 30°. NASA logs show q peaks ≈ 30 °/s (13.1) and p peaks ≈ 150 °/s, φ peaks 35–40° (13.3/13.4). Saturation/hold semantics therefore dominate the transients.
- `F16_gnc.dml` is only for cases 15/16 (circumnavigation) and is not needed. VERIFIED (README; Vol II p. 66).

### 3.3 Validation-only parts to build (all outside production physics)

| # | Component | Plugs into | Content | Tag |
|---|---|---|---|---|
| 1 | NESC aero model | `MavAerodynamicModelBase.Evaluate(state, input, atmosphere) → MavAeroCoefficients` | `MavDaveMlEvaluator` on `F16_aero.dml`; inputs vt ft/s, α/β deg, p/q/r rad/s, el/ail/rdr deg (NESC signs end-to-end); **transfer MRC → CM inside the coefficients**: Cm_cm = Cm + 0.10·CZ, Cn_cm = Cn − (1.132/30)·CY, Cl unchanged (DZCG = DYCG = 0); reference geometry S 300 ft², b 30 ft, c̄ 11.32 ft converted to metres | VERIFIED (interface: `Core/MavAerodynamicModelBase.cs`; R1 transfer check reproduces SIM 02's 23 119.7 ft·lbf) / INFERENCE (formula signs follow ΔM = r × F with r_MRC = (−1.132, 0, 0) ft) |
| 2 | NESC static thrust | `MavPropulsionModelBase.Evaluate(state, atm, throttle01, dt) → MavPropulsiveLoads` | `F16_prop.dml`, PWR = 100·throttle01, altitude ft, Mach; force on body +X only, zero moment; `ResetEngineState` no-op; no lag; report `HasAuthoritativeData`/live-flight false (validation only) | VERIFIED (interface `Core/MavPropulsionModelBase.cs`) |
| 3 | NESC LQR law | `MavFlightControlLawBase.Evaluate(state, atm, command, profile, dt) → MavControlInput` | `MavDaveMlEvaluator` on `F16_control.dml`; computes KEAS = TAS·√(ρ/ρ0)/1.68781 (ρ0 = 0.0023768924 slug/ft³), supplies all inputs (missing pilot inputs throw), scenario commands and trim references; outputs el/ail/rdr deg and throttle01 = PWR/100. Name it a check-case law, not an F-16 FLCS | VERIFIED (interface `Core/MavFlightControlLawBase.cs`; evaluator throws for an input with no value, lines 266–270) |
| 4 | Pass-through actuator | `MavControlSurfaceActuatorBase` | `MavF16ControlActuator` with all rate limits 0 (= unlimited) is usable, or a validation one; clamp limits must not be tighter than el ±25, ail ±21.5, rdr ±30.17 | VERIFIED (`F16/MavF16ControlActuator.cs` lines 24–27, 148–150) |
| 5 | NESC profile provider | `MavFlightDynamicsProfileProvider.BuildProfile()` + `ResolveEnvironment()` | mass 637.1595 slug, inertia incl. Izx 982 (verify Unity-axis sign mapping with a standalone test), CoM at 25 % MAC, reference geometry, surface limits; environment: US 1976 **geopotential** atmosphere sample at the body altitude, gravity through the load set at g_eff (32.0531 ft/s²) with `Rigidbody.useGravity` off | VERIFIED (virtual `ResolveEnvironment`, `Core/MavFlightDynamicsProfile.cs` lines 173–190; gravity load-set path `MavSixDoFBody` lines 624–754) / INFERENCE (values) |
| 6 | Scenario driver | harness | trim (SAS/AP off), then AP+SAS on from t = 0, step commands at 5/5/15/20 s, 13.4 cross-track logic, 0.1-s sampling, start/stop at spec durations | INFERENCE |
| 7 | Trim solver | harness (Core has `MavTrimSolver`, applicability not checked) | solve θ (= α), `longStkTrim`, `throttleTrim` at V = 565.685 ft/s, 10 013 ft, wings level | INFERENCE |
| 8 | Flat-Earth double-precision reference | `MavValidationRk4Integrator` / `MavNasaRigidBodyReference` (exist from R1) | same NESC models in double RK4, flat Earth + g_eff; separates (a) flat-vs-rotating modelling error from (b) Maverick/PhysX numerics | INFERENCE (files exist: Validation folder listing) |

Atmosphere numbers (`env_estimates.py`): Maverick's model at 10 013 ft gives ρ −1.54 × 10⁻⁴ and a −1.7 × 10⁻⁵ relative to SIM 05; a US 1976 geopotential evaluation gives −2.6 × 10⁻⁶ and +8 × 10⁻⁷. ρ changes −3.1 × 10⁻³ over the +100-ft step, so a fixed-density override (`ResearchSourceFixedDensity`) is not acceptable for 13.1. Trim KEAS = 287.982 kt (matches Vol II's 287.98). INFERENCE (computed) / VERIFIED (287.98 on p. 256).

Labelling gap: `MavDensitySource` has only `StandardAtmosphere` and `ResearchSourceFixedDensity`; an altitude-varying US 1976 sample returned by a validation provider has no honest label. Capability exists (the provider returns any `MavAtmosphereSample`), naming does not. VERIFIED (`Core/MavFlightEnvironment.cs`) / INFERENCE (consequence).

### 3.4 `MavDaveMlEvaluator` feature gaps — VERIFIED (`Validation/MavDaveMlEvaluator.cs` lines 13–21, 292–312; op census by grep)

| File | MathML ops used | Missing in evaluator |
|---|---|---|
| `F16_aero.dml` | abs, divide, lt, minus, piecewise, plus, power, times; 18 gridded tables | none (R1 check-shots pass) |
| `F16_prop.dml` | divide, lt, minus, piecewise, plus, times; 3 × 2-D tables, extrapolate="neither" | none (R1 used it for the trim thrust) |
| `F16_inertia.dml` | minus, times | none |
| **`F16_control.dml`** | abs, **gt (9×)**, lt, minus, piecewise (13, incl. nested in `chiErr`), plus, times | **`gt`** |
| `F16_gnc.dml` (not needed) | adds cos, csymbol `atan2` | gt, cos, atan2 |

Other evaluator facts relevant to the control file: min/max limits are applied to **every** variable including computed ones (lines 274–275), which is exactly what the DML's limited intermediates need; `piecewise` nested inside `piece` evaluates through `EvalMath` (case "piecewise"); `SetInputs` clears the cache; `F16_control.dml` has **no `checkData`**, so no NASA self-test exists for the law — VERIFIED. An independent check is needed, e.g. at the published trim with SAS+AP on and zero errors the law must return el = −25 × 0.1296382 = **−3.2410°** (README Table 11 "Horizontal tail position −3.2410 deg") and PWR = 13.9019 (README) — VERIFIED (README values) / INFERENCE (as a test).

---

## 4. Risks (Q4)

1. **Algebraic loops — none.** The aero inputs list has no α̇ (README Table 1; `F16_aero.dml` inputs lines 292–345) and the law is a static function of state; nothing depends on a state derivative. VERIFIED (inputs) / INFERENCE (conclusion).
2. **One-step control latency.** `MavFlightControlLawBase` runs at −300, actuator −200, body −100; "The state the law reads is the state sampled by the body during the previous step, i.e. one fixed step of sensor latency" (`Core/MavFlightControlLawBase.cs` lines 21–26). VERIFIED. NASA tools evaluate the law with current state (continuous Simulink-style or per frame). With q-gain 10.1 stick per rad/s (≈ 253° elevator per rad/s) and p-gain 4.56 (≈ 98° aileron per rad/s), a 20-ms delay adds phase lag in loops whose transients reach 30–150 °/s. INFERENCE. Vol II already names "execution rate of the autopilot" as a 13.3/13.4 difference source (pp. 282, 296). VERIFIED. Validation-only remedy without core change: run with and without latency — e.g. the NESC law rebuilds its state from the bound Rigidbody via the public static `MavSixDoFBody.BuildFlightState` (line 856) and `ResolveEnvironment` (line 685), or the harness drives `StepControlLaw` manually (`driveActuatorInFixedUpdate = false`) after a fresh state sample. INFERENCE.
3. **Step size and order.** Maverick: PhysX semi-implicit Euler with explicit torques, observed order 1.00–1.03 from dt 0.02 down; rotational energy grows ≈ linearly in dt (`NUMERICAL_CONVERGENCE.md` §§2–3). VERIFIED. NASA tools: POST II RK4 100 Hz; VMSRTE AB2 0.01 s; Core RK4 0.01 s; JSBSim higher-order AB 120 Hz; MAVERIC RK4 0.1 s (pp. 50–53); LaSRS++ AB2/Taylor, frame rate for cases 11/13 most likely 100 Hz (Table 21 layout is ambiguous in extraction). VERIFIED / INFERENCE (LaSRS++). SIM numbers are anonymised; tool ↔ SIM mapping is not published. VERIFIED (by absence). Expect dt 0.02 to be insufficient for 13.3/13.4 roll transients; plan dt 0.02/0.01/0.005/0.0025 with observed-order reporting. INFERENCE.
4. **Event timing.** Steps at 5.0/15.0/20.0 s are integer multiples of 0.02 and 0.01; define "command active from the first step whose start time ≥ t_step" and log state after integration at multiples of 0.1 s. Vol II lists "slight differences in timing of discrete events in fixed-step tools in cases 13–17" as a difference source (p. 589 summary list). VERIFIED (text) / INFERENCE (rule).
5. **float32.** Positions reach ≈ 31 km in case 11 (2 mm resolution), altitude 3 052 m (0.24 mm), TAS 172 m/s (1.5 × 10⁻⁵ m/s) — negligible. Coefficients are returned as float (≈ 10⁻⁷ relative). The real sensitivity is KEAS → throttle (≈ 1 per kt): float noise is ~3 × 10⁻⁵ kt, fine; an atmosphere-model offset is not (Maverick's model shifts KEAS by ≈ 0.02 kt). INFERENCE.
6. **Trim-reference ambiguity (largest documented SIM 04/05 difference).** SIM 04 kept the DML design references (α = θ = 2.6538°, 287.81 kt) and lost 0.11 ft/s on engagement; SIM 05 replaced them with its trim (pp. 256, 269, 295). VERIFIED. Run both variants and compare each to its counterpart. INFERENCE.
7. **13.4 lateral-offset logic is user-supplied and unspecified**; NASA lists differing estimation methods as a transient-difference source (p. 296). VERIFIED. Use signed perpendicular distance from the offset line (45° course, 2 000 ft right of the initial track through the t = 0 position), 0 before t = 20 s. INFERENCE.
8. **Open-loop case 11 is a trim-hold test**: SIM 04/05 keep V within 0.017 ft/s and h within 0.15 ft for 180 s. Any trim residual or integrator energy drift shows up as phugoid; NESC 25 %-CG lateral modes may also drift without SAS. INFERENCE (data from `case11_13_spread.py`).
9. **Body clamp and readiness.** `MavSixDoFBody` clamps controls with the active profile's limits before aero (lines 304–306) — the NESC profile must carry NESC limits. Validation bodies need `requireOperationalReadinessForLoadApplication = false`, `sleepThreshold = 0`, explicit CoM/inertia as in R1 (`VALIDATION_LIMITATIONS.md` §5). Peak p 2.7 rad/s is below PhysX's default `maxAngularVelocity` 7 rad/s. VERIFIED (code/docs) / INFERENCE (PhysX default).
10. **Elevator range.** Mixer reaches ±25° but the aero `el` breakpoints hold at ±24° (`F16_aero.dml` line 987, extrapolate="neither"); the evaluator holds at the edge, matching DML semantics. VERIFIED.

---

## 5. Observed NASA inter-tool spread and staged plan (Q5)

### 5.1 Observed spread (NOT acceptance thresholds)

From Vol II text (VERIFIED, pages given) and recomputed with `case11_13_spread.py` from the CSVs (INFERENCE for the recomputed numbers; SIM 04–05 recomputations reproduce NASA's quoted separations within 0.25 ft):

| Case | NASA text: end-of-run separation | Recomputed max over spec duration, SIM 04 vs SIM 05 |
|---|---|---|
| 11 | SIM 04–05 ≈ 4 ft; SIM 02 vs 04/05 ≈ 666 ft (p. 229) | V 0.017 ft/s, α 0.00024°, θ 0.00034°, h 0.15 ft, ψ 0.003°, φ 0.0009°; end 4.00 ft |
| 13.1 | SIM 04–05 2.5 ft; SIM 02–05 19 ft (p. 256) | V 0.22 ft/s, h 0.25 ft, α 0.12° and θ 0.14° (transient timing), q 1.7 °/s (transient); end 2.26 ft |
| 13.2 | SIM 04–05 2.2 ft; SIM 02 135/137 ft (−10 kt error) (p. 269) | V 0.11 ft/s, α 0.0045°, θ 0.006°, h 0.17 ft; end 2.19 ft |
| 13.3 | SIM 04–05 5.4 ft; SIM 05–02 14.3, 02–04 19.1 ft (p. 282); SIM 05 track led SIM 04 by up to 0.114° | χ 0.11°, h 0.64 ft, V 0.19 ft/s; φ 0.81°, ψ 0.79°, p 24 °/s (all transient timing at 0.1-s logging); end 5.37 ft |
| 13.4 | SIM 04–05 11.6 ft; 05–02 40.3, 02–04 31.8 ft (p. 296); final heading deviation +0.14…+0.21° (p. 295) | χ 0.11°, h 0.65 ft, V 0.19 ft/s; φ 0.66°, p 18 °/s (transient); end 11.57 ft; cross-track at 60 s 2 009.1 vs 2 000.3 ft |

Recomputed SIM 02 separations (30.6, 30.2, 73.6 ft) do not match NASA's (19, 14.3, 40.3 ft); NASA attributes SIM 02's position to its geodetic derivation (p. 296), so SIM 02 positions are unusable. INFERENCE.

Vol II Table 79 (p. 593) "largest differences, % of largest reference value", SIM 04/05, first 8 s excluded: read in row order as 11 → 2.786 %, 12 → 0.631, 13.1 → 1.080, 13.2 → 0.218, 13.3 → 14.947, 13.4 → 16.312, 15 → 11.845, 16 → 19.513. VERIFIED (values and footnotes) / INFERENCE (row alignment — the extracted table is shifted by one row). The large 13.3/13.4 values come from "mismatched timing of pitching moment transients" (fn e). VERIFIED.

### 5.2 Staged plan (each stage validation-only; production physics, coefficients, mass/inertia and control laws untouched)

0. **Offline reference (no Unity).** Add `gt` to `MavDaveMlEvaluator`; unit-test `F16_control.dml` against hand-computed points (trim identity el −3.2410°, PWR 13.9019; saturations; χ wrap). Build a double-precision flat-Earth RK4 reference with NESC aero/prop/law and g_eff; reproduce case 11 trim and the 13.x responses; compare with SIM 04/05 on the robust signals. This quantifies the flat-Earth equivalence error before any Maverick numerics enter.
1. **NESC stack on `MavSixDoFBody`** (components 1–5, §3.3); static gates: check-shots, trim α/θ/stick/throttle vs SIM 05 (target agreement far below the 0.017° flat-vs-rotating difference), Ixz sign, MRC transfer (SIM 04/05 CM moments ≈ 0 at trim).
2. **Case 11** at dt 0.02/0.01/0.005/0.0025: ΔV, Δh, θ, α, q vs SIM 04/05; report ψ/χ/position separately with the §2 expectations (≈ +0.53° and ≈ 630 ft rightward in NASA, ≈ 0 flat).
3. **13.1, 13.2** (longitudinal; SIM 04/05 only), both LQR-reference variants, with and without the one-step latency.
4. **13.3, 13.4** (lateral; saturating), with the 13.4 cross-track logic; expect a +0.10° φ offset in steady heading-hold; compare after the transient window as NASA does (first 8 s excluded) and report transient timing separately.
5. **Report** against the SIM 04–SIM 05 envelope as observed spread, never as a pass criterion; record every equivalence (g_eff, US 1976 geopotential, latency choice, reference variant) as a configuration line.
