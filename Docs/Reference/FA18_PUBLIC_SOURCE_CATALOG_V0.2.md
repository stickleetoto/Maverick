# F/A-18 Public Source Catalog V0.2

Status: **ACQUIRED-PDF PROVENANCE UPDATE**

This document supplements V0.1 with byte-level acquisition records and exact anchors for the primary PDFs now held in the research session. Source PDFs are not committed to the repository.

## FA18-SRC-001 — NASA-TM-107601 / `f18bas`

- **Title:** *Simulation Model of a Twin-Tail, High Performance Airplane*
- **Authors:** Carey S. Buttrill; P. Douglas Arbuckle; Keith D. Hoffler
- **Organization:** NASA Langley Research Center / ViGYAN
- **Publication:** July 1992
- **Stable URL:** https://ntrs.nasa.gov/citations/19920024293
- **Acquired filename:** `19920024293 (2).pdf`
- **PDF pages:** 184
- **SHA256:** `34ed8edd9452aed3afbd7677c3847b888ec4b234224e0dd016c114564f5bb9f3`
- **Configuration:** `NASA_F18BAS_BASIC_F18_OFP_8_3_3_TVC_OFF_CAPABLE`
- **Classification:** `PRIMARY_EXACT` for `f18bas`; `PRIMARY_FAMILY_ONLY` for operational fleet aircraft.
- **Exact anchors newly verified:**
  - PDF pp. 23–25 / printed 17–19 — Table 3.3 component dimensions.
  - PDF p. 26 / printed 20 — Table 3.4 aerodynamic reference dimensions.
  - PDF p. 26 / printed 20 — Table 3.5 weight/CG/inertia.
  - PDF p. 87 / printed 81 — Tables 8.6/8.7 actuator nonlinearities, rate/position limits and first-order models.
  - printed p. 83 onward — Section 9 FCS, 8.3.3 PROM-set AFU inner-loop CAS.
- **Limits:** full numerical aerodynamic lookup arrays are described but not printed.

## FA18-SRC-002 — NASA-TM-110216 / `f18harv`

- **Title:** *Simulation Model of the F/A-18 High Angle-of-Attack Research Vehicle Utilized for the Design of Advanced Control Laws*
- **Authors:** Michael D. Messina; Mark E. Strickland; Keith D. Hoffler; Susan W. Carzoo; W. Thomas Bundick; Jessie C. Yeager; Fred L. Beissner Jr.
- **Organization:** NASA Langley and contractors
- **Publication:** May 1996
- **Stable URL:** https://ntrs.nasa.gov/citations/19960027892
- **Acquired filename:** `19960027892 (2).pdf`
- **PDF pages:** 164
- **SHA256:** `55ebc29f32152970b4117865299d16777c1b0a5cd760e75bde6979944e738f6c`
- **Configuration:** exact `f18harv` simulation; contains Phase-II/III research-system capabilities.
- **Classification:** `PRIMARY_EXACT` for documented `f18harv`.
- **Exact anchors newly verified:**
  - PDF p. 19 / printed 13 — Table 2.3 HARV mass/CG/inertia states.
  - PDF pp. 21–23 / printed 15–17 — Table 2.4 source-file/subroutine map.
  - printed p. 38 — Section 4.1 engine table topology and dynamics.
  - printed p. 40 — Eq. 4.1 TV-vane actuator.
  - printed pp. 90–93 — Tables 6.1/6.2 HARV primary actuator models and limits.
- **Limits:** 29 engine numeric arrays, TV effectiveness/loss arrays and basic F/A-18 aero arrays are not printed.

## FA18-SRC-006 — NASA/TP-97-206539

- **Title:** *Flight-Determined Subsonic Longitudinal Stability and Control Derivatives of the F-18 High Angle of Attack Research Vehicle (HARV) With Thrust Vectoring*
- **Authors:** Kenneth W. Iliff; Kon-Sheng Charles Wang
- **Organization:** NASA Dryden / SPARTA
- **Publication:** December 1997
- **Stable URL:** https://ntrs.nasa.gov/citations/19980007172
- **Acquired filename:** `19980007172.pdf`
- **PDF pages:** 72
- **SHA256:** `1905e23ca63d7d3e57153df2e168210bc74d8bbc5d071502ee89b44843dc5023`
- **Configuration:** `NASA_F18_HARV_160780_PHASE2_TVC_NASA0`; LEX fence present, ANSER absent, mixer 1, 1992–1994 analyzed flights.
- **Classification:** `PRIMARY_EXACT` for analyzed Phase-II state.
- **Exact anchors newly verified:**
  - PDF p. 14 / printed 8 — Table 1 conventional control-surface limits.
  - PDF p. 16 / printed 10 — Table 3 unmodified Phase-I vs modified Phase-II/III mass/reference comparison.
  - printed pp. 24–27 and figures 11–16 — longitudinal flight-derived derivative results.
- **Important secondary use:** Table 3 is `PRIMARY_EXACT` for the explicitly labeled unmodified Phase-I 60%-fuel reference state.

## FA18-SRC-010 — NASA-CR-194838

- **Title:** *Determination of the Stability and Control Derivatives of the NASA F/A-18 HARV Using Flight Data*
- **Authors:** Marcello R. Napolitano; Joelle M. Spagnuolo
- **Organization:** West Virginia University for NASA Grant NCC 2-759; NASA technical contact Albion H. Bowers
- **Publication:** December 1993
- **Stable URL:** https://ntrs.nasa.gov/citations/19940020331
- **Acquired filename:** `19940020331.pdf`
- **PDF pages:** 124
- **SHA256:** `7ba84d9f3772f78c746bf60a838da7af3f630748b5158cc993df68b812204c81`
- **Configuration:** HARV Phase-II OBES campaign, 29 September 1992, nominal 10/25/30/40/50/60 deg AOA.
- **Classification:** `PRIMARY_EXACT` for the stated PID campaign.
- **Relevant anchors:**
  - printed p. 62 — analysis scope and restriction note.
  - printed pp. 63–71 — interpretation/results.
  - printed pp. 79–87 — lateral derivative figures 8.7–8.15.
  - printed pp. 95–103 — longitudinal derivative figures 8.23–8.31.
- **Limits:** one maneuver per test point; high-AOA scatter; report states thrust-vectoring doublet information was excluded from the analysis. Vane derivative panels require special audit before authority promotion.

## FA18-SRC-011 — NASA-TM-4240 / AIAA-90-2166

- **Title:** *A Simple Dynamic Engine Model for Use in a Real-Time Aircraft Simulation With Thrust Vectoring*
- **Author:** Steven A. Johnson
- **Organization:** NASA Ames Research Center, Dryden Flight Research Facility
- **Publication:** October 1990
- **Stable URL:** https://ntrs.nasa.gov/citations/19910009766
- **Acquired filename:** `19910009766.pdf`
- **PDF pages:** 26
- **SHA256:** `36d9d13d9fcd6e4dde0e79bf536bc1bb3f80084f66a54d34d28e1402178316ff`
- **Distribution:** report-documentation page states `Unclassified-Unlimited`.
- **Configuration:** F404-GE-400 with HARV nozzle modifications; simple dynamic engine model derived from GE `R88AEB427`.
- **Classification:** `PRIMARY_EXACT` for the published simple HARV engine model; not a generic production F404 installation deck.
- **Exact anchors:**
  - PDF pp. 6–10 / printed 2–6 — model lineage, engine architecture, table variables and validation.
  - PDF p. 14 / printed 10 — Figure 4 Max-AB gross-thrust table points.
  - PDF p. 15 / printed 11 — Figure 5 PLA dynamics and Figure 6 vectoring thrust loss example.
  - PDF p. 16 / printed 12 — Figure 7 engine-model table topology.
  - PDF p. 25 — distribution statement.
- **Limits:** Figure 4 exposes Max-AB gross thrust only; full idle/mil/min-AB/max-AB numeric tables remain unavailable.

## Acquisition consequence

The strongest exact physical reference target remains:

`NASA_F18_HARV_160780_PHASE1_BASIC`

The newly acquired primary PDFs close the target's 60%-fuel reference geometry/mass/CG/inertia and provide compatible conventional surface limits. They also substantially close simulation actuator/FCS and F404 model structure. They do **not** close the complete basic F/A-18 nonlinear aerodynamic coefficient arrays.
