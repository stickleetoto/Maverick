# F-15 Flight Physics R2

Branch `claude/f15-physics-r2`, from `main` @ `f0f9519`.

**Companion documents:**
- `F15_PHYSICS_R2_GAP_AUDIT.md` — every item classified, written before any code;
- `F15_PHYSICS_R2_SOURCE_LEDGER.md` — every constant and every rejected lead.

> **THIS IS NOT THE F-15 FCS.** The aircraft is the pilot-controlled AFIT / Baumann / Davison research model (`F15_AFIT_BAUMANN_DAVISON_PILOT_CONTROLLED_V1`) under a Maverick control law (MAVERICK_TUNED_NON_AUTHORITATIVE). The frozen research reference is unchanged.

---

## 1. Previous physical model (R1 — the V1/V2 record)

| Part | R1 |
|---|---|
| Aerodynamics | Baumann Mach 0.6 / 20,000 ft fit (Davison App. C `COEFF`), six axes, SourceReproduction gate 218.5–699.7 ft/s, α −4…90°, \|β\| ≤ 20°; refused outside |
| Atmosphere | source fixed density RHO 0.0012673 slug/ft³ at every state; source g 32.174 ft/s² through the load set |
| Propulsion | fixed 8,300 lbf total, 0.25-in thrust-line moment, throttle inactive |
| Mass / inertia | 37,000 lb; Ix 25,480, Iy 166,620, Iz 186,930, Ixz −1,000 slug·ft² (Davison) |
| Surfaces | Maverick command envelopes (stab −25…−5°, aileron ±20°, differential ±6°, rudder ±15°); **each surface reaches its bounded command in the same step (instantaneous)** |
| Trim start | Table VII point 36 (300.8 ft/s, α 17.4865°, θ 14.5460°, stab −15.1453°) |
| Control | V2 (active), V1 (regression / development) |

## 2. New physical model (R2)

**R2 = R1 + the research model's own first-order surface actuator lags.** Nothing else differs.

| Channel | Lag λ | τ | Command | Source |
|---|---|---|---|---|
| Symmetric stabilator | 20 s⁻¹ | 0.050 s | its bounded command | Davison STATE12 `F(9)=20.*(CDSTBD-DSTBD)` |
| Aileron | 20 s⁻¹ | 0.050 s | its bounded command | `F(11)=20.*(CDAILD-DAILD)` |
| Differential tail | 20 s⁻¹ | 0.050 s | 0.3 × commanded aileron | `F(12)=20.*(.3*CDAILD-DDTD)` — "acting through the stabilator actuators" |
| Rudder | 28 s⁻¹ | 0.036 s | its bounded command | `F(10)=28.*(CDRUDD-DRUDD)` |

**Source:** Davison 1992, AFIT/GAE/ENY/92M-01, DTIC ADA256613, App. B `STATE12` FUNX, **PDF p.97 (printed 87)**. The listing states "CAS off and Aileron-Rudder Interconnect off. Deflections are in degrees."

### Answers to the five questions (brief §21)

**What deficiency existed?**
- The pilot aircraft moved its surfaces by teleport: a full-scale stick input produced full-scale surface deflection, and so full-scale control moment, within one 20-ms step.
- The research model it flies has its own actuator representation, which R1 did not use.

**What evidence supports the new behaviour?**
- The same thesis lineage as the coefficients and the mass/inertia already flown.
- Four printed equations, confirmed on the rendered listing.
- It is version-matched to the model. It is **not** production F-15 actuator data: the thesis cites no hardware source for 20/28/20.

**What code owns it now?**
- `MavF15ControlActuator`, the single surface-state owner, through `dynamics` (`MavF15ActuatorDynamics`).
- The profile's `physicsRevision` selects it, and the rig wires it (`MavF15PilotPhysics.ActuatorDynamicsFor`).
- The data is `MavF15ResearchModelActuatorLags`.

**What did it replace?**
- For R2 only: the instantaneous step.
- There was no other surface-dynamics term, fake or physical, so nothing else migrates.
- A lag is **bandwidth, not a rate limit**. The existing rate-limit field stays separate and empty (no rate is sourced).

**How was it validated?** See §8.
- Headless: exact source constants; the R1 path bit-identical; exact lag solution; differential tail = 0.3 × aileron through the lag; fail-safes; unchanged trim residual; rig response.
- Play Mode: the lag measured in flight equals the source's on every channel; R1 vs R2 under V2; the pilot sequence; dt refinement.

### Numerical form

`x(t+dt) = u + (x − u)·e^(−λ·dt)`, with the command u held over the step.

This is the **exact** solution for a zero-order-hold input:
- it has no timestep stability limit;
- it adds no integration error;
- it never overshoots.

Order of operations: travel clamp first (unchanged), then the lag, then any sourced rate limit (none).

**Fail-safe behaviour:**
- a non-finite current position snaps to the bounded command;
- a zero, negative, NaN or infinite dt holds the position;
- a non-finite request is already replaced by neutral upstream (unchanged).

## 3. Frozen vs pilot-controlled separation

| | Frozen research reference | Pilot-controlled R1 | Pilot-controlled R2 |
|---|---|---|---|
| Surfaces | static source-defined hold, no travel/rate/lag | instantaneous, Maverick envelopes | Davison lags, Maverick envelopes |
| Everything else | frozen | = frozen physics | = R1 |

- No frozen or hash-locked file was modified; `[A9]` scans them for any reference to the R2 layer.
- The four-argument `MavF15ControlActuator.StepChannels`, which the frozen control-path suite uses, delegates with no dynamics and is bit-identical (`[A2]`, 500 cases).

**Default:** the profile's `physicsRevision` defaults to **R1**. Every existing prefab and rig therefore flies exactly the recorded plant, and every V1/V2 record reproduces byte for byte (§8).

**To fly R2:**
- set `Physics Revision = R2SourceActuatorLags` on the F-15 profile component (for example on a placed `F15_PilotControlledResearch_V2` instance); or
- build a rig with `MavF15PilotControlledRig.Create(..., MavF15PilotPhysicsRevision.R2SourceActuatorLags)`.

Making R2 the default is a one-line change. It would re-baseline the recorded V1/V2 pilot numerics, which is why it is left as a decision.

## 4. Atmosphere — unchanged (deliberately)

**SOURCE CONDITION = RUNTIME ATMOSPHERE:** the source's fixed 20,000-ft density and its g.

A standard atmosphere was evaluated and **not implemented** (gap audit §2.1). The source's thrust is a fixed 8,300 lbf chosen for 20,000 ft, and there is no sourced lapse.
- ISA with fixed thrust would leave the aircraft with too much thrust above 20,000 ft and too little below it.
- That is less consistent than the source model, which has no altitude at all.

**Prerequisite:** a sourced thrust deck (G6).

## 5. Propulsion — unchanged

Fixed research thrust, throttle inactive. No public F100-PW-100 dimensional deck exists (G4, G6; the 2026-09-28 search found nothing new). The F100 scaffolding is untouched.

## 6. Aerodynamic domain — unchanged

The domain is retained and explicit: refusal plus diagnostics status.

- **Low speed / stall — not widened.** Davison p.44 (printed 34): in flight test, wing-rock onset came ~4 AoA units earlier than the model predicted. He attributes this to Mach: "the model aerodynamics were only valid at M=0.6", and the 1-g stall map "was not physically realizable".
- **High speed — not widened:** compressibility (Baumann pp.118–119).
- **No Mach-dependent public F-15 dataset was located.**

## 7. Trim

Point 36 remains an equilibrium of the R2 plant: a lag with static gain 1, started at the trim surfaces, leaves them there. This is verified, not assumed (`[A7]`, SHADOW, V1 law at neutral, which commands exactly the trim bias).

| Quantity | Value |
|---|---|
| V | 300.8 ft/s |
| altitude | 6,096 m (source fixed density) |
| α | 17.4865° |
| θ | 14.5460° |
| stabilator | −15.1453° |
| lateral surfaces | 0 |
| thrust | 8,300 lbf |
| residual force / weight | X 2.96e-7, Y 0, Z −1.70e-6 |
| residual moment coefficients | L 0, M −3.29e-8, N 0 |

Every residual is bit-identical to R1. No residual is hidden by feedback (V1 has none).

## 8. Validation

Baseline `main` @ `f0f9519` vs candidate, each run in its own clean worktree on the same machine, compared report against report:

| Gate | Result |
|---|---|
| Compile (Unity Roslyn: runtime, runtime + editor, editor) | **0 errors** |
| Frozen F-15 suites (17) | **757 / 0**, every check line identical to baseline. The only differences are the scanned-file counts, which grow by exactly the new files (+4 FDM, +2 non-Validation). |
| Frozen F-15 Play Mode (closeout) | **39 / 0**, byte-identical to baseline and to the committed report |
| Pilot V1 headless / Play Mode | **49 / 0** / **31 / 0**, both byte-identical to baseline |
| Pilot V2 headless / Play Mode | **54 / 0** / **57 / 0**, both byte-identical to baseline |
| **Pilot R2 headless** (new) | **24 / 0** |
| **Pilot R2 Play Mode** (new) | **38 / 0**; two runs byte-identical, and identical after the driver move |
| Official Baseline v1 gate (shared / F-16) | **PASS 1585 / 0**, physics delta NONE, on `3af4e98` |

**R2 highlights:**
- **Headless:** 500-case bit-identity of the R1 step.
- **Lag in flight:** 33.0 % (stab, aileron) and 42.9 % (rudder) of each step on the first step, equal to 1 − e^(−λ·dt) exactly.
- **Neutral hold:** within 9.7e-8.
- **Trim residual:** bit-identical to R1 (force/weight ≤ 1.7e-6, Cm 3.3e-8).
- **R1 vs R2 under V2:** rate change 99–103 %, same decay.
- **Sequence:** α 15.55–20.17°, \|β\| ≤ 0.47°, end rates ≤ 0.0044 rad/s.
- **dt refinement:** ratio 2.98.

**New suites:**
- `MavF15PilotPhysicsR2Validation` (headless, batch adapter `RunAll`): checks `[A1]`–`[A9]`.
- `MavF15PilotPhysicsR2FlightValidationRunner` + the `Editor/MavF15PilotPhysicsR2FlightValidation` driver (Play Mode):
  ```
  -executeMethod MaverickFresh.FlightDynamics.EditorTools.MavF15PilotPhysicsR2FlightValidation.RunBatch -f15r2Out <path>
  ```
  Do not pass `-quit`. Checks: `[M]` mechanics, `[H]` neutral hold, `[L]` the lag in flight, `[S]` pulses, `[C]` R1 vs R2, `[Q]` sequence, `[D]` dt refinement.

**One gate failure, found and fixed.** On `d25941f` the official gate failed one counted assertion: `shared_propulsion_unity` U-003g (1584/1), because the Play Mode driver sat in `Validation/`. Moved to `Editor/` in `3af4e98`, the gate is back to PASS 1585/0. It was this change's own defect, not an F-16 regression.

## 9. Changes deliberately NOT implemented

| Candidate | Why not |
|---|---|
| Standard atmosphere (ISA density, Mach) | coupled to fixed thrust; less consistent than the source model (§4) |
| F100 thrust deck, throttle, spool, lapse | no public dimensional deck (G4, G6) |
| Mach / altitude-dependent coefficients | no public F-15 dataset (G2) |
| Low-speed / stall domain extension | contradicted by Davison's flight test (p.44) |
| High-speed extension | compressibility; no data |
| Actuator rate limits | none sourced for this configuration; a web lead "F-15 stabilator 40°/s" was an F-18 report (TM-4786) |
| Sourced surface travel | four conflicting public sets (G3, B3) |
| Mass / inertia change | the Davison values remain the version-matched source |
| CMMQ correction (positive Cmq at α 10.6–14.3°) | all public printings agree; the refit (McDonnell 1990) is not held; never tuned |
| V2 gain retune | the lagged plant keeps V2 closed-loop stable and damped (rate change 99–103 % of R1, same decay); §11 forbids tuning without an objective need |

## 10. Known limitations

- **Research-model envelope only:** the fixed 20,000-ft density, 218.5–699.7 ft/s, α −4…90°, \|β\| ≤ 20°. Aero and thrust refuse outside it.
- **Altitude has no aerodynamic effect;** thrust is fixed.
- **The lag is the research model's**, not verified F-15 hardware. There are no rate limits and no hinge-moment saturation.
- **Positive printed Cmq over α ≈ 10.6–14.3°:** a pilot holding α in that band flies destabilizing pitch damping from the source.
- **R2 is opt-in** (default R1, §3).
- **Not flown by a human in this work:** scripted Play Mode only.

## 11. Manual flight checklist (R2)

1. Open the project (Unity 6000.3.16f1). In an empty scene run **Maverick → F-15 → Place Pilot-Controlled Research F-15 V2 (Assisted) In Scene**.
2. On the placed object's **F-15 profile** component, set **Physics Revision = R2SourceActuatorLags**.
3. Press Play. The aircraft starts at point 36. The developer HUD (shown by default; F1 toggles it) shows `actuators: R2: research-model actuator lags …`.
4. W/S, A/D, Q/E: surfaces now trail the stick for ~0.15 s (stab/aileron) or ~0.11 s (rudder) to 95 %. The HUD line shows how far they trail.
5. Compare with R1: set the revision back, restart, and fly the same inputs. The response magnitudes should be within a few percent.
6. Stay inside **218.5–699.7 ft/s**. The HUD reports a research-domain refusal (aero and thrust off) outside it.
7. Avoid sustained α 10.6–14.3°, the source's positive-Cmq band.
