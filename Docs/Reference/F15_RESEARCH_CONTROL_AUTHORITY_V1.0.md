# F-15 — Research Surface Authority Audit (V1.0, WP-2)

> **Research configuration only** (`F15_AFIT_BAUMANN_DAVISON_MACH06_20K_RESEARCH`). **Nothing here is NASA 836.** Exact hard stops and actuator rates stay unavailable.

**Question:** do the held AFIT research sources give the research model any sourced surface authority, and what is its stabilator sign convention?

**Sources:** only the lineage already held — Baumann 1989, Davison 1992, Nolan 1992, McDonnell 1990, TM-72861, and the public sets recorded in `F15_DN1180_SOURCE_LINEAGE_V0.1.md` §3. **No new citation was followed.**

| | |
|---|---|
| Code | `F15/MavF15ResearchControlAuthority.cs` |
| Tests | `Validation/MavF15ResearchControlAuthorityValidation.cs`, `[A1]`–`[A7]`, 21 checks, PASS |

---

## 1. Three things kept apart — three types

| Concept | Type | Research configuration | Exact 836 |
|---|---|---|---|
| **A)** stabilator values the research **source commanded** | `MavF15ResearchDemonstratedSurfaceRange` / `…ControlRange` (Kind `ResearchDemonstratedControlRange`) | **declared** — §2 | — |
| **B)** where the surface **physically stops** | `MavF15PhysicalSurfaceHardStops` | **not adopted** — §3 | **unavailable — zero travel** |
| **C)** how fast it **moves** | `MavF15ActuatorRateLimits` | **unavailable** — §4 | **unavailable** |

**No conversion exists from A to B, C, an actuator limit or a surface state** (`[A1]`, reflection over every member). B and C are read-only projections of the actuator's `MavF15SurfaceLimits`, so they cannot be built from a research type.

## 2. A — ResearchDemonstratedControlRange (declared)

**The rule:** declare only as far as the source *demonstrably exercises* its model, meaning **tabulated** equilibrium solutions. Plot-only continuations are excluded.

| Baumann (DTIC ADA217366) table | Stabilator | Other surfaces | States | Rendered page checked |
|---|---|---|---|---|
| Table III — stable flat spins | **−25°** | δa = δΔe = 0; rudder −0.98…−0.60° | α ≈ 80–81°, V ≈ 227 ft/s | PDF p.74 ✓ |
| Table IV — flat spins | −19° (caption; text says −19.72 / −19.73) | aileron 0; rudder varies | α ≈ 74–81°, V ≈ 226–231 ft/s in the rows read | text only |
| Table V — stable flat spins | **−5°** | δa = δΔe = 0; rudder 12.81…14.15° | α ≈ 80–81°, V ≈ 219–220 ft/s | PDF p.82 ✓ |
| Table VII — stable low-α equilibria, 201 points | **−17.30722 … −5.275754°** | aileron, rudder, differential tail **all 0** | α 7.9…19.2°; V 288.7…699.7 ft/s; thrust 8,300 lb; 20,000 ft | PDF pp.124, 126, 128, 129, 131 ✓ |
| Table VIII — rudder sweep at δe −19° | −19° | rudder varies | α ≈ 20° | text only |

**Declared** (`AfitBaumannTabulatedEquilibria()`):

| Channel | Research demonstrated range | Basis |
|---|---|---|
| symmetric stabilator | **−25° … −5°** | Table III (−25) and Table V (−5) bound Table VII's −17.31…−5.28 |
| aileron, differential tail, rudder | **exactly 0°** (degenerate `[0, 0]`) | every Table VII point holds them at 0 |

**Not counted:**
- Baumann fig. 5-1's sweep at −29° (plot only).
- Appendix C's stabilator continuations, which run far past any physical limit. Davison (ADA256613 PDF p.77) says the bifurcation continuation deliberately goes beyond "the physical limit of elevator travel, −29 degrees".
- Tables III–V and VIII do tabulate nonzero **rudder** (at least −3.19 … +14.15° in the rows read). That is recorded here but **not declared**, because nothing needs it yet.

**What the range is not:**
- It is **not a physical limit**: `IsPhysicalLimit` is always false, and no type name says Physical, HardStop or Limit (`[A3]`).
- It is **not the flying research aircraft's authority**: the research profile still declares zero surface travel (`[A2]`).
- It is **not a statement about M 0.6**. The tabulated states lie at V = 218.5…699.7 ft/s.

## 3. B — physical hard stops: not adopted

- **Baumann Table VI** (PDF p.87, "Physical Characteristics of the F-15B") prints:
  - stabilator travel **+20 to −30 degrees**;
  - aileron and rudder travel extracted as "+30 degrees" (the ± character is not confirmed on a rendered page).
- **Davison's text** gives the elevator's physical limit as **−29°** (PDF p.77).
- These join the public sets that already disagree (15/−26, 15 up/29 down, +20/−30, +15/−25; `F15_DN1180_SOURCE_LINEAGE_V0.1.md` §3).

**Not adopted, even for research.** Picking one set would take a side without authority. Exact 836 stops stay **zero** (`MavF15PhysicalSurfaceHardStops.ExactTarget()`, `[A2]`).

## 4. C — actuator rates: unavailable. Research lags found but not wired

Davison's driver, PDF p.97 (printed p.87), says "Revised 23 Aug 89 … Equations assume CAS off and Aileron-Rudder Interconnect off":

```
F(9)  = 20.*(CDSTBD-DSTBD)      stabilator
F(10) = 28.*(CDRUDD-DRUDD)      rudder
F(11) = 20.*(CDAILD-DAILD)      aileron
F(12) = 20.*(.3*CDAILD-DDTD)    differential tail = 0.3 x commanded aileron
```

These are **first-order lags** — a bandwidth of 20 / 28 / 20 / 20 s⁻¹ — version-matched to the research model. They are **not rate limits**: no research source held gives a deflection-rate limit.

**Not wired**, as the WP-2 brief requires. The F-15 actuator has no lag or bandwidth member (`[A7]`). Adopting the lags for research only is a separate decision, recorded in the opportunities doc.

## 5. Sign convention — verified from the source, not from expected behaviour

**Source statement** (Baumann Appendix A, PDF p.86, printed p.71): "Horizontal stabilator surfaces are positively deflected when the leading edges are up (trailing edges are therefore down.)"

The same page gives:
- rudder positive = trailing edge left;
- aileron positive = right aileron trailing edge down.

These are recorded in `MavF15ResearchControlConventions`.

**Regression against Baumann Table VII** (`[A5]`).

- **Physics:** at a published pitch equilibrium (q = p = r = 0), aero Cm plus the source's thrust-line term must vanish.
- **Terms and constants:**
  - Cm uses Maverick's transcribed `MavF15BaumannMach06Longitudinal.Evaluate(α, δe, 0)`.
  - The thrust-line term is `THRUST·(0.25/12)/(q̄·S·c̄)`.
  - The source's own RHO = 0.0012673 slug/ft³ (Davison PDF p.91).
- **Assertion:** relative only — the source sign against the flipped sign, and with the thrust term against without it. No tolerance is invented.

| Table VII point | δe | α | V, ft/s | Cm + CmT, source sign | Cm + CmT, flipped | CmT |
|---|---|---|---|---|---|---|
| 1 | −9.923545 | 13.43360 | 338.4 | **+5.0e-9** | −0.134 | 2.46e-4 |
| 10 | −11.02902 | 14.30227 | 329.0 | **−8.6e-8** | −0.149 | 2.60e-4 |
| 20 | −12.44352 | 15.39903 | 318.3 | **−1.2e-7** | −0.168 | 2.78e-4 |
| 30 | −14.01293 | 16.60968 | 307.7 | **+3.3e-8** | −0.190 | 2.97e-4 |

**Result: the sign convention matches the source.** With the source sign, the pitch equilibrium closes to single-precision round-off, while the flipped sign misses by 0.13–0.19. The residual is also far smaller than the thrust-line term itself, which confirms that term's nose-up sign.

**Side finding:** Baumann's 1989 pitch equilibria are reproduced to round-off by Maverick's transcription of **Davison's 1992** routine. So, for the pitching moment at q = 0, the two versions agree at these four points.

**Checked:** only Cm at these four points. Forces, lateral axes and damping terms are **not** checked, so no general version equivalence is claimed.

## 6. STATIC_EQUILIBRIUM_VALIDATION_ONLY control

`MavF15ResearchStaticControlState` holds a research surface setting **only** to evaluate a published equilibrium:
- It is created against the demonstrated range and refuses anything outside it.
- Its one conversion leads into the research coefficient routine, via `MavF15BaumannSurfaceState`.
- It cannot become a surface state, a request, an actuator limit or a stop.

`[A6]`:
- Every Table VII test point's inputs are representable and pass through unchanged.
- These are refused:
  - −30° (Baumann's physical-table end);
  - 0° stabilator;
  - a nonzero aileron;
  - a NaN;
  - a range not belonging to the research configuration.

## 7. What this means for WP-3 (trim) — not started

**Unblocked:**
- Research equilibrium **inputs** are now representable, and **static coefficient-level** checks against Table VII can run headless.
- The pitch-moment check above is one.

**Still blocked for trim in the flying research body:**
1. **Zero surface travel.** No research physical stop is adopted (§3). The actuator therefore holds every surface at neutral, and the static control is not an actuator input.
2. **The research envelope gate.** The aero and thrust refuse outside M 0.6 ± 0.001 at 6,096 ± 1 m, and **no tabulated equilibrium lies inside it**.
   - Table VII's closest points are 120 (V = 631.6 ft/s, M ≈ 0.609, in a −63° bank) and 121 (606.5 ft/s).
   - The source applies its M 0.6 coefficients at 219–700 ft/s by its own modelling assumption.
   - Reproducing Table VII in the body needs a decision on the research speed domain first.
3. **In-source inconsistency, recorded not resolved.** Table VII points 116–120 have V = 631.6…699.7 ft/s, but Baumann's text (PDF p.119) says equilibria above 622.14 ft/s were omitted.

Whether an M 0.6 level-flight trim would fall inside the demonstrated range is a WP-3 question. **No trim was solved here.**
