# F-22 Source Graph (R1 feasibility)

**Feasibility only. No F-22 model is proposed.** Sources come from public USAF, NASA, NATO/RTO, AIAA and manufacturer material, plus the repository's F-22 catalogue on `sol/fa18-source-consolidation-r1` @e596c32 (`Docs/Reference/F22_*`). Configuration IDs: [`KNOWN_DATA.md`](KNOWN_DATA.md#1-configuration-registry).

---

## 1. Configuration spine (kept separate)

```
ATF competition
 └─ YF-22 prototypes (Dem/Val, 1990-91) ........... F22-CFG-YF22-DEMVAL   YF22_ONLY: never production authority
       different geometry from the F-22 (NASA-SP-2000-4519 history, repository reading)

F-22 EMD / development
 ├─ 1996 control-law design state ................. F22-CFG-EMD-1996-CLAW   (AIAA-96-3379)
 ├─ 1999-2000 high-AOA test aircraft .............. F22-CFG-EMD-HIGH-AOA    (SFTE-2000-PERON-F22-HIGH-AOA)
 └─ tunnel models (13.3 % buffet, dynamic, V-9) ... F22-CFG-WT-EARLY-F22    CONFIGURATION_UNKNOWN until each report is read

Production F-22A .................................. F22-CFG-PROD-F22A       (fact sheet; everything technical NOT PUBLIC)
Engine F119-PW-100 ................................ ENG-F119-PW-100

Unrelated / generic shapes ........................ F22-CFG-GENERIC-UNRELATED (ICE 101, educational "F-22-like" models)
```

## 2. Lineage

```
YF-22 (Dem/Val) ── AIAA-94-2105 (thrust-vector-aided manoeuvring; YF-22 only)
      │           NRC-1997-AVIATION-SAFETY-PILOT-CONTROL (YF-22 PIO case; rate limiting)
      │ geometry changed between YF-22 and F-22 (NASA-SP-2000-4519 history, repository reading)
      ▼
F-22 EMD design ── AIAA-96-3379 (Harris & Black 1996: design philosophy; CAP 0.35-1.0 and damping 1.1-1.2 targets per extract;
      │            VISTA NF-16D in-flight simulation, cf. F-16 pack AIAA-2018-0525 for the airframe)
      │ tunnel programme (NASA-SP-2000-4519: 1992 Langley full-scale tunnel, 1993 spin tunnel)
      │    └─ data packages: F22-LANGLEY-1992-FST-1993-SPIN-DATA (NOT PUBLICLY LOCATED)
      │ AFRL dynamic tunnel ── AIAA-99-4015 (Gillard; paywalled; model state unknown)
      │ early-model buffet ── NTRS-20000052124 (13.3 % scale), JAIRCRAFT-2006-ANDERSON-F22-BUFFET (V-9 model)
      ▼
F-22 EMD flight test ── SFTE-2000-PERON-F22-HIGH-AOA (below -40 to above +60 deg AOA explored; triplex FLCS; pitch TV)
      ▼
Production F-22A ── USAF-F22-FACT-SHEET (identity), AIAA-2013-0972 (SEEK EAGLE CFD, Distribution A; content unassessed)
                    F22-PRODUCTION-FCS-AERO-DATA (NOT PUBLICLY LOCATED)

F119-PW-100 ── PW-F119-PRODUCT-PAGE (+/-20 deg pitch vectoring, identity)
            ── NTRS-20050169937 (LO nozzle-flap internal performance vs P&W CFD, May 1995)
            ── RTO-MP-SCI-162-P23 (STORM / component-level installed-thrust model; accuracy statements) + ASME-GT2002-30001
            ── F119-PERFORMANCE-DECK (proprietary; do not estimate)
            ── AEDC-F22-TEST-HISTORY (test-programme existence)

Generic: AFRL-VA-WP-TR-1998-3043 (ICE 101: NOT an F-22), RTO-TR-029 (FCS design practice)
```

## 3. Rules applied

1. **YF-22 data is never promoted** to F-22 EMD or production. `AIAA-94-2105` is tagged `F22-CFG-YF22-DEMVAL` and `PROHIBITED` for implementation.
2. **EMD is not automatically production.** The 1996 design targets and 1999-2000 AOA envelope describe development states.
3. **Tunnel models are `CONFIGURATION_UNKNOWN`** until the report shows which geometry was tested.
4. **Fact-sheet numbers are identity data.** 35,000-lb class thrust and 44 ft 6 in span are `PROHIBITED` as model inputs. A class figure is never used to scale a curve.
5. **Unrelated F-22-like shapes are recorded so they are not mistaken for F-22 data** (ICE 101).
