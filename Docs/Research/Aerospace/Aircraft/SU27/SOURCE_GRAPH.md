# Su-27 Source Graph (SU27-R0 feasibility)

**Feasibility only. No Su-27 model is proposed.** This is a short public-source study. Every source here was seen only through web-search index extracts: the hosts that hold the documents (icas.org, tsagi.ru, nkj.ru, sukhoi.org, uecrus.com, aviation21.ru, html.rhhz.net, NTRS, DTIC) are blocked by this session's egress policy. The strongest level reached is `CONTENT_EXTRACT_VERIFIED`, and no page was verified. The repository held no earlier Su-27 work, so no source carries an `existing_maverick_analysis` entry.

Source IDs refer to [`SOURCE_INDEX.json`](../../SOURCE_INDEX.json) (slice: [`SOURCES.json`](SOURCES.json)). Configuration IDs: [`KNOWN_DATA.md`](KNOWN_DATA.md#1-configuration-registry).

---

## 1. Configuration spine (kept separate)

```
Sukhoi PFI / T-10 programme
 ├─ T-10 prototypes (original configuration, superseded) ....... SU27-CFG-T10-PROTOTYPE      never production authority
 │     redesigned (Sukhoi history pages)
 ├─ T-10S development aircraft (first flight 1981 per extract) .. SU27-CFG-T10S-DEVELOPMENT
 │
 ├─ Baseline production Su-27 / Su-27S (single-seat) ........... SU27-CFG-BASELINE-SU27S     ◀ study target; no model data located
 │
 ├─ "Su-27" named without variant or phase ..................... SU27-CFG-FAMILY-UNSPECIFIED  holding bucket, never baseline until resolved
 ├─ TsAGI tunnel / free-flight scale models (T-105, 1987) ....... SU27-CFG-WT-TSAGI-MODELS     CONFIGURATION_UNKNOWN
 │
 ├─ Su-30 family (incl. Su-30MK) ................................ SU27-CFG-DERIV-SU30          DERIVATIVE_ONLY
 └─ Su-27UB, Su-27SK/SKM, Su-33, Su-27M/Su-35, Su-37,
    Su-27LL-PS, Su-34 ............................................ SU27-CFG-DERIV-OTHER         DERIVATIVE_ONLY

Engine: AL-31F (Lyulka / UEC-Saturn) ............................ ENG-AL-31F

Not the Su-27: generic fighter / method studies ................. SU27-CFG-NOT-SU27-GENERIC
Game and enthusiast models (DCS, FlightGear) .................... SU27-CFG-ENTHUSIAST-GAME     PROHIBITED as authority
```

## 2. Lineage

```
Sukhoi Design Bureau + TsAGI (design, tunnel programme) + LII (flight test)
      │
      ├─ SU27-SUKHOI-TSAGI-AERO-MASS-DATA (aero database, reference geometry, mass, inertia) ── NOT PUBLICLY LOCATED
      │        ▲ fed by
      │        ├─ TsAGI T-105 tests of Su-27 models, 1987, alpha 0-180 deg, beta +/-90 deg
      │        │     reported in SU27-NIZH-2008-ZHELNIN (popular-science; extract)
      │        └─ Sukhoi / LII flight tests
      │
      ├─ ICAS-2002-POGOSYAN-SU27 (Pogosyan, Simonov, Zagainov; Sukhoi/TsAGI; 2002)          ◀ strongest aero lead
      │     describes the Sukhoi/TsAGI high-alpha model: asymmetric vortex breakdown, dynamic lag,
      │     static and dynamic hysteresis in lift, side force and all three moments; stall, spin, TVC requirements.
      │     Printed coefficients: UNKNOWN (PDF not opened)
      │        ┆ method family (link not established from either paper)
      │        ┆   JAIRCRAFT-1994-GOMAN-KHRABROV (TsAGI state-space unsteady aerodynamics; METHOD_ONLY)
      │        ┆   PAS-1997-GOMAN-ZAGAINOV-KHRAMTSOVSKY (bifurcation methods; examples F-4, F-14, F-15, HIRM: no Su-27)
      │
      ├─ SU27-TSAGI-CENTENARY-ARTICLE (republished TsAGI-centenary narrative; extract)
      │     FBW with stability augmentation, subsonic static instability, AoA-scheduled leading-edge flaps
      │
      ├─ SU27-SDU10-FCS-DESIGN-DATA (laws, gains, limiters, surface travel, actuators) ── NOT PUBLICLY LOCATED
      ├─ SU27-RLE-FLIGHT-MANUAL (official flight manual) ── NOT PUBLICLY LOCATED; unofficial copies not sought or used
      │
      └─ History and identity: SU27-SUKHOI-HISTORY-PAGES (T-10 → T-10S redesign), SU27-GORDON-2007-MIDLAND (secondary history)
                               SU27-BYUSHGENS-1998-TSAGI-MONOGRAPH (TsAGI monograph; Su-27 content UNCONFIRMED)

AL-31F (Lyulka / UEC-Saturn)
      ├─ SU27-UEC-2017-AL31F-NEWS (official: afterburning turbofan, variable nozzle, 12,500 kgf class, "Su-27 family")
      └─ SU27-AL31F-PERFORMANCE-DECK (thrust, fuel flow, installation, transients) ── NOT PUBLICLY LOCATED

Western and Chinese analyses (not Sukhoi data)
      ├─ AIAA-93-0183 and JAIRCRAFT-1995-ERICSSON-COBRA (Ericsson: Cobra unsteady aerodynamics; background)
      ├─ SU27-KQDLXXB-2020-POSTSTALL-MODEL-FLIGHT (Chinese survey: Soviet free-flight model programme from 1975; background)
      └─ JAIRCRAFT-2020-WANG-QUASI-COBRA ("a fighter aircraft", not identified as a Su-27)

Derivative only: SU27-SPACEPHYS-SDU-10MK (Su-30MK FCS)
Prohibited:      SU27-DCS-FLIGHT-MANUAL (game), SU27-FLIGHTGEAR-SU27SK-YANES19 (enthusiast JSBSim, export Su-27SK)
```

## 3. Rules applied

1. **A Su-27-like model is not a Su-27 model.** Only a source that names the production Su-27 or Su-27S may attach data to `SU27-CFG-BASELINE-SU27S`. A source that says only "Su-27" is tagged `SU27-CFG-FAMILY-UNSPECIFIED` until its content shows which aircraft.
2. **Prototype data never transfer.** The T-10 was redesigned into the T-10S. `SU27-CFG-T10-PROTOTYPE` and `SU27-CFG-T10S-DEVELOPMENT` stay separate from production.
3. **Derivatives are never baseline authority.** Su-30, Su-33, Su-27M/Su-35, Su-37, Su-27LL-PS, the export Su-27SK/SKM and the Su-27UB trainer are `DERIVATIVE_ONLY`. The export and trainer variants are included because no located source states that they are aerodynamically identical to the baseline.
4. **Tunnel models are `CONFIGURATION_UNKNOWN`** until a source gives scale and geometry state (T-10 or T-10S).
5. **Game and enthusiast data are prohibited**, both as values and as tuning references (`SU27-CFG-ENTHUSIAST-GAME`).
6. **Method papers are not aircraft data.** The TsAGI unsteady-aerodynamics and bifurcation papers describe methods. Their aircraft examples are not the Su-27.
7. **Class figures are identity.** The 12,500 kgf AL-31F figure is `PROHIBITED` as a model input and is never used to scale a curve.
8. **Unlicensed copies are not sources.** File-sharing scans of books and unofficial copies of the flight manual are not used.

## 4. Secondary-source disagreement (not registered as a value conflict)

Public secondary sources (web articles, seen as search extracts) disagree on the production FCS architecture. Some describe quadruplex analogue fly-by-wire in pitch and a limited analogue system with mechanical backup. Another says quadruplex fly-by-wire with no mechanical backup. None states the roll and yaw architecture clearly. No primary source was located to settle it. It is recorded as an architecture gap in [`MISSING_DATA.md`](MISSING_DATA.md), not in `CONFLICT_REGISTER.json`, because no numeric value is involved.
