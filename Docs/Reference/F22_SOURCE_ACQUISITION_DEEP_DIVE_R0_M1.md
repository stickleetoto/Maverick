# F-22 Public Source Acquisition — Deep Dive R0 M1

Status: **PUBLIC-EVIDENCE MILESTONE — no aircraft implementation**

Target where explicitly supported:

`PRODUCTION_F22A_PUBLIC_REFERENCE`

Mandatory separation remains:

- YF-22 prototype;
- F-22 EMD / developmental aircraft;
- production F-22A;
- generic ATF studies;
- F119 development/test configurations.

This milestone records stronger public evidence for flight-control design targets, EMD high-angle-of-attack validation, wind-tunnel/source lineage and public CFD/system-identification work. It does not claim that a production numeric aerodynamic database or final production control law is public.

## 1. Major result

The F-22 public evidence ceiling is higher for **control-law design intent, flight-test envelope, validation methodology and configuration history** than for numeric flight-model reconstruction.

Public evidence can now constrain a reference model more tightly in these areas:

- pitch-control design philosophy and handling-quality target ranges;
- use of low-order equivalent longitudinal response;
- integration of pitch-axis thrust vectoring with aerodynamic pitch effectors;
- EMD high-AOA envelope and departure-resistance testing;
- triplex electronic FLCS architecture at EMD test level;
- high-AOA wind-tunnel / spin / rotary-balance test lineage;
- selected clean-aircraft static/dynamic CFD validation methodology.

It still does not provide a production-ready `CX/CY/CZ/Cl/Cm/Cn` deck, mass/inertia schedule, final control allocation/gain tables, actuator dynamics, or F119 installed thrust deck.

## 2. Newly strengthened primary/public sources

### F22-DEEP-SRC-001 — Harris & Black F-22 control-law development paper

- **Title:** *F-22 Control Law Development and Flying Qualities*
- **Document:** AIAA 96-3379
- **DOI:** 10.2514/6.1996-3379
- **Authors:** Jeffrey Jay Harris; G. Thomas Black
- **Organizations:** Lockheed Martin Tactical Aircraft Systems; F-22 System Program Office, Wright-Patterson AFB
- **Publication date:** July 1996
- **Public copy:** author-uploaded conference-paper PDF available publicly
- **Configuration:** paper explicitly separates YF-22 prototype history from F-22 EMD control-law development
- **Classification:** `PRIMARY_COMPATIBLE` for F-22 development design intent; `PUBLIC_BUT_INSUFFICIENT_FOR_NUMERIC_MODEL` for production F-22A control-law reproduction

The paper provides unusually useful quantitative **design targets**, not final production schedules.

For the F-22 longitudinal design it states:

- the short-term response was intentionally structured as a low second-order equivalent response;
- integrator pole/zero effects were shaped so integrator dynamics were not exposed in the short-term closed-loop response;
- CAP around **0.35 rad/sec^2/g** was selected at flight conditions with relatively high `Nz/alpha`;
- at lower airspeeds CAP was allowed to rise toward **1.0 rad/sec^2/g** for adequate closed-loop bandwidth;
- short-period damping was typically **1.1–1.2** over most flight conditions in the design described;
- Power Approach and Up&Away CAP/damping goals were designed to be nearly identical, with identical maneuvering stick-force gradients;
- vectoring with gear down was used as part of avoiding the severe command-gradient transition that contributed to the YF-22 PIO.

**Authority boundary:** these values are 1996 F-22 design-development targets. They are not promoted as final production F-22A scheduled gains, final mode parameters, or proof of a specific operational OFP.

### F22-DEEP-SRC-002 — AFFTC EMD high-AOA flight-test evidence

- **Title:** *F-22 Initial High Angle-of-Attack Flight Test Results*
- **Author:** Lee R. Peron
- **Organization:** Air Force Flight Test Center, Edwards AFB
- **Publication:** Society of Flight Test Engineers conference abstract, 2000
- **Public PDF:** https://sfte-ec.org/sfteecold/data/Abstract/A2000-II-02.pdf
- **PDF pages:** 1
- **Visual inspection:** page 1 was successfully rendered/inspected during acquisition
- **Configuration:** F-22 Engineering and Manufacturing Development flight-test program
- **Classification:** `PRIMARY_EXACT` for the reported EMD test state; `PUBLIC_BUT_INSUFFICIENT_FOR_NUMERIC_MODEL` for production F-22A aerodynamics

Publicly supported EMD facts include:

- high-AOA envelope expansion began 22 July 1999;
- 1-g maneuvering beyond +60 deg AOA had been cleared by 8 September 1999;
- by May 2000 testing had demonstrated below -40 deg to above +60 deg AOA;
- slow-speed zoom-climb expansion and departure-resistance testing were underway;
- the report attributes successful high-AOA design to pitch-axis thrust vectoring, flight-control design, air-data system and Pratt & Whitney F119 engines;
- all air-vehicle flight-control surfaces were directed by a triplex electronic FLCS;
- flight test revealed differences from simulator/wind-tunnel predictions, including unique/unpredicted aerodynamics requiring control-law response.

This is strong validation evidence for robustness and envelope behavior but contains no complete coefficient database.

### F22-DEEP-SRC-003 — NASA SP-2000-4519 F-22 test-source graph

- **Title:** *Langley Contributions to the F-22*
- **Publication:** NASA SP-2000-4519
- **Organization:** NASA Langley Research Center / NASA History
- **NTRS record:** 20000115606
- **Stable official PDF:** https://ntrs.nasa.gov/citations/20000115606 and NASA-hosted `sp-4519.pdf`
- **Classification:** `PRIMARY_FAMILY_ONLY` / program-history authority; individual test datasets remain separate acquisition targets

This NASA source establishes that F-22 development used distinct facilities/data campaigns, including:

- supersonic performance/stability/control testing in the Langley Unitary Plan Wind Tunnel in 1991;
- high-angle-of-attack testing in the Langley Full-Scale Tunnel after the F-22 configuration changed from the YF-22;
- F-22 Spin Tunnel / rotary-balance work;
- spin-recovery-parachute configuration testing, including a later attachment relocation on the full-scale test aircraft;
- vertical-tail buffet testing in the Langley 16-Foot Transonic Dynamics Tunnel.

Most importantly, the NASA history explicitly documents meaningful YF-22 -> F-22 external-geometry changes. Therefore YF-22 coefficient/control data cannot be presumed production-compatible merely because the prototype had similar outer lines.

### F22-DEEP-SRC-004 — AFRL dynamic wind-tunnel paper lead

- **Title:** *AFRL F-22 Dynamic Wind Tunnel Test Results*
- **Document:** AIAA 99-4015
- **Author:** William J. Gillard
- **DOI:** 10.2514/6.1999-4015
- **Program:** AFRL F-22 dynamic wind-tunnel testing
- **Acquisition status:** full lawful primary paper/data tables were not acquired in this milestone
- **Classification:** `NOT_ACQUIRED` / high-priority `SECONDARY_LEAD`

Public bibliographic/abstract evidence identifies:

- rotary-balance testing;
- forced-oscillation testing about all three body axes;
- comparison against NASA Langley Spin Tunnel and Full-Scale/30x60-class data;
- dynamic-derivative comparison/validation.

This is currently the most promising missing document for an actual F-22 dynamic-derivative dataset. No derivative number from secondary summaries is accepted as direct authority.

### F22-DEEP-SRC-005 — USAF SEEK EAGLE public CFD/SID study

- **Title:** *Determining the Stability and Control Characteristics of High-Performance Maneuvering Aircraft Using High-Resolution CFD Simulation with and without Moving Control Surfaces*
- **Document:** AIAA 2013-0972
- **Authors:** James D. Clifton; C. Justin Ratcliff; David J. Bodkin; John P. Dean
- **Organization:** United States Air Force SEEK EAGLE Office, Eglin AFB
- **Public-release marking:** `DISTRIBUTION A. Approved for public release; distribution unlimited.`
- **Public PDF:** https://www.cobaltcfd.com/pdfs/AIAA_2013_ratcliff.pdf
- **PDF pages:** 20
- **Configuration:** study includes F-22 clean and external-fuel-tank computational configurations, among A-10C/F-16C work
- **Classification:** `PRIMARY_FAMILY_ONLY`; `PUBLIC_BUT_INSUFFICIENT_FOR_NUMERIC_MODEL`

The paper states that F-22 static, time-accurate CFD cases investigated longitudinal and lateral-directional static stability at selected Mach/altitude conditions and used prior wind-tunnel data and Lockheed Martin performance data as validation references. It also states that nonlinear reduced-order aerodynamic models for F-22 and other fighters were generated through system identification of computational training maneuvers.

Important restriction:

- the public paper demonstrates **method and validation existence**;
- it does not publish a complete production F-22 coefficient database or the proprietary Lockheed Martin validation dataset;
- the exact computational geometry/revision/configuration must be treated as source-specific.

A screenshot render was attempted for this public PDF during acquisition but the web cache failed; therefore no table/figure numeric value is promoted from visual inspection in this milestone.

### F22-DEEP-SRC-006 — low-speed buffet source lead

- **Title:** *Low-Speed Wind Tunnel Buffet Testing on the F-22*
- **Publication:** Journal of Aircraft 43(4), 2006
- **DOI:** 10.2514/1.10247
- **Authors:** William D. Anderson; Suresh R. Patel; Christopher L. Black
- **Configuration:** F-22 V-9 wind-tunnel model, 2002 Lockheed Martin Marietta Low-Speed Wind Tunnel test
- **Acquisition status:** abstract/bibliographic record acquired; full primary paper not acquired in this pass
- **Classification:** `NOT_ACQUIRED` / `SECONDARY_LEAD`

The public abstract indicates stability/control and buffet testing of CFD-derived geometry modifications through roughly the low-30-degree-AOA region and warns of Mach, inlet-mass-flow and scaling effects. This is useful configuration-specific validation evidence, not a baseline F-22 force/moment deck.

## 3. Publicly reproducible vs constrained vs unavailable — updated

### A. PUBLICLY REPRODUCIBLE

At public-primary level, one can reproduce or directly encode:

- production F-22A high-level external dimensions from official USAF/manufacturer sources;
- F119-PW-100 twin-engine identity;
- public thrust class as a single descriptive point/rating only, not a deck;
- two-dimensional pitch-vectoring nozzle architecture and public +/-20 deg vectoring range;
- EMD use of triplex electronic FLCS;
- EMD demonstrated high-AOA envelope anchors;
- 1996 F-22 longitudinal design-target ranges for CAP/damping **as design-study values**, with explicit date/program tag.

### B. PUBLICLY CONSTRAINABLE

Public evidence can constrain but not reproduce exactly:

- longitudinal control-law response order and design philosophy;
- mode-transition consistency goals;
- pitch-control allocation philosophy involving horizontal tails and TVC;
- high-AOA departure resistance / control robustness;
- dynamic derivative sign/trend expectations from the known AFRL/NASA wind-tunnel campaigns;
- static stability behavior at selected CFD validation conditions;
- buffet onset/mitigation qualitative behavior.

### C. REQUIRES APPROXIMATION

A flyable public-evidence model would still need explicit approximation for:

- full nonlinear static aerodynamic coefficient surfaces;
- dynamic derivatives outside any later acquired public test points;
- complete control-effectiveness database;
- final production gain schedules and control allocation;
- aerodynamic-surface actuator transfer functions/rates;
- detailed inlet/engine/nozzle installation coupling;
- F119 Mach/altitude/power thrust surface;
- fuel/mass/CG/inertia state evolution.

### D. NOT PUBLICLY AVAILABLE / NOT ACQUIRED

No defensible public authority is currently held for:

- complete production `CX/CY/CZ/Cl/Cm/Cn` table/deck;
- production mass/CG/inertia tensor schedule;
- final operational OFP gain/schedule tables;
- production actuator rate/position/dynamic tables;
- production F119 installed thrust/fuel-flow/transient deck;
- full AFRL F-22 dynamic wind-tunnel numeric derivative dataset;
- Lockheed Martin performance database cited by public CFD work.

## 4. Important numerical-boundary rule

The Harris/Black F-22 CAP/damping values are **not** equivalent to aircraft aerodynamic derivatives.

They are closed-loop design targets/choices in a 1996 development paper. A future Maverick approximation may use them only if explicitly labeled as a development-era handling-quality constraint, never as a production F-22A gain schedule or proof of the underlying plant dynamics.

Similarly, the official/public F119 thrust-class value is not a Mach/altitude thrust deck.

## 5. Highest-value next acquisitions

1. **AIAA 99-4015 — William J. Gillard, *AFRL F-22 Dynamic Wind Tunnel Test Results***, full lawful primary paper plus any public tables/figures/data supplement.
2. Original public report/data package for the **1992 Langley F-22 Full-Scale Tunnel static-force / forced-oscillation campaign**.
3. Original public report/data package for the **1993 Langley F-22 Spin Tunnel / rotary-balance campaign**.
4. Any public EMD/production-compatible **F-22 FLCS/actuator/control-allocation report** containing surface/nozzle limits, rates, schedules and configuration/OFP identity.
5. Any public **F119-PW-100 performance/install report** containing Mach-altitude-power thrust, fuel flow, transient dynamics and inlet/nozzle installation effects.

An exact production-compatible reference geometry/mass/CG/inertia technical package remains equally critical if found.

## 6. Updated readiness verdict

**F-22: `REFERENCE-ONLY — INSUFFICIENT NUMERIC DATA`.**

The deeper search increases the fidelity of a **publicly constrained reference/validation model**, especially in control-response targets and high-AOA validation. It does not cross the threshold to a provenance-grade production flight model because the fundamental plant data remain unavailable.

A separate approximation project could build a plausible F-22-like six-DoF model bounded by these public targets, but it must never be represented as an exact public-evidence F-22A aerodynamic model.