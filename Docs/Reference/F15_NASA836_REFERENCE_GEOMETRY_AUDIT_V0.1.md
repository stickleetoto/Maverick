# F-15 NASA 836 — Aerodynamic Reference Geometry Source Audit V0.1

**Target:** `NASA_F15B_836_SN74_0141_PRE_QUIET_SPIKE_BASELINE_F100_PW_100`
**Question:** Do public primary sources support exact aerodynamic reference values for NASA F-15B 836 — reference area `S`, mean aerodynamic chord `c̄`, reference span `b`, and moment reference location?
**Code:** `MavF15ReferenceGeometrySources` (every candidate, graded) · `MavF15ReferenceData.CreateExactTargetGeometry()` · `MavF15ReferenceGeometryValidation`

---

## 1. Result

| Quantity | Status for exact NASA 836 | Grade |
|---|---|---|
| Reference area `S` | **UNAVAILABLE** | `UNAVAILABLE` |
| Mean aerodynamic chord `c̄` | **UNAVAILABLE** | `UNAVAILABLE` |
| Coefficient reference span `b` | **UNAVAILABLE** | `UNAVAILABLE` |
| Moment reference location | **UNAVAILABLE** | `UNAVAILABLE` |
| Physical wingspan | 42.8 ft = 13.04544 m | `DIRECT_EXACT_836` — **physical geometry only** |

No 836 source that I found prints `S`, `c̄`, a coefficient reference span or a moment reference. None states which reference dimensions its baseline aerodynamic model uses. Neither closure route the brief allows is available:

- **A — same-airframe source printing `S`/`c̄`:** not found.
- **B — explicit chain showing the 836 baseline model uses the unchanged production-F-15 reference dimensions, and naming them:** not found. The Quiet Spike reports say only that flight data "update the baseline aerodynamic model". None names the model's origin or its normalizing dimensions.

So `S` and `c̄` stay zero. **The reference span is also now zero.** That is a correction, and it's explained in §5.

---

## 2. Sources examined

All were retrieved from NTRS, all have public distribution, and all were read in the original PDF. The table pages that matter were checked against rendered page images.

### 2.1 The five named sources

| Source | NTRS | What it gives | `S` / `c̄` / `b_ref` / moment ref |
|---|---|---|---|
| NASA/TM-2012-215978, Moua, McWherter, Cox & Gera, *Flight Test Results on the Stability and Control of the F-15 Quiet Spike Aircraft* | 20120013435 | p.6 symbol list defines "MAC" as a symbol only. p.8 table 1 gives the mass state, with CG in % MAC. p.9 says the baseline aerodynamic model was updated from four flights, and points to refs 2–3 for the model. | none |
| NASA/TM-2009-214651, McWherter et al., *Stability and Control Analysis of the F-15B Quiet Spike Aircraft* | 20090034255 | p.9: F100-PW-100, and "References 11 and 12 offer a more detailed description of the airplane baseline aerodynamic model". p.6 defines FS as a symbol only. | none |
| NASA/TM-2005-213670, Vachon, Moes & Corda, *Local Flow Conditions for Propulsion Experiments on the F-15B PFTF* | 20050241960 | p.6: length 63.7 ft, **wingspan 42.8 ft**, height 18.7 ft | physical only |
| NASA/TM-2006-213674, Corda et al., *The F-15B LIFT Flight Test* | 20060022548 | p.12: **wingspan 42.8 ft (13.05 m)**, height, length, plus a dimensioned figure | physical only |
| Smolka et al., *Flight Testing of the Gulfstream Quiet Spike on a NASA F-15B* (SETP 2007) | 20070032807 | p.10: "USAF S/N 74-0141 … production F-100-PW-100 engines". p.24: "NASA 836". p.12: Quiet Spike interface FS/WL (the article's own geometry). p.25: baseline model → ref. 8. p.3/p.9: Boeing supplied an F-15C simulation for piloted evaluation. | none |

### 2.2 References followed from those sources

| Source | NTRS | Why followed | Result |
|---|---|---|---|
| NASA/TM-2008-214634 (= AIAA-2007-6638), Cumming, Smith & Frederick, *Aerodynamic Effects of a 24-Foot Multisegmented Telescoping Nose Boom on an F-15B Airplane* | 20080015840 (TM), 20070028417 (AIAA) | The baseline-aerodynamic-model reference in every Quiet Spike report | p.6 names **"tail number 836"** and F100-PW-100. p.10: the baseline flights "update the existing aerodynamic model". Coefficients are defined without reference dimensions. **No `S`, `c̄`, `b` or moment reference.** Its references are TM-4782, TM-4596, TM-2006-213675, Hoerner, Muraca, TM-101714, RP-1168 and TM-88280, and none of them is the aerodynamic model's source. |
| NASA TM-4782, Richwine, *F-15B/Flight Test Fixture II* (1996) | 19970005357 | Ref. 1 of TM-2012-215978, "a detailed description of the NASA DFRC F-15B" | p.10: "63.7 ft long with a **42.8-ft wingspan**", basic operating weight 27,500 lbm. No `S` or `c̄`. |
| NASA/TM-2006-213675, Smith, *Photogrammetric Trajectory Estimation of Foam Debris…* | 20060011345 | Cited by TM-2008-214634 as 836's research-test-bed history | p.18: three-view dimensioned 42.8 ft / 18.7 ft. Physical only. |
| NASA TM-4596, Richwine & Del Frate, *Low-Aspect-Ratio Fin…* | 19950010443 | Cited by TM-2008-214634 | "c.g. = 28% MAC" in a figure. No MAC length. |
| NASA TM-104315, Norlin, *Flight Simulation Software at NASA Dryden* | 19960001686 | The simulation reference (ref. 15 of TM-2009-214651) | Describes simulation software only; gives no F-15 reference geometry. |
| NASA TM-101714, Haering, *Airdata Calibration of a High-Performance Aircraft…* | 19900004912 | Cited by TM-2008-214634 | No F-15 reference geometry. |

### 2.3 Other NASA 836 reports searched

| Source | NTRS | Result |
|---|---|---|
| AIAA 2001-3303, *The F-15B Propulsion Flight Test Fixture* | 20030093544 | p.6: 42.8 ft (13.05 m) span. **p.12: "aircraft center of gravity used for the analysis was 28-percent mean aerodynamic chord, which corresponded to a fuselage station of 561.7."** A CG statement (§6). |
| *Analysis of a Channeled Centerbody Supersonic Inlet for F-15B Flight Research* | 20100001729 | p.1: PFTF "available for use on the NASA F-15B airplane, **tail number 836**". Identity link only. |
| *F-15B 836 Supersonic Research Testbed Capabilities* | 20160006705 | Briefing. No reference geometry. |
| NASA/TM-2007-214624 (ALSM) | 20070025193 | No reference geometry. |
| NASA/TM-2006-213673 (force-based flow-angle probe) | 20060007313 | p.13: 42.8 ft span (physical). |
| In-flight boundary-layer transition on a flat plate | 20120014341 | p.3: 42.8 ft span, cited to *Jane's* 1979–80 (a secondary source). |
| RAGE and flowfield-rake reports | 20120011162, 20100024394, 20120006692, 20090014209 | No aircraft reference geometry. |
| NASA/TM-2011-215977 / PFTF DRC reports | 20110023583 | No aircraft reference geometry. |

The NTRS searches `"F-15B reference area mean aerodynamic chord"` and `"F-15 wing area 608"` returned **zero** records.

### 2.4 Where 608 ft² / 15.94 ft actually appear

| Source | NTRS | Airframe | Printed values |
|---|---|---|---|
| NASA/TM-2003-212027, Smith & Moes, *Real-Time Stability and Control Derivative Extraction From F-15 Flight Data* | 20030079970 | **NF-15B 837.** p.8: "a pre-production … F-15B that has been highly modified … canards … two F100-PW-229 engines, each equipped with an axisymmetric thrust vectoring … nozzle". p.23 figure 2 marks the airplane **"837"**. | p.8 table 1 "Test aircraft reference dimensions" — "the reference areas and lengths used for nondimensionalizing forces and moments": **wing area 608 ft², MAC 15.94 ft, wing span 42.7 ft, moment reference FS 557.2, WL 116.3, BL 0.0.** p.23 three-view: **42.83 ft** span, 63.75 ft length, 18.67 ft height. |
| Morelli & Smith, *Real-Time Dynamic Modeling – Data Information Requirements and Flight Test Results* | 20100004852 (p.16), 20080034476 (p.15) | Same NF-15B 837 (p.9: pre-production, canards, F100-PW-229) | Table 1: c̄ 15.94 ft, b 42.70 ft, S 608.0 ft², x_ref 557.2 in, z_ref 116.3 in, **x_cg 560.40 in**, z_cg 117.41 in, I_x 24,830, I_y 196,225, I_z 216,155, I_xz −5,329 slug-ft² |
| Davison, AFIT/GAE/ENY/92M-01, Appendix C (the AFIT/Baumann research model) | — | McAir ARO10 / 1988 F-15 aerobase, production F-15 | printed p.121: "primary source … subroutine ARO10 from McAir code used in the F15 baseline simulator". p.123: `DATA CMCGR /.2565/, CNCGR /.2565/`, "the aero stability data was taken referenced to these CG locations". p.126–127: "SPAN = WING SPAN = **42.8 FEET** = BWING", "MAC = MEAN AERODYNAMIC CHORD = **15.94 FEET** = CWING". SREF is named (pp.121/123) but its value was not on the pages re-read in this pass. *Located in the source-lineage pass:* `SREF=608.` is printed in Davison's **driver program**, DTIC ADA256613 PDF p.91 (printed p.81), beside `BWING=42.8`, `CWING=15.94` and the comment `DATA IS FROM MCAIR REPORT# A4172 AND AFFTC-TR-75-32` (`F15_A4172_SOURCE_LINEAGE_V0.1.md`). |

This is the aircraft the brief warned about. The one NASA table that prints 608 ft² / 15.94 ft as coefficient reference dimensions describes **837**, not 836.

---

## 3. Physical geometry vs. coefficient reference: recorded separately

| Field | NASA 836 value | Source | May dimensionalize coefficients? |
|---|---|---|---|
| Physical wingspan | **42.8 ft = 13.04544 m** | TM-4782 p.10, TM-2005-213670 p.6, TM-2006-213674 p.12, AIAA 2001-3303 p.6, TM-2006-213675 p.18. 836 identity via TM-2008-214634 p.6. | **No** — physical |
| Physical length | 63.7 ft = 19.41576 m | same | No |
| Physical height | 18.7 ft = 5.69976 m | same | No |
| Coefficient reference area `S` | UNAVAILABLE | — | — |
| Reference chord `c̄` | UNAVAILABLE | — | — |
| Reference span `b` | UNAVAILABLE | — | — |
| Moment reference | UNAVAILABLE | — | — |

The sources themselves show that the distinction is real:

- **Same airplane, same report, two spans.** TM-2003-212027 dimensions NF-15B 837 at **42.83 ft** on its three-view and normalizes with **42.7 ft**.
- **Two F-15 aerodynamic models, two reference spans.** ARO10 uses **42.8 ft**, NF-15B 837 uses **42.7 ft**. A reference span belongs to its model.
- 836's 42.8 ft equals ARO10's reference span numerically. That shows nothing about 836's model, because the physical number and the reference number coincide in one lineage and differ in another.

---

## 4. Authority grades

`MavF15GeometryAuthority` is a separate axis from `MavF15GeometryQuantity`. A value can be exactly sourced and still be the wrong kind of quantity, which is what happened with the span.

| Candidate | Quantity | Value | Grade | Why |
|---|---|---|---|---|
| NASA836_PHYSICAL_SPAN | physical span | 42.8 ft | `DIRECT_EXACT_836` | Printed for the 836 airframe, as an overall dimension. Wrong quantity for normalizing. |
| NASA836_S / _CBAR / _B_REFERENCE / _MOMENT_REFERENCE | reference | — | `UNAVAILABLE` | Not printed in any 836 source |
| NASA836_TABLE1_CG | CG | 26.34 % MAC | `DIRECT_EXACT_836` | A CG location, not a moment reference |
| NASA836_PFTF_CG | CG | 28 % MAC = FS 561.7 | `DIRECT_EXACT_836` | The CG for one analysis. One equation in two unknowns. |
| NF15B837_S / _CBAR / _B_REFERENCE | reference | 608 ft² / 15.94 ft / 42.7 ft | **`INCOMPATIBLE`** | Genuine reference dimensions for a different airframe and model: preproduction, canards, PW-229, TV nozzles |
| NF15B837_PHYSICAL_SPAN | physical span | 42.83 ft | `INCOMPATIBLE` | Different airframe |
| NF15B837_MOMENT_REFERENCE | moment ref | FS 557.2 / WL 116.3 / BL 0.0 | `INCOMPATIBLE` | Different airframe. Printed beside a CG of FS 560.40. |
| ARO10_S / _CBAR / _B_REFERENCE | reference | 608 ft² / 15.94 ft / 42.8 ft | **`F15_FAMILY_SUPPORT`** | Production F-15 aerobase lineage. No source says the 836 model uses it. |
| ARO10_MOMENT_REFERENCE | moment ref | 0.2565 c̄ | `F15_FAMILY_SUPPORT` | As above |
| NASA836_PFTF_CG_FAMILY_CONSISTENCY | CG cross-check | — | `CROSS_VALIDATION_ONLY` | §6 |

**No candidate reaches `EXPLICIT_PRODUCTION_REFERENCE_USED_BY_836_MODEL` or `DIRECT_EXACT_836` for any coefficient-reference quantity.**

### Provenance of 608 ft² / 15.94 ft

- **In NASA material:** `INCOMPATIBLE`. Their only NASA printing as nondimensionalizing reference dimensions is NF-15B 837's.
- **In the production-aerobase lineage (ARO10, via Davison):** `F15_FAMILY_SUPPORT`, and in Maverick only through the separately tagged `CROSS_VALIDATION` research model.
- **For NASA 836:** no authority. Not `EXPLICIT_PRODUCTION_REFERENCE_USED_BY_836_MODEL`, because no 836 source names the reference dimensions of its baseline model. That the values are plausible, even likely, is not a source chain.

---

## 5. Correction: the physical span was in the reference-span field

Earlier revisions returned `wingSpanM = 13.04544 m` from `CreateExactTargetGeometry()` and labelled it `FROZEN_DIRECT`, while `S` and `c̄` were zero.

That field is the span `Cl`, `Cn`, `p̂ = pb/2V` and `r̂ = rb/2V` are normalized by. It has to come from the same reference set as `S` and `c̄`. The 42.8 ft is correctly sourced, but as the airframe's **physical** span. Placed beside two unavailable reference quantities, it made the geometry look one-third populated, when in fact no part of a reference set existed.

**Now:**

- The coefficient reference geometry's `wingSpanM` is **0**, like `S` and `c̄`.
- The physical span is kept as `MavF15ReferenceData.PhysicalWingSpanM = 42.8 ft × 0.3048 = 13.04544 m`. The value is unchanged; it is now correctly named and no longer used to normalize anything.
- Length and height now derive from the printed feet too, and reproduce the previous metres exactly.

**Behaviour:** none changed. The profile already failed closed on `S`/`c̄`, and it now fails closed on the same check with one more field zero. Nothing consumed the exact-target span.

---

## 6. The one cross-check, and why it can't be promoted

AIAA 2001-3303 p.12 (836, PFTF) says: 28 % MAC ↔ FS 561.7.

**If** NF-15B 837's moment reference FS 557.2 sits at ARO10's 25.65 % of a 15.94-ft (191.28-in) MAC, the leading-edge MAC is at 557.2 − 0.2565 × 191.28 = **FS 508.14**. 836's statement with the same chord gives 561.7 − 0.28 × 191.28 = **FS 508.14**. The two agree to about 0.01 in.

That is a striking consistency. It still has no authority, for three reasons:

1. **Both premises are assumptions.** No source says 837's FS 557.2 is at 25.65 % MAC. No source says 836 and 837 share a MAC, or even the same fuselage-station datum.
2. **836's statement is one equation in two unknowns.** Any `c̄` satisfies it with a suitable leading edge. It cannot select `c̄`.
3. **Consistency is necessary, not sufficient.** It shows the family values are not *contradicted* by 836's CG datum. It does not show that 836's model uses them.

It is recorded as `CROSS_VALIDATION_ONLY`. It is also a useful pointer for the next search: a source that ties 836's % MAC datum to a stated chord would settle `c̄`.

> **Update (source-lineage pass).** Premise 1 no longer depends on assumption. AFIT thesis AD-A244044 (App. A, printed p.47) publishes `%MAC = (FS − 508.1)/191.33 × 100` "for all A through D models of the F-15". That puts 836's 28 % MAC at FS 561.67, 837's FS 557.2 at 25.66 % MAC, and the AFIT tables' reference station FS 557.173 at 25.65 %. Four sources share one datum. It remains a **CG datum**, uncited within the thesis, and family-scoped, and no 836 source names its model's reference chord. `S`, `c̄` and `b` stay zero. See `F15_A4172_SOURCE_LINEAGE_V0.1.md` §7.

**CG vs. moment reference.** Both 836 CG statements (26.34 % MAC for table 1; 28 % MAC = FS 561.7) are CG locations. The only NASA table here that prints both a CG and a moment reference shows them at **different** stations (837: x_ref 557.2 in, x_cg 560.40 in). `TrySelectMomentReference` refuses every CG candidate, whatever its authority.

One consequence for the mass reference: the table-1 CG of 26.34 % MAC **cannot be placed in length units** without `c̄` and the leading-edge MAC station. That is the same gap, seen from the mass side.

---

## 7. What would close it

Any one of:

- a NASA 836 (or Quiet Spike / PFTF / LIFT) source that prints the simulation's reference area, chord and span;
- a NASA 836 source stating that its baseline F-15B aerodynamic model is a named production-F-15 database, **and** naming that database's reference dimensions;
- the DFRC F-15B simulation documentation itself.

If one is found, the change is data-only: add the candidates to `MavF15ReferenceGeometrySources.All` with the accepted grade and one shared `referenceSetId`. `CreateExactTargetGeometry()` then returns the set through `TrySelectExactTargetReferenceSet`, and `[G2]`/`[G6]` already cover the gate.

---

## 8. What this does not change

- **Live flight stays off.** Valid geometry would pass `MavFlightDynamicsProfile.IsValid`, but that is only the geometry gate. `[G7]` confirms that the other gates stay closed regardless: the exact aerodynamic envelope contains no state, the control hard stops allow zero travel, the exact coefficient source is `ExactNasa836Unavailable`, and propulsion provenance is `Unavailable`.
- Not touched: Baumann coefficient values or reference geometry, FCS gains, the propulsion characteristic, scenes, prefabs, `F15Replacement`.

---

## 9. Validation — `MavF15ReferenceGeometryValidation`

| Section | Checks |
|---|---|
| [G0] | source → SI conversion for every candidate. Physical dimensions derive from feet and reproduce the old metres. |
| [G1] | exact reference set unavailable. `CreateExactTargetGeometry()` is all zero. The profile fails closed on geometry. |
| [G2] | all six authority grades against a complete synthetic set: only the two accepting grades yield `S`, `c̄`, `b` > 0, and those equal the printed values converted |
| [G3] | all four occurrences of 608 ft² / 15.94 ft lack exact authority; each family set is refused even when complete |
| [G4] | the physical span cannot fill the reference-span slot, even beside an accepted `S`/`c̄`. Two sources show physical ≠ reference span. |
| [G5] | CG statements are refused as a moment reference. 837 prints them apart. The §6 cross-check is recorded as cross-validation only. |
| [G6] | mixed sets, duplicate areas and incomplete sets are refused. A single complete accepted set is selected with its id. |
| [G7] | hypothetical valid geometry leaves envelope, hard stops, coefficient source and propulsion closed |

Synthetic fixtures use 100 ft² / 10 ft / 20 ft, deliberately unlike any F-15 value, so that no fixture can be mistaken for data.
