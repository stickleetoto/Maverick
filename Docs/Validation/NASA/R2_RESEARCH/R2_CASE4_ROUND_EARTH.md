# NESC Atmospheric Check Case 4 — Dropped Sphere, Round Non-Rotating Earth

Research for the next NASA validation phase after R1 (cases 2 and 3). Research only: nothing in any repository was changed. Every key claim is tagged **VERIFIED** (with its source) or **INFERENCE**.

Abbreviations: Vol I = NASA/TM-2015-218675 summary (NESC-RP-12-00770 Vol I); Vol II = the appendices (Vol II). Page numbers are the document's own "N of 609" or "N of 26" labels. "Data" means `E:\unity project\_nesc_source\extracted\cases\Atmospheric_checkcases\Atmos_04_DroppedSphereRoundNonRotation\`.

Reproducibility scripts (stdlib only, run with `python -I`), all in `Tools/nasa_6dof/research/scripts/`:
- `case_summary.py`: summarises cases 1, 4, 5 and 6.
- `case4_analysis.py`: inter-tool differences, the independent reference, model-convention effects and the attitude check.
- `case4_extra.py`: the envelope rule, a float32 emulation of semi-implicit Euler, and the sensitivity control.
- `xlsx_dump.py`: parses `Initial_Conditions.xlsx`.

---

## 1. Exact case 4 definition

| Item | Value | Tag / source |
|---|---|---|
| Purpose ("Verifies") | Gravitation, integration | VERIFIED Vol I Table 6.3-1 (p. 15 of 26); Vol II Table 72 row 4 |
| Vehicle | the atmospheric sphere: m = 1.0 slug; Ixx = Iyy = Izz = 3.6 slug·ft²; products of inertia 0; CM at the reference point | VERIFIED Vol II §B.1.1 Table 1 (p. 12); `models\cannonball_inertia.dml` |
| Aerodynamics | S = 0.1963495 ft² (3 in radius); **CD = 0.1 (constant)**; CL = CY = Cl = Cm = Cn = 0. **Drag only.** | VERIFIED Vol II Table 2 (p. 12); `models\cannonball_aero.dml` (Revision E, 2013-04-19) |
| Initial position | geodetic lat 0°, lon 0°, 30 000 ft MSL. Geocentric radius r0 = 20 932 254.5305 ft (sphere radius + 30 000 ft) | VERIFIED Vol II Table 28 (p. 59); data `eiPosition_ft_X` at t = 0 (SIM 4, 5, 6) |
| Initial velocity | [0, 0, 0] ft/s, Earth-relative (equal to inertial, because the Earth does not rotate) | VERIFIED Table 28 |
| Initial attitude | Euler [0, 0, 0] deg: body x north, y east, z down | VERIFIED Table 28; xlsx column H rows 39–41 |
| Initial rates | body rates [10, 20, 30] deg/s, both Earth-relative and inertial | VERIFIED Table 28; xlsx rows 18–25 (column H) |
| Earth model | round sphere, **non-rotating** (Ω = 0); sphere radius 6 371 007.1809 m = 20 902 254.5305 ft | VERIFIED Table 28; Vol II Table 73 (p. 93) |
| Gravitation | **inverse square, point mass**: A = −μ R/\|R\|³ with μ = 3.986004418 × 10¹⁴ m³/s² = 1.407644175720511 × 10¹⁶ ft³/s². **No J2.** | VERIFIED Vol II §B.4.2 eq 16–19 (pp. 44–45), eq 26–27 (pp. 46–47), Table 73, §C.1.4 |
| Gravity vs gravitation | the Earth does not rotate, so there is no centrifugal term: gravity equals gravitation | VERIFIED Vol II §B.2 footnote 1 (p. 37); INFERENCE that this makes case 4 cleaner than case 3 (R1 needed a −Ω²r term there) |
| Atmosphere | US Standard Atmosphere 1976, no wind | VERIFIED Table 28; xlsx row 197 |
| Duration / output rate | 30 s; SIM 2, 4 and 6 at 0.1 s (301 rows); SIM 5 at 0.01 s (3 001 rows) | VERIFIED data |
| Tools | **4**: SIM 02, 04, 05, 06 (no SIM 01 or 03) | VERIFIED data folder; Vol II §D.1.4 (p. 136) |

Check values from the data (VERIFIED, SIM 5):
- At t = 0: g = 32.12631207052 ft/s², ρ = 8.906867504 × 10⁻⁴ slug/ft³, T = 411.8389 °R.
- At t = 30 s: altitude = 16 231.307 ft, v_down = 867.1048 ft/s, g = 32.16862 ft/s², Mach 0.8240, q̄ = 540.24 lbf/ft².

My own evaluation of μ/r0² reproduces NASA's t = 0 `localGravity` to all 10 printed digits (32.1263120705), which confirms the Table 73 constants (VERIFIED by computation, `case4_analysis.py`).

**Constants caution.** Column H (case 4) of `Initial_Conditions.xlsx` disagrees with Table 73 and with the tools' own data (VERIFIED, `xlsx_dump.py`):

| Quantity | xlsx column H | Table 73 / tools' data |
|---|---|---|
| Sphere radius | 20 902 255.199 ft | 20 902 254.5305 ft |
| μ | 1.407644311 × 10¹⁶ ft³/s² | 1.407644175720511 × 10¹⁶ ft³/s² |
| "Dist. from Z axis" / inertial X | 20 955 646.995 ft (WGS-84 a + 30 000 ft) | 20 932 254.5305 ft |
| Inertial east velocity | 1 528.1 ft/s (a rotating-Earth leftover) | 0 (the Earth does not rotate) |

**Use Table 73.** All three agreeing tools use the Table 73 values (r0 = 20 932 254.5305 ft).

### CSV columns (VERIFIED, file headers)

| Tool | Columns | What it has |
|---|---|---|
| SIM 02 | 27 | time; `feVelocity_ft_s_X/Y/Z`; `altitudeMsl_ft`; `longitude_deg`; `latitude_deg`; `localGravity_ft_s2` (**all 0.0, not populated**); `eulerAngle_deg_Yaw/Pitch/Roll`; `bodyAngularRateWrtEi_deg_s_Roll/Pitch/Yaw`; `altitudeRateWrtMsl_ft_min`; `speedOfSound_ft_s`; `airDensity_slug_ft3`; `ambientPressure_lbf_ft2`; `ambientTemperature_dgR`; `aero_bodyForce_lbf_X/Y/Z`; `aero_bodyMoment_ftlbf_L/M/N`; `mach`; `trueAirspeed_nmi_h` |
| SIM 04 | 32 | adds `eiPosition_ft_X/Y/Z`, `eiVelocity_ft_s_X/Y/Z` and `dynamicPressure_lbf_ft2`; no altitude rate and no TAS |
| SIM 05 | 38 | SIM 04's set plus `gePosition_ft_X/Y/Z`, `altitudeRateWrtMsl_ft_min` and `trueAirspeed_nmi_h`; **`feVelocity_ft_s_Z` appears twice** (last column) |
| SIM 06 | 37 | SIM 05's set without the duplicate |

### How case 4 differs from cases 1, 5 and 6

| Case | Drag | Earth / gravitation | Initial inertial rates | Tools | Altitude at 30 s (ft) | v_down at 30 s (ft/s) |
|---|---|---|---|---|---|---|
| 1 | **none** | WGS-84, rotating, J2 | 0 (Earth-relative roll −4.178 × 10⁻³ deg/s cancels Ω) | 6 | 15 598.90 | 960.29 |
| **4** | CD 0.1 | **round, non-rotating, 1/R²** | 10, 20, 30 deg/s | 4 | **16 231.31** | **867.10** |
| 5 | CD 0.1 | round, rotating, 1/R² | inertial 10, 20, 30 (Earth-relative roll 9.9958) | 4 | 16 276.39 | 864.48 |
| 6 | CD 0.1 | WGS-84, rotating, J2 (= case 1 plus drag) | 0 | 6 | 16 284.45 | 864.01 |

Sources: VERIFIED Vol I Table 6.3-1; Vol II §C.1.1–C.1.6 (pp. 56–60); data (`case_summary.py`).

Case 4 is the only atmospheric case with **no Earth rotation**. The 45 ft difference between cases 4 and 5 is the centrifugal and rotation effect: INFERENCE, consistent with the 0.1116 ft/s² centripetal value in xlsx row 135.

---

## 2. What a flat-world simulator can compare

**Key geometric fact (INFERENCE, from the equations of motion).** The drop starts at rest on the equator above a non-rotating sphere. The only forces are radial gravitation and drag anti-parallel to the velocity. So:
- the trajectory stays on the initial radial line;
- the local NED frame stays fixed in inertial space (no transport rate, no Coriolis);
- "down" is always the same inertial direction.

A flat world with Unity −y as that direction is therefore **geometrically exact** for case 4. **Curvature contributes nothing.** The only round-Earth effect is the **magnitude** of gravity, μ/(R_s + h)².

The data are consistent with this (VERIFIED): `latitude_deg`, `longitude_deg`, `feVelocity_X/Y` and `eiPosition_Y/Z` stay at 10⁻¹⁵ or less in SIM 4, 5 and 6.

### Comparable outputs

| Output | Status | How |
|---|---|---|
| `altitudeMsl_ft`, `feVelocity_ft_s_Z`, `altitudeRateWrtMsl_ft_min`, `trueAirspeed_nmi_h`, `mach`, `dynamicPressure_lbf_ft2` | directly comparable | Unity y = h; v_D = −v_y; airspeed = \|v\| (no wind) |
| `eiPosition_ft_X` / `gePosition_ft_X` | derived only | R_s + h (identical, because the Earth does not rotate) |
| `feVelocity_ft_s_X/Y`, `latitude_deg`, `longitude_deg`, `eiPosition_Y/Z` | comparable as **≈ 0 checks** | lateral drift: lat ≈ z/R_s, lon ≈ x/R_s |
| `eulerAngle_deg_*` | **directly comparable** (unlike case 3) | NED is inertial here; no rotating-frame transform is needed |
| `bodyAngularRateWrtEi_*` | directly comparable | constant 10/20/30 deg/s |
| `aero_bodyForce_lbf_X/Y/Z` | comparable | depend on attitude; verified \|F\| = q̄·S·CD exactly in SIM 5 at 30 s (10.607638 lbf both ways) |
| `aero_bodyMoment_*` | all zero | trivially comparable |
| `localGravity_ft_s2` | comparable **only** if the validation applies 1/R² (design B, §3) | a constant-g run cannot reproduce it |
| `airDensity`, `ambientPressure`, `ambientTemperature`, `speedOfSound` | comparable, with a **known convention offset** | Maverick's `MavAtmosphereModel` evaluates the layer formula at geometric altitude (R1 discrepancy D-ATM). Density is −1.587 × 10⁻³ relative at 30 000 ft and −4.16 × 10⁻⁴ at 16 231 ft (VERIFIED by computation) |

**Attitude has a closed form (VERIFIED by computation).** For a sphere (I = 3.6·𝟙) with zero aerodynamic moment, ω is constant in both body and inertial axes. The attitude is a fixed-axis rotation at |ω| = 37.41657 deg/s.
- The closed form matches SIM 5 to 4.0 × 10⁻¹² deg, SIM 6 to 2.7 × 10⁻¹⁰ deg and SIM 4 to 1.1 × 10⁻⁸ deg.
- At 30 s the Euler angles (yaw, pitch, roll) are 37.4532°, 17.7466°, 17.9253°.
- The gyroscopic term ω × Iω is exactly zero.

So case 4 checks Maverick's attitude kinematics and Unity↔NED rate mapping against an analytic answer. It does **not** check rotational dynamics; R1 already covered those.

### Size of the round-Earth vs flat-Earth difference over 30 s

Model: double-precision RK4 at h = 1 ms (VERIFIED by computation, `case4_analysis.py`).

| Quantity | Value |
|---|---|
| Gravity variation along the path | 32.12631 → 32.16862 ft/s² (+0.132 %); first-order estimate 2gΔh/R = 0.0423 ft/s² |
| Drag-free, 1/R² vs constant g(h0) | Δh = −3.33 ft, Δv = +0.444 ft/s (drop 14 460 ft) |
| With drag (case 4), 1/R² vs constant g(h0) | Δh = **−3.00 ft**, Δv = **+0.374 ft/s** at 30 s. Drag feedback damps it slightly; drag/weight reaches 0.33 at 30 s |
| Constant 9.80665 m/s² (32.174 ft/s²) + geometric atmosphere vs NASA model | −16.95 ft, +0.81 ft/s (the 0.15 % larger g dominates) |
| Constant g(h0) + geometric atmosphere vs NASA model | +2.36 ft, −0.31 ft/s |
| **1/R² + geometric atmosphere (Maverick-equivalent, design B) vs NASA model** | **−0.633 ft, +0.064 ft/s**: the pure D-ATM effect (lower density gives less drag) |
| Curvature or Coriolis for this vertical drop | 0 (INFERENCE, geometric argument above) |

---

## 3. Candidate validation designs

Constraint: no production physics change, and no hidden correction that forces agreement.

**Design A — R1 pattern, constant gravity.**
- How it works: set `Physics.gravity` to g(h0) = 9.79210 m/s² for the run and restore it afterwards. Two references: a NASA-equivalent one (1/R², US76 at geopotential altitude) checked against NASA, and a Maverick-equivalent one (constant g(h0), geometric atmosphere) checked against Maverick.
- Legitimate? **Yes**, and it is identical in kind to R1.
- Weakness: it does not exercise the "gravitation" that case 4 exists to verify. The model gap against NASA (≈ 2.4 ft, 0.31 ft/s) has to be reported rather than closed.

**Design B — 1/R² as a validation-only environment, through the existing load path. Recommended.**
- How it works: a validation-only `MavFlightDynamicsProfileProvider` subclass overrides `ResolveEnvironment`. It returns `gravitySource = ResearchSourceGravityThroughLoadSet` with `loadSetGravityMps2 = μ/(R_s + y)²`, evaluated from the body's own current altitude each step. Then:
  - `MavSixDoFBody` adds it as the load set's separate gravitational channel, once;
  - `Rigidbody.useGravity` is off;
  - `Physics.gravity` is untouched.
- Legitimate? **Yes.** This production mechanism already exists and is already used by the F-15 research runtime (`MavF15AfitResearchRuntimeEnvironment.cs` lines 201–202; `MavF15PilotControlledFlightDynamicsProfile.cs` lines 97–98). Double application is refused by `TryAddEnvironmentGravity` (VERIFIED: `Core/MavFlightEnvironment.cs`; `Core/MavSixDoFBody.cs` lines 620–760).
- The gravity is a **declared case input** (Table 28 specifies it). It is computed from the body's state, not fitted. It is also physically exact for this geometry (§2). Its direction, Unity −y, is exactly radial for a vertical drop.
- Maverick-equivalent reference: 1/R² plus geometric atmosphere. Remaining Maverick-vs-NASA model gap: 0.63 ft and 0.064 ft/s (D-ATM only).

**Design C — reference only.** Run the independent integrator against NASA and stop. It checks the reference, not Maverick. It is acceptable only as step 1 of A or B.

**Not legitimate (reject):**
- Applying a force sized from NASA's `localGravity` **time series** (open-loop by time). That feeds in the answer.
- Any correction force or bias computed from the Maverick–NASA difference.
- Writing `Physics.gravity` every step. That is a global side channel outside the single load-application boundary, and the load-set channel exists for exactly this.
- Supplying a per-step US76-geopotential density under `MavDensitySource.ResearchSourceFixedDensity`. The value varies, so the "fixed" label would be false, and it would silently replace the atmosphere under test.
- Post-hoc subtraction of the analytic 1/R² offset from a constant-g run, then gating on the result. A decomposition like that may be **reported**, never gated.

All of these are INFERENCE: policy judgements against the stated constraint.

**Why case 4 adds value beyond R1 (INFERENCE from the R1 docs).** R1 cases 2 and 3 had CD = 0, so the aerodynamic **force** dimensionalisation and translational path were never exercised (`CASE3_DAMPING_REPORT.md` §1; `VALIDATION_LIMITATIONS.md` §3 lists case 4 as "not run"). Case 4 exercises:
- coefficient → q̄S → aero-body force → Unity axes;
- the gravitational load-set channel;
- translational integration with state-dependent force.

A sphere aero model needs `cx, cy, cz = −CD·(u, v, w)/V` in aero-body axes, with zero at V = 0.

---

## 4. Inter-tool agreement reported for case 4 (observations, NOT thresholds)

**NASA text (VERIFIED).**
- SIM 4, 5 and 6 "agreed on the predicted translational states".
- SIM 2 differs for three reasons:
  - its initial north velocity was 0.01 ft/s, giving a latitude difference of about 8 × 10⁻⁷ deg;
  - its gravity is about 0.05 ft/s² lower (a truncated μ);
  - it uses a tabular atmosphere.
- A SIM 5 altered to use a lookup table and the lower μ matched SIM 2's altitude to within 0.006 ft.
- SIM 2's Euler angles differ, attributed to step size or integration order.
- Angular rates stay constant in every tool.

Source: Vol II §D.1.4 (p. 136); §E.1.2 (pp. 589–590), which lists "SIM 2 appears to have used a truncated gravitational constant" and excludes SIM 2's X-body force, local gravity and Euler angles.

**Table 77 (VERIFIED, Vol II p. 591; extracted columns are shifted by one row).** Case 4 largest % difference: SIM 2 9.998 %, SIM 4/5/6 3.333 % each.

*INFERENCE:* the 3.333 % is an artifact of averaging SIM 2 into a four-tool ensemble. A deviation split 3:1 gives exactly 9.998/3. It does not reflect SIM 4/5/6 disagreement. **These percentages must not become gates** (R1 policy, `NUMERICAL_CONVERGENCE.md` §4).

**Computed from the data (VERIFIED, `case4_analysis.py`): maximum over 0–30 s, 0.1 s grid.**

| Signal | SIM 4–5 | SIM 4–6 | SIM 5–6 | SIM 2–4 |
|---|---:|---:|---:|---:|
| altitude (ft) | 9.8e-4 | **1.07e-2** | 9.8e-3 | 18.11 |
| v_down (ft/s) | 1.5e-4 | **1.27e-3** | 1.1e-3 | 1.064 |
| localGravity (ft/s²) | 3.0e-9 | 3.3e-8 | 3.0e-8 | n/a (column = 0) |
| density (slug/ft³) | 3.1e-9 | 1.9e-8 | 1.8e-8 | 8.1e-7 |
| q̄ (lbf/ft²) | 1.0e-3 | 4.7e-3 | 3.9e-3 | n/a |
| Euler angles (deg) | 1.1e-8 | 1.1e-8 | 2.6e-10 | 5.15 |
| aero body force (lbf) | 1.6e-5 | 9.1e-5 | 7.5e-5 | 0.74 |

The t = 0 density differs by up to 2.1 × 10⁻⁵ relative across tools (SIM 6 is highest). INFERENCE: this reflects atmosphere-implementation differences, as Vol I §7.1 says.

**Independent reference: NASA-equivalent model** (double RK4, h = 1 ms; 1/R²; US76 at geopotential altitude, r0 = 6 356 766 m, R = 287.0531). Measured (VERIFIED, `case4_analysis.py`, `case4_extra.py`):
- Self-check (h = 1 ms vs 0.5 ms): 1.5 × 10⁻¹⁰ ft, 7.8 × 10⁻¹² ft/s.
- Against SIM 4: max 1 × 10⁻⁴ ft, 2 × 10⁻⁵ ft/s.
- Against SIM 5: 1.0 × 10⁻³ ft, 1.7 × 10⁻⁴ ft/s.
- Against SIM 6: 1.08 × 10⁻² ft, 1.29 × 10⁻³ ft/s.
- R1's envelope rule (distance to SIM 4 ≤ max distance of SIM 5 or 6 to SIM 4, + 10⁻⁹): **inside at all 300 samples** for altitude and for v_down.

---

## 5. Gaps, blockers and recommended plan

### Gaps and blockers
1. **No blocker in the data.** Three consistent tools (4, 5, 6) are available; SIM 2 is excluded on NASA's own stated grounds (VERIFIED §E.1.2, Table 77 footnotes a and b).
2. **The R1 reference cannot run case 4 as it is** (VERIFIED, `MavNasaRigidBodyReference.cs`):
   - it has no translational drag term;
   - its gravity modes are only Constant and EquatorialJ2Rotating;
   - its atmosphere is troposphere-only, which is fine here because the case stays between 4 947 and 9 144 m.

   Recommendation (INFERENCE): add a **new** validation-only translational reference rather than editing the R1 file. That file is an R1-pinned validation surface. New validation `.cs` files must also be added to `fdm_validation_baseline_v1.json` (per project memory).
3. **A new validation-only `MavAerodynamicModelBase`** is needed for the sphere's drag. S = 0.018241 m² is above the inspector `[Min(0.01f)]` on `wingAreaM2` (VERIFIED, `MavFlightDynamicsTypes.cs`). Span and chord are irrelevant because all moments are zero.
4. **A new validation-only profile provider** is needed for design B (`ResolveEnvironment` is virtual; `BuildProfile` is abstract). VERIFIED: `MavFlightDynamicsProfile.cs` lines 173–190.
5. **D-ATM is a known, unresolved convention difference.** Maverick vs NASA directly will carry −0.63 ft and +0.064 ft/s from the atmosphere convention alone. **Report it; do not correct it.**
6. **Float32 position.** At y ≈ 9 km the float32 step is about 1 mm. A float32 emulation of semi-implicit Euler still shows near-first-order convergence (VERIFIED by computation):

   | dt (s) | altitude error (m) | velocity error (m/s) |
   |---:|---:|---:|
   | 0.02 | 2.877 | 0.0213 |
   | 0.01 | 1.417 | 0.0107 |
   | 0.005 | 0.739 | 0.0050 |
   | 0.0025 | 0.373 | 0.0027 |

   R1 case 3's altitude orders were irregular (1.08, 0.82, 1.78 from its §4 table). INFERENCE: float32 noise, so gate altitude order on the first two halvings only, as R1 did.
7. **PhysX attitude integration for constant ω** is believed to be an exact angle-axis update, leaving only float32 drift. That is INFERENCE from memory of PhysX source and was not verified locally, so **measure it before fixing the attitude gate**.

### Recommended plan (design B, with A as the control)

1. **[C1] Source identity.** Hash `cannonball_aero.dml`, `cannonball_inertia.dml` and the four CSVs; record the Table 73 constants; flag the xlsx column-H inconsistencies.
2. **[C2] Reference self-check.** Gate 10⁻⁶ ft and 10⁻⁷ ft/s. Basis: at least 100× below the smallest difference it judges (reference vs SIM 4: 10⁻⁴ ft, 2 × 10⁻⁵ ft/s). Observed: 1.5 × 10⁻¹⁰ and 7.8 × 10⁻¹².
3. **[C3] Reference vs NASA.** R1 envelope rule on altitude, v_down, density, q̄, aero force and Euler angles against SIM 4 with SIM 5 and 6 as the envelope; SIM 2 excluded. Plus the analytic attitude check (closed form vs SIM 5 at 4 × 10⁻¹² deg; gate 10⁻⁶ deg).
4. **[C4] Maverick (design B) vs the Maverick-equivalent reference** (1/R², geometric atmosphere), at dt 0.02, 0.01, 0.005 and 0.0025:
   - observed order 0.7–1.3 on the first two halvings, with monotone decrease;
   - expected magnitudes 2.87 m / 0.021 m/s at dt 0.02, halving each step. Report Maverick against these; gate only the order and the monotone decrease.
5. **[C5] Mechanics.**
   - exactly one load application per step and one gravitational channel;
   - `Rigidbody.useGravity` false and `Physics.gravity` unchanged; refusal counters zero; Unity damping zero; `sleepThreshold = 0`.
   - **Gravity chain:** applied force = m·μ/(R_s + y)² to 10⁻⁵ relative (float32, same basis as the R1 density chain).
   - **Drag chain:** \|F_aero\| = ½ρV²·S·CD recomputed from the body state to 10⁻⁴ relative (R1 moment-chain basis); direction anti-parallel to velocity.
   - Lateral drift \|x\|, \|z\| ≤ 0.01 m. Basis: about 100× the expected float32 rotate-and-back residual (≈ 10⁻⁴ m; INFERENCE).
   - Body rates constant: gate on the measured float32 floor.
6. **[C6] Sensitivity control** (like R1's gyroscopic control). Run the same rig with constant g(h0) through the same channel. The final v_down difference must be > 10× Maverick's dt = 0.0025 velocity error. Expected 0.374 ft/s vs ≈ 0.0087 ft/s, about 43×. Altitude is less discriminating (3.0 ft vs 1.2 ft), so report it only.
7. **[C7] Reported, not gated.**
   - Maverick vs SIM 4 directly: expected ≈ semi-implicit offset, 9.4 ft at dt 0.02 to 1.2 ft at 0.0025, plus the D-ATM −0.63 ft / +0.064 ft/s.
   - Atmosphere columns vs NASA (D-ATM).
   - The constant-9.80665 run (−17 ft), to show what plain project gravity would give.
   - NASA's Table 77 percentages, quoted for context only.

All gate numbers above are proposals derived from the computed magnitudes (INFERENCE). None of them is a NASA acceptance threshold.
