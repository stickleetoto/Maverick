# F-16 Source Coverage Matrix v0.1

**Phase 5B.5 — F-16 Reference Closure**
Compiled 2026-09-11, **revised 2026-09-12** after review. Branch `sol/phase5-wip`.

Revision 2 corrects the Ixz entry (it is representable, it was implemented, and it is now validated by
reconstruction), records the Phase 5C-R reference-envelope gate, and adds the TP-1538 Table VI
transcription workflow.

---

## 0. What aircraft is this, exactly

The replacement flight-dynamics branch does **not** target an operational F-16C of any Block.
It targets what this document calls the **NASA REFERENCE F-16**, which is the intersection of two
NASA publications:

| Element | Source | Aircraft / configuration actually described |
|---|---|---|
| Aerodynamics | Morelli, *Global Nonlinear Parametric Modeling with Application to F-16 Aerodynamics*, NASA NTRS 20040110310 | Polynomial fit to a **subsonic wind-tunnel database for a 16% scale model**, "out of ground effect, with landing gear retracted and no external stores", Mach < 0.6 |
| Mass, inertia, geometry, engine, control system | Nguyen et al., *Simulator Study of Stall/Post-Stall Characteristics of a Fighter Airplane With Relaxed Longitudinal Static Stability*, NASA TP-1538, December 1979 | A **1979 development-era F-16 with relaxed longitudinal static stability**, as flown in the Langley differential maneuvering simulator |
| Simulation packaging | Garza & Morelli, *A Collection of Nonlinear Aircraft Simulations in MATLAB*, NASA TM-2003-212145 | Reproduces the TP-1538 mass set and engine structure |

Neither source describes a production F-16C Block 25/30/40/50 flight control system, a production
engine installation, or an operational store loadout. **Any claim of "real F-16C fidelity" is
unsupported by the sources this project uses.**

## Provenance classes used below

| Class | Meaning |
|---|---|
| `AUTHORITATIVE` | Taken from a Tier 1 primary source and verified against it during this phase |
| `PUBLIC_REFERENCE` | Exists in a permitted public Tier 1 source, not yet transcribed/verified into the repo |
| `CROSS_VALIDATION_ONLY` | Tier 2 material; may compare but never override Tier 1 |
| `APPROXIMATE` | Physically reasonable but not sourced to a primary document |
| `MAVERICK_TUNING` | Chosen for this game; no fidelity claim |
| `UNAVAILABLE` | No permitted public source found |

---

## 1. Geometry

| Parameter | Maverick value | Source | Identifier | Config | Provenance | Status | Confidence | Mismatch / TODO |
|---|---|---|---|---|---|---|---|---|
| Wing area S | `27.870912 m²` (300 ft²) | TP-1538 | Table I, "Area, m² (ft²) … 27.87 (300)" | Clean | `AUTHORITATIVE` | Implemented (`MavF16MorelliReference.WingAreaM2`) | High | — |
| Wing span b | `9.144 m` (30 ft) | TP-1538 | Table I, "Span, m (ft) … 9.144 (30)" | Clean | `AUTHORITATIVE` | Implemented | High | — |
| Mean aerodynamic chord c̄ | `3.450336 m` (11.32 ft) | TP-1538 | Table I, "Mean aerodynamic chord … 3.45 (11.32)" | Clean | `AUTHORITATIVE` | Implemented | High | Repo stores the exact ft conversion (3.450336); TP-1538 prints 3.45 |
| Reference CG station | `0.35 c̄` | TP-1538 + Morelli | Table I "Reference center-of-gravity location … 0.35c̄"; Morelli nomenclature `xcgref = 0.35` | Clean | `AUTHORITATIVE` | Implemented (`XcgReferenceCbar`) | High | — |
| Default CG station | `0.25 c̄` | Morelli | §3, demonstration maneuver "with xcg = 0.25" | Clean | `AUTHORITATIVE` for that condition | Implemented (`DefaultXcgCbar`) | High | It is a *demonstration* CG, not a required operating CG |
| CG convention | Fraction of c̄, longitudinal only | TM-2003-212145 | "given in fraction of mean aerodynamic chord" | — | `AUTHORITATIVE` | Implemented | High | Lateral/vertical CG offsets not modelled |
| Unity centre-of-mass mapping | Set via `MavMassProperties` | — | — | — | `APPROXIMATE` | Partially implemented | Low | **TODO**: no verification that the Unity model origin coincides with 0.25 c̄ |

## 2. Mass and inertia

| Parameter | Maverick value | Source | Identifier | Provenance | Status | Confidence | Mismatch / TODO |
|---|---|---|---|---|---|---|---|
| Weight / mass | `637.16 slug` → `9 298.55 kg` | TP-1538 | Table I, "Weight, N (lb) … 91 188 (20 500)" | `AUTHORITATIVE` | Implemented (`MavF16MassReference`) | High | **MISMATCH — see D2**: the legacy catalog flies `9 800 kg` |
| Ix | `9 496 slug-ft²` → `12 875 kg·m²` | TP-1538 | Table I | `AUTHORITATIVE` | Implemented | High | — |
| Iy | `55 814 slug-ft²` → `75 674 kg·m²` | TP-1538 | Table I | `AUTHORITATIVE` | Implemented | High | — |
| Iz | `63 100 slug-ft²` → `85 552 kg·m²` | TP-1538 | Table I | `AUTHORITATIVE` | Implemented | High | — |
| Ixz | `982 slug-ft²` → `1 331 kg·m²` | TP-1538 | Table I | `AUTHORITATIVE` | **Implemented and validated** | High | Unity carries principal moments PLUS `inertiaTensorRotation`, so a non-diagonal inertia matrix is represented exactly. Diagonalized to principal (75 673.6, 85 576.5, 12 850.5) kg·m² with a 1.0492° rotation about Unity X; reconstruction error **0.0020 kg·m²** (2.3 × 10⁻⁶ %). Sign convention derived from TP-1538 Appendix B, not guessed |
| Body-axis convention | X fwd, Y right, Z down | TP-1538 nomenclature | — | `AUTHORITATIVE` | Implemented (`MavFlightDynamicsMath`) | High | Verified this phase — AXIS-001..004 |
| Configuration dependence | None modelled | — | — | `UNAVAILABLE` | Not implemented | — | Fuel burn, stores, gear all fixed-mass |

## 3. Morelli aerodynamics

| Item | Maverick value | Source | Identifier | Provenance | Status | Confidence | Mismatch / TODO |
|---|---|---|---|---|---|---|---|
| α valid domain | `−10° … +45°` | Morelli | Table 1, "−0.1745 rad (−10 deg) … 0.7854 rad (45 deg)" | `AUTHORITATIVE` | Implemented (`ClampAlphaRad`) | High | — |
| β valid domain | `−30° … +30°` | Morelli | Table 1 | `AUTHORITATIVE` | Implemented | High | — |
| δe range | `±25°` | Morelli Table 1; TP-1538 Table I "Symmetric (δh), deg ±25" | `AUTHORITATIVE` | Implemented | High | — |
| δa range | `±21.5°` | Morelli Table 1; TP-1538 "Ailerons (flaperons), deg ±21.5" | `AUTHORITATIVE` | Implemented | High | — |
| δr range | `±30°` | Morelli Table 1; TP-1538 "Rudder, deg ±30" | `AUTHORITATIVE` | Implemented | High | — |
| **Mach applicability** | `< 0.6` | Morelli | §3: "a 16% scale model of the F-16 aircraft flying at relatively low Mach numbers (< 0.6)" | `AUTHORITATIVE` | Enforced (`MaxReferenceMach = 0.6`) | High | **BLOCKER — see H**: gameplay reaches M 0.7+ |
| Compressibility / transonic terms | None | Morelli | Eqs. 12–17 contain no Mach term | `AUTHORITATIVE` (absence is stated) | n/a | High | The model is Mach-independent *within* its validated band |
| Rate normalisation | `p̂ = pb/2V`, `q̂ = qc̄/2V`, `r̂ = rb/2V` | Morelli | Eq. 18 | `AUTHORITATIVE` | Implemented, verified | High | — |
| CG correction, Cm | `Cm += Cz·(xcgref − xcg)` — no c̄/b | Morelli | Eq. 16 | `AUTHORITATIVE` | Implemented, **verified by test AERO-CG-002** | High | — |
| CG correction, Cn | `Cn −= Cy·(xcgref − xcg)·(c̄/b)` — with c̄/b | Morelli | Eq. 17 | `AUTHORITATIVE` | Implemented, **verified by test AERO-CG-003** | High | The two corrections differ deliberately |
| Coefficient build-up | `Cx,Cy,Cz,Cl,Cm,Cn` per Eqs. 12–17 | Morelli | Eqs. 12–17, Tables 2–3 | `AUTHORITATIVE` | Implemented | High | — |
| α̇ dependence | Folded into the q terms | Morelli | §3: "Dependence … on α̇ is included in the q dependencies, due to the manner in which the data is collected" | `AUTHORITATIVE` (stated limitation) | Inherited | High | No separate α̇ term is possible with this data |
| High-α limitation | Data stops at 45° | Morelli | Table 1 | `AUTHORITATIVE` | Clamped | High | Post-45° behaviour is outside the model |
| Model accuracy | "< 10% difference" vs wind tunnel | Morelli | §3, Fig. 1 (lat/dir doublet, α 10°, M 0.26, xcg 0.25) | `AUTHORITATIVE` | — | High | Validated at ONE condition, low Mach |

## 4. Atmosphere

| Item | Maverick value | Source | Provenance | Status | Confidence | Mismatch / TODO |
|---|---|---|---|---|---|---|
| Density | `1.225·exp(−h/8500)`, floor `0.18` | — | `MAVERICK_TUNING` | Implemented (`MavAeroBody.ComputeAirDensity`) | Low | Not ISA. Diverges from a standard atmosphere with altitude |
| Speed of sound | ISA `sqrt(γRT)`: 340.29 m/s at sea level, 295.07 at the tropopause | ISA | `AUTHORITATIVE` | Implemented (`MavAtmosphereModel`) | High | The legacy constant 343 remains only for the HUD, named `LegacyConstantSpeedOfSoundMps` |
| Static pressure | not modelled | — | `UNAVAILABLE` | Not implemented | — | Needed for a real thrust deck lookup |
| Temperature | not modelled | — | `UNAVAILABLE` | Not implemented | — | — |
| Altitude convention | Unity world `y`, metres, MSL-equivalent | — | `APPROXIMATE` | Implemented | Medium | No geoid/terrain offset concept |
| Mach calculation | **reference:** `MavAtmosphereModel.ReferenceMach` (ISA speed of sound at altitude); **legacy HUD:** `V / 343`, retained and renamed | ISA | reference `AUTHORITATIVE`, legacy `APPROXIMATE` | **Both implemented and separated** | High | The reference-envelope gate carries a `MavMachSource` and **refuses** anything but `ReferenceAtmosphere`. The legacy estimate is wrong by up to **14%** at 15 km |
| `MavAtmosphereModel` (replacement) | separate implementation | — | see file | needs audit | Present | — | **TODO**: two atmosphere implementations coexist (legacy + replacement) |

## 5. Six-DoF equations

| Item | Maverick value | Source | Provenance | Status | Confidence | Mismatch / TODO |
|---|---|---|---|---|---|---|
| Translational equations | Unity `Rigidbody` integration of summed forces | — | `APPROXIMATE` (engine-provided) | Implemented (`MavSixDoFBody` sums once, applies once) | High | Unity integrates; the project supplies loads |
| Rotational equations | Unity `Rigidbody` integration of summed torques | — | `APPROXIMATE` | Implemented | Medium | Unity uses a diagonal inertia tensor plus a rotation |
| Product of inertia Ixz | Applied via principal axes + `inertiaTensorRotation`, validated by reconstruction | TP-1538 Table I | `AUTHORITATIVE` | **Implemented** | High | Representation is exact. What remains untested is whether Unity's own rotational integrator reproduces TP-1538 Appendix B's explicit `Ixz(ṙ+pq)` / `Ixz(r²−p²)` coupling at large simultaneous p, q, r — a question about the integrator, not the representation. **BLOCKER FOR 5D** |
| Gravity transformation | World `Physics.gravity`, applied per the gravity ownership contract | — | `AUTHORITATIVE` convention | Implemented and tested this phase (GRAVITY-001..005) | High | — |
| Body/world transforms | `MavFlightDynamicsMath`, true vs axial separated | TP-1538 body-axis convention | `AUTHORITATIVE` | Implemented, verified (AXIS-001..004) | High | Exactly one conversion boundary in the replacement path |
| Engine gyroscopic moment | not implemented | TP-1538: `h_eng = 216.9 kg·m²/s (160 slug-ft²/s)` along body X | value `AUTHORITATIVE`, application `UNAVAILABLE` | Not implemented | — | `IMPORTANT LATER` |

## 6. Propulsion

**This is the section that changed most in Phase 5B.5.**

| Item | Maverick value | Source | Identifier | Provenance | Status | Confidence | Mismatch / TODO |
|---|---|---|---|---|---|---|---|
| Throttle gearing | not implemented | TM-2003-212145 | `tgear.m`: throttle [0,1] → commanded power level [0,100], "not always in a linear fashion" | structure `AUTHORITATIVE`, breakpoints `PUBLIC_REFERENCE` | Not implemented | Medium | TP-1538 fig. 66(b) shows the gearing curve |
| Power-level dynamics | present (`MavF16EnginePowerModel`) | TM-2003-212145 | `rtau`: τ = 1.0 if (Pc−P) < 25; 0.1 if > 50; 1.9 − 0.036(Pc−P) between | `AUTHORITATIVE` | Implemented | High | Verify the repo matches these three branches |
| Thrust interpolation | structure known | TP-1538 + TM-2003-212145 | `T = Tidle + (Tmil−Tidle)(P/50)` for P<50; `T = Tmil + (Tmax−Tmil)((P−50)/50)` for P≥50 | `AUTHORITATIVE` | Not implemented | High | — |
| **Thrust tables T(h,M)** | **`thrust = 0 N`, `HasAuthoritativeData = false`** | **TP-1538 Table VI** | **"TABLE VI.— THRUST VALUES USED IN SIMULATION", idle / military / maximum, tabulated vs Mach 0–1.0 and altitude 0–15 240 m, SI and US units, pages ~92–93** | **`PUBLIC_REFERENCE`** | **Workflow and validator implemented; values NOT transcribed** | **High that it exists; nothing transcribed** | `Docs/F16_TP1538_TABLE_VI_TRANSCRIPTION.md` holds the procedure and an empty template; `MavF16ThrustTranscription` parses and validates it (SI/US cross-check, idle<mil<max ordering, altitude trend, provenance hash). The template validates as **NOT TRANSCRIBED** so it can never be mistaken for data. **BLOCKER FOR 5D, not for 5C-R** |
| Engine angular momentum | not implemented | TP-1538 | "engine angular momentum at a fixed value of 216.9 kg-m²/sec (160 slug-ft²/sec)" | `AUTHORITATIVE` | Not implemented | High | `IMPORTANT LATER` |
| NASA software package | **not used** | — | — | — | Deliberately not used | — | The `.m` files carry the same tables but are a U.S.-release-gated package; TP-1538 Table VI makes them unnecessary |

## 7. Flight-control architecture

| Item | Public support | Source | Provenance | Notes |
|---|---|---|---|---|
| Configuration described | 1979 relaxed-static-stability development F-16 | TP-1538 title + Appendix A | `AUTHORITATIVE` for *that* aircraft | **Not** a production F-16C FLCS |
| g-command / CAS structure | Yes | TP-1538 §"control augmentation system (CAS) whereby the pilot commanded normal acceleration" | `AUTHORITATIVE` for TP-1538 config | Pilot commands Nz |
| Pitch-rate feedback | Yes | TP-1538: "rate and filtered normal acceleration were fed back" | `AUTHORITATIVE` for TP-1538 config | — |
| AoA feedback into Nz command | Yes | TP-1538: "The angle-of-attack feedback reduced the commanded normal acceleration" | `AUTHORITATIVE` for TP-1538 config | Structurally similar to the current instructor AoA limiter |
| AoA limiter | Yes, described qualitatively | TP-1538 §"effectiveness of the angle-of-attack limiter system" | `AUTHORITATIVE` qualitative, gains `UNAVAILABLE` | — |
| Roll-rate command | Partial | TP-1538: commanded roll rate "limited to only about 165°/sec" | `PUBLIC_REFERENCE` | A single data point, not a law |
| Yaw / coordination | not characterised in what was reviewed | — | `UNAVAILABLE` | — |
| Gain scheduling | LEF scheduled with α and q/Ps | TP-1538: "Leading-edge flap deflection was scheduled with angle of attack and q/Ps" | `AUTHORITATIVE` structure, schedule `PUBLIC_REFERENCE` (figure) | — |
| Specific FLCS gains | **none** | — | `UNAVAILABLE` | Any gain in `MavF16ControlLawV01` is `MAVERICK_TUNING` |
| AFTI / VISTA / MATV laws | **not used, not mixed in** | — | — | Deliberately excluded — different aircraft |

**Conclusion for §7:** the repo may honestly describe its control law as *"a Nz-command CAS in the
structural style documented for the TP-1538 relaxed-static-stability F-16, with Maverick-chosen
gains."* It may **not** be called the F-16C FLCS.

## 8. Actuator system

| Item | Maverick value | Source | Identifier | Provenance | Status | Mismatch / TODO |
|---|---|---|---|---|---|---|
| Symmetric horizontal tail limit | `±25°` | TP-1538 | Table I | `AUTHORITATIVE` | Implemented | — |
| Differential tail | not modelled | TP-1538 | Table I, "Differential (δd), per surface, deg ±5.375" | `AUTHORITATIVE` value, not implemented | Gap | `IMPORTANT LATER` |
| Flaperon / aileron limit | `±21.5°` | TP-1538 | Table I | `AUTHORITATIVE` | Implemented | — |
| Rudder limit | `±30°` | TP-1538 | Table I | `AUTHORITATIVE` | Implemented | — |
| Leading-edge flap | not modelled, **and not required** | TP-1538 + Morelli | See §10 below | `AUTHORITATIVE` conclusion | **Resolved — OUTSIDE V0.1 REFERENCE SCOPE** | The Morelli base functions ARE the LEF-deployed state. The compact model needs no LEF input and Maverick supplying none is self-consistent |
| Speed brake | **not modelled** | TP-1538 | Table I, "Speed brake, deg 60" | `AUTHORITATIVE` limit | Gap | `OUTSIDE V0.1 REFERENCE SCOPE` |
| Surface rate limits | present in `MavF16ControlActuator` | — | — | `MAVERICK_TUNING` | Implemented | **No sourced rate limit was found** in the material reviewed |
| Actuator lag | first-order in the actuator | — | — | `MAVERICK_TUNING` | Implemented | TP-1538 mentions a stabilator actuator model (fig. 63) not yet transcribed |
| Variant specificity | — | — | — | — | — | All limits above are the TP-1538 aircraft's, not a Block-specific F-16C's |

---

## 10. Leading-edge flap: resolved from the primary sources

**Question.** Does the Morelli v0.1 reference model assume a fixed LEF state, implicitly represent a
scheduled one, or require LEF dynamics Maverick does not supply?

**Answer: it assumes a FIXED, FULLY DEPLOYED (25°) state, and requires no LEF input at all.**

The evidence is in how TP-1538 builds its coefficients. Its nomenclature defines the `lef` subscript
verbatim as:

> `lef` — increment of variable produced by **full retraction of leading-edge flaps**; for example,
> ΔCm,lef indicates increment in Cm produced by **retraction of leading-edge flaps from 25° to 0°**

and the build-up (TP-1538 Appendix B) is

```
CX,t = CX(α,β,δh) + ΔCX,lef · (1 − δlef/25) + (c̄/2V)[CXq(α) + ΔCXq,lef · (1 − δlef/25)] + …
ΔCX,lef = CX,lef(α,β) − CX(α,β, δh = 0°)
```

At **δlef = 25°** the multiplier `(1 − 25/25)` is **zero**, the increment vanishes, and the total
reduces to the **base function** `CX(α,β,δh)`. At δlef = 0° the multiplier is 1 and the full
retraction increment applies.

Morelli's compact model (NTRS 20040110310 Eqs. 12–17) retains **only base-function-shaped terms**:

```
Cx = Cx(α,δe) + Cxq(α)·q̂                    Cy = Cy(β,δa,δr) + Cyp(α)·p̂ + Cyr(α)·r̂
Cz = Cz(α,β,δe) + Czq(α)·q̂                  Cl = Cl(α,β) + Clp·p̂ + Clr·r̂ + Clδa·δa + Clδr·δr
Cm = Cm(α,δe) + Cmq(α)·q̂ + Cz(xcgref−xcg)   Cn = Cn(α,β) + Cnp·p̂ + Cnr·r̂ + … − Cy(xcgref−xcg)(c̄/b)
```

There is **no δlef variable**, no LEF increment, and no δlef entry in Table 1's independent-variable
ranges. Morelli cites TP-1538 as Reference [3] — the same database — and describes his input as "a
slightly simplified version of the original wind tunnel database". Dropping the LEF increment terms is
part of that simplification (so is dropping β from Cx, which TP-1538's `CX(α,β,δh)` retains).

**Classification: OUTSIDE V0.1 REFERENCE SCOPE.**

Not a blocker. The model is *complete as it stands* — it describes an F-16 whose leading-edge flaps are
at 25°, and it needs nothing from Maverick to do so. Supplying no LEF model is therefore consistent
with the reference rather than a gap in it.

**The documented deviation**, recorded so nobody later mistakes this for full fidelity: the real
aircraft schedules the LEF on α and q/Ps and retracts it at low α and high speed. The v0.1 reference
aircraft does not, so at low α it carries deployed-LEF aerodynamics that the real aircraft would not.
Quantifying that difference would need the retraction increments from TP-1538, which are published as
figures rather than tables, and is not v0.1 work.

**The v0.1 reference aircraft is therefore:** F-16, clean, gear retracted, no external stores, out of
ground effect, **leading-edge flaps deployed 25°**, Mach < 0.6.

## 11. Mass and CG policy

Two masses, both legitimate, and they must never be swapped in flight.

| Policy | Mass | Source | When |
|---|---|---|---|
| `LegacyGameplay` | **9 800 kg** | `MavAircraftCatalog` F-16C profile | The aircraft flying today. Every legacy handling number was tuned against it |
| `Reference` | **9 298.65 kg** | TP-1538 Table I, 20 500 lb | The only mass for which the sourced inertia tensor and the reference aerodynamics are mutually consistent |

They differ by **501.35 kg (5.4%)**.

**Policy, frozen in Phase 5B.7.** The first 5C-R experiments **spawn directly into the ReferenceF16
configuration**. No airborne mass transition happens at all.

Three conditions fail, each enforced in code rather than documented:

| Failure | Enforced by |
|---|---|
| Reference mode requested while the Rigidbody carries the legacy mass | `MavRuntimeHandoverTarget.ValidateReadiness` — refuses at step 2 with the two masses named |
| Mass modified after physics begins | `MavAtomicHandover` — captured vs live mass compared before step 2 and again at commit; an airborne change is refused and rolled back |
| Mass provenance unspecified | `MavAtomicHandover` — `MassPolicy.Unspecified` is refused **before any step runs** |

`MavF16ReferencePhysicsRoot` applies the reference mass and inertia at **Awake**, i.e. before physics
begins, and refuses a `LegacyGameplay` policy outright. So a 5C-R rig is in the reference configuration
from its first physics step or it does not start.

**CG / physics datum — resolved in Phase 5B.7 by option B.**

The aerodynamic stations `xcgRef = 0.35 c̄` and `xcg = 0.25 c̄` are **aerodynamic reference quantities**.
They are the datum the coefficient equations resolve moments about, and they are **not** a Unity
`centerOfMass` vector. Converting one to the other needs to know where a particular art asset's origin
sits relative to the airframe, which no code can discover — inferring it from an imported mesh pivot
would be a guess dressed as a measurement.

So `MavF16ReferencePhysicsRoot` takes the other route. Its own origin is **defined** to be the reference
CG:

```
AircraftPhysicsRoot     <- Rigidbody, reference mass/inertia, MavFlightPhysicsOwnership
                           origin == reference CG, so centerOfMass == Vector3.zero
    VisualModel         <- child; any visual offset lives here
```

`MavPhysicsDatumKind` makes the declaration explicit and checkable:

| Kind | Meaning |
|---|---|
| `Undeclared` | **refused** |
| `OriginIsReferenceCg` | option B. `centerOfMass` is exactly zero by construction, no measurement needed |
| `MeasuredOffsetToCg` | option A. Requires a real measured offset *and* a provenance string |

A `datumProvenance` string is mandatory; a datum nobody can check is not a declaration. The gameplay
prefab is untouched — that is the point of a separate root, and it means a bad 5C-R result cannot leave
the shipping aircraft altered.

`MavF16FlightDynamicsProfile.cgMappingMeasuredAndDeclared` remains false and the envelope gate remains
fail-closed for the **gameplay** aircraft. Option B resolves the datum for the isolated test rig only.

## 9. Phase 5C-R reference-envelope gate

Added in revision 2. `MavF16ReferenceEnvelope` answers one question: *may the unpowered replacement
stack own physics right now?* It is separate from `MavReplacementReadiness`, which asks whether the
stack is **built**; this asks whether the aircraft is currently **inside the flight condition the
reference data was measured in**. Readiness is established once; the envelope can be left and re-entered
several times a minute by flying faster.

| Condition | Bound | Source |
|---|---|---|
| Mach | `< 0.6` strictly | Morelli §3 — "relatively low Mach numbers (< 0.6)". The bound is exclusive because the source says `<`, not `≤` |
| α | `−10° … +45°` | Morelli Table 1 |
| β | `−30° … +30°` | Morelli Table 1 |
| identity | resolved as F16C | `0851f4f` |
| configuration | clean, gear up, no external stores | Morelli §3 — the data's stated configuration |
| propulsion | **intentionally disabled** | 5C-R is an unpowered cutover; zero thrust is required, not tolerated |
| gravity owner | exactly one provider | Phase 5B.5 gravity contract |
| mass / inertia | validated | TP-1538 Table I, reconstruction-checked |
| CG mapping | measured and declared | `MavF16FlightDynamicsProfile.cgMappingMeasuredAndDeclared`, defaults **false** |
| replacement state | all finite | — |
| legacy writers | disabled atomically | — |

Outside any of these the gate **fails closed** and names the specific bound. `Mach ≥ 0.6` reports
`OutOfReferenceRange`. There is no branch that extrapolates the aerodynamic model past its published
domain, and adding one would defeat the purpose of having measured a domain.

## Sources reviewed

1. Morelli, E.A., *Global Nonlinear Parametric Modeling with Application to F-16 Aerodynamics*,
   NASA NTRS **20040110310** — full text extracted and read. Table 1, Eqs. 12–18, §3.
2. Nguyen, L.T., Ogburn, M.E., Gilbert, W.P., Kibler, K.S., Brown, P.W., Deal, P.L.,
   *Simulator Study of Stall/Post-Stall Characteristics of a Fighter Airplane With Relaxed
   Longitudinal Static Stability*, NASA **TP-1538**, Dec 1979 — full text extracted (233 pp).
   Table I, Table VI, nomenclature, Engine Simulation section, Appendix A/B references.
3. Garza, F.R. & Morelli, E.A., *A Collection of Nonlinear Aircraft Simulations in MATLAB*,
   NASA **TM-2003-212145** — full text extracted. Table 1, engine model section.

**Not used:** AeroBench, JSBSim, community simulators, and the NASA MATLAB simulation package.
No Tier 2 material contributed a value to this matrix.
