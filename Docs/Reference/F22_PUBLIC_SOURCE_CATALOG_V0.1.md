# F-22 Public Source Catalog V0.1

Status: **R0 PUBLIC-SOURCE ACQUISITION — production F-22A preferred where explicitly supported**

This catalog maps the maximum lawful public technical evidence found for the F-22 family. It strictly separates YF-22 prototype data, F-22 EMD test evidence, production F-22A facts, generic ATF studies, and F119 development/test material.

No classified, leaked, restricted-distribution, export-controlled, or unauthorized material is used.

## 1. Source classes

- `PRIMARY_EXACT`
- `PRIMARY_COMPATIBLE`
- `PRIMARY_FAMILY_ONLY`
- `SECONDARY_LEAD`
- `UNVERIFIED_LEAD`
- `NOT_ACQUIRED`
- `PUBLIC_BUT_INSUFFICIENT_FOR_NUMERIC_MODEL` — additional F-22 label for authoritative public material that cannot define a numeric flight model by itself.

Source PDFs are not committed. `SHA256` is `NOT_COMPUTED — bytes not stored` unless otherwise stated.

## 2. Configuration tags

- `YF22_DEMVAL_PROTOTYPE`
- `F22_EMD_HIGH_AOA_1999_2000`
- `F22_EMD_CONTROL_LAW_DEVELOPMENT_1996`
- `F22A_PRODUCTION_PUBLIC_BASELINE`
- `F119_PW_100_PRODUCTION_PUBLIC`
- `F22_EARLY_WIND_TUNNEL_MODEL`
- `ATF_GENERIC_STUDY`

---

## 3. Acquired public source records

### F22-SRC-001 — official production F-22A fact sheet

- **Aircraft/configuration:** production F-22A public baseline.
- **Title:** *F-22 Raptor* fact sheet.
- **Document/report number:** official USAF fact sheet; no technical-report number.
- **Revision/date:** current public fact-sheet lineage; page records production history through IOC/full-rate production.
- **Author/organization:** United States Air Force.
- **Publication source:** Air Force / Joint Base Langley-Eustis official `.mil` site.
- **Stable URL:** https://www.af.mil/About-Us/Fact-Sheets/Display/Article/104506/f-22-raptor/
- **Filename/SHA256/page count:** web source; not a PDF acquisition.
- **Relevant anchor:** “General characteristics.”
- **Data type:** high-level production geometry, weight/fuel, engine identity, thrust class, performance-class statements.
- **Public facts supported:** 2 x F119-PW-100; 2-D thrust-vectoring nozzles; 35,000-lbf-class thrust each; span 44 ft 6 in; length 62 ft 1 in; height 16 ft 8 in; public weight 43,340 lb; max takeoff 83,500 lb; internal fuel 18,000 lb; Mach-2 class/supercruise statements.
- **Applicability:** strongest exact public production source for high-level F-22A configuration facts.
- **Limitations:** “weight” is not a complete mass-property state; no `S`, `cbar`, aero datum, CG, inertia, coefficients, gains, actuator data, or thrust deck.
- **Classification:** `PRIMARY_EXACT` + `PUBLIC_BUT_INSUFFICIENT_FOR_NUMERIC_MODEL`.

### F22-SRC-002 — Pratt & Whitney F119 production engine page

- **Aircraft/configuration:** production F119-PW-100 on F-22.
- **Title:** *F119 Engine*.
- **Document/report number:** manufacturer public product page.
- **Author/organization:** Pratt & Whitney.
- **Repository:** Pratt & Whitney public website.
- **Stable URL:** https://www.prattwhitney.com/en/products/military-engines/f119  (current site may redirect).
- **Filename/SHA256/page count:** web source.
- **Relevant anchor:** F119 product description / maneuverability section.
- **Data type:** engine architecture/identity and nozzle semantics.
- **Public facts supported:** F119-PW-100 powers F-22; two-dimensional convergent/divergent pitch-vectoring nozzle; thrust can vector up/down as much as 20 degrees; production engine supports supercruise; public source describes full-authority digital control architecture at product level.
- **Applicability:** direct public production engine/nozzle identity.
- **Limitations:** no Mach/altitude thrust map, fuel flow, spool dynamics, installation losses, nozzle force-position data, or control-allocation schedule.
- **Classification:** `PRIMARY_EXACT` + `PUBLIC_BUT_INSUFFICIENT_FOR_NUMERIC_MODEL`.

### F22-SRC-003 — F-22 control-law development and flying-qualities paper

- **Aircraft/configuration:** paper explicitly separates YF-22 prototype discussion from F-22 EMD design/control-law development.
- **Title:** *F-22 Control Law Development and Flying Qualities*
- **Document:** AIAA 96-3379 / DOI 10.2514/6.1996-3379.
- **Authors:** Jeffrey Jay Harris; G. Thomas Black.
- **Organizations:** Lockheed Martin Tactical Aircraft Systems; F-22 System Program Office, Wright-Patterson AFB.
- **Publication date:** July 1996.
- **Repository/public copy:** author-uploaded public AIAA paper; ResearchGate-hosted copy was acquired as searchable PDF in this pass.
- **Stable URL:** https://doi.org/10.2514/6.1996-3379
- **Public PDF lead:** https://www.researchgate.net/publication/269064311_F-22_control_law_development_and_flying_qualities
- **PDF filename:** `F-22-control-law-development-and-flying-qualities.pdf`
- **SHA256:** `NOT_COMPUTED — bytes not stored`.
- **PDF page count:** 14.
- **Relevant PDF anchors:** PDF p. 1 distinguishes YF-22 and F-22 sections; pp. 4–7 describe F-22 design process, architecture, CAP/damping goals and PIO-risk work; figures 8–12 summarize design/evaluation metrics.
- **Data type:** control-law design architecture, flying-quality targets, handling-quality/PIO methodology and validation-by-simulation.
- **Important configuration warning:** YF-22 values and prototype-specific features are not production F-22 authority. The paper itself states some YF-22 features were point-design demonstrations and not a full-envelope production design.
- **F-22-specific public design evidence:** low-order/classical closed-loop response objective; pitch-integrator pole/zero cancellation strategy; CAP and damping used as design variables; comparable Power-Approach/Up-and-Away goals; thrust vectoring retained across gear transition; extensive HQS and VISTA/NF-16D in-flight simulation validation.
- **Applicability:** strongest public program-engineer source located for F-22 control-law *design philosophy and targets*.
- **Limitations:** 1996 development-state design paper, not final production OFP source; no final gain tables, control allocation, actuator schedules or production flight-measured response matrix.
- **Classification:** `PRIMARY_COMPATIBLE` for F-22 EMD design architecture + `PUBLIC_BUT_INSUFFICIENT_FOR_NUMERIC_MODEL`; YF-22 subsection is `PRIMARY_EXACT` only for YF-22.

### F22-SRC-004 — F-22 EMD high-angle-of-attack flight test

- **Aircraft/configuration:** F-22 Engineering and Manufacturing Development high-AOA test aircraft, 1999–2000.
- **Title:** *F-22 Initial High Angle-of-Attack Flight Test Results*
- **Author:** Lee R. Peron.
- **Organization:** Air Force Flight Test Center, Edwards AFB.
- **Publication date:** circa 2000.
- **Repository/public copy:** Society of Flight Test Engineers public conference archive.
- **Stable URL:** https://sfte-ec.org/sfteecold/data/Abstract/A2000-II-02.pdf
- **PDF filename:** `A2000-II-02.pdf`
- **SHA256:** `NOT_COMPUTED — bytes not stored`.
- **PDF page count:** 1 in the public abstract-paper file acquired.
- **Exact relevant PDF page:** p. 1, visually inspected in this research pass.
- **Printed page:** 1.
- **Table/figure/equation:** none in one-page public copy.
- **Data type:** EMD flight-test envelope/results and test configuration.
- **Public evidence:** high-AOA characteristics from below -40 deg to above +60 deg; 1-g expansion beyond 60 deg in 1999; triplex electronic FLCS; pitch-axis thrust vectoring; F119 engines; noted flight-test differences from simulator/wind-tunnel predictions; EMD, not operational production qualification.
- **Applicability:** direct authority for the EMD high-AOA test configuration and for the fact that nonlinear/poststall behavior required flight-test correction.
- **Limitations:** not a coefficient deck; does not prove production F-22A alpha limits or final control laws.
- **Classification:** `PRIMARY_EXACT` for `F22_EMD_HIGH_AOA_1999_2000` + `PUBLIC_BUT_INSUFFICIENT_FOR_NUMERIC_MODEL`.

### F22-SRC-005 — NASA Langley YF-22/F-22 development history

- **Aircraft/configuration:** YF-22 and subsequent F-22 design/wind-tunnel development explicitly separated.
- **Title:** *Partners in Freedom: Contributions of the Langley Research Center to U.S. Military Aircraft of the 1990's*
- **Report:** NASA/SP-2000-4519 / NTRS 20000115606.
- **Author:** Joseph R. Chambers.
- **Organization:** NASA Langley Research Center / NASA History Division.
- **Publication date:** October 2000.
- **Repository:** NASA NTRS.
- **Stable URL:** https://ntrs.nasa.gov/citations/20000115606
- **PDF filename:** `20000115606.pdf`
- **SHA256:** `NOT_COMPUTED — PDF bytes not stored`.
- **Relevant printed page:** F-22 chapter around printed p. 163 in searchable NASA PDF text.
- **Data type:** program/test-history provenance.
- **Critical compatibility fact:** production-development F-22 external geometry changed significantly from YF-22: wingspan increased, wing leading-edge sweep decreased, vertical tails reduced/moved aft, and horizontal tails reconfigured. NASA therefore retested the F-22 in Langley facilities.
- **Public test lineage:** 1992 full-scale-tunnel high-AOA tests; 1993 spin/rotary-balance tests; F-22 full-scale-tunnel program used static-force and forced-oscillation testing, unlike the YF-22 free-flight-model work.
- **Applicability:** decisive evidence against transferring YF-22 numeric aerodynamics directly into F-22A.
- **Limitations:** historical narrative does not publish the underlying coefficient tables.
- **Classification:** `PRIMARY_FAMILY_ONLY` + `PUBLIC_BUT_INSUFFICIENT_FOR_NUMERIC_MODEL`.

### F22-SRC-006 — AFRL dynamic wind-tunnel paper

- **Aircraft/configuration:** F-22 dynamic wind-tunnel research configuration; exact model/geometry state requires the full paper.
- **Title:** *AFRL F-22 Dynamic Wind Tunnel Test Results*
- **Document:** AIAA 99-4015 / DOI 10.2514/6.1999-4015.
- **Author:** W. J. Gillard.
- **Organization:** Air Force Research Laboratory.
- **Publication date:** 1999.
- **Repository status:** bibliographic/abstract evidence found; no verified free primary full-text copy acquired in this pass.
- **Stable identifier:** https://doi.org/10.2514/6.1999-4015
- **PDF filename/SHA256/page count:** `NOT_ACQUIRED`.
- **Known data type from public abstract:** rotary-balance and forced-oscillation data in all three body axes; comparison against NASA Langley Spin Tunnel and 30x60 data; favorable cross-facility correlation.
- **Applicability:** potentially the single highest-value missing public aerodynamic-dynamics source for an F-22 reference.
- **Limitations:** no numeric tables may be promoted until the lawfully public primary paper/data are acquired and its model configuration mapped to EMD/production geometry.
- **Classification:** `NOT_ACQUIRED` (authoritative primary lead).

### F22-SRC-007 — early F-22 fin buffeting model

- **Aircraft/configuration:** 13.3%-scale **early F-22 model**, not demonstrated production geometry.
- **Title:** *Fin Buffeting Features of an Early F-22 Model*
- **NTRS:** 20000052124.
- **Organization:** NASA Langley research.
- **Publication date:** 2000-era public record.
- **Repository:** NASA NTRS.
- **Stable URL:** https://ntrs.nasa.gov/citations/20000052124
- **PDF filename:** NTRS 20000052124 download.
- **SHA256:** `NOT_COMPUTED — bytes not stored`.
- **Data type:** high-alpha unsteady fin pressures/buffeting from Langley Transonic Dynamics Tunnel.
- **Applicability:** high-alpha aeroelastic/buffet family evidence.
- **Limitations:** explicitly an early model; not a general F-22 coefficient source and not production direct authority.
- **Classification:** `PRIMARY_FAMILY_ONLY` + `PUBLIC_BUT_INSUFFICIENT_FOR_NUMERIC_MODEL`.

### F22-SRC-008 — AEDC public propulsion/aerodynamic test history

- **Aircraft/configuration:** F-22A / F119 public test history.
- **Title/source family:** Arnold Engineering Development Complex public facility/history material, including *Beyond the Speed of Sound* and AEDC propulsion-test facility publications.
- **Organization:** U.S. Air Force / AEDC.
- **Stable public sources:** https://www.arnold.af.mil/ and official PDF facility publications.
- **SHA256:** `NOT_COMPUTED — source PDFs not stored`.
- **Relevant public statements:** thousands of hours of F119 test activity; F-22A aerodynamic/wind-tunnel testing at AEDC; F119 altitude/sea-level test capability; manufacturer/program performance and operability data were generated.
- **Data type:** test-program provenance, not released numeric deck.
- **Applicability:** proves high-quality government datasets exist and identifies facilities/test types.
- **Limitations:** public facility histories do not release the performance maps or aerodynamic databases needed for a 6-DOF model.
- **Classification:** `PRIMARY_EXACT` for test-program existence + `PUBLIC_BUT_INSUFFICIENT_FOR_NUMERIC_MODEL`.

---

## 4. Source leads / acquisition targets

### F22-LEAD-001 — full primary AIAA 99-4015

Acquire a lawful public full text of Gillard, *AFRL F-22 Dynamic Wind Tunnel Test Results*, then record model geometry, Mach/Reynolds ranges, alpha/beta/rate grids, forced-oscillation definitions and any coefficient tables.

Status: `NOT_ACQUIRED`.

### F22-LEAD-002 — 1992 F-22 Full-Scale Tunnel data package

NASA/SP-2000-4519 states F-22 high-AOA tests were run in the Full-Scale Tunnel in 1992 with static-force and forced-oscillation testing. The report/data package containing actual force/moment tables has not been acquired.

Status: `NOT_ACQUIRED` / source identity incomplete.

### F22-LEAD-003 — 1993 Spin Tunnel / rotary-balance F-22 data package

NASA/SP-2000-4519 states spin and rotary-balance tests were conducted in 1993. The exact technical report/data package remains to be identified/acquired.

Status: `NOT_ACQUIRED`.

### F22-LEAD-004 — final EMD/production flight-control law documentation

Need a lawfully public program document that defines final command variables, gain schedules, control allocation among tails/ailerons/rudders/TVC, actuator limits/rates and mode transitions.

Status: `NOT_ACQUIRED`; current AIAA 96-3379 is development-design evidence only.

### F22-LEAD-005 — F119-PW-100 performance deck

Need lawfully released thrust/fuel-flow/transient data versus Mach, altitude and power, plus installed inlet/nozzle effects. Public USAF/P&W sources currently provide only architecture and thrust class/nozzle semantics.

Status: `NOT_ACQUIRED`.

## 5. Catalog conclusion

Public F-22 evidence is strong enough to reproduce **identity, external dimensions, high-level mass/fuel state, engine/nozzle architecture, EMD high-AOA test envelope, and major control-law design philosophy**. It is not strong enough to reconstruct a provenance-grade production F-22A aerodynamic coefficient model, mass/inertia tensor, final flight-control gains/allocation, or F119 installed thrust deck.
