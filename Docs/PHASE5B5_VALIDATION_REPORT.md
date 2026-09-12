# Phase 5B.5 Validation Report

Branch `sol/phase5-wip`. Compiled 2026-09-11, **revised 2026-09-12** after review.
**Nothing committed. F16Replacement not enabled.**

Revision 4 (Phase 5B.7): D4 CLOSED - every G consumer traced and classified, and all seven
protection reads migrated from the legacy gravity-inclusive quantity to true body-normal
specific-force Nz. 5C-R mass policy frozen (spawn-time reference, no airborne transition).
CG/physics datum resolved for the isolated rig by defining a reference root whose origin IS
the CG. Concrete handover adapter implemented and failure-injected against real Rigidbody
state. Unity validation plan prepared, not run.

Revision 3 (Phase 5B.6): D5 negative limits reclassified as GAMEPLAY_SAFETY after conflating a
model-validity domain with a flight-control limit; D7 atmosphere/Mach moved to 5C-R and closed;
D8 validated dynamically; leading-edge flap resolved from source and removed from the 5C-R blocker
list; mass/CG policy and atomic handover sequence defined and failure-injection tested.

Revision 2: D8's Ixz claim corrected and closed with measured evidence; D5 fixed; Phase 5C-R
reference-envelope gate added; TP-1538 Table VI transcription workflow added; blockers split by phase.

---

## 1. Tests run

All suites are offline harnesses that compile **production source files** against a minimal
UnityEngine stub and execute production code. The stub supplies only what a game engine supplies:
a component registry, and force accumulation so that `a = ΣF/m` can be applied by the harness rather
than re-derived in a test.

| Suite | Subject | Result |
|---|---|---|
| `p5b5check` | Phase 5B.5–5B.7 physics: gravity, axis, angles, qbar, aero symmetry, control convention, force balance, energy, timestep, ownership, true Nz, dynamic inertia, handover adapter, spawn mode | **273 passed, 0 failed** |
| `fdmfull` | Phase 5A ownership validation (the real Unity editor suite, executed offline) | 59 passed, 0 failed |
| `p5acheck` | Phase 5A ownership rules, writer categories, single arming authority | 89 passed, 0 failed |
| `aoacheck` | aoaPitchReduction + envelope-protection ownership, incl. both Unity editor suites | 81 passed, 0 failed |
| `turncheck` | Phase 4B turn dynamics | 33 passed, 0 failed |
| `idcheck` | aircraft identity (commit `0851f4f`) | 33 passed, 0 failed |
| `scancheck` | identity ownership scan | 12 passed, 0 failed |
| `scenecheck` | scene-wiring / startup order | 13 passed, 0 failed |
| **Total** | | **593 passed, 0 failed** |

### The Phase 5B.5 cases, mapped to the requested list

| Requested | ID(s) | Outcome |
|---|---|---|
| GRAVITY-001 | `GRAVITY-001`, `-001b`, `-001c` | a = `(0, −9.81, 0)` exactly; **one** force write; `useGravity` off |
| GRAVITY-002 | `GRAVITY-002`, `-002b` | all four modes have exactly one source; no mode permits two |
| GRAVITY-003 | `GRAVITY-003` | Legacy→Shadow identical to 1e-4 |
| AXIS-001 | `AXIS-001` | body +X → Unity `(0,0,1)` |
| AXIS-002 | `AXIS-002`, `-002b` | body +Z → Unity `(0,−1,0)`; body +Y → `(1,0,0)` |
| AXIS-003 | `AXIS-003`, `-003b`, `-003c`, `AXIS-004` | +M → Unity −X (nose up); +L → −Z (roll right); +N → +Y (yaw right); true vs axial genuinely differ |
| AOA-001 | `AOA-001`, `-001b`, `AOA-002`, `-002b` | +5.7106° both implementations; they agree numerically |
| BETA-001 | `BETA-001`, `BETA-002` | +4.2892° both implementations |
| QBAR-001 | `QBAR-001`, `-001b`, `-001c` | qbar 0, lift/drag 0, all channels finite |
| AERO-SYM-001 | `AERO-SYM-001..001d`, `-002`, `-002b`, `-003` | Cy/Cl/Cn vanish at β=0 **while Cz/Cm do not**, so the check is not passing on an all-zero evaluation |
| AERO-CONTROL-001 | `AERO-CONTROL-001`, `-001b`, `-002`, `-003`, `AERO-CG-001..003` | dCm/dδe < 0; Cmq damping; Clp negative 5–45°; CG corrections match Eq. 16/17 including the `c̄/b` asymmetry |
| FORCE-BALANCE-001 | `FORCE-BALANCE-001`, `-001b`, `-002` | ΣF = m·g·gravityBlend; F/m = −9.81; mass-independent |
| ENERGY-001 | `ENERGY-001`, `-001b`, `-001c`, `ENERGY-002` | drag·v < 0; Cd ≥ 0 over −40…+60° α; a 10 s turn loses 300.2 → 234.5 m/s |
| TIMESTEP-001 | `TIMESTEP-001`, `-001b` | peak α differs 0.006% between dt 0.02 and 0.005 |
| OWNERSHIP-001 | `OWNERSHIP-001` | Shadow blocks replacement writes, permits legacy |
| OWNERSHIP-002 | `OWNERSHIP-002`, `-003`, `-004`, `-005`, `-006` | legacy is the only owner in Legacy and Shadow; activation refused |

Extra cases added because the audit needed them: `GRAVITY-004/b/c` (replacement is no longer a
zero-gravity hole), `GRAVITY-005/b` (gravity permission derives from the declared provider),
`CL-001/002`, `LOADFACTOR-001..005`.

## 2. The tests fail when production code is deliberately broken

Required by the brief, and run:

| Probe | What it breaks | Result |
|---|---|---|
| `axial` | collapses `AeroBodyMomentToUnityLocal` into the true-vector mapping — the exact mistake a round-trip test cannot catch | **FAIL, 4 checks** (`AXIS-003`, `-003b`, `-003c`, `-004`) |
| `alpha` | inverts `ComputeAlphaRad` | **FAIL, 3 checks** (`AOA-001`, `-001b`, `-002b`) |
| `gravityflag` | restores the original defect: aero body forces `useGravity` off regardless of owner | **FAIL, 2 checks** (`GRAVITY-004`, `-004b`, reporting `got (0,0,0)`) |
| `gravitygate` | reverts the custom-gravity gate to the general legacy gate | **PASS — no failure** |

The fourth result is reported as-is. `LegacyPhysicsAllowed` and `LegacyCustomGravityAllowed` agree on
all four current modes, so that half of the gravity change is defence in depth and not a behavioural
fix. `GRAVITY-005b` states this inside the suite output so the separation is never credited with more
than it does.

## 3. Sources reviewed

Full text extracted and read, not merely cited:

1. **Morelli, NASA NTRS 20040110310** — *Global Nonlinear Parametric Modeling with Application to
   F-16 Aerodynamics*. Table 1 (independent-variable ranges), Eqs. 12–18, §3 results.
2. **Nguyen et al., NASA TP-1538** (Dec 1979, 233 pp) — *Simulator Study of Stall/Post-Stall
   Characteristics … Relaxed Longitudinal Static Stability*. Table I (mass, geometry, surface
   limits), **Table VI (thrust values)**, nomenclature, Engine Simulation section.
3. **Garza & Morelli, NASA TM-2003-212145** — *A Collection of Nonlinear Aircraft Simulations in
   MATLAB*. Table 1, engine model section (`tgear`, `rtau`, `pdot`).

No Tier 2 material (AeroBench, JSBSim, community simulators) contributed any value. The NASA MATLAB
package was deliberately not used.

## 4. Gravity ownership result

| Mode | Provider | `Rigidbody.useGravity` | Legacy custom gravity | Net |
|---|---|---|---|---|
| Legacy | `LegacyAeroCustomGravity` | OFF | permitted (blend 1.0 for F-16) | **1 g** |
| Shadow | `LegacyAeroCustomGravity` | OFF | permitted | **1 g — identical to Legacy** |
| Fault | `LegacyAeroCustomGravity` | OFF | permitted | **1 g** |
| F16Replacement | `UnityRigidbody` | **ON** | blocked | **1 g** (was **0 g** before this phase) |

One owner per mode, no mode with none, no mode with two. The authority owns the flag and asserts it
every step at `DefaultExecutionOrder(-400)`; `debugGravityFlagCorrections` makes any remaining fight
observable. Legacy and Shadow behaviour is unchanged — verified numerically, not asserted.

`F16Replacement` resolves to `UnityRigidbody` because `MavSixDoFBody` does not carry a gravity term
in its load set. That is a statement about the code, not an intention. When the replacement stack
grows its own gravity term, `ResolveGravityProvider` is the one line that changes.

## 5. Axis / sign audit result

The replacement path has **exactly one** conversion boundary, `MavFlightDynamicsMath`, and it
correctly separates true vectors from axial vectors (determinant −1). All mappings verified:

```
body +X (fwd)   -> Unity (0,0,1)     body +Y (right) -> Unity (1,0,0)
body +Z (down)  -> Unity (0,-1,0)
+L (roll right) -> Unity -Z          +M (nose up)    -> Unity -X
+N (yaw right)  -> Unity +Y
alpha: +5.7106 deg legacy == +5.7106 deg replacement
beta:  +4.2892 deg legacy == +4.2892 deg replacement
+alpha -> +CL,  -alpha -> -CL
+delta_e -> Cm -0.2680;  -delta_e -> Cm +0.1271   (positive elevator pitches nose down)
+beta -> Cy negative (side force left)
```

No corrective minus signs are scattered through the runtime. **No axis or sign defect was found.**
The legacy stack does not use the boundary — it works directly in Unity axes with its own α/β
formula — but the two implementations were cross-checked and agree to 4 decimal places, which is the
property that matters for the shadow comparison to mean anything.

## 6. The −9.9 g / negative-CL observation — explained and proven

**It is physically valid negative lift, and it is not a sign bug anywhere.** Full evidence in defect
**P5B5-D5**. In short:

- `CL = −1.13` requires negative AoA. Production `ComputeLiftCoefficient` returns −CL for −α, as it
  should (`CL-002`).
- Negative AoA gives belly-ward lift, clamped at `maxLiftG = 9.4` for the F-16; plus gravity ⇒ ≈ 9.9 g.
- `gEstimate = Dot(dv/dt, up)/g` reads belly-ward acceleration as **negative**. −9.9 g is arithmetic.
- Four candidate sign errors were each ruled out by a deterministic test, not by assumption.

**The real defect is that the aircraft could get there.** All four AoA/G protection branches are
gated on `targetPitch < 0f` — a nose-up command — so **negative AoA and negative g have no
protection at all**. A held push-over passes the 22°/30° breakpoints without the limiter ever
engaging. That is P5B5-D5, open, because fixing it changes handling.

A secondary finding: the G telemetry is kinematic acceleration, not load factor, so it reads ~1 g low
(0 g in level flight instead of +1 g). That is P5B5-D4 — a magnitude offset, not the sign.

## 7. Morelli valid-envelope conclusion

**The Mach < 0.6 limit is authoritative, quoted verbatim from the source**, and the repo's
`MaxReferenceMach = 0.6` is correct rather than a guess: Morelli §3 describes "a 16% scale model of
the F-16 aircraft flying at relatively low Mach numbers (< 0.6), out of ground effect, with landing
gear retracted and no external stores". The model contains **no Mach term at all** (Eqs. 12–17), so it
is Mach-independent *within* that band and silent outside it. Its single published validation point is
α = 10°, **Mach 0.26**, sea level.

Gameplay reaches Mach 0.7–0.8. **This is a genuine architecture blocker, not a tuning matter.**

**Recommendation: option D — hybrid with an explicit, validated transition — and in the interim
option A.**

- **Now (this phase, already true):** option **A**, strictly limit the reference FDM envelope. The
  Morelli path stays non-live above Mach 0.6 and reports the excursion.
- **Next:** option **B** is worth one bounded attempt before D. TP-1538's aerodynamic appendix covers
  a wider envelope than the compact polynomial fit, and it is a permitted public Tier 1 source. If its
  tables cover the 0.6–1.0 band, the hybrid has a sourced upper half instead of an approximate one.
- **Then:** option **D**, with the blend region declared, provenance-labelled per band, and validated
  against the low-Mach model where they overlap.
- **Rejected: option C alone.** An approximate extension layer with no sourced upper model is a
  guessed transonic correction wearing a provenance label.

No transonic model was implemented in this phase.

## 8. Propulsion-data conclusion — **this changed**

**A permitted public dimensional thrust deck exists.** NASA **TP-1538 Table VI**, "THRUST VALUES USED
IN SIMULATION", tabulates **idle, military and maximum thrust as a function of Mach (0 … 1.0) and
altitude (0 … 15 240 m)**, in both SI and US units. TP-1538 is a NASA Technical Paper freely available
on NTRS — Tier 1, public, no release restriction, and it makes the U.S.-release-gated MATLAB package
unnecessary.

Also now authoritative and available:

| Item | Value / form | Source |
|---|---|---|
| Thrust interpolation | `T = T_idle + (T_mil − T_idle)·(P/50)` for P < 50; `T = T_mil + (T_max − T_mil)·((P−50)/50)` for P ≥ 50 | TP-1538 + TM-2003-212145 |
| Power-level time constant | τ = 1.0 s if (Pc−P) < 25; 0.1 s if > 50; `1.9 − 0.036(Pc−P)` between | TM-2003-212145 |
| Throttle gearing | [0,1] → [0,100] commanded power, non-linear | TM-2003-212145 (`tgear`), curve in TP-1538 fig. 66(b) |
| Engine angular momentum | **216.9 kg·m²/s (160 slug-ft²/s)** along body X | TP-1538 |

**What has NOT been done, and why.** The Table VI numbers were **not transcribed**. The OCR of that
table is column-interleaved — idle, military and maximum rows are shuffled together with the altitude
headers — and transcribing a mangled thrust table would be exactly the invented data this phase
forbids. The honest state is therefore preserved: `HasAuthoritativeData = false`, `thrust = 0 N`.

**Recommendation.** Transcribe TP-1538 Table VI by reading the PDF's rendered pages (~92–93)
directly, cross-check the SI and US columns against each other as an arithmetic check, and freeze the
result with a table hash through the existing `MavThrustDeckProvenance` mechanism. That is a bounded,
sourced task — no approximation needed, and no `ApproximateGameplay` propulsion model required.

## 9. FLCS source-confidence conclusion

**Low confidence for any production F-16C claim; moderate confidence for a documented structure.**

What the sources support, for the **TP-1538 1979 relaxed-static-stability development aircraft**:
a CAS in which the pilot commands normal acceleration, with pitch rate and filtered normal
acceleration fed back, angle-of-attack feedback reducing the commanded Nz, an AoA limiter system, and
leading-edge flap scheduled on α and q/Ps.

What the sources do **not** support: any specific gain, any production F-16C Block FLCS behaviour, or
yaw/roll coordination laws. Roll rate has exactly one public data point ("limited to only about
165°/sec").

**Every gain in `MavF16ControlLawV01` is `MAVERICK_TUNING` and must be labelled so.** AFTI, VISTA and
MATV control laws were deliberately not consulted and must never be merged into this model — they are
different aircraft.

Permitted description: *"a Nz-command CAS in the structural style documented for the TP-1538
relaxed-static-stability F-16, with Maverick-chosen gains."* Not permitted: *"the real F-16C FLCS."*

## 10. Reference-configuration gap classification

| Item | Classification | Note |
|---|---|---|
| Leading-edge flaps | **BLOCKER FOR LIVE REPLACEMENT** | Morelli data is for a specific LEF state; running with no LEF schedule is an unquantified mismatch. Limit (25°) and schedule variables are sourced |
| Trailing-edge flap assumptions | IMPORTANT LATER | Flaperons are the aileron surface; a separate TEF assumption is undocumented |
| Speed brake | OUTSIDE V0.1 REFERENCE SCOPE | Limit 60° sourced, not modelled |
| Landing gear aerodynamics | OUTSIDE V0.1 REFERENCE SCOPE | Morelli data is gear-retracted by construction. See also D3 |
| External stores | OUTSIDE V0.1 REFERENCE SCOPE | Morelli data is stores-clean by construction |
| Ground effect | OUTSIDE V0.1 REFERENCE SCOPE | Morelli data is out-of-ground-effect by construction |
| Transonic / supersonic | **BLOCKER FOR LIVE REPLACEMENT** | D10 |
| Engine thrust | **BLOCKER FOR LIVE REPLACEMENT** | Data now known to be public and available; transcription pending |
| Engine gyroscopic moment | IMPORTANT LATER | Value sourced (216.9 kg·m²/s), not applied |
| Actuator rate limits | IMPORTANT LATER | Current values are `MAVERICK_TUNING`; no sourced rate limit found |
| Fuel / mass change | OUTSIDE V0.1 REFERENCE SCOPE | Fixed mass |
| Centre-of-mass mapping to the Unity model | **BLOCKER FOR LIVE REPLACEMENT** | Nothing verifies the model origin corresponds to 0.25 c̄ |
| Inertia tensor application | **BLOCKER FOR LIVE REPLACEMENT** | D8 — Ixz declared but unrepresentable in a diagonal tensor |
| Atmosphere mismatch | IMPORTANT LATER | D7; two atmosphere implementations coexist |
| Wind-relative airspeed | IMPORTANT LATER | No wind model; TAS == ground speed |
| Trim implementation | IMPORTANT LATER | `MavTrimSolver` exists; not exercised this phase |
| Structural / G / AoA limits | IMPORTANT LATER | D5, D11 |
| High-α / departure behaviour | **BLOCKER FOR LIVE REPLACEMENT** | Morelli stops at 45°; Cn_β reverses sign between 25° and 35° (measured in Phase 5B), and the legacy model has no yawing moment at all |

## 11. GO / NO-GO for Phase 5C

### **GO FOR ISOLATED 5C-R UNITY TEST**

Not a go for gameplay cutover, and not a go for anything powered. A go for the isolated, unpowered,
spawn-time reference experiment described in `Docs/PHASE5CR_UNITY_VALIDATION_PLAN.md`.

**Why the recommendation changed.** Revision 3 listed three 5C-R blockers. All three are closed:

| Was blocking | Closed by |
|---|---|
| CG mapping unmeasured | Option B. `MavF16ReferencePhysicsRoot`'s origin is *defined* as the reference CG, so `centerOfMass` is exactly zero by construction and no mesh pivot is inferred. An `Undeclared` datum, or one with no provenance string, is refused |
| Mass policy not chosen | Frozen. 5C-R spawns at 9298.65 kg; reference-mode-with-legacy-mass, mass-changed-after-physics-begins and unspecified-provenance are each refused in code |
| No concrete handover target | `MavRuntimeHandoverTarget` implemented, failure-injected at all eight steps, with the real Rigidbody's mass, centre of mass, inertia tensor, velocities and gravity flag verified restored 8/8 |

And D4 — the omitted blocker this phase was called for — is closed: the G limiter now consumes true Nz.

**What stands between this and a result is a scene and a pilot**, not more code. Everything is
deliberately not auto-installed: `MavFreshBootstrap` does not touch the reference root,
`allowReplacementActivation` is false, and nothing in the project constructs a handover target.

**Three honest caveats on the go.**

1. **Coverage.** Reference Mach is higher than the legacy HUD suggested. Mach 0.6 is 204 m/s at sea
   level but only 177 m/s at the tropopause, so an unpowered glide from altitude will exit the envelope
   on speed before it runs out of height. Plan cases 2, 9 and 10 around that.
2. **Case 8 is the real unknown.** The principal-axis inertia representation is validated to
   ~4·10⁻⁷ rad/s² against two independent formulations, but whether Unity's own solver honours
   `inertiaTensorRotation` under large simultaneous p, q, r has never been measured. If any case fails,
   expect it to be that one.
3. **D4's fix changes legacy handling.** The G limiter now sees +1 g in level flight where it used to
   see 0, so it engages about a g earlier than before in terms of the old reading. That is the
   correction, not a side effect — but it means the legacy aircraft will feel different in sustained
   high-g flight and should be flown before anyone concludes the change was free.

**Not recommended yet:** a gameplay system that flips Legacy ↔ Replacement while crossing Mach 0.6.
The replacement model has not yet been shown to fly at all, and building the switching machinery before
that is answered would be solving the second problem first.
