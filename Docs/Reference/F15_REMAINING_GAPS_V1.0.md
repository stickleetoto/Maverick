# F-15 — Remaining Gaps at V1 Freeze

Only gaps that block implementation are listed here. Ranked follow-on work: `F15_POST_FREEZE_OPPORTUNITIES_V1.0.md`.

**Status keys:**
- **NOT PUBLICLY LOCATED** — searched, not found. Not a standing task.
- **UNAVAILABLE IN HELD SOURCES** — may exist; nothing held states it.
- **UNKNOWN** — no source addresses it.

---

## G1. NASA 836 `S` / `c̄` / coefficient reference `b` (and moment reference)

- **Why it matters.** These are the only reason the exact profile is invalid. Without them no coefficient, exact or research, can be dimensionalized on the exact path.
- **Public evidence.**
  - 608 ft² / 15.94 ft / 42.8 ft in the ARO10 lineage (`F15_FAMILY_SUPPORT`).
  - NF-15B 837's table (`INCOMPATIBLE`; 42.7-ft span).
  - The F-15A–D %MAC datum 508.1 / 191.33 in (family support; not promoted, see `F15_FINAL_STATE_V1.0.md` §4).
- **Already searched.**
  - Five named 836 reports and their references.
  - More than 15 further 836 NTRS reports.
  - Seven AFIT theses.
  - The NTRS and Internet Archive DTIC mirrors.
  - Details: `F15_NASA836_REFERENCE_GEOMETRY_AUDIT_V0.1.md`, `F15_A4172_SOURCE_LINEAGE_V0.1.md`.
- **What would close it.** A NASA 836 source naming its baseline aerodynamic model's reference dimensions, or naming the production database it uses together with that database's dimensions.
- **More searching worthwhile?** **Low yield.** Only a targeted look at DFRC F-15B simulation documentation, if any is public.

## G2. Exact numeric aerodynamic database

- **Why it matters.** Exact aero coefficients are zero by design. The research fit covers one flight condition only.
- **Public evidence.**
  - Baumann → Davison curve fits at M 0.6 / 20,000 ft, from the McAir ARO10 / 1988 "F-15 Aerobase" simulator tables, which are not public.
  - NASA 836 flight-identified derivative **trends** (Quiet Spike baseline flights: TM-2008-214634, TM-2012-215978). These are plots and validation targets, not a database.
- **MDC A4172 Part II:** **NOT PUBLICLY LOCATED.** No public document reproduces its tables.
- **What would close it.** The DFRC F-15B baseline aerodynamic model, or A4172 Part II / the Aerobase, in public form.
- **More searching worthwhile?** **No.** Treat it as not publicly available.

## G3. Exact FCS numeric schedules

- **Why it matters.** Every FCS stage is refused, so the law outputs neutral.
- **Public evidence.**
  - TM-72861 (preproduction F-15 No. 8): authorities, the 0.3 differential ratio, 1.8°/cm pedal gearing, ARI gradients, and RRAD/ARI **plots**.
  - Davison figure 27: roll-damper washout, zero at 20.2° α.
  - Four conflicting public surface-travel sets.
  - **NASA 836's own simplified pitch/roll/yaw control block diagrams** (NASA/TM-2009-214651 figs. 3–5; TM-2012-215978 figs. 3–5). Exact scope, **structure only, no gains**, apart from two Mach switch points (ARI out above M 1.5, roll-yaw crossfeed out above M 1.0). Identified at the V1 freeze. **Cross-checked in WP-2** (`F15_836_FCS_STRUCTURE_V1.0.md`): 9 of 11 stages confirmed as structure, and the two switches are in code. Gains remain unavailable, so this gap is unchanged.
- **DN-1180.01-238-458 Rev. D:** **NOT PUBLICLY LOCATED.** One public citation (Nolan 1992), with no numbers.
- **What would close it.** DN-1180 itself, or public AFFTC-TR-74-8 / AFFTC-TR-76-48, which are the sources behind TM-72861's figures. Those still give family scope, not 836.
- **More searching worthwhile?** **Once, narrowly:** check the distribution status of AFFTC-TR-74-8 and TR-76-48. Otherwise no.

## G4. NASA 836 engine sub-configuration and transfer equivalence

- **Why it matters.** This is what separates the PW-100(3) research characteristic from 836's thrust anchor. `MavF100PathSeparation` refuses to combine them.
- **Public evidence.**
  - 836 engines = F100-PW-100 (exact).
  - Re-engined to F100-PW-220E in 2014.
  - The P680063 chronology shows that a serial is not a configuration.
- **Gap.** The build level of 836's pre-2014 engines — (1), (2), 2-7/8 or production (3) — is **UNKNOWN**.
- **What would close it.** A NASA 836 or engine-program source naming the installed build, plus a source establishing same gas path **and** same control schedule. DEEC-era evidence contradicts the control-schedule equivalence.
- **More searching worthwhile?** **Low.**

## G5. Engine installation coordinates

- **Why it matters.** No thrust-line moment and no engine-out yaw. `geometryDeclared = false`.
- **Public evidence.** None for 836. Family engine data in the AFIT tables (Y offset ±25.5 in, Z 0.25 in, "nozzle pivot −20.219 ft") are Beck-lineage reproductions of unknown origin, not A4172 subject matter.
- **What would close it.** A NASA 836 or production F-15 structural/propulsion installation drawing or report in the FS/WL/BL datum.
- **More searching worthwhile?** **Medium, narrowly.** The family numbers exist; a primary source for them may exist too.

## G6. Dimensional thrust deck (Mach × altitude × power)

- **Why it matters.** Thrust is zero in every mode.
- **Public evidence.**
  - TP-1034 normalized characteristic at 7 conditions.
  - The 30,000 lbf channel scale, which is not the normalizer.
  - The ~22.4 klbf bound, TP-1034 only.
  - The 836 anchor, ≈23,500 lbf, approximate.
  - The design maximum net thrust is **UNAVAILABLE IN HELD SOURCES**. CP2903B is a restricted primary specification and must never be reconstructed.
- **What would close it.** A published PW-100(3) design maximum net thrust, **or** G4 closed so the 836 anchor may scale the characteristic at approximate precision. A power-lever convention must also be declared (blocker 2 of the deck).
- **More searching worthwhile?** **Low to medium.** A published figure elsewhere is possible; TP-1069/TP-1228 cannot supply it (no static point).

---

## Not blocking implementation (recorded only)

- The absolute CG station, via the family %MAC datum, is closed by decision (not promoted).
- Exact surface hard stops and actuator rates fall under G3.
- The exact aerodynamic source envelope falls under G2.
