# F-15 Full-Scale Source Catalog V0.1

Status: **PUBLIC-SOURCE CATALOG — exact-target gap-closure research**

Target:

`NASA_F15B_836_SN74_0141_PRE_QUIET_SPIKE_BASELINE_F100_PW_100`

This catalog records the strongest primary/public sources located for the gap-closure phase.
A catalog entry is not automatically numeric implementation authority.

## 1. Authority classes used in this catalog

- `DIRECT` — exact target or exact pre-spike target state for the field cited.
- `COMPATIBLE_SUPPORT` — same aircraft family or exact engine family with strong technical
  relevance, but a configuration/build/install equivalence gate remains.
- `CROSS_VALIDATION_ONLY` — useful independent/trend/methodology source; not direct target data.
- `INCOMPATIBLE` — known configuration mismatch blocks direct use.
- `UNKNOWN` — promising original-source lead not acquired/verified in this phase.

## 2. Exact-aircraft / NASA 836 source chain

### FS-S001 — NASA/TM-2012-215978

- **Title:** *Flight Test Results on the Stability and Control of the F-15 Quiet Spike Aircraft*
- **Organization:** NASA Dryden Flight Research Center
- **NTRS:** `20120013435`
- **URL:** https://ntrs.nasa.gov/citations/20120013435
- **Configuration:** NASA F-15B test airplane / Quiet Spike program; includes four pre-spike
  baseline flights and baseline airplane mass-properties row.
- **Relevant direct fields:** target FCS architecture; primary surface roles; baseline-model
  validation/update provenance; mass/CG/inertia anchor already frozen.
- **New direct closure:** ARI is operative below Mach 1.0 and zero otherwise; roll-to-yaw
  crossfeed is nullified above Mach 1.5.
- **Source locator:** section *The Flight Control System*, text associated with Figs. 3–5;
  baseline-model discussion immediately before Quiet Spike model buildup; Table 1 for existing
  mass properties.
- **Class:** `DIRECT`.

### FS-S002 — NASA/TM-2009-214651 / DFRC-654 / H-2956

- **Title:** *Stability and Control Analysis of the F-15B Quiet Spike Aircraft*
- **Organization:** NASA Dryden
- **NTRS:** `20090034255`
- **URL:** https://ntrs.nasa.gov/citations/20090034255
- **Configuration:** same NASA 836 Quiet Spike program; preflight analysis based on baseline
  F-15B simulation plus additive spike models.
- **Relevant fields:** baseline lookup-table model structure; Mach-dependent aerodynamic
  uncertainty treatment; derivative families; exact FCS architecture and schedule boundaries.
- **Limitation:** does not publish the complete baseline lookup tables.
- **Class:** `DIRECT` for model provenance/architecture, not for absent coefficient numbers.

### FS-S003 — NASA/TM-2008-214634 / H-2809

- **Title:** *Aerodynamic Effects of a 24-foot Multisegmented Telescoping Nose Boom on an F-15B Airplane*
- **Organization:** NASA Dryden
- **NTRS:** `20080015840`
- **URL:** https://ntrs.nasa.gov/citations/20080015840
- **Configuration:** NASA 836 program; includes pre-spike baseline-flight modeling and
  spike-attached comparison.
- **Relevant fields:** direct baseline validation evidence; parameter-estimation methodology;
  known model limitations.
- **Important locator:** report p. 17 describes baseline open-loop flight/simulation validation.
  The report states that transonic lateral-directional agreement was not consistent and that
  transonic rudder sweeps were particularly difficult to reproduce.
- **Class:** `DIRECT` for baseline validation/limitation evidence; spike effects are not baseline data.

### FS-S004 — AIAA-2007-6638 / NTRS 20070028417

- **Title:** *Aerodynamic Effects of a 24-Foot, Multisegmented Telescoping Nose Boom on an F-15B Airplane*
- **Organization:** NASA Dryden / AIAA
- **NTRS:** `20070028417`
- **URL:** https://ntrs.nasa.gov/citations/20070028417
- **Configuration:** same program and aircraft.
- **Relevant fields:** baseline model update/validation and transonic validation limitation.
- **Class:** `DIRECT` provenance; spike-attached numeric effects remain configuration-specific.

### FS-S005 — NTRS 20070032807

- **Title:** *Flight Testing of the Gulfstream Quiet Spike on a NASA F-15B*
- **Organization:** NASA Dryden / Gulfstream; SETP 51st Symposium
- **NTRS:** `20070032807`
- **URL:** https://ntrs.nasa.gov/citations/20070032807
- **Configuration:** explicitly identifies production-representative F-15B, USAF S/N 74-0141,
  with production F100-PW-100 engines.
- **Relevant fields:** exact aircraft/engine identity and pre-spike baseline flight campaign.
- **Class:** `DIRECT`.

### FS-S006 — NASA/TM-2006-213674 / H-2627

- **Title:** *The F-15B Lifting Insulating Foam Trajectory (LIFT) Flight Test*
- **Organization:** NASA Dryden
- **NTRS:** `20060022548`
- **URL:** https://ntrs.nasa.gov/citations/20060022548
- **Configuration:** NASA F-15B research test bed; centerline research fixture for the experiment.
- **Relevant fields:** exact-aircraft dimensions, F100-PW-100 identity, hydromechanical + CAS
  description.
- **Locator:** report p. 5, *Aircraft and Aerodynamic Flight Test Fixture Descriptions*.
- **Class:** `DIRECT` for aircraft descriptive geometry/engine identity already frozen;
  fixture aerodynamics are not baseline direct.

### FS-S007 — NASA/TM-2005-213670 / H-2625

- **Title:** *Local Flow Conditions for Propulsion Experiments on the NASA F-15B Propulsion Flight Test Fixture*
- **Organization:** NASA Dryden
- **NTRS:** `20050241960`
- **URL:** https://ntrs.nasa.gov/citations/20050241960
- **Configuration:** NASA 836 research test bed with PFTF.
- **Relevant fields:** F100-PW-100 identity and production-type F-15 inlet architecture;
  PFTF local flow.
- **Class:** `DIRECT` for aircraft inlet architecture/engine identity; PFTF local-flow values are
  not baseline-airframe aerodynamic authority.

### FS-S008 — NASA TM-2001-210395 / NTRS 20030093544

- **Title:** *The F-15B Propulsion Flight Test Fixture: A New Flight Facility for Propulsion Research*
- **Organization:** NASA Dryden
- **URL:** https://ntrs.nasa.gov/citations/20030093544
- **Configuration:** same F-15B research-test-bed lineage with PFTF attached.
- **Relevant field:** 28% MAC analysis CG is stated to correspond to FS 561.7.
- **Locator:** aerodynamic-gust-load/CFD discussion, report p. 8.
- **Class:** `COMPATIBLE_SUPPORT` for target coordinate-system research; **not** direct frozen
  pre-Quiet-Spike datum authority.

### FS-S009 — NASA TM-4782 / NTRS 19970005357

- **Title:** *F-15B/Flight Test Fixture II: A Test Bed for Flight Research*
- **Organization:** NASA Dryden
- **NTRS:** `19970005357`
- **URL:** https://ntrs.nasa.gov/citations/19970005357
- **Configuration:** NASA F-15B with FTF-II.
- **Relevant fields:** same test-bed lineage, flight-envelope/airframe and FTF documentation.
- **Class:** `COMPATIBLE_SUPPORT`; fixture configuration prevents baseline aero promotion.

## 3. F100-PW-100 propulsion-source leads

### FS-S010 — NASA TP-1373 / H-1015

- **Title:** *Comparison of Calculated and Altitude-Facility-Measured Thrust and Airflow of Two Prototype F100 Turbofan Engines*
- **Organization:** NASA Dryden / NASA Lewis
- **NTRS:** `19790004873`
- **URL:** https://ntrs.nasa.gov/citations/19790004873
- **Configuration:** two **prototype F100-PW-100** afterburning turbofans, altitude-facility calibrated.
- **Coverage:** Mach 0.80–2.00; altitude 4,020–15,240 m; corrected performance-model methodology.
- **Relevant fields:** gross-thrust/airflow performance source family and uncertainty.
- **Class:** `COMPATIBLE_SUPPORT`; engine build/serial and installed NASA 836 mapping are unproven.

### FS-S011 — NASA TM-X-3261 / E-8136

- **Title:** *Real-time Simulation of F100-PW-100 Turbofan Engine Using the Hybrid Computer*
- **Organization:** NASA Lewis
- **NTRS:** `19750019996`
- **URL:** https://ntrs.nasa.gov/citations/19750019996
- **Configuration:** F100-PW-100 augmented turbofan simulation with actual-engine comparisons.
- **Relevant fields:** steady-state model, component maps/equations, sea-level-static transient data,
  fuel-flow/internal engine state variables.
- **Class:** `COMPATIBLE_SUPPORT`; exact NASA 836 engine/control build not demonstrated.

### FS-S012 — NASA TP-1034 / E-9090

- **Title:** *Development and Verification of Real-Time, Hybrid Computer Simulation of F100-PW-100(3) Turbofan Engine*
- **Organization:** NASA Lewis
- **NTRS:** `19770024210`
- **URL:** https://ntrs.nasa.gov/citations/19770024210
- **Configuration:** explicitly `F100-PW-100(3)`.
- **Relevant fields:** simulation equations, steady-state and transient performance, control structure.
- **Class:** `COMPATIBLE_SUPPORT` for methodology, but subvariant match to target is unproven.

### FS-S013 — NASA TP-1482 / H-1061

- **Title:** *Evaluation of a Simplified Gross Thrust Calculation Technique Using Two Prototype F100 Turbofan Engines in an Altitude Facility*
- **Organization:** NASA Dryden
- **NTRS:** `19790017886`
- **URL:** https://ntrs.nasa.gov/citations/19790017886
- **Configuration:** prototype F100-PW-100 family.
- **Relevant fields:** gross-thrust calculation method and altitude-facility validation.
- **Class:** `COMPATIBLE_SUPPORT` / methodology only for target.

### FS-S014 — NASA TP-1782 / H-1118

- **Title:** *Flight Evaluation of a Simplified Gross Thrust Calculation Technique Using an F100 Turbofan Engine in an F-15 Airplane*
- **Organization:** NASA Dryden
- **NTRS:** `19810006485`
- **URL:** https://ntrs.nasa.gov/citations/19810006485
- **Configuration:** other F-15 research airplane using prototype F100-PW-100 engines.
- **Coverage:** flight evaluation M 0.6–1.5, altitude 6,000–13,700 m.
- **Relevant fields:** installed-flight thrust-calculation methodology.
- **Class:** `CROSS_VALIDATION_ONLY` for NASA 836.

## 4. F-15 inlet-source leads

### FS-S015 — NASA CR-144866 / NTRS 19780022180

- **Title:** *F-15 Inlet/Engine Test Techniques and Distortion Methodologies Studies, Vol. I: Technical Discussion*
- **Organization:** McDonnell Aircraft for NASA Dryden
- **NTRS:** `19780022180`
- **URL:** https://ntrs.nasa.gov/citations/19780022180
- **Configuration:** full-scale F-15 inlet/F100 research on another flight-test vehicle/engine set.
- **Coverage:** Mach 0.4–2.5, alpha -10 to +12 deg in the study database; recovery/distortion methodology.
- **Relevant fields:** F-15 inlet total-pressure recovery/distortion source family.
- **Class:** `CROSS_VALIDATION_ONLY` until NASA 836 inlet/configuration equivalence is proven.

## 5. Controls/geometry/aerodynamic engineering-source leads

### FS-S016 — NASA CP-10143 Vol. 3 / NTRS 19950007837

- **Title:** *Fourth High Alpha Conference, Volume 3*; contains F-15 high-alpha overview material.
- **Organization:** NASA
- **NTRS:** `19950007837`
- **URL:** https://ntrs.nasa.gov/citations/19950007837
- **Configuration:** F-15 A-D family/high-alpha overview, not NASA 836-specific.
- **Relevant fields:** surface-travel/CAS-authority family information; analog-FCC context.
- **Class:** `COMPATIBLE_SUPPORT`; candidate family limits are **not target hard stops**.

### FS-S017 — AFIT/GAE/ENY/96M-1 / ADA319164

- **Title:** *An Investigation into the Effects of Lateral Aerodynamic Asymmetries, Lateral Weight Asymmetries, and Differential Stabilator Bias on the F-15 Directional Flight Characteristics at High Angles of Attack*
- **Organization:** USAF Air Force Institute of Technology
- **Author:** David R. Evans
- **Date:** March 1996
- **DTIC accession:** `ADA319164`
- **Public archive:** https://scholar.afit.edu/etd/6115/
- **Configuration:** production-representative F-15B S/N 76-0130 research, not NASA 836.
- **Value for this task:** directly cites McDonnell Douglas `MDC A4172` and later F-15
  aerodynamic working papers, exposing the original engineering-data lineage.
- **Class:** `CROSS_VALIDATION_ONLY` numerically; strong bibliographic lead.

### FS-S018 — AFIT/GAE/ENY/90D-16 / AD-A230462

- **Title:** *Investigation of the High Angle of Attack Dynamics of the F-15B Using Bifurcation Analysis*
- **Organization:** USAF AFIT
- **Author:** Robert J. McDonnell
- **Date:** December 1990
- **DTIC:** `AD-A230462`
- **Configuration:** analytical/simulation F-15B study.
- **Value:** bibliography identifies `MDC A4172 Part II` as aerodynamic coefficients and
  stability/control derivatives.
- **Class:** `CROSS_VALIDATION_ONLY` numerically; strong primary-source acquisition lead.

### FS-S019 — AFIT/GA/ENY/91D-1 / AD-A244044

- **Title:** *Analysis of the Effects of Removing Nose Ballast From the F-15 Eagle*
- **Organization:** USAF AFIT
- **DTIC:** `AD-A244044`
- **Value:** cites `MDC A4172 Part I, Supplement 1`, USAF Series Manual Aero/Inertia.
- **Class:** `CROSS_VALIDATION_ONLY` numerically; bibliographic lead.

### FS-S020 — McDonnell Douglas MDC A4172 — not acquired

- **Bibliographic identity:** *F-15 Stability Derivatives Mass and Inertia Characteristics*,
  USAF Series Manual `A-11-2-2-1-1` / Aero-Inertia lineage, initial publication 1976 with later
  supplement; Part II is cited in public USAF work as aerodynamic coefficients and
  stability/control derivatives.
- **Organization:** McDonnell Douglas / USAF.
- **Public primary copy located this phase:** **NO**.
- **Potential value:** highest-value candidate for reference geometry, mass/inertia lineage and
  numeric aerodynamic model.
- **Class:** `UNKNOWN` until primary document and applicability to the production F-15B/NASA 836
  state are verified.

### FS-S021 — McDonnell Aircraft DN-1180.01-238-458 Rev. D — not acquired

- **Title:** *F-15 Flight Control System Description*
- **Organization:** McDonnell Aircraft
- **Date cited publicly:** October 1981
- **Public primary copy located this phase:** **NO**.
- **Potential value:** hard surface limits, mixing, actuator/control-system details.
- **Class:** `UNKNOWN`.

### FS-S022 — AFFTC-TR-76-48

- **Title:** *F/TF-15A Flying Qualities Air Force Development Test and Evaluation*
- **Authors cited by NASA:** Arthur Y. Tanaka and Rodrigo J. Huete
- **Organization:** Air Force Flight Test Center
- **Date:** July 1977
- **Configuration:** development-test F/TF-15A family, not NASA 836.
- **Value:** NASA F-15B fixture work explicitly uses this air-superiority derivative source lineage.
- **Class:** `CROSS_VALIDATION_ONLY`.

## 6. Existing RPV/subscale reference chain

The following remain valuable but cannot close full-scale target fields:

- NASA TN D-8136 / NTRS `19760008088`;
- NASA TN D-7941 / NTRS `19750012221`;
- NASA TN D-8052 / NTRS `19760010068`;
- NASA TM-X-62360 / NTRS `19780002076`;
- NASA CR-3478 / `19820016292`;
- NASA CR-3479 / `19820014339`.

Class: `CROSS_VALIDATION_ONLY` or methodology according to the frozen transfer policy.

## 7. Explicit incompatible research configurations

Not direct target authority:

- NASA 837 / F-15 ACTIVE / IFCS / canards / TVC / F100-PW-229;
- NASA 835 HIDEC/DEEC/PW1128;
- NASA 836 post-2014 F100-PW-220E state;
- F-15E/F-15EX;
- CFT/store/three-surface data;
- Quiet-Spike-attached aerodynamic deltas as baseline coefficients.

## 8. Source-acquisition priority after this phase

1. Obtain a verified public primary copy of `MDC A4172` Part I/II and supplements.
2. Obtain `DN-1180.01-238-458 Rev. D`.
3. Trace the NASA 836 baseline simulation database references beyond the public Quiet Spike reports.
4. Identify NASA 836 engine serial/build/control configuration and test whether TP-1373/TM-X-3261
   can be promoted field-by-field.
5. Locate exact NASA 836 engine installation structural coordinates and side-inlet recovery data.
