# F/A-18 Missing Data and Open Questions (r0)

Each item has an ID, the configuration it affects, what it blocks, where to look, and a priority. **P1** blocks any implementation, **P2** blocks validation claims, **P3** is refinement.

"Where to look" lists public candidates only. Items that are not public are listed in §9 and are **not** search targets.

---

## 1. Aerodynamics

| ID | Question | Config | Blocks | Where to look | Pri |
|---|---|---|---|---|---|
| Q-A1 | **Are the f18bas / f18harv aerodynamic tables printed in the public TMs (appendices), or were they distributed separately? If separately, what is their release status?** | SIM-F18BAS, SIM-F18HARV | Every table-level model (Route T) | NASA-TM-107601, NASA-TM-110216 full text; their distribution pages | **P1: top blocker** |
| Q-A2 | Can the f18harv build-up be split into baseline airframe + TV increment + strake increment + flight-data updates? Which tables did the HARV flight updates change? | SIM-F18HARV | A TV-free, strake-free model that stays source-honest | NASA-TM-110216 build-up equations; NASA-CR-198247 | P1 |
| Q-A3 | Does Morelli's multivariate-orthogonal-function work print HARV model coefficients (CD, CL, Cm, Cz …), and over what α/β/Mach domain? | SIM-F18HARV (tunnel DB) | Route M (compact polynomial model, the F-16 Morelli analogue) | NTRS-19940020628 (public); JAIRCRAFT-1995-MORELLI-MOF (paywalled); AIAA 93-3636 | P1 |
| Q-A4 | What is the upstream identity of the wind-tunnel database (manufacturer reports, NASA sub-scale tests, rotary balance, forced oscillation)? | SIM-F18BAS | Lineage completeness; judging data quality | NASA-TM-107601 bibliography | P2 |
| Q-A5 | Where do the rate (damping) derivatives in the simulation come from: forced oscillation, rotary balance, or estimates? | SIM-F18HARV | Dynamic-derivative validity at high α | NASA-TM-107601/110216 text; NASA-CR-4582 vol. 2 | P2 |
| Q-A6 | What is the basis of the claimed Mach 0–2.0 database domain (transonic/supersonic data sources)? | SIM-F18HARV | Any use above the research envelope (M 0.2–0.7) | NASA-TM-110216 | P2 |
| Q-A7 | **Aerodynamic moment reference centre** (%MAC and fuselage station) | All | Any moment coefficient; CG shift corrections | NASA-TM-110216 / NASA-TM-107601 | **P1** |
| Q-A8 | Ground-effect, landing-gear and speed-brake increments present? | SIM-* | Takeoff/landing; speed-brake use | NASA-TM-107601/110216 | P3 |
| Q-A9 | How much do the stowed TV vanes and their aft-body fairings change baseline aerodynamics? | P2/P3 vs P1 | Using Phase 2/3 flight derivatives as "basic F/A-18" evidence | NASA-TP-1999-206573 (TV-HARV vs basic comparison); NASA-TM-4771 | P2 |

## 2. Mass properties and geometry

| ID | Question | Config | Blocks | Where to look | Pri |
|---|---|---|---|---|---|
| Q-M1 | **Ixz value and sign convention** | P1, P2/P3, SIM | Coupled roll/yaw dynamics | NASA-TM-4772 table; NASA-TM-110216 | **P1** |
| Q-M2 | Resolve inertia conflict C2 (TM-4772 set vs second set 22,632 / 174,246.3 / 189,336.4 / −2,131.8) | P2 | Choosing one mass state | Locate the true source of the second set (NASA-TP-3531? TM-110216? TP-3446?) | P1 |
| Q-M3 | Loading condition behind the TM-4772 mass table: which phases the 6,480 lbm internal fuel applies to, MAC leading-edge station, fuselage-station datum | P1, P2/P3 | Converting %MAC ↔ FS ↔ Unity body frame | NASA-TM-4772; NASA-TM-110216 | P1 |
| Q-M4 | The VDD assigns ~1,500 lb to "spin chute, emergency systems, and ballast" in the TV modification, yet the airframe already had a spin chute in Phase 1. What changed? | P1 → P2 | Understanding the mass delta; not a value blocker | NASA-WEB-HARV-VDD; NASA-TM-4772 | P3 |
| Q-M5 | Mass and CG variation with fuel state | All | Anything beyond fixed loading states | Probably not public in usable form | P3 |
| Q-G1 | Printed precision of reference span (37.4 vs 37.42 ft) | All | Cosmetic, but must be recorded exactly | NASA-TM-4772, NASA-TM-110216 | P2 |
| Q-G2 | Control-surface geometry and effective-deflection definitions: differential stabilator in roll, aileron droop, LEF/TEF schedules, rudder toe | SIM-F18HARV | Control-derivative mapping | NASA-TM-107601/110216 | P1 |

## 3. Actuators

| ID | Question | Config | Blocks | Where to look | Pri |
|---|---|---|---|---|---|
| Q-ACT1 | Attribute the HARV actuator position/rate table to one document and page | SIM-F18HARV | Moving FA18-ACT-* values to L4 | NASA-TM-110216; NASA-CR-198248; NASA-TM-110217 | P1 |
| Q-ACT2 | HARV **LEF/TEF rate limits** and **actuator bandwidths / time constants** for every surface | SIM-F18HARV | Actuator model completeness | NASA-TM-107601 ("first-order actuators with rate and position limiting"); NASA-TM-110216 | P1 |
| Q-ACT3 | Rudder rate conflict C1 (82 deg/s HARV sim vs 56 deg/s AAW) | SIM vs AAW | Knowing whether AAW data can ever cross-check HARV | Both tables in context (no-load vs loaded? different actuator?) | P3 |
| Q-ACT4 | TV vane position/rate limits and actuator dynamics | P2/P3 | TV-active profile only | NASA-TM-110228; NASA-TM-110216 | P3 (for TV profile) |

## 4. Flight control

| ID | Question | Config | Blocks | Where to look | Pri |
|---|---|---|---|---|---|
| Q-C1 | Does NASA-TM-107601 or NASA-TM-110216 print a **model of the basic (production-family) F/A-18 control law** with gains? If so, which PROM version, and is it a NASA approximation or a manufacturer release? | SIM-F18BAS, SIM-F18HARV | Any "basic F/A-18 FCS" behaviour | NASA-TM-107601/110216 full text | P1 |
| Q-C2 | Is NASA-TM-110217 complete enough to code (all gain schedules, filters, limiters, mode logic)? Which ANSER version does it describe relative to v152.0? | P3-ANSER | Stage A4 (research CL) | NASA-TM-110217; NASA-CR-198250 | P1 (for A4) |
| Q-C3 | Are NASA-1A longitudinal gains printed in NASA-TP-3446? Lat-dir gains in NASA-TP-1998-208465? | P2-TV | Intermediate research-CL option | Those reports | P2 |
| Q-C4 | Release/licence status of the NASA-2 Simulink/MATLAB submodels on the Dryden HARV "Working Documents" page | P2/P3 | Any reuse of those files (none until cleared) | NASA-WEB-HARV-VDD `Work/readme` | P2 |
| Q-C5 | TV mixer tables (vane-angle inversion) | P2/P3 | TV-active profile | NASA-TM-110228 | P3 |

## 5. Propulsion

| ID | Question | Config | Blocks | Where to look | Pri |
|---|---|---|---|---|---|
| Q-P1 | **Are installed F404 thrust tables (idle/intermediate/max vs Mach, altitude, PLA) printed in the HARV simulation documents or in NASA-TM-4240?** | SIM-*, ENG | Powered flight (a zero-thrust stub is the only honest fallback) | NASA-TM-110216, NASA-TM-107601, NASA-TM-4240 | **P1** |
| Q-P2 | Engine dynamics numbers: throttle rate limit, filter time constant(s), PLA ↔ power mapping, source of the idle 35° / intermediate 102° PLA values | ENG | Spool/lag model | NASA-TM-4240; NASA-TP-3001 | P1 |
| Q-P3 | Treatment of gross vs net thrust, ram drag, and inlet recovery in the sim engine model (installed or uninstalled?) | ENG | Correct load bookkeeping in `MavPropulsiveLoads` | NASA-TM-4240; NASA-TM-110216 | P1 |
| Q-P4 | TV axial thrust loss method and vane plume interference | P2/P3 | TV-active profile | NASA-TM-4240; NASA-TP-3531; NASA-TP-97-206539 | P3 |
| Q-P5 | Engine thrust line / installation geometry (position, cant, toe) | All | Propulsive moments | NASA-TM-110216 | P2 |

## 6. Validation data

| ID | Question | Config | Blocks | Where to look | Pri |
|---|---|---|---|---|---|
| Q-V1 | Are HARV flight time histories publicly downloadable, or only parameter lists and report figures? | P1–P3 | Open-loop trajectory validation against flight | NASA-WEB-HARV-VDD `Fdat/`; NTRS-19970014822 | P2 |
| Q-V2 | Any published trim tables (α, stabilator, thrust vs speed/altitude) for the HARV simulation or aircraft? | SIM-F18HARV | External trim validation (otherwise trim is only self-consistent) | NASA-TP-3446 run conditions; NASA-TM-110216 | P2 |
| Q-V3 | Digitization plan for Iliff & Wang and Napolitano derivative-vs-α plots (figure list, axes, uncertainty budget) | P1, P2 | Derivative-level validation | NASA-TM-4786, NASA-TP-97-206539, NASA-TP-1999-206573, NASA-CR-194838 | P2 |
| Q-V4 | Do the nonlinear batch responses in NASA-TP-3446 / NASA-TP-1998-208465 / NASA-CR-198250 state their initial conditions and mass states completely enough to reproduce headlessly? | SIM-F18HARV | Headless closed-loop trajectory validation | Those reports | P2 |
| Q-V5 | Longitudinal flight derivatives for the **basic** (Phase 1) configuration: none located yet (TM-4786 is lateral-directional only) | P1 | Phase-1 longitudinal validation | Phase 1 WVU work (NASA-CR-191216?); CP-10143 papers | P2 |

## 7. Identity and metadata to confirm

- **Authors unconfirmed:** NASA-TM-4773, NASA-TM-4783 (conflicting lists), NTRS-19990060322, NTRS-20110015950, NTRS-19900019226, NASA-TP-1998-208463, NTRS-19940020628, WVU-ETD-9553, NASA-TM-2005-213666, NTRS-20050204113, NTRS-20010039533, NTRS-19960000845, NTRS-19920066113, NTRS-20150006038, NTRS-19850011474.
- **NTRS ID or report number unconfirmed:** NASA-TM-104232 (19910012818 probable), NASA-TM-2005-213666, SRA-EPAD-EHA, NASA-TM-4433 (OCR-derived number), NASA-TM-4783 (NTRS ID).
- **Dates:** end of HARV Phase 1, start and end of Phase 2; timing of the V8.3.3 → V10.1 FCS upgrade.
- **FAST tail number** (853 per program pages; not confirmed in an indexed record).

## 8. Not searched in r0 (deliberately deferred)

- Spin-tunnel and free-flight model tests of the F/A-18 (NASA Langley). Relevant to high-alpha/spin but outside the r0 time box.
- F/A-18E/F abrupt-wing-stall program (NASA/Navy). A separate aircraft (FA18-CFG-EF), out of scope.
- Individual papers inside NASA-CP-10143 and the earlier High-Angle-of-Attack Technology conferences.
- AFIT / NPS theses using F/A-18 models. These are likely DERIVED_SIMULATOR sources; index them only when they cite primary data.

## 9. Not public: do not search further

| Item | Status | Consequence |
|---|---|---|
| Production F/A-18A/B/C/D FCS gains and schedules | Not public (NATOPS is Distribution C; manufacturer documents not located) | A production-FCS F/A-18 cannot be claimed. Use NASA research laws or a Maverick-owned law labelled as such |
| NAVAIR A1-F18AC-NFM-000 (NATOPS) | `NOT_PUBLIC_DISTRIBUTION_LIMITED` | Never use, even though copies circulate |
| McDonnell Aircraft aerodynamic data reports | `NOT_PUBLICLY_LOCATED` | Upstream identity only |
| GE F404 complete nonlinear dynamic engine model | `NOT_PUBLIC_PROPRIETARY` | Only NASA's simplified derivative (NASA-TM-4240) is usable |
