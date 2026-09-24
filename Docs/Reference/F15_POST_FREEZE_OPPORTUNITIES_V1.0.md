# F-15 — Post-Freeze Opportunities (V1.0)

Ranked follow-on work after the V1 freeze checkpoint. No broad source hunt was run to produce this list. Every item is judged from what the V1 passes already hold or have already searched.

**Value classes:**
- **HIGH VALUE**
- **MEDIUM VALUE**
- **LOW VALUE**
- **BLOCKED / NOT WORTH PURSUING**

**Axes used for each item:**
- implementation value;
- probability of public-source closure (*closure*);
- impact on the exact 836 path (*exact*);
- impact on research-mode fidelity (*research*);
- validation value;
- effort — S (a pass), M (two or three passes), L (more).

---

## A. Exact NASA 836 opportunities

| # | Opportunity | Class | Why | closure | exact | research | validation | effort |
|---|---|---|---|---|---|---|---|---|
| A1 | ✅ **DONE (WP-2)** — **Cross-check the R3 stage chain against 836's own simplified control block diagrams** (NASA/TM-2009-214651 figs. 3–5; TM-2012-215978 figs. 3–5) | **HIGH VALUE** | Already held. It is the only **exact-scope** FCS evidence. It can upgrade the FCS *architecture* layer to exact structure, and it adds two exact logic facts: ARI out above M 1.5, roll-yaw crossfeed out above M 1.0. Gains stay unavailable. | certain (held) | medium (structure) | medium | high | S |
| A2 | ✅ **DONE (WP-2), with a scope correction** — TM-2012's derivative borders are unscaled and its CAS-off figures are spike-configuration, so both were excluded; TM-2008 figs. 12–15 and TM-2012 figs. 27–29 (baseline simulation) were digitized (`F15_836_VALIDATION_DATA_V1.0.md`) — **Digitize 836 baseline-flight derivative trends and flight/simulation time histories as validation-only data**: TM-2008-214634 figs. 12–15 (Cnβ and Cmα vs Mach for the baseline; baseline push-over–pull-up and rudder-sweep responses); TM-2012-215978 figs. 15–23 and 30–33 (derivative borders; CAS-off Dutch-roll and short-period characteristics) | **HIGH VALUE** | Public, exact-scope, flight-derived. They become the first 836 **validation targets**. They are nondimensional trends, so they cannot close `S` / `c̄` / `b`, and direct comparison needs matching reference definitions. Store as `OriginalPrimary × Exact836`, never as model data. | certain (held) | medium (validation only) | low | **high** | M |
| A3 | DFRC F-15B simulation documentation (the "baseline aerodynamic model" the Quiet Spike reports update) | **MEDIUM VALUE** — one time-boxed search only | Would close G1/G2 outright if public. Nothing so far suggests it is. | low | **very high** | high | high | S (search) |
| A4 | 836 inlet / propulsion installation geometry | **LOW VALUE** | No 836 source seen; installed effects are downstream of a thrust scale that does not exist yet | low | medium | low | low | M |
| A5 | 836 engine mount / thrust-line coordinates | **MEDIUM VALUE** — narrow | Needed before any engine-out or asymmetric test. Family numbers exist (AFIT tables: ±25.5 in lateral, 0.25 in vertical, nozzle pivot −20.219 ft) but are unattributed. A primary F-15 installation source may exist. | low–medium | medium | medium | medium | S–M |
| A6 | 836 engine sub-configuration (build level before 2014) | **BLOCKED / NOT WORTH PURSUING** | Searched in R5; nothing names it. The gate also needs control-schedule equivalence, which DEEC-era evidence contradicts. | very low | high | — | — | — |
| A7 | Further P680063 chronology | **BLOCKED / NOT WORTH PURSUING** | Done (six phases). The story runs on NASA #2 and #8, not 836. | — | none | none | low | — |

## B. F-15 family / production-reference opportunities

| # | Opportunity | Class | Why | closure | exact | research | validation | effort |
|---|---|---|---|---|---|---|---|---|
| B1 | **AFFTC control-system reports AFFTC-TR-74-8 and AFFTC-TR-76-48** — the sources behind TM-72861's appendix and figs. 17–23 | **MEDIUM VALUE** | Could give production F-15A PRAD/RRAD/ARI/CAS schedules and actuator data at family scope. Worth one distribution-status check. AFFTC-TR-75-32 is AD-B (limited) and must not be sought. | low–medium | low (family) | medium | medium | S (check) |
| B2 | Digitize TM-72861 public curves: RRAD (fig. 20), ARI (fig. 23), roll-CAS feel (fig. 21) | **MEDIUM VALUE** | Public now. Would let `F15FamilyReference` mode run the mechanical ratio changers and ARI at `PublicReference`, preproduction scope. Never exact. | certain | none | medium | medium | S–M |
| B3 | Actuator travel / rate limits | **LOW VALUE** now | Four public sets conflict; resolving them needs B1 or DN-1180. Digitizing any one set would pick a side without authority. | low | low | low | low | — |
| B4 | Roll-damper washout schedule (Davison fig. 27) | **MEDIUM VALUE** (research) | Zero point 20.2° α is in the text; start ≈7–8° and plateau ≈5 are plot-only. Suitable for `AFITResearch` mode as a PublicReproduction, and closes the one R3 item that has a public curve. | certain | none | medium | low | S |
| B5 | Further A4172 public reproductions (Barth 1987, Beck 1989 — upstream of the reproduced tables) | **LOW VALUE** | Would tighten provenance of family values already held. Cannot close anything for 836. | medium | none | low | low | S |
| B6 | DN-1180 derivative material | **LOW VALUE** | One public citation, no numbers. Further theses are unlikely to reproduce what Nolan did not. | low | low | low | low | S |
| B7 | A4172 Part II original / DN-1180 original | **BLOCKED / NOT WORTH PURSUING** | **NOT PUBLICLY LOCATED.** Not a standing task. | — | — | — | — | — |

## C. Research-mode improvements

| # | Opportunity | Class | Why | closure | exact | research | validation | effort |
|---|---|---|---|---|---|---|---|---|
| C1 | ✅ **DONE (WP-1)** — **Research-tagged profile provider** — Baumann/ARO10 reference geometry, plus the research model's **own** version-matched mass/inertia from Davison's driver (37,000 lb; Ix 25,480, Iy 166,620, Iz 186,930, Ixz −1,000 slug-ft²; DTIC ADA256613) rather than 836's Table 1, under an explicit research ID | **HIGH VALUE** | Without it the research model can never run in a `MavSixDoFBody` (a structural readiness requirement). Every research PlayMode, trim and perturbation test depends on it. | n/a | none (by design) | **high** | **high** | M |
| C2 | ✅ **DONE (WP-1)** — **Research-only thrust input at the source condition** — the research model's own fixed 8,300 lb (Baumann PDF p.34, p.124), tagged research, never entering R5 | **HIGH VALUE** (with C1) | Every research trim needs thrust. The source states the figure it used. Must stay outside `MavF100*`. | certain | none | high | high | S |
| C3 | F100-PW-100(3) component-map digitization | **BLOCKED / NOT WORTH PURSUING** | The maps behind TP-1034 (CCD1103-1.0) are unpublished. R5 found turbine/fan torque unavailable. | very low | none | — | — | — |
| C4 | TP-1069 / TP-1228 | **LOW VALUE** | Public NASA TPs, likely obtainable. They would fill the family-B dimensional gross-thrust dataset, but contain **no static point** and so cannot supply the TP-1034 normalizer. Family B cannot combine with A or C. | high | none | low | low | M |
| C5 | TP-1056 follow-on engine data | **LOW VALUE** | TP-1056 is held. Follow-ons would concern the same restricted-spec gap. | low | none | low | low | S |

## D. Validation / infrastructure

| # | Opportunity | Class | Why | effort |
|---|---|---|---|---|
| D1 | ✅ **DONE (WP-1)** — **Exact-vs-research contamination tests across modules** — research geometry never reaches the exact profile; research thrust never reaches R5; family FCS never passes the exact floor; research profile IDs never carry the 836 target ID; `MavF15AeroModel` refuses research without opt-in (now covered by `[X4]`) | **HIGH VALUE** | Cheap, and it guards the whole architecture. **Must precede or accompany C1**, which is the change most likely to leak. | S |
| D2 | **Research-mode trim validation vs Baumann Table VII** (ADA217366 PDF pp.124–129: equilibria against stabilator at 8,300 lb and 20,000 ft) | **HIGH VALUE** (after C1+C2) | The only sourced trim reference for the research model. Report differences; no tolerance can be asserted while Maverick's Davison-version transcription differs from Baumann's 1989 original. | M |
| D3 | Research-mode static-derivative checks (Cmα, Cnβ, Clβ, Cmq from the transcribed polynomials at the source condition, against the thesis curves; McDonnell 1990 fig. 4-2 Cmq) | **MEDIUM VALUE** | Headless, no PlayMode needed. Needs curve digitization. | S–M |
| D4 | Source-envelope validation (refusal at the M/altitude/α/β edges, in PlayMode) | **MEDIUM VALUE** | Headless coverage exists for the domain gate; PlayMode confirmation follows C1. | S |
| D5 | Automated deterministic trajectory / perturbation tests (pinned `Time.captureFramerate`) | **MEDIUM VALUE** (after C1) | Makes research-mode regressions bit-exact; no pass criteria beyond "bounded, finite, inside span" until D2 exists | M |
| D6 | F-15 suites on the Unity `Maverick/Flight Dynamics` menu | **LOW VALUE** | Convenience for the user's Phase-1 run | S |
| D7 | *(new, WP-2)* **Re-run the reconstructed digitization scripts** against the public PDFs and diff the regenerated `MavF15Nasa836ValidationData.cs` | **MEDIUM VALUE** | The scripts in `Data/F15/wp2_digitization/` were rebuilt verbatim after the scratch copies were lost, and have not been re-run. Needs the two PDFs downloaded again. | S |
| D8 | *(new, WP-2)* Route the stall inhibitor through the pitch CAS in exact mode, as 836's pitch diagram draws it | **LOW VALUE** now | Structural; can only remove output. No numeric consequence until a gain exists. | S |
| D9 | *(new, WP-2)* Research speed-domain decision for Table VII trim | **HIGH VALUE** for WP-3 | Baumann applies his M 0.6 coefficients at 219–700 ft/s; Maverick's research gate is M 0.6 ± 0.001, which excludes every tabulated equilibrium | S (decision) |
| D10 | *(new, WP-2)* Research actuator lags (Davison PDF p.97: 20 / 28 / 20 s⁻¹, differential tail = 0.3 × aileron) | **MEDIUM VALUE** (research) | Version-matched bandwidth, not a rate limit. Would need an actuator lag type kept separate from rate limits. | S |

---

## 5. Next three work packages (recommended — not implemented)

**WP-1 — Research-mode flight enablement, with contamination guards first** (C1 + C2 + D1) — ✅ **COMPLETE.** Structural research readiness reached (`F15_RESEARCH_PROFILE_V1.0.md`).
- Findings from doing it: the 8,300 lbf is TOTAL thrust and carries a nose-up thrust-line moment (THRUST × 0.25 in); Davison's inertia lines are commented but reproduced by his active constants.
- Shortfall against the original outcome line: true PlayMode was not run (the batch Play Mode bridge is hard-wired to the scheduler probe). An editor-seam pipeline smoke runs instead.

**Original scope:**
- Add a separately tagged research profile provider: Baumann/ARO10 geometry, the research model's own version-matched mass/inertia, and an explicit research ID.
- Add a research-only fixed-thrust input (8,300 lb at the source condition).
- Before either lands, add the cross-module contamination tests.
- Exact path unchanged and still fail-closed.
- Outcome: research mode becomes runnable in PlayMode for the first time.

**WP-2 — Exact 836 FCS structure and validation targets** (A1 + A2) — ✅ **COMPLETE.** See `F15_836_FCS_STRUCTURE_V1.0.md`, `F15_836_VALIDATION_DATA_V1.0.md`, `F15_RESEARCH_CONTROL_AUTHORITY_V1.0.md`.
- 9 of 11 stages are confirmed as exact 836 structure. The Mach 1.5 ARI and Mach 1.0 crossfeed switches are in code, exact mode only.
- 60 validation-only series digitized. TM-2012's derivative and CAS-off figures were excluded as spike-configuration or unscaled.
- Research surface authority audit: a demonstrated stabilator range of −25…−5° (not a physical limit). The sign convention was verified against Baumann Table VII.

**Original scope:**
- Cross-check the R3 stage chain against 836's own block diagrams. Record the M 1.5 / M 1.0 switch points as exact logic facts, with gains still unavailable.
- Digitize the baseline-flight derivative trends and time histories as `OriginalPrimary × Exact836` validation-only data, never model data.
- No source hunt needed: everything is already held.

**WP-3 — Research-mode validation against its own sources** (D2 + D3 + D5, after WP-1)
- **New prerequisite found in WP-1:** the research aircraft has zero surface travel. Baumann's Table VII equilibria need stabilator around −10°, so the D2 trim comparison needs a *research-scoped* surface-authority decision first. Baumann's own Table VI prints stabilator +20/−30°, but it conflicts with the other public sets. D3 (static derivatives, headless) is **not** blocked by this.
- **Status after WP-2:** Table VII's inputs are now representable as `STATIC_EQUILIBRIUM_VALIDATION_ONLY`, and a coefficient-level pitch check already closes to round-off. Trim **in the flying body** still needs:
  - D9, a research speed-domain decision: no tabulated equilibrium lies inside the M 0.6 ± 0.001 gate;
  - research surface travel: physical stops are still not adopted.
- Compare trim against Baumann Table VII.
- Check static derivatives against the thesis curves.
- Run deterministic perturbation runs inside the source envelope.
- Report differences; assert no invented tolerance.

**Deliberately not in the top three:** B1 (a narrow distribution check, worth folding into any later pass), B2 and B4 (family and research FCS curves, better after WP-2 settles the exact structure), and A3 (one time-boxed search, low odds).
