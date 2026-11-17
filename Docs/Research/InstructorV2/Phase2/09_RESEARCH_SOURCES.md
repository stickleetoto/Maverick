# 09 — Research Sources, Evidence Quality and Acquisition Status

**Method.** External sources were read on 2026-11-17 by four research sub-agents of this session (F-16 aero/propulsion; NASA NESC; F-15/F-22/HARV; control stability and identification), each with written verification rules. The lead researcher independently read the primary sources behind the highest-impact conclusions:

| Lead-verified source | File / identity | Pages read by the lead |
|---|---|---|
| NASA TP-1538 (Nguyen et al., Dec 1979) | NTRS 19800005879; 233-page scan; SHA-256 `aae0ece64474291368c0b4c816d3ab327c6100329e6eb030c2f4545d0913feb3` (**identical to the repository manifest**) | PDF p. 15 (report p. 9: data basis, M 0.1–0.2, α −20…90°, β ±30°, no Mach/Re/aeroelastic effects, 1/32 s integration); pp. 43–46 (report 37–40: build-up equations, engine simulation, 216.9 kg·m²/s); p. 49 (Table I); p. 51 (Table III, Cx at δh −25°); p. 99 (Table VI); contact sheets of pp. 46–65 and 86–98 to locate tables |
| Morelli, "Global Nonlinear Parametric Modeling with Application to F-16 Aerodynamics" | NTRS 20040110310; 6 pages; text layer | full text: §3, Eqs. 12–18, Tables 1–2, references |
| Morelli, "Real-Time Parameter Estimation in the Frequency Domain", AIAA-99-4043 | NTRS 20040087105; 11 pages; text layer | title page; p. 4 band/spacing statement; collinearity statement; Eqs. 15–17 |
| Repository records | `Docs/Reference/F16_TP1538_*`, `Docs/Research/Aerospace/**`, `Docs/Reference/F15_*`, F-15 V1/V2 records, code listed in docs 01–02, 05 | as cited in each document |
| Development branch (read only) | local commit `90add24` on `claude/realistic-mouse-instructor-v2` (unpushed at the time of reading) | `MavMouseAimGuidance.cs` configuration and rationale; runner scenario list |

Classes: `PRIMARY_VERIFIED` (PV), `SECONDARY_VERIFIED` (SV), `ENGINEERING_INFERENCE` (EI), `UNVERIFIED` (UV), `MAVERICK_PROPOSAL` (MP). "Inspected by" is **L** (lead) or **S** (sub-agent).

---

## A. F-16 aerodynamics and propulsion (docs 01, 02)

| Source | Org / authors | Year | URL / DOI | Location | Configuration | Claim supported | By | Class | Limitations |
|---|---|---|---|---|---|---|---|---|---|
| NASA TP-1538 | Nguyen, Ogburn, Gilbert, Kibler, Brown, Deal / NASA Langley | 1979 | https://ntrs.nasa.gov/citations/19800005879 | pp. as above | `F16-CFG-NASA-REF-TP1538` | data basis and range; build-up; Table I; Table VI; engine description and angular momentum | L | PV | scanned; tables rotated; no engine variant named |
| same | same | same | same | PDF p. 38 (report p. 32) | same | results valid only below about Mach 0.6 | S | PV | lead did not re-read this page |
| same | same | same | same | PDF p. 97 (Clp vs α to 90°), p. 70 (Cm,lef to α 45°), p. 225 (fig. 66) | same | dynamic tables to α 90°; LEF increments to α 45° | S | PV | — |
| Morelli 1998 | E. A. Morelli / NASA Langley | 1998 (NTRS dated 1997) | https://ntrs.nasa.gov/citations/20040110310 | §3; Tables 1–3 | `F16-CFG-SIM-MORELLI1998` | fitted to the Stevens & Lewis simplified data; ranges; structure; < 10 % doublet agreement | L | PV | venue (ACC 1998) only secondary |
| NASA/TM-2003-212145 | Garza & Morelli / NASA Langley | 2003 | https://ntrs.nasa.gov/citations/20030013626 | rpt pp. 29–33 | TP-1538 simulation packaging | table-lookup aero (S&L lineage), α −10…45°; no Mach in aero; engine gearing/lag; heng 160 slug·ft²/s along +X; thrust M 0–1; engine named only generically | S | PV | lead did not re-read; axis statement to re-check before implementation |
| Stevens & Lewis, *Aircraft Control and Simulation* App. A | Stevens, Lewis | 1992 | book | pp. 584–92 (per Morelli) | textbook | the simplified dataset | — | SV (via Morelli) | not inspected |
| NASA CR-3053; CR-158922 Vol. 2 | Smith et al.; Smith & Bhateley | 1979; 1978 | NTRS 19790019972; 19790003895 | text/TOC | YF-16/developmental | transonic strake tests; plots | S | SV | developmental configurations |
| NASA TP-3355 | Fox & Forrest | 1993 | NTRS 19930022544 | pp. 6–14 | F-16C 1/15-scale vs Falcon 21 | M 1.6–2.16 scope; tables only in microfiche supplement | S | PV (scope) | data not accessible |
| ICAS 2008-286 | Hendrick, Bourdiaudhy, Herbiet | 2008 | icas.org archive | pp. 1–4 | F-16A/B MLU, F100 (dash not stated) | installed thrust/TSFC deck, subsonic, plots | S | PV | not usable as data |
| NASA CR-179447 | Evans | 1991 | NTRS 19910010772 | rpt p. 2 | F100-PW-200 DEEC status deck | percent-change results only | S | PV | no absolute tables |
| DTIC ADA327869 (WL-TM-97-3065) | Kay, Ralston, Lash (Bihrle) | 1997 | DTIC | — | VISTA NF-16D | α −80…90 model (abstract) | — | UV | HTTP 403 |
| AIAA-2018-0525 | Knapp, Berger, Tischler, Cotting | 2018 | AIAA | — | VISTA/X-62A | identified model | — | UV | paywalled |

## B. NASA 6-DoF check cases (doc 07)

| Source | Org / authors | Year | URL | Location | Claim supported | By | Class | Limitations |
|---|---|---|---|---|---|---|---|---|
| NESC check-case site and case pages | NASA NESC | 2015 (page updated 2025) | https://nescacademy.nasa.gov/flightsim/2015 | landing, Bodies, Datum, Gravity, Atmosphere, Output specs, acc01–acc17, errata | case list, models, ICs, files, mass 637.26 slug on the Bodies page | S | PV | no licence text |
| NASA/TM-2015-218675 Vol I | Murri, Jackson, Shelton / NESC | 2015 | https://ntrs.nasa.gov/citations/20150001263 | §4–12, pp. 8–24 | models, tools, no tolerances, findings | S | PV | — |
| NASA/TM-2015-218675 Vol II | same | 2015 | https://ntrs.nasa.gov/citations/20150001264 | — | constants, F-16 description, match metrics | — | **UV** | > 10 MB; not retrieved |
| AIAA 2015-1810 | Jackson, Madden et al. | 2015 | NESC site, Reports | pp. 3–34 | case 11 open loop; 4 ft / 666 ft spread; moments MRC vs CM; tool list | S | PV | AIAA copyright |
| AIAA 2013-5071 | Jackson, Shelton et al. | 2013 | NESC site, Reports | pp. 5, 12–22 | free choice of integrator; geodesy | S | PV (partial) | — |
| F16_package README v6 | E. B. Jackson / NASA Langley | 2013 | via nasa/simupy-flight | whole | aero via Garza & Morelli; propulsion/inertia from Stevens & Lewis 2nd ed.; controller; envelope | S | SV | not compared with the NESC zip |
| nasa/simupy-flight | NASA Ames | 2021 | https://github.com/nasa/simupy-flight | LICENSE; case scripts; CSV headers | NOSA 1.3; case drive flags; 0.1 s output | S | PV | data provenance "accessed summer 2020" |
| Repository library F-16 NESC verification | Maverick | R2 | `Docs/Research/Aerospace/Aircraft/F16/KNOWN_DATA.md` | value table | file-line verification of DAVE-ML values | L (read record) | REPO_DOC | — |

## C. F-15, F/A-18, F-22 (docs 03, 04)

| Source | Org / authors | Year | URL | Configuration | Claim supported | By | Class |
|---|---|---|---|---|---|---|---|
| AFIT theses (McDonnell 90D-16; Fero 91D-4; Davison 92M-01) | AFIT | 1990–1992 | scholar.afit.edu/etd/7852, /7499, /7488 | F-15B (per abstracts) | lineage variant; wing-rock study | S | SV (abstracts) |
| Baumann thesis AFIT/GAE/ENY/89D-01 (ADA217366) | AFIT | 1989? | — | — | — | — | UV (not found; DTIC 403) |
| NASA/TM-2003-212027 | Smith, Moes, Morelli | 2003 | https://ntrs.nasa.gov/citations/20030079970 | NF-15B 837 (canards, PW-229) | reference set 608 / 15.94 / 42.7; derivatives in figures | S | PV |
| NASA TM-X-62360 | Petroff, Scher, Cohen | 1974 | https://ntrs.nasa.gov/citations/19780002076 | 0.075-scale early F-15 | α 0–90, plots; model geometry | S | PV |
| NASA CR-3479 / CR-3478 | Barnhart | 1982 | NTRS 19820014339 / 19820016292 | 1/12-scale F-15 | rotary balance α 8–90; plots | S | PV / SV |
| NASA TM-86042 | Haering, Burcham | 1984 | https://ntrs.nasa.gov/citations/19860014116 | F-15 HIDEC; F100 EMD | drag polars (plots, from AFFTC-TR-77-7); EMD thrust and fuel flow plots | S | PV |
| NASA TP-1373, TP-1482, TP-1782, TP-1069, TP-1228 | Kurtenbach; Burcham; Biesiadny et al. | 1978–1981 | NTRS | prototype F100-PW-100 | normalised/percent-error results; normalising constants | S | PV |
| NASA/TM-1998-206560 | Conners, Sims | 1998 | https://ntrs.nasa.gov/citations/19980228163 | F-15 ACTIVE, F100-PW-229 | in-flight thrust measurement | S | SV (abstract) |
| NASA TM-110216; TM-107601 | Strickland et al.; Buttrill et al. | 1996; 1992 | NTRS 19960027892; 19920024293 | F/A-18 HARV / f18bas | domains; no printed aero arrays | S | PV |
| Chakraborty, Seiler, Balas, JGCD | — | 2011 | UMN-hosted PDF | F/A-18 (HARV-derived fits, α 0–60) | printed polynomial coefficients | S | PV |
| AIAA 2000-1695 | Moses, Huttsell | 2000 | https://ntrs.nasa.gov/citations/20000052124 | early F-22 13.3 % model | buffet only; no aero coefficients | S | PV |

## D. Control stability and identification (docs 05, 06)

| Source | Authors | Year | URL | Location | Claim supported | By | Class |
|---|---|---|---|---|---|---|---|
| Real-Time Parameter Estimation in the Frequency Domain (AIAA-99-4043) | Morelli | 1999 | https://ntrs.nasa.gov/api/citations/20040087105/downloads/20040087105.pdf | Eqs. 9–20; p. 4 | FDEE; recursive DFT; 0.01–1.5 Hz; 0.04 Hz over 0.1–1.5 Hz; collinearity under feedback | **L** + S | PV |
| Practical Aspects of the Frequency Domain Approach | Morelli, Grauer | 2019/2020 | https://ntrs.nasa.gov/api/citations/20190001862/downloads/20190001862.pdf | Eqs. 20–22, 31, 84–91; §IV.C | derivative transform; coherence caveat; output error | S | PV |
| Efficient Methods for Aircraft Frequency Response Estimation at NASA | Grauer | ~2022 | https://ntrs.nasa.gov/citations/20210026474 | Eqs. 3–9, §§4–5 | multisines; direct and joint input-output; broken loops | S | PV |
| Real-Time Frequency Response Estimation … | Grauer, Heeg, Morelli | ~2012 | https://ntrs.nasa.gov/citations/20120014504 | §II.E | F-16 simulation multisine example | S | PV |
| Identification of Bare-Airframe Dynamics from Closed-Loop Data … | Grauer, Boucher | ~2020 | https://ntrs.nasa.gov/citations/20200002414 | Eqs. 1–4 | RPF; ≥ 2 cycles | S | PV |
| Flight Test Maneuvers for Efficient Aerodynamic Modeling | Morelli | 2011 | https://ntrs.nasa.gov/citations/20110014634 | Eq. 2 | orthogonal multisines | S | PV |
| Tailored Excitation for Multivariable Stability-Margin Measurement (TM-113085) | Bosworth, Burken | 1997 | https://ntrs.nasa.gov/citations/19970027694 | pp. 3–6 | 3-cycle rule; Schroeder phases; loop-breaking margins | S | PV |
| Flight Test Results for the F-16XL With a Digital FCS (TP-2004-212046) | Stachowiak, Bosworth | 2004 | https://ntrs.nasa.gov/citations/20040040334 | Eqs. 1–3, 5; Figs. 4, 25 | LOES; bandwidth/phase delay; roll τ_R procedure | S | PV |
| In-flight System Identification of the Ingenuity Mars Helicopter | Ågren et al. | 2025 | https://ntrs.nasa.gov/citations/20250010356 | §IV | CIFER guidelines (via Tischler & Remple) | S | SV |
| Skogestad, "Advanced control" lecture slides | Skogestad (NTNU) | 2024 | NTNU | slides 14–15 | cascade ≥ 4× (Shinskey), 10× recommended | S | SV |
| MIT 16.06 Lecture 31 | Hall | 2012 | MIT OCW | pp. 1, 5 | ZOH ≈ T/2 delay; Tustin | S | SV |
| Klyde & Mitchell; Duda (OLOP); Kolb ICAS 2024; Park JKSAS 2010 | — | 1997–2024 | — | — | rate-limit PIO analysis | S | SV / citation only |
| Tischler & Remple; Klein & Morelli; Franklin, Powell & Workman; MIL-STD-1797A | — | — | books / standard | — | — | — | not inspected |

---

## E. Discrepancies recorded (not silently resolved)

| ID | Discrepancy | Sources | Resolution status |
|---|---|---|---|
| P2-D1 | T_max at M 0.8, sea level: 26 070 lb vs 28 070 lb | TP-1538 Table VI (lead PV, SI column consistent) vs NESC `F16_prop.dml` | **Resolved per configuration with page evidence**: TP-1538 value is 26 070; NESC keeps its own value (library `F16-C6`) |
| P2-D2 | Repository: "Morelli fitted to TP-1538" | Morelli §3 (lead PV): fitted to the Stevens & Lewis simplified version | Repository wording should change (doc 08 H3) |
| P2-D3 | Repository: "domain α −10…45°" as an F-16 data property | TP-1538 tabulates α −20…90° (lead PV) | Domain belongs to the simplified set and the fit |
| P2-D4 | Repository: "LEF fixed at 25°" | TP-1538 build-up (lead PV); fitted sources silent (S) | Unresolved; wording "not stated by the fitted source" |
| P2-D5 | Repository README and propulsion reference: "thrust deck not frozen" | `F16_TP1538_THRUST_DECK_V0.1.md`, Phase 5D runtime semantics | Stale documentation (doc 08 H3) |
| P2-D6 | NESC F-16 aero lineage: "Stevens & Lewis table model" (library) vs "based on Garza & Morelli, implementing TP-1538" (README) | library; README (S) | Consistent once the chain TP-1538 → S&L simplified → Garza & Morelli is written out (doc 07 §2) |
| P2-D7 | NESC mass 637.1595 slug (DAVE-ML) vs 637.26 slug (Bodies page) | library; NESC site (S) | Unresolved (0.016 %); use the DAVE-ML value inside the validation profile |
| P2-D8 | Engine angular momentum not integrated though sourced | TP-1538 App. B (lead PV); Garza & Morelli axis (S); `MavF16PropulsionInstallation.cs:78` | Handoff H4 |
| P2-D9 | F-15 reference span 42.8 ft (repository) vs 42.7 ft (837 reference set) | repo vs TM-2003-212027 (S) | Already registered as `F15-X4` (physical vs reference span); impact ≈ 0.2 % on Cl/Cn |
| P2-D10 | Phase-delay equation sign in TP-2004-212046 Eq. 5 as printed | sub-agent reading | Recorded; use the physically consistent form (doc 05 §4.5) |

---

## F. Source acquisition status

| Source | Status | Next step |
|---|---|---|
| TP-1538 | acquired (NTRS), hash-identical to the repository copy | Table III transcription only if H6 is approved |
| Morelli 1998 | acquired | none |
| Garza & Morelli TM-2003-212145 | acquired by sub-agent | lead re-check of the engine axis statement |
| NESC site, Vol I, AIAA papers | read by sub-agent | download data zips as fixtures (doc 07 §6) |
| NESC Vol II | **not acquired** (size) | download decision |
| AFIT theses, DTIC ADA327869, AIAA-2018-0525 | **blocked** (403 / paywall) | retry from an unblocked host or library access |
| TP-3355 microfiche tables | not online | archive request if ever needed |
| Production FCS gains, production engine decks, F-22 data | not public | none |
