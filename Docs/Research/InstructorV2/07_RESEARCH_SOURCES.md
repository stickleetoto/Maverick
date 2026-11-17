# 07 — Research Sources and Evidence Quality

**Inspection method.** External sources were located and read on 2026-11-17 by three research sub-agents of this session (War Thunder; fighter FCS; FDM engines), plus two citation checks by the lead (Cooper–Harper, Wie & Barba). The lead read the sub-agents' structured reports, not every page directly. "Inspected: full" means the relevant sections were read (scanned NASA reports were rendered and read as page images); "abstract" means only an abstract or catalogue record; "none" means located but not read. Repository documents were read directly by the lead.

**Classes.** `PV` PRIMARY_VERIFIED · `SV` SECONDARY_VERIFIED (secondary source, or primary abstract/record only; community posts are verified *as statements*) · `EI` ENGINEERING_INFERENCE · `UV` UNVERIFIED · `MP` MAVERICK_PROPOSAL (used in docs 03, 06). Repository evidence: `REPO_VERIFIED` (code read at the cited line) · `REPO_DOC` (repository document).

**Rules applied:** no citation, page number, gain, limit or result is stated unless it appears in the source as read; figure readings from scans are marked moderate confidence; quotations are kept under 15 words.

---

## A. War Thunder / Gaijin (doc 01)

| Key | Title | Organization / author | Date | URL | Inspected | Location | Claim supported | Class |
|---|---|---|---|---|---|---|---|---|
| GAIJIN-PATENT-US8770979 | System and method for simulated aircraft control through desired direction of flight (US 8,770,979 B2) | Gaijin Entertainment; Yudintsev, Polyakov | prio. 2012-03-21; granted 2014-07-08 | https://patents.google.com/patent/US8770979B2/en | full (patent page) | processes 303A/303B, claims | look-ahead desired-direction control; single marker; no AoA/G limiting described; status displayed "Expired – Fee Related" (not a legal opinion) | PV |
| WT-NEWS-INSTRUCTOR-2016 | [Wiki Article] How the instructor works | Gaijin; comment by admin | 2016-12-01 | https://warthunder.com/en/news/4366-wiki-article-how-the-instructor-works | full (article + comments) | | Instructor converts all inputs; optionality depends on mode | PV (article) / SV (comments) |
| WT-NEWS-INSTRUCTOR-RU-2016 | Вики: новая статья, принципы работы инструктора | Gaijin; staff comments | 2016-11-17 | https://warthunder.com/ru/news/12566-viki-novaya-statya-principy-raboty-instruktora-ru | full | comments | pitch limited for stall/spin risk (helper); no nerfing mouse aim (admin) | PV (staff comments, low weight) |
| WT-WIKI-INSTRUCTOR | How the Instructor Works (wiki) | War Thunder Wiki | n/a | https://old-wiki.warthunder.com/Instructor/How_the_instructor_works | **none** (HTTP 401; new path 404) | | — | UV |
| STEAM-TANKFRIEND-2018 | Steam discussion paraphrasing the wiki article | Steam user | 2018-09-22 | https://steamcommunity.com/app/236390/discussions/0/1733213724902314471/ | full | | Instructor function list (AoA, trim, torque, G build-up, gear/flaps, ground, engine) | SV (paraphrase) |
| WT-DEVBLOG-HELI-2018 | [Development] Flight models and physics of helicopters | Gaijin | 2018-09-04 | https://warthunder.com/en/news/5742-development-flight-models-and-physics-of-war-thunder-helicopters-en | full | | mouse aim sets direction; Instructor executes; modes | PV |
| WT-WIKI-SIM | Simulator Battles | War Thunder Wiki (official) | n/a | https://wiki.warthunder.com/gamemode/simulator_battles | full | | no Instructor in Simulator air battles | PV |
| WT-WIKI-VTOL | VTOL | War Thunder Wiki (official) | n/a | https://wiki.warthunder.com/170-vtol | full | | Instructor holds altitude in hover | PV |
| WT-WIKI-CDK | CDK Reference | War Thunder Wiki (official) | n/a | https://wiki.warthunder.com/cdk/7439-cdk-reference | full (HTML grepped) | string IDs | control-mode strings; Instructor cancel-action hint | PV |
| WT-CL-2015-REVERT | Mouse Aim mechanics reverted | Gaijin changelog | 2015-03-26 | https://warthunder.com/en/game/changelog/current/428 | full | | reverted rework | PV |
| WT-CL-1.79 | Major Update 1.79 "Project X" | Gaijin changelog | 2018-06-12 | https://warthunder.com/en/game/changelog/current/830 | full | | per-aircraft instructor / mouse-aim tuning | PV |
| WT-CL-SONS-OF-ATTILA | "Sons of Attila" | Gaijin changelog | 2023-09-19 | https://warthunder.com/en/game/changelog/current/1536 | full | | F-16A Instructor 11 G (mouse/simplified); SAS damper limiters | PV |
| WT-CL-2.35.1.40 | Update 2.35.1.40 | Gaijin changelog | 2024-04-10 | https://warthunder.com/en/game/changelog/current/1614 | full | | cursor matched to aiming reticle (previously longitudinal axis) | PV |
| WT-ITS-FIXED-90 | It's Fixed! No. 90 | Gaijin | 2024-04-10 | https://warthunder.com/en/news/8846-its-fixed-no90-en | full | | rationale: offset sight drift in rolls | PV |
| WT-CL-DANCE-OF-DRAGONS | "Dance of Dragons" | Gaijin changelog | 2024-09-10 | https://warthunder.com/en/game/changelog/current/1660 | full | | F-104 Instructor 7.5 G with full fuel; MiG-23 roll sway | PV |
| WT-CL-HORNETS-STING | "Hornet's Sting" | Gaijin changelog | 2025-03-18 | https://warthunder.com/en/game/changelog/current/1716 | full | | toggle disabling critical-AoA limit in mouse aim / simplified | PV |
| WT-CL-2.47.0.91 | Update 2.47.0.91 | Gaijin changelog | 2025-08-06 | https://warthunder.com/en/game/changelog/current/1769 | full | | Harrier AoA/overload raised (no Instructor wording) | PV |
| WT-CL-2.59.0.11 | Update 2.59.0.11 | Gaijin changelog | 2026-09-17 | https://warthunder.com/en/game/changelog/current/1895 | full | | MiG-35/29KR Instructor behaviour at high altitude/speed | PV |
| WT-FORUM-INCONSISTENT-LIMITS-2025 | Inconsistent Instructor Limits in ARB | official forum, users | 2025-10-08 | https://forum.warthunder.com/t/inconsistent-instructor-limits-in-arb/273517 | page 1 | | per-aircraft limit variation | SV |
| WT-FORUM-COORDINATION-2024 | Aircraft coordination in mouse aim | official forum, users | 2024-01-24 | https://forum.warthunder.com/t/can-we-talk-about-aircraft-coordination-in-wt-on-mouse-aim/69953 | full | | slip/skid in mouse-aim turns | SV |
| WT-FORUM-WINGS-LEVEL-2025 | Maneuver mode forcing wings level below 800 km/h | official forum, users | 2025-12-27 | https://forum.warthunder.com/t/jets-maneuver-mode-forcing-wings-level-below-800kmh-mouse-aim/295742 | full | | forced wings-level | SV |
| WT-FORUM-ARCADE-RUDDER-2024 | Arcade jets lose control when turning | official forum, users | 2024-12-07 | https://forum.warthunder.com/t/in-arcade-air-battles-many-jet-fighters-lose-control-when-turning/192643 | full | | rudder hang 400–500 km/h | SV |
| DAGOR-ENGINE-REPO | DagorEngine | Gaijin Games Kft | latest commit seen 2026-11-07 | https://github.com/GaijinEntertainment/DagorEngine | README, tree, files, code search | `gamePhys/common/polares.*`, `daGame/netAutopilot.h` | BSD-3; no aircraft FM / Instructor / mouse aim; autopilot stub with missing header | PV (code search not exhaustive) |
| MOUSEFLIGHT | MouseFlight | Brian Hernandez (MIT) | v1.3, 2019-05-14 | https://github.com/brihernandez/MouseFlight | code read (`MouseFlightController.cs`, `Plane.cs`) | `RunAutopilot` | P-control on body-frame target, roll blend by angle-off, direct torque; behind-target unhandled | SV |
| KSP-MOUSEAIMFLIGHT | MouseAimFlight (KSP mod) | ferram4, tetryds | 2016-04-29 | https://github.com/ferram4/MouseAimFlight | code read | `NormalFlight.cs`, `AdaptivePID.cs` | up-weighted roll target; PIDs with q̄ scaling; **inconsistent licence — ideas only** | SV |
| (others) | MouseAimFlightRedux; Overbuilt PR #40; Warbirds.io dev log; World of Warplanes mouse controls | various | 2026 / undated | see doc 01 §5.3 | README / page | | comparable designs, low weight; War Thunder claims in these UV | SV / UV |

## B. Fighter flight control (doc 02)

| Key | Title | Authors / org | Year | URL / DOI | Inspected | Location | Claim supported | Class |
|---|---|---|---|---|---|---|---|---|
| NASA-TP-1538 | Simulator Study of Stall/Post-Stall Characteristics of a Fighter Airplane With Relaxed Longitudinal Static Stability | Nguyen, Ogburn, Gilbert, Kibler, Brown, Deal / NASA Langley | 1979 | https://ntrs.nasa.gov/citations/19800005879 | full (relevant pages, scan rendered) | pp. 13–15, 18–19; App. A pp. 34–35; Figs. 62–65 | simulated F-16 CAS: n command, α/g limiter (≈25° at 1 g), 308 °/s roll command, r − pα damper, ARI and LCDP, kinematic and inertia coupling, control system B | PV (individual figure gains: moderate confidence) |
| NASA-TN-D-7941 | Development of a Remote Digital Augmentation System … (F-15 3/8-scale RPRV) | Edwards, Deets / NASA FRC | 1975 | https://ntrs.nasa.gov/api/citations/19750012221/downloads/19750012221.pdf | full (text layer) | pp. 13, 21–28, 32–33; Table 3; Figs. 12–15 | early F-15 MCS + CAS: modified C\*, stall inhibitor, roll CAS limited vs α, r − αp yaw damping, ARI, 42 °/s downmode | PV (Table 3 values are scaled-model) |
| NASA-TM-2009-214651 / TM-2012-215978 | F-15B Quiet Spike stability and control reports | McWherter, Moua, Gera, Cox / NASA | 2009, 2012 | via repository record | **read by repository**, not by this research | PDF p. 30/31; p. 22/23 | NASA 836 FCS structure without numbers | REPO_DOC (`Docs/Reference/F15_836_FCS_STRUCTURE_V1.0.md`) |
| NASA-IFCS-BOSWORTH | Flight Test Results from the NF-15B IFCS Project | Bosworth, Williams-Hayes / NASA Dryden | ~2007–09 | https://ntrs.nasa.gov/api/citations/20100025868/downloads/20100025868.pdf | full (text) | §B, Eqs. 1–2 | DI + model following; reference models; research aircraft | PV |
| ENNS-MCT-1994 | Multi-Application Controls (4th High Alpha Conference) | Enns, Bugajski, Carter, Antoniewicz | 1994 | https://ntrs.nasa.gov/api/citations/19950007835/downloads/19950007835.pdf | full (slides) | slides 4, 6, 8, 17–19 | controlled variables (velocity-vector roll, C\*-type pitch), limits, pitch priority, daisy chain, YF-22/F-16 in program | PV |
| ENNS-1994 | Dynamic inversion: an evolving methodology for flight control design | Enns, Bugajski, Hendrick, Stein | 1994 | DOI 10.1080/00207179408923070 | abstract | | NDI methodology | SV |
| SNELL-1992 | Nonlinear inversion flight control for a supermaneuverable aircraft | Snell, Enns, Garrard | 1990 (conf.) / 1992 (JGCD) | https://ntrs.nasa.gov/citations/19900060606 ; DOI 10.2514/3.20932 | abstract | | time-scale separation | SV |
| SIEBERLING-2010 | Robust Flight Control Using Incremental Nonlinear Dynamic Inversion and Angular Acceleration Prediction | Sieberling, Chu, Mulder | 2010 | DOI 10.2514/1.49978 | full (repository copy) | Eqs. 20–31; §VIII | NDI and INDI equations; q̄ scaling of control moment; simulation-only validation | PV |
| F35-NIXON-2004 | Flight Control Law Development for the F-35 Joint Strike Fighter (slides) | D. Nixon / Lockheed Martin | 2004 | MathWorks-hosted PDF | full | slides 7–9 | DI structure `u = (CB)⁻¹(ċv_des − CAx)`, roll regulator | PV |
| F35-HARRIS-2018 / X35-WALKER-2002 | F-35 FCL design … ; X-35B STOVL FCL design | Harris; Walker, Allen | 2018; 2002 | — | none | | — | UV |
| BODSON-2002 | Evaluation of Optimization Methods for Control Allocation | Bodson | 2002 | JGCD 25(4):703–711 | full (author-hosted version) | §2–3, Conclusions | pseudo-inverse limits; LP methods cost and benefit | PV |
| DURHAM-1993 | Constrained Control Allocation | Durham | 1993 | JGCD 16(4):717–725 | none (citations) | | attainable moment subset | SV |
| HARKEGARD-2002 | Efficient active set algorithms for solving constrained least squares problems in aircraft control allocation | Härkegård | 2002 | IEEE CDC | abstract | | WLS allocation | SV |
| MIL-F-8785C | Flying Qualities of Piloted Airplanes | USAF | 1980 | everyspec.com | full (scan) | §3.2.2.1, Table IV, Fig. 1, Tables VI–VII | CAP, ζ_sp, Dutch roll, τ_R requirements | PV (inactive for new design) |
| MIL-STD-1797A | Flying Qualities of Piloted Aircraft | DoD | — | — | none | | — | UV |
| TOBIE-CSTAR-1966 | C\* criterion paper | Tobie, Elliott, Malcom | 1966 | not located | none | | C\* definition with V_co ≈ 400 ft/s | UV |
| COOPER-HARPER-1969 | The Use of Pilot Rating in the Evaluation of Aircraft Handling Qualities (NASA TN D-5153) | Cooper, Harper | 1969 | https://ntrs.nasa.gov/citations/19690013177 | record + search summary | | pilot rating scale | SV |
| NACA-TN-1627 | Effect of Steady Rolling on Longitudinal and Directional Stability | W. H. Phillips / NACA | 1948 | https://ntrs.nasa.gov/citations/19930082293 | summary page | Summary p. 1 | roll-coupling criterion | PV |
| KLYDE-MITCHELL-RL-PIO | Investigating the Role of Rate Limiting in Pilot-Induced Oscillations | Klyde, Mitchell | ~2003 | sierrafoot.org/x-15/documents/AIAA20035463.pdf | full (author-hosted copy) | abstract, intro, mitigation | rate-limit phase lag; PIO cases; phase-compensated limiters | PV |
| NASA-CR-198055 | Flight Evaluation … Rate-Limited Ailerons | Deppe, Chalk, Shafer | 1996 | https://ntrs.nasa.gov/api/citations/19970004404/downloads/19970004404.pdf | partial | intro, conclusions | "Rate Limit Concept" results | PV |
| F22-SETP-1999 | F-22 Flight Test Program Update | Rainey, Kohn / USAF | 1999 | https://man.fas.org/dod-101/sys/ac/docs/f-22-emd-paper.htm | full (fetched copy) | | ARI options, nose kick, single-engine laws | PV |
| DROSTE-WALKER-F16 | The General Dynamics Case Study on the F-16 Fly-by-Wire Flight Control System | Droste, Walker / AIAA | 1980s (dates conflict) | catalogues only | table of contents | | chapter on AOA/g limiting exists; content unknown | UV |
| STEVENS-LEWIS-ACS | Aircraft Control and Simulation | Stevens, Lewis (, Johnson) | 1992 / 2015 | book | none | | standard equations; F-16 model based on TP-1538 data (secondary) | SV |
| WIE-BARBA-1985 | Quaternion feedback for spacecraft large angle maneuvers | Wie, Barba | 1985 | JGCD 8(3):360–365 | index records; authors via citing paper | | quaternion feedback with short-way sign | SV |

## C. FDM engines and Unity (doc 04)

| Key | Title | Org / author | Date | URL | Inspected | Claim supported | Class |
|---|---|---|---|---|---|---|---|
| JSBSIM-REPO | JSBSim (README, COPYING, `FGFDMExec`, `FGPropagate`, `FGAccelerations`, `FGAerodynamics.h`, `FGTurbine.h`, `FGActuator.h`, `aircraft/f16/f16.xml`, UnrealEngine plugin) | JSBSim-Team | master, 2026 | https://github.com/JSBSim-Team/jsbsim | full (files) | LGPL-2.1; 120 Hz; execution order; integrators and doc/code default conflict; ω×Jω; FCS components; no Unity plugin; F-16 file GPL-tagged, cites TP-1538 | PV |
| JSBSIM-PYPI | JSBSim on PyPI | PyPI | 2026-10-16 | https://pypi.org/project/JSBSim | full | v1.3.3, LGPLv2+, wheels | PV |
| JSBSIM-DISC-530 | Execution speed discussion | GitHub users/maintainers | Dec 2021 | https://github.com/JSBSim-Team/jsbsim/discussions/530 | full | anecdotal speed; property-access cost | SV |
| SPEC-748 | SPEC CPU 2026 748.flightdm_r | SPEC | 2026 | open.spec.org (benchmark docs) | full | JSBSim-based; floating-point tolerance across compilers | PV |
| NASA-TM-2015-218675 | Check-Cases for Verification of 6-Degree-of-Freedom Flight Vehicle Simulations, Vol. I | Murri, Jackson, Shelton / NASA NESC | Jan 2015 | https://ntrs.nasa.gov/citations/20150001263 | full (text extracted) | seven tools incl. JSBSim; F-16 DAVE-ML cases; integration-method differences | PV (Vol. II unread → F-16 data provenance UV) |
| NESC-FLIGHTSIM-2015 | NESC 6-DOF check-case site | NASA NESC Academy | 2015 | https://nescacademy.nasa.gov/flightsim/2015 | full | F-16 cases 11–16, S-119 format | PV |
| YASIM-SRC | YASim `Airplane.cpp` | A. Ross / FlightGear | 2001– | gitlab.com/flightgear/flightgear (YASim) | full (file) | GPL-2.0-or-later; two-point solver | PV |
| AEROBENCH | AeroBenchVVPython | S. Bak et al. | v2 | https://github.com/stanleybak/AeroBenchVVPython | full (licence, docstrings) | GPL-3.0; S&L/Morelli F-16; LQR; GCAS modes; RK45 | PV (summary only; nothing copied) |
| NASA-TM-2003-212145 | A Collection of Nonlinear Aircraft Simulations in MATLAB | Garza, Morelli / NASA LaRC | Jan 2003 | https://ntrs.nasa.gov/api/citations/20030013626/downloads/20030013626.pdf | full (text) | F-16 from TP-1538; Euler/RK2/RK4/AB3 | PV |
| (others) | PyFME; ArduPilot SITL+JSBSim; px4-jsbsim-bridge; Gazebo LiftDrag; AirSim; gym-jsbsim; CloseAirCombat | various | various | see doc 04 §3 | repo pages / licences | licences and scope | PV/SV |
| UNITY-RB-MAXANGVEL-6000.3 | Rigidbody.maxAngularVelocity | Unity | 6000.3 | docs.unity.com (6000.3 script reference) | full | default 50 rad/s | PV |
| UNITY-RB-MAXANGVEL-2021.3 | Rigidbody.maxAngularVelocity | Unity | 2021.3 | docs.unity3d.com/2021.3 | full | default 7 rad/s | PV |
| UNITY-RB-MANUAL | Rigidbody component reference | Unity | current (6.x) | docs.unity.com manual | full | angular damping 0.05; interpolation; automatic tensor | PV (not separately confirmed for 6000.3) |
| UNITY-PHYSICS-SETTINGS | Physics project settings; `Physics.Simulate`; `Time.fixedDeltaTime` | Unity | 6000.3 | docs.unity.com | full | Enhanced Determinism; simulation modes; script stepping caveats; fixed-step quantization | PV |
| PHYSX-5-GUIDE | PhysX 5.4.1 `PxRigidBodyFlag`; 5.5.1 Rigid Body Dynamics guide | NVIDIA | 5.4.1 / 5.5.1 | nvidia-omniverse.github.io/PhysX | full | gyroscopic flag exists; determinism conditions; force reset | PV |
| NVIDIA-PHYSX-FORUM-2021 | PhysX 4 aerodynamic stability thread | A. Moravanszky (PhysX developer) | 2021-01-04 | forums.developer.nvidia.com | full | external forces integrated explicitly | SV |
| LGPL-2.1 | GNU LGPL v2.1 | FSF | 1999 | https://www.gnu.org/licenses/old-licenses/lgpl-2.1.html | full | §6 relink / shared-library options | PV |
| ML-AGENTS | ML-Agents CLI and docs | Unity | develop | https://github.com/Unity-Technologies/ml-agents | full | headless flags and defaults | PV |
| NASA-CR-2877 | Real-time simulation integration study (RSRA) | Howe, Fogarty | 1977 | https://ntrs.nasa.gov/api/citations/19770025808/downloads/19770025808.pdf | full (text) | ~50 steps/cycle with AB-2 for ~1 % accuracy | PV |
| NASA-TP-2016-218956 | Armstrong simulation architecture | Clarke, Lintereur, Bahm | 2016 | https://ntrs.nasa.gov/api/citations/20160002944/ | full (text) | modified RK2; RK vs multistep stability | PV |

## D. Repository evidence (read directly)

| Document / file | Used for |
|---|---|
| `Docs/PROJECT_FREEZE_2026.md` | stable baseline, rejected R2/gameplay experiments |
| `Docs/FlightDynamics/REALISTIC_MOUSE_INSTRUCTOR_V2_CLAUDE_HANDOFF.md` (dev branch `5776617`) | product definition and edit boundaries for the development agent |
| `Assets/MaverickFresh/Scripts/FlightDynamics/README.md` | pipeline, readiness, axis conventions, F-16 envelope, propulsion status |
| `Docs/FlightDynamics/F16_LIVE_FDM_ARCHITECTURE_V0.1.md` | phase C0/C1/C2 plan; instructor above the control law |
| `Docs/Reference/F15_PILOT_CONTROLLED_V1.md`, `…_V2.md` | F-15 rig domain, trim, V2 law, gains, measurements |
| `Docs/Reference/F15_836_FCS_STRUCTURE_V1.0.md` | F-15B 836 CAS structure |
| `Docs/Validation/FDM_VALIDATION_BASELINE_V1.md` | validation rules and counts |
| `Docs/PHASE5CR_UNITY_TEST_REPORT.md` (U5CR-008 note) | PhysX omits ω × Iω |
| `Docs/Research/Aerospace/Aircraft/F16/F16_FCS_SOURCE_GRAPH.md`, `…/F22/PUBLIC_SOURCE_FEASIBILITY.md` | existing source-library verdicts |
| Code: `MavInstructorController.cs`, `MavMouseFlightRig.cs`, `MavMouseFlightJet.cs`, `Core/MavPilotCommandSource.cs`, `Core/MavMouseInstructorPilotCommandSource.cs`, `Core/MavKeyboardPilotCommandSource.cs`, `Core/MavFlightControlLawBase.cs`, `Core/MavControlLawProtections.cs`, `Core/MavSixDoFBody.cs`, `Core/MavFlightDynamicsTypes.cs`, `Core/MavAttitudeMath.cs`, `F15/MavF15PilotControlLawV2.cs`, `F15/MavF15PilotControlGainsV2.cs`, `F15/MavF15PilotControlledRig.cs`, `F15/MavF15ControlActuator.cs`, `F16/MavF16ControlLawV01.cs`, `Validation/MavF16ReferenceFlightScenarios.cs` | doc 05 findings (line references there) |

---

## E. Disagreements and how they were resolved

| Topic | Disagreement | Resolution |
|---|---|---|
| Kinematic β̇ sign | The lead's brief to the FCS sub-agent wrote `β̇ ≈ −p sin α + r cos α`; TP-1538 gives `β̇ ≈ p sin α − r cos α` | TP-1538 is correct for the conventional body axes (+X fwd, +Y right, +Z down); doc 03 uses TP-1538's form; repository V2 doc's "β̇ per p = +sin α" agrees |
| JSBSim default integrators | header comment vs constructor code | code is authoritative for master; read the properties at run time (doc 04 §2) |
| F-16 "25° / 9 g" | widely quoted production limits vs sources | TP-1538 simulated system: ≈25° at 1 g, +8 g incremental. Production values UV |
| War Thunder aimed axis | community "nose on cursor" vs official changelog | official: longitudinal axis until 2024-04-10, aiming reticle since (W9) |
| Droste & Walker dates | 1980 / 1985 / 1998 in catalogues | unresolved; content not seen |
| Unity `maxAngularVelocity` | 7 rad/s (2021.3) vs 50 rad/s (6000.3) | version change; Maverick targets 6000.3 → 50 |
| PhysX gyroscopic forces | PhysX 5 has a flag; Unity exposes none | repository torque-free probe shows the term absent in Maverick's build |

## F. Could not verify (consolidated)

1. Production F-16 (any block) and F-15 FCS gains, limiter schedules, actuator rates — none located (consistent with the repository source library).
2. Droste & Walker case-study content; MIL-STD-1797A text; Tobie et al. C\* paper; Harris 2018; Walker & Allen 2002.
3. Any F-22 control-law architecture detail beyond the 1999 SETP update.
4. The official War Thunder wiki "How the instructor works" text (unreachable); any general Instructor limit formula; low-speed cut-off; ground-avoidance algorithm.
5. Whether DagorEngine `Polares` is War Thunder's shipped code.
6. The patent's legal status beyond what Google Patents displays.
7. NASA 6DoF check-case Vol. II (F-16 DAVE-ML data provenance).
8. Unity 6000.3-specific defaults for linear damping, fixed timestep and maximum allowed timestep (seen only for other versions).
9. JSBSim performance benchmarks (anecdotes only).
10. Individual TP-1538 figure gains (scan readings, moderate confidence) — re-read before any use.
