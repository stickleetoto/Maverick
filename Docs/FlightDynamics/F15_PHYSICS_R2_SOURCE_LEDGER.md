# F-15 Flight Physics R2 — Source Ledger

Every physical constant or curve R2 introduces, and every lead it examined and rejected. Page numbers are PDF pages unless marked *printed*.

## Adopted

### L1 — Surface actuator first-order lags (research-model lineage)

| Field | Value |
|---|---|
| **Source** | Davison, M. T., *Examination of Wing Rock for the F-15*, AFIT/GAE/ENY/92M-01, February 1992. DTIC **ADA256613**; "Approved for public release; distribution unlimited". |
| **Location** | Appendix B, bifurcation driver `STATE12`, subroutine `FUNX`. **PDF p.97 (printed p.87).** Comment block "Revised 23 Aug 89 - Added differential equations governing control surfaces. Equations assume CAS off and Aileron-Rudder Interconnect off. Deflections are in degrees." |
| **Equations (as printed)** | `F(9)=20.*(CDSTBD-DSTBD)` · `F(10)=28.*(CDRUDD-DRUDD)` · `F(11)=20.*(CDAILD-DAILD)` · `F(12)=20.*(.3*CDAILD-DDTD)` |
| **Meaning** | `ẋ = λ(x_cmd − x)`: first-order lag, bandwidth λ in s⁻¹; x in degrees (the equation is linear, so the units carry over) |

| Channel | λ (s⁻¹) | Time constant | Command |
|---|---|---|---|
| Symmetric stabilator | 20 | 0.050 s | its command |
| Rudder | 28 | 0.0357 s | its command |
| Aileron | 20 | 0.050 s | its command |
| Differential tail | 20 | 0.050 s | 0.3 × the aileron **command**, "acting through the stabilator actuators" |

**Conversion:** none. Maverick's surfaces are in degrees and λ is unit-independent.

**Interpretation:**
- The research aerodynamic model's **own** actuator representation, from the same thesis lineage as the coefficients (Davison App. C `COEFF`) and the mass/inertia (Davison App. C p.124) the aircraft already flies.
- The thesis cites no hardware source for 20/28/20, so it is **not** a claim about production F-15 actuator hardware.
- No rate limit and no position limit accompany it anywhere in the thesis. The App. C time-domain simulator (pp.121–150) is 8-state, with surfaces as parameters and no actuator states.

**Confidence:** HIGH that the printed values are what the source model used (clean listing, four consistent lines). MEDIUM as a representation of real F-15 surface bandwidth (uncited in the source).

**Provenance tag in code:** `MavEngineDataProvenance.PublicReference`, labelled "RESEARCH-MODEL ACTUATOR LAG (Davison 1992 STATE12) - not production F-15 actuator data".

## Re-confirmed, unchanged (already in the repository)

| Quantity | Value | Source |
|---|---|---|
| Density | RHO 0.0012673 slug/ft³ | Baumann p.91; Davison pp.91, 124 |
| Gravity | G 32.174 ft/s² | Baumann/Davison `FUNX` |
| Thrust | 8,300 lbf total, 0.25-in thrust line | Baumann p.34, p.124; Davison p.124 |
| Mass | 37,000 lb | Davison p.124 |
| Inertia | Ix 25,480, Iy 166,620, Iz 186,930, Ixz −1,000 slug·ft² | Davison p.124 |
| Validity | "The model aerodynamics were only valid at M=0.6" | Davison p.44 (printed 34) — the basis for **not** widening the domain |

## Examined and rejected

| Lead | Why rejected |
|---|---|
| **"F-15 stabilator rate limit 40°/s, +24/−10.5°"** (a web-search summary attributing it to NASA TM-4786) | NTRS citation 19970010502: TM-4786 is *Extraction of Lateral-Directional Stability and Control Derivatives for the **Basic F-18** Aircraft at High Angles of Attack* (Iliff & Wang, 1997). **Wrong aircraft.** Not used. |
| **F100 thrust vs Mach/altitude, NASA altitude-facility reports** (NTRS 19790004873, 19790017886; TP-1782; TM on engine P680063) | The same families the R5 audit classified. They give gross-thrust measurements at facility conditions, with no static point and no installed net-thrust deck for the configuration. They cannot anchor a dimensional deck (`F15_REMAINING_GAPS` G6). Not used. |
| **Public Mach-dependent F-15 aerodynamic database** (web search, 2026-09-28) | None located. The Aerobase / A4172 Part II remain non-public (G2). |
| **Public F-15 actuator rate limits** | Four conflicting public travel sets, no configuration-matched rate source (G3, B3). Mixing an NASA 836 / F-15 ACTIVE value into the Baumann research model would mix configurations. Not used. |
| **ISA density for the pilot path** | The ISA table itself is sourced, but see `F15_PHYSICS_R2_GAP_AUDIT.md` §2.1: coupled with the source-condition fixed thrust, it is less consistent than the source model. Deferred behind G6. |

## Searches run for R2

| Date | Engine | Query | Outcome |
|---|---|---|---|
| 2026-09-28 | web | "F-15 stabilator actuator rate limit deg/sec NASA technical memorandum simulation" | only the TM-4786 (F-18) mis-attribution |
| 2026-09-28 | web | "F100-PW-100 installed thrust versus Mach altitude NASA public data F-15 simulation propulsion model" | known R5 families |
| 2026-09-28 | web | "F-15 nonlinear aerodynamic model public dataset Mach dependent coefficients AFIT thesis simulation" | nothing new; the repo's own PR #27 and non-F-15 datasets |

No PDF was downloaded; only the NTRS abstract page for TM-4786 was read.
