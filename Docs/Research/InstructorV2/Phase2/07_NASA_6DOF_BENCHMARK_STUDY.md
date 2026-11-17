# 07 — NASA NESC 6-DoF Check Cases: Benchmark Study

**Question:** can Maverick be checked against NASA's published six-degree-of-freedom check cases, at what level, and how do we keep numerical error, model-data difference and controller difference apart?

**Short answer:** yes, at three different levels, but **no NESC case is flat-earth**. Every F-16 case runs on a rotating WGS-84 ellipsoid with J2 gravitation. That rules out direct trajectory overlay of Maverick's flat Unity world. It still leaves a strong programme of equation-of-motion checks, coefficient and trim checks, and relative-response comparisons, provided each comparison removes the effects Maverick does not model and says so.

**Evidence classes:** PV / SV / EI / UV / MP as in doc 02. External content was read by a research sub-agent of this session on 2026-11-17, except where "lead" is noted. Source keys are in doc 09.

---

## 1. What is published (source verification)

### 1.1 Documents and data

| Item | Where | Inspected | Class |
|---|---|---|---|
| NESC check-case site (landing, Bodies, Datum, Gravity, Atmosphere, Output specs, case pages acc01–acc17, errata) | `https://nescacademy.nasa.gov/flightsim/2015` | yes | PV |
| NASA/TM-2015-218675 **Vol I** (31 pp.), Murri, Jackson, Shelton, Jan 2015 | NTRS 20150001263 | full text | PV |
| NASA/TM-2015-218675 **Vol II** (appendices, 614 pp.) | NTRS 20150001264 | **not read** (file > 10 MB, beyond the fetch tool) | UV |
| AIAA 2015-1810 (Jackson, Madden et al., final results, 34 pp.) | NESC site, Reports | full text | PV |
| AIAA 2013-5071 (Jackson, Shelton et al., preliminary, 156 pp.) | NESC site, Reports | partial | PV |
| Data zips: `Atmospheric_checkcases.zip` (24.9 MB), `Orbital_checkcases.zip` (11.1 MB), model zips (`Atmospheric_models.zip`, `All_models.zip`, `F16_package.zip`), `Initial_Conditions.xlsx` | NESC landing page | listed; not downloaded | PV (existence) |
| NASA `simupy-flight` redistribution of the F-16 DAVE-ML files and case 11/13/15/16 CSVs | `github.com/nasa/simupy-flight` | partial | PV |
| Repository library file-level verification of `F16_aero.dml`, `F16_inertia.dml`, `F16_prop.dml`, `F16_control.dml`, README v6, 24 F-16 CSVs | `Docs/Research/Aerospace/Aircraft/F16/KNOWN_DATA.md` (R2) | lead read the library record | REPO_DOC |

### 1.2 Cases relevant to an atmospheric fighter simulator

| # | Vehicle | Purpose (Vol I Table 6.3-1; AIAA 2015-1810 Table 1) | Earth / gravity / atmosphere / wind | Duration |
|---|---|---|---|---|
| 1 | Dragless sphere | gravitation, translational EOM | WGS-84 rotating / J2 / US76 / none | 30 s |
| **2** | **Tumbling brick, all aero zero** | **rotational EOM** | WGS-84 rotating / J2 / US76 / none | 30 s |
| **3** | **Tumbling brick, Clp = Cmq = Cnr = −1, CD = 0** | **inertial coupling with damping** | same | 30 s |
| **4** | Sphere, CD = 0.1 | gravitation, integration | **round, non-rotating / inverse-square** / US76 / none | 30 s |
| 5 | Sphere, CD = 0.1 | Earth rotation | round rotating / inverse-square | 30 s |
| 6–8 | Sphere, CD = 0.1 | ellipsoid; steady wind; wind shear | WGS-84 / J2 | 30 s |
| 9–10 | Sphere launched east / north | translational EOM; Coriolis | WGS-84 / J2 | 30 s |
| **11** | **F-16, subsonic trim, then open loop** | atmosphere, air data, trim | WGS-84 / J2 / US76 / none; 10 013 ft, 565.69 ft/s TAS | 180 s |
| 12 | F-16, "supersonic" trim (~Mach 2, 30 013 ft) | supersonic air data | same | 180 s |
| **13.1–13.4** | **F-16 with the check-case LQR SAS + autopilot: +100 ft altitude step at 5 s; −5 kt KEAS at 5 s; 15° heading at 15 s; 2 000 ft lateral offset at 20 s** | multidimensional table lookup | same | 20 / 20 / 30 / 60 s |
| 14 | Supersonic F-16 manoeuvring | Mach effects | **never completed or published** (Vol I p. 13) | — |
| 15–16 | F-16 circling the North Pole / equator at the date line | geodetic transforms | same | 180 s |
| 17 | Two-stage rocket | staging | — | 200 s |

Orbital cases (ISS, sphere, cylinder; high-order gravity, third bodies, a non-public MET atmosphere) are irrelevant to Maverick.

### 1.3 Model assumptions

| Item | Specified | Source | Class |
|---|---|---|---|
| Earth | WGS-84 rotating ellipsoid for all F-16 cases; round non-rotating only in case 4, round rotating in case 5; flat earth described as an option but **used by no case** | Vol I §6.2.2; Datum page; AIAA 2013-5071 §III.A | PV |
| Earth rotation | 7.292115 × 10⁻⁵ rad/s | Datum page | PV |
| Gravitation | J2 (WGS-84 cases); inverse-square (cases 4–5) | Vol I §6.2.4; AIAA 2015-1810 §II.D | PV |
| Atmosphere | US Standard 1976; table vs equation implementations differed between tools and caused visible differences | Vol I §6.2.5, §7.1 | PV |
| Integration | **not specified**; each tool chose its own method and step | AIAA 2013-5071 p. 21; Vol I p. 19 | PV |
| Output | CSV with S-119 names; F-16 case 11 references sampled at **0.1 s**; column sets differ by tool | Vol I §6.2.6; CSVs | PV |
| Units / frames | English units; ECEF position, NED velocity, 3-2-1 Euler angles relative to NED, body rates relative to inertial space | Output-specs page; AIAA 2015-1810 Table 3 | PV (Table 3 partly garbled in extraction) |
| Tolerances | **none**; a predefined tolerance was deliberately rejected; agreement called "a subjective concept" | Vol I §5.0 p. 10; AIAA 2015-1810 p. 3 | PV |
| Tool identities | seven tools (Core, JEOD, JSBSim, LaSRS++, MAVERIC, POST II, VMSRTE); **SIM numbers deliberately not mapped to tools** | AIAA 2015-1810 p. 5 | PV |

### 1.4 The F-16 check-case model

| Item | Value / statement | Source | Class |
|---|---|---|---|
| Aero | `F16_aero.dml` "based on" Garza & Morelli NASA/TM-2003-212145, which implements the TP-1538 wind-tunnel data in tables | README v6 | SV (NASA redistribution) |
| Aero table grid | α −10:5:45 (12), elevator −24:12:24, \|β\| 0:5:30 | library, file-verified | REPO_DOC (file level) |
| Propulsion, inertia | Stevens & Lewis 2nd ed.; steady-state net thrust; MIL at PLA 50; no engine lag | README v6; `F16_prop.dml` | SV / REPO_DOC |
| Mass | 637.1595 slug (DAVE-ML) vs **637.26 slug on the NESC Bodies page** (0.016 % disagreement, recorded, unresolved) | library; Bodies page | REPO_DOC / PV |
| Inertia, geometry | 9 496 / 55 814 / 63 100 / Ixz 982 slug·ft²; S 300 ft², b 30 ft, c̄ 11.32 ft; MRC 35 % MAC; case CG 25 % MAC | library; Bodies page | REPO_DOC / PV |
| Controller | LQR SAS and autopilot designed at 10 000 ft / 287 KEAS; perfect state feedback; no integrators ("Type 0"); **not an F-16 FCS** | README v6 | SV |
| Envelope | "vicinity of 10,000 ft MSL and 287.8 knots equivalent airspeed" | README v6 | SV |
| Case 11 drive | trim (pitch attitude, elevator, throttle), then **unaugmented, open loop** | AIAA 2015-1810 p. 18, Table 5 | PV |
| Case 12 | trims near Mach 2 with an aero model that has no Mach input and a subsonic data basis — exercises out-of-range table behaviour, **not validated supersonic aerodynamics** | EI from the above | EI |

### 1.5 Reported agreement

- Main sources of disagreement: atmosphere implementation; geodetic vs geocentric gravity direction; geodetic → geocentric initial conditions; **F-16 trim definition (especially trimmed rotational rate)**; integration method (hypothesized) — Vol I §7.1, §8.1 (PV).
- Case 11 after 180 s: two tools within about **4 ft** of each other; a third about **666 ft** away, traced to gravity-direction handling — AIAA 2015-1810 p. 19 (PV).
- Logged aero moments are about the MRC in one tool and about the CM in others, so they are not directly comparable across tools — AIAA 2015-1810 p. 19 (PV).

### 1.6 Reuse

| Item | Terms | Class |
|---|---|---|
| NTRS Vol I/II records | "Work of the US Gov. Public Use Permitted"; Unclassified – Unlimited | PV |
| NESC site, F-16 README | no licence text; the README calls the model unclassified and open-source; no explicit redistribution clause | PV |
| `simupy-flight` | NASA Open Source Agreement 1.3 (obligations for redistributed *code*; bundles the NESC data with no separate statement) | PV |
| AIAA papers | AIAA copyright; cite, do not redistribute | PV |

**Inference (not legal advice):** storing the NESC CSVs and DAVE-ML F-16 files as *test fixtures* with attribution appears consistent with "public use permitted". Copying simupy-flight *code* would bring NOSA obligations and is unnecessary. The F-16 propulsion and inertia trace to a commercial textbook; the README asserts their use without a stated permission, which is a reason to keep them in a validation profile only, as the library already does.

---

## 2. Data lineage: why an aerodynamic comparison is meaningful

```text
NASA TP-1538 (1979) Table III: full low-speed tunnel database, α −20…90°, β ±30° (lead PV, p. 51, App. B p. 43)
        │
        └── Stevens & Lewis App. A: "slightly simplified version" (α −10…45° grid)       ← Morelli 1998 §3, ref. 2 (lead PV)
                 │
                 ├── Morelli 1998 compact global polynomial  ──►  Maverick F-16 aero (MavF16MorelliPolynomial)
                 │
                 └── Garza & Morelli TM-2003-212145 tables  ──►  NESC F16_aero.dml (α −10:5:45 grid)
```

- **Lead-verified:** Morelli states his data were "a slightly simplified version" of the TP-1538 database, citing Stevens & Lewis App. A (Morelli 1998, §3, ref. 2), and that his Table 1 ranges (α −10…45°) "represent all the data available" in that set.
- **Grid evidence:** the NESC tables use the same α −10…45° range in 5° steps (library file verification). TP-1538's own tables run from −20° to 90°.
- **Inference (EI):** Maverick's polynomial and the NESC tables are two representations of the *same* reduced dataset. Their coefficient differences should be bounded by Morelli's fit error (he reports < 10 % in a simulated doublet's time histories). This makes a **coefficient-level comparison meaningful**, whereas the library classed the NESC aero as configuration-incompatible with Maverick (`F16-C8`). The reason is lineage, not configuration identity. Mass, inertia, propulsion, controller and actuators still differ.

---

## 3. Model compatibility with Maverick

| Aspect | NESC F-16 check-case | Maverick F-16 reference | Consequence |
|---|---|---|---|
| Configuration | `F16-CFG-NESC-CHECKCASE` | `F16-CFG-NASA-REF-TP1538` | separate profiles; never merged |
| Aero | S&L-grid **tables** (via Garza & Morelli) | Morelli **polynomial** fitted to the same reduced data | comparable with a fit-error band; table and polynomial outputs will not match exactly |
| Mass / inertia | 637.16 slug; 9 496 / 55 814 / 63 100 / 982 | same values from TP-1538 Table I (lead PV, p. 49) | identical inputs; principal-axis mapping must agree |
| CG | 25 % MAC (cases), MRC 35 % | default 0.25 c̄, reference 0.35 c̄ | compatible |
| Atmosphere | US76 | ISA-style `MavAtmosphereModel` | compare air data at matched altitude; record the model difference |
| Gravity | J2, latitude/altitude dependent (~32.11 ft/s² near case 11) | constant | use the reference `localGravity_ft_s2` as Maverick's constant g in comparison runs |
| Earth | rotating ellipsoid | flat, non-rotating | Coriolis, transport rate and curvature must be removed or bounded (§4.3) |
| Propulsion | steady-state net thrust, Mach 0–1.0, MIL at PLA 50 | TP-1538 Table VI (opt-in) + Garza & Morelli lag | thrust tables differ in the T_max M 0.8 SL cell (doc 02 §4) and in the Mach 0 column; dynamics differ |
| Controller | LQR SAS/autopilot (check-case only) | none for the F-16 check; Maverick laws are different | Level 3 requires porting the check-case controller as a validation-only law |
| Actuators | none (deflections applied directly) | instantaneous (no sourced rates) | compatible |
| Integration | free per tool | PhysX explicit force/torque at 50 Hz + Maverick ω×Iω | numerical differences expected; quantify with dt studies |
| Output | 10 Hz CSV, English units | Maverick telemetry, SI | resample and convert |

---

## 4. Three-level validation design

### Level 1 — Mathematical verification (EOM, transforms, integration)

Goal: prove that Maverick integrates rigid-body motion correctly, independent of any aircraft data.

| Check | Reference | How | Removes |
|---|---|---|---|
| L1-a Torque-free rotation | Case 2 (tumbling brick, initial rates relative to the local frame 9.9958, 20, 30 deg/s, inertial 10, 20, 30) | run the brick's inertia with zero loads in Maverick; compare **inertial** body rates and angular-momentum magnitude | Earth rotation is a frame effect here: compare inertial rates, or remove the 0.004178 deg/s Earth term (EI). Expect drift of Euler angles relative to NED over 30 s from Earth rotation |
| L1-b Inertial coupling with damping | Case 3 | add Clp = Cmq = Cnr = −1 through a test aero model | the damping is non-dimensionalised by V and the case starts at V = 0; how the reference handles this is **UV** (Vol II) — resolve before use |
| L1-c Translational / gravity | Case 4 (non-rotating round earth, inverse-square gravity, CD = 0.1 sphere) | flat Maverick with g(h) = μ/(R+h)² and the same drag | μ and R come from Vol II (**UV**); without them use the CSV's own gravity column |
| L1-d Repository analogue | Maverick already runs a torque-free probe (U5CR-008 case 2) and `MavGyroscopicMomentValidation` | extend to the case 2 brick inertia and initial rates | — |
| L1-e Step convergence | all Level 1 runs | repeat at dt, dt/2, dt/4 (fixed-step, `captureFramerate` pinned); estimate convergence order p = log₂(‖e_dt − e_dt/2‖ / ‖e_dt/2 − e_dt/4‖) | separates integration error from modelling error |
| L1-f Energy | case 2 | rotational kinetic energy and ‖H‖ must be constant torque-free; report their relative drift per second vs dt | detects integrator energy error |

### Level 2 — Aircraft-model comparison (coefficients, trim, air data)

Goal: compare *model outputs* at matched inputs, with no trajectory propagation.

| Check | Reference | How | Expected differences |
|---|---|---|---|
| L2-a Static check-shots | `F16_aero.dml`: 16 aerodynamic static shots; `F16_prop.dml`: 9 propulsion shots (library file-verified) | evaluate Maverick's Morelli polynomial (and, separately, TP-1538 thrust) at each shot's inputs | aero: Morelli fit error vs tables (document the per-coefficient difference); propulsion: TP-1538 vs NESC deck (identical except the cells in doc 02 §4, and NESC's Mach 0 column) |
| L2-b Coefficient sweep | NESC tables (α −10…45, β, δe, δa, δr grid) | sweep Maverick's polynomial over the NESC grid; report per-coefficient max/RMS difference | establishes the fit-error band for every later comparison |
| L2-c Trim | case 11 README trim: θ = 2.6538° (= α), tail −3.241°, throttle 0.1390 (library file-verified); reference tools' t = 0 pitch 2.6433 / 2.6387 / 2.6389° | run `MavF16TrimReference` at the case 11 condition with NESC mass, CG 25 %, US76 density, the reference local g, and **the NESC thrust table in a validation profile** | the tools' own spread (~0.005–0.015°) is the floor; any further difference is model or trim-definition difference |
| L2-d Air data | case 11 CSV columns: density, pressure, temperature, speed of sound, Mach, q̄ | evaluate `MavAtmosphereModel` at the CSV altitudes | US76 vs ISA-style implementation |

### Level 3 — Trajectory comparison

Goal: compare propagated responses under matched initial conditions, model and inputs.

| Check | Reference | Requirements | Comparison variable |
|---|---|---|---|
| L3-a Open-loop trim flyout | case 11, 180 s | NESC aero tables **ported as a validation-only aero model** (or accept the fit-error band), NESC thrust, local g, US76 | **relative** quantities only (altitude change, speed change, attitude, body rates corrected for Earth/transport rate); lateral position cannot be compared without Coriolis (≈ 790 ft at 180 s, EI) |
| L3-b Closed-loop steps | cases 13.1–13.4 (20–60 s) | the check-case LQR SAS/autopilot ported as a validation-only law (gains file-verified by the library) | Δh, ΔKEAS, Δψ, lateral offset time histories; Coriolis effect ≈ 10–87 ft over these durations (EI), small against the commanded changes |
| L3-c Not applicable | 15, 16 (pole, date line); 12 (out-of-domain aero); 14 (unpublished) | — | — |

### Separating error sources

| Symptom | Likely source | Discriminating test |
|---|---|---|
| Difference shrinks as dt → 0 at the integrator's order | numerical integration | L1-e, repeated on the L3 case |
| Difference present at t = 0 / in static shots | model data (fit vs table, thrust table, atmosphere) | L2-a/b/d |
| Difference in trim only | trim definition (rotational rate on a rotating earth) or g | L2-c with matched g; compare with the tools' spread |
| Difference only in closed-loop cases | controller port or one-step latency | compare open-loop case 11 first; then run the controller off-line on the reference states and compare commands |
| Lateral drift growing ∝ t² | Coriolis (flat vs rotating earth) | compute the expected 2Ω×V deflection and subtract or widen the band |
| Moment comparison inconsistent | reference point (MRC vs CM) | transfer to a common point first (AIAA 2015-1810 p. 19) |

### Metrics (no thresholds invented)

The NESC programme itself sets no tolerances. Maverick should report:
- per-variable max and RMS difference against **each** reference tool, and against the envelope (min–max) of the three tools;
- whether Maverick lies inside the tool envelope at each sample;
- convergence order from dt studies;
- for static shots, absolute and relative coefficient difference.

The tools' own spread is the only defensible "floor". Anything beyond it is reported, explained and classified, never silently tuned away.

---

## 5. JSBSim as an optional offline cross-check

| Use | Value | Limit |
|---|---|---|
| Run **Maverick's own** F-16 data (Morelli polynomial as functions, TP-1538 thrust, TP-1538 mass) in JSBSim offline (Python), compare with Maverick at identical inputs | catches integration, axis-conversion, state-propagation and force/moment bookkeeping errors, because the data are identical and only the implementations differ | **numerical cross-validation, not validation of real-aircraft fidelity**: two codes running the same data can agree and both be unrealistic |
| JSBSim was one of the seven NESC tools | gives confidence in JSBSim's EOM for the check cases | which SIM number it was is unpublished |
| JSBSim defaults | read the integrator properties at run time; documentation and code disagree (Phase 1 doc 04) | — |
| Policy | JSBSim's own F-16 (GPL-tagged, TP-1538 lineage) must not be used (repository policy); no runtime integration | — |

Recommendation: the NESC route (§4) comes first because it needs no third-party code and has multi-tool reference data. JSBSim is a P3 option for diagnosing a discrepancy the NESC route reveals.

---

## 6. Recommended order of work (handoff)

| Step | Work | Location | Blocking? |
|---|---|---|---|
| 1 | Acquire `F16_package.zip` and `Atmospheric_checkcases.zip` from the NESC site; store with SHA-256 in a test-fixture folder; record provenance | `Docs/Reference/Data/NESC/` (proposal) | needs a download decision |
| 2 | L1-a/L1-e: case 2 brick in Maverick | `FlightDynamics/Validation/` | no |
| 3 | L2-a/L2-b: static shots and coefficient sweep (Morelli vs NESC tables) | Validation, offline-eligible | no |
| 4 | L2-c: case 11 trim in a validation profile | Validation | needs the NESC thrust table as a validation-only deck |
| 5 | L3-b: port the check-case LQR as a validation-only law; run 13.1–13.4 | Validation | larger effort |
| 6 | Read Vol II (constants, App. B F-16 description, App. E match metrics) | research | download > 10 MB |
