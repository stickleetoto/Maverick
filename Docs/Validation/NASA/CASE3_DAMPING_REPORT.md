# NASA Check Case 3 — Tumbling Brick with Aerodynamic Damping

**Suite:** `MavNasaBrickCheckCaseValidation` sections [B3]–[B4] · **Unity** `6000.3.16f1` · suite result 83 / 0 (run `brick2`).

---

## 1. NASA case definition

| Item | Value | Source |
|---|---|---|
| Vehicle | the case 2 brick | `brick_inertia.dml`, Vol II Table 4 |
| Aerodynamics | S 0.22222 ft², b 0.33333 ft, c 0.66667 ft; Clp = Cmq = Cnr = −1, Clr = Cnp = 0; **CD set to zero**, no lift or side force | `brick_aero.dml` (SHA-256 `a5883cfbde33…f31d`), Vol II Table 5, §C.1.3, Table 27 |
| Damping law (model file) | Cl = Clp·p̃ + Clr·r̃, Cm = Cmq·q̃, Cn = Cnp·p̃ + Cnr·r̃ with p̃ = p·b/2V, q̃ = q·c/2V, r̃ = r·b/2V | `brick_aero.dml` MathML |
| **Airspeed floor** | the model's input `trueAirspeed` (VRW) has **minValue = 0.5 ft/s** | `brick_aero.dml` (not in the report tables) |
| Initial state | as case 2: dropped from rest relative to the rotating air at 30 000 ft; inertial rates 10, 20, 30 deg/s | Vol II Table 27 |
| Expected behaviour (report) | "should start to damp out as the velocity … (airspeed) increased" | Vol II §C.1.3 |

**Is damping singular at zero airspeed?** No — verified two ways. Dimensionally the moment is q̄·S·b·Clp·p̃ = ¼ρV·S·b²·Clp·p, proportional to V, so it vanishes at V = 0. In the NASA model the normalisation also uses max(V, 0.5 ft/s), so for V < 0.5 ft/s (the first ≈ 16 ms of the fall) the moment is ∝ V² and below that floor nothing is ever divided by zero. *Inference:* the floor applies to the aero model's input only; the host simulation dimensionalises with the true q̄. This refines the Phase 2 statement "damping scales with airspeed".

---

## 2. Two references, two questions

| Reference | Assumptions | Used to judge |
|---|---|---|
| **NASA-equivalent** | US 1976 standard atmosphere at **geopotential** altitude; radial J2 gravitation at the equator **minus the centripetal term Ω²r** of a body co-moving with the rotating Earth (Vol II Table 73: a = 6 378 137 m, μ = 3.986004418 × 10¹⁴ m³/s², J2 = 0.00108262982, Ω = 7.292115 × 10⁻⁵ rad/s); airspeed = fall speed | whether the reference reproduces NASA |
| **Maverick-equivalent** | constant g = 9.786072 m/s² (NASA local gravity at t = 0, set as `Physics.gravity` for the run); the standard-layer formula evaluated at **geometric** altitude (Maverick's convention), implemented independently | whether Maverick integrates correctly |

Separating them keeps model-assumption differences (gravity, atmosphere convention) out of the integration-error measurement.

---

## 3. NASA-equivalent reference vs NASA ([B3])

Reference self-consistency (h = 1 ms vs 0.5 ms): **1.960 × 10⁻⁸** deg/s.

| Tool | max \|Δω\| (deg/s) | max attitude difference (deg) |
|---|---:|---:|
| sim 01 | 7.882 × 10⁻² | 0.3045 |
| sim 02 | 2.695 × 10⁻³ | 0.5738 |
| sim 04 | **3.358 × 10⁻⁴** | **3.673 × 10⁻³** |
| sim 05 | 4.178 × 10⁻³ | 9.912 × 10⁻² |
| sim 06 | 4.178 × 10⁻³ | 9.785 × 10⁻² |

- Translational agreement with sim 04: max |Δaltitude| **0.023 ft**, max |ΔTAS| **7.7 × 10⁻⁴ ft/s** over 30 s.
- Envelope rule: **PASS** for rates (worst ratio 0.025) and attitude (worst ratio 0.012, SIM 2 excluded as in Vol II §E.1.2).
- The NASA tools disagree with each other far more in case 3 than in case 2 (e.g. sim 01 vs sim 04: 0.08 deg/s; final attitudes up to ≈ 0.6° apart). Sim 01's density column differs from the others (consistent with Vol II's note that tabular and equation-based atmospheres were both used).
- **Gravity finding.** Without the −Ω²r term the reference fell ≈ 0.35 % faster than every tool (3.3 ft/s at 30 s). NASA's `localGravity` column is gravitation only; a body dropped with Earth's eastward velocity feels it reduced by the centripetal term (≈ 0.034 m/s² at the equator). Including it closes the gap to 0.001 ft/s.

---

## 4. Maverick backend vs the Maverick-equivalent reference ([B4])

| dt (s) | max \|Δω\| (deg/s) | max attitude error (deg) | max \|Δh\| (m) |
|---:|---:|---:|---:|
| 0.0200 | 8.030 × 10⁻² | 0.2724 | 2.968 |
| 0.0100 | 4.010 × 10⁻² | 0.1367 | 1.407 |
| 0.0050 | 2.002 × 10⁻² | 0.06856 | 0.7968 |
| 0.0025 | 9.961 × 10⁻³ | 0.03713 | 0.2323 |

Gated results (all **PASS**):
- mechanics at every step size (as in case 2);
- **first-order convergence**: rates 1.002, 1.002, 1.007; attitude 0.995, 0.996 (finest pair 0.885, reported only);
- **dissipation**: rotational kinetic energy never increases between samples at any step size;
- **zero-airspeed start**: the first step (V = 0) produces a finite, exactly zero aerodynamic moment;
- **production damping chain**: the body's own aerodynamic moment equals ½ρV²·S·b·Cl(p·b/2V′) recomputed here from the body's state, relative error **5.9 × 10⁻⁸**;
- **production atmosphere**: `MavAtmosphereModel` density equals the independent standard-layer formula at geometric altitude to **3.4 × 10⁻⁷** (float precision).

The altitude error at dt 0.02 (2.97 m after 30 s) is the textbook semi-implicit-Euler offset g·dt·t/2 = 9.786 × 0.02 × 30 / 2 = 2.94 m.

**Unity angular damping** was zero in every run (gated). No general-purpose Rigidbody damping is involved; the decay is entirely the aerodynamic damping moment applied through `MavSixDoFBody`.

---

## 5. Discrepancies found

| ID | Discrepancy | Size | Classification |
|---|---|---|---|
| D-ATM | `MavAtmosphereModel` evaluates the standard-atmosphere layer formula with **geometric** altitude; US 1976 defines it in **geopotential** altitude | max relative density difference **1.58 × 10⁻³** between 30 000 and 15 551 ft | model-convention difference (O(h/R)); documented, not changed |
| D-GRAV | a flat, constant-g world cannot represent J2 variation and the rotating-Earth centripetal term | ≈ 0.35 % in effective gravity for this case | configuration mismatch (expected for a flat-earth simulator) |

Combined effect, Maverick (dt 0.0025) vs NASA sim 04 directly: max |Δω| **2.22 × 10⁻²** deg/s, max attitude **0.295°** — larger than the integration error at that step and dominated by the two model differences above. Reported, not gated.

---

## 6. What this shows and what it does not

- Shows: Maverick's load path (aero model → `Dimensionalize` with the body's q̄ → aero-body to Unity axial-vector mapping → one application per step) applies rate damping correctly, with the right sign and magnitude, through zero airspeed; integration converges at first order.
- Does not show: any aircraft aerodynamics, compressibility, or real-vehicle damping.
